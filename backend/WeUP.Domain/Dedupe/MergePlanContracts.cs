using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Dedupe;

// ============================================================================
// M2-P09: Merge Planning and Conflict Resolution System v1.0
//
// DESIGN INTENT
// ─────────────────────────────────────────────────────────────────────────────
// Merge planning is SEPARATE from merge execution.
// 
// A MergePlan is a reviewable, auditable decision document that:
// 1. Specifies the action for every field (keep, replace, merge, review, reject).
// 2. Cites evidence and reasoning for every decision.
// 3. Preserves provenance: source, evidence refs, confidence scores.
// 4. Ensures no silent mutation: every field decision is explicit and traceable.
// 5. Supports manual review workflows without auto-executing unsafe merges.
//
// Field precedence rules (in order):
// 1. If either record is approved, require human review.
// 2. If a field is critical (title, venue), check for significant conflicts.
// 3. If a field creates a safety blocker, mark RequireReview or RejectMerge.
// 4. If field values are identical, KeepExisting.
// 5. If both values exist and differ materially:
//    - For text: ReplaceWithCandidate (prefer newer ingestion)
//    - For time: TimeOverlapConflictResolver (requires review if > 12hrs)
//    - For location: GeoDistanceConflictResolver (requires review if > 10km)
// 6. If one value is null, use the non-null value.
// 7. Default: KeepExisting (conservative approach).
//
// Conflict escalation rules:
// - If temporal delta > 12 hours: ConflictType.TemporalDeviation → RequireReview
// - If geo distance > 10 km: ConflictType.GeoDeviation → RequireReview
// - If title divergence > 25%: ConflictType.TitleDivergence → RequireReview
// - If venue name divergence > 25%: ConflictType.VenueDivergence → RequireReview
// - If address mismatch & geo unresolved: ConflictType.LocationConflict → RequireReview
// - If blocking evidence gaps: ConflictType.EvidenceGap → RequireReview
// ============================================================================

// ---------------------------------------------------------------------------
// MergeFieldAction
// ---------------------------------------------------------------------------

/// <summary>
/// Field-level resolution action. Exactly one action is chosen per mergeable field.
/// Every action must cite why it was chosen (see MergeFieldDecision.Rationale).
/// </summary>
public enum MergeFieldAction
{
    /// <summary>
    /// Keep the canonical (existing) value.
    /// Used when: canonical is authoritative, incoming is incomplete or lower confidence,
    /// or field values are identical.
    /// Safe default: conservative and preserves approved event integrity.
    /// </summary>
    KeepExisting = 0,

    /// <summary>
    /// Replace the canonical value with the incoming candidate value.
    /// Used when: incoming is newer, higher-confidence, or corrects a canonical error.
    /// Requires: candidate field confidence ≥ canonical confidence OR
    ///           evidence suggests incoming is more reliable.
    /// </summary>
    ReplaceWithCandidate = 1,

    /// <summary>
    /// Merge both values (union, concatenation, or aggregate).
    /// Used for: tags, descriptions, evidence refs, source refs, media refs.
    /// NOT used for: identity fields (title, venue, time, address).
    /// Preserves: complete audit trail via UnionedSourceRefs, UnionedEvidenceRefs.
    /// </summary>
    MergeValues = 2,

    /// <summary>
    /// Cannot auto-decide; requires human review.
    /// Used when: safety blockers are active, conflict is material, or
    ///            confidence signals are ambiguous.
    /// Paired with: ConflictDescriptor explaining the reasoning.
    /// Downstream: moderation queue with full rationale for reviewer.
    /// </summary>
    RequireReview = 3,

    /// <summary>
    /// Reject the entire merge. Do not create or modify any canonical record.
    /// Used when: merge is unsafe, conflicts are irreconcilable, or
    ///            evidence suggests they are fundamentally different events.
    /// Paired with: ConflictDescriptor with detailed rejection reason.
    /// Downstream: ingest the candidate as a new independent event.
    /// </summary>
    RejectMerge = 4,
}

// ---------------------------------------------------------------------------
// ConflictType
// ---------------------------------------------------------------------------

/// <summary>
/// Enumeration of conflict categories. Used for targeting specific resolution logic
/// and categorizing review queue items.
/// </summary>
public enum ConflictType
{
    /// <summary>Event titles differ by > 25% normalized edit distance.</summary>
    TitleDivergence = 0,

    /// <summary>Venue names differ by > 25% normalized edit distance.</summary>
    VenueDivergence = 1,

    /// <summary>Start times differ by > 12 hours (720 minutes).</summary>
    TemporalDeviation = 2,

    /// <summary>End times differ by > 2 hours (if both are present).</summary>
    EndTemporalDeviation = 3,

    /// <summary>Geo distance between coordinates exceeds 10 km (10,000 meters).</summary>
    GeoDeviation = 4,

    /// <summary>Address strings differ significantly despite same geo coordinates (data error?).</summary>
    LocationConflict = 5,

    /// <summary>No timezone provided or timezones are materially different.</summary>
    TimezoneConflict = 6,

    /// <summary>Critical evidence is missing or incomplete from one record.</summary>
    EvidenceGap = 7,

    /// <summary>Source IDs or external IDs suggest different origin streams.</summary>
    SourceIdentityConflict = 8,

    /// <summary>Canonical event has IsApproved = true; human approval required for any mutation.</summary>
    ApprovedEventMutation = 9,

    /// <summary>Category information is missing or differs.</summary>
    CategoryMismatch = 10,

    /// <summary>Multiple unresolved conflicts or ambiguous safety signals.</summary>
    AmbiguousSafetySignal = 11,
}

// ---------------------------------------------------------------------------
// ConflictDescriptor
// ---------------------------------------------------------------------------

/// <summary>
/// Detailed description of a single conflict detected during merge planning.
/// Every conflict descriptor must include the field name, type, and human-readable
/// explanation suitable for a moderation queue UI or manual reviewer.
///
/// Invariant: A conflict descriptor is emitted whenever MergeFieldAction is
/// RequireReview or RejectMerge. Conflicts drive moderation workflow routing.
/// </summary>
public sealed record ConflictDescriptor(
    /// <summary>Field name where the conflict occurs (e.g., "StartUtc", "Title").</summary>
    string FieldName,

    /// <summary>Category of conflict (temporal deviation, geo distance, etc).</summary>
    ConflictType Type,

    /// <summary>
    /// The value from the canonical (existing) record.
    /// May be null if field was missing in canonical.
    /// </summary>
    string? CanonicalValue,

    /// <summary>
    /// The value from the incoming candidate record.
    /// May be null if field was missing in candidate.
    /// </summary>
    string? CandidateValue,

    /// <summary>
    /// Quantified delta or divergence metric.
    /// Examples:
    ///   - For temporal: absolute delta in minutes (720 = 12 hours)
    ///   - For geo: distance in meters (10000 = 10 km)
    ///   - For text: normalized edit distance ratio (0.0–1.0)
    /// Used to determine escalation severity.
    /// </summary>
    double DeltaMetric,

    /// <summary>Human-readable explanation of why this is a conflict and what it means.</summary>
    string Explanation,

    /// <summary>
    /// Why this conflict prevents auto-merge.
    /// Examples:
    ///   - "Temporal delta exceeds 12-hour threshold"
    ///   - "Geographic separation exceeds 10 km threshold"
    ///   - "Canonical event is approved; mutation requires human review"
    /// </summary>
    string BlockageReason,

    /// <summary>
    /// Suggestion for how to resolve the conflict (informational for reviewer).
    /// Examples:
    ///   - "Review event timestamps; confirm they refer to the same date"
    ///   - "Check geographic coordinates for data entry error"
    ///   - "Clarify if these are recurring events with multiple instances"
    /// </summary>
    string ResolutionHint);

// ---------------------------------------------------------------------------
// MergeFieldDecision
// ---------------------------------------------------------------------------

/// <summary>
/// Decision record for a single field during merge planning.
/// Every mergeable field gets exactly one MergeFieldDecision.
/// This drives the merge plan and audit trail.
/// </summary>
public sealed record MergeFieldDecision(
    /// <summary>Field name (e.g., "Title", "StartUtc", "Tags").</summary>
    string FieldName,

    /// <summary>The action taken for this field.</summary>
    MergeFieldAction Action,

    /// <summary>
    /// The value that will be retained in the merged record.
    /// - KeepExisting: value from canonical
    /// - ReplaceWithCandidate: value from candidate
    /// - MergeValues: union/aggregated result
    /// - RequireReview: null (deferred until human review)
    /// - RejectMerge: null (not applicable; merge rejected)
    /// </summary>
    string? ResultValue,

    /// <summary>
    /// Why this action was chosen. Includes:
    /// - Similarity/confidence scores if relevant
    /// - Threshold comparisons
    /// - Evidence weighting
    /// - Provenance or source priority
    /// Example: "Both values identical (100% match); conservative keep."
    /// </summary>
    string Rationale,

    /// <summary>
    /// If this field decision is contested, a detailed conflict descriptor.
    /// Null if the decision is unambiguous.
    /// </summary>
    ConflictDescriptor? Conflict);

// ---------------------------------------------------------------------------
// MergePlanDetail
// ---------------------------------------------------------------------------

/// <summary>
/// Comprehensive, auditable merge plan for a single (candidate, canonical) pair.
///
/// A MergePlan is NOT an execution plan: it is a proposal with full reasoning.
/// Downstream execution services consume this plan to:
/// 1. Auto-commit if plan.AutoMergeAllowed = true (all actions are safe).
/// 2. Enqueue to moderation if plan.RequiresManualReview = true.
/// 3. Reject the merge entirely if plan.RejectMerge = true.
///
/// Invariants:
/// - Every field decision has explicit rationale.
/// - Conflicts are cited with severity and resolution hints.
/// - Provenance (audit metadata) is comprehensive and immutable.
/// - The plan is deterministic: same inputs always produce identical plans.
/// </summary>
public sealed record MergePlanDetail(
    /// <summary>
    /// ID of the canonical EventAggregate (source of truth).
    /// Identifies which record will be updated (if merge commits).
    /// </summary>
    string CanonicalEventId,

    /// <summary>SourceRef of the incoming candidate being merged.</summary>
    string CandidateSourceRef,

    /// <summary>
    /// All field-level decisions. One per mergeable field.
    /// Keys are field names (Title, VenueName, StartUtc, etc).
    /// Values are MergeFieldDecision records.
    /// </summary>
    IReadOnlyDictionary<string, MergeFieldDecision> FieldDecisions,

    /// <summary>
    /// All detected conflicts. May be empty if no conflicts detected.
    /// Conflicts drive moderation queue routing and manual review needs.
    /// </summary>
    ConflictDescriptor[] Conflicts,

    /// <summary>
    /// True if plan can be auto-executed without human review.
    /// Requires:
    ///   - No RequireReview or RejectMerge actions
    ///   - No active safety blockers (from DuplicateAssessment)
    ///   - Canonical event is not approved, OR all actions are KeepExisting
    /// </summary>
    bool AutoMergeAllowed,

    /// <summary>
    /// True if plan should be routed to moderation queue for human review.
    /// Indicates: safety concerns or ambiguous conflicts, not technical errors.
    /// </summary>
    bool RequiresManualReview,

    /// <summary>
    /// True if the entire merge is rejected.
    /// Used when: records are fundamentally different or merge would corrupt data.
    /// Downstream action: ingest candidate as new independent event.
    /// </summary>
    bool RejectMerge,

    /// <summary>
    /// Ordered rationale for every major decision in the plan.
    /// Suitable for audit logs and moderation queue UI.
    /// Includes: field precedence applied, conflict detection rules, threshold comparisons.
    /// </summary>
    string[] MergeRationale,

    /// <summary>
    /// If RequiresManualReview = true, reasons why human review is needed.
    /// Examples:
    ///   - "Temporal delta (720 minutes) exceeds auto-merge threshold"
    ///   - "Canonical event is approved; mutation requires approval"
    ///   - "Title divergence suggests possible different events"
    /// </summary>
    string[] ManualReviewReasons,

    /// <summary>
    /// Complete audit metadata for plan creation and provenance.
    /// </summary>
    MergePlanAudit Audit);

// ---------------------------------------------------------------------------
// MergePlanAudit
// ---------------------------------------------------------------------------

/// <summary>
/// Audit trail and provenance for a merge plan.
/// Ensures full traceability of how and why the plan was created.
/// </summary>
public sealed record MergePlanAudit(
    /// <summary>
    /// Complete DuplicateAssessment from the deduplication strategy.
    /// Provides scoring breakdown, rationale, and safety verdict.
    /// </summary>
    DuplicateAssessment Assessment,

    /// <summary>
    /// The incoming NormalizedEventCandidate being considered for merge.
    /// Preserved for reference and re-planning if logic changes.
    /// </summary>
    NormalizedEventCandidate IncomingCandidate,

    /// <summary>
    /// The canonical EventAggregateSnapshot being compared against.
    /// Preserved for reference and conflict analysis.
    /// </summary>
    EventAggregateSnapshot CanonicalSnapshot,

    /// <summary>
    /// Field precedence rules applied (as human-readable names).
    /// Examples:
    ///   - "rule_approved_protection"
    ///   - "rule_conflicting_times"
    ///   - "rule_identical_values"
    /// Allows tracing plan changes if rules are updated.
    /// </summary>
    string[] AppliedPrecedenceRules,

    /// <summary>
    /// UTC instant at which the plan was generated.
    /// </summary>
    DateTimeOffset CreatedAtUtc,

    /// <summary>
    /// Semantic version of the merge planning algorithm used.
    /// Allows tracking reproducibility and plan re-validation.
    /// Example: "1.0" (initial M2-P09 implementation).
    /// </summary>
    string MergePlannerVersion = "1.0");

// ---------------------------------------------------------------------------
// IMergePlanner Interface
// ---------------------------------------------------------------------------

/// <summary>
/// Service interface for creating merge plans.
///
/// DESIGN CONTRACT
/// ───────────────
/// The merge planner:
/// 1. Is PURE: no side effects, no I/O, no persistence.
/// 2. Takes DuplicateAssessment (which cites scoring and safety) as input.
/// 3. Produces a comprehensive, reviewable MergePlanDetail.
/// 4. Never auto-commits: all actions are recommendations, not executions.
/// 5. Preserves complete provenance: assessment, candidates, rules applied.
///
/// Downstream consumers (IEntityResolutionService, moderation queue) use the plan to:
/// - Auto-execute merge if plan.AutoMergeAllowed = true.
/// - Route to review if plan.RequiresManualReview = true.
/// - Reject if plan.RejectMerge = true.
/// </summary>
public partial interface IMergePlanner
{
    /// <summary>
    /// Generate a comprehensive, auditable merge plan for a single pair:
    /// incoming candidate vs. canonical event.
    /// </summary>
    /// <param name="incoming">The normalized candidate from ingestion.</param>
    /// <param name="canonical">The canonical event snapshot for comparison.</param>
    /// <param name="assessment">The complete deduplication assessment (scoring, safety).</param>
    /// <returns>
    /// A detailed, reviewable merge plan with full field decisions, conflicts, and audit trail.
    /// Never returns null.
    /// </returns>
    MergePlanDetail CreatePlan(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical,
        DuplicateAssessment assessment);
}
