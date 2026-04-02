namespace WeUP.Contracts.Users;

// ---------------------------------------------------------------------------
// Itinerary — user's personally ordered event plan for a day
// ---------------------------------------------------------------------------

public sealed record ItineraryItemDto(
    string ItemId,
    string EventId,
    string? EventTitle,
    string? VenueName,
    DateTimeOffset? EventStartUtc,
    int Position,
    string? Note,
    DateTimeOffset AddedAt);

public sealed record ItineraryResponse(
    ItineraryItemDto[] Items,
    int TotalCount);

public sealed record AddToItineraryRequest(
    string EventId,
    string? Note,
    int? Position);

public sealed record UpdateItineraryItemRequest(
    string? Note,
    int? Position);

public sealed record ItineraryItemResponse(
    string ItemId,
    string EventId,
    bool Added,
    string Message);
