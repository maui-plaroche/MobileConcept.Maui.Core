namespace MobileConcept.Core.Observers;

/// <summary>
/// 
/// </summary>
public interface IAppLifecycleNotifier
{
#if ANDROID
    /// <summary>
    /// 
    /// </summary>
    void NotifyStarted();   // Android only
#endif
    /// <summary>
    /// 
    /// </summary>
    void SetForeground();
    /// <summary>
    /// 
    /// </summary>
    void SetBackground();
}