/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using Random = CultMath.Random;

// Cut 2 (docs/fire-control-cut.md): pins the targeting subsystem, the reveal rule between the two info
// thresholds, the selection slot/predicate, and the required-equipment ruling (Q4). Builds its own
// hull/catalog fixtures, the same style FireControlTests uses for Cut 1, rather than reusing that file's
// or LoadoutTests' private helpers.
public sealed class TargetingSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-targeting-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public TargetingSystemTests() => Directory.CreateDirectory(_root);

    private CultCache _openCache;

    public void Dispose()
    {
        _openCache?.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetGearInfoThreshold = .9f,
        UnaidedAccuracy = .1f,
        FiringArc = 120
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Min=0 (not Min=Max): a PowerSupply term can only be observed to degrade something with room to move
    // between floor and authored ceiling (BrownoutTests' own convention).
    private static PerformanceStat Curved(float max, float exponent) =>
        new PerformanceStat { Min = 0, Max = max, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = exponent } } };

    // A 6x4 hull with one Sensors hardpoint at (0,0) (hardpoint-mounted weapon) and interior room for two
    // more Tool items, one 2-cell and one 1-cell: a hardpoint weapon, a wide interior item and a narrow one.
    // `targetingData`, when given, authors a fourth non-hull item (a Tool gear carrying that behaviour) in
    // the same catalog-build pass, so a test that needs a targeting system never has to reopen the catalog
    // file after the live, already-open ItemManager exists (CultCache is single-writer).
    private (ItemManager items, Ship target, EquippedItem weapon, EquippedItem wideItem, EquippedItem narrowItem, EquippedItem targetingItem)
        BuildTarget(GameplaySettings settings, TargetingSystemData targetingData = null)
    {
        var hullShape = new Shape(6, 4);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hullData = new HullData
        {
            Name = "TestHull", HullType = HullType.Ship, Shape = hullShape, Durability = 1, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        using (var buildCache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            buildCache.Upsert(new VerseGrammar { Revision = 1 });
            buildCache.Upsert(hullData);
            buildCache.Upsert(new GearData
            {
                Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Behaviors = { new InstantWeaponData() }
            });
            var wideShape = new Shape(2, 1);
            wideShape[new int2(0, 0)] = true;
            wideShape[new int2(1, 0)] = true;
            buildCache.Upsert(new GearData
            {
                Name = "Wide", Hardpoint = HardpointType.Tool, Shape = wideShape, Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
            });
            buildCache.Upsert(new GearData
            {
                Name = "Narrow", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
            });
            if (targetingData != null)
                buildCache.Upsert(new GearData
                {
                    Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
                    MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                    Behaviors = { targetingData }
                });
            buildCache.FlushAsync().Wait();
        }

        _openCache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 }; // hull
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = 1 }; // gun
        ledger.Lots[3] = new Lot { Origin = new Attributed(), Quality = 1 }; // wide
        ledger.Lots[4] = new Lot { Origin = new Attributed(), Quality = 1 }; // narrow
        ledger.Lots[5] = new Lot { Origin = new Attributed(), Quality = 1 }; // targeting (unused unless authored above)
        var items = new ItemManager(_openCache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull"));
        var hull = new EquippableItem { Data = hullRef, Durability = 1, Lot = 1 };
        var target = new Ship(items, zone, hull, new EntitySettings());

        var narrowRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Narrow"));
        var narrow = new EquippableItem { Data = narrowRef, Durability = 1, Lot = 4 };
        Assert.True(target.TryFindSpace(narrow, out var narrowPos));
        Assert.True(target.TryEquip(narrow, narrowPos));
        var narrowItem = target.Equipment.Single(e => e.EquippableItem == narrow);

        var wideRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Wide"));
        var wide = new EquippableItem { Data = wideRef, Durability = 1, Lot = 3 };
        Assert.True(target.TryFindSpace(wide, out var widePos));
        Assert.True(target.TryEquip(wide, widePos));
        var wideItem = target.Equipment.Single(e => e.EquippableItem == wide);

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(target.TryEquip(gun, new int2(0, 0)));
        var weapon = target.Equipment.Single(e => e.EquippableItem == gun);

        EquippedItem targetingItem = null;
        if (targetingData != null)
        {
            var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
            var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 5 };
            Assert.True(target.TryFindSpace(targeting, out var targetingPos));
            Assert.True(target.TryEquip(targeting, targetingPos));
            targetingItem = target.Equipment.Single(e => e.EquippableItem == targeting);
        }

        zone.Admit(target, piloted: false);

        return (items, target, weapon, wideItem, narrowItem, targetingItem);
    }

    // A bare-hull observer sharing the target's zone, with no equipment of its own -- only its
    // EntityInfoGathered dictionary and Target/TrySelectTargetItem matter to these tests.
    private Ship BuildObserver(ItemManager items, Zone zone)
    {
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull"));
        var hull = new EquippableItem { Data = hullRef, Durability = 1, Lot = 1 };
        var observer = new Ship(items, zone, hull, new EntitySettings());
        zone.Admit(observer, piloted: false);
        return observer;
    }

    // The selection predicate: TrySelectTargetItem accepts only an item of the current target that the observer's
    // book holds and that is still whole; a Target change nulls the aim point, and destruction drops it on read
    // with no loop clearing it. Mutation: IsRevealed returns true unconditionally, or ignores the book; the
    // Target-change subscription is removed; or ResolvedTargetItem returns AimedAt.Item without re-checking.
    [Fact]
    public void SelectionNeedsReveal()
    {
        var (items, target, weapon, _, _, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.SetTarget(target);

        Assert.False(observer.TrySelectTargetItem(weapon)); // nothing seen yet
        Assert.Null(observer.AimedAt.Item);

        observer.RecordReveal(target, weapon, SignatureChannel.LaunchFlash, weapon.Data);
        Assert.True(observer.TrySelectTargetItem(weapon));
        Assert.Same(weapon, observer.AimedAt.Item);
        Assert.Same(weapon, observer.ResolvedTargetItem);

        // A Target change nulls the aim point.
        observer.SetTarget(TargetRef.None);
        Assert.Null(observer.AimedAt.Item);

        // Destruction: re-select, then destroy the item without touching AimedAt.Item at all -- the raw field
        // still holds the item (no loop cleared it), but the read accessor drops it.
        observer.SetTarget(target);
        Assert.True(observer.TrySelectTargetItem(weapon));
        weapon.EquippableItem.Durability = 0f;
        Assert.Same(weapon, observer.AimedAt.Item);
        Assert.Null(observer.ResolvedTargetItem);
    }

    // Rulings visibility-per-capability-reveal and reveal-ends-on-perception: a target with three items, one seen
    // acting. Only that item is revealed; the others are refused by the same writer. Mutation: IsRevealed true for
    // any fitted item (book ignored).
    [Fact]
    public void GearIsRevealedByWhatItDid()
    {
        var (items, target, weapon, wideItem, narrowItem, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.SetTarget(target);

        observer.RecordReveal(target, weapon, SignatureChannel.LaunchFlash, weapon.Data);

        Assert.True(FireControl.IsRevealed(observer, weapon));
        Assert.False(FireControl.IsRevealed(observer, wideItem));
        Assert.False(FireControl.IsRevealed(observer, narrowItem));
        Assert.True(observer.TrySelectTargetItem(weapon));
        Assert.False(observer.TrySelectTargetItem(wideItem));
        Assert.False(observer.TrySelectTargetItem(narrowItem));
    }

    // Ruling quiet-gear-identifiable-hull-layout-known: watching reveals no gear. An observer whose track is full
    // (it identifies the ship) on a ship that has done nothing can aim at none of its items, though ContactOf
    // reads Identified. Mutation: a track-tier or dwell fallback in IsRevealed.
    [Fact]
    public void WatchingRevealsNoGear()
    {
        var (items, target, weapon, wideItem, narrowItem, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.EntityInfoGathered[target] = 1f;

        Assert.True(observer.ContactOf(target).Identified);
        foreach (var item in new[] { weapon, wideItem, narrowItem })
            Assert.False(FireControl.IsRevealed(observer, item));
        Assert.Empty(observer.RevealsOf(target));
    }

    // A revealed item made IsDestroyed drops out of the aim read: IsRevealed false, ResolvedTargetItem null,
    // TrySelectTargetItem refuses it. Mutation: drop the IsDestroyed filter from IsRevealed.
    [Fact]
    public void ADestroyedItemDropsOut()
    {
        var (items, target, weapon, wideItem, _, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.SetTarget(target);
        observer.RecordReveal(target, weapon, SignatureChannel.LaunchFlash, weapon.Data);
        observer.RecordReveal(target, wideItem, SignatureChannel.LaunchFlash, wideItem.Data);
        Assert.True(observer.TrySelectTargetItem(weapon));

        weapon.EquippableItem.Durability = 0f;

        Assert.False(FireControl.IsRevealed(observer, weapon));
        Assert.Null(observer.ResolvedTargetItem);
        Assert.False(observer.TrySelectTargetItem(weapon));
        Assert.True(FireControl.IsRevealed(observer, wideItem)); // the survivor stays aimable
    }

    // A revealed item that is no longer fitted to its ship cannot be aimed at, though the observer's book still
    // holds it. Mutation: drop the Equipment.Contains check from IsRevealed.
    [Fact]
    public void AnUnfittedItemCannotBeAimedAt()
    {
        var (items, target, weapon, _, _, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.SetTarget(target);
        observer.RecordReveal(target, weapon, SignatureChannel.LaunchFlash, weapon.Data);
        Assert.True(FireControl.IsRevealed(observer, weapon));

        target.Deactivate(); // an active ship refuses to unequip
        Assert.NotNull(target.TryUnequip(weapon));

        Assert.Contains(observer.RevealsOf(target), r => r.Item == weapon);
        Assert.False(FireControl.IsRevealed(observer, weapon));
    }

    // The aim read is live; the book is not. Below the identify threshold the observer perceives nothing of a
    // destruction, so a revealed gun destroyed by a third party stays in RevealsOf (knowledge) yet cannot be aimed
    // at. Mutations: IsRevealed without the live filter fails this; a live filter in RevealsOf fails it too.
    [Fact]
    public void TheAimReadIsLiveTheBookIsNot()
    {
        var (items, target, weapon, _, _, _) = BuildTarget(TestSettings());
        var observer = BuildObserver(items, target.Zone);
        observer.EntityInfoGathered[target] = .5f; // below TargetGearInfoThreshold (.9)
        observer.RecordReveal(target, weapon, SignatureChannel.LaunchFlash, weapon.Data);
        Assert.True(FireControl.IsRevealed(observer, weapon));

        weapon.EquippableItem.Durability = 0f;

        Assert.Contains(observer.RevealsOf(target), r => r.Item == weapon);
        Assert.False(FireControl.IsRevealed(observer, weapon));
    }

    // Q4: the subsystem is what supplies accuracy. No targeting system, a destroyed one, and a disabled one
    // all resolve the authored unaided figures. Mutation: fall back to the (destroyed/offline) system's own
    // stats instead of the unaided ones.
    [Fact]
    public void UnaidedFiresWorse()
    {
        var settings = TestSettings();

        var targetingData = new TargetingSystemData
        {
            Accuracy = Constant(.8f), Resolution = Constant(.6f), Precision = Constant(.7f), Tracking = Constant(.5f)
        };
        var (items, target, _, _, _, targetingUnit) = BuildTarget(settings, targetingData);
        var equippedTargeting = targetingUnit;
        var system = equippedTargeting.Behaviors.OfType<TargetingSystem>().Single();
        system.Execute(0f);

        // No targeting system at all: a second, bare-hull ship in the same catalog/zone (one CultCache
        // handle stays open per test -- BuildTarget is not called twice).
        var bareShip = BuildObserver(items, target.Zone);
        Assert.Equal(settings.UnaidedAccuracy, FireControl.Accuracy(bareShip), 4);
        // Cut 6d (docs/fire-control-cut.md): Precision now falls back to UnaidedPrecision, the same shape as
        // Accuracy/Tracking above -- it used to fall back to a bare 0, which was fine when Precision only fed
        // the old coin-flip's aimed-item branch and meant nothing to a shot with no aim point. Now it derives
        // every shot's sigma, aimed or not, so the unaided floor has to be an authored number, not a bare zero.
        Assert.Equal(settings.UnaidedPrecision, FireControl.Precision(bareShip), 4);
        Assert.Equal(1f, FireControl.Resolution(bareShip), 4);

        // Bring it online to prove the fixture can actually supply its own stats...
        equippedTargeting.UpdatePerformance();
        Assert.True(equippedTargeting.Active.Value);
        Assert.Equal(.8f, FireControl.Accuracy(target), 4);
        Assert.Equal(.7f, FireControl.Precision(target), 4);

        // ...then destroy it: durability gone, still falls back.
        equippedTargeting.EquippableItem.Durability = 0f;
        equippedTargeting.UpdatePerformance();
        Assert.False(equippedTargeting.Active.Value);
        Assert.Equal(settings.UnaidedAccuracy, FireControl.Accuracy(target), 4);
        Assert.Equal(settings.UnaidedPrecision, FireControl.Precision(target), 4);
        Assert.Equal(1f, FireControl.Resolution(target), 4);

        // Repair it, then disable it directly instead: same fallback.
        equippedTargeting.EquippableItem.Durability = 1f;
        equippedTargeting.UpdatePerformance();
        Assert.True(equippedTargeting.Active.Value);
        equippedTargeting.Enabled.Value = false;
        Assert.False(equippedTargeting.Active.Value);
        Assert.Equal(settings.UnaidedAccuracy, FireControl.Accuracy(target), 4);
        Assert.Equal(settings.UnaidedPrecision, FireControl.Precision(target), 4);
    }

    // The brownout ruling (docs/stats-and-power-cut.md) applies to Accuracy same as any other stat: a
    // half-grant halves it (PowerSupply exponent 1), not a switch-off. Reactor + a real PowerBus tick, the
    // same fixture shape BrownoutTests uses, because Item.PowerSupply's setter is internal to a different
    // assembly here and can only be driven through a live grant.
    [Fact]
    public void StarvedTargetingRollsWorse()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ReactorData { Charge = Constant(0), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors =
            {
                new EnergyDrawData { EnergyDraw = Constant(100) },
                new TargetingSystemData { Accuracy = Curved(1, 1), Resolution = Constant(1), Precision = Constant(1), Tracking = Constant(1) }
            }
        });
        cache.FlushAsync().Wait();

        EquippableItem Mint(ItemData design) => new EquippableItem
        {
            Data = cache.RefOf<ItemData>(design),
            Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        };

        Ship BuildShip(float reactorCharge, out ItemManager items)
        {
            items = new ItemManager(cache, new ProvenanceLedger(), TestSettings(), _ => { });
            var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
            var hullData = cache.GetByName<HullData>("Skiff");
            var hull = Mint(hullData);
            hull.Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(hullData), Origin = new Attributed(), Quality = .5f, Roles = new System.Collections.Generic.List<RoleFill>() });
            var ship = new Ship(items, zone, hull, new EntitySettings());

            var reactorData = cache.GetByName<GearData>("Reactor");
            ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);
            var reactor = Mint(reactorData);
            reactor.Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(reactorData), Origin = new Attributed(), Quality = .5f, Roles = new System.Collections.Generic.List<RoleFill>() });
            Assert.True(ship.TryEquip(reactor));

            var targetingData = cache.GetByName<GearData>("Targeting");
            var targeting = Mint(targetingData);
            targeting.Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(targetingData), Origin = new Attributed(), Quality = .5f, Roles = new System.Collections.Generic.List<RoleFill>() });
            Assert.True(ship.TryEquip(targeting));

            zone.Admit(ship, piloted: false);
            ship.Aim = float3(0, 0, 1);
            return ship;
        }

        float ResolveAccuracy(float reactorCharge)
        {
            var ship = BuildShip(reactorCharge, out _);
            ship.Update(1f); // one tick: PowerBus.Step grants, then Execute resolves Accuracy against it
            return FireControl.Accuracy(ship);
        }

        var full = ResolveAccuracy(1000); // demand 100, generation far exceeds it -> ratio 1
        var half = ResolveAccuracy(50);   // demand 100, generation 50 -> ratio .5

        Assert.Equal(1f, full, 3);
        Assert.Equal(.5f, half, 3);
    }

    // Q4: required equipment for anything that can fire. Across several seeds, every generated loadout with
    // a weapon also carries a targeting system. Mutation: drop `required: true` on the targeting-system
    // RandomProduct call in LoadoutGenerator.FillInterior.
    [Fact]
    public void EveryArmedLoadoutGetsATargetingSystem()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var maker = cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });

        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hull = cache.Upsert(new HullData
        {
            Name = "Armed", HullType = HullType.Ship, Shape = hullShape, Price = 100,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        });
        var gun = cache.Upsert(new GearData { Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 1, Behaviors = { new InstantWeaponData() } });
        var cargo = cache.Upsert(new CargoBayData { Name = "Crate", Shape = new Shape(), InteriorShape = new Shape(), Price = 5 });
        var capacitor = cache.Upsert(new GearData { Name = "Cap", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new CapacitorData() } });
        var targeting = cache.Upsert(new GearData { Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new TargetingSystemData() } });
        var tractor = cache.Upsert(new GearData { Name = "Tractor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Price = 1, Behaviors = { new PickupData() } });

        foreach (var (name, design) in new[]
                 {
                     ("Armed by Maker", hull.Key), ("Gun by Maker", gun.Key), ("Crate by Maker", cargo.Key),
                     ("Cap by Maker", capacitor.Key), ("Targeting by Maker", targeting.Key), ("Tractor by Maker", tractor.Key)
                 })
            cache.Upsert(new FactionProductData { Name = name, Design = new CultRecordRef<CraftedItemData>(design), Manufacturer = maker });
        cache.FlushAsync().Wait();

        for (var seed = 1; seed <= 10; seed++)
        {
            var random = new Random((uint) seed);
            var items = new ItemManager(cache, new ProvenanceLedger(), TestSettings(), _ => { });
            var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
            var pack = generator.GenerateShipLoadout(candidate => candidate.Name == "Armed");
            Assert.NotNull(pack);
            Assert.Contains(pack.Equipment, e => (cache.Get(e.item.Data) as GearData)?.Behaviors.Any(b => b is TargetingSystemData) ?? false);
        }
    }
}
