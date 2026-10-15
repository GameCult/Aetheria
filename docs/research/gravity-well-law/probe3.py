# Probe of the conserving compact well law (well_law.py) against today's law and the smooth-wells rows a2, c2, b1,
# on the measures of ../gravity-smooth-wells/probe2.py (same data, rng seed 7, 6 draws, same ratio/mid-zone/rail code
# paths), plus: split invariance (one family merged into one body of its mass), whole-zone merge depth, and
# evaluation cost (terms per sample for the sim, per texel for a 2048^2 / 4 u Surface Displacement window).
# Rows t1-t3 are the first draft (windows tapered from the centre), kept because they show what the band form buys.
# Usage: python probe3.py bodies.csv [draws] [law-index,...]
import csv, sys, os, collections, time
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'gravity-smooth-wells'))
sys.path.insert(0, os.path.join(HERE, '..', 'gravity-tail-taming'))
import well_law as WL
from laws2 import (F_today, phi_today, F_logratio, phi_logratio, F_lamb_halo, phi_lamb_halo,
                   F_splummer, phi_splummer, strength, core_radius)

rng = np.random.default_rng(7)
DRAWS = int(sys.argv[2]) if len(sys.argv) > 2 else 6
rows = [r for r in csv.DictReader(open(sys.argv[1])) if r.get('type')]
zones = collections.defaultdict(list)
for r in rows: zones[(r['density'], r['seed'])].append(r)

def zone_struct(z):   # as probe2.zone_struct
    idx = {int(r['idx']): k for k, r in enumerate(z)}
    par = np.array([idx.get(int(r['parentIdx']), -1) for r in z])
    D = np.array([float(r['distance']) for r in z]); m = np.array([float(r['mass']) for r in z])
    body = np.array([r['empty'] == '0' and r['belt'] == '0' for r in z])
    cls = ['Belt' if r['belt'] == '1' else r['type'] for r in z]
    return par, D, m, body, cls

def family(par):
    n = len(par); fam = np.eye(n, dtype=bool); depth = np.zeros(n, int)
    for j in range(n):
        a = par[j]
        while a >= 0:
            fam[a, j] = True; depth[j] += 1; a = par[a]
    return fam, depth

def place(par, D, depth):
    pos = np.zeros((len(par), 2)); th = rng.uniform(0, 2 * np.pi, len(par))
    for k in np.argsort(depth, kind='stable'):
        if par[k] >= 0: pos[k] = pos[par[k]] + D[k] * np.array([np.cos(th[k]), np.sin(th[k])])
    return pos

def q(v, ps=(10, 50, 90)):
    v = np.sort(np.asarray(v, float))
    return [round(float(v[min(len(v) - 1, int(p / 100 * len(v)))]), 2) for p in ps] if len(v) else []

# ---------------- the compact law: terms on the orbit tree ----------------
# A term is (owner node, strength, eps, a, b): strength x band term U(eps, a, b) at the owner's position.
def _tree(par, D, m, body):
    n = len(par); fam, depth = family(par)
    kids = [[j for j in range(n) if par[j] == i] for i in range(n)]
    mb = np.where(body, m, 0.0)
    MF = np.array([mb[fam[i]].sum() for i in range(n)])
    cm = np.where(body, strength(np.maximum(m, 1e-12)), 0.0); em = core_radius(np.maximum(m, 1e-12))
    CF = np.where(MF > 0, strength(np.maximum(MF, 1e-12)), 0.0); eM = core_radius(np.maximum(MF, 1e-12))
    def ring_of(p):
        pk = [j for j in kids[p] if MF[j] > 0]
        dmin = min(D[j] for j in pk)
        return [j for j in pk if abs(D[j] - dmin) <= 0.1]
    return n, fam, depth, kids, mb, MF, cm, em, CF, eM, ring_of

def build_terms_band(par, D, m, body, reach):
    """The law: band terms (well_law.py header). reach(node, D, C_F, c_parent_body) is a child's nominal reach."""
    n, fam, depth, kids, mb, MF, cm, em, CF, eM, ring_of = _tree(par, D, m, body)
    Lam = np.zeros(n); L = np.zeros(n); B = np.zeros(n); grp = {}
    for i in sorted(range(n), key=lambda i: -depth[i]):          # children before parents
        L[i] = max([D[j] for j in kids[i]], default=0.0)
        B[i] = max([WL.BETA * L[i]] + [D[j] + Lam[j] for j in kids[i]])
        if MF[i] > 0 and not body[i]:                           # bodyless: its ring group stands in for a body
            rg = ring_of(i); LG = D[rg[0]]
            BG = max([WL.BETA * LG] + [D[j] + Lam[j] for j in rg])
            L[i] = max(L[i], BG); B[i] = max(B[i], WL.BETA * L[i])
            grp[i] = (MF[rg].sum(), LG, BG)
        if MF[i] <= 0:
            Lam[i] = 0.0; continue
        p = par[i]
        floor = WL.BETA * B[i]                                  # room for the fade beyond the family's own support
        if p < 0:
            Lam[i] = np.inf
        elif not body[p] and i in ring_of(p) and len(ring_of(p)) >= 2:
            Lam[i] = max(WL.RING_REACH * D[i], floor)
        else:
            Lam[i] = max(reach(i, D[i], CF[i], cm[p] if body[p] else 0.0), floor)
    terms = []
    for i in range(n):
        if MF[i] <= 0: continue
        fa, fb = (np.inf, np.inf) if np.isinf(Lam[i]) else (max(B[i], WL.GAMMA * Lam[i]), Lam[i])
        if body[i] and MF[i] <= mb[i] * (1 + 1e-12):            # no massive descendants: one term
            terms.append((i, cm[i], em[i], fa, fb)); continue
        if body[i]:
            terms.append((i, cm[i], em[i], L[i], B[i]))
        else:
            MG, LG, BG = grp[i]; cg, eg = strength(MG), core_radius(MG)
            terms += [(i, cg, eg, L[i], B[i]), (i, -cg, eg, LG, BG)]
        terms += [(i, CF[i], eM[i], fa, fb), (i, -CF[i], eM[i], L[i], B[i])]
    return terms, Lam, L, B

def build_terms_tapered(par, D, m, body, reach):
    """The first draft: windows tapered from the centre (well_law.py, unit_pull_tapered), KAPPA 2.5, MU 1, RING 5."""
    KAPPA, MU, RING = 2.5, 1.0, 5.0
    n, fam, depth, kids, mb, MF, cm, em, CF, eM, ring_of = _tree(par, D, m, body)
    Lam = np.zeros(n); L = np.zeros(n)
    for i in sorted(range(n), key=lambda i: -depth[i]):
        L[i] = max([D[j] + Lam[j] for j in kids[i]], default=0.0)
        if MF[i] <= 0:
            Lam[i] = 0.0; continue
        p = par[i]
        floor = max(MU * L[i], KAPPA * max([D[j] for j in kids[i]], default=0.0))
        if p < 0:
            Lam[i] = np.inf
        elif not body[p] and i in ring_of(p) and len(ring_of(p)) >= 2:
            Lam[i] = max(RING * D[i], floor)
        else:
            Lam[i] = max(reach(i, D[i], CF[i], cm[p] if body[p] else 0.0), floor)
    T = []
    def t(i, c, e, ell): T.append((i, c, e, np.inf if np.isinf(ell) else 0.0, ell))
    for i in range(n):
        if MF[i] <= 0: continue
        if body[i]:
            if MF[i] <= mb[i] * (1 + 1e-12):
                t(i, cm[i], em[i], Lam[i])
            else:
                t(i, cm[i], em[i], L[i]); t(i, CF[i], eM[i], Lam[i]); t(i, -CF[i], eM[i], L[i])
        else:
            rg = ring_of(i); MG = MF[rg].sum(); LG = max(D[j] + Lam[j] for j in rg)
            if LG < L[i]:
                t(i, strength(MG), core_radius(MG), L[i]); t(i, -strength(MG), core_radius(MG), LG)
            t(i, CF[i], eM[i], Lam[i]); t(i, -CF[i], eM[i], L[i])
    return T, Lam, L, L

def _arr(T):
    return (np.array([t[0] for t in T]), np.array([t[1] for t in T]), np.array([t[2] for t in T]),
            np.array([t[3] for t in T]), np.array([t[4] for t in T]))

def compact_field(pos, T, x):
    own, c, e, a, b = _arr(T)
    d = pos[own][None, :, :] - x[:, None, :]; r = np.linalg.norm(d, axis=2); rs = np.maximum(r, 1e-9)
    F = c[None, :] * WL.band_pull(e[None, :], a[None, :], b[None, :], rs)
    return d / rs[:, :, None] * F[:, :, None], own

def compact_potential(pos, T, x):
    own, c, e, a, b = _arr(T)
    r = np.linalg.norm(pos[own][None, :, :] - x[:, None, :], axis=2)
    return (c[None, :] * WL.band_potential(e[None, :], a[None, :], b[None, :], r)).sum(1)

def own_pull(T, node, d):
    return sum(t[1] * float(WL.band_pull(t[2], t[3], t[4], np.array(d))) for t in T if t[0] == node)

# ---------------- the comparison laws: one well per body ----------------
OTHER = {
    'today': dict(F=F_today, P=phi_today, scale=lambda D, Z: np.ones_like(D)),
    'a2': dict(F=F_logratio, P=phi_logratio, scale=lambda D, Z: 1.0 * D),
    'c2': dict(F=F_lamb_halo, P=phi_lamb_halo, scale=lambda D, Z: 1.0 * D),
    'b1': dict(F=F_splummer, P=phi_splummer, scale=lambda D, Z: 0.5 * D),
}
def wells_field(law, pos, c, e, s, x):
    d = pos[None, :, :] - x[:, None, :]; r = np.linalg.norm(d, axis=2); rs = np.maximum(r, 1e-9)
    F = np.where(r < 1e-9, 0, law['F'](c[None, :], e[None, :], rs, s[None, :]))
    return d / rs[:, :, None] * F[:, :, None]
def wells_potential(law, pos, c, e, s, x):
    r = np.maximum(np.linalg.norm(pos[None, :, :] - x[:, None, :], axis=2), 1e-9)
    return np.where(c[None, :] > 0, law['P'](c[None, :], e[None, :], r, s[None, :]), 0).sum(1)

TIDAL = lambda i, D, CF, cp: (2 * D * np.sqrt(CF / (2 * cp)) if cp > 0 else 0.5 * D)
HALF = lambda i, D, CF, cp: 0.5 * D
LAWS = [
    ('today: harmonic core + log', 'today', None),
    ('(a2) log-ratio L=1*D', 'a2', None),
    ('(c2) Lamb-Oseen + halo L=1*D', 'c2', None),
    ('(b1) screened Plummer lam=0.5*D', 'b1', None),
    ('(t1) tapered draft, Lam = 0.5*D', 'tapered', HALF),
    ('(t2) tapered draft, Lam = 1*D', 'tapered', lambda i, D, CF, cp: 1.0 * D),
    ('(t3) tapered draft, Lam = 2*d_Hill', 'tapered', TIDAL),
    ('(w1) conserving compact, Lam = 0.5*D', 'band', HALF),
    ('(w2) conserving compact, Lam = 1*D', 'band', lambda i, D, CF, cp: 1.0 * D),
    ('(w3) conserving compact, Lam = 2*d_Hill', 'band', TIDAL),
]
pick = [int(x) for x in sys.argv[3].split(',')] if len(sys.argv) > 3 else range(len(LAWS))

sunm = np.sort([float(r['mass']) for r in rows if r['type'] == 'Sun' and r['empty'] == '0'])
SUN_LO, SUN_HI = sunm[int(.4 * len(sunm))], sunm[int(.6 * len(sunm))]
pre = {k: zone_struct(z) for k, z in zones.items()}
TH8 = np.arange(8) * np.pi / 4; RING8 = np.stack([np.cos(TH8), np.sin(TH8)], 1)

class ZoneLaw:
    """One law on one zone's tree: field, potential, the parent's own pull, and which node owns each contribution."""
    def __init__(self, kind, reach, par, D, m, body, Z):
        self.kind, self.par = kind, par
        self.fam, self.depth = family(par)
        if kind in ('band', 'tapered'):
            self.T, self.Lam, self.L, self.B = (build_terms_band if kind == 'band' else build_terms_tapered)(par, D, m, body, reach)
        else:
            self.law = OTHER[kind]
            self.bi = np.where(body)[0]
            self.c = strength(np.maximum(m, 1e-12))[self.bi]; self.e = core_radius(np.maximum(m, 1e-12))[self.bi]
            Dz = np.where(par < 0, Z, D)
            self.s = self.law['scale'](Dz, Z)[self.bi]
    def field(self, pos, x, exclude_family_of=None):
        if self.kind in ('band', 'tapered'):
            A, own = compact_field(pos, self.T, x)
        else:
            A = wells_field(self.law, pos[self.bi], self.c, self.e, self.s, x); own = self.bi
        if exclude_family_of is not None:
            mask = self.fam[np.asarray(exclude_family_of)][:, own]
            A = np.where(mask[:, :, None], 0, A)
        return A.sum(1)
    def potential(self, pos, x):
        if self.kind in ('band', 'tapered'): return compact_potential(pos, self.T, x)
        return wells_potential(self.law, pos[self.bi], self.c, self.e, self.s, x)
    def own(self, node, d):
        if self.kind in ('band', 'tapered'): return own_pull(self.T, node, d)
        j = int(np.where(self.bi == node)[0][0])
        return float(self.law['F'](self.c[j], self.e[j], np.array(d), self.s[j]))

def merged(par, D, m, body, f):
    """The zone with node f's descendants removed and f's body mass made the family's."""
    fam, _ = family(par)
    keep = [j for j in range(len(par)) if not (fam[f, j] and j != f)]
    remap = {j: k for k, j in enumerate(keep)}
    mb = np.where(body, m, 0.0)
    par2 = np.array([remap[par[j]] if par[j] >= 0 else -1 for j in keep])
    m2 = m[keep].copy(); m2[remap[f]] = mb[fam[f]].sum()
    body2 = body[keep].copy(); body2[remap[f]] = True
    return par2, D[keep], m2, body2, keep, remap

for li in pick:
    name, kind, reach = LAWS[li]
    t0 = time.time()
    ratio = collections.defaultdict(list); vpar = collections.defaultdict(list); vmed = []; vsun = []
    inward = collections.defaultdict(list); farc = {2: [], 10: []}; weak = collections.Counter(); nopull = collections.Counter()
    split_err = {'near': [], 'far': []}; split_exact = [0, 0]; split_window = [0, 0]; depth_ratio = []
    cost_sim = []; cost_sim_nodes = []; cost_tex = []; cost_tex_mean = []; cost_cull = []; nwells = []; nterms = []
    violations = [0, 0]
    for key, z in zones.items():
        par, D, m, body, cls = pre[key]
        Z = float(z[0]['zoneRadius'])
        zn = ZoneLaw(kind, reach, par, D, m, body, Z)
        fam, depth = zn.fam, zn.depth
        kids = [k for k in range(len(par)) if par[k] >= 0 and body[par[k]]]
        cbody = np.where(body, strength(np.maximum(m, 1e-12)), 0.0)
        a_own = {k: zn.own(par[k], D[k]) for k in kids}
        for k in kids:
            v = np.sqrt(max(a_own[k], 0) * D[k]); vpar[cls[k]].append(v)
            if a_own[k] <= 0: nopull[cls[k]] += 1
            if a_own[k] < 0.5 * cbody[par[k]] / D[k]: weak[cls[k]] += 1
            if cls[k] == 'Planet' and z[par[k]]['type'] == 'Sun' and SUN_LO <= m[par[k]] <= SUN_HI: vmed.append(v)
            if cls[k] == 'Sun': vsun.append(v)
        mz = np.where(body, m, 0).sum()
        acc_rel = collections.defaultdict(float); mids = []; farc[2].append(0.0); farc[10].append(0.0)
        for dr in range(DRAWS):
            pos = place(par, D, depth)
            Aout = zn.field(pos, pos, exclude_family_of=np.arange(len(par)))
            for k in kids:
                pk = par[k]; u = pos[pk] - pos[k]; u /= np.linalg.norm(u)
                acc_rel[k] += np.dot(Aout[k] - Aout[pk], u) / DRAWS
            X = 0.5 * Z * RING8
            AX = zn.field(pos, X)
            mids.append(np.mean(-(AX * X).sum(1) / np.linalg.norm(X, axis=1)))
            for fz in (2, 10):
                XF = fz * Z * RING8; AF = zn.field(pos, XF)
                farc[fz][-1] += np.mean(-(AF * XF).sum(1)) / DRAWS / strength(mz)
        inward[Z].append(np.mean(mids))
        for k in kids:
            if a_own[k] > 0: ratio[cls[k]].append(np.sqrt(max(acc_rel[k], 0) / a_own[k]))

        # ---- split invariance: each non-root body family with massive descendants, merged into one body ----
        pos = place(par, D, depth)
        mb = np.where(body, m, 0.0)
        Lref = zn.B if kind in ('band', 'tapered') else ZoneLaw('band', HALF, par, D, m, body, Z).B
        for f in range(len(par)):
            if par[f] < 0 or not body[f] or mb[fam[f]].sum() <= mb[f]: continue
            par2, D2, m2, body2, keepi, remap = merged(par, D, m, body, f)
            z2 = ZoneLaw(kind, reach, par2, D2, m2, body2, Z)
            pos2 = pos[keepi]
            for tag, X in (('near', pos[f] + 1.05 * Lref[f] * RING8), ('far', 2 * Z * RING8)):
                a1 = zn.field(pos, X); a2 = z2.field(pos2, X)
                split_err[tag].append(np.max(np.linalg.norm(a1 - a2, axis=1) / np.maximum(np.linalg.norm(a2, axis=1), 1e-12)))
            if kind in ('band', 'tapered'):
                same = int(zn.Lam[f] == z2.Lam[remap[f]])
                split_exact[1] += 1; split_exact[0] += same
                # families the ruled generator window would hold: every descendant within half its parent's tidal
                # radius (B25: d_H = D sqrt(c / 2 c_parent); a parent with no body parent has no window and is skipped)
                ok = True
                for j in range(len(par)):
                    if j == f or not fam[f, j]: continue
                    p = par[j]
                    if body[p] and par[p] >= 0 and body[par[p]]:
                        ok &= D[j] <= 0.5 * D[p] * np.sqrt(cbody[p] / (2 * cbody[par[p]]))
                if ok:
                    split_window[1] += 1; split_window[0] += same
        # ---- whole zone merged: depth of the deepest point against one body of the zone's mass ----
        g = np.linspace(-Z, Z, 33); G = np.concatenate([np.stack(np.meshgrid(g, g), -1).reshape(-1, 2), pos[body]])
        ref = zn.potential(pos, 10 * Z * RING8).mean()
        z1 = ZoneLaw(kind, reach, np.array([-1]), np.array([0.0]), np.array([mz]), np.array([True]), Z)
        p1 = np.zeros((1, 2)); ref1 = z1.potential(p1, 10 * Z * RING8).mean()
        depth_ratio.append((ref - zn.potential(pos, G).min()) / (ref1 - z1.potential(p1, p1).min()))

        # ---- evaluation cost ----
        nwells.append(int(body.sum()))
        if kind in ('band', 'tapered'):
            T = zn.T; own = np.array([t[0] for t in T]); ell = np.array([t[4] for t in T])
            nterms.append(len(T))
            # containment: every term lies inside each ancestor's own support L (path-length bound)
            for t in T:
                a, path = t[0], 0.0
                while par[a] >= 0:
                    path += D[a]; a = par[a]
                    violations[1] += 1
                    violations[0] += int(path + t[4] > zn.B[a] * (1 + 1e-9))
            for dr in range(2):
                pos = place(par, D, depth)
                rr = Z * np.sqrt(rng.uniform(0, 1, 64)); th = rng.uniform(0, 2 * np.pi, 64)
                S = np.stack([rr * np.cos(th), rr * np.sin(th)], 1)
                dist = np.linalg.norm(pos[own][None, :, :] - S[:, None, :], axis=2)
                cost_sim += list((dist < ell[None, :]).sum(1))
                distn = np.linalg.norm(pos[None, :, :] - S[:, None, :], axis=2)
                inside = distn < zn.Lam[None, :]
                vis = np.zeros_like(inside)
                for k in np.argsort(depth, kind='stable'):
                    vis[:, k] = inside[:, k] & (vis[:, par[k]] if par[k] >= 0 else True)
                ntests = np.zeros(64)
                for k in range(len(par)):
                    if par[k] >= 0: ntests += vis[:, par[k]]
                cost_sim_nodes += list(ntests)
                cx = Z * np.sqrt(rng.uniform()) * np.array([np.cos(th[0]), np.sin(th[0])])
                gx = cx[0] + np.linspace(-4096, 4096, 64); gy = cx[1] + np.linspace(-4096, 4096, 64)
                P = np.stack(np.meshgrid(gx, gy), -1).reshape(-1, 2)
                dist = np.linalg.norm(pos[own][None, :, :] - P[:, None, :], axis=2)
                cnt = (dist < ell[None, :]).sum(1)
                cost_tex += list(cnt); cost_tex_mean.append(cnt.mean())
                dx = np.maximum(np.abs(pos[own][:, 0] - cx[0]) - 4096, 0); dy = np.maximum(np.abs(pos[own][:, 1] - cx[1]) - 4096, 0)
                cost_cull.append(int((np.hypot(dx, dy) < ell).sum()))

    print(f'== [{li}] {name}  ({time.time() - t0:.0f}s, {DRAWS} draws)')
    for k in ['Sun', 'GasGiant', 'Planet', 'Planetoid', 'Belt']:
        print(f'  {k:9s} ratio p10/50/90/99 {q(ratio[k], (10, 50, 90, 99))}  parent-only v p10/50/90 {q(vpar[k])}  n {len(ratio[k])}')
    vmed = np.array(vmed)
    print(f'  planets round a median sun: v p10/50/90 {q(vmed)}; share in 12-18 {np.mean((vmed >= 12) & (vmed <= 18)):.2f}; n {len(vmed)}')
    print(f'  Sun-class children parent-only v p10/50/90 {q(vsun)}')
    print('  children with no parent pull: ' + str(dict(nopull)) + '; parent pull < 0.5 c/D: ' + str(dict(weak)) + f' (total {sum(weak.values())})')
    print('  mid-zone inward pull by Z: ' + ', '.join(f'{Z:.0f}: {np.median(v):.2f}' for Z, v in sorted(inward.items())))
    print('  zone far-field c / c(M_zone) p10/50/90: at 2Z ' + str(q(farc[2])) + ', at 10Z ' + str(q(farc[10])))
    print(f'  split invariance (family merged into one body), max |da|/|a| p50/p90/p99: at 1.05 B_f {q(split_err["near"], (50, 90, 99))}, at 2Z {q(split_err["far"], (50, 90, 99))}; n {len(split_err["near"])}'
          + (f'; families whose reach the merge leaves unchanged {split_exact[0]}/{split_exact[1]}, of those inside the ruled half-tidal window {split_window[0]}/{split_window[1]}' if kind in ('band', 'tapered') else ''))
    print(f'  whole-zone depth over one body of the zone mass, p10/50/90: {q(depth_ratio)}')
    print(f'  wells (bodies) per zone p50/p99: {q(nwells, (50, 99))}')
    if kind in ('band', 'tapered'):
        print(f'  containment violations (term outside an ancestor support): {violations[0]} of {violations[1]}')
        print(f'  terms per zone p50/p99: {q(nterms, (50, 99))}')
        print(f'  sim sample (uniform in the zone disc): terms in support p50/p99/mean {q(cost_sim, (50, 99))}/{np.mean(cost_sim):.2f}; tree tests p50/p99 {q(cost_sim_nodes, (50, 99))}')
        print(f'  shader texel (8192 u window): terms in support p50/p99 {q(cost_tex, (50, 99))}, mean per texel p50/p99 over windows {q(cost_tex_mean, (50, 99))}; terms meeting the window p50/p99 {q(cost_cull, (50, 99))}')
    sys.stdout.flush()
