using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace WeUP.Api.Observability;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? id = null;
        id = _accessor.HttpContext?.Items[ObservabilityConstants.CorrelationContextKey] as string;
        if (string.IsNullOrEmpty(id))
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                id = activity.Tags.FirstOrDefault(t => t.Key == "correlation_id").Value;
                if (string.IsNullOrEmpty(id)) id = activity.Id;
            }
        }

        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(ObservabilityConstants.CorrelationHeader))
        {
            request.Headers.Add(ObservabilityConstants.CorrelationHeader, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
