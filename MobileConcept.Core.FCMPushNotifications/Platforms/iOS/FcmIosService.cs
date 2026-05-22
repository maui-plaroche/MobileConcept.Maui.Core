#if IOS
using System.Runtime.InteropServices;
using Firebase.CloudMessaging;
using Foundation;
using UIKit;
using UserNotifications;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.iOS;

/// <summary>
/// Delegate combiné UserNotificationCenter + FIRMessaging.
/// Reçoit les events FCM (token refresh) et UN (foreground / tap).
/// Singleton — set comme delegate sur UNUserNotificationCenter.Current et Messaging.SharedInstance.
/// </summary>
public sealed class FcmIosService : NSObject, IUNUserNotificationCenterDelegate, IMessagingDelegate
{
    public static FcmIosService Shared { get; } = new();
    private FcmIosService() { }

    // ----- APNs token reçu de iOS → forward à FCM -----
    public void DidRegisterForRemoteNotifications(NSData deviceToken)
    {
        Messaging.SharedInstance.ApnsToken = deviceToken;
    }

    // ----- FCM token (refresh ou première fois) -----
    [Export("messaging:didReceiveRegistrationToken:")]
    public void DidReceiveRegistrationToken(Messaging messaging, string fcmToken)
    {
        if (PushNotificationService.Instance is { } svc)
            svc.HandleNewToken(fcmToken);
    }

    // ----- App foreground : choisir l'affichage -----
    [Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
    public void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        var payload = DecodePayload(notification.Request.Content.UserInfo);

        if (PushNotificationService.Instance is { } svc)
        {
            svc.RaiseNotificationReceived(payload);
            if (svc.Options.ShowNotificationInForeground)
                completionHandler(
                    UNNotificationPresentationOptions.Banner |
                    UNNotificationPresentationOptions.Sound  |
                    UNNotificationPresentationOptions.List);
            else
                completionHandler(UNNotificationPresentationOptions.None);
        }
        else
        {
            completionHandler(UNNotificationPresentationOptions.None);
        }
    }

    // ----- Tap sur notif (background ou app killed) -----
    [Export("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:")]
    public void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        var payload = DecodePayload(response.Notification.Request.Content.UserInfo);
        if (PushNotificationService.Instance is { } svc)
            svc.RaiseNotificationTapped(payload);
        completionHandler();
    }

    /// <summary>Convertit NSDictionary (iOS userInfo) → Dictionary&lt;string,string&gt; (PayloadDecoder).</summary>
    internal static NotificationPayload DecodePayload(NSDictionary userInfo)
    {
        var data = new Dictionary<string, string>();
        foreach (var key in userInfo.Keys)
        {
            var keyStr = key.ToString();
            if (string.IsNullOrEmpty(keyStr)) continue;
            // Ignore les clés Apple internes (aps.alert, aps.badge, etc.) — uniquement data
            if (keyStr == "aps") continue;
            data[keyStr] = userInfo[key]?.ToString() ?? "";
        }
        return PayloadDecoder.Decode(data);
    }
}
#endif
