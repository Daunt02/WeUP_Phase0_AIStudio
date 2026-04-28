using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class IngestionEvidenceConfiguration : IEntityTypeConfiguration<IngestionEvidenceEntity>
{
    public void Configure(EntityTypeBuilder<IngestionEvidenceEntity> builder)
    {
        builder.ToTable("ingestion_evidence");

        builder.HasKey(e => e.Id);

        // v1.0 candidate-evidence fields.
        builder.Property(e => e.CandidateId).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RawMatchedText).IsRequired();
        builder.Property(e => e.LocalConfidence).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.CandidateId, e.Source, e.RawMatchedText }).IsUnique();

        // Existing orchestration fields.
        builder.Property(e => e.JobId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EvidenceId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(256);
        builder.Property(e => e.Payload).HasMaxLength(4000);
        builder.Property(e => e.MetadataJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => e.EvidenceId);

        builder.Property(e => e.Source).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.RawMatchedText).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.LocalConfidence).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.CandidateId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.CreatedAtUtc).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
