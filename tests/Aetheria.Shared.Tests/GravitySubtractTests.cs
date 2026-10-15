using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// flatspace-no-bowl-no-ripples (aetheria-gravity target r3): a zone's height is the sum of its bodies' wells and
// nothing else. No zone-wide bowl sized by the pack, and no gas-giant or sun ripple that varies with zone time.
public sealed class GravitySubtractTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-gravsub-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;

    public GravitySubtractTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "grav.cc"), Path.Combine(_root, "grav-run.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        _cache.FlushAsync().Wait();
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 2f
        };
        _items = new ItemManager(_cache, new ProvenanceLedger(), settings, _ => { });
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    // Curves for a body whose well, light and orbit all vary with mass, so a body that read a different
    // mass or multiplier than its twin would show it.
    private static PlanetSettings Planets() => new PlanetSettings
    {
        WellStrength = new ExponentialCurve { Exponent = .2f, Multiplier = 45.08f },
        CoreRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 3f },
        RocheLimit = new ExponentialCurve { Exponent = .25f, Multiplier = 7.32f },
        BodyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 3f },
        GravityRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 500f },
        GravityDepth = new ExponentialCurve { Exponent = .175f, Multiplier = 30f },
        LightRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 300f },
        OrbitPeriod = new ExponentialCurve()
    };

    private Zone Bare(float packRadius) => new Zone(_items, Planets(), new ZonePack { Radius = packRadius },
        new GalaxyZone { Name = "Bare Zone", Owner = null }, null);

    [Fact]
    public void AZoneWithNoBodiesIsFlat()
    {
        foreach (var packRadius in new[] { 1000f, 2500f })
        {
            var zone = Bare(packRadius);
            foreach (var r in new[] { 0f, 300f, 577f, 900f, 2000f })
            {
                var at = new float2(r, 0);
                Assert.Equal(0f, zone.GetHeight(at));
                Assert.Equal(0f, zone.GetForce(at).x);
                Assert.Equal(0f, zone.GetForce(at).y);
                var inPlane = new float2(0, r);
                Assert.Equal(0f, zone.GetHeight(inPlane));
                Assert.Equal(0f, zone.GetForce(inPlane).x);
                Assert.Equal(0f, zone.GetForce(inPlane).y);
            }
        }
    }

    // One body of `data` at the origin of a zone whose only body it is.
    private Zone WithBody(BodyData data)
    {
        var orbit = _cache.Upsert(new OrbitData { FixedPosition = float2.zero }).Key;
        data.Orbit = new CultRecordRef<OrbitData>(orbit);
        data.Mass = 100f;
        data.GravityRadiusMultiplier = 1.5f;
        data.GravityDepthMultiplier = .7f;
        var body = _cache.Upsert(data).Key;
        var pack = new ZonePack { Radius = PackRadius };
        pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        pack.Planets.Add(new CultRecordRef<BodyData>(body));
        return new Zone(_items, Planets(), pack, new GalaxyZone { Name = "Giant " + data.GetType().Name, Owner = null }, null);
    }

    // The wells are the whole of a giant's height at every point, not just at a few radii: a ripple confined to
    // any annulus, or to a band derived from the body's radius, shows against the plain body somewhere in a sweep
    // that runs from a quarter of the core to twice the pack's radius, the core radius itself included. The sweep
    // is derived from the body the zone built, not from constants.
    [Fact]
    public void AGiantsWellIsAPlainWellAtEveryTime()
    {
        var plain = WithBody(new PlanetData());
        var giants = new[] { WithBody(new GasGiantData()), WithBody(new SunData()) };
        var core = plain.PlanetInstances.Values.Single().CoreRadius;
        Assert.True(core > 1f, "the fixture body has a core to sweep around");
        var radii = new System.Collections.Generic.List<float> { 0.5f, core };
        for (var r = core / 4f; r <= 4f * core; r += core / 16f) radii.Add(r);
        for (var r = 4f * core; r <= 2f * PackRadius; r += PackRadius / 200f) radii.Add(r);
        var angles = new[] { 0f, 0.7f, 2.1f, 4.0f };
        foreach (var time in new[] { 0f, 37.5f, 91.25f })
        {
            // Zone time 0 is the zones as built; each later time is one Update to that time.
            var step = time - _clock;
            if (step > 0f)
            {
                plain.Update(step);
                foreach (var giant in giants) giant.Update(step);
                _clock = time;
            }
            foreach (var r in radii)
            foreach (var a in angles)
            {
                var at = new float2(r * (float)Math.Cos(a), r * (float)Math.Sin(a));
                foreach (var giant in giants)
                {
                    Assert.Equal(plain.GetHeight(at), giant.GetHeight(at));
                    Assert.Equal(plain.GetForce(at).x, giant.GetForce(at).x);
                    Assert.Equal(plain.GetForce(at).y, giant.GetForce(at).y);
                }
            }
            Assert.True(plain.GetHeight(new float2(50f, 0)) < 0f, "the plain body digs a well here, so there is something to compare");
        }
    }

    private float _clock;
    private const float PackRadius = 1000f;

    // CultCache has no primitive for reserving a removed MessagePack key. Key 8 of a body record (its former well
    // exponent) and the wave keys of the gas giants (14-17) must not come back with a different meaning, because
    // stored records may still carry them. Every record type that is a BodyData is checked, bases walked, so a giant
    // kind added later (the sun is one) cannot take them.
    [Fact]
    public void ABodyRecordNeverReusesRetiredKeys()
    {
        static System.Collections.Generic.List<int> KeysOf(System.Type type)
        {
            var keys = new System.Collections.Generic.List<int>();
            for (var t = type; t != null; t = t.BaseType)
                foreach (var m in t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                               System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                    if (m.GetCustomAttributes(typeof(MessagePack.KeyAttribute), false) is MessagePack.KeyAttribute[] ks)
                        foreach (var k in ks) if (k.IntKey.HasValue) keys.Add(k.IntKey.Value);
            return keys;
        }

        var bodies = typeof(BodyData).Assembly.GetTypes().Where(x => typeof(BodyData).IsAssignableFrom(x)).ToArray();
        Assert.Contains(typeof(SunData), bodies);
        foreach (var type in bodies)
        {
            var keys = KeysOf(type);
            Assert.NotEmpty(keys);
            Assert.DoesNotContain(8, keys);
            if (typeof(GasGiantData).IsAssignableFrom(type))
                foreach (var retired in new[] { 14, 15, 16, 17 }) Assert.DoesNotContain(retired, keys);
        }
    }
}
