# Taming the summed gravity tails: prior art and probe

Eyes pass `eyes-ag3-tail-taming`, campaign aetheria-gravity, session self-2026-10-15-ag3, 2026-10-15, read at master c9815379. Facts with evidence pointers; no recommendation. Serves ruling `aetheria-gravity:ruling:tame-tails-parent-only-rails` (operator: "tame the tails then, keep parent-only rails"), under `field-strength-ruled` (planets 12-18 u/s round a median sun) and `core-at-roche-and-exaggeration-up` (core radius = Roche limit 7.32·m^0.25).

Scripts: `gravity-tail-taming/laws.py` (the candidate laws, force and potential), `check_laws.py` (K0/K1 accuracy, force = -dφ/dr for every law), `probe.py` (the measures), output in `probe-output.txt`. Data: `docs/research/2026-10-13-orbit-probe/data/bodies.csv` (1501 zones, 31,640 nodes), as B35/B38 used. Each law ran in 8-18 s on Starfire.

## The law today and the problem

`GravityField` (Assets/Scripts/ServerShared/GravityField.cs at c9815379): pull c/r beyond the core radius R, c·r/R² inside, potential c·(ln(r/R) + 1/2) beyond and c·r²/(2R²) inside. Map B19: c = 45.08·m^0.2. `Gravity.cginc` is generated from it and evaluates `gravityPotential` once per well per texel (one `log`).

The log potential is harmonic in the plane, so 2D Gauss's law holds: the phase-averaged pull at distance d from a parent is the strength enclosed within d, and siblings outside cancel (map B37, B38). Every sibling inside the child's distance counts in full at any range, which is the funnel (B21) and the 1.2-2x ratio (B38).

## Question 1: screened and truncated potentials

**2D screened Poisson (Yukawa, modified Helmholtz).** The Green's function of ∇²φ − φ/λ² = source in the plane is (1/2π)·K0(r/λ). K0(x) ≈ −ln(x/2) − γ for small x and ≈ sqrt(π/2x)·e^(−x) for large x (Abramowitz & Stegun 9.6.8, 9.7.2). The pull, (c/λ)·K1(r/λ), is c/r near and exponentially small beyond λ. Two physical systems obey exactly this law. Abrikosov vortices in a London superconductor interact through K0(r/λ_L) (the London penetration depth). Quasi-geostrophic point vortices in the equivalent-barotropic model interact through K0(r/L_d) (the Rossby deformation radius). Gauss's law fails, so a ring no longer acts as its centre. Graf's addition theorem for modified Bessel functions (DLMF §10.44) gives the ring average of K0 over a circle of radius s, seen from distance d. It is K0(d/λ)·I0(s/λ) outside the ring (d > s). Inside the ring (d < s) it is I0(d/λ)·K0(s/λ), which pulls outward: weak, but not zero as under the log law.

**Softening (changes the core, not the tail).** Plummer softening φ = −Gm/sqrt(r² + ε²) and spline kernels (Monaghan & Lattanzio 1985, used in GADGET-2: exactly Newtonian beyond 2.8ε) only reshape the inside. Aetheria's harmonic core at the Roche limit is of this family. None of them changes the tail, so none changes the enclosed/parent ratio.

**Split, not screening.** TreePM codes split the Newtonian potential: a short-range part, φ·erfc(r/2r_s), is summed pairwise and truncated at about 4.5 r_s, and the long-range part is solved on a mesh (Bagla 2002; Springel 2005, GADGET-2, MNRAS 364:1105). The total force stays Newtonian. The split exists for speed and does not change the physics, so it offers no precedent for weakening the far field.

**Molecular-dynamics cutoffs** (Allen & Tildesley, *Computer Simulation of Liquids*; Steinbach & Brooks 1994, J. Comput. Chem. 15:667, DOI 10.1002/jcc.540150702):
- *Hard truncation*: the force jumps at r_c, which gives impulsive kicks and energy drift under any integrator.
- *Shifted potential*: the energy is continuous but the force still jumps.
- *Shifted force*: the force goes linearly to zero at r_c. The force is continuous, but the potential is altered at every r < r_c.
- *Potential switching* (polynomial S(r) on the energy over [r_on, r_c]): leaves a force "bump" larger than the unswitched force inside the window.
- *Force switching*: S(r) applies to the force, and a constant keeps the potential continuous. Steinbach & Brooks rate force switching and force shifting the best atom-based cutoffs, and add that a longer cutoff matters more than the choice of method.
- Damped shifted force (Fennell & Gezelter 2006, J. Chem. Phys. 124:234104) is the electrostatics descendant: an erfc damping plus a force shift.

All of these are conservative pair potentials, so a potential exists and a drawn grid can show it exactly. What they trade away is accuracy beyond r_c, which here is the point.

**Orbits.** Only the Kepler and harmonic potentials have all bound orbits closed (Bertrand's theorem). The log law already precesses (rosettes, docs/research/rosette-relative-equilibria.md), and every candidate below does too. In the probe, the cap and switch laws, being C0 and C1 in force, put a slope break on the drawn grid at L (and at 2L for the switch). The switch also leaves the force exactly zero beyond 2L, so the grid there is flat and a child out there has no rail pull (measured below).

**λ per body.** The laws were probed with a per-body screening length λ = k·D (D = the body's distance from its parent, the zone radius Z for a root) and λ = 2·d_Hill (B25's tidal radius D·sqrt(c/2c_parent); falls back to D without a body parent). Since d_Hill ≈ 0.5·D for a planet round a median sun, λ = 2·d_Hill and λ = 1·D come out within 0.03 of each other on every measure (table). A body's own children (inside half its Hill radius, B25) sit at r/λ ≤ ~0.25 for λ = 1·D, where K1 still gives the full c/r (x·K1(x) = 0.94 at x = 0.25, 0.60 at x = 1, 0.28 at x = 2). Its siblings sit at r/λ ≈ 1-2, where their pull is cut to 0.60-0.28 of c/r and falls off as e^(−x) beyond. Because λ differs per body, the summed field is not the solution of one screened Poisson equation. It is a sum of per-body kernels, still a conservative potential.

## Question 2: the hyperbolic-plane Green's function

Confirmed, with a caveat. On the hyperbolic plane (curvature −1) the Green's function (1/2π)·ln coth(r/2) is harmonic away from the source, and the divergence theorem holds on the manifold. The flux through a geodesic circle is the enclosed strength, and the circumference is 2π·sinh r, so the field is c_enc/sinh r: a ring acts as its centre outside and pulls nothing inside. The 1/sinh factor is common to parent and enclosed siblings, so the enclosed/parent ratio is the flat one; it decays every body equally. Caveat: Aetheria's positions are Euclidean. On flat distances the kernel is not harmonic: its flat Laplacian is (r·cosh r − sinh r)/(r·sinh² r) > 0, a distributed screening halo. So used that way it behaves as a screened law with one global length, asymptotically e^(−r/ℓ). Probe [10] (ℓ = Z/2): the suns' children ratio p50 moves only from 2.01 to 1.98, because ℓ is much larger than sibling separations. The mid-zone inward pull falls by about 20%.

## Question 3: games that sum (or refuse to sum) many wells

| Game | How local dominance is kept | Artefact accepted | Source |
|---|---|---|---|
| Kerbal Space Program | Patched conics: exactly one attractor inside its sphere of influence, r_SOI = a·(m/M)^(2/5) (Laplace) | No Lagrange points, no perturbations; trajectories kink at SOI boundaries; Keplerian "on rails" outside physics range | KSP forum "on rails" threads; patched-conic literature (Wikibooks Astrodynamics) |
| Outer Wilds | Each body is a gravity volume; planets use **linear** falloff, the sun and some moons inverse-square; planets feel only the sun; a moon feels only its planet plus an added acceleration matching the planet's orbit; volume priorities override rather than sum | Planets don't perturb each other (developer Logan Ver Hoef: real mutual pull would make them "escape orbit"); non-physical falloff chosen for feel | New Horizons mod schema `GravityVolumeInfo` (fields `fallOff`, `upperRadius`, `priority`, `layer`); PC Gamer "Recreating gravity in games is harder than you'd think" |
| Super Mario Galaxy 1/2 | Gravity areas (sphere, plane, cylinder...) with priorities; the highest-priority area wins; equal priorities are combined | Gravity direction switches discontinuously at area boundaries; no superposition across priorities | Luma's Workshop wiki "Gravity"; Super Mario Wiki "Gravity field" |
| Universe Sandbox | Full N-body integration, selectable integrators | No local dominance: true superposition, with integrator drift | Universe Sandbox wiki "N-Body Simulation" |
| Elite Dangerous | Orbits set by the Stellar Forge generator; no source found for runtime propagation (forum claims only) | Unverified | Frontier forum thread; PC Gamer Stellar Forge article |
| Children of a Dead Earth | No source found for n-body versus patched conics | Unverified | none found |
| Spore (space stage) | No source found for any summed gravity | Unverified | none found |
| Geometry Wars warp grid | Spring-mass grid displaced by short-range forces from black holes, bullets and respawns; not a potential; border anchored, damped | The grid is a presentation, not the field ships feel; effects are local by construction | Tuts+ "Make a Neon Vector Shooter: The Warping Grid"; gamedev.net "Fluid Grid" thread |

## Question 4: the probe

**Measures** (`probe.py`), over 6 random-phase placements per zone:
- *Ratio*: the radial pull toward the parent at the child's position, phase-averaged. It is computed from every body outside the child's own family, minus the field at the parent from every body outside the parent's family (the parent rides its own rail), so tides and outer siblings count. It is reported as a circular-speed ratio sqrt(a_rel / a_parent_own), by the child's class, p10/p50/p90/p99. Unlike B38's Gauss-only figure it can fall below 1.
- *Mid-zone*: the inward radial pull at 8 points at 0.5·Z (B21's funnel measure), median over zones, by Z.
- *Planets round a median sun*: the parent-only rail speed sqrt(a·d) for planets whose sun has a mass between the sun p40 and p60 (10607-20254, n = 1116).

Every law shares c (calibrated so that c_sun = 304.9 at the median sun mass 14152) and the harmonic core at R = 7.32·m^0.25. The baseline (law 0) reproduces B38 (suns' children p50 2.01 against B38's 1.98).

Comparability with B21: B21 measured total |a| (not inward) with core 44.9·m^0.25, and its alternatives at p = 0.175. Here every row is inward pull at p = 0.2 with the Roche core.

| # | Law | Ratio p50 / p90: Sun · GasGiant · Planet · Planetoid · Belt | Mid-zone inward pull, Z = 1000 / 2125 / 4182 / 6846 / 10000 (u/s²) | Planets round a median sun, v p10/p50/p90 (u/s); share in 12-18 | Children left with no parent pull | HLSL cost per well per texel |
|---|---|---|---|---|---|---|
| 0 | log (today) | 2.01/2.46 · 1.62/2.21 · 1.24/1.71 · 1.11/1.43 · 1.18/1.55 | 1.59 / 1.27 / 0.91 / 0.67 / 0.51 | 17.1/17.5/18.0; 0.92 | 0 | 1 log |
| 1 | K0, λ = 0.5·D | 1.15/1.50 · 1.04/1.37 · 0.99/1.19 · 0.99/1.14 · 0.99/1.20 | 0.42 / 0.29 / 0.18 / 0.13 / 0.09 | 15.3/16.7/17.5; 0.99 | 0 | K0 approx (below) |
| 2 | K0, λ = 1·D | 1.28/1.56 · 1.14/1.46 · 1.03/1.29 · 1.01/1.21 · 1.03/1.27 | 0.73 / 0.52 / 0.32 / 0.22 / 0.16 | 16.6/17.2/17.7; 0.99 | 0 | K0 approx |
| 3 | K0, λ = 2·d_Hill | 1.25/1.54 · 1.12/1.46 · 1.02/1.27 · 1.00/1.21 · 1.02/1.25 | 0.68 / 0.48 / 0.31 / 0.22 / 0.16 | 16.6/17.2/17.7; 0.99 | 0 | K0 approx |
| 4 | cap c·L/r² beyond L = 100·m^0.25 | 1.37/1.74 · 1.21/1.64 · 1.08/1.42 · 1.04/1.32 · 1.08/1.40 | 1.06 / 0.70 / 0.39 / 0.19 / 0.11 | 16.4/17.4/18.0; 0.93 | 0 | 1 log or 1 div, branch |
| 5 | cap c·L/r², L = 300·m^0.25 | 1.50/1.84 · 1.41/1.92 · 1.18/1.64 · 1.08/1.41 · 1.13/1.51 | 1.52 / 1.09 / 0.63 / 0.38 / 0.25 | 17.1/17.5/18.0; 0.92 | 0 | same |
| 6 | force switch, exact to L, zero at 2L, L = 100·m^0.25 | 1.02/1.58 · 1.04/1.49 · 1.01/1.30 · 1.01/1.27 · 1.02/1.33 (Sun row over 1443 of 2211) | 0.78 / 0.49 / 0.27 / 0.10 / 0.01 | 16.9/17.4/18.0; 0.90 | 962 (Sun 768, GasGiant 75, Planetoid 78, Belt 33, Planet 8) | 1 log + cubic, branch |
| 7 | force switch, L = 300·m^0.25 | 1.23/1.61 · 1.28/1.80 · 1.15/1.61 · 1.08/1.40 · 1.12/1.48 | 1.50 / 1.01 / 0.48 / 0.26 / 0.16 | 17.1/17.5/18.0; 0.92 | 0 | same |
| 8 | uniform-disc subtraction (zone total c over Z) | 1.33/1.77 · 1.31/1.80 · 1.14/1.48 · 1.07/1.38 · 1.13/1.47 | 1.00 / 0.77 / 0.55 / 0.42 / 0.32 | 17.1/17.5/18.0; 0.92 | 0 | 1 log; plus one disc term per texel |
| 9 | exponent p = 0.5 (recalibrated at the median sun) | 1.24/1.40 · 1.14/1.37 · 1.05/1.24 · 1.03/1.26 · 1.05/1.30 | 0.67 / 0.87 / 0.73 / 0.61 / 0.51 | 16.5/17.5/18.8; 0.68 | 0 | 1 log |
| 10 | hyperbolic kernel on flat distances, ℓ = Z/2 | 1.98/2.50 · 1.59/2.22 · 1.21/1.69 · 1.09/1.41 · 1.15/1.51 | 1.28 / 1.03 / 0.72 / 0.54 / 0.41 | 17.0/17.4/17.9; 0.96 | 0 | 1 log + 1 exp |

**Other numbers from `probe-output.txt`:**
- *Parent-only rail speed of the suns' children* (children of root and rosette suns), p10/p50/p90 u/s: today 22.1/23.6/24.6. K0 λ = 0.5·D gives 14.3/17.3/20.5, K0 λ = 1·D gives 18.7/20.8/22.9, and cap L = 100 gives 14.5/17.8/22.2. Screening lowers the parent's own pull at the outer children of a root, so their rails slow.
- *Planetoids' parent-only speed* under K0 λ = 0.5·D: p10 9.2 against 11.5 today.
- *p = 0.5*: suns' children run 31-41 u/s, planetoids p10 6.2 u/s, and planets round all suns reach p90 37.3 u/s.
- *Force switch at L = 100·m^0.25*: 768 of the 2211 children of suns (35%) sit beyond their parent's 2L, where the parent pulls nothing, so a parent-only rail has no period (B35's "no field, no motion").
- *Planets round all suns*, v p50: 16.9-17.9 across every law except p = 0.5 (18.5). The share in 12-18 is 0.47-0.57 for every law, today included, because heavy root suns put p90 near 23.6 u/s.

**Grid equals field.** Every candidate has a closed-form potential, and `check_laws.py` confirms force = −dφ/dr to the finite-difference floor (4.3e-5 relative) for each one. The potentials (tail part, r ≥ R, with the harmonic core matched in value and slope at R as today):
- K0: −c·K0(r/λ).
- Cap: c·ln r inside L, and c·(ln L + 1 − L/r) beyond.
- Force switch: c·[s0·ln r + s1·r + s2·r²/2 + s3·r³/3] on [L, 2L], with the s_i the switch polynomial's coefficients (`laws.py: phi_switch`), and a constant beyond 2L.
- Disc: the zone term C·r²/(2Z²) inside Z.
- Hyperbolic: −c·ln coth(r/2ℓ).

Each fits the per-well reference subtraction `Gravity.cginc` already does (B22). Matching the core needs F_tail(R) per well, which is one more per-well constant in the `GravityWell` buffer. The same holds for λ or L (a `float` per well).

**K0 in HLSL.** Abramowitz & Stegun 9.8.5-9.8.6 give K0, and 9.8.7-9.8.8 give K1, as two branches:
- x ≤ 2: a 7-term polynomial in (x/2)², plus −ln(x/2)·I0(x), with I0 from A&S 9.8.1 (6 terms).
- x > 2: e^(−x)/sqrt(x) times a 7-term polynomial in 2/x.

A&S state |ε| < 1e-8 (9.8.5) and < 1.9e-7 (9.8.6). Against direct quadrature, `check_laws.py` measures 5.8e-8 relative for K0 and 2.6e-8 for K1, in float64. In float32 the shader's precision floor dominates. Per well per texel the cost is about 14 FMAs plus `log` or `exp`+`rsqrt` (the same routine is Numerical Recipes' `bessk0`/`bessk1`). Written branchless (both halves evaluated, one selected) it is about double. A 1D lookup texture in x = r/λ is the common alternative (one sample per well). The grid only needs the potential, K0; the force path needs K1.

## Confidence

- Q1: high on the math and the MD/N-body facts; medium on the exact DLMF section for Graf's theorem.
- Q2: high.
- Q3: high for KSP, Outer Wilds (schema plus developer quotes) and Universe Sandbox; medium for Mario Galaxy (community wiki marked in progress); low or unverified for Elite, CoaDE and Spore.
- Q4: high that the probe is internally consistent (the baseline reproduces B38). Medium on absolute ratios: 6 phase draws, phase-averaged circular speeds rather than integrated free orbits, and positions from the 2026-10-13 orbit probe's generator (B35: unchanged since c8d40eaf).

## Sources

- Abramowitz & Stegun, *Handbook of Mathematical Functions*, §9.6-9.8; NIST DLMF ch. 10 (§10.44 addition theorems).
- Steinbach & Brooks 1994, J. Comput. Chem. 15(7):667-683, https://api.crossref.org/works/10.1002%2FJCC.540150702 ; force switch and bump discussion: https://arxiv.org/html/2410.14187v2
- Fennell & Gezelter 2006, J. Chem. Phys. 124:234104. Allen & Tildesley, *Computer Simulation of Liquids*. Springel 2005, MNRAS 364:1105 (GADGET-2). Bagla 2002, J. Astrophys. Astron. 23:185 (TreePM). Monaghan & Lattanzio 1985, A&A 149:135.
- Outer Wilds: https://nh.outerwildsmods.com/schemas/body-schema/defs/gravityvolumeinfo ; https://pcgamer.com/how-to-build-a-black-hole ; https://thephysicsmill.com/2024/09/20/an-astrophysicist-attempts-to-measure-the-physics-of-outer-wilds/
- Super Mario Galaxy: https://lumasworkshop.com/wiki/Gravity ; https://mariowiki.com/Gravity_field
- KSP: https://forum.kerbalspaceprogram.com/topic/123592-a-clarification-of-quoton-railsquot ; https://en.wikibooks.org/wiki/Astrodynamics/Interplanetary_Trajectories
- Universe Sandbox: https://universesandbox.gamepedia.com/N-Body_Simulation
- Elite Dangerous: https://forums.frontier.co.uk/threads/are-the-orbits-of-the-planets-physically-correct.394502/post-6203566 ; https://www.pcgamer.com/uk/the-mind-bending-science-behind-the-planets-of-elite-dangerous/
- Geometry Wars grid: https://code.tutsplus.com/make-a-neon-vector-shooter-in-xna-the-warping-grid--gamedev-9904t ; https://www.gamedev.net/forums/topic/555317-fluid-grid-aka-geometry-wars-background/4569783/
