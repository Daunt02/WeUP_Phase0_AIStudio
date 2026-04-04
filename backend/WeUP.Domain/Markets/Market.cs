using WeUP.Domain.Spatial;

namespace WeUP.Domain.Markets;

/// <summary>
/// Represents a geographic market/city where WeUP operates.
/// Markets define operational boundaries, timezone, and launch status.
/// Phase 0 focuses on a single launch market; future versions support multi-market expansion.
/// </summary>
public class Market
{
    /// <summary>
    /// Unique code for the market (e.g., "sf", "oakland", "la").
    /// Used for scoping queries and freeze rules.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable market name (e.g., "San Francisco").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// IANA timezone for this market (e.g., "America/Los_Angeles").
    /// Used for temporal queries and schedule interpretation.
    /// </summary>
    public string Timezone { get; set; } = string.Empty;

    /// <summary>
    /// Geographic center of the market (for map initialization, fallback geocoding).
    /// </summary>
    public double CenterLat { get; set; }
    public double CenterLng { get; set; }

    /// <summary>
    /// Bounding box approximation of the market (used for rough market containment checks).
    /// </summary>
    public BoundingBox? BoundingBoxApproximation { get; set; }

    /// <summary>
    /// Current operational status of the market.
    /// </summary>
    public MarketStatus Status { get; set; } = MarketStatus.Inactive;

    /// <summary>
    /// Whether this market is currently accepting new event submissions.
    /// Soft gate: violations trigger review; hard gate: submission rejected.
    /// </summary>
    public bool IsAcceptingSubmissions { get; set; } = false;

    /// <summary>
    /// Optional description of the market for documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp when the market was launched or activated.
    /// </summary>
    public DateTimeOffset? LaunchedAt { get; set; }

    /// <summary>
    /// Timestamp when the market was created in the system.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Market() { }

    public Market(
        string code,
        string displayName,
        string timezone,
        double centerLat,
        double centerLng,
        BoundingBox? boundingBox = null,
        string? description = null)
    {
        Code = code;
        DisplayName = displayName;
        Timezone = timezone;
        CenterLat = centerLat;
        CenterLng = centerLng;
        BoundingBoxApproximation = boundingBox;
        Description = description;
    }

    /// <summary>
    /// Checks if a coordinate is approximately within the market's bounding box.
    /// Used for early validation before detailed district checks.
    /// </summary>
    public bool ApproximatelyContains(double latitude, double longitude)
    {
        if (BoundingBoxApproximation == null)
            return false; // If no bbox set, conservative: assume outside
        return BoundingBoxApproximation.Contains(latitude, longitude);
    }
}

/// <summary>
/// Market operational status enumeration.
/// </summary>
public enum MarketStatus
{
    /// <summary>Planned but not yet launched.</summary>
    Planned = 0,

    /// <summary>Active and operational.</summary>
    Active = 1,

    /// <summary>Temporarily suspended.</summary>
    Suspended = 2,

    /// <summary>No longer operational.</summary>
    Archived = 3,

    /// <summary>In inactive/draft state.</summary>
    Inactive = 4,
}
