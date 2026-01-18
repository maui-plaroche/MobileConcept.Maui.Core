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
    Task OnAppResumeAsync();
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task OnAppSleepAsync();
}