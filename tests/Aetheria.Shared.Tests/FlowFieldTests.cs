using System;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The global flow as a world fact (campaign aetheria-fauna, cut fauna-flow-field; ruling sim-knows-the-flow; target
// invariants flow-one-owner and flow-step-free). Every test reads Zone.Flow / Zone.FlowScroll, the path a fauna
// class will read, from zones built the way the other ServerShared tests build them.
public sealed class FlowFieldTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-flow-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;

    public FlowFieldTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "flow.cc"), catalogWritable: true);
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

    private static FlowSettings Flow(float amplitude = 15f, float slide = 0f, float swirl = 0f) => new FlowSettings
    {
        GlobalScale = 512f, GlobalAmplitude = amplitude, GlobalScrollSpeed = .025f, Period = 8f,
        SlopeAmplitude = slide, SwirlAmplitude = swirl
    };

    // A zone at `time` (the pack's clock, as a saved zone restores it), flat unless `depth` digs the zone's own well.
    private Zone ZoneWith(FlowSettings flow, double time = 0, float depth = 0f) => new Zone(_items,
        new PlanetSettings { Flow = flow, ZoneDepth = depth, ZoneDepthExponent = 2f },
        new ZonePack { Time = time }, new GalaxyZone { Name = "Flow Zone", Owner = null }, null);

    private static readonly float3[] Probes =
    {
        new float3(0, 0, 0), new float3(37.5f, 12f, -80.25f), new float3(-410f, -30f, 125f),
        new float3(1234f, 50f, 987f), new float3(-2501f, 7f, -3333f), new float3(9000f, 90f, -4000f)
    };

    private static void AssertClose(float3 expected, float3 actual, float tolerance = 1e-3f)
    {
        Assert.True(abs(expected.x - actual.x) <= tolerance && abs(expected.y - actual.y) <= tolerance &&
                    abs(expected.z - actual.z) <= tolerance, $"expected {expected} but flow was {actual}");
    }

    [Fact]
    public void ScrollIsSpeedTimesZoneTime()
    {
        // The flow at zone time t is the flow at time 0 of a field moved along y by -scale * speed * t, so the
        // noise scrolls by speed x zone time and nothing else (an odd t keeps the shift off a whole field period).
        const double t = 7.0;
        var flow = Flow();
        var later = ZoneWith(flow, t);
        var start = ZoneWith(flow, 0);
        var shift = flow.GlobalScale * flow.GlobalScrollSpeed * (float) t;
        foreach (var p in Probes)
            AssertClose(start.Flow(new float3(p.x, p.y - shift, p.z)), later.Flow(p));
        Assert.Equal(flow.GlobalScrollSpeed * t, later.FlowScroll, 6);
        Assert.Equal(0f, start.FlowScroll);
    }

    [Fact]
    public void FlowDoesNotDependOnStepSize()
    {
        var fine = ZoneWith(Flow());
        var coarse = ZoneWith(Flow());
        for (var i = 0; i < 60; i++) fine.Update(1f / 60f);
        for (var i = 0; i < 7; i++) coarse.Update(1f / 7f);
        Assert.Equal(1.0, fine.Time, 4);
        Assert.Equal(1.0, coarse.Time, 4);
        foreach (var p in Probes) AssertClose(fine.Flow(p), coarse.Flow(p));
        Assert.Equal(fine.FlowScroll, coarse.FlowScroll, 5);
        // The flow moved: a scroll of 0.025 per second is a different field from the start.
        Assert.NotEqual(ZoneWith(Flow()).Flow(Probes[1]).y, fine.Flow(Probes[1]).y);
    }

    [Fact]
    public void ZeroAmplitudesGiveZeroFlowAndNoNaN()
    {
        // All amplitudes zero (and a zero scale a bad config could carry): no term runs, so nothing divides by zero.
        var none = ZoneWith(new FlowSettings());
        foreach (var p in Probes) Assert.Equal(float3.zero, none.Flow(p));

        // A zone built from default PlanetSettings has no flow, the baseline every other ServerShared test relies on.
        var defaults = new Zone(_items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Default", Owner = null }, null);
        foreach (var p in Probes) Assert.Equal(float3.zero, defaults.Flow(p));

        // A term with zero amplitudes is skipped outright: not even a gradient that cannot be normalised reaches it.
        var unusable = float2(float.PositiveInfinity, float.NaN);
        Assert.Equal(float3.zero, FlowField.Slope(new FlowSettings(), unusable));

        // A flat zone has a zero gradient; swirl and slide are zero there, never NaN.
        var flat = ZoneWith(Flow(amplitude: 0f, slide: 1f, swirl: 1f));
        foreach (var p in Probes)
        {
            var f = flat.Flow(p);
            Assert.False(float.IsNaN(f.x) || float.IsNaN(f.y) || float.IsNaN(f.z), "zero gradient gave NaN");
            Assert.Equal(float3.zero, f);
        }
    }

    [Fact]
    public void SlopeTermSwirlsAndSlidesOnTheGradient()
    {
        // The zone's own well is deepest at the origin: downhill at p is toward it.
        var at = new float3(500f, 3f, 300f);
        var downhill = normalize(float2(-at.x, -at.z));

        var slide = ZoneWith(Flow(amplitude: 0f, slide: 1.5f), depth: 64f).Flow(at);
        Assert.Equal(0f, slide.y, 4);
        Assert.Equal(1.5f * downhill.x, slide.x, 2);
        Assert.Equal(1.5f * downhill.y, slide.z, 2);

        // Swirl is perpendicular to downhill, and has a handedness: (down.y, -down.x).
        var swirl = ZoneWith(Flow(amplitude: 0f, swirl: 2f), depth: 64f).Flow(at);
        Assert.Equal(0f, swirl.y, 4);
        Assert.Equal(2f * downhill.y, swirl.x, 2);
        Assert.Equal(-2f * downhill.x, swirl.z, 2);
        Assert.Equal(0f, dot(float2(swirl.x, swirl.z), downhill), 2);
        Assert.Equal(2f, length(float2(swirl.x, swirl.z)), 2);
    }

    [Fact]
    public void ZoneFlowReadsItsPlanetSettings()
    {
        var one = ZoneWith(Flow(amplitude: 15f), 3.0);
        var two = ZoneWith(Flow(amplitude: 30f), 3.0);
        foreach (var p in Probes)
        {
            var a = one.Flow(p);
            Assert.NotEqual(float3.zero, a);
            AssertClose(a * 2f, two.Flow(p));
        }
    }
}
