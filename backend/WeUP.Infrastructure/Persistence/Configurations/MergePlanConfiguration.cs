using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class MergePlanConfiguration : IEntityTypeConfiguration<MergePlanEntity>
{
    public void Configure(EntityTypeBuilder<MergePlanEntity> builder)
    {
        builder.ToTable("MergePlans");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TitleAction).IsRequired();
        builder.Property(e => e.LocationAction).IsRequired();
        builder.Property(e => e.TimeAction).IsRequired();
        builder.Property(e => e.TagsAction).IsRequired();

        builder.Property(e => e.ConflictsJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("NOW()");
    }
}
