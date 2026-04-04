namespace WeUP.Domain.Media;

/// <summary>
/// P26: Enforces legal lifecycle transitions for flyer assets.
/// All state changes must go through Transition() — never assign Status directly.
/// </summary>
public static class FlyerAssetLifecycle
{
    // Allowed transitions: from → set of valid next states
    private static readonly IReadOnlyDictionary<FlyerAssetStatus, FlyerAssetStatus[]> AllowedTransitions =
        new Dictionary<FlyerAssetStatus, FlyerAssetStatus[]>
        {
            [FlyerAssetStatus.Initialized]       = [FlyerAssetStatus.Uploaded, FlyerAssetStatus.ValidationFailed],
            [FlyerAssetStatus.Uploaded]          = [FlyerAssetStatus.ValidationFailed, FlyerAssetStatus.ProcessingPending],
            [FlyerAssetStatus.ValidationFailed]  = [FlyerAssetStatus.Archived],
            [FlyerAssetStatus.ProcessingPending] = [FlyerAssetStatus.ReviewPending, FlyerAssetStatus.ValidationFailed],
            [FlyerAssetStatus.ReviewPending]     = [FlyerAssetStatus.Approved, FlyerAssetStatus.Rejected],
            [FlyerAssetStatus.Approved]          = [FlyerAssetStatus.Archived],
            [FlyerAssetStatus.Rejected]          = [FlyerAssetStatus.Archived],
            [FlyerAssetStatus.Archived]          = [],
        };

    /// <summary>
    /// Attempts a transition to <paramref name="next"/>. Throws if illegal.
    /// </summary>
    public static FlyerAssetStatus Transition(FlyerAssetStatus current, FlyerAssetStatus next)
    {
        if (!AllowedTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
            throw new InvalidOperationException(
                $"Illegal flyer asset lifecycle transition: {current} → {next}. " +
                $"Allowed from {current}: [{string.Join(", ", AllowedTransitions.GetValueOrDefault(current, []))}]");

        return next;
    }

    /// <summary>Returns true if the transition is legal, without throwing.</summary>
    public static bool CanTransition(FlyerAssetStatus current, FlyerAssetStatus next) =>
        AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    /// <summary>True when the asset is in a terminal state and requires no further action.</summary>
    public static bool IsTerminal(FlyerAssetStatus status) =>
        status is FlyerAssetStatus.Archived;

    /// <summary>True when the asset is display-safe (approved and not archived).</summary>
    public static bool IsSafeForDisplay(FlyerAssetStatus status) =>
        status is FlyerAssetStatus.Approved;
}
