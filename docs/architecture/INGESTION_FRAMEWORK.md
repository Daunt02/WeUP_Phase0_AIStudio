# WeUP Ingestion Adapter Framework

## Overview
The Ingestion Adapter Framework provides a deterministic boundary for bringing heterogeneous event data into the WeUP ecosystem. It separates raw source inputs from the canonical event domain, ensuring that data is validated, normalized, and audited before it ever touches the primary event aggregate.

## Core Components

### 1. Ingestion Boundary
All data enters through the `IngestionCoordinator`. This service manages the lifecycle of an ingestion request, ensuring that every submission is tracked as an `IngestionJob`.

### 2. Adapter Model
The framework uses an adapter pattern (`IEventSourceAdapter`). Each adapter is responsible for a specific `IngestionSourceKind`:
- **ManualAdapter**: Handles direct user input.
- **UrlAdapter**: Handles pasted event links (e.g., Eventbrite, Facebook Events).
- **VenuePageAdapter**: Handles crawling specific venue websites.

### 3. Job Lifecycle
Every request moves through a deterministic set of states:
1. `RECEIVED`: Request has been accepted and assigned a Job ID.
2. `VALIDATING`: The resolved adapter is checking the inbound payload for basic structural integrity.
3. `NORMALIZING`: The adapter is extracting data and mapping it to a `CanonicalEventCandidate`.
4. `CANDIDATE_CREATED`: A valid candidate has been produced and is ready for the next stage (e.g., deduplication or moderation).
5. `REQUIRES_REVIEW`: The candidate was produced but has low confidence or significant issues.
6. `FAILED`: A terminal state where ingestion could not complete.

### 4. Canonical Event Candidate
Adapters do not produce `NightlifeItem` entities. Instead, they produce `CanonicalEventCandidate` objects. This is a "pre-domain" shape that preserves the raw extraction result and associated evidence (provenance).

## Provenance & Evidence
The `CanonicalSourceEvidence` object tracks:
- `sourceUrl`: Where the data came from.
- `rawPayload`: The exact data received by the adapter.
- `confidenceScore`: How certain the adapter is about the extraction.
- `extractedBy`: Which adapter version performed the work.

## Connection to Future Phases
- **P11 (Flyer OCR)**: A new `FlyerOcrAdapter` will be added to the framework.
- **P12 (Deduplication)**: The `IngestionCoordinator` will pass `CANDIDATE_CREATED` jobs to the Deduplication Engine before promotion to the canonical domain.
- **P13 (Moderation)**: Jobs in `REQUIRES_REVIEW` state will be surfaced in the Moderation Queue UI.

## API Endpoints
- `POST /api/ingestion/manual`: Submit manual event data.
- `POST /api/ingestion/url`: Submit a URL for extraction.
- `POST /api/ingestion/venue-page`: Submit a venue page for crawling.
- `GET /api/ingestion/jobs/{id}`: Query the status and result of an ingestion job.
