<!-- Eyes facts for campaign aetheria-gravity, follow-up aetheria-gravity:follow_up:rosette-generation-r3 (rulings rosettes-rare-and-huge, rosettes-placed-in-equilibrium, rosettes-artificial-derived-from-fields). Eyes pass, session self-2026-10-15-ag3, read at master 09ec04a8. Facts with evidence pointers; no recommendation. -->
# Rosettes as relative equilibria under the game's law

Facts for mapping rosette generation. "Computed" marks numbers from the scripts in `docs/research/rosette-relative-equilibria/` (numpy only; `rosette.py` holds the laws and the solver, `stab.py` the linear stability matrix; each `run*.py` reproduces one table). "Derived" marks algebra done for this pass, checked against direct summation. Everything else cites a source.

Notation: a body's well strength c plays the role of G·m in the equations (the acceleration a body feels from body j is c_j times a function of distance, whatever its own c). μ = c_light / c_heavy. ρ = r_light / r_heavy. ω is the common spin. "Log" means the game's tail law, pull c/s.

## 0. The game's law, read from code

- `GravityField.Potential` and `Acceleration` (Assets/Scripts/ServerShared/GravityField.cs, master 09ec04a8): a 2D point well on `float2`. Outside the core radius R the potential is c·(ln(r/R) + 1/2) and the pull is c/r; inside, c·r²/(2R²) and pull c·r/R², meeting the tail in value and slope at R. There is no cut-off at any distance.
- The core radius is the body's Roche limit, 7.32·m^0.25 (map B31, ruling core-at-roche-and-exaggeration-up); c = 45.08·m^0.2 (map B19, B35). Median sun: m 14152, c 304.9, R 79.8 u (map B19, B25 table).
- So the law is the 2D (logarithmic) one, not Newtonian 1/r, with a harmonic core. The "mass" that enters the equilibrium equations is c, and c ∝ m^0.2 compresses mass ratios: a 10:1 mass ratio is μ = 0.63; the generator's alternating proportions today, .5 to .95 of a pair's mass (`ExpandRosette`, ZoneGenerator.cs:551-592 at 09ec04a8), give mass ratios 1 to 0.053, so μ from 1 down to 0.555.
- What the generator does today: `ExpandRosette` puts all n children on one circle at phases i/n with alternating masses. Rail periods come from `Zone.PeriodAround` (Zone.cs:541-584): round a bodyless node, a member feels the strength of the node's orbits inside its distance as a point at the centre, plus half the strength of its ring partners (within 0.1 u), through `GravityField.Acceleration` with core 0.

## 1. Which regular-polygon relative equilibria exist

### Equal masses, one ring

- Newtonian: the regular n-gon of equal masses is a relative equilibrium for every n, with or without a central mass (the classic ring; Maxwell's Saturn-ring model is the 1+n case). Perko and Walter proved that for n ≥ 4 a regular polygon is a relative equilibrium only if the masses are equal ([Perko and Walter 1985, Proc. AMS 94, 301-309](https://www.ams.org/proc/1985-094-02/S0002-9939-1985-0784183-1/); also credited to Elmabsout, per [arXiv 1012.2490](https://arxiv.org/pdf/1012.2490)). Three bodies are the exception: Lagrange's equilateral triangle holds for any masses.
- Log: ω²r² = c(n−1)/2 with no centre, ω²r² = c_0 + c(n−1)/2 with a central strength c_0 ([Muscaş, Paşca and Stoica 2024, arXiv 2404.17899](https://arxiv.org/html/2404.17899v1), with the units normalised). Derived independently: on one circle every partner pulls a member inward by exactly c_j/(2r) whatever the angle (map B37), so the ring rule is exact. Consequence: in log the circular speed ωr does not depend on the ring's size, and the period is proportional to r (Newtonian: period ∝ r^1.5).
- Point vortices: a same-sign vortex system's relative equilibria satisfy the same algebraic equations as log-gravity central configurations, with circulation in place of c (derived: both read Σ_j c_j (x_i − x_j)/|x_i − x_j|² = ω²(x_i − x_centre); the vortex equation is first order in time, the gravity one second order, so existence is shared and stability is not). Thomson's vortex polygon is the equal-strength ring. Review: [Aref, Newton, Stremler, Tokieda and Vainchtein, "Vortex Crystals", Adv. Appl. Mech. 39 (2003)](https://researchdiscovery.drexel.edu/esploro/outputs/bookChapter/Vortex-Crystals/991021861867204721).

### Alternating masses (a 2n-gon of two strengths)

- On one circle, alternating masses are not an equilibrium under either law for n ≥ 2 pairs. Newtonian: Perko-Walter above. Log, derived: a heavy member feels ω_h²r² = ((n−1)c_h + n·c_l)/2 and a light member ω_l²r² = ((n−1)c_l + n·c_h)/2, equal only when μ = 1. Computed (`run3.py`): for a hexagon (n = 3 pairs) at μ = 0.7 the two species' free periods differ by 3.6%, at μ = 0.555 by 5.9%; for a 4-gon 6.1% and 10.0%.
- Two concentric n-gons twisted by π/n (heavies on one radius, lights on the other, alternating in angle): Newtonian existence for every mass ratio is proved by Barrabés and Cors (twisted (2,n)-crowns: exact counts for n = 3, 4; conjecture of exactly three for each mass ratio for n ≥ 5; [UAB record 204399](https://ddd.uab.cat/record/204399)). Yu and Zhang show that two nested regular N-gons can only be twisted by 0 or π/N, and on one circle only the regular 2N-gon with equal masses survives ([arXiv 1405.4705](https://arxiv.org/abs/1405.4705), as summarised by citing works). Aligned nested N-gons (twist 0): exactly two planar central configurations per mass ratio, one with each polygon inside (Moeckel and Simó 1995, SIAM J. Math. Anal., as cited in [arXiv 1912.00834](https://arxiv.org/pdf/1912.00834)).
- Log, twisted by π/n, closed form (derived; matches direct summation to 1e-15 in `run1.py`/`run2.py`). Using Σ over the n roots z_k of z^n = w of 1/(1 − z_k) = n/(1 − w):
  - inward pull on a heavy member at r_h: [(n−1)c_h/2 + n·c_l/(1 + ρ^n)] / r_h, plus c_0/r_h with a central strength;
  - on a light member at r_l = ρ·r_h: [(n−1)c_l/2 + n·c_h·ρ^n/(1 + ρ^n)] / r_l, plus c_0/r_l;
  - equal spin for both species gives one equation in ρ (κ = c_0/c_h): ρ²·[(n−1)/2 + nμ/(1 + ρ^n) + κ] = (n−1)μ/2 + nρ^n/(1 + ρ^n) + κ;
  - and the spin ω²r_h² = c_h·[(n−1)/2 + nμ/(1 + ρ^n) + κ].
  The left side minus the right is negative as ρ → 0 and positive as ρ → ∞, so a solution exists for every μ (derived).
- Log, how many solutions (computed, `run6.py`, κ = 0):
  - n = 2 (a rhombus of four): one solution for every μ.
  - n = 3 (hexagon): one solution for 0.0198 < μ < 1, i.e. for every mass ratio above about 3e-9. ρ = 1 at μ = 1 (the regular hexagon). For μ < 1 the lights sit outside: ρ = 1.103 (μ 0.9), 1.261 (0.7), 1.366 (0.5), 1.341 at μ = 0.555, 1.505 (0.1).
  - n ≥ 4: three solutions for every μ. For n = 4 two of them do not depend on μ: ρ² = (4 ± √7)/3 (ρ = 0.6719, 1.4884), and the third is ρ = √μ exactly (derived: the equation is linear in μ, and both coefficients vanish at ρ² = (4 ± √7)/3).
- Log, aligned (twist 0) nested n-gons: two solutions per mass ratio (computed for n = 3, 6 at μ = 1, 0.5, 0.1; `run1.py`), as in the Newtonian case.
- Spin in log is scale-free in speed: ω·r_h depends only on n, μ, κ, so a rosette scaled up keeps its member speeds and lengthens its period in proportion. Median-sun hexagon at μ = 0.7: v_heavy 22.8 u/s, period 276 s at r_h = 1000 u, 828 s at 3000 u (computed, `run3.py`).

### The soft core

- The core only enters when two members are closer than a member's core radius (about 80 u for a median sun). Computed (`run3.py`, median sun c 304.9, R 79.8, lights with R scaled by μ^1.25): at r_h ≥ 100 u the soft-core solution equals the pure log one to the printed digits; at r_h = 60 u the hexagon solution moves (μ = 0.7: ρ 0.710 instead of 1.261).
- When all members sit inside each other's cores every pull is linear in offset, and every configuration rotates rigidly: the solver finds a continuum of "solutions" at r_h = 30 u (computed). This is the degenerate harmonic case, not a family of real equilibria worth choosing between.
- With unequal core radii the pair forces are not reciprocal inside a core (c_i·f(s; R_j) ≠ c_j·f(s; R_i) once s < R), so inside the cores the system is not a momentum-conserving N-body problem; the symmetric polygon still fixes the centre by symmetry (derived).

### Klemperer and the operator's recollection

- Klemperer's rosettes ([Klemperer 1962, AJ 67, 162-167](https://en.wikipedia.org/wiki/Klemperer_rosette), as summarised there) allow alternating heavy and light bodies only for even counts; the hexagon of two kinds is "the nearest to being stable", its bodies in each other's triangular points L4 and L5, not L2. Klemperer and later linear analysis and simulation (Jenkins, cited by Wikipedia) find all Newtonian rosettes unstable.
- Computed (`run2.py`): every Newtonian twisted alternating 2n-gon tested (2n = 4 to 14, μ = 1 to 0.1, all roots) has a growing mode. Under the log law the twisted alternating hexagon is spectrally stable for every μ tested (1 to 0.1), and so is the rhombus (2n = 4); see section 2.

## 2. Linear stability, and whether it matters on rails

- Newtonian ring without a centre: spectrally unstable for every n ≥ 3 (Moeckel; Roberts gives a short proof for n ≥ 7; as cited in [arXiv 2510.26211](https://arxiv.org/html/2510.26211v1)). Computed (`run2.py`): growth rates 0.54 to 3.8 in units where c = r = 1, against ω² of order 1, so the growth time is comparable to a rotation.
- Newtonian 1+n (Maxwell's ring): linearly stable for a large enough central mass only when n ≥ 7 (Moeckel 1994, J. Dyn. Diff. Eq. 6, 37-51); Roberts 2000 gives the threshold, growing as n³; a poster attributes ≥ 0.435·n³ ring masses, not checked against the paper ([UT Austin poster](https://www.ices.utexas.edu/~gopal/talks/Nbody_poster.pdf); [arXiv 1903.10270](https://arxiv.org/pdf/1903.10270)).
- Log ring without a centre: linearly stable iff n = 2 to 6 (Muscaş, Paşca and Stoica 2024, Theorem 5, spectral sense). Computed: the spectrum is on the imaginary axis to 5e-8 for n ≤ 7 and unstable for n ≥ 8, so n = 7 is a borderline case the paper counts unstable.
- Log 1+n with a central strength M and ring strength μM each (same paper, Theorem 4): n = 2 unstable; n = 3 stable iff μ = 1; n = 4 to 9 stable iff 4/(n−1)² ≤ μ < 1; n ≥ 10 even, 4/(n−1)² ≤ μ ≤ 16/(n² − 8n + 8); n ≥ 11 odd, upper bound 16/((n−1)(n−7)). So in log the central body must be neither too heavy nor too light. The solver here reproduces the bounds at n = 7 and n = 12 (`run2.py`), which is its validation.
- Point vortices: Thomson's polygon is linearly stable for n < 7, unstable for n > 7 (Thomson 1883; Havelock 1931); n = 7 is linearly neutral and was proved stable nonlinearly by Kurakin and Yudovich (Chaos 12, 574-595, 2002; as cited in [Kurakin's RCD papers](https://rcd.ics.org.ru/S1560354710010028/)). Same existence equations as log gravity, different dynamics, so these thresholds are related but not the same as the gravity ones above.
- Lagrange triangle under a 1/r^a potential: spectrally stable only for a < 2 and Σm_im_j/(Σm)² < (1/3)((2−a)/(2+a))² (Routh; [Santoprete 2009, arXiv 0909.4907](https://arxiv.org/pdf/0909.4907)). At a = 1 this is the 1/27 Gascheau-Routh bound; as a → 0 (the log limit) the bound tends to 1/3, which only equal masses reach, so in that limit every unequal triangle and every restricted L4/L5 (one mass → 0, left side ≤ 1/4) is stable. This is a limit of the power family, not a theorem about the log law itself.
- Twisted alternating 2n-gon, log, computed (`run2.py`, all roots): 2n = 4 and 6 stable for every μ tested; 2n = 8 to 14 unstable except some roots at small μ (2n = 8, outer-light root, μ ≤ 0.3; 2n = 10, μ = 0.1). μ ≤ 0.3 means mass ratios below 0.0024, outside the generator's range.
- On rails: rail positions are set from the period and phase alone (`Zone.GetOrbitPosition`, Zone.cs:513-515), so no perturbation can grow; the dynamical stability of the members does not reach play (ruling rosettes-placed-in-equilibrium records this). What does reach play is (a) whether each species' rail period equals the equilibrium spin, or the polygon shears; (b) what free-flying ships near the rosette feel, which is the summed field and does not depend on the members' stability.
- Fact for (a): today's `PeriodAround` ring rule does not give the twisted equilibrium spin. Computed for the log hexagon at μ = 0.7 (ρ = 1.261): the rule gives heavies ω²r_h² = c_h (no inner strength, half of two partners) and lights ω²r_l² = 3c_h + 0.7c_h (all heavies inside, as a point, plus half of two partners), where the equilibrium has 1.698·c_h/r_h² for both. Heavies would run at 0.77 of the equilibrium spin and lights at 1.17. The rule's Gauss-law premise is a phase average; at a fixed π/n twist the inner ring pulls an outer member by n·c·ρ^n/(1+ρ^n)/r, not n·c/r, and the outer ring pulls an inner member by n·c/(1+ρ^n)/r, not zero (derived). On one circle with μ = 1 the rule is exact.

## 3. Computing the spin and the radius ratio

- Rotating frame: each member i must satisfy a_i + ω²·x_i = 0, where a_i is the summed acceleration from the others (and any centre). For a dihedral polygon the tangential parts cancel by symmetry, so only radial balance is left: one equation per species. One species: ω² = (inward pull)/r, closed form above. Two species: eliminate ω² between the two radial equations to get one scalar equation in ρ, solve it, then read ω from either.
- Closed forms: log one ring (with or without a centre) and log twisted two-ring (the ρ equation above, solved by bisection; for n = 4 the three roots in closed form). Newtonian equal ring: ω²r³ = c_0 + (c/4)·Σ_{k=1}^{n−1} csc(πk/n) (standard Maxwell-ring result; matches `run1.py`). Newtonian twisted two-ring: no closed form; the sums are elementary but the ρ equation is solved numerically (Barrabés and Cors reduce it to one equation in r and m).
- With the soft core: the same two radial equations hold, with GravityField's pull in place of c/s for every pair inside a core; there is no closed form, and bisection on ρ works (`rosette.py`, `roots('soft', ...)`). Root-finding needs care: the residual has several roots for n ≥ 4 and a continuum when all members are inside each other's cores.
- Rails then take T = 2π/ω for every member, the same for both species, which is what the follow-up asks ("rail period equals the equilibrium spin").
- The family strength (moons orbiting with a member, `Zone.Strength`) is what the far field sees of a member; whether the equilibrium uses a member's own c or its family's is a modelling choice not settled by any source here.

## 4. Rotating-frame saddles and the drawn grid

- The effective potential in the frame rotating with the rosette is Φ_eff = Σ c_j φ(|x − x_j|) − ½ω²|x|². Its stationary points are the Lagrange-like points; in the restricted three-body problem L1-L3 are saddles of Φ_eff and L4, L5 are maxima, stable only through the Coriolis force. Standard plots of Lagrange points are contour maps of Φ_eff in the rotating frame, not of the gravitational potential ([Wikimedia, Lagrange points.svg](https://commons.wikipedia.com/wiki/File:Lagrange_points.svg); [Athanassoula, barred-galaxy dynamics, astro-ph/0608665](https://arxiv.org/pdf/astro-ph/0608665), which calls L4/L5 maxima of the effective potential; [Berkeley course page](https://w.astro.berkeley.edu/~charada/proj-lagrange.html)).
- Computed for the log hexagon (`run4.py`, `run5.py`, r_h = 1):
  - μ = 1: Φ_eff has 19 stationary points: a maximum at the centre; 6 saddles at r 0.887 between neighbours; 6 maxima at r 1.48 outside each edge; 6 saddles at r 1.60 straight outside each body (L2-like).
  - μ = 0.7: the same pattern, moved: 6 saddles at r 1.001, 6 maxima at r 1.654, saddles outside the heavies at r 1.752 and outside the lights at r 1.832.
  - Each member sits on a stationary point of the others' Φ_eff, as the definition requires (|∇| ~ 1e-15). At every member tested it is a local maximum (both Hessian eigenvalues negative), not a saddle, the same kind of point as L4/L5.
- The non-rotating potential (what the grid draws, h = Σφ/K, map B22) has none of these points. Computed: the equal hexagon's inertial potential has no stationary point off the centre (the centre is a degenerate flat point); at μ = 0.7 it has 3 saddles at r 0.916 on the lights' bearings, inside the ring. At each member the others' inertial pull is exactly the centripetal ω²r (2.5 at μ = 1), so in the drawn grid a member sits on a slope, not a flat point.
- The drawn picture of a rotating Φ_eff is static only in the rotating frame; in the inertial frame the summed wells turn with the rosette. Game prior art presenting rotating-frame effective potential to players was not found in this pass; the sources found are scientific and teaching visualisations.

## 5. What the 2D log law changes against Newtonian 1/r

- Existence: the same families exist (one ring of equal masses; twisted two-ring for every mass ratio; aligned two-ring with two solutions per ratio), with closed forms in log where Newtonian sums need csc series or numerics. Alternating masses on one circle fail in both.
- Counts differ in detail: Newtonian twisted hexagon at μ = 1 has three solutions (ρ 0.928, 1, 1.077; `run1.py`) and log has one; for n ≥ 4 both have three.
- Stability differs a great deal: Newtonian rings and rosettes without a dominant centre are all unstable; in log the equal ring is stable for n ≤ 6, the twisted alternating rhombus and hexagon are stable for all μ tested, and the 1+n ring needs a centre neither too heavy nor too light (Muscaş et al.). The operator's recollection, "only one dynamically stable config ... a hex with alternating body masses placed in each other's L2", is closest to Klemperer's Newtonian hexagon, which Klemperer called the nearest to stable (L4/L5, still unstable); under the game's law the alternating hexagon is computed stable, and it is not the only one.
- Scaling: log speed is independent of size and period ∝ size; Newtonian speed ∝ size^−1/2 and period ∝ size^1.5.
- c ∝ m^0.2 squeezes mass ratios into μ ≥ 0.555 for today's generator, a range where every log solution above is unique for n ≤ 3.

## Confidence

- 1, existence and closed forms: high for the log closed forms (derived and checked by direct sums) and the Perko-Walter and ring results (primary or well-cited); medium for the Newtonian nested-polygon counts (read from abstracts and citing papers, not the full texts).
- 2, stability: high for the log ring and 1+n thresholds (Muscaş et al., reproduced numerically); medium-high for the log twisted hexagon and rhombus being spectrally stable (computed only, at six μ values each, no source); medium for the Newtonian Roberts constant (unverified against the paper).
- 3, method: high.
- 4, saddles: high for the stationary-point tables (computed, two configurations); low for game prior art (none found).
- 5, law comparison: high.
