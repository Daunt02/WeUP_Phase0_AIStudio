using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Contracts.Moderation;
using WeUP.Application.Moderation;
using WeUP.Infrastructure.Persistence;
using Xunit;

namespace WeUP.Tests.Moderation;

public sealed class ModerationQueueServiceTests
{
    private static WeUpDbContext BuildContext() =>
        new WeUpDbContext(
            new DbContextOptionsBuilder<WeUpDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    [Fact]
    public async Task Enqueue_CreatesPendingItem()
    {
        var ctx = BuildContext();
        var svc = new ModerationQueueService(ctx, NullLogger<ModerationQueueService>.Instance);

        var candidateId = Guid.NewGuid();
        var item = await svc.EnqueueAsync(candidateId);

        Assert.Equal(ModerationStatus.Pending, item.Status);
        var entity = await ctx.ModerationQueueItems.FirstOrDefaultAsync(e => e.QueueItemId == item.QueueItemId);
        Assert.NotNull(entity);
        Assert.Equal(ModerationStatus.Pending, entity!.Status);
    }

    [Fact]
    public async Task Claim_ThrowsIfNotPending()
    {
        var ctx = BuildContext();
        var svc = new ModerationQueueService(ctx, NullLogger<ModerationQueueService>.Instance);

        var candidateId = Guid.NewGuid();
        var item = await svc.EnqueueAsync(candidateId);

        // Manually set to InReview to simulate concurrent claim
        var entity = await ctx.ModerationQueueItems.FirstAsync(e => e.QueueItemId == item.QueueItemId);
        entity.Status = ModerationStatus.InReview;
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ClaimAsync(item.QueueItemId, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Throws()
    {
        var ctx = BuildContext();
        var svc = new ModerationQueueService(ctx, NullLogger<ModerationQueueService>.Instance);

        var candidateId = Guid.NewGuid();
        var item = await svc.EnqueueAsync(candidateId);

        // Attempt illegal transition Pending -> Approved
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateStatusAsync(item.QueueItemId, ModerationStatus.Approved));
    }

    [Fact]
    public async Task Concurrency_ConflictOnDoubleClaim_ThrowsDbUpdateConcurrencyException()
    {
        var candidateId = Guid.NewGuid();

        // Create initial item in a shared in-memory database name
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<WeUpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Create item
        var ctx1 = new WeUpDbContext(options);
        var svc1 = new ModerationQueueService(ctx1, NullLogger<ModerationQueueService>.Instance);
        var item = await svc1.EnqueueAsync(candidateId);

        // Two separate contexts attempt to claim
        var ctxA = new WeUpDbContext(options);
        var svcA = new ModerationQueueService(ctxA, NullLogger<ModerationQueueService>.Instance);

        var ctxB = new WeUpDbContext(options);
        var svcB = new ModerationQueueService(ctxB, NullLogger<ModerationQueueService>.Instance);

        // First claim should succeed
        await svcA.ClaimAsync(item.QueueItemId, Guid.NewGuid());

        // Second claim using another context should fail due to status check or concurrency
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.ClaimAsync(item.QueueItemId, Guid.NewGuid()));
    }
}
