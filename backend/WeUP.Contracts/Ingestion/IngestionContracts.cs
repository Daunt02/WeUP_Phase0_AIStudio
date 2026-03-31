namespace WeUP.Contracts.Ingestion;

// ---------------------------------------------------------------------------
// Source kinds
// ---------------------------------------------------------------------------

public enum IngestionSourceKind
{
    ManualSubmission,
    PastedUrl,
    VenuePage,
}

// ---------------------------------------------------------------------------
// Request types per source
// ---------------------------------------------------------------------------

public record ManualIngestionRequest(
    string Title,
    string VenueName,
    string Address,
    string StartDate,      // ISO 8601
    string? EndDate,
    string Timezone,
    string Category,
    string? Description,
    string[]? Tags,
    string SubmitterId);

public record LinkIngestionRequest(
    string Url,
    string SubmitterId);

public record VenuePageIngestionRequest(
    string VenueId,
    string PageUrl,
    string RequestedBy);

// ---------------------------------------------------------------------------
// Ingestion job lifecycle
// ---------------------------------------------------------------------------

public enum IngestionJobStatus
{
    Queued,
    Fetching,
    Extracting,
    Normalizing,
    ReviewPending,
    Completed,
    Failed,
}

public record IngestionJobResponse(
    string JobId,
    IngestionJobStatus Status,
    string? CandidateEventId,
    string? FailureReason);

// ---------------------------------------------------------------------------
// Normalized candidate (output of all adapters)
// ---------------------------------------------------------------------------

public record NormalizedEventCandidate(
    string? Title,
    string? VenueName,
    string? Address,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string SourceKind,
    string SourceRef,
    double ExtractionConfidence,
    double GeocodeConfidence,
    double TemporalConfidence,
    string[]? EvidenceRefs);
