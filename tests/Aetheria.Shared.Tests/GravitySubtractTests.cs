using System;
using System.IO;
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
        GravityStrength = 1,
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

    // The fixture the five bowl tests share reproduces the zone bowl it replaced (map B14): the bowl was
    // -PowerPulse(r / (2 R), exponent) x depth, written out here from its definition, deepest at the centre and
    // zero from r = R on, so a fixture whose radius or depth drifted from the bowl would change every pull it feeds.
    [Fact]
    public void TheWellFixtureReproducesTheFormerBowl()
    {
        foreach (var (radius, depth, exponent, at, expected) in new[]
                 {
                     (1000f, 1000f, 1f, 0f, -1000f), (1000f, 1000f, 1f, 400f, -840f), (1000f, 1000f, 1f, 1000f, 0f),
                     (2000f, 64f, 2f, 1000f, -36f), (2000f, 64f, 2f, 300f, -61.1524f)
                 })
        {
            var planets = Planets();
            var pack = new ZonePack { Radius = radius };
            WellFixture.Dig(_cache, pack, planets, radius, depth, exponent);
            var zone = new Zone(_items, planets, pack, new GalaxyZone { Name = "Bowl Zone", Owner = null }, null);
            Assert.InRange(zone.GetHeight(new float2(at, 0)), expected - 1e-2f, expected + 1e-2f);
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
        data.GravityDepthExponent = 2f;
        var body = _cache.Upsert(data).Key;
        var pack = new ZonePack { Radius = 1000f };
        pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        pack.Planets.Add(new CultRecordRef<BodyData>(body));
        return new Zone(_items, Planets(), pack, new GalaxyZone { Name = "Giant " + data.GetType().Name, Owner = null }, null);
    }

    // The wells are the whole of a giant's height at every point, not just at a few radii: a ripple confined to
    // any annulus, or to a band derived from the body's radius, shows against the plain body somewhere in a sweep
    // that runs from the centre to well past the well's support.
    [Fact]
    public void AGiantsWellIsAPlainWellAtEveryTime()
    {
        var plain = WithBody(new PlanetData());
        var giants = new[] { WithBody(new GasGiantData()), WithBody(new SunData()) };
        var radii = new System.Collections.Generic.List<float> { 0.5f };
        for (var r = 1f; r <= 100f; r += 1f) radii.Add(r);
        for (var r = 105f; r <= 2000f; r += 5f) radii.Add(r);
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

    // CultCache has no primitive for reserving a removed MessagePack key; the wave keys of the gas giant (14-17)
    // must not come back with a different meaning, because stored records may still carry them.
    [Fact]
    public void AGasGiantRecordNeverReusesTheRetiredWaveKeys()
    {
        var keys = new System.Collections.Generic.List<int>();
        for (var t = typeof(GasGiantData); t != null; t = t.BaseType)
            foreach (var m in t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                           System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                if (m.GetCustomAttributes(typeof(MessagePack.KeyAttribute), false) is MessagePack.KeyAttribute[] ks)
                    foreach (var k in ks) if (k.IntKey.HasValue) keys.Add(k.IntKey.Value);
        Assert.NotEmpty(keys);
        foreach (var retired in new[] { 14, 15, 16, 17 }) Assert.DoesNotContain(retired, keys);
    }
}
