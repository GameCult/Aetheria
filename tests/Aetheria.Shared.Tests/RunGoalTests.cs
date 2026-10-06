/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CultMath;
using UniRx;
using Xunit;

// The demo's goal (aetheria-release, cut boss-gate): the boss zone spawns its faction's boss, and the exit gate opens
// only when that boss is dead. Every input is the production one: the demo galaxy, GenerateZone, the Zone death path,
// and the run save.
public sealed partial class RunStartTests
{
    private ZonePack BossZonePack(Galaxy galaxy, ItemManager items, GalaxyZone zone, bool ambient = true)
    {
        items.Random = new CultMath.Random(7);
        return ZoneGenerator.GenerateZone(items, _zoneSettings, galaxy, zone, galaxy.IsPrelude, ambient);
    }

    private Zone ZoneOf(Galaxy galaxy, GalaxyZone zone) =>
        new Zone(_items, _planetSettings, BossZonePack(galaxy, _items, zone), zone, galaxy);

    private static Faction Zhestokost(Galaxy galaxy) => galaxy.Factions.Single(faction => faction.Name == "Zhestokost");

    private ItemManager LoggingItems(List<string> log) =>
        new ItemManager(_cache, new ProvenanceLedger(), AuthoredSettings.Load(FindRepoRoot()).Read<GameplaySettings>("GameplaySettings"), log.Add);

    private static string HullName(ItemManager items, EntityPack pack) => items.GetData(pack.Hull).Name;

    private HullData[] SoldShipHulls() => _cache.GetAll<FactionProductData>()
        .Where(product => product.Manufacturer.IsSet())
        .Select(product => _cache.Get(product.Design) as HullData)
        .Where(hull => hull != null && hull.HullType == HullType.Ship && hull.Price > 0)
        .Distinct().ToArray();

    [Fact]
    public void Boss_spawns_once_in_boss_zone()
    {
        AddPirates();
        foreach (var seed in DemoSeeds.Take(3))
        {
            var galaxy = DemoGalaxy(seed);
            var antagonist = Zhestokost(galaxy);
            foreach (var zone in galaxy.Zones)
            {
                var bosses = BossZonePack(galaxy, _items, zone).Entities.Where(entity => entity.Boss).ToArray();
                if (zone != galaxy.Exit) { Assert.Empty(bosses); continue; }

                var boss = Assert.Single(bosses);
                Assert.IsType<ShipPack>(boss);
                Assert.Same(antagonist, _cache.Get(boss.Faction));
                // Generation is repeatable per zone: the same zone yields the same boss hull.
                var again = Assert.Single(BossZonePack(galaxy, _items, zone).Entities, entity => entity.Boss);
                Assert.Equal(HullName(_items, boss), HullName(_items, again));
            }
        }
    }

    // A zone that is not ambient still has its boss: the boss is the goal, not traffic.
    [Fact]
    public void Boss_spawns_in_a_zone_that_is_not_ambient()
    {
        AddPirates();
        var galaxy = DemoGalaxy(1);
        var pack = BossZonePack(galaxy, _items, galaxy.Exit, ambient: false);
        Assert.Single(pack.Entities, entity => entity.Boss);
        Assert.DoesNotContain(pack.Entities, entity => !entity.Boss && entity is ShipPack);
    }

    [Fact]
    public void Boss_hull_from_BossHull_else_fallback()
    {
        AddPirates();
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var hulls = SoldShipHulls().OrderByDescending(hull => hull.Price).ThenBy(hull => hull.Name, StringComparer.Ordinal).ToArray();
        var priciest = hulls.First();
        var cheaper = hulls.Last();
        Assert.True(cheaper.Price < priciest.Price, "the catalog needs two ship hulls of different price for this test to tell them apart");

        // Set and sold: the boss flies it, and nothing is logged about a gap.
        antagonist.BossHull = _cache.RefOf(cheaper);
        var log = new List<string>();
        var items = LoggingItems(log);
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal(cheaper.Name, HullName(items, boss));
        Assert.DoesNotContain(log, line => line.Contains("BossHull"));

        // Unset: the most expensive ship hull on offer, and the log names the missing BossHull.
        antagonist.BossHull = default;
        log.Clear();
        boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal(priciest.Name, HullName(items, boss));
        Assert.Contains(log, line => line.Contains("Zhestokost") && line.Contains("no BossHull"));
    }

    // Set but sold by no product: the boss falls back the same way, and the log says which hull has no seller.
    [Fact]
    public void Boss_hull_nobody_sells_falls_back()
    {
        AddPirates();
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var sold = SoldShipHulls();
        var unsold = _cache.GetAll<HullData>().FirstOrDefault(hull => hull.HullType == HullType.Ship && !sold.Contains(hull));
        Assert.True(unsold != null, "the catalog needs a ship hull no product sells for this test");
        antagonist.BossHull = _cache.RefOf(unsold);
        var log = new List<string>();
        var items = LoggingItems(log);
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal(sold.OrderByDescending(hull => hull.Price).ThenBy(hull => hull.Name, StringComparer.Ordinal).First().Name, HullName(items, boss));
        Assert.Contains(log, line => line.Contains(unsold.Name) && line.Contains("BossHull"));
    }

    [Fact]
    public void Exit_sealed_until_boss_dies()
    {
        AddPirates();
        var galaxy = DemoGalaxy(1);
        var exit = ZoneOf(galaxy, galaxy.Exit);
        var boss = Assert.Single(exit.Entities, entity => entity.IsBoss);
        Assert.True(RunGoal.BossAlive(exit));
        Assert.False(RunGoal.ExitOpen(exit));

        // Death removes the boss from the zone (the one death path), and that alone opens the gate.
        boss.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.DoesNotContain(boss, exit.Entities);
        Assert.False(RunGoal.BossAlive(exit));
        Assert.True(RunGoal.ExitOpen(exit));

        // No other zone is ever an exit, boss or no boss.
        foreach (var zone in galaxy.Zones.Where(zone => zone != galaxy.Exit))
            Assert.False(RunGoal.ExitOpen(ZoneOf(galaxy, zone)));
    }

    // Continue: the mark is saved with the zone and restored, so a boss alive when the run was saved is alive after.
    [Fact]
    public void Boss_mark_survives_continue()
    {
        AddPirates();
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        galaxy.Entrance.Contents = arena;
        galaxy.Exit.Contents = ZoneOf(galaxy, galaxy.Exit);
        var before = Assert.Single(galaxy.Exit.Contents.Entities, entity => entity.IsBoss);
        Assert.False(RunGoal.ExitOpen(galaxy.Exit.Contents));

        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0]);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });
        var exit = new Zone(_items, _planetSettings, restored.Exit.PackedContents, restored.Exit, restored);

        var after = Assert.Single(exit.Entities, entity => entity.IsBoss);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Faction.Name, after.Faction.Name);
        Assert.False(RunGoal.ExitOpen(exit));
        after.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.True(RunGoal.ExitOpen(exit));
    }

    // The gate shares no point with an adjacency wormhole: every one is farther from it than a ship's wormhole reach.
    [Fact]
    public void Exit_gate_shares_no_point_with_a_wormhole()
    {
        AddPirates();
        var ratio = float.Parse(File.ReadLines(Path.Combine(FindRepoRoot(), "Assets", "Resources", "Settings.asset"))
            .Select(line => Regex.Match(line, @"^  WormholeDistanceRatio: (.+)$")).First(match => match.Success).Groups[1].Value.Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
        var reach = AuthoredSettings.Load(FindRepoRoot()).Read<GameplaySettings>("GameplaySettings").WormholeExitRadius;
        foreach (var seed in DemoSeeds)
        {
            var galaxy = DemoGalaxy(seed);
            var zone = ZoneOf(galaxy, galaxy.Exit);
            var gate = RunGoal.ExitGatePosition(zone, ratio);
            foreach (var adjacent in galaxy.Exit.AdjacentZones)
            {
                var wormhole = math.normalize(adjacent.Position - galaxy.Exit.Position) * zone.Pack.Radius * ratio;
                Assert.True(math.length(gate - wormhole) > 2 * reach, $"seed {seed}: the exit gate sits {math.length(gate - wormhole)} from a wormhole");
            }
        }
    }
}
