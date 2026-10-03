/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Scenarios S2 (docs/scenarios-cut.md, R): every new run is a scenario. The fixture is RunStartTests': a scratch copy
// of the shipped catalog, the authored settings and a prelude galaxy at a fixed seed.
public sealed partial class RunStartTests
{
    // A test's own scenario: a script given inline, staged into the fixture's arena or launched whole.
    private sealed class Scripted : Scenario
    {
        private readonly bool _ambient;
        private readonly Action<ScenarioStage> _stage;
        private readonly uint _seed;
        private readonly Func<GalaxyStage, Galaxy> _generate;

        public Scripted(bool ambient, Action<ScenarioStage> stage, uint seed = GalaxySeed, Func<GalaxyStage, Galaxy> generate = null)
        {
            _ambient = ambient;
            _stage = stage;
            _seed = seed;
            _generate = generate ?? (galaxy => galaxy.Prelude());
        }

        public override string Name => "Scripted";
        public override string Brief => "A test's own scenario.";
        public override uint Seed => _seed;
        public override bool Ambient => _ambient;
        public override Galaxy Generate(GalaxyStage stage) => _generate(stage);
        public override void Stage(ScenarioStage stage) => _stage(stage);
    }

    private static IEnumerable<Scenario> EveryScenario => Scenarios.Modes.Concat(Scenarios.Tests);

    // The galaxy inputs the menu hands to RunStart, read from the authored settings.
    private GalaxyStage Inputs(Func<uint> clock = null)
    {
        var authored = AuthoredSettings.Load(FindRepoRoot());
        return new GalaxyStage(
            authored.Read<SectorGenerationSettings>("SectorGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"),
            _cache, new PlayerSettings(), Directory.CreateDirectory(Path.Combine(_root, "Narrative")), _ => { }, null, clock);
    }

    // What MainMenu.Launch and StartGame do with a scenario: its galaxy, its arena, then staging.
    private (Galaxy galaxy, Zone arena, RunStart.Staged staged, List<string> failures) Launch(Scenario scenario, GalaxyStage inputs = null)
    {
        var galaxy = RunStart.Generate(scenario, inputs ?? Inputs());
        var arena = new Zone(_items, _planetSettings, RunStart.GenerateArena(_items, _zoneSettings, galaxy, scenario), galaxy.Entrance, galaxy);
        var failures = new List<string>();
        var staged = RunStart.Stage(_items, arena, scenario, _startingHull, _tutorialSettings, failures);
        return (galaxy, arena, staged, failures);
    }

    private RunStart.Staged Stage(Zone zone, Scenario scenario)
    {
        var failures = new List<string>();
        var staged = RunStart.Stage(_items, zone, scenario, _startingHull, _tutorialSettings, failures);
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.NotNull(staged);
        return staged;
    }

    // Staging that must fail: it returns null and admits nothing. Returns the failures.
    private List<string> Refused(Zone zone, Scenario scenario)
    {
        var entities = zone.Entities.ToList();
        var agents = zone.Agents.ToList();
        var failures = new List<string>();
        Assert.Null(RunStart.Stage(_items, zone, scenario, _startingHull, _tutorialSettings, failures));
        SafeAssert.Unchanged(zone, entities, agents);
        return failures;
    }

    // The rot test: every scenario New Game lists generates its galaxy and stages against the live catalog and the
    // authored settings with no failure. A script naming a design the catalog lost fails here, by name.
    [Fact]
    public void EveryScenarioStages()
    {
        Assert.Equal(EveryScenario.Count(), EveryScenario.Select(scenario => scenario.Name).Distinct().Count());
        foreach (var scenario in EveryScenario)
        {
            var (_, arena, staged, failures) = Launch(scenario, Inputs(() => GalaxySeed));
            Assert.True(failures.Count == 0, $"{scenario.Name}: {string.Join("; ", failures)}");
            Assert.True(staged.Player.IsPlayerShip, $"{scenario.Name}: the player is not flagged");
            SafeAssert.In(arena, staged.Player, $"{scenario.Name}: the player is admitted");
            foreach (var entity in staged.Entities) SafeAssert.In(arena, entity, $"{scenario.Name}: {entity.Name} is admitted");
            Assert.False(string.IsNullOrWhiteSpace(scenario.Brief), $"{scenario.Name} has no brief");
        }
    }

    // The two game modes are what New Game did: the tutorial a prelude galaxy and a starting-hull ship of the
    // protagonist faction at the entrance's origin; the main galaxy a main galaxy and a starting-hull ship there. Both
    // arenas keep their generated ships.
    [Fact]
    public void ModesMatchOldNewGame()
    {
        Assert.Equal("LonginusX", _startingHull);

        var tutorial = Launch(new TutorialGalaxy(), Inputs(() => GalaxySeed));
        Assert.Empty(tutorial.failures);
        Assert.True(tutorial.galaxy.IsPrelude, "the tutorial is a prelude galaxy");
        Assert.Equal(_startingHull, tutorial.staged.Player.HullData.Name);
        Assert.Same(tutorial.galaxy.ResolveFaction(_tutorialSettings.ProtagonistFaction), tutorial.staged.Player.Faction);
        Assert.Equal(float3.zero, tutorial.staged.Player.Position);
        Assert.Empty(tutorial.staged.Entities);
        Assert.Contains(tutorial.arena.Pack.Entities, entity => entity is ShipPack { IsPlayerShip: false });

        var main = Launch(new MainGalaxy(), Inputs(() => GalaxySeed));
        Assert.Empty(main.failures);
        Assert.False(main.galaxy.IsPrelude, "the main galaxy is not a prelude");
        Assert.Equal(_startingHull, main.staged.Player.HullData.Name);
        Assert.Equal(float3.zero, main.staged.Player.Position);
        Assert.Empty(main.staged.Entities);
        Assert.Contains(main.arena.Pack.Entities, entity => entity is ShipPack { IsPlayerShip: false });
    }

    // Everything a galaxy's layout fixes: each zone's name, position, owner, factions and links, the homes, the
    // entrance, the exit and the background.
    private static string Layout(Galaxy galaxy) =>
        string.Join(";", galaxy.Zones.Select(zone =>
            $"{zone.Name}@{zone.Position}:{zone.Owner?.Name}:[{string.Join(",", zone.Factions.Select(faction => faction.Name))}]" +
            $":[{string.Join(",", zone.AdjacentZones.Select(adjacent => Array.IndexOf(galaxy.Zones, adjacent)).OrderBy(i => i))}]")) +
        $" homes {string.Join(",", galaxy.HomeZones.Select(home => $"{home.Key.Name}={Array.IndexOf(galaxy.Zones, home.Value)}").OrderBy(home => home))}" +
        $" entrance {Array.IndexOf(galaxy.Zones, galaxy.Entrance)} exit {Array.IndexOf(galaxy.Zones, galaxy.Exit)} noise {galaxy.Background.NoisePosition}";

    // An arena's bodies and every entity in it, down to each item's design, maker, workmanship and role fills.
    private string Layout(ZonePack arena) =>
        $"{arena.Radius} {arena.Mass} planets [{string.Join(",", arena.Planets.Select(planet => _cache.Get(planet)).Select(body => $"{body.GetType().Name}:{body.Mass}"))}]" +
        $" orbits [{string.Join(",", arena.Orbits.Select(orbit => _cache.Get(orbit).Distance))}]" +
        $" entities [{string.Join(",", arena.Entities.Select(Gear))}]";

    // What a launch staged: the player and everything placed, each where it is and with exactly what it carries.
    private string Fits(RunStart.Staged staged) =>
        string.Join(" | ", staged.Entities.Prepend(staged.Player).Select(entity => $"{entity.Name}@{entity.Position}:{Gear(EntitySerializer.Pack(entity))}"));

    private string Gear(EntityPack pack) =>
        $"{pack.GetType().Name}{(pack is ShipPack ship ? "@" + ship.Position : "")}:{string.Join("/", EntitySerializer.Items(pack).Select(Item))}";

    private string Item(ItemInstance item)
    {
        var name = _items.GetData(item)?.Name;
        if (!(item is CraftedItemInstance crafted)) return name;
        var lot = _items.GetLot(crafted);
        return $"{name}<{(lot.Origin as Attributed)?.Faction.Key.Value}>~{lot.Quality}" +
               string.Concat((lot.Roles ?? new List<RoleFill>()).Select(fill => $"/{fill.Role}={fill.Quality}"));
    }

    // A fixed seed fixes the galaxy, its background, its arena and every fit, each item's maker and workmanship
    // included, launch after launch in one session, whatever ran in between. Seed zero takes the clock, so two launches
    // at two clock readings are two galaxies.
    [Fact]
    public void SeedRules()
    {
        foreach (var scenario in Scenarios.Tests)
        {
            Assert.NotEqual(0u, scenario.Seed);
            var first = Launch(scenario);
            Launch(new MainGalaxy(), Inputs(() => 4242));
            _items.Random = new CultMath.Random(0xC0FFEE); // play draws from the run's item random
            var second = Launch(scenario);
            Assert.True(first.failures.Count == 0, $"{scenario.Name}: {string.Join("; ", first.failures)}");
            Assert.Equal(Layout(first.galaxy), Layout(second.galaxy));
            Assert.Equal(Layout(first.arena.Pack), Layout(second.arena.Pack));
            Assert.Equal(Fits(first.staged), Fits(second.staged));
        }

        var clock = new Queue<uint>(new[] { 1000u, 2000u });
        var inputs = Inputs(() => clock.Dequeue());
        var everyLaunchNew = new Scripted(true, _ => { }, seed: 0, generate: galaxy => galaxy.Main());
        var a = RunStart.Generate(everyLaunchNew, inputs);
        Assert.Equal(1000u, inputs.Seed);
        var b = RunStart.Generate(everyLaunchNew, inputs);
        Assert.Equal(2000u, inputs.Seed);
        Assert.NotEqual(Layout(a), Layout(b));
    }

    // Generating a galaxy writes nothing a later one reads: a prelude leaves the catalog's faction influence as authored.
    [Fact]
    public void AGalaxyLeavesTheCatalogsFactionsAsAuthored()
    {
        var authored = _cache.GetAll<Faction>().ToDictionary(faction => faction, faction => faction.InfluenceDistance);
        RunStart.Generate(new TutorialGalaxy(), Inputs());
        RunStart.Generate(new MainGalaxy(), Inputs());
        Assert.All(authored, pair => Assert.Equal(pair.Value, pair.Key.InfluenceDistance));
    }

    // Every zone is linked into one graph. These seeds left zones with no link at all (the vendored float-precision
    // convex-hull triangulation dropped them), so the constructor threw KeyNotFoundException placing homes (prelude)
    // or the entrance (main). Each galaxy's links are a subset of its zone positions' Delaunay edges, and the
    // triangulation agrees with a brute-force empty-circumcircle oracle.
    [Theory]
    [InlineData(103u, true)]
    [InlineData(134u, true)]
    [InlineData(137u, true)]
    [InlineData(173u, true)]
    [InlineData(227u, true)]
    [InlineData(285u, true)]
    [InlineData(34u, false)]
    public void EveryZoneIsLinked(uint seed, bool prelude)
    {
        var galaxy = RunStart.Generate(new Scripted(true, _ => { }, seed, stage => prelude ? stage.Prelude() : stage.Main()), Inputs());
        Assert.All(galaxy.Zones, zone => Assert.Equal(galaxy.Zones.Length, zone.Distance.Count));

        var positions = galaxy.Zones.Select(zone => zone.Position).ToArray();
        var edges = Delaunay.Edges(positions);
        Assert.Equal(BruteForceDelaunay(positions), edges);
        var delaunay = new HashSet<(int, int)>(edges);
        for (var i = 0; i < galaxy.Zones.Length; i++)
            foreach (var adjacent in galaxy.Zones[i].AdjacentZones)
            {
                var j = Array.IndexOf(galaxy.Zones, adjacent);
                Assert.Contains((Math.Min(i, j), Math.Max(i, j)), delaunay);
            }
    }

    // Every triangle whose circumcircle holds no other point, as sorted edges.
    private static List<(int a, int b)> BruteForceDelaunay(float2[] points)
    {
        var edges = new SortedSet<(int a, int b)>();
        for (var i = 0; i < points.Length; i++)
        for (var j = i + 1; j < points.Length; j++)
        for (var k = j + 1; k < points.Length; k++)
        {
            double ax = points[i].x, ay = points[i].y, bx = points[j].x, by = points[j].y, cx = points[k].x, cy = points[k].y;
            var d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (d == 0) continue;
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            var ux = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            var uy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            var r2 = (ax - ux) * (ax - ux) + (ay - uy) * (ay - uy);
            var empty = true;
            for (var m = 0; m < points.Length && empty; m++)
                if (m != i && m != j && m != k)
                {
                    double dx = points[m].x - ux, dy = points[m].y - uy;
                    empty = dx * dx + dy * dy >= r2;
                }
            if (!empty) continue;
            edges.Add((i, j));
            edges.Add((j, k));
            edges.Add((i, k));
        }
        return edges.ToList();
    }

    // A scenario naming one unknown design admits nothing, not even the parts that built, and names the design.
    [Fact]
    public void StagingIsAllOrNothing()
    {
        var failures = Refused(Arena(true), new Scripted(true, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(0, 0));
            stage.Place(stage.Generated("LonginusX"), float2(300, 0), stance: ScenarioStance.Hostile, piloted: true);
            stage.Place(stage.Fit("LonginusX", ("No Such Gun", int2(1, 8), ItemRotation.None)), float2(-300, 0));
        }));
        Assert.Contains("No Such Gun", Assert.Single(failures));
    }

    // A stance holds both ways between an entity and the player. A piloted entity has exactly one agent; an unpiloted
    // one has none, and nothing steers it. (It is not frozen: the zone's gravity still moves it, as it moves any ship,
    // so the rule is observed on its steering inputs.)
    [Fact]
    public void StanceAndPilot()
    {
        var zone = Arena(true);
        var staged = Stage(zone, new Scripted(true, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(0, 0));
            stage.Place(stage.Generated("LonginusX"), float2(300, 0), stance: ScenarioStance.Hostile, piloted: true);
            stage.Place(stage.Bare("LonginusX"), float2(-300, 0), stance: ScenarioStance.Neutral, piloted: false);
        }));
        var player = staged.Player;
        var hostile = staged.Entities[0];
        var neutral = staged.Entities[1];

        Assert.True(player.IsHostileTo(hostile) && hostile.IsHostileTo(player), "hostile both ways");
        Assert.False(player.IsHostileTo(neutral) || neutral.IsHostileTo(player), "neutral both ways");
        // Admission activates: the live hostility each side tracks (what targeting and grudges read) agrees.
        Assert.True(player.EntityHostility[hostile] && hostile.EntityHostility[player]);
        Assert.False(player.EntityHostility[neutral] || neutral.EntityHostility[player]);

        Assert.Equal(1, AgentsOf(zone, hostile));
        Assert.Equal(0, AgentsOf(zone, neutral));
        Assert.Equal(0, AgentsOf(zone, player));
        var look = neutral.LookDirection;
        for (var tick = 0; tick < 50; tick++)
        {
            zone.Update(.1f);
            Assert.Equal(float2.zero, ((Ship) neutral).MovementDirection);
            Assert.Equal(look, neutral.LookDirection);
        }
        Assert.Equal(0, AgentsOf(zone, neutral));
    }

    // A stance lasts the sitting: the player docking and undocking leaves it standing both ways. It goes with an entity
    // gone for good: once an entity is destroyed, the player holds no stance toward it.
    [Fact]
    public void AStanceSurvivesDockingButNotDestruction()
    {
        var zone = Arena(true);
        var staged = Stage(zone, new Scripted(true, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(0, 0));
            stage.Place(stage.Bare("LonginusX"), float2(300, 0), stance: ScenarioStance.Hostile);
            stage.Place(stage.Bare("LonginusX"), float2(-300, 0), stance: ScenarioStance.Hostile);
        }));
        var player = staged.Player;
        var hostile = staged.Entities[0];
        var doomed = staged.Entities[1];
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);

        Assert.NotNull(station.TryDock(player));
        SafeAssert.NotIn(zone, player, "a docked ship is out of the zone");
        Assert.True(station.TryUndock(player));
        Assert.True(player.IsHostileTo(hostile) && hostile.IsHostileTo(player), "the stance was lost over docking");
        Assert.True(player.EntityHostility[hostile] && hostile.EntityHostility[player], "the live hostility was lost over docking");

        // Bare fits carry no faction, so without its stance the derived rule is not hostile
        Assert.True(player.Faction == null && doomed.Faction == null, "the fixture's ships are factionless");
        Assert.True(player.IsHostileTo(doomed));
        doomed.Hull.Durability = 0;
        doomed.HullDamage.OnNext(1);
        SafeAssert.NotIn(zone, doomed, "a destroyed entity leaves the zone");
        Assert.False(player.IsHostileTo(doomed), "the stance toward a destroyed entity outlived it");
    }

    // Undocking admits the ship unpiloted: back in the zone, active (its live hostility is tracked), with no agent.
    [Fact]
    public void UndockingAdmitsWithNoAgent()
    {
        var zone = Arena(true);
        var player = Stage(zone, new TutorialGalaxy()).Player;
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);
        var agents = zone.Agents.Count;

        Assert.NotNull(station.TryDock(player));
        SafeAssert.NotIn(zone, player, "a docked ship is out of the zone");
        Assert.True(station.TryUndock(player));
        SafeAssert.In(zone, player, "an undocked ship is back in the zone");
        Assert.Equal(agents, zone.Agents.Count);
        Assert.Equal(0, AgentsOf(zone, player));
        Assert.True(player.EntityHostility.ContainsKey(station), "an undocked ship is active");
    }

    // Ambient false keeps the stations and drops every generated ship and turret; ambient keeps them. The arena is the
    // galaxy's entrance pack. The entrance holds a story station, placed as the narrative would place one, so a story
    // station's own turrets are dropped too.
    [Fact]
    public void AQuietArenaHasNoGeneratedShipOrTurretButKeepsItsStations()
    {
        (int ships, int turrets, int stations) Census(ZonePack pack) => (
            pack.Entities.OfType<ShipPack>().Count(),
            pack.Entities.OfType<OrbitalEntityPack>().Count(e => ((HullData) _cache.Get(e.Hull.Data)).HullType == HullType.Turret),
            pack.Entities.OfType<OrbitalEntityPack>().Count(e => ((HullData) _cache.Get(e.Hull.Data)).HullType == HullType.Station));

        _galaxy.Entrance.Locations.Add(new LocationStory
        {
            Zone = _galaxy.Entrance, Name = "story", Type = LocationType.Station, Faction = _protagonist, Turrets = 2
        });
        var ambient = Census(RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scripted(true, _ => { })));
        var quietPack = RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scripted(false, _ => { }));
        var quiet = Census(quietPack);

        Assert.Same(quietPack, _galaxy.Entrance.PackedContents);
        Assert.True(ambient.ships > 0 && ambient.turrets > 0, $"an ambient arena generated {ambient}");
        Assert.Equal(0, quiet.ships);
        Assert.Equal(0, quiet.turrets);
        Assert.True(quiet.stations > 0);
        Assert.Equal(ambient.stations, quiet.stations);
        Assert.Contains(quietPack.Entities.OfType<OrbitalEntityPack>(), station => station.Story == 0);
    }

    // A scenario's player is its fit (here a catalog preset), at the scripted place and heading, carrying its cargo.
    [Fact]
    public void AScenarioPlayerIsItsFitAtItsPlaceWithItsCargo()
    {
        var generator = new LoadoutGenerator(ref _items.Random, _items, null, null, null, 2);
        var preset = Loadouts.Capture(_items, (Ship) EntitySerializer.Unpack(_items, null, generator.GenerateShipLoadout(hull => hull.Name == "Djinni")), "test djinni");
        _cache.Upsert(preset);
        // Two one-cell designs a manufacturer makes, by name, so the cargo is ordinary branded stock
        var cargo = _cache.GetAll<GearData>()
            .Where(design => design.Shape.Coordinates.Length == 1)
            .Where(design => _cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(_cache.RefOf(design).Key)))
            .OrderBy(design => design.Name, StringComparer.Ordinal)
            .Take(2)
            .ToList();
        Assert.Equal(2, cargo.Count);

        var zone = Arena(true);
        var player = Stage(zone, new Scripted(true, stage =>
            stage.Cargo(stage.Player(stage.Preset("test djinni"), float2(120, -40), float2(3, 4)), cargo.Select(design => design.Name).ToArray()))).Player;

        Assert.Equal("Djinni", player.HullData.Name);
        SafeAssert.In(zone, player, "the player is admitted");
        Assert.Equal(float3(120, 0, -40), player.Position);
        Assert.True(length(player.Direction - float2(.6f, .8f)) < 1e-5f, $"heading {player.Direction}");
        Assert.Equal(
            preset.Slots.Select(slot => (slot.Position, slot.Design.Key)).OrderBy(s => s.ToString()),
            player.Equipment.Concat<EquippedItem>(player.CargoBays).Concat(player.DockingBays)
                .Where(item => item != player.EquippedHull)
                .Select(item => (item.Position, _cache.RefOf(_items.GetData(item.EquippableItem)).Key))
                .OrderBy(s => s.ToString()));
        Assert.Equal(cargo.Select(design => _cache.RefOf(design).Key.Value).OrderBy(k => k),
            player.CargoBays.First().Cargo.Keys.Select(item => item.Data.Key.Value).OrderBy(k => k));
    }

    // A turret hull stages as an orbital entity with no orbit, which stays where it is put.
    [Fact]
    public void ATurretStagesAsAStationaryOrbitalEntity()
    {
        var zone = Arena(true);
        var staged = Stage(zone, new Scripted(true, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(0, 0));
            stage.Place(stage.Bare(TurretHull().Name), float2(200, 50), stance: ScenarioStance.Hostile);
        }));
        var turret = Assert.IsType<OrbitalEntity>(staged.Entities[0]);
        Assert.False(turret.OrbitData.IsSet());
        SafeAssert.In(zone, turret, "the turret is admitted");
        for (var tick = 0; tick < 20; tick++) zone.Update(.1f);
        Assert.Equal(float2(200, 50), turret.Position.xz);
    }

    // A run has exactly one player, and it is a ship; a station is never staged, and a turret is never piloted. Each
    // is a staging failure, named, with nothing admitted.
    [Fact]
    public void AMisplacedHullOrPlayerIsAStagingFailure()
    {
        var station = _cache.GetAll<HullData>().First(hull => hull.HullType == HullType.Station).Name;
        var turret = TurretHull().Name;
        foreach (var (script, expected) in new (Action<ScenarioStage>, string)[]
                 {
                     (stage => { stage.Player(stage.Bare("Djinni"), float2(0, 0)); stage.Place(stage.Bare(station), float2(200, 0)); }, $"{station} is a station hull"),
                     (stage => stage.Player(stage.Bare(turret), float2(0, 0)), $"player: {turret} is not a ship hull"),
                     (stage => { stage.Player(stage.Bare("Djinni"), float2(0, 0)); stage.Place(stage.Bare(turret), float2(200, 0), piloted: true); }, "cannot be piloted"),
                     (stage => stage.Place(stage.Bare("Djinni"), float2(200, 0)), "places no player"),
                     (stage => { stage.Player(stage.Bare("Djinni"), float2(0, 0)); stage.Player(stage.Bare("Djinni"), float2(9, 0)); }, "a second player"),
                 })
        {
            var failures = Refused(Arena(true), new Scripted(true, script));
            Assert.Contains(failures, failure => failure.Contains(expected));
        }
    }

    // Arcs' geometry: the bow hull sits inside every one of the player's mount arcs, the stern hull outside all of them,
    // and the turret is hostile.
    [Fact]
    public void ArcsGeometry()
    {
        var staged = Stage(Arena(false), new Arcs());
        var player = staged.Player;
        var bow = staged.Entities[0];
        var stern = staged.Entities[1];
        var turret = Assert.IsType<OrbitalEntity>(staged.Entities[2]);
        Assert.Equal((Arcs.Bow, Arcs.Stern, Arcs.Turret), (bow.Position.xz, stern.Position.xz, turret.Position.xz));

        var mounts = player.Equipment.Where(item => item.Behaviors.OfType<Weapon>().Any()).ToList();
        Assert.NotEmpty(mounts);
        foreach (var mount in mounts)
        {
            Assert.True(FireControl.InArc(mount, bow.Position - player.Position), $"{mount.Data.Name} cannot bear on the bow hull");
            Assert.False(FireControl.InArc(mount, stern.Position - player.Position), $"{mount.Data.Name} bears on the stern hull");
        }
        Assert.True(player.IsHostileTo(turret) && turret.IsHostileTo(player), "the turret is hostile");
        Assert.False(player.IsHostileTo(bow) || player.IsHostileTo(stern), "the bare hulls are neutral");
    }

    // Q3 (operator, 2026-09-30): a design no product sells is a scenario test design and nothing else. A test design
    // is one some scenario's staged entities carry, fitted or in cargo. These are the unsold designs none carries.
    private List<string> UnsoldAndUnplaced(IEnumerable<RunStart.Staged> runs)
    {
        var sold = _cache.GetAll<FactionProductData>().Select(product => product.Design.Key).ToHashSet();
        var placed = new HashSet<CultRecordKey>();
        foreach (var entity in runs.SelectMany(run => run.Entities.Prepend(run.Player)))
        {
            foreach (var item in entity.Equipment.Concat<EquippedItem>(entity.CargoBays).Concat(entity.DockingBays))
                placed.Add(_cache.RefOf(_items.GetData(item.EquippableItem)).Key);
            foreach (var item in entity.CargoBays.SelectMany(bay => bay.Cargo.Keys)) placed.Add(item.Data.Key);
        }
        return _cache.GetAll<EquippableItemData>()
            .Where(design => !sold.Contains(_cache.RefOf(design).Key) && !placed.Contains(_cache.RefOf(design).Key))
            .Select(design => design.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    // The shipped catalog holds no unsold design that no scenario carries (today, none unsold at all). A new unsold
    // design is flagged until a scenario fits it or carries it as cargo.
    [Fact]
    public void EveryUnsoldDesignIsAScenarioTestDesign()
    {
        var runs = EveryScenario.Select(scenario => Launch(scenario, Inputs(() => GalaxySeed)).staged).ToList();
        Assert.Empty(UnsoldAndUnplaced(runs));

        void Probe(string name)
        {
            var shape = new Shape(1, 1);
            shape[int2(0, 0)] = true;
            _cache.Upsert(new GearData { Name = name, Hardpoint = HardpointType.Tool, Shape = shape, Durability = 1 });
        }
        Probe("Census Fitted");
        Probe("Census Carried");
        Assert.Equal(new[] { "Census Carried", "Census Fitted" }, UnsoldAndUnplaced(runs));

        runs.Add(Stage(Arena(false), new Scripted(false, stage =>
        {
            var ship = stage.Player(stage.Fit("Djinni", ("Census Fitted", int2(1, 6), ItemRotation.None), ("Store-All Plus", int2(3, 6), ItemRotation.None)), float2(0, 0));
            stage.Cargo(ship, "Census Carried");
        })));
        Assert.Empty(UnsoldAndUnplaced(runs));
    }

    // Whether a run is the prelude is the galaxy's fact (Galaxy.IsPrelude); a save records it with no second owner.
    [Fact]
    public void SaveReadsPrelude()
    {
        var arena = Arena(true);
        var (prelude, _) = RunSave.Capture(_cache, _galaxy, arena, null, new SavedActionBarBinding[0]);
        Assert.True(prelude.IsTutorial);

        var (main, _) = RunSave.Capture(_cache, MainSectorGalaxy(), arena, null, new SavedActionBarBinding[0]);
        Assert.False(main.IsTutorial);
    }

    // PlayerSettings Key 2, the never-written tutorial flag, is retired: a player.cc written with it still loads, every other field intact.
    [Fact]
    public void OldPlayerSettingsLoad()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(PlayerSettings).Assembly);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(7);
        writer.Write("Pilot");
        writer.WriteNil();                         // Key 1, retired before this cut
        writer.Write(true);                        // Key 2, the retired tutorial flag
        MessagePackSerializer.Serialize(ref writer, new Dictionary<string, string> { ["story"] = "hash" }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerGameplaySettings { TemperatureUnit = TemperatureUnit.Kelvin, SignificantDigits = 5 }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerInputSettings { ActionBarInputs = { "<Keyboard>/1" } }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerGraphicsSettings { NebulaQuality = Quality.High, ShowAsteroidsInMinimap = true }, options);
        writer.Flush();

        var settings = MessagePackSerializer.Deserialize<PlayerSettings>(buffer.WrittenMemory, options);

        Assert.Equal("Pilot", settings.Name);
        Assert.Equal("hash", settings.HashedStoryFiles["story"]);
        Assert.Equal((TemperatureUnit.Kelvin, 5), (settings.GameplaySettings.TemperatureUnit, settings.GameplaySettings.SignificantDigits));
        Assert.Equal("<Keyboard>/1", Assert.Single(settings.InputSettings.ActionBarInputs));
        Assert.Equal((Quality.High, true), (settings.GraphicsSettings.NebulaQuality, settings.GraphicsSettings.ShowAsteroidsInMinimap));
    }
}
