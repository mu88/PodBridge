using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using PodBridge.Logic.Config;
using Tests.TestSupport.Builders;

namespace Tests.Unit.Pages;

[TestFixture]
[Category("Unit")]
public sealed class LoginTests
{
    private BunitContext _ctx = null!;
    private HttpContext _httpContext = null!;
    private RecordingNavigationManager _navigationManager = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _ctx = new BunitContext();
        _httpContext = new DefaultHttpContext();
        _navigationManager = new RecordingNavigationManager();
        _ctx.Services.AddSingleton<NavigationManager>(_navigationManager);
        _ctx.Services.AddSingleton(Options.Create(new PodBridgeOptionsBuilder().WithDefaults().Build()));
        _ctx.RenderTree.Add<CascadingValue<HttpContext>>(parameters => parameters.Add(p => p.Value, _httpContext));
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public void Render_WhenAuthenticationIsDisabled_NavigatesToApplicationRootWithForceLoad()
    {
        // Arrange

        // Act
        using var renderedLogin = _ctx.Render<PodBridge.Api.Components.Pages.Login>();

        // Assert
        renderedLogin.Markup.Should().NotBeNull();
        _navigationManager.LastUri.Should().Be("/");
        _navigationManager.LastForceLoad.Should().BeTrue();
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/");
        }

        public string? LastUri { get; private set; }

        public bool LastForceLoad { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastUri = uri;
            LastForceLoad = forceLoad;
        }
    }
}
