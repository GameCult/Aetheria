# Generator rules co-designed with the band law (aetheria-gravity, imagination-ag3-well-law, r3 pass, 2026-10-15).
# Re-places the bodies of one generated zone (same tree, same masses) so every family sits where the law is exact
# and rails are honest. The rules, each read from the law (well_law.py constants), none respelled:
#   reach        Lam_C = lam * D_C for a child of a body (the law's nominal reach); ring members RING_REACH * D.
#   lower bound  a child sits at least ROCHE_MARGIN x its parent's Roche limit out (the 234 in-core children go).
#   upper bound  a child's whole family ends before its parent's fade begins: BETA D_C <= GAMMA lam D_F and
#                D_C (1 + lam) <= GAMMA lam D_F (B_F <= GAMMA Lam_F, the band law's split-invariance condition);
#                a root's children: 0.75 Z (today's).
#   spacing      siblings at least a reach apart: D_(i+1) >= D_i / (1 - lam), so no sibling's term ever reaches
#                another sibling's rail (phase-free: |D_i - D_j| >= max(Lam_i, Lam_j)). spacing=s < 1 loosens it to
#                D_i / (1 - s lam), letting reaches overlap neighbouring rails.
#   capacity     children are laid inside-out at max(lower bound, previous x 1/(1-lam)); while the last overflows the
#                window the lightest family is folded into the parent's body (family mass, so the far field is unchanged).
#   placement    kept children keep their order and are stretched geometrically so the outermost sits at hi.
#   cusp (opt)   cusp=k: a child also sits where its Plummer peak pull 0.5 c_c/eps_c is k x its parent's pull c_p/D,
#                i.e. D >= 2 k eps_c c_p / c_c, so its own basin keeps a saddle against the parent (wells-read-as-nested-basins).
#   bodyless     a ring keeps its radius when it is a root (rosette geometry is rosette-generation-r3's), else shrinks
#                to fit its node's reach; members' children sit within 0.2 x the nearest-partner chord; outer
#                ("captured") children sit beyond the ring group's support (1 + RING_REACH) x D_ring. A pair whose
#                members cannot sit outside each other's Roche limit inside its node's reach becomes one body.
import math
import numpy as np
import well_law as WL

ROCHE_MARGIN = 1.5          # v >= 0.83 sqrt(c) at the innermost child under the Plummer core
ROCHE = lambda m: 7.32 * np.maximum(m, 1e-12) ** 0.25     # PlanetSettings.RocheLimit (Settings.asset 121-124)

def replace_zone(par, D, m, body, cls, Z, lam, cusp=0.0, spacing=1.0):
    CUSP = cusp
    n = len(par)
    kids = [[j for j in range(n) if par[j] == i] for i in range(n)]
    depth = np.zeros(n, int)
    for j in range(n):
        a = par[j]
        while a >= 0: depth[j] += 1; a = par[a]
    def subtree(i):
        out = [i]
        for j in kids[i]: out += subtree(j)
        return out
    mb = np.where(body, m, 0.0).astype(float)
    newD = D.astype(float).copy(); keep = np.ones(n, bool); m2 = m.astype(float).copy(); body2 = body.copy()
    stats = {'folded': 0, 'folded_massive': 0, 'kept': 0}
    q = 1 / (1 - spacing * lam)
    def fam_mass(i): return sum(mb[j] for j in subtree(i) if keep[j])
    def fold(i, into):
        for j in subtree(i):
            if keep[j]:
                keep[j] = False; stats['folded'] += 1; stats['folded_massive'] += int(mb[j] > 0)
                m2[into] += mb[j]; mb[into] += mb[j]
    cfun = lambda mm: 45.08 * np.maximum(mm, 1e-12) ** 0.2          # PlanetSettings.WellStrength (Settings.asset 117-120)
    def place(ch, lo, hi, into):
        """Greedy inside-out: each child at max(its lower bound, previous x q); fold the lightest family until the
        last fits under hi; then stretch geometrically so the outermost sits at hi (ratios only grow)."""
        hi = hi * (1 - 1e-6)
        ch = sorted([j for j in ch if keep[j]], key=lambda j: D[j])
        def lo_of(j):
            if not CUSP or mb[j] <= 0 or not body2[into] or mb[into] <= 0: return lo
            # the child's Plummer peak pull 0.5 c_c / eps_c must beat CUSP x the parent's pull c_p / D at the child
            return max(lo, 2 * CUSP * ROCHE(m2[j]) * cfun(m2[into]) / cfun(m2[j]))
        while ch:
            pos = []; prev = 0.0
            for j in ch:
                prev = max(lo_of(j), prev * q) if pos else lo_of(j); pos.append(prev)
            if pos[-1] <= hi: break
            light = min(ch, key=lambda j: fam_mass(j)); fold(light, into)
            ch = [j for j in ch if keep[j]]
        if not ch: return
        if len(ch) == 1:
            newD[ch[0]] = max(pos[0], math.sqrt(lo * hi)) if pos[0] <= hi else pos[0]
            return
        g = (hi / pos[-1]) ** (1 / (len(ch) - 1))
        for k, j in enumerate(ch): newD[j] = pos[k] * g ** k
    for i in sorted(range(n), key=lambda i: depth[i]):
        if not keep[i] or not kids[i]: continue
        root = par[i] < 0
        # B_F <= GAMMA Lam_F: the family's own support ends before its fade starts, so a merged body has the same terms
        def hi_of(Dn): return 0.75 * Z if root else min(WL.GAMMA * lam * Dn / WL.BETA, WL.GAMMA * lam * Dn / (1 + lam))
        if body2[i]:
            place(kids[i], ROCHE_MARGIN * ROCHE(m2[i]), hi_of(newD[i]), i)
            continue
        massive = [j for j in kids[i] if keep[j] and mb[j] > 0]
        if not massive:
            continue
        dmin = min(D[j] for j in massive)
        ring = [j for j in massive if abs(D[j] - dmin) <= 0.1]
        if len(ring) < 2: ring = [min(massive, key=lambda j: D[j])]
        heavy = max(ring, key=lambda j: mb[j])
        Dr = D[ring[0]]
        if not root:
            Dr = min(Dr, WL.GAMMA * lam * newD[i] / (WL.BETA ** 2 * (1 + WL.RING_REACH)))
            if Dr < ROCHE_MARGIN * ROCHE(m2[heavy]):
                # the pair cannot fit its node's reach with its members outside each other's Roche limit: it is one body
                for j in list(kids[i]):
                    if keep[j]: fold(j, i)
                body2[i] = True; stats['binaries_merged'] = stats.get('binaries_merged', 0) + 1
                continue
        for j in ring: newD[j] = Dr
        chord = 2 * Dr * math.sin(math.pi / len(ring)) if len(ring) >= 2 else Dr
        for j in ring:
            place(kids[j], ROCHE_MARGIN * ROCHE(m2[j]), 0.2 * chord, j)
        outer = [j for j in kids[i] if keep[j] and j not in ring]
        place(outer, (1 + WL.RING_REACH) * Dr * WL.BETA, hi_of(newD[i]), heavy)
    idx = np.where(keep)[0]; remap = {j: k for k, j in enumerate(idx)}
    par2 = np.array([remap[par[j]] if par[j] >= 0 else -1 for j in idx])
    stats['kept'] = int(keep.sum()); stats['idx'] = idx
    return par2, newD[idx], m2[idx], body2[idx], [cls[j] for j in idx], stats
