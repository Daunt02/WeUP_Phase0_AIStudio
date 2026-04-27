using WeUP.Contracts.Events;

namespace WeUP.Contracts.Saves;

public record SavedEventDto(
    string EventId,
    DateTimeOffset SavedAt,
    string ResolutionStatus,
    string? ResolutionMessage,
    EventMapItemDto? CanonicalEvent);

public record SavedEventsResponse(
    SavedEventDto[] Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage,
    int ResolvedCount,
    int MissingOrDeletedCount,
    DateTimeOffset RetrievedAtUtc,
    string SourceProjection = "canonical-event-map-v1");

public record SaveEventRequestDto(
    string EventId);

public record UnsaveEventRequestDto(
    string EventId);

public record SaveEventResponseDto(
    string EventId,
    bool Saved,
    string Message);

public record SavedStateDto(
    string EventId,
    bool Saved,
    string SessionKind,
    string PersistenceSource);

/// <summary>
/// One-way migration request from anonymous local save state into authenticated ownership.
/// </summary>
public record SaveStateMigrationRequestDto(
    string[] LocalSavedEventIds,
    SaveStateLocalDiscoveryContextDto? LocalDiscoveryContext,
    string? ClientMigrationKey);

/// <summary>
/// Lightweight anonymous discovery context snapshot.
/// This is advisory-only and must not override authenticated truth unexpectedly.
/// </summary>
public record SaveStateLocalDiscoveryContextDto(
    string? LastViewedDistrict,
    SaveStateLocalTemporalFilterDto? LastTemporalFilter,
    SaveStateLocalMapViewportDto? RecentMapViewport,
    string? UpdatedAtUtc);

public record SaveStateLocalTemporalFilterDto(
    string? Preset,
    string? Timezone,
    string? CustomStartUtc,
    string? CustomEndUtc);

public record SaveStateLocalMapViewportDto(
    string? Bbox,
    string? CapturedAtUtc);

/// <summary>
/// Count model for explicit migration auditing.
/// </summary>
public record SaveStateMigrationCountsDto(
    int ReceivedLocalSavedCount,
    int DistinctLocalSavedCount,
    int DuplicateCollapsedCount,
    int MigratedCount,
    int AlreadySavedCount,
    int InvalidLocalIdCount,
    int MissingLocalIdCount);

/// <summary>
/// Deterministic per-event migration outcome.
/// </summary>
public record SaveStateMigrationItemResultDto(
    string EventId,
    string Outcome,
    string Message);

public record SaveStateDiscoveryContextMigrationResultDto(
    string Status,
    bool Applied,
    string Message,
    string? ResolvedPreferredTimezone);

/// <summary>
/// Migration result explicitly reports all outcomes to prevent silent partial state transfer.
/// </summary>
public record SaveStateMigrationResultDto(
    string Status,
    string MigrationDirection,
    string Ownership,
    string? ClientMigrationKey,
    DateTimeOffset ProcessedAtUtc,
    SaveStateMigrationCountsDto Counts,
    SaveStateMigrationItemResultDto[] ItemResults,
    string[] RetainedLocalSavedEventIds,
    SaveStateDiscoveryContextMigrationResultDto DiscoveryContext);

// Backward-compatible alias used by existing tests and callers during migration.
public record SaveEventResponse(
    string EventId,
    bool Saved,
    string Message);
