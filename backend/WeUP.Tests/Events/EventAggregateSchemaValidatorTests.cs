// WeUP Phase 0 — M4-P19: EventAggregateSchemaValidator Tests
//
// Coverage:
//  1. ValidateAggregate: happy path, each required-field violation, coordinate range,
//     temporal order, provenance sub-fields, confidence range, version, null-island.
//  2. ValidateMapCardDto: identity, coordinate, status enum, confidence.
//  3. ValidateCalendarCardDto: base + temporal order.
//  4. ValidateDetailDto: base + version, arrays, change type enum.
//  5. ValidateModerationDto: base + PascalCase enums, bool field, version.
//  6. ValidatePublishEligibilityDto: eligibility band, confidence sub-field.

using System;
using System.Collections.Generic;
using System.Linq;
using WeUP.Application.Events;
using WeUP.Contracts.Events;
using WeUP.Contracts.Moderation;
using WeUP.Domain.Events;
using WeUP.Domain.Moderation;
using Xunit;

namespace WeUP.Tests.Events;

/// <summary>
/// M4-P19 — Tests for EventAggregateSchemaValidator.
/// Validates both the happy path and every distinct violation category.
/// </summary>
public sealed class EventAggregateSchemaValidatorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static EventAggregate BuildValidAggregate(
        string id = "evt-001",
        int version = 1,
        double lat = 29.76,
        double lng = -95.36,
        double confidence = 0.88,
        EventLifecycleStatus status = EventLifecycleStatus.Published)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventAggregate(
            CanonicalEventId: id,
            SourceEventIds: [id],
            ExternalReferences: [],
            Title: "Test Event",
            Description: null,
            Tags: ["jazz"],
            Category: "nightlife",
            VenueName: "Test Venue",
            Address: new EventAddress(
                AddressLine1: "123 Main St",
                City: "Houston",
                State: "TX",
                PostalCode: "77001",
                Country: "US",
                RawAddress: "123 Main St, Houston TX 77001 US"),
            Latitude: lat,
            Longitude: lng,
            TimeZone: "America/Chicago",
            StartUtc: now.AddDays(1),
            EndUtc: now.AddDays(1).AddHours(4),
            LocalStartDisplay: null,
            LocalEndDisplay: null,
            EventStatus: status,
            PublishStatus: EventPublishStatus.Published,
            ModerationStatus: EventModerationStatus.Approved,
            RiskLevel: EventRiskLevel.Low,
            ConfidenceScore: confidence,
            Provenance: new EventProvenanceMetadata(
                PrimarySourceKind: "manual_submission",
                PrimarySourceRef: "sub-001",
                EvidenceRefs: ["evidence-a"],
                FirstObservedAtUtc: now.AddDays(-1),
                LastObservedAtUtc: now,
                SourceRefs: ["source-ref-x"]),
            CreatedAtUtc: now.AddDays(-1),
            UpdatedAtUtc: now,
            Version: version,
            MergeLineage: new EventMergeLineage(
                ParentCanonicalEventId: null,
                MergedCanonicalEventIds: [],
                AppliedMergePlanIds: [],
                LastMergedAtUtc: null,
                LastMergedBy: null));
    }

    private static EventMapCardDto BuildValidMapCard(
        string id = "evt-001",
        string status = "PUBLISHED",
        double lat = 29.76,
        double lng = -95.36,
        double confidence = 0.88) =>
        new(id, "Test Event", "Test Venue", "nightlife", lat, lng, null, status, confidence);

    private static EventCalendarCardDto BuildValidCalendarCard() =>
        new("evt-001", "Test Event", "Test Venue", "nightlife",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
            "America/Chicago", null, "PUBLISHED");

    private static EventDetailDto BuildValidDetailDto(
        string status = "PUBLISHED",
        int version = 1,
        string? lastChangeType = null) =>
        new(
            Id: "evt-001",
            Title: "Test Event",
            Description: "A description",
            VenueName: "Test Venue",
            Address: "123 Main St, Houston TX",
            Lat: 29.76,
            Lng: -95.36,
            Category: "nightlife",
            Categories: ["nightlife"],
            StartUtc: DateTimeOffset.UtcNow.AddDays(1),
            EndUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
            Timezone: "America/Chicago",
            FlyerImageUrl: "https://cdn.test/img.jpg",
            MediaRefs: [new MediaRefDto("https://cdn.test/img.jpg", "image")],
            Tags: ["jazz"],
            Status: status,
            Confidence: 0.88,
            SourceKind: "manual submission",
            ProvenanceSummary: new EventDetailProvenanceSummaryDto(
                PrimarySourceKind: "manual submission",
                SourceCount: 1,
                FirstObservedAtUtc: DateTimeOffset.UtcNow.AddDays(-1),
                LastObservedAtUtc: DateTimeOffset.UtcNow,
                SummaryLabel: "Normalized from manual submission."),
            Version: version,
            LastChangeType: lastChangeType,
            ConcurrencyToken: "evt-001:v1");

    private static EventModerationDto BuildValidModerationDto(
        int version = 1,
        bool hasPendingReview = false) =>
        new("evt-001", "Test Event", "nightlife", "Test Venue",
            DateTimeOffset.UtcNow.AddDays(1),
            null,
            "America/Chicago",
            "Published", "Approved", "Published", "Low",
            0.88, version,
            DateTimeOffset.UtcNow,
            null, hasPendingReview, "evt-001:v1", null);

    private static void AssertViolation(SchemaValidationResult result, string ruleCode, string? fieldHint = null)
    {
        Assert.False(result.IsValid, $"Expected invalid result but got IsValid=true");
        var match = result.Violations.FirstOrDefault(v =>
            v.RuleCode == ruleCode && (fieldHint == null || v.Field == fieldHint));
        Assert.True(match is not null,
            $"Expected violation RuleCode={ruleCode}{(fieldHint != null ? $" Field={fieldHint}" : "")}. Found: [{string.Join(", ", result.Violations.Select(v => $"{v.RuleCode}@{v.Field}"))}]");
    }

    // -----------------------------------------------------------------------
    // 1. ValidateAggregate — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_ValidInput_ReturnsValid()
    {
        var result = EventAggregateSchemaValidator.ValidateAggregate(BuildValidAggregate());
        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
        Assert.Equal("EventAggregate", result.DtoType);
    }

    [Fact]
    public void ValidateAggregate_NullInput_ReturnsSingleViolation()
    {
        var result = EventAggregateSchemaValidator.ValidateAggregate(null!);
        Assert.False(result.IsValid);
        Assert.Single(result.Violations);
    }

    // -----------------------------------------------------------------------
    // 1a. ValidateAggregate — required fields
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_EmptyCanonicalEventId_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { CanonicalEventId = "" };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "CanonicalEventId");
    }

    [Fact]
    public void ValidateAggregate_EmptyTitle_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { Title = "  " };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Title");
    }

    [Fact]
    public void ValidateAggregate_EmptyVenueName_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { VenueName = "" };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "VenueName");
    }

    [Fact]
    public void ValidateAggregate_EmptyCategory_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { Category = "" };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Category");
    }

    [Fact]
    public void ValidateAggregate_NullTags_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { Tags = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_NOT_NULL", "Tags");
    }

    [Fact]
    public void ValidateAggregate_EmptyTimeZone_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { TimeZone = "" };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "TimeZone");
    }

    [Fact]
    public void ValidateAggregate_NullSourceEventIds_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { SourceEventIds = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_NOT_NULL", "SourceEventIds");
    }

    // -----------------------------------------------------------------------
    // 1b. ValidateAggregate — temporal
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_DefaultStartUtc_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with { StartUtc = default };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "INVALID_STARTUTC", "StartUtc");
    }

    [Fact]
    public void ValidateAggregate_EndBeforeStart_ReturnsViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var agg = BuildValidAggregate() with
        {
            StartUtc = now.AddDays(2),
            EndUtc = now.AddDays(1),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "END_BEFORE_START", "EndUtc");
    }

    [Fact]
    public void ValidateAggregate_NullEndUtc_IsValid()
    {
        var agg = BuildValidAggregate() with { EndUtc = null };
        Assert.True(EventAggregateSchemaValidator.ValidateAggregate(agg).IsValid);
    }

    // -----------------------------------------------------------------------
    // 1c. ValidateAggregate — address sub-fields
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_EmptyAddressCity_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with
        {
            Address = new EventAddress("123 Main St", "", "TX", "77001", "US", "123 Main St Houston TX"),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Address.City");
    }

    [Fact]
    public void ValidateAggregate_EmptyAddressCountry_ReturnsViolation()
    {
        var agg = BuildValidAggregate() with
        {
            Address = new EventAddress("123 Main St", "Houston", "TX", "77001", "", "123 Main St Houston TX"),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Address.Country");
    }

    // -----------------------------------------------------------------------
    // 1d. ValidateAggregate — geo
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(double.NaN)]
    public void ValidateAggregate_InvalidLatitude_ReturnsViolation(double lat)
    {
        var agg = BuildValidAggregate(lat: lat);
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "INVALID_LATITUDE", "Latitude");
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void ValidateAggregate_InvalidLongitude_ReturnsViolation(double lng)
    {
        var agg = BuildValidAggregate(lng: lng);
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "INVALID_LONGITUDE", "Longitude");
    }

    [Fact]
    public void ValidateAggregate_NullIslandCoordinates_ReturnsViolation()
    {
        var agg = BuildValidAggregate(lat: 0, lng: 0);
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "NULL_ISLAND_COORDINATES", "Latitude");
    }

    // -----------------------------------------------------------------------
    // 1e. ValidateAggregate — confidence and version
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void ValidateAggregate_InvalidConfidenceScore_ReturnsViolation(double confidence)
    {
        var agg = BuildValidAggregate(confidence: confidence);
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "INVALID_CONFIDENCE", "ConfidenceScore");
    }

    [Fact]
    public void ValidateAggregate_ZeroVersion_ReturnsViolation()
    {
        var agg = BuildValidAggregate(version: 0);
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "INVALID_VERSION", "Version");
    }

    // -----------------------------------------------------------------------
    // 1f. ValidateAggregate — provenance
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_EmptyPrimarySourceKind_ReturnsViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var agg = BuildValidAggregate() with
        {
            Provenance = new EventProvenanceMetadata("", "sub-001", ["ev-a"], now, now, ["src"]),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Provenance.PrimarySourceKind");
    }

    [Fact]
    public void ValidateAggregate_EmptyPrimarySourceRef_ReturnsViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var agg = BuildValidAggregate() with
        {
            Provenance = new EventProvenanceMetadata("manual_submission", "", ["ev-a"], now, now, ["src"]),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_FIELD_EMPTY", "Provenance.PrimarySourceRef");
    }

    [Fact]
    public void ValidateAggregate_NullEvidenceRefs_ReturnsViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var agg = BuildValidAggregate() with
        {
            Provenance = new EventProvenanceMetadata("manual_submission", "sub-001", null!, now, now, ["src"]),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateAggregate(agg), "REQUIRED_NOT_NULL", "Provenance.EvidenceRefs");
    }

    // -----------------------------------------------------------------------
    // 2. ValidateMapCardDto
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateMapCardDto_ValidInput_ReturnsValid()
    {
        var result = EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateMapCardDto_NullInput_ReturnsSingleViolation()
    {
        var result = EventAggregateSchemaValidator.ValidateMapCardDto(null!);
        Assert.False(result.IsValid);
        Assert.Single(result.Violations);
    }

    [Fact]
    public void ValidateMapCardDto_EmptyId_ReturnsViolation()
    {
        AssertViolation(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(id: "")),
            "REQUIRED_FIELD_EMPTY", "Id");
    }

    [Theory]
    [InlineData("published")]    // lowercase
    [InlineData("Published")]    // wrong casing
    [InlineData("INVALID")]
    public void ValidateMapCardDto_InvalidStatus_ReturnsViolation(string status)
    {
        AssertViolation(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(status: status)),
            "INVALID_ENUM_VALUE", "Status");
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("NEEDS_REVIEW")]
    [InlineData("APPROVED")]
    [InlineData("PUBLISHED")]
    [InlineData("REJECTED")]
    [InlineData("ARCHIVED")]
    public void ValidateMapCardDto_AllValidStatuses_Pass(string status)
    {
        Assert.True(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(status: status)).IsValid);
    }

    [Fact]
    public void ValidateMapCardDto_LatitudeOutOfRange_ReturnsViolation()
    {
        AssertViolation(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(lat: 91)),
            "INVALID_LATITUDE", "Lat");
    }

    [Fact]
    public void ValidateMapCardDto_ConfidenceAboveOne_ReturnsViolation()
    {
        AssertViolation(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(confidence: 1.1)),
            "INVALID_CONFIDENCE", "Confidence");
    }

    [Fact]
    public void ValidateMapCardDto_NullIsland_ReturnsViolation()
    {
        AssertViolation(EventAggregateSchemaValidator.ValidateMapCardDto(BuildValidMapCard(lat: 0, lng: 0)),
            "NULL_ISLAND_COORDINATES", "Lat");
    }

    // -----------------------------------------------------------------------
    // 3. ValidateCalendarCardDto
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateCalendarCardDto_ValidInput_ReturnsValid()
    {
        Assert.True(EventAggregateSchemaValidator.ValidateCalendarCardDto(BuildValidCalendarCard()).IsValid);
    }

    [Fact]
    public void ValidateCalendarCardDto_DefaultStartUtc_ReturnsViolation()
    {
        var dto = BuildValidCalendarCard() with { StartUtc = default };
        AssertViolation(EventAggregateSchemaValidator.ValidateCalendarCardDto(dto), "INVALID_STARTUTC", "StartUtc");
    }

    [Fact]
    public void ValidateCalendarCardDto_EndBeforeStart_ReturnsViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var dto = BuildValidCalendarCard() with
        {
            StartUtc = now.AddDays(2),
            EndUtc = now.AddDays(1),
        };
        AssertViolation(EventAggregateSchemaValidator.ValidateCalendarCardDto(dto), "END_BEFORE_START", "EndUtc");
    }

    [Fact]
    public void ValidateCalendarCardDto_EmptyTimezone_ReturnsViolation()
    {
        var dto = BuildValidCalendarCard() with { Timezone = "" };
        AssertViolation(EventAggregateSchemaValidator.ValidateCalendarCardDto(dto), "REQUIRED_FIELD_EMPTY", "Timezone");
    }

    // -----------------------------------------------------------------------
    // 4. ValidateDetailDto
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateDetailDto_ValidInput_ReturnsValid()
    {
        Assert.True(EventAggregateSchemaValidator.ValidateDetailDto(BuildValidDetailDto()).IsValid);
    }

    [Fact]
    public void ValidateDetailDto_NullMediaRefs_ReturnsViolation()
    {
        var dto = BuildValidDetailDto() with { MediaRefs = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidateDetailDto(dto), "REQUIRED_NOT_NULL", "MediaRefs");
    }

    [Fact]
    public void ValidateDetailDto_NullTags_ReturnsViolation()
    {
        var dto = BuildValidDetailDto() with { Tags = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidateDetailDto(dto), "REQUIRED_NOT_NULL", "Tags");
    }

    [Fact]
    public void ValidateDetailDto_ZeroVersion_ReturnsViolation()
    {
        var dto = BuildValidDetailDto(version: 0);
        AssertViolation(EventAggregateSchemaValidator.ValidateDetailDto(dto), "INVALID_VERSION", "Version");
    }

    [Fact]
    public void ValidateDetailDto_InvalidLastChangeType_ReturnsViolation()
    {
        var dto = BuildValidDetailDto(lastChangeType: "INVALID_TYPE");
        AssertViolation(EventAggregateSchemaValidator.ValidateDetailDto(dto), "INVALID_ENUM_VALUE", "LastChangeType");
    }

    [Theory]
    [InlineData("MinorMetadataUpdate")]
    [InlineData("MaterialEventChange")]
    [InlineData("StatusTransition")]
    [InlineData("MergeLineageUpdate")]
    public void ValidateDetailDto_ValidLastChangeType_ReturnsValid(string changeType)
    {
        Assert.True(EventAggregateSchemaValidator.ValidateDetailDto(BuildValidDetailDto(lastChangeType: changeType)).IsValid);
    }

    [Fact]
    public void ValidateDetailDto_NullLastChangeType_IsValid()
    {
        Assert.True(EventAggregateSchemaValidator.ValidateDetailDto(BuildValidDetailDto(lastChangeType: null)).IsValid);
    }

    // -----------------------------------------------------------------------
    // 5. ValidateModerationDto
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateModerationDto_ValidInput_ReturnsValid()
    {
        Assert.True(EventAggregateSchemaValidator.ValidateModerationDto(BuildValidModerationDto()).IsValid);
    }

    [Fact]
    public void ValidateModerationDto_UppercaseLifecycleStatus_ReturnsViolation()
    {
        // Moderation surface expects PascalCase, not upper-case
        var dto = BuildValidModerationDto() with { LifecycleStatus = "PUBLISHED" };
        AssertViolation(EventAggregateSchemaValidator.ValidateModerationDto(dto), "INVALID_ENUM_VALUE", "LifecycleStatus");
    }

    [Fact]
    public void ValidateModerationDto_InvalidModerationStatus_ReturnsViolation()
    {
        var dto = BuildValidModerationDto() with { ModerationStatus = "PendingReview" };
        AssertViolation(EventAggregateSchemaValidator.ValidateModerationDto(dto), "INVALID_ENUM_VALUE", "ModerationStatus");
    }

    [Fact]
    public void ValidateModerationDto_InvalidRiskLevel_ReturnsViolation()
    {
        var dto = BuildValidModerationDto() with { RiskLevel = "Critical" };
        AssertViolation(EventAggregateSchemaValidator.ValidateModerationDto(dto), "INVALID_ENUM_VALUE", "RiskLevel");
    }

    [Fact]
    public void ValidateModerationDto_ZeroVersion_ReturnsViolation()
    {
        var dto = BuildValidModerationDto(version: 0);
        AssertViolation(EventAggregateSchemaValidator.ValidateModerationDto(dto), "INVALID_VERSION", "Version");
    }

    [Fact]
    public void ValidateModerationDto_DefaultUpdatedAtUtc_ReturnsViolation()
    {
        var dto = BuildValidModerationDto() with { UpdatedAtUtc = default };
        AssertViolation(EventAggregateSchemaValidator.ValidateModerationDto(dto), "INVALID_UPDATEDATUTC", "UpdatedAtUtc");
    }

    // -----------------------------------------------------------------------
    // 6. ValidatePublishEligibilityDto
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidatePublishEligibilityDto_ValidInput_ReturnsValid()
    {
        var dto = BuildValidEligibilityDto();
        Assert.True(EventAggregateSchemaValidator.ValidatePublishEligibilityDto(dto).IsValid);
    }

    [Fact]
    public void ValidatePublishEligibilityDto_InvalidEligibilityBand_ReturnsViolation()
    {
        var dto = BuildValidEligibilityDto() with { EligibilityBand = "Unknown" };
        AssertViolation(EventAggregateSchemaValidator.ValidatePublishEligibilityDto(dto), "INVALID_ENUM_VALUE", "EligibilityBand");
    }

    [Fact]
    public void ValidatePublishEligibilityDto_NullBlockers_ReturnsViolation()
    {
        var dto = BuildValidEligibilityDto() with { Blockers = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidatePublishEligibilityDto(dto), "REQUIRED_NOT_NULL", "Blockers");
    }

    [Fact]
    public void ValidatePublishEligibilityDto_NullConfidenceSummary_ReturnsViolation()
    {
        var dto = BuildValidEligibilityDto() with { ConfidenceSummary = null! };
        AssertViolation(EventAggregateSchemaValidator.ValidatePublishEligibilityDto(dto), "REQUIRED", "ConfidenceSummary");
    }

    // -----------------------------------------------------------------------
    // 7. Cross-surface: multiple violations are collected (non-early-exit)
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAggregate_MultipleViolations_AllReported()
    {
        var agg = BuildValidAggregate() with
        {
            Title = "",
            Category = "",
            ConfidenceScore = -1.0,
        };
        var result = EventAggregateSchemaValidator.ValidateAggregate(agg);
        Assert.False(result.IsValid);
        // All three fields should be present in violation list
        Assert.Contains(result.Violations, v => v.Field == "Title");
        Assert.Contains(result.Violations, v => v.Field == "Category");
        Assert.Contains(result.Violations, v => v.Field == "ConfidenceScore");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static EventPublishEligibilityDto BuildValidEligibilityDto() =>
        new(
            CanonicalEventId: "evt-001",
            Eligible: true,
            EligibilityBand: "AutoPublishable",
            RecommendedAction: "AutoPublish",
            Blockers: [],
            ConfidenceSummary: new EligibilityConfidenceSummaryDto(
                Aggregate: 0.88, Band: "High",
                Extraction: 0.9, Geocode: 0.85, Temporal: 0.95,
                VenueMatch: 0.8, DupeRisk: 0.92,
                MeetsAutoPublishThreshold: true, RequiresManualReview: false),
            FieldCompleteness: new EligibilityFieldCompletenessSummaryDto(
                IsComplete: true, MissingRequiredFields: [], CompletenessRatio: 1.0),
            Notes: []);
}
