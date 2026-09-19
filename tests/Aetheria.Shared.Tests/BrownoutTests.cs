using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// Cut 7 (docs/stats-and-power-target.md): "Continuous consumers brown out through a power supply curve on
// their performance stats. An exponent is enough." Cut 6 wired the curve itself (PowerCurveTests.cs) and the
// bus already writes a real fractional grant (PowerBusTests.cs, PowerTierTests.cs), but every continuous
// consumer still gated its own effect on Item.PowerSupply >= 1f (Thruster.Execute, AetherDrive.Execute,
// Radiator.Execute, ConstantWeapon.Execute, EnergyDraw.Execute) -- so a partial grant read exactly like none,
// the flicking-off bug the ruling names. This file pins each behaviour's own gate removal: a partial grant now
// produces a reduced effect, a full grant is unchanged, a zero grant still produces nothing, the degradation
// follows the authored exponent (not accidentally linear), and an instant item (InputCapacitor's all-or-nothing
// rule, Cut 4) is untouched by any of it. Each rule has a matching mutation in
// tests/mutation_tests_stats_power_cut7.py.
public sealed class BrownoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-brownout-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public BrownoutTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Min=0 (not Min=Max like Constant) is the point: a term can only ever be observed to degrade something
    // when the stat actually has room to move between its floor and its authored ceiling.
    private static PerformanceStat Curved(float max, float exponent) =>
        new PerformanceStat { Min = 0, Max = max, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = exponent } } };

    // A flat Bezier (two keys, equal value, zero tangents): AetherDrive's TorqueProfile multiplies potential
    // torque by this curve's value at the current rpm ratio -- flat at 1 keeps that multiplier out of the way of
    // the PowerSupply curve under test.
    private static BezierCurve FlatCurve(float value) => new BezierCurve
    {
        Keys = new[] { new float4(0, value, 0, 0), new float4(1, value, 0, 0) }
    };

    private static EquippableItem Mint(CultCache cache, ItemManager items, ItemData design) => new EquippableItem
    {
        Data = cache.RefOf<ItemData>(design),
        Durability = design is EquippableItemData equippable ? equippable.Durability : 0,
        Lot = items.Lots.Add(new Lot { Design = cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>() })
    };

    // PowerBusTests/PowerCurveTests' own fixture: a 5x5 hull, a Reactor with no capacitor on the ship (so this
    // tick's grant ratio is a clean min(1, generation/demand), nothing smoothed by stored charge), and one named
    // consumer behaviour under test.
    private CultCache OpenCatalog(BehaviorData consumer)
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
            Name = "Consumer", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { consumer }
        });
        cache.FlushAsync().Wait();
        return cache;
    }

    private Ship BuildShip(CultCache cache, float reactorCharge)
    {
        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
        var hull = Mint(cache, items, cache.GetByName<HullData>("Skiff"));
        var ship = new Ship(items, zone, hull, new EntitySettings());

        var reactorData = cache.GetByName<GearData>("Reactor");
        ((ReactorData) reactorData.Behaviors[0]).Charge = Constant(reactorCharge);
        Assert.True(ship.TryEquip(Mint(cache, items, reactorData)));
        Assert.True(ship.TryEquip(Mint(cache, items, cache.GetByName<GearData>("Consumer"))));

        zone.Entities.Add(ship);
        // A ship already "looking" the way Direction already points keeps Ship.Update's own steering pass a
        // no-op (deltaRot == 0): no NaN from an unset LookDirection propagating into AetherDrive's Axis.z, and
        // no incidental torque thrust from the ship trying to turn toward a look direction none of these tests
        // care about.
        ship.LookDirection = float3(0, 0, 1);
        ship.Activate();
        return ship;
    }

    // --- Thruster: a reverse thruster (default EquippableItem.Rotation == None) driven by MovementDirection.y
    // --- (Ship.cs's own axis pass), producing a measurable, unidirectional velocity change with no rotation
    // --- component to disentangle (Velocity is updated from the pre-rotation Direction within the same tick). ---
    private (float velocityDelta, Thruster thruster) RunThruster(float reactorCharge, float exponent = 1)
    {
        using var cache = OpenCatalog(new ThrusterData
        {
            Thrust = Curved(100, exponent), Visibility = Constant(0), Heat = Constant(0), EnergyUsage = Constant(100)
        });
        var ship = BuildShip(cache, reactorCharge);
        ship.MovementDirection = float2(0, -1); // reverse-thruster axis += -MovementDirection.y == 1
        ship.Update(1f);
        return (-ship.Velocity.y, ship.GetBehavior<Thruster>());
    }

    [Fact]
    public void ThrusterAtHalfGrantProducesReducedNotZeroNotFullThrust()
    {
        var (full, _) = RunThruster(reactorCharge: 1000); // demand 100, generation far exceeds it -> ratio 1
        var (half, _) = RunThruster(reactorCharge: 50);   // demand 100, generation 50 -> ratio .5

        Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
    }

    [Fact]
    public void ThrusterAtFullGrantIsUnchangedFromToday()
    {
        var (full, thruster) = RunThruster(reactorCharge: 1000);
        Assert.Equal(1f, thruster.Item.PowerSupply, 3);
        Assert.Equal(.1f, full, 3); // 100 thrust / 1000 mass * dt 1, exactly as it read before this cut
    }

    [Fact]
    public void ThrusterAtZeroGrantProducesNothing()
    {
        var (zero, thruster) = RunThruster(reactorCharge: 0);
        Assert.Equal(0f, thruster.Item.PowerSupply, 3);
        Assert.Equal(0f, zero, 3);
    }

    // --- The degradation must follow the authored exponent, not the linear response a careless "just multiply
    // --- by PowerSupply" implementation (or a mutation dropping the exponent) would produce. Mirrors
    // --- PowerCurveTests' own exponent-2 bullet, but through a real gated behaviour instead of an inert stat. ---
    [Fact]
    public void ThrusterAtHalfGrantDegradesByItsAuthoredExponentNotLinearly()
    {
        var (half, _) = RunThruster(reactorCharge: 50, exponent: 2);
        // Thrust = lerp(0, 100, pow(.5, 2)) = 25; velocity delta = 25 / 1000 mass * dt 1 = .025, not the
        // linear-response .05 a dropped exponent would produce.
        Assert.Equal(.025f, half, 3);
    }

    // --- AetherDrive: PassiveCoupling = 1 engages the rotor regardless of axis input, so the drive can be
    // --- exercised without threading Ship's own movement/torque classification -- Axis stays at Ship.Update's
    // --- own default (0,0,deltaRot), and deltaRot is 0 by BuildShip's own LookDirection setup. Rpm starts at 0,
    // --- so decay(0, ...) contributes nothing and the whole tick's Rpm gain is exactly this behaviour's own
    // --- spin-up under test. ---
    private (float rpmGain, AetherDrive drive) RunAetherDrive(float reactorCharge, float exponent = 1)
    {
        using var cache = OpenCatalog(new AetherDriveData
        {
            RotorDiameter = float3(1, 1, 1), RotorMass = float3(1, 1, 1),
            MaximumRpm = Constant(100), CouplingLambda = float3(1, 1, 1), LambdaMultiplier = Constant(1),
            CouplingEfficiency = Constant(1), Torque = Curved(30, exponent), TorqueProfile = FlatCurve(1),
            EnergyDraw = Constant(50), PassiveCoupling = Constant(1)
        });
        var ship = BuildShip(cache, reactorCharge);
        ship.Update(1f);
        var drive = ship.GetBehavior<AetherDrive>();
        return (drive.Rpm.x, drive);
    }

    [Fact]
    public void AetherDriveAtHalfGrantProducesReducedNotZeroNotFullSpinUp()
    {
        var (full, _) = RunAetherDrive(reactorCharge: 1000); // demand ~50, generation far exceeds it -> ratio 1
        var (half, _) = RunAetherDrive(reactorCharge: 25);   // demand ~50, generation 25 -> ratio .5

        Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
    }

    [Fact]
    public void AetherDriveAtFullGrantIsUnchangedFromToday()
    {
        var (full, drive) = RunAetherDrive(reactorCharge: 1000);
        Assert.Equal(1f, drive.Item.PowerSupply, 3);
        Assert.True(full > 0f);
    }

    [Fact]
    public void AetherDriveAtZeroGrantProducesNothing()
    {
        var (zero, drive) = RunAetherDrive(reactorCharge: 0);
        Assert.Equal(0f, drive.Item.PowerSupply, 3);
        Assert.Equal(0f, zero, 4);
    }

    // --- Radiator: no explicit power gate survives Cut 7 at all -- PumpedHeat above is a plain Evaluate() read,
    // --- so a curved PumpedHeat already pumps less under a partial grant, and waste heat is unaffected, so a
    // --- starved radiator falls behind (the reduced-performance failure the ruling asks for) instead of the
    // --- pump simply refusing to run. Measured via RadiatorTemperature's own rise, which is exactly
    // --- pumpedHeat / ThermalMass * dt. ---
    private (float temperatureDelta, Radiator radiator) RunRadiator(float reactorCharge, float exponent = 1)
    {
        using var cache = OpenCatalog(new RadiatorData
        {
            Emissivity = Constant(0), PumpedHeat = Curved(10, exponent), TemperatureFloor = 0,
            WasteHeat = Constant(.1f), EnergyUsage = Constant(50), ThermalMass = Constant(1000)
        });
        var ship = BuildShip(cache, reactorCharge);
        var radiator = ship.GetBehavior<Radiator>();
        var before = radiator.RadiatorTemperature;
        ship.Update(1f);
        return (radiator.RadiatorTemperature - before, radiator);
    }

    [Fact]
    public void RadiatorAtHalfGrantPumpsReducedNotZeroNotFullHeat()
    {
        var (full, _) = RunRadiator(reactorCharge: 1000); // demand 50, generation far exceeds it -> ratio 1
        var (half, _) = RunRadiator(reactorCharge: 25);   // demand 50, generation 25 -> ratio .5

        Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
    }

    [Fact]
    public void RadiatorAtFullGrantIsUnchangedFromToday()
    {
        var (full, radiator) = RunRadiator(reactorCharge: 1000);
        Assert.Equal(1f, radiator.Item.PowerSupply, 3);
        Assert.True(full > 0f);
    }

    [Fact]
    public void RadiatorAtZeroGrantProducesNothing()
    {
        var (zero, radiator) = RunRadiator(reactorCharge: 0);
        Assert.Equal(0f, radiator.Item.PowerSupply, 3);
        Assert.Equal(0f, zero, 4);
    }

    // --- ConstantWeapon: the exact bug the ruling names -- a partial grant used to safe the weapon off
    // --- entirely (_firing = false), not merely fire it for less. base.Execute(dt) re-reads Damage through
    // --- Evaluate() every tick regardless of _firing, so a curved Damage stat already degrades; this proves the
    // --- weapon also keeps firing (Firing stays true) at a partial grant, and only a true-zero grant stops it. ---
    private (float damage, bool firing) RunConstantWeapon(float reactorCharge, float exponent = 1)
    {
        using var cache = OpenCatalog(new ConstantWeaponData
        {
            DamageType = DamageType.Kinetic, Damage = Curved(100, exponent), Penetration = Constant(0),
            DamageSpread = Constant(0), MinRange = Constant(0), Range = Constant(0), Energy = Constant(50),
            Heat = Constant(0), Visibility = Constant(0), MagazineSize = 0
        });
        var ship = BuildShip(cache, reactorCharge);
        var weapon = ship.GetBehavior<ConstantWeapon>();
        weapon.Activate();
        ship.Update(1f);
        return (weapon.Damage, weapon.Firing);
    }

    [Fact]
    public void ConstantWeaponAtHalfGrantKeepsFiringWithReducedDamage()
    {
        var (full, fullFiring) = RunConstantWeapon(reactorCharge: 1000); // demand 50, ratio 1
        var (half, halfFiring) = RunConstantWeapon(reactorCharge: 25);   // demand 50, ratio .5

        Assert.True(fullFiring);
        Assert.True(halfFiring); // the flicking-off bug: this used to be false
        Assert.True(half > 0f && half < full, $"half={half} should sit strictly between 0 and full={full}");
    }

    [Fact]
    public void ConstantWeaponAtFullGrantIsUnchangedFromToday()
    {
        var (full, firing) = RunConstantWeapon(reactorCharge: 1000);
        Assert.True(firing);
        Assert.Equal(100f, full, 3);
    }

    [Fact]
    public void ConstantWeaponAtZeroGrantStopsFiringAndProducesNothing()
    {
        var (zero, firing) = RunConstantWeapon(reactorCharge: 0);
        Assert.False(firing);
        Assert.Equal(0f, zero, 3);
    }

    [Fact]
    public void ConstantWeaponDamageDegradesByItsAuthoredExponentNotLinearly()
    {
        var (half, firing) = RunConstantWeapon(reactorCharge: 25, exponent: 2);
        Assert.True(firing);
        Assert.Equal(25f, half, 3); // lerp(0, 100, pow(.5, 2)) == 25, not the linear-response 50
    }

    // --- EnergyDraw: named explicitly (docs/stats-and-power-target.md) as the one continuous consumer whose
    // --- effect is not a performance stat at all -- its Execute return value IS the effect (Entity.cs's
    // --- per-BehaviorGroup Execute chain reads it to decide whether later behaviours in the group run). There
    // --- is nothing here for a curve to degrade continuously, so a partial grant still passes and only a true
    // --- zero grant closes the gate. Execute is public and side-effect-free (a plain read of Item.PowerSupply),
    // --- so it is safe to call directly for inspection after the same tick the group loop already ran it in. ---
    private bool RunEnergyDraw(float reactorCharge)
    {
        using var cache = OpenCatalog(new EnergyDrawData { EnergyDraw = Constant(50), PerSecond = true });
        var ship = BuildShip(cache, reactorCharge);
        ship.Update(1f);
        return ship.GetBehavior<EnergyDraw>().Execute(1f);
    }

    [Fact]
    public void EnergyDrawAtHalfGrantStillPasses()
    {
        Assert.True(RunEnergyDraw(reactorCharge: 25)); // demand 50, ratio .5 -- used to fail this gate entirely
    }

    [Fact]
    public void EnergyDrawAtFullGrantPasses()
    {
        Assert.True(RunEnergyDraw(reactorCharge: 1000));
    }

    [Fact]
    public void EnergyDrawAtZeroGrantFails()
    {
        Assert.False(RunEnergyDraw(reactorCharge: 0));
    }

    // --- Regression: Cut 4's InputCapacitor all-or-nothing rule for instant items must survive Cut 7 completely
    // --- untouched -- an input capacitor half full still refuses a whole-shot spend, because InstantWeapon,
    // --- Sensor and Shield are unaffected by anything in this cut. Unit-level (no Ship/catalog needed at all):
    // --- InputCapacitor is a plain class. ---
    [Fact]
    public void AnInputCapacitorStillRefusesToSpendOnAPartialCharge()
    {
        var capacitor = new InputCapacitor();
        capacitor.UpdateStats(energy: 10, cooldown: 1); // Capacity = 10, Rate = 10
        capacitor.AddCharge(5); // half charge -- real, but not a whole shot

        Assert.True(capacitor.CanSpend(5));
        Assert.False(capacitor.TrySpend(10)); // the whole-shot cost must still be refused outright

        capacitor.AddCharge(5); // now full
        Assert.True(capacitor.TrySpend(10)); // and only now does it fire, for the whole cost
    }
}
