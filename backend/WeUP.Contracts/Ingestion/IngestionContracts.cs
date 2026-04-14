namespace WeUP.Contracts.Ingestion;

using System.Text.Json.Serialization;

public enum IngestionSourceKind
{
    ManualSubmission,
    PastedUrl,
    VenuePage,
    ExternalFeed,
}

public enum IngestionJobStatus
{
    RECEIVED,
    VALIDATING,
    NORMALIZING,
    CANDIDATE_CREATED,
    REQUIRES_REVIEW,
    FAILED,
    RETRYABLE_FAILURE,
}

public enum IngestionIssueSeverity
{
    Warning,
    Error,
}

public sealed record ManualIngestionRequest(
    string Title,
    string VenueName,
    string Address,
    string StartDate,
    string? EndDate,
    string Timezone,
    string Category,
    string? Description,
    string[]? Tags,
    string SubmitterId);

public record UrlIngestionRequest(
    string Url,
    string SubmitterId,
    string? SourceLabel = null);

public sealed record LinkIngestionRequest(
    string Url,
    string SubmitterId) : UrlIngestionRequest(Url, SubmitterId);

public sealed record VenuePageIngestionRequest(
    string VenueId,
    string PageUrl,
    string RequestedBy,
    string? VenueName = null);

public sealed record IngestionAcceptedResponse(
    string JobId,
    IngestionJobStatus Status,
    string StatusUrl);

public sealed record IngestionStatusRecord(
    IngestionJobStatus Status,
    DateTimeOffset TimestampUtc,
    string? Detail = null);

public sealed record IngestionRequestEnvelope(
    string RequestId,
    IngestionSourceKind SourceKind,
    string SourceReference,
    string SubmittedBy,
    string RawPayloadJson,
    string IdempotencyKey,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyDictionary<string, string?> Metadata);

public record CanonicalEventCandidate(
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
    string[]? EvidenceRefs,
    string? ExternalSourceId = null,
    IReadOnlyDictionary<string, string?>? Attributes = null);

public sealed record NormalizedEventCandidate(
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
    string[]? EvidenceRefs,
    string? ExternalSourceId = null,
    IReadOnlyDictionary<string, string?>? Attributes = null)
    : CanonicalEventCandidate(
        Title,
        VenueName,
        Address,
        StartUtc,
        EndUtc,
        Timezone,
        Category,
        Description,
        Tags,
        SourceKind,
        SourceRef,
        ExtractionConfidence,
        GeocodeConfidence,
        TemporalConfidence,
        EvidenceRefs,
        ExternalSourceId,
        Attributes);

public sealed record CanonicalSourceEvidence(
    string EvidenceId,
    string EvidenceKind,
    string Reference,
    string? MimeType,
    string? PayloadSnippet,
    DateTimeOffset ObservedAtUtc,
    double Confidence,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record CanonicalIngestionIssue(
    string Code,
    string Message,
    IngestionIssueSeverity Severity,
    bool IsRetryable,
    string? Field,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record AdapterCapabilityDescriptor(
    string AdapterKey,
    string DisplayName,
    string Version,
    IngestionSourceKind[] SupportedSourceKinds,
    bool IsRetrySafe,
    bool RequiresNetworkAccess,
    string[] ProducedEvidenceKinds,
    string[] Notes);

public sealed record AdapterExecutionMetadata(
    string AdapterKey,
    string AdapterVersion,
    bool IsRetrySafe,
    int AttemptCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMs,
    AdapterCapabilityDescriptor Capability);

public sealed record AdapterExecutionResult(
    CanonicalEventCandidate? Candidate,
    CanonicalSourceEvidence[] Evidence,
    CanonicalIngestionIssue[] Issues,
    AdapterExecutionMetadata Execution);

public sealed record IngestionResult(
    string JobId,
    IngestionRequestEnvelope Request,
    IngestionJobStatus Status,
    CanonicalEventCandidate? Candidate,
    CanonicalSourceEvidence[] Evidence,
    CanonicalIngestionIssue[] Issues,
    AdapterExecutionMetadata? Execution,
    IngestionStatusRecord[] Lifecycle,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    [JsonIgnore]
    public string? FailureReason => Issues.LastOrDefault(issue => issue.Severity == IngestionIssueSeverity.Error)?.Message;
}

public sealed record IngestionJobResponse(
    string JobId,
    IngestionJobStatus Status,
    string? CandidateEventId,
    string? FailureReason);
