#if ANDROID
using Android.Gms.Tasks;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>
/// Convertit Android.Gms.Tasks.Task (Google Play Services Tasks) en
/// System.Threading.Tasks.Task. Pattern Xamarin standard via paire de listeners.
/// </summary>
internal static class GooglePlayTaskExtensions
{
    /// <summary>Pour Task qui retourne un Java object (string, etc.).</summary>
    public static Task<TResult?> AsAsync<TResult>(this global::Android.Gms.Tasks.Task task)
        where TResult : Java.Lang.Object
    {
        var tcs = new TaskCompletionSource<TResult?>();
        task.AddOnSuccessListener(new SuccessListener<TResult>(r => tcs.TrySetResult(r)));
        task.AddOnFailureListener(new FailureListener(ex => tcs.TrySetException(ex)));
        task.AddOnCanceledListener(new CanceledListener(() => tcs.TrySetCanceled()));
        return tcs.Task;
    }

    /// <summary>Pour Task qui retourne une chaîne (FCM token).</summary>
    public static Task<string?> AsStringAsync(this global::Android.Gms.Tasks.Task task)
    {
        var tcs = new TaskCompletionSource<string?>();
        task.AddOnSuccessListener(new SuccessListenerRaw(r => tcs.TrySetResult(r?.ToString())));
        task.AddOnFailureListener(new FailureListener(ex => tcs.TrySetException(ex)));
        task.AddOnCanceledListener(new CanceledListener(() => tcs.TrySetCanceled()));
        return tcs.Task;
    }

    /// <summary>Pour Task void (SubscribeToTopic, DeleteToken, etc.).</summary>
    public static global::System.Threading.Tasks.Task AsVoidAsync(this global::Android.Gms.Tasks.Task task)
    {
        var tcs = new TaskCompletionSource<bool>();
        task.AddOnSuccessListener(new SuccessListenerRaw(_ => tcs.TrySetResult(true)));
        task.AddOnFailureListener(new FailureListener(ex => tcs.TrySetException(ex)));
        task.AddOnCanceledListener(new CanceledListener(() => tcs.TrySetCanceled()));
        return tcs.Task;
    }

    // ----- Listeners internes -----

    private sealed class SuccessListener<TResult> : Java.Lang.Object, IOnSuccessListener
        where TResult : Java.Lang.Object
    {
        private readonly Action<TResult?> _action;
        public SuccessListener(Action<TResult?> action) => _action = action;
        public void OnSuccess(Java.Lang.Object? result) => _action(result as TResult);
    }

    private sealed class SuccessListenerRaw : Java.Lang.Object, IOnSuccessListener
    {
        private readonly Action<Java.Lang.Object?> _action;
        public SuccessListenerRaw(Action<Java.Lang.Object?> action) => _action = action;
        public void OnSuccess(Java.Lang.Object? result) => _action(result);
    }

    private sealed class FailureListener : Java.Lang.Object, IOnFailureListener
    {
        private readonly Action<Java.Lang.Exception> _action;
        public FailureListener(Action<Java.Lang.Exception> action) => _action = action;
        public void OnFailure(Java.Lang.Exception ex) => _action(ex);
    }

    private sealed class CanceledListener : Java.Lang.Object, IOnCanceledListener
    {
        private readonly Action _action;
        public CanceledListener(Action action) => _action = action;
        public void OnCanceled() => _action();
    }
}
#endif
