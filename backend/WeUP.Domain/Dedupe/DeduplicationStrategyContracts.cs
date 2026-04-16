using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Dedupe;

// ============================================================================
// M2-P06: Canonical Deduplication Strategy — Type Contracts v1.0
//
// STRATEGY INVARIANTS
// ─────────────────────────────────────────────────────────────────────────────
// 1. All scoring is deterministic: equal inputs always produce identical output.
// 2. No hidden thresholds: every weight and band boundary is a named constant
//    in WeightedDeduplicationStrategy.
// 3. Every score contribution is traceable through DimensionScore.Explanation.
// 4. Distinct is a first-class affirmative outcome with its own classification
//    path — it is NOT a low-score fallback.
// 5. Auto-merge is NEVER permitted when any MergeBlockerKind is active.
// 6. IDeduplicationStrategy is pure (no I/O, no EF context, no side effects).
//    Callers own candidate pre-selection; this strategy owns only the scoring.
// ============================================================================

// ---------------------------------------------------------------------------
// DuplicateAssessmentLevel
// ---------------------------------------------------------------------------

/// <summary>
/// Canonical classification of a duplicate assessment outcome.
///
/// Score band boundaries (weighted composite 0–1):
///   ExactDuplicate    ≥ 0.92  or deterministic source-hash identity
///   ProbableDuplicate ≥ 0.75
///   PossibleDuplicate ≥ 0.50
///   Distinct          &lt; 0.50  (affirmative — not a residual bucket)
///
/// Invariant: levels are ordered by ascending match confidence.
/// Callers may compare as integers (Distinct &lt; PossibleDuplicate &lt; ...).
/// </summary>
public enum DuplicateAssessmentLevel
{
    /// <summary>
    /// Affirmatively distinct: composite score below threshold with sufficient
    /// signal coverage to confidently exclude duplication.
    /// This is a positive assertion — not an indication of insufficient data.
    /// When data is insufficient the assessment still carries LowConfidenceFlag
    /// in ScoringNotes; the level itself remains Distinct.
    /// </summary>
    Distinct = 0,

    /// <summary>
    /// Composite score 0.50–0.74. Some signals overlap but confidence is
    /// insufficient for automatic action. Route to moderation queue for
    /// human review before any merge or discard.
    /// </summary>
    PossibleDuplicate = 1,

    /// <summary>
    /// Composite score 0.75–0.91. High-probability duplication.
    /// Eligible for auto-merge only when AutoMergeAllowed = true on the
    /// DuplicateAssessment (i.e., no hard blockers are active).
    /// </summary>
    ProbableDuplicate = 2,

    /// <summary>
    /// Composite score ≥ 0.92 or deterministic source-hash identity match.
    /// Auto-merge is safe when canonical.IsApproved = false and no blockers
    /// are active.
    /// </summary>
    ExactDuplicate = 3,
}

// ---------------------------------------------------------------------------
// MatchDimension
// ---------------------------------------------------------------------------

/// <summary>
/// The six scored matching dimensions. Weights are fixed constants in
/// WeightedDeduplicationStrategy and must sum to exactly 1.00.
///
/// Weight table (canonical):
///   TitleSimilarity    0.30
///   VenueSimilarity    0.25
///   AddressSimilarity  0.15
///   TemporalOverlap    0.15
///   GeoProximity       0.10
///   SourceHashEvidence 0.05
///                      ────
///                      1.00
/// </summary>
public enum MatchDimension
{
    /// <summary>
    /// Blended text similarity (Levenshtein × 0.6 + Jaccard × 0.4) of
    /// normalized event titles. Weight: 0.30.
    /// </summary>
    TitleSimilarity = 0,

    /// <summary>
    /// Blended text similarity of normalized venue names. Weight: 0.25.
    /// </summary>
    VenueSimilarity = 1,

    /// <summary>
    /// Blended text similarity of normalized street address strings. Weight: 0.15.
    /// </summary>
    AddressSimilarity = 2,

    /// <summary>
    /// Step-function score derived from absolute start-time delta (minutes).
    /// Breakpoints: ≤30 min → 1.00, ≤120 → 0.75, ≤360 → 0.40, ≤720 → 0.20, else 0.00.
    /// Weight: 0.15.
    /// </summary>
    TemporalOverlap = 3,

    /// <summary>
    /// Step-function score derived from Haversine distance (meters).
    /// Breakpoints: ≤100 m → 1.00, ≤500 → 0.75, ≤2000 → 0.40, ≤10000 → 0.15, else 0.00.
    /// Weight: 0.10.
    /// </summary>
    GeoProximity = 4,

    /// <summary>
    /// Exact-match score from shared SourceRef, ExternalSourceId, or
    /// EvidenceRefs intersection. Acts as a deterministic tiebreaker / bonus.
    /// Weight: 0.05.
    /// </summary>
    SourceHashEvidence = 5,
}

// ---------------------------------------------------------------------------
// MergeBlockerKind
// ---------------------------------------------------------------------------

/// <summary>
/// Hard blockers that prevent automatic merge regardless of composite score.
/// When any blocker is active on a DuplicateAssessment, AutoMergeAllowed = false.
/// Blockers do not change the DuplicateAssessmentLevel; they only constrain
/// the permitted action on a match.
/// </summary>
public enum MergeBlockerKind
{
    /// <summary>
    /// Start-time delta between candidate and canonical exceeds 720 minutes
    /// (12 hours). Even identical-title events at the same venue on different
    /// days are distinct events.
    /// </summary>
    TemporalDeviationExceedsLimit,

    /// <summary>
    /// Haversine distance between coordinates exceeds 10,000 meters (10 km).
    /// Events at materially different geographic locations are never the same
    /// event, even if names match.
    /// </summary>
    GeoDistanceExceedsLimit,

    /// <summary>
    /// Both title similarity and venue similarity are below 0.25.
    /// Neither text anchor is present; a merge would have no identifiable basis.
    /// </summary>
    BothTextAnchorsTooLow,

    /// <summary>
    /// The canonical EventAggregate has been marked IsApproved = true.
    /// Approved records require human-in-the-loop before any mutation.
    /// </summary>
    ApprovedEventProtection,

    /// <summary>
    /// candidate.ExternalSourceId equals canonical.ExternalSourceId, but
    /// title similarity is below 0.50. The same upstream source produced
    /// materially different titles — this likely indicates a data error and
    /// requires manual investigation.
    /// </summary>
    SourceIdentityConflict,

    /// <summary>
    /// Neither candidate nor canonical has a parseable StartUtc value.
    /// The temporal dimension cannot contribute; overall assessment confidence
    /// is too low to safely allow automatic merge.
    /// </summary>
    InsufficientTemporalData,
}

// ---------------------------------------------------------------------------
// DimensionScore
// ---------------------------------------------------------------------------

/// <summary>
/// Score contribution from a single matching dimension.
/// All fields are set by the strategy — callers must not construct this record.
/// </summary>
/// <param name="Dimension">Which dimension was scored.</param>
/// <param name="RawScore">Dimension score before weight application, 0.00–1.00, rounded to 4dp.</param>
/// <param name="Weight">Assigned weight constant for this dimension. Immutable.</param>
/// <param name="WeightedContribution">RawScore × Weight, contributing to CompositeScore. Rounded to 6dp.</param>
/// <param name="Explanation">
/// Human-readable justification: the method and inputs used to arrive at this score.
/// </param>
public sealed record DimensionScore(
    MatchDimension Dimension,
    double RawScore,
    double Weight,
    double WeightedContribution,
    string Explanation);

// ---------------------------------------------------------------------------
// MatchScoreBreakdown
// ---------------------------------------------------------------------------

/// <summary>
/// Complete per-dimension breakdown of a duplicate assessment scoring run.
///
/// CompositeScore is the authoritative match score and equals the exact sum
/// of all WeightedContributions (within double floating-point tolerance).
///
/// Invariant: CompositeScore == sum(dim.WeightedContribution for dim in Dimensions)
/// Callers must use CompositeScore for all threshold comparisons — never
/// recompute it from individual dimension scores.
///
/// ActiveSignals contains the names of dimensions whose RawScore exceeded
/// the signal activation threshold (per-dimension constants in the strategy).
/// This is informational; scoring does not depend on it.
/// </summary>
public sealed record MatchScoreBreakdown(
    double CompositeScore,
    DimensionScore TitleScore,
    DimensionScore VenueScore,
    DimensionScore AddressScore,
    DimensionScore TemporalScore,
    DimensionScore GeoScore,
    DimensionScore SourceHashScore,
    string[] ActiveSignals,
    string[] ScoringNotes);

// ---------------------------------------------------------------------------
// MergeSafetyVerdict
// ---------------------------------------------------------------------------

/// <summary>
/// Safety evaluation for merge operations.
/// AutoMergeAllowed = false when any blocker is active.
///
/// Callers must check AutoMergeAllowed before committing any merge. The presence
/// of blockers does not mean the records are Distinct — they may still be matched
/// at ProbableDuplicate or ExactDuplicate level, but require human review.
/// </summary>
public sealed record MergeSafetyVerdict(
    bool AutoMergeAllowed,
    MergeBlockerKind[] ActiveBlockers,
    string[] BlockerExplanations);

// ---------------------------------------------------------------------------
// EventAggregateSnapshot
// ---------------------------------------------------------------------------

/// <summary>
/// Snapshot of a canonical EventAggregate record used as the comparison target
/// for deduplication. This record is projection-side: callers build it from the
/// persistence layer (EventEntity or equivalent) before invoking the strategy.
///
/// Usage contract:
///   The strategy is pure and has no repository access.
///   Infrastructure callers project EventEntity into this snapshot:
///
///     var snapshot = new EventAggregateSnapshot(
///         CanonicalEventId: e.PublicId,
///         Title: e.CanonicalTitle,
///         VenueName: e.VenueName,
///         Address: e.AddressRaw,
///         Latitude: e.Latitude,
///         Longitude: e.Longitude,
///         StartUtc: e.StartUtc.ToString("O"),
///         EndUtc: e.EndUtc?.ToString("O"),
///         Timezone: e.Timezone,
///         Category: e.Category,
///         Confidence: e.Confidence,
///         SourceRefs: e.Sources.Select(s => s.SourceRef).ToArray(),
///         EvidenceRefs: [],
///         IsApproved: e.Status == "APPROVED");
/// </summary>
public sealed record EventAggregateSnapshot(
    string CanonicalEventId,
    string? Title,
    string? VenueName,
    string? Address,
    double Latitude,
    double Longitude,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    double Confidence,
    string[] SourceRefs,
    string[] EvidenceRefs,
    bool IsApproved,
    string? ExternalSourceId = null,
    IReadOnlyDictionary<string, string?>? Attributes = null);

// ---------------------------------------------------------------------------
// DuplicateAssessment
// ---------------------------------------------------------------------------

/// <summary>
/// Complete duplicate assessment for a single (incoming candidate, canonical) pair.
///
/// Downstream resolution service consumption pattern:
///
///   // 1. Route on Level + AutoMergeAllowed
///   if (assessment.Level == DuplicateAssessmentLevel.Distinct)
///       // → Create new canonical event; do NOT merge
///
///   else if (assessment.AutoMergeAllowed)
///       // → Commit merge via IEntityResolutionRepository.CommitMergeAsync()
///
///   else
///       // → Enqueue to moderation queue with assessment.Rationale as context
///
///   // 2. Breakdown.CompositeScore is authoritative; never re-derive from signals.
///   // 3. Rationale[] provides full audit trail for every decision factor.
///   // 4. AssessedAtUtc is informational UTC timestamp; not needed for routing.
/// </summary>
public sealed record DuplicateAssessment(
    /// <summary>SourceRef of the incoming candidate that was assessed.</summary>
    string CandidateSourceRef,
    /// <summary>CanonicalEventId of the aggregate record compared against.</summary>
    string CanonicalEventId,
    /// <summary>Classification outcome. Use this for all routing decisions.</summary>
    DuplicateAssessmentLevel Level,
    /// <summary>Full per-dimension score breakdown. CompositeScore drives Level.</summary>
    MatchScoreBreakdown Breakdown,
    /// <summary>Merge safety evaluation. Callers must check before committing.</summary>
    MergeSafetyVerdict SafetyVerdict,
    /// <summary>
    /// True only when Level is ProbableDuplicate or ExactDuplicate AND no
    /// MergeBlockerKind is active. Derived field — never set independently.
    /// </summary>
    bool AutoMergeAllowed,
    /// <summary>Ordered human-readable justification for every factor in the decision.</summary>
    string[] Rationale,
    /// <summary>UTC instant at which Assess() was called. Informational only.</summary>
    DateTimeOffset AssessedAtUtc);

// ---------------------------------------------------------------------------
// IDeduplicationStrategy
// ---------------------------------------------------------------------------

/// <summary>
/// Canonical deduplication strategy: scores and classifies the relationship
/// between a single incoming EventCandidate and a single canonical
/// EventAggregateSnapshot, producing a complete DuplicateAssessment.
///
/// DESIGN CONTRACT
/// ───────────────
/// • Pure and synchronous. No database access, no I/O, no external calls.
/// • Callers pre-select the pool of EventAggregateSnapshots to compare against
///   (e.g., pre-filtered by ±24h time window, title prefix, or geo bounding box).
///   This interface scores and classifies; it does not search or filter.
/// • Every call with the same arguments must return a result identical in all
///   fields except AssessedAtUtc.
/// • The strategy holds no mutable state; it is safe to register as a
///   singleton or to call concurrently without synchronization.
///
/// DOWNSTREAM CONSUMPTION PATTERN
/// ───────────────────────────────
///   // 1. Load snapshots (pre-filtered by time window / geo proximity)
///   var snapshots = await repo.GetCandidateSnapshotsAsync(incoming, ct);
///
///   // 2. Score each snapshot
///   var assessments = snapshots
///       .Select(s => strategy.Assess(incoming, s))
///       .OrderByDescending(a => a.Breakdown.CompositeScore)
///       .ThenBy(a => a.CanonicalEventId, StringComparer.Ordinal)  // stable sort
///       .ToArray();
///
///   // 3. Take the best assessment
///   var best = assessments.FirstOrDefault();
///
///   // 4. Route on Level and AutoMergeAllowed
///   if (best is null || best.Level == DuplicateAssessmentLevel.Distinct)
///       // → ingest as new canonical event
///   else if (best.AutoMergeAllowed)
///       // → IEntityResolutionRepository.CommitMergeAsync(...)
///   else
///       // → enqueue for moderation review with best.Rationale
/// </summary>
public interface IDeduplicationStrategy
{
    /// <summary>
    /// Scores and classifies the relationship between an incoming EventCandidate
    /// and a canonical EventAggregateSnapshot.
    /// </summary>
    /// <param name="incoming">
    /// The normalized candidate produced by the ingestion pipeline.
    /// Must not be null; individual fields may be null (handled gracefully).
    /// </param>
    /// <param name="canonical">
    /// A snapshot of an existing canonical EventAggregate record.
    /// Must not be null; individual fields may be null (handled gracefully).
    /// </param>
    /// <returns>
    /// A complete, immutable assessment. Level and AutoMergeAllowed drive routing.
    /// Breakdown and Rationale drive audit and moderation UI.
    /// </returns>
    DuplicateAssessment Assess(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical);
}
