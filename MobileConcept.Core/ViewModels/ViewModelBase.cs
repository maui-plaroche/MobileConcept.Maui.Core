namespace MobileConcept.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels providing default lifecycle implementations.
/// </summary>
public abstract class ViewModelBase : IViewModelBase
{
    /// <summary>
    /// Called when the application starts. Override to add custom logic.
    /// </summary>
    public virtual Task OnAppStartAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the application resumes. Override to add custom logic.
    /// </summary>
    public virtual Task OnAppResumeAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the application goes to sleep. Override to add custom logic.
    /// </summary>
    public virtual Task OnAppSleepAsync() => Task.CompletedTask;

    /// <summary>
    /// Called to initialize the ViewModel with optional parameters.
    /// </summary>
    public virtual Task InitializeAsync<T>(T? parameter = default) where T : class, IViewModelParameter
        => Task.CompletedTask;

    /// <summary>
    /// Called to update the ViewModel state with optional parameters.
    /// </summary>
    public virtual Task UpdateViewModelAsync<T>(CancellationToken cancellationToken = default, T? parameter = default) 
        where T : class, IViewModelParameter
        => Task.CompletedTask;

    /// <summary>
    /// Called when the page appears. Override to refresh data.
    /// </summary>
    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the page disappears. Override to cleanup resources.
    /// </summary>
    public virtual Task OnDisappearingAsync() => Task.CompletedTask;
}