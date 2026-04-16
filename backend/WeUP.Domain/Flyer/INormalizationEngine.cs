using WeUP.Contracts.Ocr;

namespace WeUP.Domain.Flyer;

public sealed record FieldHeuristicScore(
    double Confidence,
    string Rationale);

public sealed record EventCandidate(
    string? Title,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? Venue,
    string? Address,
    string[] Tags,
    IReadOnlyDictionary<string, string?> RawFields,
    IReadOnlyDictionary<string, FieldHeuristicScore> FieldScores);

public interface INormalizationEngine
{
    EventCandidate Normalize(OcrResult ocr);
}
