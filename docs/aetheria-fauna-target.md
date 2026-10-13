# Aetheria spaceborne fauna: target

Proposed campaign `aetheria-fauna`, target draft r0 (Self admits the campaign and the target). Repo
`GameCult/Aetheria`, working branch `master`, cut branches `eureka/aetheria-fauna-<cut>`. Written by Imagination
(agent imagination-aeth-fauna, session self-2026-10-13-ag2) against `origin/master` `0898725f`. Body facts and
probes are in `docs/aetheria-fauna-map.md` (cited below as `B<n>`); prior art is
`docs/research/2026-10-13-fauna-prior-art.md` (cited as `PA:<line>`; items tagged `[M]` there are from memory and
unverified). This is the target and the model page. The open forks are drafted as questions in the map.

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

Fauna are props: things in the sensor picture that are not ships, that a ship can be mistaken for, and that a
pilot must imitate by flying. They give the EW campaign (`docs/aetheria-ew-target.md`) a populated background. A
hunter reading a crowded picture has to ask of each return whether it moves like what it seems to be. That is the
prior art's oldest lesson: radar "angels" were birds and insects (PA:7). Insects drift with the wind and birds fly
relative to it, so the motion tells them apart (PA:9). Sonar's false floor was a layer of animals that rose and fell
with daylight (PA:14). Clutter that is alive is told apart by how it moves, not by how bright it is.

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

## Scale classes

| Class | Shape in the sim | Light | Motion law (tell) | Prop role |
|---|---|---|---|---|
| Krill | A cloud of thousands of members that share one orbital slot; members sway within a bounded radius | Faint reflection per member; the cloud is bright in aggregate | Velocity equals the local orbital flow; slow bounded sway | Hide in a cloud; later, a dense cloud thins sight lines as a vapour cloud does (`Zone.Obscuration`) |
| Minnow | Schools of hundreds that ride the flow | Faint reflection, a bright school | Flow plus fast milling inside the school | Clutter of small returns; a ship moving through a school is noticed (later: scatter) |
| Shark | One to three members, each about the size of a ship | Reflection and moderate thermal | Crosses the flow; patrols with sprints | A ship-sized return a hunter must think about; the best disguise target for a fighter |
| Whale | One member, many times a ship's size | Strong thermal; heard song (later) | Slow long arcs | A bright anchor; a ship near it is hard to separate (later: shadowing) |

The krill and minnow rows need nothing the Body lacks except a bounded per-kind motion term on field members (B3,
B4). Sharks and whales need an anchor that does not ride the flow, which is the second cut onward.

## Model page: identity, lifecycle, authority

This assumes the recommended options of the questions in the map (`fauna-in-sim`, `fauna-identity`,
`fauna-motion-model`). A different ruling rewrites these rows.

| Kind | Identity (what names it) | Lifecycle | Authority (who decides; forbidden writers) |
|---|---|---|---|
| **Field kind** (existing, extended) | `FieldKindData` catalog record (`ZoneData.cs:122`), by key. A fauna kind is a field kind with a non-zero member sway and zero belt `GenerationWeight`, so a rock belt never draws it (`FieldKinds.Assign`, "Zero never picks it"). | Authored in AetherDb, append-only MessagePack keys. | The catalog. Forbidden: runtime writes. |
| **Swarm** (new use of an existing kind) | A field: an `AsteroidBeltData` body in `ZonePack.Planets`, named by its record key, as a belt is. Its members have orbital elements (distance and phase) clustered into one arc, and its kind is a fauna kind. | Created by `ZoneGenerator.GenerateZone` and saved with the zone pack, like a belt. Members are never saved except for their wear. | `ZoneGenerator` creates it; `Zone` holds it (`AsteroidBelts`). Forbidden: presenters, entities. |
| **Member** (a chunk of a fauna field) | `ChunkId(field, index)` (`Zone.cs:825`; the type was named `Field` so other field kinds could join, per its own comment). | Exists unless broken (`Zone.ChunkExists`). Wear and respawn are lazy comparisons against zone time (`Zone.Wear`), as for rocks. Its pose is a pure function of zone time: the orbital slot plus the kind's bounded sway. | `AsteroidBelt.Pose` alone computes the pose; `Zone.Wear` is the only writer of damage. Forbidden: any stored pose, any per-tick member loop. |
| **Fauna provider** (existing, widened) | `BeltTargets`, one per field (`Zone.cs:175`). | Built with the zone, never saved. | Its region bounds must hold for every member. Sway widens the slack by the kind's amplitude. Forbidden: a second index or a fauna scan. |
| **Fauna detection** (derived) | `(observer, member)`, never stored. | Derived per query: `ChunkInfo` → `PassiveInfo` → `Sensor.Gain`. | `Entity`. Forbidden: any fauna-specific threshold or gain. |
| **Swarm anchor** (later, sharks and whales) | Per field: a law that moves the field's frame across the flow. | Pure function of zone time plus sparse event records (fork `fauna-motion-model`). | `Zone`. Forbidden: per-member stepping. |
| **Disturbance** (later) | `(member, time, push)`, sparse, as `ChunkWear` is. | Written when a hull passes through or a shot lands, then decays lazily against zone time. | `Zone`. It is read only by `Pose`. |
| **Fauna background kind** (aetheria-ew) | Entries of aetheria-ew's `BackgroundKind` for fauna classes (that target's row says "more by ruling"). | Static data. | `GameplaySettings`, per aetheria-ew. Its envelope gains a motion term per class (fork `fauna-disguise-path`). |

## Invariants

- `fauna-sim-owned`: no fauna state is written outside `Zone`, `ZoneGenerator` and the catalog. A test spies the
  renderer's inputs, and a grep finds no Unity writer.
- `fauna-pose-pure`: a member's pose at time t does not depend on the steps taken to reach t. Stepping a zone to t
  by 1/60 and by 1/7 gives equal poses.
- `fauna-cost-per-swarm`: zone update cost does not grow with member count. This is pinned with the index's
  `Examined` counter and by `Zone.Update` touching no member, not by timing.
- `fauna-one-gain`: a fauna member is visible exactly when `ChunkVisible` says so.
- `fauna-bounds-hold`: every member of a swaying field lies inside the regions its provider offers.
- `fauna-tell-holds`: a krill cloud's centroid velocity equals the orbital flow at its distance, within the sway
  bound. Each later class gets its own tell test.
- `fauna-never-lie`: no claim, suppression or belief is written for a fauna.

## Not in scope

- Ambient presentation-only life (the slime trails) beyond leaving it untouched.
- Fauna AI that attacks ships, unless `fauna-interaction` rules otherwise.
- Harvest economies and yield tables for fauna (the mining yield table is aetheria-release's).
- Phantom positions (aetheria-ew `ew-where-lies-ruled`).
- Any lore claim about what the fauna are. No fauna lore was found in `Vault` or AetheriaLore (B11), so the
  operator writes it, or rules that Imagination drafts it.

## Cut sequence

The map has the first cut drafted as a spec section and every later cut as one line.
