namespace WeUP.Contracts.Ingestion;

using System.Collections.Immutable;

/// <summary>
/// Supported ingestion input types for boundary normalization.
/// </summary>
public enum IngestionSourceType
{
    ImageUpload,
    ImageUrl,
    ManualEntry,
}

/// <summary>
/// External request contract accepted at the ingestion boundary.
/// Exactly one source-specific field must be populated based on SourceType.
/// </summary>
public sealed record IngestionRequest(
    IngestionSourceType SourceType,
    string SubmittedBy,
    DateTimeOffset ReceivedAtUtc,
    string RequestId,
    string? ImageBase64,
    string? ImageUrl,
    string? ManualEntryText,
    string? OriginalFileName,
    string? ContentType,
    ImmutableDictionary<string, string?> Metadata);

/// <summary>
/// Canonical payload emitted by source adapters.
/// Invariant: ContentSha256 is always computed from ContentBytes during adaptation.
/// </summary>
public sealed record RawIngestionPayload(
    string RequestId,
    IngestionSourceType SourceType,
    string SubmittedBy,
    DateTimeOffset IngestedAtUtc,
    ImmutableArray<byte> ContentBytes,
    string ContentSha256,
    string? SourceUrl,
    string? ContentType,
    string? OriginalFileName,
    string? ManualEntryText,
    ImmutableDictionary<string, string?> Metadata);
