using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;

// Shape's derived reads (Coordinates, AllCoordinates, CenterOfMass) are caches of Cells and follow every edit.
public sealed class ShapeCacheTests
{
    [Fact]
    public void CenterOfMassFollowsACellCleared()
    {
        var shape = new Shape(2, 1);
        shape[int2(0, 0)] = true;
        shape[int2(1, 0)] = true;
        Assert.Equal(0.5f, shape.CenterOfMass.x);

        shape[int2(0, 0)] = false;

        Assert.Equal(1f, shape.CenterOfMass.x);
        Assert.Equal(0f, shape.CenterOfMass.y);
    }

    [Fact]
    public void ResizeRecountsEveryCoordinateRead()
    {
        var shape = new Shape(2, 1);
        shape[int2(0, 0)] = true;
        shape[int2(1, 0)] = true;
        Assert.Equal(2, shape.AllCoordinates.Length);
        Assert.Equal(2, shape.Coordinates.Length);
        var before = shape.CenterOfMass;

        shape.Resize(3, 2);

        Assert.Equal(6, shape.AllCoordinates.Length);
        Assert.Equal(shape.Cells.Cast<bool>().Count(c => c), shape.Coordinates.Length);
        Assert.NotEqual(before, shape.CenterOfMass);
    }

    [Fact]
    public void ResizeKeepsExistingCellsAndFillsNewOnes()
    {
        // The hull drawer's width grow relies on the fill (ships-studio-footprint r1 keeps it).
        var shape = new Shape(2, 1);
        shape[int2(0, 0)] = true;

        shape.Resize(3, 1);

        Assert.True(shape[int2(0, 0)]);
        Assert.False(shape[int2(1, 0)]);
        Assert.True(shape[int2(2, 0)]);
    }

    [Fact]
    public void SetLineRecountsEveryCoordinateRead()
    {
        var shape = new Shape(4, 1);
        shape[int2(0, 0)] = true;
        Assert.Single(shape.Coordinates);
        Assert.Equal(0f, shape.CenterOfMass.x);

        shape.SetLine(float2(0f, 0f), float2(3f, 0f));

        Assert.Equal(4, shape.Coordinates.Length);
        Assert.Equal(4, shape.AllCoordinates.Length);
        Assert.Equal(1.5f, shape.CenterOfMass.x);
    }
}
