namespace WeUP.Domain.Spatial;

/// <summary>
/// Represents a geographic district or neighborhood within a city market.
/// Districts are used for spatial filtering and geographic organization of events.
/// </summary>
public class District
{
    /// <summary>
    /// Unique identifier for the district (e.g., "downtown", "mission", "marina").
    /// </summary>
    public string DistrictCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Downtown SF", "Mission District").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the district for documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Bounding box approximation of the district (PostGIS polygons in future).
    /// Used for quick geographic filtering before detailed containment checks.
    /// </summary>
    public BoundingBox BoundingBoxApproximation { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// The market/city this district belongs to (e.g., "sf", "oakland").
    /// Used for market-scoped queries.
    /// </summary>
    public string MarketCode { get; set; } = string.Empty;

    /// <summary>
    /// Sort order for UI display (lower = appears first).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this district is active and available for queries.
    /// Inactive districts can be archived without deletion.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Parent district code, if this is a sub-district.
    /// Null if this is a top-level district.
    /// Used for hierarchical neighborhood organization.
    /// </summary>
    public string? ParentDistrictCode { get; set; }

    /// <summary>
    /// Timestamp when this district was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public District() { }

    public District(
        string districtCode,
        string displayName,
        string marketCode,
        BoundingBox boundingBoxApproximation,
        int sortOrder = 0,
        string? description = null,
        string? parentDistrictCode = null)
    {
        DistrictCode = districtCode;
        DisplayName = displayName;
        MarketCode = marketCode;
        BoundingBoxApproximation = boundingBoxApproximation;
        SortOrder = sortOrder;
        Description = description;
        ParentDistrictCode = parentDistrictCode;
    }

    /// <summary>
    /// Checks if a coordinate point is within this district's bounding box approximation.
    /// </summary>
    public bool ApproximatelyContains(double latitude, double longitude) =>
        BoundingBoxApproximation.Contains(latitude, longitude);
}
