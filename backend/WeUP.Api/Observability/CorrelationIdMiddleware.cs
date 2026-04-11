using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WeUP.Api.Observability;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    public const string HeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("d");

        context.Response.Headers[HeaderName] = correlationId;

        Activity.Current?.SetTag("correlation_id", correlationId);
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            context.Items[HeaderName] = correlationId;
            await _next(context);
        }
    }
}
