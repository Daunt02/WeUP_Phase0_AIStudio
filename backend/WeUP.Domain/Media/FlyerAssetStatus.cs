namespace WeUP.Domain.Media;

/// <summary>
/// P26: Flyer asset lifecycle states.
/// Transitions are enforced by FlyerAssetLifecycle — never mutate status directly.
/// </summary>
public enum FlyerAssetStatus
{
    /// <summary>Upload record created but file not yet received.</summary>
    Initialized,

    /// <summary>File received and stored. Awaiting validation.</summary>
    Uploaded,

    /// <summary>Validation failed — file rejected (bad MIME, oversized, corrupt, duplicate).</summary>
    ValidationFailed,

    /// <summary>Validation passed. Ready for OCR / enrichment pipeline.</summary>
    ProcessingPending,

    /// <summary>Processing complete. Awaiting human review before publication.</summary>
    ReviewPending,

    /// <summary>Reviewer approved asset for use.</summary>
    Approved,

    /// <summary>Reviewer rejected asset — not suitable for publication.</summary>
    Rejected,

    /// <summary>Asset soft-deleted or superseded.</summary>
    Archived,
}
