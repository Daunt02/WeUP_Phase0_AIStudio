using WeUP.Contracts.Ingestion;

namespace WeUP.Domain.Ingestion;

/// <summary>
/// Service responsible for creating and managing ingestion job state transitions.
/// </summary>
public interface IIngestionJobManager
{
	/// <summary>
	/// Creates a new job record in the <c>Pending</c> state.
	/// </summary>
	Task<IngestionJob> CreateJobAsync(IngestionRequest request, CancellationToken ct = default);

	/// <summary>
	/// Transitions the job identified by <paramref name="jobId"/> to <paramref name="newStatus"/>.
	/// Throws <see cref="InvalidOperationException"/> if the transition is not allowed.
	/// Optional <paramref name="failureReason"/> is persisted for failure states.
	/// </summary>
	Task TransitionJobAsync(string jobId, IngestionJobStatus newStatus, string? failureReason = null, CancellationToken ct = default);

	/// <summary>
	/// Returns the current state of a job, or <c>null</c> if not found.
	/// </summary>
	Task<IngestionJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}