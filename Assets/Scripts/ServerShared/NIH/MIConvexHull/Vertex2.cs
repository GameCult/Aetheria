using System;
using System.Collections;
using MIConvexHull;
using CultMath;
using static CultMath.math;

public class Vertex2 : IVertex
{
	public float[] Position => new[] {StoredPosition.x, StoredPosition.y};

	public float2 StoredPosition;
	
	public float x { get { return StoredPosition.x; } }
	public float y { get { return StoredPosition.y; } }
	
	public Vertex2(float x, float y)
	{
		StoredPosition = float2(x, y);
	}

	public Vertex2(float2 pos)
	{
		StoredPosition = pos;
	}

	public float2 ToFloat2() {
		return StoredPosition;
	}

	public float Distance(Vertex2 v)
	{
		return length(StoredPosition-v.StoredPosition);
	}
}
