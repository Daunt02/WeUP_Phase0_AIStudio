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
    public DbSet<SavedEventEntity> SavedEvents => Set<SavedEventEntity>();
    public DbSet<EventReviewEntity> EventReviews => Set<EventReviewEntity>();
    public DbSet<IngestionJobEntity> IngestionJobs => Set<IngestionJobEntity>();
    public DbSet<IngestionEvidenceEntity> IngestionEvidence => Set<IngestionEvidenceEntity>();
    public DbSet<IngestionCandidateEntity> IngestionCandidates => Set<IngestionCandidateEntity>();
    public DbSet<IngestionAuditEntity> IngestionAudits => Set<IngestionAuditEntity>();

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
            b.Property(e => e.Email).HasMaxLength(320).IsRequired();
            b.HasIndex(e => e.Email).IsUnique();
            b.Property(e => e.DisplayName).HasMaxLength(128);
            b.Property(e => e.HomeMarket).HasMaxLength(64);
            b.Property(e => e.OnboardingState).HasMaxLength(32).IsRequired();
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
            b.Property(e => e.SourceKind).HasMaxLength(64).IsRequired();
            b.Property(e => e.SourceRef).HasMaxLength(1024).IsRequired();
            b.Property(e => e.Status).HasMaxLength(32).IsRequired();
            b.Property(e => e.FailureReason).HasMaxLength(2000);
            b.Property(e => e.CandidateEventId).HasMaxLength(64);
            b.HasIndex(e => e.JobId).IsUnique();
            b.HasIndex(e => e.SourceRef);
        });

        modelBuilder.Entity<IngestionEvidenceEntity>(b =>
        {
            b.ToTable("ingestion_evidence");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Kind).HasMaxLength(64).IsRequired();
            b.Property(e => e.Reference).HasMaxLength(1024).IsRequired();
            b.Property(e => e.Payload).HasMaxLength(4000);
        });

        modelBuilder.Entity<IngestionCandidateEntity>(b =>
        {
            b.ToTable("ingestion_candidates");
            b.HasKey(e => e.Id);
            b.Property(e => e.JobId).HasMaxLength(64).IsRequired();
            b.Property(e => e.CandidateJson).HasColumnType("jsonb").IsRequired();
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
    }
}
