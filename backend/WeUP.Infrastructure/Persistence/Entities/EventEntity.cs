namespace WeUP.Infrastructure.Persistence.Entities;

/// <summary>
/// Canonical published/reviewable event record.
/// Status lifecycle: Draft → Ingested → NeedsReview → Approved → Published → Archived/Rejected
/// </summary>
public sealed class EventEntity
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";

    // Canonical content
    public string CanonicalTitle { get; set; } = string.Empty;
    public string? CanonicalDescription { get; set; }
    public string Category { get; set; } = "other";

    // Venue (denormalized snapshot — FK to Venue added when venue entities exist)
    public Guid? VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;

    // Location
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string? AddressState { get; set; }
    public string? AddressPostalCode { get; set; }
    public string AddressCountry { get; set; } = "US";
    public string AddressRaw { get; set; } = string.Empty;

    // Canonical locality dimensions (Phase 0 hardening).
    public string? MarketCode { get; set; }
    public string? DistrictCode { get; set; }
    public string? NeighborhoodCode { get; set; }

    // Geospatial (PostGIS-ready; lat/lng stored until PostGIS extension is enabled)
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Temporal
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string Timezone { get; set; } = "America/Chicago";

    // Tags (stored as comma-separated string for Phase 0; upgrade to text[] with PostGIS migration)
    public string? TagsCsv { get; set; }

    // Confidence (0–1)
    public double Confidence { get; set; }

    // Audit
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation
    public ICollection<EventSourceEntity> Sources { get; set; } = [];
    public ICollection<EventMediaEntity> Media { get; set; } = [];
    public ICollection<EventReviewEntity> Reviews { get; set; } = [];
    public ICollection<SavedEventEntity> Saves { get; set; } = [];
}
