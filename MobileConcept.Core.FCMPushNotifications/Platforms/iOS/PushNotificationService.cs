#if IOS
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Firebase.CloudMessaging;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace MobileConcept.Core.FCMPushNotifications.Platforms.iOS;

/// <summary>Impl iOS de IPushNotificationService — façade Firebase.CloudMessaging.</summary>
public sealed class PushNotificationService : IPushNotificationService
{
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
        Instance = this;
    }

    public bool IsPermissionGranted => PermissionHelper.IsGranted();
    public string? CurrentToken => _currentToken ?? Messaging.SharedInstance.FcmToken;

    public Task<PermissionResult> RequestPermissionAsync(CancellationToken ct = default)
        => PermissionHelper.RequestAsync(ct);

    public Task OpenSystemSettingsAsync() => PermissionHelper.OpenSystemSettingsAsync();

    public async Task<RegisterResult> RegisterDeviceAsync(CancellationToken ct = default)
    {
        if (!IsPermissionGranted)
            return new RegisterResult(false, null, "permission_denied");

        try
        {
            // iOS : Messaging.SharedInstance.FcmToken est populé APRÈS que iOS
            // ait completed DidRegisterForRemoteNotifications + Firebase ait
            // échangé l'APNs token vs le FCM token. C'est async — quand on
            // arrive ici juste après le grant permission, ça peut prendre 2-5s.
            // On poll avec un timeout pour laisser le temps.
            var token = await WaitForFcmTokenAsync(TimeSpan.FromSeconds(8), ct);
            if (string.IsNullOrEmpty(token))
                return new RegisterResult(false, null, "token_timeout");

            _currentToken = token;
            var (ok, error) = await PostTokenAsync(token, ct);
            return ok
                ? new RegisterResult(true, token, null)
                : new RegisterResult(false, token, error ?? "backend_register_failed");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "RegisterDeviceAsync failed");
            System.Diagnostics.Debug.WriteLine($"[FCM] RegisterDeviceAsync exception : {ex}");
            return new RegisterResult(false, null, $"exception: {ex.Message}");
        }
    }

    /// <summary>Poll Messaging.SharedInstance.FcmToken jusqu'à population OU timeout.</summary>
    private static async Task<string?> WaitForFcmTokenAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var t = Messaging.SharedInstance.FcmToken;
            if (!string.IsNullOrEmpty(t)) return t;
            await Task.Delay(250, ct);
        }
        return Messaging.SharedInstance.FcmToken; // dernier check
    }

    public async Task UnregisterDeviceAsync(CancellationToken ct = default)
    {
        try
        {
            if (_currentToken is not null)
                await DeleteTokenAsync(_currentToken, ct);

            foreach (var t in _subscribedTopics.ToList())
                await UnsubscribeFromTopicAsync(t, ct);

            // Note : Messaging.SharedInstance.DeleteFcmToken requires a senderId
            // (legacy multi-sender API). On omet — le backend revokera le token
            // au prochain push via UNREGISTERED. Best-effort.
            _currentToken = null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "UnregisterDeviceAsync best-effort failure");
        }
    }

    public Task SubscribeToTopicAsync(string topic, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        Messaging.SharedInstance.Subscribe(topic, (err) =>
        {
            if (err is not null) tcs.TrySetException(new Exception(err.LocalizedDescription));
            else { _subscribedTopics.Add(topic); tcs.TrySetResult(true); }
        });
        return tcs.Task;
    }

    public Task UnsubscribeFromTopicAsync(string topic, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        Messaging.SharedInstance.Unsubscribe(topic, (err) =>
        {
            if (err is not null) tcs.TrySetException(new Exception(err.LocalizedDescription));
            else { _subscribedTopics.Remove(topic); tcs.TrySetResult(true); }
        });
        return tcs.Task;
    }

    public event EventHandler<string>? TokenRefreshed;
    public event EventHandler<NotificationPayload>? NotificationReceived;
    public event EventHandler<NotificationPayload>? NotificationTapped;

    public NotificationPayload? GetInitialNotification() => FcmPlatformInit.ConsumeInitialNotification();

    public Task ClearBadgesAndNotificationsAsync()
    {
        // Reset badge count + remove all delivered notifs.
        // Sur iOS 17+ UIApplication.ApplicationIconBadgeNumber est deprecated,
        // mais le warning n'est pas fatal et l'API fonctionne encore — c'est le
        // plus simple cross-version sans devoir dispatcher selon iOS version.
#pragma warning disable CA1422 // Validate platform compatibility
        UIKit.UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
#pragma warning restore CA1422
        UserNotifications.UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        return Task.CompletedTask;
    }

    // ----- Bridges depuis FcmIosService -----

    internal void HandleNewToken(string token)
    {
        _currentToken = token;
        TokenRefreshed?.Invoke(this, token);
        _ = Task.Run(() => PostTokenAsync(token, CancellationToken.None));
    }

    internal void RaiseNotificationReceived(NotificationPayload p)
        => NotificationReceived?.Invoke(this, p);

    internal void RaiseNotificationTapped(NotificationPayload p)
        => NotificationTapped?.Invoke(this, p);

    // ----- HTTP backend -----

    private async Task<(bool ok, string? error)> PostTokenAsync(string token, CancellationToken ct)
    {
        if (Options.AccessTokenProvider is null || Options.RegisterTokenEndpoint is null)
            return (false, "config_missing");

        var jwt = await Options.AccessTokenProvider(ct);
        if (string.IsNullOrEmpty(jwt))
        {
            _log?.LogDebug("RegisterDevice skipped — no access token (user not logged in)");
            System.Diagnostics.Debug.WriteLine("[FCM] RegisterDevice : no access token (AccessTokenProvider returned null)");
            return (false, "no_auth_token");
        }

        try
        {
            var http = _httpFactory.CreateClient(Options.HttpClientName);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var body = new RegisterTokenRequest
            {
                Token = token,
                Platform = "ios",
                AppVersion = NSBundle.MainBundle.InfoDictionary?["CFBundleShortVersionString"]?.ToString(),
                Locale = NSLocale.CurrentLocale.Identifier
            };

            var resp = await http.PostAsJsonAsync(
                Options.RegisterTokenEndpoint, body, PushJsonContext.Default.RegisterTokenRequest, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            System.Diagnostics.Debug.WriteLine(
                $"[FCM] POST {Options.RegisterTokenEndpoint} returned {(int)resp.StatusCode} {resp.StatusCode}");
            return (false, $"http_{(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FCM] POST failed : {ex.Message}");
            return (false, $"http_exception: {ex.Message}");
        }
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
