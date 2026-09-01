using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using PodBridge.Logic.Config;
using Tests.TestSupport.Builders;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class PodBridgeOptionsTests
{
    [Test]
    public void Defaults_WhenNotConfigured_EnablesBackgroundRefresh()
    {
        // Arrange
        var testee = new PodBridgeOptions();

        // Assert
        testee.BackgroundRefreshEnabled.Should().BeTrue();
    }

    [Test]
    public void BindConfiguration_WithValidPodcastsConfig_PopulatesOptionsCorrectly()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "PodBridge:RefreshIntervalMinutes", "120" },
            { "PodBridge:GraphQlEndpoint", "https://fixture.test/graphql" },
            { "PodBridge:Podcasts:0:PodcastId", "show-1" },
            { "PodBridge:Podcasts:0:ShowId", "platform-show-1" },
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var testee = new PodBridgeOptions();

        // Act
        config.GetSection(PodBridgeOptions.SectionName).Bind(testee);

        // Assert
        testee.RefreshIntervalMinutes.Should().Be(120);
        testee.GraphQlEndpoint.Should().Be(new Uri("https://fixture.test/graphql"));
        testee.Podcasts.Should().HaveCount(1);
        testee.Podcasts[0].PodcastId.Should().Be("show-1");
        testee.Podcasts[0].ShowId.Should().Be("platform-show-1");
    }

    [Test]
    public void BindConfiguration_WithEmptyPodcasts_DefaultsToEmptyList()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "PodBridge:RefreshIntervalMinutes", "360" },
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var testee = new PodBridgeOptions();

        // Act
        config.GetSection(PodBridgeOptions.SectionName).Bind(testee);

        // Assert
        testee.Podcasts.Should().BeEmpty();
    }

    [Test]
    public void BindConfiguration_WithAuthEnabled_PopulatesAuthOptions()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "PodBridge:Auth:Enabled", "true" },
            { "PodBridge:Auth:UsernameHash", "210000.dGVzdC1zYWx0.dGVzdC1oYXNo" },
            { "PodBridge:Auth:PasswordHash", "210000.b3RoZXItc2FsdA==.b3RoZXItaGFzaA==" },
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var testee = new PodBridgeOptions();

        // Act
        config.GetSection(PodBridgeOptions.SectionName).Bind(testee);

        // Assert
        testee.Auth.Enabled.Should().BeTrue();
        testee.Auth.UsernameHash.Should().Be("210000.dGVzdC1zYWx0.dGVzdC1oYXNo");
        testee.Auth.PasswordHash.Should().Be("210000.b3RoZXItc2FsdA==.b3RoZXItaGFzaA==");
    }

    [Test]
    public void Validate_DuplicatePodcastIds_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithPodcastId("duplicate-id"))
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithPodcastId("duplicate-id"))
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("unique"));
    }

    [Test]
    public void Validate_EmptyPodcastId_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithPodcastId(string.Empty))
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("non-empty PodcastId"));
    }

    [Test]
    public void Validate_EmptyShowId_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithShowId(string.Empty))
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("non-empty ShowId"));
    }

    [Test]
    public void Validate_DuplicateShowIds_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithShowId("duplicate-show-id"))
            .WithPodcast(new PodcastConfigBuilder().WithDefaults().WithShowId("duplicate-show-id"))
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("ShowIds must be unique"));
    }

    [Test]
    public void Validate_AuthEnabledWithoutUsername_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithAuth(enabled: true, usernameHash: null, passwordHash: "210000.c2FsdA==.aGFzaA==")
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Auth.UsernameHash"));
    }

    [Test]
    public void Validate_AuthEnabledWithoutPassword_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithAuth(enabled: true, usernameHash: "210000.c2FsdA==.aGFzaA==", passwordHash: null)
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Auth.PasswordHash"));
    }

    [Test]
    public void Validate_RelativeGraphQlEndpoint_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(new Uri("/relative/path", UriKind.Relative))
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("absolute URI"));
    }

    [Test]
    public void Validate_PodcastsWithoutGraphQlEndpoint_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .WithPodcast(new PodcastConfigBuilder().WithDefaults())
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("GraphQlEndpoint must be configured"));
    }

    [Test]
    public void Validate_NoPodcastsWithoutGraphQlEndpoint_ReturnsNoValidationError()
    {
        // Arrange: boundary case for the "podcasts.Count > 0" check - an empty Podcasts list must not
        // require a GraphQlEndpoint, unlike a non-empty one (covered by the test above).
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().NotContain(r => r.ErrorMessage!.Contains("GraphQlEndpoint must be configured"));
    }

    [Test]
    public void Validate_ValidConfiguration_ReturnsNoErrors()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(new PodcastConfigBuilder().WithDefaults())
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().BeEmpty();
    }
}

