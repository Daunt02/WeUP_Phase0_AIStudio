using WeUP.Infrastructure.Auth;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Markets;
using WeUP.Infrastructure.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Submissions;

namespace WeUP.Infrastructure.Seed;

public sealed class Phase0SeedService(
    Phase0SeedLoader loader,
    StubEventRepository events,
    StubSaveRepository saves,
    InMemoryUserRepository users,
    InMemorySubmissionRepository submissions,
    InMemoryModerationQueue moderation,
    InMemoryIngestionJobRepository ingestionJobs,
    MarketPolicyService markets)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Phase0SeedSnapshot? _lastSnapshot;

    public Phase0SeedSnapshot? CurrentSnapshot => _lastSnapshot;

    public async Task<Phase0SeedSnapshot> ResetAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var dataset = loader.Load();

            events.Reset(dataset);
            saves.Reset(dataset);
            users.Reset(dataset);
            submissions.Reset(dataset);
            moderation.Reset(dataset);
            ingestionJobs.Reset(dataset);
            markets.Reset(dataset);

            _lastSnapshot = new Phase0SeedSnapshot(
                dataset.Meta.SeedVersion,
                dataset.Meta.FixedNow,
                dataset.Markets.Length,
                dataset.Venues.Length,
                dataset.Events.Length,
                dataset.Users.Length,
                dataset.Saves.Length,
                dataset.ModerationItems.Length,
                dataset.IngestionJobs.Length,
                loader.ResolveDatasetPath());

            return _lastSnapshot;
        }
        finally
        {
            _gate.Release();
        }
    }
}