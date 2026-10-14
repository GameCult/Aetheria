"""Trace the worst presence step of the edges-only layer: where it is and what moves."""
import sys, os, math; sys.argv = ['x', '256']
import body_voronoi_probe as B
T, H = B.T, B.H
cands = T.cand_list(16.0)
name = os.environ.get("M", "mult"); met = {"mult": B.m_mult, "power": B.m_power, "add": B.m_add}[name]
def f(x, z, t, sc):
    s, b, c = T.env(x, z, t); q = T.warp(x, z, t, sc); return B.edge_profile(B.body_border(q, met)[0]) * (1 - c), q
T.set_time(100.0)
near = [c for c in cands if abs(T.density_at(lambda x, z, t, sc: f(x, z, t, sc)[0], c, 100.0)[0] - c[4]) < .3][:1500]
best = (0, None)
prev = {}
for k in range(61):
    tt = 100 + k / 60; T.set_time(tt)
    for c in near:
        sc, t0 = T.cyc_scroll(tt, c[5]); d, q = f(c[2], c[3], tt, sc); p = T.presence(d, c[4])
        if c in prev and prev[c][1] == t0:
            st = abs(p - prev[c][0])
            if st > best[0]: best = (st, c, k, d, prev[c][2], q, prev[c][3], t0)
        prev[c] = (p, t0, d, q)
st, c, k, d, dprev, q, qprev, t0 = best
print(f"{name}: step {st:.3f} at cand ({c[2]:.0f},{c[3]:.0f}) thr {c[4]:.2f} frame {k}: density {dprev:.3f} -> {d:.3f}; warped q {qprev} -> {q}; t0 {t0}")
for kk in range(k - 3, k + 3):
    tt = 100 + kk / 60; T.set_time(tt); sc, t0 = T.cyc_scroll(tt, c[5]); s, b, cc = T.env(c[2], c[3], tt)
    qq = T.warp(c[2], c[3], tt, sc); dist, n = B.body_border(qq, met)
    print(f"  frame {kk}: t0 {t0:.3f} slope {s:.4f} core {cc:.3f} border dist {dist:.2f} warped q ({qq[0]:.2f},{qq[1]:.2f})")
