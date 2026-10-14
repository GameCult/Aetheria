<!-- Prior art for campaign aetheria-gravity, step 0b. Serves questions: aetheria-gravity:question:rails-vs-field, :well-strength-from-mass, :field-strength, :moons-beyond-tidal-reach, :softening-radius, :exaggeration-scope, :grid-presentation-noise, :flow-moves-ships. Imagination imagination-ag2-gravity-0b, session self-2026-10-14-ag2, at master c6519383. -->
# Gravity prior art: 2D log wells, softening, and how games reconcile rails with felt gravity

A short pass, not a survey. Each section ends with what it advises for an Aetheria fork. Numbers marked "computed" come from scripts over the orbit probe's 31,640 generated bodies (`docs/research/2026-10-13-orbit-probe/data/bodies.csv`); the scripts are in the Imagination scratchpad and are restated in the map's body facts.

## 1. The physics: a point mass in a plane

- In two dimensions Gauss's law gives a = 2GM(<r)/r, the potential of a point mass is logarithmic, and circular speed v = sqrt(2GM) is the same at every radius (a flat rotation curve). Binney and Tremaine, *Galactic Dynamics*, treat the logarithmic potential and the Mestel disc as the standard flat-rotation-curve models. Today's OrbitPeriod rail (T = d, v = 2π for every body) is already this law, with one gravitational parameter c = v² = 4π² shared by every body regardless of mass (probe, "Point mass, disc").
- Consequence that matters for design: a·r = v². In an honest field you cannot have strong felt gravity at an orbit and slow bodies on that orbit. At r = 610 (a median planet around a median sun), a = 5 u/s² (today's peak) means v = 55 u/s, T = 69 s; a = 0.5 means v = 17.5 u/s, T = 219 s; the rail's a = 0.065 means v = 6.3 u/s, T = 610 s (computed).
- Log tails never end. Superposed, every body's tail reaches the whole zone, so the potential has no natural zero: a reference constant must be chosen (the map proposes zero at the zone's radius from the root body).
- The rubber-sheet picture is this physics, not only a metaphor. A tensioned membrane under a point load deflects logarithmically (Laplace's equation in 2D), and marble orbits on it follow a Kepler-like law in the small-slope regime (White and Walker, AJP 2002; Middleton and Langston, [Circular orbits on a warped spandex fabric](https://arxiv.org/pdf/1312.3893), AJP 2014; [elliptical-like orbits](https://arxiv.org/pdf/1601.03996)). The fabric's own mass dominates for small central loads, which is today's zone bowl in physical form.

Advises: the target's "height = potential × exaggeration" is the established picture; the drawn grid is an embedding of φ, and its readability is set by the log, not by the factor (section 5).

## 2. Softening

- Plummer softening replaces 1/r with 1/sqrt(r² + ε²); in 2D the analogue is φ = GM ln(r² + ε²), force 2GM r/(r² + ε²): linear near the centre, approaching 2GM/r only for r ≫ ε (at r = ε it is half the point-mass value). It biases the field at all radii near ε ([Rodionov and Sotnikova 2005](https://arxiv.org/pdf/astro-ph/0504573); [Dehnen 2001](https://arxiv.org/abs/astro-ph/0011568)).
- Dehnen found compact kernels (a density of finite extent, exactly Newtonian outside it) give about half the force error of Plummer at equal cost. The 2D compact kernel that matches the target's wording is the uniform disc: force 2GM r/R² inside, 2GM/r outside, potential quadratic inside and logarithmic outside, continuous in value and gradient at R (the force itself has a kink at R, which is harmless for integration at game time steps).
- The softening length is the playability radius: it sets the peak force, c/R. Today's wells peak at the slope-peak radius (0.18 of the support radius: planetoid 104, planet 183, gas giant 310, sun 490), not at the body radius (7 to 33).

Advises: use the uniform-disc (compact) core, not Plummer, so the tail is exactly the point mass the target names; choose R as its own authored quantity (question softening-radius).

## 3. Rails versus felt gravity in games

| game | bodies move by | ships feel | reconciled how |
|---|---|---|---|
| Kerbal Space Program | Kepler rails (closed-form conics) | the same μ as the rails, one body at a time (patched conics, spheres of influence) | rails and ship physics share each body's μ; moons do not perturb a vessel in its parent's SOI ([KSP PatchedConics API](https://kerbalspaceprogram.com/ksp/api/class_patched_conics.html); [forum explainer](https://forum.kerbalspaceprogram.com/topic/124639-what-are-patched-conics)) |
| Outer Wilds | real-time simulation, no rails ([Reverse Shot](https://reverseshot.org/features/2984/outer_wilds)) | the same field | exaggerated gravity, swift orbits; inverse square kept only for the sun, planets fall off more linearly because inverse square made escape too easy on island-sized planets ([PC Gamer](https://pcgamer.com/how-to-build-a-black-hole); [Vice](https://vice.com/en/article/how-outer-wilds-developers-mobius-digital-made-space-physics-fun)) |
| Elite Dangerous | rails, no perturbations ([Frontier forum](https://forums.frontier.co.uk/threads/are-the-orbits-of-the-planets-physically-correct.394502/post-6203566)) | no gravitational force; supercruise speed is capped near mass | the conflict is avoided by not having felt gravity at all |
| Starsector, most 2D space ARPGs | campaign-map rails | nothing | same as Elite |
| Spacewar! (1962) | one fixed star | the star | no rails to reconcile |

Pattern: every game where ships feel gravity derives the rails from the same parameter the ship feels (KSP) or drops rails entirely (Outer Wilds). The only games with authored rails that disagree with the field are the ones where ships feel no field. Aetheria today is the one combination nobody ships: authored rails and a felt field 20 to 70 times stronger.

Outer Wilds is also the precedent for a non-physical falloff chosen for feel (planets ~linear). The target has already chosen 1/r (the 2D point mass), which is the stable, honest choice; the feel lever left is strength, not exponent.

Advises: rails-vs-field recommends field-owns-rails (KSP's arrangement): Orbit.Period is derived from the parent's gravitational parameter.

## 4. Hierarchy: moons inside a parent that is inside a sun's field

- KSP resolves hierarchy by truncation: inside a sphere of influence only that body pulls. That is exactly the rim the target abolishes (point-mass-tail, wells-superpose), so it is prior art for what we are not doing.
- With superposed 1/r tails the sun's tide is strong, because its force falls only as 1/r. For a parent at R from its own parent, the 2D tidal (Jacobi-like) radius is d_H = R·sqrt(c_p / (2 c_g)) (rotating frame, flat rotation curve; derived the same way as Binney and Tremaine's Jacobi radius). Computed over the generated bodies, the fraction of children of non-root parents inside 0.5·d_H (where a prograde orbit is reliably bound) is: 11% if c ∝ mass, 35% for c ∝ m^0.5, 59% for m^0.25, 66% for m^0.175 (today's depth curve exponent). For children of planets it is 1%, 10%, 30% and 38%.
- So under an honest superposed field, most moons orbit where a ship could not follow them. Rails keep the bodies moving regardless; a ship's free trajectory near a moon would be torn toward the sun.

Advises: questions well-strength-from-mass and moons-beyond-tidal-reach; recommended fix is to place children within reach at generation, with a compressed mass-to-strength curve.

## 5. Exaggeration and readability under a log

- Drawn depth of one well between radius R and distance D is (c/K)·ln(D/R), K being the exaggeration (u/s² per unit height). The ratio of a sun's well to a planetoid's at the same log span is c_sun/c_planetoid: 488 if c ∝ mass, 22 for m^0.5, 4.7 for m^0.25, 2.95 for m^0.175 (median masses 14152 and 29). The factor K scales every well together and cannot change that ratio.
- Per-class factors would draw slopes that do not match the force, which breaks grid-shows-how-you-move. Data visualisation's usual move for wide dynamic range is a compressing transform (log scale); here the compression already lives in the physics if the strength curve is compressed, so the drawing can stay linear in φ.

Advises: exaggeration-scope recommends one global K; readability is decided by the strength curve (well-strength-from-mass).

## 6. Fields with curl cannot be drawn as slopes

- Helmholtz: a 2D vector field splits into a gradient part (which has a height) and a divergence-free rotational part (which has none), plus a harmonic remainder. A velocity-dependent push (drag toward a moving medium, a = -k(v - u)) is not a gradient of anything either.
- Today's flow (`FlowField`: globalFlow noise plus slopeFlow swirl and slide) has a rotational part by construction (the swirl term is the gradient rotated 90 degrees). It moves stardust and nebula, and by `aetheria-fauna:ruling:sim-knows-the-flow` fauna; it does not move ships (no ship reads `Zone.Flow`).

Advises: flow-moves-ships recommends that in flatspace ships feel only the potential, so the grid can show everything that moves them; a pseudospace push with curl needs a second visual channel (stardust streaks already draw the flow) or must be restricted to its gradient part. Recorded as a constraint for pseudospace work, not solved here.

## Sources

- [Circular orbits on a warped spandex fabric (Middleton and Langston)](https://arxiv.org/pdf/1312.3893)
- [Elliptical-like orbits on a warped spandex fabric](https://arxiv.org/pdf/1601.03996)
- [Optimal choice of the softening length and time-step in N-body simulations (Rodionov and Sotnikova)](https://arxiv.org/pdf/astro-ph/0504573)
- [Towards optimal softening in 3D N-body codes (Dehnen 2001)](https://arxiv.org/abs/astro-ph/0011568)
- [KSP PatchedConics API](https://kerbalspaceprogram.com/ksp/api/class_patched_conics.html), [What are patched conics (KSP forum)](https://forum.kerbalspaceprogram.com/topic/124639-what-are-patched-conics)
- [Recreating gravity in games is harder than you'd think (PC Gamer)](https://pcgamer.com/how-to-build-a-black-hole), [How Mobius made space physics fun (Vice)](https://vice.com/en/article/how-outer-wilds-developers-mobius-digital-made-space-physics-fun), [Reverse Shot on Outer Wilds](https://reverseshot.org/features/2984/outer_wilds), [An astrophysicist measures Outer Wilds](https://thephysicsmill.com/2024/09/20/an-astrophysicist-attempts-to-measure-the-physics-of-outer-wilds/)
- [Are the orbits of the planets physically correct? (Frontier forum)](https://forums.frontier.co.uk/threads/are-the-orbits-of-the-planets-physically-correct.394502/post-6203566), [Elite Dangerous wiki](https://elite-dangerous.fandom.com/wiki/Anti_gravity)
- Binney and Tremaine, *Galactic Dynamics* (2nd ed., 2008): logarithmic potential, Mestel disc, Jacobi radius. White and Walker, "The shape of 'the Spandex' and orbits upon its surface", Am. J. Phys. 70, 48 (2002), cited via Middleton and Langston.
