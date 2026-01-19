using System.Threading.Tasks;

namespace MobileConcept.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels providing default lifecycle implementations.
/// </summary>
public interface IViewModelBase : IAppLifeCycleViewModel
{
    /// <summary>
    /// Called when the page appears. Override to refresh data.
    /// </summary>
    public Task OnAppearingAsync();

    /// <summary>
    /// Called when the page disappears. Override to cleanup resources.
    /// </summary>
    public Task OnDisappearingAsync();
    
    Task InitializeAsync(params object[] args);
}