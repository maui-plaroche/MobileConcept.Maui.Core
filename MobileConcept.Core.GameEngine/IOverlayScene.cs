using SkiaSharp;

namespace MobileConcept.Core.GameEngine;

/// <summary>
/// Scène qui dessine en plus une couche en pleine résolution par-dessus le raster borné de <c>GameCanvasView</c> :
/// le décor coûteux reste à résolution réduite, les sprites et les éléments fins restent nets.
/// Même thread, même <see cref="IGameScene.Update"/> ; <see cref="RenderOverlay"/> est appelé après <see cref="IGameScene.Render"/>.
/// </summary>
public interface IOverlayScene : IGameScene
{
    void RenderOverlay(SKCanvas canvas, SKSize viewport);
}
