"""Body-Voronoi probe (Imagination, aetheria-fauna r4). Pure Python 3, no packages.

The operator's sketch after skein-web was ruled: the Voronoi cells are the gravity bodies themselves, and the
distance metric is the place to play. The large-scale filament layer here is a diagram over the zone's bodies
(sites = bodies, orbiting), drawn as an F2 - F1 edge profile: distance to the border to first order,
(F2 - F1) / |grad(F2 - F1)| through a Gaussian of width EW_U. The lattice web of trophic_probe.py hangs off it, its
per-site weight boosted by the large edge's profile at each lattice site (cells shrink near the edges) and its
metric stretched across the edge normal at the query point (cells align along the edges).

Reuses trophic_probe.py (orbiting wells, slope terms on, the flow warp, the lattice web, members and pop probes) and
habitat_probe.py (Zone.GetHeight/GetNormal and FlowField transliterations). Hashes are the probe's own.

  py -3 -u body_voronoi_probe.py [grid=256] [only=comma,list]
"""
import sys, os, math, time
ARGS = sys.argv[1:]
sys.argv = [sys.argv[0], "256"]
import trophic_probe as T
H = T.H

OUT = os.path.dirname(os.path.abspath(__file__))
G = int(ARGS[0]) if ARGS else 256
ONLY = ARGS[1].split(",") if len(ARGS) > 1 else None
T.WEB_AT_CYCLE = True        # ruled with skein-web: flow and web drift read at the member's cycle start
EW_U = 40.0                  # large edge half-width (u)
HALO = 4.0                   # web hangs within this many edge widths of a large edge (or on the band)
ANISO = 2.5                  # flow-stretched metric: distance across the flow counts this many times more
ALIGN = 1.0                  # lattice cells: across-edge component stretched by up to 1 + ALIGN near a large edge
VISITS = [0]                 # sites visited per evaluation (the CultMath cost measure)

# sigma: each well's Gaussian width. Zone's term pp(d/r)^16 = (1 - 4 d^2/r^2)^16 ~ exp(-64 d^2/r^2) = exp(-d^2 / 2 s^2)
SIG = [w["r"] / math.sqrt(128.0) for w in H.WELLS]
MASS = [b[1] for b in H.BODIES]

# ---------------- per-site metrics: d_i(q, flow direction) ----------------
def m_mult(i, dx, dz, fd): VISITS[0] += 1; return math.hypot(dx, dz) / SIG[i]
def m_add(i, dx, dz, fd): VISITS[0] += 1; return math.hypot(dx, dz) - SIG[i]
def m_power(i, dx, dz, fd): VISITS[0] += 1; return dx * dx + dz * dz - SIG[i] ** 2
def m_newton(i, dx, dz, fd): VISITS[0] += 1; return math.hypot(dx, dz) / MASS[i]     # potential m/d equal: w = mass
def m_well(i, dx, dz, fd):
    """Equal well depth on the zone's real profile: depth_i * pp(d/r_i)^16 equal (its log, so smaller is deeper)."""
    VISITS[0] += 1; w = H.WELLS[i]; u = 4 * (dx * dx + dz * dz) / w["r"] ** 2
    return 1e9 if u >= 1 else -(math.log(w["depth"]) + w["exp"] * math.log(1 - u))
def m_aniso(i, dx, dz, fd):
    VISITS[0] += 1; a = dx * fd[0] + dz * fd[1]; c = -dx * fd[1] + dz * fd[0]
    return math.sqrt(a * a + (ANISO * c) ** 2) / SIG[i]
def m_lp(p):
    def f(i, dx, dz, fd):
        VISITS[0] += 1
        if p == math.inf: return max(abs(dx), abs(dz)) / SIG[i]
        return (abs(dx) ** p + abs(dz) ** p) ** (1 / p) / SIG[i]
    return f

def body_border(q, metric, fd=(1.0, 0.0)):
    """Distance (u) from q to the nearest border of the body diagram under `metric`, first order, and the border's
    unit normal (grad of F2 - F1). Visits every body once; the gradient re-evaluates only the two winners."""
    ds = sorted((metric(i, q[0] - w["pos"][0], q[1] - w["pos"][1], fd), i) for i, w in enumerate(H.WELLS))
    (d1, i1), (d2, i2) = ds[0], ds[1]
    if d2 >= 1e8: return 1e9, (1.0, 0.0)
    def g(x, z):
        a, b = H.WELLS[i1]["pos"], H.WELLS[i2]["pos"]
        return metric(i2, x - b[0], z - b[1], fd) - metric(i1, x - a[0], z - a[1], fd)
    e = 1.0; v = VISITS[0]
    gx = (g(q[0] + e, q[1]) - g(q[0] - e, q[1])) / (2 * e); gz = (g(q[0], q[1] + e) - g(q[0], q[1] - e)) / (2 * e)
    VISITS[0] = v                       # the gradient is analytic in C#; the probe's finite difference is not counted
    gl = math.hypot(gx, gz) or 1e-9
    return (d2 - d1) / gl, (gx / gl, gz / gl)

def edge_profile(dist, widen=1.0): return math.exp(-(dist / (EW_U * widen)) ** 2)

# ---------------- the lattice web, weights pulled toward the large edges ----------------
SITE_W = {}                          # per tick: lattice site -> large-edge profile at its centre (cleared by set_time)
_set_time = T.set_time
def set_time(t): SITE_W.clear(); _set_time(t)
T.set_time = set_time

def lattice_web_edges(q, t, metric, near, nrm):
    """trophic_probe.lattice_web with the well-proximity boost replaced by the large-edge profile at each lattice
    site, and the cell metric stretched by (1 + ALIGN * near) across the large edge's normal at the query point."""
    if T.CUR_T0[0] is not None: t = T.CUR_T0[0]
    LAT = T.LAT; px, pz = q[0] / LAT, q[1] / LAT; nx, nz = math.floor(px), math.floor(pz); fx, fz = px - nx, pz - nz
    st = 1.0 + ALIGN * near; m = 8.0; f1 = 8.0; f2 = 8.0; KW = T.KW
    for j in range(-T.WIN, T.WIN + 1):
        for i in range(-T.WIN, T.WIN + 1):
            cx, cz = nx + i, nz + j; VISITS[0] += 1
            ox, oz = H.hash01(cx, cz, 11), H.hash01(cx, cz, 12); wt = ox * .5 + .5
            key = (cx, cz, metric)
            if key not in SITE_W:
                SITE_W[key] = edge_profile(body_border(((cx + .5) * LAT, (cz + .5) * LAT), metric)[0], 3.0)
            else: VISITS[0] += len(H.WELLS)          # count the per-site evaluation even when the probe memoises it
            wt *= 1.0 + T.BOOST * SITE_W[key]
            ox = .5 + .5 * math.sin(T.OMEGA * t + 6.2831 * ox); oz = .5 + .5 * math.sin(T.OMEGA * t + 6.2831 * oz)
            vx, vz = i - fx + ox, j - fz + oz
            a = vx * nrm[0] + vz * nrm[1]; c = -vx * nrm[1] + vz * nrm[0]
            d = math.hypot(st * a, c) * wt
            if d < f1: f1, f2 = d, f1
            elif d < f2: f2 = d
            h = T.smooth(0.0, 1.0, .5 + .5 * (m - d) / KW)
            m = m + (d - m) * h - h * (1 - h) * KW / (1 + 3 * KW)
    gap0 = .25 * KW / (1 + 3 * KW)
    soft = max(0.0, min(1.0, (f1 - m) / gap0))
    return max(soft ** 2, math.exp(-((f2 - f1) / T.EW) ** 2))

WARP_AT_CYCLE = os.environ.get("WARP_AT_CYCLE") == "1"
_warp = T.warp
if WARP_AT_CYCLE: OUT = os.path.join(OUT, "cyc"); os.makedirs(OUT, exist_ok=True)
def warp(x, z, t, sc, tau=None):
    """WARP_AT_CYCLE=1: the whole flow warp at the member's cycle start, slope terms included (the wells at t0).
    trophic_probe reads only the scroll at cycle start; its slope terms read the wells now (B34's runs)."""
    t0 = T.CUR_T0[0]
    if not WARP_AT_CYCLE or t0 is None: return _warp(x, z, t, sc, tau)
    keep = [w["pos"] for w in H.WELLS]
    for (kind, m, d, ph), w in zip(H.BODIES, H.WELLS):
        a = ph + (2 * math.pi * t0 / d if d > 0 else 0.0); w["pos"] = (math.cos(a) * d, math.sin(a) * d)
    try: return _warp(x, z, t0, sc, tau)
    finally:
        for w, p in zip(H.WELLS, keep): w["pos"] = p
T.warp = warp

def flow_dir_of(x, z, q):
    """The flow direction the warp used: u = (x - q) / TAU, so it is read when the warp is."""
    ux, uz = x - q[0], z - q[1]; l = math.hypot(ux, uz) or 1e-9; return ux / l, uz / l

def flow_dir(x, z, t, sc):
    ux, uz = T.flow_at(x, z, t, sc); l = math.hypot(ux, uz) or 1e-9; return ux / l, uz / l

def body_web(metric, strands=False):
    def D(x, z, t, sc):
        s, b, c = T.env(x, z, t); q = T.warp(x, z, t, sc)
        fd = flow_dir_of(x, z, q) if metric is m_aniso else (1.0, 0.0)
        dist, nrm = body_border(q, metric, fd); e = edge_profile(dist); halo = edge_profile(dist, HALO)
        big = e; near = halo
        if strands:
            st = T.strands(q); sh = T.strands(q, widen=4.0); VISITS[0] += 2 * len(T.pairs(False))
            big = max(e, st); near = max(halo, sh)
        web = lattice_web_edges(T.warp(x, z, t, sc, T.WEB_TAU), t, metric, halo, nrm) * max(near, b)
        return max(big, .8 * web) * (1 - c)
    return D

def skein_web_counted(x, z, t, sc):
    VISITS[0] += 2 * len(T.pairs(False)) + 25 * (1 + len(H.WELLS)); return T.D_skein_web(x, z, t, sc)

VARIANTS = {
    "pot-well": body_web(m_well), "pot-newton": body_web(m_newton), "mult": body_web(m_mult),
    "add": body_web(m_add), "power": body_web(m_power), "flow-aniso": body_web(m_aniso),
    "lp05": body_web(m_lp(.5)), "lpinf": body_web(m_lp(math.inf)),
    "skein-web": skein_web_counted, "mult-strands": body_web(m_mult, strands=True),
    "power-strands": body_web(m_power, strands=True),
}

def run(name, fn, cands, t=0.0):
    t1 = time.time(); vals = T.grid(fn, t, G); T.save(vals, os.path.join(OUT, "density_" + name), G)
    cr = T.phys_corr(vals, G)
    a = T.members(fn, cands, t); b = T.members(fn, cands, t + 1); n = max(1, len(a))
    W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
    for c in cands:
        if (c[0], c[1]) in a:
            i, j = int((c[2] / (2 * H.R) + .5) * W2), int((c[3] / (2 * H.R) + .5) * W2)
            if 0 <= i < W2 and 0 <= j < W2: px[j * W2 + i] = (235, 230, 170)
    H.mark_bodies(px, W2); H.write_png(os.path.join(OUT, "members_" + name + ".png"), W2, W2, px)
    worst, big, nn = T.pop_probe(fn, cands, 100.0, 1500)
    T.set_time(0.0); sc = H.scroll(0.0); VISITS[0] = 0; SITE_W.clear(); k = 40
    c0 = time.perf_counter()
    for m in range(k): SITE_W.clear(); fn(300.0 + 7 * m, 200.0 - 5 * m, 0.0, sc)
    ms = (time.perf_counter() - c0) / k * 1e3
    print(f"{name}: corr physarumA {cr:.3f}; members {len(a)}; changed in 1 s {len(a ^ b) / n:.3f}; "
          f"presence step max {worst:.4f} ({big} steps > .1 of {nn}); sites visited per evaluation {VISITS[0] / k:.0f}; "
          f"{ms:.2f} ms/eval (Python); {time.time() - t1:.0f}s", flush=True)

def main():
    print(f"warp at cycle start {WARP_AT_CYCLE}; grid {G}; sigma {[round(s) for s in SIG]}; edge width {EW_U} u; web at cycle start {T.WEB_AT_CYCLE}", flush=True)
    cands = T.cand_list(16.0)
    for name, fn in VARIANTS.items():
        if ONLY and name not in ONLY: continue
        run(name, fn, cands)

if __name__ == "__main__": main()
