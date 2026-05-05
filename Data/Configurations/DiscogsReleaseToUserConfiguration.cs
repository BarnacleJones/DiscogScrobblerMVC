using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class DiscogsReleaseToUserConfiguration : IEntityTypeConfiguration<DiscogsReleaseToUser>
{
    public void Configure(EntityTypeBuilder<DiscogsReleaseToUser> builder)
    {
        builder.HasIndex(r => new { r.DiscogsReleaseId, r.UserId }).IsUnique();
        builder.HasOne(r => r.Release)
               .WithMany(r => r.UserAssociations)
               .HasForeignKey(r => r.DiscogsReleaseId)
               .HasPrincipalKey(r => r.DiscogsReleaseId);
        builder.Property(r => r.UserId).HasMaxLength(450);
    }
}
