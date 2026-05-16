using WeUP.Domain.Temporal;
using WeUP.Domain.Events;

namespace WeUP.Api.Endpoints;

/// <summary>
/// P21: Temporal Query Logic Endpoints
/// Maps temporal presets to time windows for event discovery.
/// </summary>
public static class TemporalEndpoints
{
    public static void MapTemporalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/temporal")
            .WithOpenApi()
            .WithName("Temporal");

        group.MapPost("/events-at-time", GetEventsAtTime)
            .WithName("GetEventsAtTime")
            .WithDescription("Get events within a temporal preset window");

        group.MapGet("/presets", GetTemporalPresets)
            .WithName("GetTemporalPresets")
            .WithDescription("List available temporal presets with labels");
    }

    /// <summary>
    /// POST /api/temporal/events-at-time
    /// Query events within a temporal preset window.
    /// </summary>
    private static IResult GetEventsAtTime([Microsoft.AspNetCore.Mvc.FromBody] TemporalPresetRequest request, CancellationToken ct)
    {
        // Validate preset
        if (!Enum.IsDefined(typeof(TemporalPreset), request.Preset))
            return Results.BadRequest(new { error = $"Unknown preset: {request.Preset}" });

        // Map preset to time window
        var timeWindow = TemporalPresetMapper.GetTimeWindow(
            request.Preset,
            referenceTime: DateTimeOffset.UtcNow,
            marketTimezone: request.MarketTimezone ?? "America/Chicago");

        timeWindow.Validate();

        // Phase 0: Return empty list. Real implementation queries database by temporal window.
        return Results.Ok(new TemporalQueryResponse(
            Preset: request.Preset,
            PresetLabel: TemporalPresetMapper.GetPresetLabel(request.Preset),
            TimeWindowStart: timeWindow.StartUtc,
            TimeWindowEnd: timeWindow.EndUtc,
            Timezone: timeWindow.Timezone,
            Events: Array.Empty<object>(),
            Count: 0));
    }

    /// <summary>
    /// GET /api/temporal/presets
    /// List all available temporal presets with UI labels.
    /// </summary>
    private static IResult GetTemporalPresets()
    {
        var presets = Enum.GetValues(typeof(TemporalPreset))
            .Cast<TemporalPreset>()
            .Select(p => new PresetDto(
                Value: (int)p,
                Name: p.ToString(),
                Label: TemporalPresetMapper.GetPresetLabel(p)))
            .ToArray();

        return Results.Ok(new PresetListResponse(Presets: presets));
    }
}

/// <summary>
/// Request to query events at a temporal preset.
/// </summary>
public class TemporalPresetRequest
{
    public TemporalPreset Preset { get; set; }
    public string? MarketTimezone { get; set; }
}

/// <summary>
/// Response for temporal query.
/// </summary>
public record TemporalQueryResponse(
    TemporalPreset Preset,
    string PresetLabel,
    DateTimeOffset TimeWindowStart,
    DateTimeOffset TimeWindowEnd,
    string Timezone,
    object[] Events,
    int Count);

/// <summary>
/// Temporal preset metadata.
/// </summary>
public record PresetDto(int Value, string Name, string Label);

/// <summary>
/// Response listing all temporal presets.
/// </summary>
public record PresetListResponse(PresetDto[] Presets);
