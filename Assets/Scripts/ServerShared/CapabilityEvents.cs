/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using UniRx;
using CultMath;

// Capability event contract (docs/headless-playground-cut.md fork L, operator ruling 2026-09-17):
// pickup, impact absorption, melee blade and propulsion are separate item capabilities. Simulation
// (not yet built -- this file only stands up the wire shape) will own each capability's rules and
// emit these events; any number of presentations (the field shield today, later an IFS shatter
// plane or an advected particle field) subscribe to the event kinds they visualize and decide
// nothing about the rule. This file has no engine dependency: it compiles into the headless
// Aetheria.Shared build (see Aetheria.Shared.csproj) with no publisher wired up yet.

// One absorbed impact: a hit landing on the item's shield/armor capability. DamageType rides along
// so presenters can vary visuals per type; the damage-type mask itself belongs to the absorb
// capability's own data and is out of scope for this cut (operator, 2026-09-17) -- this struct only
// carries the hit's existing type through, unfiltered.
public readonly struct AbsorbEvent
{
    public readonly float3 Position;
    public readonly float3 Direction;
    public readonly float Magnitude;
    public readonly DamageType DamageType;

    public AbsorbEvent(float3 position, float3 direction, float magnitude, DamageType damageType)
    {
        Position = position;
        Direction = direction;
        Magnitude = magnitude;
        DamageType = damageType;
    }
}

// A timed grab's lifecycle (docs fork L: "a pickup is a timed grab of a selected item in range:
// extend, envelop, pull"), generalized to any item capability that reaches out and returns with
// something.
public enum GrabPhase
{
    Started,
    Extend,
    Envelop,
    Pull,
    Completed,
    Cancelled
}

// The target is identified by a plain handle, not a Unity type: the simulation will own the real
// target identity (a loot drop or item instance id) once the pickup capability exists. Today's only
// publisher (the FieldShieldTest scene) hands out its own handles; nothing here interprets them.
public readonly struct GrabEvent
{
    public readonly int TargetHandle;
    public readonly GrabPhase Phase;
    public readonly float3 TargetPosition;
    public readonly float PhaseDuration;

    public GrabEvent(int targetHandle, GrabPhase phase, float3 targetPosition, float phaseDuration)
    {
        TargetHandle = targetHandle;
        Phase = phase;
        TargetPosition = targetPosition;
        PhaseDuration = phaseDuration;
    }
}

// One melee swing.
public readonly struct MeleeEvent
{
    public readonly float3 Direction;
    public readonly float Range;
    public readonly float Arc;
    public readonly float Duration;

    public MeleeEvent(float3 direction, float range, float arc, float duration)
    {
        Direction = direction;
        Range = range;
        Arc = arc;
        Duration = duration;
    }
}

// One frame of propulsion input. Twist is packed as a float2 (x: front, y: rear) rather than a
// single scalar, mirroring the existing FieldDriver split between front and rear turn effect;
// Throttle is carried separately from the planar thrust vector for presentations that want overall
// engine intensity independent of direction (the field shield presentation does not use it today).
public readonly struct ThrustEvent
{
    public readonly float2 PlanarThrust;
    public readonly float Throttle;
    public readonly float2 Twist;

    public ThrustEvent(float2 planarThrust, float throttle, float2 twist)
    {
        PlanarThrust = planarThrust;
        Throttle = throttle;
        Twist = twist;
    }
}

// Per-entity (or per-item) source of capability events. One instance is owned by whatever will
// eventually decide these rules; today the FieldShieldTest scene stands in for that owner. Follows
// the Subject<T>-per-event-kind idiom already used on Entity (IncomingHit, HullDamage, etc): each
// event kind gets its own stream, and a subscriber to one kind never sees another.
public class CapabilityEvents
{
    private readonly Subject<AbsorbEvent> _absorb = new Subject<AbsorbEvent>();
    private readonly Subject<GrabEvent> _grab = new Subject<GrabEvent>();
    private readonly Subject<MeleeEvent> _melee = new Subject<MeleeEvent>();
    private readonly Subject<ThrustEvent> _thrust = new Subject<ThrustEvent>();

    public UniRx.IObservable<AbsorbEvent> Absorb => _absorb;
    public UniRx.IObservable<GrabEvent> Grab => _grab;
    public UniRx.IObservable<MeleeEvent> Melee => _melee;
    public UniRx.IObservable<ThrustEvent> Thrust => _thrust;

    public void PublishAbsorb(AbsorbEvent e) => _absorb.OnNext(e);
    public void PublishGrab(GrabEvent e) => _grab.OnNext(e);
    public void PublishMelee(MeleeEvent e) => _melee.OnNext(e);
    public void PublishThrust(ThrustEvent e) => _thrust.OnNext(e);
}
