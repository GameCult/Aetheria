/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using static CultMath.math;

// Pure presentation statics over sim state, never called by ServerShared, with no state of their own: the sim owns every
// pose and no presenter stores one, a previous state or a velocity of its own.
// The rule: a presenter draws every moving thing at the drawn time T = Zone.ExactTime + Clock.Lead. A pose the sim defines
// in closed form of time (an orbit, a belt chunk, a well, the field's height, the flow scroll) is that function evaluated
// at T; a pose the sim integrates (a ship, a kinematic body) is its last step plus its step velocity times Lead; a child
// is drawn where its parent is. DrawAhead.Time is the one definition of T. Ships ride the field at that drawn xz and T (a
// ship's height is the field's there, not the last step's). A ballistic round's flight is presentation
// (ruling projectile-flight-is-presentation), drawn from the sim's facts: its frozen line, the published outcome, the
// commit tick, the impact cell on the target's drawn pose, the burst point.
public static class DrawAhead
{
    // The drawn time: the one definition of T.
    public static double Time(Zone zone, float lead) => zone.ExactTime + lead;

    // An orbit's node at the drawn time: the rail law at T, not the step's memo.
    public static float2 Orbit(Zone zone, CultRecordKey orbit, float lead) => zone.OrbitPositionAt(orbit, Time(zone, lead));

    // The field's height at a point at the drawn time, its wells where their rails put them at T.
    public static float Height(Zone zone, float2 planar, float lead) => zone.GetHeight(planar, Time(zone, lead));

    // The drawn height of a floater (loot, mines): the field at the drawn time plus the floater layer.
    public static float FloaterHeight(Zone zone, GameplaySettings settings, float2 planar, float lead) => Height(zone, planar, lead) + settings.FloaterLayerHeight;

    // A belt rock's drawn position (rulings asteroids-on-floater-layer, asteroid-tops-at-layer): its planar pose at the drawn time, its top on
    // the floater layer over the field, so its centre is a drawn radius below it. pose.w is that radius because the renderer scales each mesh by
    // RockScale(pose.w, the mesh's own measured radius). The renderer's belt loop and the reticle both call this; nothing else derives a rock height.
    public static float3 ChunkAt(Zone zone, GameplaySettings settings, float4 pose, float lead) =>
        float3(pose.x, FloaterHeight(zone, settings, pose.xy, lead) - pose.w, pose.y);

    public static float3 Chunk(Zone zone, GameplaySettings settings, ChunkId chunk, float lead) =>
        ChunkAt(zone, settings, zone.ChunkPose(chunk.Field, chunk.Index, Time(zone, lead)), lead);

    // The scale that draws a mesh of the given measured radius (its farthest vertex from the mesh origin, a bound under any rotation) at the
    // rock's gameplay radius (ruling rock-meshes-by-size). A radius that is not set is refused by name, never divided by.
    public static float RockScale(float gameplayRadius, float meshRadius) =>
        meshRadius > 0f ? gameplayRadius / meshRadius : throw new System.ArgumentException("InstancedMesh.Radius is not set (zero, negative or not a number): run RockMeshRadius.Fill.");

    // One rock's entry in a belt's draw list: the rock (its index in the belt) and the scale that draws it at its gameplay radius on the mesh it is listed under.
    public readonly struct RockDraw
    {
        public readonly int Rock;
        public readonly float Scale;
        public RockDraw(int rock, float scale) { Rock = rock; Scale = scale; }
    }

    // The whole per-rock draw list of a belt (ruling rock-meshes-by-size); the renderer decides nothing and indexes nothing. Meshes are the kept
    // candidates ranked ascending by radius, and ByMesh[m] lists the rocks drawn on Meshes[m], so a renderer batching by mesh iterates the two together.
    // Rocks ranked by unworn size split into one run per mesh, smallest rocks on the smallest mesh and the last run taking the remainder; equal sizes and
    // equal radii rank by index, and a mesh lists its rocks by index. Unworn, so a mined rock keeps its mesh and only shrinks.
    // A list starts with every Scale NaN; Draw fills each entry's Scale from the poses (pose.w is the rock's gameplay radius) and returns the same arrays every call.
    public sealed class BeltDraw<T>
    {
        private readonly float[] _radii;
        private readonly int[][] _rocks;

        public T[] Meshes { get; }
        public RockDraw[][] ByMesh { get; }

        public BeltDraw(IReadOnlyList<float> unwornSizes, IReadOnlyList<T> candidates, System.Func<T, float> radius)
        {
            var radii = candidates.Select(radius).ToArray();
            for (var m = 0; m < radii.Length; m++)
                if (!(radii[m] > 0f)) throw new System.ArgumentException($"InstancedMesh.Radius of mesh #{m} is not set (zero, negative or not a number): run RockMeshRadius.Fill.");
            var meshByRank = Enumerable.Range(0, radii.Length).OrderBy(m => radii[m]).ThenBy(m => m).ToArray();
            _radii = meshByRank.Select(m => radii[m]).ToArray();
            Meshes = meshByRank.Select(m => candidates[m]).ToArray();
            var rockByRank = Enumerable.Range(0, unwornSizes.Count).OrderBy(i => unwornSizes[i]).ThenBy(i => i).ToArray();
            var run = System.Math.Max(1, unwornSizes.Count / radii.Length);
            _rocks = Enumerable.Range(0, radii.Length).Select(m => rockByRank
                    .Where((_, rank) => System.Math.Min(rank / run, radii.Length - 1) == m).OrderBy(i => i).ToArray()).ToArray();
            ByMesh = _rocks.Select(r => r.Select(rock => new RockDraw(rock, float.NaN)).ToArray()).ToArray();
        }

        public RockDraw[][] Draw(IReadOnlyList<float4> poses)
        {
            for (var m = 0; m < _rocks.Length; m++)
                for (var k = 0; k < _rocks[m].Length; k++)
                    ByMesh[m][k] = new RockDraw(_rocks[m][k], RockScale(poses[_rocks[m][k]].w, _radii[m]));
            return ByMesh;
        }
    }

    public static BeltDraw<T> DrawListOf<T>(AsteroidBelt belt, PlanetSettings settings, IReadOnlyList<T> meshes, System.Func<T, float> radius) =>
        new(Enumerable.Range(0, belt.Data.Asteroids.Length).Select(i => belt.UndamagedSize(i, settings)).ToArray(), meshes, radius);

    // A ship in a wormhole animation has its pose scripted by the step, not integrated from Velocity and TurnRate,
    // so it is drawn where the step put it: extrapolating a scripted pose draws a streak that resets every step.
    // A child is drawn where its parent is drawn, as the step copies the parent's pose onto it.
    public static float3 Position(Entity entity, float lead)
    {
        if (entity is Ship { WormholeAnimationInProgress: true }) return entity.Position;
        if (entity.Parent != null) return Position(entity.Parent, lead);
        // A body on a rail (a station) is on its orbit at T: the rail is closed form, not integrated.
        var planar = entity is OrbitalEntity { OrbitData: var orbit } && orbit.IsSet()
            ? Orbit(entity.Zone, orbit, lead)
            : entity.Position.xz + entity.Velocity * lead;
        return float3(planar.x, entity.RideHeight(planar, Time(entity.Zone, lead)), planar.y);
    }

    // The point `distance` out along a planar bearing from the ship's drawn position: where the HUD anchors a marker to the
    // ship the camera follows (an incoming lock, a remembered contact, a ping's flash).
    public static float3 AtBearing(Entity origin, float2 bearing, float distance, float lead)
    {
        var at = Position(origin, lead);
        return float3(at.x + bearing.x * distance, at.y, at.z + bearing.y * distance);
    }

    // A free body (a vapour cloud) drawn where the frame falls lead sim seconds after the last step, by the velocity
    // that step moved it with: the launch velocity and the drift together.
    public static float2 Position(KinematicBody body, float lead) => body.Position + body.TotalVelocity * lead;

    // The ship's rotation turned about its own up axis by the rate its last step turned, for lead seconds.
    public static quaternion Rotation(Ship ship, float lead) =>
        ship.WormholeAnimationInProgress ? ship.Rotation : Turned(ship.Rotation, ship.TurnRate * lead);

    // q turned about its own up axis by the angle, in the sense Direction turns for a positive TurnRate.
    public static quaternion Turned(quaternion q, float angle)
    {
        var u = float3(q.x, q.y, q.z);
        var up = float3(0, 1, 0);
        up += 2 * (q.w * cross(u, up) + cross(u, cross(u, up)));
        var half = angle * .5f;
        var s = sin(half);
        var t = new quaternion(up.x * s, up.y * s, up.z * s, cos(half));
        // Hamilton product t * q: CultMath carries no quaternion product (a gap in CultMath, not filled here).
        return normalize(new quaternion(
            t.w * q.x + t.x * q.w + t.y * q.z - t.z * q.y,
            t.w * q.y - t.x * q.z + t.y * q.w + t.z * q.x,
            t.w * q.z + t.x * q.y - t.y * q.x + t.z * q.w,
            t.w * q.w - t.x * q.x - t.y * q.y - t.z * q.z));
    }

    // A ballistic round at the drawn time (DrawAhead.Time, passed in by the caller; the target's lead is that time less its zone's step time, derived here, never a second input): on its frozen line while the outcome is unknown or a
    // miss, with the barrel's offset from that line decaying to nothing over blendTime sim seconds from the moment of firing.
    // A round the sim published as a hit or burst steers from its line, from the commit tick, onto where it ends by the
    // time damage lands: a burst's frozen point, else the impact cell on the target's drawn pose; and is held there after.
    public static float3 Round(in PendingShot shot, ShotOutcome known, float3 barrel, float blendTime, double drawnTime)
    {
        var time = (float) drawnTime;
        var drawn = Lifted(shot, Line(shot, known?.Result, time), barrel, blendTime, time);
        if (known == null || known.Result == ShotResult.Miss) return drawn;
        var end = known.HasBurstPoint
            ? float3(known.BurstPoint.x, shot.FireOrigin.y, known.BurstPoint.y)
            : Impact(known.Target, known.Cell, (float) (drawnTime - known.Target.Zone.ExactTime));
        var commit = shot.ArrivalTime - known.ArrivalIn;
        var steer = shot.ArrivalTime > commit ? saturate((time - commit) / (shot.ArrivalTime - commit)) : 1f;
        return drawn + (end - drawn) * steer;
    }

    // The impact cell's centre on the target's drawn pose.
    public static float3 Impact(Entity target, int2 cell, float lead)
    {
        var world = target.ToWorldPoint((float2) cell);
        return Carried(target, float3(world.x, target.Position.y, world.y), lead);
    }

    // A world point on the target's sim pose, moved and turned with the target for lead seconds exactly as Position and
    // Rotation draw the entity: the one owner of where a point on a hull is drawn, for the drawn round's end and for
    // impact presenters.
    public static float3 Carried(Entity target, float3 simPoint, float lead)
    {
        var at = Position(target, lead);
        var offset = float2(simPoint.x - target.Position.x, simPoint.z - target.Position.z);
        var turned = mul(offset, CultMath.float2x2.Rotate(Turn(target, lead)));
        return float3(at.x + turned.x, at.y + (simPoint.y - target.Position.y), at.z + turned.y);
    }

    // Whether the drawn flight is over by time: only a published miss ends by the clock. A hit or burst ends on ShotResolved.
    public static bool RoundOver(in PendingShot shot, ShotOutcome known, double drawnTime) =>
        known != null && known.Result == ShotResult.Miss && (float) drawnTime >= RoundEnd(shot, ShotResult.Miss);

    // The sim time a round's drawn line ends, for what is known of its outcome. A round that hit or burst (or whose outcome
    // is not yet published) ends at its ArrivalTime; a round the sim has published as a Miss flies on to the weapon's frozen
    // range and never ends short of its own arrival (a direct round's FireRange is measured to any target, in range or not).
    public static float RoundEnd(in PendingShot shot, ShotResult? known) =>
        known == ShotResult.Miss
            ? max(shot.ArrivalTime, shot.FireTime + (shot.Speed > .01f ? shot.MaxRange / shot.Speed : 0f))
            : shot.ArrivalTime;

    // The frozen line a round follows until it steers: from its frozen origin along its frozen direction at its frozen
    // speed, held at RoundEnd once it has flown that far. With no outcome known the round is drawn no further than its
    // arrival point and waits there for the sim to publish one.
    private static float2 Line(in PendingShot shot, ShotResult? known, float time)
    {
        if (shot.Speed <= .01f) return shot.FireOrigin.xz;
        var flown = clamp(time - shot.FireTime, 0f, RoundEnd(shot, known) - shot.FireTime);
        return shot.FireOrigin.xz + shot.TravelDirection * (shot.Speed * flown);
    }

    private static float Turn(Entity entity, float lead) =>
        entity is Ship { WormholeAnimationInProgress: true } ? 0f : entity.TurnRate * lead;

    private static float3 Lifted(in PendingShot shot, float2 planar, float3 barrel, float blendTime, float time)
    {
        var blend = blendTime > 0 ? saturate(1 - (time - shot.FireTime) / blendTime) : 0f;
        return float3(planar.x, shot.FireOrigin.y, planar.y) + (barrel - shot.FireOrigin) * blend;
    }
}
