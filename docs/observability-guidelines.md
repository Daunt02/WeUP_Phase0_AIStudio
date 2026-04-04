# WeUP Observability Guidelines

**Document Created:** 2026-04-04
**Audience:** All backend developers
**Purpose:** Standardized logging and observability practices for Phase 0+

---

## Overview

WeUP uses structured logging and correlation IDs to track requests across the system. This guide explains how to implement observability in new services.

**Key Concepts:**
- **Structured Logging:** Key-value pairs instead of unstructured strings
- **Correlation IDs:** Unique ID per request, propagated through all logs
- **Log Levels:** Information (default), Warning (errors), Error (exceptions)
- **Scoping:** Include userId, marketId, districtId in log context

---

## Correlation IDs

Every HTTP request gets a unique correlation ID that appears in all related logs.

### How It Works

1. **Request Arrives:** X-Correlation-ID header (or generated if missing)
2. **Middleware Captures:** `ObservabilitySetup.UseWeUPObservability()`
3. **Stored in Context:** `context.Items["CorrelationId"]`
4. **Response Includes:** X-Correlation-ID header echoed back
5. **Logs Include:** Correlation ID in every message

### Example Request/Response

```bash
# Request
curl -H "X-Correlation-ID: abc123def456" http://localhost:5000/api/spatial/map-feed

# Response Headers
HTTP/1.1 200 OK
X-Correlation-ID: abc123def456

# Console Logs
[12:34:56] Incoming request: POST /api/spatial/map-feed (X-Correlation-ID: abc123def456)
[12:34:56] Viewport query: bounds=[37.7,37.8,-122.4,-122.3] district=all (X-Correlation-ID: abc123def456)
[12:34:56] Viewport query result: 15 events in viewport (X-Correlation-ID: abc123def456)
[12:34:56] Response: 200 for POST /api/spatial/map-feed (X-Correlation-ID: abc123def456)
```

---

## Injecting ILogger

### Pattern: Dependency Injection

All services should accept `ILogger<T>` in their constructor:

```csharp
using Microsoft.Extensions.Logging;

public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task DoSomethingAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting operation");
        // ... work ...
        _logger.LogInformation("Operation completed");
    }
}
```

### Registration in Program.cs

Services are automatically registered via the DI container:

```csharp
// In Endpoints
private static IResult GetEventsInViewport(
    MapFeedRequest request,
    IViewportQueryService viewportService,
    ILogger<SpatialEndpoints> logger,
    CancellationToken ct)
{
    logger.LogInformation("Spatial endpoint called");
    // ...
}

// In Services
public class ViewportQueryService : IViewportQueryService
{
    private readonly ILogger<ViewportQueryService> _logger;
    
    public ViewportQueryService(
        IEventRepository eventRepository,
        ILogger<ViewportQueryService> logger)
    {
        _logger = logger;
    }
}
```

---

## Structured Logging

### Pattern: Key-Value Context

Log with named parameters instead of string concatenation:

```csharp
// ❌ Bad: Unstructured
_logger.LogInformation($"Viewport query for bounds {request.Bounds} returned {eventCount} events");

// ✅ Good: Structured
_logger.LogInformation(
    "Viewport query: bounds=[{MinLat},{MaxLat},{MinLng},{MaxLng}] event_count={EventCount}",
    request.Bounds.MinLat,
    request.Bounds.MaxLat,
    request.Bounds.MinLng,
    request.Bounds.MaxLng,
    eventCount);
```

**Why?** Structured logs enable:
- Searching by field (e.g., `event_count > 100`)
- Aggregation and alerting
- Machine parsing of logs
- Integration with Datadog, ELK, etc.

### Common Fields to Log

Log these fields whenever relevant:

| Field | Example | When |
|-------|---------|------|
| `userId` | "user123" | User-specific operations |
| `marketCode` | "sf" | Market-specific operations |
| `districtCode` | "downtown" | District-specific operations |
| `eventId` | "evt456" | Event-specific operations |
| `operationName` | "ViewportQuery" | Start of major operation |
| `duration_ms` | 125 | End of operation |
| `error` | "BoundsValidationFailed" | On error |

---

## Log Levels

Use the correct level for each message:

### LogInformation (Default)

Track happy-path operations:

```csharp
_logger.LogInformation("Viewport query: {EventCount} events returned", count);
_logger.LogInformation("User {UserId} saved event {EventId}", userId, eventId);
_logger.LogInformation("Analytics event recorded: {EventType}", eventType);
```

### LogWarning

Track potentially problematic situations:

```csharp
_logger.LogWarning("Viewport bounds outside market area: {Bounds}", bounds);
_logger.LogWarning("Analytics event rejected for PII: {EventType}", eventType);
_logger.LogWarning("Slow query detected: {OperationName} took {Duration}ms", op, duration);
```

### LogError

Track exceptions and failures:

```csharp
catch (InvalidOperationException ex)
{
    _logger.LogError(ex, "Failed to query viewport: {Bounds}", bounds);
}
```

---

## Example: Complete Service with Logging

```csharp
using Microsoft.Extensions.Logging;
using WeUP.Domain.Temporal;

public class TemporalQueryService
{
    private readonly IEventRepository _eventRepository;
    private readonly ILogger<TemporalQueryService> _logger;

    public TemporalQueryService(
        IEventRepository eventRepository,
        ILogger<TemporalQueryService> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task<EventDto[]> GetEventsInPresetAsync(
        TemporalPreset preset,
        string marketTimezone,
        string? userId = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Temporal query started: preset={Preset} timezone={Timezone} user={UserId}",
                preset,
                marketTimezone,
                userId ?? "anonymous");

            // Map preset to time window
            var timeWindow = TemporalPresetMapper.GetTimeWindow(
                preset,
                referenceTime: DateTimeOffset.UtcNow,
                marketTimezone: marketTimezone);

            timeWindow.Validate();

            // Query events in time window
            var events = await _eventRepository.GetEventsInTimeWindowAsync(
                timeWindow.StartUtc,
                timeWindow.EndUtc,
                ct);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "Temporal query completed: preset={Preset} event_count={EventCount} duration_ms={Duration}",
                preset,
                events.Length,
                duration);

            return events;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Temporal query failed: preset={Preset} timezone={Timezone}",
                preset,
                marketTimezone);

            throw;
        }
    }
}
```

---

## Adding Logging to Endpoints

### Pattern: Log Entry and Exit

```csharp
public static class TemporalEndpoints
{
    public static void MapTemporalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/temporal/events-at-time", GetEventsAtTime);
    }

    private static async Task<IResult> GetEventsAtTime(
        TemporalPresetRequest request,
        ITemporalQueryService temporalService,
        ILogger<TemporalEndpoints> logger,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Temporal endpoint: preset={Preset} timezone={Timezone}",
            request.Preset,
            request.MarketTimezone ?? "default");

        try
        {
            var events = await temporalService.GetEventsAtTime(request, ct);

            logger.LogInformation(
                "Temporal endpoint success: event_count={Count}",
                events.Length);

            return Results.Ok(new TemporalQueryResponse(/* ... */));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid temporal preset: {Preset}", request.Preset);
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
```

---

## Accessing Correlation ID in Services

If you need the correlation ID in a service (e.g., for analytics):

```csharp
using Microsoft.AspNetCore.Http;

public class MyService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MyService> _logger;

    public MyService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<MyService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void LogWithCorrelation(string message)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString()
            ?? "unknown";

        _logger.LogInformation("Message: {Message} correlation_id={CorrelationId}", message, correlationId);
    }
}
```

---

## Current Services with Logging

**Services already instrumented:**
- ✅ ViewportQueryService — Logs viewport queries and results
- ✅ ObservabilitySetup — Middleware logs all requests

**Services to instrument next:**
- [ ] TemporalQueryService (P21)
- [ ] AnalyticsService (P23)
- [ ] MarketPolicyService (P20)
- [ ] EventSubmissionService (P18)

---

## Future: Structured Log Output

Currently, logs go to console. In future phases:

1. **Serilog Integration** (Phase 0.5)
   ```csharp
   // Program.cs
   builder.Services.AddLogging(opts => {
       opts.ClearProviders();
       opts.AddSerilog();
   });
   ```

2. **JSON Output**
   ```json
   {
     "Timestamp": "2026-04-04T12:34:56Z",
     "Level": "Information",
     "Message": "Viewport query completed",
     "EventType": "ViewportQuery",
     "EventCount": 15,
     "Duration": 125,
     "CorrelationId": "abc123def456"
   }
   ```

3. **External Sink** (Datadog, ELK, etc.)
   - Correlation IDs automatically propagate
   - All structured fields searchable
   - Real-time alerting on error counts

---

## Checklist: Adding Logging to New Service

- [ ] Import `using Microsoft.Extensions.Logging;`
- [ ] Add `ILogger<T>` parameter to constructor
- [ ] Store as `private readonly ILogger<T> _logger;`
- [ ] Log at service entry: `_logger.LogInformation("Operation started: {Param}", param);`
- [ ] Log at service exit: `_logger.LogInformation("Operation completed: {Result}", result);`
- [ ] Log on error: `_logger.LogError(ex, "Operation failed", ...);`
- [ ] Use structured fields (not string concatenation)
- [ ] Include relevant context (userId, marketId, eventId, etc.)

---

## Questions?

Refer to:
- `backend/WeUP.Api/Observability/ObservabilitySetup.cs` — Middleware implementation
- `backend/WeUP.Infrastructure/Spatial/ViewportQueryService.cs` — Example service
- `backend/WeUP.Api/Program.cs` — Service registration and wiring

---

**Document Version:** 1.0
**Last Updated:** 2026-04-04
**Next Review:** After P22 completion
