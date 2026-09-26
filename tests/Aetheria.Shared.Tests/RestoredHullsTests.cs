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
    // Soul's finding #1 (this cut's fix batch 3): a hand-set GameplaySettings fixture left thermal radiation and
    // conduction at their bare-class defaults (1/1/1) against the shipped 3 / 1e-8 / 0.01
    // (Assets/Resources/Settings.asset), so equipped parts froze toward 0 K, performance collapsed to 0 from
    // cold, and TorqueMultiplier defaulting to 0 on top of that made every thruster's yaw contribution silently
    // zero (Ship.cs's mixer applies Torque*Thrust*TorqueMultiplier/Mass) -- velocity moved but Direction never
    // turned, which looks exactly like a content defect instead of a fixture gap. AuthoredSettings
    // (tools/AetherDb/AuthoredSettings.cs) is the same headless YAML reader tools/AetherDb already uses to
    // inspect Settings.asset outside Unity; this loads the real GameplaySettings the same way instead of
    // hand-copying its fields a second time.
    private static GameplaySettings Settings() => AuthoredSettings.Load(FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");

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

    // Shared tutorial-galaxy construction settings (Assets/Resources/Settings.asset's own values), factored out
    // so every test that needs a real tutorial Galaxy -- not just EntranceZoneAlwaysGetsADockedStationAcrossSeeds
    // -- builds it the same way instead of re-inlining the same ~20 fields.
    private static ZoneGenerationSettings TutorialZoneSettings() => new ZoneGenerationSettings
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

    private static TutorialGenerationSettings TutorialGalaxySettings() => new TutorialGenerationSettings
    {
        ProtagonistFaction = "Miss", AntagonistFaction = "Zhe", BufferFaction = "Luc",
        NeutralFactions = new[] { "Aero", "Finch" }, QuestFaction = "Adras", LinkDensity = .5f, ZoneCount = 64,
    };

    private static SectorBackgroundSettings TutorialBackgroundSettings() => new SectorBackgroundSettings
    {
        NoiseAmplitude = 1, NoiseOffset = .3f, NoiseGain = .7f, NoiseLacunarity = 2, NoiseFrequency = .1f,
        NoisePosition = 536.5106f, CloudExponent = 10, CloudAmplitude = .01f,
    };

    private static NameGeneratorSettings TutorialNameSettings() =>
        new NameGeneratorSettings { NameGeneratorMinLength = 5, NameGeneratorMaxLength = 10, NameGeneratorOrder = 3 };

    // A station's orbit is a copy of its source planet's own (Parent, Distance) (ZoneGenerator.CreateLagrangeOrbit),
    // so it always matches that one planet. It matches MORE than one planet only when the source planet shares its
    // distance with a sibling under the same parent -- exactly what ZoneGenerator's potentialLagrangePoints filter
    // excludes. An entrance station whose orbit matches more than one planet orbit this way could therefore only
    // have come from the widened candidate set, never from the ordinary (non-rosette) Lagrange selection.
    //
    // Soul's finding (this cut's fix batch 3): counting planet orbits as pack.Orbits.Count minus the
    // OrbitalEntityPack count (the previous version of this method) over-counts whenever a station or turret
    // loadout generator returns null -- CreateLagrangeOrbit already appended that orbit to pack.Orbits before the
    // null check (ZoneGenerator.cs) skips adding the entity, so the subtraction no longer matches. Deriving the
    // set from pack.Planets' own Orbit refs instead (Census.cs's own approach) sidesteps that entirely: every
    // real planet, including an ordinary rosette member, names its own orbit directly. Only a rosette ROOT is
    // excluded this way (it gets no BodyData/PlanetData entry at all), which is fine here -- a root never shares
    // its own Parent's distance with itself, so it was never a sibling candidate for `orbit` in the first place.
    private static bool IsRosetteMember(CultCache cache, ZonePack pack, OrbitData orbit)
    {
        if (!orbit.Parent.IsSet()) return false;
        var planetOrbits = pack.Planets.Select(p => cache.Get(cache.Get(p).Orbit)).ToArray();
        return planetOrbits.Count(o => o.Parent.IsSet() && o.Parent.Key.Equals(orbit.Parent.Key) && abs(o.Distance - orbit.Distance) < .1f) > 1;
    }

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
    //
    // Also requires that the NORMAL case (a zone with a real Lagrange candidate, the common case across 25 seeds)
    // seats its station on a genuine, non-rosette Lagrange orbit rather than the widened fallback -- the widened
    // candidate set is for the rare zone with none, not a replacement for ordinary placement.
    [Fact]
    public void EntranceZoneAlwaysGetsADockedStationAcrossSeeds()
    {
        var zoneSettings = TutorialZoneSettings();
        var tutorialSettings = TutorialGalaxySettings();
        var background = TutorialBackgroundSettings();
        var names = TutorialNameSettings();
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");

        // Galaxy zone-graph generation has a pre-existing, seed-dependent flakiness of its own (a disconnected
        // zone graph throws inside the Galaxy constructor on roughly 1 seed in 15, confirmed unrelated to this
        // cut: it reproduces identically against the pre-fix-batch catalog). That failure belongs to Galaxy.cs,
        // not to ZoneGenerator's entrance-station override this test is pinning, so it is skipped rather than
        // asserted on; every seed whose Galaxy actually builds must still get a docked entrance station.
        var succeeded = 0;
        var sawGenuineLagrangeOrbit = false;
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

                var dockedStation = pack.Entities.OfType<OrbitalEntityPack>()
                    .FirstOrDefault(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0);
                Assert.True(dockedStation != null, $"seed {seed}: the tutorial entrance zone generated no station with a docking bay.");

                if (!IsRosetteMember(cache, pack, cache.Get(dockedStation.Orbit)))
                    sawGenuineLagrangeOrbit = true;
            }
            finally
            {
                if (File.Exists(scratchRun)) File.Delete(scratchRun);
            }
        }

        Assert.True(succeeded >= 15, $"only {succeeded} of 25 seeds produced a Galaxy at all; too few runs to trust this result.");
        Assert.True(sawGenuineLagrangeOrbit, "none of the 25 seeds seated the entrance station on a genuine (non-rosette) Lagrange orbit.");
    }

    // Cut 1 fix batch 3 (F6, N1): a per-seed strengthening of the "across seeds" test above. That test only
    // requires a genuine Lagrange orbit at least once in 25 seeds; it says nothing about a seed where a genuine
    // candidate DID exist but the station landed on the widened fallback anyway (which the widening condition's
    // own isTutorialEntrance-and-empty-candidates guard should make impossible). This pins the normal case
    // directly, seed by seed: whenever the entrance zone actually has a genuine (non-rosette) Lagrange candidate,
    // its station must be seated on one.
    [Fact]
    public void EntranceStationSitsOnAGenuineLagrangeOrbitWheneverOneExists()
    {
        var zoneSettings = TutorialZoneSettings();
        var tutorialSettings = TutorialGalaxySettings();
        var background = TutorialBackgroundSettings();
        var names = TutorialNameSettings();
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");

        var succeeded = 0;
        var sawGenuineCandidate = false;
        for (uint seed = 1; seed <= 25; seed++)
        {
            var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-normalcase-{Guid.NewGuid():N}.cc");
            try
            {
                using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
                Galaxy galaxy;
                try { galaxy = new Galaxy(tutorialSettings, background, names, cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed); }
                catch (Exception) { continue; }
                succeeded++;

                var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
                var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Entrance, isTutorial: true);

                var planetOrbits = pack.Planets.Select(p => cache.Get(cache.Get(p).Orbit)).Where(o => o.Parent.IsSet()).ToArray();
                var hasGenuineCandidate = planetOrbits.Any(o => planetOrbits.Count(c => c.Parent.Key.Equals(o.Parent.Key) && abs(c.Distance - o.Distance) < .1f) == 1);
                if (!hasGenuineCandidate) continue;
                sawGenuineCandidate = true;

                var dockedStation = pack.Entities.OfType<OrbitalEntityPack>()
                    .FirstOrDefault(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0);
                Assert.True(dockedStation != null, $"seed {seed}: the tutorial entrance zone generated no station with a docking bay.");
                Assert.False(IsRosetteMember(cache, pack, cache.Get(dockedStation.Orbit)),
                    $"seed {seed}: a genuine Lagrange candidate existed, but the station sat on the widened (rosette) fallback anyway.");
            }
            finally
            {
                if (File.Exists(scratchRun)) File.Delete(scratchRun);
            }
        }

        Assert.True(succeeded >= 15, $"only {succeeded} of 25 seeds produced a Galaxy at all; too few runs to trust this result.");
        Assert.True(sawGenuineCandidate, "none of the 25 seeds had a genuine Lagrange candidate at all; too few runs to trust this result.");
    }

    // Cut 1 fix batch 3 (F5): the throw ZoneGenerator.cs's own entrance-station override raises
    // (ZoneGenerator.cs's "every planet in the zone is a parentless root" case, fixed in this same batch, F3) is
    // reachable through authored settings, not just a hypothetical: a real seed-1 tutorial entrance with
    // RosetteProbability 0 (no rosettes to widen into) and MassFloor far above every possible child mass (no
    // satellite ever clears the floor, so every planet stays a parentless root) leaves the entrance with no
    // candidate orbit at all, even after widening.
    [Fact]
    public void ThrowReachableWithAuthoredSettings()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-throw-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
                cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed: 1u);
            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

            var zoneSettings = TutorialZoneSettings();
            zoneSettings.RosetteProbability = 0;
            zoneSettings.MassFloor = 1e9f;

            Assert.Throws<InvalidOperationException>(() =>
                ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Entrance, isTutorial: true));
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Cut 1 fix batch 3 (F6, N3): a multi-root, heavily-widened entrance via authored settings -- RosetteProbability
    // 1 (every subzone becomes a rosette) and a fixed SubZoneCount of 8 (several independent rosette systems, each
    // its own root, never each other's sibling) with satellite/binary creation turned off so as few non-rosette
    // orbits form as this generator's own unconditional "captured planet" step (ZoneGenerator.cs's ExpandRosette
    // caller always adds at least one) allows. Verified directly against seed 1's real entrance: this leaves 14
    // distinct parents among its planets, not the single root a boring system would have. Whatever candidate
    // selection actually runs on a topology this fragmented -- ordinary or widened -- every resulting station
    // orbit must still be a real, positioned orbit: a set Parent and a positive Distance, never the zero-distance,
    // parentless orbit the pre-F4 fallback used to synthesize.
    [Fact]
    public void MultiRootEntranceStationOrbitsAreAlwaysParentedWithPositiveDistance()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-multiroot-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
                cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed: 1u);
            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

            var zoneSettings = TutorialZoneSettings();
            zoneSettings.RosetteProbability = 1;
            zoneSettings.SubZoneCount = new ExponentialLerp { Exponent = 1, Minimum = 8, Maximum = 8 };
            zoneSettings.SatelliteCreationProbability = 0;
            zoneSettings.BinaryCreationProbability = 0;

            var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Entrance, isTutorial: true);

            var roots = pack.Planets.Select(p => cache.Get(p).Orbit)
                .Select(cache.Get)
                .Select(o => o.Parent.IsSet() ? o.Parent.Key.Value : "(root)")
                .Distinct().Count();
            Assert.True(roots > 1, $"this fixture no longer produces a multi-root entrance (only {roots} distinct parent); it no longer tests the fragmented-topology case.");

            var stations = pack.Entities.OfType<OrbitalEntityPack>()
                .Where(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station).ToArray();
            Assert.NotEmpty(stations);
            foreach (var station in stations)
            {
                var orbit = cache.Get(station.Orbit);
                Assert.True(orbit.Parent.IsSet(), "a station's orbit has no Parent.");
                Assert.True(orbit.Distance > 0, $"a station's orbit Distance is not positive: {orbit.Distance}.");
            }
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Cut 1 fix batch 3 (F6, N9): a station-offset pin. CreateLagrangeOrbit (ZoneGenerator.cs) builds the
    // station's orbit as a COPY of its source planet's (Parent, Distance) but offsets Phase by
    // PI/3 * sign(random...) -- a real angular displacement, not the same orbital slot. Must fail if that offset
    // is ever dropped (a mutant that seats the station directly on the planet's own orbit object): the station's
    // Phase must differ from every planet's Phase among those sharing its own (Parent, Distance), across seeds.
    [Fact]
    public void EntranceStationPhaseDiffersFromEveryPlanetAtItsOwnDistanceAcrossSeeds()
    {
        var zoneSettings = TutorialZoneSettings();
        var tutorialSettings = TutorialGalaxySettings();
        var background = TutorialBackgroundSettings();
        var names = TutorialNameSettings();
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");

        var checkedAny = false;
        for (uint seed = 1; seed <= 25; seed++)
        {
            var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-offset-{Guid.NewGuid():N}.cc");
            try
            {
                using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
                Galaxy galaxy;
                try { galaxy = new Galaxy(tutorialSettings, background, names, cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed); }
                catch (Exception) { continue; }

                var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
                var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Entrance, isTutorial: true);

                var dockedStation = pack.Entities.OfType<OrbitalEntityPack>()
                    .FirstOrDefault(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0);
                if (dockedStation == null) continue;
                var stationOrbit = cache.Get(dockedStation.Orbit);
                if (!stationOrbit.Parent.IsSet()) continue;

                var siblingPlanets = pack.Planets.Select(p => cache.Get(cache.Get(p).Orbit))
                    .Where(o => o.Parent.IsSet() && o.Parent.Key.Equals(stationOrbit.Parent.Key) && abs(o.Distance - stationOrbit.Distance) < .1f)
                    .ToArray();
                if (siblingPlanets.Length == 0) continue;
                checkedAny = true;

                foreach (var planetOrbit in siblingPlanets)
                    Assert.True(abs(planetOrbit.Phase - stationOrbit.Phase) > 1e-3f,
                        $"seed {seed}: the docked station's Phase ({stationOrbit.Phase}) matches a planet's own Phase at the same (Parent, Distance) -- it is sitting on the planet's own orbital slot instead of an offset one.");
            }
            finally
            {
                if (File.Exists(scratchRun)) File.Delete(scratchRun);
            }
        }

        Assert.True(checkedAny, "no seed produced a docked entrance station with an identifiable source planet at its own (Parent, Distance); too few runs to trust this result.");
    }

    // Speed cap (docs/locomotion-cut.md Cut 1 verification): Longinus's restored VelocityLimitData caps it at
    // the legacy TopSpeed, 100 (100..100) -- asserted as the literal from the legacy record rather than read
    // back out of the same catalog value being tested, so a mutated TopSpeed (e.g. 1000) can't also move the
    // bound this test checks against.
    //
    // Cut 1 fix batch 3 (Soul's finding #1): the comment this replaces claimed the run went "long enough to
    // approach its drag/thrust balance well past 100" while forcing OverrideShutdown on the hull and both
    // firing thrusters for the whole scenario -- a fixture compensating for RestoredHullsTests.Settings() (then
    // hand-set, thermal radiation/conduction frozen at 1/1/1 against the shipped 3 / 1e-8 / 0.01) making every
    // part freeze toward 0 K and shut down within about a second on real physics. Settings() now loads the real
    // GameplaySettings (AuthoredSettings, above), so OverrideShutdown is no longer needed to keep the ship
    // flying: a real, unforced Longinus holds thrust (and the hull's own VelocityLimit) running well past the
    // window this test drives.
    //
    // Real settings also retired the old +-10 tolerance's premise. On real physics the mixer's own tick order
    // (Ship.cs: a tick's thrust-driven velocity is checked against VelocityLimit's cap on the FOLLOWING tick)
    // settles into a steady state at the cap plus one tick's own forward acceleration, not at the literal cap --
    // measured here at ~103.3 for this seed's generated loadout, not the ~110 an unconstrained +-10 tolerance
    // would also accept. +4 keeps that real headroom on the ceiling side. The floor is tighter (-3): a
    // materially nerfed cap (Soul's own catalog mutant, TopSpeed 100 -> 92) still produces the same kind of
    // steady state, just a lower one (~95), which -3 catches but +-10 or a +4 ceiling would not.
    //
    // Must fail when VelocityLimitData is removed (docs' own probe: Longinus reaches 122 without it), when
    // TopSpeed is mutated away from 100, and when VelocityLimit's own trigger check is loosened (a "+6 slack"
    // mutant pushes this same steady state to ~109, over the +4 ceiling).
    [Fact]
    public void LonginusNeverExceedsItsRestoredTopSpeedUnderFullThrust()
    {
        const float LegacyTopSpeed = 100f; // Longinus's legacy-record TopSpeed (100..100)

        using var cache = OpenCatalog();
        var hull = cache.GetByName<HullData>("Longinus");
        var settings = Settings();
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        // ItemManager.Random defaults to a DateTime.Now-seeded instance; without pinning it here, the item
        // qualities LoadoutGenerator/EntitySerializer roll through it (not through the `random` below, which
        // only drives which designs get picked) would make this test's own measured speed non-deterministic.
        items.Random = new Random(1u);
        var random = new Random(1);
        var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
        var pack = generator.GenerateShipLoadout(candidate => candidate == hull);
        Assert.NotNull(pack);
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var ship = (Ship) EntitySerializer.Unpack(items, zone, pack);

        zone.Entities.Add(ship);
        ship.Position = float3.zero;
        ship.Activate();
        zone.Update(0f);

        ship.MovementDirection = float2(0, 1); // full forward
        ship.LookDirection = float3(ship.Direction.x, 0, ship.Direction.y); // straight line: hold the ship's own heading

        // Soul measured this loadout's forward thrusters crossing 386 K (thermal shutdown) around 7.3 s under
        // real settings; 5 s keeps every tick inside the window where thrust runs continuously and unforced, so
        // the cap -- not a coasting or shut-down ship -- is what the assertions below are pinning.
        const int ticks = 60 * 5;
        var lastSpeed = 0f;
        for (var i = 0; i < ticks; i++)
        {
            zone.Update(1f / 60f);
            var speed = length(ship.Velocity);
            lastSpeed = speed;
            Assert.True(speed <= LegacyTopSpeed + 4f, $"tick {i}: speed {speed} exceeded the legacy top speed {LegacyTopSpeed} by more than the real tick-order headroom.");
        }

        Assert.True(lastSpeed >= LegacyTopSpeed - 3f, $"final speed {lastSpeed} fell too far below the legacy top speed {LegacyTopSpeed}; the cap looks nerfed.");
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

    // Builds the real tutorial galaxy for `seed`, then re-targets its Entrance to `entranceZoneName` via the
    // public SavedGame/SavedZone round trip (RunSave's own shape: Galaxy -> SavedZone[] -> Galaxy(cache,
    // SavedGame, log)), without touching Galaxy's private Entrance backing field by reflection. Zone content
    // (planets) is keyed off each zone's own Name/Position, not off which zone happens to be Entrance, so the
    // re-targeted galaxy's chosen zone generates identically to the original -- letting a test pick, by name, any
    // real zone from a real galaxy (e.g. one already known to have no Lagrange candidate) and make it the Entrance.
    private static Galaxy BuildGalaxyWithForcedEntrance(CultCache cache, uint seed, string entranceZoneName)
    {
        var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
            cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed);
        var target = galaxy.Zones.Single(z => z.Name == entranceZoneName);

        var factions = galaxy.Factions;
        var savedZones = galaxy.Zones.Select(zone => new SavedZone
        {
            Name = zone.Name,
            Position = zone.Position,
            AdjacentZones = zone.AdjacentZones.Select(az => Array.IndexOf(galaxy.Zones, az)).ToArray(),
            Factions = zone.Factions.Select(f => Array.IndexOf(factions, f)).ToArray(),
            Contents = zone.PackedContents,
            Owner = zone.Owner == null ? -1 : Array.IndexOf(factions, zone.Owner),
        }).ToArray();
        var keys = savedZones.Select((_, i) => new CultRecordKey($"restoredhullstests-forced-entrance-zone-{i}")).ToArray();
        cache.Commit(batch => { for (var i = 0; i < keys.Length; i++) batch.Upsert(typeof(SavedZone), savedZones[i], keys[i]); });

        var game = new SavedGame
        {
            Zones = keys.Select(k => new CultRecordRef<SavedZone>(k)).ToArray(),
            Factions = factions.Select(f => cache.RefOf(f)).ToArray(),
            HomeZones = galaxy.HomeZones.ToDictionary(x => Array.IndexOf(factions, x.Key), x => Array.IndexOf(galaxy.Zones, x.Value)),
            BossZones = galaxy.BossZones.ToDictionary(x => Array.IndexOf(factions, x.Key), x => Array.IndexOf(galaxy.Zones, x.Value)),
            Entrance = Array.IndexOf(galaxy.Zones, target),
            Exit = -1,
            Relationships = factions.Select(f => galaxy.FactionRelationships[f]).ToArray(),
            DiscoveredZones = Array.Empty<int>(),
            Background = galaxy.Background,
            IsTutorial = true,
        };
        return new Galaxy(cache, game, _ => { });
    }

    // Cut 1 fix batch 2 (Soul's finding #2): the tutorial entrance's widened candidate set. Seed 3's real tutorial
    // galaxy has a zone, "EAC-7089", whose planets are entirely one rosette with no captured satellites at all: 4
    // planets sharing one Distance under one Parent, and nothing else -- potentialLagrangePoints is empty for it
    // (verified directly against this exact fixture, not carried over from Soul's own forced-fallback probe, whose
    // own candidate check undercounted planet orbits by using pack.Planets.Count -- which excludes Empty rosette
    // roots -- and so misidentified some zones, including its originally-reported "EAC-3208", as candidate-less
    // when they were not). Re-targeted here to be the Entrance through BuildGalaxyWithForcedEntrance above. Its
    // docked station can therefore only have come from the widened candidate set (every parented planet orbit,
    // not just non-rosette ones), never from a synthesized, unparented fallback orbit. Pins: still docked, and
    // turrets sit on a real orbit (a real Parent, a finite Phase).
    //
    // Cut 1 fix batch 3 (F7): the Distance check this comment used to describe as "near their station" was
    // vacuous -- PlaceTurret (ZoneGenerator.cs) sets a turret orbit's Distance from `orbit.Distance` directly,
    // unconditionally, the same as CreateLagrangeOrbit does for the station itself, so every turret's Distance
    // equals the station's by construction regardless of placement; the check could never fail. What actually
    // makes a turret "near" its station is Phase: PlaceTurrets' own `dist = t/2` gives each turret a small
    // angular nudge (`20 * dist / orbit.Distance`) off the station's own Phase, not a placement anywhere else on
    // the orbit. Replaced with a bound on that real offset, generous over PlaceTurrets' own formula.
    [Fact]
    public void TutorialEntranceWithNoLagrangeCandidateStillSeatsAStationWithFiniteNearbyTurrets()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-widened-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = BuildGalaxyWithForcedEntrance(cache, seed: 3u, "EAC-7089");

            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
            var pack = ZoneGenerator.GenerateZone(items, TutorialZoneSettings(), galaxy, galaxy.Entrance, isTutorial: true);

            var station = pack.Entities.OfType<OrbitalEntityPack>()
                .FirstOrDefault(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0);
            Assert.True(station != null, "the known no-Lagrange-candidate entrance zone generated no docked station.");
            var stationOrbit = cache.Get(station.Orbit);

            // Confirms this fixture still exercises the widened path (a regression here would mean the fixture no
            // longer probes what this test claims to probe).
            Assert.True(IsRosetteMember(cache, pack, stationOrbit), "the entrance station no longer sits on a rosette-only orbit -- this fixture no longer tests the widened candidate path.");

            var turrets = pack.Entities.OfType<OrbitalEntityPack>()
                .Where(e => (cache.Get(e.Hull.Data) as HullData)?.HullType != HullType.Station).ToArray();
            Assert.NotEmpty(turrets);
            // Generous over PlaceTurrets' own max |distanceMultiplier| ((turrets.Length - 1) / 2): a real bound,
            // not the vacuous Distance-always-matches check this replaces.
            var maxPhaseOffset = 20f * turrets.Length / stationOrbit.Distance;
            foreach (var turret in turrets)
            {
                var turretOrbit = cache.Get(turret.Orbit);
                Assert.True(turretOrbit.Parent.IsSet(), "a turret's orbit has no Parent.");
                Assert.False(float.IsNaN(turretOrbit.Phase) || float.IsInfinity(turretOrbit.Phase), $"a turret's orbit Phase is not finite: {turretOrbit.Phase}.");
                var phaseDelta = abs(turretOrbit.Phase - stationOrbit.Phase);
                Assert.True(phaseDelta <= maxPhaseOffset,
                    $"a turret's Phase ({turretOrbit.Phase}) is not near the station's own Phase ({stationOrbit.Phase}): delta {phaseDelta} > {maxPhaseOffset}.");
            }
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Cut 1 fix batch 2 (Soul's finding #2, leak check): the entrance override must not leak to (a) other
    // tutorial zones or (b) a non-tutorial game's own entrance zone. Both are pinned against a seed-1 galaxy --
    // the widened-candidate test above uses a different fixture, seed 3's "EAC-7089" -- so this test and the one
    // below it stand or fall on this one known fixture, not that one.
    [Fact]
    public void EntranceOverrideDoesNotLeakToOtherTutorialZonesOrNonTutorialGames()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-leak-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
                cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed: 1u);
            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

            int StationCount(GalaxyZone zone, bool isTutorial) =>
                ZoneGenerator.GenerateZone(items, TutorialZoneSettings(), galaxy, zone, isTutorial)
                    .Entities.OfType<OrbitalEntityPack>()
                    .Count(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station);

            // (a) A non-entrance tutorial zone that naturally rolls zero stations keeps that roll under
            // isTutorial: true -- the floor must not leak past the galaxy.Entrance reference check.
            var otherZone = galaxy.Zones.Single(z => z.Name == "EAC-2733");
            Assert.NotSame(galaxy.Entrance, otherZone);
            Assert.Equal(0, StationCount(otherZone, isTutorial: true));

            // (b) The entrance zone itself, generated for a non-tutorial game (isTutorial: false), keeps its
            // natural roll too -- the floor must not leak past the isTutorial flag either.
            Assert.Equal(0, StationCount(galaxy.Entrance, isTutorial: false));
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Cut 1 fix batch 2 (Soul's finding #2, baseStationCount): the entrance override raises stationCount (the
    // orbit-selection floor) but must not also raise the enemyCount roll, which is keyed to baseStationCount
    // (the pre-override value) precisely so the forced station doesn't buy the zone an extra enemy. Seed 1's
    // entrance zone rolls baseStationCount 0 (confirmed via the isTutorial: false StationCount == 0 above) and
    // the override raises stationCount to 1; this pins the resulting ship count at the value baseStationCount
    // produces. It must fail if the enemy-count formula is changed to add stationCount instead (it would add a
    // ship instead of zero).
    [Fact]
    public void ForcedTutorialEntranceStationDoesNotInflateEnemyCount()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-entrance-enemycount-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
                cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed: 1u);
            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

            var pack = ZoneGenerator.GenerateZone(items, TutorialZoneSettings(), galaxy, galaxy.Entrance, isTutorial: true);
            var stationCount = pack.Entities.OfType<OrbitalEntityPack>().Count(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station);
            // Soul's finding (this cut's fix batch 3): pack.Entities.OfType<ShipPack>().Count() also counts the
            // zone's neutral wanderers (ZoneGenerator.cs's own EligibleWandererFactions spawn, keyed to
            // ZoneGenerationSettings.NeutralWandererCount -- 2 by TutorialZoneSettings' own default, unrelated to
            // this fixture's own enemy roll), so the old assertion of exactly 2 was pinning the wanderer count,
            // not the enemy count baseStationCount is supposed to gate. The nearest faction here is the same one
            // ZoneGenerator computes internally (galaxy.HomeZones' own distance-to-zone ordering); every ShipPack
            // names its faction directly (EntityPack.Faction), so filtering on it separates the two spawns.
            var nearestFaction = galaxy.Factions.MinBy(f => galaxy.HomeZones[f].Distance[galaxy.Entrance]);
            var enemyShipCount = pack.Entities.OfType<ShipPack>().Count(s => cache.Get(s.Faction) == nearestFaction);

            Assert.Equal(1, stationCount); // the override did raise the station count (from a natural roll of 0)
            Assert.Equal(0, enemyShipCount); // the enemy roll stayed keyed to the pre-override baseStationCount of 0
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Cut 1 fix batch 3 (F6, N2): the entrance-only widening must not leak to a non-entrance zone that would
    // otherwise get zero stations. Seed 3's "EAC-7089" (the same no-Lagrange fixture the widened-candidate test
    // above uses, this time NOT retargeted as Entrance) rolls a real, nonzero station count on its own -- forced
    // here by boosting one faction's presence there (HomeZone distance 0, InfluenceDistance 50, so the roll's
    // floor(rand * (factionPresence+1)) is overwhelmingly likely nonzero) via the same public SavedGame round
    // trip BuildGalaxyWithForcedEntrance uses, not by touching ZoneGenerator's private roll. Verified directly:
    // temporarily widening every zone (dropping ZoneGenerator's isTutorialEntrance guard) turns this fixture's
    // station count from 0 into 2, so a natural nonzero roll really is hiding behind the empty candidate set.
    // Must fail if that guard is dropped for real.
    private static Galaxy BuildGalaxyWithBoostedPresence(CultCache cache, uint seed, string zoneName)
    {
        var galaxy = new Galaxy(TutorialGalaxySettings(), TutorialBackgroundSettings(), TutorialNameSettings(),
            cache, new PlayerSettings(), new DirectoryInfo(Path.GetTempPath()), _ => { }, null, seed);
        var target = galaxy.Zones.Single(z => z.Name == zoneName);

        var factions = galaxy.Factions;
        var savedZones = galaxy.Zones.Select(zone => new SavedZone
        {
            Name = zone.Name,
            Position = zone.Position,
            AdjacentZones = zone.AdjacentZones.Select(az => Array.IndexOf(galaxy.Zones, az)).ToArray(),
            Factions = zone.Factions.Select(f => Array.IndexOf(factions, f)).ToArray(),
            Contents = zone.PackedContents,
            Owner = zone.Owner == null ? -1 : Array.IndexOf(factions, zone.Owner),
        }).ToArray();
        var keys = savedZones.Select((_, i) => new CultRecordKey($"restoredhullstests-boosted-presence-zone-{i}")).ToArray();
        cache.Commit(batch => { for (var i = 0; i < keys.Length; i++) batch.Upsert(typeof(SavedZone), savedZones[i], keys[i]); });

        var homeZones = galaxy.HomeZones.ToDictionary(x => Array.IndexOf(factions, x.Key), x => Array.IndexOf(galaxy.Zones, x.Value));
        var boosted = Enumerable.Range(0, factions.Length).OrderByDescending(i => factions[i].InfluenceDistance).First();
        homeZones[boosted] = Array.IndexOf(galaxy.Zones, target);
        factions[boosted].InfluenceDistance = 50;

        var game = new SavedGame
        {
            Zones = keys.Select(k => new CultRecordRef<SavedZone>(k)).ToArray(),
            Factions = factions.Select(f => cache.RefOf(f)).ToArray(),
            HomeZones = homeZones,
            BossZones = galaxy.BossZones.ToDictionary(x => Array.IndexOf(factions, x.Key), x => Array.IndexOf(galaxy.Zones, x.Value)),
            Entrance = Array.IndexOf(galaxy.Zones, galaxy.Entrance),
            Exit = -1,
            Relationships = factions.Select(f => galaxy.FactionRelationships[f]).ToArray(),
            DiscoveredZones = Array.Empty<int>(),
            Background = galaxy.Background,
            IsTutorial = true,
        };
        return new Galaxy(cache, game, _ => { });
    }

    [Fact]
    public void WideningDoesNotLeakToANonEntranceNoLagrangeZoneThatRollsAStation()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-widen-leak-{Guid.NewGuid():N}.cc");
        try
        {
            using var cache = OpenReadOnlyRealCatalogWithScratchRun(gameData, scratchRun);
            var galaxy = BuildGalaxyWithBoostedPresence(cache, seed: 3u, "EAC-7089");
            var zone = galaxy.Zones.Single(z => z.Name == "EAC-7089");
            Assert.NotSame(galaxy.Entrance, zone);

            var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
            var pack = ZoneGenerator.GenerateZone(items, TutorialZoneSettings(), galaxy, zone, isTutorial: true);

            // Confirms this fixture still has no genuine (non-rosette) Lagrange candidate -- a regression here
            // would mean the fixture no longer probes what this test claims to probe.
            var planetOrbits = pack.Planets.Select(p => cache.Get(cache.Get(p).Orbit)).Where(o => o.Parent.IsSet()).ToArray();
            var hasGenuineCandidate = planetOrbits.Any(o => planetOrbits.Count(c => c.Parent.Key.Equals(o.Parent.Key) && abs(c.Distance - o.Distance) < .1f) == 1);
            Assert.False(hasGenuineCandidate, "the entrance-adjacent fixture zone now has a genuine Lagrange candidate; it no longer tests the no-Lagrange leak path.");

            var stationCount = pack.Entities.OfType<OrbitalEntityPack>().Count(e => (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station);
            Assert.Equal(0, stationCount);
        }
        finally
        {
            if (File.Exists(scratchRun)) File.Delete(scratchRun);
        }
    }

    // Soul's finding #3 (asset identity): the existence-only check (EveryAssetReferenceResolvesToAMetaGuid) passes
    // even when Djinni.Prefab is swapped for Longinus's -- both are real GUIDs somewhere under Assets/. This pins
    // the actual identity: each restored reference names the specific legacy-record file it is supposed to, not
    // merely some meta file that happens to exist. Must fail under Soul's own "djprefab" mutation (Djinni.Prefab
    // set to Longinus's GUID).
    [Fact]
    public void RestoredAssetReferencesNameTheirOwnLegacyFileNotJustAnyExistingGuid()
    {
        string GuidOf(string assetPathUnderRepoRoot)
        {
            var meta = Path.Combine(FindRepoRoot(), assetPathUnderRepoRoot + ".meta");
            return File.ReadLines(meta).First(l => l.StartsWith("guid: ")).Substring("guid: ".Length).Trim();
        }

        using var cache = OpenCatalog();

        var djinniPrefabGuid = GuidOf("Assets/Content/Prefabs/Ships/Djinni.prefab");
        var djinni = cache.GetByName<HullData>("Djinni");
        Assert.Equal(djinniPrefabGuid, djinni.Prefab);

        // Every restored thruster's particle effect is Thruster 1.prefab, by the legacy record (soul-dump.txt).
        var thruster1Guid = GuidOf("Assets/Content/Prefabs/Thrusters/Thruster 1.prefab");
        var restoredThrusterNames = new[] { "deep space burnout", "Large Drive", "Victoire", "Small Drive", "Talaria", "RevvITup 2.0", "Medium Drive" };
        foreach (var name in restoredThrusterNames)
        {
            var design = cache.GetAll<GearData>().First(g => g.Name == name);
            var thrusterBehavior = design.Behaviors.OfType<ThrusterData>().Single();
            Assert.Equal(thruster1Guid, thrusterBehavior.ParticlesPrefab);
        }
    }
}
