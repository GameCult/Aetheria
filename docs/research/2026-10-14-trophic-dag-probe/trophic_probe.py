"""Trophic DAG probe (Imagination, aetheria-fauna r4). Pure Python 3, no packages.

Evaluates candidate filament nodes and a layered trophic DAG on the B20 zone (habitat_probe.py's transliteration
of Zone.GetHeight/GetNormal and FlowField, float64), with the wells ORBITING as Zone.GetOrbitPosition moves them
(period = distance seconds, Settings.asset OrbitPeriod exponent 1, multiplier 1). Writes per-layer density images,
a members image per candidate, and prints per candidate: correlation with the B20 Physarum maps, hosted members,
turnover in 1 s / 8 s / 60 s and the largest presence step per 1/60 s (fade band .1). Hashes are the probe's own
integer hash, not CultMath pcg; images are qualitative evidence for the C# law, not the law.

  py -3 trophic_probe.py [grid=256] [only=comma,list]
"""
import sys, os, math, time
ARGS = sys.argv[1:]
sys.argv = [sys.argv[0], "256"]
import habitat_probe as H

OUT = os.path.dirname(os.path.abspath(__file__))
G = int(ARGS[0]) if ARGS else 256
ONLY = ARGS[1].split(",") if len(ARGS) > 1 else None
R = H.R
H.FLOW.update(swirl=6.0, slope=1.5)          # flow-slope-terms-ruled: slope terms on (B25g's values)
TAU = 20.0                                   # Zel'dovich warp time (s): strands bent by the flow, x -> x - TAU u(x)
WEB_TAU = 6.0                                # the lattice web is warped less: its cells are smaller than a strand's run
CYCLE = 8.0                                  # krill cycle period (s), FlowSettings.Period
FADE = .1

def smooth(a, b, v):
    if a == b: return 1.0 if v >= b else 0.0
    v = (v - a) / (b - a); v = 0.0 if v < 0 else 1.0 if v > 1 else v; return v * v * (3 - 2 * v)

# ---------------- L0: environment (wells move) ----------------
def set_time(t):
    for (kind, m, d, ph), w in zip(H.BODIES, H.WELLS):
        a = ph + (2 * math.pi * t / d if d > 0 else 0.0)
        w["pos"] = (math.cos(a) * d, math.sin(a) * d)
    pairs_cache.clear()

def slope(x, z, t):
    n = H.normal(x, z, t); return math.acos(max(-1.0, min(1.0, n[1]))) / (math.pi / 2)

def band(s): return H.deposition(s) / 2.0      # the operator's Deposition(slope), [0, 1]; zero past slope .8
def core(s): return smooth(.7, .8, s)          # steep core mask
def envelope(b): return b ** .75           # the band, widened: where the web may live

def flow_at(x, z, t, sc): return H.flow_xz(x, z, t, sc)

# ---------------- L0.5 filament nodes ----------------
pairs_cache = {}
def pairs(hard):
    """Per-zone site graph over the wells: Gabriel graph, gate soft (margin smoothstep) or hard (0/1).
    Recomputed per tick from orbit positions (the zone already caches those per tick)."""
    if hard in pairs_cache: return pairs_cache[hard]
    W = H.WELLS; out = []
    for i in range(len(W)):
        for j in range(i + 1, len(W)):
            a, b = W[i]["pos"], W[j]["pos"]; L = math.dist(a, b); m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2); r = L / 2
            margin = min([(math.dist(m, W[k]["pos"]) - r) / r for k in range(len(W)) if k not in (i, j)] or [9.0])
            gate = (1.0 if margin > 0 else 0.0) if hard else smooth(-.15, .15, margin)
            if gate > 0: out.append((a, b, L, gate, 28.0 + .03 * L))
    pairs_cache[hard] = out; return out

def seg_dist(p, a, b):
    ax, az = b[0] - a[0], b[1] - a[1]; L2 = ax * ax + az * az
    u = 0.0 if L2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * ax + (p[1] - a[1]) * az) / L2))
    return math.hypot(p[0] - a[0] - u * ax, p[1] - a[1] - u * az)

def strands(q, hard=False, widen=1.0):
    best = 0.0
    for a, b, L, gate, sig in pairs(hard):
        d = seg_dist(q, a, b) / (sig * widen); best = max(best, gate * math.exp(-d * d))
    return best

def warp(x, z, t, sc, tau=None):
    ux, uz = flow_at(x, z, t, sc); tau = TAU if tau is None else tau; return (x - tau * ux, z - tau * uz)

def well_slack(x, z):
    """Multiplicatively weighted Voronoi of the wells (weight = gravity radius): distance to the cell border,
    first order (F2 - F1) / |grad(F2 - F1)|. Borders are Apollonius circles round the small wells."""
    ds = []
    for w in H.WELLS:
        dx, dz = x - w["pos"][0], z - w["pos"][1]; r = math.hypot(dx, dz) or 1e-6
        ds.append((r / w["r"], dx / (r * w["r"]), dz / (r * w["r"])))
    ds.sort()
    (d1, gx1, gz1), (d2, gx2, gz2) = ds[0], ds[1]
    gl = math.hypot(gx2 - gx1, gz2 - gz1) or 1e-9
    return (d2 - d1) / gl

LAT = 260.0      # lattice Voronoi cell (u)
OMEGA = .05      # site drift rate (rad/s), zone time
KW = .35         # smooth-min width (cell units), her w
WEB_AT_CYCLE = False   # read the lattice web's drift at the member's cycle start (as the flow)
WIN = 2          # search window half-size (cells); her reference uses 2
BOOST = 1.6      # site weight raised near wells by up to this factor
EW = .10         # border line half-width (cell units)
def lattice_web(q, t):
    """The operator's reference node: lattice smooth Voronoi, per-site multiplicative weight on distance, smooth-min
    blend. Weight = her hashed .5-1, raised near wells so cells shrink there (denser web on the shoulders).
    Output: 1 on cell borders, falling into the cells, from F1 minus the smooth minimum."""
    if CUR_T0[0] is not None: t = CUR_T0[0]
    px, pz = q[0] / LAT, q[1] / LAT; nx, nz = math.floor(px), math.floor(pz); fx, fz = px - nx, pz - nz
    m = 8.0; f1 = 8.0; f2 = 8.0
    for j in range(-WIN, WIN + 1):
        for i in range(-WIN, WIN + 1):
            cx, cz = nx + i, nz + j
            ox, oz = H.hash01(cx, cz, 11), H.hash01(cx, cz, 12)
            wt = ox * .5 + .5
            sx, sz = (cx + .5) * LAT, (cz + .5) * LAT
            prox = max(math.exp(-(math.dist((sx, sz), w["pos"]) / (2.2 * .103 * w["r"])) ** 2) for w in H.WELLS)
            wt *= 1.0 + BOOST * prox
            ox = .5 + .5 * math.sin(OMEGA * t + 6.2831 * ox); oz = .5 + .5 * math.sin(OMEGA * t + 6.2831 * oz)
            d = math.hypot(i - fx + ox, j - fz + oz) * wt
            if d < f1: f1, f2 = d, f1
            elif d < f2: f2 = d
            h = smooth(0.0, 1.0, .5 + .5 * (m - d) / KW)
            m = m + (d - m) * h - h * (1 - h) * KW / (1 + 3 * KW)
    gap0 = .25 * KW / (1 + 3 * KW)
    soft = max(0.0, min(1.0, (f1 - m) / gap0))           # her smooth-min gap: 1 on borders, soft falloff
    return max(soft ** 2, math.exp(-((f2 - f1) / EW) ** 2))   # F2 - F1 border line of width EW (cell units)

# ---------------- candidate krill habitat densities (filament candidates) ----------------
def env(x, z, t):
    s = slope(x, z, t); return s, band(s), core(s)

def D_band(x, z, t, sc):
    s, b, c = env(x, z, t); return b

def D_skein(x, z, t, sc, hard=False):
    s, b, c = env(x, z, t); q = warp(x, z, t, sc)
    return strands(q, hard) * (1 - c) * (.55 + .45 * b)

def D_skein_hard(x, z, t, sc): return D_skein(x, z, t, sc, True)

def D_lattice(x, z, t, sc):
    s, b, c = env(x, z, t); q = warp(x, z, t, sc, WEB_TAU)
    return lattice_web(q, t) * (1 - c) * envelope(b)

def skein_web(x, z, t, sc):
    """Strands between wells plus the lattice web hanging off them (web gated by a wide strand halo and the band)."""
    s, b, c = env(x, z, t); q = warp(x, z, t, sc)
    st = strands(q); halo = strands(q, widen=4.0)
    web = lattice_web(warp(x, z, t, sc, WEB_TAU), t) * max(halo, b)
    return max(st, .8 * web) * (1 - c), s, b, c

def D_skein_web(x, z, t, sc): return skein_web(x, z, t, sc)[0]

def D_slack(x, z, t, sc):
    s, b, c = env(x, z, t); d = well_slack(x, z); return math.exp(-(d / 70.0) ** 2) * (1 - c)

# ---------------- L1-L3: the trophic DAG over skein_web ----------------
def glim(x, z, t, sc):
    """Producers: starlight (sun's light radius) plus heat-light on warm well floors, fed along the skein."""
    sun = H.WELLS[0]; light = math.exp(-(math.dist((x, z), sun["pos"]) / (300 * 14152 ** .25 * .5)) ** 2)
    heat = sum(math.exp(-(math.dist((x, z), w["pos"]) / (.16 * w["r"])) ** 2) for w in H.WELLS[1:])
    F, s, b, c = skein_web(x, z, t, sc)
    return min(1.0, (.35 * light + .9 * heat) * (.3 + .7 * F) + .35 * F), F, b, c

def holling(p, k=.25): return p * (1 + k) / (p + k)    # Holling II saturation, 0 -> 0, 1 -> 1

def krill(x, z, t, sc):
    g, F, b, c = glim(x, z, t, sc); return F * holling(g) * (1 - c)

RING = [(math.cos(2 * math.pi * k / 8), math.sin(2 * math.pi * k / 8)) for k in range(8)]
def stencil(f, x, z, t, sc, r):
    """Prey density over a hunting radius: the mean of the prey field at the point and 8 taps on a ring of radius r
    (pointwise: 9 evaluations of the layer below, no grid)."""
    return (f(x, z, t, sc) + sum(f(x + r * cx, z + r * cz, t, sc) for cx, cz in RING)) / 9.0

def minnows(x, z, t, sc):
    s, b, c = env(x, z, t); return min(1.0, 1.6 * holling(stencil(krill, x, z, t, sc, 48.0)) * (.4 + .6 * b))

def sharks(x, z, t, sc):
    s, b, c = env(x, z, t); return min(1.0, 2.0 * stencil(minnows, x, z, t, sc, 160.0) * (.3 + .7 * b))

def lanterns(x, z, t, sc):
    return D_slack(x, z, t, sc) * min(1.0, 2.5 * stencil(krill, x, z, t, sc, 120.0))

CANDS = {"band": D_band, "skein": D_skein, "skein-hard": D_skein_hard, "lattice": D_lattice,
         "skein-web": D_skein_web, "slack": D_slack}
LAYERS = {"L1-glim": lambda x, z, t, sc: glim(x, z, t, sc)[0], "L2-krill": krill, "L2-minnows": minnows,
          "L3-sharks": sharks, "L3-lanterns": lanterns}

# ---------------- rendering and measurement ----------------
def grid(f, t, g):
    set_time(t); sc = H.scroll(t); vals = []
    for j in range(g):
        row = []
        for i in range(g):
            x = ((i + .5) / g - .5) * 2 * R; z = ((j + .5) / g - .5) * 2 * R
            row.append(None if x * x + z * z > R * R else f(x, z, t, sc))
        vals.append(row)
    return vals

def save(vals, name, g):
    px = [(0, 0, 0) if v is None else H.ramp(v) for r in vals for v in r]
    H.mark_bodies(px, g); path = os.path.join(OUT, name + ".png"); H.write_png(path, g, g, px); return path

def phys_corr(vals, g):
    phys = [c for r in H.physarum_ascii("physarum A") for c in r]
    bw, bh = g // 64, g // 32; blocks = []
    for by in range(32):
        for bx in range(64):
            cell = [vals[y][x] or 0 for y in range(by * bh, (by + 1) * bh) for x in range(bx * bw, (bx + 1) * bw)]
            blocks.append(sum(cell) / len(cell))
    return H.corr(blocks, phys)

def cand_list(C):
    n = int(R / C); out = []
    for j in range(-n, n):
        for i in range(-n, n):
            x = (i + H.hash01(i, j, 1)) * C; z = (j + H.hash01(i, j, 2)) * C
            if x * x + z * z <= R * R: out.append((i, j, x, z, .1 + .9 * H.hash01(i, j, 3), H.hash01(i, j, 4)))
    return out

def cyc_scroll(t, ph):
    """habitat-no-pop-ruled: the flow term is read at the member's own cycle start."""
    t0 = t - ((t / CYCLE + ph) % 1.0) * CYCLE; return H.scroll(t0), t0

CUR_T0 = [None]
def density_at(f, c, t):
    sc, t0 = cyc_scroll(t, c[5]); CUR_T0[0] = t0 if WEB_AT_CYCLE else None
    try: return f(c[2], c[3], t, sc), t0
    finally: CUR_T0[0] = None

def members(f, cands, t, tref=None):
    set_time(t)
    if tref is None: return {(c[0], c[1]) for c in cands if density_at(f, c, t)[0] > c[4]}
    return {(c[0], c[1]) for c in cands if f(c[2], c[3], t, cyc_scroll(tref, c[5])[0]) > c[4]}   # flow frozen at tref's cycle

def presence(d, th): return smooth(th - FADE, th + FADE, d)

def pop_probe(f, cands, t, nmax=2500):
    set_time(t)
    near = [c for c in cands if abs(density_at(f, c, t)[0] - c[4]) < .3][:nmax]
    prev = {}; worst = 0.0; big = 0
    for k in range(61):
        tt = t + k / 60.0; set_time(tt)
        for c in near:
            d, t0 = density_at(f, c, tt); p = presence(d, c[4]); key = (c[0], c[1])
            if key in prev and prev[key][1] == t0:          # cycle turns excluded (carried by the cycle fade)
                step = abs(p - prev[key][0]); worst = max(worst, step); big += step > .1
            prev[key] = (p, t0)
    return worst, big, len(near)

def gabriel_flips(T=1400.0, dt=1.0 / 60):
    """Hard Gabriel edge-set changes, and the soft gate's largest step per 1/60 s, as the wells orbit."""
    flips = 0; worst = 0.0; prev_h = None; prev_s = None; t = 0.0
    while t <= T:
        set_time(t); W = H.WELLS; hs, ss = set(), {}
        for i in range(len(W)):
            for j in range(i + 1, len(W)):
                a, b = W[i]["pos"], W[j]["pos"]; r = math.dist(a, b) / 2; m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
                mg = min((math.dist(m, W[k]["pos"]) - r) / r for k in range(len(W)) if k not in (i, j))
                if mg > 0: hs.add((i, j))
                ss[(i, j)] = smooth(-.15, .15, mg)
        if prev_h is not None:
            flips += len(hs ^ prev_h); worst = max(worst, max(abs(ss[k] - prev_s[k]) for k in ss))
        prev_h, prev_s = hs, ss; t += dt
    return flips, worst

def main():
    t0 = time.time(); T = 0.0
    print(f"grid {G}, wells orbit (period = distance s), slope terms on, TAU {TAU} s, lattice {LAT} u")
    f, w = gabriel_flips()
    print(f"gabriel over 1400 s: hard edge-set changes {f}; soft gate largest step per 1/60 s {w:.5f}")
    cands = cand_list(16.0); print(f"{len(cands)} krill candidates at 16 u")
    for name, fn in CANDS.items():
        if ONLY and name not in ONLY: continue
        t1 = time.time(); vals = grid(fn, T, G); p = save(vals, "density_" + name, G)
        cr = phys_corr(vals, G)
        a = members(fn, cands, T); b = members(fn, cands, T + 1); c8 = members(fn, cands, T + 8); c60 = members(fn, cands, T + 60)
        n = max(1, len(a))
        W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
        for c in cands:
            if (c[0], c[1]) in a:
                pi_, pj = int((c[2] / (2 * R) + .5) * W2), int((c[3] / (2 * R) + .5) * W2)
                if 0 <= pi_ < W2 and 0 <= pj < W2: px[pj * W2 + pi_] = (235, 230, 170)
        H.mark_bodies(px, W2); H.write_png(os.path.join(OUT, "members_" + name + ".png"), W2, W2, px)
        a0 = members(fn, cands, T, T); w1 = members(fn, cands, T + 1, T); w60 = members(fn, cands, T + 60, T)
        print(f"  {name} wells-only (flow frozen at t's cycle): changed in 1 s {len(a0 ^ w1) / max(1, len(a0)):.4f}, 60 s {len(a0 ^ w60) / max(1, len(a0)):.3f}")
        wst, big, nn = pop_probe(fn, cands, 100.0)
        print(f"{name}: corr physarumA {cr:.3f}; members {len(a)}; changed in 1 s {len(a ^ b) / n:.3f}, 8 s {len(a ^ c8) / n:.3f}, "
              f"60 s {len(a ^ c60) / n:.3f}; presence step max {wst:.4f} ({big} steps > .1, {nn} near-threshold candidates); {time.time() - t1:.0f}s")
    if ONLY is None or "layers" in ONLY:
        for name, fn in LAYERS.items():
            g = G if name.startswith(("L1", "L2-krill")) else G // 2
            t1 = time.time(); vals = grid(fn, T, g); save(vals, "layer_" + name, g)
            print(f"{name}: grid {g}, corr physarumA {phys_corr(vals, g):.3f}; {time.time() - t1:.0f}s")
        # members per layer at its own candidate spacing
        for name, fn, C in (("L2-minnows", minnows, 48.0), ("L3-sharks", sharks, 128.0), ("L3-lanterns", lanterns, 64.0)):
            cs = cand_list(C); t1 = time.time(); a = members(fn, cs, T); b = members(fn, cs, T + 1); c8 = members(fn, cs, T + 8)
            n = max(1, len(a)); W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
            for c in cs:
                if (c[0], c[1]) in a:
                    pi_, pj = int((c[2] / (2 * R) + .5) * W2), int((c[3] / (2 * R) + .5) * W2)
                    for di in range(-1, 2):
                        for dj in range(-1, 2):
                            if 0 <= pi_ + di < W2 and 0 <= pj + dj < W2: px[(pj + dj) * W2 + pi_ + di] = (235, 230, 170)
            H.mark_bodies(px, W2); H.write_png(os.path.join(OUT, "members_" + name + ".png"), W2, W2, px)
            print(f"members {name}: {len(a)} of {len(cs)} at {C:.0f} u; changed in 1 s {len(a ^ b) / n:.3f}, 8 s {len(a ^ c8) / n:.3f}; {time.time() - t1:.0f}s")
        # cost per evaluation (Python, relative): one krill vs one minnow vs one shark evaluation
        set_time(T); sc = H.scroll(T)
        for name, fn in (("krill", krill), ("minnows", minnows), ("sharks", sharks)):
            t1 = time.perf_counter(); [fn(300.0 + k, 200.0, T, sc) for k in range(20)]
            print(f"cost {name}: {(time.perf_counter() - t1) / 20 * 1e3:.2f} ms per evaluation (Python)")
    print(f"total {time.time() - t0:.0f}s")

if __name__ == "__main__": main()
