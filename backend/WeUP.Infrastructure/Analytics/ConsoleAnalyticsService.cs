using Microsoft.Extensions.Logging;
using WeUP.Domain.Analytics;

namespace WeUP.Infrastructure.Analytics;

/// <summary>
/// Console-based analytics service (stub for Phase 0).
/// Future: Replace with real sink (Datadog, Mixpanel, etc.).
/// </summary>
public class ConsoleAnalyticsService : IAnalyticsService
{
    private readonly ILogger<ConsoleAnalyticsService> _logger;

    public ConsoleAnalyticsService(ILogger<ConsoleAnalyticsService> logger)
    {
        _logger = logger;
    }

    public Task RecordEventAsync(AnalyticsEvent @event, CancellationToken ct = default)
    {
        if (!@event.IsPrivacyCompliant())
        {
            _logger.LogWarning("Rejecting analytics event: contains PII");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Analytics: {EventType} at {OccurredAt}", @event.EventType, @event.OccurredAt);
        return Task.CompletedTask;
    }
}
