using FluentAssertions;
using NUnit.Framework;
using PodBridge.Api.Observability;
using PodBridge.Logic.Versioning;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class AppVersionResourceDetectorTests
{
    [Test]
    public void Detect_ReturnsServiceVersionAttributeFromAppVersionProvider()
    {
        // Arrange
        var appVersionProvider = new AppVersionProvider("1.4.2+abcdef0");
        var testee = new AppVersionResourceDetector(appVersionProvider);

        // Act
        var resource = testee.Detect();

        // Assert
        resource.Attributes.Should().ContainSingle(attribute =>
            attribute.Key == "service.version" && Equals(attribute.Value, "1.4.2+abcdef0"));
    }
}
