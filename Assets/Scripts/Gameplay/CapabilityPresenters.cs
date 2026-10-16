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

// Presenters read the event and the simulated pose, never a Transform the sim does not own.
public interface IGrabPresenter
{
    void Grab(GrabEvent e);
}

public interface IMeleePresenter
{
    void Melee(MeleeEvent e);
}

public interface IThrustPresenter
{
    void Thrust(ThrustEvent e);
}
