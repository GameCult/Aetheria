using System;
using System.Collections.Generic;
using CultMath;

// The Delaunay triangulation of a galaxy's zone positions, as the edges Galaxy links zones along. Bowyer-Watson
// (Bowyer 1981, Watson 1981) in double precision: each point is inserted by removing every triangle whose
// circumcircle holds it and fanning the cavity's boundary to it. Every point lands in the triangulation, so for two or
// more distinct points the edge graph is connected.
public static class Delaunay
{
    // Each edge once, as (lower index, higher index), in ascending order.
    public static List<(int a, int b)> Edges(IReadOnlyList<float2> points)
    {
        var n = points.Count;
        var x = new double[n + 3];
        var y = new double[n + 3];
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var i = 0; i < n; i++)
        {
            x[i] = points[i].x;
            y[i] = points[i].y;
            minX = Math.Min(minX, x[i]);
            minY = Math.Min(minY, y[i]);
            maxX = Math.Max(maxX, x[i]);
            maxY = Math.Max(maxY, y[i]);
        }

        // A super-triangle far outside the points, so its vertices bend no edge between them; its triangles are
        // dropped at the end.
        var size = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-6) * 1000;
        double midX = (minX + maxX) / 2, midY = (minY + maxY) / 2;
        x[n] = midX - size; y[n] = midY - size;
        x[n + 1] = midX + size; y[n + 1] = midY - size;
        x[n + 2] = midX; y[n + 2] = midY + size;

        var triangles = new List<Triangle> { new Triangle(n, n + 1, n + 2, x, y) };
        var cavity = new Dictionary<(int, int), int>();
        for (var p = 0; p < n; p++)
        {
            cavity.Clear();
            for (var t = triangles.Count - 1; t >= 0; t--)
            {
                var triangle = triangles[t];
                var dx = x[p] - triangle.CenterX;
                var dy = y[p] - triangle.CenterY;
                if (dx * dx + dy * dy >= triangle.RadiusSquared) continue;
                Count(cavity, triangle.A, triangle.B);
                Count(cavity, triangle.B, triangle.C);
                Count(cavity, triangle.C, triangle.A);
                triangles[t] = triangles[triangles.Count - 1];
                triangles.RemoveAt(triangles.Count - 1);
            }

            // The cavity's boundary is every edge only one removed triangle had.
            foreach (var edge in cavity)
                if (edge.Value == 1)
                    triangles.Add(new Triangle(edge.Key.Item1, edge.Key.Item2, p, x, y));
        }

        var edges = new SortedSet<(int a, int b)>();
        foreach (var triangle in triangles)
        {
            if (triangle.A >= n || triangle.B >= n || triangle.C >= n) continue;
            edges.Add(Ordered(triangle.A, triangle.B));
            edges.Add(Ordered(triangle.B, triangle.C));
            edges.Add(Ordered(triangle.C, triangle.A));
        }
        return new List<(int a, int b)>(edges);
    }

    private static void Count(Dictionary<(int, int), int> cavity, int a, int b)
    {
        var key = Ordered(a, b);
        cavity[key] = cavity.TryGetValue(key, out var seen) ? seen + 1 : 1;
    }

    private static (int a, int b) Ordered(int a, int b) => a < b ? (a, b) : (b, a);

    private readonly struct Triangle
    {
        public readonly int A, B, C;
        public readonly double CenterX, CenterY, RadiusSquared;

        public Triangle(int a, int b, int c, double[] x, double[] y)
        {
            A = a;
            B = b;
            C = c;
            double ax = x[a], ay = y[a], bx = x[b], by = y[b], cx = x[c], cy = y[c];
            var d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            CenterX = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            CenterY = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            RadiusSquared = (ax - CenterX) * (ax - CenterX) + (ay - CenterY) * (ay - CenterY);
        }
    }
}
