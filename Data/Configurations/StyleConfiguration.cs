using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class StyleConfiguration : IEntityTypeConfiguration<Style>
{
    public void Configure(EntityTypeBuilder<Style> builder)
    {
        builder.HasIndex(s => s.NormalizedName).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.NormalizedName).HasMaxLength(200);
    }
}
