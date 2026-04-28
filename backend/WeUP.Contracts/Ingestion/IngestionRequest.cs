using System;
using System.Collections.Generic;

namespace WeUP.Contracts.Ingestion;

/// <summary>
/// The public request object accepted by the ingestion API.
/// </summary>
public sealed record IngestionRequest(
    Guid RequestId,
    IngestionSourceType SourceType,
    string RawInput,
    string? SubmittedByUserId,
    DateTimeOffset SubmittedAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    // Backward-compatible aliases for existing ingestion orchestration code.
    public string SubmittedBy => SubmittedByUserId ?? "anonymous";
    public DateTimeOffset ReceivedAtUtc => SubmittedAtUtc;
    public string? ImageBase64 => SourceType == IngestionSourceType.ImageUpload ? RawInput : null;
    public string? ImageUrl => SourceType == IngestionSourceType.ImageUrl ? RawInput : null;
    public string? ManualEntryText => SourceType == IngestionSourceType.ManualEntry ? RawInput : null;

    public string? OriginalFileName =>
        Metadata is not null && Metadata.TryGetValue("originalFileName", out var value) ? value : null;

    public string? ContentType =>
        Metadata is not null && Metadata.TryGetValue("contentType", out var value) ? value : null;
}
