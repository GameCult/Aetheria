# Aetheria Release: Map

Date: 2026-10-03

This map holds body facts, the model page and rationale for campaign
`aetheria-release`. It has no progress section, no per-cut sections, no rulings
and no ledger: those are typed documents in the Eureka mind. Ends live in
`docs/aetheria-release-target.md`. The Eyes inventory this pass started from is
`F:\Projects\aetheria-release-inventory.md`, which is outside the repo.

Every anchor below is against `origin/master` `f1dee184` and was read in a sparse
scratch worktree on 2026-10-03, unless the line or its section says otherwise. The section "Demo scope" holds
the 2026-10-06 re-scope: the release this campaign plans is the demo.

## Body facts

**B1. No impact event carries a world point.**
- `Entity.IncomingHit` (`ServerShared/Entity.cs:138`) carries only the source.
- `ArmorDamage` (`:139`) carries a schematic cell. `ItemDamage` (`:140`) carries the
  item. `HullDamage` (`:143`) carries the amount.
- `Zone.ShotCommitted` and `ShotResolved` (`Zone.cs:41-42`) carry a `ShotOutcome`
  (`FireControl.cs:1902-1930`), which holds a schematic cell, bearing and lateral
  offset but no world point.
- The world point of a direct hit is never computed. `ApplyDirectHit`
  (`FireControl.cs:944-998`) walks lanes in the schematic frame. A
  contact or delayed blast computes it at `:1030` and `:1043` and passes it to
  `Detonate`. `Detonate` (`:1341-1414`) has the world point and the share each entity
  took, and publishes neither.
- So nothing can show a hit at the place it landed without recomputing
  `FireControl`'s geometry.

**B2. Weapon hits on a shield show nothing.** `ShieldManager.ShowHit`
(`Gameplay/ShieldManager.cs:88`) has one caller, ship-on-ship collision (`:80`).
`FireControl` calls `Shield.TakeHit` and `Break` (`FireControl.cs:952-956`,
`:1378-1385`) with no presentation hook. The inventory's "shield impact: present"
refers to the shield-panel rig, which `FieldTester` drives in `FieldShieldTest.unity`.
It does not run in the game.

**B3. A capability event contract already exists for this.** `CapabilityEvents`
(`ServerShared/CapabilityEvents.cs`) defines `AbsorbEvent` (position, direction,
magnitude, damage type) and one `Subject` per event kind.
`CapabilityPresentationBinder` (`Gameplay/CapabilityPresentationBinder.cs`) wires
`IAbsorbPresenter` components on an object and its children. The contract's own
comment says the simulation will own and publish these events, and presentations
will subscribe and decide nothing (ruling fork L, 2026-09-17). Today its only
publisher is `FieldTester`. `AbsorbEvent` has no way to tell a shield absorb from a
hull absorb.

**B4. The weapons already announce what they do.**
- `InstantWeapon.OnFire(shotId)` (`Behaviors/InstantWeapon.cs:272`) fires only for a
  round that was not refused.
- `ChargedWeapon` has `OnStartCharging`, `OnStopCharging`, `OnCharged` and
  `OnFailed` (`ChargedWeapon.cs:87-90`).
- `ConstantWeapon` has `OnStartFiring` and `OnStopFiring`.
- `EntityInstance.SetEntity` subscribes the effect managers to all of these
  (`Gameplay/EntityInstance.cs:209-240`).

**B5. The Wwise-shaped audio path is dead at its source.**
- `EquippedItem.SoundBank` (`Entity.cs:1353`) is never assigned outside two tests
  (`FireControlPerWeaponTests.cs:424`, `:672`).
- So every `FireAudioEvent` and `SetAudioParameter` call returns at its null check.
  The calls sit in `InstantWeapon.cs:275`, `ChargedWeapon.cs:137,154,169,185`,
  `Reactor.cs:97`, `Thruster.cs:96` and `AetherDrive.cs:170-172`.
- No production code subscribes to `EquippedItem.AudioEvents` (`:1375`).
- `WwiseSoundBinding` and `WwiseLoopingSoundBinding` (`WwiseSoundBinding.cs`) have
  zero use sites.
- `GameData/SoundbanksInfo.json` (2,648 lines) is read by no code. Only
  `docs/build-delivery-cut.md:231` mentions copying it.
- `EntityInstance.cs:243-251` reads `item.Data.SoundBank` and does nothing with it.
- Catalog fields that hold Wwise ids:
  - `EquippableItemData.SoundBank` (`ItemData.cs:403`);
  - `AetherDriveData.RpmAudioParameter` and `TorqueRatioAudioParameter`
    (`AetherDrive.cs:44-48`, keys 11 and 12);
  - `Faction.OverworldMusic`, `CombatMusic` and `BossMusic` (`Corporations.cs:52-59`,
    keys 13 to 15).

**B6. Unity audio is switched off and has no listener.**
`ProjectSettings/AudioManager.asset` has `m_DisableAudio: 1`. A YAML grep found
`AudioListener:` count 0 in `ARPG.unity` and in `Main Menu.unity`; the two test
scenes have one each. The editor is Unity 6000.3.24f1
(`ProjectSettings/ProjectVersion.txt`).

**B7. The 2021 sound library is on disk and usable.**
- The Wwise project is `Aetheria-Economy_WwiseProject.wproj`, `WwiseVersion="v2021.1.1"`
  and `SchemaVersion="103"`.
- Its 225 `.wav` are in LFS and present in the operator's checkout:
  `Originals/SFX` holds 731 MB.
- The SFX include fire, hit and miss for cannon, ioncannon, lightingGun, machinegun
  and railgun. They also include charge and charge-fail, ship destroyed (two), and
  shield up, down and active. Overheat alarm, docking, undocking, wormhole, reactor
  states and sensor ping are there too.
- Interface sounds are `blip`, `equip`, `mouseover`, `pickup`, `putdown`, and trade
  buy and sell.
- Music: menu, Gen overworld and combat, Metal overworld, combat and boss, Daya
  overworld, Kawaii Future Bass. There are also 30 ambience layers.
- The 20 `Originals/Plugins/SoundSeed Grain` sources feed a Wwise plugin, and
  their procedural behaviour does not carry over outside Wwise.
- `*.wav` is an LFS pattern in `.gitattributes`.

**B8. The camera has the tooling for shake; nothing uses it.**
`com.unity.cinemachine` 2.10.7 is in `Packages/manifest.json`. It ships
`CinemachineImpulseSource` and `CinemachineImpulseListener`. A grep found no
`Impulse` and no `shake` in `Assets/Scripts`. The follow camera is
`ActionGameManager.FollowCamera` (`ActionGameManager.cs:122`).

**B9. Graphics settings are not saved.**
- `MainMenu.ShowGraphicsSettings`'s Back button (`UI/MainMenu.cs:258-263`) never calls
  `ActionGameManager.SavePlayerSettings()`.
- Gameplay's Back button does call it (`:236-241`).
- `PlayerGraphicsSettings` (`ServerShared/PlayerSettings.cs:63-67`) holds keys 0 and 1.
- `PropertiesPanel.AddField(name, float read, write, min, max)` exists
  (`UI/Properties Panel/PropertiesPanel.cs:290`).

**B10. The lanes overlap the feedback files in these hunks** (`git diff
origin/master...origin/<lane>` hunk headers):
- **mining** (`83d8371e`):
  - `FireControl.cs` at `:72` and `:522`;
  - `Entity.cs` at `:43`, `:198-240`, `:295-395` and `:1075`;
  - `EntityInstance.cs` at `:236` and `:410`;
  - `ActionGameManager.cs` across `:362-1432`, including `:1076-1100`;
  - `ZoneRenderer.cs` and `Zone.cs`.
- **scenarios** (`aa3baf12`):
  - `Entity.cs` at `:204`, `:601` and `:786-1003`;
  - `ActionGameManager.cs` at `:583`, `:732` and `:801`.
- **moddable-ships** (`a93625c2`):
  - `ActionGameManager.cs` at `:42-70`;
  - `ZoneRenderer.cs`;
  - `Packages/manifest.json`, which adds glTFast.

`feedback-1`'s edits are disjoint from these: `FireControl.cs:944-1045` and
`:1375-1412`, `Entity.cs:138-150`, and `EntityInstance.cs:159-170`.

**B11. The headless tests compile only `ServerShared`.** That covers
`Aetheria.Shared/Aetheria.Shared.csproj:20` and `tests/Aetheria.Shared.Tests`. A rule
that must be pinned headless must live in `ServerShared`.

## Model page

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Impact (`AbsorbEvent` with a layer) | Transient. The receiving entity's `Capabilities` stream, plus the shot. It is not stored. | Published once per landed hit, before the hull damage is applied. It is never replayed or persisted. | `FireControl` alone, in `ApplyDirectHit` and `Detonate`. Presenters only read it. |
| Impact cue | Derived from one impact and the receiver. | Computed at presentation time and discarded. | `Feedback` (engine-free, `ServerShared`). Visual and audio presenters render it and do not re-derive it. |
| Player feedback preferences (shake, volumes) | `PlayerSettings` global, player store, keys under `GraphicsSettings` (shake) and a new `AudioSettings` (volumes). | Edited in the settings menu and saved on Back. A new key defaults on load. | The player, through the menu. No runtime writer. |
| Presentation tuning (effect prefab, intensity scale, impulse shape, sound bank) | A Unity component or asset in `Assets/Content`. | Authored in the editor and versioned with the scene. | The operator. It stays Unity-serialized by fork M (a). |
| Sound assets | The file under `Assets/Audio`, copied from the Wwise `Originals`. Provenance is recorded per file. | Added per cut. The Wwise project stays as the source archive until `audio-route` and `audio-source-rights` are ruled. | The operator, on rights (`audio-source-rights`). |
| Ship content (`ShipAuthoring` with GLB) | `aetheria.ship_authoring` id. GLB nodes carry an `aetheria.id` extra. | Adopted by this campaign (ruling `adopt-moddable-ships`). Rows for the package, its anchors and the derived catalog are in "Content: the moddable-ships lane" below. | The moddable-ships rulings (MQ1-MQ5) and this campaign's content strand. |

No cell is empty for the two mapped cuts. The content rows are mapped in their own
section below.

## Rationale

**Why the impact is published by `FireControl` and not derived in Unity.** B1 shows
that the world point exists only inside `FireControl`'s lane walk and blast
resolution. If a presenter derived it, it would have to recompute lanes from the
`ShotOutcome`. That rebuilds the second hit authority that fire control Cuts 3 to
12.4(b) deleted. So the owner of where a round landed publishes where it landed.

**Why reuse `CapabilityEvents` rather than a new subject.** The contract was ruled
for exactly this (B3): the simulation publishes absorbs and presenters subscribe.
Its binder already exists. Reusing it adds one field (the layer) instead of a
second event family. It also brings the shield-panel presenters within reach of real
hits later, with no new wiring.

**Why the cue derivation lives in `ServerShared`.** Intensity and shake are rules
("in proportion to the damage", "zero at zero preference"). B11 says only
`ServerShared` is under headless test and Stryker.NET. Keeping the function
engine-free puts the rules where mutation testing reaches them. Audio then renders
the same cue (target: one cue, two channels).

**Why native Unity audio is recommended** (question `audio-route`):
- The simulation already announces fire, charge, impact and death (B4 and
  `feedback-1`).
- The Wwise path is dead at its source (B5).
- Unity audio is only switched off (B6).
- Of the Wwise project, what carries over is the wav files (B7). The
  2021.1 project, its events, banks and SoundSeed plugin sources would need
  migrating to a Wwise release that supports Unity 6.
- Wwise adds three costs:
  - a native plugin per platform;
  - a second authoring application;
  - a sound-bank build step. Its authoring console runs on Windows and macOS, not
    on Yggdrasil's Linux.

  Agents cannot easily author Wwise work units, and the operator's AquaSynth renders
  would have to pass through it.
- Licensing is not the obstacle. Wwise is free under a $250K development budget,
  with no asset limit (Audiokinetic pricing page and indie-license blog). FMOD is
  free under $200K yearly revenue and a $500K budget.
- What the middleware would buy is interactive music and parameter-driven
  mixing, authored by a sound designer. The release bar asks for neither yet.
- Unity's limits are known: 32 real voices by default (`AudioManager.asset`
  `m_RealVoiceCount`), no authored containers or RTPC graph, and mixer assets that
  cannot be created from script.
- Unity 6's `AudioRandomContainer` has no public scripting API for its clip list.
  The first cut therefore uses a small `ScriptableObject` sound bank with random
  pick and jitter.

Confidence: medium-high. It falls to medium if the operator expects a contracted
sound designer working in Wwise, or adaptive music with transitions and stingers
before release.

**Why content runs in parallel.** The release quota is met at the operator's pace,
one hull a day, and that clock starts only when the first Tripo mesh flies. The steps
in front of it are the operator's owed S2 play checks, the moddable-ships merge, and
the add-on's package action. Each is cheap and none depends on feedback or audio.
Serializing them behind audio would idle the slowest resource in the project.

**Sources consulted for the audio fork:**
- https://www.audiokinetic.com/en/wwise/pricing/
- https://www.audiokinetic.com/en/community/blog/free-wwise-for-indie-developers/
- https://www.gamedeveloper.com/audio/small-developers-and-creators-can-now-use-fmod-studio-for-free
- https://gamefromscratch.com/fmod-studio-now-free-for-indie-game-developers/

Two claims were not verified from primary sources on 2026-10-03: which Wwise
release first supports Unity 6000.x, and the scripting surface of
`AudioRandomContainer`.

## Demo scope

Re-scope of 2026-10-06 (rulings `first-release-is-a-demo`, `demo-cut-fixed-region`,
`demo-cast`, `miss-terris-belongs-to-emily-r3`, `ship-authors-need-no-unity`). The
release this campaign plans is the demo: one gate, one region, one boss, a fixed cast.
The design review that prompted it is `F:\Projects\aetheria-game-design-review-2026-10-05.md`,
outside the repo. Anchors in this section are against `origin/master` `df7c44f2` (the
mining merge), read with `git show` and `git grep` from the Aetheria object store on
2026-10-06.

### Body facts

- **D1. A fixed-cast generator already exists: the prelude.** `Galaxy.cs:150-262` builds a
  galaxy from named factions: protagonist, antagonist, buffer, quest and neutrals,
  resolved by name prefix (`ResolveFaction`, `:141-144`). It places homes so the
  antagonist is far from the protagonist and the buffer between them, and sets the
  entrance near the protagonist's home. Its settings are
  `Assets/Resources/Settings.asset` `TutorialGenerationSettings` (an LFS object, read with
  `git lfs smudge`): protagonist `Miss`, antagonist `Zhe`, buffer `Luc`, neutrals `Aero`
  and `Finch`, quest `Adras`, 64 zones. `TutorialGalaxy` stages it. The main galaxy
  (`Galaxy.cs:96-131`) draws `MegaCount` 12 factions at random from the catalog, places
  `BossCount` 3 boss zones, and has 128 zones. A prelude galaxy guarantees a station at
  its entrance (`ZoneGenerator.cs:223-224`) and lets generation use any product
  (`LoadoutGenerator.cs:184`).
- **D2. Boss zones and the exit exist only in the main galaxy.** `PlaceFactionsMain`
  (`Galaxy.cs:265-309`) fills `BossZones` on chokepoints; the main constructor sets
  `Exit` to the most isolated zone (`:123`). The prelude sets neither. Both persist
  (`SavedGame` keys 3 and 5).
- **D3. Nothing spawns a boss.** `Faction.BossHull` is key 10
  (`Corporations.cs:43-44`) and is unset on all 12 catalog factions. Outside the field
  and `Galaxy.BossZones`, `BossHull` is read only by `tools/AetherDb`. `ZoneGenerator`
  generates stations, turrets, faction ships and wanderers (`:280-365`) and no boss.
- **D4. Wormholes are built in Unity, and the exit does nothing.** `ZoneRenderer.cs:225-236`
  adds one `Wormhole { Position, Target }` per adjacent zone. Interact
  (`ActionGameManager.cs:321-327`) enters any wormhole in range; `EnterWormhole`
  (`:660`) populates the target zone. Nothing checks `Galaxy.Exit`.
- **D5. Station services are buy-only and credits are not saved.** `TradeMenu` has
  `Buy(CraftedItemInstance)` (`:366`) and `Buy(SimpleCommodity, int)` (`:405`) and no
  sell. "Repair" appears in no gameplay or UI script (only AetherDb comments).
  Credits are `public int Credits = 15000000` on `ActionGameManager` (`:114`);
  `SavedGame` (keys 0-12) does not carry them, so Continue resets them. The hull branch
  of `Buy` checks `GetPrice` (`:369-370`) and charges `data.Price` (`:378`). Durability
  is per instance (`ItemInstance.cs:53`) against the design's (`ItemData.cs:371`), and
  falls under fire (`Entity.cs:645-664`).
- **D6. The catalog has no Pirates and the demo cast sells no ship hull.** Decoded from
  `git show df7c44f2:GameData/Aetheria.cc | git lfs smudge` with Python `msgpack`, using
  the store's own schema table for slot names. Factions (12): Zhestokost, Finch
  Cybernetics, Lightsail Express, NiteLife Energy, Death Monkey Explosives, Aeronautics
  Unlimited, Lucent Media, Alakrita, Ewan Hart Inc, Rossum & Douglas, Adrasteia, Miss
  Terri's. Hulls (5): Longinus and LonginusX (Alakrita, ship), Djinni (Rossum &
  Douglas, ship), Zenith (AU, station), Turret (Zhestokost, turret). 64 products; by
  maker AU 11, Lightsail 9, NiteLife 9, Lucent 7, Death Monkey 6, Zhestokost 6,
  Rossum & Douglas 6, Alakrita 5, Finch 2, Miss Terri's 1, Adrasteia 1, Ewan Hart 1.

### Why the demo is a prelude galaxy

D1 is already the demo's shape: named factions in fixed roles, a small galaxy, an
entrance station. The demo needs a different cast from the tutorial (the Pirates as the
ally, not Miss Terri's), a boss zone and an exit. So the demo is a compiled scenario
whose script carries its cast and calls the prelude constructor, then places the gate at
the antagonist's home (`cut-demo-galaxy`). The tutorial's settings and Miss Terri's
prelude role are untouched. A demo mode flag on the galaxy was rejected: the scenario
script is the owner the scenario rulings already name.

The gate is in the boss zone itself: an exit wormhole there opens when the zone's boss
is dead (`cut-boss-gate`). Locking every wormhole out of a boss zone, as Three Gates
item 2 describes, is the full game's three-section rule; the demo needs one gate and
lets the player retreat.

### Scope sort

Every cut spec and follow-up in force under root `aetheria-release` on 2026-10-06, after
this pass's admissions. "Landed" means the spec has a report; it stays in force as a
record.

| Kind | Id (local) | Scope | Reason |
|---|---|---|---|
| spec | `demo-galaxy` | demo | The demo scenario, its cast and the gate's placement. |
| spec | `boss-gate` | demo | The one boss and the sealed gate; the win. |
| spec | `station-services` | demo | Sell, repair and saved credits: the run pays. |
| spec | `feedback-1` | demo | Every hit seen where it lands (target line 5). |
| spec | `audio-1` | demo | Every action makes a sound (line 4). |
| spec | `faction-play-1` | demo | Doctrine and the flight for the cast; needs r2 (follow-up `faction-play-reanchor-demo`). |
| spec | `faction-play-2` | demo | Lucent's duel is the featured neutral's spectacle; needs r2. |
| spec | `faction-play-3` | demo | Support and anchors; the played proof moves here for the cast; needs r2. |
| spec | `faction-play-4` | full game | The tender loop on real rounds is depth past the demo's proof. |
| spec | `loot-1` | demo | Floating items in the simulation; loot is what the player sells. |
| spec | `loot-2` | demo | Pickup as a capability. |
| spec | `loot-3` | demo | The grab presented in Unity; merges the loot branch. |
| spec | `ballistic-ammo` | demo | Ruled for everyone and unblocked; ammunition is a cargo good in the Pirates' game. Needs a balance pass. |
| spec | `ships-player` | demo | A built player loads package hulls: every demo hull needs it. |
| spec | `ships-addon-frame` | demo | The Blender step from a Tripo mesh to a hull, no Unity. Stale against `ship-data-all-in-cc` if `demo-ship-cc-timing` rules before-hulls. |
| spec | `ships-addon-mounts` | demo | Mount helpers for demo hulls. Same caveat. |
| spec | `ships-hull-material` | demo | One hull shader painted from a mask: the demo's material under `demo-material-scope`'s recommended option. |
| spec | `ships-addon-paint` | demo | The mask authored in Blender for that shader. |
| spec | `ships-hull-livery` | demo | Faction liveries for the cast; its Faction key collides (follow-up `livery-key-collision`). |
| spec | `scenarios-smoke` | demo | Test designs and smoke scenarios: the verification path. |
| spec | `mining-merge-unity` | demo | The Unity half of the merge that landed. |
| spec | `material-graph` | full game | Graph record and judge; not demo-bearing under `demo-material-scope` (recommended). |
| spec | `material-library-export` | full game | Same. |
| spec | `material-lowering` | full game | Same. |
| spec | `hull-bakes` | full game | Same. |
| spec | `ships-studio-schematic` | full game | Authoring comfort in Studio; the demo's few hulls do not need it. |
| spec | `ships-addon-gizmos` | full game | Same. |
| spec | `mining-index-tree` | full game | Sublinear index: wanted, not critical (ruling). |
| spec | `mining-belt-cells` | full game | Same. |
| spec | `ships-merge`, `ships-mounts`, `ships-addon-package`, `ships-bind-path`, `cut-ship-render-fixes` | demo, landed | The package path the demo hulls use. |
| spec | `scenarios-adopt`, `scenarios-menu` | demo, landed | Scenarios: the demo is one. |
| spec | `mining-index`, `mining-target-queries`, `mining-index-pins`, `mining-merge` | demo, landed | Targeting through the index; the belt freeze is gone. |
| follow-up | `pirates-catalog-record` | demo | The ally needs a Faction record and products. |
| follow-up | `zhestokost-boss-hull` | demo | The boss hull and `BossHull`. |
| follow-up | `demo-cast-hulls` | demo | A hull per cast faction. |
| follow-up | `faction-play-reanchor-demo` | demo | Re-anchor faction play, add the Pirates, play the cast. |
| follow-up | `livery-key-collision` | demo | Two specs claim Faction key 16. |
| follow-up | `demo-ux-ledger` | demo | The bar's UX polish pass has no owner. |
| follow-up | `demo-difficulty-onramp` | demo | "Game is hard"; the region needs a ramp. |
| follow-up | `vault-docs-sweep` | demo | Stale vault notes mislead the faction-play revision. |
| follow-up | `controls-facing-and-aim` | demo | Under `demo-controls-rebuild`'s recommended option. |
| follow-up | `release-target-is-a-demo` | demo | This pass; Self closes it with target r3. |
| follow-up | `release-map-not-on-master` | demo | Land this map and the target doc on master. |
| follow-up | `wwise-catalog-fields` | demo | The audio sequel removes the Wwise ids; the boss music field is among them. |
| follow-up | `scenarios-batch4-minors` | demo | Inventory drag and station-reactor quality: UX ledger items. |
| follow-up | `hull-damage-wear` | demo | Damage visible on the hull (line 6). |
| follow-up | `mod-ship-single-existence-gate` | demo | A skipped package throws on spawn; demo hulls are packages. |
| follow-up | `ambiguous-hardpoint-origin` | demo | Save correctness for package hulls. |
| follow-up | `remove-sbsar-files` | demo | Clean break from Substance before a public build. |
| follow-up | `delaunay-exact-predicates` | demo | Galaxy links; a silently unlinked zone could strand a demo run. |
| follow-up | `failed-start-scene-halts` | demo | The New Game failure path the demo's missing-cast refusal uses. |
| follow-up | `stale-docs-after-scenarios-menu` | demo | Docs that describe the old New Game path. |
| follow-up | `scratch-carry-drafts` | demo | The ship `.cc` draft bears on `demo-ship-cc-timing`. |
| follow-up | `faction-lore-notes-r3` | full game | Vault setting notes; no demo cut reads them. |
| follow-up | `mining-impacts-through-contract` | full game | Mining Cut 4 is parked. |
| follow-up | `mining-cuts-4-5-7` | full game | Yield and content; no buyer for ore in the demo. |
| follow-up | `mining-target-pin-gaps` | full game | Low test pin gaps in landed code. |
| follow-up | `belt-render-reads-index` | full game | Belt rendering through the index. |
| follow-up | `c4-ship-element-ids` | full game | Variants campaign. |
| follow-up | `variants-python-raw-push` | full game | Variants campaign. |
| follow-up | `mod-hull-changed-under-run` | full game | Mod edits under a run; demo hulls are first-party. |
| follow-up | `ship-lods` | full game | Only if a profile shows cost. |
| follow-up | `hull-decals` | full game | Insignia and damage decals. |
| follow-up | `hull-player-paint` | full game | Player liveries. |
| follow-up | `blender-pin-operator` | full game | Pins Blender for the material judge. |
| follow-up | `material-mod-graph-kernels` | full game | Mod material kernels. |
| follow-up | `material-map-rescope` | full game | Re-scope of the material map, after `demo-material-scope`. |
| follow-up | `bodies-entity-and-mines` | full game | Bodies for entities and mines. |
| follow-up | `cultmath-mixed-seed` | full game | CultLib hygiene. |
| follow-up | `launcher-ammunition` | full game | Missiles and drones campaign. |
| follow-up | `drones-munitions-substrate` | full game | Same. |
| follow-up | `articulated-mounts` | full game | Links with arc and traverse. |
| follow-up | `colosseum-npc-lab` | full game | Its own campaign. |
| follow-up | `heat-flow-schematic-particles` | full game | Presentation depth past line 6. |
| follow-up | `scenarios-verification-ledger` | full game | Deferred by ruling. |
| follow-up | `authored-missions` | full game | Authored scenes and missions. |
| follow-up | `volumetrics-backport-map` | full game | Visual quality with no demo line. |
| follow-up | `ghostlight-readme-drift` | full game | Ghostlight, narrative. |
| follow-up | `faction-play-reanchor`, `faction-lore-notes` | obsolete | Superseded on 2026-10-06 by `faction-play-reanchor-demo` and `faction-lore-notes-r3`. |

Counts: demo 52 (31 specs, 11 of them landed, and 21 follow-ups); full game 34 (9 specs,
25 follow-ups); obsolete 2 follow-ups. No in-force spec lost its purpose in both scopes,
so none was withdrawn.

## Faction play: the NPC scripting

The operator ruled on 2026-10-03 that the faction dynamics may elaborate the NPC
scripting through the extension points she built into it (ruling
`npc-scripting-may-grow`). Legacy-first still forbids CultMesh and daemon work. This
section maps those extension points, names the smallest set of primitives that
carries every dynamic in `Game Design/Faction Play.md` (AetheriaLore `f3e0e6d`), and
gives the reasons for the four proof cuts (`faction-play-1` to `-4`), which are
typed specs in the mind. Anchors are against `origin/master` `f1dee184`, read with
`git show` on 2026-10-03, unless the line names a lane.

### Body facts

**F1. NPC behaviour is a code-composed state graph, not a tree, a DSL or assets.**
- `Agent` (`ServerShared/Agents/Agent.cs:14-80`) holds one current `BaseState`. Each
  tick, `Update` (`:47-56`) runs the state, then takes the first `StateTransition`
  whose `Condition()` holds (`:82-94`: target state, condition closure, optional
  on-transition action).
- `BaseState.AddTransition(..., includeChildren, ignoreStates)`
  (`States/BaseState.cs:19-58`) copies a transition onto every state reachable from
  the source. That is the interrupt mechanism: one call makes "a target appeared"
  fire from anywhere in the graph. It is the same idea as Halo 2's impulses.
- `Minion` (`Minion.cs:5-22`) is the only `Agent` subclass. It composes root, patrol
  and combat in its constructor. Behaviour is authored in C#; there is no
  data-driven layer.
- `codex/scenarios` makes `AddTransition`'s walk state per call, because zones are
  built on several threads (its `BaseState.cs` hunk at `:12-58`).

**F2. Tasks are the command slot, and only one task kind is wired.**
- `Agent.Task` (`Agent.cs:22`) holds one `AgentTask` (`Tasks/AgentTask.cs:13-22`).
  `Minion` enters patrol when `Task is PatrolOrbitsTask` (`Minion.cs:10-12`).
- `HaulingTask`, `Mining`, `StationTowing` and `Survey` are data with no state
  that reads them. `AgentTask.Priority` and `Reserved` have no reader; they are the
  remains of a task board that is no longer in the tree (`WanderTask` and
  `MatchVelocity` were deleted in `cc7f2c88`).
- `PatrolOrbitsState` (`Tasks/PatrolOrbits.cs:10-31`) and `MoveToOrbitState`
  (`States/MoveTo.cs:31-37`) are the only movement states. No state follows an
  entity, holds a standoff, or flees.
- No task state returns to root when `Task` changes. Only combat's interrupt
  leaves patrol.

**F3. Agents are made in one place and ticked before entities.**
- `Zone.CreateAgent` (`Zone.cs:137-144`) gives every non-player ship a `Minion`
  with a patrol over four random orbits. `Zone.Update` ticks agents (`:185-186`),
  then entities (`:188`), then `FireControl.Step` (`:193`).
- `codex/scenarios` adds `Zone.Admit(entity, piloted)` as the only writer of
  `Entities.Add` and the only creator of agents (construction, run staging, warp
  arrival, undock, spawned turrets).
- Agents are not persisted (`AgentTask.cs:11`). A loaded zone rebuilds them.

**F4. Each ship perceives alone.**
- `Entity` holds `VisibleEntities`, `VisibleEnemies`, `VisibleFriendlies`,
  `EntityHostility` (`Entity.cs:34-37`) and `EntityInfoGathered` (`:64`).
- `Sensor.Execute` is the only writer of `EntityInfoGathered` (`Sensor.cs:157-189`).
  `Sensor.Ping` (`:110-125`) boosts it and adds the pinger's own visibility.
- Crossing `TargetDetectionInfoThreshold` moves an entity into the visible lists
  (`Entity.cs:237-254`).
- `FireControl.Designated` requires `source.VisibleEntities.Contains(target)`
  (`FireControl.cs:232-239`). `IsRevealed` reads the observer's own
  `EntityInfoGathered` (`:130-149`). A ship cannot fire at, or aim at gear on,
  something its own sensors do not hold.
- No group, squad or faction-level knowledge exists. `Ship.HomeEntity`
  (`Ship.cs:26`) has no reader.

**F5. Hostility has a derived rule, a per-entity override and a grudge.**
- `IsHostileTo` (`Entity.cs:535-553`): an override decides outright. Otherwise a
  zone owner is hostile to trespassers (presence not permitted), and anyone is
  hostile to whoever is hostile to them. `// TODO: Inter-faction hostility` is at
  `:547`.
- `GetFactionRelationship` (`:556-563`) answers Beloved for the own faction and
  Neutral for every other, except for the player's ship, which reads
  `Galaxy.FactionRelationships`. Ruling `faction-relations-field` fills this.
- `SetIff` (`:529-533`) writes the runtime override. `WatchForGrudge`
  (`:452-481`) sets a sticky hostile override back on anything that turns hostile
  while detected. Leaving the zone clears overrides (`:207`). The override map is
  private: nothing outside `Entity` can ask whether a grudge is held.

**F6. Target selection has four writers.**
- `Minion.cs:14`: the first enemy to become visible, when the slot is empty.
- `TurretController.cs:96`: the first visible enemy ship.
- The player's input (`ActionGameManager.cs:377-403`).
- `Entity` clears it when the target leaves the zone or visibility
  (`Entity.cs:201`, `:227`, `:298-299`).
- `codex/mining` turns `Target` into a `TargetRef` (asteroid chunks can be targets)
  written through `SetTarget`. It edits `Minion.cs:14-21` and `Combat.cs:28`.

**F7. Combat tuning is global.**
- `CombatState.SampleDps` biases range by `GameplaySettings.AgentRangeExponent`
  (`Combat.cs:158`, `Settings.cs:242`). Movement reads `AgentForwardLerp` and
  `AgentMaxForwardDistance` (`Combat.cs:95`, `Settings.cs:243-244`).
- `FireControl.AgentFires` (`FireControl.cs:93-99`) compares hit probability with
  the global `AgentMinHitProbability` (`Settings.cs:260`). Its callers are
  `Combat.cs:121` and `TurretController.cs:81`.
- `CombatState` aims at the target's most dangerous revealed weapon through
  `TrySelectTargetItem` (`Combat.cs:40-53`).

**F8. `Faction.Personality` is economy vocabulary, not combat doctrine.**
- `Faction.Personality` (`Corporations.cs:28-29`) maps `PersonalityAttribute`
  records (`ItemData.cs:902-914`: a name with low and high pole names) to floats.
- The same records key `CompoundCommodityData.DemandProfile` (`ItemData.cs:334-335`),
  which is what a population with those traits wants to buy.
- Neither map has a reader. A string probe of the working-copy store found the
  schema and no faction values; that probe is not proof.
- So the attributes are population traits shared with demand. Encoding
  `engage_on` or `target_priority` as float bands in them, as Faction Play's lever
  table proposed, would give one record two meanings.

**F9. Generation spawns individuals, not groups.**
- `ZoneGenerator` adds `enemyCount` ships of the nearest faction one at a time
  (`ZoneGenerator.cs:334-340`), plus neutral wanderers of one other faction
  (`:342-356`).
- `LoadoutGenerator.GenerateShipLoadout(hullFilter)` (`LoadoutGenerator.cs:35-50`)
  fills every hardpoint (`:189-231`), so no generated ship is unarmed. Nothing marks
  a ship as a tender or a hauler.

**F10. Hits carry their source only on the hull.** `IncomingHit` (`Entity.cs:138`)
fires with the shooter at `FireControl.cs:962` and `:1009`, after the shield
branches have returned, so a shot a shield absorbs reports no source. `feedback-1`
adds `Capabilities.Absorb` for both layers, without a source.

**F11. Cargo moves between entities; jettison has no owner.**
`Entity.TryTransferItems(target, item, quantity)` (`Entity.cs:693-725`) moves
commodities between cargo bays, and weapons draw ammunition from cargo through
`AmmoType` (`InstantWeapon.cs:187-192`). Dropped items exist only as Unity pickups
spawned on death (`EntityInstance.cs:304-320`), and the mining lane keeps pickups
Unity-only (its pickups question, recommendation A). The simulation has no
floating cargo to jettison into. The floating-item substrate is mapped as cuts
`loot-1` to `loot-3` (section "Lanes: scenarios, mining and the loot move", L6-L8).

**F12. Barks and pings already have a channel.** `Entity.SetMessage`
(`Entity.cs:1275-1278`) shows a line for `MessageDuration`. `Entity.Sensor.Ping()`
is callable by anything that holds the entity.

**F13. Agents are testable headless.** `IffAndCombatTests` builds a `Zone` with
ships and drives detection by writing `EntityInfoGathered`
(`tests/Aetheria.Shared.Tests/IffAndCombatTests.cs:51-92`). `Agents/` is under
`ServerShared`, so the whole graph compiles into `Aetheria.Shared` (B11).

**F14. Ammunition is real for three weapons, and nobody is stocked.**
- Only DeathCluster, FastBlast+- and pretty pretty bang bang carry an `AmmoType`;
  their refs were repointed in `515859cf` (`docs/cultcache-migration-cut.md:161-163`).
  Every other weapon, Zhestokost ballistics included, reloads for free.
- `LoadoutGenerator` puts no rounds in any cargo, and station stock is drawn from
  `EquippableItemData` only (`LoadoutGenerator.cs:98-99`), so no station sells
  rounds either. An NPC carrying one of the three ammunition weapons fires its
  first magazine and then never reloads (`InstantWeapon.cs:186-197`).
- So Zhestokost's "magazines run low, back to the tender" has no substrate until
  its weapons draw a commodity and generation stocks it.

### What the extension points can carry

| Extension point | Carries | Cannot carry |
|---|---|---|
| State graph with interrupt transitions (F1) | Every per-ship motor behaviour: follow at a standoff, hold, flee, return, drift, sweep. | Anything that needs more than one ship's knowledge: who may engage, shared tracks, duels. |
| `Agent.Task` (F2) | The order a ship is executing. The patrol precedent already maps a task type to a state. | Deciding the order. Nothing writes tasks except `CreateAgent`. |
| `Zone.CreateAgent`, then `Zone.Admit` (F3) | Where a ship joins whatever owns its orders, and where doctrine is applied. | Group state, which must outlive one admission. |
| Perception lists and `EntityInfoGathered` (F4) | A ship's own view, which `FireControl` trusts. | Shared knowledge. A second writer would let a ship aim at gear its own sensors never resolved (`IsRevealed`), breaking `instruments-bound` and fire control's single authority. |
| `SetIff` and grudges (F5) | Hostility memory, and spreading it: one call per member. | Faction-to-faction stance; that is the relations field. |
| `Ship.Target` (F6) | The engagement itself: `CombatState` runs while it is set. | Coordination, while four writers race for it. |
| `Faction.Personality` (F8) | Nothing in this strand; it belongs to demand. | Typed enums and per-role values. |
| `SetMessage`, `Sensor.Ping` (F12) | Hails and ping cadence. | Nothing needed here. |

The graph, the task slot and the factory were built to be extended, and they carry
the individual ship. What is missing is one layer: an owner of a group's intent.
Every cross-ship dynamic in Faction Play needs it.

### The primitives

Seven primitives carry the proof factions and leave room for the rest. Each says
where it plugs in, what it reads and which quirks it enables.

**1. Doctrine (data).** A typed `FactionDoctrine` on `Faction` (key 17; key 16 is
`Relations` by ruling), with flight-level fields and a `RoleDoctrine` per role
(combatant, support). Flight-level fields: `EngageOn` (detection, identified,
provoked, trespass, never), `Grace`, `Hail`, `ComplySpeed`, `Pack`, `StepInDelay`,
`PingInterval`. Per role: `BreakOffHull`, `BreakOffAmmo`, `RangeExponent`,
`MinHitProbability`, `HoldStandoff`, `Anchor`, `Leash`. A faction with no doctrine
gets one built from today's globals, so it fights exactly as `Minion` does now.
Inputs: catalog only. Enables the tuning half of every quirk (R&D range, Zhestokost
spray, Cryonix discipline). Why not `Personality`: F8. The shape is question
`faction-doctrine-shape`.

**2. Flight (the group's intent owner).** One runtime object per faction per zone,
holding that faction's piloted ships. It is formed at admission and rebuilt on
load, never persisted (F3). It is the only writer of each member's `Target` and
`Task`; the agent graph only executes them. It reads members' perception, hull
fraction, cargo and grudges, the doctrine, and the zone's stations. It ticks in
`Zone.Update` before the agents. This is RimWorld's Lord (a group state machine that
assigns duties to pawns) and Halo 3's objectives (squads fill prioritised tasks
with capacities), at the size this game needs. Why a new owner: no extension point
holds more than one ship's knowledge (table above).

**3. Engagement rule with grace and hail (in the flight).** Per hostile track, the
flight decides when it becomes engageable (`EngageOn` over the union of members'
views), hails once through the nearest member's `SetMessage`, holds for `Grace`,
then engages, unless the track has slowed below `ComplySpeed`, which counts as
compliance. A grudge on the track skips the grace. Enables Zhestokost's inspection
hail, Sol Dominion's "never before identification" (`identified`: any member's
info at the gear tier), Miss Terri's and Aya's "provoked", Megiddo's trespass, and
Odla's "never". F.E.A.R.'s squad dialogue is the precedent: the bark speaks a
decision already made.

**4. Engagement slots and the duel (in the flight).** `Pack` engagers per track;
the rest hold at `HoldStandoff` (default: just outside their own longest weapon
range). An engager that breaks off frees its slot after `StepInDelay`. With `Pack`
1 the slot is a duel. It is offered only to a challenger with no other hostile
track near it, and it breaks when anyone lands a hit on a holding member, or anyone
but the challenger lands one on the headliner. Broken, every slot opens and the
pack engages whoever broke the form. This carries ruling `lucent-duel-bait` whole:
honour the duel and pick the pack apart, or stage an interloper. The signal is a
landed hit with its source (F10), so the cut adds `Source` to `feedback-1`'s
absorb. Prior art: attack tokens (the "kung fu circle" of brawlers) and Halo 3's
task capacity.

**5. Anchor and leash (in the flight).** Each role's doctrine names an anchor: the
flight's support ship, the nearest own station, the patrol site, or none. An
engager past `Leash` from its anchor is ordered back, with hysteresis. Enables the
Zhestokost column that will not pass its tender, AU escorts bound to the worksite,
NiteLife guards that never leave the station, Dominion's jurisdiction, Megiddo's
perimeter, and Aya refuge across a border.

**6. Roles, break-off and rearm (in the flight).** A member with no weapon is
support; the rest are combatants. Doctrine says how many support ships generation
adds (`LoadoutGenerator` gains an unarmed option, F9). Break-off orders a flee to
the nearest own station, or away from the threat. It triggers on hull fraction
(Lucent headliners, AU haulers, Alakrita, Cryonix), or on ammunition fraction
against the count at admission. An ammunition break-off returns to the support
ship, which transfers rounds through `TryTransferItems` (F11) until the baseline is
restored; then the member rejoins. This carries the Zhestokost tender loop, once
Zhestokost weapons draw a commodity and generation stocks it (F14, question
`zhestokost-ammunition`). Support
ships use their own role doctrine: the tender follows the column's rear and flees
early, and the column, anchored to it, follows it home.

**7. Motor states (in the agent graph).** `FollowState` (an entity at a standoff,
matching its velocity) and `FleeState` (toward a refuge, or away from a threat).
They are new `BaseState`s on the existing graph, entered from tasks, and every task
state returns to root when the task changes. Follow serves escort, hold, return,
rearm and shadow; flee serves every break-off. They are two of the tutorial's four
shared states; drift-cold and sweep stay with the tutorial.

Beyond the proof, three more primitives use the same owners and need no new layer:

- **Relations** (ruling `faction-relations-field`). `GetFactionRelationship` reads
  `Faction.Relations`, and `IsHostileTo` treats Hated and Hostile as hostile at the
  TODO in F5. Every cross-faction pair starts here.
- **Picture sharing between flights.** A flight pushes track entries and grudges to
  other flights in the zone by a `ShareTrack` policy: own faction, allies by
  relation, or every flight hostile to the track (Finch). A shared entry lets a
  recipient engage and steer; adopted grudges become `SetIff` on its own members.
  Sharing never writes `EntityInfoGathered`, so a recipient fires only once its own
  sensors hold the target. This is question `shared-track-cueing`.
- **Calls.** A zone-level distress subject `(caller, attacker, position)`, raised
  by a flight when a member is hit, or as a bark with a false caller (pirates).
  Flights with `AnswerCall` in range retask members. Navigator escorts pulled off a
  Lightsail convoy, Aya cover and the corridor edge come from this plus relations.

Target priority (drives, cargo, structures) extends `CombatState`'s aim-item choice
by doctrine and the flight's track choice. Jettison is ruled (`jettison-shared-floating-items`)
and builds on `Zone.Release` from `loot-1`.

### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Doctrine | `Faction.Doctrine`, catalog, key 17. | Authored through an `AetherDb` catalog command, as the targeting migrations were. | The catalog author. Read when a ship joins a flight. |
| Flight | Runtime: zone plus faction. | Formed at admission, rebuilt on load, gone with the zone. Never persisted. | Itself alone, for members' `Target`, `Task`, hails and pings. |
| Track (flight picture) | Runtime: flight plus hostile entity. | Lives while any member sees it, or a sharer lends it. | The flight, from members' perception. Never written into `EntityInfoGathered`. |
| Duel | Runtime: flight plus challenger. | Offered, then held or broken. Ends when the challenger leaves the picture or dies. | The flight, from landed hits with their source. |
| Ammunition baseline | Runtime: member. | Counted at admission. | The flight. Rearm moves real cargo through `TryTransferItems`. |

### Rationale

**Why the flight owns intent and the agent only executes.** Faction Play's
dynamics are decisions about several ships at once: who engages, who holds, who
knows what, who goes home. Spreading them across agents would keep four writers on
`Target` (F6), add more on `Task`, and make agents read each other's private state.
The RimWorld Lord and Halo 3's objectives both keep one group-level decider over
simple individual executors, and Starsector varies one AI by parameters. The
demotions: `Minion` no longer chooses a target. `Zone.CreateAgent` no longer
assigns a patrol; the flight does. The global combat tunables become the defaults
of a faction with no doctrine. `TurretController` keeps its own target writer,
because turrets are not piloted and belong to no flight; giving stations a flight
is a later step.

**Why one flight per faction per zone.** It needs no persistence and no grouping in
generation, and a zone rarely holds two separate groups of one faction. The cost:
two such groups would act as one. When generation learns groups, the key widens to
a group id assigned in `ZoneGenerator`; nothing else changes.

**Why shared tracks are cues, not fire-control data** (the recommendation in
`shared-track-cueing`). Fire control was rebuilt so that one owner decides a hit
from the shooter's own sensor data (`Designated`, `IsRevealed`, `PSensor`). A
remote track that fed `EntityInfoGathered` would undo that, and would show the
player hits from ships that never saw them. As cues, sharing still delivers the
dynamics Faction Play names: the Zhestokost column needs no identification and
finds the hot target the Dominion pointed it at; the Finch shadow is a countdown
because the hunters come, not because they fire from beyond their sensors. The US
Navy's Cooperative Engagement Capability fires on remote tracks, and it needs the
fused track picture that the target rules out of scope.

**Why the tutorial director goes through the flight.** The tutorial sketch's
director sets agents' tasks and targets directly. With the flight as their only
writer, the director becomes an input to the flight (a scripted order for named
members that outranks doctrine), not a second writer. The tutorial cut owns that
hook; this map records the boundary.

**Why the proof is four cuts.** One cut would be about 2,500 lines with tests, well
over a Hands budget, so it is split along the primitives:
- `faction-play-1` lands the owner: doctrine, the flight as the only writer of
  `Target` and `Task`, the engagement rule with grace, hail and compliance, the
  follow state, and per-doctrine range and fire threshold. Zhestokost already hails
  before it sprays, Lucent announces itself and fires only on good odds, and AU
  escorts shoot the first contact.
- `faction-play-2` adds slots and the duel, hull break-off with the flee state, and
  ping cadence: Lucent whole. It needs `feedback-1`'s absorb.
- `faction-play-3` adds roles, support generation, anchor and leash: the tender the
  column will not leave, and the AU haulers that run while their escorts stay close.
- `faction-play-4` makes ammunition real for Zhestokost and closes the tender loop
  (question `zhestokost-ammunition`, F14). It ends with the operator's playtest of
  the three factions.

Each is sized at roughly 550 to 720 lines with tests, under 200k Hands tokens. All
four wait for `codex/mining` (it changes `Target`'s type and edits `Minion.cs`) and
`codex/scenarios` (it rewrites admission in `Zone.cs`) to merge, because the first
cut rewrites exactly those lines.

### Prior art

- Damián Isla, "Handling Complexity in the Halo 2 AI", GDC 2005
  (https://www.gamedeveloper.com/programming/gdc-2005-proceeding-handling-complexity-in-the-i-halo-2-i-ai):
  a prioritised behaviour DAG, and impulses that raise a behaviour's priority from
  anywhere. `includeChildren` is the legacy tree's impulse.
- Damián Isla, "Building a Better Battle: HALO 3 AI Objectives", GDC 2008
  (https://gdcvault.com/play/497/Building-a-Better-Battle-HALO, slides at
  https://web.cs.wpi.edu/~rich/courses/imgd4000-d09/lectures/halo3.pdf): designers
  author tasks with priorities and capacities, and squads fill them; inside a task
  the AI is autonomous. This is the slot model for the pack.
- Jeff Orkin, "Three States and a Plan: The A.I. of F.E.A.R.", GDC 2006
  (https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf): three FSM
  states and a planner per agent, and a global coordinator that clusters agents
  into squads by proximity and runs one squad behaviour at a time. Squad dialogue
  voices decisions already made. A planner is more than these dynamics need; the
  coordinator and the barks transfer.
- RimWorld's Lord, LordJob and LordToil (decompiled source,
  https://github.com/josh-m/RW-Decompile/blob/master/Verse.AI.Group/LordMaker.cs):
  a group controller with its own state machine assigns a duty to each pawn, and
  each pawn's think tree runs its duty. That is the flight and its tasks.
- Starsector officer personalities (https://starsector.wiki.gg/wiki/Officer): timid
  to reckless, each changing engagement range and retreat over one combat AI.
  Faction doctrine and fleet assignments (patrol, escort, defend) are cited from
  memory, not re-fetched. Lesson: parameters on one AI, as Faction Play already
  says.
- Attack tokens, the "kung fu circle" of brawler games: only N enemies attack at
  once while the rest posture. From memory; no primary source fetched.
- The US Navy's Cooperative Engagement Capability, which engages on remote fused
  tracks. From memory; cited as the path not taken.

## Content: the moddable-ships lane

The operator ruled on 2026-10-03 that this campaign adopts `codex/moddable-ships`
whole (ruling `adopt-moddable-ships`). This section maps what the adoption takes:
S2's owed checks, the merge, the Blender package action and S3. S5 stays with the
variants campaign. The cuts are typed specs in the mind (`ships-merge`,
`ships-mounts`, `ships-addon-package`, `ships-addon-frame`, `ships-addon-mounts`,
`ships-player`), and the forks are questions (`ships-merge-gate`,
`release-hull-home`, `s3-player-build`). Two rulings of 2026-10-03 reshape the
add-on and player cuts and add a material strand: `hull-runtime-materials` and
`thrusters-radiators-are-meshes`. They are mapped in "Hull materials, mount
meshes and per-hull prep" below, with the cuts `ships-hull-material`,
`ships-hull-livery` and `ships-addon-paint` and the questions `hull-paint-mask`
(ruled: `hull-paint-mask-texture`) and `hull-livery-scope`.

Anchors are against the lane tip `a93625c2`, read with `git show` on 2026-10-03.
For files the lane does not touch, they hold on `origin/master` `f1dee184` too.
The lane's own docs (`docs/moddable-ships-cut.md`,
`docs/moddable-ship-authoring.md`) are body facts from here on.

### Body facts

**M1. The merge into master is trivial.** All 21 commits on `origin/master` since
the lane's base `65c63495` are `Scenarios map:` doc commits.
`git merge-tree --write-tree origin/master origin/codex/moddable-ships` is clean
(tree `7e034e04`). No file is changed on both sides. Fire control and the stack
reached the lane through its own merge `18e12d81` on 2026-09-30.

**M2. The lane conflicts with the two other open lanes, one line each.**
- `codex/mining` (`83d8371e`): `AetheriaStores.cs:9` (`CatalogTypes`; mining adds
  `FieldKindData`, ships add `ShipAuthoring`) and `tools/AetherDb/Program.cs:39-41`
  (a `case` line and the usage string).
- `codex/scenarios` (`aa3baf12`): `AetheriaStores.cs:9` (scenarios add `Scenario`).
  `Program.cs`, `ItemData.cs`, `Loadout.cs` and `LoadoutTests.cs` merge on their own.
- The resolution is a union in both: keep every type and every command.
- Probe: `git merge-tree --write-tree origin/<lane> origin/codex/moddable-ships`.

**M3. The cuts already mapped in this campaign meet the lane in one file.**
- The lane edits `UI/MainMenu.cs` in `ShowMain` and inserts `EnterGame` and `Refuse`
  just before `ShowSettings` (lane `:195-218`).
- `audio-1` edits `ShowSettings` (master `:186-200`), so expect an adjacent-hunk
  conflict there. Resolve it by keeping both.
- `feedback-1` edits `ShowGraphicsSettings`, which the lane does not touch.
- Neither cut touches `ActionGameManager.cs`, `ZoneRenderer.cs` or the lane's new
  files.

**M4. The mount ruling of 2026-09-30 is in the docs, not the code.**
- `a93625c2` records it: a mount anchor exists only for weapon, radiator and
  thruster hardpoints.
- The validator still demands an anchor for every hardpoint
  (`ShipAuthoring.cs:222-230`), and a weapon's mount must have the role
  `articulation`.
- `ShipModPlan` copies every mount into `Equipment` (`ShipModPlan.cs:13-14,56`).
  `ShipModShips` fills `EquipmentHardpoints` from it (`ShipModShips.cs:152`).
- The only reader of `EquipmentHardpoints` is the dead `SoundBank` block
  (`EntityInstance.cs:244-253`), which `audio-1` also deletes. So the array has no
  consumer.
- Role-versus-type checks are split. The validator checks thruster and radiator
  mounts. `ShipModPlan.Build` checks that an emitter is a mount (`:30-35`) and that
  muzzles hang off weapons (`:38-48`).

**M5. A package can reach the game only through the console.**
- Generation, station stock and the starting ship all choose from
  `FactionProductData` (`LoadoutGenerator.RandomProducts`; `RandomHull` at
  `LoadoutGenerator.cs:113`).
- A product names a design and a manufacturer `Faction`
  (`FactionProduct.cs:15-31`). Generation also requires `Price > 0`.
- A package holds only `HullData` and `ShipAuthoring` (`ShipAuthoring.cs:126-140`
  refuses a third record; `ship_cc.py:81-82` does the same).
- So the only way into a zone is the `give <hull name>` console command while
  docked (`ActionGameManager.cs:527-556`). It commissions a bare hull at the
  station.
- A grep of `ConsoleController` and `ConsoleView` found no debug-build gate. It is
  assumed live in a player; `ships-player` checks it.
- `ship-authoring create` writes a `HullData` with default stats and `Price` 0
  (`ShipAuthoringCommands.cs:38-56`).
- Question `release-hull-home` asks how a release hull gets its manufacturer and
  price.

**M6. The model must sit on the schematic grid.**
- `Entity.ToWorldPoint` (`Entity.cs:426-433`) gives
  `world = (cell − Shape.CenterOfMass) × SchematicCellSize` (2 m, `Settings.cs:255`),
  with schematic +x to starboard and +y forward.
- Fire control decides hits in that frame. `feedback-1` places impact sparks at
  `ToWorldPoint` of the lane entry.
- A GLB whose origin is not the grid's centre of mass, or whose scale is not 2 m
  per cell, shows hits beside the hull.
- Nothing checks this today.

**M7. Axes, probed.**
- Blender 5.2.2 (`--background --factory-startup`, a cube ship exported with
  `export_format='GLB', use_active_collection=True, export_extras=True,
  export_yup=True`):
  - An object custom property `aetheria.id` lands in the node's `extras`.
  - Objects outside the active collection are left out.
  - The GLB has a scene.
  - Blender `(0, 1.5, 0)` exports as glTF `(0, 0, -1.5)`.
  - An empty's scale `(2, 3, 1)` exports as `(2, 1, 3)`.
- With glTFast's handedness flip, Unity sees `(-x, z, -y)`
  (`ShipModVisual.BuildLineMesh`, `ShipModVisual.cs:98`).
- So the nose of a ship points along Blender −Y, Blender's own Front, and the
  ship's starboard side is Blender −X.

**M8. The operator's two Tripo hulls are already in one .blend.** A read-only probe
of a copy of `C:\Users\Meta\Desktop\Quiet.blend` (Blender 5.2.2):

| Collection | Object | Triangles | Size (Blender units) | Material |
|---|---|---|---|---|
| Headliner | `ff611c02…` (and `original`) | 50,008 | 0.68 × 1.00 × 0.35 | one; a 4096² JPG base colour |
| Quiet | Dexter Quiet | 9,614 | 0.98 × 1.05 × 0.41 | one; a 4096² JPG base colour |
| Quiet | Sinister Quiet | 10,880 | 1.07 × 1.05 × 0.41 | the same |

- Each collection also holds a Grease Pencil LineArt object. Quiet holds a scaled
  icosphere, a hand-made shield ellipsoid.
- Tripo meshes arrive at about 1 unit long, one material, base colour only: no
  normal, roughness or emissive map. The textures came from FBX imports
  (`D:\Downloads\tripo_texture_*.fbm`) and are packed into the .blend.
- Schematic sketches sit beside the .blend: `HeadlinerSchematic.png`,
  `DexterQuietSchematic.png` and `SinisterQuietSchematic.png`.

**M9. The template lacks a radiator material.**
- `EntityInstance` heats radiators with `material.SetFloat("_Emission", …)`
  (`EntityInstance.cs:407-410`). Only Aetheria shaders define `_Emission`
  (`Assets/Materials/Radiators.mat`).
- `ShipModTemplate` (`ShipModTemplate.cs:12-16`) carries shield, tractor, ping,
  destroy effect, invisible and map icon, but no radiator material.
- A mod radiator therefore keeps its glTF material and never glows.
- Thruster emitters are particle emission shapes (`ShipInstance.cs:71`,
  `particlesShape.meshRenderer`). Any mesh works.

**M10. A built player has never loaded a mod ship.** Every proof so far ran in the
Editor:
- `ShipModPlaySmoke` and `ShipModPreview.Smoke` passed in Unity 6000.3.24f1
  (`moddable-ships-cut.md` S2 status).
- glTFast's manual says a Built-In pipeline player must carry glTFast's shader
  variants: a `ShaderVariantCollection` in Graphics' Preloaded Shaders, or
  placeholder materials in `Resources`. This is from the docs, not a probe
  (https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.0/manual/ProjectSetup.html).
- Whether glTFast keeps meshes readable in a player is also unprobed.
  `ShipModShips.Assemble` throws if the collider mesh is not readable
  (`ShipModShips.cs:103-104`).
- `origin/master` has no player build entry point. `docs/build-delivery-cut.md`
  Cut 1 maps one (`Assets/Editor/AetheriaBuild.cs`, copying `GameData/Aetheria.cc`).
  It does not copy `GameData/Mods`, and nobody owns it.

**M11. Git does not yet track packages well.** `.gitattributes` has no `*.glb` rule.
`GameData/*.cc` covers only direct children of `GameData`, so
`GameData/Mods/<id>/ship.cc` would be stored as a plain blob.

**M12. The add-on reads CultLib from wherever Brokkr points.** `_libraries` puts
`<cultlib_py_src>/cultcache-py/src` on `sys.path` (`ship_cc.py:46-52`). Brokkr's
preference is a working-tree path. Nothing pins the revision the add-on runs
against. The variants campaign is rewriting Python's write path (spec
`variants:cut_spec:cut-parity-py.r1`).

### S2's owed checks: what each one checks, and who can prove it

The lane owes six operator checks (`moddable-ships-cut.md`, S2 status). Each check
is a rule decided in one place, plus the Unity wiring that shows it. The rule is
agent-provable. The wiring is agent-provable unless it needs clicks through scenes.

| Check | Rule, and where it is decided | Agent proof | Needs the operator |
|---|---|---|---|
| C1. The menu names excluded mods | `ShipModCatalog.Compose` and `ResolveCatalog` exclude and name a bad or colliding package, and boot continues. Already headless: `ABadPackageIsExcludedAndNamedWhileTheGoodOnesCompose`, `ACollisionExcludesEveryPackageInvolvedAndNoneOfTheRest`, `TheBootCatalogSurvivesBadPackages`, `TheBootCatalogSurvivesAComposeThatFails`. | Built-player boot smoke (`ships-player`): with one bad package staged, `Player.log` holds `Some mods were not loaded: <dir>: <reason>`. `MainMenu.Refuse` logs it at `MainMenu.cs:213`. | No. |
| C2. New Game with a mod installed | `EnterGame` waits on `ShipModShips.Loading`, refuses a faulted preload, then loads ARPG (`MainMenu.cs:197-209`). | The preload half: the player smoke's `SHIP_MOD_PRELOAD` line, with no glTFast shader error. | Yes: clicking New Game, and the prototypes surviving the Main Menu → ARPG scene load (`DontDestroyOnLoad`, `ShipModShips.cs:39`). No mod hull appears on New Game anyway (M5). |
| C3. Continue with a mod | The run stores `mod-hull:<id>` keys, and the recomposed catalog keeps them. Headless: `ComposeAtBootIsDeterministic`. | New sim test `AModShipSurvivesSaveRecomposeAndReopen` (`ships-player`). | A Continue click in the S3 session. |
| C4. Zone entry | `LoadEntity` branches on `Visual` (`ZoneRenderer.cs:292-294`). `EntityInstance.SetEntity` binds barrels, radiators and thruster emitters by mount name. | `ShipModPlaySmoke` covers instantiation. Extending it to call `SetEntity` over an outfitted mod ship is a probe in `ships-player`. If `SetEntity` needs the live scene, record it as not yet reached. | Yes: the ship looks right, thrusters emit at the nozzles, guns fire from the muzzles, the shield fits, the map icon shows. |
| C5. Wormhole | Prototypes outlive zone changes, and `LoadEntity` clones again. | Instantiating twice from one prototype in the smoke. | Yes: one transit. |
| C6. Continue after removal | `RunSave.RequireDesigns` refuses and names the missing mod ids. Headless: `RunReferencingAMissingModRefusesContinue` and its siblings. | Covered by the rule tests. | A glance: the dialog appears on Continue. |

All the operator rows fit in the S3 session, on a built player with the first real
hull: about fifteen minutes, done once. With no package installed the mod path is
inert:
- `ResolveCatalog` returns the shipped catalog
  (`TheBootCatalogIsTheShippedOneUntilAModIsInstalled`).
- `Preload` returns at once (`ShipModShips.cs:31`).
- `Loading` is a completed task.

Merging before the operator session therefore changes nothing a no-mod player sees.
Question `ships-merge-gate` asks the operator to confirm that.

### The merge

1. Hands creates `eureka/aetheria-release-ships` at `a93625c2`. The Codex branch is
   never pushed to.
2. Hands runs `git merge --no-ff origin/master`, which is clean (M1), and verifies:
   - the headless suite on Yggdrasil (684 at S2; master added no tests);
   - the Unity compile on Starfire;
   - both batch smokes against the generated skiff fixture.

   This is cut `ships-merge`.
3. Soul passes the merge: a narrow pass, because no file changed on both sides.
4. Self merges the branch into `master` with `--no-ff`, gated by `ships-merge-gate`.
5. After that, every content cut branches from `master` in its own worktree, on
   `eureka/aetheria-release-ships-<cut>`. Anchors stay valid, because the master
   merge changes no lane file.
6. Mining and scenarios inherit the union resolutions in M2 when they merge
   (follow-up `lane-merge-union-catalogtypes`).

The stale local ref `codex/moddable-ships` (`375d6bd4`) in the Codex worktree is
left alone.

### The Blender package action

*The GLB export and `ModelAsset` below are superseded by "Ship data all in the `.cc`": Package writes `ShipModel` into `ship.cc`.*

The add-on today binds a collection, captures LineArt, and edits the grid and
hardpoints. It writes no anchors and exports no GLB (`moddable-ship-authoring.md`
proof gate 2). The design aims at one thing: the operator's time per hull, at one
hull a day. Everything that can be derived from the mesh is generated. Everything
that needs her eyes takes one click at the 3D cursor.

**Per hull, after the add-on cuts:**
1. Tripo: image to model, textured. Export GLB. Tripo's smart low-poly option is
   worth its 10 credits above about 60k triangles.
2. Blender: import the GLB into a new collection. Press **New Ship**:
   - Enter the id, the name, a reference hull such as Djinni, and the length in
     cells.
   - AetherDb creates `GameData/Mods/<id>/ship.cc` with the reference hull's stats
     (`create --like`). C# owns the schema, and Python never builds a `HullData`.
   - The add-on binds the collection, turns the long axis to −Y, scales the mesh to
     length × 2 m, rasterises a draft grid from the top-down silhouette, and shows
     the grid as an overlay.
3. Rebuild, unwrap, rebake and mask: see "Hull materials, mount meshes and
   per-hull prep" below (steps P3 to P7).
4. Mounts:
   - Thrusters and radiators are meshes (ruling `thrusters-radiators-are-meshes`).
     Select the nozzle-exit or radiator faces in Edit Mode and press **Add
     Thruster** or **Add Radiator**, or tag a selected mesh object.
   - Weapons stay points: with the 3D cursor on each gun, press **Add Weapon**
     (Energy, Ballistic or Launcher).
   - Each also creates the hardpoint row on the cell under it, with a 1×1 footprint.
5. Panel: toggle cells, add the internal hardpoints (reactor, shield, sensors and so
   on, which carry no anchor), and set footprints.
6. **Package**, one button:
   - Recentre the ship on the grid's centre of mass (M6).
   - Regenerate the structural anchors unless she has marked one as kept:
     - `hull-collider`: a convex hull of the render meshes, at most 255 faces;
     - `map-icon`: a flat top-down outline;
     - `shield`: an ellipsoid empty over the bounds with a margin;
     - `tractor`: an empty at the nose.
   - Cap textures at 2048 by default.
   - Export `ship.glb` with extras.
   - Write `ModelAsset`, `Anchors`, the layout, and the lines captured from the
     collection's LineArt.
   - Run `AetherDb ship-authoring validate`, and show the C# validator's errors by
     anchor and hardpoint name.
7. Unity Editor play: dock, `give <name>`, equip, undock, fly.

Steps 2, 4 and 6 replace the hand work that M8 shows she has started. The
`aetheria.id` of every node is its anchor id, so `ModelNodeId` equals the anchor
`Id`. Node ids are written by the add-on and never typed by her.

**Material slots.** The material owners are in "Hull materials, mount meshes and
per-hull prep" below. Ruling `hull-runtime-materials` superseded the deferral of
livery recorded here before.

**Colliders.** Hits are fire control's (R8). The hull collider serves only
ship-on-ship contact and is convex (`ShipModShips.cs:110`), so a generated convex
hull is the whole job. Unity cooks at most 255 polygons for a convex collider, and
the generator emits no more.

**Split into three cuts.**
- `ships-addon-package` is the core: anchors from role-tagged objects, GLB export,
  `create --like`, validate.
- `ships-addon-frame`: New Ship, fit, rasterise, the grid, recentring, structural
  anchors.
- `ships-addon-mounts`: the mount helpers, texture cap, triangle warning, CultLib
  revision guard, and the per-hull checklist in the authoring doc.

Each fits well under 200k Hands tokens. The core alone already makes a valid
package from hand-placed objects. Revision 2 of each (2026-10-03) carries the two
new rulings; the section below says what changed.

### Hull materials, mount meshes and per-hull prep

Three operator rulings of 2026-10-03 drive this section.
- `hull-runtime-materials`: "I expect I'll want to do quite some cleanup and prep
  for the meshes. The geometry is still not ideal, and we want more runtime
  material control than just a single baked map. See how MechWarrior does it, for
  example."
- `thrusters-radiators-are-meshes`: "Note that both thrusters and radiators should
  be meshes, the former doesn't get rendered but is used as an emission surface".
- `hull-paint-mask-texture`, answering question `hull-paint-mask` with option
  `texture-sidecar`: "I want a mask map per ship for livery, I'll be rebuilding
  the nonsense geometry and unwrapping anyway, I want these to stand up to my own
  authoring standards even if tripo does a lot of the leg work."

The prior-art survey is `F:\Projects\aetheria-hull-materials-prior-art.md`
(Eyes, 2026-10-03). Anchors below are against `80de88f2`, the tip of the
in-flight `eureka/aetheria-release-ships-mounts`. The add-on is unchanged there
since `a93625c2`.

#### Body facts

**M13. Mod hulls render outside the game's ship lighting and fade.**
- `ShipModVisual.LoadAsync` builds `new GltfImport(...)` with no material
  generator (`ShipModVisual.cs:45`). glTFast's default Built-In generator therefore
  picks `glTF/PbrMetallicRoughness` for every hull material.
- Prefab hulls paint with `Aetheria/GlowFade` (`Assets/Shaders/GlowFade.shader`;
  `Car Paint White.mat` and `Cockpit.mat` use it). It does two things a glTF
  shader does not:
  - it lights by the volumetric ambient (`noambient`, `VolumeSampleColorSimple` in
    `vert`, `:35` and `:67-72`);
  - it dithers in and out by `_Fade` (`:84-88`).
- `EntityInstance.Awake` gives each material with `_Fade` a per-entity instance it
  fades. Every other submesh is swapped to `InvisibleMaterial` and only shown by
  `ShowUnfadedElements` (`EntityInstance.cs:86-99`, `:120-131`). So a mod hull pops
  where a prefab hull dissolves, and it is lit differently.
- Nothing tints any ship today. `Faction.PrimaryColor` and `SecondaryColor`
  (`Corporations.cs:35-38`) are read only by the sector map (`SectorMap.cs`).

**M14. glTFast keeps meshes readable and exposes every core material slot.** Read
from the package source (`com.unity.cloud.gltfast@11ddc2436f97`):
- `MeshGenerator.cs:539` calls `UploadMeshData(false)`, so meshes stay readable.
  The collider (`ShipModShips.cs:103`) and a particle mesh shape need that, so
  M10's readability doubt is closed for the importer. `ships-player` still checks
  it in a built player.
- `IMaterialGenerator.GenerateMaterial(MaterialBase, IGltfReadable, bool)` sees
  every core slot: `PbrMetallicRoughness.BaseColorTexture` and
  `MetallicRoughnessTexture`, `NormalTexture`, `OcclusionTexture`,
  `EmissiveTexture`, each with `index` and `texCoord` (`Schema/Material.cs:48-77`,
  `TextureInfo.cs:42-49`). `IGltfReadable.GetTexture(int)` returns the texture.
- glTFast decodes images only as parts of a glTF. It has no entry point for a
  loose PNG.

**M15. Blender exports a named colour attribute exactly** (probed for the
rejected vertex-colour option, kept as a fact).
- A `FLOAT_COLOR` corner attribute exported with `export_vertex_color='NAME'`
  becomes one exact `COLOR_0`.
- `'ACTIVE'` wrote every attribute, and its `COLOR_0` was all ones.

**M16. The Tripo hulls, measured.** A read-only probe of a copy of `Quiet.blend`:

| Hull | Triangles | Face-connected parts | Largest part's share of faces | Parts under 2% of hull length | Non-manifold edges | UV islands |
|---|---|---|---|---|---|---|
| Headliner | 50,008 | 189 | 33% | 23 (647 faces) | 6,145 | 6,237 |
| Dexter Quiet | 9,614 | 12 | 87% | 8 (124 faces) | 524 | 1,386 |
| Sinister Quiet | 10,880 | 12 | 88% | 7 (114 faces) | 356 | 1,525 |

- The UV atlases are confetti: 1,400 to 6,200 islands in one 4096² image. Nothing
  is authored in that space. The operator's rebuild gives each hull a clean UV0,
  and the Tripo mesh becomes a bake source.
- K-means (CIELAB, a 512² sample) of the albedos:
  - Headliner's is colourful. With k = 6, the clusters are dark navy (43% of the
    texels), mid grey (21%), beige (13%), lavender (11%), orange (7%) and blue
    (4%).
  - Quiet's is near-greyscale (a* and b* within ±6): light panels (44% at L 69 to
    79), darks and teal accents.
  - Within-cluster lightness spread is 3 to 12 L. That spread is baked shading and
    detail.
- Probe scripts: `kmeans.py`, `meshstats.py`, `parts.py`, `vcol.py` and
  `maskpng.py` in this pass's scratch. Their numbers are quoted here.

**M17. Thruster and radiator mounts are already meshes at runtime.**
- `ShipModShips.Assemble` requires a `MeshRenderer` under both
  (`ShipModShips.cs:87-92`, `:128-137`).
- On `80de88f2`, `ships-mounts` hides the thruster's mesh behind the template's
  `Invisible` material (`:130`, commit `da2a299b`).
- `ShipInstance.SetEntity` hands that renderer to the exhaust particle system as
  its shape (`ShipInstance.cs:68-71`). Unity's documentation says a mesh shape
  emits along the surface normal.
- The GLB rule is not checked before Unity. `ShipModCatalog.Bind` checks only that
  each anchor's node exists (`ShipModCatalog.cs:176-180`), and
  `ship-authoring validate` reads the records alone
  (`ShipAuthoringCommands.cs:32-37`).

**M18. Blender saves a byte mask raw.** Probe, Blender 5.2.2: a 4×4 RGBA byte
image whose pixels are `(0.5, 0.25, 1.0, 0.5)`, saved as PNG.
- Tagged Non-Color or sRGB, it is written as 8-bit RGBA (colour type 6) with
  bytes `128, 64, 255, 128`. The values are raw, with no transfer applied.
- A float image saves as 16-bit.
- The `.gitattributes` rule `*.png filter=lfs` already covers package masks.

#### The hull material model

*Superseded in part by "Hull materials as owned graphs" below: the mask semantics, `HullPaint`, the shader rules and the livery resolution. Kept as the record of revision 2.*

**What a package carries.**
- **The GLB's core PBR slots,** baked by the operator onto her clean UV0:
  - base colour;
  - metallic-roughness;
  - normal;
  - occlusion;
  - emissive texture and factor.

  Missing slots fall back to factors.
- **`ship.mask.png`, the livery mask:** RGBA8 on UV0, one per hull.
  - R, G and B are the weights of the primary, secondary and accent paint. Black
    is bare: the GLB's own surface shows.
  - A is wear and grime: 0.5 is neutral, toward 1 the paint wears to the base
    surface, toward 0 grime gathers.
  - She authors it to her standard. The add-on offers starting points: a region
    mask baked from albedo clusters, and a wear alpha baked from curvature and AO.
- **Blender material slots become glTF materials.** These are surface kinds, such
  as bare metal, glass and lights, each with its own factors.
- **`ShipAuthoring.Paint`** (key 5, a `HullPaint`), typed in the package:
  - `Mask`, the mask's relative path;
  - the factory `Livery`: three colours, a metallic and smoothness per slot, wear
    and grime;
  - `RegionLuminance`, each region's mean albedo luminance under the mask, derived
    at Package.

  Null means an unpainted hull. It renders with the GLB's look, and still fades
  and is lit like a prefab hull.

**What the game owns.**
- One shader, `Aetheria/Hull`, built on `GlowFade`'s fade, dither and volumetric
  ambient. It samples the mask on UV0. Its rules:
  - Painted albedo is the slot colour × saturate(lum(base) / region luminance).
  - Wear: where alpha is above 0.5 and a tiling noise is under `_Wear`, paint
    chips to the base.
  - Grime: below 0.5, `_Grime` darkens and roughens.
  - Painted metallic and smoothness come from the slot's finish.
  - It has no `shader_feature` keywords, so build stripping cannot drop a variant.
- **Mask import: linear, DXT5, with mips.**
  - Linear, because the channels are weights and not colours: an sRGB decode
    would turn a 0.5 wear value into 0.21. M18 shows Blender writes the values
    raw, so the decode must not apply a transfer either.
  - DXT5 (BC3) is a quarter of RGBA32's memory: a 2048² mask with mips is about
    5.3 MB instead of 21 MB.
  - Its alpha block is the best-kept channel, and the alpha carries wear. Region
    edges lose only within a 4×4 block, about 2 cm on a 40 m hull at 2048.
- **`ShipModShips.PreloadAsync`** reads the PNG with `File.ReadAllBytes` and
  `ImageConversion.LoadImage` into a linear texture with mips, compresses it, and
  makes it non-readable.
- **`HullMaterialGenerator`,** a glTFast `IMaterialGenerator` with one instance
  per package, clones `ShipModTemplate.Hull` with the GLB's slots and the hull's
  mask. No glTF shader is needed at runtime.
- **`ShipModShips.ApplyPaint`** sets the resolved livery through a
  `MaterialPropertyBlock` in `ShipInstance.SetEntity`. Property blocks do not
  survive `Instantiate`.
- **Livery resolution** is one pure function, `HullPaint.Resolve`: the owner
  faction's `Livery` (`Faction` key 16, cut `ships-hull-livery`) when set, else
  the factory livery. The mask and region luminance are always the hull's.

**Role materials and the two mount meshes** (ruling
`thrusters-radiators-are-meshes`):
- **Thruster:** a mesh carrying the invisible material. It is the exhaust's
  emission shape, and its normals point aft.
- **Radiator:** a mesh rendered with the template's `Radiator` material, which
  `EntityInstance` heats.
- **Weapons** stay point anchors.

**Out of release scope, as follow-ups:**
- `hull-decals`. With a mask texture, painted-in logos can live in the mask; only
  separate insignia and damage decals remain.
- `hull-damage-wear`.
- `hull-player-paint`, unless `hull-livery-scope` rules otherwise.

#### Per-hull steps

Ruling `hull-paint-mask-texture` sets the time budget as hers. These are the steps
and who does each.

| Step | The operator | The add-on |
|---|---|---|
| P1 Tripo export | Generate; try PBR and delight once (below) | — |
| P2 New Ship | id, name, reference hull, length | `create --like`; the Tripo mesh moved into `Source` and oriented and scaled; a draft grid rasterised from it |
| P3 Rebuild | Retopologise the nonsense geometry under Ship Root, at its final scale, with her own tools | Rasterise again from her meshes |
| P4 Unwrap | A clean UV0 | Package refuses a mesh without UV0 or with UV0 outside [0, 1] |
| P5 Rebake | Check and fix the result | Bake From Source: Selected to Active from `Source`, albedo (colour only) and normal, wired into her material |
| P6 Mask | Paint the mask to her standard | New Mask; Suggest Regions; Bake Starting Mask; Bake Wear (curvature and AO into alpha); factory colours from the clusters |
| P7 Surface kinds | Material slots for glass and lights | — |
| P8 Mounts | The faces for each thruster and radiator, the cursor for each gun | Emitter normals aft; hardpoint rows on the cells beneath |
| P9 Layout | Internal hardpoints, cell touch-ups | — |
| P10 Package | — | Recentring, collider, map icon, shield, tractor, texture cap, GLB, `ship.mask.png`, region luminance, records, validation |
| P11 Look | Dock, `give`, fly | — |

Dropped with the rebuild:
- `Select Debris` and the mesh report: her new geometry replaces the debris.
- The triangle-budget warning: her topology is her standard.

The texture cap stays because it bounds runtime memory.

**P1, the Tripo export check: a step for the operator to try.**
- Tripo's API defaults to `pbr=true`, which yields base colour, metallic,
  roughness and normal maps. Texture version `v3.5-20260815` adds `delight`
  (default true).
- The hulls in `Quiet.blend` arrived with base colour only, and why is unknown.
- A delit, PBR source makes Bake From Source's albedo cleaner and gives a
  metallic-roughness map to bake too.
- Try one hull on the site and through the API. Nothing waits on it.

#### Cuts

*Revised by "Hull materials as owned graphs" below: `ships-hull-material` r3, `ships-addon-paint` r3 and `ships-hull-livery` r2 replace the revisions named here.*

- **`ships-hull-material` r2:** the shader, the generator, `HullPaint` with
  `Mask`, the mask's PNG checks in `Bind`, the linear DXT5 load and `ApplyPaint`.
- **`ships-hull-livery` r1** (unchanged): `Faction.Livery`. It waits on
  `hull-livery-scope`.
- **`ships-addon-paint` r2:**
  - the mask slot and New Mask;
  - Suggest Regions and Bake Starting Mask;
  - Bake Wear;
  - the UV0 and mask checks;
  - the mask export;
  - `ship-authoring paint --mask`.
- **`ships-addon-package` r2** (unchanged): `validate` reads the GLB; `Bind`
  refuses a thruster or radiator node without a mesh.
- **`ships-addon-frame` r3:** `Source`, rasterising from her meshes, the export
  leaving out `Source`, and Bake From Source. No debris tools.
- **`ships-addon-mounts` r3:** face-based thruster and radiator mounts on her
  meshes, and no triangle warning.
- **`ships-player` r2** (unchanged): the no-`glTF/`-shader check, radiator
  material, emitter probe. `*.png` is already LFS.

Order: `ships-mounts`, then `ships-addon-package`, then `ships-addon-frame`, then
`ships-addon-mounts`. `ships-hull-material` runs beside the add-on cuts after
`ships-mounts`. `ships-addon-paint` needs both lines; `ships-hull-livery` and
`ships-player` need `ships-hull-material`.

#### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Hull paint | `ShipAuthoring.Paint` in the package | Mask path and factory livery written at Package from the panel; region luminance re-derived every Package | `ship-authoring paint` writes it; `ShipAuthoringStore.Validate` judges it |
| Livery mask | `ship.mask.png` in the package, named by `Paint.Mask` | Authored by the operator in Blender; capped copy exported every Package | The operator; `Bind` judges the PNG header |
| Bake source | The `Source` collection in the `.blend` | Imported once from Tripo; never exported | Input to Bake From Source and to the first rasterise only |
| Faction livery | `Faction.Livery` in `Aetheria.cc` | Authored in the inspector; null keeps factory paint | The catalog |
| Hull material | One clone of `ShipModTemplate.Hull` per glTF material | Built at preload with the hull's mask; cache only | `HullMaterialGenerator` |

#### Rationale

**Why a sidecar PNG loaded by `ImageConversion`, not a texture inside the GLB.**
- Inside the GLB, the mask would have to be referenced from `extras`. That means a
  post-export rewrite of the GLB (Blender cannot write a texture index into
  extras), glTFast's Newtonsoft `ImportAddon` to read it, and load-bearing JSON
  where the package's other bindings are typed. Binding by image name instead is
  the name-as-authority the assembler is forbidden.
- glTFast has no loose-image entry point (M14). `ImageConversion` is built into the
  engine, so the sidecar costs one typed field, one header check and about a dozen
  lines of load code.
- Blender saves the image directly (M18).

**Why a generator instead of post-import material swaps.**
- glTFast calls the generator once per glTF material, with the slots already
  parsed.
- Swapping afterwards would leave glTF shaders as build inputs and would decide
  materials by renderer.

**Why the tint still divides by region luminance.**
- Her rebaked albedo may keep Tripo's colours under painted areas.
- Dividing by each region's mean keeps panel shading at any livery colour without
  asking her to author a greyscale albedo. The factory livery's colours,
  suggested from the cluster means, reproduce the baked look.

**Why the bake helpers are offered at all.** Selected-to-Active needs a selection
order, a cage or ray distance, the colour-only contributions, new images in the
right colour space and node wiring, and that setup is repeated per hull and per
map. Clustering and the curvature-and-AO alpha are starting points she paints
over. Each `ships-addon-*` operator check asks which helper earned its place.

**Why thrusters duplicate faces but radiators separate them.**
- A thruster's mesh is never drawn, so the hull keeps its nozzle.
- A radiator's faces are drawn with the radiator material. Leaving them in the hull
  would draw the surface twice.

### Hull materials as owned graphs

This section designs the hull material system as one piece. It replaces the
mask semantics, the `HullPaint` shape, the shader rules and the livery
resolution of "The hull material model" above. M13 to M18 and the per-hull steps
stand, except P6, which is restated below.

The operator's words, in order:
1. `hull-runtime-materials`: "we want more runtime material control than just a
   single baked map. See how MechWarrior does it".
2. `hull-paint-mask-texture`: "I want a mask map per ship for livery, I'll be
   rebuilding the nonsense geometry and unwrapping anyway".
3. `mask-channels-place-materials`: "If each channel determines placement of one
   material, then factions can fly their colors by setting parameters on those
   hull materials (primary, secondary, trim) and we'd ship eventual faction skins
   as their own material map... If we also want procedural wear and buildup, we
   can bake input maps for that, too."
4. `clean-break-from-substance`: "Let's make a clean break from substance, it's
   dead".
5. Not yet a ruling: "Authoring materials as graphs in blender is pretty
   intuitive, if we're owning material generation, could we not set things up so
   that we can evaluate the same graph?"
6. Not yet a ruling: "We may not be allowed to pull blenders math into our
   runtime, but nothing prevents us from putting our math in blender".
7. `materials-baked-per-ship-incremental`: "triggering texture bakes per ship
   only when they spawn or change, and then also only recomputing the parts of
   the graph that change, which would be just wear for most dynamic changes".
8. `shaders-own-every-pixel`: "our shaders own every pixel".

Surveys: `F:\Projects\blender-graph-runtime-prior-art.md`,
`F:\Projects\aetheria-texture-graph-prior-art.md` and
`F:\Projects\aetheria-hull-materials-prior-art.md` (Eyes, 2026-10-03). Anchors are
against Aetheria `239f3599` (origin/master, which contains `ships-mounts` and the
render fixes) and CultLib `a7966142`. Probe scripts are in this pass's scratch
(`matprobe/probe_bake.py`, `probe_dump.py`, `bench/Program.cs`).

#### Body facts

**M19. The operator's Blender is 5.2.2 LTS from Steam, and Steam updates it.**
- `C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe` reports
  `5.2.2 LTS`, build hash `d13f752e3b9c`, built 2026-09-15. It is the only
  Blender in the uninstall registry. A stale 5.0.1 sits in
  `D:\Steam\SteamApps\common\Blender`.
- Steam replaces the build when a new one ships. Node semantics changed in 5.0
  (Voronoi hashing), so a silent update can move her previews away from the game.

**M20. A Cycles Emit bake is an exact point oracle.** Probe `probe_bake.py`,
Blender 5.2.2, a 64×64 float input image of coordinates read with Closest
interpolation into a graph, baked by Emit into a 64×64 float image (margin 0):

| Probe | CPU | OptiX (GTX 1070) |
|---|---|---|
| P1 identity: the input texel reads back | 4096/4096 exact | 4096/4096 exact |
| P2 `a*b + c` in two Math nodes | 4096/4096 equal float32 `(a*b)+c`; fused would differ at 1011 | same |
| P3 CultMath's float-only `mod289((x*34+10)*x)` as Math nodes | 4096/4096 exact | 4096/4096 exact |
| P4 Noise Texture (3D, detail 0): two bakes | bit-identical; 0.5 at integer points | bit-identical |
| P5 an OSL Script node (`In[0]*3+1`) in the bake | 4096/4096 within 1e-6 | no result: killed after 4.5 minutes, apparently compiling OSL |

- CPU and OptiX noise agree at 2,764 of 4,096 texels, at most 1.19e-7 apart
  (1 ulp near 1). Arithmetic agrees bit for bit. So the oracle is CPU Cycles,
  and noise parity is a ULP tolerance, never bit equality, once a GPU is in the
  chain.
- Cycles' CPU kernels do not contract multiply-add (P2), so float-only
  arithmetic is reproducible from C#.

**M21. The add-on can dump a node group faithfully.** Probe `probe_dump.py`,
Blender 5.2.2:
- Group interfaces have panels. A socket reports its panel, type, default,
  min, max and a stable identifier (`Socket_3`). Panels are typed structure, so
  a socket's scope (Livery, Ship, Maps) needs no name parsing.
- `ColorRamp.evaluate(t)` and `CurveMapping.evaluate(curve, t)` run in Python,
  so ramps and curves export as sampled tables in Blender's own evaluation (a
  B-spline ramp gives 0.1667, not 0, at t = 0).
- A group instance is `ShaderNodeGroup` with `node_tree`. Mix exposes
  `data_type`, `blend_type` and both clamps. Noise exposes dimensions, type and
  normalize. Math has 41 operations.
- Shader nodes have no Bit Math or Integer Math in 5.2.2.
  `FunctionNodeBitMath` exists for geometry nodes only. PCG hashes cannot be
  built in shader nodes.

**M22. A C# tape interpreter is cheap for arithmetic and slow for noise.**
Probe `bench`, .NET 10 RyuJIT on Starfire (Ryzen 3 3100, 4 cores), a scalar SSA
tape interpreted over 256-texel batches at 2048²:
- 154 arithmetic instructions: 788 ms on one thread (1.2 ns per
  instruction-texel).
- Adding 24 CultMath `snoise(float3)` calls: 23.5 s on one thread, 7.1 s on four
  (about 230 ns per noise call per texel).
- Unity runs Mono, which is slower than RyuJIT. That was not measured.

A full-resolution noise graph costs seconds per bake on the CPU. Aetheria
already dispatches compute shaders at runtime (M23), where the same work takes
milliseconds.

**M23. Aetheria's compute and CultMath substrate.**
- Runtime compute already ships: `ShieldPanel.cs:293-439` dispatches
  `ShieldSim.compute`; Stardust, Slime and Lightning have compute kernels under
  `Assets/Shaders/Compute/`.
- `Packages/manifest.json:57` pins `org.gamecult.cultmath` at
  `cultmath-unity-v0.2.4`. CultLib has tagged `cultmath-unity-v0.3.0`.
  `Aetheria.Shared.csproj:27` compiles CultMath from source.
- No Shader Graph or SRP package is in the manifest.

**M24. Faction key 16 is taken twice.** `ships-hull-livery` r1 puts `Livery` at
`Faction` key 16. Rulings `faction-relations-field` and `faction-doctrine-typed`
put Relations at 16 and Doctrine at 17. `Corporations.cs` uses keys up to 15.
The livery takes key 18.

**M25. CultMath's seams for new shader code.**
- `CultMath.hlsl:509-510` includes Phacelle and Interval.
- `GlslLowering.SeparateFiles` (`GlslLowering.cs:19-24`) lowers a
  separately-licensed include into its own GLSL file. Its one entry is MPL-2.0
  Phacelle.
- C# has `asuint` and `asfloat` (`math.cs:390-391`) but no `uint2/3/4` types.
  Cycles' hashes are scalar `uint`, so they port without new vector types.

#### The system in brief

```
Blender (author, preview)        AetherDb (judge)                 Aetheria.cc
 node group "Hull Standard"  -->  material-graph put  ---------->  MaterialGraph record
 + per-hull wrapper material      whitelist, contract, cones        (truth)
                                        |
                                        v  lowering (Aetheria.Shared)
                                  CultMath expression IR  --> HLSL kernels: pair, frontier, wear
                                        |                     (generated, committed, dxc-checked)
                                        v  reference evaluator (C#, tests only)
Unity: MaterialBakes  -- dispatch per (hull, livery) and per wear set --> baked textures
       Aetheria/Hull  -- samples Surface, Worn, Wear, plus the GLB's normal, occlusion, emission
```

**A material graph is one per-texel function:**
- **Inputs:**
  - image roles sampled on UV0 from two package PNGs:
    - `ship.mask.png`: R, G, B place the primary, secondary and trim materials; A is
      an authored wear hint.
    - `ship.maps.png`: R ambient occlusion, G curvature (0.5 flat), B cavity, A grunge.
  - parameters in two scopes. Livery parameters are set per faction, with a
    factory default per hull. Ship parameters are set per ship, such as wear level.
- **Outputs**, a closed vocabulary:
  - Surface Color, Surface Metallic and Surface Roughness: the finished livery;
  - Worn Color, Worn Metallic and Worn Roughness: what wear reveals;
  - Wear, from 0 to 1.

**Authoring in Blender.**
- A library `.blend` holds node groups built from whitelisted nodes. The interface
  panels are `Livery`, `Ship` and `Maps`. A Maps socket's name is a role from the
  closed vocabulary.
- The per-hull material her add-on sets up is a wrapper:
  - the group instance;
  - Image Texture nodes for her mask and maps;
  - a preview that mixes Surface and Worn by Wear into a Principled BSDF, the same
    combination the game's shader makes.
- The wrapper is display only. Only the group is exported.

**The release whitelist** is Blender's primitive and converter nodes, with
Cycles semantics ported into CultMath:
- Math (all 41 operations), Vector Math (component-wise operations), Clamp,
  Map Range (Float, all four modes);
- Mix (Float, Vector, and Color with every blend mode);
- Color Ramp and Float Curve, as tables sampled in Python;
- Separate and Combine XYZ and Color (RGB mode), RGB to BW, Invert;
- Value, RGB, Reroute, Frame, Group Input and Output, and nested groups (flattened).

Procedural textures (Noise, White Noise, Voronoi, Wave, Gradient) follow the
release, as question `release-procedural-noise` proposes. Per ruling 3, release
wear comes from baked maps. Grunge is baked into `ship.maps.png` A.

**Bakes, dirty sets and the memory budget.**
- The judge computes each output's cone (the nodes it reads) and each node's
  scope: hull (reads only images), livery, or ship. Surface and Worn cones may
  not read a Ship parameter; the judge refuses that. So every per-ship cost is
  the Wear output alone.
- The **frontier** is the set of values in Wear's cone that read no Ship
  parameter but feed one that does. At most four scalars, or the judge refuses
  the graph, so they fit one RGBA16F texture.
- Three kernels per graph, generated from the record:
  - **pair**: Surface at R and Worn at R/2, for each (hull, skin, livery). R is
    2048 by default and 1024 at low quality.
  - **frontier**: the frontier values at wear resolution, once per pair.
  - **wear**: only the ship-scope nodes of Wear's cone, reading the frontier,
    per distinct set of Ship parameters.
- A Ship parameter change re-runs only the wear kernel. That is ruling 7: cost
  in proportion to the nodes that changed. A livery change is a different pair.
- Caches are content-addressed. A pair key hashes the graph record, both PNGs,
  the skin, the livery bindings and R. A wear key hashes the pair key and the
  Ship parameter values, quantised to 1/64. Ships with identical inputs share
  every texture. Entries are refcounted by live bodies and released at zero.
- Budget, at R = 2048, with BC1 (DXT1) through `Texture2D.Compress` after an async
  readback:

| Texture | Per | Size with mips |
|---|---|---|
| Surface Color and Surface MR (R metal, G roughness) | pair | 2 × 2.7 MiB |
| Worn Color and Worn MR at 1024² | pair | 2 × 0.67 MiB |
| Frontier, 512² RGBA16F, no mips | pair | 2 MiB |
| Wear, 512² R8 | wear set | 0.33 MiB |

  - That is about 8.7 MiB per pair and 0.33 MiB per ship. At R = 1024 a pair is
    about 2.6 MiB.
  - A zone with 20 ships in 6 (hull, livery) pairs holds about 59 MiB. Per-ship
    full bakes would hold about 320 MiB.
  - Each bake also needs about 70 MiB of transient upload and render textures,
    released once its readback completes.
  - Until the compressed copy exists, the ship samples the render textures directly.

**The runtime shader.**
- `Aetheria/Hull` is `GlowFade`'s surface shader: the same `noambient` volumetric
  ambient, the same dither fade by `_Fade` and the same edge colour.
- The albedo, metallic and roughness are `lerp(Surface, Worn, Wear)`. The normal,
  occlusion and emission come from the GLB's slots.
- It has no paint logic, no keywords beyond GlowFade's own, and nothing evaluated
  per frame but that one lerp. The lerp is fixed by the output contract.

**The livery.**
- `HullPaint` (ShipAuthoring key 5) names:
  - the graph;
  - the two PNGs;
  - the factory livery: bindings of the graph's Livery parameters.
- `Faction.Livery` (key 18) holds bindings of the same parameters and wins over the
  factory livery when that faction flies the hull. `HullPaint.Resolve` is the one
  resolution.
- A binding names a parameter by its interface identifier. The catalog test checks
  every binding against the graph: it must exist, be in scope and be in range.
- Faction skins (another mask per hull) are a later key on `HullPaint`, chosen by
  the livery.

#### Authority map

- **Owner.** The `MaterialGraph` record in `Aetheria.cc` owns what a hull material
  computes. CultMath owns what each node means: Cycles semantics ported once, in
  HLSL with a C# mirror. `MaterialBakes` owns when and at what resolution a graph
  is evaluated. `HullPaint.Resolve` owns which livery a body shows.
  `Aetheria/Hull` owns how the three outputs and the GLB slots become a pixel.
- **Inputs.**
  - The judge reads the Blender dump.
  - The lowering reads the record.
  - `MaterialBakes` reads:
    - the generated kernels;
    - the package PNGs;
    - the resolved livery;
    - the body's Ship parameters.
- **Outputs.** The record; the generated kernels; the baked textures per pair and
  per wear set; one property block per hull renderer.
- **Derived state.**
  - The kernels are derived from the record and committed. A test fails when they
    differ from the lowering.
  - Baked textures are cache only.
  - The Blender wrapper and its preview are display only.
  - Cones, scopes and the frontier are recomputed by every judge and lowering.
- **Forbidden writers.**
  - Unity Shader Graph, Mixture and any glTF/ shader on a mod hull.
  - Any per-frame graph evaluation in the hull shader.
  - Python encoding a `MaterialGraph` or `HullPaint`.
  - Blender's GPL EEVEE (`gpu_shader_material_*`) or blenlib noise code in any
    CultLib or Aetheria file.
  - A material, role or output chosen by a name outside the closed vocabularies.
  - A second livery resolution.
  - `Faction.PrimaryColor` and `SecondaryColor`, which stay map colours.
- **Shared paths.** Editor play, the preview smoke, the play smoke and the built
  player all bake through `MaterialBakes` from the same kernels. Every AetherDb
  path (`put`, `check`, the catalog test) judges through one `MaterialGraphJudge`.
- **Deletion line.**
  - r2's region tint (`_PaintStrength`, `RegionLuminance`, a tiling `_WearNoise`)
    and its `Livery` of three colours are never built.
  - Revision 1's vertex mask is gone.
  - The 41 `.sbsar` files go with follow-up `remove-sbsar-files`.

#### Parity

1. **Node semantics, in CultMath.**
   - Each ported function's HLSL mirrors its C# with bit equality, through the
     existing mirror tests.
   - It compiles under dxc and lowers to GLSL through the existing
     `GlslLowering`. Each new licence file gets a `SeparateFiles` entry.
   - The C# matches a **Cycles oracle fixture**. A GPL-headed tool script, run only
     under the pinned Blender (5.2.2 LTS, hash `d13f752e3b9c`), bakes each family
     on the CPU the M20 way. It refuses any other version. The fixture records the
     version and hash.
   - Tolerance is per family: exact for arithmetic (M20 P2, P3), and a stated ULP
     bound wherever Cycles uses library transcendentals.
2. **Lowering, in Aetheria.**
   - A test graph uses every whitelisted node and blend mode. The add-on's test
     script dumps it and bakes all seven outputs on a 64² fixture under the pinned
     Blender. The fixture (dump, inputs, outputs) is committed.
   - The test lowers the dump and runs CultMath's reference evaluator, then
     compares per output within the family tolerances.
   - This pins the node-to-function mapping. Neither side's unit tests can see that
     mapping.
3. **Kernels.**
   - The generated HLSL is committed and must equal the lowering's output.
   - Each kernel compiles under dxc.
   - A Unity batch smoke on Starfire dispatches the fixture graph on the GPU and
     reads back. It compares with the reference evaluator within 2 ulp plus the
     family tolerance. M20 shows a GPU and a CPU Cycles already differ by 1 ulp.
4. **Drift.**
   - The add-on stamps the Blender version into each dump.
   - The judge refuses a dump from a different major and minor version than the
     pinned semantics, naming both.
   - Updating Blender is a deliberate cut: regenerate the oracle fixtures, read the
     diff, and move the pin.
   - Before then, the operator stops Steam updating Blender, or keeps a pinned
     portable 5.2.2 for authoring.

#### The tape

The material evaluator is not a second tape if it owns no evaluation semantics.
- It lowers into CultMath's scalar expression IR: SSA values, operations that
  are CultMath functions, named inputs, parameters and several outputs, with each
  instruction's input-dependence mask.
- Two backends exist now: an HLSL emitter, and a reference evaluator for tests.
- The tape target's step 2 adds bytecode with point, interval and gradient
  evaluators over the same IR. Step 4 adds an HLSL interpreter of that bytecode.
  Neither is built now. Ruling `tape-target-unparks-when-asura-stable` stands
  for both.
- What lands now is the IR the tape will consume. Its point evaluator is only a
  test reference, not a runtime path.

A minimal evaluator is needed now, because ruling 7 requires bakes at spawn and
at change, and the release hulls need faction colours (`content-bar-one-per-concept`).

Mod-authored graphs need the tape's GPU interpreter, because Unity cannot compile
a shader at runtime. Until then a mod package uses the shipped graphs with its
own PNGs and livery values: the MWO model, per the operator's ruling 3.

Question `material-ir-in-cultmath` asks whether this IR may land in CultMath
while the tape stays parked.

#### Cuts

CultLib, campaign `cultmath-tapes`:
- **`cycles-converters`.** The Cycles oracle tool and fixture harness. Ports of
  Cycles' Math, Map Range, Clamp, Mix (every blend mode), RGB to BW and ramp-table
  lookup, in an Apache-2.0 file pair.
- **`expr-ir`.** The scalar expression IR, dependence masks, cones and frontier,
  the reference evaluator and the HLSL emitter.
- **`cycles-noise`.** Ports of Cycles' hash, Perlin, fBM, the Noise Texture node
  and White Noise (BSD-3 and Apache file pairs). This follows the release.
- The CultMath Unity release that ships the new HLSL is a release cut (follow-up
  `cultmath-unity-material-release`). Aetheria's Unity pin must move to it before
  `hull-bakes`, whose kernels include those files.

Aetheria, campaign `aetheria-release`:
- **`material-graph`.** The record, the dump, `MaterialGraphJudge` (whitelist,
  vocabularies, scopes, the contract, the version pin), and AetherDb
  `material-graph put|check`.
- **`material-lowering`.** Lowering to the IR, the generated kernels and their
  equality test, and the whole-graph Blender oracle.
- **`material-library-export`.** The add-on's dumper, Export and Check calling
  AetherDb, and the library `.blend` convention.
- **`hull-bakes`.** `MaterialBakes`: dispatch, the content-addressed tiers,
  refcounts, readback and compression, and a GPU parity smoke.
- **`ships-hull-material` r3.** `HullPaint` reshaped, the PNG checks in Bind,
  `Aetheria/Hull`, the generator and `ApplyBakes` from `SetEntity`.
- **`ships-addon-paint` r3.** The maps bake into `ship.maps.png`, the mask tools,
  Set Up Hull Material (the wrapper), and Package writing both PNGs and `Paint`.
- **`ships-hull-livery` r2.** `Faction.Livery` at key 18, as bindings checked
  against the graph.

Order:
- `cycles-converters`, then `expr-ir`, then the CultMath Unity release.
- Then `material-graph` (it may start at once; it needs neither CultLib cut),
  `material-lowering`, `hull-bakes`, `ships-hull-material` r3 and
  `ships-hull-livery` r2.
- `material-library-export` follows `material-graph`.
- `ships-addon-paint` r3 needs `ships-addon-mounts`, `material-library-export`
  and `ships-hull-material` r3.
- `ships-player` keeps its dependency on `ships-hull-material`.

**What blocks the release hulls:** every cut above except `cycles-noise`.

**What can follow:**
- `cycles-noise` and Voronoi, Wave and Gradient;
- CultMath-native nodes in Blender;
- the tape's GPU interpreter for mod graphs;
- faction skins;
- follow-ups `hull-damage-wear` (the first real Ship parameter), `hull-decals`
  and `hull-player-paint`;
- detail normals per material;
- a GPU BC encoder, if the readback-and-compress hitch measures badly.

#### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Material graph | `MaterialGraph` in `Aetheria.cc`, keyed by its stable id (the group's name lowered, as ship ids are) | Exported from the library `.blend` by `material-graph put`. Re-exported whole on change, never edited in place. The kernels are regenerated in the same command | `MaterialGraphJudge` admits it. The record is truth and the kernels are derived |
| Hull paint | `ShipAuthoring.Paint` (key 5) in the package | Written at Package. A graph id, two PNG paths and the factory livery bindings | `ship-authoring paint`; Validate, and the catalog test for bindings |
| Mask and maps PNGs | `ship.mask.png`, `ship.maps.png` in the package, named by `Paint` | Authored and baked in Blender; exported every Package | The operator. `Bind` judges the headers |
| Faction livery | `Faction.Livery` (key 18) | Authored in the inspector; null keeps the factory livery | The catalog; checked against the hull's graph |
| Pair bake | Content hash of graph, PNGs, skin, livery and R | Baked when the first body needs it; released when the last is gone | `MaterialBakes`. Cache only |
| Wear bake | Content hash of the pair key and quantised Ship parameters | Re-baked when a body's Ship parameters change | `MaterialBakes`. Cache only |
| Cycles oracle fixture | A file per family in CultMath tests, stamped with the Blender version | Regenerated only by a pin-moving cut | The tool script under the pinned Blender |

#### Rationale

**Why generated compute kernels and not a CPU interpreter.** M22 puts a
noise-bearing full-resolution bake at seconds on the operator's CPU, under a
faster JIT than Unity's. Aetheria already ships runtime compute (M23). The graphs
are game-owned, so kernels can be generated ahead of time and compiled into the
build, where a runtime shader compile is impossible. The cost is that mods cannot
bring graphs until the tape's GPU interpreter exists. That is question
`material-evaluator-backend`.

**Why the per-ship cost is the Wear output alone.** Prior art bounds memory one of
two ways:
- a shared base with a small per-instance mask, as in Destiny's gearstack wear
  mask, MW5's wear masks and Star Citizen's vertex-alpha wear;
- a cap on instance count.

Making it a rule of the output contract, enforced by the judge, makes the budget
structural rather than a convention. The lerp in the shader is the contract's one
fixed line, so the shader owns no material meaning.

**Why the frontier is cached at wear resolution.** Ruling 7 asks that a wear change
recompute only what depends on wear. Caching every node at full resolution is
Mixture's documented failure: memory linear in node count. One RGBA16F texture at
512² per pair costs 2 MiB and makes a wear bake a few dozen instructions over
262k texels.

**Why Cycles semantics are ported, not reimplemented from the manual.**
- The manual leaves Mix's blend modes, Map Range's smooth modes, ramp
  interpolation and Smooth Min unspecified (Eyes §3d).
- The Cycles kernel files are Apache-2.0, and `noise.h` is BSD-3.
- Porting them into licence-headed CultMath files, as Phacelle is kept under
  MPL-2.0, gives the exact semantics with provenance.
- Ramps and curves need no port: Python samples Blender's own evaluation into
  tables (M21).

**Why the oracle is a CPU bake.** It is exact for arithmetic and reproducible
(M20). A GPU bake differs by an ulp on noise, and OSL on OptiX did not finish.

**Why the record holds the authored graph, not the IR.** The lowering will change
as ports are added and the tape arrives. The authored graph is what she made, and
the IR is a cache of one lowering of it.

**Rejected.**
- Unity Shader Graph: its Lit target adds Unity's ambient, and ruling 8 makes our
  shaders own every pixel.
- Mixture: it adds Shader Graph and SRP Core, and its memory grows with node count.
- Material Maker: a second authoring application with no runtime parameter path.
- MaterialX: it has no HLSL generator, Blender's export is partial, and its noise
  is not Blender's.
- OSL as an authoring route: Cycles only, no EEVEE preview, and no result on OptiX
  (M20 P5).
- Region-luminance tint (r2): every channel now places a whole material.

### Ship data all in the `.cc`

The operator, 2026-10-03, admitted as ruling `ship-data-all-in-cc`: "I want all
the data to live in the .cc. there's no reason our schemas can't hold everything
we need and it means we benefit from the cross runtime guarantees CultLib
offers". It restates MQ1 (2026-09-30), "a ship .cc holds everything", which the
GLB path contradicted.

This section moves the hull's geometry, node tree, surfaces and images out of
`ship.glb` and the planned PNG sidecars into one typed record in `ship.cc`. It
changes the package format, the Package action, the validator, the runtime
loader and five specs. Anchors are against Aetheria `origin/master` `4895c752`
and CultLib `main` `a7966142`, read on 2026-10-03.

#### Body facts

**M26. The GLB is read in three places, and only for its scene graph and
meshes.**
- The add-on exports it at Package with `export_extras` and `export_yup`
  (`aetheria_ships/__init__.py:343-366`, called at `:385`). The anchors and the
  layout already go into the `.cc` (`:386`, `ship_cc.py:116-140`).
- `ShipModCatalog.Bind` reads only the JSON chunk's node `extras` and whether each
  node names a mesh (`ShipModCatalog.cs:168-218`, through Newtonsoft). AetherDb
  `validate` reaches it through `ReadPackage` when the file exists
  (`ShipAuthoringCommands.cs:34-45`).
- `ShipModVisual.LoadAsync` imports it with glTFast and maps anchors to node
  indices (`ShipModVisual.cs:33-60`). The assembler then reads three things per
  node (`ShipModShips.cs:85-120`):
  - the `MeshRenderer`, for the map icon, the thrusters and the radiators;
  - the readable `MeshFilter` mesh, for the hull collider;
  - the shield marker's local scale.
- glTFast has no other consumer:
  - `ShipModVisual.cs` is its only `using GLTFast`, and `Packages/manifest.json:9`
    is its only pin.
  - `ShipFixture.cs:29-85` hand-builds a GLB for the tests.
  - `ShipModPreview.cs:28,125` goes through `LoadAsync`.

**M27. No package is committed.**
- `git ls-tree` of `4895c752` holds no `ship.cc`, `.glb` or `GameData/Mods` path.
  Only the operator's local drafts exist, and Package landed today (`7d649d5c`).
- `.gitattributes` puts `GameData/*.cc` and `*.png` in LFS.
  `GameData/Mods/<id>/ship.cc` matches neither rule.

**M28. CultCache sets no record size limit, but the single-file store works on
the whole file.**
- C#: `SingleFileBackingStore` reads the snapshot whole and deserializes every
  record (`CultCache.cs:3290-3320`). It writes through a temp file and
  `File.Replace`. It refuses a schema that is not registered (`ToStoredDocument`,
  `:3209`). So every reader of a ship file must register every record type in it.
- Python: the MessagePack store's `push` pulls the whole store, replaces one
  envelope and rewrites the file (`stores.py:103-121`). A record it did not
  touch is carried as raw bytes.
- The binding limits are MessagePack's bin32 (4 GiB per value) and .NET's 2 GiB
  array. A ship file at tens of MiB is far inside both. The real cost is that
  every Save Layout rewrites the whole file.
- When Python writes a record kind first, it gets `_default_catalog_entry`: an
  empty member list (`stores.py:356-368`). So Python may only replace payloads of
  records whose catalog entry C# wrote. That matches the standing rule that
  Python never builds a `HullData`.

**M29. CultLib's typed mesh is a chunk payload, not a model.**
- `GameCult.Geometry` has `CultGeometryTriangleMesh`: positions, normals, UVs,
  indices and a material per triangle (`CultGeometryDocuments.cs:370-395`).
- It has no tangents, no submesh ranges, no node tree and no images.
- Its fingerprint keys the `chunk_artifact` record, so a field added to it
  changes chunk keys.
- It has no Python, TypeScript or Rust mirror.
- `GameCult.Geometry.dll` is not in the CultLib Unity package
  (`unity/org.gamecult.cultlib/Runtime/Plugins`).

**M30. Blender's conventions already line up with the runtime.**
- `ShipPolyline` stores Blender's right-handed Z-up space, and the runtime
  converts with (−x, z, −y) (`ShipModVisual.cs:85-112`).
- Blender's UV origin and `Image.pixels` row order are bottom-left, and so are
  Unity's mesh UVs and `Texture2D.LoadRawTextureData`. glTF is top-left, which is
  why the exporter and glTFast each flip V.
- Both facts come from the documentation. A probe in `ships-cc-model` proves
  them.

#### The design in brief

```
Blender collection                    ship.cc (one file, the only truth)
 objects, evaluated meshes  --+       HullData        mod-hull:<id>    (C# writes; Python edits layout)
 material slots, images       +-->    ShipAuthoring   mod-ship:<id>    (anchors, lines, later Paint)
 (Package, Python, raw slots) |       ShipModel       mod-model:<id>   (nodes, meshes, surfaces, images)
                              |
AetherDb ship-authoring       +-- seed-model (C# writes the record's catalog entry), validate
Unity preload: ShipModel -> GameObjects, Mesh, Texture2D (no importer)
Compose: HullData + ShipAuthoring into the derived catalog; ShipModel stays in the package
```

**`ShipModel`** is `[CultDocument("aetheria.ship_model", "1")]`, stored at
`mod-model:<id>`.
- One record holds the whole body.
- Its space is Blender's: right-handed, Z up, metres, UV origin bottom-left.

| Key | Member | Type | Meaning |
|---|---|---|---|
| 0 | `Id` | string, `[CultName]` | The ship id |
| 1 | `Nodes` | `List<ShipNode>` | Parents before children |
| 2 | `Meshes` | `List<ShipMesh>` | Indexed by `ShipNode.Mesh` |
| 3 | `Surfaces` | `List<ShipSurface>` | Indexed by `ShipSubmesh.Surface` |
| 4 | `Images` | `List<ShipImage>` | Indexed by the surface and paint slots |

- **`ShipNode`:**
  - `Id`: the node's `aetheria.id`, or null for an unanchored render node;
  - `Parent`: an index, or −1 for the root;
  - `Translation` (float[3]), `Rotation` (float[4], xyzw) and `Scale` (float[3]);
  - `Mesh`: an index, or −1 for none.
- **`ShipMesh`:**
  - one typed array per attribute: `Positions` (float[3n]), `Normals` (float[3n]),
    `Tangents` (float[4n], w is the bitangent sign) and `Uv0` (float[2n]);
  - `Indices` (uint[]);
  - `Submeshes`, each with `IndexStart`, `IndexCount` and `Surface`.

  Triangles only. Bounds are not stored, because every runtime computes them.
- **`ShipSurface`:**
  - `Name`: the Blender material's name, for display;
  - `BaseColor`: an image index, or −1;
  - `BaseColorFactor` (float[4]).

  `ships-hull-material` r3 appends the slots its shader reads, such as the normal
  map and emission.
- **`ShipImage`:**
  - `Width` and `Height`;
  - `Format`, an enum open to later members: `Rgba8Srgb` and `Rgba8Linear` now,
    plus the block formats if question `ship-image-encoding` rules them;
  - `Levels` (`List<byte[]>`): mip 0 first, rows bottom to top, tightly packed.

  An image has no role of its own: the slot that names it gives it one.
- `ships-addon-paint` r3 appends `Mask` and `Maps` to `ShipModel` as image
  indices. They are not PNG files.

**`ShipAuthoring`.**
- Key 2 (`ModelAsset`) is retired and never reused, as key 1 was.
- Nothing in it names the model. The model sits at its deterministic key beside
  the hull and the visual, and `LoadRecords` already enforces the keys of those
  two.
- A file that still carries key 2 loads with no model record, so it is a draft.
  Nothing is lost silently, because the GLB beside it was never the truth.

**Why the model stays out of the derived catalog.**
- `Compose` rewrites `Aetheria.modded.cc` every boot (M28). Copying ten hulls at
  30 to 80 MiB each into it would rewrite hundreds of MiB per boot.
- So `Compose` keeps writing only the two semantic records.
- `PackageOf` opens `<modsRoot>/<id>/ship.cc` read-only and takes `ShipModel`
  from it. The game reads the body from the same file the operator authored.

**Writer: Package, with no GLB.**
1. `AetherDb ship-authoring seed-model <ship.cc>` adds an empty `ShipModel` at
   `mod-model:<id>` when none is there.
   - It is idempotent.
   - C# writes the catalog entry, so Python only ever replaces a payload (M28).
   - Drafts made before this cut take the same path, so no migration branch is
     needed.
2. Python packs the body by slot in `ship_cc.replace_model`, pinned like the
   anchor and hardpoint slots, and pushes it. The add-on builds the body:
   - **Nodes** come from the bound collection, leaving out `Source` and Grease
     Pencil. A node's TRS is relative to its nearest ancestor in the collection.
   - **Meshes** come from the evaluated depsgraph, with modifiers applied:
     - numpy, which Blender bundles, splits corners into vertices by position,
       normal, tangent and UV;
     - `calc_tangents` runs on UV0;
     - each material slot becomes a submesh.
   - **Surfaces** come from the material slots, and **images** from the images
     those slots use:
     - capped at 2048 (the cap moves here from `ships-addon-mounts`);
     - read with `pixels.foreach_get`;
     - written as RGBA8 in the image's colour space.
   - The body is packed with `msgpack.packb(..., use_single_float=True)`. Without
     that flag each float costs 9 bytes instead of 5.
3. `replace_visual` writes the anchors only.
4. `AetherDb ship-authoring validate` judges all three records.

**Reader: Unity builds from records.** `ShipModVisual.Load` becomes synchronous.
- It makes a `GameObject` per node and converts TRS with the line mesh's
  (−x, z, −y). That conversion moves into one `ShipSpace` helper shared by lines
  and meshes.
- For each mesh it builds a `Mesh`:
  - UInt32 indices above 65,535 vertices;
  - positions, normals and tangents converted the same way;
  - tangent w negated and each triangle's winding reversed, because the map is a
    reflection;
  - UVs passed through;
  - the mesh kept readable for the collider.
- For each image it builds a `Texture2D` with `LoadRawTextureData`, generates
  mips, runs `Compress`, and makes the texture non-readable.
- Until `ships-hull-material` r3, every surface takes one interim template
  material with its base colour. r3 replaces it with `Aetheria/Hull`.
- These go: glTFast, `UninterruptedDeferAgent`, the `Nodes` instantiator
  subclass, `NodeIndices` and `ModelPath`.

**Validation moves onto records.** `ShipAuthoringStore.Validate(hull, ship,
model)` is the one judge. `model` is null for a draft. It checks:
- node ids are unique, and every anchor's `ModelNodeId` names one;
- parents come before children, and every index is in range;
- each `thruster-emitter`, `radiator-mesh`, `map-icon` or `hull-collider` node
  has a mesh. Today the first two are checked at `Bind` and the last two only in
  Unity;
- the streams:
  - their lengths match the vertex count;
  - every float is finite;
  - every index is below the vertex count;
  - submesh ranges lie inside the index buffer;
- the images:
  - each level is sized exactly for its format;
  - no side is over 2048;
  - each image is in a colour space its slot allows.

`Bind` keeps the directory and refs checks, and loses `ReadNodeIds`. AetherDb
`validate` always reads records, and says "draft, no model yet" when the model
is missing.

**Sizes.** These are estimates for Headliner (50,008 triangles).
`ships-cc-package` measures them.

| Part | Raw RGBA8, mip 0 | Block formats with mips |
|---|---|---|
| Mesh: ~30k vertices × 12 floats at 5 bytes, plus indices | ~2.3 MB (9.5 MB if every triangle were split) | same |
| One 2048² image | 16 MiB | 5.3 MiB (BC7 or BC5) |
| First cut: base colour only | ~19 MiB | ~8 MiB |
| After paint and r3: base, normal, emission, mask, maps | ~83 MiB | ~29 MiB |

- No CultCache limit binds here (M28).
- What matters is that every Save Layout rewrites the file. At 80 MiB that is
  roughly a second in Python, against milliseconds today. That is one input to
  question `ship-image-encoding`.
- `GameData/Mods/*/ship.cc` needs an LFS rule (M27). `ships-cc-package` adds it,
  so `ships-player` no longer has to.

#### Authority map

- **Owner.**
  - `ship.cc` owns everything about a mod ship.
  - `ShipModel` owns its body: nodes, meshes, surfaces and images.
  - `ShipAuthoringStore.Validate` is the one judge of all three records.
  - C# owns every schema and catalog entry. Python (`ship_cc`) is a pinned writer
    of payloads that C# seeded.
- **Inputs.**
  - Package reads the bound collection's evaluated meshes, materials and images.
  - The validator reads the three records.
  - `PackageOf` reads the package's `ship.cc`.
  - `ShipModVisual` reads the `ShipModel` record.
- **Outputs.**
  - The records.
  - The prototypes' GameObjects, `Mesh` and `Texture2D`. These are cache only and
    rebuilt every boot.
- **Derived state.**
  - Bounds, the Unity-space conversion, mips and compressed textures are derived
    at load.
  - The `.blend` is the operator's source. It never decides a semantic.
- **Forbidden writers.**
  - Any GLB, glTF or PNG file in a package.
  - glTFast, or any other importer, on the mod-ship path.
  - Newtonsoft reading a package.
  - Python writing a record kind that C# has not seeded.
  - A second validator, in Python or in Unity.
  - `Compose` copying `ShipModel` into the derived catalog.
- **Shared paths.** Each of these reads one `ShipModel` through one `Validate`
  and one `ShipModVisual.Load`:
  - the add-on's Package;
  - AetherDb `validate` and `compose`;
  - the Editor preview and smoke;
  - the play smoke;
  - the built player.
- **Deletion line,** before the reader lands:
  - `_export_glb` and `MODEL_ASSET`;
  - `ModelAsset` and `MODEL_ASSET_SLOT`;
  - `ReadNodeIds` and its Newtonsoft use;
  - `Package.ModelPath` and `NodeIndices`;
  - the `GltfImport` path and the glTFast pin;
  - `ShipFixture.Glb` and `GlbJson`, and the GLB-shape tests
    (`ShipModCatalogTests.cs:107-140`).

#### Questions

**`ship-model-owner`: who owns the model schema?**
- **a. Aetheria, now,** as `aetheria.ship_model`. It is shaped after glTF so that
  a later lift into CultLib is a schema move with a migration. The lift happens
  when a second consumer needs it (follow-up `cultlib-model-schema`).
- **b. CultLib, now:** `GameCult.Geometry` gains `gamecult.geometry.model`, ships
  it in the CultLib Unity package, and Aetheria wraps it.
- **c. Extend `CultGeometryTriangleMesh`.** That changes chunk keys (M29) and
  still has no node tree or images. Not recommended.
- **Recommendation: a,** with medium confidence.
  - The guarantees the ruling names are CultCache's: typed envelopes, the schema
    catalog, slot parity across runtimes, and Python writing pinned slots. They
    hold for an Aetheria record exactly as they do for a CultLib one.
  - A CultLib model type would have to serve a general consumer. That means more
    UV sets, colours, skinning, morph targets and instancing: a glTF-sized
    surface, mirrored in every runtime. Aetheria needs about a sixth of it.
  - It would also put a CultLib cut and a Unity release in front of the release
    hulls.
- **Prior art.** No engine adopts a neutral interchange format as its runtime
  format. Each owns a binary form fitted to its loader.
  - glTF 2.0 keeps one typed accessor per attribute and one primitive per
    material.
  - Unity's `Mesh` takes one array per attribute, and `SubMeshDescriptor` ranges
    over one index buffer.
  - Godot's `ArrayMesh` keeps per-surface arrays.
  - Bevy's `Mesh` keeps an attribute map, with U16 or U32 indices.
  - Unreal splits positions from tangents and UVs into separate vertex buffers.
  - `ShipMesh` takes their common subset: one array per attribute, which
    MessagePack types element by element, and submesh ranges.
  - These are from the engines' documentation and were not re-fetched for this
    pass.

**`ship-image-encoding`: how are images stored?**
- **a. RGBA8, mip 0 only.** Unity generates mips and compresses at load
  (`Texture2D.Compress`, DXT5).
- **b. Block formats with mips, encoded at Package:** BC7 for colour, mask and
  maps, BC5 for normals.
  - Unity loads them with `LoadRawTextureData`, with no decode and no compress
    hitch.
  - This needs a native encoder in Blender's Python, supplied by Brokkr: bc7enc
    (MIT or public domain), etcpak (BSD-3) or Intel's ISPC Texture Compressor
    (MIT).
- **c. PNG bytes inside the record,** decoded by `ImageConversion`.
- **Recommendation: `raw-then-bc`.** `Format` and `Levels` go in the schema from
  day one; a for the first cut; b as the release encoding, in cut
  `ships-cc-image-bc`. Confidence is medium-high. The other options are
  `bc-now`, `raw-only` and `png`.
  - b makes the file about a third the size, so every save writes less.
  - b matches the VRAM budget the material plan already assumes (BC).
  - b removes the load-time compress, which costs hundreds of milliseconds per
    2048² image.
  - c puts a xenos codec inside a typed record. Its meaning becomes "whatever a
    PNG decoder says", not a layout every runtime reads the same way. It still
    pays the decode and the compress at load.
  - KTX2 is the prior art for b's shape: a format, a size and an index of level
    byte ranges, with block formats as ordinary formats. Unity's `Texture2D` and
    Godot's `.ctex` store the same thing.
- If she rules b first, `ships-cc-package` takes the encoder and
  `ships-cc-image-bc` disappears.

#### Cuts

| Cut | Repo | Depends on | Hands budget | What |
|---|---|---|---|---|
| `ships-cc-model` r1 | Aetheria | — | ~130k | The schema; `seed-model`; `Validate` on records; `PackageOf` reading the package; `ShipModVisual.Load` from records; glTFast removed; fixtures as records; the coordinate probe; the doc |
| `ships-cc-package` r1 | Aetheria | `ships-cc-model` | ~110k | `ship_cc.replace_model`; Package without a GLB; the image cap; Blender tests and the headless smoke; the LFS rule; sizes measured |
| `ships-cc-image-bc` r1 | Aetheria | `ships-cc-package`, `ship-image-encoding` | ~90k | BC7 and BC5 with mips at Package; `LoadRawTextureData` |

**The smallest first cut.** The smallest step that gets her authoring onto the
`.cc` alone is `ships-cc-model` followed by `ships-cc-package`, merged to master
together.
- `ships-cc-model` alone would leave the add-on writing a GLB that nothing reads.
- No package is committed (M27), so the pair breaks nothing in the repo. The
  operator's drafts go through `seed-model` on their next Package.

Spec and question drafts for admission, bounds-checked:
`F:\Projects\eureka-scratch-carry\ship-cc\` (`model.json`, `package.json`,
`questions.json`).

**Specs this changes.** The Superseded resolutions are Self's to admit.
- **`ships-hull-material` r2, superseded by r3.** r3 folds this section into
  "Hull materials as owned graphs":
  - no glTFast generator: `ShipModVisual` builds materials from `ShipSurface`;
  - `HullPaint` keeps the graph and the livery, and drops the PNG paths;
  - Bind's PNG checks become image checks in `Validate`;
  - surfaces gain the normal and emission slots.
- **`ships-addon-paint` r2, superseded by r3.** Package writes the mask and maps
  as `ShipModel` images. There is no PNG export and no `paint --mask <path>`.
- **`ships-player` r2, superseded by r3:**
  - "glTFast's shader variants in the player" and the no-`glTF/`-shader check
    are dropped;
  - the LFS rule moves to `ships-cc-package`;
  - the preload log line sums image memory from the records;
  - it depends on `ships-cc-package`.
- **`ships-addon-frame` r3, superseded by r4.** "The export leaving out `Source`"
  becomes "the model writer leaves out `Source`".
- **`ships-addon-mounts` r3, superseded by r4.** The texture cap leaves it.
- **The material-system plan,** which is not admitted:
  - `hull-bakes`' pair key hashes the mask and maps image bytes instead of PNG
    files;
  - "the GLB's slots" becomes "the model's surfaces".
- **`ships-addon-package` r2 needs no revision,** because it has landed.
  `ships-cc-package` deletes its GLB export, and `ships-cc-model` deletes its GLB
  read in `validate`.

**Order:**
- `ships-cc-model` can start now.
- `ships-addon-frame` and `ships-addon-mounts` touch `__init__.py`, as
  `ships-cc-package` does. Land the pair before those two.
- `ships-hull-material` r3 and `ships-addon-paint` r3 then build on records from
  the start.
- Follow-up `variants-python-raw-push` gains a consumer: `replace_model` is a
  raw push, like the other two.

#### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Ship model | `mod-model:<id>` in the package's `ship.cc` | Seeded by `seed-model`; rewritten whole by every Package | Package writes it and `Validate` judges it. It is never copied into the derived catalog |
| Ship image | An entry of `ShipModel.Images`, named by a surface or paint slot | Captured from Blender at Package, capped at 2048 | The operator's images; `Validate` judges size and format |
| Mod body prototype | Hull record key | Built at boot from `ShipModel` | `ShipModVisual.Load`. Cache only |

#### Rationale

**Why one record and not a record per image.**
- Separate image records would need typed refs from `ShipModel` and from
  `HullPaint`, plus an orphan check.
- `HullPaint` sits in the derived catalog and `ShipModel` does not, so a ref from
  one to the other would dangle there.
- Every Package writes all the images anyway, and the C# store decodes every
  registered record either way.
- One record has no refs to keep and nothing to orphan.

**Why Python packs the body instead of handing it to C#.** Any handoff from
Blender to AetherDb needs a transport. A temporary file in another format would
be the GLB again. Python writing slots pinned by `ShipSchemaPinTests` is the
contract the hull layout and the anchors already use, and C# stays the one judge.

**Why not keep the GLB as a cache beside the record.** A second copy of the body,
with its own reader, is a second owner. Nothing in the game needs glTF. The
loader from records is about the size of the glTFast glue it replaces.

### Ship authoring across Blender and Studio

The operator, 2026-10-03, ruling `studio-is-the-authoring-pane`: "I think
bridging the data to a place we already control the UI is the smart compromise.
Studio will need to update its schematic rendering though since we'll be
shipping the stroke paths directly now." It answers her earlier ask for "a
dedicated CultCache Studio pane in Blender", after the Eyes pass
(`F:\Projects\blender-tooling-surface-prior-art.md`) found that Python cannot
register an editor type. Later the same day she added: "Honestly, I don't love
the fact that CultCache Studio runs in Unity. We have all this UI lowering infra
(Eve, now Thing) and our best inspection tool lives in, of all things, IMGUI".
Question `ship-authoring-host` puts that to her.

Rulings also in force here: `ship-data-all-in-cc`, `ship-model-aetheria-owned` and
`thrusters-radiators-are-meshes`. Anchors are against Aetheria `origin/master`
`4895c752`, CultLib `main` `a7966142` and Eve `main` `8e270d6`, all read on
2026-10-03.

#### Body facts

**M31. Studio draws a hull's schematic as a texture under a checkbox grid, and
a mod ship has no texture.**
- `InspectableSchematicShapeDrawer` (`Assets/Scripts/Editor/CultCacheDrawers.cs:93-146`)
  loads `EquippableItemData.Schematic` as an asset GUID and draws it with
  `EditorGUI.DrawPreviewTexture` under one `GUILayout.Toggle` per cell. The
  grid's height is locked to the texture's aspect, and hardpoint cells are
  tinted.
- `ShipAuthoringStore` sets `hull.Schematic = null` for a mod hull
  (`ShipAuthoring.cs:103`). So Studio shows a mod hull's cells over nothing.
- The strokes live in `ShipAuthoring.SchematicLines` (key 4, `ShipPolyline`:
  points in Blender space, radii, opacities, colour; `ShipAuthoring.cs:19,40-48`)
  at `mod-ship:<id>`, in the same `ship.cc` as the hull at `mod-hull:<id>`.
- A drawer can see the sibling record. `CultInspector.Records` is every stored
  document in the open store, and `CultInspector.Record` is a read-only copy of
  the one being drawn (CultLib `CultCacheStudioDrawers.cs:46-49`). A drawer
  returns a new value only for the member it draws.
- A hardpoint's footprint is a plain `Shape` (`ItemData.cs:563`). It carries no
  drawer attribute, so Studio shows it with the default drawing.

**M32. Neither writer guards the file against the other, and they write
differently.**
- Python `push` pulls the whole store, replaces one envelope and rewrites the
  file. Records it did not touch are carried as raw bytes (M28). The add-on's
  `replace_layout` also checks a hash of the hull body (`ship_cc.py:155-161`).
  So a Blender write is safe record by record.
- Studio's Save is `_cache.FlushAsync()` (`CultCacheStudioWindow.cs:423-425`).
  It rewrites every record from memory. Its only refresh is a manual Reload
  button (`:94`). There is no file watch and no stale-file check, and CultCache
  keeps no file version (`CultCache.cs`: no revision or timestamp check on the
  single-file store).
- So if Studio has `ship.cc` open while Package writes the model, anchors and
  lines, its next Save silently restores the old ones.

**M33. Blender already turns a Grease Pencil object into the strokes, and Line
Art can make that object from the mesh.**
- Package captures the collection's one Grease Pencil object, evaluated by
  default, into `SchematicLines` (`__init__.py:387-391`,
  `ship_cc.capture_grease_pencil` `:198`).
- Blender's Line Art modifier makes Grease Pencil strokes from mesh feature
  lines: contour, silhouette, crease, material border, edge marks and
  intersections, seen from a camera. It is evaluated through the depsgraph like
  any modifier. So `capture_grease_pencil(..., evaluated=True)` reads its output
  unchanged.

**M34. Thing cannot carry the ship authoring surface yet.** Thing is the rename
of Eve (`Eve/docs/thing-campaign.md`). That campaign renames and publishes; it
adds no primitives.
- **Component kinds** (`Eve/docs/surface-contract-v1.md:96-106,309-333`):
  - layout: `surface`, `grid`, `panel`, `card` and similar;
  - `text`, `image.*`, `graph`, `tree` and `inspector.kv`;
  - controls: button, toggle, slider, stepper, segmented, colour, select and
    input;
  - `inventory.grid`, `inventory.item` and `inventory.drag_session`. These are
    spatial items with footprints and rotation, and drop operations whose fit is
    left to the provider.
- There is no kind for vector strokes or polylines. The browser lowering's only
  SVG is for `graph` diagrams (`eve-browser-lowering/src/index.ts:1653-1685`).
- There is no cell-painting kind. Occupancy can be drawn as a `grid` of
  `control.toggle`, which means 1,024 bindings for a 32 x 32 hull.
- **Provider side:**
  - C# `GameCult.Eve.Surface` with CultMesh `OperationBinding` and state
    bindings (CultLib `src/GameCult.Mesh/docs/getting-started/03-publish-an-eve-surface.md`);
  - `cultmesh-browser`, a WebSocket client.
- **Lowerings** (`Eve/docs/renderer-parity.md:32-41`):
  - The web reference renders fixtures and local advertisements. Its stated gap
    is "live Odin/CultMesh provider feed and command round-trip tests".
  - Flutter (Windows, Linux, Android) and iOS are screenshot parity targets on
    fixtures.
  - EveUnity has a live CultMesh provider path.
- `CultInspectorModel` is engine-free C#, but nothing projects it into an Eve
  surface. The Unity window is its only lowering.

#### The design in brief

```
Blender (add-on)                         ship.cc (one file, the only truth)          Studio (host per ship-authoring-host)
 model, materials, UVs                    HullData      mod-hull:<id>  <-- cells, hardpoint values, stats   (Studio edits)
 Bake Schematic: Line Art GP  --Package-> ShipAuthoring mod-ship:<id>  <-- anchors, SchematicLines         (Blender writes)
 mount gizmos, cell overlay   --Package-> ShipModel     mod-model:<id> <-- nodes, meshes, surfaces, images (Blender writes)
 Rasterise (proposes cells)   --replace_layout, revision-checked--> HullData.Shape
 mount helpers (paired rows)  --replace_layout--> HullData.Hardpoints (type, Transform, Position)
 overlay reads mod-hull on file change (bpy.app.timers mtime poll)          Studio reloads on file change; refuses a stale Save
```

**Who owns what.**
- **Blender owns** the body and everything spatial:
  - the model;
  - the strokes, baked from the model;
  - anchors and mount meshes;
  - the `Position` of a hardpoint that has a mount object.
- **Studio owns** the hull's semantics:
  - cell occupancy after any Rasterise;
  - each hardpoint's type, footprint, rotation, armour and firing arc;
  - internal hardpoints and stats.
- Each record has one writer, except two narrow, revision-checked Blender writes
  into `HullData`:
  - Rasterise proposes `Shape`, only when she presses it;
  - the mount helpers create and remove paired rows and write the mounted
    `Position`.

**Strokes: Line Art, then Package's existing capture.**
- `Bake Schematic` creates or refreshes a Grease Pencil object `Schematic` in
  `Generated`. It carries a Line Art modifier whose source is the render meshes,
  so `Source` and `Generated` are excluded.
- The edge types are contour, crease, material border, edge marks and
  intersection, seen from a top-down orthographic camera that the bake also
  places in `Generated`.
- Package already captures the one evaluated Grease Pencil object (M33). So the
  bake adds no write path.
- Two ways to change the result:
  - **Edge marks:** she marks a feature with Mark Freestyle Edge and rebakes.
  - **Freeze Schematic:** it applies the modifier so she can redraw strokes by
    hand. This is the Grease Pencil-assisted path, and a frozen object is marked
    `aetheria.keep`.
- Silhouette alone loses panel lines. A hand-drawn-only path costs her time on
  every hull. Line Art gives the feature lines with the hand edit as a fallback.

**Studio: strokes under the grid, footprints painted.**
- `ShipSchematicProjection` (ServerShared, engine-free) maps `SchematicLines`
  into cell space:
  - cell = Blender XY / 2 m, mirrored on both axes, plus `Shape.CenterOfMass`;
  - schematic +x is Blender −X, and schematic +y is Blender −Y.

  This is `ships-addon-frame`'s grid rule, run backwards.
- The shape drawer finds `mod-ship:<id>` in `inspector.Records` when the hull
  has no texture. It draws the projected strokes with `Handles.DrawAAPolyLine`,
  using each line's colour and radius, and unlocks the height.
- A new `InspectableFootprint` drawer paints a hardpoint's footprint as a toggle
  grid with the hull's occupied cells shown around it.
- The rules live in ServerShared, so a Thing host reuses them and replaces only
  the drawers.

**The bridge: the shared file, with a stale-write guard and a file watch.**
- Studio's window gets a `FileSystemWatcher` on the open path:
  - when the store is not dirty, a change on disk reloads it on the next editor
    tick;
  - when it is dirty, Save compares the file's SHA-256 with the one recorded at
    the last load or flush. If they differ, Save refuses and offers Reload,
    naming the file.
- The guard sits in Studio, not in CultCache. The operator's
  CultCache scope restraint (2026-10-02) keeps CultCache from growing into a database, and
  Studio is the only writer that flushes whole stores.
- The add-on polls the file's mtime from a `bpy.app.timers` callback. Blender
  forbids threads, so there is no watcher thread. On a change it rereads
  `mod-hull` for its overlay.
- **Prior art:**
  - Send2UE ships exported files and remote commands, not live state.
  - Multi-User replicates Blender datablocks for many editors at once. That is
    the wrong shape for one operator whose two tools own disjoint records.
  - CultCache's own write rules are already per record on the Python side (M32).
- A CultMesh live link earns its place only when a provider process holds the
  store, which is the Thing host. Then Blender connects through `cultmesh-py`
  and stops writing the file. Until then it adds a transport and no invariant.
  This is not a separate fork: it follows `ship-authoring-host`.

**Gizmos and the overlay in Blender.** These go in a `WorkSpaceTool` "Ship
Mounts" with a `GizmoGroup`, and replace N-panel text entry.
- **Overlay:** a `gpu` and `blf` draw handler in the 3D view, at Ship Root's
  placement:
  - the cell grid, with occupied cells filled;
  - each hardpoint's footprint, tinted by type;
  - each weapon's firing arc as a fan.

  It is read-only and replaces `ships-addon-frame` r3's generated Grid object,
  so there is nothing left to exclude from export.
- **Mount gizmos:** one per mount object (weapon empty, muzzle, thruster mesh,
  radiator mesh), coloured by role.
  - Clicking selects the object and names its hardpoint row in the tool header.
  - Dragging a weapon snaps it to cell centres. On release its row's `Position`
    is written through `replace_layout`.
  - Package re-derives every mounted `Position` from the object (the cell under
    its bounds centre). So the r3 warning becomes a derivation.
- **Studio's side:** it shows `Position` read-only on a row whose `Transform`
  names a mount object.
- **Not built:**
  - a Sprytile-style cell painter in Blender (Studio owns cells);
  - a firing-arc dial gizmo (Studio owns the arc; the overlay only shows it).
- **Deleted:** the N-panel hull editor:
  - the cell checkbox grid and the hardpoint boxes;
  - `AETHERIA_PG_cell`, `AETHERIA_PG_hardpoint` and `AETHERIA_PG_layout`;
  - Load Layout, Resize Grid, Add Hardpoint, Remove Hardpoint and Save Layout.

#### Authority map

- **Owner.**
  - `ship.cc` is the only truth.
  - `HullData` semantics belong to Studio's edits, judged by
    `ShipAuthoringStore.Validate`.
  - `ShipAuthoring` and `ShipModel` belong to Package.
  - Spatial placement belongs to Blender's objects.
- **Inputs.**
  - Blender reads its scene and the file's `mod-hull` (for the overlay and the
    revision).
  - Studio reads the whole store, and the sibling `mod-ship` for strokes.
- **Outputs.**
  - Blender writes `mod-ship`, `mod-model`, a proposed `Shape`, paired
    hardpoint rows and mounted `Position`s.
  - Studio writes `mod-hull`.
- **Derived state.**
  - The Blender overlay is display-only.
  - Studio's stroke underlay is display-only.
  - A mounted hardpoint's `Position` is derived from its object.
  - The `Schematic` Grease Pencil object is derived from the meshes unless
    frozen.
- **Forbidden writers.**
  - The N-panel layout editor, which is deleted.
  - A Studio Save over a file that changed on disk since it loaded.
  - Studio editing a mounted `Position`.
  - Blender editing a hardpoint's type, footprint, rotation, armour or arc.
  - Python computing `CenterOfMass`.
- **Shared paths.**
  - Every Blender write to `HullData` goes through `replace_layout` and its
    revision check.
  - Rasterise, the mount helpers and the gizmo release write through that one
    path.
  - Package writes the other two records.
- **Deletion line.** The N-panel layout editor is cut in `ships-addon-frame` r4,
  before any overlay or gizmo is added.

#### Questions

**`ship-authoring-host`: where does the hull editor and stroke view run?**

Context:
- Thing cannot carry it today (M34). It has no stroke kind and no cell-painting
  kind.
- No lowering has proven the live CultMesh round trip.
- Nothing projects `CultInspectorModel` into a surface.

Prior art: the Language Server Protocol and Jupyter's kernel and front-end
split. In both, one process owns the model and the editors are interchangeable
clients. This is the target shape doctrine names. Both reached it after a
working single-host editor existed.

- **a. `thin-imgui-now-thing-next`.**
  - The Studio drawers above, kept thin: every rule lives in engine-free
    ServerShared (`ShipSchematicProjection`, the footprint rule) and in
    `CultInspectorModel`.
  - The Studio disk guard.
  - A Thing host for Studio as its own campaign after the release, with the
    missing pieces named here: a stroke kind, a cell-paint kind, the browser
    live loop, and a `CultInspectorModel`-to-surface projection.
  - About 160k Hands tokens now, in two cuts. The IMGUI added is about 150
    lines of drawer.
- **b. `thing-now`.**
  - An Aetheria provider: AetherDb, which already owns `ship-authoring`, holds
    `ship.cc` and publishes a surface over CultMesh.
  - New contract kinds `schematic.strokes` and `cells.paint`, in the contract,
    the browser lowering and conformance fixtures.
  - The browser live-loop proof.
  - A `CultInspectorModel` projection limited to the field kinds `HullData`
    uses.
  - Blender connects through `cultmesh-py`.
  - About 5 cuts, roughly 600-800k Hands tokens, across Eve, CultLib and
    Aetheria. Hull authoring waits on all of it, ahead of the release.
- **c. `thing-now-minimal`.**
  - As b, but no new kinds:
    - the provider renders the strokes server-side as an `image` asset;
    - cells are a `grid` of `control.toggle`.
  - About 3 cuts, roughly 300-400k Hands tokens.
  - It leaves 1,024 bindings per hull and a stroke view that cannot zoom
    crisply. The Thing campaign would then rebuild that surface.

Recommendation: **a**, confidence medium-high.
- The release needs hull authoring now. Studio already edits every `HullData`
  field.
- The IMGUI cost is held to one drawer file, because the rules sit where a
  Thing host will read them.
- b is the right destination, but it would put three unproven pieces (two new
  kinds and the live loop) on the release's critical path.
- c builds a surface that b would then throw away.

Depends on: nothing. The Blender cuts are independent of this answer.

#### Cuts

| Spec | Repo | Depends on | Hands budget | What it does |
|---|---|---|---|---|
| `ships-studio-schematic` r1 | Aetheria | `ship-authoring-host` | ~90k | `ShipSchematicProjection`; strokes under the grid; the `InspectableFootprint` drawer; tests on the projection |
| `studio-disk-guard` r1 | CultLib | `ship-authoring-host` | ~70k | File watch, auto-reload when clean, stale-Save refusal by SHA-256, in the Studio window; a CultLib release |
| `ships-addon-schematic-bake` r1 | Aetheria | — | ~70k | Bake Schematic (Line Art and the ortho camera in `Generated`), Freeze Schematic, its own module; smoke |
| `ships-addon-frame` r4 | Aetheria | `ships-cc-package` | ~150k | r3 with the `.cc` change, the N-panel layout editor cut first, the Grid object replaced by the cell overlay, Rasterise kept |
| `ships-addon-mounts` r4 | Aetheria | `ships-addon-frame` | ~110k | r3 without the texture cap and without internal-hardpoint editing; rows are pairs only |
| `ships-addon-gizmos` r1 | Aetheria | `ships-addon-mounts` | ~120k | The Ship Mounts tool, mount gizmos, the footprint and arc overlay, mounted `Position` derived on release and at Package, Studio's read-only `Position` |

Admitted on 2026-10-03, along with question `ship-authoring-host`:
`ships-studio-schematic` r1, `ships-addon-schematic-bake` r1 and
`ships-addon-gizmos` r1. Two batches wait on Self, bounds-checked, in
`F:\Projects\eureka-scratch-carry\studio-pane\`:
- `pending-after-ships-cc-package.json`: `ships-addon-frame` r4,
  `ships-addon-mounts` r4 and their Superseded resolutions of r3. Admission
  refuses r4 until `ships-cc-package`, which it depends on, is admitted.
- `pending-after-cultlib-in-campaign.json`: `studio-disk-guard` r1. Admission
  refuses it because `GameCult/CultLib` is not a repo of the campaign. Self adds
  the repo (a campaign revision) or routes the cut to a CultLib campaign.

**Specs this changes.**
- **`ships-addon-frame` r3, superseded by r4.**
  - It folds in the `.cc` section's change: the model writer leaves out
    `Source`.
  - The Grid object becomes the overlay, and the layout editor is cut first.
- **`ships-addon-mounts` r3, superseded by r4.**
  - It folds in the `.cc` section's change: the texture cap leaves.
  - "The layout panel stays the editor" becomes "Studio is the editor".
  - Add and Remove Hardpoint for internal types leave Blender.
- **`ships-cc-model` (draft).** It should mark `ShipModel`'s bulk arrays
  `[CultInspectorHidden]` and show their counts read-only. Otherwise Studio,
  opening `ship.cc`, tries to draw millions of floats in IMGUI. Self amends the
  draft before admitting it.

**Order against the `.cc` move on 2026-10-06.**
- **Before the 6th:**
  - `ships-studio-schematic` and `studio-disk-guard`, once
    `ship-authoring-host` is ruled. They read only `HullData` and
    `ShipAuthoring.SchematicLines`, both on master.
  - `ships-addon-schematic-bake`. It lives in its own module with a one-line
    registration, so its merge with `ships-cc-package`'s `__init__.py` edits is
    trivial. It feeds Package's existing capture.
- **After `ships-cc-package` lands:** `ships-addon-frame` r4, then
  `ships-addon-mounts` r4, then `ships-addon-gizmos`. Each builds on
  `ShipModel`-in-`.cc`, not on the GLB.
- If she rules b or c, `ships-studio-schematic` and `studio-disk-guard` are
  withdrawn and the Thing cuts are mapped then. The Blender cuts do not change.

#### Rationale

**Why Studio owns semantics and Blender owns space.**
- The operator's ruling puts editing where GameCult controls the UI.
- What Blender knows better than any grid editor is where things are on the
  mesh. So placement is the one hull value Blender writes, and it is derived
  from objects, not typed.

**Why Line Art and not silhouette edges or a custom edge extractor.**
- Line Art is Blender's own feature-line generator. Its output is the Grease
  Pencil object that Package already captures.
- A Python edge walker would re-implement occlusion and chaining, and own them
  forever.

**Why the guard sits in Studio, not in CultCache.**
- Python already writes safely record by record.
- Only a whole-store flush from memory can clobber. The editor that does that
  flush owns the check.

**Rejected.**
- A Blender fork with a native editor (the Eyes pass, section 2).
- A Sprytile-style cell painter in Blender: a second editor of cells.
- A CultMesh link before a provider holds the store.

### S3: the first playable mod ship in a built player

- **First hull: Headliner.**
  - It is a single hull. Quiet comes as two variants (Dexter and Sinister), and
    MQ2 A makes them two full packages.
  - Its 50k triangles exercise the heavy path: collider generation and texture
    memory.
  - It is Lucent's duel ship (ruling `lucent-duel-bait`), so `faction-play-2` will
    want it in Lucent flights.
  - Its mesh and texture are already in `Quiet.blend`, so no new GLB is needed.
  - The operator authors it with the add-on, because placing the mounts needs her
    eyes. She commits the package with the add-on cuts' tooling.
- **`ships-player` does the agent-side work**, and does not wait on the hull:
  - the template radiator material;
  - glTFast's shader variants in the player;
  - one preload log line with hull count, shader names and texture memory;
  - LFS rules for packages (M11);
  - `GameData/Mods` staged into the player;
  - the C3 sim test;
  - the `SetEntity` probe;
  - the built-player boot smoke on Starfire, with the skiff fixture and a bad
    package.
- **What it needs to build a player** is question `s3-player-build` (M10).
- **The operator's S3 session** is gate 4, gate 5 (S4) and C2 to C6: Headliner in a
  built player, about fifteen minutes.

### S5 and the variants campaign: what this strand needs, and when

- **Nothing before release for S5 itself.**
  - The bar is 3 shipped prefab hulls plus 10 package hulls
    (`content-bar-one-per-concept`). The prefab hulls keep working through the
    `Prefab` branch.
  - Packages are complete hulls (MQ2 A), so no family needs variants.
  - S5, which migrates the shipped hulls, retires the FBX builder and collapses
    families, waits on variants C3 and C4, and stays theirs.
- **Before the operator's CultLib checkout moves past the C2a merge:**
  - Python must still write a raw record into a single-file store. `ship_cc` pushes
    `HullData` and `ShipAuthoring` envelopes (`ship_cc.py:108,163`), and `parity-py`
    rewrites that write path.
  - Follow-up `variants-python-raw-push` asks `parity-py` to keep that path or name
    its replacement, with `tools/blender/tests` as its consumer check.
  - `ships-addon-mounts` adds a guard: the add-on refuses an untested
    `cultcache_py` revision (M12).
- **When C4 brings element ids to Aetheria**, they arrive on `ShipAnchor`,
  `HardpointData` and `ShipPolyline`.
  - `ship_cc` already carries hardpoint slots past `FiringArc`, keyed by mount id.
    `ships-addon-package` does the same for anchors, keyed by anchor id.
  - Lines are replaced whole, which Q7 allows.
  - Key 32 (`HullData.Visual`) is taken, and C4 must not reuse it.
  - The lane's own S5 text puts hull families "in `Aetheria.cc`". MQ1's confirmed
    reading puts a family in its ship's `.cc`. S5's owner should resolve which one
    holds before C4 maps it.
  - This is follow-up `c4-ship-element-ids`.

### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Ship package | `GameData/Mods/<id>/`; `<id>` is `ShipAuthoring.Id`, lower-case and stable across renames. | Created by `ship-authoring create`, edited daily in Blender, validated by C#. Removing it makes Continue refuse runs that used it (MQ4). | The package's two records. The `.blend` in `Asset Sources/` is the operator's source and never the owner of semantics. |
| Hull record | `mod-hull:<id>` in the package. | Stats seeded from a reference hull at create; layout written by Blender's Save and Package. | `HullData` in the package: cells, hardpoints, stats. How it gets a manufacturer is question `release-hull-home`. |
| Anchor | `ShipAnchor.Id`, equal to the mount id for mounts and to the GLB node's `aetheria.id`. | Written whole by Package from role-tagged objects. Unknown tail slots are carried by id. | Blender objects tagged `aetheria.role` propose; the C# validator decides. |
| GLB | Retired by "Ship data all in the `.cc`": the body is `ShipModel` at `mod-model:<id>`. | Was re-exported by every Package; `ships-cc-package` deletes the export. | None. `ShipAuthoring` key 2 is retired. |
| Derived catalog | `Aetheria.modded.cc` in the persistent data path. | Recomposed every boot, and disposable. | `ShipModCatalog.Compose`. Never authored. |
| Mod prototypes | Hull record key. | Built at boot, kept across scenes, gone at exit. | `ShipModShips.Preload`. Cache only. |

### Rationale

**Why the operator checks move to the S3 session.**
- They need a mod ship in a zone, which today means the console and a docked
  player (M5). Done in the Editor with the skiff fixture, they prove the wiring on
  a tetrahedron.
- Done once in a built player with Headliner, the same fifteen minutes also give
  gates 4 and 5, which are owed anyway.
- The merge is safe without them because the path is inert with no package
  installed.

**Why mounts before the add-on.** The add-on writes anchors. If it learned
`articulation` as the weapon role, the first real package would need migrating when
the ruling's code landed. `ships-mounts` also collapses the role checks into the
validator (M4), so that the add-on's Validate button and compose refuse the same
things.

**Why the add-on calls AetherDb instead of growing a Python validator.** The doc's
shared-path claim is true only for C# (`moddable-ship-authoring.md`, correction 4).
A second validator in Python would be a second semantic authority. A subprocess
call costs a few seconds per Package and keeps one owner. The same reasoning makes
`create --like` a C# command: `HullData` has some 30 slots that Python would
otherwise copy by hand.

**Why generated structural anchors.** The collider, map icon, shield and tractor
are functions of the mesh's bounds and silhouette. Hand-making them per hull is the
work that M8's hand-scaled icosphere already shows. Generating them, with a "keep
mine" flag, removes about half of the per-hull steps without taking a decision from
her.

**Why first-party hulls through the package path** (the recommendation in
`release-hull-home`). In KSP, RimWorld, Factorio and Starsector, the base game's
content uses the same format and the same loading path as mods:
- KSP stock parts live in `GameData/Squad`, beside mod folders.
- Factorio's base game is the `base` mod, read before every other mod and merged
  into one prototype table.
- Starsector mods put their ships into an existing faction's fleets and markets with
  their own `.faction` file. The engine merges that file into the core faction's
  known-hull lists, so the package carries its own availability.
- The RimWorld claim is from memory and was not re-fetched.

Dogfooding the mod path means the release exercises the path players' mods will
take. A package that carries its own product needs no edit to `Aetheria.cc`.

Sources:
- https://forum.kerbalspaceprogram.com/topic/144858-squad-folder/
- https://forums.factorio.com/viewtopic.php?t=12564
- https://spmatlas.com/guides/mods-explained/
- https://fractalsoftworks.com/forum/index.php?topic=12970.0
- https://docs.blender.org/manual/en/latest/addons/scene_gltf2.html

## Lanes: scenarios, mining and the loot move

The operator ruled on 2026-10-03 that this campaign adopts `codex/scenarios` and
`codex/mining` from where they stand ("Adopt mining and scenarios", ruling
`adopt-mining-and-scenarios`). This section maps:
- both adoptions;
- the loot move into `ServerShared`, which jettison rests on (ruling
  `jettison-shared-floating-items`);
- the merge order of every lane and the faction-play cuts;
- the re-map of `faction-play-4` to ruling `ballistics-real-ammo`.

The cuts are typed specs in the mind: `scenarios-adopt`, `mining-index`,
`mining-index-pins`, `mining-target-queries`, `mining-merge`, `mining-index-tree`,
`mining-belt-cells`, `loot-1` to `loot-3`, `ballistic-ammo` and `faction-play-4` r2.
The forks are questions `fastblast-ammo`, `reticle-exactness` and `belt-size-tiers`.

The lane tips were read with `git show` in a scratch clone on 2026-10-03:
- `codex/scenarios` is at `aa3baf12`: 30 ahead of master and 20 behind, base `702b454b`.
- `codex/mining` is at `83d8371e`: 32 ahead and 21 behind, base `65c63495`.
- Everything master gained since either base is `Scenarios map:` doc commits.

The operator's records for both lanes live in `docs/scenarios-cut.md` on master: the
Soul passes on scenarios batches 2 to 4 (lines 72-140), and the mining Cut 3 Soul and
belt-freeze rulings (lines 830-876).

### Body facts

**L1. The scenarios blocker is real, latent, and wider than one lookup.**
- Soul's batch-4 probe P30 exists only in a dead session scratchpad
  (`...\F--Projects-CultLib\2a6aec4d-...\scratchpad\soul5\SoulScen5Probes.cs`, log
  `probesC.log`). At `aa3baf12` it returned:
  - (a) An L-shaped gun in an L-shaped hardpoint equips at origin `(1, 1)`.
    `Hardpoints[origin]` is NULL, while the hardpoint found by the item's cells is the
    right one. `ArcFor` returns the default 120 instead of the authored 360.
  - (b) A design with an empty leading column: `ItemFits(-1, 4)` is true but
    `TryFindSpace` is false, and `ArcFor` throws `IndexOutOfRangeException`.
  - (c) Over the catalog with that design, 380 placements are accepted. In 9 the origin
    is outside every hardpoint, in 7 it is inside another hardpoint (a radiator), and
    14 offsets are never tried by `TryFindSpace`.
- 18 readers take an item's hardpoint from its origin cell
  (`Hardpoints[item.Position.x, item.Position.y]`):
  - `FireControl.ArcFor` (`:38`);
  - eleven Unity weapon managers;
  - `EntityInstance.cs:245,260` and `ShipInstance.cs:70`;
  - `ActionGameManager.cs:1044,1048,1049`.

  This is a grep at `aa3baf12`. The pattern in `scenarios-adopt`'s negative check
  matches exactly these 18.
- `Entity.HardpointAt` (`Entity.cs:820-835`) already finds the right hardpoint at equip.
  `TryEquip` (`:904-935`) throws that answer away.
- The defect is latent. A catalog probe at `aa3baf12` with catalog `70a7b0a9` (an xUnit
  probe run through `ygg-verify`, job `aeth@aa3baf12e4`, 2026-10-03) found:
  - every catalog hardpoint is a full rectangle;
  - the only gear designs whose shape lacks its origin cell are the Vulcan reactor and
    the Industrial Thermostatic Heater (Tool).

  So nothing in the shipped catalog trips it today. The first non-rectangular hardpoint
  or mount would.
- The "held fix" was never written. The fix batch was queued and not dispatched under
  the 2026-10-01 drain (scenarios-cut.md:105-106). P30's harness survives only in that
  scratchpad, so `scenarios-adopt` commits its fixtures as tests.

**L2. Merge-tree probes between the lane tips** (`git merge-tree --write-tree
--name-only`, 2026-10-03):

| Pair | Conflicts |
|---|---|
| master x scenarios | none |
| master x mining | none |
| scenarios x moddable-ships | `AetheriaStores.cs` (`CatalogTypes`, line 9) |
| mining x moddable-ships | `AetheriaStores.cs`, `tools/AetherDb/Program.cs` |
| scenarios x mining | `AetheriaStores.cs`; `Zone.cs` (one region: scenarios' `MineAsteroid` against mining's deletion); `GameData/Aetheria.cc`; `FireControlCut124Tests.cs` |

- The scenarios x mining set matches the merge rule recorded in scenarios-cut.md S4
  (lines 133-140).
- Only mining changes `Settings.asset` (`347752dd` to `b2e346f5`, `MiningDifficulty`
  removed), so it merges without a conflict.

**L3. The belt freeze's cause is mapped** (mining Cut 3 Soul F1). Historical: the scan, `ChunksNear` and `ActionGameManager.TargetCandidates` were deleted by `mining-index` and `mining-target-queries`; see "Mining index: as built".
- Every reticle, next or previous press builds `ActionGameManager.TargetCandidates`
  (`:1174` at `83d8371e`).
- That calls `Entity.VisibleChunksInReach` (`Entity.cs:377-385`), which calls
  `Zone.ChunksNear` (`Zone.cs:326-339`).
- `ChunksNear` loops over every rock of every belt whose annulus reaches, posing each
  rock and then testing its visibility.
- The handlers then sort the whole list by distance and search it with
  `Array.IndexOf` (`:393-407`).
- Soul measured 63 ms at 30k rocks, 449 ms at 300k and 4.3 s at 3M. At 3M, 633k rocks
  are in reach and 2,240 are visible.

**L4. The operator's index ruling, and the visibility bound.**
- The operator, verbatim: "Just don't specialize the indexing too much towards asteroid
  belts, because we'll want a bunch of subsystems feeding targeting data"
  (scenarios-cut.md:871-875).
- Detection of a rock is the settled sensor rule (Q14 A):
  `ChunkInfo = saturate(sum_i v * s_i * curve_i(angle) / d / (n k))`. The sources are
  `Sensor.Gain` (`Sensor.cs:187-199`) and `Entity.ChunkInfo` (`Entity.cs:355-368`).
- The rule is linear in visibility `v` and falls as `1/d`. A Bezier curve lies inside
  the hull of its control points. So for a region with a known maximum visibility and
  a known nearest distance, an upper bound on `ChunkInfo` is cheap to compute.
- Rock visibility is `CrossSection * pi * cells^2 * light` (`Zone.ChunkVisibility`,
  `:314-321`).
- Together these let a provider prune whole regions without deciding detection.

**L5. Hands' unbuilt work in progress** (superseded by `Targeting/` on master; the patch was not adopted) is
`F:\Projects\HANDOFF-mining-index-wip-2026-10-01.patch`: 257 lines against
`83d8371e`'s `Zone.cs`.
- It groups rocks into bands of 256 by orbit distance, each sorted by orbital phase at
  a key time.
- It widens the query arc by the band's rate spread, re-keys at most 8 bands per query,
  bounds the light per band, and counts `ChunksExamined`.
- Its structure is belt-shaped (`ChunksNear(position, range, distancePerVisibility)` on
  `Zone`), and its queries still build eager lists.
- Kept: the kinetic bands. Not kept: the belt-shaped API and the eager queries.

**L6. Nothing of the loot move has landed, on master or on any lane.**
- `git grep` for `Zone.Loot`, `TryPickUp` and `LootDrop` over every local and remote
  ref found nothing in code.
- On master `f1dee184`, the loot path is all Unity:
  - the roll uses `UnityEngine.Random.value` and `onUnitSphere`
    (`EntityInstance.cs:302-321`);
  - pickups are `GridObject`s, moved by Unity (`GridObject.cs`);
  - collection is `ShieldManager.OnCollisionEnter` (`ShieldManager.cs:40-55`);
  - the tractor beam pulls by `Physics.SphereCastAll` (`TractorBeam.cs:14-30`).
- No lane touches these hunks. Mining edits `EntityInstance.cs:236,410` and
  `ZoneRenderer.cs:72-483`, away from the loot lines. Moddable-ships edits
  `ZoneRenderer.cs:289-304`.

**L7. Today's loot tuning, read from the assets.**
- `GameSettings.cs:14-16`: `PickupLifetime` 30, `LootDropProbability` .25,
  `LootDropVelocity` 25.
- All five pickup prefabs (`Assets/Prefabs/RPG/Pickups/*.prefab`) carry the same
  `GridObject` values: `Drag` .05, `LaunchDrag` .2, `Gravity` 1, `GridAttraction` 10,
  `RotationSpeed` 1.
- `Tractor Beam.prefab`: `Radius` 25, `Traction` 25, `Distance` 75.
- `GridObject` also moves mines (`Mine.cs:12`, `MineManager.cs:21-32`).

**L8. Fork L already decides the pickup, and the Tractor Beam has no capability yet.**
- Fork L was ruled by the operator on 2026-09-17 (headless-playground-cut.md:924-970):
  - pickup is a simulation-owned timed grab: extend, envelop, pull;
  - no Unity collision decides a pickup;
  - the tractor beam is a generic Tool item that provides only pickup;
  - the simulation owns the grabbed object and its pose;
  - `GrabEvent` names the object by a typed simulation identity;
  - loot is a body plus an item instance.
- Two readings are therefore not the design:
  - option (a) of that section, "contact detection stays Unity collision", which was
    recommended before the ruling;
  - the brief's "`Zone.TryPickUp` from `ShieldManager`".

  The jettison ruling's own words agree: jettisoned cargo "can be picked up without
  unity's involvement".
- Scenarios rulings of 2026-09-30 (scenarios-cut.md:46-56): picking up requires a
  pickup behaviour, which the Tractor Beam carries, and every generated ship and the
  player's starting ship carry one.
- The catalog probe (L1's job) found the Tractor Beam as `GearData` on a **Sensors**
  hardpoint, with **no behaviours** and one product. Nothing in the simulation can grab
  today.
- The presentation seam exists, but grabs nothing real:
  - `GrabEvent` carries an `int TargetHandle` (`CapabilityEvents.cs:53-67`);
  - `CapabilityPresentationBinder` resolves it to a live `Transform` through delegates
    (`:16-45`), and only `FieldTester` supplies them.

**L9. The ammunition substrate, by catalog probe** (same job, catalog `70a7b0a9`):

| Hardpoint | Weapon | Behaviour | Magazine | Ammo | Energy |
|---|---|---|---|---|---|
| Ballistic | 6k Shooter | Instant | 6 | none | 5 |
| Ballistic | Autocannon | Auto | 50 | Ammo | 5 |
| Ballistic | ClearPath | Auto | 0 | none | 1..0.5 |
| Ballistic | DeathCluster | Auto | 8 | Ammo | 25..5 |
| Ballistic | Earp | Instant | 6 | none | 5 |
| Ballistic | pretty pretty bang bang | Charged | 8 | Ammo | 25..50 |
| Energy | FastBlast+- | Auto | 12 | Ammo | 0 |
| Energy | ChargeBlast SG, ChargeBlast+-, ColdFire, CShot RainbowLite Lazer, plight, Spectra | mixed | 0 or 1 | none | 0 to 150 |
| Launcher | GT 3K, LRMM72, pswarm, scorched void policy, SRMM72 | mixed | 2 to 288 | none | 0.1 to 50 |

- The catalog has one ammunition commodity, `Ammo`, at price 1000.
- One cargo unit is one **magazine**. `InstantWeapon.UseAmmo` removes 1 unit per reload
  and refills `MagazineSize` rounds (`InstantWeapon.cs:176-202`). `ConstantWeapon` does
  the same (`:129-160`). So `faction-play-4` r1's "`AmmoMagazines x MagazineSize`
  rounds" over-stocked by a factor of `MagazineSize`.
- `UseAmmo` returns true at once when `MagazineSize <= 1` (`:178`), so a one-round gun
  fires free even with an `AmmoType`.
- Firing energy reaches a weapon through the power bus:
  - `PowerBus.cs:122` takes every active `IPowerConsumer`;
  - an item's `PowerSupply` is its tier's grant ratio (`:196-205`), which is 0 below a
    starved tier;
  - so a weapon that requests nothing still reads its tier's ratio, and a brownout
    still reaches it.

  That is why `ballistic-ammo` takes ammo-fed weapons off the bus instead of zeroing
  their request.
- `ChargedWeapon`'s charge is time-based (`_charge += dt / ChargeTime`). Only firing
  spends the capacitor, so bypassing the spend does not stall a charge.

### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Equipped item's hardpoint | `EquippedItem.Hardpoint`, a reference into the hull's `Hardpoints` list; null for Tool gear. | Set once at equip from the placement rule, and derived again on every load (load re-equips). Never stored. | `Entity.HardpointAt`, through `TryEquip`. No reader derives it from the origin cell. |
| Targeting index | `Zone.Targets`, one per zone. | Built with the zone. Providers register as belts are built and entities admitted. Rebuilt on load; never saved. | `TargetingIndex` for candidate sets and bounds; each provider for its own motion; `Entity` for detection. |
| Belt band (provider region) | Belt key plus band index; arc segments by key order. | Derived from `AsteroidBeltData`, `PlanetSettings` and time. Re-keyed lazily, a bounded number per query. Cache only. `mining-belt-cells` replaces it with rings and cells. | `BeltTargets`. |
| Belt ring and cell (after `mining-belt-cells`) | Belt key, ring index, cell index. A ring is a run of rocks by orbit distance; its cells are equal sectors of a frame turning at the ring's mean rate. | Derived when the belt is built, never saved. A ring is refreshed (its rocks re-bucketed) when its drift passes one cell, within a rock budget per query. | `BeltTargets`. |
| Target choice | The `TargetRef` a key press writes. | Made per press; no list is kept between presses. | `Entity` (reticle, next, previous, nearest), through `SetTarget`. |
| Floating item | `FloatingItemId`: a zone counter, never reused in a zone. | Released at death (and later by jettison); drifts; held during a grab; removed into cargo by `TryPickUp`, or at `FloatingItemLifetime`. Not saved: gone when the zone unloads, as Unity loot is today. | `Zone`: release, step, expiry and commit. |
| Loot dice | Zone `CombatSeed`, loot stream, death ordinal. | One generator per death, discarded after the roll. | `Zone.DropLoot`, on `SimulationDice`. Never `UnityEngine.Random`, never a shared stream. |
| Grab | Holder entity plus `FloatingItemId`. | Started, Extend, Envelop, Pull, then Completed or Cancelled. One holder per item, one grab per Pickup. | The `Pickup` behaviour owns the timeline; `Zone` owns the held pose and the commit. |
| Pickup capability | `PickupData` (BehaviorData union key 40) on the Tractor Beam design. | Authored by an AetherDb one-shot, then edited in the catalog. Every generated ship carries one. | The catalog author. |
| Ammunition | An `AmmoType` commodity; one unit is one magazine. | Stocked at generation and in station inventories; consumed one unit per reload; moved by `TryTransferItems` when rearming. | Cargo. `WeaponData.DrawsRounds`, derived from `AmmoType`, decides reactor or magazine. |

### Merge order

Each lane becomes a `eureka/aetheria-release-*` branch; nothing is pushed to
`codex/*`. In order of landing on master:

1. **`ships-merge`.** Its spec exists; ruling `ships-merge-on-agent-proof`.
2. **`scenarios-adopt`.**
   - Master (with ships) is merged into scenarios first. The only conflict is
     `AetheriaStores.cs:9`. The blocker fix follows.
   - Scenarios goes before mining for two reasons: its remaining work is one small cut,
     and mining's merge rule was written to resolve scenarios' side.
3. **`mining-index`, then `mining-index-pins`, then `mining-target-queries`**, on the
   mining branch. They touch only mining's own code, so they run in parallel with
   steps 1 and 2. The pins land first, so the same tests guard `Best` when
   `mining-target-queries` gives it callers.
4. **`mining-merge`.** Master (with ships and scenarios) is merged into mining under the
   recorded rule, and the field kinds are applied again to scenarios' catalog.
5. **`feedback-1` and `audio-1`** may land at any time: their hunks are disjoint from
   all three lanes (B10). `feedback-1` must land before `loot-2`.
6. **`loot-1` to `loot-3`**, on one branch from master after step 4, merged together.
   They wait for step 4 because mining's belt code rewrites `Zone.cs`, and the loot
   cuts edit `Zone`'s constructor and `Update`.
7. **`ballistic-ammo`**, after step 4: it needs scenarios' `LoadoutGenerator` and
   mining's `Weapon.cs`. It is independent of the loot cuts.
8. **`faction-play-1` to `-3`**, re-anchored first (follow-up `faction-play-reanchor`).
   `faction-play-1` rewrites lines both lanes changed, so it starts only after step 4:
   - mining's targeting lines: `Minion.cs:14-21`, `Combat.cs:28`, and `Target` as a
     `TargetRef` written through `SetTarget`;
   - scenarios' admission lines: `Zone.Admit`.
9. **`faction-play-4`** r2, after `faction-play-3` and `ballistic-ammo`.
10. **Jettison, then pirate collection**, on `Zone.Release` and the grab, after step 6.
11. **`mining-index-tree`, then `mining-belt-cells`**, at any time after step 4, on a
    branch from master. They gate nothing, neither the merge nor the release (ruling
    `targeting-sublinear-wanted-not-critical`). They inherit the tests of
    `mining-index-pins` and `mining-target-queries`, and tighten the cost pins.

### Mining index: as built (master `df7c44f2`)

Status, read this before L3 to L5 and the cuts above. They are the plan and the pre-cut
Body. `eureka/aetheria-release-mining` merged to master as `df7c44f2`. Landed: `mining-index`
(`cut-mining-index.h1`), `mining-index-pins` (`cut-mining-index-pins.h1`),
`mining-target-queries` (`cut-mining-target-queries.h1` and `.h2`) and `mining-merge`
(`cut-mining-merge.h1`). Not landed: `mining-index-tree` and `mining-belt-cells`. L3's
scan, L5's patch and the belt task machine no longer exist.

**Owner, by question.**
- *Which things can lie in a region, and how visible at most:* `TargetingIndex`
  (`ServerShared/Targeting/TargetingIndex.cs`), one per zone as `Zone.Targets`. It names
  no belt, rock or chunk. It owns region ordering and reach.
- *Where rocks are and which regions bound them:* `BeltTargets`, one provider per belt,
  registered in `Zone`'s constructor beside `EntityTargets`.
- *Is this target detected, and which one does a press pick:* `Entity`
  (`ChunkVisible`, `VisibleEntities`, `TargetUnderReticle`, `TargetNext`,
  `TargetPrevious`, `TargetNearestEnemy`), through `PickTarget` and `Eligible`.
- *Who writes the choice:* `Entity.SetTarget`, the one writer.
- *Rock wear and respawn:* `Zone`'s wear store (`ChunkWear`, `BrokenUntil`). Belts have
  no task, timer list or update thread: `EvaluateBelt` computes poses on demand for
  rendering, and `BeltTargets` derives bounds from time.

**Inputs and outputs.**
- `TargetSearch` in: searcher position, reach, optional observer, and
  `ReachPerVisibility` (`Entity.DetectionReachPerVisibility`, from sensor gain bounds and
  the curve's hull).
- Providers out: `TargetRegion`s (distance interval, bearing interval, visibility
  ceiling) and `TargetCandidate`s (a `TargetRef` and a planar position).
- Index out: `Within` returns every candidate within exact reach whose region can be
  detected, unordered. `Best` takes an `ITargetKey` (`Bound`, `Key`, `Precedes`) and
  returns the least-key eligible candidate: regions are sorted by bound and opened until
  a bound exceeds the best key.
- `Entity` presses: `AngleKey` (reticle: least planar angle to the look direction) and
  `DistanceKey` (next and previous: the successor or predecessor in (distance, target)
  order, wrapping, with the held target as pivot). Ties order a ship before a rock,
  then by chunk field and index.
- `Entity.VisibleChunksInReach` keeps its contract (visible chunks within mining reach,
  by index then field) on `Within`.

**Derived state, never saved.** The index and every provider's bands, keys and `Rekeys`
count are derived from `AsteroidBeltData`, `PlanetSettings` and zone time and rebuilt
with the zone. Bands re-key lazily, at most 8 per query. `Examined` is a diagnostic
counter, not state. No candidate list survives a press. The only persisted belt state is
`ZonePack` key 6 (`ChunkWear`).

**Forbidden writers.**
- Unity code picks no target. `ActionGameManager` binds four input actions to the four
  `Entity` methods and does nothing else (`ActionGameManager.cs:389-392`). It builds no
  candidate list, sorts nothing and calls neither `Within` nor `Best`.
- Nothing enumerates a belt's rocks to answer a targeting question. The only loop over
  a belt's rocks outside `BeltTargets` is `Zone.EvaluateBelt` (rendering).
- The index decides reach and nothing about detection. A provider's bounds must hold for
  every thing in the region; a provider that loosens them costs speed, one that tightens
  them past a real rock drops targets.
- Only `SetTarget` writes `Target`; presses return false and keep the target when no
  candidate exists.
- No `UnityEngine.Random` or shared stream in target choice (it has none).

**Shared paths.** Reticle, next, previous and nearest-enemy presses; `VisibleChunksInReach`;
fire control's use of `Target` (`TargetRef`); the AI's `Minion` and `Combat` target
writers (F6, still `SetTarget`); load (`Zone` rebuilds providers).

**Verification layer.** `tests/Aetheria.Shared.Tests/MiningIndexTests.cs`: index
equals brute force (`WithinIsExactlyTheBruteForceSet`, `BestIsTheBruteForceNearest`),
region bounds hold per rock (`BeltRegionBoundsHoldForEveryRock`,
`ACurveStaysWithinItsHullRange`), no visible rock dropped, cost pins (`Examined`,
`DarkBandsAreSkippedWhole`, `ReKeyingIsBoundedPerQuery`, `RegionsStayNearTheSearchArc`,
`VisibilityBoundPrunesADimBelt`) and `ARockExactlyAtReachIsFound`.
`MiningTargetQueriesTests.cs`: press rules against a brute-force order, perception
(`ShipsTheObserverHasNotDetectedAreNeverPicked`), tie orders, empty presses, and
per-press cost ceilings (`AKeyPressDoesNotGrowWithTheBelt`,
`PressPruningKeepsEachPressNearItsMeasuredCost`). Both are partial `MiningCut3Tests`
and compile only `ServerShared` (B11). The Unity side was compiled separately: a
Unity 6000.3.24f1 batchmode compile at `a83aa54e` on 2026-10-06 (`mining-merge-unity`)
exited 0 with no `error CS` in any of the five assemblies. Both ships smokes passed, and it
needed no fix commit, so the cut has no report. Stryker and `tools/mutation-table.sh` covered `Entity.cs` spans fully; survivors in
`BeltTargets` and `TargetingIndex` are equivalent or float boundaries.

**Live seams and debt.**
- `EntityTargets` is registered on every zone but no production query reaches it with
  an observer: `Entity.PickTarget` offers `VisibleEntities` itself, and `MiningSearch`
  carries no observer. The index owns no ship answer today. Finding
  `orphaned-index-surfaces`, deferred to `mining-index-tree`.
- `Entity.VisibleChunksInReach` has tests as its only callers; `Within` has no
  production caller.
- A press still costs O(rocks in a fixed belt): `Best` enumerates and sorts the flat
  region set. Deferred to `mining-index-tree` (hierarchy, heap, `Accepts` split) and
  `mining-belt-cells` (rings and cells); findings `cut-mining-index-tree.r1` and
  `cut-mining-belt-cells.r1` carry them. Neither gates the release.
- The index comment headers (`TargetingIndex.cs:10`, `Zone.cs:26`, both test files)
  cite `docs/aetheria-release-map.md`, which exists only on
  `eureka/aetheria-release-target`, not on master.

### Mining index: rings, cells and best-first search

**Why the press still grows.** At `a4e8e147`, after `mining-target-queries`, a press is
one `Best` query. Its cost has two linear terms in the rock count of a fixed belt:
- **Region enumeration.** `Best` asks every provider for its whole flat region set,
  bounds each region and sorts the set, before opening one. A band holds a fixed 256
  rocks, so at 3M rocks a band is 0.05 units wide and a full ring. A 400-unit search
  crosses about 8,000 of them, and each contributes its arc segments: 30k-64k regions,
  38-61 ms.
- **Sliver regions.** A segment of 32 rocks in such a band is a 0.05 x 470-unit
  sliver. A sliver near the searcher has a small bound but holds rocks spread along
  hundreds of units, so best-first opens many slivers for few useful rocks.

**The floor.** The press queries are exact: the smallest angle to the look direction
(reticle), and the successor in (distance, target) order (next, previous). In linear
space these are thin-cone and thin-ring range queries. Any tree of cells must open
every cell that the cone's ray or the ring's circle crosses: about the boundary's
length over the cell size. That is O(sqrt(n)) for n rocks in reach, the kd-tree
line-query bound (Lee and Wong 1977), and no linear-space structure beats it for
simplex ranges (Chazelle 1989). The target is therefore sqrt(n), not flat. A flat
cost needs an approximate answer (question `reticle-exactness`).

**Target growth.** In a fixed belt, regions opened plus rocks examined per press grow
as the square root of the rock count: at most 4.5x per tenfold rocks (sqrt(10) is
3.16), against 10x at `a4e8e147`. Visibility tests per press (`ChunkVisible`, the
expensive part) stay within 2x per tenfold. Building stays O(n) per belt, and a
refresh costs O(ring).

**Proof by measurement.** Soul's F1 fixture: rocks 300..900 about the sun, light radius
1000, kind cross-section 5, sensor Eye, guns 150 and 400.
- Run it at 30k, 300k and 3M rocks, with the eye at chunk 0 + (5, 5), at (310, 0) and
  at (600, 0).
- Presses: five `TargetNext` from no target, one `TargetPrevious` from no target, and
  `TargetUnderReticle` looking along the belt and across it.
- Record `Opened`, `Examined`, `Tested` and time. Repeat after +300 s (stale rings)
  and after +1e7 s.
- The suite pins 30k against 300k. The 3M point is a Soul probe, too slow for the
  suite.
- Expected at 3M, with 16-rock cells about 3.5 units across: low thousands examined
  and hundreds of regions opened per press. Today it is 37k-108k examined and 30k-64k
  regions.

**The design.** The index owns search order. The belt owns rock motion and cell bounds.
- **`mining-index-tree` (index only).** The provider seam becomes a hierarchy, `Roots`
  and `Open`. Opening a region yields child regions, candidates, or both.
  - `Best` keeps a binary heap of regions by their bound (netstandard2.1 has no
    `PriorityQueue`). It stops when the next bound exceeds the best key. This is
    Hjaltason and Samet's incremental distance browsing, now over a tree.
  - `Within` walks the tree depth-first, pruning with `CanHold`.
  - `ITargetKey` splits into a cheap geometric `Key` and an expensive `Accepts`
    (detection). `Accepts` runs only for a candidate that would beat the best so far.
  - New diagnostics beside `Examined`: `Opened` (regions opened) and `Tested`
    (`Accepts` calls).
  - `BeltTargets` and `EntityTargets` are adapted mechanically: today's flat regions
    become roots that open into candidates. Answers and costs do not change.
- **`mining-belt-cells` (belt only).** `BeltTargets` is rebuilt as rings of square
  cells.
  - Rocks sorted by orbit distance are cut into rings. A ring closes once its width
    reaches its cell arc (2 pi r x 16 / count), so cells are about square at any
    density. A ring also closes at a cap of 8,192 rocks, which bounds one refresh.
  - Each ring turns at its mean rate. Its cells are equal sectors of that turning
    frame, about 16 rocks each, held as one index array with cell offsets.
  - A rock drifts against its frame by at most the ring's rate half-spread times the
    time since the ring was refreshed. That drift widens every cell's sector, so the
    bounds stay sound without touching a rock.
  - When the drift passes one cell, the ring is refreshed: every rock is re-bucketed
    by its current turn in one O(ring) counting pass, with no sort.
  - A refresh happens only when a ring node is opened, before any of its cells exist
    in the query, so a query never sees a rock twice.
  - Each query refreshes at most 16,384 rocks. A ring left stale is searched through
    its wider sectors, which stays correct.
  - The tree is implicit: a balanced range tree over ring indices (annulus bounds),
    whose ring leaves open into halving cell ranges (sector bounds), down to single
    cells. Today's `Sector` bounds and light bound carry over.
- Drift in cell units grows at about pi r |d rate / d r| per second, whatever the
  density.
  - With period = distance, a ring goes stale every r / pi seconds: about 190 s at
    r = 600.
  - Refresh work per unit time is O(n) per belt, amortised over the presses that touch
    it.

**Prior art**, cited from memory and not re-fetched:
- Hjaltason and Samet, "Distance browsing in spatial databases" (ACM TODS 1999): the
  heap-ordered search that `Best` becomes.
- Šaltenis, Jensen, Leutenegger and Lopez, "Indexing the positions of continuously
  moving objects" (SIGMOD 2000), the TPR-tree. Its bounds widen with elapsed time by a
  velocity bound and are tightened on update. Here that is the drift-widened sector
  and the refresh.
- Basch, Guibas and Hershberger, "Data structures for mobile data" (SODA 1997). An
  event-driven kinetic order is rejected: every pair of rocks with different rates
  eventually swaps, so events grow as the square of a ring's size.
- Arya, Mount, Netanyahu, Silverman and Wu, "An optimal algorithm for approximate
  nearest neighbor searching in fixed dimensions" (JACM 1998). A cost flat in density
  is reachable only for an approximate answer (question `reticle-exactness`).
- Space games: EVE's belts are static, and Elite Dangerous generates ring rocks per
  cell around the player. Neither simulates per-rock orbital shear, so they bear on
  the visual budget, not on this index.

**Forks.**
- `reticle-exactness`: keep the reticle exact at sqrt(n), or accept an angle tolerance
  for a flat cost. Next and previous stay exact either way, because cycling must visit
  every target once.
- `belt-size-tiers`: give each size class its own provider, so small rocks are pruned
  at their shorter detection reach. This is a constant factor on `Tested`, not a
  growth fix.

### Rationale

**Why every item carries its hardpoint, rather than keeping the origin inside it.**
Soul named both fixes.
- Keeping the origin inside the hardpoint forbids legal placements. An L-shaped gun
  could never fill an L-shaped hardpoint whose corner is empty, because the gun's own
  origin cell is that corner (P30 (a)).
- Looking the hardpoint up by the item's cells at every reader would put the placement
  rule in 18 places.

The placement rule already knows the answer at equip. Keeping that answer deletes 18
re-derivations; it adds nothing.

**Why the index is generic and the belt is a provider.**
- The operator ruled it (L4).
- The seam is the smallest that carries both providers in the tree today and the ones
  the operator named, EW and microfauna. A provider yields regions with a distance
  interval, a bearing interval and a visibility ceiling, and the candidates for a
  region.
- The index owns no motion model. A provider for orbiting rocks, one for moving ships
  and one for a slime field each keep their own.
- Prior art, cited from memory and not re-fetched:
  - best-first incremental search over a region hierarchy: Hjaltason and Samet,
    "Distance browsing in spatial databases" (ACM TODS 1999);
  - kinetic sorted orders for moving points: Basch, Guibas and Hershberger, "Data
    structures for mobile data" (SODA 1997).

**Why the index and the key-press rules are two cuts.**
- The index alone removes the 633k-rock scan, but leaves the handlers sorting every
  visible rock (2,240 at 3M) on each press.
- The operator's rule is that a press must not grow with belt size. Only best-first
  answers meet it. After Soul's first pass on the index, the operator accepted the
  remaining growth with rock density (see below).
- Together the two are about 900 lines with tests, over one Hands budget. The seam
  between them, the index's query API, is where Soul can falsify each.

**Why the index fix before the merge is only pins, and the cells come after.**
- Soul (verdict `cut-mining-index.s1`) measured the index at `a4e8e147`. It is correct,
  and the 3M-rock press fell from 4.3 s to 150-600 ms.
- Press cost still grows linearly with rock count in a fixed belt (finding
  `f1-press-linear-in-density`).
- The operator ruled that this is not critical, but that the improvement is wanted
  (ruling `targeting-sublinear-wanted-not-critical`, which supersedes
  `targeting-density-scaling-accepted`): "O(sqrt(n)) is way better than O(n), my
  ruling says it's not critical, not that it's undesired".
- So the merge waits only for `mining-index-pins`: three tests for findings
  `cost-loosening-unpinned` and `exact-reach-unpinned`, which the suite could not see.
  - The share of regions past reach catches a widened arc window or a loosened
    `Nearest`.
  - Examined against an exact per-rock bound catches a loosened visibility bound.
  - A rock exactly at reach, searched from beyond its belt's outer edge too, catches
    a slack sign slip.
- `mining-target-queries` r1 stands as specced. Its `AKeyPressDoesNotGrowWithTheBelt`
  scales area at constant density, which both rulings leave in force.
- The sublinear structure is the section "Mining index: rings, cells and best-first
  search" above, as two cuts after `mining-merge`.

**Why loot is three cuts on one branch, merged together.**
- The ruled design (L8) needs three things:
  - floating items in the simulation (`loot-1`);
  - the grab as a capability, with its catalog entry and generation (`loot-2`);
  - the presenters (`loot-3`).
- Together they are about 1,000 lines.
- Merging after `loot-1` alone would ship a game where loot cannot be collected,
  because `loot-1` deletes the Unity collection that the ruling retires.

One branch keeps master playable, and three cuts keep each Soul pass small.

**Why floating items are not saved.** Today's Unity loot is not saved either, and a
floating item lives 30 seconds. Saving them would add a `ZonePack` field and a
migration for a window shorter than a save cycle. Jettison may want longer lifetimes;
its cut can revisit this with a reason.

**Why the loot dice are the zone's, keyed by death ordinal.**
- Fire control's Cut 6b rule is that a roll belongs to the thing rolled for, not to a
  shared stream, so a UI or another system drawing first cannot change it.
- The loot roll follows the same rule with its own stream, so it cannot disturb shot
  dice either (test `LootDiceDoNotTouchShotDice`).
- The mixing function moves to one owner (`SimulationDice`) instead of being copied.
  The owner-level fix is CultMath's (follow-up `cultmath-mixed-seed`).

**Why ammo-fed weapons leave the power bus.** By L9, a weapon on the bus reads its
tier's grant ratio even when it requests nothing. A request of zero would still let a
brownout silence a gun whose energy comes from its rounds. Off the bus, its supply is
1 and the reactor cannot touch it. That makes the ruling's trade-off literal: energy
weapons compete for reactor power, and ballistic weapons compete for cargo.

**Why one ammunition commodity.**
- The catalog has one (`Ammo`).
- The mechanism is data (`AmmoType` per weapon), so a split by calibre is a catalog edit
  that needs no code.
- A tender carrying one commodity is also the simplest rearm loop to prove.

This is a default, not a ruling. The operator can ask for calibres at any time.

**Why mining's Q6a should be asked again.** Mining ruled that ore is lost when a hold
is full (Q6a A), because pickups were Unity-only (mining-cut.md:286-287). After
`loot-1`, overflow could float through `Zone.Release` like any other loot. Follow-up
`mining-cuts-4-5-7` carries the question to that map.

## Controls

The control rebuild of ruling `facing-separate-from-aim`, which ruling
`demo-controls-rebuild` puts before the demo. The pilot steers the hull's facing with
its own input, the cursor aims, fixed mounts fire when the aim is inside their arc and
turrets track. Articulated mounts (follow-up `articulated-mounts`, ruling
`arc-and-traverse-on-the-link`) are full game; this section keeps every arc read behind
the seam they will plug into. Anchors are against `origin/master` `228f241e`, read in a
detached scratch worktree on 2026-10-06.

### Prior art

Sources are marked: **primary** (the game's own source, manual or developer post, read
in full), **wiki** (a community wiki page read in full), **snippet** (a search-result
summary only, not read at source; treat as unverified).

- **Starsector** (wiki: the fandom `Piloting` page, read through the MediaWiki API; the
  Fractal Softworks manual PDF could not be fetched). WASD is ship-relative: A and D
  turn, Ctrl-A and Ctrl-D strafe. Holding Shift makes the ship "automatically point
  towards the mouse"; a setting makes that the default. The mouse aims the selected
  weapon group, which tracks the cursor and fires on the left button. Shift+1-5 puts a
  group on autofire: those weapons pick and attack their own targets, and the HUD marks
  the group with a filled square. R designates the ship under the mouse, and weapons
  "preferentially fire at the target". The wiki's own advice is to put everything except
  missiles on autofire so that the mouse governs only shields. That advice is the
  community's answer to the core failure of a cursor-aimed, arc-limited model: holding
  the cursor inside a narrow arc while manoeuvring is hard (snippet, forum). Vanilla
  draws weapon arcs, and a popular mod redraws them (snippet).
- **Endless Sky** (primary: `source/Preferences.cpp` and `PreferencesPanel.cpp` on
  `master`). Fixed guns fire along the hull; turrets track. Player options: "Automatic
  aiming" off / always on / when firing (default when firing), "Automatic firing" off /
  on / guns only / turrets only (default off), "Aim turrets with mouse", "Turrets focus
  fire" (default on), "Control ship with mouse", and "Turret overlays" off / always on /
  blindspots only (default blindspots only). So the arc display defaults to showing only
  where the guns cannot reach.
- **Escape Velocity Nova** (primary: the Nova Bible, weapon `Guidance` field). Keyboard
  turning; fixed guns fire ahead. Guidance 7 is a "front-quadrant turret" that can fire
  plus or minus 45 degrees off the nose and "fires straight ahead if no target";
  guidance 8 is the rear quadrant. A designated target pulls a limited-arc turret's
  rounds onto it, and without one the mount fires down its axis. This is the closest
  precedent for Aetheria's designation-based fire control.
- **Cosmoteer** (primary: developer blog, "Cosmoteer 0.14.11 - Arcade Style Ship
  Controls"). Direct Control: W and S thrust, A and D rotate, Q and E strafe; the mouse
  aims and fires. Turreted weapons fire on the left button and fixed weapons on the right
  by default, rebindable per weapon. Rotating to face the cursor is an opt-in setting.
- **Nebulous: Fleet Command** (snippet). Weapons engage designated targets on their own
  within their arcs; ships auto-orient to bring mounts to bear. Its known failures are
  arc failures: narrow-arc guns idle while the ship searches for an angle that brings
  every turret to bear, and turrets report "target masked by hull".
- **Battlestar Galactica Deadlock** (snippet). Broadside, bow and stern batteries have
  about 90-degree arcs; dorsal and ventral guns cover 360 degrees with limited elevation.
  Arcs show when a turret is clicked in the radial menu, and a key toggles them on the
  selected ship. Players asked for more visible arcs (a Steam thread title).
- **SubSpace and Continuum** (snippet). Arrow keys rotate, up thrusts, guns fire along
  the nose: the pure nose-aim model this ruling retires.
- **World of Warships** (snippet), added because Aetheria's camera is a chase camera, not
  top-down (body fact C7). The mouse orbits the camera and aims; the keyboard is the helm.
  Guns traverse toward the aim at a turret traverse speed. The classic complaint is "the
  guns won't fire though the view looks on target": the turret has not finished
  traversing or cannot bear, and the reticle's gun markers carry that state.

**Failure modes to design against.**
1. "My guns don't fire": the aim or target is outside a mount's arc, or a turret has not
   traversed yet, and nothing on screen says so (Starsector forum, Nebulous, World of
   Warships). Answer: the HUD shows each group's state where the eye already is (the
   group crosshair and the arc fan), and per-group autofire exists for players who would
   rather manoeuvre.
2. Holding the cursor inside a narrow arc while also flying the ship is hard (Starsector).
   Answer: a hold-to-face modifier for nose guns, and autofire for side guns.
3. Over-eager auto-orientation fights the player (Nebulous ships hunting for an angle).
   Answer: the player's hull never turns itself; only an AI hull turns to bring a group to
   bear, and it chooses one group.
4. Arcs drawn everywhere become clutter. Answer: draw only arcs below 360 degrees, the
   Endless Sky "blindspots only" default.

### Body facts

- **C1. The input layout.** `Assets/Resources/Aetheria.inputactions`, map Player: `Move`
  (Vector2: W/S as y, A/D as x, gamepad left stick), `Look` (pointer delta, right stick),
  and `Turn` (1D axis, Q negative, E positive), which no script reads. Fire is the action
  bar: `ActionGameManager.cs:101-103` binds the left, right and middle mouse buttons to
  action-bar slots, and `ActionBarWeaponGroupBinding.Activate` (`ActionBarSlot.cs:189-195`)
  activates every weapon in its group. No Shift binding exists in the Player or Global maps.
- **C2. The player's steering is the aim.** `ActionGameManager.cs:1277-1281`: the pointer
  delta (cursor locked, `:648`) integrates `_entityYawPitch`, which becomes
  `_viewDirection`, which is written to `CurrentEntity.LookDirection`. `:1290` writes
  `Move` to `ship.MovementDirection` (x strafe, y thrust).
- **C3. `LookDirection` is the facing command.** `Ship.Update` (`Ship.cs:278-291`) sets
  `deltaRot = dot(look, right)`, shapes it `sqrt(|d|) * sign(d)`, feeds it to the rotation
  thrusters and the aether drives' `Axis.z`, and below `|d| < .01` writes
  `Direction = lerp(Direction, look, ...)` directly. So aim and facing are one field.
- **C4. Turning is kinematic.** No angular velocity exists. A thruster rotates
  `Entity.Direction` by `input * Torque * Thrust * TorqueMultiplier / Mass * dt`
  (`Thruster.cs:106-107`); an aether drive by `force.z * axis.z * AetherTorqueMultiplier /
  Mass` (`AetherDrive.cs:154-155`). `Settings.asset` (LFS): `TorqueFloor 0.5`,
  `TorqueMultiplier 0.1`, `AetherTorqueMultiplier 0.1`. `Ship.TurnTime` (`Ship.cs:61-66`)
  estimates a turn. Turn rate is therefore torque over mass times one global multiplier.
- **C5. FireControl's arc.** `ArcFor` (`FireControl.cs:36-41`) is
  `HardpointData.FiringArc` (`ItemData.cs:569`, key 6) when above zero, else
  `GameplaySettings.FiringArc` (`Settings.cs:214`, initializer 120; the field is absent
  from `Settings.asset`, so 120 holds). `InArc` (`:50-58`) compares a planar bearing with
  `MountDirection` (hull `Direction` rotated by the item's rotation); 360 or more passes,
  and a bearing under 1e-6 passes. `AimDirection` (`:116-126`) clamps `LookDirection` to
  the arc's nearer edge. The Turret hull's hardpoints author 360 (`AetherDb
  firing-arc-migrate`, `Program.cs:1037`).
- **C6. The trigger gate reads the target, not the aim.** `ArcPermitsFire` (`:76-88`):
  with no target it passes; with a target it tests the target's bearing, and a fused
  weapon passes anyway (operator ruling 2026-09-30, `fire-control-cut.md:2628-2640`).
  `PFire` (`:256-270`) prices a shot at zero out of arc. `Solve` (`:440-473`) flies a
  round to the target's intercept when it is engaged (`TravelDirection`, `:396-400`), and
  along `AimDirection` otherwise. So the cursor today decides only facing, reticle
  targeting (`Entity.TargetUnderReticle`, `Entity.cs:407-412`), no-lock fused flight,
  the lock cone (`LockWeapon.cs:100`), the tractor (`ShipInstance.cs:116`), guided
  rounds' aim point (`GuidedProjectileManager.cs:79`) and loot's view direction
  (`ZoneRenderer.cs:440`).
- **C7. The camera is a chase camera that looks at the aim.** `FollowCamera` follows the
  ship and looks at `EntityInstance.LookAtPoint`, set each frame to ship position plus
  `LookDirection` times the target range, or 10,000 with no target
  (`EntityInstance.cs:401-402`, `ActionGameManager.cs:1039-1040`). The scene's two
  framing transposers (`ARPG.unity:4963`, `:21276`) position the camera and do not rotate
  it, so the camera's heading comes from the aim and survives the split. Which of the two
  belongs to `FollowCamera` was not resolved.
- **C8. What the HUD draws.** `UpdateTargetIndicators` (`ActionGameManager.cs:1390-1422`):
  `ViewDot` at `LookAtPoint`; one crosshair per articulation group at the mean of its
  barrels' forward rays (`_articulationGroups`, `:1041-1057`, grouped by the barrel's
  `ArticulationPoint.Group`); the target indicator; lock indicators. `Update` fills the
  target panel's bars and `UpdateFireControlDebug` (`:1319-1380`) prints the fire-control
  gates as text, including `arc {d.InArc}`. No arc, no lead marker and no predicted path
  is drawn.
- **C9. The barrel picture has its own arc.** `ArticulationPoint` (Unity) slews a barrel
  toward `Target` (every point's target is `LookAtPoint`, `EntityInstance.cs:278-283`)
  at `Speed` and clamps yaw to `YawMin..YawMax`, authored by `ShipPrefabAuthoring.cs:141-148`
  and unrelated to `FiringArc`. Package hulls have none (`ShipModShips.cs:164`): their
  weapons are points, so every package-hull weapon lands in group -1 and one crosshair.
- **C10. AI writers treat `LookDirection` as facing.** `Agent.Accelerate`
  (`Agent.cs:59-79`) writes it to turn toward the velocity error. `Combat` (`:98-128`)
  calls `Accelerate(noTurn: true)` and then writes `LookDirection = toTarget` (the
  intercept), which turns the nose to the target. `MoveTo.cs:26` writes it.
  `TurretController.cs:74-77` writes it; a turret hull has no rotation thrusters, so it
  only aims. Faction-play r1's new `FollowState` "looks at the anchor" and `FleeState`
  flies through `Accelerate`, so they inherit the same assumption; their r2 (follow-up
  `faction-play-reanchor-demo`) must use the helm port below.
- **C11. The Arcs scenario assumes nose aim.** `Scenarios/Arcs.cs`: forward mounts, a bare
  hull off the bow and one off the stern, a hostile 360 turret. Its check "nothing fires at
  the stern one through the ship" could only be reached by turning, because turning the
  view turned the hull. Checklist item 11 (`merge-to-master-checklist.md:49`) also wants
  side mounts firing abeam.
- **C12. Weapon groups persist.** `Entity.WeaponGroups` (`Entity.cs:69`) is saved as
  `EntityPack` key 16 (`EntitySerializer.cs:189`) and `Loadout` key 3 (`Loadout.cs:19`).

### Authority map

- **Owner.** `Ship` owns facing: `Ship.Update` turns the hull from one demand,
  `Ship.Turn` (-1..1, positive clockwise), and from nothing else. `Entity.Aim`
  (`LookDirection` renamed) owns where the pilot points. `FireControl` owns every mount
  question: its arc (`ArcFor`), its axis (`MountDirection`), whether its trigger is free
  (`ArcPermitsFire`), and the one direction its next round flies (`MountAim`, new).
- **Inputs.** The player's `Turn` axis and hold-to-face button, the pointer delta, the AI
  states' chosen headings, the hardpoint's `FiringArc` and item rotation, the designated
  target.
- **Outputs.** Hull rotation through the thrusters and drives; the trigger gate; each
  round's flight direction; the facts the HUD draws.
- **Derived state.** `Steering.Toward(ship, heading)` derives a turn demand from a
  heading (the C3 law, moved). The barrel picture, the group crosshairs, the arc fans,
  the lead marker and the predicted path are display-only derivations of FireControl and
  `Ship.Coast`.
- **Demotions.** `LookDirection` is no longer an owner of facing; it is renamed `Aim` and
  steers nothing. `ArticulationPoint` is no longer an owner of arcs; its yaw derives from
  `MountAim`. The barrel transforms are no longer the source of the group crosshair.
- **Forbidden writers.** Any write of `Entity.Direction` outside the thrusters, the aether
  drives, wormhole exit, load and staging (the `lerp` snap in `Ship.Update` goes). Any
  read of `Aim` in `Ship.Update`. Any read of `FiringArc` outside `FireControl.ArcFor`,
  hardpoint validation and AetherDb. Any arc, bearing or intercept arithmetic in
  `Assets/Scripts/Gameplay` or `UI`.
- **Shared paths.** The player and every AI state reach the hull only through `Ship.Turn`;
  player and AI rounds leave through the same `MountAim`; the HUD, the barrel picture and
  `Solve` read the same `MountAim`.
- **Deletion line.** Before any new behaviour: the facing block of `Ship.Update`
  (`Ship.cs:278-291`) is replaced, the `LookDirection` field is renamed so that every old
  writer fails to compile until it chooses aim or helm, and `ArticulationPoint`'s yaw
  clamp is deleted.

### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Turn demand (`Ship.Turn`) | Per ship, runtime only. | Written every frame by the player input or the ship's agent; never saved. | The pilot (input or agent), through `Steering.Toward` for headings. |
| Aim (`Entity.Aim`) | Per entity, runtime only. | Written every frame by the player's view or the agent; never saved (as `LookDirection` was not). | The pilot. |
| Mount aim | Derived per weapon from aim, target, arc and mount. | Computed on read. | `FireControl.MountAim`. |
| Gun solution | Derived per weapon from its subject, arc, mount and aim. | Computed on read. | `FireControl.Solution`. |
| Face-the-aim state | Per player session, runtime only. | Held by Left Shift, latched by Caps Lock; never saved. | `HelmInput`. |
| Arc-display preference | `PlayerSettings` player store. | Edited by a toggle; persists. | The player. |

### Rationale

**Why rename `LookDirection`.** A rename makes every old writer stop compiling until
someone decides whether it meant aim or helm. Keeping the name would leave AI states that
mean "turn there" compiling as "aim there", silently, which is exactly the split-brain the
rebuild removes. The Gameplay and test call sites are mechanical.

**Why a turn demand rather than a desired heading.** The player's keys command a rate;
the AI and the hold-to-face button command a heading. A demand is the narrower port: a
heading converts to it through one function (`Steering.Toward`), and a rate needs no
conversion. A heading port would force the keys to invent a far-off heading.

**Why the cursor gates and the target lands.** The ruling's words make the aim the
trigger gate: a fixed mount fires when the aim is inside its arc. FireControl's lock
model, its hit pricing and its tests are built on a designated target. Question
`controls-target-pulls-rounds` asks whether a designated target inside the arc pulls the
rounds and turrets onto it (Escape Velocity's front-quadrant turret), or whether rounds
always fly along the clamped aim, which would reopen FireControl's pricing.

**Why traverse is not simulated in the demo.** Traverse speed is a stat of the moving
part (ruling `arc-and-traverse-on-the-link`), and that model is full game. `MountAim` is
stateless now; articulated mounts make it stateful behind the same signature, and every
caller (Solve, the trigger, the HUD, the barrel) already reads it, so nothing else moves.

**Why the predicted path is a coast.** It answers "where does this hull go if I let go",
which is the question gravity wells and slower turns make hard. It reuses the hull's own
drag and gravity step (extracted to `Ship.Coast`), so the line cannot disagree with the
simulation. Thrust is not projected: it changes every frame.

**Cut order.** `controls-helm` first: the split itself, and the rename forces every writer
to be ported in the same cut. `controls-mount-aim` second: the trigger and the one round
direction (revised by "Firing on solutions" below into the per-gun solution). Then
`controls-hud` and `controls-ai-bearing`, which are independent of each other.
`controls-autofire` was withdrawn under ruling `fire-on-solutions`.

### Firing on solutions

Revision of 2026-10-06 after the operator's rulings on this section's questions:
`controls-target-pulls-rounds` (target-pulls), `controls-facing-input` (A/D turn, Q/E
strafe, Left Shift held or Caps Lock toggled faces the aim), `controls-turn-inertia`
(kinematic, tuned) and the direction `fire-on-solutions`: "Fire control should surface a
firing solution for any gun that's in arc, and the player should be able to fire on any
solution without aiming." The aim stops being the only trigger gate. Question
`controls-autofire` was withdrawn with it. Anchors are against `origin/master` `e1296f1f`,
whose code is unchanged from `228f241e`.

#### Prior art: firing without manual aim

Source marks as above.

- **Elite Dangerous** (wiki: the fandom `Category:Weapons` page, read in full through the
  MediaWiki API). Three mounts of every weapon trade effort for output. Fixed mounts are
  the strongest and shoot straight ahead with a few degrees of convergence. Gimballed
  mounts track a locked target inside a cone and "act as a Fixed mount", firing straight
  ahead, when nothing is locked. Turrets cover 360 degrees and take one global mode:
  Forward Fire (manual, as fixed), Target Only (track and fire on the locked target inside
  their arcs once the trigger is pulled) and Fire at Will (any hostile that is firing at
  you). Chaff breaks gimbal and turret tracking but not fixed mounts. So assisted fire is
  paid for in damage and is deniable by countermeasures, and manual aim keeps its value.
- **MechWarrior Online and 5** (snippet). Weapons sit in numbered groups on triggers;
  a group can chain-fire. Lock-on weapons (LRMs) cannot fire without a lock, which a
  teammate or your own sensors must hold; Artemis shortens the lock.
- **Nebulous: Fleet Command** (snippet). Weapons are tasked on designated targets; ships
  turn to bear. WCON "Free" lets untasked weapons return fire on any contact that attacks
  the ship; "Tight" fires only on command; Hold Fire keeps the orders. Shift groups like
  weapons into batteries such as fore and rear.
- **Battlestar Galactica Deadlock** (snippet, earlier pass). Batteries fire on ordered
  targets within their arcs; arcs show on demand.
- **Freelancer** (snippet). The reticle leads the target, and fixed and turret guns are
  all slaved to the mouse crosshair; the lead cross is the aim aid.
- **Starsector** (wiki, earlier pass). Autofire groups pick and attack their own targets,
  preferring the designated one.

**Failure modes.**
1. Solutions nobody can read in a fight: a marker per gun per contact swamps the screen
   (Elite's Fire at Will turrets spraying at whatever shoots, Starsector's autofire
   picking its own targets). Answer: solutions are drawn per weapon group, against one
   subject, on the group crosshair the player already reads.
2. No skill left: if every gun fires itself on everything, the player only steers.
   Answer: the player still designates, chooses which group to fire and when, and steers
   the arc onto the target. A solution needs targeting data (FireControl's `Designated`
   and reveal tiers), so a contact you cannot see well gives no solution and free aim keeps
   its value, as Elite's chaff keeps fixed mounts valuable.
3. Guns that silently refuse (the old "my guns don't fire"): a group whose trigger is
   held and does nothing. Answer: each group crosshair shows one of three states, and the
   arc fans use the same colours.

#### Body facts

- **C13. A solution type already exists.** `FireSolution` (`FireControl.cs:1809-1824`) is
  `Solve`'s per-shot answer: outcome, `PFire`, `Designated`, the engaged entity and the
  travel direction. `HitProbability` (`:316-324`) is the one live price, and `Inspect`
  (`:331`) is its presentation-only breakdown. `PFire` (`:256-270`) holds the two gates a
  solution needs: `Designated` (visible, in range, locked) and `InArc` of the target.
- **C14. A target is one entity per shooter.** `Entity.Target` holds one `TargetRef`
  (an entity or a chunk); reticle, nearest, next and previous are its only player writers
  (`ActionGameManager.cs:389-392`).

#### The design

- **A solution is per gun and has one owner.** `FireControl.Solution(Weapon, Entity
  shooter)` answers, for one gun: the subject it is drawn against, whether it bears (the
  subject is `Designated` and inside this mount's arc, the same two gates `PFire` already
  applies, now one extracted function `Bears` that `PFire` also calls), and the direction
  its round would fly (`MountAim`: the subject's intercept when it bears, else the
  arc-clamped aim). The price stays `HitProbability`; the HUD asks it, never a copy.
- **What a solution is drawn against** is question `controls-solution-subject`. The specs
  are written for the recommended option, the designated target.
- **The trigger.** A gun fires when its trigger is held and one of three holds, checked
  in this order by one gate (`ArcPermitsFire`): it is fused (the 2026-09-30 exemption); it
  has a solution, and fires on it without the aim; or the aim is inside its arc (free fire
  along the aim). Otherwise it holds. Free fire with no solution is the
  `facing-separate-from-aim` rule unchanged: a fixed mount fires along the aim when the aim
  is inside its arc.
- **How the player fires on a solution** is question `controls-solution-trigger`. The
  specs are written for the recommended option: the existing group triggers, with no new
  input.
- **The HUD.** One crosshair per weapon group, in one of three states: on solution (it
  sits on the subject's intercept, since `MountAim` is the intercept, and shows how many
  of the group's guns bear and the best `HitProbability` among them); free (it sits on the
  aim ray); holding (dimmed at the arc edge nearest the aim). Arc fans take the same
  colours per gun. The separate lead marker of `controls-hud` r1 is dropped: a group
  crosshair on solution is the lead.
- **Autofire is not mapped.** Its job was to let side guns fire without the aim; a
  solution now does that on the group trigger. Firing with no input held is not ruled, so
  `cut-controls-autofire` is withdrawn.
- **The AI is unchanged by this.** Agents already fire on what is in arc and worth it
  (`AgentFires` over `HitProbability`), and their aim is their intercept, so `Solution`
  and `AgentFires` agree for them. `cut-controls-ai-bearing` still holds.
- **The face-the-aim state** has one owner, `HelmInput` (engine-free, ServerShared): a
  held flag fed by Left Shift, a latched flag toggled by Caps Lock, and `FaceAim` as their
  union. While it is on, the hull turns toward the aim and the A/D axis is ignored. The
  latch is the game's own state, not the keyboard's Caps Lock light, so the HUD shows a
  small "face aim" mark while it is latched.

#### Authority map changes

- `FireControl.Solution` is the one owner of a gun's solution; `MountAim` is its
  direction and is no longer a separate decision. `PFire` and `Solution` share `Bears`.
- `ArcPermitsFire` is no longer the aim's gate alone; it is the one trigger gate, in the
  order above.
- `HelmInput` owns face-the-aim. Shift and Caps Lock are its inputs; `ActionGameManager`
  reads `HelmInput.Demand` and decides nothing.
- Forbidden: a second hit price or arc test in Gameplay or UI (the HUD reads `Solution`
  and `HitProbability`); a face-aim flag outside `HelmInput`.

## Missiles and point defense

Ruling `missiles-sim-side-light`: "We need missiles sim side, and we can cut that so as to
enable PDC without requiring full entities. Kind of like the asteroids." Ruling
`thresholded-autofire`: "I think we need thresholded autofire as an option, otherwise PDC
would require too much micro." Ruling `missiles-fuel-and-seekers`: "a missile knows where
it's going when it's fired but relies on its own sensors for terminal maneuvering.
Maneuvering which should expend limited fuel. A missile that spends fuel evading PD might
not have enough left to catch up to a target that is itself also dodging." Ruling
`missile-stats-now-gear-later`: "For now these stats will belong to the missile, later they
will be derived from gear." Full munition entities stay later (ruling
`drones-and-munitions-are-entities`, follow-up `drones-munitions-substrate`); nothing here
may foreclose them.

The third pass answers four more rulings. `missiles-resolution-model`, geometry-sets-odds: "I
think geometry should set the odds, though. We're not simulating realistic scale and
collisions. Going sideways really fast can help you evade both guns and missiles, but the
extent should be based on stats. If it's only geometry then the missile's terminal
maneuvering fuel, gathered targeting data and piloting skill don't exist at all as far as hit
resolution is concerned." `missile-cognition`: "I actually did mean the skill of the missile
itself. Cognition is a commodity in Aetheria, and a smarter missile can better spend its
delta v". `manoeuvre-penalises-solutions`: "There is currently no firing solution penalty for
target maneuvering afaik, which is a real gap for the speed demon hot doctrine glass cannon
play style." `evasion-not-only-flight-time`: "Deviation from predicted intercept means you
have to spend your delta as the projectile is in flight. Doesn't help you dodge a laser at
all." `missiles-seeker-retarget`, hostile-in-cone: "I like the idea of letting them reacquire
targets. Makes turning invisible and firing decoys a plausible strategy."

Anchors are against `origin/master` `7f51d46d` (nothing under `Assets/Scripts` or `tools`
changed since `c7432f39`, where the second pass read them), read in a detached scratch
worktree on 2026-10-06. Cuts: `missile-stats` (r2), `evasion-term` (r1), `missile-records`
(r3, flight only), `missile-odds` (r1), `missile-presenter` (r2), `munition-shots` (r3),
`autofire-threshold` (r3), `ai-autofire` (r1, unchanged).

### Prior art

Source marks as in Controls.

- **Nebulous: Fleet Command** (wiki: the community `mechanics:missiles` page, read in full).
  Missiles are tracked contacts with hit points by template (10 to 150) and armour as wall
  thickness, and a radar cross-section that varies with aspect. Point-defense turrets and
  defensive missiles engage them automatically; decoys soak PD attention (turrets take
  decoys first). Launch rate is bounded by programming channels, which caps salvo
  density. Defensive missiles cannot engage other defensive missiles, so two ships cannot
  dump their magazines at each other's interceptors (snippet, glossary search).
- **Starsector** (wiki: `Weapon_data.csv`, read in full). A missile is a projectile with
  "proj hitpoints". Weapon hints decide targeting, not whether a round can physically
  connect: `PD` targets missiles but prefers missiles in range over ships, `PD_ONLY`
  targets only missiles (and fighters with `ANTI_FTR`), `PD_ALSO` takes missiles only when
  nothing else is available. Autofire runs per-weapon target AI (snippet, mod README).
  Forum consensus (snippet): missile power is non-linear, weak until it saturates PD, then
  strong until magazines run dry.
- **FreeSpace 2** (snippet, hard-light wiki `Weapons.tbl` and `Ai_profiles.tbl`). Bombs are
  "interceptable": weapons with hit points and an enlarged collision radius. Turrets shoot
  bombs; AI profiles stop turrets targeting bombs beyond their own weapon range; bombs are
  invulnerable for a short delay after launch.
- **Children of a Dead Earth** (snippet, forum threads). PD is a saturation contest: either
  saturate it with many small rounds or send few heavily armoured missiles; small lasers
  are enough for PD. The counterplay is economic, not a hard counter.
- **Homeworld** (snippet). Flak and defense-field frigates are the anti-missile and
  anti-strike roles; no technical detail found.


**Fuel, seekers and terminal guidance** (second pass, for ruling `missiles-fuel-and-seekers`).

- **Nebulous: Fleet Command** (wiki: the community `mechanics:missiles` page, read in full).
  A missile's engine trades burn duration, top speed and manoeuvrability in one fuel-mix
  editor; engine size adds fuel and range only. During the burn it accelerates and turns at
  its G rating; after the burn it coasts at its final velocity and cannot manoeuvre.
  Terminal manoeuvres (weave, corkscrew) start when the seeker finds the target and exist to
  survive point defense. Launch on a track plots an intercept from the reported position and
  velocity; the seeker then re-selects its target every 0.5 s by signal strength, and a
  command-guided missile that loses its track cruises to the last known position. Seekers
  have a range and a cone (active radar 2 km, 50 degrees). Decoys draw PD.
- **Children of a Dead Earth** (forum: the missile construction and tuning guide, read in
  full). Delta-v is split into boost, midcourse and terminal phases; against fast or evasive
  targets 50-60% goes to boost, against slow ones 70-80%. A missile whose terminal delta-v
  runs out against a manoeuvring target cannot close; high acceleration lowers the terminal
  delta-v needed. Guidance is proportional navigation or augmented PN, and a target that
  burns briefly then coasts makes APN waste delta-v chasing an acceleration that stopped.
- **DCS World** (simtuts defence guide, read in full; Falcon BMS not fetched). A missile's
  motor burns out and it then bleeds energy, so a long shot arrives slow and is defeated by
  basic manoeuvring; turning cold makes it fly farther; a hard break 2-3 s before impact
  exploits proportional navigation's turn limit; beaming (notching) breaks a Doppler
  seeker's lock. The lethal envelope is smaller than maximum range.
- **Starsector** (wiki: Harpoon MRM and ECCM Package pages). Missiles have a flight time
  (Harpoon 12.5 s) and hit points (Harpoon 150); light PD and flak intercept them; a nimble
  frigate can evade a first run, and ECCM (more acceleration and turn rate) is the answer.
  Missiles that run out of flight time still collide (snippet).
- **HighFleet** (gameplay.tips guide, snippet). A blind-fired missile flies ballistic and
  switches on its own radar 100 km before the aim point, a 75 km, 90-degree cone; it locks
  the first fleet, friend or foe, that enters the cone and guides to impact.
- **Homeworld** (fandom wiki, snippet). Missile stats are per missile type (1,250 m/s,
  1,650 m/s^2, 120 HP for the missile destroyer's round); no fuel model found.
- **Guidance law** (Cranfield, "Zero-Effort-Miss Shaping Guidance Laws", snippet). PN commands
  lateral acceleration proportional to the line-of-sight rate; the zero-effort miss over
  time-to-go is the acceleration a missile still needs. PN needs only the bearing and its
  rate, which is what ruling `shared-track-bearing-only` allows a missile in flight.

**Odds from geometry and stats** (third pass, for rulings `missiles-resolution-model`,
`manoeuvre-penalises-solutions` and `evasion-not-only-flight-time`; recalled, not fetched this
pass).

- **EVE Online.** A turret's hit chance falls with the target's angular velocity against the
  turret's tracking stat and with its signature against the turret's resolution, whatever the
  round's flight time; a missile's damage falls with the target's speed against the missile's
  explosion velocity. Going sideways fast helps against both, by an amount the stats set: the
  operator's sentence, shipped as a tracking term rather than as flight-time dodging.
- **Zero-effort miss** (Cranfield, above). The miss a guided round would make if neither side
  manoeuvred from now on is the standard terminal quantity; what the missile can still correct
  is its lateral acceleration and remaining delta-v over the time to go. The odds below price
  that endgame instead of asking a coarse simulation to decide contact.

**Failure modes to design against.**
1. PD trivialises missiles: perfect interception makes launchers dead weight. Answer: a
   PD round is priced and rolled like any shot (spread against a small target, range,
   sensor info), a round that cannot arrive before the missile does has no solution, and
   heat and ammunition bound how much a group can spend. Saturation then beats PD, as in
   Starsector and CDE.
2. PD that never works: interception left to luck the player cannot read. Answer: the HUD
   marks detected inbound munitions and the ones an autofire gun is engaging, and an
   intercept shows an effect at the munition's simulated position.
3. Unreadable salvos: thirty rounds and thirty tracers. Answer: markers only for detected
   hostile munitions within weapon reach, one shape, no per-gun lines.
4. Cost blowups: per-missile physics engines, per-missile agents, per-gun O(contacts) scans
   every frame. Answer: a missile is a few floats on its pending shot stepped by one pure
   function (a PN command, a fuel debit, one settled seeker reading and one
   closest-approach test against its own target); reacquisition queries the targeting index
   only on a seeker interval; an autofire gun keeps its subject while it stays worth firing.
   The per-missile step cost is measured and pinned (`missile-records`).
5. Recursion: PD missiles shooting PD missiles (Nebulous's explicit rule). Answer: a
   munition weapon never takes a munition as its subject.
6. Unreadable missile states (burning, coasting dry, seeker blind). Answer: the presenter's
   flame follows the live throttle and goes dark when the tank is empty; the HUD diamond is
   filled while a munition can still manoeuvre and hollow once it coasts; a missile that
   self-destructs at the end of its life shows a fizzle, not a hit.
7. PD-or-nothing balance: evasion that is free makes PD useless; no evasion makes PD
   decisive. Answer: evasion and pursuit draw on the same fuel, so PD pressure that a
   missile survives still costs it terminal authority downrange (the operator's sentence),
   and a dodging target spends that authority faster.
8. Two hit models drifting apart. Answer: one roll and one price owner for every shot. A gun
   round is priced from its frozen shooter data, the target's realized deviation in flight and
   the tracking term; a guided round flies until its terminal gate and is priced there from its
   own seeker data, its zero-effort miss beyond reach, the same tracking term and its terminal
   correction. Both commit through `Commit` and apply through `Apply`, read the one
   `PDeviation` shape and the one `Evasion` term, and nothing after the roll decides again.
9. Determinism and replay. The zone steps by the frame's delta time (M15), so a stepped
   missile is exactly as replayable as a ship's motion and no less. Answer: no shared random
   stream (the evasion phase comes from the shot id, as the roll's seed does), and the
   terminal gate tests closest approach of straight motions, so a long frame cannot step past it.

### Body facts

- **M1. A guided round is an ordinary shot in the simulation.** `LauncherData`
  (`Behaviors/Launcher.cs:10`, a `LockWeaponData`, so a `LockWeapon`) and `GuidedWeaponData`
  (`:37`, an `InstantWeapon`) fire through `InstantWeapon.Execute` into `FireControl.Fire`
  (`InstantWeapon.cs:271`) like any gun. Flight time is `FlightDistance / weapon.Velocity`
  (`FireControl.cs:559`); `CommitTime` is `ArrivalTime - CommitHorizon` (0.5 s,
  `Settings.cs:250`), or the fire time for a fused round with no target (`:588`); `Commit`
  rolls at that horizon (`:707`) and `Step` applies at arrival (`:645`). No ServerShared
  code reads `MissileVelocity`, `Thrust`, `DodgeFrequency` or the three curves.
- **M2. The flight is Unity's and decides nothing.** `GuidedProjectile.Update`
  (`Gameplay/Weapons/GuidedProjectile.cs:127-212`) homes with `first_order_intercept`, a
  noise dodge and the guidance, lift and thrust curves. `GuidedProjectileManager.Bind`
  (`GuidedProjectileManager.cs:14-26`) subscribes to `ShotCommitted` (retarget to the
  burst point) and `ShotResolved` (burst or fade). The split branch (`GuidedProjectile.cs:166-194`)
  spawns visual children that no shot owns. Between fire and arrival a missile is a
  `PendingShot` with no position and a Unity transform nothing simulated can reach.
- **M3. The `GuidedWeaponData` branch homes on the aim** (`GuidedProjectileManager.cs:62-80`, the aim at `:79`,
  `LookDirection`, renamed by `controls-helm`). An unfused round with no engaged target has
  `FlightDistance` 0 (`FireControl.cs:494`) and so resolves the tick it fires.
- **M4. The pending shot.** `PendingShot` (`FireControl.cs:1748-1798`) is a struct in
  `Zone.PendingShots` (`Zone.cs:46`), never saved; `TryGetShot` is a linear scan
  (`Zone.cs:55-65`). Its `Target` is an `Entity`. `Step` resolves a shot whose target left
  the zone as a fresh `Miss`, at any stage, without rewriting the committed outcome
  (`:619-629`): the precedent an intercepted missile follows. `ShotResult` is `Miss`,
  `Hit`, `Burst` (`:1944-1949`).
- **M5. Mines are not simulation records.** `Mine` moves by a Unity `GridObject`, arms by
  `Physics.OverlapSphere` and damages through `FireControl.Detonate`
  (`Gameplay/Weapons/Mine.cs`, `MineManager.cs`). `Detonate` (`FireControl.cs:1350`) iterates
  `zone.Entities` only. Follow-up `bodies-entity-and-mines` owns mines; no cut here moves them.
- **M6. No point defense exists.** A case-insensitive search for `point defen`, `PDC`,
  `\bPD\b` and `anti-?missile` over `Assets/Scripts` and `tests` returns nothing. Nothing can
  take a shot as its target.
- **M7. Targets.** `TargetRef` (`Zone.cs:595-621`) is an `Entity` or a `ChunkId`. The zone
  registers `EntityTargets` and one `BeltTargets` per belt (`Zone.cs:101`, `:119`). Shots
  at chunks are not routed yet (`FireControl.cs:533`, mining Cut 4).
- **M8. A weapon's only subject is its entity's one target.** `Fire` reads
  `source.Target` (`FireControl.cs:534`), `Refuses` reads it (`:94`), and
  `Weapon.StanceAllowsFire` reads it (`Weapon.cs:107`).
- **M9. AI fire is per weapon.** `Combat` (`Agents/States/Combat.cs:119-124`) and
  `TurretController` (`Behaviors/TurretController.cs:83-96`) activate each weapon when
  `FireControl.AgentFires` (`FireControl.cs:101-109`) passes: `HitProbability >=
  AgentMinHitProbability` (0.2, `Settings.cs:259`) for an unfused weapon, designated and not
  refused for a fused one.
- **M10. Presenters fly along the barrel.** `ProjectileManager.Fire` launches along
  `barrel.forward`, not the shot's frozen `TravelDirection`; package hulls have no
  articulation (C9), so their rounds leave along the hull whatever the simulation decided.
- **M11. EntityPack keys.** The base `EntityPack` uses keys 0 to 16; `ShipPack` uses 17 to
  19 and `OrbitalEntityPack` 17 to 20 (`EntitySerializer.cs:153-189`). The next free base key
  is 21.
- **M12. Shift is already an action-bar slot.** `GetDefaultPlayerSettings`
  (`ActionGameManager.cs:95-105`) binds `<Keyboard>/leftShift` to action-bar slot 0, which
  `controls-helm` r2 also binds to Face Aim. Reported to Self; not this section's cut.
- **M13. Launcher content.** GT 3K, pswarm and scorched void policy carry guidance-system,
  thruster and warhead roles; LRMM72 and SRMM72 are flat (`docs/stats-power-cut7-roles.md:139-146`).
  The Launcher Angles scenario fits a GT 3K and a pswarm (`Scenarios/LauncherAngles.cs`).
- **M14. Cost.** The first pass's derived pose cost nothing per tick. A missile that spends
  fuel by events cannot be a closed form of its shot and time (what it spent depends on
  what PD and its target did), so the revision steps it: per live munition per tick, one PN
  command, one fuel debit, one settled seeker reading (`Sensor.Gain`, no stored info), one
  closest-approach test against its own target, and, while it has no lock, one index query
  per seeker interval. No ServerShared scratch build was run for this pass;
  `missile-records` r3 measures the step and pins it.
- **M15. Zone time is the frame's delta time.** `ActionGameManager` calls
  `Zone.Update(Time.deltaTime)` (`Gameplay/ActionGameManager.cs:1308`); `Zone.Update`
  (`Zone.cs:202-221`) advances time, orbits, agents and entities by that dt, then
  `FireControl.Step`. There is no fixed tick. Rolls are seeded per (zone, shot id)
  (`FireControl.cs:707` onward), so an outcome does not depend on what else drew, but no
  zone state is frame-exact replayable today. Headless tests step with a fixed dt.
- **M16. What a seeker can reuse.** `Sensor.Gain` (`Behaviors/Sensor.cs:187-190`) is the one
  gain rule: a passive tick adds `visibility * sensitivity * response * dt / distance`.
  Entities hold stepped info (`Sensor.Execute`, `EntityInfoGathered`, `:137-180`); a chunk's
  info is the settled value `saturate(sum(rate) / (n * TargetInfoDecay))` computed when asked
  (`Entity.ChunkInfo`, `Entity.cs:357-370`). Detection is info above
  `TargetDetectionInfoThreshold` for entities (`Entity.cs:245-256`) and chunks (`:374`). A
  target's brightness is `Entity.Visibility`, the sum of its `VisibilitySources`
  (`Entity.cs:139`). A seeker therefore needs no stored info: its reading of its target is
  the settled value from its own sensitivity and cone, the chunk rule with one sensor.
- **M17. Launcher data.** `LauncherData` keys 26-31 and `GuidedWeaponData` keys 21-26 hold, in
  order, `GuidanceCurve`, `ThrustCurve`, `LiftCurve`, `Thrust`, `DodgeFrequency`,
  `MissileVelocity` (`Behaviors/Launcher.cs:10-61`). `LockWeaponData`'s `LockSpeed`,
  `SensorImpact` and `LockAngle` are the launcher's lock-on, the shooter's sensors, not the
  round's (`Behaviors/LockWeapon.cs:13-19`, `:104`). The warhead is `WeaponItemData`'s
  `Damage`, `DamageSpread`, `DamageType`, `BlastRadius` (key 29) and `Fuse` (key 32)
  (`ItemData.cs:477-506`), read through `FireControl.FuseOf` (`FireControl.cs:63-68`). The
  catalog is `GameData/Aetheria.cc`; each catalog change has its own AetherDb
  `*-migrate apply` command (`tools/AetherDb/Program.cs:32-40`). The roles doc assigns
  `Thrust`, `MissileVelocity` and `Velocity` to the thruster role (`docs/stats-power-cut7-roles.md:139-146`).
- **M18. What the flight fields meant.** `GuidedProjectile.Update` (`GuidedProjectile.cs:127-212`)
  aims at `first_order_intercept` at `TopSpeed` (`MissileVelocity`), turns its velocity
  toward the desired one at `Thrust * ThrustCurve(progress)` per second, and blends a noise
  dodge at `DodgeFrequency` by `GuidanceCurve(progress)`. There is no fuel, no seeker, and the
  round fades past `Range`.
- **M19. Blasts.** `Apply` detonates a `Burst` or `Proximity` round through `Detonate` at its
  `BurstPoint` (`FireControl.cs:920-930`); `Detonate` overlaps the disc with the cells of
  every entity in the zone (`:1350`), the path mines use (M5). `HullEntry` (`:839`) gives the
  distance along a line to an entity's first metal cell.

- **M20. No manoeuvre enters the price, and none reaches an instant weapon** (probe for
  rulings `manoeuvre-penalises-solutions` and `evasion-not-only-flight-time`, source read at
  `7f51d46d` on 2026-10-06). `HitProbability` (`FireControl.cs:316-323`) is
  `PFire * PSpread * POnHull`: shooter accuracy and info, barrel spread, silhouette. The
  target's velocity enters only the lead (`PredictedIntercept`, `:202-208`), which cancels a
  constant sideways speed exactly. The one motion term is the roll's `DeviationProbability`
  (`:425-431`): the target's realized departure from the fire-time straight-line prediction,
  forgiven by the shooter's `Tracking`, measured at `CommitTime`. `Fire` sets `CommitTime` to
  `now + max(0, flightTime - CommitHorizon)` (`:588`, `CommitHorizon` 0.5 s), so a round whose
  flight is under 0.5 s commits at fire and measures nothing. Beams fire through `Fire` per
  `BeamResolveInterval` with flight time 0 (`Behaviors/ConstantWeapon.cs:176-188`), as instant
  weapons do (`InstantWeapon.cs:271`). Result: the solution the HUD, `AgentFires` and autofire
  read carries no manoeuvre penalty; at the roll only long-flight rounds see manoeuvres, only
  ones made in flight, and never a sideways speed. A laser cannot be dodged at all. The
  operator's reading holds.
- **M21. What agility exists.** No `Agility` stat exists anywhere. `Ship` aggregates
  `LeftStrafeThrust` and `RightStrafeThrust` from its active lateral thrusters and aether drives
  (`Ship.cs:52-53`, `:181-220`); a thruster accelerates its entity by `Thrust / Mass`
  (`Behaviors/Thruster.cs:104`). A hull's strafe acceleration is therefore
  `min(LeftStrafeThrust, RightStrafeThrust) / Mass`, derived from fitted gear; an entity that is
  not a `Ship` has none. No entity stores its acceleration. The shooter's tracking is
  `TargetingSystem.Tracking` (`Behaviors/TargetingSystem.cs:43`, `:80`), falling back to
  `UnaidedTracking` (10, `Settings.cs:228`) through `FireControl.Tracking` (`:190`): metres of
  deviation forgiven.
- **M22. The shot pipeline can carry a guided round.** `Step` (`FireControl.cs:609-656`)
  commits when `now >= CommitTime` and applies once committed and `now >= ArrivalTime`;
  `Commit` rolls `CommitProbability` with the per-shot seed (`:707-760`). A munition whose
  `CommitTime` stays infinite until its terminal gate, which then sets it to the gate's time and
  `ArrivalTime` to the closest approach, goes through the same commit, events and application
  as a gun round.

### The design

**The resolution model** is ruled: `missiles-resolution-model`, geometry-sets-odds. The second
pass recommended pure geometry (a fuze decides contact) and rejected both roll variants as two
answers to one question. The operator overturned it for a reason that pass did not weigh: the
simulation is not at a scale where contact means anything, so a fuze makes terminal fuel,
targeting data and the missile's own skill irrelevant to the hit. The split-brain worry is
answered by one commit point, after which the flight obeys the roll, which is the gun's own
contract (M22):

- The flight steps as before (PN, fuel, seeker) and decides *whether and when* a missile
  reaches its terminal gate, and with what left. A missile that runs dry, loses its seeker or is
  outmanoeuvred arrives with a large miss, or never arrives.
- At the gate the roll decides *whether it connects*, once, at a price `FireControl` owns. After
  the roll the flight presents the outcome and decides nothing.

**Evasion is a tracking term, one for every shot** (rulings `manoeuvre-penalises-solutions` and
`evasion-not-only-flight-time`). M20 shows the price has no manoeuvre term and a laser cannot be
dodged. The term measures how hard the target's current sideways motion makes a solution to
hold, whatever the flight time, and it adds to the in-flight deviation rather than replacing it.
`evasion-term` lands it before any missile cut reads it:

- `FireControl.Agility(Entity)`: a ship's strafe acceleration,
  `min(LeftStrafeThrust, RightStrafeThrust) / Mass` (M21), 0 for anything else. A munition's
  agility is its `Thrusters.Lateral` while it has delta-v, 0 dry.
- `Entity.Acceleration`: the entity's realized planar acceleration over its last update,
  written once per `Entity.Update`, never saved.
- `GameplaySettings.SolutionWindow` (0.5 s): the time over which a target's sideways motion
  counts against holding a solution. A fire-control constant, not a weapon or missile stat.
- `FireControl.Evasion(agility, velocity, acceleration, lineOfSight, window)`: the sideways
  distance the target can open on a solution within the window,
  `min(|v_lat|, agility * window) * window + 0.5 * min(|a_lat|, agility) * window^2`, lateral to
  the line of sight. Sideways speed counts up to what the hull could reverse in the window, so a
  heavy hull drifting sideways is still easy to hold; sideways acceleration (a dodge in
  progress) counts up to the hull's agility. The target's stats set the extent; its motion sets
  the use.
- `FireControl.PDeviation(deviation, tolerance)`: `saturate(1 - deviation / tolerance)`, a step
  at 0 when the tolerance is 0. The one shape every shot uses.
- Guns, beams and instant weapons alike. `DeviationProbability` adds
  `Evasion(Agility(target), target velocity, target acceleration, TravelDirection,
  SolutionWindow)` at the commit to the realized deviation, forgiven by the shooter's
  `Tracking` as before. `HitProbability` multiplies `PDeviation(Evasion(...), Tracking)`, so the
  solution the HUD shows, `AgentFires` and autofire's `Worth` all see a dodging target as a
  worse shot. A laser commits at fire, so its whole penalty is the tracking term.
  `TheHudEstimateIsTheCommitPrice` keeps holding: at zero realized deviation the forecast is the
  commit price.

**The speed demon's counterplay** (ruling `speed-demon-counterplay`): "Decent chance that unless
you specced specifically to counter them, the only gun you have that'll hit a speed demon is
stuff you intended to use for PD. That's what they're counting on, since they sacrificed all
their armor for that mobility." The term delivers that through two stats and nothing else: a
light gun's high `Tracking` forgives the evasion a heavy gun's low `Tracking` cannot, and the
target's agility (strafe over mass) is high exactly when it carries little armour mass.
`evasion-term` pins it with catalog-like numbers over the 0.5 s window: a speed demon crossing
at 150 m/s with 60 m/s^2 of strafe and dodging at full strafe opens `30 * 0.5 + 0.5 * 60 *
0.25 = 22.5` m, so a light gun with `Tracking` 50 keeps 0.55 of its unpenalised price and a heavy
gun with `Tracking` 10 keeps 0; a heavy hull crossing at the same speed with 10 m/s^2 of strafe
opens `5 * 0.5 + 0.5 * 10 * 0.25 = 3.75` m, so the heavy gun keeps 0.625 and the light one 0.925. The
autofire chooser lets PD guns divert: with no worth-firing munition, an autofire gun takes the
designated target or the best-priced hostile when its price clears the threshold. A
worth-firing hostile munition preempts an entity subject even while that subject stays worth
firing (`autofire-threshold` r3), so a PD group that diverted onto a speed demon still turns
back to the missiles.

**The missile's price** (`missile-odds`). At the gate, the same model with the missile's
inputs:

| Gun factor | Missile factor |
|---|---|
| `PFire` = shooter `Accuracy` x `PSensor(shooter info)` | data: locked, `PSensor(settings, 1, seeker reading)`, its own gathered data; unlocked, the frozen launch `PFire` |
| realized deviation in flight | zero-effort miss beyond reach, `max(0, ZEM - reach)`, with `reach = max(BlastRadius, SchematicCellSize / 2)` plus half the target's silhouette span across the line of sight |
| `Evasion` against the gun's bearing | `Evasion(Agility(target), target velocity - missile velocity, target acceleration, line of sight, SolutionWindow)` |
| tolerance `Tracking` | correction: locked, `Cognition * Closable(Lateral, DeltaV, time to go)`; unlocked, 0 |
| `PSpread`, `POnHull` | 1: a guided round has no barrel spread and steers onto metal |

`Closable(a, dv, t)` is the sideways distance a missile can still cover in `t` with acceleration
`a` and delta-v `dv`: `0.5 * a * t^2` when `dv >= a * t`, else `dv * t - dv^2 / (2a)`. Terminal
fuel, lateral authority and cognition widen the tolerance; seeker data scales the whole; the
target's motion and agility and the flight's own miss eat into it. An unguided rocket (no
seeker, no lateral authority) has tolerance 0, so geometry alone decides it: it connects only
when its straight line passes within reach.

**Cognition** (ruling `missile-cognition`) is `Control.Cognition`, 0 to 1, authored per missile
and later derived from guidance gear: the missile's skill at spending delta-v, in exactly four
places. (1) *Efficiency*: the debit is `|command| * dt * (2 - Cognition)`, so a mindless missile
wastes as much as it uses on corrections. (2) *Reserve*: before the gate, pursuit and evasion
may not spend below `Cognition * Lateral * CommitHorizon`, the delta-v a full-authority endgame
needs, so a smart missile arrives with its terminal and a dumb one may arrive dry. (3) *When to
jink*: evasion against inbound PD draws only on fuel above the reserve, so a dumb missile
(reserve 0) jinks itself dry while a smart one stops jinking to keep its terminal. (4) *The
price*: the correction scales with it.

**The missile's stats** (ruling `missile-stats-now-gear-later`). One `MunitionData` object on
`LauncherData` and on `GuidedWeaponData`, grouped by the gear each group will later come
from, so the entity cut replaces a group with a derivation from that gear without
reshaping anything that reads it:

| Group (gear later) | Fields (`PerformanceStat`, evaluated through the item) | Replaces |
|---|---|---|
| `Hull` (missile hull) | `Durability`, `Radius`, `Signature` | the first pass's global settings |
| `Thrusters` (missile thruster) | `Acceleration` (main motor), `Lateral` (manoeuvre authority: the sideways acceleration cap), `MaxSpeed` | `Thrust`, `MissileVelocity` |
| `Sensors` (seeker) | `Sensitivity`, `ConeHalfAngle`, `Range` | nothing (new) |
| `Control` (guidance, its cognition) | `Navigation` (PN constant), `Evasion` (share of `Lateral` spent jinking), `EvasionFrequency`, `Lifetime`, `Cognition` (skill at spending delta-v, 0 to 1, ruling `missile-cognition`) | `DodgeFrequency`, `GuidanceCurve` |
| `Fuel` (tank) | `DeltaV` (m/s) | nothing (new) |
| `Payload` (warhead) | no new field: `Munitions.Payload(item)` reads the weapon's existing `Damage`, `DamageSpread`, `DamageType`, `BlastRadius`, `Fuse` | nothing; one reader |

Payload is an accessor, not copied fields: the warhead already has one owner on
`WeaponItemData` (M17), and a copy would be a second. `LiftCurve` stays presentation outside
the groups. `ThrustCurve` dies once the presenter reads the live throttle. The
`missile-stats` cut lands the object, migrates the catalog (`Thrust` to
`Thrusters.Acceleration`, `MissileVelocity` to `Thrusters.MaxSpeed`, `DodgeFrequency` to
`Control.EvasionFrequency`), authors the new fields for GT 3K, pswarm, scorched void policy,
LRMM72 and SRMM72 (M13; the flat dumbfire pair get no seeker and no lateral authority, so
they fly as rockets), and deletes the three migrated fields before anything reads the
stats. `missile-presenter` deletes `GuidanceCurve` and `ThrustCurve` once the presenter stops
reading them. Together they close follow-up `launcher-flight-fields`.

**A missile is still its pending shot.** A shot is a munition when its weapon data is
`LauncherData` or `GuidedWeaponData` (`Munitions.IsMunition`, the one test). Its identity is
its `ShotId`; its flight state is one inline struct on the shot, `MunitionFlight`: planar
`Position` and `Velocity`, `DeltaV` remaining, `Command` (the last step's commanded
acceleration; the presenter's throttle is its length over `Acceleration`, and its sideways part
is the munition's acceleration as a target), `Wear`, its `Seeker` subject (an
`Entity`, null when it has none), `Locked`, and the source's faction frozen at fire. No second
list, dictionary or class of missiles exists beside `Zone.PendingShots`. This is still the
asteroid shape: a light record with a little state, simulated where the simulation lives, no
Entity, gear or agent behind it.

**Launch.** `Fire` freezes, as today, the launch data the shooter's own sensors gave it
(`FireTargetPosition`, `FireTargetVelocity`; a `LauncherData` round still needs the
launcher's lock, M17). The flight starts at the fire origin with the shooter's velocity plus
`weapon.Velocity` along the mount (`LaunchDirection`), full `DeltaV`, and the shot's target as
its seeker subject, unlocked (none for a round fired along the aim, whose launch data is
the aim point or burst position). A munition shot's `CommitTime` is infinite until its
terminal gate opens; `ArrivalTime` is `FireTime + Control.Lifetime`, its self-destruct time,
until the gate replaces it.

**The step.** `FireControl.Step` advances each live munition once per tick, before its
resolution tests, through one function `Munitions.Advance(Zone zone, ref PendingShot shot, float dt)`:

1. *Seek.* The seeker's reading of its subject is the settled sensor value (M16) with one
   sensor: `Sensor.Gain(subject.Visibility, Sensitivity, cone(angle off the velocity),
   distance, 1)` over `TargetInfoDecay`, saturated; zero beyond `Sensors.Range` or outside
   `ConeHalfAngle`. `Locked` while it exceeds `TargetDetectionInfoThreshold`. Nothing else is
   stored. The settled-value arithmetic moves out of `Entity.ChunkInfo` into one function
   both call.
2. *Aim point.* Locked: the subject's live position and velocity (terminal guidance on its
   own sensor). Unlocked: the launch track extrapolated, `FireTargetPosition +
   FireTargetVelocity * (now - FireTime)` (midcourse: it knows where it was sent). A missile
   in flight is told nothing else (ruling `shared-track-bearing-only`).
3. *Command.* Proportional navigation at the aim point: lateral acceleration `Navigation *
   closing speed * line-of-sight rate`, clamped to `Thrusters.Lateral`, plus along-track
   acceleration toward `MaxSpeed`, clamped to `Acceleration`. PN needs only the bearing and
   its rate, so terminal guidance uses no data the ruling forbids.
4. *Evade.* While PD rounds are inbound at it (`InboundRounds > 0`, which arrives with
   `munition-shots`, the cut that makes it shootable), it adds a jink of `Evasion * Lateral`
   whose sign follows a phase seeded from its shot id at `EvasionFrequency`, drawing only on
   fuel above its reserve (cognition, above). Evasion and pursuit share the clamp and the tank.
5. *Pay.* `DeltaV -= |command| * dt * (2 - Cognition)`. Before the gate a command that would
   take `DeltaV` below the reserve `Cognition * Lateral * CommitHorizon` is scaled down to what
   the reserve allows; after the gate the whole tank is usable. A dry missile commands nothing
   and coasts on its velocity (Nebulous).
6. *Move and gate.* Integrate position. `Munitions.Terminal` holds when the missile has a seeker
   subject and the time to its closest approach with that subject (both straight motions) is
   within `CommitHorizon` and before its lifetime ends. `missile-odds` opens the gate there
   (`CommitTime = now`, `ArrivalTime = now + time to go`) and the generic `Commit` rolls. Until
   `missile-odds` lands nothing opens the gate, and every missile ends at its lifetime.

**Resolution.** At the gate the generic `Commit` rolls the munition price with the per-shot
seed and publishes `ShotCommitted`: `Hit` or `Miss`. Between commit and arrival (at most
`CommitHorizon`) the flight presents the outcome and decides nothing: after a hit it closes on
the target's live position with whatever sideways acceleration the closure needs, without
debit; after a miss it commands nothing and coasts past. At `ArrivalTime` the generic `Apply`
resolves it. A hit detonates through `Detonate` at the hull entry point on the missile's line to
the target (`HullEntry`, M19), or at the target's position when the line finds no cell, with
radius `max(BlastRadius, SchematicCellSize / 2)`. A miss damages nothing: a fused missile's near
miss is the presenter's flash, not a blast, so the roll is the only thing that decides the
target's damage. A munition that reaches its lifetime without a gate resolves `Miss` at its
position (a fizzle). Interception keeps the first pass's rule at every stage, committed
included: wear at `Hull.Durability` resolves a fresh `Intercepted` outcome, no damage. A seeker
subject that leaves the zone before the gate makes the missile blind (retargeting, below); after
the gate it resolves a fresh `Miss`, the target-gone precedent (M4).

**Retargeting** is ruled: `missiles-seeker-retarget`, hostile-in-cone. A missile with no
subject, or one outside its cone, queries the targeting index every
`GameplaySettings.SeekerInterval` (0.5 s, Nebulous's re-selection period) for entities hostile
to the frozen faction, within `Sensors.Range` and the cone, and takes the brightest reading
above threshold. Turning invisible works through the same reading: a target whose visibility
falls below what the seeker needs is lost, and the missile seeks again. Decoys need something a
seeker can read that is not a hostile ship; no cut here makes one (follow-up
`missile-decoys-signature`).

**Seen and shot like a rock.** A munition's detectability uses the passive balance with its
own `Hull.Signature`; the index provider `MunitionTargets` offers every live munition at its
stepped position; `TargetRef` gains `MunitionId`; `Entity.SetTarget` refuses a munition. PD
rounds at a munition are priced and rolled by `FireControl` as the first pass said; lead now
reads the missile's real velocity, and the deviation term reads its real stepped position
against the frozen prediction, so jinking is what beats PD. As a target a munition takes the
shared `Evasion` term with its own agility (`Lateral` while it has delta-v) and the sideways part
of its last command as its acceleration, so a jinking missile is harder to hold and a dry one is
easy prey. The "round arrives too late"
gate reads `Munitions.TimeToGo` (distance to its aim point over closing speed) instead of a
fixed arrival.

**The presenter.** `GuidedProjectile` places itself at the stepped position (height from
`LiftCurve` over its life), faces the velocity, and drives its flame from the length of
`Command`, dark when dry. Between commit and arrival it follows the record's post-commit
flight. It plays a hit at a `Hit` (at the burst point), a kill at `Intercepted`, a near-miss
flash at a committed `Miss` and a fizzle at a lifetime `Miss`.

**Thresholded autofire.** A weapon group can be set to autofire with a threshold (default
`AgentMinHitProbability`, 0.2). Each tick, before behaviours, `Autofire.Update` gives each
weapon of an autofire group a subject from `FireControl.ChooseSubject` and activates it
when one is chosen, deactivating it otherwise. `ChooseSubject` keeps the current subject while it is still worth firing, except that a
worth-firing hostile munition preempts an entity subject; otherwise it takes, in order, the most urgent detected
hostile munition in reach (least `Munitions.TimeToGo`), then the designated target, then the
detected hostile entity in reach with the highest price, each only if worth firing.
"Worth firing" is one function, `FireControl.Worth(weapon, shooter, subject, threshold)`:
the price against the threshold for an unfused weapon, designated and not refused for a
fused one. `AgentFires` becomes `Worth` at the agent threshold. `FireControl.SubjectOf(weapon,
shooter)` is the one answer to "what does this weapon fire at": its autofire subject in an
autofire group, else the entity's designated target. `Fire`, `Refuses`, `Solution`,
`ArcPermitsFire` and the stance gate read it.

**What makes a gun point defense** is ruled: `pd-who-engages`, any-gun ("anything can be
PD"). Nothing in data; any autofire gun can engage a munition, and the price decides: a
fast, tight, short-flight gun clears a threshold against a small fast target, a heavy gun
does not. Heat is the waste guard.

**Manual fire against autofire** is ruled: `controls-solution-subject`, designated ("manual
guns fire only on selected target when solution is available, autofire guns seek their own
firing solutions"). Manual fire is `controls-mount-aim` r2 unchanged (`SubjectOf` returns the
designated target); autofire's own choice is `ChooseSubject`. The trigger is ruled too:
`controls-solution-trigger`, group-trigger ("Group triggers only"): holding a group's trigger
fires each gun on its solution, else along the aim when the aim is in its arc, else holds.
A launcher fired along the aim with no solution launches at the aim point as its launch
data and its seeker may acquire what it finds there (HighFleet's blind fire).

**The AI.** Agents fire through the same chooser (`ai-autofire`): `Combat` and
`TurretController` treat every group as autofire at the agent threshold, with their chosen
target as the designated one. One owner of "which subject, and is it worth it" for player
and AI alike, and AI point defense against the player's missiles falls out of it. The
revision leaves `ai-autofire` r1 as it is: it reads only `ChooseSubject` and `Worth`.

**The HUD.** An autofire group's action-bar slot shows a filled mark and its threshold.
`ControlsHud` draws a small diamond on each detected hostile munition within the ship's
longest weapon range: filled while it can still manoeuvre, hollow once it coasts dry, ringed
while one of your autofire guns has it as subject.

### Authority map

- **Owner.** `FireControl` owns the one hit price for every shot, gun, beam or guided
  (`HitProbability`, `CommitProbability`, `Evasion`, `PDeviation`, `Agility`), every munition
  transition (fire, gate, commit, application, interception, lifetime, resolution) and every
  subject question (`SubjectOf`, `ChooseSubject`, `Worth`). `Munitions` owns the pure facts and
  the flight arithmetic (`IsMunition`, `Payload`, `Advance`, `TimeToGo`, `Terminal`, `Reserve`,
  the seeker reading); `FireControl.Step` is its only caller that writes a shot. `MunitionData`
  owns a missile type's stats, cognition included, until gear replaces each group.
  `Entity.Update` owns `Entity.Acceleration`. The player owns which groups are autofire and
  their thresholds.
- **Inputs.** The frozen launch data, the flight state, zone time and dt, the seeker subject's
  live position, velocity, acceleration and visibility, the munition's stats, the count of PD
  rounds inbound; the weapon's stats and the shooter's sensors, accuracy and tracking; the
  target's strafe thrust and mass; `Entity.Autofire`.
- **Outputs.** Munition positions, velocities and commands for the index, the HUD and the
  presenter; `Hit`, `Miss` and `Intercepted` outcomes; blasts through `Detonate`; prices that
  carry the tracking term for the HUD, the AI and autofire; weapon activation for autofire
  groups; the saved autofire settings.
- **Derived state.** Seeker reading (every step, never stored beyond `Locked`), time to go, the
  terminal fact, reserve, zero-effort miss, correction, agility, evasion, throttle (from
  `Command`), detectability, the index's munition region.
- **Demotions.** The first pass's derived pose is gone: a munition's position is stepped state,
  written only in `Munitions.Advance` under `FireControl.Step`. The second pass's geometric fuze
  never lands: the flight decides only whether and with what a missile reaches its gate; the
  roll at the gate decides contact, and the post-commit flight only presents it. Flight time is
  no longer the only way a manoeuvre reaches a price: the tracking term reaches every shot.
  `GuidedProjectile` owns only how a round looks. `Thrust`, `MissileVelocity`, `DodgeFrequency`,
  `GuidanceCurve` and `ThrustCurve` no longer exist as loose launcher fields. `Entity.Target` is
  the designated target, the subject of manual fire only. `PendingShot.Target` derives from
  `Subject`.
- **Forbidden writers.** Any write of a `MunitionFlight` field outside `Munitions.Advance` and
  `Fire`, except `Wear` (only `FireControl.DamageMunition`) and `InboundRounds` (only
  `FireControl`'s shot transitions). Any contact, damage or dice in `Munitions`. A commit point
  for a munition other than its terminal gate; a second price for guided rounds beside
  `CommitProbability`'s munition branch. An evasion, agility or deviation-shape formula outside
  `FireControl.Evasion`, `Agility` and `PDeviation`. Any write of `Entity.Acceleration` outside
  `Entity.Update`. Homing, intercept or guidance arithmetic in `Gameplay` or `UI`. A copy of
  warhead fields inside `MunitionData`. A global setting standing in for a per-missile stat. Any
  read of `Entity.Target` as a weapon's subject outside `SubjectOf`. A second worth-it test
  beside `Worth`. A second record type, list or dictionary of missiles beside
  `Zone.PendingShots`.
- **Shared paths.** Guns, beams, instant weapons, PD rounds at munitions and guided rounds all
  price through `PDeviation` and `Evasion`, commit through `Commit` and apply through `Apply`.
  Missile hits, mines and fused rounds all damage through `Detonate`. The seeker and
  `Entity.ChunkInfo` read the same settled-sensor function over `Sensor.Gain`. Player autofire,
  `Combat` and `TurretController` choose through `ChooseSubject`; the presenter, the HUD and the
  index read the same flight state.
- **Deletion line.** Before new behaviour: the loose launcher flight fields go in
  `missile-stats` (migrated, then deleted); `DeviationProbability`'s inline
  `saturate(1 - deviation / Tracking)` becomes `PDeviation` in `evasion-term` before the term is
  added; `GuidedProjectile`'s homing, dodge and split code and the two curves go in
  `missile-presenter`; the stored `PendingShot.Target` field becomes `Subject` in
  `munition-shots`.

### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Munition (light record) | Its `PendingShot`'s `ShotId`, as `MunitionId` in a `TargetRef`. | From fire through its terminal gate (commit) to arrival, or until interception or lifetime; never saved, as pending shots are not. | `FireControl` for transitions and the roll; `Munitions.Advance` for flight. |
| Munition flight | `PendingShot.Flight` (position, velocity, delta-v, command, wear, seeker, lock, faction, inbound rounds). | Stepped every tick; gone with its shot. | `Munitions.Advance` under `FireControl.Step`; wear by `DamageMunition`. |
| Entity acceleration | `Entity.Acceleration`. | Written every update from the velocity change; never saved. | `Entity.Update`. |
| Missile stats | `MunitionData` on `LauncherData` / `GuidedWeaponData`, six groups. | Catalog data in `Aetheria.cc`. | The catalog now; gear once munitions are entities. |
| Autofire setting | Per weapon group, `Entity.Autofire` (on, threshold). | Set by the player; saved in `EntityPack` key 21. | The player. |
| Autofire subject | Per weapon, runtime only. | Re-picked by `Autofire.Update` when the current one stops being worth firing. | `FireControl.ChooseSubject`. |

### Rationale

**Why stepping now.** The first pass refused stepping because a stepped missile would hold
a second clock of arrival beside `FireControl`'s frozen timing. Ruling
`missiles-fuel-and-seekers` makes the outcome depend on what happens in flight, which no
frozen time can know; so the frozen timing and the roll leave guided rounds and the flight
becomes their one clock. Stepping costs a few dozen operations per missile per tick, pinned
by measurement in `missile-records`.

**Why PN on a bearing.** Ruling `shared-track-bearing-only` lets a missile in flight hold a
bearing and nothing more. Proportional navigation is the textbook guidance law and needs
only the line-of-sight angle and its rate, so the law and the ruling agree, and the
textbook's failure modes (a late hard break, burn-and-coast against augmented PN, low energy
at long range) become the dodging the operator asked for.

**Why odds at one gate.** Ruling `missiles-resolution-model` reverses the second pass's
geometry-only recommendation, and the reason is the operator's: at this simulation's scale a
fuze is not physics, it is a coarse proxy that throws away terminal fuel, seeker data and
cognition. The second pass's objection to rolls (flight and dice answering one question twice)
is met by placing the roll where guns place theirs, at a commit before arrival, and making the
flight after it presentation. The flight still matters: it is what spends fuel, loses seekers
and builds the miss the price reads. The entity cut keeps this shape: gear replaces the stats
the price reads, not the price.

**Why evasion is a tracking term.** Ruling `evasion-not-only-flight-time`: in-flight deviation
only charges a dodge the round's flight time allows, so a laser cannot be dodged (M20). A
tracking term charges the target's current sideways motion against the shooter's `Tracking`
whatever the weapon, in the shape EVE's turrets use, and adds to the in-flight deviation, which
still rewards a dodge made while a slow round flies. Capping speed by what the hull could
reverse in the window keeps it a stat: a heavy hull cannot buy evasion by drifting.

**Why reactive evasion.** The operator's sentence is "a missile that spends fuel evading
PD". Evasion triggered by inbound rounds makes PD fire cost the missile fuel even when it
misses, so a saturating salvo and a well-placed PD group both change what arrives. A
constant terminal weave (Nebulous) would charge every missile the same and make PD
irrelevant to the fuel story.

**Why stats per missile type, in gear-shaped groups.** Ruling `missile-stats-now-gear-later`.
The first pass's global durability, radius and visibility are replaced. Each group is what
one gear category will supply, so the entity cut swaps a group's source and nothing that
reads `Munitions` changes.

**Why the player cannot designate a missile.** Presses that land on a swarm of rounds
instead of the ship behind them are a classic irritation, and the operator asked for
autofire precisely so that PD needs no micro.

**Cut order.** `missile-stats` (r2) first: the stat object with `Cognition` and the catalog
migration, no behaviour change. `evasion-term` (r1) needs nothing and may run beside it: the
tracking term in every gun price. `missile-records` (r3) after `missile-stats`: flight, seeker,
reacquisition, cognition's efficiency and reserve, the terminal fact, lifetime, interception,
the provider and the cost pin, headless; missiles fly but never hit. `missile-odds` (r1) after
`missile-records` and `evasion-term`: the gate, the price, the commit, the post-commit flight and
the blast. `missile-presenter` (r2) after `missile-odds` and `controls-helm`. `munition-shots`
(r3) after `missile-odds` and `controls-mount-aim`. `autofire-threshold` (r3) after it and
`controls-hud`; `ai-autofire` r1 last, after `controls-ai-bearing`. Splitting the old
`missile-records` keeps each Hands pass inside its budget: the flight alone sat near the
ceiling, and the price is a second subject with its own tests.

**Left for later.** Full munition entities (follow-up `drones-munitions-substrate`), whose gear
replaces `MunitionData` group by group; midcourse bearing updates from the launcher over a
datalink (ruling `shared-track-bearing-only` allows a bearing; nothing sends one yet); seeker
kinds, decoys (follow-up `missile-decoys-signature`: a decoy needs a signature the seeker's
reading can take), jamming and soft kill; boost and midcourse fuel planning; mines as records
(`bodies-entity-and-mines`); whether launchers draw rounds (`launcher-ammunition`). Whether
the demo cast fields launchers for the player's PD to meet is content: the cuts add a
`Point Defense` scenario for the operator check.
