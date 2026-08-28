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
    public async Task GetRoot_WithoutAuth_When_AuthEnabled_Returns401()
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetRoot_WithValidAuth_When_AuthEnabled_Returns200()
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetRoot_WithInvalidAuth_When_AuthEnabled_Returns401()
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetRoot_WithInvalidUsername_When_AuthEnabled_Returns401()
    {
        // Arrange: covers the short-circuit branch of `FixedTimeEquals(username, ...) && FixedTimeEquals(password, ...)`
        // where the username check alone already fails, so the password check is never evaluated.
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetRoot_WithMalformedBase64Credentials_Returns401()
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetRoot_WithCredentialsMissingColonSeparator_Returns401()
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
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be401Unauthorized();
    }

    [Test]
    public async Task GetRoot_WithoutAuth_When_AuthDisabled_Returns200()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: false);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be200Ok();
    }
}
