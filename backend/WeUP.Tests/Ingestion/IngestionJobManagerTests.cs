// ------------------------------------------------------------
// File: WeUP.Tests/Ingestion/IngestionJobManagerTests.cs
// M1-P05 – Ingestion Job Lifecycle and State Tracking v1.0
// ------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Contracts.Ingestion;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Persistence;
using Xunit;

namespace WeUP.Tests.Ingestion;

public sealed class IngestionJobManagerTests
{
    // Build a fresh isolated in-memory context for each test.
    private static WeUpDbContext BuildContext() =>
        new WeUpDbContext(
            new DbContextOptionsBuilder<WeUpDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    private static IngestionRequest BuildRequest() => new IngestionRequest(
        RequestId: Guid.NewGuid(),
        SourceType: IngestionSourceType.ImageUpload,
        RawInput: "base64encodedimage",
        SubmittedByUserId: null,
        SubmittedAtUtc: DateTimeOffset.UtcNow);

    // -----------------------------------------------------------------------
    // CreateJobAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateJob_InitialState_IsPending()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);

        var job = await manager.CreateJobAsync(BuildRequest());

        Assert.Equal(IngestionJobStatus.Pending, job.Status);
        Assert.NotEmpty(job.JobId);
    }

    [Fact]
    public async Task CreateJob_PersistsEntityToDb()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);

        var job = await manager.CreateJobAsync(BuildRequest());

        var entity = await ctx.IngestionJobs.FirstOrDefaultAsync(j => j.JobId == job.JobId);
        Assert.NotNull(entity);
        Assert.Equal(IngestionJobStatus.Pending.ToString(), entity.Status);
    }

    [Fact]
    public async Task CreateJob_WritesAuditRecord()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);

        var job = await manager.CreateJobAsync(BuildRequest());

        var audit = await ctx.IngestionAudits.FirstOrDefaultAsync(a => a.JobId == job.JobId);
        Assert.NotNull(audit);
        Assert.Equal(IngestionJobStatus.Pending.ToString(), audit.Stage);
    }

    // -----------------------------------------------------------------------
    // TransitionJobAsync – happy paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Transition_ValidMonotonicPath_Succeeds()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        // Pending → Processing → OCR → Normalized → ReadyForDedup → Completed
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.OCR);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Normalized);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.ReadyForDedup);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Completed);

        var final = await manager.GetJobAsync(job.JobId);
        Assert.NotNull(final);
        Assert.Equal(IngestionJobStatus.Completed, final!.Status);
    }

    [Fact]
    public async Task Transition_ToFailed_StoresFailureReason()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.FAILED, "OCR timeout");

        var result = await manager.GetJobAsync(job.JobId);
        Assert.NotNull(result);
        Assert.Equal(IngestionJobStatus.FAILED, result!.Status);
        Assert.Equal("OCR timeout", result.ErrorMessage);
    }

    [Fact]
    public async Task Transition_RetryablePath_ProcessingAfterRetryableFailure()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.RETRYABLE_FAILURE, "transient timeout");
        // Retry is allowed
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing);

        var result = await manager.GetJobAsync(job.JobId);
        Assert.NotNull(result);
        Assert.Equal(IngestionJobStatus.Processing, result!.Status);
    }

    // -----------------------------------------------------------------------
    // TransitionJobAsync – invalid transitions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Transition_InvalidBacktrack_Throws()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.OCR);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Normalized);

        // Attempt illegal backtrack to OCR → must throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TransitionJobAsync(job.JobId, IngestionJobStatus.OCR));
    }

    [Fact]
    public async Task Transition_FromTerminalCompleted_Throws()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.OCR);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Normalized);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.ReadyForDedup);
        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing));
    }

    [Fact]
    public async Task Transition_FromTerminalFailed_Throws()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);
        var job = await manager.CreateJobAsync(BuildRequest());

        await manager.TransitionJobAsync(job.JobId, IngestionJobStatus.FAILED);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TransitionJobAsync(job.JobId, IngestionJobStatus.Processing));
    }

    // -----------------------------------------------------------------------
    // GetJobAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetJob_UnknownId_ReturnsNull()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);

        var result = await manager.GetJobAsync("nonexistent-job-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task TransitionJob_UnknownId_Throws()
    {
        var ctx = BuildContext();
        var manager = new IngestionJobManager(ctx, NullLogger<IngestionJobManager>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TransitionJobAsync("nonexistent-job-id", IngestionJobStatus.Processing));
    }
}
