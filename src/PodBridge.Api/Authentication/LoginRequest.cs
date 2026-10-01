namespace PodBridge.Api.Authentication;

internal sealed record LoginRequest
{
    // Blazor's [SupplyParameterFromForm] binder always assigns Username/Password explicitly on every POST
    // (even to an empty string when the form field is absent), so these declared defaults are never actually
    // observed - equivalent mutants. Kept for a valid object when Login.razor's OnInitialized constructs one
    // for the initial GET render, before any form has been posted.
    // Stryker disable once all: Blazor's form binder always assigns these explicitly; see comment above
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
