using Microsoft.Extensions.Logging;
using WeUP.Contracts.Dedupe;
using WeUP.Contracts.Ingestion;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Events;
using ContractDuplicateAssessment = WeUP.Contracts.Dedupe.DuplicateAssessment;
using ContractDuplicateAssessmentLevel = WeUP.Contracts.Dedupe.DuplicateAssessmentLevel;
using ContractMatchDimension = WeUP.Contracts.Dedupe.MatchDimension;
using ContractMatchScoreBreakdown = WeUP.Contracts.Dedupe.MatchScoreBreakdown;
using DomainDuplicateAssessment = WeUP.Domain.Dedupe.DuplicateAssessment;
using DomainDuplicateAssessmentLevel = WeUP.Domain.Dedupe.DuplicateAssessmentLevel;
using DomainMatchScoreBreakdown = WeUP.Domain.Dedupe.MatchScoreBreakdown;

namespace WeUP.Application.Dedupe;

/// <summary>
/// Deterministic weighted deduplication service that evaluates one candidate against
/// multiple canonical events and returns the highest-confidence assessment.
/// </summary>
public sealed class WeightedDeduplicationService(
    IDeduplicationStrategy strategy,
    ILogger<WeightedDeduplicationService> logger) : IDeduplicationService
{
    private readonly IDeduplicationStrategy _strategy = strategy;
    private readonly ILogger<WeightedDeduplicationService> _logger = logger;

    public Task<ContractDuplicateAssessment> AssessAsync(
        CandidateEvent candidate,
        IReadOnlyCollection<EventAggregate> canonicalEvents)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(canonicalEvents);

        if (canonicalEvents.Count == 0)
        {
            return Task.FromResult(new ContractDuplicateAssessment(
                CandidateId: candidate.CandidateId,
                CanonicalEventId: Guid.Empty,
                Level: ContractDuplicateAssessmentLevel.Distinct,
                Scores: [],
                Explanation: "No existing canonical events available for comparison."));
        }

        var normalizedCandidate = ToNormalizedCandidate(candidate);

        var best = canonicalEvents
            .Select(ToSnapshot)
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                Assessment = _strategy.Assess(normalizedCandidate, snapshot),
            })
            .OrderByDescending(entry => entry.Assessment.Breakdown.CompositeScore)
            .ThenBy(entry => entry.Snapshot.CanonicalEventId, StringComparer.Ordinal)
            .First();

        var mapped = MapAssessment(candidate.CandidateId, best.Snapshot.CanonicalEventId, best.Assessment);

        _logger.LogInformation(
            "Deduplication assessment for Candidate {CandidateId} against Canonical {CanonicalEventId}: {Level}",
            mapped.CandidateId,
            mapped.CanonicalEventId,
            mapped.Level);

        return Task.FromResult(mapped);
    }

    public Task<DedupeResult> EvaluateCandidateAsync(
        NormalizedEventCandidate candidate,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use AssessAsync(CandidateEvent, IReadOnlyCollection<EventAggregate>) for canonical scoring.");

    private static ContractDuplicateAssessment MapAssessment(
        Guid candidateId,
        string canonicalEventId,
        DomainDuplicateAssessment assessment)
    {
        var level = MapLevel(assessment.Level, assessment.SafetyVerdict.ActiveBlockers.Length > 0);
        var scores = ToScoreBreakdown(assessment.Breakdown);

        var explanation = string.Join("; ",
        [
            $"CompositeScore={assessment.Breakdown.CompositeScore:F3}",
            $"Level={assessment.Level}",
            $"AutoMergeAllowed={assessment.AutoMergeAllowed}",
            $"Blockers={assessment.SafetyVerdict.ActiveBlockers.Length}",
        ]);

        return new ContractDuplicateAssessment(
            CandidateId: candidateId,
            CanonicalEventId: ToDeterministicGuid(canonicalEventId),
            Level: level,
            Scores: scores,
            Explanation: explanation);
    }

    private static ContractDuplicateAssessmentLevel MapLevel(
        DomainDuplicateAssessmentLevel level,
        bool hasSafetyBlockers)
    {
        if (hasSafetyBlockers && level != DomainDuplicateAssessmentLevel.Distinct)
        {
            return ContractDuplicateAssessmentLevel.Conflict;
        }

        return level switch
        {
            DomainDuplicateAssessmentLevel.ExactDuplicate => ContractDuplicateAssessmentLevel.ExactDuplicate,
            DomainDuplicateAssessmentLevel.ProbableDuplicate => ContractDuplicateAssessmentLevel.ProbableDuplicate,
            DomainDuplicateAssessmentLevel.PossibleDuplicate => ContractDuplicateAssessmentLevel.PossibleDuplicate,
            _ => ContractDuplicateAssessmentLevel.Distinct,
        };
    }

    private static ContractMatchScoreBreakdown[] ToScoreBreakdown(DomainMatchScoreBreakdown breakdown)
    {
        var locationRaw = Math.Max(breakdown.AddressScore.RawScore, breakdown.GeoScore.RawScore);

        return
        [
            new ContractMatchScoreBreakdown(ContractMatchDimension.Title, (float)breakdown.TitleScore.RawScore),
            new ContractMatchScoreBreakdown(ContractMatchDimension.Venue, (float)breakdown.VenueScore.RawScore),
            new ContractMatchScoreBreakdown(ContractMatchDimension.StartTime, (float)breakdown.TemporalScore.RawScore),
            new ContractMatchScoreBreakdown(ContractMatchDimension.Location, (float)locationRaw),
            new ContractMatchScoreBreakdown(ContractMatchDimension.SourceHash, (float)breakdown.SourceHashScore.RawScore),
        ];
    }

    private static NormalizedEventCandidate ToNormalizedCandidate(CandidateEvent candidate)
    {
        var attributes = BuildAttributes(candidate.RawFields);

        return new NormalizedEventCandidate(
            Title: candidate.Title,
            VenueName: ReadRawField(candidate.RawFields, "VenueName", "venue", "locationName"),
            Address: candidate.RawLocationText,
            StartUtc: candidate.InferredStartUtc?.ToString("O"),
            EndUtc: null,
            Timezone: ReadRawField(candidate.RawFields, "Timezone", "timeZone") ?? "UTC",
            Category: ReadRawField(candidate.RawFields, "Category"),
            Description: ReadRawField(candidate.RawFields, "Description"),
            Tags: ReadRawField(candidate.RawFields, "Tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SourceKind: ReadRawField(candidate.RawFields, "SourceKind") ?? "candidate",
            SourceRef: ReadRawField(candidate.RawFields, "SourceRef") ?? candidate.RequestId.ToString("N"),
            ExtractionConfidence: candidate.OverallExtractionConfidence,
            GeocodeConfidence: candidate.OverallExtractionConfidence,
            TemporalConfidence: candidate.OverallExtractionConfidence,
            EvidenceRefs: ReadRawField(candidate.RawFields, "EvidenceRefs")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ExternalSourceId: ReadRawField(candidate.RawFields, "ExternalSourceId"),
            Attributes: attributes);
    }

    private static EventAggregateSnapshot ToSnapshot(EventAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        return new EventAggregateSnapshot(
            CanonicalEventId: aggregate.CanonicalEventId,
            Title: aggregate.Title,
            VenueName: aggregate.VenueName,
            Address: aggregate.Address.RawAddress,
            Latitude: aggregate.Latitude,
            Longitude: aggregate.Longitude,
            StartUtc: aggregate.StartUtc.ToString("O"),
            EndUtc: aggregate.EndUtc?.ToString("O"),
            Timezone: aggregate.TimeZone,
            Category: aggregate.Category,
            Confidence: aggregate.ConfidenceScore,
            SourceRefs: aggregate.SourceEventIds,
            EvidenceRefs: aggregate.Provenance.EvidenceRefs,
            IsApproved: aggregate.EventStatus is EventLifecycleStatus.Approved or EventLifecycleStatus.Published,
            ExternalSourceId: aggregate.ExternalReferences.FirstOrDefault()?.ReferenceId,
            Attributes: null);
    }

    private static Guid ToDeterministicGuid(string canonicalEventId)
    {
        if (Guid.TryParse(canonicalEventId, out var parsed))
        {
            return parsed;
        }

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonicalEventId));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    private static IReadOnlyDictionary<string, string?>? BuildAttributes(IReadOnlyDictionary<string, string> rawFields)
    {
        if (rawFields.Count == 0)
        {
            return null;
        }

        var output = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var latitude = ReadRawField(rawFields, "Latitude", "Lat");
        var longitude = ReadRawField(rawFields, "Longitude", "Lng", "Lon");

        if (!string.IsNullOrWhiteSpace(latitude))
        {
            output["latitude"] = latitude;
        }

        if (!string.IsNullOrWhiteSpace(longitude))
        {
            output["longitude"] = longitude;
        }

        return output.Count == 0 ? null : output;
    }

    private static string? ReadRawField(IReadOnlyDictionary<string, string> rawFields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (rawFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var match = rawFields.FirstOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}