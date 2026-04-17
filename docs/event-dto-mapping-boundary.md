# Event DTO Mapping Boundary — M4-P17

## Overview

This document defines the strict anti-corruption boundary between the canonical
`EventAggregate` domain object and the API DTOs serialised to API consumers.
No internal domain field may reach a client without passing through the
authorised mapper.

---

## Field Visibility Tiers

| Tier          | Who can read it                      | Mapper method(s) that include it          |
| ------------- | ------------------------------------ | ----------------------------------------- |
| FRONTEND-SAFE | Any authenticated user               | `ToMapCard`, `ToCalendarCard`, `ToDetail` |
| OPERATIONAL   | Moderator / internal tooling         | `ToModeration`, `ToPublishEligibility`    |
| INTERNAL-ONLY | Never serialised to any API response | — (omitted by all mappers)                |

### INTERNAL-ONLY fields (must never appear in any DTO)

| Field                              | Reason                                          |
| ---------------------------------- | ----------------------------------------------- |
| `Provenance.EvidenceRefs`          | Raw ingestion evidence chain — operational only |
| `Provenance.SourceRefs`            | Internal source tracking identifiers            |
| `SourceEventIds`                   | Synthetic merge/dedup keys                      |
| `ExternalReferences`               | Raw third-party identifiers                     |
| `MergeLineage.AppliedMergePlanIds` | Internal dedup plan record                      |
| `Address.RawAddress`               | May contain PII / unformatted scrape output     |

---

## Serialisation Rules

| Concern           | Rule                                                                                    |
| ----------------- | --------------------------------------------------------------------------------------- |
| Timestamps        | ISO 8601 UTC (`DateTimeOffset.ToString("O")`)                                           |
| Lifecycle status  | Uppercase storage string via `EventLifecycleStatusMapper.ToStorage()`                   |
| Operational enums | PascalCase (C# default enum serialisation)                                              |
| Address           | `FormatAddress(address)` — joins `AddressLine1`, `City`, `State`; never `RawAddress`    |
| Merge lineage     | `null` when aggregate has no parent/children (omitted from FRONTEND-SAFE DTOs entirely) |

---

## Mapper Method Inventory

| Method                                                           | DTO returned                 | Endpoint                                   | Tier          |
| ---------------------------------------------------------------- | ---------------------------- | ------------------------------------------ | ------------- |
| `EventDtoMapper.ToMapCard(aggregate, thumbnail?)`                | `EventMapCardDto`            | `GET /api/events/map-feed`                 | FRONTEND-SAFE |
| `EventDtoMapper.ToCalendarCard(aggregate, thumbnail?)`           | `EventCalendarCardDto`       | `GET /api/events/calendar-feed` (new)      | FRONTEND-SAFE |
| `EventDtoMapper.ToDetail(aggregate, media[], primarySourceKind)` | `EventDetailDto`             | `GET /api/events/{id}`                     | FRONTEND-SAFE |
| `EventDtoMapper.ToModeration(aggregate)`                         | `EventModerationDto`         | `GET /api/moderation/events/{id}`          | OPERATIONAL   |
| `EventDtoMapper.ToPublishEligibility(aggregate, result)`         | `EventPublishEligibilityDto` | `GET /api/events/{id}/publish-eligibility` | OPERATIONAL   |

---

## New Endpoints (M4-P17)

### `GET /api/moderation/events/{id}`

Returns an `EventModerationDto` with lifecycle status, moderation status,
risk level, and reduced merge lineage summary. Only accessible to moderators
(`ModeratorAuthorizationFilter` is applied to the `/api/moderation` group).

### `GET /api/events/{id}/publish-eligibility`

Returns an `EventPublishEligibilityDto` constructed from the canonical aggregate
and the current `IPublishEligibilityService` evaluation. Includes confidence
gate decision, field completeness result, and any hard/soft blockers.

> **Phase 1 note**: this endpoint should be gated behind moderator auth before
> it is exposed in production.

---

## Dual-Manifest Sync Procedure

The backend contract manifest exists in two locations:

| File                                               | Consumer                                                                      |
| -------------------------------------------------- | ----------------------------------------------------------------------------- |
| `contracts/backend-contract-manifest.json`         | Frontend TypeScript parity tests (`__tests__/unit/dtoContractParity.test.ts`) |
| `backend/contracts/backend-contract-manifest.json` | C# manifest test (`WeUP.Tests/Integration/ContractManifestTests.cs`)          |

**Both files must be updated atomically whenever a DTO changes.**

Quick sync command (PowerShell):

```powershell
Copy-Item contracts/backend-contract-manifest.json backend/contracts/backend-contract-manifest.json
```

---

## Anti-Corruption Pattern Enforcement

1. **`EfEventRepository`** — the three projection helpers (`ToMapCard`,
   `ToCalendar`, `ToDetail`) must not copy `EventAggregate` fields directly
   to DTOs without going through the approved formatting helpers. As of M4-P17:
   - `ToDetail` uses inline address-safe formatting that omits `RawAddress`.
   - `ToMapCard` accepts the thumbnail from `PrimaryThumbnail(entity)` directly.
     _Full routing through `EventDtoMapper` is blocked by the existing
     `WeUP.Application → WeUP.Infrastructure` project cycle; inline equivalents
     must be kept in sync with the mapper._

2. **`StubEventRepository`** — serves development; returns aggregates via
   `GetAggregateAsync`. Callers (endpoints) must pass them through the mapper.

3. **`EventEndpoints` / `ModerationEndpoints`** — all new endpoints must invoke
   `EventDtoMapper.*` before returning; never serialise the raw `EventAggregate`.

4. **Frontend** — must consume `apiContracts.ts` interfaces only; never import
   domain types. Projection adapters in `domains/event/projections.ts` convert
   DTOs to UI view models.

---

## Test Coverage

| Test file                                         | Count | What it covers                                                                                           |
| ------------------------------------------------- | ----- | -------------------------------------------------------------------------------------------------------- |
| `WeUP.Tests/Events/EventDtoMappingTests.cs`       | 29    | All 5 mapper methods, internal field exclusion, lifecycle serialisation, address formatting, null guards |
| `WeUP.Tests/Integration/ContractManifestTests.cs` | 1     | Manifest JSON keys match actual C# DTO property names                                                    |
| `__tests__/unit/dtoContractParity.test.ts`        | 24    | TypeScript interface ↔ manifest alignment, projection adapter shapes                                     |
