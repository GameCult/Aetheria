# Settings Into CultCache Globals: Cut Map

Date: 2026-09-17

Status: Imagination pass, cut map. Nothing here has landed. Anchors are against
`codex/item-provenance` HEAD `00ebe46a` (which carries the separate mouse-sensitivity
move). Claims marked **(probe)** were run, not read off names:

- a Python walk of every `MonoBehaviour`/`ScriptableObject` field graph in
  `Assets/Scripts` (attribute-prefixed fields included), and of the scene, prefab and
  asset YAML;
- a Python parse of `Settings.asset` mapped key by key onto the C# classes;
- a headless build of `tools/AetherDb` in a sparse scratch worktree of `00ebe46a`, with
  CultLib at `a0813c6` as a detached scratch worktree. Both were removed afterwards, and
  the main tree got no `bin`/`obj` writes. The probe sources were scratch-only and are
  not kept; each result below says what it ran.

Operator ruling, 2026-09-17: "We don't care for Unity's serializer, we have CultCache
sitting right there. All settings should be in globals."

Rulings (2026-09-17):
- **Engine asset references: Addressables, stored as asset GUIDs** (operator: "you got it",
  to that proposal). This supersedes forks I and B: nothing moves under `Resources`, and the
  existing `[CultInspectorAssetPath]` fields (`ItemData.Icon`/`ActionBarIcon`, `Weapon` effect,
  `Thruster`/`AetherDrive` particles, `Corporations` logo) convert too, so there is one rule.
  That conversion is its own cut, mapped in `docs/addressables-cut.md`, and lands before Cut 1;
  Cut 1's icon and body-preset fields then use it.
- **K (a), D (a), P (a), X (a), M (a): Self's defaults**, stated to the operator, who did not
  object. Reopen any of them on request.
- Open: none blocking Cut 0.
Doc sweep for Self: this map supersedes the mechanism in `docs/headless-playground-cut.md`
fork S ("a text edit of `Settings.asset`", `:1151-1159`, `:404-410`, `:693-694`). It also
changes the `Run.New(CultCache, GameplaySettings, ZoneGenerationSettings, PlanetSettings, ...)`
signature (`:415`), and that map's use of `AuthoredSettings` (`:176`, `:224`) goes away.
Once fork P is ruled, those settings come from the cache.

## 0. Target

Ends:
- Every authored setting lives in a CultCache global. The catalog holds design data
  and the player store holds preferences. Unity's serializer holds no setting.
- One owner per setting. The Unity game, the headless sim, the tests and `AetherDb`
  read the same record through the same port. No reader rebuilds the values by hand,
  and none parses them out of YAML.
- Nothing authored is lost, and no authored value loads silently as zero.

Invariants:
- Catalog globals are authored, never invented. `AetheriaStores.Open` keeps refusing a
  populated catalog that is missing one, however it is opened (`AetheriaStores.cs:14-15`,
  ruling behind `ab4ced0f`).
- A catalog global is never written at runtime. Runtime variations are copies owned by
  the run or the entity.
- One home store per type, and a commit lands in one store.

Not settings (the census below gives the evidence):
- Engine configuration: `ProjectSettings/*`, `Assets/Resources/InputSystem.inputsettings.asset`.
- Per-component presentation tuning on scene and prefab instances (fork M).

## 1. Census

### 1.1 `GameSettings` / `Settings.asset`, field by field

`GameSettings` (`Assets/Scripts/Gameplay/GameSettings.cs:11`) is the only
`ScriptableObject` settings type. It is referenced from 15 YAML fields:
`Assets/Scenes/ARPG.unity` x12, `Assets/Scenes/Main Menu.unity` x2 and
`Assets/Prefabs/UI/Main Menu Canvas.prefab` x1. The asset guid is
`cd2344e2001a37544aab0960c164d4ff`.

**(probe)** YAML-to-class mapping: every key under the `MonoBehaviour` maps to a field,
and no key is unplaced. The asset holds 266 scalar leaves, 15 `float3` components, 16
`Color` components, 58 `Gradient` keys and 53 object references. One field is missing
from the YAML: `ZoneGenerationSettings.NeutralWandererCount`, which Unity loads as its
initializer, 2. The existing `AetherDb settings` command agrees: 0 unplaced, 0 missing.

Classes: **C** is a catalog global (design data). **CP** is a catalog global holding
presentation data: authored and not a preference, but only Unity reads it. **E** is an
engine asset reference (forks I and B). **Dead** means no code reads it (checked with
`rg` over `Assets/Scripts`, `tools` and `tests`).

| Field (`GameSettings.cs` line) | Type | Authored (`Settings.asset` line) | Readers (HEAD) | Class |
|---|---|---|---|---|
| `DefaultOverworldSoundbank`, `DefaultCombatSoundbank`, `DefaultBossSoundbank`, `AmbienceSoundBank` (13-16) | string | `:15-18` | none | Dead (fork X) |
| `StartingHullName` (17) | string | `LonginusX` (the code default is `Longinus`; `LonginusX` is in the catalog) | `ActionGameManager.cs:776` | C |
| `PickupLifetime` (18) | float | 30 | `ZoneRenderer.cs:572` | C |
| `LootDropProbability`, `LootDropVelocity` (19-20) | float | .25, 25 | `EntityInstance.cs:413,417,428` | C |
| `HeatstrokePhasingFloor`, `HeatstrokePhasingFrequency` (21-22) | float | 0, 5 | `ActionGameManager.cs:1236` | CP |
| `WormholeDistanceRatio` (23) | float | .75 | `ZoneRenderer.cs:230` (a game rule, per the headless map) | C |
| `DefaultViewDistance`, `MinimapZoneGravityRange` (24-25) | float | 4096, .45 | `ZoneRenderer.cs:178,526` | CP |
| `MinimapZoomLevels`, `DefaultMinimapZoom` (26-27) | float[], int | [250,500,1000,2000,4000], 3 | `ActionGameManager.cs:280-284`, `ZoneRenderer.cs:179` | CP |
| `IconSize` (28) | `ExponentialCurve` | .15/10/25 | `ZoneRenderer.cs:412` | CP |
| `AsteroidMeshCount`, `MinimapAsteroidSize`, `MinimapIconSize`, `PlanetRotationSpeed` (29-32) | int, float | 4, 3, .125, .1 | `ZoneRenderer.cs:342,361,147,497` | CP |
| `NameGeneratorSettings` (33) | ServerShared | `:43-46` | `MainMenu.cs:132,158` -> `Galaxy` | C |
| `TutorialGenerationSettings` (34) | ServerShared | `:47-56` | `MainMenu.cs:156`, `ActionGameManager.cs:772` | C |
| `TutorialBackgroundSettings`, `SectorBackgroundSettings` (35-36) | ServerShared | `:57-74` (`NoisePosition` at `:63,72`) | `MainMenu.cs:125-157` (**writes**, see 1.4) | C |
| `SectorGenerationSettings` (37) | ServerShared | `:75-79` | `MainMenu.cs:130` | C |
| `BodySettingsCollections` (38) | `{float MinimumMass; CelestialBodySettings[]}` | `:80-99`: 6 collections, 7 refs, all under `Assets/Plugins/Celestial Body/Solar System/` | `ZoneRenderer.cs:397` | E (fork B) |
| `PlanetSettings` (39) | ServerShared | `:100-155` | `ActionGameManager.cs:690` -> `Zone`, `ZoneRenderer.cs:391-536` | C |
| `ZoneSettings` (40) | `ZoneGenerationSettings` | `:156-217` | `ActionGameManager.cs:685`, `SectorRenderer.cs:71-72` | C |
| `GameplaySettings` (41) | ServerShared | `:218-326` | `ItemManager` (ctor at `ActionGameManager.cs:263`), plus Unity readers listed in Cut 1 | C |
| `HullHitColor` (42) | Color | `:327` | none | Dead (fork X) |
| `ArmorHitColor`, `HardpointHitColor`, `GearHitColor` (43-45) | Color | `:328-330`, every a=1 | `InventoryPanel.cs:497-499` | CP -> `float3` |
| `ArmorGradient`, `DurabilityGradient` (46-47) | Gradient | `:331-390` | `InventoryPanel.cs:790,792` | CP -> `float4[]` |
| `DefaultEnvironment` (48) | `ZoneEnvironment` | `:391-425` | `ActionGameManager.cs:203`, `VolumeSampling.cs:54` | C |
| `ItemIcons`, `WeaponTypeIcons`, `WeaponCaliberIcons`, `WeaponRangeIcons`, `WeaponFireTypeIcons`, `WeaponModifierIcons` (49-54) | Sprite[] | `:426-477` | `GetIcon`/`GetIcons` (`:56-84`) from `ActionBarSlot.cs:155-156`, `SchematicListElement.cs:27-44`; edited by `Editor/GameplaySettingsEditor.cs` | E (fork I) |

**(probe)** Icon references, resolved guid to path through every `.meta` under
`Assets`:
- 33 resolve under `Assets/Resources/Sprites/Icons{,/Weapons,/Tech}`.
- 3 are intentional `{fileID: 0}` slots: `ItemIcons[13]`, `WeaponFireTypeIcons[0]` and
  `WeaponModifierIcons[0]` (the None slot).
- **9 point at assets that do not exist**: `WeaponTypeIcons[1]` (`dc8c6f7a…`),
  `WeaponTypeIcons[9]` (`9ae7f8b4…`) and every real `WeaponModifierIcons[1..7]`. This bug
  predates the cut. It is recorded here, not fixed, and not invented over.

Gradients convert without loss **(read, not built)**. Both gradients are `m_Mode: 0`
(Blend) with `m_NumAlphaKeys: 2`, and alpha is 1 at 0 and 1 at 65535. That is exactly
what the existing `UnityExtensions.ToGradient(float4[], sharp: false)`
(`CultCache/UnityExtensions.cs:29-36`) rebuilds. Color key time is a `ushort`, so `w =
ctime/65535f` goes back to the same `ushort`. The values are:
- `ArmorGradient`: `(.2244,.66,.2244, 0)`, `(1,1,1, 1)`.
- `DurabilityGradient`: `(.7264151,0,0, 0)`, `(1,1,.19999999, 29613/65535)`,
  `(.2244,.66,.2244, 1)`.

### 1.2 Other `Assets/Resources/*.asset`

**(probe)** None of these five has a live script. For each, the `m_Script` guid matches
no `.meta` in `Assets` or `Library/PackageCache`, and no scene, prefab, material, asset
or `.cs` references the asset's own guid. `git log -S` on the script guids finds where
each script was deleted:

| Asset | Script deleted | What it held | Class |
|---|---|---|---|
| `Atmosphere Settings.asset` | `AtmosphereConfig.cs`, `d61d2f19` "Raymarching Cleanup" (2022-07-13) | atmosphere scattering constants | dead presentation |
| `Volume Settings.asset` | the clouds renderer, `d61d2f19` | cloud texture refs and tiling | dead presentation |
| `Elysium.asset`, `Test Galaxy.asset` | `Galaxy.cs` and `GalaxyEditor.cs`, `0f33e541` "Sector Constellations" (2021-03-11) | the old galaxy generator's `MapData` (arms, twist, masses) | dead content (fork X) |
| `Icons.asset` | `UI/Icons.cs`, `81acaf2a` (2021-05-08) | plus and minus textures | dead presentation |

`InputSystem.inputsettings.asset` is engine configuration and stays.

### 1.3 CultMath fields Unity serializes (these load as zero)

**(probe)** A walk of the field graph from every `MonoBehaviour`/`ScriptableObject`
through `[Serializable]` types, looking for CultMath `floatN`/`intN`/`boolN`/`quaternion`
leaves, finds these. A `Vector3` or `Vector2` field is not affected: the inline
`{x: …}` YAML values belong to those.

| Field | Stored data | Loss |
|---|---|---|
| `ActionGameManager.Sensitivity` (float2, cf60fd07 `:101`) | `ARPG.unity:16548-16550` `(-.001, .001)` | Real. Fixed separately in `00ebe46a` (`PlayerInputSettings.Sensitivity`, `PlayerSettings.cs:76`). The scene key is now stale. |
| `GameSettings.GameplaySettings.Tiers[].Color` (`RarityTier.Color`, `Settings.cs:226`) | `Settings.asset:221-250`: 5 tiers | **Real.** Rarity colours render black in `TradeMenu.cs:193`, `PropertiesPanel.cs:436` and `ZoneRenderer.cs:567`. Cut 1 recovers them. |
| `MapRenderer.Position` (float2, `MapRenderer.cs:30`) | `ARPG.unity:10236-10238` `(0, 0)` | None. The stored value was zero, and the field is runtime pan state. |
| `FieldDriver.Push` (float2, `FieldDriver.cs:17`) | no key in `FieldShieldTest.unity` | None. It is runtime state, set at `:179`. |

`FieldShieldTest.unity:1047-1049` also holds a stale `Throttle: (0,0)` for a field that
was deleted. It is harmless. **(probe)** No `[Serializable]` type on a Unity-reachable
path lost the attribute during the migration: the YAML map above placed every key.

### 1.4 Writers into settings (forbidden once they are globals)

- `MainMenu.cs:125` and `:148` assign `Settings.{Sector,Tutorial}BackgroundSettings.NoisePosition = Random.value * 1000`
  on the shared authored object. The authored values `91.29439` and `536.5106`
  (`Settings.asset:72,63`) look like play-mode residue from this writer. Run state already
  has a home: `Galaxy.Background` is saved as `SavedGame.Background` (`SavedGame.cs:37`).
- **Entity settings aliasing.** `Loadout.cs:131` and `LoadoutGenerator.cs:46,61,76` pass
  `GameplaySettings.DefaultEntitySettings` itself into `Entity`, and `Entity.cs:340`
  stores it without copying. `InventoryMenu.cs:175` then writes
  `CurrentEntity.Settings.ShutdownPerformance`. That write changes the default for every
  generated entity, and once settings are a global it changes the cached catalog record.
  Two other places copy: `ActionGameManager.cs:228-232` (`NewEntitySettings`, used by
  `TradeMenu.cs:377`) and `EntitySerializer.cs:51-52`. Three copies and four aliases make
  split authority.

### 1.5 Component tuning on scene and prefab instances

**(probe)** This lists every scalar, non-reference serialized field stored in `.unity`
and `.prefab` files for scripts in `Assets/Scripts`, grouped by script:
- **Presentation timing, layout and VFX:** 45 scripts. Examples: `ShieldAnimation`
  curves, `ConstantLaser`/`Laser`/`HitscanEffect` curves, `InputDisplayLayout` colours,
  `SectorMap`, `ZoneRenderer` tour, `TextButton`, `PropertiesList`, `MenuPanel` tab
  colours, `ItemPickup` label fades.
- **Behaviour on prefabs that affects play:** `Projectile` (`Gravity`, `Drag`,
  `DirectHitDamageMultiplier`), `GuidedProjectile` (`Thrust`, `TopSpeed`, split and
  airburst), `Mine` (`Lifetime`, `BlastRange`, `ActivationDelay`), `TractorBeam`,
  `GridObject`. These are per-prefab instance data. `docs/headless-playground-cut.md`
  already moves simulation ownership of hits, loot and fields.
- **Run state misfiled as a component field:** `ActionGameManager.Credits`, 100000000
  in `ARPG.unity` against a code default of 15000000. It is the player's wallet
  (`TradeMenu.cs:344-385`). It is not saved, so it resets every scene load.

Fork M decides whether any of this counts as "settings".

## 2. Global shape

> **Superseded in part (2026-09-17):** wherever this map stores an engine asset as an
> `Assets/Resources/...` path or loads it with `UnityHelpers.LoadAsset`/`Resources.Load`, read
> instead: the asset's GUID under `[CultInspectorAssetGuid]`, loaded with `EngineAssets.Load<T>`,
> the asset living under `Assets/Content` (`docs/addressables-cut.md`, which lands first). That
> covers `ItemIcons`/`WeaponTypeIcons`/... (2.1), the 3.2 import's `{fileID, guid}` resolution (store
> the guid directly, no path), the body presets (the 7 `CelestialBodySettings` assets move to
> `Assets/Content`, not `Resources`), and Cut 1's picker check. Forks I and B are history.

### 2.1 Document

This is fork D, recommended (a). One catalog global in a new file,
`Assets/Scripts/ServerShared/GameSettings.cs`. The name is freed by deleting the Unity
`ScriptableObject`. Both classes cannot coexist: Assembly-CSharp would see two global
`GameSettings` types (CS0433). So the type change and the switch of every reader must be
one commit.

```csharp
[CultDocument("aetheria.gamesettings", "1"), CultGlobal, MessagePackObject]
public class GameSettings
{
    // Simulation and generation
    [Key(0)]  public GameplaySettings GameplaySettings;
    [Key(1)]  public PlanetSettings PlanetSettings;
    [Key(2)]  public ZoneGenerationSettings ZoneSettings;
    [Key(3)]  public SectorGenerationSettings SectorGenerationSettings;
    [Key(4)]  public TutorialGenerationSettings TutorialGenerationSettings;
    [Key(5)]  public NameGeneratorSettings NameGeneratorSettings;
    [Key(6)]  public SectorBackgroundSettings SectorBackgroundSettings;
    [Key(7)]  public SectorBackgroundSettings TutorialBackgroundSettings;
    [Key(8)]  public ZoneEnvironment DefaultEnvironment;
    [Key(9)]  public string StartingHullName;
    [Key(10)] public float PickupLifetime;
    [Key(11)] public float LootDropProbability;
    [Key(12)] public float LootDropVelocity;
    [Key(13)] public float WormholeDistanceRatio;
    // Presentation (authored; read only by Unity)
    [Key(14)] public float HeatstrokePhasingFloor;
    [Key(15)] public float HeatstrokePhasingFrequency;
    [Key(16)] public float DefaultViewDistance;
    [Key(17)] public float MinimapZoneGravityRange;
    [Key(18)] public float[] MinimapZoomLevels;
    [Key(19)] public int DefaultMinimapZoom;
    [Key(20)] public ExponentialCurve IconSize;
    [Key(21)] public int AsteroidMeshCount;
    [Key(22)] public float MinimapAsteroidSize;
    [Key(23)] public float MinimapIconSize;
    [Key(24)] public float PlanetRotationSpeed;
    [Key(25)] public float3 ArmorHitColor;
    [Key(26)] public float3 HardpointHitColor;
    [Key(27)] public float3 GearHitColor;
    [Key(28)] public float4[] ArmorGradient;       // xyz colour, w time; lowered by ToGradient()
    [Key(29)] public float4[] DurabilityGradient;
    // Engine asset paths (forks I and B, recommended (a)): "Assets/Resources/..." like ItemData.Icon
    [Key(30)] public string[] ItemIcons;            // [CultInspectorAssetPath] on each, indexed by enum as today
    [Key(31)] public string[] WeaponTypeIcons;
    [Key(32)] public string[] WeaponCaliberIcons;
    [Key(33)] public string[] WeaponRangeIcons;
    [Key(34)] public string[] WeaponFireTypeIcons;
    [Key(35)] public string[] WeaponModifierIcons;
    [Key(36)] public BodySettingsCollection[] BodySettingsCollections; // { [Key(0)] float MinimumMass; [Key(1)] string[] BodySettings }
}
```

**(probe)** A top-level Cult document must put an integer `[Key]` on every public member.
The CultLib contract check threw `…member GameplaySettings has no [Key]` on a
`keyAsPropertyName` probe document. Nested `[MessagePackObject(keyAsPropertyName: true)]`
classes are accepted as they are. A probe global with `[Key(0)] GameplaySettings`, filled
from the asset, wrote to a catalog and read back read-only with all 5 tiers. Legendary
came back `(1, .4, 0)`, and a re-serialize was byte-identical (1841 bytes). The existing
settings classes are kept unchanged.

`StartingHullName`, `LootDrop*` and `WormholeDistanceRatio` stay top-level. Headless
fork S wanted them inside `GameplaySettings` so there would be one reader. Once
everything reads one global, the nesting buys nothing.

### 2.2 Readers

This is fork P, recommended (a).
- **Simulation.** `ItemManager` already holds the catalog cache, so it owns the read.
  `ItemManager(CultCache itemData, ProvenanceLedger lots, Action<string> logger)` sets
  `Settings = itemData.GetGlobal<GameSettings>() ?? throw …`. It exposes
  `GameSettings Settings` and `GameplaySettings GameplaySettings => Settings.GameplaySettings`,
  so the 55 `GameplaySettings` reads in ServerShared do not change.
  `Zone` drops its `PlanetSettings` parameter (`Zone.cs:46`) and reads
  `itemManager.Settings.PlanetSettings`.
- **Unity before a run** (`MainMenu`, `VolumeSampling`): one static,
  `ActionGameManager.GameSettings => CultCache.GetGlobal<GameSettings>()`, on the same
  composition as `ActionGameManager.PlayerSettings` (`:66`). Inside a run the same
  object is also reachable as `ItemManager.Settings`. Both paths read the one record, so
  they cannot disagree.
- **Icons.** `GetIcon`/`GetIcons` move to Unity-side extension methods on `GameSettings`
  in `CultCache/UnityExtensions.cs`. They take the path through
  `UnityHelpers.LoadAsset<Sprite>` and cache the sprite by path. An empty path returns
  null, the same as today's missing sprite.
- **Tests and `AetherDb`.** No `new GameplaySettings` remains outside one test seeding
  helper. `AetherDb loadout` reads the real catalog's global. Test fixtures seed a
  `GameSettings` into their temp catalogs, which they must do anyway: `AetheriaStores`
  refuses a populated catalog without one, writable or not **(probe)**.

## 3. Data migration

### 3.1 Bootstrap of the first catalog global

**(probe)** The catalog cannot gain its first global through `AetheriaStores`:

- Open `GameData/Aetheria.cc` (a copy) with a build whose `GameSettings` type is
  `[CultGlobal]` and routed to the catalog. The **writable** open threw `…has no
  aetheria.probe.settings record; catalog globals are authored, never invented.`, and so
  did the read-only one. This is the deliberate `ab4ced0f` rule.
- With `[CultGlobal]` removed from the type, a scratch build opened the same copy
  writable. It upserted with an explicit key, `UpsertAsync(typeof(T), doc, new
  CultRecordKey("global:aetheria.probe.settings"))`, and flushed.
- The build with `[CultGlobal]` restored then opened that file **read-only** and returned
  the record from `GetGlobal`, under key `global:aetheria.probe.settings`, with the
  colours intact. CultCache classifies a global by descriptor when it hydrates
  (`CultCache.cs:1854`), not by key. The explicit key matches the one a global write
  mints (`:1540`).
- Record diff between the original and the written copy: 180 records before, 181 after;
  0 removed, 0 changed (a SHA-256 of each record's MessagePack), and 1 added. The file
  grew by 2501 bytes.

This is fork K, recommended (a): the authoring build exists only in scratch, and the
landed type differs from it by the one attribute.

### 3.2 Steps (Cut 1, **First**)

1. Close Unity; `GameData/Aetheria.cc.lock` must be free. Capture the "before" state:
   - `dotnet run --project tools/AetherDb -p:CultLibRoot=<a0813c6 worktree> -- settings > before-settings.txt`
     (the method: `dotnet run` stdout, redirected by Git Bash, no BOM);
   - a per-record SHA-256 dump of `Aetheria.cc` through `AetheriaStores.Open` (read-only
     works before the type exists).
2. Create a scratch worktree of the Cut 0 landing commit and a CultLib worktree at
   `a0813c6`. Add `GameSettings.cs` as in 2.1 **without** `[CultGlobal]`, and add it to
   `CatalogTypes`. Add a scratch `import-settings` command over `AuthoredSettings` that
   fills the document through the catalog's own types:
   - existing classes by the current `Read<T>(subtree)`;
   - `Color` `{r,g,b,a}` to `float3`, refusing when `a != 1`;
   - `Gradient` to `float4[]` from `key{i}`/`ctime{i}` for `i < m_NumColorKeys`,
     refusing unless `m_Mode == 0` and every alpha key is 1;
   - `{fileID, guid}` to an `Assets/...` path through a `.meta` guid index. `{fileID: 0}`
     and unresolved guids become `""`. Every unresolved one is printed, and the run fails
     unless the printed set equals the 9 in 1.1.
   - `BodySettingsCollections` per fork B.
   - `float.Parse(…, CultureInfo.InvariantCulture)`, **throwing** on failure. Today's
     `TryParse(...) ? f : 0f` (`AuthoredSettings.cs:131`) is the silent-zero shape this
     cut exists to kill. The probe host culture was `en-150`.
   - Write with key `global:aetheria.gamesettings` into the real `GameData/Aetheria.cc`.
3. Value check, in the same scratch build:
   - Reopen the file and walk the YAML again. Every scalar leaf (266, less the 4
     soundbank strings Cut 0 deletes), every `float3` component (15) and every live
     colour component (9) must equal the document value by path (`==` on the invariant
     parse). Every gradient key and time must match, and every icon and body path must
     match its guid's `.meta` path.
   - Print the counts compared. Exit non-zero on any mismatch or unplaced key.
   - `NeutralWandererCount == 2` (absent from YAML, carried by the initializer).
4. Record diff (`before` against `after`): exactly one record added,
   `global:aetheria.gamesettings`; 0 changed; 0 removed.
5. Copy the written `Aetheria.cc` into the main tree. It lands in the Cut 1 commit
   together with the type (with `[CultGlobal]`). Remove the scratch worktrees.

## 4. Cuts

### Cut 0. Cut the settings writers and the dead settings

- **Repo/branch:** Aetheria `codex/item-provenance` from `00ebe46a`. No dependencies.
  Unity is closed for the asset edits.
- **First:** `rg -n "Settings\.\w*BackgroundSettings\.NoisePosition" Assets/Scripts`
  returns 2 hits at HEAD. Run `dotnet test tests/Aetheria.Shared.Tests` for a baseline.
- **Deletes first:**
  - `GameSettings.cs:13-16` (soundbanks) and `:42` (`HullHitColor`); `Settings.asset:15-18,327`.
  - `ActionGameManager.cs:228-232` (`NewEntitySettings`).
  - `EntitySerializer.cs:51-52` (the copy; `pack.Settings ??= itemManager.GameplaySettings.DefaultEntitySettings`).
  - Fork X (a): `Assets/Resources/{Atmosphere Settings,Volume Settings,Elysium,Test Galaxy,Icons}.asset` and their `.meta` files.
- **Per-file changes:**
  - `Entity.cs:340`: `Settings = MessagePackSerializer.Deserialize<EntitySettings>(MessagePackSerializer.Serialize(settings))`.
    The entity owns its copy, so every construction path is safe, including
    `Loadout.cs:131`, `LoadoutGenerator.cs:46,61,76`, `EntitySerializer.Unpack` and
    `TradeMenu.cs:377`.
  - `TradeMenu.cs:377`: pass `GameManager.ItemManager.GameplaySettings.DefaultEntitySettings`.
  - `MainMenu.cs:125-157`: take a run-owned copy of the background (the same MessagePack
    round trip), set `NoisePosition` on the copy, search the copy (`:146-150`), and pass
    the copy to `Galaxy`. The authored object is never assigned.
- **Authority map:**
  - Owner: `Entity` owns its `EntitySettings` instance. `Galaxy` (and so `SavedGame`)
    owns the run's background position.
  - Inputs: the authored defaults, read only.
  - Outputs: per-entity and per-run copies.
  - Derived state: `DefaultEntitySettings` and `*BackgroundSettings` are template-only.
  - Forbidden writers: `InventoryMenu.cs:175` may write only the entity's own copy.
    `MainMenu` never writes the settings.
  - Shared paths: new ship (`LoadoutGenerator`), preset (`Loadout.Build`), bought ship
    (`TradeMenu`) and Continue (`EntitySerializer.Unpack`) all go through the `Entity` ctor.
  - Deletion line: `NewEntitySettings` and the serializer copy are gone before the ctor copy lands.
- **Verification:**
  - builds: `Aetheria.Shared`, `tests/Aetheria.Shared.Tests` (`-p:CultLibRoot=`); Unity batchmode compile.
  - tests: `EntitiesDoNotShareDefaultEntitySettings` (LoadoutTests fixture) pins "no entity
    aliases the template". Generate two ships, write `ShutdownPerformance` on one, then
    assert that the other and `GameplaySettings.DefaultEntitySettings` are unchanged. It
    fails when `Entity.cs:340` goes back to `Settings = settings`.
  - negative:
    - `rg -n "Settings\.\w*BackgroundSettings\.NoisePosition" Assets/Scripts` returns 0.
    - `rg -n "NewEntitySettings|Soundbank|SoundBank|HullHitColor" Assets/Scripts` returns 0
      (at HEAD it hits only the lines deleted here).
    - `git ls-files "Assets/Resources/*.asset"` lists only `Settings.asset` and
      `InputSystem.inputsettings.asset`.
  - operator: open the Inventory ship settings and change Shutdown Threshold. A
    generated NPC ship's threshold must not move.

### Cut 1. Move the settings into the catalog global

- **Repo/branch:** Aetheria `codex/item-provenance` from the Cut 0 landing. Depends on
  forks K, I, B, D and P. It is one commit, because the type rename forces the reader
  switch (2.1).
- **First:** section 3.2, steps 1-4. The written `Aetheria.cc` is in hand before any
  main-tree edit.
- **Deletes first:**
  - `Assets/Scripts/Gameplay/GameSettings.cs` (+`.meta`; `BodySettingsCollection` moves per fork B).
  - `Assets/Resources/Settings.asset` (+`.meta`).
  - `Assets/Scripts/Editor/GameplaySettingsEditor.cs` (+`.meta`; 100 lines; Studio edits the record).
  - The 7 serialized `public GameSettings Settings;` fields: `ActionGameManager.cs:99`,
    `SchematicDisplay.cs:14`, `SchematicListElement.cs:9`, `MainMenu.cs:20`,
    `InventoryPanel.cs:26`, `VolumeSampling.cs:22`, `ZoneRenderer.cs:27`. Also their 15
    YAML reference lines (`rg -n "cd2344e2001a37544aab0960c164d4ff" Assets -g "*.unity" -g "*.prefab"`),
    text-edited with Unity closed, and the stale `Sensitivity` block at
    `ARPG.unity:16548-16550`.
  - `tools/AetherDb/AuthoredSettings.cs` (174 lines); `Program.cs:30-31,33` (the
    `settings` and `settings-dump` commands and help text) and `:174-228`; the `YamlDotNet`
    reference (`AetherDb.csproj:16`); `Program.cs:437-442` (the hand-built settings).
  - `ItemManager.cs:28,32` (the settings parameter); `Zone.cs:46` (the `PlanetSettings` parameter).
  - Tests: `RunSaveTests.cs:313-319` and `IffAndCombatTests.cs:62-70` (`TestSettings`
    copies); `AetheriaStoresTests.cs:11-14` (`TestCatalogGlobal`, the stand-in for
    "the game has none yet").
- **Adds:**
  - `ServerShared/GameSettings.cs` (2.1). `AetheriaStores.cs:9`: add `typeof(GameSettings)` to `CatalogTypes`.
  - The icon extensions in `CultCache/UnityExtensions.cs` (2.2).
  - One test seeding helper. It builds `GameSettings { GameplaySettings = {DefaultEntitySettings, one Common tier, QualityPriceModifier}, PlanetSettings = new() }`
    with an optional tweak, and each fixture upserts it where it seeds its temp catalog:
    `LoadoutTests.cs:27`, `IffAndCombatTests.cs:23-24`, `AetheriaStoresTests.cs:27-28`,
    and `RunSaveTests.cs:23`.
  - `LoadoutTests.StatsReadTheLot` (`:495-503`) seeds its two tiers through the helper
    instead of mutating a live settings object.
  - `IffAndCombatTests` seeds `TargetDetectionInfoThreshold = .5f` the same way.
  - `GameData/Aetheria.cc`: +1 record.
- **Per-file changes** (reader switch; the member paths after `Settings.` do not change):
  - `ActionGameManager.cs:203,280-284,313,671,685,772,776,847,1208-1292` use `ItemManager.Settings`
    inside the run. `:230` goes with Cut 0. `:263` becomes
    `new ItemManager(CultCache, RunSave.Lots(CultCache), Debug.Log)`. `:690` becomes
    `new Zone(ItemManager, galaxyZone.PackedContents, …)`. Add the static
    `GameSettings` accessor next to `PlayerSettings` (`:66`).
  - `ZoneRenderer.cs:147-572` (22 lines) use `ItemManager.Settings` (`ZoneRenderer.ItemManager`, `:95`).
    `:397` follows fork B. `:497` and `:412` are unchanged apart from the source.
  - `EntityInstance.cs:413,417,428`: `ZoneRenderer.Settings.LootDrop*` becomes `Entity.ItemManager.Settings.LootDrop*`.
  - `MainMenu.cs:125-158` and `VolumeSampling.cs:54` use `ActionGameManager.GameSettings`.
  - `InventoryPanel.cs:497-499`: `.ToColor()` on the `float3`. `:790,792`: build
    `ToGradient()` once per panel and keep it, instead of one gradient per call.
  - `SchematicDisplay.cs:169`, `SectorRenderer.cs:71-72`, `WeaponGroupAssignment.cs:25`,
    `ActionBarSlot.cs:155-156` and `SchematicListElement.cs:27-44` read through `ActionGameManager.Instance.ItemManager.Settings`.
  - `Zone.cs:46-…`: `Settings = itemManager.Settings.PlanetSettings`. The callers are
    `IffAndCombatTests.cs:58` and `RunSaveTests.cs:93`.
  - `tools/AetherDb/Program.cs:444`: `new ItemManager(db.Cache, new ProvenanceLedger(), log.Add)`.
    The loadout command now runs on the authored tiers and price curve.
  - `AetheriaStoresTests`: the refusal tests (including `WritablePopulatedCatalogMissingGlobalIsLoud`)
    remove and expect `GameSettings` instead of `TestCatalogGlobal`.
- **Authority map:**
  - Owner: the catalog record `global:aetheria.gamesettings`, authored in CultCache Studio.
  - Inputs: none at runtime. It is authored data.
  - Outputs: `ItemManager.Settings` (sim, in-run Unity) and `ActionGameManager.GameSettings` (pre-run Unity).
  - Derived state: Unity `Gradient`, `Color` and `Sprite` objects are lowering-only, built
    from the record. Entity and run copies come from Cut 0.
  - Forbidden writers: any runtime assignment into the record. The Unity serializer
    (asset deleted). `GameSettingsEditor`. `AuthoredSettings` and YAML. Hand-built
    `GameplaySettings` in `AetherDb`.
  - Shared paths: new game, Continue, the tutorial, the headless tests and `AetherDb` all
    construct `ItemManager` over a cache holding the record, so they all read the same one.
  - Deletion line: the `ScriptableObject`, the asset, its editor, the YAML reader and the
    settings constructor parameters are gone in the same commit that adds the global.
- **Verification:**
  - builds: `Aetheria.Shared`; `tools/AetherDb`; `tests/Aetheria.Shared.Tests` (`-p:CultLibRoot=`
    at `a0813c6`, `--artifacts-path` in scratch); Unity batchmode compile (Assembly-CSharp and Editor).
  - data: the 3.2 value check prints its compared counts with 0 mismatches, and the record diff is +1/0/0.
  - tests:
    - `ItemManagerReadsTiersFromTheCatalogGlobal`: two temp catalogs whose `GameSettings`
      differ only in `Tiers` give different `GetTier` for the same lot. Pins one owner.
      It cannot be written against a constructor that takes settings.
    - `ItemManagerRefusesACacheWithoutGameSettings`: a bare `CultCache` makes the
      constructor throw. Pins loud absence, never defaults.
    - `MissingCatalogGlobalIsLoud`, `WritablePopulatedCatalogMissingGlobalIsLoud` and
      `GameShapedOpenMissingGlobalIsLoud` (`AetheriaStoresTests.cs:116-143`, retargeted). Pins "authored, never invented".
    - `ShippedCatalogSettingsAreNotZero`: opens `GameData/Aetheria.cc` read-only (root
      found as in `AetherDb.FindRoot`) and asserts every `RarityTier.Color` has a
      nonzero component, the hit colours are nonzero, each gradient has at least 2 keys,
      `MinimapZoomLevels` is non-empty, and `PlanetSettings.GravityStrength > 0`. Pins
      "no authored value silently loads as zero" (this cut's triggering defect). Exact
      values belong to the one-time 3.2 check, not to a test that future authoring would break.
  - negative (hit counts at `00ebe46a` in brackets; each must be 0 after):
    - `rg -n "\bGameSettings Settings\b" Assets/Scripts` [7]
    - `rg -l "0d957fc3cdc9310499a3a494a317fe0a|cd2344e2001a37544aab0960c164d4ff" Assets -g "*.unity" -g "*.prefab" -g "*.asset"` [4]
    - `rg -n "AuthoredSettings|YamlDotNet" tools -g "!**/obj/**" -g "!**/bin/**"` [9]
    - `rg -n "Settings\.asset" -g "*.cs" .` [2]
    - `rg -n "TestCatalogGlobal" tests` [7]
    - `rg -n "new GameplaySettings\b" tools Assets/Scripts` [1]. In `tests`, exactly 1
      (the helper) [2 now].
    - `rg -n "ScriptableObject" Assets/Scripts/ServerShared` must stay 0.
  - operator:
    - Play smoke: main menu, then New Game (tutorial). The tutorial galaxy generates,
      and the starting ship is LonginusX.
    - In a station trade menu and the properties panel, item names show tier colours:
      Legendary orange, Uncommon green. None is black.
    - The inventory shows armour and durability gradients, and hit flashes use the
      authored colours: armour white, hardpoint pale blue, gear pale red.
    - The minimap cycles 5 zoom levels. Planets render with body presets. Loot drops and
      pickups expire.
    - Action bar and schematic weapon icons appear. Modifier icons stay blank, as they
      are today (1.1).
    - Continue after save.
    - Studio click-through: the `aetheria.gamesettings` record opens, nested curves and
      tiers edit, icon fields show the asset-path picker, and the edit persists.
- **Operator questions:** forks K, I, B, D and P (section 6).

## 5. Subtraction ledger (estimate)

| Cut | Removed | Added | Dependencies, formats, targets |
|---|---|---|---|
| 0 | ~12 C# lines; 5 dead fields; 5 orphan assets (~18 KB YAML); 1 dead copy path | ~12 C# lines; 1 test | none |
| 1 | `GameSettings.cs` 88, `GameplaySettingsEditor.cs` 100, `AuthoredSettings.cs` 174, `Program.cs` ~65, `Settings.asset` 477 YAML lines, 7 serialized fields plus 16 scene YAML lines, 2 `TestSettings` copies ~15, `TestCatalogGlobal` 4, 2 settings ctor parameters | `ServerShared/GameSettings.cs` ~45, icon extensions ~30, test helper ~15, tests ~70, +1 catalog record (~2.5 KB) | removes the `YamlDotNet` dependency and one Unity asset format (settings YAML); no targets added |

## 6. Operator forks (ordered by how much they block)

> **Superseded in part (2026-09-17):** wherever this map stores an engine asset as an
> `Assets/Resources/...` path or loads it with `UnityHelpers.LoadAsset`/`Resources.Load`, read
> instead: the asset's GUID under `[CultInspectorAssetGuid]`, loaded with `EngineAssets.Load<T>`,
> the asset living under `Assets/Content` (`docs/addressables-cut.md`, which lands first). That
> covers `ItemIcons`/`WeaponTypeIcons`/... (2.1), the 3.2 import's `{fileID, guid}` resolution (store
> the guid directly, no path), the body presets (the 7 `CelestialBodySettings` assets move to
> `Assets/Content`, not `Resources`), and Cut 1's picker check. Forks I and B are history.

**K. How the first catalog global gets written.** Blocks Cut 1.
- (a) A scratch-only authoring build without `[CultGlobal]` writes the record under the
  explicit `global:` key, and the landed type adds the attribute (probed, 3.1). No landed
  code bypasses the refusal.
- (b) Loosen `AetheriaStores` so writable opens skip the refusal. This reverses the
  `ab4ced0f` reasoning ("a missing global went silent in play mode").
- (c) Land a one-shot `import-settings` command with a bypass flag on `Open`, then
  delete both. A guardrail hole ships, even if briefly.
- **Recommend (a).**

**I. Icon sprites (engine references) in the global.** Blocks Cut 1.
- (a) Asset-path strings with `[CultInspectorAssetPath]`, loaded through
  `UnityHelpers.LoadAsset<Sprite>`. This is what `ItemData.Icon`/`ActionBarIcon`,
  `Weapon`, `Thruster` and `AetherDrive` particles already do, and all 33 live sprites
  already sit under `Assets/Resources`. The 9 dangling refs become empty strings.
- (b) Keep a slim Unity `ScriptableObject` holding only the sprite arrays. This keeps
  Unity serialization for this data, against the ruling's direction.
- (c) Put the sprite arrays on each consuming component. Several prefab instances would
  each duplicate them.
- **Recommend (a).** Separately, the operator may want to know that modifier icons are
  already broken (9 missing assets).

**B. `BodySettingsCollections` (mass thresholds plus `CelestialBodySettings` plugin assets).** Blocks Cut 1.
- (a) Thresholds and asset paths go in the global, like fork I. The 7 referenced assets
  move, with their `.meta` files so guids are kept, from `Assets/Plugins/Celestial Body/Solar System/…`
  to `Assets/Resources/Celestial Bodies/`, because `Resources.Load` needs them there.
  Their dependencies follow by guid.
- (b) Serialized field on `ZoneRenderer`, the only reader. Thresholds and references stay
  in `ARPG.unity`, with no asset moves. Unity keeps authoring a setting.
- **Recommend (a).** One rule ("engine assets are referenced from globals by path") is
  cheaper to explain than an exception. The 7 moves are a one-time cost.

**D. Document shape.** Blocks Cut 1.
- (a) One catalog global `GameSettings` (2.1). It keeps today's single authoring
  surface, every existing settings class, and every `Settings.X` member path.
- (b) Split by consumer: `GameplaySettings`, `GenerationSettings` and `PresentationSettings`
  globals. It gives narrower ports, but nothing needs isolating yet, and it means three
  records and a wider reader diff.
- **Recommend (a).** A later split is a mechanical re-key if a consumer ever needs one.

**P. Simulation port.** Blocks Cut 1.
- (a) `ItemManager` and `Zone` read the global from the cache they already hold, and
  their settings constructor parameters go (2.2).
- (b) Keep the parameters, with callers passing `cache.GetGlobal<GameSettings>().…`. The
  sim could then run on settings the catalog does not hold, and tests and `AetherDb`
  could keep hand-building them. That is the duplication this cut exists to delete.
- **Recommend (a).** The headless-playground `Run.New` signature shrinks with it.

**M. Is component tuning on scene and prefab instances "settings"?** Does not block Cut 0 or Cut 1.
- (a) No. It is instance data of presentation components (1.5) and stays Unity-serialized.
  Gameplay-affecting prefab values (`Projectile`, `Mine`, `GuidedProjectile`) move when
  the headless-playground map moves hit and loot ownership into the sim.
  `ActionGameManager.Credits` is run state, not a setting, so it goes to follow-ups.
- (b) Yes. Each component's tuning becomes a catalog global or a per-prefab catalog
  record. That is a second authoring surface per component and a much larger cut.
- **Recommend (a).**

**X. Dead authored data.** Low blocking; affects Cut 0 only.
- (a) Delete the 4 soundbank names, `HullHitColor` and the 5 orphan `Resources` assets.
  Their bytes stay in git history (1.2 names the commits), and `Elysium`/`Test Galaxy`
  hold parameters of a generator deleted in 2021.
- (b) Migrate them into the global or a catalog record anyway.
- **Recommend (a).** Nothing reads them. They are not pre-breach content in the sense of
  `GameData`; they are Unity assets whose scripts are gone.

## Follow-ups outside this migration

- `ActionGameManager.Credits` is not saved and resets per scene load (`ActionGameManager.cs:101`,
  `ARPG.unity`: 100000000). It belongs in the run store with `SavedGame`.
- `NoisePosition` is run state inside a settings class (`Settings.cs:62`). After Cut 0 the
  authored value is only an unused seed. It could move to `SavedGame`/`Galaxy`.
- `StartingHullName` and the tutorial faction names are name strings, not `CultRecordRef`s,
  so `AetherDb dangling` cannot see them.
- `GameplaySettings` mixes presentation (`LockIndicator*`, `MessageDuration`) with sim rules.
- `TargetArmorInfoThreshold` and `TargetGearInfoThreshold` are authored and unread
  (recorded in the headless map).
- 9 dangling icon references (1.1).
