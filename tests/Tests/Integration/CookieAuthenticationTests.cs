using FluentAssertions;
using FluentAssertions.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Tests.TestSupport.Http;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class CookieAuthenticationTests
{
    [Test]
    public async Task GetLogin_WithoutAuth_When_AuthEnabled_Returns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/login");

        // Assert
        response.Should().Be200Ok();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Sign in");
        html.Should().Contain("login-username");
        html.Should().Contain("login-password");
    }

    [Test]
    public async Task GetRoot_WithoutAuth_When_AuthEnabled_RedirectsToLoginWithReturnUrl()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be302Found();
        response.Headers.Location?.OriginalString.Should().Be("http://localhost/login?ReturnUrl=%2F");
    }

    [Test]
    public async Task GetStylesheet_WithoutAuth_When_AuthEnabled_Returns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/app.css");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task PostLogin_WithValidCredentials_RedirectsToReturnUrl_SetsCookie_AndAllowsRootAccess()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var loginResponse = await PostLoginAsync(client, "testuser", "testpass", "/");
        SetCookieHeader(client, loginResponse);
        using var rootResponse = await client.GetAsync("/");

        // Assert
        loginResponse.Should().Be302Found();
        loginResponse.Headers.Location?.OriginalString.Should().Be("http://localhost/");
        loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(cookie => cookie.Contains("PodBridge.UiAuth=", StringComparison.Ordinal));
        rootResponse.Should().Be200Ok();
    }

    [Test]
    public async Task PostLogin_WithValidCredentials_AndPodcastReturnUrl_RedirectsBackToRequestedPage()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await PostLoginAsync(client, "testuser", "testpass", "/podcasts/test-show");

        // Assert
        response.Should().Be302Found();
        response.Headers.Location?.OriginalString.Should().Be("http://localhost/podcasts/test-show");
    }

    [Test]
    public async Task PostLogin_WithInvalidCredentials_RerendersLoginWithGenericError_AndDoesNotSetAuthCookie()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await PostLoginAsync(client, "testuser", "wrongpass", "/");

        // Assert
        response.Should().Be200Ok();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Invalid username or password.");
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            cookies.Should().NotContain(cookie => cookie.Contains("PodBridge.UiAuth=", StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task PostLogin_WithEmptyUsername_RerendersLoginWithValidationError_AndDoesNotSetAuthCookie()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await PostLoginAsync(client, string.Empty, "testpass", "/");

        // Assert
        response.Should().Be200Ok();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Username is required.");
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            cookies.Should().NotContain(cookie => cookie.Contains("PodBridge.UiAuth=", StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task PostLogin_WithEmptyPassword_RerendersLoginWithValidationError_AndDoesNotSetAuthCookie()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await PostLoginAsync(client, "testuser", string.Empty, "/");

        // Assert
        response.Should().Be200Ok();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Password is required.");
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            cookies.Should().NotContain(cookie => cookie.Contains("PodBridge.UiAuth=", StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task PostLogin_WithExternalReturnUrl_RedirectsToRootInsteadOfExternalSite()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // Act
        using var response = await PostLoginAsync(client, "testuser", "testpass", "https://evil.example/phish");

        // Assert
        response.Should().Be302Found();
        response.Headers.Location?.OriginalString.Should().Be("http://localhost/");
    }

    [Test]
    public async Task PostLogout_AfterSuccessfulLogin_ClearsCookie_AndRootRedirectsToLoginAgain()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var loginResponse = await PostLoginAsync(client, "testuser", "testpass", "/");
        loginResponse.Should().Be302Found();
        SetCookieHeader(client, loginResponse);

        // Act
        using var logoutResponse = await PostLogoutAsync(client, string.Empty, "/");
        client.DefaultRequestHeaders.Remove("Cookie");
        using var rootResponse = await client.GetAsync("/");

        // Assert
        logoutResponse.Should().Be302Found();
        logoutResponse.Headers.Location?.OriginalString.Should().Be("/login?ReturnUrl=%2F");
        logoutResponse.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(cookie => cookie.Contains("PodBridge.UiAuth=;", StringComparison.Ordinal));
        rootResponse.Should().Be302Found();
        rootResponse.Headers.Location?.OriginalString.Should().Be("http://localhost/login?ReturnUrl=%2F");
    }

    [Test]
    public async Task GetRoot_WithoutAuth_When_AuthDisabled_Returns200()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(authEnabled: false);
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetLogin_When_AuthDisabled_RedirectsToRoot()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(authEnabled: false);
        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync("/login");

        // Assert
        response.Should().Be302Found();
        response.Headers.Location?.OriginalString.Should().Be("http://localhost/");
    }

    [Test]
    public async Task GetApi_WithUiCookieButWithoutBasicAuth_Returns401()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var loginResponse = await PostLoginAsync(client, "testuser", "testpass", "/");
        loginResponse.Should().Be302Found();
        SetCookieHeader(client, loginResponse);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be401Unauthorized();
    }

    private static TestWebApplicationFactory CreateFactory()
    {
        return new TestWebApplicationFactory(
            authEnabled: true,
            authUsername: "testuser",
            authPassword: "testpass");
    }

    private static HttpClient CreateClient(TestWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    private static void SetCookieHeader(HttpClient client, HttpResponseMessage response)
    {
        var cookieHeader = HtmlFormHelper.GetCookieHeader(response);
        client.DefaultRequestHeaders.Remove("Cookie");

        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string? username,
        string? password,
        string? returnUrl)
    {
        var getPath = string.IsNullOrEmpty(returnUrl) ? "/login" : $"/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        var formState = await HtmlFormHelper.GetAntiforgeryFormStateAsync(client, getPath);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", formState.CookieHeader);

        using var content = HtmlFormHelper.CreateLoginContent(formState.Token, username, password, returnUrl);
        return await client.PostAsync("/login", content);
    }

    private static async Task<HttpResponseMessage> PostLogoutAsync(
        HttpClient client,
        string antiforgeryToken,
        string? returnUrl)
    {
        using var content = HtmlFormHelper.CreateLogoutContent(antiforgeryToken, returnUrl);
        return await client.PostAsync("/logout", content);
    }
}
