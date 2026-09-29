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

## Rulings (operator, 2026-09-30)

- **MQ1: a ship `.cc` is self-contained.** Operator: "The idea is for the new moddable ships to eventually
  take over from the existing system. A ship .cc should contain everything needed to instantiate and render a
  ship." The confirmed reading: the hull is its own `HullData` record and the visual is its own record, both in
  the one ship `.cc`, side by side. The hull is not embedded in the visual. A hull family such as Longinus and
  LonginusX lives together in one `.cc`, so variants resolve within one store. S1 follows option B, with both
  records inside the ship's own store.
- **MQ2 A for now.** There are no cross-file variants of shipped hulls in this lane. **Operator note, to be
  carried into CultLib's variants target:** "we'll want cross-store variant support eventually; modders should
  be able to add variants of existing items without modifying the original".
- **MQ3 A.** The FBX prefab builder is scaffolding. The typed `.cc` lane replaces the existing system, and the
  builder dies at S5.
- **MQ4 A.** Continue refuses loudly and names the missing mod ids.
- **MQ5 A.** The game composes the modded catalog at boot, from `GameData/Mods`, into a disposable file.
- **Soul (2026-09-30) on `375d6bd4`** found the Blender↔C# `.cc` round trip real, with no vendored codec.
  Before any cut builds on it, the S-Soul fix slot must fix:
  - F1: 8 of 9 validator mutations survive, including the `..` path guard and the prefab refusal.
  - F2: `Compose`, `ReadPackage` and `ReadNodeIds` are untested.
  - F3: there are no Python tests; slot numbers and enums are duplicated by position.
  - F6: `CatalogTypes` includes `ShipAuthoring`. Check name collisions.
  - F7: a drive-by `using` removal.
  - F5 is settled by MQ1.

- **Pivots own firing arcs (operator, 2026-09-30, from fire control).** A turret's arc belongs to its
  pivot, not its hardpoints. When this campaign adds pivots to `HullData`, the arc moves from the hardpoint
  to the pivot, and the interim "intersection of hardpoint arcs per pivot" rule in fire control is deleted.
  Unity's `Pivot.<group>.<yawMin>.<yawMax>...` node-name parsing (`ShipPrefabAuthoring.cs:144`) must not
  survive as a second arc authority.

# Part B. Moddable ships

## B.1 Status header (verified against git)

Branch `codex/moddable-ships`, tip `375d6bd4`: 4 commits on `4b594e11`. `4b594e11` is an ancestor of fire
control, which has since moved 8 commits to `b66ba524`. Neither side touches the other's files.

| SHA | What landed |
|---|---|
| `42eb5214` | `ShipAuthoring` record and validator; `AetherDb ship-authoring create/inspect/validate`; Blender LineArt capture; 2 xunit tests |
| `4ba4bbaf` | `ShipModCatalog.Compose` (derived catalog) and GLB node-id reader |
| `24c29640` | Unity `ShipModVisual` (glTFast runtime import, line mesh) and Editor preview; `com.unity.cloud.gltfast` 6.20.0 added to `Packages/manifest.json` |
| `375d6bd4` | Blender layout editing (hull grid, hardpoints) through `ship_cc.py` |

The delta is +511 C# (including `tools/AetherDb`), +494 Python, +119 tests, and one new Unity package dependency.
It also carries a stray fix in `TradeMenu.cs` (an unused `using System.Runtime.Remoting.Contexts`).

**Test counts.** The two ship tests pass on the merged tree: 364/364 (section 0).

**Proof-gate status against the doc's own gates (`docs/moddable-ship-authoring.md:149-164` @`375d6bd4`):**

| Gate | Doc implies | Verified in git |
|---|---|---|
| 1. C#/Python round trip preserving unknown fields | met | **C# only.** `StandaloneShipRoundTripsWithoutTouchingTheCatalog` pins C#. The Python half and "preserving unknown future fields" rest on the Quiet probe, whose output `Build/quiet.cc` is git-ignored. No repository test exercises `ship_cc.py`. |
| 2. Blender edits cells and hardpoints | met, except node-id authoring | **Code exists, unpinned.** `ship_cc.replace_layout` (`ship_cc.py:75-106`) has no test. |
| 3. Validator rejects malformed records, changes nothing on failure | met, and compose checks it | **Validator pinned.** `InvalidSchematicOrDanglingMountCannotBePublished` covers 5 cases. **Compose unpinned:** no test covers key collision, the atomic replace, a missing GLB node, or "the previous derived file is unchanged on failure" (`ShipModCatalog.cs:23-73`). |
| 4. Loads in a built player | outstanding | not started |
| 5. Play checks | outstanding | not started |

**Corrections to `docs/moddable-ship-authoring.md` at `375d6bd4`:**

1. **`:9-16` and `:132-136` report the Quiet probe's numbers as facts.** They include 1,040 polylines and
   "206 shipped records became 208". Nothing in git reproduces them. Label them as an unrecorded probe, or
   commit a fixture that pins them.
2. **The composed catalog holds two copies of every mod hull.**
   - `:114-118` says compose preserves one owner per ship.
   - `ShipModCatalog.cs:60-61` upserts `ship.Hull` as a `HullData` at `mod-hull:<id>`, **and** the whole
     `ShipAuthoring`, whose key 1 is the same `HullData`, at `mod-ship:<id>`.
   - Gameplay can read only the first. The second is a dead copy of the same truth inside a derived file. Q1
     decides the fix.
3. **`AetheriaStores.CatalogTypes` now routes `ShipAuthoring` to the shipped catalog** (`AetheriaStores.cs:9`
   @`375d6bd4`), while the doc says the shipped catalog "contains no copy of that ship". Routing a type is not
   holding a record, so this is harmless today. It stays correct only while nothing writes a `ShipAuthoring`
   through a catalog-writable cache on `Aetheria.cc`. Name that invariant in the doc.
4. **`:98-106` "the same semantic validator" on every path.** The Blender side validates nothing semantic: it
   checks grid bounds and footprint shape (`ship_cc.py:83-97`). "Shared path" is true for the C# callers only.

## B.2 Merge path onto the fire-control-12 lineage

| Probe | Result |
|---|---|
| `git merge-tree --write-tree origin/codex/fire-control-12 origin/codex/moddable-ships` | clean, tree `97711bcd` |
| `git merge-tree --write-tree 8a2db490 origin/codex/moddable-ships` (master+fc12) | clean, tree `88672977` |
| on top of mining merged into master+fc12 | clean, tree `a7410174` |
| files shared with fire control `4b594e11..b66ba524` (6 files: `Entity.cs`, `FireControl.cs`, two test files, two docs) | none |

The merged tree passes the full headless suite: 364/364 alone, and 381/381 with mining (section 0).

The only cross-lane seam is `Packages/manifest.json` (glTFast). It merges textually, but it needs Unity to
resolve the package on first open, like the CultMath tag already does (`docs/merge-to-master-checklist.md:14`).

## B.3 Ordered cuts

| Cut | Nature | Blocked by |
|---|---|---|
| S0 | Placement: merge master, correct the doc | fire-control-12 on master |
| S-Soul | Fix batch from the parallel Soul pass | Soul's report |
| S1 | One hull record, and a hull names its visual | MQ1 |
| S2 | The game composes and loads mod ships (`ShipInstance` wiring, boot preload) | S1; MQ4, MQ5 |
| S3 | Gate 4: a built player loads a mod ship | S2; a playable mod ship (content) |
| S4 | Gate 5: play checks | S3 |
| S5 | Migration of shipped hulls, onto variants; FBX builder retirement | S4; MQ2, MQ3; variants C4 |

### S0. Placement and doc correction

- **Repo/branch:** Aetheria, `codex/moddable-ships`, in a worktree.
- **Deletes first:** none.
- **Keeps/moves/adds:** `git merge --no-ff master`. Apply corrections 1-4 to the doc.
- **Verification:**
  - Yggdrasil full suite on the merge (the M0 command).
  - Unity batchmode compile of the merged tree with glTFast resolved. This is Self's step, and it needs network
    for the package.
  - The Editor smoke already in the branch: `-executeMethod ShipModPreview.Smoke -shipModPath <package/ship.cc>`
    (`ShipModPreview.cs:8` @`375d6bd4`), run against a committed fixture package. It is the first recorded run
    of that smoke.
- **Subtraction:** docs only.

### S-Soul. Fix slot for the parallel Soul pass

- **Repo/branch:** `codex/moddable-ships`, after S0.
- **Content:** Soul's findings. Two areas this map expects Soul to own, and does not duplicate:
  - Compose's unpinned failure paths (B.1, gate 3).
  - The hand-copied schema on the Python side:
    - `ship_cc.py:17-21` slot constants (`HULL_SLOT 1`, `HULL_SHAPE_SLOT 5`, `HULL_HARDPOINTS_SLOT 23`);
    - `ship_cc.py:88-98` hardpoint slots 0-6;
    - `__init__.py:18-21`, a copy of the `HardpointType` and `ItemRotation` enums.

    All of these match C# today: `ItemData.cs:287` `Shape` is `Key(5)`, `HullData.Hardpoints` is `Key(23)` at
    `:512`, and `HardpointData` is `:553-563`, all @`b66ba524`. `Enums.cs:38` matches too. The hazard is drift:
    fire control added slot 6 (`FiringArc`) to `HardpointData` in this same lineage. The memory
    `honor-invariants-over-workarounds` names hand-copied schemas as a known failure. The coherent fix reads
    the published schema catalog. Soul decides whether it is in scope.

### S1. One hull record, and a hull names its visual (written for MQ1 = B)

- **Repo/branch:** `codex/moddable-ships`, after S-Soul.
- **Deletes first:**
  - `ShipAuthoring.Hull` (`ShipAuthoring.cs:15`, key 1) becomes a retired-key comment.
  - The embedded-hull branch of the validator (`ShipAuthoring.cs:84-133`) moves; it does not duplicate.
  - The second upsert in `ShipModCatalog.Compose` (`ShipModCatalog.cs:60-61`) becomes a verbatim copy of the
    mod store's records.
  - `ship_cc.py`'s `body[HULL_SLOT]` indirection (`:63-66`, `:80-100`).
  - The only existing `.cc` with the old shape is the git-ignored `Build/quiet.cc` probe. No committed content
    migrates.
- **Adds:**
  - A mod `.cc` holds two records: a `HullData` at the deterministic key `mod-hull:<id>` (`ShipModCatalog.cs:20`)
    and the `ShipAuthoring` visual package (`Id`, `ModelAsset`, `Anchors`, `SchematicLines`).
  - `HullData` gains a nullable `CultRecordRef<ShipAuthoring> Visual` at the next free key on its ancestry.
    `EquippableItemData` owns keys up to 31 (the comment at `ItemData.cs` `WeaponItemData.Fuse`), so it is 32,
    unless C4 claims that slot first.
  - The validator refuses a hull with both `Prefab` and `Visual` set, or neither, when it is a ship.
  - The validator is one function over (hull, visual). It checks every `Hardpoints[].Transform` against the
    visual's anchor ids, the checks that are at `ShipAuthoring.cs:111-136` today.
- **Authority map:**
  - Owner: the `HullData` record owns hull semantics (cells, hardpoints, stats). The `ShipAuthoring` record owns
    the visual package (model path, anchor-to-node map, lines).
  - Inputs: the mod `.cc`, and the GLB node extras.
  - Outputs: records copied verbatim into the derived catalog.
  - Demotion: `ShipAuthoring` is no longer an owner of hull data. `mod-ship:<id>` in the derived catalog is the
    same record as in the mod store.
  - Forbidden writers: compose transforming records; Blender writing hull slots inside `ShipAuthoring`; any
    lookup from hull to visual by key-prefix convention. The typed `Visual` ref is the only binding.
  - Shared paths: `ship-authoring validate`, compose, the Editor preview and the runtime loader all call the one
    `(hull, visual)` validator.
  - Deletion line: after S1, `grep -n "\.Hull\b" ShipAuthoring.cs ShipModCatalog.cs` finds no embedded hull.
- **Verification** (Yggdrasil, `--filter FullyQualifiedName~ShipAuthoring`, then the full suite):
  - `ComposedCatalogHoldsOneHullPerModShip`: the negative for correction 2. It fails today.
  - `HullNamesExactlyOneVisual`: Prefab xor Visual.
  - `HardpointsMustResolveToTheVisualsAnchors`: moved from the old validator.
  - `ComposeRefusesAKeyCollisionAndLeavesThePreviousFile` and `ComposeRefusesAMissingGlbNode`: the gate 3
    negatives, unless S-Soul already added them.
  - `AHullVariantInheritsItsVisual`: runs only once Aetheria is on variants C4. It is the reason for B, and it
    is recorded as pending until then.
  - A Python check, in the Blender-free `python:3.12-slim` image with `pip install msgpack -e <CultLib>/packages/cultcache-py`:
    `ship_cc.replace_layout` on a C#-written fixture preserves every untouched slot, then C# reopens it and it
    validates. This pins gate 1's Python half for the first time.
  - Operator: in Blender, load a layout, change a mount, save, then `ship-authoring validate`.
- **Subtraction estimate:** about 40 removed (the embedded-hull paths and the duplicate upsert), about 35 added
  (`Visual`, the joint validator's plumbing), and about 25 in the Python path.

### S2. The game composes and loads mod ships

- **Repo/branch:** `codex/moddable-ships`, after S1.
- **Deletes first:** none. This cut is additive: it is the construction boundary the doc names at `:141-145`.
- **Adds, per MQ4 and MQ5:**
  - **Compose at boot** (MQ5 = A): `ActionGameManager` opens its catalog at `ActionGameManager.cs:45-60`
    (`b66ba524`). When `GameData/Mods` holds packages, it opens the derived file instead, composed at boot into
    the persistent data path. `capturepreset` keeps writing the shipped catalog (`:605-620`, and the
    `Loadout.cs:74` writable open).
  - **Boot preload:** after the catalog opens and before the first zone loads, every hull with `Visual` set
    imports its GLB through `ShipModVisual.LoadAsync` into an inactive prototype keyed by hull key.
    `LoadEntity` then instantiates synchronously, as it does for prefabs.
  - **Assembly:** a `ShipInstance` template prefab (Addressable) carries the shared effect prototypes: shield,
    tractor, thruster particles, radiator material. The loader parents the GLB under it and fills
    `EntityInstance`'s arrays (`EntityInstance.cs:15-27` @`b66ba524`: `MapIcon`, `Shield`, `HullColliders`,
    `EquipmentHardpoints`, `RadiatorHardpoints`, `ThrusterHardpoints`, `WeaponHardpoints`,
    `ArticulationPoints`, and `ShipInstance.TractorBeam`) from anchors by role. The name matches that
    `EntityInstance.cs:245,260,271` and `ShipInstance.cs:69-70` do against `hp.Transform` keep working, because
    anchor ids are the mount ids.
  - **`ZoneRenderer.LoadEntity`** (`ZoneRenderer.cs:289-310` @`b66ba524`) branches on `hullData.Prefab` or
    `hullData.Visual`. That is one branch point, owned by the hull record.
- **Authority map:**
  - Owner: the hull's `Visual` ref decides the visual. The anchor role decides the component slot.
  - Inputs: the derived catalog snapshot, the mod package directory, and the template prefab.
  - Outputs: a `ShipInstance` indistinguishable to the simulation from a prefab one.
  - Derived state: the prototypes (cache-only, rebuilt each boot) and the derived catalog (disposable).
  - Forbidden writers: prefab arrays hand-wired for a mod ship; any async load during `LoadEntity`; any gameplay
    read of a mod package outside the derived catalog.
  - Shared paths: new game, Continue, zone entry, and wormhole transit all go through `LoadEntity`.
- **Verification:**
  - Yggdrasil: `ComposeAtBootIsDeterministic`. The same packages give byte-identical derived records, which
    pins run-store references across relaunches.
  - `RunReferencingAMissingModRefusesContinue`, per MQ4.
  - Unity batchmode (Self): an Editor play-mode smoke loads a fixture mod ship into a zone and asserts
    `ThrusterHardpoints`, `WeaponHardpoints` and the collider are populated.
  - Operator: none yet (S3 and S4).
- **Subtraction estimate:** 0 removed, about 150 added (loader, template, preload). This is the capability gate 4
  buys.

### S3. Gate 4: a built player

A player build loads a composed mod ship without the Editor or Blender. Only Self (the batchmode build) and the
operator (launch) can do this. It also requires a playable mod ship: a complete package with hardpoints, not the
Quiet probe, which has four anchors and zero hardpoints (`docs/moddable-ship-authoring.md:134-136`). That is
content, and it is the operator's to author or commission.

### S4. Gate 5: play checks

The operator only: spawn, equip, thrust, fire, shield, damage, save and reload, and the schematic UI for the mod
ship. It runs on master's post-fire-control rules, so the checklist's steps 11-15 (arcs, rolled impacts, blasts)
apply to the mod hull too.

### S5. Migration onto variants

After gate 5, and after MQ2 and MQ3.
- Shipped hulls move to `Visual` one at a time, and each migration deletes that hull's `Prefab`.
- Hull families collapse into variants in `Aetheria.cc`. The variants scan found Longinus/LonginusX, differing
  in `Behaviors`, `Hardpoints` and two cosmetic members (`CultLib docs/document-variants-cut.md` §6).
- It waits on variants C4 (Aetheria adoption at `caching-unity-v1.5.0`).
- **Mount ids come first.** The typed lane requires unique, non-empty mount ids (`ShipAuthoring.cs:115-117`
  @`375d6bd4`). No shipped hull has them: Djinni has 3 empty `Transform`s and one duplicate, and Zenith has 5
  hardpoints with one distinct `Transform` (section 0). Each migration first assigns unique mount ids and repoints
  the prefab-era name matches. `Transform` is also not usable as a C2 element id, so C4's minted id and the mount
  id stay two members: one is the identity for variants, and the other names the model node.
- **Deletion line:** when the last hull migrates, `HullData.Prefab` (key 24) retires, and `ShipPrefabAuthoring.cs`
  goes (MQ3).

## B.4 Subtraction ledger (estimate)

| Cut | Removed | Added | Formats, targets, dependencies |
|---|---|---|---|
| landed | 1 | ~511 C#, ~494 Py, 119 test | `aetheria.ship_authoring` document type; glTFast package |
| S1 | ~40 | ~35 C#, ~25 Py | `ShipAuthoring` key 1 retired; `HullData` `Visual` slot added |
| S2 | 0 | ~150 | one template prefab; no new target |
| S5 | per hull: its prefab; finally `ShipPrefabAuthoring.cs` (252) plus its smoke (80) and `HullData.Prefab` | ~0 | FBX builder retired |

The lane is net additive until S5, and S5 is where the old authority dies. Keeping two ship-authoring paths alive
indefinitely is the liability MQ3 exists to close.

## B.5 Operator questions (moddable ships), one fork each

**MQ1. Where does a mod ship's hull live, and how does a hull find its model?** (Blocks S1.)

*Context.*
- Today a mod ship is one `ShipAuthoring` record with the `HullData` embedded inside it.
- Compose writes that hull twice into the derived catalog: once as a `HullData`, and once inside the
  `ShipAuthoring` copy.
- The runtime has no binding yet from a hull to its model; the doc lists it as outstanding.
- You want variants to kill duplicate data entry for ships. A variant is a whole record whose base is another
  record of the same type in the same store.
- "LonginusX is Longinus with another hardpoint layout" is therefore a `HullData` variant. An embedded hull inside
  another document cannot be one.

*Options.*
- **A:** keep the embedded hull. One record per mod, but a hull family must be full copies, and the derived
  catalog keeps a dead second copy.
- **B:** split it. The hull is a plain `HullData` record, and it names its visual through a `Visual` ref, the way
  it names a Unity prefab today. `ShipAuthoring` becomes the visual package only. A hull variant inherits its
  base's visual through normal resolution.
- **C:** split it the other way. `ShipAuthoring` references its hull. Then a hull variant has no visual unless
  something reads the variant's stored base key, and no consumer may read deltas (variants target, "Readers see
  complete documents").

*Recommend B.* It is the only shape where variants reach ships without either copying or delta-reading. It costs
little now, because no committed content uses the old shape.

**MQ2. Can a mod ship be a variant of a shipped hull?** (Blocks S5's plan for mods; does not block S1-S4.)

*Context.*
- Your variants ruling 3 says a variant and its base live in the same store. A mod's `.cc` is its own store, so
  "my Longinus with a turret" cannot name the shipped Longinus as its base.
- Separately, Blender reads mod stores through the Python runtime, and variants ruling 5 has Python refuse any
  store holding a variant until Python resolution is built (it is parked).
- So even a family within one mod's store is blocked in Blender today.

*Options.*
- **A:** no. A mod ships complete hulls, and families inside a mod wait for Python resolution.
- **B:** yes, through compose. Compose would resolve a mod-declared base against the shipped catalog, which is a
  second resolver outside the cache.
- **C:** yes, by reopening ruling 3 for a mod layer.

*Recommend A.* B is exactly the bespoke inheritance the variants campaign exists to prevent. C is a CultLib
decision bigger than this lane.

**MQ3. What is the FBX prefab builder for, now that the typed `.cc` lane exists?** (Blocks S5.)

*Context.*
- `ShipPrefabAuthoring.cs` (252 lines) and its smoke (80 lines) landed on fire-control-12 at `4b594e11`, so they
  reach master in this merge.
- They build Unity prefabs from FBX collections by naming convention (`docs/ship-collection-authoring.md`).
- The typed lane builds the same `ShipInstance` from a `.cc` record and a GLB.
- The ships doc keeps the FBX builder "available to existing content", with retirement "following migration".
- As things stand, the game has two ship-authoring authorities, and no ruling says which one new ships use.

*The question.* Is the FBX builder a permanent path, for example for artists who will never use Blender with
Brokkr? Or is it scaffolding until the typed lane passes gate 5?

*Options.*
- **A:** scaffolding. New ships use the typed lane from gate 5 on, and the builder dies at S5.
- **B:** permanent. Both paths stay, and S2's loader keeps the `Prefab` branch forever.
- **C:** retire it now, and author no new ships until gate 5.

*Recommend A.* Two permanent authoring authorities for one concept is the split this doctrine cuts. C stalls
content for no gain.

**MQ4. What happens to a saved run when a mod it used is removed?** (Blocks S2's Continue path.)

*Context.*
- The run store references designs by key, and a mod hull's key is `mod-hull:<id>`.
- Remove the mod, and those references dangle.
- The fire-control checklist already records that an old run store throws on its first lot lookup
  (`docs/merge-to-master-checklist.md:15`).

*Options.*
- **A:** Continue refuses loudly and names the missing mod ids.
- **B:** the missing ships are stripped from the run on load.
- **C:** the run loads with placeholder hulls.

*Recommend A.* A bad mod is rejected before any scene is built (the doc's own shared-path rule). B silently edits
the player's save.

**MQ5. Who composes the derived catalog?** (Blocks S2.)

*Context.* The doc's own objective is "a mod package can be installed without opening the Unity Editor". Gate 4
says "in a built Unity player without Unity Editor or Blender". A built player ships no `AetherDb`, and today
only `AetherDb ship-authoring compose` composes.

*Options.*
- **A:** the game composes at boot, from `GameData/Mods`, into a disposable file in the persistent data path.
- **B:** an installer or `AetherDb` step composes, and the game opens `Aetheria.modded.cc` when it exists.

*Recommend A.* Compose already lives in `ServerShared` (`ShipModCatalog.cs`), so the game can call it. B makes
"is the derived file stale?" a new failure the player owns.

## B.6 Dependencies (moddable ships)

- **Fire-control-12 on master:** S0.
- **Unity package resolution:** glTFast 6.20.0 needs network on first open (S0 compile).
- **Document variants:**
  - **C2 (element ids):** "ids everywhere" puts an element-id member on `ShipAnchor`, `ShipPolyline`,
    `HardpointData`, `ItemRole` and `BehaviorData` (every object-list element these records reach).
    Consequences I read from the code, not ran:
    - **Hardpoint ids survive a Blender layout save** while the mount id is unchanged. `ship_cc.py:88-98` keeps
      each hardpoint's slots from 7 on, keyed by `Transform`, so an id slot at 7 or later rides along. Renaming a
      mount makes a new element, which is the ruling's own rule ("a moved element is a new element"). A
      hardpoint created in Blender has no id until the next C# write mints one.
    - **Line ids churn on every capture.** `replace_lines` replaces `SchematicLines` whole (`ship_cc.py:51`).
      This is harmless: a variant would override lines whole anyway (variants Q7).
    - **The Python side writes stores C2's registry will then require ids for.** C2 mints them on the first C#
      write, and Python carries the id as an ordinary member (variants Q3 rollout). So mod stores need no
      migration step.
  - **Python refusal:** mod stores stay v1 (no variants) while Blender reads them through `cultcache-py`
    (MQ2 = A).
  - **C3 (Studio):** it edits hull variants in `Aetheria.cc`. It is a prerequisite for S5, not for S1-S4.
  - **C4 (Aetheria adoption, `caching-unity-v1.5.0`):** S5, and the pending test `AHullVariantInheritsItsVisual`.
    Slot coordination: S1 claims `HullData` key 32 for `Visual`, and C4's id slots must not collide with it.
- **Brokkr:** the Blender add-on depends on Brokkr's `brokkr_bridge` preferences for the CultLib Python path
  (`__init__.py:182-186`). It is an operator-machine dependency with no pin in this repo.
