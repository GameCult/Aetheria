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
// the demo galaxy holds, as one function of its depth toward the gate. The fixture is RunStartTests'.
public sealed partial class RunStartTests
{
    private sealed record ZoneRow(int Index, int Jumps, double Depth, DemoPressure.Pressure Asked, int Hostiles, int Friendly, string[] HostileHulls, int[] HostilePrices);

    // The demo's own declaration, as RunStart.GenerateArena passes it: the entrance zone gets its station.
    private static IReadOnlyList<EntranceStationOrder> DemoEntranceStations => new DemoTerminus().EntranceStations;

    // Every zone of the demo galaxy as the generator builds it. Hostile means the antagonist's ships, the one cast faction
    // the player is Hated by.
    private ZoneRow[] DemoZones(uint seed)
    {
        var (galaxy, _, _, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var antagonist = galaxy.Antagonist;
        return galaxy.Zones.Select((zone, index) =>
        {
            var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, DemoEntranceStations);
            var ships = pack.Entities.OfType<ShipPack>().ToArray();
            // The boss is the run's goal, placed apart from the pressure.
            var hostile = ships.Where(ship => _cache.Get(ship.Faction) == antagonist && !ship.Boss).ToArray();
            return new ZoneRow(index, galaxy.Entrance.Distance[zone],
                DemoPressure.Depth(galaxy, zone), DemoPressure.Of(galaxy, zone),
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
        // The bound is the literal quarter, not DemoPressure.GraceDepth, so a grace that shrinks to nothing fails here.
        foreach (var depth in DepthGrid().Where(depth => depth <= 0.25f))
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
                Assert.True(row.Hostiles == row.Asked.Hostiles, $"seed {seed} zone {row.Index}: {row.Hostiles} hostiles at depth {row.Depth:0.00}, the curve asks {row.Asked.Hostiles}");
                byThird[Math.Min(2, (int)(row.Depth * 3))].Add(row.Hostiles);
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
            var antagonist = galaxy.Antagonist;
            var offered = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Exit, antagonist, .5f)
                .OfferedShipHulls().Select(hull => hull.Price).ToArray();
            Assert.NotEmpty(offered);
            foreach (var row in DemoZones(seed).Where(row => row.Hostiles > 0))
            {
                var ceiling = DemoPressure.PriceCeiling(offered, row.Asked.Tier);
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
                var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, DemoEntranceStations);
                var hostile = pack.Entities.Where(entity => IsHated(galaxy, entity.Faction)).Select(entity => entity.GetType().Name + ":" + _items.GetData(entity.Hull).Name).ToArray();
                Assert.True(hostile.Length == 0, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)} (jump {galaxy.Entrance.Distance[zone]}): {string.Join(", ", hostile)}");
            }
            var entrance = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, galaxy.Entrance, DemoEntranceStations);
            Assert.Contains(entrance.Entities.OfType<OrbitalEntityPack>(), entity => entity.Kind.IsSet());
        }
    }

    // Every zone at jump >= 2 whose depth, derived here as e / (e + x) from the zone's own distances, is at most a quarter holds
    // no ship, station or turret of a Hated faction: the grace as the literal 0.25, not as DemoPressure.GraceDepth or its depth term.
    [Fact]
    public void DemoGalaxy_grace_depth_by_literal()
    {
        var graced = 0;
        foreach (var seed in RingSeeds)
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            foreach (var zone in galaxy.Zones)
            {
                float e = galaxy.Entrance.Distance[zone], x = galaxy.Exit.Distance[zone];
                if (e < 2 || e / (e + x) > 0.25f) continue;
                graced++;
                // The depth term itself: where no hostile would be drawn anyway (the curve's own ramp is 0 there), grace is
                // what moves the antagonist's stations and turrets away, so it is asserted, not left to the generated zone.
                Assert.True(DemoPressure.InGrace(galaxy, zone), $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)} (jump {e}, depth {e / (e + x):0.00}) is not in grace");
                var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, DemoEntranceStations);
                var hostile = pack.Entities.Where(entity => IsHated(galaxy, entity.Faction)).Select(entity => entity.GetType().Name + ":" + _items.GetData(entity.Hull).Name).ToArray();
                Assert.True(hostile.Length == 0, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)} (jump {e}, depth {e / (e + x):0.00}): {string.Join(", ", hostile)}");
            }
        }
        Assert.True(graced > 0, "no zone at jump >= 2 has depth <= 0.25 on seeds 1-100");
    }

    // Depth runs from the entrance to the gate and nowhere outside them: 0 only at the entrance, 1 only at the exit, every other
    // zone strictly between, including those behind the entrance (a clamped depth read them as 0).
    [Fact]
    public void DemoGalaxy_depth_spans_entrance_to_gate()
    {
        foreach (var seed in Enumerable.Range(1, 20).Select(seed => (uint)seed))
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            foreach (var zone in galaxy.Zones)
            {
                var depth = DemoPressure.Depth(galaxy, zone);
                if (zone == galaxy.Entrance) Assert.Equal(0f, depth);
                else if (zone == galaxy.Exit) Assert.Equal(1f, depth);
                else Assert.True(depth > 0f && depth < 1f, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)}: depth {depth}");
            }
        }
    }

    // The demo's galaxy raids with the faction whose gate it has, whatever else BossZones holds: a second boss zone for another
    // faction nearer the entrance changes neither the antagonist nor the raiders of a mid-depth zone, and generates without refusing.
    [Fact]
    public void DemoGalaxy_antagonist_is_the_gate_faction()
    {
        var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => 1));
        var zhestokost = galaxy.Factions.Single(faction => faction.Name == "Zhestokost");
        Assert.Same(zhestokost, galaxy.Antagonist);
        Assert.Contains(zhestokost, galaxy.BossZones.Keys);

        var near = galaxy.Entrance.AdjacentZones[0];
        galaxy.BossZones[galaxy.Factions.First(faction => faction != zhestokost)] = near;
        Assert.Same(zhestokost, galaxy.Antagonist);
        var mid = galaxy.Zones.Where(zone => zone != near && zone != galaxy.Exit && DemoPressure.Of(galaxy, zone).Hostiles > 0)
            .MinBy(zone => Math.Abs(DemoPressure.Depth(galaxy, zone) - .5f));
        Assert.NotNull(mid);
        var raiders = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, mid, DemoEntranceStations)
            .Entities.OfType<ShipPack>().Where(ship => !ship.Boss && _cache.Get(ship.Faction) == zhestokost).ToArray();
        Assert.Equal(DemoPressure.Of(galaxy, mid).Hostiles, raiders.Length);
        ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, galaxy.Exit, DemoEntranceStations);
    }

    // A galaxy with no gate antagonist keeps the nearest faction's patrol even where BossZones is filled (Main Galaxy's bosses):
    // a zone's facts are the same with BossZones intact and cleared, which a scope keyed on BossZones would break. LongHaul has no gate.
    [Fact]
    public void MainGalaxy_patrol_ignores_boss_zones()
    {
        foreach (var seed in new uint[] { 1, 2, 3 })
        {
            var (galaxy, _, _, _) = Launch(new MainGalaxy(), Inputs(() => seed));
            Assert.Null(galaxy.Antagonist);
            Assert.NotEmpty(galaxy.BossZones);
            var zone = galaxy.Zones.First(z => z != galaxy.Entrance && !galaxy.BossZones.ContainsValue(z));

            var intactItems = LoggingItems(new List<string>());
            var intact = ZoneFacts(intactItems, BossZonePack(galaxy, intactItems, zone));
            galaxy.BossZones.Clear();
            var clearedItems = LoggingItems(new List<string>());
            var cleared = ZoneFacts(clearedItems, BossZonePack(galaxy, clearedItems, zone));
            Assert.True(intact == cleared, $"seed {seed}: BossZones changed an ambient zone\nintact:\n{intact}\ncleared:\n{cleared}");
        }
        Assert.Null(Launch(new LongHaul(), Inputs(() => 1)).galaxy.Antagonist);
    }

    // A save committed with key 18 unset (one from before it) restores no antagonist and generates the patrol, not raids.
    [Fact]
    public void SavedGame_without_antagonist_continues_unraided()
    {
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.Empty(failures);
        galaxy.Entrance.Contents = arena;
        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0], 0);
        game.Antagonist = default;
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });
        Assert.Null(restored.Antagonist);

        string[] Hulls(Galaxy g, int index) => ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, g, g.Zones[index], DemoEntranceStations)
            .Entities.OfType<ShipPack>().Select(ship => _items.GetData(ship.Hull).Name).ToArray();
        var deep = DemoZones(1).Where(row => row.Asked.Hostiles >= 3).ToArray();
        Assert.NotEmpty(deep);
        Assert.Contains(deep, row => !Hulls(galaxy, row.Index).SequenceEqual(Hulls(restored, row.Index)));
    }

    // A zone as the player meets it: generated, with the demo's player staged into it and the generated ships' standing in force.
    // adjust edits the generated pack before the arena is built.
    private (Zone arena, Ship player) EnterZone(Galaxy galaxy, GalaxyZone zone, Action<ZonePack> adjust = null)
    {
        var pack = ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, galaxy, zone, DemoEntranceStations);
        adjust?.Invoke(pack);
        var arena = new Zone(_items, _planetSettings, pack, zone, galaxy);
        var failures = new List<string>();
        var staged = RunStart.Stage(_items, arena, new DemoTerminus(), _startingHull, failures);
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        arena.Update(0);
        return (arena, staged.Player);
    }

    // No ship or structure of a Hated faction is in any ring arena at arrival, on the seeds where the generator once put one
    // there. It claims no survival: whether the player lives is DemoGalaxy_pursuit_measure's printout and the operator's ruling.
    [Fact]
    public void DemoGalaxy_ring_arrival_has_no_hated_entity()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 69, 97 })
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            foreach (var zone in Ring(galaxy))
            {
                var (arena, player) = EnterZone(galaxy, zone);
                var hostile = arena.Entities.Where(entity => entity != player && entity.Faction != null && galaxy.FactionRelationships[entity.Faction] == FactionRelationship.Hated).Select(entity => entity.Name).ToArray();
                Assert.True(hostile.Length == 0, $"seed {seed} zone {Array.IndexOf(galaxy.Zones, zone)}: hostile to the player at arrival: {string.Join(", ", hostile)}");
            }
        }
    }

    // The pursuit measure, not an assertion of survival: set DEMO_PRESSURE_PURSUIT to a path to write it. In each seed's raided
    // zone nearest the entrance (the smallest jump whose curve asks for hostiles), the staged default fit, AI-flown as the proxy for
    // a played one, meets one antagonist raider of that zone's tier astern for 120 simulated seconds, the zone's own raiders removed
    // so one pursuer is what is measured. Only that an engagement happened (some hull changed in some arena) is asserted: the
    // outcome is a balance number for the operator (question demo-raider-balance), and no threshold here is a ruling.
    [Fact]
    public void DemoGalaxy_pursuit_measure()
    {
        const float Seconds = 120f;
        var path = Environment.GetEnvironmentVariable("DEMO_PRESSURE_PURSUIT");
        if (string.IsNullOrEmpty(path)) return;
        var lines = new List<string> { "seed zone jump tier player-hull-start player-hull-end raider-hull-start raider-hull-end" };
        var engaged = false;
        foreach (var seed in new uint[] { 1, 2, 3, 69, 97 })
        {
            var (galaxy, _, _, _) = Launch(new DemoTerminus(), Inputs(() => seed));
            var antagonist = galaxy.Antagonist;
            var zone = galaxy.Zones.Where(z => DemoPressure.Of(galaxy, z).Hostiles > 0).MinBy(z => galaxy.Entrance.Distance[z]);
            var tier = DemoPressure.Of(galaxy, zone).Tier;
            var (arena, player) = EnterZone(galaxy, zone, pack => pack.Entities.RemoveAll(entity => entity is ShipPack ship && !ship.Boss && _cache.Get(ship.Faction) == antagonist));

            var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, zone, antagonist, .5f);
            var ceiling = DemoPressure.PriceCeiling(generator.OfferedShipHulls().Select(hull => hull.Price).ToArray(), tier);
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
            engaged |= player.Hull.Durability != playerStart || pursuer.Hull.Durability != raiderStart;
            lines.Add($"{seed} {Array.IndexOf(galaxy.Zones, zone)} {galaxy.Entrance.Distance[zone]} {tier:0.00} {playerStart:0} {player.Hull.Durability:0} {raiderStart:0} {pursuer.Hull.Durability:0}");
        }
        File.WriteAllLines(path, lines);
        Assert.True(engaged, "no hull changed in any pursuit arena");
    }

    // Continue regenerates the same zone: the curve reads only the persisted Entrance, Exit and Antagonist, so a galaxy
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

        string[] Hulls(Galaxy g, int index) => ZoneGenerator.GenerateZone(_items, _zoneSettings, _planetSettings, g, g.Zones[index], DemoEntranceStations)
            .Entities.OfType<ShipPack>().Select(ship => _items.GetData(ship.Hull).Name).ToArray();
        var deep = DemoZones(1).Where(row => row.Hostiles >= 3).ToArray();
        Assert.NotEmpty(deep);
        foreach (var row in deep)
            Assert.Equal(Hulls(galaxy, row.Index), Hulls(restored, row.Index));
    }
}
