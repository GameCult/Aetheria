/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using Random = CultMath.Random;

// Cut 1 (docs/locomotion-cut.md, "Restore the legacy thruster ship hulls to the live catalog"). Verifies the
// content restore-hulls landed, against the real shipped catalog, read-only: the hulls exist with the expected
// thruster hardpoint counts, every thruster hardpoint accepts a catalog thruster design through TryEquip at its
// own position, and the ship then moves and turns for a few ticks under MovementDirection/LookDirection on the
// pre-Cut-3 mixer -- proof of the restored content, not of a controller this cut does not touch.
public sealed class RestoredHullsTests
{
    // TorqueMultiplier defaults to 0 on a bare GameplaySettings; Thruster.Execute multiplies its rotation
    // output by it (Ship.cs's mixer applies Torque*Thrust*TorqueMultiplier/Mass), so leaving it unset makes
    // every thruster's yaw contribution silently zero regardless of how the ship is equipped -- velocity moves
    // but Direction never turns, which looks exactly like a content defect instead of a fixture gap.
    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TorqueMultiplier = 1,
    };

    // Same composition FireControlCut7Tests.OpenReadOnlyRealCatalog uses: a registry scoped to the shipped
    // assembly's own [CultDocument] types, so this test's own process-wide type registration never pollutes the
    // real catalog's validation the way the shared, AppDomain-scanning CultCache() default would.
    private static CultCache OpenReadOnlyRealCatalog(string catalogPath)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, true), AetheriaStores.CatalogTypes);
        return cache;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }

    private static CultCache OpenCatalog() => OpenReadOnlyRealCatalog(Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc"));

    // ZoneGenerator.GenerateZone writes OrbitData/BodyData (run-store types, AetheriaStores.RunTypes), so a
    // catalog-only cache has no home for them. A scratch run file, deleted after the seed that used it.
    private static CultCache OpenReadOnlyRealCatalogWithScratchRun(string catalogPath, string runPath)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, true), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(runPath), AetheriaStores.RunTypes);
        return cache;
    }

    // Headless asset-reference rule: every hull's Prefab, every ThrusterData.ParticlesPrefab, and every
    // non-empty Schematic names a GUID that actually exists under Assets/ (a .meta file carries it). Unity's
    // EngineAssetCheck enforces this too, but only in-editor; this is the same check run headless against the
    // shipped catalog, so a fabricated or dropped GUID fails a build that never opens Unity. Soul's own probe
    // (commit message, this cut's fix batch) found Djinni.Prefab fabricated and all five restored thrusters'
    // ParticlesPrefab null; this test is that probe, committed so it can be rerun and mutated.
    [Fact]
    public void EveryAssetReferenceResolvesToAMetaGuid()
    {
        var assetGuids = new HashSet<string>(Directory.EnumerateFiles(Path.Combine(FindRepoRoot(), "Assets"), "*.meta", SearchOption.AllDirectories)
            .Select(f => File.ReadLines(f).FirstOrDefault(l => l.StartsWith("guid: "))?.Substring("guid: ".Length).Trim())
            .Where(g => g != null));

        using var cache = OpenCatalog();
        var missing = new List<string>();
        foreach (var hull in cache.GetAll<HullData>())
            if (!assetGuids.Contains(hull.Prefab ?? ""))
                missing.Add($"{hull.Name}.Prefab = {hull.Prefab ?? "null"}");
        foreach (var design in cache.GetAll<EquippableItemData>())
        {
            foreach (var thruster in design.Behaviors.OfType<ThrusterData>())
                if (!assetGuids.Contains(thruster.ParticlesPrefab ?? ""))
                    missing.Add($"{design.Name}.ParticlesPrefab = {thruster.ParticlesPrefab ?? "null"}");
            if (!string.IsNullOrEmpty(design.Schematic) && !assetGuids.Contains(design.Schematic))
                missing.Add($"{design.Name}.Schematic = {design.Schematic}");
        }

        Assert.True(missing.Count == 0, "Dangling asset references:\n" + string.Join("\n", missing));
    }

    [Theory]
    [InlineData("Longinus", 4)]
    [InlineData("Djinni", 8)]
    public void RestoredHullExistsWithExpectedThrusterHardpointCount(string name, int expectedThrusterHardpoints)
    {
        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>(name);
        Assert.NotNull(hull);
        Assert.Equal(HullType.Ship, hull.HullType);
        Assert.Equal(expectedThrusterHardpoints, hull.Hardpoints.Count(h => h.Type == HardpointType.Thruster));
    }

    [Theory]
    [InlineData("Longinus")]
    [InlineData("Djinni")]
    public void EveryThrusterHardpointAcceptsACatalogThrusterAtItsOwnPosition(string name)
    {
        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>(name);
        var thrusterDesigns = cache.GetAll<GearData>().Where(g => g.Hardpoint == HardpointType.Thruster).ToArray();
        Assert.NotEmpty(thrusterDesigns);

        var settings = Settings();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= hull.Hardpoints.Count + 1; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = .5f };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var hullItem = new EquippableItem { Data = cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 };
        var ship = new Ship(items, zone, hullItem, settings.DefaultEntitySettings);

        var lot = 2;
        foreach (var hardpoint in hull.Hardpoints.Where(h => h.Type == HardpointType.Thruster))
        {
            var cells = hardpoint.Shape.Coordinates.Length;
            var design = thrusterDesigns.FirstOrDefault(d => d.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) && d.Shape.Coordinates.Length == cells);
            Assert.True(design != null, $"{name}'s {hardpoint.Transform ?? hardpoint.Type.ToString()} hardpoint at {hardpoint.Position} has no fitting catalog thruster design (needs {cells} cells).");
            var gearItem = new EquippableItem { Data = cache.RefOf<ItemData>(design), Durability = design.Durability, Lot = lot++ };
            Assert.True(ship.TryEquip(gearItem, hardpoint.Position),
                $"{name}'s {hardpoint.Transform ?? hardpoint.Type.ToString()} hardpoint at {hardpoint.Position} refused {design.Name} via TryEquip.");
        }
    }

    // "The ship then moves and turns... for a few ticks" (docs/locomotion-cut.md verification list). Equips
    // every thruster hardpoint with a fitting design, then drives MovementDirection/LookDirection the same way
    // ActionGameManager's player-input path does (docs' own consumer audit) and checks Entity.Velocity and
    // Entity.Direction both move over a few ticks on the current (pre-Cut-3) mixer.
    [Theory]
    [InlineData("Longinus")]
    [InlineData("Djinni")]
    public void RestoredHullMovesAndTurnsUnderMovementAndLookDirection(string name)
    {
        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>(name);
        var thrusterDesigns = cache.GetAll<GearData>().Where(g => g.Hardpoint == HardpointType.Thruster).ToArray();

        var settings = Settings();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= hull.Hardpoints.Count + 1; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = .5f };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var hullItem = new EquippableItem { Data = cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 };
        var ship = new Ship(items, zone, hullItem, settings.DefaultEntitySettings);

        var lot = 2;
        foreach (var hardpoint in hull.Hardpoints.Where(h => h.Type == HardpointType.Thruster))
        {
            var cells = hardpoint.Shape.Coordinates.Length;
            var design = thrusterDesigns.First(d => d.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) && d.Shape.Coordinates.Length == cells);
            var gearItem = new EquippableItem { Data = cache.RefOf<ItemData>(design), Durability = design.Durability, Lot = lot++ };
            Assert.True(ship.TryEquip(gearItem, hardpoint.Position));
        }

        // A reactor: Victoire (one of Cut 1's own restored thrusters, docs/locomotion-cut.md's own note on it)
        // carries a real EnergyUsage draw unlike its four siblings, so an unpowered ship reads as though only
        // some fitting thrusters produce thrust -- an artifact of which design TryEquip's own hardpoint search
        // happens to land on, not of the restored content. Every hull here also has a Reactor hardpoint; a real
        // ship always carries one, so this fixture should too.
        var reactorHardpoint = hull.Hardpoints.FirstOrDefault(h => h.Type == HardpointType.Reactor);
        if (reactorHardpoint != null)
        {
            var reactorDesign = cache.GetAll<GearData>().First(g => g.Hardpoint == HardpointType.Reactor &&
                g.Shape.FitsWithin(reactorHardpoint.Shape, reactorHardpoint.Rotation, out _) && g.Shape.Coordinates.Length == reactorHardpoint.Shape.Coordinates.Length);
            var reactorItem = new EquippableItem { Data = cache.RefOf<ItemData>(reactorDesign), Durability = reactorDesign.Durability, Lot = lot++ };
            Assert.True(ship.TryEquip(reactorItem, reactorHardpoint.Position));
        }

        zone.Entities.Add(ship);
        ship.Position = float3.zero;
        ship.Activate();
        zone.Update(0f); // warm-up: resolves equipment stats before the driven ticks read them

        var startVelocity = ship.Velocity;
        var startDirection = ship.Direction;

        ship.MovementDirection = float2(0, 1); // full forward, same axis ActionGameManager.Input.Player.Move drives
        ship.LookDirection = float3(1, 0, 0); // off-axis target heading, so the mixer's yaw thrusters have work to do
        for (var i = 0; i < 10; i++) zone.Update(1f / 60f);

        Assert.NotEqual(startVelocity, ship.Velocity);
        Assert.NotEqual(startDirection, ship.Direction);
    }

    // Operator ruling, 2026-09-25 (docs/locomotion-cut.md, "Tutorial station"): "What we're testing is going to
    // be the tutorial level, right? Let's drop a station in there." The tutorial's entrance zone always gets a
    // generated faction station with a docking bay, deterministically -- not merely likely. This REPLACES
    // TutorialZoneGenerationCanProduceADockedStation, which never used the tutorial construction path at all: it
    // built an ad hoc single-zone SavedGame/Galaxy and asserted a docking bay showed up in at least one of 25
    // zone-name variants, which is a claim about the BACKGROUND station path in general, not about the entrance
    // zone Galaxy.cs actually picks (lowest-influence, unowned -- the one case ZoneGenerator.cs's own stationCount
    // roll produces 0 on essentially every seed, docs' own probe: 30 of 30). This test builds the real tutorial
    // Galaxy exactly as Assets/Resources/Settings.asset configures it and ActionGameManager.StartGame ->
    // PopulateLevel(CurrentGalaxy.Entrance) generates it, across seeds, and requires a docked station in every
    // run. It must fail without ZoneGenerator's tutorial-entrance override.
    [Fact]
    public void EntranceZoneAlwaysGetsADockedStationAcrossSeeds()
    {
        var zoneSettings = new ZoneGenerationSettings
        {
            PlanetSafetyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 2.5f, Constant = 0 },
            MassFloor = 1, SunMass = 5000, GasGiantMass = 1000, PlanetMass = 100,
            SatellitePasses = 5, SatelliteCreationMassFloor = 100, SatelliteCreationProbability = .25f,
            BinaryCreationProbability = .1f, RosetteProbability = .1f,
            ZoneRadius = new ExponentialLerp { Exponent = 1.5f, Minimum = 1000, Maximum = 10000 },
            ZoneMass = new ExponentialLerp { Exponent = 1.5f, Minimum = 10000, Maximum = 500000 },
            SubZoneCount = new ExponentialLerp { Exponent = 1.5f, Minimum = 0, Maximum = 8 },
            ZoneBoundaryRadius = .9f, BeltProbability = .25f, BeltMassCeiling = 500,
            AsteroidBeltWidth = new ExponentialCurve { Exponent = .666f, Multiplier = 5, Constant = 50 },
            AsteroidCount = new ExponentialCurve { Exponent = .5f, Multiplier = 1, Constant = 11 },
            AsteroidRotationSpeed = new ExponentialLerp { Exponent = 2, Minimum = .1f, Maximum = .5f },
            SunColorSaturation = .85f, SunSecondaryColorDistance = .33f, SunLightSaturation = .5f, SunFogTintSaturation = .55f,
            GasGiantBandCount = new ExponentialLerp { Exponent = 2, Minimum = 5, Maximum = 8 },
            GasGiantBandColorSeparation = .25f, GasGiantBandAltColorChance = .25f,
            GasGiantBandSaturation = new ExponentialLerp { Exponent = .5f, Minimum = 0, Maximum = .8f },
            GasGiantBandBrightness = new ExponentialLerp { Exponent = .25f, Minimum = .25f, Maximum = 1 },
            NameData = new string[0],
        };
        var tutorialSettings = new TutorialGenerationSettings
        {
            ProtagonistFaction = "Miss", AntagonistFaction = "Zhe", BufferFaction = "Luc",
            NeutralFactions = new[] { "Aero", "Finch" }, QuestFaction = "Adras", LinkDensity = .5f, ZoneCount = 64,
        };
        var background = new SectorBackgroundSettings
        {
            NoiseAmplitude = 1, NoiseOffset = .3f, NoiseGain = .7f, NoiseLacunarity = 2, NoiseFrequency = .1f,
            NoisePosition = 536.5106f, CloudExponent = 10, CloudAmplitude = .01f,
        };
        var names = new NameGeneratorSettings { NameGeneratorMinLength = 5, NameGeneratorMaxLength = 10, NameGeneratorOrder = 3 };
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");

        // Galaxy zone-graph generation has a pre-existing, seed-dependent flakiness of its own (a disconnected
        // zone graph throws inside the Galaxy constructor on roughly 1 seed in 15, confirmed unrelated to this
        // cut: it reproduces identically against the pre-fix-batch catalog). That failure belongs to Galaxy.cs,
        // not to ZoneGenerator's entrance-station override this test is pinning, so it is skipped rather than
        // asserted on; every seed whose Galaxy actually builds must still get a docked entrance station.
        var succeeded = 0;
        for (uint seed = 1; seed <= 25; seed++)
        {
            var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-station-{Guid.NewGuid():N}.cc");
            try
            {
                // Galaxy mutates Faction.InfluenceDistance in place; a fresh catalog open per seed keeps runs independent.
                using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
                Galaxy galaxy;
                try { galaxy = new Galaxy(tutorialSettings, background, names, cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed); }
                catch (Exception) { continue; }
                succeeded++;

                var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
                var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Entrance, isTutorial: true);

                var docked = pack.Entities.OfType<OrbitalEntityPack>()
                    .Any(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0);
                Assert.True(docked, $"seed {seed}: the tutorial entrance zone generated no station with a docking bay.");
            }
            finally
            {
                if (File.Exists(scratchRun)) File.Delete(scratchRun);
            }
        }

        Assert.True(succeeded >= 15, $"only {succeeded} of 25 seeds produced a Galaxy at all; too few runs to trust this result.");
    }

    // Speed cap (docs/locomotion-cut.md Cut 1 verification): Longinus's restored VelocityLimitData caps it at
    // 100 (legacy TopSpeed 100..100). Runs it under straight-line full forward thrust -- LookDirection held on
    // the ship's own heading, so the mixer never spends torque turning and thrust stays aligned with velocity --
    // long enough to approach its drag/thrust balance well past 100, and asserts the observed speed never
    // exceeds the cap at any sampled tick. Must fail when VelocityLimitData is removed (docs' own probe: Longinus
    // reaches 122 without it).
    [Fact]
    public void LonginusNeverExceedsItsRestoredTopSpeedUnderFullThrust()
    {
        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>("Longinus");
        var topSpeed = ((VelocityLimitData) hull.Behaviors.Single(b => b is VelocityLimitData)).TopSpeed.Max;
        var thrusterDesigns = cache.GetAll<GearData>().Where(g => g.Hardpoint == HardpointType.Thruster).ToArray();

        var settings = Settings();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= hull.Hardpoints.Count + 1; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = .5f };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var hullItem = new EquippableItem { Data = cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 };
        var ship = new Ship(items, zone, hullItem, settings.DefaultEntitySettings);

        var lot = 2;
        foreach (var hardpoint in hull.Hardpoints.Where(h => h.Type == HardpointType.Thruster))
        {
            var design = thrusterDesigns.First(d => d.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) && d.Shape.Coordinates.Length == hardpoint.Shape.Coordinates.Length);
            var gearItem = new EquippableItem { Data = cache.RefOf<ItemData>(design), Durability = design.Durability, Lot = lot++ };
            Assert.True(ship.TryEquip(gearItem, hardpoint.Position));
        }
        var reactorHardpoint = hull.Hardpoints.First(h => h.Type == HardpointType.Reactor);
        var reactorDesign = cache.GetAll<GearData>().First(g => g.Hardpoint == HardpointType.Reactor &&
            g.Shape.FitsWithin(reactorHardpoint.Shape, reactorHardpoint.Rotation, out _) && g.Shape.Coordinates.Length == reactorHardpoint.Shape.Coordinates.Length);
        Assert.True(ship.TryEquip(new EquippableItem { Data = cache.RefOf<ItemData>(reactorDesign), Durability = reactorDesign.Durability, Lot = lot++ }, reactorHardpoint.Position));

        zone.Entities.Add(ship);
        ship.Position = float3.zero;
        ship.Activate();
        zone.Update(0f);

        ship.MovementDirection = float2(0, 1); // full forward
        ship.LookDirection = float3(ship.Direction.x, 0, ship.Direction.y); // straight line: hold the ship's own heading

        for (var i = 0; i < 60 * 20; i++)
        {
            zone.Update(1f / 60f);
            var speed = length(ship.Velocity);
            // The mixer applies thrust then VelocityLimit clamps at the START of the next tick (Ship.cs's
            // per-behavior execution order), so one tick's own acceleration can carry speed briefly past the
            // cap before the following tick reins it in. The tolerance below is several times the base flight
            // table's own measured Longinus forward acceleration (172.38 m/s^2) at dt=1/60 (~2.9 per tick).
            Assert.True(speed <= topSpeed + 10f, $"tick {i}: speed {speed} exceeded the restored top speed {topSpeed} by more than one tick's acceleration.");
        }
    }

    // Loadout generation (docs/locomotion-cut.md Cut 1 verification): LoadoutGenerator, filtered to each
    // restored hull, must fill every one of its thruster hardpoints -- the check that the restored designs and
    // their FactionProductData/roles are authored correctly, not just present.
    [Theory]
    [InlineData("Longinus")]
    [InlineData("Djinni")]
    public void LoadoutGeneratorFillsEveryThrusterHardpointAcrossSeeds(string name)
    {
        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>(name);
        var thrusterHardpoints = hull.Hardpoints.Where(h => h.Type == HardpointType.Thruster).ToArray();
        var settings = Settings();

        for (uint seed = 1; seed <= 10; seed++)
        {
            var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
            var random = new Random(seed);
            var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
            var pack = generator.GenerateShipLoadout(candidate => candidate == hull);
            Assert.True(pack != null, $"{name}: seed {seed} produced no loadout.");

            foreach (var hardpoint in thrusterHardpoints)
            {
                var filled = pack.Equipment.Any(e => e.position.Equals(hardpoint.Position) &&
                    (cache.Get(e.item.Data) as GearData)?.Hardpoint == HardpointType.Thruster);
                Assert.True(filled, $"{name}: seed {seed}'s {hardpoint.Transform ?? hardpoint.Type.ToString()} hardpoint at {hardpoint.Position} got no thruster.");
            }
        }
    }
}
