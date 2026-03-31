using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class SavedEventEntityConfiguration : IEntityTypeConfiguration<SavedEventEntity>
{
    public void Configure(EntityTypeBuilder<SavedEventEntity> builder)
    {
        builder.ToTable("saved_events");
        builder.HasKey(s => new { s.UserId, s.EventId });

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.EventId);

        builder.HasOne(s => s.User)
               .WithMany(u => u.Saves)
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Event)
               .WithMany(e => e.Saves)
               .HasForeignKey(s => s.EventId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
