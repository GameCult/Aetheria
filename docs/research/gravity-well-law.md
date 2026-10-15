# The conserving compact well law: derivation and probe

Imagination pass `imagination-ag3-well-law`, campaign aetheria-gravity, session self-2026-10-15-ag3, 2026-10-15, read at master 8bc5ef18. Serves ruling `aetheria-gravity:ruling:tame-tails-parent-only-rails` and revises question `aetheria-gravity:question:tail-taming-law` (now `...:tail-taming-law-r2`). Builds on `gravity-tail-taming.md` and `gravity-smooth-wells.md`: same data, same measures, their scripts reused.

The operator, verbatim, in order:
- "Having all the derivatives shake out nicely isn't just an affectation, the player can feel higher-order discontinuities especially in a game where moving with the grid is such a huge part of the experience."
- "this is all a consequence of exaggerating the contributions of small bodies. We're doing that so everything can coexist within one order of magnitude of scale, but that compression has annoying side-effects, like a well getting deeper the more its bodies are split by the zone generator. Two systems, same mass, one of them spent it on planets, that one goes waaaay deeper."
- On mass conserved at range: "That would be quite convenient if we can make it work out that way."
- "would be even more convenient if we can use this as an optimization and not have to evaluate the little guys at all except when near them"

Scripts in `gravity-well-law/`:
- `well_law.py`: the band term (closed-form pull and potential), and the tapered first draft.
- `check_well_law.py` and `check-output.txt`: force = dφ/dr, one-sided derivative jumps at 50 digits, circular speed, float32 error.
- `selfpull.py` and `selfpull-output.txt`: the tapered draft's self-pull against the band form.
- `probe3.py` and `probe3-output.txt`: the term tree per zone, the measures of `probe2.py`, and split invariance, whole-zone depth, containment and cost. Each law ran in its own process (rng seed 7, 6 draws) on Starfire, 20-95 s.

## 1. The law

**One primitive, the band term** U(r; ε, a, b): a Plummer-softened log source whose pull is exact up to radius a and fades to zero over the band [a, b].
- Pull toward the centre: F(r) = r/(r²+ε²) · B(s), with s = r².
- B(s) = 1 for s ≤ a²; B = 1 − S(t) on the band, t = (s − a²)/(b² − a²); B = 0 for s ≥ b².
- S is the order-4 smoothstep, S(t) = 35t⁴ − 84t⁵ + 70t⁶ − 20t⁷.
- Potential: 0 beyond b. On the band, ½[R·ln((t+τ)/(1+τ)) − (I(1) − I(t))], where τ = (a²+ε²)/(b²−a²), (1 − S(t)) = (t+τ)·Q(t) + R, and I is the antiderivative of Q. Inside a, ½ln((s+ε²)/(a²+ε²)) plus the band's value at t = 0.
- a = b = ∞ is the global term, ½ln(r²+ε²).
- The cost is one log, plus a 7-step synthetic division and Horner on the band only.

**A node's terms.** Every node F of the orbit tree contributes at its own position, F being a body, or a bodyless node with its ring group standing in for the body:

c(m_F)·U(ε(m_F), L_F, B_F) + c(M_F)·[U(ε(M_F), max(B_F, ½Λ_F), Λ_F) − U(ε(M_F), L_F, B_F)]

- m_F is the body's mass and M_F the family's. c and ε are the existing `WellStrength` and `RocheLimit` curves, evaluated at m_F or M_F; no new curve.
- L_F is the outermost child's distance.
- B_F = max(1.5·L_F, max over children of D_C + Λ_C): the family's own support.
- Λ_F is the family's reach in its parent: max(nominal, 1.5·B_F). The nominal reach probed is 0.5·D, 1·D or 2·d_Hill, with d_Hill = D·sqrt(c(M_F)/(2c_parent)) (B25's tidal radius, for the family's mass).
- A ring member of a bodyless node reaches 4·D, so its partners across the ring feel it whole.
- A root's Λ is infinite.
- A body with no massive descendants is one term, c(m)·U(ε(m), ½Λ, Λ).

**Bodyless nodes.** The ring group G is the massive children at the innermost distance. It contributes c(M_G)·[U(ε_G, L_F, B_F) − U(ε_G, D_ring, B_G)], with B_G = max(1.5·D_ring, max over members of D + Λ). L_F is raised to at least B_G.

**What this guarantees, by construction (not approximated):**
- *Inside L_F the node pulls exactly as its body alone*, c(m_F)·d/(d²+ε²). The family terms are zero there, since both of their windows are flat at 1 inside L_F and cancel. So a child's parent-only rail is exactly the body's Plummer pull, and no child feels its own mass through its parent's family term. The tapered draft did feel it: a lone body on a bodyless node was pulled at 38% of its own pull by its own mass (`selfpull.py`; the band form: 0).
- *Beyond B_F the family pulls exactly as one body of mass M_F at the node*, c(M_F)·r/(r²+ε(M_F)²), out to max(B_F, ½Λ_F), and fades to nothing at Λ_F. Every descendant term lies inside B_F: the probe counts 0 containment violations of 70,890 (term, ancestor) pairs.
- *Split invariance.* Merge a family into one body of its mass in the same slot. Outside B_F the field is identical, term for term, whenever the merge leaves Λ_F unchanged, that is, whenever the nominal reach is at least 1.5·B_F. With the tidal reach this holds for 2382 of the 2427 families whose children sit inside the ruled generator window (half the tidal radius, `moons-within-reach-ruled`), and for 4573 of all 6976 families in today's generator, which places most children outside that window (B25).
- *The zone's far field is exactly c(M_zone)*, at 2Z and at 10Z (p10 = p90 = 1.00). The root's Λ is infinite, so beyond B_root the zone is one body of its total mass however the generator split it.
- *Smoothness.* φ is a function of r², so it is C^∞ at every centre and wherever B is constant. On each band edge it is C⁴: `check-output.txt` measures φ⁽⁰⁾-φ⁽⁴⁾ jumps of at most 1e-45 (zero at 50 digits) and a φ⁽⁵⁾ jump of 5.5e3 at r = a, the first nonzero order. The soft core is Plummer: no C1 seam at the Roche limit. The order is one constant: order 6 gives C⁶ for 4 more FMAs.
- *Grid = K·φ.* The closed form satisfies F = dφ/dr to 1.6e-9 (band) and 1.7e-6 (global term, finite-difference floor). In float32 the potential's error is at most 1.5e-5 per unit strength, about 2e-3 u of drawn height at c = 300 and K = 2.05, against the parity verify's 0.03 u tolerance.
- *Time.* Every term rides its node's rail position, and its parameters are fixed when the zone is built, so the field is C^∞ in time. The centre is the node's body, not the family's centre of mass: what is exact is "the merged body in the same slot". A centre of mass would add a moving centroid per family and buy nothing a test can see beyond B_F.

**The peak slope.** Plummer's peak pull is 0.5·c/ε, at r = ε, which is the Roche limit. Today's harmonic core peaks at 1.0·c/R. `core-at-roche-and-exaggeration-up` holds the peak drawn slope, so K must halve, from 4.0983607 to 2.0491803. Every slope outside the cores then draws 2x steeper than at today's K, while the tamed funnel pulls about 2.6x less.

## 2. Probe

The ratio is the free-coasting circular speed over the parent-only rail speed (1 = rails honest). The other measures are as in `gravity-smooth-wells.md` §2. "Split" is the share of in-window families whose field outside B_F is unchanged by merging them, then the error p50/p90 at 1.05·B_F over all 6976 families. "Depth" is the zone's deepest point against one body of the zone's mass, p50/p90.

| # | Law | Ratio p50/p90: Sun · GasGiant · Planet · Planetoid · Belt | Mid-zone pull Z = 1000 / 2125 / 4182 / 6846 / 10000 | Median-sun planets v p50; share 12-18 | Sun-class rails v p50 | Far field / c(M_zone), 2Z; 10Z | Split | Depth | Pull < 0.5 c/D |
|---|---|---|---|---|---|---|---|---|---|
| 0 | today | 2.00/2.47 · 1.63/2.23 · 1.24/1.72 · 1.10/1.43 · 1.16/1.55 | 1.59/1.28/0.92/0.68/0.51 | 17.48; 0.92 | 23.62 | 5.54; 5.54 | n/a; 0.53/1.50 | 3.43/4.83 | 46 |
| a2 | log-ratio L = 1·D | 1.26/1.55 · 1.12/1.45 · 1.02/1.27 · 1.01/1.20 · 1.02/1.26 | 0.65/0.46/0.29/0.21/0.15 | 16.86; 1.00 | 19.93 | 0.36; 0.02 | n/a; 0.48/1.18 | 1.28/1.45 | 407 |
| c2 | Lamb-Oseen + halo L = 1·D | 1.26/1.55 · 1.11/1.45 · 1.02/1.27 · 1.00/1.19 · 1.02/1.26 | 0.66/0.46/0.30/0.21/0.15 | 17.16; 0.99 | 20.04 | 0.36; 0.02 | n/a; 0.48/1.19 | 1.26/1.41 | 146 |
| b1 | screened Plummer λ = 0.5·D (smooth k0-half-d) | 1.16/1.49 · 1.04/1.37 · 0.99/1.20 · 0.99/1.16 · 0.99/1.22 | 0.42/0.29/0.19/0.13/0.09 | 16.30; 0.98 | 17.20 | 0.07; 0.00 | n/a; 0.40/0.95 | 1.10/1.18 | 3610 |
| t3 | tapered draft, Λ = 2·d_Hill | 1.05/1.27 · 0.99/1.24 · 0.97/1.14 · 0.98/1.13 · 0.98/1.17 | 0.56/0.40/0.25/0.17/0.13 | 17.16; 0.99 | 23.60 | 1.00; 1.00 | 2427/2427; 0.00/0.10 | 0.99/1.31 | 234 |
| w1 | band, Λ = 0.5·D | 1.01/1.24 · 1.00/1.24 · 0.99/1.14 · 0.99/1.12 · 0.99/1.15 | 0.55/0.39/0.24/0.17/0.13 | 17.17; 0.99 | 23.60 | 1.00; 1.00 | 2075/2427; 0.52/1.63 | 1.00/1.32 | 234 |
| w2 | band, Λ = 1·D | 1.09/1.32 · 1.01/1.30 · 0.97/1.18 · 0.97/1.15 · 0.98/1.19 | 0.61/0.43/0.27/0.19/0.14 | 17.17; 0.99 | 23.60 | 1.00; 1.00 | 2380/2427; 0.00/0.89 | 1.01/1.36 | 234 |
| w3 | **band, Λ = 2·d_Hill** | 1.09/1.32 · 1.00/1.29 · 0.97/1.17 · 0.97/1.15 · 0.98/1.19 | 0.60/0.42/0.27/0.19/0.14 | 17.17; 0.99 | 23.60 | 1.00; 1.00 | 2382/2427; 0.00/0.91 | 1.01/1.34 | 234 |

Readings:
- **The depth column is the operator's complaint, measured.** Today a zone's deepest point is 3.43x (p90 4.83x) the depth of one body of the same mass. Under the conserving law it is 1.01x (p90 1.34x). The far field at range is 5.54x today and exactly 1.00x here.
- **Rails.** The p50 ratio is 0.97-1.09 for every class (today 1.10-2.00). The p10-p90 spread is 0.77-1.32, from siblings felt inside their reach. The band form is about 0.05 wider at p90 than the tapered draft at the same reach: a sibling is felt whole out to half its reach. Under b1 (screened, not conserving) the suns' children's rails slow from 23.6 to 17.2 u/s and 3610 children fall below half the log pull. Under the conserving law the root has no reach, so its children keep their rails (23.6), and only the 234 children inside their parent's Roche limit (B35's 234) are weak.
- **12-18 u/s** holds: planets round a median sun run at 17.2 u/s p50, 99% in band.
- **Funnel.** Mid-zone pull falls to 0.38-0.40 of today's in every zone size.
- **The 234.** These are the children inside their parent's Roche limit, unchanged from B35. The Plummer core gives them 0.71·√c at ε rather than the harmonic core's solid-body rail. `children-within-reach` moves them out.
- **Split.** The band with the tidal reach is exact for 98% of in-window families. The tapered draft is exact for 100%, but at the cost of the self-pull above. Families outside the window (today's generator) differ near B_F (p90 0.91) and never at 2Z.

## 3. Evaluation cost

- **Today:** every well everywhere, 17 per sample or texel (p99 58), each one log.
- **Conserving law (w3):** 30 terms per zone (p99 114).
  - *Sim sample* (uniform in the zone disc), walking the tree and skipping any subtree whose reach does not contain the sample: 4 terms evaluated p50 (p99 15, mean 5.6), after 10 reach tests (p99 34).
  - *Shader texel*, in the 8192 u Surface Displacement window (2048² at 4 u), flat loop with a reach test per term: 3 terms in support p50 (p99 14; per-window mean p50 3.7, p99 12.6). 21 terms meet the window p50 (p99 76), so a CPU cull to the window is optional.
- The walk is exact, not Barnes-Hut: the field outside a subtree's reach is zero, so skipping it changes nothing.

## 4. Consequences the cut carries, or names for later

- **Rails (orbits-from-field's ring law).** A child's period reads its parent node's own terms at d (`PullAround`). A ring member of a bodyless node adds each ring partner's own terms, projected toward the node at their chord. A binary member at chord 2D therefore feels c(M_partner)·2D/(4D²+ε²), today's half-partner rule in the new law. Gauss's "inside" sum and the recursive `Strength` go. A strengthless orbit on a bodyless ring keeps taking the mean of what its bodied partners feel, until `children-within-reach` deletes that case (`binaries-orbited-from-afar`, follow-up `companion-ring-residuals-r2`).
- **K halves** (§1, "The peak slope").
- **children-within-reach (named, not done here).** The law owns a reach (Λ) and a family support (B). The generator's "within reach" should read them rather than spell the tidal radius a second time: children inside half d_Hill fit Λ = 2·d_Hill with room (98% exact split). Circumbinary children belong beyond the ring group's B_G = 5·D_ring, which is 2.5x the separation, inside Holman & Wiegert's 2-4x.
- **The outer body round a binary** feels c(M_pair+moons), the compressed strength of the summed mass, not today's sum of strengths. That is conservation, and `OrbitsFromFieldTests.AnOuterBodyFeelsTheWholePairAndItsMoons` changes to match.
- **gravity-law-one-spelling** stays open: the band arithmetic is still spelled in C# and in the HLSL template. Its coefficients can be generated from one C# array.

## Prior art

- **The construction is a partition of unity with charge neutrality.** Ewald 1921 splits each charge into a neutralised short-range part plus a long-range remainder. TreePM codes (Bagla 2002; Springel 2005, GADGET-2) use an erfc split with the long range on a mesh. The fast multipole method (Greengard & Rokhlin 1987) gives exact far fields of clusters by multipole expansion, with a near/far split by well-separatedness. Here the far part of each family is truncated at its monopole by fiat, which is exactly the operator's "mass conserved at range". The near part is exact and compact, so no expansion error arises: the law is defined by the truncation rather than approximated by it.
- **Compact smooth windows.** Wendland 1995 (Adv. Comput. Math. 4:389) gives compactly supported C^{2k} radial functions. The smoothstep-in-r² used here is a C^k window of the same family, chosen because ∫ r·B(r²)/(r²+ε²) dr stays elementary (log plus polynomial), so the grid stays exactly K·φ.
- **Force switching.** Steinbach & Brooks 1994 prefer switching the force over switching the potential. This law switches the source profile, so the force and the potential agree exactly, and it switches with C⁴ continuity rather than C¹.
- **Spheres of influence.** Patched conics (KSP, Laplace's r_SOI = a·(m/M)^(2/5)) let one attractor own each sphere of influence with a kink at the boundary. This law keeps that dominance (inside L_F only the parent pulls on the rail) and replaces the kink with a C⁴ band.

## Confidence

- §1: high. The guarantees are construction plus the measured checks: zero containment violations, far field exactly 1.00, C⁴ jumps at 50 digits.
- §2: high on internal consistency. Row 0 reproduces the earlier baseline within 0.01 (2.00 against 2.01, from a different rng consumption order). Medium on absolute ratios, for the caveats of `gravity-tail-taming.md`: 6 phase draws, phase-averaged circular speeds, positions from the 2026-10-13 generator.
- §3: high on counts. Op costs are by inspection, not compiled HLSL.
- The knobs (order 4, band 1.5, fade from ½Λ, ring reach 4) were chosen once, not swept. Each is a single constant.
