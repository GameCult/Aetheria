# Aetheria Release Target

Date: 2026-10-03

Status: target. This document owns the ends. The typed `target` in the Eureka mind
(campaign `aetheria-release`) owns the labelled invariants and the not-in-scope list;
this file gives their reasons. Cut specs, questions and rulings are typed documents in
the mind and are not restated here. Body facts, the model page and the rationale for
the cut order are in `docs/aetheria-release-map.md`.

## Why

The game has enough systems to ship: fire control, stats and power, heat, item
provenance, locomotion, and the mining and scenario lanes in flight. What keeps it
unreleasable is presentation and breadth. It plays no sound at all. A hit on a hull
shows nothing where it lands, and the camera never reacts. Three hulls on two models
and one station are all a player can fly or dock at.

## Operator direction (2026-10-02 and 2026-10-03)

- "we already have plenty of game here, especially once we merge everything in flight
  (and maybe another campaign or two geared at making the world feel more alive). The
  only thing making it unreleasable to me is the lack of polish, content and feedback.
  We need audio, we need ships, stations and gear for the player to covet and collect,
  and we need our mechanics to be visible and visceral."
- "If we get the moddable ships pipeline usable and a Tripo3D subscription I can pump
  one of these out every day, game ready." The pipeline is GPT Image 2.5, then
  Tripo3D, then minor cleanup.
- "I don't think Asura is the way to go for content."
- The announcement bar (2026-10-02): a wider selection of ships and gear, a UX polish
  pass, and audio feedback throughout.

## What "release" means

The release is the public announcement to players and Patreon, with a build they
can download. It is done when every line below holds on one build made from
`master`, and the operator has played that build and called it. The operator's call
is part of the definition: a ticked list without it is not a release.

1. **The lanes in flight have landed.** `codex/mining`, `codex/scenarios` and
   `codex/moddable-ships` are merged into `master` or explicitly parked by the
   operator. A release does not ship around half-merged work.
2. **Every player action that changes the world makes a sound.** That covers weapon
   fire and charging, impacts on shield and hull, ship destruction, docking and
   undocking, the wormhole, thrusters and the drive, inventory and trade actions, menu
   clicks, the overheat alarm, and music in the menu, overworld and combat. The
   player can set master, music, effects and interface volume, and the settings
   persist.
3. **Every hit is seen where it lands and felt by the one who takes it.** An impact
   on a shield shows on the shield at the side it came from. An impact on a hull
   shows at the struck point. A hit on the player's own ship moves the camera in
   proportion to the damage. The player can turn shake down to zero, and the hit is
   still shown.
4. **Damage, heat and exposure are visible before they are fatal.** Lost armour,
   a destroyed item and a critical hull each look different on the ship and in the
   HUD. Overheat and hull-critical warnings use two channels, sound plus a visual,
   and the more urgent one wins. The player can see how visible they are, how close
   that is to being detected, and the largest source of it.
5. **There is enough to covet.** The content quota is set by the operator's ruling
   on question `release-content-bar`. Every hull and station in the quota is
   flyable or dockable and enters through the moddable-ships package path. Rarity
   tiers show their colours, and every weapon type sounds and looks distinct.
6. **A new player can play for thirty minutes on the Windows build without hitting
   a blocking defect.** The build is published through the build-delivery path.

## Ends

- **The simulation states what happened, and presentation shows it.** Fire control,
  shields, damage, heat and detection publish typed facts. Visual and audio
  presenters subscribe to them, derive a cue from each, and render it. They decide
  no rule and recompute no geometry.
- **One cue, two channels.** Each fact becomes a cue in one engine-free function.
  The visual and the audio presenter render that same cue, so the two channels
  cannot disagree about what happened or how hard.
- **New content is data.** A new hull, station or piece of gear arrives as a catalog
  record or a mod package. Nobody hand-wires a prefab array. The operator's daily
  ship goes through the image model, Tripo, cleanup and the Blender add-on, and
  comes out as a `ShipAuthoring` package with its GLB.
- **The player controls intensity without losing the signal.** Shake, flashes,
  particle density and each audio channel have settings. Turning any of them down
  never removes the tactical information it carried.

## Invariants that must survive

The typed target carries these as labelled statements. The reasons:

- **Legacy first** (operator, 2026-09-11). All work lands in the restored Unity tree.
  There is no daemon, CultMesh or Eve re-architecture here. Shared infrastructure
  is adopted, not invented.
- **The simulation owns the facts.** Fire control was rebuilt (Cuts 3 to 12.4(b)) so
  that no Unity collider or effect decides a hit. Feedback must not reopen that
  path. It reads `FireControl`'s facts and never re-derives them.
- **The simulation names no sound.** Today the simulation calls `FireAudioEvent`
  with Wwise ids, against a sound bank that is never assigned. Audio becomes a
  presenter of the same semantic events that visual effects use (`OnFire`,
  `OnStartCharging`, impacts and deaths).
- **State is CultCache.** Player preferences such as shake and volumes go in the
  player store (`PlayerSettings`), and authored data goes in the catalog. Engine
  asset references are Addressables GUIDs (ruling of 2026-09-17). Presentation
  tuning stays on its Unity component (fork M (a) of `docs/settings-globals-cut.md`).
- **Instruments bound what is shown** (`Visual and Sensory Direction`). A cue
  never shows the player more than their sensors know.
- **The lanes are respected.** This campaign never commits to a `codex/*` branch or
  to the operator's checkout. It branches from `origin/master` and rebases after
  each lane merges.
- **Rules are tested headless.** Every rule decided in `ServerShared` has a
  behavioural test in `tests/Aetheria.Shared.Tests`. A presentation-only behaviour
  is an operator check, reachable through ordinary play or a scenario.
- **The operator judges whether the bar is met.**

## Not in scope

- Asura and procedural planet content. The operator ruled it out for content.
- New mechanics. Contact tiers beyond the current threshold, fused tracks, thermal
  capture, probes and new weapon classes are not added here. This campaign makes
  the existing mechanics visible.
- Faction mechanics beyond what `Faction Play.md` and its rulings name.
- "World feels alive" work such as ambient traffic and events. The operator named
  it as a possible separate campaign.
- Build publishing machinery. `docs/build-delivery-target.md` owns it. This
  campaign needs it done before release, but does not build it.
- Platforms other than Windows 64-bit, multiplayer, narrative and Ink work.
- Finishing other lanes' cuts: mining, scenarios, moddable-ships S2 and S5, thermal
  balance, and the settings-globals tier-colour fix. This campaign depends on them
  and does not do them.

## Strands

- **Feedback.** Impacts, damage states, warnings and detection, made visible and
  felt. The first cut is mapped (`feedback-1`). It gives impacts a world point and a
  layer, then shows them on the shield or hull and in the camera.
- **Audio.** The route is open as question `audio-route`. The first cut is mapped
  for the recommended route (`audio-1`) and is blocked until the ruling. Its sketched
  sequel covers interface, inventory, trade, docking and wormhole sounds, then
  music, then continuous loops (thrusters, reactor, drive) and the overheat alarm,
  then AquaSynth renders for whatever the 2021 library lacks.
- **Content pipeline.** This strand is a sketch until `codex/moddable-ships` merges.
  1. The Blender add-on gains a collection-to-package action. It places anchors
     with `aetheria.id` extras for weapon, radiator and thruster mounts, the collider,
     map icon, shield and tractor, then exports the GLB with the `ship.cc`. This is
     proof gate 2 of `docs/moddable-ship-authoring.md`.
  2. Moddable-ships S3. The first playable mod ship is a Tripo mesh of a concept
     whose image already worked (the Headliner or the Quiet Sense), taken through
     the whole path.
  3. Repeatability. A convex collider from the render mesh, and one material-slot
     convention that leaves room for a later livery slot.
  4. Throughput. One hull a day from the operator, plus stations, then gear.
- **Faction Play.** The design is `Game Design/Faction Play.md` in AetheriaLore,
  at `5c45e27` plus `f3e0e6d`. It covers behaviour, relations, loadouts, resources,
  visuals and hooks. On 2026-10-03 the operator accepted every proposal in it, and
  the acceptance is recorded as three rulings:
  - `faction-relations-field`: relations are their own authored field, not derived
    from allegiance;
  - `faction-lore-changes`: the five lore changes;
  - `lucent-duel-bait`: Lucent's need for glory lets the player bait it into a
    duel.

  A fourth ruling, `npc-scripting-may-grow`, lets the strand extend the NPC
  scripting through its existing extension points. The note had limited itself to
  four agent states and the unread `Personality` field, and that limit is lifted.
  Legacy-first still forbids re-architecture. The map's section "Faction play: the
  NPC scripting" names what grows: a typed doctrine on `Faction`, and one flight per
  faction per zone that alone decides its members' targets and orders, executed by
  the existing state graph through two new motor states.

  The strand's first deliverable is the note's smallest proof: Zhestokost, Lucent
  and AU in one run, which a playtester can describe without being told. It is three
  cuts:
  1. doctrine, the flight, engagement with grace and hail, hull break-off and ping
     cadence (`faction-play-1`);
  2. pack slots and Lucent's duel (`faction-play-2`, after `feedback-1`);
  3. support ships, anchor and leash, and Zhestokost's rearm loop
     (`faction-play-3`).

  All three wait for the mining and scenarios lanes to merge. Relations, track
  sharing between factions, distress calls and target priority come after the
  proof. The feedback and content strands leave room for faction colour and
  livery, and decide nothing about them.

## Sequencing

Self proposed feedback, then audio, then content. Feedback goes first because it is
the cheapest per unit of perceived quality and it makes later content read. This
target keeps that order for agent-paced work, with one change: **content is the
calendar-critical path, so its unblocking runs in parallel rather than last.** The
quota is met one hull a day of operator time, and that clock cannot start until a
mesh flies. The steps that start it cost the agents little:

- the operator's owed S2 play checks on `codex/moddable-ships`;
- the merge;
- the add-on's package action.

Agent order: `feedback-1`. Then `audio-1`, once `audio-route` is ruled; it consumes
`feedback-1`'s impact cue. Then the content strand's add-on action as soon as
moddable-ships merges. After that, the remaining feedback and audio cuts alternate.

## Lanes and collisions

| Lane | Owner | This campaign |
|---|---|---|
| `codex/fire-control-12` (operator checkout, modified `.blend`) | Codex / operator | Never touched. Reads use `git show` or a scratch worktree. |
| `codex/mining` | Codex | Waits for its merge. `feedback-1` edits `FireControl.Apply`, `ApplyDirectHit` and `Detonate`, which mining does not change (it edits `FireControl.cs:72-100` and `:522-535`). Mining Cut 4 (shots at chunks) should publish impacts through the same contract (follow-up). |
| `codex/scenarios` | Codex, blocked on a fix batch | Waits. Operator checks in this campaign are reachable through ordinary play. Where one is not, it waits for scenarios Cut 4. |
| `codex/moddable-ships` | Codex, S2 owing operator checks | Waits for the merge. Ownership of anchor authoring and S3 after the merge is question `content-lane-owner`. |
| `eureka/aetheria-release-*` | This campaign | One branch per cut, from `origin/master`, each in its own worktree. |

The hot files are shared with the lanes: `Entity.cs`, `FireControl.cs`,
`ActionGameManager.cs`, `EntityInstance.cs` and `ZoneRenderer.cs`. Each cut keeps
its hunks out of the regions the open lanes change, which are listed per spec. New
behaviour goes in new files wherever an existing owner allows it.
