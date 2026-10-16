# Tail-taming probe over docs/research/2026-10-13-orbit-probe/data/bodies.csv (1501 zones, 31,640 nodes).
# For each candidate law: (1) relative radial pull on a child, phase-averaged over random phases, over its parent's own
# pull -> circular-speed ratio sqrt(a_rel / a_parent); (2) B21's mid-zone inward pull; (3) parent-only rail speeds.
# a_rel = radial component, toward the parent, of [field at the child from every body outside the child's family]
#         minus [field at the parent from every body outside the parent's family] (the parent rides its own rail),
#         with the disc term included where a law subtracts one.
# Usage: python probe.py bodies.csv [draws] [law-index,...]
import csv, sys, os, collections, time
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from laws import *

rng = np.random.default_rng(7)
DRAWS = int(sys.argv[2]) if len(sys.argv) > 2 else 6
rows = [r for r in csv.DictReader(open(sys.argv[1])) if r.get('type')]
zones = collections.defaultdict(list)
for r in rows: zones[(r['density'], r['seed'])].append(r)

def hill2(D, c, cpar):  # 2 x B25's tidal radius d_H = D sqrt(c / (2 c_parent)); a root or bodyless-parent child: D
    ok = cpar > 0
    return np.where(ok, 2 * D * np.sqrt(c / (2 * np.where(ok, cpar, 1.0))), D)

ONE = lambda m, D, Z, c, cp: np.ones_like(m)
# (name, tail, p, scale(m, D, Z, c, c_parent) -> per-body parameter, subtract disc)
LAWS = [
    ('log (today)', tail_log, 0.2, ONE, False),
    ('K0 lam=0.5*D', tail_k0, 0.2, lambda m, D, Z, c, cp: 0.5 * D, False),
    ('K0 lam=1*D', tail_k0, 0.2, lambda m, D, Z, c, cp: 1.0 * D, False),
    ('K0 lam=2*d_Hill', tail_k0, 0.2, lambda m, D, Z, c, cp: hill2(D, c, cp), False),
    ('cap c*L/r^2, L=100m^.25', tail_cap, 0.2, lambda m, D, Z, c, cp: 100 * m ** 0.25, False),
    ('cap c*L/r^2, L=300m^.25', tail_cap, 0.2, lambda m, D, Z, c, cp: 300 * m ** 0.25, False),
    ('switch L..2L, L=100m^.25', tail_switch, 0.2, lambda m, D, Z, c, cp: 100 * m ** 0.25, False),
    ('switch L..2L, L=300m^.25', tail_switch, 0.2, lambda m, D, Z, c, cp: 300 * m ** 0.25, False),
    ('uniform-disc subtraction', tail_log, 0.2, ONE, True),
    ('exponent p=0.5', tail_log, 0.5, ONE, False),
    ('hyperbolic kernel ell=Z/2', tail_hyp, 0.2, lambda m, D, Z, c, cp: 0.5 * Z * np.ones_like(m), False),
]
pick = [int(x) for x in sys.argv[3].split(',')] if len(sys.argv) > 3 else range(len(LAWS))

def zone_struct(z):
    idx = {int(r['idx']): k for k, r in enumerate(z)}
    n = len(z)
    par = np.array([idx.get(int(r['parentIdx']), -1) for r in z])
    D = np.array([float(r['distance']) for r in z]); m = np.array([float(r['mass']) for r in z])
    body = np.array([r['empty'] == '0' and r['belt'] == '0' for r in z])
    cls = ['Belt' if r['belt'] == '1' else r['type'] for r in z]
    fam = np.eye(n, dtype=bool)  # fam[i, j]: j is i or a descendant of i
    depth = np.zeros(n, int)
    for j in range(n):
        a = par[j]
        while a >= 0:
            fam[a, j] = True; depth[j] += 1; a = par[a]
    order = list(np.argsort(depth, kind='stable'))
    return par, D, m, body, cls, fam, order

def place(par, D, order):
    pos = np.zeros((len(par), 2)); th = rng.uniform(0, 2 * np.pi, len(par))
    for k in order:
        if par[k] >= 0: pos[k] = pos[par[k]] + D[k] * np.array([np.cos(th[k]), np.sin(th[k])])
    return pos

def field(pos_b, c, R, s, tail, x):
    """Per-body acceleration contributions at points x: (n, nb, 2), each toward its body."""
    d = pos_b[None, :, :] - x[:, None, :]; r = np.linalg.norm(d, axis=2)
    rs = np.maximum(r, 1e-6)
    F = np.where(r < 1e-6, 0, pull(tail, c[None, :], R[None, :], rs, s[None, :]))
    return d / rs[:, :, None] * F[:, :, None]

def disc_acc(x, C, Z):  # field of a uniform disc of total strength C over radius Z centred at 0 (inward)
    r = np.linalg.norm(x, axis=1, keepdims=True); rs = np.maximum(r, 1e-9)
    return -x / rs * np.where(r < Z, C * r / Z ** 2, C / rs)

def q(v, ps=(10, 50, 90)):
    v = np.sort(np.asarray(v))
    return [round(float(v[min(len(v) - 1, int(p / 100 * len(v)))]), 2) for p in ps] if len(v) else []

pre = {k: zone_struct(z) for k, z in zones.items()}
sunm = np.sort([float(r['mass']) for r in rows if r['type'] == 'Sun' and r['empty'] == '0'])
SUN_LO, SUN_HI = sunm[int(.4 * len(sunm))], sunm[int(.6 * len(sunm))]  # 'median sun': mass between p40 and p60
for li in pick:
    name, tail, p, scale, disc = LAWS[li]
    t0 = time.time()
    ratio = collections.defaultdict(list); vpar = collections.defaultdict(list); vsp = []; vmed = []
    inward = collections.defaultdict(list)
    for key, z in zones.items():
        par, D, m, body, cls, fam, order = pre[key]
        Z = float(z[0]['zoneRadius'])
        c = np.where(body, strength(m, p), 0.0); R = core_radius(m)
        cpar = np.array([c[par[k]] if par[k] >= 0 else 0.0 for k in range(len(par))])
        Dz = np.where(par < 0, Z, D)
        s = scale(m, Dz, Z, c, cpar)
        bi = np.where(body)[0]; bpos = {j: t for t, j in enumerate(bi)}
        kids = [k for k in range(len(par)) if par[k] >= 0 and body[par[k]]]
        a_own = {k: float(pull(tail, c[par[k]], R[par[k]], D[k], s[par[k]])) for k in kids}
        for k in kids:
            v = np.sqrt(a_own[k] * D[k]); vpar[cls[k]].append(v)
            if cls[k] == 'Planet' and z[par[k]]['type'] == 'Sun':
                vsp.append(v)
                if SUN_LO <= m[par[k]] <= SUN_HI: vmed.append(v)
        acc_rel = collections.defaultdict(float); mids = []
        fb = fam[:, bi][:, :, None]
        for dr in range(DRAWS):
            pos = place(par, D, order)
            A = field(pos[bi], c[bi], R[bi], s[bi], tail, pos)
            Aout = np.where(fb, 0, A).sum(axis=1)       # at node k, from bodies outside k's family
            if disc: Aout = Aout - disc_acc(pos, c.sum(), Z)
            for k in kids:
                pk = par[k]; u = pos[pk] - pos[k]; u /= np.linalg.norm(u)
                acc_rel[k] += np.dot(Aout[k] - Aout[pk], u) / DRAWS   # Aout[k] includes the parent's own pull
            th = np.arange(8) * np.pi / 4; X = 0.5 * Z * np.stack([np.cos(th), np.sin(th)], 1)
            AX = field(pos[bi], c[bi], R[bi], s[bi], tail, X).sum(axis=1)
            if disc: AX = AX - disc_acc(X, c.sum(), Z)
            mids.append(np.mean(-(AX * X).sum(1) / np.linalg.norm(X, axis=1)))
        inward[Z].append(np.mean(mids))
        for k in kids:
            if a_own[k] > 0: ratio[cls[k]].append(np.sqrt(max(acc_rel[k], 0) / a_own[k]))
    vsp = np.array(vsp)
    print(f'== [{li}] {name}  ({time.time() - t0:.0f}s, {DRAWS} draws)')
    for k in ['Sun', 'GasGiant', 'Planet', 'Planetoid', 'Belt']:
        print(f'  {k:9s} ratio p10/50/90/99 {q(ratio[k], (10, 50, 90, 99))}  parent-only v p10/50/90 {q(vpar[k])}  n {len(ratio[k])}')
    print(f'  planets round suns: parent-only v p10/50/90 {q(vsp)}; share in 12-18 u/s {np.mean((vsp >= 12) & (vsp <= 18)):.2f}')
    vmed = np.array(vmed)
    print(f'  planets round a median sun (mass p40-p60, {SUN_LO:.0f}-{SUN_HI:.0f}): v p10/50/90 {q(vmed)}; share in 12-18 {np.mean((vmed >= 12) & (vmed <= 18)):.2f}; n {len(vmed)}')
    nz = {k: sum(1 for x in vpar[k] if x == 0) for k in vpar}
    print('  children with no parent pull at their distance: ' + str({k: v for k, v in nz.items() if v}))
    print('  mid-zone inward pull by Z: ' + ', '.join(f'{Z:.0f}: {np.median(v):.2f}' for Z, v in sorted(inward.items())))
    sys.stdout.flush()
