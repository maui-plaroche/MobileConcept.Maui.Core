#if ANDROID
using Android.App;
using Android.Content;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>
/// Init Firebase + capture de la notif initiale (cold start via tap).
/// À appeler par l'app dans MainActivity.OnCreate AVANT base.OnCreate.
/// </summary>
public static class FcmPlatformInit
{
    private const string PayloadExtraKey = "fcm_payload_json";

    private static NotificationPayload? _initialNotification;

    /// <summary>
    /// Init FirebaseApp (idempotent — pas d'erreur si déjà init).
    /// À appeler dans MainActivity.OnCreate AVANT base.OnCreate.
    /// </summary>
    public static void Initialize(Activity activity)
    {
        if (Firebase.FirebaseApp.GetInstance(Firebase.FirebaseApp.DefaultAppName) is null)
            Firebase.FirebaseApp.InitializeApp(activity);

        // Capture l'intent qui a launched l'app si elle contient un payload (cold start)
        HandleIntent(activity.Intent);
    }

    /// <summary>
    /// À appeler depuis MainActivity.OnNewIntent pour router les taps sur notif
    /// quand l'app est en background (déjà lancée).
    /// </summary>
    public static void HandleIntent(Intent? intent)
    {
        if (intent is null) return;

        var json = intent.GetStringExtra(PayloadExtraKey);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (data is null) return;

            var payload = PayloadDecoder.Decode(data);

            // Si l'instance du service existe déjà, raise immédiatement le tap event.
            // Sinon, on stocke comme initial notification (GetInitialNotification() en lira la valeur).
            if (PushNotificationService.Instance is { } svc)
                svc.RaiseNotificationTapped(payload);
            else
                _initialNotification = payload;
        }
        catch
        {
            // JSON malformé — on ignore silencieusement (poison message)
        }
    }

    /// <summary>Consommé une seule fois par IPushNotificationService.GetInitialNotification().</summary>
    internal static NotificationPayload? ConsumeInitialNotification()
    {
        var p = _initialNotification;
        _initialNotification = null;
        return p;
    }

    /// <summary>Clé d'extra utilisée par NotificationChannelManager pour construire le PendingIntent.</summary>
    internal static string IntentExtraKey => PayloadExtraKey;
}
#endif
