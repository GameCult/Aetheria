# Belt shapes: prior art (Eyes pass)

Serves ruling: `aetheria-gravity:ruling:belt-shapes-direction` (follow-up `aetheria-gravity:follow_up:belt-shapes`).
Session self-2026-10-17-ag5. Eyes, read-only. Facts with pointers; no conclusion or recommendation.
Marks: (inferred) = my arithmetic or reading, not stated by the source. (gap) = looked, not found.
Wikipedia pages were read through a summarizing fetch, so numbers are the summary's reading of the page, not verbatim; primary papers were not opened except through search snippets.

## 0. Aetheria context read

Operator words (ruling): a belt today is "just full belts"; she names planetoids "sweeping out an annulus", Trojans, and "Oort-like belts, ... spreading around some material at the edges of the wells". The ruling says these illustrate and do not close the space.

Laws in force that bear on this (from the rulings-in-force query):
- `belt-fills-its-slot` (Defaulted): a belt is massless, placed after siblings; slot = max(lower bound, previous sibling outer edge x q) to min(window, next sibling / q); width = min(AsteroidBeltWidth(D), slot). Production belts are slot-bound (median 11% of authored width, p90 29%).
- `moons-within-reach-ruled`, `tail-taming-law-r3-band-loose-cusp`: children sit inside a fraction of the parent's reach (band law, reach 1/2 D), at least max(1.5 x parent Roche, ...) out, half-reach sibling spacing; what does not fit folds into the parent.
- `binaries-orbited-from-afar`: nothing orbits on a binary's own ring ("strengthless companion on the ring" is a layout the generator must not produce); cites Holman & Wiegert 1999 (circumbinary stable beyond ~2-4 separations).
- `asteroids-on-floater-layer`, `asteroid-tops-at-layer`, `rock-meshes-by-size`, `orbitals-above-floaters-below`: rocks sit on the floater layer, tops at layer; rock mesh ranked by size.
- `rails-vs-field-ruled`, `tame-tails-parent-only-rails`: each orbit's period derives from its parent's pull only; rails, not n-body.
- `well-strength-from-mass-ruled`: strength c = G m^p, p about 0.175-0.25. `core-at-roche-and-exaggeration-up`: core radius = Roche limit, a mass curve.

Code read: `F:\Projects\Aetheria\Assets\Scripts\ServerShared\ZoneGenerator.cs` 557-662.
- 558-564: belt candidates are childless planets with Mass < `BeltMassCeiling`, not binaries; each is flagged `Belt = true` with probability `BeltProbability`, at most one belt per parent (`!planet.Parent.Children.Any(p=>p.Belt)`).
- 566-569: masses renormalized to the root's mass.
- 576-581: `RocheMargin` 1.5, `CuspMargin` 1.5, `SiblingSpacing` .5.
- 597: `WellMass` is 0 for a belt, so a belt contributes no pull and constrains no sibling's cusp bound.
- 634-647: a belt is an interval inside its slot; `Distance` is the slot centre; `BeltHalfWidth = min(AsteroidBeltWidth(D), slot)/2`.
- 625-627 / 599-603: when the outermost child overflows the window, the lightest family folds into the parent (`Fold`).
Map (`docs/aetheria-gravity-map.md`): B53 / line 139: a belt's extent is the well tree's input ("inside L_F a node pulls as its body alone" holds for every rail round the node, and a belt is a family of rails out to its outermost asteroid); belts must keep their inner edge outside the Roche margin. Line 136: asteroid periods live on the belt (`Pose` and `BeltTargets` divide by the same float).
(inferred) No code path read gives a belt a member body, a phase-locked cluster, or an outer-edge population; a belt is one interval per parent.

## 1. Planetoid embedded in a belt that clears or sweeps a lane

### 1.1 Observed cases
| Case | Numbers | Source |
|---|---|---|
| Ceres, Vesta in main belt: they do not clear it | Ceres 39.2% and Vesta 10.8% of belt mass; belt total 2.39e21 kg, ~3% of the Moon; mean spacing ~965,600 km; belt "mostly empty" | https://en.wikipedia.org/wiki/Asteroid_belt |
| Pan, Encke gap | Pan mean diameter 27.4 km (34.6 x 28.2 x 21.0); a = 133,584 km; Encke gap ~322 km; a ringlet shares Pan's orbit, taken as evidence of particles in horseshoe orbits | https://en.wikipedia.org/wiki/Pan_(moon) |
| Pan mass and Hill radius | mass 8.6e-12 Saturn masses (~5e15 kg, inferred conversion); R_H ~19-20 km; one source states edge-to-Pan distance = 8.45 R_H | search snippet, https://arxiv.org/pdf/1211.4762 and https://en.wikipedia.org/wiki/Pan_(moon); (inferred) gap ~16 R_H wide |
| Daphnis, Keeler gap | mean diameter 7.8 km; a = 136,505.5 km; Keeler gap ~42 km; moon raises edge waves, inner edge waves lead, outer trail | https://en.wikipedia.org/wiki/Daphnis_(moon) |
| Shepherds | mechanism: proximity/resonant deflection; particles ahead speed up and move out, behind slow and move in. Prometheus and Pandora at F ring (only Prometheus thought to shepherd); Cordelia/Ophelia on Uranus's epsilon ring; Galatea at Neptune's ring arcs | https://en.wikipedia.org/wiki/Shepherd_moon |
| Propeller moonlets | radial scale set by Hill radius; azimuthal extent by moonlet mass vs ring viscosity; Blériot R_H ~400-590 m; partial-gap separation ~4 R_H in simulations | https://arxiv.org/pdf/1811.05329 (via search) |

(inferred) Pan's gap is 42x Pan's mean radius wide; Daphnis's gap is ~10.8x Daphnis's mean radius wide. A gap is many body radii wide and is set by Hill radius, not by body size.

### 1.2 Gap-width physics
- Hill radius: R_H = a (m / 3(M+m))^(1/3) (https://en.wikipedia.org/wiki/Hill_sphere). Scales as a x m^(1/3); for eccentric orbit evaluated at pericentre.
- Hill-sphere stability limits quoted there: two planets need mutual separation > 2 sqrt(3) mutual Hill radii for stability; three or more planets with < ~10 mutual Hill radii are "inherently unstable".
- Gap-opening in a ring: moonlet scattering torque (far field falls as 1/x^4 beyond ~5 R_H, Goldreich & Tremaine; near field numerical) opposed by viscous spreading (https://arxiv.org/pdf/1902.01627 and arXiv 1208.4775 via search). Gap width is therefore not a fixed multiple of R_H: it depends on ring viscosity and surface density. The Pan figure above (~8 R_H per side) is one observation.
- Disc-planet criterion, Crida, Morbidelli & Masset 2006 (https://arxiv.org/pdf/astro-ph/0511082): 3H/(4 R_H) + 50/(q Re) <= 1, q = mass ratio, H = scale height, Re = Reynolds number; "deep gap" = ~90% of co-orbital gas removed. The two terms are the thermal limit (R_H > H) and the viscous limit (q > ~40/Re). Written for gas discs, not rings (inferred: the form transfers qualitatively to a particle disc with H = ring thickness; no source read says so).
- Resonance-overlap (chaotic zone) width, Wisdom 1980: delta a / a ~ 1.3 mu^(2/7), mu = planet/star mass ratio; 1.6 mu^(2/7) quoted as half-width in one source (Duncan, Quinn & Tremaine 1989). Above a critical eccentricity: 1.8 e^(1/5) mu^(1/5) (Mustill & Wyatt 2012). Sources: https://arxiv.org/pdf/astro-ph/0111600, https://arxiv.org/pdf/1207.0101, https://arxiv.org/pdf/1411.1378 (all via search; coefficients disagree between half- and full-width conventions). This scaling is for a clearing planet with test particles over time, no viscosity.
- Scalings side by side (inferred): ring-moonlet gap ~ R_H ~ m^(1/3); chaotic-zone clearing ~ mu^(2/7) a. Exponents 0.33 and 0.29 are close; prefactors differ by a viscosity-dependent factor.
- Kirkwood gaps (resonance, not a body in the lane): 5:1 1.780 AU, 4:1 2.065, 3:1 2.502, 5:2 2.825, 7:3 2.958, 2:1 3.279 AU. Mechanism per source: overlapping nu5/nu6 secular resonances inside mean-motion resonances; orbits go chaotic and become planet-crossing within millions of years. A single-epoch snapshot shows no density dip because eccentric orbits still cross gap radii. 3:2 (3.972 AU, Hildas) and 4:3 (4.296 AU, Thule) are populated resonances. https://en.wikipedia.org/wiki/Kirkwood_gap

### 1.3 Rails vs dynamics
- Translates directly (inferred): R_H = a (m/3M)^(1/3) as a width rule needs only masses and the parent's distance; Pan/Daphnis show gap width as a multiple of R_H on a ring that is otherwise on circular rails. Kirkwood-gap positions are ratios of the parent's period (period ratio p:q gives a = a_J (q/p)^(2/3)), computable on rails.
- Assumes dynamics: the Wisdom width and Crida criterion are statements about what happens over time to test particles; on rails they would be authored widths. Viscous gap-refilling has no rail analogue.
- Mismatch with Aetheria's law (inferred): Aetheria's reach and Roche come from c = G m^p with p 0.175-0.25, not m^(1/3), so a Hill-radius-derived width would be a different curve of mass than the field's.

## 2. Trojans and co-orbitals

### 2.1 Geometry and dynamics
- Tadpole orbits librate about L4/L5 (60 deg ahead/behind); horseshoe orbits wrap L3 to L4 to L5; the tadpole-horseshoe separatrix sits near libration angle ~24 deg (https://arxiv.org/pdf/1311.6750). Horseshoe orbits need libration beyond ~312 deg full excursion (i.e. tadpole within ~156 deg of the Lagrange point angle) per https://arxiv.org/pdf/2402.15168; Janus-Epimetheus are horseshoe partners, Polydeuces a tadpole at Dione's L5 (https://en.wikipedia.org/wiki/Horseshoe_orbit).
- Stability: L4/L5 linearly stable for mass ratio mu below the Routh/Gascheau value 0.03852 (https://arxiv.org/pdf/2510.13479, https://ar5iv.labs.arxiv.org/html/2007.13814). Horseshoe-existence upper limit disagrees by source (~4e-4, ~9.5e-4 to 1.2e-3, "Saturn-mass").
- Width scaling (via search, https://arxiv.org/pdf/1509.02276 and astro-ph/0302135): tadpole boundary delta a = (2 sqrt2 / sqrt3) sqrt(mu), in units of the planet's a, as quoted; horseshoe width scales ~mu^(1/3) (Hill-sphere separatrix) in one source and mu^(1/2) in a generalization; smaller mu allows larger libration amplitude.
- Pan's ringlet is a horseshoe population inside a gap (Section 1.1): a co-orbital structure and a gap in one body.

### 2.2 Real populations
- Jupiter Trojans (Wikipedia, Feb 2026 count): 9,738 at L4 (Greek), 5,931 at L5 (Trojan); L4/L5 about 1.6 (inferred from counts; ScienceAlert via search also says ~1.6x). Estimated true counts: L4 ~160-240k >2 km, ~600k >1 km; L5 comparable. Revised 2008-9 estimate 6,300 and 3,400 above 2 km (may be 2x high). https://en.wikipedia.org/wiki/Jupiter_trojan
- Same page: libration amplitude 0.6 to 88 deg, mean ~33 deg; libration period ~150 yr; each swarm ~26 deg along the orbit; inclinations to ~40 deg; a = 5.05-5.35 AU (mean 5.2 +/- 0.15); swarm width ~0.6 AU (two Hill radii); along-orbit extent ~2.5 AU; up to 17% unstable over the Solar System's age; collisional fragments below ~40 km; largest Hektor ~200 km.
- Asymmetry: observational bias is proposed (L5 lies toward the Galactic centre, crowded sky) and, separately, dynamical origins (fast outward Jupiter migration yields more stable L4 orbits, Li et al. via https://www.sciencealert.com/the-mysterious-asymmetry-of-jupiters-asteroids-may-finally-be-explained; capture during chaotic giant-planet evolution, Vokrouhlicky 2024 https://www.boulder.swri.edu/~bottke/Reprints/Vokrouhlick%C3%BD_2024_AJ_167_138_Debiased_Trojans.pdf). The sources do not settle the cause. Neptune Trojans: 27 at L4 and 4 at L5 on an undated mirror, first L5 found 2008 (https://en.wikipedia.org/wiki/Neptune_trojan, via search).
- Hildas, 3:2 with Jupiter: triangle ~1 AU sides, apices 20-40% wider, apices align with Jupiter's L5, L4, L3; a 3.7-4.2 AU (mean 3.97); e < 0.3; >7,000 members; 5.0-5.5 yr near aphelion at apices vs 2.5-3.0 yr on sides; period ~7.9 yr. https://en.wikipedia.org/wiki/Hilda_asteroid
- Cluster extent vs mass ratio (inferred): Jupiter mu is ~1e-3, so R_H/a = (mu/3)^(1/3) ~ 0.07 and R_H ~ 0.36 AU at 5.2 AU, consistent with the page's swarm width of ~2 R_H (0.6 AU). The observed Jupiter scale is therefore ~2 R_H radial width and ~26 deg (swarm) / ~33 deg (mean libration amplitude) along the orbit. Section 2.1 gives the sources' scalings with mu for other mass ratios.

### 2.3 Rails vs dynamics
- Translates directly (inferred): a Trojan on a rail is the parent's rail with phase offset +/-60 deg and the same period; a tadpole libration is a small superposed oscillation of phase and radius (period ~150 yr = ~12.7 Jupiter years, from the page) that a rail model can fake with a phase wobble or omit. The same-radius, same-period constraint is exactly what a rail system gives for free.
- Assumes dynamics: libration amplitude distribution, the tadpole/horseshoe separatrix, and the 17% leak are effects of the three-body problem; on rails they are authored.
- Aetheria constraint cross-reference (inferred): the planet's Trojan zone sits on the planet's own orbital radius, 60 deg away. Under `binaries-orbited-from-afar` a layout "on the same ring as a pair of bodies" was ruled out; the Trojan layout is a different case (same ring as one body, 60 deg apart) and the read of ZoneGenerator shows no check for it either way. Pull of the host planet near L4/L5 would also be inside the planet's reach only if reach exceeds the 60-deg chord (= radius of the orbit, an equilateral triangle) (inferred: chord to L4 equals the orbit radius D, while band reach is 1/2 D, so L4/L5 lie beyond the planet's own reach in the current law).

## 3. Oort-like and scattered-disk material at a well's edge

- Oort cloud: theorized ~2,000 to 200,000 AU; outer spherical region ~20,000-50,000 AU radius, inner torus ~2,000-20,000 AU (Hills cloud, Hills 1981, tens to hundreds of times as many nuclei as the outer cloud); outer edge set by the Sun's Hill sphere / Galactic tide truncation ~100,000-200,000 AU; isotropy attributed to giant-planet scattering followed by stellar passes, molecular clouds and Galactic tide randomizing inclinations; outer cloud possibly trillions of objects >1 km but only ~5 Earth masses total (Halley proxy). https://en.wikipedia.org/wiki/Oort_cloud
- Sun's Hill sphere: unstable outer boundary 230,000 AU (1.1 pc; 3.6 ly), per https://en.wikipedia.org/wiki/Hill_sphere. Other stable-fraction numbers (1/2, 2/3 of R_H) were not on that page (gap).
- Scattered disc: perihelion > 30 AU (about 30-35 closest), a > 50 AU out past 100 AU (one scheme to 2000 AU), e up to ~0.8, i up to ~40 deg; dynamically unstable under Neptune scattering, feeding centaurs/JFCs and outward to the Oort cloud. Detached objects (Sedna) have perihelion beyond Neptune's reach, Brown places Sedna in the inner Oort cloud. Kuiper belt ~30-50 AU: classical objects untouched by Neptune, resonant objects locked. https://en.wikipedia.org/wiki/Scattered_disc
- (gap) Kuiper cliff at ~48 AU (2:1 resonance edge): not found on the pages read.
- Distribution shapes, the three zones in order of distance from the star (inferred): flat-ish plane belt, thick plane-biased scattered disc (inclination to 40 deg, high e), isotropic sphere. Inclination spread grows with distance; edge is a tidal truncation, not a hard wall.
- Rails vs dynamics: the extent rule (Hill sphere of the host in the larger potential) is a static radius, rail-compatible; the isotropy is an outcome of long stochastic evolution, authored as a distribution on rails (inferred). Aetheria is a flat 2D zone with an orbital plane and layers (rulings on layers), so "isotropic" has no direct meaning there (inferred); the radial edge rule and the sparse mass are what translate. Aetheria's own edge law: `dark-beyond-populated-area` (light falls off outside the populated area, soft not hard).

## 4. Implementations

Evidence quality here is low: most sources were forum posts or search snippets; several primary sources would not load (Starsector javadoc returned HTTP 402; fandom wiki and docs.rs accrete source not readable).

| System | Rails or n-body | How these shapes are placed | Pointer |
|---|---|---|---|
| Elite Dangerous Stellar Forge | static, generated once | Newsletter #26: systems formed from first principles; matter accretes in protoplanetary discs over simulated time guided by chemistry and angular momentum; #36: stellar encounters may remove outer planets and capture others. Ring systems of planets and brown dwarfs reported unrealistically large (player analysis of Stellar Forge). Belt "clusters" are static; a player proposal wants them dynamic. Max 512 bodies per system (secondhand). (gap) the placement rule for belt clusters and rings was not found | https://mwomercs.com/forums/topic/146675-elite-dangerous/page__view__findpost__p__3654624, https://forums.frontier.co.uk/goto/post?id=5341120, https://forums.frontier.co.uk/goto/post?id=7453534 |
| Space Engine | Keplerian orbits (inferred from the forum), no n-body in results | Asteroid belts for imported systems made by a community Python tool (JackDole's Asteroid Belt Creator) from parameters; a user calls procedural moon orbits unrealistic. (gap) no source for Trojans, Kuiper belt or Oort cloud generation | https://forum.spaceengine.org/viewtopic.php%3Ft%3D262, https://forum.spaceengine.org/viewtopic.php%3Ft%3D604 |
| Starsector | rails | `StarSystemGenerator.addOrbitingEntities()` adds planets plus rings and asteroid fields; the OLD age setting yields mostly rings and debris, YOUNG a higher planet-to-debris ratio; entities take orbit angle, radius, days; hand-made systems place belts by authored radius. (gap) method signatures and Trojan handling not retrievable | https://starsector.wiki.gg/wiki/Star_System_Tutorial |
| Stellaris | no orbit simulation | belts need a fixed radius in generation scripts; off-centre belts and belts in binary systems unsupported (modder reports) | https://admin-forum.paradoxplaza.com/forum/threads/offcentered-asteroid-belts.1113769/latest, Steam Synth Collective changelog |
| EVE Online | static | each system's belts appear to be fixed per system; two geometries: ~50 km semicircle or ~70 km sphere; player observation only | https://wiki.eveuniversity.org/Asteroid |
| Oolite | placed by authored coordinates | asteroid fields at an authored radius with no check against planet orbits; vanilla clumps "don't resemble real belts"; an add-on places asteroids at Lagrange points | https://bb.oolite.space/viewtopic.php?p=14995 |
| Dole / Accrete / StarGen and ports | static output | Dole 1969 RAND "Formation of Planetary Systems by Aggregation"; Rust port `accrete`: parameters dust_density_coeff A, dust-to-gas k, b critical mass for gas giants, post_accretion_intensity; outputs planets plus a moons and rings stage. (gap) dust-band sweeping and effect limits not read; StarGen source (gryphel.com) not opened | https://docs.rs/crate/accrete/latest, https://www.gryphel.com/c/sw/astro/stargen |
| Universe Sandbox | n-body | Trojan made by placing a ring and converting a body to a Trojan, per a community guide citing preconditions (primary >= 25x secondary; secondary >= 10x body) | https://gamepretty.com/universe-sandbox-how-to-make-a-trojan-2021/ |
| Kerbal / Principia | stock KSP: patched conics on rails; Principia: n-body | a forum thread reports Principia kicking Bop and Vall out via Tylo; no source on Principia Trojans; stock KSP Lagrange points not simulated (forum title) | https://github.com/mockingbirdnest/Principia, https://forum.kerbalspaceprogram.com/topic/205593-lagrange-points-not-simulated |
| Celestia | (gap) nothing found | | |
| Endless Space | (gap) not searched (budget) | | |

Failure modes found in sources:
- Elite ring systems too large (player analysis).
- Stellaris off-centre belts silently dropped when added as a child of a star; no belts in binaries.
- Oolite belts placed by coordinates unrelated to planetary orbits.
- Principia: moons ejected by neighbours (an n-body failure, not a placement one).
- Nothing found in these sources about placement inside another body's Roche or Hill sphere (gap): the only Roche/Hill rules found are the physics ones above.

What decides where clusters may sit, per source: authored radius (Oolite, Stellaris, Starsector hand-made, EVE); accretion outcome (Dole/Stellar Forge); resonance/Hill-radius criteria are in the literature only (Sections 1-3). No implementation found ties belt or cluster location to a Hill-radius or resonance rule (inferred from absence in what was read, not shown).

## 5. Gaps in this pass
Space Engine, Celestia, Endless Space, Stellar Forge placement rules, StarGen source, Starsector API signatures, the Kuiper cliff, Lindblad-resonance gap-opening criterion for rings (Lissauer-Shu-Cuzzi 1981, Goldreich-Tremaine 1980 not opened), exact horseshoe and tadpole prefactors (sources disagree). Budget was 40 calls.
