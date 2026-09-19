using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Operator ruling, 2026-09-19 (docs/stats-and-power-cut.md, shield reserve ruling block): supersedes Cut 4's
// derived reserve sizing. ShieldData now authors Capacity, RefillDuration and RestoreDuration directly; the
// shield gains a Broken state a hit it cannot fully cover falls into, rather than being silently refused with
// its charge intact. Two further operator rulings on the fork the first left open: the breaking hit passes
// through in full (no partial absorption -- TakeHit is never called for it), and the reserve empties to zero on
// break so the restore duration means the same thing every time regardless of how full the shield was when it
// broke. This file pins all of that; InputCapacitorTests.cs keeps pinning the surviving Cut 4 rules (a hit
// within the reserve may draw a partial amount; TrySpend/AddCharge atomicity) against the same class. Each rule
// here has a matching mutation in tests/mutation_tests_shield_reserve.py.
public sealed class ShieldReserveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-shieldreserve-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public ShieldReserveTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Same fixture shape as InputCapacitorTests.OpenCatalog: a Reactor whose generation BuildShip sets per test,
    // and a Shield authoring Capacity = 10, RefillDuration = 2 (Rate = 5/s while up), RestoreDuration = 5
    // (Rate = 2/s while broken) -- the two durations deliberately far apart so a test that used the wrong one
    // would be caught well outside rounding noise. EnergyUsage = 1 keeps damage == reserve cost, so test
    // arithmetic reads directly in damage units.
    private CultCache OpenCatalog()
    {
        var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        cache.Upsert(new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = hullShape, Durability = 10, Mass = 1000 });
        cache.Upsert(new GearData
        {
            Name = "Reactor", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ReactorData
            {
                Charge = Constant(0), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2)
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Shield", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ShieldData
            {
                Efficiency = Constant(1), EnergyUsage = Constant(1),
                Capacity = Constant(10), RefillDuration = Constant(2), RestoreDuration = Constant(5)
            } }
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    private (Ship ship, Shield shield) BuildShipWithShield(CultCache cache, float reactorGeneration)
    {
        var ledger = new ProvenanceLedger();
        var items = new ItemManager(cache, ledger, Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorGeneration);
        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));

        var shieldData = cache.GetByName<GearData>("Shield");
        Assert.True(ship.TryEquip(Mint(cache, items, shieldData)));

        zone.Entities.Add(ship);
        ship.Activate();

        return (ship, ship.GetBehavior<Shield>());
    }

    // Rule: a hit within the reserve is absorbed and spends charge. Capacity = 10, RefillDuration = 2 -> Rate =
    // 5/s; a generous reactor (20/s) grants the full request every tick, so a 1s warm-up tick banks 5 charge.
    // A 3-damage hit (cost 3, EnergyUsage = 1) is well inside that -- absorbed -- and spends charge rather than
    // leaving it untouched: a second hit that needs more than the 2 left (2.5) is refused, proving the first
    // hit actually drew the reserve down instead of being a free pass.
    [Fact]
    public void HitWithinReserveIsAbsorbedAndSpendsCharge()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // reserve Rate = 5/s, granted in full -> Charge = 5

        Assert.True(shield.CanTakeHit(DamageType.Kinetic, 3f));
        shield.TakeHit(DamageType.Kinetic, 3f); // Charge: 5 -> 2

        // Proves the hit actually drew the reserve down (not a free absorb that left Charge at 5): a second hit
        // that would have fit inside the original 5 (4, with clear margin either side of the exact 2 left, to
        // stay clear of float rounding at the boundary) no longer fits inside what remains. Per the ruling that
        // any refusal is itself a break, this also puts the shield into Broken.
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 4f));
        Assert.True(shield.Broken);
    }

    // Rule: a hit beyond the reserve breaks the shield.
    [Fact]
    public void HitBeyondReserveBreaksTheShield()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // Charge = 5

        Assert.False(shield.Broken);
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 6f)); // exceeds the 5 held
        Assert.True(shield.Broken);
    }

    // Rule: the breaking hit passes through in full -- no partial absorption. Operator's second ruling on the
    // fork the first left open: TakeHit must never be called for this hit at all (every caller treats
    // CanTakeHit's false branch as "route the whole hit to the hull instead"), so the reserve must not have
    // spent, or banked, any part of the 5 it held. Pinned by timing rather than a single large tick (a big
    // enough single Update always tops up to Capacity regardless of where it started, because RequestedFill
    // caps at Capacity-Charge and hides the difference): if TakeHit had run for the breaking hit, its own
    // TrySpend is atomic and cost (6) exceeds the charge held (5), so TrySpend would refuse and leave that 5
    // charge in place, un-emptied -- restoring measurably faster than a genuinely emptied reserve.
    [Fact]
    public void BreakingHitIsNotPartiallyAbsorbed()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // Charge = 5
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 6f)); // breaks; TakeHit must not run for this hit

        ship.Update(3f); // from zero: 6 of 10 -- not yet full. From a surviving 5 (TakeHit ran, refused, kept it
                          // untouched): 5 + 6 clamped to 10 -- already full.
        Assert.True(shield.Broken);
    }

    // Rule: a broken shield absorbs nothing -- gated on Broken itself, not merely on the reserve happening to
    // read empty (a break always leaves it empty right away, so a hit against a *freshly* broken shield would
    // still fail on reserve arithmetic alone and would not catch a missing Broken check). Restore partway (1s
    // at Rate = 2/s -> 2 of 10) so the reserve genuinely holds enough to cover a small hit on arithmetic alone,
    // and confirm the shield still refuses it while Broken remains true.
    [Fact]
    public void BrokenShieldAbsorbsNothing()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // Charge = 5
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 6f)); // breaks, reserve now 0
        Assert.True(shield.Broken);

        ship.Update(1f); // restoring at 2/s -> 2 of 10, still short of full -- still Broken
        Assert.True(shield.Broken);
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 1f)); // reserve arithmetic alone would allow this
    }

    // Rule: a break empties the reserve to zero -- the restore duration means the same thing every time,
    // regardless of how full the shield was when the breaking hit landed. Pinned by timing, not by a boundary
    // CanTakeHit call (which would short-circuit on Broken alone and not actually distinguish the two
    // hypotheses): break with 5 of 10 charge still in play, at RestoreDuration = 5 (Rate = 2/s). If that
    // leftover 5 survived the break, only (10-5)/2 = 2.5s more would be needed to reach Capacity, so by 3s
    // post-break the shield would already have restored. If the break truly zeroed the reserve, 3s only banks
    // 6 of 10 and the shield is still Broken; a further 2s (5s total, RestoreDuration exactly) is what actually
    // restores it from zero.
    [Fact]
    public void BreakEmptiesTheReserveRegardlessOfChargeHeldAtTheMoment()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // Charge = 5
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 6f)); // breaks with 5 charge in play

        ship.Update(3f); // from zero: 6 of 10 -- not yet full. From a surviving 5: would already be full.
        Assert.True(shield.Broken);

        ship.Update(2f); // total 5s since the break -- exactly RestoreDuration, from zero
        Assert.False(shield.Broken);
    }

    // Rule: a broken shield returns to full on the restore duration, not the refill duration. RefillDuration =
    // 2 (Rate 5/s) is far shorter than RestoreDuration = 5 (Rate 2/s); ticking for exactly RefillDuration's
    // span while broken must NOT be enough to restore (that would mean the refill rate leaked into the broken
    // path), but ticking for the full RestoreDuration must be.
    [Fact]
    public void BrokenShieldRestoresOnRestoreDurationNotRefillDuration()
    {
        using var cache = OpenCatalog();
        var (ship, shield) = BuildShipWithShield(cache, reactorGeneration: 20);
        ship.Update(1f); // Charge = 5
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 6f)); // breaks, reserve now 0

        ship.Update(2f); // RefillDuration's span -- must not be enough to restore at RestoreDuration's rate
        Assert.True(shield.Broken);

        ship.Update(3f); // total 5s since the break -- exactly RestoreDuration
        Assert.False(shield.Broken);
        Assert.True(shield.CanTakeHit(DamageType.Kinetic, 10f)); // and genuinely full, not just "not broken"
    }

    // Rule: a partial grant from the bus stretches both durations proportionally -- the bus still rations
    // everything by one fraction (no tiers yet). A reactor generating half the reserve's Rate * 1 demand grants
    // exactly half every tick, so the reserve should read as having banked half of what a fully-supplied
    // reactor would over the same span. Pinned by comparing two ships built with different reactor generation
    // rather than reading raw charge (Shield exposes no such accessor -- CanTakeHit's own boundary is the
    // observable this suite uses throughout).
    [Fact]
    public void PartialGrantStretchesTheRefillDurationProportionally()
    {
        using var fullCache = OpenCatalog();
        var (fullShip, fullShield) = BuildShipWithShield(fullCache, reactorGeneration: 20); // covers demand (5/s) twice over
        fullShip.Update(1f); // granted in full -> Charge = 5

        using var halfCache = OpenCatalog();
        var (halfShip, halfShield) = BuildShipWithShield(halfCache, reactorGeneration: 2.5f); // half of the 5/s request
        halfShip.Update(1f); // granted half -> Charge = 2.5

        // Check the affordable amount FIRST: any refused CanTakeHit call breaks the shield (the ruling this
        // file pins throughout), which would zero the reserve and make every later call read false regardless
        // of grant. The half-grant reserve affords roughly its half share (2, with margin either side of the
        // exact 2.5 to stay clear of float rounding at the boundary); only then do we spend the one refusal
        // this test needs on a hit big enough that even the full-grant reserve could not have covered it more
        // than four-fold over, to show the two really do differ.
        Assert.True(halfShield.CanTakeHit(DamageType.Kinetic, 2f));
        Assert.True(fullShield.CanTakeHit(DamageType.Kinetic, 4f));
        Assert.False(halfShield.CanTakeHit(DamageType.Kinetic, 4f));
    }
}
