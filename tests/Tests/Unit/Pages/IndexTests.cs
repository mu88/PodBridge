using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.Feeds;
using Tests.TestSupport.Builders;

namespace Tests.Unit.Pages;

[TestFixture]
public sealed class IndexTests
{
    private BunitContext _ctx = null!;
    private IPodcastCache _podcastCache = null!;
    private IFeedUrlBuilder _feedUrlBuilder = null!;
    private IHttpContextAccessor _httpContextAccessor = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new BunitContext();
        _podcastCache = Substitute.For<IPodcastCache>();
        _feedUrlBuilder = Substitute.For<IFeedUrlBuilder>();
        _httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public void Render_WithNoConfiguredShows_DisplaysHeadingAndEmptyState()
    {
        // Arrange
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().Build());

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.Index>();

        // Assert
        testee.Find("h2").TextContent.Should().Be("Available Podcasts");
        testee.Find("p").TextContent.Should().Be("No podcasts configured.");
    }

    [Test]
    public void Render_WithConfiguredShows_DisplaysRowsWithIdsAndNames()
    {
        // Arrange
        var firstShow = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show-1").WithShowId("show-1-id").Build();
        var secondShow = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show-2").WithShowId("show-2-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(firstShow).WithPodcast(secondShow).Build());
        _feedUrlBuilder.BuildFeedUrl(firstShow.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/feeds/example-show-1");
        _feedUrlBuilder.BuildFeedUrl(secondShow.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/feeds/example-show-2");

        var podcast1 = new PodcastBuilder().WithDefaults().WithTitle("Example Show 1").Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithTitle("Example Show 2").Build();
        _podcastCache.TryGetFull(firstShow.PodcastId).Returns(new CachedPodcast(podcast1, DateTimeOffset.UtcNow));
        _podcastCache.TryGetFull(secondShow.PodcastId).Returns(new CachedPodcast(podcast2, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.Index>();
        var rows = testee.FindAll("tbody tr");

        // Assert
        rows.Should().HaveCount(2);
        rows[0].TextContent.Should().Contain("Example Show 1");
        rows[1].TextContent.Should().Contain("Example Show 2");
    }

    [Test]
    public void Render_WithNotYetFetchedShow_DisplaysPlaceholdersAndNoImage()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/feeds/example-show");
        _podcastCache.TryGetFull(show.PodcastId).Returns((CachedPodcast?)null);

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.Index>();
        var row = testee.Find("tbody tr");

        // Assert
        row.TextContent.Should().Contain("Podcast show-id - not yet fetched");
        row.TextContent.Should().Contain("Not yet generated");
        testee.FindAll("img").Should().BeEmpty();
    }

    [Test]
    public void Render_WithCachedShowButNoImage_OmitsImage()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/feeds/example-show");
        var podcast = new PodcastBuilder().WithDefaults().Build() with { ImageUrl = null };
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.Index>();

        // Assert
        testee.FindAll("img").Should().BeEmpty();
    }

    private void ConfigureServices(PodBridgeOptions options)
    {
        _ctx.Services.AddSingleton(_podcastCache);
        _ctx.Services.AddSingleton(_feedUrlBuilder);
        _ctx.Services.AddSingleton(_httpContextAccessor);
        _ctx.Services.AddSingleton(Options.Create(options));
    }
}
