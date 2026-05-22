namespace MobileConcept.Core.FCMPushNotifications;

/// <summary>
/// Payload brut d'une notification reçue. Title/Body sont des conventions FCM
/// standards extraites de Data ; tous les autres champs métier vivent dans Data
/// et sont laissés à l'interprétation de l'app consommatrice (le package ne
/// connait aucun schéma métier — il est transport-only).
/// </summary>
public sealed record NotificationPayload(
    string? Title,
    string? Body,
    IReadOnlyDictionary<string, string> Data);

/// <summary>État de la permission OS après un appel à RequestPermissionAsync.</summary>
/// <param name="Granted">true si l'OS autorise les notifications maintenant.</param>
/// <param name="ShouldShowSettings">
/// true si l'user a déjà refusé et que la popup OS ne s'affichera plus :
/// l'app doit alors proposer d'ouvrir les réglages via OpenSystemSettingsAsync().
/// </param>
public sealed record PermissionResult(bool Granted, bool ShouldShowSettings);

/// <summary>Résultat d'un enregistrement de device auprès du backend.</summary>
public sealed record RegisterResult(bool Success, string? Token, string? Error);

/// <summary>
/// Descripteur d'un channel de notification Android. L'app consommatrice les
/// déclare via FCMOptions.AndroidChannels, le package les crée au boot.
/// </summary>
public sealed record AndroidChannelDescriptor(
    string Id,
    string Name,
    Importance Importance,
    string? Description = null);

/// <summary>Mirror cross-platform de NotificationManager.IMPORTANCE_* (Android).</summary>
public enum Importance
{
    Min,
    Low,
    Default,
    High,
    Max
}
