# Mining: refreshed cut map (v2, operator rulings of 2026-09-30)

v1 dated 2026-09-29; this is v2, 2026-09-30. Imagination output for Self. It re-anchors the Codex mining lane to
the Body as it will stand after `codex/fire-control-12` merges to `master`, and it incorporates the operator's
rulings of 2026-09-30:

| Question | Ruling |
|---|---|
| Q4 | A |
| Q5 | A |
| Q6a | A |
| Q6b | A |
| Q7 | A′ |
| Q8 | **B** |
| Q9 | **C** |
| Q10 | A |
| Q11 | authored catalog records that yield simple commodities only |
| Q12 | A |
| Q13 | A, plus the detection correction |

The Codex map (`docs/mining-cut.md`) is raw material, not authority. Where the two disagree, this file is the
proposal.

**What changed from v1:**
- **Chunk detection** now goes through the existing reflectance rule. It is no longer unconditional, and it is not
  deferred to EW (Cut 3).
- **Cut 6 is deleted.** AI mining is deferred, and the same-path proof is a test agent in Cut 5.
- **Cut 7 authors Corrosive and Ionizing weapons.**
- **The field-kind record arrives in Cut 3** (for cross-section), not Cut 5.
- **Two new forks:** Q14 (how chunk sensor info integrates) and Q16 (how a belt gets its kind).
- **The CultMath-root claim** is settled in the shared precondition.

The moddable-ships half of v1 lives in `docs/moddable-ships-cut.md` on `codex/moddable-ships`. Section 0's ship
rows are kept for the combined test count.

## Rulings added 2026-09-30 (Self, after v2; operator confirmed Q14 A and Q16 B the same day)

- **Q14 A.** A ship's knowledge of a rock is the value the per-tick sensor rule would settle at, computed on
  demand through the shared `Sensor.Gain`. It is stateless and costs nothing per tick.
- **Q16 B.** A belt's field kind is chosen at generation and saved. A belt from an older run with no kind gets
  one assigned once on first load, by the same function, and that assignment is saved.

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

  All five hulls use `Prefab`. This matters to ship migration (`docs/moddable-ships-cut.md` S5).

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

### Building fire-control-12 headless needs two CultLib checkouts

Soul (ships branch) reported that fire-control-12 does not compile against its pinned CultLib `45c2f40`, with
`erf` and `erfinv` missing. v1's merges ran green on `45c2f40`. **Both claims are true. They describe two
different build invocations.**

- `Directory.Build.props` @`b66ba524` pins two revisions of the same repository: `CultLibRevision` `45c2f40`
  (CultCache) and `CultMathRevision` `6d5e209` (CultMath, tag `cultmath-unity-v0.2.4`).
- `CultMathRoot` defaults to `CultLibRoot` when it is not passed.
- `erf` and `erfinv` exist in CultMath at `6d5e209` (`packages/cultmath/src/CultMath/math.cs:528`). They do not
  exist at `45c2f40`: `git grep` finds neither there.
- `FireControl.cs` calls them at `:1323` and `:1348` @`b66ba524` (Cut 12.0 added them to CultMath).

Probed on Yggdrasil at `b66ba524`, `dotnet build Aetheria.Shared`:

| Invocation | Result |
|---|---|
| A. `-p:CultLibRoot=/CultLib` only (at `45c2f40`) | **fails**: `FireControl.cs(1323,54) CS0103 'erf'`, `(1348,53) 'erfinv'` |
| B. as A, plus `-p:SkipCultLibRevisionCheck=true` | the same failure |
| C. `-p:CultLibRoot=/CultLib -p:CultMathRoot=/CultMath` (at `6d5e209`) | **builds** (v1's 379/381 runs used this) |

**The rule for every Hands and Soul job:** pass both roots, each checked out clean at its own pin. Soul's run was
invocation A: a missing CultMath root, not a broken branch.

**A second defect this surfaced (not a mining cut).** Case A should never have reached the compiler. The
`VerifyCultLibRevision` target in `Directory.Build.targets` exists to refuse a CultMath root at the wrong
revision, and here it stayed silent. Its `Condition` compares `ProjectReference` identities against a path
spelled with backslashes (`$(CultMathRoot)\packages\cultmath\...`). I infer that on Linux the identity no longer
matches that spelling, so the guard never runs. I have not verified this on Windows.

This is fire control's or the build owner's to fix. A pin guard that turns itself off on the verification host
is a guard in name only. Hand it to Self as a spawn-worthy defect.

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
4. **Chunk detection: both the Codex map and v1 of this map got the ruling wrong.**
   - The Codex map's Cut 3 (`:667-668`, `:693`) specifies `FireControl.ChunkDetectionRange` from the scanner's
     `Range`, an `UnaidedChunkRange` fallback, and a test `AScannerExtendsChunkDetection`. Its Cut 7
     (`:834-836`) restores "the Resource Scanner as the chunk sensor".
   - The Codex Q2 text (`:176-185`) makes a chunk visible unconditionally with PSensor 1, and defers detection to
     the EW campaign. v1 of this map followed that text.
   - The operator's correction, 2026-09-30: "We can just assign a reflectivity and cross section to each chunk
     just like we do with entities, then their visibility depends on how much light is shining on them, no new
     mechanism needed ... This is what I actually intended with my ruling about deferring detection."
   - So chunk detection is **in this campaign**, through the existing reflectance rule (Cut 3). All three earlier
     readings are superseded:
     - the scanner as chunk sensor;
     - unconditional visibility;
     - PSensor fixed at 1.
   - `ResourceScanner` stays parked.
5. **Q2's blast consequence (`:221-223`) was written against `Splash`.** Cut 12.4 replaced it with
   `Detonate(Zone, float2 worldPlanar, float radius, float damage, DamageType)` (`FireControl.cs:1074` @`b66ba524`).
   - `Detonate` takes no shooter.
   - It walks `zone.Entities` only (`:1084`).
   - So a proximity burst beside a rock does nothing to the rock today, and nothing could route its loot.
   - This was new question Q10, ruled A on 2026-09-30: a blast covers every chunk it overlaps (Cut 4).
6. **`:135`, and the `SimpleCommodityData` comment it copies, are wrong about the retired keys.** Both claim keys
   6, 7 and 8 are retired. They are live: `ItemData` declares them for every item, `SpecificHeat` `Key(6)`,
   `Conductivity` `Key(7)` and `Price` `Key(8)` (`ItemData.cs:291-298` @`b66ba524`), and `SimpleCommodityData`
   inherits them. Only key 11 is retired on `SimpleCommodityData`. Keys 12 and 13 are still free. The
   comment at `ItemData.cs:304` should say "key 11".
7. **Q7's recommendation (`:298-310`) predates the Q2 ruling.** Q2 (`:171-174`) says composition and loot tables
   are **per field kind**, with debris rolling salvage through the same roll. Q7 recommends per-ore `Abundance` on
   `SimpleCommodityData`, which has one table for every field kind. The two are unreconciled. Q7 is restated
   below. Resolved 2026-09-30: Q11 (kinds are authored records yielding simple commodities) and Q7 A′.
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
| 3 | A chunk is a target, detected by reflected light; the field-kind record appears | M0 (Q14 A, Q16 B ruled) |
| 4 | A shot at a chunk goes through FireControl (re-anchored to 12.4); blasts cover chunks | 3; the fire-control no-target fuse ruling |
| 5 | Composition, loot, the mined lot, and the same-path test agent | 4 |
| ~~6~~ | **Deleted** (Q9 = C). The test agent moved into Cut 5. | — |
| 7 | Content: field kinds, ores, the Drill Bit, and new Corrosive and Ionizing weapons | 5; variants C4 for families authored as variants |

Every mining question is ruled.

### M0. Placement and map correction

- **Repo/branch:** Aetheria, `codex/mining`, in a worktree. Do not use the operator's tree.
- **Deletes first:** none in code.
- **Keeps/moves/adds:**
  - `git merge --no-ff master` once fire-control-12 is on master.
  - In `docs/mining-cut.md`: apply corrections 1-11 of A.1.
  - Replace Cut 3 and Cut 4 with the versions below.
  - Strike `UnaidedChunkRange` from Cut 5's and Cut 7's tunables, and "Resource Scanner as the chunk sensor" from
    Cut 7.
  - Replace Q2's "detection is deferred to the electronic-warfare campaign" paragraph (`:176-185`) with the
    operator's 2026-09-30 correction, verbatim.
  - Record the 2026-09-30 rulings in the map's status header.
  - Delete Cut 6 from the map; its history note points here.
  - Mark the `:396-412` table as history, anchored at `8bd25f6f`.
- **Verification:**
  - Yggdrasil, the same job that section 0 ran:
    `ygg-verify.sh <aetheria clone> <merge sha> dotnet '<clone CultLib 45c2f40 to /CultLib and 6d5e209 to /CultMath; restore the two LFS files; dotnet test tests/Aetheria.Shared.Tests -p:CultLibRoot=/CultLib -p:CultMathRoot=/CultMath>'`.
    Both roots are mandatory (shared precondition).
    - The job mounts the Yggdrasil mirrors read-only with
      `DOCKER_ARGS="-v /home/gamecultadmin/eureka-verify/repos:/repos:ro -v /home/gamecultadmin/eureka-verify/lfs-aetheria:/lfs:ro"`.
    - It runs `git config --global --add safe.directory "*"` first. The `GIT_CONFIG_*` safe-directory setting that
      `ygg-verify.sh` exports does not reach a local fetch from `/repos`.
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

### Cut 3. A chunk is a target, and is detected by reflected light

- **Repo/branch:** Aetheria, `codex/mining`, after M-Soul.
- **Written for:** Q13 = A with the detection correction, Q14 = A, Q16 = B (all ruled).

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

### Cut 5. Composition, the loot roll, the mined lot, and the same-path test agent

- **Repo/branch:** `codex/mining`, after Cut 4.
- **Written for:** Q4 A, Q5 A, Q6a A, Q6b A, Q7 A′, Q9 C, Q10 A and Q11, all ruled on 2026-09-30.

**Deletes first:**
- `BodyData.Resources` (`ZoneData.cs:78-79` @`283ce7dc`, key 4) becomes a retired-key comment. Its only reader,
  `MineAsteroid`, died in Cut 2.
- `Agents/Tasks/Mining.cs` and its `.meta` (@`283ce7dc`: 19 lines, no reader, not persisted per `AgentTask.cs:11`,
  and not registered in any union). AI mining is deferred (Q9 = C), and the campaign that picks it up writes its own
  task.
  - Leave the `TaskType.Mine` enum member (`AgentTask.cs:27`). Removing it would renumber the members after it for
    no gain.

**Adds:**
- **`FieldKindData` gains `Entries`**, a `List<FieldYield>`. Each `FieldYield` is:
  - `Commodity`: a `CultRecordRef<SimpleCommodityData>` (simple commodities only, Q11);
  - `Abundance` (float);
  - `Affinity`: an object list of `(DamageType, float)` pairs, absent meaning 1.

  **Every list here is an object list, not a dictionary.** Variants address object-list elements by id and replace
  dictionaries whole (variants Q7). So a "rich belt" variant can override one entry's abundance.
- **`Zone.ChunkComposition(ChunkId, Span<…>)`**: derived. `Abundance_o × exp(ChunkCompositionVariance × g(StableHash(field key, index, o)))`,
  normalized, from the belt's kind (Cut 3). Never stored.
- **The loot roll (Q4 A, Q5 A)**, for one hit with damage *D* on a chunk of size *s*:
  - units = `min(D, remaining HP) / HP(s) × Yield(s)`, the floor plus one Bernoulli draw on the remainder;
  - the weight of ore *o* = `Affinity_o(d) × Comp(o)^(1/Depth)`, with `Depth = 1 + P × MiningDepthPerPenetration`;
  - one draw per unit.
- **Where each roll happens:**
  - **Direct hit:** inside `Commit`, after the hit draw, from the shot's own generator (`FireControl.cs:540`
    @`b66ba524`). Frozen into `ShotOutcome.Loot` (commodity and count pairs).
  - **Blast (Q10 = A):** inside `Detonate`'s chunk pass, per covered chunk, with that chunk's share as *D*. The
    draws come from a generator seeded by the same `(zone, shot)` pair as the shot's commit dice, plus a fixed
    blast salt, walked in `ChunksNear` order (belt key, then index). That keeps the dice the shot's own, and
    deterministic.
  - **This amends target invariant 5.** The old wording was "the loot is decided at Commit". The new wording:
    "loot is decided by the owner that decides the damage, from the shot's own dice". That is `Commit` for a direct
    hit, and detonation for a blast, following 12.4's bystander rule. I state it here, not as a new fork: it follows
    from Q10 = A plus 12.4's ruled model.
- **`ItemManager.ExtractedLot(zone, field, commodity)`** (Q6b A): find-or-mint, with a source-to-lot index derived
  at construction and never persisted.
- **`Extracted`** (`Provenance.cs:116` @`b66ba524`) gains `Body` at key 2 (`CultRecordRef<BodyData>`, nil on older
  records).
- **`Lot` moves** from `CraftedItemInstance` (`ItemInstance.cs:38`) to `ItemInstance`, keeping key 11. Coordinate
  with variants C4 (A.6).
- **The deposit (Q6a A):** units go straight into the shooter's cargo at arrival (a direct hit) or at detonation (a
  blast), through `TryStore` (`Entity.cs:1805`). A full hold loses the overflow.
- **Tunables:** `MiningDepthPerPenetration` on `GameplaySettings`; `AsteroidYield` (`ExponentialLerp`) and
  `ChunkCompositionVariance` on `PlanetSettings`. None may be CultMath-typed while settings are Unity-serialized.
- **The same-path test agent (Q9 = C, replacing Cut 6).** A headless test drives a ship through exactly the
  player's calls:
  1. `Entity.SetTarget(TargetRef(chunk))` (Cut 3);
  2. `weapon.Activate()` (the call `Combat.cs:121` and the player's trigger make);
  3. `Zone.Update` ticks.

  There is no `Agent`, no `AgentTask` and no `Minion` state. It proves the path an AI will later drive, without
  building the AI.

**Per-file changes** (`b66ba524`):
- `FireControl.Commit`, `ApplyDirectHit` and `Detonate`: the roll and the deposit, as above.
- `Entity.cs:1856`: the stack merge matches `Data.Key` only today; it must also match `Lot`.
- `Entity.cs:1934`: the split constructs a `SimpleCommodity` without `Lot`; it must copy `Lot`.
- `SavedGame.cs:113`: the ledger roots are `.OfType<CraftedItemInstance>()` today; they become every
  `ItemInstance` with `Lot != 0`.
- `ItemManager.cs:72`: `GetLot(CraftedItemInstance)` takes `ItemInstance`.

**Authority map:**
- **Owners:**
  - `Commit` decides direct-hit loot, and `Detonate` decides blast loot, each from the shot's dice.
  - `ItemManager` mints lots.
  - `EquippedCargoBay` places units.
  - `FieldKindData` owns what a field yields.
  - `FieldKinds.Assign` owns which kind a belt is.
- **Inputs:** frozen damage, damage type and penetration; the chunk's composition and remaining hit points at the
  deciding moment.
- **Outputs:** `ShotOutcome.Loot`, units in cargo, ledger lots.
- **Derived state:** composition (never stored); the source-to-lot index.
- **Forbidden writers:**
  - any loot draw outside `Commit` and `Detonate`'s chunk pass;
  - any mined `SimpleCommodity` with `Lot == 0`;
  - any shared random stream;
  - per-ore tables on `SimpleCommodityData`;
  - any extraction call that bypasses `FireControl`.
- **Shared paths:** the player's trigger and the test agent (identical calls); save GC; Continue, where the index is
  rebuilt.
- **Deletion line:** `BodyData.Resources` is retired, and `Mining.cs` deleted, before the roll is added.

**Verification** (Yggdrasil, `--filter FullyQualifiedName~MiningCut5`, then the full suite):

| Test | Rule it pins |
|---|---|
| `RightDamageTypeRaisesTheOresShare` | The Q4 table, as a fixture on a kind record. |
| `PenetrationFlattensTowardRareOre`, `ZeroPenetrationReadsTheRawComposition` | Q4's depth term. |
| `AChunkYieldsItsAuthoredTotalWhateverBreaksIt` | Invariant 7, summed over direct hits and blasts from different weapons. |
| `DirectHitLootIsDecidedAtCommit` | Loot frozen in `ShotOutcome`, performed at arrival. |
| `BlastLootIsDecidedAtDetonationFromTheShotsDice` | The amended invariant 5. The same galaxy and shot give the same ore. |
| `ABlastPaysTheShooter` | Q10 = A. |
| `ASourcelessBlastWearsButYieldsNothing` | The null loot sink. |
| `MinedUnitsCarryAnExtractedLotNamingZoneAndField` | Provenance, including `Extracted.Body`. |
| `UnitsFromOneSourceStack`, `UnitsFromTwoSourcesDoNot` | Q6b A, and the negative for `Entity.cs:1856`. |
| `ASplitStackKeepsItsLot` | The negative for `Entity.cs:1934`. |
| `AMinedStackSurvivesSaveGc` | The negative for `SavedGame.cs:113`. It fails on the old roots. |
| `AFullHoldLosesTheOverflow` | Q6a A. |
| `CompositionIsStableAcrossContinue` | Derived, never stored. |
| `TwoKindsRollDifferentTables` | Per field kind (Q2, Q11). |
| `ATestAgentMinesThroughThePlayersPath` | Q9 = C. After N seconds: ore in the hold, every unit's lot `Extracted` from that belt, and every unit traceable to a `ShotOutcome` or a detonation. The negative: grep for any call to `Zone.Wear` or `ItemManager.ExtractedLot` outside `FireControl.cs`. |

Also:
- Stryker over the formula, the blast pass and the mint.
- Census: the new `FieldKindData` slots read as `defaulted_missing_slot` until Cut 7.
- Operator: mine one lit rock with a Thermal gun and one with a Kinetic gun, and read the cargo and its brand rows.

**Subtraction estimate:** about 21 removed (`Resources` 2, `Mining.cs` 19). About 190 added: the roll, the blast
loot, the mint, the kind entries and the test agent. The test agent replaces Cut 6's ~90 lines.

### Cut 6. Deleted (Q9 = C)

AI mining is deferred. Its same-path proof is the test agent in Cut 5. The Codex Cut 6 (`:800-822` @`283ce7dc`),
its locomotion Cut 4 dependency and its inheritance of F12-5 all leave this campaign.

The campaign that builds AI mining starts from:
- `Zone.CreateAgent` (`Zone.cs:126-133` @`283ce7dc`);
- the `Minion` transitions (`Minion.cs:10-21` @`b66ba524`);
- the one writer `Entity.SetTarget`;
- `Combat.cs:121`'s `Activate`.

### Cut 7. Content (operator review sheet, one catalog commit)

This is a content cut, applied through an `AetherDb mining-content [apply]` command. It is a dry run by default,
and the precedent is `targeting-catalog`. The operator reviews a sheet before `apply`. The command honours variants
Q1a: no plain `Upsert` at a variant key.

**1. Ores.** The 14 legacy ores missing from live, restored with their legacy mass, specific heat, conductivity and
category (values are in `legacy-0414`, per the Codex map's `:830-832`):
- Minerals: Crystal, Silicon, Carbon, Nitrogen;
- Metals: Iron, Titanium, Copper, Lead;
- Compounds: Ethanol, Hydrocarbons, Acid, Oxygen, Carbon Dioxide;
- Organics: Plant Matter.

Simple commodities only (Q11).

**2. Field kinds.** One `FieldKindData` "Asteroid" record:
- `CrossSection` per cell: recommend about 5. That is dull rock, below Longinus's 7.6 and Djinni's 15 per cell at
  their maximum reflector values (section 0).
- `GenerationWeight` 1.
- `Entries` over the live and restored ores, each with an `Abundance` and `Affinity` over **all six damage types**,
  now that Q8 = B makes every type reachable.

Further kinds ("rich", "ice", debris) are the operator's to add. Each is a variant of "Asteroid" overriding entries
by id, once variants C4 lands.

**3. Drill Bit (Q1 A).**
- A `ConstantWeaponData` beam at Kinetic, with high penetration and range 5-20.
- DPS, efficiency and penetration are from legacy `MiningToolData` (DPS 1-2.5, efficiency 2-5, penetration 1-5,
  range 5-20).
- No live weapon uses `ConstantWeaponData` (census), so the Drill Bit is also the first live constant weapon. The
  operator's check covers the beam's presentation.

**4. Corrosive and Ionizing weapons (Q8 = B, against v1's recommendation).**

*The balance fact this rests on (read at `b66ba524`):* **damage type does nothing in combat today.**
- `WeaponData.DamageType` (`Weapon.cs:22-23`) is frozen into the shot (`FireControl.cs:1477`) and passed to
  `Shield.CanTakeHit` and `TakeHit` (`Shield.cs:166`, `:186`). Both ignore their `type` argument.
- Armour absorption (`ArmorAbsorb`, `ItemAbsorb`) and `Detonate` take no type.
- The only other reader is `AbsorbEvent`, which is presentation (`CapabilityEvents.cs:28`).
- Mining's affinity (Cut 5) becomes **the first simulation reader of damage type.**
- Therefore a new-type weapon's combat power is fixed entirely by its non-type stats. Its identity shows only in
  mining and presentation.

*Anchors (census, merged tree):*
- Kinetic 9, Electric 3, Optical 3, Thermal 3.
- Sustained single-target DPS (`Damage × Count / Cooldown`, min to max quality) for the auto class:
  - Autocannon 6;
  - ClearPath 20-180;
  - Spectra 40-340;
  - ColdFire 16-160;
  - CShot RainbowLite 33-600.
- Burst weapons (DeathCluster, FastBlast+-) are magazine-limited and are not comparable by DPS.

*Records proposed.* One per type. Each is a minimal override of an existing weapon, so its combat envelope equals
its base's by construction, and balance risk is zero on day one.

| New record | Type | Base (class, hardpoint) | Overrides | Why this base |
|---|---|---|---|---|
| (name: operator's) | Corrosive | ClearPath (`AutoWeaponData`, Ballistic) | `Name`, `DamageType` | Short-range stream: a sprayer. Range 250-600, DPS 20-180, cheap (price 50,000, mass 50). |
| (name: operator's) | Ionizing | Spectra (`AutoWeaponData`, Energy) | `Name`, `DamageType` | Beam-like optical: a particle beam. Range 800-1750, DPS 40-340. |

Optional second step, flagged for review and not default: give the Ionizing record `Penetration` 0.25 (Autocannon's
value) with damage × 0.8 to pay for it. That makes it the rare-ore tool through Q4's depth term. It changes combat,
because penetration drives 12.3's lanes, so it needs the operator's eye and F12-2's retune.

*Presentation:* the effect prefab and icon stay the base's until art exists. The Corrosive and Ionizing presentation
look is the operator's call, and so are the names.

*Variants:* both records are ideal variants: a base plus two overrides, one of them nested in
`Behaviors[weapon].DamageType`. A nested override needs element ids (C2), and Aetheria adopting them needs C4.
- If Cut 7 runs before C4: author them as plain records, and list them in C4's family re-author pass.
- If it runs after: author them as variants.

Either way, nobody copies a record by hand twice.

**5. Tunables:** values for `AsteroidYield`, `ChunkCompositionVariance` and `MiningDepthPerPenetration`.

**Verification:**
- `census` and `dangling` before and after.
- Cut 5's loot fixture runs against the live catalog. It asserts that every ore in every kind is reachable, meaning
  some live weapon's damage type has an affinity above 1 for it.
- `EveryDamageTypeHasALiveWeapon` pins Q8 = B.
- Operator: review the sheet; mine with the new Corrosive and Ionizing weapons; feel the Drill Bit.

## A.4 Subtraction ledger (estimate, whole campaign from here)

| Cut | Removed | Added | Formats and targets |
|---|---|---|---|
| 1-2 (landed) | 250 | 181 src, 681 test | union 26 retired; `ZonePack` key 6 |
| 3 | ~0 (optionally ~15, the scanner `Update`) | ~140, plus ~95 mechanical site edits | `Target` retyped; `FieldKindData` document type; `AsteroidBeltData.Kind` (nullable) |
| 4 | ~5 (integrator extraction) | ~115 | `PendingShot` and `ShotOutcome` target retyped; `Detonate` gains a chunk pass and a loot sink |
| 5 | ~21 (`Resources`, `Mining.cs`) | ~190 | `FieldKindData.Entries`; `Extracted` key 2; `Lot` moves to `ItemInstance` key 11; `BodyData` key 4 retired |
| 6 | deleted | 0 | none |
| 7 | 0 | content | 14 ores, 1 field kind, the Drill Bit, 2 weapons |

No new package, assembly or executable target. The net is additive from Cut 3 on. Each addition buys a named
capability: detectable, targetable chunks, the loot roll, blast mining, and provenance. Deleting Cut 6 removed
about 90 lines of AI code from the plan.

## A.5 Operator questions (mining)

### Ruled, 2026-09-30 (recorded, not reopened)

| Q | Ruling | Where it lands |
|---|---|---|
| Q4 | A, the formula as written | Cut 5 |
| Q5 | A, per hit | Cut 5 |
| Q6a | A, straight into cargo; overflow lost | Cut 5 |
| Q6b | A, one lot per (zone, field, commodity) source | Cut 5 |
| Q7 | A′, a per-kind entry list (commodity, abundance, affinity), derived, never stored per chunk | Cuts 3 and 5 |
| Q8 | **B**, author Corrosive and Ionizing weapons (against v1's recommendation) | Cut 7 §4 |
| Q9 | **C**, AI mining deferred; the path is proven by a test agent (against v1's recommendation) | Cut 5; Cut 6 deleted |
| Q10 | A, a blast covers every chunk it overlaps; the shooter gets the loot | Cuts 4 and 5 |
| Q11 | field kinds are authored catalog records; tables yield simple commodities only | Cuts 3, 5 and 7 |
| Q12 | A, launchers cannot mine | Cuts 3 and 4 |
| Q13 | A, reach is the longest active weapon range. Detection correction: chunks are detected by the existing reflectance rule, not deferred to EW | Cut 3 |

Earlier rulings: Q1 A, Q2 A (one slot, two kinds; field-kind generalization), Q3 A.

### Open, two forks raised by the Q13 correction

**Q14. How does a ship's knowledge of a rock build up over time?** (Blocks Cut 3's `ChunkInfo`.)

*Context.* Your correction puts chunks on the same detection rule as ships:
- visibility = cross-section × light (`Reflector.cs:46`);
- each sensor adds info in proportion to visibility, sensitivity and angle over distance;
- info decays.

For ships this is a per-tick integrator. Every sensor visits every entity every tick and keeps a running value
per pair (`Sensor.cs:157-189`). Knowledge rises over a few seconds, then holds or fades. That per-tick loop is
exactly what cannot run over rocks: the probe zone has 809, and the loop costs about 184 ns per body per tick per
sensor, which is the kind of per-rock cost you asked to slim down.

The gain arithmetic itself is shared in every option below; Cut 3 extracts it into one function. The question is
only how it is integrated over time for a rock.

*Options.*
- **A. Settled value, on demand.** A rock's info is the value the per-tick rule would settle at, for this observer,
  now. It is computed when something asks (targeting, the fire gate), stored nowhere, and costs nothing per tick.
  - What you lose: the rise time. A lit rock is known at its settled level the moment it is in range. A rock never
    tries to hide, so that delay carries little play.
  - Pings: a ping's boost is `visibility × …` (`Sensor.cs:170-175`), so a dark rock gains nothing from a ping in
    any option.
  - A parity test pins that a ship with the same reflector, held still, settles at exactly A's value.
- **B. Per-tick accumulation within reach.** Each sensor-bearing ship integrates rocks within its longest weapon
  range every tick.
  - It has the ships' exact dynamics, rise time included.
  - Cost per tick grows with ships × rocks in reach, and it keeps state per ship per rock, which needs pruning.
- **C. Lazy integration.** Store (value, last-time) per ship per rock, and integrate the elapsed time when queried.
  - It is close to B's dynamics with no per-tick cost.
  - The state grows with every rock a ship ever looked at, and it is approximate when light changes between
    queries.

*Recommend A.* It is the same rule with no new state and no per-tick cost, and it matches the campaign's founding
invariant: chunk quantities are pure functions of time. The one behaviour it drops, the rise time, belongs to things
that move and hide, and rocks do neither.

**Q16. How does a belt get its field kind, including belts in runs that started before this campaign?** (Blocks
Cut 3.)

*Context.*
- Your Q11 ruling makes field kinds authored records. A chunk's reflectivity (Cut 3) and its yield table (Cut 5)
  both come from its belt's kind.
- Today belts are generated with no kind (`AsteroidBeltData`, `ZoneData.cs:101-105` @`283ce7dc`), and a saved run
  keeps its belts for the whole run.
- Only one kind will exist at first ("Asteroid"), but you will add more.

*Options.*
- **A. Derived, never stored.** A belt's kind is a pure function of the belt's identity and the catalog's kinds
  (weighted pick). Nothing is saved. Adding or reweighting a kind silently re-kinds belts the player has already
  mined.
- **B. Stored at generation, assigned once for old belts.** New belts get a kind when generated. A belt loaded
  without one is assigned by the same function the first time it loads, and saved. Adding a kind later never
  changes an existing belt.
- **C. Stored at generation only.** Belts from older runs stay kind-less: dark, unminable and unseen, until New
  Game.

*Recommend B.* One assignment function serves both paths (generation and first load), so there is no second
rule. A belt keeps the identity the player learned. A is the only option where a catalog edit rewrites the world
under a running save.

## A.6 Dependencies (mining)

- **Fire-control-12 on master:** hard, for every cut from M0 on.
- **Both CultLib roots in every build:** CultCache `45c2f40` and CultMath `6d5e209` (shared precondition). The
  `VerifyCultLibRevision` guard does not fire on the Linux verify host. That is a separate defect, handed to Self.
- **The no-target fuse ruling:** the same lines as Cut 4 (`FireControl.cs:389`). Land it first or fold it in.
- **F12-2, the penetration retune:** Q4's `MiningDepthPerPenetration` absorbs it. It gates Cut 7 §4's optional
  Ionizing penetration step.
- **F12-6, mines:** when mines ship, they are sourceless blasts. They wear chunks and yield nothing (Cut 4's null
  sink).
- **Settings-globals:** Cut 5's tunables join `GameplaySettings` and `PlanetSettings` wherever those live. None may
  be CultMath-typed while settings are Unity-serialized.
- **No longer dependencies (Cut 6 deleted):** locomotion Cut 4, and F12-5 (AI and proximity blasts).
- **Document variants:**
  - **C2 (element ids, in flight on `hands/variants-c2a`):**
    - "Ids everywhere" (variants Q3) requires an element-id member on every object-list element type. For mining,
      that is `FieldYield` (new, Cut 5), the affinity pair type inside it (new), and `ChunkWearPack`
      (`ZoneData.cs:51` @`283ce7dc`, a list element in `ZonePack`).
    - If C4 lands first, Cuts 3 and 5 add ids to their own new types. If they land first, C4's sweep lists them.
    - **Slot coordination:** Cut 5 claims key 11 on `ItemInstance` (the `Lot` move), and Cut 3 claims the next
      free key on `AsteroidBeltData`. C4 must not put an element id on either. Whichever lands second checks.
  - **C3 and C4 (Studio, then Aetheria adoption at `caching-unity-v1.5.0`):**
    - Field-kind families ("rich belt" as a variant of "Asteroid") and the two new weapons (variants of ClearPath
      and Spectra, with a nested `DamageType` override) are the natural variant content.
    - If Cut 7 lands before C4, it authors them as plain records and adds them to C4's re-author list.
  - Aetheria pins CultLib `45c2f40` and `caching-unity-v1.4.0` today (`Directory.Build.props`). No mining cut
    needs a CultLib bump.
  - **For the C4 owner, not mining:** four catalog records hold a null `Behaviors` element: Refinery, Deep Ore
    Extractor, Assembly Line and Shipyard (section 0, present on `b66ba524`). C4's `MintElementIds` pass over
    `Aetheria.cc` has to decide what a null list element is: refuse it, drop it, or skip it. The variants cut map
    does not say.
