/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Scenarios Cut 1 (docs/scenarios-cut.md, 2.2): RunStart owns what a new run starts with, and Zone.Admit is the one
// admission into a zone. Every test stages into a real arena: the entrance zone of a prelude galaxy at a fixed seed,
// built from the shipped catalog and the authored settings (Assets/Resources/Settings.asset). The presets and
// scenarios live in a scratch copy of the catalog, because the shipped one holds none.
public sealed class RunStartTests : IDisposable
{
    private const uint GalaxySeed = 1;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-runstart-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly Galaxy _galaxy;
    private readonly ZoneGenerationSettings _zoneSettings;
    private readonly PlanetSettings _planetSettings;
    private readonly string _startingHull;
    private readonly Faction _protagonist;

    public RunStartTests()
    {
        var repo = FindRepoRoot();
        Directory.CreateDirectory(_root);
        var catalog = Path.Combine(_root, "Aetheria.cc");
        File.Copy(Path.Combine(repo, "GameData", "Aetheria.cc"), catalog);

        // A registry scoped to the shipped assembly's own [CultDocument] types, as RestoredHullsTests composes it, so
        // this test assembly's own documents never reach the real catalog's validation.
        _cache = new CultCache(Registry());
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(_root, "run.cc")), AetheriaStores.RunTypes);

        var authored = AuthoredSettings.Load(repo);
        _zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
        _planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
        var tutorial = authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
        _galaxy = new Galaxy(tutorial, authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), _cache, new PlayerSettings(),
            Directory.CreateDirectory(Path.Combine(_root, "Narrative")), _ => { }, null, GalaxySeed);
        _items = new ItemManager(_cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
        _protagonist = _galaxy.ResolveFaction(tutorial.ProtagonistFaction);

        // GameSettings.StartingHullName is a plain field of the Unity settings object, not a ServerShared subtree
        _startingHull = File.ReadLines(Path.Combine(repo, "Assets", "Resources", "Settings.asset"))
            .Select(line => Regex.Match(line, @"^  StartingHullName: (.+)$"))
            .First(match => match.Success).Groups[1].Value.Trim();
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static CultDocumentRegistry Registry() => CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false })
        .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }

    private Zone Arena(Scenario scenario) =>
        new Zone(_items, _planetSettings, RunStart.GenerateArena(_items, _zoneSettings, _galaxy, scenario), _galaxy.Entrance, _galaxy);

    private HullData Hull(string name) => _cache.GetAll<HullData>().Single(hull => hull.Name == name);

    // The first turret hull, by name, that a manufacturer makes: a real turret, not a fixture.
    private HullData TurretHull() => _cache.GetAll<HullData>()
        .Where(hull => hull.HullType == HullType.Turret)
        .Where(hull => _cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(_cache.RefOf(hull).Key)))
        .OrderBy(hull => hull.Name, StringComparer.Ordinal)
        .First();

    private CultRecordRef<Loadout> Preset(Loadout loadout)
    {
        var existing = _cache.GetAll<Loadout>().FirstOrDefault(other => other.Name == loadout.Name);
        if (existing != null) return _cache.RefOf(existing);
        _cache.Upsert(loadout);
        return _cache.RefOf(loadout);
    }

    private CultRecordRef<Loadout> Bare(HullData hull) =>
        Preset(new Loadout { Name = "bare " + hull.Name, Hull = _cache.RefOf(hull), WeaponGroups = new int[0][] });

    // A fully fitted preset: a generated ship of this hull, captured.
    private Loadout Fitted(string hullName)
    {
        var generator = new LoadoutGenerator(ref _items.Random, _items, null, null, null, 2);
        var ship = (Ship) EntitySerializer.Unpack(_items, null, generator.GenerateShipLoadout(hull => hull.Name == hullName));
        Assert.NotEmpty(ship.CargoBays);
        return Loadouts.Capture(_items, ship, "fitted " + hullName);
    }

    private static ScenarioShip At(CultRecordRef<Loadout> preset, float x, float z) =>
        new ScenarioShip { Loadout = preset, Position = float2(x, z) };

    private static ScenarioEntity Placed(CultRecordRef<Loadout> preset, float x, float z, ScenarioStance stance, bool piloted) =>
        new ScenarioEntity { Loadout = preset, Position = float2(x, z), Stance = stance, Piloted = piloted };

    private Scenario Quiet(ScenarioShip player, params ScenarioEntity[] entities) =>
        new Scenario { Name = "test", Seed = GalaxySeed, Ambient = false, Player = player, Entities = entities.ToList() };

    private RunStart.Staged Stage(Zone zone, Scenario scenario)
    {
        var failures = new List<string>();
        var staged = RunStart.Stage(_items, zone, scenario, _startingHull, _protagonist, failures);
        Assert.Empty(failures);
        Assert.NotNull(staged);
        return staged;
    }

    private static int AgentsOf(Zone zone, Entity entity) => zone.Agents.Count(agent => agent.Ship == entity);

    // New Game is the absent-scenario case: the authored starting hull, flagged as the player's, admitted at the
    // origin, for the protagonist faction, with no agent. The hull name is an input: staging honours another one too.
    [Fact]
    public void WithNoScenarioThePlayerIsTheStartingHullAtTheOrigin()
    {
        Assert.Equal("LonginusX", _startingHull);
        foreach (var hullName in new[] { _startingHull, "Djinni" })
        {
            var zone = Arena(null);
            var failures = new List<string>();
            var staged = RunStart.Stage(_items, zone, null, hullName, _protagonist, failures);
            Assert.Empty(failures);
            var player = staged.Player;
            Assert.Equal(hullName, player.HullData.Name);
            Assert.True(player.IsPlayerShip);
            SafeAssert.In(zone, player, "the player is admitted");
            Assert.True(zone == player.Zone, "the player's zone is the arena");
            Assert.Equal(float3.zero, player.Position);
            Assert.Same(_protagonist, player.Faction);
            Assert.Equal(0, AgentsOf(zone, player));
            Assert.Empty(staged.Entities);
        }
    }

    // A scenario's player is its preset, fitted slot for slot, at the authored position and heading, carrying its cargo.
    [Fact]
    public void AScenarioPlayerIsItsPresetAtItsPlaceWithItsCargo()
    {
        var fitted = Fitted("Djinni");
        var preset = Preset(fitted);
        // Two one-cell designs a manufacturer makes, by name, so the cargo is ordinary branded stock
        var cargo = _cache.GetAll<GearData>()
            .Where(design => design.Shape.Coordinates.Length == 1)
            .Where(design => _cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(_cache.RefOf(design).Key)))
            .OrderBy(design => design.Name, StringComparer.Ordinal)
            .Take(2)
            .Select(design => new CultRecordRef<EquippableItemData>(_cache.RefOf(design).Key))
            .ToList();
        Assert.Equal(2, cargo.Count);
        var zone = Arena(null);
        var scenario = Quiet(new ScenarioShip { Loadout = preset, Position = float2(120, -40), Direction = float2(3, 4), Cargo = cargo });

        var player = Stage(zone, scenario).Player;

        Assert.Equal("Djinni", player.HullData.Name);
        Assert.True(player.IsPlayerShip);
        SafeAssert.In(zone, player, "the player is admitted");
        Assert.Equal(float3(120, 0, -40), player.Position);
        Assert.True(length(player.Direction - float2(.6f, .8f)) < 1e-5f, $"heading {player.Direction}");
        Assert.Equal(
            fitted.Slots.Select(slot => (slot.Position, slot.Design.Key)).OrderBy(s => s.ToString()),
            player.Equipment.Concat<EquippedItem>(player.CargoBays).Concat(player.DockingBays)
                .Where(item => item != player.EquippedHull)
                .Select(item => (item.Position, _cache.RefOf(_items.GetData(item.EquippableItem)).Key))
                .OrderBy(s => s.ToString()));
        Assert.Equal(cargo.Select(design => design.Key.Value).OrderBy(k => k),
            player.CargoBays.First().Cargo.Keys.Select(item => item.Data.Key.Value).OrderBy(k => k));
    }

    // A piloted entity gets exactly one agent; an unpiloted one gets none, and nothing steers it. (It is not frozen:
    // the zone's gravity still moves it, as it moves any ship, so the rule is observed on its steering inputs.)
    [Fact]
    public void APilotedEntityHasOneAgentAndAnUnpilotedOneNoneAndStaysPut()
    {
        var zone = Arena(null);
        var staged = Stage(zone, Quiet(At(Bare(Hull("Djinni")), 0, 0),
            Placed(Preset(Fitted("LonginusX")), 300, 0, ScenarioStance.Neutral, piloted: true),
            Placed(Bare(Hull("LonginusX")), -300, 0, ScenarioStance.Neutral, piloted: false)));
        var piloted = staged.Entities[0];
        var unpiloted = staged.Entities[1];

        Assert.Equal(1, AgentsOf(zone, piloted));
        Assert.Equal(0, AgentsOf(zone, unpiloted));
        Assert.Equal(0, AgentsOf(zone, staged.Player));
        SafeAssert.In(zone, piloted, "the piloted entity is admitted");
        SafeAssert.In(zone, unpiloted, "the unpiloted entity is admitted");

        var look = unpiloted.LookDirection;
        for (var tick = 0; tick < 50; tick++)
        {
            zone.Update(.1f);
            Assert.Equal(float2.zero, ((Ship) unpiloted).MovementDirection);
            Assert.Equal(look, unpiloted.LookDirection);
        }
        Assert.Equal(0, AgentsOf(zone, unpiloted));
    }

    // A stance holds both ways between the entity and the player.
    [Fact]
    public void AStanceIsSetBothWays()
    {
        var longinus = Bare(Hull("LonginusX"));
        var staged = Stage(Arena(null), Quiet(At(Bare(Hull("Djinni")), 0, 0),
            Placed(longinus, 300, 0, ScenarioStance.Hostile, piloted: false),
            Placed(longinus, -300, 0, ScenarioStance.Neutral, piloted: false)));
        var player = staged.Player;
        var hostile = staged.Entities[0];
        var neutral = staged.Entities[1];

        Assert.True(player.IsHostileTo(hostile));
        Assert.True(hostile.IsHostileTo(player));
        Assert.False(player.IsHostileTo(neutral));
        Assert.False(neutral.IsHostileTo(player));

        // Admission activates: the live hostility each side tracks (what targeting and grudges read) agrees.
        Assert.True(player.EntityHostility[hostile]);
        Assert.True(hostile.EntityHostility[player]);
        Assert.False(player.EntityHostility[neutral]);
        Assert.False(neutral.EntityHostility[player]);
    }

    // Ambient: false keeps the stations and drops every generated ship and turret; ambient, and a plain New Game, keep
    // them. The arena is the galaxy's entrance pack. The entrance holds a story station, placed as the narrative would
    // place one, so a story station's own turrets are dropped too.
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
        var plain = Census(RunStart.GenerateArena(_items, _zoneSettings, _galaxy, null));
        var ambient = Census(RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scenario { Ambient = true }));
        var quietPack = RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scenario { Ambient = false });
        var quiet = Census(quietPack);

        Assert.Same(quietPack, _galaxy.Entrance.PackedContents);
        Assert.True(plain.ships > 0 && plain.turrets > 0, $"a plain arena generated {plain}");
        Assert.True(ambient.ships > 0 && ambient.turrets > 0, $"an ambient arena generated {ambient}");
        Assert.Equal(0, quiet.ships);
        Assert.Equal(0, quiet.turrets);
        Assert.True(quiet.stations > 0);
        Assert.Equal(ambient.stations, quiet.stations);
        Assert.Contains(quietPack.Entities.OfType<OrbitalEntityPack>(), station => station.Story == 0);
    }

    // A turret preset stages as an orbital entity with no orbit, which stays where it is put.
    [Fact]
    public void ATurretPresetStagesAsAStationaryOrbitalEntity()
    {
        var zone = Arena(null);
        var staged = Stage(zone, Quiet(At(Bare(Hull("Djinni")), 0, 0),
            Placed(Bare(TurretHull()), 200, 50, ScenarioStance.Hostile, piloted: false)));
        var turret = Assert.IsType<OrbitalEntity>(staged.Entities[0]);
        Assert.False(turret.OrbitData.IsSet());
        SafeAssert.In(zone, turret, "the turret is admitted");
        for (var tick = 0; tick < 20; tick++) zone.Update(.1f);
        Assert.Equal(float2(200, 50), turret.Position.xz);
    }

    // A station preset is refused, and so is a turret as the player or a piloted turret: each is a staging failure
    // naming the hull, with nothing admitted.
    [Fact]
    public void AStationPresetOrAMisplacedTurretIsAStagingFailure()
    {
        var station = _cache.GetAll<HullData>().First(hull => hull.HullType == HullType.Station);
        var turret = TurretHull();
        var djinni = Bare(Hull("Djinni"));
        foreach (var (scenario, expected) in new[]
                 {
                     (Quiet(At(djinni, 0, 0), Placed(Bare(station), 200, 0, ScenarioStance.Neutral, false)), $"{station.Name} is a station hull"),
                     (Quiet(At(Bare(turret), 0, 0)), $"player: {turret.Name} is not a ship hull"),
                     (Quiet(At(djinni, 0, 0), Placed(Bare(turret), 200, 0, ScenarioStance.Neutral, true)), "cannot be piloted"),
                 })
        {
            var zone = Arena(null);
            var entities = zone.Entities.ToList();
            var agents = zone.Agents.ToList();
            var failures = new List<string>();
            Assert.Null(RunStart.Stage(_items, zone, scenario, _startingHull, _protagonist, failures));
            Assert.Contains(failures, failure => failure.Contains(expected));
            SafeAssert.Unchanged(zone, entities, agents);
            Assert.Contains(RunStart.Check(_items, scenario), failure => failure.Contains(expected));
        }
    }

    // A scenario naming a missing design or preset fails with each named; the check entry finds the same failures with
    // no zone and writes no document, and staging admits nothing, not even the parts that built: a player and a piloted
    // entity that build cleanly stay out when a later entity fails.
    [Fact]
    public void AMissingDesignFailsNamedAndChangesNothing()
    {
        var djinni = Bare(Hull("Djinni"));
        var scenario = Quiet(
            new ScenarioShip { Loadout = djinni, Cargo = { new CultRecordRef<EquippableItemData>(new CultRecordKey("absent-design")) } },
            Placed(Bare(Hull("LonginusX")), 300, 0, ScenarioStance.Hostile, piloted: true),
            Placed(new CultRecordRef<Loadout>(new CultRecordKey("absent-preset")), -300, 0, ScenarioStance.Neutral, false));
        var zone = Arena(null);
        var entities = zone.Entities.ToList();
        var agents = zone.Agents.ToList();
        var documents = _cache.AllStoredDocuments.Count();

        var checkFailures = RunStart.Check(_items, scenario);
        Assert.Equal(2, checkFailures.Count);
        Assert.Contains("absent-design", checkFailures[0]);
        Assert.Contains("absent-preset", checkFailures[1]);
        Assert.Equal(documents, _cache.AllStoredDocuments.Count());

        var failures = new List<string>();
        Assert.Null(RunStart.Stage(_items, zone, scenario, _startingHull, _protagonist, failures));
        Assert.Equal(checkFailures, failures);
        SafeAssert.Unchanged(zone, entities, agents);

        var lateFailure = Quiet(At(djinni, 0, 0),
            Placed(Bare(Hull("LonginusX")), 300, 0, ScenarioStance.Hostile, piloted: true),
            Placed(new CultRecordRef<Loadout>(new CultRecordKey("absent-preset")), -300, 0, ScenarioStance.Neutral, false));
        failures.Clear();
        Assert.Null(RunStart.Stage(_items, zone, lateFailure, _startingHull, _protagonist, failures));
        Assert.Contains("absent-preset", Assert.Single(failures));
        SafeAssert.Unchanged(zone, entities, agents);
        Assert.Empty(RunStart.Check(_items, null));
    }

    // The Q3 rule must not reach a real hull: the hulls scenarios rely on each have a product, and materialize bare,
    // branded, under the prelude's availability.
    [Fact]
    public void TheRealHullsMaterializeBareFromABrandedProduct()
    {
        var available = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, null, 2);
        foreach (var hull in new[] { Hull("Djinni"), Hull("LonginusX"), TurretHull() })
        {
            var failures = new List<string>();
            var entity = Loadouts.Materialize(_items, null, _cache.Get(Bare(hull)), available.IsAvailable, failures);
            Assert.Empty(failures);
            Assert.NotNull(_items.Brand(entity.Hull).Maker);
        }
    }

    // Zone.Admit is the only creator of agents: construction pilots every packed ship but the player's, and an
    // unpiloted admission (a warp arrival, an undock, a spawned turret) gets none.
    [Fact]
    public void AdmitIsTheOnlyAgentCreator()
    {
        var pack = RunStart.GenerateArena(_items, _zoneSettings, _galaxy, null);
        var packedPlayer = pack.Entities.OfType<ShipPack>().First();
        packedPlayer.IsPlayerShip = true;
        var zone = new Zone(_items, _planetSettings, pack, _galaxy.Entrance, _galaxy);
        var ships = zone.Entities.OfType<Ship>().ToList();
        Assert.True(ships.Count > 1);
        foreach (var ship in ships) Assert.Equal(ship.IsPlayerShip ? 0 : 1, AgentsOf(zone, ship));

        var arriving = (Ship) Loadouts.Materialize(_items, zone, _cache.Get(Bare(Hull("Djinni"))), _ => true, new List<string>());
        var agents = zone.Agents.Count;
        zone.Admit(arriving, piloted: false);
        SafeAssert.In(zone, arriving, "the arrival is admitted");
        Assert.Equal(agents, zone.Agents.Count);
    }

    // The scenario type is catalog data: it round-trips through the catalog file.
    [Fact]
    public void AScenarioRoundTripsThroughTheCatalog()
    {
        var djinni = Bare(Hull("Djinni"));
        var cargo = new CultRecordRef<EquippableItemData>(_cache.RefOf(Hull("LonginusX")).Key);
        var scenario = new Scenario
        {
            Name = "Round trip", Brief = "one of everything", Seed = 42, Ambient = true,
            Player = new ScenarioShip { Loadout = djinni, Position = float2(1, 2), Direction = float2(0, -1), Cargo = { cargo } },
            Entities = { Placed(djinni, 3, 4, ScenarioStance.Hostile, piloted: true) }
        };
        // Written through its own cache over a second copy of the catalog, disposed, then read back fresh
        var copy = Path.Combine(_root, "roundtrip.cc");
        File.Copy(Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc"), copy);
        using (var writer = new CultCache(Registry()))
        {
            writer.AddBackingStore(new SingleFileMessagePackBackingStore(copy), AetheriaStores.CatalogTypes);
            writer.Commit(batch => batch.Upsert(typeof(Loadout), _cache.Get(djinni), djinni.Key));
            writer.Upsert(scenario);
            writer.FlushAsync().Wait();
        }

        using var reopened = new CultCache(Registry());
        reopened.AddBackingStore(new SingleFileMessagePackBackingStore(copy, true), AetheriaStores.CatalogTypes);
        var loaded = Assert.Single(reopened.GetAll<Scenario>());
        Assert.Equal(("Round trip", "one of everything", 42u, true), (loaded.Name, loaded.Brief, loaded.Seed, loaded.Ambient));
        Assert.Equal(djinni.Key, loaded.Player.Loadout.Key);
        Assert.Equal((float2(1, 2), float2(0, -1)), (loaded.Player.Position, loaded.Player.Direction));
        Assert.Equal(cargo.Key, Assert.Single(loaded.Player.Cargo).Key);
        var entity = Assert.Single(loaded.Entities);
        Assert.Equal((djinni.Key, float2(3, 4), ScenarioStance.Hostile, true), (entity.Loadout.Key, entity.Position, entity.Stance, entity.Piloted));
    }
}
