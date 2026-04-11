namespace WeUP.Infrastructure.Seed;

public sealed record Phase0SeedDataset(
    Phase0SeedMeta Meta,
    Phase0MarketSeed[] Markets,
    Phase0VenueSeed[] Venues,
    Phase0EventSeed[] Events,
    Phase0UserSeed[] Users,
    Phase0SaveSeed[] Saves,
    Phase0ModerationItemSeed[] ModerationItems,
    Phase0IngestionJobSeed[] IngestionJobs);

public sealed record Phase0SeedMeta(
    string SeedVersion,
    string FixedNow,
    string LaunchMarketCode,
    string Description);

public sealed record Phase0MarketSeed(
    string Code,
    string DisplayName,
    string Timezone,
    string Status,
    bool IsAcceptingSubmissions,
    string? Description,
    string? LaunchedAt,
    double CenterLat,
    double CenterLng,
    Phase0BoundingBoxSeed BoundingBox,
    string[] Districts);

public sealed record Phase0BoundingBoxSeed(double MinLat, double MaxLat, double MinLng, double MaxLng);

public sealed record Phase0VenueSeed(
    string VenueId,
    string MarketCode,
    string DistrictCode,
    string Name,
    string Address,
    double Latitude,
    double Longitude);

public sealed record Phase0EventSeed(
    string EventId,
    string VenueId,
    string Title,
    string Description,
    string Category,
    string PriceTier,
    string SourceKind,
    string Status,
    double Confidence,
    string StartsAtUtc,
    string EndsAtUtc,
    string ImageUrl,
    string[] Tags,
    int EnergyLevel,
    string Neighborhood);

public sealed record Phase0UserSeed(
    string UserId,
    string Email,
    string? DisplayName,
    string? HomeMarket,
    string OnboardingState,
    string CreatedAt);

public sealed record Phase0SaveSeed(string UserId, string EventId, string SavedAt);

public sealed record Phase0ModerationItemSeed(
    string ItemId,
    string Kind,
    string Status,
    Phase0CandidateSeed? Candidate,
    Phase0ProvenanceSeed Provenance,
    Phase0ConfidenceSeed Confidence,
    Phase0DedupeSeed? DedupeMatch,
    Phase0IngestionJobSummarySeed? IngestionJob,
    string[] ReviewReasons,
    string? AssignedReviewerId,
    string? LinkedEventId,
    string CreatedAt,
    string UpdatedAt,
    Phase0ReviewHistorySeed[] History);

public sealed record Phase0CandidateSeed(
    string? Title,
    string? VenueName,
    string? Address,
    string? StartUtc,
    string? EndUtc,
    string? Timezone,
    string? Category,
    string? Description,
    string[]? Tags,
    string SourceKind,
    string SourceRef);

public sealed record Phase0ProvenanceSeed(
    string SourceKind,
    string SourceRef,
    string? IngestionJobId,
    string[] EvidenceRefs,
    string SubmittedAt);

public sealed record Phase0ConfidenceSeed(
    double Extraction,
    double Geocode,
    double Temporal,
    double VenueMatch,
    double DupeRisk,
    double Aggregate,
    string Bucket,
    string[] ReviewBlockers);

public sealed record Phase0DedupeSeed(
    string? ExistingEventId,
    string? ExistingEventTitle,
    double MatchScore,
    string Severity,
    string[] MatchReasons);

public sealed record Phase0IngestionJobSummarySeed(
    string JobId,
    string Status,
    string? FailureReason,
    string CreatedAt,
    string? CompletedAt);

public sealed record Phase0ReviewHistorySeed(
    string Action,
    string ActorId,
    string? Note,
    string PreviousStatus,
    string NextStatus,
    string Timestamp);

public sealed record Phase0IngestionJobSeed(
    string JobId,
    string Status,
    string? CandidateEventId,
    string SourceKind,
    string SourceRef,
    string? FailureReason);

public sealed record Phase0SeedSnapshot(
    string SeedVersion,
    string FixedNow,
    int MarketCount,
    int VenueCount,
    int EventCount,
    int UserCount,
    int SaveCount,
    int ModerationItemCount,
    int IngestionJobCount,
    string DatasetPath);