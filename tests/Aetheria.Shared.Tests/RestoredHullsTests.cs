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

    // Same registry-scoping problem FireControlCut7Tests.OpenReadOnlyRealCatalog solves for a catalog-only open:
    // AetheriaStores.Open's `new CultCache()` uses the shared, AppDomain-scanning registry, which also picks up
    // this test assembly's own [CultDocument] fixture types and then demands a record for each -- so this test,
    // which also needs a writable scratch run store (RunSave.Commit's SavedGame/SavedZone), builds the same
    // catalog+run composition AetheriaStores.Open does, but against a registry scoped to the shipped assembly.
    private static CultCache OpenRealCatalogWithScratchRun(string catalogPath, string runPath)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, true), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(runPath), AetheriaStores.RunTypes);
        return cache;
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
        System.Console.WriteLine($"DEBUG end velocity {ship.Velocity} direction {ship.Direction}");

        Assert.NotEqual(startVelocity, ship.Velocity);
        Assert.NotEqual(startDirection, ship.Direction);
    }

    // Operator addition, 2026-09-25: "What we're testing is going to be the tutorial level... Let's drop a
    // station in there." ZoneGenerator's STORY station path (galaxyZone.Locations, populated by StoryProcessor)
    // is dead in the live code today -- Galaxy's tutorial constructor has that call commented out
    // (Assets/Scripts/ServerShared/Galaxy.cs:242) -- so every zone's stations come from the BACKGROUND path
    // (ZoneGenerator.cs:290-304): a random count near the owning faction, placed at whatever Lagrange points
    // that zone's own planet generation produced. That placement is genuinely probabilistic (stationCount can
    // roll 0, and a zone with no sub-zones has nowhere to put one), so this drives real generation across many
    // zone names/seeds and asserts at least one produces a station with a docking bay -- proof this repo's own
    // ZoneGenerator can still place a station on the restored catalog, not a claim that every zone gets one.
    //
    // Builds the same minimal SavedGame/Galaxy round trip the shipped AetherDb `loadout exclude-sellers` command
    // and FireControlCut6cTests use, scoped to Aeronautics Unlimited (Zenith's own seller, docs probe) as the
    // zone's sole owning/nearest faction so factionPresence is high and stations are not competing for the RNG
    // draw with distant, uninvolved factions.
    [Fact]
    public void TutorialZoneGenerationCanProduceADockedStation()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        var zoneSettings = new ZoneGenerationSettings
        {
            SunMass = 5000,
            PlanetMass = 100,
            SatellitePasses = 2,
            SatelliteCreationProbability = 0,
            BinaryCreationProbability = 0,
            RosetteProbability = 0,
            ZoneRadius = new ExponentialLerp { Minimum = 1000, Maximum = 1000, Exponent = 1 },
            ZoneMass = new ExponentialLerp { Minimum = 1000, Maximum = 1000, Exponent = 1 },
            SubZoneCount = new ExponentialLerp { Minimum = 4, Maximum = 4, Exponent = 1 },
            ZoneBoundaryRadius = 4,
            BeltProbability = 0,
            PlanetSafetyRadius = new ExponentialCurve { Multiplier = 1, Exponent = 0 },
        };

        var found = false;
        for (var seed = 0; seed < 25 && !found; seed++)
        {
            var scratchRun = Path.Combine(Path.GetTempPath(), $"aetheria-restoredhulls-tutorial-{Guid.NewGuid():N}.cc");
            try
            {
                using var cache = OpenRealCatalogWithScratchRun(gameData, scratchRun);
                var au = cache.GetAll<Faction>().Single(f => f.ShortName == "AU");
                var auRef = cache.RefOf(au);
                var game = new SavedGame
                {
                    Background = new SectorBackgroundSettings
                    {
                        NoiseAmplitude = 1, NoiseGain = .5f, NoiseLacunarity = 2, NoiseFrequency = .1f, CloudExponent = 10, CloudAmplitude = 1,
                    },
                    Factions = new[] { auRef },
                    Relationships = new[] { FactionRelationship.Neutral },
                    HomeZones = new Dictionary<int, int> { { 0, 0 } },
                    BossZones = new Dictionary<int, int>(),
                    DiscoveredZones = new[] { 0 },
                    ActionBarBindings = new SavedActionBarBinding[0],
                    Entrance = 0,
                    Exit = -1,
                };
                var zones = new[]
                {
                    new SavedZone { Name = $"Test Zone {seed}", Position = float2(seed, 0), AdjacentZones = Array.Empty<int>(), Factions = new[] { 0 }, Owner = 0, Contents = null },
                };
                RunSave.Commit(cache, game, zones, new ProvenanceLedger());
                var galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });

                var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
                var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, galaxy.Zones[0], isTutorial: true);

                if (pack.Entities.OfType<OrbitalEntityPack>().Any(e =>
                        (cache.Get(e.Hull.Data) as HullData)?.HullType == HullType.Station && e.DockingBays.Length > 0))
                    found = true;
            }
            finally
            {
                if (File.Exists(scratchRun)) File.Delete(scratchRun);
            }
        }

        Assert.True(found, "No station with a docking bay generated in 25 zone attempts against the restored catalog.");
    }
}
