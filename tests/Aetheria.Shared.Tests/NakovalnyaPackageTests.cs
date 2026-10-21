using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// The first first-party ship package, GameData/Mods/aetheria.nakovalnya: the Zhestokost boss hull. It enters the game as
// data through the one compose path (ShipModCatalog.ResolveCatalog) and is made by Zhestokost but never offered
// (rulings off-market-is-unoffered-product, boss-hulls-exclusive).
public sealed class NakovalnyaPackageTests
{
    private const string Id = "aetheria.nakovalnya";

    private static CultCache Catalog()
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(TestCatalog.Repo, true), AetheriaStores.CatalogTypes);
        return cache;
    }

    private static HullData Hull(CultCache cache) => cache.Get(new CultRecordRef<HullData>(ShipModCatalog.HullKey(Id)));

    [Fact]
    public void ComposeExcludesNothing()
    {
        var root = RunStartTests.FindRepoRoot();
        using var work = new TempDirectory();
        var shipped = Path.Combine(root, "GameData", "Aetheria.cc");
        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(shipped, Path.Combine(work.Path, "derived.cc"),
            Path.Combine(root, "GameData", "Mods"), TestCatalog.Registry());
        Assert.Empty(excluded);
        Assert.NotEqual(shipped, catalog);

        using var cache = Catalog();
        var hull = Hull(cache);
        Assert.NotNull(hull);
        Assert.Equal(("Nakovalnya", HullType.Ship), (hull.Name, hull.HullType));
        Assert.Equal(ShipModCatalog.AuthoringKey(Id), hull.Visual.Key);
    }

    [Fact]
    public void BossProductIsNotOffered()
    {
        using var cache = Catalog();
        var hull = Hull(cache);
        var zhestokost = cache.GetAll<Faction>().Single(faction => faction.Name == "Zhestokost");
        var product = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(ShipModCatalog.HullKey(Id)));
        Assert.Equal(cache.RefOf(zhestokost).Key, product.Manufacturer.Key);
        Assert.Equal(ShipModCatalog.ProductKey(Id, cache.RefOf(zhestokost).Key), cache.RefOf(product).Key);
        Assert.Equal("Nakovalnya", product.Name);
        Assert.True(product.NotOffered);
        Assert.True(hull.Price > 0);

        Assert.DoesNotContain(hull, LoadoutGenerator.SoldHulls(cache));
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        foreach (var faction in cache.GetAll<Faction>().Append(null))
        {
            var random = new CultMath.Random(7u);
            var generator = new LoadoutGenerator(ref random, items, null, null, faction, .5f);
            Assert.False(generator.IsAvailable(product), faction?.Name);
            // Even a required draw that falls back to any manufacturer never reaches it.
            Assert.Null(generator.RandomHull(HullType.Ship, design => design == hull).design);
        }
    }

    [Fact]
    public void LayoutMatchesTheBrief()
    {
        using var cache = Catalog();
        var hull = Hull(cache);
        var ship = cache.Get(new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(Id)));
        var rows = hull.Hardpoints.GroupBy(h => (h.Type, h.Shape.Width, h.Shape.Height)).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, rows.GetValueOrDefault((HardpointType.Ballistic, 2, 2)));
        Assert.Equal(10, rows.GetValueOrDefault((HardpointType.Ballistic, 1, 2)));
        Assert.Equal(8, hull.Hardpoints.Count(h => h.Type == HardpointType.Thruster));
        Assert.Equal(2, hull.Hardpoints.Count(h => h.Type == HardpointType.Radiator));
        foreach (var kind in new[] { HardpointType.Reactor, HardpointType.Shield, HardpointType.ControlModule, HardpointType.Sensors })
            Assert.Equal(1, hull.Hardpoints.Count(h => h.Type == kind));
        Assert.Equal(27, hull.Hardpoints.Count);

        // Every mount row names a model anchor by id (ruling schematic-and-model-are-separate-spaces). Every row has an id
        // (ShipAuthoringStore.Validate); an internal row's id names no anchor.
        var anchors = ship.Anchors.Select(anchor => anchor.Id).ToHashSet(StringComparer.Ordinal);
        var internals = new[] { HardpointType.Reactor, HardpointType.Shield, HardpointType.ControlModule, HardpointType.Sensors };
        var mounts = hull.Hardpoints.Where(h => !internals.Contains(h.Type)).ToArray();
        Assert.Equal(23, mounts.Length);
        Assert.All(mounts, h => Assert.Contains(h.Transform, anchors));
        Assert.All(hull.Hardpoints.Where(h => internals.Contains(h.Type)), h => Assert.DoesNotContain(h.Transform, anchors));
        Assert.Equal(Enumerable.Range(1, 3).Select(i => $"Gun.H{i}").Concat(Enumerable.Range(1, 10).Select(i => $"Gun.F{i}")).OrderBy(s => s, StringComparer.Ordinal),
            mounts.Where(h => h.Type == HardpointType.Ballistic).Select(h => h.Transform).OrderBy(s => s, StringComparer.Ordinal));

        Assert.InRange(hull.Shape.Width, 1, 32);
        Assert.InRange(hull.Shape.Height, 1, 32);
    }
}
