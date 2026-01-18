namespace MobileConcept.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels providing default lifecycle implementations.
/// </summary>
public interface IViewModelBase
{
    /// <summary>
    /// Called when the application starts. Override to add custom logic.
    /// </summary>
    public Task OnAppStartAsync();

    /// <summary>
    /// Called when the application resumes. Override to add custom logic.
    /// </summary>
    public Task OnAppResumeAsync();

    /// <summary>
    /// Called when the application goes to sleep. Override to add custom logic.
    /// </summary>
    public Task OnAppSleepAsync();

    /// <summary>
    /// Called to initialize the ViewModel with optional parameters.
    /// </summary>
    public Task InitializeAsync<T>(T? parameter = default) where T : class, IViewModelParameter;

    /// <summary>
    /// Called to update the ViewModel state with optional parameters.
    /// </summary>
    public Task UpdateViewModelAsync<T>(CancellationToken cancellationToken = default, T? parameter = default) 
        where T : class, IViewModelParameter;

    /// <summary>
    /// Called when the page appears. Override to refresh data.
    /// </summary>
    public Task OnAppearingAsync();

    /// <summary>
    /// Called when the page disappears. Override to cleanup resources.
    /// </summary>
    public Task OnDisappearingAsync();
}