using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public static class UnityExtensions
{
    public static Texture2D ToTexture(this Color c)
    {
        Texture2D result = new Texture2D(1, 1);
        result.SetPixels(new[]{c});
        result.Apply();

        return result;
    }

    private const int GradSteps = 32;
    public static Texture2D ToTexture(this Gradient g)
    {
        var tex = new Texture2D(GradSteps, 1) {wrapMode = TextureWrapMode.Clamp};
        for (int x = 0; x < GradSteps; x++)
        {
            tex.SetPixel(x, 0, g.Evaluate( (float)x / GradSteps));
        }
        tex.Apply();
        return tex;
    }

    public static Gradient ToGradient(this float4[] keys, bool sharp = false)
    {
        var grad = new Gradient();
        grad.mode = sharp ? GradientMode.Fixed : GradientMode.Blend;
        grad.alphaKeys = new[] {new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1)};
        grad.colorKeys = keys.Select(k => new GradientColorKey(k.xyz.ToColor(), k.w)).ToArray();
        return grad;
    }

    public static AnimationCurve ToCurve(this float4[] keys)
    {
        return new AnimationCurve(keys.Select(v => new Keyframe(v.x, v.y, v.z, v.w)).ToArray());
    }

    public static AnimationCurve ToCurve(this BezierCurve curve)
    {
        return new AnimationCurve(curve.Keys.Select(v => new Keyframe(v.x, v.y, v.z, v.w)).ToArray());
    }
	
    public static Color ToColor(this float3 v) => new Color(v.x,v.y,v.z);
    public static float3 ToFloat3(this Color c) => float3(c.r,c.g,c.b);
	
    public static Color ToColor(this float4 v) => new Color(v.x,v.y,v.z, v.w);
    public static float4 ToFloat4(this Color c) => float4(c.r, c.g, c.b, c.a);
}