#if ANDROID
using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>
/// Helper POST_NOTIFICATIONS (Android 13+ runtime permission).
/// Délègue à MAUI Permissions.RequestAsync pour réutiliser l'infra existante
/// (gère le Activity callback + thread UI).
/// </summary>
internal static class PermissionHelper
{
    private const string AskedFlagKey = "MobileConcept.FCM.AndroidAsked";

    public static bool IsGranted()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return true;
        var ctx = global::Android.App.Application.Context;
        return ContextCompat.CheckSelfPermission(ctx, Manifest.Permission.PostNotifications)
               == Permission.Granted;
    }

    public static async Task<PermissionResult> RequestAsync(CancellationToken ct = default)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return new(Granted: true, ShouldShowSettings: false);

        if (IsGranted()) return new(true, false);

        var alreadyAsked = Preferences.Get(AskedFlagKey, false);

        // Si déjà refusé une fois ET shouldShowRationale=false → refus permanent
        // ⇒ on ne re-trigger PAS l'OS popup, on demande à l'app d'orienter vers les settings.
        if (alreadyAsked)
        {
            var act = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (act is not null)
            {
                var shouldShow = AndroidX.Core.App.ActivityCompat
                    .ShouldShowRequestPermissionRationale(act, Manifest.Permission.PostNotifications);
                if (!shouldShow) return new(false, ShouldShowSettings: true);
            }
        }

        // Première demande (ou retry possible). Délègue à MAUI Permissions.
        var status = await Permissions.RequestAsync<NotificationsPermission>();
        Preferences.Set(AskedFlagKey, true);

        return new(
            Granted: status == PermissionStatus.Granted,
            ShouldShowSettings: status != PermissionStatus.Granted);
    }

    public static Task OpenSystemSettingsAsync()
    {
        var ctx = global::Android.App.Application.Context;
        Intent intent;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            intent = new Intent(Settings.ActionAppNotificationSettings);
            intent.PutExtra(Settings.ExtraAppPackage, ctx.PackageName);
        }
        else
        {
            intent = new Intent(Settings.ActionApplicationDetailsSettings);
            intent.SetData(global::Android.Net.Uri.Parse($"package:{ctx.PackageName}"));
        }
        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
        return Task.CompletedTask;
    }

    /// <summary>Custom MAUI Permission spec pour POST_NOTIFICATIONS (Android 13+).</summary>
    private sealed class NotificationsPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { (Manifest.Permission.PostNotifications, true) };
    }
}
#endif
