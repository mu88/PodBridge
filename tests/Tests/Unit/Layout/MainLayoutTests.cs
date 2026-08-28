using System.Diagnostics.CodeAnalysis;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Api.Components.Layout;

namespace Tests.Unit.Layout;

[TestFixture]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001", Justification = "bUnit manages rendered component lifecycle")]
[Category("Unit")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MainLayoutTests
{
    private BunitContext _ctx = null!;
    private IHostEnvironment _hostEnvironment = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _ctx = new BunitContext();
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.EnvironmentName.Returns("Development");
        _ctx.Services.AddSingleton(_hostEnvironment);
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public void Render_WithBodyContent_RendersHeaderAndBody()
    {
        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.Find("h1").TextContent.Should().Be("PodBridge");
        testee.Find("main").TextContent.Should().Be("Example page content");
    }

    [Test]
    public void Render_InProductionEnvironment_ShowsGenericErrorMessage()
    {
        // Arrange
        _hostEnvironment.EnvironmentName.Returns("Production");

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.Find("#blazor-error-ui").TextContent.Should().Contain("An error has occurred.");
    }
}

