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

    // The band's potential is the integral of its pull, in single precision, on both sides of the series threshold: tau =
    // (a^2 + eps^2) / (b^2 - a^2) is swept from 0.05 to 10 (every (a, b) where that tau has a real softening), and the
    // potential stays within 1.8e-6 per unit strength of the double-precision Simpson integral of the pull. A closed form kept
    // past tau 0.6 or a series cut short misses this by 1e-5 to 1e-3. Both branches are within the bound between tau 0.3 and 0.6,
    // so the exact seam (SeriesFrom 0.4) inside that interval is pinned by the committed shader being the generated one, not here.
    [Fact]
    public void TheBandPotentialIsExactInSinglePrecision()
    {
        var checkedCases = 0;
        foreach (var tau in new[] { .05, .3, .35, .39, .41, .45, .5, .55, .6, .7, 1, 1.5, 2.5, 10 })
            foreach (var (a, b) in new[] { (1000.0, 1500.0), (40.0, 60.0), (11.0, 22.0), (0.0, 1525.0), (150.0, 4000.0) })
            {
                var e2 = tau * (b * b - a * a) - a * a;
                if (e2 < 0) continue;
                var eps = (float) Math.Sqrt(e2);
                e2 = (double) eps * eps; // the oracle integrates the softening the code is given
                for (var k = 0; k < 15; k++)
                {
                    var r = b * (.02 + .979 * k / 14);
                    var oracle = -Simpson(x =>
                    {
                        var t = x <= a ? 0.0 : (x * x - a * a) / (b * b - a * a);
                        return x / (x * x + e2) * (1 - S(t));
                    }, r, b, 8000);
                    var actual = GravityField.Potential(eps, (float) a, (float) b, (float) r);
                    Assert.InRange(actual, oracle - 1.8e-6, oracle + 1.8e-6);
                    checkedCases++;
                }
            }
        Assert.True(checkedCases > 400, "the sweep ran its cases");
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

    // A sole root has no reach: far from everything the zone is one body of its total mass, however the generator split it.
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

    // Three subzones: a sun of 5000 with a planet, a sun of 900 alone, and a bodyless pair; each is a root (a parentless orbit
    // stands where its distance and phase put it). Masses are the test's own, the zone's total 6100.
    private Zone ThreeSubzones(out CultRecordKey[] roots)
    {
        var w = _worlds.New();
        var big = w.Orbit(default, 3000f, .85f);
        w.Body(big, 5000f);
        w.Body(w.Orbit(big, 800f), 100f);
        var small = w.Orbit(default, 9000f, .3f);
        w.Body(small, 900f);
        var pair = w.Orbit(default, 12000f, .7f);
        w.Body(w.Orbit(pair, 300f, 0f), 60f);
        w.Body(w.Orbit(pair, 300f, .5f), 40f);
        roots = new[] { big, small, pair };
        return w.Build();
    }

    private static readonly float[] ThreeSubzoneMass = { 5100f, 900f, 100f };

    // The zone node sits at the family-mass-weighted mean of its roots' places and pulls the zone's total mass from there.
    [Fact]
    public void AZoneOfSeveralSunsPullsAsItsTotalMassFromTheirBarycentre()
    {
        var zone = ThreeSubzones(out var roots);
        var centre = float2.zero;
        for (var i = 0; i < roots.Length; i++) centre += zone.Orbits[roots[i]].Position * ThreeSubzoneMass[i] / 6100f;
        Assert.InRange(length(zone.Wells.Centre - centre), 0f, 1e-3f);

        // Each subzone reaches half the way to its nearest neighbour, or 1.5 of its support if more.
        for (var i = 0; i < roots.Length; i++)
        {
            var nearest = Enumerable.Range(0, roots.Length).Where(j => j != i).Min(j => length(zone.Orbits[roots[j]].Position - zone.Orbits[roots[i]].Position));
            Assert.Equal(Math.Max(.5f * nearest, 1.5f * zone.Wells.Support(roots[i])), zone.Wells.Reach(roots[i]), 2);
        }

        var far = 10f * zone.Pack.Radius;
        foreach (var angle in new[] { 0f, 2.1f, 4.4f })
            Near(ZoneWorlds.Curves().WellStrength.Evaluate(6100f),
                length(zone.GetForce(zone.Wells.Centre + float2(far * cos(angle), far * sin(angle)))) * far, 1e-3f);
    }

    // One sun with a planet, alone in its zone at the place a root of `distance` and `phase` stands.
    private Zone OneSubzone(float sun, float distance, float phase, out CultRecordKey root)
    {
        var w = _worlds.New();
        root = w.Orbit(default, distance, phase);
        w.Body(root, sun);
        w.Body(w.Orbit(root, 700f, .2f), 120f);
        return w.Build();
    }

    // Isolated subzones: inside its own support a sun is felt exactly as if it were alone in the zone, the other suns and the
    // zone node adding nothing; a root's reach is half the distance to the nearest other sun, or 1.5 of its support if more.
    [Fact]
    public void ASubzoneFeelsOnlyItsOwnSun()
    {
        var w = _worlds.New();
        var a = w.Orbit(default, 0f);
        w.Body(a, 4000f);
        w.Body(w.Orbit(a, 700f, .2f), 120f);
        var b = w.Orbit(default, 9000f, 0f);
        w.Body(b, 2500f);
        w.Body(w.Orbit(b, 700f, .2f), 120f);
        var zone = w.Build();

        var rng = new System.Random(3);
        foreach (var (root, sun, distance) in new[] { (a, 4000f, 0f), (b, 2500f, 9000f) })
        {
            var alone = OneSubzone(sun, distance, 0f, out var aloneRoot);
            Assert.True(float.IsPositiveInfinity(alone.Wells.Reach(aloneRoot)), "a sole massive root is the whole zone");
            var support = zone.Wells.Support(root);
            Assert.Equal(Math.Max(.5f * 9000f, 1.5f * support), zone.Wells.Reach(root), 2);
            var at = zone.Orbits[root].Position;
            for (var i = 0; i < 50; i++)
            {
                var angle = (float) (rng.NextDouble() * 2 * Math.PI);
                var radius = support * (.2f + .8f * (float) rng.NextDouble());
                var p = at + float2(radius * cos(angle), radius * sin(angle));
                var expected = alone.GetForce(p);
                Assert.InRange(length(zone.GetForce(p) - expected), 0f, 1e-5f * length(expected) + 1e-9f);
            }
        }
    }

    // Between the subzones the ground is flat; beyond the populated area the zone is one body of its total mass at its centre.
    [Fact]
    public void BeyondEverySubzoneTheZoneIsOneBody()
    {
        var zone = ThreeSubzones(out var roots);
        var curves = ZoneWorlds.Curves();
        var centre = zone.Wells.Centre;
        var span = roots.Max(r => length(zone.Orbits[r].Position - centre) + zone.Wells.Reach(r)); // A, as the law defines it

        var flat = 0;
        for (var x = -14000f; x <= 14000f; x += 700f)
            for (var y = -14000f; y <= 14000f; y += 700f)
            {
                var p = float2(x, y);
                if (length(p - centre) >= span || roots.Any(r => length(p - zone.Orbits[r].Position) <= zone.Wells.Reach(r))) continue;
                flat++;
                Assert.InRange(length(zone.GetForce(p)), 0f, 1e-7f);
            }
        Assert.True(flat > 100, "fixture: there is open ground between the subzones");

        // The rim: from A to 1.5 A the zone's pull rises from nothing to its whole Plummer pull as the order-4 smoothstep of
        // (d^2 - A^2) / ((1.5 A)^2 - A^2), A being the reach-derived radius above.
        foreach (var (radius, angle) in new[] { (1.05f, .7f), (1.15f, 3.1f), (1.25f, 5.0f), (1.35f, 1.9f), (1.45f, 4.2f) })
        {
            var p = centre + float2(span * radius * cos(angle), span * radius * sin(angle));
            var d = length(centre - p);
            var t = ((double) d * d - (double) span * span) / (2.25 * span * span - (double) span * span);
            var expected = (centre - p) / d * (float) (Plummer(curves, 6100f, d) * S(t));
            Assert.InRange(length(zone.GetForce(p) - expected), 0f, 2e-5f * Plummer(curves, 6100f, d));
        }

        foreach (var (radius, angle) in new[] { (1.6f, .4f), (3f, 2.5f), (8f, 5.1f) })
        {
            var p = centre + float2(span * radius * cos(angle), span * radius * sin(angle));
            var expected = (centre - p) / length(centre - p) * Plummer(curves, 6100f, length(centre - p));
            Assert.InRange(length(zone.GetForce(p) - expected), 0f, 1e-5f * length(expected));
        }
    }

    // A belt is a child of its parent's family: the family's support covers the belt's outermost asteroid, so every asteroid
    // circles the planet's body alone at its own distance (not at the belt orbit's, 43, where the fade used to start).
    [Fact]
    public void ABeltRidesInsideItsParentsBody()
    {
        var w = _worlds.New();
        var sun = w.Orbit(default, 0f);
        w.Body(sun, 5000f);
        var planet = w.Orbit(sun, 3000f);
        w.Body(planet, 172f);
        var belt = w.Belt(w.Orbit(planet, 43f, .4f), 10f, 87f, 43f); // the outermost asteroid is in the middle: neither the first nor the last listed
        var zone = w.Build();

        Assert.Equal(1.5f * 87f, zone.Wells.Support(planet), 3);
        var curves = ZoneWorlds.Curves();
        var periods = zone.AsteroidBelts[belt].Periods;
        Assert.Equal(3, periods.Length);
        for (var i = 0; i < 3; i++)
        {
            var d = zone.AsteroidBelts[belt].Data.Asteroids[i].Distance;
            var expected = 2 * PI * sqrt(d / Plummer(curves, 172f, d));
            Assert.InRange(periods[i], expected * (1 - 1e-5f), expected * (1 + 1e-5f));
        }
    }

    // The fade of a tight family begins at its support: the planet (moon at 150, support 225, reach at its floor 337.5) pulls
    // as the whole family out to 225 and then gives way as the order-4 smoothstep of (r^2 - 225^2) / (337.5^2 - 225^2).
    [Fact]
    public void ATightFamilysPullFadesFromItsSupport()
    {
        var w = _worlds.New();
        var sun = w.Orbit(default, 0f);
        w.Body(sun, 5000f);
        var planet = w.Orbit(sun, 400f);
        w.Body(planet, 100f);
        var moon = w.Orbit(planet, 150f);
        w.Body(moon, 1f);
        var zone = w.Build();
        var curves = ZoneWorlds.Curves();

        Assert.Equal(225f, zone.Wells.Support(planet), 3);
        Assert.Equal(337.5f, zone.Wells.Reach(planet), 3);
        foreach (var r in new[] { 232f, 250f, 270f, 290f, 310f, 325f, 333f, 336f })
        {
            var t = (r * r - 225.0 * 225) / (337.5 * 337.5 - 225.0 * 225);
            var expected = Plummer(curves, 101f, r) * (1 - S(t));
            var scale = Plummer(curves, 101f, r); // float rounding of the four cancelling terms is relative to the Plummer pull
            Assert.InRange(zone.Wells.PullAround(planet, r, moon), expected - 1e-5 * scale, expected + 1e-5 * scale);
        }
    }

    // A wide family fades from Gamma x its reach, not its support: a planet at 2000 with a moon at 100 has support 150 and reach
    // 1000 (half its distance), so it pulls as the whole family out to 500 and gives way from there as the order-4 smoothstep
    // of (r^2 - 500^2) / (1000^2 - 500^2).
    [Fact]
    public void AWideFamilysPullFadesFromGammaTimesItsReach()
    {
        var w = _worlds.New();
        var sun = w.Orbit(default, 0f);
        w.Body(sun, 5000f);
        var planet = w.Orbit(sun, 2000f);
        w.Body(planet, 100f);
        var moon = w.Orbit(planet, 100f);
        w.Body(moon, 1f);
        var zone = w.Build();
        var curves = ZoneWorlds.Curves();

        Assert.Equal(150f, zone.Wells.Support(planet), 3);
        Assert.Equal(1000f, zone.Wells.Reach(planet), 3);
        foreach (var r in new[] { 480f, 520f, 600f, 700f, 800f, 900f, 980f })
        {
            var t = Math.Max(0.0, (r * r - 500.0 * 500) / (1000.0 * 1000 - 500.0 * 500));
            var expected = Plummer(curves, 101f, r) * (1 - S(t));
            var scale = Plummer(curves, 101f, r);
            Assert.InRange(zone.Wells.PullAround(planet, r, moon), expected - 1e-5 * scale, expected + 1e-5 * scale);
        }
    }

    // A ring group's pull rises from its ring: a pair at 300 round a bodyless node pulls from the node as the order-4 smoothstep
    // of (r^2 - 300^2) / (1500^2 - 300^2) of the pair's Plummer, 1500 being the ring's distance and the members' RingReach.
    [Fact]
    public void ARingGroupsPullRisesFromItsRing()
    {
        var w = _worlds.New();
        var sun = w.Orbit(default, 0f);
        w.Body(sun, 5000f);
        var node = w.Orbit(sun, 3000f);
        w.Body(w.Orbit(node, 300f, 0f), 80f);
        w.Body(w.Orbit(node, 300f, .5f), 60f);
        var zone = w.Build();
        var curves = ZoneWorlds.Curves();

        foreach (var r in new[] { 310f, 400f, 600f, 900f, 1200f, 1400f, 1490f })
        {
            var t = (r * r - 300.0 * 300) / (1500.0 * 1500 - 300.0 * 300);
            var expected = Plummer(curves, 140f, r) * S(t);
            var scale = Plummer(curves, 140f, r); // float rounding of the four cancelling terms is relative to the Plummer pull
            Assert.InRange(zone.Wells.PullAround(node, r, default), expected - 1e-5 * scale, expected + 1e-5 * scale);
        }
    }

    // The drawn height is the force's potential everywhere, however far: its slope along the radius is the pull, for a zone of
    // one sun and for one of three subzones, out to ten pack radii (a potential flattened at some radius reads zero here).
    [Fact]
    public void FarOutTheHeightIsTheForcesPotential()
    {
        var single = SunPlanetMoon(1f, out _, out _);
        var several = ThreeSubzones(out _);
        foreach (var (zone, centre) in new[] { (single, float2.zero), (several, several.Wells.Centre) })
            foreach (var radius in new[] { 2f, 4f, 7f, 10f })
                foreach (var angle in new[] { .3f, 2.4f, 4.6f })
                {
                    var outward = float2(cos(angle), sin(angle));
                    var p = centre + outward * (radius * zone.Pack.Radius);
                    const float h = 10f;
                    var slope = (zone.GetHeight(p + outward * h) - zone.GetHeight(p - outward * h)) * zone.Settings.HeightExaggeration / (2 * h);
                    var pull = -dot(zone.GetForce(p), outward);
                    Assert.InRange(slope, pull * .98f, pull * 1.02f);
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
        w.Body(w.Orbit(default, 9000f, .3f), 900f); // a second massive root: the zone node joins the walk
        var zone = w.Build();

        var rng = new System.Random(7);
        for (var n = 0; n < 200; n++)
        {
            var p = float2((float) (rng.NextDouble() * 24000 - 6000), (float) (rng.NextDouble() * 24000 - 12000));
            var force = float2.zero;
            var potential = 0f;
            var scale = 0f;
            foreach (var term in zone.Wells.Terms)
            {
                var offset = (term.Node.IsSet() ? zone.Orbits[term.Node].Position : zone.Wells.Centre) - p; // an unset node is the zone node
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

    // A ring of three on a bodyless node: each member feels both partners' own pulls at their chord (the side of the inscribed
    // triangle), each projected toward the node (cos 30 degrees), so the projection and the chord both decide the period.
    [Fact]
    public void ARosetteMemberFeelsItsPartnersProjectedTowardTheNode()
    {
        var w = _worlds.New();
        var node = w.Orbit(default, 0f);
        var members = new[] { w.Orbit(node, 400f, 0f), w.Orbit(node, 400f, 1f / 3f), w.Orbit(node, 400f, 2f / 3f) };
        var bodies = members.Select(m => w.Body(m, 120f)).ToArray();
        var zone = w.Build();

        var chord = 400f * sqrt(3f);
        var pull = 2f * zone.PlanetInstances[bodies[0]].WellStrength * GravityField.Pull(zone.PlanetInstances[bodies[0]].RocheLimit,
            float.PositiveInfinity, float.PositiveInfinity, chord) * cos(PI / 6f);
        foreach (var member in members)
            Assert.InRange(zone.Orbits[member].Period, 2 * PI * sqrt(400f / pull) * (1 - 1e-4f), 2 * PI * sqrt(400f / pull) * (1 + 1e-4f));
    }
}
