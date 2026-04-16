using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.SourceAdapters;

/// <summary>
/// Adapter for manually entered text payloads.
/// Boundary invariant: hash is derived from UTF-8 bytes of manualEntryText.
/// </summary>
public sealed class ManualEntryAdapter : ISourceAdapter
{
    public IngestionSourceType SourceType => IngestionSourceType.ManualEntry;

    public Task<RawIngestionPayload> AdaptAsync(IngestionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SourceAdapterGuards.EnsureCommonInvariants(request);

        if (request.SourceType != IngestionSourceType.ManualEntry)
        {
            throw new IngestionValidationException("ManualEntryAdapter cannot handle a different source type.");
        }

        SourceAdapterGuards.EnsureNoCrossSourceFields(request, nameof(IngestionRequest.ManualEntryText));

        if (string.IsNullOrWhiteSpace(request.ManualEntryText))
        {
            throw new IngestionValidationException("manualEntryText is required for ManualEntry source type.");
        }

        var contentBytes = SourceAdapterGuards.Utf8(request.ManualEntryText);
        var sha256 = SourceAdapterGuards.ComputeSha256(contentBytes);
        var metadata = SourceAdapterGuards.MetadataWithInvariant(
            request.Metadata,
            "adapter.sourceType",
            request.SourceType.ToString());

        var payload = new RawIngestionPayload(
            RequestId: request.RequestId,
            SourceType: request.SourceType,
            SubmittedBy: request.SubmittedBy,
            IngestedAtUtc: DateTimeOffset.UtcNow,
            ContentBytes: contentBytes,
            ContentSha256: sha256,
            SourceUrl: null,
            ContentType: request.ContentType,
            OriginalFileName: request.OriginalFileName,
            ManualEntryText: request.ManualEntryText,
            Metadata: metadata);

        return Task.FromResult(payload);
    }
}
