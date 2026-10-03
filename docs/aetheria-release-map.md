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
floating cargo to jettison into.

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
by doctrine and the flight's track choice. Jettison waits on question
`jettison-owner`.

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
