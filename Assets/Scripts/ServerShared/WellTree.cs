using System;
using System.Collections.Generic;
using GameCult.Caching;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

/// <summary>
/// An orbit as the well tree reads it: where it hangs on the tree and the mass of the well-bearing body on it (0 for none).
/// Extent is the outermost distance from the parent of anything riding this orbit's rail (a belt orbit: its outermost
/// asteroid; any other orbit: its Distance). Anchor is a parentless orbit's stationary position, ignored with a parent.
/// </summary>
public readonly struct WellNode
{
    public readonly CultRecordKey Key, Parent;
    public readonly float Distance, Phase, Mass, Extent;
    public readonly float2 Anchor;

    public WellNode(CultRecordKey key, CultRecordKey parent, float distance, float phase, float mass, float extent, float2 anchor)
    {
        Key = key;
        Parent = parent;
        Distance = distance;
        Phase = phase;
        Mass = mass;
        Extent = extent;
        Anchor = anchor;
    }
}

/// <summary>
/// The one owner of which gravity terms a zone has: the orbit tree turned into band terms (GravityField is the law of one
/// term). Built once from the pack's orbits and the authored curves, never reading time; the caller passes positions.
///
/// Every node F (a body, or a bodyless node with its ring group standing in for the body) contributes at its own position
///   c(m_F) U(eps(m_F), L_F, B_F) + c(M_F) [ U(eps(M_F), max(B_F, Gamma Lam_F), Lam_F) - U(eps(M_F), L_F, B_F) ]
/// m_F is the body's mass, M_F the family's, c and eps the WellStrength and RocheLimit curves. L_F is the outermost
/// child's extent: inside it the node pulls exactly as its body alone. B_F, the support, bounds every descendant term:
/// beyond it the family pulls exactly as one body of mass M_F, out to max(B_F, Gamma Lam_F), and is gone at its reach
/// Lam_F. A zone's sole massive root has infinite reach, so its far field is exactly c(M_zone). A zone of several massive
/// roots (subzones) has one zone node, which is not an orbit: at the roots' centre of mass it owns the only global term,
/// c(M_zone) U_global less c(M_zone) U(eps, A, Beta A), nothing inside A and the whole zone as one body beyond Beta A;
/// each root is bounded to its own subzone (SubzoneReach).
/// A body with no massive descendants is one term c(m) U(eps(m), Gamma Lam, Lam). Beyond a node's reach its whole
/// subtree contributes exactly nothing, so Force and Potential skip it: the walk is exact, not an approximation.
/// </summary>
public sealed class WellTree
{
    /// <summary>Orbits whose distances differ by no more than this are on one ring (a binary, a ring of siblings).</summary>
    public const float RingWindow = .1f;

    /// <summary>A band term of the field: Strength x GravityField's U(Softening, Inner, Outer) at Node's position, less Reference.</summary>
    public readonly struct Term
    {
        public readonly CultRecordKey Node;
        public readonly float Strength, Softening, Inner, Outer, Reference;

        public Term(CultRecordKey node, float strength, float softening, float inner, float outer, float reference)
        {
            Node = node;
            Strength = strength;
            Softening = softening;
            Inner = inner;
            Outer = outer;
            Reference = reference;
        }
    }

    private readonly Dictionary<CultRecordKey, int> _index = new Dictionary<CultRecordKey, int>();
    private CultRecordKey[] _key;
    private int[][] _kids;
    private int[] _roots;
    private int[][] _ring;
    private int[] _termStart, _termCount;
    private float[] _distance, _phase, _reach, _support, _extent;
    private float2[] _anchor;
    private int[] _massiveRoots;
    private float _packRadius;
    private int _zoneStart, _zoneCount;
    private Term[] _terms;

    /// <summary>Where the zone node's terms sit (the family-mass-weighted mean of the massive roots' anchors); meaningful when it has terms.</summary>
    public float2 Centre { get; private set; }

    /// <summary>Every term of the zone, grouped by node, the zone node's last (its Node is unset: it sits at Centre): what the lowering uploads and the shader sums.</summary>
    public IReadOnlyList<Term> Terms => _terms;

    private WellTree() { }

    /// <summary>A node's reach in its parent's field (+inf for a zone's sole massive root; a subzone's radius for a root of several); 0 for an orbit that carries no mass.</summary>
    public float Reach(CultRecordKey node) => _reach[_index[node]];

    /// <summary>A node's support B: every term of its descendants lies inside it, so beyond it the family is one body.</summary>
    public float Support(CultRecordKey node) => _support[_index[node]];

    /// <summary>
    /// A well's strength and softening at a mass, from the authored curves (the one place they are read and checked).
    /// A mass of 0 is no well.
    /// </summary>
    public static (float Strength, float Softening) Curves(PlanetSettings settings, float mass)
    {
        var strength = settings.WellStrength.Evaluate(mass);
        var softening = settings.RocheLimit.Evaluate(mass);
        if (!(strength >= 0f) || float.IsInfinity(strength))
            throw new InvalidOperationException("A body's well strength must be finite and not negative (PlanetSettings.WellStrength).");
        if (strength > 0 && !(softening > 0f && !float.IsInfinity(softening)))
            throw new InvalidOperationException("A body's Roche limit, which is its softening, must be positive and finite (PlanetSettings.RocheLimit).");
        return (strength, softening);
    }

    /// <summary>
    /// The tree of a zone's orbits. `packRadius` is where the global terms are zero: each carries its own potential there
    /// as its Reference (a bounded term is already 0 beyond its outer edge, so it needs none).
    /// </summary>
    public static WellTree Build(IReadOnlyList<WellNode> nodes, PlanetSettings settings, float packRadius)
    {
        var tree = new WellTree();
        var n = nodes.Count;
        tree._key = new CultRecordKey[n];
        tree._distance = new float[n];
        tree._phase = new float[n];
        tree._extent = new float[n];
        tree._anchor = new float2[n];
        tree._packRadius = packRadius;
        for (var i = 0; i < n; i++)
        {
            tree._extent[i] = nodes[i].Extent;
            tree._anchor[i] = nodes[i].Anchor;
            tree._index[nodes[i].Key] = i;
            tree._key[i] = nodes[i].Key;
            tree._distance[i] = nodes[i].Distance;
            tree._phase[i] = nodes[i].Phase;
        }

        var parent = new int[n];
        var kids = new List<int>[n];
        var depth = new int[n];
        for (var i = 0; i < n; i++) kids[i] = new List<int>();
        for (var i = 0; i < n; i++)
        {
            parent[i] = nodes[i].Parent.IsSet() && tree._index.TryGetValue(nodes[i].Parent, out var p) ? p : -1;
            if (parent[i] >= 0) kids[parent[i]].Add(i);
        }
        for (var i = 0; i < n; i++)
            for (var a = parent[i]; a >= 0; a = parent[a]) depth[i]++;
        var roots = new List<int>();
        for (var i = 0; i < n; i++)
            if (parent[i] < 0) roots.Add(i);
        tree._roots = roots.ToArray();
        var deepestFirst = new int[n];
        for (var i = 0; i < n; i++) deepestFirst[i] = i;
        Array.Sort(deepestFirst, (x, y) => depth[y] != depth[x] ? depth[y].CompareTo(depth[x]) : x.CompareTo(y));

        var body = new float[n];      // the body's own mass
        var family = new float[n];    // the family's mass M_F
        for (var i = 0; i < n; i++) family[i] = body[i] = nodes[i].Mass;
        foreach (var i in deepestFirst)
            if (parent[i] >= 0) family[parent[i]] += family[i];

        // Massive roots are the zone's subzones; with two or more, their centre of mass is the zone node's position.
        var massive = new List<int>();
        foreach (var r in roots)
            if (family[r] > 0) massive.Add(r);
        tree._massiveRoots = massive.ToArray();
        var centre = float2.zero;
        var total = 0f;
        foreach (var r in massive)
        {
            centre += tree._anchor[r] * family[r];
            total += family[r];
        }
        tree.Centre = total > 0f ? centre / total : float2.zero;

        tree._kids = new int[n][];
        var ring = tree._ring = new int[n][];
        for (var i = 0; i < n; i++)
        {
            tree._kids[i] = kids[i].ToArray();
            ring[i] = RingOf(kids[i], family, tree._distance);
        }

        BuildReaches(tree, parent, body, family, ring, deepestFirst, out var lower, out var group);
        for (var i = 0; i < n; i++)
            if (body[i] > 0) ring[i] = null; // a ring stands in for a body only on a bodyless node
        tree.EmitTerms(parent, body, family, ring, lower, group, settings, packRadius);
        return tree;
    }

    // The massive children of a node at its innermost massive distance, within RingWindow of it; null when there are none.
    private static int[] RingOf(List<int> kids, float[] family, float[] distance)
    {
        var inner = float.PositiveInfinity;
        foreach (var k in kids)
            if (family[k] > 0) inner = Math.Min(inner, distance[k]);
        if (float.IsPositiveInfinity(inner)) return null;
        var members = new List<int>();
        foreach (var k in kids)
            if (family[k] > 0 && distance[k] - inner <= RingWindow) members.Add(k);
        return members.ToArray();
    }

    // The massive children of a bodyless node that stand in for its body: their mass, the ring's distance and its support.
    private readonly struct RingGroup
    {
        public readonly float Mass, Inner, Support;
        public RingGroup(float mass, float inner, float support)
        {
            Mass = mass;
            Inner = inner;
            Support = support;
        }
    }

    // Children before parents: a family's support needs its children's reaches. lower = L_F, the outermost child's extent.
    private static void BuildReaches(WellTree t, int[] parent, float[] body, float[] family, int[][] ring, int[] order,
        out float[] lower, out RingGroup[] group)
    {
        var n = parent.Length;
        var d = t._distance;
        lower = new float[n];
        group = new RingGroup[n];
        t._support = new float[n];
        t._reach = new float[n];
        foreach (var i in order)
        {
            var low = 0f;
            foreach (var k in t._kids[i]) low = Math.Max(low, t._extent[k]);
            var support = GravityField.Beta * low;
            foreach (var k in t._kids[i]) support = Math.Max(support, t._extent[k] + t._reach[k]);
            if (family[i] > 0 && body[i] <= 0)
            {
                // Bodyless: its ring group stands in for a body (it has massive children, or it would carry no mass).
                var inner = float.PositiveInfinity;
                var mass = 0f;
                foreach (var j in ring[i])
                {
                    inner = Math.Min(inner, d[j]);
                    mass += family[j];
                }
                var ringSupport = GravityField.Beta * inner;
                foreach (var j in ring[i]) ringSupport = Math.Max(ringSupport, d[j] + t._reach[j]);
                low = Math.Max(low, ringSupport);
                support = Math.Max(support, GravityField.Beta * low);
                group[i] = new RingGroup(mass, inner, ringSupport);
            }
            lower[i] = low;
            t._support[i] = support;
            if (family[i] <= 0) continue;
            var floor = GravityField.Beta * support;
            var p = parent[i];
            float reach;
            if (p < 0) reach = t.SubzoneReach(i, floor);
            else if (body[p] <= 0 && ring[p] != null && ring[p].Length >= 2 && Array.IndexOf(ring[p], i) >= 0)
                reach = Math.Max(GravityField.RingReach * d[i], floor);
            else reach = Math.Max(GravityField.Reach * d[i], floor);
            t._reach[i] = reach > 0f ? reach : 0f;
        }
    }

    // A root's reach: the floor (Beta x support) or the subzone reach, whichever is more. The subzone shape is decided in the
    // public static SubzoneReach (multi-root-isolated-subzones): the generator calls the same function, so Reach x the nearest
    // root's distance is spelled once and the roots' windows are fitted to it (root-window-floor).
    private float SubzoneReach(int root, float floor)
    {
        var anchors = new List<float2>();
        foreach (var other in _massiveRoots) anchors.Add(_anchor[other]);
        return Math.Max(SubzoneReach(anchors, Array.IndexOf(_massiveRoots, root)), floor);
    }

    /// <summary>
    /// A subzone root's reach among the zone's massive roots' anchors: a sole massive root is the whole zone (+inf, so its
    /// global term is the far field); with several, each root is bounded to GravityField.Reach x the distance to the nearest
    /// other root (multi-root-isolated-subzones). The superposed-inside option is not a change to this return: it needs every
    /// root's support, and roots are processed one at a time.
    /// </summary>
    public static float SubzoneReach(IReadOnlyList<float2> anchors, int root)
    {
        if (anchors.Count < 2) return float.PositiveInfinity;
        var nearest = float.PositiveInfinity;
        for (var other = 0; other < anchors.Count; other++)
            if (other != root) nearest = Math.Min(nearest, length(anchors[other] - anchors[root]));
        return GravityField.Reach * nearest;
    }

    private void EmitTerms(int[] parent, float[] body, float[] family, int[][] ring, float[] lower, RingGroup[] group,
        PlanetSettings settings, float packRadius)
    {
        var n = parent.Length;
        var terms = new List<Term>();
        _termStart = new int[n];
        _termCount = new int[n];
        for (var i = 0; i < n; i++)
        {
            _termStart[i] = terms.Count;
            if (family[i] <= 0 || !(_reach[i] > 0f)) continue;
            var global = float.IsPositiveInfinity(_reach[i]);
            var fadeFrom = global ? float.PositiveInfinity : Math.Max(_support[i], GravityField.Gamma * _reach[i]);
            void Add(float strength, float softening, float inner, float outer) => terms.Add(new Term(_key[i], strength, softening, inner,
                outer, float.IsPositiveInfinity(outer) ? strength * GravityField.Potential(softening, inner, outer, packRadius) : 0f));
            if (body[i] > 0 && family[i] <= body[i])
            {
                var (c, eps) = Curves(settings, body[i]);
                Add(c, eps, fadeFrom, _reach[i]);
                _termCount[i] = terms.Count - _termStart[i];
                continue;
            }
            if (body[i] > 0)
            {
                var (c, eps) = Curves(settings, body[i]);
                Add(c, eps, lower[i], _support[i]);
            }
            else
            {
                var (c, eps) = Curves(settings, group[i].Mass);
                Add(c, eps, lower[i], _support[i]);
                Add(-c, eps, group[i].Inner, group[i].Support);
            }
            var (cf, ef) = Curves(settings, family[i]);
            Add(cf, ef, fadeFrom, _reach[i]);
            Add(-cf, ef, lower[i], _support[i]);
            _termCount[i] = terms.Count - _termStart[i];
        }
        _zoneStart = terms.Count;
        if (_massiveRoots.Length >= 2)
        {
            var total = 0f;
            var span = 0f;
            foreach (var r in _massiveRoots)
            {
                total += family[r];
                span = Math.Max(span, length(_anchor[r] - Centre) + _reach[r]);
            }
            var (c, eps) = Curves(settings, total);
            terms.Add(new Term(default, c, eps, float.PositiveInfinity, float.PositiveInfinity,
                c * GravityField.Potential(eps, float.PositiveInfinity, float.PositiveInfinity, packRadius)));
            if (span > 0f) terms.Add(new Term(default, -c, eps, span, GravityField.Beta * span, 0f));
        }
        _zoneCount = terms.Count - _zoneStart;
        _terms = terms.ToArray();
    }

    // One node's own terms at distance r from it, toward its centre.
    private float PullOf(int node, float r) => PullOf(_termStart[node], _termCount[node], r);

    private float PullOf(int start, int count, float r)
    {
        var pull = 0f;
        for (var t = start; t < start + count; t++)
        {
            var term = _terms[t];
            pull += term.Strength * GravityField.Pull(term.Softening, term.Inner, term.Outer, r);
        }
        return pull;
    }

    /// <summary>
    /// The field's force at p (toward the wells, u/s^2): every node's terms, walked from the roots; a subtree is skipped
    /// the moment p is outside its reach, which is exact. `positionAt(orbit, time)` is the caller's, so this never owns time.
    /// </summary>
    public float2 Force(float2 p, Func<CultRecordKey, double, float2> positionAt, double time)
    {
        var force = float2.zero;
        var toZone = Centre - p;
        var rz = length(toZone);
        if (_zoneCount > 0 && rz > 0f) force += toZone / rz * PullOf(_zoneStart, _zoneCount, rz);
        foreach (var root in _roots) ForceFrom(root, p, positionAt, time, ref force);
        return force;
    }

    private void ForceFrom(int node, float2 p, Func<CultRecordKey, double, float2> positionAt, double time, ref float2 force)
    {
        var offset = positionAt(_key[node], time) - p;
        var r = length(offset);
        if (r > 0f)
            force += offset / r * PullOf(node, r);
        foreach (var kid in _kids[node])
            if (length(positionAt(_key[kid], time) - p) < _reach[kid]) ForceFrom(kid, p, positionAt, time, ref force);
    }

    /// <summary>The field's potential at p: every node's terms less their references, walked as Force walks.</summary>
    public float Potential(float2 p, Func<CultRecordKey, double, float2> positionAt, double time)
    {
        var sum = 0f;
        var rz = length(Centre - p);
        for (var t = _zoneStart; t < _zoneStart + _zoneCount; t++)
            sum += _terms[t].Strength * GravityField.Potential(_terms[t].Softening, _terms[t].Inner, _terms[t].Outer, rz) - _terms[t].Reference;
        foreach (var root in _roots) sum += PotentialFrom(root, p, positionAt, time);
        return sum;
    }

    private float PotentialFrom(int node, float2 p, Func<CultRecordKey, double, float2> positionAt, double time)
    {
        var r = length(positionAt(_key[node], time) - p);
        var sum = 0f;
        for (var t = _termStart[node]; t < _termStart[node] + _termCount[node]; t++)
        {
            var term = _terms[t];
            sum += term.Strength * GravityField.Potential(term.Softening, term.Inner, term.Outer, r) - term.Reference;
        }
        foreach (var kid in _kids[node])
            if (length(positionAt(_key[kid], time) - p) < _reach[kid]) sum += PotentialFrom(kid, p, positionAt, time);
        return sum;
    }

    /// <summary>
    /// The pull (toward the node, u/s^2) a rail at `distance` round `parentOrbit` is given: the node's own terms at that
    /// distance. A ring member of a bodyless node adds each other member's own terms at their chord (positions at t = 0),
    /// projected toward the node. Infinite period for a pull of 0 is the caller's.
    /// </summary>
    public float PullAround(CultRecordKey parentOrbit, float distance, CultRecordKey self)
    {
        var node = _index[parentOrbit];
        var pull = PullOf(node, distance);
        var members = _ring[node];
        if (members == null || members.Length < 2) return pull;
        if (_index.TryGetValue(self, out var me) && Array.IndexOf(members, me) >= 0) return pull + PartnersPull(members, me);
        return pull;
    }

    // What the other members of a ring pull member m with, along the direction from m to the node.
    private float PartnersPull(int[] members, int m)
    {
        var at = _distance[m] * OrbitData.Evaluate(_phase[m]);
        var sum = 0f;
        foreach (var q in members)
        {
            if (q == m) continue;
            var chord = _distance[q] * OrbitData.Evaluate(_phase[q]) - at;
            var r = length(chord);
            if (r <= 0f || _distance[m] <= 0f) continue;
            sum += PullOf(q, r) * dot(chord / r, -at / _distance[m]);
        }
        return sum;
    }
}
