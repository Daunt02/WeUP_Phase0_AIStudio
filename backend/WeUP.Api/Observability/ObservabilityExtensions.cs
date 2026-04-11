using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;
using OpenTelemetry.Resources;

namespace WeUP.Api.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddWeUPObservability(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "WeUP")
            .WriteTo.Console(new JsonFormatter())
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(new ActivitySource("WeUP.Ingestion"));
        builder.Services.AddSingleton(new ActivitySource("WeUP.Moderation"));
        builder.Services.AddSingleton<CorrelationIdDelegatingHandler>();

        builder.Services.AddOpenTelemetryTracing(tp =>
        {
            tp.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("WeUP"))
              .AddAspNetCoreInstrumentation(o => o.RecordException = true)
              .AddHttpClientInstrumentation()
              .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
              .AddSource("WeUP.Ingestion", "WeUP.Moderation")
              .AddConsoleExporter();

            var otlp = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlp))
            {
                tp.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlp));
            }
        });

        builder.Services.AddOpenTelemetryMetrics(mb =>
        {
            mb.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("WeUP"))
              .AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation()
              .AddConsoleExporter();
        });

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("WeUP"));
            options.AddConsoleExporter();
        });

        return builder;
    }

    public static IApplicationBuilder UseWeUPObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
