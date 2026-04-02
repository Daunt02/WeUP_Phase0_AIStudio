# Bundle 6 — Identity and User Persistence

**Prompts:** P16, P17, P18
**Goal:** Move from ephemeral anonymous UI to persistent user behaviour.

---

## P16 — Auth, Sessions, and Basic Profile Backbone

### What was built
- `WeUP.Contracts.Auth` — RegisterRequest, LoginRequest, UpdateProfileRequest, AuthResponse, UserProfileDto
- `IUserProfileRepository` (Domain) — email lookup, create, update
- `ITokenService` (Domain) — issue / validate opaque bearer tokens
- `InMemoryUserRepository` (Infrastructure) — ConcurrentDictionary-based thread-safe store
- `BearerTokenService` (Infrastructure) — 64-char hex tokens, 24 h TTL, in-memory expiry
- `UserAuthService` (Application) — register (idempotent), login, getProfile, updateProfile
- `AuthEndpoints` (API) — `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `PATCH /auth/profile`

### Auth flow (Phase 0)
```
POST /auth/register  { email, displayName?, homeMarket? }
  → { userId, token, tokenType: "Bearer", expiresInSeconds: 86400, profile }

POST /auth/login  { email }
  → same shape (email must already be registered)

GET /auth/me
  Authorization: Bearer <token>
  → UserProfileDto

PATCH /auth/profile
  Authorization: Bearer <token>
  → UserProfileDto
```

### Upgrade path
Replace `BearerTokenService` with `JwtTokenService` using `Microsoft.AspNetCore.Authentication.JwtBearer`.
Set `WeUp:Auth:JwtSecret` and `WeUp:Auth:JwtIssuer` in `appsettings.json`.
Uncomment `app.UseAuthentication()` / `app.UseAuthorization()` in `Program.cs`.

---

## P17 — Persist Saved Signals, Itinerary State, and User Preferences

### What was built
- `WeUP.Contracts.Users.ItineraryContracts` — AddToItineraryRequest, UpdateItineraryItemRequest, ItineraryResponse
- `WeUP.Contracts.Users.PreferencesContracts` — UserPreferencesDto, UpdatePreferencesRequest
- `IItineraryRepository` + `IUserPreferencesRepository` (Domain)
- `InMemoryItineraryRepository` — position-based ordering, shift-on-insert
- `InMemoryPreferencesRepository` — safe defaults (8 km radius, notifications off)
- `ItineraryEndpoints` — `GET/POST /api/users/me/itinerary`, `PATCH/DELETE /itinerary/{itemId}`, `GET/PATCH /preferences`
- `SaveEndpoints` — upgraded from anonymous stub to real `BearerTokenService` auth

### Itinerary API
```
GET  /api/users/me/itinerary         → ItineraryResponse
POST /api/users/me/itinerary         { eventId, note?, position? }
PATCH /api/users/me/itinerary/{id}   { note?, position? }
DELETE /api/users/me/itinerary/{id}

GET   /api/users/me/preferences
PATCH /api/users/me/preferences      { preferredCategories?, homeRadiusMeters?, notifyOnNewEvents?, ... }
```

---

## P18 — Event Submission Workflow with Draft, Review, Published States

### State machine
```
DRAFT ──────────────► SUBMITTED_FOR_REVIEW ──► APPROVED
   ▲                          │
   │                          ├──► REJECTED
   └── CHANGES_REQUESTED ◄───┘
```

### What was built
- `WeUP.Contracts.Events.SubmissionContracts` — `SubmissionStatus` enum, `DraftSubmissionRequest`, `SubmissionDto`, `SubmitForReviewResponse`
- `IEventSubmissionService` (Domain) — full lifecycle interface
- `InMemorySubmissionRepository` (Infrastructure) — owner-scoped access, field-merge patch, pre-submit validation (title, venue, address, startUtc, category required)
- `SubmissionEndpoints` (API):
  - `POST /api/events/submissions` — create draft
  - `GET /api/events/submissions` — list user's submissions
  - `GET /api/events/submissions/{id}` — get (owner-only)
  - `PATCH /api/events/submissions/{id}` — update draft or changes-requested
  - `POST /api/events/submissions/{id}/submit` — validate and submit for review
  - `GET /api/events/submissions/{id}/status` — public status check

### Integration with moderation
Submitted events route into the `ModerationQueue` (P13–P15). The moderator's `approve` / `reject` / `request-changes` actions call back to `ApplyReviewFeedbackAsync`, updating submission status.

---

## Running locally

```bash
cd backend
dotnet run --project WeUP.Api
```

Swagger UI: `http://localhost:5000/swagger`

1. `POST /auth/register` with your email → copy the token
2. Add `Authorization: Bearer <token>` to all `/api/users/me/*` calls
3. `POST /api/events/submissions` to create a draft
4. `POST /api/events/submissions/{id}/submit` to push to review queue
5. `POST /moderation/queue/{id}/approve` to approve (P15)
