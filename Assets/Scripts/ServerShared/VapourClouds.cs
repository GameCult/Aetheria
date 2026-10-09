using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// A vented vapour cloud: a free-floating disc that thins every sensor sight line through it. Not an Entity,
// never priced or rolled, never saved. Zone.Vent is the only adder and Zone's cloud step the only remover;
// Zone.Obscuration is the one reader detection uses.
public sealed class VapourCloud
{
    public FloatingBodyId Id;
    public KinematicBody Body;
    public double VentedAt;
    public float Lifetime;
    public float Radius;
    // The opacity at the moment of venting; it fades linearly to nothing over Lifetime.
    public float Opacity;
    public Entity Venter;

    public float OpacityAt(double now) =>
        Lifetime <= 0f ? 0f : saturate(Opacity) * saturate(1f - (float) (now - VentedAt) / Lifetime);

    // Whether the segment a-b reaches the disc: the closest point of the segment to the centre lies within the
    // radius. A cloud past either end of the segment touches it only if it is near that end.
    public bool Touches(float2 a, float2 b)
    {
        var ab = b - a;
        var length2 = lengthsq(ab);
        var t = length2 > 0f ? saturate(dot(Body.Position - a, ab) / length2) : 0f;
        return lengthsq(Body.Position - (a + ab * t)) <= Radius * Radius;
    }
}
