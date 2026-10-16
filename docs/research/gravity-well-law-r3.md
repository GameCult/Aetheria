# The well law and the generator, co-designed; graded as nested basins

Imagination pass `imagination-ag3-well-law` (r3), campaign aetheria-gravity, session self-2026-10-15-ag3, 2026-10-16, read at master b71dfc21. It revises `gravity-well-law.md` (r2) for question `aetheria-gravity:question:tail-taming-law-r3`, under rulings `tame-tails-parent-only-rails`, `wells-read-as-nested-basins` and the lean `tail-taming-lean-conserving-compact`.

The operator, verbatim:
- "'reach is narrower than the generator's window' tells me that this exploration has not been taking full advantage of its permission to also treat zone generation (or at least the variables governing its behavior) as part of the solution space."
- "Hypotheses regarding the aesthetic quality of the proposed solution can be somewhat graded by running the minimap shader on the resulting heightfield: it presents both potential contour lines and shades steep slopes (which should be avoided by overencumbered freighters) with scary orange plan contours"
- (ruling `wells-read-as-nested-basins`) "What we'll want to see is clear cusps between bodies and defined wells ... Fields should flow into each other with enough curvature to create saddles to ride along, and parents should encompass their children."

New scripts, all in `gravity-well-law/`:
- `replace.py`: the generator rules, applied as a re-placement of each generated zone. It keeps the same tree and masses and folds whatever does not fit into its parent.
- `probe3.py`: gained `place=`, `cusp=` and `spacing=` arguments and a stricter split counter (same reach and same fade start).
- `probe4.py`: the basin metrics and the minimap port.
- `well_law.py`: gained a stable series branch for τ > 1 (§3).

Outputs are `probe3-r3-output.txt`, `probe4-output.txt` and `minimap/C*-*.png`.

## 1. The two pulls the law and the generator must balance

- **Rail honesty** wants the parent to own the child's orbit: no sibling's term may reach it. In the band law a body's term reaches Λ = λ·D. So siblings must stand at least a reach apart, D_(i+1) ≥ D_i/(1 − λ), and a child's whole family must end before its parent's fade starts (B_F ≤ ½Λ_F). Split invariance is exact under the same condition.
- **A visible cusp** wants the child to keep its own basin against the parent's slope. With the Plummer core the child's steepest pull is 0.5·c_c/ε_c, so a saddle exists on the child-parent line only if D ≥ 2·ε_c·c_p/c_c.
  - For a planetoid moon of a median planet that is about 53 u. Today's generator puts moons from 2·PlanetSafetyRadius (about 18 u) upward, so 805 of 10,127 planetoid cusps vanish today (C0 below).
  - The saddle must also lie inside the child's reach: r_saddle ≈ D·c_c/(c_c + c_p), 0.3-0.4 D for this mass range. So λ ≥ about 0.4.
- **The window between them.** For a child of parent P: [max(1.5·Roche_P, 3·ε_c·c_p/c_c), ½·λ·D_P/β], and the upper end is D_P/6 for λ = ½ and β = 1.5.
  - It is about [45-80, 102] u for moons of a median planet at 610 u.
  - For the root's children it is [120, 0.75·Z].
- **What it costs.** The window is narrow, so the honest generator holds fewer bodies than today's. That count is the price, measured below.

## 2. Candidates

Each candidate varies one thing from the one before. The phases are the same in every candidate (a hash of zone and node), and so are the zones. "Generator" means `replace.py` applied to today's generated zones.

| # | Law | Generator rule | Varies from previous |
|---|---|---|---|
| C0 | today (harmonic core, log tails), K 4.098 | today | control |
| C1 | band law, reach ½·D, K 2.049 | today | the law |
| C2 | band law | strict: Roche margin 1.5, window from the law's reach, siblings a full reach apart | the generator |
| C3 | band law | C2 plus the cusp bound D ≥ 3·ε_c·c_p/c_c | the cusp bound |
| C4 | band law | C3 with siblings half a reach apart, D_(i+1) ≥ D_i/(1 − λ/2) | the spacing |

**Hypotheses, written before the first render run and recorded unchanged:**
- **H0** (C0): zone-wide funnel contours round the sun; small bodies are blobs at the minimap's contour spacing; orange only at cores; low nesting.
- **H1** (C1): the funnel contours go and each family gets its own contour set. Blob share falls because the depth range shrinks. Steep share rises (K halved doubles the drawn slope). Dead ground stays near 0, because a parent's body slope persists under its children.
- **H2** (C2): about half the bodies remain. Each stands alone with plain parent slope between (the "self-contained spikes" risk). Nesting rises.
- **H3** (C3): moon cusps never vanish, and blob share falls further.
- **H4** (C4): body count recovers toward today. Rails loosen to about ±10%, and blob and nesting stay near C3.

## 3. Results

Basin metrics are over 60 zones (every 25th, all Z) at the minimap texel (15.625 u). Cusps are over every pair in all 1500 zones. Rails come from `probe3-r3-output.txt` (6 draws).

- **Nodes kept:** C2 13,447, C3 11,260, C4 20,768 (of 31,640).
- **Cusp vanished:** count / pairs for planets and planetoids.
- **Prominence:** cusp prominence over c_child, p50, planets and planetoids.
- **Blob:** bodies without their own closed contour at the minimap spacing DR/20.
- **Nested:** the child's basin spills into its parent's.
- **Saddle:** p50 of the smaller Hessian eigenvalue at spill saddles, 1/s².
- **Dead:** share of the disc with |F| < 0.05 u/s², a 10 u drift over a 20 s coast (about a hull length; 1% of a packed Djinni's 4 u/s², B25).
- **Steep:** share with the port's danger blend > 0.5 (freighter no-go).
- **Ratio:** rail honesty, p10-p90 across classes.
- **Split:** families whose field outside B is unchanged by merging them.

| # | Nodes | Wells/zone p50 | Cusp vanished (planet; planetoid) | Prominence/c (planet; planetoid) | Blob | Nested | Saddle | Dead p50/p90 | Steep p50/p90 | Lanes open | Ratio p10-p90 | Split exact | Far field |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C0 | 31,640 | 17 | 358/6879; 805/10127 | 0.94; 0.73 | 0.876 | 0.459 | 0.022 | 0.000/0.001 | 0.026/0.146 | 0.97 | 0.95-2.47 | n/a | 5.54x |
| C1 | 31,640 | 17 | 261/6879; 499/10127 | 0.91; 0.57 | 0.585 | 0.542 | 0.013 | 0.001/0.005 | 0.029/0.125 | 1.00 | 0.83-1.24 | 1765/6976 | 1.00x |
| C2 | 13,447 | 8 | 203/3724; 164/1633 | 0.27; 0.16 | 0.729 | 0.528 | 0.017 | 0.001/0.003 | 0.016/0.078 | 1.00 | 0.99-1.07 | 3198/3199 | 1.00x |
| C3 | 11,260 | 7 | 30/2766; 3/890 | 0.34; 0.27 | 0.488 | 0.588 | 0.019 | 0.001/0.003 | 0.017/0.084 | 1.00 | 0.99-1.03 | 2308/2309 | 1.00x |
| C4 | 20,768 | 12 | 47/6177; 4/4894 | 0.54; 0.36 | 0.561 | 0.542 | 0.020 | 0.001/0.003 | 0.023/0.109 | 1.00 | 0.90-1.10 | 4107/4108 | 1.00x |

Every candidate keeps the r2 guarantees where the generator places it: far field exactly c(M_zone), zone depth 0.99x of one body of its mass (p90 1.29-1.34), planets round a median sun 99-100% in 12-18 u/s, and 0 children under half their parent's pull. That last one was 234 in C0 and C1: the Roche margin removes them. The one family per placed run that is not exactly split-invariant is a bodyless node's child; I did not chase it.

**Renders** (`minimap/C<n>-<zone>.png`). The zones: small (Z 1000, seed 1), binary (Z 2125, seed 10), rosette (Z 4182, seed 104), moons (Z 6846, seed 253, 25 moons today). Per render (steep, blob, nested) is in `probe4-output.txt`.

| Hyp. | Held / failed | Why |
|---|---|---|
| H0 | held | C0-moons shows 17 concentric zone contours round the sun with planets as orange specks inside them. Blob 34/34 there and 0.876 overall; nested 0.459. |
| H1 | held, but steep did not rise | The funnel contours are gone and every planet has its own ring set, with a cusp against the sun (C1-moons). Blob falls to 0.585. Steep *falls* slightly (p90 0.146 to 0.125): the funnel's summed slope near the sun outweighs the halved K. |
| H2 | half held | 42% of nodes remain. Nesting does not rise (0.528). Blob *rises* to 0.729: the strict window squeezes moons close to their parents, where the parent's slope eats their cusp (planetoid prominence 0.16). No dead plateaus appear (dead 0.001), so the "self-contained spikes" failure does not show as flat ground. It shows as small cusps. |
| H3 | held | Planetoid cusps vanish for 3/890 (C0 805/10127). Blob 0.488 is the best of the five, nested 0.588 the best. The price: 36% of nodes kept. |
| H4 | held | 66% of nodes kept and wells 12 per zone (C0 17). Cusps survive (planetoid 4/4894). Blob 0.561, nested 0.542, saddle curvature 0.020, close to today's 0.022. Rails 0.90-1.10. |

**What no candidate fixes.** Blob stays at 0.49 or above in every candidate, because the minimap spaces its 21 contours linearly over the deepest body's depth (`IsolinesUI.shader` line 178, `_DepthRange` = the deepest planet, ZoneRenderer.cs 622-626). With c ∝ m^0.2 a planetoid's own well is a few percent of a sun's depth, below one contour step under any law. Fixing it is a presentation choice (log-spaced or per-family contours) for grid-noise-presentation, not a law or generator one. The "rosette" render keeps 11/11 blobs in C2-C4 because the root rosette's members are deep and close; rosette geometry is `rosette-generation-r3`'s.

**Precision finding.** The band closed form cancels catastrophically when the softening is comparable to the band (τ = (a²+ε²)/(b²−a²) > 1). The float32 error is 6.9e-3 at τ = 3 and 12 at τ = 10 (`check-output.txt` §5). Today's generator produces such terms: children inside their parent's Roche limit, and binaries shrunk inside their members' cores. `well_law.band_potential` now sums the band as a series in (1−t)/(1+τ) when τ > 1 (float32 error 1e-7). Forces were never affected. Potentials, and so the r2 depth column, were, slightly; the r2 rows rerun here (w1 and w3 on today's generator) match r2 to two digits. The r2 split claim "2382/2427" used a counter that did not check the fade start; the corrected count for w3 is 2329/2427 in-window, 3425/6976 overall.

## 4. Recommendation and what the generator must do

**Recommended: C4.** Band law with reach ½·D, plus the generator rules below. It is the only candidate that holds all three of:
- the nested-basins bar near today's saddle curvature, with cusps nearly always present and 40% fewer blobs;
- rails within ±10% (today 0.95-2.47);
- two thirds of today's bodies.

It is exact where it matters: far field, merged families, and no child in a core.

**Alternative: C3.** It wins on every basin number and has exact rails, at 36% of the bodies. If the operator values honest rails and clean basins over body count, C3 is the choice; the rules differ by one constant (spacing).

**Generator rules (children-within-reach's scope).** Each rule reads the law's reach (WellTree.Reach / Support), never a respelling.
1. **Lower bound** D ≥ max(1.5·Roche_parent, 3·ε_child·c_parent/c_child), so there is no child in a core and every child keeps a cusp. Replaces PlanetSafetyRadius at ZoneGenerator.cs 407, 587, 632, 648.
2. **Upper bound:** the child's family ends before its parent's fade: 1.5·D_C ≤ ½·λ·D_P and D_C·(1+λ) ≤ ½·λ·D_P, i.e. D_C ≤ D_P/6 at λ = ½. A root's children keep 0.75·Z (line 406). Replaces ChildDistanceMaximum's neighbour rule (650-651) and the linear distance map (631-632).
3. **Spacing:** D_(i+1) ≥ D_i/(1 − λ/2). Replaces distMulMin/distMulMax (425-426, 440-441, 455-456, 497-498, 615) as the spacing owner. They may remain as random spread above the minimum.
4. **Capacity:** a requested count (452, 494, 422, 437) is a request. Children that do not fit are folded into the parent's body (family mass, so the field at range is unchanged).
5. **Pairs:** a binary whose members cannot sit outside each other's Roche limit inside its node's reach is made one body (486). Circumbinary children sit beyond (1 + 4)·D_ring·1.5.
6. **Rosettes** keep their own follow-up (twisted geometry). The law's ring group must then accept members on two radii.

**Dead or derived generator variables:**
- `PlanetSafetyRadius` (Settings.cs 113, Settings.asset 135-138): dead.
- `ChildDistanceMinimum` and `ChildDistanceMaximum`: derived from the law's reach, Roche and cusp bounds.
- `distMul*` as spacing: dead.
- `p0ChildDist` and `p1ChildDist` (573-574): derived (members' windows, 0.2 × the nearest-partner chord).
- Unchanged: counts as requests, `massMul*`, `jupiterJump`, `massFraction`, `MassFloor` and the final mass renormalisation (mass split); the m^p exponent and the WellStrength and Roche curves. Conservation removes the reason to retune p.

## Confidence

- Basin metrics: medium. They are measured at the minimap texel (15.6 u), so moons under two texels are basins of one or two cells, and "nested" is limited by that resolution, not by the law.
- Cusps (analytic line samples, all zones) and rails: high.
- The port: the frag logic of IsolinesUI.shader and the material's values. The background is a stand-in colour and the window is the whole zone at the minimap's texel. It is a port, not the shader; Self renders the chosen candidate in Unity.
- Hypotheses H0-H4 were written before the first render run.
