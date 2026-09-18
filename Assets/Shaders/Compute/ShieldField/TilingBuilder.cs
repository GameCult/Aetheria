using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ShieldField
{
    /// <summary>Hard cap on vertices per cell. Must match MAX_VERTS in ShieldCommon.hlsl.</summary>
    public static class Limits { public const int MaxVerts = 8; }

    [StructLayout(LayoutKind.Sequential)]
    public struct CellStatic
    {
        public Vector2 centroid;
        public float radius;       // circumradius, used for growth + culling
        public float spawnDelay;   // materialisation wavefront offset, seconds
        public uint vertStart;
        public uint vertCount;
        public uint nbrStart;
        public uint nbrCount;
        public float toughness;    // per-cell break resistance multiplier
        public uint tileType;      // author-defined class (thin/fat rhomb, etc)
        public uint splitSeed;     // stable per-cell RNG seed for shard assignment
        public float area;         // cell area — denominator of the graph Laplacian
        public float openW;        // summed weight of unshared edges (free-surface ghost term)
        public float pad0;
        public float pad1;
        public float pad2;
        public const int Stride = 4 * 16;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DualEdge
    {
        public uint nbr;      // neighbouring cell index
        public Vector2 mid;   // midpoint of the shared edge, panel space
        public Vector2 dir;   // unit direction along the shared edge
        public float len;     // shared edge length
        public float w;       // finite-volume Laplacian weight: len / |c_i - c_j|
        public const int Stride = 4 * 7;
    }

    /// <summary>
    /// C# mirror of CellState in ShieldCommon.hlsl — field-for-field, same order, same 16-float
    /// stride as `bState`'s allocation in ShieldPanel.cs. Cut 3 (docs/shield-panel-cut.md) needs
    /// this on both sides of the boundary: ShieldPanel.ResetSim writes it directly to seed each
    /// cell's `growth` at arm time (the D17/D20 fix — see the comment on ResetSim), and the Cut 3
    /// batchmode probe reads it back to verify same-frame injection and reuse-driven temper erosion
    /// without adding a readback path the running sim doesn't already need.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CellState
    {
        public Vector3 vel;
        public float spin;
        public Vector3 pos;
        public float rot;
        public float damage;
        public float breakTime;
        public float glow;
        public float growth;
        public float temper;
        public float released;
        public float peakTension;
        public float pad2;
        public const int Stride = 4 * 16;
    }

    /// <summary>Raw output of a tiling generator: a bag of convex polygons, wound CCW.</summary>
    public class PolygonSoup
    {
        public readonly List<Vector2[]> polys = new();
        public readonly List<int> types = new();

        public void Add(Vector2[] p, int type = 0)
        {
            if (p.Length < 3) return;
            if (SignedArea(p) < 0f) Array.Reverse(p);   // force CCW
            polys.Add(p);
            types.Add(type);
        }

        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, n = p.Count; i < n; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % n];
                a += u.x * v.y - v.x * u.y;
            }
            return a * 0.5f;
        }

        public static Vector2 Centroid(IList<Vector2> p)
        {
            float a = 0f; Vector2 c = Vector2.zero;
            for (int i = 0, n = p.Count; i < n; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % n];
                float cr = u.x * v.y - v.x * u.y;
                a += cr; c += (u + v) * cr;
            }
            if (Mathf.Abs(a) < 1e-9f)
            {
                c = Vector2.zero;
                foreach (var q in p) c += q;
                return c / p.Count;
            }
            return c / (3f * a);
        }
    }

    /// <summary>Welded, adjacency-resolved tiling ready for GPU upload.</summary>
    public class TilingData
    {
        public Vector2[] verts;         // flat pool, each cell's ring is contiguous
        public CellStatic[] cells;
        public DualEdge[] dualEdges;    // flat pool, each cell's arcs are contiguous
        public Bounds bounds;

        public int CellCount => cells.Length;
        public int EdgeCount => dualEdges.Length;
    }

    public static class TilingBuilder
    {
        /// <summary>
        /// Welds coincident vertices, discards cells outside the clip radius, resolves the dual
        /// (cell adjacency) graph, and packs everything into flat GPU-friendly arrays.
        /// </summary>
        public static TilingData Build(PolygonSoup soup, float weldEps = 1e-3f,
                                       float clipRadius = float.PositiveInfinity,
                                       Vector2 wavefrontOrigin = default,
                                       float wavefrontSpeed = 6f,
                                       float delayJitter = 0.08f,
                                       int seed = 12345)
        {
            var weld = new VertexWelder(weldEps);

            // ---- pass 1: keep cells whose centroid is inside the clip disc, weld their rings
            var keptRings = new List<int[]>();
            var keptTypes = new List<int>();
            for (int c = 0; c < soup.polys.Count; c++)
            {
                var poly = soup.polys[c];
                if (poly.Length > Limits.MaxVerts) continue;   // silently drop; raise MaxVerts if this bites
                Vector2 ctr = PolygonSoup.Centroid(poly);
                if (ctr.sqrMagnitude > clipRadius * clipRadius) continue;

                var ring = new int[poly.Length];
                bool degenerate = false;
                for (int i = 0; i < poly.Length; i++)
                {
                    ring[i] = weld.Get(poly[i]);
                    for (int j = 0; j < i; j++) if (ring[j] == ring[i]) degenerate = true;
                }
                if (degenerate) continue;
                keptRings.Add(ring);
                keptTypes.Add(soup.types[c]);
            }

            int cellCount = keptRings.Count;
            if (cellCount == 0) throw new InvalidOperationException("Tiling produced no cells.");

            // ---- pass 2: undirected edge -> incident cells (at most two for a manifold tiling)
            var edgeMap = new Dictionary<long, (int a, int b)>(cellCount * 4);
            for (int c = 0; c < cellCount; c++)
            {
                var ring = keptRings[c];
                for (int i = 0; i < ring.Length; i++)
                {
                    long k = EdgeKey(ring[i], ring[(i + 1) % ring.Length]);
                    if (edgeMap.TryGetValue(k, out var e))
                        edgeMap[k] = (e.a, e.b < 0 ? c : e.b);   // third incidence would be non-manifold; ignore
                    else
                        edgeMap[k] = (c, -1);
                }
            }

            // Centroids must exist before arcs, since Laplacian weights need
            // the distance between the two cells' centroids.
            var ctrs = new Vector2[cellCount];
            var areas = new float[cellCount];
            for (int c = 0; c < cellCount; c++)
            {
                var ring = keptRings[c];
                var pts = new Vector2[ring.Length];
                for (int i = 0; i < ring.Length; i++) pts[i] = weld.Point(ring[i]);
                ctrs[c] = PolygonSoup.Centroid(pts);
                areas[c] = Mathf.Abs(PolygonSoup.SignedArea(pts));
            }

            // ---- pass 3: build directed dual arcs per cell
            var rng = new System.Random(seed);
            var vertPool = new List<Vector2>(cellCount * 5);
            var edgePool = new List<DualEdge>(cellCount * 5);
            var cells = new CellStatic[cellCount];
            var bMin = new Vector2(float.MaxValue, float.MaxValue);
            var bMax = new Vector2(float.MinValue, float.MinValue);

            for (int c = 0; c < cellCount; c++)
            {
                var ring = keptRings[c];
                int n = ring.Length;

                int vStart = vertPool.Count;
                Vector2 ctr = ctrs[c];
                for (int i = 0; i < n; i++) vertPool.Add(weld.Point(ring[i]));

                float radius = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = vertPool[vStart + i];
                    radius = Mathf.Max(radius, (p - ctr).magnitude);
                    bMin = Vector2.Min(bMin, p); bMax = Vector2.Max(bMax, p);
                }

                int eStart = edgePool.Count;
                float openW = 0f;
                for (int i = 0; i < n; i++)
                {
                    int ia = ring[i], ib = ring[(i + 1) % n];
                    var pair = edgeMap[EdgeKey(ia, ib)];
                    int other = pair.a == c ? pair.b : pair.a;

                    Vector2 pa = weld.Point(ia), pb = weld.Point(ib);
                    Vector2 d = pb - pa; float len = d.magnitude;
                    if (len < 1e-6f) continue;
                    Vector2 mid = (pa + pb) * 0.5f;

                    if (other < 0)
                    {
                        // Unshared edge: a free surface. Accumulate its ghost weight so the
                        // wave kernel can impose stress = 0 there (Dirichlet -> sign-inverting
                        // reflection, which is what turns the incoming compression into the
                        // tension that spalls the rim).
                        openW += len / Mathf.Max((mid - ctr).magnitude, 1e-4f);
                        continue;
                    }

                    float nbrDist = Mathf.Max((ctrs[other] - ctr).magnitude, 1e-4f);
                    edgePool.Add(new DualEdge
                    {
                        nbr = (uint)other,
                        mid = mid,
                        dir = d / len,
                        len = len,
                        w = len / nbrDist
                    });
                }

                float dist = (ctr - wavefrontOrigin).magnitude;
                cells[c] = new CellStatic
                {
                    centroid = ctr,
                    radius = radius,
                    spawnDelay = dist / Mathf.Max(wavefrontSpeed, 1e-3f)
                                 + (float)rng.NextDouble() * delayJitter,
                    vertStart = (uint)vStart,
                    vertCount = (uint)n,
                    nbrStart = (uint)eStart,
                    nbrCount = (uint)(edgePool.Count - eStart),
                    toughness = 0.8f + (float)rng.NextDouble() * 0.4f,
                    tileType = (uint)Mathf.Max(0, keptTypes[c]),
                    splitSeed = (uint)rng.Next(int.MinValue, int.MaxValue),
                    area = Mathf.Max(areas[c], 1e-6f),
                    openW = openW,
                    pad0 = 0f,
                    pad1 = 0f,
                    pad2 = 0f
                };
            }

            var data = new TilingData
            {
                verts = vertPool.ToArray(),
                cells = cells,
                dualEdges = edgePool.ToArray()
            };
            Vector2 size = bMax - bMin;
            data.bounds = new Bounds((bMin + bMax) * 0.5f, new Vector3(size.x, size.y, 0.1f));
            return data;
        }

        static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ------------------------------------------------------------------
        // Dual tiling: one dual cell per interior primal vertex, its corners
        // being the centroids of the primal cells around that vertex.
        // ------------------------------------------------------------------
        public static PolygonSoup TakeDual(PolygonSoup soup, float weldEps = 1e-3f)
        {
            var weld = new VertexWelder(weldEps);
            var rings = new List<int[]>();
            var centroids = new List<Vector2>();

            for (int c = 0; c < soup.polys.Count; c++)
            {
                var poly = soup.polys[c];
                var ring = new int[poly.Length];
                for (int i = 0; i < poly.Length; i++) ring[i] = weld.Get(poly[i]);
                rings.Add(ring);
                centroids.Add(PolygonSoup.Centroid(poly));
            }

            // vertex -> incident cells, and vertex -> boundary flag
            var incident = new Dictionary<int, List<int>>();
            var degree = new Dictionary<int, int>();
            var edgeCount = new Dictionary<long, int>();

            for (int c = 0; c < rings.Count; c++)
            {
                var ring = rings[c];
                for (int i = 0; i < ring.Length; i++)
                {
                    if (!incident.TryGetValue(ring[i], out var lst)) incident[ring[i]] = lst = new List<int>();
                    lst.Add(c);
                    long k = EdgeKey(ring[i], ring[(i + 1) % ring.Length]);
                    edgeCount[k] = edgeCount.TryGetValue(k, out int n) ? n + 1 : 1;
                }
            }
            // a vertex is interior only if every edge touching it is shared by two cells
            var onBoundary = new HashSet<int>();
            foreach (var kv in edgeCount)
                if (kv.Value < 2)
                {
                    onBoundary.Add((int)(kv.Key >> 32));
                    onBoundary.Add((int)(kv.Key & 0xffffffff));
                }

            var dual = new PolygonSoup();
            foreach (var kv in incident)
            {
                if (onBoundary.Contains(kv.Key)) continue;
                var cellsAround = kv.Value;
                if (cellsAround.Count < 3) continue;

                Vector2 hub = weld.Point(kv.Key);
                var pts = new List<Vector2>(cellsAround.Count);
                foreach (int c in cellsAround) pts.Add(centroids[c]);
                pts.Sort((u, v) =>
                    Mathf.Atan2(u.y - hub.y, u.x - hub.x).CompareTo(
                    Mathf.Atan2(v.y - hub.y, v.x - hub.x)));

                dual.Add(pts.ToArray(), cellsAround.Count);   // type = vertex figure order
            }
            return dual;
        }

        // ------------------------------------------------------------------
        // Merge triangle pairs into rhombs where the union is equilateral.
        // Turns a Penrose half-rhomb (Robinson triangle) soup into P3 rhombs.
        // ------------------------------------------------------------------
        public static PolygonSoup MergeRhombs(PolygonSoup soup, float weldEps = 1e-3f, float tol = 0.04f)
        {
            var weld = new VertexWelder(weldEps);
            int n = soup.polys.Count;
            var rings = new int[n][];
            for (int c = 0; c < n; c++)
            {
                var poly = soup.polys[c];
                rings[c] = new int[poly.Length];
                for (int i = 0; i < poly.Length; i++) rings[c][i] = weld.Get(poly[i]);
            }

            var edgeMap = new Dictionary<long, (int a, int b)>();
            for (int c = 0; c < n; c++)
            {
                if (rings[c].Length != 3) continue;
                for (int i = 0; i < 3; i++)
                {
                    long k = EdgeKey(rings[c][i], rings[c][(i + 1) % 3]);
                    edgeMap[k] = edgeMap.TryGetValue(k, out var e) ? (e.a, c) : (c, -1);
                }
            }

            var used = new bool[n];
            var outSoup = new PolygonSoup();
            foreach (var kv in edgeMap)
            {
                var (ca, cb) = kv.Value;
                if (cb < 0 || used[ca] || used[cb]) continue;
                if (rings[ca].Length != 3 || rings[cb].Length != 3) continue;
                if (soup.types[ca] != soup.types[cb]) continue;

                int p = (int)(kv.Key >> 32), q = (int)(kv.Key & 0xffffffff);
                int xa = Opposite(rings[ca], p, q), xb = Opposite(rings[cb], p, q);
                if (xa < 0 || xb < 0) continue;

                Vector2 P = weld.Point(p), Q = weld.Point(q), A = weld.Point(xa), B = weld.Point(xb);
                float e0 = (A - P).magnitude, e1 = (Q - A).magnitude,
                      e2 = (B - Q).magnitude, e3 = (P - B).magnitude;
                float avg = 0.25f * (e0 + e1 + e2 + e3);
                if (avg < 1e-5f) continue;
                float dev = (Mathf.Abs(e0 - avg) + Mathf.Abs(e1 - avg)
                           + Mathf.Abs(e2 - avg) + Mathf.Abs(e3 - avg)) / (4f * avg);
                if (dev > tol) continue;                       // not a rhombus, leave them alone

                outSoup.Add(new[] { P, A, Q, B }, soup.types[ca]);
                used[ca] = used[cb] = true;
            }

            for (int c = 0; c < n; c++)
                if (!used[c]) outSoup.Add((Vector2[])soup.polys[c].Clone(), soup.types[c]);
            return outSoup;
        }

        static int Opposite(int[] tri, int p, int q)
        {
            for (int i = 0; i < 3; i++) if (tri[i] != p && tri[i] != q) return tri[i];
            return -1;
        }
    }

    /// <summary>Grid-bucketed vertex welder. O(1) lookup, tolerant to floating point drift.</summary>
    public class VertexWelder
    {
        readonly float eps, inv;
        readonly Dictionary<(int, int), List<int>> buckets = new();
        readonly List<Vector2> points = new();

        public VertexWelder(float eps) { this.eps = eps; inv = 1f / eps; }
        public Vector2 Point(int i) => points[i];
        public int Count => points.Count;

        public int Get(Vector2 p)
        {
            int gx = Mathf.FloorToInt(p.x * inv), gy = Mathf.FloorToInt(p.y * inv);
            float e2 = eps * eps;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (buckets.TryGetValue((gx + dx, gy + dy), out var lst))
                        foreach (int i in lst)
                            if ((points[i] - p).sqrMagnitude <= e2) return i;

            int id = points.Count;
            points.Add(p);
            if (!buckets.TryGetValue((gx, gy), out var b)) buckets[(gx, gy)] = b = new List<int>();
            b.Add(id);
            return id;
        }
    }
}
