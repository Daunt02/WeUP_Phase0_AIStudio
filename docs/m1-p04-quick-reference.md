---
Title: Confidence Scoring Quick Reference
Version: 1.0
---

# Confidence Scoring - Quick Reference Guide

## 5-Minute Integration

### 1. Add Service Registration

**File**: `Program.cs` (or wherever DI is configured)

```csharp
// Add to services
services.AddScoped<IConfidenceScorer, ConfidenceScorer>();
```

### 2. Inject into Pipeline

```csharp
public sealed class FlyerIngestionPipeline(
    // ... existing dependencies
    IConfidenceScorer confidenceScorer
) : IFlyerIngestionPipeline
{
    // Ready to use
}
```

### 3. Score Candidates

```csharp
// After normalization produces EventCandidate
var candidate = await normalizationService.Normalize(ocrResult);

// Score with evidence tracking
var scoredCandidate = confidenceScorer.ScoreCandidate(
    candidate,
    ocrResult,
    sourceKind: "flyer_ocr"
);

// Result is EventCandidateV2 with:
// - FieldConfidence<T> for each field
// - OverallConfidence aggregated score
// - EvidenceBundle with raw sources
// - Full rationale trail
```

## Confidence Ranges (Quick Lookup)

| Score     | Meaning   | Action                         |
| --------- | --------- | ------------------------------ |
| 0.90-1.0  | Very High | ✓ Auto-approve candidates      |
| 0.75-0.89 | High      | ✓ Publish with optional review |
| 0.60-0.74 | Moderate  | ⚠ Flag for human review        |
| 0.40-0.59 | Low       | ✗ Require manual review        |
| 0.00-0.39 | Very Low  | ✗ Quarantine/reject            |

## Built-in Penalties (Quick Lookup)

| Situation                       | Penalty | How              |
| ------------------------------- | ------- | ---------------- |
| OCR < 0.70 confidence           | -15%    | × 0.85           |
| Missing required field          | -30%    | × 0.70 per field |
| Temporal conflict (end < start) | -30%    | × 0.70           |
| Venue ambiguity                 | -20%    | × 0.80           |
| Geocoding uncertain             | -20%    | × 0.80           |

## Built-in Boosts

| Situation              | Boost | How    |
| ---------------------- | ----- | ------ |
| OCR >= 0.95            | +5%   | × 1.05 |
| Multiple sources agree | +10%  | × 1.10 |
| High-trust source      | +5%   | × 1.05 |

## Field Weights in Overall Score

```
Overall = (Title × 0.25) + (DateTime × 0.30) + (Venue × 0.25) + (Address × 0.20)
```

**Why these weights?**

- DateTime most important (30%) - core event identifier
- Title & Venue (25% each) - event identity
- Address (20%) - location verification

## Accessing Confidence in Code

### Field-Level Confidence

```csharp
var scoredCandidate = confidenceScorer.ScoreCandidate(candidate, ocrResult);

// Check individual field confidence
if (scoredCandidate.Title?.Confidence > 0.85)
{
    Console.WriteLine($"High confidence title: {scoredCandidate.Title.Value}");
    Console.WriteLine($"Why: {scoredCandidate.Title.Rationale}");
}

// Check for missing fields
if (scoredCandidate.Venue?.Value.StartsWith("[Missing]") == true)
{
    Console.WriteLine("⚠ Venue is missing - requires manual entry");
}
```

### Overall Confidence

```csharp
double overallScore = scoredCandidate.OverallConfidence;  // [0.0, 1.0]
string explanation = scoredCandidate.OverallConfidenceRationale;

Console.WriteLine($"Confidence: {overallScore:P0}");
Console.WriteLine(explanation);  // Full explanation of calculation
```

### Evidence & Audit Trail

```csharp
var evidence = scoredCandidate.EvidenceBundle;

// Raw OCR text (before any processing)
var rawText = evidence.RawOcrText;

// Individual OCR blocks
foreach (var block in evidence.OcrBlocks ?? Array.Empty<OcrTextBlock>())
{
    Console.WriteLine($"Block {block.Index}: \"{block.Text}\" ({block.Confidence:P0})");
}

// Source hash (for deduplication)
var sourceHash = evidence.SourceHash;  // SHA256

// Scoring component breakdown (for audit)
foreach (var (field, component) in evidence.ScoringComponents ?? new Dictionary<string, ConfidenceComponent>())
{
    Console.WriteLine($"{field}: {component.ComponentType} = {component.BaseScore:P0}");
}

// Processing context
var ocrEngine = evidence.ProcessingContext?["ocr_engine"];
var engineVersion = evidence.ProcessingContext?["ocr_engine_version"];
```

## Common Scenarios

### Scenario 1: High Confidence Flyer

```
Title: "Annual Jazz Festival 2026"
StartUtc: May 15, 2026 7:00 PM
Venue: "The Fillmore"
Address: "1805 Geary Blvd, San Francisco, CA"

Result: ~80% confidence
Why: All fields present, moderate OCR quality, no conflicts
Action: Auto-publish with metadata
```

### Scenario 2: Low Confidence Flyer

```
Title: (missing)
StartUtc: "sometime in May" (ambiguous)
Venue: "Place" (generic, too short)
Address: (missing)

Result: ~30% confidence
Why: Missing required fields (-30%), generic venue (-20%), ambiguous date
Action: Quarantine for manual review
```

### Scenario 3: High Quality OCR

```
Title: "Comedy Show"
OCR reports: 0.98 confidence
Result: ~0.98 confidence for title field
Why: Direct OCR + excellent quality (+5% boost)
Action: Trust this field completely
```

### Scenario 4: Temporal Conflict

```
StartUtc: May 15 @ 9:00 PM
EndUtc: May 15 @ 7:00 PM  ← End BEFORE start!

Result: Overall -30% penalty applied
Rationale: "Temporal conflict detected (end before start) applied -30% penalty"
Action: Flag for manual review / automatic correction
```

## Debugging Confidence Scores

### Turn on detailed logging

```csharp
// If using ILogger<ConfidenceScorer>
logger.LogInformation("Scoring candidate: {candidate}", candidate);
logger.LogInformation("Overall confidence: {confidence:P} - {rationale}",
    scoredCandidate.OverallConfidence,
    scoredCandidate.OverallConfidenceRationale);

// Log field details
foreach (var field in new[] { scoredCandidate.Title, scoredCandidate.Venue })
{
    logger.LogInformation("Field {name}: {conf:P} - {rationale}",
        field?.Value ?? "[null]",
        field?.Confidence ?? 0.0,
        field?.Rationale ?? "[null]");
}
```

### Inspect evidence

```csharp
var evidence = scoredCandidate.EvidenceBundle;
Console.WriteLine($"Bundle: {evidence.BundleId}");
Console.WriteLine($"Source: {evidence.SourceKind}");
Console.WriteLine($"Raw OCR: {evidence.RawOcrText?.Substring(0, 100)}...");
Console.WriteLine($"Source Hash: {evidence.SourceHash}");

// Is this evidence identical to a previous run?
if (previousEvidence.SourceHash == currentEvidence.SourceHash)
{
    Console.WriteLine("→ Same source material (dedup candidate)");
}
```

## Testing Tips

### Unit test template

```csharp
[Test]
public void ScoreCandidate_WithCondition_ExpectsBehavior()
{
    // Arrange
    var candidate = new EventCandidate(
        Title: "Test Event",
        StartUtc: DateTimeOffset.UtcNow.AddDays(7),
        EndUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
        Venue: "Test Venue",
        Address: "123 Main St",
        Tags: new[] { "test" },
        RawFields: new Dictionary<string, string?>(),
        FieldScores: new Dictionary<string, FieldHeuristicScore>()
    );

    // Act
    var result = new ConfidenceScorer().ScoreCandidate(candidate);

    // Assert
    Assert.That(result.OverallConfidence, Is.GreaterThan(0.60));
    Assert.That(result.Title?.Confidence, Is.Not.Null);
    Assert.That(result.EvidenceBundle.BundleId, Is.Not.Null);
    Assert.That(result.OverallConfidenceRationale, Is.Not.Empty);
}
```

## Performance Notes

- **Scoring time**: <1ms per candidate (deterministic, no I/O)
- **Evidence storage**: Typically 50-500KB per flyer
- **Memory**: Minimal - just raw text + metadata
- **No external dependencies**: Fully self-contained

## Files Modified/Created

| File                                            | Purpose                                                                |
| ----------------------------------------------- | ---------------------------------------------------------------------- |
| `WeUP.Domain/Flyer/ConfidenceModel.cs`          | Record definitions (FieldConfidence, EvidenceBundle, EventCandidateV2) |
| `WeUP.Domain/Flyer/IConfidenceScorer.cs`        | Service interface                                                      |
| `WeUP.Infrastructure/Flyer/ConfidenceScorer.cs` | Scoring implementation                                                 |
| `docs/m1-p04-confidence-scoring-v1.md`          | Full documentation                                                     |
| `docs/m1-p04-quick-reference.md`                | This file                                                              |

## Common Errors & Solutions

### Error: "IConfidenceScorer not registered"

**Solution**: Add `services.AddScoped<IConfidenceScorer, ConfidenceScorer>();` to DI setup

### Error: "FieldConfidence<T> value is null"

**Solution**: Check if field extraction failed. Fields can be null - check `?.Value` before use

### Error: "Evidence is null"

**Solution**: EvidenceBundle is always created but may have null properties (RawOcrText, OcrBlocks). Safe to check conditionally

### Question: "Why is my confidence lower than expected?"

**Solution**: Check `OverallConfidenceRationale` string - it explains exactly what penalties were applied

## Next Steps

1. ✅ Add service to DI container
2. ✅ Inject into FlyerIngestionPipeline
3. ✅ Call `ScoreCandidate()` after normalization
4. ✅ Store EvidenceBundle for audit
5. ✅ Use confidence thresholds to route to review/publish
6. ⏳ Monitor confidence distribution in production
7. ⏳ Fine-tune penalty/boost factors based on human review

---

**Questions?** See full documentation in `docs/m1-p04-confidence-scoring-v1.md`
