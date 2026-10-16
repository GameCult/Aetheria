"""Density and member images per option of aetheria-fauna:question:habitat-density-driver, under the spec's law
(cut-fauna-habitat-placement.r1): absolute density in [0, 1], fixed constants (no zone-wide normalisation),
candidate threshold .1 + .9 h, presence = smoothstep(thr - .1, thr + .1, density), hosted = presence > 0.
Slope terms on (swirl 6, slide 1.5: flow-slope-terms-ruled turns them on). Uses habitat_probe.py's transliteration.
"""
import math, os, sys, zlib, struct
sys.argv = sys.argv[:1]
import habitat_probe as H

H.FLOW.update(swirl=6.0, slope=1.5)
OUT = H.OUT; R = H.R; T = 100.0


def band(s): return H.deposition(s) / 2


OPTIONS = {  # option label -> density of the locals (s, div, W, det, a)
    "band-only": lambda s, div, W, det, a: band(s),
    "band-times-convergence": lambda s, div, W, det, a: band(s) * H.sig(1.5 * -div / .15),
    "band-times-strain": lambda s, div, W, det, a: band(s) * H.sig(3 * W / .086),
    "band-times-contour-flow": lambda s, div, W, det, a: band(s) * (.25 + .75 * math.exp(-(a / .45) ** 2)),
}


def read_png(path):
    d = open(path, "rb").read(); i = 8; idat = b""; w = h = 0
    while i < len(d):
        n = struct.unpack(">I", d[i:i + 4])[0]; t = d[i + 4:i + 8]; c = d[i + 8:i + 8 + n]
        if t == b"IHDR": w, h = struct.unpack(">II", c[:8])
        if t == b"IDAT": idat += c
        i += 12 + n
    return zlib.decompress(idat)


old1 = os.path.join(OUT, "members_c1_windrows.png"); old6 = os.path.join(OUT, "members_c6_band_soft_windrows_swirl.png")
if os.path.exists(old1) and os.path.exists(old6):
    print(f"old members_c1 vs old members_c6_swirl: bytes identical = {read_png(old1) == read_png(old6)}")

# densities on a 256 grid
G = 256; grid = []
sc = H.scroll(T)
for j in range(G):
    for i in range(G):
        x = ((i + .5) / G - .5) * 2 * R; z = ((j + .5) / G - .5) * 2 * R
        grid.append(None if x * x + z * z > R * R else H.locals_at(x, z, T, sc))
for name, f in OPTIONS.items():
    px = [(0, 0, 0) if v is None else H.ramp(f(*v)) for v in grid]   # absolute [0, 1] scale, same for all options
    H.mark_bodies(px, G); p = os.path.join(OUT, f"option_density_{name}.png"); H.write_png(p, G, G, px); print("png", p)

# members: 16 u jittered lattice, evaluated at T, T+1, T+8
C = 16.0; n = int(2 * R / C); cands = []
for j in range(n):
    for i in range(n):
        x = (i + H.hash01(i, j, 1)) * C - R; z = (j + H.hash01(i, j, 2)) * C - R
        if x * x + z * z <= R * R: cands.append((i, j, x, z, .1 + .9 * H.hash01(i, j, 3)))
loc = {}
for tt in (T, T + 1.0, T + 8.0):
    s2 = H.scroll(tt); loc[tt] = [H.locals_at(x, z, tt, s2) for (_, _, x, z, _) in cands]
for name, f in OPTIONS.items():
    pres = {tt: [H.smoothstep(c[4] - .1, c[4] + .1, f(*v)) for c, v in zip(cands, loc[tt])] for tt in loc}
    hosted = {tt: {k for k, p in enumerate(pres[tt]) if p > 0} for tt in pres}
    a = hosted[T]
    print(f"{name}: {len(a)} hosted of {len(cands)} candidates; changed in 1 s {len(a ^ hosted[T + 1]) / max(1, len(a)):.3f}, in 8 s {len(a ^ hosted[T + 8]) / max(1, len(a)):.3f} (continuous evaluation)")
    W2 = 600; px = [(10, 14, 30)] * (W2 * W2)
    for k in a:
        _, _, x, z, _ = cands[k]; b = pres[T][k]
        pi_, pj = int((x / (2 * R) + .5) * W2), int((z / (2 * R) + .5) * W2)
        if 0 <= pi_ < W2 and 0 <= pj < W2: px[pj * W2 + pi_] = tuple(int(30 + (c - 30) * b) for c in (235, 230, 170))
    H.mark_bodies(px, W2); p = os.path.join(OUT, f"option_members_{name}.png"); H.write_png(p, W2, W2, px); print("png", p)
