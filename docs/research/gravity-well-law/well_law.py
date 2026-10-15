# The conserving compact well law (aetheria-gravity, Imagination pass imagination-ag3-well-law, 2026-10-15).
#
# One primitive, the band term U(r; eps, a, b): a Plummer-softened log source whose pull is exact up to a and fades
# to zero over [a, b] by an order-k smoothstep in s = r^2:
#   pull (toward the centre)  F(r) = r / (r^2 + eps^2) * B(s)
#   B(s) = 1 for s <= a^2;  1 - S(t), t = (s - a^2) / (b^2 - a^2), on the band;  0 for s >= b^2
#   S(t) = t^k sum_{j<k} C(k-1+j, j) (1-t)^j   (the order-k smoothstep: S - 0 and 1 - S vanish to order k at the ends)
#   potential: 0 for s >= b^2; on the band 1/2 [R ln((t+tau)/(1+tau)) - (I(1) - I(t))], tau = (a^2+eps^2)/(b^2-a^2),
#   where (1 - S(t)) = (t + tau) Q(t) + R and I is the antiderivative of Q; inside, 1/2 ln((s+eps^2)/(a^2+eps^2)) plus
#   the band's value at t = 0.  a = b = inf is the global term 1/2 ln(r^2 + eps^2).
# The potential is a function of r^2 (C-infinity at the centre and wherever B is constant) and C^k on the circles
# r = a and r = b (the k-th derivative of the pull jumps there).
#
# A zone's potential is a sum of such terms on the orbit tree (see the probe's build_terms): every node F (a body, or
# a bodyless node's ring group) at its own position contributes
#   c(m_F) U(eps(m_F), L_F, B_F)  +  c(M_F) [ U(eps(M_F), max(B_F, GAMMA Lam_F), Lam_F) - U(eps(M_F), L_F, B_F) ]
# m_F is the body's mass, M_F the family's. L_F is the outermost child's distance: inside it the node pulls exactly
# as its body alone. B_F >= every member term's reach: beyond it the family pulls exactly as one body of mass M_F,
# out to max(B_F, GAMMA Lam_F), and is gone at its reach Lam_F >= BETA B_F. A root's Lam is infinite, so the zone's far field is exactly
# c(M_zone). A leaf is one term, c(m) U(eps(m), GAMMA Lam, Lam) (B = 0).
#
# The first draft, the tapered window W(u) = 1 - S(u) applied from the centre (pull r W(r^2/l^2) / (r^2 + eps^2)), is
# kept as unit_pull_tapered / unit_potential_tapered: its family charge leaks inside the family (a child feels a share
# of its own mass through its parent's family term), which the band form removes.
import math
import numpy as np

K_ORDER = 4          # the smoothstep's order: the potential is C^4 on every band edge
BETA = 1.5           # a family's charge is fully on by B_F >= BETA x its outermost child
GAMMA = 0.5          # a family is felt whole out to max(B_F, GAMMA x its reach) and fades from there to its reach
                     # (its reach is at least BETA x B_F, so the fade is never narrower than half of B_F)
RING_REACH = 4.0     # a ring member of a bodyless node reaches RING_REACH x its distance: whole out to the ring's diameter

def smoothstep_coeffs(k=K_ORDER):
    """S(t) as polynomial coefficients in t, lowest first."""
    out = np.zeros(2 * k)
    for j in range(k):                      # C(k-1+j, j) t^k (1-t)^j
        cj = math.comb(k - 1 + j, j)
        for i in range(j + 1):
            out[k + i] += cj * math.comb(j, i) * (-1) ** i
    return out

SC = smoothstep_coeffs()
FADE = -SC.copy(); FADE[0] += 1.0           # 1 - S(t)

def fade(t):
    t = np.clip(t, 0.0, 1.0)
    return np.polyval(FADE[::-1], t)

def band_pull(eps, a, b, r):
    """Pull toward the centre per unit strength."""
    r = np.asarray(r, float); s = r * r
    a = np.asarray(a, float); b = np.asarray(b, float)
    inf = np.isinf(b)
    A2 = np.where(inf, 0.0, a * a); B2 = np.where(inf, 1.0, b * b)
    t = (s - A2) / np.maximum(B2 - A2, 1e-30)
    prof = np.where(inf, 1.0, np.where(s <= A2, 1.0, np.where(s >= B2, 0.0, fade(t))))
    return r * prof / (s + np.asarray(eps) ** 2)

def _band_coeffs(tau):
    """Q (t + tau) + R = 1 - S(t): returns the coefficients q_i (arrays over tau) and R."""
    n = len(FADE) - 1
    q = [None] * n
    q[n - 1] = FADE[n] * np.ones_like(tau)
    for i in range(n - 1, 0, -1):           # synthetic division by (t + tau): q_{i-1} = f_i - tau q_i
        q[i - 1] = FADE[i] - tau * q[i]
    R = FADE[0] - tau * q[0]
    return q, R

def _qint(q, t):                            # I(t) = sum q_i t^(i+1) / (i+1), Horner
    acc = np.zeros_like(t)
    for i in range(len(q) - 1, -1, -1):
        acc = (acc + q[i] / (i + 1)) * t
    return acc

def band_potential(eps, a, b, r):
    """Potential per unit strength: 0 at and beyond b; the global term (b = inf) is 1/2 ln(r^2 + eps^2)."""
    r = np.asarray(r, float); s = r * r
    eps = np.asarray(eps, float); a = np.asarray(a, float); b = np.asarray(b, float)
    shape = np.broadcast(r, eps, a, b).shape
    s, eps, a, b = (np.broadcast_to(x, shape) for x in (s, eps, a, b))
    inf = np.isinf(b)
    A2 = np.where(inf, 0.0, a * a); B2 = np.where(inf, 2.0, b * b)
    span = B2 - A2
    tau = (A2 + eps * eps) / span
    q, R = _band_coeffs(tau)
    def band(t):
        return 0.5 * (R * np.log((t + tau) / (1 + tau)) - (_qint(q, np.ones_like(t)) - _qint(q, t)))
    t = np.clip((s - A2) / span, 0.0, 1.0)
    inside = 0.5 * np.log((s + eps * eps) / (A2 + eps * eps)) + band(np.zeros_like(t))
    val = np.where(s <= A2, inside, np.where(s >= B2, 0.0, band(t)))
    return np.where(inf, 0.5 * np.log(s + eps * eps), val)

# ---- the first draft: tapered window from the centre (see the header) ----
def unit_pull_tapered(eps, ell, r):
    r = np.asarray(r, float); ell = np.asarray(ell, float)
    inf = np.isinf(ell)
    u = np.where(inf, 0.0, r * r / np.where(inf, 1.0, ell) ** 2)
    win = np.where(inf, 1.0, np.where(u < 1, fade(u), 0.0))
    return r * win / (r * r + np.asarray(eps) ** 2)

def unit_potential_tapered(eps, ell, r):
    """The tapered term is the band term with a = 0: the same closed form."""
    return band_potential(eps, np.zeros_like(np.asarray(ell, float)), ell, r)
