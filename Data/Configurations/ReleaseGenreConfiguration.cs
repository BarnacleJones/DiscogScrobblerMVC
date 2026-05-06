using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class ReleaseGenreConfiguration : IEntityTypeConfiguration<ReleaseGenre>
{
    public void Configure(EntityTypeBuilder<ReleaseGenre> builder)
    {
        builder.HasKey(r => new { r.ReleaseId, r.GenreId });
        builder.HasOne(r => r.Release)
            .WithMany(rel => rel.GenreLinks)
            .HasForeignKey(r => r.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Genre)
            .WithMany(g => g.ReleaseLinks)
            .HasForeignKey(r => r.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
