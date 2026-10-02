using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using FluentAssertions.Web;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Tests.SystemTests;

[Category("System")]
public class PodBridgeUiSystemTests
{
    private const string AlphaShowId = "show-alpha-id";
    private const string AlphaPodcastId = "alpha-show";
    private const string AlphaTitle = "Alpha Show";
    private const string BetaShowId = "show-beta-id";
    private const string BetaPodcastId = "beta-show";
    private const string BetaTitle = "Beta Show";
    private const string GammaShowId = "show-gamma-id";
    private const string GammaPodcastId = "gamma-show";
    private const string GammaTitle = "Gamma Show";

    private CancellationTokenSource _cancellationTokenSource = null!;

    [SetUp]
    public void Setup()
    {
        // 3 minutes to cover test execution with Playwright interactions and periodic-refresh waits.
        _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
    }

    [Test]
    public async Task SearchAndAddPodcastViaUi_ShouldAddPodcastAndDisplayAllThreeFeeds()
    {
        // Arrange
        await SeedTestDataAsync();

        await using var page = await SystemTestsFixture.Browser.NewPageAsync();
        using var httpClient = SystemTestsHelper.CreateAuthenticatedHttpClient();

        // Act & Assert - Log in
        await LogInAsync(page);

        // Ensure seeded podcasts are fetched before UI interaction
        await EnsureSeededPodcastsAreAvailableAsync(httpClient);

        // Search and add Gamma via UI
        await SearchAndAddPodcastAsync(page, "gamma", GammaShowId);

        // Verify all three podcasts are available (Gamma newly added, Alpha+Beta pre-seeded)
        await EnsureAllPodcastsAreAvailableAsync(page, httpClient);

        // Verify the added podcast's RSS feed XML content via UI link
        await CopyAndVerifyFeedXmlAsync(page, httpClient);

        // Log out and verify the session is cleared
        await LogOutAsync(page);
    }

    private static async Task LogInAsync(IPage page)
    {
        await page.GotoAsync($"{SystemTestsFixture.PodBridgeInternalAddress}login");
        await page.FillAsync("#login-username", "systemtestuser");
        await page.FillAsync("#login-password", "systemtestpass");
        await page.ClickAsync("button:has-text('Sign in')");
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
    }

    private static async Task LogOutAsync(IPage page)
    {
        // Open the user account menu disclosure
        await page.ClickAsync("summary.user-menu-trigger");

        // Click the logout button
        await page.ClickAsync("button:has-text('Log out')");

        // Wait for navigation to /login to complete
        await page.WaitForURLAsync(url => url.Contains("/login", StringComparison.Ordinal));

        // Verify the current URL contains /login, confirming successful logout
        page.Url.Should().Contain("/login");

        // Optionally verify that trying to access the protected Index page redirects to login again
        await page.GotoAsync(SystemTestsFixture.PodBridgeInternalAddress.ToString());
        page.Url.Should().Contain("/login", "because the session should have been cleared");
    }

    private async Task SeedTestDataAsync()
    {
        await SystemTestsHelper.SeedPodcastAsync(AlphaPodcastId, AlphaShowId, _cancellationTokenSource.Token);
        await SystemTestsHelper.SeedPodcastAsync(BetaPodcastId, BetaShowId, _cancellationTokenSource.Token);
    }

    private async Task EnsureSeededPodcastsAreAvailableAsync(HttpClient httpClient)
    {
        using var alphaResponse = await SystemTestsHelper.WaitForFeedToBePopulatedAsync(httpClient, AlphaTitle, _cancellationTokenSource.Token);
        alphaResponse.Should().Be200Ok();

        using var betaResponse = await SystemTestsHelper.WaitForFeedToBePopulatedAsync(httpClient, BetaTitle, _cancellationTokenSource.Token);
        betaResponse.Should().Be200Ok();
    }

    private async Task SearchAndAddPodcastAsync(IPage page, string searchQuery, string expectedShowId)
    {
        await page.GotoAsync($"{SystemTestsFixture.PodBridgeInternalAddress}podcasts/add", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("[data-testid='search-query-input']", searchQuery);
        await page.ClickAsync("[data-testid='search-submit-button']");

        // Wait for the results to load and render
        await Task.Delay(1000, _cancellationTokenSource.Token);

        // Wait for the search results table to appear before looking for the specific button
        await page.WaitForSelectorAsync("table.podcast-table tbody tr", new PageWaitForSelectorOptions { Timeout = 60000 });

        // Wait for the specific add button to be visible
        await page.WaitForSelectorAsync($"[data-testid='add-podcast-button-{expectedShowId}']", new PageWaitForSelectorOptions { Timeout = 60000 });
        await page.ClickAsync($"[data-testid='add-podcast-button-{expectedShowId}']");
    }

    private async Task EnsureAllPodcastsAreAvailableAsync(IPage page, HttpClient httpClient)
    {
        // Wait for Gamma to be populated
        using var gammaResponse = await SystemTestsHelper.WaitForFeedToBePopulatedAsync(httpClient, GammaTitle, _cancellationTokenSource.Token);
        gammaResponse.Should().Be200Ok();

        // Navigate to Index and verify all three titles are present via data-testid locators
        await page.GotoAsync(SystemTestsFixture.PodBridgeInternalAddress.ToString());

        var alphaText = await page.Locator($"[data-testid='podcast-title-{AlphaPodcastId}']").InnerTextAsync();
        alphaText.Should().Contain(AlphaTitle);

        var betaText = await page.Locator($"[data-testid='podcast-title-{BetaPodcastId}']").InnerTextAsync();
        betaText.Should().Contain(BetaTitle);

        var gammaText = await page.Locator($"[data-testid='podcast-title-{GammaPodcastId}']").InnerTextAsync();
        gammaText.Should().Contain(GammaTitle);

        // Verify RSS feeds for all three have exactly 3 episodes
        await VerifyFeedHasExactEpisodesAsync(httpClient, AlphaPodcastId, 3);
        await VerifyFeedHasExactEpisodesAsync(httpClient, BetaPodcastId, 3);
        await VerifyFeedHasExactEpisodesAsync(httpClient, GammaPodcastId, 3);
    }

    private async Task CopyAndVerifyFeedXmlAsync(IPage page, HttpClient httpClient)
    {
        // Navigate to Index page to access the RSS feed link
        await page.GotoAsync(SystemTestsFixture.PodBridgeInternalAddress.ToString());

        // Read the Gamma podcast's RSS feed link href from the UI
        var feedLinkHref = await page.Locator($"[data-testid='podcast-feed-link-{GammaPodcastId}']").GetAttributeAsync("href");
        feedLinkHref.Should().NotBeNullOrEmpty();

        // Extract path+query from the absolute URL (href will have the internal network address as host)
        var feedUri = new Uri(feedLinkHref!);
        var pathAndQuery = feedUri.PathAndQuery;

        // Fetch the RSS feed using the authenticated httpClient (with host-mapped address)
        using var response = await httpClient.GetAsync(pathAndQuery, _cancellationTokenSource.Token);
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/rss+xml");

        // Parse and verify the RSS XML structure
        var feedXml = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        var doc = XDocument.Parse(feedXml);
        var channel = doc.Root!.Element("channel")!;
        channel.Element("title")!.Value.Should().Be(GammaTitle);

        var items = channel.Elements("item").ToList();
        items.Should().HaveCount(3);

        // Verify episode titles are present
        items.Select(item => item.Element("title")!.Value).Should().Contain("Gamma Episode 1");
        items.Select(item => item.Element("title")!.Value).Should().Contain("Gamma Episode 2");
        items.Select(item => item.Element("title")!.Value).Should().Contain("Gamma Episode 3");
    }

    private async Task VerifyFeedHasExactEpisodesAsync(HttpClient httpClient, string podcastId, int expectedEpisodeCount)
    {
        using var response = await httpClient.GetAsync($"/api/podcasts/{podcastId}", _cancellationTokenSource.Token);
        response.Should().Be200Ok();

        var feedXml = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        var doc = XDocument.Parse(feedXml);
        var channel = doc.Root!.Element("channel")!;
        var items = channel.Elements("item").ToList();

        items.Should().HaveCount(expectedEpisodeCount, because: $"podcast {podcastId} should have exactly {expectedEpisodeCount.ToString(CultureInfo.InvariantCulture)} episodes");
    }
}
