using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Security;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class CredentialHasherTests
{
    [Test]
    public void Verify_WithMatchingValue_ReturnsTrue()
    {
        // Arrange
        var hash = CredentialHasher.Hash("correct-password");

        // Act
        var result = CredentialHasher.Verify("correct-password", hash);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Verify_WithNonMatchingValue_ReturnsFalse()
    {
        // Arrange
        var hash = CredentialHasher.Hash("correct-password");

        // Act
        var result = CredentialHasher.Verify("wrong-password", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Hash_CalledTwiceWithSameValue_ProducesDifferentHashesDueToRandomSalt()
    {
        // Act
        var first = CredentialHasher.Hash("same-value");
        var second = CredentialHasher.Hash("same-value");

        // Assert
        first.Should().NotBe(second);
        CredentialHasher.Verify("same-value", first).Should().BeTrue();
        CredentialHasher.Verify("same-value", second).Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("not-enough-parts")]
    [TestCase("too.many.parts.here")]
    [TestCase("not-a-number.c2FsdA==.aGFzaA==")]
    [TestCase("210000.not-valid-base64!!!.aGFzaA==")]
    [TestCase("210000.c2FsdA==.not-valid-base64!!!")]
    public void Verify_WithMalformedStoredHash_ReturnsFalse(string malformedStoredHash)
    {
        // Act
        var result = CredentialHasher.Verify("any-value", malformedStoredHash);

        // Assert
        result.Should().BeFalse();
    }
}
