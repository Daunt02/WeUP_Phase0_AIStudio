# Ingestion Boundary

## Purpose

P10 establishes a backend-first ingestion boundary that accepts heterogeneous source inputs and converts them into pre-domain canonical event candidates. The ingestion boundary is intentionally upstream of the canonical event aggregate so that manual submissions, pasted links, venue pages, and future external feeds all enter the same deterministic normalization pipeline without leaking raw payloads into UI models or bypassing provenance.

## Boundary shape

The ingestion contract layer lives in `backend/WeUP.Contracts/Ingestion` and separates four concerns:

1. Raw source input
   `IngestionRequestEnvelope` captures the source kind, source reference, submitter identity, raw payload JSON, idempotency key, received timestamp, and request metadata.

2. Normalized candidate output
   `CanonicalEventCandidate` is the pre-domain candidate shape. It is intentionally not the persisted canonical event aggregate. It carries extracted fields, source ref, confidence scores, and evidence references so downstream steps can reason about the candidate before promotion.

3. Evidence and issues
   `CanonicalSourceEvidence` preserves provenance for raw payloads, fetched documents, venue references, and extracted metadata. `CanonicalIngestionIssue` captures deterministic warnings and errors with retryability and field context.

4. Adapter execution metadata
   `AdapterCapabilityDescriptor` and `AdapterExecutionMetadata` describe which adapter ran, what it supports, whether it is retry-safe, and how long execution took.

## Adapter model

The adapter seam is split into three backend roles:

1. `IEventSourceAdapter`
   Each adapter owns a single source shape, validates inbound envelopes, and produces canonical candidates, evidence, issues, and execution metadata.

2. `IEventSourceAdapterResolver`
   The resolver maps `IngestionSourceKind` to the correct adapter so the coordinator does not become a source-specific god service.

3. `IIngestionCoordinator`
   The coordinator builds request envelopes, persists the ingestion job, drives lifecycle transitions, invokes validation and normalization, and returns the authoritative `IngestionResult` snapshot for the job.

## Source kinds

P10 implements these source kinds:

1. `ManualSubmission`
   Direct user-entered payloads produce the highest-trust deterministic candidate and preserve submitter attribution evidence.

2. `PastedUrl`
   Pasted event links fetch deterministic HTML content and extract Open Graph, title-tag, and JSON-LD metadata where present.

3. `VenuePage`
   Venue page ingestion records venue-page provenance and produces a reviewable candidate seed. It does not pretend to solve multi-event page expansion in P10.

4. `ExternalFeed`
   P10 defines this seam in the contracts so future external feed adapters can join the same coordinator and job repository without changing the ingestion boundary.

## Job lifecycle

Every ingestion job is traceable and auditable. The coordinator persists status transitions in this order when applicable:

1. `RECEIVED`
   The backend accepted the request envelope and assigned a job ID.

2. `VALIDATING`
   The selected adapter validates payload shape and deterministic field requirements.

3. `NORMALIZING`
   The adapter fetches or transforms the source input into canonical pre-domain fields.

4. `CANDIDATE_CREATED`
   A canonical candidate snapshot was produced and stored with evidence references.

5. `REQUIRES_REVIEW`
   The candidate is ready for downstream review and later aggregate promotion.

6. `FAILED`
   The job failed deterministically with non-retryable issues.

7. `RETRYABLE_FAILURE`
   The job failed in a way that remains safe to retry, such as remote fetch failure.

Each stored `IngestionResult` includes the request envelope, lifecycle timestamps, evidence, issues, and the latest candidate snapshot.

## Persistence model

`IIngestionJobRepository` is the storage seam for ingestion jobs. P10 supports both:

1. In-memory storage for development, tests, and seeded Phase 0 resets.
2. EF-backed storage using `ingestion_jobs`, `ingestion_candidates`, `ingestion_evidence`, and `ingestion_audit` tables when the PostgreSQL path is enabled.

There is no hidden state. A submitted ingestion request is always represented by a job record that can be queried through `GET /api/ingestion/jobs/{id}`.

## API surface

P10 exposes these backend endpoints:

1. `POST /api/ingestion/manual`
2. `POST /api/ingestion/url`
3. `POST /api/ingestion/venue-page`
4. `GET /api/ingestion/jobs/{id}`

The `POST` endpoints return an accepted response with a real job ID and status URL. The job endpoint returns the full `IngestionResult` snapshot so downstream services and operators can inspect lifecycle, candidate, evidence, and issues.

## Connection to P11 and P12

P10 deliberately stops at deterministic canonical candidate creation.

1. P11 flyer OCR plugs in as another upstream extraction path that should produce the same `CanonicalEventCandidate`, `CanonicalSourceEvidence`, and `CanonicalIngestionIssue` shapes instead of inventing a separate event model.

2. P12 deduplication should consume the P10 candidate output and evidence references after `CANDIDATE_CREATED` or `REQUIRES_REVIEW`. Dedup logic should never bypass the ingestion boundary or mutate raw adapter payloads.

This keeps OCR, dedupe, and later moderation UI work layered on top of a single auditable ingestion contract instead of fragmenting ingestion behavior across the frontend and backend.
