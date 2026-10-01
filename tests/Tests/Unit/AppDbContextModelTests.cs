using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using PodBridge.Logic.Config;
using PodBridge.Persistence;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class AppDbContextModelTests
{
    [Test]
    public void OnModelCreating_ConfiguresPodcastConfigConstraints()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var dbContext = new AppDbContext(options);

        // Act
        var entityType = dbContext.Model.FindEntityType(typeof(PodcastConfig))!;

        // Assert - pins the length/required-ness constraints and the ShowId uniqueness invariant that
        // OnModelCreating configures; a regression here (e.g. accidentally dropping the unique index)
        // would otherwise only surface as a hard-to-diagnose duplicate-ShowId bug in production.
        var podcastIdProperty = entityType.FindProperty(nameof(PodcastConfig.PodcastId))!;
        podcastIdProperty.GetMaxLength().Should().Be(200);
        podcastIdProperty.IsNullable.Should().BeFalse();

        var showIdProperty = entityType.FindProperty(nameof(PodcastConfig.ShowId))!;
        showIdProperty.GetMaxLength().Should().Be(200);
        showIdProperty.IsNullable.Should().BeFalse();

        entityType.GetIndexes().Should().ContainSingle(index => index.IsUnique
            && index.Properties.Count == 1
            && index.Properties[0].Name == nameof(PodcastConfig.ShowId));
    }
}
