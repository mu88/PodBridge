using FluentAssertions;
using NUnit.Framework;
using PodBridge.Api.Authentication;
using PodBridge.Logic.Config;
using PodBridge.Logic.Security;

namespace Tests.Unit.Authentication;

[TestFixture]
[Category("Unit")]
public sealed class ConfiguredCredentialValidatorTests
{
    [Test]
    public void AreValid_WithMatchingUsernameAndPassword_ReturnsTrue()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Enabled = true,
            UsernameHash = CredentialHasher.Hash("testuser"),
            PasswordHash = CredentialHasher.Hash("testpass"),
        };

        // Act
        var isValid = ConfiguredCredentialValidator.AreValid(authOptions, "testuser", "testpass");

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public void AreValid_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Enabled = true,
            UsernameHash = CredentialHasher.Hash("testuser"),
            PasswordHash = CredentialHasher.Hash("testpass"),
        };

        // Act
        var isValid = ConfiguredCredentialValidator.AreValid(authOptions, "testuser", "wrongpass");

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public void AreValid_WithWrongUsername_ReturnsFalse()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Enabled = true,
            UsernameHash = CredentialHasher.Hash("testuser"),
            PasswordHash = CredentialHasher.Hash("testpass"),
        };

        // Act
        var isValid = ConfiguredCredentialValidator.AreValid(authOptions, "wronguser", "testpass");

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public void AreValid_WithWrongUsernameAndWrongPassword_ReturnsFalse()
    {
        // Arrange - both hashes must be verified unconditionally (no short-circuiting), so this
        // covers the case where neither check alone would already determine the result.
        var authOptions = new AuthOptions
        {
            Enabled = true,
            UsernameHash = CredentialHasher.Hash("testuser"),
            PasswordHash = CredentialHasher.Hash("testpass"),
        };

        // Act
        var isValid = ConfiguredCredentialValidator.AreValid(authOptions, "wronguser", "wrongpass");

        // Assert
        isValid.Should().BeFalse();
    }
}
