using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PodBridge.Api;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class HttpRequestExtensionsTests
{
    [Test]
    public void ToBaseUrl_ValidRequest_BuildsSchemeHostAndPathBase()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("fixture.test");
        context.Request.PathBase = "/base";

        // Act
        var baseUrl = context.Request.ToBaseUrl();

        // Assert
        baseUrl.Should().Be("https://fixture.test/base");
    }
}
