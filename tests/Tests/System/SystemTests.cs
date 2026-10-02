using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using CliWrap;
using CliWrap.Buffered;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using FluentAssertions.Web;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using WireMock.Net.Testcontainers;

namespace Tests.SystemTests;

[Category("System")]
public class PodBridgeSystemTests
{
    private const string FixtureShowId = "fixture-show";
    private const string FixtureShowIdShowId = "fixture-show-id";
    private const string FixtureShowDisplayName = "System Test Fixture Show";

    private CancellationTokenSource _cancellationTokenSource = null!;

    [SetUp]
    public void Setup()
    {
        // 3 minutes to cover test execution and waiting for periodic-refresh cycles.
        _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
    }

    [Test]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP014:Use a single instance of HttpClient", Justification = "Short-lived system test clients targeting Testcontainers-assigned ports that only exist for the duration of these tests; a shared instance isn't feasible.")]
    public async Task AppRunningInDocker_ShouldSyncPodcastFromWireMockAndExposeRssFeedWithAuthEnforced()
    {
        // Arrange
        await SystemTestsHelper.SeedPodcastAsync(FixtureShowId, FixtureShowIdShowId, _cancellationTokenSource.Token);

        using var httpClientWithoutAuth = new HttpClient { BaseAddress = SystemTestsFixture.PodBridgeBaseAddress };
        using var httpClientWithAuth = SystemTestsHelper.CreateAuthenticatedHttpClient();

        // Act & Assert - /healthz should be accessible without auth
        using var healthCheck = await httpClientWithoutAuth.GetAsync("healthz", _cancellationTokenSource.Token);
        healthCheck.Should().Be200Ok();

        // /api/podcasts without auth should return 401
        using var podcastsWithoutAuth = await httpClientWithoutAuth.GetAsync("/api/podcasts", _cancellationTokenSource.Token);
        podcastsWithoutAuth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // /api/podcasts/{id} feed without auth should return 401
        using var feedWithoutAuth = await httpClientWithoutAuth.GetAsync($"/api/podcasts/{FixtureShowId}", _cancellationTokenSource.Token);
        feedWithoutAuth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Wait for background worker to populate cache
        using var podcastsResponse = await SystemTestsHelper.WaitForFeedToBePopulatedAsync(httpClientWithAuth, FixtureShowDisplayName, _cancellationTokenSource.Token);
        podcastsResponse.Should().Be200Ok();

        var podcastsJson = await podcastsResponse.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        podcastsJson.Should().Contain(FixtureShowId).And.Contain(FixtureShowDisplayName);

        // Fetch feed with auth
        using var feedResponse = await httpClientWithAuth.GetAsync($"/api/podcasts/{FixtureShowId}", _cancellationTokenSource.Token);
        feedResponse.Should().Be200Ok();
        feedResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/rss+xml");

        var feedXml = await feedResponse.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        var doc = XDocument.Parse(feedXml);
        var channel = doc.Root!.Element("channel")!;
        channel.Element("title")!.Value.Should().Be(FixtureShowDisplayName);

        var items = channel.Elements("item").ToList();
        items.Should().HaveCount(2);

        // The RSS feed lists episodes newest-first (RssFeed.cs orders by PublishDate descending),
        // so "Episode 2" (published later) appears before "Episode 1".
        items[0].Element("title")!.Value.Should().Be("Episode 2: The Journey");
        items[1].Element("title")!.Value.Should().Be("Episode 1: The Beginning");
    }
}

