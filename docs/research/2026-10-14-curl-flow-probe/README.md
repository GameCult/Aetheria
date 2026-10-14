# Curl flow probe (2026-10-14)

Operator: "can we use something prettier for the flow noise?" This probe renders candidates for
`FlowField.Global` (today a Tri3D triangle noise, `Assets/Scripts/ServerShared/FlowField.cs` on master, lowered
in `Assets/Shaders/Volumetric.cginc` `globalFlow`) on the habitat probe's zone and scale. It is evidence for a
question, not a design.

`curl_probe.py` imports `../2026-10-14-fauna-habitat-probe/habitat_probe.py`: the zone (4000 u, the same six
bodies), `Zone.GetNormal`, and the FlowField transliteration (scale 512 u, amplitude 15, scroll speed .025). The
probe's numpy Tri3D matches `habitat_probe.flow_global` to 0.0 over 50 points. The new code is CultMath
`math.snoise_grad` (`packages/cultmath/src/CultMath/math.cs:714`), transliterated term for term to numpy
float64 with the same `step` tie order, and `math.fbm_grad`. The analytic gradient matches central differences
to 2.2e-8. The noise is float64, not the float32 law.

## What the curl candidates are

The flow is 2D: a stream function `psi(x/S, z/S, tau)`, velocity `k * (dpsi/dz, -dpsi/dx)`, no y component.
Today's flow has a halved y component; dropping it is part of the change. The curl of a scalar stream function
is divergence-free by construction (Bridson, Hourihan and Nordenstam, "Curl-noise for procedural fluid flow",
SIGGRAPH 2007, cited from memory). Each `k` is set so the RMS speed over the zone equals Tri3D's (6.60 u/s), so
the speeds below compare shape at equal energy.

| # | Name | Stream function |
|---|---|---|
| 0 | `0-tri3d` | Control: today's `FlowField.Global`, at y = 0. |
| 1 | `1-curl-simplex` | `snoise_grad(x/S, z/S, tau)`, one octave. k = 1.87. |
| 2 | `2-curl-fbm3` | `fbm_grad(..., 3 octaves, lacunarity 2, gain .4)`. The gradient weights octave i by `(2 * .4)^i`, so velocity carries 1, .8 and .64 per octave. k = 1.13. |
| 3 | `3-curl-warped` | `fbm_grad` with 2 octaves at a warped position `q = p + .55 * (n1, n2)`. n1 and n2 are two offset `snoise_grad` at frequency .6. The chain rule through the warp's Jacobian is exact, so the flow stays divergence-free. k = .76. |
| 4 | `4-curl-fbm3-slope` | #2 plus `FlowField.Slope` with swirl 6 and slide 1.5 (B25g), as the flow-slope-terms ruling turns it on. |
| 4b | `4b-curl-warped-slope` | #3 plus the same slope terms. |
| 5 | `5-lab-stretch-fixed` | The Nebula Span Lab's construction as a stream function (see below and `labs-noise-facts.md`): `fbm_grad` (3 octaves, lacunarity 2, gain .5) of the stretched argument `Mop(v) = v + (1/5 - 1) dot(v, d) d`, with d fixed at azimuth 25 (the lab's "Streaming filaments" preset). The flow is exact analytically, since Mop is symmetric. k = 1.09. |
| 5b | `5b-lab-stretch-contour` | #5 with d set to the local contour tangent (the swirl direction around wells, bilinear on the gravity grid). The analytic chain rule would need dd/dp, which is the height Hessian, so psi is differenced instead (4 evaluations). k = 1.25. |
| 5c | `5c-lab-stretch-contour-slope` | #5b plus the slope terms. |

Time is the noise's third axis: `tau = fmod(.025 * zoneTime, 867)`, folded in double as `FlowField.Scroll`
folds `frac`. This works because `snoise(float3)` has a period of exactly 867 along each axis: a shift of 867
along any axis moves the skewed lattice by `289 * (1, 1, 4)`, and every hash is taken mod 289. Measured in
float64 over 4,000 points, the shift changes the value by at most 1.2e-11 on each axis, and 2.9e-11 for the
3-octave fbm. The fold therefore adds no seam. It holds for fbm only with an integer lacunarity.

## Numbers

Each statistic is taken over 20,000 random points in 95 % of the zone, at t = 0. Divergence and vorticity are
central differences with a 1 u step. "Still" is the fraction of points with speed below .1 RMS.

| # | div RMS | vort RMS | div / vort | speed p10 / p50 / p90 / p99 / max (u/s) | still | snoise_grad per eval |
|---|---|---|---|---|---|---|
| 0 | .207 | .153 | **1.35** | 1.7 / 5.9 / 9.6 / 11.6 / 12.2 | 3.2 % | 0 (2 `normalize(tri3)` + `cross`) |
| 1 | 1.4e-6 | .100 | 1.4e-5 | 1.5 / 3.9 / 8.4 / 23.3 / 45.7 | 2.3 % | 1 |
| 2 | 4.5e-5 | .267 | 1.7e-4 | 1.5 / 4.1 / 10.5 / 19.3 / 32.3 | 1.9 % | 3 |
| 3 | 9.1e-4 | .585 | 1.5e-3 | 0.8 / 2.7 / 8.9 / 25.3 / **125.7** | 8.2 % | 4 |
| 4 | .175 | .334 | .52 | 3.7 / 7.3 / 12.9 / 21.4 / 34.7 | 0.4 % | 3 + slope term |
| 4b | .152 | .634 | .24 | 4.2 / 6.7 / 11.2 / 26.5 / 107.7 | 0.4 % | 4 + slope term |
| 5 | 5.6e-5 | .273 | 2.0e-4 | 0.9 / 3.2 / 10.7 / 22.9 / 36.1 | 6.4 % | 3 |
| 5b | 1.98 | 3.33 | .59 | 0.6 / 1.9 / 6.1 / 22.8 / **174.8** | 10.4 % | 12 as probed; 3 plus a height Hessian analytically |
| 5c | 2.02 | 3.35 | .60 | 4.3 / 6.4 / 9.6 / 24.0 / 212.0 | 0.2 % | as 5b plus the slope term |

The curl candidates' divergence is finite-difference error. Tri3D's is real: today's flow is more compressive
than rotational (1.35). That compression is why its long-exposure drift collapses into kinked convergence lines.

Cost is counted in calls; the probe measured no GPU timings. One `snoise_grad` is a few times the ALU of Tri3D.
The `+slope` rows add one gravity-texture gradient in the shader, which `FLOW_SLOPE` already pays today.

### Scroll

The table gives the correlation between the velocity field at t = 0 and at t, on a 64 x 64 grid.

| # | 10 s | 40 s | 120 s |
|---|---|---|---|
| 0 | .32 | .45 | .80 |
| 1 | .57 | -.09 | .09 |
| 2 | .29 | -.04 | .12 |
| 3 | .10 | -.01 | .01 |
| 5 | .22 | -.03 | .15 |
| 5b | .31 | -.01 | -.03 |

Tri3D is quasi-periodic: octave periods of 40 s and 24.7 s make the field recur, with a correlation of .8 at
120 s. The curl fields evolve without recurring for `867 / .025 = 34,680 s` (9.6 h). The warped field turns over
fastest, within 10 s. Its warp also rides tau, so it boils; giving the warp a slower tau rate is the knob.

Float32 precision of tau, as a noise coordinate: folded, the ULP is at most 6.1e-5 noise units (0.03 u at
S = 512) at any zone age. Unfolded, it is 2.4e-4 at 1 day, 2.0e-3 at 10 days and 1.6e-2 at 100 days (8 u
steps). The fold is required, as it is today.

## Images (option order)

- `lic_contact_sheet.png`: every LIC side by side, in the order 0, 1, 2, 3, 4, 4b, 5, 5b, 5c.
- `lic_<name>.png`: a line-integral convolution of the flow over the whole zone (512 px, t = 0). Brightness and
  colour rise with speed relative to Tri3D's RMS. Red rings mark the wells.
- `drift_<name>.png`, left panel: the Stardust law for one cycle (Period 2 s from `ARPG.unity`;
  `position -= flow(spawn) * lifetime * Period`, `Stardust.compute:142`), each particle's whole-cycle streak, in a
  1200 u window. Right panel: the same seeds advected along the field for 40 s with RK2. This long exposure shows
  the field's texture; Stardust does not draw it.

Reading the images. Tri3D shows the triangular lattice measured in the habitat probe (B25h): repeated chevrons,
and in the long exposure, crisp convergence streaks. `1-curl-simplex` is clean but bubbly: isolated round eddies
with fast rims (its p99 is 3.7x RMS), which reads as a pattern of its own. `2-curl-fbm3` is busier and more even,
with the same eddies plus small-scale turbulence. `3-curl-warped` is the most fluid: shearing filaments, folded
eddies and long streaming sheets in the exposure. It carries thin fast jets (max 19x RMS) and 8 % near-still
water. In `4` and `4b` the sun's swirl dominates: the sun's well covers the whole zone, so a 6 u/s swirl
underlies everything, and the slide term restores convergence (div/vort .24 to .52).

In the one-cycle Stardust panels, every candidate looks like short straight dashes, because Stardust
back-projects linearly at the spawn point over 2 s (up to 30 u). The noise's prettiness shows on screen through
the dashes' direction coherence and through the nebula's flow-advected noise, not through particle paths.

## The Nebula Span Lab candidate (5, 5b, 5c)

Self's input: the operator said "The last nebula playground I saw had some pretty sophisticated noise".
`labs-noise-facts.md` holds the Eyes extraction of that page, copied here unchanged. Its noise is a density warp,
not a flow. The page builds a two-level warp chain of fbm, ridged `1 - 2|n|` or curl fields over CultMath
simplex, with up to 4 octaves and a stretch along one fixed direction. It moves only by adding a constant offset
to the noise argument. Candidates 5 to 5c turn the lab's stretched fbm into a stream function and take its curl,
so the result is a velocity field. It scrolls on the same folded tau as the other curl candidates.

- **5, fixed axis**: long parallel jets, all leaning along azimuth 25. It reads as a prevailing wind across the
  whole zone. It is exactly divergence-free and costs the same as #2 (3 `snoise_grad`). A fixed axis is a global
  heading the zone has no physical reason to own.
- **5b and 5c, axis following the slope: the probe found this does not work as built.** The lab's stretch is a
  global linear map of the position (`dot(v, d) d`). With a varying d, the argument changes by about
  `|p| * delta(d)`, so it shears fastest far from the origin and where d turns quickly. At the well floors and
  saddles the contour tangent is undefined or flips sign, and the bilinear axis passes through zero there. The
  LIC shows dipole fans on every well and seam lines through the saddles. The numbers show it: a max speed 28x
  RMS, and a measured div/vort of .59. The flow is a discrete curl, but its structure is finer than the
  measurement step. Following the slope coherently needs the stretch in a local frame rather than in world
  position. One route is to sample the noise in coordinates of the gravity potential, such as height and arc
  along the contour. The other is to build the anisotropy from the height Hessian. Neither is probed here.
- **Following the Skein instead**: this is the same construction with d set to the nearest strand's tangent. It
  hits the same fault (the stretch is global in position, and the axis is undefined at strand junctions and
  between strands), so it needs the same local-frame fix. It is not rendered.

## CultMath gaps, by name

- **No curl node.** A 2D curl is a swizzle of `snoise_grad`/`fbm_grad` (`(g.y, -g.x)` of the xz-plane
  gradient), but nothing names it, and Aetheria would carry the stream-function law. Candidates are
  `curl2_snoise(float3)` and `curl2_fbm(float3, octaves, lacunarity, gain)`, in C# and HLSL, under the
  mirror tests.
- **`fbm_grad` exists** in C#, HLSL and GLSL. Candidates 1 and 2 need nothing else from CultMath.
- **No warp-with-Jacobian.** Candidate 3 composes the chain rule by hand. CultMath has no
  `warp_grad`/composition node that carries derivatives through a domain warp. The tape work
  (CultLib `docs/cultmath-tape-target.md`) is where derivative composition would live.
- **No 4D simplex** (`snoise(float4)`/`snoise_grad(float4)`). A true 3D curl (Bridson's vector potential: three
  potentials, so 3 `snoise_grad` per octave) that also evolves in time needs a fourth axis. Without one, a 3D
  flow must scroll by translating instead of evolving. This matters only if the flow keeps a y component.
- **No Hessian of simplex.** The lab generates `cultmath_snoise_gh` locally by splicing CultMath's gradient
  kernel (`makeGH`). CultMath has only the bound constant `CULTMATH_SNOISE_HESSIAN`. A slope-following stretch
  done analytically also needs the height field's Hessian, which is a separate gap on the Aetheria side.
- **Lab-local pieces that CultMath lacks** (from `labs-noise-facts.md` section 2): the curl mode (`dispVec`
  mode 2, `curlK`, a 3-component vector-potential curl); the ridged form `1 - 2|n|` with per-octave offset
  (CultMath's `ridged_grad` is `1 - |n|`); the stretch operator `Mop` with per-component offsets
  (`fieldComp`); and the warp chain (`fieldS`, `dispVec`, `dispY`, `brushPoint`, `ampG`). The facts also record
  a constant mismatch: the lab pins `SN_L = 10.099261` and calls it CultMath's, but CultMath's
  `SNOISE_LIPSCHITZ` is now 7.9640074.
- **No period constant.** The 867 period is a fact of `snoise(float3)` that nothing in CultMath states or pins.
  A named `snoise_period` and a test pinning it would let FlowField fold tau in double without relying on this
  probe.

## Consequences outside the noise

- **Convergence.** Tri3D's divergence is what makes drift pile into lines. A curl flow has none, so any habitat
  or fauna term that reads flow convergence, such as the B25 convergence candidate, goes to zero except through
  the slide term into wells.
- **Parity.** `aetheria-fauna:follow_up:flow-parity-under-cultmath` asks for Tri3D to move under CultMath. A curl
  flow built from `snoise_grad`/`fbm_grad` has its primitives in CultMath already, so FlowFieldParityVerify
  shrinks to the composition check and Tri3D never needs a CultMath twin.

To rerun: `py -3 -u curl_probe.py 512` from this folder, about 45 s. It needs numpy and Pillow, unlike the
sibling probes, and the habitat probe at its relative path. `run.log` is the committed run.
