// ------------------------------------------------------------
// File: WeUP.Domain/Ingestion/INormalizationEngine.cs
// ------------------------------------------------------------
using System.Threading.Tasks;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// Pure, side-effect free service that converts an <see cref="OcrResult"/>
/// into a <see cref="CandidateEvent"/>.  No database, network or caching may be used.
/// </summary>
public interface INormalizationEngine
{
    /// <summary>
    /// Transform the OCR result into a candidate event.
    /// Must be deterministic – identical input yields identical output.
    /// </summary>
    Task<CandidateEvent> NormalizeAsync(OcrResult ocrResult);
}
