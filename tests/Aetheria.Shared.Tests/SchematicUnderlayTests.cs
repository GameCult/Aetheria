using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;

// SchematicUnderlay: the one rule that registers a mod hull's line art onto the hull Shape's cell rect.
public sealed class SchematicUnderlayTests
{
    private static ShipPolyline Line(params float[] xyz) => new ShipPolyline { Points = xyz };

    // Lines spanning Blender X [-3,5], Y [-10,2] (nose at Y -10, starboard at X -3).
    private static List<ShipPolyline> Lines() => new List<ShipPolyline>
    {
        Line(-3, -10, 0, 5, 2, 0),
        Line(1, -4, 1, 0, 0, 1),
    };

    private static Shape SixByFour()
    {
        var shape = new Shape(6, 4);
        shape[new int2(1, 1)] = true;
        shape[new int2(2, 1)] = true;
        return shape;
    }

    private static (float2 min, float2 max) Bounds(float2[][] fit)
    {
        var all = fit.SelectMany(l => l).ToArray();
        return (new float2(all.Min(p => p.x), all.Min(p => p.y)), new float2(all.Max(p => p.x), all.Max(p => p.y)));
    }

    [Fact]
    public void UnderlaySpansHullBoundsNotOccupiedCells()
    {
        var (min, max) = Bounds(SchematicUnderlay.Fit(Lines(), SixByFour()));

        Assert.Equal(0f, min.x, 4);
        Assert.Equal(0f, min.y, 4);
        Assert.Equal(6f, max.x, 4);
        Assert.Equal(4f, max.y, 4);
    }

    [Fact]
    public void UnderlayNoseUpStarboardRight()
    {
        var fit = SchematicUnderlay.Fit(Lines(), SixByFour());

        // Line 0 point 0 holds both the lowest Blender Y (nose) and the lowest Blender X (starboard).
        Assert.Equal(4f, fit[0][0].y, 4);
        Assert.Equal(6f, fit[0][0].x, 4);
        // Line 0 point 1 holds the highest of both: aft and port.
        Assert.Equal(0f, fit[0][1].y, 4);
        Assert.Equal(0f, fit[0][1].x, 4);
    }

    [Fact]
    public void UnderlayFollowsTheHullDrawersSize()
    {
        var hull = SixByFour();
        hull.Resize(8, 5);

        var (min, max) = Bounds(SchematicUnderlay.Fit(Lines(), hull));

        Assert.Equal(new float2(0, 0), new float2(min.x, min.y));
        Assert.Equal(8f, max.x, 4);
        Assert.Equal(5f, max.y, 4);
    }

    [Fact]
    public void UnderlayStretchesEachAxis()
    {
        var lines = new List<ShipPolyline> { Line(0, 0, 0, 4, 2, 0) };

        var (min, max) = Bounds(SchematicUnderlay.Fit(lines, new Shape(3, 3)));

        Assert.Equal(0f, min.x, 4);
        Assert.Equal(0f, min.y, 4);
        Assert.Equal(3f, max.x, 4);
        Assert.Equal(3f, max.y, 4);
    }

    [Fact]
    public void UnderlayDegenerateAndEmpty()
    {
        var flat = new List<ShipPolyline> { Line(2, 0, 0, 2, 5, 0) };

        var fit = SchematicUnderlay.Fit(flat, new Shape(6, 4));

        Assert.All(fit[0], p => Assert.Equal(3f, p.x, 4));
        Assert.All(fit[0], p => Assert.True(float.IsFinite(p.y)));
        var pointless = SchematicUnderlay.Fit(new List<ShipPolyline> { Line() }, new Shape(6, 4));
        Assert.Single(pointless);
        Assert.Empty(pointless[0]);
        Assert.Empty(SchematicUnderlay.Fit(null, new Shape(6, 4)));
        Assert.Empty(SchematicUnderlay.Fit(new List<ShipPolyline>(), new Shape(6, 4)));
        Assert.Empty(SchematicUnderlay.Fit(Lines(), null));
    }
}
