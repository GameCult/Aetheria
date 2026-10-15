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

    // The authored curves of the shipped Settings.asset: strength c = 45.08 m^.2, core R = 3 m^.25, Roche 7.32 m^.25.
    private static PlanetSettings Curves(float exaggeration = 1f) => new PlanetSettings
    {
        WellStrength = new ExponentialCurve { Exponent = .2f, Multiplier = 45.08f },
        CoreRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 3f },
        RocheLimit = new ExponentialCurve { Exponent = .25f, Multiplier = 7.32f },
        BodyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 2f },
        GravityRadius = new ExponentialCurve(), GravityDepth = new ExponentialCurve(),
        LightRadius = new ExponentialCurve(), OrbitPeriod = new ExponentialCurve(),
        HeightExaggeration = exaggeration
    };

    private int _bodies;

    private Zone ZoneOf(PlanetSettings planets, params (float mass, float2 at)[] bodies)
    {
        var pack = new ZonePack { Radius = PackRadius };
        foreach (var (mass, at) in bodies)
        {
            var orbit = _cache.Upsert(new OrbitData { FixedPosition = at }).Key;
            var body = _cache.Upsert(new PlanetData { Name = "Body " + ++_bodies, Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key;
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
            BodyRadius = new ExponentialCurve(), GravityRadius = new ExponentialCurve(), GravityDepth = new ExponentialCurve(),
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
            var core = planets.CoreRadius.Evaluate(mass);
            foreach (var r in new[] { 3f * core, 10f * core, PackRadius / 2f })
                Near(planets.WellStrength.Evaluate(mass), length(zone.GetForce(new float2(r, 0))) * r, 1e-3f);
        }
    }

    [Fact]
    public void TheCoreRadiusFollowsItsOwnCurve()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var core = planets.CoreRadius.Evaluate(mass);
            float Pull(float r) => length(zone.GetForce(new float2(0, r)));
            var slope = Pull(.25f * core) / (.25f * core);
            Near(slope, Pull(.5f * core) / (.5f * core), 1e-3f);
            Near(slope, Pull(.9f * core) / (.9f * core), 1e-3f);
            Assert.True(Pull(core) > Pull(.98f * core), "the pull still rises up to the core radius");
            Assert.True(Pull(core) > Pull(1.02f * core), "and falls beyond it");
        }
    }

    [Fact]
    public void TheCoreSitsInsideTheRocheLimit()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
            Near(planets.RocheLimit.Evaluate(mass), ZoneOf(planets, (mass, float2.zero)).PlanetInstances.Values.Single().RocheLimit, 1e-6f);

        PlanetSettings Constants(float core, float roche) => new PlanetSettings
        {
            WellStrength = new ExponentialCurve { Constant = 100f },
            CoreRadius = new ExponentialCurve { Constant = core },
            RocheLimit = new ExponentialCurve { Constant = roche },
            BodyRadius = new ExponentialCurve { Constant = 10f },
            GravityRadius = new ExponentialCurve(), GravityDepth = new ExponentialCurve(),
            LightRadius = new ExponentialCurve(), OrbitPeriod = new ExponentialCurve()
        };
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(60f, 50f), (100f, float2.zero)));
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(50f, 50f), (100f, float2.zero)));
        Assert.Throws<InvalidOperationException>(() => ZoneOf(Constants(0f, 50f), (100f, float2.zero)));
        ZoneOf(Constants(20f, 50f), (100f, float2.zero)); // inside the limit, and past the body's radius
    }

    [Fact]
    public void TheTailIsAPointMassWithNoRimAndNoGate()
    {
        var planets = Curves();
        var zone = ZoneOf(planets, (273.9f, float2.zero));
        var c = planets.WellStrength.Evaluate(273.9f);
        var core = planets.CoreRadius.Evaluate(273.9f);
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
            var core = planets.CoreRadius.Evaluate(273.9f);
            Assert.Equal(0f, lone.GetHeight(new float2(PackRadius, 0)), 4);
            Near(-c * (log(PackRadius / core) + .5f) / k, lone.GetHeight(float2.zero), 1e-4f);

            var two = ZoneOf(planets, (29.2f, new float2(-300f, 0f)), (14152.4f, new float2(300f, 0f)));
            var c0 = planets.WellStrength.Evaluate(29.2f);
            var core0 = planets.CoreRadius.Evaluate(29.2f);
            var c1 = planets.WellStrength.Evaluate(14152.4f);
            var core1 = planets.CoreRadius.Evaluate(14152.4f);
            const float step = .5f;
            foreach (var p in new[] { float2(-200f, 120f), float2(0f, 0f), float2(100f, -300f), float2(500f, 40f), float2(-290f, 12f), float2(310f, 20f), float2(700f, 700f), float2(-80f, -500f) })
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

    [Fact]
    public void TheCoreMeetsTheTailAtItsRadius()
    {
        var planets = Curves();
        foreach (var mass in new[] { 29.2f, 14152.4f })
        {
            var zone = ZoneOf(planets, (mass, float2.zero));
            var core = planets.CoreRadius.Evaluate(mass);
            var inside = new float2(core * (1f - 1e-3f), 0);
            var outside = new float2(core * (1f + 1e-3f), 0);
            Assert.InRange(abs(zone.GetHeight(inside) - zone.GetHeight(outside)), 0f, abs(zone.GetHeight(outside)) * 1e-3f);
            Assert.InRange(abs(length(zone.GetForce(inside)) - length(zone.GetForce(outside))), 0f, length(zone.GetForce(outside)) * 1e-3f);
        }
    }
}
