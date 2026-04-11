using Microsoft.Extensions.Logging;

namespace WeUP.Api.Observability;

public static class LoggingExtensions
{
    private static readonly EventId IngestionEventId = new EventId(1000, "Ingestion");
    private static readonly EventId ModerationEventId = new EventId(2000, "Moderation");

    public static void LogIngestionStarted(this ILogger logger, string ingestionId, string source)
    {
        logger.LogInformation(IngestionEventId, "Ingestion started: {IngestionId} from {Source}", ingestionId, source);
    }

    public static void LogIngestionCompleted(this ILogger logger, string ingestionId, int processed)
    {
        logger.LogInformation(IngestionEventId, "Ingestion completed: {IngestionId} processed {Count}", ingestionId, processed);
    }

    public static void LogIngestionFailed(this ILogger logger, string ingestionId, Exception ex)
    {
        logger.LogError(IngestionEventId, ex, "Ingestion failed: {IngestionId}", ingestionId);
    }

    public static void LogModerationCreated(this ILogger logger, string moderationId, string itemId)
    {
        logger.LogInformation(ModerationEventId, "Moderation item created: {ModerationId} for {ItemId}", moderationId, itemId);
    }

    public static void LogModerationResolved(this ILogger logger, string moderationId, string resolution)
    {
        logger.LogInformation(ModerationEventId, "Moderation resolved: {ModerationId} resolution={Resolution}", moderationId, resolution);
    }
}
