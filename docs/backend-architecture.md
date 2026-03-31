# WeUP Phase 0 — Backend Architecture

## Stack
- .NET 8 / ASP.NET Core Minimal APIs
- PostgreSQL + EF Core (wired in P08)
- OpenTelemetry (wired in P22)

## Solution structure

```
backend/
  WeUP.sln
  WeUP.Api/           — HTTP endpoints, Program.cs, middleware
  WeUP.Contracts/     — Request/response DTOs shared with frontend
  WeUP.Domain/        — Repository interfaces, domain types
  WeUP.Infrastructure/— EF Core entities, migrations, stub impls
```

## Dependency flow

```
WeUP.Api → WeUP.Domain → WeUP.Contracts
WeUP.Api → WeUP.Infrastructure → WeUP.Domain
```

Infrastructure never imports from Api. Api never imports EF entities.

## Endpoint surface (Phase 0)

| Method | Path | Description |
|---|---|---|
| POST | /api/events/map | Map viewport feed |
| POST | /api/events/calendar | Calendar date-window feed |
| GET | /api/events/{id} | Event detail |
| POST | /api/events/submissions | Create event submission |
| GET | /api/users/me/saves | List saved events |
| POST | /api/users/me/saves/{eventId} | Save event (idempotent) |
| DELETE | /api/users/me/saves/{eventId} | Unsave event (idempotent) |
| GET | /health | Health check |

## Frontend integration

Replace `eventService.fetchEventsInBounds()` with calls to `POST /api/events/map`.
Replace local save state with calls to `/api/users/me/saves`.
See `services/eventService.ts` for the migration seam.

## Current state (P07)
Stub repositories return empty/correct-shape responses. EF Core implementation added in P08–P09.

## Auth seam
`ResolveUserId()` in endpoints reads `sub` claim. Full auth backbone (sessions, tokens, profile) implemented in P16.
