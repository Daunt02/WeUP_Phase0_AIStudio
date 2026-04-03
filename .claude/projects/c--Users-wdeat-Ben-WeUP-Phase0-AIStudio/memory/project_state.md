---
name: WeUP Phase 0 Build State
description: Current build status, completed prompts, and what's next in the 030 prompt bundle
type: project
---

Up through P18 is implemented and committed. All 6 bundles of Pack 1 are now complete.

**Why:** Building WeUP Phase 0 city-grade MVP per the 030 Prompt Bundle Pack spec (mnt/ folder).

**Completed prompts:**
- P01–P15: Bundles 1–5 (runtime, domain, backend, ingestion, moderation)
- Bundle 5 Repairs (R5-01/02/03): WeUP.Application.csproj was missing, EF Core/Http packages missing from Infrastructure
- P16: Auth backbone (BearerTokenService, InMemoryUserRepository, /auth/* endpoints)
- P17: Itinerary + Preferences (InMemoryItineraryRepository, InMemoryPreferencesRepository, /api/users/me/* endpoints)
- P18: Event submission workflow (Draft→SubmittedForReview→Approved/Rejected, /api/events/submissions/*)

**Next prompts:** P19 (Bounding Box/Cluster/District queries), P20 (City Partitioning), P21 (Timeline/Calendar UX)

**How to apply:** When resuming, pick up at P19 (Bundle 7 — Spatial and Temporal Semantics).

**Runtime:** .NET 10 (not 8 — host only has .NET 10). All projects updated to net10.0.

**Known:** All in-memory stubs. Swap to EF Core + Postgres by setting WeUpDb connection string and switching registrations in Program.cs (instructions in Program.cs comments).
