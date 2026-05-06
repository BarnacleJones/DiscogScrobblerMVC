using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.HasIndex(t => new { t.ReleaseId, t.Position });
        builder.Property(t => t.Position).HasMaxLength(20);
        builder.Property(t => t.Title).HasMaxLength(500);
        builder.Property(t => t.Duration).HasMaxLength(20);
        builder.HasOne(t => t.Release).WithMany(r => r.Tracks).HasForeignKey(t => t.ReleaseId);
    }
}
