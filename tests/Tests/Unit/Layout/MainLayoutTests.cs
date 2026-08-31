using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Api.Components.Layout;
using PodBridge.Logic.Config;
using Tests.TestSupport.Builders;

namespace Tests.Unit.Layout;

[TestFixture]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001", Justification = "bUnit manages rendered component lifecycle")]
[Category("Unit")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MainLayoutTests
{
    private BunitContext _ctx = null!;
    private IHostEnvironment _hostEnvironment = null!;
    private HttpContextAccessor _httpContextAccessor = null!;
    private IOptions<PodBridgeOptions> _options = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _ctx = new BunitContext();
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.EnvironmentName.Returns("Development");
        _httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        _options = Options.Create(new PodBridgeOptionsBuilder().WithDefaults().Build());
        _ctx.Services.AddSingleton(_hostEnvironment);
        _ctx.Services.AddSingleton<IHttpContextAccessor>(_httpContextAccessor);
        _ctx.Services.AddSingleton(_options);
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

    [Test]
    public void Render_WhenAuthenticatedAndAuthEnabled_ShowsLogoutForm()
    {
        // Arrange
        _ctx.Services.AddSingleton(Options.Create(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));
        _httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "testuser")], "PodBridgeUiCookie"));

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.Find("form.logout-form").Should().NotBeNull();
        testee.Markup.Should().Contain("Signed in as testuser");
    }

    [Test]
    public void Render_WhenAuthEnabledButNotAuthenticated_HidesLogoutForm()
    {
        // Arrange
        _ctx.Services.AddSingleton(Options.Create(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.FindAll("form.logout-form").Should().BeEmpty();
    }
}
