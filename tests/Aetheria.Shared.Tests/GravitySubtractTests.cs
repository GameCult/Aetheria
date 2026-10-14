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

    [Fact]
    public void AGiantsWellIsAPlainWellAtEveryTime()
    {
        var plain = WithBody(new PlanetData());
        var giants = new[] { WithBody(new GasGiantData()), WithBody(new SunData()) };
        foreach (var time in new[] { 0f, 37.5f })
        {
            // Zone time 0 is the zones as built; 37.5 is one Update of that length.
            if (time > 0f)
            {
                plain.Update(time);
                foreach (var giant in giants) giant.Update(time);
            }
            foreach (var r in new[] { 50f, 200f, 400f })
            {
                var at = new float2(r, 0);
                Assert.True(plain.GetHeight(at) < 0f, "the plain body digs a well here, so there is something to compare");
                foreach (var giant in giants)
                {
                    Assert.Equal(plain.GetHeight(at), giant.GetHeight(at));
                    Assert.Equal(plain.GetForce(at).x, giant.GetForce(at).x);
                    Assert.Equal(plain.GetForce(at).y, giant.GetForce(at).y);
                }
            }
        }
    }
}
