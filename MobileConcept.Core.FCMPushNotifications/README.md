# MobileConcept.Core.FCMPushNotifications

Native FCM push notifications for .NET MAUI (iOS + Android), with backend-agnostic
token registration and topic management. **Transport-only** abstraction — the
consuming app declines categories and payload conventions.

## Install

```bash
dotnet add package MobileConcept.Core.FCMPushNotifications
```

Multi-target : `net10.0-android` + `net10.0-ios`. Requires MAUI 10+.

## Quickstart

### 1. Place Firebase config files

Download from [Firebase Console](https://console.firebase.google.com) :

```
Platforms/Android/google-services.json           # or google-services.{Debug|Release}.json
Platforms/iOS/GoogleService-Info.plist           # or GoogleService-Info.{Debug|Release}.plist
```

The package's MSBuild `.targets` auto-declares them and supports per-Configuration
swap (Debug/Release) if you maintain separate dev/prod Firebase projects.

### 2. Configure in `MauiProgram.cs`

```csharp
.UseFCMPushNotifications(opts =>
{
    opts.RegisterTokenEndpoint   = new Uri("https://api.example.com/api/me/push/token");
    opts.UnregisterTokenEndpoint = new Uri("https://api.example.com/api/me/push/token");
    opts.AccessTokenProvider     = ct => myAuthStorage.GetAccessTokenAsync(ct);
    opts.DefaultSmallIconResourceId = Resource.Drawable.ic_notif;
    opts.AndroidChannels = new[]
    {
        new AndroidChannelDescriptor("orders", "Commandes", Importance.High),
        new AndroidChannelDescriptor("promos", "Promotions", Importance.Low),
    };
    opts.ChannelIdDataField = "category";        // ou "channelId" (default)
    opts.FallbackChannelId  = "default";
})
```

### 3. Init at platform entry points

**`Platforms/Android/MainActivity.cs`**

```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    FcmPlatformInit.Initialize(this);   // BEFORE base.OnCreate
    base.OnCreate(savedInstanceState);
}

protected override void OnNewIntent(Intent? intent)
{
    base.OnNewIntent(intent);
    FcmPlatformInit.HandleIntent(intent);
}
```

**`Platforms/iOS/AppDelegate.cs`**

```csharp
public override bool FinishedLaunching(UIApplication app, NSDictionary? launchOptions)
{
    FcmPlatformInit.Initialize(launchOptions);   // BEFORE base.FinishedLaunching
    return base.FinishedLaunching(app, launchOptions);
}
```

### 4. iOS Entitlements + Info.plist

`Platforms/iOS/Entitlements.plist` :

```xml
<key>aps-environment</key>
<string>development</string>   <!-- or "production" for App Store -->
```

`Platforms/iOS/Info.plist` :

```xml
<key>FirebaseAppDelegateProxyEnabled</key><false/>
<key>UIBackgroundModes</key><array><string>remote-notification</string></array>
```

Also enable **Push Notifications capability** in your Apple Developer provisioning profile.

### 5. Use `IPushNotificationService` in your app

```csharp
public class MyViewModel
{
    private readonly IPushNotificationService _push;

    public MyViewModel(IPushNotificationService push)
    {
        _push = push;

        // Subscribe to events
        push.TokenRefreshed       += (_, token) => _logger.Log($"FCM token refreshed: {token[..10]}...");
        push.NotificationReceived += (_, payload) => DisplayInAppBanner(payload);
        push.NotificationTapped   += (_, payload) => Navigate(payload.Data.GetValueOrDefault("deepLink"));

        // Cold start
        if (push.GetInitialNotification() is { } cold)
            Navigate(cold.Data.GetValueOrDefault("deepLink"));
    }

    public async Task ActivateNotificationsAsync()
    {
        var perm = await _push.RequestPermissionAsync();
        if (perm.Granted)
            await _push.RegisterDeviceAsync();
        else if (perm.ShouldShowSettings)
            await _push.OpenSystemSettingsAsync();
    }

    public async Task LogoutAsync()
    {
        await _push.UnregisterDeviceAsync();
    }
}
```

## API surface

### `IPushNotificationService`

| Member | Description |
|---|---|
| `IsPermissionGranted` | Live OS check |
| `CurrentToken` | Cached FCM token |
| `RequestPermissionAsync` | Triggers OS popup (1 shot only) |
| `OpenSystemSettingsAsync` | Deep-link to OS settings (for previously denied) |
| `RegisterDeviceAsync` | Fetch token + POST to backend |
| `UnregisterDeviceAsync` | DELETE backend + unsubscribe topics |
| `SubscribeToTopicAsync(topic)` | FCM topic subscription |
| `UnsubscribeFromTopicAsync(topic)` | |
| `event TokenRefreshed` | FCM rotates the token (rare) |
| `event NotificationReceived` | App foreground |
| `event NotificationTapped` | Tap from background / killed |
| `GetInitialNotification()` | Cold start payload (one-shot) |

### Backend conventions

The package sends to your `RegisterTokenEndpoint` :

```jsonc
POST /api/me/push/token
Authorization: Bearer <jwt>
{
  "token":      "<FCM token>",
  "platform":   "ios" | "android",
  "appVersion": "1.0.0",
  "locale":     "fr-FR"
}
```

Backend response is ignored (best-effort fire-and-update). For a full backend
implementation reference, see `MobileConcept.Server.FCMPushNotifications` (not yet
released — see [VoxMap.API/Push](https://github.com/maui-plaroche/VoxMap)).

### Payload data convention

The package only knows about `title` and `body` in `data[]`. All other keys
are pass-through. Define your app's conventions :

```jsonc
{
  "data": {
    "title":      "Hello",
    "body":       "World",
    "category":   "orders",          // → routes to Android channel "orders"
    "type":       "order_shipped",
    "deepLink":   "myapp://orders/123",
    "orderId":    "123"
  }
}
```

## FAQ

**Q. Why data-only payloads (no `notification` field) ?**
A. With `notification`, Android auto-displays even in background (you lose control)
and Android does NOT call `OnMessageReceived` for `data`-only-in-background. With
data-only, your `FcmAndroidService` is always invoked, giving you full control.

**Q. Why doesn't `RequestPermissionAsync` re-show the popup when I call it twice ?**
A. iOS hard-coded behavior — once denied, the OS popup is dead. The package returns
`PermissionResult(Granted: false, ShouldShowSettings: true)` so you can route
the user to OS settings.

**Q. AOT-compatible ?**
A. Yes, `<IsAotCompatible>true</IsAotCompatible>`. Internal types use no reflection
for serialization (manual `Dictionary<string,string>` reads).

**Q. Multi-env Firebase (dev/prod) ?**
A. Maintain `google-services.Debug.json` + `google-services.Release.json` (and
likewise for iOS). The MSBuild `.targets` copies the right one at build time
based on `$(Configuration)`.

## License

MIT
