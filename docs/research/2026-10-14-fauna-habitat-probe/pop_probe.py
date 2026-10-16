# presence-fade under the c6 law (band x sigmoid(1.5 * conv / .15)), slope terms on, 1/60 steps over 3 s.
import math, sys
_argv = sys.argv; sys.argv = sys.argv[:1]
import habitat_probe as H
H.FLOW.update(swirl=float(__import__("os").environ.get("SWIRL", "6")), slope=float(__import__("os").environ.get("SLIDE", "1.5")))
def smooth(a, b, v): return H.smoothstep(a, b, v)
def density(x, z, t, sc):
    s, div, W, det, a = H.locals_at(x, z, t, sc)
    return H.deposition(s) / 2 * H.sig(1.5 * -div / .15)
C, R = 16.0, H.R; cands = []
for j in range(0, 250, 1):
    for i in range(0, 250, 1):
        x = (i + H.hash01(i, j, 1)) * C - R; z = (j + H.hash01(i, j, 2)) * C - R
        if x * x + z * z < R * R: cands.append((x, z, .1 + .9 * H.hash01(i, j, 3)))
# keep candidates on the shoulders (band > .05 at t0) so the sample is the population that can host
sc = H.scroll(100.0)
cands = [c for c in cands if H.deposition(H.locals_at(c[0], c[1], 100.0, sc)[0]) / 2 > .05][:600]
F = float(__import__("os").environ.get("FADE", ".05"))
prev = None; maxd = 0.0; maxdd = 0.0; jumps = 0; steps = 180
for k in range(steps + 1):
    t = 100.0 + k / 60.0; sc = H.scroll(t)
    d = [density(x, z, t, sc) for x, z, _ in cands]
    p = [smooth(th - F, th + F, v) for v, (_, _, th) in zip(d, cands)]
    if prev:
        for a, b, da, db in zip(prev[0], p, prev[1], d):
            maxd = max(maxd, abs(a - b)); maxdd = max(maxdd, abs(da - db)); jumps += abs(a - b) > .25
    prev = (p, d)
print(f"fade {F}: {len(cands)} shoulder candidates, {steps} steps of 1/60 s: max |d presence|/step {maxd:.3f}, max |d density|/step {maxdd:.4f}, steps>0.25: {jumps}")
