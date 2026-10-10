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

// RunStart owns what a new run starts with, and Zone.Admit is the one admission into a zone (docs/scenarios-cut.md,
// 2.2 and R). Every test stages into a real arena: the entrance zone of a prelude galaxy at a fixed seed, built from a
// scratch copy of the catalog the game boots (TestCatalog) and the authored settings (Assets/Resources/Settings.asset).
public sealed partial class RunStartTests : IDisposable
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
    private readonly TutorialGenerationSettings _tutorialSettings;

    public RunStartTests()
    {
        var repo = FindRepoRoot();
        Directory.CreateDirectory(_root);
        var catalog = Path.Combine(_root, "Aetheria.cc");
        File.Copy(TestCatalog.Repo, catalog);

        // A registry scoped to the shipped assembly's own [CultDocument] types, as RestoredHullsTests composes it, so
        // this test assembly's own documents never reach the real catalog's validation.
        _cache = new CultCache(TestCatalog.Registry());
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
        _cache.AddBackingStore(new SingleFileMessagePackBackingStore(Path.Combine(_root, "run.cc")), AetheriaStores.RunTypes);

        var authored = AuthoredSettings.Load(repo);
        _zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
        _planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
        var tutorial = _tutorialSettings = authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
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

    internal static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }

    // The fixture galaxy's arena, with or without its generated ships and turrets.
    private Zone Arena(bool ambient) =>
        new Zone(_items, _planetSettings, RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scripted(ambient, _ => { })), _galaxy.Entrance, _galaxy);

    private HullData Hull(string name) => _cache.GetAll<HullData>().Single(hull => hull.Name == name);

    // The first turret hull, by name, that a manufacturer makes: a real turret, not a fixture.
    private HullData TurretHull() => _cache.GetAll<HullData>()
        .Where(hull => hull.HullType == HullType.Turret)
        .Where(hull => _cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(_cache.RefOf(hull).Key)))
        .OrderBy(hull => hull.Name, StringComparer.Ordinal)
        .First();

    // A loadout with only its hull, in memory.
    private Loadout BareLoadout(HullData hull) =>
        new Loadout { Name = "bare " + hull.Name, Hull = _cache.RefOf(hull), WeaponGroups = new int[0][] };

    private static int AgentsOf(Zone zone, Entity entity) => zone.Agents.Count(agent => agent.Ship == entity);

    // A heater is gear whose thermostat runs its heat only below a target: a low-pass thermotoggle, then heat.
    private static bool IsHeater(EquippedItem item) =>
        item.Behaviors.OfType<Thermotoggle>().Any(t => !t.ThermotoggleData.HighPass) && item.Behaviors.OfType<Heat>().Any();

    // Every generated station carries exactly one heater, and a reactor to power it.
    [Fact]
    public void EveryGeneratedStationCarriesOneHeaterAndAReactor()
    {
        var zone = Arena(true);
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);
        for (var i = 0; i < 8; i++)
        {
            var station = EntitySerializer.Unpack(_items, zone, generator.GenerateStationLoadout());
            Assert.True(station.Equipment.Count(IsHeater) == 1, $"station {i} carries {station.Equipment.Count(IsHeater)} heaters");
            Assert.True(station.Equipment.Any(item => item.Behaviors.OfType<Reactor>().Any()), $"station {i} has no reactor");
        }
    }

    private const float Freezing = 273.15f;

    // A main-sector galaxy: each station's gear must come from manufacturers its faction can reach, the makers its
    // allegiance names, present in the galaxy or not.
    private Galaxy MainSectorGalaxy()
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
        var galaxy = MainSectorGalaxy();
        Assert.False(galaxy.IsPrelude);
        var zone = Arena(true);
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

    // A faction always reaches its own manufacturer's gear: in a main-sector galaxy every product a faction makes is
    // on offer to that faction, allegiance or not. And an allegiance map lists only other factions.
    [Fact]
    public void AFactionReachesItsOwnGearAndItsAllegianceNamesOnlyOthers()
    {
        var galaxy = MainSectorGalaxy();
        var products = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).ToArray();
        foreach (var faction in galaxy.Factions)
        {
            Assert.DoesNotContain(faction.Allegiance.Keys, key => key.Key.Equals(_cache.RefOf(faction).Key));
            var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, faction, .5f);
            var own = products.Where(p => p.Manufacturer.Key.Equals(_cache.RefOf(faction).Key)).ToList();
            Assert.True(own.All(generator.IsAvailable), $"{faction.Name} cannot reach its own {string.Join(", ", own.Where(p => !generator.IsAvailable(p)).Select(p => p.Name))}");
        }
    }

    // A faction with no access to Zhestokost (its allegiance names only AU, Lightsail and NiteLife) gets
    // stations with no Zhestokost gear, powered by a reactor it can reach, and an idle one of them holds its heater's
    // cells above freezing over the second five of ten minutes.
    [Fact]
    public void AStationWithoutZhestokostAccessIsPoweredAndStaysAboveFreezing()
    {
        var galaxy = MainSectorGalaxy();
        Faction Named(string shortName) => _cache.GetAll<Faction>().Single(f => f.ShortName == shortName);
        var coop = new Faction { Name = "Test Cooperative", ShortName = "Coop" };
        _cache.Upsert(coop);
        foreach (var ally in new[] { Named("AU"), Named("Lightsail"), Named("NiteLife") }) coop.Allegiance[_cache.RefOf(ally)] = 1;
        var zhestokost = Named("Zhestokost");
        var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, coop, .5f);
        var products = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).ToArray();
        Assert.DoesNotContain(products.Where(p => p.Manufacturer.Key.Equals(_cache.RefOf(zhestokost).Key)), generator.IsAvailable);
        Assert.Contains(products.Where(p => p.Manufacturer.Key.Equals(_cache.RefOf(Named("AU")).Key)), generator.IsAvailable);
        // Its allegiance does not name itself, yet it reaches what it makes.
        var own = new FactionProductData { Name = "Cooperative Capacitor", Design = products.First().Design, Manufacturer = _cache.RefOf(coop) };
        _cache.Upsert(own);
        Assert.True(generator.IsAvailable(own), "a faction reaches its own manufacturer's gear");
        var zone = Arena(false); // no ships: nothing but the zone runs
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
        var zone = Arena(false); // no ships: nothing but the zone runs
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
        var zone = Arena(false); // no ships: nothing but the zone runs
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
        var zone = Arena(false); // no ships: nothing but the zone runs
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
        var zone = Arena(false); // no ships: nothing but the zone runs
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

    // An empty hull of this kind to equip into by hand.
    private Entity BareHull(HullData hull) =>
        new Ship(_items, null, (EquippableItem) _items.CreateInstance(_cache.GetAll<FactionProductData>().First(p => p.Design.Key.Equals(_cache.RefOf(hull).Key))),
            _items.GameplaySettings.DefaultEntitySettings);

    private HullData ScratchHull(string name, int size, params HardpointData[] hardpoints)
    {
        var shape = new Shape(size, size);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hull = new HullData { Name = name, HullType = HullType.Ship, Shape = shape, Durability = 100, Mass = 1000, Price = 1 };
        hull.Hardpoints.AddRange(hardpoints);
        _cache.Upsert(hull);
        _cache.Upsert(new FactionProductData
        {
            Name = name, Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(hull).Key), Manufacturer = _cache.RefOf(_protagonist)
        });
        return hull;
    }

    private EquippableItem Instance(EquippableItemData design) =>
        (EquippableItem) _items.CreateInstance(_cache.GetAll<FactionProductData>().First(p => p.Design.Key.Equals(_cache.RefOf(design).Key)));

    // Placement searches every offset the fit rule does: an L-shaped hardpoint, whose origin cell is empty, takes the L
    // design that fills it, by hand and in generation.
    [Fact]
    public void AnLShapedHardpointTakesItsLDesign()
    {
        var l = new Shape(2, 2);
        l[int2(1, 0)] = true;
        l[int2(0, 1)] = true;
        l[int2(1, 1)] = true;
        var hull = ScratchHull("L Mount", 9, new HardpointData { Type = HardpointType.Ballistic, Position = int2(1, 1), Shape = l });
        var gun = ScratchGear("L Gun", HardpointType.Ballistic, 1, 1);
        gun.Shape = l;
        Assert.True(hull.Hardpoints[0].IsFilledBy(gun));

        Assert.True(BareHull(hull).TryEquip(Instance(gun)), "equipping by hand");
        var ship = EntitySerializer.Unpack(_items, null, PreludeGenerator().GenerateShipLoadout(candidate => candidate == hull));
        Assert.True(ship.Equipment.Any(item => item.Data.HardpointType == HardpointType.Ballistic), "generation equips the L hardpoint");
    }

    // Generated entities whose hardpoints share leftover cells with general gear, found by generating until enough.
    private List<(Entity entity, HardpointData hardpoint, EquippedItem held)> SharedHardpoints(int wanted)
    {
        var found = new List<(Entity, HardpointData, EquippedItem)>();
        var generator = PreludeGenerator();
        for (var i = 0; i < 400 && found.Count < wanted; i++)
        {
            var entity = EntitySerializer.Unpack(_items, null, generator.GenerateTurretLoadout());
            foreach (var hardpoint in entity.HullData.Hardpoints)
            {
                var occupants = hardpoint.Shape.Coordinates.Select(c => entity.GearOccupancy[hardpoint.Position.x + c.x, hardpoint.Position.y + c.y])
                    .Where(o => o != null).Distinct().ToList();
                var held = occupants.FirstOrDefault(o => o.Data.HardpointType == hardpoint.Type);
                if (held != null && occupants.Any(o => o.Data.HardpointType == HardpointType.Tool)) found.Add((entity, hardpoint, held));
            }
        }
        return found;
    }

    // A hardpoint's leftover cells may hold general gear (operator, 2026-09-30, "B is the design intent"): a hardpoint
    // item needs only its own cells free. So a generated turret whose spare gun-mount cells hold tool gear takes its own
    // gun back after the gun is pulled.
    [Fact]
    public void AHardpointItemGoesBackBesideGearInItsLeftoverCells()
    {
        var shared = SharedHardpoints(10);
        Assert.True(shared.Count > 0, "generation shares hardpoint cells with general gear");
        foreach (var (entity, hardpoint, held) in shared)
        {
            var item = entity.TryUnequip(held);
            Assert.True(item != null && entity.TryEquip(item), $"{entity.HullData.Name}: {held.Data.Name} back into its {hardpoint.Type} hardpoint");
        }
    }

    // A hardpoint holds at most one hardpoint item, however much room it has left.
    [Fact]
    public void AHardpointHoldsOneHardpointItem()
    {
        var mount = new Shape(8, 8);
        foreach (var cell in mount.AllCoordinates) mount[cell] = true;
        var hull = ScratchHull("Roomy Mount", 10, new HardpointData { Type = HardpointType.Ballistic, Position = int2(1, 1), Shape = mount });
        var gun = ScratchGear("Small Gun", HardpointType.Ballistic, 1, 1);
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(gun)), "the first gun");
        Assert.False(entity.TryEquip(Instance(gun)), "a second gun in the same hardpoint");
    }

    // Loading a saved entity equips its items one by one; the result does not depend on their order, even where general
    // gear sits in a hardpoint's leftover cells.
    [Fact]
    public void SaveLoadGivesTheSameLoadoutInAnyOrder()
    {
        var shared = SharedHardpoints(3);
        Assert.True(shared.Count > 0, "generation shares hardpoint cells with general gear");
        string Loadout(Entity e) => string.Join(";", e.Equipment.Select(i => $"{i.Data.Name}@{i.Position}").OrderBy(s => s, StringComparer.Ordinal));
        foreach (var entity in shared.Select(s => s.entity).Distinct())
        {
            var pack = EntitySerializer.Pack(entity);
            var inOrder = Loadout(EntitySerializer.Unpack(_items, null, pack));
            pack.Equipment = pack.Equipment.Reverse().ToArray();
            var reversed = Loadout(EntitySerializer.Unpack(_items, null, pack));
            Assert.Equal(Loadout(entity), inOrder);
            Assert.Equal(inOrder, reversed);
        }
    }

    // Generation prefers gear that fills a hardpoint: a station's 16-cell Reactor hardpoint always gets a 16-cell
    // reactor, though smaller ones fit it.
    [Fact]
    public void GenerationFillsAHardpointWhenSomethingFillsIt()
    {
        var zenith = _cache.GetAll<HullData>().Single(h => h.Name == "Zenith");
        var reactorPoint = zenith.Hardpoints.Single(h => h.Type == HardpointType.Reactor);
        Assert.Contains(_cache.GetAll<GearData>(), design => reactorPoint.Takes(design) && !reactorPoint.IsFilledBy(design));
        var generator = PreludeGenerator();
        for (var i = 0; i < 50; i++)
        {
            var design = generator.RandomProduct<GearData>(reactorPoint, 2).design;
            Assert.True(reactorPoint.IsFilledBy(design), $"draw {i}: {design.Name} does not fill the hardpoint");
        }
    }

    // Matching hardpoints carry matching units: a generated Djinni's eight thruster hardpoints hold one product's lot.
    [Fact]
    public void MatchingHardpointsShareOneLot()
    {
        var ship = EntitySerializer.Unpack(_items, null, PreludeGenerator().GenerateShipLoadout(hull => hull.Name == "Djinni"));
        var thrusters = ship.Equipment.Where(item => item.Data.HardpointType == HardpointType.Thruster).ToList();
        Assert.Equal(8, thrusters.Count);
        Assert.Single(thrusters.Select(item => item.EquippableItem.Lot).Distinct());
    }

    // The bus bills a thermostat-gated consumer on exactly the ticks it runs: across 5 K either side of a heater's
    // target, the heater's draw is billed exactly when its thermostat is open.
    [Fact]
    public void TheBusBillsAHeaterExactlyWhenItsThermostatIsOpen()
    {
        var zone = Arena(false); // no ships: nothing but the zone runs
        var station = zone.Entities.First(entity => entity is OrbitalEntity && entity.DockingBays.Count > 0);
        var heater = station.Equipment.Single(IsHeater);
        var thermostat = heater.Behaviors.OfType<Thermotoggle>().Single();
        var request = heater.Behaviors.OfType<EnergyDraw>().Single().PowerRequest(.1f);
        for (var tick = 0; tick < 10; tick++) zone.Update(.1f);

        float Demand(float temperature)
        {
            foreach (var v in station.HullData.Shape.Coordinates) station.Temperature[v.x, v.y] = temperature;
            station.PowerBus.Step(.1f);
            return station.PowerBus.TotalDemand;
        }
        var unbilled = Demand(thermostat.TargetTemperature + 20); // well above: closed
        var mismatches = new List<string>();
        for (var delta = -5f; delta <= 5f; delta += .25f)
        {
            var billed = Demand(thermostat.TargetTemperature + delta) - unbilled > request * .5f;
            if (billed != thermostat.Open) mismatches.Add($"{delta:+0.00;-0.00} K: open {thermostat.Open}, billed {billed}");
        }
        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    // A thermostat gates the rest of its own behaviour group, not the whole item: of two draws on one item, the one
    // behind a closed thermostat is not billed and the one in another group is.
    [Fact]
    public void AThermostatGatesOnlyItsOwnGroup()
    {
        var gear = new GearData
        {
            Name = "Two Group Gear", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10, Mass = 10, Price = 1000,
            MinimumTemperature = 100, MaximumTemperature = 500, OptimalTemperature = 280, PlateauWidth = 200, // online at any hull temperature here
            Behaviors =
            {
                new ThermotoggleData { Group = 0, TargetTemperature = 1 }, // low-pass below 1 K: always closed here
                new EnergyDrawData { Group = 0, EnergyDraw = new PerformanceStat { Min = 10, Max = 10 }, PerSecond = true },
                new EnergyDrawData { Group = 1, EnergyDraw = new PerformanceStat { Min = 7, Max = 7 }, PerSecond = true },
            }
        };
        _cache.Upsert(gear);
        _cache.Upsert(new FactionProductData
        {
            Name = "Two Group Gear", Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(gear).Key), Manufacturer = _cache.RefOf(_protagonist)
        });
        var zone = Arena(false);
        var ship = BareHull(Hull("Djinni"));
        ship.Zone = zone;
        Assert.True(ship.TryEquip(Instance(gear)));
        zone.Admit(ship, piloted: false);
        for (var tick = 0; tick < 5; tick++) zone.Update(.1f);
        Assert.True(ship.Equipment.Single(item => item.Data == gear).Active.Value, "the gear is active");
        ship.PowerBus.Step(1f);
        Assert.True(abs(ship.PowerBus.TotalDemand - 7) < .01f, $"billed {ship.PowerBus.TotalDemand} for a closed 10 and an ungated 7");
    }

    // The Q3 rule must not reach a real hull: the hulls scenarios rely on each have a product, and materialize bare,
    // branded, under the prelude's availability.
    [Fact]
    public void TheRealHullsMaterializeBareFromABrandedProduct()
    {
        var available = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, null, 2);
        foreach (var hull in new[] { Hull("Djinni"), Hull("Longinus"), TurretHull() })
        {
            var failures = new List<string>();
            var entity = Loadouts.Materialize(_items, null, BareLoadout(hull), available.IsAvailable, failures);
            Assert.Empty(failures);
            Assert.NotNull(_items.Brand(entity.Hull).Maker);
        }
    }

    // Zone.Admit is the only creator of agents: construction pilots every packed ship but the player's, and an
    // unpiloted admission (a warp arrival, an undock, a spawned turret) gets none.
    [Fact]
    public void AdmitIsTheOnlyAgentCreator()
    {
        var pack = RunStart.GenerateArena(_items, _zoneSettings, _galaxy, new Scripted(true, _ => { }));
        var packedPlayer = pack.Entities.OfType<ShipPack>().First();
        packedPlayer.IsPlayerShip = true;
        var zone = new Zone(_items, _planetSettings, pack, _galaxy.Entrance, _galaxy);
        var ships = zone.Entities.OfType<Ship>().ToList();
        Assert.True(ships.Count > 1);
        foreach (var ship in ships) Assert.Equal(ship.IsPlayerShip ? 0 : 1, AgentsOf(zone, ship));

        var arriving = (Ship) Loadouts.Materialize(_items, zone, BareLoadout(Hull("Djinni")), _ => true, new List<string>());
        var agents = zone.Agents.Count;
        zone.Admit(arriving, piloted: false);
        SafeAssert.In(zone, arriving, "the arrival is admitted");
        Assert.Equal(agents, zone.Agents.Count);
    }
}
