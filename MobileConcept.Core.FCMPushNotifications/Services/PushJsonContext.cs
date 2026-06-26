using System.Text.Json.Serialization;

namespace MobileConcept.Core.FCMPushNotifications;

/// <summary>
/// Corps POST d'enregistrement du token push. Type CONCRET (pas anonyme) :
/// sous AOT/trimming iOS (RunAOTCompilation + PublishTrimmed), sérialiser un type
/// anonyme via System.Text.Json lève « ConstructorContainsNullParameterNames »
/// car les noms des paramètres du constructeur sont supprimés au trim.
/// </summary>
public sealed class RegisterTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? AppVersion { get; set; }
    public string? Locale { get; set; }
}

/// <summary>Corps du DELETE de désenregistrement du token push.</summary>
public sealed class UnregisterTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Contexte JSON source-generated → sérialisation SANS réflexion (compatible AOT
/// et trimming). camelCase pour préserver le contrat API existant
/// (token, platform, appVersion, locale).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RegisterTokenRequest))]
[JsonSerializable(typeof(UnregisterTokenRequest))]
public partial class PushJsonContext : JsonSerializerContext
{
}
