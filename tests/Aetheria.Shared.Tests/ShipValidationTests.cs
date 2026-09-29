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
    private static readonly Dictionary<string, (Action<ShipAuthoring> Break, string Message)> Cases = Build();

    public static IEnumerable<object[]> CaseNames => Cases.Keys.Select(name => new object[] { name });

    [Fact]
    public void TheFixtureIsValid() => ShipAuthoringStore.Validate(ShipAuthoringTests.Fixture());

    [Theory, MemberData(nameof(CaseNames))]
    public void ValidatorRefusesEachBreakageByItsOwnGuard(string name)
    {
        var ship = ShipAuthoringTests.Fixture();
        Cases[name].Break(ship);
        var error = Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ship));
        Assert.Contains(Cases[name].Message, error.Message);
    }

    [Fact]
    public void ValidatorRefusesANullRecord() =>
        Assert.Contains("is null", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(null)).Message);

    [Theory]
    [InlineData("a"), InlineData("z"), InlineData("0"), InlineData("9"), InlineData("a0z9"), InlineData("0a"), InlineData("9z"),
     InlineData("a.b_c-d"), InlineData("a-"), InlineData("z_")]
    public void IdsMayUseLowerCaseAsciiDigitsDotsUnderscoresAndHyphens(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Id = id;
        ShipAuthoringStore.Validate(ship);
    }

    [Theory]
    [InlineData("`a"), InlineData("{a"), InlineData("/a"), InlineData(":a"), InlineData("@a"), InlineData("-a"), InlineData(".a"), InlineData("_a"),
     InlineData("a`"), InlineData("a{"), InlineData("a/"), InlineData("a:"), InlineData("a@"), InlineData("aA"), InlineData("a b")]
    public void IdsOutsideThatAlphabetAreRefused(string id)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Id = id;
        Assert.Contains("ship ID must use", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ship)).Message);
    }

    [Theory]
    [InlineData("skiff.glb"), InlineData("models/skiff.glb"), InlineData("models\\skiff.glb"), InlineData("..skiff.glb"), InlineData("a..b/skiff.glb")]
    public void ModelAssetsMayLiveInsideTheirPackage(string asset)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.ModelAsset = asset;
        ShipAuthoringStore.Validate(ship);
    }

    [Theory]
    [InlineData("../skiff.glb"), InlineData("..\\skiff.glb"), InlineData("a/../skiff.glb"), InlineData("a\\..\\skiff.glb"), InlineData("a/..")]
    public void ModelAssetsCannotEscapeTheirPackage(string asset)
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.ModelAsset = asset;
        Assert.Contains("relative package path", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ship)).Message);
    }

    [Fact]
    public void RootedModelAssetsAreRefused()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.ModelAsset = Path.GetFullPath("skiff.glb");
        Assert.Contains("relative package path", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ship)).Message);
    }

    [Fact]
    public void OptionalPartsAndRangeEdgesAreAccepted()
    {
        var ship = ShipAuthoringTests.Fixture();
        var line = ship.SchematicLines[0];
        line.Points = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 2f, 0f, 0f };
        line.Radii = new[] { 0f, 5f, 0f };
        line.Opacities = new[] { 0f, 1f, .5f };
        ShipAuthoringStore.Validate(ship);
        line.Radii = null;
        line.Opacities = null;
        line.Color = null;
        ShipAuthoringStore.Validate(ship);
        ship.SchematicLines = null;
        ship.Hull.Hardpoints = null;
        ship.Anchors.RemoveAt(ship.Anchors.Count - 1);
        ShipAuthoringStore.Validate(ship);
    }

    [Fact]
    public void AnAnchorMayNameItsHardpointAsParent()
    {
        var ship = ShipAuthoringTests.Fixture();
        ship.Anchors.Add(new ShipAnchor { Id = "muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "thruster.port" });
        ShipAuthoringStore.Validate(ship);
    }

    [Fact]
    public async Task ReadRefusesAFileThatHoldsTwoShips()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "two.cc");
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            cache.Upsert(ShipAuthoringTests.Fixture());
            var other = ShipAuthoringTests.Fixture();
            other.Id = "mod.other";
            cache.Upsert(other);
            await cache.FlushAsync();
        }
        Assert.Contains("found 2", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(path)).Message);
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
            cache.Upsert(ship);
            await cache.FlushAsync();
        }
        Assert.Contains("hull name is required", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(path)).Message);
    }

    private static Dictionary<string, (Action<ShipAuthoring>, string)> Build()
    {
        var cases = new Dictionary<string, (Action<ShipAuthoring>, string)>();
        void Add(string name, Action<ShipAuthoring> breakIt, string message) => cases.Add(name, (breakIt, message));
        HardpointData Mount(ShipAuthoring ship) => ship.Hull.Hardpoints[0];

        Add("id-null", s => s.Id = null, "Ship ID is required");
        Add("id-blank", s => s.Id = "  ", "Ship ID is required");
        Add("hull-null", s => s.Hull = null, "hull data is required");
        Add("hull-name-null", s => s.Hull.Name = null, "hull name is required");
        Add("hull-name-blank", s => s.Hull.Name = " ", "hull name is required");
        Add("hull-shape-null", s => s.Hull.Shape = null, "at least one cell");
        Add("hull-cells-null", s => s.Hull.Shape.Cells = null, "at least one cell");
        Add("hull-width-zero", s => s.Hull.Shape.Cells = new bool[0, 2], "at least one cell");
        Add("hull-height-zero", s => s.Hull.Shape.Cells = new bool[2, 0], "at least one cell");
        Add("hull-no-occupied-cell", s => s.Hull.Shape.Cells = new bool[2, 2], "at least one cell");
        Add("hull-names-a-prefab", s => s.Hull.Prefab = "Djinni", "cannot name a Unity prefab");
        Add("model-null", s => s.ModelAsset = null, "relative package path");
        Add("model-blank", s => s.ModelAsset = " ", "relative package path");

        Add("anchors-null", s => s.Anchors = null, "anchors are required");
        Add("anchor-null", s => s.Anchors.Add(null), "anchor IDs must be present and unique");
        Add("anchor-id-null", s => s.Anchors[4].Id = null, "anchor IDs must be present and unique");
        Add("anchor-id-blank", s => s.Anchors.Add(new ShipAnchor { Id = " ", Role = "articulation", ModelNodeId = "x" }), "anchor IDs must be present and unique");
        Add("anchor-id-duplicate", s => s.Anchors.Add(new ShipAnchor { Id = "map", Role = "articulation", ModelNodeId = "x" }), "anchor IDs must be present and unique");
        Add("anchor-node-null", s => s.Anchors[4].ModelNodeId = null, "needs a model node ID");
        Add("anchor-node-blank", s => s.Anchors[4].ModelNodeId = " ", "needs a model node ID");
        Add("anchor-node-shared", s => s.Anchors.Add(new ShipAnchor { Id = "other", Role = "articulation", ModelNodeId = "map" }), "claimed by more than one anchor");
        foreach (var role in new[] { "map-icon", "hull-collider", "shield", "tractor" })
        {
            Add("role-missing-" + role, s => s.Anchors.RemoveAll(a => a.Role == role), $"exactly one {role} anchor is required");
            Add("role-doubled-" + role, s => s.Anchors.Add(new ShipAnchor { Id = "extra", Role = role, ModelNodeId = "extra" }), $"exactly one {role} anchor is required");
        }

        Add("hardpoint-null", s => s.Hull.Hardpoints.Add(null), "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-null", s => Mount(s).Transform = null, "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-blank", s => Mount(s).Transform = " ", "hardpoint IDs must be present and unique");
        Add("hardpoint-transform-duplicate", s => s.Hull.Hardpoints.Add(new HardpointData { Position = new int2(0, 0), Transform = "thruster.port" }), "hardpoint IDs must be present and unique");
        Add("hardpoint-shape-null", s => Mount(s).Shape = null, "has no cells");
        Add("hardpoint-cells-null", s => Mount(s).Shape.Cells = null, "has no cells");
        Add("hardpoint-cells-all-empty", s => Mount(s).Shape.Cells = new bool[1, 1], "has no cells");
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
            s.Anchors.Add(new ShipAnchor { Id = "thruster.starboard", Role = "thruster-emitter", ModelNodeId = "thruster-starboard" });
        }, "overlaps another hardpoint");
        Add("hardpoint-without-anchor", s => s.Anchors.RemoveAll(a => a.Id == "thruster.port"), "has no model anchor");
        Add("anchor-parent-unknown", s => s.Anchors.Add(new ShipAnchor { Id = "muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "nowhere" }), "unknown hardpoint parent");

        Add("line-null", s => s.SchematicLines.Add(null), "finite XYZ points");
        Add("line-points-null", s => s.SchematicLines[0].Points = null, "finite XYZ points");
        Add("line-one-point", s => s.SchematicLines[0].Points = new[] { 0f, 0f, 0f }, "finite XYZ points");
        Add("line-points-not-triples", s => s.SchematicLines[0].Points = new float[7], "finite XYZ points");
        Add("line-point-nan", s => s.SchematicLines[0].Points[3] = float.NaN, "finite XYZ points");
        Add("line-point-infinite", s => s.SchematicLines[0].Points[5] = float.PositiveInfinity, "finite XYZ points");
        Add("line-radii-too-short", s => s.SchematicLines[0].Radii = new[] { .1f }, "attributes have the wrong length");
        Add("line-radii-too-long", s => s.SchematicLines[0].Radii = new[] { .1f, .1f, .1f }, "attributes have the wrong length");
        Add("line-opacities-too-short", s => s.SchematicLines[0].Opacities = new[] { 1f }, "attributes have the wrong length");
        Add("line-opacities-too-long", s => s.SchematicLines[0].Opacities = new[] { 1f, 1f, 1f }, "attributes have the wrong length");
        Add("line-radius-nan", s => s.SchematicLines[0].Radii[1] = float.NaN, "radius and opacity must be finite");
        Add("line-radius-infinite", s => s.SchematicLines[0].Radii[1] = float.PositiveInfinity, "radius and opacity must be finite");
        Add("line-radius-negative", s => s.SchematicLines[0].Radii[0] = -.01f, "radius and opacity must be finite");
        Add("line-opacity-nan", s => s.SchematicLines[0].Opacities[1] = float.NaN, "radius and opacity must be finite");
        Add("line-opacity-infinite", s => s.SchematicLines[0].Opacities[1] = float.PositiveInfinity, "radius and opacity must be finite");
        Add("line-opacity-negative", s => s.SchematicLines[0].Opacities[0] = -.01f, "radius and opacity must be finite");
        Add("line-opacity-above-one", s => s.SchematicLines[0].Opacities[0] = 1.01f, "radius and opacity must be finite");
        Add("line-color-short", s => s.SchematicLines[0].Color = new[] { 1f, 1f, 1f }, "must be finite RGBA");
        Add("line-color-long", s => s.SchematicLines[0].Color = new[] { 1f, 1f, 1f, 1f, 1f }, "must be finite RGBA");
        Add("line-color-nan", s => s.SchematicLines[0].Color[2] = float.NaN, "must be finite RGBA");
        Add("line-color-infinite", s => s.SchematicLines[0].Color[3] = float.NegativeInfinity, "must be finite RGBA");
        return cases;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheria-ship-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, true);
}
