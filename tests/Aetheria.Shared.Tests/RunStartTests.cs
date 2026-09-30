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

    // A stance lasts the sitting: the player docking and undocking leaves it standing both ways. It goes with an entity
    // gone for good: once an entity is destroyed, the player holds no stance toward it.
    [Fact]
    public void AStanceSurvivesDockingButNotDestruction()
    {
        var zone = Arena(null);
        var longinus = Bare(Hull("LonginusX"));
        var staged = Stage(zone, Quiet(At(Bare(Hull("Djinni")), 0, 0),
            Placed(longinus, 300, 0, ScenarioStance.Hostile, piloted: false),
            Placed(longinus, -300, 0, ScenarioStance.Hostile, piloted: false)));
        var player = staged.Player;
        var hostile = staged.Entities[0];
        var doomed = staged.Entities[1];
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);

        Assert.NotNull(station.TryDock(player));
        SafeAssert.NotIn(zone, player, "a docked ship is out of the zone");
        Assert.True(station.TryUndock(player));
        Assert.True(player.IsHostileTo(hostile), "the player's stance was lost over docking");
        Assert.True(hostile.IsHostileTo(player), "the entity's stance was lost over docking");
        Assert.True(player.EntityHostility[hostile], "the player's live hostility was lost over docking");
        Assert.True(hostile.EntityHostility[player], "the entity's live hostility was lost over docking");

        // Presets carry no faction, so without its stance the derived rule is not hostile
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
        var zone = Arena(null);
        var player = Stage(zone, null).Player;
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

    // A heater is gear whose thermostat runs its heat only below a target: a low-pass thermotoggle, then heat.
    private static bool IsHeater(EquippedItem item) =>
        item.Behaviors.OfType<Thermotoggle>().Any(t => !t.ThermotoggleData.HighPass) && item.Behaviors.OfType<Heat>().Any();

    // Every generated station carries exactly one heater, and a reactor to power it.
    [Fact]
    public void EveryGeneratedStationCarriesOneHeaterAndAReactor()
    {
        var zone = Arena(null);
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);
        for (var i = 0; i < 8; i++)
        {
            var station = EntitySerializer.Unpack(_items, zone, generator.GenerateStationLoadout());
            Assert.True(station.Equipment.Count(IsHeater) == 1, $"station {i} carries {station.Equipment.Count(IsHeater)} heaters");
            Assert.True(station.Equipment.Any(item => item.Behaviors.OfType<Reactor>().Any()), $"station {i} has no reactor");
        }
    }

    private const float Freezing = 273.15f;

    // A main-sector galaxy (not the prelude, which offers every product): each station's gear must come from
    // manufacturers its faction can reach, the galaxy's factions its allegiance names.
    private Galaxy MainGalaxy()
    {
        var authored = AuthoredSettings.Load(FindRepoRoot());
        return new Galaxy(authored.Read<SectorGenerationSettings>("SectorGenerationSettings"),
            authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"),
            authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), _cache, _ => { }, null, GalaxySeed);
    }

    // Every item a station carries, equipped or stocked, with the product it was made as.
    private IEnumerable<(string item, FactionProductData product)> Made(Entity station) =>
        station.Equipment.Select(item => item.EquippableItem).Cast<CraftedItemInstance>()
            .Concat(station.CargoBays.SelectMany(bay => bay.Cargo.Keys).OfType<CraftedItemInstance>())
            .Select(item => (_items.GetData(item).Name, _items.Brand(item).Product));

    private string Maker(FactionProductData product) => product == null ? "nobody" : _cache.Get(product.Manufacturer)?.Name ?? "nobody";

    // Every faction's stations, in a main-sector galaxy, carry a heater and a reactor, and nothing, equipped or
    // stocked, from a manufacturer that faction cannot reach (LoadoutGenerator.IsAvailable, the one reach rule).
    [Fact]
    public void EveryFactionsStationsCarryOnlyGearItCanReachIncludingAHeaterAndAReactor()
    {
        var galaxy = MainGalaxy();
        Assert.False(galaxy.IsPrelude);
        var zone = Arena(null);
        foreach (var faction in galaxy.Factions)
        {
            var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, faction, .5f);
            for (var i = 0; i < 3; i++)
            {
                var station = EntitySerializer.Unpack(_items, zone, generator.GenerateStationLoadout());
                Assert.True(station.Equipment.Count(IsHeater) == 1, $"{faction.Name} station {i}: no heater");
                Assert.True(station.Equipment.Any(item => item.Behaviors.OfType<Reactor>().Any()), $"{faction.Name} station {i}: no reactor");
                var unreachable = Made(station).Where(made => made.product == null || !generator.IsAvailable(made.product)).ToList();
                Assert.True(unreachable.Count == 0,
                    $"{faction.Name} station {i} carries {string.Join(", ", unreachable.Select(u => u.item + " by " + Maker(u.product)))}");
            }
        }
    }

    // A faction with no access to Zhestokost (its allegiance names only AU, Lightsail and NiteLife) gets
    // stations with no Zhestokost gear, powered by a reactor it can reach, and an idle one of them holds its heater's
    // cells above freezing over the second five of ten minutes.
    [Fact]
    public void AStationWithoutZhestokostAccessIsPoweredAndStaysAboveFreezing()
    {
        var galaxy = MainGalaxy();
        Faction Named(string shortName) => _cache.GetAll<Faction>().Single(f => f.ShortName == shortName);
        var coop = new Faction { Name = "Test Cooperative", ShortName = "Coop" };
        _cache.Upsert(coop);
        foreach (var ally in new[] { Named("AU"), Named("Lightsail"), Named("NiteLife") }) coop.Allegiance[_cache.RefOf(ally)] = 1;
        var zhestokost = Named("Zhestokost");
        var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, coop, .5f);
        var products = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).ToArray();
        Assert.DoesNotContain(products.Where(p => p.Manufacturer.Key.Equals(_cache.RefOf(zhestokost).Key)), generator.IsAvailable);
        Assert.Contains(products.Where(p => p.Manufacturer.Key.Equals(_cache.RefOf(Named("AU")).Key)), generator.IsAvailable);
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        Entity station = null;
        for (var i = 0; i < 8; i++)
        {
            station = EntitySerializer.Unpack(_items, zone, generator.GenerateStationLoadout());
            Assert.True(station.Equipment.Any(item => item.Behaviors.OfType<Reactor>().Any()), $"station {i}: no reactor");
            var foreign = Made(station).Where(made => made.product == null || !generator.IsAvailable(made.product) ||
                                                      made.product.Manufacturer.Key.Equals(_cache.RefOf(zhestokost).Key)).ToList();
            Assert.True(foreign.Count == 0, $"station {i} carries {string.Join(", ", foreign.Select(u => u.item + " by " + Maker(u.product)))}");
        }

        zone.Admit(station, piloted: false);
        var heater = station.Equipment.Single(IsHeater);
        var coldest = float.MaxValue;
        for (var tick = 0; tick < 6000; tick++)
        {
            zone.Update(.1f);
            if (tick >= 3000) coldest = min(coldest, heater.Temperature);
        }
        Assert.True(coldest > Freezing, $"the heater's cells fell to {coldest} K (station now {station.MinTemp}..{station.MaxTemp} K)");
    }

    // An idle station, powered by its reactor, holds its heater's cells (where the thermostat reads) above freezing
    // over the second five of ten idle minutes. Unpowered, they cool from 280 K to about 247 K in that time. (The
    // station's border cells still sit near 265 K; how warm the whole hull should run is a tuning question.)
    [Fact]
    public void AnIdleStationStaysAboveFreezing()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);
        var heater = station.Equipment.Single(IsHeater);
        var coldest = float.MaxValue;
        for (var tick = 0; tick < 6000; tick++)
        {
            zone.Update(.1f);
            if (tick >= 3000) coldest = min(coldest, heater.Temperature);
        }
        Assert.True(coldest > Freezing, $"the heater's cells fell to {coldest} K (station now {station.MinTemp}..{station.MaxTemp} K)");
    }

    // The heater itself warms the station: from a hull at one uniform temperature (so nothing conducts and interior
    // cells radiate nothing), one tick below the heater's target raises its cells, and one tick above it does not.
    [Fact]
    public void AStationHeaterHeatsBelowItsTargetAndNotAbove()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);
        var heater = station.Equipment.Single(IsHeater);
        var target = heater.Behaviors.OfType<Thermotoggle>().Single().TargetTemperature;
        for (var tick = 0; tick < 10; tick++) zone.Update(.1f); // the power bus is running

        float Step(float start)
        {
            foreach (var v in station.HullData.Shape.Coordinates) station.Temperature[v.x, v.y] = start;
            zone.Update(.1f);
            return heater.Temperature - start;
        }
        var below = Step(target - 20);
        var above = Step(target + 20);
        Assert.True(below > 0, $"below its target the heater's cells moved {below} K");
        Assert.True(above <= 0, $"above its target the heater's cells moved {above} K");
    }

    // A consumer runs only when its group's thermostat is open, and is billed only then: a heater above its target
    // puts no demand on the power bus, below it the bus bills its draw.
    [Fact]
    public void AHeaterAboveItsTargetDrawsNoPower()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);
        var heater = station.Equipment.Single(IsHeater);
        var target = heater.Behaviors.OfType<Thermotoggle>().Single().TargetTemperature;
        var request = heater.Behaviors.OfType<EnergyDraw>().Single().PowerRequest(.1f);
        Assert.True(request > 0, "the heater asks for power");
        for (var tick = 0; tick < 10; tick++) zone.Update(.1f); // the heater is online
        Assert.True(heater.Active.Value, "the heater is active");

        float Demand(float temperature)
        {
            foreach (var v in station.HullData.Shape.Coordinates) station.Temperature[v.x, v.y] = temperature;
            station.PowerBus.Step(.1f);
            return station.PowerBus.TotalDemand;
        }
        var cold = Demand(target - 20);
        var warm = Demand(target + 20);
        Assert.True(cold - warm >= request * .99f, $"demand {cold} below the target, {warm} above; the heater asks {request}");
    }

    // A station hull with no room left for a heater fails its generation by name, rather than spawning a station
    // that will freeze. The only station hull on offer is one whose interior takes the smallest docking bay and no more.
    [Fact]
    public void AStationWithNoRoomForAHeaterFailsLoudly()
    {
        var sold = _cache.GetAll<FactionProductData>().Select(product => product.Design.Key).ToHashSet();
        var bay = _cache.GetAll<DockingBayData>().Where(design => sold.Contains(_cache.RefOf(design).Key))
            .OrderBy(design => design.Shape.Coordinates.Length).ThenBy(design => design.Name, StringComparer.Ordinal).First();
        var shape = new Shape(bay.Shape.Width + 2, bay.Shape.Height + 2);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var cramped = new HullData { Name = "Cramped Station", HullType = HullType.Station, Shape = shape, Durability = 100, Mass = 1000, Price = 1 };
        _cache.Upsert(cramped);
        _cache.Upsert(new FactionProductData
        {
            Name = "Cramped Station", Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(cramped).Key), Manufacturer = _cache.RefOf(_protagonist)
        });
        foreach (var hull in _cache.GetAll<HullData>().Where(hull => hull.HullType == HullType.Station && hull != cramped))
            hull.Price = 0; // generation offers no zero-price design, so the cramped hull is the only station
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);

        var failure = Assert.Throws<InvalidLoadoutException>(() => generator.GenerateStationLoadout());
        Assert.Contains("heater", failure.Message);
    }

    // Orbit positions are computed once per tick, before the asteroid belts' tasks start, and those tasks finish
    // before the next tick clears the cache. Were the cache cleared under a running belt task, an orbit could be
    // served last tick's position. Over 3000 back-to-back ticks no orbit stands still against its parent. (Measured
    // against the parent because a moon can genuinely pause in zone space, where its motion cancels its planet's.)
    [Fact]
    public void NoOrbitStandsStillAgainstItsParent()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        Assert.True(zone.AsteroidBelts.Count > 0, "the arena has an asteroid belt to race against");
        float2 Relative(Orbit orbit, bool previous) =>
            (previous ? orbit.PreviousPosition : orbit.Position) -
            (zone.Orbits.TryGetValue(orbit.Data.Parent.Key, out var parent) ? previous ? parent.PreviousPosition : parent.Position : orbit.Data.FixedPosition);
        zone.Update(.02f);
        var stalls = new List<string>();
        for (var tick = 0; tick < 3000; tick++)
        {
            zone.Update(.02f);
            stalls.AddRange(zone.Orbits.Values
                .Where(orbit => orbit.Period > .01f && orbit.Data.Distance > 0 && lengthsq(Relative(orbit, false) - Relative(orbit, true)) == 0)
                .Select(orbit => $"tick {tick}: period {orbit.Period}, distance {orbit.Data.Distance}, at {orbit.Position}"));
        }
        Assert.True(stalls.Count == 0, $"{stalls.Count} readings of an orbit standing still; first {stalls.FirstOrDefault()}");
    }

    // A belt's asteroids are laid out for one time: the time of the tick that started the belt's task. Were the
    // clock advanced before that task finished, some asteroids would be placed for the next tick and some for this.
    [Fact]
    public void EveryAsteroidInABeltIsPlacedForTheSameTime()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        var (key, belt) = zone.AsteroidBelts.First();
        var asteroids = ((AsteroidBeltData) zone.Planets[key]).Asteroids;
        const float dt = .02f;
        float2 At(Asteroid asteroid, float time, float2 center) =>
            OrbitData.Evaluate((float) frac((double) time / zone.Settings.OrbitPeriod.Evaluate(asteroid.Distance) + asteroid.Phase)) * asteroid.Distance + center;
        var torn = 0;
        zone.Update(dt);
        for (var tick = 0; tick < 3000; tick++)
        {
            var started = zone.Time; // the time the running task was started for
            zone.Update(dt);        // settles that task and publishes its layout
            for (var i = 0; i < asteroids.Length; i++)
            {
                var placed = belt.Transforms[i].xy;
                var here = At(asteroids[i], started, belt.OrbitPosition);
                var next = At(asteroids[i], started + dt, belt.OrbitPosition);
                if (lengthsq(next - here) < 1e-6f) continue; // too slow to tell the two times apart
                if (lengthsq(placed - here) > lengthsq(placed - next)) torn++;
            }
        }
        Assert.True(torn == 0, $"{torn} asteroid placements were for the next tick");
    }

    // Mining writes a belt's damage and respawn state, which the belt's task reads, so it waits for the task first.
    // This drives mining hard while the belt updates and fails on any exception the race throws; the race itself is
    // rare enough that removing the wait did not fail it in two runs, so it guards, but does not prove, the rule.
    [Fact]
    public void MiningWhileTheBeltUpdatesNeverRacesIt()
    {
        var zone = Arena(new Scenario { Ambient = false }); // no ships: nothing but the zone runs
        var (key, _) = zone.AsteroidBelts.First();
        var data = (AsteroidBeltData) zone.Planets[key];
        var count = data.Asteroids.Length;
        var miner = zone.Entities.First();
        for (var tick = 0; tick < 1000; tick++)
        {
            zone.Update(.02f);
            for (var pass = 0; pass < 4; pass++)
            {
                // Chip every asteroid (the belt's damage table fills), then break every one (it empties again).
                if (data.Resources.Count > 0)
                    for (var i = 0; i < count; i++) zone.MineAsteroid(miner, key, (i + tick) % count, 1e-3f, 0, 1);
                for (var i = 0; i < count; i++) zone.MineAsteroid(miner, key, (i + tick) % count, 1e6f, 0, 1);
            }
        }
    }

    private LoadoutGenerator PreludeGenerator() =>
        new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);

    private bool Offered(LoadoutGenerator generator, EquippableItemData design) =>
        generator.RandomProducts<EquippableItemData>(1, 0, candidate => candidate == design).Length > 0;

    private GearData ScratchGear(string name, HardpointType type, int width, int height)
    {
        var shape = new Shape(width, height);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var design = new GearData { Name = name, Hardpoint = type, Shape = shape, Durability = 10, Mass = 10, Price = 1000 };
        _cache.Upsert(design);
        _cache.Upsert(new FactionProductData
        {
            Name = name, Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(design).Key), Manufacturer = _cache.RefOf(_protagonist)
        });
        return design;
    }

    // Hardpoint fit is loose (operator, 2026-09-30: "nobody's gonna stop you from putting a small reactor in a large
    // reactor's hardpoint"), and there is one fit rule, HardpointData.Takes. Generation offers every sold hardpoint
    // design that fits some hull's hardpoint of its type, and none that fits no hull.
    [Fact]
    public void GenerationOffersExactlyTheSoldGearSomeHullCanFit()
    {
        var hulls = _cache.GetAll<HullData>().ToArray();
        var sold = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).Select(p => p.Design.Key).ToHashSet();
        var generator = PreludeGenerator();
        var wrong = _cache.GetAll<EquippableItemData>()
            .Where(design => sold.Contains(_cache.RefOf(design).Key) && design.Price > 0)
            .Where(design => design.HardpointType != HardpointType.Tool && design.HardpointType != HardpointType.Hull)
            .Where(design => Offered(generator, design) != hulls.Any(hull => hull.Hardpoints.Any(hardpoint => hardpoint.Takes(design))))
            .Select(design => design.Name).ToList();
        Assert.True(wrong.Count == 0, $"offered against the fit rule: {string.Join(", ", wrong)}");
        Assert.True(Offered(generator, _cache.GetAll<EquippableItemData>().Single(d => d.Name == "Autocannon")),
            "the Autocannon fits the turret's 8-cell Ballistic hardpoint, so it is offered");
    }

    // Gear no hull can fit is never offered; authoring a hull whose hardpoint takes it makes it offered.
    [Fact]
    public void GearNoHullCanFitIsOfferedOnlyOnceAHullTakesIt()
    {
        var huge = ScratchGear("Oversized Cannon", HardpointType.Ballistic, 7, 7);
        var generator = PreludeGenerator();
        Assert.False(Offered(generator, huge), "no hull fits a 7x7 Ballistic design");

        var shape = new Shape(9, 9);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hardpointShape = new Shape(8, 8);
        foreach (var cell in hardpointShape.AllCoordinates) hardpointShape[cell] = true;
        _cache.Upsert(new HullData
        {
            Name = "Big Mount", HullType = HullType.Ship, Shape = shape, Durability = 100, Mass = 1000, Price = 1,
            Hardpoints = { new HardpointData { Type = HardpointType.Ballistic, Position = int2(0, 0), Shape = hardpointShape } }
        });
        Assert.True(Offered(generator, huge), "a hull whose hardpoint takes it gives it a home");
    }

    // Where nothing fills a hardpoint, generation puts in something that fits: a hull whose only Reactor hardpoint is
    // 5x5, which no reactor fills, still gets a reactor.
    [Fact]
    public void GenerationFitsWhatItCannotFill()
    {
        var shape = new Shape(9, 9);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hardpointShape = new Shape(5, 5);
        foreach (var cell in hardpointShape.AllCoordinates) hardpointShape[cell] = true;
        var hull = new HullData
        {
            Name = "Odd Reactor Bay", HullType = HullType.Ship, Shape = shape, Durability = 100, Mass = 1000, Price = 1,
            Hardpoints = { new HardpointData { Type = HardpointType.Reactor, Position = int2(1, 1), Shape = hardpointShape } }
        };
        _cache.Upsert(hull);
        _cache.Upsert(new FactionProductData
        {
            Name = "Odd Reactor Bay", Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(hull).Key), Manufacturer = _cache.RefOf(_protagonist)
        });
        Assert.DoesNotContain(_cache.GetAll<GearData>(), design => hull.Hardpoints[0].IsFilledBy(design));

        var ship = EntitySerializer.Unpack(_items, null, PreludeGenerator().GenerateShipLoadout(candidate => candidate == hull));
        Assert.True(ship.Equipment.Any(item => item.Behaviors.OfType<Reactor>().Any()), "the 5x5 Reactor hardpoint got a reactor");
    }

    // Equipping and generation read the same rule: for every hull, hardpoint and design of that hardpoint's type, the
    // hardpoint takes the design exactly when an empty hull of that kind accepts it somewhere in that hardpoint.
    [Fact]
    public void EquippingAndGenerationAgreeOnEveryDesignAndHardpoint()
    {
        var products = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).ToArray();
        var gear = _cache.GetAll<EquippableItemData>().Where(d => d.HardpointType != HardpointType.Tool && d.HardpointType != HardpointType.Hull).ToArray();
        var disagreements = new List<string>();
        foreach (var hull in _cache.GetAll<HullData>())
        {
            var product = products.FirstOrDefault(p => p.Design.Key.Equals(_cache.RefOf(hull).Key));
            if (product == null) continue;
            var entity = new Ship(_items, null, (EquippableItem) _items.CreateInstance(product), _items.GameplaySettings.DefaultEntitySettings);
            foreach (var hardpoint in hull.Hardpoints)
            foreach (var design in gear.Where(d => d.HardpointType == hardpoint.Type))
            {
                var item = new EquippableItem { Data = _cache.RefOf<ItemData>(design), Durability = design.Durability };
                var accepted = false;
                for (var x = 0; x < hardpoint.Shape.Width && !accepted; x++)
                for (var y = 0; y < hardpoint.Shape.Height && !accepted; y++)
                    accepted = entity.ItemFits(item, hardpoint.Position + int2(x, y));
                if (accepted != hardpoint.Takes(design))
                    disagreements.Add($"{hull.Name} {hardpoint.Type} at {hardpoint.Position}, {design.Name}: equip {accepted}, rule {!accepted}");
            }
        }
        Assert.True(disagreements.Count == 0, string.Join("; ", disagreements.Take(10)) + $" ({disagreements.Count} in all)");
    }

    // The sold hardpoint gear that no hull fits even loosely today, stated so that authoring a hull that takes one
    // shows up here as a changed list.
    [Fact]
    public void TheSoldHardpointGearWithNoHomeToday()
    {
        var hulls = _cache.GetAll<HullData>().ToArray();
        var sold = _cache.GetAll<FactionProductData>().Select(product => product.Design.Key).ToHashSet();
        var homeless = _cache.GetAll<EquippableItemData>()
            .Where(design => sold.Contains(_cache.RefOf(design).Key) && design.Price > 0)
            .Where(design => design.HardpointType != HardpointType.Tool && design.HardpointType != HardpointType.Hull)
            .Where(design => !hulls.Any(hull => hull.Hardpoints.Any(hardpoint => hardpoint.Takes(design))))
            .Select(design => design.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "CShot RainbowLite Lazer", "ChargeBlast SG", "ChargeBlast+-", "OK Disperser" }, homeless);
    }

    // A sold weapon can be generated onto an NPC, whose combat state samples its damage at range, so every weapon a
    // product sells carries a damage curve.
    [Fact]
    public void EverySoldWeaponHasADamageCurve()
    {
        var sold = _cache.GetAll<FactionProductData>().Select(product => product.Design.Key).ToHashSet();
        var missing = _cache.GetAll<EquippableItemData>()
            .Where(design => sold.Contains(_cache.RefOf(design).Key))
            .Where(design => design.Behaviors.OfType<WeaponData>().Any(weapon => weapon.DamageCurve?.Keys == null || weapon.DamageCurve.Keys.Length == 0))
            .Select(design => design.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, $"sold weapons with no damage curve: {string.Join(", ", missing)}");
    }

    // Q3, with the operator's 2026-09-30 ruling on the ten product-less designs: a design no product sells is a
    // scenario test design and nothing else. A test design is one a scenario places: its preset's hull or a slot of
    // it, or cargo it carries. These are the unsold designs no scenario places, by name.
    private static List<string> UnsoldAndUnplaced(CultCache cache)
    {
        var sold = cache.GetAll<FactionProductData>().Select(product => product.Design.Key).ToHashSet();
        var placed = new HashSet<CultRecordKey>();
        foreach (var scenario in cache.GetAll<Scenario>())
        foreach (var ship in scenario.Entities.Prepend(scenario.Player).Where(ship => ship != null))
        {
            foreach (var cargo in ship.Cargo) placed.Add(cargo.Key);
            var preset = cache.Get(ship.Loadout);
            if (preset == null) continue;
            placed.Add(preset.Hull.Key);
            foreach (var slot in preset.Slots) placed.Add(slot.Design.Key);
        }
        return cache.GetAll<EquippableItemData>()
            .Where(design => !sold.Contains(cache.RefOf(design).Key) && !placed.Contains(cache.RefOf(design).Key))
            .Select(design => design.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    // The shipped catalog holds no unsold design outside a scenario (today, none unsold at all). A new unsold design is
    // flagged until a scenario places it, in a preset's slot or as an entity's cargo.
    [Fact]
    public void EveryUnsoldDesignIsAScenarioTestDesign()
    {
        Assert.Empty(UnsoldAndUnplaced(_cache));

        GearData Probe(string name)
        {
            var design = new GearData { Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1 };
            _cache.Upsert(design);
            return design;
        }
        var fitted = Probe("Census Fitted");
        var carried = Probe("Census Carried");
        Assert.Equal(new[] { "Census Carried", "Census Fitted" }, UnsoldAndUnplaced(_cache));

        var preset = Preset(new Loadout
        {
            Name = "census preset", Hull = _cache.RefOf(Hull("Djinni")), WeaponGroups = new int[0][],
            Slots = { new LoadoutSlot { Design = new CultRecordRef<EquippableItemData>(_cache.RefOf(fitted).Key) } }
        });
        var scenario = Quiet(At(Bare(Hull("Djinni")), 0, 0), Placed(preset, 100, 0, ScenarioStance.Neutral, false));
        scenario.Name = "census";
        _cache.Upsert(scenario);
        Assert.Equal(new[] { "Census Carried" }, UnsoldAndUnplaced(_cache));

        scenario.Entities[0].Cargo.Add(new CultRecordRef<EquippableItemData>(_cache.RefOf(carried).Key));
        _cache.Upsert(scenario);
        Assert.Empty(UnsoldAndUnplaced(_cache));
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
