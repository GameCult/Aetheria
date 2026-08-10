using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public static class AetheriaMath
{
    public const float Deg2Rad = 0.01745329f;
    public const float Rad2Deg = 57.29578f;

    public static float Decay(float source, float lambda, float dt)
    {
        return source * exp(-lambda * dt);
    }

    public static float3 Decay(float3 source, float lambda, float dt)
    {
        return source * exp(-lambda * dt);
    }

    public static float3 Decay(float3 source, float3 lambda, float dt)
    {
        return source * exp(-lambda * dt);
    }
    
    public static float Damp(float a, float b, float lambda, float dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    public static float2 Damp(float2 a, float2 b, float lambda, float dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    public static float3 Damp(float3 a, float3 b, float lambda, float dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    public static double Damp(double a, double b, double lambda, double dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    public static double2 Damp(double2 a, double2 b, double2 lambda, double2 dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    public static double3 Damp(double3 a, double3 b, double3 lambda, double3 dt)
    {
        return lerp(a, b, 1 - exp(-lambda * dt));
    }
    
    //first-order intercept using absolute target position
    public static float3 FirstOrderIntercept
    (
        float3 shooterPosition,
        float3 shooterVelocity,
        float shotSpeed,
        float3 targetPosition,
        float3 targetVelocity
    )  {
        float3 targetRelativePosition = targetPosition - shooterPosition;
        float3 targetRelativeVelocity = targetVelocity - shooterVelocity;
        float t = FirstOrderInterceptTime
        (
            shotSpeed,
            targetRelativePosition,
            targetRelativeVelocity
        );
        return targetPosition + t*targetRelativeVelocity;
    }
    
    //first-order intercept using relative target position
    public static float FirstOrderInterceptTime
    (
        float shotSpeed,
        float3 targetRelativePosition,
        float3 targetRelativeVelocity
    ) {
        float velocitySquared = lengthsq(targetRelativeVelocity);
        if(velocitySquared < 0.001f)
            return 0f;
 
        float a = velocitySquared - shotSpeed*shotSpeed;
 
        //handle similar velocities
        if (abs(a) < 0.001f)
        {
            float t = -lengthsq(targetRelativePosition)/ (2f*dot(targetRelativeVelocity, targetRelativePosition));
            return max(t, 0f); //don't shoot back in time
        }
 
        float b = 2f*dot(targetRelativeVelocity, targetRelativePosition);
        float c = lengthsq(targetRelativePosition);
        float determinant = b*b - 4f*a*c;
 
        if (determinant > 0f) { //determinant > 0; two intercept paths (most common)
            float	t1 = (-b + sqrt(determinant))/(2f*a),
                    t2 = (-b - sqrt(determinant))/(2f*a);
            if (t1 > 0f) {
                if (t2 > 0f)
                    return min(t1, t2); //both are positive
                else
                    return t1; //only t1 is positive
            } else
                return max(t2, 0f); //don't shoot back in time
        } else if (determinant < 0f) //determinant < 0; no intercept path
            return 0f;
        else //determinant = 0; one intercept path, pretty much never happens
            return max(-b/(2f*a), 0f); //don't shoot back in time
    }
    
    // http://csharphelper.com/blog/2016/09/find-the-shortest-distance-between-a-point-and-a-line-segment-in-c/
    // Calculate the distance between
    // point pt and the segment p1 --> p2.
    public static float FindDistanceToSegment(
        float2 pt, float2 p1, float2 p2, out float2 closest)
    {
        float dx = p2.x - p1.x;
        float dy = p2.y - p1.y;
        if (dx == 0 && dy == 0)
        {
            // It's a point not a line segment.
            closest = p1;
            dx = pt.x - p1.x;
            dy = pt.y - p1.y;
            return sqrt(dx * dx + dy * dy);
        }

        // Calculate the t that minimizes the distance.
        float t = ((pt.x - p1.x) * dx + (pt.y - p1.y) * dy) /
                  (dx * dx + dy * dy);

        // See if this represents one of the segment's
        // end points or a point in the middle.
        if (t < 0)
        {
            closest = float2(p1.x, p1.y);
            dx = pt.x - p1.x;
            dy = pt.y - p1.y;
        }
        else if (t > 1)
        {
            closest = float2(p2.x, p2.y);
            dx = pt.x - p2.x;
            dy = pt.y - p2.y;
        }
        else
        {
            closest = float2(p1.x + t * dx, p1.y + t * dy);
            dx = pt.x - closest.x;
            dy = pt.y - closest.y;
        }

        return sqrt(dx * dx + dy * dy);
    }
    
    //Returns a position between 4 float3s with Catmull-Rom spline algorithm
    //http://www.iquilezles.org/www/articles/minispline/minispline.htm
    public static float GetCatmullRomPosition(float p0, float p1, float p2, float p3, float t)
    {
        //The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
        var a = 2f * p1;
        var b = p2 - p0;
        var c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        var d = -p0 + 3f * p1 - 3f * p2 + p3;

        //The cubic polynomial: a + b * t + c * t^2 + d * t^3
        var pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

        return pos;
    }
    
    public static float3 GetCatmullRomPosition(float3 p0, float3 p1, float3 p2, float3 p3, float t)
    {
        //The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
        var a = 2f * p1;
        var b = p2 - p0;
        var c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        var d = -p0 + 3f * p1 - 3f * p2 + p3;

        //The cubic polynomial: a + b * t + c * t^2 + d * t^3
        var pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

        return pos;
    }

    // https://catlikecoding.com/unity/tutorials/curves-and-splines/
    public static float GetQuadraticSplinePosition(float p0, float p1, float p2, float t)
    {
        t = saturate(t);
        var oneMinusT = 1f - t;
        return
            oneMinusT * oneMinusT * p0 +
            2f * oneMinusT * t * p1 +
            t * t * p2;
    }

    public static float3 GetQuadraticSplinePosition(float3 p0, float3 p1, float3 p2, float t)
    {
        t = saturate(t);
        var oneMinusT = 1f - t;
        return
            oneMinusT * oneMinusT * p0 +
            2f * oneMinusT * t * p1 +
            t * t * p2;
    }
    
    public static float3 GetCubicSplinePosition (float p0, float p1, float p2, float p3, float t) {
        t = saturate(t);
        var oneMinusT = 1f - t;
        return
            oneMinusT * oneMinusT * oneMinusT * p0 +
            3f * oneMinusT * oneMinusT * t * p1 +
            3f * oneMinusT * t * t * p2 +
            t * t * t * p3;
    }
    
    public static float3 GetCubicSplinePosition (float3 p0, float3 p1, float3 p2, float3 p3, float t) {
        t = saturate(t);
        var oneMinusT = 1f - t;
        return
            oneMinusT * oneMinusT * oneMinusT * p0 +
            3f * oneMinusT * oneMinusT * t * p1 +
            3f * oneMinusT * t * t * p2 +
            t * t * t * p3;
    }

    public static float Smoothstep(float x)
    {
        x = saturate(x);
        return x * x * (3 - 2 * x);
    }

    public static float Smootherstep(float x)
    {
        x = saturate(x);
        return x * x * x * (x * (x * 6 - 15) + 10);
    }
}
