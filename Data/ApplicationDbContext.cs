using DiscogScrobblerMVC.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Release> Releases { get; set; }
    public DbSet<DiscogsReleaseToUser> DiscogsReleaseToUsers { get; set; }
    public DbSet<DiscogsReleaseImages> DiscogsReleaseImages { get; set; }
    public DbSet<Artist> Artists { get; set; }
    public DbSet<Label> Labels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // registers Identity tables
        // auto-discovers all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
