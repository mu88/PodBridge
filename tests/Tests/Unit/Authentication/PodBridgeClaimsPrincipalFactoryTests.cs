using System.Security.Claims;
using FluentAssertions;
using NUnit.Framework;
using PodBridge.Api.Authentication;

namespace Tests.Unit.Authentication;

[TestFixture]
[Category("Unit")]
public sealed class PodBridgeClaimsPrincipalFactoryTests
{
    [Test]
    public void Create_WithSchemeAndUsername_ReturnsPrincipalWithNameClaimAndScheme()
    {
        // Act
        var principal = PodBridgeClaimsPrincipalFactory.Create("SomeScheme", "testuser");

        // Assert
        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.Identity.AuthenticationType.Should().Be("SomeScheme");
        principal.Identity.Name.Should().Be("testuser");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("testuser");
    }
}
