using System.Security.Cryptography;
using System.Text;

namespace WeUP.Domain.Media;

/// <summary>
/// P27: Source trust tiers for flyer provenance.
/// T1 = verified (promoter/venue/admin), T2 = approved source, T3 = unverified user.
/// </summary>
public enum SourceTier
{
    T1_Verified  = 1,  // Verified promoter, venue, or moderator — authority 0.88–0.95
    T2_Approved  = 2,  // Approved aggregator or venue calendar — authority 0.55
    T3_Unverified = 3, // General user submission — authority 0.35
}

/// <summary>
/// P27: Provenance record for a submitted flyer.
/// Captures who submitted, what source tier, and a hashed identity reference.
/// Raw submitter PII is never stored here — only a one-way hash.
/// </summary>
public sealed record ProvenanceRecord
{
    public required string ProvenanceId { get; init; }

    /// <summary>FK to FlyerAssetRecord.AssetId</summary>
    public required string AssetId { get; init; }

    public required SourceTier SourceTier { get; init; }

    /// <summary>
    /// SHA-256 hash of the submitter's user ID — not the raw ID.
    /// Preserves audit trail without storing PII in the provenance log.
    /// </summary>
    public required string SubmitterHash { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>Baseline authority score derived from SourceTier at intake.</summary>
    public required double BaselineAuthority { get; init; }

    /// <summary>Source URL if ingested from an approved external source (not user upload).</summary>
    public string? SourceUrl { get; init; }

    /// <summary>EventSubmission ID if this flyer was submitted as part of an event submission.</summary>
    public string? SubmissionId { get; init; }

    /// <summary>Notes left by the submitter at intake time (optional).</summary>
    public string? SubmitterNote { get; init; }

    // ── Static helpers ───────────────────────────────────────────────────────

    public static string HashSubmitterId(string submitterId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(submitterId));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static double BaselineAuthorityForTier(SourceTier tier) => tier switch
    {
        SourceTier.T1_Verified   => 0.90,
        SourceTier.T2_Approved   => 0.55,
        SourceTier.T3_Unverified => 0.35,
        _ => 0.35,
    };
}

/// <summary>
/// P27: Durable storage contract for flyer provenance records.
/// </summary>
public interface IProvenanceRepository
{
    Task<ProvenanceRecord> SaveAsync(ProvenanceRecord record, CancellationToken ct = default);
    Task<ProvenanceRecord?> GetAsync(string provenanceId, CancellationToken ct = default);
    Task<ProvenanceRecord?> GetByAssetIdAsync(string assetId, CancellationToken ct = default);
    Task<ProvenanceRecord[]> ListBySubmitterHashAsync(string submitterHash, CancellationToken ct = default);
}
