using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// The two launch suppressors the catalog sells (campaign aetheria-ew, cut ew-hide-launch-catalog; rulings
// ew-suppressor-strength-ruled, ew-suppressor-makers-ruled, ew-mild-suppressor-makers-defaulted), read from the shipped catalog and
// fitted to shipped hulls: what the designs say, that quality moves only the stat it is authored on, and what a player feels. The
// strong Launch Suppressor hides a launch from the target it is fired at and gives out after an opening volley; the mild Launch
// Baffle leaves the target its sight, hides the launch from the crowd and lasts a fight. The entity-level mechanism belongs to
// ContactRevealTests; this file owns the catalog's figures.
public sealed class LaunchSuppressorCatalogTests
{
    internal const string StrongName = "Launch Suppressor";
    internal const string MildName = "Launch Baffle";
    // The observer's sensor, named: the reveal readings below are this design's PassiveInfo, so a catalog that gains an earlier
    // sensor must not change which one the observer fits.
    private const string SensorName = "Compact Sensor";
    private const float Flash = 1000f;
    private const float Tick = .25f; // exact in floats: four ticks are one second

    private static GearData Design(CultCache cache, string name) => Assert.Single(cache.GetAll<GearData>(), g => g.Name == name);

    private static LaunchSuppressorData Data(GearData design) => Assert.Single(design.Behaviors.OfType<LaunchSuppressorData>());

    private static void AssertStat(PerformanceStat stat, float min, float max, string role)
    {
        Assert.Equal(min, stat.Min, 4);
        Assert.Equal(max, stat.Max, 4);
        var term = Assert.Single(stat.Terms);
        Assert.Equal(StatSource.Quality, term.Source);
        Assert.Equal(role, term.Role);
        Assert.Equal(1f, term.Exponent); // linear in the role's quality: the design's range is the whole of what quality changes
    }

    [Fact]
    public void LaunchSuppressorDesignsAreAuthored()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var computer = Design(cache, "Targeting Computer");
        var designs = new[]
        {
            (Design: Design(cache, StrongName), Price: 40000, Factor: (.08f, .03f), Heat: (1.4f, .8f), Rated: (310f, 330f)),
            (Design: Design(cache, MildName), Price: 25000, Factor: (.4f, .2f), Heat: (.8f, .5f), Rated: (310f, 330f)),
        };
        foreach (var (design, price, factor, heat, rated) in designs)
        {
            Assert.Equal(HardpointType.Tool, design.Hardpoint);
            Assert.Equal((1, 1), (design.Shape.Width, design.Shape.Height));
            Assert.True(design.Shape.Cells[0, 0], "the one cell is solid");
            Assert.Equal(60f, design.Mass);
            Assert.Equal(50f, design.Durability);
            Assert.Equal(price, design.Price);
            Assert.False(string.IsNullOrWhiteSpace(design.Description));
            Assert.Equal(new[] { "baffles", "heat sink" }, design.Roles.Select(r => r.Name).ToArray());
            // The thermal constants and temperatures are the Targeting Computer's, the catalog's 1x1 tool.
            Assert.Equal(
                (computer.SpecificHeat, computer.Conductivity, computer.MinimumTemperature, computer.MaximumTemperature, computer.OptimalTemperature, computer.PlateauWidth, computer.ThermalResilience),
                (design.SpecificHeat, design.Conductivity, design.MinimumTemperature, design.MaximumTemperature, design.OptimalTemperature, design.PlateauWidth, design.ThermalResilience));
            var data = Data(design);
            AssertStat(data.Factor, factor.Item1, factor.Item2, "baffles");
            AssertStat(data.HeatPerUnit, heat.Item1, heat.Item2, "heat sink");
            AssertStat(data.RatedTemperature, rated.Item1, rated.Item2, "heat sink");
            CultRecordRefs.Validate(design);
        }

        // Exactly these six products and these makers, each authoring both roles at its maker's own mean and deviation.
        foreach (var (name, makers) in new[]
                 {
                     (StrongName, new[] { "DME", "Finch", "NiteLife", "R&D" }),
                     (MildName, new[] { "Alakrita", "Lightsail" }),
                 })
        {
            var key = cache.RefOf(Design(cache, name)).Key;
            var products = cache.GetAll<FactionProductData>().Where(p => p.Design.Key.Equals(key)).ToArray();
            Assert.Equal(makers, products.Select(p => cache.Get(p.Manufacturer).ShortName).OrderBy(m => m, StringComparer.Ordinal).ToArray());
            Assert.Equal(products.Length, products.Select(p => p.Name).Distinct().Count());
            Assert.All(products, product =>
            {
                var (mean, dev) = Program.MakerProfiles[cache.Get(product.Manufacturer).ShortName];
                Assert.Equal(new[] { "baffles", "heat sink" }, product.Roles.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToArray());
                Assert.All(product.Roles, r =>
                {
                    Assert.Equal(mean, r.Mean, 3);
                    Assert.Equal(dev, r.StandardDeviation, 3);
                });
                Assert.False(string.IsNullOrWhiteSpace(product.Description));
            });
        }
    }

    // A shipped hull with its drives, a reactor, a radiator and the named suppressor fitted before the ship goes live (a ship
    // cannot be equipped once active), minted through the production lot path with a quality for each of the suppressor's roles.
    // The recipe is RestoredHullsTests.BuildThrustedShip's; the suppressor takes the first free interior space, as generation does.
    internal sealed class Rig : IDisposable
    {
        public readonly CultCache Cache = RestoredHullsTests.OpenCatalog();
        public readonly Ship Ship;
        public readonly EquippedItem Suppressor;
        public readonly LaunchSuppressorData Data;
        public readonly int2 Cell;

        public Rig(string hullName, string designName, float baffles = .5f, float sink = .5f, string sensorName = null)
        {
            var hull = Cache.GetByName<HullData>(hullName);
            var design = Design(Cache, designName);
            Data = LaunchSuppressorCatalogTests.Data(design);
            var settings = RestoredHullsTests.Settings();
            var ledger = new ProvenanceLedger();
            var suppressorLot = hull.Hardpoints.Count + 2;
            for (var i = 1; i <= suppressorLot; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = .5f };
            ledger.Lots[suppressorLot] = new Lot
            {
                Origin = new Attributed(), Quality = .5f,
                Roles = new List<RoleFill> { new RoleFill { Role = "baffles", Quality = baffles }, new RoleFill { Role = "heat sink", Quality = sink } },
            };
            var items = new ItemManager(Cache, ledger, settings, _ => { });
            var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
            Ship = new Ship(items, zone, new EquippableItem { Data = Cache.RefOf<ItemData>(hull), Durability = hull.Durability, Lot = 1 }, settings.DefaultEntitySettings);

            var lot = 2;
            var thrusters = Cache.GetAll<GearData>().Where(g => g.Hardpoint == HardpointType.Thruster).ToArray();
            foreach (var hardpoint in hull.Hardpoints.Where(h => h.Type == HardpointType.Thruster))
            {
                var thruster = thrusters.First(hardpoint.IsFilledBy);
                Assert.True(Ship.TryEquip(new EquippableItem { Data = Cache.RefOf<ItemData>(thruster), Durability = thruster.Durability, Lot = lot++ }, hardpoint.Position));
            }
            foreach (var type in new[] { HardpointType.Reactor, HardpointType.Radiator })
            {
                var hardpoint = hull.Hardpoints.First(h => h.Type == type);
                var filler = Cache.GetAll<GearData>().First(hardpoint.IsFilledBy);
                Assert.True(Ship.TryEquip(new EquippableItem { Data = Cache.RefOf<ItemData>(filler), Durability = filler.Durability, Lot = lot++ }, hardpoint.Position));
            }
            if (sensorName != null)
            {
                var sensor = Design(Cache, sensorName);
                var hardpoint = hull.Hardpoints.First(h => h.Type == HardpointType.Sensors && h.IsFilledBy(sensor));
                Assert.True(Ship.TryEquip(new EquippableItem { Data = Cache.RefOf<ItemData>(sensor), Durability = sensor.Durability, Lot = lot++ }, hardpoint.Position));
            }
            Assert.True(Ship.TryEquip(new EquippableItem { Data = Cache.RefOf<ItemData>(design), Durability = design.Durability, Lot = suppressorLot }));

            zone.Admit(Ship, piloted: false);
            Ship.Position = float3.zero;
            zone.Update(0f);
            Suppressor = Ship.Equipment.Single(e => e.Data.Name == designName);
            Cell = Suppressor.InsetShape.Coordinates.Single();
        }

        public void Dispose() => Cache.Dispose();

        // Write one launch flash (a direct call: no update, so nothing cools between calls) and read back what was stored.
        public float Launch(float strength)
        {
            var source = new object();
            Ship.SetSignature(source, SignatureChannel.LaunchFlash, strength, Suppressor);
            return Ship.VisibilitySources[source].Strength;
        }

        public float Reading(PerformanceStat stat) => Suppressor.Evaluate(stat);

        public float Temperature => Ship.Temperature[Cell.x, Cell.y];

        // The temperature the cell reaches after n direct launches of a flash while the suppressor is working: each removed unit
        // costs HeatPerUnit, spread over the cell's thermal mass. Counts the launches made while the cell is at or under its rating.
        public int OpeningVolley(float strength)
        {
            var factor = Math.Clamp(Reading(Data.Factor), 0f, 1f);
            var perCall = (strength - strength * factor) * Reading(Data.HeatPerUnit) / Ship.ThermalMass[Cell.x, Cell.y];
            var rated = Reading(Data.RatedTemperature);
            var count = 0;
            for (double t = Temperature; t <= rated; t += perCall) count++;
            return count;
        }
    }

    // One launch of a fight: what was stored, how far the suppressor's cell rose, and the rise the authored figures predict for the
    // light removed (removed x HeatPerUnit / the cell's thermal mass).
    internal readonly record struct Launched(float Stored, float Rise, float Predicted);

    // A fight of one launch a second with the hull ticking between them (four quarter-second updates).
    internal static Launched[] Fight(string hull, string design, float strength, int launches = 60)
    {
        using var rig = new Rig(hull, design);
        var perUnit = rig.Reading(rig.Data.HeatPerUnit);
        var mass = rig.Ship.ThermalMass[rig.Cell.x, rig.Cell.y];
        var fight = new Launched[launches];
        for (var i = 0; i < launches; i++)
        {
            var before = rig.Temperature;
            var stored = rig.Launch(strength);
            fight[i] = new Launched(stored, rig.Temperature - before, (strength - stored) * perUnit / mass);
            for (var t = 0; t < 4; t++) rig.Ship.Zone.Update(Tick);
        }
        return fight;
    }

    // The number of launches that are suppressed before the first that is not.
    internal static int LeadingSuppressed(string hull, string design, float strength, int launches = 60) =>
        Fight(hull, design, strength, launches).TakeWhile(l => l.Stored < strength).Count();

    // A sensor ship at the origin: the observer. What it reveals is Entity.Sense's own test, a stored strength times its
    // passive information at the emitter's position over the settings' threshold.
    private sealed class Observer : IDisposable
    {
        private readonly Rig _rig;
        private readonly float _threshold = RestoredHullsTests.Settings().TargetDetectionInfoThreshold;

        public Observer() => _rig = new Rig("Longinus", MildName, sensorName: SensorName);

        public void Dispose() => _rig.Dispose();

        public bool Sees(float stored, float distance) => stored * _rig.Ship.PassiveInfo(new float2(distance, 0f), 1f) > _threshold;
    }

    public static IEnumerable<object[]> QualityGrid() =>
        from design in new[] { StrongName, MildName }
        from role in new[] { "baffles", "heat sink" }
        from quality in new[] { 0f, .5f, 1f }
        select new object[] { design, role, quality };

    // The role under test takes the quality; the other role sits at a quality no row uses, so a stat read from the wrong role
    // lands somewhere else.
    [Theory]
    [MemberData(nameof(QualityGrid))]
    public void ShippedSuppressorQualityMovesItsStats(string designName, string role, float quality)
    {
        const float Decoy = .25f;
        var baffles = role == "baffles" ? quality : Decoy;
        var sink = role == "heat sink" ? quality : Decoy;
        using var rig = new Rig("Longinus", designName, baffles, sink);
        float Lerp(PerformanceStat stat, float q) => stat.Min + (stat.Max - stat.Min) * q;

        var factor = rig.Reading(rig.Data.Factor);
        var perUnit = rig.Reading(rig.Data.HeatPerUnit);
        Assert.Equal(Lerp(rig.Data.Factor, baffles), factor, 4);
        Assert.Equal(Lerp(rig.Data.HeatPerUnit, sink), perUnit, 4);
        Assert.Equal(Lerp(rig.Data.RatedTemperature, sink), rig.Reading(rig.Data.RatedTemperature), 4);

        var before = rig.Temperature;
        var stored = rig.Launch(Flash);
        Assert.Equal(Flash * factor, stored, 2);
        var expected = (Flash - stored) * perUnit / rig.Ship.ThermalMass[rig.Cell.x, rig.Cell.y];
        Assert.InRange(rig.Temperature - before, expected * .999f, expected * 1.001f);
    }

    [Theory]
    [InlineData("Longinus")]
    [InlineData("Djinni")]
    public void StrongSuppressorHidesFromItsTargetAndStopsAfterAnOpeningVolley(string hull)
    {
        // From rest with no cooling: each call pays heat into one cell, so a few calls in the rating is crossed and every later
        // call is bright. The count is derived from the cell's thermal mass and the authored figures; the Djinni's margin is
        // under a kelvin, so it is never a literal.
        using var rig = new Rig(hull, StrongName);
        var expectedSuppressed = rig.OpeningVolley(Flash);
        Assert.InRange(expectedSuppressed, 2, 8);
        var stored = Enumerable.Range(0, 12).Select(_ => rig.Launch(Flash)).ToArray();
        var hidden = rig.Reading(rig.Data.Factor) * Flash;
        Assert.All(stored.Take(expectedSuppressed), s => Assert.Equal(hidden, s, 2));
        Assert.All(stored.Skip(expectedSuppressed), s => Assert.Equal(Flash, s));

        // With the hull ticking, one launch a second, a light flash still gets hidden for a dozen launches.
        Assert.True(LeadingSuppressed(hull, StrongName, 300f) >= 12);
    }

    [Fact]
    public void StrongSuppressorHidesALaunchFromTheObserverAtTheTarget()
    {
        using var rig = new Rig("Longinus", StrongName);
        using var observer = new Observer();
        var hidden = rig.Launch(Flash);
        Assert.False(observer.Sees(hidden, 1500f)); // the shot leaves unseen
        Assert.True(observer.Sees(Flash, 1500f));   // the same shot with no suppressor is seen
    }

    [Fact]
    public void MildSuppressorLeavesTheTargetItsSightAndHidesFromTheCrowd()
    {
        using var mild = new Rig("Longinus", MildName);
        using var strong = new Rig("Longinus", StrongName);
        using var observer = new Observer();
        var dimmed = mild.Launch(Flash);
        Assert.Equal(300f, dimmed, 1);               // mid quality: Factor .3
        Assert.True(observer.Sees(dimmed, 1500f));   // the target still sees the launch
        Assert.False(observer.Sees(dimmed, 6000f));  // a far observer loses it...
        Assert.True(observer.Sees(Flash, 6000f));    // ...and would have seen it unsuppressed
        Assert.False(observer.Sees(strong.Launch(Flash), 1500f)); // the strong design hides it from the target too
    }

    // What the ruled figures make of a flash at mid quality: Factor lerp(.08, .03, .5) and lerp(.4, .2, .5).
    private static float MidFactor(string design) => design == StrongName ? .055f : .3f;

    // The suppressor's one cell starts at rest (280 K) and a direct launch is made at each of these temperatures around the
    // rating, a fraction of a kelvin either side: the rating alone decides whether the flash is hidden. No launch step is
    // involved, so the line is pinned to the kelvin, not to however far a launch happens to move the cell.
    [Theory]
    [InlineData(StrongName, 0f, -.5f, true)]
    [InlineData(StrongName, 0f, 0f, true)]
    [InlineData(StrongName, 0f, .5f, false)]
    [InlineData(StrongName, 1f, -.5f, true)]
    [InlineData(StrongName, 1f, .5f, false)]
    [InlineData(MildName, 0f, -.5f, true)]
    [InlineData(MildName, 0f, .5f, false)]
    [InlineData(MildName, .5f, 0f, true)]
    [InlineData(MildName, 1f, -.5f, true)]
    [InlineData(MildName, 1f, .5f, false)]
    public void TheRatedTemperatureIsTheLineBetweenHidingAndNot(string design, float sink, float offset, bool hidden)
    {
        using var rig = new Rig("Longinus", design, sink: sink);
        var rated = rig.Reading(rig.Data.RatedTemperature);
        Assert.Equal(310f + 20f * sink, rated, 3); // the authored 310..330 on the heat sink
        rig.Ship.Temperature[rig.Cell.x, rig.Cell.y] = rated + offset;
        Assert.Equal(hidden ? Flash * MidFactor(design) : Flash, rig.Launch(Flash), 2);
    }

    // Hiding is linear in the flash at every strength, and so is the heat it costs: a rule that treats weak flashes differently
    // (or scales heat with strength) moves a row. Both designs at mid quality, a first launch from rest.
    public static IEnumerable<object[]> FlashGrid() =>
        from design in new[] { StrongName, MildName }
        from strength in new[] { 40f, 200f, 499f, 501f, 1000f, 2400f }
        select new object[] { design, strength };

    [Theory]
    [MemberData(nameof(FlashGrid))]
    public void SuppressionAndItsHeatAreLinearInTheFlash(string design, float strength)
    {
        using var rig = new Rig("Longinus", design);
        var before = rig.Temperature;
        var stored = rig.Launch(strength);
        Assert.Equal(strength * MidFactor(design), stored, 2);
        var expected = (strength - stored) * rig.Reading(rig.Data.HeatPerUnit) / rig.Ship.ThermalMass[rig.Cell.x, rig.Cell.y];
        Assert.InRange(rig.Temperature - before, expected * .999f, expected * 1.001f);
    }

    // "A long fight" for the mild design is launches at one launch a second: 30 of a 300 flash (a light weapon) or 8 of a 1000
    // flash (a heavy launch), on either hull (probed 36 and 33, and 10 and 10). Every one of those must store exactly the flash
    // times the authored Factor and cost exactly the heat its removed light predicts, so a design that stops hiding late, or
    // whose Factor or heat drifts as the cell warms, fails here. Past that the sink sits on its rating and the design flickers:
    // each launch is either fully hidden or fully bright, never in between. That flicker is the ruled design at its rating.
    [Theory]
    [InlineData("Longinus", 300f, 30)]
    [InlineData("Djinni", 300f, 30)]
    [InlineData("Longinus", Flash, 8)]
    [InlineData("Djinni", Flash, 8)]
    public void MildSuppressorKeepsHidingThroughALongFight(string hull, float flash, int hold)
    {
        var fight = Fight(hull, MildName, flash);
        var hidden = flash * MidFactor(MildName);
        for (var i = 0; i < hold; i++)
        {
            Assert.True(Math.Abs(fight[i].Stored - hidden) < .01f, $"{hull}: launch {i} of {flash} stored {fight[i].Stored}, not the authored {hidden}");
            Assert.InRange(fight[i].Rise, fight[i].Predicted * .999f, fight[i].Predicted * 1.001f);
        }
        Assert.All(fight.Skip(hold), l => Assert.True(Math.Abs(l.Stored - hidden) < .01f || l.Stored == flash, $"{hull}: a launch stored {l.Stored}, neither hidden nor bright"));
    }

    [Theory]
    [InlineData("Longinus")]
    [InlineData("Djinni")]
    public void MildSuppressorSurvivesALongerFightThanStrong(string hull)
    {
        foreach (var flash in new[] { 300f, Flash })
        {
            var mild = LeadingSuppressed(hull, MildName, flash);
            var strong = LeadingSuppressed(hull, StrongName, flash);
            Assert.True(mild > strong, $"{hull}: mild {mild} did not outlast strong {strong} at {flash}");
        }
    }
}
