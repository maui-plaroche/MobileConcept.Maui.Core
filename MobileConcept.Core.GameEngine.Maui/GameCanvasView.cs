using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>
/// Vue de jeu. Deux modes, même contrat de scène, même code sur Android et iOS :
/// <list type="bullet">
/// <item>processeur (par défaut) : surface raster bornée, agrandie par le compositeur natif, plus une couche pleine résolution pour <see cref="IOverlayScene"/> ;</item>
/// <item>carte graphique (<c>gpu: true</c>) : une seule surface en pleine résolution, Metal sur iOS (<see cref="MetalSurface"/>), OpenGL sur Android (<see cref="SKGLView"/>). À chaque image de l'écran, la scène avance
/// puis s'enregistre dans une <see cref="SKPicture"/> sur le fil d'interface ; la surface de la carte graphique la rejoue. Les règles et les gestes ne quittent donc
/// jamais le fil d'interface, même si Android dessine sur son fil OpenGL.</item>
/// </list>
/// La scène reste en coordonnées MAUI.
/// </summary>
public sealed class GameCanvasView : ContentView, IDisposable
{
    private readonly SKCanvasView canvas = new() { IgnorePixelScaling = false, InputTransparent = true };
    // Couche pleine résolution, transparente, pour les scènes <see cref="IOverlayScene"/> (sprites nets au-dessus d'un décor borné).
    private readonly SKCanvasView overlay = new() { IgnorePixelScaling = false, InputTransparent = true };
    private bool overlayAttached;
    // MAUI 10 décale par défaut les enfants d'un layout des marges de sécurité iOS : le canvas doit couvrir tout l'écran.
    private readonly AbsoluteLayout layout = new() { IgnoreSafeArea = true, SafeAreaEdges = SafeAreaEdges.None };
    // Images déclenchées par l'écran (Choreographer, CADisplayLink), pas par un minuteur : cadence régulière et pas de temps exact.
    private DisplayFrameSource? frames;
    private double previous = -1;
    private bool disposed;
    // Au-delà, l'application était suspendue ou l'image a calé : on n'avance pas le jeu d'un bond.
    private const double MaxFrameSeconds = .1;
    public IGameScene? Scene { get; set; }
    public bool IsRunning => frames?.IsRunning == true;
    /// <summary>Fréquence maximale demandée aux écrans à fréquence variable (iPhone ProMotion jusqu'à 120). 0 : fréquence par défaut du système.</summary>
    public int MaximumFramesPerSecond { get; set; }
    // Borne du bitmap Skia, en pixels physiques, indépendamment de la densité Android.
    private int maxRasterDimension = 1024, maxRasterPixels = 768 * 1024;
    // Modifiables à chaud : une scène coûteuse (carte) garde un raster borné, une scène légère (plateau en cache) passe en pleine résolution.
    public int MaxRasterDimension { get => maxRasterDimension; set { if (maxRasterDimension == value) return; maxRasterDimension = value; ApplyRaster(); } }
    public int MaxRasterPixels { get => maxRasterPixels; set { if (maxRasterPixels == value) return; maxRasterPixels = value; ApplyRaster(); } }
    private double sizeWidth, sizeHeight;
    public event EventHandler? FrameAdvanced;
    /// <summary>Mesure de la cadence et du temps de dessin, écrite sur la console toutes les 5 s. Désactivée par défaut, pour les mesures seulement.</summary>
    public static bool FrameDiagnostics { get; set; }
    private readonly FrameDiagnosticsLog diagnostics = new();
    // Mode carte graphique : image enregistrée la plus récente, images à libérer, image en cours de lecture par le fil OpenGL.
    // Surface de la carte graphique : Metal (iOS), Vulkan ou OpenGL (Android). Remplaçable à chaud (repli Vulkan → OpenGL).
    private View? gpuView;
    private Action? invalidateGpu;
    /// <summary>API graphique Android du mode carte graphique. Vulkan est essayé si l'appareil déclare Vulkan 1.1, avec repli automatique sur OpenGL en cas d'échec.</summary>
    public static AndroidGpuApi AndroidGpu { get; set; } = AndroidGpuApi.OpenGL;
    /// <summary>API réellement utilisée : « Metal », « Vulkan », « OpenGL » ou « processeur ».</summary>
    public string RenderingApi { get; private set; } = "processeur";
    private readonly object pictures = new();
    private SKPicture? latest, playing;
    private readonly List<SKPicture> retired = [];
    private readonly SKPictureRecorder recorder = new();
    private float latestScale = 1;
    public bool UsesGpu => gpuView is not null;

    public GameCanvasView(bool gpu = false)
    {
        if (gpu)
        {
            Content = layout;
#if IOS
            UseGpuView(CreateMetal());
#elif ANDROID
            UseGpuView(AndroidGpu == AndroidGpuApi.Vulkan && Vulkan.VulkanTextureView.IsSupported(Android.App.Application.Context) ? CreateVulkan() : CreateOpenGL());
#else
            UseGpuView(CreateOpenGL());
#endif
            Unloaded += OnUnloaded;
            diagnostics.Gpu = true;
            return;
        }
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
        if (gpuView is not null) { AbsoluteLayout.SetLayoutBounds(gpuView, new Rect(0, 0, width, height)); return; }
        double density = Math.Max(1, DeviceDisplay.Current.MainDisplayInfo.Density);
        double ratio = Math.Min(1, Math.Min(MaxRasterDimension / (Math.Max(width, height) * density),
            Math.Sqrt(MaxRasterPixels / (width * height * density * density))));
        AbsoluteLayout.SetLayoutBounds(canvas, new Rect(0, 0, width * ratio, height * ratio));
        canvas.Scale = 1 / ratio;
        AbsoluteLayout.SetLayoutBounds(overlay, new Rect(0, 0, width, height));
    }
    public void InvalidateSurface() { if (gpuView is not null) { Record(); invalidateGpu?.Invoke(); return; } canvas.InvalidateSurface(); if (overlayAttached) overlay.InvalidateSurface(); }
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
        frames ??= new DisplayFrameSource(Dispatcher, Tick);
        frames.MaximumFramesPerSecond = MaximumFramesPerSecond;
        previous = -1; frames.Start();
#if ANDROID
        // Android 15 et plus : sans catégorie haute, le système ramène une vue « normale » à 60 Hz dès qu'on ne touche plus l'écran.
        if (MaximumFramesPerSecond > 60 && OperatingSystem.IsAndroidVersionAtLeast(35) && gpuView?.Handler?.PlatformView is Android.Views.View native)
            native.RequestedFrameRate = Android.Views.View.RequestedFrameRateCategoryHigh;
#endif
    }
    public void Stop() { frames?.Stop(); previous = -1; }
    // Mesure des à-coups (FrameDiagnostics) : travail du tic précédent et passages du ramasse-miettes.
    private double lastUpdateMs, lastRecordMs, lastAdvancedMs, lastFrameInterval;
    private int lastGc0, lastGc1, lastGc2;
    private void Tick(double now)
    {
        // Pas variable, égal au temps réel entre deux images de l'écran : une image avance toujours d'exactement ce qu'elle affiche.
        double dt = previous < 0 ? 0 : Math.Clamp(now - previous, 0, MaxFrameSeconds);
        if (FrameDiagnostics && previous >= 0) TraceSpike(now - previous);
        long t0 = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
        long allocatedBefore = FrameDiagnostics ? GC.GetAllocatedBytesForCurrentThread() : 0;
        if (Scene is { } scene && dt > 0) scene.Update((float)dt);
        if (FrameDiagnostics) lastUpdateMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        if (FrameDiagnostics && previous >= 0) diagnostics.Tick(now, now - previous, dt > 0 ? 1 : 0, Scene is IOverlayScene);
        previous = now;
        if (gpuView is not null)
        {
            long t1 = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
            Record(); invalidateGpu?.Invoke();
            long t2 = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
            FrameAdvanced?.Invoke(this, EventArgs.Empty);
            if (FrameDiagnostics) { lastRecordMs = Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds; lastAdvancedMs = Stopwatch.GetElapsedTime(t2).TotalMilliseconds; diagnostics.Allocated(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore); }
            return;
        }
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
        long start = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
        Scene?.Render(target, new SKSize((float)Width, (float)Height));
        if (FrameDiagnostics) diagnostics.Painted(false, Stopwatch.GetElapsedTime(start).TotalMilliseconds, e.Info.Width, e.Info.Height);
        target.Restore();
    }
    private void PaintOverlay(object? sender, SKPaintSurfaceEventArgs e)
    {
        var target = e.Surface.Canvas;
        target.Clear(SKColors.Transparent);
        if (Width <= 0 || Height <= 0 || Scene is not IOverlayScene scene) return;
        target.Save();
        target.Scale(e.Info.Width / (float)Width, e.Info.Height / (float)Height);
        long start = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
        scene.RenderOverlay(target, new SKSize((float)Width, (float)Height));
        if (FrameDiagnostics) diagnostics.Painted(true, Stopwatch.GetElapsedTime(start).TotalMilliseconds, e.Info.Width, e.Info.Height);
        target.Restore();
    }
    private void UseGpuView(View view)
    {
        if (gpuView is not null) { layout.Children.Remove(gpuView); gpuView.Handler?.DisconnectHandler(); }
        gpuView = view;
        layout.Children.Add(view);
        if (sizeWidth > 0) AbsoluteLayout.SetLayoutBounds(view, new Rect(0, 0, sizeWidth, sizeHeight));
    }
#if IOS
    private View CreateMetal()
    {
        var view = new MetalSurface { InputTransparent = true };
        view.PaintSurface += (_, e) => PaintGpu(e.Surface, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        invalidateGpu = view.InvalidateSurface; RenderingApi = "Metal";
        return view;
    }
#endif
    private View CreateOpenGL()
    {
        var view = new SKGLView { HasRenderLoop = false, IgnorePixelScaling = false, InputTransparent = true };
        view.PaintSurface += (_, e) => PaintGpu(e.Surface, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        invalidateGpu = view.InvalidateSurface; RenderingApi = "OpenGL";
        return view;
    }
#if ANDROID
    private View CreateVulkan()
    {
        var view = new VulkanSurface { InputTransparent = true };
        view.PaintSurface += (surface, width, height) => PaintGpu(surface, width, height);
        // Échec Vulkan (pilote, surface) : bascule sur OpenGL, sur le fil d'interface.
        view.RenderingFailed += error =>
        {
            Console.WriteLine($"[GameCanvasView] Vulkan indisponible, repli sur OpenGL : {error.Message}");
            Dispatcher.Dispatch(() => { if (!disposed && ReferenceEquals(gpuView, view)) UseGpuView(CreateOpenGL()); });
        };
        invalidateGpu = view.InvalidateSurface; RenderingApi = "Vulkan";
        return view;
    }
#endif
    /// <summary>Fil d'interface : la scène s'enregistre en pixels physiques (les scènes qui lisent l'échelle du canvas gardent leurs choix de netteté).</summary>
    private void Record()
    {
        if (Scene is not { } scene || Width <= 0 || Height <= 0) return;
        float density = (float)Math.Max(1, DeviceDisplay.Current.MainDisplayInfo.Density);
        var size = new SKSize((float)Width, (float)Height);
        long start = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
        // Enregistreur réutilisé d'une image à l'autre : une allocation de moins par image (le ramasse-miettes Android coûte cher).
        var canvas = recorder.BeginRecording(SKRect.Create(size.Width * density, size.Height * density));
        canvas.Scale(density);
        scene.Render(canvas, size);
        if (scene is IOverlayScene overlayScene) overlayScene.RenderOverlay(canvas, size);
        var picture = recorder.EndRecording();
        if (FrameDiagnostics) diagnostics.Painted(true, Stopwatch.GetElapsedTime(start).TotalMilliseconds, (int)(size.Width * density), (int)(size.Height * density));
        lock (pictures)
        {
            if (latest is not null) retired.Add(latest);
            latest = picture; latestScale = density;
            // Libère les images que le fil OpenGL ne lit plus.
            for (int i = retired.Count - 1; i >= 0; i--)
                if (!ReferenceEquals(retired[i], playing)) { retired[i].Dispose(); retired.RemoveAt(i); }
        }
    }
    /// <summary>Fil de rendu (OpenGL ou Vulkan) sur Android, fil d'interface sur iOS (Metal) : rejoue la dernière image enregistrée.</summary>
    private void PaintGpu(SKSurface surface, int pixelWidth, int pixelHeight)
    {
        var target = surface.Canvas;
        SKPicture? picture; float scale;
        lock (pictures) { picture = playing = latest; scale = latestScale; }
        long start = FrameDiagnostics ? Stopwatch.GetTimestamp() : 0;
        target.Clear(SKColors.Transparent);
        if (picture is not null && Width > 0)
        {
            target.Save();
            // La surface peut différer de quelques pixels de la taille enregistrée.
            float fit = pixelWidth / (float)(Width * scale);
            if (MathF.Abs(fit - 1) > .001f) target.Scale(fit);
            target.DrawPicture(picture);
            target.Restore();
        }
        target.Flush();
        if (FrameDiagnostics) diagnostics.Painted(false, Stopwatch.GetElapsedTime(start).TotalMilliseconds, pixelWidth, pixelHeight);
        lock (pictures) playing = null;
    }
    /// <summary>Une ligne [FrameSpike] par intervalle d'image anormalement long : ce que le moteur a fait au tic précédent, le reste étant hors du moteur.</summary>
    private void TraceSpike(double interval)
    {
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        // Intervalle normal : médiane glissante grossière (premier intervalle mesuré, puis mise à jour lente).
        lastFrameInterval = lastFrameInterval <= 0 ? interval : lastFrameInterval * .98 + Math.Min(interval, lastFrameInterval * 1.5) * .02;
        if (interval > Math.Max(.025, lastFrameInterval * 2.2))
        {
            double ms = interval * 1000, engine = lastUpdateMs + lastRecordMs + lastAdvancedMs;
            double pause = GC.GetTotalPauseDuration().TotalMilliseconds;
            Console.WriteLine(FormattableString.Invariant($"[FrameSpike] {ms:0.0} ms | pause GC cumulée {pause:0} ms | update {lastUpdateMs:0.0} | enregistrement {lastRecordMs:0.0} | FrameAdvanced {lastAdvancedMs:0.0} | hors moteur {Math.Max(0, ms - engine - lastFrameInterval * 1000):0.0} | GC gen0 +{g0 - lastGc0} gen1 +{g1 - lastGc1} gen2 +{g2 - lastGc2}"));
        }
        lastGc0 = g0; lastGc1 = g1; lastGc2 = g2;
    }
    private void OnUnloaded(object? sender, EventArgs e) => Stop();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Stop();
        frames?.Dispose(); frames = null;
        canvas.PaintSurface -= Paint; overlay.PaintSurface -= PaintOverlay; Unloaded -= OnUnloaded;
        Scene = null; FrameAdvanced = null;
        canvas.Handler?.DisconnectHandler(); overlay.Handler?.DisconnectHandler(); gpuView?.Handler?.DisconnectHandler(); Handler?.DisconnectHandler();
        lock (pictures) { latest?.Dispose(); latest = null; foreach (var p in retired) p.Dispose(); retired.Clear(); }
        recorder.Dispose();
    }
}

/// <summary>API graphique du mode carte graphique sur Android.</summary>
public enum AndroidGpuApi { OpenGL, Vulkan }
