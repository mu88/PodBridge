using System.Diagnostics.CodeAnalysis;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using Tests.TestSupport;
using Tests.TestSupport.Builders;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class GraphQlPodcastDirectoryTests
{
    private HttpClient _httpClient = null!;
    private IOptionsMonitor<PodBridgeOptions> _options = null!;
    private GraphQlPodcastDirectory _testee = null!;
    private HttpMessageHandlerStub _httpMessageHandler = null!;

    [SetUp]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP014:Use a single instance of HttpClient", Justification = "Each test gets a fresh HttpClient wired to its own HttpMessageHandlerStub with test-specific canned responses; disposed in Setup/Teardown.")]
    public void Setup()
    {
        _httpMessageHandler?.Dispose();
        _httpClient?.Dispose();
        _httpMessageHandler = new HttpMessageHandlerStub();
        _httpClient = new HttpClient(_httpMessageHandler);
        _options = new TestOptionsMonitor<PodBridgeOptions>(new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(new Uri("https://fixture.test/graphql"))
            .Build());
        _testee = new GraphQlPodcastDirectory(_httpClient, _options, new GraphQlClient());
    }

    [TearDown]
    public void Teardown()
    {
        _httpMessageHandler?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public async Task SearchAsync_ValidResponse_ReturnsMappedResults()
    {
        // Arrange
        const string responseJson = """
            {
              "data": {
                "search": {
                  "programSets": {
                    "nodes": [
                      { "id": "show-1", "title": "Show One", "numberOfElements": 12 },
                      { "id": "show-2", "title": "Show Two", "numberOfElements": 3 }
                    ]
                  }
                }
              }
            }
            """;
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].ShowId.Should().Be("show-1");
        result[0].Title.Should().Be("Show One");
        result[0].EpisodeCount.Should().Be(12);
        result[1].ShowId.Should().Be("show-2");
    }

    [Test]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithoutCallingBackend()
    {
        // Act
        var result = await _testee.SearchAsync(string.Empty, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _httpMessageHandler.RequestCount.Should().Be(0);
    }

    [Test]
    public async Task SearchAsync_WhitespaceQuery_ReturnsEmptyWithoutCallingBackend()
    {
        // Act
        var result = await _testee.SearchAsync("   ", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _httpMessageHandler.RequestCount.Should().Be(0);
    }

    [Test]
    public async Task SearchAsync_NoMatchingShows_ReturnsEmptyList()
    {
        // Arrange
        const string responseJson = """{ "data": { "search": { "programSets": { "nodes": [] } } } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.SearchAsync("no matches", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchAsync_GraphQlErrors_ThrowsInvalidOperationException()
    {
        // Arrange
        const string responseJson = """{ "data": null, "errors": [ { "message": "search failed" } ] }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*search failed*");
    }

    [Test]
    public async Task SearchAsync_NullGraphQlEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var optionsWithNullEndpoint = new TestOptionsMonitor<PodBridgeOptions>(new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .Build());
        var testee = new GraphQlPodcastDirectory(_httpClient, optionsWithNullEndpoint, new GraphQlClient());

        // Act
        var act = async () => await testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*GraphQlEndpoint must be configured*");
    }

    [Test]
    public async Task FindPreviewAsync_ValidResponse_ReturnsPreviewWithResolvedImageWidth()
    {
        // Arrange
        const string responseJson = """
            {
              "data": {
                "programSet": {
                  "title": "Fixture Show",
                  "image": { "url": "https://images.example.test/cover-{width}.jpg" }
                }
              }
            }
            """;
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Fixture Show");
        result.ImageUrl.Should().Be(new Uri("https://images.example.test/cover-640.jpg"));
    }

    [Test]
    public async Task FindPreviewAsync_ResponseWithoutImage_ReturnsPreviewWithNullImageUrl()
    {
        // Arrange
        const string responseJson = """{ "data": { "programSet": { "title": "Fixture Show", "image": null } } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ImageUrl.Should().BeNull();
    }

    [Test]
    public async Task FindPreviewAsync_UnknownShowId_ReturnsNull()
    {
        // Arrange
        const string responseJson = """{ "data": { "programSet": null } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FindPreviewAsync("unknown-show-id", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task FindPreviewAsync_NullGraphQlEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var optionsWithNullEndpoint = new TestOptionsMonitor<PodBridgeOptions>(new PodBridgeOptionsBuilder()
            .WithDefaults()
            .WithGraphQlEndpoint(null)
            .Build());
        var testee = new GraphQlPodcastDirectory(_httpClient, optionsWithNullEndpoint, new GraphQlClient());

        // Act
        var act = async () => await testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*GraphQlEndpoint must be configured*");
    }

    [Test]
    public async Task FindPreviewAsync_ResponseBodyIsNull_ReturnsNullWithoutThrowing()
    {
        // Arrange - the whole GraphQL response body is JSON "null", exercising FindPreviewAsync's
        // "payload?.ProgramSet" null-conditional chain at its very first link.
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, "null");

        // Act
        var result = await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task SearchAsync_ResponseWithSearchButWithoutProgramSets_ReturnsEmptyListWithoutThrowing()
    {
        // Arrange - "search" is present but "programSets" is entirely absent, isolating the middle link
        // of the payload?.Search?.ProgramSets?.Nodes null-conditional chain (distinct from the
        // "search" key itself being absent, already covered by ResponseWithoutSearchData).
        const string responseJson = """{ "data": { "search": {} } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchAsync_ValidQuery_SendsQueryAndLimitAsGraphQlVariables()
    {
        // Arrange
        const string responseJson = """{ "data": { "search": { "programSets": { "nodes": [] } } } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        _httpMessageHandler.LastRequestBody.Should().Contain("\"query\":\"some query\"").And.Contain("\"limit\":10");
    }

    [Test]
    public async Task SearchAsync_ResponseWithoutSearchData_ReturnsEmptyListWithoutThrowing()
    {
        // Arrange - "search" is entirely absent from the payload (not just its "nodes"), exercising the
        // null-coalescing fallback across the whole payload?.Search?.ProgramSets?.Nodes chain.
        const string responseJson = """{ "data": {} }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchAsync_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        _httpMessageHandler.SetResponse(HttpStatusCode.InternalServerError, string.Empty);

        // Act
        var act = async () => await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task SearchAsync_EmptyErrorsArray_DoesNotThrow()
    {
        // Arrange - "errors" is present but empty, distinguishing "no errors at all" (fallback to Data)
        // from "at least one error" (throws), rather than any non-negative Count.
        const string responseJson = """{ "data": { "search": { "programSets": { "nodes": [] } } }, "errors": [] }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task SearchAsync_MultipleGraphQlErrors_JoinsMessagesWithSemicolon()
    {
        // Arrange
        const string responseJson = """{ "data": null, "errors": [ { "message": "error one" }, { "message": "error two" } ] }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be("error one; error two");
    }

    [Test]
    public async Task SearchAsync_GraphQlErrorWithoutMessage_UsesEmptyMessage()
    {
        // Arrange - the error object omits "message" entirely, exercising GraphQlError.Message's default value.
        const string responseJson = """{ "data": null, "errors": [ {} ] }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var act = async () => await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(string.Empty);
    }

    [Test]
    public async Task SearchAsync_ResponseBodyIsNull_ReturnsEmptyListWithoutThrowing()
    {
        // Arrange - the whole GraphQL response body is JSON "null", exercising ExecuteAsync's
        // "payload is null ? default : payload.Data" fallback.
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, "null");

        // Act
        var result = await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchAsync_NodeWithoutIdOrTitle_MapsThemToEmptyStrings()
    {
        // Arrange - the node omits both "id" and "title", exercising ProgramSetNode's default values.
        const string responseJson = """
            { "data": { "search": { "programSets": { "nodes": [ { "numberOfElements": 5 } ] } } } }
            """;
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.SearchAsync("some query", CancellationToken.None);

        // Assert
        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ShowId = string.Empty, Title = string.Empty, EpisodeCount = 5 });
    }

    [Test]
    public async Task FindPreviewAsync_ValidShowId_SendsShowIdAsGraphQlVariable()
    {
        // Arrange
        const string responseJson = """{ "data": { "programSet": null } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        _httpMessageHandler.LastRequestBody.Should().Contain("\"showId\":\"some-show-id\"");
    }

    [Test]
    public async Task FindPreviewAsync_ProgramSetWithoutTitle_MapsToEmptyString()
    {
        // Arrange - the programSet omits "title" entirely, exercising PreviewProgramSet's default value.
        const string responseJson = """{ "data": { "programSet": { "image": null } } }""";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be(string.Empty);
    }

    [Test]
    public async Task FindPreviewAsync_ImageUrlThatIsNotAValidAbsoluteUri_ReturnsNullImageUrl()
    {
        // Arrange - exercises CreateImageUrl's Uri.TryCreate failure branch: a relative/malformed URL
        // (once the "{width}" placeholder is substituted) can't be parsed as an absolute Uri.
        const string responseJson = """
            {
              "data": {
                "programSet": {
                  "title": "Fixture Show",
                  "image": { "url": "not a valid uri {width}" }
                }
              }
            }
            """;
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _testee.FindPreviewAsync("some-show-id", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ImageUrl.Should().BeNull();
    }

    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public int RequestCount { get; private set; }

        public string? LastRequestBody { get; private set; }

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responses.Dequeue();
        }
    }
}
