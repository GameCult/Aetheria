using CultMath;
using GameCult.Caching;

// The one fixture that gives a test zone a slope: a body on a parentless orbit (the origin unless `at` says otherwise; call again for more bodies), with the
// curves a body needs. Gravity is the sum of body wells (GravityField), so a test that wants ground to slide on
// digs it here and nowhere else. `strength` is the well's c (u^2/s^2) and `coreRadius` its softened core in u, which is
// the Roche limit curve (the one curve of a body's core).
internal static class WellFixture
{
    public static void Dig(CultCache cache, ZonePack pack, PlanetSettings settings, float strength, float coreRadius, float2 at = default)
    {
        settings.BodyRadius = new ExponentialCurve();
        settings.WellStrength = new ExponentialCurve { Constant = strength };
        settings.RocheLimit = new ExponentialCurve { Constant = coreRadius };
        var orbit = cache.Upsert(new OrbitData { FixedPosition = at }).Key;
        var body = cache.Upsert(new PlanetData { Name = $"Well {at.x},{at.y}", Orbit = new CultRecordRef<OrbitData>(orbit) }).Key;
        pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        pack.Planets.Add(new CultRecordRef<BodyData>(body));
    }
}
