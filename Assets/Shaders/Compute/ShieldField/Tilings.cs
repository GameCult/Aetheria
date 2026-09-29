using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShieldField
{
    public enum TilingKind
    {
        Hex,            // regular hexagons
        TriHex,         // 3.6.3.6 — hexagons + triangles (dual: rhombille)
        Square,
        PenroseP3,      // five-fold quasiperiodic rhombs, via substitution
        Voronoi,        // Lloyd-relaxed jittered lattice
        VoronoiIFS      // Voronoi over a chaos-game IFS attractor — fractal cell density
    }

    /// <summary>One affine map of an iterated function system, with its selection weight.</summary>
    [Serializable]
    public struct IfsMap
    {
        public Vector2 translate;
        public float rotateDeg;
        public float scale;
        public float weight;

        public Vector2 Apply(Vector2 p)
        {
            float r = rotateDeg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 q = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c) * scale;
            return q + translate;
        }
    }

    public static class Tilings
    {
        public const float Phi = 1.6180339887f;

        public static PolygonSoup Generate(TilingKind kind, float radius, float cellSize,
                                           int seed, IfsMap[] ifs = null)
        {
            switch (kind)
            {
                case TilingKind.Hex: return Hex(radius, cellSize);
                case TilingKind.TriHex: return TriHex(radius, cellSize);
                case TilingKind.Square: return Square(radius, cellSize);
                case TilingKind.PenroseP3: return Penrose(radius, cellSize);
                case TilingKind.Voronoi: return Voronoi(JitteredSites(radius, cellSize, seed), radius, 3);
                case TilingKind.VoronoiIFS: return Voronoi(ChaosGame(ifs ?? Pentaflake(), radius, cellSize, seed), radius, 1);
                default: return Hex(radius, cellSize);
            }
        }

        // ==============================================================
        // Periodic lattices
        // ==============================================================
        public static PolygonSoup Hex(float radius, float size)
        {
            var soup = new PolygonSoup();
            float w = size * Mathf.Sqrt(3f), h = size * 1.5f;
            int nx = Mathf.CeilToInt(radius / w) + 2, ny = Mathf.CeilToInt(radius / h) + 2;

            for (int j = -ny; j <= ny; j++)
                for (int i = -nx; i <= nx; i++)
                {
                    Vector2 c = new Vector2(i * w + (j & 1) * w * 0.5f, j * h);
                    if (c.magnitude > radius) continue;
                    soup.Add(Ngon(c, size, 6, 90f), 0);
                }
            return soup;
        }

        public static PolygonSoup TriHex(float radius, float size)
        {
            // Hexagons on a triangular lattice with gaps filled by upward/downward triangles.
            var soup = new PolygonSoup();
            float s = size;                          // hexagon circumradius
            float a = s * Mathf.Sqrt(3f);            // lattice spacing between hex centres
            float w = a * Mathf.Sqrt(3f), h = a * 1.5f;
            int nx = Mathf.CeilToInt(radius / w) + 2, ny = Mathf.CeilToInt(radius / h) + 2;

            var hexCentres = new List<Vector2>();
            for (int j = -ny; j <= ny; j++)
                for (int i = -nx; i <= nx; i++)
                {
                    Vector2 c = new Vector2(i * w + (j & 1) * w * 0.5f, j * h);
                    if (c.magnitude > radius + a) continue;
                    hexCentres.Add(c);
                    soup.Add(Ngon(c, s, 6, 90f), 0);
                }

            // Triangles sit in the interstices: each is bounded by three hex corners.
            var weld = new VertexWelder(1e-3f);
            var corners = new Dictionary<int, Vector2>();
            foreach (var c in hexCentres)
                for (int k = 0; k < 6; k++)
                {
                    Vector2 p = c + Polar(s, 90f + k * 60f);
                    corners[weld.Get(p)] = p;
                }

            float triSide = s;
            var seen = new HashSet<long>();
            foreach (var c in hexCentres)
                for (int k = 0; k < 6; k++)
                {
                    // interstitial triangle centre lies between two adjacent hexes
                    Vector2 dir = Polar(1f, 60f * k);
                    Vector2 tc = c + dir * (a / Mathf.Sqrt(3f));
                    if (tc.magnitude > radius) continue;
                    long key = ((long)Mathf.RoundToInt(tc.x * 1000f) << 32) ^ (uint)Mathf.RoundToInt(tc.y * 1000f);
                    if (!seen.Add(key)) continue;

                    float rot = (k % 2 == 0) ? 180f : 0f;
                    var tri = Ngon(tc, triSide / Mathf.Sqrt(3f), 3, rot);
                    soup.Add(tri, 1);
                }
            return soup;
        }

        public static PolygonSoup Square(float radius, float size)
        {
            var soup = new PolygonSoup();
            int n = Mathf.CeilToInt(radius / size) + 1;
            for (int j = -n; j <= n; j++)
                for (int i = -n; i <= n; i++)
                {
                    Vector2 c = new Vector2((i + 0.5f) * size, (j + 0.5f) * size);
                    if (c.magnitude > radius) continue;
                    float h = size * 0.5f;
                    soup.Add(new[]{ c + new Vector2(-h,-h), c + new Vector2(h,-h),
                                    c + new Vector2(h, h), c + new Vector2(-h, h) }, 0);
                }
            return soup;
        }

        // ==============================================================
        // Penrose P3 — Robinson triangle substitution.
        // Run TilingBuilder.MergeRhombs on the result to recover the rhombs.
        // ==============================================================
        public static PolygonSoup Penrose(float radius, float cellSize)
        {
            // D18 fix: the substitution count used to be a fixed authored constant,
            // so the whole tiling was just uniformly rescaled to `radius` afterward —
            // cell count never grew with panel size. Every other tiling here (Hex,
            // TriHex, Square, Voronoi) uses cellSize to hold local cell size constant
            // while radius controls how far the tiling extends; derive an iteration
            // count that does the same for Penrose. Each substitution divides edge
            // length by phi (the inflation factor), and the seed triangles have unit
            // edges before the final `* scale` below, so solve for the iteration
            // count that brings a unit edge down to approximately cellSize once
            // scaled by `radius`.
            //
            // D21 fix: a quasiperiodic substitution can only land ON one of these
            // phi-quantised edge lengths, never between them — `cellSize` for Penrose
            // means "the target edge length", and the right answer is the iteration
            // count whose resulting edge length is NEAREST that target, not the count
            // that guarantees the tiling is at least as fine as requested. The
            // original `CeilToInt(...) + 1` always rounded up AND added an extra
            // substitution on top, which is why Penrose landed an order of magnitude
            // denser than Hex at the same cellSize (D21, §2.2/§2.3): every fractional
            // log rounded away from cellSize toward finer, and the +1 then halved the
            // edge length again. Rounding to the nearest integer substitution count is
            // the fix; every other tiling here already reports the density the caller
            // asked for, and Penrose can only ever approximate it to within one
            // phi step, same as it does now, just centred on the target instead of
            // always overshooting it.
            int iterations = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Log(Mathf.Max(radius / Mathf.Max(cellSize, 1e-4f), 1f), Phi)),
                1, 12);
            return Penrose(radius, iterations);
        }

        /// <summary>Penrose substitution with an explicit iteration count. Kept for probes
        /// and callers that want to pin the level of detail directly.</summary>
        public static PolygonSoup Penrose(float radius, int iterations)
        {
            var tris = new List<(int type, Vector2 a, Vector2 b, Vector2 c)>();
            for (int i = 0; i < 10; i++)
            {
                Vector2 b = Polar(1f, (2 * i - 1) * 18f);
                Vector2 c = Polar(1f, (2 * i + 1) * 18f);
                if (i % 2 == 0) (b, c) = (c, b);
                tris.Add((0, Vector2.zero, b, c));
            }

            for (int it = 0; it < iterations; it++)
            {
                var next = new List<(int, Vector2, Vector2, Vector2)>(tris.Count * 3);
                foreach (var (type, A, B, C) in tris)
                {
                    if (type == 0)
                    {
                        Vector2 P = A + (B - A) / Phi;
                        next.Add((0, C, P, B));
                        next.Add((1, P, C, A));
                    }
                    else
                    {
                        Vector2 Q = B + (A - B) / Phi;
                        Vector2 R = B + (C - B) / Phi;
                        next.Add((1, R, C, A));
                        next.Add((1, Q, R, B));
                        next.Add((0, R, Q, A));
                    }
                }
                tris = next;
            }

            float scale = radius / 1.0f;
            var soup = new PolygonSoup();
            foreach (var (type, A, B, C) in tris)
                soup.Add(new[] { A * scale, B * scale, C * scale }, type);
            return soup;
        }

        // ==============================================================
        // Site generators
        // ==============================================================
        public static List<Vector2> JitteredSites(float radius, float spacing, int seed)
        {
            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            float w = spacing, h = spacing * 0.866f;
            int nx = Mathf.CeilToInt(radius / w) + 1, ny = Mathf.CeilToInt(radius / h) + 1;
            for (int j = -ny; j <= ny; j++)
                for (int i = -nx; i <= nx; i++)
                {
                    Vector2 p = new Vector2(i * w + (j & 1) * w * 0.5f, j * h);
                    p += new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * spacing * 0.45f;
                    if (p.magnitude < radius * 1.15f) pts.Add(p);
                }
            return pts;
        }

        /// <summary>Chaos game over an IFS. Cell density inherits the attractor's fractal measure.</summary>
        public static List<Vector2> ChaosGame(IfsMap[] maps, float radius, float spacing, int seed)
        {
            int target = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * radius * radius / (spacing * spacing)), 64, 6000);
            var rng = new System.Random(seed);
            float total = 0f; foreach (var m in maps) total += Mathf.Max(m.weight, 1e-4f);

            Vector2 p = Vector2.zero;
            for (int i = 0; i < 64; i++) p = Pick(maps, rng, total).Apply(p);   // burn in

            // Accept points onto a grid so we never emit two sites on top of each other.
            float minDist = spacing * 0.42f;
            var grid = new Dictionary<(int, int), List<Vector2>>();
            float inv = 1f / minDist;
            var pts = new List<Vector2>(target);

            for (int i = 0; i < target * 220 && pts.Count < target; i++)
            {
                p = Pick(maps, rng, total).Apply(p);
                Vector2 q = p * radius;
                if (q.magnitude > radius * 1.1f) continue;

                int gx = Mathf.FloorToInt(q.x * inv), gy = Mathf.FloorToInt(q.y * inv);
                bool tooClose = false;
                for (int dx = -1; dx <= 1 && !tooClose; dx++)
                    for (int dy = -1; dy <= 1 && !tooClose; dy++)
                        if (grid.TryGetValue((gx + dx, gy + dy), out var lst))
                            foreach (var o in lst)
                                if ((o - q).sqrMagnitude < minDist * minDist) { tooClose = true; break; }
                if (tooClose) continue;

                if (!grid.TryGetValue((gx, gy), out var b)) grid[(gx, gy)] = b = new List<Vector2>();
                b.Add(q);
                pts.Add(q);
            }
            return pts;
        }

        static IfsMap Pick(IfsMap[] maps, System.Random rng, float total)
        {
            float r = (float)rng.NextDouble() * total;
            foreach (var m in maps) { r -= Mathf.Max(m.weight, 1e-4f); if (r <= 0f) return m; }
            return maps[maps.Length - 1];
        }

        /// <summary>Five-fold pentaflake attractor. Pairs well with a Penrose outline layer.</summary>
        public static IfsMap[] Pentaflake()
        {
            float r = 1f / (1f + Phi);
            var maps = new IfsMap[5];
            for (int i = 0; i < 5; i++)
                maps[i] = new IfsMap
                {
                    translate = Polar(1f - r, 90f + i * 72f),
                    rotateDeg = 0f,
                    scale = r,
                    weight = 1f
                };
            return maps;
        }

        /// <summary>Sierpinski triangle attractor — sparse, angular density falloff.</summary>
        public static IfsMap[] Sierpinski()
        {
            var maps = new IfsMap[3];
            for (int i = 0; i < 3; i++)
                maps[i] = new IfsMap
                {
                    translate = Polar(0.5f, 90f + i * 120f),
                    rotateDeg = 0f,
                    scale = 0.5f,
                    weight = 1f
                };
            return maps;
        }

        // ==============================================================
        // Voronoi by half-plane clipping. O(n * k) with k nearest neighbours.
        // Brute-force nearest search; fine at init for a few thousand sites.
        // ==============================================================
        public static PolygonSoup Voronoi(List<Vector2> sites, float radius, int lloydPasses)
        {
            for (int pass = 0; pass < lloydPasses; pass++)
            {
                var moved = new List<Vector2>(sites.Count);
                foreach (var cell in VoronoiCells(sites, radius))
                    moved.Add(cell == null ? Vector2.zero : PolygonSoup.Centroid(cell));
                for (int i = 0; i < sites.Count; i++)
                    if (moved[i] != Vector2.zero) sites[i] = moved[i];
            }

            var soup = new PolygonSoup();
            foreach (var cell in VoronoiCells(sites, radius))
            {
                if (cell != null && cell.Count >= 3 && cell.Count <= Limits.MaxVerts)
                    soup.Add(cell.ToArray(), cell.Count % 3);
            }
            return soup;
        }

        static IEnumerable<List<Vector2>> VoronoiCells(List<Vector2> sites, float radius)
        {
            const int K = 24;
            float R = radius * 1.4f;
            var box = new List<Vector2> { new(-R,-R), new(R,-R), new(R,R), new(-R,R) };
            var near = new List<(float d, int i)>(sites.Count);

            for (int i = 0; i < sites.Count; i++)
            {
                Vector2 s = sites[i];
                near.Clear();
                for (int j = 0; j < sites.Count; j++)
                {
                    if (j == i) continue;
                    near.Add(((sites[j] - s).sqrMagnitude, j));
                }
                near.Sort((a, b) => a.d.CompareTo(b.d));

                var poly = new List<Vector2>(box);
                int k = Mathf.Min(K, near.Count);
                for (int m = 0; m < k && poly.Count > 0; m++)
                {
                    Vector2 t = sites[near[m].i];
                    Vector2 n = t - s;
                    float len = n.magnitude;
                    if (len < 1e-6f) continue;
                    n /= len;
                    float d = Vector2.Dot((s + t) * 0.5f, n);
                    poly = ClipHalfPlane(poly, n, d);
                }
                yield return poly.Count >= 3 ? poly : null;
            }
        }

        /// <summary>Sutherland–Hodgman clip; keeps the side where dot(p, n) &lt;= d.</summary>
        static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 n, float d)
        {
            var outp = new List<Vector2>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = Vector2.Dot(a, n) - d, db = Vector2.Dot(b, n) - d;
                bool ina = da <= 0f, inb = db <= 0f;
                if (ina) outp.Add(a);
                if (ina != inb)
                {
                    float t = da / (da - db);
                    outp.Add(Vector2.Lerp(a, b, t));
                }
            }
            return outp;
        }

        // ==============================================================
        static Vector2 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        static Vector2[] Ngon(Vector2 c, float r, int n, float rotDeg)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = c + Polar(r, rotDeg + i * 360f / n);
            return p;
        }
    }
}
