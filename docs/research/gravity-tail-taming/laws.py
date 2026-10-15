# Candidate one-well laws for tail taming (aetheria-gravity, Eyes pass eyes-ag3-tail-taming, 2026-10-15).
# Each law gives the pull F(r) toward the body (u/s^2) and a potential phi(r) with -dphi/dr = -F (phi up to a constant).
# Shared: c = G*m^p, core radius R = Roche = 7.32*m^0.25 with a harmonic core F = F_tail(R)*r/R inside R (GravityField at 09ec04a8+).
# K0/K1 use Abramowitz & Stegun 9.8.5-9.8.8 polynomial approximations, the same ones an HLSL port would use.
import numpy as np

G0, P0, RO = 45.08, 0.2, 7.32
CSUN, MSUN = 0.5 * 609.8, 14152.4  # field-strength-ruled calibration (B19)

def strength(m, p=P0):
    G = CSUN / MSUN ** p
    return G * m ** p

def core_radius(m):
    return RO * m ** 0.25

# ---- modified Bessel functions, A&S 9.8.1-9.8.8 ----
def _i0(x):
    t = (x / 3.75) ** 2
    small = 1 + t*(3.5156229 + t*(3.0899424 + t*(1.2067492 + t*(0.2659732 + t*(0.0360768 + t*0.0045813)))))
    return small  # valid |x| <= 3.75; only used for x <= 2

def _i1(x):
    t = (x / 3.75) ** 2
    return x * (0.5 + t*(0.87890594 + t*(0.51498869 + t*(0.15084934 + t*(0.02658733 + t*(0.00301532 + t*0.00032411))))))

def k0(x):
    x = np.asarray(x, float)
    xs = np.minimum(x, 2.0); y = (xs / 2) ** 2
    lo = -np.log(xs / 2) * _i0(xs) + (-0.57721566 + y*(0.42278420 + y*(0.23069756 + y*(0.03488590 + y*(0.00262698 + y*(0.00010750 + y*0.00000740))))))
    xl = np.maximum(x, 2.0); z = 2 / xl
    hi = np.exp(-xl) / np.sqrt(xl) * (1.25331414 + z*(-0.07832358 + z*(0.02189568 + z*(-0.01062446 + z*(0.00587872 + z*(-0.00251540 + z*0.00053208))))))
    return np.where(x <= 2, lo, hi)

def k1(x):
    x = np.asarray(x, float)
    xs = np.minimum(x, 2.0); y = (xs / 2) ** 2
    lo = np.log(xs / 2) * _i1(xs) + (1 / xs) * (1 + y*(0.15443144 + y*(-0.67278579 + y*(-0.18156897 + y*(-0.01919402 + y*(-0.00110404 + y*(-0.00004686)))))))
    xl = np.maximum(x, 2.0); z = 2 / xl
    hi = np.exp(-xl) / np.sqrt(xl) * (1.25331414 + z*(0.23498619 + z*(-0.03655620 + z*(0.01504268 + z*(-0.00780353 + z*(0.00325614 + z*(-0.00068245)))))))
    return np.where(x <= 2, lo, hi)

# ---- tail laws: F_tail(c, r, s) with s a per-body scale parameter (array broadcastable) ----
def tail_log(c, r, s):            # today: F = c/r
    return c / r
def tail_k0(c, r, lam):           # 2D screened Poisson: phi = -c K0(r/lam), F = (c/lam) K1(r/lam) -> c/r near
    return c / lam * k1(r / lam)
def tail_cap(c, r, L):            # B21's cap: c/r inside L, c L / r^2 beyond (force continuous at L)
    return np.where(r < L, c / r, c * L / r ** 2)
def _switch(r, a, b):             # force switch 1 -> 0 over [a, b], cubic smoothstep (C1)
    u = np.clip((r - a) / (b - a), 0, 1)
    return 1 - u * u * (3 - 2 * u)
def tail_switch(c, r, L):         # force-switched cutoff: exact c/r to L, smooth to zero at 2L
    return c / r * _switch(r, L, 2 * L)
def tail_hyp(c, r, ell):          # hyperbolic-plane Green's function used on flat distances: F = c/(ell sinh(r/ell))
    return c / (ell * np.sinh(np.minimum(r / ell, 80)))

def pull(tail, c, R, r, s):
    """Pull toward the body at distance r: tail outside the core, linear inside, continuous at R."""
    r = np.asarray(r, float)
    rr = np.maximum(r, R)
    ft = tail(c, rr, s)
    return np.where(r < R, ft * r / R, ft)

# Potentials (closed forms, for the HLSL statement and the grid-equals-field check), tail part only, r >= R.
def phi_log(c, r, s):   return c * np.log(r)
def phi_k0(c, r, lam):  return -c * k0(r / lam)
def phi_cap(c, r, L):   return np.where(r < L, c * np.log(r), c * (np.log(L) + 1 - L / r))
def phi_hyp(c, r, ell): return -c * np.log(1 / np.tanh(np.minimum(r / (2 * ell), 40)))
def phi_switch(c, r, L):
    # integral of (c/r) * (1 - 3u^2 + 2u^3), u = (r-L)/L on [L, 2L]: expand in r, integrate term by term
    a, b = L, 2 * L
    def inner(x):  # antiderivative of S(x)/x on [a, b]
        u = (x - a) / (b - a); d = b - a
        # S = 1 - 3u^2 + 2u^3 with u = (x-a)/d; expand to polynomial in x: S = s0 + s1 x + s2 x^2 + s3 x^3
        s3 = 2 / d**3; s2 = -3 / d**2 - 6 * a / d**3; s1 = 6 * a / d**2 + 6 * a * a / d**3; s0 = 1 - 3 * a * a / d**2 - 2 * a**3 / d**3
        return s0 * np.log(x) + s1 * x + s2 * x * x / 2 + s3 * x**3 / 3
    rc = np.clip(r, a, b)
    return c * np.where(r < a, np.log(np.maximum(r, 1e-9)), np.log(a) + inner(rc) - inner(a))
