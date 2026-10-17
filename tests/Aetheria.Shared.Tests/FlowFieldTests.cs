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
        _cache = AetheriaStores.Open(Path.Combine(_root, "flow.cc"), Path.Combine(_root, "flow-run.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        _cache.FlushAsync().Wait();
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Color = new float3(1, 1, 1) } },
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

    // A zone at `time` (the pack's clock, as a saved zone restores it), flat unless `strength` digs the zone's own well.
    private Zone ZoneWith(FlowSettings flow, double time = 0, float strength = 0f)
    {
        var planets = new PlanetSettings { Flow = flow };
        var pack = new ZonePack { Time = time };
        if (strength > 0f) WellFixture.Dig(_items.ItemData, pack, planets, strength, 100f);
        return new Zone(_items, planets, pack, new GalaxyZone { Name = "Flow Zone", Owner = null }, null);
    }

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
        Assert.Equal(flow.GlobalScrollSpeed * t, later.FlowScroll.x, 6);
        Assert.Equal(float2(0f, 0f), start.FlowScroll);
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
        Assert.Equal(fine.FlowScroll.x, coarse.FlowScroll.x, 5);
        Assert.Equal(fine.FlowScroll.y, coarse.FlowScroll.y, 5);
        // The flow moved: a scroll of 0.025 per second is a different field from the start, somewhere.
        var start = ZoneWith(Flow());
        Assert.Contains(Probes, p => length(start.Flow(p) - fine.Flow(p)) > .05f);
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

    // Zone.Flow's slope term reads the owner's force at the point: two equal wells 600 apart (two subzones, each felt within
    // its reach of 300), probes inside each off the axis, where the direction is not along the axis.
    [Fact]
    public void TheSlopeTermFollowsTheSuperposedForce()
    {
        var planets = new PlanetSettings { Flow = Flow(amplitude: 0f, slide: 1.5f) };
        var pack = new ZonePack();
        WellFixture.Dig(_items.ItemData, pack, planets, 300f, 100f, new float2(-300f, 0f));
        WellFixture.Dig(_items.ItemData, pack, planets, 300f, 100f, new float2(300f, 0f));
        var zone = new Zone(_items, planets, pack, new GalaxyZone { Name = "Two Wells", Owner = null }, null);
        var checkedPoints = 0;
        foreach (var at in new[] { float2(-200f, 150f), float2(-350f, -120f), float2(250f, 220f), float2(420f, -90f) })
        {
            var flow = zone.Flow(float3(at.x, 3f, at.y));
            var expected = 1.5f * normalize(zone.GetForce(at));
            Assert.Equal(expected.x, flow.x, 3);
            Assert.Equal(expected.y, flow.z, 3);
            checkedPoints++;
        }
        Assert.Equal(4, checkedPoints);
    }

    [Fact]
    public void SlopeTermSwirlsAndSlidesOnTheGradient()
    {
        // The zone's own well is deepest at the origin: downhill at p is toward it.
        var at = new float3(500f, 3f, 300f);
        var downhill = normalize(float2(-at.x, -at.z));

        var slide = ZoneWith(Flow(amplitude: 0f, slide: 1.5f), strength: 300f).Flow(at);
        Assert.Equal(0f, slide.y, 4);
        Assert.Equal(1.5f * downhill.x, slide.x, 2);
        Assert.Equal(1.5f * downhill.y, slide.z, 2);

        // Swirl is perpendicular to downhill, and has a handedness: (down.y, -down.x).
        var swirl = ZoneWith(Flow(amplitude: 0f, swirl: 2f), strength: 300f).Flow(at);
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
        foreach (var p in Probes) AssertClose(one.Flow(p) * 2f, two.Flow(p));
        // The flow is live (the origin is a legitimate zero of the field, so it is not every probe).
        Assert.Contains(Probes, p => length(one.Flow(p)) > 1f);
    }

    [Fact]
    public void ZoneFlowIsTheLawAtTheScrollTheShaderGets()
    {
        // Zone.Flow and the lowered scroll are one zone-time value: the flow at p is the law evaluated at the zone's
        // own FlowScroll, after any run of steps (a flow that ran ahead of or behind the shader's scroll differs here).
        var zone = ZoneWith(Flow(), 3.25);
        for (var step = 0; step < 90; step++)
        {
            zone.Update(1f / 60f);
            foreach (var p in Probes) AssertClose(FlowField.Global(Flow(), p, zone.FlowScroll), zone.Flow(p), 1e-4f);
        }
    }

    [Fact]
    public void PausedSimFreezesTheFlowPhases()
    {
        // Presenters take their cycle phase from zone time: a zone that does not advance does not move them, and a
        // zone that does moves them by dt / period (mod 1).
        const float period = 8f;
        var zone = ZoneWith(Flow(), 5.0);
        var before = zone.CyclePhase(period);
        var scroll = zone.FlowScroll;
        zone.Update(0f);
        Assert.Equal(before, zone.CyclePhase(period));
        Assert.Equal(scroll, zone.FlowScroll);
        zone.Update(2f);
        Assert.Equal(frac(before + 2f / period), zone.CyclePhase(period), 5);
        Assert.InRange(zone.CyclePhase(period), 0f, .9999999f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void ScrollKeepsAdvancingAtDaysOfZoneTime(int days)
    {
        // Zone time persists and only grows. A float of speed x time stops resolving a 1/60 s step after days; the
        // phases the shader gets stay in [0, 1) and advance by speed x dt on every step.
        const double dt = 1.0 / 60.0;
        var flow = Flow();
        var zone = ZoneWith(flow, days * 86400.0 + .1);
        var previous = zone.FlowScroll;
        for (var step = 0; step < 30; step++)
        {
            zone.Update((float) dt);
            var now = zone.FlowScroll;
            Assert.InRange(now.x, 0f, .9999999f);
            Assert.InRange(now.y, 0f, .9999999f);
            var dx = now.x - previous.x;
            var dy = now.y - previous.y;
            if (dx < -.5f) dx += 1f;
            if (dy < -.5f) dy += 1f;
            var expected = flow.GlobalScrollSpeed * dt;
            Assert.InRange((double) dx, expected - 5e-7, expected + 5e-7);
            Assert.InRange((double) dy, expected * 1.61803398875 - 5e-7, expected * 1.61803398875 + 5e-7);
            previous = now;
        }
    }

    // The law as Volumetric.cginc spelled it before the per-octave fold: Tri3D(p) with p * Golden for the second
    // octave and p = pos / scale - (0, speed * t, 0), in double with the unfolded scroll. The oracle the fold must
    // agree with; it shares no code with FlowField.
    // The origin is a degenerate point of the field (a zero cross product), where no law can be compared.
    private static readonly float3[] OffOrigin = Probes[1..];

    private static double OTri(double x) => Math.Abs(x - Math.Floor(x) - .5);
    private static double[] OTri3(double x, double y, double z) => new[] { OTri(z + OTri(y)), OTri(z + OTri(x)), OTri(y + OTri(x)) };

    private static double[] ONormalize(double[] v)
    {
        var l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        return new[] { v[0] / l, v[1] / l, v[2] / l };
    }

    private static double[] UnfoldedLaw(FlowSettings f, float3 p, double t)
    {
        double g = 1.61803398875f;
        var s = (double) f.GlobalScrollSpeed * t;
        double x = p.x / (double) f.GlobalScale, y = p.y / (double) f.GlobalScale - s, z = p.z / (double) f.GlobalScale;
        var t1 = ONormalize(OTri3(x, y, z));
        var t2 = ONormalize(OTri3(x * g, y * g, z * g));
        var c = new[] { t1[1] * t2[2] - t1[2] * t2[1], t1[2] * t2[0] - t1[0] * t2[2], t1[0] * t2[1] - t1[1] * t2[0] };
        return new[] { c[0] * f.GlobalAmplitude, c[1] * f.GlobalAmplitude * .5, c[2] * f.GlobalAmplitude };
    }

    private static double Distance(float3 a, double[] b) =>
        Math.Sqrt((a.x - b[0]) * (a.x - b[0]) + (a.y - b[1]) * (a.y - b[1]) + (a.z - b[2]) * (a.z - b[2]));

    [Fact]
    public void FoldedScrollIsTheUnfoldedLawAtAnyAge()
    {
        // fold-law: the phase each octave gets is the unfolded speed x time (second octave: x Golden), folded once in
        // double. Computing the second from the folded first (frac(frac(s) x Golden)) agrees below 40 s and nowhere after.
        var f = Flow();
        foreach (var t in new[] { 0.0, 3.1, 39.99, 40.0, 40.01, 86400.0, 864000.0 + .3, 8640000.0 + .7 })
        foreach (var p in OffOrigin)
            Assert.True(Distance(FlowField.Global(f, p, FlowField.Scroll(f, t)), UnfoldedLaw(f, p, t)) / f.GlobalAmplitude < 5e-5,
                $"t={t} p={p}");
    }

    [Fact]
    public void NoJumpWhereAPhaseWraps()
    {
        // Each octave's phase wraps at its own times (the first every 40 s of zone time at speed .025): across a wrap
        // the flow moves by what the unfolded law moves, so nothing jumps.
        var f = Flow();
        double speed = f.GlobalScrollSpeed, golden = 1.61803398875f;
        const double h = 1.0 / 120.0;
        foreach (var (octave, cycles) in new[] { (0, 1.0), (0, 21600.0), (1, 1.0), (1, 34950.0) })
        {
            var t = octave == 0 ? cycles / speed : cycles / (speed * golden);
            var before = FlowField.Scroll(f, t - h);
            var after = FlowField.Scroll(f, t + h);
            Assert.True(octave == 0 ? before.x > .9f && after.x < .1f : before.y > .9f && after.y < .1f,
                $"octave {octave} cycles {cycles}: the probe times do not straddle a wrap");
            foreach (var p in OffOrigin)
            {
                var folded = FlowField.Global(f, p, after) - FlowField.Global(f, p, before);
                var a = UnfoldedLaw(f, p, t + h);
                var b = UnfoldedLaw(f, p, t - h);
                Assert.True(Distance(folded, new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }) / f.GlobalAmplitude < 1e-4,
                    $"octave {octave} cycles {cycles} p={p}");
            }
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public void CycleKeepsAdvancingAtDays(int days)
    {
        // The cycle phase takes zone time in double: a float zone time at 10 days (8.6e5 s) resolves 0.06 s and at
        // 100 days 1 s, so a 1/60 s step would not move the phase.
        foreach (var period in new[] { 8f, .25f })
        {
            const double dt = 1.0 / 60.0;
            var time = days * 86400.0 + .1;
            var previous = FlowField.Cycle(time, period);
            for (var step = 0; step < 30; step++)
            {
                time += dt;
                var now = FlowField.Cycle(time, period);
                var advance = now - previous;
                if (advance < -.5f) advance += 1f;
                Assert.InRange((double) advance, dt / period - 1e-5, dt / period + 1e-5);
                previous = now;
            }
        }
    }

    [Fact]
    public void PhasesAreStrictlyBelowOne()
    {
        // A phase just below an integer rounds to 1f in a float cast; the contract is [0, 1), so it reads 0 instead
        // (1 is 0 modulo 1: the flow there is the flow at 0).
        var f = Flow();
        double speed = f.GlobalScrollSpeed, golden = 1.61803398875f;
        foreach (var gap in new[] { 1e-9, 1e-12 })
        {
            var first = FlowField.Scroll(f, (1.0 - gap) / speed);
            var second = FlowField.Scroll(f, (2.0 - gap) / (speed * golden));
            Assert.InRange(first.x, 0f, .9999999f);
            Assert.InRange(second.y, 0f, .9999999f);
            Assert.InRange(FlowField.Cycle(8.0 * (1.0 - gap), 8f), 0f, .9999999f);
        }
    }
}
