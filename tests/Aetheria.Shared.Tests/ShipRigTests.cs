using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
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

    // Reach is Max - Min per degree of freedom, however the limits sit about zero: a sweep from 20 to 50 reaches 30, and one from
    // -10 to 50 reaches 60. Symmetric limits cannot tell that from |Min| + |Max| (70, 60) or twice the larger side (100, 100).
    [Fact]
    public void ReachIsMaxMinusMinForLimitsThatSitOffCentre()
    {
        OneGun(30, (Up, 20f, 50f)).Validate();
        Assert.Contains("reaches 30 degrees", Refusal(OneGun(31, (Up, 20f, 50f))));
        OneGun(60, (Up, -10f, 50f)).Validate();
        Assert.Contains("reaches 60 degrees", Refusal(OneGun(61, (Up, -10f, 50f))));
        OneGun(50, (Up, -50f, 0f)).Validate();
        Assert.Contains("reaches 50 degrees", Refusal(OneGun(51, (Up, -50f, 0f))));
        // Off-centre limits on two serial degrees of freedom add their own ranges: 30 + 60.
        OneGun(90, (Up, 20f, 50f), (Up, -10f, 50f)).Validate();
        Assert.Contains("reaches 90 degrees", Refusal(OneGun(91, (Up, 20f, 50f), (Up, -10f, 50f))));
    }

    [Fact]
    public void AnAxisWithinFiveDegreesOfUpIsYawAndOneBeyondItIsNot()
    {
        OneGun(90, (Tilted(4f), -45f, 45f)).Validate();
        Assert.Contains("reaches 0 degrees", Refusal(OneGun(90, (Tilted(6f), -45f, 45f))));
        // An axis pointing down is as much yaw as one pointing up.
        OneGun(90, (new[] { 0f, -1f, 0f }, -45f, 45f)).Validate();
    }

    // A joint has one actuator (ruling one-joint-one-actuator): a gun whose reach runs through a joint another gun drives is
    // refused, so a child's reach never borrows a parent's sweep and its parent's pose is never two solvers' outcome.
    [Fact]
    public void AGunWhoseReachRunsThroughAJointAnotherGunDrivesIsRefused()
    {
        var ship = Armed("outer", "inner");
        ship.Visual.Joints.Add(Joint("outer", null, (Up, -90f, 90f)));
        ship.Visual.Joints.Add(Joint("inner", "outer", (Up, -10f, 10f)));
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 20;
        var message = Refusal(ship);
        Assert.Contains("joint inner reaches through joint outer, which another actuator drives", message);
        Assert.Contains("a joint has one actuator", message);

        // With nothing riding the outer joint it is only a link in the inner chain, and counts.
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g0").Joint = null;
        ship.Validate();
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1").FiringArc = 201;
        Assert.Contains("joint inner chain reaches 200 degrees", Refusal(ship));
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
    public void AJointRidesOnlyAWeaponMountOrAThrusterEmitterThatNamesARealJoint()
    {
        var ship = Armed("ghost");
        Assert.Contains("names an unknown joint", Refusal(ship));

        ship = Armed("j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        ship.Visual.Anchors.Single(anchor => anchor.Id == "g0.muzzle").Joint = "j";
        Assert.Contains("only a weapon mount or a thruster emitter rides one", Refusal(ship));

        var thruster = Thrusting((null, 90f, "ghost"));
        Assert.Contains("thruster-emitter anchor t0 names an unknown joint", Refusal(thruster));
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

    // The exporter tags nodes that are not joints (a bracket, a forearm mesh). They sit between a mount and its joint and
    // between a joint and its parent joint, and the nearest JOINT above is still what Bind compares.
    [Fact]
    public void BindWalksPastTaggedNodesThatAreNotJoints()
    {
        var nodes = RigNodes.Select(node => node.Name switch
        {
            "bracket" => node with { Id = "bracket" },
            "wrist" => node with { Parent = "forearm" },
            _ => node
        }).Append(new Node("forearm", "forearm", "elbow")).ToArray();

        var package = ShipModCatalog.ReadPackage(Package(null, nodes));

        Assert.Equal(new[] { "arm", "elbow", "wrist" }, package.Visual.Joints.Select(joint => joint.Id));
        Assert.Equal("wrist", package.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint);
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

    // The thruster's emission mesh follows the same rule as a gun mount: its record names the nearest joint above its node.
    [Fact]
    public void BindAcceptsAThrusterRowOnTheJointItsMeshHangsUnder()
    {
        var package = ShipModCatalog.ReadPackage(Package(ship =>
        {
            var thruster = ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port");
            thruster.FiringArc = 90;
            // The gun leaves the wrist: the arm has one actuator, and the wrist is only a link in its chain.
            ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint = null;
            ship.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Joint = "arm";
        }, Reparent("thruster-port", "arm")));
        Assert.Equal("arm", package.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Joint);
    }

    [Fact]
    public void BindRefusesAThrusterMeshThatTheRecordAndTheModelPlaceDifferently()
    {
        // The record says the thruster is fixed; the model hangs its mesh under the wrist.
        Assert.Contains("thruster emitter thruster.port must name the nearest joint above its model node",
            BindRefusal(Package(null, Reparent("thruster-port", "wrist"))));
        // The record rides the arm; the model leaves the mesh at the root.
        Assert.Contains("thruster emitter thruster.port must name the nearest joint above its model node",
            BindRefusal(Package(ship =>
            {
                ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port").FiringArc = 90;
                ship.Visual.Anchors.Single(anchor => anchor.Id == "gun").Joint = null;
                ship.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Joint = "arm";
            })));
    }

    [Fact]
    public void BindRefusesARadiatorMeshUnderAJoint() =>
        Assert.Contains("radiator-mesh anchor thruster.port sits under a joint in the model", BindRefusal(Package(ship =>
        {
            ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port").Type = HardpointType.Radiator;
            ship.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Role = "radiator-mesh";
        }, Reparent("thruster-port", "wrist"))));

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
    public void RigsAreOnePerDrivenJointInHullOrderOfTheirFirstWeapon()
    {
        // g0 on the inner joint, g1 and g2 on the outer one (g2 authored first among the anchors).
        var ship = Armed("barrel", "turret", "turret");
        ship.Visual.Joints.Add(Joint("turret", null, (Up, -180f, 180f)));
        ship.Visual.Joints.Add(Joint("barrel", null, (Up, -180f, 180f)));
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

    // Hull order, not name order: the first gun rides "turret" and the second "barrel".
    [Fact]
    public void RigsFollowHullOrderAndNotTheOrderOfTheJointNames()
    {
        var ship = Armed("turret", "barrel");
        ship.Visual.Joints.Add(Joint("turret", null, (Up, -180f, 180f)));
        ship.Visual.Joints.Add(Joint("barrel", null, (Up, -180f, 180f)));

        var rigs = ShipModPlan.Build(ship.Hull, ship.Visual).Rigs;

        Assert.Equal(new[] { "turret", "barrel" }, rigs.Select(rig => rig.Joint));
        Assert.Equal(new[] { "g0", "g1" }, rigs.Select(rig => rig.LeadMount));
    }

    // g1 is listed before g0 in the hull though its name sorts after it.
    [Fact]
    public void TheLeadMountOfAJointIsItsFirstWeaponInHullOrderWhateverTheNames()
    {
        var ship = Armed("j", "j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        var g1 = ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g1");
        ship.Hull.Hardpoints.Remove(g1);
        ship.Hull.Hardpoints.Insert(ship.Hull.Hardpoints.FindIndex(hardpoint => hardpoint.Transform == "g0"), g1);

        var rig = Assert.Single(ShipModPlan.Build(ship.Hull, ship.Visual).Rigs);

        Assert.Equal(("g1", "g1.muzzle"), (rig.LeadMount, rig.LeadMuzzle));
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

    // A rig is a gun group's: a thruster row on a joint of its own is drawn by nobody here, and adds no entry.
    [Fact]
    public void AThrusterRowOnAJointAddsNoRigBesideTheGunRigs()
    {
        var ship = Armed("j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        ship.Visual.Joints.Add(Joint("k", null, (Up, -45f, 45f)));
        var thruster = ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        thruster.FiringArc = 90;
        ship.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "k";

        var rig = Assert.Single(ShipModPlan.Build(ship.Hull, ship.Visual).Rigs);

        Assert.Equal(("j", "g0"), (rig.Joint, rig.LeadMount));
    }

    [Fact]
    public void AShipWithNoGunOnAJointHasNoRigs() =>
        Assert.Empty(ShipModPlan.Build(Armed((string)null).Hull, Armed((string)null).Visual).Rigs);

    // ---- Thruster rows: a row's gimbal is the hardpoints' FiringArc, its members share a Row, its joint only draws it ----

    // The fixture's skiff with thrusters "t0".."tN" side by side (one cell each, the fixture's own thruster dropped). Each
    // tuple is a thruster's Row (null for none), its FiringArc, and the joint its emitter rides (null for none).
    private static ShipParts Thrusting(params (string Row, float Arc, string Joint)[] thrusters)
    {
        var ship = ShipAuthoringTests.Fixture();
        var shape = new Shape(thrusters.Length, 1);
        for (var x = 0; x < thrusters.Length; x++) shape.Cells[x, 0] = true;
        ship.Hull.Shape = shape;
        ship.Hull.Hardpoints.Clear();
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Id == "thruster.port");
        for (var index = 0; index < thrusters.Length; index++)
        {
            ship.Hull.Hardpoints.Add(new HardpointData
            {
                Type = HardpointType.Thruster, Position = new int2(index, 0), Shape = new Shape(), Transform = $"t{index}",
                Row = thrusters[index].Row, FiringArc = thrusters[index].Arc
            });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = $"t{index}", Role = "thruster-emitter", ModelNodeId = $"t{index}", Joint = thrusters[index].Joint });
        }
        return ship;
    }

    private static ShipParts OnJoint(ShipParts ship, string id, params (float[] Axis, float Min, float Max)[] dofs)
    {
        ship.Visual.Joints.Add(Joint(id, null, dofs));
        return ship;
    }

    [Fact]
    public void ARowIsOnlyForThrusters()
    {
        // The same Row that a Thruster hardpoint carries is refused on a weapon hardpoint, naming the hardpoint and its type.
        Thrusting(("r", 90f, null)).Validate();
        var ship = Armed((string)null);
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").Row = "r";
        var message = Refusal(ship);
        Assert.Contains("hardpoint g0 is a Energy hardpoint", message);
        Assert.Contains("only a Thruster hardpoint may state a row", message);
    }

    [Fact]
    public void ARowOfThrustersSharingRotationAndArcIsValidWhateverTheArc()
    {
        Thrusting(("r", 90f, null), ("r", 90f, null)).Validate();
        Thrusting(("r", 360f, null), ("r", 360f, null)).Validate();
        Thrusting(("r", .5f, null)).Validate();
        // A thruster with no Row and no arc is simply a fixed thruster.
        Thrusting((null, 0f, null), (null, 0f, null)).Validate();
    }

    [Fact]
    public void AThrusterRowMayRideAJoint()
    {
        // gimballed-thrusters: two emitters of one row (arc 90) on a joint that sweeps 90 degrees of yaw.
        OnJoint(Thrusting(("r", 90f, "j"), ("r", 90f, "j")), "j", (Up, -45f, 45f)).Validate();
        // A thruster with an arc and no Row is a row of one, and rides a joint the same way.
        OnJoint(Thrusting((null, 90f, "j")), "j", (Up, -45f, 45f)).Validate();
        // Serial degrees of freedom add, exactly as they do for a gun.
        OnJoint(Thrusting(("r", 120f, "j")), "j", (Up, -30f, 30f), (Up, -30f, 30f)).Validate();
    }

    [Fact]
    public void AFixedThrusterMayNotRideAJoint()
    {
        var ship = OnJoint(Thrusting((null, 0f, "j")), "j", (Up, -180f, 180f));
        Assert.Contains("thruster hardpoint t0 rides a joint, so it must state its firing arc above 0", Refusal(ship));
        // Fixed and on no joint, it is fine.
        ship.Visual.Anchors.Single(anchor => anchor.Id == "t0").Joint = null;
        ship.Validate();
    }

    [Fact]
    public void AJointShortOfTheRowsArcIsRefused()
    {
        var short1 = Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("r", 90f, "j")), "j", (Up, -30f, 30f)));
        Assert.Contains("joint j chain reaches 60 degrees of yaw", short1);
        Assert.Contains("90 degree gimbal arc", short1);
        // One degree over the reach is refused for a row of one as well, and the arc itself decides it.
        Assert.Contains("reaches 90 degrees", Refusal(OnJoint(Thrusting((null, 91f, "j")), "j", (Up, -45f, 45f))));
        OnJoint(Thrusting((null, 90f, "j")), "j", (Up, -45f, 45f)).Validate();
        // Pitch adds nothing to the yaw a gimbal swings through.
        Assert.Contains("reaches 0 degrees", Refusal(OnJoint(Thrusting(("r", 10f, "j")), "j", (Right, -180f, 180f))));
    }

    [Fact]
    public void ARowOnAJointIsWholeAndAlone()
    {
        // A row whose members ride two joints.
        var split = Thrusting(("r", 90f, "j"), ("r", 90f, "k"));
        OnJoint(split, "j", (Up, -45f, 45f));
        OnJoint(split, "k", (Up, -45f, 45f));
        Assert.Contains("must ride it too", Refusal(split));
        // A row with a member on no joint.
        Assert.Contains("must ride it too", Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("r", 90f, null)), "j", (Up, -45f, 45f))));
        // A joint carrying two rows, and one carrying two rows of one.
        Assert.Contains("joint j carries more than one actuator (t0 and t1)",
            Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("s", 90f, "j")), "j", (Up, -45f, 45f))));
        Assert.Contains("joint j carries more than one actuator (t0 and t1)",
            Refusal(OnJoint(Thrusting((null, 90f, "j"), (null, 90f, "j")), "j", (Up, -45f, 45f))));
        // A row and a thruster of no row on one joint.
        Assert.Contains("carries more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), (null, 90f, "j")), "j", (Up, -45f, 45f))));
        // Each row on a joint of its own is fine.
        var apart = Thrusting(("r", 90f, "j"), ("s", 90f, "k"));
        OnJoint(apart, "j", (Up, -45f, 45f));
        OnJoint(apart, "k", (Up, -45f, 45f));
        apart.Validate();
    }

    [Fact]
    public void ARadiatorStillMayNotRideAJoint()
    {
        var ship = OnJoint(Thrusting((null, 90f, "j")), "j", (Up, -180f, 180f));
        ship.Hull.Hardpoints.Single().Type = HardpointType.Radiator;
        ship.Visual.Anchors.Single(anchor => anchor.Id == "t0").Role = "radiator-mesh";
        Assert.Contains("radiator-mesh anchor t0 may not ride a joint", Refusal(ship));
    }

    // A joint has one actuator (ruling one-joint-one-actuator): a gun group or one thruster row, never both, never two rows.
    [Fact]
    public void AGunAndAThrusterRowMayNotShareAJoint()
    {
        var ship = Armed("j");
        ship.Visual.Joints.Add(Joint("j", null, (Up, -45f, 45f)));
        ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = 90;
        var thruster = ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        thruster.FiringArc = 90;
        ship.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "j";
        var message = Refusal(ship);
        Assert.Contains("joint j carries more than one actuator", message);
        Assert.Contains("a joint has one actuator", message);

        // The same ship with the thruster on a joint of its own is the acceptance control.
        ship.Visual.Joints.Add(Joint("k", null, (Up, -45f, 45f)));
        ship.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "k";
        ship.Validate();
        // A thruster of a named row is refused on a gun's joint alike.
        thruster.Row = "r";
        ship.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "j";
        Assert.Contains("a joint has one actuator", Refusal(ship));

        // The gun may be any of several: the second gun is the one sharing the thruster's joint.
        var second = Armed("k", "j");
        second.Visual.Joints.Add(Joint("k", null, (Up, -90f, 90f)));
        second.Visual.Joints.Add(Joint("j", null, (Up, -90f, 90f)));
        var lone = second.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        lone.FiringArc = 90;
        second.Visual.Anchors.Single(anchor => anchor.Id == lone.Transform).Joint = "k";
        Assert.Contains("joint k carries more than one actuator", Refusal(second));
        second.Visual.Anchors.Single(anchor => anchor.Id == lone.Transform).Joint = "j";
        Assert.Contains("joint j carries more than one actuator", Refusal(second));
    }

    [Fact]
    public void TwoRowsMayNotShareAJointWhateverTheirSizesOrOrder()
    {
        // A row of three and a row of one, the second row's member last; a row of two and one; and a row of four against one.
        Assert.Contains("carries more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("r", 90f, "j"), ("r", 90f, "j"), ("s", 90f, "j")), "j", (Up, -45f, 45f))));
        Assert.Contains("carries more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("r", 90f, "j"), ("s", 90f, "j")), "j", (Up, -45f, 45f))));
        // Rows are compared exactly: "r" and "R" are two rows, and so is a blank name against a letter.
        Assert.Contains("carries more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("R", 90f, "j")), "j", (Up, -45f, 45f))));
        Assert.Contains("carries more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), (" ", 90f, "j")), "j", (Up, -45f, 45f))));
        var apart = Thrusting(("r", 90f, "j"), ("R", 90f, "k"));
        OnJoint(apart, "j", (Up, -45f, 45f));
        OnJoint(apart, "k", (Up, -45f, 45f));
        apart.Validate();
    }

    [Fact]
    public void ARowOfAnySizeRidesItsJointWholeOrIsRefused()
    {
        foreach (var size in new[] { 3, 4, 5 })
        for (var left = 0; left < size; left++)
        {
            // Every member on j except one, which is on no joint or on another; the one may stand anywhere in the row.
            foreach (var elsewhere in new string[] { null, "k" })
            {
                var members = Enumerable.Range(0, size).Select(index => ("r", 90f, index == left ? elsewhere : "j")).ToArray();
                var ship = OnJoint(Thrusting(members), "j", (Up, -45f, 45f));
                OnJoint(ship, "k", (Up, -45f, 45f));
                Assert.Contains("must ride it too", Refusal(ship));
            }
        }
        // The same rows, every member on j, are valid.
        foreach (var size in new[] { 3, 4, 5 })
            OnJoint(Thrusting(Enumerable.Range(0, size).Select(_ => ("r", 90f, "j")).ToArray()), "j", (Up, -45f, 45f)).Validate();
    }

    [Fact]
    public void ARowSharesRotationAndTheExactArcAcrossAnyNumberOfMembers()
    {
        // The offender may be the second, third or fourth member, and may differ by a hair.
        foreach (var arc in new[] { 89.5f, 89.99f, 60f, 91f, 120f, 270f })
        for (var offender = 1; offender < 4; offender++)
        {
            var members = Enumerable.Range(0, 4).Select(index => ("r", index == offender ? arc : 90f, (string)null)).ToArray();
            Assert.Contains("must share Rotation and FiringArc", Refusal(Thrusting(members)));
        }
        var rotated = Thrusting(("r", 90f, null), ("r", 90f, null), ("r", 90f, null));
        rotated.Hull.Hardpoints[2].Rotation = ItemRotation.Reversed;
        Assert.Contains("must share Rotation and FiringArc", Refusal(rotated));
        Thrusting(("r", 90f, null), ("r", 90f, null), ("r", 90f, null), ("r", 90f, null)).Validate();
    }

    [Fact]
    public void AnArcIsJudgedAgainstTheReachAtEverySizeUpToATurn()
    {
        // The reach is the arc to the degree for a row of one, a row of three and a row of four, below, at and above 180.
        foreach (var arc in new[] { 10f, 100f, 181f, 270f, 359f, 360f })
        foreach (var size in new[] { 1, 3, 4 })
        {
            var members = Enumerable.Range(0, size).Select(_ => (size == 1 ? null : "r", arc, "j")).ToArray();
            OnJoint(Thrusting(members), "j", (Up, -arc / 2, arc / 2)).Validate();
            if (arc < 360f)
            {
                var short1 = Refusal(OnJoint(Thrusting(members), "j", (Up, -(arc - 1f) / 2, (arc - 1f) / 2)));
                Assert.Contains($"chain reaches {arc - 1f} degrees", short1);
            }
        }
    }

    [Fact]
    public void NoThrusterSwingsFurtherThanATurnWhetherOrNotItIsInARow()
    {
        foreach (var arc in new[] { 360.01f, 360.5f, 361f, 400f, 720f, 1000f })
        {
            Assert.Contains("firing arc may be at most 360 degrees", Refusal(Thrusting((null, arc, null))));
            Assert.Contains("firing arc may be at most 360 degrees", Refusal(Thrusting(("r", arc, null))));
            Assert.Contains("firing arc may be at most 360 degrees", Refusal(Thrusting(("r", arc, null), ("r", arc, null), ("r", arc, null))));
            Assert.Contains("firing arc may be at most 360 degrees", Refusal(OnJoint(Thrusting((null, arc, "j")), "j", (Up, -180f, 180f))));
        }
        Thrusting((null, 360f, null)).Validate();
        Thrusting((null, 359f, null), ("r", 360f, null), ("r", 360f, null)).Validate();
    }

    [Fact]
    public void OnlyAThrusterHardpointMayStateARowWhateverItsType()
    {
        foreach (var type in Enum.GetValues(typeof(HardpointType)).Cast<HardpointType>().Where(type => type != HardpointType.Thruster))
        {
            var ship = Thrusting((null, 0f, null));
            ship.Hull.Hardpoints.Single().Type = type;
            ship.Hull.Hardpoints.Single().Row = "r";
            Assert.Contains($"is a {type} hardpoint, and only a Thruster hardpoint may state a row", Refusal(ship));
        }
    }

    // Nesting is refused whichever actuators nest and whichever sits above: ruling one-joint-one-actuator says the judge refuses a
    // row whose reach includes a joint another actuator drives.
    [Fact]
    public void AnActuatorWhoseReachRunsThroughAJointAnotherActuatorDrivesIsRefusedWhicheverKindsNest()
    {
        // row under row
        var rows = Thrusting(("r", 90f, "outer"), ("s", 40f, "inner"));
        OnJoint(rows, "outer", (Up, -45f, 45f));
        rows.Visual.Joints.Add(Joint("inner", "outer", (Up, -20f, 20f)));
        Assert.Contains("joint inner reaches through joint outer, which another actuator drives", Refusal(rows));
        // a row of one under a row, and the row's reach is no excuse
        var single = Thrusting(("r", 90f, "outer"), (null, 40f, "inner"));
        OnJoint(single, "outer", (Up, -180f, 180f));
        single.Visual.Joints.Add(Joint("inner", "outer", (Up, -180f, 180f)));
        Assert.Contains("reaches through joint outer", Refusal(single));

        // a gun under a row's joint
        var gun = Armed("child");
        gun.Visual.Joints.Add(Joint("top", null, (Up, -45f, 45f)));
        gun.Visual.Joints.Add(Joint("child", "top", (Up, -20f, 20f)));
        var thruster = gun.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        thruster.FiringArc = 90;
        gun.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "top";
        gun.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = 40;
        Assert.Contains("joint child reaches through joint top", Refusal(gun));

        // a row under a gun's joint
        var below = Armed("top");
        below.Visual.Joints.Add(Joint("top", null, (Up, -180f, 180f)));
        below.Visual.Joints.Add(Joint("low", "top", (Up, -20f, 20f)));
        var lowThruster = below.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        lowThruster.FiringArc = 40;
        below.Visual.Anchors.Single(anchor => anchor.Id == lowThruster.Transform).Joint = "low";
        Assert.Contains("joint low reaches through joint top", Refusal(below));

        // Nesting is refused wherever the driven ancestor stands in the chain, with undriven joints between.
        var deep = Armed("top", "leaf");
        deep.Visual.Joints.Add(Joint("top", null, (Up, -90f, 90f)));
        deep.Visual.Joints.Add(Joint("mid", "top", (Up, -90f, 90f)));
        deep.Visual.Joints.Add(Joint("leaf", "mid", (Up, -90f, 90f)));
        Assert.Contains("joint leaf reaches through joint top", Refusal(deep));

        // Control: the same actuators on joints whose reaches are disjoint are accepted, and an undriven parent counts.
        var apart = Armed("top", "leaf");
        apart.Visual.Joints.Add(Joint("top", null, (Up, -90f, 90f)));
        apart.Visual.Joints.Add(Joint("base", null, (Up, -90f, 90f)));
        apart.Visual.Joints.Add(Joint("leaf", "base", (Up, -90f, 90f)));
        apart.Validate();
        var lowApart = Thrusting(("r", 90f, "low"), ("s", 90f, "other"));
        OnJoint(lowApart, "top", (Up, -45f, 45f));
        lowApart.Visual.Joints.Add(Joint("low", "top", (Up, -20f, 20f)));
        OnJoint(lowApart, "other", (Up, -45f, 45f));
        lowApart.Validate();
    }

    // The refusal does not depend on how many thrusters a row has, or how wide the gun's arc is beside the row's.
    [Fact]
    public void AGunAndAThrusterRowOnOneJointAreRefusedWhateverTheirSizes()
    {
        foreach (var gunArc in new[] { 10f, 45f, 89f, 90f, 91f, 180f, 360f })
        foreach (var size in new[] { 1, 2, 3, 4, 5 })
        foreach (var named in new[] { false, true })
        {
            var ship = Armed("j");
            ship.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
            ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "g0").FiringArc = gunArc;
            // The fixture thruster is the first member; the rest are appended, each its own cell and emitter.
            var rowName = named || size > 1 ? "r" : null;
            var first = ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
            first.FiringArc = 90; first.Row = rowName;
            ship.Visual.Anchors.Single(anchor => anchor.Id == first.Transform).Joint = "j";
            var shape = new Shape(2 + size - 1, 1);
            for (var x = 0; x < 2 + size - 1; x++) shape.Cells[x, 0] = true;
            ship.Hull.Shape = shape;
            for (var extra = 1; extra < size; extra++)
            {
                ship.Hull.Hardpoints.Add(new HardpointData
                    { Type = HardpointType.Thruster, Position = new int2(1 + extra, 0), Shape = new Shape(), Transform = $"x{extra}", FiringArc = 90, Row = "r" });
                ship.Visual.Anchors.Add(new ShipAnchor { Id = $"x{extra}", Role = "thruster-emitter", ModelNodeId = $"x{extra}", Joint = "j" });
            }
            Assert.Contains("joint j carries more than one actuator", Refusal(ship));
        }
    }

    // Every kind of conflict falls out of one definition of what drives a joint: a gun and a row, two rows, two rows of one, a
    // row and a thruster of no row, and a gun that nests.
    [Fact]
    public void EveryKindOfSharedDrivingIsRefusedByTheOneRuleThatSaysWhatDrivesAJoint()
    {
        var gunRow = Armed("j");
        gunRow.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        var thruster = gunRow.Hull.Hardpoints.Single(hardpoint => hardpoint.Type == HardpointType.Thruster);
        thruster.FiringArc = 90;
        gunRow.Visual.Anchors.Single(anchor => anchor.Id == thruster.Transform).Joint = "j";
        Assert.Contains("more than one actuator", Refusal(gunRow));
        Assert.Contains("more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), ("s", 90f, "j")), "j", (Up, -180f, 180f))));
        Assert.Contains("more than one actuator", Refusal(OnJoint(Thrusting((null, 90f, "j"), (null, 90f, "j")), "j", (Up, -180f, 180f))));
        Assert.Contains("more than one actuator", Refusal(OnJoint(Thrusting(("r", 90f, "j"), (null, 90f, "j")), "j", (Up, -180f, 180f))));
        var nested = Armed("a", "b");
        nested.Visual.Joints.Add(Joint("a", null, (Up, -180f, 180f)));
        nested.Visual.Joints.Add(Joint("b", "a", (Up, -180f, 180f)));
        Assert.Contains("which another actuator drives", Refusal(nested));
        // Control: two guns on one joint are one actuator.
        var guns = Armed("j", "j");
        guns.Visual.Joints.Add(Joint("j", null, (Up, -180f, 180f)));
        guns.Validate();
    }

    // Shipped hulls name a Unity prefab, not a rig, so the shipped catalog may hold none that rides a joint; every hull that does
    // name a visual record must pass the judge, and the count of those is what this run actually judged.
    [Fact]
    public void EveryShippedHullThatNamesARigPassesTheJudge()
    {
        var path = Path.Combine(_directory.Path, "shipped.cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), path);
        using var catalog = new CultCache(TestCatalog.Registry());
        catalog.AddBackingStore(new SingleFileMessagePackBackingStore(path), AetheriaStores.CatalogTypes);
        var hulls = catalog.GetAll<ItemData>().OfType<HullData>().ToArray();
        Assert.NotEmpty(hulls);
        foreach (var hull in hulls.Where(hull => hull.Visual.IsSet()))
            ShipAuthoringStore.Validate(hull, catalog.Get(hull.Visual));
    }

    // Row and Slew are persistent: a package or catalog written with them is read by key, so their positions are wire format.
    [Fact]
    public void RowAndSlewKeepTheirWirePositions()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HardpointData).Assembly);
        var hardpoint = new HardpointData { Type = HardpointType.Thruster, Shape = new Shape(), Transform = "t", FiringArc = 40f, Row = "left" };
        var fields = MessagePackSerializer.Deserialize<object[]>(MessagePackSerializer.Serialize(hardpoint, options), options);
        Assert.Equal(8, fields.Length);
        Assert.Equal(40f, Convert.ToSingle(fields[6]));
        Assert.Equal("left", fields[7]);
        Assert.Null(MessagePackSerializer.Deserialize<object[]>(MessagePackSerializer.Serialize(new HardpointData { Shape = new Shape() }, options), options)[7]);

        var thruster = new ThrusterData();
        thruster.Thrust.Max = 5f;
        thruster.Slew.Max = 90f;
        var slew = MessagePackSerializer.Deserialize<object[]>(MessagePackSerializer.Serialize(thruster, options), options);
        Assert.Equal(7, slew.Length);
        Assert.Equal(MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(thruster.Slew, options), options),
            MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(slew[6], options), options));
        Assert.NotEqual(MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(thruster.Thrust, options), options),
            MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(slew[6], options), options));
    }

    // A package written with a row reads back with it, and a fixed thruster with none reads back with none.
    [Fact]
    public async System.Threading.Tasks.Task ARowRoundTripsThroughAShipPackageAndNoRowStaysNone()
    {
        var path = Path.Combine(_directory.Path, "rows.cc");
        var ship = Thrusting(("r", 90f, null), ("r", 90f, null), (null, 0f, null));
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.Write(cache, ship.Hull, ship.Visual);
            await cache.FlushAsync();
        }
        var (hull, _) = ShipAuthoringStore.Read(path);
        Assert.Equal(new[] { "r", "r", null }, hull.Hardpoints.Select(hardpoint => hardpoint.Row));
        Assert.Equal(new[] { 90f, 90f, 0f }, hull.Hardpoints.Select(hardpoint => hardpoint.FiringArc));
    }
}
