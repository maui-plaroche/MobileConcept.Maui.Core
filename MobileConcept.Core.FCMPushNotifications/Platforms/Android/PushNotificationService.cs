#if ANDROID
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Firebase.Messaging;
using Microsoft.Extensions.Logging;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.Android;

/// <summary>Impl Android de IPushNotificationService — façade FirebaseMessaging.Instance.</summary>
public sealed class PushNotificationService : IPushNotificationService
{
    /// <summary>Singleton instance — utilisé par FcmAndroidService et FcmPlatformInit pour bridger.</summary>
    internal static PushNotificationService? Instance { get; private set; }

    internal FCMOptions Options { get; }
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PushNotificationService>? _log;

    private string? _currentToken;
    private readonly HashSet<string> _subscribedTopics = new();

    public PushNotificationService(
        FCMOptions options,
        IHttpClientFactory httpFactory,
        ILogger<PushNotificationService>? log = null)
    {
        Options = options;
        _httpFactory = httpFactory;
        _log = log;

        // Création idempotente des channels au boot (depuis options.AndroidChannels)
        NotificationChannelManager.EnsureChannels(options);

        Instance = this;
    }

    public bool IsPermissionGranted => PermissionHelper.IsGranted();

    public string? CurrentToken => _currentToken;

    public Task<PermissionResult> RequestPermissionAsync(CancellationToken ct = default)
        => PermissionHelper.RequestAsync(ct);

    public Task OpenSystemSettingsAsync() => PermissionHelper.OpenSystemSettingsAsync();

    public async Task<RegisterResult> RegisterDeviceAsync(CancellationToken ct = default)
    {
        if (!IsPermissionGranted)
            return new RegisterResult(false, null, "permission_denied");

        try
        {
            // Récupère le FCM token courant (auto-créé au premier appel)
            var token = await FirebaseMessaging.Instance.GetToken().AsStringAsync();
            if (string.IsNullOrEmpty(token))
                return new RegisterResult(false, null, "token_empty");

            _currentToken = token;
            var ok = await PostTokenAsync(token, ct);
            return ok
                ? new RegisterResult(true, token, null)
                : new RegisterResult(false, token, "backend_register_failed");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "RegisterDeviceAsync failed");
            return new RegisterResult(false, null, ex.Message);
        }
    }

    public async Task UnregisterDeviceAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. DELETE backend (best-effort)
            if (_currentToken is not null)
                await DeleteTokenAsync(_currentToken, ct);

            // 2. Unsubscribe topics actifs
            foreach (var t in _subscribedTopics.ToList())
                await UnsubscribeFromTopicAsync(t, ct);

            // 3. Delete local FCM token (force regen au prochain register)
            await FirebaseMessaging.Instance.DeleteToken().AsVoidAsync();
            _currentToken = null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "UnregisterDeviceAsync best-effort failure");
        }
    }

    public async Task SubscribeToTopicAsync(string topic, CancellationToken ct = default)
    {
        await FirebaseMessaging.Instance.SubscribeToTopic(topic).AsVoidAsync();
        _subscribedTopics.Add(topic);
    }

    public async Task UnsubscribeFromTopicAsync(string topic, CancellationToken ct = default)
    {
        await FirebaseMessaging.Instance.UnsubscribeFromTopic(topic).AsVoidAsync();
        _subscribedTopics.Remove(topic);
    }

    public event EventHandler<string>? TokenRefreshed;
    public event EventHandler<NotificationPayload>? NotificationReceived;
    public event EventHandler<NotificationPayload>? NotificationTapped;

    public NotificationPayload? GetInitialNotification() => FcmPlatformInit.ConsumeInitialNotification();

    public Task ClearBadgesAndNotificationsAsync()
    {
        var ctx = global::Android.App.Application.Context;
        AndroidX.Core.App.NotificationManagerCompat.From(ctx).CancelAll();
        // Sur Android, le "notification dot" (Android 8+) suit automatiquement
        // l'existence de notifs actives. CancelAll() les supprime → dot disparaît.
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // Internal bridges depuis FcmAndroidService / FcmPlatformInit
    // ---------------------------------------------------------------

    internal void HandleNewToken(string token)
    {
        _currentToken = token;
        TokenRefreshed?.Invoke(this, token);

        // Re-POST automatique au backend (si user toujours loggué)
        _ = Task.Run(() => PostTokenAsync(token, CancellationToken.None));
    }

    internal void RaiseNotificationReceived(NotificationPayload p)
        => NotificationReceived?.Invoke(this, p);

    internal void RaiseNotificationTapped(NotificationPayload p)
        => NotificationTapped?.Invoke(this, p);

    // ---------------------------------------------------------------
    // HTTP backend
    // ---------------------------------------------------------------

    private async Task<bool> PostTokenAsync(string token, CancellationToken ct)
    {
        if (Options.AccessTokenProvider is null || Options.RegisterTokenEndpoint is null)
            return false;

        var jwt = await Options.AccessTokenProvider(ct);
        if (string.IsNullOrEmpty(jwt))
        {
            _log?.LogDebug("RegisterDevice skipped — no access token (user not logged in)");
            return false;
        }

        var http = _httpFactory.CreateClient(Options.HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var body = new RegisterTokenRequest
        {
            Token = token,
            Platform = "android",
            AppVersion = global::Android.App.Application.Context.PackageManager?
                .GetPackageInfo(global::Android.App.Application.Context.PackageName!, 0)?.VersionName,
            Locale = Java.Util.Locale.Default.ToLanguageTag()
        };

        var resp = await http.PostAsJsonAsync(
            Options.RegisterTokenEndpoint, body, PushJsonContext.Default.RegisterTokenRequest, ct);
        return resp.IsSuccessStatusCode;
    }

    private async Task<bool> DeleteTokenAsync(string token, CancellationToken ct)
    {
        if (Options.AccessTokenProvider is null || Options.UnregisterTokenEndpoint is null)
            return false;

        var jwt = await Options.AccessTokenProvider(ct);
        if (string.IsNullOrEmpty(jwt)) return false;

        var http = _httpFactory.CreateClient(Options.HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var req = new HttpRequestMessage(HttpMethod.Delete, Options.UnregisterTokenEndpoint)
        {
            Content = JsonContent.Create(
                new UnregisterTokenRequest { Token = token }, PushJsonContext.Default.UnregisterTokenRequest)
        };
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }
}
#endif
