# Aetheria gravity: target

Campaign `aetheria-gravity`. Opened 2026-10-14 on operator ruling
`aetheria-release:ruling:gravity-honest-wells-agreed`. Its invariants,
not-in-scope list and canonical implementations are the typed target
`aetheria-gravity:target:r1`; this page keeps the rationale.

## Why

The orbit probe (`docs/research/2026-10-13-orbit-probe/report.md`) measured
today's wells against the real code:

- The heightfield is not a potential. Each well is a positive core plus an
  equal negative-density ring (antimass) out to its rim, so it is
  mass-neutral and nothing is felt outside the rim.
- The force is `S*R*|grad h|^2` behind a slope gate (`|n_xz|^2 > 0.001`),
  which zeroes it in every core, at every rim, and in every zone bowl wider
  than about 3117 units.
- The outer half of every well has unstable circular orbits, and about 40%
  of moons sit there.
- The wells pull 20 to 70 times harder than the OrbitPeriod rail implies, so
  holding a rail orbit costs a real fraction of a ship's thrust.

The operator's frame (2026-10-13): bodies sit above the grid so there is always
room to fly beneath them, so being within a sun's radius is not being inside
it; gravity is shaped that way on purpose, and the picture is why it looks as
it does. "We can use correct wells and simply exaggerate the height."

## The shape

One scalar potential per zone owns gravity. Each body contributes a 2D
point-mass well: logarithmic potential and force falling as 1/r outside the
body's radius, softened inside it (force linear to zero at the centre, matched
in value and slope at the radius, Plummer-like). Wells add. Force is the
potential's gradient, with no gate. The drawn height is the potential times one
exaggeration factor, so what the player sees and what the ship feels are the
same function. With a 1/r tail every circular orbit is stable.

In Ink zones the Adrasteia pillars stand where suns would
(`aetheria-fauna:ruling:ink-pillars-are-suns`): a pillar is the zone's central
body, and by this campaign its well follows the same law.

## What the first Imagination pass settles (step 0b)

- Whether the rails (OrbitPeriod) are derived from the field, or the field is
  calibrated to the rails. Today they disagree by 20-70x in force; one must own
  the other.
- What becomes of the zone bowl and of the gas-giant and sun waves: parts of
  the potential, a separate push, or gone.
- Hull drag and real ship acceleration (unmeasured by the probe), which decide
  whether a free orbit is possible and how much lighter near-body flight feels.
- Every reader of `Zone.GetHeight`, `GetForce` and `GravityAcceleration`
  (sim, AI, renderer, shaders), so the potential has one owner and the height
  is derived from it everywhere.
- Follow-up `aetheria-release:follow_up:zone-orbits-unplaced-before-first-tick`:
  a zone must be valid before its first tick, or every well sits at the origin.

## Not here

Portals and portal gravity, and non-Euclidean zone geometry, belong to the
pseudospace-flavours line (`aetheria-release:follow_up:portals-and-portal-gravity`,
`aetheria-release:follow_up:pseudospace-geometry-owner`).
