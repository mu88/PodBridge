using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using PodBridge.Persistence;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class PodcastDatabaseHealthCheckTests
{
    [Test]
    public async Task CheckHealthAsync_DatabaseReachable_ReturnsHealthy()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var dbContextFactory = new PooledDbContextFactory<AppDbContext>(options);
        var testee = new PodcastDatabaseHealthCheck(dbContextFactory);

        // Act
        var result = await testee.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseUnreachable_ReturnsDegradedWithException()
    {
        // Arrange
#pragma warning disable IDISP004 // NSubstitute call-configuration syntax invokes CreateDbContextAsync only to record the call spec; no AppDbContext instance is ever actually created.
        var brokenDbContextFactory = Substitute.For<IDbContextFactory<AppDbContext>>();
        brokenDbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated database failure"));
#pragma warning restore IDISP004
        var testee = new PodcastDatabaseHealthCheck(brokenDbContextFactory);

        // Act
        var result = await testee.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Cannot connect to the Podcasts database");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
