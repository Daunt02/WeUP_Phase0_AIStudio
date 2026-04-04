# WeUP Extended Prompt Bundle Pack — Phase 0.5 Onwards

**Follows:** WeUP Phase 0 — 030 Prompt Bundle Pack (P01–P56)
**Covers:** Phase 0.5 (Production Readiness) through Phase 0.30+ (Intelligence Layer)
**Format:** Mirrors the 030 Prompt Bundle Pack naming convention exactly
**Naming:** Pack_09–Pack_13 | Bundle 9a–Bundle 13 | P57–P100+

---

## Hard Premise

Phase 0 produced a fully scaffolded, stub-based system with 25 prompts and 91 tests. Phase 0.5 is not optional polish — it is the **production bridge** that must land before any user-facing expansion. Every stub must become real before Phase 0.15 media features or Phase 0.20 trust features are meaningful.

> "Can a real user discover, save, and review real city events on a stable, durable map?" — this must be true before anything else ships.

---

## Bundle Pack Program (Extended)

| Pack | Name | Mission |
|------|------|---------|
| Pack 9 | WeUP Phase 0.5 Production Readiness Pack | From stubs to real infrastructure |
| Pack 10 | WeUP Phase 0.15 Media Signal Pack | From static flyers to real media ingestion |
| Pack 11 | WeUP Phase 0.20 Trust Graph Pack | From public discovery to gated cultural access |
| Pack 12 | WeUP Phase 0.25 Operator Revenue Pack | From user app to venue/sponsor operating system |
| Pack 13 | WeUP Phase 0.30+ Intelligence Pack | From city map to adaptive cultural intelligence network |

---

---

# Pack 9 — Phase 0.5 Production Readiness Pack

**From stubs to real infrastructure**

## Bundle Titles
- Bundle 9a — EF Core Migration and Repository Production Swap
- Bundle 9b — JWT Authentication and Session Management
- Bundle 9c — Cloud Media Storage
- Bundle 9d — Analytics Sink and Observability Export
- Bundle 9e — Promoter Profile Domain and API
- Bundle 9f — Promoter Feed and Media Upload Flows
- Bundle 9g — Security Hardening
- Bundle 9h — Integration Tests and Release Gates

---

### Bundle 9a — EF Core Migration and Repository Production Swap

#### P57 — Run EF Core Initial Migration and Activate Production Repositories

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace all in-memory stub repositories with EF Core + PostgreSQL implementations that were pre-built during Phase 0.

Context:
Phase 0 shipped with fully-written EfEventRepository, EfSaveRepository, and WeUpDbContext. They are ready but gated behind commented-out registrations in Program.cs. The stubs (StubEventRepository, StubSaveRepository) return hardcoded data and survive no server restarts.

What to do:
1. Add Microsoft.EntityFrameworkCore.Design to WeUP.Infrastructure.csproj and WeUP.Api.csproj
2. Run the EF Core initial schema migration:
   - dotnet ef migrations add InitialSchema --project backend/WeUP.Infrastructure --startup-project backend/WeUP.Api
   - dotnet ef database update --project backend/WeUP.Infrastructure --startup-project backend/WeUP.Api
3. In Program.cs, uncomment and activate EF Core service registrations (replace singleton stubs with scoped EF implementations)
4. Set WeUpDb connection string in appsettings.Development.json
5. Switch all relevant Singleton registrations to Scoped (required for DbContext lifetime)
6. Verify the backend builds and all 48 backend tests still pass
7. Verify GET /api/events/map returns data from the database (not hardcoded)

Acceptance criteria:
- EF migration applies cleanly with 0 errors
- Backend builds with 0 errors
- All 48 backend tests pass
- GET /api/events/map responds with events from PostgreSQL
- Server restart does not clear event or save data
- Singleton → Scoped lifetime changes cause no test regressions

Output format:
1. List all changes to .csproj files
2. Show the migration command output
3. Show the Program.cs registration changes
4. Confirm tests still pass
5. Show a sample event from the DB via curl or test


#### P58 — Implement Remaining EF Core Repository Swaps

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace all remaining in-memory repositories that were not covered by EfEventRepository and EfSaveRepository.

Context:
Seven stubs still exist after P57:
- InMemoryUserRepository → IUserProfileRepository
- InMemoryPreferencesRepository → IUserPreferencesRepository
- InMemoryItineraryRepository → IItineraryRepository
- InMemoryModerationQueue → IModerationQueueRepository
- InMemoryAuditTrail → IAuditTrailService
- InMemorySubmissionRepository → IEventSubmissionService
- InMemoryIngestionJobRepository → IIngestionJobRepository

What to do:
1. Create new EF Core entity types and DbSet registrations for missing tables:
   - UserPreferences, Itineraries, ModerationQueueItems, AuditTrailEntries, IngestionJobs
2. Add these entities to WeUpDbContext
3. Create new migration for the additional tables
4. Implement EF Core repository classes for each interface
5. Register each new repository in Program.cs, replacing in-memory stubs
6. Ensure all services still compile and no tests regress

Acceptance criteria:
- All 7 remaining stubs removed from production registration
- New tables present in database schema
- All endpoints that previously worked with in-memory data continue to work
- 48 backend tests still passing
- ModerationQueue data survives a server restart

---

### Bundle 9b — JWT Authentication and Session Management

#### P59 — Replace BearerTokenService with JWT Authentication

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace the in-memory BearerTokenService with real JWT authentication backed by a configurable secret.

Context:
BearerTokenService stores issued tokens in a ConcurrentDictionary that is cleared on restart. auth/login returns a fake token. UseAuthentication() and UseAuthorization() are commented out in Program.cs. JwtBearer package is not yet added.

What to do:
1. Add Microsoft.AspNetCore.Authentication.JwtBearer NuGet package to WeUP.Api.csproj
2. Create a JwtTokenService implementing ITokenService:
   - Issue signed JWTs with expiry (configurable, default 60 minutes)
   - Include userId, email, and role claims
   - Sign using HS256 with WeUP:Auth:JwtSecret from configuration
3. Uncomment and configure app.UseAuthentication() and app.UseAuthorization() in Program.cs
4. Configure AddAuthentication().AddJwtBearer() with token validation parameters
5. Add [Authorize] attributes to all endpoints that require authentication (saves, itinerary, submissions, moderation)
6. Leave public endpoints (map feed, event detail, temporal, health) unauthenticated
7. Update AuthEndpoints to return a signed JWT on successful login
8. Add WeUP:Auth:JwtSecret to appsettings.json (empty) and appsettings.Development.json (placeholder)

Acceptance criteria:
- POST /auth/login returns a valid signed JWT
- Protected endpoints return 401 without a valid token
- Protected endpoints return 200 with a valid Bearer token
- Token validation rejects expired tokens
- App builds with 0 errors
- Backend tests updated to pass auth headers where required

Output format:
1. List NuGet package additions
2. Show JwtTokenService implementation
3. Show Program.cs authentication configuration changes
4. List endpoints that are now protected vs public
5. Show a sample login and authenticated request via curl


#### P60 — Implement Refresh Tokens and Session Persistence

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Add refresh token support so user sessions survive access token expiry without requiring re-login.

Context:
P59 introduced short-lived JWT access tokens. Users on mobile or in long sessions will be logged out unnecessarily without refresh tokens. Phase 0.5 UX must feel stable.

What to do:
1. Add a RefreshTokens table to the database (UserId, Token, ExpiresAt, IsRevoked, CreatedAt)
2. Extend POST /auth/login to return both an access token (JWT) and a refresh token (opaque, 30-day expiry)
3. Add POST /auth/refresh endpoint that accepts a valid refresh token and returns a new access token
4. Add POST /auth/logout endpoint that revokes the provided refresh token
5. Implement token revocation check on each refresh attempt
6. Add EF Core migration for the RefreshTokens table

Acceptance criteria:
- POST /auth/refresh returns a new access token when given a valid refresh token
- POST /auth/logout marks the refresh token as revoked
- A revoked refresh token cannot produce a new access token
- Refresh tokens expire after 30 days
- EF migration applies cleanly

---

### Bundle 9c — Cloud Media Storage

#### P61 — Implement Cloudinary Media Storage Service

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace LocalFlyerUploadService with a Cloudinary-backed IFlyerUploadService that stores files in the cloud and returns CDN URLs.

Context:
LocalFlyerUploadService saves files to the local filesystem (wwwroot/uploads/). Files are lost on deployment. Phase 0.15 (video pipeline) will rely on Cloudinary's transcoding and thumbnail features, so adopting it now avoids a future migration.

What to do:
1. Add CloudinaryDotNet NuGet package to WeUP.Infrastructure.csproj
2. Create CloudinaryFlyerUploadService implementing IFlyerUploadService:
   - UploadAsync: upload file stream to Cloudinary, return CDN URL as the asset URL
   - Store Cloudinary public_id in FlyerAsset.S3Url field
   - On image upload: request auto-quality transformation
   - On image upload: generate a thumbnail (300x400, auto-crop)
   - DeleteAsync: call Cloudinary destroy API by public_id
   - GetAsync: return stored metadata (no Cloudinary API call needed)
   - ListAsync: return metadata from DB
3. Register CloudinaryFlyerUploadService in Program.cs (replace LocalFlyerUploadService)
4. Add Cloudinary configuration to appsettings.json: WeUP:Cloudinary:CloudName, ApiKey, ApiSecret
5. Update FlyerAsset to store both the CDN URL and the Cloudinary public_id

Acceptance criteria:
- POST /api/media/flyers uploads a file to Cloudinary
- Response contains a valid CDN URL (not a local path)
- File is retrievable via the CDN URL immediately after upload
- DELETE /api/media/flyers/{assetId} removes the file from Cloudinary
- Server restart does not lose uploaded file metadata

---

### Bundle 9d — Analytics Sink and Observability Export

#### P62 — Implement Segment Analytics Service

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace ConsoleAnalyticsService with a Segment analytics sink so all 8 analytics event types are tracked in a real analytics platform.

Context:
ConsoleAnalyticsService writes analytics events to Console.Log only. No events are stored, queryable, or visible in dashboards. The IsPrivacyCompliant() guard is already in place and must remain enforced before any external send.

What to do:
1. Create SegmentAnalyticsService implementing IAnalyticsService using Segment's HTTP Tracking API:
   - Serialize each AnalyticsEvent to a Segment Track call
   - Enforce IsPrivacyCompliant() before sending — drop the event if not compliant
   - Map all 8 AnalyticsEventType values to Segment event names
   - Send userId or anonymousId based on whether UserId is present
   - Fire-and-forget (don't block the request thread)
2. Add WeUP:Analytics:SegmentWriteKey to configuration
3. Register SegmentAnalyticsService in Program.cs (replace ConsoleAnalyticsService)
4. Keep ConsoleAnalyticsService registered in Development environment as fallback

Acceptance criteria:
- All 8 event types produce a Segment Track call when the service is active
- Events without a userId use an anonymousId
- PII guard prevents compliant=false events from reaching Segment
- Development environment can fall back to console logging
- 48 backend tests still pass (ConsoleAnalyticsService used in test environment)


#### P63 — Add OpenTelemetry OTLP Export

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Activate the OpenTelemetry export seam that was prepared in P22, routing traces and metrics to an external OTLP backend.

Context:
P22 added correlation IDs and structured logging. The builder.Services.AddOpenTelemetry() call is commented out in Program.cs. The infrastructure is in place — only the export configuration is missing.

What to do:
1. Add NuGet packages:
   - OpenTelemetry.Extensions.Hosting
   - OpenTelemetry.Instrumentation.AspNetCore
   - OpenTelemetry.Instrumentation.Http
   - OpenTelemetry.Exporter.OpenTelemetryProtocol
2. Uncomment and configure AddOpenTelemetry() in Program.cs:
   - WithTracing: AddAspNetCoreInstrumentation, AddHttpClientInstrumentation, AddOtlpExporter
   - WithMetrics: AddAspNetCoreInstrumentation, AddOtlpExporter
   - ServiceName: "weup-api"
   - ServiceVersion: from assembly version
3. Add WeUP:Observability:OtlpEndpoint to configuration
4. In Development, fall back to console exporter if no OTLP endpoint is configured
5. Ensure correlation IDs from P22 appear in trace spans

Acceptance criteria:
- Traces appear in the configured OTLP backend (Honeycomb, Datadog, or Jaeger)
- Correlation ID from X-Correlation-ID header appears as a trace attribute
- Development environment works without OTLP endpoint configured (console exporter)
- No performance regression on endpoint response times

---

### Bundle 9e — Promoter Profile Domain and API

#### P64 — Define Promoter Profile Domain Model and EF Entities

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Create the domain model and EF Core entities for the Promoter Profile system per the Phase 0.1 feature specification.

Context:
Phase 0.1 defines Promoter Profiles as the trust and identity layer for event creators. Users follow promoters, not just events. Three entities are needed: PromoterProfile, MediaAsset, and FeedPost with a FeedPostMedia join table.

What to do:
1. Create domain models in WeUP.Domain/Promoters/:
   - PromoterProfile: Id, DisplayName, Slug (unique), Bio, ProfileImageUrl, BannerImageUrl, City, InstagramUrl, TikTokUrl, WebsiteUrl, VerificationStatus (Pending/Verified/Rejected), IsActive, CreatedAt, UpdatedAt
   - MediaAsset: Id, PromoterProfileId, EventId?, VenueId?, MediaType (Photo/Video), Url, ThumbnailUrl, Caption, AltText, SortOrder, VisibilityStatus (Draft/Published/Hidden/Flagged), DurationSeconds?, Width, Height, FileSizeBytes, CreatedAt, UpdatedAt
   - FeedPost: Id, PromoterProfileId, Title, Body, LinkedEventId?, PostType (General/EventPromo/Recap/VenueHighlight), PublishedAt, Status (Draft/Published/Archived/Flagged), CreatedAt, UpdatedAt
   - FeedPostMedia: FeedPostId, MediaAssetId, SortOrder
2. Create EF Core entity configurations and add DbSets to WeUpDbContext
3. Create EF migration for new tables
4. Define interfaces: IPromoterProfileRepository, IMediaAssetRepository, IFeedPostRepository
5. Create in-memory stub implementations for each interface (Phase 0 pattern)
6. Register stubs in Program.cs

Acceptance criteria:
- Domain models compile with 0 errors
- EF migration adds PromoterProfiles, MediaAssets, FeedPosts, FeedPostMedia tables
- Slug field has unique index
- All 48 existing tests still pass
- Stub implementations return empty collections


#### P65 — Implement Promoter Profile API Endpoints

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build all API endpoints for the Promoter Profile system: profile CRUD, feed management, and admin operations.

Context:
P64 defined the domain model. P65 wires it to the HTTP layer. The Phase 0.1 Feature Addition document specifies the exact API surface required.

What to do:
1. Create PromoterEndpoints.cs in WeUP.Api/Endpoints/:
   - POST /api/promoters — create profile (authenticated)
   - GET /api/promoters/{slug} — public profile with feed and events
   - PUT /api/promoters/{id} — update profile (authenticated, own profile)
   - GET /api/promoters/{id}/events — promoter's associated events
   - GET /api/promoters/{id}/feed — paginated feed posts
2. Create PromoterFeedEndpoints.cs:
   - POST /api/promoter-feed — create feed post (authenticated)
   - PUT /api/promoter-feed/{id} — update post
   - DELETE /api/promoter-feed/{id} — delete post
   - GET /api/promoter-feed/{id} — get single post with media
3. Create AdminPromoterEndpoints.cs:
   - POST /api/admin/promoters/{id}/verify — set VerificationStatus = Verified
   - POST /api/admin/promoters/{id}/suspend — set IsActive = false
4. Map all new endpoint groups in Program.cs
5. Add request/response DTOs to WeUP.Contracts
6. Add xUnit tests for new endpoint contracts

Acceptance criteria:
- POST /api/promoters creates a profile with unique slug
- GET /api/promoters/{slug} returns profile, feed, and events
- Authenticated endpoints return 401 without valid JWT
- Admin endpoints return 403 for non-admin users
- New tests pass alongside existing 48

---

### Bundle 9f — Promoter Feed and Media Upload Flows

#### P66 — Implement Promoter Media Upload with Cloudinary

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Wire the promoter media upload flow — images and short-form videos — through Cloudinary storage and attach them to feed posts.

Context:
P61 added Cloudinary for flyer uploads. P64–P65 added the Promoter domain model and API. P66 connects them: promoter media (profile photos, feed images, short videos) must upload to Cloudinary and persist metadata to the MediaAssets table.

What to do:
1. Create POST /api/media/upload endpoint for promoter media:
   - Accept multipart file upload
   - Validate file type: images (jpg, png, webp), videos (mp4, mov, webm)
   - Validate video duration ≤ 30 seconds (use Cloudinary metadata response)
   - Upload to Cloudinary, request thumbnail generation for images
   - For videos: request poster frame at 1 second
   - Store MediaAsset record with Cloudinary URL and public_id
   - Return assetId + URLs
2. Create POST /api/media/attach-to-post endpoint:
   - Accept FeedPostId + MediaAssetId + SortOrder
   - Create FeedPostMedia join record
3. Create DELETE /api/media/{id} endpoint:
   - Delete from Cloudinary by public_id
   - Remove MediaAsset record from DB
4. Create PUT /api/media/{id}/sort-order endpoint:
   - Reorder media within a post
5. Enforce upload limits: max 10 images per batch, max 30-second video
6. Add to moderation queue: all uploaded media begins in Draft status

Acceptance criteria:
- Image upload returns a Cloudinary CDN URL and a thumbnail URL
- Video upload returns a poster frame URL alongside the video URL
- Videos over 30 seconds are rejected with 422
- Unsupported MIME types rejected with 422
- Media attaches to feed posts correctly with sort order
- All new uploads begin in Draft/moderation status

---

### Bundle 9g — Security Hardening

#### P67 — Implement Rate Limiting, CSRF Protection, and Input Validation

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Add production security controls to all endpoints before real users interact with the system.

Context:
Phase 0 has minimal security controls. For production, auth endpoints must be rate-limited, uploads must have size and type guards, and state-mutating endpoints must have CSRF protection.

What to do:
1. Add rate limiting using .NET 8 built-in rate limiting middleware:
   - Auth endpoints (POST /auth/login, POST /auth/register): 10 requests per minute per IP
   - Upload endpoints (POST /api/media/upload, POST /api/media/flyers): 20 requests per minute per user
   - All other endpoints: 300 requests per minute per IP
2. Add CSRF protection using Microsoft.AspNetCore.Antiforgery:
   - Generate antiforgery token for state-mutating requests from browser clients
   - Validate on POST/PUT/DELETE endpoints that are not API-key or JWT-only
3. Add FluentValidation for all request DTOs in WeUP.Contracts:
   - PromoterProfile: Slug length (3–50 chars, lowercase alphanumeric + hyphens), DisplayName length
   - MediaAsset: MIME type whitelist, file size limits
   - AnalyticsEvent: EventType must be a valid enum value
4. Configure Kestrel upload size limits:
   - Default max body: 10 MB
   - Video upload endpoint: 100 MB (Phase 0.15)
5. Restrict CORS to configured allowed origins (remove hardcoded localhost in non-Development environments)
6. Add HSTS header in production

Acceptance criteria:
- POST /auth/login returns 429 after 10 rapid requests from the same IP
- Invalid MIME type on upload returns 422 with a clear error message
- FluentValidation errors return 400 with structured error payload
- CORS blocks requests from unlisted origins in production
- Upload size over 10 MB returns 413

---

### Bundle 9h — Integration Tests and Release Gates

#### P68 — Add Integration Tests for Phase 0.5 Services

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Extend the backend test suite to cover all Phase 0.5 implementations: JWT auth, Promoter Profiles, media upload contracts, and security controls.

Context:
Phase 0 shipped with 48 xUnit tests covering domain models and endpoint contracts. Phase 0.5 adds JWT, Promoter Profiles, Cloudinary upload, analytics sink, and rate limiting. Each new service needs test coverage before Phase 0.15 begins.

What to do:
1. Add JWT auth tests:
   - POST /auth/login returns 200 with valid JWT
   - POST /auth/login returns 401 for wrong credentials
   - Protected endpoint returns 401 without token
   - Protected endpoint returns 200 with valid token
2. Add Promoter Profile tests:
   - POST /api/promoters creates profile with unique slug
   - Duplicate slug returns 409
   - GET /api/promoters/{slug} returns correct data
   - Admin verify endpoint changes VerificationStatus
3. Add Media Asset tests:
   - MediaAsset domain model validation (MIME types, size limits)
   - FeedPost with linked media returns correct structure
   - Sort order reordering preserves all assets
4. Add rate limiting tests:
   - Confirm 429 after exceeding rate limit
5. Update frontend Jest tests for new promoter service TypeScript interfaces

Acceptance criteria:
- Total test count ≥ 80 (up from 91 total; 48 backend → 65+ backend)
- All new tests pass
- All 43 existing frontend tests still pass
- Test suite completes in < 5 seconds

---

---

# Pack 10 — Phase 0.15 Media Signal Pack

**From static flyers to real media ingestion**

## Bundle Titles
- Bundle 10a — Flyer Media Intake (P25–P27)
- Bundle 10b — Video Flyer Pipeline (P28–P30)
- Bundle 10c — Asset Moderation and Compliance (P31)
- Bundle 10d — Venue Media Publishing (P32)
- Bundle 10e — Media Search and Ranking (P33)

---

### Bundle 10a — Flyer Media Intake

#### P69 — Build Full Flyer Upload Service with Lifecycle States

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Implement the production flyer upload service with full lifecycle state management per the Phase 0.15 specification.

Context:
P61 added Cloudinary for basic uploads. Phase 0.15 requires a full lifecycle: Initialized → Uploading → Uploaded → ValidationFailed → ProcessingPending → ReviewPending → Approved/Rejected/Archived. Uploads must persist across restarts, validate thoroughly, and detect duplicates.

What to do:
1. Add a UploadLifecycleStatus enum with all 8 states
2. Extend MediaAsset to include LifecycleStatus, ContentHash (SHA-256), and ProcessingJobId
3. Implement duplicate detection on upload: compute SHA-256 hash, check MediaAssets table for existing hash, return existing assetId with "duplicate" warning header if found
4. Add dimension validation on image uploads: reject images < 600×800 px with 422
5. Add size validation: reject images > 10 MB
6. Add MIME whitelist enforcement: only image/jpeg and image/png allowed for flyers
7. Implement lifecycle state transitions as guarded methods (prevent illegal jumps)
8. Add POST /api/media/uploads (multipart) endpoint — distinct from existing /api/media/flyers
9. Add EF migration for new MediaAsset fields

Acceptance criteria:
- Upload transitions through lifecycle states correctly
- Duplicate image returns existing assetId with HTTP 200 + Duplicate: true header
- Image under 600×800 returns 422 with field-level error
- Image over 10 MB returns 413
- Non-JPEG/PNG returns 422 with allowed types in error message
- Lifecycle status persists across restarts


#### P70 — Implement Venue Asset Library and Reusable Media Catalog

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Allow venue owners to build and reuse a media library across multiple event submissions.

Context:
Without asset reuse, venues upload the same logos, banners, and flyers repeatedly. The venue asset library creates a catalog that can be attached to any event submission.

What to do:
1. Add VenueId as a foreign key on MediaAsset (nullable — an asset can belong to a venue or a promoter)
2. Create GET /api/venues/{venueId}/assets — list all approved assets for a venue
3. Create POST /api/venues/{venueId}/assets — upload an asset directly to venue library
4. Add role enforcement: only venue owner or venue admin can manage venue assets
5. Add POST /api/events/submissions/{id}/attach-media — attach a venue library asset to a submission
6. Create a MediaAsset catalog projection DTO for frontend display

Acceptance criteria:
- Venue owner can upload to their library and see all their assets
- Asset can be attached to multiple event submissions without duplication
- Non-authorized users receive 403 on venue asset endpoints

---

### Bundle 10b — Video Flyer Pipeline

#### P71 — Implement Video Flyer Upload and Background Processing Job

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Accept video flyer uploads (up to 100 MB), queue a processing job, and extract poster frames via Cloudinary.

Context:
Phase 0.15 Bundle 10 adds video flyer support. Cloudinary handles transcoding and frame extraction, eliminating the need for local FFmpeg. A background job model must track processing state and register derived assets.

What to do:
1. Extend POST /api/media/video-uploads to accept video files (mp4, mov, webm, max 100 MB)
2. Upload to Cloudinary with eager transformation: extract poster at 1 second, generate thumbnail
3. Register a VideoProcessingJob record (Status: Queued → Running → Succeeded → Failed)
4. On Cloudinary callback (webhook) or polling: register derived MediaAsset records:
   - MediaAssetType = VideoPoster (from poster extraction)
   - MediaAssetType = VideoKeyFrame (from frame extraction)
5. Link derived assets back to original video upload via ParentAssetId
6. Add EF migration for VideoProcessingJobs table and derived asset fields

Acceptance criteria:
- Video upload accepted up to 100 MB
- Videos over 100 MB return 413
- Unsupported video MIME types return 422
- VideoProcessingJob record created on upload
- Derived poster asset linked to original video
- Derived assets have CDN URLs

---

### Bundle 10c — Asset Moderation and Compliance

#### P72 — Build Media Moderation Queue and Compliance Controls

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Implement the media moderation queue so uploaded assets are reviewed before appearing in production.

Context:
Without moderation, any uploaded image or video appears immediately. Phase 0.15 requires all media to pass through ReviewPending before being Approved. Rejected assets are quarantined and audit-logged.

What to do:
1. Create GET /api/moderation/media-queue — list assets in ReviewPending state, paginated
2. Create POST /api/moderation/media-queue/{itemId}/approve — set LifecycleStatus = Approved, audit log
3. Create POST /api/moderation/media-queue/{itemId}/reject — set LifecycleStatus = Rejected, move to quarantine, audit log with reason
4. Add policy checks on upload:
   - MIME type validation (already in P69)
   - File size (already in P69)
   - NSFW detection stub (IContentModerationService — returns Safe for all in Phase 0.15)
   - Copyright flag stub (always returns NoConcern in Phase 0.15)
5. Rejected assets invisible from all public endpoints
6. Create MediaModerationLog table (assetId, action, moderatorId, reason, timestamp)
7. Add EF migration

Acceptance criteria:
- All new uploads appear in moderation queue with status ReviewPending
- Approving an asset makes it visible on public endpoints
- Rejecting an asset removes it from all public responses
- Moderation actions are logged with moderator identity and reason
- Quarantined assets can be listed by admins but not public users

---

### Bundle 10d — Media-Derived Event Enrichment

#### P73 — Implement OCR Confidence Review and LLM Normalization for Media

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Connect the flyer OCR and LLM normalization pipeline (P11) to the media upload flow so uploaded flyers automatically enrich event data.

Context:
P11 implemented OCR and LLM normalization as stubs. P25 (Phase 0 Media Upload) provided the upload endpoint. Phase 0.15 connects them: when a flyer is uploaded, extract text via OCR, normalize to EventAggregate fields, and populate a submission draft.

What to do:
1. On successful flyer upload (LifecycleStatus = ProcessingPending), trigger OCR pipeline:
   - Call IOcrService with the Cloudinary image URL
   - Receive raw text + per-field confidence scores
2. Pass OCR output to ILlmEventNormalizer:
   - Normalize to canonical EventAggregate fields (title, date, venue, address, category)
3. Create a draft EventSubmission pre-populated with normalized fields
4. Set OCR confidence scores on the submission (extractionConfidence per field)
5. If overall confidence > 0.8: move to NEEDS_REVIEW with moderator flag "high confidence"
6. If confidence < 0.8: move to NEEDS_REVIEW with flag "manual review required"
7. Expose the pre-populated draft submission via GET /api/events/submissions/{id}

Acceptance criteria:
- Flyer upload triggers OCR pipeline automatically
- Normalized fields appear in draft submission
- Confidence scores are stored per field
- High-confidence submissions are flagged differently than low-confidence ones
- OCR errors do not fail the upload (graceful degradation to empty draft)


#### P74 — Implement Thumbnail Scoring and Media-Enriched Event Projections

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Score media assets and select the primary poster for each event, then return media-enriched projection DTOs to the frontend.

Context:
The frontend currently receives a plain image URL on map cards and calendar entries. Phase 0.15 requires media-enriched projections that include scored thumbnails selected from multiple flyer assets.

What to do:
1. Implement IThumbnailScorer that scores MediaAssets:
   - Visual quality (use Cloudinary quality score if available, else default 0.5)
   - Confidence from OCR pipeline (from P73)
   - Asset freshness (newer = higher)
   - Asset type preference: flyer > venue photo > placeholder
   - Returns a numeric score 0–1
2. Set PrimaryThumbnailAssetId on EventEntity to the highest-scoring approved asset
3. Create media-enriched projection DTOs:
   - EventMapMediaProjection: eventId, title, startTime, geoPoint, primaryThumbnailUrl, thumbnailScore
   - EventCalendarMediaProjection: eventId, title, startTime, endTime, primaryThumbnailUrl
   - EventDetailMediaProjection: all fields + all approved media assets
4. Update GET /api/events/map and GET /api/events/calendar to return media-enriched projections
5. Update GET /api/events/{id} to return EventDetailMediaProjection

Acceptance criteria:
- Map feed returns thumbnail URLs from Cloudinary (not placeholder paths)
- Thumbnail scoring is deterministic for identical inputs
- Calendar feed shows scored thumbnails
- Event detail returns all approved media assets in a sorted gallery
- Frontend displays Cloudinary CDN images in map cards

---

---

# Pack 11 — Phase 0.20 Trust Graph Pack

**From public discovery to gated cultural access**

## Bundle Titles
- Bundle 11a — Trust Identity Graph (P33)
- Bundle 11b — Invite Tier Engine (P34)
- Bundle 11c — Proximity and Reveal Rules (P35–P36)
- Bundle 11d — Reputation and Anti-Abuse (P37–P38)
- Bundle 11e — Trust Moderation and Appeals (P39–P40)

---

### Bundle 11a — Trust Identity Graph

#### P75 — Design User Trust Graph, Nodes, Edges, and Privacy Boundaries

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Create the trust graph domain model — nodes (users), edges (invites, interactions), and privacy rules — that will power tier-based event visibility.

Context:
Phase 0.20 gates certain events behind trust tiers. Without a trust graph, all events are equally visible. With it, WeUP can surface private events only to users who have been invited by someone close to the event.

What to do:
1. Create domain models in WeUP.Domain/Trust/:
   - UserTrustNode: UserId, CreatedAt, ReputationScore (0.0–1.0), TierLevel (Public=0, Adjacent=1, Private=2)
   - InviteEdge: Id, InviterId, InviteeId, Tier, ExpiresAt, AcceptedAt, RevokedAt, Status (Pending/Accepted/Expired/Revoked)
   - InteractionEdge: UserId, EventId, InteractionType (Attended, Saved, Rated), OccurredAt, RatingScore?
2. Create EF Core entities and add to WeUpDbContext
3. Define IUserTrustGraphRepository with:
   - GetNodeAsync(userId)
   - GetEdgesForUserAsync(userId)
   - AddInviteEdgeAsync(edge)
   - UpdateReputationScoreAsync(userId, score)
4. Create stub implementation
5. Add EF migration
6. Privacy rule: trust graph edge data is never exposed in public API responses — only visibility decisions derived from it

Acceptance criteria:
- Trust graph entities created and migrated
- Repository interface defined and stub registered
- Privacy rule documented in code comments
- 48 existing tests still pass


#### P76 — Implement Tier Unlock Engine and Visibility Contracts

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the tier unlock engine that computes per-user event visibility based on their trust graph position.

Context:
P75 created the trust graph model. P76 uses it: given a userId, compute their maximum visibility tier, then filter event queries to include only events at or below that tier.

What to do:
1. Implement ITierUnlockService:
   - GetUserTierAsync(userId): traverse graph edges to determine maximum accessible tier
   - GetVisibleEventIdsAsync(userId, bounds): return set of eventIds the user can see within bounds
2. Extend GET /api/events/map to accept optional trustLevel header or query param
3. Apply tier filter in ViewportQueryService: events with required tier > user tier are hidden (return isVisible: false rather than omitting, so locked pins can render)
4. Add GET /api/graph/tier/{userId} endpoint returning tier level and radius entitlements
5. Define tier radii: Public (no restriction), Adjacent (0–30 km), Private (0–10 km)
6. Feature-flag gate: trust tier filtering disabled unless WeUP:Features:TrustTierEnabled = true

Acceptance criteria:
- With TrustTierEnabled = false: all events visible (backward compatible)
- With TrustTierEnabled = true: events above user tier hidden or marked isVisible: false
- GET /api/graph/tier/{userId} returns correct tier and radius
- Tier calculation is consistent across multiple calls for the same user

---

### Bundle 11b — Invite Tier Engine

#### P77 — Implement Invite Issuance, Acceptance, Expiry, and Revocation

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the full invite lifecycle so users can invite others into their trust tier.

Context:
P75 modeled InviteEdge. P77 implements the user-facing invite flow: issue an invite, accept or decline, automatic expiry, and revocation.

What to do:
1. Create invite API endpoints:
   - POST /api/invites — issue an invite (inviterId, inviteeEmail, tier, expiresAfterDays)
   - POST /api/invites/{id}/accept — invitee accepts, creates confirmed InviteEdge
   - POST /api/invites/{id}/decline — invitee declines, marks edge Declined
   - DELETE /api/invites/{id} — inviter revokes, marks edge Revoked
   - GET /api/invites/sent — list invites sent by current user
   - GET /api/invites/received — list invites received by current user
2. Add background job (or endpoint-triggered): expire invites past ExpiresAt
3. Enforce invite limits per tier (configurable, default: 10 active invites per user)
4. On invite acceptance: update recipient's UserTrustNode tier if edge tier > current tier
5. Add InviteNotification model (email stub — log to console in Phase 0.5)

Acceptance criteria:
- POST /api/invites creates a pending invite edge
- POST /api/invites/{id}/accept updates recipient's tier
- Expired invites cannot be accepted
- Revoked invites cannot be accepted
- Active invite count is enforced per user

---

### Bundle 11c — Proximity Scoring and Reveal Rules

#### P78 — Add Proximity Scoring and Private Event Reveal Contracts

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Implement proximity-based visibility scoring so trust tier + geographic proximity together determine whether a private event is revealed.

Context:
A user in Adjacent tier can see events within 30 km. A user in Private tier can see events within 10 km. Proximity is computed from the user's current map center against the event's geoPoint.

What to do:
1. Implement IProximityScoreService:
   - ComputeScore(userLocation, eventLocation): returns 0–10 (10 = within 1 km, 0 = over 30 km)
   - DetermineVisibility(userTier, proximityScore, eventRequiredTier): returns isVisible bool
2. Extend GET /api/events/map response to include isVisible and requiredTier per event
3. Add "locked pin" contract: hidden events return id, geoPoint, requiredTier, and isVisible: false — but no title, description, or time
4. Frontend can render a locked pin at the location; tapping it shows a "Unlock Required" modal
5. Add ProximityRevealRequest: POST /api/events/{id}/reveal — checks user tier and proximity, returns full event or 403

Acceptance criteria:
- Events beyond proximity radius return isVisible: false even for adjacent-tier users
- Locked pin response contains geoPoint but no PII or event details
- POST /api/events/{id}/reveal returns full event for eligible users
- POST /api/events/{id}/reveal returns 403 for ineligible users
- Proximity score computation is tested with distance boundary cases

---

### Bundle 11d — Reputation and Anti-Abuse

#### P79 — Implement Reputation Signals and Anti-Abuse Rules

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the reputation scoring system and basic anti-abuse controls that govern tier escalation.

Context:
Trust tiers without reputation signals are gameable — anyone can invite others to max tier immediately. Reputation must be earned through verifiable actions: attendance, hosting, referrals.

What to do:
1. Define reputation signals and weights:
   - Attended an event: +0.05 per verified attendance
   - Hosted a published event: +0.10 per approved event
   - Successful invite (invitee accepted): +0.03 per invite
   - Invite revoked by invitee: -0.05
   - Flagged for abuse: -0.20
2. Implement IReputationScoringService that recalculates score from all signals (idempotent)
3. Add background job trigger: recalculate reputation after any relevant action
4. Anti-abuse heuristics:
   - Rapid invite spam: > 5 invites in 1 hour → auto-flag UserFlag with reason "invite_spam"
   - Duplicate submissions: > 3 identical submissions in 1 day → auto-flag "submission_spam"
   - Flagged users cannot earn tier escalation until flag is cleared
5. Create UserFlag entity (UserId, Reason, AutoFlagged, ReviewedAt, ClearedAt)
6. Add GET /api/admin/users/{id}/flags for admin review

Acceptance criteria:
- Reputation score recalculates correctly after each signal
- Auto-flag triggers correctly on spam thresholds
- Flagged users' tier does not increase
- Admin can view and clear flags
- Score calculation is deterministic for identical signal sets

---

---

# Pack 12 — Phase 0.25 Operator Revenue Pack

**From user app to venue/sponsor operating system**

## Bundle Titles
- Bundle 12a — Venue Console (P41)
- Bundle 12b — Sponsor Console (P42–P43)
- Bundle 12c — Campaign and Attribution (P44)
- Bundle 12d — Monetization and Payout Ops (P45–P46)
- Bundle 12e — Admin Governance (P47–P48)

---

### Bundle 12a — Venue Console

#### P80 — Build Venue Portal for Event Submission, Editing, and Analytics

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the venue-facing portal that allows venue operators to manage events, view analytics, and submit for verification.

Context:
Phase 0.25 turns WeUP into an operating system for venues. A venue owner must be able to create and edit events, see who is saving/attending, and submit for platform verification.

What to do:
1. Create Venue entity (Id, Name, Slug, Address, GeoPoint, VerificationStatus, OwnerId)
2. Add RBAC roles: VenueOwner, VenueEditor — enforced on all venue-scoped endpoints
3. Create venue portal endpoints:
   - GET /api/venues/{venueId} — venue profile
   - POST /api/venues/{venueId}/events — submit event under venue identity
   - PUT /api/venues/{venueId}/events/{eventId} — edit event (draft or published)
   - GET /api/venues/{venueId}/analytics — impressions, saves, attendance, revenue
4. Create venue verification workflow:
   - POST /api/venues/{venueId}/verification-request — submit legal docs (stub: logs request)
   - Admin endpoint: POST /api/admin/venues/{venueId}/verify — sets VerificationStatus = Verified
5. Verified venues can publish events directly (bypass moderation queue); unverified are always DRAFT

Acceptance criteria:
- Venue owner can submit an event that appears under venue identity
- Non-owner returns 403 on venue edit endpoints
- Verified venue events skip moderation queue
- Unverified venue events remain in DRAFT
- Analytics endpoint returns impression and save counts (real data from DB)


#### P81 — Build Sponsor Portal for Geo-Activated Campaign Creation

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the sponsor-facing portal for creating geo-targeted event promotion campaigns.

Context:
Sponsors pay to have their brand associated with events in specific geographic areas and trust tiers. This is WeUP's primary revenue stream in Phase 0.25.

What to do:
1. Create Sponsor entity (Id, Name, ContactEmail, BillingEmail, VerificationStatus)
2. Create SponsorCampaign entity (Id, SponsorId, Name, Status, Radius, CenterGeoPoint, Budget, CPM, StartDate, EndDate, TargetTiers[])
3. Create sponsor endpoints:
   - GET /api/sponsors/{sponsorId} — sponsor profile
   - POST /api/sponsors/{sponsorId}/campaigns — create campaign
   - GET /api/sponsors/{sponsorId}/campaigns — list campaigns with status
   - GET /api/sponsors/{sponsorId}/campaigns/{campaignId}/metrics — impressions, saves, conversions
4. Campaign validation: budget > 0, start < end, radius 1–50 km, at least one tier selected
5. On campaign creation: validate inventory availability (daily cap per district not exceeded)
6. Return sponsored: true on events in GET /api/events/map when includeSponsored=true query param provided

Acceptance criteria:
- Sponsor can create a campaign with geo + tier targeting
- Map feed returns sponsored events with sponsored: true flag
- Campaign metrics accumulate per impression/save event
- Budget validation rejects 0-budget campaigns
- District inventory cap prevents over-selling

---

### Bundle 12b — Campaign Attribution and Monetization

#### P82 — Implement Campaign Attribution and Billing Backbone

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Track sponsor campaign attribution signals and generate invoices and venue payouts.

Context:
P81 created campaigns. P82 closes the revenue loop: attribute user actions (view, save, attend) to campaigns, generate sponsor invoices, and calculate venue revenue shares.

What to do:
1. Record attribution signals on user actions:
   - Map card view of sponsored event → CampaignImpression
   - Save of sponsored event → CampaignSave
   - Attendance (venue check-in stub) → CampaignAttendance
2. Create SponsorCampaignMetrics table (CampaignId, Date, Impressions, Saves, Attendances, SpendUSD)
3. Implement billing service:
   - POST /api/billing/invoices — generate invoice for a sponsor for a billing period
   - GET /api/billing/invoices/{id} — retrieve invoice with line items
   - Invoice line items: CPM charge per 1000 impressions, CPC per save, CPA per attendance
4. Implement settlement engine (nightly batch or on-demand endpoint):
   - Calculate venue revenue share per save/attendance conversion
   - Write to Payouts table (VenueId, Amount, Period, Status)
   - GET /api/venues/{venueId}/payouts — list payout history
5. Revenue sharing percentages configurable: WeUP:Revenue:VenueShare, PlatformShare (default 70%/30%)

Acceptance criteria:
- Every sponsored event impression creates a CampaignImpression record
- Invoice generated with correct CPM/CPC/CPA calculations
- Venue payout calculated at configured revenue share
- Payout history accessible to venue owners
- Revenue config is changeable without code deploy (appsettings)

---

### Bundle 12c — Admin Governance

#### P83 — Build Operator Admin Console for City-Level Oversight

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the city-level admin console giving operators a real-time view of venues, sponsors, campaigns, trust graph health, and revenue KPIs.

Context:
WeUP operators need a single view of the city they manage. This is not a frontend-heavy feature — it is a set of read-only API endpoints that power a minimal dashboard UI.

What to do:
1. Create admin-only KPI endpoints (role: Admin):
   - GET /api/admin/city/{cityId}/overview — active venues count, sponsor campaign count, active events, saves today, media queue length, revenue YTD
   - GET /api/admin/city/{cityId}/trust — tier distribution (% Public/Adjacent/Private), active invites, pending flags
   - GET /api/admin/city/{cityId}/media — pending media queue size, rejection rate, average OCR confidence
   - GET /api/admin/city/{cityId}/revenue — sponsor spend YTD, venue payouts pending, top-spending campaigns
2. Add PATCH /api/markets/{cityId}/status — set market to Active/Frozen/Closed (from Phase 0 MarketPolicyService)
3. City-level data partitioned by cityId (already in MarketPolicyService from P20)
4. All admin endpoints require Admin role claim in JWT

Acceptance criteria:
- Admin overview returns non-zero counts from real DB data
- Market freeze correctly blocks new event submissions (not just logging)
- Non-admin JWT returns 403 on all admin endpoints
- Revenue figures match billing service calculations

---

---

# Pack 13 — Phase 0.30+ Intelligence Pack

**From city map to adaptive cultural intelligence network**

## Bundle Titles
- Bundle 13a — Cultural Ranking Engine (P49)
- Bundle 13b — Live Signal Fusion (P50)
- Bundle 13c — Personalization and Discovery (P51)
- Bundle 13d — GeoAudio / Ambient Detection (P52)
- Bundle 13e — City Expansion Control Plane (P55)
- Bundle 13f — Experimentation and Model Governance (P56)

---

### Bundle 13a — Cultural Ranking Engine

#### P84 — Build Event Ranking Engine Using Freshness, Density, Trust, and Intent

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Replace chronological event ordering with a cultural ranking engine that scores events by freshness, density, trust, and intent signals.

Context:
Phase 0 sorts events by start time. Phase 0.30+ sorts by cultural relevance. A "hot" underground show starting tonight outranks a mediocre event posted two weeks ago for Friday night.

What to do:
1. Implement EventRankingEngine with a composite score:
   - Freshness (0–0.3): inverse of hours since event start (events closer to now score higher)
   - Density (0–0.2): count of events per km² in the event's district (normalize against city average)
   - Trust (0–0.3): venue verification status + host reputation score + OCR confidence
   - Intent (0–0.2): saves count + share count + attendance confirmations (normalized per week)
2. Store RankingScore and RankingBucket (Hot, Trending, New, Standard) on EventEntity
3. Add GET /api/events/ranked?city={cityId}&limit=50 — returns events ordered by score descending
4. Add cursor-based pagination on ranked endpoint
5. Recalculate ranking scores on a schedule (every 30 minutes stub — just recalculate on request in Phase 0.30)
6. Add modelVersionId field to EventEntity for future A/B rollback capability

Acceptance criteria:
- GET /api/events/ranked returns events in score-descending order
- Events with more saves rank higher than identical events with fewer saves
- Freshness decay is observable (events > 48 hours old score lower)
- Ranking is deterministic for identical inputs (same score = same order)
- modelVersionId stored with each ranking snapshot


#### P85 — Add Live Signal Fusion from User Actions and Venue Activity

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Continuously update event ranking scores using real-time signals from user saves, opens, and venue activity.

Context:
P84 built the ranking engine with batch recalculation. P85 makes it reactive: a sudden surge in saves on an event should bump its score within 30 seconds, not 30 minutes.

What to do:
1. Create EventLiveSignal table (EventId, SignalType, OccurredAt, Weight)
2. On every save, open, or share: write an EventLiveSignal record
3. Implement live signal aggregator (scheduled every 30 seconds in Phase 0.30 — simple DB polling):
   - Read signals from last 30 seconds
   - Add signal weight to event's intent score component
   - Recalculate RankingScore for affected events
4. Add GET /api/events/{id}/signals endpoint (admin only) — recent signals for debugging
5. Create ILiveSignalProcessor with ProcessBatchAsync(signals) for testability
6. Feature-flag gate: live signal fusion disabled unless WeUP:Features:LiveSignalFusion = true

Acceptance criteria:
- Event save creates an EventLiveSignal record
- RankingScore updates within 30 seconds of signal write (when feature enabled)
- Live signal processor handles empty batches without error
- Signal processing is idempotent (re-processing same signals does not double-score)

---

### Bundle 13b — Personalization and Discovery

#### P86 — Implement Personalized Discovery Without Breaking Cold-Start UX

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Add personalized event recommendations for returning users while guaranteeing a populated discovery view for new users.

Context:
Phase 0 shows the same map to all users. Phase 0.30+ adapts discovery to each user's taste, trust graph, and interaction history. The cold-start guardrail is critical: a new user with no history must see the same quality content as a returning user.

What to do:
1. Implement IRecommendationService:
   - GetRecommendationsAsync(userId, bounds, limit): collaborative-filtering stub (Phase 0.30 = content-based on saves + trust-graph edges)
   - Cold-start check: if user has < 5 saved events, fall back to GET /api/events/ranked (city trending)
2. Create EventRecommendationProjection DTO: eventId, title, score, recommendationReason (TrendingInDistrict / MatchesSavedCategory / TrustedByConnection)
3. Add GET /api/users/me/recommendations endpoint
4. Ensure private events are never recommended to users without the required trust tier
5. Feature-flag gate: recommendations disabled unless WeUP:Features:Personalization = true

Acceptance criteria:
- New users (< 5 saves) receive city trending results
- Returning users receive personalized results ordered by relevance
- Private events never appear for ineligible users regardless of recommendation score
- Cold-start fallback is indistinguishable from ranked endpoint in quality

---

### Bundle 13c — GeoAudio / Ambient Detection

#### P87 — Prototype GeoAudio Ingestion and Venue Audio Matching

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build the GeoAudio ingestion pipeline and venue audio matching prototype as a feature-flagged experimental layer.

Context:
GeoAudio is WeUP's differentiator for nightlife discovery — ambient sound from a venue (BPM, genre, crowd noise) provides a trust signal that no other platform has. Phase 0.30 is the prototype phase: ingestion must work, matching must be testable, but it is not customer-facing without the flag enabled.

What to do:
1. Create POST /api/audio/ingest endpoint:
   - Accept short ambient audio clips ≤ 10 seconds (mp3, wav, m4a)
   - Upload to Cloudinary audio delivery
   - Extract metadata via IVenueAudioAnalyzer stub (returns BPM=120, Genre=Electronic, CrowdLevel=0.7)
   - Store in VenueAudioProfile (VenueId, ClipUrl, BPM, Genre, CrowdNoiseLevel, Confidence, RecordedAt)
2. Implement audio similarity scoring stub:
   - CompareAudioProfiles(a, b): returns similarity 0–1 based on BPM and genre match
   - Venues with similarity > 0.8 are considered "matching" for the Live-Now badge
3. Add VenueAudioScore table (VenueId, MatchedVenueId, SimilarityScore, ComputedAt)
4. Add Live-Now badge contract: GET /api/events/map returns liveNow: true for events at matching venues when audio confidence > 0.8
5. Feature-flag gate: WeUP:Features:GeoAudio = false by default

Acceptance criteria:
- Audio clip uploads successfully with 10-second limit enforced
- VenueAudioProfile created on successful ingestion
- Similarity scoring is deterministic for identical inputs
- Live-Now badge only appears when GeoAudio feature flag is enabled and confidence > 0.8
- 10-second limit returns 422 with clear error message

---

### Bundle 13d — Experimentation and Model Governance

#### P88 — Add Experimentation Framework, Model Versioning, and Safety Rollback

You are working inside the WeUP_Phase0_AIStudio repository.

Goal:
Build an experimentation framework that allows ranking and recommendation algorithms to be A/B tested and safely rolled back.

Context:
Phase 0.30+ will have multiple ranking model versions, live signal fusion variants, and personalization algorithm candidates. Without experiment governance, a bad model can degrade the entire city's discovery feed with no rollback path.

What to do:
1. Create Experiments table (Id, Name, Description, AlgorithmType, ModelVersionId, StartDate, EndDate, Status, UserAllocationPct)
2. Create ExperimentAssignments table (UserId, ExperimentId, Variant, AssignedAt)
3. Implement IExperimentRouter:
   - GetVariantForUser(userId, experimentName): returns "control" or "treatment"
   - Variant assignment is consistent per user (hash-based, not random per request)
4. Integrate experiment router into EventRankingEngine:
   - Control variant: existing ranking algorithm
   - Treatment variant: configurable via modelVersionId
5. Add safety rollback endpoint:
   - POST /api/experiments/{expId}/rollback — re-computes rankings using control model, logs diff
   - Stores rollback record in ExperimentRollbacks table
6. Add GET /api/admin/experiments — list active experiments with conversion metrics
7. Store modelVersionId with each ranking snapshot for auditability

Acceptance criteria:
- User assignment to experiment variant is stable across requests
- POST /api/experiments/{expId}/rollback successfully reverts to control algorithm
- Ranking snapshots include modelVersionId
- 100% of users can be assigned to "control" variant as a full rollback
- Experiment metrics (impressions, saves per variant) are queryable

---

## Recommended Bundle Execution Order

```
Pack 9  → Pack 10 → Pack 11 → Pack 12 → Pack 13
```

Do not invert this order. Building the trust graph (Pack 11) before the media pipeline (Pack 10) is built means operating on a system with no real content. Building the operator revenue layer (Pack 12) before trust gating (Pack 11) is complete means sponsors paying for placements that have undefined visibility rules.

Each pack depends on the previous:
- Pack 9 makes the data durable
- Pack 10 makes the content real
- Pack 11 makes access meaningful
- Pack 12 makes WeUP a business
- Pack 13 makes WeUP intelligent

---

## Naming Convention

Consistent with the 030 Prompt Bundle Pack:

```
Pack:   WeUP_Pack_09_Phase0.5_Production
Bundle: B09a_EF_Core_Migration
Prompt: P57_Run_EF_Core_Initial_Migration
```

---

## Prompt Index

| Prompt | Bundle | Description |
|--------|--------|-------------|
| P57 | 9a | EF Core Initial Migration + Repository Activation |
| P58 | 9a | Remaining EF Core Repository Swaps |
| P59 | 9b | JWT Authentication |
| P60 | 9b | Refresh Tokens and Session Persistence |
| P61 | 9c | Cloudinary Media Storage |
| P62 | 9d | Segment Analytics Sink |
| P63 | 9d | OpenTelemetry OTLP Export |
| P64 | 9e | Promoter Profile Domain Model and EF Entities |
| P65 | 9e | Promoter Profile API Endpoints |
| P66 | 9f | Promoter Media Upload with Cloudinary |
| P67 | 9g | Rate Limiting, CSRF, and Input Validation |
| P68 | 9h | Integration Tests and Release Gates |
| P69 | 10a | Full Flyer Upload Lifecycle |
| P70 | 10a | Venue Asset Library |
| P71 | 10b | Video Flyer Upload and Processing |
| P72 | 10c | Media Moderation Queue |
| P73 | 10d | OCR and LLM Normalization Pipeline |
| P74 | 10d | Thumbnail Scoring and Media-Enriched Projections |
| P75 | 11a | Trust Graph Domain Model |
| P76 | 11a | Tier Unlock Engine |
| P77 | 11b | Invite Issuance and Lifecycle |
| P78 | 11c | Proximity Scoring and Private Reveal |
| P79 | 11d | Reputation Signals and Anti-Abuse |
| P80 | 12a | Venue Portal |
| P81 | 12b | Sponsor Portal and Campaign Creation |
| P82 | 12b | Campaign Attribution and Billing |
| P83 | 12c | Admin Governance Console |
| P84 | 13a | Cultural Ranking Engine |
| P85 | 13a | Live Signal Fusion |
| P86 | 13b | Personalized Discovery |
| P87 | 13c | GeoAudio Ingestion and Matching |
| P88 | 13d | Experimentation Framework and Safety Rollback |

**Total extended prompts: P57–P88 (32 prompts)**
**Combined with original 030 pack: P01–P88 (88 prompts total)**

---

## Phase Scope Framing

Mirrors the 030 pack framing exactly:

| Phase | Scope Question |
|-------|---------------|
| Phase 0.5 | Can real users interact with real data, real auth, and real media? |
| Phase 0.15 | Can WeUP ingest and operationalize real flyer and video media? |
| Phase 0.20 | Can WeUP gate access through trust and proximity? |
| Phase 0.25 | Can venues and sponsors operate inside WeUP as customers? |
| Phase 0.30+ | Can WeUP become the cultural intelligence layer for a city? |

---

**Document Version:** 1.0
**Created:** 2026-04-04
**Authority:** WeUP Phase 0 – 030 Prompt Bundle Pack + Phase 0.1 FRD/TRD/PRD + Phase Checklist + 12-Month Roadmap
**Prompt Range:** P57–P88
