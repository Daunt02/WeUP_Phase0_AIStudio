using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeUP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entity_resolution_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CandidateSourceRef = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CanonicalEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_resolution_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VenueName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    FlyerAssetIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_submissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CanonicalTitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CanonicalDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    VenueName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AddressCity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AddressState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AddressPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AddressCountry = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    AddressRaw = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TagsCsv = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "flyer_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalAssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProvenanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FlyerType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OcrText = table.Column<string>(type: "text", nullable: true),
                    OcrExtractionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OcrEngineVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OcrBlocksJson = table.Column<string>(type: "jsonb", nullable: true),
                    OcrReady = table.Column<bool>(type: "boolean", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    DerivativeAssetIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProcessingHistoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidationFailuresJson = table.Column<string>(type: "jsonb", nullable: true),
                    IngestionJobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ModerationItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CanonicalEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LinkedWorkflowIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    NormalizationRunId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NormalizationVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NormalizationSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewReasonsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flyer_evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "flyer_provenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvenanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceTier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UploaderUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UploadOrigin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmitterHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BaselineAuthority = table.Column<double>(type: "double precision", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IngestionJobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PartnerProvider = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmitterNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flyer_provenance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CandidateSourceRef = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CandidateJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Payload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IssuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    AdapterKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AdapterVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsRetrySafe = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CandidateEventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OriginalFilename = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CanonicalContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WidthPx = table.Column<int>(type: "integer", nullable: true),
                    HeightPx = table.Column<int>(type: "integer", nullable: true),
                    IsAnimated = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploaderUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UploadOrigin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OwnerType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VenueId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IngestionWorkflowSource = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageContainer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    StorageUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StorageETag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StorageVersionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "media_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InitializedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_uploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    HomeMarket = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OnboardingState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "video_derived_frames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVideoAssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DerivedAssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcessingJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploaderUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VenueId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ModerationItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TimestampOffsetMs = table.Column<long>(type: "bigint", nullable: false),
                    FrameType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WidthPx = table.Column<int>(type: "integer", nullable: false),
                    HeightPx = table.Column<int>(type: "integer", nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageContainer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    StorageUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtractionStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtractionVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPosterSelected = table.Column<bool>(type: "boolean", nullable: false),
                    PosterSelectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_derived_frames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "video_flyer_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploaderUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VenueId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ModerationItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IngestionJobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageContainer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    StorageUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    WidthPx = table.Column<int>(type: "integer", nullable: true),
                    HeightPx = table.Column<int>(type: "integer", nullable: true),
                    DetectedCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BitrateKbps = table.Column<int>(type: "integer", nullable: true),
                    ProcessingJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PosterAssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_flyer_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "video_flyer_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InitializedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_flyer_uploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "video_processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CurrentStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StageHistoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_processing_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_media_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ChangeRequestInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequiredReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PublishDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_reviews_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtractionVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmitterId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_sources_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_events",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_events", x => new { x.UserId, x.EventId });
                    table.ForeignKey(
                        name: "FK_saved_events_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_events_user_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "user_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_resolution_records_CandidateSourceRef",
                table: "entity_resolution_records",
                column: "CandidateSourceRef");

            migrationBuilder.CreateIndex(
                name: "IX_entity_resolution_records_ResolutionId",
                table: "entity_resolution_records",
                column: "ResolutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_resolution_records_Status",
                table: "entity_resolution_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_event_media_EventId",
                table: "event_media",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_reviews_EventId_ReviewStatus",
                table: "event_reviews",
                columns: new[] { "EventId", "ReviewStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_event_reviews_ReviewedAt",
                table: "event_reviews",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_event_sources_EventId_SourceKind",
                table: "event_sources",
                columns: new[] { "EventId", "SourceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_event_sources_SourceRef",
                table: "event_sources",
                column: "SourceRef");

            migrationBuilder.CreateIndex(
                name: "IX_event_submissions_SubmissionId",
                table: "event_submissions",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_submissions_SubmittedByUserId",
                table: "event_submissions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_event_submissions_SubmittedByUserId_Status",
                table: "event_submissions",
                columns: new[] { "SubmittedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_events_Category",
                table: "events",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_events_Latitude_Longitude",
                table: "events",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_events_Latitude_Longitude_StartUtc",
                table: "events",
                columns: new[] { "Latitude", "Longitude", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_events_PublicId",
                table: "events",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_StartUtc",
                table: "events",
                column: "StartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_events_StartUtc_Status",
                table: "events",
                columns: new[] { "StartUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_events_Status",
                table: "events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueId",
                table: "events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_AssetId",
                table: "flyer_evidence",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_CanonicalEventId",
                table: "flyer_evidence",
                column: "CanonicalEventId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_EvidenceId",
                table: "flyer_evidence",
                column: "EvidenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_IngestionJobId",
                table: "flyer_evidence",
                column: "IngestionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_ModerationItemId",
                table: "flyer_evidence",
                column: "ModerationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_NormalizationRunId",
                table: "flyer_evidence",
                column: "NormalizationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_OcrExtractionId",
                table: "flyer_evidence",
                column: "OcrExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_ProvenanceId",
                table: "flyer_evidence",
                column: "ProvenanceId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_Status",
                table: "flyer_evidence",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_evidence_SubmissionId",
                table: "flyer_evidence",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_AssetId",
                table: "flyer_provenance",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_IngestionJobId",
                table: "flyer_provenance",
                column: "IngestionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_ProvenanceId",
                table: "flyer_provenance",
                column: "ProvenanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_SubmissionId",
                table: "flyer_provenance",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_SubmitterHash",
                table: "flyer_provenance",
                column: "SubmitterHash");

            migrationBuilder.CreateIndex(
                name: "IX_flyer_provenance_UploaderUserId",
                table: "flyer_provenance",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_candidates_JobId",
                table: "ingestion_candidates",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_evidence_EvidenceId",
                table: "ingestion_evidence",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_IdempotencyKey",
                table: "ingestion_jobs",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_JobId",
                table: "ingestion_jobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_RequestId",
                table: "ingestion_jobs",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_SourceRef",
                table: "ingestion_jobs",
                column: "SourceRef");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_AssetId",
                table: "media_assets",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_ContentHash",
                table: "media_assets",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_OwnerType_OwnerId",
                table: "media_assets",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_Status",
                table: "media_assets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_SubmissionId",
                table: "media_assets",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_UploaderUserId",
                table: "media_assets",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_AssetId",
                table: "media_uploads",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_Status",
                table: "media_uploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_UploadId",
                table: "media_uploads",
                column: "UploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_events_EventId",
                table: "saved_events",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_events_UserId",
                table: "saved_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_Email",
                table: "user_profiles",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_PublicId",
                table: "user_profiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_derived_frames_DerivedAssetId",
                table: "video_derived_frames",
                column: "DerivedAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_derived_frames_ProcessingJobId",
                table: "video_derived_frames",
                column: "ProcessingJobId");

            migrationBuilder.CreateIndex(
                name: "IX_video_derived_frames_SourceVideoAssetId",
                table: "video_derived_frames",
                column: "SourceVideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_video_derived_frames_SourceVideoAssetId_IsPosterSelected",
                table: "video_derived_frames",
                columns: new[] { "SourceVideoAssetId", "IsPosterSelected" });

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_AssetId",
                table: "video_flyer_assets",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_IngestionJobId",
                table: "video_flyer_assets",
                column: "IngestionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_ModerationItemId",
                table: "video_flyer_assets",
                column: "ModerationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_Status",
                table: "video_flyer_assets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_SubmissionId",
                table: "video_flyer_assets",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_UploaderUserId",
                table: "video_flyer_assets",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_assets_VenueId",
                table: "video_flyer_assets",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_uploads_AssetId",
                table: "video_flyer_uploads",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_uploads_Status",
                table: "video_flyer_uploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_video_flyer_uploads_UploadId",
                table: "video_flyer_uploads",
                column: "UploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_AssetId",
                table: "video_processing_jobs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_JobId",
                table: "video_processing_jobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_Status",
                table: "video_processing_jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_resolution_records");

            migrationBuilder.DropTable(
                name: "event_media");

            migrationBuilder.DropTable(
                name: "event_reviews");

            migrationBuilder.DropTable(
                name: "event_sources");

            migrationBuilder.DropTable(
                name: "event_submissions");

            migrationBuilder.DropTable(
                name: "flyer_evidence");

            migrationBuilder.DropTable(
                name: "flyer_provenance");

            migrationBuilder.DropTable(
                name: "ingestion_audit");

            migrationBuilder.DropTable(
                name: "ingestion_candidates");

            migrationBuilder.DropTable(
                name: "ingestion_evidence");

            migrationBuilder.DropTable(
                name: "ingestion_jobs");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "media_uploads");

            migrationBuilder.DropTable(
                name: "saved_events");

            migrationBuilder.DropTable(
                name: "video_derived_frames");

            migrationBuilder.DropTable(
                name: "video_flyer_assets");

            migrationBuilder.DropTable(
                name: "video_flyer_uploads");

            migrationBuilder.DropTable(
                name: "video_processing_jobs");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "user_profiles");
        }
    }
}
