using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 2 (docs/stats-and-power-cut.md): the resolver owns every value. PerformanceStat -- the catalog object --
// used to hold two Dictionary<Entity, ...> fields (the modifier sets) that grew one entry per entity that ever
// evaluated it, for the life of the process (§0.3). This file pins the two structural claims that fix replaces
// them with: one StatResolver per Entity, reachable only from it, and a resolved value that recomputes only when
// a source it declared actually moves.
public sealed class StatResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-statresolver-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private static readonly int2 HardpointCell = new int2(0, 0);
    private static readonly int2 SecondHardpointCell = new int2(1, 0);
    private static readonly int2 FirstGunCell = new int2(2, 0);
    private static readonly int2 SecondGunCell = new int2(0, 1);

    public StatResolverTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private CultCache OpenCatalog(CapacitorData capacitor, StatModifierData modifier = null)
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = SecondHardpointCell, Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = FirstGunCell, Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = SecondGunCell, Shape = new Shape() }
            }
        });
        cache.Upsert(new GearData { Name = "Battery", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { capacitor } });
        if (modifier != null)
            cache.Upsert(new GearData { Name = "Booster", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { modifier } });
        cache.FlushAsync().Wait();
        return cache;
    }

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    // A ship holding one Battery (and, if present, one Booster), activated in a real (non-null) Zone so
    // Entity.Activate runs every IInitializableBehavior.Initialize -- StatModifier's included.
    private Ship BuildActivatedShip(CultCache cache, ItemManager items, bool withBooster)
    {
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hullItem = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hullItem, new EntitySettings());
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Battery")), HardpointCell));
        if (withBooster)
            Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Booster")), SecondHardpointCell));
        zone.Entities.Add(ship);
        ship.Activate();
        return ship;
    }

    // A modifier's StatReference names a behaviour type, and WeaponData.Damage is declared once on the abstract
    // base. A ship carrying an AutoWeapon gun and a ChargedWeapon gun and a Booster whose modifier aims at
    // `target` is the real case; the two tests below read each gun's resolved Damage through EquippedItem.Evaluate.
    private (CultCache Cache, EquippedItem Auto, EquippedItem Charged, PerformanceStat AutoDamage, PerformanceStat ChargedDamage)
        BuildGunShipWithBooster(string target)
    {
        var autoDamage = new PerformanceStat { Min = 10, Max = 10 };
        var chargedDamage = new PerformanceStat { Min = 10, Max = 10 };
        var modifier = new StatModifierData
        {
            Stat = new StatReference { Target = target, Stat = nameof(WeaponData.Damage) },
            Modifier = new PerformanceStat { Min = 3, Max = 3 },
            Type = StatModifierType.Multiplier
        };
        var cache = OpenCatalog(new CapacitorData { Capacity = new PerformanceStat { Min = 1, Max = 1 } }, modifier);
        cache.Upsert(new GearData { Name = "AutoGun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { new AutoWeaponData { Damage = autoDamage } } });
        cache.Upsert(new GearData { Name = "ChargedGun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { new ChargedWeaponData { Damage = chargedDamage } } });
        cache.FlushAsync().Wait();
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var ship = BuildActivatedShip(cache, items, withBooster: true);
        ship.Deactivate(); // TryEquip refuses while deployed
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("AutoGun")), FirstGunCell));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("ChargedGun")), SecondGunCell));
        // Re-activate so the booster's Initialize sees the guns it did not see at the first activation.
        ship.Activate();
        var booster = ship.Equipment.Single(e => e.Data.Name == "Booster");
        var behavior = booster.GetBehavior<StatModifier>();
        behavior.Execute(0f);
        behavior.Update(0f);
        return (cache, ship.Equipment.Single(e => e.Data.Name == "AutoGun"), ship.Equipment.Single(e => e.Data.Name == "ChargedGun"), autoDamage, chargedDamage);
    }

    // A modifier aimed at the abstract base reaches every subtype carried. Mutation: restore exact-type matching
    // (`bd.GetType() == targetType`) in either behaviour filter of StatModifier.TargetsOf and both guns read 10.
    [Fact]
    public void AModifierOnABaseTypeReachesEverySubtype()
    {
        var (cache, auto, charged, autoDamage, chargedDamage) = BuildGunShipWithBooster(nameof(WeaponData));
        using var _ = cache;

        Assert.Equal(30f, auto.Evaluate(autoDamage), 3);
        Assert.Equal(30f, charged.Evaluate(chargedDamage), 3);
    }

    // A modifier aimed at one concrete subtype leaves its sibling alone: assignability is downward only.
    [Fact]
    public void AModifierOnAConcreteTypeReachesOnlyIt()
    {
        var (cache, auto, charged, autoDamage, chargedDamage) = BuildGunShipWithBooster(nameof(AutoWeaponData));
        using var _ = cache;

        Assert.Equal(30f, auto.Evaluate(autoDamage), 3);
        Assert.Equal(10f, charged.Evaluate(chargedDamage), 3);
    }

    // §1.1's authority claim, structurally: "StatResolver, one per Entity, constructed with it." Mutation: make
    // Entity.Resolver a static field -- every entity then shares one resolver, and NotSame goes red.
    [Fact]
    public void EachEntityOwnsItsOwnResolver()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        using var cache = OpenCatalog(new CapacitorData { Capacity = capacity });
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var shipA = BuildActivatedShip(cache, items, withBooster: false);
        var shipB = BuildActivatedShip(cache, items, withBooster: false);

        Assert.NotSame(shipA.Resolver, shipB.Resolver);
    }

    // The consequence of one-resolver-per-entity: a modifier attached on one entity's item never touches another
    // entity's resolved value, even though both equip the very same design (the very same PerformanceStat
    // instance, from the same catalog record). The same static-Resolver mutation above kills this test too, since
    // a shared resolver is the only way two entities could ever collide on an owner-keyed entry in this design.
    [Fact]
    public void AModifierOnOneEntityDoesNotReachAnotherEntitysResolvedValue()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        var modifier = new StatModifierData
        {
            Stat = new StatReference { Target = nameof(CapacitorData), Stat = nameof(CapacitorData.Capacity) },
            Modifier = new PerformanceStat { Min = 5, Max = 5 },
            Type = StatModifierType.Constant
        };
        using var cache = OpenCatalog(new CapacitorData { Capacity = capacity }, modifier);
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

        var boosted = BuildActivatedShip(cache, items, withBooster: true);
        var plain = BuildActivatedShip(cache, items, withBooster: false);

        // Drive the Booster's StatModifier directly (Execute then Update) rather than a full tick, so the test
        // does not also have to bring the item thermally/durability online first.
        var boosterEquipped = boosted.Equipment.Single(e => e.Data.Name == "Booster");
        var modifierBehavior = boosterEquipped.GetBehavior<StatModifier>();
        Assert.NotNull(modifierBehavior);
        modifierBehavior.Execute(0f);
        modifierBehavior.Update(0f);

        var boostedBattery = boosted.Equipment.Single(e => e.Data.Name == "Battery");
        var plainBattery = plain.Equipment.Single(e => e.Data.Name == "Battery");

        Assert.Equal(6f, boostedBattery.Evaluate(capacity), 3); // 1 (Min==Max) + 5 (the Constant modifier)
        Assert.Equal(1f, plainBattery.Evaluate(capacity), 3);   // untouched
    }

    // Soul's Gate 2 finding on Cut 2 (docs/stats-and-power-cut.md): StatModifier.Initialize recomputes _targets
    // by matching the *behaviour type* the modifier targets (here, any equipped item with a CapacitorData
    // behaviour) fresh every time it runs -- but _applied was never reset alongside it. So after the modifier
    // has already applied once, deactivating, unequipping its target, equipping a replacement with the same
    // targeted behaviour, and reactivating used to leave the modifier permanently unattached: Update saw
    // `_executed && _applied` (both still true from the stale attachment) and neither branch of its latch fired,
    // so ApplyModifier never ran against the new target. Mutation: delete the `if (_applied) RemoveModifier();`
    // line this cut adds to Initialize, and the final assertion goes red (the replacement reads 1f, unboosted).
    [Fact]
    public void ARefitTargetReattachesTheModifierAfterReactivation()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        var modifier = new StatModifierData
        {
            Stat = new StatReference { Target = nameof(CapacitorData), Stat = nameof(CapacitorData.Capacity) },
            Modifier = new PerformanceStat { Min = 5, Max = 5 },
            Type = StatModifierType.Constant
        };
        using var cache = OpenCatalog(new CapacitorData { Capacity = capacity }, modifier);
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

        var ship = BuildActivatedShip(cache, items, withBooster: true);
        var boosterEquipped = ship.Equipment.Single(e => e.Data.Name == "Booster");
        var modifierBehavior = boosterEquipped.GetBehavior<StatModifier>();
        Assert.NotNull(modifierBehavior);

        // First activation: applies against the original Battery.
        modifierBehavior.Execute(0f);
        modifierBehavior.Update(0f);
        var originalBattery = ship.Equipment.Single(e => e.Data.Name == "Battery");
        Assert.Equal(6f, originalBattery.Evaluate(capacity), 3);

        // Deactivate, unequip the target, equip a replacement with the same targeted behaviour, reactivate.
        ship.Deactivate();
        Assert.NotNull(ship.TryUnequip(originalBattery));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Battery")), HardpointCell));
        ship.Activate();

        // The modifier behaviour re-executes on the new activation, exactly as it did on the first.
        modifierBehavior.Execute(0f);
        modifierBehavior.Update(0f);

        var replacementBattery = ship.Equipment.Single(e => e.Data.Name == "Battery");
        Assert.Equal(6f, replacementBattery.Evaluate(capacity), 3); // reattached; not stuck unboosted at 1f
    }

    // §0.3's headline claim: the catalog used to keep every entity that ever evaluated one of its stats alive for
    // the life of the process, through PerformanceStat's own per-entity modifier dictionaries. This fails at HEAD
    // before Cut 2. Mutation: make Entity.Resolver static -- its dictionaries then live on a field the catalog's
    // assembly can reach for the life of the process, and the entity (and its EquippedItem) never dies.
    [Fact]
    public void UnequippingAndDroppingAnEntityLeavesItCollectible()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        using var cache = OpenCatalog(new CapacitorData { Capacity = capacity });
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });

        WeakReference EquipEvaluateAndDrop()
        {
            var ship = BuildActivatedShip(cache, items, withBooster: false);
            var battery = ship.Equipment.Single(e => e.Data is GearData);
            battery.Evaluate(capacity); // exactly the call that leaked pre-cut (Entity.cs, EquippedItem.ScaleModifier/ConstantModifier)
            return new WeakReference(ship);
        }

        var weakShip = EquipEvaluateAndDrop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weakShip.IsAlive);
    }

    // Cut 2's Adds: "StatValidation for self-dependency ... cycles." A modifier whose own magnitude stat is one
    // of the stats it writes would resolve one tick behind itself forever -- refused at equip (Entity.Activate),
    // naming the entity and the (Target, Stat) reference. Mutation: neuter the DFS's self-check in
    // StatModifier.ValidateNoCycle and this goes from throwing to succeeding silently.
    [Fact]
    public void AModifierThatTargetsItsOwnMagnitudeStatIsRefusedAtEquip()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        var selfModifier = new StatModifierData
        {
            Stat = new StatReference { Target = nameof(CapacitorData), Stat = nameof(CapacitorData.Capacity) },
            Modifier = capacity, // the modifier's own magnitude IS the stat it targets
            Type = StatModifierType.Constant
        };
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() } }
        });
        cache.Upsert(new GearData
        {
            Name = "Loop", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10,
            Behaviors = { new CapacitorData { Capacity = capacity }, selfModifier }
        });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hullItem = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hullItem, new EntitySettings());
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Loop")), HardpointCell));
        zone.Entities.Add(ship);

        var error = Assert.Throws<InvalidOperationException>(() => ship.Activate());
        Assert.Contains(ship.Name, error.Message);        // the entity, by name ("Skiff", HullData.Name)
        Assert.Contains(nameof(CapacitorData), error.Message);
        Assert.Contains(nameof(CapacitorData.Capacity), error.Message);
    }

    // A small, deliberately fake context so the resolver's own caching rule is provable without any catalog,
    // entity or item plumbing: a stat recomputes when a source it declared moves, and only then.
    private sealed class FakeContext : IStatContext
    {
        public Lot Lot { get; set; }
        public float Heat = 1f;
        public float Durability = 1f;
        // Real contexts (EquippedItem, ConsumableItemEffect) route these through their entity's resolver, keyed
        // by themselves as owner; a resolver-only test needs the same wiring or it can never observe an attached
        // modifier, which is the whole point of AttachingAModifierInvalidatesImmediately below.
        public StatResolver Resolver;
        public object Owner;
        public float HeatFactor(float exponent) => Heat;
        public float DurabilityFactor(float exponent) => Durability;
        public float ConsumableProgressFactor(float exponent) => 1f;
        public float PowerSupplyFactor(float exponent) => 1f;
        public float ScaleModifier(PerformanceStat stat) => Resolver?.ScaleModifier(Owner, stat) ?? 1f;
        public float ConstantModifier(PerformanceStat stat) => Resolver?.ConstantModifier(Owner, stat) ?? 0f;
    }

    [Fact]
    public void ResolveDoesNotRecomputeWhenNoDeclaredSourceMoved()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new FakeContext { Heat = 1f };
        var stat = new PerformanceStat { Min = 0, Max = 10, Terms = { new StatTerm { Source = StatSource.Heat, Exponent = 1 } } };

        Assert.Equal(10f, resolver.Resolve(owner, stat, context), 3);
        context.Heat = 0f; // the live context changed, but nothing told the resolver a source moved
        Assert.Equal(10f, resolver.Resolve(owner, stat, context), 3); // still the stale, cached value
    }

    [Fact]
    public void ResolveRecomputesOnlyAfterItsDeclaredSourceIsInvalidated()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new FakeContext { Heat = 1f };
        var stat = new PerformanceStat { Min = 0, Max = 10, Terms = { new StatTerm { Source = StatSource.Heat, Exponent = 1 } } };

        Assert.Equal(10f, resolver.Resolve(owner, stat, context), 3);
        context.Heat = 0f;
        resolver.InvalidateSource(owner, StatSource.Heat);
        Assert.Equal(0f, resolver.Resolve(owner, stat, context), 3);
    }

    // Invalidating an undeclared source must not touch a stat that never read it -- otherwise every stat would
    // recompute on every tick regardless of its own Terms, which is exactly the "recomputes only when its own
    // source moves" rule this cut adds.
    [Fact]
    public void InvalidatingAnUnrelatedSourceDoesNotRecomputeAStatThatDidNotDeclareIt()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new FakeContext { Durability = 1f };
        var stat = new PerformanceStat { Min = 0, Max = 10, Terms = { new StatTerm { Source = StatSource.Durability, Exponent = 1 } } };

        Assert.Equal(10f, resolver.Resolve(owner, stat, context), 3);
        context.Durability = 0f;
        resolver.InvalidateSource(owner, StatSource.Heat); // a different source moving
        Assert.Equal(10f, resolver.Resolve(owner, stat, context), 3); // Durability's own generation never bumped
    }

    // Attaching or detaching a modifier invalidates immediately, independent of the source-generation scheme --
    // a modifier is not one of the stat's declared Terms.
    [Fact]
    public void AttachingAModifierInvalidatesImmediately()
    {
        var resolver = new StatResolver();
        var owner = new object();
        var context = new FakeContext { Resolver = resolver, Owner = owner };
        var stat = new PerformanceStat { Min = 1, Max = 1 };

        Assert.Equal(1f, resolver.Resolve(owner, stat, context), 3);
        resolver.AttachModifier(owner, stat, "modifier-a", StatModifierType.Constant, 4f);
        Assert.Equal(5f, resolver.Resolve(owner, stat, context), 3);
        resolver.DetachModifier(owner, stat, "modifier-a");
        Assert.Equal(1f, resolver.Resolve(owner, stat, context), 3);
    }

    // Soul's Gate 1 finding on Cut 2 (docs/stats-and-power-cut.md §0b: a resolver entry is "destroyed at
    // unequip / entity teardown"): nothing ever removed an owner from _cache/_modifiers/_generations, so an
    // EquippedItem that was unequipped, evaluated, and replaced kept growing the resolver's retained set forever.
    // This pins the fix at the retained-set level, not only via a WeakReference on one entity (which the existing
    // UnequippingAndDroppingAnEntityLeavesItCollectible already covers): repeatedly equip, evaluate (populating
    // _cache and, through UpdatePerformance, _generations), and unequip, then assert the resolver's own entry
    // counts return to the same baseline they started at, cycle after cycle. Mutation: delete the
    // Resolver.Forget(item) call in Entity.TryUnequip and this goes red because the counts climb instead.
    [Fact]
    public void UnequipReturnsTheResolversRetainedSetToBaseline()
    {
        var capacity = new PerformanceStat { Min = 1, Max = 1 };
        using var cache = OpenCatalog(new CapacitorData { Capacity = capacity });
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var ship = BuildActivatedShip(cache, items, withBooster: false);
        var resolver = ship.Resolver;

        var baselineGenerations = resolver.GenerationOwnerCount;
        var baselineCache = resolver.CacheEntryCount;
        var baselineModifiers = resolver.ModifierEntryCount;

        for (var i = 0; i < 20; i++)
        {
            var battery = ship.Equipment.Single(e => e.Data.Name == "Battery");
            battery.UpdatePerformance(); // Cut 2: bumps this owner's Heat and Durability generations
            battery.Evaluate(capacity); // populates a _cache entry keyed by (battery, capacity)

            ship.Deactivate();
            Assert.NotNull(ship.TryUnequip(battery));
            Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Battery")), HardpointCell));
            ship.Activate();
        }

        Assert.Equal(baselineGenerations, resolver.GenerationOwnerCount);
        Assert.Equal(baselineCache, resolver.CacheEntryCount);
        Assert.Equal(baselineModifiers, resolver.ModifierEntryCount);
    }

    // The other half of Gate 1: a ConsumableItemEffect that expires drops out of Entity._activeConsumables
    // (Entity.Update) without ever telling the resolver, so 20 activate/evaluate/expire cycles left 22 generation
    // owners at HEAD (this ship's own EquippedItem owners plus one per expired consumable instance). Mutation:
    // delete the Resolver.Forget(_activeConsumables[i]) call at expiry and this goes red.
    [Fact]
    public void ConsumableExpiryReturnsTheResolversRetainedSetToBaseline()
    {
        using var cache = OpenCatalog(new CapacitorData { Capacity = new PerformanceStat { Min = 1, Max = 1 } });
        var boosterRef = cache.Upsert(new ConsumableItemData { Name = "Stim", Duration = 0.01f });
        cache.FlushAsync().Wait();
        var boosterData = cache.Get(boosterRef);

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var ship = BuildActivatedShip(cache, items, withBooster: false);
        var resolver = ship.Resolver;

        // A warm-up tick with no consumable active: the still-equipped Battery owns a permanent Heat/Durability
        // generation entry from here on (correctly -- it is never unequipped in this test), so the baseline this
        // test pins against must be taken after that entry exists, not before it.
        ship.Update(0f);
        var baselineGenerations = resolver.GenerationOwnerCount;
        var baselineCache = resolver.CacheEntryCount;
        var baselineModifiers = resolver.ModifierEntryCount;

        for (var i = 0; i < 20; i++)
        {
            var lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(boosterData), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() });
            var consumableItem = Assert.IsType<ConsumableItem>(items.CreateInstance(lot));
            ship.ActivateConsumable(consumableItem);

            // Duration is 0.01s; one 1-second tick expires it and drives Entity.Update's expiry branch.
            ship.Update(1f);
        }

        Assert.Equal(baselineGenerations, resolver.GenerationOwnerCount);
        Assert.Equal(baselineCache, resolver.CacheEntryCount);
        Assert.Equal(baselineModifiers, resolver.ModifierEntryCount);
    }

    // Ruling boost-stacking-multiply: every multiplier on one stat multiplies, with no stacking penalty, and every
    // constant adds. The rig is the production path end to end: gear carrying StatModifierData is equipped on a ship
    // beside a thruster, a consumable is minted through ItemManager.CreateInstance and activated on the ship, and
    // the resolved value is read through EquippedItem.Evaluate. Nothing here calls AttachModifier by hand.
    private sealed class ThrustRig
    {
        public CultCache Cache;
        public ItemManager Items;
        public Ship Ship;
        public EquippedItem Engine;
        public PerformanceStat Thrust;
        public Func<ConsumableItem> Consumable; // null when the rig was built without one

        public float Resolved => Engine.Evaluate(Thrust);
        // Two ticks: the first executes every modifier, the second applies it (StatModifier.Update).
        public void Tick(float dt) { for (var i = 0; i < 2; i++) Ship.Update(dt); }
    }

    private static StatModifierData ThrustModifier(StatModifierType type, float value) => new StatModifierData
    {
        Stat = new StatReference { Target = nameof(ThrusterData), Stat = nameof(ThrusterData.Thrust) },
        Modifier = new PerformanceStat { Min = value, Max = value },
        Type = type
    };

    // A Thrust-10 thruster and one gear per entry of `gear`, in that attach order, plus (optionally) a consumable
    // whose single modifier is `consumable`. The hull has four hardpoints, so at most three gear modifiers.
    private ThrustRig BuildThrustRig((StatModifierType Type, float Value)[] gear, (StatModifierType Type, float Value)? consumable = null)
    {
        var thrust = new PerformanceStat { Min = 10, Max = 10 };
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var cells = new[] { HardpointCell, SecondHardpointCell, FirstGunCell, SecondGunCell };
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints = cells.Select(c => new HardpointData { Type = HardpointType.Sensors, Position = c, Shape = new Shape() }).ToList()
        });
        cache.Upsert(new GearData { Name = "Engine", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { new ThrusterData { Thrust = thrust } } });
        for (var i = 0; i < gear.Length; i++)
            cache.Upsert(new GearData { Name = "Boost" + i, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 10, Behaviors = { ThrustModifier(gear[i].Type, gear[i].Value) } });
        if (consumable != null)
            cache.Upsert(new ConsumableItemData { Name = "Stim", Duration = 1f, Shape = new Shape(), Behaviors = { ThrustModifier(consumable.Value.Type, consumable.Value.Value) } });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var ship = new Ship(items, zone, Mint(cache, items, cache.GetByName<HullData>("Skiff")), new EntitySettings());
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Engine")), cells[0]));
        for (var i = 0; i < gear.Length; i++)
        {
            // A test gear has no authored heat response, so its thermal performance reads 0 and it would sit offline;
            // the override is the entity's own switch for keeping a part running, not a stand-in for the rule.
            var boost = Mint(cache, items, cache.GetByName<GearData>("Boost" + i));
            boost.OverrideShutdown = true;
            Assert.True(ship.TryEquip(boost, cells[i + 1]));
        }
        ship.OverrideShutdown = true;
        zone.Entities.Add(ship);
        ship.Activate();

        Func<ConsumableItem> mint = null;
        if (consumable != null)
        {
            var design = cache.GetByName<ConsumableItemData>("Stim");
            mint = () => Assert.IsType<ConsumableItem>(items.CreateInstance(items.Lots.Add(
                new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })));
        }
        return new ThrustRig { Cache = cache, Items = items, Ship = ship, Engine = ship.Equipment.Single(e => e.Data.Name == "Engine"), Thrust = thrust, Consumable = mint };
    }

    // Two scales multiply: x1.2 and x1.25 on one stat resolve to base x1.5, and x1.1, x1.2, x1.25 to base x1.65,
    // whatever order they attach in. Mutations at StatResolver.ScaleModifier: a sum of excesses reads 14.5 and
    // 15.5, the strongest-only rule 12.5, a diminishing curve 14.67, and `result *= value * value` or
    // `result *= value + 0.01f` (functions of the scale itself) read off 15.
    [Theory]
    [InlineData(new[] { 1.2f, 1.25f }, 15f)]
    [InlineData(new[] { 1.25f, 1.2f }, 15f)]
    [InlineData(new[] { 1.1f, 1.2f, 1.25f }, 16.5f)]
    [InlineData(new[] { 1.25f, 1.1f, 1.2f }, 16.5f)]
    [InlineData(new[] { 1.2f, 1.25f, 1.1f }, 16.5f)]
    public void TwoScalesOnOneStatMultiply(float[] scales, float expected)
    {
        var rig = BuildThrustRig(scales.Select(s => (StatModifierType.Multiplier, s)).ToArray());
        using var _ = rig.Cache;
        Assert.Equal(10f, rig.Resolved, 3); // modifiers not yet applied
        rig.Tick(.1f);
        Assert.Equal(expected, rig.Resolved, 3);
    }

    // A gear modifier and a consumable modifier on one drive's Thrust share one entry and multiply: x1.2 gear and
    // x1.5 consumable read x1.8, and when the consumable expires only the gear's x1.2 remains.
    [Fact]
    public void AConsumableBoostMultipliesWithGear()
    {
        var rig = BuildThrustRig(new[] { (StatModifierType.Multiplier, 1.2f) }, (StatModifierType.Multiplier, 1.5f));
        using var _ = rig.Cache;
        rig.Tick(.1f);
        Assert.Equal(12f, rig.Resolved, 3); // gear alone

        rig.Ship.ActivateConsumable(rig.Consumable());
        rig.Tick(.1f); // 0.8s of the 1s remain
        Assert.Equal(18f, rig.Resolved, 3);

        rig.Tick(.5f); // the consumable's duration has run out
        Assert.Equal(12f, rig.Resolved, 3);
    }

    // Constants add, and compose with scales through PerformanceStat.Evaluate: (base x scale) + constant. +3 and +4
    // resolve as a constant of 7; with a x2 scale beside them the value is Evaluate's for scale 2 and constant 7,
    // (10 x 2) + 7. Mutations at ConstantModifier: `result = Math.Max(result, value)` reads constant 4;
    // `result += value * 2` reads constant 14.
    [Theory]
    [InlineData(new[] { 3f, 4f }, new float[0], 1f, 7f, 17f)]
    [InlineData(new[] { 3f, 4f }, new[] { 2f }, 2f, 7f, 27f)]
    public void TwoConstantsOnOneStatAdd(float[] constants, float[] scales, float expectedScale, float expectedConstant, float expected)
    {
        var rig = BuildThrustRig(constants.Select(c => (StatModifierType.Constant, c))
            .Concat(scales.Select(s => (StatModifierType.Multiplier, s))).ToArray());
        using var _ = rig.Cache;
        rig.Tick(.1f);
        Assert.Equal(expectedScale, rig.Ship.Resolver.ScaleModifier(rig.Engine, rig.Thrust), 3);
        Assert.Equal(expectedConstant, rig.Ship.Resolver.ConstantModifier(rig.Engine, rig.Thrust), 3);
        Assert.Equal(expected, rig.Resolved, 3);
    }
}
