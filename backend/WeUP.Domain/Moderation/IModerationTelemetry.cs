namespace WeUP.Domain.Moderation;

public interface IModerationTelemetry
{
    Task RefreshBacklogAsync(CancellationToken ct = default);

    void TrackProcessingCompletion(
        ModerationQueueItem item,
        string action,
        string moderationStatus,
        DateTimeOffset startedAtUtc,
        DateTimeOffset actionedAtUtc,
        string actorId);
}

public sealed class NullModerationTelemetry : IModerationTelemetry
{
    public static NullModerationTelemetry Instance { get; } = new();

    private NullModerationTelemetry()
    {
    }

    public Task RefreshBacklogAsync(CancellationToken ct = default)
    {
        _ = ct;
        return Task.CompletedTask;
    }

    public void TrackProcessingCompletion(
        ModerationQueueItem item,
        string action,
        string moderationStatus,
        DateTimeOffset startedAtUtc,
        DateTimeOffset actionedAtUtc,
        string actorId)
    {
        _ = item;
        _ = action;
        _ = moderationStatus;
        _ = startedAtUtc;
        _ = actionedAtUtc;
        _ = actorId;
    }
}