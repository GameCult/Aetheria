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
}
