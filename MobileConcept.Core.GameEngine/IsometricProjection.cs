using System.Numerics;

namespace MobileConcept.Core.GameEngine;

/// <summary>Repère logique en cases ; projection en unités de dessin. L'axe Z est vertical.</summary>
public sealed class IsometricProjection
{
    public float TileWidth { get; }
    public float TileHeight { get; }
    public float Elevation { get; }

    public IsometricProjection(float tileWidth = 96, float tileHeight = 48, float elevation = 48)
    {
        if (!float.IsFinite(tileWidth) || tileWidth <= 0) throw new ArgumentOutOfRangeException(nameof(tileWidth));
        if (!float.IsFinite(tileHeight) || tileHeight <= 0) throw new ArgumentOutOfRangeException(nameof(tileHeight));
        if (!float.IsFinite(elevation) || elevation <= 0) throw new ArgumentOutOfRangeException(nameof(elevation));
        (TileWidth, TileHeight, Elevation) = (tileWidth, tileHeight, elevation);
    }

    public Vector2 Project(Vector3 p) => new((p.X - p.Y) * TileWidth / 2,
        (p.X + p.Y) * TileHeight / 2 - p.Z * Elevation);

    /// <summary>Inversion sur un plan de hauteur connue, après inversion de la caméra.</summary>
    public Vector2 Unproject(Vector2 p, float z = 0)
    {
        float y = p.Y + z * Elevation;
        return new(y / TileHeight + p.X / TileWidth, y / TileHeight - p.X / TileWidth);
    }
}

/// <summary>Caméra 2D : Screen = World * Zoom + Offset.</summary>
public sealed class Camera2D
{
    private float zoom = 1;
    public Vector2 Offset { get; set; }
    public float Zoom
    {
        get => zoom;
        set => zoom = float.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
    public Vector2 WorldToScreen(Vector2 p) => p * Zoom + Offset;
    public Vector2 ScreenToWorld(Vector2 p) => (p - Offset) / Zoom;
}
