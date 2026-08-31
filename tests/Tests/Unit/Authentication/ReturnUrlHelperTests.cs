using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PodBridge.Api.Authentication;

namespace Tests.Unit.Authentication;

[TestFixture]
[Category("Unit")]
public sealed class ReturnUrlHelperTests
{
    [Test]
    public void GetApplicationRoot_WithoutPathBase_ReturnsSlash()
    {
        // Arrange
        var pathBase = PathString.Empty;

        // Act
        var root = ReturnUrlHelper.GetApplicationRoot(pathBase);

        // Assert
        root.Should().Be("/");
    }

    [Test]
    public void GetApplicationRoot_WithPathBase_ReturnsPathBaseSlash()
    {
        // Arrange
        var pathBase = new PathString("/podbridge");

        // Act
        var root = ReturnUrlHelper.GetApplicationRoot(pathBase);

        // Assert
        root.Should().Be("/podbridge/");
    }

    [TestCase("/")]
    [TestCase("/podcasts/test-show")]
    [TestCase("/podcasts/test-show?format=json")]
    public void GetSafeDestination_WithLocalPath_ReturnsOriginalValue(string returnUrl)
    {
        // Arrange
        var pathBase = PathString.Empty;

        // Act
        var safeDestination = ReturnUrlHelper.GetSafeDestination(returnUrl, pathBase);

        // Assert
        safeDestination.Should().Be(returnUrl);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("https://evil.example/phish")]
    [TestCase("//evil.example/phish")]
    [TestCase("/\\evil.example/phish")]
    public void GetSafeDestination_WithUnsafeValue_ReturnsRoot(string? returnUrl)
    {
        // Arrange
        var pathBase = PathString.Empty;

        // Act
        var safeDestination = ReturnUrlHelper.GetSafeDestination(returnUrl, pathBase);

        // Assert
        safeDestination.Should().Be("/");
    }

    [Test]
    public void BuildLoginPath_WithSafeReturnUrl_EncodesReturnUrl()
    {
        // Arrange
        var pathBase = PathString.Empty;

        // Act
        var loginPath = ReturnUrlHelper.BuildLoginPath(pathBase, "/podcasts/test-show?format=json");

        // Assert
        loginPath.Should().Be("/login?ReturnUrl=%2Fpodcasts%2Ftest-show%3Fformat%3Djson");
    }

    [Test]
    public void BuildLoginPath_WithUnsafeReturnUrl_UsesPathBaseRoot()
    {
        // Arrange
        var pathBase = new PathString("/podbridge");

        // Act
        var loginPath = ReturnUrlHelper.BuildLoginPath(pathBase, "https://evil.example/phish");

        // Assert
        loginPath.Should().Be("/podbridge/login?ReturnUrl=%2Fpodbridge%2F");
    }
}
