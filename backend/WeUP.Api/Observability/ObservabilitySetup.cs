using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WeUP.Api.FeatureFlags;

namespace WeUP.Api.Observability;

/// <summary>
/// OpenTelemetry and structured logging setup for Phase 0 runtime.
/// </summary>
public static class ObservabilitySetup
{
    public static void AddWeUPObservability(this WebApplicationBuilder builder)
    {
        var serviceResource = ResourceBuilder.CreateDefault()
            .AddService(ObservabilityConstants.ServiceName, serviceVersion: ObservabilityConstants.ServiceVersion);

        // Structured logging
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.SetResourceBuilder(serviceResource);
            options.AddConsoleExporter();
        });

        builder.Services.AddSingleton(new System.Diagnostics.ActivitySource(ObservabilityConstants.ActivitySourceName));
        builder.Services.AddSingleton(new System.Diagnostics.Metrics.Meter(ObservabilityConstants.MeterName));
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: ObservabilityConstants.ServiceName,
                serviceVersion: ObservabilityConstants.ServiceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ObservabilityConstants.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddConsoleExporter();

                var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(ObservabilityConstants.MeterName)
                    // Ingestion pipeline metrics (M10-P46)
                    .AddMeter(ObservabilityConstants.IngestionMeterName)
                    .AddConsoleExporter()
                    // Prometheus scrape endpoint served at /metrics
                    .AddPrometheusExporter();

                var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter();
                }
            });

        // Correlation, telemetry, and feature flag seams used by the Phase 0 API.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<CorrelationIdProvider>();
        builder.Services.AddSingleton<CorrelationIdDelegatingHandler>();
        builder.Services.AddSingleton<IOperationalTelemetry, OperationalTelemetry>();
        builder.Services.Configure<FeatureFlagsOptions>(builder.Configuration.GetSection("FeatureFlags"));
        builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
    }

    public static void UseWeUPObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        // Expose /metrics for Prometheus scraping (M10-P46).
        // Endpoint is intentionally unauthenticated because Prometheus scrapers run
        // inside the cluster network.  Gate behind network policy if needed.
        app.MapPrometheusScrapingEndpoint();
    }
}

/// <summary>
/// Provider for correlation IDs across requests.
/// </summary>
public sealed class CorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        var fromContext = _httpContextAccessor.HttpContext?.Items[ObservabilityConstants.CorrelationContextKey]?.ToString();
        if (!string.IsNullOrWhiteSpace(fromContext))
        {
            return fromContext;
        }

        return System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "unknown";
    }
}
