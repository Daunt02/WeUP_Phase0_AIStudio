# WeUP Repair Doctrine

**Created:** 2026-04-04
**Scope:** Phase 0 and onwards — runtime issue patterns, root causes, and fixes

This document records every diagnosed runtime failure, its root cause, and the fix applied. It serves as a reference for preventing recurrence and onboarding new engineers.

---

## Incident 001 — Backend 500 on Every Request (Duplicate Endpoint Name)

**Date:** 2026-04-04
**Severity:** P0 — backend completely broken, all requests return 500

### Symptom
Every API call returns a 500 with:
```
System.InvalidOperationException: Duplicate endpoint name 'GetMapFeed' found on
'HTTP: POST /api/spatial/map-feed => GetMapFeed' and 'HTTP: POST /api/events/map'.
Endpoint names must be globally unique.
```

### Root Cause
Two endpoint files both used `.WithName("GetMapFeed")`:
- `SpatialEndpoints.cs` line 22: `group.MapPost("/map-feed", GetMapFeed).WithName("GetMapFeed")`
- `EventEndpoints.cs` line 25: `group.MapPost("/map", ...).WithName("GetMapFeed")`

ASP.NET Core requires globally unique endpoint names across the entire application. The router fails to initialize when duplicates exist, causing every request to crash before reaching any handler.

### Fix
Renamed `EventEndpoints.cs` `.WithName("GetMapFeed")` → `.WithName("GetEventMapFeed")`.

### Prevention Rule
**Every `.WithName()` call must be unique across the entire solution.** Use a prefix convention matching the endpoint group:
- Spatial endpoints: `Spatial_*` (e.g., `Spatial_GetMapFeed`)
- Event endpoints: `Event_*` (e.g., `Event_GetMapFeed`)
- Analytics endpoints: `Analytics_*`

When adding a new endpoint, search the solution for the name string before using it.

---

## Incident 002 — Map Not Full Screen (Height Collapse)

**Date:** 2026-04-04
**Severity:** P1 — core UI non-functional, map renders as 0px height

### Symptom
Map renders as a thin strip or is invisible. Only the TopBar and BottomNav are visible. The page background is white (no map tiles).

### Root Cause
`WorldCoordinator.tsx` root container used `className="w-full h-full relative"`. `RadarMap` uses `absolute inset-0` which requires the parent to have an explicit height.

`h-full` in CSS resolves to 100% of the *parent's* height. The chain was:
- `<html>` — no height set → height = content
- `<body>` — no height set → height = content
- `WorldCoordinator` `div` → `h-full` = 100% of body = content height (collapses)
- `RadarMap` `absolute inset-0` → anchors to parent, but parent has 0 height → invisible

### Fix
Two changes:
1. `globals.css`: Added `html, body { height: 100%; }` so the percentage chain resolves to the viewport.
2. `WorldCoordinator.tsx`: Changed root div from `h-full` to `h-screen` (100vh) as the authoritative height source.

### Prevention Rule
**Never rely on `h-full` at the top of the component tree** unless `html` and `body` are explicitly sized. Use `h-screen` (100vh) for the outermost full-viewport container.

For any new full-screen surface (modals, overlays, panels): verify that at least one ancestor in the chain has an explicit pixel or viewport height before using `absolute inset-0`.

---

## Incident 003 — Frontend API Calls Never Reach Backend (Missing Proxy)

**Date:** 2026-04-04
**Severity:** P1 — all frontend-to-backend calls return 404 or Next.js route errors

### Symptom
Frontend service calls (`temporalService`, `analyticsService`, `eventService`) fail with errors like:
- `Temporal query failed: Not Found`
- `404` on `/api/temporal/events-at-time`

### Root Cause
The frontend services call `/api/*` (relative URL). In Next.js, relative `/api/*` calls go to the **Next.js API routes** layer, not the .NET backend. There were no Next.js API routes defined, so all calls returned 404.

`next.config.ts` had no `rewrites()` configured. The frontend `.env.local` has `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000` but the services use relative paths — not the env var.

### Fix
Added `async rewrites()` to `next.config.ts`:
```ts
async rewrites() {
  return [
    {
      source: '/api/:path*',
      destination: 'http://localhost:5000/api/:path*',
    },
  ];
},
```

This proxies all `/api/*` requests from the Next.js dev server to the .NET backend at port 5000 — no CORS issues, no absolute URLs needed in service files.

### Prevention Rule
**Always configure Next.js rewrites when the frontend calls a separate backend.** Do not rely on `NEXT_PUBLIC_API_BASE_URL` in service files without verifying the proxy is in place.

For production: set the rewrite `destination` via environment variable (e.g., `process.env.BACKEND_URL ?? 'http://localhost:5000'`) so staging/production backends can be targeted without code changes.

---

## Incident 004 — Frontend Running on Wrong Port (Port Collision)

**Date:** 2026-04-04
**Severity:** P2 — confusing, user sees blank page on expected URL

### Symptom
User navigates to `http://localhost:3000` and sees a stale or blank page. Frontend is actually running on `http://localhost:3001`.

### Root Cause
Two `npm run dev` processes were started sequentially. The first (still running) held port 3000. The second silently moved to port 3001.

### Fix
Kill existing frontend process before restarting:
```bash
cmd /c "taskkill /F /PID <PID>"
# Then restart:
npm run dev -- --port 3000
```

### Prevention Rule
**Always kill existing processes before restarting dev servers.** Add a `restart-dev` script to `package.json` that kills then relaunches:
```json
"restart-dev": "npx kill-port 3000 && npm run dev"
```

Or use `--port 3000` flag explicitly — Next.js will error if the port is taken rather than silently moving, when the port is explicitly specified.

---

## General Diagnostic Checklist

Run this checklist when the app is not working as expected:

### Backend
```bash
# 1. Is it running?
curl http://localhost:5000/health

# 2. Does any route work?
curl http://localhost:5000/api/temporal/presets

# 3. Any startup errors?
# Check dotnet run output for InvalidOperationException
```

### Frontend
```bash
# 1. Which port is it actually on?
# Check terminal output: "Local: http://localhost:XXXX"

# 2. Does it proxy to backend?
curl http://localhost:3000/api/health

# 3. Environment variables loaded?
# Check .env.local exists and has NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN
```

### Common Failures by Symptom

| Symptom | Check First |
|---------|-------------|
| White page, no map | `html, body { height: 100% }` and `h-screen` on root div |
| 500 on every request | Duplicate `.WithName()` in endpoint files |
| 404 on `/api/*` calls | `next.config.ts` rewrites configured? |
| Wrong port | Another dev server still running on 3000 |
| Map tiles don't load | `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` set in `.env.local`? |
| Temporal error on load | Backend running? Proxy configured? |
| Tests fail after adding endpoint | Check for duplicate `.WithName()` values |

---

## Fix Registry

| ID | File | Change | Reason |
|----|------|--------|--------|
| 001 | `backend/WeUP.Api/Endpoints/EventEndpoints.cs:25` | `.WithName("GetMapFeed")` → `.WithName("GetEventMapFeed")` | Duplicate endpoint name crashed router |
| 002a | `app/globals.css` | Added `html, body { height: 100%; }` | Enables `h-full` chain to resolve to viewport |
| 002b | `features/world/WorldCoordinator.tsx:117` | `h-full` → `h-screen` | Authoritative viewport height for map surface |
| 003 | `next.config.ts` | Added `rewrites()` proxying `/api/*` to port 5000 | Frontend API calls were hitting Next.js, not .NET backend |

---

**Document Version:** 1.0
**Last Updated:** 2026-04-04
