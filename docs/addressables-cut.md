|.AssetPathb"# Engine Asset References Through Addressables: Cut Map
|.AssetPathb"
|.AssetPathb"Date: 2026-09-17
|.AssetPathb"
|.AssetPathb"Status: Imagination pass, cut map. Nothing here has landed. Anchors are against
|.AssetPathb"`codex/item-provenance` HEAD `f8396313` (settings Cut 0 in progress; none of its commits
|.AssetPathb"touch a file named here, checked with `git diff --name-only f13a3356 f8396313`). Ends are
|.AssetPathb"owned by the ruling in `docs/settings-globals-cut.md` (header); this document owns the means.
|.AssetPathb"
|.AssetPathb"Claims marked **(probe)** were run, not read off names:
|.AssetPathb"- a scratch console over `Aetheria.Shared` (sparse scratch worktree of `f13a3356`, CultLib
|.AssetPathb"  `a0813c6` as a detached scratch worktree, both removed afterwards; the catalog was a copy)
|.AssetPathb"  that opened `GameData/Aetheria.cc` through `AetheriaStores.Open` read-only and walked every
|.AssetPathb"  stored document for string members carrying `[CultInspectorAssetPath]`;
|.AssetPathb"- a Python pass resolving each stored path against the main tree and its `.meta` guid, and a
|.AssetPathb"  guid closure over the referenced prefabs' YAML;
|.AssetPathb"- reads of `Library/PackageCache/com.unity.addressables@8460f1c9c927` (2.9.1) source and its
|.AssetPathb"  `Documentation~`.
|.AssetPathb"
|.AssetPathb"Operator ruling (2026-09-17): engine asset references from CultCache data are Unity
|.AssetPathb"Addressables, stored as the asset's `.meta` GUID and loaded through Addressables by GUID.
|.AssetPathb"One rule for every engine asset reference from data. No dual Resources/Addressables path.
|.AssetPathb"
|.AssetPathb"Rulings: Q1 (A), one `Assets/Content` folder entry (operator, 2026-09-17: "I take your recommendation"). Open: none.
|.AssetPathb"Ruling S (A), sub-assets (operator, 2026-09-17: "A"): a stored reference is the Addressables key,
|.AssetPathb"the bare GUID for a main asset and `guid[subAssetName]` when a sub-asset (e.g. one sprite of a
|.AssetPathb"sheet) is picked. It is what `AssetReference` keeps (GUID plus sub-object name), and
|.AssetPathb"Addressables loads both forms. The drawer writes and reads both; `EngineAssets.Load<T>` passes the
|.AssetPathb"key through. Found by Soul on CultLib `0155ba8`, before release.
|.AssetPathb"Correction: Cut 0's negative grep `AssetPath` also matches Unity's `GUIDToAssetPath`/
|.AssetPathb"`AssetPathToGUID`; the check is `git grep -n "CultInspectorAssetPath\|.AssetPath\b"`.
|.AssetPathb"
|.AssetPathb"## 0. Target
|.AssetPathb"
|.AssetPathb"Ends:
|.AssetPathb"- A CultCache field that names an engine asset stores that asset's GUID. No stored path.
|.AssetPathb"- One Unity-side loader turns a GUID into an asset. No runtime code calls `Resources.Load`
|.AssetPathb"  for data-named assets, and nothing strips an `Assets/Resources/` prefix.
|.AssetPathb"- Every stored GUID resolves to an addressable asset of the type its consumer loads, and a
|.AssetPathb"  check proves it.
|.AssetPathb"
|.AssetPathb"Invariants kept:
|.AssetPathb"- ServerShared stays engine-free (`Aetheria.Shared.csproj` builds it without Unity), so the
|.AssetPathb"  data carries a string, never a Unity type.
|.AssetPathb"- Catalog globals are authored, never invented; the migration rewrites values in existing
|.AssetPathb"  records only.
|.AssetPathb"- One home store per type, one-store commits.
|.AssetPathb"
|.AssetPathb"## 1. Census
|.AssetPathb"
|.AssetPathb"### 1.1 Stored references **(probe)**
|.AssetPathb"
|.AssetPathb"8 attributed members exist (`git grep CultInspectorAssetPath`). No other engine asset string
|.AssetPathb"exists in ServerShared: the only other `*Path` strings are `InputLayout.cs:68,92` (input
|.AssetPathb"control paths) and `WwiseMetadata.cs:37,91` (Wwise object paths), neither a Unity asset.
|.AssetPathb"
|.AssetPathb"| Member (`file:line`) | Stored | Non-empty | Resolves | Dangling | Runtime reader |
|.AssetPathb"|---|---|---|---|---|---|
|.AssetPathb"| `ConsumableItemData.Icon` (`ItemData.cs:352`) | 0 | 0 | 0 | 0 | `ActionBarSlot.cs:114` |
|.AssetPathb"| `EquippableItemData.Schematic` (`ItemData.cs:362`) | 51 | 1 | 1 | 0 | editor only, `Editor/CultCacheDrawers.cs:104` |
|.AssetPathb"| `EquippableItemData.ActionBarIcon` (`ItemData.cs:392`) | 51 | 0 | 0 | 0 | `ActionBarSlot.cs:153` |
|.AssetPathb"| `HullData.Prefab` (`ItemData.cs:499`) | 3 | 3 | 3 | 0 | `ZoneRenderer.cs:295,304` |
|.AssetPathb"| `WeaponData.EffectPrefab` (`Behaviors/Weapon.cs:43`) | 18 | 18 | 18 | 0 | `EntityInstance.cs:195,224` |
|.AssetPathb"| `ThrusterData.ParticlesPrefab` (`Behaviors/Thruster.cs:26`) | 2 | 2 | 2 | 0 | `ShipInstance.cs:67` |
|.AssetPathb"| `AetherDriveData.Particles` (`Behaviors/AetherDrive.cs:50`) | 1 | 1 | 1 | 0 | `ShipInstance.cs:53` |
|.AssetPathb"| `Faction.Logo` (`Corporations.cs:25`) | 12 | 5 | 5 | 0 | none |
|.AssetPathb"
|.AssetPathb"Totals: 138 stored values in 180 documents, 30 non-empty, 27 distinct paths, all 30 resolve
|.AssetPathb"to an existing file with a `.meta` guid, **0 dangling**. Every one of the 27 distinct paths is
|.AssetPathb"under `Assets/Resources/`: 21 prefabs under `Prefabs/`, 1 png under `Schematics/`, 5 pngs under
|.AssetPathb"`Sprites/Icons/{Logos,Tech}` (3 paths are shared by two records each).
|.AssetPathb"The value-by-value list (record, member, path, guid) is the migration's expected report;
|.AssetPathb"Cut 2 regenerates it rather than trusting this copy.
|.AssetPathb"
|.AssetPathb"### 1.2 Load sites (all synchronous, all on the main thread)
|.AssetPathb"
|.AssetPathb"| Site | Loads | Context |
|.AssetPathb"|---|---|---|
|.AssetPathb"| `Gameplay/ActionBarSlot.cs:114` | `Resources.Load<Texture2D>` with inline prefix strip | binding ctor |
|.AssetPathb"| `Gameplay/ActionBarSlot.cs:153` | same | binding ctor |
|.AssetPathb"| `Gameplay/EntityInstance.cs:195` | `UnityHelpers.LoadAsset<InstantWeaponEffectManager>` | `SetEntity`, then `Instantiate` |
|.AssetPathb"| `Gameplay/EntityInstance.cs:224` | `UnityHelpers.LoadAsset<ConstantWeaponEffectManager>` | same |
|.AssetPathb"| `Gameplay/ShipInstance.cs:53` | `UnityHelpers.LoadAsset<ParticleSystem>` | same, `Instantiate` |
|.AssetPathb"| `Gameplay/ShipInstance.cs:67` | same | LINQ select, `Instantiate` |
|.AssetPathb"| `Zone Display/ZoneRenderer.cs:295,304` | `UnityHelpers.LoadAsset<GameObject>` | `LoadEntity`, `Instantiate` |
|.AssetPathb"| `Editor/CultCacheDrawers.cs:104` | `AssetDatabase.LoadAssetAtPath<Texture2D>` | Studio drawer |
|.AssetPathb"
|.AssetPathb"`UnityHelpers.cs:7-10` is the only helper and has no other caller. Four sites load a
|.AssetPathb"**component** type. Addressables cannot do that: "You can't load a component of a GameObject
|.AssetPathb"directly through Addressables" (`Documentation~/load-assets.md:60`). The loader must load the
|.AssetPathb"GameObject and take the component.
|.AssetPathb"
|.AssetPathb"### 1.3 `Resources.Load` not tied to data
|.AssetPathb"
|.AssetPathb"One: `Gameplay/ActionGameManager.cs:420` (`:425` before Cut 0) loads
|.AssetPathb"`Sprites/Input/{controlName}` by naming convention. No data names it; it is outside the ruling.
|.AssetPathb"**Default: out of scope**, and `Assets/Resources/Sprites/Input/` stays in Resources. It goes to
|.AssetPathb"follow-ups: if Resources is ever emptied, this becomes a player-store or catalog map of control
|.AssetPathb"name to GUID.
|.AssetPathb"
|.AssetPathb"### 1.4 Resources dependencies of the referenced assets **(probe)**
|.AssetPathb"
|.AssetPathb"The guid closure of the 27 referenced assets is 145 assets; 40 sit in `Assets/Resources`: the
|.AssetPathb"27 seeds, plus 13 dependencies. 8 of those are inside the subtrees Cut 2 moves (`Prefabs/`
|.AssetPathb"top-level Influence, Lightning, PingEffect, Shield, Tractor Beam; `Sprites/Icons/Stroked/`
|.AssetPathb"Satellite, Ship, orbital). 5 stay behind: `Gradients/Ironbow.png` (written there by
|.AssetPathb"`GradientMapper.cs:94-96`), `Sprites/Mote.png`, `Sprites/ringThick.png`,
|.AssetPathb"`Sun Albedo/sunsurface1map 1.png`, `Sun Flow/sunsurface1offset 1.png`. Nothing loads them by
|.AssetPathb"name; left in Resources they ship twice (Resources data and the bundle). That is size, not
|.AssetPathb"correctness. **Default: follow-up**, reported by the Addressables Analyze rule "Check Resources
|.AssetPathb"to Addressable Duplicate Dependencies" (`Editor/Build/AnalyzeRules/CheckResourcesDupeDependencies.cs`).
|.AssetPathb"
|.AssetPathb"## 2. Ownership decisions
|.AssetPathb"
|.AssetPathb"### 2.1 The picker and the attribute belong to CultLib
|.AssetPathb"
|.AssetPathb"`[CultInspectorAssetPath]` is CultLib's (`src/GameCult.Caching/CultInspectorAttributes.cs:65-77`),
|.AssetPathb"read into `CultInspectorMetadata.AssetPath` (`CultInspectorModel.cs:44,56`) and drawn by CultLib's
|.AssetPathb"Studio, not by Aetheria: `src/GameCult.Unity/Assets/Caching/Editor/CultCacheStudioDrawers.cs:168-176`
|.AssetPathb"turns an `ObjectField` into `AssetDatabase.GetAssetPath`. Aetheria's
|.AssetPathb"`Assets/Scripts/Editor/CultCacheDrawers.cs` claims only its own attributes. No CultLib path
|.AssetPathb"under `src/GameCult.Caching`, `src/GameCult.Unity` or `unity/` changed between `a0813c6` and
|.AssetPathb"CultLib `main` `244154e`. Aetheria is the only consumer in `F:\Projects` (`git grep` over every
|.AssetPathb"repo; the other hits are CultLib's README and voidbot memory dumps).
|.AssetPathb"
|.AssetPathb"Decision: the owner fills the gap. CultLib replaces the path attribute with
|.AssetPathb"`CultInspectorAssetGuidAttribute(Type? assetType = null)`, "a string member holding an engine
|.AssetPathb"asset's stable identity; in Unity, the `.meta` GUID", and its Studio drawer stores
|.AssetPathb"`AssetDatabase.AssetPathToGUID` and reads back through `GUIDToAssetPath`. Reusing the name
|.AssetPathb"`AssetPath` for GUIDs would make the attribute lie, and keeping both leaves a dead path form
|.AssetPathb"for the next field to pick up. Addressables stays out of CultLib: whether an asset is
|.AssetPathb"addressable is Aetheria's content rule (2.3), so the Studio package gains no Addressables
|.AssetPathb"dependency. `AssetType` stays optional; Aetheria cannot pass it (ServerShared is engine-free,
|.AssetPathb"recorded in `docs/cultcache-migration-cut.md:2003`), so the Studio falls back to `Object`.
|.AssetPathb"
|.AssetPathb"Cost: a CultLib release (plugins rebuilt with `scripts/build-unity-package.ps1`), tags
|.AssetPathb"`cultlib-unity-v1.0.60` and `caching-unity-v1.4.0` (removing a public attribute is breaking),
|.AssetPathb"and an Aetheria bump of `Packages/manifest.json` (both URLs), `Packages/packages-lock.json`
|.AssetPathb"and `Directory.Build.props` `CultLibRevision`. Coordination: CultLib `codex/studio-grouping`
|.AssetPathb"(mapped, blocked on the Aetheria lot schema) also edits `CultInspectorModel.cs` and the Studio
|.AssetPathb"drawers; whichever releases second rebases onto the first release. The older CultLib worktree branches
|.AssetPathb"(`CultLib-aetheria-authority`, `CultLib-delvehold-isosurface`, `CultLib-document-variants`) carry
|.AssetPathb"the attribute in its pre-move location (`Runtime/CultCacheInspectorAttributes.cs`); they are
|.AssetPathb"CultLib branches, not consumers, and meet the rename only if rebased.
|.AssetPathb"
|.AssetPathb"### 2.2 Addressables package **(probe)**
|.AssetPathb"
|.AssetPathb"- `com.unity.addressables` **2.9.1** is already resolved and compiled, at depth 1, pulled by
|.AssetPathb"  `com.unity.localization` 1.5.8 (`Packages/packages-lock.json:18-33,117-126`). Its
|.AssetPathb"  `package.json` needs Unity 2023.1+; the project is `6000.3.24f1`. `Library/ScriptAssemblies`
|.AssetPathb"  holds `Unity.Addressables.dll`, `Unity.Addressables.Editor.dll`, `Unity.ResourceManager.dll`.
|.AssetPathb"  No script uses Localization and no Localization settings exist (grep of `Assets`,
|.AssetPathb"  `ProjectSettings`); that is a follow-up, not this cut.
|.AssetPathb"- No settings exist yet: no `Assets/AddressableAssetsData`, no `com.unity.addressableassets`
|.AssetPathb"  in `ProjectSettings/EditorBuildSettings.asset` `m_configObjects`.
|.AssetPathb"- Creating settings (`AddressableAssetSettings.Create`, `:2023-2056`, and `Validate`,
|.AssetPathb"  `:1946-1976`) writes: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`,
|.AssetPathb"  `DefaultObject.asset`, `AssetGroups/Default Local Group.asset` with
|.AssetPathb"  `AssetGroups/Schemas/Default Local Group_BundledAssetGroupSchema.asset` and
|.AssetPathb"  `_ContentUpdateGroupSchema.asset`, `AssetGroupTemplates/Packed Assets.asset`,
|.AssetPathb"  `DataBuilders/BuildScriptFastMode.asset`, `BuildScriptPackedPlayMode.asset`,
|.AssetPathb"  `BuildScriptPackedMode.asset`, each with a `.meta`, plus the config object entry in
|.AssetPathb"  `EditorBuildSettings.asset`. It sets `BuildAddressablesWithPlayerBuild = PreferencesValue`,
|.AssetPathb"  a per-user preference.
|.AssetPathb"- **GUID keys.** `BundledAssetGroupSchema.IncludeGUIDInCatalog` defaults to `true`
|.AssetPathb"  (`BundledAssetGroupSchema.cs:155`); the player catalog then adds each entry's guid as a key
|.AssetPathb"  (`AddressableAssetEntry.CreateKeyList`, `:254-261`). Folder entries expand into sub-entries
|.AssetPathb"  carrying their own guid (`AddressableAssetEntry.cs:554`). In editor play mode ("Use Asset
|.AssetPathb"  Database"), `AddressableAssetSettingsLocator` keys direct entries by guid (`:155`) and resolves
|.AssetPathb"  a guid under an addressable folder entry by walking parent folders (`:262-283`). A guid that is
|.AssetPathb"  not addressable yields no location and the load fails. So `LoadAssetAsync<T>(guid)` works in
|.AssetPathb"  editor and player for direct and folder entries, and fails for non-addressable assets.
|.AssetPathb"- **Resources.** An asset under a `Resources` folder cannot be an entry: the Inspector and Groups
|.AssetPathb"  window move it to `Resources_moved` (`AddressableAssetUtility.SafeMoveResourcesToGroup`,
|.AssetPathb"  `:309-339`), and moving an entry into Resources unmarks it (`AddressableAssetSettings.cs:2900-2906`).
|.AssetPathb"  The programmatic `CreateOrMoveEntry` (`:2541`) does not check, so a script could create an
|.AssetPathb"  entry the GUI would refuse; the cut must move assets out of Resources rather than rely on that.
|.AssetPathb"  Moving a file together with its `.meta` keeps the guid, so stored GUIDs survive the move.
|.AssetPathb"- **Sync.** `AsyncOperationHandle.WaitForCompletion` (`AsyncOperationHandle.cs:178`) completes a
|.AssetPathb"  local load in place; `SynchronousAddressables.md:21` puts the cost near `Resources.Load` for
|.AssetPathb"  local bundles and warns that it completes every pending load.
|.AssetPathb"- **Player build.** Bundles build with the player only when `BuildAddressablesWithPlayerBuild`
|.AssetPathb"  resolves to build (`build-player-builds.md:5`). Cut 1 sets `BuildWithPlayer` in the project
|.AssetPathb"  settings so the rule is committed, not a preference.
|.AssetPathb"
|.AssetPathb"### 2.3 How an asset becomes addressable (Q1)
|.AssetPathb"
|.AssetPathb"Recommended (Q1 A): one folder entry, `Assets/Content`, in `Default Local Group`. Addressable
|.AssetPathb"means "lives under `Assets/Content`". Cut 2 moves `Assets/Resources/Prefabs/`,
|.AssetPathb"`Assets/Resources/Schematics/` and `Assets/Resources/Sprites/Icons/` there with their `.meta`
|.AssetPathb"files, keeping relative paths. `Sprites/Icons/` includes the 33 settings icons settings Cut 1
|.AssetPathb"will reference, so that cut only stores GUIDs. The 7 `CelestialBodySettings` presets under
|.AssetPathb"`Assets/Plugins/Celestial Body/Solar System/` move into `Assets/Content/Celestial Bodies/` in
|.AssetPathb"settings Cut 1 (that map's old fork B(a) move, retargeted).
|.AssetPathb"
|.AssetPathb"### 2.4 Loading owner
|.AssetPathb"
|.AssetPathb"`Assets/Scripts/EngineAssets.cs` (Assembly-CSharp, replaces `UnityHelpers.cs`), about 30 lines:
|.AssetPathb"- `public static T Load<T>(string guid) where T : Object`. Empty GUID returns `null`. A
|.AssetPathb"  `Component` type loads the GameObject and returns `GetComponent<T>()`.
|.AssetPathb"- Loads with `Addressables.LoadAssetAsync<T>(guid).WaitForCompletion()`. All call sites are
|.AssetPathb"  synchronous construction paths that `Instantiate` at once. Making them async would reorder
|.AssetPathb"  entity construction for 30 local assets. **Default: sync.**
|.AssetPathb"- Caches one handle per `(guid, type)` for the process lifetime and never releases, which is
|.AssetPathb"  the lifetime `Resources.Load` has today. It owns every handle; no caller holds or releases one.
|.AssetPathb"  A failed handle logs the GUID and returns `null`; callers keep their existing null handling.
|.AssetPathb"
|.AssetPathb"### 2.5 Validation owner
|.AssetPathb"
|.AssetPathb"`Assets/Scripts/Editor/EngineAssetCheck.cs` (Assembly-CSharp-Editor). It needs `AssetDatabase`,
|.AssetPathb"the Addressables editor settings and the consumer component types, so it cannot be a headless
|.AssetPathb"`AetherDb` command or a `Tests.asmdef` test (that assembly cannot reference Assembly-CSharp).
|.AssetPathb"It has a menu item and a batchmode entry (`-executeMethod EngineAssetCheck.Run`, exit 1 on
|.AssetPathb"failure). It opens the catalog read-only through `AetheriaStores.Open`, walks every stored
|.AssetPathb"document for `[CultInspectorAssetGuid]` string members, and for each non-empty value checks:
|.AssetPathb"1. it is 32 lowercase hex characters;
|.AssetPathb"2. `AssetDatabase.GUIDToAssetPath` names an existing file;
|.AssetPathb"3. `AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid, includeImplicit: true)` is non-null;
|.AssetPathb"4. the asset satisfies its member's consumer contract, from one table with one row per member:
|.AssetPathb"   `Icon`, `ActionBarIcon`, `Schematic`, `Logo` are `Texture2D`; `HullData.Prefab` is a
|.AssetPathb"   GameObject with `EntityInstance`; `WeaponData.EffectPrefab` has `InstantWeaponEffectManager`
|.AssetPathb"   on `InstantWeaponData` records and `ConstantWeaponEffectManager` on `ConstantWeaponData`
|.AssetPathb"   records; `ThrusterData.ParticlesPrefab` and `AetherDriveData.Particles` have `ParticleSystem`.
|.AssetPathb"   An attributed member missing from the table fails the check, so a new field cannot skip it.
|.AssetPathb"
|.AssetPathb"It reports record name, member, GUID and path per failure. The check does not write, and no
|.AssetPathb"repair path exists.
|.AssetPathb"
|.AssetPathb"## 3. Data migration (Cut 2, First)
|.AssetPathb"
|.AssetPathb"- Scratch-only program over `Aetheria.Shared` at the pre-Cut-2 commit (the fork K (a)
|.AssetPathb"  precedent: no landed bypass, no one-shot command left behind). It opens a **copy** of the
|.AssetPathb"  catalog writable, walks every stored document for `[CultInspectorAssetPath]` string members,
|.AssetPathb"  and maps each non-empty path to the `guid:` of `<repo>/<path>.meta`. It writes through the
|.AssetPathb"  catalog's own types and commits once.
|.AssetPathb"- Report, one line per value: record type, record name, member, old path, GUID. Unresolved
|.AssetPathb"  paths are listed and **fail the run with nothing written**; none are expected (1.1). Empty
|.AssetPathb"  values stay empty.
|.AssetPathb"- No record is added or removed, and no global is touched. Member names, keys and `JsonProperty`
|.AssetPathb"  names are unchanged: the member still names the asset, and the attribute says the form.
|.AssetPathb"- The rewritten `GameData/Aetheria.cc` (LFS) lands in the same commit as the code. A commit
|.AssetPathb"  that holds GUIDs with path-loading code, or paths with GUID-loading code, loads nothing.
|.AssetPathb"- Before/after: `AetherDb census` and `dangling` outputs are byte-identical (same capture
|.AssetPathb"  method both sides); the report has 30 lines; a scratch re-read finds 30 GUIDs, 108 empty
|.AssetPathb"  strings and no string starting with `Assets/`.
|.AssetPathb"
|.AssetPathb"## 4. Cuts
|.AssetPathb"
|.AssetPathb"### Cut 0. CultLib: asset GUID attribute and Studio picker
|.AssetPathb"
|.AssetPathb"- **Repo/branch:** CultLib, a `codex/` branch from `main` `244154e`. Releases before Cut 2.
|.AssetPathb"- **Deletes first:** `CultInspectorAssetPathAttribute` (`src/GameCult.Caching/CultInspectorAttributes.cs:65-77`);
|.AssetPathb"  `CultInspectorMetadata.AssetPath` (`CultInspectorModel.cs:44,56`); the path branch in
|.AssetPathb"  `CultCacheStudioDrawers.cs:170-176`.
|.AssetPathb"- **Adds:** `CultInspectorAssetGuidAttribute(Type? assetType = null)` with the identity comment;
|.AssetPathb"  `CultInspectorMetadata.AssetGuid`; the drawer branch: `GUIDToAssetPath` then
|.AssetPathb"  `LoadAssetAtPath(path, assetType)`, then `ObjectField`, storing `AssetPathToGUID(next)` or
|.AssetPathb"  `string.Empty`. README `src/GameCult.Unity/Assets/Caching/README.md:153,164`.
|.AssetPathb"- **Release:** rebuild plugins, bump `unity/org.gamecult.cultlib/package.json` to 1.0.60 and
|.AssetPathb"  `src/GameCult.Unity/Assets/Caching/package.json` to 1.4.0 (depending on cultlib 1.0.60), tag both.
|.AssetPathb"- **Verification:**
|.AssetPathb"  - builds: `GameCult.Caching`, the CultLib test project that covers `CultInspectorModel`, the
|.AssetPathb"    Unity package build script.
|.AssetPathb"  - tests: a model test pins that a member carrying the GUID attribute exposes `AssetGuid` and
|.AssetPathb"    its `AssetType`.
|.AssetPathb"  - negative: `git grep -n "AssetPath" -- src unity` finds nothing (checked: today the only hits
|.AssetPathb"    are the three files above and the README).
|.AssetPathb"  - operator (Unity, after Cut 2's bump): pick a prefab in Studio, the stored value is its GUID,
|.AssetPathb"    and reopening shows the same asset.
|.AssetPathb"
|.AssetPathb"### Cut 1. Aetheria: Addressables settings
|.AssetPathb"
|.AssetPathb"- **Repo/branch:** Aetheria `codex/item-provenance`, after settings Cut 0 lands. Independent of Cut 0.
|.AssetPathb"- **First:** Unity closed (operator), because batchmode refuses an open project.
|.AssetPathb"- **Adds:** `"com.unity.addressables": "2.9.1"` as a direct dependency in `Packages/manifest.json`,
|.AssetPathb"  since Aetheria code now uses it; the lock entry goes from depth 1 to 0. Settings created by a
|.AssetPathb"  scratch-only batchmode `-executeMethod` script, which is not landed: `Create`, then set
|.AssetPathb"  `BuildAddressablesWithPlayerBuild = BuildWithPlayer`, create `Assets/Content/` (+`.meta`) and
|.AssetPathb"  `CreateOrMoveEntry(guid of Assets/Content, DefaultGroup)`. Operator alternative: *Window >
|.AssetPathb"  Asset Management > Addressables > Groups > Create Addressables Settings*, then the same three
|.AssetPathb"  edits by hand.
|.AssetPathb"- **Intentional Unity churn (the whole list):** `Packages/manifest.json`,
|.AssetPathb"  `Packages/packages-lock.json`, `ProjectSettings/EditorBuildSettings.asset` (one
|.AssetPathb"  `com.unity.addressableassets` config object), `Assets/Content.meta`, and under
|.AssetPathb"  `Assets/AddressableAssetsData/`: `AddressableAssetSettings.asset`, `DefaultObject.asset`,
|.AssetPathb"  `AssetGroups/Default Local Group.asset`, `AssetGroups/Schemas/Default Local Group_BundledAssetGroupSchema.asset`,
|.AssetPathb"  `AssetGroups/Schemas/Default Local Group_ContentUpdateGroupSchema.asset`,
|.AssetPathb"  `AssetGroupTemplates/Packed Assets.asset`, `DataBuilders/BuildScriptFastMode.asset`,
|.AssetPathb"  `DataBuilders/BuildScriptPackedPlayMode.asset`, `DataBuilders/BuildScriptPackedMode.asset`,
|.AssetPathb"  and every folder and file `.meta`. Nothing else: the modified materials and render textures
|.AssetPathb"  in the worktree today are pre-existing Unity churn and stay unstaged.
|.AssetPathb"- **Harmless alone:** no code loads through Addressables yet, and `Assets/Content` is empty.
|.AssetPathb"- **Verification:**
|.AssetPathb"  - operator: Unity opens without Addressables errors; the Groups window shows `Assets/Content`
|.AssetPathb"    under `Default Local Group`; the ARPG play smoke is unchanged.
|.AssetPathb"  - negative: `git show --stat` lists only the files above.
|.AssetPathb"
|.AssetPathb"### Cut 2. Aetheria: switch every data asset reference to GUIDs
|.AssetPathb"
|.AssetPathb"One commit, or a series whose intermediate commits Soul accepts as unplayable. See section 3
|.AssetPathb"for why it is atomic.
|.AssetPathb"
|.AssetPathb"- **Depends on:** Cuts 0 and 1, and Q1.
|.AssetPathb"- **First:** the migration report (section 3) against the pre-cut catalog; `AetherDb census` and
|.AssetPathb"  `dangling` captures; `git grep -n "Assets/Resources/" -- Assets/Scripts` capture.
|.AssetPathb"- **Deletes first:**
|.AssetPathb"  - `Assets/Scripts/UnityHelpers.cs` (+`.meta`), 11 lines.
|.AssetPathb"  - `Gameplay/ActionBarSlot.cs:114`, `:153`: the inline `Substring("Assets/Resources/".Length).Split('.').First()`
|.AssetPathb"    loads (and `using System.Linq` if nothing else uses it).
|.AssetPathb"  - All 8 `CultInspectorAssetPath` usages (`ItemData.cs:352,362,392,499`, `Weapon.cs:43`,
|.AssetPathb"    `Thruster.cs:26`, `AetherDrive.cs:50`, `Corporations.cs:25`) become `CultInspectorAssetGuid`.
|.AssetPathb"- **Moves (git mv, file and `.meta` together, guids unchanged):** `Assets/Resources/Prefabs/` to
|.AssetPathb"  `Assets/Content/Prefabs/`, `Assets/Resources/Schematics/` to `Assets/Content/Schematics/`,
|.AssetPathb"  `Assets/Resources/Sprites/Icons/` to `Assets/Content/Sprites/Icons/`, plus their folder `.meta` files.
|.AssetPathb"- **Adds:** `Assets/Scripts/EngineAssets.cs` (2.4); `Assets/Scripts/Editor/EngineAssetCheck.cs` (2.5);
|.AssetPathb"  the migrated `GameData/Aetheria.cc`; the CultLib bump (manifest URLs `#cultlib-unity-v1.0.60`
|.AssetPathb"  and `#caching-unity-v1.4.0`, lock, `Directory.Build.props:5` `CultLibRevision`).
|.AssetPathb"- **Per-file changes (against `f8396313`):**
|.AssetPathb"  - `ActionBarSlot.cs:114`, `:153`: `EngineAssets.Load<Texture2D>(...)`.
|.AssetPathb"  - `EntityInstance.cs:195`, `:224`: `EngineAssets.Load<InstantWeaponEffectManager>` /
|.AssetPathb"    `<ConstantWeaponEffectManager>(data.EffectPrefab)`; the error text at `:200`, `:229` says GUID, not path.
|.AssetPathb"  - `ShipInstance.cs:53`, `:67`: `EngineAssets.Load<ParticleSystem>`.
|.AssetPathb"  - `ZoneRenderer.cs:295`, `:304`: `EngineAssets.Load<GameObject>`.
|.AssetPathb"  - `Editor/CultCacheDrawers.cs:104`: `LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(item.Schematic))`.
|.AssetPathb"- **Authority map:**
|.AssetPathb"  - Owner: the catalog field holds the GUID. `EngineAssets` owns turning a GUID into a loaded
|.AssetPathb"    asset and holding its handle. The `Assets/Content` folder entry owns addressability.
|.AssetPathb"  - Inputs: GUID strings from catalog records; the Addressables catalog (editor locator or player catalog).
|.AssetPathb"  - Outputs: loaded `Object`s or components, or `null` with a logged GUID.
|.AssetPathb"  - Derived state: asset paths are display-only (Studio, check reports). Group entries are
|.AssetPathb"    derived from folder location.
|.AssetPathb"  - Forbidden writers: `Resources.Load` for data-named assets; any path-prefix stripping; any
|.AssetPathb"    runtime or editor code that writes a path into an asset member; per-asset entries created by
|.AssetPathb"    scripts outside `Assets/Content`.
|.AssetPathb"  - Shared paths: game runtime, editor play mode and player builds all load through
|.AssetPathb"    `EngineAssets` and the same GUID keys. Studio authoring and the migration both store GUIDs.
|.AssetPathb"    The check reads through the same `AetheriaStores.Open`.
|.AssetPathb"  - Deletion line: `UnityHelpers.cs`, the two inline strips, and the path attribute are gone
|.AssetPathb"    before `EngineAssets` exists.
|.AssetPathb"- **Verification:**
|.AssetPathb"  - builds: `Aetheria.Shared` headless with `-p:CultLibRoot=<CultLib 1.0.60 scratch worktree>`;
|.AssetPathb"    `tests/Aetheria.Shared.Tests`; Unity batchmode compile.
|.AssetPathb"  - tests: `EngineAssetCheck.Run` in batchmode exits 0 on the migrated catalog. Mutation: on a
|.AssetPathb"    scratch catalog copy, set one `HullData.Prefab` to a Texture2D GUID, one to a GUID outside
|.AssetPathb"    `Assets/Content`, one to a random 32-hex, and one to an old path. Each must fail with its
|.AssetPathb"    record named. Also add a scratch attributed member missing from the table; it must fail.
|.AssetPathb"  - data: section 3 before/after checks.
|.AssetPathb"  - negative (checked against `f8396313` for collisions; each has hits only at the sites above today):
|.AssetPathb"    - `git grep -n "Resources.Load" -- Assets/Scripts` gives exactly `ActionGameManager.cs:420`.
|.AssetPathb"    - `git grep -n "Assets/Resources/" -- '*.cs'` gives only the generated comments at
|.AssetPathb"      `AetheriaInput.cs:5,19`, `GradientMapper.cs:94,96`, and `tools/AetherDb/AuthoredSettings.cs:13`
|.AssetPathb"      until settings Cut 1 deletes that file.
|.AssetPathb"    - `git grep -n "UnityHelpers\|CultInspectorAssetPath"` gives nothing outside `docs/`.
|.AssetPathb"    - `git ls-files Assets/Resources/Prefabs Assets/Resources/Schematics Assets/Resources/Sprites/Icons` is empty.
|.AssetPathb"  - operator (Unity):
|.AssetPathb"    1. Addressables Play Mode Script "Use Asset Database": ARPG play smoke. Ships, turret and
|.AssetPathb"       Zenith station spawn; thruster and aether drive particles show; fire an instant weapon
|.AssetPathb"       (e.g. Autocannon) and a charged one (ChargeBlast SG); the action bar shows icons where
|.AssetPathb"       the settings fallback applies.
|.AssetPathb"    2. Play Mode Script "Use Existing Build" after *Build > New Build > Default Build Script*:
|.AssetPathb"       the same smoke. This proves the player catalog carries GUID keys.
|.AssetPathb"    3. Addressables *Analyze*: run "Check Resources to Addressable Duplicate Dependencies" and
|.AssetPathb"       record the result. The 5 known leftovers (1.4) are expected.
|.AssetPathb"    4. Studio: `LonginusX` schematic still underlays the shape drawer.
|.AssetPathb"
|.AssetPathb"## 5. Subtraction ledger (estimate)
|.AssetPathb"
|.AssetPathb"| Cut | Removed | Added | Dependencies, formats, targets |
|.AssetPathb"|---|---|---|---|
|.AssetPathb"| 0 | path attribute ~12, metadata 2, drawer branch 6 | GUID attribute ~12, metadata 2, drawer branch ~7, test ~15 | CultLib release; no targets |
|.AssetPathb"| 1 | none | Addressables settings assets (Unity YAML, ~12 files) | `com.unity.addressables` becomes direct (already installed); Addressables build step joins player builds |
|.AssetPathb"| 2 | `UnityHelpers.cs` 11, 2 inline strips, 283 assets (584 files with `.meta`) leave `Assets/Resources` (moved, not deleted) | `EngineAssets.cs` ~30, `EngineAssetCheck.cs` ~80, 30 catalog values rewritten | removes the Resources path convention for data; no targets |
|.AssetPathb"
|.AssetPathb"Net code is positive (~+110). It buys the ruled capability (GUID identity that survives moves,
|.AssetPathb"and one loader) and a check that did not exist: today a mistyped path fails only at spawn.
|.AssetPathb"
|.AssetPathb"## 6. Operator forks
|.AssetPathb"
|.AssetPathb"**Q1. How assets become addressable.** Blocks Cut 1.
|.AssetPathb"- (A) One folder entry `Assets/Content`. Addressable means under that folder. Cut 2 moves
|.AssetPathb"  `Prefabs/`, `Schematics/` and `Sprites/Icons/` out of Resources into it, and settings Cut 1
|.AssetPathb"  moves the 7 body presets there. New content is addressable by placement, and the group file
|.AssetPathb"  does not change per asset. Unreferenced assets in those folders ship too, as they do from
|.AssetPathb"  Resources today.
|.AssetPathb"- (B) One entry per referenced asset, assets kept in place, except that the 27 in Resources must
|.AssetPathb"  still move (2.2). Only referenced assets ship. Each new pick needs a manual *Addressable* tick
|.AssetPathb"  (the check catches a miss), and the group asset changes with every new reference.
|.AssetPathb"- (C) Per-asset entries generated from the catalog by an editor step before play and build. The
|.AssetPathb"  group becomes derived state, but a sync step runs at play and build boundaries, and a stale
|.AssetPathb"  group between steps fails loads in play mode.
|.AssetPathb"- **Recommend (A).** It makes the invariant structural (location) with no per-asset bookkeeping
|.AssetPathb"  and no sync step, and it matches how Resources behaves today.
|.AssetPathb"
|.AssetPathb"## Follow-ups outside this migration
|.AssetPathb"
|.AssetPathb"- `ActionGameManager.cs:420` loads input prompt sprites from `Resources/Sprites/Input` by control
|.AssetPathb"  name (1.3).
|.AssetPathb"- 5 Resources textures referenced by moved prefabs stay in Resources and ship twice (1.4).
|.AssetPathb"- `com.unity.localization` 1.5.8 is a direct dependency with no script use and no settings; it
|.AssetPathb"  was the only reason Addressables was installed.
|.AssetPathb"- `Faction.Logo` (5 values) has no reader; `EquippableItemData.ActionBarIcon` and
|.AssetPathb"  `ConsumableItemData.Icon` hold no values (1.1).
|.AssetPathb"- Doc sweep for Self: `docs/settings-globals-cut.md` still carries path-based text in 2.1
|.AssetPathb"  (`:238`, "Engine asset paths (forks I and B...)"), 3.2 (`:328`), Cut 1, and forks I and B
|.AssetPathb"  (`:522-541`). Those become history once this map is ruled. Settings Cut 1's icon and preset
|.AssetPathb"  fields use `[CultInspectorAssetGuid]` and `EngineAssets`.
