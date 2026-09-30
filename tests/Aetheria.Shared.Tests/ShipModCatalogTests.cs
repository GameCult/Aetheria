using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameCult.Caching;
using Xunit;

// Compose, ReadPackage and ReadNodeIds: the derived catalog a game would open, and the GLB node ids its anchors bind to.
public sealed class ShipModCatalogTests : IDisposable
{
    private readonly TempDirectory _directory = new TempDirectory();
    private string Shipped => Path.Combine(_directory.Path, "Aetheria.cc");
    private string Derived => Path.Combine(_directory.Path, "Derived", "Aetheria.modded.cc");
    private string Mods => Path.Combine(_directory.Path, "Mods");
    private static readonly string[] FixtureNodes = { "map", "collider", "shield", "tractor", "thruster-port" };

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

        Assert.Equal(1, ShipModCatalog.Compose(Shipped, Derived, Mods));

        Assert.Equal(shippedBefore, Hash(Shipped));
        using var cache = AetheriaStores.Open(Derived);
        Assert.Contains("Wasp", cache.GetAll<HullData>().Select(hull => hull.Name));
        Assert.Contains("Widget", cache.GetAll<GearData>().Select(gear => gear.Name));
        var records = cache.AllStoredDocuments.ToDictionary(record => record.Key.Value, record => record.Document);
        Assert.Equal("Skiff", Assert.IsType<HullData>(records[ShipModCatalog.HullKey("mod.skiff").Value]).Name);
        Assert.Equal("mod.skiff", Assert.IsType<ShipAuthoring>(records[ShipModCatalog.AuthoringKey("mod.skiff").Value]).Id);
        AssertOnlyTheDerivedFile();
    }

    [Fact]
    public void ComposeCountsEveryPackageAndReplacesAnExistingDerivedCatalog()
    {
        WritePackage("mod.skiff");
        Assert.Equal(1, ShipModCatalog.Compose(Shipped, Derived, Mods));
        WritePackage("mod.barge", hullName: "Barge");

        Assert.Equal(2, ShipModCatalog.Compose(Shipped, Derived, Mods));

        using var cache = AetheriaStores.Open(Derived);
        Assert.Equal(new[] { "Barge", "Skiff", "Wasp" }, cache.GetAll<HullData>().Select(hull => hull.Name).OrderBy(name => name));
        AssertOnlyTheDerivedFile();
    }

    [Fact]
    public void AFailedComposeLeavesThePreviousDerivedFileAndNoTemporaryFile()
    {
        WritePackage("mod.skiff");
        ShipModCatalog.Compose(Shipped, Derived, Mods);
        var before = File.ReadAllBytes(Derived);

        WritePackage("mod.barge", hullName: "Wasp");
        Assert.Contains("collides with an existing catalog item", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.Equal(before, File.ReadAllBytes(Derived));
        AssertOnlyTheDerivedFile();

        Directory.Delete(Path.Combine(Mods, "mod.barge"), true);
        WritePackage("mod.barge", hullName: "Barge", nodes: FixtureNodes.Where(node => node != "shield").ToArray());
        Assert.Contains("model has no node with aetheria.id=shield", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.Equal(before, File.ReadAllBytes(Derived));
    }

    [Theory]
    [InlineData("mod-hull:mod.skiff", false)]
    [InlineData("mod-ship:mod.skiff", true)]
    public void ComposeRefusesAModKeyThatAlreadyExistsInTheCatalog(string key, bool authoring)
    {
        WritePackage("mod.skiff");
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            if (authoring)
            {
                var stranger = ShipAuthoringTests.Fixture();
                stranger.Id = "stranger";
                cache.UpsertAsync(typeof(ShipAuthoring), stranger, new CultRecordKey(key)).GetAwaiter().GetResult();
            }
            else
                cache.UpsertAsync(typeof(HullData), new HullData { Name = "Stranger", Shape = ShipAuthoringTests.Fixture().Hull.Shape },
                    new CultRecordKey(key)).GetAwaiter().GetResult();
            cache.FlushAsync().Wait();
        }

        Assert.Contains("a mod key collides", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.False(File.Exists(Derived));
    }

    [Theory]
    [InlineData(typeof(HullData))]
    [InlineData(typeof(GearData))]
    public void ComposeRefusesAHullNamedLikeAShippedItem(Type shippedType)
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

        Assert.Contains("hull name 'Skiff' collides with an existing catalog item", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.False(File.Exists(Derived));
    }

    [Fact]
    public void ComposeRefusesTwoModsThatShareAHullName()
    {
        WritePackage("mod.a", hullName: "Skiff");
        WritePackage("mod.b", hullName: "Skiff");

        Assert.Contains("mod.b: hull name 'Skiff' collides", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.False(File.Exists(Derived));
    }

    [Fact]
    public void ComposeRefusesAShipIdThatAShippedAuthoringRecordAlreadyUses()
    {
        using (var cache = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            cache.UpsertAsync(typeof(ShipAuthoring), ShipAuthoringTests.Fixture(), new CultRecordKey("shipped:skiff")).GetAwaiter().GetResult();
            cache.FlushAsync().Wait();
        }
        WritePackage("mod.skiff", hullName: "Different");

        Assert.Contains("ship ID collides with an existing catalog ship authoring record", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.Compose(Shipped, Derived, Mods)).Message);
        Assert.False(File.Exists(Derived));
    }

    [Fact]
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
        Assert.Equal("mod.skiff", package.Ship.Id);
        Assert.Equal(Path.GetFullPath(Path.Combine(Mods, "mod.skiff", "skiff.glb")), package.ModelPath);
        Assert.Equal(new Dictionary<string, uint> { ["map"] = 0, ["collider"] = 1, ["shield"] = 2, ["tractor"] = 3, ["thruster-port"] = 4 }, package.NodeIndices);
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

    [Fact]
    public void NodeIdsMapToTheirGlbNodeIndexSkippingNodesWithoutOne()
    {
        var path = Write("nodes.glb", Glb(@"{""asset"":{""version"":""2.0""},""nodes"":[
            {""name"":""plain""},{""extras"":{""aetheria.id"":""a""}},{""extras"":{}},{""extras"":{""aetheria.id"":""""}},{""extras"":{""aetheria.id"":""b""}}]}"));
        Assert.Equal(new Dictionary<string, uint> { ["a"] = 1, ["b"] = 4 }, ShipModCatalog.ReadNodeIds(path));
    }

    [Fact]
    public void AGlbWithoutNodesHasNoIds() =>
        Assert.Empty(ShipModCatalog.ReadNodeIds(Write("empty.glb", Glb(@"{""asset"":{""version"":""2.0""}}"))));

    [Fact]
    public void ADuplicateNodeIdIsRefused() =>
        Assert.Contains("duplicate GLB node aetheria.id=a", Assert.Throws<InvalidOperationException>(() =>
            ShipModCatalog.ReadNodeIds(Write("dup.glb", Glb(@"{""nodes"":[{""extras"":{""aetheria.id"":""a""}},{""extras"":{""aetheria.id"":""a""}}]}")))).Message);

    [Fact]
    public void AMalformedGlbHeaderIsRefused()
    {
        var good = Glb(@"{""nodes"":[]}");
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
        Assert.Equal(new Dictionary<string, uint> { ["a"] = 0 }, ShipModCatalog.ReadNodeIds(Write("full.glb", bytes)));
    }

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

    private static byte[] Glb(string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var padded = new byte[(body.Length + 3) / 4 * 4];
        Array.Fill(padded, (byte)' ');
        body.CopyTo(padded, 0);
        var bytes = new byte[20 + padded.Length + 8];
        BitConverter.GetBytes(0x46546C67u).CopyTo(bytes, 0);
        BitConverter.GetBytes(2u).CopyTo(bytes, 4);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 8);
        BitConverter.GetBytes((uint)padded.Length).CopyTo(bytes, 12);
        BitConverter.GetBytes(0x4E4F534Au).CopyTo(bytes, 16);
        padded.CopyTo(bytes, 20);
        BitConverter.GetBytes(0u).CopyTo(bytes, 20 + padded.Length);
        BitConverter.GetBytes(0x004E4942u).CopyTo(bytes, 24 + padded.Length);
        return bytes;
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private void WritePackage(string id, string hullName = "Skiff", string[] nodes = null, string modelAsset = "skiff.glb",
        Action<ShipAuthoring> tweak = null)
    {
        var ship = ShipAuthoringTests.Fixture();
        tweak?.Invoke(ship);
        ship.Id = id;
        ship.Hull.Name = hullName;
        ship.ModelAsset = modelAsset;
        var directory = Path.Combine(Mods, id);
        Directory.CreateDirectory(directory);
        using (var cache = ShipAuthoringStore.Open(Path.Combine(directory, "ship.cc"), writable: true))
        {
            cache.Upsert(ship);
            cache.FlushAsync().Wait();
        }
        var json = @"{""asset"":{""version"":""2.0""},""nodes"":[" +
            string.Join(",", (nodes ?? FixtureNodes).Select(node => $@"{{""extras"":{{""aetheria.id"":""{node}""}}}}")) + "]}";
        File.WriteAllBytes(Path.Combine(directory, modelAsset), Glb(json));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
