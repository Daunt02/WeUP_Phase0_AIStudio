using System.Globalization;
using System.Text.Json;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Ocr;
using WeUP.Domain.Flyer;
using WeUP.Domain.Ingestion;
using WeUP.Domain.Media;
using FlyerNormalizationEngineAlias = WeUP.Domain.Flyer.INormalizationEngine;

namespace WeUP.Infrastructure.Flyer;

public sealed class FlyerIngestionPipeline(
    IFlyerAssetStore assetStore,
    IProvenanceRepository provenanceRepository,
    IFlyerEvidenceRepository evidenceRepository,
    IFlyerTextPostProcessor postProcessor,
    IFlyerOcrService ocrService,
    IFlyerNormalizationService normalizationService,
    IFlyerConfidenceEvaluator confidenceEvaluator,
    IIngestionJobRepository jobs,
    IIngestionAuditWriter audit) : IFlyerIngestionPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IngestionResult> SubmitAsync(FlyerUploadIngestionRequest request, CancellationToken ct = default)
    {
        var asset = await assetStore.GetAsync(request.AssetId, ct);
        if (asset is null)
        {
            throw new InvalidOperationException($"Flyer asset '{request.AssetId}' was not found.");
        }

        var sourceReference = $"flyer-asset:{asset.AssetId}";
        var metadata = new Dictionary<string, string?>
        {
            ["pipeline"] = "flyer-ingestion-v1",
            ["assetId"] = asset.AssetId,
            ["submissionId"] = request.SubmissionId,
            ["sourceUrl"] = request.SourceUrl,
            ["partnerProvider"] = request.PartnerProvider,
            ["reprocess"] = request.Reprocess ? "true" : "false",
        };

        var envelope = new IngestionRequestEnvelope(
            RequestId: Guid.NewGuid().ToString("N"),
            SourceKind: IngestionSourceKind.FlyerUpload,
            SourceReference: sourceReference,
            SubmittedBy: request.SubmittedBy,
            RawPayloadJson: JsonSerializer.Serialize(request, JsonOptions),
            IdempotencyKey: BuildIdempotencyKey(request.AssetId, request.SubmittedBy, request.Reprocess),
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            Metadata: metadata);

        var job = await jobs.CreateAsync(envelope, ct);
        await audit.WriteAsync(job.JobId, IngestionJobStatus.RECEIVED.ToString(), $"assetId={request.AssetId}", ct);

        var lifecycleAsset = ToAssetReference(asset);

        try
        {
            job = Transition(job, IngestionJobStatus.VALIDATING, "Flyer asset and provenance validation started.");
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, IngestionJobStatus.VALIDATING.ToString(), "asset-lookup-ok", ct);

            var provenance = await GetOrCreateProvenanceAsync(asset, request, job.JobId, ct);
            var evidence = await GetOrCreateEvidenceAsync(asset, provenance, request, job.JobId, ct);

            job = Transition(job, IngestionJobStatus.NORMALIZING, "OCR extraction started.");
            await jobs.SaveAsync(job, ct);

            var ocr = await ocrService.ExtractAsync(lifecycleAsset, job.JobId, ct);
            await audit.WriteAsync(job.JobId, "OCR", $"success={ocr.Success} confidence={ocr.Confidence:F2}", ct);

            var stageEvidence = BuildStageEvidence(job.JobId, lifecycleAsset, ocr, evidence, provenance);
            var stageIssues = ocr.Issues;
            if (!ocr.Success)
            {
                stageIssues =
                [
                    ..ocr.Issues,
                    BuildIssue(
                        "flyer_ocr_unreadable",
                        "Flyer OCR failed or returned unreadable text.",
                        IngestionIssueSeverity.Error,
                        retryable: false,
                        field: "ocr")
                ];

                await UpdateEvidenceForFailureAsync(evidence, ocr, stageIssues, ct);

                job = Transition(
                    job,
                    IngestionJobStatus.FAILED,
                    "OCR failed; candidate could not be normalized.",
                    issues: stageIssues,
                    evidence: stageEvidence);

                await jobs.SaveAsync(job, ct);
                await audit.WriteAsync(job.JobId, IngestionJobStatus.FAILED.ToString(), "ocr-failed", ct);
                return job;
            }

            var cleanedText = postProcessor.Clean(ocr.RawText);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                stageIssues =
                [
                    ..stageIssues,
                    BuildIssue(
                        "flyer_ocr_empty",
                        "OCR succeeded but no usable text remained after cleanup.",
                        IngestionIssueSeverity.Error,
                        retryable: false,
                        field: "ocrText")
                ];

                await UpdateEvidenceForFailureAsync(evidence, ocr, stageIssues, ct);

                job = Transition(
                    job,
                    IngestionJobStatus.FAILED,
                    "OCR output was empty after post-processing.",
                    issues: stageIssues,
                    evidence: stageEvidence);

                await jobs.SaveAsync(job, ct);
                await audit.WriteAsync(job.JobId, IngestionJobStatus.FAILED.ToString(), "ocr-cleaned-empty", ct);
                return job;
            }

            var normalizationRequest = new FlyerNormalizationRequest(
                lifecycleAsset,
                ocr,
                cleanedText,
                job.JobId,
                new Dictionary<string, string?>
                {
                    ["submittedBy"] = request.SubmittedBy,
                    ["sourceUrl"] = request.SourceUrl,
                    ["partnerProvider"] = request.PartnerProvider,
                    ["sourceTier"] = provenance.SourceTier.ToString(),
                });

            var normalization = await normalizationService.NormalizeAsync(normalizationRequest, ct);
            await audit.WriteAsync(job.JobId, "NORMALIZATION", $"review={normalization.RequiresManualReview}", ct);

            stageIssues = [.. stageIssues, .. normalization.Issues];
            var confidence = confidenceEvaluator.Evaluate(
                lifecycleAsset,
                ocr,
                normalization.Candidate,
                provenance.BaselineAuthority,
                normalization.ReviewTriggers);

            var reviewReasons = normalization.ReviewTriggers
                .Union(BuildConfidenceReviewReasons(confidence))
                .Distinct()
                .ToArray();

            var requiresReview = normalization.RequiresManualReview || reviewReasons.Length > 0;
            var finalCandidate = normalization.Candidate?.CanonicalCandidate;

            stageEvidence =
            [
                ..stageEvidence,
                BuildNormalizationEvidence(normalization, confidence, lifecycleAsset),
            ];

            await UpdateEvidenceForSuccessAsync(evidence, provenance, ocr, normalization, confidence, reviewReasons, ct);

            job = Transition(
                job,
                IngestionJobStatus.CANDIDATE_CREATED,
                finalCandidate is null ? "Normalization returned no canonical candidate." : "Canonical candidate created from flyer extraction.",
                candidate: finalCandidate,
                issues: stageIssues,
                evidence: stageEvidence);
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, IngestionJobStatus.CANDIDATE_CREATED.ToString(), "candidate-created", ct);

            var terminalStatus = requiresReview ? IngestionJobStatus.REQUIRES_REVIEW : IngestionJobStatus.CANDIDATE_CREATED;
            var detail = requiresReview
                ? $"Manual review required ({string.Join(", ", reviewReasons.Select(r => r.ToString()))})."
                : "Candidate produced without review blockers.";

            job = Transition(
                job,
                terminalStatus,
                detail,
                candidate: finalCandidate,
                issues: stageIssues,
                evidence: stageEvidence);

            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, terminalStatus.ToString(), detail, ct);
            return job;
        }
        catch (Exception ex)
        {
            var failure = BuildIssue("flyer_pipeline_failed", ex.Message, IngestionIssueSeverity.Error, retryable: false, field: null);
            job = Transition(job, IngestionJobStatus.FAILED, ex.Message, issues: [.. job.Issues, failure]);
            await jobs.SaveAsync(job, ct);
            await audit.WriteAsync(job.JobId, IngestionJobStatus.FAILED.ToString(), ex.Message, ct);
            return job;
        }
    }

    public async Task<FlyerIngestionJobDetailResponse?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        var job = await jobs.GetAsync(jobId, ct);
        if (job is null)
        {
            return null;
        }

        var evidence = await evidenceRepository.GetByIngestionJobIdAsync(jobId, ct);
        if (evidence is null)
        {
            return null;
        }

        var assetRecord = await assetStore.GetAsync(evidence.AssetId, ct);
        if (assetRecord is null)
        {
            return null;
        }

        var asset = ToAssetReference(assetRecord);
        var parsedCandidate = ParseNormalizationSnapshot(evidence.NormalizationSnapshotJson);
        var parsedConfidence = ParseConfidence(evidence.NormalizationSnapshotJson);
        var parsedReviewReasons = ParseReviewReasons(evidence.ReviewReasons);

        var ocr = BuildOcrResultFromEvidence(evidence, jobId);
        var confidence = parsedConfidence ?? BuildFallbackConfidence(job.Candidate, parsedReviewReasons);
        var requiresReview = job.Status == IngestionJobStatus.REQUIRES_REVIEW || parsedReviewReasons.Length > 0;

        return new FlyerIngestionJobDetailResponse(
            JobId: job.JobId,
            Status: job.Status,
            Asset: asset,
            Ocr: ocr,
            Candidate: parsedCandidate,
            Confidence: confidence,
            RequiresManualReview: requiresReview,
            ReviewReasons: parsedReviewReasons,
            Evidence: job.Evidence,
            Issues: job.Issues,
            Lifecycle: job.Lifecycle,
            CreatedAtUtc: job.CreatedAtUtc,
            UpdatedAtUtc: job.UpdatedAtUtc);
    }

    public async Task<FlyerIngestionEvidenceResponse?> GetEvidenceAsync(string jobId, CancellationToken ct = default)
    {
        var job = await jobs.GetAsync(jobId, ct);
        if (job is null)
        {
            return null;
        }

        var evidence = await evidenceRepository.GetByIngestionJobIdAsync(jobId, ct);
        if (evidence is null)
        {
            return null;
        }

        var assetRecord = await assetStore.GetAsync(evidence.AssetId, ct);
        if (assetRecord is null)
        {
            return null;
        }

        var asset = ToAssetReference(assetRecord);
        return new FlyerIngestionEvidenceResponse(
            JobId: jobId,
            Asset: asset,
            ProvenanceId: evidence.ProvenanceId,
            EvidenceId: evidence.EvidenceId,
            OcrExtractionId: evidence.OcrExtractionId,
            OcrEngineVersion: evidence.OcrEngineVersion,
            NormalizationRunId: evidence.NormalizationRunId,
            NormalizationVersion: evidence.NormalizationVersion,
            RawOcrTextSnapshot: evidence.OcrText,
            ProcessingHistory: evidence.ProcessingHistory,
            ValidationFailures: evidence.ValidationFailures,
            ReviewReasons: evidence.ReviewReasons,
            Evidence: job.Evidence);
    }

    private static string BuildIdempotencyKey(string assetId, string submittedBy, bool reprocess)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes($"{assetId}|{submittedBy}|{reprocess}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    }

    private async Task<ProvenanceRecord> GetOrCreateProvenanceAsync(
        FlyerAssetRecord asset,
        FlyerUploadIngestionRequest request,
        string jobId,
        CancellationToken ct)
    {
        var existing = await provenanceRepository.GetByAssetIdAsync(asset.AssetId, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.IngestionJobId, jobId, StringComparison.Ordinal))
            {
                existing = existing with { IngestionJobId = jobId };
                await provenanceRepository.SaveAsync(existing, ct);
            }

            return existing;
        }

        var provenance = new ProvenanceRecord
        {
            ProvenanceId = Guid.NewGuid().ToString("N"),
            AssetId = asset.AssetId,
            SourceTier = SourceTier.T3_Unverified,
            UploaderUserId = asset.SubmitterId,
            UploadOrigin = FlyerUploadOrigin.ManualUploader,
            SourceType = FlyerSourceType.IngestionJobImport,
            SubmitterHash = ProvenanceRecord.HashSubmitterId(asset.SubmitterId),
            RecordedAt = DateTimeOffset.UtcNow,
            BaselineAuthority = ProvenanceRecord.BaselineAuthorityForTier(SourceTier.T3_Unverified),
            SourceUrl = request.SourceUrl,
            SubmissionId = request.SubmissionId,
            IngestionJobId = jobId,
            PartnerProvider = request.PartnerProvider,
            SubmitterNote = request.SubmitterNote,
        };

        return await provenanceRepository.SaveAsync(provenance, ct);
    }

    private async Task<FlyerEvidenceRecord> GetOrCreateEvidenceAsync(
        FlyerAssetRecord asset,
        ProvenanceRecord provenance,
        FlyerUploadIngestionRequest request,
        string jobId,
        CancellationToken ct)
    {
        var existing = await evidenceRepository.GetByAssetIdAsync(asset.AssetId, ct);
        if (existing is not null)
        {
            if (request.Reprocess || existing.IngestionJobId is null || !string.Equals(existing.IngestionJobId, jobId, StringComparison.Ordinal))
            {
                var updated = existing with
                {
                    IngestionJobId = jobId,
                    LinkedWorkflowIds = MergeWorkflowIds(existing.LinkedWorkflowIds, jobId, request.SubmissionId),
                    ProcessingHistory = [
                        .. existing.ProcessingHistory,
                        $"ingestion:job:{jobId}:reprocess={request.Reprocess.ToString().ToLowerInvariant()}"
                    ],
                };
                await evidenceRepository.UpdateAsync(updated, ct);
                return updated;
            }

            return existing;
        }

        var created = new FlyerEvidenceRecord
        {
            EvidenceId = Guid.NewGuid().ToString("N"),
            AssetId = asset.AssetId,
            OriginalAssetId = asset.AssetId,
            ProvenanceId = provenance.ProvenanceId,
            SubmissionId = request.SubmissionId,
            FlyerType = FlyerType.Unknown,
            Status = EvidenceStatus.Pending,
            OcrReady = true,
            IngestionJobId = jobId,
            LinkedWorkflowIds = MergeWorkflowIds([], jobId, request.SubmissionId),
            ProcessingHistory =
            [
                "intake:linked",
                $"ingestion:job:{jobId}:created"
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return await evidenceRepository.SaveAsync(created, ct);
    }

    private static string[] MergeWorkflowIds(string[] current, string jobId, string? submissionId)
    {
        return current
            .Concat([jobId])
            .Concat(string.IsNullOrWhiteSpace(submissionId) ? [] : [submissionId])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private CanonicalSourceEvidence[] BuildStageEvidence(
        string jobId,
        FlyerAssetReference asset,
        FlyerOcrExtractionResult ocr,
        FlyerEvidenceRecord evidence,
        ProvenanceRecord provenance)
    {
        var observedAt = DateTimeOffset.UtcNow;
        return
        [
            new CanonicalSourceEvidence(
                EvidenceId: $"flyer-asset-{asset.AssetId}",
                EvidenceKind: "flyer-asset",
                Reference: asset.AssetId,
                MimeType: asset.ContentType,
                PayloadSnippet: asset.StorageKey,
                ObservedAtUtc: observedAt,
                Confidence: 1.0,
                Metadata: asset.Metadata),
            new CanonicalSourceEvidence(
                EvidenceId: $"flyer-provenance-{provenance.ProvenanceId}",
                EvidenceKind: "flyer-provenance",
                Reference: provenance.ProvenanceId,
                MimeType: "application/json",
                PayloadSnippet: JsonSerializer.Serialize(new
                {
                    provenance.SourceTier,
                    provenance.UploadOrigin,
                    provenance.SourceType,
                    provenance.BaselineAuthority,
                }, JsonOptions),
                ObservedAtUtc: observedAt,
                Confidence: provenance.BaselineAuthority,
                Metadata: new Dictionary<string, string?>
                {
                    ["assetId"] = provenance.AssetId,
                    ["ingestionJobId"] = provenance.IngestionJobId,
                }),
            new CanonicalSourceEvidence(
                EvidenceId: $"flyer-ocr-{ocr.ExtractionId}",
                EvidenceKind: "flyer-ocr",
                Reference: ocr.ExtractionId,
                MimeType: "text/plain",
                PayloadSnippet: Truncate(ocr.RawText, 800),
                ObservedAtUtc: ocr.CompletedAtUtc,
                Confidence: ocr.Confidence,
                Metadata: new Dictionary<string, string?>
                {
                    ["jobId"] = jobId,
                    ["assetId"] = asset.AssetId,
                    ["engine"] = ocr.Engine,
                    ["engineVersion"] = ocr.EngineVersion,
                    ["ocrBlockCount"] = ocr.Blocks.Length.ToString(CultureInfo.InvariantCulture),
                    ["evidenceId"] = evidence.EvidenceId,
                }),
        ];
    }

    private static CanonicalSourceEvidence BuildNormalizationEvidence(
        FlyerNormalizationResult normalization,
        FlyerConfidenceVector confidence,
        FlyerAssetReference asset)
    {
        var runId = normalization.Candidate?.NormalizationRunId ?? Guid.NewGuid().ToString("N");
        var snapshot = JsonSerializer.Serialize(new
        {
            candidate = normalization.Candidate,
            confidence,
            normalization.ReviewTriggers,
            normalization.MissingFields,
            normalization.UnresolvedAmbiguities,
        }, JsonOptions);

        return new CanonicalSourceEvidence(
            EvidenceId: $"flyer-normalization-{runId}",
            EvidenceKind: "flyer-normalization",
            Reference: runId,
            MimeType: "application/json",
            PayloadSnippet: Truncate(snapshot, 1800),
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Confidence: confidence.Aggregate,
            Metadata: new Dictionary<string, string?>
            {
                ["assetId"] = asset.AssetId,
                ["normalizationVersion"] = normalization.Candidate?.NormalizationVersion,
                ["requiresManualReview"] = normalization.RequiresManualReview ? "true" : "false",
            });
    }

    private async Task UpdateEvidenceForFailureAsync(
        FlyerEvidenceRecord evidence,
        FlyerOcrExtractionResult ocr,
        IReadOnlyCollection<CanonicalIngestionIssue> issues,
        CancellationToken ct)
    {
        var updated = evidence with
        {
            OcrText = ocr.RawText,
            OcrExtractionId = ocr.ExtractionId,
            OcrEngineVersion = $"{ocr.Engine}@{ocr.EngineVersion}",
            OcrBlocksJson = JsonSerializer.Serialize(ocr.Blocks, JsonOptions),
            ValidationFailures = issues.Where(i => i.Severity == IngestionIssueSeverity.Error).Select(i => i.Message).Distinct().ToArray(),
            ProcessingHistory =
            [
                ..evidence.ProcessingHistory,
                $"ocr:failed:{ocr.ExtractionId}",
            ],
        };

        await evidenceRepository.UpdateAsync(updated, ct);
    }

    private async Task UpdateEvidenceForSuccessAsync(
        FlyerEvidenceRecord evidence,
        ProvenanceRecord provenance,
        FlyerOcrExtractionResult ocr,
        FlyerNormalizationResult normalization,
        FlyerConfidenceVector confidence,
        FlyerReviewTriggerReason[] reviewReasons,
        CancellationToken ct)
    {
        var snapshot = JsonSerializer.Serialize(new
        {
            candidate = normalization.Candidate,
            confidence,
            normalization.Issues,
            reviewReasons,
        }, JsonOptions);

        var updated = evidence with
        {
            OcrText = ocr.RawText,
            OcrExtractionId = ocr.ExtractionId,
            OcrEngineVersion = $"{ocr.Engine}@{ocr.EngineVersion}",
            OcrBlocksJson = JsonSerializer.Serialize(ocr.Blocks, JsonOptions),
            ConfidenceScore = confidence.Aggregate,
            NormalizationRunId = normalization.Candidate?.NormalizationRunId,
            NormalizationVersion = normalization.Candidate?.NormalizationVersion,
            NormalizationSnapshotJson = snapshot,
            ReviewReasons = reviewReasons.Select(r => r.ToString()).ToArray(),
            ProcessingHistory =
            [
                .. evidence.ProcessingHistory,
                $"ocr:succeeded:{ocr.ExtractionId}",
                $"normalize:succeeded:{normalization.Candidate?.NormalizationRunId ?? "none"}",
                reviewReasons.Length == 0 ? "review:auto-clear" : $"review:required:{string.Join(',', reviewReasons.Select(r => r.ToString()))}",
                $"authority:baseline:{provenance.BaselineAuthority:F2}",
            ],
            ValidationFailures = normalization.Issues.Where(i => i.Severity == IngestionIssueSeverity.Error).Select(i => i.Message).Distinct().ToArray(),
        };

        await evidenceRepository.UpdateAsync(updated, ct);
    }

    private static CanonicalIngestionIssue BuildIssue(
        string code,
        string message,
        IngestionIssueSeverity severity,
        bool retryable,
        string? field,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        return new CanonicalIngestionIssue(
            code,
            message,
            severity,
            retryable,
            field,
            metadata ?? new Dictionary<string, string?>());
    }

    private static FlyerReviewTriggerReason[] BuildConfidenceReviewReasons(FlyerConfidenceVector confidence)
    {
        var reasons = new List<FlyerReviewTriggerReason>();
        if (confidence.Extraction < 0.50)
        {
            reasons.Add(FlyerReviewTriggerReason.LowExtractionConfidence);
        }

        if (confidence.Temporal < 0.50)
        {
            reasons.Add(FlyerReviewTriggerReason.LowTemporalConfidence);
        }

        if (confidence.VenueMatch < 0.50)
        {
            reasons.Add(FlyerReviewTriggerReason.LowVenueMatchConfidence);
        }

        if (confidence.Geocode < 0.50)
        {
            reasons.Add(FlyerReviewTriggerReason.LowGeocodeConfidence);
        }

        if (confidence.Dedupe < 0.99)
        {
            reasons.Add(FlyerReviewTriggerReason.DedupePending);
        }

        return reasons.Distinct().ToArray();
    }

    private static FlyerAssetReference ToAssetReference(FlyerAssetRecord asset)
    {
        return new FlyerAssetReference(
            AssetId: asset.AssetId,
            StorageKey: asset.StorageKey,
            ContentType: asset.CanonicalContentType ?? asset.ContentType,
            OriginalFilename: asset.OriginalFilename,
            UploadedBy: asset.SubmitterId,
            UploadedAtUtc: asset.UploadedAt,
            Metadata: new Dictionary<string, string?>
            {
                ["sourceReference"] = asset.SourceReference,
                ["contentHash"] = asset.ContentHash,
                ["status"] = asset.Status.ToString(),
                ["widthPx"] = asset.WidthPx?.ToString(CultureInfo.InvariantCulture),
                ["heightPx"] = asset.HeightPx?.ToString(CultureInfo.InvariantCulture),
                ["localPath"] = asset.LocalPath,
                ["s3Url"] = asset.S3Url,
            });
    }

    private static IngestionResult Transition(
        IngestionResult current,
        IngestionJobStatus status,
        string? detail,
        CanonicalEventCandidate? candidate = null,
        CanonicalSourceEvidence[]? evidence = null,
        CanonicalIngestionIssue[]? issues = null)
    {
        var now = DateTimeOffset.UtcNow;
        return current with
        {
            Status = status,
            Candidate = candidate ?? current.Candidate,
            Evidence = evidence ?? current.Evidence,
            Issues = issues ?? current.Issues,
            Lifecycle = [.. current.Lifecycle, new IngestionStatusRecord(status, now, detail)],
            UpdatedAtUtc = now,
        };
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLen ? value : value[..maxLen];
    }

    private static FlyerOcrExtractionResult? BuildOcrResultFromEvidence(FlyerEvidenceRecord evidence, string jobId)
    {
        if (string.IsNullOrWhiteSpace(evidence.OcrExtractionId))
        {
            return null;
        }

        var blocks = ParseOcrBlocks(evidence.OcrBlocksJson);
        var confidence = evidence.ConfidenceScore ?? 0.0;
        var engineVersion = evidence.OcrEngineVersion ?? "stub-ocr@unknown";
        var split = engineVersion.Split('@', 2, StringSplitOptions.TrimEntries);
        var engine = split.Length == 2 ? split[0] : "stub-ocr";
        var version = split.Length == 2 ? split[1] : engineVersion;

        return new FlyerOcrExtractionResult(
            ExtractionId: evidence.OcrExtractionId,
            JobId: jobId,
            AssetId: evidence.AssetId,
            Engine: engine,
            EngineVersion: version,
            Confidence: confidence,
            Success: !string.IsNullOrWhiteSpace(evidence.OcrText),
            RawText: evidence.OcrText ?? string.Empty,
            Blocks: blocks,
            Issues: Array.Empty<CanonicalIngestionIssue>(),
            StartedAtUtc: evidence.CreatedAt,
            CompletedAtUtc: evidence.CreatedAt);
    }

    private static FlyerOcrTextBlock[] ParseOcrBlocks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<FlyerOcrTextBlock>();
        }

        try
        {
            return JsonSerializer.Deserialize<FlyerOcrTextBlock[]>(json, JsonOptions) ?? Array.Empty<FlyerOcrTextBlock>();
        }
        catch
        {
            return Array.Empty<FlyerOcrTextBlock>();
        }
    }

    private static FlyerNormalizedEventCandidate? ParseNormalizationSnapshot(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (!doc.RootElement.TryGetProperty("candidate", out var candidateElement) || candidateElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return candidateElement.Deserialize<FlyerNormalizedEventCandidate>(JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static FlyerConfidenceVector? ParseConfidence(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (!doc.RootElement.TryGetProperty("confidence", out var confidenceElement) || confidenceElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return confidenceElement.Deserialize<FlyerConfidenceVector>(JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static FlyerReviewTriggerReason[] ParseReviewReasons(string[] rawReasons)
    {
        return rawReasons
            .Select(reason => Enum.TryParse<FlyerReviewTriggerReason>(reason, true, out var parsed) ? parsed : (FlyerReviewTriggerReason?)null)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .Distinct()
            .ToArray();
    }

    private static FlyerConfidenceVector BuildFallbackConfidence(CanonicalEventCandidate? candidate, FlyerReviewTriggerReason[] reasons)
    {
        var extraction = candidate?.ExtractionConfidence ?? 0.0;
        var geocode = candidate?.GeocodeConfidence ?? 0.0;
        var temporal = candidate?.TemporalConfidence ?? 0.0;
        var venue = string.IsNullOrWhiteSpace(candidate?.VenueName) ? 0.3 : 0.7;
        var dedupe = reasons.Contains(FlyerReviewTriggerReason.DedupePending) ? 0.10 : 1.0;
        var source = 0.35;
        var review = 0.0;

        return new FlyerConfidenceVector(extraction, geocode, temporal, venue, dedupe, source, review);
    }
}

public sealed class HeuristicFlyerNormalizationService(FlyerNormalizationEngineAlias normalizationEngine) : IFlyerNormalizationService
{
    private const string Version = "heuristic-normalizer-v1.0";

    public Task<FlyerNormalizationResult> NormalizeAsync(FlyerNormalizationRequest request, CancellationToken ct = default)
    {
        var ocrResult = ToContractOcrResult(request.Ocr, request.Metadata);
        var extracted = normalizationEngine.Normalize(ocrResult);

        var titleCandidates = BuildCandidates(extracted.Title, GetScore(extracted, "title"), "ocr:title");
        var venueCandidates = BuildCandidates(extracted.Venue, GetScore(extracted, "venue"), "ocr:venue");
        var addressCandidates = BuildCandidates(extracted.Address, GetScore(extracted, "address"), "ocr:address");
        var dateCandidates = BuildCandidates(ToIso(extracted.StartUtc), GetScore(extracted, "startUtc"), "ocr:startUtc");
        var endCandidates = BuildCandidates(ToIso(extracted.EndUtc), GetScore(extracted, "endUtc"), "ocr:endUtc");
        var categoryCandidates = Array.Empty<FlyerFieldValueCandidate>();

        var notes = new List<string>();
        var missing = new List<string>();
        var ambiguities = new List<string>();
        var warnings = new List<FlyerExtractionWarning>();
        var reviewTriggers = new HashSet<FlyerReviewTriggerReason> { FlyerReviewTriggerReason.DedupePending };

        if (string.IsNullOrWhiteSpace(extracted.Title))
        {
            missing.Add("title");
            reviewTriggers.Add(FlyerReviewTriggerReason.MissingTitle);
            warnings.Add(new FlyerExtractionWarning("missing_title", "Could not determine flyer title.", "title", true, EmptyMetadata()));
        }

        if (string.IsNullOrWhiteSpace(extracted.Address))
        {
            missing.Add("address");
            reviewTriggers.Add(FlyerReviewTriggerReason.MissingAddress);
            warnings.Add(new FlyerExtractionWarning("missing_address", "No reliable address candidate found.", "address", true, EmptyMetadata()));
        }

        if (string.IsNullOrWhiteSpace(extracted.Venue))
        {
            missing.Add("venue");
            reviewTriggers.Add(FlyerReviewTriggerReason.MissingVenue);
            warnings.Add(new FlyerExtractionWarning("missing_venue", "Venue name missing or ambiguous.", "venue", true, EmptyMetadata()));
        }

        if (extracted.StartUtc is null)
        {
            missing.Add("startUtc");
            reviewTriggers.Add(FlyerReviewTriggerReason.MissingDate);
            warnings.Add(new FlyerExtractionWarning("missing_start_utc", "No explicit timezone datetime was found.", "startUtc", true, EmptyMetadata()));
        }

        if (GetRationale(extracted, "venue").Contains("ambiguous", StringComparison.OrdinalIgnoreCase)
            || GetRationale(extracted, "title").Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
        {
            ambiguities.Add("multiple_title_or_venue_candidates");
            reviewTriggers.Add(FlyerReviewTriggerReason.UnresolvedAmbiguity);
        }

        if (request.Ocr.Confidence < 0.55)
        {
            warnings.Add(new FlyerExtractionWarning("low_ocr_confidence", "OCR confidence below preferred threshold.", "ocr", false, EmptyMetadata()));
            reviewTriggers.Add(FlyerReviewTriggerReason.LowExtractionConfidence);
        }

        if (missing.Count > 0)
        {
            reviewTriggers.Add(FlyerReviewTriggerReason.PartialExtraction);
            reviewTriggers.Add(FlyerReviewTriggerReason.NormalizationIncomplete);
        }

        notes.Add("Deterministic normalization engine v1.0 (no fabricated fallback values).");
        notes.AddRange(extracted.FieldScores.Select(kvp => $"{kvp.Key}:{kvp.Value.Rationale}"));

        var extractionSignals = new[]
        {
            GetScore(extracted, "title"),
            GetScore(extracted, "venue"),
            GetScore(extracted, "address"),
            GetScore(extracted, "startUtc"),
        };
        var extraction = Math.Clamp(extractionSignals.Average(), 0.0, 1.0);
        var temporal = GetScore(extracted, "startUtc");
        var geocode = GetScore(extracted, "address");
        var venueMatch = GetScore(extracted, "venue");

        var confidence = new FlyerConfidenceVector(
            Extraction: extraction,
            Geocode: geocode,
            Temporal: temporal,
            VenueMatch: venueMatch,
            Dedupe: 0.10,
            SourceTrust: 0.35,
            ReviewConfidence: 0.0);

        var issueList = warnings
            .Select(w => new CanonicalIngestionIssue(
                w.Code,
                w.Message,
                w.Blocking ? IngestionIssueSeverity.Error : IngestionIssueSeverity.Warning,
                false,
                w.Field,
                w.Metadata))
            .ToArray();

        var normalizationRunId = Guid.NewGuid().ToString("N");
        var canonical = new NormalizedEventCandidate(
            Title: extracted.Title,
            VenueName: extracted.Venue,
            Address: extracted.Address,
            StartUtc: ToIso(extracted.StartUtc),
            EndUtc: ToIso(extracted.EndUtc),
            Timezone: null,
            Category: null,
            Description: null,
            Tags: extracted.Tags.Length == 0 ? null : extracted.Tags,
            SourceKind: "flyer_upload",
            SourceRef: request.Asset.AssetId,
            ExtractionConfidence: extraction,
            GeocodeConfidence: geocode,
            TemporalConfidence: temporal,
            EvidenceRefs:
            [
                $"flyer-ocr-{request.Ocr.ExtractionId}",
                $"flyer-asset-{request.Asset.AssetId}",
            ],
            ExternalSourceId: null,
            Attributes: BuildAttributes(extracted, normalizationRunId));

        var normalized = new FlyerNormalizedEventCandidate(
            CanonicalCandidate: canonical,
            TitleCandidates: titleCandidates,
            VenueCandidates: venueCandidates,
            StartDateTimeCandidates: dateCandidates,
            EndDateTimeCandidates: endCandidates,
            AddressCandidates: addressCandidates,
            CategoryCandidates: categoryCandidates,
            Tags: extracted.Tags,
            DescriptiveNotes: notes.ToArray(),
            MissingFields: missing.ToArray(),
            UnresolvedAmbiguities: ambiguities.ToArray(),
            FieldConfidence: new FlyerFieldConfidenceBreakdown(
                Title: GetScore(extracted, "title"),
                Venue: GetScore(extracted, "venue"),
                Address: GetScore(extracted, "address"),
                StartDateTime: GetScore(extracted, "startUtc"),
                EndDateTime: GetScore(extracted, "endUtc"),
                Category: 0.0,
                Description: 0.0,
                Tags: GetScore(extracted, "tags")),
            Warnings: warnings.ToArray(),
            ReviewTriggers: reviewTriggers.ToArray(),
            NormalizationRunId: normalizationRunId,
            NormalizationVersion: Version);

        var requiresReview = reviewTriggers.Count > 0;

        return Task.FromResult(new FlyerNormalizationResult(
            Candidate: normalized,
            Confidence: confidence,
            Issues: issueList,
            ReviewTriggers: reviewTriggers.ToArray(),
            RequiresManualReview: requiresReview,
            MissingFields: missing.ToArray(),
            UnresolvedAmbiguities: ambiguities.ToArray(),
            Notes: notes.ToArray()));
    }

    private static IReadOnlyDictionary<string, string?> EmptyMetadata() => new Dictionary<string, string?>();

    private static OcrResult ToContractOcrResult(FlyerOcrExtractionResult ocr, IReadOnlyDictionary<string, string?> metadata)
    {
        return new OcrResult(
            ExtractionId: ocr.ExtractionId,
            JobId: ocr.JobId,
            AssetId: ocr.AssetId,
            Provider: ocr.Engine,
            ProviderVersion: ocr.EngineVersion,
            Confidence: ocr.Confidence,
            Success: ocr.Success,
            RawText: ocr.RawText,
            Blocks: ocr.Blocks.Select(block => new WeUP.Contracts.Ocr.OcrTextBlock(
                Index: block.Index,
                Text: block.Text,
                Confidence: block.Confidence,
                X: block.X,
                Y: block.Y,
                Width: block.Width,
                Height: block.Height,
                Metadata: block.Metadata)).ToArray(),
            FailureReason: ocr.Issues.FirstOrDefault(i => i.Field == "ocr")?.Message,
            AttemptCount: 1,
            StartedAtUtc: ocr.StartedAtUtc,
            CompletedAtUtc: ocr.CompletedAtUtc,
            Metadata: metadata);
    }

    private static string? ToIso(DateTimeOffset? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);

    private static double GetScore(EventCandidate candidate, string field)
        => candidate.FieldScores.TryGetValue(field, out var score) ? Math.Clamp(score.Confidence, 0.0, 1.0) : 0.0;

    private static string GetRationale(EventCandidate candidate, string field)
        => candidate.FieldScores.TryGetValue(field, out var score)
            ? score.Rationale
            : string.Empty;

    private static FlyerFieldValueCandidate[] BuildCandidates(string? value, double confidence, string evidenceRef)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return
        [
            new FlyerFieldValueCandidate(
                Value: value,
                Confidence: Math.Clamp(confidence, 0.0, 1.0),
                EvidenceRefs: [evidenceRef],
                IsSelected: true)
        ];
    }

    private static IReadOnlyDictionary<string, string?> BuildAttributes(EventCandidate candidate, string normalizationRunId)
    {
        return new Dictionary<string, string?>
        {
            ["normalizationRunId"] = normalizationRunId,
            ["normalizationVersion"] = Version,
            ["rawFieldsJson"] = JsonSerializer.Serialize(candidate.RawFields),
            ["fieldScoresJson"] = JsonSerializer.Serialize(candidate.FieldScores),
        };
    }
}

public sealed class FlyerConfidenceEvaluator : IFlyerConfidenceEvaluator
{
    public FlyerConfidenceVector Evaluate(
        FlyerAssetReference asset,
        FlyerOcrExtractionResult ocr,
        FlyerNormalizedEventCandidate? candidate,
        double sourceAuthority,
        IReadOnlyCollection<FlyerReviewTriggerReason> reviewTriggers)
    {
        var extraction = candidate?.CanonicalCandidate.ExtractionConfidence ?? ocr.Confidence;
        var geocode = candidate?.CanonicalCandidate.GeocodeConfidence ?? 0.0;
        var temporal = candidate?.CanonicalCandidate.TemporalConfidence ?? 0.0;

        var venueMatch = candidate?.FieldConfidence.Venue
            ?? (string.IsNullOrWhiteSpace(candidate?.CanonicalCandidate.VenueName) ? 0.20 : 0.70);

        var dedupe = reviewTriggers.Contains(FlyerReviewTriggerReason.DedupePending) ? 0.10 : 1.0;
        var sourceTrust = Math.Clamp(sourceAuthority, 0.0, 1.0);

        return new FlyerConfidenceVector(
            Extraction: Math.Clamp(extraction, 0.0, 1.0),
            Geocode: Math.Clamp(geocode, 0.0, 1.0),
            Temporal: Math.Clamp(temporal, 0.0, 1.0),
            VenueMatch: Math.Clamp(venueMatch, 0.0, 1.0),
            Dedupe: dedupe,
            SourceTrust: sourceTrust,
            ReviewConfidence: 0.0);
    }
}
