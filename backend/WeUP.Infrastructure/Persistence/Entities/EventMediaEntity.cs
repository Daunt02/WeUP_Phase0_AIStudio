namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Media asset reference attached to a canonical event.
/// Full asset management model added in Phase 0.15 (P25+).
/// </summary>
public sealed class EventMediaEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    /// <summary>Stable asset ID — matches MediaAsset.Id in Phase 0.15 media system.</summary>
    public string AssetId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>image | video | poster</summary>
    public string Kind { get; set; } = "image";
    public int? Width { get; set; }
    public int? Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public EventEntity Event { get; set; } = null!;
}
