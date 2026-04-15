using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WeUP.Api.Observability
{
    public interface IOperationalTelemetry
    {
        void TrackEvent(string name, IDictionary<string, string>? properties = null);
        void TrackException(Exception ex, IDictionary<string, string>? properties = null);
    }

    public class OperationalTelemetry : IOperationalTelemetry
    {
        private readonly ILogger<OperationalTelemetry> _logger;
        private readonly ActivitySource _activitySource;
        private readonly CorrelationIdProvider _correlationIdProvider;

        public OperationalTelemetry(
            ILogger<OperationalTelemetry> logger,
            ActivitySource activitySource,
            CorrelationIdProvider correlationIdProvider)
        {
            _logger = logger;
            _activitySource = activitySource;
            _correlationIdProvider = correlationIdProvider;
        }

        public void TrackEvent(string name, IDictionary<string, string>? properties = null)
        {
            using var a = _activitySource.StartActivity(name, ActivityKind.Internal);
            var correlationId = _correlationIdProvider.GetCorrelationId();
            a?.SetTag("correlation_id", correlationId);
            if (properties != null)
            {
                foreach (var kv in properties)
                {
                    a?.SetTag(kv.Key, kv.Value);
                }
            }

            _logger.LogInformation(
                "Telemetry event {EventName} correlationId={CorrelationId} props={@Properties}",
                name,
                correlationId,
                properties);
        }

        public void TrackException(Exception ex, IDictionary<string, string>? properties = null)
        {
            using var a = _activitySource.StartActivity("exception", ActivityKind.Internal);
            var correlationId = _correlationIdProvider.GetCorrelationId();
            a?.SetTag("correlation_id", correlationId);
            a?.SetTag("exception.type", ex.GetType().FullName ?? "unknown");
            a?.SetTag("exception.message", ex.Message);
            a?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (properties != null)
            {
                foreach (var kv in properties)
                {
                    a?.SetTag(kv.Key, kv.Value);
                }
            }

            _logger.LogError(
                ex,
                "Telemetry exception correlationId={CorrelationId} message={Message} props={@Properties}",
                correlationId,
                ex.Message,
                properties);
        }
    }
}
