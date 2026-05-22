namespace MobileConcept.Core.FCMPushNotifications;

/// <summary>
/// Configuration runtime du package. Settée via .UseFCMPushNotifications(opts => {...})
/// dans MauiProgram.cs. Les propriétés requises (endpoint backend + access token
/// provider) sont validées au startup par .Validate().
/// </summary>
public sealed class FCMOptions
{
    /// <summary>Endpoint backend appelé en POST au RegisterDeviceAsync. Ex: https://api.example.com/api/me/push/token.</summary>
    public Uri? RegisterTokenEndpoint { get; set; }

    /// <summary>Endpoint backend appelé en DELETE au UnregisterDeviceAsync.</summary>
    public Uri? UnregisterTokenEndpoint { get; set; }

    /// <summary>
    /// Fonction qui fournit le Bearer JWT courant (ou null si user non loggué).
    /// Appelée à chaque appel HTTP. L'app pose le token via son IAuthStorage.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Channels Android à créer au boot. L'app les déclare, le package les enregistre
    /// idempotemment via NotificationManager.CreateNotificationChannel.
    /// </summary>
    public IReadOnlyList<AndroidChannelDescriptor> AndroidChannels { get; set; } = Array.Empty<AndroidChannelDescriptor>();

    /// <summary>
    /// Clé dans data[] qui contient le channelId pour router une notif Android
    /// vers le bon channel. Défaut : "channelId". L'app peut utiliser "category"
    /// si elle préfère réutiliser cette donnée métier.
    /// </summary>
    public string ChannelIdDataField { get; set; } = "channelId";

    /// <summary>Channel fallback si data[ChannelIdDataField] absent ou inconnu.</summary>
    public string FallbackChannelId { get; set; } = "default";

    /// <summary>
    /// Resource ID de l'icône monochrome (Resource.Drawable.ic_notif).
    /// REQUIS sur Android — sans, les notifs s'affichent avec un carré blanc.
    /// </summary>
    public int DefaultSmallIconResourceId { get; set; }

    /// <summary>
    /// Si true, affiche une notif système même quand l'app est au foreground.
    /// Si false (défaut), seul l'event NotificationReceived est levé — l'app
    /// gère l'affichage in-app (Toast, banner custom).
    /// </summary>
    public bool ShowNotificationInForeground { get; set; } = false;

    /// <summary>Name du HttpClient IHttpClientFactory à utiliser pour les appels backend.</summary>
    public string HttpClientName { get; set; } = "FCMPush";

    /// <summary>
    /// Vérifie que les propriétés requises sont fournies. Throw au startup
    /// (depuis AppBuilderExtensions.UseFCMPushNotifications) si misconfig.
    /// </summary>
    internal void Validate()
    {
        if (RegisterTokenEndpoint is null)
            throw new InvalidOperationException(
                "FCMOptions.RegisterTokenEndpoint is required.");
        if (UnregisterTokenEndpoint is null)
            throw new InvalidOperationException(
                "FCMOptions.UnregisterTokenEndpoint is required.");
        if (AccessTokenProvider is null)
            throw new InvalidOperationException(
                "FCMOptions.AccessTokenProvider is required.");
    }
}
