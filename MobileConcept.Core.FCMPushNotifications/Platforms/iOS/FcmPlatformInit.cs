#if IOS
using Firebase.CloudMessaging;
using Foundation;
using UIKit;
using UserNotifications;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.iOS;

/// <summary>
/// Init Firebase iOS + assignation des delegates + capture cold start.
/// À appeler par l'app dans AppDelegate.FinishedLaunching AVANT base.FinishedLaunching.
/// </summary>
public static class FcmPlatformInit
{
    private static NotificationPayload? _initialNotification;

    public static void Initialize(NSDictionary? launchOptions = null)
    {
        // Init FirebaseApp (lit GoogleService-Info.plist du bundle automatiquement)
        if (Firebase.Core.App.DefaultInstance is null)
            Firebase.Core.App.Configure();

        // Assigne les delegates
        UNUserNotificationCenter.Current.Delegate = FcmIosService.Shared;
        Messaging.SharedInstance.Delegate = FcmIosService.Shared;

        // Cold start : si l'app a été lancée par tap sur notif, capture le payload
        if (launchOptions is not null
            && launchOptions[UIApplication.LaunchOptionsRemoteNotificationKey] is NSDictionary userInfo)
        {
            _initialNotification = FcmIosService.DecodePayload(userInfo);
        }
    }

    /// <summary>
    /// À appeler depuis AppDelegate.RegisteredForRemoteNotifications(app, deviceToken).
    /// Avec FirebaseAppDelegateProxyEnabled=NO (notre config), Firebase ne swizzle plus
    /// AppDelegate, donc il faut transmettre manuellement l'APNs token à FIRMessaging
    /// pour qu'il puisse échanger contre le FCM token + recevoir les pushes.
    /// </summary>
    public static void SetApnsToken(NSData deviceToken)
    {
        Messaging.SharedInstance.ApnsToken = deviceToken;
    }

    internal static NotificationPayload? ConsumeInitialNotification()
    {
        var p = _initialNotification;
        _initialNotification = null;
        return p;
    }
}
#endif
