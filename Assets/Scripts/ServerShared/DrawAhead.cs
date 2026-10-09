/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using static CultMath.math;

// Where a presenter draws the latest sim state when the frame falls Lead sim seconds after the last step: ships by
// their last step's velocity, ballistic rounds on FireControl's RoundAt and RoundEnd. Pure reads of the simulation for
// presenters (never called by ServerShared, no state of their own): the sim owns every pose and no presenter stores
// one, a previous state or a velocity of its own.
public static class DrawAhead
{
    // A ship in a wormhole animation has its pose scripted by the step, not integrated from Velocity and TurnRate,
    // so it is drawn where the step put it: extrapolating a scripted pose draws a streak that resets every step.
    public static float3 Position(Entity entity, float lead) =>
        entity is Ship { WormholeAnimationInProgress: true }
            ? entity.Position
            : entity.Position + float3(entity.Velocity.x, 0, entity.Velocity.y) * lead;

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

    // A ballistic round at a sim time (the clock's Zone.Time + Lead): on FireControl's line for what the sim has published
    // of its outcome, with the barrel's offset from that line decaying to nothing over blendTime sim seconds from the moment of firing.
    public static float3 Round(in PendingShot shot, ShotResult? known, float3 barrel, float blendTime, float time) =>
        Lifted(shot, FireControl.RoundAt(shot, known, time), barrel, blendTime, time);

    // Where a round that hit or burst stops: the outcome's burst point, else where it arrives on its line.
    public static float3 RoundStop(in PendingShot shot, ShotOutcome outcome, float3 barrel, float blendTime) =>
        Lifted(shot, outcome.HasBurstPoint ? outcome.BurstPoint : FireControl.RoundAt(shot, outcome.Result, shot.ArrivalTime),
            barrel, blendTime, shot.ArrivalTime);

    // Whether the drawn flight is over by time: only a published miss ends by the clock. A hit or burst ends on ShotResolved.
    public static bool RoundOver(in PendingShot shot, ShotResult? known, float time) =>
        known == ShotResult.Miss && time >= FireControl.RoundEnd(shot, ShotResult.Miss);

    private static float3 Lifted(in PendingShot shot, float2 planar, float3 barrel, float blendTime, float time)
    {
        var blend = blendTime > 0 ? saturate(1 - (time - shot.FireTime) / blendTime) : 0f;
        return float3(planar.x, shot.FireOrigin.y, planar.y) + (barrel - shot.FireOrigin) * blend;
    }
}
