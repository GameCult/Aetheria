using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;

// Hardpoint gear holds the hardpoint it was seated in across save and reload: an origin two hardpoints accept is
// resolved by the item's saved Rotation (HardpointData.Rotation), not by hardpoint declaration order.
public sealed partial class RunStartTests
{
    // Two disjoint ballistic hardpoints, A (None, cell (6,5)) and B (Reversed, cell (5,5)). The edge gun's single cell is
    // (1,0): origin (5,5) puts it at (6,5) unturned and at (5,5) turned 180 degrees, so both hardpoints take it there.
    private (HullData hull, HardpointData a, HardpointData b) TwinMountHull(string name)
    {
        var a = Mount(HardpointType.Ballistic, 6, 5, rotation: ItemRotation.None);
        var b = Mount(HardpointType.Ballistic, 5, 5, rotation: ItemRotation.Reversed);
        return (ScratchHull(name, 12, a, b), a, b);
    }

    [Fact]
    public void AnOriginTwoHardpointsAcceptReloadsIntoTheHardpointItHeld()
    {
        var (hull, _, b) = TwinMountHull("Origin Twin Mounts");
        var filler = ScratchGear("Origin Filler", HardpointType.Ballistic, 1, 1);
        var edge = ScratchGear("Origin Edge Gun", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(filler), int2(6, 5)));
        Assert.True(entity.TryEquip(Instance(edge), int2(5, 5)));
        var held = entity.Equipment.Single(i => i.Data == edge);
        Assert.Same(b, held.Hardpoint);
        Assert.Equal(ItemRotation.Reversed, held.EquippableItem.Rotation);
        // Un-equip the filler as a player would, then save and reload.
        entity.TryUnequip(entity.Equipment.Single(i => i.Data == filler));

        var reloaded = EntitySerializer.Unpack(_items, null, EntitySerializer.Pack(entity));
        var after = reloaded.Equipment.Single(i => i.Data == edge);
        Assert.Equal(1, reloaded.HullData.Hardpoints.IndexOf(after.Hardpoint));
    }

    [Fact]
    public void ReloadKeepsTheFirstHardpointToo()
    {
        var (hull, _, _) = TwinMountHull("Origin Twin Mounts First");
        var edge = ScratchGear("Origin Edge Gun First", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(edge), int2(5, 5)));
        Assert.Equal(ItemRotation.None, entity.Equipment.Single(i => i.Data == edge).EquippableItem.Rotation);

        var reloaded = EntitySerializer.Unpack(_items, null, EntitySerializer.Pack(entity));
        var after = reloaded.Equipment.Single(i => i.Data == edge);
        Assert.Equal(0, reloaded.HullData.Hardpoints.IndexOf(after.Hardpoint));
    }

    [Fact]
    public void AnItemWhoseRotationNamesNoAcceptingHardpointStillSeats()
    {
        var (hull, a, _) = TwinMountHull("Origin Twin Mounts Stray");
        var edge = ScratchGear("Origin Edge Gun Stray", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        var item = Instance(edge);
        item.Rotation = ItemRotation.Clockwise;
        Assert.True(entity.ItemFits(item, int2(5, 5)));
        Assert.True(entity.TryEquip(item, int2(5, 5)));
        var held = entity.Equipment.Single(i => i.Data == edge);
        Assert.Same(a, held.Hardpoint);
        Assert.Equal(ItemRotation.None, held.EquippableItem.Rotation);
    }

    // A probe reports; only TryEquip seats. The probe used to turn the item to the hardpoint it found, so the same item
    // probed twice could resolve differently, and a failed or merely previewed placement left it turned.
    [Fact]
    public void AProbeLeavesTheItemsRotationAlone()
    {
        var (hull, a, b) = TwinMountHull("Origin Twin Mounts Probe");
        var edge = ScratchGear("Origin Edge Gun Probe", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        var item = Instance(edge);
        item.Rotation = ItemRotation.Clockwise;

        Assert.True(entity.ItemFits(item, int2(5, 5)));
        Assert.Equal(ItemRotation.Clockwise, item.Rotation);
        Assert.True(entity.ItemFits(item, int2(5, 5), out var seated));
        Assert.Equal(ItemRotation.None, seated);
        Assert.Equal(ItemRotation.Clockwise, item.Rotation);
        Assert.True(entity.TryFindSpace(item, out _));
        Assert.Equal(ItemRotation.Clockwise, item.Rotation);
        Assert.False(entity.ItemFits(item, int2(0, 0), out var unseated));
        Assert.Equal(ItemRotation.Clockwise, unseated);
        Assert.Equal(ItemRotation.Clockwise, item.Rotation);

        Assert.True(entity.TryEquip(item, int2(5, 5)));
        var held = entity.Equipment.Single(i => i.Data == edge);
        Assert.Same(a, held.Hardpoint);
        Assert.Equal(ItemRotation.None, held.EquippableItem.Rotation);

        var reversed = Instance(edge);
        reversed.Rotation = ItemRotation.Reversed;
        var fresh = BareHull(hull);
        Assert.True(fresh.ItemFits(reversed, int2(5, 5), out var first));
        Assert.True(fresh.ItemFits(reversed, int2(5, 5), out var second));
        Assert.Equal(ItemRotation.Reversed, first);
        Assert.Equal(first, second);
        Assert.Equal(b.Rotation, first);
    }
}

// The shipped catalog authors no origin that two differently turned hardpoints of one type both take. The fit rule alone
// over-approximates what Entity.HardpointAt sees (occupancy only removes candidates), so zero here is stricter than needed.
public sealed class ShippedHardpointOriginTests
{
    [Fact]
    public void TheShippedCatalogHasNoAmbiguousOrigin()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var designs = cache.GetAll<EquippableItemData>().Where(d => !(d is HullData) && d.HardpointType != HardpointType.Tool).ToArray();
        var ambiguous = new List<string>();
        var pairs = 0;
        var probes = 0;
        foreach (var hull in cache.GetAll<HullData>())
            for (var i = 0; i < hull.Hardpoints.Count; i++)
                for (var j = i + 1; j < hull.Hardpoints.Count; j++)
                {
                    var a = hull.Hardpoints[i];
                    var b = hull.Hardpoints[j];
                    if (a.Type != b.Type || a.Rotation == b.Rotation) continue;
                    pairs++;
                    foreach (var design in designs.Where(d => d.HardpointType == a.Type))
                        for (var x = -20; x < hull.Shape.Width; x++)
                            for (var y = -20; y < hull.Shape.Height; y++)
                            {
                                var origin = int2(x, y);
                                probes++;
                                if (a.TakesAt(design, origin - a.Position) && b.TakesAt(design, origin - b.Position))
                                    ambiguous.Add($"{hull.Name} {design.Name} at ({x},{y}): hardpoints {i} and {j}");
                            }
                }
        System.Console.WriteLine($"ShippedHardpointOriginTests visited {pairs} same-type different-rotation pairs");
        // A scan that visits nothing would pass; the catalog has such pairs today, so losing them must fail loudly.
        Assert.True(pairs > 0 && probes > 0, $"the scan visited {pairs} pairs and {probes} origins");
        Assert.True(ambiguous.Count == 0, $"{ambiguous.Count} ambiguous origins: " + string.Join("; ", ambiguous.Take(10)));
    }
}
