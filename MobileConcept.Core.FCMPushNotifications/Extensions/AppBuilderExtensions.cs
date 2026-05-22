using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace MobileConcept.Core.FCMPushNotifications;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Enregistre le service FCM cross-platform dans le pipeline MAUI. À appeler
    /// dans MauiProgram.CreateMauiApp() :
    ///
    /// <code>
    /// .UseFCMPushNotifications(opts =>
    /// {
    ///     opts.RegisterTokenEndpoint   = new Uri("https://api.example.com/api/me/push/token");
    ///     opts.UnregisterTokenEndpoint = new Uri("https://api.example.com/api/me/push/token");
    ///     opts.AccessTokenProvider     = ct => myAuthStorage.GetAccessTokenAsync(ct);
    ///     opts.AndroidChannels = [ new("default", "Notifications", Importance.Default) ];
    ///     opts.DefaultSmallIconResourceId = Resource.Drawable.ic_notif;
    /// })
    /// </code>
    ///
    /// L'extension valide les options requises (throw au startup si misconfig),
    /// puis enregistre IPushNotificationService dans le DI container.
    /// </summary>
    public static MauiAppBuilder UseFCMPushNotifications(
        this MauiAppBuilder builder,
        Action<FCMOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var opts = new FCMOptions();
        configure(opts);
        opts.Validate();   // throw si RegisterTokenEndpoint / UnregisterTokenEndpoint / AccessTokenProvider absent

        builder.Services.AddSingleton(opts);
        builder.Services.AddHttpClient(opts.HttpClientName);

#if ANDROID
        builder.Services.AddSingleton<IPushNotificationService, Platforms.Android.PushNotificationService>();
#elif IOS
        builder.Services.AddSingleton<IPushNotificationService, Platforms.iOS.PushNotificationService>();
#endif

        return builder;
    }
}
