#if ANDROID
using Android.App;
using Firebase.Messaging;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>
/// Subclass de FirebaseMessagingService — enregistré dans AndroidManifest.xml par
/// le manifest merger NuGet (intent-filter MESSAGING_EVENT).
/// </summary>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class FcmAndroidService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        if (PushNotificationService.Instance is { } svc)
            svc.HandleNewToken(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        // Convertit RemoteMessage.Data (IDictionary<string,string> Java) → Dict .NET
        var data = new Dictionary<string, string>();
        foreach (var kv in message.Data)
            data[kv.Key] = kv.Value;

        var payload = PayloadDecoder.Decode(data);

        if (PushNotificationService.Instance is { } svc)
        {
            var isForeground = IsAppInForeground();
            if (isForeground)
            {
                if (svc.Options.ShowNotificationInForeground)
                    // Toujours attacher le PendingIntent : permet tap pour dismiss
                    // (autoCancel) + ramener l'app au foreground si elle est passée
                    // background entre la display et le tap.
                    NotificationChannelManager.Display(payload, svc.Options, withDeepLinkIntent: true);
                svc.RaiseNotificationReceived(payload);
            }
            else
            {
                // Background : on construit nous-même la notif (data-only payload)
                NotificationChannelManager.Display(payload, svc.Options, withDeepLinkIntent: true);
            }
        }
        else
        {
            // Service pas encore instancié (app started by FCM) — affiche quand même
            // pour ne pas perdre la notif.
        }
    }

    /// <summary>
    /// Détecte si l'application a au moins une activity au foreground.
    /// Méthode approximative mais robuste — utilise l'API ActivityManager.
    /// </summary>
    private static bool IsAppInForeground()
    {
        var ctx = global::Android.App.Application.Context;
        if (ctx.GetSystemService(global::Android.Content.Context.ActivityService)
            is not global::Android.App.ActivityManager am)
            return false;

        var pkg = ctx.PackageName;
        var processes = am.RunningAppProcesses;
        if (processes is null) return false;

        foreach (var p in processes)
        {
            if (p.ProcessName == pkg
                && p.Importance == global::Android.App.Importance.Foreground)
                return true;
        }
        return false;
    }
}
#endif
