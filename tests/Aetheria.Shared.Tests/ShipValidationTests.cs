using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CultMath;
using Xunit;

// One case per refusal in ShipAuthoringStore.Validate and Read. Each case starts from the valid fixture, breaks exactly
// one thing, and names the message that guard alone produces, so a guard that stops firing (or fires for the wrong
// reason) fails its own case and no other.
public sealed class ShipValidationTests
{
    private static readonly Dictionary<string, (Action<ShipParts> Break, string Message)> Cases = Build();

    public static IEnumerable<object[]> CaseNames => Cases.Keys.Select(name => new object[] { name });

    [Fact]
    public void TheFixtureIsValid() => ShipAuthoringTests.Fixture().Validate();

    [Theory, MemberData(nameof(CaseNames))]
    public void ValidatorRefusesEachBreakageByItsOwnGuard(string name)
    {
        var ship = ShipAuthoringTests.Fixture();
        Cases[name].Break(ship);
        var error = Assert.Throws<InvalidOperationException>(ship.Validate);
        Assert.Contains(Cases[name].Message, error.Message);
    }

    [Fact]
    public void HardpointsAtTheEdgesOfTheirRangesAreValid()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Hull.Hardpoints[0].Armor = 5;
        ship.Hull.Hardpoints[0].FiringArc = 360;
        ship.Hull.Hardpoints[0].Type = HardpointType.ControlModule;
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Id == "thruster.port");
        ship.Hull.Hardpoints[0].Rotation = ItemRotation.Clockwise;
        ship.Validate();
    }

    [Fact]
    public void ValidatorRefusesANullRecord() =>
        Assert.Contains("is null", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ShipAuthoringTests.Fixture().Hull, null)).Message);

    [Theory]
    [InlineData("a"), InlineData("z"), InlineData("0"), InlineData("9"), InlineData("a0z9"), InlineData("0a"), InlineData("9z"),
     InlineData("a.b_c-d"), InlineData("a-"), InlineData("z_")]
    public void IdsMayUseLowerCaseAsciiDigitsDotsUnderscoresAndHyphens(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.WithId(id).Validate();
    }

    [Theory]
    [InlineData("`a"), InlineData("{a"), InlineData("/a"), InlineData(":a"), InlineData("@a"), InlineData("-a"), InlineData(".a"), InlineData("_a"),
     InlineData("a`"), InlineData("a{"), InlineData("a/"), InlineData("a:"), InlineData("a@"), InlineData("aA"), InlineData("a b")]
    public void IdsOutsideThatAlphabetAreRefused(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.WithId(id);
        Assert.Contains("Ship ID must use", Assert.Throws<InvalidOperationException>(ship.Validate).Message);
    }

    [Theory]
    [InlineData("com0"), InlineData("console"), InlineData("lpt"), InlineData("auxiliary"), InlineData("a.con"), InlineData("com10")]
    public void IdsThatOnlyResembleDeviceNamesAreAdmitted(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.WithId(id);
        ship.Validate();
    }

    [Theory]
    [InlineData("con"), InlineData("abc.")]
    public void IdRefusalsNameNoValue(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.WithId(id);
        Assert.DoesNotContain(id, Assert.Throws<InvalidOperationException>(ship.Validate).Message);
    }

    [Theory]
    [InlineData("skiff.glb"), InlineData("models/skiff.glb"), InlineData("models\\skiff.glb"), InlineData("..skiff.glb"), InlineData("a..b/skiff.glb")]
    public void ModelAssetsMayLiveInsideTheirPackage(string asset)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Visual.ModelAsset = asset;
        ship.Validate();
    }

    [Theory]
    [InlineData("../skiff.glb"), InlineData("..\\skiff.glb"), InlineData("a/../skiff.glb"), InlineData("a\\..\\skiff.glb"), InlineData("a/..")]
    public void ModelAssetsCannotEscapeTheirPackage(string asset)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Visual.ModelAsset = asset;
        Assert.Contains("relative package path", Assert.Throws<InvalidOperationException>(ship.Validate).Message);
    }

    [Fact]
    public void RootedModelAssetsAreRefused()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Visual.ModelAsset = Path.GetFullPath("skiff.glb");
        Assert.Contains("relative package path", Assert.Throws<InvalidOperationException>(ship.Validate).Message);
    }

    [Fact]
    public void OptionalPartsAndRangeEdgesAreAccepted()
    {
        var ship = ShipAuthoringTests.Fixture();
        var line = ship.Visual.SchematicLines[0];
        line.Points = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 2f, 0f, 0f };
        line.Radii = new[] { 0f, 5f, 0f };
        line.Opacities = new[] { 0f, 1f, .5f };
        ship.Validate();
        line.Radii = null;
        line.Opacities = null;
        line.Color = null;
        ship.Validate();
        ship.Visual.SchematicLines = null;
        ship.Hull.Hardpoints = null;
        ship.Visual.Anchors.RemoveAt(ship.Visual.Anchors.Count - 1);
        ship.Validate();
    }

    [Fact]
    public void EmptyCellsInAHardpointFootprintNeedNotLieOnTheHull()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Hull.Hardpoints[0].Shape = new Shape(2, 1);
        ship.Hull.Hardpoints[0].Shape.Cells[0, 0] = true;
        ship.Validate();
    }

    [Fact]
    public void AnEmptyParentIdMeansNoParent()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Visual.Anchors[0].ParentId = "";
        ship.Validate();
    }

    [Fact]
    public void AnAnchorMayNameItsHardpointAsParent()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Ballistic, Position = new int2(0, 0), Shape = new Shape(), Transform = "gun" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun", Role = "weapon-mount", ModelNodeId = "gun" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "gun" });
        ship.Validate();
    }

    [Fact]
    public async Task ReadValidatesWhatItLoads()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "bad.cc");
        var ship = ShipAuthoringTests.Fixture();
        ship.Hull.Name = "";
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.Write(cache, ship.Hull, ship.Visual);
            await cache.FlushAsync();
        }
        Assert.Contains("hull name is required", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(path)).Message);
    }

    private static Dictionary<string, (Action<ShipParts>, string)> Build()
    {
        var cases = new Dictionary<string, (Action<ShipParts>, string)>();
        void Add(string name, Action<ShipParts> breakIt, string message) => cases.Add(name, (breakIt, message));
        HardpointData Mount(ShipParts ship) => ship.Hull.Hardpoints[0];

        Add("id-null", s => s.Visual.Id = null, "Ship ID is required");
        Add("id-blank", s => s.Visual.Id = "  ", "Ship ID is required");
        foreach (var device in new[] { "con", "prn", "aux", "nul", "nul.x", "com1", "com9", "lpt1", "lpt9", "con.txt" })
            Add("id-device-" + device, s => s.Visual.Id = device, "Windows device name");
        foreach (var id in new[] { "abc.", "a.", "ab.", "a.b." })
            Add("id-trailing-dot-" + id, s => s.Visual.Id = id, "must not end with a dot");
        Add("hull-null", s => s.Hull = null, "hull data is required");
        Add("hull-name-null", s => s.Hull.Name = null, "hull name is required");
        Add("hull-name-blank", s => s.Hull.Name = " ", "hull name is required");
        Add("hull-shape-null", s => s.Hull.Shape = null, "at least one cell");
        Add("hull-cells-null", s => s.Hull.Shape.Cells = null, "at least one cell");
        Add("hull-width-zero", s => s.Hull.Shape.Cells = new bool[0, 2], "at least one cell");
        Add("hull-height-zero", s => s.Hull.Shape.Cells = new bool[2, 0], "at least one cell");
        Add("hull-no-occupied-cell", s => s.Hull.Shape.Cells = new bool[2, 2], "at least one cell");
        Add("hull-names-a-prefab-and-a-visual", s => s.Hull.Prefab = "Djinni", "names both a Unity prefab and a visual record");
        Add("model-null", s => s.Visual.ModelAsset = null, "relative package path");
        Add("model-blank", s => s.Visual.ModelAsset = " ", "relative package path");

        Add("anchors-null", s => s.Visual.Anchors = null, "anchors are required");
        Add("anchor-null", s => s.Visual.Anchors.Add(null), "anchor IDs must be present and unique");
        Add("anchor-id-null", s => s.Visual.Anchors[4].Id = null, "anchor IDs must be present and unique");
        Add("anchor-id-blank", s => s.Visual.Anchors.Add(new ShipAnchor { Id = " ", Role = "weapon-muzzle", ModelNodeId = "x" }), "anchor IDs must be present and unique");
        Add("anchor-id-duplicate", s => s.Visual.Anchors.Add(new ShipAnchor { Id = "map", Role = "weapon-muzzle", ModelNodeId = "x" }), "anchor IDs must be present and unique");
        Add("anchor-node-null", s => s.Visual.Anchors[4].ModelNodeId = null, "needs a model node ID");
        Add("anchor-node-blank", s => s.Visual.Anchors[4].ModelNodeId = " ", "needs a model node ID");
        Add("anchor-node-shared", s => s.Visual.Anchors.Add(new ShipAnchor { Id = "other", Role = "weapon-muzzle", ModelNodeId = "map" }), "claimed by more than one anchor");
        foreach (var role in new[] { "map-icon", "hull-collider", "shield", "tractor" })
        {
            Add("role-missing-" + role, s => s.Visual.Anchors.RemoveAll(a => a.Role == role), $"exactly one {role} anchor is required");
            Add("role-doubled-" + role, s => s.Visual.Anchors.Add(new ShipAnchor { Id = "extra", Role = role, ModelNodeId = "extra" }), $"exactly one {role} anchor is required");
        }

        void Joint(ShipParts s, string id, string parent, float[] axes, float[] min, float[] max) =>
            s.Visual.Joints.Add(new ShipJoint { Id = id, Parent = parent, Axes = axes, Min = min, Max = max });
        var yaw = new[] { 0f, 1f, 0f };
        Add("joint-id-blank", s => Joint(s, " ", null, yaw, new[] { -1f }, new[] { 1f }), "joint IDs must be present and unique");
        Add("joint-null", s => s.Visual.Joints.Add(null), "joint IDs must be present and unique");
        Add("joint-id-duplicate", s => { Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f }); Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f }); }, "joint IDs must be present and unique");
        Add("joint-id-is-an-anchor-node", s => Joint(s, "map", null, yaw, new[] { -1f }, new[] { 1f }), "is also an anchor's model node");
        Add("joint-axes-length-four", s => Joint(s, "j", null, new[] { 0f, 1f, 0f, 0f }, new[] { -1f }, new[] { 1f }), "needs one or more degrees of freedom");
        Add("joint-no-degrees-of-freedom", s => Joint(s, "j", null, new float[0], new float[0], new float[0]), "needs one or more degrees of freedom");
        Add("joint-null-limits", s => Joint(s, "j", null, yaw, null, null), "needs one or more degrees of freedom");
        Add("joint-max-count-differs", s => Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f, 2f }), "needs one or more degrees of freedom");
        Add("joint-axis-not-unit", s => Joint(s, "j", null, new[] { 0f, 2f, 0f }, new[] { -1f }, new[] { 1f }), "must be a unit vector");
        Add("joint-axis-not-finite", s => Joint(s, "j", null, new[] { 0f, float.NaN, 0f }, new[] { -1f }, new[] { 1f }), "axes and limits must be finite");
        Add("joint-limit-not-finite", s => Joint(s, "j", null, yaw, new[] { -1f }, new[] { float.PositiveInfinity }), "axes and limits must be finite");
        Add("joint-min-not-finite", s => Joint(s, "j", null, yaw, new[] { float.NaN }, new[] { 1f }), "axes and limits must be finite");
        Add("joint-max-not-a-number", s => Joint(s, "j", null, yaw, new[] { -1f }, new[] { float.NaN }), "axes and limits must be finite");
        Add("joint-min-above-max", s => Joint(s, "j", null, yaw, new[] { 10f }, new[] { 5f }), "must satisfy -180 <= Min <= Max <= 180");
        Add("joint-max-181", s => Joint(s, "j", null, yaw, new[] { 0f }, new[] { 181f }), "must satisfy -180 <= Min <= Max <= 180");
        Add("joint-min-minus-181", s => Joint(s, "j", null, yaw, new[] { -181f }, new[] { 0f }), "must satisfy -180 <= Min <= Max <= 180");
        Add("joint-parent-dangling", s => Joint(s, "j", "ghost", yaw, new[] { -1f }, new[] { 1f }), "has an unknown parent joint");
        Add("joint-cycle", s => { Joint(s, "a", "b", yaw, new[] { -1f }, new[] { 1f }); Joint(s, "b", "a", yaw, new[] { -1f }, new[] { 1f }); }, "is on a parent cycle");
        Add("joint-on-a-thruster-mount", s => { Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f }); s.Visual.Anchors[4].Joint = "j"; }, "gimballed-thrusters");
        Add("joint-on-a-radiator-mount", s => { Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f }); s.Visual.Anchors[4].Role = "radiator-mesh"; s.Visual.Anchors[4].Joint = "j"; s.Hull.Hardpoints[0].Type = HardpointType.Radiator; }, "gimballed-thrusters");
        Add("joint-on-a-map-icon", s => { Joint(s, "j", null, yaw, new[] { -1f }, new[] { 1f }); s.Visual.Anchors[0].Joint = "j"; }, "may not name a joint");
        Add("anchor-role-unknown", s => s.Visual.Anchors.Add(new ShipAnchor { Id = "x", Role = "engine", ModelNodeId = "x" }), "has unknown role 'engine'");
        Add("anchor-role-null", s => s.Visual.Anchors.Add(new ShipAnchor { Id = "x", Role = null, ModelNodeId = "x" }), "has unknown role ''");
        Add("anchor-role-cased", s => s.Visual.Anchors[0].Role = "Map-Icon", "has unknown role 'Map-Icon'");

        Add("hardpoint-null", s => s.Hull.Hardpoints.Add(null), "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-null", s => Mount(s).Transform = null, "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-blank", s => Mount(s).Transform = " ", "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-duplicate", s => s.Hull.Hardpoints.Add(new HardpointData { Position = new int2(0, 0), Transform = "thruster.port" }), "hardpoint IDs must be present and unique");
        Add("hardpoint-shape-null", s => Mount(s).Shape = null, "has no cells");
        Add("hardpoint-cells-null", s => Mount(s).Shape.Cells = null, "has no cells");
        Add("hardpoint-cells-all-empty", s => Mount(s).Shape.Cells = new bool[1, 1], "has no cells");
        Add("hardpoint-type-undefined", s => Mount(s).Type = (HardpointType)99, "unknown type 99");
        Add("hardpoint-type-negative", s => Mount(s).Type = (HardpointType)(-1), "unknown type -1");
        Add("hardpoint-rotation-undefined", s => Mount(s).Rotation = (ItemRotation)4, "unknown rotation 4");
        Add("hardpoint-rotation-negative", s => Mount(s).Rotation = (ItemRotation)(-1), "unknown rotation -1");
        Add("hardpoint-armor-nan", s => Mount(s).Armor = float.NaN, "armor and firing arc must be finite and not negative");
        Add("hardpoint-armor-infinite", s => Mount(s).Armor = float.PositiveInfinity, "armor and firing arc must be finite and not negative");
        Add("hardpoint-armor-negative", s => Mount(s).Armor = -1, "armor and firing arc must be finite and not negative");
        Add("hardpoint-arc-nan", s => Mount(s).FiringArc = float.NaN, "armor and firing arc must be finite and not negative");
        Add("hardpoint-arc-infinite", s => Mount(s).FiringArc = float.NegativeInfinity, "armor and firing arc must be finite and not negative");
        Add("hardpoint-arc-negative", s => Mount(s).FiringArc = -1, "armor and firing arc must be finite and not negative");
        Add("hardpoint-left-of-hull", s => Mount(s).Position = new int2(-1, 0), "outside the hull schematic");
        Add("hardpoint-below-hull", s => Mount(s).Position = new int2(1, -1), "outside the hull schematic");
        Add("hardpoint-right-of-hull", s => Mount(s).Position = new int2(2, 0), "outside the hull schematic");
        Add("hardpoint-above-hull", s => Mount(s).Position = new int2(1, 2), "outside the hull schematic");
        Add("hardpoint-on-an-empty-hull-cell", s => Mount(s).Position = new int2(0, 1), "outside the hull schematic");
        Add("hardpoint-footprint-overhangs", s =>
        {
            Mount(s).Shape = new Shape(2, 1);
            Mount(s).Shape.Cells[0, 0] = Mount(s).Shape.Cells[1, 0] = true;
        }, "outside the hull schematic");
        Add("hardpoints-overlap", s =>
        {
            s.Hull.Hardpoints.Add(new HardpointData { Position = new int2(1, 0), Transform = "thruster.starboard" });
            s.Visual.Anchors.Add(new ShipAnchor { Id = "thruster.starboard", Role = "thruster-emitter", ModelNodeId = "thruster-starboard" });
        }, "overlaps another hardpoint");
        Add("hardpoint-without-anchor", s => s.Visual.Anchors.RemoveAll(a => a.Id == "thruster.port"), "has no model anchor");
        Add("anchor-parent-unknown", s => s.Visual.Anchors.Add(new ShipAnchor { Id = "muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "nowhere" }), "unknown hardpoint parent");

        Add("line-null", s => s.Visual.SchematicLines.Add(null), "finite XYZ points");
        Add("line-points-null", s => s.Visual.SchematicLines[0].Points = null, "finite XYZ points");
        Add("line-one-point", s => s.Visual.SchematicLines[0].Points = new[] { 0f, 0f, 0f }, "finite XYZ points");
        Add("line-points-not-triples", s => s.Visual.SchematicLines[0].Points = new float[7], "finite XYZ points");
        Add("line-point-nan", s => s.Visual.SchematicLines[0].Points[3] = float.NaN, "finite XYZ points");
        Add("line-point-infinite", s => s.Visual.SchematicLines[0].Points[5] = float.PositiveInfinity, "finite XYZ points");
        Add("line-radii-too-short", s => s.Visual.SchematicLines[0].Radii = new[] { .1f }, "attributes have the wrong length");
        Add("line-radii-too-long", s => s.Visual.SchematicLines[0].Radii = new[] { .1f, .1f, .1f }, "attributes have the wrong length");
        Add("line-opacities-too-short", s => s.Visual.SchematicLines[0].Opacities = new[] { 1f }, "attributes have the wrong length");
        Add("line-opacities-too-long", s => s.Visual.SchematicLines[0].Opacities = new[] { 1f, 1f, 1f }, "attributes have the wrong length");
        Add("line-radius-nan", s => s.Visual.SchematicLines[0].Radii[1] = float.NaN, "radius and opacity must be finite");
        Add("line-radius-infinite", s => s.Visual.SchematicLines[0].Radii[1] = float.PositiveInfinity, "radius and opacity must be finite");
        Add("line-radius-negative", s => s.Visual.SchematicLines[0].Radii[0] = -.01f, "radius and opacity must be finite");
        Add("line-opacity-nan", s => s.Visual.SchematicLines[0].Opacities[1] = float.NaN, "radius and opacity must be finite");
        Add("line-opacity-infinite", s => s.Visual.SchematicLines[0].Opacities[1] = float.PositiveInfinity, "radius and opacity must be finite");
        Add("line-opacity-negative", s => s.Visual.SchematicLines[0].Opacities[0] = -.01f, "radius and opacity must be finite");
        Add("line-opacity-above-one", s => s.Visual.SchematicLines[0].Opacities[0] = 1.01f, "radius and opacity must be finite");
        Add("line-color-short", s => s.Visual.SchematicLines[0].Color = new[] { 1f, 1f, 1f }, "must be finite RGBA");
        Add("line-color-long", s => s.Visual.SchematicLines[0].Color = new[] { 1f, 1f, 1f, 1f, 1f }, "must be finite RGBA");
        Add("line-color-nan", s => s.Visual.SchematicLines[0].Color[2] = float.NaN, "must be finite RGBA");
        Add("line-color-infinite", s => s.Visual.SchematicLines[0].Color[3] = float.NegativeInfinity, "must be finite RGBA");
        return cases;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheria-ship-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, true);
}
