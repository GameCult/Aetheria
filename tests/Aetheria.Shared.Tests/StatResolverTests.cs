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
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(3, 3);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10,
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Sensors, Position = HardpointCell, Shape = new Shape() },
                new HardpointData { Type = HardpointType.Sensors, Position = SecondHardpointCell, Shape = new Shape() }
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
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
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
}
