using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class BasicAuthenticationTests
{
    [Test]
    public async Task GetPodcasts_WithoutAuth_When_AuthEnabled_Returns401()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetPodcasts_WithValidAuth_When_AuthEnabled_Returns200()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetPodcasts_WithInvalidAuth_When_AuthEnabled_Returns401()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:wrongpassword"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetPodcasts_WithInvalidUsername_When_AuthEnabled_Returns401()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("wronguser:testpass"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetPodcasts_WithMalformedBase64Credentials_Returns401()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "not-valid-base64!!!");

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetPodcasts_WithCredentialsMissingColonSeparator_Returns401()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
        using var client = factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuseronly"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetPodcasts_WithoutAuth_When_AuthDisabled_Returns200()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: false);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetPodcasts_WithMalformedAuthHeader_When_AuthDisabled_Returns200()
    {
        // Arrange - proves the authentication handler's own header-parsing logic never runs at all
        // when Auth is disabled, since the API endpoint group carries no RequireAuthorization in
        // that case (rather than the handler itself special-casing a disabled Auth configuration).
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "not-valid-base64!!!");

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
    }
}
