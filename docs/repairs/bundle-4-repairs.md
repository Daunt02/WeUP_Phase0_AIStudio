# Bundle 4 — End-of-Bundle Repair Prompts

## Issues found after review of P10–P12

### R4-01: IngestionDispatcher depends on concrete adapter types instead of IIngestionAdapter
**Problem:** IngestionDispatcher constructor takes `ManualSubmissionAdapter`, `LinkAdapter`, `VenuePageAdapter` directly. This creates a concrete coupling and prevents adding new adapters without changing the dispatcher.
**Fix:** Change IngestionDispatcher to accept `IEnumerable<IIngestionAdapter>` and route by `SourceKind` property.

### R4-02: IExtractionNormalizer interface defined but never implemented
**Problem:** `IExtractionNormalizer` in `IIngestionAdapter.cs` has no implementation and is not used in the pipeline. It's a dead interface that creates confusion.
**Fix:** Remove or implement it — in Phase 0, the normalizer lives in the Flyer pipeline (ILlmEventNormalizer). Consolidate or clearly comment the placeholder.

### R4-03: DeduplicationService uses EntityFrameworkQueryableExtensions without a using statement
**Problem:** The fully-qualified `Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync()` call is a workaround for a missing `using Microsoft.EntityFrameworkCore;` directive.
**Fix:** Add `using Microsoft.EntityFrameworkCore;` and use `.ToListAsync()` directly.

### R4-04: FlyerIngestionPipeline registered nowhere in Program.cs
**Problem:** The flyer pipeline and its dependencies (storage, OCR stubs, geocoding, etc.) are not registered in the DI container and there is no flyer upload endpoint.
**Fix:** Register flyer pipeline services in Program.cs and add POST /api/ingestion/flyers endpoint.
