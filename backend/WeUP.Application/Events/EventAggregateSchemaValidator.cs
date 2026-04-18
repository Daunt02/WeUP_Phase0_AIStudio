// WeUP Phase 0 — M4-P19: Structured Schema Validator
//
// Non-throwing counterpart to EventAggregate.Validate().
// Collects ALL violations in a single pass and returns a structured
// SchemaValidationResult that can be:
//  - Logged with full context in development
//  - Surfaced as CI assertion output
//  - Used by test helpers to assert exact violation sets
//
// USAGE:
//   var result = EventAggregateSchemaValidator.ValidateAggregate(aggregate);
//   if (!result.IsValid)
//       logger.LogError("Schema violations: {Violations}", result.Violations);
//
// Methods:
//   ValidateAggregate(EventAggregate)
//   ValidateMapCardDto(EventMapCardDto)
//   ValidateCalendarCardDto(EventCalendarCardDto)
//   ValidateDetailDto(EventDetailDto)
//   ValidateModerationDto(EventModerationDto)

using System;
using System.Collections.Generic;
using System.Linq;
using WeUP.Contracts.Events;
using WeUP.Domain.Events;

namespace WeUP.Application.Events;

// ---------------------------------------------------------------------------
// Result types
// ---------------------------------------------------------------------------

/// <summary>
/// An individual schema contract violation.
/// </summary>
public sealed record SchemaViolation(
    /// <summary>Dot-path of the offending field or "(aggregate)" for root-level.</summary>
    string Field,
    /// <summary>Stable rule code for CI output and observability.</summary>
    string RuleCode,
    /// <summary>Human-readable developer-targeted description.</summary>
    string Message);

/// <summary>
/// Outcome of a schema validation pass. Never throws — inspect IsValid and Violations.
/// </summary>
public sealed record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<SchemaViolation> Violations,
    string DtoType);

// ---------------------------------------------------------------------------
// Validator
// ---------------------------------------------------------------------------

/// <summary>
/// Collects all schema violations in a single pass.
/// All enum value sets must be kept in sync with the C# domain enum definitions and
/// with the TypeScript schemaValidators.ts counterpart.
/// </summary>
public static class EventAggregateSchemaValidator
{
    // Valid enum value sets (must match C# enum names exactly)
    private static readonly HashSet<string> ValidFrontendStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "DRAFT", "NEEDS_REVIEW", "APPROVED", "PUBLISHED", "REJECTED", "ARCHIVED" };

    private static readonly HashSet<string> ValidLifecycleStatuses =
        new(StringComparer.Ordinal) { "Draft", "Candidate", "Reviewed", "Approved", "Rejected", "Published", "Cancelled", "Archived" };

    private static readonly HashSet<string> ValidModerationStatuses =
        new(StringComparer.Ordinal) { "Unreviewed", "InReview", "Approved", "Rejected" };

    private static readonly HashSet<string> ValidPublishStatuses =
        new(StringComparer.Ordinal) { "NotEligible", "EligibilityPending", "Eligible", "Published", "Unpublished", "Archived" };

    private static readonly HashSet<string> ValidRiskLevels =
        new(StringComparer.Ordinal) { "Low", "Medium", "High", "Restricted" };

    private static readonly HashSet<string> ValidChangeTypes =
        new(StringComparer.Ordinal) { "MinorMetadataUpdate", "MaterialEventChange", "StatusTransition", "MergeLineageUpdate" };

    private static readonly HashSet<string> ValidEligibilityBands =
        new(StringComparer.Ordinal) { "AutoPublishable", "ManualReviewRequired", "Blocked" };

    private static readonly HashSet<string> ValidRecommendedActions =
        new(StringComparer.Ordinal) { "AutoPublish", "RouteToManualReview", "BlockPublish" };

    // ---------------------------------------------------------------------------
    // 1. EventAggregate
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates all contract invariants on the canonical event aggregate.
    /// Does NOT throw — all violations are returned in the result.
    /// </summary>
    public static SchemaValidationResult ValidateAggregate(EventAggregate aggregate)
    {
        if (aggregate is null)
            return Fail("EventAggregate", "(aggregate)", "REQUIRED", "Aggregate must not be null.");

        var v = new ViolationBuilder("EventAggregate");

        // Identity
        v.RequireNonEmpty("CanonicalEventId", aggregate.CanonicalEventId);
        v.RequireNotNull("SourceEventIds", aggregate.SourceEventIds);

        // Content
        v.RequireNonEmpty("Title", aggregate.Title);
        v.RequireNonEmpty("VenueName", aggregate.VenueName);
        v.RequireNonEmpty("Category", aggregate.Category);
        v.RequireNotNull("Tags", aggregate.Tags);

        // Temporal
        v.RequireNonEmpty("TimeZone", aggregate.TimeZone);
        if (aggregate.StartUtc == default)
            v.Add("StartUtc", "INVALID_STARTUTC", "StartUtc must not be DateTimeOffset.MinValue.");
        if (aggregate.EndUtc.HasValue && aggregate.EndUtc.Value < aggregate.StartUtc)
            v.Add("EndUtc", "END_BEFORE_START", $"EndUtc ({aggregate.EndUtc:O}) must not be before StartUtc ({aggregate.StartUtc:O}).");

        // Address
        if (aggregate.Address is null)
        {
            v.Add("Address", "REQUIRED", "Address is required.");
        }
        else
        {
            v.RequireNonEmpty("Address.AddressLine1", aggregate.Address.AddressLine1);
            v.RequireNonEmpty("Address.City", aggregate.Address.City);
            v.RequireNonEmpty("Address.Country", aggregate.Address.Country);
            v.RequireNonEmpty("Address.RawAddress", aggregate.Address.RawAddress);
        }

        // Geo
        if (double.IsNaN(aggregate.Latitude) || aggregate.Latitude < -90 || aggregate.Latitude > 90)
            v.Add("Latitude", "INVALID_LATITUDE", $"Latitude must be in [-90, 90], got {aggregate.Latitude}.");
        if (double.IsNaN(aggregate.Longitude) || aggregate.Longitude < -180 || aggregate.Longitude > 180)
            v.Add("Longitude", "INVALID_LONGITUDE", $"Longitude must be in [-180, 180], got {aggregate.Longitude}.");
        if (aggregate.Latitude == 0 && aggregate.Longitude == 0)
            v.Add("Latitude", "NULL_ISLAND_COORDINATES", "Coordinates (0, 0) indicate an unresolved null-island location.");

        // Confidence
        if (double.IsNaN(aggregate.ConfidenceScore) || aggregate.ConfidenceScore < 0 || aggregate.ConfidenceScore > 1)
            v.Add("ConfidenceScore", "INVALID_CONFIDENCE", $"ConfidenceScore must be in [0, 1], got {aggregate.ConfidenceScore}.");

        // Version / concurrency
        if (aggregate.Version < 1)
            v.Add("Version", "INVALID_VERSION", $"Version must be >= 1, got {aggregate.Version}.");
        v.RequireNonEmpty("ConcurrencyToken", aggregate.EffectiveConcurrencyToken);

        // Provenance
        if (aggregate.Provenance is null)
        {
            v.Add("Provenance", "REQUIRED", "Provenance is required.");
        }
        else
        {
            v.RequireNonEmpty("Provenance.PrimarySourceKind", aggregate.Provenance.PrimarySourceKind);
            v.RequireNonEmpty("Provenance.PrimarySourceRef", aggregate.Provenance.PrimarySourceRef);
            v.RequireNotNull("Provenance.EvidenceRefs", aggregate.Provenance.EvidenceRefs);
        }

        // Merge lineage
        v.RequireNotNull("MergeLineage", aggregate.MergeLineage);
        if (aggregate.MergeLineage is not null)
        {
            foreach (var mergeRecord in aggregate.MergeLineage.EffectiveMergeRecords)
            {
                if (string.IsNullOrWhiteSpace(mergeRecord.MergeId))
                    v.Add("MergeLineage.MergeRecords.MergeId", "REQUIRED_FIELD_EMPTY", "MergeLineage merge record MergeId is required.");
                if (string.IsNullOrWhiteSpace(mergeRecord.MergeReason))
                    v.Add("MergeLineage.MergeRecords.MergeReason", "REQUIRED_FIELD_EMPTY", "MergeLineage merge record MergeReason is required.");
                if (string.IsNullOrWhiteSpace(mergeRecord.MergeActor))
                    v.Add("MergeLineage.MergeRecords.MergeActor", "REQUIRED_FIELD_EMPTY", "MergeLineage merge record MergeActor is required.");

                foreach (var sourceReference in mergeRecord.SourceReferences ?? [])
                {
                    if (string.IsNullOrWhiteSpace(sourceReference.SourceRef))
                        v.Add("MergeLineage.MergeRecords.SourceReferences.SourceRef", "REQUIRED_FIELD_EMPTY", "Merge source reference SourceRef is required.");
                    if (string.IsNullOrWhiteSpace(sourceReference.SourceKind))
                        v.Add("MergeLineage.MergeRecords.SourceReferences.SourceKind", "REQUIRED_FIELD_EMPTY", "Merge source reference SourceKind is required.");
                }
            }
        }

        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // 2. EventMapCardDto
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates the map-card DTO surface.
    /// Rules: required identity fields, coordinate ranges, status enum, confidence range.
    /// </summary>
    public static SchemaValidationResult ValidateMapCardDto(EventMapCardDto dto)
    {
        if (dto is null)
            return Fail("EventMapCardDto", "(dto)", "REQUIRED", "DTO must not be null.");

        var v = new ViolationBuilder("EventMapCardDto");
        v.RequireNonEmpty("Id", dto.Id);
        v.RequireNonEmpty("Title", dto.Title);
        v.RequireNonEmpty("VenueName", dto.VenueName);
        v.RequireNonEmpty("Category", dto.Category);
        v.RequireLatitude("Lat", dto.Lat);
        v.RequireLongitude("Lng", dto.Lng);
        v.WarnNullIsland("Lat", dto.Lat, dto.Lng);
        v.RequireEnumValue("Status", dto.Status, ValidFrontendStatuses.ToDictionary(_ => _, _ => true).Keys.ToHashSet(StringComparer.Ordinal));
        v.RequireConfidence("Confidence", dto.Confidence);
        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // 3. EventCalendarCardDto
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates the calendar-card DTO surface.
    /// Additional rules: StartUtc not default, EndUtc >= StartUtc if set, Timezone non-empty.
    /// </summary>
    public static SchemaValidationResult ValidateCalendarCardDto(EventCalendarCardDto dto)
    {
        if (dto is null)
            return Fail("EventCalendarCardDto", "(dto)", "REQUIRED", "DTO must not be null.");

        var v = new ViolationBuilder("EventCalendarCardDto");
        v.RequireNonEmpty("Id", dto.Id);
        v.RequireNonEmpty("Title", dto.Title);
        v.RequireNonEmpty("VenueName", dto.VenueName);
        v.RequireNonEmpty("Category", dto.Category);
        if (dto.StartUtc == default)
            v.Add("StartUtc", "INVALID_STARTUTC", "StartUtc must not be DateTimeOffset.MinValue.");
        if (dto.EndUtc.HasValue && dto.EndUtc.Value < dto.StartUtc)
            v.Add("EndUtc", "END_BEFORE_START", $"EndUtc ({dto.EndUtc:O}) must not be before StartUtc ({dto.StartUtc:O}).");
        v.RequireNonEmpty("Timezone", dto.Timezone);
        v.RequireEnumValue("Status", dto.Status, ValidFrontendStatuses);
        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // 4. EventDetailDto
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates the detail DTO surface.
    /// Additional rules: Address non-empty, MediaRefs/Tags non-null, Version >= 1,
    /// Confidence in [0,1], LastChangeType enum if non-null, coordinate ranges.
    /// </summary>
    public static SchemaValidationResult ValidateDetailDto(EventDetailDto dto)
    {
        if (dto is null)
            return Fail("EventDetailDto", "(dto)", "REQUIRED", "DTO must not be null.");

        var v = new ViolationBuilder("EventDetailDto");
        v.RequireNonEmpty("Id", dto.Id);
        v.RequireNonEmpty("Title", dto.Title);
        v.RequireNonEmpty("VenueName", dto.VenueName);
        v.RequireNonEmpty("Category", dto.Category);
        v.RequireNonEmpty("Address", dto.Address);
        v.RequireLatitude("Lat", dto.Lat);
        v.RequireLongitude("Lng", dto.Lng);
        v.WarnNullIsland("Lat", dto.Lat, dto.Lng);
        if (dto.StartUtc == default)
            v.Add("StartUtc", "INVALID_STARTUTC", "StartUtc must not be DateTimeOffset.MinValue.");
        if (dto.EndUtc.HasValue && dto.EndUtc.Value < dto.StartUtc)
            v.Add("EndUtc", "END_BEFORE_START", $"EndUtc ({dto.EndUtc:O}) must not be before StartUtc ({dto.StartUtc:O}).");
        v.RequireNonEmpty("Timezone", dto.Timezone);
        v.RequireNotNull("MediaRefs", (object?)dto.MediaRefs);
        v.RequireNotNull("Tags", (object?)dto.Tags);
        v.RequireEnumValue("Status", dto.Status, ValidFrontendStatuses);
        v.RequireConfidence("Confidence", dto.Confidence);
        v.RequireNonEmpty("SourceKind", dto.SourceKind);
        if (dto.Version < 1)
            v.Add("Version", "INVALID_VERSION", $"Version must be >= 1, got {dto.Version}.");
        if (dto.LastChangeType is not null && !ValidChangeTypes.Contains(dto.LastChangeType))
            v.Add("LastChangeType", "INVALID_ENUM_VALUE",
                $"LastChangeType must be one of [{string.Join(", ", ValidChangeTypes)}], got \"{dto.LastChangeType}\".");
        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // 5. EventModerationDto
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates the moderation-view DTO surface (operational, moderator-gated).
    /// Additional rules: PascalCase enum validation for lifecycle/moderation/publish/risk,
    /// Version >= 1, UpdatedAtUtc not default, HasPendingReview is bool (type-safe in C#).
    /// </summary>
    public static SchemaValidationResult ValidateModerationDto(EventModerationDto dto)
    {
        if (dto is null)
            return Fail("EventModerationDto", "(dto)", "REQUIRED", "DTO must not be null.");

        var v = new ViolationBuilder("EventModerationDto");
        v.RequireNonEmpty("CanonicalEventId", dto.CanonicalEventId);
        v.RequireNonEmpty("Title", dto.Title);
        v.RequireNonEmpty("Category", dto.Category);
        v.RequireNonEmpty("VenueName", dto.VenueName);
        if (dto.StartUtc == default)
            v.Add("StartUtc", "INVALID_STARTUTC", "StartUtc must not be DateTimeOffset.MinValue.");
        if (dto.EndUtc.HasValue && dto.EndUtc.Value < dto.StartUtc)
            v.Add("EndUtc", "END_BEFORE_START", $"EndUtc ({dto.EndUtc:O}) must not be before StartUtc ({dto.StartUtc:O}).");
        v.RequireNonEmpty("Timezone", dto.Timezone);
        v.RequireEnumValue("LifecycleStatus", dto.LifecycleStatus, ValidLifecycleStatuses);
        v.RequireEnumValue("ModerationStatus", dto.ModerationStatus, ValidModerationStatuses);
        v.RequireEnumValue("PublishStatus", dto.PublishStatus, ValidPublishStatuses);
        v.RequireEnumValue("RiskLevel", dto.RiskLevel, ValidRiskLevels);
        v.RequireConfidence("ConfidenceScore", dto.ConfidenceScore);
        if (dto.Version < 1)
            v.Add("Version", "INVALID_VERSION", $"Version must be >= 1, got {dto.Version}.");
        if (dto.UpdatedAtUtc == default)
            v.Add("UpdatedAtUtc", "INVALID_UPDATEDATUTC", "UpdatedAtUtc must not be DateTimeOffset.MinValue.");
        if (dto.LastChangeType is not null && !ValidChangeTypes.Contains(dto.LastChangeType))
            v.Add("LastChangeType", "INVALID_ENUM_VALUE",
                $"LastChangeType must be one of [{string.Join(", ", ValidChangeTypes)}], got \"{dto.LastChangeType}\".");
        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // 6. EventPublishEligibilityDto
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates the publish eligibility DTO.
    /// </summary>
    public static SchemaValidationResult ValidatePublishEligibilityDto(EventPublishEligibilityDto dto)
    {
        if (dto is null)
            return Fail("EventPublishEligibilityDto", "(dto)", "REQUIRED", "DTO must not be null.");

        var v = new ViolationBuilder("EventPublishEligibilityDto");
        v.RequireNonEmpty("CanonicalEventId", dto.CanonicalEventId);
        v.RequireEnumValue("EligibilityBand", dto.EligibilityBand, ValidEligibilityBands);
        v.RequireEnumValue("RecommendedAction", dto.RecommendedAction, ValidRecommendedActions);
        v.RequireNotNull("Blockers", (object?)dto.Blockers);
        v.RequireNotNull("Notes", (object?)dto.Notes);
        if (dto.ConfidenceSummary is null)
        {
            v.Add("ConfidenceSummary", "REQUIRED", "ConfidenceSummary must not be null.");
        }
        else
        {
            v.RequireConfidence("ConfidenceSummary.Aggregate", dto.ConfidenceSummary.Aggregate);
        }
        v.RequireNotNull("FieldCompleteness", dto.FieldCompleteness);
        return v.Build();
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static SchemaValidationResult Fail(string dtoType, string field, string ruleCode, string message)
        => new(false, new[] { new SchemaViolation(field, ruleCode, message) }, dtoType);

    private sealed class ViolationBuilder(string dtoType)
    {
        private readonly List<SchemaViolation> _violations = new();
        private readonly string _dtoType = dtoType;

        public void Add(string field, string ruleCode, string message)
            => _violations.Add(new SchemaViolation(field, ruleCode, message));

        public void RequireNonEmpty(string field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(field, "REQUIRED_FIELD_EMPTY", $"{field} is required and must not be empty.");
        }

        public void RequireNotNull(string field, object? value)
        {
            if (value is null)
                Add(field, "REQUIRED_NOT_NULL", $"{field} must be non-null (empty collection is allowed).");
        }

        public void RequireLatitude(string field, double value)
        {
            if (double.IsNaN(value) || value < -90 || value > 90)
                Add(field, "INVALID_LATITUDE", $"{field} must be in [-90, 90], got {value}.");
        }

        public void RequireLongitude(string field, double value)
        {
            if (double.IsNaN(value) || value < -180 || value > 180)
                Add(field, "INVALID_LONGITUDE", $"{field} must be in [-180, 180], got {value}.");
        }

        public void RequireConfidence(string field, double value)
        {
            if (double.IsNaN(value) || value < 0 || value > 1)
                Add(field, "INVALID_CONFIDENCE", $"{field} must be in [0, 1], got {value}.");
        }

        public void WarnNullIsland(string latField, double lat, double lng)
        {
            if (lat == 0 && lng == 0)
                Add(latField, "NULL_ISLAND_COORDINATES",
                    $"Coordinates ({latField}=0, Lng=0) indicate an unresolved null-island location.");
        }

        public void RequireEnumValue(string field, string? value, HashSet<string> validValues)
        {
            if (value is null || !validValues.Contains(value))
                Add(field, "INVALID_ENUM_VALUE",
                    $"{field} must be one of [{string.Join(", ", validValues.OrderBy(x => x))}], got \"{value}\".");
        }

        public SchemaValidationResult Build()
            => new(_violations.Count == 0, _violations.AsReadOnly(), _dtoType);
    }
}
