#if IOS
using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
using UserNotifications;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.iOS;

internal static class PermissionHelper
{
    public static bool IsGranted()
    {
        var settings = UNUserNotificationCenter.Current.GetNotificationSettingsAsync()
            .GetAwaiter().GetResult();
        return settings.AuthorizationStatus
            is UNAuthorizationStatus.Authorized
            or UNAuthorizationStatus.Provisional
            or UNAuthorizationStatus.Ephemeral;
    }

    public static async Task<PermissionResult> RequestAsync(CancellationToken ct = default)
    {
        // RequestAuthorizationAsync : popup OS uniquement à la 1ère demande.
        // Si user a déjà refusé, OS no-op et retourne (false, null).
        var (granted, _) = await UNUserNotificationCenter.Current
            .RequestAuthorizationAsync(
                UNAuthorizationOptions.Alert |
                UNAuthorizationOptions.Badge |
                UNAuthorizationOptions.Sound);

        if (granted)
        {
            // Enregistre pour les remote notifications → déclenche DidRegisterForRemoteNotifications
            await MainThread.InvokeOnMainThreadAsync(() =>
                UIApplication.SharedApplication.RegisterForRemoteNotifications());
        }

        // Distinction "jamais demandé" vs "refus permanent" via UNNotificationSettings.AuthorizationStatus
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        var permanentlyDenied = settings.AuthorizationStatus == UNAuthorizationStatus.Denied;

        return new PermissionResult(
            Granted: granted,
            ShouldShowSettings: permanentlyDenied);
    }

    public static Task OpenSystemSettingsAsync()
    {
        var url = new NSUrl(UIApplication.OpenSettingsUrlString);
        UIApplication.SharedApplication.OpenUrl(url, new NSDictionary(), null);
        return Task.CompletedTask;
    }
}
#endif
