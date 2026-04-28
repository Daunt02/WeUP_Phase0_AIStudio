namespace WeUP.Contracts.Ingestion;

/// <summary>
/// Configuration options for confidence evaluation.
/// </summary>
public sealed class ConfidenceOptions
{
    /// <summary>
    /// Minimum confidence required to avoid manual review. Default = 0.8.
    /// </summary>
    public float ReviewThreshold { get; set; } = 0.8f;
}
