using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Tests.TestSupport.Builders;
using Tests.TestSupport.Http;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class RateLimitingTests
{
    [Test]
    public async Task GetPodcast_ExceedingRateLimit_Returns503()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            rateLimitingPermitLimit: 2,
            rateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient();

        // Act - make requests exceeding the limit
        using var response1 = await client.GetAsync("/api/podcasts/test-show");
        using var response2 = await client.GetAsync("/api/podcasts/test-show");
        using var response3 = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task GetPodcasts_ExceedingRateLimit_Returns503()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            rateLimitingPermitLimit: 2,
            rateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient();

        // Act
        using var response1 = await client.GetAsync("/api/podcasts");
        using var response2 = await client.GetAsync("/api/podcasts");
        using var response3 = await client.GetAsync("/api/podcasts");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task GetLogin_ExceedingAuthRateLimit_StillReturns200()
    {
        // Arrange - GET requests to /login (initial page view, browser refresh, redirect back after
        // logout) must never count against the brute-force login limiter - only actual submitted
        // login attempts (POST) should. Otherwise a legitimate user gets locked out just by viewing
        // the page repeatedly, without ever having tried a single (wrong or correct) credential.
        await using var factory = new TestWebApplicationFactory(
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass",
            authRateLimitingPermitLimit: 2,
            authRateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act - request the login page far more often than the permit limit
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 5; i++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.GetAsync("/login");
        }

        // Assert
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.OK);
        lastResponse.Dispose();
    }

    [Test]
    public async Task PostLogin_ExceedingRateLimit_Returns429()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass",
            authRateLimitingPermitLimit: 3,
            authRateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        // Act
        var formState = await HtmlFormHelper.GetAntiforgeryFormStateAsync(client, "/login");
        client.DefaultRequestHeaders.Add("Cookie", formState.CookieHeader);

        using var response1 = await PostLoginAsync(client, formState.Token, "testuser", "wrongpass");
        using var response2 = await PostLoginAsync(client, formState.Token, "testuser", "wrongpass");
        using var response3 = await PostLoginAsync(client, formState.Token, "testuser", "wrongpass");
        using var response4 = await PostLoginAsync(client, formState.Token, "testuser", "wrongpass");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
        response4.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task PostLogin_UsesRateLimitIndependentFromApiEndpoints()
    {
        // Arrange - an exhausted, very restrictive API rate limit must not affect the separately
        // configured (and more generous, in this test) login rate limit, and vice versa.
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass",
            rateLimitingPermitLimit: 1,
            rateLimitingWindowMinutes: 1,
            authRateLimitingPermitLimit: 10,
            authRateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Act - exhaust the restrictive API limit first
        using var apiResponse1 = await client.GetAsync("/api/podcasts");
        using var apiResponse2 = await client.GetAsync("/api/podcasts");

        var formState = await HtmlFormHelper.GetAntiforgeryFormStateAsync(client, "/login");
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", formState.CookieHeader);
        using var loginResponse = await PostLoginAsync(client, formState.Token, "testuser", "wrongpass");

        // Assert
        apiResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        apiResponse2.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string antiforgeryToken,
        string? username,
        string? password)
    {
        using var content = HtmlFormHelper.CreateLoginContent(antiforgeryToken, username, password);
        return await client.PostAsync("/login", content);
    }
}
