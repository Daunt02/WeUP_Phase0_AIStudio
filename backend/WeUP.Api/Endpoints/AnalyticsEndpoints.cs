using WeUP.Domain.Analytics;

namespace WeUP.Api.Endpoints;

/// <summary>
/// P23: Product Analytics Endpoints
/// Record analytics events for product metrics and feature flags.
/// </summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics")
            .WithOpenApi()
            .WithName("Analytics");

        group.MapPost("/events", RecordAnalyticsEvent)
            .WithName("RecordAnalyticsEvent")
            .WithDescription("Record an analytics event");

        group.MapGet("/event-types", GetAnalyticsEventTypes)
            .WithName("GetAnalyticsEventTypes")
            .WithDescription("List available analytics event types");
    }

    /// <summary>
    /// POST /api/analytics/events
    /// Record an analytics event for product metrics.
    /// </summary>
    private static async Task<IResult> RecordAnalyticsEvent(
        RecordAnalyticsRequest request,
        IAnalyticsService analyticsService,
        CancellationToken ct)
    {
        // Validate event type
        if (!Enum.IsDefined(typeof(AnalyticsEventType), request.EventType))
            return Results.BadRequest(new { error = $"Unknown event type: {request.EventType}" });

        // Create analytics event
        var analyticsEvent = new AnalyticsEvent(
            EventType: request.EventType,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: request.UserId,
            Properties: request.Properties);

        // Record event (service handles PII checks)
        await analyticsService.RecordEventAsync(analyticsEvent, ct);

        return Results.Accepted();
    }

    /// <summary>
    /// GET /api/analytics/event-types
    /// List all available analytics event types.
    /// </summary>
    private static IResult GetAnalyticsEventTypes()
    {
        var eventTypes = Enum.GetValues(typeof(AnalyticsEventType))
            .Cast<AnalyticsEventType>()
            .Select(e => new EventTypeDto(Value: (int)e, Name: e.ToString()))
            .ToArray();

        return Results.Ok(new EventTypeListResponse(EventTypes: eventTypes));
    }
}

/// <summary>
/// Request to record an analytics event.
/// </summary>
public record RecordAnalyticsRequest(
    AnalyticsEventType EventType,
    string? UserId = null,
    Dictionary<string, object>? Properties = null);

/// <summary>
/// Analytics event type metadata.
/// </summary>
public record EventTypeDto(int Value, string Name);

/// <summary>
/// Response listing all analytics event types.
/// </summary>
public record EventTypeListResponse(EventTypeDto[] EventTypes);
