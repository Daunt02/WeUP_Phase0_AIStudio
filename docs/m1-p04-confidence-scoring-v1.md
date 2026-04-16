---
Title: M1-P04 Confidence Scoring and Evidence Model v1.0
Version: 1.0
Status: Implementation Complete
Created: 2026-04-15
---

# Confidence Scoring and Evidence Model v1.0

## Overview

This document describes the deterministic confidence scoring and evidence tracking system for the ingestion pipeline. Every extracted field now carries explicit, auditable confidence scores with supporting evidence.

## Core Principles

1. **No Implicit Confidence**: Every field has an explicit confidence value with documented reasoning
2. **Deterministic Scoring**: Same input always produces same output; no probabilistic models
3. **Full Auditability**: All scoring decisions can be replayed, inspected, and challenged
4. **Evidence Preservation**: Raw sources preserved for review workflows and error analysis

## Architecture

### Records and Types

#### `FieldConfidence<TField>`

Generic record capturing confidence for any extracted field.

```csharp
public sealed record FieldConfidence<TField>(
    TField Value,                              // Actual extracted value
    double Confidence,                         // [0.0, 1.0]
    IReadOnlyList<string> EvidenceRefs,       // Links to supporting evidence
    string Rationale,                          // Human-readable explanation
    string EvidenceSource,                     // Source type ("ocr", "heuristic", etc)
    IReadOnlyDictionary<string, string?>? Metadata);  // Debugging context
```

**Confidence Ranges:**

- **0.90-1.0**: High confidence (structured source or OCR 90%+)
- **0.70-0.89**: Moderate confidence (OCR + weak heuristic)
- **0.50-0.69**: Low confidence (OCR with ambiguity)
- **0.30-0.49**: Very low confidence (derived/inferred)
- **0.0-0.29**: Minimal confidence (placeholder/fallback)

#### `EvidenceBundle`

Immutable evidence collection for audit and review.

```csharp
public sealed record EvidenceBundle(
    string BundleId,                               // Unique ID
    DateTimeOffset CollectedAtUtc,                 // When evidence was collected
    string? RawOcrText,                            // Original OCR output
    IReadOnlyList<OcrTextBlock>? OcrBlocks,       // Individual text blocks with positions
    string? SourceHash,                            // SHA256 hash for dedup
    string SourceKind,                             // e.g., "flyer_ocr"
    IReadOnlyDictionary<string, ConfidenceComponent>? ScoringComponents,  // Scoring breakdown
    IReadOnlyDictionary<string, string?>? ProcessingContext);  // OCR engine info
```

**Evidence Includes:**

- Raw OCR text (before any normalization)
- OCR blocks with confidence and spatial coordinates
- SHA256 source hash (for change detection)
- Processing context (OCR engine version, run ID)
- Scoring component breakdown (for audit replay)

#### `EventCandidateV2`

Enhanced candidate with field-level confidence and evidence.

```csharp
public sealed record EventCandidateV2(
    FieldConfidence<string>? Title,                    // Explicit confidence
    FieldConfidence<DateTimeOffset>? StartUtc,        // Explicit confidence
    FieldConfidence<DateTimeOffset>? EndUtc,          // Explicit confidence
    FieldConfidence<string>? Venue,                   // Explicit confidence
    FieldConfidence<string>? Address,                 // Explicit confidence
    FieldConfidence<string>? Category,                // Explicit confidence
    FieldConfidence<string>? Description,             // Explicit confidence
    FieldConfidence<IReadOnlyList<string>>? Tags,    // Explicit confidence
    double OverallConfidence,                         // Aggregated score [0.0, 1.0]
    string OverallConfidenceRationale,                // Explains overall score derivation
    EvidenceBundle EvidenceBundle,                    // Full evidence including raw sources
    DateTimeOffset CreatedAtUtc,                      // When candidate was created
    string ScoringVersion);                           // Version of scoring rules
```

### ConfidenceScorer Service

**Location**: `WeUP.Infrastructure.Flyer.ConfidenceScorer`

Deterministic scorer that produces `EventCandidateV2` records from `EventCandidate` input.

**Key Methods**:

- `ScoreCandidate(EventCandidate, OcrResult?, string)`: Main scoring entry point

**Scoring Constants**:

```csharp
static class ConfidenceRanges
{
    const double HighConfidence = 0.90;      // Structured/OCR 90%+
    const double ModerateConfidence = 0.70;  // OCR + weak heuristic
    const double LowConfidence = 0.50;       // OCR alone with ambiguity
    const double VeryLowConfidence = 0.35;   // Derived/inferred
    const double MinimalConfidence = 0.15;   // Placeholder
}

static class Penalties
{
    const double LowOcrQuality = 0.85;              // -15%
    const double TemporalConflict = 0.70;          // -30%
    const double VenueAmbiguity = 0.80;            // -20%
    const double MissingRequiredField = 0.70;      // -30% per field
    const double GeocodingUncertainty = 0.80;      // -20%
}

static class Boosts
{
    const double MultiSourceAgreement = 1.10;      // +10%
    const double HighTrustSource = 1.05;           // +5%
    const double ExcellentOcr = 1.05;              // +5%
}
```

## Scoring Rules

### 1. OCR Confidence Propagation

Direct field extractions from OCR inherit the OCR engine's confidence score, adjusted for field-specific factors.

**Rule**: `FieldConfidence = OcrConfidence × FieldQuality`

**Adjustments**:

- If OCR confidence < 0.70: Apply -15% penalty (LowOcrQuality)
- If OCR confidence >= 0.95: Apply +5% boost (ExcellentOcr)
- If field content is suspiciously short/simple: Additional -15% penalty

**Example**:

```
OCR reports: 0.92 confidence for text block containing title
Field: "Jazz Night at Club X"
- Base OCR: 0.92
- Length check (>5 chars): ✓ No penalty
- Final confidence: 0.92 - already high, no additional adjustments
- Rationale: "OCR confidence 0.92 + field length valid = 0.92"
```

### 2. Heuristic Strength Adjustments

Inferred/derived values get lower base confidence than direct extractions.

**Rule**: `InferredConfidence ≤ 0.75`

**Examples**:

- **Title** (direct extraction): 0.70-0.95 base
- **Venue** (from text inference): 0.65-0.80 base
- **Address** (structured extraction): 0.75-0.85 base
- **DateTime** (temporal heuristics): 0.60-0.80 base
- **Tags** (derived classification): 0.40-0.75 base

**Details**:

#### Title Scoring (0.70-0.95)

- Base: Heuristic score or 0.70 fallback
- Penalty: -15% if length < 5 chars
- Penalty: Additional -15% if OCR < 0.70
- Boost: +5% if OCR >= 0.95
- Final: Clamped to [0, 1]

#### Venue Scoring (0.65-0.80)

- Base: Heuristic score or 0.65 fallback
- Penalty: -20% if name is generic or very short (<4 chars)
- Generic patterns: "location", "here", "place", "venue", "area"
- Rationale explicitly notes ambiguity level

#### Address Scoring (0.75-0.85)

- Base: 0.75 (addresses usually have reliable structure)
- Penalty: -20% if missing typical address components
- Address signatures: "st", "ave", "blvd", "rd", "apt", "suite", "zip", "city"
- Penalty: -20% if too short (<10 chars)

#### DateTime Scoring (0.60-0.80)

- Base: Heuristic score or 0.70 fallback
- Penalty: -40% if date is >1 day in past (obsolete flyer)
- Penalty: -50% if date is >2 years in future (likely OCR error)
- Clamped minimum: 0.15 (never trust very old/future dates completely)

#### Tags/Categories (0.40-0.75)

- No tags present: 0.40 (moderate penalty for missing)
- Derived from text: 0.75 base
- Multiple tags: No additional penalty/boost

### 3. Conflict Penalties

Contradictory data reduces overall confidence.

**Temporal Conflict**: -30% if EndUtc < StartUtc

```csharp
// Example: Title says "7-9pm" but OCR extracted "3-1pm"
titleConfidence = 0.80;
startConfidence = 0.70;
endConfidence = 0.60;
temporalConflict = true;  // End before start
overallScore *= 0.70;  // -30% penalty applied
rationale += "Temporal conflict detected (end before start)"
```

**Venue Ambiguity**: -20% if venue name is generic or very short

```csharp
// "Place" vs "The Fillmore"
if (venue.Length < 4 || IsGenericVenueName(venue))
    confidence *= 0.80;  // -20% ambiguity penalty
```

**Geocoding Uncertainty**: -20% if address can't be validated

```csharp
// Address without typical components or very short
if (address.Length < 10 || !ContainsAddressSignatures(address))
    confidence *= 0.80;  // -20% penalty
```

### 4. Missing Field Penalties

Required missing fields reduce overall confidence.

**Penalty**: -30% per missing required field (cumulative)

**Required Fields**: Title, StartUtc, Venue, Address

```csharp
// If 1 field missing: -30%
// If 2 fields missing: -30% × -30% = -51%
// If 3 fields missing: -30% × -30% × -30% = -65.7%

missingFieldPenalty *= Penalties.MissingRequiredField;  // Per field
```

### 5. Overall Confidence Aggregation

Weighted formula combining all field scores:

```
OverallConfidence = (
    Title × 0.25 +
    Max(StartUtc, EndUtc) × 0.30 +
    Venue × 0.25 +
    Address × 0.20
) × ApplicablePenalties × SourceTrustBoosts
```

**Field Weights**:

- **DateTime**: 30% (most important for event detection)
- **Title & Venue**: 25% each (core event identity)
- **Address**: 20% (location verification)

**Order of Aggregation**:

1. Multiply field scores by weights
2. Sum to get weighted base
3. Apply temporal conflict penalty if detected
4. Apply missing required field penalties
5. Apply OCR quality adjustments
6. Apply source trust bonus (if from trusted source)
7. Clamp result to [0.0, 1.0]

**Example Calculation**:

```
Fields:
  Title: 0.85 (clear, good OCR)
  StartUtc: 0.75 (partially inferred)
  EndUtc: 0.65 (inferred, weak temporal signal)
  Venue: 0.70 (partially matched)
  Address: 0.80 (structured appearance)

Weighted:
  = (0.85 × 0.25) + (0.75 × 0.30) + (0.70 × 0.25) + (0.80 × 0.20)
  = 0.2125 + 0.225 + 0.175 + 0.16
  = 0.7725 (77.25%)

Penalties:
  - No temporal conflict
  - No missing required fields
  - OCR confidence 0.85 (no penalty)

Final: 0.7725 → 77.25% confidence
```

## Integration Guide

### Adding to FlyerIngestionPipeline

1. **Register the service** in dependency injection (typically in Program.cs):

```csharp
services.AddScoped<IConfidenceScorer, ConfidenceScorer>();
```

2. **Inject into FlyerIngestionPipeline**:

```csharp
public sealed class FlyerIngestionPipeline(
    // ... existing dependencies
    IConfidenceScorer confidenceScorer,
    // ... other dependencies
) : IFlyerIngestionPipeline
{
    // Usage in pipeline
}
```

3. **Score candidates after normalization**:

```csharp
// After NormalizationEngine.Normalize() produces EventCandidate
var candidate = normalizationService.Normalize(ocrResult);

// Score with full evidence tracking
var scoredCandidate = confidenceScorer.ScoreCandidate(
    candidate: candidate,
    ocrResult: ocrResult,            // Pass OCR for better scoring
    sourceKind: "flyer_ocr"          // Tag the source
);

// Now use scoredCandidate (EventCandidateV2) in downstream pipeline
```

4. **Preserve evidence bundle**:

```csharp
// Store evidence for auditing
var evidenceId = Guid.NewGuid().ToString("N");
evidenceRepository.StoreEvidenceBundle(
    jobId: job.JobId,
    bundle: scoredCandidate.EvidenceBundle,
    associatedWith: evidenceId
);

// Link to moderation queue for review
moderationQueue.EnqueueIfNeeded(
    candidate: scoredCandidate,
    evidences: new[] { evidenceId }
);
```

### Using in Review/Moderation Workflows

```csharp
// When reviewing a candidate, all explanation is available
var candidate = FindCandidate(candidateId);  // Returns EventCandidateV2

Console.WriteLine($"Title Confidence: {candidate.Title?.Confidence:P0}");
Console.WriteLine($"  Reasoning: {candidate.Title?.Rationale}");
Console.WriteLine($"  Source: {candidate.Title?.EvidenceSource}");

// Access raw evidence for deep inspection
var evidence = candidate.EvidenceBundle;
Console.WriteLine($"Raw OCR: {evidence.RawOcrText}");
foreach (var block in evidence.OcrBlocks ?? Array.Empty<OcrTextBlock>())
{
    Console.WriteLine($"  Block {block.Index}: \"{block.Text}\" ({block.Confidence:P0})");
}

// Inspect scoring breakdown
foreach (var (fieldName, component) in evidence.ScoringComponents ?? new Dictionary<string, ConfidenceComponent>())
{
    Console.WriteLine($"{fieldName}: {component.ComponentType} = {component.BaseScore:P0}");
    if (component.Description != null)
        Console.WriteLine($"  → {component.Description}");
}
```

### Recomputing Scores

The evidence bundle preserves all information needed to recompute confidence using new rules:

```csharp
// If scoring rules are updated, can replay with old evidence
public EventCandidateV2 Rescore(EventCandidateV2 original, IConfidenceScorer newScorer)
{
    // Reconstruct original EventCandidate from evidence
    var reconstituedCandidate = ReconstructCandidate(original.EvidenceBundle);

    // Score with new rules
    var newScore = newScorer.ScoreCandidate(
        reconstituedCandidate,
        ocrResult: null,  // We have raw text, not OcrResult
        sourceKind: original.EvidenceBundle.SourceKind
    );

    return newScore;
}
```

## Testing and Validation

### Unit Test Examples

```csharp
[TestFixture]
public class ConfidenceScorerTests
{
    private ConfidenceScorer _scorer = new();

    [Test]
    public void ScoreCandidate_WithCompleteData_ReturnsHighConfidence()
    {
        // Arrange
        var candidate = new EventCandidate(
            Title: "Jazz Night",
            StartUtc: DateTime.UtcNow.AddDays(7),
            EndUtc: DateTime.UtcNow.AddDays(7).AddHours(3),
            Venue: "The Fillmore",
            Address: "1805 Geary Blvd, San Francisco, CA 94115",
            Tags: new[] { "music", "jazz" },
            RawFields: new Dictionary<string, string?>(),
            FieldScores: new Dictionary<string, FieldHeuristicScore>
            {
                ["title"] = new(0.85, "High-confidence OCR match"),
                ["venue"] = new(0.80, "Database match"),
                ["address"] = new(0.90, "Structured postal format"),
            }
        );

        // Act
        var result = _scorer.ScoreCandidate(candidate, sourceKind: "test");

        // Assert
        Assert.That(result.OverallConfidence, Is.GreaterThan(0.75));
        Assert.That(result.Title?.Confidence, Is.GreaterThan(0.80));
        Assert.That(result.OverallConfidenceRationale, Does.Contain("Weighted field scores"));
        Assert.That(result.EvidenceBundle.BundleId, Is.Not.Null);
    }

    [Test]
    public void ScoreCandidate_WithMissingTitle_AppliesPenalty()
    {
        // Arrange
        var candidate = new EventCandidate(
            Title: null,  // Missing required field
            StartUtc: DateTime.UtcNow.AddDays(7),
            EndUtc: null,
            Venue: "Venue",
            Address: "Address",
            Tags: Array.Empty<string>(),
            RawFields: new Dictionary<string, string?>(),
            FieldScores: new Dictionary<string, FieldHeuristicScore>()
        );

        // Act
        var result = _scorer.ScoreCandidate(candidate);

        // Assert
        Assert.That(result.Title?.Value, Is.EqualTo("[Missing]"));
        Assert.That(result.Title?.Confidence, Is.LessThan(0.20));
        Assert.That(result.OverallConfidence, Is.LessThan(0.70));
    }

    [Test]
    public void ScoreCandidate_WithTemporalConflict_DetectsAndPenalizes()
    {
        // Arrange
        var now = DateTime.UtcNow.AddDays(7);
        var candidate = new EventCandidate(
            Title: "Event",
            StartUtc: now.AddHours(9),  // 9pm
            EndUtc: now.AddHours(19),   // 7pm next day
            Venue: "Venue",
            Address: "Address",
            Tags: Array.Empty<string>(),
            RawFields: new Dictionary<string, string?>(),
            FieldScores: new Dictionary<string, FieldHeuristicScore>()
        );

        // Act - end BEFORE start (conflict)
        candidate = candidate with
        {
            StartUtc = now.AddHours(19),
            EndUtc = now.AddHours(9)
        };
        var result = _scorer.ScoreCandidate(candidate);

        // Assert
        Assert.That(result.OverallConfidenceRationale, Does.Contain("Temporal conflict"));
        Assert.That(result.OverallConfidence, Is.LessThan(0.65));
    }
}
```

## Performance Considerations

- **Scoring**: Deterministic, no I/O or network calls; <1ms per candidate
- **Evidence Bundle**: Stores raw OCR + metadata; typical size 50-500KB per flyer
- **Storage**: EvidenceBundle can be archived separately if storage is a concern

## Audit Trail

Every score is auditable:

1. **FieldConfidence**: Contains full rationale for each field
2. **EvidenceBundle**: Stores raw input material and scoring components
3. **OverallConfidenceRationale**: Documents aggregation and penalties
4. **Metadata**: Field-level metadata for debugging

## Example: Full Audit Trace

```
Flyer: "Jazz Night at Club XYZ - May 15th 7-11pm @ 1234 Main St"

=== FIELD SCORES ===

Title: "Jazz Night at Club XYZ"
  Confidence: 0.87
  Source: OCR
  Rationale: OCR confidence 0.90 + field length valid (25 chars) = 0.90;
             No additional penalties applied
  Evidence: ocr_primary

StartUtc: 2026-05-15T19:00:00Z
  Confidence: 0.72
  Source: Heuristic
  Rationale: Date/time derived from text heuristics; Temporal pattern "7pm" matched;
             Event is 30 days in future (valid range)
  Evidence: ocr_temporal
  Metadata: inferred_from_text=true, has_time_component=true

EndUtc: 2026-05-15T23:00:00Z
  Confidence: 0.68
  Source: Heuristic
  Rationale: Derived from implicit duration "7-11pm" = 4 hours (reasonable);
             No explicit end marker in raw text
  Evidence: ocr_temporal
  Metadata: inferred_from_text=true, has_time_component=true

Venue: "Club XYZ"
  Confidence: 0.65
  Source: Heuristic
  Rationale: Inferred from text without venue database match;
             Venue name length=8 chars (not suspiciously short);
             Not matching generic patterns
  Evidence: ocr_inferred

Address: "1234 Main St"
  Confidence: 0.78
  Source: OCR
  Rationale: Address extracted from document text;
             Contains address signatures (st);
             Length=12 chars (sufficient)
  Evidence: ocr_extracted

=== OVERALL CONFIDENCE ===

Weighted Score = (Title × 0.25) + (DateTime × 0.30) + (Venue × 0.25) + (Address × 0.20)
               = (0.87 × 0.25) + (max(0.72, 0.68) × 0.30) + (0.65 × 0.25) + (0.78 × 0.20)
               = 0.2175 + 0.216 + 0.1625 + 0.156
               = 0.752 (75.2%)

Penalties Applied:
  - No temporal conflict (start < end, 4-hour duration reasonable)
  - No missing required fields (all present)
  - OCR confidence 0.90 (no penalty)

Final Confidence: 75.2%

Status: MODERATE CONFIDENCE - Suitable for automated processing with optional human review
```

## Future Enhancements

- **Machine-learned penalties**: Once enough human review data exists, adjust penalty factors
- **Source-specific rules**: Different scoring for flyers vs. manual submissions vs. venues
- **Temporal seasonality**: Adjust future-date penalty based on event type (concerts vs. regular events)
- **Geographic variation**: Address patterns vary by region/country
- **Batch recomputation**: Scheduled re-scoring with improved rules
