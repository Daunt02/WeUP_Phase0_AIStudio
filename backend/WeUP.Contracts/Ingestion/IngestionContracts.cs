namespace WeUP.Contracts.Ingestion;

using System.Text.Json.Serialization;

public enum IngestionSourceKind
{
    ManualSubmission,
    PastedUrl,
    VenuePage,
    FlyerUpload,
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

public sealed record FlyerUploadIngestionRequest(
    string AssetId,
    string SubmittedBy,
    string? SubmissionId = null,
    string? SourceUrl = null,
    string? PartnerProvider = null,
    string? SubmitterNote = null,
    bool Reprocess = false);

public sealed record FlyerAssetReference(
    string AssetId,
    string StorageKey,
    string ContentType,
    string? OriginalFilename,
    string? UploadedBy,
    DateTimeOffset UploadedAtUtc,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record FlyerOcrTextBlock(
    int Index,
    string Text,
    double Confidence,
    int X,
    int Y,
    int Width,
    int Height,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record FlyerOcrExtractionResult(
    string ExtractionId,
    string JobId,
    string AssetId,
    string Engine,
    string EngineVersion,
    double Confidence,
    bool Success,
    string RawText,
    FlyerOcrTextBlock[] Blocks,
    CanonicalIngestionIssue[] Issues,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record FlyerFieldValueCandidate(
    string Value,
    double Confidence,
    string[] EvidenceRefs,
    bool IsSelected,
    bool IsAmbiguous = false,
    string? NormalizedValue = null);

public sealed record FlyerExtractionWarning(
    string Code,
    string Message,
    string? Field,
    bool Blocking,
    IReadOnlyDictionary<string, string?> Metadata);

public enum FlyerReviewTriggerReason
{
    UnreadableFlyer,
    MissingTitle,
    MissingDate,
    MissingVenue,
    MissingAddress,
    AmbiguousVenue,
    ConflictingTimeData,
    PartialExtraction,
    NormalizationIncomplete,
    LowExtractionConfidence,
    LowTemporalConfidence,
    LowVenueMatchConfidence,
    LowGeocodeConfidence,
    DedupePending,
    UnresolvedAmbiguity,
}

public sealed record FlyerFieldConfidenceBreakdown(
    double Title,
    double Venue,
    double Address,
    double StartDateTime,
    double EndDateTime,
    double Category,
    double Description,
    double Tags);

public sealed record FlyerConfidenceVector(
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double Dedupe,
    double SourceTrust,
    double ReviewConfidence)
{
    public double Aggregate =>
        Extraction * 0.25 +
        Geocode * 0.20 +
        Temporal * 0.15 +
        VenueMatch * 0.10 +
        Dedupe * 0.15 +
        SourceTrust * 0.10 +
        ReviewConfidence * 0.05;
}

public sealed record FlyerNormalizedEventCandidate(
    CanonicalEventCandidate CanonicalCandidate,
    FlyerFieldValueCandidate[] TitleCandidates,
    FlyerFieldValueCandidate[] VenueCandidates,
    FlyerFieldValueCandidate[] StartDateTimeCandidates,
    FlyerFieldValueCandidate[] EndDateTimeCandidates,
    FlyerFieldValueCandidate[] AddressCandidates,
    FlyerFieldValueCandidate[] CategoryCandidates,
    string[] Tags,
    string[] DescriptiveNotes,
    string[] MissingFields,
    string[] UnresolvedAmbiguities,
    FlyerFieldConfidenceBreakdown FieldConfidence,
    FlyerExtractionWarning[] Warnings,
    FlyerReviewTriggerReason[] ReviewTriggers,
    string NormalizationRunId,
    string NormalizationVersion);

public sealed record FlyerIngestionEvidenceResponse(
    string JobId,
    FlyerAssetReference Asset,
    string ProvenanceId,
    string EvidenceId,
    string? OcrExtractionId,
    string? OcrEngineVersion,
    string? NormalizationRunId,
    string? NormalizationVersion,
    string? RawOcrTextSnapshot,
    string[] ProcessingHistory,
    string[] ValidationFailures,
    string[] ReviewReasons,
    CanonicalSourceEvidence[] Evidence);

public sealed record FlyerIngestionJobDetailResponse(
    string JobId,
    IngestionJobStatus Status,
    FlyerAssetReference Asset,
    FlyerOcrExtractionResult? Ocr,
    FlyerNormalizedEventCandidate? Candidate,
    FlyerConfidenceVector Confidence,
    bool RequiresManualReview,
    FlyerReviewTriggerReason[] ReviewReasons,
    CanonicalSourceEvidence[] Evidence,
    CanonicalIngestionIssue[] Issues,
    IngestionStatusRecord[] Lifecycle,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

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
