using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Refresh;
using Tests.TestSupport;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
public sealed class RefreshScenarioTests
{
    private IEpisodeSource _episodeSourceMock = null!;
    private IPodcastCache _podcastCache = null!;

    [SetUp]
    public void Setup()
    {
        _episodeSourceMock = Substitute.For<IEpisodeSource>();
        _podcastCache = new PodcastCache(TimeProvider.System);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithMultipleShows_CachesAllPodcasts()
    {
        // Arrange
        var show1Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var show2Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show2").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(show1Config)
            .WithPodcast(show2Config)
            .Build();

        var podcast1 = new PodcastBuilder().WithDefaults().WithTitle("Show One").Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithTitle("Show Two").Build();

        _episodeSourceMock.FetchEpisodesAsync(show1Config, Arg.Any<CancellationToken>()).Returns(podcast1);
        _episodeSourceMock.FetchEpisodesAsync(show2Config, Arg.Any<CancellationToken>()).Returns(podcast2);

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        var show1Cached = _podcastCache.TryGetFull(show1Config.PodcastId);
        var show2Cached = _podcastCache.TryGetFull(show2Config.PodcastId);

        show1Cached.Should().NotBeNull();
        show2Cached.Should().NotBeNull();
        show1Cached!.Podcast.Title.Should().Be("Show One");
        show2Cached!.Podcast.Title.Should().Be("Show Two");
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithActivityListenerRegistered_RecordsActivityTagsOnBothSpans()
    {
        // Arrange: registers a listener so Observability.Source.StartActivity() returns a non-null
        // Activity, exercising the "activity is present" branch of the activity?.SetTag(...) calls in
        // both RefreshAllShowsAsync and TryRefreshPodcastAsync (which is null in every other test here,
        // since no listener is registered for PodBridge's ActivitySource by default).
        using var activityListenerScope = new ActivityListenerScope();
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(showConfig).Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        _podcastCache.TryGetFull(showConfig.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenNewShowAddedAfterFirstRefresh_NewShowBecomesAvailable()
    {
        // Arrange
        var show1Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var show2Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show2").Build();

        var podcast1 = new PodcastBuilder().WithDefaults().WithTitle("Show One").Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithTitle("Show Two").Build();

        _episodeSourceMock.FetchEpisodesAsync(show1Config, Arg.Any<CancellationToken>()).Returns(podcast1);
        _episodeSourceMock.FetchEpisodesAsync(show2Config, Arg.Any<CancellationToken>()).Returns(podcast2);

        // First refresh with only show1
        var options1 = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show1Config).Build();
        var optionsWrapper1 = Options.Create(options1);
        using var sut1 = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper1,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);
        await sut1.RefreshAllShowsAsync(CancellationToken.None);

        // Act: Second refresh with show1 + show2
        var options2 = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(show1Config).WithPodcast(show2Config).Build();
        var optionsWrapper2 = Options.Create(options2);
        using var sut2 = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper2,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);
        await sut2.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        _podcastCache.TryGetFull(show1Config.PodcastId).Should().NotBeNull();
        _podcastCache.TryGetFull(show2Config.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenNewEpisodeIsAdded_CacheContainsAllEpisodes()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(showConfig).Build();

        var episode1 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 2" };

        var podcast1 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1).Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();

        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>())
            .Returns(podcast1, podcast2);

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterFirst = _podcastCache.TryGetFull(showConfig.PodcastId);
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterSecond = _podcastCache.TryGetFull(showConfig.PodcastId);

        // Assert
        cachedAfterFirst.Should().NotBeNull();
        cachedAfterFirst!.Podcast.Episodes.Should().HaveCount(1);
        cachedAfterSecond.Should().NotBeNull();
        cachedAfterSecond!.Podcast.Episodes.Should().HaveCount(2);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenEpisodeIsRetracted_CacheNoLongerContainsIt()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(showConfig).Build();

        var episode1 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 2" };

        var podcast1 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithEpisodes(episode2).Build();

        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>())
            .Returns(podcast1, podcast2);

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterFirst = _podcastCache.TryGetFull(showConfig.PodcastId);
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterSecond = _podcastCache.TryGetFull(showConfig.PodcastId);

        // Assert
        cachedAfterFirst.Should().NotBeNull();
        cachedAfterFirst!.Podcast.Episodes.Should().HaveCount(2);
        cachedAfterSecond.Should().NotBeNull();
        cachedAfterSecond!.Podcast.Episodes.Should().HaveCount(1);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenOneShowFails_CachesSucceedingShowAndSkipsFailingOne()
    {
        // Arrange
        var succeedingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("succeeding-show").Build();
        var failingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("failing-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(succeedingShowConfig)
            .WithPodcast(failingShowConfig)
            .Build();

        var succeedingPodcast = new PodcastBuilder().WithDefaults().WithTitle("Succeeding Show").Build();
        _episodeSourceMock.FetchEpisodesAsync(succeedingShowConfig, Arg.Any<CancellationToken>()).Returns(succeedingPodcast);
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Domain.Podcast>(_ => throw new InvalidOperationException("Simulated fetch failure"));

        var optionsWrapper = Options.Create(podBridgeOptions);
        var loggerMock = Substitute.For<ILogger<EpisodeRefreshWorker>>();
        loggerMock.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            loggerMock);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a single failing show must not abort the whole refresh cycle");
        _podcastCache.TryGetFull(succeedingShowConfig.PodcastId).Should().NotBeNull();
        _podcastCache.TryGetFull(failingShowConfig.PodcastId).Should().BeNull();
        loggerMock.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenShowFailsWithActivityListenerRegistered_MarksActivityAsError()
    {
        // Arrange: registers a listener so the catch block's activity?.SetStatus(...)/AddException(...)
        // calls exercise the "activity is present" branch too (activity is null in the other failure test).
        using var activityListenerScope = new ActivityListenerScope();
        var failingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("failing-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(failingShowConfig).Build();
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Domain.Podcast>(_ => throw new InvalidOperationException("Simulated fetch failure"));

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a single failing show must not abort the whole refresh cycle");
        _podcastCache.TryGetFull(failingShowConfig.PodcastId).Should().BeNull();
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenCancelledMidLoop_StopsProcessingRemainingShows()
    {
        // Arrange
        var firstShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("first-show").Build();
        var secondShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("second-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithPodcast(firstShowConfig)
            .WithPodcast(secondShowConfig)
            .Build();

        using var cts = new CancellationTokenSource();
        var firstPodcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(firstShowConfig, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return firstPodcast;
            });

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _podcastCache.TryGetFull(firstShowConfig.PodcastId).Should().NotBeNull("the first show was already processed before cancellation");
        _podcastCache.TryGetFull(secondShowConfig.PodcastId).Should().BeNull("the loop must stop before processing the second show");
        await _episodeSourceMock.DidNotReceive().FetchEpisodesAsync(secondShowConfig, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithNoConfiguredShows_CompletesWithoutError()
    {
        // Arrange
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            TimeProvider.System,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ExecuteAsync_OnPeriodicTimerTick_RefreshesShowsAgainThenStopsWhenLoopSeamReturnsFalse()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(showConfig).Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            timeProvider,
            NullLogger<EpisodeRefreshWorker>.Instance,
            continueLoop: () => false);

        // Act
        // ExecuteAsync is invoked directly via reflection (rather than via StartAsync, which runs it on
        // the ThreadPool through Task.Run) so it runs synchronously up to its first genuine await point -
        // avoiding a race between the ThreadPool scheduling the task and this test advancing the fake clock.
        var executeAsyncMethod = typeof(EpisodeRefreshWorker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeTask = (Task)executeAsyncMethod.Invoke(testee, [CancellationToken.None])!;
        timeProvider.Advance(TimeSpan.FromMinutes(podBridgeOptions.RefreshIntervalMinutes)); // triggers the tick that then stops the loop
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _podcastCache.TryGetFull(showConfig.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithDefaultLoopSeam_ContinuesLoopingUntilCancelled()
    {
        // Arrange: uses the 5-arg (production) constructor so the default `_continueLoop = () => true`
        // delegate is exercised, rather than the test-only seam constructor used by the other ExecuteAsync
        // test above.
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().WithPodcast(showConfig).Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        using var cts = new CancellationTokenSource();

        var optionsWrapper = Options.Create(podBridgeOptions);
        using var testee = new EpisodeRefreshWorker(
            _episodeSourceMock,
            _podcastCache,
            optionsWrapper,
            timeProvider,
            NullLogger<EpisodeRefreshWorker>.Instance);

        // Act
        var executeAsyncMethod = typeof(EpisodeRefreshWorker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeTask = (Task)executeAsyncMethod.Invoke(testee, [cts.Token])!;
        timeProvider.Advance(TimeSpan.FromMinutes(podBridgeOptions.RefreshIntervalMinutes)); // first tick: default continueLoop() runs and returns true
        await Task.Delay(TimeSpan.FromMilliseconds(100)); // lets the loop re-enter and start waiting on the timer again
        await cts.CancelAsync();
        var act = async () => await executeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _podcastCache.TryGetFull(showConfig.PodcastId).Should().NotBeNull();
    }
}
