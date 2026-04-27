using System.Linq;
using WeUP.Contracts.Events;
using WeUP.Domain.Moderation;

namespace WeUP.Domain.Events;

/// <summary>
/// Canonical lifecycle status for EventAggregate.
/// </summary>
public enum EventLifecycleStatus
{
    Draft = 0,
    Candidate = 1,
    Reviewed = 2,
    Approved = 3,
    Rejected = 4,
    Published = 5,
    Cancelled = 6,
    Archived = 7,
}

/// <summary>
/// Publishability state. This is separate from lifecycle to keep publication
/// gating explicit and auditable.
/// </summary>
public enum EventPublishStatus
{
    NotEligible = 0,
    EligibilityPending = 1,
    Eligible = 2,
    Published = 3,
    Unpublished = 4,
    Archived = 5,
}

/// <summary>
/// Moderation review state for the canonical event.
/// </summary>
public enum EventModerationStatus
{
    Unreviewed = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>
/// Normalized address and location fields owned by the canonical aggregate.
/// Spatial taxonomy linkage rules:
/// - MarketCode is canonical and required once geospatial resolution completes.
/// - DistrictCode and NeighborhoodCode are canonical taxonomy IDs/slugs, not freeform labels.
/// - Display labels are projection concerns and must not be persisted as identity keys.
/// </summary>
public sealed record EventAddress(
    string AddressLine1,
    string City,
    string? State,
    string? PostalCode,
    string Country,
    string RawAddress,
    string? MarketCode = null,
    string? DistrictCode = null,
    string? NeighborhoodCode = null);

/// <summary>
/// External reference used to link canonical events back to ingestion systems,
/// partner systems, or manual submission channels.
/// </summary>
public sealed record ExternalEventReference(
    string ReferenceType,
    string SourceSystem,
    string ReferenceId,
    string? Url = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

/// <summary>
/// Provenance metadata attached to the canonical event for auditability.
/// </summary>
public sealed record EventProvenanceMetadata(
    string PrimarySourceKind,
    string PrimarySourceRef,
    string[] EvidenceRefs,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    string[] SourceRefs,
    IReadOnlyDictionary<string, string?>? Metadata = null);

/// <summary>
/// Structured source reference for a merge operation.
/// </summary>
public sealed record EventMergeSourceReference(
    string SourceRef,
    string SourceKind,
    string Origin,
    double? Confidence = null,
    string? Rationale = null,
    string? ExternalSourceId = null);

/// <summary>
/// Immutable merge entry appended to canonical merge lineage history.
/// </summary>
public sealed record EventMergeRecord(
    string MergeId,
    DateTimeOffset MergedAtUtc,
    string MergeReason,
    string MergeActor,
    string MergeOrigin,
    double? MergeConfidence,
    string[] CandidateIds,
    EventMergeSourceReference[] SourceReferences,
    string[] MergeRationale,
    bool RequiresManualReview);

/// <summary>
/// Merge lineage metadata for deduplication and merge history chaining.
/// </summary>
public sealed record EventMergeLineage(
    string? ParentCanonicalEventId,
    string[] MergedCanonicalEventIds,
    string[] AppliedMergePlanIds,
    DateTimeOffset? LastMergedAtUtc,
    string? LastMergedBy,
    string[]? MergedSourceRefs = null,
    EventMergeRecord[]? MergeRecords = null)
{
    public string[] EffectiveMergedSourceRefs => MergedSourceRefs ?? [];

    public EventMergeRecord[] EffectiveMergeRecords => MergeRecords ?? [];
}

/// <summary>
/// Canonical classification of a state evolution event for frontend and audit consumers.
/// </summary>
public enum EventVersionChangeType
{
    MinorMetadataUpdate = 0,
    MaterialEventChange = 1,
    StatusTransition = 2,
    MergeLineageUpdate = 3,
}

/// <summary>
/// Domain-level reason code for deterministic version increment semantics.
/// </summary>
public enum EventVersionReason
{
    MinorMetadataUpdate = 0,
    ContentEdit = 1,
    TimeEdit = 2,
    VenueLocationEdit = 3,
    ModerationDecision = 4,
    PublishStatusChange = 5,
    MergeApplied = 6,
    Cancelled = 7,
    Rescheduled = 8,
}

/// <summary>
/// Structured evolution classification for canonical event history.
/// </summary>
public enum EventEvolutionType
{
    TitleUpdate = 0,
    VenueCorrection = 1,
    TimeCorrection = 2,
    Reschedule = 3,
    Cancellation = 4,
    ModerationOverride = 5,
    Publish = 6,
    Unpublish = 7,
    MergeApplied = 8,
    DemergeReview = 9,
    Other = 10,
}

/// <summary>
/// Minimal snapshot of canonical state before/after each versioned change.
/// </summary>
public sealed record EventStateSnapshot(
    string Title,
    string? Description,
    string[] Tags,
    string Category,
    string VenueName,
    EventAddress Address,
    double Latitude,
    double Longitude,
    string TimeZone,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    EventLifecycleStatus EventStatus,
    EventPublishStatus PublishStatus,
    EventModerationStatus ModerationStatus,
    EventRiskLevel RiskLevel,
    double ConfidenceScore,
    EventMergeLineage MergeLineage,
    EventProvenanceMetadata Provenance);

/// <summary>
/// Immutable audit entry representing one canonical version step.
/// </summary>
public sealed record EventStateChangeEntry(
    int FromVersion,
    int ToVersion,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy,
    EventVersionReason Reason,
    EventVersionChangeType ChangeType,
    string[] ChangedFields,
    bool RequiresModerationReview,
    EventStateSnapshot Before,
    EventStateSnapshot After,
    string? Notes = null,
    EventEvolutionType EvolutionType = EventEvolutionType.Other);

/// <summary>
/// Audited mutation request. All update paths must use this contract.
/// </summary>
public sealed record EventAggregateUpdateRequest(
    int ExpectedVersion,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy,
    EventVersionReason Reason,
    string[] ChangedFields,
    bool RequiresModerationReview = false,
    string? Notes = null,
    string? Title = null,
    string? Description = null,
    string[]? Tags = null,
    string? Category = null,
    string? VenueName = null,
    EventAddress? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    string? TimeZone = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? LocalStartDisplay = null,
    string? LocalEndDisplay = null,
    EventLifecycleStatus? EventStatus = null,
    EventPublishStatus? PublishStatus = null,
    EventModerationStatus? ModerationStatus = null,
    EventRiskLevel? RiskLevel = null,
    double? ConfidenceScore = null,
    EventProvenanceMetadata? Provenance = null,
    EventMergeLineage? MergeLineage = null);

/// <summary>
/// Canonical event aggregate (v1.0): the authoritative backend source of truth
/// for event identity, lifecycle, moderation state, temporal state, spatial
/// state, provenance, and publish eligibility.
///
/// UI convenience fields:
/// - LocalStartDisplay
/// - LocalEndDisplay
/// These are rendering helpers only. Domain logic must use StartUtc/EndUtc.
/// </summary>
public sealed record EventAggregate(
    string CanonicalEventId,
    string[] SourceEventIds,
    ExternalEventReference[] ExternalReferences,
    string Title,
    string? Description,
    string[] Tags,
    string Category,
    string VenueName,
    EventAddress Address,
    double Latitude,
    double Longitude,
    string TimeZone,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string? LocalStartDisplay,
    string? LocalEndDisplay,
    EventLifecycleStatus EventStatus,
    EventPublishStatus PublishStatus,
    EventModerationStatus ModerationStatus,
    EventRiskLevel RiskLevel,
    double ConfidenceScore,
    EventProvenanceMetadata Provenance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version,
    EventMergeLineage MergeLineage,
    string? ConcurrencyToken = null,
    EventStateChangeEntry[]? ChangeHistory = null)
{
    private const int MaxChangeHistoryEntries = 100;

    private static readonly IReadOnlyDictionary<EventLifecycleStatus, EventLifecycleStatus[]> AllowedTransitions =
        new Dictionary<EventLifecycleStatus, EventLifecycleStatus[]>
        {
            [EventLifecycleStatus.Draft] = [EventLifecycleStatus.Candidate, EventLifecycleStatus.Archived],
            [EventLifecycleStatus.Candidate] = [EventLifecycleStatus.Reviewed, EventLifecycleStatus.Rejected, EventLifecycleStatus.Cancelled],
            [EventLifecycleStatus.Reviewed] = [EventLifecycleStatus.Approved, EventLifecycleStatus.Rejected, EventLifecycleStatus.Candidate],
            [EventLifecycleStatus.Approved] = [EventLifecycleStatus.Published, EventLifecycleStatus.Cancelled, EventLifecycleStatus.Archived],
            [EventLifecycleStatus.Rejected] = [EventLifecycleStatus.Candidate, EventLifecycleStatus.Archived],
            [EventLifecycleStatus.Published] = [EventLifecycleStatus.Cancelled, EventLifecycleStatus.Archived],
            [EventLifecycleStatus.Cancelled] = [EventLifecycleStatus.Published, EventLifecycleStatus.Archived],
            [EventLifecycleStatus.Archived] = [],
        };

    public string EffectiveConcurrencyToken =>
        string.IsNullOrWhiteSpace(ConcurrencyToken) ? $"{CanonicalEventId}:v{Version}" : ConcurrencyToken;

    public EventStateChangeEntry[] EffectiveChangeHistory => ChangeHistory ?? [];

    public EventStateChangeEntry? LatestChange => EffectiveChangeHistory.Length == 0 ? null : EffectiveChangeHistory[^1];

    /// <summary>
    /// Validate aggregate invariants for MVP/Phase 0 canonical contract.
    /// Throws InvalidOperationException on first failure — use
    /// EventAggregateSchemaValidator.ValidateAggregate() for a full violation list.
    /// </summary>
    public void Validate()
    {
        // Identity
        if (string.IsNullOrWhiteSpace(CanonicalEventId))
            throw new InvalidOperationException("CanonicalEventId is required.");
        if (SourceEventIds is null)
            throw new InvalidOperationException("SourceEventIds must be non-null (empty array is allowed).");

        // Content
        if (string.IsNullOrWhiteSpace(Title))
            throw new InvalidOperationException("Title is required.");
        if (string.IsNullOrWhiteSpace(VenueName))
            throw new InvalidOperationException("VenueName is required.");
        if (string.IsNullOrWhiteSpace(Category))
            throw new InvalidOperationException("Category is required.");
        if (Tags is null)
            throw new InvalidOperationException("Tags must be non-null (empty array is allowed).");

        // Temporal
        if (string.IsNullOrWhiteSpace(TimeZone))
            throw new InvalidOperationException("TimeZone is required.");
        if (StartUtc == default)
            throw new InvalidOperationException("StartUtc must not be DateTimeOffset.MinValue.");
        if (StartUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("StartUtc must be stored in canonical UTC (offset +00:00).");
        if (EndUtc.HasValue && EndUtc.Value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("EndUtc must be stored in canonical UTC (offset +00:00) when present.");
        if (EndUtc.HasValue && EndUtc.Value < StartUtc)
            throw new InvalidOperationException("EndUtc cannot be earlier than StartUtc.");

        // Address
        if (Address is null)
            throw new InvalidOperationException("Address is required.");
        if (string.IsNullOrWhiteSpace(Address.AddressLine1))
            throw new InvalidOperationException("Address.AddressLine1 is required.");
        if (string.IsNullOrWhiteSpace(Address.City))
            throw new InvalidOperationException("Address.City is required.");
        if (string.IsNullOrWhiteSpace(Address.Country))
            throw new InvalidOperationException("Address.Country is required.");
        if (string.IsNullOrWhiteSpace(Address.RawAddress))
            throw new InvalidOperationException("Address.RawAddress is required.");

        // Geo
        var geoValidation = GeoValidationRules.ValidatePoint(Latitude, Longitude, address: Address.RawAddress);
        var geoIssue = geoValidation.Issues.FirstOrDefault(issue => issue.Code != "MISSING_ADDRESS");
        if (geoIssue is not null)
            throw new InvalidOperationException(geoIssue.Message);

        // Confidence
        if (double.IsNaN(ConfidenceScore) || ConfidenceScore < 0 || ConfidenceScore > 1)
            throw new InvalidOperationException("ConfidenceScore must be within [0, 1].");

        // Version / concurrency
        if (Version <= 0)
            throw new InvalidOperationException("Version must be >= 1.");
        if (string.IsNullOrWhiteSpace(EffectiveConcurrencyToken))
            throw new InvalidOperationException("ConcurrencyToken is required.");

        // Provenance integrity
        if (Provenance is null)
            throw new InvalidOperationException("Provenance metadata is required.");
        if (string.IsNullOrWhiteSpace(Provenance.PrimarySourceKind))
            throw new InvalidOperationException("Provenance.PrimarySourceKind is required.");
        if (string.IsNullOrWhiteSpace(Provenance.PrimarySourceRef))
            throw new InvalidOperationException("Provenance.PrimarySourceRef is required.");
        if (Provenance.EvidenceRefs is null)
            throw new InvalidOperationException("Provenance.EvidenceRefs must be non-null (empty array is allowed).");

        // Merge lineage
        if (MergeLineage is null)
            throw new InvalidOperationException("MergeLineage is required.");

        foreach (var mergeRecord in MergeLineage.EffectiveMergeRecords)
        {
            if (string.IsNullOrWhiteSpace(mergeRecord.MergeId))
                throw new InvalidOperationException("MergeLineage.MergeRecords[].MergeId is required.");
            if (string.IsNullOrWhiteSpace(mergeRecord.MergeReason))
                throw new InvalidOperationException("MergeLineage.MergeRecords[].MergeReason is required.");
            if (string.IsNullOrWhiteSpace(mergeRecord.MergeActor))
                throw new InvalidOperationException("MergeLineage.MergeRecords[].MergeActor is required.");
            if (string.IsNullOrWhiteSpace(mergeRecord.MergeOrigin))
                throw new InvalidOperationException("MergeLineage.MergeRecords[].MergeOrigin is required.");
            foreach (var sourceReference in mergeRecord.SourceReferences ?? [])
            {
                if (string.IsNullOrWhiteSpace(sourceReference.SourceRef))
                    throw new InvalidOperationException("MergeLineage.MergeRecords[].SourceReferences[].SourceRef is required.");
                if (string.IsNullOrWhiteSpace(sourceReference.SourceKind))
                    throw new InvalidOperationException("MergeLineage.MergeRecords[].SourceReferences[].SourceKind is required.");
                if (string.IsNullOrWhiteSpace(sourceReference.Origin))
                    throw new InvalidOperationException("MergeLineage.MergeRecords[].SourceReferences[].Origin is required.");
            }
        }

        ValidateStatusConsistency();
    }

    /// <summary>
    /// Guard for optimistic concurrency.
    /// </summary>
    public void EnsureExpectedVersion(int expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new InvalidOperationException(
                $"Version mismatch for {CanonicalEventId}. Expected={expectedVersion}, Actual={Version}.");
        }
    }

    /// <summary>
    /// Apply an auditable update request with deterministic version increment semantics.
    /// </summary>
    public EventAggregate ApplyUpdate(EventAggregateUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureExpectedVersion(request.ExpectedVersion);

        if (string.IsNullOrWhiteSpace(request.ChangedBy))
            throw new InvalidOperationException("ChangedBy is required.");

        if (request.ChangedFields is null || request.ChangedFields.Length == 0)
            throw new InvalidOperationException("ChangedFields must contain at least one field name.");

        var before = CaptureSnapshot();

        var next = this with
        {
            Title = request.Title ?? Title,
            Description = request.Description ?? Description,
            Tags = request.Tags ?? Tags,
            Category = request.Category ?? Category,
            VenueName = request.VenueName ?? VenueName,
            Address = request.Address ?? Address,
            Latitude = request.Latitude ?? Latitude,
            Longitude = request.Longitude ?? Longitude,
            TimeZone = request.TimeZone ?? TimeZone,
            StartUtc = request.StartUtc?.ToUniversalTime() ?? StartUtc,
            EndUtc = request.EndUtc?.ToUniversalTime() ?? EndUtc,
            LocalStartDisplay = request.LocalStartDisplay ?? LocalStartDisplay,
            LocalEndDisplay = request.LocalEndDisplay ?? LocalEndDisplay,
            EventStatus = request.EventStatus ?? EventStatus,
            PublishStatus = request.PublishStatus ?? PublishStatus,
            ModerationStatus = request.ModerationStatus ?? ModerationStatus,
            RiskLevel = request.RiskLevel ?? RiskLevel,
            ConfidenceScore = request.ConfidenceScore ?? ConfidenceScore,
            Provenance = request.Provenance ?? Provenance,
            MergeLineage = request.MergeLineage ?? MergeLineage,
        };

        if (request.Reason == EventVersionReason.Cancelled)
        {
            next = next with
            {
                EventStatus = EventLifecycleStatus.Cancelled,
                PublishStatus = EventPublishStatus.Unpublished,
            };
        }

        if (request.Reason == EventVersionReason.Rescheduled)
        {
            var startChanged = request.StartUtc.HasValue && request.StartUtc.Value != StartUtc;
            var endChanged = request.EndUtc != EndUtc;
            if (!startChanged && !endChanged)
            {
                throw new InvalidOperationException("Rescheduled updates require StartUtc or EndUtc change.");
            }
        }

        if (request.RequiresModerationReview)
        {
            next = next with
            {
                ModerationStatus = EventModerationStatus.InReview,
                EventStatus = next.EventStatus == EventLifecycleStatus.Published
                    ? EventLifecycleStatus.Reviewed
                    : next.EventStatus,
                PublishStatus = next.PublishStatus == EventPublishStatus.Published
                    ? EventPublishStatus.Unpublished
                    : next.PublishStatus,
            };
        }

        if (next.CanonicalEventId != CanonicalEventId)
            throw new InvalidOperationException("CanonicalEventId is immutable.");

        if (next.CreatedAtUtc != CreatedAtUtc)
            throw new InvalidOperationException("CreatedAtUtc is immutable.");

        var after = next.CaptureSnapshot();
        if (SnapshotsEqual(before, after))
            return this;

        var changeType = ClassifyChangeType(request.Reason);
        var evolutionType = ClassifyEvolutionType(request, before, after);
        var nextVersion = Version + 1;
        var history = AppendHistory(new EventStateChangeEntry(
            FromVersion: Version,
            ToVersion: nextVersion,
            ChangedAtUtc: request.ChangedAtUtc,
            ChangedBy: request.ChangedBy,
            Reason: request.Reason,
            ChangeType: changeType,
            ChangedFields: request.ChangedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RequiresModerationReview: request.RequiresModerationReview,
            Before: before,
            After: after,
            Notes: request.Notes,
            EvolutionType: evolutionType));

        var updated = next with
        {
            UpdatedAtUtc = request.ChangedAtUtc,
            Version = nextVersion,
            ConcurrencyToken = GenerateConcurrencyToken(nextVersion, request.ChangedAtUtc),
            ChangeHistory = history,
        };

        updated.Validate();
        return updated;
    }

    /// <summary>
    /// Validate lifecycle status transition.
    /// </summary>
    public void EnsureCanTransitionTo(EventLifecycleStatus nextStatus)
    {
        if (EventStatus == nextStatus)
            return;

        if (!AllowedTransitions.TryGetValue(EventStatus, out var allowed) || !allowed.Contains(nextStatus))
        {
            throw new InvalidOperationException(
                $"Invalid lifecycle transition: {EventStatus} -> {nextStatus}.");
        }
    }

    /// <summary>
    /// Apply a validated lifecycle transition and bump aggregate version.
    /// </summary>
    public EventAggregate TransitionTo(EventLifecycleStatus nextStatus, DateTimeOffset transitionAtUtc)
        => TransitionTo(nextStatus, transitionAtUtc, Version, "system");

    /// <summary>
    /// Apply a validated lifecycle transition with optimistic concurrency and audit entry.
    /// </summary>
    public EventAggregate TransitionTo(
        EventLifecycleStatus nextStatus,
        DateTimeOffset transitionAtUtc,
        int expectedVersion,
        string changedBy)
    {
        EnsureCanTransitionTo(nextStatus);

        var nextPublishStatus = nextStatus switch
        {
            EventLifecycleStatus.Published => EventPublishStatus.Published,
            EventLifecycleStatus.Archived => EventPublishStatus.Archived,
            EventLifecycleStatus.Rejected => EventPublishStatus.NotEligible,
            EventLifecycleStatus.Cancelled => EventPublishStatus.Unpublished,
            EventLifecycleStatus.Approved => PublishStatus is EventPublishStatus.Published ? EventPublishStatus.Published : EventPublishStatus.Eligible,
            _ => PublishStatus,
        };

        var nextModerationStatus = nextStatus switch
        {
            EventLifecycleStatus.Reviewed => EventModerationStatus.InReview,
            EventLifecycleStatus.Approved => EventModerationStatus.Approved,
            EventLifecycleStatus.Rejected => EventModerationStatus.Rejected,
            _ => ModerationStatus,
        };

        return ApplyUpdate(new EventAggregateUpdateRequest(
            ExpectedVersion: expectedVersion,
            ChangedAtUtc: transitionAtUtc,
            ChangedBy: changedBy,
            Reason: EventVersionReason.ModerationDecision,
            ChangedFields: [nameof(EventStatus), nameof(PublishStatus), nameof(ModerationStatus)],
            EventStatus: nextStatus,
            PublishStatus: nextPublishStatus,
            ModerationStatus: nextModerationStatus));
    }

    private void ValidateStatusConsistency()
    {
        if (EventStatus == EventLifecycleStatus.Published && PublishStatus != EventPublishStatus.Published)
            throw new InvalidOperationException("Published events must have PublishStatus=Published.");

        if (PublishStatus == EventPublishStatus.Published && EventStatus != EventLifecycleStatus.Published)
            throw new InvalidOperationException("PublishStatus=Published requires EventStatus=Published.");

        if (EventStatus == EventLifecycleStatus.Rejected && ModerationStatus != EventModerationStatus.Rejected)
            throw new InvalidOperationException("Rejected events must have ModerationStatus=Rejected.");

        if (ModerationStatus == EventModerationStatus.Rejected && EventStatus == EventLifecycleStatus.Published)
            throw new InvalidOperationException("Rejected events cannot be Published.");
    }

    private EventStateSnapshot CaptureSnapshot()
        => new(
            Title,
            Description,
            Tags,
            Category,
            VenueName,
            Address,
            Latitude,
            Longitude,
            TimeZone,
            StartUtc,
            EndUtc,
            EventStatus,
            PublishStatus,
            ModerationStatus,
            RiskLevel,
            ConfidenceScore,
            MergeLineage,
            Provenance);

    private static bool SnapshotsEqual(EventStateSnapshot left, EventStateSnapshot right)
    {
        return left.Title == right.Title &&
               left.Description == right.Description &&
               left.Category == right.Category &&
               left.VenueName == right.VenueName &&
               left.Address == right.Address &&
               left.Latitude.Equals(right.Latitude) &&
               left.Longitude.Equals(right.Longitude) &&
               left.TimeZone == right.TimeZone &&
               left.StartUtc == right.StartUtc &&
               left.EndUtc == right.EndUtc &&
               left.EventStatus == right.EventStatus &&
               left.PublishStatus == right.PublishStatus &&
               left.ModerationStatus == right.ModerationStatus &&
               left.RiskLevel == right.RiskLevel &&
               left.ConfidenceScore.Equals(right.ConfidenceScore) &&
               left.MergeLineage == right.MergeLineage &&
               left.Provenance == right.Provenance &&
               left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal);
    }

    private EventStateChangeEntry[] AppendHistory(EventStateChangeEntry entry)
    {
        var combined = EffectiveChangeHistory
            .Concat([entry])
            .OrderBy(change => change.ToVersion)
            .ToArray();

        if (combined.Length <= MaxChangeHistoryEntries)
            return combined;

        return combined[^MaxChangeHistoryEntries..];
    }

    private string GenerateConcurrencyToken(int nextVersion, DateTimeOffset changedAtUtc)
        => $"{CanonicalEventId}:v{nextVersion}:{changedAtUtc.ToUnixTimeMilliseconds()}";

    private static EventVersionChangeType ClassifyChangeType(EventVersionReason reason)
        => reason switch
        {
            EventVersionReason.MinorMetadataUpdate => EventVersionChangeType.MinorMetadataUpdate,
            EventVersionReason.ModerationDecision => EventVersionChangeType.StatusTransition,
            EventVersionReason.PublishStatusChange => EventVersionChangeType.StatusTransition,
            EventVersionReason.MergeApplied => EventVersionChangeType.MergeLineageUpdate,
            _ => EventVersionChangeType.MaterialEventChange,
        };

    private static EventEvolutionType ClassifyEvolutionType(
        EventAggregateUpdateRequest request,
        EventStateSnapshot before,
        EventStateSnapshot after)
    {
        switch (request.Reason)
        {
            case EventVersionReason.MergeApplied:
                return EventEvolutionType.MergeApplied;
            case EventVersionReason.Rescheduled:
                return EventEvolutionType.Reschedule;
            case EventVersionReason.Cancelled:
                return EventEvolutionType.Cancellation;
            case EventVersionReason.ModerationDecision:
                return EventEvolutionType.ModerationOverride;
            case EventVersionReason.PublishStatusChange:
                return after.PublishStatus == EventPublishStatus.Published
                    ? EventEvolutionType.Publish
                    : EventEvolutionType.Unpublish;
            case EventVersionReason.TimeEdit:
                return EventEvolutionType.TimeCorrection;
            case EventVersionReason.VenueLocationEdit:
                return EventEvolutionType.VenueCorrection;
            case EventVersionReason.ContentEdit:
                if (ChangedField(request, nameof(EventAggregate.Title)) || before.Title != after.Title)
                    return EventEvolutionType.TitleUpdate;
                if (ChangedField(request, nameof(EventAggregate.VenueName)) || ChangedField(request, nameof(EventAggregate.Address)))
                    return EventEvolutionType.VenueCorrection;
                if (ChangedField(request, nameof(EventAggregate.StartUtc)) || ChangedField(request, nameof(EventAggregate.EndUtc)) || before.StartUtc != after.StartUtc || before.EndUtc != after.EndUtc)
                    return EventEvolutionType.TimeCorrection;
                return EventEvolutionType.Other;
            default:
                return EventEvolutionType.Other;
        }
    }

    private static bool ChangedField(EventAggregateUpdateRequest request, string fieldName)
        => request.ChangedFields.Any(
            changedField => changedField.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// String/status mapping helpers for legacy storage fields and contracts.
/// </summary>
public static class EventLifecycleStatusMapper
{
    public static EventLifecycleStatus FromStorage(string? status)
        => (status ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "DRAFT" => EventLifecycleStatus.Draft,
            "INGESTED" => EventLifecycleStatus.Candidate,
            "NEEDS_REVIEW" => EventLifecycleStatus.Candidate,
            "REVIEWED" => EventLifecycleStatus.Reviewed,
            "APPROVED" => EventLifecycleStatus.Approved,
            "REJECTED" => EventLifecycleStatus.Rejected,
            "PUBLISHED" => EventLifecycleStatus.Published,
            "CANCELLED" => EventLifecycleStatus.Cancelled,
            "ARCHIVED" => EventLifecycleStatus.Archived,
            _ => EventLifecycleStatus.Candidate,
        };

    public static string ToStorage(EventLifecycleStatus status)
        => status switch
        {
            EventLifecycleStatus.Draft => "DRAFT",
            EventLifecycleStatus.Candidate => "NEEDS_REVIEW",
            EventLifecycleStatus.Reviewed => "REVIEWED",
            EventLifecycleStatus.Approved => "APPROVED",
            EventLifecycleStatus.Rejected => "REJECTED",
            EventLifecycleStatus.Published => "PUBLISHED",
            EventLifecycleStatus.Cancelled => "CANCELLED",
            EventLifecycleStatus.Archived => "ARCHIVED",
            _ => "NEEDS_REVIEW",
        };
}