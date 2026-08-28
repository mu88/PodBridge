using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
public sealed class NotFoundPageTests
{
    [Test]
    public async Task GetNonExistentEndpoint_Returns404WithNotFoundPage()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/this-does-not-exist");

        // Assert
        response.Should().Be404NotFound();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Not Found");
    }
}
