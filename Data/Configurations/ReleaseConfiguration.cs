using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.HasIndex(r => r.DiscogsReleaseId).IsUnique();
        builder.Property(r => r.Album).HasMaxLength(500);
    }
}
