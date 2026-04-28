using WeUP.Contracts.Moderation;
using WeUP.Domain.Flyer;

namespace WeUP.Domain.Moderation;

public sealed record ModerationQueueCountSample(
    string RiskLevel,
    string ModerationStatus,
    int Count);

public sealed record ModerationQueueAgeSample(
    string RiskLevel,
    string ModerationStatus,
    double AgeMs);

public sealed record ModerationQueueTelemetrySnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<ModerationQueueCountSample> QueueSize,
    IReadOnlyList<ModerationQueueCountSample> PendingTotals,
    IReadOnlyList<ModerationQueueAgeSample> BacklogAge);

/// <summary>
/// Shared telemetry dimensions for moderation observability.
///
/// These helpers intentionally normalize queue state into stable, alert-friendly tags
/// so moderation pressure remains visible even when the underlying queue model stores
/// status and risk metadata across multiple fields.
/// </summary>
public static class ModerationTelemetryDimensions
{
    private const string RiskLevelMarker = "risk-level:";

    public const string Pending = "pending";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string NeedsEdit = "needs_edit";
    public const string UnknownRiskLevel = "unknown";

    public static ModerationQueueTelemetrySnapshot Empty { get; } = new(
        DateTimeOffset.UtcNow,
        [],
        [],
        []);

    public static string ResolveRiskLevel(ModerationQueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ResolveRiskLevel(item.ReviewReasons, item.Confidence.Aggregate);
    }

    public static string ResolveRiskLevel(IEnumerable<string> reviewReasons, double confidenceAggregate)
    {
        ArgumentNullException.ThrowIfNull(reviewReasons);

        var persisted = reviewReasons
            .FirstOrDefault(reason => reason.StartsWith(RiskLevelMarker, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(persisted))
        {
            var level = persisted[RiskLevelMarker.Length..].Trim();
            return string.IsNullOrWhiteSpace(level) ? UnknownRiskLevel : level.ToLowerInvariant();
        }

        var fallback = confidenceAggregate switch
        {
            < 0.35 => EventRiskLevel.Restricted,
            < 0.55 => EventRiskLevel.High,
            < 0.75 => EventRiskLevel.Medium,
            _ => EventRiskLevel.Low,
        };

        return fallback.ToString().ToLowerInvariant();
    }

    public static string ResolveWorkflowStatus(ModerationQueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ResolveWorkflowStatus(item.Status, item.History.LastOrDefault()?.Action);
    }

    public static string ResolveWorkflowStatus(ModerationItemStatus itemStatus, string? action)
    {
        if (string.Equals(action, "request-changes", StringComparison.OrdinalIgnoreCase))
        {
            return NeedsEdit;
        }

        if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected;
        }

        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "merge", StringComparison.OrdinalIgnoreCase))
        {
            return Approved;
        }

        return itemStatus switch
        {
            ModerationItemStatus.Open => Pending,
            ModerationItemStatus.InReview => InReview,
            ModerationItemStatus.Resolved => Approved,
            _ => Pending,
        };
    }

    public static bool IsPendingStatus(string moderationStatus)
    {
        return string.Equals(moderationStatus, Pending, StringComparison.Ordinal)
            || string.Equals(moderationStatus, InReview, StringComparison.Ordinal)
            || string.Equals(moderationStatus, NeedsEdit, StringComparison.Ordinal);
    }

    public static DateTimeOffset ResolveProcessingStartedAtUtc(ModerationQueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var currentStatus = ResolveWorkflowStatus(item);
        var startedAtUtc = item.CreatedAt;

        foreach (var entry in item.History)
        {
            if (string.Equals(ResolveWorkflowStatus(entry.NextStatus, entry.Action), currentStatus, StringComparison.Ordinal))
            {
                startedAtUtc = entry.Timestamp;
            }
        }

        return startedAtUtc;
    }
}