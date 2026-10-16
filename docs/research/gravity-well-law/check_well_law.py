# Checks of the band term (well_law.py): force = dphi/dr (grid equals field), smoothness class on both band edges
# (one-sided derivatives at 50 digits), circular speed round a median sun, and float32 error of the closed form.
import os, sys, math
import numpy as np, mpmath as mp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'gravity-tail-taming'))
from well_law import *
from laws import strength, core_radius

print('S(t) coefficients (lowest first):', SC.tolist())
print('1 - S(t):', FADE.tolist())

# 1. force = dphi/dr, band terms, the tapered draft (a = 0) and the global term
for eps, a, b in [(79.8, 1000.0, 1500.0), (79.8, 0.0, 1525.0), (17.0, 40.0, 60.0), (32.7, 400.0, 800.0), (79.8, 12000.0, 25000.0)]:
    r = np.linspace(1e-3, 1.2 * b, 400001)
    p = band_potential(eps, a, b, r); f = band_pull(eps, a, b, r); d = np.gradient(p, r)
    print(f'eps {eps:6.1f} band [{a:7.0f}, {b:7.0f}]: max |F - dphi/dr| / max F = {np.max(np.abs(d - f)[3:-3]) / f.max():.1e}; '
          f'phi(b) = {float(band_potential(eps, a, b, np.array(b))):.1e}')
r = np.linspace(1e-3, 30000, 200001)
p = band_potential(79.8, np.inf, np.inf, r); f = band_pull(79.8, np.inf, np.inf, r)
print(f'global term: max |F - dphi/dr| / max F = {np.max(np.abs(np.gradient(p, r) - f)[3:-3]) / f.max():.1e}')

# 2. smoothness at both band edges: derivatives of each branch at the edge, at 50 digits
mp.mp.dps = 50
def branches(eps, a, b):
    eps, a, b = mp.mpf(eps), mp.mpf(a), mp.mpf(b)
    span = b * b - a * a; tau = (a * a + eps * eps) / span
    n = len(FADE) - 1; q = [None] * n; q[n - 1] = mp.mpf(FADE[n])
    for i in range(n - 1, 0, -1): q[i - 1] = mp.mpf(FADE[i]) - tau * q[i]
    R = mp.mpf(FADE[0]) - tau * q[0]
    I = lambda t: sum(q[i] * t ** (i + 1) / (i + 1) for i in range(n))
    band = lambda r: (R * mp.log(((r * r - a * a) / span + tau) / (1 + tau)) - (I(1) - I((r * r - a * a) / span))) / 2
    inside = lambda r: mp.log((r * r + eps * eps) / (a * a + eps * eps)) / 2 + band(a)
    return inside, band, (lambda r: mp.mpf(0))
eps, a, b = 79.8, 1000.0, 1500.0
inside, band, beyond = branches(eps, a, b)
for edge, lo, hi in ((a, inside, band), (b, band, beyond)):
    jumps = [mp.nstr((mp.diff(hi, mp.mpf(edge), n) - mp.diff(lo, mp.mpf(edge), n)) * mp.mpf(edge) ** n, 4) for n in range(7)]
    print(f'edge r = {edge:.0f}: jumps of phi^(0..6) x r^n = {jumps}')
print('odd derivatives at r = 0 (inside branch, must be 0):', [mp.nstr(mp.diff(inside, mp.mpf(0), n), 3) for n in (1, 3, 5)])

# 3. circular speed round a median sun: the body term is exact Plummer inside a, so v/sqrt(c) = r / sqrt(r^2 + eps^2)
m = 14152.4; c = strength(m); e = core_radius(m)
print(f'median sun: c = {c:.1f}, sqrt(c) = {math.sqrt(c):.2f} u/s, eps = Roche = {e:.1f}')
rr = np.array([e, 1.5 * e, 2 * e, 3 * e, 610.0])
print('  v/sqrt(c) at eps, 1.5 eps, 2 eps, 3 eps, 610 u:', np.round(np.sqrt(rr * c * band_pull(e, np.inf, np.inf, rr)) / math.sqrt(c), 3).tolist())
rr = np.logspace(0, 4, 40001); F = band_pull(e, np.inf, np.inf, rr)
print(f'  peak pull: {F.max() * e:.3f} c/eps at r = {rr[np.argmax(F)] / e:.3f} eps (today: 1.0 c/R at R)')

# 4. float32: the band closed form evaluated in single precision against float64
def band32(eps, a, b, r):
    f32 = np.float32
    eps, a, b, r = f32(eps), f32(a), f32(b), r.astype(f32)
    s = r * r; span = b * b - a * a; tau = (a * a + eps * eps) / span
    n = len(FADE) - 1; q = [None] * n; q[n - 1] = f32(FADE[n])
    for i in range(n - 1, 0, -1): q[i - 1] = f32(FADE[i]) - tau * q[i]
    R = f32(FADE[0]) - tau * q[0]
    def I(t):
        acc = np.zeros_like(t)
        for i in range(n - 1, -1, -1): acc = ((acc + q[i] / f32(i + 1)) * t).astype(f32)
        return acc
    t = np.clip((s - a * a) / span, 0, 1).astype(f32)
    bandv = f32(.5) * (R * np.log((t + tau) / (f32(1) + tau)).astype(f32) - (I(np.ones_like(t)) - I(t)))
    inside = f32(.5) * np.log((s + eps * eps) / (a * a + eps * eps)).astype(f32) + bandv[0] * 0 + f32(.5) * (R * np.log(tau / (f32(1) + tau)) - I(np.ones(1, f32))[0])
    return np.where(s <= a * a, inside, np.where(s >= b * b, f32(0), bandv))
for eps, a, b in [(79.8, 1000.0, 1500.0), (17.0, 40.0, 60.0), (79.8, 12000.0, 25000.0)]:
    r64 = np.linspace(0, b, 100001)
    ref = band_potential(eps, a, b, r64)
    print(f'float32 eps {eps} band [{a}, {b}]: max abs err {np.max(np.abs(band32(eps, a, b, r64) - ref)):.2e} (unit strength; depth {np.abs(ref).max():.2f})')

# 5. large tau (softening comparable to the band): the closed form cancels in float32; the series branch does not
def series32(eps, a, b, r):
    f32 = np.float32
    eps, a, b, r = f32(eps), f32(a), f32(b), r.astype(f32)
    s = r * r; span = b * b - a * a; tau = (a * a + eps * eps) / span; w = f32(1) / (f32(1) + tau)
    def bandv(t):
        Y = (f32(1) - t).astype(f32); acc = np.zeros_like(t)
        for k in range(40):
            inner = np.zeros_like(t)
            for j in range(4, len(SC)): inner = (inner + f32(SC[j]) * Y ** (j + k + 1) / f32(j + k + 1)).astype(f32)
            acc = (acc + w ** (k + 1) * inner).astype(f32)
        return f32(-.5) * acc
    t = np.clip((s - a * a) / span, 0, 1).astype(f32)
    inside = f32(.5) * np.log((s + eps * eps) / (a * a + eps * eps)).astype(f32) + bandv(np.zeros(1, f32))[0]
    return np.where(s <= a * a, inside, np.where(s >= b * b, f32(0), bandv(t)))
for eps, a, b in [(1658.0, 1000.0, 1500.0), (3391.0, 1000.0, 1500.0), (59.0, 11.0, 22.0)]:
    r64 = np.linspace(0, b, 20001); ref = band_potential(eps, a, b, r64)
    tau = (a * a + eps * eps) / (b * b - a * a)
    print(f'tau {tau:6.2f}: closed form float32 err {np.max(np.abs(band32(eps, a, b, r64) - ref)):.1e}; series float32 err '
          f'{np.max(np.abs(series32(eps, a, b, r64) - ref)):.1e}; F = dphi/dr {np.max(np.abs(np.gradient(ref, r64) - band_pull(eps, a, b, r64))[3:-3]) / band_pull(eps, a, b, r64).max():.1e}')
