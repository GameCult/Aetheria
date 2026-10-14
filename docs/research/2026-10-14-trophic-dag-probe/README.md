# Trophic DAG probe (2026-10-14)

Serves rulings `aetheria-fauna:ruling:habitat-trophic-dag` and `:voronoi-primitive`, and the questions
`aetheria-fauna:question:skein-filament-construction` and `:trophic-graph-tape`. Facts are map rows B28-B36 in
`docs/aetheria-fauna-map.md`; prior art is `docs/research/2026-10-14-trophic-dag-filaments-prior-art.md` (`TD:<line>`).

`trophic_probe.py` imports `habitat_probe.py` (the float64 transliteration of `Zone.GetHeight`/`GetNormal` and
`FlowField`, in `../2026-10-14-fauna-habitat-probe/`) and adds what that probe lacked: the wells orbit, as
`Zone.GetOrbitPosition` moves them (period = distance in seconds, `Settings.asset` `OrbitPeriod` exponent 1,
multiplier 1; about 6.3 u/s for every planet). The flow's slope terms are on (swirl 6, slide 1.5, as B25g). Its hash
is the probe's own integer hash, not CultMath `pcg3d`; images are qualitative evidence for a C# law, not the law.

Filament candidates (krill habitat density, each times the steep-core mask):

| Name | Construction |
|---|---|
| `band` | The operator's `Deposition(slope)` alone (r3's baseline, B25a). Control, not an option. |
| `skein` | Strands: a Gabriel graph over the zone's wells (TD:31 [V]) with a soft gate (the empty-disc margin through a smoothstep), each edge a Gaussian strand of width 28 u + 3 % of its length, bent by a Zel'dovich warp of the flow, `x - 20 s * u(x)` (TD:39 [V]). |
| `skein-hard` | `skein` with the hard 0/1 Gabriel gate, for the pop count. |
| `lattice` | The operator's reference node: a lattice smooth Voronoi (260 u cells) with her hashed per-site multiplicative weight, raised by up to 1.6x near wells so the cells shrink on the shoulders; output is the border (her smooth-min gap, and F2 - F1 at width .1 cell), warped by the flow for 6 s, times the band widened (`band^.75`). Sites drift on zone time (.05 rad/s). |
| `skein-web` | `skein` plus `lattice` hanging off the strands: the web is gated by a strand halo (4x width) or the band. |
| `slack` | A multiplicatively weighted Voronoi of the wells (weight = gravity radius), distance to the cell border to first order, `(F2 - F1) / |grad(F2 - F1)|`: the saddles where pulls cancel (Apollonius circles round small wells, TD:20 [M]). |

Trophic layers over `skein-web` (`layer_*.png`): `L1-glim` (starlight inside the sun's light radius plus heat-light on
warm well floors, fed along the Skein), `L2-krill` (Skein times a Holling-II saturation of glim, TD:55 [S]),
`L2-minnows` (Holling of krill averaged over the point and 8 taps on a 48 u ring), `L3-sharks` (minnows over a
160 u ring, times the band), `L3-lanterns` (slack times krill over a 120 u ring).

Images: `density_<candidate>.png` (zone 4000 u across, t = 0, absolute density, red = bodies), `members_<candidate>.png`
(hosted krill candidates at 16 u, fixed thresholds .1 + .9h), `layer_<layer>.png`, `members_L2-minnows.png` (48 u),
`members_L3-sharks.png` (128 u), `members_L3-lanterns.png` (64 u).

Logs: `run1.log` (the full run, 1,211 s), `win.log` (search window and weight range against presence steps),
`cyc.log` (the lattice drift read at the member's cycle start).

To rerun: copy this folder and `../2026-10-14-fauna-habitat-probe/habitat_probe.py` into one scratch directory
(the scripts write PNGs next to themselves), then `py -3 -u trophic_probe.py 256` (about 20 minutes),
`py -3 -u winprobe.py`, `py -3 -u cycprobe.py`. `habitat_probe.py` reads the B20 Physarum map from
`C:\aeth-wt\fauna-target-r3\docs\research\2026-10-13-fauna-placement-probe\output.txt`; edit the path if the
worktree moved. Python 3 only, no packages.
