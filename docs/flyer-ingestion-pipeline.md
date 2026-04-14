# Flyer OCR + Normalization Pipeline (P11)

## Purpose

P11 establishes a real flyer ingestion pipeline shape for Phase 0:

asset upload -> OCR extraction -> normalization -> canonical candidate output -> review/publish gate decision

The pipeline does not perform final dedupe merge or moderation UI actions. Those remain in P12/P13.

## API Surface

- `POST /api/ingestion/flyers`
  - Request: `FlyerUploadIngestionRequest`
  - Response: `IngestionAcceptedResponse`
- `GET /api/ingestion/flyers/{jobId}`
  - Response: `FlyerIngestionJobDetailResponse`
- `GET /api/ingestion/flyers/{jobId}/evidence`
  - Response: `FlyerIngestionEvidenceResponse`

The endpoint requires a durable flyer `assetId` from media intake (`/api/media/flyers`) and then executes the ingestion pipeline against that asset reference.

## Pipeline Stages

1. Source registration

- Validate the asset reference and create an ingestion job envelope with `SourceKind = FlyerUpload`.
- Ensure provenance exists (or create it) with source tier and baseline authority.
- Ensure evidence record exists (or reuse/update it for reprocess runs).

2. OCR extraction

- `IFlyerOcrService` accepts `FlyerAssetReference` and returns `FlyerOcrExtractionResult`.
- Output includes:
  - extraction id
  - raw OCR text snapshot
  - OCR block metadata (regions, confidence)
  - deterministic issues

3. OCR text post-processing

- `IFlyerTextPostProcessor` cleans OCR text before normalization.
- Empty cleaned output is treated as deterministic failure (`flyer_ocr_empty`).

4. Normalization

- `IFlyerNormalizationService` converts cleaned OCR text into `FlyerNormalizedEventCandidate`:
  - title/venue/address/date-time candidates
  - category/tag candidates
  - selected canonical candidate
  - missing fields and unresolved ambiguities
  - extraction warnings and review triggers

5. Candidate + confidence + review gate

- `IFlyerConfidenceEvaluator` computes `FlyerConfidenceVector` with dimensions:
  - extraction
  - temporal
  - venue match
  - geocode
  - dedupe placeholder
  - source trust
  - review confidence
- Review reasons are emitted deterministically from:
  - normalization triggers
  - confidence threshold checks
- Any ambiguity or unresolved blocker routes to `REQUIRES_REVIEW`.

6. Persistence and retrieval

- Generic ingestion job state persists in `ingestion_jobs`, `ingestion_candidates`, `ingestion_evidence`, and `ingestion_audit`.
- Flyer evidence trace persists in `flyer_evidence`:
  - OCR extraction id/version/blocks
  - normalization run id/version/snapshot
  - raw OCR text snapshot
  - review reasons
  - processing history

## Evidence Chain

For every flyer job, traceability includes:

- original flyer asset id
- upload timestamp (from media asset)
- OCR extraction id and OCR engine/version
- normalization run id and normalization version
- raw OCR text snapshot
- staged evidence references (`flyer-asset`, `flyer-provenance`, `flyer-ocr`, `flyer-normalization`)

This enables later moderation and appeals without recomputing extraction steps.

## Confidence Inputs

The confidence vector is assembled from:

- OCR confidence and normalized extraction confidence
- temporal confidence from date/time parsing
- geocode confidence from address reliability
- venue match confidence from venue candidate certainty
- dedupe placeholder confidence (`DedupePending` until P12)
- source trust from provenance baseline authority
- review confidence (set after human review workflows)

Weighted aggregate uses the same moderation-compatible shape as existing publish eligibility dimensions.

## Manual Review Triggers

Common deterministic trigger reasons:

- `UnreadableFlyer`
- `MissingDate`
- `MissingVenue`
- `MissingAddress`
- `AmbiguousVenue`
- `ConflictingTimeData`
- `PartialExtraction`
- `NormalizationIncomplete`
- `LowExtractionConfidence`
- `LowTemporalConfidence`
- `LowVenueMatchConfidence`
- `LowGeocodeConfidence`
- `DedupePending`
- `UnresolvedAmbiguity`

## Failure Modes Represented

- unreadable flyer (OCR failure)
- OCR succeeded but cleaned text empty
- missing date/venue/address during normalization
- ambiguous candidates requiring review
- conflicting or low-confidence temporal extraction
- partial extraction
- normalization incomplete with no canonical candidate

Failures and warnings are deterministic and inspectable via job detail and evidence endpoints.

## Provider Replacement Seams

Current Phase 0 implementations are intentionally stub/heuristic:

- `StubFlyerOcrService` (`IFlyerOcrService`)
- `HeuristicFlyerNormalizationService` (`IFlyerNormalizationService`)

Production integration can replace these with vendor implementations without changing:

- endpoint contracts
- orchestration flow
- evidence/provenance persistence model
- job lifecycle semantics
