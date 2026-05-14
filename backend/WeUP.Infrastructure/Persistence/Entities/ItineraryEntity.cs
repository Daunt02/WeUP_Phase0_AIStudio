namespace WeUP.Infrastructure.Persistence.Entities;

public sealed class ItineraryEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int Position { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }
}
