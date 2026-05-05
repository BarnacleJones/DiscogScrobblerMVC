using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.HasIndex(a => a.Name).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(500);
        builder.HasMany(a => a.Releases).WithMany(r => r.Artists);
    }
}
