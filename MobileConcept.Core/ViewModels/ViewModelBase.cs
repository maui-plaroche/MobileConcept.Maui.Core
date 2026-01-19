using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MobileConcept.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels providing default lifecycle implementations.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IViewModelBase
{
    /// <summary>
    /// Called when the page appears. Override to refresh data.
    /// </summary>
    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the page disappears. Override to cleanup resources.
    /// </summary>
    public virtual Task OnDisappearingAsync() => Task.CompletedTask;

    public virtual Task InitializeAsync(params object[] args) => Task.CompletedTask;

    /// <summary>
    /// Called when the application starts. Override to perform initialization logic or setup.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task OnAppStartAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the application enters the foreground. Override to handle any necessary refresh
    /// or state restoration that should occur when the application becomes active again.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task OnAppEnterForegroundAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the application enters the background. Override to handle necessary tasks
    /// such as saving application state or releasing resources when the application is no longer active.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task OnAppEnterBackgroundAsync() => Task.CompletedTask;
}