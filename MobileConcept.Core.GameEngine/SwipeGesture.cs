using System.Numerics;

namespace MobileConcept.Core.GameEngine;

public enum SwipeDirection { Up, Right, Down, Left }

/// <summary>Interprétation d'un balayage écran en unités logiques, indépendante de la projection du jeu.</summary>
public static class SwipeGesture
{
    public static SwipeDirection? Resolve(Vector2 delta, float minimumDistance = 24)
    {
        if (!float.IsFinite(minimumDistance) || minimumDistance <= 0) throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Y)) return null;
        if (delta.LengthSquared() < minimumDistance * minimumDistance) return null;
        if (MathF.Abs(delta.X) >= MathF.Abs(delta.Y)) return delta.X > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        return delta.Y > 0 ? SwipeDirection.Down : SwipeDirection.Up;
    }
}
