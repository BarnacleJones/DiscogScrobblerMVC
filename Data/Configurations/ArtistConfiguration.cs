using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.HasIndex(a => a.Name);
        // Non-unique: Discogs collection payloads can repeat the same id with different name strings;
        // we match by id first and do not enforce global uniqueness on Discogs ids.
        builder.HasIndex(a => a.DiscogsArtistId).HasFilter("\"DiscogsArtistId\" IS NOT NULL");
        builder.Property(a => a.Name).HasMaxLength(500);
        builder.Property(a => a.LastFmArtistName).HasMaxLength(500);
        builder.Property(a => a.DiscogsProfile).HasMaxLength(65535);
        builder.Property(a => a.DiscogsImageUrl).HasMaxLength(2048);
        builder.Property(a => a.LocalImageFilename).HasMaxLength(2000);
        builder.Property(a => a.LocalThumbnailFilename).HasMaxLength(2000);
        builder.HasMany(a => a.Releases).WithMany(r => r.Artists);
    }
}
