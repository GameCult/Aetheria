"""Is density_c6's triangular lattice the FlowField law's own structure or a probe artifact?
Transect along x at z = 300 (y = 0, t = 100, slope terms off: the global term alone), 0.5 u spacing over 2048 u.
1. Kinks of the law: tri(v) = |frac(v) - .5| is piecewise linear, so Tri3D's derivatives jump where an argument of
   a tri crosses a multiple of 1/2. Predict those x from the law's own arguments (both octaves) and check where the
   largest convergence jumps sit.
2. Finite-difference step: convergence with step 6 (probe and spec) vs step .5: correlation.
3. float32: the flow evaluated with every input and the tri arguments rounded to float32 vs float64: max difference.
"""
import math, struct, sys
sys.argv = sys.argv[:1]
import habitat_probe as H

H.FLOW.update(swirl=0.0, slope=0.0)
T = 100.0; sc = H.scroll(T); Z = 300.0; S = H.FLOW["scale"]; GOLD = H.GOLD


def conv(x, z, e):
    a = H.flow_xz(x + e, z, T, sc); b = H.flow_xz(x - e, z, T, sc)
    c = H.flow_xz(x, z + e, T, sc); d = H.flow_xz(x, z - e, T, sc)
    return -((a[0] - b[0]) / (2 * e) + (c[1] - d[1]) / (2 * e))


xs = [-1024 + .5 * k for k in range(4097)]
c6 = [conv(x, Z, 6.0) for x in xs]; c05 = [conv(x, Z, .5) for x in xs]
m6, m5 = sum(c6) / len(c6), sum(c05) / len(c05)
r = sum((a - m6) * (b - m5) for a, b in zip(c6, c05)) / math.sqrt(sum((a - m6) ** 2 for a in c6) * sum((b - m5) ** 2 for b in c05))
print(f"FD step 6 vs .5 along the transect: correlation {r:.3f}")

# predicted kink x: arguments of tri along the transect (y field = -scroll, z fixed), both octaves
def kinks():
    out = []
    for oc, sh in ((1.0, sc[0]), (GOLD, sc[1])):
        py, pz = -sh, Z / S * oc
        ty, tz = H.tri(py), H.tri(pz)
        # tri(p.x) kinks: p.x = k/2; tri(p.z + tri(p.x)) and tri(p.y + tri(p.x)) kink where tri(p.x) + const = k/2
        for k in range(-40, 41):
            out.append(k / 2 * S / oc)
        for base in (pz, py):
            for k in range(-40, 41):
                tv = k / 2 - base   # tri(p.x) must equal tv in [0, .5]
                if 0 <= tv <= .5:
                    for m in range(-20, 21):
                        for px in (m + .5 - tv, m + .5 + tv):
                            out.append(px * S / oc)
    return sorted(set(round(v, 3) for v in out if -1030 < v < 1030))


K = kinks()
jumps = sorted(((abs(c05[k + 1] - c05[k]), xs[k]) for k in range(len(xs) - 1)), reverse=True)[:40]
near = sum(1 for _, x in jumps if min(abs(x - k) for k in K) <= 1.0)
print(f"law kink lines crossing the 2048 u transect: {len(K)}; of the 40 largest convergence jumps (step .5), {near} lie within 1 u of a predicted kink")
spacing = sorted(K); gaps = [b - a for a, b in zip(spacing, spacing[1:]) if b - a > .5]
print(f"median spacing of kink lines along x: {sorted(gaps)[len(gaps) // 2]:.1f} u (flow scale {S:.0f} u)")


def f32(v): return struct.unpack("f", struct.pack("f", v))[0]


md = 0.0
for x in xs[::8]:
    a = H.flow_global(x, 0.0, Z, sc)
    b = H.flow_global(f32(x), 0.0, f32(Z), (f32(sc[0]), f32(sc[1])))
    md = max(md, abs(a[0] - b[0]), abs(a[2] - b[2]))
print(f"flow with float32-rounded inputs vs float64: max |difference| {md:.2e} u/s (amplitude 15)")
