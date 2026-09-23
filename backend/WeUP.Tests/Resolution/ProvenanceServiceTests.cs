using WeUP.Application.Resolution;
using WeUP.Contracts.Events;
using WeUP.Contracts.Ingestion;
using WeUP.Contracts.Resolution;
using WeUP.Domain.Dedupe;
using WeUP.Domain.Resolution;
using Xunit;

namespace WeUP.Tests.Resolution;

public sealed class ProvenanceServiceTests
{
    private readonly ProvenanceService _service = new();

    [Fact]
    public void CreateAppendOnlyEntry_PreservesOriginalSourceAcrossMultipleMerges()
    {
        var existingEntry = new ProvenanceEntry(
            EntryId: "event-1:prov:0001",
            CanonicalEventId: "event-1",
            SequenceNumber: 1,
            PreviousEntryId: null,
            ResolutionId: "resolution-1",
            SourceRequestIds: ["request-1"],
            CandidateIds: ["candidate-1"],
            EvidenceBundleRefs: ["bundle-1"],
            SourceRefs: ["seed:event-1", "candidate-1"],
            EvidenceRefs: ["evidence-1"],
            FieldLineage:
            [
                new FieldLineage(
                    FieldName: "Title",
                    PriorValue: "Original Title",
                    CurrentValue: "Second Title",
                    OriginalSourceRef: "seed:event-1",
                    OriginalConfidence: 0.81,
                    CurrentSourceRef: "candidate-1",
                    CurrentConfidence: 0.88,
                    EvidenceRefs: ["evidence-1"],
                    EvidenceBundleRefs: ["bundle-1"],
                    ChangedAtUtc: DateTimeOffset.Parse("2026-04-17T10:00:00Z"),
                    Reason: "Title changed from prior canonical value to merged candidate value.")
            ],
            MergeHistory: new MergeHistoryEntry(
                MergeId: "resolution-1",
                SourceRequestIds: ["request-1"],
                CandidateIds: ["candidate-1"],
                EvidenceBundleRefs: ["bundle-1"],
                ChangedFields: ["Title"],
                MergedAtUtc: DateTimeOffset.Parse("2026-04-17T10:00:00Z"),
                MergeActor: "system:entity-resolution",
                MergeReason: "Initial merge."),
            RecordedAtUtc: DateTimeOffset.Parse("2026-04-17T10:00:00Z"));

        var entry = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-2",
            beforeTitle: "Second Title",
            afterTitle: "Third Title",
            candidateSourceRef: "candidate-2",
            existingEntries: [existingEntry]));

        var titleLineage = Assert.Single(entry.FieldLineage.Where(item => item.FieldName == "Title"));
        Assert.Equal("seed:event-1", titleLineage.OriginalSourceRef);
        Assert.Equal(0.81, titleLineage.OriginalConfidence);
        Assert.Equal("Second Title", titleLineage.PriorValue);
        Assert.Equal("Third Title", titleLineage.CurrentValue);
        Assert.Equal("candidate-2", titleLineage.CurrentSourceRef);
        Assert.Equal(2, entry.SequenceNumber);
        Assert.Equal(existingEntry.EntryId, entry.PreviousEntryId);
    }

    [Fact]
    public void AppendAndQueryHistory_ReturnEntriesInChronologicalOrder()
    {
        var first = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-1",
            beforeTitle: "Title A",
            afterTitle: "Title B",
            candidateSourceRef: "candidate-1",
            mergedAtUtc: DateTimeOffset.Parse("2026-04-17T10:00:00Z")));

        var entries = _service.Append([], first);

        var second = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-2",
            beforeTitle: "Title B",
            afterTitle: "Title C",
            candidateSourceRef: "candidate-2",
            mergedAtUtc: DateTimeOffset.Parse("2026-04-17T11:00:00Z"),
            existingEntries: entries));

        entries = _service.Append(entries, second);

        var lineage = _service.GetFieldLineage(entries, "Title");
        var history = _service.GetMergeHistory(entries);

        Assert.Equal(new string?[] { "Title B", "Title C" }, lineage.Select(item => item.CurrentValue).ToArray());
        Assert.Equal(new[] { "resolution-1", "resolution-2" }, history.Select(item => item.MergeId).ToArray());
        Assert.Equal(new[] { 1, 2 }, entries.Select(item => item.SequenceNumber).ToArray());
    }

    [Fact]
    public void CreateAppendOnlyEntry_CapturesRequestCandidateAndEvidenceReferences()
    {
        var entry = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-3",
            beforeTitle: "Title B",
            afterTitle: "Title D",
            candidateSourceRef: "candidate-3",
            sourceRequestIds: ["request-17", "request-18"],
            candidateIds: ["candidate-3", "candidate-shadow"],
            evidenceBundleRefs: ["bundle-3"],
            evidenceRefs: ["evidence-3", "evidence-4"]));

        Assert.Equal(new[] { "request-17", "request-18" }, entry.SourceRequestIds);
        Assert.Equal(new[] { "candidate-3", "candidate-shadow" }, entry.CandidateIds);
        Assert.Equal(new[] { "bundle-3" }, entry.EvidenceBundleRefs);
        Assert.Contains("evidence-3", entry.EvidenceRefs);
        Assert.Contains("Title", entry.MergeHistory.ChangedFields);
    }

    [Fact]
    public void GetEvolutionHistory_ProjectsStructuredMergeEvolution()
    {
        var first = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-evo-1",
            beforeTitle: "Title A",
            afterTitle: "Title B",
            candidateSourceRef: "candidate-evo-1",
            mergedAtUtc: DateTimeOffset.Parse("2026-04-17T10:00:00Z")));

        var second = _service.CreateAppendOnlyEntry(CreateCommand(
            resolutionId: "resolution-evo-2",
            beforeTitle: "Title B",
            afterTitle: "Title C",
            candidateSourceRef: "candidate-evo-2",
            mergedAtUtc: DateTimeOffset.Parse("2026-04-17T11:00:00Z"),
            existingEntries: [first]));

        var entries = _service.Append([first], second);
        var evolution = _service.GetEvolutionHistory(entries, "event-1");

        Assert.Equal(2, evolution.Length);
        Assert.All(evolution, item => Assert.Equal("event-1", item.CanonicalEventId));
        Assert.Equal("resolution-evo-1", evolution[0].MergeId);
        Assert.Equal("resolution-evo-2", evolution[1].MergeId);
        Assert.All(evolution, item => Assert.Equal("TitleUpdate", item.EvolutionType));
        Assert.All(evolution, item => Assert.Contains("Title", item.ChangedFields));
    }

    private static ProvenanceBuildCommand CreateCommand(
        string resolutionId,
        string beforeTitle,
        string afterTitle,
        string candidateSourceRef,
        DateTimeOffset? mergedAtUtc = null,
        ProvenanceEntry[]? existingEntries = null,
        string[]? sourceRequestIds = null,
        string[]? candidateIds = null,
        string[]? evidenceBundleRefs = null,
        string[]? evidenceRefs = null)
    {
        var beforeFields = CreateFields(beforeTitle);
        var afterFields = CreateFields(afterTitle);
        var sourceRefs = new[] { "seed:event-1", candidateSourceRef };
        var evidence = evidenceRefs ?? ["evidence-1"];

        return new ProvenanceBuildCommand(
            ResolutionId: resolutionId,
            CanonicalBeforeMerge: CreateSnapshot("event-1", beforeTitle, sourceRefs, evidence),
            CanonicalAfterMerge: CreateSnapshot("event-1", afterTitle, sourceRefs, evidence),
            CanonicalBeforeFields: beforeFields,
            CanonicalAfterFields: afterFields,
            Candidate: new NormalizedEventCandidate(
                Title: afterTitle,
                VenueName: "Venue 1",
                Address: "123 Main St",
                StartUtc: "2026-04-17T20:00:00Z",
                EndUtc: null,
                Timezone: "America/Chicago",
                Category: "music",
                Description: null,
                Tags: ["live"],
                SourceKind: "test",
                SourceRef: candidateSourceRef,
                ExtractionConfidence: 0.93,
                GeocodeConfidence: 0.77,
                TemporalConfidence: 0.86,
                EvidenceRefs: evidence,
                ExternalSourceId: candidateSourceRef,
                Attributes: new Dictionary<string, string?>
                {
                    ["requestId"] = sourceRequestIds?.FirstOrDefault() ?? resolutionId,
                    ["evidenceBundleId"] = evidenceBundleRefs?.FirstOrDefault(),
                }),
            Plan: new MergePlan(
                CanonicalEventId: "event-1",
                Title: afterTitle,
                VenueName: "Venue 1",
                Address: "123 Main St",
                StartUtc: "2026-04-17T20:00:00Z",
                EndUtc: null,
                Timezone: "America/Chicago",
                Category: "music",
                Description: null,
                Tags: ["live"],
                MergedConfidence: 0.93,
                AutoMergeAllowed: true,
                MergeRationale: ["Higher-confidence title selected."],
                ManualReviewReasons: [],
                Conflicts: [],
                UnionedSourceRefs: sourceRefs,
                UnionedEvidenceRefs: evidence,
                PreservedReviewRefs: []),
            ExistingEntries: existingEntries ?? [],
            SourceRequestIds: sourceRequestIds ?? [resolutionId],
            CandidateIds: candidateIds ?? [candidateSourceRef],
            EvidenceBundleRefs: evidenceBundleRefs ?? [],
            MergeActor: "system:entity-resolution",
            MergeReason: "Automated duplicate merge.",
            MergedAtUtc: mergedAtUtc ?? DateTimeOffset.Parse("2026-04-17T10:00:00Z"));
    }

    private static EventAggregateSnapshot CreateSnapshot(string canonicalEventId, string title, string[] sourceRefs, string[] evidenceRefs)
        => new(
            CanonicalEventId: canonicalEventId,
            Title: title,
            VenueName: "Venue 1",
            Address: "123 Main St",
            Latitude: 29.7604,
            Longitude: -95.3698,
            StartUtc: "2026-04-17T20:00:00Z",
            EndUtc: null,
            Timezone: "America/Chicago",
            Category: "music",
            Confidence: 0.81,
            SourceRefs: sourceRefs,
            EvidenceRefs: evidenceRefs,
            IsApproved: false,
            ExternalSourceId: canonicalEventId);

    private static IReadOnlyDictionary<string, string?> CreateFields(string title)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = title,
            ["VenueName"] = "Venue 1",
            ["Address"] = "123 Main St",
            ["StartUtc"] = "2026-04-17T20:00:00Z",
            ["EndUtc"] = null,
            ["Timezone"] = "America/Chicago",
            ["Category"] = "music",
            ["Description"] = null,
            ["Tags"] = "live",
        };
}