"""Curl-flow probe (Imagination, aetheria-fauna, 2026-10-14). Python 3 + numpy + Pillow.

Renders candidate replacements for FlowField.Global (today's Tri3D) on the habitat probe's zone and scale.
Reuses ../2026-10-14-fauna-habitat-probe/habitat_probe.py for the zone, the wells, Zone.GetNormal and the
FlowField law transliteration (flow_global, scroll, FLOW settings: scale 512, amplitude 15, speed .025).

New here, and only here:
  - snoise_grad: CultMath math.snoise_grad (packages/cultmath/src/CultMath/math.cs:714) transliterated term for
    term to numpy float64, including the tie order of the step() lines. Float64, not the float32 law.
  - fbm_grad: CultMath math.fbm_grad, same octave compounding.
  - curl candidates: 2D curl of a scalar stream function psi(x/S, z/S, tau), v = k (dpsi/dz, -dpsi/dx),
    with tau = speed * t folded modulo 867 (snoise(float3) is exactly periodic by 867 along each axis; see
    period_check). Velocity has no y component.

Usage: py -3 -u curl_probe.py [N]   (N = LIC grid, default 512). Writes PNGs and prints numbers.
"""
import math, os, sys, time
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "2026-10-14-fauna-habitat-probe"))
_argv = sys.argv; sys.argv = sys.argv[:1]
import habitat_probe as H   # zone, wells, normal(), flow_global(), scroll(), FLOW
sys.argv = _argv

N = int(sys.argv[1]) if len(sys.argv) > 1 else 512
R, S = H.R, H.FLOW["scale"]          # zone half-width 2000 u, flow scale 512 u
SPEED = H.FLOW["speed"]              # .025 field units per second of zone time
PERIOD = 867.0                       # snoise(float3) period along each axis, in noise units
STARDUST_PERIOD = 2.0                # Stardust Period in ARPG.unity / Main Menu.unity
SWIRL, SLIDE = 6.0, 1.5              # B25g slope terms (flow-slope-terms ruling)
rng = np.random.default_rng(7)

# ---------------- CultMath snoise_grad, float64 transliteration ----------------
def _mod289(v): return v - np.floor(v * (1.0 / 289.0)) * 289.0
def _permute(v): return _mod289(((v * 34.0) + 10.0) * v)
def _step(edge, value): return np.where(value < edge, 0.0, 1.0)

def snoise_grad(P):
    """P: (...,3). Returns (...,4): gradient xyz, value w."""
    cx, cy = 1.0 / 6.0, 1.0 / 3.0
    i = np.floor(P + (P.sum(-1, keepdims=True) * cy))
    x0 = P - i + (i.sum(-1, keepdims=True) * cx)
    g = np.stack([_step(x0[..., 1], x0[..., 0]), _step(x0[..., 2], x0[..., 1]), 1.0 - _step(x0[..., 2], x0[..., 0])], -1)
    l = 1.0 - g
    lzxy = l[..., [2, 0, 1]]
    i1 = np.minimum(g, lzxy); i2 = np.maximum(g, lzxy)
    x1 = x0 - i1 + cx; x2 = x0 - i2 + cy; x3 = x0 - 0.5
    i = _mod289(i)
    z4 = lambda k: np.stack([np.zeros_like(i[..., k]), i1[..., k], i2[..., k], np.ones_like(i[..., k])], -1)
    p = _permute(_permute(_permute(i[..., 2:3] + z4(2)) + i[..., 1:2] + z4(1)) + i[..., 0:1] + z4(0))
    n = 0.142857142857
    nsx, nsy, nsz = 2.0 * n, 0.5 * n - 1.0, n
    j = p - 49.0 * np.floor(p * nsz * nsz)
    xs = np.floor(j * nsz); ys = np.floor(j - 7.0 * xs)
    x = xs * nsx + nsy; y = ys * nsx + nsy
    h = 1.0 - np.abs(x) - np.abs(y)
    b0 = np.stack([x[..., 0], x[..., 1], y[..., 0], y[..., 1]], -1)
    b1 = np.stack([x[..., 2], x[..., 3], y[..., 2], y[..., 3]], -1)
    s0 = np.floor(b0) * 2.0 + 1.0; s1 = np.floor(b1) * 2.0 + 1.0
    sh = -_step(h, 0.0)
    a0 = b0[..., [0, 2, 1, 3]] + s0[..., [0, 2, 1, 3]] * sh[..., [0, 0, 1, 1]]
    a1 = b1[..., [0, 2, 1, 3]] + s1[..., [0, 2, 1, 3]] * sh[..., [2, 2, 3, 3]]
    ps = [np.stack([a0[..., 0], a0[..., 1], h[..., 0]], -1), np.stack([a0[..., 2], a0[..., 3], h[..., 1]], -1),
          np.stack([a1[..., 0], a1[..., 1], h[..., 2]], -1), np.stack([a1[..., 2], a1[..., 3], h[..., 3]], -1)]
    xs_ = [x0, x1, x2, x3]
    value = 0.0; grad = 0.0
    for pk, xk in zip(ps, xs_):
        pk = pk * (1.79284291400159 - 0.85373472095314 * (pk * pk).sum(-1, keepdims=True))
        m0 = np.maximum(0.5 - (xk * xk).sum(-1, keepdims=True), 0.0)
        m3 = m0 ** 3; m4 = m0 ** 4
        px = (pk * xk).sum(-1, keepdims=True)
        value = value + m4 * px
        grad = grad + (-8.0 * m3 * xk * px + m4 * pk)
    return np.concatenate([grad * 105.0, value * 105.0], -1)

def fbm_grad(P, octaves, lacunarity, gain):
    a, f, out = 1.0, 1.0, 0.0
    for _ in range(octaves):
        nn = snoise_grad(P * f)
        out = out + np.concatenate([a * f * nn[..., :3], a * nn[..., 3:]], -1)
        f *= lacunarity; a *= gain
    return out

# ---------------- time fold ----------------
def tau_of(t):
    """speed * t folded modulo the noise period in double, as FlowField.Scroll folds frac in double."""
    return math.fmod(SPEED * t, PERIOD)

# ---------------- candidates: world xz (arrays) -> velocity xz ----------------
def tri3d(x, z, t):
    sc = H.scroll(t); G = H.GOLD
    fx, fz = x / S, z / S; fy = np.zeros_like(fx)
    def tri(v): return np.abs(v - np.floor(v) - .5)
    def tri3(a, b, c): return tri(c + tri(b)), tri(c + tri(a)), tri(b + tri(a))
    def nrm(a, b, c): l = np.sqrt(a * a + b * b + c * c); return a / l, b / l, c / l
    a = nrm(*tri3(fx, fy - sc[0], fz)); b = nrm(*tri3(fx * G, fy * G - sc[1], fz * G))
    cx = a[1] * b[2] - a[2] * b[1]; cz = a[0] * b[1] - a[1] * b[0]
    A = H.FLOW["amp"]; return cx * A, cz * A

def stream_grad(kind, x, z, t):
    """Gradient of the stream function psi wrt field coords (x/S, z/S). Returns (gx, gz)."""
    tau = tau_of(t)
    P = np.stack([x / S, z / S, np.full_like(x, tau)], -1)
    if kind == "curl1":
        g = snoise_grad(P); return g[..., 0], g[..., 1]
    if kind == "curlfbm":
        g = fbm_grad(P, 3, 2.0, 0.4); return g[..., 0], g[..., 1]
    if kind == "warp":
        W, fw = 0.55, 0.6                                           # warp amplitude (field units), warp frequency
        o1 = np.array([17.3, 41.9, 0.0]); o2 = np.array([-63.1, 5.7, 0.0])
        Pw = np.stack([x / S * fw, z / S * fw, np.full_like(x, tau * fw)], -1)
        n1 = snoise_grad(Pw + o1); n2 = snoise_grad(Pw + o2)
        qx = x / S + W * n1[..., 3]; qz = z / S + W * n2[..., 3]
        g = fbm_grad(np.stack([qx, qz, np.full_like(x, tau)], -1), 2, 2.0, 0.4)
        # exact chain rule: grad_p psi = J^T grad_q psi, J = I + W fw [[dn1/dx, dn1/dz], [dn2/dx, dn2/dz]]
        J11 = 1 + W * fw * n1[..., 0]; J12 = W * fw * n1[..., 1]
        J21 = W * fw * n2[..., 0]; J22 = 1 + W * fw * n2[..., 1]
        return g[..., 0] * J11 + g[..., 1] * J21, g[..., 0] * J12 + g[..., 1] * J22
    raise KeyError(kind)

GAIN = {}   # per-candidate k, set so RMS speed matches tri3d's RMS over the zone at t = 0
def slope_terms(x, z, t):
    """FlowField.Slope with swirl 6, slide 1.5, downhill from H.normal (pointwise, slow)."""
    ux = np.zeros_like(x); uz = np.zeros_like(x)
    fx, fz = x.ravel(), z.ravel(); ox, oz = ux.ravel(), uz.ravel()
    for k in range(fx.size):
        n = H.normal(float(fx[k]), float(fz[k]), t); dx, dz = n[0], n[2]; l = math.hypot(dx, dz)
        if l > 0:
            dx, dz = dx / l, dz / l
            ox[k] = dz * SWIRL + dx * SLIDE; oz[k] = -dx * SWIRL + dz * SLIDE
    return ox.reshape(x.shape), oz.reshape(x.shape)

def velocity(kind, x, z, t, slope=None):
    if kind == "tri3d": return tri3d(x, z, t)
    base = kind.replace("+slope", "")
    gx, gz = stream_grad(base, x, z, t)
    k = GAIN[base]
    ux, uz = k * gz, -k * gx                     # curl of psi (k carries 1/S and the amplitude)
    if kind.endswith("+slope"):
        sx, sz = slope if slope is not None else slope_terms(x, z, t)
        ux, uz = ux + sx, uz + sz
    return ux, uz

CANDS = ["tri3d", "curl1", "curlfbm", "warp", "curlfbm+slope", "warp+slope"]
NAMES = {"tri3d": "0-tri3d", "curl1": "1-curl-simplex", "curlfbm": "2-curl-fbm3",
         "warp": "3-curl-warped", "curlfbm+slope": "4-curl-fbm3-slope", "warp+slope": "4b-curl-warped-slope"}
COST = {"tri3d": "0 snoise_grad (2 normalize(tri3) + cross)", "curl1": "1 snoise_grad",
        "curlfbm": "3 snoise_grad (fbm_grad, 3 octaves)", "warp": "4 snoise_grad (2 warp + fbm_grad 2 octaves)",
        "curlfbm+slope": "3 snoise_grad + the slope term (gravity gradient: 1 texel gradient in the shader)",
        "warp+slope": "4 snoise_grad + the slope term"}

# ---------------- images ----------------
STOPS = np.array([[0, 10, 14, 30], [.45, 20, 120, 130], [.8, 200, 210, 120], [1, 255, 245, 210]], float)
def ramp(v):
    v = np.clip(v, 0, 1); out = np.zeros(v.shape + (3,))
    for c in range(3): out[..., c] = np.interp(v, STOPS[:, 0], STOPS[:, c + 1])
    return out

def wells_px(w, x0, z0, span):
    for wl in H.WELLS:
        i = (wl["pos"][0] - x0) / span * w; j = (wl["pos"][1] - z0) / span * w
        if 0 <= i < w and 0 <= j < w: yield i, w - 1 - j

def lic(ux, uz, L=28):
    n = ux.shape[0]; noise = rng.random((n, n))
    sp = np.hypot(ux, uz) + 1e-12; dx, dz = ux / sp, uz / sp
    acc = noise.copy(); cnt = np.ones_like(noise)
    jj, ii = np.mgrid[0:n, 0:n].astype(float)
    for sgn in (1, -1):
        px, pz = ii.copy(), jj.copy()
        for s in range(L):
            a = np.clip(np.rint(px).astype(int), 0, n - 1); b = np.clip(np.rint(pz).astype(int), 0, n - 1)
            px = px + sgn * dx[b, a] * .7; pz = pz + sgn * dz[b, a] * .7
            a = np.clip(np.rint(px).astype(int), 0, n - 1); b = np.clip(np.rint(pz).astype(int), 0, n - 1)
            wgt = math.cos(math.pi * .5 * (s + 1) / (L + 1)) ** 2
            acc += noise[b, a] * wgt; cnt += wgt
    return acc / cnt

def save_lic(name, ux, uz, vref):
    t = lic(ux, uz); t = (t - t.mean()) / (t.std() + 1e-9) * .22 + .5
    sp = np.hypot(ux, uz) / vref
    rgb = ramp(.25 + .6 * np.clip(sp, 0, 1.6) / 1.6) * np.clip(t, 0, 1.2)[..., None] * 1.15
    img = Image.fromarray(np.clip(rgb[::-1], 0, 255).astype(np.uint8)); d = ImageDraw.Draw(img)
    for i, j in wells_px(N, -R, -R, 2 * R): d.ellipse([i - 4, j - 4, i + 4, j + 4], outline=(255, 70, 70), width=2)
    path = os.path.join(HERE, f"lic_{name}.png"); img.save(path); return path

def save_drift(kind, name, t0, slope_fn):
    """Left: the Stardust law for one cycle (position -= flow(spawn) * lifetime * Period, Stardust.compute:142),
    each particle's whole-cycle streak. Right: the same seeds advected along the field (RK2, 40 s), a long
    exposure to read the field's texture; not what Stardust draws. Window 1200 u, centred off the sun."""
    W = 640; span = 1200.0; x0, z0 = -1700.0, -600.0
    m = 2600; sx = x0 + rng.random(m) * span; sz = z0 + rng.random(m) * span
    panels = []
    for mode in ("cycle", "exposure"):
        img = Image.new("RGB", (W, W), (10, 14, 30)); d = ImageDraw.Draw(img)
        if mode == "cycle":
            ux, uz = velocity(kind, sx, sz, t0, slope=slope_fn(sx, sz) if kind.endswith("+slope") else None)
            ex, ez = sx - ux * STARDUST_PERIOD, sz - uz * STARDUST_PERIOD
            paths = [np.stack([sx, sz], -1), np.stack([ex, ez], -1)]
        else:
            px, pz = sx.copy(), sz.copy(); paths = [np.stack([px, pz], -1)]; dt = 1.0
            for s in range(40):
                sl = slope_fn(px, pz) if kind.endswith("+slope") else None
                ax, az = velocity(kind, px, pz, t0 + s * dt, slope=sl)
                mx, mz = px + ax * dt * .5, pz + az * dt * .5
                sl = slope_fn(mx, mz) if kind.endswith("+slope") else None
                bx, bz = velocity(kind, mx, mz, t0 + (s + .5) * dt, slope=sl)
                px, pz = px + bx * dt, pz + bz * dt; paths.append(np.stack([px, pz], -1))
        P = np.stack(paths, 0); P[..., 0] = (P[..., 0] - x0) / span * W; P[..., 1] = W - 1 - (P[..., 1] - z0) / span * W
        for k in range(m):
            pts = [tuple(p) for p in P[:, k]]
            if mode == "cycle":
                d.line(pts, fill=(120, 190, 190), width=1); d.point(pts[-1], fill=(255, 245, 210))
            else:
                d.line(pts, fill=(70, 150, 160), width=1); d.point(pts[-1], fill=(255, 245, 210))
        for i, j in wells_px(W, x0, z0, span): d.ellipse([i - 5, j - 5, i + 5, j + 5], outline=(255, 70, 70), width=2)
        panels.append(img)
    out = Image.new("RGB", (2 * W + 8, W), (40, 40, 40)); out.paste(panels[0], (0, 0)); out.paste(panels[1], (W + 8, 0))
    path = os.path.join(HERE, f"drift_{name}.png"); out.save(path); return path

# ---------------- numbers ----------------
def numbers(kind, t, slope_fn):
    m = 20000; x = (rng.random(m) * 2 - 1) * R * .95; z = (rng.random(m) * 2 - 1) * R * .95; e = 1.0
    sl = (lambda a, b: slope_fn(a, b)) if kind.endswith("+slope") else (lambda a, b: None)
    ux, uz = velocity(kind, x, z, t, slope=sl(x, z))
    axp, _ = velocity(kind, x + e, z, t, slope=sl(x + e, z)); axm, _ = velocity(kind, x - e, z, t, slope=sl(x - e, z))
    _, azp = velocity(kind, x, z + e, t, slope=sl(x, z + e)); _, azm = velocity(kind, x, z - e, t, slope=sl(x, z - e))
    bzp, _ = velocity(kind, x, z + e, t, slope=sl(x, z + e)); bzm, _ = velocity(kind, x, z - e, t, slope=sl(x, z - e))
    _, bxp = velocity(kind, x + e, z, t, slope=sl(x + e, z)); _, bxm = velocity(kind, x - e, z, t, slope=sl(x - e, z))
    div = (axp - axm) / (2 * e) + (azp - azm) / (2 * e)
    vort = (bxp - bxm) / (2 * e) - (bzp - bzm) / (2 * e)
    sp = np.hypot(ux, uz)
    return dict(rms_div=float(np.sqrt((div ** 2).mean())), rms_vort=float(np.sqrt((vort ** 2).mean())),
                div_over_vort=float(np.sqrt((div ** 2).mean()) / np.sqrt((vort ** 2).mean())),
                speed=[float(np.percentile(sp, q)) for q in (10, 50, 90, 99)] + [float(sp.max())],
                rms=float(np.sqrt((sp ** 2).mean())), still_frac=float((sp < .1 * np.sqrt((sp ** 2).mean())).mean()))

def period_check():
    m = 4000; P = rng.random((m, 3)) * 50
    out = {}
    for ax in range(3):
        Q = P.copy(); Q[:, ax] += PERIOD
        out["axis%d" % ax] = float(np.abs(snoise_grad(P) - snoise_grad(Q)).max())
    Q = P.copy(); Q[:, 2] += PERIOD
    out["fbm3_z"] = float(np.abs(fbm_grad(P, 3, 2.0, .4) - fbm_grad(Q, 3, 2.0, .4)).max())
    # float32 precision of an unfolded tau (as a field coordinate) at long zone times
    for days in (1, 10, 100):
        tau = SPEED * days * 86400.0
        out[f"ulp32_unfolded_{days}d"] = float(np.spacing(np.float32(tau)))
        out[f"ulp32_folded_{days}d"] = float(np.spacing(np.float32(math.fmod(tau, PERIOD))))
    return out

def grad_check():
    m = 2000; P = rng.random((m, 3)) * 20; e = 1e-5; g = snoise_grad(P)
    err = 0.0
    for ax in range(3):
        d = np.zeros(3); d[ax] = e
        fd = (snoise_grad(P + d)[:, 3] - snoise_grad(P - d)[:, 3]) / (2 * e)
        err = max(err, float(np.abs(fd - g[:, ax]).max()))
    return err

def main():
    t0 = 0.0; T = time.time()
    print("snoise_grad analytic vs FD max err", grad_check())
    print("period / precision", period_check())
    # tri3d numpy vs habitat_probe pointwise
    xs = (rng.random(50) * 2 - 1) * R; zs = (rng.random(50) * 2 - 1) * R
    a = tri3d(xs, zs, 123.4); sc = H.scroll(123.4)
    b = np.array([[H.flow_global(float(x), 0.0, float(z), sc)[k] for k in (0, 2)] for x, z in zip(xs, zs)])
    print("tri3d numpy vs habitat_probe max err", float(np.abs(np.stack(a, -1) - b).max()))
    c = (np.arange(N) + .5) / N * 2 * R - R; X, Z = np.meshgrid(c, c)
    tx, tz = tri3d(X, Z, t0); vref = float(np.sqrt((tx ** 2 + tz ** 2).mean()))
    for kind in ("curl1", "curlfbm", "warp"):
        gx, gz = stream_grad(kind, X, Z, t0); GAIN[kind] = vref / float(np.sqrt((gx ** 2 + gz ** 2).mean()))
    print("tri3d RMS speed", vref, "gains k", GAIN)
    print("slope grid ...", flush=True)
    SX, SZ = slope_terms(X, Z, t0)
    # slope sampler for scattered points: nearest cell of the grid (the shader samples a gravity texture too)
    def slope_fn(x, z):
        i = np.clip(((x + R) / (2 * R) * N).astype(int), 0, N - 1); j = np.clip(((z + R) / (2 * R) * N).astype(int), 0, N - 1)
        return SX[j, i], SZ[j, i]
    for kind in CANDS:
        name = NAMES[kind]
        ux, uz = velocity(kind, X, Z, t0, slope=(SX, SZ) if kind.endswith("+slope") else None)
        p1 = save_lic(name, ux, uz, vref); p2 = save_drift(kind, name, t0, slope_fn)
        nums = numbers(kind, t0, slope_fn)
        print(f"== {name}: cost {COST[kind]}\n   {nums}\n   {p1}\n   {p2}", flush=True)
    # scroll: how far the field decorrelates over one minute of zone time, per candidate
    for kind in CANDS[:4]:
        u0 = np.stack(velocity(kind, X[::8, ::8], Z[::8, ::8], 0.0), -1).reshape(-1, 2)
        for dt in (10.0, 40.0, 120.0):
            u1 = np.stack(velocity(kind, X[::8, ::8], Z[::8, ::8], dt), -1).reshape(-1, 2)
            cc = float((u0 * u1).sum() / np.sqrt((u0 ** 2).sum() * (u1 ** 2).sum()))
            print(f"   scroll {NAMES[kind]} corr(t=0, t={dt:.0f}s) = {cc:.3f}")
    # contact sheet
    ims = [Image.open(os.path.join(HERE, f"lic_{NAMES[k]}.png")).resize((384, 384)) for k in CANDS]
    sheet = Image.new("RGB", (392 * len(CANDS) - 8, 384), (40, 40, 40))
    for k, im in enumerate(ims): sheet.paste(im, (k * 392, 0))
    sheet.save(os.path.join(HERE, "lic_contact_sheet.png"))
    print("done", round(time.time() - T, 1), "s")

if __name__ == "__main__": main()
