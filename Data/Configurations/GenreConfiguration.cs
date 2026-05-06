using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasIndex(g => g.NormalizedName).IsUnique();
        builder.Property(g => g.Name).HasMaxLength(200);
        builder.Property(g => g.NormalizedName).HasMaxLength(200);
    }
}
