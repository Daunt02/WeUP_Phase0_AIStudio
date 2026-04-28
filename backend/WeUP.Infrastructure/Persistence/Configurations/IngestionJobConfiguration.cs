// ------------------------------------------------------------
// File: WeUP.Infrastructure/Persistence/Configurations/IngestionJobConfiguration.cs
// M1-P05 – Ingestion Job Lifecycle and State Tracking v1.0
// ------------------------------------------------------------
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="IngestionJobEntity"/>.
/// NOTE: The schema is also configured inline in <c>WeUpDbContext.OnModelCreating</c>
/// for Phase 0 convention compatibility. This class is provided for reference and
/// can replace the inline block when the project migrates to full configuration-class pattern.
/// </summary>
public sealed class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJobEntity>
{
    public void Configure(EntityTypeBuilder<IngestionJobEntity> builder)
    {
        builder.ToTable("ingestion_jobs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.JobId)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(e => e.RequestId)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(e => e.SourceKind)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(e => e.SourceRef)
               .HasMaxLength(1024)
               .IsRequired();

        builder.Property(e => e.SubmittedBy)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(e => e.IdempotencyKey)
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(e => e.RawPayloadJson)
               .HasColumnType("jsonb")
               .IsRequired();

        builder.Property(e => e.MetadataJson)
               .HasColumnType("jsonb")
               .IsRequired();

        // Status stored as string for readability in the database
        builder.Property(e => e.Status)
               .HasMaxLength(32)
               .IsRequired();

        builder.Property(e => e.FailureReason)
               .HasMaxLength(2000);

        builder.Property(e => e.IssuesJson)
               .HasColumnType("jsonb")
               .IsRequired();

        builder.Property(e => e.AdapterKey)
               .HasMaxLength(128);

        builder.Property(e => e.AdapterVersion)
               .HasMaxLength(64);

        builder.Property(e => e.CandidateEventId)
               .HasMaxLength(64);

        builder.HasIndex(e => e.JobId).IsUnique();
        builder.HasIndex(e => e.RequestId).IsUnique();
        builder.HasIndex(e => e.SourceRef);
        builder.HasIndex(e => e.IdempotencyKey);
    }
}
