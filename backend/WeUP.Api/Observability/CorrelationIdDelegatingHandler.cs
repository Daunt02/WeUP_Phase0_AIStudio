using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace WeUP.Api.Observability;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    public const string HeaderName = "X-Correlation-ID";

    public CorrelationIdDelegatingHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? id = null;
        id = _accessor.HttpContext?.Items[HeaderName] as string;
        if (string.IsNullOrEmpty(id))
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                id = activity.Tags.FirstOrDefault(t => t.Key == "correlation_id").Value;
                if (string.IsNullOrEmpty(id)) id = activity.Id;
            }
        }

        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(HeaderName))
        {
            request.Headers.Add(HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
