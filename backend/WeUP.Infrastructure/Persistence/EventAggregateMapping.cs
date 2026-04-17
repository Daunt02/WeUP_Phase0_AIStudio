using System.Globalization;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence;

internal static class EventAggregateMapping
{
    public static EventAggregate ToCanonicalAggregate(this EventEntity entity)
    {
        var lifecycle = EventLifecycleStatusMapper.FromStorage(entity.Status);
        var moderationStatus = lifecycle switch
        {
            EventLifecycleStatus.Reviewed => EventModerationStatus.InReview,
            EventLifecycleStatus.Approved => EventModerationStatus.Approved,
            EventLifecycleStatus.Rejected => EventModerationStatus.Rejected,
            _ => EventModerationStatus.Unreviewed,
        };

        var publishStatus = lifecycle switch
        {
            EventLifecycleStatus.Published => EventPublishStatus.Published,
            EventLifecycleStatus.Archived => EventPublishStatus.Archived,
            EventLifecycleStatus.Rejected => EventPublishStatus.NotEligible,
            EventLifecycleStatus.Approved => EventPublishStatus.Eligible,
            EventLifecycleStatus.Cancelled => EventPublishStatus.Unpublished,
            _ => EventPublishStatus.EligibilityPending,
        };

        var riskLevel = entity.Confidence switch
        {
            >= 0.80 => EventRiskLevel.Low,
            >= 0.60 => EventRiskLevel.Medium,
            >= 0.40 => EventRiskLevel.High,
            _ => EventRiskLevel.Restricted,
        };

        var sourceRefs = entity.Sources
            .Select(s => s.SourceRef)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var evidenceRefs = entity.Sources
            .Where(source => source.SourceKind.Equals("evidence_ref", StringComparison.OrdinalIgnoreCase))
            .Select(source => source.SourceRef)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var externalRefs = entity.Sources
            .Where(source => !source.SourceKind.Equals("evidence_ref", StringComparison.OrdinalIgnoreCase))
            .Select(source => new ExternalEventReference(
                ReferenceType: source.SourceKind,
                SourceSystem: source.SourceKind,
                ReferenceId: source.SourceRef,
                Url: null,
                Metadata: null))
            .ToArray();

        var firstObservedAtUtc = entity.Sources.Count == 0
            ? entity.CreatedAt
            : entity.Sources.Min(source => source.IngestedAt);

        var lastObservedAtUtc = entity.Sources.Count == 0
            ? entity.UpdatedAt
            : entity.Sources.Max(source => source.IngestedAt);

        var provenance = new EventProvenanceMetadata(
            PrimarySourceKind: entity.Sources.FirstOrDefault()?.SourceKind ?? "unknown",
            PrimarySourceRef: entity.Sources.FirstOrDefault()?.SourceRef ?? entity.PublicId,
            EvidenceRefs: evidenceRefs,
            FirstObservedAtUtc: firstObservedAtUtc,
            LastObservedAtUtc: lastObservedAtUtc,
            SourceRefs: sourceRefs,
            Metadata: null);

        var lineage = new EventMergeLineage(
            ParentCanonicalEventId: null,
            MergedCanonicalEventIds: entity.Sources
                .Where(source => source.SourceKind.Equals("resolution_source", StringComparison.OrdinalIgnoreCase))
                .Select(source => source.SourceRef)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AppliedMergePlanIds: [],
            LastMergedAtUtc: null,
            LastMergedBy: null);

        var aggregate = new EventAggregate(
            CanonicalEventId: entity.PublicId,
            SourceEventIds: sourceRefs,
            ExternalReferences: externalRefs,
            Title: entity.CanonicalTitle,
            Description: entity.CanonicalDescription,
            Tags: string.IsNullOrWhiteSpace(entity.TagsCsv)
                ? []
                : entity.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Category: entity.Category,
            VenueName: entity.VenueName,
            Address: new EventAddress(
                AddressLine1: entity.AddressLine1,
                City: entity.AddressCity,
                State: entity.AddressState,
                PostalCode: entity.AddressPostalCode,
                Country: entity.AddressCountry,
                RawAddress: entity.AddressRaw,
                MarketCode: entity.MarketCode,
                DistrictCode: entity.DistrictCode,
                NeighborhoodCode: entity.NeighborhoodCode),
            Latitude: entity.Latitude,
            Longitude: entity.Longitude,
            TimeZone: entity.Timezone,
            StartUtc: entity.StartUtc,
            EndUtc: entity.EndUtc,
            LocalStartDisplay: ToLocalDisplay(entity.StartUtc, entity.Timezone),
            LocalEndDisplay: entity.EndUtc.HasValue ? ToLocalDisplay(entity.EndUtc.Value, entity.Timezone) : null,
            EventStatus: lifecycle,
            PublishStatus: publishStatus,
            ModerationStatus: moderationStatus,
            RiskLevel: riskLevel,
            ConfidenceScore: entity.Confidence,
            Provenance: provenance,
            CreatedAtUtc: entity.CreatedAt,
            UpdatedAtUtc: entity.UpdatedAt,
            Version: 1,
            MergeLineage: lineage);

        aggregate.Validate();
        return aggregate;
    }

    public static EventAggregateSnapshot ToAggregateSnapshot(this EventEntity entity)
    {
        var aggregate = entity.ToCanonicalAggregate();
        return new EventAggregateSnapshot(
            CanonicalEventId: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            VenueName: aggregate.VenueName,
            Address: aggregate.Address.RawAddress,
            Latitude: aggregate.Latitude,
            Longitude: aggregate.Longitude,
            StartUtc: aggregate.StartUtc.ToString("O", CultureInfo.InvariantCulture),
            EndUtc: aggregate.EndUtc?.ToString("O", CultureInfo.InvariantCulture),
            Timezone: aggregate.TimeZone,
            Category: aggregate.Category,
            Confidence: aggregate.ConfidenceScore,
            SourceRefs: aggregate.Provenance.SourceRefs,
            EvidenceRefs: aggregate.Provenance.EvidenceRefs,
            IsApproved: aggregate.EventStatus == EventLifecycleStatus.Approved || aggregate.EventStatus == EventLifecycleStatus.Published,
            ExternalSourceId: aggregate.ExternalReferences.FirstOrDefault()?.ReferenceId ?? aggregate.CanonicalEventId);
    }

    private static string ToLocalDisplay(DateTimeOffset utc, string timeZone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            var local = TimeZoneInfo.ConvertTime(utc, tz);
            return local.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
        }
        catch
        {
            return utc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }
    }
}