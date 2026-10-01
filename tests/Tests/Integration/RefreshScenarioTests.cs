using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Refresh;
using PodBridge.Logic.Shared;
using PodBridge.Persistence;
using Tests.TestSupport;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class RefreshScenarioTests
{
    private IEpisodeSource _episodeSourceMock = null!;
    private PodcastCache _podcastCache = null!;
    private IEpisodeRefreshHealthState _healthStateMock = null!;
    private List<SqliteAppDbContextFactoryScope> _dbContextFactoryScopes = null!;

    [SetUp]
    public void Setup()
    {
        _episodeSourceMock = Substitute.For<IEpisodeSource>();
        _podcastCache = new PodcastCache(TimeProvider.System);
        _healthStateMock = Substitute.For<IEpisodeRefreshHealthState>();
        _dbContextFactoryScopes = [];
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var dbContextFactoryScope in _dbContextFactoryScopes)
        {
            dbContextFactoryScope.Dispose();
        }
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithMultipleShows_CachesAllPodcasts()
    {
        // Arrange
        var show1Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var show2Config = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show2").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .Build();

        var podcast1 = new PodcastBuilder().WithDefaults().WithTitle("Show One").Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithTitle("Show Two").Build();

        _episodeSourceMock.FetchEpisodesAsync(show1Config, Arg.Any<CancellationToken>()).Returns(podcast1);
        _episodeSourceMock.FetchEpisodesAsync(show2Config, Arg.Any<CancellationToken>()).Returns(podcast2);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [show1Config, show2Config]);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        var show1Cached = _podcastCache.FindFull(show1Config.PodcastId);
        var show2Cached = _podcastCache.FindFull(show2Config.PodcastId);

        show1Cached.Should().NotBeNull();
        show2Cached.Should().NotBeNull();
        show1Cached!.Podcast.Title.Should().Be("Show One");
        show2Cached!.Podcast.Title.Should().Be("Show Two");
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithActivityListenerRegistered_RecordsActivityTagsOnBothSpans()
    {
        // Arrange: registers listeners so Observability.Source.StartActivity() returns a non-null
        // Activity, exercising the "activity is present" branch of the activity?.SetTag(...) calls in
        // both RefreshAllShowsAsync and TryRefreshPodcastAsync (which is null in every other test here,
        // since no listener is registered for PodBridge's ActivitySource by default). The recorded tag
        // *values*, activity name, and metric counter measurements are asserted too, not just that a
        // listener is present, so mutations to the counted values (platform/podcast/success/failure
        // counts), the tagged podcast id, the activity name, and the Observability.Record*(...) calls
        // (Counter.Add(...), invisible on any Activity) are all caught.
        using var activityListenerScope = new ActivityListenerScope();
        using var meterListenerScope = new MeterListenerScope();
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var episode1 = new EpisodeBuilder().WithDefaults().Build();
        var episode2 = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig]);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        _podcastCache.FindFull(showConfig.PodcastId).Should().NotBeNull();

        var refreshActivity = activityListenerScope.StoppedActivities.Single(
            activity => activity.GetTagItem(Observability.PlatformCountTag) is not null);
        refreshActivity.GetTagItem(Observability.PlatformCountTag).Should().Be(1);
        refreshActivity.GetTagItem(Observability.PodcastCountTag).Should().Be(1);
        refreshActivity.GetTagItem(Observability.RefreshSuccessCountTag).Should().Be(1);
        refreshActivity.GetTagItem(Observability.RefreshFailureCountTag).Should().Be(0);

        var podcastActivity = activityListenerScope.StoppedActivities.Single(
            activity => activity.GetTagItem(Observability.PodcastIdTag) is not null);
        podcastActivity.GetTagItem(Observability.PodcastIdTag).Should().Be(showConfig.PodcastId);
        podcastActivity.OperationName.Should().Be("RefreshPodcast");

        meterListenerScope.Measurements.Should().ContainSingle(
            m => m.InstrumentName == "podbridge.episodes.fetched" &&
                 m.Value == 2 &&
                 Equals(m.GetTag(Observability.PodcastIdTag), showConfig.PodcastId));
        meterListenerScope.Measurements.Should().ContainSingle(
            m => m.InstrumentName == "podbridge.refresh.success" &&
                 m.Value == 1 &&
                 Equals(m.GetTag(Observability.PodcastIdTag), showConfig.PodcastId));
        meterListenerScope.Measurements.Should().NotContain(m => m.InstrumentName == "podbridge.refresh.failure");
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithNoPodcastsConfigured_RecordsZeroPlatformAndPodcastCount()
    {
        // Arrange: boundary case for the "podcasts.Count > 0 ? 1 : 0" ternary - an empty database table
        // must record platformCount 0, distinct from any non-empty result set (which always records 1,
        // regardless of the exact count), and distinct from unconditionally recording 1 (mutant: `true`).
        using var activityListenerScope = new ActivityListenerScope();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        var refreshActivity = activityListenerScope.StoppedActivities.Single(
            activity => activity.GetTagItem(Observability.PlatformCountTag) is not null);
        refreshActivity.GetTagItem(Observability.PlatformCountTag).Should().Be(0);
        refreshActivity.GetTagItem(Observability.PodcastCountTag).Should().Be(0);
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
        var options1 = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper1 = new TestOptionsMonitor<PodBridgeOptions>(options1);
        using var sut1 = CreateTestee(optionsWrapper1, [show1Config]);
        await sut1.RefreshAllShowsAsync(CancellationToken.None);

        // Act: Second refresh with show1 + show2
        var options2 = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper2 = new TestOptionsMonitor<PodBridgeOptions>(options2);
        using var sut2 = CreateTestee(optionsWrapper2, [show1Config, show2Config]);
        await sut2.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        _podcastCache.FindFull(show1Config.PodcastId).Should().NotBeNull();
        _podcastCache.FindFull(show2Config.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenNewEpisodeIsAdded_CacheContainsAllEpisodes()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();

        var episode1 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 2" };

        var podcast1 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1).Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();

        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>())
            .Returns(podcast1, podcast2);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig]);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterFirst = _podcastCache.FindFull(showConfig.PodcastId);
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterSecond = _podcastCache.FindFull(showConfig.PodcastId);

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
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();

        var episode1 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 2" };

        var podcast1 = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithEpisodes(episode2).Build();

        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>())
            .Returns(podcast1, podcast2);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig]);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterFirst = _podcastCache.FindFull(showConfig.PodcastId);
        await testee.RefreshAllShowsAsync(CancellationToken.None);
        var cachedAfterSecond = _podcastCache.FindFull(showConfig.PodcastId);

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
            .Build();

        var succeedingPodcast = new PodcastBuilder().WithDefaults().WithTitle("Succeeding Show").Build();
        _episodeSourceMock.FetchEpisodesAsync(succeedingShowConfig, Arg.Any<CancellationToken>()).Returns(succeedingPodcast);
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Shared.Podcast>(_ => throw new InvalidOperationException("Simulated fetch failure"));

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        var fakeLogger = new FakeLogger<PodcastRefreshService>();
        using var testee = CreateTestee(optionsWrapper, [succeedingShowConfig, failingShowConfig], podcastRefreshLogger: fakeLogger);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a single failing show must not abort the whole refresh cycle");
        _podcastCache.FindFull(succeedingShowConfig.PodcastId).Should().NotBeNull();
        _podcastCache.FindFull(failingShowConfig.PodcastId).Should().BeNull();
        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Error);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenShowFailsWithActivityListenerRegistered_MarksActivityAsError()
    {
        // Arrange: registers a listener so the catch block's activity?.SetStatus(...)/AddException(...)
        // calls exercise the "activity is present" branch too (activity is null in the other failure test).
        // The recorded tag values, activity status, and failure counter measurement are asserted too, so
        // mutations to the failure counter, the SetStatus/AddException calls, and Observability.
        // RecordRefreshFailure(...) (invisible on any Activity) are caught (cache state alone doesn't
        // depend on them, since podcastCache.Update never runs on the failure path either way).
        using var activityListenerScope = new ActivityListenerScope();
        using var meterListenerScope = new MeterListenerScope();
        var failingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("failing-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Shared.Podcast>(_ => throw new InvalidOperationException("Simulated fetch failure"));

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [failingShowConfig]);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a single failing show must not abort the whole refresh cycle");
        _podcastCache.FindFull(failingShowConfig.PodcastId).Should().BeNull();

        var refreshActivity = activityListenerScope.StoppedActivities.Single(
            activity => activity.GetTagItem(Observability.PlatformCountTag) is not null);
        refreshActivity.GetTagItem(Observability.RefreshSuccessCountTag).Should().Be(0);
        refreshActivity.GetTagItem(Observability.RefreshFailureCountTag).Should().Be(1);

        var podcastActivity = activityListenerScope.StoppedActivities.Single(
            activity => activity.GetTagItem(Observability.PodcastIdTag) is not null);
        podcastActivity.Status.Should().Be(ActivityStatusCode.Error);
        podcastActivity.StatusDescription.Should().Be("Simulated fetch failure");
        podcastActivity.Events.Should().ContainSingle(e => e.Name == "exception");

        meterListenerScope.Measurements.Should().ContainSingle(
            m => m.InstrumentName == "podbridge.refresh.failure" &&
                 m.Value == 1 &&
                 Equals(m.GetTag(Observability.PodcastIdTag), failingShowConfig.PodcastId));
        meterListenerScope.Measurements.Should().NotContain(m => m.InstrumentName == "podbridge.refresh.success");
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenCancelledMidLoop_StopsProcessingRemainingShows()
    {
        // Arrange
        var firstShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("first-show").Build();
        var secondShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("second-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .Build();

        using var cts = new CancellationTokenSource();
        var firstPodcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(firstShowConfig, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return firstPodcast;
            });

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [firstShowConfig, secondShowConfig]);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _podcastCache.FindFull(firstShowConfig.PodcastId).Should().NotBeNull("the first show was already processed before cancellation");
        _podcastCache.FindFull(secondShowConfig.PodcastId).Should().BeNull("the loop must stop before processing the second show");
        await _episodeSourceMock.DidNotReceive().FetchEpisodesAsync(secondShowConfig, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenFetchThrowsCancellationButTokenWasNotCancelled_TreatsItAsAFailure()
    {
        // Arrange: HttpClient throws TaskCanceledException (an OperationCanceledException subtype) whenever a
        // request times out, regardless of whether the caller's own CancellationToken was ever cancelled. That
        // must be treated as a regular refresh failure - not as a real cancellation - otherwise it would abort
        // the whole refresh cycle and, with no BackgroundServiceExceptionBehavior configured, crash the host.
        var succeedingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("succeeding-show").Build();
        var timingOutShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("timing-out-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .Build();

        var succeedingPodcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(succeedingShowConfig, Arg.Any<CancellationToken>()).Returns(succeedingPodcast);
        _episodeSourceMock.FetchEpisodesAsync(timingOutShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Shared.Podcast>(_ => throw new TaskCanceledException("Simulated GraphQL timeout", new TimeoutException()));

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        var fakeLogger = new FakeLogger<PodcastRefreshService>();
        using var testee = CreateTestee(optionsWrapper, [succeedingShowConfig, timingOutShowConfig], podcastRefreshLogger: fakeLogger);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("a timed-out request must not abort the whole refresh cycle");
        _podcastCache.FindFull(succeedingShowConfig.PodcastId).Should().NotBeNull();
        _podcastCache.FindFull(timingOutShowConfig.PodcastId).Should().BeNull();
        _healthStateMock.Received(1).RecordResult(timingOutShowConfig.PodcastId, succeeded: false);
        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Error);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenFetchThrowsCancellationForTheGivenToken_PropagatesRealCancellation()
    {
        // Arrange: the exception is only a genuine shutdown request when it was raised for the exact
        // CancellationToken this refresh run was given - that case must keep propagating unchanged (unlike
        // the timeout case above) so BackgroundService shutdown semantics keep working.
        var failingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("failing-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();

        using var cts = new CancellationTokenSource();
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Shared.Podcast>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [failingShowConfig]);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _podcastCache.FindFull(failingShowConfig.PodcastId).Should().BeNull();
        _healthStateMock.DidNotReceive().RecordResult(failingShowConfig.PodcastId, Arg.Any<bool>());
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenShowSucceeds_RecordsSuccessInHealthState()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig]);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        _healthStateMock.Received(1).RecordResult(showConfig.PodcastId, succeeded: true);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithMixOfSucceedingAndFailingShows_LogsRollUpSummaryOnce()
    {
        // Arrange
        var succeedingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("succeeding-show").Build();
        var failingShowConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("failing-show").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .Build();

        var succeedingPodcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(succeedingShowConfig, Arg.Any<CancellationToken>()).Returns(succeedingPodcast);
        _episodeSourceMock.FetchEpisodesAsync(failingShowConfig, Arg.Any<CancellationToken>())
            .Returns<PodBridge.Logic.Shared.Podcast>(_ => throw new InvalidOperationException("Simulated fetch failure"));

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        var fakeLogger = new FakeLogger<EpisodeRefreshWorker>();
        using var testee = CreateTestee(optionsWrapper, [succeedingShowConfig, failingShowConfig], workerLogger: fakeLogger);

        // Act
        await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert - exactly one roll-up summary log for the whole cycle (not one per podcast), reporting
        // the succeeded/total counts regardless of individual failures.
        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Information)
            .Which.Message.Should().Contain("1/2 podcasts refreshed");
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenDatabaseIsUnreachable_LogsErrorAndSkipsCycleWithoutThrowing()
    {
        // Arrange - substitutes a broken IPodcastRepository to exercise the catch branch in
        // EpisodeRefreshWorker.FindPodcastsAsync (transient Postgres failure while loading the podcast list).
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        var fakeLogger = new FakeLogger<EpisodeRefreshWorker>();
        var brokenPodcastRepository = Substitute.For<IPodcastRepository>();
        brokenPodcastRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated database failure"));

        using var testee = CreateTesteeWithBrokenDatabase(brokenPodcastRepository, optionsWrapper, fakeLogger);

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Error)
            .Which.Message.Should().Be("Failed to load podcasts from the database; skipping this refresh cycle");
        await _episodeSourceMock.DidNotReceiveWithAnyArgs().FetchEpisodesAsync(default!, default);
    }

    [Test]
    public async Task RefreshAllShowsAsync_WhenCancelledWhileLoadingPodcasts_PropagatesOperationCanceledExceptionUncaught()
    {
        // Arrange - the "when" filter on FindPodcastsAsync's catch clause must NOT swallow a genuine
        // OperationCanceledException that occurred because the caller's own cancellationToken was
        // triggered (as opposed to some other, unrelated OperationCanceledException) - that case must
        // propagate normally like any other host-shutdown cancellation, not be treated as a "transient
        // Postgres failure" to skip and retry next cycle.
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var cts = new CancellationTokenSource();
        var cancellingPodcastRepository = Substitute.For<IPodcastRepository>();
        cancellingPodcastRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Throws(_ => new OperationCanceledException(cts.Token));

        using var testee = CreateTesteeWithBrokenDatabase(cancellingPodcastRepository, optionsWrapper, NullLogger<EpisodeRefreshWorker>.Instance);
        await cts.CancelAsync();

        // Act
        var act = async () => await testee.RefreshAllShowsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RefreshAllShowsAsync_WithNoConfiguredShows_CompletesWithoutError()
    {
        // Arrange
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper);

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
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig], timeProvider, continueLoop: () => false);

        // Act
        // ExecuteAsync is invoked directly via reflection (rather than via StartAsync, which runs it on
        // the ThreadPool through Task.Run) so it runs synchronously up to its first genuine await point -
        // avoiding a race between the ThreadPool scheduling the task and this test advancing the fake clock.
        var executeAsyncMethod = typeof(EpisodeRefreshWorker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeTask = (Task)executeAsyncMethod.Invoke(testee, [CancellationToken.None])!;
        timeProvider.Advance(TimeSpan.FromMinutes(podBridgeOptions.RefreshIntervalMinutes)); // triggers the tick that then stops the loop
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

        // Assert
        _podcastCache.FindFull(showConfig.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithDefaultLoopSeam_ContinuesLoopingUntilCancelled()
    {
        // Arrange: uses the 5-arg (production) constructor so the default `_continueLoop = () => true`
        // delegate is exercised, rather than the test-only seam constructor used by the other ExecuteAsync
        // test above.
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _episodeSourceMock.FetchEpisodesAsync(showConfig, Arg.Any<CancellationToken>()).Returns(podcast);

        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        using var cts = new CancellationTokenSource();

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        using var testee = CreateTestee(optionsWrapper, [showConfig], timeProvider);

        // Act
        var executeAsyncMethod = typeof(EpisodeRefreshWorker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeTask = (Task)executeAsyncMethod.Invoke(testee, [cts.Token])!;
        timeProvider.Advance(TimeSpan.FromMinutes(podBridgeOptions.RefreshIntervalMinutes)); // first tick: default continueLoop() runs and returns true
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, cts.Token); // lets the loop re-enter and start waiting on the timer again
        await cts.CancelAsync();
        var act = async () => await executeTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _podcastCache.FindFull(showConfig.PodcastId).Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithBackgroundRefreshDisabled_CompletesImmediatelyWithoutRefreshing()
    {
        // Arrange
        var showConfig = new PodcastConfigBuilder().WithDefaults().WithPodcastId("show1").Build();
        var podBridgeOptions = new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithBackgroundRefreshEnabled(false)
            .Build();

        var optionsWrapper = new TestOptionsMonitor<PodBridgeOptions>(podBridgeOptions);
        var fakeLogger = new FakeLogger<EpisodeRefreshWorker>();
        using var testee = CreateTestee(optionsWrapper, [showConfig], workerLogger: fakeLogger);

        // Act
        var executeAsyncMethod = typeof(EpisodeRefreshWorker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeTask = (Task)executeAsyncMethod.Invoke(testee, [CancellationToken.None])!;
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

        // Assert - completes on its own (no PeriodicTimer/cancellation needed) and never touches the cache
        await _episodeSourceMock.DidNotReceiveWithAnyArgs().FetchEpisodesAsync(default!, default);
        _podcastCache.FindFull(showConfig.PodcastId).Should().BeNull();
        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Information);
    }

    private EpisodeRefreshWorker CreateTesteeWithBrokenDatabase(
        IPodcastRepository brokenPodcastRepository,
        IOptionsMonitor<PodBridgeOptions> optionsWrapper,
        ILogger<EpisodeRefreshWorker> workerLogger)
    {
        var podcastRefreshService = new PodcastRefreshService(
            _episodeSourceMock,
            _podcastCache,
            _healthStateMock,
            NullLogger<PodcastRefreshService>.Instance);

        return new EpisodeRefreshWorker(
            podcastRefreshService,
            brokenPodcastRepository,
            optionsWrapper,
            TimeProvider.System,
            workerLogger);
    }

    private EpisodeRefreshWorker CreateTestee(
        IOptionsMonitor<PodBridgeOptions> optionsWrapper,
        IReadOnlyList<PodcastConfig>? podcasts = null,
        TimeProvider? timeProvider = null,
        ILogger<EpisodeRefreshWorker>? workerLogger = null,
        ILogger<PodcastRefreshService>? podcastRefreshLogger = null,
        Func<bool>? continueLoop = null)
    {
        var dbContextFactoryScope = new SqliteAppDbContextFactoryScope(podcasts);
        _dbContextFactoryScopes.Add(dbContextFactoryScope);
        var podcastRepository = new PodcastRepository(dbContextFactoryScope.DbContextFactory);

        var podcastRefreshService = new PodcastRefreshService(
            _episodeSourceMock,
            _podcastCache,
            _healthStateMock,
            podcastRefreshLogger ?? NullLogger<PodcastRefreshService>.Instance);

        return continueLoop is null
            ? new EpisodeRefreshWorker(
                podcastRefreshService,
                podcastRepository,
                optionsWrapper,
                timeProvider ?? TimeProvider.System,
                workerLogger ?? NullLogger<EpisodeRefreshWorker>.Instance)
            : new EpisodeRefreshWorker(
                podcastRefreshService,
                podcastRepository,
                optionsWrapper,
                timeProvider ?? TimeProvider.System,
                workerLogger ?? NullLogger<EpisodeRefreshWorker>.Instance,
                continueLoop);
    }
}
