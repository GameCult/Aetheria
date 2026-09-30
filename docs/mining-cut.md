# Mining: Target and Cut Map

Date: 2026-09-25 (corrected for M0, 2026-09-30)

Status: target and cut map, from an Imagination pass. Cuts 1 and 2 have landed on `codex/mining` (see the status
header in Part III), which was merged with master at M0 to carry fire control. The first half is the
**target** (the ends). The second half is the **cut map** (the means). When the campaign closes, split the target
out as `docs/mining-target.md` and keep this file as history.

Anchors are against `codex/fire-control-12` HEAD `8bd25f6f`, read from a clean worktree of that commit. **Every
anchor in a fire-control file is stale at `b66ba524`** (`FireControl.cs` is 1,614 lines there; `Splash` is gone); Cuts 3
and 4 below carry the re-anchored versions, and the `8bd25f6f` anchors elsewhere are history. Claims
marked **(probe)** were measured by running code; claims marked **(read)** come from source at the cited anchor.
Probes live in the session scratchpad and are not kept in the repo:

- `miningprobe`: a console build against `Aetheria.Shared` and `tools/AetherDb` (CultLib `45c2f40`, CultMath
  `v0.2.4`). It opens the catalog and a copy of the operator's real save (`GameData/run.cc`, 2026-09-22), reads
  `PlanetSettings` and `GameplaySettings` from `Assets/Resources/Settings.asset` through `AuthoredSettings`, builds
  the densest saved zone as a real `Zone` (entities stripped so the belt cost is isolated), and times
  `Zone.Update` over 3000 ticks at dt 1/60. Run ten times.
- `miningcensus`: walks every stored document in `GameData/Aetheria.cc`.
- `sensorbench`: the inner loop of `Sensor.Execute` (`Behaviors/Sensor.cs:163-189`) against a `ReactiveDictionary`
  carrying the one `ObserveReplace` subscriber `Entity.Activate` installs (`Entity.cs:237-253`).
- The legacy record: `scratchpad/legacy-0305.json` and `legacy-0414.json` (decoded `AetherDB.msgpack` history and
  `Legacy/AetherDB.2021-04-14.msgpack`), and the deleted `GameData/SimpleCommodityData/*.json` at `c6ffcad3^`.

**Scope: mining only.** Fire control Cut 12.4 closed 2026-09-29 (`c50029d5`; 362 tests at that close) and is on
master (`dcd7bbc5`, 2026-09-30), merged into this branch at M0. Cuts 3 and 4 below edit `FireControl.cs` and
`Entity.cs`, code fire control rewrote after this map was first written, so they are re-anchored to `b66ba524`. Cut 1
and Cut 2 touched no fire-control code. §Overlap names every shared line.

---

# Part I: Target

## Operator's words (2026-09-25)

On the asteroid simulation:

> "Calculating the positions and sampling all those gravity wells for every asteroid was literally the heaviest
> operation in the game, which is why it got explicit multi threading"
>
> "Would be nice if we could slim that down"

The campaign:

> "Make that the mining campaign, then. You target the chunk, hit it with a weapon that does the right kind of
> damage for the ore you want to extract, and each chunk rolls its loot table with a weight determined by some
> arcane combination of the damage type, the chunk's composition, and the stats of the weapon you hit it with."

## Standing rulings this campaign obeys (not re-litigated)

- **Same inputs.** "Player and AI are controlling the same ship with the same inputs" (`docs/locomotion-cut.md`
  R2, branch `codex/locomotion`). A chunk becomes a target through the same slot, the same writer and the same
  fire path for both.
- **Reciprocal formulas.** "When the formula is unintuitive, we don't make the handle more opaque for correctness,
  we fix the formula. Plenty of core gameplay formulas are reciprocal just for this reason." Every weapon stat in
  the loot weight reads as a benefit.
- **WeaponModifiers are labels, not behaviour** (fire-control Cut 12 ruling). The loot roll reads weapon data
  fields (`WeaponData.DamageType`, `Damage`, `Penetration`), never `WeaponModifiers`.
- **Provenance** (`docs/item-provenance-target.md`): mined ore enters the economy as a lot in the one
  `ProvenanceLedger`. Aetheria grows no second provenance owner. Branding is derived, never stored.
- **Content audits check pre-breach history.** Absence from `GameData/Aetheria.cc` is not absence. Recoverable
  content is an operator review item (Cut 7).
- **A shot is decided at Commit and performed at arrival** (fire-control R4). The shot's dice are its own
  (Cut 6b, 9.1: `MixSeed(CombatSeed, ShotId)`); nothing reads a shared stream.
- Delete before adding. A gap goes to its owner. A new persisted field on an existing document is nullable, or
  has a default that the MessagePack nil rule already produces.

## Objective

A player or an AI targets an asteroid chunk, fires the weapon whose damage type suits the ore it wants, and each
hit yields ore into its cargo, weighted by the chunk's composition, the damage type and the weapon's stats. Every
unit is born as a lot with honest provenance: extracted, here, from this belt. The belt simulation behind it costs
nothing per tick.

## Invariants

1. **A chunk's pose is a pure function of time.** Position and rotation are evaluated when something asks: the
   renderer, targeting, range, hit resolution. Nothing stores a pose. There is one orbit function, and if a GPU copy
   is ever added, a parity test pins it to that function.
2. **Zone.Update does no per-asteroid work** and starts no threads.
3. **One target slot.** A ship has one current target, which is either a ship or a chunk, set by one writer path
   that both the player and the AI call. The fire path reads the slot, not a parallel channel.
4. **One fire path.** A shot at a chunk is a `PendingShot` that goes through `FireControl.Fire`, `Step`, `Commit`
   and `Apply`, priced from the same factor functions a shot at a ship uses: Accuracy, PSensor, Sigma, PSpread and
   the exact Gaussian mass over a silhouette. A chunk's silhouette is its disc.
5. **The loot is decided at Commit.** The yield and the ore picks come from the shot's own generator, in a fixed
   draw order after the hit draw. Apply only performs them.
6. **Every mined unit carries a lot.** The lot is `Extracted` from this zone and this belt, and stacks merge only
   within one lot. A mined unit never exists without a ledger entry.
7. **A chunk never yields more than its authored total**, whatever weapon breaks it.
8. **Composition is derived, never stored** (recommended in Q7): a pure function of the belt's identity and the
   catalog's ore table.

## Canonical implementations

- Orbit evaluation: `OrbitData.Evaluate` (`ZoneData.cs`) and `Zone.GetOrbitPosition` (`Zone.cs:182-217`). Chunk
  pose gets one owner beside them.
- The hit model: `FireControl` (`FireControl.cs`), Cut 12 shape.
- Cargo: `EquippedCargoBay.TryStore(SimpleCommodity)` (`Entity.cs:1808-1861`). Pickup already uses
  `Entity.CargoBays.Any(c => c.TryStore(item))` (`Gameplay/ShieldManager.cs:44`).
- Provenance: `ProvenanceLedger`, `Lot`, `Extracted` (`Provenance.cs`); mints through `ItemManager`.
- Deterministic dice: `FireControl.MixSeed` (`FireControl.cs:461`) and `Zone.CombatSeed` (`Zone.cs:60`).

## Not consumers

- `AetheriaEve` and the old daemon rebuild: taxidermy.
- Njordr: shape precedent only (`Extracted { source, at }`, `njordr-engine/src/lib.rs:98`), adopted as local typed
  state, as the provenance campaign already did.
- `Agents/Tasks/Survey.cs`, `HaulingTask.cs`, `StationTowing.cs`: data-only task classes with no reader. They are
  not touched.
- The commented-out 2020 `MiningController` (`399b933a`, deleted in `c6ffcad3`): design intent only. It drove
  `MiningTool` through a `Switch`. The operator's model replaces that tool.

## Out of scope

- Backward economy generation, routes and piracy (next scope, `docs/three-gates-scope.md:141-145`). This campaign
  writes the first real `Extracted` lots, and generation will later synthesize more of the same shape.
- Refining, crafting from ore, prices for ore (every live simple commodity has `Price 0` except Ammo, priced 1000 **(merged-tree census)**).
- AI cargo offload at a home station (the 2020 controller's `GoHome`). An AI miner with full cargo stops mining.
- Electronic-warfare clutter and masking beyond reflected light ("a ship ... can float in a debris field or asteroid belt
  and pass as junk", `docs/three-gates-scope.md:155-157`) stays parked there, and so does black-body emission for
  chunks. Detection of a chunk by reflected light is **in** this campaign (Q2 correction, 2026-09-30; Cut 3).
- Orbits beyond belts. `Zone.Update` still steps the planet orbits (`:156-161`), which cost about 31 us/tick in the
  probe zone. That is cheap and out of scope.

---

## 0b. Identity, lifecycle, authority

| Kind | What names it | What happens to it over time | Who decides |
|---|---|---|---|
| **Chunk** | `ChunkId = (CultRecordKey Field, int Index)`. `Field` is the field's record key in the run store (today always an `AsteroidBeltData`; debris fields are another kind, Q2); `Index` indexes its chunks (`Asteroids` for a belt). Stable across save/continue, because `RunSave.Commit` writes the run store's whole view, bodies included (`SavedGame.cs:97-99`), and nothing rewrites `Asteroids` after generation. | Born at zone generation (`ZoneGenerator.cs:141-153`). Lives for the run. Removed with every `BodyData` by `RunSave.Clear` (`SavedGame.cs:124`). | `ZoneGenerator` writes `Asteroids` once. Nothing else may write it. |
| **Chunk pose** (position, rotation, velocity) | Derived: `(ChunkId, zone time)`. | Never stored. Evaluated on demand. | One function on `AsteroidBelt` (Cut 1). Forbidden writers: any stored pose array, any per-tick loop. |
| **Chunk wear** (damage taken, broken-until time) | Keyed by `ChunkId` inside the owning `Zone`. | Damage accumulates at Apply. When it reaches the chunk's hitpoints, the chunk breaks with `RespawnAt = zone time + AsteroidRespawnTime(size)`. At `time >= RespawnAt` it is whole again, read lazily with no timer. Persisted in `ZonePack` (new nullable key 6) with the zone's own `Time`, and pruned of expired entries at pack. | `FireControl.Apply` through one `Zone` method is the only writer. `Zone` owns the storage. |
| **Chunk composition** | Derived: `(belt identity, ChunkId, catalog ore table)`. | Never stored (recommended, Q7). A retune of the catalog retunes every belt. | The catalog author, through per-ore data on `SimpleCommodityData`. The derivation function is the only reader of that data for belts. |
| **Ore affinity and abundance** | Per ore: new nullable fields on `SimpleCommodityData` (keys 12 and 13 are free. Only key 11 is retired on `SimpleCommodityData`; keys 6, 7 and 8 are live, inherited from `ItemData` (`SpecificHeat`, `Conductivity`, `Price`, `ItemData.cs:291-298` @`b66ba524`). The comment at `ItemData.cs:304` wrongly lists 6, 7, 8 and 11). | Authored in Studio. Changes with the catalog. | Catalog author. |
| **Global loot tunables** | `GameplaySettings` / `PlanetSettings` fields (Cut 5). | Authored. Today they live in `Settings.asset`. They move with the settings-globals campaign (`docs/settings-globals-cut.md`, mapped, not landed). | Catalog author. |
| **Mined lot** | `LotId` in the run's `ProvenanceLedger`. `Lot { Design = the commodity, Origin = Extracted { Zone, Commodity, Body }, Quality, Roles = null }`. | Minted at the first extraction from a (zone index, belt, commodity) source (Q6). Reused for later units from that source. Collected by `Reachable` when no instance reaches it. A later extraction after collection mints a fresh lot. | `ItemManager.ExtractedLot(zone, belt, commodity)` is the only writer. Its source→lot index is derived at load, not persisted. |
| **Mined unit** | A `SimpleCommodity` instance, `Data` = commodity, `Lot` = the source lot (`Lot` moves to the `ItemInstance` base, key 11). | Stored in cargo. Stacks merge only within the same `Data` and `Lot`. A split copies `Lot`. | `EquippedCargoBay` owns placement. The mint owns `Lot`. |
| **Current target** | `Entity.Target` (runtime; not packed, `EntitySerializer.cs` has no target field **(read)**). | Set by targeting inputs or AI state. Cleared by the existing entity-removal paths. A broken chunk stays held and prices 0 until retargeted (Cut 3). | Player: the `ActionGameManager` targeting handlers. AI: the Minion states. Both write the one slot. |
| **Mining shot** | `ShotId` (`Zone.NextShotId`), as every shot. | Fire → Commit → arrival. Never serialized (`FireControl.cs:852-855`). | `FireControl`. |

No cell is empty. The cells marked "recommended" depend on the rulings below and are rewritten if a ruling goes the
other way.

---

## Operator questions, in the order they block cuts

The 2026-09-30 rulings on Q4-Q13 are recorded in the Part III status header. Where a question below was written before its ruling, the ruling wins and the recommendation text is history.

Self delivers these one at a time (memory: pace operator questions). Cut 1 needs no ruling. The CPU-versus-GPU
belt renderer is a stated default: CPU, one orbit function, so no parity test is needed. Reopen it on request.

**Q1 (blocks Cut 2). What becomes of `MiningTool`, `ResourceScanner` and the Drill Bit?** **Ruled 2026-09-25: A.**
Operator: "Bring back the drill, indeed."
The operator's model is "hit it with a weapon". `MiningTool` (`Behaviors/MiningTool.cs`, 76 lines) is a separate
damage channel with its own DPS, efficiency, penetration and range. Nothing assigns its target (`AsteroidBelt` and
`Asteroid` have no writer), and it throws `KeyNotFoundException` if it ever executes with an unset belt
(`:59` indexes before the `IsSet` check at `:60`). No live item carries it. The legacy catalog did: **Drill Bit**
(GearData; `MiningToolData` DPS 1-2.5, efficiency 2-5, penetration 1-5, range 5-20) and **Resource Scanner**
(`ResourceScannerData` range 100-200, minimum density 25-5, scan duration 5-2) are in both `legacy-0305` and
`legacy-0414` **(probe)**.
- A: Delete `MiningTool` and `MineAsteroid`. Retire union tag 26 (comment, do not reuse). Restore the Drill Bit in
  Cut 7 as a short-range weapon, a `ConstantWeaponData` beam at Kinetic, penetration high and range 5-20, so
  content keeps its meaning through the one fire path.
- B: Keep `MiningTool` as a second extraction channel beside weapons.
- **Recommended: A.** B is a second hit path with its own dice and its own yield rule, which is exactly the
  split Cut 3 of fire control removed. `ResourceScanner` stays parked (Q2 correction, 2026-09-30).

**Q2 (blocks Cut 3). How does a chunk become a target?** **Ruled 2026-09-25: A, one slot, two kinds.**
Operator: "Note that I have been saying chunks specifically because this should generalize to more than just
asteroid fields ("rocks"), there's various kinds of debris fields we'd want to represent, too. Maybe that's
pedantic, it doesn't change the schema. One target slot, two kinds." So a target is an `Entity` or a **chunk of a
field**; asteroid belts are one kind of field and debris fields another. `ChunkId` is `(Field, Index)` (landed in
Cut 2); new chunk-surface code speaks of chunks and fields, never belts or rocks. Composition and loot tables
(Q4, Q7) are per field kind: a debris field rolls salvage through the same weighted roll.

**Detection (operator correction, 2026-09-30, replacing the 2026-09-25 "deferred to the electronic-warfare campaign" text).** Operator: "We can just assign a reflectivity and cross section to each chunk just like we do with entities, then their visibility depends on how much light is shining on them, no new mechanism needed ... This is what I actually intended with my ruling about deferring detection."

The options as mapped, kept for the record: The options below were
costed against the live code:

- **A. One slot, two kinds.** `Entity.Target` becomes `ReactiveProperty<TargetRef>`, where `TargetRef` is a small
  readonly value that is either an `Entity` or a `ChunkId`, with value equality. Readers that need a ship read
  `Target.Value.Entity` (null for a chunk). FireControl's factor functions take a `TargetRef`, with the chunk
  branch at each target-shaped read. Blast radius **(read)**: the count has grown since the first read. At `b66ba524`: 28 `Target` reads in `ActionGameManager.cs`, 22 in
  `FireControl.cs`, 11 in `Entity.cs`, plus Unity weapon-manager sites (`GuidedProjectileManager`, `LightningGunManager`,
  `ConstantParticleWeaponManager`, `GuidedProjectile`) the first count omitted. The first read was about 60 in 12 files
  (`ActionGameManager.cs` 20, `FireControl.cs` 11, `Entity.cs` 8, `TurretController.cs` 7, `LockWeapon.cs` 5,
  `EntityInstance.cs` 3, `Weapon.cs` 2, `Combat.cs`, `Minion.cs`, `Ship.cs:84`, `ActionBarSlot.cs`), plus
  `PendingShot.Target` and `ShotOutcome.Target`. Most sites are mechanical (`.Entity`).
- **B. A parallel chunk slot.** `Entity.TargetChunk` (a `ReactiveProperty<ChunkId?>`) sits beside `Target`,
  behind one writer that clears the other. About 15 sites. It represents one concept in two fields, and every
  reader must know to check both.
- **C. Chunks become Entities.** Rejected on cost **(probe)**. One sensor-bearing entity pays **184 ns per
  observed entity per tick**, which is **149 us/tick for the 809 chunks** of the probe zone. With ten ships in a
  zone that is about 1.5 ms/tick, the same order as the per-asteroid gravity sampling the operator just asked to
  remove (1.9 ms/tick, below). It would also add 809 entries and subscriptions to every entity's
  `EntityInfoGathered`, a hull and schematic per rock, and `Entity.Update` per rock.
- **D. Promote a chunk to an Entity while targeted.** Rejected: its identity flips kind mid-shot, and a pending
  shot would outlive its target's kind.

**Recommended: A**, sequenced after fire-control Cut 12 closes. "A ship has one target, and a target is a ship or a
rock" is the one-owner sentence, and it matches the parked electronic-warfare direction: "every body presents a
signature through the shared detection interface". Consequences that come with A, named here and not decided
later:
- **Visibility.** Superseded twice; the live design is Cut 3 (a chunk is detected by reflected light through the
  shared sensor gain rule). The scanner-as-chunk-sensor plan, the fallback range tunable, unconditional visibility and
  PSensor 1 are all struck. `ResourceScanner` stays parked.
- **Stance.** `StanceAllowsFire` (`Weapon.cs:101`) allows fire on a chunk.
- **Lock weapons.** Launchers cannot lock a chunk. `LockWeapon.cs:86` requires `IsHostileTo`, and rocks are not
  hostile, so guided and lock launchers (GT 3K, LRMM72, SRMM72, pswarm, scorched void policy) cannot mine. Rule it
  if that is wrong.
- **Blasts (fire-control 12.4).** Superseded by Q10 (ruled A, 2026-09-30): a blast covers every chunk it overlaps, and
  the shooter gets the loot (Cut 4). The text this replaces was written against `Splash`, which Cut 12.4 replaced with
  `Detonate(Zone, float2 worldPlanar, float radius, float damage, DamageType)` (`FireControl.cs:1074` @`b66ba524`); that
  function takes no shooter and walks `zone.Entities` only (`:1084`), so a blast beside a rock did nothing to it.

**Q3 (blocks Cut 2). Do chunks respawn, and does wear survive save/continue?** **Ruled 2026-09-25: A.**
Today `MineAsteroid` sets `RespawnTimers` (`Zone.cs:316`), but nothing ever decrements them, and `AsteroidExists`
(`:247`) ignores them. A broken asteroid renders at size 0 forever and can still be mined **(read)**. None of this
is persisted.
- A: Respawn after `AsteroidRespawnTime(size)` (authored 10-100 s), as an absolute `RespawnAt`, and persist wear in
  `ZonePack`.
- B: Depletion is permanent for the run, persisted.
- C: No persistence. A belt heals when the zone reloads.
- **Recommended: A.** It keeps the authored curve, costs no timer, and makes save/continue honest. C turns a reload
  into a belt reset.

**Q4 (blocks Cut 5). The loot weight formula.** The operator owns this ("some arcane combination"). Proposed:

For one hit by weapon `w` (damage type `d`, damage `D`, penetration `P`) on chunk `c` (size `s`):

- **Units:** `n = min(D, remaining HP) / HP(s) × Yield(s)`, taken as its floor plus one Bernoulli draw on the
  remainder. `HP` is the existing `AsteroidHitpoints` curve (authored 10-200). `Yield` is a new `PlanetSettings`
  `ExponentialLerp`. *Why:* a chunk yields the same total however it is broken (Invariant 7). Damage is a benefit
  because it gets you there faster.
- **Weight of ore `o`:** `w(o) = Affinity_o(d) × Comp_c(o)^(1/Depth)`, with `Depth = 1 + P × MiningDepthPerPenetration`.
  - `Comp_c(o)`: what is in this rock (Q7).
  - `Affinity_o(d)`: the right tool for the ore. It is authored per ore and per damage type, and defaults to 1.
  - `Depth`: penetration digs past the common stuff. It flattens the composition toward the rare ores and never
    makes the common ore impossible. Precedent: the 2020 and live rule `pow(x.Value, 1f / penetration)`
    (`Zone.cs:323`). It is written as `1 + P·k` so that penetration 0, which covers 15 of 18 live weapons, reads as
    the raw composition rather than dividing by zero.
- Each unit's ore is drawn from the normalized weights, in a fixed order after the hit draw.

Worked examples. Composition is Rock .60, Water .20, Gold .15, Uranium .05. Illustrative affinities: Rock Kinetic
1.5, Water Thermal 2, Gold Electric 2, Uranium Optical 2, and 0.5 elsewhere. `MiningDepthPerPenetration = 4`.
Computed, not estimated:

| Weapon (live stats) | d | P | Rock | Water | Gold | Uranium |
|---|---|---|---|---|---|---|
| Kinetic, pen 0 (Earp) | Kinetic | 0 | .818 | .091 | .068 | .023 |
| Autocannon | Kinetic | .25 | .687 | .132 | .115 | .066 |
| pretty pretty bang bang | Thermal | 0 | .375 | .500 | .094 | .031 |
| ChargeBlast+- | Electric | 0 | .414 | .138 | .414 | .034 |
| ColdFire | Optical | 0 | .522 | .174 | .130 | .174 |
| Optical at pen .25 (none exists) | Optical | .25 | .309 | .179 | .155 | .357 |

So the right damage type roughly triples an ore's share, and penetration multiplies a rare ore's share further.
Alternatives to put to the operator:
- A: the formula above (Damage sets quantity; damage type and penetration set weights).
- B: A, plus `DamageSpread` as breadth: a yield multiplier `1 + DamageSpread × k`, which strips more rock per hit at
  the raw composition. It is a benefit, and GT 3K, plight, pswarm, SRMM72 and scorched void policy author it **(probe)**.
- C: A, with no penetration term (damage type and composition only). This is the simplest to read.
- **Recommended: A.** Every term is one sentence, and every weapon stat in it is a benefit. B is a clean add later,
  because it changes no weight. Note: penetration is scheduled for a retune (fire-control F12-2, "Retuning
  penetration is a follow up"). `MiningDepthPerPenetration` is the one knob that absorbs that retune.

Tunables: `Affinity` and `Abundance` per ore on `SimpleCommodityData` (catalog, Studio). `MiningDepthPerPenetration`
on `GameplaySettings`. `AsteroidYield` and `ChunkCompositionVariance` on `PlanetSettings`. Hazard: none of them may be
CultMath-typed while settings are Unity-serialized (memory: Unity-serialized CultMath fields load as zero).

**Q5 (blocks Cut 5). Is the roll per hit, or once when the chunk breaks?**
- A: Per hit, with units ∝ damage (as in Q4). Each hit's own weapon decides its ore, so switching guns mid-rock
  matters, and the only state kept is HP.
- B: Once at break, weighted by the weapon that broke it.
- C: Once at break, with hits accumulating per-ore weight as they land. This needs per-chunk, per-ore state.
- **Recommended: A.** The operator's sentence ties each weight to "the weapon you hit it with". A gives continuous
  feedback and keeps no memory. B lets a chip shot from the wrong gun at the end decide the whole rock.

**Q6 (blocks Cut 5). Where does the ore go, and how big is a lot?**
- Destination. A: straight into the shooter's cargo at arrival, where a full hold loses the overflow. B: a floating
  pickup. **Recommended: A.** Pickups are Unity-only (`ItemPickup` and collection by `ShieldManager.cs:40-55`), so
  an AI could never collect them, which would break the same-inputs rule.
- Lot granularity. A: one lot per (zone, belt, commodity) source. B: one lot per hit. **Recommended: A.** B makes
  every hit a separate cargo stack, because stacks merge only within one lot, and it grows the ledger per shot. A is
  what backward generation will synthesize anyway: an extraction source at a place.
- Lot quality: 1 for extracted lots. Nothing reads a `SimpleCommodity`'s quality (price and tier read
  `CraftedItemInstance` only, `ItemManager.cs:114,224`). Grade can arrive as its own ruling later.

**Q7 (blocks Cut 5). Where does composition live?** **Resolved 2026-09-30: A′** (a per-kind entry list of commodity, abundance and affinity, derived, never stored per chunk), with Q11 (field kinds are authored catalog records yielding simple commodities). The per-ore `Abundance` on `SimpleCommodityData` recommended below is superseded: it gives one table for every field kind, which the Q2 ruling (tables are per field kind) contradicts.
`BodyData.Resources` (key 4, `ZoneData.cs:53-54`) is on every body and has never had a writer since the 2020
generator (`17673cd2`) was commented out. Its only reader is `MineAsteroid` (`Zone.cs:322-323`). No live belt has
resources **(probe: 8 belts, 0 authored)**.
- A: Composition is derived: `Comp_c(o) = Abundance_o × exp(Variance × g(hash(belt key, index, o)))`, normalized,
  with `g ∈ [-1, 1]`. Nothing is stored. Existing saves work, and key 4 is retired.
- B: `ZoneGenerator` writes `BodyData.Resources` per belt at generation, and the chunk jitter is derived. Belts in
  runs started before Cut 5 have empty compositions until New Game.
- C: Per-belt only, with no per-chunk variation.
- **Recommended: A.** It has one owner (the catalog) and nothing to migrate. It follows the same "derived, never
  stored" spirit as branding. The operator's wording ("the chunk's composition") argues against C. If B is chosen,
  its generator must seed from the belt's identity, not the shared generation stream (`ZoneGenerator.cs:48`),
  because a new draw there would shift every later placement in newly generated zones.

**Q8 (blocks Cut 7). The catalog has no Corrosive or Ionizing weapon, now or in its history.** Live weapons use
Kinetic (9), Optical (3), Electric (3) and Thermal (3) **(probe)**. `legacy-0305` and `legacy-0414` use the same
four types **(probe)**. An ore whose affinity peaks at Corrosive or Ionizing is unreachable.
- A: Author affinities over the four types that exist, and add Corrosive and Ionizing weapons when content wants
  them.
- B: Author new Corrosive and Ionizing weapons in Cut 7.
- **Recommended: A**, with the gap recorded in Cut 7's review sheet.

**Q9 (blocks Cut 6). Which AI ships mine?** **Ruled 2026-09-30: C** (defer; against the recommendation below). Cut 6 is deleted. Nothing assigns a `Mining` task today. `Zone.CreateAgent`
(`Zone.cs:122-129`) gives every NPC a `PatrolOrbitsTask`.
- A: In a zone with belts, an NPC whose loadout has a weapon with nonzero affinity to some belt ore gets a `Mining`
  task on the nearest belt, with an authored share (`GameplaySettings.AgentMinerShare`).
- B: Only dedicated miner hulls or factions mine. This needs faction-territory roles (`docs/faction-territory-target.md:36-49`,
  "extraction").
- C: Defer AI mining. The same path is proven by a test agent only.
- **Recommended: A** for this campaign. B is the faction-territory campaign's call and can replace A's
  eligibility rule without touching the path.

---

# Part II: Body map

## The belt simulation today

**(read)** `Zone` owns belts (`Zone.cs:26`, built at `:91-92`). Each tick, `Zone.Update` (`:149-179`):
1. advances `_time` (`:151`) and clears `_updatedOrbits` (`:152`);
2. waits on last tick's belt tasks (`:153-155`);
3. steps every orbit (`:156-161`);
4. for each belt, copies `NewTransforms` into `Transforms`, copies `NewOrbitPosition` into `OrbitPosition`, and
   starts one `Task.Run(UpdateAsteroidTransforms)` (`:163-168`).

`UpdateAsteroidTransforms` (`:249-275`) evaluates every asteroid into a `float4` (x, y, rotation, damage-scaled
size). The per-asteroid gravity sample is commented out (`:272`). The shader applies height instead: `Asteroid.shader:28-32`
samples `_NebulaSurfaceHeight` per vertex and adds `_AsteroidVerticalOffset`, and the renderer sets that offset
globally (`ZoneRenderer.cs:442`).

**Findings (probe), on the densest zone of the operator's save** (EAC-2265, radius 5418, 8 belts of 123, 29, 101, 132,
269, 99, 30 and 26 asteroids, 809 in total, 56 bodies; that save has generated 1 of its 64 zones):

- **Cost before:** shipped `Zone.Update` with entities stripped costs **400-1760 us wall per tick** (the main
  thread blocks in `t.Wait()` at `:153-154`) and **125-420 us process CPU per tick**, over 5 clean runs.
- **Cost after** (the same zone with no belt work): **31-39 us wall, 26-37 us CPU per tick.** Belts become free
  per tick.
- The belt arithmetic alone, run sequentially, is 76-94 us/tick for all 809 asteroids. The threading bought no
  speed: task scheduling and the blocking wait cost more than the work.
- **One asteroid on demand costs 186-224 ns** (`OrbitPeriod.Evaluate`, `OrbitData.Evaluate` and the parent orbit
  lookup). A targeting query over the largest belt (269 asteroids) is about 55 us, paid when asked and not per tick.
- **The threading is a data race.** **4 of 10 probe runs died** with `InvalidOperationException: Operations that
  change non-concurrent collections must have exclusive access`, thrown from `Zone.GetOrbitPosition`
  (`:209`/`:216`) inside a belt task. Parallel belt tasks, and the main thread's `_updatedOrbits.Clear()` at `:152`
  (which runs before the wait at `:153`), mutate the same `HashSet` and `Dictionary` unsynchronized. .NET detects
  it; Unity's Mono may corrupt silently instead. Deleting the threading is a correctness fix as well as a cost fix.
- **Every pose the sim or renderer reads is exactly one tick stale.** After `Update`, `Transforms` matches the pose
  at the previous tick's time with 0.0000 error, and misses the current pose by 0.209 units at the measured maximum
  speed of 12.6 u/s. That comes from the double buffer (`:165`).
- **The operator's history, confirmed.** `GetHeight` for every asteroid, the commented-out line `:272`, costs
  **1.9-2.1 ms/tick** over 56 bodies. That really was the heaviest per-tick operation. It is already gone from the
  sim, and the shader now does the height.

## Consumers of the belt state

| Surface | Readers (HEAD) | Fate |
|---|---|---|
| `AsteroidBelt.Transforms` / `NewTransforms` (`Zone.cs:505-506`) | `Zone.cs:165,230,273`; `MiningTool.cs:62`; `ResourceScanner.cs:80`; `ZoneRenderer.cs:456,462,467,476`; `AsteroidBeltUI.Update` (`ZoneRenderer.cs:660-676`) | Deleted (Cut 1). The renderer fills its own buffers from the one function. |
| `AsteroidBelt.OrbitPosition` / `NewOrbitPosition` (`:508-509`) | `Zone.cs:166,256,271`; `ZoneRenderer.cs:449,451,662` | Deleted. The belt centre is `Zone.GetOrbitPosition(parent)`. `AsteroidBeltUI` already stores `_orbitParent` (`:593,613`) and never reads it. |
| `BeltUpdates` (`:46`) | `:153-155,167` | Deleted. |
| `NearestAsteroid` (`:226-245`) | none. It reads `Transforms[i].xz` (x and rotation) where it means `.xy` | Deleted. Cut 3 adds the one chunk query. |
| `AsteroidExists` (`:247`) | `MiningTool.cs:61` | Replaced by `Zone.ChunkExists` in Cut 2. |
| `RespawnTimers`, `Damage`, `MiningAccumulator` (`:510-512`) | `Zone.cs:260-264,306-318` | Replaced by the wear store (Cut 2). |
| `Zone._random` (`:33,71`) | `MineAsteroid` only (`:323-324`) | Deleted with `MineAsteroid`, Q1=A. It is a shared stream, which Cut 6b forbids for combat dice. |
| `ActionGameManager` save/continue | no belt reader. `RunSave.Capture` packs entities, orbits and planet refs only | Gains wear through `Zone.PackZone` (Cut 2). |
| `SectorRenderer.cs:84` (belt count), `MapRenderer.cs:55-73` and `MainMenu.cs:257-259` (`ShowAsteroidUI` toggles) | none read poses | Untouched. |

## The mining path today, end to end

Unreachable **(read)**: `MiningTool.Execute` (`MiningTool.cs:56-75`) range-checks against the stale `Transforms` and
calls `Zone.MineAsteroid` (`Zone.cs:297-335`). That accumulates damage and a per-miner accumulator, picks a resource
from the always-empty `beltData.Resources`, and mints nothing: the `SimpleCommodity` mint is commented out
(`:327-333`, `// TODO: Drop item onto the Grid`). No code assigns `MiningTool.AsteroidBelt` or `Asteroid`. The AI
`MiningController` that did (`399b933a`) was deleted in `c6ffcad3`. `Agents/Tasks/Mining.cs` has no reader, and
`AgentTask` is "owned by their Agent and not persisted" (`AgentTask.cs:10`).

## How FireControl sees a target (what a chunk must answer)

**History, anchored at `8bd25f6f`.** Every line number below is stale (`FireControl.cs` is 1,614 lines at `b66ba524`, `Splash` is gone, and the chunk visibility and info rows are superseded by Cut 3). The re-anchored, live version is in Cuts 3 and 4.

**(read, `FireControl.cs` at `8bd25f6f`)**:

| Read | Where | For a chunk |
|---|---|---|
| `source.Target.Value` | `Fire` `:332` | the slot (Q2) |
| `target.Position`, `range` | `PFire` `:170-171`, `Inspect` `:246-247` | `Zone.ChunkPosition` |
| `source.VisibleEntities.Contains` | `PFire` `:173`, `Inspect` `:248` | within chunk detection range (Q2) |
| `EntityInfoGathered[target]` → `PSensor` | `PFire` `:179-180` | info 1 |
| `LockWeapon.IsLocked` | `PFire` `:175` | never locked (Q2) |
| `InArc(toTarget)` | `PFire` `:176` | unchanged |
| `target.Velocity` | `PredictedIntercept` `:135-138`, `Fire` `:349` | `Zone.ChunkVelocity`: analytic orbital velocity plus the parent `Orbit.Velocity` (`Zone.cs:160`) |
| `target.Hull`, `Silhouette(target, hull, aimed, bearing, precision)` | `Forecast` `:196-205`, `CommitProbability` `:486-487`, `Commit` `:531` | a disc of radius `r / SchematicCellSize` cells, one interval `[-R, R)`, `a = 0`, bearing-free |
| `DeviationProbability` (live position) | `:310-316` | unchanged in shape. A chunk's curvature over a 2 s flight is `v²t²/2R` ≈ 0.16 u at the probe belt, well inside any Tracking |
| `zone.Entities.Contains(target)` (gone → miss) | `Step` `:409` | `!Zone.ChunkExists` (broken) → miss |
| `Shield` | `Commit` `:552-555`, `Apply` `:570-575` | none |
| `ApplyHit` | `Apply` `:581` | `Zone.Mine(...)`: wear plus the decided loot |
| `Splash` over `zone.Entities` | `:613-642` (being replaced by 12.4) | Q2: targeted chunk only |

`Silhouette` (`:679-726`) does three things: builds the intervals, merges them, and integrates the Gaussian. The
integration (`:710-716`) and `Span` (`:718`) are the only parts a disc needs. Cut 4 extracts that tail as the one
integrator, used by the hull path and `Silhouette.Disc` alike, so no second copy of `Phi` summation exists.

## The catalog and the legacy record (probe)

- **Live simple commodities (13):** Minerals: Hydrogen, Potassium, Rock. Metals: Gold, New Metals, Uranium.
  Compounds: Methane, Water. Organics: Algae, Animal Products, Bacteria, Fungi. Ammo: Ammo. Every one is priced 0 except Ammo (1000). The catalog oid moved after `8bd25f6f` (`4f218e85`, `dd437580`, locomotion Cut 1): `1862ddc4` then, `2aa79c0a` at `b66ba524`.
- **Legacy (30, identical in `legacy-0305` and `legacy-0414`):** the 13 live ones, plus Crystal, Silicon, Carbon,
  Nitrogen (Minerals); Iron, Titanium, Copper, Lead (Metals); Ethanol, Hydrocarbons, Acid, Oxygen, Carbon Dioxide
  (Compounds); Plant Matter (Organics); and Autocannon, Charged Shotgun, Flak Cannon and Auto Shotgun ammo. That is
  17 missing from live, 14 of them plausible ores.
- **Resource-distribution data does not survive.** The 2020 fields (`ResourceDensity` map layers, `Minimum`,
  `Maximum`, `Exponent`, `Floor`; `17673cd2`) were keys 6, 7, 8 and 11 on `SimpleCommodityData`. Only key 11 stays retired there; 6, 7 and 8 are live `ItemData` keys (`SpecificHeat`, `Conductivity`, `Price`). No values remain in the deleted
  JSON records at `c6ffcad3^` (for example, `Uranium.json` carries only name, mass and category) or in either
  msgpack.
- **Mining gear:** Drill Bit and Resource Scanner exist in legacy only (Q1). Surface Ore Extractor and Deep Ore
  Extractor are behaviourless husks in both live and legacy (`docs/item-provenance-substrate.md:62-64`), and Mining
  Rights is a compound commodity. None of these are touched here. The extractors are station factory gear for
  backward generation.
- **Weapons:** see Q8. The catalog has no `BodyData` (bodies are run records).

## Provenance: how a mined lot is born

`Lot` is on `CraftedItemInstance` at key 11 (`ItemInstance.cs:38`). The provenance map put it there on purpose,
"`SimpleCommodity` gets no lot ... Moving it to the base later with the same key is byte-identical (probe Q4), so
mining yield adds without reshaping" (`docs/item-provenance-cut.md:150-156`). `SimpleCommodity` uses key 2
(`ItemInstance.cs:47`), so key 11 is free on it.

The mint:
- **Who:** the shooter's shot, at Apply.
- **What:** the commodity (`Lot.Design`). `Extracted.Commodity` repeats `Lot.Design`, and the provenance cut chose
  that deliberately (`Provenance.cs:116-120`). It is left alone.
- **Where:** `Extracted.Zone`, which is the run's zone index (`Array.IndexOf(Galaxy.Zones, GalaxyZone)`, the index
  `savedzone-{i}` uses, `SavedGame.cs:103`), plus a new `Extracted.Body` (key 2,
  `CultRecordRef<BodyData>`, nil on any older record). That is Njordr's "source".
- **Quality:** 1 (Q6). **Roles:** null.

Three existing rules must learn about simple lots:
- `RunSave.Commit` collects lot roots from `OfType<CraftedItemInstance>()` only (`SavedGame.cs:110-114`). Mined lots
  would be garbage-collected at the first save unless the roots take every `ItemInstance` with `Lot != 0`.
- The stack merge (`Entity.cs:1838`) matches on `Data` only.
- The stack split (`Entity.cs:1916`) does not copy `Lot`.

A `SimpleCommodity` with `Lot == 0` is unattributed: the ledger starts at 1 (`Provenance.cs:14`), so 0 never names a
lot. That includes every existing loadout ammo stack. Reading such a lot through the ledger's indexer still throws.

## Overlap with work in flight

- **Fire control (main tree, other agents).** Cuts 3 and 4 edit `FireControl.cs` at `Fire`, `Step`, `PFire`,
  `Forecast`, `HitProbability`, `Inspect`, `CommitProbability`, `Commit`, `Apply`, `Silhouette`, `PendingShot` and
  `ShotOutcome`, and they touch `Splash`'s successor from 12.4. They must follow Cut 12.4. Cuts 1, 2 and 5-7 do not
  touch `FireControl.cs`, except Cut 5's loot-draw lines inside `Commit`, which also wait for 12.4.
- **Locomotion (`codex/locomotion`).** Its Cut 4 rewrites `MoveTo` and the heading planner. Cut 6 is deleted (Q9 = C), so
  this campaign no longer depends on locomotion Cut 4.
- **Settings globals (`docs/settings-globals-cut.md`).** Cut 5's new tunables join `GameplaySettings` and
  `PlanetSettings` wherever those live when it lands.
- The uncommitted `AetheriaInput.cs` change in the main tree ("Cycle Target Item") is fire-control work. Cut 3's
  targeting handlers sit beside it in `ActionGameManager.cs:372-404`.

---

# Part III: Cut map

## Status header

Status: cut map. Ends are owned by Part I of this document; Part III owns the means. Updated at M0, 2026-09-30.

**M0 (2026-09-30):** `codex/mining` merged with master (fire control, `dcd7bbc5`) and this map corrected against
`docs/mining-cut-refresh.md` (v2). Cuts 3 and 4 below are the refresh's versions. The refresh's Cut 5 and Cut 7 texts are
the current proposals and are not yet folded into this file; where they differ, the refresh wins.

Rulings (operator), 2026-09-25: Q1 = A, Q2 = A (one slot, two kinds; field generalization), Q3 = A.

Rulings (operator), 2026-09-30:

| Q | Ruling |
|---|---|
| Q4 | A, the formula as written (Cut 5) |
| Q5 | A, per hit (Cut 5) |
| Q6a | A, straight into cargo; overflow lost (Cut 5) |
| Q6b | A, one lot per (zone, field, commodity) source (Cut 5) |
| Q7 | A′, a per-kind entry list (commodity, abundance, affinity), derived, never stored per chunk (Cuts 3 and 5) |
| Q8 | **B**, author Corrosive and Ionizing weapons (Cut 7), against the earlier recommendation |
| Q9 | **C**, AI mining deferred; the path is proven by a test agent in Cut 5. Cut 6 is deleted |
| Q10 | A, a blast covers every chunk it overlaps; the shooter gets the loot (Cuts 4 and 5) |
| Q11 | field kinds are authored catalog records; tables yield simple commodities only (Cuts 3, 5, 7) |
| Q12 | A, launchers cannot mine (Cuts 3 and 4) |
| Q13 | A, reach is the longest active weapon range |
| Q2 correction | Chunks are detected by the existing reflectance rule (reflectivity and cross-section, lit by the suns), not deferred to EW. Struck: the scanner as chunk sensor, the fallback range tunable, unconditional visibility, PSensor 1. `ResourceScanner` stays parked |

**Q14 (how a ship's info on a rock integrates over time) and Q16 (how a belt gets its field kind) remain open.** They
block Cut 3. Self's recommendations (Q14 A, settled value on demand; Q16 B, stored at generation and assigned once for
old belts) are proposals, not operator rulings; Cut 3 is written for them.

**M-Soul content unknown:** no Soul report for Cuts 1-2 exists; the mutation sweep at `22fb4021` stands in. Cut 3
does not start until M-Soul is closed or empty.

**Still owed:**
- From Cut 1: a Unity compile of the `ZoneRenderer` and `AsteroidBeltUI` change (M0 runs it), an isolated Stryker rerun,
  and the operator's fly-through. Nothing after `d9c887c1` records any of them.
- From Cut 2: Soul has not verified the two-commit fix (`34994732`, `93bfdebd`). It folds into Cut 3's Soul pass.

Test counts: the map's 274 after Cut 2 were counted on its own `8bd25f6f` base. On the merged tree (`fc90e936`, master plus
mining) the suite is 498 tests, all passing on Yggdrasil (CultLib `45c2f40`, CultMath `6d5e209`).

**Cut 2: landed 2026-09-25** at `3b9f0f1d`, `c924bae0`, `daa64560`. 272 tests; production net −73 lines; `AetherDb census` byte-identical; break threshold kept strictly `>`; `ZonePack` key 6 nullable, proven by a raw-MessagePack older-record test. Spec discrepancy fixed: the retirement comment for union tag 26 no longer names `MiningToolData`, which the cut's own negative grep forbids. Soul found one real defect, dormant until Cut 4 wires `FireControl.Apply` into `Wear`: a hit on a chunk that was
already broken reset `BrokenUntil` and resurrected it on the spot (any shot resolving after the chunk broke —
travel time, two shots close together). Fixed at `34994732`: a hit on a broken chunk changes nothing and `Wear`
returns false; pinned below and above hitpoints and across a save taken inside the respawn window (both tests fail
on the old code). `93bfdebd` removes the dead `MiningDifficulty: 500` still serialized in `Settings.asset`. Soul also
confirmed the strict `>` threshold cannot stall (wear accumulates across hits; only zero damage never breaks).
274 tests. **Closed**; the two-commit fix's own check folds into Cut 3's Soul pass.

**Cut 1: landed 2026-09-25** at `22a54ccb` and `d9c887c1` (Stryker fix batch: `EvaluateBelt` had no
coverage and `Radius`'s `Max`→`Min` survived). 262 tests. Probe on the operator's densest zone, 10 runs each:
before, 541-3654 µs wall and 120-391 µs CPU per tick with **3/10 runs crashing** on the belt-task race; after,
95-185 µs wall and 99-152 µs CPU, **0/10 races**. Soul closed it: no thread or task left in ServerShared, poses a
pure function of zone time (formula byte-identical to `e729e6d6`), one owner, no per-frame renderer allocation
(the buffer is allocated once in `LoadPlanet`).

Correction to this map's own baseline: the "31-39 µs with no belt work" figure above does not reproduce for a
zone of this shape. Soul's independent probe of the same 56-body, 8-belt layout: 100 µs per tick with belts,
114 µs with the belts replaced by plain bodies at equal orbit count — belts cost nothing per tick. The ~100 µs
that remains is the pre-existing per-body orbit step (about 2 µs per orbit over ~57 orbits), untouched by this
cut. The 31-39 µs was measured on a smaller body count.

Still owed for Cut 1: a Unity compile of the renderer change (`ZoneRenderer.cs`, `AsteroidBeltUI`), which runs
when this branch meets a tree Unity can open; an isolated Stryker rerun by a later Soul (the closing pass's run
collided with its own concurrent build); and the operator's fly-through.

Follow-ups outside this campaign:
- `AsteroidBeltUI.Update` feeds `Quaternion.Euler(90, transform.z, 0)` a rotation in radians where the API takes
  degrees (`ZoneRenderer.cs:666`). It is a presentation defect and is not fixed here.
- The planet orbit step stays per tick (`Zone.cs:156-161`). It is now the whole of the zone's per-tick cost
  (~2 µs per orbit). 2 µs for one orbit evaluation is more than its arithmetic warrants — curve evaluation or
  the `_updatedOrbits` memo is the likely cost. Measure before touching it; a candidate follow-up, not scope.

## Cut order

| Cut | Nature | Depends on |
|---|---|---|
| 1 | Subtraction: belts evaluated on demand. No threads, no stored poses. | none |
| 2 | Subtraction plus one owner: the dead mining path goes, and chunk identity and wear get one owner | Q1, Q3 |
| M0 | Placement: merge master, correct the map | fire-control-12 on master |
| M-Soul | Fix slot for the parallel Soul pass over Cuts 1-2 (content unknown) | Soul report |
| 3 | A chunk is a target, detected by reflected light; the field-kind record appears | M0, M-Soul; Q14, Q16 |
| 4 | A shot at a chunk goes through FireControl; blasts cover chunks | 3; the fire-control no-target fuse ruling |
| 5 | Composition, loot roll, provenance, deposit | 4; Q4-Q7 |
| ~~6~~ | **Deleted** (Q9 = C); the test agent moved into Cut 5 | none |
| 7 | Content: ores, affinities, abundance, Drill Bit, Corrosive and Ionizing weapons (operator review) | 5; Q1, Q8 |

Cuts 1 and 2 are kept apart so Soul can falsify "nothing changed" (Cut 1) separately from "the dead path is gone
and wear has one owner" (Cut 2).

---

### Cut 1. Belts are evaluated when asked

- **Repo/branch:** Aetheria, `codex/mining` from `codex/fire-control-12` `8bd25f6f`. Depends on nothing.
- **First:** re-run `miningprobe` on the branch point and record the before numbers in the commit message (wall and
  CPU per tick, and the race count over 10 runs).
- **Deletes first** (`Zone.cs` at `8bd25f6f`):
  - `:9-10` `using System.Threading; using System.Threading.Tasks;` (2 lines);
  - `:46` `BeltUpdates` (1);
  - `:153-155`, the wait loop (3);
  - `:163-168`, the per-belt copy and `Task.Run` (6);
  - `:226-245` `NearestAsteroid` (20);
  - `:249-275` `UpdateAsteroidTransforms` (27);
  - `:505-506` and `:508-509`, the four `AsteroidBelt` pose fields, and `:517-518`, their allocations (6).
  - About 65 lines in total.
- **Keeps:** `AsteroidBelt.Data`, `Radius` (`ZoneRenderer.cs:452`), and the wear dictionaries (Cut 2 replaces them).
  The per-orbit step `:156-161`.
- **Adds:** on `AsteroidBelt`, one function that is the only place a chunk pose is computed:
  `Pose(int index, double time, float2 parentPosition, PlanetSettings)` → (position, rotation). It is the formula of
  `:269-271`, verbatim, with `time` in double exactly as `_time` is used now. There is also a size read,
  `Size(int index, PlanetSettings)`, which is the rule of `:259-267` moved unchanged, and a batch
  `Evaluate(Span<float4> into, double time, float2 parent, PlanetSettings)` that loops `Pose` and `Size` for the
  renderer. `Zone` exposes them at its own time: `ChunkPose(CultRecordKey belt, int i)` and
  `EvaluateBelt(CultRecordKey belt, Span<float4> into)`. They read `_time` (double) and
  `GetOrbitPosition(parent)`, so no caller passes a float time.
- **Per-file changes:**
  - `Zone.cs:149-179`: `Update` loses the belt block entirely.
  - `MiningTool.cs:59-62` and `ResourceScanner.cs:80`: read `Zone.ChunkPose` instead of `Transforms`. No behaviour
    change; they die or reshape in Cut 2.
  - `ZoneRenderer.cs:447-477`: per visible belt, `Zone.EvaluateBelt` into a renderer-owned `float4[]` (allocated in
    `LoadPlanet`, `:340-362`, sized to the belt). The matrices are built from that buffer as before. The visibility
    bounds use `Zone.GetOrbitPosition(parent)` in place of `belt.OrbitPosition` (`:449-452`).
  - `ZoneRenderer.cs:660-676`: `AsteroidBeltUI.Update` takes the renderer's buffer, and its bounds use its own
    `_orbitParent` (`:593,613`) through `_zone.GetOrbitPosition`.
- **Behaviour change, named:** poses are read at the current time instead of one tick behind. The measured maximum
  difference is 0.21 u in the probe zone. No sim reader is reachable (MiningTool has no writer). The renderer draws
  the rocks where they are now.
- **Authority map:**
  - Owner: `AsteroidBelt.Pose` and `AsteroidBelt.Size`.
  - Inputs: the asteroid record, the parent orbit position at zone time, zone time (double), `PlanetSettings`, and
    wear (size only).
  - Outputs: position, rotation, size, on request.
  - Derived state: every renderer buffer is display-only, owned by `ZoneRenderer`.
  - Forbidden writers: any stored per-asteroid pose in `ServerShared`, any `Task` or thread in `Zone`, and any
    second copy of the orbit formula.
  - Shared paths: renderer, minimap (`AsteroidBeltUI`), and every sim reader, through `Zone`.
  - Deletion line: the listed deletes land in the same commit as the adds. No reader of `Transforms` survives.
- **Verification:**
  - builds: `Aetheria.Shared`, `tests/Aetheria.Shared.Tests`, `tools/AetherDb`, each with both roots (`-p:CultLibRoot=...
    -p:CultMathRoot=...`). The Unity compile is Self's or the operator's step, never batchmode against the main tree.
  - tests (new `MiningCut1Tests.cs`; fixture: an `OrbitData` and an `AsteroidBeltData` upserted into a run-store
    cache, with a non-degenerate belt of at least 20 asteroids at distinct distances, phases and sizes, and
    authored-shape `PlanetSettings` curves, not defaults):
    - `ChunkPoseIsTheOrbitAtZoneTime`: after N updates of uneven dt, every chunk's pose equals the closed-form orbit
      at the zone's time, including the parent's motion. Pins no staleness.
    - `ChunkPoseDoesNotDependOnUpdateCadence`: one update of 1.0 s and 60 updates of 1/60 give equal poses. Pins
      pure function of time.
    - `DamagedChunkShrinksAndABrokenOneHasNoSize`: pins the size rule moved from `:259-267`.
    - Stryker over `AsteroidBelt` and `Zone.Chunk*`. Every survivor is triaged.
  - negative: `rg -n "Task\.Run|System\.Threading|NewTransforms|\.Transforms\b|BeltUpdates|NearestAsteroid|\.OrbitPosition\b|NewOrbitPosition" Assets/Scripts/ServerShared/Zone.cs "Assets/Scripts/Zone Display" Assets/Scripts/ServerShared/Behaviors`
    returns only `Zone.cs:301`, a commented line inside `MineAsteroid` that Cut 2 deletes. After Cut 2 it is empty.
    Tested against `8bd25f6f`: every other hit is a line this cut deletes or rewrites. Widening it to `Assets/Scripts`
    collides with `Galaxy.cs:6-7` (`System.Threading`, a legitimate use) and with `followOrbitPosition`
    (`ActionGameManager.cs:841-843`).
  - probe: `miningprobe` after the cut shows belt cost 0 and 0 races over 10 runs. Record the after numbers.
  - operator: fly through a belt. The rocks orbit smoothly, the minimap asteroid overlay tracks them, and the map
    screen shows them.
- **Operator questions:** none (the CPU renderer is the stated default).

---

### Cut 2. The dead mining path goes, and chunks get one owner

- **Repo/branch:** Aetheria, `codex/mining`. Depends on Cut 1, Q1 and Q3. Written for Q1=A and Q3=A.
- **Deletes first:**
  - `Behaviors/MiningTool.cs` (76) and its `.meta`.
  - `Behaviors.cs:173` `Union(26, typeof(MiningToolData))`, becoming a `// Union(26, MiningToolData): retired, do not
    reuse` comment, per the file's own precedent at `:172`, `:174`.
  - `Zone.cs:297-335` `MineAsteroid` (39); `:247` `AsteroidExists` (1); `:33,71` `_random` (2); `:510-512` the three
    dictionaries (3).
  - `ResourceScanner.cs:71-107`, the survey `Execute` (37), plus `ScanTarget`, `Asteroid`, `_scanTime` and
    `_scanTarget` (`:38-59`). Its data (`Range`, `MinimumDensity`, `ScanDuration`) stays until Q2 rules. If Q2 goes
    against the scanner-as-chunk-sensor, the whole behaviour parks at a tag instead
    (`parked/mining-resource-scanner`), because it has legacy content.
  - `PlanetSettings.MiningDifficulty` (`Settings.cs:32`). Its only reader is `MineAsteroid`.
  - About 160 lines in total.
- **Adds:**
  - `ChunkId` (readonly struct, `(CultRecordKey Field, int Index)`, with value equality).
  - The wear store on `Zone`: `ChunkWear { float Damage; double? BrokenUntil }` keyed by `ChunkId`.
  - `Zone.ChunkExists(ChunkId)`: the index is in range and the chunk is not broken at zone time.
  - `Zone.ChunkRadius(ChunkId)`: `Size` from Cut 1, reading wear.
  - `Zone.Wear(ChunkId, float damage)` → whether it broke. It is the only writer, and Cut 4's Apply is its only
    caller.
  - Lazy respawn: an entry whose `BrokenUntil <= time` reads as absent.
  - `ZonePack` key 6: `List<ChunkWearPack>` (belt key, index, damage, broken-until), nullable. It is written by
    `PackZone` (`Zone.cs:131-142`) with expired entries pruned, and read in the constructor after belts are built
    (`:91-92`).
- **Authority map:**
  - Owner: `Zone` owns chunk wear. `Zone.Wear` is its only writer.
  - Inputs: damage delivered at Apply, zone time, `AsteroidHitpoints` and `AsteroidRespawnTime` (`Settings.cs:29-30`).
  - Outputs: `ChunkExists`, `ChunkRadius`, and the packed wear.
  - Derived state: respawn is a comparison, not a timer.
  - Forbidden writers: any per-tick respawn loop. `MineAsteroid`, `MiningTool` and `ResourceScanner.Execute` are
    deleted.
  - Shared paths: live play, save (`RunSave.Capture` → `PackZone`), continue (the `Zone` constructor), and zone
    re-entry.
  - Deletion line: the deletes above land before the wear store appears.
- **Verification:**
  - tests:
    - `WearAccumulatesAndBreaksAtHitpoints` pins the threshold (strictly `>` as `:314`, or `>=`; say which, and pin it).
    - `ABrokenChunkReturnsAtItsRespawnTime` pins the lazy respawn at the authored curve.
    - `WearSurvivesPackAndUnpack`: a zone round trip through `ZonePack` MessagePack keeps damage and broken-until.
    - `APackOmitsExpiredWear`.
    - `ARunWithoutKeySixLoadsWhole`: nil key 6 means no wear.
  - negative: `rg -n "MiningTool|MineAsteroid|MiningAccumulator|RespawnTimers|MiningDifficulty|AsteroidExists" Assets/Scripts tests tools`
    is empty.
  - catalog: `AetherDb census` before and after is byte-identical. No live item carries tag 26 **(probe)**.
  - operator: none (nothing reachable changes).

---

### Cut 3. A chunk is a target, and is detected by reflected light

- **Repo/branch:** Aetheria, `codex/mining`, after M-Soul.
- **Written for:** Q13 = A with the detection correction, Q14 = A, Q16 = B (recommended).

#### How detection works today (read at `b66ba524`)

1. **Emitters write into `Entity.VisibilitySources`** (`Entity.cs:63`, a `Dictionary<object, float>`). Every
   entry is one source's contribution:
   - `Reflector.cs:46`: `CrossSection × Zone.GetLight(Position.xz)`. This is the rule the operator named.
   - Black-body radiation: `Entity.cs:1269`.
   - Weapons: `InstantWeapon.cs:257`, `ConstantWeapon.cs:166`.
   - Thrusters: `Thruster.cs:110-111`.
   - Sensor pings: `Sensor.cs:117`.
   - `Visibility.cs:44` and `Radiator.cs:121`.

   Sources decay each tick (`Entity.cs:1083-1087`, `VisibilityDecay` 0.5).
2. **`Entity.Visibility`** (`Entity.cs:136`) is the sum of the sources.
3. **Each `Sensor` behaviour on an observer** loops over every entity in the zone, every tick
   (`Sensor.cs:157-189`).
   - It adds a gain into `EntityInfoGathered[target]` (`Entity.cs:64`, a `ReactiveDictionary`).
   - Passive gain is `target.Visibility × Sensitivity × SensitivityCurve(angle/π) × dt / dist` (`:179-184`).
   - Ping gain is `target.Visibility × Sensitivity × PingBoost × dist`, once per ping (`:170-175`).
   - It then decays: `next *= 1 − TargetInfoDecay × dt` (`:186`; `TargetInfoDecay` 0.5 in shipped
     `Settings.asset`).
4. **Crossing `TargetDetectionInfoThreshold`** (0.1) adds the target to or removes it from `VisibleEntities`, via
   the `ObserveReplace` subscriber at `Entity.cs:237-253`.
5. **Fire control reads both.** `PFire` gates on `VisibleEntities.Contains` (`FireControl.cs:173`) and prices
   `PSensor` from the info value (`:179`). The same info also gates the armour and gear reveal thresholds
   (0.5 and 0.8) through `IsRevealed`.
6. **Light** is `Zone.GetLight(float2)` (`Zone.cs:369` @`283ce7dc`). It sums `PowerPulse(d / LightRadius, 8)`
   over every sun within its `LightRadius`. Beyond every sun's radius, light is 0.

#### How a chunk plugs in, without becoming an entity

Two things are needed:
- **A chunk's visibility**, `V(chunk, t)`: `cross-section(chunk) × GetLight(pose(chunk, t))`. This is
  `Reflector.cs:46`'s rule, applied to a pose that is already a pure function of time (Cut 1). No stored source,
  and no decay: a chunk has only the reflected term, because rocks carry no thrusters, weapons or heat
  (black-body emission is out of scope, per the operator's correction).
- **An observer's info on a chunk.** The gain rule is the existing one. Q14 decides how it integrates over time.
  Recommended: the closed-form equilibrium of the per-tick rule, evaluated on demand, with no per-tick state.

**No adapter, and why that is the goal.** The adapter would be a pseudo-entity per chunk, or an `IDetectable`
wrapper, so that `Sensor.Execute`'s loop over `Zone.Entities` sees rocks. That is rejected option C of the Codex
Q2 in a costume:
- It puts every chunk into every sensor's per-tick loop. The map measured 184 ns per observed body per tick,
  which is 149 µs per sensor-bearing ship for the 809-chunk probe zone.
- It puts chunks into `EntityInfoGathered`, whose `ObserveReplace` subscriber builds `VisibleEntities`,
  `VisibleEnemies` and `VisibleFriendlies`. Every consumer of those collections would then have to learn that
  some "entities" are rocks.

The shared owner has to be **the rule, not the container**. The cut extracts the gain terms at `Sensor.cs:170-184`
into one static function, and both paths call it:
- the entity loop, per tick, unchanged in behaviour;
- the chunk query, on demand.

Nothing about a chunk is stored per observer.

#### Deletes first

- The Codex scanner-as-sensor plan: no `ChunkDetectionRange`, no `UnaidedChunkRange`, and no scanner read
  anywhere in the target path.
- The v1 placeholders: unconditional chunk visibility, and PSensor fixed at 1.
- Optional, Self's call: the parked `ResourceScanner` behaviour still runs a per-tick `Update` over three stats no
  code reads (`ResourceScanner.cs:35-58` @`283ce7dc`). Delete the `Update` and the properties, and keep
  `ResourceScannerData` authored.

#### Adds

- **`Sensor.Gain(...)`**, a static function extracted verbatim from `Sensor.cs:170-175` (ping) and `:179-184`
  (passive). Inputs: target visibility, sensitivity, the curve at the angle, distance, dt, and whether this is a
  ping. `Sensor.Execute` calls it. This is a pure extraction.
- **`FieldKindData`**, a new catalog document type (Q11: authored catalog records). Cut 3 introduces it with only
  what detection needs:
  - `Name` (`[CultName]`);
  - `CrossSection` (float): reflectivity per schematic cell of chunk area;
  - `GenerationWeight` (float), for Q16.

  Cut 5 adds the yield entries. Only one kind exists at first, "Asteroid".
- **The belt names its kind.** `AsteroidBeltData` gains a nullable `CultRecordRef<FieldKindData> Kind`, at the next
  free key after `Asteroids` `Key(9)` (`ZoneData.cs:101-105` @`283ce7dc`).
  - `ZoneGenerator` writes it at generation, through one function `FieldKinds.Assign(belt key, catalog)`: a
    `StableHash` pick weighted by `GenerationWeight`.
  - Under Q16 = B, a belt loaded without a kind is assigned by the same function on first load and written back.
    One derivation serves both the generation path and the load path.
- **`Zone.ChunkVisibility(ChunkId)`** = `kind.CrossSection × π(ChunkRadius / SchematicCellSize)² × GetLight(ChunkPose.xy)`.
  - Area in schematic cells keeps the numbers comparable to hull reflectors. The census has Longinus at 500-150
    over 66 cells and Djinni at 2500-500 over 166, so about 2-15 per cell.
  - A worn chunk shrinks and dims. A broken chunk has radius 0, so it is invisible.
- **`Entity.ChunkInfo(ChunkId)`**: the info this observer has on the chunk, from its active `Sensor` behaviours
  through `Sensor.Gain`, integrated per Q14. Visible means `ChunkInfo > TargetDetectionInfoThreshold`.
- **`TargetRef`**: a readonly struct holding `Entity Entity` and `ChunkId? Chunk`, with value equality and `IsNone`.
  It lives beside `ChunkId` (`Zone.cs:527` @`283ce7dc`).
- **`Zone.ChunksNear(float2 position, float range, List<ChunkId> into)`**: the one chunk query.
  - Belts whose annulus cannot reach are skipped.
  - The rest are evaluated through `ChunkPose` (`Zone.cs:285`).
  - Broken chunks are skipped through `ChunkExists` (`:231`).
  - It knows nothing about detection. Callers filter by `ChunkInfo`.
- **`Zone.ChunkVelocity(ChunkId)`**: analytic velocity plus the parent `Orbit.Velocity`.
- **`Entity.SetTarget(TargetRef)`**: the one writer. The player's handlers and the Cut 5 test agent both call it
  (target invariant 3, "set by one writer path that both the player and the AI call"). Today every handler writes
  `Target.Value` directly (`ActionGameManager.cs:377,385,395,403`), so there is no one writer to share.
- **Reach (Q13 = A):** cycling and any programmatic chunk pick use `ChunksNear(position, longest active weapon
  Range.Max)`, then filter to chunks the observer can see.

#### Per-file changes (`b66ba524` unless marked)

- `Sensor.cs:170-184`: call `Sensor.Gain`. Behaviour must stay identical; the whole existing suite is the check.
- `Entity.cs:46`: `Target` becomes `ReactiveProperty<TargetRef>`, written only through `SetTarget`.
- `Entity.cs:201`, `:227`: entity removal clears the slot only when it holds that entity.
- `Entity.cs:229`: `TargetedBy` fires for entities only.
- `Entity.cs:233`: `TargetedByCount` counts entity targets only.
- **`Entity.cs:298-299`:** today it clears a target missing from `EntityInfoGathered`, which would clear every
  chunk target on the next pass. For a chunk, it clears when `ChunkInfo ≤ TargetDetectionInfoThreshold`, the same
  "lost track" rule expressed through the chunk path. A broken chunk has 0 visibility, so it is lost the same way.
- `Entity.cs:317`: `TrySelectTargetItem` is entity-only.
- `Entity.cs:1078`: `TargetRange` reads the chunk position for a chunk.
- `ActionGameManager.cs:365-377` (reticle pick): pick the chunk under the reticle from `ChunksNear` at the
  reticle, if visible.
- `ActionGameManager.cs:381-388` (`TargetNearest`): stays enemies-only. It drives lock.
- `ActionGameManager.cs:390-404` (next and previous): include visible chunks within Q13's reach.
- `ActionGameManager.cs:412-414` (Cycle Target Item): entity-only.
- `ActionGameManager.cs:582`, `:745`, `:769`, `:1046`, `:1224`, `:1239`, `:1266-1267`, `:1355-1373`: `.Entity`
  reads, or the chunk position for the indicator.
- `ActionGameManager.cs:1285`: `UpdateFireControlDebug` takes `TargetRef`.
- `Weapon.cs:101` `StanceAllowsFire`: true for a chunk. `Weapon.cs:110` `ArcAllowsFire`: position through the ref.
- `LockWeapon.cs:86`: a chunk never locks (Q12 = A, ruled).
- `TurretController`, `Combat.cs`, `Minion.cs`, `Ship.cs`, `EntityInstance.cs`, `PropertiesPanel.cs`,
  `FieldDriver.cs`, `ActionBarSlot.cs`: `.Entity` and null checks.

#### Authority map

- **Owners:**
  - `Entity.Target` is the one slot, and `SetTarget` is its one writer.
  - `Sensor.Gain` is the one detection gain rule.
  - `FieldKindData` owns a chunk's reflectivity.
  - `FieldKinds.Assign` owns which kind a belt is.
- **Inputs:** player handlers and the test agent; sensor stats; chunk pose and radius; sun light.
- **Outputs:** the current `TargetRef`; `ChunkVisibility`; `ChunkInfo`.
- **Derived state:**
  - `ChunkVisibility` and `ChunkInfo` are pure functions of time and are never stored.
  - `TargetItem` is entity-only.
  - `TargetRange`.
- **Forbidden writers:**
  - any second target field;
  - any direct `Target.Value =` outside `SetTarget`;
  - any chunk in `Zone.Entities` or `EntityInfoGathered`;
  - any per-tick chunk detection loop;
  - any chunk-specific copy of the gain arithmetic;
  - any unconditional visibility for chunks.
- **Shared paths:** reticle, cycling, the test agent, dock and undock re-activation, and belt generation and load
  (through `FieldKinds.Assign`).
- **Deletion line:** no `ReactiveProperty<Entity> Target` remains; no `Target.Value =` outside `SetTarget`; no
  gain arithmetic outside `Sensor.Gain`.

#### Verification

Yggdrasil, the M0 command with `--filter FullyQualifiedName~MiningCut3`, then the full suite.

| Test | Rule it pins |
|---|---|
| `SensorGainExtractionChangesNothing` | The existing detection tests, and a recorded `EntityInfoGathered` trace over N ticks for a fixed scene, are byte-equal before and after the extraction. |
| `ChunkInfoMatchesAnEntityHeldStill` | Q14's parity. An entity with a `Reflector` of the same cross-section, at the chunk's position and held still, converges under the per-tick loop to the value `ChunkInfo` returns (tolerance from the discrete fixed point). **One rule, two integrators.** |
| `AChunkInDarknessIsNotVisible` | A chunk outside every sun's `LightRadius` has `ChunkVisibility` 0 and cannot be targeted or fired on. |
| `ABrighterOrBiggerChunkIsSeenFurther` | Monotone in cross-section, radius and light. |
| `AWornChunkDims`, `ABrokenChunkIsInvisible` | Visibility follows `ChunkRadius`. |
| `CyclingReachIsTheLongestActiveWeaponRange` | Q13. A visible chunk beyond it is not offered. |
| `AChunkTargetSurvivesTheInfoPassWhileSeen` | The negative for `Entity.cs:298-299`. It fails on the naive retype. |
| `AChunkTargetIsDroppedWhenItGoesDark` | The lost-track rule, through the chunk path. |
| `TargetRefEquality` | Value equality; a chunk never equals an entity. |
| `ChunkTargetIsNeverLocked` | Q12 = A. |
| `FieldKindAssignmentIsOneFunction` | Generation and first-load assignment give the same kind for the same belt key; the kind is written back once. |
| every existing fire-control and sensor test | Pass unchanged. This is the negative that ship detection and targeting did not move. |

Also:
- Negative greps: `git grep -n "ReactiveProperty<Entity> Target\b"` and `git grep -n "Target.Value ="` outside
  `Entity.SetTarget` are empty.
- Stryker over `Sensor.Gain`, `Zone.ChunkVisibility`, `Entity.ChunkInfo`, `ChunksNear` and `TargetRef`.
- Census before and after: `AsteroidBeltData` is a run-store type and is absent from the catalog, and
  `FieldKindData` shows one record once Cut 7's content lands. Until then, Cut 3 ships a test fixture kind and a
  one-record catalog write through `AetherDb`, flagged for operator review.
- Unity compile: Self.
- Operator:
  - target a lit rock with the reticle, and cycle through rocks and ships;
  - fly into a belt far from the sun and confirm its rocks cannot be picked;
  - dock and undock with a rock held.

#### Subtraction estimate

About 0 removed (optionally about 15 for the scanner `Update`). About 140 added:
- `Sensor.Gain` extraction nets about 0;
- `FieldKindData` plus assignment, about 40;
- `ChunkVisibility` and `ChunkInfo`, about 30;
- `TargetRef`, `ChunksNear`, `SetTarget`, about 70.

Plus about 95 mechanical site edits. One new catalog document type (`FieldKindData`), and one nullable
run-store slot (`AsteroidBeltData.Kind`).

### Cut 4. A shot at a chunk goes through FireControl, and blasts cover chunks (re-anchored to 12.4)

- **Repo/branch:** `codex/mining`, after Cut 3.
- **Written for:** Q10 = A, Q12 = A (both ruled).
- **Lands after:** the fire-control no-target fuse ruling (`docs/fire-control-cut.md`, 12.4 status, "a fused
  weapon fired with no target bursts at the shooter's own position", `FireControl.cs:389`). That ruling's fix
  edits the same `target != null` branches this cut retypes (`:367`, `:372-378`, `:389`). If the ruling is still
  open, this cut preserves today's behaviour for the no-target case and says so in its commit.

**Deletes first:** none. `PendingShot.Target` (`:1468`) and `ShotOutcome.Target` (`:1598`) become `TargetRef`.

**Adds:**
- **`Silhouette.Disc(float radiusCells, float precision)`.** The shared integrator is extracted from `Silhouette`
  `:1302-1308` (the `Phi` sum over merged intervals, and `span`). The hull path and the disc path call one
  function. No second `Phi` summation exists.
- **A chunk pass inside `Detonate`** (Q10 = A). After the entity pass (`:1084-1149`):
  - `ChunksNear(worldPlanar, radius + max chunk radius)`.
  - Each covered chunk takes `damage × CircleCircleOverlap(blast, chunk) / (π r_blast²)`, the same normaliser the
    cell shares use (`:1092`).
  - `CircleCircleOverlap` is the closed-form lens area, about 12 lines, beside `CircleSquareOverlap` (`:1153`).
  - Each share goes to `Zone.Wear`.
  - **One area rule:** a blast's disc is divided over whatever it covers, whether cells or chunks. Conservation
    holds across both.
- **`Detonate` gains an optional loot sink**, the detonating shot, so the shooter receives the ore (Q10 = A).
  - A sourceless blast passes null. None exists today, but a mine is one (F12-6). A null-sink blast wears chunks
    and yields nothing.
  - This is the only new parameter. `Detonate` still reads no host (12.4's rule).

**Per-file changes** (`FireControl.cs` @`b66ba524`):
- `PFire` `:161-181`:
  - For a chunk, the visibility gate (`:173`) is `ChunkInfo > TargetDetectionInfoThreshold`, and `info` (`:179`)
    is `ChunkInfo` (Cut 3). **This replaces v1's "no gate, info 1".**
  - `PSensor` prices a chunk exactly as it prices a ship at the same info.
  - A `LockWeapon` is gated out (`:175`, Q12 = A). The arc stays (`:176`).
- `Forecast` `:206`, `HitProbability` `:228`, `Inspect` `:243-283`: the disc silhouette. `Inspect`'s `Visible` and
  `Info` come from the chunk path.
- `PredictedIntercept` `:133`, `TravelDirection` `:307`: position and velocity through the ref. `Bearing` `:316`
  is not called for a chunk, because a disc has no facing.
- `DeviationProbability` `:336-341`: the live chunk position is `Zone.ChunkPose` at `now`.
- `Fire` `:355-418`: freezes the ref.
- `Step` `:439`: `targetGone` includes `!ChunkExists`.
- `CommitProbability` `:506-520`: for a chunk, the disc (no `GetData(Hull)` at `:516`).
- `Commit` `:550-600`: for a chunk hit, no `Lane` (`:563-578`) and no shield block (`:585-592`). `Cell`, `Bearing`
  and `Lateral` stay zero in `MakeOutcome` (`:1028`).
- `Apply` `:651-678`:
  - Null fuse on a chunk: `Zone.Wear(chunk, Damage)` (`Zone.cs:260` @`283ce7dc`).
  - Contact or delayed fuse on a chunk: no `ApplyBlastHit`, which needs a hull (`:747-760`). `Detonate` at the
    chunk's centre at arrival. The pose is a pure function of time, so the point is exact.
  - Proximity: `Detonate` as today, now with the chunk pass.
  - Contact or delayed on a ship: `ApplyBlastHit` as today, and its `Detonate` now also covers nearby chunks.
- Unity presentation: `EntityInstance.cs` (4 target reads) and the four weapon managers aim at the chunk's
  rendered position. Presentation only.

**Authority map:**
- **Owner:** `FireControl`, unchanged. `Detonate` stays the one area owner.
- **New inputs:** chunk pose, velocity, radius and info, through `Zone` and `Entity.ChunkInfo`.
- **When blast damage to chunks is decided:** at detonation. Each covered chunk is judged where it is at arrival,
  which is 12.4's own bystander rule (`ApplyBlastHit`'s comment at `:773-777`: "every bystander is judged against
  where it actually is at arrival"). A direct hit on a chunk is still decided at `Commit`.
- **Forbidden writers:**
  - any chunk hit test outside `Commit`, except `Detonate`'s area pass;
  - any second area-share formula;
  - any second `Phi` integrator;
  - `Detonate` reading a host.
- **Shared paths:** player trigger, the Cut 5 test agent, AI `Activate` (`Combat.cs:121`), and turrets (which
  never hold a chunk). Any blast from any source goes through `Detonate`.
- **Deletion line:** none.

**Verification** (Yggdrasil, `--filter FullyQualifiedName~MiningCut4`, then the full suite):

| Test | Rule it pins |
|---|---|
| `AChunkShotUsesTheSameFactors` | `Accuracy`, `PSpread`, `Sigma` and `PSensor` equal a ship shot's at equal range, precision and info. One fire path. |
| `AnUnseenChunkCannotBeFiredOn` | The gate is the detection rule, not a constant. |
| `DiscMassIsTheClosedForm` | `POnHull = Φ(R/σ) − Φ(−R/σ)`: the shared integrator. |
| `HullSilhouetteUnchangedByTheExtraction` | Every existing `Silhouette` test and the `ProbeC` equivalence still pass. The negative for the extraction. |
| `AChunkThatBreaksMidFlightIsAMiss` | `Step`'s gone rule. |
| `ChunkHitsWearTheChunk`, `AHitOnABrokenChunkChangesNothing` | Wear; re-pins `34994732` through the real path. |
| `ABlastConservesAcrossCellsAndChunks` | The shares delivered to cells plus chunks, plus the uncovered area, equal the damage. Same lattice style as 12.4's 1,944-disc conservation probe. |
| `ABlastBesideAnUntargetedRockWearsIt` | Q10 = A. |
| `CircleCircleOverlapIsTheLens` | Closed form, with the tangent, contained and disjoint cases (12.4's F1 tangent lesson). |
| `LaunchersCannotFireOnAChunk` | Q12 = A. |
| `ChunkDiceAreTheShotsOwn` | The same galaxy and shot id give the same outcome. |

Also: Stryker over the new branches. Operator: shoot a lit rock with each damage type; it shrinks and breaks,
and misses at range look plausible.

**Subtraction estimate:** about 5 removed (the integrator extraction) and about 115 added (the chunk pass and the
lens area add about 35 over v1).

---

### Cut 5. Composition, the loot roll, and the mined lot

- **Repo/branch:** Aetheria, `codex/mining`. Depends on Cut 4 and Q4-Q7. Written for A in each.
- **Deletes first:** `BodyData.Resources` (`ZoneData.cs:53-54`), becoming a retired-key comment (key 4). Its reader
  `MineAsteroid` is already gone. Under Q7=B it stays instead, gets a writer, and the jitter is derived.
- **Adds:**
  - `SimpleCommodityData` key 12 `Dictionary<DamageType, float> Affinity` (nullable; absent means 1). Key 13
    `float? Abundance` (null means not a belt ore).
  - `PlanetSettings.AsteroidYield` (`ExponentialLerp`) and `ChunkCompositionVariance` (float).
    `GameplaySettings.MiningDepthPerPenetration` (float).
  - `Zone.ChunkComposition(ChunkId, Span<...>)`: derived (Q7), from belt-ore abundances times the `StableHash` jitter.
  - The loot roll inside `Commit`, after the hit draw, from the same per-shot generator: first the units draw, then
    one draw per unit for the ore. The result is frozen into `ShotOutcome` as `Loot` (commodity and count pairs,
    presentation-readable).
  - `ItemManager.ExtractedLot(int zone, CultRecordKey belt, CultRecordRef<SimpleCommodityData>)`: find-or-mint,
    using an index derived from the ledger at construction.
  - `Extracted` key 2 `CultRecordRef<BodyData> Body`.
  - `ItemInstance` gains `Lot` at key 11. It moves from `CraftedItemInstance` (`ItemInstance.cs:38`), so the key is
    byte-identical per provenance probe Q4.
- **Per-file changes:**
  - `FireControl.Apply`: after `Zone.Wear`, mint or find the lot and `Source.CargoBays.Any(c => c.TryStore(unit))`.
    Overflow is lost.
  - `Entity.cs:1838`: merge only when `Data` and `Lot` both match. `:1916`: the split copies `Lot`.
  - `SavedGame.cs:110-114`: the roots are every `ItemInstance` with `Lot != 0`.
  - `ItemManager.cs:72` `GetLot` takes `ItemInstance`.
- **Authority map:**
  - Owners: `FireControl.Commit` decides the loot. `ItemManager` mints lots. `EquippedCargoBay` places units.
  - Inputs: frozen damage, type and penetration; the chunk composition at commit; remaining HP at commit.
  - Outputs: `ShotOutcome.Loot`, units in cargo, ledger lots.
  - Derived: composition, and the source→lot index.
  - Forbidden writers: any loot draw outside `Commit`; any mined `SimpleCommodity` without a lot; any shared random
    stream.
  - Shared paths: player and AI shots; save GC; continue (the index is rebuilt).
  - Deletion line: `BodyData.Resources` retired before the composition function is added.
- **Verification:**
  - tests:
    - `RightDamageTypeRaisesTheOresShare` (the Q4 table as a fixture).
    - `PenetrationFlattensTowardRareOre`.
    - `ZeroPenetrationReadsTheRawComposition`.
    - `AChunkYieldsItsAuthoredTotalWhateverBreaksIt` (Invariant 7: the sum over many shots from different weapons).
    - `LootIsDecidedAtCommitAndPerformedAtArrival`.
    - `TheSameShotYieldsTheSameOre`.
    - `MinedUnitsCarryAnExtractedLotNamingZoneAndBelt`.
    - `UnitsFromOneSourceStack`, and `UnitsFromTwoSourcesDoNot`.
    - `ASplitStackKeepsItsLot`.
    - `MinedLotsSurviveSave` (GC roots).
    - `AFullHoldLosesTheOverflow`.
    - `CompositionIsStableAcrossContinue`.
    - Stryker over the formula and the mint.
  - negative: `rg -n "\.Resources\b" Assets/Scripts/ServerShared` returns no `BodyData` reads.
  - catalog: `AetherDb census` shows keys 12 and 13 as `defaulted_missing_slot` until Cut 7 writes them. That is the
    compatible drift fire-control Cut 12 already measured.
  - operator: mine one rock with a Thermal gun and one with a Kinetic gun, and read the cargo.

---

### Cut 6. Deleted (Q9 = C)

AI mining is deferred, and its same-path proof is the test agent in Cut 5. The original Cut 6 text (`MiningState`, the
`Mining` task, the locomotion Cut 4 dependency) is in git history at `22fb4021` and in
`docs/mining-cut-refresh.md`, Cut 6. The campaign that builds AI mining starts from `Zone.CreateAgent`, the `Minion`
transitions, `Entity.SetTarget` and `Combat.cs:121`.

---

### Cut 7. Content (operator review)

- A content cut through an `AetherDb` command (`mining-content [apply]`, dry run by default, one catalog commit;
  precedent: `targeting-catalog`). The operator reviews a sheet before `apply`:
  - The 14 legacy ores missing from live (Crystal, Silicon, Carbon, Nitrogen, Iron, Titanium, Copper, Lead, Ethanol,
    Hydrocarbons, Acid, Oxygen, Carbon Dioxide, Plant Matter), restored with their legacy mass, specific heat,
    conductivity and category **(probe values in `legacy-0414`)**. The 3 missing ammo types are out of scope.
  - `Abundance` and `Affinity` for each belt ore, over the four damage types that exist (Q8).
  - The Drill Bit as a mining weapon (Q1), restored from legacy with its authored ranges.
  - `AsteroidYield`, `ChunkCompositionVariance`, and `MiningDepthPerPenetration` values.
  - The Corrosive and Ionizing gap, recorded (Q8).
- **Verification:** `census` and `dangling` before and after. The loot-table fixture from Cut 5 runs against the live
  catalog, asserting that every belt ore is reachable by at least one live weapon's damage type.

---

## Subtraction ledger (estimate)

| Cut | Removed | Added | Formats and targets |
|---|---|---|---|
| 1 | ~65 (`Zone.cs`), ~12 (`ZoneRenderer.cs`) | ~35 | threading removed; no new target |
| 2 | ~160 (`MiningTool.cs`, `MineAsteroid`, survey `Execute`, dictionaries, `_random`, `MiningDifficulty`) | ~70 | union 26 retired; `ZonePack` key 6 added |
| 3 | ~0 | ~80, plus ~60 mechanical site edits | `Target` retyped |
| 4 | ~0 | ~70 | `PendingShot`/`ShotOutcome` target retyped |
| 5 | 2 (`BodyData.Resources`) | ~150 | `SimpleCommodityData` keys 12 and 13; `Extracted` key 2; `ItemInstance.Lot` moves to the base |
| 6 | deleted (Q9 = C) | 0 | none |
| 7 | 0 | content only | catalog records |

Net code is roughly flat against about 240 lines deleted. Every addition buys a named capability: targetable chunks,
the loot roll, provenance, AI mining.

## Build budget

Headless: `Aetheria.Shared` (netstandard2.1), `tests/Aetheria.Shared.Tests` (net10.0), and `tools/AetherDb`
(net10.0), on the Windows workstation. Nothing ships from them. Both CultLib roots are passed explicitly. Unity
compiles are Self's or the operator's, on the main tree, never batchmode while the editor holds the project. No new
package, assembly or executable target.
