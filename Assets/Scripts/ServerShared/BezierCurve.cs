using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class BezierCurve
{
    [JsonProperty("keys"), Key(0)] public float4[] Keys;
    
    [IgnoreMember] private int2 _cachedIndices;
    
    [IgnoreMember] private const int STEPS = 64;

    [IgnoreMember] private float? _maximum;
    
    [IgnoreMember]
    public float Maximum
    {
        get
        {
            if (_maximum != null) return _maximum ?? 0;
            var samples = Enumerable.Range(0, STEPS).Select(i => (float) i / STEPS).ToArray();
            float max = 0;
            _maximum = 0;
            foreach (var f in samples)
            {
                var p = Evaluate(f);
                if (p > max)
                {
                    max = p;
                    _maximum = f;
                }
            }

            return _maximum??0;
        }
    }
    
    // Integrate area under curve between start and end time
    public float IntegrateCurve(float startTime, float endTime, int steps)
    {
        return Integrate(Evaluate, startTime, endTime, steps);
    }

    // Integrate function f(x) using the trapezoidal rule between x=x_low..x_high
    public static float Integrate(Func<float, float> f, float x_low, float x_high, int N_steps)
    {
        float h = (x_high - x_low) / N_steps;
        float res = (f(x_low) + f(x_high)) / 2;
        for (int i = 1; i < N_steps; i++)
        {
            res += f(x_low + i * h);
        }
        return h * res;
    }

    // public static float Screen(float a, float b)
    // {
    //     return 1 - (1 - a)*(1 - b);
    // }
    
    public float Evaluate(float time)
    {
        // Clamp time
        time = clamp(time, Keys[0].x, Keys[Keys.Length - 1].x);
        
        FindSurroundingKeyframes(time, Keys); 
        return HermiteInterpolate(time, Keys[_cachedIndices.x], Keys[_cachedIndices.y]);
    }

    void FindSurroundingKeyframes(float time, float4[] curve)
    {
        // Check that time is within cached keyframe time
        if (time >= curve[_cachedIndices.x].x && 
            time <= curve[_cachedIndices.y].x)
        {
            return;
        }

            
        // Fall back to using dichotomic search.
        var length = curve.Length;
        int half;
        int middle;
        int first = 0;

        while (length > 0)
        {
            half = length >> 1;
            middle = first + half;

            if (time < curve[middle].x)
            {
                length = half;
            }
            else
            {
                first = middle + 1;
                length = length - half - 1;
            }
        }

        // If not within range, we pick the last element twice.
        var indices = int2(first - 1, min(curve.Length - 1, first));
        _cachedIndices = indices;
    }

    float HermiteInterpolate(float time, in float4 leftKeyframe, in float4 rightKeyframe)
    {
        // Handle stepped curve.
        if (isinf(leftKeyframe.w) || isinf(rightKeyframe.z))
        {
            return leftKeyframe.y;
        }

        float dx = rightKeyframe.x - leftKeyframe.x;
        float m0;
        float m1;
        float t;
        if (dx != 0.0f)
        {
            t = (time - leftKeyframe.x) / dx;
            m0 = leftKeyframe.w * dx;
            m1 = rightKeyframe.z * dx;
        }
        else
        {
            t = 0.0f;
            m0 = 0;
            m1 = 0;
        }

        return HermiteInterpolate(t, leftKeyframe.y, m0, m1, rightKeyframe.y);
    }

    static float HermiteInterpolate(float t, float p0, float m0, float m1, float p1)
    {
        // Unrolled the equations to avoid precision issue.
        // (2 * t^3 -3 * t^2 +1) * p0 + (t^3 - 2 * t^2 + t) * m0 + (-2 * t^3 + 3 * t^2) * p1 + (t^3 - t^2) * m1

        var a = 2.0f * p0 + m0 - 2.0f * p1 + m1;
        var b = -3.0f * p0 - 2.0f * m0 + 3.0f * p1 - m1;
        var c = m0;
        var d = p0;

        return t * (t * (a * t + b) + c) + d;
    }
}