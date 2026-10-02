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
    public void BindConfiguration_WithValidConfig_PopulatesOptionsCorrectly()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "PodBridge:RefreshIntervalMinutes", "120" },
            { "PodBridge:GraphQlEndpoint", "https://fixture.test/graphql" },
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
    }

    [Test]
    public void BindConfiguration_WithEmptyConfig_KeepsDefaults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var testee = new PodBridgeOptions();

        // Act
        config.GetSection(PodBridgeOptions.SectionName).Bind(testee);

        // Assert
        testee.RefreshIntervalMinutes.Should().Be(360);
        testee.BackgroundRefreshEnabled.Should().BeTrue();
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
    public void Validate_NullGraphQlEndpoint_ReturnsNoErrors()
    {
        // Arrange - isolates the "GraphQlEndpoint is not null" operand of the guard: when the endpoint
        // isn't configured at all, the absolute-URI check must be skipped entirely rather than throwing
        // on a null dereference.
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void EffectiveRefreshInterval_WhenRefreshIntervalNotSet_FallsBackToRefreshIntervalMinutes()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder().WithDefaults().Build();

        // Act & Assert
        options.EffectiveRefreshInterval.Should().Be(TimeSpan.FromMinutes(options.RefreshIntervalMinutes));
    }

    [Test]
    public void EffectiveRefreshInterval_WhenRefreshIntervalSet_TakesPrecedenceOverRefreshIntervalMinutes()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithRefreshInterval(TimeSpan.FromSeconds(10))
            .Build();

        // Act & Assert
        options.EffectiveRefreshInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void Validate_NonPositiveRefreshInterval_ReturnsValidationError()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithRefreshInterval(TimeSpan.Zero)
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("RefreshInterval"));
    }

    [Test]
    public void Validate_ValidConfiguration_ReturnsNoErrors()
    {
        // Arrange
        var options = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .Build();

        // Act
        var results = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        results.Should().BeEmpty();
    }
}
