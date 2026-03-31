**Configuration**

- **Purpose**: centralize environment access, separate client-safe public config from server-only secrets, and fail early with clear messages when required public values are missing.

- **Client-safe access**: import from `lib/env/public.ts` (the exported `publicEnv`). Only `NEXT_PUBLIC_` prefixed values are read here.
- **Server-only access**: import `getServerEnv()` or `requireServerEnv()` from `lib/env/server.ts`. Never import `lib/env/server.ts` from client components.

Required variables (examples):
- `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` — public Mapbox token (REQUIRED). The app will fail early if missing.
- `NEXT_PUBLIC_API_BASE_URL` — base URL for frontend to call APIs (default `/`).

Optional/feature flags:
- `NEXT_PUBLIC_ANALYTICS_ENABLED` — set to `true` to enable analytics.
- `NEXT_PUBLIC_FEATURE_FLAGS` — JSON object or comma-separated list of enabled flags.

Server-only secrets (never expose to client):
- `MAPBOX_SECRET` — secret Mapbox credentials (server use only).
- `API_KEY` — internal API key for server-to-server calls.
- `DISABLE_HMR` — development-only flag used by next.config.ts to disable file watching.

Local setup:
1. Copy `.env.local.example` to `.env.local` for development.
2. Fill `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` with a valid public token.
3. Start `npm run dev`.

Security and deployment notes:
- Only `NEXT_PUBLIC_` prefixed variables are safe to import in client-side code. Audit any new env var — if it is a secret do not prefix with `NEXT_PUBLIC_` and only access it through server modules or API routes.
- The runtime guards in `lib/env/public.ts` intentionally throw when required public values are missing so the app fails fast with a deterministic error message.
- CI/CD systems should populate server-only secrets (non-`NEXT_PUBLIC_`) into the deployment environment. Do not commit real secrets into repository files.
