using System.Collections;
using System.Linq;
using MIConvexHull;
using Unity.Mathematics;
using static Unity.Mathematics.math;

/// <summary>
/// A vertex is a simple class that stores the postion of a point, node or vertex.
/// </summary>
public class Cell2 : TriangulationCell<Vertex2, Cell2>
{
	float2? circumCenter, centroid;
	
	public float2 Circumcenter {
		get {
			circumCenter = circumCenter ?? GetCircumcenter();
			return circumCenter.Value;
		}
	}
	
	public float2 Centroid {
		get {
			centroid = centroid ?? GetCentroid();
			return centroid.Value;
		}
	} 

	public Cell2 ()
	{

	}

	double LengthSquared (float[] v)
	{
		float norm = 0;
		for (int i = 0; i < v.Length; i++) {
			var t = v [i];
			norm += t * t;
		}
		return norm;
	}

	float Determinant(float[,] m)
	{
		float fCofactor00 = m[1,1] * m[2,2] - m[1,2] * m[2,1];
		float fCofactor10 = m[1,2] * m[2,0] - m[1,0] * m[2,2];
		float fCofactor20 = m[1,0] * m[2,1] - m[1,1] * m[2,0];
		
		float fDet = m[0,0] * fCofactor00 + m[0,1] * fCofactor10 + m[0,2] * fCofactor20;
		
		return fDet;
	}
	
	float2 GetCircumcenter ()
	{
		// From MathWorld: http://mathworld.wolfram.com/Circumcircle.html
	
		var points = Vertices;
	
		float[,] m = new float[3, 3];
	
		// x, y, 1
		for (int i = 0; i < 3; i++) {
			m[i, 0] = points[i].x;
			m[i, 1] = points[i].y;
			m[i, 2] = 1;
		}
		var a = Determinant(m);
	
		// size, y, 1
		for (int i = 0; i < 3; i++) {
			m[i, 0] = lengthsq(points[i].StoredPosition);
		}
		var dx = -Determinant(m);
	
		// size, x, 1
		for (int i = 0; i < 3; i++) {
			m[i, 1] = points[i].x;
		}
		var dy = Determinant(m);
	
		// size, x, y
		//for (int i = 0; i < 3; i++) {
		//	m[i, 2] = points[i].y;
		//}
		//var c = -Det(m);
	
		var s = -1.0f / (2.0f * a);
		//var r = System.Math.Abs(s) * System.Math.Sqrt(dx * dx + dy * dy - 4 * a * c);

		return new float2(s * dx, s * dy);
	}
	
	float2 GetCentroid ()
	{
		return new float2(Vertices.Select(v => v.Position[0]).Average(), Vertices.Select(v => v.Position[1]).Average());
	}    

}















