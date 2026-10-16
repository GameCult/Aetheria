# Basin readability probe and minimap renders (aetheria-gravity, imagination-ag3-well-law, r3 pass, 2026-10-15).
# For ruling wells-read-as-nested-basins. Per candidate (law + generator rule), over the same zones and phases:
#   cusp     every body-parent/child pair: the potential along the segment child -> parent has an interior saddle
#            (a local maximum after the child's own minimum); prominence = (saddle - child minimum) / c_child.
#   grid     on a sample of zones, at the minimap's texel (15.625 u: ARPG.unity camera "Map Gravity" orthographic
#            size 1000 at scene line 7812 over the 128^2 MinimapGravity.renderTexture, lines 15-16):
#            dead ground  share of the zone disc where |F| < 0.05 u/s^2 (a 10 u drift over a 20 s coast, about a
#                         hull length; 1% of a packed Djinni's 4 u/s^2, map B25);
#            steep        share where the port's danger blend > 0.5 (the orange "freighter no-go");
#            lanes open   the non-steep part of the disc is one region (islands under 0.5% of the disc ignored);
#            basins       steepest descent on the drawn height (8 neighbours): a body's basin is the one holding its
#                         cell; blob = its basin is shared with another body or its prominence (spill height - basin
#                         minimum) is under the minimap contour spacing DR/20; nested = it spills into its parent's
#                         basin; saddle curvature = Hessian eigenvalues of phi at the spill point.
#   renders  a numpy port of Assets/Shaders/IsolinesUI.shader (constants cited where used) on four zones.
# Usage: python probe4.py bodies.csv outdir [candidate-index,...] [sample stride, default 25 = 60 zones]
import os, sys, math, zlib, struct, collections, hashlib
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
DATA, OUT = sys.argv[1], sys.argv[2]
ONLY = [int(x) for x in sys.argv[3].split(',')] if len(sys.argv) > 3 else None
STRIDE = int(sys.argv[4]) if len(sys.argv) > 4 else 25
os.makedirs(OUT, exist_ok=True)
sys.argv = ['probe3.py', DATA, '1', '0']
src = open(os.path.join(HERE, 'probe3.py'), encoding='utf-8').read()
g = {'__file__': os.path.join(HERE, 'probe3.py'), '__name__': 'probe3_defs'}
exec(src[:src.index('for li in pick:')], g)        # definitions, data and zones only; the probe loop does not run
import replace as RP, well_law as WL
zones, pre, ZoneLaw, strength, core_radius, family = g['zones'], g['pre'], g['ZoneLaw'], g['strength'], g['core_radius'], g['family']
HALF = g['HALF']

TEXEL = 2000.0 / 128          # minimap texel in u (see header)
K_TODAY, K_BAND = 4.0983607, 2.0491803     # Settings.asset 125 today; halved for the Plummer peak (gravity-well-law.md)
FLAT = 0.05

CANDIDATES = [
    # name, law kind, reach, K, placement (lam, cusp, spacing) or None
    ('C0 today law, today generator', 'today', None, K_TODAY, None),
    ('C1 band law (reach D/2), today generator', 'band', HALF, K_BAND, None),
    ('C2 band law + strict generator', 'band', HALF, K_BAND, (0.5, 0.0, 1.0)),
    ('C3 band law + strict generator + cusp bound', 'band', HALF, K_BAND, (0.5, 1.5, 1.0)),
    ('C4 band law + loose spacing + cusp bound', 'band', HALF, K_BAND, (0.5, 1.5, 0.5)),
]

def phases(key, ids):
    """One phase per original node, the same in every candidate (hash of zone key and node index)."""
    return np.array([int(hashlib.md5(f'{key}-{i}'.encode()).hexdigest()[:8], 16) / 2 ** 32 * 2 * np.pi for i in ids])

def place_fixed(par, D, depth, th):
    pos = np.zeros((len(par), 2))
    for k in np.argsort(depth, kind='stable'):
        if par[k] >= 0: pos[k] = pos[par[k]] + D[k] * np.array([np.cos(th[k]), np.sin(th[k])])
    return pos

def zone_for(cand, key, Z):
    par, D, m, body, cls = pre[key]
    ids = np.arange(len(par))
    if cand[4] is not None:
        lam, cusp, spacing = cand[4]
        par, D, m, body, cls, st = RP.replace_zone(par, D, m, body, cls, Z, lam, cusp, spacing)
        ids = st['idx']
    zn = ZoneLaw(cand[1], cand[2], par, D, m, body, Z)
    fam, depth = family(par)
    pos = place_fixed(par, D, depth, phases(key, ids))
    return zn, par, D, m, body, cls, pos

def reference(zn, Z):
    if zn.kind in ('band', 'tapered'):
        own, c, e, a, b = g['_arr'](zn.T)
        return float((c * WL.band_potential(e, a, b, np.full_like(c, Z))).sum())
    return float(np.where(zn.c > 0, zn.law['P'](zn.c, zn.e, np.full_like(zn.c, Z), zn.s), 0).sum())

def heights(zn, pos, X, Z, K):
    out = np.empty(len(X))
    for i in range(0, len(X), 20000):
        out[i:i + 20000] = zn.potential(pos, X[i:i + 20000])
    return (out - reference(zn, Z)) / K

def forces(zn, pos, X):
    out = np.empty((len(X), 2))
    for i in range(0, len(X), 20000):
        out[i:i + 20000] = zn.field(pos, X[i:i + 20000])
    return out

def q(v, ps=(10, 50, 90)):
    v = np.sort(np.asarray(v, float))
    return [round(float(v[min(len(v) - 1, int(p / 100 * len(v)))]), 3) for p in ps] if len(v) else []

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)

# ---- the minimap port: Assets/Shaders/IsolinesUI.shader frag (lines 151-207) with Minimap Gravity Lines.mat values ----
MAT = dict(Color=np.array([0, .4, .6, 1]), AngleColor=np.array([1.4980392, 0, 1.2705883, .5019608]),
           DangerColor=np.array([2.670157, .22367807, 0, 1]), LineWidth=.15, LineFade=5., AngleWidth=.05, AngleFade=1.15,
           DangerSteepness=1.5, Scale=10.)   # Assets/Materials/UI/Minimap Gravity Lines.mat, m_Floats and m_Colors
def minimap(h, depth_range):
    """h: drawn height grid (GetHeight, <= 0 in wells), rows = +y. Returns rgba per the shader's col."""
    height = h                                                     # shader 161: height = -tex, tex = -GetHeight
    me = height
    e = np.roll(height, -1, axis=1); n = np.roll(height, -1, axis=0)
    plan = np.stack([e - me, n - me], -1)                          # calcGrad, shader 144-149 (forward, one texel)
    slope = np.linalg.norm(plan, axis=-1) + 1e-12                  # shader 171
    start, dr = 0.0, depth_range                                    # ZoneRenderer.cs 625-626: _StartDepth 0, _DepthRange = max planet depth
    blend = smoothstep(start, start + dr / 32, -height)             # shader 165
    danger = smoothstep(0, MAT['DangerSteepness'], (slope / MAT['Scale']) ** 2)   # shader 173
    linecol = (MAT['Color'][None, None] * (1 - danger[..., None]) + MAT['DangerColor'][None, None] * danger[..., None]) * blend[..., None]
    col = np.zeros(h.shape + (4,))
    spacing = dr / 20                                                # shader 178
    for ih in range(1, 22):                                          # shader 179-182
        iso = np.abs(height + start + ih * spacing)
        col += (1 - smoothstep(MAT['LineWidth'], MAT['LineWidth'] * MAT['LineFade'], iso / slope))[..., None] * linecol
    angle = np.arctan2(plan[..., 1], plan[..., 0]) / np.pi + 1     # shader 184
    angcol = (blend * slope / MAT['Scale'])[..., None] * (MAT['AngleColor'][None, None] * (1 - danger[..., None]) + MAT['DangerColor'][None, None] * danger[..., None])
    for ia in np.arange(0.5, 13, 1.0):                               # shader 195-201
        iso = np.abs(angle - ia / 6.0)
        l = 1 - smoothstep(MAT['AngleWidth'], MAT['AngleWidth'] * MAT['AngleFade'], iso)
        l = l * smoothstep(-start - dr, -start, height)
        col += l[..., None] * angcol
    return col, danger

def write_png(path, rgb):
    hgt, wid, _ = rgb.shape
    raw = b''.join(b'\x00' + rgb[y].tobytes() for y in range(hgt - 1, -1, -1))   # rows = +y, PNG top row = max y
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', wid, hgt, 8, 2, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def composite(col):
    bg = np.array([.02, .03, .05])                                   # stand-in for the Minimap Background material
    a = np.clip(col[..., 3:4], 0, 1)
    rgb = np.clip(col[..., :3], 0, 1) * a + bg * (1 - a)
    return (rgb * 255).astype(np.uint8)

# ---- grid analysis ----
def grid_for(Z):
    nx = int(math.ceil(2.1 * Z / TEXEL))
    xs = (np.arange(nx) - (nx - 1) / 2) * TEXEL
    X = np.stack(np.meshgrid(xs, xs), -1).reshape(-1, 2)            # rows = +y
    return xs, X, nx

def labels_by_descent(h):
    ny, nx = h.shape
    idx = np.arange(ny * nx).reshape(ny, nx)
    best = idx.copy(); bh = h.copy()
    pad = np.pad(h, 1, constant_values=np.inf); pidx = np.pad(idx, 1, constant_values=-1)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0: continue
            nh = pad[1 + dy:1 + dy + ny, 1 + dx:1 + dx + nx]; ni = pidx[1 + dy:1 + dy + ny, 1 + dx:1 + dx + nx]
            better = nh < bh
            bh = np.where(better, nh, bh); best = np.where(better, ni, best)
    lab = best.ravel()
    for _ in range(64):
        nxt = lab[lab]
        if np.array_equal(nxt, lab): break
        lab = nxt
    return lab.reshape(ny, nx)

def spills(h, lab):
    """For every basin: (spill height, spill-into basin, spill cell pair) over 4-neighbour boundaries."""
    best = {}
    for ax in (0, 1):
        a = lab; b = np.roll(lab, -1, axis=ax); ha = h; hb = np.roll(h, -1, axis=ax)
        sl = (slice(None, -1), slice(None)) if ax == 0 else (slice(None), slice(None, -1))
        a, b, ha, hb = a[sl], b[sl], ha[sl], hb[sl]
        mask = a != b
        pas = np.maximum(ha, hb)[mask]; la = a[mask]; lb = b[mask]
        ys, xs = np.nonzero(mask)
        order = np.argsort(pas)
        for o in order:
            for (u, v) in ((la[o], lb[o]), (lb[o], la[o])):
                if u not in best:
                    y, x = ys[o], xs[o]
                    best[u] = (pas[o], v, (y, x, ax))
    return best

def components(mask):
    """4-connected components by min-label propagation with pointer jumping (labels are cell indices)."""
    ny, nx = mask.shape
    big = ny * nx
    lab = np.where(mask, np.arange(big).reshape(ny, nx), big).ravel()
    flat_mask = mask.ravel()
    while True:
        old = lab.copy()
        L = lab.reshape(ny, nx)
        for ax in (0, 1):
            for sh in (1, -1):
                r = np.roll(L, sh, axis=ax)
                L = np.where(mask, np.minimum(L, r), big)
        lab = L.ravel()
        ext = np.append(lab, big)
        for _ in range(20):
            lab = np.where(flat_mask, np.minimum(lab, ext[lab]), big); ext = np.append(lab, big)
        if np.array_equal(lab, old): break
    return lab.reshape(ny, nx)

def analyse(cand, key, z):
    Z = float(z[0]['zoneRadius'])
    zn, par, D, m, body, cls, pos = zone_for(cand, key, Z)
    xs, X, nx = grid_for(Z)
    h = heights(zn, pos, X, Z, cand[3]).reshape(nx, nx)
    F = forces(zn, pos, X)
    disc = (np.linalg.norm(X, axis=1) <= Z).reshape(nx, nx)
    bodies = np.where(body)[0]
    cell = lambda p: (int(round(p[1] / TEXEL + (nx - 1) / 2)), int(round(p[0] / TEXEL + (nx - 1) / 2)))
    dr = max(1.0, max(-heights(zn, pos, pos[bodies], Z, cand[3]).min(), 0.0))
    col, danger = minimap(h, dr)
    steep = danger > 0.5
    dead = (np.linalg.norm(F, axis=1) < FLAT).reshape(nx, nx)
    free = disc & ~steep
    comp = components(free)
    sizes = collections.Counter(comp[free].tolist())
    big = [s for s in sizes.values() if s >= 0.005 * disc.sum()]
    lab = labels_by_descent(h)
    sp = spills(h, lab)
    out = dict(dead=dead[disc].mean(), steep=steep[disc].mean(), lanes=len(big) <= 1, blob=0, nb=0, nested=0, nn=0, curv=[], dr=dr)
    bl = {j: lab[cell(pos[j])] if 0 <= cell(pos[j])[0] < nx and 0 <= cell(pos[j])[1] < nx else None for j in bodies}
    share = collections.Counter(v for v in bl.values() if v is not None)
    for j in bodies:
        if par[j] < 0 or bl[j] is None: continue
        out['nb'] += 1
        b = bl[j]
        prom = (sp[b][0] - h.ravel()[b]) if b in sp else 0.0
        if share[b] > 1 or prom < dr / 20: out['blob'] += 1
        p = par[j]
        if body[p] and bl.get(p) is not None and b in sp:
            out['nn'] += 1; out['nested'] += int(sp[b][1] == bl[p])
        if b in sp and share[b] == 1:
            y, x, ax = sp[b][2]
            pt = np.array([xs[x] + (TEXEL / 2 if ax == 1 else 0), xs[y] + (TEXEL / 2 if ax == 0 else 0)])
            e = 1.0
            Fx1, Fx0 = zn.field(pos, (pt + [e, 0])[None])[0], zn.field(pos, (pt - [e, 0])[None])[0]
            Fy1, Fy0 = zn.field(pos, (pt + [0, e])[None])[0], zn.field(pos, (pt - [0, e])[None])[0]
            H = -np.array([(Fx1 - Fx0) / (2 * e), (Fy1 - Fy0) / (2 * e)])   # Hessian of phi = -dF/dx
            ev = np.linalg.eigvalsh((H + H.T) / 2)
            out['curv'].append(float(np.min(np.abs(ev))))
    return out, h, col, zn, pos, body, par, cls

def cusp_pairs(cand, key, z):
    Z = float(z[0]['zoneRadius'])
    zn, par, D, m, body, cls, pos = zone_for(cand, key, Z)
    res = []
    t = np.concatenate([np.geomspace(1e-4, 0.05, 150), np.linspace(0.05, 0.98, 250)[1:]])
    for k in range(len(par)):
        p = par[k]
        if p < 0 or not body[p] or not body[k]: continue
        X = pos[k][None] + t[:, None] * (pos[p] - pos[k])[None]
        X = np.concatenate([pos[k][None], X])
        phi = zn.potential(pos, X)
        ck = strength(m[k])
        i0 = int(np.argmin(phi[:60]))                                 # the child's own minimum, near the child
        rest = phi[i0:]
        interior = [i for i in range(1, len(rest) - 1) if rest[i] >= rest[i - 1] and rest[i] > rest[i + 1]]
        if interior:
            s_ = interior[0]
            res.append((cls[k], True, (rest[s_] - rest[:s_ + 1].min()) / ck))
        else:
            res.append((cls[k], False, 0.0))
    return res

if __name__ == '__main__':
    keys = sorted(zones.keys())
    # the four render zones, picked from today's generator (same zones in every candidate)
    def stats_of(key):
        par, D, m, body, cls = pre[key]; fam, depth = family(par)
        bodyless = [i for i in range(len(par)) if not body[i] and cls[i] != 'Belt']
        return dict(Z=float(zones[key][0]['zoneRadius']), moons=int(((depth >= 2) & body).sum()),
                    rosette=any(par[i] < 0 for i in bodyless), binary=any(par[i] >= 0 for i in bodyless), n=len(par))
    S = {k: stats_of(k) for k in keys}
    pick = {
        'small': next(k for k in keys if S[k]['Z'] == 1000 and S[k]['n'] >= 10 and not S[k]['rosette']),
        'binary': next(k for k in keys if S[k]['binary'] and S[k]['Z'] == 2125),
        'rosette': next(k for k in keys if S[k]['rosette'] and abs(S[k]['Z'] - 4182) < 1),
        'moons': max((k for k in keys if abs(S[k]['Z'] - 6846) < 1 and not S[k]['rosette']), key=lambda k: S[k]['moons']),
    }
    print('render zones:', {n: (k, S[k]) for n, k in pick.items()})
    sample = [k for i, k in enumerate(keys) if i % STRIDE == 0]      # every STRIDE-th zone (60 at 25), every Z in proportion
    for ci, cand in enumerate(CANDIDATES):
        if ONLY is not None and ci not in ONLY: continue
        cusp = collections.defaultdict(list); vanished = collections.Counter(); total = collections.Counter()
        for key in keys:
            for c, ok, pr in cusp_pairs(cand, key, zones[key]):
                total[c] += 1
                if ok: cusp[c].append(pr)
                else: vanished[c] += 1
        agg = collections.defaultdict(list)
        for key in sample:
            o = analyse(cand, key, zones[key])[0]
            for kk in ('dead', 'steep', 'lanes'): agg[kk].append(o[kk])
            agg['blob'].append((o['blob'], o['nb'])); agg['nested'].append((o['nested'], o['nn'])); agg['curv'] += o['curv']
        blob = sum(a for a, b in agg['blob']) / max(1, sum(b for a, b in agg['blob']))
        nest = sum(a for a, b in agg['nested']) / max(1, sum(b for a, b in agg['nested']))
        print(f'== {cand[0]}')
        for c in ('Sun', 'GasGiant', 'Planet', 'Planetoid'):
            print(f'  cusp {c:9s}: vanished {vanished[c]}/{total[c]}; prominence/c p10/50 {q(cusp[c], (10, 50))}')
        print(f'  dead ground (|F| < {FLAT}) share of disc p50/p90 {q(agg["dead"], (50, 90))}; steep share p50/p90 {q(agg["steep"], (50, 90))}; '
              f'lanes open in {np.mean(agg["lanes"]):.2f} of zones')
        print(f'  blob share {blob:.3f}; nested share {nest:.3f}; saddle min |eigenvalue| p50 {q(agg["curv"], (50,))} 1/s^2 (n {len(agg["curv"])})')
        for name, key in pick.items():
            o, h, col, zn, pos, body, par, cls = analyse(cand, key, zones[key])
            write_png(os.path.join(OUT, f'C{ci}-{name}.png'), composite(col))
            print(f'  render {name} {key}: steep {o["steep"]:.3f}, dead {o["dead"]:.3f}, lanes {o["lanes"]}, blob {o["blob"]}/{o["nb"]}, '
                  f'nested {o["nested"]}/{o["nn"]}, bodies {int(body.sum())}, depth range {o["dr"]:.1f}')
        sys.stdout.flush()
