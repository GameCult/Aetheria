using System.Collections;
using MIConvexHull;
using CultMath;
using static CultMath.math;

public class Vertex3 : IVertex
{
	public float[] Position => new[] {StoredPosition.x, StoredPosition.y};

	public float3 StoredPosition;
	
	public Vertex3(float x, float y, float z)
	{
		StoredPosition = float3(x, y, z);
	}

	public Vertex3(float3 pos)
	{
		StoredPosition = pos;
	}

	public float3 ToFloat3() {
		return StoredPosition;
	}

	public float Distance(Vertex3 v)
	{
		return length(StoredPosition-v.StoredPosition);
	}
}
