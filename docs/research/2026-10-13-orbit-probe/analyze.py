import csv, json, collections, os, math, sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

HERE = os.path.dirname(os.path.abspath(__file__))
D = os.path.join(HERE, 'data')
S_STR, REF = 2.0, 60.0            # Settings.asset GravityStrength, Zone.GravityReferenceRate
SR = S_STR * REF
GATE = 0.001                      # |n_xz|^2 gate in Zone.GravityAcceleration
TP = 2 * math.pi
A_SHIP = 30.0                     # u/s^2: Longinus (mass 2500) + Small Drive (60k..90k thrust) = 24..36
OUT = os.path.join(HERE, 'out'); os.makedirs(OUT, exist_ok=True)
md = []   # report fragments
def P(s=''): md.append(s)

# ---------- load ----------
def load_profiles():
    cases, cur = {}, None
    for line in open(os.path.join(D, 'profiles.csv')):
        line = line.strip()
        if line.startswith('@@CASE'):
            cur = line.split()[1]; cases[cur] = []
        elif line and cur:
            p = line.split(',')
            if len(p) == 4: cases[cur].append([float(x) for x in p])
    return {k: np.array(v) for k, v in cases.items()}
cases = load_profiles()
meta = {}
for r in csv.DictReader(open(os.path.join(D, 'meta.csv'))):
    meta[(r['type'], r['tag'], int(r['waves']))] = dict(mass=float(r['mass']), Rg=float(r['wellRadius']), D=float(r['wellDepth']), body=float(r['bodyRadius']), n=int(r['n']))
EXP = 16.0

def well_h(r, Dd, Rg, e=EXP):
    x = 2 * r / Rg
    return -Dd * np.clip(1 - x * x, 0, None) ** e
def well_dh(r, Dd, Rg, e=EXP):
    x = 2 * r / Rg; u = np.clip(1 - x * x, 0, None)
    return np.where(x < 1, 4 * Dd * e * x * u ** (e - 1) / Rg, 0.0)
def well_lap(r, Dd, Rg, e=EXP):
    x = 2 * r / Rg; u = np.clip(1 - x * x, 1e-300, None)
    h2 = 8 * Dd * e / Rg ** 2 * u ** (e - 2) * (u - 2 * (e - 1) * x * x)
    return np.where(x < 1, h2 + well_dh(r, Dd, Rg, e) / np.maximum(r, 1e-9), 0.0)
def well_h2(r, Dd, Rg, e=EXP):
    x = 2 * r / Rg; u = np.clip(1 - x * x, 1e-300, None)
    return np.where(x < 1, 8 * Dd * e / Rg ** 2 * u ** (e - 2) * (u - 2 * (e - 1) * x * x), 0.0)
BOWL_E = 2.0; BOWL_D = 64.0
def bowl_h(r, Rz):  # PowerPulse(r/(2Rz),2)*64 -> x=r/Rz
    u = np.clip(1 - (r / Rz) ** 2, 0, None); return -BOWL_D * u ** BOWL_E
def bowl_dh(r, Rz):
    x = r / Rz; u = np.clip(1 - x * x, 0, None)
    return np.where(x < 1, 4 * BOWL_D * BOWL_E * x * u ** (BOWL_E - 1) / (2 * Rz), 0.0) if False else np.where(x < 1, BOWL_D * BOWL_E * u ** (BOWL_E - 1) * 2 * x / Rz, 0.0)
def bowl_lap(r, Rz):
    x = r / Rz; u = np.clip(1 - x * x, 1e-300, None)
    h2 = BOWL_D * BOWL_E / Rz ** 2 * (2 * u ** (BOWL_E - 1) + 2 * x * (BOWL_E - 1) * u ** (BOWL_E - 2) * (-2 * x))
    return np.where(x < 1, h2 + bowl_dh(r, Rz) / np.maximum(r, 1e-9), 0.0)

def accA(s):
    m = s * s / (1 + s * s)
    return np.where(m > GATE, SR * s * s, 0.0)
def period(r, a):
    with np.errstate(divide='ignore', invalid='ignore'):
        return np.where(a > 0, TP * np.sqrt(r / a), np.inf)
def stable(r, a):   # d(r^3 a)/dr > 0
    L = r ** 3 * a
    d = np.gradient(L, r)
    return d > 0

# ---------- cross-check of the analytic field vs real code ----------
P('## Cross-check: analytic reimplementation vs the real Zone (GetHeight / GetForce)')
P()
P('| case | max abs err h (units) | max rel err a_A (where a>0.01) | cells where real force is 0 but formula >0.001 | cells where formula is 0 but real >0.001 |')
P('|---|---|---|---|---|')
for name, arr in cases.items():
    r, h, fx, fy = arr.T
    if name.startswith('Bowl'):
        Rz = float(name.split('_R')[1]); hh = bowl_h(r, Rz); s = bowl_dh(r, Rz)
    else:
        t, tag, w = name.split('_'); m = meta[(t, tag, int(w[-1]))]
        if t in ('GasGiant', 'Sun') and w == 'waves1':
            continue
        hh = well_h(r, m['D'], m['Rg']); s = well_dh(r, m['D'], m['Rg'])
    a_real = np.hypot(fx, fy); a_f = accA(s)
    sel = a_f > 0.01
    rel = float(np.max(np.abs(a_real[sel] - a_f[sel]) / a_f[sel])) if sel.any() else float('nan')
    P(f'| {name} | {np.max(np.abs(h-hh)):.2e} | {rel:.2e} | {int(np.sum((a_real==0)&(a_f>1e-3)))} | {int(np.sum((a_f==0)&(a_real>1e-3)))} |')
P()
# direction sanity
r, h, fx, fy = cases['Planet_p50_waves0'].T
P(f'Direction: Planet_p50 force x component is {"negative (inward)" if np.all(fx[(np.hypot(fx,fy)>0)&(r<1000)]<=0) else "mixed"} for r<1000, y component max {np.max(np.abs(fy)):.2e}.')
P()

# ---------- waves contribution ----------
P('## Wave term (gas giants and suns)')
P()
P('| case | peak a with waves | peak a without | max rel diff of a over r<support |')
P('|---|---|---|---|')
for t in ('GasGiant', 'Sun'):
    for tag in ('p10', 'p50', 'p90'):
        a1 = np.hypot(*cases[f'{t}_{tag}_waves1'][:, 2:4].T); a0 = np.hypot(*cases[f'{t}_{tag}_waves0'][:, 2:4].T)
        rr = cases[f'{t}_{tag}_waves1'][:, 0]; m = meta[(t, tag, 1)]
        sel = rr < m['Rg'] / 2
        P(f'| {t} {tag} | {a1.max():.3f} | {a0.max():.3f} | {np.max(np.abs(a1[sel]-a0[sel]))/a0.max():.3f} of peak |')
P()

# ---------- bodies / bands ----------
rows = [r for r in csv.DictReader(open(os.path.join(D, 'bodies.csv'))) if r.get('zoneRadius') is not None and r.get('belt') is not None]
bands = collections.defaultdict(list)   # parentType -> list of (childType, n, p10, p50, p90)
tmp = collections.defaultdict(list)
for r in rows:
    if r['empty'] == '1' or r['parentIdx'] == '-1': continue
    tmp[(r['parentType'], r['type'])].append(float(r['distance']))
for (pt, ct), v in tmp.items():
    v = np.sort(v); q = lambda p: float(v[int((len(v) - 1) * p)])
    bands[pt].append((ct, len(v), q(.1), q(.5), q(.9)))
# stations: middle-upper half of parented, non-empty bodies sorted by distance, per zone
zones = collections.defaultdict(list)
for r in rows:
    if r['parentIdx'] != '-1' and r['empty'] != '1':
        zones[(r['density'], r['seed'])].append((float(r['distance']), r['parentType']))
st = collections.defaultdict(list)
for z, lst in zones.items():
    lst.sort(); cand = lst[len(lst) // 2:]
    for d, pt in cand: st[pt].append(d)
stationBands = {}
for pt, v in st.items():
    v = np.sort(v); stationBands[pt] = (len(v), *(float(v[int((len(v) - 1) * p)]) for p in (.1, .5, .9)))
P('## Where orbits are in the game (real generator: ZoneGenerator.GenerateEntities, 5 zone densities x 300 seeds, authored ZoneGenerationSettings)')
P()
P('Distance is OrbitData.Distance, relative to the parent body. Rail speed is 2*pi*d/OrbitPeriod(d) = 2*pi u/s at every d (OrbitPeriod = 1*d^1+0).')
P()
P('| parent type | child type | n | d p10 | d p50 | d p90 |')
P('|---|---|---|---|---|---|')
order = {'Sun': 0, 'GasGiant': 1, 'Planet': 2, 'Planetoid': 3}
for pt in sorted(bands, key=lambda x: order.get(x, 9)):
    for ct, n, a, b, c in sorted(bands[pt], key=lambda x: order.get(x[0], 9) if x[0] != 'Belt' else 5):
        P(f'| {pt} | {ct} | {n} | {a:.0f} | {b:.0f} | {c:.0f} |')
P()
P('Stations (ZoneGenerator.cs:225-310) sit on the orbit of a selected body (same Distance, phase +-60 deg, parent = that body\'s parent), chosen from the upper half of the distance-sorted bodies. Station distance by parent type, p10/p50/p90 over all candidates (upper-half pool, count unknown from the stationCount draw):')
P()
P('| parent type | n candidates | d p10 | d p50 | d p90 |')
P('|---|---|---|---|---|')
for pt in sorted(stationBands, key=lambda x: order.get(x, 9)):
    n, a, b, c = stationBands[pt]; P(f'| {pt} | {n} | {a:.0f} | {b:.0f} | {c:.0f} |')
P()
P('Belts are bodies in the table above (type Belt); their rocks sit at belt distance +- AsteroidBeltWidth(d) = 5*d^0.666+50 wide.')
P()

# ---------- per type analysis ----------
types = ['Planetoid', 'Planet', 'GasGiant', 'Sun']
summary = {}
colors = dict(A='#c0392b', B='#2471a3', rail='#1e8449', disc='#8e44ad')
GRID = [5, 10, 25, 50, 100, 150, 200, 300, 400, 500, 750, 1000, 1500, 2000, 3000, 4000, 5000]

def mark_for(parent_type, r):
    out = []
    for ct, n, a, b, c in bands.get(parent_type, []):
        if a <= r <= c: out.append(f'{ct}[{a:.0f}-{c:.0f}]')
    return ' '.join(out)

for t in types:
    for tag in ('p10', 'p50', 'p90'):
        m = meta[(t, tag, 0)]
        Rg, Dd = m['Rg'], m['D']; rs = Rg / 2
        r = np.linspace(rs / 20000, rs, 40000)
        s = well_dh(r, Dd, Rg); aA = accA(s)
        k = aA.max() / s.max(); aB = k * s
        summary[(t, tag)] = dict(mass=m['mass'], Rg=Rg, rs=rs, D=Dd, peakA=aA.max(), peakS=s.max(), k=k,
                                 r_gate_lo=float(r[np.argmax(aA > 0)]), r_peak=float(r[np.argmax(s)]))
    # ---- p50 detail tables
    tag = 'p50'; m = meta[(t, tag, 0)]; Rg, Dd = m['Rg'], m['D']; rs = Rg / 2
    sm = summary[(t, tag)]; k = sm['k']
    r = np.linspace(rs / 40000, rs, 40000)
    h = well_h(r, Dd, Rg); s = well_dh(r, Dd, Rg); aA = accA(s); aB = k * s
    TA = period(r, aA); TB = period(r, aB); stA = stable(r, aA); stB = stable(r, aB)
    lap = well_lap(r, Dd, Rg)
    P(f'### {t} (representative mass {m["mass"]:.0f} = p50 of {t}s in generated zones; well radius {Rg:.0f}, support radius {rs:.0f}, depth {Dd:.1f}, body radius {m["body"]:.1f}, exponent 16)')
    P()
    P(f'Peak slope |h\'| = {sm["peakS"]:.4f} at r = {sm["r_peak"]:.0f}. Peak a_A = S*R*s^2 = {sm["peakA"]:.4f} u/s^2. Gate (|n|^2 > 0.001 <=> s > 0.0316) switches the force on at r = {sm["r_gate_lo"]:.1f}. Reading B: k = {k:.3f} u/s^2 per unit slope (a_B = k*|h\'|, peak-matched).')
    P()
    P('| r | h(r) | slope s | a_A | T_A | a_B | T_B | T_rail=r | hold-rail thrust A (u/s^2, frac of 30) | hold-rail thrust B | stable A | stable B | Laplacian (h"+h\'/r) | orbits here |')
    P('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    pts = sorted(set([g for g in GRID if g < rs] + [round(rs * f) for f in (0.9, 0.99)] + [round(x) for _, _, a, b, c in bands.get(t, []) for x in (a, b, c) if x < rs]))
    for rr in pts:
        i = int(np.searchsorted(r, rr)); i = min(i, len(r) - 1)
        f = lambda v: ('inf' if not np.isfinite(v) else (f'{v:.3g}'))
        need = TP ** 2 / rr
        tA = need - aA[i]; tB = need - aB[i]
        P(f'| {rr:.0f} | {h[i]:.2f} | {s[i]:.4f} | {aA[i]:.4f} | {f(TA[i])} | {aB[i]:.4f} | {f(TB[i])} | {rr:.0f} | {tA:+.3f} ({abs(tA)/A_SHIP:.3%}) | {tB:+.3f} ({abs(tB)/A_SHIP:.3%}) | {"yes" if stA[i] else "no"} | {"yes" if stB[i] else "no"} | {lap[i]:+.2e} | {mark_for(t, rr)} |')
    P()
    # stability spans
    def spans(mask):
        out = []; on = False
        for i in range(len(r)):
            if mask[i] and not on: lo = r[i]; on = True
            if (not mask[i]) and on: out.append((lo, r[i - 1])); on = False
        if on: out.append((lo, r[-1]))
        return out
    def fmt(sp): return ', '.join(f'{a:.0f}-{b:.0f}' for a, b in sp if b - a > 1) or 'none'
    P(f'Stable circular-orbit spans (d(r^3 a)/dr>0, with a>0): A: {fmt(spans(stA & (aA>0)))}; B: {fmt(spans(stB))}. Unstable (outer well, a falls faster than 1/r^3): A: {fmt(spans((~stA)&(aA>0)))}; B: {fmt(spans(~stB))}.')
    # share of orbit band that is stable
    for ct, n, a, b, c in bands.get(t, []):
        sel = (r >= a) & (r <= c)
        if sel.sum() == 0: continue
        gA = (aA[sel] > 0).mean(); sA = (stA & (aA > 0))[sel].mean(); sB = stB[sel].mean()
        medi = int(np.searchsorted(r, b)); medi = min(medi, len(r)-1)
        P(f'- {ct} orbits of a {t} (d {a:.0f}-{c:.0f}): force on (A) at {gA:.0%} of the band, stable (A) {sA:.0%}, stable (B) {sB:.0%}; at the median d={b:.0f}: T_A/T_rail = {TA[medi]/b if np.isfinite(TA[medi]) else float("inf"):.3g}, T_B/T_rail = {TB[medi]/b:.3g}.')
    P()
    # --- implied mass (B)
    Sigma = k * lap / TP    # G=1
    Mcum = r * aB / 2.0     # 2D Gauss: a = 2 G M(<r)/r
    dr = np.gradient(r)
    Mpos = float(np.sum(np.clip(Sigma, 0, None) * TP * r * dr)); Mneg = float(np.sum(np.clip(Sigma, None, 0) * TP * r * dr))
    sgn = np.where(np.diff(np.sign(lap)) != 0)[0]
    P(f'Reading B mass: Sigma = k*(h"+h\'/r)/(2*pi) (G=1). Positive mass {Mpos:.2f}, negative (antimass) {Mneg:.2f}, net M(<rim) = {Mpos+Mneg:.3f} (should be 0 because h\'(rim)=0). Laplacian changes sign at r = {", ".join(f"{r[i]:.0f}" for i in sgn)}. Core mass (r<first sign change) fraction of the antimass: {Mpos/abs(Mneg) if Mneg else 0:.2f}.')
    P()
    summary[(t, 'p50')].update(Mpos=Mpos, Mneg=Mneg, signs=[float(r[i]) for i in sgn])

    # ---------- figures
    fig, ax = plt.subplots(figsize=(8, 5))
    ax.plot(r, np.where(np.isfinite(TA), TA, np.nan), color=colors['A'], label='T_field A (a = S*R*|h\'|^2, gated)')
    ax.plot(r, TB, color=colors['B'], label='T_field B (a = k*|h\'|)')
    ax.plot(r, r, color=colors['rail'], lw=2, label='T_rail = OrbitPeriod(r) = r')
    ax.axvline(rs, color='gray', ls=':'); ax.text(rs, 1.5, ' support', color='gray', rotation=90, va='bottom')
    unst = (~stA) & (aA > 0)
    ax.fill_between(r, 1, 1e8, where=unst, color='#c0392b', alpha=.08, label='A unstable (r^3 a falling)')
    bc = ['#f5b041', '#af7ac5', '#48c9b0', '#aab7b8']
    for j, (ct, n, a, b, c) in enumerate(bands.get(t, [])):
        ax.axvspan(a, min(c, rs * 1.0), color=bc[j % 4], alpha=.18, label=f'{ct} orbits p10-p90')
    ax.set_xscale('log'); ax.set_yscale('log'); ax.set_ylim(1, 1e6); ax.set_xlim(max(1, rs / 400), rs * 1.02)
    ax.set_xlabel('r (units from body)'); ax.set_ylabel('orbital period T (s)')
    ax.set_title(f'{t} (mass {m["mass"]:.0f}): field periods vs OrbitPeriod rail'); ax.legend(fontsize=7, loc='upper left')
    fig.tight_layout(); fig.savefig(os.path.join(HERE, f'fig_T_{t}.png'), dpi=130); plt.close(fig)

    # derived wells (anchored to h(rs)=0)
    rr_ = np.linspace(rs / 2000, rs, 3000)
    hA = (TP / math.sqrt(SR)) * 2 * (np.sqrt(rr_) - math.sqrt(rs))         # h' = 2pi/(sqrt(SR) sqrt r)
    r0 = m['body']
    hB = (TP ** 2 / k) * (np.log(rr_) - math.log(rs))
    fig, ax = plt.subplots(figsize=(8, 5))
    ax.plot(rr_, well_h(rr_, Dd, Rg), color='k', lw=2, label=f'today (PowerPulse e=16, depth {Dd:.0f})')
    ax.plot(rr_, hA, color=colors['A'], label='derived A: h = 1.147*(sqrt r - sqrt rs)')
    ax.plot(rr_[rr_ >= r0], hB[rr_ >= r0], color=colors['B'], label=f'derived B: h = (4pi^2/k) ln(r/rs), k={k:.2f}')
    ax.axvline(r0, color='gray', ls=':'); ax.text(r0, -Dd * .95, ' body radius', color='gray', rotation=90)
    ax.set_xscale('log'); ax.set_ylim(-Dd * 1.08, Dd * .06); ax.set_xlabel('r'); ax.set_ylabel('h(r)')
    ax.set_title(f'{t}: today\'s well vs the well that makes the rail exact'); ax.legend(fontsize=7)
    fig.tight_layout(); fig.savefig(os.path.join(HERE, f'fig_well_{t}.png'), dpi=130); plt.close(fig)
    summary[(t, 'p50')].update(depthA=float(-hA[0]), depthB=float(-hB[rr_ >= r0][0]), gateA=(0.5735 / 0.0316) ** 2)

    fig, axs = plt.subplots(1, 2, figsize=(11, 4.2))
    axs[0].plot(r, Sigma, color=colors['B']); axs[0].fill_between(r, 0, Sigma, where=Sigma < 0, color='#c0392b', alpha=.35, label='negative density (antimass)')
    axs[0].fill_between(r, 0, Sigma, where=Sigma > 0, color=colors['B'], alpha=.2, label='positive')
    axs[0].axhline(0, color='k', lw=.5); axs[0].set_xlabel('r'); axs[0].set_ylabel('implied surface density (G=1, k peak-matched)')
    axs[0].set_title(f'{t}: Sigma(r) = k*laplacian(h)/2pi'); axs[0].legend(fontsize=7)
    axs[1].plot(r, Mcum, color=colors['B']); axs[1].axhline(0, color='k', lw=.5); axs[1].set_xlabel('r'); axs[1].set_ylabel('enclosed mass M(<r) = r*a_B/2')
    axs[1].set_title('enclosed mass returns to 0 at the rim')
    fig.tight_layout(); fig.savefig(os.path.join(HERE, f'fig_rho_{t}.png'), dpi=130); plt.close(fig)

# ---------- summary table over p10/p50/p90 ----------
P('## Summary by body type and mass quantile (reading A and B peaks)')
P()
P('| type | quantile | mass | support r | depth | peak slope at r | peak a_A | force switches on at r | k (B) | rail hold thrust at peak (frac of 30) |')
P('|---|---|---|---|---|---|---|---|---|---|')
for t in types:
    for tag in ('p10', 'p50', 'p90'):
        d = summary[(t, tag)]
        P(f'| {t} | {tag} | {d["mass"]:.0f} | {d["rs"]:.0f} | {d["D"]:.0f} | {d["peakS"]:.4f} at {d["r_peak"]:.0f} | {d["peakA"]:.3f} | {d["r_gate_lo"]:.1f} | {d["k"]:.2f} | {abs(TP**2/d["r_peak"]-d["peakA"])/A_SHIP:.3%} |')
P()
P('Ship scale (catalog, tests/ships.csv): thrust is Thruster.Thrust/Entity.Mass per second (Thruster.cs:121). Longinus (mass 2500, TopSpeed 100) with a Small Drive (thrust 60000..90000) = 24..36 u/s^2; Djinni (mass 10000, TopSpeed 50) = 6..9. A_SHIP = 30 used above. Gravity peaks are 0.01-3 u/s^2.')
P()

# ---------- zone bowl ----------
P('## Zone bowl')
P()
P('h_bowl = -64*(1-(r/Rz)^2)^2, slope = 256*u*(1-u^2)/Rz with u = r/Rz, max 98.5/Rz at u = 0.577. Gate needs slope > 0.0316, i.e. Rz < 3117 for the bowl to push at all.')
P()
P('| pack radius Rz | peak slope | peak a_A (real code) | real code max force | force on over | centre-of-zone T_A at 0.5Rz | note |')
P('|---|---|---|---|---|---|---|')
for Rz in (1000, 2500, 5000, 10000):
    arr = cases[f'Bowl_R{Rz}']; rr = arr[:, 0]; a_real = np.hypot(arr[:, 2], arr[:, 3])
    s = bowl_dh(np.linspace(1, Rz, 20000), Rz); peak = s.max(); aa = accA(s)
    on = np.linspace(1, Rz, 20000)[aa > 0]
    ta = period(np.array([0.5 * Rz]), accA(bowl_dh(np.array([0.5 * Rz]), Rz)))[0]
    P(f'| {Rz} | {peak:.4f} | {aa.max():.4f} | {a_real.max():.4f} | {("%.0f-%.0f" % (on.min(), on.max())) if len(on) else "never"} | {ta:.4g} | {"zone radius range is 1000..10000 (ZoneRadius)" if Rz==1000 else ""} |')
P()
# combined sun + bowl
for Rz in (1000, 2000, 5000):
    m = meta[('Sun', 'p50', 0)]; Rg, Dd = m['Rg'], m['D']; rs = Rg / 2
    r = np.linspace(1, rs, 20000)
    sw = well_dh(r, Dd, Rg); sb = bowl_dh(r, Rz); st_ = sw + sb
    aW = accA(sw); aC = accA(st_)
    sel = (r > 300) & (r < 1500)
    P(f'Sun p50 (support {rs:.0f}) in a zone of radius {Rz}, bowl centred on the sun (the generator puts the root body at the origin): bowl adds {np.mean(aC[sel]-aW[sel]):+.3f} u/s^2 on average over r=300..1500 (well alone {aW[sel].mean():.3f}); T_A at r=1000: well alone {period(np.array([1000.]), accA(well_dh(np.array([1000.]),Dd,Rg)))[0]:.0f} s, with bowl {period(np.array([1000.]), accA(well_dh(np.array([1000.]),Dd,Rg)+bowl_dh(np.array([1000.]),Rz)))[0]:.0f} s (rail 1000 s).')
P()
P('Bowl as reading B mass: laplacian of -64(1-u^2)^2 = 64*(...) is positive at the centre and negative near the rim, same antimass-ring signature (see fig_bowl.png).')
P()
fig, axs = plt.subplots(1, 3, figsize=(14, 4))
for Rz, c in ((1000, '#c0392b'), (2500, '#d68910'), (5000, '#2471a3'), (10000, '#1e8449')):
    rr = np.linspace(1, Rz, 4000); s = bowl_dh(rr, Rz)
    axs[0].plot(rr / Rz, s, color=c, label=f'Rz={Rz}'); axs[1].plot(rr / Rz, accA(s), color=c); axs[2].plot(rr / Rz, bowl_lap(rr, Rz) * Rz ** 2 / BOWL_D, color=c)
axs[0].axhline(0.0316, color='k', ls='--', lw=.7); axs[0].text(.02, .034, 'force gate s=0.0316'); axs[0].set_title('bowl slope vs r/Rz'); axs[0].legend(); axs[0].set_xlabel('r/Rz')
axs[1].set_title('a_A from bowl (u/s^2)'); axs[1].set_xlabel('r/Rz')
axs[2].set_title('bowl laplacian * Rz^2 / 64'); axs[2].axhline(0, color='k', lw=.5); axs[2].set_xlabel('r/Rz')
fig.tight_layout(); fig.savefig(os.path.join(HERE, 'fig_bowl.png'), dpi=130); plt.close(fig)

# ---------- inverse ----------
P('## Inverse: the well that makes OrbitPeriod exact (T = r)')
P()
P(f'A: h\'(r) = 2*pi*sqrt(r)/(sqrt(S*R)*T) = 2*pi/(sqrt({SR:.0f})*sqrt(r)) = {TP/math.sqrt(SR):.4f}/sqrt(r); h = {2*TP/math.sqrt(SR):.4f}*sqrt(r) (cusp at the centre, slope infinite at r=0). a = S*R*h\'^2 = 4*pi^2/r exactly. Stable for any T growing slower than r^2: T=r qualifies everywhere. The force gate zeroes the field once h\' < 0.0316, i.e. beyond r = {(TP/math.sqrt(SR)/math.sqrt(GATE/(1-GATE)))**2:.0f} u, so the exact well cannot be realised past that radius with the gate in place (a rail at 1000 u would need slope {TP/math.sqrt(SR)/math.sqrt(1000):.4f}).')
P()
P('| type (p50) | rim (support) | today\'s depth | derived A depth (rim to centre) | derived B depth (rim to body radius) | k |')
P('|---|---|---|---|---|---|')
for t in types:
    d = summary[(t, 'p50')]; mm = meta[(t, 'p50', 0)]
    P(f'| {t} | {d["rs"]:.0f} | {d["D"]:.1f} | {d["depthA"]:.1f} | {d["depthB"]:.1f} | {d["k"]:.2f} |')
P()
P('B: h\'(r) = 4*pi^2*r/(k*T^2) = 4*pi^2/(k*r), h = (4*pi^2/k) ln r, laplacian = 0 everywhere except r=0: a single point mass (2D Newton: a = 2*G*M/r, M = r*a/2 = 2*pi^2 in G=1 units, independent of r).')
P()

# ---------- point mass / disc in B (Planet p50) ----------
t = 'Planet'; m = meta[(t, 'p50', 0)]; Rg, Dd = m['Rg'], m['D']; rs = Rg / 2; k = summary[(t, 'p50')]['k']
rc = [b for ct, n, a, b, c in bands['Sun'] if ct == 'Planet'][0]
r = np.linspace(2, 3 * rs, 4000)
Ttar = rc                               # disc period set to the rail at the median planet-around-sun distance
Om = TP / Ttar
a_disc = np.where(r < rs, Om ** 2 * r, Om ** 2 * rs ** 2 / r)
T_disc = TP * np.sqrt(r / a_disc)
s = well_dh(r, Dd, Rg); T_today_B = period(r, k * s); T_today_A = period(r, accA(s))
fig, ax = plt.subplots(figsize=(8, 5))
ax.plot(r, T_today_A, color=colors['A'], label='today, reading A'); ax.plot(r, T_today_B, color=colors['B'], label='today, reading B')
ax.plot(r, r, color=colors['rail'], lw=2, label='rail T=r = 2D point mass in B')
ax.plot(r, T_disc, color=colors['disc'], label=f'uniform disc radius {rs:.0f}, T_core={Ttar:.0f}s, point mass outside')
ax.set_xscale('log'); ax.set_yscale('log'); ax.set_ylim(1, 1e5); ax.set_xlabel('r'); ax.set_ylabel('T (s)'); ax.legend(fontsize=7)
ax.set_title(f'{t} p50, reading B: point mass vs uniform disc vs today'); fig.tight_layout(); fig.savefig(os.path.join(HERE, 'fig_point_disc.png'), dpi=130); plt.close(fig)
hpt = (TP ** 2 / k) * np.log(r / rs)
hdisc = np.where(r < rs, (Om ** 2 / k) * (r ** 2 - rs ** 2) / 2, (Om ** 2 * rs ** 2 / k) * np.log(r / rs))
P('## Reading B comparisons: point mass, uniform disc')
P()
P(f'2D gravity (Gauss law in the plane): a(r) = 2*G*M(<r)/r. Point mass: a = c/r, v = const = sqrt(c), T = 2*pi*r/v ~ r (flat rotation curve). OrbitPeriod is Exponent=1, Multiplier=1, i.e. T=d^1: exactly the 2D point mass with c = 4*pi^2 (v = 2*pi u/s). Generalising, OrbitPeriod exponent E gives a ~ r^(1-2E): E=1 is the 2D point mass (1/r), E=1.5 would be 3D Kepler (1/r^2), E=0 is a rigid-rotation disc interior (a ~ r, T const).')
P()
P(f'Uniform disc of radius {rs:.0f} (the {t} p50 support) in B, with core period fixed to the rail at the median planet-around-sun distance ({rc:.0f} u): a = Omega^2*r inside (Omega = 2*pi/{Ttar:.0f}), T constant = {Ttar:.0f} s inside; point-mass tail outside, T = {TP/ Om * 1:.0f}*(r/{rs:.0f}) s beyond r={rs:.0f}. Height: parabola h = (Omega^2/(2k))(r^2 - rs^2) inside, log outside; the potential never returns to flat (log), unlike today\'s wells which are exactly flat beyond the rim (zero net mass). A point mass has an unbounded log well; today\'s well is bounded and mass-neutral.')
P()
P(f'Today\'s p50 {t} core (r << rim): h ≈ -D + D*e*(2/Rg)^2*r^2 -> h" = {2*Dd*EXP*4/Rg**2:.4g}, h\'/r = h"; so near the centre laplacian = 2h" = {4*Dd*EXP*4/Rg**2:.4g} > 0: positive density, rigid-rotation core in B (T = 2*pi/sqrt(k*h") = {TP/math.sqrt(k*2*Dd*EXP*4/Rg**2):.0f} s), not a point mass.')
P()
open(os.path.join(HERE, 'report_numbers.md'), 'w', encoding='utf-8').write('\n'.join(md))
json.dump({f'{a}_{b}': {kk: (vv if not isinstance(vv, list) else vv) for kk, vv in v.items()} for (a, b), v in summary.items()}, open(os.path.join(HERE, 'summary.json'), 'w'), indent=1)
print('ok', len(md))
