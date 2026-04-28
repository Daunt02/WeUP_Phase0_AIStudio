namespace WeUP.Api.Observability;

public static class ObservabilityConstants
{
    public const string ServiceName = "weup-api";
    public const string ServiceVersion = "phase0";
    public const string CorrelationHeader = "X-Correlation-ID";
    public const string CorrelationContextKey = "CorrelationId";
    public const string ActivitySourceName = "WeUP.Api";
    public const string MeterName = "WeUP.Api";
    /// <summary>
    /// Dedicated meter for ingestion pipeline metrics (M10-P46).
    /// Kept separate from the API meter so ingestion instruments can be
    /// enabled or disabled independently.
    /// </summary>
    public const string IngestionMeterName = "WeUP.Ingestion";
    /// <summary>
    /// Dedicated meter for OCR and normalization quality telemetry (M10-P47).
    /// This remains isolated from ingestion and API meters to keep signal ownership clear.
    /// </summary>
    public const string OcrNormalizationMeterName = "WeUP.OcrNormalization";
    public const string IngestionActivitySource = "WeUP.Ingestion";
    public const string ModerationActivitySource = "WeUP.Moderation";
}
