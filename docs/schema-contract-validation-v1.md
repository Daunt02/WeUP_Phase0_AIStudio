# Schema Contract Validation v1.0

**Bundle**: M4-P19  
**Layer**: Cross-stack — Backend (C#/.NET 8) + Frontend (TypeScript/Next.js)  
**Status**: Implemented

---

## 1. Purpose

Prevent silent contract drift between:

- **EventAggregate** (C# domain record — canonical backend truth)
- **DTO surfaces** (C# records exported via `EventContracts.cs`)
- **TypeScript API contracts** (`domains/event/apiContracts.ts`)
- **Frontend projections** (`domains/event/projections.ts`)

All violations must be **developer-visible** during local development and **fail CI** before they reach staging.

---

## 2. Drift Issues Fixed in This Bundle

| #   | Surface                                  | Drift Type                                                                                                                                     | Fix                              |
| --- | ---------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| 1   | `EventDetailDto` TS interface            | Missing `version`, `lastChangeType`, `concurrencyToken` (all 3 present in C# record with defaults)                                             | Added 3 fields as required types |
| 2   | `EventModerationDto` TS interface        | Missing `lastChangeType`, `hasPendingReview`, `concurrencyToken`                                                                               | Added 3 fields as required types |
| 3   | `backend-contract-manifest.json`         | Manifest didn't list the 6 new fields — parity tests under-covered                                                                             | Updated both DTO field lists     |
| 4   | `dtoContractParity.test.ts` fixtures     | `sampleDetail` and `sampleModeration` missing the 6 fields — tests would not fail on actual API response                                       | Updated both fixtures            |
| 5   | `projections.ts` — `fromMapCardDto`      | `?? "other"` on `category`, `?? "PUBLISHED"` on `status` masked missing/null fields from API                                                   | Removed both fallbacks           |
| 6   | `projections.ts` — `fromCalendarCardDto` | Same two fallbacks                                                                                                                             | Removed                          |
| 7   | `projections.ts` — `fromDetailDto`       | Same two fallbacks                                                                                                                             | Removed                          |
| 8   | `EventAggregate.Validate()`              | Missing: `Address.City`, `Address.Country`, `SourceEventIds` not null, `Provenance.PrimarySourceKind/Ref/EvidenceRefs`, `StartUtc` not default | Added all 7 checks               |

---

## 3. Schema Rules by Surface

### 3.1 EventAggregate (domain record)

Enforced by: `EventAggregate.Validate()` (throwing, first-failure) and `EventAggregateSchemaValidator.ValidateAggregate()` (non-throwing, full list).

| Field                          | Rule                          | Code                      |
| ------------------------------ | ----------------------------- | ------------------------- |
| `CanonicalEventId`             | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `SourceEventIds`               | Not null                      | `REQUIRED_NOT_NULL`       |
| `Title`                        | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `VenueName`                    | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Category`                     | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Tags`                         | Not null                      | `REQUIRED_NOT_NULL`       |
| `TimeZone`                     | Required non-empty (IANA)     | `REQUIRED_FIELD_EMPTY`    |
| `StartUtc`                     | Not `DateTimeOffset.MinValue` | `INVALID_STARTUTC`        |
| `EndUtc`                       | If set, ≥ StartUtc            | `END_BEFORE_START`        |
| `Address`                      | Not null                      | `REQUIRED`                |
| `Address.AddressLine1`         | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Address.City`                 | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Address.Country`              | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Address.RawAddress`           | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Latitude`                     | ∈ [-90, 90]                   | `INVALID_LATITUDE`        |
| `Longitude`                    | ∈ [-180, 180]                 | `INVALID_LONGITUDE`       |
| `(0, 0)` coordinates           | Warned as null-island         | `NULL_ISLAND_COORDINATES` |
| `ConfidenceScore`              | ∈ [0, 1]                      | `INVALID_CONFIDENCE`      |
| `Version`                      | ≥ 1                           | `INVALID_VERSION`         |
| `ConcurrencyToken` (effective) | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Provenance`                   | Not null                      | `REQUIRED`                |
| `Provenance.PrimarySourceKind` | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Provenance.PrimarySourceRef`  | Required non-empty            | `REQUIRED_FIELD_EMPTY`    |
| `Provenance.EvidenceRefs`      | Not null                      | `REQUIRED_NOT_NULL`       |
| `MergeLineage`                 | Not null                      | `REQUIRED_NOT_NULL`       |

### 3.2 EventMapCardDto

Enforced by: `EventAggregateSchemaValidator.ValidateMapCardDto()` and `validateMapCardDto()` (TS).

| Field        | Rule                                                                              | Code                      |
| ------------ | --------------------------------------------------------------------------------- | ------------------------- |
| `Id`         | Required non-empty                                                                | `REQUIRED_FIELD_EMPTY`    |
| `Title`      | Required non-empty                                                                | `REQUIRED_FIELD_EMPTY`    |
| `VenueName`  | Required non-empty                                                                | `REQUIRED_FIELD_EMPTY`    |
| `Category`   | Required non-empty                                                                | `REQUIRED_FIELD_EMPTY`    |
| `Lat`        | ∈ [-90, 90]                                                                       | `INVALID_LATITUDE`        |
| `Lng`        | ∈ [-180, 180]                                                                     | `INVALID_LONGITUDE`       |
| `(0, 0)`     | Warned as null-island                                                             | `NULL_ISLAND_COORDINATES` |
| `Status`     | One of `DRAFT\|NEEDS_REVIEW\|APPROVED\|PUBLISHED\|REJECTED\|ARCHIVED` (uppercase) | `INVALID_ENUM_VALUE`      |
| `Confidence` | ∈ [0, 1]                                                                          | `INVALID_CONFIDENCE`      |

### 3.3 EventCalendarCardDto

All MapCard rules, plus:

| Field             | Rule               | Code                   |
| ----------------- | ------------------ | ---------------------- |
| `StartUtc`        | Not default/zero   | `INVALID_STARTUTC`     |
| `EndUtc` (if set) | ≥ StartUtc         | `END_BEFORE_START`     |
| `Timezone`        | Required non-empty | `REQUIRED_FIELD_EMPTY` |

### 3.4 EventDetailDto

All CalendarCard rules, plus:

| Field                          | Rule                              | Code                   |
| ------------------------------ | --------------------------------- | ---------------------- |
| `Address`                      | Required non-empty display string | `REQUIRED_FIELD_EMPTY` |
| `MediaRefs`                    | Not null (empty array allowed)    | `REQUIRED_NOT_NULL`    |
| `Tags`                         | Not null (empty array allowed)    | `REQUIRED_NOT_NULL`    |
| `SourceKind`                   | Required non-empty                | `REQUIRED_FIELD_EMPTY` |
| `Version`                      | ≥ 1                               | `INVALID_VERSION`      |
| `LastChangeType` (if non-null) | One of valid change types         | `INVALID_ENUM_VALUE`   |

Valid change types: `MinorMetadataUpdate`, `MaterialEventChange`, `StatusTransition`, `MergeLineageUpdate`

### 3.5 EventModerationDto

| Field                                        | Rule                                                                                 | Code                         |
| -------------------------------------------- | ------------------------------------------------------------------------------------ | ---------------------------- |
| `CanonicalEventId`                           | Required non-empty                                                                   | `REQUIRED_FIELD_EMPTY`       |
| `Title`, `Category`, `VenueName`, `Timezone` | Required non-empty                                                                   | `REQUIRED_FIELD_EMPTY`       |
| `StartUtc`                                   | Not default                                                                          | `INVALID_STARTUTC`           |
| `EndUtc` (if set)                            | ≥ StartUtc                                                                           | `END_BEFORE_START`           |
| `LifecycleStatus`                            | One of PascalCase lifecycle values                                                   | `INVALID_ENUM_VALUE`         |
| `ModerationStatus`                           | One of `Unreviewed\|InReview\|Approved\|Rejected`                                    | `INVALID_ENUM_VALUE`         |
| `PublishStatus`                              | One of `NotEligible\|EligibilityPending\|Eligible\|Published\|Unpublished\|Archived` | `INVALID_ENUM_VALUE`         |
| `RiskLevel`                                  | One of `Low\|Medium\|High\|Restricted`                                               | `INVALID_ENUM_VALUE`         |
| `ConfidenceScore`                            | ∈ [0, 1]                                                                             | `INVALID_CONFIDENCE`         |
| `Version`                                    | ≥ 1                                                                                  | `INVALID_VERSION`            |
| `UpdatedAtUtc`                               | Not default                                                                          | `INVALID_UPDATEDATUTC`       |
| `LastChangeType` (if non-null)               | One of valid change types                                                            | `INVALID_ENUM_VALUE`         |
| `HasPendingReview`                           | Boolean                                                                              | `EXPECTED_BOOLEAN` (TS only) |

---

## 4. Status Serialization Contract

| Surface                                                                       | Format                    | Examples                              |
| ----------------------------------------------------------------------------- | ------------------------- | ------------------------------------- |
| Frontend status (`EventMapCardDto`, `EventCalendarCardDto`, `EventDetailDto`) | UPPERCASE_SCREAMING       | `"PUBLISHED"`, `"NEEDS_REVIEW"`       |
| Operational status (`EventModerationDto.LifecycleStatus`)                     | PascalCase — C# enum name | `"Published"`, `"Candidate"`          |
| Moderation status                                                             | PascalCase                | `"InReview"`, `"Approved"`            |
| Publish status                                                                | PascalCase                | `"EligibilityPending"`, `"Published"` |
| Risk level                                                                    | PascalCase                | `"Low"`, `"Restricted"`               |

**CRITICAL**: Frontend-facing DTOs use uppercase screaming-snake; operational/moderation DTOs use PascalCase matching C# enum names. These are **different** and must not be confused.

---

## 5. Enforcement Architecture

```
API response (JSON)
       │
       ▼
domains/event/schemaValidators.ts
  validateMapCardDto / validateDetailDto / etc.
       │
       ├── returns SchemaValidationResult { valid, violations[] }
       │
       └── used by:
           ├── assertValid*() wrappers (throw SchemaValidationError in tests)
           ├── API service layer (log + metric on violation)
           └── validate:schema CI script

Backend (C#)
       │
       ├── EventAggregate.Validate()           ← throwing, first-failure
       └── EventAggregateSchemaValidator.*     ← non-throwing, full violation list
              │
              └── used by:
                  ├── WeUP.Tests EventAggregateSchemaValidatorTests
                  └── moderation / publish eligibility service validation hooks
```

---

## 6. How to Run Validators

### Frontend (TypeScript)

```bash
# Run only schema validator tests (fast, no network)
npm run validate:schema

# Run all frontend tests including schema validators
npm run test:frontend
```

### Backend (C#)

```bash
# Run schema validator tests and DTO mapping tests together
dotnet test backend/WeUP.Tests/WeUP.Tests.csproj \
  --filter "EventAggregateSchemaValidatorTests|EventDtoMappingTests" \
  --configuration Release

# Run all backend tests
npm run test:backend
```

### Full CI gate (includes schema validation)

```bash
npm run release:check
```

---

## 7. Contract Manifest

The file `contracts/backend-contract-manifest.json` is the single-source-of-truth for all DTO field lists (camelCase). It drives `__tests__/unit/dtoContractParity.test.ts` at CI time.

**When a C# DTO record changes (field added/removed/renamed):**

1. Update the C# record in `WeUP.Contracts/Events/EventContracts.cs`
2. Update `contracts/backend-contract-manifest.json` (same field, camelCase)
3. Update the TypeScript interface in `domains/event/apiContracts.ts`
4. Update affected test fixtures in `__tests__/unit/dtoContractParity.test.ts`
5. Update `schemaValidators.ts` if the new field needs a validation rule
6. Update this document

---

## 8. Violation Rule Codes Reference

| Code                      | Meaning                                                   |
| ------------------------- | --------------------------------------------------------- |
| `REQUIRED_FIELD_EMPTY`    | String field is null, undefined, or whitespace-only       |
| `REQUIRED_NOT_NULL`       | Collection or object field is null                        |
| `REQUIRED`                | Object-level field is completely null                     |
| `INVALID_LATITUDE`        | Latitude outside [-90, 90]                                |
| `INVALID_LONGITUDE`       | Longitude outside [-180, 180]                             |
| `NULL_ISLAND_COORDINATES` | (Lat=0, Lng=0) — unresolved event location                |
| `INVALID_CONFIDENCE`      | Confidence/score outside [0, 1]                           |
| `INVALID_ENUM_VALUE`      | String field does not match the allowed set               |
| `INVALID_STARTUTC`        | StartUtc is DateTimeOffset.MinValue or invalid ISO string |
| `END_BEFORE_START`        | EndUtc is earlier than StartUtc                           |
| `INVALID_VERSION`         | Version integer < 1                                       |
| `INVALID_UPDATEDATUTC`    | UpdatedAtUtc is DateTimeOffset.MinValue                   |
| `EXPECTED_STRING`         | Field is not a string (TS only)                           |
| `EXPECTED_STRING_OR_NULL` | Field is not a string or null (TS only)                   |
| `EXPECTED_NUMBER`         | Field is not a finite number (TS only)                    |
| `EXPECTED_BOOLEAN`        | Field is not a boolean (TS only)                          |
| `EXPECTED_ARRAY`          | Field is not an array (TS only)                           |
| `EXPECTED_OBJECT`         | Field is not an object (TS only)                          |
| `INVALID_ISO_UTC`         | String is not a valid ISO 8601 datetime (TS only)         |
| `NOT_OBJECT`              | Root value is not an object (TS only)                     |
