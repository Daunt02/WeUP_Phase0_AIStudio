namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Provenance record — traces every event back to at least one source.
/// One event may have multiple source records (e.g. flyer + pasted URL).
/// </summary>
public sealed class EventSourceEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    /// <summary>manual_submission | flyer_upload | pasted_url | scraped_venue_page | external_feed</summary>
    public string SourceKind { get; set; } = string.Empty;

    /// <summary>Submission ID, job ID, URL, or external feed event ID.</summary>
    public string SourceRef { get; set; } = string.Empty;

    public DateTimeOffset IngestedAt { get; set; }
    public string ExtractionVersion { get; set; } = "1.0.0";

    // Optional: submitter identity (null for automated sources)
    public string? SubmitterId { get; set; }

    // Navigation
    public EventEntity Event { get; set; } = null!;
}
