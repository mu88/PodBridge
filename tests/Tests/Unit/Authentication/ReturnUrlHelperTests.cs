using FluentAssertions;
using NUnit.Framework;
using PodBridge.Api.Authentication;

namespace Tests.Unit.Authentication;

[TestFixture]
[Category("Unit")]
public sealed class ReturnUrlHelperTests
{
    [TestCase("/")]
    [TestCase("/podcasts/test-show")]
    [TestCase("/podcasts/test-show?format=json")]
    public void GetSafeDestination_WithLocalPath_ReturnsOriginalValue(string returnUrl)
    {
        // Act
        var safeDestination = ReturnUrlHelper.GetSafeDestination(returnUrl);

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
        // Act
        var safeDestination = ReturnUrlHelper.GetSafeDestination(returnUrl);

        // Assert
        safeDestination.Should().Be("/");
    }

    [Test]
    public void BuildLoginPath_WithSafeReturnUrl_EncodesReturnUrl()
    {
        // Act
        var loginPath = ReturnUrlHelper.BuildLoginPath("/podcasts/test-show?format=json");

        // Assert
        loginPath.Should().Be("/login?ReturnUrl=%2Fpodcasts%2Ftest-show%3Fformat%3Djson");
    }

    [Test]
    public void BuildLoginPath_WithUnsafeReturnUrl_UsesRoot()
    {
        // Act
        var loginPath = ReturnUrlHelper.BuildLoginPath("https://evil.example/phish");

        // Assert
        loginPath.Should().Be("/login?ReturnUrl=%2F");
    }
}
