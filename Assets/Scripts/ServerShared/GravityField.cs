using System;
using CultMath;
using static CultMath.math;

/// <summary>
/// The law of one band term U(r; eps, a, b), the only primitive of a zone's gravity (WellTree decides which terms
/// exist). A Plummer-softened log source whose pull toward the centre is exact up to a and fades to nothing over [a, b]:
/// pull r / (r^2 + eps^2) * B(s), s = r^2, B = 1 for s &lt;= a^2, 1 - S(t) on the band with t = (s - a^2) / (b^2 - a^2),
/// 0 for s &gt;= b^2; S is the order-Order smoothstep. The potential is 0 beyond b. With a = b = +inf it is the global term
/// 1/2 ln(r^2 + eps^2). The potential is a function of r^2 (smooth at the centre), and C^Order on the circles r = a and
/// r = b (C4 at Order 4: the player feels higher-order seams), so the drawn height is exactly the sim's potential.
/// On the band the closed form is 1/2 [R ln((t + tau) / (1 + tau)) - (I(1) - I(t))], tau = (a^2 + eps^2) / (b^2 - a^2),
/// (1 - S(t)) = (t + tau) Q(t) + R, I the antiderivative of Q; for tau &gt; 1 it cancels catastrophically in float32
/// (7e-3 at tau 3, 12 at tau 10), so the band is summed as a series in (1 - t) / (1 + tau), whose ratio is below 1/2.
/// Pull and Potential are per unit strength; the caller scales by the term's strength (WellStrength at a mass).
/// Gravity.cginc is generated from this file's constants and coefficients (GravityShader).
/// </summary>
public static class GravityField
{
    /// <summary>The smoothstep's order: the potential is C^Order on every band edge.</summary>
    public const int Order = 4;
    /// <summary>A family's charge is fully on by its support, Beta x its outermost child.</summary>
    public const float Beta = 1.5f;
    /// <summary>A family is felt whole out to max(support, Gamma x its reach) and fades from there to its reach.</summary>
    public const float Gamma = .5f;
    /// <summary>A body's reach in its parent's field, in its distance (the nominal reach before the fade's room).</summary>
    public const float Reach = .5f;
    /// <summary>A ring member of a bodyless node reaches RingReach x its distance: whole out to the ring's diameter.</summary>
    public const float RingReach = 4f;

    /// <summary>The band is summed as a series for tau above this. Measured (map B54): float32 within 1.0e-6 per unit strength everywhere with SeriesTerms terms; the closed form is float32-clean only to about 0.45.</summary>
    public const float SeriesFrom = .4f;

    /// <summary>The series branch's length: its ratio is below 1/1.4, so 40 terms reach float32's floor.</summary>
    public const int SeriesTerms = 40;

    /// <summary>S(t) as polynomial coefficients in t, lowest first: t^Order sum_j C(Order-1+j, j) (1-t)^j.</summary>
    public static readonly float[] S = BuildS();

    /// <summary>1 - S(t) as polynomial coefficients in t, lowest first: the band's profile B.</summary>
    public static readonly float[] OneMinusS = BuildOneMinusS();

    private static float[] BuildS()
    {
        var c = new double[2 * Order];
        for (var j = 0; j < Order; j++)
        {
            var cj = Binomial(Order - 1 + j, j);
            for (var i = 0; i <= j; i++)
                c[Order + i] += cj * Binomial(j, i) * (i % 2 == 0 ? 1 : -1);
        }
        var s = new float[2 * Order];
        for (var i = 0; i < s.Length; i++) s[i] = (float) c[i];
        return s;
    }

    private static float[] BuildOneMinusS()
    {
        var f = new float[2 * Order];
        for (var i = 0; i < f.Length; i++) f[i] = -S[i];
        f[0] += 1f;
        return f;
    }

    private static double Binomial(int n, int k)
    {
        var r = 1.0;
        for (var i = 1; i <= k; i++) r = r * (n - k + i) / i;
        return r;
    }

    // B(s) for the band term (a, b); b = +inf is no fade at all.
    private static float Profile(float inner, float outer, float s)
    {
        if (float.IsPositiveInfinity(outer)) return 1f;
        var a2 = inner * inner;
        var b2 = outer * outer;
        if (s <= a2) return 1f;
        if (s >= b2) return 0f;
        var t = (s - a2) / (b2 - a2);
        var acc = 0f;
        for (var i = OneMinusS.Length - 1; i >= 0; i--) acc = acc * t + OneMinusS[i];
        return acc;
    }

    /// <summary>The pull toward the centre at distance r, per unit strength.</summary>
    public static float Pull(float softening, float inner, float outer, float r)
    {
        var s = r * r;
        return r * Profile(inner, outer, s) / (s + softening * softening);
    }

    /// <summary>The potential at distance r, per unit strength: 0 at and beyond outer, the global log when outer is +inf.</summary>
    public static float Potential(float softening, float inner, float outer, float r)
    {
        var s = r * r;
        var e2 = softening * softening;
        if (float.IsPositiveInfinity(outer)) return .5f * log(s + e2);
        var a2 = inner * inner;
        var b2 = outer * outer;
        if (s >= b2) return 0f;
        var tau = (a2 + e2) / (b2 - a2);
        if (s <= a2) return .5f * log((s + e2) / (a2 + e2)) + Band(tau, 0f);
        return Band(tau, (s - a2) / (b2 - a2));
    }

    // The band's potential at t in [0, 1], 0 at t = 1.
    private static float Band(float tau, float t) => tau <= SeriesFrom ? BandClosed(tau, t) : BandSeries(tau, t);

    private static float BandClosed(float tau, float t)
    {
        // (1 - S(t)) = (t + tau) Q(t) + R by synthetic division; I is Q's antiderivative.
        var n = OneMinusS.Length - 1;
        Span<float> q = stackalloc float[n];
        q[n - 1] = OneMinusS[n];
        for (var i = n - 1; i > 0; i--) q[i - 1] = OneMinusS[i] - tau * q[i];
        var remainder = OneMinusS[0] - tau * q[0];
        return .5f * (remainder * log((t + tau) / (1f + tau)) - (Antiderivative(q, 1f) - Antiderivative(q, t)));
    }

    private static float Antiderivative(ReadOnlySpan<float> q, float t)
    {
        var acc = 0f;
        for (var i = q.Length - 1; i >= 0; i--) acc = (acc + q[i] / (i + 1)) * t;
        return acc;
    }

    private static float BandSeries(float tau, float t)
    {
        // With y = 1 - t, 1 - S(1 - y) = S(y) and 1 / (t + tau) = sum_k y^k / (1 + tau)^(k+1):
        // band = -1/2 sum_k (1 + tau)^-(k+1) sum_j S_j y^(j+k+1) / (j+k+1).
        var y = 1f - t;
        Span<float> yj = stackalloc float[S.Length];
        yj[0] = 1f;
        for (var j = 1; j < yj.Length; j++) yj[j] = yj[j - 1] * y;
        var w = 1f / (1f + tau);
        var wk = w;
        var yk = y;
        var acc = 0f;
        for (var k = 0; k < SeriesTerms; k++)
        {
            var inner = 0f;
            for (var j = Order; j < S.Length; j++) inner += S[j] * yj[j] / (j + k + 1);
            acc += wk * yk * inner;
            wk *= w;
            yk *= y;
        }
        return -.5f * acc;
    }
}
