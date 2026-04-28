using System.Globalization;
using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Dedupe;

/// <summary>
/// M2-P09: MergePlanner Implementation — Field Precedence and Conflict Detection v1.0
///
/// Deterministic, auditable merge planning service that:
/// 1. Takes a DuplicateAssessment and produces a comprehensive MergePlanDetail.
/// 2. Applies field-level conflict detection across temporal, geographic, and text dimensions.
/// 3. Generates explicit rationale for every field decision.
/// 4. Preserves complete provenance in audit trail.
/// 5. Separates planning from execution (this is pure planning logic).
///
/// DECISION LAYERS (applied in order):
/// ─────────────────────────────────────────────
/// Layer 1: Safety Overrides
///   If canonical.IsApproved = true → RequireReview for field mutation
///
/// Layer 2: Conflict Detection
///   - Temporal delta > 720min → ConflictType.TemporalDeviation → RequireReview
///   - Geo distance > 10km → ConflictType.GeoDeviation → RequireReview  
///   - Title divergence > 25% → ConflictType.TitleDivergence → RequireReview
///   - Venue divergence > 25% → ConflictType.VenueDivergence → RequireReview
///   - Address divergence > 40% → ConflictType.LocationConflict → RequireReview
///
/// Layer 3: Value Comparison
///   - Both null → KeepExisting (no change)
///   - Only canonical → KeepExisting
///   - Only incoming → ReplaceWithCandidate
///   - Identical → KeepExisting
///   - Different → check confidence gap
///
/// Layer 4: Confidence Weighting
///   - If incoming confidence >> canonical (+10%) → ReplaceWithCandidate
///   - Otherwise → KeepExisting (conservative default)
///
/// Layer 5: Non-Critical Field Merging
///   - Tags, descriptions → MergeValues (union/append)
///
/// RESULT DETERMINATION:
/// ─────────────────────
/// AutoMergeAllowed = all actions ∈ {KeepExisting, ReplaceWithCandidate, MergeValues}
///                     AND no safety blockers active
///                     AND (canonical.IsApproved = false OR all actions = KeepExisting)
/// RequiresManualReview = any action ∈ {RequireReview} OR conflicts.Count > 0
/// RejectMerge = any action ∈ {RejectMerge}
/// </summary>
public sealed class MergePlanner : IMergePlanner
{
    // Thresholds for conflict detection
    private const double TextDivergenceThreshold = 0.25;           // 25%
    private const double TemporalDivergenceThresholdMinutes = 720.0; // 12 hours
    private const double GeoDivergenceThresholdMeters = 10_000.0;  // 10 km
    private const double ConfidenceGapThresholdForReplacement = 0.10;

    public MergePlanDetail CreatePlan(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical,
        DuplicateAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(assessment);

        var rationale = new List<string>(capacity: 32);
        var conflicts = new List<ConflictDescriptor>(capacity: 8);
        var decisions = new Dictionary<string, MergeFieldDecision>(StringComparer.Ordinal);
        var appliedRules = new List<string>(capacity: 12);

        // Extract safety signals
        var hasApprovedProtection = canonical.IsApproved;
        var hasSafetyBlockers = assessment.SafetyVerdict.ActiveBlockers.Length > 0;

        rationale.Add($"assessment_level={assessment.Level}");
        rationale.Add($"auto_merge_allowed_by_assessment={assessment.AutoMergeAllowed}");
        rationale.Add($"canonical_is_approved={hasApprovedProtection}");

        // ─────────────────────────────────────────────────────────────────────
        // Layer 1: Safety Override — Approved Event Protection
        // ─────────────────────────────────────────────────────────────────────
        var approvedEventAction = hasApprovedProtection ? MergeFieldAction.RequireReview : MergeFieldAction.KeepExisting;
        if (hasApprovedProtection)
        {
            appliedRules.Add("rule_approved_protection");
            rationale.Add("approved_event_mutation_requires_review");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Layer 2: Conflict Detection
        // ─────────────────────────────────────────────────────────────────────
        var (hasTemporalConflict, temporalConflictDesc) = DetectTemporalConflict(canonical, incoming, rationale, appliedRules);
        if (hasTemporalConflict && temporalConflictDesc != null)
            conflicts.Add(temporalConflictDesc);

        var (hasGeoConflict, geoConflictDesc) = DetectGeoConflict(canonical, incoming, rationale, appliedRules);
        if (hasGeoConflict && geoConflictDesc != null)
            conflicts.Add(geoConflictDesc);

        var (hasTitleConflict, titleConflictDesc) = DetectTitleConflict(canonical, incoming, rationale, appliedRules);
        if (hasTitleConflict && titleConflictDesc != null)
            conflicts.Add(titleConflictDesc);

        var (hasVenueConflict, venueConflictDesc) = DetectVenueConflict(canonical, incoming, rationale, appliedRules);
        if (hasVenueConflict && venueConflictDesc != null)
            conflicts.Add(venueConflictDesc);

        var (hasAddressConflict, addressConflictDesc) = DetectAddressConflict(canonical, incoming, rationale, appliedRules);
        if (hasAddressConflict && addressConflictDesc != null)
            conflicts.Add(addressConflictDesc);

        // ─────────────────────────────────────────────────────────────────────
        // Layer 3–5: Field Decisions
        // ─────────────────────────────────────────────────────────────────────

        decisions["Title"] = DecideField(
            "Title", canonical.Title, incoming.Title, incoming.ExtractionConfidence, 0.8,
            hasTitleConflict, approvedEventAction, rationale);

        decisions["VenueName"] = DecideField(
            "VenueName", canonical.VenueName, incoming.VenueName, 0.85, 0.85,
            hasVenueConflict, approvedEventAction, rationale);

        decisions["Address"] = DecideField(
            "Address", canonical.Address, incoming.Address, incoming.GeocodeConfidence, 0.9,
            hasAddressConflict, approvedEventAction, rationale);

        decisions["StartUtc"] = DecideTemporalField(
            "StartUtc", canonical.StartUtc, incoming.StartUtc, incoming.TemporalConfidence,
            hasTemporalConflict, approvedEventAction, rationale);

        decisions["EndUtc"] = DecideTemporalField(
            "EndUtc", canonical.EndUtc, incoming.EndUtc, incoming.TemporalConfidence,
            hasTemporalConflict, approvedEventAction, rationale);

        decisions["Timezone"] = DecideField(
            "Timezone", canonical.Timezone, incoming.Timezone, 0.95, 0.95,
            false, approvedEventAction, rationale);

        decisions["Category"] = DecideField(
            "Category", canonical.Category, incoming.Category, 0.8, 0.8,
            false, approvedEventAction, rationale);

        decisions["Description"] = new MergeFieldDecision(
            FieldName: "Description",
            Action: MergeFieldAction.MergeValues,
            ResultValue: incoming.Description ?? canonical.Title,
            Rationale: "Descriptions merged if incoming provides additional detail.",
            Conflict: null);

        decisions["Tags"] = new MergeFieldDecision(
            FieldName: "Tags",
            Action: MergeFieldAction.MergeValues,
            ResultValue: "merged_tags",
            Rationale: "Tags are unioned: both sets preserved for full categorization.",
            Conflict: null);

        // ─────────────────────────────────────────────────────────────────────
        // Determine final merge plan status
        // ─────────────────────────────────────────────────────────────────────
        var hasRequireReview = decisions.Values.Any(d => d.Action == MergeFieldAction.RequireReview);
        var hasRejectMerge = decisions.Values.Any(d => d.Action == MergeFieldAction.RejectMerge);
        var autoMergeAllowed = !hasRequireReview && !hasRejectMerge && !hasApprovedProtection && assessment.AutoMergeAllowed;
        var requiresManualReview = hasRequireReview || conflicts.Count > 0 || hasApprovedProtection || hasSafetyBlockers || !assessment.AutoMergeAllowed;

        appliedRules.Add(autoMergeAllowed ? "rule_auto_merge_safe" : "rule_manual_review_required");

        rationale.Add($"field_decisions_made={decisions.Count}");
        rationale.Add($"conflicts_detected={conflicts.Count}");
        rationale.Add($"final_auto_merge_allowed={autoMergeAllowed}");

        var manualReviewReasons = new List<string>();
        if (hasApprovedProtection)
            manualReviewReasons.Add("Canonical event is approved; mutation requires authorization.");
        if (!assessment.AutoMergeAllowed)
            manualReviewReasons.Add("Dedup assessment did not authorize automatic merge.");
        if (conflicts.Count > 0)
            manualReviewReasons.Add($"Detected {conflicts.Count} field conflict(s) requiring review.");
        if (hasRequireReview)
            manualReviewReasons.Add("One or more critical fields marked RequireReview.");
        if (hasSafetyBlockers)
            manualReviewReasons.Add($"Assessment identified {assessment.SafetyVerdict.ActiveBlockers.Length} safety blocker(s).");

        var audit = new MergePlanAudit(
            Assessment: assessment,
            IncomingCandidate: incoming,
            CanonicalSnapshot: canonical,
            AppliedPrecedenceRules: appliedRules.ToArray(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            MergePlannerVersion: "1.0");

        return new MergePlanDetail(
            CanonicalEventId: canonical.CanonicalEventId,
            CandidateSourceRef: incoming.SourceRef,
            FieldDecisions: decisions,
            Conflicts: conflicts.ToArray(),
            AutoMergeAllowed: autoMergeAllowed,
            RequiresManualReview: requiresManualReview,
            RejectMerge: hasRejectMerge,
            MergeRationale: rationale.ToArray(),
            ManualReviewReasons: manualReviewReasons.ToArray(),
            Audit: audit);
    }

    private static (bool, ConflictDescriptor?) DetectTemporalConflict(
        EventAggregateSnapshot canonical, NormalizedEventCandidate incoming,
        List<string> rationale, List<string> appliedRules)
    {
        if (!TryParseUtc(incoming.StartUtc, out var incomingStart) ||
            !TryParseUtc(canonical.StartUtc, out var canonicalStart))
        {
            rationale.Add("temporal_conflict_detection=insufficient_data");
            return (false, null);
        }

        var delta = Math.Abs((incomingStart - canonicalStart).TotalMinutes);
        rationale.Add($"temporal_delta_minutes={delta:F1}");

        if (delta > TemporalDivergenceThresholdMinutes)
        {
            appliedRules.Add("rule_temporal_conflict_detected");
            var conflict = new ConflictDescriptor(
                FieldName: "StartUtc",
                Type: ConflictType.TemporalDeviation,
                CanonicalValue: canonicalStart.ToString("O"),
                CandidateValue: incomingStart.ToString("O"),
                DeltaMetric: delta,
                Explanation: $"Start times differ by {delta / 60.0:F1} hours.",
                BlockageReason: $"Temporal delta ({delta:F0}min) exceeds threshold (720min).",
                ResolutionHint: "Verify time zone and date conversions match.");
            return (true, conflict);
        }

        return (false, null);
    }

    private static (bool, ConflictDescriptor?) DetectGeoConflict(
        EventAggregateSnapshot canonical, NormalizedEventCandidate incoming,
        List<string> rationale, List<string> appliedRules)
    {
        var (incomingLat, incomingLng) = ExtractCoordinates(incoming);
        if (!incomingLat.HasValue || !incomingLng.HasValue || (canonical.Latitude == 0 && canonical.Longitude == 0))
        {
            rationale.Add("geo_conflict_detection=missing_coordinates");
            return (false, null);
        }

        var distanceMeters = HaversineDistance(canonical.Latitude, canonical.Longitude, incomingLat.Value, incomingLng.Value);
        rationale.Add($"geo_distance_meters={distanceMeters:F1}");

        if (distanceMeters > GeoDivergenceThresholdMeters)
        {
            appliedRules.Add("rule_geo_conflict_detected");
            var conflict = new ConflictDescriptor(
                FieldName: "Geo",
                Type: ConflictType.GeoDeviation,
                CanonicalValue: $"({canonical.Latitude:F6}, {canonical.Longitude:F6})",
                CandidateValue: $"({incomingLat:F6}, {incomingLng:F6})",
                DeltaMetric: distanceMeters,
                Explanation: $"Coordinates differ by {distanceMeters / 1000.0:F1} km.",
                BlockageReason: $"Geographic distance ({distanceMeters / 1000.0:F1} km) exceeds threshold (10 km).",
                ResolutionHint: "Check for geocoding errors or multiple venue branches.");
            return (true, conflict);
        }

        return (false, null);
    }

    private static (bool, ConflictDescriptor?) DetectTitleConflict(
        EventAggregateSnapshot canonical, NormalizedEventCandidate incoming,
        List<string> rationale, List<string> appliedRules)
    {
        if (string.IsNullOrEmpty(incoming.Title) || string.IsNullOrEmpty(canonical.Title))
            return (false, null);

        var similarity = TextSimilarity(canonical.Title, incoming.Title);
        var divergence = 1.0 - similarity;
        rationale.Add($"title_similarity={similarity:F4}");

        if (divergence > TextDivergenceThreshold)
        {
            appliedRules.Add("rule_title_conflict_detected");
            var conflict = new ConflictDescriptor(
                FieldName: "Title",
                Type: ConflictType.TitleDivergence,
                CanonicalValue: canonical.Title,
                CandidateValue: incoming.Title,
                DeltaMetric: divergence,
                Explanation: $"Titles differ by {divergence * 100.0:F1}%.",
                BlockageReason: $"Title divergence ({divergence * 100.0:F1}%) exceeds threshold (25%).",
                ResolutionHint: "Verify titles refer to the same event.");
            return (true, conflict);
        }

        return (false, null);
    }

    private static (bool, ConflictDescriptor?) DetectVenueConflict(
        EventAggregateSnapshot canonical, NormalizedEventCandidate incoming,
        List<string> rationale, List<string> appliedRules)
    {
        if (string.IsNullOrEmpty(incoming.VenueName) || string.IsNullOrEmpty(canonical.VenueName))
            return (false, null);

        var similarity = TextSimilarity(canonical.VenueName, incoming.VenueName);
        var divergence = 1.0 - similarity;
        rationale.Add($"venue_similarity={similarity:F4}");

        if (divergence > TextDivergenceThreshold)
        {
            appliedRules.Add("rule_venue_conflict_detected");
            var conflict = new ConflictDescriptor(
                FieldName: "VenueName",
                Type: ConflictType.VenueDivergence,
                CanonicalValue: canonical.VenueName,
                CandidateValue: incoming.VenueName,
                DeltaMetric: divergence,
                Explanation: $"Venue names differ by {divergence * 100.0:F1}%.",
                BlockageReason: $"Venue divergence ({divergence * 100.0:F1}%) exceeds threshold (25%).",
                ResolutionHint: "Check for name variations or different branches.");
            return (true, conflict);
        }

        return (false, null);
    }

    private static (bool, ConflictDescriptor?) DetectAddressConflict(
        EventAggregateSnapshot canonical, NormalizedEventCandidate incoming,
        List<string> rationale, List<string> appliedRules)
    {
        if (string.IsNullOrEmpty(incoming.Address) || string.IsNullOrEmpty(canonical.Address))
            return (false, null);

        var similarity = TextSimilarity(canonical.Address, incoming.Address);
        var divergence = 1.0 - similarity;
        rationale.Add($"address_similarity={similarity:F4}");

        if (divergence > 0.40)
        {
            appliedRules.Add("rule_address_conflict_detected");
            var conflict = new ConflictDescriptor(
                FieldName: "Address",
                Type: ConflictType.LocationConflict,
                CanonicalValue: canonical.Address,
                CandidateValue: incoming.Address,
                DeltaMetric: divergence,
                Explanation: $"Addresses differ by {divergence * 100.0:F1}%.",
                BlockageReason: $"Address divergence ({divergence * 100.0:F1}%) suggests location mismatch.",
                ResolutionHint: "Normalize addresses and verify geocoding consistency.");
            return (true, conflict);
        }

        return (false, null);
    }

    private static MergeFieldDecision DecideField(
        string fieldName, string? canonicalValue, string? incomingValue,
        double incomingConfidence, double canonicalConfidence,
        bool hasConflict, MergeFieldAction approvedEventOverride, List<string> rationale)
    {
        if (approvedEventOverride == MergeFieldAction.RequireReview)
        {
            rationale.Add($"{fieldName}_action=require_review(approved_event_protection)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.RequireReview, canonicalValue,
                "Canonical is approved; mutation requires authorization.", null);
        }

        if (hasConflict)
        {
            rationale.Add($"{fieldName}_action=require_review(conflict)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.RequireReview, canonicalValue,
                "Conflict detected; requires manual review.", null);
        }

        if (string.IsNullOrEmpty(canonicalValue) && string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=keep_existing(both_null)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, null,
                "Both values are null.", null);
        }

        if (!string.IsNullOrEmpty(canonicalValue) && string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=keep_existing(incoming_null)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, canonicalValue,
                "Canonical has value; incoming empty.", null);
        }

        if (string.IsNullOrEmpty(canonicalValue) && !string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=replace_with_candidate(canonical_null)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.ReplaceWithCandidate, incomingValue,
                "Canonical empty; use incoming.", null);
        }

        if (string.Equals(canonicalValue, incomingValue, StringComparison.OrdinalIgnoreCase))
        {
            rationale.Add($"{fieldName}_action=keep_existing(identical)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, canonicalValue,
                "Values are identical.", null);
        }

        if ((incomingConfidence - canonicalConfidence) > ConfidenceGapThresholdForReplacement)
        {
            rationale.Add($"{fieldName}_action=replace_with_candidate(higher_confidence)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.ReplaceWithCandidate, incomingValue,
                $"Incoming confidence ({incomingConfidence:F2}) >> canonical ({canonicalConfidence:F2}).", null);
        }

        rationale.Add($"{fieldName}_action=keep_existing(conservative)");
        return new MergeFieldDecision(
            fieldName, MergeFieldAction.KeepExisting, canonicalValue,
            "Conservative approach: retain canonical value.", null);
    }

    private static MergeFieldDecision DecideTemporalField(
        string fieldName, string? canonicalValue, string? incomingValue,
        double incomingConfidence, bool hasConflict, MergeFieldAction approvedEventOverride,
        List<string> rationale)
    {
        if (approvedEventOverride == MergeFieldAction.RequireReview)
        {
            rationale.Add($"{fieldName}_action=require_review(approved)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.RequireReview, canonicalValue,
                "Approved event requires authorization.", null);
        }

        if (hasConflict)
        {
            rationale.Add($"{fieldName}_action=require_review(temporal_conflict)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.RequireReview, canonicalValue,
                "Temporal values differ significantly.", null);
        }

        if (string.IsNullOrEmpty(canonicalValue) && string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=keep_existing(both_null)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, null, "Both null.", null);
        }

        if (!string.IsNullOrEmpty(canonicalValue) && string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=keep_existing");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, canonicalValue, "Canonical has value.", null);
        }

        if (string.IsNullOrEmpty(canonicalValue) && !string.IsNullOrEmpty(incomingValue))
        {
            rationale.Add($"{fieldName}_action=replace_with_candidate");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.ReplaceWithCandidate, incomingValue, "Use incoming.", null);
        }

        if (string.Equals(canonicalValue, incomingValue, StringComparison.Ordinal))
        {
            rationale.Add($"{fieldName}_action=keep_existing(identical)");
            return new MergeFieldDecision(
                fieldName, MergeFieldAction.KeepExisting, canonicalValue, "Identical.", null);
        }

        rationale.Add($"{fieldName}_action=keep_existing(temporal_conservative)");
        return new MergeFieldDecision(
            fieldName, MergeFieldAction.KeepExisting, canonicalValue,
            "Temporal field: conservative approach.", null);
    }

    private static double TextSimilarity(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var lev = 1.0 - (LevenshteinDistance(a, b) / (double)Math.Max(a.Length, b.Length));
        var jac = JaccardSimilarity(a, b);
        return (0.6 * lev) + (0.4 * jac);
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;
        var d = new int[len1 + 1, len2 + 1];

        for (var i = 0; i <= len1; i++) d[i, 0] = i;
        for (var j = 0; j <= len2; j++) d[0, j] = j;

        for (var i = 1; i <= len1; i++)
            for (var j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }

        return d[len1, len2];
    }

    private static double JaccardSimilarity(string a, string b)
    {
        var tokensA = Tokenize(a).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tokensB = Tokenize(b).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var intersection = tokensA.Count(t => tokensB.Contains(t));
        var union = tokensA.Count + tokensB.Count - intersection;
        return union == 0 ? 0.0 : intersection / (double)union;
    }

    private static IEnumerable<string> Tokenize(string s) =>
        System.Text.RegularExpressions.Regex.Split(s, @"[\W_]+").Where(t => !string.IsNullOrEmpty(t));

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static bool TryParseUtc(string? value, out DateTimeOffset result)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = default;
            return false;
        }
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out result);
    }

    private static (double?, double?) ExtractCoordinates(NormalizedEventCandidate candidate)
    {
        if (candidate.Attributes == null)
            return (null, null);
        if (double.TryParse(candidate.Attributes.GetValueOrDefault("Latitude"), out var lat) &&
            double.TryParse(candidate.Attributes.GetValueOrDefault("Longitude"), out var lng))
            return (lat, lng);
        return (null, null);
    }
}
