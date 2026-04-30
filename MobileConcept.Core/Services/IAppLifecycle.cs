namespace MobileConcept.Core.Services;

/// <summary>
/// App-wide lifecycle events fired when the application enters foreground or
/// background. Subscribe from any DI-resolved component (services, ViewModels,
/// AppShell) for cross-cutting concerns like session refresh, telemetry, etc.
/// Cold start is not fired through these events — it is the initial activation.
/// </summary>
public interface IAppLifecycle
{
    /// <summary>Fired when the app passes from background to foreground.</summary>
    event Func<Task>? AppEnterForegroundAsync;

    /// <summary>Fired when the app passes from foreground to background.</summary>
    event Func<Task>? AppEnterBackgroundAsync;
}
