**Configuration**

- **Purpose**: centralize environment access, separate client-safe public config from server-only secrets, and fail early with clear messages when required public values are missing.

- **Client-safe access**: import from `lib/env/public.ts` (the exported `publicEnv`). Only `NEXT_PUBLIC_` prefixed values are read here.
- **Server-only access**: import `getServerEnv()` or `requireServerEnv()` from `lib/env/server.ts`. Never import `lib/env/server.ts` from client components.

Required variables (examples):

- `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` — public Mapbox token (REQUIRED). The app will fail early if missing.
- `NEXT_PUBLIC_API_BASE_URL` — base URL for frontend to call APIs (default `/`).

Required variables (examples):

- `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` — public Mapbox token (REQUIRED). The app intentionally fails early with a clear message if this is missing.
- `NEXT_PUBLIC_API_BASE_URL` — base URL for frontend to call APIs (default `/`).

Optional/feature flags:

- `NEXT_PUBLIC_ANALYTICS_ENABLED` — set to `true` to enable analytics.
- `NEXT_PUBLIC_FEATURE_FLAGS` — JSON object or comma-separated list of enabled flags.

Server-only secrets (never expose to client):

- `MAPBOX_SECRET` — secret Mapbox credentials (server use only).
- `API_KEY` — internal API key for server-to-server calls.
- `DISABLE_HMR` — development-only flag used by next.config.ts to disable file watching.

Server module guard:

- `lib/env/server.ts` contains a runtime guard that throws if it is imported client-side. Always access secrets via server-only code paths (API routes, server components) and never import that module from `app/` client components.

Local setup:

1. Copy `.env.local.example` to `.env.local` for development.
2. Fill `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` with a valid public token.
3. Start `npm run dev`.

Seed script note:

- The `scripts/seed-data.ts` script uses `getServerEnv().API_URL` (or falls back to `API_URL`) to determine the backend target. For local testing copy `.env.local.example` to `.env.local` and set `API_URL` to your running backend (default `http://localhost:5000`).

Security and deployment notes:

- Only `NEXT_PUBLIC_` prefixed variables are safe to import in client-side code. Audit any new env var — if it is a secret do not prefix with `NEXT_PUBLIC_` and only access it through server modules or API routes.
- The runtime guards in `lib/env/public.ts` intentionally throw when required public values are missing so the app fails fast with a deterministic error message.
- CI/CD systems should populate server-only secrets (non-`NEXT_PUBLIC_`) into the deployment environment. Do not commit real secrets into repository files.

Checklist for PR reviewers and deployers:

- Ensure any variable that contains secrets is NOT prefixed with `NEXT_PUBLIC_`.
- Verify `.env.example` lists all required public values and that `.env.local.example` is used for local development only.
- Confirm that client code imports only from `lib/env/public.ts` and server code imports from `lib/env/server.ts`.
