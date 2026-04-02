# Development README

Quick steps to run the app locally (frontend + backend):

1) Frontend (Next.js)

Install dependencies and run dev server:

```bash
npm install
npm run dev
```

The frontend will expect `NEXT_PUBLIC_API_BASE_URL` and `NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` in `.env.local`.

Create `.env.local` by copying `.env.local.example` and updating values:

```powershell
cp .env.local.example .env.local
# then edit .env.local and replace tokens
```

2) Backend (.NET minimal API)

From the repo root run:

```powershell
dotnet run --project backend/WeUP.Api
```

By default the backend will bind to the Kestrel defaults (check terminal output). The frontend example `NEXT_PUBLIC_API_BASE_URL` uses `http://localhost:5000` — adjust if your backend started on a different port.

3) Health check & smoke test

After the backend starts, verify it with:

```powershell
# PowerShell
.\scripts\smoke-check.ps1

# Or cURL
curl http://localhost:5000/health
```

4) Notes
- Local dev auth uses a dev token stored in `localStorage` under `weup_dev_token` (see `services/auth.ts`). This is dev-only; do not use for production.
- To switch to the EF/Postgres implementation, follow the comments in `backend/WeUP.Api/Program.cs` and the migrations README under `backend/WeUP.Infrastructure/Persistence/Migrations`.
