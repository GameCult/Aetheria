"""Diagnostics: where the presence steps over .1 come from, and the large layer alone (edges_<name>.png)."""
import sys; sys.argv = ['x', '256']
import body_voronoi_probe as B
T, H = B.T, B.H
mode = sys.argv[1:] or []
cands = T.cand_list(16.0)
def edges_only(metric):
    def D(x, z, t, sc):
        s, b, c = T.env(x, z, t); q = T.warp(x, z, t, sc)
        fd = B.flow_dir_of(x, z, q) if metric is B.m_aniso else (1.0, 0.0)
        return B.edge_profile(B.body_border(q, metric, fd)[0]) * (1 - c)
    return D
MET = {"pot-well": B.m_well, "pot-newton": B.m_newton, "mult": B.m_mult, "add": B.m_add, "power": B.m_power,
       "flow-aniso": B.m_aniso, "lp05": B.m_lp(.5), "lpinf": B.m_lp(float('inf'))}
import os
which = os.environ.get("DIAG", "edges")
if which == "edges":
    for n, m in MET.items():
        f = edges_only(m); vals = T.grid(f, 0.0, 256); T.save(vals, os.path.join(B.OUT, "edges_" + n), 256)
        w, big, nn = T.pop_probe(f, cands, 100.0, 1500)
        print(f"edges-only {n}: presence step max {w:.4f} ({big} steps > .1 of {nn})", flush=True)
else:
    for boost, align in ((0.0, 1.0), (1.6, 0.0), (0.0, 0.0)):
        T.BOOST, B.ALIGN = boost, align
        for n in ("mult", "power"):
            w, big, nn = T.pop_probe(B.VARIANTS[n], cands, 100.0, 1500)
            print(f"BOOST {boost} ALIGN {align} {n}: presence step max {w:.4f} ({big} steps > .1 of {nn})", flush=True)
    print("diag done", flush=True)
