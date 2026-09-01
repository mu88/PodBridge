using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class ScalarOpenApiTests
{
    [Test]
    public async Task GetScalar_WithoutAuth_When_AuthEnabled_Returns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/scalar/");

        // Assert
        response.Should().Be200Ok();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("PodBridge API Reference");
    }

    [Test]
    public async Task GetOpenApiDocument_WithoutAuth_When_AuthEnabled_Returns200WithPodcastPathsAndBasicAuthScheme()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        paths.GetProperty("/api/podcasts");
        paths.GetProperty("/api/podcasts/{podcastId}");

        var securitySchemes = json.RootElement.GetProperty("components").GetProperty("securitySchemes");
        var basicAuth = securitySchemes.GetProperty("basicAuth");
        basicAuth.GetProperty("type").GetString().Should().Be("http");
        basicAuth.GetProperty("scheme").GetString().Should().Be("basic");

        var getPodcasts = json.RootElement.GetProperty("paths").GetProperty("/api/podcasts").GetProperty("get");
        var security = getPodcasts.GetProperty("security");
        security.GetArrayLength().Should().BeGreaterThan(0);
        security[0].TryGetProperty("basicAuth", out var basicAuthRequirement).Should().BeTrue();
        basicAuthRequirement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    private static TestWebApplicationFactory CreateFactory()
    {
        return new TestWebApplicationFactory(
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
    }

    private static HttpClient CreateClient(TestWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }
}
