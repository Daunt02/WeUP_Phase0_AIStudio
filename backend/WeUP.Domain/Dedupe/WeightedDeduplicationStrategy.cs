using System.Globalization;
using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Dedupe;

/// <summary>
/// Deterministic, explainable scoring strategy for candidate-to-canonical deduplication.
///
/// Operational intent:
/// - Compare one <see cref="NormalizedEventCandidate"/> to one <see cref="EventAggregateSnapshot"/>.
/// - Produce a complete, auditable <see cref="DuplicateAssessment"/>.
/// - Keep scoring pure and stable: no I/O, no mutable state, no random behavior.
///
/// Downstream resolution services should:
/// 1) Rank competing canonical snapshots by <see cref="MatchScoreBreakdown.CompositeScore"/>.
/// 2) Route on <see cref="DuplicateAssessment.Level"/> and <see cref="DuplicateAssessment.AutoMergeAllowed"/>.
/// 3) Persist <see cref="DuplicateAssessment.Rationale"/> for moderation and audit trails.
/// </summary>
public sealed class WeightedDeduplicationStrategy : IDeduplicationStrategy
{
    // ---------------------------------------------------------------------
    // Explicit score bands (no hidden thresholds)
    // ---------------------------------------------------------------------
    public const double ExactDuplicateThreshold = 0.92;
    public const double ProbableDuplicateThreshold = 0.75;
    public const double PossibleDuplicateThreshold = 0.50;

    // ---------------------------------------------------------------------
    // Canonical dimension weights (must sum to exactly 1.00)
    // ---------------------------------------------------------------------
    public const double TitleWeight = 0.30;
    public const double VenueWeight = 0.25;
    public const double AddressWeight = 0.15;
    public const double TemporalWeight = 0.15;
    public const double GeoWeight = 0.10;
    public const double SourceHashWeight = 0.05;

    // ---------------------------------------------------------------------
    // Hard blocker boundaries
    // ---------------------------------------------------------------------
    public const double HardTemporalDeviationMinutes = 720.0;
    public const double HardGeoDistanceMeters = 10_000.0;

    private const double TextAnchorMinimum = 0.25;
    private const double SourceConflictTitleMinimum = 0.50;

    // Informational activation threshold for ActiveSignals (non-scoring).
    private const double ActiveSignalThreshold = 0.50;

    static WeightedDeduplicationStrategy()
    {
        var weightSum = TitleWeight + VenueWeight + AddressWeight + TemporalWeight + GeoWeight + SourceHashWeight;
        if (Math.Abs(weightSum - 1.0) > 0.0000001)
        {
            throw new InvalidOperationException("Deduplication weights must sum to exactly 1.00.");
        }
    }

    public DuplicateAssessment Assess(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(canonical);

        var rationale = new List<string>(capacity: 24);
        var scoringNotes = new List<string>(capacity: 12);

        var titleRaw = BlendedTextSimilarity(incoming.Title, canonical.Title);
        var venueRaw = BlendedTextSimilarity(incoming.VenueName, canonical.VenueName);
        var addressRaw = BlendedTextSimilarity(incoming.Address, canonical.Address);

        var titleScore = CreateDimensionScore(
            MatchDimension.TitleSimilarity,
            titleRaw,
            TitleWeight,
            $"title_similarity(raw={titleRaw:F4}, method=0.6*levenshtein+0.4*jaccard)");

        var venueScore = CreateDimensionScore(
            MatchDimension.VenueSimilarity,
            venueRaw,
            VenueWeight,
            $"venue_similarity(raw={venueRaw:F4}, method=0.6*levenshtein+0.4*jaccard)");

        var addressScore = CreateDimensionScore(
            MatchDimension.AddressSimilarity,
            addressRaw,
            AddressWeight,
            $"address_similarity(raw={addressRaw:F4}, method=0.6*levenshtein+0.4*jaccard)");

        var incomingStartParsed = TryParseUtc(incoming.StartUtc, out var incomingStart);
        var canonicalStartParsed = TryParseUtc(canonical.StartUtc, out var canonicalStart);
        var hasTemporalPair = incomingStartParsed && canonicalStartParsed;

        var temporalRaw = 0.0;
        var temporalDeltaMinutes = double.NaN;
        if (hasTemporalPair)
        {
            temporalDeltaMinutes = Math.Abs((incomingStart - canonicalStart).TotalMinutes);
            temporalRaw = ComputeTemporalRawScore(temporalDeltaMinutes);
        }
        else
        {
            scoringNotes.Add("temporal_missing_one_or_both_start_values");
        }

        var temporalScore = CreateDimensionScore(
            MatchDimension.TemporalOverlap,
            temporalRaw,
            TemporalWeight,
            hasTemporalPair
                ? $"temporal_overlap(delta_minutes={temporalDeltaMinutes:F2}, raw={temporalRaw:F4}, breakpoints=[30,120,360,720])"
                : "temporal_overlap(raw=0.0000, reason=unparseable_start_utc)");

        var incomingGeoParsed = TryGetIncomingCoordinates(incoming, out var inLat, out var inLon);
        var canonicalGeoParsed = TryGetCanonicalCoordinates(canonical, out var canLat, out var canLon);
        var hasGeoPair = incomingGeoParsed && canonicalGeoParsed;

        var geoRaw = 0.0;
        var geoDistanceMeters = double.NaN;
        if (hasGeoPair)
        {
            geoDistanceMeters = ComputeHaversineMeters(inLat, inLon, canLat, canLon);
            geoRaw = ComputeGeoRawScore(geoDistanceMeters);
        }
        else
        {
            scoringNotes.Add("geo_missing_or_invalid_coordinates");
        }

        var geoScore = CreateDimensionScore(
            MatchDimension.GeoProximity,
            geoRaw,
            GeoWeight,
            hasGeoPair
                ? $"geo_proximity(distance_meters={geoDistanceMeters:F2}, raw={geoRaw:F4}, breakpoints=[100,500,2000,10000])"
                : "geo_proximity(raw=0.0000, reason=missing_or_invalid_coordinates)");

        var sourceScoreData = ComputeSourceEvidenceScore(incoming, canonical);
        var sourceHashScore = CreateDimensionScore(
            MatchDimension.SourceHashEvidence,
            sourceScoreData.Raw,
            SourceHashWeight,
            sourceScoreData.Explanation);

        var compositeScore = Round6(
            titleScore.WeightedContribution +
            venueScore.WeightedContribution +
            addressScore.WeightedContribution +
            temporalScore.WeightedContribution +
            geoScore.WeightedContribution +
            sourceHashScore.WeightedContribution);

        var activeSignals = CollectActiveSignals(
            titleScore,
            venueScore,
            addressScore,
            temporalScore,
            geoScore,
            sourceHashScore);

        var level = ClassifyLevel(compositeScore, sourceScoreData.IsDeterministicIdentity);
        var blockers = EvaluateBlockers(
            canonical,
            titleRaw,
            venueRaw,
            hasTemporalPair,
            temporalDeltaMinutes,
            hasGeoPair,
            geoDistanceMeters,
            incoming.ExternalSourceId,
            canonical.ExternalSourceId);

        var blockerKinds = blockers.Select(static b => b.Kind).ToArray();
        var blockerExplanations = blockers.Select(static b => b.Explanation).ToArray();
        var safetyVerdict = new MergeSafetyVerdict(
            AutoMergeAllowed: blockerKinds.Length == 0,
            ActiveBlockers: blockerKinds,
            BlockerExplanations: blockerExplanations);

        var autoMergeAllowed =
            safetyVerdict.AutoMergeAllowed &&
            (level == DuplicateAssessmentLevel.ProbableDuplicate || level == DuplicateAssessmentLevel.ExactDuplicate);

        rationale.Add($"composite_score={compositeScore:F6}");
        rationale.Add($"classification={level}");
        rationale.Add($"auto_merge_allowed={autoMergeAllowed}");
        rationale.Add(titleScore.Explanation);
        rationale.Add(venueScore.Explanation);
        rationale.Add(addressScore.Explanation);
        rationale.Add(temporalScore.Explanation);
        rationale.Add(geoScore.Explanation);
        rationale.Add(sourceHashScore.Explanation);

        if (blockerExplanations.Length > 0)
        {
            foreach (var explanation in blockerExplanations)
            {
                rationale.Add($"blocker={explanation}");
            }
        }

        if (level == DuplicateAssessmentLevel.Distinct)
        {
            rationale.Add("distinct_outcome=affirmative_non_match_not_residual");
        }

        if (!hasTemporalPair)
        {
            scoringNotes.Add("low_confidence_temporal_dimension_unavailable");
        }

        var breakdown = new MatchScoreBreakdown(
            CompositeScore: compositeScore,
            TitleScore: titleScore,
            VenueScore: venueScore,
            AddressScore: addressScore,
            TemporalScore: temporalScore,
            GeoScore: geoScore,
            SourceHashScore: sourceHashScore,
            ActiveSignals: activeSignals,
            ScoringNotes: scoringNotes.Distinct(StringComparer.Ordinal).ToArray());

        return new DuplicateAssessment(
            CandidateSourceRef: incoming.SourceRef,
            CanonicalEventId: canonical.CanonicalEventId,
            Level: level,
            Breakdown: breakdown,
            SafetyVerdict: safetyVerdict,
            AutoMergeAllowed: autoMergeAllowed,
            Rationale: rationale.ToArray(),
            AssessedAtUtc: DateTimeOffset.UtcNow);
    }

    private static DuplicateAssessmentLevel ClassifyLevel(double compositeScore, bool deterministicIdentity)
    {
        if (deterministicIdentity)
        {
            return DuplicateAssessmentLevel.ExactDuplicate;
        }

        if (compositeScore >= ExactDuplicateThreshold)
        {
            return DuplicateAssessmentLevel.ExactDuplicate;
        }

        if (compositeScore >= ProbableDuplicateThreshold)
        {
            return DuplicateAssessmentLevel.ProbableDuplicate;
        }

        if (compositeScore >= PossibleDuplicateThreshold)
        {
            return DuplicateAssessmentLevel.PossibleDuplicate;
        }

        // Distinct is an explicit, first-class classification branch.
        return DuplicateAssessmentLevel.Distinct;
    }

    private static DimensionScore CreateDimensionScore(
        MatchDimension dimension,
        double rawScore,
        double weight,
        string explanation)
    {
        var clampedRaw = Clamp01(rawScore);
        var roundedRaw = Round4(clampedRaw);
        var weighted = Round6(roundedRaw * weight);

        return new DimensionScore(
            Dimension: dimension,
            RawScore: roundedRaw,
            Weight: weight,
            WeightedContribution: weighted,
            Explanation: explanation);
    }

    private static string[] CollectActiveSignals(params DimensionScore[] scores)
    {
        var active = new List<string>(scores.Length);
        foreach (var score in scores)
        {
            if (score.RawScore >= ActiveSignalThreshold)
            {
                active.Add(score.Dimension.ToString());
            }
        }

        return active.ToArray();
    }

    private static List<BlockerEntry> EvaluateBlockers(
        EventAggregateSnapshot canonical,
        double titleRaw,
        double venueRaw,
        bool hasTemporalPair,
        double temporalDeltaMinutes,
        bool hasGeoPair,
        double geoDistanceMeters,
        string? incomingExternalSourceId,
        string? canonicalExternalSourceId)
    {
        var blockers = new List<BlockerEntry>(capacity: 6);

        if (canonical.IsApproved)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.ApprovedEventProtection,
                "canonical_is_approved_requires_human_review"));
        }

        if (!hasTemporalPair)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.InsufficientTemporalData,
                "missing_parseable_start_utc_on_candidate_or_canonical"));
        }
        else if (temporalDeltaMinutes > HardTemporalDeviationMinutes)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.TemporalDeviationExceedsLimit,
                $"temporal_delta_minutes={temporalDeltaMinutes:F2}_exceeds_{HardTemporalDeviationMinutes:F0}"));
        }

        if (hasGeoPair && geoDistanceMeters > HardGeoDistanceMeters)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.GeoDistanceExceedsLimit,
                $"geo_distance_meters={geoDistanceMeters:F2}_exceeds_{HardGeoDistanceMeters:F0}"));
        }

        if (titleRaw < TextAnchorMinimum && venueRaw < TextAnchorMinimum)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.BothTextAnchorsTooLow,
                $"title_raw={titleRaw:F4}_and_venue_raw={venueRaw:F4}_below_{TextAnchorMinimum:F2}"));
        }

        if (IsSameExternalId(incomingExternalSourceId, canonicalExternalSourceId) && titleRaw < SourceConflictTitleMinimum)
        {
            blockers.Add(new BlockerEntry(
                MergeBlockerKind.SourceIdentityConflict,
                $"same_external_source_id_with_title_raw={titleRaw:F4}_below_{SourceConflictTitleMinimum:F2}"));
        }

        return blockers;
    }

    private static bool IsSameExternalId(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            left.Trim(),
            right.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static SourceEvidenceScore ComputeSourceEvidenceScore(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical)
    {
        var sourceRefExact = HasExactSourceRefMatch(incoming.SourceRef, canonical.SourceRefs);
        var externalSourceExact = IsSameExternalId(incoming.ExternalSourceId, canonical.ExternalSourceId);

        var incomingEvidence = ToNormalizedSet(incoming.EvidenceRefs);
        var canonicalEvidence = ToNormalizedSet(canonical.EvidenceRefs);

        var evidenceOverlapRatio = ComputeSetOverlapRatio(incomingEvidence, canonicalEvidence);
        var raw = sourceRefExact || externalSourceExact ? 1.0 : evidenceOverlapRatio;

        var deterministicIdentity = sourceRefExact || externalSourceExact;
        var explanation =
            $"source_hash_evidence(raw={Clamp01(raw):F4}, source_ref_exact={sourceRefExact}, " +
            $"external_source_exact={externalSourceExact}, evidence_overlap_ratio={evidenceOverlapRatio:F4})";

        return new SourceEvidenceScore(
            Raw: Clamp01(raw),
            IsDeterministicIdentity: deterministicIdentity,
            Explanation: explanation);
    }

    private static bool HasExactSourceRefMatch(string candidateSourceRef, string[] canonicalSourceRefs)
    {
        if (string.IsNullOrWhiteSpace(candidateSourceRef))
        {
            return false;
        }

        var normalized = NormalizeScalar(candidateSourceRef);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var sourceRef in canonicalSourceRefs)
        {
            if (NormalizeScalar(sourceRef) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> ToNormalizedSet(string[]? values)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (values is null)
        {
            return set;
        }

        foreach (var value in values)
        {
            var normalized = NormalizeScalar(value);
            if (normalized.Length > 0)
            {
                set.Add(normalized);
            }
        }

        return set;
    }

    private static double ComputeSetOverlapRatio(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0.0;
        }

        var intersectionCount = 0;
        foreach (var value in left)
        {
            if (right.Contains(value))
            {
                intersectionCount++;
            }
        }

        if (intersectionCount == 0)
        {
            return 0.0;
        }

        var unionCount = left.Count + right.Count - intersectionCount;
        if (unionCount <= 0)
        {
            return 0.0;
        }

        return (double)intersectionCount / unionCount;
    }

    private static double BlendedTextSimilarity(string? left, string? right)
    {
        var normalizedLeft = NormalizeText(left);
        var normalizedRight = NormalizeText(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0.0;
        }

        if (normalizedLeft == normalizedRight)
        {
            return 1.0;
        }

        var levenshtein = NormalizedLevenshteinSimilarity(normalizedLeft, normalizedRight);
        var jaccard = TokenJaccardSimilarity(normalizedLeft, normalizedRight);

        return Clamp01((0.6 * levenshtein) + (0.4 * jaccard));
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.ToLowerInvariant().Trim();
        var chars = new char[lower.Length];
        var idx = 0;
        var previousWasSpace = false;

        for (var i = 0; i < lower.Length; i++)
        {
            var c = lower[i];
            if (char.IsLetterOrDigit(c))
            {
                chars[idx++] = c;
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                chars[idx++] = ' ';
                previousWasSpace = true;
            }
        }

        var normalized = new string(chars, 0, idx).Trim();
        return normalized;
    }

    private static string NormalizeScalar(string? value) => NormalizeText(value);

    private static double NormalizedLevenshteinSimilarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0.0;
        }

        var distance = LevenshteinDistance(left, right);
        var maxLen = Math.Max(left.Length, right.Length);
        if (maxLen == 0)
        {
            return 1.0;
        }

        return Clamp01(1.0 - ((double)distance / maxLen));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + substitutionCost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    private static double TokenJaccardSimilarity(string left, string right)
    {
        var leftTokens = SplitTokens(left);
        var rightTokens = SplitTokens(right);

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0.0;
        }

        var intersection = 0;
        foreach (var token in leftTokens)
        {
            if (rightTokens.Contains(token))
            {
                intersection++;
            }
        }

        if (intersection == 0)
        {
            return 0.0;
        }

        var union = leftTokens.Count + rightTokens.Count - intersection;
        return union <= 0 ? 0.0 : (double)intersection / union;
    }

    private static HashSet<string> SplitTokens(string normalizedValue)
    {
        var tokens = normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new HashSet<string>(tokens, StringComparer.Ordinal);
    }

    private static double ComputeTemporalRawScore(double deltaMinutes)
    {
        if (deltaMinutes <= 30.0)
        {
            return 1.0;
        }

        if (deltaMinutes <= 120.0)
        {
            return 0.75;
        }

        if (deltaMinutes <= 360.0)
        {
            return 0.40;
        }

        if (deltaMinutes <= HardTemporalDeviationMinutes)
        {
            return 0.20;
        }

        return 0.0;
    }

    private static double ComputeGeoRawScore(double distanceMeters)
    {
        if (distanceMeters <= 100.0)
        {
            return 1.0;
        }

        if (distanceMeters <= 500.0)
        {
            return 0.75;
        }

        if (distanceMeters <= 2_000.0)
        {
            return 0.40;
        }

        if (distanceMeters <= HardGeoDistanceMeters)
        {
            return 0.15;
        }

        return 0.0;
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed);
    }

    private static bool TryGetCanonicalCoordinates(EventAggregateSnapshot snapshot, out double latitude, out double longitude)
    {
        latitude = snapshot.Latitude;
        longitude = snapshot.Longitude;

        if (!IsValidLatitude(latitude) || !IsValidLongitude(longitude))
        {
            return false;
        }

        return !(Math.Abs(latitude) < 0.0000001 && Math.Abs(longitude) < 0.0000001);
    }

    private static bool TryGetIncomingCoordinates(NormalizedEventCandidate candidate, out double latitude, out double longitude)
    {
        latitude = default;
        longitude = default;

        if (candidate.Attributes is null || candidate.Attributes.Count == 0)
        {
            return false;
        }

        var latFound = TryGetNumericAttribute(candidate.Attributes, out latitude, "latitude", "lat");
        var lonFound = TryGetNumericAttribute(candidate.Attributes, out longitude, "longitude", "lng", "lon");

        return latFound && lonFound && IsValidLatitude(latitude) && IsValidLongitude(longitude);
    }

    private static bool TryGetNumericAttribute(
        IReadOnlyDictionary<string, string?> attributes,
        out double value,
        params string[] possibleKeys)
    {
        value = default;

        foreach (var key in possibleKeys)
        {
            if (!attributes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6_371_000.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) *
            Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180.0);

    private static bool IsValidLatitude(double value) => value is >= -90.0 and <= 90.0;

    private static bool IsValidLongitude(double value) => value is >= -180.0 and <= 180.0;

    private static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static double Round6(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static double Clamp01(double value)
    {
        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }

    private sealed record SourceEvidenceScore(double Raw, bool IsDeterministicIdentity, string Explanation);

    private sealed record BlockerEntry(MergeBlockerKind Kind, string Explanation);
}