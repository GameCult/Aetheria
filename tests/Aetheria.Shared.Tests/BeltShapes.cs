using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// The belt shapes a test reads back from a built zone. A swarm's or a lane's rail is a copy of its host's rail (same Parent,
// Distance and Phase), never a slot Place laid out, so the tests that walk placed orbits leave copied rails out through this one
// helper.
public static class BeltShapes
{
    public static bool CopiedRail(Zone zone, CultRecordKey orbit)
    {
        if (!zone.Planets.Values.Any(b => b is AsteroidBeltData && b.Orbit.Key.Equals(orbit))) return false;
        var rail = zone.Orbits[orbit].Data;
        return zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => zone.Orbits[b.Orbit.Key].Data)
            .Any(o => o.Parent.Key.Equals(rail.Parent.Key) && o.Distance == rail.Distance && o.Phase == rail.Phase);
    }

    // Every laned belt with its rail and its host: a belt on a copied rail that does not ride the rail (a swarm does), whose host
    // is the body orbit the rail copies.
    public static IEnumerable<(Orbit Host, Orbit Rail, AsteroidBelt Belt)> Lanes(Zone zone)
    {
        foreach (var (key, belt) in zone.AsteroidBelts.Where(b => !b.Value.Data.RidesRail))
        {
            var railKey = zone.Planets[key].Orbit.Key;
            if (!CopiedRail(zone, railKey)) continue;
            var rail = zone.Orbits[railKey];
            var host = zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => zone.Orbits[b.Orbit.Key])
                .Single(o => o.Data.Parent.Key.Equals(rail.Data.Parent.Key) && o.Data.Distance == rail.Data.Distance && o.Data.Phase == rail.Data.Phase);
            yield return (host, rail, belt);
        }
    }
}
