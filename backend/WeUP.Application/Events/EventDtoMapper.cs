using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;

namespace WeUP.Application.Events;

// ---------------------------------------------------------------------------
// M4-P17: Event DTO Mapping Boundary
//
// Field visibility classification:
//   FRONTEND-SAFE  — safe to expose to any authenticated browser client
//   OPERATIONAL    — moderator/operator tools only (requires moderator auth)
//   INTERNAL-ONLY  — never exposed through any API surface
//
// Mapping rules:
//   1. EventAggregate → EventMapCardDto    (FRONTEND-SAFE)
//   2. EventAggregate → EventCalendarCardDto (FRONTEND-SAFE)
//   3. EventAggregate → EventDetailDto       (FRONTEND-SAFE)
//   4. EventAggregate → EventModerationDto   (OPERATIONAL)
//   5. EventAggregate + PublishEligibilityResult → EventPublishEligibilityDto (OPERATIONAL)
//
// INTERNAL-ONLY fields (never mapped to any DTO):
//   - Provenance.EvidenceRefs
//   - Provenance.SourceRefs
//   - MergeLineage.AppliedMergePlanIds
//   - ExternalReferences
//   - SourceEventIds
// ---------------------------------------------------------------------------

/// <summary>
/// Explicit, inspectable, testable mapper from <see cref="EventAggregate"/> to API DTOs.
/// No reflection, no convention-based magic. Every field mapping is traceable to this class.
/// </summary>
public static class EventDtoMapper
{
    // -----------------------------------------------------------------------
    // 1. Map card (FRONTEND-SAFE)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produce the lean map-pin card DTO.
    /// Exposes: id, title, venue, category, coordinates, thumbnail, lifecycle status, confidence.
    /// Omits: provenance, moderation details, source refs, merge lineage, address.
    /// </summary>
    public static EventMapCardDto ToMapCard(EventAggregate aggregate, string? thumbnailUrl = null)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return new EventMapCardDto(
            Id: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            VenueName: aggregate.VenueName,
            Category: aggregate.Category,
            Lat: aggregate.Latitude,
            Lng: aggregate.Longitude,
            ThumbnailUrl: thumbnailUrl,
            Status: SerializeLifecycleStatus(aggregate.EventStatus),
            Confidence: aggregate.ConfidenceScore);
    }

    // -----------------------------------------------------------------------
    // 2. Calendar card (FRONTEND-SAFE)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produce the calendar card DTO with temporal fields.
    /// Exposes: id, title, venue, category, UTC start/end, timezone, thumbnail, lifecycle status.
    /// Omits: address, coordinates, confidence, provenance, moderation details.
    /// </summary>
    public static EventCalendarCardDto ToCalendarCard(EventAggregate aggregate, string? thumbnailUrl = null)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return new EventCalendarCardDto(
            Id: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            VenueName: aggregate.VenueName,
            Category: aggregate.Category,
            StartUtc: aggregate.StartUtc,
            EndUtc: aggregate.EndUtc,
            Timezone: aggregate.TimeZone,
            ThumbnailUrl: thumbnailUrl,
            Status: SerializeLifecycleStatus(aggregate.EventStatus));
    }

    // -----------------------------------------------------------------------
    // 3. Event detail (FRONTEND-SAFE)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produce the full event detail DTO.
    /// Exposes: all display fields, tags, media refs (url+kind only), source kind label.
    /// Omits: provenance refs, moderation state, risk level, merge lineage, raw address.
    /// </summary>
    /// <param name="aggregate">The canonical event aggregate.</param>
    /// <param name="mediaRefs">Resolved media refs from the media service. Pass empty array if none.</param>
    /// <param name="sourceKindLabel">Human-readable source kind label (e.g. "flyer upload").</param>
    public static EventDetailDto ToDetail(
        EventAggregate aggregate,
        MediaRefDto[] mediaRefs,
        string sourceKindLabel)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(mediaRefs);
        return new EventDetailDto(
            Id: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            Description: aggregate.Description,
            VenueName: aggregate.VenueName,
            Address: FormatAddress(aggregate.Address),
            Lat: aggregate.Latitude,
            Lng: aggregate.Longitude,
            Category: aggregate.Category,
            StartUtc: aggregate.StartUtc,
            EndUtc: aggregate.EndUtc,
            Timezone: aggregate.TimeZone,
            MediaRefs: mediaRefs,
            Tags: aggregate.Tags,
            Status: SerializeLifecycleStatus(aggregate.EventStatus),
            Confidence: aggregate.ConfidenceScore,
            SourceKind: sourceKindLabel,
            Version: aggregate.Version,
            LastChangeType: aggregate.LatestChange?.ChangeType.ToString(),
            ConcurrencyToken: aggregate.EffectiveConcurrencyToken);
    }

    // -----------------------------------------------------------------------
    // 4. Moderation view (OPERATIONAL — requires moderator auth)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produce the moderation-view DTO.
    /// Exposes: lifecycle, moderation, publish status, risk level, version, merge lineage summary.
    /// Omits: raw provenance refs, evidence chain, external references, source event IDs.
    /// </summary>
    public static EventModerationDto ToModeration(EventAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return new EventModerationDto(
            CanonicalEventId: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            Category: aggregate.Category,
            VenueName: aggregate.VenueName,
            StartUtc: aggregate.StartUtc,
            EndUtc: aggregate.EndUtc,
            Timezone: aggregate.TimeZone,
            LifecycleStatus: aggregate.EventStatus.ToString(),
            ModerationStatus: aggregate.ModerationStatus.ToString(),
            PublishStatus: aggregate.PublishStatus.ToString(),
            RiskLevel: aggregate.RiskLevel.ToString(),
            ConfidenceScore: aggregate.ConfidenceScore,
            Version: aggregate.Version,
            UpdatedAtUtc: aggregate.UpdatedAtUtc,
                LastChangeType: aggregate.LatestChange?.ChangeType.ToString(),
                HasPendingReview: aggregate.LatestChange?.RequiresModerationReview == true,
                ConcurrencyToken: aggregate.EffectiveConcurrencyToken,
            MergeLineage: ToMergeLineageSummary(aggregate.MergeLineage));
    }

    // -----------------------------------------------------------------------
    // 5. Publish eligibility (OPERATIONAL — requires moderator/operator auth)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produce the publish eligibility DTO from an aggregate + pre-evaluated eligibility result.
    /// Exposes: eligibility band, blockers (code+message+field only), confidence summary, field completeness.
    /// Omits: raw policy weights, internal dimension weight overrides, full provenance context.
    /// </summary>
    public static EventPublishEligibilityDto ToPublishEligibility(
        EventAggregate aggregate,
        PublishEligibilityResult result)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(result);
        return new EventPublishEligibilityDto(
            CanonicalEventId: aggregate.CanonicalEventId,
            Eligible: result.Eligible,
            EligibilityBand: result.EligibilityBand.ToString(),
            RecommendedAction: result.RecommendedNextAction.ToString(),
            Blockers: result.Blockers.Select(ToBlockerSummary).ToArray(),
            ConfidenceSummary: ToEligibilityConfidenceSummary(result.ConfidenceSummary),
            FieldCompleteness: ToFieldCompletenessSummary(result.FieldCompletenessSummary),
            Notes: result.Notes);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serialize lifecycle status to the canonical frontend uppercase string.
    /// Frontend consumers must match these exact string values.
    /// </summary>
    public static string SerializeLifecycleStatus(EventLifecycleStatus status)
        => status switch
        {
            EventLifecycleStatus.Draft => "DRAFT",
            EventLifecycleStatus.Candidate => "NEEDS_REVIEW",
            EventLifecycleStatus.Reviewed => "NEEDS_REVIEW",
            EventLifecycleStatus.Approved => "APPROVED",
            EventLifecycleStatus.Rejected => "REJECTED",
            EventLifecycleStatus.Published => "PUBLISHED",
            EventLifecycleStatus.Cancelled => "ARCHIVED",
            EventLifecycleStatus.Archived => "ARCHIVED",
            _ => "DRAFT",
        };

    /// <summary>
    /// Produce a single display string from the canonical address record.
    /// Only the display-safe fields (line1, city, state) are included.
    /// RawAddress and provenance metadata are omitted.
    /// </summary>
    public static string FormatAddress(EventAddress address)
    {
        var parts = new[] { address.AddressLine1, address.City, address.State }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    private static EventMergeLineageSummaryDto? ToMergeLineageSummary(EventMergeLineage lineage)
    {
        if (lineage.ParentCanonicalEventId is null && lineage.MergedCanonicalEventIds.Length == 0)
            return null;

        return new EventMergeLineageSummaryDto(
            ParentCanonicalEventId: lineage.ParentCanonicalEventId,
            MergedEventCount: lineage.MergedCanonicalEventIds.Length,
            LastMergedAtUtc: lineage.LastMergedAtUtc);
    }

    private static PublishBlockerSummaryDto ToBlockerSummary(PublishBlocker blocker)
        => new(
            Code: blocker.Code.ToString(),
            Message: blocker.Message,
            IsHardBlock: blocker.IsHardBlock,
            Field: blocker.Field);

    private static EligibilityConfidenceSummaryDto ToEligibilityConfidenceSummary(
        ConfidenceGateDecision decision)
        => new(
            Aggregate: decision.Aggregate,
            Band: decision.MeetsAutoPublishThreshold
                ? "High"
                : decision.RequiresManualReview
                    ? "Medium"
                    : "Low",
            Extraction: decision.DimensionScores.GetValueOrDefault("extraction"),
            Geocode: decision.DimensionScores.GetValueOrDefault("geocode"),
            Temporal: decision.DimensionScores.GetValueOrDefault("temporal"),
            VenueMatch: decision.DimensionScores.GetValueOrDefault("venueMatch"),
            DupeRisk: decision.DimensionScores.GetValueOrDefault("dupeRisk"),
            MeetsAutoPublishThreshold: decision.MeetsAutoPublishThreshold,
            RequiresManualReview: decision.RequiresManualReview);

    private static EligibilityFieldCompletenessSummaryDto ToFieldCompletenessSummary(
        FieldCompletenessResult completeness)
        => new(
            IsComplete: completeness.IsComplete,
            MissingRequiredFields: completeness.MissingRequiredFields,
            CompletenessRatio: completeness.CompletenessRatio);
}
