# Fauna placement: prior art and a probe

Imagination, agent `imagination-aeth-fauna-r2`, session self-2026-10-13-ag2, for campaign `aetheria-fauna`. It
answers one question: how should the zone generator decide where fauna live, so that placement "tells an ecological
story ... an ecology shaped by the level" (ruling `fauna-habitat-ruled`)? Self proposed a seeded CPU Physarum pass
at zone generation, with closed-form gradient placement as the alternative. Citations were checked on the web on
2026-10-13 unless marked `[M]` (from memory, unverified).

## Sources

1. **Jones 2010.** J. Jones, "Characteristics of pattern formation and evolution in approximations of Physarum
   transport networks", *Artificial Life* 16(2):127-153, 2010, doi 10.1162/artl.2010.16.2.16202 (UWE repository
   record https://uwe-repository.worktribe.com/output/980579). Particle agents sense a trail map with three sensors,
   turn toward the strongest, move and deposit; the map diffuses and decays. Parameter maps give characteristic
   patterns. Self's citation is correct.
2. **Jones 2011.** "Influences on the formation and evolution of Physarum polycephalum inspired emergent transport
   networks", *Natural Computing* 10(4):1345-1369, doi 10.1007/s11047-010-9223-z. The same model approximates Steiner
   trees, relative neighbourhood graphs and convex and concave hulls of its food sources. This matters: the network a
   Physarum pass draws between wells is a cheap Steiner-like graph linking them.
3. **Tero et al. 2010.** "Rules for biologically inspired adaptive network design", *Science* 327(5964):439-442,
   doi 10.1126/science.1177894. The organism, with food at the Tokyo-area cities, grew a network comparable in cost,
   efficiency and fault tolerance to the rail system; a flow-reinforcement model reproduces it.
4. **Monte Carlo Physarum Machine.** Burchett et al., ApJ Letters 2020, and Elek et al., arXiv 2204.01256 (2022):
   Jones's agent model, made probabilistic and 3D, fed galaxy positions and masses, reconstructs the cosmic web's
   filaments. Polyphorm (IEEE VIS 2020) is the interactive tool. This is the closest prior art to the operator's
   experiment: a slime-mould agent model fed by gravitating masses draws filaments between them.
5. **EcoBrush.** Gain, Long, Cordonnier and Cani, *Computer Graphics Forum* 36(2):63-73, 2017, doi
   10.1111/cgf.13107. Ecosystem simulations are run offline, their plant distributions are stored in a database
   indexed by terrain conditions (slope, light, moisture, temperature), and a terrain is populated by lookup and
   interpolation, much faster than simulating it. This is the "simulate once, distil into a closed form" hybrid.
6. **Cordonnier et al. 2017.** "Authoring landscapes by combining ecosystem and terrain erosion simulation", *ACM
   ToG* 36(4), doi 10.1145/3072959.3073667. Vegetation and erosion simulations feed each other; placement is the
   outcome of a coupled simulation, not a rule.
7. **D8 flow accumulation.** O'Callaghan and Mark 1984, *Computer Vision, Graphics and Image Processing* 28:323-344.
   Each cell drains to its steepest neighbour, and accumulated upstream area marks channels. It is the standard
   closed-form way to derive "where things collect" from a heightfield. On a gravity heightfield it collects into
   well cores, which is where the lore says fauna are torn apart, so it answers the wrong question here.
8. **Maxent.** Phillips, Anderson and Schapire 2006, *Ecological Modelling* 190:231-259. Species distribution
   models fit habitat suitability as a function of environmental covariates. It is the closed-form reading of
   habitat: suitability is a function of local conditions, with no network between patches.
9. **Physarum parameters.** The programmable Physarum machines paper (arXiv 0901.4556) reports that the ratio of
   rotation angle to sensor angle tunes the network from contracting to branching. The operator's prefab varies
   both with slope, so the network contracts on flat ground and branches on slopes.
10. **Float determinism.** Transcendentals (sin, cos, acos) can differ across platforms and runtimes (Factorio's
    developers hit it; Unity's forum reports x86 versus ARM physics divergence). A chaotic agent model amplifies a
    last-bit difference into a different map. A placement computed this way must be computed once and saved, never
    recomputed on another machine.

## Probe (real code, Yggdrasil)

A scratch xunit test (`2026-10-13-fauna-placement-probe/FaunaPlacementProbeScratch.cs.txt`, never merged) at
`1746e8bd`, run on Yggdrasil (dotnet sdk 10, Release, 4 CPUs; 1 test passed; output in
`2026-10-13-fauna-placement-probe/output.txt`). The zone is synthetic: radius 2000, a p50 sun at the centre, two
p50 planets, a p50 gas giant with a moon, a planetoid, built with the real `Zone`, the real `PlanetSettings` values
and `Zone.GetNormal`. The agent rules are the operator's `Slime.compute` (`UpdateAgents`, `Diffuse`) transliterated
to C#, with her `Slime Renderer.prefab` parameter values, 40,000 agents, 600 steps of 0.1 s, a 256² trail map, and
a hash RNG.

| Measure | Result |
|---|---|
| Cost | 8.6 to 9.4 s single-threaded for 40k agents × 600 steps on a 256² map. Agent-steps dominate; 10k × 300 on 128² should be about 1 s (extrapolated, not run). The slope grid (65k `GetNormal` calls) took 0.2 s. |
| Run-to-run determinism | Two runs: identical FNV hash of the trail map (bit-for-bit). Across platforms: not tested, and not expected (source 10). |
| Difference from closed form | Correlation of the trail map with a closed-form suitability map (her `Deposition(slope)`) is 0.28, and 0.29 against that map blurred. The Physarum pass is not a smoothed slope map: it draws filaments. |
| Where the trail lies | By `u` = distance to the nearest well over its support radius: core (`u` < 0.1) 5.7 %, 0.1 to 0.3 35.6 %, 0.3 to 0.6 54.1 %, 0.6 to 1 4.7 %, outside every well 0 %. The trail sits on the shoulders of the wells, avoids the cores and links wells by filaments. This is the lore's "bloom on the shoulder of each well ... filaments that link one body's cloud to the next" without anyone writing it. |
| Effect of the flow | Adding the live global flow to agent motion changes the map completely (correlation with the no-flow map 0.34). |
| The live flow's speed | `Settings.asset` has `GlobalScale` 512, `GlobalAmplitude` 15 and `SlopeAmplitude` = `SwirlAmplitude` = 0. The horizontal global flow averages 6.06 u/s (max 12.2) over the zone, the same as the rail speed 2π = 6.28 u/s. Anything that drifts with the flow moves as fast as orbits do. |

The ASCII maps in `output.txt` show the slope field, the closed-form map and both Physarum maps.

## Reading

- The operator's experiment is the cosmic-web method (source 4) in miniature. What she liked, patterns that link
  the wells, is what a Physarum pass does and a local suitability rule cannot: a suitability function of local
  slope (Maxent's shape, source 8) has no links between patches. The probe's 0.28 correlation measures the gap.
- A closed form can draw the links if it is told to: Jones 2011 says the network approximates a Steiner tree or a
  relative neighbourhood graph of the food sources, so filaments along the RNG of the zone's bodies and wormhole
  mouths, plus a shoulder band per well, is a deterministic closed-form imitation. It gives straight, legible links
  and loses the organic branching.
- EcoBrush's hybrid (simulate offline, distil into a lookup by local conditions) keeps the cost low but, like any
  local lookup, cannot reproduce zone-specific topology.
- The cost (seconds at the probed size) and the chaos (source 10) are both answered by running the pass once, when
  the zone is generated, and saving what it produced: the swarm fields, as belts are saved in the zone pack. The
  trail map is a generation intermediate, never state.

## Recommendation for the placement question

`physarum-at-generation`: run the operator's rules (her parameters, moved into a `ServerShared` settings record)
once in `ZoneGenerator`, fed by `Zone.GetNormal` and the zone's flow field, seeded from the zone seed, with food
sources at each well's shoulder and at wormhole mouths (the lore's springs); sample its trail map to place and
shape fauna fields; save the fields; discard the map. Budget it to about a second (fewer agents and steps, a 128²
map) and measure that in the cut. Confidence: moderate-high for the look (the probe shows the filaments and the
shoulder band); moderate for the cost, which is extrapolated below the probed size.
