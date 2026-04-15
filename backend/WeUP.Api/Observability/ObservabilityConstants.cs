namespace WeUP.Api.Observability;

public static class ObservabilityConstants
{
    public const string ServiceName = "weup-api";
    public const string ServiceVersion = "phase0";
    public const string CorrelationHeader = "X-Correlation-ID";
    public const string CorrelationContextKey = "CorrelationId";
    public const string ActivitySourceName = "WeUP.Api";
    public const string MeterName = "WeUP.Api";
    public const string IngestionActivitySource = "WeUP.Ingestion";
    public const string ModerationActivitySource = "WeUP.Moderation";
}
