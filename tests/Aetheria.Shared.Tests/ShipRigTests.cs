using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CultMath;
using Xunit;

// The rig in the visual record: what the judge (Validate) refuses about joints and the guns that ride them, what Bind
// refuses when the GLB disagrees with the record, and what ShipModPlan.Rigs hands the assembler. The rig is
// presentation (ruling rig-is-presentation): arc stays the hardpoint's and traverse the weapon's, so the judge only
// asks that the chain could sweep the arc the hardpoint states.
public sealed class ShipRigTests : IDisposable
{
    private readonly TempDirectory _directory = new TempDirectory();
    public void Dispose() => _directory.Dispose();

    private static readonly float[] Up = { 0f, 1f, 0f };
    private static readonly float[] Right = { 1f, 0f, 0f };

    private static ShipJoint Joint(string id, string parent, params (float[] Axis, float Min, float Max)[] dofs) => new ShipJoint
    {
        Id = id, Parent = parent,
        Axes = dofs.SelectMany(dof => dof.Axis).ToArray(), Min = dofs.Select(dof => dof.Min).ToArray(), Max = dofs.Select(dof => dof.Max).ToArray()
    };

    private static float[] Tilted(float degrees) => new[] { MathF.Sin(degrees * MathF.PI / 180f), MathF.Cos(degrees * MathF.PI / 180f), 0f };

    // The fixture's skiff with energy guns "g0".."gN" side by side (one cell each) and the thruster after them. Each gun
    // is a weapon mount (node "gN") with one muzzle ("gN.muzzle", node "gN-muzzle"), fires 180 degrees, and rides the joint
    // its tuple names (null is a fixed mount).
    private static ShipParts Armed(params string[] jointOfGun)
    {
        var ship = ShipAuthoringTests.Fixture();
        var shape = new Shape(jointOfGun.Length + 1, 1);
        for (var x = 0; x <= jointOfGun.Length; x++) shape.Cells[x, 0] = true;
        ship.Hull.Shape = shape;
        ship.Hull.Hardpoints[0].Position = new int2(jointOfGun.Length, 0);
        for (var gun = 0; gun < jointOfGun.Length; gun++)
        {
            ship.Hull.Hardpoints.Add(new HardpointData
                { Type = HardpointType.Energy, Position = new int2(gun, 0), Shape = new Shape(), Transform = $"g{gun}", FiringArc = 180 });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = $"g{gun}", Role = "weapon-mount", ModelNodeId = $"g{gun}", Joint = jointOfGun[gun] });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = $"g{gun}.muzzle", Role = "weapon-muzzle", ModelNodeId = $"g{gun}-muzzle", ParentId = $"g{gun}" });
        }
        return ship;
    }

    private static string Refusal(ShipParts ship) => Assert.Throws<InvalidOperationException>(ship.Validate).Message;

    // One gun, on joint "j" with the given degrees of freedom, firing the given arc.
    private static ShipParts OneGun(float arc, params (float[] Axis, float Min, float Max)[] dofs)
    {
        var ship = Armed("j");
        ship.Visual.Joints.Add(Joint("j", null, dofs));
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = arc;
        return ship;
    }

    [Fact]
    public void AnArmedShipWithAJointIsValid() => OneGun(90, (Up, -45f, 45f)).Validate();

    [Fact]
    public void ReachIsTheYawRangeOfTheChainAgainstTheArcTheHardpointStates()
    {
        OneGun(90, (Up, -45f, 45f)).Validate();
        var short1 = Refusal(OneGun(100, (Up, -45f, 45f)));
        Assert.Contains("joint j chain reaches 90 degrees of yaw", short1);
        Assert.Contains("100 degree firing arc", short1);

        // Serial degrees of freedom add, whether they share a joint or sit on two.
        OneGun(120, (Up, -30f, 30f), (Up, -30f, 30f)).Validate();
        Assert.Contains("reaches 120", Refusal(OneGun(121, (Up, -30f, 30f), (Up, -30f, 30f))));
        var serial = Armed("child");
        serial.Visual.Joints.Add(Joint("base", null, (Up, -30f, 30f)));
        serial.Visual.Joints.Add(Joint("child", "base", (Up, -30f, 30f)));
        serial.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = 120;
        serial.Validate();
        serial.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = 121;
        Assert.Contains("reaches 120", Refusal(serial));

        // A degree of freedom about +X is pitch: it adds nothing, however wide.
        Assert.Contains("reaches 0 degrees", Refusal(OneGun(10, (Right, -180f, 180f))));
        Assert.Contains("reaches 90", Refusal(OneGun(91, (Up, -45f, 45f), (Right, -180f, 180f))));

        // A full turn covers every arc up to 360, and no reach is reported above it.
        OneGun(360, (Up, -180f, 180f)).Validate();
        Assert.Contains("reaches 360 degrees", Refusal(OneGun(400, (Up, -180f, 180f), (Up, -180f, 180f))));
    }

    [Fact]
    public void AnAxisWithinFiveDegreesOfUpIsYawAndOneBeyondItIsNot()
    {
        OneGun(90, (Tilted(4f), -45f, 45f)).Validate();
        Assert.Contains("reaches 0 degrees", Refusal(OneGun(90, (Tilted(6f), -45f, 45f))));
        // An axis pointing down is as much yaw as one pointing up.
        OneGun(90, (new[] { 0f, -1f, 0f }, -45f, 45f)).Validate();
    }

    [Fact]
    public void AChainStopsAtADrivenAncestorSoItsYawDoesNotCountForTheChild()
    {
        var ship = Armed("outer", "inner");
        ship.Visual.Joints.Add(Joint("outer", null, (Up, -180f, 180f)));
        ship.Visual.Joints.Add(Joint("inner", "outer", (Up, -10f, 10f)));
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 20;
        ship.Validate();
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 100;
        Assert.Contains("joint inner chain reaches 20 degrees", Refusal(ship));

        // With nothing riding the outer joint it is only a link in the inner chain, and counts.
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g0").Joint = null;
        ship.Validate();
    }

    [Fact]
    public void AGunOnAJointStatesItsArc()
    {
        var ship = OneGun(0, (Up, -180f, 180f));
        Assert.Contains("must state its firing arc above 0", Refusal(ship));

        // The same hardpoint on no joint is a fixed mount, and a fixed mount at the default arc is fine.
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g0").Joint = null;
        ship.Validate();
    }

    [Fact]
    public void MountsOnOneJointShareTheirArcAndRotation()
    {
        var ship = Armed("j", "j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        ship.Validate();

        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 90;
        Assert.Contains("must share Rotation and FiringArc", Refusal(ship));
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 180;
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").Rotation = ItemRotation.Reversed;
        Assert.Contains("must share Rotation and FiringArc", Refusal(ship));

        // Guns on different joints, or one on none, keep their own.
        ship.Visual.Joints.Add(Joint("k", null, (Up, -180f, 180f)));
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g1").Joint = "k";
        ship.Validate();
    }

    [Fact]
    public void AJointRidesOnlyAWeaponMountThatNamesARealJoint()
    {
        var ship = Armed("ghost");
        Assert.Contains("names an unknown joint", Refusal(ship));

        ship = Armed("j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g0.muzzle").Joint = "j";
        Assert.Contains("only a weapon mount rides one", Refusal(ship));
    }

    [Fact]
    public void JointShapeAcceptsItsBoundariesAndTheUnitTolerance()
    {
        OneGun(1, (Up, -180f, 180f)).Validate();
        OneGun(1, (Up, 179f, 180f)).Validate();
        OneGun(1, (new[] { 0f, 1.0009f, 0f }, 0f, 5f)).Validate();
        OneGun(1, (new[] { 0f, .9991f, 0f }, 0f, 5f)).Validate();
        Assert.Contains("must be a unit vector", Refusal(OneGun(1, (new[] { 0f, 1.0015f, 0f }, 0f, 5f))));
        Assert.Contains("must be a unit vector", Refusal(OneGun(1, (new[] { 0f, .9985f, 0f }, 0f, 5f))));
    }

    // ---- Bind: the GLB must have the rig the record states ----

    // A model node: its glTF name (unique, what Parent names), its aetheria.id (null for a node the exporter left untagged),
    // its parent's name, and whether it carries a mesh.
    private record Node(string Name, string Id, string Parent, bool Mesh = false);

    // arm > elbow > wrist > (an untagged bracket) > gun > gun-muzzle, with the fixture's other nodes at the root.
    private static readonly Node[] RigNodes =
    {
        new Node("map", "map", null), new Node("collider", "collider", null), new Node("shield", "shield", null),
        new Node("tractor", "tractor", null), new Node("thruster-port", "thruster-port", null, true),
        new Node("arm", "arm", null), new Node("elbow", "elbow", "arm"), new Node("wrist", "wrist", "elbow"),
        new Node("bracket", null, "wrist"), new Node("gun", "gun", "bracket"), new Node("gun-muzzle", "gun-muzzle", "gun")
    };

    private static string GlbJson(Node[] nodes)
    {
        var text = new StringBuilder(@"{""asset"":{""version"":""2.0""},""meshes"":[{""primitives"":[]}],""nodes"":[");
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            var kids = Enumerable.Range(0, nodes.Length).Where(child => nodes[child].Parent == node.Name).ToArray();
            text.Append(index > 0 ? "," : "").Append($@"{{""name"":""{node.Name}""");
            if (node.Mesh) text.Append(@",""mesh"":0");
            if (node.Id != null) text.Append($@",""extras"":{{""aetheria.id"":""{node.Id}""}}");
            if (kids.Length > 0) text.Append(@",""children"":[" + string.Join(",", kids) + "]");
            text.Append('}');
        }
        return text.Append("]}").ToString();
    }

    // The fixture's package with the arm rig (arm, elbow, wrist; the gun on the wrist), then the tweak, written beside a
    // GLB of the given nodes.
    private string Package(Action<ShipParts> tweak, Node[] nodes = null)
    {
        var mods = Path.Combine(_directory.Path, "Mods");
        Directory.CreateDirectory(mods);
        var directory = ShipFixture.WritePackage(mods, "mod.skiff", tweak: ship =>
        {
            ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun").FiringArc = 90;
            ship.Visual.Joints.Add(Joint("arm", null, (Up, -45f, 45f)));
            ship.Visual.Joints.Add(Joint("elbow", "arm", (Right, -45f, 45f)));
            ship.Visual.Joints.Add(Joint("wrist", "elbow", (Up, -45f, 45f)));
            ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint = "wrist";
            tweak?.Invoke(ship);
        });
        File.WriteAllBytes(Path.Combine(directory, "skiff.glb"), ShipFixture.Glb(GlbJson(nodes ?? RigNodes)));
        return Path.Combine(directory, "ship.cc");
    }

    private static string BindRefusal(string path) => Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadPackage(path)).Message;

    private static Node[] Reparent(string name, string parent) =>
        RigNodes.Select(node => node.Name == name ? node with { Parent = parent } : node).ToArray();

    [Fact]
    public void BindAcceptsARigTheModelHasEvenWithAnUntaggedNodeBetween()
    {
        var package = ShipModCatalog.ReadPackage(Package(null));
        Assert.Equal(new[] { "arm", "elbow", "wrist" }, package.Visual.Joints.Select(joint => joint.Id));
    }

    [Fact]
    public void BindRefusesAMountWhoseJointIsNotTheNearestOneAboveItsNode()
    {
        Assert.Contains("weapon mount gun must name the nearest joint above its model node",
            BindRefusal(Package(ship => ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint = "elbow")));
    }

    [Fact]
    public void BindRefusesAMountThatTheRecordAndTheModelPlaceDifferently()
    {
        // The record says the gun is fixed; the model hangs it under the wrist.
        Assert.Contains("weapon mount gun must name the nearest joint above its model node",
            BindRefusal(Package(ship => ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint = null)));
        // The record rides the wrist; the model hangs the gun at the root.
        Assert.Contains("weapon mount gun must name the nearest joint above its model node", BindRefusal(Package(null, Reparent("gun", null))));
    }

    [Fact]
    public void BindRefusesAJointThatIsNoModelNode()
    {
        var path = Package(null, RigNodes.Where(node => node.Name != "elbow").Select(node => node.Name == "wrist" ? node with { Parent = null } : node).ToArray());
        Assert.Contains("model has no node with aetheria.id=elbow for a joint", BindRefusal(path));
    }

    [Fact]
    public void BindRefusesAJointWhoseParentSkipsAJointNode()
    {
        Assert.Contains("joint wrist must name the nearest joint above its model node as its Parent",
            BindRefusal(Package(ship => ship.Visual.Joints.Single(joint => joint.Id == "wrist").Parent = "arm")));
        Assert.Contains("joint elbow must name the nearest joint above its model node as its Parent",
            BindRefusal(Package(ship => ship.Visual.Joints.Single(joint => joint.Id == "elbow").Parent = null)));
    }

    [Fact]
    public void BindRefusesAThrusterMeshUnderAJointNamingTheFollowUp() =>
        Assert.Contains("gimballed-thrusters", BindRefusal(Package(null, Reparent("thruster-port", "wrist"))));

    [Fact]
    public void NodeParentsAreTheNearestTaggedAncestorAndRootsHaveNone()
    {
        var path = Path.Combine(_directory.Path, "rig.glb");
        File.WriteAllBytes(path, ShipFixture.Glb(GlbJson(RigNodes)));
        var nodes = ShipModCatalog.ReadNodeIds(path);
        Assert.Equal(-1, nodes["arm"].Parent);
        Assert.Equal((int)nodes["arm"].Index, nodes["elbow"].Parent);
        Assert.Equal((int)nodes["wrist"].Index, nodes["gun"].Parent);
        Assert.Equal((int)nodes["gun"].Index, nodes["gun-muzzle"].Parent);
    }

    // ---- ShipModPlan.Rigs ----

    [Fact]
    public void RigsAreOnePerDrivenJointInHullOrderOfTheirFirstWeaponWithChainsThatStopAtDrivenAncestors()
    {
        // g0 on the inner joint, g1 and g2 on the outer one (g2 authored first among the anchors).
        var ship = Armed("barrel", "turret", "turret");
        ship.Visual.Joints.Add(Joint("turret", null, (Up, -180f, 180f)));
        ship.Visual.Joints.Add(Joint("barrel", "turret", (Up, -180f, 180f)));
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Id == "g1.muzzle");
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "g1.b", Role = "weapon-muzzle", ModelNodeId = "g1-b", ParentId = "g1", Order = 1 });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "g1.z", Role = "weapon-muzzle", ModelNodeId = "g1-z", ParentId = "g1", Order = 0 });
        ship.Visual.Anchors.Reverse();

        var rigs = ShipModPlan.Build(ship.Hull, ship.Visual).Rigs;

        Assert.Equal(2, rigs.Length);
        Assert.Equal("barrel", rigs[0].Joint);
        Assert.Equal(new[] { "barrel" }, rigs[0].Chain);
        Assert.Equal(("g0", "g0.muzzle"), (rigs[0].LeadMount, rigs[0].LeadMuzzle));
        Assert.Equal("turret", rigs[1].Joint);
        Assert.Equal(new[] { "turret" }, rigs[1].Chain);
        Assert.Equal(("g1", "g1.z"), (rigs[1].LeadMount, rigs[1].LeadMuzzle));
    }

    [Fact]
    public void AnUndrivenParentJointIsPartOfTheChainRootFirst()
    {
        var ship = Armed("wrist");
        ship.Visual.Joints.Add(Joint("arm", null, (Up, -90f, 90f)));
        ship.Visual.Joints.Add(Joint("elbow", "arm", (Right, -45f, 45f)));
        ship.Visual.Joints.Add(Joint("wrist", "elbow", (Up, -90f, 90f)));

        var rig = Assert.Single(ShipModPlan.Build(ship.Hull, ship.Visual).Rigs);

        Assert.Equal(new[] { "arm", "elbow", "wrist" }, rig.Chain);
    }

    [Fact]
    public void AShipWithNoGunOnAJointHasNoRigs() =>
        Assert.Empty(ShipModPlan.Build(Armed((string)null).Hull, Armed((string)null).Visual).Rigs);
}
