# Mining: Target and Cut Map

Date: 2026-09-25

Status: target and cut map, from an Imagination pass. Nothing here has landed. The first half is the
**target** (the ends). The second half is the **cut map** (the means). When the campaign closes, split the target
out as `docs/mining-target.md` and keep this file as history.

Anchors are against `codex/fire-control-12` HEAD `8bd25f6f`, read from a clean worktree of that commit. Claims
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

**Scope: mining only.** Fire control is mid-rewrite on this branch (Cut 12.3 and 12.4 of
`docs/fire-control-cut.md`, being worked in the main tree by other agents). Cuts 3 and 4 below edit `FireControl.cs`
and `Entity.cs` and must land after fire-control Cut 12 closes, or rebase onto it. Cut 1 and Cut 2 touch no
fire-control code. §Overlap names every shared line.

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
- Refining, crafting from ore, prices for ore (every live simple commodity has `Price 0` **(probe)**).
- AI cargo offload at a home station (the 2020 controller's `GoHome`). An AI miner with full cargo stops mining.
- Electronic-warfare clutter ("a ship ... can float in a debris field or asteroid belt and pass as junk",
  `docs/three-gates-scope.md:155-157`). This is parked there. Q2's recommendation keeps the door open, because a
  chunk enters through the shared detection gate.
- Orbits beyond belts. `Zone.Update` still steps the planet orbits (`:156-161`), which cost about 31 us/tick in the
  probe zone. That is cheap and out of scope.

---

## 0b. Identity, lifecycle, authority

| Kind | What names it | What happens to it over time | Who decides |
|---|---|---|---|
| **Chunk** | `ChunkId = (CultRecordKey Belt, int Index)`. `Belt` is the `AsteroidBeltData` record key in the run store; `Index` indexes its `Asteroids` array. Stable across save/continue, because `RunSave.Commit` writes the run store's whole view, bodies included (`SavedGame.cs:97-99`), and nothing rewrites `Asteroids` after generation. | Born at zone generation (`ZoneGenerator.cs:141-153`). Lives for the run. Removed with every `BodyData` by `RunSave.Clear` (`SavedGame.cs:124`). | `ZoneGenerator` writes `Asteroids` once. Nothing else may write it. |
| **Chunk pose** (position, rotation, velocity) | Derived: `(ChunkId, zone time)`. | Never stored. Evaluated on demand. | One function on `AsteroidBelt` (Cut 1). Forbidden writers: any stored pose array, any per-tick loop. |
| **Chunk wear** (damage taken, broken-until time) | Keyed by `ChunkId` inside the owning `Zone`. | Damage accumulates at Apply. When it reaches the chunk's hitpoints, the chunk breaks with `RespawnAt = zone time + AsteroidRespawnTime(size)`. At `time >= RespawnAt` it is whole again, read lazily with no timer. Persisted in `ZonePack` (new nullable key 6) with the zone's own `Time`, and pruned of expired entries at pack. | `FireControl.Apply` through one `Zone` method is the only writer. `Zone` owns the storage. |
| **Chunk composition** | Derived: `(belt identity, ChunkId, catalog ore table)`. | Never stored (recommended, Q7). A retune of the catalog retunes every belt. | The catalog author, through per-ore data on `SimpleCommodityData`. The derivation function is the only reader of that data for belts. |
| **Ore affinity and abundance** | Per ore: new nullable fields on `SimpleCommodityData` (keys 12 and 13; keys 6, 7, 8 and 11 are retired and must not be reused, `ItemData.cs:304`). | Authored in Studio. Changes with the catalog. | Catalog author. |
| **Global loot tunables** | `GameplaySettings` / `PlanetSettings` fields (Cut 5). | Authored. Today they live in `Settings.asset`. They move with the settings-globals campaign (`docs/settings-globals-cut.md`, mapped, not landed). | Catalog author. |
| **Mined lot** | `LotId` in the run's `ProvenanceLedger`. `Lot { Design = the commodity, Origin = Extracted { Zone, Commodity, Body }, Quality, Roles = null }`. | Minted at the first extraction from a (zone index, belt, commodity) source (Q6). Reused for later units from that source. Collected by `Reachable` when no instance reaches it. A later extraction after collection mints a fresh lot. | `ItemManager.ExtractedLot(zone, belt, commodity)` is the only writer. Its source→lot index is derived at load, not persisted. |
| **Mined unit** | A `SimpleCommodity` instance, `Data` = commodity, `Lot` = the source lot (`Lot` moves to the `ItemInstance` base, key 11). | Stored in cargo. Stacks merge only within the same `Data` and `Lot`. A split copies `Lot`. | `EquippedCargoBay` owns placement. The mint owns `Lot`. |
| **Current target** | `Entity.Target` (runtime; not packed, `EntitySerializer.cs` has no target field **(read)**). | Set by targeting inputs or AI state. Cleared by the existing entity-removal paths. A broken chunk stays held and prices 0 until retargeted (Cut 3). | Player: the `ActionGameManager` targeting handlers. AI: the Minion states. Both write the one slot. |
| **Mining shot** | `ShotId` (`Zone.NextShotId`), as every shot. | Fire → Commit → arrival. Never serialized (`FireControl.cs:852-855`). | `FireControl`. |

No cell is empty. The cells marked "recommended" depend on the rulings below and are rewritten if a ruling goes the
other way.

---

## Operator questions, in the order they block cuts

Self delivers these one at a time (memory: pace operator questions). Cut 1 needs no ruling. The CPU-versus-GPU
belt renderer is a stated default: CPU, one orbit function, so no parity test is needed. Reopen it on request.

**Q1 (blocks Cut 2). What becomes of `MiningTool`, `ResourceScanner` and the Drill Bit?**
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
  split Cut 3 of fire control removed. `ResourceScanner` is Q2's business.

**Q2 (blocks Cut 3). How does a chunk become a target?** This is the architectural fork. The options below were
costed against the live code:

- **A. One slot, two kinds.** `Entity.Target` becomes `ReactiveProperty<TargetRef>`, where `TargetRef` is a small
  readonly value that is either an `Entity` or a `ChunkId`, with value equality. Readers that need a ship read
  `Target.Value.Entity` (null for a chunk). FireControl's factor functions take a `TargetRef`, with the chunk
  branch at each target-shaped read. Blast radius **(read)**: about 60 `Target` reads in 12 files
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
- **Visibility.** A chunk passes the visibility gate within the shooter's chunk detection range, and its PSensor is
  1, because rocks do not hide. Recommend that range be the equipped `ResourceScanner`'s `Range`, with a
  `GameplaySettings.UnaidedChunkRange` fallback. That is the `TargetingSystem`/`UnaidedTracking` precedent
  (`FireControl.cs:121-125`), and it gives `ResourceScanner` a job: it is to chunks what the targeting system is to
  ships. Its dead survey half (`ResourceScanner.cs:71-107`, `// TODO: Implement Scanning!`) is deleted.
- **Stance.** `StanceAllowsFire` (`Weapon.cs:101`) allows fire on a chunk.
- **Lock weapons.** Launchers cannot lock a chunk. `LockWeapon.cs:86` requires `IsHostileTo`, and rocks are not
  hostile, so guided and lock launchers (GT 3K, LRMM72, SRMM72, pswarm, scorched void policy) cannot mine. Rule it
  if that is wrong.
- **Blasts (fire-control 12.4).** A blast shot targeting a chunk delivers `damage × area(blast disc ∩ chunk disc) /
  area(blast disc)`, which is 12.4's own area rule with the chunk disc as the only cell. Untargeted chunks near a
  blast are unaffected, so there is no per-blast scan of belts.

**Q3 (blocks Cut 2). Do chunks respawn, and does wear survive save/continue?**
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
  the raw composition. It is a benefit, and GT 3K, plight, pswarm and scorched void policy author it **(probe)**.
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

**Q7 (blocks Cut 5). Where does composition live?**
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
Kinetic (10), Optical (3), Electric (3) and Thermal (3) **(probe)**. `legacy-0305` and `legacy-0414` use the same
four types **(probe)**. An ore whose affinity peaks at Corrosive or Ionizing is unreachable.
- A: Author affinities over the four types that exist, and add Corrosive and Ionizing weapons when content wants
  them.
- B: Author new Corrosive and Ionizing weapons in Cut 7.
- **Recommended: A**, with the gap recorded in Cut 7's review sheet.

**Q9 (blocks Cut 6). Which AI ships mine?** Nothing assigns a `Mining` task today. `Zone.CreateAgent`
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
  Compounds: Methane, Water. Organics: Algae, Animal Products, Bacteria, Fungi. Ammo: Ammo. Every one is priced 0.
- **Legacy (30, identical in `legacy-0305` and `legacy-0414`):** the 13 live ones, plus Crystal, Silicon, Carbon,
  Nitrogen (Minerals); Iron, Titanium, Copper, Lead (Metals); Ethanol, Hydrocarbons, Acid, Oxygen, Carbon Dioxide
  (Compounds); Plant Matter (Organics); and Autocannon, Charged Shotgun, Flak Cannon and Auto Shotgun ammo. That is
  17 missing from live, 14 of them plausible ores.
- **Resource-distribution data does not survive.** The 2020 fields (`ResourceDensity` map layers, `Minimum`,
  `Maximum`, `Exponent`, `Floor`; `17673cd2`) are the retired keys 6, 7, 8 and 11. No values remain in the deleted
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
- **Locomotion (`codex/locomotion`).** Its Cut 4 rewrites `MoveTo` and the heading planner. Cut 6 here drives the
  approach through whatever `MoveToState` is live and adds no motion code, so it waits for, or rebases onto,
  locomotion Cut 4.
- **Settings globals (`docs/settings-globals-cut.md`).** Cut 5's new tunables join `GameplaySettings` and
  `PlanetSettings` wherever those live when it lands.
- The uncommitted `AetheriaInput.cs` change in the main tree ("Cycle Target Item") is fire-control work. Cut 3's
  targeting handlers sit beside it in `ActionGameManager.cs:372-404`.

---

# Part III: Cut map

## Status header

Status: cut map. Ends are owned by Part I of this document; Part III owns the means.

Rulings (operator): none yet. Q1-Q9 open.

Open: Q1-Q9. Cut 1 needs none of them.

Follow-ups outside this campaign:
- `AsteroidBeltUI.Update` feeds `Quaternion.Euler(90, transform.z, 0)` a rotation in radians where the API takes
  degrees (`ZoneRenderer.cs:666`). It is a presentation defect and is not fixed here.
- The planet orbit step stays per tick (`Zone.cs:156-161`). It is cheap.

## Cut order

| Cut | Nature | Depends on |
|---|---|---|
| 1 | Subtraction: belts evaluated on demand. No threads, no stored poses. | none |
| 2 | Subtraction plus one owner: the dead mining path goes, and chunk identity and wear get one owner | Q1, Q3 |
| 3 | Targetable chunks: the one slot, the one chunk query, the HUD | Q2; fire-control Cut 12 closed |
| 4 | Hit resolution against chunks through FireControl (damage only, no loot) | 3 |
| 5 | Composition, loot roll, provenance, deposit | 4; Q4-Q7 |
| 6 | AI mining on the same path | 5; Q9; locomotion Cut 4 |
| 7 | Content: ores, affinities, abundance, Drill Bit, Resource Scanner (operator review) | 5; Q1, Q8 |

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
  - `ChunkId` (readonly struct, `(CultRecordKey Belt, int Index)`, with value equality).
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

### Cut 3. A chunk is a target

- **Repo/branch:** Aetheria, `codex/mining`, rebased onto the fire-control Cut 12 close. Depends on Cut 2 and Q2.
  Written for Q2=A.
- **Deletes first:** none of substance. Reshapes follow.
- **Adds:**
  - `TargetRef` (readonly struct: `Entity Entity`, `ChunkId? Chunk`, value equality, `IsNone`).
  - `Zone.ChunksNear(float2 position, float range, List<ChunkId> into)`. It is the one chunk query: it skips belts
    whose annulus (`GetOrbitPosition(parent)`, `Radius`) cannot reach, evaluates the rest on demand, and skips broken
    chunks. The player's reticle pick, target cycling and the AI all call it.
  - `Zone.ChunkVelocity(ChunkId)`: analytic, `2πD/T · (−sin θ, cos θ)` plus the parent `Orbit.Velocity` (`Zone.cs:160`).
  - `FireControl.ChunkDetectionRange(Entity)`: the equipped `ResourceScanner`'s `Range` when active, else
    `GameplaySettings.UnaidedChunkRange` (the `Tracking` shape, `FireControl.cs:121-125`).
- **Per-file changes** (anchors at `8bd25f6f`; re-anchor after the Cut 12 rebase):
  - `Entity.cs:46`: `Target` becomes `ReactiveProperty<TargetRef>`. `:201`, `:227`, `:231-234`, `:298-299` compare
    and clear through `.Entity`. `:317` and `:328` (`TrySelectTargetItem`, `ResolvedTargetItem`) are entity-only,
    via `.Entity`. `:1060` `TargetRange` reads the chunk position for a chunk.
  - `ActionGameManager.cs:372-404`: the reticle, next and previous handlers include chunks from `Zone.ChunksNear`
    within `ChunkDetectionRange`. `TargetNearest` stays enemies-only (`:380-388`, its own comment: it drives weapon
    lock). The HUD at `:1266-1275` shows the chunk's size, and its wear as hitpoints, for a chunk target. The
    visibility fills are ship-only. `UpdateFireControlDebug(Entity)` (`:1284`) takes `TargetRef`.
  - `Weapon.cs:101` `StanceAllowsFire`: a chunk is allowed. `:110-117` `ArcAllowsFire`: reads the target position
    through the ref.
  - `LockWeapon.cs:80-98`, `TurretController.cs:60-100`, `Combat.cs:28-126`, `Minion.cs:14,18,21`, `Ship.cs:84`,
    `EntityInstance.cs:207,236,405`: `.Entity` and null-checks. Turrets and combat never hold a chunk (they write
    only enemies).
- **Authority map:**
  - Owner: `Entity.Target`, the one slot.
  - Inputs: the player's targeting handlers and the AI states.
  - Outputs: the current `TargetRef`.
  - Derived state: `TargetItem` (ship-only, nulled on any change as today, `Entity.cs:286`); `TargetRange`.
  - Forbidden writers: any second target field; any chunk selection that bypasses `Zone.ChunksNear`.
  - Shared paths: player reticle, cycling, AI (Cut 6), and dock/undock reactivation (`Entity.cs:298-299`).
  - Deletion line: no `ReactiveProperty<Entity> Target` remains.
- **Verification:**
  - tests:
    - `ReticleAndCycleOfferChunksWithinDetectionRange`, headless through the same query the handler calls.
    - `AScannerExtendsChunkDetection` (and `UnaidedFallbackWithoutOne`).
    - `ABrokenChunkIsNotOffered`.
    - `TargetingAChunkClearsTheAimedItem`.
    - `TargetRefEquality`: the same chunk held twice is equal.
    - Every existing fire-control test still passes unchanged. That is the negative check that ship targeting did
      not move.
  - negative: `rg -n "ReactiveProperty<Entity> Target\b"` is empty.
  - operator: target a rock with the reticle, cycle through rocks and ships, and dock and undock with a rock
    targeted.

---

### Cut 4. A shot at a chunk goes through FireControl

- **Repo/branch:** Aetheria, `codex/mining`. Depends on Cut 3 and fire-control 12.4.
- **Deletes first:** none. Reshape `PendingShot.Target` and `ShotOutcome.Target` (`FireControl.cs:860,997`) to `TargetRef`.
- **Adds:** `Silhouette.Disc(float radiusCells, float precision)`, which returns one interval `[-R, R)`, `A = 0`,
  and `Span = 2R`. The hull path and the disc path share one integrator, extracted from `:710-718`. `ShotOutcome`
  gains `float3 ImpactPoint` for presentation: the chunk position at commit.
- **Per-file changes** (re-anchor after 12.4):
  - `PFire` `:161-181`: for a chunk, the visibility gate is range ≤ `ChunkDetectionRange`, `info = 1`, and a
    `LockWeapon` is gated out.
  - `Forecast`, `HitProbability`, `Inspect` (`:194-265`): for a chunk, the silhouette is the disc.
  - `PredictedIntercept` `:133-139` and `TravelDirection`: position and velocity through the ref.
  - `Fire` `:329-393`: freezes the ref.
  - `Step` `:398-443`: `targetGone` includes `!ChunkExists`.
  - `CommitProbability` and `Commit` (`:476-559`): for a chunk, the disc and no shield.
  - `Apply` `:564-582`: a chunk hit calls `Zone.Wear(chunk, Damage)`.
  - Unity presentation: `EntityInstance.cs:207,236` and the effect managers aim at the chunk's rendered position when
    the target is a chunk. That is presentation only.
- **Authority map:**
  - Owner: `FireControl`, unchanged.
  - New inputs: the chunk pose, velocity and radius through `Zone`.
  - Forbidden: any hit test against chunk geometry outside `Commit`, and any read of a chunk pose in `Apply` (R4).
  - Shared: player trigger, AI `Activate`, turrets (which cannot hold chunks).
  - Deletion line: none.
- **Verification:**
  - tests:
    - `AChunkShotUsesTheSameFactors`: Accuracy, PSpread and Sigma equal a ship shot's at equal range and precision.
    - `DiscMassIsTheClosedForm`: `POnHull = Φ(R/σ) − Φ(−R/σ)`.
    - `ABiggerRockIsEasierToHit`.
    - `AChunkThatBreaksMidFlightIsAMiss`.
    - `ChunkHitsWearTheChunk`.
    - `LaunchersCannotFireOnAChunk`.
    - `ChunkDiceAreTheShotsOwn`: the same galaxy and shot give the same outcome.
    - Stryker over the new branches.
  - negative: no `Target.Value.Position`-shaped chunk read outside `Zone.Chunk*` (grep).
  - operator: shoot a rock with each damage type. It shrinks and breaks, and misses are plausible at range.

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

### Cut 6. The AI mines on the same path

- **Repo/branch:** Aetheria, `codex/mining`, rebased onto locomotion Cut 4. Depends on Cut 5 and Q9.
- **Deletes first:** `Agents/Tasks/Mining.cs` is reshaped. It is not persisted, so its MessagePack attributes go.
  It keeps `Belt` (`CultRecordKey`) and a wanted-ore reference.
- **Adds:** `MiningState` in `Minion` (`Minion.cs:7-22`), entered when `Task is Mining` and no enemy is targeted.
  It picks a chunk via `Zone.ChunksNear` and writes `Ship.Target` (the same slot the player writes). It approaches
  through the live `MoveToState` toward `ChunkPosition`. It selects the weapon group whose damage type has the
  highest `Affinity` for the wanted ore, gates on `FireControl.HitProbability ≥ AgentMinHitProbability`, and calls
  `weapon.Activate()`, which is exactly `Combat.cs:117-124`. It retargets when `!ChunkExists`, and stops when no
  cargo bay can take a unit. The combat transition keeps priority: a visible enemy preempts mining
  (`Minion.cs:14,18`). `Zone.CreateAgent` (`Zone.cs:122-129`) assigns `Mining` by Q9's rule.
- **Authority map:**
  - Owner: `MiningState` decides what to shoot.
  - Forbidden: any AI-only extraction call. The only way ore enters an AI hold is `FireControl.Apply`.
  - Shared paths: the same target slot, `HitProbability`, `Activate`, `Commit` and `Apply` as the player.
- **Verification:**
  - tests:
    - `AnAgentMinesThroughFireControl`: a headless zone, an agent with a Mining task, N seconds of ticks, then ore in
      its hold whose lots are `Extracted` from that belt, and every unit traceable to a `ShotOutcome`.
    - `AnEnemyPreemptsMining`.
    - `TheAgentPicksTheAffineGroup`.
  - operator: watch an NPC mine a belt.

---

### Cut 7. Content (operator review)

- A content cut through an `AetherDb` command (`mining-content [apply]`, dry run by default, one catalog commit;
  precedent: `targeting-catalog`). The operator reviews a sheet before `apply`:
  - The 14 legacy ores missing from live (Crystal, Silicon, Carbon, Nitrogen, Iron, Titanium, Copper, Lead, Ethanol,
    Hydrocarbons, Acid, Oxygen, Carbon Dioxide, Plant Matter), restored with their legacy mass, specific heat,
    conductivity and category **(probe values in `legacy-0414`)**. The 3 missing ammo types are out of scope.
  - `Abundance` and `Affinity` for each belt ore, over the four damage types that exist (Q8).
  - The Drill Bit as a mining weapon (Q1), and the Resource Scanner as the chunk sensor (Q2), restored from legacy
    with their authored ranges.
  - `AsteroidYield`, `ChunkCompositionVariance`, `MiningDepthPerPenetration` and `UnaidedChunkRange` values.
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
| 6 | ~15 (`Mining.cs` attributes) | ~90 | none |
| 7 | 0 | content only | catalog records |

Net code is roughly flat against about 240 lines deleted. Every addition buys a named capability: targetable chunks,
the loot roll, provenance, AI mining.

## Build budget

Headless: `Aetheria.Shared` (netstandard2.1), `tests/Aetheria.Shared.Tests` (net10.0), and `tools/AetherDb`
(net10.0), on the Windows workstation. Nothing ships from them. Both CultLib roots are passed explicitly. Unity
compiles are Self's or the operator's, on the main tree, never batchmode while the editor holds the project. No new
package, assembly or executable target.
