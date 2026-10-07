using CultMath;
using static CultMath.math;

// The player's face-the-aim state, engine-free. Held follows the Face Aim hold input each frame; Latched is
// flipped by the Caps Lock press. Either one faces the aim; neither leaves the hull to the turn axis.
// Runtime only, never saved: Latched starts false each session.
public class HelmInput
{
    public bool Held;
    public bool Latched;

    public bool FaceAim => Held || Latched;

    public void Toggle() => Latched = !Latched;

    public float Demand(Ship ship, float2 aim, float turnAxis) =>
        FaceAim ? Steering.Toward(ship, aim) : clamp(turnAxis, -1f, 1f);
}
