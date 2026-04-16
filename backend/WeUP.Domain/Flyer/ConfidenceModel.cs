namespace WeUP.Domain.Flyer;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Generic field confidence record that captures confidence, evidence source, and rationale.
/// This is the foundation of the confidence model - every extracted field must have explicit confidence.
/// </summary>
/// <typeparam name="TField">The type of the field being scored (string, DateTime, etc.)</typeparam>
public sealed record FieldConfidence<TField>(
    /// <summary>The actual extracted value.</summary>
    TField Value,

    /// <summary>
    /// Confidence score [0.0, 1.0] where:
    /// 1.0 = Maximum confidence (e.g., from structured/canonical source)
    /// 0.9 = High confidence (e.g., OCR with high confidence + multiple corroborating signals)
    /// 0.7-0.8 = Moderate confidence (e.g., OCR + weak heuristic match)
    /// 0.5-0.6 = Low confidence (e.g., OCR alone with ambiguity)
    /// 0.3-0.4 = Very low confidence (e.g., derived inference)
    /// 0.0-0.2 = Minimal/fallback (e.g., placeholder or required field missing)
    /// </summary>
    double Confidence,

    /// <summary>Evidence references that support this confidence score.</summary>
    IReadOnlyList<string> EvidenceRefs,

    /// <summary>
    /// Human-readable explanation of how confidence was derived.
    /// Examples:
    /// - "OCR confidence 0.95 + venue database match (0.90) = aggregated 0.93"
    /// - "Derived from implicit timezone via heuristic (0.6 base confidence)"
    /// - "Temporal conflict penalty applied: 8 hours overlap detected (-0.15)"
    /// </summary>
    string Rationale,

    /// <summary>Source of the evidence (e.g., "ocr", "venue_db", "heuristic", "submission_form").</summary>
    string EvidenceSource,

    /// <summary>Optional metadata about confidence derivation for debugging/audit.</summary>
    IReadOnlyDictionary<string, string?>? Metadata = null);

/// <summary>
/// Comprehensive evidence bundle that preserves all inputs needed to audit confidence decisions.
/// This is immutable and auditable - no information is discarded during normalization.
/// </summary>
public sealed record EvidenceBundle(
    /// <summary>Unique identifier for this evidence bundle.</summary>
    string BundleId,

    /// <summary>Timestamp when evidence was collected.</summary>
    DateTimeOffset CollectedAtUtc,

    /// <summary>The raw OCR text before any normalization.</summary>
    string? RawOcrText,

    /// <summary>Individual OCR text blocks with confidence scores and positions.</summary>
    IReadOnlyList<OcrTextBlock>? OcrBlocks,

    /// <summary>
    /// Source hash (SHA256) of the raw input material.
    /// Used to detect if the same evidence has been processed multiple times.
    /// </summary>
    string? SourceHash,

    /// <summary>The source kind that produced this evidence (e.g., "flyer_ocr", "manual_form", "entity_resolution").</summary>
    string SourceKind,

    /// <summary>
    /// All field-level scoring components used to derive confidence.
    /// This allows replaying the scoring calculation for audit or adjustment.
    /// </summary>
    IReadOnlyDictionary<string, ConfidenceComponent>? ScoringComponents,

    /// <summary>Optional processing context (e.g., OCR engine version, normalization run ID).</summary>
    IReadOnlyDictionary<string, string?>? ProcessingContext = null);

/// <summary>
/// Individual OCR text block with confidence and spatial information.
/// </summary>
public sealed record OcrTextBlock(
    /// <summary>Sequential index of this block in the OCR result.</summary>
    int Index,

    /// <summary>Extracted text content.</summary>
    string Text,

    /// <summary>OCR engine's confidence [0.0, 1.0].</summary>
    double Confidence,

    /// <summary>X coordinate in the source image.</summary>
    int X,

    /// <summary>Y coordinate in the source image.</summary>
    int Y,

    /// <summary>Width of the bounding box.</summary>
    int Width,

    /// <summary>Height of the bounding box.</summary>
    int Height,

    /// <summary>Additional metadata from OCR engine.</summary>
    IReadOnlyDictionary<string, string?>? Metadata = null);

/// <summary>
/// Atomic scoring component that contributed to a field's confidence.
/// Used internally by ConfidenceScorer to track confidence derivation.
/// </summary>
public sealed record ConfidenceComponent(
    /// <summary>The type of component (e.g., "ocr_direct", "heuristic_match", "conflict_penalty").</summary>
    string ComponentType,

    /// <summary>The base confidence value contributed by this component.</summary>
    double BaseScore,

    /// <summary>
    /// Optional modifier applied to baseScore (multiplier or fixed deduction).
    /// Examples: 1.0 (unchanged), 0.95 (5% penalty), 0.8 (20% reduction).
    /// </summary>
    double? Modifier = 1.0,

    /// <summary>Human-readable description of this component's contribution.</summary>
    string? Description = null);

/// <summary>
/// EventCandidateV2: Enhanced version of EventCandidate that includes field-level confidence
/// and preserves evidence for audit and review.
/// 
/// This record ensures that:
/// - Every extracted field has explicit, traceable confidence
/// - No "implicit confidence" exists
/// - Confidence decisions are auditable and reproducible
/// - Evidence is preserved for review workflows
/// </summary>
public sealed record EventCandidateV2(
    /// <summary>Event title with explicit confidence.</summary>
    FieldConfidence<string>? Title,

    /// <summary>Event start date/time (UTC) with explicit confidence.</summary>
    FieldConfidence<DateTimeOffset>? StartUtc,

    /// <summary>Event end date/time (UTC) with explicit confidence.</summary>
    FieldConfidence<DateTimeOffset>? EndUtc,

    /// <summary>Venue name with explicit confidence.</summary>
    FieldConfidence<string>? Venue,

    /// <summary>Full address with explicit confidence.</summary>
    FieldConfidence<string>? Address,

    /// <summary>Category/classification with explicit confidence.</summary>
    FieldConfidence<string>? Category,

    /// <summary>Event description/notes with explicit confidence.</summary>
    FieldConfidence<string>? Description,

    /// <summary>Tags/categories (array) with explicit confidence.</summary>
    FieldConfidence<IReadOnlyList<string>>? Tags,

    /// <summary>
    /// Overall confidence aggregated from all field-level scores.
    /// This is deterministic and explained via aggregation rules.
    /// </summary>
    double OverallConfidence,

    /// <summary>
    /// Explanation of overall confidence derivation.
    /// Documents which fields were present, which penalties applied, final score.
    /// </summary>
    string OverallConfidenceRationale,

    /// <summary>The evidence bundle supporting all confidence scores in this candidate.</summary>
    EvidenceBundle EvidenceBundle,

    /// <summary>Timestamp when this candidate was created.</summary>
    DateTimeOffset CreatedAtUtc,

    /// <summary>Version of the confidence scoring algorithm used.</summary>
    string ScoringVersion = "1.0");

/// <summary>
/// Intermediate result from confidence scoring.
/// Used internally by ConfidenceScorer to build EventCandidateV2.
/// </summary>
internal sealed record ConfidenceScoringResult(
    FieldConfidence<string>? Title,
    FieldConfidence<DateTimeOffset>? StartUtc,
    FieldConfidence<DateTimeOffset>? EndUtc,
    FieldConfidence<string>? Venue,
    FieldConfidence<string>? Address,
    FieldConfidence<string>? Category,
    FieldConfidence<string>? Description,
    FieldConfidence<IReadOnlyList<string>>? Tags,
    double OverallConfidence,
    string OverallConfidenceRationale);
