using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>Surface raster bornée, agrandie par le compositeur natif. La scène reste en coordonnées MAUI.</summary>
public sealed class GameCanvasView : ContentView, IDisposable
{
    private readonly SKCanvasView canvas = new() { IgnorePixelScaling = false, InputTransparent = true };
    // Couche pleine résolution, transparente, pour les scènes <see cref="IOverlayScene"/> (sprites nets au-dessus d'un décor borné).
    private readonly SKCanvasView overlay = new() { IgnorePixelScaling = false, InputTransparent = true };
    private bool overlayAttached;
    // MAUI 10 décale par défaut les enfants d'un layout des marges de sécurité iOS : le canvas doit couvrir tout l'écran.
    private readonly AbsoluteLayout layout = new() { IgnoreSafeArea = true, SafeAreaEdges = SafeAreaEdges.None };
    private readonly Stopwatch stopwatch = new();
    private readonly FixedStepClock clock = new();
    private IDispatcherTimer? timer;
    private double previous;
    private bool disposed;
    public IGameScene? Scene { get; set; }
    public bool IsRunning => timer?.IsRunning == true;
    // Borne du bitmap Skia, en pixels physiques, indépendamment de la densité Android.
    private int maxRasterDimension = 1024, maxRasterPixels = 768 * 1024;
    // Modifiables à chaud : une scène coûteuse (carte) garde un raster borné, une scène légère (plateau en cache) passe en pleine résolution.
    public int MaxRasterDimension { get => maxRasterDimension; set { if (maxRasterDimension == value) return; maxRasterDimension = value; ApplyRaster(); } }
    public int MaxRasterPixels { get => maxRasterPixels; set { if (maxRasterPixels == value) return; maxRasterPixels = value; ApplyRaster(); } }
    private double sizeWidth, sizeHeight;
    public event EventHandler? FrameAdvanced;

    public GameCanvasView()
    {
        canvas.AnchorX = canvas.AnchorY = 0;
        layout.Children.Add(canvas); Content = layout;
        canvas.PaintSurface += Paint; overlay.PaintSurface += PaintOverlay; Unloaded += OnUnloaded;
    }
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        sizeWidth = width; sizeHeight = height; ApplyRaster();
    }
    private void ApplyRaster()
    {
        double width = sizeWidth, height = sizeHeight;
        if (width <= 0 || height <= 0) return;
        double density = Math.Max(1, DeviceDisplay.Current.MainDisplayInfo.Density);
        double ratio = Math.Min(1, Math.Min(MaxRasterDimension / (Math.Max(width, height) * density),
            Math.Sqrt(MaxRasterPixels / (width * height * density * density))));
        AbsoluteLayout.SetLayoutBounds(canvas, new Rect(0, 0, width * ratio, height * ratio));
        canvas.Scale = 1 / ratio;
        AbsoluteLayout.SetLayoutBounds(overlay, new Rect(0, 0, width, height));
    }
    public void InvalidateSurface() { canvas.InvalidateSurface(); if (overlayAttached) overlay.InvalidateSurface(); }
    /// <summary>La couche pleine résolution n'existe (surface et texture natives) que tant que la scène en a besoin.</summary>
    private void SyncOverlay(bool wanted)
    {
        if (wanted == overlayAttached) return;
        overlayAttached = wanted;
        if (wanted) layout.Children.Add(overlay);
        else { layout.Children.Remove(overlay); overlay.Handler?.DisconnectHandler(); }
    }
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsRunning) return;
        timer ??= Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1.0 / 60);
        timer.Tick -= Tick; timer.Tick += Tick;
        clock.Reset(); previous = 0; stopwatch.Restart(); timer.Start();
    }
    public void Stop() { timer?.Stop(); stopwatch.Reset(); clock.Reset(); }
    private void Tick(object? sender, EventArgs e)
    {
        double now = stopwatch.Elapsed.TotalSeconds;
        if (Scene is { } scene) clock.Advance(now - previous, scene.Update);
        previous = now;
        bool hasOverlay = Scene is IOverlayScene;
        SyncOverlay(hasOverlay);
        canvas.InvalidateSurface(); if (hasOverlay) overlay.InvalidateSurface();
        FrameAdvanced?.Invoke(this, EventArgs.Empty);
    }
    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var target = e.Surface.Canvas;
        target.Clear(SKColors.Transparent);
        target.Save();
        // Le viewport et les gestes restent indépendants de la résolution interne.
        // Le canvas natif est déjà borné par OnSizeAllocated : utiliser ses pixels réels,
        // sans appliquer une deuxième fois la réduction liée à la densité.
        if (Width <= 0 || Height <= 0) { target.Restore(); return; }
        target.Scale(e.Info.Width / (float)Width, e.Info.Height / (float)Height);
        Scene?.Render(target, new SKSize((float)Width, (float)Height));
        target.Restore();
    }
    private void PaintOverlay(object? sender, SKPaintSurfaceEventArgs e)
    {
        var target = e.Surface.Canvas;
        target.Clear(SKColors.Transparent);
        if (Width <= 0 || Height <= 0 || Scene is not IOverlayScene scene) return;
        target.Save();
        target.Scale(e.Info.Width / (float)Width, e.Info.Height / (float)Height);
        scene.RenderOverlay(target, new SKSize((float)Width, (float)Height));
        target.Restore();
    }
    private void OnUnloaded(object? sender, EventArgs e) => Stop();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Stop();
        if (timer is not null) timer.Tick -= Tick;
        canvas.PaintSurface -= Paint; overlay.PaintSurface -= PaintOverlay; Unloaded -= OnUnloaded;
        Scene = null; FrameAdvanced = null;
        canvas.Handler?.DisconnectHandler(); overlay.Handler?.DisconnectHandler(); Handler?.DisconnectHandler();
    }
}
