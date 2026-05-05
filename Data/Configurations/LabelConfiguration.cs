using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasIndex(l => l.Name).IsUnique();
        builder.HasIndex(l => l.DiscogsLabelId).IsUnique().HasFilter("\"DiscogsLabelId\" IS NOT NULL");
        builder.Property(l => l.Name).HasMaxLength(500);
        builder.HasMany(l => l.Releases).WithMany(r => r.Labels);
    }
}
