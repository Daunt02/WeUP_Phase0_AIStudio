# Video Derived Assets (Phase 0.15)

## Frame Extraction Architecture

Phase 0.15 uses a deterministic first-pass pipeline executed when `/api/media/video-uploads/{uploadId}/complete` is called:

1. Metadata read (`IVideoMetadataReader`)
2. Frame extraction (`IVideoFrameExtractor`)
3. Poster selection (`IPosterSelectionService`)
4. Derived asset registration (`IVideoDerivedAssetRegistrar`)
5. Processing summary/query (`IVideoDerivedAssetQueryService`)

### Strategy decision

Chosen strategy: `fixed interval frames` with a `scene-change-aware extraction seam`, implemented as `first-pass deterministic extraction`.

Justification:

- Deterministic behavior is required for reproducibility, testing, and diagnosability in Phase 0.15.
- Fixed interval extraction guarantees a frame set even for low-quality or low-motion clips.
- A scene-change seam exists (`VideoFrameType.SceneChangeCandidate`) so Phase 0.5+ can add true scene-change logic without changing storage/query contracts.
- This strategy avoids arbitrary poster selection by persisting explicit scoring/selection reasons.

## Stages and Models

### Processing stages

- `MetadataExtraction`
- `FrameExtraction`
- `PosterGeneration`
- `Finalization`

Each stage writes to `VideoProcessingJob.StageHistory` with status and timestamps.

### Durable models

- Source video asset: `VideoFlyerAsset`
- Processing run: `VideoProcessingJob`
- Derived frame/poster metadata: `VideoDerivedFrameAsset`

`VideoDerivedFrameAsset` persists:

- source video asset id
- derived asset id
- processing job id
- uploader/owner context (`UploaderUserId`, `SubmissionId`, `VenueId`)
- moderation/review seam (`ModerationItemId`)
- extracted timestamp offset
- frame type
- width/height
- storage ref (`provider/container/object key/uri`)
- extraction stage/version
- poster selection reasoning

## Linkage and Traceability Rules

The pipeline enforces these rules:

- No extraction result is kept only in ephemeral memory.
- No derived file is written without registration.
- Poster selection is persisted with explicit reasoning.
- Low-quality or short videos still produce deterministic outputs.
- Every derived record links back to:
  - original video asset
  - processing job
  - uploader/owner context
  - moderation seam

## Retrieval Seams

- `GET /api/media/video-assets/{assetId}/poster`
- `GET /api/media/video-assets/{assetId}/frames`
- `GET /api/media/video-assets/{assetId}/processing-summary`
- `GET /api/media/video-jobs/{jobId}`

These endpoints provide poster metadata, frame metadata, and job/result diagnostics.

## How This Supports Later Work

### Thumbnail ranking

Persisted frame metadata and poster reasoning support offline ranking models that can re-score existing candidate frames.

### Moderation review

Moderation seams on derived records allow reviewers to inspect exact extracted frames used in poster decisions.

### Event enrichment

Time-sliced frames provide additional visual evidence for event extraction workflows (venue cues, date overlays, branding cues).

### Display projections

UI clients can render stable preview strips and poster images from durable derived asset refs without reprocessing source videos.
