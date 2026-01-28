using System.Threading.Tasks;

namespace MobileConcept.Core.ViewModels;

/// <summary>
/// Base interface for all ViewModels providing default lifecycle implementations.
/// </summary>
public interface IViewModelBase : IAppLifeCycleViewModel
{
    /// <summary>
    /// Called when the page appears. Override to refresh data.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnAppearingAsync();

    /// <summary>
    /// Called when the page disappears. Override to cleanup resources.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnDisappearingAsync();

    /// <summary>
    /// Called after navigation to initialize the ViewModel with passed parameters.
    /// </summary>
    /// <param name="args">The navigation parameters passed to this ViewModel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(params object[] args);
}