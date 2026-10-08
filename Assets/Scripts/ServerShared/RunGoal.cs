using System.Linq;
using CultMath;
using static CultMath.math;

// The run's goal: whether a zone's boss lives, and so whether the exit gate is open. Engine-free and stateless; the
// answer is derived from Zone.Entities (the death path removes the dead, Zone.cs), so no "boss defeated" flag exists to
// disagree with it, and no presentation decides it.
public static class RunGoal
{
    public static Entity Boss(Zone zone) => zone.Entities.FirstOrDefault(entity => entity.IsBoss);

    public static bool BossAlive(Zone zone) => Boss(zone) != null;

    // The exit gate fails closed: it is open only in the galaxy's exit zone, once a boss was spawned there and no living
    // boss remains. An exit zone that never got a boss stays sealed, so a missing boss can never read as a dead one.
    public static bool ExitOpen(Zone zone) =>
        zone.Galaxy?.Exit != null && zone.GalaxyZone == zone.Galaxy.Exit && zone.Pack.BossSpawned && !BossAlive(zone);

    // Where the exit gate sits: at the adjacency wormholes' radius (the zone's radius times the ratio), in the middle of
    // the widest angular gap between them, so it shares a point with none. A zone with no neighbours gets the +x axis.
    public static float2 ExitGatePosition(Zone zone, float wormholeDistanceRatio)
    {
        var radius = zone.Pack.Radius * wormholeDistanceRatio;
        var angles = zone.GalaxyZone.AdjacentZones
            .Select(adjacent => normalize(adjacent.Position - zone.GalaxyZone.Position))
            .Select(direction => atan2(direction.y, direction.x))
            .OrderBy(angle => angle)
            .ToArray();
        if (angles.Length == 0) return float2(radius, 0);

        var bestAngle = 0f;
        var bestGap = -1f;
        for (var i = 0; i < angles.Length; i++)
        {
            var next = i + 1 < angles.Length ? angles[i + 1] : angles[0] + 2 * PI;
            if (next - angles[i] <= bestGap) continue;
            bestGap = next - angles[i];
            bestAngle = angles[i] + bestGap / 2;
        }
        return float2(cos(bestAngle), sin(bestAngle)) * radius;
    }
}
