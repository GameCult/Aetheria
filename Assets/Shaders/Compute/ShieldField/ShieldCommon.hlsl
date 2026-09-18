#ifndef SHIELD_COMMON_INCLUDED
#define SHIELD_COMMON_INCLUDED

#define MAX_VERTS 8          // must match Limits.MaxVerts in TilingBuilder.cs
#define TAU 6.28318530718
#define PI  3.14159265359

// ---- fracture pattern ids (must match FracturePattern in ShieldPanel.cs)
#define PAT_RADIAL  0
#define PAT_STAR    1
#define PAT_LANCE   2
#define PAT_SPIRAL  3
#define PAT_CRYSTAL 4
#define PAT_FIZZLE  5

struct CellStatic
{
    float2 center;
    float  radius;
    float  spawnDelay;
    uint   vertStart;
    uint   vertCount;
    uint   nbrStart;
    uint   nbrCount;
    float  toughness;
    uint   tileType;
    uint   splitSeed;
    float  area;       // Laplacian denominator
    float  openW;      // free-surface ghost weight (rim edges)
    float  pad0;
    float  pad1;
    float  pad2;
};

struct DualEdge
{
    uint   nbr;
    float2 mid;
    float2 dir;
    float  len;
    float  w;          // finite-volume weight, len / |c_i - c_j|
};

struct CellState
{
    float3 vel;
    float  spin;
    float3 pos;         // displacement from rest, panel space (z = out of plane)
    float  rot;
    float  damage;      // 0..1 persistent
    float  breakTime;   // <0 while intact
    float  glow;        // transient crack emission
    float  growth;      // 0..1 materialisation
    float  temper;      // remaining surface compression — the "charge" that resists initiation
    float  released;    // stored energy dumped at break; sets fragment fineness
    float  peakTension; // running max, for visualisation
    float  pad2;
};

// ------------------------------------------------------------------
// hashing
// ------------------------------------------------------------------
uint Hash(uint x)
{
    x ^= x >> 16; x *= 0x7feb352du;
    x ^= x >> 15; x *= 0x846ca68bu;
    x ^= x >> 16; return x;
}
float  Hash01(uint x) { return Hash(x) * (1.0 / 4294967296.0); }
float2 Hash2(uint x)  { return float2(Hash01(x), Hash01(x ^ 0x68bc21ebu)); }

// ------------------------------------------------------------------
// shard partitioning
//
// A cell is drawn as a fan of vertCount triangles around its centroid.
// A shard is a contiguous run of those triangles. Split boundaries are
// derived from the cell seed, so every shader agrees without storage.
// ------------------------------------------------------------------
// Fragment count scales with the stored elastic energy released at the moment
// of breaking -- the same reason a highly tempered pane dices into gravel while
// an annealed one splits into a few big shards. `released` is 0 for a cell that
// is merely cracked and not yet broken, so the chords appear before separation.
uint ShardCount(uint seed, float damage, float released, uint vertCount,
                float crackStart, uint maxShards)
{
    if (damage <= crackStart) return 0;
    float t = saturate(max(damage - crackStart, 0.0) / max(1.0 - crackStart, 1e-4));
    t = saturate(t * 0.4 + released * 1.2);
    uint hi = min(vertCount, maxShards);
    if (hi < 2) return 0;
    uint n = (uint)round(lerp(2.0, (float)hi, t * (0.55 + 0.45 * Hash01(seed))));
    return clamp(n, 2u, hi);
}

// Returns the shard index for fan triangle `tri`; startTri receives the
// first triangle of that shard (used as the shard's motion reference).
uint ShardOf(uint seed, uint nSplits, uint vertCount, uint tri, out uint startTri)
{
    uint count = 0, prevB = 0, maxB = 0;
    [loop] for (uint k = 0; k < nSplits; k++)
    {
        uint b = Hash(seed ^ (k * 0x9E3779B9u + 0x2545F491u)) % max(vertCount, 1u);
        maxB = max(maxB, b);
        if (b <= tri) { count++; prevB = max(prevB, b); }
    }
    startTri = (count == 0) ? maxB : prevB;
    return (count == 0) ? (nSplits - 1) : (count - 1);
}

// Is fan edge `ei` (the radial chord from centroid to vertex ei) a shard boundary?
bool IsChord(uint seed, uint nSplits, uint vertCount, uint ei)
{
    [loop] for (uint k = 0; k < nSplits; k++)
        if ((Hash(seed ^ (k * 0x9E3779B9u + 0x2545F491u)) % max(vertCount, 1u)) == ei) return true;
    return false;
}

// ------------------------------------------------------------------
// easing
// ------------------------------------------------------------------
float EaseOutBack(float t)
{
    float u = t - 1.0;
    return 1.0 + 2.70158 * u * u * u + 1.70158 * u * u;
}
float EaseOutCubic(float t) { float u = 1.0 - t; return 1.0 - u * u * u; }

float2 Rot2(float2 p, float a)
{
    float s = sin(a), c = cos(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

#endif
