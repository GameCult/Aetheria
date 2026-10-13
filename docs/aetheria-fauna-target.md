# Aetheria spaceborne fauna: target

Campaign `aetheria-fauna`, target content r2 (r1 was admitted from the r0 draft at `f9705d32`; Self admits r2).
Repo `GameCult/Aetheria`, working branch `master`, cut branches `eureka/aetheria-fauna-<cut>`. Written by
Imagination (agents imagination-aeth-fauna and imagination-aeth-fauna-r2, session self-2026-10-13-ag2); r2 is
against `origin/master` `1746e8bd`. Body facts and probes are in `docs/aetheria-fauna-map.md` (cited below as
`B<n>`); prior art is `docs/research/2026-10-13-fauna-prior-art.md` (`PA:<line>`; `[M]` items are from memory and
unverified) and `docs/research/2026-10-13-fauna-placement-prior-art.md` (placement). This is the target and the
model page. Open forks are questions in the mind under `aetheria-fauna`.

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
   - *krill* clouds ride the local orbital flow, as belt rocks do: their velocity is the flow at their distance. This
     is the drifting-insect tell (PA:9);
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
6. **Deterministic.** A fauna's state is a function of the zone's seed, its saved field data, zone time and sim
   events on fixed steps (`SimClock`, B9). It uses no `UnityEngine.Random`, no wall clock and no frame time. Any dice
   come from `SimulationDice` on a stream of their own.
7. **The zone owns fauna; presenters read them.** No Unity callback writes fauna state. The renderer draws them as it
   draws belts, from a pose evaluated at zone time (`ZoneRenderer.cs:543-569`, B7).
8. **The targeting index carries fauna as providers.** No consumer scans a swarm (`TargetingIndex.cs`, B4).
9. **The flow is a world fact with one owner in the sim.** The global flow that stardust and nebula follow
   (`Volumetric.cginc` `flow()`, B15) is evaluated by one `ServerShared` function of position and zone time from the
   zone's flow settings. Fauna drift with it. The shader lowers the same function from the same settings and time,
   and a parity check pins the two. No presenter accumulates its own flow time (B16).
10. **Placement tells an ecological story and is decided once.** The generator places fauna from the zone's gravity
    and flow, deterministically from the zone seed, and saves what it placed with the zone pack. Fauna gather on
    the shoulders of wells and along links between them, not in the cores and not in flat space (placement probe,
    `docs/research/2026-10-13-fauna-placement-prior-art.md`). The method is the question `fauna-placement`.
11. **No fauna in Sol.** Sol is a single flatspace zone with no fauna inside it; the strangeness begins at its edge.
12. **Harm and harvest go through existing owners.** A fauna attack is damage through the path a weapon's hit
    takes; a harvest yields items through the economy's yield path; neither gets a fauna-only damage or loot rule.

## Scale classes

| Class | Shape in the sim | Light | Motion law (tell) | Prop role |
|---|---|---|---|---|
| Krill | A cloud of thousands of members that share one orbital slot; members sway within a bounded radius | Faint reflection per member; the cloud is bright in aggregate | Velocity equals the local orbital flow; slow bounded sway | Hide in a cloud; later, a dense cloud thins sight lines as a vapour cloud does (`Zone.Obscuration`) |
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
| **Swarm** (new use of an existing kind) | A field: an `AsteroidBeltData` body in `ZonePack.Planets`, named by its record key, as a belt is. Its members have orbital elements (distance and phase) clustered into one arc, and its kind is a fauna kind. | Created by `ZoneGenerator.GenerateZone` and saved with the zone pack, like a belt. Members are never saved except for their wear. | `ZoneGenerator` creates it; `Zone` holds it (`AsteroidBelts`). Forbidden: presenters, entities. |
| **Member** (a chunk of a fauna field) | `ChunkId(field, index)` (`Zone.cs:825`; the type was named `Field` so other field kinds could join, per its own comment). | Exists unless broken (`Zone.ChunkExists`). Wear and respawn are lazy comparisons against zone time (`Zone.Wear`), as for rocks. Its pose is a pure function of zone time: the orbital slot plus the kind's bounded sway. | `AsteroidBelt.Pose` alone computes the pose; `Zone.Wear` is the only writer of damage. Forbidden: any stored pose, any per-tick member loop. |
| **Fauna provider** (existing, widened) | `BeltTargets`, one per field (`Zone.cs:175`). | Built with the zone, never saved. | Its region bounds must hold for every member. Sway widens the slack by the kind's amplitude. Forbidden: a second index or a fauna scan. |
| **Fauna detection** (derived) | `(observer, member)`, never stored. | Derived per query: `ChunkInfo` → `PassiveInfo` → `Sensor.Gain`. | `Entity`. Forbidden: any fauna-specific threshold or gain. |
| **Swarm anchor** (later, sharks and whales) | Per field: a law that moves the field's frame across the flow. | Pure function of zone time plus sparse event records (fork `fauna-motion-model`). | `Zone`. Forbidden: per-member stepping. |
| **Disturbance** (later) | `(member, time, push)`, sparse, as `ChunkWear` is. | Written when a hull passes through or a shot lands, then decays lazily against zone time. | `Zone`. It is read only by `Pose`. |
| **Flow field** (new owner) | One law: `FlowField`, evaluated per zone by `Zone.Flow(position)` from the zone's world-law settings (`PlanetSettings.Flow`). | Settings authored in `Settings.asset`; time is zone time. Never stored per position. | `Zone`. Derived: every shader flow uniform (`_FlowScroll` and the rest). Forbidden: `VolumeSampling._flowScroll`, `ZoneEnvironment.Flow`, any frame-time accumulator. |
| **Habitat placement** (generation intermediate) | None: it exists only inside `ZoneGenerator.GenerateZone`. | Computed once per zone from the seed, gravity and flow; consumed to place fields; discarded. | `ZoneGenerator`. Its products, fauna fields, are saved as belts are. Forbidden: recomputing on load, saving the intermediate map. |
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
- `fauna-tell-holds`: each class's members move by that class's law; a krill cloud moves with the orbital flow and
  the global flow, within its sway bound. Each class gets its own tell test.
- `fauna-never-lie`: no claim, suppression or belief is written for a fauna.
- `flow-one-owner`: the global flow has one owner, `FlowField` in `ServerShared`, evaluated at zone time from the
  zone's flow settings. Every shader flow uniform is derived from it; no presenter keeps its own flow clock.
- `flow-shader-parity`: the HLSL `globalFlow` and `slopeFlow` in `Volumetric.cginc` give the C# owner's values for
  the same position, time, settings and gradient, within a stated tolerance; a parity check that evaluates both
  pins it and fails when either side changes alone.
- `flow-step-free`: the flow at a position and zone time does not depend on step size or frame rate.
- `fauna-none-in-sol`: Sol's zone holds no fauna field; generation never places one there.
- `fauna-placement-seeded`: a zone's fauna placement is a function of its seed and its generated bodies on the
  generating runtime; the saved zone pack is the authority after that, and loading never recomputes placement.

## Not in scope

- Ambient presentation-only life (the slime trails) beyond leaving it untouched; its rules may be reused at
  generation, but the presenter stays presentation.
- Phantom positions (aetheria-ew `ew-where-lies-ruled`).
- Ships, floating items, mines and clouds drifting with the flow, until `flow-moves-what` is ruled.
- Per-zone flow and nebula environments; Sol's cloudless bubble is a later cut, and until it lands every zone
  shares one flow.

Attacks and harvest are in scope as later cuts, designed per class (`fauna-interaction-ruled`,
`fauna-harvest-ruled`); the first cuts build the ground they stand on: the flow, placement and the krill field.

## Cut sequence

The map has the first cut drafted as a spec section and every later cut as one line.
