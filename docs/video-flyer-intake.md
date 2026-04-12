# Video Flyer Intake — P28

## Overview

P28 builds the WeUP Phase 0.15 backend foundation for video flyer ingestion: durable asset records, upload lifecycle management, automatic processing job creation, explicit state machines, and uploader provenance. Video flyers are first-class domain objects distinct from image flyers — they carry video-specific metadata, require async post-processing, and must not be published directly from a raw upload.

---

## Architecture

### Two-phase upload lifecycle

```
Client                         API                       Storage / Queue
  │                              │                              │
  │─ POST /video-uploads ───────►│  validate MIME/size          │
  │  (multipart + file bytes)    │  compute SHA-256             │
  │                              │─ SaveAsync ─────────────────►│ local disk
  │◄── 201 { uploadId, assetId } │  save VideoFlyerAsset        │
  │    status: Uploaded          │  save VideoFlyerUpload        │
  │                              │                              │
  │─ POST /video-uploads/{id}    │  validate state = Uploaded   │
  │   /complete ────────────────►│  create VideoProcessingJob   │
  │  { success: true, ... }      │  asset → ProcessingPending   │
  │◄── 200 { jobId, status }     │  upload → ProcessingPending  │
  │                              │                              │
  │─ GET /video-jobs/{jobId} ───►│  return job (Queued)         │
  │◄── 200 { status: Queued }    │                              │
                                 │  — future: worker picks job  │
                                 │    advances stages           │
                                 │    writes result, poster     │
```

No video content is synchronously processed inside a request handler. The job record acts as a durable queue entry that any worker can claim.

### Upload strategy justification

Phase 0.15 uses **direct multipart upload**:

- Simple; no pre-signed URL infrastructure required yet
- Server validates and stores file before returning IDs
- Maximum file size enforced at 500 MB (config: `VideoIntake:MaxFileSizeBytes`)

Phase 0.5+ migration path to **signed URL flow**:

1. `POST /video-uploads` accepts metadata only → returns `uploadUrl` (S3/Azure Blob pre-signed URL)
2. Client PUTs bytes directly to `uploadUrl` (no server relay, no memory buffer)
3. Client calls `/complete` after PUT finishes; server verifies ETag/checksum from object storage
4. `IVideoStorageService` swaps from `LocalVideoStorageService` to a cloud impl — no service layer changes required

---

## Lifecycle states

### VideoFlyerAsset / VideoFlyerUpload

```
Initialized → Uploading → Uploaded → ProcessingPending → Processing
                                │                             │
                                └──────────── Rejected ◄──── ┤ (validation/review failure)
                                                             │
                                          ProcessingComplete → ReviewPending → Archived
```

| State                | Meaning                                                                 |
| -------------------- | ----------------------------------------------------------------------- |
| `Initialized`        | Upload record created; no bytes received yet                            |
| `Uploading`          | File transfer in progress (Phase 0: brief; Phase 0.5+: client-side PUT) |
| `Uploaded`           | File stored; awaiting `/complete` signal                                |
| `ProcessingPending`  | `/complete` called; a `VideoProcessingJob` is queued                    |
| `Processing`         | A worker has claimed the job                                            |
| `ProcessingComplete` | Pipeline finished; poster/frames available; ready for review            |
| `ReviewPending`      | In human moderation queue                                               |
| `Rejected`           | Rejected at upload validation or moderation review                      |
| `Archived`           | Soft-deleted or superseded                                              |

### VideoProcessingJob

| Status      | Meaning                                        |
| ----------- | ---------------------------------------------- |
| `Queued`    | Awaiting worker                                |
| `Running`   | Worker executing stages                        |
| `Succeeded` | All stages complete; result populated          |
| `Failed`    | One or more stages failed; asset stays pending |
| `Cancelled` | Job cancelled before run (e.g. asset archived) |

### VideoProcessingStage (ordered pipeline)

| Stage                | Purpose                                                          |
| -------------------- | ---------------------------------------------------------------- |
| `MetadataExtraction` | Extract duration, codec, container, dimensions, bitrate          |
| `PosterGeneration`   | Capture representative frame; encode as linked `MediaAsset`      |
| `FrameExtraction`    | Sample frames at intervals for preview grids                     |
| `Transcoding`        | Re-encode to web-safe container if source is non-compliant       |
| `Finalization`       | Update asset record, emit domain event, set `ProcessingComplete` |

Workers advance stages by calling `UpdateJobAsync` on `IVideoFlyerRepository`. The service layer does not dictate worker scheduling — that is an infrastructure concern left for Phase 0.5+ (Hangfire, Azure Service Bus, etc.).

---

## Domain models

| Model                         | File                                 | Purpose                                                |
| ----------------------------- | ------------------------------------ | ------------------------------------------------------ |
| `VideoFlyerAsset`             | `Domain/Media/VideoFlyerAsset.cs`    | Durable asset record with video-specific fields        |
| `VideoFlyerUpload`            | `Domain/Media/VideoFlyerAsset.cs`    | Upload session tracking record                         |
| `VideoProvenanceLinks`        | `Domain/Media/VideoFlyerAsset.cs`    | Links to SubmissionId, VenueId, ModerationItemId, etc. |
| `VideoStorageRef`             | `Domain/Media/VideoFlyerAsset.cs`    | Storage provider/container/key for the video file      |
| `VideoProcessingJob`          | `Domain/Media/VideoProcessingJob.cs` | Processing job with stage history and result           |
| `VideoProcessingStageRecord`  | `Domain/Media/VideoProcessingJob.cs` | Per-stage execution record                             |
| `VideoProcessingResult`       | `Domain/Media/VideoProcessingJob.cs` | Extracted metadata and artifact refs                   |
| `InitiateVideoUploadCommand`  | `Domain/Media/VideoFlyerAsset.cs`    | Input to upload initiation                             |
| `CompleteVideoUploadCommand`  | `Domain/Media/VideoFlyerAsset.cs`    | Input to completion signal                             |
| `VideoUploadCompletionResult` | `Domain/Media/VideoFlyerAsset.cs`    | Result bundle (upload + asset + optional job)          |

---

## API endpoints

### `POST /api/media/video-uploads`

Accepts `multipart/form-data`. Required: `file` (binary). Optional form fields: `submissionId`, `venueId`, `moderationItemId`, `ingestionJobId`.

Authentication: Bearer token required (or `uploaderUserId` form field for internal callers).

**Request:**

```
POST /api/media/video-uploads
Content-Type: multipart/form-data

file=<binary>
submissionId=sub-abc (optional)
venueId=venue-xyz (optional)
```

**Response 201:**

```json
{
  "uploadId": "a3f1e2...",
  "assetId": "b9c4d5...",
  "status": "Uploaded",
  "uploadUrl": null,
  "initializedAt": "2026-04-12T10:00:00Z"
}
```

**Validation errors → 422:**

- Unsupported content type (only `video/*` allowed)
- File exceeds 500 MB
- Unknown file extension
- Missing file

---

### `POST /api/media/video-uploads/{uploadId}/complete`

Signals that the upload is ready for processing. Creates a `VideoProcessingJob` in `Queued` status.

**Request body:**

```json
{
  "success": true,
  "detectedCodec": "h264",
  "clientDurationSeconds": 47,
  "clientWidthPx": 1920,
  "clientHeightPx": 1080
}
```

**Response 200 (success path):**

```json
{
  "uploadId": "a3f1e2...",
  "assetId": "b9c4d5...",
  "jobId": "f7a2b3...",
  "status": "ProcessingPending",
  "completedAt": "2026-04-12T10:01:00Z"
}
```

**Response 200 (failure path, `success: false`):**

```json
{
  "uploadId": "a3f1e2...",
  "assetId": "b9c4d5...",
  "jobId": null,
  "status": "Rejected",
  "completedAt": "2026-04-12T10:01:00Z"
}
```

**Errors:**

- 404 — `uploadId` not found
- 422 — upload is not in `Uploaded` state (e.g. already completed)

---

### `GET /api/media/video-assets/{assetId}`

Returns full asset projection including provenance links, storage ref, and video metadata (once populated by processing).

**Response 200:**

```json
{
  "assetId": "b9c4d5...",
  "status": "ProcessingPending",
  "contentType": "video/mp4",
  "fileSizeBytes": 4194304,
  "originalFilename": "event-promo.mp4",
  "uploadedAt": "2026-04-12T10:00:00Z",
  "uploaderUserId": "user-camille",
  "provenance": {
    "submissionId": "sub-abc",
    "venueId": "venue-xyz",
    "moderationItemId": null,
    "ingestionJobId": null
  },
  "storage": {
    "provider": "LocalFileSystem",
    "container": "/app/uploads/video/202604",
    "objectKey": "video/202604/b9c4d5....mp4"
  },
  "durationSeconds": 47,
  "widthPx": 1920,
  "heightPx": 1080,
  "detectedCodec": "h264",
  "bitrateKbps": null,
  "processingJobId": "f7a2b3...",
  "posterAssetId": null,
  "createdAt": "2026-04-12T10:00:00Z",
  "updatedAt": "2026-04-12T10:01:00Z"
}
```

---

### `GET /api/media/video-jobs/{jobId}`

Returns the processing job with stage history.

**Response 200:**

```json
{
  "jobId": "f7a2b3...",
  "assetId": "b9c4d5...",
  "status": "Queued",
  "queuedAt": "2026-04-12T10:01:00Z",
  "startedAt": null,
  "completedAt": null,
  "failureReason": null,
  "currentStage": null,
  "stageHistory": [],
  "result": null
}
```

After a worker runs:

```json
{
  "status": "Succeeded",
  "currentStage": "Finalization",
  "stageHistory": [
    { "stage": "MetadataExtraction", "status": "Succeeded", ... },
    { "stage": "PosterGeneration",   "status": "Succeeded", ... },
    { "stage": "Finalization",        "status": "Succeeded", ... }
  ],
  "result": {
    "durationSeconds": 47,
    "widthPx": 1920,
    "heightPx": 1080,
    "detectedCodec": "h264",
    "bitrateKbps": 4200,
    "posterAssetId": "poster-abc123"
  }
}
```

---

## Uploader provenance

`VideoProvenanceLinks` records four nullable upstream workflow references on every asset:

| Field              | Source                     | Used by                          |
| ------------------ | -------------------------- | -------------------------------- |
| `SubmissionId`     | Set by frontend on submit  | Submission ↔ asset linkage       |
| `VenueId`          | Set when venue uploads     | Venue asset library              |
| `ModerationItemId` | Set by moderation workflow | Moderation queue ↔ asset linkage |
| `IngestionJobId`   | Set by ingestion pipeline  | Ingestion traceability           |

`UploaderUserId` is stored directly on the asset. No raw PII beyond user ID is stored; provenance hashing (as used in image flyers via `ProvenanceRecord`) can be layered on in Phase 0.5 if Tier classification is needed for video.

---

## Persistence summary

Three new PostgreSQL tables (EF Core convention-mapped):

| Table                   | Entity                     | Key indexes                                              |
| ----------------------- | -------------------------- | -------------------------------------------------------- |
| `video_flyer_assets`    | `VideoFlyerAssetEntity`    | `assetId` (unique), status, uploaderUserId, submissionId |
| `video_flyer_uploads`   | `VideoFlyerUploadEntity`   | `uploadId` (unique), assetId, status                     |
| `video_processing_jobs` | `VideoProcessingJobEntity` | `jobId` (unique), assetId, status                        |

`StageHistoryJson` and `ResultJson` are stored as `jsonb` columns for schema flexibility during pipeline iteration.

EF migration generation (when Postgres is configured):

```bash
cd backend
dotnet ef migrations add P28_VideoFlyerIntake \
  --project WeUP.Infrastructure \
  --startup-project WeUP.Api
```

---

## Service layer summary

| Class                          | Responsibility                                                |
| ------------------------------ | ------------------------------------------------------------- |
| `VideoFlyerUploadService`      | Validates, stores, and tracks upload + asset lifecycle        |
| `VideoIntakeValidation`        | MIME allowlist, size ceiling, extension plausibility check    |
| `LocalVideoStorageService`     | Writes video files to `uploads/video/{yyyyMM}/{assetId}{ext}` |
| `InMemoryVideoFlyerRepository` | In-memory store for dev/testing (replaces EF in stub mode)    |
| `IVideoFlyerRepository`        | Repository interface for assets, uploads, and jobs            |
| `IVideoFlyerUploadService`     | Upload + lifecycle service interface                          |
| `IVideoStorageService`         | Storage abstraction seam (swap to cloud impl in Phase 0.5+)   |

---

## DI registration (Program.cs)

```csharp
// Phase 0.15 — in-memory storage, always registered
builder.Services.Configure<VideoIntakeOptions>(builder.Configuration.GetSection(VideoIntakeOptions.SectionName));
builder.Services.AddSingleton<VideoIntakeValidation>();
builder.Services.AddSingleton<IVideoStorageService, LocalVideoStorageService>();
builder.Services.AddSingleton<InMemoryVideoFlyerRepository>();
builder.Services.AddSingleton<IVideoFlyerRepository>(sp =>
    sp.GetRequiredService<InMemoryVideoFlyerRepository>());
builder.Services.AddSingleton<IVideoFlyerUploadService, VideoFlyerUploadService>();

// Phase 0.5+ — swap to EF-backed repository when DB is available:
// builder.Services.AddScoped<IVideoFlyerRepository, EfVideoFlyerRepository>();
```

---

## Configuration

`appsettings.json` (or environment overrides):

```json
{
  "VideoIntake": {
    "MaxFileSizeBytes": 524288000,
    "AllowedContentTypes": [
      "video/mp4",
      "video/webm",
      "video/quicktime",
      "video/x-msvideo",
      "video/mpeg",
      "video/ogg"
    ],
    "LocalStorageRoot": "uploads/video"
  }
}
```

---

## Frontend integration

The frontend never publishes a video flyer directly from upload. The integration contract is:

1. **Initiate upload**:

   ```
   POST /api/media/video-uploads   (multipart, with Bearer token)
   → { uploadId, assetId, status: "Uploaded" }
   ```

2. **Signal completion** (immediately after file is accepted):

   ```
   POST /api/media/video-uploads/{uploadId}/complete
   Body: { success: true, clientDurationSeconds?: number, ... }
   → { jobId, status: "ProcessingPending" }
   ```

3. **Poll job status** (optional, for progress UI):

   ```
   GET /api/media/video-jobs/{jobId}
   → { status: "Queued" | "Running" | "Succeeded" | "Failed" }
   ```

4. **Do not** show the video to end-users until the asset reaches `ProcessingComplete` and clears moderation review (`ReviewPending` → approved). The frontend must treat `ProcessingPending` / `Processing` / `ProcessingComplete` as non-publishable states.

5. **Upload URL seam**: the `uploadUrl` field in the initiation response is `null` in Phase 0.15. In Phase 0.5+, it will contain a pre-signed URL and the frontend must PUT the file bytes directly to that URL instead of embedding them in the POST body.

---

## Design rules enforced

| Rule                          | Implementation                                                                        |
| ----------------------------- | ------------------------------------------------------------------------------------- |
| Video ≠ large image upload    | Separate domain models, separate endpoint group, separate validation                  |
| No sync video processing      | `VideoFlyerUploadService` never processes video; only creates a job record            |
| Frontend cannot invent states | All lifecycle transitions are enforced in the service layer                           |
| Raw asset ≠ publishable event | No endpoint publishes video; `ReviewPending` must be cleared by moderation            |
| Clean storage seam            | `IVideoStorageService` replaces without touching service or endpoint code             |
| Clean job seam                | `IVideoFlyerRepository.UpdateJobAsync` is the only path for workers to advance stages |
| Uploader provenance preserved | `UploaderUserId` + `VideoProvenanceLinks` on every asset record                       |

---

## Phase 0.5+ expansion seams

- **Worker integration**: implement a background service / message consumer that reads `Queued` jobs from `IVideoFlyerRepository`, calls FFmpeg/media library for each stage, and calls `UpdateJobAsync` after each stage.
- **Signed URL upload**: add `IVideoStorageService.GenerateUploadUrlAsync(assetId, contentType)` for cloud object storage. The service layer already has the seam (`UploadUrl` on `VideoFlyerUpload`).
- **EF Core repository**: `EfVideoFlyerRepository` implementing `IVideoFlyerRepository` — entities and schema are already defined.
- **Frame/poster extraction**: `FrameExtraction` and `PosterGeneration` stages create new `MediaAsset` records and write `PosterAssetId` / `FrameAssetIds` back to `VideoProcessingResult`.
- **Moderation linkage**: after `ProcessingComplete`, set `ModerationItemId` on the asset by calling the moderation intake service.
