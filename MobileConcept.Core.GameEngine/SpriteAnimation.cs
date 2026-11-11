using System.Numerics;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine;

/// <summary>Rectangle d'atlas et pivot normalisé (pieds : 0.5, 1).</summary>
public readonly record struct SpriteFrame(SKRect Source, Vector2 Pivot);

/// <summary>Animation sans dépendance MAUI. L'image appartient au cache de la scène.</summary>
public sealed class SpriteAnimation
{
    private readonly SpriteFrame[] frames;
    private readonly double fps;
    private double elapsed;
    public bool Loop { get; }
    public bool Completed => !Loop && elapsed >= frames.Length / fps;
    public SpriteFrame Current => frames[Math.Min((int)(elapsed * fps), frames.Length - 1)];
    public SpriteAnimation(IEnumerable<SpriteFrame> frames, double framesPerSecond, bool loop = true)
    {
        this.frames = frames.ToArray();
        if (this.frames.Length == 0) throw new ArgumentException("Au moins une frame est nécessaire.", nameof(frames));
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        fps = framesPerSecond;
        Loop = loop;
    }
    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        double duration = frames.Length / fps;
        elapsed = Loop ? (elapsed + dt) % duration : Math.Min(elapsed + dt, duration);
    }
    public void Restart() => elapsed = 0;
    public void Draw(SKCanvas canvas, SKImage atlas, Vector2 feet, float scale = 1)
    {
        var f = Current;
        var size = new Vector2(f.Source.Width, f.Source.Height) * scale;
        var topLeft = feet - size * f.Pivot;
        canvas.DrawImage(atlas, f.Source, SKRect.Create(topLeft.X, topLeft.Y, size.X, size.Y),
            new SKSamplingOptions(SKFilterMode.Linear));
    }
}

public static class Easing
{
    public static float SmoothStep(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    public static float OutCubic(float t) { t = 1 - Math.Clamp(t, 0, 1); return 1 - t * t * t; }
    public static float Pulse(float seconds, float frequency = 1) => .5f + .5f * MathF.Sin(seconds * frequency * MathF.Tau);
}
