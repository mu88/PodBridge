using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Versioning;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class AppVersionProviderTests
{
    [Test]
    public void DisplayVersion_WithSourceRevisionSuffix_StripsSuffix()
    {
        // Arrange
        var testee = new AppVersionProvider("1.4.2+abcdef0");

        // Assert
        testee.DisplayVersion.Should().Be("1.4.2");
    }

    [Test]
    public void FullVersion_WithSourceRevisionSuffix_KeepsSuffix()
    {
        // Arrange
        var testee = new AppVersionProvider("1.4.2+abcdef0");

        // Assert
        testee.FullVersion.Should().Be("1.4.2+abcdef0");
    }

    [Test]
    public void DisplayVersion_WithoutSourceRevisionSuffix_ReturnsVersionUnchanged()
    {
        // Arrange
        var testee = new AppVersionProvider("1.4.2");

        // Assert
        testee.DisplayVersion.Should().Be("1.4.2");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void DisplayVersion_WithNullOrWhitespaceInformationalVersion_ReturnsUnknown(string? informationalVersion)
    {
        // Arrange
        var testee = new AppVersionProvider(informationalVersion);

        // Assert
        testee.DisplayVersion.Should().Be("unknown");
        testee.FullVersion.Should().Be("unknown");
    }
}
