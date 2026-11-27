using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// A census over every ship package the repo composes: each hardpoint of a package hull takes at least one catalog item of its
// type under the one fit rule (HardpointData.Takes). The bug class is nakovalnya-package s2: flank flak and radiators that
// took nothing. A type with no item in the catalog at all is skipped (follow-up no-shield-item), because no package can fix
// that. Packages are enumerated through the composed catalog (ShipModCatalog's mod keys), as the game boots them
// (ruling census-tests-read-the-owners).
public sealed class PackageMountCensusTests
{
    private static CultCache Open(string catalog)
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog, true), AetheriaStores.CatalogTypes);
        return cache;
    }

    // Every package hull of a composed catalog, named by ship id. The key format is ShipModCatalog's: a hull record with a mod
    // key is a package's, and the id is what follows the prefix HullKey puts on it.
    private static (string Package, HullData Hull)[] Packages(CultCache cache)
    {
        var prefix = ShipModCatalog.HullKey("").Value;
        return cache.GetAll<HullData>()
            .Select(hull => (Key: cache.RefOf(hull).Key, Hull: hull))
            .Where(entry => ShipModCatalog.IsModKey(entry.Key))
            .Select(entry => (entry.Key.Value.Substring(prefix.Length), entry.Hull))
            .ToArray();
    }

    // One line per hardpoint no catalog item of its type fits, naming package, row and type.
    private static string[] Unfilled(IEnumerable<(string Package, HullData Hull)> packages, IReadOnlyCollection<EquippableItemData> items) =>
        packages.SelectMany(package => package.Hull.Hardpoints
                .Where(row => items.Any(item => item.HardpointType == row.Type) && !items.Any(row.Takes))
                .Select(row => $"{package.Package}: row {row.Transform} ({row.Type}) takes no catalog item"))
            .ToArray();

    [Fact]
    public void EveryPackageMountTakesSomeGear()
    {
        using var cache = Open(TestCatalog.Repo);
        var packages = Packages(cache);
        Assert.NotEmpty(packages);
        Assert.Empty(Unfilled(packages, cache.GetAll<EquippableItemData>().ToArray()));
    }

    private static HullData StarterHull(string root, string id, HullData like) =>
        ShipModCatalog.ReadPackage(ShipStarterCommand.Write(Path.Combine(root, "GameData", "Mods"), id, "Probe " + id, like)).Hull;

    private static HullData Like() => new HullData { Name = "Like", HullType = HullType.Ship, Mass = 1234, Behaviors = { new VelocityLimitData() } };

    // The census cannot pass vacuously. The starter package passes; the same package with any one row's Shape cut to a single
    // cell, that row first or last in the list, fails naming that row and no other. (Widening a mount only lets it take more
    // gear under Takes, so the pin narrows.) Every row the pin can narrow is tried, so a census that skips a row fails here.
    [Fact]
    public void TheCensusNamesAMountNothingFits()
    {
        using var cache = Open(TestCatalog.Repo);
        var items = cache.GetAll<EquippableItemData>().ToArray();
        using var work = new TempDirectory();
        var hull = StarterHull(work.Path, "probe.census", Like());
        Assert.Empty(Unfilled(new[] { ("probe.census", hull) }, items));

        var original = hull.Hardpoints.ToList();
        var narrowable = original.Where(row => items.Any(item => item.HardpointType == row.Type && item.Shape.Coordinates.Length > 1)).ToArray();
        Assert.True(narrowable.Length >= 2, "the pin needs at least two rows that a one-cell shape starves");
        foreach (var row in narrowable)
            foreach (var first in new[] { true, false })
            {
                var shape = row.Shape;
                row.Shape = new Shape(1, 1);
                var others = original.Where(other => other != row);
                hull.Hardpoints = first ? new[] { row }.Concat(others).ToList() : others.Append(row).ToList();
                Assert.Equal(new[] { $"probe.census: row {row.Transform} ({row.Type}) takes no catalog item" }, Unfilled(new[] { ("probe.census", hull) }, items));
                row.Shape = shape;
            }
    }

    // Enumeration reads every package of the composed catalog, not the first: two packages compose, and both are censused.
    [Fact]
    public void TheCensusEnumeratesEveryPackage()
    {
        using var work = new TempDirectory();
        var mods = Path.Combine(work.Path, "GameData", "Mods");
        foreach (var id in new[] { "probe.alpha", "probe.omega" })
            ShipStarterCommand.Write(mods, id, "Probe " + id, Like());
        var catalog = TestCatalog.Resolve(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Aetheria.cc"), mods, work.Path);
        using var cache = Open(catalog);
        Assert.Equal(new[] { "probe.alpha", "probe.omega" }, Packages(cache).Select(package => package.Package).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
}
