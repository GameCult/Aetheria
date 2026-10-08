/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using static CultMath.math;

// Where a presenter draws the latest sim state when the frame falls Lead sim seconds after the last step. Pure reads
// of the simulation for presenters (never called by ServerShared): the sim owns every pose and no presenter stores
// one, a previous state or a velocity of its own.
public static class DrawAhead
{
    public static float3 Position(Entity entity, float lead) =>
        entity.Position + float3(entity.Velocity.x, 0, entity.Velocity.y) * lead;

    // The ship's rotation turned about its own up axis by the rate its last step turned, for lead seconds.
    public static quaternion Rotation(Ship ship, float lead)
    {
        var q = ship.Rotation;
        var u = float3(q.x, q.y, q.z);
        var up = float3(0, 1, 0);
        up += 2 * (q.w * cross(u, up) + cross(u, cross(u, up)));
        var half = ship.TurnRate * lead * .5f;
        var s = sin(half);
        var t = new quaternion(up.x * s, up.y * s, up.z * s, cos(half));
        // Hamilton product t * q: CultMath carries no quaternion product (a gap in CultMath, not filled here).
        return normalize(new quaternion(
            t.w * q.x + t.x * q.w + t.y * q.z - t.z * q.y,
            t.w * q.y - t.x * q.z + t.y * q.w + t.z * q.x,
            t.w * q.z + t.x * q.y - t.y * q.x + t.z * q.w,
            t.w * q.w - t.x * q.x - t.y * q.y - t.z * q.z));
    }

    // Zone time for a presenter that evaluates a sim function of time (FireControl.RoundAt).
    public static float Time(Zone zone, float lead) => zone.Time + lead;
}
