using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Config;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class AuthOptionsTests
{
    [Test]
    public void Defaults_WhenNotConfigured_DisablesAuthWithEmptyHashes()
    {
        // Arrange
        var testee = new AuthOptions();

        // Assert
        testee.Enabled.Should().BeFalse();
        testee.UsernameHash.Should().BeEmpty();
        testee.PasswordHash.Should().BeEmpty();
    }
}
