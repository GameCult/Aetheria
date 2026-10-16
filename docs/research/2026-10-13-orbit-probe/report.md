<!-- Research for operator question 2026-10-13: do Aetheria orbits match the gravity terrain; potential/Laplacian reading. Probe imagination-aeth-orbit-probe, session self-2026-10-13-ag2, at master 0898725f. -->
# Orbit probe: does following an orbit cost effort, and what do the wells say?

Imagination probe `imagination-aeth-orbit-probe`, 2026-10-13. A measurement, not a cut: nothing admitted, nothing pushed. Source tree: GameCult/Aetheria origin/master e99cfbed, in a detached worktree (since removed). Real code ran on Yggdrasil (stopgap, dotnet image, one scratch xunit test, job log `run2.log`, 1 passed); the tables below are the analytic reimplementation in `analyze.py`, cross-checked against that run.

## Short answer

Self's analysis largely holds. Two things it missed matter more than the rest: the force gate, and magnitude. Today's wells pull 20 to 70 times harder than the OrbitPeriod rail implies, so a field orbit at the same radius takes 0.11 to 0.23 of the rail period, and holding the rail costs a real fraction of a ship's thrust. The wells' shape is not the main hostility; the strength is, plus an unstable outer half and a dead-zone gate.

## Method and sources

| number | value | where it came from |
|---|---|---|
| GravityStrength S | 2 | `Assets/Resources/Settings.asset`, PlanetSettings (read from the checked-out file; a real YAML, not an LFS pointer) |
| GravityReferenceRate R | 60 | `Zone.cs:678` constant |
| force gate | \|n_xz\|^2 > 0.001 (slope s > 0.0316) | `Zone.GravityAcceleration`, Zone.cs:680-685 |
| OrbitPeriod | Exponent 1, Multiplier 1, Constant 0, so T = d seconds | Settings.asset PlanetSettings.OrbitPeriod; `ExponentialCurve.Evaluate = M*pow(v,E)+C` |
| well depth / radius | 30*m^0.175 / 500*m^0.25 (times per-body multipliers, default 1) | Settings.asset GravityDepth, GravityRadius; Zone.cs:738-741 |
| well exponent | 16 | `BodyData.GravityDepthExponent` default (ZoneData.cs:97); the generator never overrides it |
| zone bowl | depth 64, exponent 2, radius Pack.Radius | Settings.asset ZoneDepth, ZoneDepthExponent; Zone.cs:629 |
| waves (gas giants, suns) | depth 0.5*m^0.3, radius 500*m^0.25, freq 0.2*m^0.45 | Settings.asset WaveDepth/WaveRadius/WaveFrequency; Zone.cs:643-651 |
| zone radius | lerp(1000, 10000, density^1.5) | Settings.asset ZoneSettings.ZoneRadius (same values as RestoredHullsTests.TutorialZoneSettings, which the probe used) |
| body masses and orbit distances | generated | real `ZoneGenerator.GenerateEntities`, authored ZoneGenerationSettings, 5 densities x 300 seeds = 31,640 bodies (`data/bodies.csv`). Mass thresholds: sun>5000, gas giant>1000, planet>100, else planetoid. |
| ship acceleration | 24 to 36 u/s^2 (Longinus 2500 + Small Drive 60-90k thrust); 6 to 9 (Djinni 10000) | catalog via the probe (`data/ships.csv`); `Thruster.Execute`: `Velocity -= dir*input*thrust/Entity.Mass*dt`. 30 u/s^2 is used as "typical". Approximation: Entity.Mass read as hull mass only. |

PowerPulse(x,e) doubles x then clamps, so a well's real support radius is Rg/2 (half the "GravityWellRadius"), and the zone bowl's is Pack.Radius. Real code on a grid is in `data/profiles.csv` (22 cases: 4 body types x p10/p50/p90 mass, gas giants and suns with and without waves, and 4 bowls). "p50 body" below means the median mass of that type among generated bodies (planetoid 29, planet 274, gas giant 2260, sun 14152).

Cross-check (first table in the appendix): the analytic h matches `Zone.GetHeight` to 1e-4 units; a = S*R*s^2 matches `Zone.GetForce` to 0.7% to 8% (worst for suns, from the 0.1-unit finite-difference step and float32 heights over a steep exponent-16 profile); direction is inward; the only gate disagreements are single cells at the on/off edge.

## Where Self's analysis held or broke

1. a = S*R*|grad h|^2: held. The step cancels (normal xz = -grad h / sqrt(1+|grad h|^2), m = s^2/(1+s^2), 1/(1-m)-1 = s^2); measured against the real code. Not in Self's version: the gate zeroes the force wherever s <= 0.0316. That kills the inner core (force switches on only at r = 3.6 for a small planetoid up to 123 for a heavy sun), the outer rim tail, every zone bowl with radius above 3117 (zone radius is 1000 to 10000: density above about 0.38), and the inverse well beyond r = 329.
2. Waves matter for gas giants and suns: they add 8 to 32 percent of the peak acceleration (2.1x for a 284k-mass rosette sun). Self's analysis ignored them; the tables use the well alone (waves are a time-dependent ripple on top).
3. OrbitPeriod is independent of the field: held. T = d exactly, so every rail body moves at the constant speed 2*pi = 6.28 u/s whatever its distance. That is the 2D point-mass flat rotation curve (below).
4. T_field(r) = 2*pi*sqrt(r/a): held. Non-monotonic: flat or falling inside, minimum near the slope peak, rising to infinity at the rim.
5. Instability where r^3 a falls: held, and it bites. Stable spans under A (B in brackets), p50 body: planetoid out to r = 161 (199), planet 282 (349), gas giant 478 (591), sun 756 (935); beyond that to the rim circular orbits are unstable. About 0.28 rs (A), 0.34 rs (B).
6. Inverse: held with a caveat. h'(r) = 2*pi/(sqrt(S*R)*sqrt(r)) = 0.5736/sqrt(r) for T = r; the gate makes it unrealisable past 329 u.

## Findings

### F1. The field orbit is 5 to 8 times too fast: gravity is 20 to 70 times the rail's
In every orbit band, T_field/T_rail at the median distance is 0.11 to 0.23 (planets of suns 0.12, moons of planets 0.18, belts of gas giants 0.15). The rail needs a = 4*pi^2/r = 39.5/r (0.04 at r=1000, 0.4 at r=100); the wells peak at 3.2 to 16.6 u/s^2 (summary table). Gravity exceeds the centripetal need almost everywhere inside the orbit bands, so holding the rail means thrusting outward against the well.

### F2. Cost of holding the rail: not free
Thrust needed = |4*pi^2/r - a(r)|. A planet's p50 well at its moon/belt orbit distances costs 17 to 30 percent of a 30 u/s^2 ship's acceleration (sun p50: 15 to 17 percent at 487-610 u, 10 percent at 750); the peak over the well is 11 percent (heavy sun) to 53 percent (small planetoid). Near the rim the need drops under 1 percent, but there the A force is gated off anyway. Ship drag (`Ship.cs:131`, `Velocity *= exp(-Drag*dt)`) makes a free orbit impossible regardless; hull Drag values were not measured.

### F3. Zone bowl: matters in small zones, absent in large
Bowl slope peaks at 98.5/Rz. Rz = 1000: push peaks at 1.17 u/s^2 (on for r = 126 to 931); Rz = 2500: 0.19 (on 882 to 1939); Rz >= 3117: never on. With the sun at the zone origin and Rz = 2000 the bowl adds about 1.2 u/s^2 on average over r = 300 to 1500 to the sun's own 2.1, shortening T_A at r=1000 from 230 s to 143 s (rail 1000 s). It is also an ambient push toward zone centre on every body, including those orbiting an off-centre planet. Under A the bowl and wells do not add linearly (the force is a function of the summed slope).

### F4. Where orbits sit relative to wells
Children of a sun: planets d 207-1419 (p50 610), belts 156-1091, planetoids 101-717, gas giants 304-3360, against a p50 sun support radius 2727. A sun's A force is on for 82 percent of the planet band, 37 percent of the gas giant band, 20 percent of the binary-sun band (binaries and rosettes orbit in the dead zone). Moons of a planet: d 57-443 against support 1017; the A-stable span ends at 282, so about 40 percent of moon orbits sit in the unstable outer half. Stations sit on a body's orbit distance (upper half of the distance-sorted bodies): around a sun p50 1053 (inside the A-unstable 756-2727 band), around a gas giant 407, around a planet 411.

### F5. Inverse: what well would make the rail exact
A reading: h = 1.147*sqrt(r), slope infinite at the centre (a cusp), depth rim-to-centre 27 to 59 (today 54 to 160); a = 4*pi^2/r exactly; the gate defeats it past 329 u. B reading with k peak-matched: h = (4*pi^2/k)*ln(r/rs), depth rim to body radius only 4.4 to 7.0 (today 54 to 160), because the rail wants gravity of 0.04 to 0.4 u/s^2, not 5 to 17. Tuned for looks, today's wells are deep, with a flat bottom, a steep mid-radius flank and a flat rim; the rail wants a shallow log (B) or a sqrt cusp (A).

## Reading B: the heightfield as a potential

k (u/s^2 per unit slope, peak-matched to A): planetoid 39.3, planet 33.2, gas giant 28.4, sun 24.7 (p50; range 19.7 to 44.7 over p10-p90). T_B, stability and rail mismatch are in each table and in `fig_T_<type>.png`. T_B tracks T_A around the slope peak (peak-matching makes them agree there) and differs at both ends: B has no inner dead zone and keeps force past where A is gated to zero.

### Laplacian and implied mass (G=1, 2D Poisson: laplacian(phi) = 2*pi*G*Sigma, phi = k*h)
Every well profile has the same signature: positive surface density in the core, a sign change at exactly r = rs/sqrt(e) = rs/4 for e=16 (analytic: laplacian ~ u^(e-2)(1-e*x^2), x = 2r/Rg), then a negative-density ring (antimass) out to the rim, then zero. The positive and negative masses are equal (planet p50 +2022 and -2022 in G=1 units; planetoid 1616, gas giant 2497, sun 2999); net M(<rim) = r*a/2 = 0 because the slope is zero at the rim. So the terrain is not a physical potential: it is a mass-neutral object, a core plus its own antimass ring, which is why nothing is felt outside the rim. The zone bowl has the same signature: positive core, negative density from Rz/sqrt(2) outward (`fig_bowl.png`).

### Point mass, disc, and the OrbitPeriod exponent
In 2D, Gauss gives a = 2*G*M(<r)/r. A point mass: a = c/r, v constant, T proportional to r, logarithmic potential. OrbitPeriod (E=1, M=1) is exactly this: c = 4*pi^2, v = 2*pi. The inverse B well is h = (4*pi^2/k)*ln r with laplacian zero except at the origin: a single point mass (M = 2*pi^2 in G=1 units). An OrbitPeriod exponent E gives a ~ r^(1-2E): E=1 is 2D point mass, E=0 is a rigid-rotation uniform disc (T constant), E=1.5 would be 3D Kepler (1/r^2). A uniform disc of radius rs (`fig_point_disc.png`, planet p50): T constant inside, point-mass tail outside, the potential never returns to flat. Today's core is rigid rotation (planet p50: T_B flat at 22 s) but then bends into an antimass ring: neither a point mass nor a disc.
Under A, the inverse well h = 1.147*sqrt(r) read as a B potential has laplacian 0.287*r^-1.5: a spread mass with enclosed mass growing as sqrt(r), also not a point mass. A forces are not linear in superposition; B forces are.

## Files in this folder
`fig_T_{Planetoid,Planet,GasGiant,Sun}.png`: T_field(A), T_field(B), T_rail over r, child-orbit bands shaded, A-unstable span shaded. `fig_well_*.png`: today's well vs derived A vs derived B. `fig_rho_*.png`: implied surface density and enclosed mass under B. `fig_bowl.png`, `fig_point_disc.png`. CSVs: `data/bodies.csv` (31,640 generated bodies), `data/profiles.csv` (real-code radial profiles: r, h, force x, force y), `data/meta.csv`, `data/ships.csv`. Scripts: `analyze.py`, `extract.py`, `job.sh`; probe source `OrbitProbeScratch.cs.txt` (it was a throwaway local commit in the worktree, never pushed to GameCult; it was pushed to the Yggdrasil verify mirror as refs/verify/<sha> by the stopgap).

## Open and unmeasured
- Hull Drag values (decide whether any free orbit is possible for ships).
- Ship acceleration with items' mass included, and thruster quality (Min/Max range).
- Sub-zone circles (smaller radius and mass) are not in the generated distribution.
- The bowl is evaluated at the zone origin only, not at a body's actual offset.
- Waves are excluded from the T tables.

---

# Appendix: generated tables (analyze.py)

## Cross-check: analytic reimplementation vs the real Zone (GetHeight / GetForce)

| case | max abs err h (units) | max rel err a_A (where a>0.01) | cells where real force is 0 but formula >0.001 | cells where formula is 0 but real >0.001 |
|---|---|---|---|---|
| Planetoid_p10_waves0 | 5.67e-05 | 6.55e-03 | 0 | 0 |
| Planetoid_p50_waves0 | 7.74e-05 | 8.87e-03 | 0 | 0 |
| Planetoid_p90_waves0 | 9.12e-05 | 1.76e-02 | 0 | 0 |
| Planet_p10_waves0 | 9.61e-05 | 2.90e-02 | 0 | 0 |
| Planet_p50_waves0 | 1.14e-04 | 2.61e-02 | 0 | 1 |
| Planet_p90_waves0 | 1.37e-04 | 1.86e-02 | 0 | 0 |
| GasGiant_p10_waves0 | 1.43e-04 | 2.38e-02 | 0 | 0 |
| GasGiant_p50_waves0 | 1.61e-04 | 1.00e+00 | 1 | 0 |
| GasGiant_p90_waves0 | 1.77e-04 | 3.99e-02 | 0 | 0 |
| Sun_p10_waves0 | 1.94e-04 | 5.52e-02 | 0 | 0 |
| Sun_p50_waves0 | 2.26e-04 | 7.25e-02 | 0 | 0 |
| Sun_p90_waves0 | 3.85e-04 | 8.24e-02 | 0 | 0 |
| Bowl_R1000 | 1.18e-05 | 4.91e-03 | 0 | 1 |
| Bowl_R2500 | 1.18e-05 | 1.00e+00 | 1 | 0 |
| Bowl_R5000 | 1.18e-05 | nan | 0 | 0 |
| Bowl_R10000 | 1.18e-05 | nan | 0 | 0 |

Direction: Planet_p50 force x component is negative (inward) for r<1000, y component max 0.00e+00.

## Wave term (gas giants and suns)

| case | peak a with waves | peak a without | max rel diff of a over r<support |
|---|---|---|---|
| GasGiant p10 | 8.048 | 7.429 | 0.084 of peak |
| GasGiant p50 | 7.489 | 6.726 | 0.115 of peak |
| GasGiant p90 | 7.090 | 6.103 | 0.162 of peak |
| Sun p10 | 6.944 | 5.790 | 0.200 of peak |
| Sun p50 | 6.687 | 5.111 | 0.321 of peak |
| Sun p90 | 10.097 | 3.268 | 2.124 of peak |

## Where orbits are in the game (real generator: ZoneGenerator.GenerateEntities, 5 zone densities x 300 seeds, authored ZoneGenerationSettings)

Distance is OrbitData.Distance, relative to the parent body. Rail speed is 2*pi*d/OrbitPeriod(d) = 2*pi u/s at every d (OrbitPeriod = 1*d^1+0).

| parent type | child type | n | d p10 | d p50 | d p90 |
|---|---|---|---|---|---|
| Sun | Sun | 2873 | 817 | 3167 | 6367 |
| Sun | GasGiant | 3663 | 304 | 1405 | 3360 |
| Sun | Planet | 6112 | 207 | 610 | 1419 |
| Sun | Planetoid | 3195 | 101 | 266 | 717 |
| Sun | Belt | 1133 | 156 | 487 | 1091 |
| GasGiant | GasGiant | 316 | 92 | 210 | 407 |
| GasGiant | Planet | 1316 | 111 | 288 | 637 |
| GasGiant | Planetoid | 4217 | 89 | 214 | 581 |
| GasGiant | Belt | 1094 | 104 | 262 | 641 |
| Planet | Planet | 693 | 45 | 114 | 299 |
| Planet | Planetoid | 3084 | 57 | 145 | 443 |
| Planet | Belt | 835 | 72 | 172 | 483 |
| Planetoid | Planetoid | 382 | 31 | 79 | 253 |
| Planetoid | Belt | 72 | 36 | 93 | 260 |

Stations (ZoneGenerator.cs:225-310) sit on the orbit of a selected body (same Distance, phase +-60 deg, parent = that body's parent), chosen from the upper half of the distance-sorted bodies. Station distance by parent type, p10/p50/p90 over all candidates (upper-half pool, count unknown from the stationCount draw):

| parent type | n candidates | d p10 | d p50 | d p90 |
|---|---|---|---|---|
| Sun | 12454 | 345 | 1053 | 4005 |
| GasGiant | 1982 | 215 | 407 | 853 |
| Planet | 424 | 122 | 411 | 1021 |
| Planetoid | 16 | 255 | 427 | 783 |

Belts are bodies in the table above (type Belt); their rocks sit at belt distance +- AsteroidBeltWidth(d) = 5*d^0.666+50 wide.

### Planetoid (representative mass 29 = p50 of Planetoids in generated zones; well radius 1162, support radius 581, depth 54.1, body radius 7.0, exponent 16)

Peak slope |h'| = 0.3274 at r = 104. Peak a_A = S*R*s^2 = 12.8667 u/s^2. Gate (|n|^2 > 0.001 <=> s > 0.0316) switches the force on at r = 6.2. Reading B: k = 39.294 u/s^2 per unit slope (a_B = k*|h'|, peak-matched).

| r | h(r) | slope s | a_A | T_A | a_B | T_B | T_rail=r | hold-rail thrust A (u/s^2, frac of 30) | hold-rail thrust B | stable A | stable B | Laplacian (h"+h'/r) | orbits here |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 5 | -54.08 | 0.0257 | 0.0000 | inf | 1.0093 | 14 | 5 | +7.896 (26.319%) | +6.886 (22.955%) | no | yes | +1.02e-02 |  |
| 10 | -53.89 | 0.0511 | 0.3137 | 35.5 | 2.0090 | 14 | 10 | +3.634 (12.114%) | +1.939 (6.463%) | yes | yes | +1.02e-02 |  |
| 25 | -52.56 | 0.1248 | 1.8678 | 23 | 4.9023 | 14.2 | 25 | -0.289 (0.962%) | -3.323 (11.077%) | yes | yes | +9.70e-03 |  |
| 31 | -51.73 | 0.1524 | 2.7872 | 21 | 5.9885 | 14.3 | 31 | -1.514 (5.046%) | -4.715 (15.717%) | yes | yes | +9.41e-03 | Planetoid[31-253] |
| 36 | -50.91 | 0.1743 | 3.6477 | 19.7 | 6.8509 | 14.4 | 36 | -2.551 (8.504%) | -5.754 (19.181%) | yes | yes | +9.13e-03 | Planetoid[31-253] |
| 50 | -48.07 | 0.2295 | 6.3199 | 17.7 | 9.0175 | 14.8 | 50 | -5.530 (18.434%) | -8.228 (27.427%) | yes | yes | +8.15e-03 | Planetoid[31-253] Belt[36-260] |
| 79 | -40.17 | 0.3064 | 11.2653 | 16.6 | 12.0394 | 16.1 | 79 | -10.766 (35.885%) | -11.540 (38.465%) | yes | yes | +5.57e-03 | Planetoid[31-253] Belt[36-260] |
| 93 | -35.75 | 0.3233 | 12.5446 | 17.1 | 12.7046 | 17 | 93 | -12.120 (40.400%) | -12.280 (40.934%) | yes | yes | +4.21e-03 | Planetoid[31-253] Belt[36-260] |
| 100 | -33.47 | 0.3269 | 12.8197 | 17.5 | 12.8432 | 17.5 | 100 | -12.425 (41.416%) | -12.448 (41.495%) | yes | yes | +3.54e-03 | Planetoid[31-253] Belt[36-260] |
| 150 | -17.97 | 0.2736 | 8.9812 | 25.7 | 10.7498 | 23.5 | 150 | -8.718 (29.060%) | -10.487 (34.955%) | yes | yes | -2.58e-04 | Planetoid[31-253] Belt[36-260] |
| 200 | -7.20 | 0.1548 | 2.8768 | 52.4 | 6.0840 | 36 | 200 | -2.679 (8.931%) | -5.887 (19.622%) | no | no | -1.57e-03 | Planetoid[31-253] Belt[36-260] |
| 253 | -1.88 | 0.0555 | 0.3694 | 164 | 2.1802 | 67.7 | 253 | -0.213 (0.711%) | -2.024 (6.747%) | no | no | -1.10e-03 | Belt[36-260] |
| 260 | -1.52 | 0.0468 | 0.2627 | 198 | 1.8384 | 74.7 | 260 | -0.111 (0.369%) | -1.687 (5.622%) | no | no | -9.91e-04 |  |
| 300 | -0.38 | 0.0147 | 0.0000 | inf | 0.5787 | 143 | 300 | +0.132 (0.439%) | -0.447 (1.491%) | no | no | -4.37e-04 |  |
| 400 | -0.00 | 0.0001 | 0.0000 | inf | 0.0053 | 1.73e+03 | 400 | +0.099 (0.329%) | +0.093 (0.311%) | no | no | -8.43e-06 |  |
| 500 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 3.45e+05 | 500 | +0.079 (0.263%) | +0.079 (0.263%) | no | no | -7.07e-10 |  |
| 523 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 3.59e+06 | 523 | +0.075 (0.252%) | +0.075 (0.252%) | no | no | -9.84e-12 |  |
| 575 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 5.38e+13 | 575 | +0.069 (0.229%) | +0.069 (0.229%) | no | no | -4.84e-25 |  |

Stable circular-orbit spans (d(r^3 a)/dr>0, with a>0): A: 6-161; B: 0-199. Unstable (outer well, a falls faster than 1/r^3): A: 161-275; B: 199-581.
- Planetoid orbits of a Planetoid (d 31-253): force on (A) at 100% of the band, stable (A) 59%, stable (B) 76%; at the median d=79: T_A/T_rail = 0.21, T_B/T_rail = 0.203.
- Belt orbits of a Planetoid (d 36-260): force on (A) at 100% of the band, stable (A) 56%, stable (B) 73%; at the median d=93: T_A/T_rail = 0.184, T_B/T_rail = 0.183.

Reading B mass: Sigma = k*(h"+h'/r)/(2*pi) (G=1). Positive mass 1616.15, negative (antimass) -1616.15, net M(<rim) = -0.000 (should be 0 because h'(rim)=0). Laplacian changes sign at r = 145, 581. Core mass (r<first sign change) fraction of the antimass: 1.00.

### Planet (representative mass 274 = p50 of Planets in generated zones; well radius 2034, support radius 1017, depth 80.1, body radius 12.2, exponent 16)

Peak slope |h'| = 0.2768 at r = 183. Peak a_A = S*R*s^2 = 9.1968 u/s^2. Gate (|n|^2 > 0.001 <=> s > 0.0316) switches the force on at r = 12.8. Reading B: k = 33.221 u/s^2 per unit slope (a_B = k*|h'|, peak-matched).

| r | h(r) | slope s | a_A | T_A | a_B | T_B | T_rail=r | hold-rail thrust A (u/s^2, frac of 30) | hold-rail thrust B | stable A | stable B | Laplacian (h"+h'/r) | orbits here |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 5 | -80.08 | 0.0124 | 0.0000 | inf | 0.4123 | 21.9 | 5 | +7.896 (26.319%) | +7.483 (24.945%) | no | yes | +4.95e-03 |  |
| 10 | -79.99 | 0.0248 | 0.0000 | inf | 0.8236 | 21.9 | 10 | +3.948 (13.159%) | +3.124 (10.414%) | no | yes | +4.94e-03 |  |
| 25 | -79.34 | 0.0614 | 0.4531 | 46.7 | 2.0413 | 22 | 25 | +1.126 (3.753%) | -0.462 (1.541%) | yes | yes | +4.87e-03 |  |
| 45 | -77.64 | 0.1083 | 1.4077 | 35.5 | 3.5981 | 22.2 | 45 | -0.530 (1.768%) | -2.721 (9.069%) | yes | yes | +4.67e-03 |  |
| 50 | -77.07 | 0.1195 | 1.7146 | 33.9 | 3.9710 | 22.3 | 50 | -0.925 (3.083%) | -3.181 (10.605%) | yes | yes | +4.61e-03 | Planet[45-299] |
| 57 | -76.18 | 0.1348 | 2.1795 | 32.1 | 4.4771 | 22.4 | 57 | -1.487 (4.956%) | -3.785 (12.615%) | yes | yes | +4.50e-03 | Planet[45-299] |
| 72 | -73.92 | 0.1655 | 3.2870 | 29.4 | 5.4982 | 22.7 | 72 | -2.739 (9.129%) | -4.950 (16.500%) | yes | yes | +4.25e-03 | Planetoid[57-443] Planet[45-299] |
| 100 | -68.57 | 0.2143 | 5.5096 | 26.8 | 7.1183 | 23.6 | 100 | -5.115 (17.049%) | -6.724 (22.412%) | yes | yes | +3.66e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 114 | -65.44 | 0.2337 | 6.5563 | 26.2 | 7.7651 | 24.1 | 114 | -6.210 (20.700%) | -7.419 (24.729%) | yes | yes | +3.32e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 145 | -57.67 | 0.2641 | 8.3700 | 26.2 | 8.7736 | 25.5 | 145 | -8.098 (26.992%) | -8.501 (28.338%) | yes | yes | +2.51e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 150 | -56.34 | 0.2673 | 8.5745 | 26.3 | 8.8802 | 25.8 | 150 | -8.311 (27.704%) | -8.617 (28.723%) | yes | yes | +2.37e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 172 | -50.35 | 0.2758 | 9.1312 | 27.3 | 9.1639 | 27.2 | 172 | -8.902 (29.672%) | -8.934 (29.781%) | yes | yes | +1.79e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 200 | -42.62 | 0.2743 | 9.0310 | 29.6 | 9.1135 | 29.4 | 200 | -8.834 (29.445%) | -8.916 (29.720%) | yes | yes | +1.09e-03 | Belt[72-483] Planetoid[57-443] Planet[45-299] |
| 299 | -18.86 | 0.1910 | 4.3760 | 51.9 | 6.3439 | 43.1 | 299 | -4.244 (14.146%) | -6.212 (20.706%) | no | yes | -5.35e-04 | Belt[72-483] Planetoid[57-443] |
| 300 | -18.67 | 0.1898 | 4.3214 | 52.4 | 6.3042 | 43.3 | 300 | -4.190 (13.966%) | -6.173 (20.575%) | no | yes | -5.44e-04 | Belt[72-483] Planetoid[57-443] |
| 400 | -5.44 | 0.0797 | 0.7620 | 144 | 2.6473 | 77.2 | 400 | -0.663 (2.211%) | -2.549 (8.495%) | no | no | -6.95e-04 | Belt[72-483] Planetoid[57-443] |
| 443 | -2.76 | 0.0468 | 0.2624 | 258 | 1.5535 | 106 | 443 | -0.173 (0.578%) | -1.464 (4.881%) | no | no | -5.30e-04 | Belt[72-483] Planetoid[57-443] |
| 483 | -1.34 | 0.0259 | 0.0000 | inf | 0.8598 | 149 | 483 | +0.082 (0.272%) | -0.778 (2.594%) | no | no | -3.61e-04 |  |
| 500 | -0.96 | 0.0195 | 0.0000 | inf | 0.6487 | 174 | 500 | +0.079 (0.263%) | -0.570 (1.899%) | no | no | -2.95e-04 |  |
| 750 | -0.00 | 0.0000 | 0.0000 | inf | 0.0005 | 7.89e+03 | 750 | +0.053 (0.175%) | +0.052 (0.174%) | no | no | -6.44e-07 |  |
| 915 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 5.5e+06 | 915 | +0.043 (0.144%) | +0.043 (0.144%) | no | no | -4.93e-12 |  |
| 1000 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 2.72e+12 | 1000 | +0.039 (0.132%) | +0.039 (0.132%) | no | no | -1.40e-22 |  |
| 1007 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 1.43e+14 | 1007 | +0.039 (0.131%) | +0.039 (0.131%) | no | no | -8.70e-26 |  |

Stable circular-orbit spans (d(r^3 a)/dr>0, with a>0): A: 13-282; B: 0-349. Unstable (outer well, a falls faster than 1/r^3): A: 282-470; B: 349-1017.
- Belt orbits of a Planet (d 72-483): force on (A) at 97% of the band, stable (A) 51%, stable (B) 67%; at the median d=172: T_A/T_rail = 0.158, T_B/T_rail = 0.158.
- Planetoid orbits of a Planet (d 57-443): force on (A) at 100% of the band, stable (A) 58%, stable (B) 76%; at the median d=145: T_A/T_rail = 0.18, T_B/T_rail = 0.176.
- Planet orbits of a Planet (d 45-299): force on (A) at 100% of the band, stable (A) 93%, stable (B) 100%; at the median d=114: T_A/T_rail = 0.231, T_B/T_rail = 0.212.

Reading B mass: Sigma = k*(h"+h'/r)/(2*pi) (G=1). Positive mass 2021.62, negative (antimass) -2021.62, net M(<rim) = -0.000 (should be 0 because h'(rim)=0). Laplacian changes sign at r = 254, 1017. Core mass (r<first sign change) fraction of the antimass: 1.00.

### GasGiant (representative mass 2260 = p50 of GasGiants in generated zones; well radius 3447, support radius 1724, depth 115.9, body radius 20.7, exponent 16)

Peak slope |h'| = 0.2363 at r = 310. Peak a_A = S*R*s^2 = 6.7013 u/s^2. Gate (|n|^2 > 0.001 <=> s > 0.0316) switches the force on at r = 25.5. Reading B: k = 28.358 u/s^2 per unit slope (a_B = k*|h'|, peak-matched).

| r | h(r) | slope s | a_A | T_A | a_B | T_B | T_rail=r | hold-rail thrust A (u/s^2, frac of 30) | hold-rail thrust B | stable A | stable B | Laplacian (h"+h'/r) | orbits here |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 5 | -115.88 | 0.0063 | 0.0000 | inf | 0.1784 | 33.4 | 5 | +7.896 (26.319%) | +7.717 (25.724%) | no | yes | +2.50e-03 |  |
| 10 | -115.84 | 0.0125 | 0.0000 | inf | 0.3552 | 33.4 | 10 | +3.948 (13.159%) | +3.593 (11.975%) | no | yes | +2.49e-03 |  |
| 25 | -115.51 | 0.0312 | 0.0000 | inf | 0.8835 | 33.4 | 25 | +1.579 (5.264%) | +0.696 (2.319%) | no | yes | +2.48e-03 |  |
| 50 | -114.35 | 0.0617 | 0.4563 | 65.8 | 1.7487 | 33.6 | 50 | +0.333 (1.111%) | -0.959 (3.197%) | yes | yes | +2.43e-03 |  |
| 89 | -111.05 | 0.1068 | 1.3679 | 50.7 | 3.0277 | 34.1 | 89 | -0.924 (3.081%) | -2.584 (8.614%) | yes | yes | +2.30e-03 | Planetoid[89-581] |
| 92 | -110.73 | 0.1100 | 1.4529 | 50 | 3.1203 | 34.1 | 92 | -1.024 (3.413%) | -2.691 (8.971%) | yes | yes | +2.29e-03 | Planetoid[89-581] |
| 100 | -109.81 | 0.1187 | 1.6905 | 48.3 | 3.3658 | 34.3 | 100 | -1.296 (4.319%) | -2.971 (9.903%) | yes | yes | +2.25e-03 | Planetoid[89-581] GasGiant[92-407] |
| 104 | -109.33 | 0.1229 | 1.8136 | 47.6 | 3.4862 | 34.3 | 104 | -1.434 (4.780%) | -3.107 (10.355%) | yes | yes | +2.23e-03 | Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 111 | -108.44 | 0.1302 | 2.0340 | 46.4 | 3.6920 | 34.5 | 111 | -1.678 (5.595%) | -3.336 (11.121%) | yes | yes | +2.20e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 150 | -102.63 | 0.1671 | 3.3494 | 42 | 4.7377 | 35.4 | 150 | -3.086 (10.288%) | -4.474 (14.915%) | yes | yes | +1.97e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 200 | -93.30 | 0.2037 | 4.9815 | 39.8 | 5.7778 | 37 | 200 | -4.784 (15.947%) | -5.580 (18.601%) | yes | yes | +1.62e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 210 | -91.23 | 0.2095 | 5.2661 | 39.7 | 5.9405 | 37.4 | 210 | -5.078 (16.927%) | -5.753 (19.175%) | yes | yes | +1.54e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 214 | -90.39 | 0.2116 | 5.3745 | 39.7 | 6.0013 | 37.5 | 214 | -5.190 (17.300%) | -5.817 (19.390%) | yes | yes | +1.51e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 262 | -79.74 | 0.2303 | 6.3660 | 40.3 | 6.5315 | 39.8 | 262 | -6.215 (20.718%) | -6.381 (21.269%) | yes | yes | +1.13e-03 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 288 | -73.67 | 0.2351 | 6.6330 | 41.4 | 6.6671 | 41.3 | 288 | -6.496 (21.653%) | -6.530 (21.767%) | yes | yes | +9.29e-04 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 300 | -70.85 | 0.2361 | 6.6879 | 42.1 | 6.6946 | 42.1 | 300 | -6.556 (21.854%) | -6.563 (21.877%) | yes | yes | +8.36e-04 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 400 | -47.79 | 0.2176 | 5.6842 | 52.7 | 6.1718 | 50.6 | 400 | -5.585 (18.618%) | -6.073 (20.244%) | yes | yes | +1.59e-04 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 407 | -46.28 | 0.2149 | 5.5406 | 53.9 | 6.0934 | 51.4 | 407 | -5.444 (18.145%) | -5.996 (19.988%) | yes | yes | +1.21e-04 | Planet[111-637] Planetoid[89-581] GasGiant[92-407] Belt[104-641] |
| 500 | -28.39 | 0.1670 | 3.3454 | 76.8 | 4.7348 | 64.6 | 500 | -3.266 (10.888%) | -4.656 (15.519%) | no | yes | -2.53e-04 | Planet[111-637] Planetoid[89-581] Belt[104-641] |
| 581 | -16.83 | 0.1188 | 1.6935 | 116 | 3.3688 | 82.5 | 581 | -1.626 (5.419%) | -3.301 (11.003%) | no | yes | -3.77e-04 | Planet[111-637] Planetoid[89-581] Belt[104-641] |
| 637 | -11.06 | 0.0879 | 0.9262 | 165 | 2.4914 | 100 | 637 | -0.864 (2.881%) | -2.429 (8.098%) | no | no | -3.79e-04 | Belt[104-641] |
| 641 | -10.71 | 0.0858 | 0.8833 | 169 | 2.4330 | 102 | 641 | -0.822 (2.739%) | -2.371 (7.905%) | no | no | -3.77e-04 |  |
| 750 | -4.03 | 0.0402 | 0.1937 | 391 | 1.1394 | 161 | 750 | -0.141 (0.470%) | -1.087 (3.623%) | no | no | -2.68e-04 |  |
| 1000 | -0.16 | 0.0026 | 0.0000 | inf | 0.0751 | 725 | 1000 | +0.039 (0.132%) | -0.036 (0.119%) | no | no | -3.50e-05 |  |
| 1500 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 1.37e+06 | 1500 | +0.026 (0.088%) | +0.026 (0.088%) | no | no | -6.81e-11 |  |
| 1551 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 8.47e+06 | 1551 | +0.025 (0.085%) | +0.025 (0.085%) | no | no | -2.44e-12 |  |
| 1706 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 1.59e+14 | 1706 | +0.023 (0.077%) | +0.023 (0.077%) | no | no | -7.89e-26 |  |

Stable circular-orbit spans (d(r^3 a)/dr>0, with a>0): A: 25-478; B: 0-591. Unstable (outer well, a falls faster than 1/r^3): A: 478-779; B: 591-1724.
- Planet orbits of a GasGiant (d 111-637): force on (A) at 100% of the band, stable (A) 70%, stable (B) 91%; at the median d=288: T_A/T_rail = 0.144, T_B/T_rail = 0.143.
- Planetoid orbits of a GasGiant (d 89-581): force on (A) at 100% of the band, stable (A) 79%, stable (B) 100%; at the median d=214: T_A/T_rail = 0.186, T_B/T_rail = 0.176.
- GasGiant orbits of a GasGiant (d 92-407): force on (A) at 100% of the band, stable (A) 100%, stable (B) 100%; at the median d=210: T_A/T_rail = 0.189, T_B/T_rail = 0.178.
- Belt orbits of a GasGiant (d 104-641): force on (A) at 100% of the band, stable (A) 70%, stable (B) 91%; at the median d=262: T_A/T_rail = 0.154, T_B/T_rail = 0.152.

Reading B mass: Sigma = k*(h"+h'/r)/(2*pi) (G=1). Positive mass 2496.62, negative (antimass) -2496.62, net M(<rim) = -0.000 (should be 0 because h'(rim)=0). Laplacian changes sign at r = 431, 1724. Core mass (r<first sign change) fraction of the antimass: 1.00.

### Sun (representative mass 14152 = p50 of Suns in generated zones; well radius 5454, support radius 2727, depth 159.8, body radius 32.7, exponent 16)

Peak slope |h'| = 0.2059 at r = 490. Peak a_A = S*R*s^2 = 5.0891 u/s^2. Gate (|n|^2 > 0.001 <=> s > 0.0316) switches the force on at r = 46.2. Reading B: k = 24.712 u/s^2 per unit slope (a_B = k*|h'|, peak-matched).

| r | h(r) | slope s | a_A | T_A | a_B | T_B | T_rail=r | hold-rail thrust A (u/s^2, frac of 30) | hold-rail thrust B | stable A | stable B | Laplacian (h"+h'/r) | orbits here |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 5 | -159.77 | 0.0035 | 0.0000 | inf | 0.0857 | 48.2 | 5 | +7.896 (26.319%) | +7.810 (26.033%) | no | yes | +1.38e-03 |  |
| 10 | -159.74 | 0.0069 | 0.0000 | inf | 0.1703 | 48.2 | 10 | +3.948 (13.159%) | +3.778 (12.592%) | no | yes | +1.37e-03 |  |
| 25 | -159.56 | 0.0172 | 0.0000 | inf | 0.4246 | 48.2 | 25 | +1.579 (5.264%) | +1.155 (3.848%) | no | yes | +1.37e-03 |  |
| 50 | -158.92 | 0.0342 | 0.1406 | 119 | 0.8460 | 48.3 | 50 | +0.649 (2.163%) | -0.056 (0.188%) | yes | yes | +1.36e-03 |  |
| 100 | -156.37 | 0.0674 | 0.5450 | 85.1 | 1.6655 | 48.7 | 100 | -0.150 (0.501%) | -1.271 (4.236%) | yes | yes | +1.32e-03 |  |
| 101 | -156.30 | 0.0681 | 0.5558 | 84.7 | 1.6818 | 48.7 | 101 | -0.165 (0.550%) | -1.291 (4.303%) | yes | yes | +1.32e-03 | Planetoid[101-717] |
| 150 | -152.21 | 0.0986 | 1.1664 | 71.3 | 2.4363 | 49.3 | 150 | -0.903 (3.011%) | -2.173 (7.244%) | yes | yes | +1.25e-03 | Planetoid[101-717] |
| 156 | -151.61 | 0.1021 | 1.2521 | 70.1 | 2.5244 | 49.4 | 156 | -0.999 (3.330%) | -2.271 (7.571%) | yes | yes | +1.24e-03 | Planetoid[101-717] |
| 200 | -146.56 | 0.1268 | 1.9308 | 63.9 | 3.1346 | 50.2 | 200 | -1.733 (5.778%) | -2.937 (9.791%) | yes | yes | +1.17e-03 | Planetoid[101-717] Belt[156-1091] |
| 207 | -145.66 | 0.1305 | 2.0449 | 63.2 | 3.2259 | 50.3 | 207 | -1.854 (6.181%) | -3.035 (10.117%) | yes | yes | +1.15e-03 | Planetoid[101-717] Belt[156-1091] |
| 266 | -137.10 | 0.1585 | 3.0148 | 59 | 3.9170 | 51.8 | 266 | -2.866 (9.555%) | -3.769 (12.562%) | yes | yes | +1.02e-03 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] |
| 300 | -131.49 | 0.1719 | 3.5442 | 57.8 | 4.2470 | 52.8 | 300 | -3.413 (11.375%) | -4.115 (13.718%) | yes | yes | +9.35e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] |
| 304 | -130.79 | 0.1733 | 3.6039 | 57.7 | 4.2826 | 52.9 | 304 | -3.474 (11.580%) | -4.153 (13.843%) | yes | yes | +9.25e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] |
| 400 | -112.81 | 0.1985 | 4.7274 | 57.8 | 4.9050 | 56.7 | 400 | -4.629 (15.429%) | -4.806 (16.021%) | yes | yes | +6.65e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 487 | -95.12 | 0.2059 | 5.0888 | 61.5 | 5.0890 | 61.5 | 487 | -5.008 (16.692%) | -5.008 (16.693%) | yes | yes | +4.28e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 500 | -92.44 | 0.2058 | 5.0845 | 62.3 | 5.0868 | 62.3 | 500 | -5.006 (16.685%) | -5.008 (16.693%) | yes | yes | +3.94e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 610 | -70.26 | 0.1942 | 4.5251 | 73 | 4.7989 | 70.8 | 610 | -4.460 (14.868%) | -4.734 (15.780%) | yes | yes | +1.33e-04 | Planetoid[101-717] Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 717 | -50.77 | 0.1683 | 3.3998 | 91.2 | 4.1596 | 82.5 | 717 | -3.345 (11.149%) | -4.105 (13.682%) | yes | yes | -5.36e-05 | Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 750 | -45.37 | 0.1585 | 3.0129 | 99.1 | 3.9157 | 87 | 750 | -2.960 (9.868%) | -3.863 (12.877%) | yes | yes | -9.63e-05 | Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 817 | -35.47 | 0.1370 | 2.2534 | 120 | 3.3864 | 97.6 | 817 | -2.205 (7.350%) | -3.338 (11.127%) | no | yes | -1.61e-04 | Belt[156-1091] Planet[207-1419] GasGiant[304-3360] |
| 1000 | -15.84 | 0.0788 | 0.7445 | 230 | 1.9466 | 142 | 1000 | -0.705 (2.350%) | -1.907 (6.357%) | no | no | -2.10e-04 | Belt[156-1091] Planet[207-1419] GasGiant[304-3360] Sun[817-6367] |
| 1091 | -9.80 | 0.0548 | 0.3601 | 346 | 1.3538 | 178 | 1091 | -0.324 (1.080%) | -1.318 (4.392%) | no | no | -1.87e-04 | Belt[156-1091] Planet[207-1419] GasGiant[304-3360] Sun[817-6367] |
| 1405 | -1.15 | 0.0094 | 0.0000 | inf | 0.2332 | 488 | 1405 | +0.028 (0.094%) | -0.205 (0.684%) | no | no | -5.94e-05 | Planet[207-1419] GasGiant[304-3360] Sun[817-6367] |
| 1419 | -1.02 | 0.0085 | 0.0000 | inf | 0.2113 | 515 | 1419 | +0.028 (0.093%) | -0.183 (0.611%) | no | no | -5.51e-05 | GasGiant[304-3360] Sun[817-6367] |
| 1500 | -0.50 | 0.0046 | 0.0000 | inf | 0.1144 | 720 | 1500 | +0.026 (0.088%) | -0.088 (0.293%) | no | no | -3.40e-05 | GasGiant[304-3360] Sun[817-6367] |
| 2000 | -0.00 | 0.0000 | 0.0000 | inf | 0.0003 | 1.58e+04 | 2000 | +0.020 (0.066%) | +0.019 (0.065%) | no | no | -2.11e-07 | GasGiant[304-3360] Sun[817-6367] |
| 2454 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 1.23e+07 | 2454 | +0.016 (0.054%) | +0.016 (0.054%) | no | no | -1.32e-12 | GasGiant[304-3360] Sun[817-6367] |
| 2699 | -0.00 | 0.0000 | 0.0000 | inf | 0.0000 | 2.43e+14 | 2699 | +0.015 (0.049%) | +0.015 (0.049%) | no | no | -3.93e-26 | GasGiant[304-3360] Sun[817-6367] |

Stable circular-orbit spans (d(r^3 a)/dr>0, with a>0): A: 46-756; B: 0-935. Unstable (outer well, a falls faster than 1/r^3): A: 756-1206; B: 935-2727.
- Planetoid orbits of a Sun (d 101-717): force on (A) at 100% of the band, stable (A) 100%, stable (B) 100%; at the median d=266: T_A/T_rail = 0.222, T_B/T_rail = 0.195.
- Belt orbits of a Sun (d 156-1091): force on (A) at 100% of the band, stable (A) 64%, stable (B) 83%; at the median d=487: T_A/T_rail = 0.126, T_B/T_rail = 0.126.
- Planet orbits of a Sun (d 207-1419): force on (A) at 82% of the band, stable (A) 45%, stable (B) 60%; at the median d=610: T_A/T_rail = 0.12, T_B/T_rail = 0.116.
- GasGiant orbits of a Sun (d 304-3360): force on (A) at 37% of the band, stable (A) 19%, stable (B) 26%; at the median d=1405: T_A/T_rail = inf, T_B/T_rail = 0.347.
- Sun orbits of a Sun (d 817-6367): force on (A) at 20% of the band, stable (A) 0%, stable (B) 6%; at the median d=3167: T_A/T_rail = inf, T_B/T_rail = inf.

Reading B mass: Sigma = k*(h"+h'/r)/(2*pi) (G=1). Positive mass 2999.36, negative (antimass) -2999.36, net M(<rim) = -0.000 (should be 0 because h'(rim)=0). Laplacian changes sign at r = 682, 2727. Core mass (r<first sign change) fraction of the antimass: 1.00.

## Summary by body type and mass quantile (reading A and B peaks)

| type | quantile | mass | support r | depth | peak slope at r | peak a_A | force switches on at r | k (B) | rail hold thrust at peak (frac of 30) |
|---|---|---|---|---|---|---|---|---|---|
| Planetoid | p10 | 5 | 380 | 40 | 0.3721 at 68 | 16.613 | 3.6 | 44.65 | 53.446% |
| Planetoid | p50 | 29 | 581 | 54 | 0.3274 at 104 | 12.867 | 6.2 | 39.29 | 41.628% |
| Planetoid | p90 | 79 | 746 | 64 | 0.3038 at 134 | 11.075 | 8.6 | 36.46 | 35.936% |
| Planet | p10 | 120 | 827 | 69 | 0.2946 at 149 | 10.412 | 9.8 | 35.35 | 33.820% |
| Planet | p50 | 274 | 1017 | 80 | 0.2768 at 183 | 9.197 | 12.8 | 33.22 | 29.935% |
| Planet | p90 | 760 | 1312 | 96 | 0.2564 at 236 | 7.892 | 17.8 | 30.77 | 25.748% |
| GasGiant | p10 | 1160 | 1459 | 103 | 0.2484 at 262 | 7.406 | 20.5 | 29.81 | 24.185% |
| GasGiant | p50 | 2260 | 1724 | 116 | 0.2363 at 310 | 6.701 | 25.5 | 28.36 | 21.912% |
| GasGiant | p90 | 4263 | 2020 | 130 | 0.2253 at 363 | 6.093 | 31.3 | 27.04 | 19.947% |
| Sun | p10 | 6163 | 2215 | 138 | 0.2192 at 398 | 5.765 | 35.3 | 26.30 | 18.886% |
| Sun | p50 | 14152 | 2727 | 160 | 0.2059 at 490 | 5.089 | 46.2 | 24.71 | 16.695% |
| Sun | p90 | 284099 | 5772 | 270 | 0.1644 at 1037 | 3.245 | 122.8 | 19.73 | 10.691% |

Ship scale (catalog, tests/ships.csv): thrust is Thruster.Thrust/Entity.Mass per second (Thruster.cs:121). Longinus (mass 2500, TopSpeed 100) with a Small Drive (thrust 60000..90000) = 24..36 u/s^2; Djinni (mass 10000, TopSpeed 50) = 6..9. A_SHIP = 30 used above. Gravity peaks are 0.01-3 u/s^2.

## Zone bowl

h_bowl = -64*(1-(r/Rz)^2)^2, slope = 256*u*(1-u^2)/Rz with u = r/Rz, max 98.5/Rz at u = 0.577. Gate needs slope > 0.0316, i.e. Rz < 3117 for the bowl to push at all.

| pack radius Rz | peak slope | peak a_A (real code) | real code max force | force on over | centre-of-zone T_A at 0.5Rz | note |
|---|---|---|---|---|---|---|
| 1000 | 0.0985 | 1.1651 | 1.1646 | 126-931 | 133.6 | zone radius range is 1000..10000 (ZoneRadius) |
| 2500 | 0.0394 | 0.1864 | 0.1866 | 882-1939 | 528.1 |  |
| 5000 | 0.0197 | 0.0000 | 0.0000 | never | inf |  |
| 10000 | 0.0099 | 0.0000 | 0.0000 | never | inf |  |

Sun p50 (support 2727) in a zone of radius 1000, bowl centred on the sun (the generator puts the root body at the origin): bowl adds +2.328 u/s^2 on average over r=300..1500 (well alone 2.080); T_A at r=1000: well alone 230 s, with bowl 230 s (rail 1000 s).
Sun p50 (support 2727) in a zone of radius 2000, bowl centred on the sun (the generator puts the root body at the origin): bowl adds +1.158 u/s^2 on average over r=300..1500 (well alone 2.080); T_A at r=1000: well alone 230 s, with bowl 143 s (rail 1000 s).
Sun p50 (support 2727) in a zone of radius 5000, bowl centred on the sun (the generator puts the root body at the origin): bowl adds +0.182 u/s^2 on average over r=300..1500 (well alone 2.080); T_A at r=1000: well alone 230 s, with bowl 205 s (rail 1000 s).

Bowl as reading B mass: laplacian of -64(1-u^2)^2 = 64*(...) is positive at the centre and negative near the rim, same antimass-ring signature (see fig_bowl.png).

## Inverse: the well that makes OrbitPeriod exact (T = r)

A: h'(r) = 2*pi*sqrt(r)/(sqrt(S*R)*T) = 2*pi/(sqrt(120)*sqrt(r)) = 0.5736/sqrt(r); h = 1.1471*sqrt(r) (cusp at the centre, slope infinite at r=0). a = S*R*h'^2 = 4*pi^2/r exactly. Stable for any T growing slower than r^2: T=r qualifies everywhere. The force gate zeroes the field once h' < 0.0316, i.e. beyond r = 329 u, so the exact well cannot be realised past that radius with the gate in place (a rail at 1000 u would need slope 0.0181).

| type (p50) | rim (support) | today's depth | derived A depth (rim to centre) | derived B depth (rim to body radius) | k |
|---|---|---|---|---|---|
| Planetoid | 581 | 54.1 | 27.0 | 4.4 | 39.29 |
| Planet | 1017 | 80.1 | 35.8 | 5.2 | 33.22 |
| GasGiant | 1724 | 115.9 | 46.6 | 6.1 | 28.36 |
| Sun | 2727 | 159.8 | 58.6 | 7.0 | 24.71 |

B: h'(r) = 4*pi^2*r/(k*T^2) = 4*pi^2/(k*r), h = (4*pi^2/k) ln r, laplacian = 0 everywhere except r=0: a single point mass (2D Newton: a = 2*G*M/r, M = r*a/2 = 2*pi^2 in G=1 units, independent of r).

## Reading B comparisons: point mass, uniform disc

2D gravity (Gauss law in the plane): a(r) = 2*G*M(<r)/r. Point mass: a = c/r, v = const = sqrt(c), T = 2*pi*r/v ~ r (flat rotation curve). OrbitPeriod is Exponent=1, Multiplier=1, i.e. T=d^1: exactly the 2D point mass with c = 4*pi^2 (v = 2*pi u/s). Generalising, OrbitPeriod exponent E gives a ~ r^(1-2E): E=1 is the 2D point mass (1/r), E=1.5 would be 3D Kepler (1/r^2), E=0 is a rigid-rotation disc interior (a ~ r, T const).

Uniform disc of radius 1017 (the Planet p50 support) in B, with core period fixed to the rail at the median planet-around-sun distance (610 u): a = Omega^2*r inside (Omega = 2*pi/610), T constant = 610 s inside; point-mass tail outside, T = 610*(r/1017) s beyond r=1017. Height: parabola h = (Omega^2/(2k))(r^2 - rs^2) inside, log outside; the potential never returns to flat (log), unlike today's wells which are exactly flat beyond the rim (zero net mass). A point mass has an unbounded log well; today's well is bounded and mass-neutral.

Today's p50 Planet core (r << rim): h ≈ -D + D*e*(2/Rg)^2*r^2 -> h" = 0.002478, h'/r = h"; so near the centre laplacian = 2h" = 0.004957 > 0: positive density, rigid-rotation core in B (T = 2*pi/sqrt(k*h") = 22 s), not a point mass.
