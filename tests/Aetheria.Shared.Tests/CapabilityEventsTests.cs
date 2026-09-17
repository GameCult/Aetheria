using System.Collections.Generic;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;

// Pins the routing invariant of CapabilityEvents (Assets/Scripts/ServerShared/CapabilityEvents.cs,
// docs/headless-playground-cut.md fork L): a publish of one capability event kind reaches only the
// subscribers of that kind. A subscriber to Absorb must never see a Grab (or Melee, or Thrust) event,
// and vice versa for every other kind.
public sealed class CapabilityEventsTests
{
    [Fact]
    public void EachEventKindReachesOnlyItsOwnSubscribers()
    {
        var events = new CapabilityEvents();

        var absorbed = new List<AbsorbEvent>();
        var grabbed = new List<GrabEvent>();
        var meleed = new List<MeleeEvent>();
        var thrust = new List<ThrustEvent>();

        events.Absorb.Subscribe(absorbed.Add);
        events.Grab.Subscribe(grabbed.Add);
        events.Melee.Subscribe(meleed.Add);
        events.Thrust.Subscribe(thrust.Add);

        var absorbEvent = new AbsorbEvent(float3(1, 2, 3), float3(0, 0, 1), 5f, DamageType.Corrosive);
        var grabEvent = new GrabEvent(7, GrabPhase.Started, float3(1, 0, 0), 1.5f);
        var meleeEvent = new MeleeEvent(float3(0, 0, 1), 10f, 90f, 0.5f);
        var thrustEvent = new ThrustEvent(float2(1, 0), 0.5f, float2(0.2f, -0.2f));

        events.PublishAbsorb(absorbEvent);
        events.PublishGrab(grabEvent);
        events.PublishMelee(meleeEvent);
        events.PublishThrust(thrustEvent);

        // Each stream received exactly one event: its own.
        Assert.Equal(new[] { absorbEvent }, absorbed);
        Assert.Equal(new[] { grabEvent }, grabbed);
        Assert.Equal(new[] { meleeEvent }, meleed);
        Assert.Equal(new[] { thrustEvent }, thrust);

        // And nothing extra leaked across kinds: every list has exactly one entry, not more.
        Assert.Single(absorbed);
        Assert.Single(grabbed);
        Assert.Single(meleed);
        Assert.Single(thrust);
    }
}
