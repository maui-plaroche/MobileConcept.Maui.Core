#if IOS
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp.Views.iOS;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>
/// Surface Skia dessinée par Metal sur iOS (SKMetalView). Dessin à la demande : <see cref="InvalidateSurface"/>
/// demande une image, rendue au prochain rafraîchissement de l'écran sur le fil d'interface.
/// </summary>
public sealed class MetalSurface : View
{
    public event EventHandler<SKPaintMetalSurfaceEventArgs>? PaintSurface;
    internal void RaisePaint(SKPaintMetalSurfaceEventArgs e) => PaintSurface?.Invoke(this, e);
    public void InvalidateSurface() => Handler?.Invoke(nameof(InvalidateSurface));
}

public sealed class MetalSurfaceHandler : ViewHandler<MetalSurface, SKMetalView>
{
    public static readonly IPropertyMapper<MetalSurface, MetalSurfaceHandler> PropertyMapper = new PropertyMapper<MetalSurface, MetalSurfaceHandler>(ViewMapper);
    public static readonly CommandMapper<MetalSurface, MetalSurfaceHandler> Commands = new(ViewCommandMapper)
    {
        [nameof(MetalSurface.InvalidateSurface)] = (handler, _, _) => handler.PlatformView?.SetNeedsDisplay(),
    };

    public MetalSurfaceHandler() : base(PropertyMapper, Commands) { }

    protected override SKMetalView CreatePlatformView() => new()
    {
        // Pas de boucle propre à MTKView : c'est la boucle du moteur (CADisplayLink) qui demande chaque image.
        Paused = true,
        EnableSetNeedsDisplay = true,
        UserInteractionEnabled = false,
    };

    protected override void ConnectHandler(SKMetalView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.PaintSurface += OnPaint;
    }

    protected override void DisconnectHandler(SKMetalView platformView)
    {
        platformView.PaintSurface -= OnPaint;
        base.DisconnectHandler(platformView);
        platformView.Dispose();
    }

    private void OnPaint(object? sender, SKPaintMetalSurfaceEventArgs e) => VirtualView?.RaisePaint(e);
}
#endif
