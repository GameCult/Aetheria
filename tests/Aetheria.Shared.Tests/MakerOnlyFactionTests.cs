/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// Faction.ManufacturerOnly records ruling minor-power-brands' "no galaxy presence": the main Galaxy constructor's faction
// draw skips such a faction, so it holds no territory and its products reach buyers through allegiance alone.
public sealed class MakerOnlyFactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-makeronly-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly string _repo;

    public MakerOnlyFactionTests()
    {
        _repo = FindRepoRoot();
        Directory.CreateDirectory(_root);
        var catalog = Path.Combine(_root, "Aetheria.cc");
        File.Copy(Path.Combine(_repo, "GameData", "Aetheria.cc"), catalog);
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        _cache = new CultCache(registry);
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(_root, "run.cc")), AetheriaStores.RunTypes);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }

    private Galaxy MainGalaxy(int megaCount, uint seed)
    {
        var authored = AuthoredSettings.Load(_repo);
        var sector = authored.Read<SectorGenerationSettings>("SectorGenerationSettings");
        sector.MegaCount = megaCount;
        return new Galaxy(sector, authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), _cache, _ => { }, null, seed);
    }

    [Fact]
    public void TheGalaxyNeverPlacesAManufacturerOnlyFaction()
    {
        var maker = new Faction { Name = "Test Maker", ShortName = "TestMaker", ManufacturerOnly = true };
        _cache.Upsert(maker);
        var all = _cache.GetAll<Faction>().Count();
        Assert.True(all > 1);

        for (uint seed = 1; seed <= 6; seed++)
        {
            var galaxy = MainGalaxy(all, seed);
            Assert.DoesNotContain(maker, galaxy.Factions);
            Assert.False(galaxy.HomeZones.ContainsKey(maker));
            Assert.False(galaxy.BossZones.ContainsKey(maker));
            Assert.False(galaxy.FactionRelationships.ContainsKey(maker));
            Assert.Equal(all - 1, galaxy.Factions.Length);
        }
    }

    [Fact]
    public void ShippedFactionsArePresent()
    {
        Assert.All(_cache.GetAll<Faction>(), faction => Assert.False(faction.ManufacturerOnly, faction.Name));
        var galaxy = MainGalaxy(_cache.GetAll<Faction>().Count(), 1);
        Assert.Equal(_cache.GetAll<Faction>().Count(), galaxy.Factions.Length);
    }
}
