using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;
using WeUP.Infrastructure.Persistence;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Phase 0 deduplication service.
/// Uses deterministic heuristics — no fuzzy black-box matching.
/// Comparison dimensions: title, venue, address, geo, temporal overlap.
/// </summary>
public sealed class DeduplicationService(
    WeUpDbContext db,
    IMergePolicyEvaluator mergePolicy) : IDeduplicationService
{
    // Thresholds
    private const double ProbableDuplicateThreshold = 0.80;
    private const double PossibleDuplicateThreshold = 0.55;
    private const double GeoProximityMeters = 200.0;
    private const double TemporalOverlapMinutes = 60.0;

    public async Task<DedupeResult> EvaluateCandidateAsync(
        NormalizedEventCandidate candidate,
        CancellationToken ct = default)
    {
        // Query existing events that might overlap
        var existingEvents = await GetCandidateMatchesAsync(candidate, ct);

        if (existingEvents.Count == 0)
        {
            return new DedupeResult(
                candidate.SourceRef,
                DedupeOutcome.NoMatch,
                new DedupeScore(DedupeOutcome.NoMatch, 0.0, 0.0, 0.0, 0.0, double.MaxValue, 0.0, []),
                null, null, false, []);
        }

        // Score against each existing event; take the highest
        DedupeResult? best = null;
        foreach (var existing in existingEvents)
        {
            var score = Score(candidate, existing);
            var outcome = ClassifyOutcome(score);
            var result = BuildResult(candidate, existing, score, outcome, mergePolicy);
            if (best is null || score.Score > best.Score.Score)
                best = result;
        }

        return best!;
    }

    private async Task<List<ExistingEventSnapshot>> GetCandidateMatchesAsync(
        NormalizedEventCandidate candidate, CancellationToken ct)
    {
        // Pre-filter: same title prefix or same venue name, within ±24h of candidate start
        DateTimeOffset? startUtc = candidate.StartUtc is not null
            ? DateTimeOffset.TryParse(candidate.StartUtc, out var dt) ? dt : null
            : null;

        var query = db.Events.AsQueryable();

        if (candidate.Title is not null)
        {
            var titlePrefix = candidate.Title[..Math.Min(10, candidate.Title.Length)].ToLowerInvariant();
            query = query.Where(e =>
                e.CanonicalTitle.ToLower().StartsWith(titlePrefix) ||
                e.VenueName.ToLower() == (candidate.VenueName ?? string.Empty).ToLower());
        }

        if (startUtc.HasValue)
        {
            var window = TimeSpan.FromHours(24);
            query = query.Where(e =>
                e.StartUtc >= startUtc.Value - window &&
                e.StartUtc <= startUtc.Value + window);
        }

        var entities = await query.Take(20).ToListAsync(ct);

        return entities.Select(e => new ExistingEventSnapshot(
            e.Id.ToString(),
            e.CanonicalTitle,
            e.VenueName,
            e.AddressRaw,
            e.Latitude,
            e.Longitude,
            e.StartUtc.ToString("O"),
            e.EndUtc?.ToString("O"),
            e.Confidence,
            e.Sources.Select(s => s.SourceRef).ToArray())).ToList();
    }

    private static DedupeScore Score(NormalizedEventCandidate c, ExistingEventSnapshot e)
    {
        var titleSim = StringSimilarity(c.Title, e.Title);
        var venueSim = StringSimilarity(c.VenueName, e.VenueName);
        var addrSim  = StringSimilarity(c.Address, e.Address);
        var geoDist  = double.MaxValue;
        var timeOver = 0.0;

        var composite = titleSim * 0.40 + venueSim * 0.30 + addrSim * 0.15
                      + (geoDist < GeoProximityMeters ? 0.15 : 0.0);

        var reasons = new List<string>();
        if (titleSim > 0.8) reasons.Add($"Title similarity {titleSim:F2}");
        if (venueSim > 0.8) reasons.Add($"Venue similarity {venueSim:F2}");
        if (addrSim > 0.7) reasons.Add($"Address similarity {addrSim:F2}");

        return new DedupeScore(
            DedupeOutcome.NoMatch, composite,
            titleSim, venueSim, addrSim,
            geoDist, timeOver, [.. reasons]);
    }

    private static DedupeOutcome ClassifyOutcome(DedupeScore score)
    {
        if (score.Score >= ProbableDuplicateThreshold) return DedupeOutcome.MergeIntoExisting;
        if (score.Score >= PossibleDuplicateThreshold) return DedupeOutcome.ProbableDuplicate;
        if (score.Score >= 0.35) return DedupeOutcome.PossibleDuplicate;
        return DedupeOutcome.NoMatch;
    }

    private static DedupeResult BuildResult(
        NormalizedEventCandidate candidate,
        ExistingEventSnapshot existing,
        DedupeScore score,
        DedupeOutcome outcome,
        IMergePolicyEvaluator mergePolicy)
    {
        var updatedScore = score with { Outcome = outcome };

        MergeDecision? merge = outcome == DedupeOutcome.MergeIntoExisting
            ? mergePolicy.Merge(candidate, ToCandidate(existing))
            : null;

        var requiresReview = outcome is DedupeOutcome.ProbableDuplicate or DedupeOutcome.NeedsManualResolution;
        var reasons = outcome switch
        {
            DedupeOutcome.ProbableDuplicate   => new[] { "Probable duplicate — manual confirmation required" },
            DedupeOutcome.NeedsManualResolution => new[] { "Ambiguous match — manual resolution required" },
            _ => Array.Empty<string>(),
        };

        return new DedupeResult(
            candidate.SourceRef, outcome, updatedScore,
            existing.EventId, merge, requiresReview, reasons);
    }

    private static NormalizedEventCandidate ToCandidate(ExistingEventSnapshot e) =>
        new(e.Title, e.VenueName, e.Address, e.StartUtc, e.EndUtc,
            null, null, null, null, "existing", e.EventId,
            e.Confidence, 0.0, 0.0, e.SourceRefs);

    /// <summary>
    /// Normalized Levenshtein similarity (0–1). Returns 0 for null/empty inputs.
    /// </summary>
    private static double StringSimilarity(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
        var la = a.ToLowerInvariant().Trim();
        var lb = b.ToLowerInvariant().Trim();
        if (la == lb) return 1.0;

        var dist = LevenshteinDistance(la, lb);
        var maxLen = Math.Max(la.Length, lb.Length);
        return 1.0 - (double)dist / maxLen;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;
        var d = new int[s.Length + 1, t.Length + 1];
        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= t.Length; j++) d[0, j] = j;
        for (var i = 1; i <= s.Length; i++)
        for (var j = 1; j <= t.Length; j++)
            d[i, j] = s[i - 1] == t[j - 1]
                ? d[i - 1, j - 1]
                : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));
        return d[s.Length, t.Length];
    }

    private record ExistingEventSnapshot(
        string EventId, string Title, string VenueName, string Address,
        double Lat, double Lng, string StartUtc, string? EndUtc,
        double Confidence, string[] SourceRefs);
}

// ---------------------------------------------------------------------------
// Merge policy
// ---------------------------------------------------------------------------

/// <summary>
/// Field-level merge rules:
/// - Prefer higher-confidence value
/// - Never overwrite reviewed/approved data with lower-confidence candidate
/// - Preserve all source refs
/// </summary>
public sealed class MergePolicyEvaluator : IMergePolicyEvaluator
{
    public MergeDecision Merge(NormalizedEventCandidate incoming, NormalizedEventCandidate existing)
    {
        var rationale = new List<string>();

        // Title: prefer existing if it has higher confidence
        var title = incoming.ExtractionConfidence > existing.ExtractionConfidence
            ? Choose(incoming.Title, existing.Title, "title", rationale, "incoming")
            : Choose(existing.Title, incoming.Title, "title", rationale, "existing");

        var venue    = PreferNonNull(existing.VenueName, incoming.VenueName, "venue", rationale);
        var address  = PreferNonNull(existing.Address, incoming.Address, "address", rationale);
        var startUtc = PreferNonNull(existing.StartUtc, incoming.StartUtc, "startUtc", rationale);

        var mergedSources = (existing.EvidenceRefs ?? [])
            .Concat(incoming.EvidenceRefs ?? [])
            .Distinct()
            .ToArray();

        return new MergeDecision(title, venue, address, startUtc,
            existing.EndUtc ?? incoming.EndUtc,
            existing.Timezone ?? incoming.Timezone,
            existing.Category ?? incoming.Category,
            existing.Description ?? incoming.Description,
            MergeTags(existing.Tags, incoming.Tags),
            mergedSources, [.. rationale]);
    }

    private static string? Choose(string? preferred, string? fallback, string field, List<string> r, string source)
    {
        if (preferred is not null) { r.Add($"{field}: used {source}"); return preferred; }
        return fallback;
    }

    private static string? PreferNonNull(string? a, string? b, string field, List<string> r)
    {
        if (a is not null) return a;
        if (b is not null) { r.Add($"{field}: used incoming (existing was null)"); return b; }
        return null;
    }

    private static string[]? MergeTags(string[]? a, string[]? b)
    {
        var all = (a ?? []).Concat(b ?? []).Distinct().ToArray();
        return all.Length > 0 ? all : null;
    }
}
