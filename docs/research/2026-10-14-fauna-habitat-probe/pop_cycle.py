# cycle-sampled-flow: band at now, convergence at the member's own cycle start (period 8 s, hashed phase).
import math, sys, os
sys.argv = sys.argv[:1]
import habitat_probe as H
H.FLOW.update(swirl=6.0, slope=1.5)
P, F, C, R = 8.0, .1, 16.0, H.R
cands = []
for j in range(250):
    for i in range(250):
        x = (i + H.hash01(i, j, 1)) * C - R; z = (j + H.hash01(i, j, 2)) * C - R
        if x * x + z * z < R * R: cands.append((x, z, .1 + .9 * H.hash01(i, j, 3), H.hash01(i, j, 4)))
sc0 = H.scroll(100.0)
cands = [c for c in cands if H.deposition(H.locals_at(c[0], c[1], 100.0, sc0)[0]) / 2 > .05][:600]
def start(t, ph): return math.floor(t / P + ph) * P - ph * P
cache = {}
def pres(c, t):
    x, z, th, ph = c; t0 = start(t, ph)
    key = (x, z, t0)
    if key not in cache: cache[key] = H.locals_at(x, z, t0, H.scroll(t0))[1]
    s = H.locals_at(x, z, t, H.scroll(t))[0]
    d = H.deposition(s) / 2 * H.sig(1.5 * -cache[key] / .15)
    return H.smoothstep(th - F, th + F, d), t0
prev = None; maxd = 0.0; jumps = 0; boundary = 0
for k in range(181):
    t = 100.0 + k / 60.0
    cur = [pres(c, t) for c in cands]
    if prev:
        for (a, s0), (b, s1) in zip(prev, cur):
            if s0 != s1: boundary += 1; continue   # the cycle turned: the member is faded here by the flow-coupling ruling
            maxd = max(maxd, abs(a - b)); jumps += abs(a - b) > .25
    prev = cur
print(f"cycle-sampled-flow: {len(cands)} candidates, 180 steps: max |d presence|/step within a cycle {maxd:.4f}, steps>0.25: {jumps}, cycle turns {boundary}")
