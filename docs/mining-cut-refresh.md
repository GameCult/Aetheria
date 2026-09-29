# Aetheria Codex assimilation: refreshed cut maps (mining, moddable ships)

Date 2026-09-29. Imagination output for Self. It re-anchors two Codex lanes to the Body as it will stand after
`codex/fire-control-12` merges to `master`. The Codex maps are raw material, not authority. Where this file and
a Codex map disagree, this file is the proposal and the Codex map is the input.

## How this was established

- **Branch tips read, 2026-09-29, after `git fetch`.**
  - `origin/master` `9b85211f`
  - `origin/codex/fire-control-12` `b66ba524`
  - `origin/codex/mining` `283ce7dc`
  - `origin/codex/moddable-ships` `375d6bd4`
  - Nothing in `F:\Projects\Aetheria`'s working tree was read or switched. Every read is `git show` or `git grep`
    against a SHA.
- **The post-fire-control Body is a synthetic merge.** `git merge-tree --write-tree origin/master
  origin/codex/fire-control-12` is clean, tree `91576243`. I wrapped it in an unreferenced local commit,
  `8a2db490` (no ref, no push to origin), so it can be built. The fire-control checklist (`docs/merge-to-master-checklist.md:7,107`
  on `b66ba524`) predicts the same clean merge.
- **Anchors.** Each `file:line` names a real SHA, never the synthetic one:
  - `b66ba524` for files only fire control touched;
  - `283ce7dc` for files the mining branch touched;
  - `375d6bd4` for files the ships branch touched.

  The three file sets are disjoint. I checked with `git diff --name-only` over each branch's own range, and none
  of the three pairs shares a file. So each anchored line is byte-identical in the merged tree.
- **Tests ran on Yggdrasil, against synthetic merges** (section 0). CultLib `45c2f40` and CultMath `6d5e209` are
  the pins in `Directory.Build.props`. The two LFS files the tests read (`GameData/Aetheria.cc`,
  `Assets/Resources/Settings.asset`) were staged beside the job, because the verify mirror carries no LFS.
- **Catalog facts** come from a probe that opens the merged tree's `GameData/Aetheria.cc` through
  `AetheriaStores.Open` (section 0).

## 0. Probe results

**Test suite on the synthetic merges**, Yggdrasil, `dotnet test tests/Aetheria.Shared.Tests`, CultLib `45c2f40`,
CultMath `6d5e209`, the `dotnet` image:

| Tree | Commit (local, unreferenced) | Result |
|---|---|---|
| master + fire-control-12 + mining | `396d19e5` | **379 passed, 0 failed** |
| master + fire-control-12 + ships | `0036204f` | **364 passed, 0 failed** |
| master + fire-control-12 + mining + ships | `474c35c3` | **381 passed, 0 failed** |

Fire control closed 12.4 at 362 tests. 379 = 362 + 17 mining tests, 364 = 362 + 2 ship tests, and 381 is both
together. Nothing on either branch breaks a fire-control test, and nothing in fire control breaks a branch test.
This proves the headless simulation only. No Unity compile ran.

**Catalog probe** (`scratchpad/probe/Program.cs`: it opens `GameData/Aetheria.cc` through `AetheriaStores.Open`
in the merged tree; the catalog oid `2aa79c0a` is the same in all three merges and at `b66ba524`):

- 124 items: 32 `GearData`, 18 `WeaponItemData`, 13 `SimpleCommodityData`, 5 `HullData`, 51 compound
  commodities, 4 cargo bays, 1 docking bay.
- **Weapon damage types:** Kinetic 9, Electric 3, Optical 3, Thermal 3. There is none of Corrosive or Ionizing,
  though the `DamageType` enum has all six. The mining map said Kinetic 10.
- **Penetration > 0:** only Autocannon, LRMM72 and SRMM72, each at 0.25.
- **DamageSpread > 0:** GT 3K 3.25, plight 3.3, pswarm 1.5, scorched void policy 1.5, and SRMM72 0.5. The map
  listed four and missed SRMM72.
- **No weapon has `BlastRadius` or `Fuse`.** Blasts are dormant in shipped content until F12-2 authors them.
- **Simple commodities:** the 13 the map named. Every one is priced 0 **except Ammo, at 1000**. The map's
  "every live simple commodity has Price 0" is off by one.
- **No live item carries a scanner or mining behaviour**, on fire control or on mining merged.
- **Pre-existing, on `b66ba524` as well as on the merges:** four `GearData` records (Refinery, Deep Ore
  Extractor, Assembly Line, Shipyard) hold a **null element** in `Behaviors` (index 0 of 1). It does not come from
  mining's union-26 retirement: the same four are null on fire control alone. This matters to variants C4 (A.6).
- **Shipped hull mount ids are not unique:**

  | Hull | Hardpoints | Distinct `Transform`s | Empty `Transform`s |
  |---|---|---|---|
  | Djinni | 22 | 20 | 3 |
  | Longinus | 13 | 12 | 2 |
  | LonginusX | 10 | 9 | 2 |
  | Turret | 7 | 3 | 5 |
  | Zenith | 5 | 1 | 5 |

  All five hulls use `Prefab`. This matters to ship migration (S5).

## Shared precondition for both campaigns

Both campaigns wait on one event: `codex/fire-control-12` merging into `master` with `--no-ff`, which Self does
after the operator signs off (`docs/merge-to-master-checklist.md:105-113` on `b66ba524`). Until then, neither
campaign has a base to merge onto except the fire-control branch itself. After it:

```
git switch codex/mining          && git merge --no-ff master   # in a worktree, never the operator's tree
git switch codex/moddable-ships  && git merge --no-ff master
```

**Merge, do not rebase.** Both maps and their Soul receipts cite landed SHAs: mining cites 11 of its 13 commits,
and the ships doc cites its commits' results. A rebase rewrites every one of those SHAs, which strands the maps'
evidence. The merge has no conflicts (below), so it costs one merge commit per branch.

---

# Part A. Mining

## A.1 Status header (verified against git)

Branch `codex/mining`, tip `283ce7dc`: 13 commits on `8bd25f6f`. `8bd25f6f` is an ancestor of fire control,
which has since moved **76 commits** to `b66ba524`.

**What truly landed:**

| Cut | Commits | What |
|---|---|---|
| map | `e729e6d6`, `670831c8`, `007771fe`, `e4d38f16`, `a9538b20`, `283ce7dc` | target, cut map, Q1-Q3 rulings, status |
| 1 | `22a54ccb`; fix `d9c887c1` | belts evaluated on demand; `Task.Run` belt threading deleted |
| 2 | `3b9f0f1d`, `c924bae0`; fixes `daa64560`, `34994732`, `93bfdebd` | `MiningTool`, `MineAsteroid` and the survey `Execute` deleted; `ChunkId`, `Zone` wear store, `ZonePack` key 6 |

Production delta under `Assets/Scripts` (`git diff --numstat 8bd25f6f 283ce7dc`): **+181 / −250**. Tests add
**+681** (`MiningCut1Tests.cs` 240, `MiningCut2Tests.cs` 441).

**Test counts.** The map says 274 tests after Cut 2 (`:493`), counted on its own `8bd25f6f` base. On the merged
tree the suite is 379, of which 17 are mining's. Every one passes (section 0).

**Corrections to `docs/mining-cut.md` at `283ce7dc`:**

1. **`:5` "Nothing here has landed" is false.** Cuts 1 and 2 landed at the SHAs above, and the map's own status
   block (`:487-500`) says so. Replace it with a status line naming both cuts and the base.
2. **`:9` anchors at `8bd25f6f` are stale for every fire-control file.** At `b66ba524`, `FireControl.cs` is 1,614
   lines. `Splash` is gone. `Apply` is a switch over three functions (`ApplyDirectHit` `:680`, `ApplyBlastHit`
   `:740`, `Detonate` `:1074`). `Silhouette` is at `:1278` and `PendingShot` at `:1464`. Every Cut 3 and 4 anchor
   in the map (`:396-412`, `:669-721`) is re-anchored in A.3 below.
3. **`:24-27` "Fire control is mid-rewrite" is stale.** Cut 12.4 closed 2026-09-29 (`c50029d5`), and 362 tests
   passed at that close (`docs/fire-control-cut.md` 12.4 status on `b66ba524`).
4. **Cut 3's spec contradicts the Q2 ruling recorded in the same file.**
   - Q2's EW deferral (`:176-185`) says a chunk passes the visibility gate unconditionally with PSensor 1, that
     targeting is an on-demand field query, and that `ResourceScanner` parks.
   - Cut 3 (`:667-668`, `:693`) still specifies `FireControl.ChunkDetectionRange` from the scanner's `Range`, an
     `UnaidedChunkRange` fallback, and a test `AScannerExtendsChunkDetection`.
   - Cut 7 (`:834-836`) still restores "the Resource Scanner as the chunk sensor" and authors `UnaidedChunkRange`.
   - All of that is superseded and is rewritten in A.3.
5. **Q2's blast consequence (`:221-223`) was written against `Splash`.** Cut 12.4 replaced it with
   `Detonate(Zone, float2 worldPlanar, float radius, float damage, DamageType)` (`FireControl.cs:1074` @`b66ba524`).
   - `Detonate` takes no shooter.
   - It walks `zone.Entities` only (`:1085`).
   - So a proximity burst beside a rock does nothing to the rock today, and nothing could route its loot.
   - This is new question Q10.
6. **`:135`, and the `SimpleCommodityData` comment it copies, are wrong about the retired keys.** Both claim keys
   6, 7 and 8 are retired. They are live: `ItemData` declares them for every item, `SpecificHeat` `Key(6)`,
   `Conductivity` `Key(7)` and `Price` `Key(8)` (`ItemData.cs:291-298` @`b66ba524`), and `SimpleCommodityData`
   inherits them. Only key 11 is retired on `SimpleCommodityData`. Keys 12 and 13 are still free. The
   comment at `ItemData.cs:304` should say "key 11".
7. **Q7's recommendation (`:298-310`) predates the Q2 ruling.** Q2 (`:171-174`) says composition and loot tables
   are **per field kind**, with debris rolling salvage through the same roll. Q7 recommends per-ore `Abundance` on
   `SimpleCommodityData`, which has one table for every field kind. The two are unreconciled. Q7 is restated
   below, behind a new question about what a field kind is.
8. **The `Target` blast-radius count (`:193-196`, "about 60 reads in 12 files") has grown.**
   - At `b66ba524`, `git grep` finds 28 target reads in `ActionGameManager.cs` and 22 in `FireControl.cs`, where
     the map counted 20 and 11.
   - It finds 11 in `Entity.cs`, plus sites in the Unity weapon managers (`GuidedProjectileManager`,
     `LightningGunManager`, `ConstantParticleWeaponManager`, `GuidedProjectile`) that the map does not list.
9. **The catalog changed after the mining probe.**
   - `4f218e85` and `dd437580` (locomotion Cut 1) landed Longinus, Djinni and five thrusters in `Aetheria.cc`
     after `8bd25f6f`.
   - Catalog oid at `8bd25f6f`: `1862ddc4`. At `b66ba524`, and in all three merged trees: `2aa79c0a`.
   - The census in section 0 replaces the map's weapon and commodity counts.
   - Kinetic is 9 weapons, not 10 (`:313`).
   - Ammo is priced 1000 (`:117`, `:421`).
   - SRMM72 also authors `DamageSpread` (`:270`).
   - The Q8 conclusion stands: no Corrosive or Ionizing weapon exists.
10. **Still owed from Cut 1 (`:508-510`):** a Unity compile of the `ZoneRenderer`/`AsteroidBeltUI` change, an
    isolated Stryker rerun, and the operator's fly-through. Nothing after `d9c887c1` records any of them.
11. **Still owed from Cut 2 (`:493`):** Soul has not verified the two-commit fix (`34994732`, `93bfdebd`). The
    map folds that check into Cut 3's Soul pass. Keep it named, so that it is not lost when Cut 3 is rewritten.

## A.2 Merge path onto the fire-control-12 lineage

| Probe | Result |
|---|---|
| `git merge-tree --write-tree origin/codex/fire-control-12 origin/codex/mining` | clean, tree `b7feb776` |
| `git merge-tree --write-tree 8a2db490 origin/codex/mining` (master+fc12) | clean, tree `7280568c` |
| mining merged, then ships merged on top | clean, tree `a7410174` |
| files touched by both mining (`8bd25f6f..283ce7dc`) and fire control (`8bd25f6f..b66ba524`) | none |

The merged tree passes the full headless suite: 379/379, and 381/381 with ships (section 0).

**No textual conflicts, and no shared file.** The semantic seam is not textual: mining Cut 3 onward edits the
code fire control rewrote after the mining map was written. That is handled by re-anchoring, not by merging.

## A.3 Ordered cuts

The Codex numbering is kept so the map's history still reads, and Soul fix slots are added. Cut order:

| Cut | Nature | Blocked by |
|---|---|---|
| M0 | Placement: merge master, correct the map | fire-control-12 on master |
| M-Soul | Fix batch from the parallel Soul pass over Cuts 1-2 (content unknown here) | Soul's report |
| 3 | A chunk is a target (rewritten for the EW deferral) | M0; Q12; Q13 |
| 4 | A shot at a chunk goes through FireControl (re-anchored to 12.4) | 3; Q10; the fire-control no-target fuse ruling |
| 5 | Composition, loot, the mined lot | 4; Q4, Q5, Q6, Q7, Q11 |
| 6 | The AI mines on the same path | 5; Q9; locomotion Cut 4 |
| 7 | Content | 5; Q1, Q8; variants C4 for any family authored as variants |

### M0. Placement and map correction

- **Repo/branch:** Aetheria, `codex/mining`, in a worktree. Do not use the operator's tree.
- **Deletes first:** none in code.
- **Keeps/moves/adds:**
  - `git merge --no-ff master` once fire-control-12 is on master.
  - In `docs/mining-cut.md`: apply corrections 1-11 of A.1.
  - Replace Cut 3 and Cut 4 with the versions below.
  - Strike `UnaidedChunkRange` from Cut 5's and Cut 7's tunables, and "Resource Scanner as the chunk sensor" from
    Cut 7.
  - Mark the `:396-412` table as history, anchored at `8bd25f6f`.
- **Verification:**
  - Yggdrasil, the same job that section 0 ran:
    `ygg-verify.sh <aetheria clone> <merge sha> dotnet '<clone CultLib 45c2f40 to /CultLib and 6d5e209 to /CultMath; restore the two LFS files; dotnet test tests/Aetheria.Shared.Tests -p:CultLibRoot=/CultLib -p:CultMathRoot=/CultMath>'`.
    Rule pinned: fire control and mining compose without a behaviour change on either side. Every pre-existing
    test passes unchanged.
  - The verify mirror has no LFS (the push from `F:\Projects\Aetheria` fails in the LFS pre-push hook on an
    `ssh:` remote). Push from a `--shared --no-checkout` clone, and mount the two LFS blobs. This is recorded
    so the next Hands does not rediscover it.
  - Unity batchmode compile of the merged tree (Self, in a worktree, never with the operator's editor open on
    the main tree). This is the Cut 1 renderer compile the map still owes.
  - Operator: fly through a belt (the Cut 1 check the map still owes). Rocks orbit smoothly, and the minimap
    and the map screen track them.
- **Subtraction:** docs only.

### M-Soul. Fix slot for the parallel Soul pass

- **Repo/branch:** `codex/mining`, after M0.
- **Content:** whatever the parallel Soul pass finds in Cuts 1-2, including the unverified `34994732` and
  `93bfdebd`. This map does not predict it. Cut 3 does not start until this slot is closed or empty, because
  Cut 3 builds on `Zone.Wear`, `ChunkExists` and `ChunkRadius`.

### Cut 3. A chunk is a target (rewritten for the Q2 ruling)

- **Repo/branch:** Aetheria, `codex/mining`, after M-Soul.
- **Deletes first:**
  - The scanner-as-sensor plan: no `ChunkDetectionRange`, no `UnaidedChunkRange`, no scanner read in the target
    path.
  - Optional, and Self's call on what the ruling's "parks" means: the `ResourceScanner` behaviour still runs a
    per-tick `Update` that evaluates three stats no code reads (`ResourceScanner.cs:35-58` @`283ce7dc`,
    `IAlwaysUpdatedBehavior`). Either keep it as is, or delete the behaviour's `Update` and properties and keep
    `ResourceScannerData` authored. No live item carries it (see the census).
- **Adds:**
  - `TargetRef`: a readonly struct holding `Entity Entity` and `ChunkId? Chunk`, with value equality and
    `IsNone`. It lives beside `ChunkId` (`Zone.cs:527` @`283ce7dc`).
  - `Zone.ChunksNear(float2 position, float range, List<ChunkId> into)`: the one chunk query. Belts whose
    annulus cannot reach are skipped. The rest are evaluated through `ChunkPose` (`Zone.cs:285`), and broken
    chunks are skipped through `ChunkExists` (`:231`).
  - `Zone.ChunkVelocity(ChunkId)`: analytic velocity plus the parent `Orbit.Velocity`.
  - **Visibility, per the Q2 ruling:** a chunk is always visible with info 1. How far the player's cycling and
    the AI's query reach is Q13.
- **Per-file changes** (`b66ba524` unless marked):
  - `Entity.cs:46`: `Target` becomes `ReactiveProperty<TargetRef>`.
  - `Entity.cs:201`, `:227`: entity removal clears the slot only when it holds that entity.
  - `Entity.cs:229`: `TargetedBy` fires for entities only.
  - `Entity.cs:233`: `TargetedByCount` counts entity targets only.
  - **`Entity.cs:298-299`:** it clears the target when the target is not in `EntityInfoGathered`. Unchanged, it
    clears every chunk target on the next pass, because a chunk is never in that dictionary. The chunk branch
    clears only when `!Zone.ChunkExists`, or leaves a broken chunk held as the map's `:139` says. Pick one and
    pin it.
  - `Entity.cs:317`: `TrySelectTargetItem` is entity-only.
  - `Entity.cs:1078`: `TargetRange` reads the chunk position for a chunk.
  - `ActionGameManager.cs:365-377` (reticle pick): include the chunk under the reticle, from `ChunksNear` at the
    reticle.
  - `ActionGameManager.cs:381-388` (`TargetNearest`): stays enemies-only. It drives lock.
  - `ActionGameManager.cs:390-404` (next and previous): include chunks within the Q13 reach.
  - `ActionGameManager.cs:412-414` (Cycle Target Item): entity-only.
  - `ActionGameManager.cs:582`, `:745`, `:769`, `:1046`, `:1224`, `:1239`, `:1266-1267`, `:1355-1373`: `.Entity`
    reads, or the chunk position for the indicator.
  - `ActionGameManager.cs:1285`: `UpdateFireControlDebug` takes `TargetRef`.
  - `Weapon.cs:101` `StanceAllowsFire`: true for a chunk. `Weapon.cs:110` `ArcAllowsFire`: the position through the ref.
  - `LockWeapon.cs:86`: a chunk never locks (Q12).
  - `TurretController`, `Combat.cs`, `Minion.cs`, `Ship.cs`, `EntityInstance.cs`, `PropertiesPanel.cs`,
    `FieldDriver.cs`, `ActionBarSlot.cs`: `.Entity` and null checks.
- **Authority map:**
  - Owner: `Entity.Target`, the one slot.
  - Inputs: the player's handlers and AI states (Cut 6).
  - Outputs: the current `TargetRef`.
  - Derived state: `TargetItem`, which is entity-only and nulled on any change as today; `TargetRange`.
  - Forbidden writers: any second target field; any chunk pick that bypasses `ChunksNear`; any chunk visibility
    or detection stored per tick.
  - Shared paths: reticle, cycling, AI, and dock or undock re-activation (`Entity.cs:298-299`).
  - Deletion line: no `ReactiveProperty<Entity> Target` remains.
- **Verification** (Yggdrasil, the M0 command with `--filter FullyQualifiedName~MiningCut3`, then the full suite):
  - `ChunksNearSkipsUnreachableBeltsAndBrokenChunks`: pins the one query and the break rule.
  - `AChunkTargetSurvivesTheInfoPass`: the negative for `Entity.cs:298-299`. It fails on the naive retype.
  - `TargetingAChunkClearsTheAimedItem`: pins the derived `TargetItem` rule.
  - `TargetRefEquality`: the same chunk held twice is equal, and a chunk never equals an entity.
  - `ChunkTargetIsNeverLocked`: pins Q12's default.
  - Every existing fire-control test passes unchanged. That is the negative that ship targeting did not move.
  - Negative grep: `git grep -n "ReactiveProperty<Entity> Target\b"` is empty.
  - Stryker over `TargetRef` and `Zone.ChunksNear` (`dotnet tool install -g dotnet-stryker` inside the same job).
  - Unity compile: Self.
  - Operator: target a rock with the reticle; cycle through rocks and ships; dock and undock with a rock held.
- **Subtraction estimate:** about 0 removed and about 70 added, plus about 90 mechanical site edits (up from the
  map's 60, correction 8).

### Cut 4. A shot at a chunk goes through FireControl (re-anchored to 12.4)

- **Repo/branch:** `codex/mining`, after Cut 3.
- **Lands after:** the fire-control no-target fuse ruling (`docs/fire-control-cut.md`, 12.4 status, "a fused
  weapon fired with no target bursts at the shooter's own position", `FireControl.cs:389`). That ruling's fix
  edits the same `target != null` branches this cut retypes (`:367`, `:372-378`, `:389`). If the ruling is still
  open, this cut preserves today's behaviour for the no-target case and says so in its commit.
- **Deletes first:** none. `PendingShot.Target` (`:1468`) and `ShotOutcome.Target` (`:1598`) become `TargetRef`.
- **Adds:**
  - `Silhouette.Disc(float radiusCells, float precision)`.
  - The shared integrator is extracted from `Silhouette` `:1302-1308` (the `Phi` sum over merged intervals, and
    `span`). The hull path and the disc path then call one function. No second `Phi` summation exists.
- **Per-file changes** (`FireControl.cs` @`b66ba524`):
  - `PFire` `:161-181`: for a chunk, no `VisibleEntities` gate (`:173`) and info 1 (`:179`). A `LockWeapon` is
    gated out (`:175`, Q12). The arc stays (`:176`).
  - `Forecast` `:206`, `HitProbability` `:228`, `Inspect` `:243-283`: the disc silhouette. `Inspect`'s
    `Visible` is true for a chunk.
  - `PredictedIntercept` `:133`, `TravelDirection` `:307`: position and velocity through the ref. `Bearing`
    `:316` is not called for a chunk, because a disc has no facing.
  - `DeviationProbability` `:336-341`: the live chunk position is `Zone.ChunkPose` at `now`.
  - `Fire` `:355-418`: freezes the ref.
  - `Step` `:439`: `targetGone` includes `!ChunkExists`.
  - `CommitProbability` `:506-520`: for a chunk, the disc (no `GetData(Hull)` at `:516`).
  - `Commit` `:550-600`: for a chunk hit, no `Lane` (`:563-578`) and no shield block (`:585-592`). `Cell`,
    `Bearing` and `Lateral` stay zero in `MakeOutcome` (`:1028`).
  - `Apply` `:651-678`:
    - Null fuse on a chunk: `Zone.Wear(chunk, Damage)` (`Zone.cs:260` @`283ce7dc`).
    - Contact or delayed fuse on a chunk: no `ApplyBlastHit` (it needs a hull, `:747-760`). The fuse point is
      the chunk centre at arrival. The chunk's pose is a pure function of time, so the point is exact.
    - Proximity fuse: `Detonate` as today. What it does to chunks is Q10.
  - `Detonate` `:1074-1150`: unchanged unless Q10 says otherwise.
  - Unity presentation: `EntityInstance.cs` (4 target reads) and the four weapon managers aim at the chunk's
    rendered position. Presentation only.
- **Authority map:**
  - Owner: `FireControl`, unchanged.
  - New inputs: chunk pose, velocity and radius through `Zone`.
  - Forbidden writers: any chunk hit test outside `Commit`; any chunk pose read in `Apply` except the fuse point
    of a blast; any second `Phi` integrator.
  - Shared paths: player trigger, AI `Activate` (`Combat.cs:121`), turrets (which never hold a chunk).
  - Deletion line: none.
- **Verification** (Yggdrasil, `--filter FullyQualifiedName~MiningCut4`, then the full suite):
  - `AChunkShotUsesTheSameFactors`: `Accuracy`, `PSpread` and `Sigma` equal a ship shot's at equal range and
    precision. Pins one fire path.
  - `DiscMassIsTheClosedForm`: `POnHull = Φ(R/σ) − Φ(−R/σ)`. Pins the shared integrator.
  - `HullSilhouetteUnchangedByTheExtraction`: every existing `Silhouette` test and the `ProbeC` equivalence
    still pass. This is the negative for the extraction.
  - `AChunkThatBreaksMidFlightIsAMiss`: pins `Step`'s gone rule.
  - `ChunkHitsWearTheChunk`, and `AHitOnABrokenChunkChangesNothing`, which re-pins `34994732` through the real
    path.
  - `LaunchersCannotFireOnAChunk`: pins Q12.
  - `ChunkDiceAreTheShotsOwn`: the same galaxy and shot id give the same outcome.
  - Stryker over the new branches.
  - Operator: shoot a rock with each damage type. It shrinks and breaks, and misses at range look plausible.
- **Subtraction estimate:** about 0 removed and about 80 added. The integrator extraction nets about −5.

### Cut 5. Composition, the loot roll, and the mined lot

- **Repo/branch:** `codex/mining`, after Cut 4. Written for the recommendations in Q4-Q7 and Q11. Rewrite the
  cut if a ruling differs.
- **Deletes first:** `BodyData.Resources` (`ZoneData.cs:78-79` @`283ce7dc`, key 4) becomes a retired-key comment.
  Its only reader, `MineAsteroid`, died in Cut 2.
- **Adds, under Q7 = A′ (see Q7):**
  - A catalog document type per field kind, holding a list of yield entries: commodity reference, abundance,
    and affinity per damage type.
  - A nullable kind reference on the field, written by `ZoneGenerator` for new fields. Existing runs resolve a
    null kind as Q11 rules.
  - `Zone.ChunkComposition(ChunkId, …)`, derived from the kind's entries and a `StableHash` jitter.
  - The loot roll inside `Commit`, after the hit draw, from the shot's own generator. Frozen into
    `ShotOutcome.Loot`.
  - `ItemManager.ExtractedLot(zone, field, commodity)`: find-or-mint, with a source-to-lot index derived at
    construction.
  - `Extracted` (`Provenance.cs:116` @`b66ba524`) gains `Body` at key 2.
  - `Lot` moves from `CraftedItemInstance` (`ItemInstance.cs:38`) to `ItemInstance`, keeping key 11. Coordinate
    with variants C4 (Dependencies).
- **Per-file changes** (`b66ba524`):
  - `FireControl.Apply`: after `Zone.Wear`, mint or find the lot and `TryStore` it (`Entity.cs:1805`).
  - `Entity.cs:1856` (the stack merge matches `Data.Key` only): it must also match `Lot`.
  - `Entity.cs:1934` (the split constructs a `SimpleCommodity` without `Lot`): the split copies `Lot`.
  - `SavedGame.cs:113` (`.OfType<CraftedItemInstance>()` as the ledger roots): every `ItemInstance` with
    `Lot != 0`.
  - `ItemManager.cs:72` (`GetLot(CraftedItemInstance)`): takes `ItemInstance`.
- **Authority map:**
  - Owners:
    - `FireControl.Commit` decides the loot.
    - `ItemManager` mints lots.
    - `EquippedCargoBay` places units.
    - The field-kind record owns what a field yields.
  - Inputs: frozen damage, damage type and penetration; the composition at commit; remaining hit points at commit.
  - Outputs: `ShotOutcome.Loot`, units in cargo, ledger lots.
  - Derived state: composition (never stored); the source-to-lot index.
  - Forbidden writers:
    - any loot draw outside `Commit`, except the blast branch Q10 names;
    - any mined `SimpleCommodity` with `Lot == 0`;
    - any shared random stream;
    - per-ore tables on `SimpleCommodityData` (the Q7 alternative that competes with the kind record).
  - Deletion line: `BodyData.Resources` is retired before the composition function is added.
- **Verification:** the map's list (`:780-792` @`283ce7dc`) stands, with the fixture rebuilt on the kind record,
  plus:
  - `ADebrisKindAndAnAsteroidKindRollDifferentTables`: pins "per field kind" (Q2).
  - `AMinedStackSurvivesSaveGc`: the negative for `SavedGame.cs:113`. It fails on the old roots.
  - `StacksMergeOnlyWithinALot`: the negative for `Entity.cs:1856`.
  - Census before and after: the new slots read as `defaulted_missing_slot` until Cut 7.
  - Operator: mine one rock with a Thermal gun and one with a Kinetic gun, and read the cargo and its brand
    rows.
- **Subtraction estimate:** 2 removed, about 170 added (the kind record costs about 20 more than per-ore fields).

### Cut 6. The AI mines on the same path

The map's Cut 6 (`:800-822` @`283ce7dc`) stands, re-anchored:
- the agent factory is `Zone.CreateAgent` at `Zone.cs:126-133` (`283ce7dc`), which assigns `PatrolOrbitsTask`
  at `:129`;
- the `Minion` transitions are at `Minion.cs:10-21` (`b66ba524`);
- the `Activate` call to copy is `Combat.cs:121`.

It waits on locomotion Cut 4 (not landed; locomotion Cut 1 is the only locomotion cut on fire-control-12, per
`docs/merge-to-master-checklist.md:93`). It inherits fire control's F12-5 ("`Combat.cs:117` gates a proximity
weapon on a direct-hit forecast"): an AI miner with a proximity weapon is gated by the wrong forecast until F12-5
lands. Name it in the cut and do not fix it there.

### Cut 7. Content

The map's Cut 7 (`:826-839`) stands, with three changes:
- It drops the Resource Scanner as the chunk sensor (correction 4).
- It authors the field-kind records (Q7 = A′) instead of per-ore fields.
- It runs on the adopted variants release, if any family is authored as variants (Dependencies).

Its `AetherDb mining-content` command must honour variants Q1a: no plain `Upsert` at a variant key.

## A.4 Subtraction ledger (estimate, whole campaign from here)

| Cut | Removed | Added | Formats and targets |
|---|---|---|---|
| 1-2 (landed) | 250 | 181 src, 681 test | union 26 retired; `ZonePack` key 6 |
| 3 | ~0 (optionally ~15, the scanner `Update`) | ~70, plus ~90 mechanical site edits | `Target` retyped |
| 4 | ~5 (integrator extraction) | ~80 | `PendingShot` and `ShotOutcome` target retyped |
| 5 | 2 | ~170 | a field-kind document type; `Extracted` key 2; `Lot` moves to `ItemInstance` key 11; `BodyData` key 4 retired |
| 6 | ~15 | ~90 | none |
| 7 | 0 | content | catalog records |

No new package, assembly or executable target. The net is additive from Cut 3 on. Each addition buys a named
capability: targetable chunks, the loot roll, provenance, and AI mining.

## A.5 Operator questions (mining), one fork each

Q1-Q3 are ruled. Q4-Q9 are restated with context. Q10-Q13 are new, raised by 12.4 and by the Q2 ruling.

**Q4. The loot weight formula.** (Blocks Cut 5.)

*Context.* You asked for loot weighted by "some arcane combination of the damage type, the chunk's composition,
and the stats of the weapon you hit it with". The map's proposal for one hit by weapon *w* (damage type *d*,
damage *D*, penetration *P*) on chunk *c*:
- **Units** = `min(D, remaining HP) / HP(size) × Yield(size)`: the floor, plus one Bernoulli draw on the remainder.
  A chunk yields the same total however it is broken; damage only gets you there faster.
- **Ore weight** = `Affinity_ore(d) × Comp_c(ore)^(1/Depth)`, with `Depth = 1 + P × MiningDepthPerPenetration`.
  - Affinity is "the right tool for the ore".
  - Penetration flattens the composition toward rare ores, but never removes the common one. This is the 2020
    `pow(x, 1/penetration)` rule, rewritten so penetration 0 means the raw composition.
- Worked table (`:257-264` @`283ce7dc`): the right damage type roughly triples an ore's share, and penetration
  multiplies a rare ore's share further.
- Penetration is being retuned (fire-control F12-2). `MiningDepthPerPenetration` is the one knob that absorbs it.

*Options.*
- **A:** the formula as written.
- **B:** A, plus `DamageSpread` as a yield multiplier ("breadth"). It changes no weights.
- **C:** A without the penetration term.

*Recommend A.* Every term is one sentence, and every weapon stat in it reads as a benefit (your reciprocal-formula
rule). B can be added later without changing a weight.

**Q5. Is the loot rolled per hit, or once when the chunk breaks?** (Blocks Cut 5.)

*Context.* Per-hit rolling keeps only hit points as state. Rolling at break needs either the breaking weapon
alone to decide everything, or per-ore accumulation per chunk.

*Options.*
- **A:** per hit, units proportional to damage.
- **B:** once at break, weighted by the weapon that broke it.
- **C:** once at break, weights accumulated per hit.

*Recommend A.* Your sentence ties the weight to "the weapon you hit it with". Under B, a chip shot from the wrong
gun at the end decides the whole rock.

**Q6. Where does the ore go, and how big is a lot?** (Blocks Cut 5.) Two sub-forks, one decision each.

*Q6a. Destination.*
- **A:** straight into the shooter's cargo at arrival. A full hold loses the overflow.
- **B:** a floating pickup.
- *Recommend A.* Pickups are Unity-only (`ItemPickup`, and collection in `ShieldManager`). An AI can never collect
  them, which breaks the same-inputs ruling.

*Q6b. Lot granularity.*
- **A:** one lot per (zone, field, commodity) source, reused until garbage collection.
- **B:** one lot per hit.
- *Recommend A.* Stacks merge only within one lot, so B makes every hit its own cargo stack and grows the ledger
  per shot. A is also the shape backward generation will synthesize.

Lot quality is 1. Nothing reads a `SimpleCommodity`'s quality today (`ItemManager.cs:117,226` @`b66ba524` read
crafted lots only).

**Q11 (new; answer it before Q7). What is a field kind, and what does each kind yield?**

*Context.* Your Q2 ruling generalized mining to "chunks of a field", with "various kinds of debris fields" beside
asteroid belts, and said a debris field "rolls salvage through the same weighted roll". Today the only field in
the code is `AsteroidBeltData`, and there is no kind concept. The loot entries the map designed are ores
(`SimpleCommodityData`). Salvage could mean commodities, or it could mean gear.

The question is what the kind is for:
- Is a kind a content record an author makes more of: "rich belt", "ice belt", "wreck field of faction X"?
- Is it a small fixed set of simulation categories: rock, debris?
- Can a kind's table name items other than simple commodities, such as a damaged `GearData` from a wreck?

I am not recommending here, because the answer defines the concept. What depends on it:
- where composition lives (Q7);
- whether mined units are always `SimpleCommodity`, which Cut 5's lot and stacking rules assume;
- whether debris salvage enters provenance as `Extracted` or as a different origin.

**Q7 (restated). Where does composition live?** (Blocks Cut 5; follows Q11.)

*Context.*
- `BodyData.Resources` exists on every body. It has had no writer since 2020, its only reader died in Cut 2, and
  0 of the 8 belts in the probe save have data in it.
- The map recommended per-ore `Abundance` and `Affinity` fields on `SimpleCommodityData`, with composition
  derived. That was written before your ruling that tables are per field kind.

*Options.*
- **A′ (new):** composition is derived from a per-kind catalog record: a list of entries, each a commodity,
  abundance and affinity. A field names its kind. Nothing is stored per chunk.
- **A:** the original. Per-ore fields on `SimpleCommodityData` give one global table for every field kind.
- **B:** `ZoneGenerator` writes each belt's `Resources` at generation, and chunk jitter is derived. Runs started
  before Cut 5 stay empty until New Game.

*Recommend A′,* if Q11 says kinds are content.
- It is the only option where a debris field and a belt can differ, which is what Q2 ruled.
- It is also the shape document variants pay off on. "Rich belt" is a variant of "common belt" that overrides one
  entry's abundance by element id. That works only if the entries are a list of objects, not a dictionary: the
  variants ruling Q7 replaces dictionaries whole.

**Q8. The catalog has no Corrosive or Ionizing weapon, now or in its history.** (Blocks Cut 7.)

*Context.*
- Live weapons use four damage types (census, section 0), and the enum has six.
- Both legacy msgpacks use the same four.
- An ore whose affinity peaks on a missing type cannot be reached.

*Options.*
- **A:** author affinities over the four types that exist, and record the gap.
- **B:** author new Corrosive and Ionizing weapons in Cut 7.

*Recommend A.* New weapon types are a content decision, not a mining one.

**Q9. Which AI ships mine?** (Blocks Cut 6.)

*Context.* Nothing assigns a `Mining` task. Every NPC gets `PatrolOrbitsTask` (`Zone.cs:129` @`283ce7dc`).

*Options.*
- **A:** in a zone with fields, an NPC whose loadout has a weapon with nonzero affinity for some ore there gets a
  `Mining` task, at an authored share.
- **B:** only dedicated miner hulls or factions mine. That needs the faction-territory "extraction" role.
- **C:** defer AI mining, and prove the path with a test agent only.

*Recommend A* for this campaign. B can later replace A's eligibility rule without touching the path.

**Q10 (new). What does a blast do to chunks, and who gets the ore?** (Blocks Cut 4's proximity branch and
Cut 5's blast loot.)

*Context.*
- 12.4 made every blast an area: `Detonate` applies damage in proportion to the overlap of the blast disc with
  each covered cell, over every entity, live at detonation (`FireControl.cs:1074-1150` @`b66ba524`).
- It takes no shooter, so it cannot route loot.
- Chunks are not entities, so today a blast ignores them entirely.
- The mining map assumed only the targeted chunk counts.
- One mechanism fact matters: **a chunk's pose is a pure function of time.** `Commit` can compute exactly where
  every chunk will be at arrival, so blast loot can still be decided at commit, as the target's invariant 5
  demands. The only thing that can change between commit and arrival is a chunk breaking, and `Wear` already
  refuses a hit on a broken chunk.

*Options.*
- **A:** a blast covers every chunk its disc overlaps. Each chunk takes its area share. `Commit` computes the
  covered chunks at the arrival pose and rolls their loot from the shot's generator into the shooter's cargo.
  `Detonate` gains a chunk pass through `ChunksNear`.
- **B:** a blast covers only the targeted chunk, with its area share. No other chunk is touched.
- **C:** blasts do not mine. Only direct hits wear chunks.

*Recommend A.*
- It is 12.4's own rule, "a blast has no direction", applied to one more kind of body, so there is one area rule.
- B is a special case that makes a proximity burst next to a rock you did not target do nothing.
- The cost of A is one `ChunksNear` query per blast. The map measured about 55 µs for the largest probe belt.

**Q12 (new, from the map's own open line `:217-220`). Can launchers mine?**

*Context.*
- Lock and guided launchers need a lock. `LockWeapon.cs:86` @`b66ba524` locks only a hostile target, and a rock is
  never hostile.
- So GT 3K, LRMM72, SRMM72, pswarm and scorched void policy cannot fire at a chunk.
- The map said "Rule it if that is wrong", and nobody has.

*Options.*
- **A:** launchers cannot mine.
- **B:** a chunk can be locked, and the lock rule gains a chunk branch.

*Recommend A.* It keeps the lock path as it is, and most launchers are blast weapons, which reach chunks through
Q10 = A anyway when a proximity round is fired at a nearby ship. It is your call if you want missiles as mining
tools.

**Q13 (new). Until the EW campaign, how far away can a chunk be targeted?**

*Context.*
- Your Q2 ruling deferred chunk detection. A chunk is visible unconditionally, and targeting it is an on-demand
  query.
- The reticle pick is local.
- "Next target" and the AI's pick need a radius, or they walk every rock in the zone (809 in the probe zone).
- The map had tied that radius to the Resource Scanner, which your ruling parked.

*Options.*
- **A:** the longest range among the ship's active weapons.
- **B:** a `GameplaySettings` constant.
- **C:** reticle only; cycling skips chunks.

*Recommend A.* It needs no new tunable. Weapon range already gates fire, so a rock you can cycle to is a rock you
can shoot. The EW campaign replaces it.

## A.6 Dependencies (mining)

- **Fire-control-12 on master:** hard, for every cut from M0 on.
- **The no-target fuse ruling:** the same lines as Cut 4 (`FireControl.cs:389`). Land it first or fold it in.
- **F12-2, the penetration retune:** Q4's `MiningDepthPerPenetration` absorbs it. There is no ordering constraint.
- **F12-5, AI and proximity blasts:** Cut 6 inherits it.
- **Locomotion Cut 4:** Cut 6.
- **Settings-globals:** Cut 5's tunables join `GameplaySettings` and `PlanetSettings` wherever those live. None may
  be CultMath-typed while settings are Unity-serialized.
- **Document variants:**
  - **C2 (element ids, in flight on `hands/variants-c2a`):**
    - "Ids everywhere" (variants Q3) requires an element-id member on every object-list element type. For
      mining, that is the Q7 = A′ yield-entry type (new) and `ChunkWearPack` (`ZoneData.cs:51` @`283ce7dc`,
      a list element in `ZonePack`).
    - If C4 lands first, Cut 5 adds ids to its own new types. If Cut 5 lands first, C4's sweep lists them.
    - **Slot coordination:** Cut 5 claims key 11 on `ItemInstance`, the `Lot` move. C4 must not put
      `ItemInstance`'s element id at 11. Whichever lands second checks.
  - **C3 and C4 (Studio, then Aetheria adoption at `caching-unity-v1.5.0`):** Cut 7 content authored as variants
    (field-kind families; a Drill Bit as a variant of an existing beam, if you name a base) waits for C4.
    Content authored as plain records does not. The `AetherDb` content command honours variants Q1a either way.
  - Aetheria pins CultLib `45c2f40` and `caching-unity-v1.4.0` today (`Directory.Build.props`). No mining cut
    needs a CultLib bump.
  - **For the C4 owner, not mining:** four catalog records hold a null `Behaviors` element: Refinery, Deep Ore
    Extractor, Assembly Line and Shipyard (section 0, present on `b66ba524`). C4's `MintElementIds` pass over
    `Aetheria.cc` has to decide what a null list element is: refuse it, drop it, or skip it. The variants cut map
    does not say. These are the station factory gear the mining map leaves alone (`:430-433` @`283ce7dc`).

---

