using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using Tests.TestSupport.Builders;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class GraphQlEpisodeSourceTests
{
    private HttpClient _httpClient = null!;
    private IOptions<PodBridgeOptions> _options = null!;
    private GraphQlEpisodeSource _testee = null!;
    private HttpMessageHandlerStub _httpMessageHandler = null!;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandler?.Dispose();
        _httpClient?.Dispose();
        _httpMessageHandler = new HttpMessageHandlerStub();
        _httpClient = new HttpClient(_httpMessageHandler);
        _options = Options.Create(new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(new Uri("https://fixture.test/graphql"))
            .Build());
        _testee = new GraphQlEpisodeSource(_httpClient, _options);
    }

    [TearDown]
    public void Teardown()
    {
        _httpMessageHandler?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public async Task FetchEpisodesAsync_ValidResponse_ReturnsEpisodesAndPodcastMetadata()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("valid-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
        result.Episodes[0].Title.Should().Be("Episode 1");
        result.Title.Should().Be("Fixture Show");
    }

    [Test]
    public async Task FetchEpisodesAsync_WithActivityListenerRegistered_RecordsPodcastIdTag()
    {
        // Arrange: registers a listener so Observability.Source.StartActivity() returns a non-null
        // Activity, exercising the "activity is present" branch of activity?.SetTag(...) (activity is
        // null in every other test here, since no listener is registered for PodBridge's ActivitySource
        // by default).
        using var activityListenerScope = new Tests.TestSupport.ActivityListenerScope();
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("activity-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build();
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
    }

    [Test]
    public async Task FetchEpisodesAsync_MultiplePages_AggregatesAllEpisodes()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("multi-page-show").Build();
        var item1 = new GraphQlItemBuilder().WithDefaults().Build() with { Title = "Episode 1", Id = "item1" };
        var item2 = new GraphQlItemBuilder().WithDefaults().Build() with { Title = "Episode 2", Id = "item2" };

        var page1Json = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder()
                .WithDefaults()
                .WithItems(item1)
                .WithPagination(hasNextPage: true, endCursor: "cursor1")
                .Build())
            .BuildJson();

        var page2Json = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder()
                .WithDefaults()
                .WithItems(item2)
                .WithPagination(hasNextPage: false, endCursor: null)
                .Build())
            .BuildJson();

// IDISP004: ownership of these HttpResponseMessage instances transfers to HttpMessageHandlerStub's
        // response queue and, from there, to the HttpClient/GraphQlEpisodeSource that consumes them.
#pragma warning disable IDISP004
        _httpMessageHandler.SetResponses(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page1Json) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page2Json) });
#pragma warning restore IDISP004

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(2);
        result.Episodes[0].Title.Should().Be("Episode 1");
        result.Episodes[1].Title.Should().Be("Episode 2");
    }

    [Test]
    public async Task FetchEpisodesAsync_PaginationExceedsMaxPages_ThrowsInvalidOperationException()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("runaway-pagination-show").Build();

// IDISP004: ownership of these HttpResponseMessage instances transfers to HttpMessageHandlerStub's
        // response queue and, from there, to the HttpClient/GraphQlEpisodeSource that consumes them.
#pragma warning disable IDISP004
        _httpMessageHandler.SetResponses(Enumerable.Range(1, 100)
            .Select(pageNumber =>
            {
                var item = new GraphQlItemBuilder().WithDefaults().Build() with { Id = $"item{pageNumber}" };
                var pageJson = new GraphQlResponseBuilder()
                    .WithDefaults()
                    .WithProgramSet(new ProgramSetBuilder()
                        .WithDefaults()
                        .WithItems(item)
                        .WithPagination(hasNextPage: true, endCursor: $"cursor{pageNumber}")
                        .Build())
                    .BuildJson();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(pageJson) };
            })
            .ToArray());
#pragma warning restore IDISP004

        // Act
        var act = async () => await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeded the maximum of 100 pages*");
    }

    [Test]
    public async Task FetchEpisodesAsync_GraphQlErrorsInResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("error-show").Build();
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithNoProgramSet()
            .WithErrors("Show not found", "Invalid ID")
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Show not found*Invalid ID*");
    }

    [Test]
    public async Task FetchEpisodesAsync_NullProgramSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("unknown-show").Build();
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithNoProgramSet()
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No program set was returned for show*");
    }

    [Test]
    public async Task FetchEpisodesAsync_NullResponseBody_ThrowsInvalidOperationException()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("null-body-show").Build();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, "null");

        // Act
        var act = async () => await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No program set was returned for show*");
    }

    [Test]
    public async Task FetchEpisodesAsync_DataPresentButProgramSetNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("data-no-program-set").Build();
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithDataWrapperButNoProgramSet()
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No program set was returned for show*");
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNoAudioUrl_FiltersOutEpisode()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("partial-audio-show").Build();
        var itemWithAudio = new GraphQlItemBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var itemNoAudio = new GraphQlItemBuilder().WithDefaults().Build() with { Title = "Episode 2", Audios = [] };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(itemWithAudio, itemNoAudio).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
        result.Episodes[0].Title.Should().Be("Episode 1");
    }

    [Test]
    public async Task FetchEpisodesAsync_PrefersMpegAudioFormat()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("audio-preference-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with
        {
            Audios =
            [
                new AssetType { Url = "https://fixture.test/audio.ogg", MimeType = "audio/ogg" },
                new AssetType { Url = "https://fixture.test/audio.mp3", MimeType = "audio/mpeg" },
            ],
        };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
        result.Episodes[0].AudioUrl.OriginalString.Should().Contain("audio.mp3");
        result.Episodes[0].AudioMimeType.Should().Be("audio/mpeg");
    }

    [Test]
    public async Task FetchEpisodesAsync_PreferredAudioHasOggMimeType_PropagatesOggMimeType()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("ogg-mimetype-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with
        {
            Audios = [new AssetType { Url = "https://fixture.test/audio.ogg", MimeType = "audio/ogg" }],
        };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
        result.Episodes[0].AudioMimeType.Should().Be("audio/ogg");
    }

    [Test]
    public async Task FetchEpisodesAsync_PreferredAudioHasNoMimeType_FallsBackToDefaultMpegMimeType()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("missing-mimetype-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with
        {
            Audios = [new AssetType { Url = "https://fixture.test/audio.bin", MimeType = string.Empty }],
        };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(1);
        result.Episodes[0].AudioMimeType.Should().Be("audio/mpeg");
    }

    [Test]
    public async Task FetchEpisodesAsync_ReplacesWidthPlaceholderInImageUrls()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("image-width-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build();
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().NotBeNull();
        result.ImageUrl!.OriginalString.Should().Contain("640");
        result.ImageUrl.OriginalString.Should().NotContain("{width}");
    }

    [Test]
    public async Task FetchEpisodesAsync_NoShowImage_PodcastImageUrlIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("no-image-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build();
        var programSet = new ProgramSetBuilder().WithDefaults().WithItems(item).Build() with { Image = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(programSet)
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullSharingUrl_EpisodeLinkIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("no-link-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { SharingUrl = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].Link.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullEpisodeNumber_EpisodeNumberIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("no-episode-number-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { EpisodeNumber = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].EpisodeNumber.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullDescription_FallsBackToShowNotes()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("fallback-description-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { Description = null, ShowNotes = "Fixture show notes" };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].Description.Should().Be("Fixture show notes");
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullDescriptionAndShowNotes_FallsBackToSummary()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("fallback-summary-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with { Description = null, ShowNotes = null, Summary = "Fixture summary" };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].Description.Should().Be("Fixture summary");
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithOnlySynopsis_FallsBackToSynopsis()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("fallback-synopsis-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with { Description = null, ShowNotes = null, Summary = null, Synopsis = "Fixture synopsis" };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].Description.Should().Be("Fixture synopsis");
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullImage_EpisodeImageUrlIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("no-episode-image-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { Image = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes[0].ImageUrl.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithNullAudiosList_FiltersOutEpisode()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("null-audios-show").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build() with { Audios = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().BeEmpty();
    }

    [Test]
    public async Task FetchEpisodesAsync_ItemWithMalformedAudioUrls_FiltersOutEpisode()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("malformed-audio-show").Build();
        var item = new GraphQlItemBuilder()
            .WithDefaults()
            .Build() with { Audios = [new AssetType { Url = "not-a-valid-url", MimeType = "audio/mpeg" }] };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(new ProgramSetBuilder().WithDefaults().WithItems(item).Build())
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Episodes.Should().BeEmpty();
    }

    [Test]
    public async Task FetchEpisodesAsync_ShowWithNullDescription_FallsBackToSynopsis()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("show-fallback-description").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build();
        var programSet = new ProgramSetBuilder().WithDefaults().WithItems(item).Build() with { Description = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(programSet)
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Description.Should().Be("Fixture show synopsis");
    }

    [Test]
    public async Task FetchEpisodesAsync_ShowWithNullPublicationService_AuthorIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("show-no-publisher").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build();
        var programSet = new ProgramSetBuilder().WithDefaults().WithItems(item).Build() with { PublicationService = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(programSet)
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Author.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_ShowWithNullSharingUrl_LinkIsNull()
    {
        // Arrange
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().WithShowId("show-no-link").Build();
        var item = new GraphQlItemBuilder().WithDefaults().Build();
        var programSet = new ProgramSetBuilder().WithDefaults().WithItems(item).Build() with { SharingUrl = null };
        var responseJson = new GraphQlResponseBuilder()
            .WithDefaults()
            .WithProgramSet(programSet)
            .BuildJson();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        result.Link.Should().BeNull();
    }

    [Test]
    public async Task FetchEpisodesAsync_NullGraphQlEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var optionsWithNullEndpoint = Options.Create(new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .Build());
        var testee = new GraphQlEpisodeSource(_httpClient, optionsWithNullEndpoint);
        var podcastConfig = new PodcastConfigBuilder().WithDefaults().Build();

        // Act
        var act = async () => await testee.FetchEpisodesAsync(podcastConfig, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GraphQlEndpoint must be configured*");
    }

    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content),
            });
        }

        public void SetResponses(params HttpResponseMessage[] responses)
        {
            foreach (var response in responses)
            {
                _responses.Enqueue(response);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
