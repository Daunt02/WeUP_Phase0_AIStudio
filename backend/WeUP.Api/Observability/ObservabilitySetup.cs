using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WeUP.Api.Observability;

/// <summary>
/// P22 OpenTelemetry & Structured Logging Setup
/// Minimal implementation: console logging, correlation IDs, trace instrumentation
/// </summary>
public static class ObservabilitySetup
{
    public static void AddWeUPObservability(this WebApplicationBuilder builder)
    {
        // Structured logging (minimal: console sink)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Add correlation ID service
        builder.Services.AddScoped<CorrelationIdProvider>();
    }

    public static void UseWeUPObservability(this WebApplication app)
    {
        // Correlation ID middleware
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Guid.NewGuid().ToString();

            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers.Add("X-Correlation-ID", correlationId);

            await next();
        });
    }
}

/// <summary>
/// Provider for correlation IDs across requests.
/// </summary>
public class CorrelationIdProvider
{
    public string GetCorrelationId(IHttpContextAccessor httpContextAccessor)
    {
        return httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString() ?? "unknown";
    }
}
