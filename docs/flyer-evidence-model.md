# Flyer Evidence Model (P27)

## Purpose

P27 turns uploaded flyers into durable, evidence-bearing system assets. Each flyer now persists enough metadata and linkage context to support:

- OCR normalization workflows
- moderation review and audit trails
- dedupe and media similarity seams
- later thumbnail/visual ranking pipelines

The model keeps upload, provenance, and evidence records separate so moderation and OCR can reference the same asset record without coupling flyer persistence to published event creation.

## Durable Records

### 1. Asset metadata record

Persisted through `media_assets` (flyer rows use `AssetType = FlyerImage`):

- asset id
- uploader/owner ids
- upload origin and source type
- original filename and content type
- file size and checksum/content hash
- width/height and canonical content type
- upload timestamp and lifecycle status
- storage provider/container/object key (+ optional URI/etag/version)
- optional submission id and metadata JSON

### 2. Provenance record

Persisted through `flyer_provenance`:

- provenance id + asset id
- source trust tier (`T1/T2/T3`)
- uploader user id (when known)
- upload origin classification (`ManualUploader`, `VenueOwner`, `SystemImported`, `PartnerProvided`, `Scraped`)
- source type (`DirectUpload`, `SubmissionAttachment`, `IngestionJobImport`, `PartnerFeed`, `ScrapedMedia`)
- submitter hash (PII-safe audit identifier)
- baseline authority score
- source URL and partner provider
- linked submission id and ingestion job id
- submitter note

### 3. Evidence record

Persisted through `flyer_evidence`:

- evidence id + asset/provenance ids
- original asset id (derivative lineage root)
- lifecycle status (`Pending`, `Linked`, `Rejected`)
- OCR readiness + OCR text + confidence score
- derivative asset refs (JSON list)
- processing history (JSON list)
- validation failures (JSON list)
- linked workflow ids (JSON list)
- linked submission/ingestion/moderation ids
- canonical event id (after review link)
- review notes

## Relationship Map

The persistence model supports these links without forcing event publication at intake:

- flyer asset -> submission (`SubmissionId`)
- flyer asset -> provenance (`AssetId`)
- flyer asset -> evidence (`AssetId`)
- evidence -> ingestion job (`IngestionJobId`)
- evidence -> moderation item (`ModerationItemId`)
- evidence -> canonical event (`CanonicalEventId`)

## API Query Seams

Added review-facing seams under `/api/media`:

- `GET /flyers/assets/{assetId}/detail`
  - full review projection: visual metadata + provenance summary + lifecycle state + evidence summary
- `GET /flyers/uploader/{uploaderUserId}/assets`
  - uploader-owned evidence-grade assets
- `GET /flyers/review/summaries?limit=...`
  - moderation queue summaries (pending evidence)
- `GET /flyers/submission/{submissionId}`
  - linked submission/media lookup

Intake endpoint now accepts richer provenance context:

- `uploadOrigin`
- `sourceType`
- `ingestionJobId`
- `moderationItemId`
- `partnerProvider`

## Why This Supports OCR/Moderation/Enrichment

### OCR normalization

`OcrReady`, derivative refs, and processing history give OCR workers deterministic inputs and append-only execution context.

### Moderation queue detail

Review surfaces can fetch one projection that includes lifecycle state, provenance authority, validation failures, and linked workflow ids.

### Dedupe/media similarity seam

Durable `ContentHash`, dimensions, and derivative lineage provide stable keys/features for similarity scoring.

### Thumbnail ranking seam

Derivative refs and visual metadata allow later rankers to score/select best thumbnails while preserving linkage to the original evidence root.

## Operational Notes

- Provenance is not flattened to a single source string.
- Evidence fields are retained after upload success.
- Moderation and OCR consume the same durable evidence records.
- Linking to canonical events is optional and performed later through review workflows.
