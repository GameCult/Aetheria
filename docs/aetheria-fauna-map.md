# Aetheria spaceborne fauna: map

Campaign `aetheria-fauna`. Target: `docs/aetheria-fauna-target.md` (content r2). Written by Imagination
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

## Cut sequence r1 (superseded by the r2 sequence below)

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

## r2: the flow, placement and the content the rulings opened

Written by Imagination (agent imagination-aeth-fauna-r2, session self-2026-10-13-ag2) against `origin/master`
`1746e8bd`, after the operator ruled on the nine questions above. The questions above are answered; their rulings
are in the mind. This section adds the body facts the flow ruling needs, the forks it opened, and the r2 cut
sequence, which replaces the r1 sequence above. The krill draft above stays as the shape of the krill cut; it is
re-mapped against the flow owner and the placement ruling when it is about to run.

### Body facts (r2)

Probed at `1746e8bd`.

| # | Question | Result |
|---|---|---|
| B15 | What is the flow? | `Volumetric.cginc`: `globalFlow(pos)` = `Tri3D(pos/_FlowScale - (0,_FlowScroll,0)) * _FlowAmplitude` with y halved (:130-135); `Tri3D` is the cross product of two normalised `tri3` folds, the second at the golden ratio (:46-47, :69-74); `slopeFlow(grad)` = `normalize(grad)` turned 90 degrees times `_FlowSwirlAmplitude` plus `normalize(grad)` times `_FlowSlopeAmplitude` (:137-141); `flow(pos)` sums the two under the keywords `FLOW_GLOBAL` and `FLOW_SLOPE` (:148-161). Consumers: `Stardust.compute:139-142` (each particle is placed at `-flow * lifetime * period`, a closed-form advection over a cycle) and the nebula's noise advection (`Volumetric.cginc:185-208`). |
| B16 | Who owns it today? | The renderer. `VolumeSampling.Update` pushes the parameters from `environment.Flow` and accumulates `_flowScroll += GlobalScrollSpeed * Time.deltaTime` on the frame clock (`VolumeSampling.cs:96-106`). `environment` is `ActionGameManager.CurrentEnvironment`, which always returns `Settings.DefaultEnvironment` (`ActionGameManager.cs:222-228`), a field of the presentation `GameSettings` ScriptableObject (`GameSettings.cs:40`). `ZoneEnvironment` and `FlowSettings` are already `ServerShared` MessagePack types (`Environment.cs:8-61`), but no zone holds one. The sim cannot see the flow. |
| B17 | What flow is live? | `Assets/Resources/Settings.asset:396-402` (LFS): `GlobalScale` 512, `GlobalAmplitude` 15, `GlobalScrollSpeed` 0.025, `Period` 8, `SlopeAmplitude` 0, `SwirlAmplitude` 0. Only the global term runs; `FLOW_SLOPE` is off in the shipped game. Probe: the horizontal global flow averages 6.06 u/s (max 12.2) across a zone, the same as the rail speed 2π = 6.28 u/s. |
| B18 | Does the slope term have a sim twin? | It reads `_NebulaSurfaceHeight`, a GPU render of brush meshes (`Assets/Shaders/Brushes/*`), which is a second lowering of the heightfield `Zone.GetHeight` owns (`Zone.cs:627-660`). Its `getUV` negates world position (`Volumetric.cginc:81-84`), so its texel gradient points downhill in world terms. `normalize` of a zero gradient is NaN (:139); today the keyword keeps that from running. |
| B19 | What clock do presenters use? | Zone time: `Zone._time` (double, `Zone.cs:70`). Belts are drawn at it (`ZoneRenderer.cs:543-547`), gas giant wave phase too (:587-588). `Zone.Settings` is the public `PlanetSettings` (`Zone.cs:59`), the world-law block every zone already receives. |
| B20 | Placement prior art and probe | `docs/research/2026-10-13-fauna-placement-prior-art.md`. A CPU transliteration of the operator's `Slime.compute` with her prefab's parameters, fed by `Zone.GetNormal`: bit-identical run to run, 9 s for 40k agents × 600 steps on 256², trail on the shoulders of the wells and in filaments between them (0 % in flat space, 5.7 % in cores), correlation 0.28 with a closed-form slope-suitability map. Adding the flow reshapes the network (correlation 0.34 with the no-flow map). |
| B21 | Is a generated zone recomputed? | No. A zone pack is generated once, on first entry (`ActionGameManager.cs:727`, `??=`; the entrance in `RunStart.cs:29`), and saved with the galaxy (`Galaxy.cs:70`). Placement computed at generation is saved; it never has to reproduce on another machine. |
| B22 | Can Unity check a shader against C#? | Yes. `ShieldPanelCut3Verify.cs:18, 47-55` dispatches compute kernels and reads them back in batchmode run without `-nographics` (verified 2026-09-18), driven by `tests/mutation_tests_shield_panel_cut3.py`. That is the precedent for the flow parity check. Hands worktrees on Starfire have imported Libraries (`C:\aeth-wt\lock-on\Library` and others). |
| B23 | Overlap with in-flight passes | ew-claims (Weapon.cs, Entity.cs, Sensing.cs, SchematicDisplay.cs, tests), scenarios-lock-on-r3 (Scenarios/LockOn.cs, Scenarios.cs, a test) and sensor-catalog-r2 (Behaviors, Listener.cs, Entity.cs, `GameData/Aetheria.cc`, AetherDb, tests) touch none of the flow cut's files. The flow cut edits `Settings.asset` (LFS); none of the three does. |

### Decisions made here, not forks

- **The scroll becomes zone time.** `sim-knows-the-flow` makes the flow a sim fact, and `fauna-pose-pure` needs it to
  be a function of zone time. `_FlowScroll` is therefore `GlobalScrollSpeed × Zone.Time`, derived, and the frame-clock
  accumulator is deleted. Where no zone exists (menus), the same function runs on the presentation clock; that is a
  different input to the one function, not a second law. `_FlowPeriod` and the noise phases on `_Time` stay
  presentation: they animate how the nebula shows the flow, not the flow.
- **The flow settings live with the zone's world laws.** `FlowSettings` moves from `ZoneEnvironment` (read by no
  zone) to `PlanetSettings.Flow`, which every `Zone` already receives (B19). One zone flow for every zone, as today.
  Per-zone environments (Sol's cloudless bubble) are a later cut that adds a zone-pack override; the owner stays
  `Zone`.
- **The slope term is ported and guarded.** It is part of the law the ruling names, so the owner carries it; its
  term is skipped when its amplitudes are zero (as the shader's keyword does), and a zero gradient gives zero, not
  NaN, on both sides. Its parity is pinned as a function of the gradient; making the heightfield's two lowerings
  agree is follow-up work (`flow-slope-gradient-parity`).

### Questions (admitted in the mind)

`aetheria-fauna:question:fauna-placement`, `:fauna-flow-coupling`, `:flow-moves-what`, `:flow-slope-terms`. The
options and recommendations are in the mind; the research for the first is the placement prior-art file.

### Cut sequence r2 (superseded by the r3 sequence below)

1. `fauna-flow-field` (cut spec admitted): the flow's owner moves into `ServerShared` (`FlowField`, `Zone.Flow`), the
   shader lowers it from zone time, and a Unity parity check pins HLSL to C#.
2. `fauna-habitat-placement`: the generation pass per `fauna-placement`; its settings record in `ServerShared`;
   food at well shoulders and wormhole mouths; Sol excluded; budget measured. Waits on 1 and that ruling.
3. `fauna-krill-swarms`: the krill draft above, re-mapped: krill fields placed by 2, swaying and drifting by
   `fauna-flow-coupling`. Waits on 1, 2. It cannot run beside 1: it needs `Zone.Flow`, and both edit `Zone.cs` and
   `Settings.cs`.
4. `fauna-krill-catalog`: AetherDb authors the Krill field kind.
5. `fauna-field-presentation`: fauna kinds pick their own look; `SectorRenderer` counts belts only.
6. `fauna-minnow-schools`: minnows, fast milling, grazing the krill filaments.
7. `fauna-anchors`: field frames that move across the flow under class laws (`fauna-motion-model-ruled`).
8. `fauna-sharks-and-whales`: the two classes, generated by 2's map (sharks on steep flanks, whales round gas giants).
9. `fauna-disturbance`: sparse reaction records; schools scatter, sharks veer; reactions are tells.
10. `fauna-attacks-strike`: sharks strike and minnow schools scour, through the ordinary damage path.
11. `fauna-attacks-signal`: whale song as noise in the shared signal space (with aetheria-ew), drifter discharge.
12. `fauna-harvest-goods`: catalog goods per kind (krill paste, photophores, heat-organs, whale bone with strange
    matter, spider silk) and yields through the economy's yield table; fields refill by respawn.
13. `fauna-harvest-gear`: resonance fishing, nets and trawls as gear with makers (the lore names Orbital Forge's
    retooled resonators); trawls leave straight-edged channels.
14. `fauna-void-spiders`: nets strung across trojan points, snaring and tethering what drifts in; silk.
15. `fauna-gas-giant-drifters`: a cloud-deck class bound to gas giants.
16. `fauna-edge-giants`: larger classes found only far from Sol; the class mix scales with distance from Sol.
17. `fauna-springs`: wormhole mouths feed placement and the refill tide.
18. `fauna-genre-uses`: cultivators' harvesting techniques, necrotech-bound fauna, mecha that ride fauna; each lands
    with its owner's campaign, none trimmed for genre consistency.
19. `sol-flatspace-zone`: a per-zone environment override; Sol is cloudless, flowless and fauna-free, with krill
    haze outside its Oort edge.
20. `fauna-returns`: what an observer is shown (`fauna-distance-ruled`, `fauna-disguise-path-ruled`).
21. `fauna-in-ew-blend`: fauna background kinds with motion-law envelopes in aetheria-ew's judge.
22. `fauna-krill-obscure`: dense krill thin sight lines through `Zone.Obscuration`. Waits on an operator yes.
23. `flow-moves-matter`: loose matter drifts with the flow, per `flow-moves-what`.
24. `flow-slope-gradient-parity`: the brush-rendered heightfield and `Zone.GetHeight` agree, so the slope term can
    be switched on (`flow-slope-terms`).
25. `orbit-sync-helper`: the generic flying aid that holds a ship on the local orbital flow. Waits on an operator yes.

## r3: placement is a closed form evaluated at run time

Written by Imagination (agent imagination-aeth-fauna-r3, session self-2026-10-14-ag) against `origin/master`
`b836af48`, with the flow owner read at `eureka/aetheria-fauna-flow-field` `5f5a1e4d`. The operator ruled
`fauna-placement-ruled` against `physarum-at-generation`: placement is "deterministic based on the gravity slope and
flow that makes nice patterns", because "the level is constantly changing". The target is revised to r3 (Placement
law, invariants `fauna-placement-closed-form`, `-story`, `-nested`, `-no-pop`). The B20 probe and the r2 rationale for
a generation pass stand as evidence of what the slime look is; the generation pass itself is dropped. Prior art for
the closed form: `docs/research/2026-10-14-fauna-placement-closed-form.md` (`CF:<line>`).

### Body facts (r3)

| # | Question | Result |
|---|---|---|
| B24 | Does the gravity field move? | Yes, in closed form of zone time. Orbit positions are `OrbitData.Evaluate(frac(_time / Period + Phase)) * Distance` plus the parent's position, cached per tick (`Zone.cs:476-497`). `GetHeight` subtracts each gas giant's `RadialWaves(..., _time * speed)` (`Zone.cs:645-655`), and `Sun` derives from `GasGiant` (`Zone.cs:771`), so the sun's well ripples too; `GasGiantData` defaults give wave speed x8 (`ZoneData.cs:199`). The slope at a point is a function of `_time`, and nothing in `GetHeight` can be evaluated at another time without a time parameter. |
| B25 | Which pointwise closed form makes the slime look? (scratch probe, not committed) | A pure-Python transliteration of `FlowField.Global`/`Slope`/`Scroll` at `5f5a1e4d` and `Zone.GetHeight`/`GetNormal`/`PowerPulse`/`RadialWaves` at `b836af48`, on the B20 zone (sun, four planets, a gas giant; radius 2000; live flow scale 512, amplitude 15, scroll speed .025), 256² grid at t = 100 s, float64 (C# is float32). Density images were written per candidate, member-dot images under fixed per-candidate thresholds (jittered 16 u lattice, about 49k candidates), and correlated with the B20 Physarum maps (64x32 blocks). Results follow in the next rows. Scripts, images and how to rerun: `docs/research/2026-10-14-fauna-habitat-probe/` (README there). These rows' member counts normalise density by its zone maximum, which the law may not do (CF:56); B25g gives the counts under the spec's law. The first `members_c6_band_soft_windrows_swirl.png` was a mislabelled render and is not kept. |
| B25a | Slope band alone (`c0`: the operator's `Deposition(slope)`, zero past slope .8) | Rings on every planet's shoulder, a bright bridge where two shoulders meet; correlation with Physarum A .517 (B .483), the highest of all candidates. 1,435 members. Changes 3.3 % of members in 1 s and 20 % in 8 s from the wells' ripples alone (B24). |
| B25b | Band times flow convergence (`c1`, gain 3; `c6`, gain 1.5, slope terms on: swirl 6, slide 1.5) | Langmuir-windrow analogue (CF:17-18). The band keeps its rings, cut by the flow's convergence into a crisp triangular texture: `Tri3D` is piecewise linear (`tri`), so its derivatives are piecewise constant and the convergence field is a tiling of hard-edged triangles and diagonal streaks. `c6` correlation .464 (B .449); `c1` .428. Members gather in clumps on the shoulders with thin lines between wells. Under continuous evaluation 20-22 % of members change in 1 s and 89-94 % in 8 s: the flow scrolls a full first-octave phase in 40 s (1 / .025). Convergence std over the zone is .147 /s live and .151 /s with the slope terms on. |
| B25c | Band times flow-along-contour (`c5`, narrow; `c7`, wide) | Members where the flow runs along the well's contour (neither climbing nor falling). Fine scalloped arcs, more organic than `c6`, but sparse (94 members narrow, 395 wide) and unstable: 42-134 % of members change in 1 s. `c7` correlation .480. |
| B25d | Okubo-Weiss strain (`c2`) and gravity saddles (`c3`, `c4`) | `c2` (band times sigmoid of W, CF:11) correlation .507, texture like `c1`. `c3` (negative Hessian determinant of the height: outer shoulders and saddles between wells) makes clean rings with dark gaps and no links, .377. `c4` (`c3` times convergence) .316. |
| B25e | Does a presence fade on the density stop pops? (`docs/research/2026-10-14-fauna-habitat-probe/pop_probe.py`) | No. `c6` with a fade band of .1 around each threshold, 600 shoulder candidates, 180 steps of 1/60 s from t = 100: the density moves by up to .267 in one step, so presence moves by up to .35 in one step (4 steps over .25; .56 with a band of .05). The same with the slope terms off, so the jumps are the global `Tri3D` term's: its convergence changes almost discontinuously in time at some points. |
| B25f | Does reading the flow term at the member's own cycle start stop them? (`docs/research/2026-10-14-fauna-habitat-probe/pop_cycle.py`) | Yes. Band at now, convergence at the member's cycle start (period 8 s, hashed phase), same fade and candidates: presence moves by at most .0003 per step within a cycle, 0 steps over .25; the flow term changes only at the 221 cycle turns, where `fauna-flow-coupling-ruled` already fades the member. The slope band moves smoothly (B24's ripples are continuous). `FlowField.At` takes a scroll, and `FlowField.Scroll(settings, t)` is pure in t, so the flow at a past zone time needs no new owner. |
| B25g | The options under the spec's law (`habitat_options.py`, `pop_maps.py`) | Absolute density, fixed constants of `cut-fauna-habitat-placement.r1`, candidate thresholds .1 + .9h, fade .1, slope terms on, 49,083 candidates at 16 u. Hosted at t = 100 and changed under continuous evaluation in 1 s / 8 s: band-only 4,388, 2.0 % / 12.2 %; band-times-convergence 2,235, 10.3 % / 47.1 %; band-times-strain 2,721, 7.9 % / 31.3 %; band-times-contour-flow 2,307, 15.8 % / 49.2 %. No-pop, convergence law, 5,607 shoulder candidates, 60 steps of 1/60 s, largest presence step with cycle turns excluded and members moving > .1 in a step: presence-fade .33, 2 members; cycle-sampled-flow .0010, 0; cycle-sampled-all .0000, 0. Images: `option_density_<option>.png`, `option_members_<option>.png`, `option_nopop_<option>.png`. |
| B25h | Is the triangular lattice in the convergence the law's or the probe's? (`lattice_probe.py`) | The law's. Along a 2,048 u transect at 0.5 u spacing, all 40 of the 40 largest convergence jumps lie within 1 u of a kink line predicted from `tri`'s arguments in `FlowField.Tri3D` (both octaves; 53 lines cross the transect). The derivative step does not make it: convergence with step 6 and step .5 correlates .964. Rounding the inputs to float32 moves the flow by at most 5.6e-9 u/s (arithmetic stays float64, so this bounds input rounding only). The shader never renders the flow's derivative; it advects nebula noise and stardust by the flow's value, which is continuous, so the lattice is not visible in game today. A density driven by convergence would carry it into member placement: crisp in a density render, a soft patchiness at the member density of `option_members_band-times-convergence.png`. |
| B26 | Is Sol a zone the code can tell apart? | No. No `ServerShared` code names Sol (grep of `Sol\b` outside `Solar`/`Solid`/`Solve`); a zone's seed is `GalaxyZone.Name.StableHash()` (`Zone.cs:144`, `CombatSeed`). `fauna-none-in-sol` needs the per-zone environment of `sol-flatspace-zone`. |
| B27 | Is there placement code to cut? | No. Nothing under `Assets/Scripts/ServerShared` or `tests/Aetheria.Shared.Tests` names habitat or fauna at `b836af48`. The generation pass of r2 was never built. |

### Decisions made here, not forks

- **Candidates are a hashed jittered lattice with fixed per-candidate thresholds**, not a blue-noise set. It is the
  simplest deterministic, position-stable construction (CF:42 [M]); it is HZD's shape (CF:33 [V]), and nesting
  follows from the fixed thresholds (CF:40). Blue noise (Wei 2008, recursive Wang tiles, CF:42-43 [S]) buys evenness
  the jitter already roughly gives; it can replace the lattice later behind the same `Candidate` function.
- **The hash comes from `SimulationDice.Mix`** on the zone seed, a new `HabitatStream` and the cell, so placement
  uses the one dice owner (B9) and no `UnityEngine.Random`.
- **The convergence is normalised by a fixed scale in the settings** (.15 /s, B25b), never by a zone-wide statistic,
  which would make the law path-integrated (CF:56).
- **Settings are a record, bound per class later.** `HabitatSettings` lands with `Habitat`; the krill cut binds a
  record to the krill field kind. Per-class tuning by eye follows the `flow-slope-terms` pattern.
- **Sol** waits for `sol-flatspace-zone` (B26), which makes Sol host no fauna kind.

### Questions (admitted in the mind, raised in `cut-fauna-habitat-placement.r1`)

`aetheria-fauna:question:habitat-density-driver` (which flow quantity shapes the band; the images above are its
evidence) and `aetheria-fauna:question:habitat-no-pop` (how a member is carried through a change of hosting).

### Cut sequence (r3, one line each; each is mapped when it is about to run)

1. `fauna-flow-field` (spec admitted; third Soul pass): the flow's owner in `ServerShared` (`FlowField`, `Zone.Flow`).
2. `fauna-habitat-placement` (spec admitted, waits on 1 and the two questions): `Habitat`, `HabitatSettings`,
   `Zone.HabitatDensity`/`HabitatPresence`, fixed-threshold candidates, tests and the cost measured.
3. `fauna-krill-swarms`: fauna fields derived per zone (not generated), krill members at hosted candidates, advected
   cycles (`fauna-flow-coupling-ruled`), the index provider and `ChunkVisible` through presence. Waits on 1, 2.
4. `fauna-krill-catalog`: AetherDb authors the Krill field kind and its `HabitatSettings`.
5. `fauna-field-presentation`: fauna kinds pick their own look and fade by presence; `SectorRenderer` counts belts only.
6. `fauna-minnow-schools`: minnows, fast milling, a habitat on the krill density.
7. `fauna-anchors`: field frames that move across the flow under class laws (`fauna-motion-model-ruled`).
8. `fauna-sharks-and-whales`: sharks by krill density (ideal free distribution, CF:49-50), whales round gas giants.
9. `fauna-disturbance`: sparse reaction records; schools scatter, sharks veer; reactions are tells.
10. `fauna-attacks-strike`: sharks strike and minnow schools scour, through the ordinary damage path.
11. `fauna-attacks-signal`: whale song as noise in the shared signal space (with aetheria-ew), drifter discharge.
12. `fauna-harvest-goods`: catalog goods per kind and yields through the economy's yield table; fields refill.
13. `fauna-harvest-gear`: resonance fishing, nets and trawls as gear with makers.
14. `fauna-void-spiders`: nets across trojan points, snaring and tethering; silk.
15. `fauna-gas-giant-drifters`: a cloud-deck class bound to gas giants.
16. `fauna-edge-giants`: larger classes far from Sol; the hosted class mix scales with distance from Sol.
17. `fauna-springs`: wormhole mouths raise habitat density (a pointwise term of distance to a mouth).
18. `fauna-genre-uses`: cultivators, necrotech and mecha uses, each with its owner's campaign.
19. `sol-flatspace-zone`: a per-zone environment override; Sol is cloudless, flowless and hosts no fauna kind.
20. `fauna-returns`: what an observer is shown (`fauna-distance-ruled`, `fauna-disguise-path-ruled`).
21. `fauna-in-ew-blend`: fauna background kinds with motion-law envelopes in aetheria-ew's judge.
22. `fauna-krill-obscure`: dense krill thin sight lines through `Zone.Obscuration`. Waits on an operator yes.
23. `flow-moves-matter`: floating items, mines and vented clouds drift with the flow (`flow-moves-what-ruled`).
24. `flow-slope-gradient-parity`: the brush-rendered heightfield and `Zone.GetHeight` agree (`flow-slope-terms`).
25. `orbit-sync-helper`: the generic flying aid that holds a ship on the local orbital flow. Waits on an operator yes.

## r4: placement is a trophic DAG

Written by Imagination (agent imagination-aeth-trophic-dag, session self-2026-10-14-ag) against `origin/master`
`7633408f` (the flow owner `FlowField`/`Zone.Flow` is on master) and CultLib `main` `6c902fd4`. The operator ruled
`habitat-trophic-dag`: none of the r3 density drivers gives "the filament feeling"; placement is "a few layers of
ecological abstraction, all deterministic, to author the various species with", whose trophic levels "form a DAG
which we can evaluate in either shader passes or in C#". `voronoi-primitive` adds the weighted Voronoi she likes.
Self relayed a correction on 2026-10-14, her words: "Parity between shader and C# is CultMath's whole raison
d'etre". The target is revised to r4 ("Placement law"). The r3 rows B24-B27 and the habitat probe stand; B25's
candidates are superseded as designs. Prior art: `docs/research/2026-10-14-trophic-dag-filaments-prior-art.md`
(`TD:<line>`, tags [V] read, [S] search summary, [M] memory or derivation). The species the graph authors come from
the lore branch `eureka/aetheria-fauna-lore` `74c7e212`, note `Open Water` (`OW`).

### Body facts (r4)

| # | Question | Result |
|---|---|---|
| B28 | What does CultMath already give a node library? | Integer PCG hashes `pcg`, `pcg3d`, `pcg4d` (Jarzynski and Olano), "integer-only, so dxc and C# agree bit for bit" (`packages/cultmath/src/CultMath/math.cs:393-426`, `shaders/CultMath.hlsl:396-426`). `cellular(float3)`: Worley F1 and F2 - F1 with analytic gradients, jitter from `pcg3d` of the integer cell, 3D, unweighted (`math.cs:849-975`, `CultMath.hlsl:440-503`). `smin_grad` (`math.cs:831-847`, `CultMath.hlsl:428-437`). `distance_to_segment` in C# only (`math.cs:319`; no HLSL twin). No weighted cellular, no diagram over explicit weighted sites, no site-graph gate. |
| B29 | How does CultMath own parity? | Two legs, one owner each (`packages/cultmath/docs/design.md:202-206`): HLSL against C# by the mirror test, which compiles the text of `CultMath.hlsl` as C# and compares float32 bit patterns (`design.md:118-124`), plus a clean `dxc -T lib_6_3` compile (`design.md:114-116`); GLSL against C# by golden fixtures run on a device. The mirror proves the composition in the text, not that a GPU agrees (`design.md:119-121`); device HLSL runs are consumers' today (Asura `gpu-snoise-parity`; Aetheria `FlowFieldParityVerify`). CultMath ships a Unity compute smoke (`packages/cultmath/scripts/unity-smoke/`). Aetheria pins `cultmath-unity-v0.4.1` (`Packages/manifest.json`). |
| B30 | Does Aetheria already keep a math twin outside CultMath? | Yes. `FlowField` uses CultMath's `math`, but its `Tri`, `Tri3` and `Tri3D` are Aetheria functions (`FlowField.cs:66-72`) whose HLSL twin is `Volumetric.cginc`, pinned by Aetheria's own `FlowFieldParityVerify` (`RelativeTolerance` 1.5e-4). That is a second parity owner beside CultMath. |
| B31 | What is parked in CultMath that a DAG would use? | The tape: steps 2-5 (expression graph lowered to bytecode with C# evaluators, pruning, an HLSL interpreter, meshing) are parked until Asura is stable (`docs/cultmath-tape-target.md:3-8`, ruling `tape-target-unparks-when-asura-stable`). "Truth and lowerings" (`:84-92`): the field is a typed CultCache document, and the CPU evaluator and the HLSL interpreter are lowerings of it. Step 1 (intervals) and affine forms are on main. |
| B32 | Do the candidates make filaments? (`docs/research/2026-10-14-trophic-dag-probe/`, README there; wells orbit, slope terms on, t = 0) | `skein` draws strands between neighbouring wells, bent by the flow (`density_skein.png`); `lattice` draws a fine cellular web, dense on the shoulders, dim elsewhere, with no long links (`density_lattice.png`); `skein-web` draws both: strands from well to well with a web hanging off them (`density_skein-web.png`, `layer_L2-krill.png`). Correlation with the B20 Physarum map at 64x32 blocks (this measures the envelope, not filaments: a block is 62 u, wider than a strand): band .512, lattice .359, skein-web .296, krill .277, skein .225, slack .171. Hosted krill at 16 u: band 2,075, skein-web 1,475, skein 639, lattice 419. |
| B33 | Does the site graph pop as wells orbit? | The hard Gabriel graph over the six bodies changes its edge set 29 times in 1,400 s (one gas-giant orbit), each change a strand appearing or vanishing whole (TD:33 [M], now measured). The soft gate (empty-disc margin through smoothstep(-.15, .15)) moves by at most .00075 per 1/60 s. |
| B34 | Do the candidates pop under the krill carriage? (`run1.log`, `win.log`, `cyc.log`; fade .1, 60 steps of 1/60 s from t = 100, flow read at the member's cycle start, cycle turns excluded) | Largest presence step: band .0035, slack .013, skein .054, lattice .124 (8 steps over .1), skein-web .493 (8 steps). The lattice steps are not discontinuities: a traced member's web value rises .03 per step for 5 steps, a thin weighted border sweeping a fixed candidate. Widening the search window to 7x7 changes nothing; lowering the weight boost to .6 removes the lattice steps (.047); reading the lattice drift at the member's cycle start, as the flow is read, removes them at full boost: lattice .011, skein-web .066, 0 steps over .1 (`cyc.log`). |
| B35 | How fast do hosted sets turn over? | Members changed in 1 s, all terms / flow frozen at t's cycle (wells only): band .032 / .032; skein .244 / .106; lattice .594 / .449 (.262 with the drift at cycle start); skein-web .305 / .186 (.216 with the drift at cycle start); slack .089 / .089. The strands ride their wells at about 6 u/s across 16 u candidates, so a tenth of strand members change each second through the fade: motion, not popping (B34). |
| B36 | What does a stencil predator cost? (Python, one evaluation) | krill .26 ms, minnows 3.19 ms (9 krill), sharks 29.3 ms (81 krill): each ring-stencil level multiplies the cost by 9. Hosted: minnows 274 of 5,432 candidates at 48 u; sharks 19 of 755 at 128 u; lanterns 36 of 3,063 at 64 u. |
| B37 | What does a diagram over the bodies draw, per metric? (`docs/research/2026-10-14-body-voronoi-probe/`, README there; sites = the six bodies, weight = each well's Gaussian width `sigma = r / sqrt(128)`, edge = first-order border distance through a 40 u Gaussian, warped as the strands) | `power` (`d^2 - sigma^2`): long, nearly straight edges spanning the zone between the bodies, a triangle of filaments (`edges_power.png`). `add` (`d - sigma`): long hyperbolic arcs, two near-parallel bars north of the sun (`edges_add.png`). `mult` (`d / sigma`): rings round the small bodies only; the sun's cell covers most of the zone (sigma ratio up to 4.7:1; the ring round a 274-mass planet is about 264 u) (`edges_mult.png`). `pot-newton` (equal `m/d`, i.e. multiplicative with weight = mass, CultLib `tape-register-model.md` 5a): rings of about 12 u, the sun swallows the zone. `pot-well` (equal depth on the zone's real well profile): rings like `mult`, plus dotted arcs where each well's compact support ends (r/2). `flow-aniso` (`mult`, across-flow distance 2.5x): edges shattered into short fragments, because the metric turns with the flow on the flow's 512 u scale. `lp05`: axis-aligned bars across the zone; `lpinf`: square cells round the small bodies. Correlation with the B20 Physarum map: mult-strands .388, power-strands .371, mult .335, flow-aniso .322, power .313, skein-web .296, add .263. |
| B38 | Do they pop as the bodies orbit? (16 u krill, fade .1, 60 steps of 1/60 s from t = 100, flow warp and web drift at the member's cycle start, bodies now; `run2.log`, `run3.log`) | Worst presence step / members changed in 1 s / hosted: skein-web .014 / .205 / 1,501; power .029 / .312 / 3,817; power-strands .029 / .269 / 4,965; flow-aniso .037 / .300 / 4,090; pot-newton .048 / .299 / 689; add .090 / .338 / 4,077; mult .091 / .279 / 3,270; mult-strands .091 / .238 / 4,462; pot-well, lp05, lpinf 1.0 (1, 72 and 237 steps over .1). `mult`'s .091 is the web, not the edge: without the cell boost .023, without the alignment .055 (`diag2.log`); the cell weights are read from the moving edges now. |
| B39 | Why did B34's reading pop more? (`run1.log`, `diag_edges.log`, `trace.py`) | `trophic_probe.py` reads only the flow's scroll at the member's cycle start; the slope terms (swirl 6, slide 1.5) read the wells now. The slope terms use the normalised slope direction, which turns quickly on flat ground as the wells move, and the 20 s warp is a lever on it: at a candidate on slope .019 the warped point moved 1.4 u per 1/60 s. With the whole warp at cycle start, the edges alone drop from .152 to .023 (mult) and .126 to .029 (power), and the ruled skein-web from .066 to .014. B34's skein-web number was this reading's. |
| B40 | Where does the first-order border distance break? | Where the metric's gradient is singular or jumps: `lp05` (singular on the axes through each site), `lpinf` (jumps on the diagonals), `pot-well` (unbounded at the support rim): presence steps of 1.0. Also where the second-nearest site changes, the normaliser `|grad(F2 - F1)|` jumps: invisible inside the 40 u edge, visible in a 4x halo (power's web with no cell boost: .197-.301, `diag2.log`). A raw `F2 - F1` profile has no such jump but its width varies with the weights. |
| B41 | Does the 5x5 lattice window hold the body-pulled weights? (`diag2.log`, 3,385 points) | Cell weights span .5-2.6 (5.2:1), and the alignment stretches across-edge distance up to 2x more. 5x5 against 7x7: mult differs at 1 point by .28; power and add at none. CultLib `lattice-site-weight.md` (`21281a39`) finds the window must grow with the weight ratio (5x5 off by .017 at about 12:1); the alignment is a query-point anisotropy, which does change winners, unlike a query-point scalar weight. |
| B42 | What does one evaluation visit? | Every variant is pointwise over explicit sites; none needs a grid. Large layer: the 6 bodies (the two winners' gradient is analytic). Web: 25 cells, each reading the large layer at its centre (25 x 6), so 181 sites per evaluation; with strands 193 (6 soft-Gabriel edges, strand and halo); skein-web 187. Python: .36-.74 ms per evaluation. CultMath: `mult` and `power` are gap (2) as mapped; `add`, Minkowski and `flow-aniso` extend gap (2) with a metric parameter, `flow-aniso` also with a direction operand; `pot-well` is the zone's well profile as a per-site score, an Aetheria function, not a CultMath metric. The web's per-cell read of the large layer is the cell-centre weight placement of `cultmath-tapes:question:lattice-site-weight-source`. |

### Decisions made here, not forks

- **The layers are the lore's web.** `OW` gives energy budgets (starlight, heat-light, gravity waves, mind-light),
  a current with fronts, shear and slack, and the Skein: "a slime-mould web that grows toward food along the gravity
  slope, linking well to well". The filament layer is therefore the Skein, a species of its own, and the layers
  above read it. The DAG is donor-controlled: each level is a function of the levels below and never regulates
  them (TD:56 [M]); a predator that thins its prey closes a cycle, which needs iteration (TD:56), so it is out.
- **Strands link wells; the wells' Voronoi borders are slack.** In the adhesion model, filaments are borders between
  void cells and the mass sits at the vertices (TD:38 [V]). Here the mass is the wells, so filaments between wells
  are the dual: edges of a site graph, the graphs the Physarum agents approximate (TD:9, prior doc). The wells'
  weighted Voronoi borders are where pulls cancel, which `OW` gives to the ambushers (lanterns, void spiders).
  With multiplicative weights the border round a small well is an Apollonius circle (TD:20 [M];
  `density_slack.png` shows the circles).
- **The site graph's gate is soft.** B33: a hard Gabriel graph pops a whole strand 29 times per gas-giant orbit;
  the soft gate is continuous. The graph is computed per tick from the orbit positions `Zone` already caches per
  tick (B24), over the zone's bodies only; wormhole mouths and belts join as sites in their own cuts.
- **Hunting radius is a stencil, or a widened kernel.** A predator reads its prey's density as the mean over a
  fixed ring of taps: pointwise, exact by definition, the same in C# and HLSL. It costs 9x per level (B36), so a
  stencil reads at most two levels down. Where the prey is a sum of Gaussian kernels (the Skein's strands) a
  predator reads the same kernels widened instead, the exact blur of a Gaussian profile (TD:78 [M]). Grid-only
  operators (blur of arbitrary fields, distance transforms, eikonal paths, jump flooding: TD:19, TD:78) are
  presentation-only, never sim-visible.
- **Fast drift is read at the member's cycle start.** `habitat-no-pop-ruled` reads the krill's flow at its cycle
  start. B34 extends it, measured: the flow warp and the lattice web's drift are read at the member's cycle start;
  the slope band and the site graph, which move slowly and continuously, are read now and carried by the fade.
- **Nodes are CultMath functions; Aetheria composes.** Per Self's relay of the operator. Each primitive the DAG
  needs is a CultMath function with an HLSL twin in `CultMath.hlsl`, pinned by CultMath's mirror test and dxc
  compile (B29). Aetheria's `Habitat` is a composition of those calls and owns no math twin. Gaps to fill in
  CultLib, not helpers in Aetheria: (1) a 2D weighted lattice cellular with the smooth-min blend (her node) on
  `pcg3d` cells and zone time; (2) a weighted diagram over an explicit span of sites (F1, F2 and first-order border
  distance, multiplicative and power weights); (3) the HLSL twin of `distance_to_segment`; (4) the soft Gabriel
  gate over a site span. Gap (5): CultMath has no parity path for a consumer's composition, only for its own text
  (B29), so a presenter that draws a composed layer (Skein glow, glim haze) waits on a CultMath-owned composition
  check rather than a second `FlowFieldParityVerify`. B30's `Tri3D` twin is the existing instance of the same split.
- **The search window follows the weight range.** A multiplicatively weighted lattice node needs its window to
  cover the weight ratio (TD:20 [M]); the probe's 1.6x boost needed no more than 5x5 (B34). The CultMath node states
  the weight range it supports instead of trusting the caller.

### Questions (admitted in the mind)

`aetheria-fauna:question:skein-filament-construction` (which construction is the Skein; images per option in
`docs/research/2026-10-14-trophic-dag-probe/`) and `aetheria-fauna:question:trophic-graph-tape` (hand-written CultMath
compositions now and the tape later, or unpark the tape for this).
`aetheria-fauna:question:body-diagram-metric` (which metric draws the large-scale Skein from the bodies, after the
operator's sketch; images per option in `docs/research/2026-10-14-body-voronoi-probe/`, facts B37-B42).

### Cut sequence (r4, one line each; each is mapped when it is about to run; r3 lines 4-25 stand after line 7)

1. `fauna-flow-field`: on master (`FlowField`, `Zone.Flow`, `FlowFieldParityVerify`).
2. `cultmath-trophic-nodes` (CultLib): gaps (1)-(4) above with HLSL twins, mirror tests and stated weight ranges;
   a CultMath Unity tag. Waits on both questions.
3. `fauna-trophic-habitat` (Aetheria): the CultMath pin bump; `Habitat` composes environment, Skein, glim and krill
   in C# at candidates; per-species settings records; the r3 candidates and fade; tests for the r4 invariants; the
   per-candidate cost measured. Replaces r3 line 2. Waits on 2.
4. `fauna-krill-swarms`: as r3 line 3, on 3's krill hosting.
5. `cultmath-composition-parity` (CultLib): gap (5), a CultMath-owned check that a consumer's C# composition and its
   HLSL twin agree on a device; `FlowField`'s `Tri3D` moves under it. Waits on Self's scoping.
6. `fauna-trophic-presenter`: the Skein and glim drawn in the nebula from the same composition. Waits on 3 and 5.
7. `fauna-minnow-schools`, then predators by stencil and ambushers on slack (r3 lines 6-8), re-mapped as species of
   the DAG per `fauna-bestiary-open` and `OW`.
