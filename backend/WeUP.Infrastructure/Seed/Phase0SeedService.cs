using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeUP.Infrastructure.Auth;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Markets;
using WeUP.Infrastructure.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;
using WeUP.Infrastructure.Submissions;

namespace WeUP.Infrastructure.Seed;

public sealed class Phase0SeedService(
    IConfiguration configuration,
    IServiceProvider services,
    Phase0SeedLoader loader,
    MarketPolicyService markets)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Phase0SeedSnapshot? _lastSnapshot;

    public Phase0SeedSnapshot? CurrentSnapshot => _lastSnapshot;

    public async Task<Phase0SeedSnapshot> ResetAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var dataset = loader.Load();

            await ResetPersistenceAsync(dataset, ct);
            ResetInMemoryStores(dataset);
            markets.Reset(dataset);

            _lastSnapshot = new Phase0SeedSnapshot(
                dataset.Meta.SeedVersion,
                dataset.Meta.FixedNow,
                dataset.Markets.Length,
                dataset.Venues.Length,
                dataset.Events.Length,
                dataset.Users.Length,
                dataset.Saves.Length,
                dataset.ModerationItems.Length,
                dataset.IngestionJobs.Length,
                loader.ResolveDatasetPath());

            return _lastSnapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ResetPersistenceAsync(Phase0SeedDataset dataset, CancellationToken ct)
    {
        if (!string.Equals(configuration["WeUP:PersistenceMode"], "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var db = services.GetService<WeUpDbContext>();
        if (db is null)
        {
            return;
        }

        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE \"saved_events\", \"event_sources\", \"event_media\", \"event_reviews\", \"event_submissions\", \"events\", \"user_roles\", \"user_preferences\", \"user_profiles\" RESTART IDENTITY CASCADE;",
                ct);
        }
        else
        {
            db.SavedEvents.RemoveRange(db.SavedEvents);
            db.EventSources.RemoveRange(db.EventSources);
            db.EventMedia.RemoveRange(db.EventMedia);
            db.EventReviews.RemoveRange(db.EventReviews);
            db.EventSubmissions.RemoveRange(db.EventSubmissions);
            db.Events.RemoveRange(db.Events);
            db.UserRoles.RemoveRange(db.UserRoles);
            db.UserPreferences.RemoveRange(db.UserPreferences);
            db.UserProfiles.RemoveRange(db.UserProfiles);
            await db.SaveChangesAsync(ct);
        }

        var venues = dataset.Venues.ToDictionary(v => v.VenueId, StringComparer.OrdinalIgnoreCase);
        var marketsByCode = dataset.Markets.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var seedTimestamp = DateTimeOffset.Parse(dataset.Meta.FixedNow);

        var users = dataset.Users.Select(user => new UserProfileEntity
        {
            Id = DeterministicGuid.Create($"user:{user.UserId}"),
            PublicId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            HomeMarket = user.HomeMarket,
            OnboardingState = user.OnboardingState,
            CreatedAt = DateTimeOffset.Parse(user.CreatedAt),
            UpdatedAt = DateTimeOffset.Parse(user.CreatedAt),
        }).ToArray();

        var events = dataset.Events.Select(seedEvent =>
        {
            var venue = venues[seedEvent.VenueId];
            return new EventEntity
            {
                Id = DeterministicGuid.Create($"event:{seedEvent.EventId}"),
                PublicId = seedEvent.EventId,
                Status = seedEvent.Status,
                CanonicalTitle = seedEvent.Title,
                CanonicalDescription = seedEvent.Description,
                Category = seedEvent.Category,
                VenueName = venue.Name,
                AddressLine1 = venue.Address,
                AddressCity = venue.DistrictCode,
                AddressCountry = "US",
                AddressRaw = venue.Address,
                Latitude = venue.Latitude,
                Longitude = venue.Longitude,
                StartUtc = DateTimeOffset.Parse(seedEvent.StartsAtUtc),
                EndUtc = DateTimeOffset.Parse(seedEvent.EndsAtUtc),
                Timezone = marketsByCode[venue.MarketCode].Timezone,
                TagsCsv = seedEvent.Tags.Length == 0 ? null : string.Join(',', seedEvent.Tags),
                Confidence = seedEvent.Confidence,
                CreatedAt = seedTimestamp,
                UpdatedAt = seedTimestamp,
            };
        }).ToArray();

        var eventMedia = dataset.Events.Select(seedEvent => new EventMediaEntity
        {
            Id = DeterministicGuid.Create($"event-media:{seedEvent.EventId}"),
            EventId = DeterministicGuid.Create($"event:{seedEvent.EventId}"),
            AssetId = $"asset-{seedEvent.EventId}",
            Url = seedEvent.ImageUrl,
            Kind = "poster",
            CreatedAt = seedTimestamp,
        }).ToArray();

        var eventSources = dataset.Events.Select(seedEvent => new EventSourceEntity
        {
            Id = DeterministicGuid.Create($"event-source:{seedEvent.EventId}"),
            EventId = DeterministicGuid.Create($"event:{seedEvent.EventId}"),
            SourceKind = seedEvent.SourceKind,
            SourceRef = seedEvent.EventId,
            ExtractionVersion = dataset.Meta.SeedVersion,
            IngestedAt = seedTimestamp,
        }).ToArray();

        var saves = dataset.Saves.Select(save => new SavedEventEntity
        {
            UserId = DeterministicGuid.Create($"user:{save.UserId}"),
            EventId = DeterministicGuid.Create($"event:{save.EventId}"),
            SavedAt = DateTimeOffset.Parse(save.SavedAt),
        }).ToArray();

        var roles = dataset.Users
            .SelectMany(user =>
            {
                var seeded = user.Roles is { Length: > 0 }
                    ? user.Roles
                    : ["user"];

                return seeded
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role.Trim().ToLowerInvariant())
                    .Append("user")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(role => new UserRoleEntity
                    {
                        UserId = DeterministicGuid.Create($"user:{user.UserId}"),
                        Role = role,
                        AssignedAt = seedTimestamp,
                    });
            })
            .ToArray();

        await db.UserProfiles.AddRangeAsync(users, ct);
        await db.Events.AddRangeAsync(events, ct);
        await db.EventMedia.AddRangeAsync(eventMedia, ct);
        await db.EventSources.AddRangeAsync(eventSources, ct);
        await db.SavedEvents.AddRangeAsync(saves, ct);
        await db.UserRoles.AddRangeAsync(roles, ct);
        await db.SaveChangesAsync(ct);
    }

    private void ResetInMemoryStores(Phase0SeedDataset dataset)
    {
        services.GetService<StubEventRepository>()?.Reset(dataset);
        services.GetService<StubSaveRepository>()?.Reset(dataset);
        services.GetService<InMemoryUserRepository>()?.Reset(dataset);
        services.GetService<InMemoryUserRoleRepository>()?.Reset(dataset);
        services.GetService<InMemorySubmissionRepository>()?.Reset(dataset);
        services.GetService<InMemoryModerationQueue>()?.Reset(dataset);
        services.GetService<InMemoryIngestionJobRepository>()?.Reset(dataset);
    }
}