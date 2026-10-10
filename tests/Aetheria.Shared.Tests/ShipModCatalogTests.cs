using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameCult.Caching;
using Newtonsoft.Json.Linq;
using Xunit;

// Compose, ReadPackage and ReadNodeIds: the derived catalog a game would open, and the GLB node ids its anchors bind to.
public sealed class ShipModCatalogTests : IDisposable
{
    private readonly TempDirectory _directory = new TempDirectory();
    private string Shipped => Path.Combine(_directory.Path, "Aetheria.cc");
    private string Derived => Path.Combine(_directory.Path, "Derived", "Aetheria.modded.cc");
    private string Mods => Path.Combine(_directory.Path, "Mods");
    private static readonly string[] FixtureNodes = ShipFixture.Nodes;

    public ShipModCatalogTests()
    {
        Directory.CreateDirectory(Mods);
        using var cache = AetheriaStores.Open(Shipped, catalogWritable: true);
        // The test assembly registers TestCatalogGlobal (AetheriaStoresTests), so any populated catalog must hold one.
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(new HullData { Name = "Wasp", Shape = ShipAuthoringTests.Fixture().Hull.Shape });
        cache.Upsert(new GearData { Name = "Widget" });
        cache.FlushAsync().Wait();
    }

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void ComposeDerivesACatalogAndLeavesTheShippedOneUntouched()
    {
        WritePackage("mod.skiff");
        Directory.CreateDirectory(Path.Combine(Mods, "notes"));
        var shippedBefore = Hash(Shipped);

        Assert.Equal(1, ShipModCatalog.Compose(Shipped, Derived, Mods).Included.Length);

        Assert.Equal(shippedBefore, Hash(Shipped));
        using var cache = AetheriaStores.Open(Derived);
        Assert.Contains("Wasp", cache.GetAll<HullData>().Select(hull => hull.Name));
        Assert.Contains("Widget", cache.GetAll<GearData>().Select(gear => gear.Name));
        var records = cache.AllStoredDocuments.ToDictionary(record => record.Key.Value, record => record.Document);
        Assert.Equal("Skiff", Assert.IsType<HullData>(records[ShipModCatalog.HullKey("mod.skiff").Value]).Name);
        Assert.Equal("mod.skiff", Assert.IsType<ShipAuthoring>(records[ShipModCatalog.AuthoringKey("mod.skiff").Value]).Id);
        AssertOnlyTheDerivedFile();
    }

    // Correction 2 of the S0 map: the derived catalog held each mod hull twice, once as a HullData and once inside the
    // ship's authoring record. The hull is now one record, and the visual holds no hull.
    [Fact]
    public void ComposedCatalogHoldsOneHullPerModShip()
    {
        WritePackage("mod.skiff", hullName: "Skiffhull");
        ShipModCatalog.Compose(Shipped, Derived, Mods);

        using var cache = AetheriaStores.Open(Derived);
        Assert.Equal(1, cache.GetAll<HullData>().Count(hull => hull.Name == "Skiffhull"));
        // The hull's name is stored exactly once in the derived file, so no second copy of the hull hides in another record.
        Assert.Equal(1, Occurrences(File.ReadAllBytes(Derived), Encoding.UTF8.GetBytes("Skiffhull")));
        var visual = cache.GetAll<ShipAuthoring>().Single();
        Assert.Equal("mod.skiff", visual.Id);
    }

    [Fact]
    public void ComposeRefusesAShippedHullThatNamesTwoBodies()
    {
        WritePackage("mod.skiff");
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            cache.Upsert(new HullData
            {
                Name = "Chimera",
                Shape = ShipAuthoringTests.Fixture().Hull.Shape,
                Prefab = "Djinni",
                Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey("mod.skiff"))
            });
            cache.FlushAsync().Wait();
        }
        Assert.Contains("Chimera: a hull names one body",
            Assert.Throws<InvalidOperationException>(() => ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.False(File.Exists(Derived));
    }

    [Fact]
    public void ComposeCopiesTheModStoresRecordsUnchangedAndTheHullNamesItsVisualByRef()
    {
        WritePackage("mod.skiff");
        var (modHull, modVisual) = ShipAuthoringStore.Read(Path.Combine(Mods, "mod.skiff", "ship.cc"));
        ShipModCatalog.Compose(Shipped, Derived, Mods);

        using var cache = AetheriaStores.Open(Derived);
        var hull = cache.Get<HullData>(ShipModCatalog.HullKey("mod.skiff"));
        var visual = cache.Get<ShipAuthoring>(ShipModCatalog.AuthoringKey("mod.skiff"));
        Assert.Same(visual, cache.Get(hull.Visual));
        var options = GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.OptionsFor(typeof(ShipAuthoring).Assembly);
        Assert.Equal(MessagePack.MessagePackSerializer.Serialize(modHull, options), MessagePack.MessagePackSerializer.Serialize(hull, options));
        Assert.Equal(MessagePack.MessagePackSerializer.Serialize(modVisual, options), MessagePack.MessagePackSerializer.Serialize(visual, options));
    }

    // The package the Unity smoke (ShipModPreview.Smoke) loads. Set AETHERIA_SHIP_FIXTURE_DIR to have this run leave it
    // there: `-shipModPath $AETHERIA_SHIP_FIXTURE_DIR/mod.skiff/ship.cc`.
    [Fact]
    public void TheFixturePackageIsCompleteAndItsGlbCarriesAScene()
    {
        var directory = ShipFixture.WritePackage(Mods, "mod.skiff");

        var package = ShipModCatalog.ReadPackage(Path.Combine(directory, "ship.cc"));
        Assert.Equal(ShipFixture.Nodes.Length, package.NodeIndices.Count);
        var glb = File.ReadAllBytes(package.ModelPath);
        var json = JObject.Parse(Encoding.UTF8.GetString(glb, 20, (int)BitConverter.ToUInt32(glb, 12)));
        Assert.Equal(0, (int)json["scene"]);
        Assert.Equal(Enumerable.Range(0, ShipFixture.Nodes.Length), json["scenes"][0]["nodes"].Select(node => (int)node));
        Assert.Equal(1, ShipModCatalog.Compose(Shipped, Derived, Mods).Included.Length);

        var export = Environment.GetEnvironmentVariable("AETHERIA_SHIP_FIXTURE_DIR");
        if (string.IsNullOrEmpty(export)) return;
        var target = Path.Combine(export, "mod.skiff");
        Directory.CreateDirectory(target);
        foreach (var name in new[] { "ship.cc", "skiff.glb" }) File.Copy(Path.Combine(directory, name), Path.Combine(target, name), true);
    }

    [Fact]
    public void TheFixtureGlbCarriesARealMeshOnEveryAnchorTheAssemblerReadsOneFrom()
    {
        var package = ShipModCatalog.ReadPackage(Path.Combine(ShipFixture.WritePackage(Mods, "mod.skiff"), "ship.cc"));
        var glb = File.ReadAllBytes(package.ModelPath);
        var jsonLength = (int)BitConverter.ToUInt32(glb, 12);
        var json = JObject.Parse(Encoding.UTF8.GetString(glb, 20, jsonLength));
        var binLength = (int)BitConverter.ToUInt32(glb, 20 + jsonLength);
        Assert.Equal(0x004E4942u, BitConverter.ToUInt32(glb, 24 + jsonLength));
        Assert.Equal(glb.Length, 28 + jsonLength + binLength);
        Assert.Equal((int)json["buffers"][0]["byteLength"], ShipFixture.MeshBytes().Length);
        Assert.True(binLength >= ShipFixture.MeshBytes().Length);

        var roles = new[] { "map-icon", "hull-collider", "thruster-emitter", "radiator-mesh" };
        var needed = package.Visual.Anchors.Where(anchor => roles.Contains(anchor.Role)).ToArray();
        Assert.Contains(needed, anchor => anchor.Role == "thruster-emitter");
        foreach (var anchor in needed)
        {
            var node = json["nodes"][(int)package.NodeIndices[anchor.ModelNodeId]];
            var mesh = json["meshes"][(int)node["mesh"]];
            var accessor = json["accessors"][(int)mesh["primitives"][0]["attributes"]["POSITION"]];
            Assert.True((int)accessor["count"] >= 4, anchor.Id);
        }
        Assert.Contains(package.Visual.Anchors, anchor => anchor.Role == "weapon-muzzle" && anchor.ParentId == "gun");
        Assert.Contains(package.Hull.Hardpoints, hardpoint => hardpoint.Type == HardpointType.Energy && hardpoint.Transform == "gun");
    }

    [Fact]
    public void ComposeCountsEveryPackageAndReplacesAnExistingDerivedCatalog()
    {
        WritePackage("mod.skiff");
        Assert.Equal(1, ShipModCatalog.Compose(Shipped, Derived, Mods).Included.Length);
        WritePackage("mod.barge", hullName: "Barge");

        Assert.Equal(2, ShipModCatalog.Compose(Shipped, Derived, Mods).Included.Length);

        using var cache = AetheriaStores.Open(Derived);
        Assert.Equal(new[] { "Barge", "Skiff", "Wasp" }, cache.GetAll<HullData>().Select(hull => hull.Name).OrderBy(name => name));
        AssertOnlyTheDerivedFile();
    }

    // One bad package never stops the game: it is left out and named, and the rest are composed.
    [Fact]
    public void ABadPackageIsExcludedAndNamedWhileTheGoodOnesCompose()
    {
        WritePackage("mod.good", hullName: "Good");
        WritePackage("mod.nomodel", hullName: "Nomodel", nodes: FixtureNodes.Where(node => node != "shield").ToArray());
        WritePackage("mod.nofile", hullName: "Nofile");
        File.Delete(Path.Combine(Mods, "mod.nofile", "skiff.glb"));
        Directory.CreateDirectory(Path.Combine(Mods, "mod.garbage"));
        File.WriteAllText(Path.Combine(Mods, "mod.garbage", "ship.cc"), "not a ship file");

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.good" }, composition.Included);
        Assert.Equal(new[] { "mod.garbage", "mod.nofile", "mod.nomodel" }, composition.Excluded.Select(exclusion => exclusion.Package));
        Assert.Contains("model has no node with aetheria.id=shield", composition.Excluded.Single(e => e.Package == "mod.nomodel").Reason);
        Assert.Contains("must name an existing GLB", composition.Excluded.Single(e => e.Package == "mod.nofile").Reason);
        Assert.Contains("mod.nofile: ", composition.Excluded.Single(e => e.Package == "mod.nofile").ToString());
        using (var cache = AetheriaStores.Open(Derived))
            Assert.Equal(new[] { "Good", "Wasp" }, cache.GetAll<HullData>().Select(hull => hull.Name).OrderBy(name => name));
        AssertOnlyTheDerivedFile();
    }

    // A name collision leaves out every package that claims the name; the verdict does not depend on directory order.
    [Fact]
    public void ACollisionExcludesEveryPackageInvolvedAndNoneOfTheRest()
    {
        WritePackage("mod.a", hullName: "Skiff");
        WritePackage("mod.b", hullName: "Skiff");
        WritePackage("mod.c", hullName: "Skiff");
        WritePackage("mod.d", hullName: "Wasp");
        WritePackage("mod.e", hullName: "Fine");

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.e" }, composition.Included);
        Assert.Equal(new[] { "mod.a", "mod.b", "mod.c", "mod.d" }, composition.Excluded.Select(exclusion => exclusion.Package));
        Assert.Equal("hull name 'Skiff' is also claimed by mod.b, mod.c.", composition.Excluded[0].Reason);
        Assert.Equal("hull name 'Skiff' is also claimed by mod.a, mod.c.", composition.Excluded[1].Reason);
        Assert.Equal("hull name 'Wasp' collides with an existing catalog item.", composition.Excluded[3].Reason);
        using var cache = AetheriaStores.Open(Derived);
        Assert.Equal(new[] { "Fine", "Wasp" }, cache.GetAll<HullData>().Select(hull => hull.Name).OrderBy(name => name));
    }

    // A package that fails for two reasons names both.
    [Fact]
    public void APackageThatCollidesTwiceNamesBothReasons()
    {
        WritePackage("mod.a", hullName: "Wasp");
        WritePackage("mod.b", hullName: "Wasp");

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Empty(composition.Included);
        Assert.Equal("hull name 'Wasp' collides with an existing catalog item.; hull name 'Wasp' is also claimed by mod.b.",
            composition.Excluded[0].Reason);
    }

    [Theory]
    [InlineData("mod-hull:mod.skiff", false)]
    [InlineData("mod-ship:mod.skiff", true)]
    public void APackageWhoseKeyAlreadyExistsInTheCatalogIsExcluded(string key, bool authoring)
    {
        WritePackage("mod.skiff");
        WritePackage("mod.other", hullName: "Other");
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            if (authoring)
            {
                var stranger = ShipAuthoringTests.Fixture().WithId("stranger").Visual;
                cache.UpsertAsync(typeof(ShipAuthoring), stranger, new CultRecordKey(key)).GetAwaiter().GetResult();
            }
            else
                cache.UpsertAsync(typeof(HullData), new HullData { Name = "Stranger", Shape = ShipAuthoringTests.Fixture().Hull.Shape },
                    new CultRecordKey(key)).GetAwaiter().GetResult();
            cache.FlushAsync().Wait();
        }

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.other" }, composition.Included);
        Assert.Contains("a mod key collides", Assert.Single(composition.Excluded).Reason);
        Assert.Equal("mod.skiff", composition.Excluded[0].Package);
    }

    [Theory]
    [InlineData(typeof(HullData))]
    [InlineData(typeof(GearData))]
    public void APackageWithAHullNamedLikeAShippedItemIsExcluded(Type shippedType)
    {
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            if (shippedType == typeof(HullData))
                cache.Upsert(new HullData { Name = "Skiff", Shape = ShipAuthoringTests.Fixture().Hull.Shape });
            else
                cache.Upsert(new GearData { Name = "Skiff" });
            cache.FlushAsync().Wait();
        }
        WritePackage("mod.skiff");

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Empty(composition.Included);
        Assert.Equal("hull name 'Skiff' collides with an existing catalog item.", Assert.Single(composition.Excluded).Reason);
    }

    [Fact]
    public void APackageWhoseShipIdAShippedAuthoringRecordAlreadyUsesIsExcluded()
    {
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            cache.UpsertAsync(typeof(ShipAuthoring), ShipAuthoringTests.Fixture().Visual, new CultRecordKey("shipped:skiff")).GetAwaiter().GetResult();
            cache.FlushAsync().Wait();
        }
        WritePackage("mod.skiff", hullName: "Different");

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Empty(composition.Included);
        Assert.Equal("ship ID collides with an existing catalog ship authoring record.", Assert.Single(composition.Excluded).Reason);
    }

    // The game's boot: a bad package is named and the rest boot; with none left, the shipped catalog boots.
    [Fact]
    public void TheBootCatalogSurvivesBadPackages()
    {
        WritePackage("mod.good", hullName: "Good");
        WritePackage("mod.bad", hullName: "Bad", nodes: FixtureNodes.Where(node => node != "shield").ToArray());

        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods);
        Assert.Equal(Derived, catalog);
        Assert.Equal("mod.bad", Assert.Single(excluded).Package);

        Directory.Delete(Path.Combine(Mods, "mod.good"), true);
        (catalog, excluded) = ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods);
        Assert.Equal(Shipped, catalog);
        Assert.Equal("mod.bad", Assert.Single(excluded).Package);
    }

    public void ComposeRefusesUnsafeInputsAndOutputs()
    {
        WritePackage("mod.skiff");
        Assert.Contains("Shipped catalog is missing", Assert.Throws<FileNotFoundException>(() =>
            ShipModCatalog.Compose(Path.Combine(_directory.Path, "absent.cc"), Derived, Mods)).Message);
        Assert.Contains("Mod root is missing", Assert.Throws<DirectoryNotFoundException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Path.Combine(_directory.Path, "NoMods"))).Message);
        var before = Hash(Shipped);
        Assert.Contains("cannot replace the shipped catalog", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Shipped, Mods)).Message);
        Assert.Contains("cannot replace the shipped catalog", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Shipped.ToUpperInvariant().Replace(_directory.Path.ToUpperInvariant(), _directory.Path), Mods)).Message);
        Assert.Contains("cannot replace a mod source or asset", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Path.Combine(Mods, "mod.skiff", "out.cc"), Mods)).Message);
        Assert.Equal(before, Hash(Shipped));
        Assert.False(File.Exists(Path.Combine(Mods, "mod.skiff", "out.cc")));
    }

    [Fact]
    public void ReadPackageBindsTheShipToItsDirectoryAndGlb()
    {
        WritePackage("mod.skiff");
        var package = ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"));
        Assert.Equal("mod.skiff", package.Visual.Id);
        Assert.Equal(Path.GetFullPath(Path.Combine(Mods, "mod.skiff", "skiff.glb")), package.ModelPath);
        Assert.Equal(new Dictionary<string, uint> { ["map"] = 0, ["collider"] = 1, ["shield"] = 2, ["tractor"] = 3, ["thruster-port"] = 4, ["gun"] = 5, ["gun-muzzle"] = 6 }, package.NodeIndices);
    }

    [Fact]
    public void ReadPackageRefusesADirectoryNameThatIsNotTheShipId()
    {
        WritePackage("mod.skiff");
        Directory.Move(Path.Combine(Mods, "mod.skiff"), Path.Combine(Mods, "renamed"));
        Assert.Contains("ship ID must match its package directory name", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "renamed", "ship.cc"))).Message);
    }

    [Fact]
    public void ReadPackageRefusesAMissingOrWronglyNamedModel()
    {
        WritePackage("mod.skiff");
        File.Delete(Path.Combine(Mods, "mod.skiff", "skiff.glb"));
        Assert.Contains("must name an existing GLB inside its package", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);

        Directory.Delete(Path.Combine(Mods, "mod.skiff"), true);
        WritePackage("mod.skiff", modelAsset: "skiff.bin");
        Assert.Contains("must name an existing GLB inside its package", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);
    }

    [Fact]
    public void ReadPackageAppliesTheCatalogsItemValidationToTheHull()
    {
        WritePackage("mod.skiff", tweak: ship => ship.Hull.MinimumTemperature = ship.Hull.MaximumTemperature);
        Assert.Contains("zero-span range", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);
    }

    [Fact]
    public void ReadPackageRefusesAnAnchorWhoseNodeIsNotInTheModel()
    {
        WritePackage("mod.skiff", nodes: FixtureNodes.Where(node => node != "thruster-port").ToArray());
        Assert.Contains("model has no node with aetheria.id=thruster-port for anchor thruster.port", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);
    }

    // Ruling thrusters-radiators-are-meshes: a thruster's mesh is its emission surface and a radiator's is what glows, so
    // either on an empty node is refused before compose; a weapon mount (and shield, tractor) may be an empty.
    [Fact]
    public void AMeshRoleAnchorOnAnEmptyNodeIsRefused()
    {
        var withoutThrusterMesh = ShipFixture.MeshNodes.Where(node => node != "thruster-port").ToArray();
        WritePackage("mod.skiff", meshNodes: withoutThrusterMesh);
        Assert.Contains("thruster-emitter anchor thruster.port needs a mesh, but model node thruster-port has none", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);

        Directory.Delete(Path.Combine(Mods, "mod.skiff"), true);
        void AsRadiator(ShipParts ship)
        {
            ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port").Type = HardpointType.Radiator;
            ship.Visual.Anchors.Single(anchor => anchor.Id == "thruster.port").Role = "radiator-mesh";
        }
        WritePackage("mod.skiff", tweak: AsRadiator, meshNodes: withoutThrusterMesh);
        Assert.Contains("radiator-mesh anchor thruster.port needs a mesh", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc"))).Message);

        // The same radiator on a mesh node, and the weapon mount, shield and tractor on empty nodes, are accepted.
        Directory.Delete(Path.Combine(Mods, "mod.skiff"), true);
        WritePackage("mod.skiff", tweak: AsRadiator);
        var nodes = ShipModCatalog.ReadNodeIds(Path.Combine(Mods, "mod.skiff", "skiff.glb"));
        Assert.False(nodes["gun"].HasMesh || nodes["shield"].HasMesh || nodes["tractor"].HasMesh);
        Assert.Equal("mod.skiff", ShipModCatalog.ReadPackage(Path.Combine(Mods, "mod.skiff", "ship.cc")).Visual.Id);
    }

    // AetherDb's validate judges the GLB once it exists, so Blender's Package sees what Compose would refuse.
    [Fact]
    public void ValidateCommandReadsTheGlbWhenPresent()
    {
        WritePackage("mod.skiff", nodes: FixtureNodes.Where(node => node != "thruster-port").ToArray());
        var path = Path.Combine(Mods, "mod.skiff", "ship.cc");
        var (code, output, error) = Run("validate", path);
        Assert.Equal(1, code);
        Assert.Contains("model has no node with aetheria.id=thruster-port for anchor thruster.port", error);

        File.Delete(Path.Combine(Mods, "mod.skiff", "skiff.glb"));
        (code, output, error) = Run("validate", path);
        Assert.Equal(0, code);
        Assert.Contains("records only", output);

        WritePackage("mod.good");
        (code, output, _) = Run("validate", Path.Combine(Mods, "mod.good", "ship.cc"));
        Assert.Equal(0, code);
        Assert.Contains("records and model", output);
    }

    [Theory]
    [InlineData("con"), InlineData("nul.x"), InlineData("abc.")]
    public void CreateRefusesABadIdBeforeTouchingTheDisk(string id)
    {
        var target = Path.Combine(_directory.Path, "GameData", "Mods", id, "ship.cc");
        var (code, output, error) = Run("create", target, id, "Probe", "--like", "Djinni");
        Assert.Equal(1, code);
        Assert.Contains("Ship ID", error);
        Assert.False(Directory.Exists(Path.Combine(_directory.Path, "GameData")));
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var (previousOut, previousError) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try { return (ShipAuthoringCommands.Run(args), output.ToString(), error.ToString()); }
        finally { Console.SetOut(previousOut); Console.SetError(previousError); }
    }

    [Fact]
    public void NodeIdsMapToTheirGlbNodeIndexSkippingNodesWithoutOne()
    {
        var path = Write("nodes.glb", ShipFixture.Glb(@"{""asset"":{""version"":""2.0""},""nodes"":[
            {""name"":""plain""},{""extras"":{""aetheria.id"":""a""}},{""extras"":{}},{""extras"":{""aetheria.id"":""""}},{""mesh"":0,""extras"":{""aetheria.id"":""b""}}]}"));
        Assert.Equal(new Dictionary<string, (uint, bool)> { ["a"] = (1, false), ["b"] = (4, true) }, ShipModCatalog.ReadNodeIds(path));
    }

    [Fact]
    public void AGlbWithoutNodesHasNoIds() =>
        Assert.Empty(ShipModCatalog.ReadNodeIds(Write("empty.glb", ShipFixture.Glb(@"{""asset"":{""version"":""2.0""}}"))));

    [Fact]
    public void ADuplicateNodeIdIsRefused() =>
        Assert.Contains("duplicate GLB node aetheria.id=a", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadNodeIds(Write("dup.glb", ShipFixture.Glb(@"{""nodes"":[{""extras"":{""aetheria.id"":""a""}},{""extras"":{""aetheria.id"":""a""}}]}")))).Message);

    [Fact]
    public void AMalformedGlbHeaderIsRefused()
    {
        var good = ShipFixture.Glb(@"{""nodes"":[]}");
        void Refuse(string name, byte[] bytes, string message) =>
            Assert.Contains(message, Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadNodeIds(Write(name, bytes))).Message);

        Refuse("short.glb", With(good.Take(19).ToArray(), 8, 19), "invalid GLB 2 header");
        Refuse("magic.glb", With(good, 0, 0x46546C68), "invalid GLB 2 header");
        Refuse("version.glb", With(good, 4, 1), "invalid GLB 2 header");
        Refuse("longer.glb", With(good, 8, (uint)good.Length + 1), "invalid GLB 2 header");
        Refuse("shorter.glb", With(good, 8, (uint)good.Length - 4), "invalid GLB 2 header");
        Refuse("chunktype.glb", With(good, 16, 0x004E4942), "missing GLB JSON chunk");
        Refuse("chunklength.glb", With(good, 12, (uint)good.Length), "missing GLB JSON chunk");
    }

    [Fact]
    public void AGlbWhoseJsonChunkFillsTheFileIsAccepted()
    {
        var json = Encoding.UTF8.GetBytes(@"{""nodes"":[{""extras"":{""aetheria.id"":""a""}}]}   ");
        var bytes = new byte[20 + json.Length];
        BitConverter.GetBytes(0x46546C67u).CopyTo(bytes, 0);
        BitConverter.GetBytes(2u).CopyTo(bytes, 4);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 8);
        BitConverter.GetBytes((uint)json.Length).CopyTo(bytes, 12);
        BitConverter.GetBytes(0x4E4F534Au).CopyTo(bytes, 16);
        json.CopyTo(bytes, 20);
        Assert.Equal(new Dictionary<string, uint> { ["a"] = 0 }, Indices(ShipModCatalog.ReadNodeIds(Write("full.glb", bytes))));
    }

    // A run store references a mod hull by its key, so the same packages must derive the same records on every launch,
    // whatever order the directories were created in.
    [Fact]
    public void ComposeAtBootIsDeterministic()
    {
        var ids = new[] { "mod.a", "mod.b", "mod.c", "mod.d", "mod.e", "mod.f" };
        foreach (var id in ids.Reverse()) WritePackage(id, hullName: "Hull " + id);
        var firstMods = Mods;
        var secondMods = Path.Combine(_directory.Path, "Mods2");
        foreach (var id in ids) ShipFixture.WritePackage(secondMods, id, "Hull " + id);
        var second = Path.Combine(_directory.Path, "Derived2", "Aetheria.modded.cc");

        ShipModCatalog.Compose(Shipped, Derived, firstMods);
        ShipModCatalog.Compose(Shipped, second, secondMods);
        ShipModCatalog.Compose(Shipped, Path.Combine(_directory.Path, "Derived3", "Aetheria.modded.cc"), firstMods);

        Assert.Equal(Records(Derived), Records(second));
        Assert.Equal(Records(Derived), Records(Path.Combine(_directory.Path, "Derived3", "Aetheria.modded.cc")));
        Assert.Contains(ShipModCatalog.HullKey("mod.c").Value, Records(Derived).Keys);
    }

    [Fact]
    public void TheBootCatalogIsTheShippedOneUntilAModIsInstalled()
    {
        Assert.Equal(Shipped, ShipModCatalog.ResolveCatalog(Shipped, Derived, Path.Combine(_directory.Path, "absent")).Catalog);
        Directory.CreateDirectory(Path.Combine(Mods, "notes"));
        Assert.Equal(Shipped, ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods).Catalog);
        Assert.False(File.Exists(Derived));

        WritePackage("mod.skiff");
        Assert.Equal(Derived, ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods).Catalog);
        using (var cache = AetheriaStores.Open(Derived))
            Assert.Contains("Skiff", cache.GetAll<HullData>().Select(hull => hull.Name));

        // Recomposed on every call: a mod removed since the last boot is gone from the file the game opens next.
        Directory.Delete(Path.Combine(Mods, "mod.skiff"), true);
        Assert.Equal(Shipped, ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods).Catalog);
        WritePackage("mod.barge", hullName: "Barge");
        Assert.Equal(Derived, ShipModCatalog.ResolveCatalog(Shipped, Derived, Mods).Catalog);
        using (var cache = AetheriaStores.Open(Derived))
            Assert.Equal(new[] { "Barge", "Wasp" }, cache.GetAll<HullData>().Select(hull => hull.Name).OrderBy(name => name));
    }

    // A failure of the composition itself is a mod problem too: the game boots on the shipped catalog and names it.
    [Fact]
    public void TheBootCatalogSurvivesAComposeThatFails()
    {
        WritePackage("mod.skiff");
        var blocker = Path.Combine(_directory.Path, "blocker");
        File.WriteAllText(blocker, "a file where the derived catalog's directory should be");

        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(Shipped, Path.Combine(blocker, "Aetheria.modded.cc"), Mods);

        Assert.Equal(Shipped, catalog);
        var exclusion = Assert.Single(excluded);
        Assert.Equal("(all mods)", exclusion.Package);
        Assert.False(string.IsNullOrWhiteSpace(exclusion.Reason));
        // An invalid output (the shipped catalog itself) is refused the same way, with its reason.
        (catalog, excluded) = ShipModCatalog.ResolveCatalog(Shipped, Shipped, Mods);
        Assert.Equal(Shipped, catalog);
        Assert.Contains("cannot replace the shipped catalog", Assert.Single(excluded).Reason);
    }

    // Gameplay reads a mod ship's records from the derived catalog; the package directory supplies only the GLB.
    [Fact]
    public void PackageOfTakesItsRecordsFromTheCatalogAndOnlyTheGlbFromTheMod()
    {
        WritePackage("mod.skiff");
        ShipModCatalog.Compose(Shipped, Derived, Mods);
        File.Delete(Path.Combine(Mods, "mod.skiff", "ship.cc"));

        using var cache = AetheriaStores.Open(Derived);
        var hull = cache.GetAll<HullData>().Single(candidate => candidate.Name == "Skiff");
        var package = ShipModCatalog.PackageOf(cache, hull, Mods);

        Assert.Same(hull, package.Hull);
        Assert.Same(cache.Get(hull.Visual), package.Visual);
        Assert.Equal(Path.Combine(Mods, "mod.skiff", "skiff.glb"), package.ModelPath);
        Assert.Equal(ShipFixture.Nodes.Length, package.NodeIndices.Count);

        // The shared validator judges the catalog's records too, not only a ship.cc.
        hull.Prefab = "Djinni";
        Assert.Contains("names both a Unity prefab and a visual record", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.PackageOf(cache, hull, Mods)).Message);
        hull.Prefab = null;

        // A hull whose visual the catalog does not hold, and a mod whose GLB is gone, are refused.
        Assert.Contains("holds no visual record", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.PackageOf(cache, new HullData { Name = "Ghost" }, Mods)).Message);
        File.Delete(package.ModelPath);
        Assert.Contains("model asset must name an existing GLB", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.PackageOf(cache, hull, Mods)).Message);
    }

    // Every stored record's key, schema and payload bytes. StoredAt is a write timestamp, not record content.
    private static Dictionary<string, string> Records(string path) =>
        GameCult.Caching.MessagePack.CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path)).Records
            .ToDictionary(record => record.Key, record => record.SchemaId + ":" + Convert.ToHexString(record.Payload));

    private void AssertOnlyTheDerivedFile()
    {
        var names = Directory.GetFileSystemEntries(Path.GetDirectoryName(Derived)).Select(entry => Path.GetFileName(entry)).ToArray();
        Assert.True(names.Length == 1 && names[0] == "Aetheria.modded.cc", string.Join(" | ", names));
    }

    private static byte[] With(byte[] bytes, int offset, uint value)
    {
        var copy = (byte[])bytes.Clone();
        BitConverter.GetBytes(value).CopyTo(copy, offset);
        return copy;
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private void WritePackage(string id, string hullName = "Skiff", string[] nodes = null, string modelAsset = "skiff.glb",
        Action<ShipParts> tweak = null, string[] meshNodes = null) => ShipFixture.WritePackage(Mods, id, hullName, nodes, modelAsset, tweak, meshNodes);

    private static Dictionary<string, uint> Indices(Dictionary<string, (uint Index, bool HasMesh)> nodes) =>
        nodes.ToDictionary(node => node.Key, node => node.Value.Index);

    private static int Occurrences(byte[] haystack, byte[] needle)
    {
        var count = 0;
        for (var start = 0; start <= haystack.Length - needle.Length; start++)
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle)) count++;
        return count;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
