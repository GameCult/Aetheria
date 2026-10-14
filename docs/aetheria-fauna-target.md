# Aetheria spaceborne fauna: target

Campaign `aetheria-fauna`, target content r4 (r1 was admitted from the r0 draft at `f9705d32`, r2 by Self; r3 and
r4 are admitted by Imagination). Repo `GameCult/Aetheria`, working branch `master`, cut branches
`eureka/aetheria-fauna-<cut>`; the DAG's node primitives land in `GameCult/CultLib` (CultMath). Written by
Imagination (agents imagination-aeth-fauna, imagination-aeth-fauna-r2, imagination-aeth-fauna-r3 and
imagination-aeth-trophic-dag, sessions self-2026-10-13-ag2 and self-2026-10-14-ag); r4 is against `origin/master`
`7633408f` and CultLib `6c902fd4`. Trophic DAG prior art: `docs/research/2026-10-14-trophic-dag-filaments-prior-art.md`
(`TD:<line>`); its probe: `docs/research/2026-10-14-trophic-dag-probe/`. Body facts and probes are in `docs/aetheria-fauna-map.md` (cited below as `B<n>`); prior art is
`docs/research/2026-10-13-fauna-prior-art.md` (`PA:<line>`; `[M]` items are from memory and unverified),
`docs/research/2026-10-13-fauna-placement-prior-art.md` (placement by simulation) and
`docs/research/2026-10-14-fauna-placement-closed-form.md` (`CF:<line>`; its tags: `[V]` read, `[S]` search summary
only, `[M]` memory or derivation, unverified). This is the target and the model page. Open forks are questions in
the mind under `aetheria-fauna`.

r4 applies `habitat-trophic-dag` (2026-10-14), after the r3 density drivers missed the look:

> I'm not getting the filament feeling. I think we need a few layers of ecological abstraction, all deterministic, to
> author the various species with. The trophic levels should form a DAG which we can evaluate in either shader
> passes or in C#

with `voronoi-primitive` (a weighted Voronoi node, her reference shader a sketch of the look) and her correction,
relayed by Self: "Parity between shader and C# is CultMath's whole raison d'etre". Placement becomes a layered,
donor-controlled trophic DAG whose filament layer is the lore's Skein, whose nodes are CultMath functions with HLSL
twins, and which Aetheria composes ("Placement law" below; map section r4). The krill carriage of
`habitat-no-pop-ruled` stands and is extended to every fast term (map B34).

r3 applies `fauna-placement-ruled` (2026-10-13), against the recommendation to run a Physarum pass at generation:

> Nothing we generate ahead of time would suffice, because the level is constantly changing. I'd imagine we can come
> up with something deterministic based on the gravity slope and flow that makes nice patterns.

Placement is therefore a pure function evaluated when it is asked, never a generated and saved product. The
invariant `fauna-placement-seeded` ("the saved zone pack is the authority") is replaced by the placement invariants
below. r3 also carries the rulings admitted after r2: drift by advected cycles (`fauna-flow-coupling-ruled`), loose
matter drifts with the flow and ships do not (`flow-moves-what-ruled`), the slope terms are turned on
(`flow-slope-terms-ruled`), and presenters take their phase from zone time (`presenter-phase-from-zone-time`).

r2 applies the operator's rulings of 2026-10-13: fauna attack ships in various ways (`fauna-interaction-ruled`),
are harvestable and prominent in Elysium (`fauna-harvest-ruled`), are placed to tell an ecological story shaped by
gravity (`fauna-habitat-ruled`), are native pseudospace life absent from Sol (`fauna-origin-pseudospace`,
`sol-flatspace-bubble`), and drift with the global flow, whose authority moves into the sim (`sim-knows-the-flow`).
The standing directions `aetheria-release:ruling:kitchen-sink-content`, `catalog-breadth-now` and
`setting-permits-necrotech` make the ecology broad: more classes, more modes of harm, more goods, more uses, and no
trimming for genre consistency.

## Purpose, in the operator's words

> I'm gonna need the spaceborne microfauna cut mapped too. I wanna see swarms of space minnows and clouds of space
> krill, space sharks and space whales. EW is a prop hunt game and we need more props.

(2026-10-13, this session)

> You can't really camp in Aetheria because everything is constantly moving. We can provide a helper to sync the
> player to an orbit but we can't do that for matching the behavior of a whale if that's what you're currently
> disguised at. It becomes a game of adversarial observation

(ruling `aetheria-ew:ruling:disguise-held-by-flying`, 2026-10-13)

Earlier, on the targeting index (`docs/scenarios-cut.md:872`): "we'll want a bunch of subsystems feeding targeting
data. Think the spacebound microfauna from the slime mold experiment". The index is general for this reason
(`docs/aetheria-release-map.md:3712`).

## What fauna are for

Fauna are an ecology a pilot reads, hunts in, hides in, harvests and is hunted by. The first use is the oldest:
they are props, things in the sensor picture that are not ships, that a ship can be mistaken for, and that a
pilot must imitate by flying. They give the EW campaign (`docs/aetheria-ew-target.md`) a populated background. A
hunter reading a crowded picture has to ask of each return whether it moves like what it seems to be. That is the
prior art's oldest lesson: radar "angels" were birds and insects (PA:7). Insects drift with the wind and birds fly
relative to it, so the motion tells them apart (PA:9). Sonar's false floor was a layer of animals that rose and fell
with daylight (PA:14). Clutter that is alive is told apart by how it moves, not by how bright it is.

The other uses follow from the rulings. Fauna are a renewable harvest, big in Elysium, with goods for many trades
(the lore draft names krill paste for Bloom concrete, photophores, shark heat-organs and whale bone carrying strange
matter). Fauna are a hazard: classes attack ships, each in its own way. Fauna are material for every genre the
factions carry: cultivators harvest them, necrotech binds them, mecha pilots ride them. Fauna are a map: where they
gather shows the shape of the wells and the currents, and where they are missing says something happened.

## Design truths (ends, not means)

1. **Anything a sensor can perceive or a shot can hit is simulated; nothing below detection decides anything.**
   Sensing, the targeting index and fire control are all in `ServerShared`, so a fauna that is only drawn cannot be a
   contact, a target or a prop. The slime-mold experiment (`Slime.cs`, a GPU Physarum swarm of 131,072 agents in
   the ARPG scene, B8) stays what it is: ambient presentation that no sensor reads.
2. **Fauna never lie.** Their returns are truth. Only ships lie, through aetheria-ew's claims. A fauna class is
   the standard a disguised ship must fly to (`disguise-held-by-flying`).
3. **Each class has one motion law, legible and learnable.** An observer who watches a return for long enough can
   test it against the class's law. The laws are written to be told apart:
   - *krill* drift with the global flow: each member advects along the flow at its seed point for one cycle, fades
     and re-seeds (`fauna-flow-coupling-ruled`). This is the drifting-insect tell (PA:9);
   - *minnow* schools ride the flow and mill fast inside their school, so their jitter is quick;
   - *sharks* cut across the flow, patrol and sprint, and accelerate harder than anything else in the class list;
   - *whales* are slow, huge and hot, and swing in long arcs.
   An orbit-sync helper that matches a ship to the local flow is a separate, generic flying aid. It makes "krill" the
   cheapest disguise, and nothing makes a ship move like a shark or a whale except its pilot.
4. **Per-tick cost is per swarm, not per member.** Members are evaluated only when a query, a presenter or a sensor
   asks, as belt rocks are (`Zone.ChunkPose`, B3). Probe B10 measured it: 128 ships take about 4.9 ms per sim tick, and
   100,000 stepped bodies that read the zone force take 16 ms. Neither form scales to fauna counts.
5. **One detection rule.** Fauna are seen through `Sensor.Gain` by way of `Entity.PassiveInfo`, as chunks are
   (`Entity.ChunkInfo`, B5). There is no second sensor model and no fauna-specific threshold.
6. **Deterministic.** A fauna's state is a function of the zone's seed, its world laws, its bodies' orbits, zone
   time and sim events on fixed steps (`SimClock`, B9). Nothing about where fauna are is saved. It uses no `UnityEngine.Random`, no wall clock and no frame time. Any dice
   come from `SimulationDice` on a stream of their own.
7. **The zone owns fauna; presenters read them.** No Unity callback writes fauna state. The renderer draws them as it
   draws belts, from a pose evaluated at zone time (`ZoneRenderer.cs:543-569`, B7).
8. **The targeting index carries fauna as providers.** No consumer scans a swarm (`TargetingIndex.cs`, B4).
9. **The flow is a world fact with one owner in the sim.** The global flow that stardust and nebula follow
   (`Volumetric.cginc` `flow()`, B15) is evaluated by one `ServerShared` function of position and zone time from the
   zone's flow settings. Fauna drift with it. The shader lowers the same function from the same settings and time,
   and a parity check pins the two. No presenter accumulates its own flow time (B16).
10. **Placement tells an ecological story, and the level writes it as it moves.** Where fauna are is a closed form
    of the gravity slope and the flow, evaluated at zone time whenever a query, presenter or sensor asks
    (`fauna-placement-ruled`). Wells orbit, gas giants and suns ripple their wells (B24), and the flow scrolls, so the
    pattern moves with the level: a placement generated ahead would go stale. Life follows the Skein, the
    slime-mould web strung from well to well, and the energy the wells give off; predators follow their prey, and
    ambushers wait in the slack between wells. Nothing gathers in the cores, and open flat space away from the Skein
    is nearly empty. The law is in "Placement law" below.
11. **No fauna in Sol.** Sol is a single flatspace zone with no fauna inside it; the strangeness begins at its edge.
12. **Harm and harvest go through existing owners.** A fauna attack is damage through the path a weapon's hit
    takes; a harvest yields items through the economy's yield path; neither gets a fauna-only damage or loot rule.

## Placement law

Where fauna of a species are is decided by a **trophic DAG**: layers of fields, each a deterministic function of the
layers below it and of world facts, evaluated at a point at zone time when a query, presenter or sensor asks
(`habitat-trophic-dag`). Pointwise fields of slope and flow alone draw bands and blobs (B25); filaments need an
input that is not local, and the DAG takes it from the zone's bodies, which every point can read (map r4).

| Layer | Fields (the first web, from the lore's Open Water; the bestiary stays open) | Reads |
|---|---|---|
| 0 Environment | Slope and the operator's `Deposition(slope)` band, the steep-core mask (`Zone.GetNormal`); the flow (`Zone.Flow`); the zone's bodies: positions from the per-tick orbit cache, gravity radius, kind; energy kernels round them: starlight inside a sun's light radius, heat-light on warm well floors. Later sources join here: gravity-wave rings (whales), mind-light round crews and stations (moths), wormhole mouths (upwelling), wrecks (succession). | World facts only |
| 1 Structure | **The Skein**: strands between neighbouring bodies (a site graph with a soft gate, each edge a Gaussian strand, bent by a Zel'dovich warp of the flow, TD:39 [V]) and a weighted lattice Voronoi web hanging off them (`voronoi-primitive`). **Slack**: the borders of the bodies' weighted Voronoi, where pulls cancel. Which construction is the Skein is the question `skein-filament-construction`. | Layer 0 |
| 2 Producers | Glim: energy times the Skein's feeding. | 0, 1 |
| 3 Grazers | Krill: the Skein times a saturating response to glim (Holling II, TD:55 [S]). Minnows: krill over a hunting ring. | 0-2 |
| 4 Predators and ambushers | Sharks: minnows over a wider ring, on the band. Lanterns and void spiders: slack times krill over a ring. | 0-3 |

1. **Operators.** A sim-visible layer uses only: pointwise arithmetic, remap curves, smoothstep, min, max and smooth
   min, saturating responses; CultMath nodes (integer hash, weighted lattice cellular, weighted diagram over
   explicit sites, segment kernel, soft site-graph gate); a **stencil** (the mean of a lower layer at the point and a
   fixed ring of taps, at most two levels deep, 9x the cost per level, B36); and **kernel widening** where the prey
   is a sum of Gaussian kernels (the exact blur, TD:78 [M]). Grid operators (blur of arbitrary fields, distance
   transforms, eikonal paths, jump flooding, TD:19, TD:78), iteration and stored fields are presentation-only.
2. **Donor control.** A layer never reads itself or a layer above it: predators follow prey and never thin it in
   the law (TD:56 [M]). Thinning is a sim event (harvest, attack wear), not a placement term.
3. **Candidates with fixed thresholds** (r3, kept). Each species has a lattice of candidate seed points, one per
   cell, jittered by a hash of the zone seed and the cell, each with its own fixed threshold from the same hash; a
   candidate hosts a member when its species' density there is above its threshold (Horizon Zero Dawn's shape,
   CF:33 [V]; nesting pinned as an invariant, CF:40 [M]). The member's identity is the cell, a `ChunkId` with no
   stored state. Candidate spacing is per species (16 u krill, 48 u minnows, 128 u sharks in the probe).
4. **No pop.** Terms that move fast are read at the member's own cycle start (`habitat-no-pop-ruled` for the flow,
   extended to the flow warp and the lattice web's drift, measured in B34); terms that move slowly and continuously
   (the slope band, the soft site graph, B33) are read now and carried by the fade. A member never appears or
   vanishes at full presence.
5. **Nodes in CultMath, compositions in Aetheria.** Every node primitive is a CultMath function with an HLSL twin,
   pinned by CultMath's mirror test (map B29); what CultMath lacks is a gap filled in CultLib (map r4, gaps 1-5),
   never a helper in Aetheria. Each species' composition is C# in `ServerShared` (`Habitat`) calling those
   functions, with its parameters in a per-species settings record; the sim evaluates only C#. A presenter that
   draws a composed layer (the Skein's glow, glim haze) uses the composition's HLSL twin under a CultMath-owned
   composition check. Whether compositions later become data on CultMath's parked tape is the question
   `trophic-graph-tape`.

The law costs nothing per tick: it runs per candidate when asked, as `Zone.ChunkPose` does for rocks; the site graph
is per zone per tick over a handful of bodies.

## Scale classes

| Class | Shape in the sim | Light | Motion law (tell) | Prop role |
|---|---|---|---|---|
| Krill | Thousands of members at hosted candidates on the well shoulders; each advects along the flow for one cycle, fades and re-seeds | Faint reflection per member; a patch is bright in aggregate | Drifts with the global flow (advected cycles) | Hide in a cloud; later, a dense cloud thins sight lines as a vapour cloud does (`Zone.Obscuration`) |
| Minnow | Schools of hundreds that ride the flow | Faint reflection, a bright school | Flow plus fast milling inside the school | Clutter of small returns; a ship moving through a school is noticed (later: scatter) |
| Shark | One to three members, each about the size of a ship | Reflection and moderate thermal | Crosses the flow; patrols with sprints | A ship-sized return a hunter must think about; the best disguise target for a fighter |
| Whale | One member, many times a ship's size | Strong thermal; heard song (later) | Slow long arcs | A bright anchor; a ship near it is hard to separate (later: shadowing) |

More classes are wanted, not fewer (`kitchen-sink-content`). The lore draft's room for more becomes classes in the
cut sequence: *void spiders* that string nets across trojan points and snare what drifts in, *gas giant drifters*
that live in the upper cloud deck, and *edge giants*, the larger things past the charts, found only far from Sol.
Each class gets its own motion law, its own mode of harm and its own goods. Attack modes are designed per class in
later cuts; the ones the lore implies are sharks that strike, minnow schools that scour sensors and hulls, whale
song that is noise in the shared signal space, spider nets that snare and tether, and drifters that discharge.

The krill and minnow rows need nothing the Body lacks except a bounded per-kind motion term on field members and the
flow (B3, B4, B15). Sharks and whales need an anchor that does not ride the flow, which comes later.

## Model page: identity, lifecycle, authority

The rows follow the rulings `fauna-in-sim-ruled`, `fauna-identity-ruled` and `fauna-motion-model-ruled`.

| Kind | Identity (what names it) | Lifecycle | Authority (who decides; forbidden writers) |
|---|---|---|---|
| **Field kind** (existing, extended) | `FieldKindData` catalog record (`ZoneData.cs:122`), by key. A fauna kind is a field kind with a non-zero member sway and zero belt `GenerationWeight`, so a rock belt never draws it (`FieldKinds.Assign`, "Zero never picks it"). | Authored in AetherDb, append-only MessagePack keys. | The catalog. Forbidden: runtime writes. |
| **Fauna field** (one per fauna kind a zone hosts) | The fauna kind's catalog key within the zone: a zone hosts every fauna kind its world laws admit, so the field is derived, not generated. | Derived when the zone is built, from its world laws and the catalog; never saved. Only its members' wear is saved (`ZonePack.ChunkWear`). | `Zone`. Forbidden: `ZoneGenerator` writing fauna into the pack; presenters; entities. |
| **Member** (a chunk of a fauna field) | `ChunkId(field, index)` (`Zone.cs:825`; the type was named `Field` so other field kinds could join, per its own comment); the index is a lattice cell. | Present while its candidate is hosted (placement law) and it is not broken (`Zone.ChunkExists`). Wear and respawn are lazy comparisons against zone time (`Zone.Wear`), as for rocks. Its pose is a pure function of zone time: its seed point advected along the flow for its cycle. | The habitat law decides hosting; the class law alone computes the pose; `Zone.Wear` is the only writer of damage. Forbidden: any stored pose or hosting bit, any per-tick member loop. |
| **Fauna provider** (existing, widened) | `BeltTargets`, one per field (`Zone.cs:175`). | Built with the zone, never saved. | Its region bounds must hold for every member. Sway widens the slack by the kind's amplitude. Forbidden: a second index or a fauna scan. |
| **Fauna detection** (derived) | `(observer, member)`, never stored. | Derived per query: `ChunkInfo` → `PassiveInfo` → `Sensor.Gain`. | `Entity`. Forbidden: any fauna-specific threshold or gain. |
| **Swarm anchor** (later, sharks and whales) | Per field: a law that moves the field's frame across the flow. | Pure function of zone time plus sparse event records (fork `fauna-motion-model`). | `Zone`. Forbidden: per-member stepping. |
| **Disturbance** (later) | `(member, time, push)`, sparse, as `ChunkWear` is. | Written when a hull passes through or a shot lands, then decays lazily against zone time. | `Zone`. It is read only by `Pose`. |
| **Flow field** (new owner) | One law: `FlowField`, evaluated per zone by `Zone.Flow(position)` from the zone's world-law settings (`PlanetSettings.Flow`). | Settings authored in `Settings.asset`; time is zone time. Never stored per position. | `Zone`. Derived: every shader flow uniform (`_FlowScroll` and the rest). Forbidden: `VolumeSampling._flowScroll`, `ZoneEnvironment.Flow`, any frame-time accumulator. |
| **Habitat** (new owner, a law: the trophic DAG) | `Habitat` in `ServerShared`: one composition per species over CultMath nodes, a species' settings record, a candidate's hashed seed point and threshold. | Never stored: evaluated at zone time per candidate when asked. | `Habitat` decides density and hosting; `Zone` supplies world facts (`GetNormal`, `Flow`, body positions from the per-tick orbit cache); CultMath owns every node and its HLSL twin. Forbidden: a generation pass, a saved or cached habitat map, a per-tick sweep, a math twin in Aetheria, a grid operator in a sim-visible layer. |
| **Skein site graph** (derived) | Per zone: the soft gate of each pair of bodies (map r4). | Recomputed per tick from orbit positions; never saved. | `Habitat`, through a CultMath gate function. Forbidden: a hard (0/1) gate; a cached graph across ticks. |
| **Attack** (later, per class) | `(member or swarm, ship, time)`, an event. | Raised by a class law when its trigger holds; applied through the ordinary damage path. | `Zone`. Forbidden: a fauna-only damage rule. |
| **Harvest yield** (later) | Item kinds in the catalog, per fauna kind. | A member broken or netted by harvesting gear yields items through the economy's yield table; the field refills by respawn. | The catalog and the yield table. Forbidden: fauna-only loot code. |
| **Fauna background kind** (aetheria-ew) | Entries of aetheria-ew's `BackgroundKind` for fauna classes (that target's row says "more by ruling"). | Static data. | `GameplaySettings`, per aetheria-ew. Its envelope gains a motion term per class (fork `fauna-disguise-path`). |

## Invariants

- `fauna-sim-owned`: no fauna state is written outside `Zone`, `ZoneGenerator` and the catalog. A test spies the
  renderer's inputs, and a grep finds no Unity writer.
- `fauna-pose-pure`: a member's pose at time t does not depend on the steps taken to reach t. Stepping a zone to t
  by 1/60 and by 1/7 gives equal poses.
- `fauna-cost-per-swarm`: zone update cost does not grow with member count. This is pinned with the index's
  `Examined` counter and by `Zone.Update` touching no member, not by timing.
- `fauna-one-gain`: a fauna member is visible exactly when `ChunkVisible` says so.
- `fauna-bounds-hold`: every member of a swaying or drifting field lies inside the regions its provider offers.
- `fauna-tell-holds`: each class's members move by that class's law; a krill member advects with the global flow
  from its seed point for its cycle. Each class gets its own tell test.
- `fauna-never-lie`: no claim, suppression or belief is written for a fauna.
- `flow-one-owner`: the global flow has one owner, `FlowField` in `ServerShared`, evaluated at zone time from the
  zone's flow settings. Every shader flow uniform is derived from it; no presenter keeps its own flow clock.
- `flow-shader-parity`: the HLSL `globalFlow` and `slopeFlow` in `Volumetric.cginc` give the C# owner's values for
  the same position, time, settings and gradient, within a stated tolerance; a parity check that evaluates both
  pins it and fails when either side changes alone.
- `flow-step-free`: the flow at a position and zone time does not depend on step size or frame rate.
- `fauna-none-in-sol`: Sol's zone hosts no fauna kind, so no candidate there is ever hosted.
- `fauna-placement-closed-form`: whether a candidate hosts a member at zone time t is a pure function of the zone
  seed, the species' settings, the world facts at t (slope, flow at the member's cycle start, the bodies' positions)
  and the lower layers evaluated at bounded points. Nothing about placement is generated ahead, saved, cached across
  ticks or stepped.
- `fauna-placement-story`: density is zero in well cores; along a Skein strand between two bodies it exceeds the
  density at a point of equal slope off the strand; ambushers' density peaks on the slack borders; a predator's
  density rises with its prey's within its hunting ring.
- `fauna-placement-nested`: each candidate's threshold is fixed, so raising the density at a candidate never
  un-hosts it and lowering it never hosts it.
- `fauna-placement-no-pop`: a member never appears or vanishes at full presence; every change of hosting is carried
  through a fade. Within a member's cycle its presence moves by at most a stated bound per 1/60 s step as the bodies
  orbit (the probe's worst was .066, map B34).
- `trophic-dag-acyclic`: each layer reads only world facts and the layers below it; no layer reads itself or a layer
  above it.
- `trophic-dag-pointwise`: every sim-visible layer is evaluated at a point from world facts, the per-tick site
  graph and a bounded number of evaluations of lower layers (stencils at most two levels deep); no grid, no
  iteration, no stored field.
- `trophic-nodes-in-cultmath`: every node primitive a layer calls is a CultMath function with an HLSL twin pinned by
  CultMath's parity checks; Aetheria holds compositions only.
- `skein-gate-continuous`: the Skein's site-graph gates are continuous in the bodies' positions, so no strand
  appears or vanishes as the bodies orbit.

## Not in scope

- Ambient presentation-only life (the slime trails) beyond leaving it untouched; the habitat law reuses its
  `Deposition(slope)` curve, but the presenter stays presentation.
- Population dynamics: no layer is stepped or regulated by the layer above it; harvest and attack wear thin
  members as sim events through `Zone.Wear`, not as placement terms.
- Phantom positions (aetheria-ew `ew-where-lies-ruled`).
- Ships drifting with the flow (`flow-moves-what-ruled`: ships do not; floating items, mines and vented clouds do,
  in a later cut).
- Per-zone flow and nebula environments; Sol's cloudless bubble is a later cut, and until it lands every zone
  shares one flow.

Attacks and harvest are in scope as later cuts, designed per class (`fauna-interaction-ruled`,
`fauna-harvest-ruled`); the first cuts build the ground they stand on: the flow, the habitat law and the krill field.

## Cut sequence

The map's r4 cut sequence (with the r3 lines it keeps) has one line per cut; each is mapped when it is about to run.
