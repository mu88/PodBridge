namespace PodBridge.Logic.Config;

public sealed class AuthOptions
{
    public bool Enabled { get; init; } = false;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
