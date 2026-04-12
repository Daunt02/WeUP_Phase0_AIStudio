namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// P28: EF Core entity for video processing jobs.
/// StageHistoryJson and ResultJson are stored as jsonb columns.
/// </summary>
public sealed class VideoProcessingJobEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Current stage name as string, or null when not yet started.</summary>
    public string? CurrentStage { get; set; }

    /// <summary>JSON array of VideoProcessingStageRecord objects.</summary>
    public string? StageHistoryJson { get; set; }

    /// <summary>JSON of VideoProcessingResult, populated on job completion.</summary>
    public string? ResultJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
