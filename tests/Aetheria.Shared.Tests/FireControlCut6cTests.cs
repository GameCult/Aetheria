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
using static CultMath.math;
using float3 = CultMath.float3;
using int2 = CultMath.int2;
using Random = CultMath.Random;

// Cut 6c (docs/fire-control-cut.md, "the formula gets fixed, and the single point of failure"): pins 6c.1
// (Resolution is a benefit, not a cost -- HitProbability takes its reciprocal) and 6c.2 (a galaxy with no
// seller of a required targeting design degrades the loadout instead of throwing). Builds its own fixtures,
// the same convention every earlier cut's test file establishes.
public sealed class FireControlCut6cTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut6c-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut6cTests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetArmorInfoThreshold = .2f,
        TargetGearInfoThreshold = .8f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 1f,
        UnaidedAccuracy = .05f,
        UnaidedTracking = 10f,
        AgentMinHitProbability = 0f,
        BeamResolveInterval = .1f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private static Shape SolidShape(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // ---- 6c.1: a shooter/target engagement, resolution-parameterised, the same shape FireAuthorityTests'
    // and FireControlCut5Tests' own Build helpers use. ----

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
    }

    private Engagement Build(GameplaySettings settings, float resolution, float targetRange = 100, float range = 200)
    {
        var hullShape = SolidShape(5, 5);
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(10), Range = Constant(range), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0),
                Penetration = Constant(0), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(1f), Resolution = Constant(resolution), Precision = Constant(0f), Tracking = Constant(1000f)
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 };
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 };
        for (var i = 5; i <= 12; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(e => e.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 9 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, targetRange);
        shooter.Target.Value = target;
        shooter.SetIff(target, true);

        zone.Update(0f); // warm-up: resolves the targeting system's stats before HitProbability reads them

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    // 6c.1 (operator ruling 2026-09-19): Resolution is a benefit -- higher stays better -- so two identical
    // engagements differing only in Resolution must have the HIGHER-Resolution one score the higher (or
    // equal, at saturation) HitProbability at every info level between detection and full, strictly higher
    // at a level where neither has yet saturated. Mutation: read Resolution as the ceiling directly instead
    // of taking its reciprocal (the shipped Cut 6a behaviour) -- that inverts the relationship, so the low-
    // Resolution engagement scores higher and this test goes red.
    [Fact]
    public void HigherResolutionYieldsHigherOrEqualHitProbability()
    {
        var settings = TestSettings();
        var low = Build(settings, resolution: 2f);
        var high = Build(settings, resolution: 4f);

        foreach (var info in new[] { .15f, .2f, .3f, .5f, .75f, .99f })
        {
            low.Shooter.EntityInfoGathered[low.Target] = info;
            high.Shooter.EntityInfoGathered[high.Target] = info;
            var pLow = FireControl.HitProbability(low.Weapon, low.Shooter, low.Target);
            var pHigh = FireControl.HitProbability(high.Weapon, high.Shooter, high.Target);
            Assert.True(pHigh >= pLow - 1e-5f, $"at info {info}: expected higher Resolution (4) to score >= lower Resolution (2), got pHigh={pHigh} pLow={pLow}");
        }

        // Neither design is saturated at info .3 (ceilings are .55 and .325 respectively, per the spec's own
        // worked numbers), so the higher-Resolution engagement must be strictly ahead here.
        low.Shooter.EntityInfoGathered[low.Target] = .3f;
        high.Shooter.EntityInfoGathered[high.Target] = .3f;
        var pLowMid = FireControl.HitProbability(low.Weapon, low.Shooter, low.Target);
        var pHighMid = FireControl.HitProbability(high.Weapon, high.Shooter, high.Target);
        Assert.True(pHighMid > pLowMid, $"expected strictly higher at info .3, got pHigh={pHighMid} pLow={pLowMid}");
    }

    // 6c.2: a galaxy that contains no seller of the one targeting design in the catalog still generates an
    // armed loadout instead of throwing InvalidLoadoutException, and the resulting entity resolves the
    // unaided fallback (Cut 5.1's floor), not a targeting system it was never able to equip.
    // Mutation: restore `required: true` on the targeting-system RandomProduct call in
    // LoadoutGenerator.FillInterior (the shipped Cut 6a behaviour) -- that throws instead of degrading.
    [Fact]
    public void NoSellerDegradesToUnaidedInsteadOfThrowing()
    {
        string Run() => Path.Combine(_root, "run.cc");
        string Player() => Path.Combine(_root, "player.cc");

        CultRecordRef<Faction> maker, targetingMaker;
        using (var seedCache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            seedCache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
            maker = seedCache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
            targetingMaker = seedCache.Upsert(new Faction { Name = "Targeting Maker", ShortName = "TGM" });

            var hullShape = SolidShape(5, 5);
            var hull = seedCache.Upsert(new HullData
            {
                Name = "Armed", HullType = HullType.Ship, Shape = hullShape, Price = 100,
                Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
            });
            var gun = seedCache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1, Behaviors = { new InstantWeaponData() } });
            var cargo = seedCache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = new Shape(), Price = 5 });
            var capacitor = seedCache.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
            var targeting = seedCache.Upsert(new GearData { Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new TargetingSystemData() } });

            // Everything but the targeting system is sold by Maker, who IS in the galaxy. Targeting is sold
            // only by Targeting Maker, who is NOT -- the single point of failure 6c.2 fixes.
            foreach (var (name, design, factionMaker) in new[]
                     {
                         ("Armed by Maker", hull.Key, maker), ("Gun by Maker", gun.Key, maker),
                         ("Crate by Maker", cargo.Key, maker), ("Cap by Maker", capacitor.Key, maker),
                         ("Targeting by Targeting Maker", targeting.Key, targetingMaker)
                     })
                seedCache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = factionMaker });
            seedCache.FlushAsync().Wait();
        }

        // RunSave.Commit needs a run store to write SavedGame/SavedZone into (a catalog-only cache is "home"
        // to neither), the same three-store shape AetheriaStores.Open(Catalog, Run, Player) gives the game
        // in every build (RunSaveTests.Open).
        var cache = AetheriaStores.Open(Catalog, Run(), Player());
        _openCaches.Add(cache);

        // A minimal one-zone galaxy whose faction roster excludes Targeting Maker entirely, the same
        // SavedGame/Galaxy round trip RunSaveTests uses to build a real Galaxy without standing up full
        // procedural generation.
        var game = new SavedGame
        {
            Factions = new[] { maker },
            Relationships = new[] { FactionRelationship.Neutral },
            HomeZones = new Dictionary<int, int> { { 0, 0 } },
            BossZones = new Dictionary<int, int>(),
            DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0],
            Exit = -1
        };
        var zones = new[]
        {
            new SavedZone { Name = "Zone 0", Position = float2(0, 0), AdjacentZones = Array.Empty<int>(), Factions = new[] { 0 }, Owner = 0, Contents = null }
        };
        RunSave.Commit(cache, game, zones, new ProvenanceLedger());
        var galaxy = new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
        Assert.False(galaxy.ContainsFaction(targetingMaker));

        var random = new Random(1u);
        var settings = TestSettings();
        var log = new List<string>();
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, log.Add);
        var generator = new LoadoutGenerator(ref random, items, galaxy, null, null, .5f);

        var pack = generator.GenerateShipLoadout(candidate => candidate.Name == "Armed");

        Assert.NotNull(pack); // must not have thrown InvalidLoadoutException
        Assert.DoesNotContain(pack.Equipment, e => (cache.Get(e.item.Data) as GearData)?.Behaviors.Any(b => b is TargetingSystemData) ?? false);
        Assert.Contains(log, l => l.Contains("targeting", StringComparison.OrdinalIgnoreCase));

        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var entity = EntitySerializer.Unpack(items, zone, pack);
        zone.Entities.Add(entity);
        entity.Activate();
        zone.Update(0f);

        Assert.Null(entity.GetBehavior<TargetingSystem>());
        Assert.Equal(settings.UnaidedAccuracy, FireControl.Accuracy(entity), 4);
        Assert.Equal(settings.UnaidedTracking, FireControl.Tracking(entity), 4);
    }
}
