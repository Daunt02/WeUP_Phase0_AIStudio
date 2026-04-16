using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Ingestion;

namespace WeUP.Infrastructure.Ingestion.SourceAdapters;

internal static class SourceAdapterGuards
{
    internal static void EnsureCommonInvariants(IngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            throw new IngestionValidationException("requestId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SubmittedBy))
        {
            throw new IngestionValidationException("submittedBy is required.");
        }

        if (request.ReceivedAtUtc == default)
        {
            throw new IngestionValidationException("receivedAtUtc must be a valid timestamp.");
        }

        if (request.Metadata is null)
        {
            throw new IngestionValidationException("metadata must be provided.");
        }
    }

    internal static void EnsureNoCrossSourceFields(IngestionRequest request, params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);

        if (!allowedSet.Contains(nameof(IngestionRequest.ImageBase64)) && !string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            throw new IngestionValidationException("imageBase64 is not allowed for this source type.");
        }

        if (!allowedSet.Contains(nameof(IngestionRequest.ImageUrl)) && !string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new IngestionValidationException("imageUrl is not allowed for this source type.");
        }

        if (!allowedSet.Contains(nameof(IngestionRequest.ManualEntryText)) && !string.IsNullOrWhiteSpace(request.ManualEntryText))
        {
            throw new IngestionValidationException("manualEntryText is not allowed for this source type.");
        }
    }

    internal static ImmutableDictionary<string, string?> MetadataWithInvariant(
        ImmutableDictionary<string, string?> metadata,
        string key,
        string? value)
    {
        if (metadata.ContainsKey(key))
        {
            throw new IngestionValidationException($"metadata already contains reserved key '{key}'.");
        }

        return metadata.Add(key, value);
    }

    internal static string ComputeSha256(ImmutableArray<byte> bytes)
    {
        var hash = SHA256.HashData(bytes.AsSpan());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static ImmutableArray<byte> Utf8(string value)
        => ImmutableArray.Create(Encoding.UTF8.GetBytes(value));
}
