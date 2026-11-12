/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using Xunit;

// The demo's pressure curve (aetheria-release, cut demo-pressure-curve): how many hostiles of what hull tier a zone of
// the demo galaxy holds, as one function of its depth toward the boss zone. The fixture is RunStartTests'.
public sealed partial class RunStartTests
{
    private sealed record ZoneRow(int Index, int Jumps, double Depth, int Hostiles, int Friendly, string[] HostileHulls, int[] HostilePrices);

    // Every zone of the demo galaxy as the generator builds it. Hostile means the antagonist's ships, the one cast faction
    // the player is Hated by.
    private ZoneRow[] DemoZones(uint seed)
    {
        var (galaxy, _, _, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var antagonist = galaxy.Factions.Single(faction => faction.Name == "Zhestokost");
        var toExit = galaxy.Exit.Distance[galaxy.Entrance];
        return galaxy.Zones.Select((zone, index) =>
        {
            var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, galaxy.IsPrelude);
            var ships = pack.Entities.OfType<ShipPack>().ToArray();
            // The boss is the run's goal, placed apart from the pressure.
            var hostile = ships.Where(ship => _cache.Get(ship.Faction) == antagonist && !ship.Boss).ToArray();
            return new ZoneRow(index, galaxy.Entrance.Distance[zone],
                1.0 - (double)galaxy.Exit.Distance[zone] / toExit,
                hostile.Length, ships.Count(ship => _cache.Get(ship.Faction) != antagonist),
                hostile.Select(ship => _items.GetData(ship.Hull).Name).ToArray(),
                hostile.Select(ship => _items.GetData(ship.Hull).Price).ToArray());
        }).ToArray();
    }

    // The measured table the operator tunes against (not an assertion): set DEMO_PRESSURE_TABLE to a path to write it.
    [Fact]
    public void DemoGalaxy_pressure_table()
    {
        var path = Environment.GetEnvironmentVariable("DEMO_PRESSURE_TABLE");
        if (string.IsNullOrEmpty(path)) return;
        var lines = new List<string> { "seed zone jumps depth hostiles other hull-prices" };
        foreach (var seed in DemoSeeds)
            foreach (var row in DemoZones(seed).OrderBy(r => r.Jumps))
                lines.Add($"{seed} {row.Index,3} {row.Jumps,2} {row.Depth:0.00} {row.Hostiles,2} {row.Friendly,2} [{string.Join(",", row.HostilePrices)}]");
        File.WriteAllLines(path, lines);
    }

    private static IEnumerable<float> DepthGrid() => Enumerable.Range(0, 1001).Select(step => step / 1000f);

    // The curve only rises with depth, and rises somewhere in both its counts and its tiers.
    [Fact]
    public void DemoPressure_monotone()
    {
        var grid = DepthGrid().Select(depth => DemoPressure.At(depth)).ToArray();
        for (var i = 1; i < grid.Length; i++)
        {
            Assert.True(grid[i - 1].Hostiles <= grid[i].Hostiles, $"hostiles fall at step {i}");
            Assert.True(grid[i - 1].Tier <= grid[i].Tier, $"tier falls at step {i}");
        }
        Assert.True(grid.First().Hostiles < grid.Last().Hostiles);
        Assert.True(grid.First().Tier < grid.Last().Tier);
    }

    [Fact]
    public void DemoPressure_grace_at_entrance()
    {
        Assert.Equal(0, DemoPressure.At(0f).Hostiles);
        foreach (var depth in DepthGrid().Where(depth => depth <= DemoPressure.GraceDepth))
            Assert.Equal(0, DemoPressure.At(depth).Hostiles);
    }

    [Fact]
    public void DemoPressure_boss_zone_is_ceiling()
    {
        var ceiling = DemoPressure.At(1f);
        Assert.Equal(DemoPressure.MaxHostiles, ceiling.Hostiles);
        foreach (var depth in DepthGrid())
        {
            Assert.True(DemoPressure.At(depth).Hostiles <= ceiling.Hostiles);
            Assert.True(DemoPressure.At(depth).Tier <= ceiling.Tier);
        }
    }

    // However low the tier, the ceiling admits at least the cheapest hull offered, and the top tier admits them all.
    [Fact]
    public void DemoPressure_price_ceiling_never_empty()
    {
        var prices = new[] { 10_000_000, 7_500_000, 60_000_000, 10_000_000 };
        foreach (var tier in DepthGrid())
            Assert.Contains(prices, price => price <= DemoPressure.PriceCeiling(prices, tier));
        Assert.Equal(prices.Min(), DemoPressure.PriceCeiling(prices, 0f));
        Assert.Equal(prices.Max(), DemoPressure.PriceCeiling(prices, 1f));
    }

    // The generator draws the function's numbers, not a parallel formula: every zone of the demo galaxy holds exactly the
    // hostiles the curve asks of its depth, none dearer than its tier admits, and the mean count rises from the near third
    // of the way to the far third.
    [Fact]
    public void DemoGalaxy_generated_pressure_follows_curve()
    {
        var byThird = new List<double>[3] { new List<double>(), new List<double>(), new List<double>() };
        foreach (var seed in DemoSeeds)
            foreach (var row in DemoZones(seed))
            {
                var pressure = DemoPressure.At((float)Math.Clamp(row.Depth, 0.0, 1.0));
                Assert.True(row.Hostiles == pressure.Hostiles, $"seed {seed} zone {row.Index}: {row.Hostiles} hostiles at depth {row.Depth:0.00}, the curve asks {pressure.Hostiles}");
                byThird[Math.Min(2, (int)(Math.Clamp(row.Depth, 0.0, 1.0) * 3))].Add(row.Hostiles);
            }
        var means = byThird.Select(bucket => bucket.Average()).ToArray();
        Assert.True(means[0] <= means[1] && means[1] < means[2], string.Join(" ", means));
    }

    [Fact]
    public void DemoGalaxy_hostile_hulls_stay_inside_the_tier()
    {
        foreach (var seed in DemoSeeds)
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            var antagonist = galaxy.Factions.Single(faction => faction.Name == "Zhestokost");
            var offered = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Exit, antagonist, .5f)
                .OfferedShipHulls().Select(hull => hull.Price).ToArray();
            Assert.NotEmpty(offered);
            foreach (var row in DemoZones(seed).Where(row => row.Hostiles > 0))
            {
                var ceiling = DemoPressure.PriceCeiling(offered, DemoPressure.At((float)Math.Clamp(row.Depth, 0.0, 1.0)).Tier);
                Assert.All(row.HostilePrices, price => Assert.True(price <= ceiling, $"seed {seed} zone {row.Index}: a {price} hull above the {ceiling} ceiling"));
            }
        }
    }

    // The entrance carries no antagonist raid on any seed, and the guaranteed station is still there; its neighbours too.
    [Fact]
    public void DemoGalaxy_entrance_ring_has_no_hostiles()
    {
        foreach (var seed in DemoSeeds)
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            var ring = galaxy.Entrance.AdjacentZones.Prepend(galaxy.Entrance).Select(zone => Array.IndexOf(galaxy.Zones, zone)).ToHashSet();
            var rows = DemoZones(seed);
            Assert.All(rows.Where(row => ring.Contains(row.Index) && row.Depth <= DemoPressure.GraceDepth), row => Assert.Equal(0, row.Hostiles));
            Assert.Equal(0, rows[Array.IndexOf(galaxy.Zones, galaxy.Entrance)].Hostiles);
            var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, galaxy.Entrance, galaxy.IsPrelude);
            Assert.Contains(pack.Entities.OfType<OrbitalEntityPack>(), entity => entity.Kind.IsSet());
        }
    }

    // The first zone is survivable: the staged player idles in the launched entrance arena for five simulated minutes and
    // its hull is not touched. A proxy for "quiet enough to dock and learn the controls", not the operator's judgement.
    [Fact]
    public void DemoGalaxy_first_zone_survivable()
    {
        const float Seconds = 300f;
        foreach (var seed in DemoSeeds)
        {
            var (_, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
            Assert.Empty(failures);
            arena.Update(0);
            var hull = staged.Player.Hull.Durability;
            for (var tick = 0; tick < MathF.Round(Seconds / SimClock.GameStep); tick++) arena.Update(SimClock.GameStep);
            Assert.True(staged.Player.Hull.Durability >= hull, $"seed {seed}: hull {staged.Player.Hull.Durability} of {hull} after {Seconds}s idle");
        }
    }

    // Continue regenerates the same zone: the curve reads only the persisted Entrance, Exit and prelude mark, so a galaxy
    // restored from the save draws the same hostile hulls as before it.
    [Fact]
    public void DemoGalaxy_continue_regenerates_same_zone()
    {
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.Empty(failures);
        galaxy.Entrance.Contents = arena;
        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0], 0);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });

        string[] Hulls(Galaxy g, int index) => ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, g, g.Zones[index], g.IsPrelude)
            .Entities.OfType<ShipPack>().Select(ship => _items.GetData(ship.Hull).Name).ToArray();
        var deep = DemoZones(1).Where(row => row.Hostiles >= 3).ToArray();
        Assert.NotEmpty(deep);
        foreach (var row in deep)
            Assert.Equal(Hulls(galaxy, row.Index), Hulls(restored, row.Index));
    }
}
