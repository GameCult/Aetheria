# Smooth (C-infinity) one-well laws (aetheria-gravity, Eyes pass eyes-ag3-smooth-wells, 2026-10-15).
# Each law: pull F(c, eps, r, s) toward the body (u/s^2) for ALL r >= 0 (the core is part of the law, no seam),
# and potential phi(c, eps, r, s) with dphi/dr = F (phi up to a constant). eps = today's Roche core radius 7.32*m^0.25.
# Reuses ../gravity-tail-taming/laws.py for c, eps, the A&S K0/K1 and today's piecewise law.
import os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'gravity-tail-taming'))
from laws import k0, k1, strength, core_radius, pull, tail_log, P0  # noqa: F401

EULER = 0.5772156649015329

# ---- exponential integral E1, A&S 5.1.53 (0 < u <= 1) and 5.1.54 (u >= 1): the shader form, piecewise at u = 1 ----
def e1_lo(u):  # E1(u) = -ln u + poly(u), |err| < 2e-7
    return -np.log(u) + (-0.57721566 + u*(0.99999193 + u*(-0.24991055 + u*(0.05519968 + u*(-0.00976004 + u*0.00107857)))))
def e1_hi(u):  # u e^u E1(u) = P4/Q4, |err| < 2e-8
    p = 0.2677737343 + u*(8.6347608925 + u*(18.0590169730 + u*(8.5733287401 + u)))
    q = 3.9584969228 + u*(21.0996530827 + u*(25.6329561486 + u*(9.5733223454 + u)))
    return np.exp(-u) / u * p / q
def ein(u):
    """Ein(u) = integral_0^u (1 - e^-t)/t dt = E1(u) + ln u + gamma; entire. Shader form: A&S pieces, seam at u = 1."""
    u = np.asarray(u, float)
    ul = np.clip(u, 1e-300, 1.0); uh = np.maximum(u, 1.0)
    lo = e1_lo(ul) + np.log(ul) + EULER          # the -ln u cancels: lo is a pure polynomial in u
    hi = e1_hi(uh) + np.log(uh) + EULER
    return np.where(u <= 1, lo, hi)

# ---- laws: F(c, eps, r, s) and phi(c, eps, r, s); s is the per-body scale (L or lambda), unused where noted ----
def F_today(c, eps, r, s):   # harmonic core to eps, c/r beyond (C1 potential at eps)
    return pull(tail_log, c, eps, r, s)
def phi_today(c, eps, r, s):
    r = np.asarray(r, float)
    return np.where(r < eps, c * r * r / (2 * eps * eps), c * (np.log(np.maximum(r, 1e-300) / eps) + 0.5))

def F_plummer(c, eps, r, s):  # (d) Plummer-softened log, no taming
    return c * r / (r * r + eps * eps)
def phi_plummer(c, eps, r, s):
    return 0.5 * c * np.log(r * r + eps * eps)

def F_logratio(c, eps, r, L):  # (a) neutralised Plummer: Plummer core minus Plummer halo of width L, same strength
    r2 = r * r
    return c * r * (L * L - eps * eps) / ((r2 + eps * eps) * (r2 + L * L))
def phi_logratio(c, eps, r, L):
    return 0.5 * c * np.log((r * r + eps * eps) / (r * r + L * L))

def F_splummer(c, eps, r, lam):  # (b) screened Plummer: phi = -c K0(sqrt(r^2+eps^2)/lam)
    q = np.sqrt(r * r + eps * eps)
    return c / lam * k1(q / lam) * r / q
def phi_splummer(c, eps, r, lam):
    return -c * k0(np.sqrt(r * r + eps * eps) / lam)

def F_lamb(c, eps, r, s):  # (c) Lamb-Oseen core (Gaussian source), log tail
    r = np.asarray(r, float); rs = np.maximum(r, 1e-12)
    return c * (-np.expm1(-(rs / eps) ** 2)) / rs
def phi_lamb(c, eps, r, s):
    return 0.5 * c * ein((np.asarray(r, float) / eps) ** 2)

def F_lamb_halo(c, eps, r, L):  # (c') Lamb-Oseen core minus the log-ratio's Plummer halo at L
    return F_lamb(c, eps, r, L) - c * r / (r * r + L * L)
def phi_lamb_halo(c, eps, r, L):
    return phi_lamb(c, eps, r, L) - 0.5 * c * np.log(r * r + L * L)

def F_conserve(c, eps, r, L, cf):  # (e) conserving halo: compressed c near, mass-additive c_far beyond L
    r2 = r * r
    return c * r / (r2 + eps * eps) - (c - cf) * r / (r2 + L * L)
def phi_conserve(c, eps, r, L, cf):
    return 0.5 * c * np.log(r * r + eps * eps) - 0.5 * (c - cf) * np.log(r * r + L * L)
