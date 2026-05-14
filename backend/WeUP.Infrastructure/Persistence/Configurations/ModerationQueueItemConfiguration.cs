using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class ModerationQueueItemConfiguration : IEntityTypeConfiguration<ModerationQueueItemEntity>
{
    public void Configure(EntityTypeBuilder<ModerationQueueItemEntity> builder)
    {
        builder.ToTable("ModerationQueue");
        builder.HasKey(e => e.QueueItemId);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("NOW()");

        // Append‑only semantics – disallow updates via EF by throwing.
        builder.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
