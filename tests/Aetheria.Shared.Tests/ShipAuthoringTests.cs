using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CultMath;
using GameCult.Caching;
using Xunit;

public sealed class ShipAuthoringTests
{
    [Fact]
    public async Task StandaloneShipRoundTripsWithoutTouchingTheCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheria-ship-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "skiff.cc");
        try
        {
            var ship = Fixture();
            ship.Validate();
            using (var cache = ShipAuthoringStore.Open(path, writable: true))
            {
                ShipAuthoringStore.Write(cache, ship.Hull, ship.Visual);
                await cache.FlushAsync();
            }
            var (hull, visual) = ShipAuthoringStore.Read(path);
            Assert.Equal("mod.skiff", visual.Id);
            Assert.Equal("Skiff", hull.Name);
            Assert.True(hull.Shape.Cells[1, 0]);
            Assert.Equal("thruster.port", hull.Hardpoints[0].Transform);
            Assert.Equal(ShipModCatalog.AuthoringKey("mod.skiff"), hull.Visual.Key);
            Assert.Equal("skiff.glb", visual.ModelAsset);
            Assert.Equal(2, visual.SchematicLines[0].Points.Length / 3);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void InvalidSchematicOrDanglingMountCannotBePublished()
    {
        var ship = Fixture();
        ship.Hull.Shape.Cells[1, 0] = false;
        Assert.Contains("outside the hull schematic", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Visual.Anchors.RemoveAt(ship.Visual.Anchors.Count - 1);
        Assert.Contains("has no model anchor", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Visual.SchematicLines[0].Points[0] = float.NaN;
        Assert.Contains("finite XYZ", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Hull.Hardpoints.Add(new HardpointData
        {
            Type = HardpointType.Thruster,
            Position = new int2(1, 0),
            Shape = new Shape(),
            Transform = "thruster.starboard"
        });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "thruster.starboard", Role = "thruster-emitter", ModelNodeId = "thruster-starboard" });
        Assert.Contains("overlaps another hardpoint", Assert.Throws<InvalidOperationException>(ship.Validate).Message);
    }

    // The hull owns hull semantics and the visual owns the model package, so the one joint check refuses a hull mount the
    // visual has no anchor for, and a visual anchor parented to a mount the hull does not have.
    [Fact]
    public void HardpointsMustResolveToTheVisualsAnchors()
    {
        var ship = Fixture();
        ship.Hull.Hardpoints[0].Transform = "thruster.renamed";
        Assert.Contains("thruster.renamed has no model anchor", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "thruster.gone" });
        Assert.Contains("unknown hardpoint parent thruster.gone", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        Fixture().Validate();
    }

    [Fact]
    public void HullNamesExactlyOneVisual()
    {
        var ship = Fixture();
        ship.Hull.Prefab = "Djinni";
        Assert.Contains("cannot name a Unity prefab", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Hull.Visual = default;
        Assert.Contains("the hull must name its visual record mod-ship:mod.skiff", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey("mod.other"));
        Assert.Contains("the hull must name its visual record mod-ship:mod.skiff", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        Fixture().Validate();
    }

    [Fact]
    public async Task LoadRefusesAFileWhoseRecordsAreNotOneHullAndOneVisualAtTheirKeys()
    {
        using var directory = new TempDirectory();
        async Task<string> Write(string name, Action<CultCache> fill)
        {
            var path = Path.Combine(directory.Path, name);
            using var cache = ShipAuthoringStore.Open(path, writable: true);
            fill(cache);
            await cache.FlushAsync();
            return path;
        }
        void Hull(CultCache cache, string key) =>
            cache.UpsertAsync(typeof(HullData), Fixture().Hull, new CultRecordKey(key)).GetAwaiter().GetResult();
        void Visual(CultCache cache, string key, string id = "mod.skiff") =>
            cache.UpsertAsync(typeof(ShipAuthoring), Fixture().WithId(id).Visual, new CultRecordKey(key)).GetAwaiter().GetResult();

        var twoShips = await Write("two.cc", cache =>
        {
            ShipAuthoringStore.Write(cache, Fixture().Hull, Fixture().Visual);
            Visual(cache, "mod-ship:mod.other", "mod.other");
        });
        Assert.Contains("found 2", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(twoShips)).Message);

        var noHull = await Write("nohull.cc", cache => Visual(cache, "mod-ship:mod.skiff"));
        Assert.Contains("exactly one hull record, found 0", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(noHull)).Message);

        var twoHulls = await Write("twohulls.cc", cache =>
        {
            ShipAuthoringStore.Write(cache, Fixture().Hull, Fixture().Visual);
            Hull(cache, "mod-hull:mod.other");
        });
        Assert.Contains("exactly one hull record, found 2", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(twoHulls)).Message);

        var wrongHullKey = await Write("hullkey.cc", cache => { Hull(cache, "hull:skiff"); Visual(cache, "mod-ship:mod.skiff"); });
        Assert.Contains("records must be stored under mod-hull:mod.skiff and mod-ship:mod.skiff",
            Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(wrongHullKey)).Message);

        var wrongVisualKey = await Write("visualkey.cc", cache => { Hull(cache, "mod-hull:mod.skiff"); Visual(cache, "ship:skiff"); });
        Assert.Contains("records must be stored under", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(wrongVisualKey)).Message);
    }

    internal static ShipParts Fixture()
    {
        var shape = new Shape(2, 2);
        shape.Cells[0, 0] = true;
        shape.Cells[1, 0] = true;
        return new ShipParts
        {
            Hull = new HullData
            {
                Name = "Skiff",
                Shape = shape,
                Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey("mod.skiff")),
                Hardpoints = new List<HardpointData>
                {
                    new HardpointData
                    {
                        Type = HardpointType.Thruster,
                        Position = new int2(1, 0),
                        Shape = new Shape(),
                        Transform = "thruster.port"
                    }
                }
            },
            Visual = new ShipAuthoring
            {
                Id = "mod.skiff",
                ModelAsset = "skiff.glb",
                Anchors = new List<ShipAnchor>
                {
                    new ShipAnchor { Id = "map", Role = "map-icon", ModelNodeId = "map" },
                    new ShipAnchor { Id = "collider", Role = "hull-collider", ModelNodeId = "collider" },
                    new ShipAnchor { Id = "shield", Role = "shield", ModelNodeId = "shield" },
                    new ShipAnchor { Id = "tractor", Role = "tractor", ModelNodeId = "tractor" },
                    new ShipAnchor { Id = "thruster.port", Role = "thruster-emitter", ModelNodeId = "thruster-port" }
                },
                SchematicLines = new List<ShipPolyline>
                {
                    new ShipPolyline
                    {
                        Layer = "Hull",
                        Material = "White",
                        Points = new[] { 0f, 0f, 0f, 1f, 0f, 0f },
                        Radii = new[] { .01f, .01f },
                        Opacities = new[] { 1f, 1f },
                        Color = new[] { 1f, 1f, 1f, 1f }
                    }
                }
            }
        };
    }
}

// One ship's two records: the hull and the visual it names.
internal sealed class ShipParts
{
    public HullData Hull;
    public ShipAuthoring Visual;

    public void Deconstruct(out HullData hull, out ShipAuthoring visual) => (hull, visual) = (Hull, Visual);

    public ShipParts WithId(string id)
    {
        Visual.Id = id;
        Hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(id));
        return this;
    }

    public void Validate() => ShipAuthoringStore.Validate(Hull, Visual);
}
