using System.Globalization;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Resolution;

namespace WeUP.Application.Resolution;

/// <summary>
/// Deterministic matching and thresholding for duplicate detection.
///
/// Weighted score model (all deterministic and explainable):
/// - title similarity: 0.30
/// - venue similarity: 0.20
/// - time overlap: 0.20
/// - address similarity: 0.10
/// - geo proximity: 0.10
/// - source reference duplication: 0.10
///
/// Thresholds:
/// - >= 0.82: likely duplicate (eligible for auto-merge if no blockers)
/// - >= 0.62: possible duplicate (manual review)
/// - < 0.62: no match
/// </summary>
public sealed class DeterministicEventDuplicateDetector(IEntityResolutionRepository repository) : IEventDuplicateDetector
{
    private const double TitleWeight = 0.30;
    private const double VenueWeight = 0.20;
    private const double TimeWeight = 0.20;
    private const double AddressWeight = 0.10;
    private const double GeoWeight = 0.10;
    private const double SourceWeight = 0.10;

    public async Task<DuplicateMatchCandidate[]> CompareAsync(
        NormalizedEventCandidate candidate,
        int maxComparisons,
        CancellationToken ct = default)
    {
        var comparisons = await repository.GetComparisonRecordsAsync(candidate, maxComparisons, ct);
        if (comparisons.Length == 0)
        {
            return Array.Empty<DuplicateMatchCandidate>();
        }

        var results = new List<DuplicateMatchCandidate>(comparisons.Length);
        foreach (var record in comparisons)
        {
            var score = ScorePair(candidate, record.Candidate);
            var matchedSignals = BuildSignalSummary(score);
            var explanations = BuildExplanations(score);

            results.Add(new DuplicateMatchCandidate(
                record.RecordId,
                record.RecordType,
                record.CanonicalEventId,
                record.Candidate,
                score with
                {
                    MatchedSignals = matchedSignals,
                    Explanations = explanations,
                },
                record.SourceRefs,
                record.EvidenceRefs,
                record.ReviewRefs,
                record.IsCandidateRecord,
                record.IsExistingEventRecord));
        }

        return results
            .OrderByDescending(m => m.Score.CompositeScore)
            .ThenBy(m => m.ComparedRecordId, StringComparer.Ordinal)
            .ToArray();
    }

    private static DuplicateMatchScore ScorePair(NormalizedEventCandidate left, NormalizedEventCandidate right)
    {
        var title = Similarity(left.Title, right.Title);
        var venue = Similarity(left.VenueName, right.VenueName);
        var address = Similarity(left.Address, right.Address);
        var (time, deltaMinutes) = TimeOverlapScore(left, right);
        var (geo, distanceMeters) = GeoScore(left, right);
        var source = SourceDuplicationScore(left, right);

        var composite =
            (title * TitleWeight) +
            (venue * VenueWeight) +
            (time * TimeWeight) +
            (address * AddressWeight) +
            (geo * GeoWeight) +
            (source * SourceWeight);

        return new DuplicateMatchScore(
            CompositeScore: Math.Round(composite, 4),
            TitleSimilarity: Math.Round(title, 4),
            VenueSimilarity: Math.Round(venue, 4),
            TimeOverlap: Math.Round(time, 4),
            AddressSimilarity: Math.Round(address, 4),
            GeoProximity: Math.Round(geo, 4),
            SourceReferenceDuplication: Math.Round(source, 4),
            GeoDistanceMeters: distanceMeters,
            TimeDeltaMinutes: deltaMinutes,
            MatchedSignals: [],
            Explanations: []);
    }

    private static string[] BuildSignalSummary(DuplicateMatchScore score)
    {
        var signals = new List<string>();
        if (score.TitleSimilarity >= 0.70) signals.Add(MatchSignal.TitleSimilarity.ToString());
        if (score.VenueSimilarity >= 0.70) signals.Add(MatchSignal.VenueSimilarity.ToString());
        if (score.TimeOverlap >= 0.70) signals.Add(MatchSignal.TimeOverlap.ToString());
        if (score.AddressSimilarity >= 0.65) signals.Add(MatchSignal.AddressSimilarity.ToString());
        if (score.GeoProximity >= 0.70) signals.Add(MatchSignal.GeoProximity.ToString());
        if (score.SourceReferenceDuplication >= 0.90) signals.Add(MatchSignal.SourceReferenceDuplication.ToString());
        return signals.ToArray();
    }

    private static string[] BuildExplanations(DuplicateMatchScore score)
    {
        var explanations = new List<string>
        {
            $"Composite score: {score.CompositeScore:F2}",
            $"Title similarity: {score.TitleSimilarity:F2}",
            $"Venue similarity: {score.VenueSimilarity:F2}",
            $"Time overlap score: {score.TimeOverlap:F2}",
            $"Address similarity: {score.AddressSimilarity:F2}",
            $"Geo proximity score: {score.GeoProximity:F2}",
            $"Source duplication score: {score.SourceReferenceDuplication:F2}",
        };

        if (score.GeoDistanceMeters < double.MaxValue)
        {
            explanations.Add($"Geo distance: {score.GeoDistanceMeters:F1}m");
        }

        if (score.TimeDeltaMinutes < double.MaxValue)
        {
            explanations.Add($"Start-time delta: {score.TimeDeltaMinutes:F1}min");
        }

        return explanations.ToArray();
    }

    private static double Similarity(string? a, string? b)
    {
        var na = NormalizeText(a);
        var nb = NormalizeText(b);
        if (string.IsNullOrWhiteSpace(na) || string.IsNullOrWhiteSpace(nb)) return 0;
        if (na.Equals(nb, StringComparison.Ordinal)) return 1;

        var lev = 1d - ((double)LevenshteinDistance(na, nb) / Math.Max(na.Length, nb.Length));
        var jac = JaccardSimilarity(na, nb);
        return Clamp01((lev * 0.6) + (jac * 0.4));
    }

    private static (double Score, double DeltaMinutes) TimeOverlapScore(NormalizedEventCandidate left, NormalizedEventCandidate right)
    {
        var leftStart = ParseDate(left.StartUtc);
        var rightStart = ParseDate(right.StartUtc);
        if (!leftStart.HasValue || !rightStart.HasValue)
        {
            return (0, double.MaxValue);
        }

        var delta = Math.Abs((leftStart.Value - rightStart.Value).TotalMinutes);
        if (delta <= 30) return (1.0, delta);
        if (delta <= 120) return (0.70, delta);
        if (delta <= 360) return (0.40, delta);
        if (delta <= 720) return (0.20, delta);
        return (0.0, delta);
    }

    private static (double Score, double DistanceMeters) GeoScore(NormalizedEventCandidate left, NormalizedEventCandidate right)
    {
        var leftGeo = ExtractLatLng(left.Attributes);
        var rightGeo = ExtractLatLng(right.Attributes);
        if (!leftGeo.HasValue || !rightGeo.HasValue)
        {
            return (0, double.MaxValue);
        }

        var distance = HaversineMeters(leftGeo.Value.lat, leftGeo.Value.lng, rightGeo.Value.lat, rightGeo.Value.lng);
        if (distance <= 100) return (1.0, distance);
        if (distance <= 500) return (0.75, distance);
        if (distance <= 2000) return (0.40, distance);
        if (distance <= 10000) return (0.15, distance);
        return (0.0, distance);
    }

    private static double SourceDuplicationScore(NormalizedEventCandidate left, NormalizedEventCandidate right)
    {
        if (!string.IsNullOrWhiteSpace(left.SourceRef) &&
            left.SourceRef.Equals(right.SourceRef, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (!string.IsNullOrWhiteSpace(left.ExternalSourceId) &&
            left.ExternalSourceId.Equals(right.ExternalSourceId, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var leftEvidence = new HashSet<string>((left.EvidenceRefs ?? []).Select(NormalizeText), StringComparer.Ordinal);
        var rightEvidence = new HashSet<string>((right.EvidenceRefs ?? []).Select(NormalizeText), StringComparer.Ordinal);
        leftEvidence.RemoveWhere(string.IsNullOrWhiteSpace);
        rightEvidence.RemoveWhere(string.IsNullOrWhiteSpace);

        if (leftEvidence.Count == 0 || rightEvidence.Count == 0)
        {
            return 0.0;
        }

        leftEvidence.IntersectWith(rightEvidence);
        return leftEvidence.Count > 0 ? 0.85 : 0.0;
    }

    private static (double lat, double lng)? ExtractLatLng(IReadOnlyDictionary<string, string?>? attributes)
    {
        if (attributes is null || attributes.Count == 0) return null;

        if (!TryParseDouble(attributes, "lat", out var lat) && !TryParseDouble(attributes, "latitude", out lat))
            return null;

        if (!TryParseDouble(attributes, "lng", out var lng) &&
            !TryParseDouble(attributes, "lon", out lng) &&
            !TryParseDouble(attributes, "longitude", out lng))
            return null;

        return (lat, lng);
    }

    private static bool TryParseDouble(IReadOnlyDictionary<string, string?> attributes, string key, out double value)
    {
        value = 0;
        if (!attributes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return false;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static DateTimeOffset? ParseDate(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static double JaccardSimilarity(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        if (leftTokens.Count == 0 || rightTokens.Count == 0) return 0;
        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var matrix = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(DegreesToRadians(lat1))
                * Math.Cos(DegreesToRadians(lat2))
                * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadius * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}

public sealed class DeterministicMergePlanner : IMergePlanner
{
    public MergePlan CreatePlan(
        NormalizedEventCandidate incoming,
        DuplicateMatchCandidate bestMatch,
        ResolutionDecision decision)
    {
        var existingCandidate = bestMatch.ComparedCandidate;

        // Existing canonical event values remain default-preferred for safety unless they are missing.
        var title = PreferExisting(incoming.Title, existingCandidate.Title);
        var venue = PreferExisting(incoming.VenueName, existingCandidate.VenueName);
        var address = PreferExisting(incoming.Address, existingCandidate.Address);
        var startUtc = PreferExisting(incoming.StartUtc, existingCandidate.StartUtc);
        var endUtc = PreferExisting(incoming.EndUtc, existingCandidate.EndUtc);
        var timezone = PreferExisting(incoming.Timezone, existingCandidate.Timezone);
        var category = PreferExisting(incoming.Category, existingCandidate.Category);
        var description = PreferExisting(incoming.Description, existingCandidate.Description);

        var conflicts = BuildConflicts(incoming, bestMatch.Score);
        var manualReasons = conflicts.Where(c => c.BlocksAutoMerge).Select(c => c.Reason).Distinct(StringComparer.Ordinal).ToArray();
        var mergedConfidence = Math.Round(Math.Max(0.30, (incoming.ExtractionConfidence + bestMatch.Score.CompositeScore) / 2.0), 4);

        var sources = bestMatch.ComparedSourceRefs
            .Concat(new[] { incoming.SourceRef })
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var evidence = (bestMatch.ComparedEvidenceRefs ?? [])
            .Concat(incoming.EvidenceRefs ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rationale = new List<string>
        {
            "Title/venue/address/start precedence defaults to existing canonical values when present.",
            "Source references are unioned; no source reference is dropped.",
            "Evidence references are unioned; conflicting evidence is preserved for audit.",
            "Manual review blockers are computed from material venue/time/address/source conflicts.",
        };

        return new MergePlan(
            CanonicalEventId: bestMatch.CanonicalEventId ?? string.Empty,
            Title: title,
            VenueName: venue,
            Address: address,
            StartUtc: startUtc,
            EndUtc: endUtc,
            Timezone: timezone,
            Category: category,
            Description: description,
            Tags: (incoming.Tags ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            MergedConfidence: mergedConfidence,
            AutoMergeAllowed: decision.AutoMergeAllowed && manualReasons.Length == 0,
            MergeRationale: rationale.ToArray(),
            ManualReviewReasons: manualReasons,
            Conflicts: conflicts,
            UnionedSourceRefs: sources,
            UnionedEvidenceRefs: evidence,
            PreservedReviewRefs: bestMatch.ComparedReviewRefs ?? []);
    }

    private static FieldConflict[] BuildConflicts(NormalizedEventCandidate incoming, DuplicateMatchScore score)
    {
        var conflicts = new List<FieldConflict>();

        if (!string.IsNullOrWhiteSpace(incoming.VenueName) && score.VenueSimilarity < 0.35)
        {
            conflicts.Add(new FieldConflict(
                "venue",
                ExistingValue: null,
                IncomingValue: incoming.VenueName,
                BlocksAutoMerge: true,
                Reason: "Material venue mismatch."));
        }

        if (!string.IsNullOrWhiteSpace(incoming.Address) && score.AddressSimilarity < 0.30)
        {
            conflicts.Add(new FieldConflict(
                "address",
                ExistingValue: null,
                IncomingValue: incoming.Address,
                BlocksAutoMerge: true,
                Reason: "Incompatible address evidence."));
        }

        if (score.TimeDeltaMinutes != double.MaxValue && score.TimeDeltaMinutes > 240)
        {
            conflicts.Add(new FieldConflict(
                "time",
                ExistingValue: null,
                IncomingValue: incoming.StartUtc,
                BlocksAutoMerge: true,
                Reason: "Materially different event time."));
        }

        var lowConfidenceSourceMismatch = incoming.ExtractionConfidence < 0.45 && score.SourceReferenceDuplication < 0.20;
        if (lowConfidenceSourceMismatch)
        {
            conflicts.Add(new FieldConflict(
                "source",
                ExistingValue: null,
                IncomingValue: incoming.SourceKind,
                BlocksAutoMerge: true,
                Reason: "Low-confidence source mismatch."));
        }

        return conflicts.ToArray();
    }

    private static string? PreferExisting(string? incoming, string? existing)
    {
        if (!string.IsNullOrWhiteSpace(existing)) return existing;
        return string.IsNullOrWhiteSpace(incoming) ? null : incoming;
    }
}

public sealed class EntityResolutionService(
    IEventDuplicateDetector duplicateDetector,
    IMergePlanner mergePlanner,
    IEntityResolutionRepository repository) : IEntityResolutionService
{
    private const double LikelyDuplicateThreshold = 0.82;
    private const double PossibleDuplicateThreshold = 0.62;

    public async Task<EntityResolutionResult> EvaluateAsync(EvaluateResolutionRequest request, CancellationToken ct = default)
    {
        var resolutionId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var matches = await duplicateDetector.CompareAsync(request.Candidate, request.MaxComparisons, ct);
        var best = matches.FirstOrDefault();
        var decision = BuildDecision(resolutionId, request.Candidate, best, now);
        var plan = best is null || string.IsNullOrWhiteSpace(best.CanonicalEventId)
            ? null
            : mergePlanner.CreatePlan(request.Candidate, best, decision);

        var status = decision.RequiresManualReview ? "MANUAL_REVIEW_REQUIRED" : "EVALUATED";

        var audit = new List<string>
        {
            $"Evaluated candidate '{request.Candidate.SourceRef}' at {now:O}.",
            $"Decision: {decision.DecisionType}.",
        };

        if (best is not null)
        {
            audit.Add($"Best match: {best.ComparedRecordType}/{best.ComparedRecordId} with score {best.Score.CompositeScore:F2}.");
        }

        if (plan is not null && plan.Conflicts.Length > 0)
        {
            audit.AddRange(plan.Conflicts.Select(c => $"Conflict [{c.FieldName}]: {c.Reason}"));
        }

        var result = new EntityResolutionResult(
            ResolutionId: resolutionId,
            Candidate: request.Candidate,
            Matches: matches,
            BestMatch: best,
            Decision: decision,
            MergePlan: plan,
            Status: status,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            MergedAtUtc: null,
            AuditTrail: audit.ToArray());

        await repository.SaveResultAsync(result, ct);
        return result;
    }

    public async Task<MergeResolutionResponse> MergeAsync(MergeResolutionRequest request, CancellationToken ct = default)
    {
        var result = await repository.GetResultAsync(request.ResolutionId, ct);
        if (result is null)
        {
            return new MergeResolutionResponse(request.ResolutionId, false, false, "Resolution not found.", null, []);
        }

        if (result.Status.Equals("MERGED", StringComparison.OrdinalIgnoreCase))
        {
            return new MergeResolutionResponse(result.ResolutionId, true, false, "Resolution already merged.", result.MergePlan?.CanonicalEventId, result.AuditTrail);
        }

        if (result.BestMatch is null || result.MergePlan is null)
        {
            var noPlan = result with
            {
                Status = "MANUAL_REVIEW_REQUIRED",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                AuditTrail = result.AuditTrail.Concat(["Merge blocked: no safe canonical target available."]).ToArray(),
            };
            await repository.SaveResultAsync(noPlan, ct);
            return new MergeResolutionResponse(result.ResolutionId, false, true, "Merge requires manual review.", null, noPlan.AuditTrail);
        }

        if ((result.Decision.RequiresManualReview || !result.MergePlan.AutoMergeAllowed) && !request.AllowUnsafeMerge)
        {
            var blocked = result with
            {
                Status = "MANUAL_REVIEW_REQUIRED",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                AuditTrail = result.AuditTrail.Concat(["Merge blocked by manual-review policy."]).ToArray(),
            };
            await repository.SaveResultAsync(blocked, ct);
            return new MergeResolutionResponse(result.ResolutionId, false, true, "Merge requires manual review.", result.MergePlan.CanonicalEventId, blocked.AuditTrail);
        }

        var commit = await repository.CommitMergeAsync(new MergeCommitCommand(
            ResolutionId: result.ResolutionId,
            Candidate: result.Candidate,
            BestMatch: result.BestMatch,
            Plan: result.MergePlan,
            Decision: result.Decision,
            MatchReasons: result.BestMatch.Score.Explanations,
            RequestedBy: request.RequestedBy,
            RequestedAtUtc: DateTimeOffset.UtcNow), ct);

        var updated = result with
        {
            Status = commit.Success ? "MERGED" : "MANUAL_REVIEW_REQUIRED",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            MergedAtUtc = commit.Success ? DateTimeOffset.UtcNow : null,
            AuditTrail = result.AuditTrail.Concat(commit.AuditTrail).ToArray(),
        };

        await repository.SaveResultAsync(updated, ct);

        return new MergeResolutionResponse(
            ResolutionId: updated.ResolutionId,
            Merged: commit.Success,
            RequiresManualReview: commit.RequiresManualReview,
            Message: commit.Message,
            CanonicalEventId: commit.CanonicalEventId,
            AuditTrail: updated.AuditTrail);
    }

    public Task<EntityResolutionResult?> GetAsync(string resolutionId, CancellationToken ct = default)
        => repository.GetResultAsync(resolutionId, ct);

    private static ResolutionDecision BuildDecision(
        string resolutionId,
        NormalizedEventCandidate candidate,
        DuplicateMatchCandidate? best,
        DateTimeOffset decidedAtUtc)
    {
        if (best is null)
        {
            return new ResolutionDecision(
                ResolutionId: resolutionId,
                DecisionType: ResolutionDecisionType.NoMatch,
                AutoMergeAllowed: false,
                RequiresManualReview: false,
                Reasons: ["No comparable records found."],
                DecidedAtUtc: decidedAtUtc);
        }

        var reasons = new List<string>(best.Score.Explanations);
        var score = best.Score.CompositeScore;

        var hasMaterialConflict =
            (!string.IsNullOrWhiteSpace(candidate.VenueName) && best.Score.VenueSimilarity < 0.35) ||
            (!string.IsNullOrWhiteSpace(candidate.Address) && best.Score.AddressSimilarity < 0.30) ||
            (best.Score.TimeDeltaMinutes != double.MaxValue && best.Score.TimeDeltaMinutes > 240) ||
            (candidate.ExtractionConfidence < 0.45 && best.Score.SourceReferenceDuplication < 0.20);

        if (best.CandidateToCandidateComparison)
        {
            reasons.Add("Best match is candidate-to-candidate; manual reviewer must choose canonical target.");
            return new ResolutionDecision(
                ResolutionId: resolutionId,
                DecisionType: ResolutionDecisionType.RequiresManualReview,
                AutoMergeAllowed: false,
                RequiresManualReview: true,
                Reasons: reasons.ToArray(),
                DecidedAtUtc: decidedAtUtc);
        }

        if (score >= LikelyDuplicateThreshold && !hasMaterialConflict)
        {
            reasons.Add("Likely duplicate threshold met without blockers.");
            return new ResolutionDecision(
                ResolutionId: resolutionId,
                DecisionType: ResolutionDecisionType.AutoMergeAllowed,
                AutoMergeAllowed: true,
                RequiresManualReview: false,
                Reasons: reasons.ToArray(),
                DecidedAtUtc: decidedAtUtc);
        }

        if (score >= PossibleDuplicateThreshold || hasMaterialConflict)
        {
            reasons.Add(hasMaterialConflict
                ? "Material conflict detected; manual review required."
                : "Possible duplicate threshold met; manual review required.");

            return new ResolutionDecision(
                ResolutionId: resolutionId,
                DecisionType: ResolutionDecisionType.RequiresManualReview,
                AutoMergeAllowed: false,
                RequiresManualReview: true,
                Reasons: reasons.ToArray(),
                DecidedAtUtc: decidedAtUtc);
        }

        reasons.Add("Below duplicate threshold.");
        return new ResolutionDecision(
            ResolutionId: resolutionId,
            DecisionType: ResolutionDecisionType.NoMatch,
            AutoMergeAllowed: false,
            RequiresManualReview: false,
            Reasons: reasons.ToArray(),
            DecidedAtUtc: decidedAtUtc);
    }
}
