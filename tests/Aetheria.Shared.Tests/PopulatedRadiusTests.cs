using System;
using System.Collections.Generic;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Zone.PopulatedRadius is the extent of the populated area, owned by the zone and read by the ambient tint's presenter
// (ruling aetheria-gravity:ruling:dark-beyond-populated-area). The zone here is the production constructor reading a pack
// of a root off the origin, a planet, a moon and a moon's moon, each with a well of its own mass.
public sealed class PopulatedRadiusTests : IDisposable
{
    private static readonly float2 RootAt = new float2(300f, -200f);
    private const float PlanetDistance = 400f;
    private const float MoonDistance = 100f;
    private const float MoonsMoonDistance = 40f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-populated-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public PopulatedRadiusTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static ExponentialCurve Flat(float v) => new ExponentialCurve { Constant = v };

    // A body's Roche limit is 10 u per unit of mass, so each body's term differs and the outermost is the moon's moon.
    private static PlanetSettings Settings() => new PlanetSettings
    {
        BodyRadius = Flat(20f),
        WellStrength = new ExponentialCurve { Multiplier = 100f, Exponent = 1f },
        RocheLimit = new ExponentialCurve { Multiplier = 10f, Exponent = 1f },
        LightRadius = Flat(0f)
    };

    private sealed class Scene
    {
        public Zone Zone;
        public CultRecordKey[] Orbits;
    }

    private Scene Build(float radius, bool moonsMoon = true)
    {
        var dir = Path.Combine(_root, $"zone{_caches.Count}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var items = new ItemManager(cache, new ProvenanceLedger(), new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        }, _ => { });

        var rootOrbit = cache.Upsert(new OrbitData { FixedPosition = RootAt }).Key;
        var planetOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(rootOrbit), Distance = PlanetDistance, Phase = .1f }).Key;
        var moonOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(planetOrbit), Distance = MoonDistance, Phase = .3f }).Key;
        var moonsMoonOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(moonOrbit), Distance = MoonsMoonDistance, Phase = .6f }).Key;
        var pack = new ZonePack { Radius = radius, Mass = 10000f, Time = 0 };
        foreach (var orbit in new[] { rootOrbit, planetOrbit, moonOrbit, moonsMoonOrbit })
            pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        void Body(CultRecordKey orbit, float mass) =>
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key));
        Body(rootOrbit, 5f);
        Body(planetOrbit, 1f);
        Body(moonOrbit, .5f);
        if (moonsMoon) Body(moonsMoonOrbit, .01f);
        var zone = new Zone(items, Settings(), pack, new GalaxyZone { Name = "Populated", Owner = null }, null);
        return new Scene { Zone = zone, Orbits = new[] { rootOrbit, planetOrbit, moonOrbit, moonsMoonOrbit } };
    }

    [Fact]
    public void ItCoversEveryBodyAtEveryTimeAndIsTheChainSumOfTheOutermost()
    {
        var s = Build(5000f);

        // Worked by hand: the root's offset from the origin, every distance down the chain, the outermost body's Roche limit.
        var expected = length(RootAt) + PlanetDistance + MoonDistance + MoonsMoonDistance + 10f * .01f;
        Assert.Equal(expected, s.Zone.PopulatedRadius, 2);

        var worst = 0f;
        for (var i = 0; i < 50; i++)
        {
            var time = i * 137.0;
            foreach (var body in s.Zone.PlanetInstances.Values)
            {
                var reach = length(s.Zone.OrbitPositionAt(body.BodyData.Orbit.Key, time)) + body.RocheLimit;
                Assert.True(reach <= s.Zone.PopulatedRadius + .01f, "a body's well is outside the populated radius");
                worst = max(worst, reach);
            }
        }
        Assert.True(worst > 0f);
    }

    [Fact]
    public void ItBoundsTheRootOffTheOriginAndEachLevelOfTheChain()
    {
        // Without the moon's moon the outermost is the moon: the bound loses that level's distance and Roche term and no more.
        var s = Build(5000f, moonsMoon: false);

        Assert.Equal(length(RootAt) + PlanetDistance + MoonDistance + 10f * .5f, s.Zone.PopulatedRadius, 2);
    }

    [Theory]
    [InlineData(100f)]
    [InlineData(5000f)]
    [InlineData(90000f)]
    public void ItDoesNotReadPackRadius(float radius)
    {
        var s = Build(radius);

        Assert.Equal(length(RootAt) + PlanetDistance + MoonDistance + MoonsMoonDistance + 10f * .01f, s.Zone.PopulatedRadius, 2);
    }
}
