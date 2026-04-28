namespace WeUP.Contracts.Ingestion;

/// <summary>
/// Aggregated confidence for the three core dimensions of a candidate event.
/// </summary>
public sealed record ConfidenceVector(
    float TemporalConfidence,
    float SpatialConfidence,
    float SemanticConfidence)
{
    /// <summary>
    /// When any dimension falls below the safety threshold (default 0.8) the event
    /// must be reviewed manually.
    /// </summary>
    public bool RequiresManualReview =>
        TemporalConfidence < 0.8f ||
        SpatialConfidence < 0.8f ||
        SemanticConfidence < 0.8f;
}
