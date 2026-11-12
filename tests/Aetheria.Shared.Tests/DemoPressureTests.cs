/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var hostile = ships.Where(ship => _cache.Get(ship.Faction) == antagonist).ToArray();
            return new ZoneRow(index, galaxy.Entrance.Distance[zone],
                1.0 - (double)galaxy.Exit.Distance[zone] / toExit,
                hostile.Length, ships.Length - hostile.Length,
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
}
