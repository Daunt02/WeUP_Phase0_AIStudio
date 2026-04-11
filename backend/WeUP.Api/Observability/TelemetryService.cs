using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public OperationalTelemetry(ILogger<OperationalTelemetry> logger, ActivitySource activitySource)
        {
            _logger = logger;
            _activitySource = activitySource;
        }

        public void TrackEvent(string name, IDictionary<string, string>? properties = null)
        {
            using var a = _activitySource.StartActivity(name, ActivityKind.Internal);
            if (properties != null)
            {
                foreach (var kv in properties)
                {
                    a?.SetTag(kv.Key, kv.Value);
                }
            }

            _logger.LogInformation("Telemetry Event: {EventName} {@Properties}", name, properties);
        }

        public void TrackException(Exception ex, IDictionary<string, string>? properties = null)
        {
            using var a = _activitySource.StartActivity("exception", ActivityKind.Internal);
            a?.SetTag("exception.type", ex.GetType().FullName ?? "unknown");
            a?.SetTag("exception.message", ex.Message);

            if (properties != null)
            {
                foreach (var kv in properties)
                {
                    a?.SetTag(kv.Key, kv.Value);
                }
            }

            _logger.LogError(ex, "Telemetry Exception: {Message} {@Properties}", ex.Message, properties);
        }
    }
}
