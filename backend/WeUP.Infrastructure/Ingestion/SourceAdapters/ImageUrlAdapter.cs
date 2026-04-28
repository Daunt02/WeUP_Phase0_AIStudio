using System.Collections.Immutable;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.SourceAdapters;

/// <summary>
/// Adapter for remotely hosted images.
/// Boundary invariant: bytes are downloaded and hashed before downstream processing.
/// </summary>
public sealed class ImageUrlAdapter(IHttpClientFactory httpClientFactory) : ISourceAdapter
{
    public IngestionSourceType SourceType => IngestionSourceType.ImageUrl;

    public async Task<RawIngestionPayload> AdaptAsync(IngestionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SourceAdapterGuards.EnsureCommonInvariants(request);

        if (request.SourceType != IngestionSourceType.ImageUrl)
        {
            throw new IngestionValidationException("ImageUrlAdapter cannot handle a different source type.");
        }

        SourceAdapterGuards.EnsureNoCrossSourceFields(request, nameof(IngestionRequest.ImageUrl));

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new IngestionValidationException("imageUrl is required for ImageUrl source type.");
        }

        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new IngestionValidationException("imageUrl must be a valid absolute HTTP or HTTPS URL.");
        }

        using var client = httpClientFactory.CreateClient("ingestion");
        using var response = await client.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new IngestionValidationException($"imageUrl fetch failed with status code {(int)response.StatusCode}.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentBytes = ImmutableArray.Create(bytes);
        if (contentBytes.IsDefaultOrEmpty)
        {
            throw new IngestionValidationException("imageUrl resolved to an empty payload.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? request.ContentType;
        var sha256 = SourceAdapterGuards.ComputeSha256(contentBytes);

        IReadOnlyDictionary<string, string?> metadata = request.Metadata is null
            ? new Dictionary<string, string?>()
            : request.Metadata.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);
        metadata = SourceAdapterGuards.MetadataWithInvariant(metadata, "adapter.sourceType", request.SourceType.ToString());
        metadata = SourceAdapterGuards.MetadataWithInvariant(metadata, "adapter.sourceUrl", request.ImageUrl);
        metadata = SourceAdapterGuards.MetadataWithInvariant(metadata, "adapter.httpStatus", ((int)response.StatusCode).ToString());

        var payload = new RawIngestionPayload(
            RequestId: request.RequestId,
            SourceType: request.SourceType,
            SubmittedBy: request.SubmittedBy,
            IngestedAtUtc: DateTimeOffset.UtcNow,
            ContentBytes: contentBytes,
            ContentSha256: sha256,
            SourceUrl: request.ImageUrl,
            ContentType: contentType,
            OriginalFileName: request.OriginalFileName,
            ManualEntryText: null,
            Metadata: metadata);

        return payload;
    }
}
