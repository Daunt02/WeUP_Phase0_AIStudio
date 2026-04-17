using Microsoft.EntityFrameworkCore;
using WeUP.Infrastructure.Persistence.Configurations;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// WeUP Phase 0 database context. Target: PostgreSQL.
/// Connection string configured via environment (see docs/configuration.md).
/// </summary>
public sealed class WeUpDbContext(DbContextOptions<WeUpDbContext> options) : DbContext(options)
{
    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<EventSourceEntity> EventSources => Set<EventSourceEntity>();
    public DbSet<EventMediaEntity> EventMedia => Set<EventMediaEntity>();
    public DbSet<UserProfileEntity> UserProfiles => Set<UserProfileEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<UserPreferencesEntity> UserPreferences => Set<UserPreferencesEntity>();
    public DbSet<SavedEventEntity> SavedEvents => Set<SavedEventEntity>();
    public DbSet<EventReviewEntity> EventReviews => Set<EventReviewEntity>();
    public DbSet<IngestionJobEntity> IngestionJobs => Set<IngestionJobEntity>();
    public DbSet<IngestionEvidenceEntity> IngestionEvidence => Set<IngestionEvidenceEntity>();
    public DbSet<IngestionCandidateEntity> IngestionCandidates => Set<IngestionCandidateEntity>();
    public DbSet<IngestionAuditEntity> IngestionAudits => Set<IngestionAuditEntity>();
    public DbSet<EntityResolutionRecordEntity> EntityResolutionRecords => Set<EntityResolutionRecordEntity>();
    public DbSet<EventMergeProvenanceEntity> EventMergeProvenance => Set<EventMergeProvenanceEntity>();
    public DbSet<MediaAssetEntity> MediaAssets => Set<MediaAssetEntity>();
    public DbSet<MediaUploadEntity> MediaUploads => Set<MediaUploadEntity>();
    public DbSet<FlyerProvenanceEntity> FlyerProvenances => Set<FlyerProvenanceEntity>();
    public DbSet<FlyerEvidenceEntity> FlyerEvidence => Set<FlyerEvidenceEntity>();
    public DbSet<VideoFlyerAssetEntity> VideoFlyerAssets => Set<VideoFlyerAssetEntity>();
    public DbSet<VideoFlyerUploadEntity> VideoFlyerUploads => Set<VideoFlyerUploadEntity>();
    public DbSet<VideoProcessingJobEntity> VideoProcessingJobs => Set<VideoProcessingJobEntity>();
    public DbSet<VideoDerivedFrameEntity> VideoDerivedFrames => Set<VideoDerivedFrameEntity>();
    public DbSet<EventSubmissionEntity> EventSubmissions => Set<EventSubmissionEntity>();
    public DbSet<ModerationQueueItemEntity> ModerationQueueItems => Set<ModerationQueueItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new EventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SavedEventEntityConfiguration());

        // Remaining entities use convention-based config for Phase 0
        modelBuilder.Entity<EventSourceEntity>(b =>
        {
            b.ToTable("event_sources");
            b.HasKey(e => e.Id);
            b.Property(e => e.SourceKind).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceRef).HasMaxLength(512).IsRequired();
            b.Property(e => e.ExtractionVersion).HasMaxLength(32).IsRequired();
            b.Property(e => e.SubmitterId).HasMaxLength(128);
            b.HasIndex(e => new { e.EventId, e.SourceKind });
            b.HasIndex(e => e.SourceRef);  // dedupe lookup
        });

        modelBuilder.Entity<EventMediaEntity>(b =>
        {
            b.ToTable("event_media");
            b.HasKey(e => e.Id);
            b.Property(e => e.AssetId).HasMaxLength(256).IsRequired();
            b.Property(e => e.Url).HasMaxLength(2048).IsRequired();
            b.Property(e => e.Kind).HasMaxLength(32).IsRequired();
            b.HasIndex(e => e.EventId);
        });

        modelBuilder.Entity<UserProfileEntity>(b =>
        {
            b.ToTable("user_profiles");
            b.HasKey(e => e.Id);
            b.Property(e => e.PublicId).HasMaxLength(128).IsRequired();
            b.Property(e => e.Email).HasMaxLength(320).IsRequired();
            b.HasIndex(e => e.PublicId).IsUnique();
            b.HasIndex(e => e.Email).IsUnique();
            b.Property(e => e.DisplayName).HasMaxLength(128);
            b.Property(e => e.HomeMarket).HasMaxLength(64);
            b.Property(e => e.OnboardingState).HasMaxLength(32).IsRequired();

            b.HasMany(e => e.Roles)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Preferences)
                .WithOne(e => e.User)
                .HasForeignKey<UserPreferencesEntity>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRoleEntity>(b =>
        {
            b.ToTable("user_roles");
            b.HasKey(e => new { e.UserId, e.Role });
            b.Property(e => e.Role).HasMaxLength(64).IsRequired();
            b.HasIndex(e => e.Role);
        });

        modelBuilder.Entity<UserPreferencesEntity>(b =>
        {
            b.ToTable("user_preferences");
            b.HasKey(e => e.UserId);
            b.Property(e => e.PreferredCategoriesJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.HomeRadiusMeters).IsRequired();
            b.Property(e => e.PreferredTimeZone).HasMaxLength(64);
        });

        modelBuilder.Entity<EventSubmissionEntity>(b =>
        {
            b.ToTable("event_submissions");
            b.HasKey(e => e.Id);
            b.Property(e => e.SubmissionId).HasMaxLength(128).IsRequired();
            b.Property(e => e.SubmittedByUserId).HasMaxLength(128).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.Title).HasMaxLength(512);
            b.Property(e => e.VenueName).HasMaxLength(256);
            b.Property(e => e.Address).HasMaxLength(1024);
            b.Property(e => e.Timezone).HasMaxLength(64);
            b.Property(e => e.Category).HasMaxLength(64);
            b.Property(e => e.Description).HasMaxLength(4000);
            b.Property(e => e.TagsJson).HasColumnType("jsonb");
            b.Property(e => e.FlyerAssetIdsJson).HasColumnType("jsonb");
            b.Property(e => e.ReviewNote).HasMaxLength(4000);
            b.HasIndex(e => e.SubmissionId).IsUnique();
            b.HasIndex(e => e.SubmittedByUserId);
            b.HasIndex(e => new { e.SubmittedByUserId, e.Status });
        });

        modelBuilder.Entity<EventReviewEntity>(b =>
        {
            b.ToTable("event_reviews");
            b.HasKey(e => e.Id);
            b.Property(e => e.ReviewStatus).HasMaxLength(32).IsRequired();
            b.Property(e => e.ReviewerId).HasMaxLength(128);
            b.Property(e => e.RejectionReason).HasMaxLength(1024);
            b.Property(e => e.Notes).HasMaxLength(4000);
            b.Property(e => e.ChangeRequestInstructions).HasMaxLength(4000);
            b.Property(e => e.RequiredReason).HasMaxLength(512);
            b.Property(e => e.PublishDecision).HasMaxLength(32);
            b.HasIndex(e => new { e.EventId, e.ReviewStatus });
            b.HasIndex(e => e.ReviewedAt);
        });

        // Ingestion job entities
        modelBuilder.Entity<IngestionJobEntity>(b =>
        {
            b.ToTable("ingestion_jobs");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.RequestId).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceKind).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceRef).HasMaxLength(1024).IsRequired();
            b.Property(e => e.SubmittedBy).HasMaxLength(256).IsRequired();
            b.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
            b.Property(e => e.RawPayloadJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.MetadataJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Status).HasMaxLength(32).IsRequired();
            b.Property(e => e.FailureReason).HasMaxLength(2000);
            b.Property(e => e.IssuesJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.AdapterKey).HasMaxLength(128);
            b.Property(e => e.AdapterVersion).HasMaxLength(64);
            b.Property(e => e.CandidateEventId).HasMaxLength(64);
            b.HasIndex(e => e.JobId).IsUnique();
            b.HasIndex(e => e.RequestId).IsUnique();
            b.HasIndex(e => e.SourceRef);
            b.HasIndex(e => e.IdempotencyKey);
        });

        modelBuilder.Entity<IngestionEvidenceEntity>(b =>
        {
            b.ToTable("ingestion_evidence");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.EvidenceId).HasMaxLength(128).IsRequired();
            b.Property(e => e.Kind).HasMaxLength(64).IsRequired();
            b.Property(e => e.Reference).HasMaxLength(1024).IsRequired();
            b.Property(e => e.MimeType).HasMaxLength(256);
            b.Property(e => e.Payload).HasMaxLength(4000);
            b.Property(e => e.MetadataJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(e => e.EvidenceId);
        });

        modelBuilder.Entity<IngestionCandidateEntity>(b =>
        {
            b.ToTable("ingestion_candidates");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.CandidateSourceRef).HasMaxLength(1024).IsRequired();
            b.Property(e => e.CandidateJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(e => e.JobId).IsUnique();
        });

        modelBuilder.Entity<IngestionAuditEntity>(b =>
        {
            b.ToTable("ingestion_audit");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Stage).HasMaxLength(64).IsRequired();
            b.Property(e => e.Detail).HasMaxLength(2000);
            b.Property(e => e.Timestamp).IsRequired();
        });

        modelBuilder.Entity<EntityResolutionRecordEntity>(b =>
        {
            b.ToTable("entity_resolution_records");
            b.HasKey(e => e.Id);
            b.Property(e => e.ResolutionId).HasMaxLength(64).IsRequired();
            b.Property(e => e.CandidateSourceRef).HasMaxLength(1024).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.ResultJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.CanonicalEventId).HasMaxLength(128);
            b.HasIndex(e => e.ResolutionId).IsUnique();
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.CandidateSourceRef);
        });

        modelBuilder.Entity<EventMergeProvenanceEntity>(b =>
        {
            b.ToTable("event_merge_provenance");
            b.HasKey(e => e.Id);
            b.Property(e => e.EntryId).HasMaxLength(160).IsRequired();
            b.Property(e => e.CanonicalEventId).HasMaxLength(128).IsRequired();
            b.Property(e => e.ResolutionId).HasMaxLength(64).IsRequired();
            b.Property(e => e.MergeActor).HasMaxLength(256).IsRequired();
            b.Property(e => e.MergeReason).HasMaxLength(4000).IsRequired();
            b.Property(e => e.EntryJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(e => e.EntryId).IsUnique();
            b.HasIndex(e => new { e.CanonicalEventId, e.SequenceNumber }).IsUnique();
            b.HasIndex(e => e.RecordedAtUtc);
        });

        modelBuilder.Entity<MediaAssetEntity>(b =>
        {
            b.ToTable("media_assets");
            b.HasKey(e => e.Id);
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetType).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            b.Property(e => e.ChecksumSha256).HasMaxLength(128).IsRequired();
            b.Property(e => e.ContentHash).HasMaxLength(128);
            b.Property(e => e.OriginalFilename).HasMaxLength(512).IsRequired();
            b.Property(e => e.CanonicalContentType).HasMaxLength(128);
            b.Property(e => e.WidthPx);
            b.Property(e => e.HeightPx);
            b.Property(e => e.IsAnimated);
            b.Property(e => e.UploaderUserId).HasMaxLength(128);
            b.Property(e => e.UploadOrigin).HasMaxLength(64);
            b.Property(e => e.SourceType).HasMaxLength(64);
            b.Property(e => e.OwnerType).HasMaxLength(64).IsRequired();
            b.Property(e => e.OwnerId).HasMaxLength(128);
            b.Property(e => e.VenueId).HasMaxLength(128);
            b.Property(e => e.IngestionWorkflowSource).HasMaxLength(256);
            b.Property(e => e.StorageProvider).HasMaxLength(64).IsRequired();
            b.Property(e => e.StorageContainer).HasMaxLength(256).IsRequired();
            b.Property(e => e.StorageObjectKey).HasMaxLength(1024).IsRequired();
            b.Property(e => e.StorageUri).HasMaxLength(2048);
            b.Property(e => e.StorageETag).HasMaxLength(256);
            b.Property(e => e.StorageVersionId).HasMaxLength(256);
            b.Property(e => e.SubmissionId).HasMaxLength(128);
            b.Property(e => e.MetadataJson).HasColumnType("jsonb");
            b.HasIndex(e => e.AssetId).IsUnique();
            b.HasIndex(e => new { e.OwnerType, e.OwnerId });
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.UploaderUserId);
            b.HasIndex(e => e.SubmissionId);
            b.HasIndex(e => e.ContentHash);
        });

        modelBuilder.Entity<MediaUploadEntity>(b =>
        {
            b.ToTable("media_uploads");
            b.HasKey(e => e.Id);
            b.Property(e => e.UploadId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.RequestedByUserId).HasMaxLength(128);
            b.Property(e => e.FailureReason).HasMaxLength(2000);
            b.HasIndex(e => e.UploadId).IsUnique();
            b.HasIndex(e => e.AssetId);
            b.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<FlyerProvenanceEntity>(b =>
        {
            b.ToTable("flyer_provenance");
            b.HasKey(e => e.Id);
            b.Property(e => e.ProvenanceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceTier).HasMaxLength(32).IsRequired();
            b.Property(e => e.UploaderUserId).HasMaxLength(128);
            b.Property(e => e.UploadOrigin).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
            b.Property(e => e.SubmitterHash).HasMaxLength(128).IsRequired();
            b.Property(e => e.SourceUrl).HasMaxLength(2048);
            b.Property(e => e.SubmissionId).HasMaxLength(128);
            b.Property(e => e.IngestionJobId).HasMaxLength(128);
            b.Property(e => e.PartnerProvider).HasMaxLength(256);
            b.Property(e => e.SubmitterNote).HasMaxLength(4000);
            b.HasIndex(e => e.ProvenanceId).IsUnique();
            b.HasIndex(e => e.AssetId);
            b.HasIndex(e => e.UploaderUserId);
            b.HasIndex(e => e.SubmitterHash);
            b.HasIndex(e => e.SubmissionId);
            b.HasIndex(e => e.IngestionJobId);
        });

        modelBuilder.Entity<FlyerEvidenceEntity>(b =>
        {
            b.ToTable("flyer_evidence");
            b.HasKey(e => e.Id);
            b.Property(e => e.EvidenceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.OriginalAssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.ProvenanceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.EventId).HasMaxLength(128);
            b.Property(e => e.SubmissionId).HasMaxLength(128);
            b.Property(e => e.FlyerType).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.OcrReady).IsRequired();
            b.Property(e => e.OcrText).HasColumnType("text");
            b.Property(e => e.OcrExtractionId).HasMaxLength(128);
            b.Property(e => e.OcrEngineVersion).HasMaxLength(128);
            b.Property(e => e.OcrBlocksJson).HasColumnType("jsonb");
            b.Property(e => e.DerivativeAssetIdsJson).HasColumnType("jsonb");
            b.Property(e => e.ProcessingHistoryJson).HasColumnType("jsonb");
            b.Property(e => e.ValidationFailuresJson).HasColumnType("jsonb");
            b.Property(e => e.IngestionJobId).HasMaxLength(128);
            b.Property(e => e.ModerationItemId).HasMaxLength(128);
            b.Property(e => e.CanonicalEventId).HasMaxLength(128);
            b.Property(e => e.LinkedWorkflowIdsJson).HasColumnType("jsonb");
            b.Property(e => e.NormalizationRunId).HasMaxLength(128);
            b.Property(e => e.NormalizationVersion).HasMaxLength(128);
            b.Property(e => e.NormalizationSnapshotJson).HasColumnType("jsonb");
            b.Property(e => e.ReviewReasonsJson).HasColumnType("jsonb");
            b.Property(e => e.ReviewNotes).HasMaxLength(4000);
            b.HasIndex(e => e.EvidenceId).IsUnique();
            b.HasIndex(e => e.AssetId);
            b.HasIndex(e => e.ProvenanceId);
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.SubmissionId);
            b.HasIndex(e => e.IngestionJobId);
            b.HasIndex(e => e.OcrExtractionId);
            b.HasIndex(e => e.NormalizationRunId);
            b.HasIndex(e => e.ModerationItemId);
            b.HasIndex(e => e.CanonicalEventId);
        });

        // P28: Video flyer intake entities
        modelBuilder.Entity<VideoFlyerAssetEntity>(b =>
        {
            b.ToTable("video_flyer_assets");
            b.HasKey(e => e.Id);
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            b.Property(e => e.ChecksumSha256).HasMaxLength(128).IsRequired();
            b.Property(e => e.OriginalFilename).HasMaxLength(512).IsRequired();
            b.Property(e => e.UploaderUserId).HasMaxLength(128);
            b.Property(e => e.SubmissionId).HasMaxLength(128);
            b.Property(e => e.VenueId).HasMaxLength(128);
            b.Property(e => e.ModerationItemId).HasMaxLength(128);
            b.Property(e => e.IngestionJobId).HasMaxLength(128);
            b.Property(e => e.StorageProvider).HasMaxLength(64).IsRequired();
            b.Property(e => e.StorageContainer).HasMaxLength(256).IsRequired();
            b.Property(e => e.StorageObjectKey).HasMaxLength(1024).IsRequired();
            b.Property(e => e.StorageUri).HasMaxLength(2048);
            b.Property(e => e.DetectedCodec).HasMaxLength(64);
            b.Property(e => e.ProcessingJobId).HasMaxLength(64);
            b.Property(e => e.PosterAssetId).HasMaxLength(64);
            b.HasIndex(e => e.AssetId).IsUnique();
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.UploaderUserId);
            b.HasIndex(e => e.SubmissionId);
            b.HasIndex(e => e.VenueId);
            b.HasIndex(e => e.ModerationItemId);
            b.HasIndex(e => e.IngestionJobId);
        });

        modelBuilder.Entity<VideoFlyerUploadEntity>(b =>
        {
            b.ToTable("video_flyer_uploads");
            b.HasKey(e => e.Id);
            b.Property(e => e.UploadId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.RequestedByUserId).HasMaxLength(128);
            b.Property(e => e.FailureReason).HasMaxLength(2000);
            b.HasIndex(e => e.UploadId).IsUnique();
            b.HasIndex(e => e.AssetId);
            b.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<VideoProcessingJobEntity>(b =>
        {
            b.ToTable("video_processing_jobs");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.AssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.FailureReason).HasMaxLength(2000);
            b.Property(e => e.CurrentStage).HasMaxLength(64);
            b.Property(e => e.StageHistoryJson).HasColumnType("jsonb");
            b.Property(e => e.ResultJson).HasColumnType("jsonb");
            b.HasIndex(e => e.JobId).IsUnique();
            b.HasIndex(e => e.AssetId);
            b.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<VideoDerivedFrameEntity>(b =>
        {
            b.ToTable("video_derived_frames");
            b.HasKey(e => e.Id);
            b.Property(e => e.SourceVideoAssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.DerivedAssetId).HasMaxLength(64).IsRequired();
            b.Property(e => e.ProcessingJobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.UploaderUserId).HasMaxLength(128);
            b.Property(e => e.SubmissionId).HasMaxLength(128);
            b.Property(e => e.VenueId).HasMaxLength(128);
            b.Property(e => e.ModerationItemId).HasMaxLength(128);
            b.Property(e => e.FrameType).HasMaxLength(64).IsRequired();
            b.Property(e => e.StorageProvider).HasMaxLength(64).IsRequired();
            b.Property(e => e.StorageContainer).HasMaxLength(256).IsRequired();
            b.Property(e => e.StorageObjectKey).HasMaxLength(1024).IsRequired();
            b.Property(e => e.StorageUri).HasMaxLength(2048);
            b.Property(e => e.ExtractionStage).HasMaxLength(64).IsRequired();
            b.Property(e => e.ExtractionVersion).HasMaxLength(64).IsRequired();
            b.Property(e => e.PosterSelectionReason).HasMaxLength(2000);
            b.HasIndex(e => e.SourceVideoAssetId);
            b.HasIndex(e => e.ProcessingJobId);
            b.HasIndex(e => new { e.SourceVideoAssetId, e.IsPosterSelected });
            b.HasIndex(e => e.DerivedAssetId).IsUnique();
        });

        modelBuilder.Entity<ModerationQueueItemEntity>(b =>
        {
            b.ToTable("moderation_queue_items");
            b.HasKey(e => e.Id);
            b.Property(e => e.ItemId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Kind).HasMaxLength(64).IsRequired();
            b.Property(e => e.Status).HasMaxLength(64).IsRequired();
            b.Property(e => e.CandidateJson).HasColumnType("jsonb");
            b.Property(e => e.ProvenanceJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.ConfidenceJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.DedupeMatchJson).HasColumnType("jsonb");
            b.Property(e => e.IngestionJobJson).HasColumnType("jsonb");
            b.Property(e => e.ReviewReasonsJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.HistoryJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.AssignedReviewerId).HasMaxLength(128);
            b.Property(e => e.LinkedEventId).HasMaxLength(128);
            b.HasIndex(e => e.ItemId).IsUnique();
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.Kind);
            b.HasIndex(e => e.AssignedReviewerId);
            b.HasIndex(e => e.CreatedAt);
        });
    }
}
