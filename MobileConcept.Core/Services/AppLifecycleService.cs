using Microsoft.Maui.ApplicationModel;
using MobileConcept.Core.Observers;

namespace MobileConcept.Core.Services;

/// <summary>
/// Provides services and notifications related to the application's lifecycle,
/// including start, foreground, and background states. This class implements both
/// <see cref="IAppLifecycleService"/> and <see cref="IAppLifecycleNotifier"/>, combining
/// lifecycle notification responsibilities with state management functionality.
/// </summary>
public class AppLifecycleService : IAppLifecycleService, IAppLifecycleNotifier
{
    private bool _hasStarted;
    private bool _isForeground;

    /// <summary>
    /// Indicates whether the application is currently in the foreground.
    /// </summary>
    public bool IsForeground => _isForeground;

    /// <summary>
    /// Occurs when the application has started for the first time during its lifecycle.
    /// This event is specific to the Android platform.
    /// </summary>
    public event Action? AppStarted;
    /// <summary>
    /// 
    /// </summary>
    public event Action? EnteredForeground;

    /// <summary>
    /// Triggered when the application transitions from the foreground to the background.
    /// </summary>
    public event Action? EnteredBackground;

    // ANDROID ONLY
    /// <summary>
    /// Notifies that the application has started for the first time.
    /// This method sets the internal state to indicate the application has started
    /// and is in the foreground for the first time, then triggers the <see cref="IAppLifecycleService.AppStarted"/> event.
    /// </summary>
    public void NotifyStarted()
    {
        if (_hasStarted)
            return;

        _hasStarted = true;
        _isForeground = true;

        MainThread.BeginInvokeOnMainThread(() =>
            AppStarted?.Invoke());
    }

    /// <summary>
    /// Sets the application state to foreground.
    /// If the application has not started on Android, this method invokes <see cref="NotifyStarted"/>.
    /// If the application is already in the foreground, it does nothing.
    /// Otherwise, it updates the internal state to indicate that the application is in the foreground
    /// and triggers the <see cref="IAppLifecycleService.EnteredForeground"/> event.
    /// </summary>
    public void SetForeground()
    {
        if (!_hasStarted)
        {
#if ANDROID
            NotifyStarted();
            return;
#else
            _hasStarted = true;
#endif
        }

        if (_isForeground)
            return;

        _isForeground = true;

        MainThread.BeginInvokeOnMainThread(() =>
            EnteredForeground?.Invoke());
    }

    /// <summary>
    /// Sets the application state to background.
    /// If the application is already in the background, this method does nothing.
    /// Otherwise, it updates the internal state to indicate that the application has moved
    /// to the background and triggers the <see cref="IAppLifecycleService.EnteredBackground"/> event.
    /// </summary>
    public void SetBackground()
    {
        if (!_isForeground)
            return;

        _isForeground = false;

        MainThread.BeginInvokeOnMainThread(() =>
            EnteredBackground?.Invoke());
    }
}