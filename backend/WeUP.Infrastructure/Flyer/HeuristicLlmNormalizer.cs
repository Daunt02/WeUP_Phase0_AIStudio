using WeUP.Contracts.Ingestion;
using WeUP.Domain.Flyer;

namespace WeUP.Infrastructure.Flyer;

/// <summary>
/// Backward-compatible wrapper name kept for existing references.
/// Internally delegates to the P11 flyer normalization seam.
/// </summary>
public sealed class HeuristicLlmNormalizer(IFlyerNormalizationService inner) : IFlyerNormalizationService
{
    public Task<FlyerNormalizationResult> NormalizeAsync(FlyerNormalizationRequest request, CancellationToken ct = default)
        => inner.NormalizeAsync(request, ct);
}
