using System;
using System.Linq;
using CultMath;
using MessagePack;
using Newtonsoft.Json;
using static CultMath.math;
using float2 = CultMath.float2;

// The tractor beam: the capability that lets a ship collect floating items. It spins its beam up in sim time toward
// the pilot's command, and once the beam is up claims the free item nearest its axis and runs one timed grab
// (extend, envelop, pull) that ends in Zone.TryPickUp, the one commit of a pickup. Its stats are flat
// (Min == Max), as the other behaviours that carry their own timings.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class PickupData : BehaviorData
{
    // How far along the beam's axis an item can be claimed, and how far off the axis.
    [Inspectable, JsonProperty("range"), Key(1), RuntimeInspectable]
    public PerformanceStat Range = new PerformanceStat();

    [Inspectable, JsonProperty("beamRadius"), Key(2), RuntimeInspectable]
    public PerformanceStat BeamRadius = new PerformanceStat();

    // The spin-up (0..1) needed to claim an item, and to keep a grab going.
    [Inspectable, JsonProperty("grabPower"), Key(3), RuntimeInspectable]
    public PerformanceStat GrabPower = new PerformanceStat();

    // Seconds of each phase of a grab.
    [Inspectable, JsonProperty("extendTime"), Key(4), RuntimeInspectable]
    public PerformanceStat ExtendTime = new PerformanceStat();

    [Inspectable, JsonProperty("envelopTime"), Key(5), RuntimeInspectable]
    public PerformanceStat EnvelopTime = new PerformanceStat();

    [Inspectable, JsonProperty("pullTime"), Key(6), RuntimeInspectable]
    public PerformanceStat PullTime = new PerformanceStat();

    // Spin-up and spin-down, in power per sim second.
    [Inspectable, JsonProperty("spinRate"), Key(7), RuntimeInspectable]
    public PerformanceStat SpinRate = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Pickup(this, item);
    }

    // A beam is a piece of gear the pilot holds on; no consumable carries one.
    public override Behavior CreateInstance(ConsumableItemEffect item) =>
        throw new NotSupportedException("pickup: gear only");
}

// Owns this ship's beam power and its one grab. Power is written only here; Zone owns the held item's pose.
public class Pickup : Behavior, IAlwaysUpdatedBehavior
{
    private readonly PickupData _data;
    // Power and the grab clock are kept in double so a fixed tick count lands on the same side of a boundary
    // whatever dt the zone steps by.
    private double _power;
    private bool _grabbing;
    private FloatingBodyId _target;
    private double _elapsed;
    // How many of Envelop and Pull have been published so far (0, 1 or 2).
    private int _published;

    public Pickup(PickupData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    // The beam's spun-up state in [0, 1]: what the pilot's command has driven it to.
    public float Power => (float) _power;

    public void Update(float dt)
    {
        var active = Item.Active.Value;
        var command = active ? Math.Clamp(Entity.TractorCommand, 0f, 1f) : 0f;
        var step = (double) Evaluate(_data.SpinRate) * dt;
        _power = Math.Clamp(_power + Math.Clamp(command - _power, -step, step), 0.0, 1.0);

        var zone = Entity.Zone;
        var grabPower = Evaluate(_data.GrabPower);
        var extend = (double) Evaluate(_data.ExtendTime);
        var envelop = (double) Evaluate(_data.EnvelopTime);
        var pull = (double) Evaluate(_data.PullTime);
        var range = Evaluate(_data.Range);
        var origin = Entity.Position.xz;

        if (!_grabbing)
        {
            if (!active || _power < grabPower) return;
            var item = Select(zone, origin, range, Evaluate(_data.BeamRadius));
            if (item == null || !zone.TryHold(Entity, item.Id)) return;
            _grabbing = true;
            _target = item.Id;
            _elapsed = 0;
            _published = 0;
            Publish(GrabPhase.Started, item, 0);
            Publish(GrabPhase.Extend, item, (float) extend);
            return;
        }

        var held = zone.FloatingItems.FirstOrDefault(f => f.Id.Equals(_target));
        if (held == null || !ReferenceEquals(held.Holder, Entity))
        {
            _grabbing = false;
            Entity.Capabilities.PublishGrab(new GrabEvent(_target, GrabPhase.Cancelled, float3(0), 0));
            return;
        }

        var pulling = _elapsed >= extend + envelop;
        if (!active || _power < grabPower || (!pulling && length(held.Body.Position - origin) > range))
        {
            _grabbing = false;
            zone.Free(_target);
            Publish(GrabPhase.Cancelled, held, 0);
            return;
        }

        _elapsed += dt;
        if (_published < 1 && _elapsed >= extend) { _published = 1; Publish(GrabPhase.Envelop, held, (float) envelop); }
        if (_published < 2 && _elapsed >= extend + envelop) { _published = 2; Publish(GrabPhase.Pull, held, (float) pull); }
        if (_published == 2)
            zone.SetHeldProgress(_target, pull > 0 ? (float) ((_elapsed - extend - envelop) / pull) : 1f);
        if (_elapsed < extend + envelop + pull) return;

        _grabbing = false;
        if (zone.TryPickUp(Entity, _target)) Publish(GrabPhase.Completed, held, 0);
        else
        {
            zone.Free(_target);
            Publish(GrabPhase.Cancelled, held, 0);
        }
    }

    // The free item nearest the beam axis within Range along it and BeamRadius of it; ties go to the lowest id. A
    // zero aim selects nothing.
    private FloatingItem Select(Zone zone, float2 origin, float range, float radius)
    {
        var aim = Entity.Aim.xz;
        if (lengthsq(aim) <= 0) return null;
        var axis = normalize(aim);
        FloatingItem best = null;
        var bestOff = float.MaxValue;
        foreach (var floating in zone.FloatingItems)
        {
            if (floating.Holder != null) continue;
            var to = floating.Body.Position - origin;
            var along = dot(to, axis);
            var off = length(to - axis * along);
            if (along < 0 || along > range || off > radius) continue;
            if (off < bestOff || (off == bestOff && floating.Id.Value < best.Id.Value))
            {
                best = floating;
                bestOff = off;
            }
        }
        return best;
    }

    private void Publish(GrabPhase phase, FloatingItem item, float duration) =>
        Entity.Capabilities.PublishGrab(new GrabEvent(item.Id, phase, float3(item.Body.Position.x, Entity.Position.y, item.Body.Position.y), duration));
}
