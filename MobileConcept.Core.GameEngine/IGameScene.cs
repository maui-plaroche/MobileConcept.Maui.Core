using SkiaSharp;

namespace MobileConcept.Core.GameEngine;

/// <summary>Thread UI unique. Update anime ; Render ne modifie jamais les règles métier.</summary>
public interface IGameScene : IDisposable
{
    void Update(float deltaSeconds);
    void Render(SKCanvas canvas, SKSize viewport);
}
