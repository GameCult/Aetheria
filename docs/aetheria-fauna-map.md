# Aetheria spaceborne fauna: map

Proposed campaign `aetheria-fauna`. Target: `docs/aetheria-fauna-target.md` (draft r0). Written by Imagination
(agent imagination-aeth-fauna, session self-2026-10-13-ag2) against `origin/master` `0898725f`. This map holds the
body facts, the questions Self admits together with the campaign, the first cut as a draft spec, and the rest of
the cut sequence. Prior art: `docs/research/2026-10-13-fauna-prior-art.md` (`PA:<line>`). Items tagged `[M]` there
come from memory and are unverified.

## Body facts

Probed at `0898725f`. A file:line is a line on that commit.

| # | Question | Result |
|---|---|---|
| B1 | How does the sim hold things that are not ships? | Three runtime collections on `Zone`, all `ReactiveCollection`s with one adder and one remover: `Mines` (Zone.cs:44, `Lay`/`StepMines`), `Clouds` (:48, `Vent`/`StepClouds`), `FloatingItems` (:53, `Release`/expiry/`TryPickUp`). All three are stepped every tick as a `KinematicBody` (FloatingBodies.cs:25-44) on `Zone.GetForce`, have ids from `NextBodyId` (Zone.cs:131), are never saved and are not `Entity`s. Belts are the fourth kind, and they are not stepped (B3). |
| B2 | What is an `Entity`'s per-tick shape? | `Zone.Update` (Zone.cs:274-305) runs `PublishInbox`, `Update` and `Sense` for every entity, then mines, clouds, floating items, `Contacts.Step` (O(n²) hull pairs, Contacts.cs:57-79) and `FireControl.Step`. Each active `Sensor.Execute` walks every zone entity and calls `Zone.Obscuration` per pair (Sensor.cs:186-234). `Entity.Sense` walks every other entity's signatures (Entity.cs:989-1047). Entity count therefore costs O(n²) per tick, and each entity carries a hull, gear, a thermal grid and Rx subscriptions (Entity.cs:262-330). |
| B3 | How are belt rocks held? | As members of a field: `ChunkId(Field, Index)` (Zone.cs:825-839). The comment there says "Field is deliberate, not belt": the operator's ruling generalizes chunks to other kinds of field. The pose is a pure function of zone time (`AsteroidBelt.Pose`, Zone.cs:800-808: orbit distance, phase, `OrbitPeriod`). Wear is a sparse dictionary read lazily (`_wear`, `ChunkExists`, `ChunkRadius`, `Wear`, Zone.cs:503-555). Visibility is `ChunkVisibility` = kind cross-section × area × light (Zone.cs:570-578). Nothing about a rock is stepped per tick. Members are saved through `AsteroidBeltData.Asteroids` (ZoneData.cs:107-117) and wear through `ZonePack.ChunkWear`. |
| B4 | What is the targeting index's provider seam? | `ITargetProvider.Regions` and `Candidates` (TargetingIndex.cs:65-72). A region carries distance, bearing and visibility bounds that must hold for every member. `Within` and `Best` are best-first over regions (Hjaltason and Samet). Two providers exist: `EntityTargets` (offers only the observer's `VisibleEntities`) and `BeltTargets` (orbit bands of 256, segments of 32, re-keyed lazily, BeltTargets.cs:22-190). `BeltTargets` widens every bound by `slack` (:109) and bounds size by `_largestRadius` (:35, :52, :181). `Zone` adds one `BeltTargets` per `AsteroidBeltData` body (Zone.cs:170-175). |
| B5 | How are non-entities seen? | `Entity.ChunkInfo` → `PassiveInfo(position, visibility)` → each sensor's `PassiveRate` → `Sensor.Gain`, the one gain rule (Entity.cs:427-454, Sensor.cs:242-263). `ChunkVisible` is info above `TargetDetectionInfoThreshold`. Chunks are never contacts: there is no reveal, no track and no `Contact` for a chunk. `ContactOf` and the reveal book are keyed by `Entity` (Entity.cs:232, :963). |
| B6 | How is a non-entity target named? | `TargetRef` holds an `Entity` or a `ChunkId` (Zone.cs:844-870). Chunk-aware sites: Entity.cs (10 reads, e.g. :379, :419, :531, :561-572, :1653-1655), FireControl.cs:139-142 (a chunk bears through its pose), ActionGameManager.cs:1174, :1419, :1453. A new member kind that is a `ChunkId` passes through all of them unchanged. A third `TargetRef` arm would touch every one. |
| B7 | How does presentation bind sim things? | `ZoneRenderer` subscribes to `ObserveAdd`/`ObserveRemove` on `Mines`, `FloatingItems` and `Clouds` and instantiates one prefab per thing (ZoneRenderer.cs:232-314). Belts are drawn every frame from `Zone.EvaluateBelt` into the renderer's own buffer through `Graphics.DrawMeshInstanced` (:536-570). Meshes are picked per belt at load (:431-437). No pose is stored on the sim side. |
| B8 | What is the slime-mold experiment? | `Assets/Shaders/Compute/Slime/Slime.cs` plus `Slime.compute`: a GPU Physarum swarm (agents sense a trail texture, turn and deposit; speed and turn follow the gravity slope; random respawn). It runs live in `Scenes/ARPG.unity` through `Prefabs/RPG/Slime Renderer.prefab`, with 131,072 agents and 32,768 trail particles in the scene override (ARPG.unity:19676-19712). `ZoneRenderer` sets only its radius (:198). Its spawn-at-suns code is commented out (:528-532). It is presentation only: it is nondeterministic, the sim never reads it, and it decides nothing. A Discord note from 2021-03-28 calls the agents "rendered in the actual Aetheria level as wee little trails". |
| B9 | Tick model and dice? | `SimClock` runs fixed steps at 60 per real second, caps catch-up at 0.1 s of real time, and gives presenters `Lead` (SimClock.cs:11-60, ActionGameManager.cs:1363). `SimulationDice.For(zoneSeed, stream, ordinal)` supplies deterministic streams (`LootStream` 1, `MineStream` 2). |
| B10 | What do things cost? (scratch probe, deleted, never committed) | Run on Yggdrasil (dotnet sdk 10, Release, 4 CPUs), a test in `ContactRevealTests`' fixture at `0898725f`. **Entities** with one passive sensor each, ms per `Zone.Update`: 8 → 0.38, 32 → 2.85, 128 → 4.95. The ships' light was zero, so no track ever formed; this is the floor, and JIT warm-up inflates the 32 reading. **Stepped bodies** (`KinematicBody.Step` plus `Zone.GetForce`, a zone with no planets), ms per tick: 1k → 0.99 (includes JIT), 10k → 1.45, 100k → 16.1. Without `GetForce`: 10k → 0.10, 100k → 0.99. `GetHeight` loops every planet 4 times per `GetForce` (Zone.cs:627-717), so a real zone costs more. **PassiveInfo** with one sensor: 0.185 µs per call. Reading: a tick has 16.7 ms. Fauna as entities are unaffordable beyond tens. A stepped body per member is affordable only in the low thousands. Evaluating a member on demand, as chunks are, costs nothing per tick, and a query's cost is bounded by the index. |
| B11 | Is there fauna lore? | No. `Vault/**/*.md` has no krill, whale or microfauna; voidbot over AetheriaLore surfaces nothing; Discord has only the slime-agent remarks. Lore is open. |
| B12 | Where would fauna touch aetheria-ew? | aetheria-ew's `BackgroundKind` row (target: "Append-only enum (Rock, Debris, more by ruling)") and cut 6 `ew-blend` ("believed only inside a belt where real chunks are near ... light relative to nearby chunks"). The ruling `ew-blend-presentation-ruled` shows a believed background contact as "a faint background marker labelled as the kind". The ruling `disguise-held-by-flying` makes the motion test adversarial observation. |
| B13 | Do the in-flight Hands passes overlap? | ew-claims (`origin/eureka/aetheria-ew-ew-claims-in-the-book`) edits Sensing.cs, Entity.cs, Combat.cs, SchematicDisplay.cs, FireControl.cs and tests. loot-3 edits FieldDriver, TractorBeam, ItemPickup, the capability presenters and PickupTests. The first cut below touches ZoneData.cs, Zone.cs (`AsteroidBelt`, its construction), BeltTargets.cs, ZoneGenerator.cs and Settings.cs (`ZoneGenerationSettings`): **no file overlap**. |
| B14 | What else reads every belt body? | `SectorRenderer.cs:84` counts `AsteroidBeltData` bodies as belts in the sector UI, so a fauna field would count as a belt there. `ZoneRenderer.LoadPlanet` draws it with asteroid meshes (:431). Both are presentation, and both are later cuts. |

## Rationale

- **Fauna are fields, not entities and not stepped bodies (B2, B3, B10).** The chunk path already does what a swarm
  needs. It gives identity without allocation, a pose as a pure function of time, lazy wear and respawn, detection
  through the one gain rule, a target slot, an index provider and an instanced draw. Making a member a chunk of a
  fauna field reuses every one of those owners. The alternatives each add an owner: an entity per fish costs O(n²)
  per tick; a stepped body per member costs O(n) per tick and has no index provider; a third `TargetRef` arm touches about
  15 sites (B6).
- **Krill first.** Krill ride the orbital flow, which is exactly the motion belt rocks already have (B3). One bounded
  sway term per kind and a clustered generator give a cloud that moves like krill, and nothing else in the Body has
  to change. It is the smallest cut the operator can see run, and its tell ("drifts with the wind", PA:9) is the
  one the prior art says observers use first.
- **Sharks and whales need an anchor.** Their tell is moving against the flow, so the field's frame has to move.
  `fauna-motion-model` decides whether that is a closed form plus sparse events or a stepped anchor. Either one is
  per swarm (a few dozen per zone), not per member.
- **Distance aggregation is a presentation and query question first.** The index already bounds what any query
  examines (B4). Whether a school is one return or many is a question of what an observer is shown, so it is asked
  rather than built ahead of need.
- **The prop-hunt tax already exists.** Garry's Mod charges hunters health for shooting real props, and Black Ops 4
  makes props whistle (PA:35-36). In Aetheria, shooting a return to test it already shows a launch flash and spends
  heat and ammunition (aetheria-ew H1, H6), and a member that is shot breaks through `Zone.Wear` like a rock. No
  extra tax is proposed.

## Questions for the operator (draft; Self admits them with the campaign)

The prior-art file (`docs/research/2026-10-13-fauna-prior-art.md`) was read before these recommendations were
written. One rule shapes every option below: under `disguise-held-by-flying`, no option offers static hiding or an
automatic mimic, and the only flying aid is a generic orbit sync.

1. **`fauna-in-sim`: where do fauna live: sim or presentation?**
   - Options:
     - `sim-perceivable-present-ambient`: anything a sensor can perceive or a shot can hit is in `ServerShared`; sub-detection ambient life (the slime trails) stays presentation and decides nothing.
     - `presentation-only`: fauna are drawn only; they cannot be contacts or targets.
     - `sim-everything`: the slime agents move into the sim too.
   - Recommended: `sim-perceivable-present-ambient`. A prop must be in the sensor picture, and sensing is sim (B5). The slime swarm is 131k nondeterministic GPU agents (B8).
   - Depends: everything.

2. **`fauna-identity`: what names a fauna member?**
   - Options:
     - `field-member`: a member is a `ChunkId` of a fauna field, and fields gain fauna kinds.
     - `fauna-arm`: a third `TargetRef` arm with its own id.
     - `entity`: each fauna is an `Entity`.
   - Recommended: `field-member`. It reuses every chunk owner (B3, B6), and `ChunkId` was named for this. `entity` is ruled out by cost (B10).
   - Depends: cut 1 and every later cut.

3. **`fauna-motion-model`: how do fauna move?**
   - Options:
     - `closed-form-plus-events`: each class's law is a pure function of zone time, and reactions (scatter, flee, pursue) are sparse event records that decay lazily, as wear does.
     - `stepped-anchor`: each swarm's frame is a `KinematicBody` with a steering law, stepped per tick, and members are closed-form offsets from it.
     - `boids`: members are stepped individually with neighbour search (Reynolds; spatial hashing, PA:20-21).
   - Recommended: `closed-form-plus-events`. It is deterministic at any step size, costs nothing per tick and saves for free. A law written in closed form is also the clearest thing a player can learn, which `disguise-held-by-flying` needs. `stepped-anchor` is the fallback if a class's law cannot be written in closed form. `boids` costs per member per tick (B10), and the prior art found no deterministic server-side swarm source (PA:24).
   - Depends: cuts 2 onward. Cut 1 (krill ride the flow) is closed form under any option except `boids`.

4. **`fauna-interaction`: do sharks and whales act on ships?**
   - Options:
     - `props-react`: fauna never harm or block ships; they react (schools scatter, sharks veer, whales sound off), and the reaction is a tell.
     - `sharks-bite`: sharks attack ships that come close.
     - `whales-block`: whales are hulls in `Contacts.Step`.
   - Recommended: `props-react` for this campaign. Danger moves the game from prop hunt to hazard avoidance, and blocking hulls would need entity contact (B2). Either can be a later campaign.
   - Depends: the anchor and disturbance cuts.

5. **`fauna-kill-harvest-lure`: can fauna be killed, harvested or lured?**
   - Options:
     - `shootable-respawning`: members break and respawn through `Zone.Wear` like rocks; harvest and lures are later.
     - `untouchable`: shots pass through fauna.
     - `harvest-economy`: fauna yield goods through the mining yield table.
   - Recommended: `shootable-respawning`. It comes free with `field-member`, and shooting to test a return is the prop-hunt check, taxed by its own launch flash and heat (PA:35-36).
   - Depends: cut 1 (it inherits rock wear unless this is ruled `untouchable`).

6. **`fauna-habitat`: where do fauna live?**
   - Options:
     - `hosted-by-bodies`: each class favours host bodies (krill and minnows around planets and in belt orbits, whales around gas giants, sharks wherever minnows are), placed by the zone generator from the zone seed.
     - `everywhere`: uniform scatter.
     - `authored-only`: fauna appear only in scenario zones.
   - Recommended: `hosted-by-bodies`. Fauna then become a readable feature of a zone's bodies, and hiding has a geography.
   - Depends: cut 1's generator rule (it places krill on planet orbits by default).

7. **`fauna-distance-representation`: how does a school read at range?**
   - Options:
     - `aggregate-far-individual-near`: beyond a class's resolution distance an observer sees one return per school, with summed light at its centroid; near it, it sees members.
     - `individuals-always`: every member that passes `ChunkVisible` is its own return.
     - `aggregate-only`: a school is always one return.
   - Recommended: `aggregate-far-individual-near`. It matches how radar and sonar read layers and flocks (PA:9, PA:14), keeps the HUD legible, and gives a ship near a school a place to hide that ends when the hunter closes in.
   - Depends: the contact-presentation cut and aetheria-ew's blend judge.

8. **`fauna-disguise-path`: how does a ship pass as fauna?**
   - Options:
     - `returns-alike-plus-blend`: below `Identified`, a ship's return and a fauna return share one marker, so any ship can hide among fauna by how it flies, with no gear; aetheria-ew's blend gear carries the disguise past identification by claiming a fauna `BackgroundKind`, judged by an envelope that includes the class's motion law.
     - `blend-only`: only blend gear lets a ship pass as fauna.
     - `returns-alike-only`: no gear; identification always ends the disguise.
   - Recommended: `returns-alike-plus-blend`. aetheria-ew made imitation gear scarce (`ew-imitation-availability-ruled`), so `blend-only` would make prop hunt a rare event. With `returns-alike-plus-blend`, gearless hiding is the common game and gear is the event.
   - Depends: aetheria-ew `ew-blend` (cut 6) and contact presentation. This is a cross-campaign question, and Self may file it in aetheria-ew.

9. **`fauna-shadowing`: can a ship hide in a whale's return?**
   - Options:
     - `defer`: whales are bright, separate returns for now.
     - `merge-within-resolution`: an observer who cannot resolve two returns sees one.
   - Recommended: `defer`. Sensing has no resolution concept today (B5). Adding one is a sensing change, which belongs to aetheria-ew or a sensing campaign, not to fauna.
   - Depends: nothing in this campaign.

## First cut (draft spec): `fauna-krill-swarms`

**Waits on:** rulings on `fauna-in-sim`, `fauna-identity`, `fauna-habitat` (recommended options assumed) and
`fauna-kill-harvest-lure` (`shootable-respawning` assumed; under `untouchable`, `Wear` refuses fauna fields and
that adds one test).
**Base:** `origin/master`; branch `eureka/aetheria-fauna-krill-swarms`. Overlap with in-flight passes: none (B13).
**Model:** Sonnet Hands. Cap: 100 calls. Estimate: about 7 files, +260/-10 lines, of which about 150 are tests.

**Objective:** krill clouds exist in generated zones as fields of a fauna kind. They are sensed, targeted, shot and
drawn through the chunk path, and they move by the krill law: they ride the orbital flow, and each member sways
within a bounded radius.

**Scope:**
1. `FieldKindData` (ZoneData.cs:122-136) gains two append-only keys, `SwayRadius` (`Key(3)`, world units, default 0)
   and `SwayPeriod` (`Key(4)`, seconds), and `SwarmWeight` (`Key(5)`, default 0): how often swarm generation picks
   this kind. A rock kind has sway 0 and swarm weight 0. A krill kind has belt `GenerationWeight` 0, so belts never
   draw it (ZoneData.cs:143-158, "Zero never picks it").
2. `FieldKinds.Assign` (ZoneData.cs:143) takes the weight to read, so belts and swarms share one weighted pick (one
   rule, one path). Add no second picker.
3. `AsteroidBelt` (Zone.cs:785-808) takes the field's resolved sway when `Zone` builds it (Zone.cs:170-175).
   `Pose` adds a bounded closed-form offset: `SwayRadius * float2(cos(2π(t/SwayPeriod + h₁(i))), sin(2π(t/(SwayPeriod·φ) + h₂(i))))`.
   Here `h₁` and `h₂` are hashes of the member index (`SimulationDice.Mix` of it), and φ is a fixed irrational
   ratio so the path is not a circle. The offset's length is at most `SwayRadius·√2`, and that figure is the bound
   the provider uses. `Radius` (:791) adds it too. With sway 0, the pose is bit-for-bit today's.
4. `BeltTargets` (BeltTargets.cs:109) adds the field's sway bound to `slack`, so every region's distance and bearing
   bounds still hold for every member. No other change to the provider.
5. `ZoneGenerator.GenerateZone` (ZoneGenerator.cs:40-210) places krill swarms after the bodies exist. For each
   planet or gas giant body, with a probability from `ZoneGenerationSettings` (Settings.cs:109-135, new fields
   `KrillSwarmProbability`, `KrillSwarmCount`, `KrillSwarmArc` (turns) and `KrillSwarmWidth` (world units), with code
   defaults), it adds an `AsteroidBeltData` whose orbit is parented to that body. Its members are clustered: phase
   within `KrillSwarmArc` of one centre and distance within `KrillSwarmWidth` of one radius. Its `Kind` is picked by
   `FieldKinds.Assign` on `SwarmWeight`. Draw from the generator's existing `random` (:52), so the result follows the
   zone seed. No kind with positive `SwarmWeight` means no swarms.
6. Out of scope (later cuts): a krill look in the renderer (krill draw through the belt path as small rocks); the
   `SectorRenderer` belt count; the catalog authoring of a Krill kind (cut 1b); obscuration.

**Tests** (new `tests/Aetheria.Shared.Tests/FaunaSwarmTests.cs`, as a `partial class MiningCut3Tests` to reuse
`BuildSceneAt` and `Belt` (MiningCut3Tests.cs:74-130); kinds are authored in the test catalog, never re-derived):
- `SwayIsBoundedAndPure`: for a swaying field, every member's `ChunkPose` at many times lies within the sway bound
  of its orbital slot (`AsteroidBelt.Pose` with sway 0). A zone stepped to t by 1/60 and by 1/7 gives equal poses
  (`fauna-pose-pure`).
- `RockPosesUnchanged`: a sway-0 kind's poses equal the pre-cut formula. The existing `MiningIndexTests` and
  `MiningCut3Tests` stay green.
- `RegionBoundsHoldForEveryMemberOfASwayingField`: the pattern of `BeltRegionBoundsHoldForEveryRock`
  (MiningIndexTests.cs:105) over a swaying field at several times (`fauna-bounds-hold`).
- `KrillRideTheFlow`: a swarm's centroid velocity over one sway period equals the orbital velocity at its mean
  distance, within the sway bound divided by the interval (`fauna-tell-holds`).
- `KrillAreSeenThroughTheOneGain`: a krill member is `ChunkVisible` exactly when `PassiveInfo` at its pose ×
  `ChunkVisibility` passes the threshold; its visibility scales with the kind's cross-section (`fauna-one-gain`).
- `KrillBreakAndRespawn`: `Zone.Wear` breaks a krill member and it respawns at zone time, as a rock does. Under
  `untouchable`, this test asserts the refusal instead.
- `GeneratedZonesHostKrill`: with a catalog that holds one rock kind (belt weight 1) and one krill kind (belt
  weight 0, swarm weight 1), a generated zone has swarms only on planet or gas giant orbits, each with the krill
  kind, members inside the arc and width, and no belt with the krill kind. Generating the same galaxy zone twice
  gives equal swarms. Use `RestoredHullsTests.TutorialZoneSettings()` with the krill fields set.

**Hand mutants** (one list to `mutation-table.sh`, each expected to fail its test):
- drop the sway term in `Pose`;
- sway radius × 2 in `Pose` but not in the bound (the bound must catch it);
- omit the sway bound from `slack`;
- `Assign` reads `GenerationWeight` for swarms;
- the generator ignores `KrillSwarmArc` (full ring);
- the generator uses a new `System.Random` instead of the zone's `random`;
- the sway uses `_time` rounded to the frame (a function of dt).

**Verification:** one Yggdrasil job: build, the full `Aetheria.Shared.Tests`, and the mutant table. Recipe:
`F:\Projects\eureka-handoff-aux-2026-10-12\hands-aetheria-common-ag.md`.

**Promises for Soul:** `fauna-pose-pure`, `fauna-bounds-hold`, `fauna-tell-holds` (krill), `fauna-one-gain`,
rocks unchanged, no per-tick member loop (grep `Zone.Update` and the step functions for any loop over
`Asteroids`), and generation that is deterministic per zone.

## Cut sequence (one line each; each is mapped when it is about to run)

1. `fauna-krill-swarms`: krill fields through the chunk path, with the sway law and generation (drafted above).
1b. `fauna-krill-catalog`: an AetherDb command that authors the Krill field kind (cross-section, sway, swarm weight).
2. `fauna-field-presentation`: fauna kinds pick their own look (instanced sprites or particles per kind, not
   asteroid meshes); `SectorRenderer` counts belts only. Waits on 1.
3. `fauna-minnow-schools`: the minnow kind; the sway law gains a fast milling term per kind. Waits on 1.
4. `fauna-anchors`: a field frame that moves across the flow under a class law (shark patrol and sprint, whale arcs),
   per `fauna-motion-model`. Waits on that ruling and 1.
5. `fauna-sharks-and-whales`: the two kinds and their generation (sharks near schools, whales around gas giants),
   thermal light for whales. Waits on 4, `fauna-habitat`.
6. `fauna-disturbance`: sparse disturbance records; schools scatter from hulls and shots, sharks veer
   (`props-react`). Waits on 4, `fauna-interaction`.
7. `fauna-returns`: what an observer is shown of fauna (background markers, aggregate far and individual near), one
   marker shared with unidentified ship returns. Waits on `fauna-distance-representation`, `fauna-disguise-path`,
   and aetheria-ew's contact presentation.
8. `fauna-in-ew-blend`: fauna `BackgroundKind`s with motion-law envelopes in aetheria-ew's judge. Owned by
   aetheria-ew cut 6 or after it, per `fauna-disguise-path`.
9. `fauna-krill-obscure`: dense krill thin sight lines through `Zone.Obscuration`, as vapour clouds do (one gain
   term, no second one). Waits on 1, an operator yes.
10. `orbit-sync-helper`: the generic flying aid that holds a ship on the local orbital flow. It is not fauna-specific
    and may belong to the flight campaign. Waits on an operator yes.
