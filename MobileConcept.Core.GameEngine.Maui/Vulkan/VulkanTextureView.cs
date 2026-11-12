#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine.Maui.Vulkan;

/// <summary>
/// Vue Android dessinée par Skia sur Vulkan. TextureView (comme le SKGLView de SkiaSharp) : elle se compose comme une vue
/// ordinaire (superpositions MAUI, couche native du jeu). Tout le travail Vulkan se fait sur un fil de rendu dédié ;
/// <see cref="RequestRender"/> demande une image depuis n'importe quel fil.
/// </summary>
public sealed class VulkanTextureView : TextureView, TextureView.ISurfaceTextureListener
{
    private readonly object gate = new();
    private readonly Queue<Action<VulkanRenderer>> commands = new();
    private Thread? thread;
    private bool running, frameRequested;
    private VulkanRenderer? renderer;

    /// <summary>Sur le fil de rendu : surface Skia de l'image à dessiner et sa taille en pixels.</summary>
    public event Action<SKSurface, int, int>? PaintSurface;
    /// <summary>Sur le fil de rendu : Vulkan indisponible ou en échec ; l'hôte bascule sur un autre rendu.</summary>
    public event Action<Exception>? RenderingFailed;

    public VulkanTextureView(Context context) : base(context) => SurfaceTextureListener = this;
    private VulkanTextureView(nint handle, JniHandleOwnership transfer) : base(handle, transfer) { }

    /// <summary>Vulkan 1.1 déclaré par l'appareil (obligatoire sur les appareils 64 bits depuis Android 10).</summary>
    public static bool IsSupported(Context context) =>
        OperatingSystem.IsAndroidVersionAtLeast(24) &&
        context.PackageManager?.HasSystemFeature(Android.Content.PM.PackageManager.FeatureVulkanHardwareVersion, (int)Vk.ApiVersion11) == true;

    public void RequestRender()
    {
        lock (gate) { frameRequested = true; Monitor.Pulse(gate); }
    }

    private void Post(Action<VulkanRenderer> command)
    {
        lock (gate) { commands.Enqueue(command); Monitor.Pulse(gate); }
    }

    private void EnsureThread()
    {
        if (thread is not null) return;
        running = true;
        thread = new Thread(Loop) { Name = "MobileConcept Vulkan", IsBackground = true };
        thread.Start();
    }

    private void Loop()
    {
        try
        {
            renderer = new VulkanRenderer();
            renderer.Initialize();
            while (true)
            {
                Action<VulkanRenderer>[] pending; bool frame;
                lock (gate)
                {
                    while (running && commands.Count == 0 && !frameRequested) Monitor.Wait(gate);
                    if (!running && commands.Count == 0) break;
                    pending = commands.ToArray(); commands.Clear();
                    frame = frameRequested; frameRequested = false;
                }
                foreach (var command in pending) command(renderer);
                if (frame && renderer.HasSurface && PaintSurface is { } paint) renderer.Render(paint);
            }
        }
        catch (Exception e)
        {
            RenderingFailed?.Invoke(e);
            // Débloque un éventuel appelant en attente (destruction de la surface).
            lock (gate) { running = false; foreach (var command in commands) try { command(null!); } catch { } commands.Clear(); }
        }
        finally
        {
            renderer?.Dispose(); renderer = null;
        }
    }

    public void OnSurfaceTextureAvailable(SurfaceTexture texture, int width, int height)
    {
        EnsureThread();
        using var surface = new Surface(texture);
        nint window = Vk.ANativeWindow_fromSurface(JNIEnv.Handle, surface.Handle);
        surface.Release();
        Post(r => { if (r is null) { Vk.ANativeWindow_release(window); return; } r.AttachWindow(window, width, height); });
        RequestRender();
    }

    public void OnSurfaceTextureSizeChanged(SurfaceTexture texture, int width, int height)
    {
        Post(r => r?.Resize(width, height));
        RequestRender();
    }

    public bool OnSurfaceTextureDestroyed(SurfaceTexture texture)
    {
        // La fenêtre doit être libérée par Vulkan avant de rendre la main à Android.
        using var done = new ManualResetEventSlim();
        bool alive;
        lock (gate) alive = running;
        if (alive) { Post(r => { try { r?.DetachWindow(); } finally { done.Set(); } }); done.Wait(TimeSpan.FromSeconds(2)); }
        return true;
    }

    public void OnSurfaceTextureUpdated(SurfaceTexture texture) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (gate) { running = false; Monitor.Pulse(gate); }
            thread?.Join(TimeSpan.FromSeconds(2)); thread = null;
        }
        base.Dispose(disposing);
    }
}
#endif
