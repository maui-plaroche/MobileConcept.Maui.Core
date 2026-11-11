using System.Numerics;
using MobileConcept.Core.GameEngine;
using SkiaSharp;
using Xunit;

namespace MobileConcept.Core.GameEngine.Tests;

public class EngineTests
{
    [Theory][InlineData(0, 0, 0)][InlineData(4, 2, 1)][InlineData(-3, 9, 2)]
    public void ProjectionAndCameraRoundTrip(float x, float y, float z)
    {
        var projection = new IsometricProjection();
        var camera = new Camera2D { Zoom = 1.7f, Offset = new(100, 150) };
        var screen = camera.WorldToScreen(projection.Project(new(x, y, z)));
        var world = projection.Unproject(camera.ScreenToWorld(screen), z);
        Assert.InRange(Vector2.Distance(new(x, y), world), 0, .0001f);
    }
    [Fact] public void LongPauseDoesNotCauseCatchUpSpiral()
    {
        var clock = new FixedStepClock(); int updates = 0;
        Assert.Equal(6, clock.Advance(90, _ => updates++));
        Assert.Equal(6, updates); Assert.InRange(clock.Interpolation, 0, .9999f);
        clock.Reset(); Assert.Equal(0, clock.Advance(0, _ => updates++));
    }
    [Fact] public void PoolIsBoundedAndExpires()
    {
        using var particles = new ParticleSystem(32, 42);
        for (int i = 0; i < 20; i++) particles.Emit(EffectPreset.Victory, Vector2.Zero);
        Assert.Equal(32, particles.Count);
        particles.Update(3); Assert.Equal(0, particles.Count);
        particles.ReducedMotion = true; particles.Emit(EffectPreset.Victory, Vector2.Zero);
        Assert.Equal(0, particles.Count);
    }
    [Fact] public void AnimationLoopsAndClamps()
    {
        SpriteFrame[] frames = [new(new(0, 0, 10, 10), new(.5f, 1)), new(new(10, 0, 20, 10), new(.5f, 1))];
        var loop = new SpriteAnimation(frames, 10); loop.Update(.25f);
        Assert.Equal(frames[0], loop.Current);
        var once = new SpriteAnimation(frames, 10, false); once.Update(100);
        Assert.True(once.Completed); Assert.Equal(frames[1], once.Current);
    }
    [Fact] public void OversizedTexturesAndPeakMemoryAreRejected()
    {
        var limits = new TextureLimits(1024, 8 * 1024 * 1024);
        Assert.Equal(4 * 1024 * 1024, limits.Validate(1024, 1024, 0));
        Assert.Throws<InvalidDataException>(() => limits.Validate(2048, 2048, 0));
        Assert.Throws<InvalidDataException>(() => limits.Validate(1024, 1024, 1));
        Assert.Throws<InvalidDataException>(() => limits.Validate(int.MaxValue, int.MaxValue, 0));
    }
    [Fact] public void CacheLoadsReusesAndReleasesPixels()
    {
        using var input = new SKBitmap(8, 8);
        using var data = input.Encode(SKEncodedImageFormat.Png, 100);
        using var cache = new TextureCache();
        using var stream = data.AsStream();
        var first = cache.Load("tile", stream);
        Assert.Same(first, cache.Load("tile", Stream.Null));
        Assert.Equal(256, cache.ResidentBytes);
        cache.Clear(); Assert.Equal(0, cache.ResidentBytes);
    }
    [Fact] public void OversizedDecodedImageIsRejectedBeforeAllocation()
    {
        using var input = new SKBitmap(33, 1);
        using var data = input.Encode(SKEncodedImageFormat.Png, 100);
        using var cache = new TextureCache(new TextureLimits(32));
        using var stream = data.AsStream();
        Assert.Throws<InvalidDataException>(() => cache.Load("large", stream));
        Assert.Equal(0, cache.ResidentBytes);
    }
}
