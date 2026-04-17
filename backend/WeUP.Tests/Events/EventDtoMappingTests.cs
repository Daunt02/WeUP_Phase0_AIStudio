using System.Reflection;
using System.Text.Json;
using WeUP.Application.Events;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using Xunit;

namespace WeUP.Tests.Events;

/// <summary>
/// M4-P17: Contract and parity tests for the EventDtoMapper boundary.
///
/// Coverage:
///  1. All 5 mapper methods produce correct output from canonical input.
///  2. Internal-only fields (provenance, source event IDs, external refs) are NOT
///     present on any frontend-safe DTO type.
///  3. Lifecycle status serialization is deterministic.
///  4. Null and optional field handling is correct.
///  5. Canonical event identity (id) is consistent across MapCard, CalendarCard, Detail.
/// </summary>
public sealed class EventDtoMappingTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static EventAggregate BuildAggregate(
        string id = "evt-test-001",
        EventLifecycleStatus lifecycleStatus = EventLifecycleStatus.Published,
        EventPublishStatus publishStatus = EventPublishStatus.Published,
        EventModerationStatus moderationStatus = EventModerationStatus.Approved,
        EventRiskLevel riskLevel = EventRiskLevel.Low,
        double confidence = 0.92,
        int version = 3,
        string? parentId = null,
        string[]? mergedIds = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventAggregate(
            CanonicalEventId: id,
            SourceEventIds: [id, "legacy-src-99"],  // internal — must NOT appear in DTOs
            ExternalReferences: [new ExternalEventReference("seed", "manual", id)],  // internal
            Title: "Test Event Title",
            Description: "A test description.",
            Tags: ["jazz", "rooftop"],
            Category: "nightlife",
            VenueName: "The Test Venue",
            Address: new EventAddress(
                AddressLine1: "123 Test St",
                City: "Houston",
                State: "TX",
                PostalCode: "77001",
                Country: "US",
                RawAddress: "123 Test St, Houston TX 77001 US"),  // raw — must NOT appear in detail DTO
            Latitude: 29.760,
            Longitude: -95.369,
            TimeZone: "America/Chicago",
            StartUtc: now.AddDays(2),
            EndUtc: now.AddDays(2).AddHours(4),
            LocalStartDisplay: null,
            LocalEndDisplay: null,
            EventStatus: lifecycleStatus,
            PublishStatus: publishStatus,
            ModerationStatus: moderationStatus,
            RiskLevel: riskLevel,
            ConfidenceScore: confidence,
            Provenance: new EventProvenanceMetadata(
                PrimarySourceKind: "manual_submission",
                PrimarySourceRef: "sub-001",
                EvidenceRefs: ["evidence-a", "evidence-b"],  // internal — must NOT appear in any public DTO
                FirstObservedAtUtc: now.AddDays(-1),
                LastObservedAtUtc: now,
                SourceRefs: ["source-ref-x", "source-ref-y"]),  // internal
            CreatedAtUtc: now.AddDays(-1),
            UpdatedAtUtc: now,
            Version: version,
            MergeLineage: new EventMergeLineage(
                ParentCanonicalEventId: parentId,
                MergedCanonicalEventIds: mergedIds ?? [],
                AppliedMergePlanIds: ["plan-z"],  // internal
                LastMergedAtUtc: parentId != null ? now.AddDays(-1) : null,
                LastMergedBy: parentId != null ? "moderator-01" : null));
    }

    private static PublishEligibilityResult BuildEligibilityResult(
        bool eligible = true,
        PublishEligibilityBand band = PublishEligibilityBand.AutoPublishable)
    {
        var decision = new ConfidenceGateDecision(
            Band: band,
            Aggregate: 0.88,
            MeetsAutoPublishThreshold: eligible,
            RequiresManualReview: !eligible,
            IsBlocked: false,
            DimensionScores: new Dictionary<string, double>
            {
                ["extraction"] = 0.90,
                ["geocode"] = 0.85,
                ["temporal"] = 0.95,
                ["venueMatch"] = 0.80,
                ["dupeRisk"] = 0.92,
            },
            Explanations: ["All checks passed."]);

        return new PublishEligibilityResult(
            Eligible: eligible,
            EligibilityBand: band,
            RecommendedNextAction: eligible ? PublishRecommendedAction.AutoPublish : PublishRecommendedAction.RouteToManualReview,
            Blockers: [],
            ConfidenceSummary: decision,
            FieldCompletenessSummary: new FieldCompletenessResult(
                IsComplete: true,
                MissingRequiredFields: [],
                InvalidRequiredFields: [],
                CompletenessRatio: 1.0,
                RequiredCount: 8,
                SatisfiedCount: 8),
            Policy: new AutoPublishPolicy(
                PolicyId: "default",
                Description: "Default policy",
                AutoPublishThreshold: 0.80,
                ManualReviewThreshold: 0.55,
                BlockPublishThreshold: 0.40,
                MinimumDimensionThreshold: 0.20,
                DimensionMinimums: new Dictionary<string, double>(),
                DimensionWeights: new Dictionary<string, double>()),
            Notes: []);
    }

    // -----------------------------------------------------------------------
    // 1. MapCard mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void ToMapCard_ReturnsCorrectFields()
    {
        var aggregate = BuildAggregate();
        var dto = EventDtoMapper.ToMapCard(aggregate, thumbnailUrl: "https://cdn.test/img.jpg");

        Assert.Equal("evt-test-001", dto.Id);
        Assert.Equal("Test Event Title", dto.Title);
        Assert.Equal("The Test Venue", dto.VenueName);
        Assert.Equal("nightlife", dto.Category);
        Assert.Equal(29.760, dto.Lat);
        Assert.Equal(-95.369, dto.Lng);
        Assert.Equal("https://cdn.test/img.jpg", dto.ThumbnailUrl);
        Assert.Equal("PUBLISHED", dto.Status);
        Assert.Equal(0.92, dto.Confidence);
    }

    [Fact]
    public void ToMapCard_NullThumbnailIsAllowed()
    {
        var aggregate = BuildAggregate();
        var dto = EventDtoMapper.ToMapCard(aggregate);

        Assert.Null(dto.ThumbnailUrl);
    }

    [Fact]
    public void ToMapCard_DoesNotExposeInternalFields()
    {
        var dtoProperties = typeof(EventMapCardDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Internal-only fields that must NOT appear on the map card DTO
        Assert.DoesNotContain("Provenance", dtoProperties);
        Assert.DoesNotContain("SourceEventIds", dtoProperties);
        Assert.DoesNotContain("ExternalReferences", dtoProperties);
        Assert.DoesNotContain("MergeLineage", dtoProperties);
        Assert.DoesNotContain("Description", dtoProperties);
        Assert.DoesNotContain("Tags", dtoProperties);
        Assert.DoesNotContain("RawAddress", dtoProperties);
    }

    // -----------------------------------------------------------------------
    // 2. CalendarCard mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void ToCalendarCard_ReturnsCorrectFields()
    {
        var aggregate = BuildAggregate();
        var dto = EventDtoMapper.ToCalendarCard(aggregate);

        Assert.Equal("evt-test-001", dto.Id);
        Assert.Equal("The Test Venue", dto.VenueName);
        Assert.Equal("America/Chicago", dto.Timezone);
        Assert.NotEqual(default, dto.StartUtc);
        Assert.NotNull(dto.EndUtc);
        Assert.Equal("PUBLISHED", dto.Status);
    }

    [Fact]
    public void ToCalendarCard_SameIdAsMapCard()
    {
        var aggregate = BuildAggregate();
        var mapCard = EventDtoMapper.ToMapCard(aggregate);
        var calCard = EventDtoMapper.ToCalendarCard(aggregate);

        // Canonical identity must be consistent across DTO shapes
        Assert.Equal(mapCard.Id, calCard.Id);
    }

    [Fact]
    public void ToCalendarCard_DoesNotExposeInternalFields()
    {
        var dtoProperties = typeof(EventCalendarCardDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Provenance", dtoProperties);
        Assert.DoesNotContain("SourceEventIds", dtoProperties);
        Assert.DoesNotContain("ConfidenceScore", dtoProperties);
        Assert.DoesNotContain("ModerationStatus", dtoProperties);
        Assert.DoesNotContain("RiskLevel", dtoProperties);
    }

    // -----------------------------------------------------------------------
    // 3. Detail mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDetail_ReturnsCorrectFields()
    {
        var aggregate = BuildAggregate();
        var media = new[] { new MediaRefDto("https://cdn.test/img.jpg", "image") };
        var dto = EventDtoMapper.ToDetail(aggregate, media, "manual submission");

        Assert.Equal("evt-test-001", dto.Id);
        Assert.Equal("Test Event Title", dto.Title);
        Assert.Equal("A test description.", dto.Description);
        Assert.Equal("The Test Venue", dto.VenueName);
        Assert.Equal("123 Test St, Houston, TX", dto.Address);  // formatted, not raw
        Assert.Equal(29.760, dto.Lat);
        Assert.Equal(-95.369, dto.Lng);
        Assert.Equal("nightlife", dto.Category);
        Assert.Equal("America/Chicago", dto.Timezone);
        Assert.Equivalent(new[] { "jazz", "rooftop" }, dto.Tags);
        Assert.Equal("PUBLISHED", dto.Status);
        Assert.Equal(0.92, dto.Confidence);
        Assert.Equal("manual submission", dto.SourceKind);
        Assert.Equal(3, dto.Version);
        Assert.NotNull(dto.ConcurrencyToken);
        Assert.Single(dto.MediaRefs);
        Assert.Equal("https://cdn.test/img.jpg", dto.MediaRefs[0].Url);
    }

    [Fact]
    public void ToDetail_SameIdAsMapCard()
    {
        var aggregate = BuildAggregate();
        var mapCard = EventDtoMapper.ToMapCard(aggregate);
        var detail = EventDtoMapper.ToDetail(aggregate, [], "test");

        Assert.Equal(mapCard.Id, detail.Id);
    }

    [Fact]
    public void ToDetail_AddressFormatExcludesRawAddress()
    {
        var aggregate = BuildAggregate();
        var dto = EventDtoMapper.ToDetail(aggregate, [], "test");

        // RawAddress ("123 Test St, Houston TX 77001 US") must NOT appear verbatim
        Assert.NotEqual(aggregate.Address.RawAddress, dto.Address);
        Assert.Contains("Houston", dto.Address);
        Assert.Contains("TX", dto.Address);
    }

    [Fact]
    public void ToDetail_DoesNotExposeInternalFields()
    {
        var dtoProperties = typeof(EventDetailDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Provenance", dtoProperties);
        Assert.DoesNotContain("SourceEventIds", dtoProperties);
        Assert.DoesNotContain("ExternalReferences", dtoProperties);
        Assert.DoesNotContain("MergeLineage", dtoProperties);
        Assert.DoesNotContain("ModerationStatus", dtoProperties);
        Assert.DoesNotContain("RiskLevel", dtoProperties);
        Assert.DoesNotContain("RawAddress", dtoProperties);
    }

    // -----------------------------------------------------------------------
    // 4. Moderation mapping (OPERATIONAL)
    // -----------------------------------------------------------------------

    [Fact]
    public void ToModeration_ReturnsCorrectOperationalFields()
    {
        var aggregate = BuildAggregate(
            lifecycleStatus: EventLifecycleStatus.Reviewed,
            moderationStatus: EventModerationStatus.InReview,
            publishStatus: EventPublishStatus.EligibilityPending,
            riskLevel: EventRiskLevel.Medium);

        var dto = EventDtoMapper.ToModeration(aggregate);

        Assert.Equal("evt-test-001", dto.CanonicalEventId);
        Assert.Equal("Reviewed", dto.LifecycleStatus);
        Assert.Equal("InReview", dto.ModerationStatus);
        Assert.Equal("EligibilityPending", dto.PublishStatus);
        Assert.Equal("Medium", dto.RiskLevel);
        Assert.Equal(0.92, dto.ConfidenceScore);
        Assert.Equal(3, dto.Version);
        Assert.NotNull(dto.ConcurrencyToken);
        Assert.False(dto.HasPendingReview);
    }

    [Fact]
    public void ToModeration_NullMergeLineageWhenNotMerged()
    {
        var aggregate = BuildAggregate();
        var dto = EventDtoMapper.ToModeration(aggregate);

        Assert.Null(dto.MergeLineage);
    }

    [Fact]
    public void ToModeration_PopulatesMergeLineageWhenMerged()
    {
        var aggregate = BuildAggregate(
            parentId: "parent-evt-001",
            mergedIds: ["child-evt-002", "child-evt-003"]);

        var dto = EventDtoMapper.ToModeration(aggregate);

        Assert.NotNull(dto.MergeLineage);
        Assert.Equal("parent-evt-001", dto.MergeLineage!.ParentCanonicalEventId);
        Assert.Equal(2, dto.MergeLineage.MergedEventCount);
    }

    [Fact]
    public void ToModeration_DoesNotExposeRawProvenanceRefs()
    {
        var dtoProperties = typeof(EventModerationDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Provenance", dtoProperties);
        Assert.DoesNotContain("SourceEventIds", dtoProperties);
        Assert.DoesNotContain("ExternalReferences", dtoProperties);
        Assert.DoesNotContain("AppliedMergePlanIds", dtoProperties);
    }

    // -----------------------------------------------------------------------
    // 5. PublishEligibility mapping (OPERATIONAL)
    // -----------------------------------------------------------------------

    [Fact]
    public void ToPublishEligibility_ReturnsCorrectEligibilityFields()
    {
        var aggregate = BuildAggregate();
        var result = BuildEligibilityResult(eligible: true, band: PublishEligibilityBand.AutoPublishable);
        var dto = EventDtoMapper.ToPublishEligibility(aggregate, result);

        Assert.Equal("evt-test-001", dto.CanonicalEventId);
        Assert.True(dto.Eligible);
        Assert.Equal("AutoPublishable", dto.EligibilityBand);
        Assert.Equal("AutoPublish", dto.RecommendedAction);
        Assert.Empty(dto.Blockers);
        Assert.True(dto.FieldCompleteness.IsComplete);
        Assert.Equal(1.0, dto.FieldCompleteness.CompletenessRatio);
    }

    [Fact]
    public void ToPublishEligibility_BlockersExcludeInternalMetadata()
    {
        var aggregate = BuildAggregate();
        var result = BuildEligibilityResult(eligible: false, band: PublishEligibilityBand.Blocked);
        // Add a blocker with internal metadata
        var blockerWithMeta = new PublishBlocker(
            Code: PublishBlockerCode.LowExtractionConfidence,
            Message: "Extraction confidence too low.",
            IsHardBlock: true,
            Field: "confidence",
            Metadata: "internal policy threshold=0.55");  // NOT exposed

        var blockedResult = result with
        {
            Eligible = false,
            Blockers = [blockerWithMeta],
        };

        var dto = EventDtoMapper.ToPublishEligibility(aggregate, blockedResult);
        var blockerSummaryProperties = typeof(PublishBlockerSummaryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Single(dto.Blockers);
        Assert.Equal("LowExtractionConfidence", dto.Blockers[0].Code);
        // Internal Metadata field must NOT be on the summary DTO
        Assert.DoesNotContain("Metadata", blockerSummaryProperties);
    }

    [Fact]
    public void ToPublishEligibility_ConfidenceSummaryOmitsPolicyWeights()
    {
        var aggregate = BuildAggregate();
        var result = BuildEligibilityResult();
        var dto = EventDtoMapper.ToPublishEligibility(aggregate, result);

        var summaryProperties = typeof(EligibilityConfidenceSummaryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Policy weights are internal; the summary exposes dimension SCORES but not WEIGHTS
        Assert.DoesNotContain("WeightsOverride", summaryProperties);
        Assert.DoesNotContain("Policy", summaryProperties);
        Assert.Equal(0.90, dto.ConfidenceSummary.Extraction);
        Assert.Equal(0.85, dto.ConfidenceSummary.Geocode);
    }

    // -----------------------------------------------------------------------
    // 6. Lifecycle status serialization
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(EventLifecycleStatus.Draft, "DRAFT")]
    [InlineData(EventLifecycleStatus.Candidate, "NEEDS_REVIEW")]
    [InlineData(EventLifecycleStatus.Reviewed, "NEEDS_REVIEW")]
    [InlineData(EventLifecycleStatus.Approved, "APPROVED")]
    [InlineData(EventLifecycleStatus.Rejected, "REJECTED")]
    [InlineData(EventLifecycleStatus.Published, "PUBLISHED")]
    [InlineData(EventLifecycleStatus.Cancelled, "ARCHIVED")]
    [InlineData(EventLifecycleStatus.Archived, "ARCHIVED")]
    public void SerializeLifecycleStatus_IsCorrectAndStable(
        EventLifecycleStatus input, string expected)
    {
        var actual = EventDtoMapper.SerializeLifecycleStatus(input);
        Assert.Equal(expected, actual);
    }

    // -----------------------------------------------------------------------
    // 7. Address formatting
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatAddress_OmitsNullState()
    {
        var address = new EventAddress(
            AddressLine1: "456 Oak Ave",
            City: "Austin",
            State: null,
            PostalCode: "78701",
            Country: "US",
            RawAddress: "456 Oak Ave Austin 78701");
        var result = EventDtoMapper.FormatAddress(address);
        Assert.Equal("456 Oak Ave, Austin", result);
        Assert.DoesNotContain("78701", result);   // PostalCode not in display format
        Assert.DoesNotContain("US", result);       // Country not in display format
    }

    [Fact]
    public void FormatAddress_IncludesAllPresentParts()
    {
        var address = new EventAddress(
            AddressLine1: "100 Main St",
            City: "Chicago",
            State: "IL",
            PostalCode: "60601",
            Country: "US",
            RawAddress: "raw-internal");
        var result = EventDtoMapper.FormatAddress(address);
        Assert.Equal("100 Main St, Chicago, IL", result);
    }

    // -----------------------------------------------------------------------
    // 8. Null-safety / argument guards
    // -----------------------------------------------------------------------

    [Fact]
    public void AllMappers_ThrowForNullAggregate()
    {
        Assert.Throws<ArgumentNullException>(() => EventDtoMapper.ToMapCard(null!));
        Assert.Throws<ArgumentNullException>(() => EventDtoMapper.ToCalendarCard(null!));
        Assert.Throws<ArgumentNullException>(() => EventDtoMapper.ToDetail(null!, [], "test"));
        Assert.Throws<ArgumentNullException>(() => EventDtoMapper.ToModeration(null!));
        Assert.Throws<ArgumentNullException>(() =>
            EventDtoMapper.ToPublishEligibility(null!, BuildEligibilityResult()));
        Assert.Throws<ArgumentNullException>(() =>
            EventDtoMapper.ToPublishEligibility(BuildAggregate(), null!));
    }

    [Fact]
    public void ToDetail_ThrowsForNullMediaRefs()
    {
        var aggregate = BuildAggregate();
        Assert.Throws<ArgumentNullException>(() => EventDtoMapper.ToDetail(aggregate, null!, "test"));
    }
}
