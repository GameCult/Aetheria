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
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun", Role = "articulation", ModelNodeId = "gun" });
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
        Assert.Equal(new[] { "thruster.port", "gun", "fin" }, plan.Equipment);
        Assert.Equal(new[] { "thruster.port" }, plan.Thrusters);
        Assert.Equal(new[] { "fin" }, plan.Radiators);
        var (mount, muzzles) = Assert.Single(plan.Weapons);
        Assert.Equal("gun", mount);
        Assert.Equal(new[] { "gun.z", "gun.a" }, muzzles);
    }

    [Fact]
    public void AThrusterOrRadiatorMountMustCarryItsRole()
    {
        var ship = Rig();
        ship.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Role = "articulation";
        Assert.Contains("Thruster hardpoint thruster.port needs a thruster-emitter anchor", Refusal(ship));

        ship = Rig();
        ship.Visual.Anchors.Single(anchor => anchor.Id == "fin").Role = "articulation";
        Assert.Contains("Radiator hardpoint fin needs a radiator-mesh anchor", Refusal(ship));
    }

    [Fact]
    public void AnEmitterOrMeshAnchorMustBeItsHardpointsMount()
    {
        // A thruster-emitter anchor on a weapon mount, and a radiator-mesh anchor that is no mount at all.
        var ship = Rig();
        ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Role = "thruster-emitter";
        Assert.Contains("thruster-emitter anchor gun must be a Thruster hardpoint's mount", Refusal(ship));

        ship = Rig();
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "loose", Role = "radiator-mesh", ModelNodeId = "loose" });
        Assert.Contains("radiator-mesh anchor loose must be a Radiator hardpoint's mount", Refusal(ship));
    }

    [Fact]
    public void MuzzlesBelongToWeaponMountsAndEveryWeaponHasOne()
    {
        var ship = Rig();
        ship.Visual.Anchors.Single(anchor => anchor.Id == "gun.z").ParentId = null;
        Assert.Contains("muzzle gun.z must be parented to a weapon hardpoint's mount", Refusal(ship));

        ship = Rig();
        ship.Visual.Anchors.Single(anchor => anchor.Id == "gun.z").ParentId = "thruster.port";
        Assert.Contains("muzzle gun.z must be parented to a weapon hardpoint's mount", Refusal(ship));

        ship = Rig();
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Role == "weapon-muzzle");
        Assert.Contains("weapon hardpoint gun needs at least one muzzle anchor", Refusal(ship));
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

        // Any other type refuses the same muzzles.
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun").Type = HardpointType.Tool;
        Assert.Contains("must be parented to a weapon hardpoint's mount", Refusal(ship));
    }

    [Fact]
    public void ABodyThatFailsTheSharedValidatorIsNeverPlanned()
    {
        var ship = Rig();
        ship.Hull.Prefab = "Djinni";
        Assert.Contains("names both a Unity prefab and a visual record", Refusal(ship));
    }
}
