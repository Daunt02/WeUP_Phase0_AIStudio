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

public record SaveEventResponse(
    string EventId,
    bool Saved,
    string Message);
