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
            // iOS : Messaging.SharedInstance.FcmToken est populé après que iOS
            // ait registered for remote notifications et que le delegate
            // DidReceiveRegistrationToken ait été appelé. Si null ici, l'app
            // a appelé RegisterDeviceAsync trop tôt → on attend simplement
            // que TokenRefreshed soit raise via HandleNewToken (le re-POST
            // backend se fera automatiquement à ce moment-là).
            var token = Messaging.SharedInstance.FcmToken;
            if (string.IsNullOrEmpty(token))
                return new RegisterResult(false, null, "token_pending");

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

        var body = new
        {
            token,
            platform = "ios",
            appVersion = NSBundle.MainBundle.InfoDictionary?["CFBundleShortVersionString"]?.ToString(),
            locale = NSLocale.CurrentLocale.Identifier
        };

        var resp = await http.PostAsJsonAsync(Options.RegisterTokenEndpoint, body, ct);
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
            Content = JsonContent.Create(new { token })
        };
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }
}
#endif
