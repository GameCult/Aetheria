using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 4 (docs/stats-and-power-cut.md, Cut 4): closes Cut 3's named, temporary exception for the four instant
// draws (a burst, a shot, a ping, a hit taken) -- each now spends from its own InputCapacitor, fed continuously
// by PowerBus like every other consumer, instead of the entity's shared bus capacitors directly. This file pins
// Cut 4's own verification bullets ("no item ever receives a fraction of a shot"; halving the grant halves the
// sustained rate of fire without changing damage per shot) plus the atomicity and half-charge-under-brownout
// rules the map's per-file notes require. Each rule has a matching mutation in
// tests/mutation_tests_stats_power_cut4.py.
public sealed class InputCapacitorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-inputcap-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public InputCapacitorTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Same fixture shape as PowerBusTests.OpenCatalog (see its comment for why): a 3x3 interior hull, wide heat
    // bounds so ThermalOnline never has to be exercised, and a Reactor whose Charge this method leaves at 0 --
    // BuildShip below sets the actual generation per test.
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
        // BurstCount and Cooldown must be authored: InstantWeapon.UpdateStats divides Damage/Heat/Energy by
        // (int)BurstCount and Execute divides dt by Cooldown, so the catalog's own zero-valued PerformanceStat
        // default is not a usable fixture value here (unlike Reactor/Capacitor above, which tolerate it).
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new InstantWeaponData
            {
                Count = Constant(1), Cooldown = Constant(1), Energy = Constant(10), MagazineSize = 0
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Shield", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            // Capacity/RefillDuration/RestoreDuration authored explicitly (shield reserve ruling, 2026-09-19):
            // ShieldData no longer derives its reserve size from EnergyUsage, so a fixture that only authors
            // Efficiency/EnergyUsage now gets a zero-capacity reserve that breaks on every hit. Capacity = 1
            // reproduces this fixture's pre-ruling reserve size exactly (EnergyUsage was 1); durations are
            // unused by the tests below (they never provoke a break here) but must be nonzero so RefreshReserve
            // never divides by a zero duration.
            Behaviors = { new ShieldData { Efficiency = Constant(1), EnergyUsage = Constant(1), Capacity = Constant(1), RefillDuration = Constant(1), RestoreDuration = Constant(1) } }
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

    private (Ship ship, ItemManager items, Reactor reactor) BuildShip(CultCache cache, float reactorCharge)
    {
        var ledger = new ProvenanceLedger();
        var items = new ItemManager(cache, ledger, Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);

        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));

        zone.Entities.Add(ship);
        ship.Activate();

        return (ship, items, ship.GetBehavior<Reactor>());
    }

    private InstantWeapon EquipGun(CultCache cache, Ship ship)
    {
        var gunData = cache.GetByName<GearData>("Gun");
        ship.Deactivate();
        Assert.True(ship.TryEquip(Mint(cache, ship.ItemManager, gunData)));
        ship.Activate();
        return ship.GetBehavior<InstantWeapon>();
    }

    private Shield EquipShield(CultCache cache, Ship ship)
    {
        var shieldData = cache.GetByName<GearData>("Shield");
        ship.Deactivate();
        Assert.True(ship.TryEquip(Mint(cache, ship.ItemManager, shieldData)));
        ship.Activate();
        return ship.GetBehavior<Shield>();
    }

    // Cut 4's own verification bullet: "a weapon whose input capacitor is partly filled does not fire a
    // partial shot." Gun costs 10 energy/shot with a 1s cooldown (Rate = 10/s); a 5-generation reactor fills
    // the capacitor to exactly half (5) after one second -- not enough to fire, and not a rejected-but-costly
    // attempt either: ammo/heat/cooldown state must be completely untouched.
    [Fact]
    public void WeaponDoesNotFireOnAPartialCharge()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 5);
        var gun = EquipGun(cache, ship);
        var fired = false;
        gun.OnFire += _ => fired = true;

        // Trigger() reads BurstCount/Cooldown off cached properties that only exist after UpdateStats has run
        // once (inside Execute) -- a zero-dt warm-up tick populates them without spending any time or charge,
        // the same role PowerBusTests.BuildShip's own warm-up tick plays for Capacitor.Capacity.
        ship.Update(0f);

        // Arm the burst BEFORE the tick that charges the capacitor: InstantWeapon.Execute adds this tick's
        // grant and then runs its burst loop in the same call, so a single dt=1 tick both fills the capacitor
        // to exactly half (5 of 10) and attempts the shot against that same half charge -- proving the failure
        // is read from the charge actually banked this tick, not a stale or optimistic value.
        gun.Activate();
        ship.Update(1f); // capacitor request = 10, granted 5 -> half charge, then the burst loop's own attempt

        Assert.False(fired);
    }

    // The other half of the same rule: once the reactor can cover a full second's worth (10 generation), the
    // capacitor reaches exactly Capacity in the same tick the burst loop runs, and the shot goes through.
    [Fact]
    public void WeaponFiresOnceFullyCharged()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 10);
        var gun = EquipGun(cache, ship);
        var fired = false;
        gun.OnFire += _ => fired = true;

        ship.Update(0f); // warm-up: populates BurstCount/Cooldown before Trigger() reads them
        gun.Activate();
        ship.Update(1f); // capacitor request = 10, granted 10 -> full charge, then the burst loop fires

        Assert.True(fired);
    }

    // Cut 4's other verification bullet: "halving the grant halves the refill rate ... without changing damage
    // per shot." Pinned directly on InputCapacitor -- the mechanism PowerBus and every owning behaviour's
    // Execute drive identically (RequestedFill(dt) for the request, AddCharge(request * PowerSupply) for the
    // fill) -- rather than on a full ship simulation, whose demand naturally shrinks as a capacitor tops up and
    // so does not hold a constant grant fraction across ticks.
    [Fact]
    public void HalvingTheGrantHalvesTheChargeBankedWithoutChangingCapacity()
    {
        var full = new InputCapacitor();
        full.UpdateStats(energy: 10f, cooldown: 1f); // Capacity = 10, Rate = 10/s
        var half = new InputCapacitor();
        half.UpdateStats(energy: 10f, cooldown: 1f);

        var fullRequest = full.RequestedFill(.1f);
        var halfRequest = half.RequestedFill(.1f);
        Assert.Equal(fullRequest, halfRequest, 5); // identical ask -- only the grant differs

        full.AddCharge(fullRequest * 1f);  // PowerSupply == 1: full grant
        half.AddCharge(halfRequest * .5f); // PowerSupply == .5: half grant

        Assert.Equal(2f * half.Charge, full.Charge, 4); // half the grant banks exactly half the charge
        Assert.Equal(10f, full.Capacity, 3);
        Assert.Equal(10f, half.Capacity, 3); // per-shot cost (Capacity) is unaffected by the grant
    }

    // A half-charged item's own reserve neither drains nor advances on its own when the bus grants nothing
    // (Item.PowerSupply == 0, e.g. under total brownout): AddCharge(0) must be a true no-op, not a slow leak or
    // a forced top-up. Exercised directly on InputCapacitor -- the unit under test PowerBus.Step and every
    // owning behaviour's Execute both call into.
    [Fact]
    public void HalfChargedCapacitorHoldsItsChargeWhenNothingIsGranted()
    {
        var capacitor = new InputCapacitor();
        capacitor.UpdateStats(energy: 10f, cooldown: 1f);
        capacitor.AddCharge(5f);
        Assert.Equal(5f, capacitor.Charge, 3);

        capacitor.AddCharge(0f); // the bus granted nothing this tick
        Assert.Equal(5f, capacitor.Charge, 3);
        Assert.False(capacitor.TrySpend(capacitor.Capacity)); // still can't fire on half charge
        Assert.Equal(5f, capacitor.Charge, 3); // and the refused attempt did not touch it
    }

    // AddCharge must clamp to Capacity -- the bus over-filling by a rounding error, or a Capacity that shrank
    // under a stat change after charge had already accumulated, must never leave Charge reporting more energy
    // than a single activation could ever need.
    [Fact]
    public void AddChargeNeverExceedsCapacity()
    {
        var capacitor = new InputCapacitor();
        capacitor.UpdateStats(energy: 10f, cooldown: 1f);

        capacitor.AddCharge(999f);

        Assert.Equal(10f, capacitor.Charge, 3);
    }

    // InputCapacitor.TrySpend must be atomic -- Entity.TrySpendCapacitorCharge's old contract, now scoped to
    // one behaviour's own buffer: either the cost is covered in full, or Charge is untouched.
    [Fact]
    public void TrySpendMovesNothingWhenTheCostCannotBeFullyCovered()
    {
        var capacitor = new InputCapacitor();
        capacitor.UpdateStats(energy: 10f, cooldown: 1f);
        capacitor.AddCharge(6f);

        Assert.False(capacitor.TrySpend(10f));
        Assert.Equal(6f, capacitor.Charge, 3);

        Assert.True(capacitor.TrySpend(6f));
        Assert.Equal(0f, capacitor.Charge, 3);
    }

    // Q4 (operator ruling): capacity/rate derive from Energy and Cooldown by default, but an authored override
    // replaces the rate -- ChargedWeapon's own mechanism (RateOverride => ChargeEnergy). Pinned directly on
    // InputCapacitor: a nonzero rateOverride wins over the Capacity/cooldown default.
    [Fact]
    public void RateOverrideReplacesTheDerivedDefault()
    {
        var derived = new InputCapacitor();
        derived.UpdateStats(energy: 10f, cooldown: 2f); // default rate = 10/2 = 5
        Assert.Equal(5f, derived.Rate, 3);

        var overridden = new InputCapacitor();
        overridden.UpdateStats(energy: 10f, cooldown: 2f, rateOverride: 100f);
        Assert.Equal(100f, overridden.Rate, 3);
    }

    // Shield is the map's named "odd one out": a hit is not a chosen activation, so its reserve is a continuous
    // one, not an activation buffer -- CanTakeHit/TakeHit may draw an arbitrary fraction of the reserve (never
    // "must be exactly Capacity" the way a weapon's shot must). But the underlying spend is still atomic: a hit
    // costing more than the whole reserve holds is refused entirely, not partially absorbed.
    [Fact]
    public void ShieldReserveAllowsAPartialDrawButRefusesAHitItCannotFullyCover()
    {
        using var cache = OpenCatalog();
        var (ship, _, _) = BuildShip(cache, reactorCharge: 10);
        var shield = EquipShield(cache, ship);

        ship.Update(1f); // EnergyUsage = 1 -> reserve Capacity = Rate = 1, granted in full -> Charge = 1

        // A small hit (cost 0.5, well under the 1-unit reserve) is a legitimate partial draw, unlike a weapon
        // shot, which is only ever spent whole.
        Assert.True(shield.CanTakeHit(DamageType.Kinetic, .5f));
        shield.TakeHit(DamageType.Kinetic, .5f);

        // With .5 left, a smaller hit (0.3) is still absorbed -- this is the assertion that actually
        // distinguishes "may draw a partial amount" from "requires the reserve to be at full Capacity", the
        // rule the map draws between Shield and the weapon/sensor buffers above.
        Assert.True(shield.CanTakeHit(DamageType.Kinetic, .3f));

        // A hit needing more than what's left (0.5 remains, hit costs 1) is refused outright, not partially
        // absorbed.
        Assert.False(shield.CanTakeHit(DamageType.Kinetic, 1f));
    }
}
