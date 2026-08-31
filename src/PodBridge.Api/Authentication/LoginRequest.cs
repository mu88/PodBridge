namespace PodBridge.Api.Authentication;

internal sealed record LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
