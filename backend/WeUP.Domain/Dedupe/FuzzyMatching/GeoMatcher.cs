namespace WeUP.Domain.Dedupe.FuzzyMatching;

/// <summary>
/// Deterministic geospatial duplicate signal matcher.
///
/// Design goals:
/// - Bounded and explainable scoring (score/confidence in [0,1])
/// - Geographic proximity contributes evidence but never guarantees duplicates
/// - Venue/address disagreement suppresses score/confidence
/// - Missing coordinates are explicit negative evidence
/// </summary>
public sealed class GeoMatcher : IGeoMatcher
{
    private static readonly HashSet<string> ComparisonStopTokens = new(StringComparer.Ordinal)
    {
        "the",
        "venue",
    };

    private static readonly IReadOnlyDictionary<string, string> TokenExpansions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["st"] = "street",
            ["rd"] = "road",
            ["blvd"] = "boulevard",
            ["ave"] = "avenue",
        };

    private readonly GeoMatcherThresholds _thresholds;
    private readonly IGeoDistanceCalculator _distanceCalculator;

    public GeoMatcher(
        GeoMatcherThresholds? thresholds = null,
        IGeoDistanceCalculator? distanceCalculator = null)
    {
        _thresholds = thresholds ?? GeoMatcherThresholds.Default;
        _distanceCalculator = distanceCalculator ?? new HaversineGeoDistanceCalculator();
        ValidateThresholds(_thresholds);
    }

    public GeoMatchResult Match(GeoMatchInput left, GeoMatchInput right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var hasLeftCoords = TryGetCoordinates(left, out var leftLat, out var leftLon);
        var hasRightCoords = TryGetCoordinates(right, out var rightLat, out var rightLon);

        var venueAgreement = CompareTextAgreement(left.VenueName, right.VenueName);
        var addressAgreement = CompareTextAgreement(left.Address, right.Address);

        if (!hasLeftCoords || !hasRightCoords)
        {
            var missingConfidence = ComputeMissingCoordinateConfidence(left.GeocodeConfidence, right.GeocodeConfidence);
            return new GeoMatchResult(
                Outcome: GeoMatchOutcome.NoMeaningfulGeoMatch,
                Score: 0.0,
                Confidence: Round4(missingConfidence),
                DistanceMeters: null,
                IsCoordinateMissing: true,
                VenueAgreement: venueAgreement.IsAgreement,
                AddressAgreement: addressAgreement.IsAgreement,
                VenueConflict: venueAgreement.IsConflict,
                AddressConflict: addressAgreement.IsConflict,
                Explanation:
                    "geo(score=0.0000, outcome=no_meaningful_geo_match, reason=missing_or_invalid_coordinates, " +
                    $"left_has_coords={hasLeftCoords}, right_has_coords={hasRightCoords}, " +
                    $"confidence={missingConfidence:F4})");
        }

        var distanceMeters = _distanceCalculator.CalculateMeters(leftLat, leftLon, rightLat, rightLon);
        var (baseScore, baseConfidence, bandName) = ComputeBand(distanceMeters);
        var geocodeFactor = ComputeGeocodeFactor(left.GeocodeConfidence, right.GeocodeConfidence);

        var scoreMultiplier = 1.0;
        var confidenceAdjustment = 0.0;

        if (venueAgreement.IsConflict)
        {
            scoreMultiplier *= 0.65;
            confidenceAdjustment -= 0.20;
        }
        else if (venueAgreement.IsAgreement)
        {
            confidenceAdjustment += 0.08;
        }

        if (addressAgreement.IsConflict)
        {
            scoreMultiplier *= 0.70;
            confidenceAdjustment -= 0.18;
        }
        else if (addressAgreement.IsAgreement)
        {
            confidenceAdjustment += 0.08;
        }

        // Strong dual conflict means closeness is likely same area, not same event.
        if (venueAgreement.IsConflict && addressAgreement.IsConflict)
        {
            scoreMultiplier *= 0.75;
            confidenceAdjustment -= 0.10;
        }

        var score = Clamp01(baseScore * scoreMultiplier * geocodeFactor);
        var confidence = Clamp01((baseConfidence * geocodeFactor) + confidenceAdjustment);
        var outcome = ClassifyOutcome(
            distanceMeters,
            venueAgreement.IsAgreement,
            addressAgreement.IsAgreement,
            venueAgreement.IsConflict,
            addressAgreement.IsConflict,
            score);

        return new GeoMatchResult(
            Outcome: outcome,
            Score: Round4(score),
            Confidence: Round4(confidence),
            DistanceMeters: Round2(distanceMeters),
            IsCoordinateMissing: false,
            VenueAgreement: venueAgreement.IsAgreement,
            AddressAgreement: addressAgreement.IsAgreement,
            VenueConflict: venueAgreement.IsConflict,
            AddressConflict: addressAgreement.IsConflict,
            Explanation:
                $"geo(score={score:F4}, confidence={confidence:F4}, outcome={ToToken(outcome)}, " +
                $"distance_meters={distanceMeters:F2}, band={bandName}, geocode_factor={geocodeFactor:F4}, " +
                $"venue_agree={venueAgreement.IsAgreement}, venue_conflict={venueAgreement.IsConflict}, " +
                $"address_agree={addressAgreement.IsAgreement}, address_conflict={addressAgreement.IsConflict}, " +
                $"thresholds=[exact<={_thresholds.ExactProximityMeters:F0}, near<={_thresholds.NearProximityMeters:F0}, " +
                $"weak<={_thresholds.WeakProximityMeters:F0}, nomatch<={_thresholds.NoMatchMeters:F0}])");
    }

    private (double Score, double Confidence, string BandName) ComputeBand(double distanceMeters)
    {
        if (distanceMeters <= _thresholds.ExactProximityMeters)
        {
            return (_thresholds.ExactBandScore, 0.95, "exact");
        }

        if (distanceMeters <= _thresholds.NearProximityMeters)
        {
            return (_thresholds.NearBandScore, 0.85, "near");
        }

        if (distanceMeters <= _thresholds.WeakProximityMeters)
        {
            return (_thresholds.WeakBandScore, 0.70, "weak");
        }

        if (distanceMeters <= _thresholds.NoMatchMeters)
        {
            return (_thresholds.NoMatchBandScore, 0.45, "no_match_band");
        }

        return (0.0, 0.25, "outside_range");
    }

    private GeoMatchOutcome ClassifyOutcome(
        double distanceMeters,
        bool venueAgreement,
        bool addressAgreement,
        bool venueConflict,
        bool addressConflict,
        double score)
    {
        if (distanceMeters > _thresholds.NoMatchMeters || score <= 0.10)
        {
            return GeoMatchOutcome.NoMeaningfulGeoMatch;
        }

        var hasCoherence = venueAgreement || addressAgreement;
        var hasConflict = venueConflict || addressConflict;

        if (distanceMeters <= _thresholds.ExactProximityMeters && hasCoherence && !hasConflict)
        {
            return GeoMatchOutcome.SameVenueSameCoordinates;
        }

        if (distanceMeters <= _thresholds.WeakProximityMeters && hasConflict)
        {
            return GeoMatchOutcome.NearbyDistinctVenue;
        }

        if (distanceMeters <= _thresholds.WeakProximityMeters)
        {
            return GeoMatchOutcome.NearbyLikelyDuplicate;
        }

        return GeoMatchOutcome.NoMeaningfulGeoMatch;
    }

    private static bool TryGetCoordinates(GeoMatchInput input, out double latitude, out double longitude)
    {
        latitude = default;
        longitude = default;

        if (!input.Latitude.HasValue || !input.Longitude.HasValue)
        {
            return false;
        }

        latitude = input.Latitude.Value;
        longitude = input.Longitude.Value;

        if (latitude is < -90.0 or > 90.0)
        {
            return false;
        }

        if (longitude is < -180.0 or > 180.0)
        {
            return false;
        }

        return true;
    }

    private static TextAgreement CompareTextAgreement(string? left, string? right)
    {
        var normalizedLeft = NormalizeText(left);
        var normalizedRight = NormalizeText(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return TextAgreement.Unknown;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return TextAgreement.Agreement;
        }

        var overlap = TokenOverlapRatio(normalizedLeft, normalizedRight);
        if (overlap >= 0.75)
        {
            return TextAgreement.Agreement;
        }

        if (overlap <= 0.20)
        {
            return TextAgreement.Conflict;
        }

        return TextAgreement.Unknown;
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

        return new string(chars, 0, idx).Trim();
    }

    private static double TokenOverlapRatio(string left, string right)
    {
        var leftTokens = TokenizeForComparison(left);
        var rightTokens = TokenizeForComparison(right);

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

        var denominator = Math.Max(leftTokens.Count, rightTokens.Count);
        return denominator <= 0 ? 0.0 : (double)intersection / denominator;
    }

    private static HashSet<string> TokenizeForComparison(string normalized)
    {
        var rawTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in rawTokens)
        {
            var token = ExpandToken(raw);
            if (token.Length == 0 || ComparisonStopTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static string ExpandToken(string token)
    {
        if (TokenExpansions.TryGetValue(token, out var expanded))
        {
            return expanded;
        }

        return token;
    }

    private static double ComputeGeocodeFactor(double? leftGeocodeConfidence, double? rightGeocodeConfidence)
    {
        var left = Clamp01OrDefault(leftGeocodeConfidence, 0.65);
        var right = Clamp01OrDefault(rightGeocodeConfidence, 0.65);

        // Keep a floor so available coordinates can still participate, but low
        // geocode confidence cannot present as high-certainty evidence.
        return Math.Max(0.40, Math.Min(left, right));
    }

    private static double ComputeMissingCoordinateConfidence(double? leftGeocodeConfidence, double? rightGeocodeConfidence)
    {
        var left = Clamp01OrDefault(leftGeocodeConfidence, 0.0);
        var right = Clamp01OrDefault(rightGeocodeConfidence, 0.0);
        return Clamp01(0.10 + (Math.Min(left, right) * 0.20));
    }

    private static void ValidateThresholds(GeoMatcherThresholds thresholds)
    {
        if (thresholds.ExactProximityMeters <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholds), "ExactProximityMeters must be > 0.");
        }

        if (thresholds.NearProximityMeters < thresholds.ExactProximityMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholds), "NearProximityMeters must be >= ExactProximityMeters.");
        }

        if (thresholds.WeakProximityMeters < thresholds.NearProximityMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholds), "WeakProximityMeters must be >= NearProximityMeters.");
        }

        if (thresholds.NoMatchMeters < thresholds.WeakProximityMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholds), "NoMatchMeters must be >= WeakProximityMeters.");
        }

        ValidateBandScore(thresholds.ExactBandScore, nameof(thresholds.ExactBandScore));
        ValidateBandScore(thresholds.NearBandScore, nameof(thresholds.NearBandScore));
        ValidateBandScore(thresholds.WeakBandScore, nameof(thresholds.WeakBandScore));
        ValidateBandScore(thresholds.NoMatchBandScore, nameof(thresholds.NoMatchBandScore));
    }

    private static void ValidateBandScore(double value, string name)
    {
        if (value is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, "Band score must be in [0,1].");
        }
    }

    private static string ToToken(GeoMatchOutcome outcome) => outcome switch
    {
        GeoMatchOutcome.SameVenueSameCoordinates => "same_venue_same_coordinates",
        GeoMatchOutcome.NearbyLikelyDuplicate => "nearby_likely_duplicate",
        GeoMatchOutcome.NearbyDistinctVenue => "nearby_distinct_venue",
        _ => "no_meaningful_geo_match",
    };

    private static double Clamp01OrDefault(double? value, double defaultValue)
    {
        if (!value.HasValue || double.IsNaN(value.Value))
        {
            return defaultValue;
        }

        return Clamp01(value.Value);
    }

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

    private static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private readonly record struct TextAgreement(bool IsAgreement, bool IsConflict)
    {
        public static TextAgreement Agreement { get; } = new(true, false);
        public static TextAgreement Conflict { get; } = new(false, true);
        public static TextAgreement Unknown { get; } = new(false, false);
    }
}