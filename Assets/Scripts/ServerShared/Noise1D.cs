using Unity.Mathematics;
using static Unity.Mathematics.math;

public static class Noise1D
{
	private const float HASHSCALE = 0.1031f;

	static float hash(float p)
	{
		float3 p3 = frac(float3(p) * HASHSCALE);
		p3 += dot(p3, p3.yzx + 19.19f);
		return frac((p3.x + p3.y) * p3.z);
	}

	static float fade(float t) { return t * t * t * (t * (6f* t - 15f) + 10f); }

	static float grad(float hash, float p)
	{
		int i = (int)(1e4 * hash);
		return (i & 1) == 0 ? p : -p;
	}

	public static float noise(float p)
	{
		float pi = floor(p), pf = p - pi, w = fade(pf);
		return lerp(grad(hash(pi), pf), grad(hash(pi + 1.0f), pf - 1.0f), w) * 2.0f;
	}
}
