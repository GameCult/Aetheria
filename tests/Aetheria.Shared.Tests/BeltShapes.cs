using System.Linq;
using GameCult.Caching;

// The belt shapes a test reads back from a built zone. A swarm's rail is a copy of its host's rail (same Parent, Distance and
// Phase), never a slot Place laid out, so the tests that walk placed orbits leave copied rails out through this one helper.
public static class BeltShapes
{
    public static bool CopiedRail(Zone zone, CultRecordKey orbit)
    {
        if (!zone.Planets.Values.Any(b => b is AsteroidBeltData && b.Orbit.Key.Equals(orbit))) return false;
        var rail = zone.Orbits[orbit].Data;
        return zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => zone.Orbits[b.Orbit.Key].Data)
            .Any(o => o.Parent.Key.Equals(rail.Parent.Key) && o.Distance == rail.Distance && o.Phase == rail.Phase);
    }
}
