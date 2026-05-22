#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>
/// Crée les NotificationChannel Android au boot (Android 8+ requirement)
/// et construit/affiche les notifications système quand on est en background
/// ou que ShowNotificationInForeground est true.
/// </summary>
internal static class NotificationChannelManager
{
    private static bool _channelsEnsured;

    /// <summary>
    /// Idempotent. Crée tous les channels déclarés par opts.AndroidChannels + un
    /// channel fallback. À appeler depuis PushNotificationService.ctor.
    /// </summary>
    public static void EnsureChannels(FCMOptions opts)
    {
        if (_channelsEnsured) return;
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) { _channelsEnsured = true; return; }

        var ctx = global::Android.App.Application.Context;
        var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        if (nm is null) return;

        // Fallback channel toujours créé (utilisé si data[ChannelIdDataField] absent)
        Create(nm, opts.FallbackChannelId, "Notifications", NotificationImportance.Default, null);

        foreach (var ch in opts.AndroidChannels)
            Create(nm, ch.Id, ch.Name, MapImportance(ch.Importance), ch.Description);

        _channelsEnsured = true;
    }

    private static void Create(
        NotificationManager nm,
        string id,
        string name,
        NotificationImportance importance,
        string? description)
    {
        var existing = nm.GetNotificationChannel(id);
        if (existing is not null) return;   // pas de re-création (sinon override les prefs user)

        var channel = new NotificationChannel(id, name, importance);
        if (description is not null) channel.Description = description;
        nm.CreateNotificationChannel(channel);
    }

    private static NotificationImportance MapImportance(Importance i) => i switch
    {
        Importance.Min     => NotificationImportance.Min,
        Importance.Low     => NotificationImportance.Low,
        Importance.Default => NotificationImportance.Default,
        Importance.High    => NotificationImportance.High,
        Importance.Max     => NotificationImportance.Max,
        _ => NotificationImportance.Default
    };

    /// <summary>
    /// Construit + affiche une notification Android pour le payload. Le PendingIntent
    /// embarque le payload JSON pour permettre le routing au tap.
    /// </summary>
    public static void Display(NotificationPayload payload, FCMOptions opts, bool withDeepLinkIntent)
    {
        var ctx = global::Android.App.Application.Context;

        var channelId = ResolveChannelId(payload, opts);

        var builder = new NotificationCompat.Builder(ctx, channelId)
            .SetContentTitle(payload.Title ?? "")
            .SetContentText(payload.Body ?? "")
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityHigh);

        if (opts.DefaultSmallIconResourceId != 0)
            builder.SetSmallIcon(opts.DefaultSmallIconResourceId);

        if (withDeepLinkIntent)
        {
            // Récupère l'intent de launch standard via PackageManager (robust pour
            // MAUI où Assembly.GetEntryAssembly() peut ne pas retourner l'app).
            // Fallback ResolveMainActivityType si jamais le PackageManager retourne null.
            var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);

            if (launch is null)
            {
                var mainActivityType = ResolveMainActivityType();
                if (mainActivityType is not null)
                {
                    launch = new Intent(ctx, mainActivityType);
                }
            }

            if (launch is not null)
            {
                launch.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                launch.PutExtra(
                    FcmPlatformInit.IntentExtraKey,
                    System.Text.Json.JsonSerializer.Serialize(payload.Data));

                // requestCode unique par notif pour permettre plusieurs notifs
                // simultanées avec extras différents (sinon UpdateCurrent
                // écrase le payload de la précédente)
                var requestCode = System.Threading.Interlocked.Increment(ref _intentCounter);
                var pendingIntent = PendingIntent.GetActivity(
                    ctx, requestCode, launch,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                builder.SetContentIntent(pendingIntent);
            }
        }

        var nm = NotificationManagerCompat.From(ctx);
        nm.Notify(GenerateNotifId(), builder.Build());
    }

    private static string ResolveChannelId(NotificationPayload payload, FCMOptions opts)
    {
        if (payload.Data.TryGetValue(opts.ChannelIdDataField, out var id)
            && !string.IsNullOrEmpty(id))
        {
            // Vérifier que le channel existe — sinon fallback
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var ctx = global::Android.App.Application.Context;
                var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
                if (nm?.GetNotificationChannel(id) is not null) return id;
            }
            else { return id; }
        }
        return opts.FallbackChannelId;
    }

    private static Type? ResolveMainActivityType()
    {
        // Cherche dans l'assembly de l'app consommatrice une Activity marquée
        // comme launcher (intent-filter MAIN/LAUNCHER). Best-effort.
        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
        if (entryAssembly is null) return null;

        foreach (var t in entryAssembly.GetTypes())
        {
            if (!typeof(Activity).IsAssignableFrom(t)) continue;
            var attrs = t.GetCustomAttributes(typeof(global::Android.App.ActivityAttribute), false)
                         as global::Android.App.ActivityAttribute[];
            if (attrs is { Length: > 0 } && attrs[0] is { } attr && attr.MainLauncher)
                return t;
        }
        return null;
    }

    private static int _counter = 1000;
    private static int GenerateNotifId() => System.Threading.Interlocked.Increment(ref _counter);

    private static int _intentCounter = 0;
}
#endif
