using Microsoft.Maui.Dispatching;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>
/// Rythme d'images calé sur le rafraîchissement de l'écran, comme dans Unity ou Godot :
/// Choreographer sur Android, CADisplayLink sur iOS. Le rappel reçoit l'horodatage de l'image
/// fourni par le système (en secondes, horloge monotone), régulier à la vsync près,
/// au lieu de l'heure de réveil d'un minuteur.
/// </summary>
internal sealed class DisplayFrameSource : IDisposable
{
    private readonly Action<double> frame;
    private bool running;
#if ANDROID
    private readonly Callback callback;
#elif IOS
    private CoreAnimation.CADisplayLink? link;
#else
    private readonly IDispatcher dispatcher;
    private IDispatcherTimer? timer;
    private readonly System.Diagnostics.Stopwatch stopwatch = new();
#endif

    public DisplayFrameSource(IDispatcher dispatcher, Action<double> frame)
    {
        this.frame = frame;
#if ANDROID
        callback = new Callback(this);
#elif !IOS
        this.dispatcher = dispatcher;
#endif
    }

    public bool IsRunning => running;

    /// <summary>Fréquence maximale demandée à l'écran (iPhone ProMotion, Android 90/120 Hz). 0 : fréquence par défaut du système.
    /// Sur Android, la demande reste plafonnée par le réglage « Affichage fluide » de l'utilisateur.</summary>
    public int MaximumFramesPerSecond { get; set; }

    public void Start()
    {
        if (running) return;
        running = true;
#if ANDROID
        RequestDisplayMode(MaximumFramesPerSecond);
        Android.Views.Choreographer.Instance!.PostFrameCallback(callback);
#elif IOS
        link = CoreAnimation.CADisplayLink.Create(OnLink);
        if (MaximumFramesPerSecond > 0 && OperatingSystem.IsIOSVersionAtLeast(15))
            link.PreferredFrameRateRange = new CoreAnimation.CAFrameRateRange { Minimum = 60, Maximum = MaximumFramesPerSecond, Preferred = MaximumFramesPerSecond };
        link.AddToRunLoop(Foundation.NSRunLoop.Main, Foundation.NSRunLoopMode.Common);
#else
        timer ??= dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1.0 / 60);
        timer.Tick -= OnTimer; timer.Tick += OnTimer;
        stopwatch.Restart(); timer.Start();
#endif
    }

    public void Stop()
    {
        if (!running) return;
        running = false;
#if ANDROID
        Android.Views.Choreographer.Instance!.RemoveFrameCallback(callback);
        RequestDisplayMode(0);
#elif IOS
        link?.Invalidate(); link?.Dispose(); link = null;
#else
        timer?.Stop();
#endif
    }

    public void Dispose()
    {
        Stop();
#if ANDROID
        callback.Dispose();
#endif
    }

#if ANDROID
    /// <summary>
    /// Demande à la fenêtre le mode d'écran le plus rapide sans dépasser <paramref name="maxFps"/> (même résolution) ;
    /// 0 rend la décision au système (hors du jeu, économie de batterie).
    /// </summary>
    private static void RequestDisplayMode(int maxFps)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var window = activity?.Window;
        var display = OperatingSystem.IsAndroidVersionAtLeast(30) ? activity?.Display : window?.WindowManager?.DefaultDisplay;
        if (window is null || display is null) return;
        int modeId = 0;
        if (maxFps > 0 && display.GetMode() is { } current && display.GetSupportedModes() is { } modes)
        {
            float best = 0;
            foreach (var mode in modes)
                if (mode.PhysicalWidth == current.PhysicalWidth && mode.PhysicalHeight == current.PhysicalHeight
                    && mode.RefreshRate <= maxFps + .5f && mode.RefreshRate > best) { best = mode.RefreshRate; modeId = mode.ModeId; }
        }
        var attributes = window.Attributes;
        if (attributes is null || attributes.PreferredDisplayModeId == modeId) return;
        attributes.PreferredDisplayModeId = modeId;
        window.Attributes = attributes;
    }

    private sealed class Callback(DisplayFrameSource owner) : Java.Lang.Object, Android.Views.Choreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos)
        {
            if (!owner.running) return;
            // Reposté avant le travail de l'image : l'image suivante reste demandée même si celle-ci est longue.
            Android.Views.Choreographer.Instance!.PostFrameCallback(this);
            owner.frame(frameTimeNanos / 1e9);
        }
    }
#elif IOS
    // TargetTimestamp : instant où l'image sera affichée, le bon repère pour placer les objets.
    private void OnLink() { if (running && link is not null) frame(link.TargetTimestamp); }
#else
    private void OnTimer(object? sender, EventArgs e) => frame(stopwatch.Elapsed.TotalSeconds);
#endif
}
