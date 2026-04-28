using System.Collections.Immutable;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.SourceAdapters;

/// <summary>
/// Adapter for direct image uploads.
/// Boundary invariant: payload hash is computed immediately from decoded bytes.
/// </summary>
public sealed class ImageUploadAdapter : ISourceAdapter
{
    public IngestionSourceType SourceType => IngestionSourceType.ImageUpload;

    public Task<RawIngestionPayload> AdaptAsync(IngestionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SourceAdapterGuards.EnsureCommonInvariants(request);

        if (request.SourceType != IngestionSourceType.ImageUpload)
        {
            throw new IngestionValidationException("ImageUploadAdapter cannot handle a different source type.");
        }

        SourceAdapterGuards.EnsureNoCrossSourceFields(request, nameof(IngestionRequest.ImageBase64));

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            throw new IngestionValidationException("imageBase64 is required for ImageUpload source type.");
        }

        ImmutableArray<byte> imageBytes;
        try
        {
            imageBytes = ImmutableArray.Create(Convert.FromBase64String(request.ImageBase64));
        }
        catch (FormatException ex)
        {
            throw new IngestionValidationException($"imageBase64 is not valid Base64: {ex.Message}");
        }

        if (imageBytes.IsDefaultOrEmpty)
        {
            throw new IngestionValidationException("imageBase64 decoded to an empty payload.");
        }

        IReadOnlyDictionary<string, string?> metadata = request.Metadata is null
            ? new Dictionary<string, string?>()
            : request.Metadata.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);
        metadata = SourceAdapterGuards.MetadataWithInvariant(
            metadata,
            "adapter.sourceType",
            request.SourceType.ToString());

        var sha256 = SourceAdapterGuards.ComputeSha256(imageBytes);

        var payload = new RawIngestionPayload(
            RequestId: request.RequestId,
            SourceType: request.SourceType,
            SubmittedBy: request.SubmittedBy,
            IngestedAtUtc: DateTimeOffset.UtcNow,
            ContentBytes: imageBytes,
            ContentSha256: sha256,
            SourceUrl: null,
            ContentType: request.ContentType,
            OriginalFileName: request.OriginalFileName,
            ManualEntryText: null,
            Metadata: metadata);

        return Task.FromResult(payload);
    }
}
