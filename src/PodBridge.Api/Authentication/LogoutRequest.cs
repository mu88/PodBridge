namespace PodBridge.Api.Authentication;

internal sealed record LogoutRequest
{
    public string? ReturnUrl { get; init; }
}
