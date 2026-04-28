using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace WeUP.Contracts.Ingestion;

/// <summary>
/// Normalized payload emitted by any concrete source adapter.
/// </summary>
public sealed record RawIngestionPayload(
    Guid RequestId,
    byte[] ImageBytes,
    string? SourceUrl,
    string ContentHash,
    DateTimeOffset ReceivedAtUtc)
{
    // Backward-compatible constructor used by existing orchestrator/adapters.
    public RawIngestionPayload(
        Guid RequestId,
        IngestionSourceType SourceType,
        string SubmittedBy,
        DateTimeOffset IngestedAtUtc,
        ImmutableArray<byte> ContentBytes,
        string ContentSha256,
        string? SourceUrl,
        string? ContentType,
        string? OriginalFileName,
        string? ManualEntryText,
        IReadOnlyDictionary<string, string?> Metadata)
        : this(RequestId, ContentBytes.ToArray(), SourceUrl, ContentSha256, IngestedAtUtc)
    {
        this.SourceType = SourceType;
        this.SubmittedBy = SubmittedBy;
        this.ContentType = ContentType;
        this.OriginalFileName = OriginalFileName;
        this.ManualEntryText = ManualEntryText;
        this.Metadata = Metadata;
    }

    public IngestionSourceType SourceType { get; init; }
    public string? SubmittedBy { get; init; }
    public string? ContentType { get; init; }
    public string? OriginalFileName { get; init; }
    public string? ManualEntryText { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>();

    public byte[] ContentBytes => ImageBytes;
    public string ContentSha256 => ContentHash;
    public DateTimeOffset IngestedAtUtc => ReceivedAtUtc;
}
