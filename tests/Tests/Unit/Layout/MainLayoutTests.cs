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
using PodBridge.Logic.Versioning;
using Tests.TestSupport;
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
    private IOptionsSnapshot<PodBridgeOptions> _options = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _ctx = new BunitContext();
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.EnvironmentName.Returns("Development");
        _httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        _options = new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().Build());
        _ctx.Services.AddSingleton(_hostEnvironment);
        _ctx.Services.AddSingleton<IHttpContextAccessor>(_httpContextAccessor);
        _ctx.Services.AddSingleton(_options);
        _ctx.Services.AddSingleton<IAppVersionProvider>(new AppVersionProvider("1.4.2+abcdef0"));
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
    public void Render_WithBodyContent_ShowsDisplayVersionInFooter()
    {
        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.Find(".app-version").TextContent.Should().Be("v1.4.2");
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
        _ctx.Services.AddSingleton<IOptionsSnapshot<PodBridgeOptions>>(new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));
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
        _ctx.Services.AddSingleton<IOptionsSnapshot<PodBridgeOptions>>(new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.FindAll("form.logout-form").Should().BeEmpty();
    }

    [Test]
    public void Render_WhenAuthEnabledAndHttpContextIsNull_HidesLogoutForm()
    {
        // Arrange - isolates the "HttpContextAccessor.HttpContext?" null-conditional operand of the
        // compound guard: no HttpContext at all (e.g. outside a request) must not throw.
        _httpContextAccessor.HttpContext = null;
        _ctx.Services.AddSingleton<IOptionsSnapshot<PodBridgeOptions>>(new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.FindAll("form.logout-form").Should().BeEmpty();
    }

    [Test]
    public void Render_WhenAuthEnabledAndUserHasNoIdentity_HidesLogoutForm()
    {
        // Arrange - isolates the "User.Identity?" null-conditional operand: a ClaimsPrincipal with zero
        // identities (User.Identity itself is null), distinct from an identity that merely isn't authenticated.
        _ctx.Services.AddSingleton<IOptionsSnapshot<PodBridgeOptions>>(new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));
        _httpContextAccessor.HttpContext!.User = new ClaimsPrincipal();

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.FindAll("form.logout-form").Should().BeEmpty();
    }

    [Test]
    public void Render_WhenAuthenticatedWithoutNameClaim_ShowsSignedInAsAnonymousWithQuestionMarkAvatar()
    {
        // Arrange - isolates the "?? string.Empty" fallback for the username and GetInitial's
        // "IsNullOrEmpty(name)" branch: an authenticated identity without a Name claim.
        _ctx.Services.AddSingleton<IOptionsSnapshot<PodBridgeOptions>>(new TestOptionsSnapshot<PodBridgeOptions>(new PodBridgeOptionsBuilder().WithDefaults().WithAuth(true).Build()));
        _httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity([], "PodBridgeUiCookie"));

        // Act
        var testee = _ctx.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddContent(0, "Example page content")));

        // Assert
        testee.Find(".user-avatar").TextContent.Should().Be("?");
        testee.Markup.Should().Contain("Signed in as ");
    }
}
