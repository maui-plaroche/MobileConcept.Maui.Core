namespace MobileConcept.Core.Views;

/// <summary>
/// 
/// </summary>
public interface IAppLifeCycleContentPage
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