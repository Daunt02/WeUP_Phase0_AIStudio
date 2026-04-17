using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// EF Core + Postgres implementation of IEventRepository.
/// Replaces StubEventRepository once the database is provisioned.
/// </summary>
public sealed class EfEventRepository(WeUpDbContext db) : IEventRepository, IEventSubmissionRepository, IEventLifecycleRepository
{
    // ---------------------------------------------------------------------------
    // Map feed
    // ---------------------------------------------------------------------------

    public async Task<MapFeedResponse> GetMapFeedAsync(MapFeedRequest request, CancellationToken ct = default)
    {
        var bb = request.Bounds;
        var w = request.Window;

        var query = db.Events
            .AsNoTracking()
            .Where(e => e.Status == "PUBLISHED")
            .Where(e => e.Latitude >= bb.MinLat && e.Latitude <= bb.MaxLat)
            .Where(e => e.Longitude >= bb.MinLng && e.Longitude <= bb.MaxLng)
            .Where(e => e.StartUtc >= w.StartUtc && e.StartUtc < w.EndUtc);

        if (request.Categories?.Length > 0)
            query = query.Where(e => request.Categories.Contains(e.Category));

        var locality = NormalizeLocality(request.Locality, request.DistrictCode);
        query = ApplyLocalityFilter(query, locality);

        if (request.MinConfidence > 0)
            query = query.Where(e => e.Confidence >= request.MinConfidence);

        query = request.Sort switch
        {
            "freshness"    => query.OrderByDescending(e => e.CreatedAt),
            "confidence"   => query.OrderByDescending(e => e.Confidence),
            "start_time_desc" => query.OrderByDescending(e => e.StartUtc),
            _              => query.OrderBy(e => e.StartUtc),
        };

        var entities = await query
            .Include(e => e.Media)
            .ToListAsync(ct);

        var aggregates = entities.Select(entity => entity.ToCanonicalAggregate()).ToArray();
        var events = aggregates.Select(ToMapCard).ToArray();
        var clusters = BuildViewportClusters(events);
        return new MapFeedResponse(events, events.Length, clusters, "bounding_box");
    }

    // ---------------------------------------------------------------------------
    // Calendar feed
    // ---------------------------------------------------------------------------

    public async Task<CalendarFeedResponse> GetCalendarFeedAsync(CalendarFeedRequest request, CancellationToken ct = default)
    {
        var w = request.Window;

        var query = db.Events
            .AsNoTracking()
            .Where(e => e.Status == "PUBLISHED")
            .Where(e => e.StartUtc >= w.StartUtc && e.StartUtc < w.EndUtc);

        if (request.Categories?.Length > 0)
            query = query.Where(e => request.Categories.Contains(e.Category));

        var locality = NormalizeLocality(request.Locality, request.DistrictCode);
        query = ApplyLocalityFilter(query, locality);

        if (request.MinConfidence > 0)
            query = query.Where(e => e.Confidence >= request.MinConfidence);

        var totalCount = await query.CountAsync(ct);

        query = query.OrderBy(e => e.StartUtc);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.Media)
            .ToListAsync(ct);

        var items = entities
            .Select(entity => entity.ToCanonicalAggregate())
            .Select(ToCalendar)
            .ToArray();
        return new CalendarFeedResponse(items, totalCount, page, pageSize, (page - 1) * pageSize + items.Length < totalCount);
    }

    // ---------------------------------------------------------------------------
    // Event detail
    // ---------------------------------------------------------------------------

    public async Task<EventDetailResponse> GetEventDetailAsync(string eventId, CancellationToken ct = default)
    {
        var entity = await db.Events
            .AsNoTracking()
            .Include(e => e.Media)
            .Include(e => e.Sources)
            .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

        if (entity is null) return new EventDetailResponse(null);
        var aggregate = entity.ToCanonicalAggregate();
        return new EventDetailResponse(ToDetail(aggregate, entity));
    }

    // ---------------------------------------------------------------------------
    // Event submission
    // ---------------------------------------------------------------------------

    public async Task<string> CreateSubmissionAsync(string userId, EventSubmissionRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var publicId = $"evt-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var entity = new EventEntity
        {
            Id = id,
            PublicId = publicId,
            Status = EventLifecycleStatusMapper.ToStorage(EventLifecycleStatus.Draft),
            CanonicalTitle = request.Title,
            CanonicalDescription = request.Description,
            Category = request.Category,
            VenueName = request.VenueName,
            AddressLine1 = request.Address,
            AddressCity = string.Empty,
            MarketCode = null,
            DistrictCode = null,
            NeighborhoodCode = null,
            AddressCountry = "US",
            AddressRaw = request.Address,
            Latitude = 0,
            Longitude = 0,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Timezone = request.Timezone,
            Confidence = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
        };

        entity.Sources.Add(new EventSourceEntity
        {
            Id = Guid.NewGuid(),
            EventId = id,
            SourceKind = "manual_submission",
            SourceRef = userId,
            IngestedAt = now,
            SubmitterId = userId,
        });

        if (request.Tags is { Length: > 0 })
            entity.TagsCsv = string.Join(',', request.Tags);

        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);
        return publicId;
    }

    public async Task<string?> GetSubmissionStatusAsync(string submissionId, CancellationToken ct = default)
    {
        return await db.Events
            .AsNoTracking()
            .Where(e => e.PublicId == submissionId)
            .Select(e => (string?)e.Status)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetLifecycleStatusAsync(string eventId, CancellationToken ct = default)
    {
        return await db.Events
            .AsNoTracking()
            .Where(e => e.PublicId == eventId)
            .Select(e => (string?)e.Status)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TransitionLifecycleStatusAsync(string eventId, string newStatus, CancellationToken ct = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
        if (entity is null)
        {
            return false;
        }

        var aggregate = entity.ToCanonicalAggregate();
        var nextStatus = EventLifecycleStatusMapper.FromStorage(newStatus);
        aggregate.EnsureCanTransitionTo(nextStatus);

        entity.Status = EventLifecycleStatusMapper.ToStorage(nextStatus);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------------------------------------------------
    // Projections
    // ---------------------------------------------------------------------------

    private static EventMapCardDto ToMapCard(EventAggregate e) => new(
        e.CanonicalEventId,
        e.Title,
        e.VenueName,
        e.Category,
        e.Latitude,
        e.Longitude,
        null,
        EventLifecycleStatusMapper.ToStorage(e.EventStatus),
        e.ConfidenceScore);

    private static EventCalendarDto ToCalendar(EventAggregate e) => new(
        e.CanonicalEventId,
        e.Title,
        e.VenueName,
        e.Category,
        e.StartUtc,
        e.EndUtc,
        e.TimeZone,
        null,
        EventLifecycleStatusMapper.ToStorage(e.EventStatus));

    private static EventDetailDto ToDetail(EventAggregate e, EventEntity sourceEntity) => new(
        e.CanonicalEventId,
        e.Title,
        e.Description,
        e.VenueName,
        e.Address.RawAddress,
        e.Latitude,
        e.Longitude,
        e.Category,
        e.StartUtc,
        e.EndUtc,
        e.TimeZone,
        sourceEntity.Media.Select(m => new MediaRefDto(m.Url, m.Kind)).ToArray(),
        string.IsNullOrEmpty(sourceEntity.TagsCsv) ? [] : sourceEntity.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
        EventLifecycleStatusMapper.ToStorage(e.EventStatus),
        e.ConfidenceScore,
        e.Provenance.PrimarySourceKind);

    private static string? PrimaryThumbnail(EventEntity e)
    {
        var poster = e.Media.FirstOrDefault(m => m.Kind == "poster");
        var image  = e.Media.FirstOrDefault(m => m.Kind == "image");
        return (poster ?? image)?.Url;
    }

    private static IQueryable<EventEntity> ApplyLocalityFilter(
        IQueryable<EventEntity> query,
        LocalityFilterRequest? locality)
    {
        if (locality == null)
            return query;

        if (!string.IsNullOrWhiteSpace(locality.MarketCode))
        {
            var marketCode = locality.MarketCode.Trim();
            query = query.Where(e => e.MarketCode == marketCode);
        }

        if (!string.IsNullOrWhiteSpace(locality.DistrictCode))
        {
            var districtCode = locality.DistrictCode.Trim();
            query = query.Where(e => e.DistrictCode == districtCode);
        }

        if (!string.IsNullOrWhiteSpace(locality.NeighborhoodCode))
        {
            var neighborhoodCode = locality.NeighborhoodCode.Trim();
            query = query.Where(e => e.NeighborhoodCode == neighborhoodCode);
        }

        return query;
    }

    private static LocalityFilterRequest? NormalizeLocality(LocalityFilterRequest? locality, string? legacyDistrictCode)
    {
        if (locality != null)
            return locality;

        if (string.IsNullOrWhiteSpace(legacyDistrictCode))
            return null;

        return new LocalityFilterRequest(DistrictCode: legacyDistrictCode);
    }

    private static EventMapClusterDto[]? BuildViewportClusters(EventMapCardDto[] events)
    {
        if (events.Length == 0)
            return [];

        // Phase 0 cluster seam: fixed-size quantization; replace with PostGIS ST_Cluster*.
        var clusters = events
            .GroupBy(e => $"{Math.Round(e.Lat, 2):F2}:{Math.Round(e.Lng, 2):F2}")
            .Select(group => new EventMapClusterDto(
                ClusterId: group.Key,
                CenterLat: group.Average(e => e.Lat),
                CenterLng: group.Average(e => e.Lng),
                Count: group.Count(),
                EventIds: group.Select(e => e.Id).ToArray()))
            .OrderByDescending(cluster => cluster.Count)
            .ToArray();

        return clusters;
    }
}
