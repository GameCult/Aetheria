"""Run with WARP_AT_CYCLE=1. (a) Which part of the web pops: BOOST/ALIGN ablation on mult and power.
(b) Lattice search window against the body-pulled weight range: WIN 2 vs WIN 3 on the density at 4,000 points."""
import os, sys, math; sys.argv = ['x', '256']
import body_voronoi_probe as B
T, H = B.T, B.H
cands = T.cand_list(16.0)
for boost, align in ((0.0, 1.0), (1.6, 0.0), (0.0, 0.0)):
    T.BOOST, B.ALIGN = boost, align
    for n in ("mult", "power"):
        w, big, nn = T.pop_probe(B.VARIANTS[n], cands, 100.0, 1500)
        print(f"BOOST {boost} ALIGN {align} {n}: presence step max {w:.4f} ({big} steps > .1 of {nn})", flush=True)
T.BOOST, B.ALIGN = 1.6, 1.0
T.set_time(0.0); sc = H.scroll(0.0)
pts = [(((k * 0.6180339887) % 1 - .5) * 2 * H.R * .95, ((k * 0.7548776662) % 1 - .5) * 2 * H.R * .95) for k in range(4000)]
pts = [p for p in pts if p[0] ** 2 + p[1] ** 2 < H.R ** 2]
wmin, wmax = 9, 0
for n in ("mult", "power", "add"):
    f = B.VARIANTS[n]; T.WIN = 2; a = [f(x, z, 0.0, sc) for x, z in pts]
    T.WIN = 3; b = [f(x, z, 0.0, sc) for x, z in pts]; T.WIN = 2
    d = [abs(u - v) for u, v in zip(a, b)]
    print(f"window {n}: WIN 2 vs 3 max |diff| {max(d):.4f}, points differing > .01: {sum(x > .01 for x in d)} of {len(d)}", flush=True)
ws = [v for v in B.SITE_W.values()]
print(f"site boost profile range {min(ws):.3f}..{max(ws):.3f}: weight range .5*(1+1.6*min) .. 1.0*(1+1.6*max) = "
      f"{.5 * (1 + 1.6 * min(ws)):.2f}..{1 + 1.6 * max(ws):.2f}, ratio {(1 + 1.6 * max(ws)) / (.5 * (1 + 1.6 * min(ws))):.1f}:1; "
      f"with ALIGN the across-edge distance is stretched up to {1 + B.ALIGN:.1f}x more", flush=True)
print("diag2 done", flush=True)
