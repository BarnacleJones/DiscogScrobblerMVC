using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class DiscogsReleaseImagesConfiguration : IEntityTypeConfiguration<DiscogsReleaseImages>
{
    public void Configure(EntityTypeBuilder<DiscogsReleaseImages> builder)
    {
        builder.HasIndex(r => r.DiscogsReleaseId).IsUnique();
        builder.HasOne(r => r.Release)
               .WithOne(r => r.Images)
               .HasForeignKey<DiscogsReleaseImages>(r => r.DiscogsReleaseId)
               .HasPrincipalKey<Release>(r => r.DiscogsReleaseId);
        builder.Property(r => r.CoverUrl).HasMaxLength(2000);
    }
}
