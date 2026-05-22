namespace MobileConcept.Core.FCMPushNotifications;

/// <summary>
/// Façade cross-platform pour le SDK FCM. Implémentée par PushNotificationService
/// dans Platforms/Android et Platforms/iOS (résolu via DI selon la cible).
///
/// Le contrat est transport-only : aucune notion métier (catégorie, payload schéma).
/// L'app consommatrice décline ces concepts en lisant raw.Data dans les events.
/// </summary>
public interface IPushNotificationService
{
    // — État OS —

    /// <summary>Permission OS courante (lecture live à chaque accès).</summary>
    bool IsPermissionGranted { get; }

    /// <summary>FCM token actuel (cache du dernier OnNewToken). Null avant 1ère obtention.</summary>
    string? CurrentToken { get; }

    // — Permission —

    /// <summary>
    /// Demande la permission OS.
    /// - Jamais demandée  → popup OS s'affiche.
    /// - Déjà accordée    → no-op, retourne Granted=true.
    /// - Déjà refusée     → no-op OS, retourne Granted=false, ShouldShowSettings=true.
    /// L'app réagit au résultat (register si Granted, dialog "ouvrir réglages" si ShouldShowSettings).
    /// </summary>
    Task<PermissionResult> RequestPermissionAsync(CancellationToken ct = default);

    /// <summary>Ouvre les réglages système de l'app (Notifications) pour permettre une réactivation manuelle.</summary>
    Task OpenSystemSettingsAsync();

    // — Token lifecycle —

    /// <summary>
    /// Récupère le token FCM puis POST sur RegisterTokenEndpoint avec le Bearer
    /// fourni par AccessTokenProvider. N'appelle PAS RequestPermissionAsync.
    /// Si IsPermissionGranted == false, retourne Success=false sans appel HTTP.
    /// </summary>
    Task<RegisterResult> RegisterDeviceAsync(CancellationToken ct = default);

    /// <summary>
    /// DELETE sur UnregisterTokenEndpoint + FirebaseMessaging.DeleteToken() local.
    /// Unsubscribe également des topics actifs.
    /// </summary>
    Task UnregisterDeviceAsync(CancellationToken ct = default);

    // — Topics —

    Task SubscribeToTopicAsync(string topic, CancellationToken ct = default);
    Task UnsubscribeFromTopicAsync(string topic, CancellationToken ct = default);

    // — Événements —

    /// <summary>Émis quand FCM rote le token (rare, mais arrive). L'impl re-POST automatiquement au backend.</summary>
    event EventHandler<string>? TokenRefreshed;

    /// <summary>Notif reçue alors que l'app est au foreground. L'app décide quoi en faire (toast, banner, silence).</summary>
    event EventHandler<NotificationPayload>? NotificationReceived;

    /// <summary>Tap sur une notif système (background ou app killed) → app activée. L'app route via deep-link.</summary>
    event EventHandler<NotificationPayload>? NotificationTapped;

    // — Cold start —

    /// <summary>
    /// Si l'app a été lancée par un tap sur une notif (app killed), retourne le payload une seule fois.
    /// Appels suivants retournent null. À appeler au démarrage de l'app.
    /// </summary>
    NotificationPayload? GetInitialNotification();
}
