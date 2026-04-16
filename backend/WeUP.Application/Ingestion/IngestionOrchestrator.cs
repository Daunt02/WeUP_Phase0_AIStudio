using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Flyer;
using WeUP.Domain.Ingestion;
using WeUP.Domain.Ocr;

namespace WeUP.Application.Ingestion;

public sealed class IngestionOrchestrator(
    IRawIngestionPayloadFactory payloadFactory,
    IOcrService ocrService,
    INormalizationEngine normalizationEngine,
    IIngestionOrchestrationRepository repository,
    IIngestionLifecycleObserver observer) : IIngestionOrchestrator
{
    public async Task<IngestionJob> StartAsync(IngestionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var job = new IngestionJob(
            JobId: Guid.NewGuid().ToString("N"),
            RequestId: request.RequestId,
            SourceType: request.SourceType,
            SubmittedBy: request.SubmittedBy,
            Status: IngestionJobStatus.Pending,
            Payload: null,
            Ocr: null,
            Normalized: null,
            Evidence: [],
            Lifecycle: [new IngestionStatusRecord(IngestionJobStatus.Pending, now, "Ingestion request accepted.")],
            OcrAttemptCount: 0,
            ErrorMessage: null,
            Metadata: request.Metadata,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            CompletedAtUtc: null);

        job = await repository.CreateAsync(job, ct);
        await observer.ObserveAsync("job_started", job, BuildProperties(job), ct);

        try
        {
            job = await TransitionAsync(job, IngestionJobStatus.Processing, "Source adapter execution started.", ct);

            RawIngestionPayload payload;
            try
            {
                payload = await payloadFactory.CreateAsync(request, ct);
            }
            catch (Exception ex)
            {
                return await FailAsync(job, $"Adapter failure: {ex.Message}", "adapter_failure", ct);
            }

            job = job with
            {
                Payload = payload,
                Evidence =
                [
                    ..job.Evidence,
                    new IngestionEvidenceRecord(
                        EvidenceId: $"raw-{payload.ContentSha256[..Math.Min(payload.ContentSha256.Length, 24)]}",
                        Stage: IngestionJobStatus.Processing.ToString(),
                        Kind: "raw_payload",
                        Reference: payload.ContentSha256,
                        Payload: payload.ManualEntryText,
                        ObservedAtUtc: DateTimeOffset.UtcNow,
                        Metadata: payload.Metadata)
                ],
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await repository.SaveAsync(job, ct);

            job = await TransitionAsync(job, IngestionJobStatus.OCR, "OCR extraction started.", ct);
            var ocr = await ExecuteOcrWithRetryAsync(job, payload, ct);
            if (!ocr.Success)
            {
                var failedWithOcr = job with
                {
                    Ocr = ocr,
                    OcrAttemptCount = ocr.AttemptCount,
                    Evidence =
                    [
                        ..job.Evidence,
                        BuildOcrEvidence(ocr, success: false),
                    ],
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };

                await repository.SaveAsync(failedWithOcr, ct);
                return await FailAsync(
                    failedWithOcr,
                    $"OCR failed after retry: {ocr.FailureReason ?? "unknown failure"}",
                    "ocr_failure",
                    ct);
            }

            job = job with
            {
                Ocr = ocr,
                OcrAttemptCount = ocr.AttemptCount,
                Evidence =
                [
                    ..job.Evidence,
                    BuildOcrEvidence(ocr, success: true),
                ],
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await repository.SaveAsync(job, ct);
            await observer.ObserveAsync("ocr_completed", job, BuildProperties(job), ct);

            job = await TransitionAsync(job, IngestionJobStatus.Normalized, "Normalization started.", ct);

            EventCandidate normalized;
            try
            {
                normalized = normalizationEngine.Normalize(ocr);
            }
            catch (Exception ex)
            {
                // Preserve OCR evidence even when normalization fails.
                return await FailAsync(job, $"Normalization failure: {ex.Message}", "normalization_failure", ct);
            }

            var normalizedSnapshot = ToSnapshot(normalized);
            job = job with
            {
                Normalized = normalizedSnapshot,
                Evidence =
                [
                    ..job.Evidence,
                    new IngestionEvidenceRecord(
                        EvidenceId: $"norm-{job.JobId[..12]}",
                        Stage: IngestionJobStatus.Normalized.ToString(),
                        Kind: "normalized_payload",
                        Reference: normalizedSnapshot.Title ?? "untitled",
                        Payload: normalizedSnapshot.Description(),
                        ObservedAtUtc: DateTimeOffset.UtcNow,
                        Metadata: job.Metadata)
                ],
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await repository.SaveAsync(job, ct);
            await observer.ObserveAsync("normalization_completed", job, BuildProperties(job), ct);

            job = await TransitionAsync(job, IngestionJobStatus.ReadyForDedup, "Candidate staged for deduplication.", ct);
            job = await TransitionAsync(job, IngestionJobStatus.Completed, "Ingestion lifecycle completed.", ct, completed: true);

            var durationMs = Math.Max(0, (long)(job.UpdatedAtUtc - job.CreatedAtUtc).TotalMilliseconds);
            await observer.ObserveAsync(
                "job_duration",
                job,
                MergeProperties(BuildProperties(job), new Dictionary<string, string?>
                {
                    ["duration_ms"] = durationMs.ToString(),
                }),
                ct);

            return job;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await FailAsync(job, ex.Message, "unhandled_failure", ct);
        }
    }

    public Task<IngestionJob?> GetAsync(string jobId, CancellationToken ct = default)
        => repository.GetAsync(jobId, ct);

    private async Task<IngestionJob> TransitionAsync(
        IngestionJob current,
        IngestionJobStatus next,
        string detail,
        CancellationToken ct,
        bool completed = false)
    {
        var now = DateTimeOffset.UtcNow;
        var transitioned = current with
        {
            Status = next,
            Lifecycle = [.. current.Lifecycle, new IngestionStatusRecord(next, now, detail)],
            UpdatedAtUtc = now,
            CompletedAtUtc = completed ? now : current.CompletedAtUtc,
        };

        await repository.SaveAsync(transitioned, ct);
        return transitioned;
    }

    private async Task<IngestionJob> FailAsync(IngestionJob current, string error, string reasonCode, CancellationToken ct)
    {
        var failed = await TransitionAsync(current with { ErrorMessage = error }, IngestionJobStatus.FAILED, error, ct, completed: true);
        await observer.ObserveAsync(
            "job_failed",
            failed,
            MergeProperties(
                BuildProperties(failed),
                new Dictionary<string, string?>
                {
                    ["reason_code"] = reasonCode,
                    ["error_message"] = error,
                }),
            ct);

        return failed;
    }

    private async Task<OcrResult> ExecuteOcrWithRetryAsync(IngestionJob job, RawIngestionPayload payload, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(payload.ManualEntryText))
        {
            var now = DateTimeOffset.UtcNow;
            return new OcrResult(
                ExtractionId: Guid.NewGuid().ToString("N"),
                JobId: job.JobId,
                AssetId: payload.ContentSha256[..Math.Min(payload.ContentSha256.Length, 24)],
                Provider: "manual-entry",
                ProviderVersion: "v1",
                Confidence: 1.0,
                Success: true,
                RawText: payload.ManualEntryText,
                Blocks: [],
                FailureReason: null,
                AttemptCount: 1,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                Metadata: payload.Metadata);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"weup-ingest-{job.JobId}.bin");
        await File.WriteAllBytesAsync(tempPath, payload.ContentBytes.ToArray(), ct);

        try
        {
            var first = await ocrService.ExtractAsync(BuildOcrRequest(job, payload, tempPath), ct);
            if (first.Success || first.AttemptCount >= 2)
            {
                return first;
            }

            return await ocrService.ExtractAsync(BuildOcrRequest(job, payload, tempPath), ct);
        }
        catch (Exception ex)
        {
            var now = DateTimeOffset.UtcNow;
            return new OcrResult(
                ExtractionId: Guid.NewGuid().ToString("N"),
                JobId: job.JobId,
                AssetId: payload.ContentSha256[..Math.Min(payload.ContentSha256.Length, 24)],
                Provider: "orchestrator",
                ProviderVersion: "v1",
                Confidence: 0.0,
                Success: false,
                RawText: string.Empty,
                Blocks: [],
                FailureReason: ex.Message,
                AttemptCount: 2,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                Metadata: payload.Metadata);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static OcrRequest BuildOcrRequest(IngestionJob job, RawIngestionPayload payload, string localPath)
        => new(
            JobId: job.JobId,
            AssetId: payload.ContentSha256[..Math.Min(payload.ContentSha256.Length, 24)],
            StorageKey: $"ingestion/{job.JobId}/{payload.SourceType}",
            ContentType: payload.ContentType ?? "application/octet-stream",
            OriginalFilename: payload.OriginalFileName,
            LocalPath: localPath,
            Metadata: payload.Metadata);

    private static IngestionEvidenceRecord BuildOcrEvidence(OcrResult ocr, bool success)
        => new(
            EvidenceId: $"ocr-{ocr.ExtractionId}",
            Stage: IngestionJobStatus.OCR.ToString(),
            Kind: "ocr_output",
            Reference: ocr.ExtractionId,
            Payload: ocr.RawText,
            ObservedAtUtc: ocr.CompletedAtUtc,
            Metadata: new Dictionary<string, string?>
            {
                ["provider"] = ocr.Provider,
                ["provider_version"] = ocr.ProviderVersion,
                ["attempt_count"] = ocr.AttemptCount.ToString(),
                ["success"] = success ? "true" : "false",
                ["failure_reason"] = ocr.FailureReason,
            });

    private static NormalizedPayloadSnapshot ToSnapshot(EventCandidate candidate)
    {
        var fieldConfidence = candidate.FieldScores.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Confidence,
            StringComparer.OrdinalIgnoreCase);

        var aggregate = fieldConfidence.Count == 0 ? 0.0 : fieldConfidence.Values.Average();

        return new NormalizedPayloadSnapshot(
            Title: candidate.Title,
            Venue: candidate.Venue,
            Address: candidate.Address,
            StartUtc: candidate.StartUtc?.UtcDateTime.ToString("O"),
            EndUtc: candidate.EndUtc?.UtcDateTime.ToString("O"),
            Tags: candidate.Tags,
            RawFields: candidate.RawFields,
            FieldConfidences: fieldConfidence,
            AggregateConfidence: aggregate);
    }

    private static Dictionary<string, string?> BuildProperties(IngestionJob job)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["job_id"] = job.JobId,
            ["request_id"] = job.RequestId,
            ["source_type"] = job.SourceType.ToString(),
            ["status"] = job.Status.ToString(),
            ["ocr_attempt_count"] = job.OcrAttemptCount.ToString(),
        };

    private static Dictionary<string, string?> MergeProperties(
        IReadOnlyDictionary<string, string?> left,
        IReadOnlyDictionary<string, string?> right)
    {
        var merged = new Dictionary<string, string?>(left, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in right)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup only
        }
    }
}

internal static class NormalizedPayloadSnapshotExtensions
{
    public static string Description(this NormalizedPayloadSnapshot snapshot)
        => $"title={snapshot.Title ?? "(none)"}; venue={snapshot.Venue ?? "(none)"}; confidence={snapshot.AggregateConfidence:F3}";
}
