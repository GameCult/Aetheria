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
        return ZoneGenerator.GenerateZone(items, _zoneSettings, _planetSettings, galaxy, zone, galaxy.IsPrelude, ambient);
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
        var galaxy = DemoGalaxy(1);
        var pack = BossZonePack(galaxy, _items, galaxy.Exit, ambient: false);
        Assert.Single(pack.Entities, entity => entity.Boss);
        Assert.DoesNotContain(pack.Entities, entity => !entity.Boss && entity is ShipPack);
    }

    [Fact]
    public void Exit_sealed_until_boss_dies()
    {
        var galaxy = DemoGalaxy(1);
        var exit = ZoneOf(galaxy, galaxy.Exit);
        var boss = Assert.Single(exit.Entities, entity => entity.IsBoss);
        Assert.True(RunGoal.BossAlive(exit));
        Assert.False(RunGoal.ExitOpen(exit));

        // Death removes the boss from the zone (the one death path), and that alone opens the gate.
        boss.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.False(exit.Entities.Any(x => ReferenceEquals(x, boss)), "the boss is removed from the exit");
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
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        galaxy.Entrance.Contents = arena;
        galaxy.Exit.Contents = ZoneOf(galaxy, galaxy.Exit);
        var before = Assert.Single(galaxy.Exit.Contents.Entities, entity => entity.IsBoss);
        Assert.False(RunGoal.ExitOpen(galaxy.Exit.Contents));

        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0], 0);
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

    // The gate fails closed. The main galaxy puts its bosses on choke points, not at its exit, so its exit zone never gets
    // one: the gate stays sealed on arrival and when every boss in the galaxy dies, because a boss that never existed is
    // not a dead one.
    [Fact]
    public void Exit_sealed_where_no_boss_was_spawned_main_galaxy()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => seed));
            var exit = ZoneOf(galaxy, galaxy.Exit);
            Assert.False(RunGoal.ExitOpen(exit), $"seed {seed}: the gate is open on arrival");
            foreach (var boss in exit.Entities.Where(entity => entity.IsBoss).ToArray())
                boss.HeatstrokeDeath.OnNext(Unit.Default);
            Assert.False(RunGoal.ExitOpen(exit), $"seed {seed}: the gate is open with no boss spawned");
        }
    }

    // The same for a demo exit whose boss could not be generated: the zone logs the gap by name and the gate stays sealed.
    [Fact]
    public void Exit_sealed_and_logged_when_the_boss_cannot_be_generated()
    {
        var galaxy = DemoGalaxy(1);
        galaxy.BossZones.Clear();
        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.Contains(log, line => line.Contains("Galaxy.Exit") && line.Contains("exit-without-boss"));
        var exit = new Zone(items, _planetSettings, pack, galaxy.Exit, galaxy);
        Assert.False(RunGoal.ExitOpen(exit));
    }

    // The gate is the exit zone's alone. The main galaxy puts bosses on choke points that are not its exit: such a zone's
    // boss spawns and dies, and no gate opens, because the exit-zone condition is what keeps a slain boss from winning.
    [Fact]
    public void Exit_open_only_in_the_exit_zone()
    {
        var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => 1));
        // The shipped catalog sets no BossHull outside the demo's antagonist, so the main galaxy places no boss: give a
        // faction one and a boss zone that is not the exit, as the generator would for a faction with a boss hull.
        var faction = galaxy.Factions.First();
        faction.BossHull = _cache.RefOf(SoldShipHulls().First());
        var bossZone = galaxy.Zones.First(zone => zone != galaxy.Exit);
        galaxy.BossZones[faction] = bossZone;
        var zone = ZoneOf(galaxy, bossZone);
        Assert.True(zone.Pack.BossSpawned);
        Assert.NotEmpty(zone.Entities.Where(entity => entity.IsBoss));
        foreach (var boss in zone.Entities.Where(entity => entity.IsBoss).ToArray())
            boss.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.False(RunGoal.BossAlive(zone));
        Assert.False(RunGoal.ExitOpen(zone));
    }

    // A boss killed before the save stays dead after it, and the gate stays open: the spawn fact travels with the zone.
    [Fact]
    public void Exit_open_after_save_of_a_slain_boss()
    {
        var galaxy = DemoGalaxy(1);
        var exit = ZoneOf(galaxy, galaxy.Exit);
        Assert.Single(exit.Entities, entity => entity.IsBoss).HeatstrokeDeath.OnNext(Unit.Default);
        var repacked = exit.PackZone();
        Assert.True(repacked.BossSpawned);
        Assert.DoesNotContain(repacked.Entities, entity => entity.Boss);
        Assert.True(RunGoal.ExitOpen(new Zone(_items, _planetSettings, repacked, galaxy.Exit, galaxy)));
    }

    // The gate shares no point with an adjacency wormhole: every one is farther from it than a ship's wormhole reach.
    [Fact]
    public void Exit_gate_shares_no_point_with_a_wormhole()
    {
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

    // The gate sits on the wormholes' radius, in the middle of the widest gap between neighbours, whatever the
    // neighbours' number and order: an empty ring, one, two straddling the +-pi seam, and eight spread evenly (every gap
    // under one radian). Checked against a brute-force sweep of every direction.
    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 40.0 })]
    [InlineData(new[] { 170.0, -170.0 })]
    [InlineData(new[] { 100.0, 10.0, 20.0, 350.0, -90.0 })]
    [InlineData(new[] { 0.0, 45.0, 90.0, 135.0, 180.0, -135.0, -90.0, -45.0 })]
    public void Exit_gate_sits_in_the_widest_gap_at_the_wormhole_radius(double[] neighbourDegrees)
    {
        var galaxy = DemoGalaxy(1);
        var zone = ZoneOf(galaxy, galaxy.Exit);
        const float ratio = 0.75f;
        var radius = zone.Pack.Radius * ratio;
        // Away from the origin and with close neighbours, so a direction taken from raw positions instead of the
        // offset from the zone cannot pass.
        var originalPosition = galaxy.Exit.Position;
        var origin = galaxy.Exit.Position = math.float2(500, -300);
        var original = galaxy.Exit.AdjacentZones;
        galaxy.Exit.AdjacentZones = neighbourDegrees
            .Select(degrees => new GalaxyZone
            {
                Position = origin + math.float2((float) Math.Cos(degrees * Math.PI / 180), (float) Math.Sin(degrees * Math.PI / 180)) * 10
            }).ToList();
        float2 gate;
        try { gate = RunGoal.ExitGatePosition(zone, ratio); }
        finally { galaxy.Exit.AdjacentZones = original; galaxy.Exit.Position = originalPosition; }

        Assert.Equal(radius, math.length(gate), 2);
        if (neighbourDegrees.Length == 0) { Assert.Equal(radius, gate.x, 2); Assert.Equal(0, gate.y, 2); return; }

        double Separation(double angle) => neighbourDegrees.Min(degrees =>
        {
            var difference = Math.Abs(angle - degrees * Math.PI / 180) % (2 * Math.PI);
            return difference > Math.PI ? 2 * Math.PI - difference : difference;
        });
        var best = Enumerable.Range(0, 7200).Max(step => Separation(step * Math.PI / 3600));
        Assert.True(Separation(Math.Atan2(gate.y, gate.x)) >= best - 0.002,
            $"the gate's nearest wormhole is {Separation(Math.Atan2(gate.y, gate.x))} rad away; the widest gap's middle is {best}");
    }

    // Two neighbours straight across leave two gaps of exactly pi: the first in ascending angle (from -pi) wins, so the
    // gate sits at +radius on y, not -radius.
    [Fact]
    public void Exit_gate_takes_the_first_of_equal_gaps()
    {
        var galaxy = DemoGalaxy(1);
        var zone = ZoneOf(galaxy, galaxy.Exit);
        const float ratio = 0.75f;
        var originalPosition = galaxy.Exit.Position;
        var origin = galaxy.Exit.Position = math.float2(500, -300);
        var original = galaxy.Exit.AdjacentZones;
        galaxy.Exit.AdjacentZones = new List<GalaxyZone>
        {
            new GalaxyZone { Position = math.float2(510, -300) },
            new GalaxyZone { Position = math.float2(490, -300) },
        };
        float2 gate;
        try { gate = RunGoal.ExitGatePosition(zone, ratio); }
        finally { galaxy.Exit.AdjacentZones = original; galaxy.Exit.Position = originalPosition; }

        Assert.Equal(0, gate.x, 3);
        Assert.Equal(zone.Pack.Radius * ratio, gate.y, 2);
    }
}
