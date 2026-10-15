# Smooth wells: C-infinity laws for the grid and the rails

Eyes pass `eyes-ag3-smooth-wells`, campaign aetheria-gravity, session self-2026-10-15-ag3, 2026-10-15, read at master 39ebf7f6. Facts with evidence pointers; no recommendation. Extends `gravity-tail-taming.md` (same data, metrics, phases and table format) under question `aetheria-gravity:question:tail-taming-law` and ruling `aetheria-gravity:ruling:tame-tails-parent-only-rails`.

Operator, verbatim: "Since both orbits and well shape are negotiable, is there a solution available that's more mathematically elegant? Having all the derivatives shake out nicely isn't just an affectation, the player can feel higher-order discontinuities especially in a game where moving with the grid is such a huge part of the experience."

Scripts in `gravity-smooth-wells/`:
- `laws2.py`: the laws (force and potential). It imports c, the core radius, today's law and the A&S K0/K1 from `../gravity-tail-taming/laws.py`.
- `check_laws2.py`: force = dφ/dr for each law, the shader form of Ein against mpmath, and circular-speed shapes. Output: `check-output.txt` (first lines) and the text below.
- `seams.py`: jumps in φ…φ''' at each piecewise seam, with each branch differentiated analytically in mpmath at 50 digits. Output: `seams-output.txt`.
- `probe2.py`: `../gravity-tail-taming/probe.py` with whole-law pulls. Output: `probe2-output.txt`.

Every law has c = 45.08-calibrated strength (c_sun = 304.9 at the median sun) and ε = today's Roche core radius 7.32·m^0.25. Law 0 of `probe2.py` reproduces the tail-taming baseline exactly (Sun ratio p50 2.01, mid-zone 1.59 … 0.51). Each law ran in 9-16 s on Starfire, one process per law.

## 1. Smoothness class

A radial potential φ(r) is C^∞ on the plane exactly when it is a smooth function of r². Each law below is a function of r² with no seam, so it is real-analytic everywhere, the core included. The A&S polynomial forms are piecewise, so "the HLSL form" is a separate column.

| Law | φ on the plane (exact) | HLSL/C# form | Seam location |
|---|---|---|---|
| Today: harmonic core + log | C^1 | C^1 | r = R. φ'' jumps by 2c/R² (inside +c/R², outside −c/R²); φ''' jumps by 2c/R³ |
| Tail-taming K0 rows (harmonic core + K0) | C^1 (core seam) | C^1 at R, plus the A&S seam | r = R, and r = 2λ |
| Tail-taming cap | C^1 at R and at L (φ'' jumps by c/L² at L) | same | |
| Tail-taming force switch | C^1 at R; C^2 at L and 2L | same | |
| (a) log-ratio | C^ω | C^∞: elementary (log, div), no branch | none |
| (b) screened Plummer | C^ω | C^0 in the formula: A&S piecewise | √(r²+ε²) = 2λ |
| (c) Lamb-Oseen, with or without halo | C^ω (Ein is entire) | φ: A&S E1 is piecewise. Force c(1−e^(−r²/ε²))/r is elementary and C^∞ | φ seam at r = ε (u = r²/ε² = 1) |
| (d) Plummer log | C^ω | C^∞: one log, no branch | none |
| (e) conserving halo | C^ω | C^∞: two logs, no branch | none |

**Measured seam jumps** (`seams-output.txt`; jump = high branch − low branch; "rel" = |jump| / |true derivative|):

| Seam | φ | φ' | φ'' | φ''' |
|---|---|---|---|---|
| A&S K0 at x = 2, in K0^(n) (multiply by −c/λ^n) | 7.6e-9 (rel 6.7e-8) | 2.5e-7 (rel 1.8e-6) | 8.4e-7 (rel 4.6e-6) | 7.7e-5 (rel 2.9e-4) |
| A&S K1 at x = 2 (force path), in K1^(n) (multiply by c/λ^(n+1)) | 1.5e-9 (rel 1.1e-8) | 1.9e-7 (rel 1.0e-6) | 5.1e-6 (rel 1.9e-5) | 4.6e-5 (rel 1.0e-4) |
| A&S E1 → Lamb-Oseen φ at r = ε, units c/ε^n | 2.2e-9 (rel 5.5e-9) | 2.2e-6 (rel 3.5e-6) | 1.3e-4 (rel 1.2e-3) | 5.1e-3 (rel 5.4e-3) |
| Today's core at r = R, units c/R^n | 0 | 0 | 2.0 (rel 2.0) | 2.0 (rel 1.0) |

On the same units (c/ε², with ε = R), the Lamb-Oseen shader seam's φ'' jump is 6.4e-5 of today's. The K0 seam's φ'' jump is 8.4e-7·c/λ². With λ ≥ 0.5·D ≫ R, that is below 1e-6 of today's 2c/R². The K0 value jump at the seam (6.7e-8 relative) is about one float32 ulp of K0(2) = 0.114 (float32 ε = 6e-8).

## 2. Probe (same measures as `gravity-tail-taming.md` Question 4; 6 draws, rng seed 7)

**Family (e), conserving halo** (added on the coordinator's brief, after the operator's observation, verbatim: "this is all a consequence of exaggerating the contributions of small bodies... a well getting deeper the more its bodies are split by the zone generator. Two systems, same mass, one of them spent it on planets, that one goes waaaay deeper."):
- φ_i = (c_near/2)·ln(r²+ε²) − ((c_near − c_far)/2)·ln(r²+L²).
- c_near is today's compressed 45.08-calibrated m_i^0.2 strength.
- c_far,i = c(M_F)·m_i/M_F, where F is the body's parent family (the parent and all its descendants; for the root, the whole zone) and only non-belt, non-empty bodies carry mass.
- L = k·ext, where ext is the body's own family extent: the longest summed-distance path from the body to any descendant. A body with no descendants falls back to D (Z for a root).
- c_far/c_near = (m_i/M_F)^0.8 ≤ 1, so the halo strength is never negative.

**Far-field measure** (every law): the inward pull at 8 points on a circle of radius 2Z (and 10Z) round the zone centre, times the radius, averaged over the 6 draws. It is divided by c(M_zone), the strength of the zone's body mass merged into one body under the same calibration.

"outer" = the body's outermost direct-child distance (any child type, belts and empties included); a body with no children falls back to D (Z for a root). The Sun class in "Ratio" and in the rail-speed line is **Sun-class children, i.e. suns orbiting a sun** (`probe.py` keys by the child's class). `gravity-tail-taming.md` labels the same numbers "children of root and rosette suns", and that label does not match its code.

| # | Law | Ratio p50 / p90: Sun · GasGiant · Planet · Planetoid · Belt | Mid-zone inward pull, Z = 1000 / 2125 / 4182 / 6846 / 10000 (u/s²) | Planets round a median sun, v p10/p50/p90 (u/s); share in 12-18 | Sun-class children's parent-only rail v p10/p50/p90 | Children with no parent pull | Children with parent pull < 0.5·c/D | Zone far-field c / c(M_zone), p50/p90 at 2Z; at 10Z |
|---|---|---|---|---|---|---|---|---|
| 0 | today (harmonic core + log) | 2.01/2.46 · 1.62/2.21 · 1.24/1.71 · 1.11/1.43 · 1.18/1.55 | 1.59 / 1.27 / 0.91 / 0.67 / 0.51 | 17.1/17.5/18.0; 0.92 | 22.1/23.6/24.6 | 0 | 46 | 5.54/7.98; 5.54/7.98 |
| d | Plummer log, no taming | 2.01/2.46 · 1.63/2.22 · 1.25/1.72 · 1.13/1.44 · 1.19/1.55 | 1.57 / 1.26 / 0.91 / 0.67 / 0.51 | 16.1/17.2/17.8; 0.99 | 22.0/23.6/24.6 | 0 | 234 | 5.54/7.98; 5.54/7.98 |
| a1 | log-ratio L = 0.5·D | 1.17/1.57 · 1.04/1.37 · 0.99/1.19 · 0.99/1.16 · 0.99/1.24 | 0.34 / 0.24 / 0.15 / 0.11 / 0.08 | 14.4/16.3/17.1; 0.96 | 12.3/15.4/19.5 | 18 (L ≤ ε for 285 bodies: repulsive) | 5838 | 0.10/0.11; 0.00/0.00 |
| a2 | log-ratio L = 1·D | 1.26/1.55 · 1.12/1.43 · 1.02/1.26 · 1.01/1.20 · 1.02/1.26 | 0.64 / 0.45 / 0.29 / 0.20 / 0.15 | 15.5/16.9/17.5; 1.00 | 17.7/19.9/22.7 | 0 (L ≤ ε for 34 bodies) | 407 | 0.36/0.40; 0.02/0.02 |
| a3 | log-ratio L = 1.5·outer | 1.17/1.41 · 1.12/1.39 · 1.05/1.31 · 1.03/1.27 · 1.05/1.32 | 0.74 / 0.46 / 0.27 / 0.18 / 0.14 | 14.1/15.2/16.7; 1.00 | 18.4/20.6/23.1 | 0 (7 bodies) | 637 | 0.34/0.48; 0.02/0.02 |
| a4 | log-ratio L = 2·outer | 1.22/1.46 · 1.16/1.44 · 1.07/1.35 · 1.04/1.28 · 1.08/1.35 | 0.85 / 0.54 / 0.32 / 0.22 / 0.16 | 15.1/15.9/17.0; 1.00 | 19.8/22.0/23.7 | 0 (7 bodies) | 326 | 0.52/0.72; 0.03/0.04 |
| b1 | screened Plummer λ = 0.5·D | 1.16/1.49 · 1.04/1.34 · 0.99/1.19 · 0.99/1.16 · 1.00/1.22 | 0.41 / 0.29 / 0.18 / 0.13 / 0.09 | 14.7/16.3/17.0; 0.98 | 14.3/17.2/20.5 | 0 | 3610 | 0.07/0.08; 0.00/0.00 |
| b2 | screened Plummer λ = 1·D | 1.28/1.56 · 1.15/1.46 · 1.04/1.29 · 1.02/1.22 · 1.04/1.28 | 0.70 / 0.50 / 0.32 / 0.23 / 0.16 | 15.6/16.8/17.5; 1.00 | 18.7/20.8/22.9 | 0 | 297 | 0.45/0.50; 0.00/0.00 |
| c0 | Lamb-Oseen core, log tail | 2.01/2.46 · 1.62/2.21 · 1.24/1.71 · 1.11/1.43 · 1.18/1.54 | 1.59 / 1.27 / 0.91 / 0.67 / 0.51 | 17.0/17.4/18.0; 0.93 | 22.1/23.6/24.6 | 0 | 121 | 5.54/7.98; 5.54/7.98 |
| c1 | Lamb-Oseen + halo L = 0.5·D | 1.17/1.57 · 1.04/1.37 · 0.99/1.19 · 0.99/1.15 · 0.99/1.22 | 0.35 / 0.24 / 0.16 / 0.11 / 0.08 | 14.8/16.6/17.5; 0.97 | 12.3/15.4/19.6 | 1 | 4944 | 0.10/0.12; 0.00/0.00 |
| c2 | Lamb-Oseen + halo L = 1·D | 1.26/1.55 · 1.12/1.44 · 1.02/1.26 · 1.00/1.19 · 1.02/1.26 | 0.66 / 0.46 / 0.29 / 0.21 / 0.15 | 16.5/17.2/17.8; 0.99 | 17.8/20.0/22.8 | 0 | 146 | 0.36/0.40; 0.02/0.02 |
| c3 | Lamb-Oseen + halo L = 1.5·outer | 1.17/1.41 · 1.11/1.39 · 1.04/1.31 · 1.02/1.26 · 1.05/1.32 | 0.75 / 0.46 / 0.28 / 0.18 / 0.14 | 14.3/15.8/17.2; 1.00 | 18.4/20.6/23.1 | 0 | 136 | 0.34/0.48; 0.02/0.02 |
| c4 | Lamb-Oseen + halo L = 2·outer | 1.22/1.46 · 1.15/1.44 · 1.06/1.34 · 1.04/1.27 · 1.07/1.34 | 0.87 / 0.54 / 0.32 / 0.22 / 0.16 | 15.4/16.4/17.5; 1.00 | 19.8/22.0/23.7 | 0 | 122 | 0.52/0.73; 0.03/0.04 |
| e1 | conserving halo L = 1.5·ext | 1.25/1.50 · 1.17/1.44 · 1.06/1.33 · 1.04/1.28 · 1.07/1.34 | 0.86 / 0.58 / 0.37 / 0.25 / 0.19 | 14.5/15.9/16.9; 1.00 | 21.7/23.4/24.5 | 0 (7 bodies) | 431 | 1.42/1.99; 1.28/1.84 |
| e2 | conserving halo L = 2·ext | 1.30/1.55 · 1.20/1.49 · 1.09/1.37 · 1.05/1.30 · 1.09/1.36 | 0.95 / 0.64 / 0.40 / 0.27 / 0.21 | 15.3/16.3/17.1; 1.00 | 21.8/23.5/24.5 | 0 (7 bodies) | 270 | 1.52/2.07; 1.28/1.85 |

Notes from `probe2-output.txt`:
- *Core alone (rows d, c0 against 0)*: replacing the harmonic core with a C^∞ core moves no ratio or mid-zone figure by more than 0.02. It lowers rail speeds for children near their parent's core. Planetoids' p10 drops 11.5 → 11.0 (Plummer) and 11.4 (Lamb-Oseen). The count of children with a parent pull below half the log value rises from 46 to 234 (Plummer) and 121 (Lamb-Oseen).
- *Screening with the smooth core (b) against the tail-taming K0 rows (harmonic core)*: λ = 0.5·D ratio p50s 1.16/1.04/0.99/0.99/1.00, against 1.15/1.04/0.99/0.99/0.99. λ = 1·D gives 1.28/1.15/1.04/1.02/1.04, against 1.28/1.14/1.03/1.01/1.03. The mid-zone figures differ by ≤ 0.03. The median-sun planets go from 16.7 to 16.3 (0.5·D) and from 17.2 to 16.8 (1·D).
- *L = 0.5·D*: L ≤ ε for 285 of 27,165 bodies, where the log-ratio force c·r·(L²−ε²)/(…) is repulsive. 18 children then have no inward parent pull, and the ratio n drops by 2-12 per class.
- *Split-deepening (far-field column)*:
  - *Today, Plummer and Lamb-Oseen*: the zone's far field is Σc_i = 5.54× (p50) and 7.98× (p90) the merged body's c(M_zone), the same at 2Z and 10Z. This is the n^(1−p) effect the operator names.
  - *Screened and halo laws (a, b, c')*: at 2Z the zone's far field is 0.07-0.52× c(M_zone), and at 10Z ≤ 0.04×. Their far field is neutral by construction.
  - *(e)*: 1.42×/1.99× at 2Z and 1.28×/1.84× at 10Z (p50/p90), not 1.0. The static sum Σc_far,i / c(M_zone) over each zone gives the same 1.02/1.27/1.84 (p10/50/90; one root in each of 1500 zones). The excess comes from nesting. A moon's c_far is its share of its planet family's compressed c(M_F), and the compressed strength per unit mass is higher for a smaller family. So the shares sum to c(M_F) only within one family level, not across the zone. If every body instead took its share of the zone total, c(M_zone)·m_i/M_zone, the shares would sum to exactly c(M_zone).
- *(e) rails*:
  - It keeps today's Sun-class rails (21.7/23.4/24.5 against 22.1/23.6/24.6), because c_near governs inside L.
  - It cuts the Sun-class ratio p50 from 2.01 to 1.25 (1.5·ext) or 1.30 (2·ext), and the mid-zone pull from 1.59 to 0.86-0.95 at Z = 1000.
  - Planets round a median sun: 15.9 and 16.3 p50, all in 12-18.
- *Planets round all suns*, share in 12-18 u/s: 0.51 today, 0.53-0.64 for the smooth laws (highest: log-ratio 1.5·outer at 0.64).

## 3. Closed forms, circular speed and shader cost

Pull F is toward the body; v² = r·F; s = √(r²+ε²); u = r²/ε². Cost is per well per texel. The force vector is F·Δ/r, with Δ the offset to the well, so "no sqrt" below means the vector form needs no square root.

| Law | φ | F | v²(r) | Plateau v ≈ √c | φ cost (grid) | Force-vector cost |
|---|---|---|---|---|---|---|
| Today | c r²/2R² (r<R); c(ln(r/R)+½) | c r/R² ; c/r | c r²/R² ; c | exactly √c for all r ≥ R | 1 log (of r²), 1 select | 1 div, 1 select, no sqrt |
| (d) Plummer | (c/2)·ln(r²+ε²) | c r/(r²+ε²) | c r²/(r²+ε²) | v ≥ 0.9√c from 2.07ε, rising to √c at infinity; 0.71√c at ε | 1 log | 1 div, no sqrt |
| (a) log-ratio | (c/2)·ln[(r²+ε²)/(r²+L²)] | c r(L²−ε²)/[(r²+ε²)(r²+L²)] | c r²(L²−ε²)/[(r²+ε²)(r²+L²)] | peak v² = c(L−ε)/(L+ε) at r = √(εL); v ≥ 0.9√c requires L ≥ 9.5ε. Median sun (ε = 79.8): L = 1000 gives a peak of 0.923√c, ≥0.9 on 185-431; L = 4000 gives a peak of 0.980√c, ≥0.9 on 166-1926 | 1 log + 1 div (or 2 logs) | 1 div (2 FMA products), no sqrt |
| (b) screened Plummer | −c·K0(s/λ) | (c/λ)·K1(s/λ)·r/s | (c r²/(λ s))·K1(s/λ) | peak 0.923√c at r = 300 for λ = 1000 (median sun); ≥0.9 on 190-485 | 1 sqrt + A&S K0: low branch 1 log + ~13 FMA, high branch 1 exp + 1 rsqrt + 1 div + 7 FMA; branchless does both | 1 sqrt + A&S K1 (same shape) + 1 div |
| (c) Lamb-Oseen | (c/2)·Ein(u), Ein(u) = E1(u)+ln u+γ | c(1−e^(−u))/r | c(1−e^(−u)) | v ≥ 0.9√c from 1.29ε; 0.80√c at ε, 0.99√c at 2ε | A&S: low branch a degree-5 polynomial in u (the ln cancels); high branch 1 exp + 2 div + 1 log; branchless does both | 1 exp + 1 div, no sqrt, no branch |
| (c') Lamb-Oseen + halo | (c/2)·[Ein(u) − ln(r²+L²)] | c(1−e^(−u))/r − c r/(r²+L²) | c[(1−e^(−u)) − r²/(r²+L²)] | median sun, L = 1000: peak 0.981√c at r = 181; ≥0.9 on 105-484 | as (c) + 1 log | 1 exp + 2 div, no sqrt |
| (e) conserving halo | (c_n/2)·ln(r²+ε²) − ((c_n−c_f)/2)·ln(r²+L²) | c_n r/(r²+ε²) − (c_n−c_f) r/(r²+L²) | c_n r²/(r²+ε²) − (c_n−c_f) r²/(r²+L²) | two plateaus: v → √c_n for ε ≪ r ≪ L and v → √c_f for r ≫ L; the halo term removes (c_n−c_f)/2 of v² at r = L | 2 logs | 1 div (single fraction over (r²+ε²)(r²+L²)), no sqrt; two constants per well (c_f, L) beyond today's |

Further facts:
- *Peak pull* (units c/ε): today 1.0 (at R), Lamb-Oseen 0.638 (at r ≈ 1.12ε), Plummer 0.5 (at r = ε).
- *Far fields*: (e) keeps a log tail of strength c_far, F → c_f/r. (a) and (c') fall as φ ≈ −(c/2)(L²−ε²)/r², F ≈ c(L²−ε²)/r³, which is algebraic. (b) falls as e^(−r/λ)/√r. (c) and (d) keep the log tail.
- *Sources (2D Laplacian of φ)*: (d) 2cε²/(r²+ε²)², the surface density of a projected Plummer sphere. (c) a Gaussian, (2c/ε²)·e^(−u). (a) the same Plummer core minus a Plummer halo of width L with equal strength, so the net source is zero ("neutral"). (c') a Gaussian core minus the same halo.
- *Gauss's law*: (a), (b), (c') and (e) are not harmonic outside the core: the halo (a, c') or screening (b) is spread over all r. A ring of siblings therefore no longer acts as its centre. (c) and (d) are harmonic to within e^(−u) and ε²/r² respectively, so the enclosed-strength funnel stays: rows c0 and d equal row 0.
- *Check*: `check_laws2.py` shows max |F − dφ/dr| / max F of 1.9e-7 (Plummer, log-ratio, conserving halo), 1.2e-5 (screened Plummer, through the A&S pair), 5e-6 (Lamb-Oseen, through A&S E1), and 1.1e-4 for today (the finite difference straddles the kink at R). The shader form of Ein agrees with mpmath to 2.3e-7 absolute over u from 1e-4 to 100.

## 4. Literature

- **Plummer softening in 2D.** The potential (c/2)·ln(r²+ε²) is the log potential of a projected Plummer sphere, Σ ∝ ε²/(r²+ε²)² (Plummer 1911, MNRAS 71:460; Binney & Tremaine, *Galactic Dynamics*, §2.2). Krasny 1986 (J. Comput. Phys. 65:292) regularised 2D point vortices by adding δ² to the Biot-Savart denominator, which gives the kernel r/(r²+δ²) of row d (the "vortex blob" kernel).
- **Kuzmin disc.** Kuzmin 1956 (Astron. Zh. 33:27) and Toomre 1963 (ApJ 138:385): a razor-thin disc with Σ ∝ a/(R²+a²)^(3/2), whose in-plane potential under 3D gravity is −GM/√(R²+a²). It is the 3D-gravity counterpart of Plummer softening, not a 2D log law. It shares the r²+a² form but not the kernel.
- **Lamb-Oseen vortex.** Oseen 1912 and Lamb, *Hydrodynamics* (6th ed. 1932, §334a): a Gaussian vorticity core with swirl velocity (Γ/2πr)(1−e^(−r²/r_c²)). This is row c's force law, and Gaussian blobs are a standard choice in vortex methods. Beale & Majda 1985 (J. Comput. Phys. 58:188, DOI 10.1016/0021-9991(85)90176-7) build smooth explicit 2D/3D velocity kernels of arbitrary order. I did not confirm from the abstract whether they compare against Gaussian blobs.
- **Charge-neutral smoothing.** Ewald 1921 (Ann. Phys. 369:253) adds to each point charge an opposite Gaussian cloud of equal charge, so the real-space sum (erfc kernel) is short-ranged. In 2D, a point log minus an equal Gaussian is −(c/2)·E1(r²/σ²), which is row c' with a Gaussian halo in place of the Plummer halo. Wolf et al. 1999 (J. Chem. Phys. 110:8254) neutralise each cutoff sphere for a pairwise 1/r sum. Debye & Hückel 1923 (Phys. Z. 24:185): a mobile neutralising cloud screens each charge. In 2D the screened Green's function is K0, which is row b.
- **Dehnen 2001** (MNRAS 324:273, astro-ph/0011568): Plummer softening gives significantly larger force errors than kernels of finite extent. "Compensating" kernels (e.g. his K1) reduce the error further by exceeding the Newtonian force near the softening length, which offsets the too-small force inside. None of the laws here overshoot: (d) and (c) approach c/r from below, and (a), (b), (c') and (e) stay below c_near/r at all r. Dehnen's error measure is a 3D N-body sampling bias and is not this probe's metric.

## Confidence

- §1: high. The seam jumps are branch derivatives at 50 digits, and the smoothness classes follow from analyticity in r².
- §2: high on internal consistency (law 0 reproduces the tail-taming baseline digit for digit). Medium on absolute ratios, for the same caveats as `gravity-tail-taming.md` (6 phase draws, phase-averaged circular speeds, generator positions). "outer" is my reading of "outermost child distance of the body's family" (direct children only).
- §3: high on formulas and checks. Op counts are by inspection, not compiled HLSL.
- §4: high for Plummer, Lamb-Oseen, Ewald, Debye-Hückel, Krasny 1986 and the Dehnen 2001 abstract claims. Medium on the Kuzmin 1956 and Lamb section citations and on the Wolf 1999 page. Dehnen's kernel definitions were not read beyond the abstract.

## Sources

- Abramowitz & Stegun §5.1.53-5.1.54 (E1), §9.8.1-9.8.8 (I0, I1, K0, K1).
- Dehnen 2001: https://arxiv.org/abs/astro-ph/0011568 ; its K1 kernel's use downstream: https://arxiv.org/pdf/1310.4502
- Beale & Majda 1985: https://scholars.duke.edu/publication/759540 ; Beale & Majda 1982, Math. Comp. 39:29: https://www.ams.org/mcom/1982-39-159/S0025-5718-1982-0658213-7/
- Krasny 1986: https://dept.math.lsa.umich.edu/~krasny/paper_jcp_1986.pdf
- Plummer 1911; Kuzmin 1956; Toomre 1963; Lamb 1932; Ewald 1921; Wolf et al. 1999; Debye & Hückel 1923, as cited above.
