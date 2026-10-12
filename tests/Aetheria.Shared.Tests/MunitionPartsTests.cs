using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Newtonsoft.Json;
using Xunit;

// Aetheria release, cut missile-stats (ruling missile-stats-now-gear-later): a missile type's stats are five gear-part
// behaviours on the launcher design. The shipped catalog is the oracle for what the migration copied and authored.
public sealed class MunitionPartsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "munition-parts-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public MunitionPartsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    // The flight stats of the five designs that carried LauncherData or GuidedWeaponData at b6262467 (Thrust, MissileVelocity
    // and DodgeFrequency), read from that revision's catalog before the fields were retired. All five read role thruster on
    // both ranged stats. The column names are the retired fields'.
    private static readonly (string Design, float ThrustMin, float ThrustMax, float MissileVelocityMin, float MissileVelocityMax, float DodgeFrequency)[] Golden =
    {
        ("GT 3K", 135, 325, 150, 300, 0.25f),
        ("LRMM72", 400, 600, 200, 300, 0.25f),
        ("SRMM72", 1600, 2400, 400, 600, 1f),
        ("pswarm", 500, 2000, 400, 600, 1f),
        ("scorched void policy", 500, 2000, 400, 600, 1f),
    };

    private static PerformanceStat Flat(float value) => new PerformanceStat { Min = value, Max = value };

    private static PerformanceStat Ranged(float min, float max, string role) => role == null ? Flat(max) : new PerformanceStat
    {
        Min = min, Max = max, Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = 1.5f, Role = role } },
    };

    // The five parts a munition design carries, every field set to a distinct value so a field read from the wrong slot or
    // dropped on the way is seen. The thruster's stats vary with the given role's quality when one is named; the design
    // that carries them declares it. Fixtures that only need a valid munition pass none.
    internal static List<BehaviorData> Parts(int group, string role = null) => new List<BehaviorData>
    {
        new MunitionHullData { Group = group, Durability = Flat(21), Radius = Flat(4.5f) },
        new MunitionThrusterData { Group = group, Acceleration = Ranged(300, 400, role), Lateral = Ranged(150, 200, role), MaxSpeed = Ranged(250, 350, role), Visibility = Flat(11) },
        new MunitionSeekerData { Group = group, Sensitivity = Flat(.8f), BeamWidth = Flat(55), Range = Flat(900) },
        new MunitionControlData { Group = group, Navigation = Flat(3.5f), Evasion = Flat(.4f), EvasionFrequency = Flat(.6f), Lifetime = Flat(12), Cognition = Flat(.35f) },
        new MunitionFuelData { Group = group, DeltaV = Flat(700) },
    };

    private static string Describe(PerformanceStat stat) =>
        $"{stat.Min}..{stat.Max} [{string.Join(",", stat.Terms.Select(t => $"{t.Source}^{t.Exponent}@{t.Role}"))}]";

    private static string[] Describe(BehaviorData part) =>
        part.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat))
            .Select(f => $"{part.GetType().Name}.{f.Name} {Describe((PerformanceStat) f.GetValue(part))}").Prepend($"{part.GetType().Name} group {part.Group}").ToArray();

    private static WeaponItemData Munition(string name, BehaviorData launcher, int group = 0)
    {
        launcher.Group = group;
        var design = new WeaponItemData { Name = name, Roles = new List<ItemRole> { new ItemRole { Name = "motor" } } };
        design.Behaviors.Add(launcher);
        design.Behaviors.AddRange(Parts(group, "motor"));
        return design;
    }

    private static T Part<T>(EquippableItemData design) where T : MunitionPartData => design.Behaviors.OfType<T>().Single();

    [Fact]
    public void MunitionPartsRoundTrip()
    {
        var designs = new[] { Munition("Round A", new LauncherData(), 2), Munition("Round B", new GuidedWeaponData(), 3) };
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            foreach (var design in designs) cache.Upsert(design);
            cache.FlushAsync().Wait();
        }

        using var reopened = AetheriaStores.Open(Catalog);
        foreach (var design in designs)
        {
            var stored = reopened.GetByName<WeaponItemData>(design.Name);
            var packed = stored.Behaviors.OfType<MunitionPartData>().ToArray();
            Assert.Equal(new[] { typeof(MunitionHullData), typeof(MunitionThrusterData), typeof(MunitionSeekerData), typeof(MunitionControlData), typeof(MunitionFuelData) },
                packed.Select(p => p.GetType()));
            var expected = design.Behaviors.OfType<MunitionPartData>().ToArray();
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(Describe(expected[i]), Describe(packed[i]));
                var json = (MunitionPartData) JsonConvert.DeserializeObject(JsonConvert.SerializeObject(expected[i]), expected[i].GetType());
                Assert.Equal(Describe(expected[i]), Describe(json));
            }
        }
    }

    [Fact]
    public void MigrationCopiesTheFlightStats()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        foreach (var (name, thrustMin, thrustMax, speedMin, speedMax, dodge) in Golden)
        {
            var design = cache.GetByName<WeaponItemData>(name);
            var thruster = Part<MunitionThrusterData>(design);
            Assert.Equal((thrustMin, thrustMax), (thruster.Acceleration.Min, thruster.Acceleration.Max));
            Assert.Equal((thrustMin / 2f, thrustMax / 2f), (thruster.Lateral.Min, thruster.Lateral.Max));
            Assert.Equal((speedMin, speedMax), (thruster.MaxSpeed.Min, thruster.MaxSpeed.Max));
            // The terms come across whole: the role's Quality term and whatever heat or durability terms the stat carried.
            Assert.Equal(Describe(thruster.Acceleration).Split('[')[1], Describe(thruster.Lateral).Split('[')[1]);
            foreach (var stat in new[] { thruster.Acceleration, thruster.Lateral, thruster.MaxSpeed })
                Assert.Equal("thruster", Assert.Single(stat.Terms, t => t.Source == StatSource.Quality).Role);

            var evasion = Part<MunitionControlData>(design).EvasionFrequency;
            Assert.Equal((dodge, dodge), (evasion.Min, evasion.Max));
            Assert.Empty(evasion.Terms);
        }
    }

    [Fact]
    public void EveryLauncherCarriesAuthoredParts()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var munitions = cache.GetAll<EquippableItemData>()
            .Where(d => d.Behaviors.Any(b => b is LauncherData || b is GuidedWeaponData)).ToArray();
        Assert.Equal(Golden.Select(g => g.Design).OrderBy(n => n, StringComparer.Ordinal), munitions.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        foreach (var design in munitions)
        {
            var parts = design.Behaviors.OfType<MunitionPartData>().Select(p => p.GetType()).OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { typeof(MunitionControlData), typeof(MunitionFuelData), typeof(MunitionHullData), typeof(MunitionSeekerData), typeof(MunitionThrusterData) }, parts);
            var weapon = design.Behaviors.OfType<WeaponData>().Single();
            var hull = Part<MunitionHullData>(design);
            var thruster = Part<MunitionThrusterData>(design);
            var seeker = Part<MunitionSeekerData>(design);
            var control = Part<MunitionControlData>(design);
            var fuel = Part<MunitionFuelData>(design);
            foreach (var (label, stat) in new[]
            {
                ("Durability", hull.Durability), ("Radius", hull.Radius), ("Acceleration", thruster.Acceleration), ("Lateral", thruster.Lateral),
                ("MaxSpeed", thruster.MaxSpeed), ("Visibility", thruster.Visibility), ("Sensitivity", seeker.Sensitivity), ("BeamWidth", seeker.BeamWidth),
                ("Seeker Range", seeker.Range), ("Navigation", control.Navigation), ("Lifetime", control.Lifetime), ("DeltaV", fuel.DeltaV),
            })
                Assert.True(stat.Min > 0f && stat.Max > 0f, $"{design.Name}: {label} is not above zero");
            Assert.True(control.Cognition.Min > 0f && control.Cognition.Max <= 1f, $"{design.Name}: Cognition is not in (0, 1]");
            var expected = 1.5f * weapon.Range.Max;
            Assert.InRange(control.Lifetime.Max * thruster.MaxSpeed.Max, .99f * expected, 1.01f * expected);
        }
    }

    [Fact]
    public void ValidationRefusesIncompleteMunitions()
    {
        CultRecordRefs.Validate(Munition("Complete", new LauncherData()));

        var missing = Munition("No Tank", new LauncherData());
        missing.Behaviors.RemoveAll(b => b is MunitionFuelData);
        var refusal = Assert.Throws<InvalidOperationException>(() => CultRecordRefs.Validate(missing));
        Assert.Contains("No Tank", refusal.Message);
        Assert.Contains(nameof(MunitionFuelData), refusal.Message);

        var doubled = Munition("Two Seekers", new GuidedWeaponData());
        doubled.Behaviors.Add(new MunitionSeekerData());
        refusal = Assert.Throws<InvalidOperationException>(() => CultRecordRefs.Validate(doubled));
        Assert.Contains("Two Seekers", refusal.Message);
        Assert.Contains(nameof(MunitionSeekerData), refusal.Message);

        var stray = new WeaponItemData { Name = "Stray Part" };
        stray.Behaviors.Add(new MunitionHullData());
        refusal = Assert.Throws<InvalidOperationException>(() => CultRecordRefs.Validate(stray));
        Assert.Contains("Stray Part", refusal.Message);
        Assert.Contains(nameof(MunitionHullData), refusal.Message);
    }

    // A GT 3K fitted to a Longinus launcher hardpoint, its lot's thruster role at the given quality.
    private static EquippedItem Fit(CultCache cache, float thrusterQuality, out Ship ship)
    {
        var hull = cache.GetByName<HullData>("Longinus");
        var gt = cache.GetByName<WeaponItemData>("GT 3K");
        var hardpoint = hull.Hardpoints.First(h => h.Type == HardpointType.Launcher && h.IsFilledBy(gt));
        var settings = RestoredHullsTests.Settings();
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = .5f };
        ledger.Lots[2] = new Lot { Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill> { new RoleFill { Role = "thruster", Quality = thrusterQuality } } };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        ship = new Ship(items, zone, new EquippableItem { Data = cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 }, settings.DefaultEntitySettings);
        Assert.True(ship.TryEquip(new EquippableItem { Data = cache.RefOf<ItemData>(gt), Durability = gt.Durability, Lot = 2 }, hardpoint.Position));
        zone.Entities.Add(ship);
        ship.Activate();
        // Settle at the item's optimal temperature, as ConditionRatioTests does, so heat and wear read their identity and only
        // the role's quality moves a stat.
        var item = ship.Equipment.Single(e => e.Data.Name == "GT 3K");
        foreach (var cell in item.InsetShape.Coordinates) ship.Temperature[cell.x, cell.y] = gt.OptimalTemperature;
        item.UpdatePerformance();
        item.UpdatePerformance();
        return item;
    }

    [Fact]
    public void PartsReadThroughTheItem()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var design = cache.GetByName<WeaponItemData>("GT 3K");
        var poor = Fit(cache, .2f, out var poorShip);
        var good = Fit(cache, .9f, out _);

        var part = MunitionParts.Of<MunitionThrusterData>(poor);
        Assert.Same(Part<MunitionThrusterData>(design), part);
        Assert.Same(part, MunitionParts.Of<MunitionThrusterData>(good));
        var low = poor.Evaluate(part.Acceleration);
        var high = good.Evaluate(part.Acceleration);
        Assert.True(low < high, $"role quality .2 reads {low}, .9 reads {high}");
        Assert.InRange(low, part.Acceleration.Min, part.Acceleration.Max);
        Assert.InRange(high, part.Acceleration.Min, part.Acceleration.Max);
        Assert.True(poor.Evaluate(part.MaxSpeed) < good.Evaluate(part.MaxSpeed));
        Assert.Empty(poorShip.GetBehaviors<Thruster>());
        Assert.DoesNotContain(poor.Behaviors, b => b is Thruster);
    }
}
