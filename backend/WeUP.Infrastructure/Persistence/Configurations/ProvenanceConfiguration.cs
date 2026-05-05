using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using WeUP.Infrastructure.Resolution;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class ProvenanceConfiguration : IEntityTypeConfiguration<ProvenanceEntity>
{
    public void Configure(EntityTypeBuilder<ProvenanceEntity> builder)
    {
        builder.ToTable("Provenance");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.FieldName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.ChangedBy).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Reason).IsRequired().HasMaxLength(500);

        // Append‑only: prevent updates/deletes via EF by throwing if an update is attempted after save.
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }

        // Also prevent cascade deletes or relationships from causing deletes.
        builder.Metadata.SetIsTableExcludedFromMigrations(false);
    }
}
