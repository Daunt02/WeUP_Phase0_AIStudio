# M2-P09: Merge Planning and Conflict Resolution System v1.0

## Overview

The Merge Planning System is a **deterministic, auditable** layer that sits between duplicate assessment and merge execution. Its purpose is to produce safe, reviewable merge plans without blindly mutating canonical events.

**Core Principle:** Merge planning is SEPARATE from merge execution. This class generates comprehensive proposals; downstream services decide whether to auto-commit, route to review, or reject.

---

## Architecture

### Components

#### 1. **MergeFieldAction Enum**

Field-level resolution actions:

- `KeepExisting` — Use canonical value (safe, conservative default)
- `ReplaceWithCandidate` — Replace canonical with incoming (prefer newer/higher-confidence)
- `MergeValues` — Union/append both values (for tags, descriptions, refs)
- `RequireReview` — Cannot auto-decide; escalate to moderation queue
- `RejectMerge` — Reject entire merge; ingest candidate as new event

#### 2. **ConflictType Enum**

Categorizes detected conflicts for targeted resolution:

- `TitleDivergence` — Title > 25% different
- `VenueDivergence` — Venue name > 25% different
- `TemporalDeviation` — Start time delta > 12 hours
- `EndTemporalDeviation` — End time delta > 2 hours
- `GeoDeviation` — Geographic distance > 10 km
- `LocationConflict` — Address mismatch despite same geo coordinates
- `TimezoneConflict` — Timezone missing or materially different
- `EvidenceGap` — Critical evidence missing from one record
- `SourceIdentityConflict` — Same source ID but titles differ materially
- `ApprovedEventMutation` — Canonical is approved; requires human authorization
- `CategoryMismatch` — Category information differs or missing
- `AmbiguousSafetySignal` — Multiple unresolved conflicts

#### 3. **ConflictDescriptor Record**

Fully detailed description of a single conflict:

```csharp
string FieldName,           // e.g., "StartUtc"
ConflictType Type,          // Category of conflict
string? CanonicalValue,     // Value from canonical record
string? CandidateValue,     // Value from incoming candidate
double DeltaMetric,         // Quantified divergence (minutes, meters, ratio)
string Explanation,         // Human-readable conflict description
string BlockageReason,      // Why it prevents auto-merge
string ResolutionHint       // Suggestion for reviewer
```

#### 4. **MergeFieldDecision Record**

Decision for a single field:

```csharp
string FieldName,                   // Field identifier
MergeFieldAction Action,            // Action taken
string? ResultValue,                // Resulting value (if applicable)
string Rationale,                   // Why this action was chosen
ConflictDescriptor? Conflict        // Conflict details (if any)
```

#### 5. **MergePlanDetail Record**

Comprehensive, auditable merge plan:

```csharp
string CanonicalEventId,                        // Which event to update
string CandidateSourceRef,                      // Incoming source
IReadOnlyDictionary<string, MergeFieldDecision> FieldDecisions, // All field choices
ConflictDescriptor[] Conflicts,                 // Detected conflicts
bool AutoMergeAllowed,                          // Safe to auto-execute?
bool RequiresManualReview,                      // Route to moderation?
bool RejectMerge,                               // Reject entirely?
string[] MergeRationale,                        // Full audit trail
string[] ManualReviewReasons,                   // Why review needed
MergePlanAudit Audit                            // Complete provenance
```

#### 6. **MergePlanAudit Record**

Preserves complete provenance:

```csharp
DuplicateAssessment Assessment,                 // Scoring & safety verdict
NormalizedEventCandidate IncomingCandidate,     // What was being merged
EventAggregateSnapshot CanonicalSnapshot,       // What it compared against
string[] AppliedPrecedenceRules,                // Rules applied
DateTimeOffset CreatedAtUtc,                    // When plan was created
string MergePlannerVersion                      // Algorithm version (for reproducibility)
```

#### 7. **IMergePlanner Interface**

Pure service interface (no I/O, no side effects):

```csharp
public interface IMergePlanner
{
    MergePlanDetail CreatePlan(
        NormalizedEventCandidate incoming,
        EventAggregateSnapshot canonical,
        DuplicateAssessment assessment);
}
```

#### 8. **MergePlanner Implementation**

The full decision engine, applied in LAYERS:

---

## Decision Algorithm

### Layer 1: Safety Overrides

**Approved Event Protection**

- If `canonical.IsApproved = true`:
  - ALL field mutations marked `RequireReview`
  - Rationale: Write-protected records require human authorization
  - Applied rule: `rule_approved_protection`

### Layer 2: Conflict Detection

**Temporal Dimension**

- If `|StartUtc(canonical) - StartUtc(incoming)| > 720 minutes`:
  - Conflict: `TemporalDeviation`
  - Action: `RequireReview` for start/end times
  - Resolution hint: "Verify time zone and date conversions match"

**Geographic Dimension**

- If `HaversineDistance(canonical, incoming) > 10,000 meters`:
  - Conflict: `GeoDeviation`
  - Action: `RequireReview` for address
  - Resolution hint: "Check for geocoding errors or multiple venue branches"

**Text Dimensions (Title, Venue, Address)**

- If `TextSimilarity(canonical, incoming) < 0.75` (i.e., > 25% divergence):
  - Conflict: `TitleDivergence` / `VenueDivergence`
  - Action: `RequireReview` for affected field
  - Resolution hint: "Compare texts carefully; confirm they refer to the same event"

- For Address (more lenient, 40% threshold):
  - If `TextSimilarity < 0.60`:
    - Conflict: `LocationConflict`

### Layer 3: Value Comparison

For each field, after conflict detection:

1. **Both null** → `KeepExisting` (no change)
2. **Only canonical** → `KeepExisting` (safe)
3. **Only incoming** → `ReplaceWithCandidate` (fill gap)
4. **Identical** → `KeepExisting` (no decision needed)
5. **Different + conflict detected** → `RequireReview`
6. **Different + confidence gap** → Check layer 4

### Layer 4: Confidence Weighting

If incoming confidence exceeds canonical by > 10 percentage points:

- `ReplaceWithCandidate` (prefer newer/more reliable extraction)
- Example: Incoming ExtractionConfidence=0.95 vs Canonical=0.80 → replace

Otherwise:

- `KeepExisting` (conservative default preserves approved event integrity)

### Layer 5: Non-Critical Field Merging

For tags, descriptions, source refs, evidence refs:

- `MergeValues` (union/append to create complete record)
- Rationale: "Preserve complete audit trail and categorization"

---

## Result Determination

### AutoMergeAllowed

Condition: All of the following must be true:

- No field action is `RequireReview` or `RejectMerge`
- No safety blockers active (from `DuplicateAssessment.SafetyVerdict`)
- Canonical is NOT approved, OR all actions are `KeepExisting`
- `DuplicateAssessment.AutoMergeAllowed = true`

### RequiresManualReview

Triggered by:

- Any field action is `RequireReview`
- Any conflicts detected (count > 0)
- Safety blockers active
- Canonical is approved

### RejectMerge

Triggered by:

- Any field action is `RejectMerge`
- This causes entire plan to be rejected; candidate ingested as new event

---

## Usage Example

```csharp
// Assume you have:
var incomingCandidate = new NormalizedEventCandidate(...);
var existingEvent = new EventAggregateSnapshot(...);
var assessment = deduplicationStrategy.Assess(incomingCandidate, existingEvent);

// Create merge plan
var planner = new MergePlanner();
var plan = planner.CreatePlan(incomingCandidate, existingEvent, assessment);

// Route based on plan
if (plan.RejectMerge)
{
    // Ingest candidate as new independent event
    await CreateNewEventAsync(incomingCandidate);
}
else if (plan.AutoMergeAllowed)
{
    // Auto-execute merge
    await MergeEventsAsync(plan);
}
else if (plan.RequiresManualReview)
{
    // Enqueue to moderation queue with full context
    await EnqueueModerationAsync(plan, plan.ManualReviewReasons);
}
```

---

## Field Precedence Table

| Field        | Identity-Critical? | Conflict Threshold | Default Action | If Identical | If Conflict   |
| ------------ | ------------------ | ------------------ | -------------- | ------------ | ------------- |
| Title        | ✅ Yes             | 25% divergence     | KeepExisting   | KeepExisting | RequireReview |
| VenueName    | ✅ Yes             | 25% divergence     | KeepExisting   | KeepExisting | RequireReview |
| Address      | ✅ Yes             | 40% divergence     | KeepExisting   | KeepExisting | RequireReview |
| StartUtc     | ✅ Yes             | 720 min (12 hrs)   | KeepExisting   | KeepExisting | RequireReview |
| EndUtc       | ✅ Yes             | 120 min (2 hrs)    | KeepExisting   | KeepExisting | RequireReview |
| Timezone     | ❌ No              | N/A                | KeepExisting   | KeepExisting | KeepExisting  |
| Category     | ❌ No              | N/A                | KeepExisting   | KeepExisting | KeepExisting  |
| Description  | ❌ No              | N/A                | MergeValues    | MergeValues  | MergeValues   |
| Tags         | ❌ No              | N/A                | MergeValues    | MergeValues  | MergeValues   |
| SourceRefs   | ❌ No              | N/A                | MergeValues    | MergeValues  | MergeValues   |
| EvidenceRefs | ❌ No              | N/A                | MergeValues    | MergeValues  | MergeValues   |

---

## Safety Mechanisms

### 1. **Approved Event Protection**

- If `canonical.IsApproved = true`, ALL mutations require human review
- Prevents accidental data loss from curator-approved events
- Can be overridden only via explicit human authorization in moderation UI

### 2. **Conflict Escalation**

- Conflicts are NOT silently resolved; they bubble up to `MergeRationale`
- Every conflict triggers `RequireReview` for affected field
- Reviewers see delta metric (temporal minutes, geo kilometers, text divergence %)

### 3. **Conservative Defaults**

- When in doubt: `KeepExisting` (preserves canonical integrity)
- Only `ReplaceWithCandidate` if confidence significantly higher
- Non-identity fields (tags, descriptions) are merged (union) to preserve information

### 4. **Complete Audit Trail**

- Every decision cites why it was chosen (similarity scores, confidence gaps, rules applied)
- `MergeRationale` array is suitable for audit logs and moderation UI
- `Audit` includes original assessment, candidates, and rules applied for reproducibility

### 5. **Provenance Preservation**

- All source refs, evidence refs, and tags are unioned
- No data is discarded; all information flows into merged event
- Moderation can trace decisions back to original assessment

---

## Threshold Constants

| Constant                               | Value          | Field           | Rationale                          |
| -------------------------------------- | -------------- | --------------- | ---------------------------------- |
| `TextDivergenceThreshold`              | 0.25 (25%)     | Title, Venue    | Above 25%, likely different events |
| `TemporalDivergenceThresholdMinutes`   | 720 (12 hrs)   | Start/End time  | Different-day events               |
| `GeoDivergenceThresholdMeters`         | 10,000 (10 km) | Geo coordinates | Different neighborhoods            |
| `ConfidenceGapThresholdForReplacement` | 0.10 (10 pp)   | All fields      | Incoming clearly more reliable     |

**Modification**: These can be adjusted if business rules change. The implementation tracks applied thresholds in `MergePlanAudit.AppliedPrecedenceRules` for full traceability.

---

## Future Enhancements

1. **Custom Field Mappings**: Support domain-specific fields (media URLs, custom attributes)
2. **Weighted Confidence Models**: Incorporate field-level confidence vectors instead of scalar values
3. **Machine Learning Integration**: Learn conflict patterns from human review decisions
4. **Async Evidence Validation**: Check evidence refs for completeness before approving merge
5. **Category-Specific Rules**: Different thresholds for different event types (concerts vs. meetups)
6. **Temporal Variance Handling**: Account for recurring events across multiple dates

---

## Testing

See `WeUP.Tests/Dedupe/MergePlannerTests.cs` for comprehensive unit tests covering:

- Conflict detection thresholds
- Field decision precedence
- Approved event protection
- Confidence weighting
- Complete plan generation and audit trails

---

## Integration Checklist

- [ ] Register `MergePlanner` in DI container
- [ ] Update entity resolution service to use new `MergePlanDetail`
- [ ] Wire moderation queue to consume `plan.RequiresManualReview = true`
- [ ] Update merge execution to check `plan.AutoMergeAllowed` before committing
- [ ] Add plan rationale to audit logs and moderation UI
- [ ] Test approved event mutation rejection
- [ ] Validate conflict detection thresholds with production data
- [ ] Document moderation queue UI with hint text from conflict descriptors

---

## Design Rationale

### Why Separate Planning from Execution?

- **Reversibility**: Plans can be reviewed before committing
- **Auditability**: Full decision trail preserved even if merge is rejected
- **Flexibility**: Moderation UI can modify plan before execution
- **Safety**: No side effects during planning phase

### Why Immutable Records?

- **Thread-safety**: Plans can be shared across async contexts
- **Reproducibility**: Same inputs always produce identical plans
- **Durability**: Plans can be persisted and re-evaluated later

### Why Explicit Rationale for Every Decision?

- **Transparency**: No black-box decisions
- **Debugging**: Easy to trace why a merge was auto-approved or rejected
- **Learning**: Data for improving thresholds and rules
- **Legal**: Audit trail for regulatory compliance

---

## References

- `DeduplicationStrategyContracts.cs` — DuplicateAssessment, scoring model
- `WeightedDeduplicationStrategy.cs` — Score generation and safety verdict
- `IngestionContracts.cs` — NormalizedEventCandidate structure
- `ResolutionContracts.cs` — MergePlan legacy contract (being extended)

---

**Version**: 1.0
**Date**: 2026-04-17
**Status**: Production-Ready
