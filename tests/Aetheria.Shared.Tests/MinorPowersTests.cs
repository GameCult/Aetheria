/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// The Aya Collective, Megiddo and Orbital Forge (aetheria-release, cut minor-power-factions; ruling no-territory-ban): three
// ordinary factions in the shipped catalog, authored by AetherDb's minor-power-factions command, which a scratch copy of the
// catalog without them proves idempotent and conflict-refusing.
public sealed class MinorPowersTests : IDisposable
{
    private static readonly string[] Minor = { "Aya", "Megiddo", "Forge" };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "minor-powers-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    // A scratch copy of the shipped catalog with the three, their products and every allegiance toward them removed, through a
    // registry scoped to the shipped assembly (as PiratesFactionCommandTests does), so the command has all of it to author.
    private void StripTheMinorPowers()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        File.Copy(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), CatalogPath);
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        var gone = cache.GetAll<Faction>().Where(f => Minor.Contains(f.ShortName)).Select(f => cache.RefOf(f)).ToArray();
        Assert.Equal(3, gone.Length);
        foreach (var product in cache.GetAll<FactionProductData>().Where(p => gone.Any(g => g.Key.Equals(p.Manufacturer.Key))).ToArray())
            Assert.True(cache.Remove(cache.RefOf(product).Key));
        foreach (var reference in gone) Assert.True(cache.Remove(reference.Key));
        foreach (var faction in cache.GetAll<Faction>())
            foreach (var key in faction.Allegiance.Keys.Where(k => gone.Any(g => g.Key.Equals(k.Key))).ToArray())
                faction.Allegiance.Remove(key);
        cache.Commit(batch =>
        {
            foreach (var faction in cache.GetAll<Faction>()) batch.Upsert(typeof(Faction), faction, cache.RefOf(faction).Key);
        });
        cache.FlushAsync().Wait();
    }

    [Fact]
    public void TheMinorPowersAreOrdinaryFactions()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var factions = cache.GetAll<Faction>().ToArray();
        foreach (var shortName in Minor)
        {
            var faction = Assert.Single(factions, f => f.ShortName == shortName);
            Assert.NotNull(cache.Get(faction.GeonameFile));
            Assert.True(faction.InfluenceDistance > 0, shortName);
            var named = faction.Allegiance.Keys.Select(k => k.Key).ToArray();
            var expected = factions.Where(f => f != faction && f.ShortName != "Pirates").Select(f => cache.RefOf(f).Key).ToArray();
            Assert.Equal(expected.Length, named.Length);
            Assert.All(expected, key => Assert.Contains(named, n => n.Equals(key)));
        }
        // Territory is nothing a faction can say of itself: no member of Faction marks a maker-only or landless power.
        Assert.DoesNotContain(typeof(Faction).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic),
            m => m.Name.Contains("ManufacturerOnly") || m.Name.Contains("Territory"));
    }

    [Fact]
    public void TheCommandIsIdempotentAndRefusesAConflict()
    {
        StripTheMinorPowers();
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Equal(0, MinorPowerFactionsCommand.Run(new string[0], _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));

        Assert.Equal(0, MinorPowerFactionsCommand.Run(new[] { "apply" }, _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var db = AetherDb.Open(root: _root);
        try
        {
            var factions = db.Cache.GetAll<Faction>().ToArray();
            foreach (var shortName in Minor) Assert.Single(factions, f => f.ShortName == shortName);
            foreach (var shortName in Minor)
            {
                var own = factions.Single(f => f.ShortName == shortName);
                var expected = factions.Where(f => f != own && f.ShortName != "Pirates").Select(f => db.Cache.RefOf(f).Key).ToArray();
                Assert.Equal(expected.Length, own.Allegiance.Count);
                Assert.All(expected, key => Assert.Contains(own.Allegiance.Keys, k => k.Key.Equals(key)));
            }
            var forge = db.Cache.RefOf(factions.Single(f => f.ShortName == "Forge"));
            Assert.Equal(.5f, factions.Single(f => f.ShortName == "AU").Allegiance[forge]);
            // The Pirates' allegiance is pirates-faction's; Miss Terri's is her own.
            Assert.All(factions.Where(f => f.ShortName is "Pirates" or "Miss Terri's"), f => Assert.DoesNotContain(f.Allegiance.Keys, k => k.Equals(forge)));
        }
        finally { db.Cache.Dispose(); }

        Assert.Equal(0, MinorPowerFactionsCommand.Run(new[] { "apply" }, _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void ANameHeldUnderAnotherKeyIsRefusedWithNothingWritten()
    {
        StripTheMinorPowers();
        var squatter = AetherDb.Open(catalogWritable: true, root: _root);
        squatter.Cache.Commit(batch => batch.Upsert(typeof(Faction), new Faction { Name = "Megiddo", ShortName = "Megiddo" }));
        squatter.Cache.Dispose();
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Throws<InvalidOperationException>(() => MinorPowerFactionsCommand.Run(new[] { "apply" }, _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
    }
}

public sealed partial class RunStartTests
{
    // Forge holds no zone in the prelude, so its gear reaches AU only through allegiance (ruling gear-without-presence); a
    // design-only slot draws among the makers AU can reach (ruling design-only-weighted-draw). Miss Terri's names none of them.
    [Fact]
    public void TheirGearSellsWhereTheyAreAbsent()
    {
        var factions = _cache.GetAll<Faction>().ToArray();
        var forge = factions.Single(f => f.ShortName == "Forge");
        Assert.DoesNotContain(_galaxy.Factions, f => f == forge);
        var autocannon = _cache.GetAll<CraftedItemData>().Single(d => d.Name == "Autocannon");
        var pattern30 = _cache.GetAll<FactionProductData>().Single(p => p.Name == "Pattern 30");

        LoadoutGenerator For(string shortName, uint seed)
        {
            _items.Random = new CultMath.Random(seed);
            return new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, factions.Single(f => f.ShortName == shortName), .5f);
        }
        Assert.True(For("AU", 1).IsAvailable(pattern30));
        // Seeds 1-64 spread by a multiplicative hash: the generator's first draws of adjacent small seeds are close together.
        Assert.Contains(Enumerable.Range(1, 64), seed => For("AU", unchecked((uint) seed * 2654435761u)).ChooseProduct(autocannon)?.Name == pattern30.Name);

        var terri = For("Miss Terri's", 1);
        var theirs = _cache.GetAll<FactionProductData>().Where(p => new[] { "Aya", "Megiddo", "Forge" }.Contains(_cache.Get(p.Manufacturer).ShortName)).ToArray();
        Assert.Equal(10, theirs.Length);
        Assert.All(theirs, product => Assert.False(terri.IsAvailable(product), product.Name));
    }
}
