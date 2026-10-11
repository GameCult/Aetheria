using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's smoke-hull command (aetheria-release, cut scenarios-smoke), run on a scratch copy of the shipped catalog
// with the Smoke Test Hull removed from it.
public sealed class SmokeHullCommandTests : IDisposable
{
    private const string HullName = "Smoke Test Hull";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "smoke-hull-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    public SmokeHullCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        foreach (var hull in cache.GetAll<HullData>().Where(h => h.Name == HullName).ToArray()) Assert.True(cache.Remove(cache.RefOf(hull).Key));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (HullData[] Hulls, HullData Longinus, EquippableItemData[] Fitted, int Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var hulls = db.Cache.GetAll<HullData>().Where(h => h.Name == HullName).ToArray();
            var keys = hulls.Select(h => db.Cache.RefOf(h).Key).ToArray();
            var products = db.Cache.GetAll<FactionProductData>().Count(p => keys.Any(k => p.Design.Key.Equals(k)));
            var fitted = new[] { "ChargeBlast SG", "Autocannon", "Point Defense Gun", "Point Defense Laser" }
                .Select(name => db.Cache.GetAll<EquippableItemData>().Single(d => d.Name == name)).ToArray();
            return (hulls, db.Cache.GetAll<HullData>().Single(h => h.Name == "Longinus"), fitted, products);
        }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_one_productless_hull_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Read().Hulls);

        Assert.Equal(0, Program.SmokeHullCatalog(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Hulls);

        Assert.Equal(0, Program.SmokeHullCatalog(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var (hulls, longinus, fitted, products) = Read();
        var hull = Assert.Single(hulls);
        Assert.Equal(0, products);

        // The Longinus's own hardpoints survive, and four are added: each takes the gear the scenarios fit to it.
        Assert.Equal(longinus.Hardpoints.Count + 4, hull.Hardpoints.Count);
        var added = hull.Hardpoints.Skip(longinus.Hardpoints.Count).ToArray();
        var (chargeBlast, autocannon, pdGun, pdLaser) = (fitted[0], fitted[1], fitted[2], fitted[3]);
        Assert.Single(added, h => h.Type == HardpointType.Energy && h.Rotation == ItemRotation.None && h.Takes(chargeBlast));
        Assert.Single(added, h => h.Type == HardpointType.Ballistic && h.Rotation == ItemRotation.None && h.Takes(autocannon));
        Assert.Single(added, h => h.Type == HardpointType.Ballistic && h.Rotation == ItemRotation.Clockwise && h.Takes(pdGun));
        Assert.Single(added, h => h.Type == HardpointType.Energy && h.Rotation == ItemRotation.CounterClockwise && h.Takes(pdLaser));
        foreach (var hardpoint in added)
            foreach (var cell in hardpoint.Shape.Coordinates)
                Assert.True(hull.Shape[hardpoint.Position + cell], "a new hardpoint lies outside the hull's cells");

        // A second apply finds the hull authored and writes nothing; so does a dry run.
        Assert.Equal(0, Program.SmokeHullCatalog(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.SmokeHullCatalog(apply: false, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Single(Read().Hulls);
    }
}
