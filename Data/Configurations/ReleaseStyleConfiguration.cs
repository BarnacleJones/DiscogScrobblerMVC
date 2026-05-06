using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class ReleaseStyleConfiguration : IEntityTypeConfiguration<ReleaseStyle>
{
    public void Configure(EntityTypeBuilder<ReleaseStyle> builder)
    {
        builder.HasKey(r => new { r.ReleaseId, r.StyleId });
        builder.HasOne(r => r.Release)
            .WithMany(rel => rel.StyleLinks)
            .HasForeignKey(r => r.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Style)
            .WithMany(s => s.ReleaseLinks)
            .HasForeignKey(r => r.StyleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
