# Media Intake (P25)

## Overview

Phase 0.15 media intake turns flyer and venue uploads into durable backend objects with explicit ownership, lifecycle state, and storage references.

Key outcomes:

- A media upload now creates both a `MediaUpload` job record and a `MediaAsset` record.
- Asset identifiers are generated only by the backend.
- Ownership, uploader identity, and storage references are persisted.
- Upload lifecycle is explicit and review-aware.
- Flyer and venue media share one foundation.

## Why multipart upload in Phase 0.15

Phase 0.15 uses direct multipart uploads (`POST /api/media/uploads`) instead of signed URL flows.

Rationale:

- We need backend-owned durable records before file bytes are considered accepted.
- Multipart keeps validation, checksuming, ownership checks, and metadata capture in one transaction boundary.
- Operational complexity is lower for local/dev and aligns with in-memory + local filesystem fallback.
- The storage layer is abstracted (`IMediaStorageService`) so signed URL + cloud object storage can be added later without API contract breakage.

## Domain Model

### MediaAsset

Canonical durable media object.

Fields:

- `assetId`
- `assetType`: `FlyerImage | VenueImage | PromotionalPoster | EventMedia`
- `status`: lifecycle status
- `contentType`
- `fileSizeBytes`
- `checksumSha256`
- `originalFilename`
- `uploadedAt`
- `uploaderUserId`
- `owner` (`MediaOwnerRef`)
- `storage` (`MediaStorageRef`)
- `submissionId` (workflow seam)
- `metadataJson` (media-type-aware metadata seam)

### MediaUpload

Upload/job lifecycle object associated with an asset.

Fields:

- `uploadId`
- `assetId`
- `status`
- `initializedAt`
- `completedAt`
- `requestedByUserId`
- `failureReason`

### MediaOwnerRef

Ownership model supporting user, venue, and system-originated ingestion.

Fields:

- `ownerType`: `User | Venue | SystemWorkflow`
- `ownerId`
- `venueId`
- `ingestionWorkflowSource`

### MediaStorageRef

Provider-agnostic storage pointer.

Fields:

- `provider`: `LocalFileSystem | CloudObjectStorage`
- `container`
- `objectKey`
- `uri`
- `eTag`
- `versionId`

## Lifecycle

Statuses:

- `Initialized`
- `Uploading`
- `Uploaded`
- `ProcessingPending`
- `ProcessingComplete`
- `ReviewPending`
- `Rejected`
- `Archived`

Current flow:

1. `POST /api/media/uploads`: creates `Uploading`, persists bytes, transitions to `Uploaded`.
2. `POST /api/media/uploads/{uploadId}/complete`: transitions to processing + review status.
3. `GET` endpoints expose lifecycle for moderation/review integration.

## API Contracts

### `POST /api/media/uploads`

Multipart form:

- `file` (required)
- `assetType` (required)
- `ownerType` (optional; default `User`)
- `ownerId`, `venueId`, `uploaderUserId`, `submissionId`, `ingestionWorkflowSource`, `metadataJson` (optional)

Returns:

- `201 Created`
- `{ uploadId, assetId, status }`

### `POST /api/media/uploads/{uploadId}/complete`

JSON body:

- `processingSucceeded` (default `true`)
- `queueForReview` (default `true`)
- `failureReason`
- `metadataJson`

Returns:

- `200 OK` with upload lifecycle record.

### `GET /api/media/uploads/{uploadId}`

Returns upload state.

### `GET /api/media/assets/{assetId}`

Returns durable asset state, owner, and storage refs.

## Persistence

EF entities:

- `media_assets`
- `media_uploads`

Repository abstraction:

- `IMediaIntakeRepository`
- In-memory implementation for local/testing
- EF Core + PostgreSQL implementation when DB is configured

## Storage

Storage abstraction:

- `IMediaStorageService`
- `LocalMediaStorageService` writes to `uploads/media/<yyyyMM>/...`

Future seam:

- add cloud object storage implementation with same interface and no controller change.

## Auth and Ownership

Uploader context resolution:

- Prefer authenticated user via bearer token.
- Allow explicit `uploaderUserId` fallback for local dev.
- `User`/`Venue` owners require uploader context.
- `SystemWorkflow` supports ingestion-owned uploads.

## Submission and Moderation seams

- `submissionId` is accepted and persisted on asset.
- Completion endpoint can queue asset status to `ReviewPending`.
- This keeps media persistence separate from event publication while still linking workflows.

## Frontend seam

`components/AddEventModal.tsx` now:

- opens a real file picker for “Upload Flyer”
- calls `services/mediaService.ts` upload APIs
- stores backend-issued `assetId`
- passes `flyerAssetIds` into publish payload when available

No simulated success path remains for flyer uploads.

## Flyer Validation and OCR Readiness (P26)

Flyer uploads are validated server-side before they are marked ready for OCR:

1. The backend receives bytes and creates a backend-generated storage key.
2. Lifecycle moves `Initialized -> Uploaded` once intake receives the asset.
3. `IFlyerAssetValidator` runs strict checks:
   - MIME allowlist
   - extension vs detected type mismatch
   - max file size
   - minimum dimensions
   - corruption/readability via image decode
   - animated format rejection when disabled
   - SHA-256 checksum generation
   - duplicate detection seam by hash + uploader within recency window
4. On failure, lifecycle moves `Uploaded -> ValidationFailed` and stores a deterministic reason.
5. On success, lifecycle moves `Uploaded -> ProcessingPending`.
6. `ProcessingPending` is the handoff state for OCR and downstream moderation workflows.

Operationally, OCR workers should only pick assets in `ProcessingPending` and should never process `ValidationFailed` assets.
