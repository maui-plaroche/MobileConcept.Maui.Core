using System.Numerics;
using Xunit;

namespace MobileConcept.Core.GameEngine.Tests;

public class SwipeTests
{
    [Theory]
    [InlineData(0, -60, SwipeDirection.Up)]
    [InlineData(80, 4, SwipeDirection.Right)]
    [InlineData(-4, 70, SwipeDirection.Down)]
    [InlineData(-70, 10, SwipeDirection.Left)]
    [InlineData(25, -40, SwipeDirection.Up)]
    public void ScreenDirectionsUseDominantAxis(float x, float y, SwipeDirection expected) =>
        Assert.Equal(expected, SwipeGesture.Resolve(new(x, y)));
    [Fact] public void TapAndInvalidCoordinatesDoNotMove()
    {
        Assert.Null(SwipeGesture.Resolve(new(5, 4)));
        Assert.Null(SwipeGesture.Resolve(new(float.NaN, 40)));
    }
}
