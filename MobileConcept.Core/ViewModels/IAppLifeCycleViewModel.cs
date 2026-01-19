using System.Threading.Tasks;

namespace MobileConcept.Core.ViewModels;

/// <summary>
/// 
/// </summary>
public interface IAppLifeCycleViewModel
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task OnAppStartAsync();
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task OnAppEnterForegroundAsync();
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task OnAppEnterBackgroundAsync();
}