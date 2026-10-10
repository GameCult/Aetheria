/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using static CultMath.math;

// Pure presentation statics over sim state, never called by ServerShared, with no state of their own: the sim owns every
// pose and no presenter stores one, a previous state or a velocity of its own. Ships are drawn where the frame falls Lead
// sim seconds after the last step, by their last step's velocity and turn. A ballistic round's flight is presentation
// (ruling projectile-flight-is-presentation), drawn from the sim's facts: its frozen line, the published outcome, the
// commit tick, the impact cell on the target's drawn pose, the burst point.
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

    // A ballistic round at a sim time (the clock's Zone.Time + Lead): on its frozen line while the outcome is unknown or a
    // miss, with the barrel's offset from that line decaying to nothing over blendTime sim seconds from the moment of firing.
    // A round the sim published as a hit or burst steers from its line, from the commit tick, onto where it ends by the
    // time damage lands: a burst's frozen point, else the impact cell on the target's drawn pose; and is held there after.
    public static float3 Round(in PendingShot shot, ShotOutcome known, float3 barrel, float blendTime, float simTime, float lead)
    {
        var time = simTime + lead;
        var drawn = Lifted(shot, Line(shot, known?.Result, time), barrel, blendTime, time);
        if (known == null || known.Result == ShotResult.Miss) return drawn;
        var end = known.HasBurstPoint
            ? float3(known.BurstPoint.x, shot.FireOrigin.y, known.BurstPoint.y)
            : Impact(known.Target, known.Cell, lead);
        var commit = shot.ArrivalTime - known.ArrivalIn;
        var steer = shot.ArrivalTime > commit ? saturate((time - commit) / (shot.ArrivalTime - commit)) : 1f;
        return drawn + (end - drawn) * steer;
    }

    // The impact cell's centre on the target's drawn pose: the cell at the sim pose, drawn ahead by the target's last
    // step velocity and turned by its last step turn for lead seconds, as Position and Rotation draw the entity.
    public static float3 Impact(Entity target, int2 cell, float lead)
    {
        var at = Position(target, lead);
        var offset = target.ToWorldPoint((float2) cell) - target.Position.xz;
        var turned = mul(offset, CultMath.float2x2.Rotate(Turn(target, lead)));
        return float3(at.x + turned.x, at.y, at.z + turned.y);
    }

    // Whether the drawn flight is over by time: only a published miss ends by the clock. A hit or burst ends on ShotResolved.
    public static bool RoundOver(in PendingShot shot, ShotOutcome known, float simTime, float lead) =>
        known != null && known.Result == ShotResult.Miss && simTime + lead >= RoundEnd(shot, ShotResult.Miss);

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
