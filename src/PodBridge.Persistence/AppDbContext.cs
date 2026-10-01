using Microsoft.EntityFrameworkCore;
using PodBridge.Logic.Config;

namespace PodBridge.Persistence;

// Holds only the Podcasts list - everything else (Auth, GraphQlEndpoint, refresh/rate-limiting settings)
// intentionally stays in PodBridgeOptions/IConfiguration. Keeping runtime-mutable, user-editable data
// (Podcasts) separate from infrastructure/secrets config limits the blast radius of a bug in either one.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PodcastConfig> Podcasts => Set<PodcastConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PodcastConfig>(podcast =>
        {
            podcast.HasKey(configuredPodcast => configuredPodcast.PodcastId);
            podcast.Property(configuredPodcast => configuredPodcast.PodcastId).HasMaxLength(200).IsRequired();
            podcast.Property(configuredPodcast => configuredPodcast.ShowId).HasMaxLength(200).IsRequired();

            // Enforces that ShowIds must be unique, with the database as the source of truth for Podcasts.
            podcast.HasIndex(configuredPodcast => configuredPodcast.ShowId).IsUnique();
        });
    }
}
