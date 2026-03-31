# Bundle 3 — End-of-Bundle Repair Prompts

## Issues found after review of P07–P09

### R3-01: EventEntity missing Tags support
**Problem:** `EventDetailDto` returns empty `[]` for tags because `EventEntity` has no tag storage. The submission request accepts tags but they are silently dropped.
**Fix:** Add a `tags` text array column to EventEntity (Postgres `text[]` or a JSON column for Phase 0 simplicity), and write tags during `CreateSubmissionAsync`. Read them back in `ToDetail()`.

### R3-02: EfSaveRepository.GetSavesAsync uses LINQ Join with Include — not supported
**Problem:** EF Core does not support `.Include()` inside a `Join()` LINQ expression. The join will either fail at runtime or silently drop the Include.
**Fix:** Rewrite as two separate queries: fetch saves, then fetch matching events, then join in memory. Or use a navigation property approach.

### R3-03: MapFeedRequest uses POST but returns read-only data
**Problem:** Map feed and calendar feed use POST for query semantics. This is acceptable but breaks HTTP cache semantics and is non-standard for read endpoints. REST clients and CDNs expect GET for reads.
**Fix:** Convert map feed and calendar feed to GET endpoints with query parameters, or document the POST-as-query pattern explicitly and add `Cache-Control: no-store` to avoid caching confusion.

### R3-04: StubEventRepository and EfEventRepository both implement two interfaces from one class
**Problem:** A single class implementing both `IEventRepository` and `IEventSubmissionRepository` means they share the same DI lifetime. If registered as Scoped (required for EF DbContext), the stub (registered as Singleton) will conflict.
**Fix:** Extract `EfEventSubmissionRepository` as a separate class, or register the EF implementation as Scoped for both interfaces consistently.

### R3-05: Missing appsettings.json in WeUP.Api
**Problem:** The project has no `appsettings.json` or `appsettings.Development.json`, making local dev configuration invisible and preventing the connection string pattern from working.
**Fix:** Add `appsettings.json` with placeholder structure and `appsettings.Development.json` (gitignored) with local dev values.
