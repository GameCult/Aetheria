using System;
using System.Collections.Generic;
using System.Numerics;
using CultMath;

// The Delaunay triangulation of a galaxy's zone positions, as the edges Galaxy links zones along. Bowyer-Watson
// (Bowyer 1981, Watson 1981): each point is inserted by removing every triangle whose circumcircle strictly holds it and
// fanning the cavity's boundary to it. The orientation and in-circle predicates are exact: a double-precision
// determinant answers when it clears its rounding-error bound (Shewchuk 1997), otherwise the determinant is evaluated
// in BigInteger, since every double is a dyadic rational. For two or more distinct points the edge graph is connected:
// collinear points link each to its neighbour along the line; duplicate or non-finite points throw.
public static class Delaunay
{
    // Each edge once, as (lower index, higher index), in ascending order.
    public static List<(int a, int b)> Edges(IReadOnlyList<float2> points)
    {
        var n = points.Count;
        var order = new int[n];
        for (var i = 0; i < n; i++)
        {
            if (!float.IsFinite(points[i].x) || !float.IsFinite(points[i].y))
                throw new ArgumentException($"Delaunay point {i} is not finite: ({points[i].x}, {points[i].y})");
            order[i] = i;
        }
        // Lexicographic order runs along any line, and puts equal points side by side.
        Array.Sort(order, (i, j) => points[i].x != points[j].x ? points[i].x.CompareTo(points[j].x) : points[i].y.CompareTo(points[j].y));
        for (var i = 1; i < n; i++)
            if (points[order[i - 1]].x == points[order[i]].x && points[order[i - 1]].y == points[order[i]].y)
                throw new ArgumentException($"Delaunay points {Math.Min(order[i - 1], order[i])} and {Math.Max(order[i - 1], order[i])} are the same point ({points[order[i]].x}, {points[order[i]].y})");
        if (n < 2) return new List<(int a, int b)>();

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

        var collinear = true;
        for (var i = 2; i < n && collinear; i++)
            collinear = Orientation(x, y, order[0], order[1], order[i]) == 0;
        if (collinear)
        {
            var line = new List<(int a, int b)>();
            for (var i = 1; i < n; i++) line.Add(Ordered(order[i - 1], order[i]));
            line.Sort();
            return line;
        }

        // A super-triangle so far outside the points that its vertices bend no edge between them: a float's coordinates
        // are multiples of 2^-149, so a triple of distinct non-collinear points has circumradius under extent^3 * 2^300,
        // which 2^700 * extent exceeds for any extent a float holds. Its triangles are dropped at the end. Its
        // coordinates overflow the double determinants, which then fail every comparison and fall to the exact path.
        var size = Math.Max(maxX - minX, maxY - minY) * Math.Pow(2, 700);
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
                if (InCircle(x, y, triangle.A, triangle.B, triangle.C, p) * triangle.Orientation <= 0) continue;
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
        // +1 if A, B, C run counter-clockwise, -1 if clockwise; a triangle never has zero area.
        public readonly int Orientation;

        public Triangle(int a, int b, int c, double[] x, double[] y)
        {
            A = a;
            B = b;
            C = c;
            Orientation = Delaunay.Orientation(x, y, a, b, c);
            if (Orientation == 0) throw new InvalidOperationException($"Delaunay built a zero-area triangle on points {a}, {b}, {c}");
        }
    }

    // Shewchuk's error bounds for the double-precision determinants: (3 + 16e)e and (10 + 96e)e, e = 2^-53.
    private const double OrientationBound = 3.3306690738754716e-16;
    private const double InCircleBound = 1.1102230246251577e-15;

    // The sign of the area a, b, c span: +1 counter-clockwise, -1 clockwise, 0 collinear.
    private static int Orientation(double[] x, double[] y, int a, int b, int c)
    {
        var left = (x[a] - x[c]) * (y[b] - y[c]);
        var right = (y[a] - y[c]) * (x[b] - x[c]);
        var det = left - right;
        var bound = OrientationBound * (Math.Abs(left) + Math.Abs(right));
        if (det > bound) return 1;
        if (-det > bound) return -1;
        var exact = Exact.Of(x, y, a, b, c);
        return (exact[0] * exact[3] - exact[1] * exact[2]).Sign;
    }

    // The sign of the in-circle determinant: +1 when p is strictly inside the circle through a, b, c taken
    // counter-clockwise, -1 strictly outside, 0 on it.
    private static int InCircle(double[] x, double[] y, int a, int b, int c, int p)
    {
        double adx = x[a] - x[p], ady = y[a] - y[p], bdx = x[b] - x[p], bdy = y[b] - y[p], cdx = x[c] - x[p], cdy = y[c] - y[p];
        double abL = adx * bdy, abR = bdx * ady, bcL = bdx * cdy, bcR = cdx * bdy, caL = cdx * ady, caR = adx * cdy;
        double alift = adx * adx + ady * ady, blift = bdx * bdx + bdy * bdy, clift = cdx * cdx + cdy * cdy;
        var det = alift * (bcL - bcR) + blift * (caL - caR) + clift * (abL - abR);
        var permanent = (Math.Abs(bcL) + Math.Abs(bcR)) * alift + (Math.Abs(caL) + Math.Abs(caR)) * blift + (Math.Abs(abL) + Math.Abs(abR)) * clift;
        var bound = InCircleBound * permanent;
        if (det > bound) return 1;
        if (-det > bound) return -1;
        var e = Exact.Of(x, y, a, b, c, p);
        BigInteger ax = e[0], ay = e[1], bx = e[2], by = e[3], cx = e[4], cy = e[5];
        return (e[6] * (bx * cy - cx * by) + e[7] * (cx * ay - ax * cy) + e[8] * (ax * by - bx * ay)).Sign;
    }

    // Coordinates exactly, as integers in units of the smallest binary exponent among them.
    private static class Exact
    {
        // Orientation: [a.x - c.x, a.y - c.y, b.x - c.x, b.y - c.y] with the cross product the caller takes.
        public static BigInteger[] Of(double[] x, double[] y, int a, int b, int c)
        {
            var v = Scaled(new[] { x[a], y[a], x[b], y[b], x[c], y[c] });
            return new[] { v[0] - v[4], v[1] - v[5], v[2] - v[4], v[3] - v[5] };
        }

        // In-circle: [a - p (x, y), b - p, c - p, then each one's squared length].
        public static BigInteger[] Of(double[] x, double[] y, int a, int b, int c, int p)
        {
            var v = Scaled(new[] { x[a], y[a], x[b], y[b], x[c], y[c], x[p], y[p] });
            BigInteger adx = v[0] - v[6], ady = v[1] - v[7], bdx = v[2] - v[6], bdy = v[3] - v[7], cdx = v[4] - v[6], cdy = v[5] - v[7];
            return new[] { adx, ady, bdx, bdy, cdx, cdy, adx * adx + ady * ady, bdx * bdx + bdy * bdy, cdx * cdx + cdy * cdy };
        }

        private static BigInteger[] Scaled(double[] values)
        {
            var mantissa = new long[values.Length];
            var exponent = new int[values.Length];
            var lowest = int.MaxValue;
            for (var i = 0; i < values.Length; i++)
            {
                var bits = BitConverter.DoubleToInt64Bits(values[i]);
                var biased = (int) ((bits >> 52) & 0x7FF);
                var fraction = bits & 0xFFFFFFFFFFFFFL;
                mantissa[i] = biased == 0 ? fraction : fraction | (1L << 52);
                exponent[i] = biased == 0 ? -1074 : biased - 1075;
                if (bits < 0) mantissa[i] = -mantissa[i];
                if (mantissa[i] != 0) lowest = Math.Min(lowest, exponent[i]);
            }
            var scaled = new BigInteger[values.Length];
            for (var i = 0; i < values.Length; i++)
                scaled[i] = mantissa[i] == 0 ? BigInteger.Zero : new BigInteger(mantissa[i]) << (exponent[i] - lowest);
            return scaled;
        }
    }
}
