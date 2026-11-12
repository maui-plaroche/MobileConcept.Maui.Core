#if ANDROID
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using MobileConcept.Core.GameEngine.Maui.Vulkan;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>
/// Surface Skia dessinée par Vulkan sur Android. Dessin à la demande : <see cref="InvalidateSurface"/> demande une image,
/// rendue sur le fil de rendu Vulkan ; <see cref="PaintSurface"/> est donc appelé sur ce fil.
/// </summary>
public sealed class VulkanSurface : View
{
    public event Action<SKSurface, int, int>? PaintSurface;
    /// <summary>Vulkan indisponible ou en échec (appelé sur le fil de rendu) : l'hôte doit basculer sur OpenGL.</summary>
    public event Action<Exception>? RenderingFailed;
    internal void RaisePaint(SKSurface surface, int width, int height) => PaintSurface?.Invoke(surface, width, height);
    internal void RaiseFailed(Exception error) => RenderingFailed?.Invoke(error);
    public void InvalidateSurface() => (Handler?.PlatformView as VulkanTextureView)?.RequestRender();
}

public sealed class VulkanSurfaceHandler : ViewHandler<VulkanSurface, VulkanTextureView>
{
    public static readonly IPropertyMapper<VulkanSurface, VulkanSurfaceHandler> PropertyMapper = new PropertyMapper<VulkanSurface, VulkanSurfaceHandler>(ViewMapper);

    public VulkanSurfaceHandler() : base(PropertyMapper) { }

    protected override VulkanTextureView CreatePlatformView() => new(Context);

    protected override void ConnectHandler(VulkanTextureView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.PaintSurface += OnPaint;
        platformView.RenderingFailed += OnFailed;
    }

    protected override void DisconnectHandler(VulkanTextureView platformView)
    {
        platformView.PaintSurface -= OnPaint;
        platformView.RenderingFailed -= OnFailed;
        base.DisconnectHandler(platformView);
        platformView.Dispose();
    }

    private void OnPaint(SKSurface surface, int width, int height) => VirtualView?.RaisePaint(surface, width, height);
    private void OnFailed(Exception error) => VirtualView?.RaiseFailed(error);
}
#endif
