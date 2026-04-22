namespace WeUP.Contracts.Saves;

public record SavedEventDto(
    string EventId,
    string Title,
    string VenueName,
    DateTimeOffset StartUtc,
    string? ThumbnailUrl,
    DateTimeOffset SavedAt);

public record SavedEventsResponse(
    SavedEventDto[] Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage);

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

// Backward-compatible alias used by existing tests and callers during migration.
public record SaveEventResponse(
    string EventId,
    bool Saved,
    string Message);
