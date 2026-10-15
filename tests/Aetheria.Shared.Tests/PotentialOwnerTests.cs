using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// aetheria-gravity target r4: one potential per zone owns gravity (potential-owns-gravity), built from the authored
// curves of mass (well strength, core radius, Roche limit) and one global exaggeration. Each test builds the zone
// through the production constructor from PlanetSettings and body records and reads the owner's GetForce and
// GetHeight; the closed forms written here state the law, they are not Zone's code.
public sealed class PotentialOwnerTests : IDisposable
{
    private const float PackRadius = 1000f;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-potential-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;

    public PotentialOwnerTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "pot.cc"), Path.Combine(_root, "pot-run.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        _cache.FlushAsync().Wait();
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        };
        _items = new ItemManager(_cache, new ProvenanceLedger(), settings, _ => { });
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    // The authored curves of the shipped Settings.asset: strength c = 45.08 m^.2, Roche limit and core R = 7.32 m^.25.
    private static PlanetSettings Curves(float exaggeration = 1f) => new PlanetSettings
    {
        WellStrength = new ExponentialCurve { Exponent = .2f, Multiplier = 45.08f },
        RocheLimit = new ExponentialCurve { Exponent = .25f, Multiplier = 7.32f },
        BodyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 2f },
        LightRadius = new ExponentialCurve(), OrbitPeriod = new ExponentialCurve(),
        HeightExaggeration = exaggeration
    };

    private int _bodies;

    private Zone ZoneOf(PlanetSettings planets, params (float mass, float2 at)[] bodies) => ZoneOf(planets, null, bodies);

    // `author` edits each body's record before it is stored: the per-body knobs a body record carries.
    private Zone ZoneOf(PlanetSettings planets, Action<PlanetData> author, params (float mass, float2 at)[] bodies)
    {
        var pack = new ZonePack { Radius = PackRadius };
        foreach (var (mass, at) in bodies)
        {
            var orbit = _cache.Upsert(new OrbitData { FixedPosition = at }).Key;
            var data = new PlanetData { Name = "Body " + ++_bodies, Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass };
            author?.Invoke(data);
            var body = _cache.Upsert(data).Key;
            pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
            pack.Planets.Add(new CultRecordRef<BodyData>(body));
        }
        return new Zone(_items, planets, pack, new GalaxyZone { Name = "Potential " + _bodies, Owner = null }, null);
    }

    private static void Near(float expected, float actual, float relative) =>
        Assert.InRange(actual, expected - Math.Abs(expected) * relative - 1e-5f, expected + Math.Abs(expected) * relative + 1e-5f);

    // The law written out: potential c r^2 / 2R^2 inside the core, c (ln(r / R) + 1/2) beyond.
    private static float Phi(float c, float core, float r) => r < core ? c * r * r / (2 * core * core) : c * (log(r / core) + .5f);

    // A zone whose bodies have no authored gravity curves (most test zones) has no pull and no dip, and no NaN either.
    [Fact]
    public void BodiesWithNoAuthoredWellPullAndDigNothing()
    {
        var planets = new PlanetSettings
        {
            BodyRadius = new ExponentialCurve(),
            LightRadius = new ExponentialCurve(), OrbitPeriod = new ExponentialCurve()
        };
        var zone = ZoneOf(planets, (100f, float2.zero), (5000f, new float2(200f, 0f)));
        foreach (var p in new[] { float2.zero, float2(200f, 0f), float2(37f, -911f), float2(5000f, 5000f) })
        {
            Assert.Equal(0f, zone.GetHeight(p));
            Assert.Equal(0f, zone.GetForce(p).x);
            Assert.Equal(0f, zone.GetForce(p).y);
        }
    }

    [Fact]
    public void StrengthFollowsTheAuthoredMassCurve()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 273.9f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var core = planets.RocheLimit.Evaluate(mass);
            foreach (var r in new[] { 3f * core, 10f * core, PackRadius / 2f })
                Near(planets.WellStrength.Evaluate(mass), length(zone.GetForce(new float2(r, 0))) * r, 1e-3f);
        }
    }

    [Fact]
    public void TheCoreRadiusIsTheRocheCurve()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var core = planets.RocheLimit.Evaluate(mass);
            float Pull(float r) => length(zone.GetForce(new float2(0, r)));
            var slope = Pull(.25f * core) / (.25f * core);
            Near(slope, Pull(.5f * core) / (.5f * core), 1e-3f);
            Near(slope, Pull(.9f * core) / (.9f * core), 1e-3f);
            Assert.True(Pull(core) > Pull(.98f * core), "the pull still rises up to the core radius");
            Assert.True(Pull(core) > Pull(1.02f * core), "and falls beyond it");
        }
    }

    // core-at-roche-and-exaggeration-up: the one curve is both the Roche limit and the core. A body with a well and no
    // Roche limit has no core and is an authoring error; any positive, finite limit builds.
    [Fact]
    public void TheRocheLimitIsTheCore()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
            Near(planets.RocheLimit.Evaluate(mass), ZoneOf(planets, (mass, float2.zero)).PlanetInstances.Values.Single().RocheLimit, 1e-6f);

        PlanetSettings Constants(float roche) => new PlanetSettings
        {
            WellStrength = new ExponentialCurve { Constant = 100f },
            RocheLimit = new ExponentialCurve { Constant = roche },
            BodyRadius = new ExponentialCurve { Constant = 10f },
            LightRadius = new ExponentialCurve(), OrbitPeriod = new ExponentialCurve()
        };
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(0f), (100f, float2.zero)));
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(-5f), (100f, float2.zero)));
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(float.PositiveInfinity), (100f, float2.zero)));
        ZoneOf(Constants(50f), (100f, float2.zero));
    }

    // core-at-roche-and-exaggeration-up: growing the core to the Roche limit (3 m^.25 to 7.32 m^.25, 2.44x) and dividing the
    // global K by that factor leaves the steepest drawn slope (|grad phi| / K, at the core) where it was, and draws every
    // slope outside the old core 2.44x steeper. The "before" side is the previous authoring, stated here from the ruling:
    // core 3 m^.25 and K 10. The "after" side is the shipped Settings.asset read through the production zone.
    [Fact]
    public void ThePeakDrawnSlopeStaysAndTheFunnelDrawsSteeper()
    {
        const float oldCoreMultiplier = 3f, oldExaggeration = 10f;
        var planets = AuthoredSettings.Load(RunStartTests.FindRepoRoot()).Read<PlanetSettings>("PlanetSettings");
        foreach (var mass in new[] { 29.2f, 273.9f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var c = planets.WellStrength.Evaluate(mass);
            var oldCore = oldCoreMultiplier * pow(mass, .25f);
            float Drawn(float r) => length(zone.GetForce(new float2(r, 0))) / zone.Settings.HeightExaggeration;

            var peakBefore = c / oldCore / oldExaggeration;
            Assert.InRange(Drawn(planets.RocheLimit.Evaluate(mass)), peakBefore * .98f, peakBefore * 1.02f);

            var rocheOverOldCore = planets.RocheLimit.Evaluate(mass) / oldCore;
            var farBefore = c / (3f * oldCore) / oldExaggeration;
            Near(rocheOverOldCore, Drawn(3f * oldCore) / farBefore, 1e-2f);
            Assert.InRange(Drawn(3f * oldCore) / farBefore, 2.44f * .99f, 2.44f * 1.01f);
        }
    }

    // The shipped curves build a well at every mass of the generated range. The curves are the ones
    // Settings.asset carries, read the way RunStartTests reads them, so a retune of the asset is judged here.
    [Fact]
    public void TheAuthoredCurvesBuildAtEveryMass()
    {
        var planets = AuthoredSettings.Load(RunStartTests.FindRepoRoot()).Read<PlanetSettings>("PlanetSettings");
        Assert.True(planets.WellStrength.Evaluate(100f) > 0f && planets.RocheLimit.Evaluate(100f) > 0f, "the authored asset carries a well");
        for (var exponent10 = -3f; exponent10 <= 8f; exponent10 += .25f)
            ZoneOf(planets, (pow(10f, exponent10), float2.zero));
    }

    // A well that is negative, not a number or infinite is an authoring error, not an antimass body. Zero is the unauthored
    // default and digs nothing (BodiesWithNoAuthoredWellPullAndDigNothing).
    [Theory]
    [InlineData(-10f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1e-6f)]
    [InlineData(-float.Epsilon)]
    public void AnUnphysicalWellStrengthThrowsAtConstruction(float strength)
    {
        var planets = Curves();
        planets.WellStrength = new ExponentialCurve { Constant = strength };
        Assert.Throws<InvalidOperationException>(() => ZoneOf(planets, (100f, float2.zero)));
        planets.RocheLimit = new ExponentialCurve();
        Assert.Throws<InvalidOperationException>(() => ZoneOf(planets, (100f, float2.zero)));
    }

    // Zero is the boundary: a well of no strength builds beside the authored Roche curve (it digs nothing), where
    // the smallest negative strength does not.
    [Theory]
    [InlineData(0f)]
    [InlineData(-0f)]
    public void AWellOfNoStrengthBuildsAndDigsNothing(float strength)
    {
        var planets = Curves();
        planets.WellStrength = new ExponentialCurve { Constant = strength };
        var zone = ZoneOf(planets, (100f, float2.zero));
        foreach (var p in new[] { float2.zero, float2(3f, 1f), float2(40f, -9f), float2(900f, 0f) })
        {
            Assert.Equal(0f, zone.GetHeight(p));
            Assert.Equal(0f, zone.GetForce(p).x);
            Assert.Equal(0f, zone.GetForce(p).y);
        }
    }

    // The well is authored once, by curves of mass (target: no per-body authoring of a core or Roche limit): a body whose
    // record carries another radius multiplier has the same strength, core and Roche limit, and pulls and digs exactly as
    // its plain twin.
    [Theory]
    [InlineData(2.5f)]
    [InlineData(.5f)]
    public void PerBodyMultipliersDoNotMoveTheWell(float bodyRadius)
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
        {
            var plain = ZoneOf(planets, (mass, float2.zero));
            var scaled = ZoneOf(planets, data =>
            {
                data.BodyRadiusMultiplier = bodyRadius;
            }, (mass, float2.zero));
            var a = plain.PlanetInstances.Values.Single();
            var b = scaled.PlanetInstances.Values.Single();
            Assert.Equal(a.WellStrength, b.WellStrength);
            Assert.Equal(a.RocheLimit, b.RocheLimit);
            foreach (var p in new[] { float2(.4f * a.RocheLimit, 0), float2(0, 3f * a.RocheLimit), float2(400f, 300f) })
            {
                Assert.Equal(plain.GetForce(p), scaled.GetForce(p));
                Assert.Equal(plain.GetHeight(p), scaled.GetHeight(p));
            }
        }
    }

    [Fact]
    public void TheTailIsAPointMassWithNoRimAndNoGate()
    {
        var planets = Curves();
        var zone = ZoneOf(planets, (273.9f, float2.zero));
        var c = planets.WellStrength.Evaluate(273.9f);
        var core = planets.RocheLimit.Evaluate(273.9f);
        foreach (var r in new[] { core, 2f * core, PackRadius, 10f * PackRadius, 100f * PackRadius, 1000f * PackRadius })
        foreach (var angle in new[] { 0f, 1.1f, 3.5f })
        {
            var at = new float2(r * cos(angle), r * sin(angle));
            var force = zone.GetForce(at);
            Near(c, length(force) * r, 1e-3f);
            Assert.True(dot(normalize(force), normalize(-at)) > .9999f, "the pull points at the body");
        }
    }

    [Fact]
    public void WellsSuperpose()
    {
        var planets = Curves();
        var a = (mass: 29.2f, at: new float2(-300f, 0f));
        var b = (mass: 14152.4f, at: new float2(300f, 0f));
        var both = ZoneOf(planets, a, b);
        var onlyA = ZoneOf(planets, a);
        var onlyB = ZoneOf(planets, b);
        var probes = new[]
        {
            float2(-300f, 0f), float2(300f, 0f), float2(-310f, 4f), float2(305f, -9f), float2(0f, 0f), float2(0f, 250f),
            float2(-150f, 100f), float2(150f, -100f), float2(900f, 700f), float2(-1200f, -300f), float2(40f, 3000f), float2(-300f, -40f)
        };
        foreach (var p in probes)
        {
            var sum = onlyA.GetForce(p) + onlyB.GetForce(p);
            Near(sum.x, both.GetForce(p).x, 1e-4f);
            Near(sum.y, both.GetForce(p).y, 1e-4f);
            Near(onlyA.GetHeight(p) + onlyB.GetHeight(p), both.GetHeight(p), 1e-4f);
        }
    }

    [Fact]
    public void HeightIsThePotentialOverOneK()
    {
        foreach (var k in new[] { 1f, 10f })
        {
            var planets = Curves(k);
            var lone = ZoneOf(planets, (273.9f, float2.zero));
            var c = planets.WellStrength.Evaluate(273.9f);
            var core = planets.RocheLimit.Evaluate(273.9f);
            Assert.Equal(0f, lone.GetHeight(new float2(PackRadius, 0)), 4);
            Near(-c * (log(PackRadius / core) + .5f) / k, lone.GetHeight(float2.zero), 1e-4f);

            var two = ZoneOf(planets, (29.2f, new float2(-300f, 0f)), (14152.4f, new float2(300f, 0f)));
            var c0 = planets.WellStrength.Evaluate(29.2f);
            var core0 = planets.RocheLimit.Evaluate(29.2f);
            var c1 = planets.WellStrength.Evaluate(14152.4f);
            var core1 = planets.RocheLimit.Evaluate(14152.4f);
            const float step = .5f;
            // Inside the two cores as well, out to their outer bands (0.5 R to R), where the potential is the quadratic bowl.
            var inCore = new[]
            {
                float2(-300f + .5f * core0, 0f), float2(-300f, .65f * core0), float2(-300f - .86f * core0, 0f), float2(-300f + .6f * core0, .6f * core0),
                float2(300f + .55f * core1, 0f), float2(300f, -.7f * core1), float2(300f - .86f * core1, 0f), float2(300f + .9f * core1, 0f)
            };
            foreach (var p in inCore.Concat(new[] { float2(-200f, 120f), float2(0f, 0f), float2(100f, -300f), float2(500f, 40f), float2(-290f, 12f), float2(310f, 20f), float2(700f, 700f), float2(-80f, -500f) }))
            {
                var expected = Phi(c0, core0, length(p - float2(-300f, 0f))) - Phi(c0, core0, PackRadius)
                             + Phi(c1, core1, length(p - float2(300f, 0f))) - Phi(c1, core1, PackRadius);
                Near(expected, two.GetHeight(p) * k, 1e-3f);

                // The downhill slope of the potential is the force: the central difference of height * K is -GetForce.
                var gradient = float2(
                    (two.GetHeight(p + float2(step, 0)) - two.GetHeight(p - float2(step, 0))) * k / (2 * step),
                    (two.GetHeight(p + float2(0, step)) - two.GetHeight(p - float2(0, step))) * k / (2 * step));
                var force = two.GetForce(p);
                Assert.InRange(length(gradient + force), 0f, length(force) * 1e-2f + 1e-4f);
            }
        }
    }

    // Inside the core the potential is the bowl c r^2 / 2R^2 all the way out to R: the drop from a lone body's centre to a point
    // at r is exactly that, from the middle of the core to a hair inside its edge (the log form agrees with the bowl to second
    // order at R, so only a tight tolerance sees a join that sits just inside it).
    [Fact]
    public void TheCoreIsTheBowlUpToItsEdge()
    {
        foreach (var k in new[] { 1f, 10f })
        {
            var planets = Curves(k);
            foreach (var mass in new[] { 29.2f, 14152.4f })
            {
                var zone = ZoneOf(planets, (mass, float2.zero));
                var c = planets.WellStrength.Evaluate(mass);
                var core = planets.RocheLimit.Evaluate(mass);
                foreach (var fraction in new[] { .5f, .75f, .9f, .95f, .98f, .99f, .996f })
                foreach (var angle in new[] { 0f, 2.3f })
                {
                    var r = fraction * core;
                    var drop = (zone.GetHeight(new float2(r * cos(angle), r * sin(angle))) - zone.GetHeight(float2.zero)) * k;
                    Near(c * fraction * fraction / 2f, drop, 2e-5f);
                }
            }
        }
    }

    [Fact]
    public void TheCoreMeetsTheTailAtItsRadius()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var core = planets.RocheLimit.Evaluate(mass);
            var inside = new float2(core * (1f - 1e-3f), 0);
            var outside = new float2(core * (1f + 1e-3f), 0);
            Assert.InRange(abs(zone.GetHeight(inside) - zone.GetHeight(outside)), 0f, abs(zone.GetHeight(outside)) * 1e-3f);
            Assert.InRange(abs(length(zone.GetForce(inside)) - length(zone.GetForce(outside))), 0f, length(zone.GetForce(outside)) * 1e-3f);
        }
    }
}
