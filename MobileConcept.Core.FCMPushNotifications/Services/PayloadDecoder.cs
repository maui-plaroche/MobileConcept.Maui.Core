namespace MobileConcept.Core.FCMPushNotifications;

/// <summary>
/// Décodeur pure-logic d'un dictionnaire FCM data → NotificationPayload typé.
/// Lit les clés conventionnelles "title" / "body" (si présentes) et préserve
/// toutes les autres clés dans Data pour l'app consommatrice.
///
/// Pas d'IO, pas de plateforme — testable en isolation via net10.0 (sans MAUI).
/// </summary>
public static class PayloadDecoder
{
    public const string TitleKey = "title";
    public const string BodyKey  = "body";

    /// <summary>Décode un IDictionary&lt;string,string&gt; (typique Android RemoteMessage.Data).</summary>
    public static NotificationPayload Decode(IReadOnlyDictionary<string, string> data)
    {
        string? title = null;
        string? body  = null;
        if (data.TryGetValue(TitleKey, out var t)) title = t;
        if (data.TryGetValue(BodyKey,  out var b)) body  = b;

        // Snapshot du dict tel quel — l'app a accès à TOUTES les clés (incluant
        // title/body si elle veut les relire) via NotificationPayload.Data.
        return new NotificationPayload(title, body, data);
    }
}
