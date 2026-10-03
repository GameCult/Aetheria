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
and `hull-livery-scope`.

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
3. Cleanup and paint, the variable steps: see "Hull materials, mount meshes and
   per-hull prep" below (prep steps P3 to P5).
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

Two operator rulings of 2026-10-03 drive this section.
- `hull-runtime-materials`: "I expect I'll want to do quite some cleanup and prep
  for the meshes. The geometry is still not ideal, and we want more runtime
  material control than just a single baked map. See how MechWarrior does it, for
  example."
- `thrusters-radiators-are-meshes`: "Note that both thrusters and radiators should
  be meshes, the former doesn't get rendered but is used as an emission surface".

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

**M14. glTFast keeps meshes readable and passes vertex colours through unchanged.**
Read from the package source (`com.unity.cloud.gltfast@11ddc2436f97`):
- `MeshGenerator.cs:539` calls `UploadMeshData(false)`, so meshes stay readable.
  The collider (`ShipModShips.cs:103`) and a particle mesh shape need that, so
  M10's readability doubt is closed for the importer. `ships-player` still checks
  it in a built player.
- `VertexBufferColors.cs:25,88` stores `COLOR_0` as `float4` with no colour-space
  conversion. The project is in Linear colour space (`m_ActiveColorSpace: 1`).
- `IMaterialGenerator.GenerateMaterial(MaterialBase, IGltfReadable, bool)` sees
  every core slot: `PbrMetallicRoughness.BaseColorTexture` and
  `MetallicRoughnessTexture`, `NormalTexture`, `OcclusionTexture`,
  `EmissiveTexture`, each with `index` and `texCoord` (`Schema/Material.cs:48-77`,
  `TextureInfo.cs:42-49`). `IGltfReadable.GetTexture(int)` returns the texture.

**M15. Blender exports a named colour attribute exactly.** Probe, Blender 5.2.2,
`--background --factory-startup`:
- Setup: a quad with a `FLOAT_COLOR` corner attribute `aetheria.paint` set to
  `(1, 0.5, 0.25, a)`, with `a` = 0, 0.5, 1, 0.5.
- Export: `export_vertex_color='NAME', export_vertex_color_name='aetheria.paint',
  export_all_vertex_colors=False`.
- Result: one `COLOR_0`, unsigned-short normalised `VEC4`, values exact to 1e-5,
  alpha kept.
- With `export_vertex_color='ACTIVE'`, the exporter wrote every colour attribute
  (`COLOR_0` and `COLOR_1`), and `COLOR_0` was all ones. The add-on must name the
  attribute.

**M16. The Tripo hulls, measured.** A read-only probe of a copy of `Quiet.blend`:

| Hull | Triangles | Face-connected parts | Largest part's share of faces | Parts under 2% of hull length | Non-manifold edges | UV islands |
|---|---|---|---|---|---|---|
| Headliner | 50,008 | 189 | 33% | 23 (647 faces) | 6,145 | 6,237 |
| Dexter Quiet | 9,614 | 12 | 87% | 8 (124 faces) | 524 | 1,386 |
| Sinister Quiet | 10,880 | 12 | 88% | 7 (114 faces) | 356 | 1,525 |

- Headliner is a kitbash. 124 of its parts are 2 to 10% of its length, so they are
  greebles, not debris. Deleting small parts automatically would strip its
  detail.
- The UV atlases are confetti: 1,400 to 6,200 islands in one 4096² image. A mask
  painted or filtered in that texture space is unusable, and a clean texture mask
  would need a fresh unwrap and a rebake per hull.
- K-means (CIELAB, a 512² sample) of the albedos:
  - Headliner's is colourful. With k = 6, the clusters are dark navy (43% of the
    texels), mid grey (21%), beige (13%), lavender (11%), orange (7%) and blue
    (4%).
  - Quiet's is near-greyscale (a* and b* within ±6): light panels (44% at L 69 to
    79), darks and teal accents.
  - Within-cluster lightness spread is 3 to 12 L. That spread is the baked shading
    and detail a tint must keep.
- Probe scripts: `kmeans.py`, `meshstats.py`, `parts.py`, `vcol.py` in this pass's
  scratch. Their numbers are quoted here.

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
- The add-on side is still the r1 spec: a disc and a panel at the 3D cursor.

#### The hull material model

The model buys MechWarrior and Warframe-class control with the fewest new
surfaces:
- three paint colours chosen at runtime;
- a finish per paint;
- wear on edges and grime in cavities;
- emissive lights;
- glowing radiators;
- livery from the faction, with player paint and patterns as a later step.

It is written for the recommended option of question `hull-paint-mask`.

**What a package carries.**
- **The GLB's core PBR slots:**
  - base colour (Tripo's albedo);
  - metallic-roughness;
  - normal;
  - occlusion;
  - emissive texture and factor.

  Whatever Tripo or the operator's bake provides is used, and missing slots fall
  back to factors.
- **`COLOR_0` on the hull meshes, the paint mask.**
  - RGB is the weight of the primary, secondary and accent paint. Black is bare:
    the GLB's own surface shows.
  - Alpha is signed convexity: 0.5 is flat, toward 1 on convex edges (where paint
    wears off) and toward 0 in cavities (where grime collects).
  - RGB is authored per face in Blender. Alpha is derived at Package.
- **Blender material slots become glTF materials.** These are surface kinds, such
  as bare metal, glass and lights, each with its own factors. A light is a material
  with an emissive factor. No texture is needed.
- **`ShipAuthoring.Paint` (key 5, a `HullPaint`), typed in the package.**
  - The factory `Livery`: three colours, a metallic and smoothness per slot, wear
    and grime.
  - `RegionLuminance`: each region's mean albedo luminance, derived at Package.
  - Null means an unpainted hull. It renders with the GLB's look, and still fades
    and is lit like a prefab hull.

**What the game owns.**
- One shader, `Aetheria/Hull`, built on `GlowFade`'s fade, dither and volumetric
  ambient. Its rules:
  - Painted albedo is the slot colour × saturate(lum(base) / region luminance).
    The factory livery, whose colours are the region means, therefore reproduces
    the original look. A new colour keeps the original's panel lines and shading.
  - Wear: where alpha is above 0.5 and a tiling noise is under `_Wear`, paint
    chips to the GLB's surface.
  - Grime: below 0.5, `_Grime` darkens and roughens.
  - Painted metallic and smoothness come from the slot's finish.
  - It has no `shader_feature` keywords. Its only variants are `GlowFade`'s
    `multi_compile` set, so build stripping cannot drop one.
- `HullMaterialGenerator`, a glTFast `IMaterialGenerator`, builds every hull
  material as a clone of `ShipModTemplate.Hull` with the GLB's slots. No glTF
  shader is needed at runtime, so `ships-player` r1's variant collection is
  deleted.
- `ShipModShips.ApplyPaint` sets the resolved livery on each hull renderer through
  a `MaterialPropertyBlock` in `ShipInstance.SetEntity`. Property blocks do not
  survive `Instantiate`, so a prototype cannot carry them.
- Livery resolution, as one pure function, `HullPaint.Resolve`: the owner
  faction's `Livery` (`Faction` key 16, cut `ships-hull-livery`) when set, else
  the hull's factory livery. Region luminance is always the hull's own.

**Role materials and the two mount meshes** (ruling
`thrusters-radiators-are-meshes`):
- **Thruster:** a mesh carrying the invisible material (done in `ships-mounts`). It
  is the exhaust's emission shape, and its normals point aft.
- **Radiator:** a mesh rendered with the template's `Radiator` material.
  `EntityInstance` drives its `_Emission` with temperature (`ships-player`).
- **Weapons** stay point anchors.
- **Map icon and collider:** as before.

**Out of release scope, as follow-ups:**
- `hull-decals`: insignia and damage decals.
- `hull-damage-wear`: wear driven by hull durability. The feedback strand owns it.
- `hull-player-paint`: player paint and patterns, unless question
  `hull-livery-scope` rules otherwise.

#### What the operator authors per hull, and what is derived

| Step | Operator authors | Derived automatically |
|---|---|---|
| P1 Tripo export | One test regeneration with PBR on (below) | — |
| P2 New Ship | id, name, reference hull, length | orientation, scale, draft grid, `create --like` |
| P3 Clean | which selected debris to delete; decimation if warned; normals fixed where wrong | debris and enclosed-part selection, mesh report (tris, parts, non-manifold edges) |
| P4 Paint regions | each cluster's role; face fixes with Primary, Secondary, Accent and Bare; livery colour tweaks | face clusters from the albedo, default roles, factory colours = cluster means |
| P5 Surface kinds | material slots for glass and lights, with their factors | — |
| P6 Mounts | the faces for each thruster and radiator, the 3D cursor for each gun | emitter normals turned aft, hardpoint rows on the cells beneath |
| P7 Layout | internal hardpoints, cell touch-ups | — |
| P8 Package | — | recentring, collider, map icon, shield, tractor, wear and grime alpha, region luminance, texture cap, GLB, records, validation |
| P9 Look | dock, `give`, fly | — |

**Rejected for the release path: retopology and rebaking.**
- Decimation keeps Tripo's UVs, and the paint mask lives on faces, so no step needs
  a new unwrap.
- A retopologised hull baked Selected to Active (Blender manual, Cycles baking)
  stays available for a hull whose silhouette or shading needs it.
- The vendor claims for that path are 2 to 4 hours a hull (survey §4).

**Time budget per hull.** These are estimates, not measurements. The operator
checks in `ships-addon-frame`, `ships-addon-mounts` and `ships-addon-paint` record
the real numbers.

| Step | Quiet-class (10k triangles, 12 parts) | Headliner-class (50k, 189 parts) |
|---|---|---|
| P2 | 2 min | 2 min |
| P3 | 10 min | 30 to 45 min |
| P4 | 15 min | 30 min |
| P5 | 5 min | 10 min |
| P6 and P7 | 15 min | 20 min |
| P8 and P9 | 10 min | 10 min |
| **Total** | **about 1 hour** | **about 1.5 to 2 hours** |

Ten package hulls (`content-bar-one-per-concept`) cost about 12 to 18 hours of
operator time with the tooling. The add-on cuts own what shrinks it:
- debris selection (`ships-addon-frame`);
- face-based mounts (`ships-addon-mounts`);
- clustering, roles and derived wear (`ships-addon-paint`).

**P1, the Tripo export check: a step for the operator to try.**
- Tripo's API defaults to `pbr=true`, which yields base colour, metallic,
  roughness and normal maps. Texture version `v3.5-20260815` adds `delight`
  (default true), which removes baked lighting.
- The hulls in `Quiet.blend` arrived with base colour only, and why is unknown.
- Suggested try: regenerate one hull (Dexter Quiet) on the site and through the
  API, with PBR on and delight on, then import the GLB into Blender.
  - If the metallic-roughness and normal maps are present, the generator uses them
    with no further work.
  - A delit albedo also makes the region-luminance tint cleaner.
  - If the site only exports base colour, the API is the route for the release
    hulls.
- Nothing in the cuts waits on this.

#### Cuts

- **`ships-hull-material`** (new): `Aetheria/Hull`, `HullMaterialGenerator`,
  `HullPaint` and `Livery`, `ShipAuthoring.Paint`, the `COLOR_0` package rule and
  `ApplyPaint` with the factory livery.
- **`ships-hull-livery`** (new): `Faction.Livery` and its resolution. It waits on
  `hull-livery-scope`.
- **`ships-addon-paint`** (new): Suggest and Apply Regions, the role buttons,
  derived alpha and luminance, the named-attribute export and
  `ship-authoring paint`.
- **`ships-addon-package` r2:**
  - `validate` also reads the GLB (`ReadPackage`);
  - `Bind` refuses a thruster or radiator anchor whose node has no mesh.
- **`ships-addon-frame` r2:** Select Debris and the mesh report.
- **`ships-addon-mounts` r2:**
  - Add Thruster duplicates selected faces, with normals aft;
  - Add Radiator separates them;
  - either one can tag a selected mesh object;
  - no primitives at the cursor.
- **`ships-player` r2:**
  - the glTFast variant collection is deleted;
  - a no-`glTF/`-shader check;
  - every radiator submesh gets the radiator material;
  - an emitter readability probe;
  - it is no longer blocked (ruling `adopt-build-cut-1`).

Order: `ships-mounts`, then `ships-addon-package`, then `ships-addon-frame`, then
`ships-addon-mounts`. `ships-hull-material` can run beside the add-on cuts, after
`ships-mounts`. `ships-addon-paint` needs both lines; `ships-hull-livery` and
`ships-player` need `ships-hull-material`.

#### Model page rows

| Kind | Identity | Lifecycle | Authority |
|---|---|---|---|
| Hull paint | `ShipAuthoring.Paint` in the package | Factory livery authored in Blender's panel; region luminance re-derived every Package | `ship-authoring paint` writes it; `ShipAuthoringStore.Validate` judges it |
| Paint mask | `COLOR_0` on the GLB's unanchored meshes | RGB authored per face; alpha re-derived every Package | Blender's `aetheria.paint` attribute; `Bind` requires it when `Paint` is set |
| Faction livery | `Faction.Livery` in `Aetheria.cc` | Authored in the inspector; null keeps factory paint | The catalog |
| Hull material | One clone of `ShipModTemplate.Hull` per glTF material | Built at preload, cache only | `HullMaterialGenerator` |

#### Rationale

**Why vertex colours rather than a mask texture** (the recommendation in
`hull-paint-mask`):
- M16 settles it. A texture mask on Tripo's confetti atlases needs a new unwrap and
  rebake per hull, which is the costliest prep step there is.
- Hard-surface paint follows panels, and panels are faces, so face-level regions
  lose little.
- Star Citizen already carries wear in vertex alpha, and its tint palette is three
  colours (survey §2).
- A sidecar texture can be added later as a second source for one hull that needs
  stripes inside a face. That adds a field, not a rebuild.

**Why a generator instead of post-import material swaps.**
- glTFast calls the generator once per glTF material, with the slots already
  parsed.
- Swapping afterwards would leave glTF shaders as build inputs and would decide
  materials by renderer, which Assemble is forbidden to do by name.

**Why the tint divides by region luminance.** Warframe asks artists for about 50%
grey under tinted areas (survey §2). Tripo's albedo is not that: Headliner's navy
sits at L 16. Dividing by the region's own mean normalises any albedo without
rewriting the texture, and the factory livery round-trips to the original look.

**Why thrusters duplicate faces but radiators separate them.**
- A thruster's mesh is never drawn, so the hull must keep its nozzle.
- A radiator's faces are drawn with the radiator material. Leaving them in the hull
  would draw the surface twice and fight over depth.

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
| GLB | `ShipAuthoring.ModelAsset`, relative, in the package. | Re-exported by every Package. | Derived from the collection. The package directory supplies it to the game, and the game reads nothing else from there. |
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
`mining-target-queries`, `mining-merge`, `loot-1` to `loot-3`, `ballistic-ammo` and
`faction-play-4` r2. The fork is question `fastblast-ammo`.

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

**L3. The belt freeze's cause is mapped** (mining Cut 3 Soul F1):
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

**L5. Hands' unbuilt work in progress** is
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
| Belt band (provider region) | Belt key plus band index; arc segments by key order. | Derived from `AsteroidBeltData`, `PlanetSettings` and time. Re-keyed lazily, a bounded number per query. Cache only. | `BeltTargets`. |
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
3. **`mining-index`, then `mining-target-queries`**, on the mining branch. They touch
   only mining's own code, so they run in parallel with steps 1 and 2.
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
  answers meet it.
- Together the two are about 900 lines with tests, over one Hands budget. The seam
  between them, the index's query API, is where Soul can falsify each.

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
