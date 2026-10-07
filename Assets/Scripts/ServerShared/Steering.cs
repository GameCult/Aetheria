using System;
using CultMath;
using static CultMath.math;

// The one law that turns a wanted heading into a facing command. Ship.Update reads only Ship.Turn; whoever
// wants the hull to face somewhere (the player's face-the-aim state, an agent state) asks here and writes Turn.
public static class Steering
{
    const float Deadband = .01f;

    // Positive is clockwise. Lateral component of the heading with sqrt shaping and a deadband; a heading
    // behind the hull turns at full demand (its lateral component alone would vanish at exactly astern).
    // A zero or non-finite heading holds course.
    public static float Toward(Ship ship, float2 heading)
    {
        if (!float.IsFinite(heading.x) || !float.IsFinite(heading.y) || lengthsq(heading) < 1e-12f) return 0f;
        var dir = normalize(ship.Direction);
        var wanted = normalize(heading);
        var d = dot(wanted, dir.Rotate(ItemRotation.Clockwise));
        if (dot(wanted, dir) < 0f) return d < 0f ? -1f : 1f;
        if (abs(d) < Deadband) return 0f;
        return sqrt(abs(d)) * sign(d);
    }
}
