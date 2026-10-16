using System;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// aetheria-gravity, cut well-law: the conserving compact law. GravityField is the law of one band term; WellTree turns a zone's
// orbit tree into terms and walks them. The oracles here are written out from the law (the order-4 smoothstep, Simpson
// integration of the pull in double, merging a family into one body), never by calling the code under test twice.
public sealed class WellLawTests : IDisposable
{
    private readonly ZoneWorlds _worlds = new ZoneWorlds();

    public void Dispose() => _worlds.Dispose();

    // The Plummer pull of a term without a band: r / (r^2 + eps^2).
    private static double Plummer(double eps, double r) => r / (r * r + eps * eps);

    // The order-4 smoothstep, spelled: S(t) = 35 t^4 - 84 t^5 + 70 t^6 - 20 t^7.
    private static double S(double t) => t * t * t * t * (35 - 84 * t + 70 * t * t - 20 * t * t * t);

    private static double Simpson(Func<double, double> f, double from, double to, int n = 4000)
    {
        var h = (to - from) / n;
        var sum = f(from) + f(to);
        for (var i = 1; i < n; i++) sum += f(from + i * h) * (i % 2 == 1 ? 4 : 2);
        return sum * h / 3;
    }

    public static readonly object[][] Terms =
    {
        new object[] { 79.8f, 1000f, 1500f },
        new object[] { 17f, 40f, 60f },
        new object[] { 79.8f, 0f, 1525f },
        new object[] { 1658f, 1000f, 1500f },   // tau 3: the series branch
        new object[] { 59f, 11f, 22f },         // tau 10
        new object[] { 79.8f, float.PositiveInfinity, float.PositiveInfinity }
    };

    // The pull is the slope of the potential: the potential's drop between two radii is the integral of the pull between
    // them, by Simpson in double, for terms on both branches, inside the band, across its edges and beyond it.
    [Theory]
    [MemberData(nameof(Terms))]
    public void TheTermIsItsPotentialsSlope(float eps, float inner, float outer)
    {
        var span = float.IsPositiveInfinity(outer) ? 3000f : outer;
        foreach (var (from, to) in new[] { (.2f, .55f), (.4f, .99f), (.9f, 1.3f), (.05f, 1.0f) })
        {
            float r0 = from * span, r1 = Math.Min(to * span, float.IsPositiveInfinity(outer) ? 6000f : outer * 1.1f);
            var drop = GravityField.Potential(eps, inner, outer, r1) - GravityField.Potential(eps, inner, outer, r0);
            var integral = Simpson(r => GravityField.Pull(eps, inner, outer, (float) r), r0, r1);
            Assert.InRange(drop, integral - 3e-5 - 1e-4 * Math.Abs(integral), integral + 3e-5 + 1e-4 * Math.Abs(integral));
        }
        if (!float.IsPositiveInfinity(outer))
        {
            Assert.Equal(0f, GravityField.Potential(eps, inner, outer, outer));
            Assert.Equal(0f, GravityField.Potential(eps, inner, outer, outer * 1.5f));
            Assert.Equal(0f, GravityField.Pull(eps, inner, outer, outer * 1.5f));
        }
    }

    // The pull is the Plummer pull faded by 1 - S(t) across the band, S the order-4 smoothstep: a smoothstep of order 2 has a
    // different profile, and the profile's first three derivatives vanish at both edges (the deficit goes as t^4).
    [Fact]
    public void ABandEdgeIsSmooth()
    {
        const float eps = 79.8f, a = 1000f, b = 1500f;
        foreach (var t in new[] { 0f, .02f, .05f, .1f, .3f, .5f, .7f, .9f, .98f, 1f })
        {
            var r = (float) Math.Sqrt(a * a + t * (b * b - a * a));
            var pull = GravityField.Pull(eps, a, b, r);
            var expected = Plummer(eps, r) * (1 - S(t));
            Assert.InRange(pull, expected - 1e-5 * Plummer(eps, r), expected + 1e-5 * Plummer(eps, r));
        }
        Assert.Equal((double) GravityField.Pull(eps, a, b, a), Plummer(eps, a), 6);
        Assert.Equal(0f, GravityField.Pull(eps, a, b, b));
        var deficit = new Func<float, double>(t => 1 - GravityField.Pull(eps, a, b, (float) Math.Sqrt(a * a + t * (b * b - a * a))) / Plummer(eps, Math.Sqrt(a * a + t * (b * b - a * a))));
        Assert.InRange(deficit(.1f) / deficit(.05f), 13.0, 15.0); // doubling t multiplies the deficit by about 14 (t^4), not by 4
    }

    // tau = (a^2 + eps^2) / (b^2 - a^2): the closed form holds to tau 1 and the series beyond it, and the two meet; in single
    // precision the tau 10 potential stays within 1e-5 of the double-precision integral of the pull.
    [Fact]
    public void TheSeriesBranchMeetsTheClosedForm()
    {
        // tau 1 +- 1e-6: eps^2 = b^2 - 2 a^2 gives tau 1 exactly.
        const float a = 0.8f, b = 1.28062487f;
        var below = (float) Math.Sqrt(b * b - 2 * a * a) * (1 - 1e-6f);
        var above = (float) Math.Sqrt(b * b - 2 * a * a) * (1 + 1e-6f);
        foreach (var r in new[] { 0.5f, 0.85f, 1.0f, 1.2f })
            Assert.InRange(GravityField.Potential(above, a, b, r) - GravityField.Potential(below, a, b, r), -3e-6f, 3e-6f);

        const float eps = 59f, inner = 11f, outer = 22f;
        foreach (var r in new[] { 12f, 15f, 18f, 21f })
        {
            var oracle = -Simpson(x => GravityField.Pull(eps, inner, outer, (float) x), r, outer);
            Assert.InRange(GravityField.Potential(eps, inner, outer, r), oracle - 1e-5, oracle + 1e-5);
        }
    }

    private static float Plummer(PlanetSettings curves, float mass, float d) =>
        curves.WellStrength.Evaluate(mass) * d / (d * d + curves.RocheLimit.Evaluate(mass) * curves.RocheLimit.Evaluate(mass));

    // A sun, a planet at 3000 and a moon at 150 round the planet, the moon's mass as given.
    private Zone SunPlanetMoon(float moonMass, out CultRecordKey planetOrbit, out CultRecordKey moonOrbit, float planetMass = 100f)
    {
        var w = _worlds.New();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        planetOrbit = w.Orbit(sunOrbit, 3000f);
        w.Body(planetOrbit, planetMass);
        moonOrbit = w.Orbit(planetOrbit, 150f);
        if (moonMass > 0f) w.Body(moonOrbit, moonMass);
        return w.Build();
    }

    // Inside its outermost child a node pulls exactly as its body alone: the family terms cancel there, so a child's rail
    // never feels its own mass through its parent.
    [Fact]
    public void InsideAFamilyItsParentPullsAsItsBodyAlone()
    {
        var curves = ZoneWorlds.Curves();
        foreach (var moonMass in new[] { 1f, 2f })
        {
            var zone = SunPlanetMoon(moonMass, out var planetOrbit, out var moonOrbit);
            foreach (var d in new[] { 20f, 60f, 150f })
            {
                var expected = Plummer(curves, 100f, d);
                Assert.InRange(zone.Wells.PullAround(planetOrbit, d, moonOrbit), expected * (1 - 1e-5f), expected * (1 + 1e-5f));
            }
        }
    }

    // Merging a family into one body of its mass in the same slot changes nothing beyond the family's support, and does
    // inside it.
    [Fact]
    public void AFamilyMergedIntoOneBodyIsTheSameFieldOutsideIt()
    {
        var family = SunPlanetMoon(1f, out var planetOrbit, out _);
        var merged = SunPlanetMoon(0f, out _, out _, planetMass: 101f);
        var support = family.Wells.Support(planetOrbit);
        Assert.True(support > 150f, "fixture: the support clears the moon");
        var planetAt = family.Orbits[planetOrbit].Position;
        foreach (var (distance, angle) in new[] { (1.05f, .3f), (1.4f, 2.2f), (3f, 4.0f), (9f, 1.0f), (20f, 5.5f) })
        {
            var p = planetAt + float2(support * distance * cos(angle), support * distance * sin(angle));
            var f = family.GetForce(p);
            var m = merged.GetForce(p);
            Assert.InRange(length(f - m), 0f, 1e-5f * length(f) + 1e-9f);
            Assert.InRange(abs(family.GetHeight(p) - merged.GetHeight(p)), 0f, 1e-5f * (1f + abs(family.GetHeight(p))));
        }
        var inside = planetAt + float2(100f, 0f);
        Assert.True(length(family.GetForce(inside) - merged.GetForce(inside)) > 1e-3f * length(merged.GetForce(inside)), "inside the support the moon is a body of its own");
    }

    // The root has no reach: far from everything the zone is one body of its total mass, however the generator split it.
    [Fact]
    public void TheZonesFarFieldIsItsTotalMass()
    {
        var curves = ZoneWorlds.Curves();
        var one = SunPlanetMoon(1f, out _, out _);
        var w = _worlds.New();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        for (var i = 0; i < 5; i++) w.Body(w.Orbit(sunOrbit, 800f + 450f * i, i / 5f), 20.2f);
        var five = w.Build();
        foreach (var zone in new[] { one, five })
        {
            var far = 10f * zone.Pack.Radius;
            foreach (var angle in new[] { 0f, 1.7f, 4.1f })
                Near(curves.WellStrength.Evaluate(5101f), length(zone.GetForce(float2(far * cos(angle), far * sin(angle)))) * far, 1e-3f);
        }
    }

    private static void Near(float expected, float actual, float relative) =>
        Assert.InRange(actual, expected * (1 - relative), expected * (1 + relative));

    // A node's reach is half its distance, or one and a half times its support if that is more; the root's is infinite.
    [Fact]
    public void AReachIsHalfTheDistanceOrOneAndAHalfTheSupport()
    {
        var w = _worlds.New();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        var wide = w.Orbit(sunOrbit, 3000f);
        w.Body(wide, 100f);
        var tight = w.Orbit(sunOrbit, 100f);
        w.Body(tight, 100f);
        w.Body(w.Orbit(wide, 150f), 1f);
        w.Body(w.Orbit(tight, 150f), 1f);
        var zone = w.Build();

        Assert.True(float.IsPositiveInfinity(zone.Wells.Reach(sunOrbit)));
        Assert.Equal(1500f, zone.Wells.Reach(wide), 3);
        Assert.Equal(225f, zone.Wells.Support(wide), 3);
        Assert.Equal(337.5f, zone.Wells.Reach(tight), 3);
        Assert.Equal(225f, zone.Wells.Support(tight), 3);
    }

    // A lone body on a bodyless node is pulled by nothing of its own: the node's terms cancel where the ring is.
    [Fact]
    public void ALoneBodyOnABodylessNodeFeelsNoPullOfItsOwn()
    {
        var w = _worlds.New();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        var node = w.Orbit(sunOrbit, 3000f);
        var alone = w.Orbit(node, 200f);
        w.Body(alone, 100f);
        var zone = w.Build();

        Assert.Equal(0f, zone.Wells.PullAround(node, 200f, alone));
        Assert.True(float.IsPositiveInfinity(zone.Orbits[alone].Period));
    }

    // The walk skips a subtree outside its reach, which is exact: at points all over a tree with families, a ring and a belt of
    // moons, the force and potential of the walk equal the flat sum over every term to float rounding of the terms.
    [Fact]
    public void TheWalkIsTheSum()
    {
        var w = _worlds.New();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        for (var i = 0; i < 5; i++)
        {
            var planet = w.Orbit(sunOrbit, 700f + 800f * i, frac(i * .3819f));
            w.Body(planet, 40f + 30f * i);
            for (var j = 0; j < 3; j++) w.Body(w.Orbit(planet, 90f + 70f * j, frac(j * .6180339887f)), 1f + j);
        }
        var node = w.Orbit(sunOrbit, 5200f, .13f);
        w.Body(w.Orbit(node, 300f, 0f), 80f);
        w.Body(w.Orbit(node, 300f, .5f), 60f);
        var zone = w.Build();

        var rng = new System.Random(7);
        for (var n = 0; n < 200; n++)
        {
            var p = float2((float) (rng.NextDouble() * 12000 - 6000), (float) (rng.NextDouble() * 12000 - 6000));
            var force = float2.zero;
            var potential = 0f;
            var scale = 0f;
            foreach (var term in zone.Wells.Terms)
            {
                var offset = zone.Orbits[term.Node].Position - p;
                var r = length(offset);
                var pull = term.Strength * GravityField.Pull(term.Softening, term.Inner, term.Outer, r);
                if (r > 0f) force += offset / r * pull;
                var level = term.Strength * GravityField.Potential(term.Softening, term.Inner, term.Outer, r) - term.Reference;
                potential += level;
                scale += abs(pull) + abs(level);
            }
            Assert.InRange(length(zone.GetForce(p) - force), 0f, 2e-6f * scale + 1e-9f);
            Assert.InRange(abs(zone.GetHeight(p) * zone.Settings.HeightExaggeration - potential), 0f, 2e-6f * scale + 1e-9f);
        }
    }
}
