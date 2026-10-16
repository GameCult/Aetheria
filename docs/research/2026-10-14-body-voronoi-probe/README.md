# Body-Voronoi probe (2026-10-14)

Serves the operator's sketch after `aetheria-fauna:ruling:skein-filament-construction-ruled`: "I was imagining the
voronoi cells to be the gravity objects themselves, aren't there a bunch of fun ways to play with the distance
metrics to get filaments that way too?" Facts are map rows B37-B42 in `docs/aetheria-fauna-map.md`. Prior art:
CultLib `docs/research/tape-register-model.md` section 5 (`dfc720e7`) and `docs/research/lattice-site-weight.md`
(`21281a39`).

`body_voronoi_probe.py` imports `trophic_probe.py` (`../2026-10-14-trophic-dag-probe/`: orbiting wells, slope terms
on, the flow warp, the lattice web, the members and pop probes) and replaces only the large-scale filament layer.
Same zone, same frame (t = 0), same 16 u krill candidates and thresholds, same fade .1.

**The large layer.** A diagram over the zone's six bodies (sites = bodies, read now), drawn as an edge profile: the
distance to the border to first order, `(F2 - F1) / |grad(F2 - F1)|`, through a Gaussian of half-width 40 u. Each
body's weight is its well's Gaussian width `sigma = r / sqrt(128)` (the zone's `pp(d/r)^16` term is about
`exp(-d^2 / 2 sigma^2)`): sun 482 u, planets 180, 180, 146, 103, gas giant 305. Bent by the same flow warp as the
strands (20 s).

| Variant | Metric `d_i` |
|---|---|
| `pot-well` | Equal well depth on the zone's real profile: `-ln(depth_i * pp(d/r_i)^16)` |
| `pot-newton` | Equal point-mass potential `m/d`: multiplicative with weight = mass (section 5a's identity) |
| `mult` | `d / sigma_i`: Apollonius circles, her shader's trick |
| `add` | `d - sigma_i`: hyperbola arcs |
| `power` | `d^2 - sigma_i^2`: straight edges, the adhesion model's tiling |
| `flow-aniso` | `mult` with the across-flow component stretched 2.5x; the flow direction is the warp's own |
| `lp05`, `lpinf` | `mult` in the Minkowski p = .5 and p = inf (Chebyshev) norms, axis-aligned |
| `skein-web` | Control: the ruled Skein (`trophic_probe.py`), strands plus web |
| `mult-strands`, `power-strands` | The diagram's edges and the strands together, with the web |

**The web.** `trophic_probe.lattice_web` with two changes: each lattice cell's hash weight is raised by up to 1.6x by
the large-edge profile (3x wider) at the cell centre, `(cx + .5) * LAT` (cell-centre weight in
`lattice-site-weight.md`'s terms; `trophic_probe.py`'s well proximity is also read at the cell centre, not at the
jittered site), and the cell metric is stretched up to 2x across the large edge's normal at the query point, so cells
shrink and align along the edges. The web hangs within 4 edge widths of a large edge, or on the band.

Images (zone 4,000 u across, red = bodies): `density_<variant>.png` (krill habitat density, t = 0),
`members_<variant>.png` (hosted krill candidates), `edges_<variant>.png` (the large layer alone, no web; also for the
pruned variants).

Logs:
- `run1.log`: all variants as `trophic_probe.py` reads the flow (scroll at the member's cycle start, the slope terms
  with the wells now). `run2.log` and `run3.log` (`power-strands`): the whole warp at cycle start, slope terms
  included (`WARP_AT_CYCLE=1`). The member images here are run 2 and 3's.
- `diag_edges.log`: the large layer alone, run-1 reading. `trace.py`: the worst step traced to the warped point
  moving 1.4 u per 1/60 s.
- `diag2.log` (run-2 reading): web ablation (boost, align) and the lattice window, 5x5 against 7x7.

To rerun: copy this folder, `../2026-10-14-trophic-dag-probe/trophic_probe.py` and
`../2026-10-14-fauna-habitat-probe/habitat_probe.py` into one scratch directory, then
`py -3 -u body_voronoi_probe.py 256` (about 15 minutes), the same with `WARP_AT_CYCLE=1` (writes into `cyc/`),
`py -3 -u diag.py` with `DIAG=edges`, and `WARP_AT_CYCLE=1 py -3 -u diag2.py`. `habitat_probe.py` reads the B20 map
from `C:\aeth-wt\fauna-target-r3\docs\research\2026-10-13-fauna-placement-probe\output.txt`. Python 3 only, no
packages. Hashes are the probe's own integer hash, not CultMath `pcg3d`; the images are qualitative evidence.
