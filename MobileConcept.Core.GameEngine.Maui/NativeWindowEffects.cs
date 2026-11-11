using System.Numerics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine.Maui;

public interface IWindowEffects
{
    /// <summary>Position normalisée dans la fenêtre [0,1]. Appeler sur le thread UI.</summary>
    void Play(Window window, EffectPreset effect, Vector2 position);
    void Clear(Window window);
    bool ReducedMotion { get; set; }
}

/// <summary>Overlay non interactif attaché à la fenêtre explicitement fournie, jamais à une fenêtre globale.</summary>
public sealed class NativeWindowEffects : IWindowEffects, IDisposable
{
    private readonly Dictionary<Window, Overlay> overlays = new();
    private bool reducedMotion;
    public bool ReducedMotion
    {
        get => reducedMotion;
        set { EnsureMainThread(); reducedMotion = value; if (value) Dispose(); }
    }
    public void Play(Window window, EffectPreset effect, Vector2 position)
    {
        EnsureMainThread();
        if (ReducedMotion) return;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || position.X < 0 || position.X > 1 || position.Y < 0 || position.Y > 1)
            throw new ArgumentOutOfRangeException(nameof(position));
        Clear(window);
        var overlay = new Overlay(window, effect, position, () => Clear(window));
        overlays.Add(window, overlay);
        try { overlay.Attach(); }
        catch { Clear(window); throw; }
    }
    public void Clear(Window window)
    {
        EnsureMainThread();
        if (overlays.Remove(window, out var overlay)) overlay.Dispose();
    }
    public void Dispose()
    {
        EnsureMainThread();
        foreach (var window in overlays.Keys.ToArray()) Clear(window);
    }
    private static void EnsureMainThread()
    {
        if (!MainThread.IsMainThread) throw new InvalidOperationException("Les effets natifs doivent être pilotés sur le thread UI.");
    }

    private sealed class Overlay : IGameScene
    {
        private readonly Window window;
        private readonly EffectPreset effect;
        private readonly Vector2 position;
        private readonly Action completed;
        private readonly ParticleSystem particles = new();
        private readonly GameCanvasView view;
        private bool emitted, disposed, completionQueued;
        private float elapsed;
#if IOS
        private UIKit.UIView? native;
#elif ANDROID
        private Android.Views.View? native;
#endif
        public Overlay(Window window, EffectPreset effect, Vector2 position, Action completed)
        {
            this.window = window; this.effect = effect; this.position = position; this.completed = completed;
            view = new GameCanvasView { Scene = this, InputTransparent = true, BackgroundColor = Colors.Transparent };
            view.FrameAdvanced += OnFrame;
        }
        public void Attach()
        {
            var context = window.Handler?.MauiContext ?? throw new InvalidOperationException("La fenêtre doit être attachée.");
#if IOS
            var host = window.Handler!.PlatformView as UIKit.UIWindow
                ?? throw new InvalidOperationException("UIWindow introuvable.");
            native = view.ToPlatform(context);
            native.Frame = host.Bounds;
            native.AutoresizingMask = UIKit.UIViewAutoresizing.FlexibleWidth | UIKit.UIViewAutoresizing.FlexibleHeight;
            native.UserInteractionEnabled = false;
            native.Opaque = false;
            native.BackgroundColor = UIKit.UIColor.Clear;
            host.AddSubview(native);
#elif ANDROID
            var activity = window.Handler!.PlatformView as Android.App.Activity
                ?? throw new InvalidOperationException("Activity de la fenêtre introuvable.");
            var host = activity.Window?.DecorView as Android.Views.ViewGroup
                ?? throw new InvalidOperationException("DecorView introuvable.");
            native = view.ToPlatform(context);
            native.Clickable = false; native.Focusable = false;
            native.ImportantForAccessibility = Android.Views.ImportantForAccessibility.NoHideDescendants;
            host.AddView(native, new Android.Views.ViewGroup.LayoutParams(-1, -1));
#else
            throw new PlatformNotSupportedException();
#endif
            window.Deactivated += OnHidden;
            window.Stopped += OnHidden;
            window.Destroying += OnHidden;
            view.Start();
        }
        private void OnHidden(object? sender, EventArgs e) => completed();
        private void OnFrame(object? sender, EventArgs e)
        {
            // Délai borné même si aucun PaintSurface n'arrive (fenêtre invisible).
            if (!completionQueued && ((emitted && particles.Count == 0) || elapsed > 4))
            {
                completionQueued = true;
                view.Dispatcher.Dispatch(() => { if (!disposed) completed(); });
            }
        }
        public void Update(float dt) { elapsed += dt; particles.Update(dt); }
        public void Render(SKCanvas canvas, SKSize viewport)
        {
            if (!emitted && viewport.Width > 0 && viewport.Height > 0)
            {
                particles.Emit(effect, new(position.X * viewport.Width, position.Y * viewport.Height));
                emitted = true;
            }
            particles.Draw(canvas);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            window.Deactivated -= OnHidden; window.Stopped -= OnHidden; window.Destroying -= OnHidden;
            view.FrameAdvanced -= OnFrame;
            view.Stop();
#if IOS
            native?.RemoveFromSuperview();
#elif ANDROID
            if (native?.Parent is Android.Views.ViewGroup parent) parent.RemoveView(native);
#endif
            view.Dispose(); native = null; particles.Dispose();
        }
    }
}
