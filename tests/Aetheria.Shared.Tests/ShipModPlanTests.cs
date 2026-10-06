using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;

// ShipModPlan: which anchor fills which component slot. The Unity assembler only follows this.
public sealed class ShipModPlanTests
{
    // Thruster, turret and radiator mounts on a 3x1 hull. The muzzles' Order runs against their ids on purpose.
    private static ShipParts Rig()
    {
        var ship = ShipAuthoringTests.Fixture();
        var shape = new Shape(3, 1);
        for (var x = 0; x < 3; x++) shape.Cells[x, 0] = true;
        ship.Hull.Shape = shape;
        ship.Hull.Hardpoints[0].Position = new int2(2, 0);
        ship.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Energy, Position = new int2(0, 0), Shape = new Shape(), Transform = "gun" });
        ship.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Radiator, Position = new int2(1, 0), Shape = new Shape(), Transform = "fin" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun", Role = "weapon-mount", ModelNodeId = "gun" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.a", Role = "weapon-muzzle", ModelNodeId = "gun-a", ParentId = "gun", Order = 1 });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.z", Role = "weapon-muzzle", ModelNodeId = "gun-z", ParentId = "gun", Order = 0 });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "fin", Role = "radiator-mesh", ModelNodeId = "fin" });
        return ship;
    }

    private static ShipModPlan Plan(ShipParts ship) => ShipModPlan.Build(ship.Hull, ship.Visual);
    private static string Refusal(ShipParts ship) => Assert.Throws<InvalidOperationException>(() => Plan(ship)).Message;

    [Fact]
    public void EachSlotIsFilledFromItsAnchor()
    {
        var plan = Plan(Rig());

        Assert.Equal(("map", "collider", "shield", "tractor"), (plan.MapIcon, plan.HullCollider, plan.Shield, plan.Tractor));
        Assert.Equal(new[] { "thruster.port" }, plan.Thrusters);
        Assert.Equal(new[] { "fin" }, plan.Radiators);
        var (mount, muzzles) = Assert.Single(plan.Weapons);
        Assert.Equal("gun", mount);
        Assert.Equal(new[] { "gun.z", "gun.a" }, muzzles);
    }

    // Equal Order falls back to the id, compared ordinally, whatever order the anchors were authored in.
    [Fact]
    public void MuzzlesOfEqualOrderAreTiedByIdOrdinally()
    {
        var ship = Rig();
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Role == "weapon-muzzle");
        foreach (var id in new[] { "gun.c", "gun.a", "gun.B", "gun.b" })
            ship.Visual.Anchors.Add(new ShipAnchor { Id = id, Role = "weapon-muzzle", ModelNodeId = id, ParentId = "gun", Order = 2 });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.z", Role = "weapon-muzzle", ModelNodeId = "gun.z", ParentId = "gun", Order = 1 });

        Assert.Equal(new[] { "gun.z", "gun.B", "gun.a", "gun.b", "gun.c" }, Assert.Single(Plan(ship).Weapons).Muzzles);
    }

    [Theory]
    [InlineData(HardpointType.Energy)]
    [InlineData(HardpointType.Ballistic)]
    [InlineData(HardpointType.Launcher)]
    public void EveryWeaponHardpointTypeTakesMuzzles(HardpointType type)
    {
        var ship = Rig();
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun").Type = type;
        Assert.Equal("gun", Assert.Single(Plan(ship).Weapons).Mount);
    }

    [Fact]
    public void ABodyThatFailsTheSharedValidatorIsNeverPlanned()
    {
        var ship = Rig();
        ship.Hull.Prefab = "Djinni";
        Assert.Contains("names both a Unity prefab and a visual record", Refusal(ship));
    }
}
