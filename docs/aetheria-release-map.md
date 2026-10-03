# Aetheria Release: Map

Date: 2026-10-03

This map holds body facts, the model page and rationale for campaign
`aetheria-release`. It has no progress section, no per-cut sections, no rulings
and no ledger: those are typed documents in the Eureka mind. Ends live in
`docs/aetheria-release-target.md`. The Eyes inventory this pass started from is
`F:\Projects\aetheria-release-inventory.md`, which is outside the repo.

Every anchor below is against `origin/master` `f1dee184` and was read in a sparse
scratch worktree on 2026-10-03, unless the line says otherwise.

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
| Ship content (`ShipAuthoring` with GLB) | `aetheria.ship_authoring` id. GLB nodes carry an `aetheria.id` extra. | Owned by the moddable-ships lane until it merges. | The moddable-ships rulings (MQ1-MQ5). This campaign adds no owner. |

No cell is empty for the two mapped cuts. The content rows defer to a lane this
campaign does not own until question `content-lane-owner` is ruled.

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
