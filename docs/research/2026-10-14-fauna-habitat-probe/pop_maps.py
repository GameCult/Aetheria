"""One image per option of aetheria-fauna:question:habitat-no-pop. Law: band-times-convergence (spec r1 constants),
slope terms on, 16 u lattice thinned to every second cell on the shoulders (band > .05 at t = 100), fade .1,
cycle period 8 s with a hashed phase. Steps of 1/60 s for 1 s. A dot is a hosted member at t = 100, brightness its
presence; red marks a member whose presence moved by more than .1 in one step without its cycle turning
(a pop); a cycle turn is excluded because the advected cycle fades the member there (fauna-flow-coupling-ruled).
"""
import math, os, sys
sys.argv = sys.argv[:1]
import habitat_probe as H

H.FLOW.update(swirl=6.0, slope=1.5)
R, C, P, F, T = H.R, 16.0, 8.0, .1, 100.0
n = int(2 * R / C); cands = []
sc0 = H.scroll(T)
for j in range(0, n, 2):
    for i in range(0, n, 2):
        x = (i + H.hash01(i, j, 1)) * C - R; z = (j + H.hash01(i, j, 2)) * C - R
        if x * x + z * z <= R * R and H.deposition(H.locals_at(x, z, T, sc0)[0]) / 2 > .05:
            cands.append((x, z, .1 + .9 * H.hash01(i, j, 3), H.hash01(i, j, 4)))


def dens(s, div): return H.deposition(s) / 2 * H.sig(1.5 * -div / .15)
def start(t, ph): return math.floor(t / P + ph) * P - ph * P


cache = {}
def at(x, z, t):
    k = (x, z, t)
    if k not in cache: cache[k] = H.locals_at(x, z, t, H.scroll(t))
    return cache[k]


def presence(mode, c, t):
    x, z, th, ph = c; t0 = start(t, ph)
    if mode == "presence-fade": s, div = at(x, z, t)[0], at(x, z, t)[1]
    elif mode == "cycle-sampled-flow": s, div = at(x, z, t)[0], at(x, z, t0)[1]
    else: s, div = at(x, z, t0)[0], at(x, z, t0)[1]   # cycle-sampled-all
    return H.smoothstep(th - F, th + F, dens(s, div)), t0


for mode in ("presence-fade", "cycle-sampled-flow", "cycle-sampled-all"):
    prev = [presence(mode, c, T) for c in cands]; first = [p for p, _ in prev]; popped = set(); maxd = 0.0
    for k in range(1, 61):
        cur = [presence(mode, c, T + k / 60.0) for c in cands]
        for idx, ((a, s0), (b, s1)) in enumerate(zip(prev, cur)):
            if mode != "presence-fade" and s0 != s1: continue
            maxd = max(maxd, abs(a - b))
            if abs(a - b) > .1: popped.add(idx)
        prev = cur
    hosted = [i for i, p in enumerate(first) if p > 0]
    W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
    for idx in set(hosted) | popped:
        x, z = cands[idx][0], cands[idx][1]; b = first[idx]
        col = (255, 50, 50) if idx in popped else tuple(int(30 + (c - 30) * b) for c in (235, 230, 170))
        pi_, pj = int((x / (2 * R) + .5) * W2), int((z / (2 * R) + .5) * W2)
        for di in (0, 1):
            for dj in (0, 1):
                if 0 <= pi_ + di < W2 and 0 <= pj + dj < W2: px[(pj + dj) * W2 + pi_ + di] = col
    H.mark_bodies(px, W2); p = os.path.join(H.OUT, f"option_nopop_{mode}.png"); H.write_png(p, W2, W2, px)
    print(f"{mode}: {len(cands)} shoulder candidates, {len(hosted)} hosted at t=100; 60 steps of 1/60 s: max |d presence| per step (cycle turns excluded) {maxd:.4f}, members popping (> .1 in a step) {len(popped)}; png {p}")
