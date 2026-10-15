using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

/// <summary>
/// The law of one gravity well: a 2D point mass with a softened core. Outside the core radius the potential is
/// logarithmic and the pull is c / r; inside it the pull falls linearly to zero at the centre and meets the tail in
/// value and slope at the core radius. c is the well's strength (u^2/s^2), R its core radius (u). No threshold
/// anywhere: a well pulls at every distance. Zone.GetForce and Zone.GetHeight sum this over a zone's bodies.
/// </summary>
public static class GravityField
{
    // The potential at distance r from the body, defined up to a constant (callers subtract their reference).
    public static float Potential(float c, float coreRadius, float r) =>
        r < coreRadius
            ? c * r * r / (2 * coreRadius * coreRadius)
            : c * (log(r / coreRadius) + .5f);

    // The acceleration at `offsetFromBody`, toward the body: zero at the body itself.
    public static float2 Acceleration(float c, float coreRadius, float2 offsetFromBody)
    {
        var r = length(offsetFromBody);
        if (r == 0f) return float2.zero;
        var pull = r < coreRadius ? c * r / (coreRadius * coreRadius) : c / r;
        return -offsetFromBody / r * pull;
    }
}
