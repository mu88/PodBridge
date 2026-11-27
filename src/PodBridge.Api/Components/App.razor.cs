namespace PodBridge.Api.Components;

// Keep the component type resolvable when Stryker cannot load the Razor source generator.
// Rendering remains in App.razor; see https://github.com/stryker-mutator/stryker-net/issues/3813.
#pragma warning disable MA0206 // Keep an explicit empty body for the Razor partial declaration.
public partial class App : Microsoft.AspNetCore.Components.ComponentBase { }
#pragma warning restore MA0206
