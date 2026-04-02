# Bundle 5 Repairs

## R5-01 — Create WeUP.Application Project

**Problem:** `WeUP.Application/` folder contained orphaned `.cs` files (IngestionDispatcher, ModerationQueueService, PublishEligibilityService, ReviewActionService, DeduplicationService) with no `.csproj`. The Application layer was not part of any project in the solution, causing it to never compile.

**Fix:**
- Created `backend/WeUP.Application/WeUP.Application.csproj` referencing Contracts, Domain, and Infrastructure.
- Added project to `WeUP.sln` via `dotnet sln add`.
- Added `<ProjectReference>` to `WeUP.Api.csproj`.

**Note:** Application references Infrastructure because `DeduplicationService` uses `WeUpDbContext` directly (P12 design). This is a pragmatic Phase 0 shortcut acknowledged in code comments.

---

## R5-02 — Add Missing NuGet Packages to WeUP.Infrastructure

**Problem:** `WeUP.Infrastructure.csproj` had a placeholder comment (`<!-- EF Core packages added in P08 -->`) but no actual `<PackageReference>` entries. This caused 22 build errors across all EF Core–dependent files and 2 errors for `IHttpClientFactory`.

**Fix:**
- Added `Npgsql.EntityFrameworkCore.PostgreSQL` v8.* (bundles `Microsoft.EntityFrameworkCore`).
- Added `Microsoft.Extensions.Http` v8.* for `IHttpClientFactory` (used by `LinkAdapter` and `VenuePageAdapter`).
- Added `Microsoft.EntityFrameworkCore` v8.* to `WeUP.Application.csproj` for LINQ extension methods in `DeduplicationService`.

---

## R5-03 — IngestionDispatcher Cleanup

**Problem:** `IngestionDispatcher` was injecting concrete adapter types (`ManualSubmissionAdapter`, `LinkAdapter`, `VenuePageAdapter`) from Infrastructure, coupling Application to Infrastructure's implementation details.

**Fix (applied as working-copy change before P16):**
- Changed to `IEnumerable<IIngestionAdapter>` with dictionary dispatch.
- Removed `using WeUP.Infrastructure.Ingestion.Adapters;` from Application layer.
- Result: adding a new adapter only requires DI registration in `Program.cs` — no changes to `IngestionDispatcher`.
