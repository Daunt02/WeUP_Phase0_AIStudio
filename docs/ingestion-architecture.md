**WeUP Phase 0 — Ingestion Adapter Architecture**

## Overview

This document describes the Phase 0 ingestion adapter framework implemented in the backend. The goal is to accept multiple source kinds (manual submissions, pasted links, venue pages) and convert them into durable ingestion jobs that produce normalized event candidates for review and downstream processing.

Key interfaces (location)

- `WeUP.Domain.Ingestion.IIngestionAdapter` — adapter contract returning `NormalizedEventCandidate`.
- `WeUP.Domain.Ingestion.IIngestionDispatcher` — orchestration entrypoint used by HTTP endpoints.
- `WeUP.Domain.Ingestion.IIngestionJobRepository` — durable job store (Phase 0: in-memory; replaceable with EF/Npgsql later).
- `WeUP.Domain.Ingestion.IExtractionNormalizer` — seam for normalization (LLM or heuristic).
- `WeUP.Domain.Ingestion.IIngestionAuditWriter` — audit/evidence writer.

Implemented components

- `WeUP.Application.Ingestion.IngestionDispatcher` — routes requests to adapters, updates job status, and writes audit entries.
- `WeUP.Infrastructure.Ingestion.InMemoryIngestionJobRepository` — Phase 0 in-memory job store.
- `WeUP.Infrastructure.Ingestion.ConsoleIngestionAuditWriter` — simple console audit writer.
- `WeUP.Infrastructure.Ingestion.Adapters.ManualSubmissionAdapter` — accepts manual submission requests.
- `WeUP.Infrastructure.Ingestion.Adapters.LinkAdapter` — fetches pasted URLs, extracts basic OG/JSON-LD hints.
- `WeUP.Infrastructure.Ingestion.Adapters.VenuePageAdapter` — fetches venue pages and seeds candidate data.
- `WeUP.Contracts.Ingestion.IngestionContracts.cs` — request/response/contract types and `NormalizedEventCandidate`.

Ingestion job lifecycle

- `Queued` — job created and awaiting work.
- `Fetching` — adapter fetching external content (HTTP, file, feed).
- `Extracting` — extraction/parsing of raw content into raw payloads / evidence.
- `Normalizing` — conversion of raw extraction payloads into `NormalizedEventCandidate` (LLM/heuristics).
- `ReviewPending` — candidate ready for human or automated review; no automatic publication in Phase 0.
- `Completed` — candidate accepted and persisted as canonical event (future work).
- `Failed` — pipeline error; failure reason persisted and surfaced to operators.

Durability, provenance & evidence

- Phase 0 uses `InMemoryIngestionJobRepository` for durability. This is intentionally pluggable; replace with `EfIngestionJobRepository` (EF Core + Postgres) when ready.
- All adapters include `EvidenceRefs` in `NormalizedEventCandidate` (URL fetch references, fetch errors, venue IDs). Evidence is preserved and persisted alongside job records in the future persistent repo.
- `IIngestionAuditWriter` records a time-ordered trail of pipeline stages for each `jobId`. Phase 0 writes to console; production should write to a persisted audit table tied to `jobId`.

Extension seams

- New adapters implement `IIngestionAdapter` and are registered in DI. `IngestionDispatcher` accepts `IEnumerable<IIngestionAdapter>` and routes by `SourceKind` — no dispatcher changes required for new adapters.
- Add normalization strategies by implementing `IExtractionNormalizer` or creating new `IIngestionAdapter` variants that call LLMs, OCR, or vendor APIs.
- Add downstream pipelines (dedupe, OCR, event publish) by subscribing to the `ReviewPending` state and implementing `IPublishEligibilityService` and `IDedupService`.

Persistence & audit decisions

- Phase 0 keeps jobs in-memory to simplify local dev and CI. Job model is simple and includes `JobId`, `Status`, `CandidateEventId`, and `FailureReason`.
- Production should replace `InMemoryIngestionJobRepository` with an EF-based `EfIngestionJobRepository` that stores:
  - ingestion_jobs (jobId PK, sourceKind, sourceRef, status, createdAt, updatedAt, failureReason)
  - ingestion_evidence (evidenceId PK, jobId FK, kind, reference, payload/blob)
  - ingestion_candidates (candidateId PK, jobId FK, normalized JSON payload)
  - ingestion_audit (auditId PK, jobId FK, stage, detail, timestamp)
- Persisting evidence and normalized candidate JSON preserves provenance for later review, dedupe, and appeals.

API surface (already implemented)

- POST `/api/ingestion/manual` — body: `ManualIngestionRequest` → returns 202 Accepted with `jobId` location.
- POST `/api/ingestion/link` — body: `LinkIngestionRequest` → validates absolute URL and starts job.
- POST `/api/ingestion/venue-page` — body: `VenuePageIngestionRequest` → starts venue page scrape job.
- GET `/api/ingestion/jobs/{id}` — fetch job status and basic job response (`IngestionJobResponse`).

Frontend integration (how to call)
Examples (fetch API):

1. Manual submission

```http
POST /api/ingestion/manual
Content-Type: application/json

{
  "title": "Indie Night",
  "venueName": "The Green Room",
  "address": "42 Example St, City",
  "startDate": "2026-05-01T20:00:00Z",
  "endDate": null,
  "timezone": "UTC",
  "category": "music",
  "description": "A small indie showcase",
  "tags": ["indie","night"],
  "submitterId": "user:123"
}
```

2. Pasted link

```http
POST /api/ingestion/link
Content-Type: application/json

{ "url": "https://examplevenue.com/event/12345", "submitterId": "user:123" }
```

3. Venue page

```http
POST /api/ingestion/venue-page
Content-Type: application/json

{ "venueId": "venue:abc", "pageUrl": "https://examplevenue.com/calendar", "requestedBy": "user:123" }
```

4. Poll job status

```http
GET /api/ingestion/jobs/{jobId}
```

Integration guidance for frontend

- Replace any simulation in `AddEventModal` with calls above. The modal should POST the user input (or pasted URL) to the correct endpoint and poll the job GET until `ReviewPending` or `Failed`.
- The server returns a `jobId` location header in 202 Accepted; store it and poll `/api/ingestion/jobs/{jobId}`.
- Do not allow the frontend to fabricate `NormalizedEventCandidate`. All derived fields must come from the backend pipeline.

Next steps (Phase 0 → Phase 1)

- Replace `InMemoryIngestionJobRepository` with `EfIngestionJobRepository` and add migrations.
- Implement `IExtractionNormalizer` backed by an LLM service to extract times, addresses, and structured fields.
- Wire OCR outputs as an adapter input (flyer ingestion pipeline already scaffolded under `Flyer` services).
- Add dedupe and publish eligibility services to transition `ReviewPending` → `Completed` automatically when rules allow.

## Contact

If you want, I can: commit an EF-backed job repository, add an audit DB schema, or update the frontend `AddEventModal` to call these endpoints. Which should I do next?
