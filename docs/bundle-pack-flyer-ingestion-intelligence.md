# WeUP Flyer Ingestion Intelligence Bundle Pack

**Version:** 1.0  
**Date:** 2026-04-04  
**Scope:** Phase 0.15+ — Flyer OCR, entity extraction, screenshot reconstruction, authority scoring, canonical event assembly  
**Classification:** Engineering Planning Artifact — PRD / FRD / TRD Source

---

## 1. Bundle Pack Title

**WeUP Flyer Intelligence Pack — FI-01 through FI-08**  
*From uploaded image to structured, scored, privacy-safe event intelligence*

---

## 2. Problem Statement

WeUP currently accepts flyer uploads as opaque media blobs. A flyer is stored, but its content is never read. This means:

- Event facts (date, time, venue, lineup) must be manually entered by the uploader — a friction point that kills supply
- Duplicate events are created because no system knows two flyers are the same event
- Screenshot-sourced content from Instagram is treated as a first-class flyer, exposing platform chrome and profile images as canonical event art
- Authority is unweighted — a spam re-upload is treated identically to a verified promoter's original
- Venue identity, artist identity, and promoter identity are never extracted or linked
- The city intelligence layer has no structured fuel to power ranking, recommendation, or discovery

This bundle pack defines the full pipeline that transforms a raw flyer image into a structured, scored, privacy-respecting, deduplicated event intelligence record.

---

## 3. Product Goals

1. Accept flyers from promoters, users, moderators, and approved source ingestion (not raw web scraping)
2. Extract all event-relevant facts via OCR and layout intelligence
3. Resolve venue, artist, promoter, and series identities to canonical entities
4. Geolocate events with confidence scoring and ambiguity handling
5. Detect and disassemble screenshot wrappers — extract the actual flyer region, suppress platform chrome and incidental profile images
6. Score flyer authority based on source, corroboration, and verification status
7. Reinforce event authority when multiple independently submitted flyers match the same event
8. Assemble or update canonical event drafts ready for moderation and publication
9. Preserve WeUP privacy principles: minimize retention, suppress incidental identities, support provenance without doxxing
10. Produce an explainable, auditable scoring record for every flyer and every event

---

## 4. Non-Goals

- **Not a general web scraper** — source ingestion is controlled and approved; not open-web crawling
- **Not a social graph builder** — Instagram handles are social identity references, never surveillance endpoints
- **Not a facial recognition system** — faces in flyer art are not identified; faces in screenshot wrappers are suppressed
- **Not a real-time stream processor** — pipeline is async; P99 latency target is minutes, not seconds
- **Not a duplicate user account detector** — artist/promoter deduplication is entity-based, not identity verification
- **Not a copyright enforcement engine** — provenance is preserved for moderation use, not automated DMCA

---

## 5. User and Source Types

| Source Type | Trust Tier | Baseline Authority | Notes |
|---|---|---|---|
| Verified Promoter (direct upload) | T1 | 0.90 | Highest baseline; subject to anti-abuse checks |
| Verified Venue (direct upload) | T1 | 0.88 | Second highest; venue is the canonical host |
| Unverified User (submission) | T3 | 0.35 | Corroboration required to elevate |
| Moderator/Admin (manual entry) | T1 | 0.95 | Treated as ground truth after review |
| Approved Source Ingestion | T2 | 0.55 | Approved aggregators, venue calendars; not open web |
| Re-upload of Existing Canonical | Penalty | -0.20 delta | Detected as duplicate; authority not doubled |

---

## 6. End-to-End Workflow

```
Flyer Submitted
     │
     ▼
[FI-01] Intake + Provenance Registration
     │ metadata capture, source tier, uploader identity, raw blob stored
     ▼
[FI-03] Visual Classification
     │ screenshot vs native flyer vs collage vs story capture
     │ detect and crop platform chrome, avatar/profile suppression
     ▼
[FI-02] OCR + Layout Intelligence
     │ text extraction, field hierarchy, date/time, venue, artist, handles
     ▼
[FI-04] Entity Resolution
     │ venue, DJ/artist, promoter, series matching
     │ fuzzy matching, alias handling, provenance-preserving merges
     ▼
[FI-05] Geolocation
     │ address parse → geocode → venue match → neighborhood tag
     ▼
[FI-06] Authority Scoring
     │ compute FlyerAuthorityScore + ViewScore
     │ apply screenshot penalty, source diversity bonus, corroboration boost
     ▼
[FI-07] Canonical Event Assembly
     │ create or update EventDraft
     │ attach flyer variants, provenance, entities, scores
     │ compute publish eligibility + moderation queue priority
     ▼
[FI-08] Privacy, Governance, Retention
     │ strip incidental identities, apply redaction policy
     │ provenance audit log, retention tier assignment
     ▼
Moderation Queue → Published Event
```

---

## 7. Pipeline Stages

### Stage 1: Intake
- Validate upload (file type, size, magic bytes — P26 already implements this)
- Register RawFlyerAsset with provenance (uploader, source tier, timestamp, hash)
- Store raw blob to object storage; record StorageRef
- Enqueue for visual classification

### Stage 2: Visual Classification (async worker)
- Run CV classifier: native_flyer / screenshot_with_wrapper / story_capture / collage / unknown
- If screenshot: detect platform chrome regions (Instagram nav bar, story ring, comment bar, profile header)
- Identify avatar/profile image regions — flag for suppression
- Crop canonical flyer region; store as derived asset
- Compute ScreenshotAnalysis record (detected_chrome_regions, avatar_regions, crop_bounds, confidence)

### Stage 3: OCR + Layout Intelligence (async worker)
- Run OCR on canonical flyer region (not the raw screenshot)
- Segment text into layout zones: headline, subheadline, body, footer, social_strip
- Extract structured fields: event_name, venue_name, address, date_raw, time_raw, artists, promoters, handles, ticket_url, price, age_restriction
- Assign per-field confidence scores
- Store OCRExtractionResult

### Stage 4: Entity Resolution
- For each extracted field, attempt entity match against canonical tables
- Venue: fuzzy name match + address proximity match → VenueCandidate
- Artist/DJ: fuzzy name + alias table + social handle linkage → ArtistCandidate
- Promoter: name + handle → PromoterCandidate
- Series: event name pattern match → SeriesCandidate
- Unresolved entities create provisional records pending moderation review

### Stage 5: Geolocation
- Parse address from OCR result
- Geocode against city-scoped geocoder (not global)
- Cross-reference against known venue entity locations
- Assign confidence: exact_match / venue_fallback / neighborhood_estimate / unresolved
- Tag city, neighborhood, district

### Stage 6: Authority Scoring
- Compute FlyerAuthorityScore (0.0–1.0) — see Section 12
- Compute ViewScore — see Section 12
- If duplicate flyer group detected, apply similarity reinforcement to group authority

### Stage 7: Canonical Event Assembly
- Determine if this flyer matches an existing EventDraft (via similarity group or entity match)
- If match: attach as FlyerVariant; update authority scores; do not create duplicate
- If new: create EventDraft with extracted entities, geocoded location, temporal facts, canonical flyer
- Compute publish eligibility (minimum: authority > 0.5, date resolved, venue resolved or neighborhood resolved)
- Assign moderation queue priority (higher authority → lower priority; low authority + user submission → higher review priority)

### Stage 8: Privacy and Governance
- Apply PrivacyRedactionRecord for any incidental identity detected in screenshot wrapper
- Do not retain raw screenshot wrapper bytes beyond moderation period
- Store only the cropped/canonical flyer region as the durable asset
- Provenance records retain source tier + uploader hash (not raw uploader PII unless required for dispute)
- Apply retention tier: canonical_flyer = permanent; raw_intake = 30 days post-publication; screenshot_wrapper = 7 days

---

## 8. Bundle Breakdown

### Bundle FI-01 — Ingestion Intake
**Purpose:** All upload paths enter a single, provenance-aware intake gate.

Deliverables:
- `POST /api/media/flyers/intake` — unified intake endpoint
- `POST /api/admin/media/ingest` — moderator/admin manual intake
- `POST /api/ingestion/source/{sourceId}/submit` — approved source adapter intake
- `RawFlyerAsset` record creation
- `ProvenanceRecord` creation at intake
- Source trust tier assignment
- Enqueue for visual classification worker

### Bundle FI-02 — OCR + Layout Intelligence
**Purpose:** Extract all event facts from the canonical flyer region.

Deliverables:
- Python OCR worker (Tesseract or cloud OCR with layout awareness)
- Layout zone segmenter
- Date/time normalizer (handles "SAT APR 12", "9PM – 3AM", "DOORS 8", etc.)
- Structured field extractor per OCRExtractionResult schema
- Per-field confidence assignment
- Writes to `ocr_extraction_results` table

### Bundle FI-03 — Visual Classification
**Purpose:** Classify the upload and disassemble screenshot wrappers.

Deliverables:
- CV classifier: native_flyer / screenshot / story / collage
- Instagram chrome detector (nav bar, story ring, like bar, profile header bounding boxes)
- Avatar/profile image region detector and suppressor
- Canonical flyer crop pipeline
- Derived asset registration (cropped flyer stored as new StorageRef)
- ScreenshotAnalysis record creation

### Bundle FI-04 — Entity Resolution
**Purpose:** Link extracted text to canonical domain entities.

Deliverables:
- Venue resolver (fuzzy + geo proximity)
- Artist/DJ resolver (alias table, handle linkage)
- Promoter resolver (name + handle)
- Series resolver (pattern match on event names)
- Provisional entity creation for unresolved candidates
- `EntityResolutionResult` record per flyer
- Merge queue for ambiguous candidates

### Bundle FI-05 — Geolocation
**Purpose:** Convert extracted address/venue text to geocoded location.

Deliverables:
- Address parser (handles informal venue addresses, cross-streets, building names)
- City-scoped geocoder integration
- Venue entity fallback (if geocoding fails, use known venue coords)
- Neighborhood/district tagger
- Confidence: exact / venue_fallback / neighborhood / unresolved
- `GeoLocationResult` record per flyer

### Bundle FI-06 — Authority Engine
**Purpose:** Score each flyer and reinforce event authority via corroboration.

Deliverables:
- `FlyerAuthorityScore` computation
- `ViewScore` computation
- Similarity group detection (perceptual hash or embedding similarity)
- Corroboration reinforcement when multiple flyers match same event
- Screenshot penalty application
- `AuthorityScoreBreakdown` record (explainable, per-signal)

### Bundle FI-07 — Canonical Event Assembly
**Purpose:** Create or update the structured EventDraft from pipeline outputs.

Deliverables:
- Duplicate/match detection (entity + similarity group)
- FlyerVariantGroup management
- EventDraft creation / update
- Canonical flyer selection logic
- Publish eligibility computation
- Moderation queue priority assignment
- `CanonicalEvent` record creation

### Bundle FI-08 — Privacy, Governance, and Retention
**Purpose:** Enforce WeUP privacy standards throughout the pipeline.

Deliverables:
- `PrivacyRedactionRecord` creation for incidental identities
- Retention tier assignment
- Screenshot wrapper purge schedule
- Provenance record anonymization policy
- User deletion / takedown support
- Pipeline audit log
- Data minimization review pass

---

## 9. Required Services

| Service | Language | Responsibility |
|---|---|---|
| `FlyerIntakeService` | .NET | Intake endpoint, provenance, blob storage, queue dispatch |
| `FlyerVisualClassifier` | Python (CV) | Screenshot detection, chrome crop, avatar suppression |
| `FlyerOcrWorker` | Python (OCR) | Text extraction, layout segmentation, field extraction |
| `EntityResolverService` | .NET / Python | Venue, artist, promoter, series resolution |
| `GeocoderService` | .NET (with geocoder API) | Address parse, geocode, neighborhood tag |
| `AuthorityScoringService` | .NET | FlyerAuthorityScore, ViewScore computation |
| `EventAssemblyService` | .NET | EventDraft creation/update, variant attachment |
| `PrivacyGovernanceService` | .NET | Redaction, retention, audit |
| `SimilarityIndexService` | Python | Perceptual hash / embedding similarity for dedup |
| `ModerationQueueService` | .NET | Queue management, priority assignment, review routing |

---

## 10. Data Contracts and Schemas

### RawFlyerAsset
```csharp
record RawFlyerAsset {
    string AssetId;
    string StorageKey;
    string ContentHash;
    long FileSizeBytes;
    string ContentType;
    DateTimeOffset UploadedAt;
    string SubmitterId;
    SourceTier SourceTier;         // T1/T2/T3
    string? SourceAdapterId;       // null for direct uploads
    FlyerAssetStatus Status;       // from P26
    string? ProvenanceRecordId;
}
```

### ProcessedFlyerAsset
```csharp
record ProcessedFlyerAsset {
    string AssetId;                  // FK → RawFlyerAsset
    string CanonicalStorageKey;      // cropped/reconstructed flyer
    FlyerClassification Classification; // native_flyer / screenshot / etc.
    string? ScreenshotAnalysisId;
    string? OcrExtractionResultId;
    string? EntityResolutionResultId;
    string? GeoLocationResultId;
    float AuthorityScore;
    float ViewScore;
    string? AuthorityScoreBreakdownId;
    string? EventDraftId;
    FlyerProcessingStatus ProcessingStatus;
    DateTimeOffset ProcessedAt;
}
```

### ScreenshotAnalysis
```csharp
record ScreenshotAnalysis {
    string Id;
    string RawAssetId;
    FlyerClassification Classification;
    BoundingBox[] ChromeRegions;      // nav bars, story rings, etc.
    BoundingBox[] AvatarRegions;      // profile images to suppress
    BoundingBox? FlyerRegion;         // actual flyer within screenshot
    float ClassificationConfidence;
    bool CanonicalRegionExtracted;
    string? CroppedAssetStorageKey;
    DateTimeOffset AnalyzedAt;
}
```

### OCRExtractionResult
```csharp
record OCRExtractionResult {
    string Id;
    string ProcessedAssetId;
    string? EventName;              float EventNameConfidence;
    string? VenueNameRaw;           float VenueNameConfidence;
    string? AddressRaw;             float AddressConfidence;
    string? DateRaw;                float DateConfidence;
    DateTimeOffset? ParsedDateStart;
    DateTimeOffset? ParsedDateEnd;
    string? TimeRaw;
    string[]? ArtistsRaw;           float[] ArtistConfidences;
    string[]? PromotersRaw;
    string[]? SocialHandles;
    string? TicketUrl;
    string? PriceRaw;
    string? AgeRestriction;
    string[] UnclassifiedTextBlocks;
    float OverallOcrConfidence;
    DateTimeOffset ExtractedAt;
}
```

### ExtractedFact
```csharp
record ExtractedFact {
    string Id;
    string SourceAssetId;
    string FieldName;
    string RawValue;
    string? NormalizedValue;
    float Confidence;
    string? LinkedEntityId;
    string? LinkedEntityType;
    DateTimeOffset ExtractedAt;
}
```

### VenueCandidate / ArtistCandidate / PromoterCandidate
```csharp
record EntityCandidate {
    string Id;
    string RawName;
    string? ResolvedEntityId;
    float MatchConfidence;
    EntityMatchMethod Method;   // exact / fuzzy / geo_proximity / handle_linkage / provisional
    bool IsProvisional;
    DateTimeOffset CreatedAt;
}
```

### EventDraft
```csharp
record EventDraft {
    string Id;
    string? CanonicalEventId;
    string Title;
    string? VenueEntityId;
    string? VenueNameFallback;
    double? Latitude;
    double? Longitude;
    string? NeighborhoodId;
    DateTimeOffset? StartUtc;
    DateTimeOffset? EndUtc;
    string? TimezoneId;
    string[]? ArtistEntityIds;
    string[]? PromoterEntityIds;
    string? SeriesEntityId;
    string? CanonicalFlyerAssetId;
    string[] FlyerVariantIds;
    float AuthorityScore;
    float ViewScore;
    EventDraftStatus Status;    // extracted / review_pending / approved / rejected
    float PublishEligibilityScore;
    int ModerationQueuePriority;
    DateTimeOffset CreatedAt;
    DateTimeOffset UpdatedAt;
}
```

### FlyerVariantGroup
```csharp
record FlyerVariantGroup {
    string Id;
    string EventDraftId;
    string[] MemberAssetIds;
    string CanonicalAssetId;
    float GroupAuthorityScore;
    int SubmissionCount;
    int UniqueSubmitterCount;
    DateTimeOffset LastSubmissionAt;
}
```

### ProvenanceRecord
```csharp
record ProvenanceRecord {
    string Id;
    string AssetId;
    SourceTier SourceTier;
    string SubmitterHash;          // hashed submitter ID, not raw PII
    string? SourceAdapterId;
    bool IsPromoterVerified;
    bool IsVenueVerified;
    DateTimeOffset RecordedAt;
}
```

### AuthorityScoreBreakdown
```csharp
record AuthorityScoreBreakdown {
    string Id;
    string AssetId;
    float BaseSourceScore;
    float PromoterVerificationBonus;
    float VenueVerificationBonus;
    float CorroborationBonus;      // from FlyerVariantGroup.UniqueSubmitterCount
    float OcrConfidenceContrib;
    float EntityResolutionContrib;
    float GeoConfidenceContrib;
    float ScreenshotPenalty;
    float SpamPenalty;
    float ModeratorBoost;
    float FinalScore;
    DateTimeOffset ComputedAt;
}
```

### ViewScoreBreakdown
```csharp
record ViewScoreBreakdown {
    string Id;
    string EventDraftId;
    float AuthorityContrib;
    float CompletenessContrib;     // how many fields resolved
    float FreshnessContrib;        // time to event start
    float GeoRelevanceContrib;     // distance decay from user/city center
    float CulturalRelevanceContrib; // category/tag match
    float MediaQualityContrib;     // resolution, layout quality
    float ModerationStatusContrib;
    float FinalViewScore;
    DateTimeOffset ComputedAt;
}
```

### PrivacyRedactionRecord
```csharp
record PrivacyRedactionRecord {
    string Id;
    string AssetId;
    RedactionReason[] Reasons;     // avatar_region / platform_chrome / screenshot_wrapper
    BoundingBox[] RedactedRegions;
    bool WrapperPurged;
    DateTimeOffset RedactedAt;
    DateTimeOffset? WrapperPurgeScheduledAt;
}
```

### ModerationDecision
```csharp
record ModerationDecision {
    string Id;
    string EventDraftId;
    string ModeratorId;
    ModerationOutcome Outcome;     // approved / rejected / needs_edit / escalated
    string? RejectionReason;
    DateTimeOffset DecidedAt;
}
```

---

## 11. Entity Model

### Canonical Entities (resolved by FI-04)

```
VenueEntity
├── id, canonical_name, aliases[]
├── address, latitude, longitude
├── neighborhood_id, city_id
├── verified: bool
├── social_handles: { instagram, website }
└── created_at, updated_at

ArtistEntity
├── id, canonical_name, aliases[]
├── genres[], collective_id (optional)
├── social_handles: { instagram, soundcloud, ra, bandcamp }
├── verified: bool
└── created_at, updated_at

PromoterEntity
├── id, canonical_name, aliases[]
├── verified: bool, verification_tier
├── social_handles
└── linked_venue_ids (optional)

SeriesEntity
├── id, canonical_name
├── recurrence_pattern (weekly_friday / monthly_last_saturday / irregular)
├── linked_promoter_id, linked_venue_id (optional)
└── created_at

NeighborhoodEntity
├── id, name, city_id
├── polygon (GeoJSON)
└── tags: { cultural_district, nightlife_dense, etc. }
```

---

## 12. Authority and View Score Model

### Flyer Authority Score (0.0–1.0)

```
FlyerAuthorityScore = clamp(
    BaseSourceScore
  + PromoterVerificationBonus * 0.10
  + VenueVerificationBonus    * 0.08
  + CorroborationBonus        * min(UniqueSubmitters - 1, 5) * 0.04
  + OcrConfidence             * 0.10
  + EntityResolutionConfidence* 0.08
  + GeoConfidence             * 0.06
  - ScreenshotPenalty         * 0.20
  - SpamPenalty               * 0.40
  + ModeratorBoost            * 0.15,
  min=0.0, max=1.0
)
```

**BaseSourceScore by tier:**
- T1 Verified Promoter: 0.90
- T1 Verified Venue: 0.88
- T1 Moderator/Admin: 0.95
- T2 Approved Source: 0.55
- T3 User Submission: 0.35

**CorroborationBonus:** Each additional *unique* submitter who independently submits a visually matching flyer adds +0.04 (capped at 5 unique submitters = +0.20 max). Re-uploads from the same submitter do not count.

**ScreenshotPenalty:** Applied if `FlyerClassification == screenshot_with_wrapper`. Can be reduced to 0.10 if canonical region was successfully extracted.

**SpamPenalty:** Applied if submitter has spam flags, if content hash matches known banned asset, or if submission velocity is anomalous.

### View Score (0.0–1.0)

```
ViewScore = weighted_sum(
    authority_score        * 0.30,
    completeness_score     * 0.20,   // % of key fields resolved
    freshness_score        * 0.15,   // time-decay toward event start
    geo_relevance          * 0.15,   // user/city distance decay
    cultural_relevance     * 0.10,   // tag/category match
    media_quality_score    * 0.05,
    moderation_status_mult * 1.00    // binary gate: 0 if rejected/pending, 1 if approved
)
```

Both scores are persisted with full breakdowns for auditability. Neither score is a black box.

---

## 13. Image/Screenshot Reconstruction Logic

### Classification Priority
1. Detect image dimensions and aspect ratio: story captures are typically 9:16
2. Run color histogram analysis: Instagram gradients and UI color signatures
3. Run object detection: nav bar, bottom tab bar, story ring, heart/comment/share button strip
4. If any UI chrome detected → classified as `screenshot_with_wrapper`

### Chrome Region Detection
Target regions to detect and mask:
- Top status bar (battery, time, carrier)
- Instagram navigation header (back arrow, username, follow button)
- Story ring (colored circle around avatar)
- Profile image / avatar (top-left or centered)
- Caption text block below image
- Like/comment/share action strip
- Bottom tab bar

### Canonical Flyer Extraction
1. Remove detected chrome bounding boxes from the image space
2. Identify the largest remaining contiguous image region
3. If region occupies >50% of original dimensions → crop to that region
4. Store as `canonical_flyer` derived asset
5. Mark original raw asset for scheduled purge (7 days)

### Avatar Suppression Rules
- If a detected avatar/profile image region is entirely within the platform chrome → suppress and do not use as event thumbnail
- If a face/image appears within the extracted canonical flyer region → retain as part of flyer art (promoter or artist appearance on the flyer is intentional)
- Do not run facial recognition. Only suppress profile-image-shaped regions in known chrome positions.

### Failure Modes
- Cannot extract canonical region (too much chrome, no usable flyer area): mark as `screenshot_unrecoverable`, flag for manual review, do not auto-publish
- Very low resolution: mark as `low_quality`, allow into pipeline but downgrade media_quality_score

---

## 14. Deduplication and Similarity Logic

### Levels of Deduplication

**Level 1 — Exact Hash Match**  
SHA-256 hash match = exact byte-for-byte duplicate. Link to existing asset, do not store new blob. Increment FlyerVariantGroup.SubmissionCount.

**Level 2 — Perceptual Hash Match**  
pHash distance < threshold (e.g. Hamming distance < 12) = visually similar flyer. Candidate for same FlyerVariantGroup. Requires entity confirmation before merging.

**Level 3 — Entity + Temporal Match**  
Same venue entity + same event date + overlapping artist set = likely same event. Create FlyerVariantGroup even if visual similarity is low (text-only flyers for same event).

**Level 4 — Semantic Similarity (stretch)**  
Embedding similarity of extracted event facts using vector search. Catches variants with different design but identical content.

### Corroboration vs. Spam
- Corroboration: unique submitters from different source tiers submitting visually equivalent flyers → authority boost
- Spam: same submitter re-uploading the same or near-identical flyer within a short window → flag, no authority boost

### Canonical Flyer Selection
Priority order within a FlyerVariantGroup:
1. Moderator-approved canonical selection (manual override)
2. T1 Verified Promoter upload with highest resolution
3. T1 Verified Venue upload
4. Native flyer (not a screenshot) with highest pHash quality score
5. Best-cropped screenshot with highest visual quality

---

## 15. Privacy and Trust Rules

### Data Minimization
- Raw screenshot wrapper bytes: retained max 7 days post-processing; purged unless under active moderation dispute
- OCR output: retain event facts; purge raw text that contains irrelevant personal conversation fragments
- Provenance: retain source tier + submission timestamp + hashed submitter reference; do not retain raw PII in provenance unless required for dispute resolution
- Extracted social handles: store as normalized `@handle` references; do not cross-reference against external identity graphs

### Incidental Identity Suppression
- Faces visible only in avatar/profile regions of screenshot chrome: suppress and do not retain as event media
- Usernames appearing in screenshot captions or comment bars: strip from OCR output; do not store in event record
- "Posted by @username" content from re-posts: strip; record only that the source was a re-post, not the originating username

### Provenance Without Doxxing
- Internal provenance records use hashed submitter IDs, not raw user IDs
- Source adapter provenance identifies the adapter, not the originating account on the source platform
- Moderators can access full provenance chain for dispute resolution; it is not exposed publicly

### Retention Tiers
| Asset Type | Retention |
|---|---|
| Canonical flyer (approved) | Permanent |
| Canonical flyer (rejected) | 30 days post-rejection |
| Raw intake blob (screenshots) | 7 days post-processing |
| Raw intake blob (native flyers) | 30 days post-publication |
| OCR extraction result | Permanent (event facts) |
| Screenshot analysis record | Permanent metadata; blob purged |
| Provenance record | Permanent (hashed reference) |
| PrivacyRedactionRecord | Permanent |

### Source Access Policy
- T3 users cannot see other submissions for the same event
- T2 approved sources are not notified of how their content was used
- T1 promoters can see their own submission status but not competing submissions
- Moderators have full pipeline visibility

---

## 16. Moderation and Review Flows

### Queue Priority Formula
```
ModerationPriority = (1.0 - AuthorityScore) * 10 + SourceTierPenalty + FlagsWeight
```
Higher number = reviewed first. Low-authority user submissions float to top.

### Auto-Approve Threshold
EventDrafts with `AuthorityScore >= 0.85` AND `source_tier = T1` AND no spam flags may be auto-approved pending a spot-check sample rate (configurable, default 10% manual review).

### Rejection Reasons (standardized)
`duplicate | no_event_found | no_valid_date | venue_unverifiable | screenshot_unrecoverable | spam | policy_violation | low_quality`

### Appeals
Promoters may appeal a rejected submission. Appeals create a high-priority moderation ticket with the original submission and rejection reason.

### Escalation
Submissions involving potential policy violations (unauthorized use, NSFW content, doxxing) are escalated to a senior moderation queue bypassing normal priority ranking.

---

## 17. Edge Cases and Failure Modes

| Case | Handling |
|---|---|
| Flyer with no explicit address | Use venue entity coordinates if resolved; otherwise neighborhood estimate |
| Venue nickname (e.g. "The Lot" vs "The Lot Studios") | Alias table in VenueEntity; fuzzy match with threshold |
| DJ with alias (e.g. "DJ Premiere" vs "DJ Premier") | Alias table in ArtistEntity; require confirmation before merge |
| Multiple venues on one flyer | Create two VenueCandidate records; flag for manual review |
| Recurring event (e.g. weekly Friday night) | Match SeriesEntity; create per-occurrence EventDraft linked to series |
| Multi-day event | DateTimeOffset[] for start/end; flag as multi-day |
| Afterparty on same flyer | Create separate EventDraft with parent_event_id linkage |
| Festival with lineup | Create FestivalEventDraft; ArtistEntity[] sorted by billing tier |
| Blurry / low-light screenshot | OCR confidence < 0.30 → flag as `low_ocr_quality`; route to manual entry |
| Partial flyer crop (edge cut off) | OCR proceeds; confidence reduced; missing fields flagged |
| Story screenshot (9:16 ratio, ephemeral) | Story capture classification; crop aggressively; purge wrapper immediately |
| Reposted flyer with stickers over content | Sticker regions detected as non-flyer overlay; OCR attempts underlying text |
| Fake/manipulated flyer | Authority score low if unverifiable; moderation review required |
| Conflicting dates on same flyer | Both parsed; lowest-confidence date marked provisional |
| Duplicate events with slightly different times | Entity + date match detects near-duplicate; create variant group with flag |
| Face as intentional flyer art vs avatar screenshot | Face within canonical flyer region = retained; face only in chrome position = suppressed |

---

## 18. Suggested Repo / Folder Structure

```
backend/
  WeUP.Domain/
    Media/
      Flyer/                    ← P26 (validation, lifecycle)
      Ingestion/
        RawFlyerAsset.cs
        ProcessedFlyerAsset.cs
        ScreenshotAnalysis.cs
        OCRExtractionResult.cs
        ExtractedFact.cs
    Events/
      EventDraft.cs
      CanonicalEvent.cs
      FlyerVariantGroup.cs
    Entities/
      VenueEntity.cs
      ArtistEntity.cs
      PromoterEntity.cs
      SeriesEntity.cs
    Scoring/
      AuthorityScoreBreakdown.cs
      ViewScoreBreakdown.cs
    Privacy/
      PrivacyRedactionRecord.cs
      ProvenanceRecord.cs

  WeUP.Infrastructure/
    Media/
      LocalFlyerUploadService.cs  ← P25/P26
      InMemoryFlyerAssetStore.cs  ← P26
    Ingestion/
      FlyerIntakeService.cs
      SimilarityIndexService.cs
    Scoring/
      AuthorityScoringService.cs
      ViewScoringService.cs
    Privacy/
      PrivacyGovernanceService.cs

  WeUP.Api/
    Endpoints/
      MediaEndpoints.cs           ← P25/P26
      IngestionEndpoints.cs       ← FI-01
      EventDraftEndpoints.cs      ← FI-07
      ModerationEndpoints.cs      ← FI-08

workers/
  flyer_classifier/              ← Python; FI-03
    classifier.py
    chrome_detector.py
    avatar_suppressor.py
    crop_pipeline.py

  flyer_ocr/                     ← Python; FI-02
    ocr_worker.py
    layout_segmenter.py
    field_extractor.py
    date_normalizer.py

  similarity_index/              ← Python; FI-06
    phash_worker.py
    embedding_worker.py

docs/
  bundle-pack-flyer-ingestion-intelligence.md  ← this file
  media-intake.md                              ← P25 ops doc
```

---

## 19. APIs and Worker Contracts

### REST Endpoints

| Endpoint | Bundle | Description |
|---|---|---|
| `POST /api/media/flyers/intake` | FI-01 | Unified intake; returns RawFlyerAsset.Id |
| `GET /api/media/flyers/{assetId}` | P25/P26 | Asset metadata + lifecycle status |
| `GET /api/media/flyers/{assetId}/processing-status` | FI-02/03 | OCR + classification progress |
| `GET /api/events/drafts/{draftId}` | FI-07 | EventDraft with all attached entities |
| `POST /api/moderation/drafts/{draftId}/decision` | FI-08 | Moderator approve/reject |
| `GET /api/media/flyers/{assetId}/authority` | FI-06 | Full AuthorityScoreBreakdown |
| `GET /api/events/drafts/{draftId}/view-score` | FI-06 | Full ViewScoreBreakdown |

### Worker Queue Contracts

```
Queue: flyer.intake.classify
  Message: { assetId, storageKey, contentType, sourceTier }
  Consumer: FlyerVisualClassifier worker
  Output: ScreenshotAnalysis → writes to DB; enqueues flyer.ocr.process

Queue: flyer.ocr.process
  Message: { assetId, canonicalStorageKey }
  Consumer: FlyerOcrWorker
  Output: OCRExtractionResult → writes to DB; enqueues entity.resolve

Queue: entity.resolve
  Message: { ocrResultId, assetId }
  Consumer: EntityResolverService
  Output: EntityResolutionResult → writes to DB; enqueues event.assemble

Queue: event.assemble
  Message: { assetId, entityResultId, geoResultId, authorityScore }
  Consumer: EventAssemblyService
  Output: EventDraft created/updated; moderation queue entry created
```

---

## 20. Event Lifecycle State Machine

```
RawFlyerAsset.Status:
  Initialized → Uploaded → ProcessingPending → ProcessingComplete → ReviewPending → Approved / Rejected → Archived

EventDraft.Status:
  extracted → review_pending → approved → published
                            ↘ rejected → archived
                            ↘ needs_edit → review_pending (cycle)

FlyerVariantGroup:
  Seeded by first submission → growing (new variants added) → canonical_selected → locked
```

---

## 21. Observability and Audit Requirements

### Metrics (OpenTelemetry)
- `flyer.intake.received` — counter by source_tier
- `flyer.classification.duration_ms` — histogram
- `flyer.classification.result` — counter by classification type
- `flyer.ocr.confidence` — histogram
- `flyer.authority.score` — histogram
- `event.draft.created` / `event.draft.updated` — counters
- `moderation.queue.depth` — gauge
- `moderation.decision` — counter by outcome

### Audit Log Requirements
- Every lifecycle transition logged with timestamp + triggering service
- Every entity resolution logged with confidence + method
- Every authority score computed logged with full breakdown
- Every PrivacyRedactionRecord applied logged
- Every ModerationDecision logged with moderator ID (hashed for privacy)

### Alerting Thresholds
- OCR worker backlog > 500: page on-call
- Authority score mean < 0.40 over 1h rolling: investigate spam wave
- Screenshot classification rate > 80%: check source adapter health
- Duplicate detection rate > 50%: investigate event duplication campaign

---

## 22. Testing Strategy

### Unit Tests
- FlyerAssetLifecycle transition matrix (all legal + all illegal) — P26 already covers this
- OCR date normalizer: 50+ format variants
- Authority score formula: known input → known output
- Entity resolver: fuzzy match threshold boundaries
- Screenshot detector: synthetic chrome injection tests

### Integration Tests
- Full pipeline run on synthetic flyer corpus (100 test images with known ground truth)
- Duplicate detection: upload same flyer twice from two users; assert FlyerVariantGroup created
- Screenshot pipeline: known Instagram screenshot → assert wrapper purged, canonical flyer extracted
- Low OCR confidence → assert routed to manual review, not auto-assembled

### Contract Tests
- Each queue message format validated against schema
- Each REST endpoint validated against OpenAPI spec

### Privacy Tests
- Submit screenshot containing profile photo → assert avatar region in PrivacyRedactionRecord
- Submit flyer from user with spam flags → assert SpamPenalty applied in breakdown
- Verify wrapper purge scheduled within 7 days for screenshot assets

---

## 23. Rollout Plan

### Phase 0.15 MVP Scope (P25–P38 from original 030 pack)
- FI-01: Intake endpoint operational (uses P26 validation already implemented)
- FI-02: OCR worker MVP (Tesseract; date/venue/artist extraction only; no lineup parsing)
- FI-03: Screenshot detector MVP (Instagram chrome only; crop pipeline basic)
- FI-04: Venue resolver only (artist/promoter entity resolution is Phase 1)
- FI-05: Geocoder integration (city-scoped; venue fallback)
- FI-06: Authority score v1 (source tier + OCR confidence; no visual similarity yet)
- FI-07: EventDraft creation (basic; manual canonical flyer selection)
- FI-08: Retention rules enforced; screenshot wrapper purge scheduled

### Phase 1 Expansion (post-0.15)
- FI-04: Artist + promoter entity resolution
- FI-06: Visual similarity corroboration engine (perceptual hash)
- FI-06: ViewScore v2 with geo and cultural relevance signals
- FI-03: Non-Instagram screenshot detection (TikTok, Twitter/X, generic mobile)
- FI-07: Auto-approve threshold for T1 verified promoters
- FI-04: Series detection and recurring event linking

---

## 24. Example Input/Output Cases

### Case 1: Verified Promoter Uploads Native JPEG Flyer
**Input:** 2.1 MB JPEG; source_tier=T1; promoter_verified=true; no prior similar submissions  
**Output:**
- Classification: `native_flyer`; no chrome detected; canonical = original asset
- OCR: event_name=0.95, venue_name=0.91, date=0.93, artists=0.88
- Entity resolution: venue matched (exact), 2 of 3 artists resolved
- Geolocation: exact_match (0.98)
- Authority score: 0.93
- EventDraft created; moderation_priority=2 (high authority → low review urgency)

### Case 2: User Submits Instagram Story Screenshot
**Input:** 1.8 MB JPEG; 1080×1920; source_tier=T3; story ring detected  
**Output:**
- Classification: `screenshot_with_wrapper`; chrome: story_ring, profile_header, action_strip
- Avatar region: detected and flagged for suppression
- Canonical flyer: cropped to 1080×1400 region; stored as derived asset
- OCR on canonical region: date=0.72, venue_name=0.55, event_name=0.80
- Authority score: 0.41 (T3 base 0.35 + screenshot penalty -0.20 partially offset by OCR quality)
- PrivacyRedactionRecord created; wrapper scheduled for purge in 7 days
- EventDraft created but marked `review_required` (authority < 0.50)

### Case 3: Third Upload of Known Event
**Input:** JPEG; source_tier=T2; pHash distance=9 from existing FlyerVariantGroup  
**Output:**
- Duplicate detection: Level 2 (perceptual hash match)
- Added to existing FlyerVariantGroup; SubmissionCount=3, UniqueSubmitterCount=3
- Corroboration bonus applied: +0.08 to group authority
- EventDraft authority updated: 0.78 → 0.86
- Moderation priority downgraded (higher authority = less urgent review)

### Case 4: Repost Flyer With Sticker Covering Date
**Input:** JPEG; user submission; original flyer obscured in date area by emoji sticker  
**Output:**
- Classification: `native_flyer` (no platform chrome detected)
- OCR: date=0.12 (low confidence — obscured), venue_name=0.88, artists=0.82
- EventDraft created with `date_provisional=true`; moderation_flag=`date_unverifiable`
- Moderator prompted to manually confirm date before publish eligibility granted

---

## 25. Stretch Enhancements

1. **Lineup Tier Extraction** — detect headline vs. support vs. opener from font size hierarchy in flyer layout
2. **Recurring Event Series Detection** — NLP pattern matching on event names to detect "every Friday" / "monthly" series
3. **Multi-City Flyer Variants** — detect when same event is being promoted in multiple cities with location-swapped flyer variants
4. **Dynamic ViewScore Freshness Decay** — real-time score decay as event date approaches and passes
5. **Flyer Art Style Fingerprint** — detect promoter/designer style signatures as an additional corroboration signal
6. **Venue Disambiguation via Map** — when two venues have similar names in same city, present ambiguity to moderator with map for visual confirmation
7. **Anti-Manipulation Velocity Detection** — detect coordinated bulk submission campaigns (same hash pool submitted by rotating accounts)
8. **OCR Language Detection** — detect non-English flyers; route to language-aware OCR pipeline
9. **Afterparty and Multi-Stage Linking** — detect "and then" / "after" / "stage 2" language to link events into a multi-event chain
10. **Flyer QR Code Extraction** — decode QR codes from flyers to extract ticket URLs, RSVP links, and venue confirmation

---

## Implementation Order (Recommended)

1. **FI-01** — Intake first. All other bundles are worthless without a reliable intake gate.
2. **FI-03** — Visual classification second. OCR on a raw screenshot is noise; crop first.
3. **FI-02** — OCR only after clean canonical image is available.
4. **FI-05** — Geolocation before entity resolution; venue geocoding helps resolve venue ambiguity.
5. **FI-04** — Entity resolution. Venue first, then artist/promoter.
6. **FI-06** — Authority scoring once entity and geo data are available for scoring inputs.
7. **FI-07** — Event assembly last; requires all upstream outputs.
8. **FI-08** — Privacy and governance runs in parallel with FI-03/07 but retention enforcement gates publication.

---

## Top 10 Engineering Risks

1. **OCR quality on real-world nightlife flyers** — dark backgrounds, artistic fonts, overlapping elements, intentional illegibility. Tesseract will fail frequently. Cloud OCR (Google Vision / AWS Textract) is more reliable but costs money and adds a third-party dependency.
2. **Screenshot chrome detection generalization** — training on Instagram-specific chrome; TikTok, Twitter, WhatsApp, and generic mobile screenshots will break the detector until retrained.
3. **Entity resolution false merges** — fuzzy matching venue/artist names will merge unrelated entities if threshold is too permissive. Requires conservative defaults and merge review queue.
4. **Perceptual hash collisions** — different events with visually similar flyer styles (same designer template) may be incorrectly grouped. Requires entity confirmation before merging.
5. **Queue backlog under load** — OCR and CV workers are CPU-heavy. Spikes during weekend event upload rush will cause moderation lag. Worker autoscaling strategy required.
6. **Geocoder rate limits and cost** — city-scoped geocoding is cheaper but requires an up-to-date venue and address database. Geocoder API failures will leave events geoless.
7. **Privacy redaction accuracy** — avatar suppression based on bounding boxes is imprecise. Under-suppression leaks profile images; over-suppression corrupts flyer art.
8. **Schema versioning for ProcessedFlyerAsset** — pipeline outputs will evolve; EventDraft records from old pipeline versions must remain readable by new code.
9. **Python ↔ .NET worker coordination** — inter-process queue contracts must be strictly versioned; schema drift between services will cause silent data loss.
10. **Moderation queue throughput** — authority scoring reduces review volume but does not eliminate it. Manual review capacity must scale with city coverage expansion.

---

## Top 10 Product Risks

1. **Low OCR precision erodes user trust in auto-extracted events** — one wrong date or venue on a promoted event causes real-world harm. Publish threshold must be conservative.
2. **Promoter frustration with review delays** — T1 promoters expect near-immediate publication. Pipeline latency and moderation queues must be transparent.
3. **Screenshot disassembly fails on important events** — if a high-profile event is only submitted as a screenshot and the crop fails, that event may not appear or may appear incorrectly.
4. **Authority score gaming** — coordinated bulk submissions from fake accounts to artificially elevate authority. Anti-manipulation rules must be active before launch.
5. **Entity resolution creates wrong canonical event** — two different events on the same date at the same venue get merged into one. Needs explicit moderation confirm before merge.
6. **Privacy overreach perception** — if users feel their flyer submissions are being analyzed beyond event discovery, trust is damaged. Privacy policy and in-app disclosure required.
7. **City coverage gaps** — entity databases (venues, artists, promoters) are sparse in new markets. Pipeline degrades gracefully but first-city expansion needs data seeding.
8. **Recurring event spam** — bad actors could flood the system with recurring event variants to dominate discovery. Recurrence rules need abuse limits.
9. **Flyer art misattribution** — flyer art cropped from a screenshot may strip the artist's credit. Attribution must be preserved in the ProvenanceRecord.
10. **Moderation review bottleneck at launch** — launching in a city without sufficient moderator coverage will create a publication backlog that degrades the discovery experience.

---

## Phase 0 MVP vs Phase 1 Expansion

| Capability | Phase 0 MVP | Phase 1 Expansion |
|---|---|---|
| Intake | JPEG/PNG from users + T1 | All source types + video flyers |
| Screenshot detection | Instagram chrome only | All major platforms |
| OCR fields | Date, venue, event name, artists | Full lineup, ticket URL, age restriction, price |
| Entity resolution | Venue only | Artist, promoter, series |
| Geolocation | Address geocode + venue fallback | Neighborhood polygon + cultural district |
| Authority scoring | Source tier + OCR confidence | Full model with visual similarity corroboration |
| View score | Static at assembly | Real-time decay + geo relevance |
| Deduplication | Exact hash | Perceptual hash + entity + semantic embedding |
| Auto-approve | No | T1 verified promoter with threshold |
| Privacy enforcement | Screenshot wrapper purge | Full redaction pipeline |
| Moderation | Manual review all | Auto-approve T1; spot-check sampling |
| Observability | Basic metrics | Full audit trail + alerting |

---

**Document Version:** 1.0  
**Last Updated:** 2026-04-04  
**Status:** Planning Artifact — Ready for engineering breakdown into P27+ implementation prompts
