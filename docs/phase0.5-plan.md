# WeUP Phase 0.5 — Production Readiness Plan

**Status:** Planning
**Follows:** Phase 0 Complete (P01–P25, 91 tests passing)
**Precedes:** Phase 0.15 — Richer Media Ingestion
**Roadmap Alignment:** v1.0–v1.5 (July–September 2026)
**Source Authority:** WeUP Phase Checklist, 12-Month Roadmap, Phase 0.1 FRD/TRD/PRD, Pre-Deployment Action List, Canon Phase0, What This MVP Actually Is

---

## What Phase 0.5 Is

Phase 0.5 is the **production bridge** — it takes the fully-scaffolded Phase 0 stub system and replaces every in-memory and console-output service with a real, durable implementation. No new user-facing features are added. The goal is:

> **Make everything that already works in stubs work against real infrastructure, so Phase 0.15 can build on a solid foundation.**

From *"What This MVP Actually Is"*:
> *"This MVP is a spatial event coordination system... The system must feel different from Eventbrite + Instagram duct-taped together. If it doesn't feel different, it fails."*

Phase 0.5 ensures the backend is production-grade before that feeling is validated with real users.

---

## Current State After Phase 0

| Area | Status | Stub Location |
|------|--------|---------------|
| Event repository | In-memory (3 hardcoded events) | `StubEventRepository.cs` |
| Save repository | HashSet in-memory | `StubSaveRepository.cs` |
| User profiles | ConcurrentDictionary | `InMemoryUserRepository.cs` |
| User preferences | ConcurrentDictionary | `InMemoryPreferencesRepository.cs` |
| Itinerary | Per-user locked list | `InMemoryItineraryRepository.cs` |
| Moderation queue | ConcurrentDictionary | `InMemoryModerationQueue.cs` |
| Audit trail | Locked append-only list | `InMemoryAuditTrail.cs` |
| Submission workflow | In-memory per-user bag | `InMemorySubmissionRepository.cs` |
| Ingestion jobs | ConcurrentDictionary | `InMemoryIngestionJobRepository.cs` |
| Analytics | Console.Log | `ConsoleAnalyticsService.cs` |
| Media storage | Local filesystem | `LocalFlyerUploadService.cs` |
| Authentication | In-memory bearer tokens | `BearerTokenService.cs` |
| OCR | Stub (returns empty) | `StubOcrService.cs` |
| Geocoding | Stub (returns zero coords) | `StubGeocodingService.cs` |
| Observability | Console log only | No OTLP exporter |

**91 tests passing. Backend and frontend both build clean. All contracts are defined.**

---

## Pre-Deployment Checklist

*Derived from `WeUP Pre-deployment of MVP Action List.docx`*

### Business & Product
- [ ] Finalize MVP feature scope (confirmed: map-first city MVP)
- [ ] Define success metrics and KPIs for launch
- [ ] Create user personas (attendees, organizers, Tier 1/2/3 users)
- [ ] Identify 2–5 pilot events/partners for beta testing
- [ ] Draft event partnership agreements and terms
- [ ] Create pricing strategy (tickets, commerce commission, fees)

### Technical Infrastructure
- [ ] Provision PostgreSQL database (local Docker or hosted — Supabase, Neon, Railway, or RDS)
- [ ] Configure connection string `WeUpDb` in environment
- [ ] Run EF Core initial migration (`dotnet ef migrations add InitialSchema`)
- [ ] Provision S3-compatible storage (AWS S3, Supabase Storage, or Cloudinary)
- [ ] Configure Mapbox production token (rotate from dev token)
- [ ] Set up CI/CD pipeline (GitHub Actions: build → test → lint → package → staging → production)
- [ ] Configure HTTPS and domain (for production deployment)
- [ ] Set up analytics sink (Segment, Datadog, or equivalent)
- [ ] Configure OpenTelemetry OTLP exporter (Datadog, Honeycomb, or Jaeger)

### Security
- [ ] Generate JWT secret (min 256-bit) and store in secrets manager
- [ ] Enable CSRF protection
- [ ] Add rate limiting (upload endpoints, auth endpoints)
- [ ] Enforce upload size limits (images: 10 MB, video: 100 MB per Phase 0.15 spec)
- [ ] Add content-type validation for all upload endpoints
- [ ] Implement auth token revocation
- [ ] Restrict CORS to production domain (remove `localhost:3000` from production CORS policy)
- [ ] Audit all endpoints for unauthenticated access

### Quality & Testing
- [ ] All 91 tests passing on CI
- [ ] E2E smoke tests run against staging
- [ ] Seed data script verified (`npm run seed-data`)
- [ ] Load test core endpoints (map-feed, calendar, event detail)
- [ ] Review error states in frontend (empty, loading, error, success)

---

## Phase 0.5 Work Items

### Workstream 1: Database & Persistence

**Goal:** Replace all in-memory stubs with EF Core + PostgreSQL.

**Already implemented (ready to activate):**
- `backend/WeUP.Infrastructure/Persistence/WeUpDbContext.cs` — DbContext with 6 entity types
- `backend/WeUP.Infrastructure/Persistence/EfEventRepository.cs` — full spatial + temporal queries
- `backend/WeUP.Infrastructure/Persistence/EfSaveRepository.cs` — paginated saves with event details

**Steps:**
1. Add `Microsoft.EntityFrameworkCore.Design` to `WeUP.Infrastructure.csproj` and `WeUP.Api.csproj`
2. Run migration:
   ```bash
   dotnet ef migrations add InitialSchema \
     --project backend/WeUP.Infrastructure \
     --startup-project backend/WeUP.Api
   dotnet ef database update \
     --project backend/WeUP.Infrastructure \
     --startup-project backend/WeUP.Api
   ```
3. In `Program.cs`, uncomment and activate:
   ```csharp
   var connStr = builder.Configuration.GetConnectionString("WeUpDb")
       ?? throw new InvalidOperationException("WeUpDb connection string is required.");
   builder.Services.AddDbContext<WeUpDbContext>(opts => opts.UseNpgsql(connStr));
   builder.Services.AddScoped<IEventRepository, EfEventRepository>();
   builder.Services.AddScoped<IEventSubmissionRepository, EfEventRepository>();
   builder.Services.AddScoped<ISaveRepository, EfSaveRepository>();
   ```
4. Switch `Singleton` → `Scoped` for all repository registrations
5. Create EF implementations for remaining stubs:
   - `EfUserProfileRepository` (replaces `InMemoryUserRepository`)
   - `EfUserPreferencesRepository` (replaces `InMemoryPreferencesRepository`)
   - `EfItineraryRepository` (replaces `InMemoryItineraryRepository`)
   - `EfModerationQueueRepository` (replaces `InMemoryModerationQueue`)
   - `EfAuditTrailRepository` (replaces `InMemoryAuditTrail`)
   - `EfSubmissionRepository` (replaces `InMemorySubmissionRepository`)
   - `EfIngestionJobRepository` (replaces `InMemoryIngestionJobRepository`)

**Missing DB tables (need new entities + migrations):**
- `Itineraries`
- `UserPreferences`
- `ModerationQueue`
- `AuditTrail`
- `IngestionJobs`

**Configuration:**
```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "WeUpDb": "Host=localhost;Database=weup_dev;Username=weup;Password=YOUR_PASSWORD"
  }
}
```

---

### Workstream 2: Authentication — JWT

**Goal:** Replace `BearerTokenService` (in-memory token store) with real JWT.

**Steps:**
1. Add `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package
2. Configure JWT in `Program.cs`:
   ```csharp
   builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(opts => {
           opts.TokenValidationParameters = new TokenValidationParameters {
               ValidateIssuerSigningKey = true,
               IssuerSigningKey = new SymmetricSecurityKey(
                   Encoding.UTF8.GetBytes(builder.Configuration["WeUP:Auth:JwtSecret"]!)),
               ValidateIssuer = false,
               ValidateAudience = false,
           };
       });
   ```
3. Uncomment `app.UseAuthentication()` and `app.UseAuthorization()` in `Program.cs`
4. Add `[Authorize]` attributes to protected endpoints
5. Update `UserAuthService` to issue real JWTs (signed, expiring)
6. Implement refresh token flow
7. Add token revocation (blocklist in DB or Redis)

**Configuration:**
```json
// appsettings.json (via environment or secrets manager)
{
  "WeUP": {
    "Auth": {
      "JwtSecret": "YOUR_256_BIT_SECRET",
      "TokenExpiryMinutes": 60,
      "RefreshTokenExpiryDays": 30
    }
  }
}
```

---

### Workstream 3: Media Storage — S3 / Cloudinary

**Goal:** Replace `LocalFlyerUploadService` with cloud object storage.

**From `WeUP Phase 0.1 Feature Addition.docx`:**
> *"Use object storage for media: Supabase Storage, Cloudinary, Firebase Storage, S3-compatible bucket. For Phase 0, Cloudinary or Supabase Storage is practical. Cloudinary handles image optimization, video delivery, transformations, thumbnails."*

**Recommended for Phase 0.5:** Cloudinary (handles both image and video; relevant for Phase 0.15 video pipeline)

**Steps:**
1. Add `CloudinaryDotNet` NuGet package
2. Create `CloudinaryFlyerUploadService` implementing `IFlyerUploadService`:
   - Upload to Cloudinary with auto-transformation
   - Generate thumbnails on upload
   - Store Cloudinary public_id as `S3Url` field on `FlyerAsset`
   - Return CDN URL in response
3. Register in `Program.cs` (swap `LocalFlyerUploadService`)
4. Update `LocalFileStorageService` (flyer pipeline) → `CloudinaryStorageService`

**Configuration:**
```json
{
  "WeUP": {
    "Cloudinary": {
      "CloudName": "YOUR_CLOUD_NAME",
      "ApiKey": "YOUR_API_KEY",
      "ApiSecret": "YOUR_API_SECRET"
    }
  }
}
```

**Phase 0.15 Note:** Cloudinary also handles video transcoding, frame extraction, and poster generation — choosing it now avoids a storage migration when Phase 0.15 Video Pipeline (Bundle 10) is implemented.

---

### Workstream 4: Analytics Sink

**Goal:** Replace `ConsoleAnalyticsService` with a real event sink.

**Options (pick one):**
| Option | Best For | Cost |
|--------|---------|------|
| Segment | Multi-destination routing | Free tier available |
| Datadog | Unified with observability | Paid |
| PostHog | Self-hosted, open source | Free self-hosted |
| Mixpanel | Product analytics focus | Free tier available |

**Recommended for Phase 0.5:** Segment (routes to any downstream destination without re-deploys)

**Steps:**
1. Add Segment .NET SDK or use HTTP API directly
2. Create `SegmentAnalyticsService` implementing `IAnalyticsService`:
   - `RecordEventAsync` → `analytics.Track(userId, eventType, properties)`
   - `IsPrivacyCompliant()` guard remains enforced before send
3. Register in `Program.cs` (swap `ConsoleAnalyticsService`)
4. Map all 8 `AnalyticsEventType` values to Segment event names

**Configuration:**
```json
{
  "WeUP": {
    "Analytics": {
      "SegmentWriteKey": "YOUR_SEGMENT_WRITE_KEY"
    }
  }
}
```

---

### Workstream 5: Observability — OpenTelemetry Export

**Goal:** Export traces and metrics to an external backend.

**From `WeUP Phase Checklist Phase 0+.txt`:**
> *"OpenTelemetry SDK, structured logging, correlation-ID middleware (request → job → moderation) — Bundle 8 P22 (early Phase 0)"*

Correlation IDs are active. What's missing is the export.

**Steps:**
1. Add NuGet packages:
   ```
   OpenTelemetry.Extensions.Hosting
   OpenTelemetry.Instrumentation.AspNetCore
   OpenTelemetry.Instrumentation.Http
   OpenTelemetry.Exporter.OpenTelemetryProtocol
   ```
2. Uncomment and configure in `Program.cs`:
   ```csharp
   builder.Services.AddOpenTelemetry()
       .WithTracing(tracing => tracing
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddOtlpExporter(opts => opts.Endpoint = new Uri(
               builder.Configuration["WeUP:Observability:OtlpEndpoint"]!)))
       .WithMetrics(metrics => metrics
           .AddAspNetCoreInstrumentation()
           .AddOtlpExporter());
   ```
3. Configure Grafana, Datadog, or Honeycomb as OTLP receiver

**Configuration:**
```json
{
  "WeUP": {
    "Observability": {
      "OtlpEndpoint": "https://api.honeycomb.io",
      "ServiceName": "weup-api"
    }
  }
}
```

---

### Workstream 6: Promoter Profiles (Phase 0.1)

**Goal:** Implement the Promoter Identity Feed per `WeUP Phase 0.1 Feature Addition.docx`, `Phase 0.1 FRD`, `Phase 0.1 TRD`, and `Phase 0.1 PRD`.

**Product Framing (from Phase 0.1 PRD):**
> *"Extend WeUP Phase 0 into an identity-driven platform by introducing Promoter Profiles with media feeds (photos + short-form video). Users begin to recognize promoters, not just random flyers."*

**Core User Stories:**
- As a promoter: named profile with photos/videos → people trust my brand → follow my events
- As a user: browse a promoter's profile and media feed → decide if their events match my taste

#### Domain Model

**PromoterProfile:**
```
Id, DisplayName, Slug (unique, human-readable), Bio,
ProfileImageUrl, BannerImageUrl, City,
InstagramUrl, TikTokUrl, WebsiteUrl,
VerificationStatus (Pending/Verified/Rejected),
IsActive, CreatedAt, UpdatedAt
```

**MediaAsset:**
```
Id, PromoterProfileId, EventId?, VenueId?,
MediaType (Photo/Video), Url, ThumbnailUrl,
Caption, AltText, SortOrder,
VisibilityStatus (Draft/Published/Hidden/Flagged),
DurationSeconds?, Width, Height, FileSizeBytes,
CreatedAt, UpdatedAt
```
- Photos: jpg, jpeg, png, webp
- Videos: mp4, mov, webm (max 15–30 seconds for Phase 0.5)
- Max 5–10 images per post/batch
- Always generate thumbnails

**FeedPost:**
```
Id, PromoterProfileId, Title, Body,
LinkedEventId?, PostType (General/EventPromo/Recap/VenueHighlight),
PublishedAt, Status (Draft/Published/Archived/Flagged),
CreatedAt, UpdatedAt
```

**FeedPostMedia (join):**
```
FeedPostId, MediaAssetId, SortOrder
```

#### API Surface

**Promoter Profile Endpoints:**
```
POST   /api/promoters
GET    /api/promoters/{slug}
PUT    /api/promoters/{id}
GET    /api/promoters/{id}/events
GET    /api/promoters/{id}/feed
```

**Feed Endpoints:**
```
POST   /api/promoter-feed
PUT    /api/promoter-feed/{id}
DELETE /api/promoter-feed/{id}
GET    /api/promoter-feed/{id}
```

**Media Endpoints:**
```
POST   /api/media/upload
POST   /api/media/attach-to-post
DELETE /api/media/{id}
PUT    /api/media/{id}/sort-order
```

**Admin Endpoints:**
```
POST   /api/admin/promoters/{id}/verify
POST   /api/admin/media/{id}/hide
POST   /api/admin/media/{id}/flag
POST   /api/admin/promoters/{id}/suspend
```

#### New DB Tables Required
- `PromoterProfiles`
- `FeedPosts`
- `MediaAssets`
- `FeedPostMedia`
- `PromoterFollowers` (optional Phase 0.5)
- `MediaModerationLogs`

#### Frontend UX (from Phase 0.1 Feature Addition)

**Public Profile Layout:**
- Hero: banner, avatar, display name, bio, verify badge, follow button, social icons
- Tabs: Feed | Events | Gallery
- Feed tab: card-based vertical stream (title, caption, media carousel/video, event chip, date)
- Events tab: upcoming and past events
- Gallery tab: media grid only

**Promoter-side flows:**
1. Create profile → name, avatar, banner, bio, socials
2. Create feed post → type, media upload, caption, attach to event, publish
3. Manage media → reorder, delete, draft, preview, pin featured

#### Phase 0.5 Scope Constraints (from Phase 0.1 Feature Addition)
**In scope:**
- Named promoter profile
- Profile avatar + cover image
- Bio + social links
- Photo gallery
- Short video uploads (≤30s)
- Media feed timeline
- Event tagging
- Profile page visible to users

**Not yet (defer to later phases):**
- Live streaming
- Stories
- DMs
- Public comments
- Complex creator monetization
- Long-form video hosting
- Algorithmic recommendation engine

#### Moderation Requirements
Every upload must pass:
1. File type validation
2. Size validation
3. Duration validation (video)
4. Virus/malware scan if available
5. Basic content moderation queue

---

### Workstream 7: Security Hardening

**Goal:** Make Phase 0 production-safe before real users interact with it.

| Item | Implementation |
|------|---------------|
| CSRF protection | `builder.Services.AddAntiforgery()` + `[ValidateAntiForgeryToken]` on state-changing endpoints |
| Rate limiting | `builder.Services.AddRateLimiter()` — 60 req/min on auth endpoints, 10 req/min on upload |
| Upload size limits | `builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 10_485_760)` (10 MB) |
| Content-type validation | Add `IFormFile` MIME-type check before processing in upload endpoints |
| Auth token revocation | Blocklist table in DB, checked on each request |
| CORS hardening | Move `localhost:3000` to `appsettings.Development.json` only; production uses real domain |
| Input validation | Add FluentValidation for all request DTOs |
| HTTPS enforcement | `app.UseHsts()` in production; redirect HTTP → HTTPS |

---

## Phase 0.15 Preview — Richer Media Ingestion

*What comes immediately after Phase 0.5 (per `WeUP Phase Checklist Phase 0+.txt` and 12-Month Roadmap v1.12–v1.15)*

### Bundle 9 — Flyer Intake (v1.12–v1.13, Jan 2027)
- `POST /api/media/uploads` (multipart) → MediaAsset record with `uploadId`
- Upload lifecycle: Initialized → Uploading → Uploaded → ValidationFailed → ProcessingPending → ReviewPending → Approved/Rejected → Archived
- MIME whitelist: image/jpeg, image/png; max 10 MB; min 600×800 px
- Duplicate detection (hash-based) → returns existing `assetId` with "duplicate" warning
- IMediaStorageService abstraction (Cloudinary in Phase 0.5 satisfies this)

### Bundle 10 — Video Pipeline (v1.14–v1.15, Feb 2027)
- `POST /api/media/video-uploads` (up to 100 MB)
- Background `VideoProcessingJob` (queued on upload)
- FFmpeg-based: `IVideoFrameExtractor` → poster frame, N key frames
- Derived assets: `MediaAssetType = VideoPoster`, `VideoKeyFrame`
- Cloudinary handles this without FFmpeg if Cloudinary is adopted in Phase 0.5

### Bundle 11 — Media Moderation Queue
- `GET /api/moderation/media-queue`
- `POST /api/moderation/media-queue/{itemId}/approve | reject`
- Policy checks: file type, size, NSFW detection (stub), copyright flagging
- Quarantine for rejected assets

### Bundle 12 — Venue Asset Library
- `GET /api/venues/{venueId}/assets`
- `POST /api/venues/{venueId}/assets`
- Role-based: owner, venue-admin, sponsor

### Bundle 13 — Media-Derived Event Enrichment
- OCR pipeline (Google Vision / Tesseract) → raw fields
- LLM normalization → canonical `EventAggregate` fields
- Thumbnail scoring → primary poster for map/calendar cards
- Media-enriched DTOs: `EventMapMediaProjection`, `EventCalendarMediaProjection`, `EventDetailMediaProjection`

---

## 12-Month Roadmap Alignment

*From `WeUP – 12-Month High-Level Roadmap.txt`*

| Release | Date | What Ships |
|---------|------|-----------|
| v1.0 | Jul 2026 E | Runtime Spine (P01) — folder structure, World Surface Coordinator |
| v1.1 | Jul 2026 L | Env Contract (P02) — typed environment module |
| v1.2 | Aug 2026 E | UI State Refactor (P03) — reducer, no render-time mutation |
| v1.3 | Aug 2026 L | Canonical Event Aggregate (P04) |
| v1.4 | Sep 2026 E | Source Provenance & Confidence (P05) |
| v1.5 | Sep 2026 L | Temporal & Geospatial Query Contracts (P06) |
| v1.6 | Oct 2026 E | .NET 8 API Scaffold (P07) |
| v1.7 | Oct 2026 L | EF Core + Postgres Schema (P08) ← **Phase 0.5 DB work lands here** |
| v1.8 | Nov 2026 E | Map / Calendar / Detail / Save APIs (P09) |
| v1.9 | Nov 2026 L | Source Adapter Framework (P10) |
| v1.10 | Dec 2026 E | Flyer OCR + LLM Normalisation (P11) |
| v1.11 | Dec 2026 L | Deduplication & Entity Resolution (P12) |
| v1.12 | Jan 2027 E | Flyer Media Upload Service (P25) ← **Phase 0.15 begins** |
| v1.13 | Jan 2027 L | Flyer Validation & Lifecycle (P26) |
| v1.14 | Feb 2027 E | Video Flyer Intake & Job Registration (P28) |
| v1.15 | Feb 2027 L | Video Frame / Poster Extraction (P29) |
| v1.16 | Mar 2027 E | Thumbnail Scoring & Media Ranking (P36) |
| v1.17 | Mar 2027 L | Trust-Graph Identity Model (P33) ← **Phase 0.20 begins** |
| v1.18 | Apr 2027 E | Invite Tier Engine (P34) |
| v1.19 | Apr 2027 L | Proximity Scoring & Reveal Rules (P35) |
| v1.20 | May 2027 E | Venue Console (P41) ← **Phase 0.25 begins** |
| v1.21 | May 2027 L | Sponsor Console (P42) |
| v1.22 | Jun 2027 E | Cultural Ranking Engine (P49) ← **Phase 0.30+ begins** |
| v1.23 | Jun 2027 E | Phase 1 "City-Scale" Prep / Feature Freeze |
| v1.24 | Jun 2027 L | Phase 1 Launch — Multi-city, GeoAudio, Sponsor Marketplace, Partner API |

**Phase 0.5 work is the prerequisite for v1.7 and onwards shipping to production.**

---

## 5-Year Vision Context

*From `WeUP – 5-Year Feature Roadmap & Concept Matrix.txt`*

The six core user story pillars that Phase 0.5 must not violate:

1. **Discover** — privacy-first, invite-based discovery (no algorithm-driven feed)
2. **Unlock** — reveal events with privacy, attendance proof
3. **Buy** — instant, secure, fair purchase (Phase 1+)
4. **Arrive** — venue knows crowd, no surveillance
5. **Participate** — engage without being surveilled
6. **Persist** — cultural artifacts that retain value

Phase 0.5 must ensure the persistence layer supports these pillars — especially that **user data is private by default** and that **analytics are PII-compliant** (already enforced via `IsPrivacyCompliant()` guard).

---

## UI Constitution Constraints

*From `WeUP Canon Phase0.docx` — these are non-negotiable regardless of backend changes*

Phase 0.5 backend work must not introduce frontend changes that violate:

- **Map-first**: World layer (persistent map) must remain always-visible
- **No-scroll**: No scrolling anywhere in the app
- **One focus target**: Only one modal/focus layer at a time
- **No hover cards**: No intermediate preview layers
- **Modal-first navigation**: No page-stack navigation
- **Spatial continuity**: Every interaction must preserve orientation
- **Temporal invariants**: Time scrubber must immediately affect visible state

Any new API responses added in Phase 0.5 (e.g., Promoter Profile feed) must be consumed by components that respect these invariants.

---

## Success Criteria / Exit Conditions for Phase 0.5

Phase 0.5 is complete when ALL of the following are true:

### Infrastructure
- [ ] PostgreSQL running and accepting connections in staging
- [ ] EF Core migration applied cleanly (0 errors)
- [ ] All in-memory stubs swapped for EF Core implementations
- [ ] Data survives server restart
- [ ] JWT auth issuing and validating tokens correctly
- [ ] Media upload reaching cloud storage (Cloudinary or S3)
- [ ] Analytics events appearing in Segment/PostHog dashboard
- [ ] OpenTelemetry traces visible in observability backend

### API
- [ ] `GET /api/events/map` returns real events from DB
- [ ] `POST /api/users/me/saves/{eventId}` persists across restarts
- [ ] `POST /api/auth/login` returns a real signed JWT
- [ ] `POST /api/media/flyers` stores file in cloud, returns CDN URL
- [ ] `POST /api/promoters` creates a real profile
- [ ] `GET /api/promoters/{slug}` returns profile with feed
- [ ] All 48 backend tests still passing
- [ ] All 43 frontend tests still passing

### Security
- [ ] HTTPS enforced in staging/production
- [ ] Auth required on all protected endpoints
- [ ] Rate limiting active on auth and upload endpoints
- [ ] CORS restricted to production domain
- [ ] Upload MIME-type validation rejecting invalid files

### Frontend
- [ ] Map loads with real events from DB
- [ ] Save/unsave persists across page reload
- [ ] Auth flow works with JWT (login, persist session, logout)
- [ ] Promoter profile page renders correctly
- [ ] No UI constitution violations introduced

### Documentation
- [ ] `docs/CONTRACT_VALIDATION.md` updated with Promoter Profile contracts
- [ ] `docs/phase0-diagnostic-report.md` updated with Phase 0.5 completion
- [ ] `CHANGELOG.md` created with v1.0+ entries
- [ ] API reference updated with new Promoter endpoints
- [ ] Environment variable documentation updated

---

## Prompt Bundle Plan for Phase 0.5

The following prompt bundles map Phase 0.5 work to the existing bundle methodology:

| Bundle | Focus | Prompts |
|--------|-------|---------|
| Bundle 9a | EF Core Migration + Repository Swap | P26–P28 |
| Bundle 9b | JWT Auth + Token Refresh | P29–P30 |
| Bundle 9c | Cloud Media Storage (Cloudinary) | P31 |
| Bundle 9d | Analytics Sink + OTLP Export | P32–P33 |
| Bundle 9e | Promoter Profile Domain + API | P34–P38 |
| Bundle 9f | Promoter Feed + Media Uploads | P39–P41 |
| Bundle 9g | Security Hardening | P42–P43 |
| Bundle 9h | Integration Tests + E2E Validation | P44 |

---

## Files Modified by Phase 0.5

**Backend (to be modified):**
- `backend/WeUP.Api/Program.cs` — uncomment EF Core, JWT, OTLP, rate limiting
- `backend/WeUP.Api/appsettings.json` — add JWT, Cloudinary, analytics, OTLP config
- `backend/WeUP.Infrastructure/WeUP.Infrastructure.csproj` — add EF Design, Cloudinary, JWT packages
- `backend/WeUP.Api/WeUP.Api.csproj` — add JWT package

**Backend (new files):**
- `backend/WeUP.Infrastructure/Persistence/Migrations/` — EF migrations
- `backend/WeUP.Infrastructure/Auth/EfUserProfileRepository.cs`
- `backend/WeUP.Infrastructure/Auth/EfUserPreferencesRepository.cs`
- `backend/WeUP.Infrastructure/Auth/EfItineraryRepository.cs`
- `backend/WeUP.Infrastructure/Auth/JwtTokenService.cs`
- `backend/WeUP.Infrastructure/Moderation/EfModerationQueueRepository.cs`
- `backend/WeUP.Infrastructure/Moderation/EfAuditTrailRepository.cs`
- `backend/WeUP.Infrastructure/Submissions/EfSubmissionRepository.cs`
- `backend/WeUP.Infrastructure/Ingestion/EfIngestionJobRepository.cs`
- `backend/WeUP.Infrastructure/Media/CloudinaryFlyerUploadService.cs`
- `backend/WeUP.Infrastructure/Analytics/SegmentAnalyticsService.cs`
- `backend/WeUP.Domain/Promoters/PromoterProfile.cs`
- `backend/WeUP.Domain/Promoters/FeedPost.cs`
- `backend/WeUP.Domain/Promoters/MediaAsset.cs`
- `backend/WeUP.Api/Endpoints/PromoterEndpoints.cs`
- `backend/WeUP.Api/Endpoints/PromoterFeedEndpoints.cs`

**Frontend (new files):**
- `services/promoterService.ts`
- `components/PromoterProfile.tsx`
- `components/PromoterFeed.tsx`
- `components/MediaGallery.tsx`

---

**Document Version:** 1.0
**Created:** 2026-04-04
**Source Authority:** WeUP mnt documentation corpus + Phase 0 codebase analysis
**Next Review:** On Phase 0.5 kickoff
