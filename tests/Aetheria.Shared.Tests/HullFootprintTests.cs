using System.Linq;
using CultMath;
using Xunit;

// HullFootprint: the one rule for editing a hardpoint's schematic footprint (Position + Shape together) and the
// one reading of its cells in hull space.
public sealed class HullFootprintTests
{
    private static HardpointData Hardpoint(int2 position, params int2[] cellsInShapeSpace)
    {
        var max = cellsInShapeSpace.Aggregate(new int2(0, 0), (a, c) => new int2(CultMath.math.max(a.x, c.x), CultMath.math.max(a.y, c.y)));
        var shape = new Shape(max.x + 1, max.y + 1);
        foreach (var c in cellsInShapeSpace) shape[c] = true;
        return new HardpointData { Position = position, Shape = shape };
    }

    private static int2[] Hull(HardpointData hp) => HullFootprint.Cells(hp).OrderBy(c => c.y).ThenBy(c => c.x).ToArray();

    private static HardpointData Apply(HardpointData hp, int2 hullCell)
    {
        Assert.True(HullFootprint.Toggle(hp.Shape, hp.Position, hullCell, out var shape, out var position));
        return new HardpointData { Position = position, Shape = shape };
    }

    [Fact]
    public void CellsAreTheShapeLaidAtPosition()
    {
        var hp = Hardpoint(new int2(3, 3), new int2(0, 0), new int2(1, 0));
        Assert.Equal(new[] { new int2(3, 3), new int2(4, 3) }, Hull(hp));
    }

    [Fact]
    public void FootprintGrowsTowardAnyEdgeAndKeepsItsCells()
    {
        var hp = Hardpoint(new int2(3, 3), new int2(0, 0));

        var grown = Apply(Apply(hp, new int2(2, 3)), new int2(3, 2));

        Assert.Equal(new int2(2, 2), grown.Position);
        Assert.Equal(2, grown.Shape.Width);
        Assert.Equal(2, grown.Shape.Height);
        Assert.Equal(new[] { new int2(3, 2), new int2(2, 3), new int2(3, 3) }, Hull(grown));

        var right = Apply(Apply(hp, new int2(4, 3)), new int2(3, 4));
        Assert.Equal(new int2(3, 3), right.Position);
        Assert.Equal(new[] { new int2(3, 3), new int2(4, 3), new int2(3, 4) }, Hull(right));
    }

    [Fact]
    public void FootprintTrimsBackToItsOriginal()
    {
        var hp = Hardpoint(new int2(3, 3), new int2(0, 0));
        var grown = Apply(Apply(hp, new int2(2, 3)), new int2(3, 2));

        var trimmed = Apply(Apply(grown, new int2(2, 3)), new int2(3, 2));

        Assert.Equal(hp.Position, trimmed.Position);
        Assert.Equal(1, trimmed.Shape.Width);
        Assert.Equal(1, trimmed.Shape.Height);
        Assert.Equal(Hull(hp), Hull(trimmed));
    }

    [Fact]
    public void FootprintRefusesItsLastCell()
    {
        var hp = Hardpoint(new int2(3, 3), new int2(0, 0));

        Assert.False(HullFootprint.Toggle(hp.Shape, hp.Position, new int2(3, 3), out var shape, out var position));

        Assert.Equal(hp.Position, position);
        Assert.Same(hp.Shape, shape);
        Assert.Equal(new[] { new int2(3, 3) }, Hull(hp));
    }

    [Fact]
    public void FootprintToggleLeavesItsInputUnchanged()
    {
        var hp = Hardpoint(new int2(3, 3), new int2(0, 0), new int2(1, 0));

        HullFootprint.Toggle(hp.Shape, hp.Position, new int2(2, 3), out _, out _);
        HullFootprint.Toggle(hp.Shape, hp.Position, new int2(4, 3), out _, out _);

        Assert.Equal(new int2(3, 3), hp.Position);
        Assert.Equal(2, hp.Shape.Width);
        Assert.Equal(1, hp.Shape.Height);
        Assert.Equal(new[] { new int2(3, 3), new int2(4, 3) }, Hull(hp));
    }

    [Fact]
    public void TrimmingAnInteriorBorderMovesPositionWithoutMovingCells()
    {
        var hp = Hardpoint(new int2(5, 5), new int2(0, 0), new int2(1, 0), new int2(2, 0));

        var trimmed = Apply(hp, new int2(5, 5));

        Assert.Equal(new int2(6, 5), trimmed.Position);
        Assert.Equal(new[] { new int2(6, 5), new int2(7, 5) }, Hull(trimmed));
    }
}
