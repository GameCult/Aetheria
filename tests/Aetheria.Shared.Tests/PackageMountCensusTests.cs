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
    private static CultCache Catalog()
    {
        var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(TestCatalog.Repo, true), AetheriaStores.CatalogTypes);
        return cache;
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
        using var cache = Catalog();
        var packages = cache.GetAll<HullData>()
            .Select(hull => (Key: cache.RefOf(hull).Key, Hull: hull))
            .Where(entry => entry.Key.Value.StartsWith("mod-hull:", StringComparison.Ordinal))
            .Select(entry => (entry.Key.Value.Substring("mod-hull:".Length), entry.Hull))
            .ToArray();
        Assert.NotEmpty(packages);
        Assert.Empty(Unfilled(packages, cache.GetAll<EquippableItemData>().ToArray()));
    }

    // The census cannot pass vacuously. The starter package passes; the same package with one row's Shape cut to a single cell
    // fails naming that row. (Widening a mount only lets it take more gear under Takes, so the pin narrows.)
    [Fact]
    public void TheCensusNamesAMountNothingFits()
    {
        using var cache = Catalog();
        var items = cache.GetAll<EquippableItemData>().ToArray();
        using var work = new TempDirectory();
        var like = new HullData { Name = "Like", HullType = HullType.Ship, Mass = 1234, Behaviors = { new VelocityLimitData() } };
        var path = ShipStarterCommand.Write(Path.Combine(work.Path, "GameData", "Mods"), "probe.census", "Probe Census", like);

        var hull = ShipModCatalog.ReadPackage(path).Hull;
        Assert.Empty(Unfilled(new[] { ("probe.census", hull) }, items));

        var control = hull.Hardpoints.Single(row => row.Transform == "internal.control");
        Assert.False(items.Where(item => item.HardpointType == control.Type).All(item => item.Shape.Coordinates.Length == 1),
            "the pin needs a control module larger than one cell");
        control.Shape = new Shape(1, 1);
        var failures = Unfilled(new[] { ("probe.census", hull) }, items);
        Assert.Equal(new[] { "probe.census: row internal.control (ControlModule) takes no catalog item" }, failures);
    }
}
