using System.Numerics;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine;

public enum EffectPreset { Dust, Goal, Victory }

/// <summary>Pool borné ; abandon des émissions excédentaires, aucune allocation par particule.</summary>
public sealed class ParticleSystem : IDisposable
{
    private struct Particle
    {
        public Vector2 Position, Velocity;
        public float Age, Life, Size, Gravity;
        public SKColor Color;
    }
    private readonly Particle[] particles;
    private readonly Random random;
    private readonly SKPaint paint = new() { IsAntialias = true };
    private int count;
    public int Count => count;
    public int Capacity => particles.Length;
    public bool ReducedMotion { get; set; }
    public ParticleSystem(int capacity = 256, int? seed = null)
    {
        if (capacity < 1 || capacity > 10000) throw new ArgumentOutOfRangeException(nameof(capacity));
        particles = new Particle[capacity];
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }
    public void Emit(EffectPreset preset, Vector2 origin)
    {
        if (ReducedMotion) return;
        int amount = preset switch { EffectPreset.Dust => 10, EffectPreset.Goal => 28, _ => 90 };
        for (int i = 0; i < amount && count < Capacity; i++)
        {
            float angle = random.NextSingle() * MathF.Tau;
            float speed = preset == EffectPreset.Victory ? 90 + random.NextSingle() * 180 : 15 + random.NextSingle() * 55;
            particles[count++] = new Particle
            {
                Position = origin, Velocity = new(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - speed / 2),
                Life = preset == EffectPreset.Victory ? 1.2f + random.NextSingle() : .35f + random.NextSingle() * .4f,
                Gravity = preset == EffectPreset.Victory ? 180 : -10,
                Size = preset == EffectPreset.Dust ? 2 + random.NextSingle() * 3 : 2 + random.NextSingle() * 4,
                Color = preset switch
                {
                    EffectPreset.Dust => new SKColor(202, 177, 130),
                    EffectPreset.Goal => new SKColor(255, 218, 100),
                    _ => (i % 3) switch { 0 => new SKColor(255, 204, 92), 1 => new SKColor(116, 226, 190), _ => new SKColor(243, 130, 157) }
                }
            };
        }
    }
    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        for (int i = count - 1; i >= 0; i--)
        {
            ref var p = ref particles[i];
            p.Age += dt;
            if (p.Age >= p.Life) { particles[i] = particles[--count]; continue; }
            p.Position += p.Velocity * dt + new Vector2(0, .5f * p.Gravity * dt * dt);
            p.Velocity.Y += p.Gravity * dt;
        }
    }
    public void Draw(SKCanvas canvas)
    {
        for (int i = 0; i < count; i++)
        {
            ref var p = ref particles[i];
            paint.Color = p.Color.WithAlpha((byte)(255 * Math.Clamp(1 - p.Age / p.Life, 0, 1)));
            canvas.DrawCircle(p.Position.X, p.Position.Y, p.Size, paint);
        }
    }
    public void Clear() => count = 0;
    public void Dispose() { Clear(); paint.Dispose(); }
}
