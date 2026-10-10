using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameCult.Caching;
using Xunit;

// TestCatalog.Resolve: the catalog every RunStartTests partial stages from is the one the game boots.
public sealed class TestCatalogTests : IDisposable
{
    private readonly TempDirectory _directory = new TempDirectory();
    private string Shipped => Path.Combine(_directory.Path, "Aetheria.cc");
    private string Mods => Path.Combine(_directory.Path, "Mods");
    private string Work => Path.Combine(_directory.Path, "Work");

    public TestCatalogTests()
    {
        Directory.CreateDirectory(Mods);
        using var cache = AetheriaStores.Open(Shipped, catalogWritable: true, registry: TestCatalog.Registry());
        cache.Upsert(new HullData { Name = "Wasp", Shape = ShipAuthoringTests.Fixture().Hull.Shape });
        cache.FlushAsync().Wait();
    }

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void NoModsMeansTheShippedCatalog()
    {
        var before = File.ReadAllBytes(Shipped);

        Assert.Equal(Shipped, TestCatalog.Resolve(Shipped, Path.Combine(_directory.Path, "absent"), Work));
        Assert.Equal(Shipped, TestCatalog.Resolve(Shipped, Mods, Work));

        Assert.Equal(SHA256.HashData(before), SHA256.HashData(File.ReadAllBytes(Shipped)));
    }

    [Fact]
    public void APackageReachesTheFixture()
    {
        ShipFixture.WritePackage(Mods, "mod.skiff");

        var catalog = TestCatalog.Resolve(Shipped, Mods, Work);

        Assert.NotEqual(Shipped, catalog);
        using var cache = AetheriaStores.Open(catalog, registry: TestCatalog.Registry());
        Assert.Equal("Skiff", cache.Get<HullData>(ShipModCatalog.HullKey("mod.skiff")).Name);
        Assert.Contains("Wasp", cache.GetAll<HullData>().Select(hull => hull.Name));
    }

    [Fact]
    public void ABrokenPackageFailsLoudly()
    {
        ShipFixture.WritePackage(Mods, "mod.good", hullName: "Good");
        ShipFixture.WritePackage(Mods, "mod.broken", hullName: "Broken");
        File.Delete(Path.Combine(Mods, "mod.broken", "skiff.glb"));

        var error = Assert.Throws<InvalidOperationException>(() => TestCatalog.Resolve(Shipped, Mods, Work));

        Assert.Contains("mod.broken", error.Message);
        Assert.DoesNotContain("mod.good", error.Message);
    }

    [Fact]
    public void AFirstPartyPackageComposesOverTheRealCatalog()
    {
        // A repository root holding the real shipped catalog (which has no TestCatalogGlobal record) and one valid package
        // under GameData/Mods, the layout TestCatalog.Repo resolves from.
        var root = Path.Combine(_directory.Path, "repo");
        Directory.CreateDirectory(Path.Combine(root, "GameData"));
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), Path.Combine(root, "GameData", "Aetheria.cc"));
        ShipFixture.WritePackage(Path.Combine(root, "GameData", "Mods"), "mod.skiff", hullName: "FixtureSkiff");

        var catalog = TestCatalog.ForRepo(root);

        Assert.NotEqual(Path.Combine(root, "GameData", "Aetheria.cc"), catalog);
        // Opened as RunStartTests opens it: the shipped assembly's registry, the game's own global check.
        using var cache = AetheriaStores.Open(catalog, registry: TestCatalog.Registry());
        Assert.Equal("FixtureSkiff", cache.Get<HullData>(ShipModCatalog.HullKey("mod.skiff")).Name);
        Assert.NotEmpty(cache.GetAll<HullData>().Where(hull => hull.Name != "FixtureSkiff"));
    }

    [Fact]
    public void APackageWithoutItsShipFileFailsLoudly()
    {
        ShipFixture.WritePackage(Mods, "mod.good", hullName: "Good");
        ShipFixture.WritePackage(Mods, "mod.lost", hullName: "Lost");
        File.Delete(Path.Combine(Mods, "mod.lost", "ship.cc"));

        var error = Assert.Throws<InvalidOperationException>(() => TestCatalog.Resolve(Shipped, Mods, Work));

        Assert.Contains("mod.lost", error.Message);
        Assert.Contains("ship.cc", error.Message);
        Assert.DoesNotContain("mod.good", error.Message);
    }
}
