using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PodBridge.Logic.Config;
using PodBridge.Persistence;

namespace Tests.TestSupport;

internal sealed class SqliteAppDbContextFactoryScope : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteAppDbContextFactoryScope(IEnumerable<PodcastConfig>? podcasts = null)
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();

        if (podcasts is not null)
        {
            dbContext.Podcasts.AddRange(podcasts);
            dbContext.SaveChanges();
        }

        DbContextFactory = new PooledDbContextFactory<AppDbContext>(options);
    }

    public IDbContextFactory<AppDbContext> DbContextFactory { get; }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
