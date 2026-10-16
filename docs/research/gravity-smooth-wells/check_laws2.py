# Checks: force = dphi/dr for every smooth law (grid equals field); Ein shader form against mpmath; circular-speed shape.
import numpy as np, mpmath as mp, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from laws2 import *
c, eps = 304.9, 7.32 * 14152.4 ** 0.25   # median sun
r = np.linspace(0.01, 20000, 400001)
for name, F, P, s in [('today', F_today, phi_today, 0), ('plummer', F_plummer, phi_plummer, 0),
                      ('logratio L=800', F_logratio, phi_logratio, 800.0), ('splummer lam=800', F_splummer, phi_splummer, 800.0),
                      ('lamb', F_lamb, phi_lamb, 0), ('lamb+halo L=800', F_lamb_halo, phi_lamb_halo, 800.0)]:
    ph = P(c, eps, r, s); d = np.gradient(ph, r); f = F(c, eps, r, s)
    print(f'{name:18s} max |F - dphi/dr| / max F = {np.max(np.abs(f - d)[5:-5]) / np.abs(f).max():.1e}')
us = np.concatenate([np.logspace(-4, 0, 50), np.logspace(0, 2, 50)])
ref = np.array([float(mp.ein(u)) if hasattr(mp, 'ein') else float(mp.e1(u) + mp.log(u) + mp.euler) for u in us])
ph = phi_conserve(c, eps, r, 800.0, 60.0); d = np.gradient(ph, r); f = F_conserve(c, eps, r, 800.0, 60.0)
print(f'{"conserve L=800":18s} max |F - dphi/dr| / max F = {np.max(np.abs(f - d)[5:-5]) / np.abs(f).max():.1e}')
print('Ein shader form vs mpmath: max abs err', f'{np.max(np.abs(ein(us) - ref)):.1e}')
# circular speed v^2 = r F: peak and plateau
print(f'median sun: c = {c}, sqrt(c) = {np.sqrt(c):.2f} u/s, eps = {eps:.1f}')
rr = np.logspace(0, 5, 20001)
for name, F, s in [('today', F_today, 0), ('plummer', F_plummer, 0), ('lamb', F_lamb, 0),
                   ('logratio L=1000', F_logratio, 1000.0), ('logratio L=4000', F_logratio, 4000.0),
                   ('splummer lam=1000', F_splummer, 1000.0), ('lamb+halo L=1000', F_lamb_halo, 1000.0)]:
    v = np.sqrt(np.maximum(rr * F(c, eps, rr, s), 0)) / np.sqrt(c)
    i = np.argmax(v); band = rr[v >= 0.9]
    print(f'{name:18s} v/sqrt(c): peak {v[i]:.3f} at r = {rr[i]:.0f} ({rr[i]/eps:.2f} eps); v >= 0.9 sqrt(c) on r in '
          f'[{band.min():.0f}, {band.max():.0f}]' if len(band) else f'{name:18s} peak {v[i]:.3f} at r = {rr[i]:.0f}; never 0.9')
    for x in (eps, 2 * eps, 5 * eps):
        pass
print('v/sqrt(c) at r = eps, 2eps, 3eps:')
for name, F in [('today', F_today), ('plummer', F_plummer), ('lamb', F_lamb)]:
    print(f'  {name:8s}', [round(float(np.sqrt(x * F(c, eps, x, 0) / c)), 3) for x in (eps, 2 * eps, 3 * eps)])
print('max pull (units c/eps):', {n: round(float(np.max(F(c, eps, rr, 0)) * eps / c), 3) for n, F in [('today', F_today), ('plummer', F_plummer), ('lamb', F_lamb)]})
