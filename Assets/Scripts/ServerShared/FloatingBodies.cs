/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// A free-floating body in a zone: a floating item or a mine. Zone-scoped and never reused within a zone;
// runtime state only, never saved.
public readonly struct FloatingBodyId : IEquatable<FloatingBodyId>
{
    public readonly uint Value;
    public FloatingBodyId(uint value) => Value = value;
    public bool Equals(FloatingBodyId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is FloatingBodyId other && Equals(other);
    public override int GetHashCode() => (int) Value;
    public override string ToString() => Value.ToString();
}

// The one drift integrator of every free body in a zone. A launch velocity decays on its own drag; the zone's
// force accumulates into a separate drift velocity that has its own drag, so a body settles to
// force * gravity / drag. Engine-free: the caller supplies dt and the zone force at the body's position.
public struct KinematicBody
{
    public float2 Position;
    // The launch velocity, decaying.
    public float2 Velocity;
    public float2 Drift;

    // What a presenter draws ahead with.
    public float2 TotalVelocity => Velocity + Drift;

    public void Step(float dt, float2 force, GameplaySettings s)
    {
        Velocity *= exp(-s.FloatingBodyLaunchDrag * dt);
        Drift += force * s.FloatingBodyGravity * dt;
        Drift *= exp(-s.FloatingBodyDrag * dt);
        Position += (Velocity + Drift) * dt;
    }
}
