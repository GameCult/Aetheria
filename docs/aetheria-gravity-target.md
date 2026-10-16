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

Ink zones are not under this law. Their pillars stand where suns would
(`aetheria-fauna:ruling:ink-pillars-are-suns`), but the Ink need not obey
flatspace gravity: the operator sketched a pillar as a negative-gravity
singularity with a bowl around it, a Bundt tin, plus a swell ships feel
(`aetheria-fauna:ruling:ships-feel-swell`). That law is
`aetheria-release:follow_up:ink-gravity-law`, and it plugs into the potential
owner this campaign builds.

## Rulings of 2026-10-14

The forks the first Imagination pass raised were ruled the same day
(`aetheria-gravity:ruling:*-ruled`): the field owns the orbits; gravity starts
at about 0.5 u/s^2 at a median planet's orbit; pull comes from mass through an
authored compressed curve; children are placed within their parent's reach;
each well's soft core has its own authored radius and never levels the field
out where bodies orbit; one global exaggeration; the grid's height carries
gravity only, and the wormholes' presentation is provisional; ships feel only
the potential. The flatspace bowl and ripples are deleted, not disabled: the
map found them live in `Settings.asset` and `Zone.GetHeight`. The model page and
body facts are in `docs/aetheria-gravity-map.md`.

## Not here

Portals and portal gravity, and non-Euclidean zone geometry, belong to the
pseudospace-flavours line (`aetheria-release:follow_up:portals-and-portal-gravity`,
`aetheria-release:follow_up:pseudospace-geometry-owner`).
