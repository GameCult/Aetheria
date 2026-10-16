"""Habitat closed-form probe (Imagination, aetheria-fauna r3). Pure Python, no deps.

Transliterates, term for term:
  - FlowField.Global / Slope / Scroll (branch eureka/aetheria-fauna-flow-field @ 5f5a1e4d, FlowField.cs)
  - Zone.GetHeight / GetNormal / PowerPulse / RadialWaves (b836af48, Zone.cs:631-720)
  - the earlier probe's zone: same bodies, same PlanetSettings values (2026-10-13 probe)
Evaluates candidate pointwise habitat densities over the zone, writes a PNG per candidate, member-dot PNGs
under the fixed-threshold (HZD dither) placement, and turnover numbers. Float64 here; C# is float32.
"""
import math, struct, zlib, sys, os, time

OUT = os.path.dirname(os.path.abspath(__file__))
R = 2000.0
G = int(sys.argv[1]) if len(sys.argv) > 1 else 200
GOLD = 1.61803398875

# --- PlanetSettings (earlier probe = Settings.asset values) ---
def curve(m, e, mul, c=0.0): return mul * m ** e + c
BODIES = [("Sun", 14152, 0, 0), ("Planet", 274, 610, .4), ("Planet", 274, 1050, 2.6),
          ("GasGiant", 2260, 1400, 4.4), ("Planet", 120, 1700, 4.6), ("Planet", 29, 450, 3.9)]
WELLS = []
for kind, m, d, ph in BODIES:
    pos = (math.cos(ph) * d, math.sin(ph) * d)
    w = dict(pos=pos, r=curve(m, .25, 500), depth=curve(m, .175, 30), exp=16.0, gas=kind in ("Sun", "GasGiant"))
    if w["gas"]:  # Sun : GasGiant; GasGiantData defaults: radius x1, depth x1, speed x8
        w.update(wr=curve(m, .25, 500), wd=curve(m, .3, .5), wf=curve(m, .45, .2), ws=.025 * 8)
    WELLS.append(w)

def pp(x, e):
    x *= 2; x = max(-1.0, min(1.0, x)); return ((x + 1) * (1 - x)) ** e

def height(x, z, t):
    h = -pp(math.hypot(x, z) / (R * 2), 2) * 64
    for w in WELLS:
        dx, dz = x - w["pos"][0], z - w["pos"][1]
        d2 = dx * dx + dz * dz
        if d2 < w["r"] ** 2: h -= pp(math.sqrt(d2) / w["r"], w["exp"]) * w["depth"]
        if w["gas"] and d2 < w["wr"] ** 2:
            u = math.sqrt(d2) / w["wr"]
            h -= pp(u, 8) * math.cos((u * 2) ** 1.25 * w["wf"] + t * w["ws"]) * w["wd"]
    return h

def normal(x, z, t, step=.1):
    a = height(x - step, z, t) - height(x + step, z, t)
    b = height(x, z - step, t) - height(x, z + step, t)
    n = math.sqrt(a * a + b * b + (2 * step) ** 2)
    return a / n, 2 * step / n, b / n   # .xzy: (x, y=up, z)

# --- FlowField (5f5a1e4d) ---
FLOW = dict(scale=512.0, amp=15.0, speed=.025, slope=0.0, swirl=0.0)
def frac(v): return v - math.floor(v)
def tri(v): return abs(frac(v) - .5)
def tri3(p): return (tri(p[2] + tri(p[1])), tri(p[2] + tri(p[0])), tri(p[1] + tri(p[0])))
def nrm(v):
    l = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2); return (v[0] / l, v[1] / l, v[2] / l)
def scroll(t): s = FLOW["speed"] * t; return frac(s), frac(s * GOLD)
def flow_global(x, y, z, sc):
    f = (x / FLOW["scale"], y / FLOW["scale"], z / FLOW["scale"])
    a = nrm(tri3((f[0], f[1] - sc[0], f[2])))
    b = nrm(tri3((f[0] * GOLD, f[1] * GOLD - sc[1], f[2] * GOLD)))
    c = (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
    A = FLOW["amp"]; return c[0] * A, c[1] * A * .5, c[2] * A
def flow_xz(x, z, t, sc):
    g = flow_global(x, 0.0, z, sc); ux, uz = g[0], g[2]
    if FLOW["slope"] or FLOW["swirl"]:
        n = normal(x, z, t); dx, dz = n[0], n[2]; l = math.hypot(dx, dz)
        if l > 0:
            dx, dz = dx / l, dz / l
            ux += dz * FLOW["swirl"] + dx * FLOW["slope"]; uz += -dx * FLOW["swirl"] + dz * FLOW["slope"]
    return ux, uz

# --- local quantities at a point (all pointwise) ---
E = 6.0   # finite-difference step for flow and Hessian derivatives (flow scale 512)
def smoothstep(a, b, v): v = max(0.0, min(1.0, (v - a) / (b - a))); return v * v * (3 - 2 * v)
def deposition(s):  # the operator's Slime Renderer.prefab Deposition curve
    return 0.0 if s > .8 else 2.0 * smoothstep(0.0, .6, s) ** .8
def sig(v): return 1.0 / (1.0 + math.exp(-max(-40.0, min(40.0, v))))

def locals_at(x, z, t, sc):
    n = normal(x, z, t); s = math.acos(max(-1.0, min(1.0, n[1]))) / (math.pi / 2)
    uxp, uzp = flow_xz(x + E, z, t, sc); uxm, uzm = flow_xz(x - E, z, t, sc)
    uxq, uzq = flow_xz(x, z + E, t, sc); uxr, uzr = flow_xz(x, z - E, t, sc)
    dux_dx, duz_dx = (uxp - uxm) / (2 * E), (uzp - uzm) / (2 * E)
    dux_dz, duz_dz = (uxq - uxr) / (2 * E), (uzq - uzr) / (2 * E)
    div = dux_dx + duz_dz
    sn, ss, om = dux_dx - duz_dz, duz_dx + dux_dz, duz_dx - dux_dz
    W = sn * sn + ss * ss - om * om
    h0 = height(x, z, t)
    hxx = (height(x + E, z, t) - 2 * h0 + height(x - E, z, t)) / E ** 2
    hzz = (height(x, z + E, t) - 2 * h0 + height(x, z - E, t)) / E ** 2
    hxz = (height(x + E, z + E, t) - height(x + E, z - E, t) - height(x - E, z + E, t) + height(x - E, z - E, t)) / (4 * E * E)
    det = hxx * hzz - hxz * hxz
    ux, uz = flow_xz(x, z, t, sc); dx, dz = n[0], n[2]; dl = math.hypot(dx, dz); ul = math.hypot(ux, uz)
    a = 0.0 if dl == 0 or ul == 0 else (ux * dx + uz * dz) / (dl * ul)
    return s, div, W, det, a

# scale constants (set from the grid's own spread on the first pass)
K = dict(div=1.0, W=1.0, det=1.0)
CANDS = {
    "c0_slope_band":   lambda s, div, W, det, a: deposition(s) / 2,
    "c1_windrows":     lambda s, div, W, det, a: deposition(s) / 2 * sig(3 * -div / K["div"]),
    "c2_strain":       lambda s, div, W, det, a: deposition(s) / 2 * sig(3 * W / K["W"]),
    "c3_saddle_links": lambda s, div, W, det, a: (0.0 if s > .8 else 1.0) * sig(3 * -det / K["det"]) * smoothstep(0.02, .25, s),
    "c4_links_windrows": lambda s, div, W, det, a: (0.0 if s > .8 else 1.0) * sig(3 * -det / K["det"]) * smoothstep(0.02, .25, s) * sig(3 * -div / K["div"]),
    # krill hold where the current runs along the well's contour: flow neither climbs nor falls (a ~ 0)
    "c6_band_soft_windrows": lambda s, div, W, det, a: deposition(s) / 2 * sig(1.5 * -div / K["div"]),
    "c7_band_wide_contour": lambda s, div, W, det, a: deposition(s) / 2 * (.25 + .75 * math.exp(-(a / .45) ** 2)),
    "c5_contour_currents": lambda s, div, W, det, a: deposition(s) / 2 * math.exp(-(a / .2) ** 2),
}

def grid_pass(t):
    sc = scroll(t); rows = []
    for j in range(G):
        row = []
        for i in range(G):
            x = ((i + .5) / G - .5) * 2 * R; z = ((j + .5) / G - .5) * 2 * R
            row.append(None if x * x + z * z > R * R else locals_at(x, z, t, sc))
        rows.append(row)
    return rows

def std(vals):
    m = sum(vals) / len(vals); return math.sqrt(sum((v - m) ** 2 for v in vals) / len(vals)) or 1.0

# --- PNG ---
def ramp(v):  # dark navy -> teal -> pale gold
    v = max(0.0, min(1.0, v)); stops = [(0, (10, 14, 30)), (.45, (20, 120, 130)), (.8, (200, 210, 120)), (1, (255, 245, 210))]
    for (a, ca), (b, cb) in zip(stops, stops[1:]):
        if v <= b:
            f = (v - a) / (b - a); return tuple(int(ca[k] + (cb[k] - ca[k]) * f) for k in range(3))
def write_png(path, w, h, px):
    raw = b"".join(b"\x00" + bytes(c for p in px[y * w:(y + 1) * w] for c in p) for y in range(h - 1, -1, -1))
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))

def mark_bodies(px, w):
    for wl in WELLS:
        i = int((wl["pos"][0] / (2 * R) + .5) * w); j = int((wl["pos"][1] / (2 * R) + .5) * w)
        for di in range(-2, 3):
            for dj in range(-2, 3):
                if 0 <= i + di < w and 0 <= j + dj < w: px[(j + dj) * w + i + di] = (255, 60, 60)

# --- physarum A from the 2026-10-13 probe (ASCII 64x32) for a correlation against the slime look ---
def physarum_ascii(name):
    p = r"C:\aeth-wt\fauna-target-r3\docs\research\2026-10-13-fauna-placement-probe\output.txt"
    lines = open(p, encoding="utf-8").read().splitlines(); rampc = " .:-=+*#%@"
    k = next(i for i, l in enumerate(lines) if l.startswith("@@MAP " + name))
    rows = [l for l in lines[k + 1:k + 40] if l.startswith("|")][:32]
    return [[rampc.index(c) for c in r[1:65]] for r in reversed(rows)]   # printed top=y high

def corr(a, b):
    ma, mb = sum(a) / len(a), sum(b) / len(b)
    sa = math.sqrt(sum((v - ma) ** 2 for v in a)); sb = math.sqrt(sum((v - mb) ** 2 for v in b))
    return sum((x - ma) * (y - mb) for x, y in zip(a, b)) / (sa * sb or 1)

def hash01(i, j, salt):
    s = (i * 73856093 ^ j * 19349663 ^ salt * 83492791) & 0xffffffff
    s ^= 2747636419; s = (s * 2654435769) & 0xffffffff; s ^= s >> 16; s = (s * 2654435769) & 0xffffffff; s ^= s >> 16
    s = (s * 2654435769) & 0xffffffff; return s / 4294967295.0

ONLY = sys.argv[3].split(",") if len(sys.argv) > 3 else list(CANDS)
MEMB = sys.argv[4].split(",") if len(sys.argv) > 4 and sys.argv[4] else []
def main():
    tag = sys.argv[2] if len(sys.argv) > 2 else "live"
    if tag == "swirl": FLOW.update(swirl=6.0, slope=1.5)
    t0 = time.time(); T = 100.0
    L = grid_pass(T)
    flat = [v for r in L for v in r if v is not None]
    K["div"] = std([v[1] for v in flat]); K["W"] = std([v[2] for v in flat]); K["det"] = std([v[3] for v in flat])
    print(f"[{tag}] grid {G}x{G} at t={T}: {time.time() - t0:.1f}s; std div={K['div']:.4g} W={K['W']:.4g} det={K['det']:.4g}")
    phys = {n: [c for r in physarum_ascii(n) for c in r] for n in ("physarum A", "physarum B")}
    for name, f in CANDS.items():
        if name not in ONLY: continue
        vals = [[None if v is None else f(*v) for v in r] for r in L]
        mx = max(v for r in vals for v in r if v is not None) or 1
        px = [(0, 0, 0) if v is None else ramp(v / mx) for r in vals for v in r]
        mark_bodies(px, G); path = os.path.join(OUT, f"density_{name}_{tag}.png"); write_png(path, G, G, px)
        # 64x32 block means for correlation with the physarum maps
        bw, bh = G // 64, G // 32; blocks = []
        for by in range(32):
            for bx in range(64):
                cell = [vals[y][x] or 0 for y in range(by * bh, (by + 1) * bh) for x in range(bx * bw, (bx + 1) * bw)]
                blocks.append(sum(cell) / len(cell))
        cov = sum(1 for v in flat if f(*v) > .5 * mx) / len(flat)
        print(f"  {name}: png {path}\n    corr physarumA={corr(blocks, phys['physarum A']):.3f} physarumB={corr(blocks, phys['physarum B']):.3f} area>half-max={cov:.3f}")
    # members under fixed per-candidate thresholds (HZD dither shape): jittered lattice, cell C, hashed threshold
    C = 16.0; n = int(2 * R / C); P = 8.0
    for name in MEMB:
        f = CANDS[name]; mx = None; members = {}; t1 = time.time()
        for tt in (T, T + 1.0, T + P):
            sc = scroll(tt); m = set(); dens = {}
            for j in range(n):
                for i in range(n):
                    x = (i + hash01(i, j, 1)) * C - R; z = (j + hash01(i, j, 2)) * C - R
                    if x * x + z * z > R * R: continue
                    d = f(*locals_at(x, z, tt, sc)); dens[(i, j)] = d
            if mx is None: mx = max(dens.values()) or 1
            for k, d in dens.items():
                if d / mx > hash01(k[0], k[1], 3) ** .5 * .9 + .1: m.add(k)   # threshold in (.1, 1)
            members[tt] = m
        a, b, c = members[T], members[T + 1.0], members[T + P]
        print(f"  members {name}: {len(a)} of ~{int(math.pi * (R / C) ** 2)} candidates; changed in 1 s {len(a ^ b) / max(1, len(a)):.3f}, in {P:.0f} s {len(a ^ c) / max(1, len(a)):.3f} ({time.time() - t1:.0f}s)")
        W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
        for (i, j) in a:
            x = (i + hash01(i, j, 1)) * C - R; z = (j + hash01(i, j, 2)) * C - R
            pi_, pj = int((x / (2 * R) + .5) * W2), int((z / (2 * R) + .5) * W2)
            if 0 <= pi_ < W2 and 0 <= pj < W2: px[pj * W2 + pi_] = (235, 230, 170)
        mark_bodies(px, W2); path = os.path.join(OUT, f"members_{name}_{tag}.png"); write_png(path, W2, W2, px); print(f"    png {path}")

if __name__ == "__main__": main()
