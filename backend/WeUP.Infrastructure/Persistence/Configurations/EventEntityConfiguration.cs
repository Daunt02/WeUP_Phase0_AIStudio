using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Persistence.Configurations;

public sealed class EventEntityConfiguration : IEntityTypeConfiguration<EventEntity>
{
    public void Configure(EntityTypeBuilder<EventEntity> builder)
    {
        builder.ToTable("events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CanonicalTitle).HasMaxLength(512).IsRequired();
        builder.Property(e => e.CanonicalDescription).HasMaxLength(4000);
        builder.Property(e => e.Category).HasMaxLength(64).IsRequired();
        builder.Property(e => e.VenueName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.AddressLine1).HasMaxLength(512).IsRequired();
        builder.Property(e => e.AddressCity).HasMaxLength(128).IsRequired();
        builder.Property(e => e.AddressState).HasMaxLength(64);
        builder.Property(e => e.AddressPostalCode).HasMaxLength(20);
        builder.Property(e => e.AddressCountry).HasMaxLength(4).IsRequired();
        builder.Property(e => e.AddressRaw).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(128);

        // Indexes for Phase 0 query patterns
        builder.HasIndex(e => e.Status);                                      // status filter
        builder.HasIndex(e => e.PublicId).IsUnique();                         // stable external API id
        builder.HasIndex(e => e.StartUtc);                                    // date-window queries
        builder.HasIndex(e => new { e.StartUtc, e.Status });                  // calendar feed
        builder.HasIndex(e => new { e.Latitude, e.Longitude });               // spatial filter (pre-PostGIS)
        builder.HasIndex(e => new { e.Latitude, e.Longitude, e.StartUtc });   // map feed compound
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.VenueId);

        // Relationships
        builder.HasMany(e => e.Sources).WithOne(s => s.Event).HasForeignKey(s => s.EventId);
        builder.HasMany(e => e.Media).WithOne(m => m.Event).HasForeignKey(m => m.EventId);
        builder.HasMany(e => e.Reviews).WithOne(r => r.Event).HasForeignKey(r => r.EventId);
        builder.HasMany(e => e.Saves).WithOne(s => s.Event).HasForeignKey(s => s.EventId);
    }
}
