namespace WeUP.Domain.Analytics;

/// <summary>
/// P23: Analytics Event Types
/// Core events for product metrics and feature flags.
/// </summary>
public enum AnalyticsEventType
{
    /// <summary>User viewed the map.</summary>
    MapViewed,

    /// <summary>User saved an event.</summary>
    EventSaved,

    /// <summary>User unsaved an event.</summary>
    EventUnsaved,

    /// <summary>User submitted a new event.</summary>
    EventSubmitted,

    /// <summary>User navigated to event detail.</summary>
    EventDetailViewed,

    /// <summary>User selected a temporal preset.</summary>
    TemporalPresetSelected,

    /// <summary>User selected a district filter.</summary>
    DistrictFilterApplied,

    /// <summary>Error occurred (guard against PII).</summary>
    ErrorOccurred,
}

/// <summary>
/// Analytics event record (minimal, guard against PII).
/// </summary>
public record AnalyticsEvent(
    AnalyticsEventType EventType,
    DateTimeOffset OccurredAt,
    string? UserId = null,
    Dictionary<string, object>? Properties = null)
{
    /// <summary>
    /// Ensure no PII in properties (stub for future validation).
    /// </summary>
    public bool IsPrivacyCompliant()
    {
        // Future: check Properties for email, phone, etc.
        return true;
    }
}

/// <summary>
/// Simple analytics service (stub: logs to console).
/// Future: Replace with real sink (Datadog, Mixpanel, etc.).
/// </summary>
public interface IAnalyticsService
{
    Task RecordEventAsync(AnalyticsEvent @event, CancellationToken ct = default);
}
