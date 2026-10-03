using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching.MessagePack;
using MessagePack;
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
        Assert.Contains("names both a Unity prefab and a visual record", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        // A whitespace-only prefab is unset.
        ship = Fixture();
        ship.Hull.Prefab = " \t";
        ship.Validate();

        ship = Fixture();
        ship.Hull.Visual = default;
        Assert.Contains("the hull must name its visual record mod-ship:mod.skiff", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        ship = Fixture();
        ship.Hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey("mod.other"));
        Assert.Contains("the hull must name its visual record mod-ship:mod.skiff", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        // The key is compared exactly: a differently-cased key is another record.
        ship = Fixture();
        ship.Hull.Visual = new CultRecordRef<ShipAuthoring>(new CultRecordKey("MOD-SHIP:mod.skiff"));
        Assert.Contains("the hull must name its visual record mod-ship:mod.skiff", Assert.Throws<InvalidOperationException>(ship.Validate).Message);

        Fixture().Validate();
    }

    // A hull with both a prefab and a visual is refused wherever hulls are validated or composed.
    // ShipModCatalogTests.ComposeRefusesAShippedHullThatNamesTwoBodies covers Compose.
    [Fact]
    public void RequireOneBodyRefusesBothAndAcceptsEitherAlone()
    {
        var hull = Fixture().Hull;
        ShipAuthoringStore.RequireOneBody(hull, "skiff");
        hull.Prefab = "Djinni";
        Assert.Contains("skiff: a hull names one body", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.RequireOneBody(hull, "skiff")).Message);
        hull.Visual = default;
        ShipAuthoringStore.RequireOneBody(hull, "skiff");
    }

    // The ship authoring record held the whole hull at key 1 before S1.
    [Fact]
    public async Task LoadRefusesAShipAuthoringRecordThatStillCarriesTheRetiredEmbeddedHull()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "legacy.cc");
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.Write(cache, Fixture().Hull, Fixture().Visual);
            await cache.FlushAsync();
        }
        RewritePayload(path, "aetheria.ship_authoring", slots => { slots[1] = new object[] { "Skiff" }; });
        foreach (var load in new Action[] { () => ShipAuthoringStore.Load(path), () => ShipAuthoringStore.Read(path) })
        {
            var message = Assert.Throws<InvalidOperationException>(load).Message;
            Assert.Contains("legacy embedded hull at retired key 1", message);
            Assert.Contains(path, message);
        }

        // An old file has no separate hull record at all; it is named for what it is, not for a missing hull.
        var oldShape = Path.Combine(directory.Path, "old.cc");
        using (var cache = ShipAuthoringStore.Open(oldShape, writable: true))
        {
            cache.UpsertAsync(typeof(ShipAuthoring), Fixture().Visual, ShipModCatalog.AuthoringKey("mod.skiff")).GetAwaiter().GetResult();
            await cache.FlushAsync();
        }
        RewritePayload(oldShape, "aetheria.ship_authoring", slots => { slots[1] = new object[] { "Skiff" }; });
        var oldMessage = Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(oldShape)).Message;
        Assert.Contains("legacy embedded hull", oldMessage);
        Assert.DoesNotContain("exactly one hull record", oldMessage);

        // A nil in the retired slot is the shape every current file has.
        RewritePayload(path, "aetheria.ship_authoring", slots => { slots[1] = null; });
        ShipAuthoringStore.Load(path);
    }

    // The layout editor's Python once accepted any msgpack value in a cell; C# then failed with a bare serializer exception.
    [Fact]
    public async Task LoadNamesTheFileWhoseRecordDoesNotDecode()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "cells.cc");
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.Write(cache, Fixture().Hull, Fixture().Visual);
            await cache.FlushAsync();
        }
        // ItemData.Shape is key 5: [[width, height, cells]].
        RewritePayload(path, "aetheria.hulldata", slots => ((object[])((object[])slots[5])[0])[2] = new object[] { 5, false, true, false });
        var error = Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(path));
        Assert.Contains(path, error.Message);
        Assert.IsAssignableFrom<MessagePackSerializationException>(error.InnerException);
    }

    // Decodes one record's payload of the named schema to its slot array, lets the edit change it, and stores it again.
    private static void RewritePayload(string path, string schemaName, Action<object[]> edit)
    {
        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path));
        var schemaId = snapshot.SchemaCatalog.Single(entry => entry.SchemaName == schemaName).SchemaId;
        var record = snapshot.Records.Single(candidate => candidate.SchemaId == schemaId);
        var slots = MessagePackSerializer.Deserialize<object[]>(record.Payload);
        edit(slots);
        record.Payload = MessagePackSerializer.Serialize(slots);
        File.WriteAllBytes(path, CultDocumentMessagePackSerialization.SerializeSnapshot(snapshot));
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

    private static readonly string[] AllRoles =
    {
        "map-icon", "hull-collider", "shield", "tractor", "thruster-emitter", "weapon-mount", "weapon-muzzle", "radiator-mesh", "articulation"
    };

    // The fixture with its thruster swapped for one hardpoint of the given type at mount id "mount", and no anchor for it.
    private static ShipParts WithOnly(HardpointType type)
    {
        var ship = Fixture();
        ship.Hull.Hardpoints.Clear();
        ship.Visual.Anchors.RemoveAll(anchor => anchor.Id == "thruster.port");
        ship.Hull.Hardpoints.Add(new HardpointData { Type = type, Position = new int2(0, 0), Shape = new Shape(), Transform = "mount" });
        return ship;
    }

    private static string Refusal(ShipParts ship) => Assert.Throws<InvalidOperationException>(ship.Validate).Message;

    // Mount anchors exist only for hardpoints that show on the model: weapons, radiators and thrusters.
    [Theory]
    [InlineData(HardpointType.Hull), InlineData(HardpointType.Tool), InlineData(HardpointType.Thermal), InlineData(HardpointType.WarpDrive),
     InlineData(HardpointType.Reactor), InlineData(HardpointType.Shield), InlineData(HardpointType.Sensors),
     InlineData(HardpointType.ControlModule), InlineData(HardpointType.AetherDrive)]
    public void AnInternalHardpointCarriesNoAnchor(HardpointType type)
    {
        WithOnly(type).Validate();

        // Any anchor taking the mount's id is refused, a mount role or not. (The four structural roles are one-of-a-kind,
        // so they are covered by renaming the existing anchor.)
        foreach (var role in AllRoles)
        {
            var ship = WithOnly(type);
            var existing = ship.Visual.Anchors.FirstOrDefault(anchor => anchor.Role == role);
            if (existing != null) existing.Id = "mount";
            else ship.Visual.Anchors.Add(new ShipAnchor { Id = "mount", Role = role, ModelNodeId = "mount" });
            Assert.Contains($"mod.skiff: {type} hardpoint mount is internal, so no anchor may take its id", Refusal(ship));
        }
    }

    [Theory]
    [InlineData(HardpointType.Thruster, "thruster-emitter")]
    [InlineData(HardpointType.Radiator, "radiator-mesh")]
    [InlineData(HardpointType.Energy, "weapon-mount")]
    [InlineData(HardpointType.Ballistic, "weapon-mount")]
    [InlineData(HardpointType.Launcher, "weapon-mount")]
    public void EachVisibleHardpointNeedsItsRoleAnchor(HardpointType type, string wanted)
    {
        ShipParts Rig()
        {
            var ship = WithOnly(type);
            if (ShipAuthoringStore.IsWeapon(type))
                ship.Visual.Anchors.Add(new ShipAnchor { Id = "mount.muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "mount" });
            return ship;
        }

        Assert.Contains($"mod.skiff: {type} hardpoint mount has no model anchor", Refusal(Rig()));

        // A structural anchor (the map icon, the collider...) whose id is the mount, or a mount anchor of the wrong role.
        foreach (var role in AllRoles.Where(role => role != wanted))
        {
            var ship = Rig();
            var existing = ship.Visual.Anchors.FirstOrDefault(anchor => anchor.Role == role);
            if (existing != null) existing.Id = "mount";
            else ship.Visual.Anchors.Add(new ShipAnchor { Id = "mount", Role = role, ModelNodeId = "mount" });
            Assert.Contains($"mod.skiff: {type} hardpoint mount needs a {wanted} anchor of that id", Refusal(ship));
        }

        var valid = Rig();
        valid.Visual.Anchors.Add(new ShipAnchor { Id = "mount", Role = wanted, ModelNodeId = "mount" });
        valid.Validate();
    }

    // Called on Validate itself: a mount-role anchor is the mount of a hardpoint of its own type, never a loose node and
    // never another type's mount.
    [Theory]
    [InlineData("thruster-emitter", HardpointType.Radiator)]
    [InlineData("thruster-emitter", HardpointType.Energy)]
    [InlineData("thruster-emitter", HardpointType.Reactor)]
    [InlineData("radiator-mesh", HardpointType.Thruster)]
    [InlineData("radiator-mesh", HardpointType.Launcher)]
    [InlineData("radiator-mesh", HardpointType.Sensors)]
    [InlineData("weapon-mount", HardpointType.Thruster)]
    [InlineData("weapon-mount", HardpointType.Radiator)]
    [InlineData("weapon-mount", HardpointType.Tool)]
    public void AMountRoleAnchorMustBeAMountOfItsType(string role, HardpointType otherType)
    {
        string Refused(ShipParts ship) =>
            Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Validate(ship.Hull, ship.Visual)).Message;

        var loose = Fixture();
        loose.Visual.Anchors.Add(new ShipAnchor { Id = "loose", Role = role, ModelNodeId = "loose" });
        Assert.Contains($"mod.skiff: {role} anchor loose must be the mount of a hardpoint of its type", Refused(loose));

        // The anchor takes the id of a hardpoint of another type; whichever side the validator reads first, it refuses.
        var other = WithOnly(otherType);
        other.Visual.Anchors.Add(new ShipAnchor { Id = "mount", Role = role, ModelNodeId = "mount" });
        if (ShipAuthoringStore.IsWeapon(otherType))
            other.Visual.Anchors.Add(new ShipAnchor { Id = "mount.muzzle", Role = "weapon-muzzle", ModelNodeId = "muzzle", ParentId = "mount" });
        Assert.Contains($"mod.skiff: {otherType} hardpoint mount ", Refused(other));
    }

    [Fact]
    public void MuzzleRulesBelongToTheValidator()
    {
        ShipParts Armed()
        {
            var ship = Fixture();
            ship.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Energy, Position = new int2(0, 0), Shape = new Shape(), Transform = "gun" });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun", Role = "weapon-mount", ModelNodeId = "gun" });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.b", Role = "weapon-muzzle", ModelNodeId = "gun-b", ParentId = "gun", Order = 0 });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.a", Role = "weapon-muzzle", ModelNodeId = "gun-a", ParentId = "gun", Order = 0 });
            ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.z", Role = "weapon-muzzle", ModelNodeId = "gun-z", ParentId = "gun", Order = -1 });
            return ship;
        }

        var orphan = Armed();
        orphan.Visual.Anchors.Single(anchor => anchor.Id == "gun.z").ParentId = null;
        Assert.Contains("mod.skiff: muzzle gun.z must be parented to a weapon hardpoint's mount", Refusal(orphan));

        var onThruster = Armed();
        onThruster.Visual.Anchors.Single(anchor => anchor.Id == "gun.z").ParentId = "thruster.port";
        Assert.Contains("mod.skiff: muzzle gun.z must be parented to a weapon hardpoint's mount", Refusal(onThruster));

        var unarmed = Armed();
        unarmed.Visual.Anchors.RemoveAll(anchor => anchor.Role == "weapon-muzzle");
        Assert.Contains("mod.skiff: weapon hardpoint gun needs at least one muzzle anchor", Refusal(unarmed));

        // Each weapon needs a muzzle of its own: another weapon's muzzle does not arm it.
        var second = Armed();
        second.Hull.Shape.Cells[0, 1] = true;
        second.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Launcher, Position = new int2(0, 1), Shape = new Shape(), Transform = "tube" });
        second.Visual.Anchors.Add(new ShipAnchor { Id = "tube", Role = "weapon-mount", ModelNodeId = "tube" });
        Assert.Contains("mod.skiff: weapon hardpoint tube needs at least one muzzle anchor", Refusal(second));

        // A valid pair plans without a refusal of the plan's own; muzzles by Order, then id ordinally.
        var valid = Armed();
        var (mount, muzzles) = Assert.Single(ShipModPlan.Build(valid.Hull, valid.Visual).Weapons);
        Assert.Equal("gun", mount);
        Assert.Equal(new[] { "gun.z", "gun.a", "gun.b" }, muzzles);
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
