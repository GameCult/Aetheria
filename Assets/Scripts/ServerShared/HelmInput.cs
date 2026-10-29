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

    private float _turnAxis;

    // The turn axis as the helm reads it. A digital input (keys) ramps up while held and back down while released, both at
    // full scale per rampTime, so tapping at a rate holds a gentle turn; reversing cuts to zero and ramps the other way.
    // An analog input passes straight through.
    public float TurnAxis(float raw, bool digital, float dt, float rampTime)
    {
        if (!digital || rampTime <= 0) return _turnAxis = raw;
        var step = dt / rampTime;
        if (raw == 0) return _turnAxis = sign(_turnAxis) * max(0f, abs(_turnAxis) - step);
        if (sign(raw) != sign(_turnAxis)) _turnAxis = 0;
        return _turnAxis = sign(raw) * min(abs(raw), abs(_turnAxis) + step);
    }

    // The turn the player commands: the aim heading, resolved against the hull at each sim step, or the turn axis.
    public TurnCommand Demand(float2 aim, float turnAxis) =>
        FaceAim ? TurnCommand.Toward(aim) : TurnCommand.OfRate(clamp(turnAxis, -1f, 1f));
}
