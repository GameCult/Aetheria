/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
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
                var pressure = row.Jumps <= DemoPressure.GraceJumps ? new DemoPressure.Pressure(0, 0f) : DemoPressure.At((float)Math.Clamp(row.Depth, 0.0, 1.0));
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

    private static readonly uint[] RingSeeds = Enumerable.Range(1, 100).Select(seed => (uint)seed).ToArray();

    private static GalaxyZone[] Ring(Galaxy galaxy) => galaxy.Entrance.AdjacentZones.Prepend(galaxy.Entrance).ToArray();

    // Hated by the player: the antagonist's faction, by the standing the demo stages.
    private bool IsHated(Galaxy galaxy, CultRecordRef<Faction> faction) =>
        faction.IsSet() && galaxy.FactionRelationships[_cache.Get(faction)] == FactionRelationship.Hated;

    // The quiet start, pinned by its own literal and not by GraceDepth: the entrance and its jump-1 neighbours carry no hostile
    // ship and no hostile station or turret, on a hundred seeds, and the guaranteed entrance station is there.
    [Fact]
    public void DemoGalaxy_entrance_ring_has_no_hostiles()
    {
        foreach (var seed in RingSeeds)
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            foreach (var zone in Ring(galaxy))
            {
                var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, galaxy.IsPrelude);
                var hostile = pack.Entities.Where(entity => IsHated(galaxy, entity.Faction)).Select(entity => entity.GetType().Name + ":" + _items.GetData(entity.Hull).Name).ToArray();
                Assert.True(hostile.Length == 0, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)} (jump {galaxy.Entrance.Distance[zone]}): {string.Join(", ", hostile)}");
            }
            var entrance = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, galaxy.Entrance, galaxy.IsPrelude);
            Assert.Contains(entrance.Entities.OfType<OrbitalEntityPack>(), entity => entity.Kind.IsSet());
        }
    }

    // A ring zone as the player meets it: generated, with the demo's player staged into it and the generated ships' standing in force.
    private (Zone arena, Ship player) EnterRingZone(Galaxy galaxy, GalaxyZone zone)
    {
        var arena = new Zone(_items, _planetSettings, ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, galaxy.IsPrelude), zone, galaxy);
        var failures = new List<string>();
        var staged = RunStart.Stage(_items, arena, new DemoTerminus(), _startingHull, _tutorialSettings, failures);
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        arena.Update(0);
        return (arena, staged.Player);
    }

    // The first zones are survivable: in every ring zone no ship is hostile to the player at arrival, on the seeds where the
    // generator once put one there. The pursuit measure (the staged default fit, AI-flown as the proxy for a played one, against one
    // tier-lowest antagonist raider astern for 120 simulated seconds) is printed, not asserted, when DEMO_PRESSURE_PURSUIT names a
    // file: the measured outcome is a balance number for the operator (the player loses it), and no threshold here is a ruling.
    [Fact]
    public void DemoGalaxy_first_zone_survivable()
    {
        const float Seconds = 120f;
        var path = Environment.GetEnvironmentVariable("DEMO_PRESSURE_PURSUIT");
        var lines = new List<string> { "seed zone jump player-hull-start player-hull-end raider-hull-start raider-hull-end" };
        foreach (var seed in new uint[] { 1, 2, 3, 69, 97 })
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            var antagonist = DemoPressure.Antagonist(galaxy);
            foreach (var zone in Ring(galaxy))
            {
                var (arena, player) = EnterRingZone(galaxy, zone);
                var hostile = arena.Entities.Where(entity => entity != player && player.IsHostileTo(entity)).Select(entity => entity.Name).ToArray();
                Assert.True(hostile.Length == 0, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)}: hostile to the player at arrival: {string.Join(", ", hostile)}");
                if (path == null) continue;

                var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, zone, antagonist, .5f);
                var ceiling = DemoPressure.PriceCeiling(generator.OfferedShipHulls().Select(hull => hull.Price).ToArray(), 0f);
                var pursuer = EntitySerializer.Unpack(_items, arena, generator.GenerateShipLoadout(hull => hull.Price <= ceiling));
                pursuer.Position = player.Position + CultMath.math.float3(0, 0, -300);
                pursuer.SetIff(player, true);
                player.SetIff(pursuer, true);
                arena.Admit(pursuer, true);
                Assert.True(arena.Remove(player));
                player.Deactivate();
                arena.Admit(player, true);
                var (playerStart, raiderStart) = (player.Hull.Durability, pursuer.Hull.Durability);
                for (var tick = 0; tick < MathF.Round(Seconds / SimClock.GameStep); tick++) arena.Update(SimClock.GameStep);
                lines.Add($"{seed} {Array.IndexOf(galaxy.Zones, zone)} {galaxy.Entrance.Distance[zone]} {playerStart:0} {player.Hull.Durability:0} {raiderStart:0} {pursuer.Hull.Durability:0}");
            }
        }
        if (path != null) File.WriteAllLines(path, lines);
    }

    // A prelude with several boss zones raids with the owner of the one nearest the entrance, and generates without refusing.
    [Fact]
    public void DemoPressure_antagonist_is_the_nearest_boss_zone_owner()
    {
        var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => 1));
        var zhestokost = DemoPressure.Antagonist(galaxy);
        Assert.Equal("Zhestokost", zhestokost.Name);
        var other = galaxy.Factions.First(faction => faction != zhestokost);
        galaxy.BossZones[other] = galaxy.Entrance.AdjacentZones[0];
        Assert.Same(other, DemoPressure.Antagonist(galaxy));
        galaxy.BossZones.Remove(other);
        galaxy.BossZones[other] = galaxy.Zones.MaxBy(zone => galaxy.Entrance.Distance[zone]);
        Assert.Same(zhestokost, DemoPressure.Antagonist(galaxy));
        ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, galaxy.Exit, galaxy.IsPrelude);
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
