using System.Diagnostics.CodeAnalysis;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.PodcastManagement;

namespace Tests.Unit.Pages;

[TestFixture]
[Category("Unit")]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP006:Implement IDisposable", Justification = "NUnit tear-down disposes the bUnit context.")]
public sealed class AddPodcastTests
{
    private BunitContext _ctx = null!;
    private IPodcastDirectory _podcastDirectory = null!;
    private IPodcastManagementService _podcastManagementService = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new BunitContext();
        _podcastDirectory = Substitute.For<IPodcastDirectory>();
        _podcastManagementService = Substitute.For<IPodcastManagementService>();

        _ctx.Services.AddSingleton(_podcastDirectory);
        _ctx.Services.AddSingleton(_podcastManagementService);
        _ctx.Services.AddLogging();
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public async Task Search_WithMatchingShows_RendersResultRows()
    {
        // Arrange
        _podcastDirectory.SearchAsync("some show", Arg.Any<CancellationToken>())
            .Returns([new PodcastSearchResult("show-1", "Some Show", 12)]);

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#search-query").ChangeAsync("some show");
        await testee.FindAll("form")[0].SubmitAsync();

        // Assert
        var rows = testee.FindAll("tbody tr");
        rows.Should().HaveCount(1);
        rows[0].TextContent.Should().Contain("Some Show").And.Contain("show-1").And.Contain("12");
    }

    [Test]
    public async Task Search_WithNoMatches_ShowsEmptyState()
    {
        // Arrange
        _podcastDirectory.SearchAsync("no matches", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PodcastSearchResult>)[]);

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#search-query").ChangeAsync("no matches");
        await testee.FindAll("form")[0].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("No shows found for \"no matches\".");
    }

    [Test]
    public async Task Search_WhenDirectoryThrows_ShowsSearchError()
    {
        // Arrange
        _podcastDirectory.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PodcastSearchResult>>(_ => throw new HttpRequestException("boom"));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#search-query").ChangeAsync("some show");
        await testee.FindAll("form")[0].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("Search failed - please try again shortly.");
    }

    [Test]
    public async Task AddFromSearchResult_WhenAdded_ShowsConfirmationAndClearsResults()
    {
        // Arrange
        _podcastDirectory.SearchAsync("some show", Arg.Any<CancellationToken>())
            .Returns([new PodcastSearchResult("show-1", "Some Show", 12)]);
        _podcastManagementService.AddAsync("show-1", "Some Show", Arg.Any<CancellationToken>())
            .Returns(new PodcastAddResult(PodcastAddStatus.Added, "Some Show", "some-show"));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();
        await testee.Find("#search-query").ChangeAsync("some show");
        await testee.FindAll("form")[0].SubmitAsync();

        // Act
        await testee.FindAll("form")[1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("'Some Show' was added and is now being fetched in the background.");
        testee.FindAll("tbody tr").Should().BeEmpty();
    }

    [Test]
    public async Task AddByShowId_WithBlankShowId_ShowsValidationMessageWithoutCallingService()
    {
        // Arrange
        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#manual-show-id").ChangeAsync("   ");
        await testee.FindAll("form")[^1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("ShowId is required.");
        await _podcastManagementService.DidNotReceive().AddByShowIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddByShowId_WithKnownShow_ShowsConfirmation()
    {
        // Arrange
        _podcastManagementService.AddByShowIdAsync("manual-show", Arg.Any<CancellationToken>())
            .Returns(new PodcastAddResult(PodcastAddStatus.Added, "Manual Show", "manual-show"));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#manual-show-id").ChangeAsync("manual-show");
        await testee.FindAll("form")[^1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("'Manual Show' was added and is now being fetched in the background.");
    }

    [Test]
    public async Task AddByShowId_WithUnknownShow_ShowsNotFoundMessage()
    {
        // Arrange
        _podcastManagementService.AddByShowIdAsync("unknown-show", Arg.Any<CancellationToken>())
            .Returns(new PodcastAddResult(PodcastAddStatus.ShowIdNotFound));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#manual-show-id").ChangeAsync("unknown-show");
        await testee.FindAll("form")[^1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("No show found for ShowId 'unknown-show'.");
    }

    [Test]
    public async Task AddByShowId_WithAlreadyAddedShowId_ShowsDuplicateMessage()
    {
        // Arrange
        _podcastManagementService.AddByShowIdAsync("duplicate-show", Arg.Any<CancellationToken>())
            .Returns(new PodcastAddResult(PodcastAddStatus.ShowIdAlreadyExists, "Duplicate Show"));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#manual-show-id").ChangeAsync("duplicate-show");
        await testee.FindAll("form")[^1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("'Duplicate Show' has already been added.");
    }

    [Test]
    public async Task AddByShowId_WhenServiceReportsFailure_ShowsGenericFailureMessage()
    {
        // Arrange
        _podcastManagementService.AddByShowIdAsync("broken-show", Arg.Any<CancellationToken>())
            .Returns(new PodcastAddResult(PodcastAddStatus.Failed));

        using var testee = _ctx.Render<PodBridge.Api.Components.Pages.AddPodcast>();

        // Act
        await testee.Find("#manual-show-id").ChangeAsync("broken-show");
        await testee.FindAll("form")[^1].SubmitAsync();

        // Assert
        testee.Markup.Should().Contain("Failed to add podcast - please try again.");
    }
}
