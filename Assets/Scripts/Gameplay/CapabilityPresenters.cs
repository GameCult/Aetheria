using UnityEngine;

// Presentation-side contract for the capability events in Assets/Scripts/ServerShared/CapabilityEvents.cs
// (docs/headless-playground-cut.md fork L). A presentation implements whichever of these interfaces
// match what it visualizes -- one presentation may implement all four (the field shield), or just one
// (a dedicated melee-blade effect). CapabilityPresentationBinder is what actually wires a presenter's
// method to the matching event stream; a presenter never talks to CapabilityEvents itself.
public interface IAbsorbPresenter
{
    void Absorb(AbsorbEvent e);
}

// Grab needs a live Transform to animate, which the engine-free GrabEvent cannot carry (its
// TargetHandle is a plain identity, not a Unity reference). The binder resolves the handle through a
// delegate supplied by the event source's owner and hands the presenter the result, along with the
// initial velocity the target was already moving with (also owner-supplied, for the same reason).
public interface IGrabPresenter
{
    void Grab(GrabEvent e, Transform target, Vector3 initialVelocity);
}

public interface IMeleePresenter
{
    void Melee(MeleeEvent e);
}

public interface IThrustPresenter
{
    void Thrust(ThrustEvent e);
}
