# Engine Asset References Through Addressables: Cut Map

Date: 2026-09-17

Status: Imagination pass, cut map. Nothing here has landed. Anchors are against
`codex/item-provenance` HEAD `f8396313` (settings Cut 0 in progress; none of its commits
touch a file named here, checked with `git diff --name-only f13a3356 f8396313`). Ends are
owned by the ruling in `docs/settings-globals-cut.md` (header); this document owns the means.

Claims marked **(probe)** were run, not read off names:
- a scratch console over `Aetheria.Shared` (sparse scratch worktree of `f13a3356`, CultLib
  `a0813c6` as a detached scratch worktree, both removed afterwards; the catalog was a copy)
  that opened `GameData/Aetheria.cc` through `AetheriaStores.Open` read-only and walked every
  stored document for string members carrying `[CultInspectorAssetPath]`;
- a Python pass resolving each stored path against the main tree and its `.meta` guid, and a
  guid closure over the referenced prefabs' YAML;
- reads of `Library/PackageCache/com.unity.addressables@8460f1c9c927` (2.9.1) source and its
  `Documentation~`.

Operator ruling (2026-09-17): engine asset references from CultCache data are Unity
Addressables, stored as the asset's `.meta` GUID and loaded through Addressables by GUID.
One rule for every engine asset reference from data. No dual Resources/Addressables path.

Rulings: none yet. Open: Q1 (blocks Cut 1).

## 0. Target

Ends:
- A CultCache field that names an engine asset stores that asset's GUID. No stored path.
- One Unity-side loader turns a GUID into an asset. No runtime code calls `Resources.Load`
  for data-named assets, and nothing strips an `Assets/Resources/` prefix.
- Every stored GUID resolves to an addressable asset of the type its consumer loads, and a
  check proves it.

Invariants kept:
- ServerShared stays engine-free (`Aetheria.Shared.csproj` builds it without Unity), so the
  data carries a string, never a Unity type.
- Catalog globals are authored, never invented; the migration rewrites values in existing
  records only.
- One home store per type, one-store commits.

## 1. Census

### 1.1 Stored references **(probe)**

8 attributed members exist (`git grep CultInspectorAssetPath`). No other engine asset string
exists in ServerShared: the only other `*Path` strings are `InputLayout.cs:68,92` (input
control paths) and `WwiseMetadata.cs:37,91` (Wwise object paths), neither a Unity asset.

| Member (`file:line`) | Stored | Non-empty | Resolves | Dangling | Runtime reader |
|---|---|---|---|---|---|
| `ConsumableItemData.Icon` (`ItemData.cs:352`) | 0 | 0 | 0 | 0 | `ActionBarSlot.cs:114` |
| `EquippableItemData.Schematic` (`ItemData.cs:362`) | 51 | 1 | 1 | 0 | editor only, `Editor/CultCacheDrawers.cs:104` |
| `EquippableItemData.ActionBarIcon` (`ItemData.cs:392`) | 51 | 0 | 0 | 0 | `ActionBarSlot.cs:153` |
| `HullData.Prefab` (`ItemData.cs:499`) | 3 | 3 | 3 | 0 | `ZoneRenderer.cs:295,304` |
| `WeaponData.EffectPrefab` (`Behaviors/Weapon.cs:43`) | 18 | 18 | 18 | 0 | `EntityInstance.cs:195,224` |
| `ThrusterData.ParticlesPrefab` (`Behaviors/Thruster.cs:26`) | 2 | 2 | 2 | 0 | `ShipInstance.cs:67` |
| `AetherDriveData.Particles` (`Behaviors/AetherDrive.cs:50`) | 1 | 1 | 1 | 0 | `ShipInstance.cs:53` |
| `Faction.Logo` (`Corporations.cs:25`) | 12 | 5 | 5 | 0 | none |

Totals: 138 stored values in 180 documents, 30 non-empty, 27 distinct paths, all 30 resolve
to an existing file with a `.meta` guid, **0 dangling**. Every one of the 27 distinct paths is
under `Assets/Resources/`: 21 prefabs under `Prefabs/`, 1 png under `Schematics/`, 5 pngs under
`Sprites/Icons/{Logos,Tech}` (3 paths are shared by two records each).
The value-by-value list (record, member, path, guid) is the migration's expected report;
Cut 2 regenerates it rather than trusting this copy.

### 1.2 Load sites (all synchronous, all on the main thread)

| Site | Loads | Context |
|---|---|---|
| `Gameplay/ActionBarSlot.cs:114` | `Resources.Load<Texture2D>` with inline prefix strip | binding ctor |
| `Gameplay/ActionBarSlot.cs:153` | same | binding ctor |
| `Gameplay/EntityInstance.cs:195` | `UnityHelpers.LoadAsset<InstantWeaponEffectManager>` | `SetEntity`, then `Instantiate` |
| `Gameplay/EntityInstance.cs:224` | `UnityHelpers.LoadAsset<ConstantWeaponEffectManager>` | same |
| `Gameplay/ShipInstance.cs:53` | `UnityHelpers.LoadAsset<ParticleSystem>` | same, `Instantiate` |
| `Gameplay/ShipInstance.cs:67` | same | LINQ select, `Instantiate` |
| `Zone Display/ZoneRenderer.cs:295,304` | `UnityHelpers.LoadAsset<GameObject>` | `LoadEntity`, `Instantiate` |
| `Editor/CultCacheDrawers.cs:104` | `AssetDatabase.LoadAssetAtPath<Texture2D>` | Studio drawer |

`UnityHelpers.cs:7-10` is the only helper and has no other caller. Four sites load a
**component** type. Addressables cannot do that: "You can't load a component of a GameObject
directly through Addressables" (`Documentation~/load-assets.md:60`). The loader must load the
GameObject and take the component.

### 1.3 `Resources.Load` not tied to data

One: `Gameplay/ActionGameManager.cs:420` (`:425` before Cut 0) loads
`Sprites/Input/{controlName}` by naming convention. No data names it; it is outside the ruling.
**Default: out of scope**, and `Assets/Resources/Sprites/Input/` stays in Resources. It goes to
follow-ups: if Resources is ever emptied, this becomes a player-store or catalog map of control
name to GUID.

### 1.4 Resources dependencies of the referenced assets **(probe)**

The guid closure of the 27 referenced assets is 145 assets; 40 sit in `Assets/Resources`: the
27 seeds, plus 13 dependencies. 8 of those are inside the subtrees Cut 2 moves (`Prefabs/`
top-level Influence, Lightning, PingEffect, Shield, Tractor Beam; `Sprites/Icons/Stroked/`
Satellite, Ship, orbital). 5 stay behind: `Gradients/Ironbow.png` (written there by
`GradientMapper.cs:94-96`), `Sprites/Mote.png`, `Sprites/ringThick.png`,
`Sun Albedo/sunsurface1map 1.png`, `Sun Flow/sunsurface1offset 1.png`. Nothing loads them by
name; left in Resources they ship twice (Resources data and the bundle). That is size, not
correctness. **Default: follow-up**, reported by the Addressables Analyze rule "Check Resources
to Addressable Duplicate Dependencies" (`Editor/Build/AnalyzeRules/CheckResourcesDupeDependencies.cs`).

## 2. Ownership decisions

### 2.1 The picker and the attribute belong to CultLib

`[CultInspectorAssetPath]` is CultLib's (`src/GameCult.Caching/CultInspectorAttributes.cs:65-77`),
read into `CultInspectorMetadata.AssetPath` (`CultInspectorModel.cs:44,56`) and drawn by CultLib's
Studio, not by Aetheria: `src/GameCult.Unity/Assets/Caching/Editor/CultCacheStudioDrawers.cs:168-176`
turns an `ObjectField` into `AssetDatabase.GetAssetPath`. Aetheria's
`Assets/Scripts/Editor/CultCacheDrawers.cs` claims only its own attributes. No CultLib path
under `src/GameCult.Caching`, `src/GameCult.Unity` or `unity/` changed between `a0813c6` and
CultLib `main` `244154e`. Aetheria is the only consumer in `F:\Projects` (`git grep` over every
repo; the other hits are CultLib's README and voidbot memory dumps).

Decision: the owner fills the gap. CultLib replaces the path attribute with
`CultInspectorAssetGuidAttribute(Type? assetType = null)`, "a string member holding an engine
asset's stable identity; in Unity, the `.meta` GUID", and its Studio drawer stores
`AssetDatabase.AssetPathToGUID` and reads back through `GUIDToAssetPath`. Reusing the name
`AssetPath` for GUIDs would make the attribute lie, and keeping both leaves a dead path form
for the next field to pick up. Addressables stays out of CultLib: whether an asset is
addressable is Aetheria's content rule (2.3), so the Studio package gains no Addressables
dependency. `AssetType` stays optional; Aetheria cannot pass it (ServerShared is engine-free,
recorded in `docs/cultcache-migration-cut.md:2003`), so the Studio falls back to `Object`.

Cost: a CultLib release (plugins rebuilt with `scripts/build-unity-package.ps1`), tags
`cultlib-unity-v1.0.60` and `caching-unity-v1.4.0` (removing a public attribute is breaking),
and an Aetheria bump of `Packages/manifest.json` (both URLs), `Packages/packages-lock.json`
and `Directory.Build.props` `CultLibRevision`. Coordination: CultLib `codex/studio-grouping`
(mapped, blocked on the Aetheria lot schema) also edits `CultInspectorModel.cs` and the Studio
drawers; whichever releases second rebases onto the first release.

### 2.2 Addressables package **(probe)**

- `com.unity.addressables` **2.9.1** is already resolved and compiled, at depth 1, pulled by
  `com.unity.localization` 1.5.8 (`Packages/packages-lock.json:18-33,117-126`). Its
  `package.json` needs Unity 2023.1+; the project is `6000.3.24f1`. `Library/ScriptAssemblies`
  holds `Unity.Addressables.dll`, `Unity.Addressables.Editor.dll`, `Unity.ResourceManager.dll`.
  No script uses Localization and no Localization settings exist (grep of `Assets`,
  `ProjectSettings`); that is a follow-up, not this cut.
- No settings exist yet: no `Assets/AddressableAssetsData`, no `com.unity.addressableassets`
  in `ProjectSettings/EditorBuildSettings.asset` `m_configObjects`.
- Creating settings (`AddressableAssetSettings.Create`, `:2023-2056`, and `Validate`,
  `:1946-1976`) writes: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`,
  `DefaultObject.asset`, `AssetGroups/Default Local Group.asset` with
  `AssetGroups/Schemas/Default Local Group_BundledAssetGroupSchema.asset` and
  `_ContentUpdateGroupSchema.asset`, `AssetGroupTemplates/Packed Assets.asset`,
  `DataBuilders/BuildScriptFastMode.asset`, `BuildScriptPackedPlayMode.asset`,
  `BuildScriptPackedMode.asset`, each with a `.meta`, plus the config object entry in
  `EditorBuildSettings.asset`. It sets `BuildAddressablesWithPlayerBuild = PreferencesValue`,
  a per-user preference.
- **GUID keys.** `BundledAssetGroupSchema.IncludeGUIDInCatalog` defaults to `true`
  (`BundledAssetGroupSchema.cs:155`); the player catalog then adds each entry's guid as a key
  (`AddressableAssetEntry.CreateKeyList`, `:254-261`). Folder entries expand into sub-entries
  carrying their own guid (`AddressableAssetEntry.cs:554`). In editor play mode ("Use Asset
  Database"), `AddressableAssetSettingsLocator` keys direct entries by guid (`:155`) and resolves
  a guid under an addressable folder entry by walking parent folders (`:262-283`). A guid that is
  not addressable yields no location and the load fails. So `LoadAssetAsync<T>(guid)` works in
  editor and player for direct and folder entries, and fails for non-addressable assets.
- **Resources.** An asset under a `Resources` folder cannot be an entry: the Inspector and Groups
  window move it to `Resources_moved` (`AddressableAssetUtility.SafeMoveResourcesToGroup`,
  `:309-339`), and moving an entry into Resources unmarks it (`AddressableAssetSettings.cs:2900-2906`).
  The programmatic `CreateOrMoveEntry` (`:2541`) does not check, so a script could create an
  entry the GUI would refuse; the cut must move assets out of Resources rather than rely on that.
  Moving a file together with its `.meta` keeps the guid, so stored GUIDs survive the move.
- **Sync.** `AsyncOperationHandle.WaitForCompletion` (`AsyncOperationHandle.cs:178`) completes a
  local load in place; `SynchronousAddressables.md:21` puts the cost near `Resources.Load` for
  local bundles and warns that it completes every pending load.
- **Player build.** Bundles build with the player only when `BuildAddressablesWithPlayerBuild`
  resolves to build (`build-player-builds.md:5`). Cut 1 sets `BuildWithPlayer` in the project
  settings so the rule is committed, not a preference.

### 2.3 How an asset becomes addressable (Q1)

Recommended (Q1 A): one folder entry, `Assets/Content`, in `Default Local Group`. Addressable
means "lives under `Assets/Content`". Cut 2 moves `Assets/Resources/Prefabs/`,
`Assets/Resources/Schematics/` and `Assets/Resources/Sprites/Icons/` there with their `.meta`
files, keeping relative paths. `Sprites/Icons/` includes the 33 settings icons settings Cut 1
will reference, so that cut only stores GUIDs. The 7 `CelestialBodySettings` presets under
`Assets/Plugins/Celestial Body/Solar System/` move into `Assets/Content/Celestial Bodies/` in
settings Cut 1 (that map's old fork B(a) move, retargeted).

### 2.4 Loading owner

`Assets/Scripts/EngineAssets.cs` (Assembly-CSharp, replaces `UnityHelpers.cs`), about 30 lines:
- `public static T Load<T>(string guid) where T : Object`. Empty GUID returns `null`. A
  `Component` type loads the GameObject and returns `GetComponent<T>()`.
- Loads with `Addressables.LoadAssetAsync<T>(guid).WaitForCompletion()`. All call sites are
  synchronous construction paths that `Instantiate` at once. Making them async would reorder
  entity construction for 30 local assets. **Default: sync.**
- Caches one handle per `(guid, type)` for the process lifetime and never releases, which is
  the lifetime `Resources.Load` has today. It owns every handle; no caller holds or releases one.
  A failed handle logs the GUID and returns `null`; callers keep their existing null handling.

### 2.5 Validation owner

`Assets/Scripts/Editor/EngineAssetCheck.cs` (Assembly-CSharp-Editor). It needs `AssetDatabase`,
the Addressables editor settings and the consumer component types, so it cannot be a headless
`AetherDb` command or a `Tests.asmdef` test (that assembly cannot reference Assembly-CSharp).
It has a menu item and a batchmode entry (`-executeMethod EngineAssetCheck.Run`, exit 1 on
failure). It opens the catalog read-only through `AetheriaStores.Open`, walks every stored
document for `[CultInspectorAssetGuid]` string members, and for each non-empty value checks:
1. it is 32 lowercase hex characters;
2. `AssetDatabase.GUIDToAssetPath` names an existing file;
3. `AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid, includeImplicit: true)` is non-null;
4. the asset satisfies its member's consumer contract, from one table with one row per member:
   `Icon`, `ActionBarIcon`, `Schematic`, `Logo` are `Texture2D`; `HullData.Prefab` is a
   GameObject with `EntityInstance`; `WeaponData.EffectPrefab` has `InstantWeaponEffectManager`
   on `InstantWeaponData` records and `ConstantWeaponEffectManager` on `ConstantWeaponData`
   records; `ThrusterData.ParticlesPrefab` and `AetherDriveData.Particles` have `ParticleSystem`.
   An attributed member missing from the table fails the check, so a new field cannot skip it.

It reports record name, member, GUID and path per failure. The check does not write, and no
repair path exists.

## 3. Data migration (Cut 2, First)

- Scratch-only program over `Aetheria.Shared` at the pre-Cut-2 commit (the fork K (a)
  precedent: no landed bypass, no one-shot command left behind). It opens a **copy** of the
  catalog writable, walks every stored document for `[CultInspectorAssetPath]` string members,
  and maps each non-empty path to the `guid:` of `<repo>/<path>.meta`. It writes through the
  catalog's own types and commits once.
- Report, one line per value: record type, record name, member, old path, GUID. Unresolved
  paths are listed and **fail the run with nothing written**; none are expected (1.1). Empty
  values stay empty.
- No record is added or removed, and no global is touched. Member names, keys and `JsonProperty`
  names are unchanged: the member still names the asset, and the attribute says the form.
- The rewritten `GameData/Aetheria.cc` (LFS) lands in the same commit as the code. A commit
  that holds GUIDs with path-loading code, or paths with GUID-loading code, loads nothing.
- Before/after: `AetherDb census` and `dangling` outputs are byte-identical (same capture
  method both sides); the report has 30 lines; a scratch re-read finds 30 GUIDs, 108 empty
  strings and no string starting with `Assets/`.

## 4. Cuts

### Cut 0. CultLib: asset GUID attribute and Studio picker

- **Repo/branch:** CultLib, a `codex/` branch from `main` `244154e`. Releases before Cut 2.
- **Deletes first:** `CultInspectorAssetPathAttribute` (`src/GameCult.Caching/CultInspectorAttributes.cs:65-77`);
  `CultInspectorMetadata.AssetPath` (`CultInspectorModel.cs:44,56`); the path branch in
  `CultCacheStudioDrawers.cs:170-176`.
- **Adds:** `CultInspectorAssetGuidAttribute(Type? assetType = null)` with the identity comment;
  `CultInspectorMetadata.AssetGuid`; the drawer branch: `GUIDToAssetPath` then
  `LoadAssetAtPath(path, assetType)`, then `ObjectField`, storing `AssetPathToGUID(next)` or
  `string.Empty`. README `src/GameCult.Unity/Assets/Caching/README.md:153,164`.
- **Release:** rebuild plugins, bump `unity/org.gamecult.cultlib/package.json` to 1.0.60 and
  `src/GameCult.Unity/Assets/Caching/package.json` to 1.4.0 (depending on cultlib 1.0.60), tag both.
- **Verification:**
  - builds: `GameCult.Caching`, the CultLib test project that covers `CultInspectorModel`, the
    Unity package build script.
  - tests: a model test pins that a member carrying the GUID attribute exposes `AssetGuid` and
    its `AssetType`.
  - negative: `git grep -n "AssetPath" -- src unity` finds nothing (checked: today the only hits
    are the three files above and the README).
  - operator (Unity, after Cut 2's bump): pick a prefab in Studio, the stored value is its GUID,
    and reopening shows the same asset.

### Cut 1. Aetheria: Addressables settings

- **Repo/branch:** Aetheria `codex/item-provenance`, after settings Cut 0 lands. Independent of Cut 0.
- **First:** Unity closed (operator), because batchmode refuses an open project.
- **Adds:** `"com.unity.addressables": "2.9.1"` as a direct dependency in `Packages/manifest.json`,
  since Aetheria code now uses it; the lock entry goes from depth 1 to 0. Settings created by a
  scratch-only batchmode `-executeMethod` script, which is not landed: `Create`, then set
  `BuildAddressablesWithPlayerBuild = BuildWithPlayer`, create `Assets/Content/` (+`.meta`) and
  `CreateOrMoveEntry(guid of Assets/Content, DefaultGroup)`. Operator alternative: *Window >
  Asset Management > Addressables > Groups > Create Addressables Settings*, then the same three
  edits by hand.
- **Intentional Unity churn (the whole list):** `Packages/manifest.json`,
  `Packages/packages-lock.json`, `ProjectSettings/EditorBuildSettings.asset` (one
  `com.unity.addressableassets` config object), `Assets/Content.meta`, and under
  `Assets/AddressableAssetsData/`: `AddressableAssetSettings.asset`, `DefaultObject.asset`,
  `AssetGroups/Default Local Group.asset`, `AssetGroups/Schemas/Default Local Group_BundledAssetGroupSchema.asset`,
  `AssetGroups/Schemas/Default Local Group_ContentUpdateGroupSchema.asset`,
  `AssetGroupTemplates/Packed Assets.asset`, `DataBuilders/BuildScriptFastMode.asset`,
  `DataBuilders/BuildScriptPackedPlayMode.asset`, `DataBuilders/BuildScriptPackedMode.asset`,
  and every folder and file `.meta`. Nothing else: the modified materials and render textures
  in the worktree today are pre-existing Unity churn and stay unstaged.
- **Harmless alone:** no code loads through Addressables yet, and `Assets/Content` is empty.
- **Verification:**
  - operator: Unity opens without Addressables errors; the Groups window shows `Assets/Content`
    under `Default Local Group`; the ARPG play smoke is unchanged.
  - negative: `git show --stat` lists only the files above.

### Cut 2. Aetheria: switch every data asset reference to GUIDs

One commit, or a series whose intermediate commits Soul accepts as unplayable. See section 3
for why it is atomic.

- **Depends on:** Cuts 0 and 1, and Q1.
- **First:** the migration report (section 3) against the pre-cut catalog; `AetherDb census` and
  `dangling` captures; `git grep -n "Assets/Resources/" -- Assets/Scripts` capture.
- **Deletes first:**
  - `Assets/Scripts/UnityHelpers.cs` (+`.meta`), 11 lines.
  - `Gameplay/ActionBarSlot.cs:114`, `:153`: the inline `Substring("Assets/Resources/".Length).Split('.').First()`
    loads (and `using System.Linq` if nothing else uses it).
  - All 8 `CultInspectorAssetPath` usages (`ItemData.cs:352,362,392,499`, `Weapon.cs:43`,
    `Thruster.cs:26`, `AetherDrive.cs:50`, `Corporations.cs:25`) become `CultInspectorAssetGuid`.
- **Moves (git mv, file and `.meta` together, guids unchanged):** `Assets/Resources/Prefabs/` to
  `Assets/Content/Prefabs/`, `Assets/Resources/Schematics/` to `Assets/Content/Schematics/`,
  `Assets/Resources/Sprites/Icons/` to `Assets/Content/Sprites/Icons/`, plus their folder `.meta` files.
- **Adds:** `Assets/Scripts/EngineAssets.cs` (2.4); `Assets/Scripts/Editor/EngineAssetCheck.cs` (2.5);
  the migrated `GameData/Aetheria.cc`; the CultLib bump (manifest URLs `#cultlib-unity-v1.0.60`
  and `#caching-unity-v1.4.0`, lock, `Directory.Build.props:5` `CultLibRevision`).
- **Per-file changes (against `f8396313`):**
  - `ActionBarSlot.cs:114`, `:153`: `EngineAssets.Load<Texture2D>(...)`.
  - `EntityInstance.cs:195`, `:224`: `EngineAssets.Load<InstantWeaponEffectManager>` /
    `<ConstantWeaponEffectManager>(data.EffectPrefab)`; the error text at `:200`, `:229` says GUID, not path.
  - `ShipInstance.cs:53`, `:67`: `EngineAssets.Load<ParticleSystem>`.
  - `ZoneRenderer.cs:295`, `:304`: `EngineAssets.Load<GameObject>`.
  - `Editor/CultCacheDrawers.cs:104`: `LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(item.Schematic))`.
- **Authority map:**
  - Owner: the catalog field holds the GUID. `EngineAssets` owns turning a GUID into a loaded
    asset and holding its handle. The `Assets/Content` folder entry owns addressability.
  - Inputs: GUID strings from catalog records; the Addressables catalog (editor locator or player catalog).
  - Outputs: loaded `Object`s or components, or `null` with a logged GUID.
  - Derived state: asset paths are display-only (Studio, check reports). Group entries are
    derived from folder location.
  - Forbidden writers: `Resources.Load` for data-named assets; any path-prefix stripping; any
    runtime or editor code that writes a path into an asset member; per-asset entries created by
    scripts outside `Assets/Content`.
  - Shared paths: game runtime, editor play mode and player builds all load through
    `EngineAssets` and the same GUID keys. Studio authoring and the migration both store GUIDs.
    The check reads through the same `AetheriaStores.Open`.
  - Deletion line: `UnityHelpers.cs`, the two inline strips, and the path attribute are gone
    before `EngineAssets` exists.
- **Verification:**
  - builds: `Aetheria.Shared` headless with `-p:CultLibRoot=<CultLib 1.0.60 scratch worktree>`;
    `tests/Aetheria.Shared.Tests`; Unity batchmode compile.
  - tests: `EngineAssetCheck.Run` in batchmode exits 0 on the migrated catalog. Mutation: on a
    scratch catalog copy, set one `HullData.Prefab` to a Texture2D GUID, one to a GUID outside
    `Assets/Content`, one to a random 32-hex, and one to an old path. Each must fail with its
    record named. Also add a scratch attributed member missing from the table; it must fail.
  - data: section 3 before/after checks.
  - negative (checked against `f8396313` for collisions; each has hits only at the sites above today):
    - `git grep -n "Resources.Load" -- Assets/Scripts` gives exactly `ActionGameManager.cs:420`.
    - `git grep -n "Assets/Resources/" -- '*.cs'` gives only the generated comments at
      `AetheriaInput.cs:5,19`, `GradientMapper.cs:94,96`, and `tools/AetherDb/AuthoredSettings.cs:13`
      until settings Cut 1 deletes that file.
    - `git grep -n "UnityHelpers\|CultInspectorAssetPath"` gives nothing outside `docs/`.
    - `git ls-files Assets/Resources/Prefabs Assets/Resources/Schematics Assets/Resources/Sprites/Icons` is empty.
  - operator (Unity):
    1. Addressables Play Mode Script "Use Asset Database": ARPG play smoke. Ships, turret and
       Zenith station spawn; thruster and aether drive particles show; fire an instant weapon
       (e.g. Autocannon) and a charged one (ChargeBlast SG); the action bar shows icons where
       the settings fallback applies.
    2. Play Mode Script "Use Existing Build" after *Build > New Build > Default Build Script*:
       the same smoke. This proves the player catalog carries GUID keys.
    3. Addressables *Analyze*: run "Check Resources to Addressable Duplicate Dependencies" and
       record the result. The 5 known leftovers (1.4) are expected.
    4. Studio: `LonginusX` schematic still underlays the shape drawer.

## 5. Subtraction ledger (estimate)

| Cut | Removed | Added | Dependencies, formats, targets |
|---|---|---|---|
| 0 | path attribute ~12, metadata 2, drawer branch 6 | GUID attribute ~12, metadata 2, drawer branch ~7, test ~15 | CultLib release; no targets |
| 1 | none | Addressables settings assets (Unity YAML, ~12 files) | `com.unity.addressables` becomes direct (already installed); Addressables build step joins player builds |
| 2 | `UnityHelpers.cs` 11, 2 inline strips, 283 assets (584 files with `.meta`) leave `Assets/Resources` (moved, not deleted) | `EngineAssets.cs` ~30, `EngineAssetCheck.cs` ~80, 30 catalog values rewritten | removes the Resources path convention for data; no targets |

Net code is positive (~+110). It buys the ruled capability (GUID identity that survives moves,
and one loader) and a check that did not exist: today a mistyped path fails only at spawn.

## 6. Operator forks

**Q1. How assets become addressable.** Blocks Cut 1.
- (A) One folder entry `Assets/Content`. Addressable means under that folder. Cut 2 moves
  `Prefabs/`, `Schematics/` and `Sprites/Icons/` out of Resources into it, and settings Cut 1
  moves the 7 body presets there. New content is addressable by placement, and the group file
  does not change per asset. Unreferenced assets in those folders ship too, as they do from
  Resources today.
- (B) One entry per referenced asset, assets kept in place, except that the 27 in Resources must
  still move (2.2). Only referenced assets ship. Each new pick needs a manual *Addressable* tick
  (the check catches a miss), and the group asset changes with every new reference.
- (C) Per-asset entries generated from the catalog by an editor step before play and build. The
  group becomes derived state, but a sync step runs at play and build boundaries, and a stale
  group between steps fails loads in play mode.
- **Recommend (A).** It makes the invariant structural (location) with no per-asset bookkeeping
  and no sync step, and it matches how Resources behaves today.

## Follow-ups outside this migration

- `ActionGameManager.cs:420` loads input prompt sprites from `Resources/Sprites/Input` by control
  name (1.3).
- 5 Resources textures referenced by moved prefabs stay in Resources and ship twice (1.4).
- `com.unity.localization` 1.5.8 is a direct dependency with no script use and no settings; it
  was the only reason Addressables was installed.
- `Faction.Logo` (5 values) has no reader; `EquippableItemData.ActionBarIcon` and
  `ConsumableItemData.Icon` hold no values (1.1).
- Doc sweep for Self: `docs/settings-globals-cut.md` still carries path-based text in 2.1
  (`:238`, "Engine asset paths (forks I and B...)"), 3.2 (`:328`), Cut 1, and forks I and B
  (`:522-541`). Those become history once this map is ruled. Settings Cut 1's icon and preset
  fields use `[CultInspectorAssetGuid]` and `EngineAssets`.
