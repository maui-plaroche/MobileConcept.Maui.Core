namespace MobileConcept.Core.Helpers;

/// <summary>
/// Provides helper methods for accessing platform-specific MAUI application components.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Gets the current MAUI application instance.
    /// </summary>
    /// <returns>The current <see cref="Application"/> instance, or null if not available.</returns>
    public static Application? GetApplication() 
        => IPlatformApplication.Current?.Application as Application;

    /// <summary>
    /// Gets the current active window of the application.
    /// </summary>
    /// <returns>The first <see cref="Window"/> in the application's window collection, or null if no windows exist.</returns>
    public static Window? GetCurrentWindow() 
        => GetApplication()?.Windows.FirstOrDefault();

    /// <summary>
    /// Gets the main page of the current window.
    /// </summary>
    /// <returns>The main <see cref="Page"/> of the current window, or null if not available.</returns>
    public static Page? GetMainPage() 
        => GetCurrentWindow()?.Page;

    /// <summary>
    /// Gets the dependency injection service provider for the current platform application.
    /// </summary>
    /// <returns>The <see cref="IServiceProvider"/> for resolving services, or null if not available.</returns>
    public static IServiceProvider? GetServices() 
        => IPlatformApplication.Current?.Services;
}