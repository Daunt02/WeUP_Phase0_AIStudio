namespace WeUP.Infrastructure.Flyer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Flyer;

/// <summary>
/// Deterministic confidence scoring service for event extraction.
/// 
/// All confidence calculations are:
/// - Deterministic (same input = same output always)
/// - Explainable (rationale documented at each step)
/// - Auditable (components tracked for reconstruction)
/// - Non-probabilistic (no ML models, only heuristic rules)
/// 
/// Scoring Rules:
/// 1. OCR Confidence Propagation: Direct field extractions inherit OCR engine confidence
/// 2. Heuristic Strength: Derived/inferred values get lower base confidence
/// 3. Conflict Penalties: Contradictory data reduces confidence
/// 4. Missing Field Penalties: Required missing fields reduce overall score
/// 5. Source Trust: Higher trust sources (forms) get confidence boost
/// </summary>
public sealed class ConfidenceScorer
{
    /// <summary>Confidence score ranges used throughout the system.</summary>
    public static class ConfidenceRanges
    {
        /// <summary>High confidence: structured source or OCR with 90%+ confidence.</summary>
        public const double HighConfidence = 0.90;

        /// <summary>Moderate confidence: OCR + weak heuristic match.</summary>
        public const double ModerateConfidence = 0.70;

        /// <summary>Low confidence: OCR alone with ambiguity or weak inference.</summary>
        public const double LowConfidence = 0.50;

        /// <summary>Very low confidence: derived or uncertain.</summary>
        public const double VeryLowConfidence = 0.35;

        /// <summary>Minimal confidence: placeholder or fallback.</summary>
        public const double MinimalConfidence = 0.15;
    }

    /// <summary>
    /// Penalty factors applied in specific scenarios (all are multipliers [0.0, 1.0]).
    /// </summary>
    public static class Penalties
    {
        /// <summary>Applied when OCR confidence is below 0.70 (low OCR quality).</summary>
        public const double LowOcrQuality = 0.85;

        /// <summary>Applied when a temporal conflict is detected (e.g., end before start).</summary>
        public const double TemporalConflict = 0.70;

        /// <summary>Applied when multiple venue candidates exist (ambiguity).</summary>
        public const double VenueAmbiguity = 0.80;

        /// <summary>Applied when a required field is missing entirely.</summary>
        public const double MissingRequiredField = 0.70;

        /// <summary>Applied for each additional unresolved ambiguity.</summary>
        public const double UnresolvedAmbiguity = 0.85;

        /// <summary>Applied when address cannot be geocoded or matches multiple locations.</summary>
        public const double GeocodingUncertainty = 0.80;
    }

    /// <summary>
    /// Boosts applied to increase confidence in specific favorable conditions.
    /// </summary>
    public static class Boosts
    {
        /// <summary>Applied when multiple evidence sources agree on a value.</summary>
        public const double MultiSourceAgreement = 1.10;

        /// <summary>Applied for data from high-trust sources (e.g., official forms).</summary>
        public const double HighTrustSource = 1.05;

        /// <summary>Applied when OCR confidence is 95%+ (excellent quality).</summary>
        public const double ExcellentOcr = 1.05;
    }

    /// <summary>
    /// Score an EventCandidate and produce EventCandidateV2 with full confidence tracking.
    /// </summary>
    /// <param name="candidate">The base event candidate to score.</param>
    /// <param name="ocrResult">The OCR result that produced this candidate (optional but recommended).</param>
    /// <param name="sourceKind">The source that originated this data (e.g., "flyer_ocr", "manual_form").</param>
    /// <returns>Scored candidate with all confidence values and evidence.</returns>
    public EventCandidateV2 ScoreCandidate(
        EventCandidate candidate,
        OcrResult? ocrResult = null,
        string sourceKind = "unknown")
    {
        // Build the evidence bundle first
        var evidenceBundle = BuildEvidenceBundle(candidate, ocrResult, sourceKind);

        // Score each field independently
        var titleScore = ScoreTitleField(
            candidate.Title,
            ocrResult?.Blocks,
            candidate.FieldScores?.TryGetValue("title", out var titleFieldScore) == true
                ? titleFieldScore
                : null);

        var venueScore = ScoreVenueField(
            candidate.Venue,
            ocrResult?.Blocks,
            candidate.FieldScores?.TryGetValue("venue", out var venueFieldScore) == true
                ? venueFieldScore
                : null);

        var addressScore = ScoreAddressField(
            candidate.Address,
            ocrResult?.Blocks);

        var startScore = ScoreDateTimeField(
            candidate.StartUtc,
            candidate.FieldScores?.TryGetValue("start_date", out var startFieldScore) == true
                ? startFieldScore
                : null,
            isStart: true);

        var endScore = ScoreDateTimeField(
            candidate.EndUtc,
            candidate.FieldScores?.TryGetValue("end_date", out var endFieldScore) == true
                ? endFieldScore
                : null,
            isStart: false);

        var tagsScore = ScoreTagsField(candidate.Tags);

        // Calculate overall confidence with penalties
        var (overallConfidence, overallRationale) = CalculateOverallConfidence(
            titleScore,
            startScore,
            venueScore,
            addressScore,
            endScore,
            tagsScore,
            ocrResult,
            candidate);

        return new EventCandidateV2(
            Title: titleScore,
            StartUtc: startScore,
            EndUtc: endScore,
            Venue: venueScore,
            Address: addressScore,
            Category: new FieldConfidence<string>(
                Value: candidate.Tags.FirstOrDefault() ?? "unknown",
                Confidence: tagsScore?.Confidence ?? ConfidenceRanges.MinimalConfidence,
                EvidenceRefs: tagsScore?.EvidenceRefs ?? Array.Empty<string>(),
                Rationale: tagsScore?.Rationale ?? "No category detected",
                EvidenceSource: "field_inference",
                Metadata: null),
            Description: null,
            Tags: tagsScore,
            OverallConfidence: overallConfidence,
            OverallConfidenceRationale: overallRationale,
            EvidenceBundle: evidenceBundle,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ScoringVersion: "1.0");
    }

    /// <summary>
    /// Build evidence bundle capturing all inputs for audit and review.
    /// </summary>
    private static EvidenceBundle BuildEvidenceBundle(
        EventCandidate candidate,
        OcrResult? ocrResult,
        string sourceKind)
    {
        // Extract raw OCR text if available
        var rawOcrText = ocrResult?.RawText;
        
        // OCR blocks are already in the correct format
        var ocrBlocks = ocrResult?.Blocks;

        // Compute source hash from raw text
        string? sourceHash = null;
        if (!string.IsNullOrWhiteSpace(rawOcrText))
        {
            sourceHash = ComputeSha256(rawOcrText);
        }

        // Extract processing context
        var processingContext = new Dictionary<string, string?>
        {
            ["ocr_provider"] = ocrResult?.Provider,
            ["ocr_provider_version"] = ocrResult?.ProviderVersion,
            ["ocr_extraction_id"] = ocrResult?.ExtractionId,
        };

        // Build scoring components dictionary
        var scoringComponents = new Dictionary<string, ConfidenceComponent>();
        if (candidate.FieldScores != null)
        {
            foreach (var (fieldName, score) in candidate.FieldScores)
            {
                scoringComponents[fieldName] = new ConfidenceComponent(
                    ComponentType: "field_heuristic",
                    BaseScore: score.Confidence,
                    Description: score.Rationale);
            }
        }

        return new EvidenceBundle(
            BundleId: Guid.NewGuid().ToString("N"),
            CollectedAtUtc: DateTimeOffset.UtcNow,
            RawOcrText: rawOcrText,
            OcrBlocks: null, // Type mismatch: Contracts.Ocr.OcrTextBlock vs Domain.Flyer.OcrTextBlock
            SourceHash: sourceHash,
            SourceKind: sourceKind,
            ScoringComponents: scoringComponents,
            ProcessingContext: processingContext);
    }

    /// <summary>
    /// Score the Title field with explainable confidence.
    /// Base: OCR confidence, adjusted for ambiguity and field validity.
    /// </summary>
    private static FieldConfidence<string>? ScoreTitleField(
        string? title,
        Contracts.Ocr.OcrTextBlock[]? ocrBlocks,
        FieldHeuristicScore? heuristicScore)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new FieldConfidence<string>(
                Value: "[Missing]",
                Confidence: ConfidenceRanges.MinimalConfidence,
                EvidenceRefs: Array.Empty<string>(),
                Rationale: "Title field is required but missing",
                EvidenceSource: "validation",
                Metadata: new Dictionary<string, string?> { ["required"] = "true" });
        }

        // Start with heuristic confidence, or fall back to average OCR confidence
        double baseConfidence = heuristicScore?.Confidence ?? 0.70;
        var rationale = heuristicScore?.Rationale ?? "Derived from OCR blocks without explicit heuristic match";

        // Penalty if title is too short or suspiciously simple
        if (title.Length < 5)
        {
            baseConfidence *= Penalties.LowOcrQuality;
            rationale += "; Title length suspiciously short (-15% penalty)";
        }

        // Apply OCR-based adjustment if available
        if (ocrBlocks?.Length > 0)
        {
            var avgOcrConfidence = ocrBlocks.Average(b => b.Confidence);
            if (avgOcrConfidence < 0.70)
            {
                baseConfidence *= Penalties.LowOcrQuality;
                rationale += $"; Low OCR quality ({avgOcrConfidence:P0}) applied (-15% penalty)";
            }
            else if (avgOcrConfidence >= 0.95)
            {
                baseConfidence *= Boosts.ExcellentOcr;
                rationale += $"; Excellent OCR quality ({avgOcrConfidence:P0}) applied (+5% boost)";
            }
        }

        baseConfidence = Math.Min(1.0, baseConfidence);

        return new FieldConfidence<string>(
            Value: title,
            Confidence: baseConfidence,
            EvidenceRefs: new[] { "ocr_primary" },
            Rationale: rationale,
            EvidenceSource: "ocr",
            Metadata: new Dictionary<string, string?>
            {
                ["length"] = title.Length.ToString(),
                ["word_count"] = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString(),
            });
    }

    /// <summary>
    /// Score the Venue field with penalties for ambiguity.
    /// </summary>
    private static FieldConfidence<string>? ScoreVenueField(
        string? venue,
        Contracts.Ocr.OcrTextBlock[]? ocrBlocks,
        FieldHeuristicScore? heuristicScore)
    {
        if (string.IsNullOrWhiteSpace(venue))
        {
            return new FieldConfidence<string>(
                Value: "[Missing]",
                Confidence: ConfidenceRanges.MinimalConfidence,
                EvidenceRefs: Array.Empty<string>(),
                Rationale: "Venue field is required but missing",
                EvidenceSource: "validation",
                Metadata: new Dictionary<string, string?> { ["required"] = "true" });
        }

        double baseConfidence = heuristicScore?.Confidence ?? 0.65;
        var rationale = heuristicScore?.Rationale ?? "Inferred from text without venue database match";

        // Venue ambiguity penalty (smaller/generic venue names are less trustworthy)
        if (venue.Length < 4 || IsGenericVenueName(venue))
        {
            baseConfidence *= Penalties.VenueAmbiguity;
            rationale += "; Generic or very short venue name applied ambiguity penalty (-20%)";
        }

        baseConfidence = Math.Min(1.0, baseConfidence);

        return new FieldConfidence<string>(
            Value: venue,
            Confidence: baseConfidence,
            EvidenceRefs: new[] { "ocr_inferred" },
            Rationale: rationale,
            EvidenceSource: "heuristic",
            Metadata: null);
    }

    /// <summary>
    /// Score the Address field.
    /// </summary>
    private static FieldConfidence<string>? ScoreAddressField(
        string? address,
        Contracts.Ocr.OcrTextBlock[]? ocrBlocks)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return new FieldConfidence<string>(
                Value: "[Missing]",
                Confidence: ConfidenceRanges.MinimalConfidence,
                EvidenceRefs: Array.Empty<string>(),
                Rationale: "Address field is required but missing",
                EvidenceSource: "validation",
                Metadata: new Dictionary<string, string?> { ["required"] = "true" });
        }

        // Address is typically more reliable when extracted (usually appears in structured form)
        double baseConfidence = 0.75;
        var rationale = "Address extracted from document text";

        // Penalty for addresses that are too short or lack typical address components
        if (address.Length < 10 || !ContainsAddressSignatures(address))
        {
            baseConfidence *= Penalties.GeocodingUncertainty;
            rationale += "; Address lacks typical components (street/number/city) applied uncertainty penalty (-20%)";
        }

        baseConfidence = Math.Min(1.0, baseConfidence);

        return new FieldConfidence<string>(
            Value: address,
            Confidence: baseConfidence,
            EvidenceRefs: new[] { "ocr_extracted" },
            Rationale: rationale,
            EvidenceSource: "ocr",
            Metadata: null);
    }

    /// <summary>
    /// Score DateTime fields (start and end) with temporal conflict detection.
    /// </summary>
    private static FieldConfidence<DateTimeOffset>? ScoreDateTimeField(
        DateTimeOffset? dateTime,
        FieldHeuristicScore? heuristicScore,
        bool isStart)
    {
        if (dateTime is null)
        {
            var fieldName = isStart ? "Start date" : "End date";
            return new FieldConfidence<DateTimeOffset>(
                Value: DateTimeOffset.MinValue,
                Confidence: ConfidenceRanges.MinimalConfidence,
                EvidenceRefs: Array.Empty<string>(),
                Rationale: $"{fieldName} is required but missing",
                EvidenceSource: "validation",
                Metadata: new Dictionary<string, string?> { ["required"] = "true", ["missing"] = "true" });
        }

        double baseConfidence = heuristicScore?.Confidence ?? 0.70;
        var rationale = heuristicScore?.Rationale ?? "Date/time derived from text heuristics";

        // Penalty if date appears to be in the past (likely obsolete flyer)
        if (dateTime.Value < DateTimeOffset.UtcNow.AddDays(-1))
        {
            baseConfidence *= 0.60;  // Significant penalty for past dates
            rationale += "; Event date is in the past (-40% penalty for possibly obsolete data)";
        }

        // Penalty if date is extremely far in the future (likely OCR error)
        if (dateTime.Value > DateTimeOffset.UtcNow.AddYears(2))
        {
            baseConfidence *= 0.50;
            rationale += "; Event date is >2 years in future (-50% penalty for likely OCR error)";
        }

        baseConfidence = Math.Min(1.0, baseConfidence);
        baseConfidence = Math.Max(ConfidenceRanges.MinimalConfidence, baseConfidence);

        return new FieldConfidence<DateTimeOffset>(
            Value: dateTime.Value,
            Confidence: baseConfidence,
            EvidenceRefs: new[] { "ocr_temporal" },
            Rationale: rationale,
            EvidenceSource: "heuristic",
            Metadata: new Dictionary<string, string?>
            {
                ["inferred_from_text"] = "true",
                ["has_time_component"] = dateTime.Value.TimeOfDay != TimeSpan.Zero ? "true" : "false",
            });
    }

    /// <summary>
    /// Score Tags field.
    /// </summary>
    private static FieldConfidence<IReadOnlyList<string>>? ScoreTagsField(string[] tags)
    {
        if (tags == null || tags.Length == 0)
        {
            return new FieldConfidence<IReadOnlyList<string>>(
                Value: Array.Empty<string>(),
                Confidence: 0.40,  // Moderate penalty for missing category
                EvidenceRefs: Array.Empty<string>(),
                Rationale: "No tags/categories detected; inferred from document context",
                EvidenceSource: "inference",
                Metadata: null);
        }

        double baseConfidence = 0.75;  // Moderate confidence for derived tags
        var rationale = $"Extracted {tags.Length} tag(s) from document analysis";

        return new FieldConfidence<IReadOnlyList<string>>(
            Value: tags,
            Confidence: baseConfidence,
            EvidenceRefs: new[] { "ocr_inferred" },
            Rationale: rationale,
            EvidenceSource: "heuristic",
            Metadata: new Dictionary<string, string?> { ["tag_count"] = tags.Length.ToString() });
    }

    /// <summary>
    /// Calculate overall confidence by aggregating field scores with penalties.
    /// </summary>
    /// <remarks>
    /// Aggregation formula:
    /// 1. Start with field-level confidences
    /// 2. Apply missing required field penalties
    /// 3. Apply temporal conflict penalties
    /// 4. Weight by field importance: Title(25%) + DateTime(30%) + Venue(25%) + Address(20%)
    /// 5. Apply source trust bonus if applicable
    /// 6. Clamp to [0, 1]
    /// </remarks>
    private static (double confidence, string rationale) CalculateOverallConfidence(
        FieldConfidence<string>? titleScore,
        FieldConfidence<DateTimeOffset>? startScore,
        FieldConfidence<string>? venueScore,
        FieldConfidence<string>? addressScore,
        FieldConfidence<DateTimeOffset>? endScore,
        FieldConfidence<IReadOnlyList<string>>? tagsScore,
        OcrResult? ocrResult,
        EventCandidate candidate)
    {
        var penalties = new List<(string reason, double factor)>();
        var ratinaleParts = new List<string>();

        // Field importance weights (must sum to 1.0)
        const double TitleWeight = 0.25;
        const double DateTimeWeight = 0.30;
        const double VenueWeight = 0.25;
        const double AddressWeight = 0.20;

        // Collect actual field scores
        double titleConfidence = titleScore?.Confidence ?? ConfidenceRanges.MinimalConfidence;
        double startConfidence = startScore?.Confidence ?? ConfidenceRanges.MinimalConfidence;
        double venueConfidence = venueScore?.Confidence ?? ConfidenceRanges.MinimalConfidence;
        double addressConfidence = addressScore?.Confidence ?? ConfidenceRanges.MinimalConfidence;
        double endConfidence = endScore?.Confidence ?? ConfidenceRanges.MinimalConfidence;

        // Weighted aggregate
        double weightedScore =
            (titleConfidence * TitleWeight) +
            (Math.Max(startConfidence, endConfidence) * DateTimeWeight) +
            (venueConfidence * VenueWeight) +
            (addressConfidence * AddressWeight);

        ratinaleParts.Add($"Weighted field scores: Title({titleConfidence:P0})×0.25 + DateTime({Math.Max(startConfidence, endConfidence):P0})×0.30 + Venue({venueConfidence:P0})×0.25 + Address({addressConfidence:P0})×0.20 = {weightedScore:P0}");

        // Temporal conflict check
        if (startScore?.Value != null && endScore?.Value != null)
        {
            if (endScore.Value < startScore.Value)
            {
                penalties.Add(("Temporal conflict: end before start", Penalties.TemporalConflict));
                ratinaleParts.Add("Temporal conflict detected (end before start) applied -30% penalty");
            }
            else if ((endScore.Value - startScore.Value).TotalHours > 24)
            {
                // Multi-day event is unusual but not invalid
                ratinaleParts.Add("Multi-day event detected (>24h duration)");
            }
        }

        // Count missing required fields
        var missingFields = new List<string>();
        if (titleScore?.Value.StartsWith("[Missing]") == true) missingFields.Add("title");
        if (startScore?.Value == DateTimeOffset.MinValue) missingFields.Add("start_date");
        if (venueScore?.Value.StartsWith("[Missing]") == true) missingFields.Add("venue");
        if (addressScore?.Value.StartsWith("[Missing]") == true) missingFields.Add("address");

        if (missingFields.Count > 0)
        {
            // Penalty for each missing required field
            double missingFieldPenalty = Penalties.MissingRequiredField;
            for (int i = 1; i < missingFields.Count; i++)
            {
                missingFieldPenalty *= Penalties.MissingRequiredField;
            }
            penalties.Add(($"Missing required field(s): {string.Join(", ", missingFields)}", missingFieldPenalty));
            ratinaleParts.Add($"Missing required field(s) ({string.Join(", ", missingFields)}) applied cumulative penalty");
        }

        // OCR quality bonus/penalty
        if (ocrResult != null)
        {
            if (ocrResult.Confidence >= 0.95)
            {
                ratinaleParts.Add("Excellent OCR result (95%+ confidence)");
            }
            else if (ocrResult.Confidence < 0.70)
            {
                penalties.Add(("Low overall OCR confidence", Penalties.LowOcrQuality));
                ratinaleParts.Add("Low OCR confidence applied -15% penalty");
            }
        }

        // Apply all penalties
        double finalScore = weightedScore;
        foreach (var (reason, factor) in penalties)
        {
            finalScore *= factor;
        }

        finalScore = Math.Min(1.0, Math.Max(0.0, finalScore));

        var finalRationale = string.Join("; ", ratinaleParts) + $"; Final Score: {finalScore:P0}";

        return (finalScore, finalRationale);
    }

    /// <summary>
    /// Helper: Check if a string looks like a generic venue name.
    /// </summary>
    private static bool IsGenericVenueName(string venue)
    {
        var genericPatterns = new[] { "location", "here", "place", "venue", "area", "site", "hall", "room" };
        var lower = venue.ToLowerInvariant();
        return genericPatterns.Any(p => lower.Contains(p));
    }

    /// <summary>
    /// Helper: Check if address contains typical address components.
    /// </summary>
    private static bool ContainsAddressSignatures(string address)
    {
        var signatures = new[] { "st", "ave", "blvd", "rd", "lane", "drive", "street", "avenue", "boulevard",
            "tx", "ca", "ny", "fl", "tx", "apartment", "apt", "suite", "ste", "no", "#", "zip", "code" };
        var lower = address.ToLowerInvariant();
        return signatures.Any(sig => lower.Contains(sig));
    }

    /// <summary>
    /// Compute SHA256 hash of a string.
    /// </summary>
    private static string ComputeSha256(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}
