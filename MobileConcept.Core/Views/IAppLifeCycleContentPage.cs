using System.Threading.Tasks;

namespace MobileConcept.Core.Views;

/// <summary>
/// 
/// </summary>
public interface IAppLifeCycleContentPage
{
    /// <summary>
    /// Invoked when the application starts. Implement this method to initialize
    /// resources, configure settings, or perform any required startup logic.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task OnAppStartAsync(); // -- Android only

    /// <summary>
    /// Invoked when the application transitions from the background to the foreground state.
    /// Implement this method to resume tasks or refresh resources that were paused or released
    /// while the app was in the background.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task OnAppEnterForegroundAsync();

    /// <summary>
    /// Invoked when the application transitions to the background state.
    /// Implement this method to perform tasks such as saving application state
    /// or releasing resources that are no longer needed while the app is not active.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task OnAppEnterBackgroundAsync();
}