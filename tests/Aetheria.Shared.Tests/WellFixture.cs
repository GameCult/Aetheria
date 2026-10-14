using CultMath;
using GameCult.Caching;

// The one fixture that gives a test zone a slope: a single body on a parentless orbit at the origin, with
// the curves a body needs. Gravity height is the sum of body wells, so a test that wants ground to slide on
// digs it here and nowhere else. `radius` is the zone radius the well is sized to (its well reaches 2 x radius,
// as the former bowl did); the well is deepest at the centre and zero at and beyond half its radius.
internal static class WellFixture
{
    public static void Dig(CultCache cache, ZonePack pack, PlanetSettings settings, float radius, float depth, float exponent)
    {
        settings.BodyRadius = new ExponentialCurve();
        settings.OrbitPeriod = new ExponentialCurve();
        settings.GravityRadius = new ExponentialCurve { Constant = 2f * radius };
        settings.GravityDepth = new ExponentialCurve { Constant = depth };
        var orbit = cache.Upsert(new OrbitData { FixedPosition = float2.zero }).Key;
        var body = cache.Upsert(new PlanetData { Orbit = new CultRecordRef<OrbitData>(orbit), GravityDepthExponent = exponent }).Key;
        pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        pack.Planets.Add(new CultRecordRef<BodyData>(body));
    }
}
