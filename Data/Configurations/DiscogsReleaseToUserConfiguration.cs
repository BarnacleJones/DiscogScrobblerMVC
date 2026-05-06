using DiscogScrobblerMVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscogScrobblerMVC.Data.Configurations;

public class DiscogsReleaseToUserConfiguration : IEntityTypeConfiguration<DiscogsReleaseToUser>
{
    public void Configure(EntityTypeBuilder<DiscogsReleaseToUser> builder)
    {
        // Legacy / single row per user+release when instance id unknown
        builder.HasIndex(r => new { r.UserId, r.DiscogsReleaseId }, "IX_DiscogsReleaseToUsers_UserId_DiscogsReleaseId")
            .IsUnique()
            .HasFilter("[DiscogsInstanceId] IS NULL");
        // One row per physical collection instance
        builder.HasIndex(r => new { r.UserId, r.DiscogsInstanceId })
            .IsUnique()
            .HasFilter("[DiscogsInstanceId] IS NOT NULL");
        // Unfiltered composite index so per-user predicates (e.g. the stats dashboard's
        // "releases owned by user" subquery) can always use an index regardless of whether
        // DiscogsInstanceId is null. The two indexes above are partial and SQLite won't
        // pick them for predicates that don't imply the filter. Named overload makes EF
        // treat this as a logically distinct index from the partial unique above.
        builder.HasIndex(r => new { r.UserId, r.DiscogsReleaseId }, "IX_DiscogsReleaseToUsers_UserId_DiscogsReleaseId_Plain");
        builder.HasOne(r => r.Release)
               .WithMany(r => r.UserAssociations)
               .HasForeignKey(r => r.DiscogsReleaseId)
               .HasPrincipalKey(r => r.DiscogsReleaseId);
        builder.Property(r => r.UserId).HasMaxLength(450);
    }
}
