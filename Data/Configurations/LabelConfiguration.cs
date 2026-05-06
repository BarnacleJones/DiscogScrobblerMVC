using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasIndex(l => l.Name);
        builder.HasIndex(l => l.DiscogsLabelId).HasFilter("\"DiscogsLabelId\" IS NOT NULL");
        builder.Property(l => l.Name).HasMaxLength(500);
        builder.Property(l => l.DiscogsProfile).HasMaxLength(65535);
        builder.Property(l => l.DiscogsImageUrl).HasMaxLength(2048);
        builder.Property(l => l.LocalImageFilename).HasMaxLength(2000);
        builder.Property(l => l.LocalThumbnailFilename).HasMaxLength(2000);
        builder.HasMany(l => l.Releases).WithMany(r => r.Labels);
    }
}
