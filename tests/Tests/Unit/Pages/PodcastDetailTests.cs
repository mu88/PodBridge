using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.Domain;
using PodBridge.Logic.Feeds;
using Tests.TestSupport.Builders;

namespace Tests.Unit.Pages;

[TestFixture]
[Category("Unit")]
public sealed class PodcastDetailTests
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
    public void Render_WithUnknownId_TriggersNotFoundAndRendersNothing()
    {
        // Arrange
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().Build());

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, "unknown-show"));

        // Assert
        testee.Markup.Should().BeNullOrWhiteSpace();
    }

    [Test]
    public void Render_WithNotYetFetchedPodcast_ShowsPlaceholderAndNoEpisodes()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");
        _podcastCache.TryGetFull(show.PodcastId).Returns((CachedPodcast?)null);

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("h2").TextContent.Should().Be("Podcast show-id - not yet fetched");
        testee.Markup.Should().Contain("Feed not yet generated, please retry shortly.");
        testee.FindAll("table").Should().BeEmpty();
        testee.Instance.DisplayTitle.Should().Be("example-show");
    }

    [Test]
    public void Render_WithCachedPodcastButNoEpisodes_ShowsEmptyEpisodeState()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Markup.Should().Contain("No episodes available yet.");
        testee.FindAll("table").Should().BeEmpty();
        testee.Instance.DisplayTitle.Should().Be("Fixture Podcast");
    }

    [Test]
    public void Render_WithCachedPodcastAndEpisodes_ListsEpisodesNewestFirstWithFormattedDuration()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var olderEpisode = new EpisodeBuilder().WithDefaults().Build() with
        {
            Title = "Older Episode",
            PublishDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var newerEpisode = new EpisodeBuilder().WithDefaults().Build() with
        {
            Title = "Newer Episode",
            PublishDate = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var podcast = new PodcastBuilder().WithDefaults()
            .WithTitle("Example Show")
            .WithEpisodes(olderEpisode, newerEpisode)
            .Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));
        var rows = testee.FindAll("tbody tr");

        // Assert
        testee.Find("h2").TextContent.Should().Be("Example Show");
        rows.Should().HaveCount(2);
        rows[0].TextContent.Should().Contain("Newer Episode");
        rows[1].TextContent.Should().Contain("Older Episode");
        rows[0].TextContent.Should().Contain("30:00");
    }

    [Test]
    public void Render_WithEpisodeWithoutDuration_ShowsDash()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().WithMinimalMetadata().Build() with { Title = "No Duration Episode" };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain("-");
    }

    [Test]
    public void Render_WithLongDescription_TruncatesWithEllipsisMarker()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var longDescription = string.Join(' ', Enumerable.Repeat("word", 100));
        var episode = new EpisodeBuilder().WithDefaults().Build() with { Description = longDescription };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain("[...]").And.NotContain(longDescription);
    }

    [Test]
    public void Render_WithShortDescription_DoesNotTruncate()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().Build() with { Description = "A short description." };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain("A short description.").And.NotContain("[...]");
    }

    [Test]
    public void Render_WithEpisode_FormatsPublishDateUnambiguouslyWithUtcSuffix()
    {
        // Arrange: the app is pure static SSR Blazor with no way to know the visitor's browser
        // timezone, so the UTC timestamp is shown as-is with an explicit "(UTC)" suffix instead of
        // silently mislabeling it as local time.
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().Build() with { PublishDate = new DateTimeOffset(2026, 8, 24, 17, 41, 0, TimeSpan.Zero) };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain("24.08.2026 17:41 (UTC)");
    }

    [Test]
    public void Render_WithEpisodeImage_RendersEpisodeThumbnail()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().Build() with { ImageUrl = new Uri("https://fixture.test/episode.jpg") };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody img").GetAttribute("src").Should().Be("https://fixture.test/episode.jpg");
    }

    [Test]
    public void Render_WithoutEpisodeImage_RendersNoThumbnail()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().Build() with { ImageUrl = null };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.FindAll("tbody img").Should().BeEmpty();
    }

    [Test]
    public void Render_WithoutPodcastImage_RendersNoHeroImage()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var podcast = new PodcastBuilder().WithDefaults().Build() with { ImageUrl = null };
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.FindAll("img.podcast-hero-image").Should().BeEmpty();
    }

    [Test]
    public void Render_WithEpisodeDurationOfOneHourOrMore_FormatsDurationAsHoursMinutesSeconds()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var episode = new EpisodeBuilder().WithDefaults().Build() with { DurationSeconds = 3661 };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain("1:01:01");
    }

    [Test]
    public void Render_WithLongDescriptionWithoutSpaces_TruncatesAtMaxLength()
    {
        // Arrange
        var show = new PodcastConfigBuilder().WithDefaults().WithPodcastId("example-show").WithShowId("show-id").Build();
        ConfigureServices(new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show).Build());
        _feedUrlBuilder.BuildFeedUrl(show.PodcastId, Arg.Any<string>()!).Returns("https://feeds.example.test/api/feeds/example-show");

        var descriptionWithoutSpaces = new string('a', 300);
        var episode = new EpisodeBuilder().WithDefaults().Build() with { Description = descriptionWithoutSpaces };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        _podcastCache.TryGetFull(show.PodcastId).Returns(new CachedPodcast(podcast, DateTimeOffset.UtcNow));

        // Act
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.PodcastDetail>(parameters => parameters
            .Add(podcastDetail => podcastDetail.PodcastId, show.PodcastId));

        // Assert
        testee.Find("tbody tr").TextContent.Should().Contain(new string('a', 220) + " [...]").And.NotContain(descriptionWithoutSpaces);
    }

    private void ConfigureServices(PodBridgeOptions options)
    {
        _ctx.Services.AddSingleton(_podcastCache);
        _ctx.Services.AddSingleton(_feedUrlBuilder);
        _ctx.Services.AddSingleton(_httpContextAccessor);
        _ctx.Services.AddSingleton(Options.Create(options));
    }
}
