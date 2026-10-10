using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// The first two consumables (aetheria-release cut consumables-first-set, rulings consumables-scarce,
// overdrive-keep-steering, vapour-cloud-obscuration), read from the shipped catalog and run on a restored hull
// through the real consumable host: the Thruster Overdrive locks the throttle and multiplies the drive, the
// Coolant Vent leaves a cloud and takes its toll on the radiators. Throttles and sight lines belong to
// ThrottleLockTests and VapourCloudTests; this file owns what the catalog says and that it reaches the stats.
public sealed class ConsumablesFirstSetTests
{
    private const string OverdriveName = "Thruster Overdrive";
    private const string VentName = "Coolant Vent";
    private const float Tick = .25f; // exact in floats, so a duration of 6 s is 24 ticks and 15 s is 60

    private static ConsumableItemData Design(CultCache cache, string name) => Assert.Single(cache.GetAll<ConsumableItemData>(), c => c.Name == name);

    private static void AssertStat(PerformanceStat stat, float min, float max, string role)
    {
        Assert.Equal(min, stat.Min, 4);
        Assert.Equal(max, stat.Max, 4);
        var term = Assert.Single(stat.Terms);
        Assert.Equal(StatSource.Quality, term.Source);
        Assert.Equal(role, term.Role);
    }

    private static void AssertModifier(BehaviorData behavior, string target, string stat, float min, float max, string role)
    {
        var modifier = Assert.IsType<StatModifierData>(behavior);
        Assert.Equal(target, modifier.Stat.Target);
        Assert.Equal(stat, modifier.Stat.Stat);
        Assert.Equal(StatModifierType.Multiplier, modifier.Type);
        AssertStat(modifier.Modifier, min, max, role);
    }

    // Half the diagonal of a ship hull's footprint in zone units: the radius of the smallest disc that holds it.
    private static float Circumradius(HullData hull, float cellSize) =>
        .5f * cellSize * MathF.Sqrt(hull.Shape.Width * hull.Shape.Width + hull.Shape.Height * hull.Shape.Height);

    [Fact]
    public void ConsumablesFirstSetIsAuthored()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var cellSize = RestoredHullsTests.Settings().SchematicCellSize;

        var overdrive = Design(cache, OverdriveName);
        Assert.Equal(1, overdrive.Shape.Width);
        Assert.Equal(1, overdrive.Shape.Height);
        Assert.Equal(10f, overdrive.Mass);
        Assert.Equal(18000, overdrive.Price);
        Assert.False(overdrive.Stackable);
        Assert.Equal(6f, overdrive.Duration);
        Assert.Equal(new[] { "propellant", "regulator" }, overdrive.Roles.Select(r => r.Name).ToArray());
        Assert.Equal(5, overdrive.Behaviors.Count);
        Assert.IsType<ThrottleLockData>(overdrive.Behaviors[0]); // the lock is decided before the multipliers run
        AssertModifier(overdrive.Behaviors[1], nameof(ThrusterData), nameof(ThrusterData.Thrust), 3f, 4.5f, "propellant");
        AssertModifier(overdrive.Behaviors[2], nameof(VelocityLimitData), nameof(VelocityLimitData.TopSpeed), 2f, 3f, "propellant");
        AssertModifier(overdrive.Behaviors[3], nameof(ThrusterData), nameof(ThrusterData.Heat), 5f, 3f, "regulator");
        AssertModifier(overdrive.Behaviors[4], nameof(ThrusterData), nameof(ThrusterData.Visibility), 5f, 3f, "regulator");

        var vent = Design(cache, VentName);
        Assert.Equal(1, vent.Shape.Width);
        Assert.Equal(1, vent.Shape.Height);
        Assert.Equal(15f, vent.Mass);
        Assert.Equal(12000, vent.Price);
        Assert.False(vent.Stackable);
        Assert.Equal(15f, vent.Duration);
        Assert.Equal(new[] { "nozzle", "coolant" }, vent.Roles.Select(r => r.Name).ToArray());
        Assert.Equal(2, vent.Behaviors.Count);
        var dump = Assert.IsType<VapourDumpData>(vent.Behaviors[0]);
        // The cloud at its smallest holds every ship hull; its largest is half as big again.
        var radiusMin = dump.Radius.Min;
        foreach (var hull in cache.GetAll<HullData>().Where(h => h.HullType == HullType.Ship))
            Assert.True(radiusMin >= Circumradius(hull, cellSize), $"{hull.Name} pokes out of the smallest cloud");
        Assert.Equal(1.5f * Circumradius(cache.GetByName<HullData>("Djinni"), cellSize), radiusMin, 3);
        Assert.Equal(33.03f, radiusMin, 1); // the Djinni, 14x17 cells at two units a cell
        AssertStat(dump.Radius, radiusMin, 1.5f * radiusMin, "nozzle");
        AssertStat(dump.Opacity, .90f, .98f, "nozzle");
        AssertStat(dump.Lifetime, 14f, 20f, "coolant");
        AssertModifier(vent.Behaviors[1], nameof(RadiatorData), nameof(RadiatorData.Emissivity), .3f, .5f, "coolant");

        foreach (var design in new[] { overdrive, vent })
        {
            CultRecordRefs.Validate(design); // every modifier resolves and every role a stat reads is declared
            Assert.True(design.SpecificHeat > 0f && design.Conductivity > 0f);

            var products = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(cache.RefOf(design).Key)).ToArray();
            Assert.Equal(2, products.Length);
            var makers = products.Select(p => cache.Get(p.Manufacturer)).ToArray();
            Assert.Equal(2, makers.Select(m => m.Name).Distinct().Count());
            Assert.All(makers, maker =>
            {
                Assert.DoesNotContain("Terri", maker.Name + maker.ShortName, StringComparison.OrdinalIgnoreCase); // ruling miss-terris-belongs-to-emily-r3
                Assert.DoesNotContain("Pirates", maker.Name + maker.ShortName, StringComparison.OrdinalIgnoreCase);
            });
            Assert.All(products, product =>
            {
                Assert.Equal(design.Roles.Select(r => r.Name).OrderBy(r => r, StringComparer.Ordinal).ToArray(),
                    product.Roles.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray());
                Assert.NotEqual(design.Name, product.Name);
                Assert.False(string.IsNullOrWhiteSpace(product.Description));
            });
            // No description names a maker of this design, the design's own included.
            foreach (var text in products.Select(p => p.Description).Append(design.Description))
                foreach (var maker in makers)
                {
                    Assert.DoesNotContain(maker.Name, text, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain(maker.ShortName, text, StringComparison.OrdinalIgnoreCase);
                }
        }
    }

    // A restored Djinni with its drives, reactor and one radiator, warmed up; the consumable is minted through the
    // production lot path with the given quality per role.
    private sealed class Run : IDisposable
    {
        public readonly CultCache Cache = RestoredHullsTests.OpenCatalog();
        public readonly Ship Ship;
        public readonly Zone Zone;

        public Run()
        {
            Ship = RestoredHullsTests.BuildThrustedShip(Cache, "Djinni", HardpointType.Radiator);
            Zone = Ship.Zone;
            Zone.Update(0f);
        }

        public void Dispose() => Cache.Dispose();

        public IEnumerable<EquippedItem> With<T>() where T : BehaviorData => Ship.Equipment.Where(e => e.Data.Behaviors.OfType<T>().Any());

        public float Read<T>(EquippedItem item, Func<T, PerformanceStat> stat) where T : BehaviorData =>
            item.Evaluate(stat(item.Data.Behaviors.OfType<T>().First()));

        public void Activate(ConsumableItemData design, float propellantOrNozzleQuality, float regulatorOrCoolantQuality)
        {
            var first = design.Roles[0].Name;
            var second = design.Roles[1].Name;
            var lot = Ship.ItemManager.Lots.Add(new Lot
            {
                Design = Cache.RefOf<ItemData>(design), Origin = new Attributed(), Quality = .5f,
                Roles = new List<RoleFill>
                {
                    new RoleFill { Role = first, Quality = propellantOrNozzleQuality },
                    new RoleFill { Role = second, Quality = regulatorOrCoolantQuality },
                }
            });
            Ship.ActivateConsumable(new ConsumableItem { Data = Cache.RefOf<ItemData>(design), Lot = lot });
        }

        public void Step(int ticks)
        {
            for (var i = 0; i < ticks; i++) Zone.Update(Tick);
        }
    }

    private static float TopSpeed(Run run) => run.Read<VelocityLimitData>(run.With<VelocityLimitData>().First(), d => d.TopSpeed);

    // Quality rises the benefit and lowers the cost: each role's quality picks its own stats' place in Min..Max.
    [Theory]
    [InlineData(1f, 0f, 4.5f, 3f, 5f, 5f)]
    [InlineData(0f, 1f, 3f, 2f, 3f, 3f)]
    public void TheOverdriveLocksAndBoosts(float propellant, float regulator, float thrust, float topSpeed, float heat, float visibility)
    {
        using var run = new Run();
        var design = Design(run.Cache, OverdriveName);
        var drives = run.With<ThrusterData>().ToArray();
        Assert.NotEmpty(drives);
        var baseThrust = drives.Select(d => run.Read<ThrusterData>(d, t => t.Thrust)).ToArray();
        var baseHeat = drives.Select(d => run.Read<ThrusterData>(d, t => t.Heat)).ToArray();
        var baseVisibility = drives.Select(d => run.Read<ThrusterData>(d, t => t.Visibility)).ToArray();
        var baseTopSpeed = TopSpeed(run);
        Assert.All(baseThrust.Concat(baseHeat).Concat(baseVisibility).Append(baseTopSpeed), v => Assert.True(v > 0f));
        Assert.False(run.Ship.ThrottleLocked);
        var entries = run.Ship.Resolver.ModifierEntryCount;

        run.Activate(design, propellant, regulator);
        Assert.True(run.Ship.ThrottleLocked);
        run.Step(2); // the first tick executes the modifiers and the second applies them

        // Heat wears a drive slightly while it burns at full input, so the scale is read within 3 percent.
        for (var i = 0; i < drives.Length; i++)
        {
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Thrust) / baseThrust[i], thrust * .97f, thrust * 1.03f);
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Heat) / baseHeat[i], heat * .97f, heat * 1.03f);
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Visibility) / baseVisibility[i], visibility * .97f, visibility * 1.03f);
        }
        Assert.InRange(TopSpeed(run) / baseTopSpeed, topSpeed * .97f, topSpeed * 1.03f);
        Assert.True(run.Ship.ThrottleLocked);

        run.Step(21); // 5.75 s in: the last tick before the effect runs out
        Assert.True(run.Ship.ThrottleLocked);
        Assert.InRange(TopSpeed(run) / baseTopSpeed, topSpeed * .97f, topSpeed * 1.03f);

        run.Step(1); // 6 s: the effect is spent, the lock goes with it, and no modifier outlives it
        Assert.False(run.Ship.ThrottleLocked);
        Assert.Null(run.Ship.FindActiveConsumable(design));
        Assert.Equal(entries, run.Ship.Resolver.ModifierEntryCount);
        for (var i = 0; i < drives.Length; i++)
        {
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Thrust) / baseThrust[i], 0f, 1.2f);
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Heat) / baseHeat[i], 0f, 1.2f);
            // A drive that has burned for six seconds glows brighter than a cold one (up to 1.34 measured); a boost that
            // outlived the effect would sit at 3 or more, and the resolver's entry count above is the exact check.
            Assert.InRange(run.Read<ThrusterData>(drives[i], t => t.Visibility) / baseVisibility[i], 0f, 1.8f);
        }
        Assert.InRange(TopSpeed(run) / baseTopSpeed, 0f, 1.2f);
    }

    [Theory]
    [InlineData(1f, 0f, 1.5f, .98f, 14f, .3f)]
    [InlineData(0f, 1f, 1f, .90f, 20f, .5f)]
    public void TheCoolantVentsACloud(float nozzle, float coolant, float radiusOfMax, float opacity, float lifetime, float emissivity)
    {
        using var run = new Run();
        var design = Design(run.Cache, VentName);
        var radiatorItem = run.With<RadiatorData>().First();
        var baseEmissivity = run.Read<RadiatorData>(radiatorItem, r => r.Emissivity);
        Assert.True(baseEmissivity > 0f);
        var entries = run.Ship.Resolver.ModifierEntryCount;
        Assert.Empty(run.Zone.Clouds);

        run.Activate(design, nozzle, coolant);
        run.Step(2);

        var cloud = Assert.Single(run.Zone.Clouds);
        var radiusMin = ((VapourDumpData) design.Behaviors[0]).Radius.Min;
        Assert.Equal(radiusMin * radiusOfMax, cloud.Radius, 2);
        Assert.Equal(opacity, cloud.Opacity, 3);
        Assert.Equal(lifetime, cloud.Lifetime, 3);
        Assert.InRange(run.Read<RadiatorData>(radiatorItem, r => r.Emissivity) / baseEmissivity, emissivity * .97f, emissivity * 1.03f);

        run.Step(57); // 14.75 s in: the penalty holds to the last tick
        Assert.InRange(run.Read<RadiatorData>(radiatorItem, r => r.Emissivity) / baseEmissivity, emissivity * .97f, emissivity * 1.03f);

        run.Step(1); // 15 s: the coolant is back
        Assert.Null(run.Ship.FindActiveConsumable(design));
        Assert.Equal(entries, run.Ship.Resolver.ModifierEntryCount);
        Assert.InRange(run.Read<RadiatorData>(radiatorItem, r => r.Emissivity) / baseEmissivity, .97f, 1.03f);
    }
}
