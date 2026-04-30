namespace MobileConcept.Core.Services;

public class AppLifecycle : IAppLifecycle
{
    public event Func<Task>? AppEnterForegroundAsync;
    public event Func<Task>? AppEnterBackgroundAsync;

    internal async Task RaiseEnterForegroundAsync()
    {
        if (AppEnterForegroundAsync is not { } handler) return;
        foreach (Func<Task> subscriber in handler.GetInvocationList().Cast<Func<Task>>())
        {
            try { await subscriber(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppLifecycle.Foreground] {ex}");
            }
        }
    }

    internal async Task RaiseEnterBackgroundAsync()
    {
        if (AppEnterBackgroundAsync is not { } handler) return;
        foreach (Func<Task> subscriber in handler.GetInvocationList().Cast<Func<Task>>())
        {
            try { await subscriber(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppLifecycle.Background] {ex}");
            }
        }
    }
}
