namespace WeUP.Domain.Flyer;

using WeUP.Contracts.Ocr;

/// <summary>
/// Service interface for deterministic confidence scoring of event candidates.
/// 
/// This service produces EventCandidateV2 records with fully traceable confidence values,
/// making all scoring decisions auditable and reproducible.
/// </summary>
public interface IConfidenceScorer
{
    /// <summary>
    /// Score an event candidate with full confidence tracking.
    /// </summary>
    /// <param name="candidate">The extracted event candidate to score.</param>
    /// <param name="ocrResult">The OCR result that produced this candidate (recommended for better scoring).</param>
    /// <param name="sourceKind">The source system that produced this data (e.g., "flyer_ocr", "manual_form").</param>
    /// <returns>Fully scored candidate with confidence values and evidence bundle.</returns>
    EventCandidateV2 ScoreCandidate(
        EventCandidate candidate,
        OcrResult? ocrResult = null,
        string sourceKind = "unknown");
}
