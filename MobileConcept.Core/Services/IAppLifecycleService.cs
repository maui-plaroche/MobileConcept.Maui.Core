namespace MobileConcept.Core.Services;

/// <summary>
/// Defines methods and events for monitoring and handling application lifecycle events,
/// such as transitioning between foreground and background states and initial application startup.
/// </summary>
public interface IAppLifecycleService
{
    /// <summary>
    /// Event invoked when the application has successfully started
    /// and completed its initialization process. This event is exclusive
    /// to the Android platform.
    /// </summary>
    event Action AppStarted; // Android Only

    /// <summary>
    /// Event triggered when the application transitions from the background
    /// to the foreground state. Useful for handling tasks or refreshing
    /// resources when the app becomes active.
    /// </summary>
    event Action EnteredForeground; // Background → Foreground

    /// <summary>
    /// Event triggered when the application transitions from the foreground
    /// to the background. This event occurs during a state change where the
    /// application is no longer actively interacting with the user.
    /// </summary>
    event Action EnteredBackground; // Foreground → Background

    /// <summary>
    /// Indicates whether the application is currently in the foreground state.
    /// Returns true if the application is visible and actively interacting with the user;
    /// otherwise, returns false.
    /// </summary>
    bool IsForeground { get; }
}