using System.Text;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PodBridge.Api.Authentication;
using PodBridge.Logic.Config;
using PodBridge.Logic.Security;
using Tests.TestSupport;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class BasicAuthenticationHandlerTests
{
    private BasicAuthenticationHandler _testee = null!;
    private DefaultHttpContext _httpContext = null!;

    [SetUp]
    public async Task Setup()
    {
        var authOptions = new AuthOptions
        {
            Enabled = true,
            UsernameHash = CredentialHasher.Hash("testuser"),
            PasswordHash = CredentialHasher.Hash("testpass"),
        };
        var podBridgeOptions = new PodBridgeOptions { Auth = authOptions };

        _testee = new BasicAuthenticationHandler(
            new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new TestOptionsSnapshot<PodBridgeOptions>(podBridgeOptions));
        _httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(BasicAuthenticationHandler.SchemeName, BasicAuthenticationHandler.SchemeName, typeof(BasicAuthenticationHandler));
        await _testee.InitializeAsync(scheme, _httpContext);
    }

    [Test]
    public async Task AuthenticateAsync_MissingAuthorizationHeader_Fails()
    {
        // Act
        var result = await _testee.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Be("Missing or malformed Authorization header");
    }

    [Test]
    public async Task AuthenticateAsync_ValidCredentialsUnderExactAuthorizationHeaderKey_Succeeds()
    {
        // Arrange - the header must be looked up under the exact "Authorization" key; any other key
        // would not be found and would fall into the "missing header" failure branch instead.
        _httpContext.Request.Headers.Authorization = $"Basic {EncodeCredentials("testuser", "testpass")}";

        // Act
        var result = await _testee.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task AuthenticateAsync_AuthorizationHeaderNotValidBase64_FailsWithExpectedMessage()
    {
        // Arrange
        _httpContext.Request.Headers.Authorization = "Basic not-valid-base64!!!";

        // Act
        var result = await _testee.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Be("Authorization header is not valid Base64");
    }

    [Test]
    public async Task AuthenticateAsync_WrongPassword_FailsWithExpectedMessage()
    {
        // Arrange
        _httpContext.Request.Headers.Authorization = $"Basic {EncodeCredentials("testuser", "wrongpassword")}";

        // Act
        var result = await _testee.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Be("Invalid username or password");
    }

    [Test]
    public async Task ChallengeAsync_SetsExpectedWwwAuthenticateHeader()
    {
        // Act
        await _testee.ChallengeAsync(properties: null);

        // Assert
        _httpContext.Response.Headers.WWWAuthenticate.ToString().Should().Be("Basic realm=\"PodBridge\", charset=\"UTF-8\"");
    }

    private static string EncodeCredentials(string username, string password)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }
}
