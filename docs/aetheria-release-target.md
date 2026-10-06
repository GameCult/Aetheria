# Aetheria Release Target: the Demo

Date: 2026-10-03, re-scoped 2026-10-06

Status: target. This document owns the ends. The typed `target` in the Eureka mind
(campaign `aetheria-release`) owns the labelled invariants and the not-in-scope list;
this file gives their reasons. Cut specs, questions and rulings are typed documents in
the mind and are not restated here. Body facts, the model page, the scope sort and the
rationale for the cut order are in `docs/aetheria-release-map.md`.

## Why

The game has deep systems: fire control, stats and power, heat, item provenance,
locomotion, scenarios and the targeting index. What keeps it unreleasable is that a
player has nothing to do between minute five and minute thirty, and nothing they do
is seen or heard. It plays no sound. A hit on a hull shows nothing where it lands.
Loot cannot be sold, damage cannot be repaired, no boss guards anything, and reaching
the exit does nothing. Every NPC fights the same way.

The first release is a demo, and it is called one. The full game, Terminus with three
procedurally generated regions, needs enough faction variety to give each region its
own challenges and stories. Until that content exists the game is not presented as
complete.

## Operator direction

- 2026-10-06 (ruling `first-release-is-a-demo`): "Full Terminus requires enough
  faction variety to present unique challenges and stories across three regions, I
  can't call it anything more than a demo if we don't have enough content to
  populate the three gates. I'm fine with reducing scope so we can get to a release
  on time, but I will not pretend it is the whole game."
- 2026-10-06 (ruling `demo-cut-fixed-region`): the demo is one gate, one region and
  one boss with a fixed faction presence. Full Terminus keeps procedural regions,
  each with its own political and economic setup.
- 2026-10-06 (ruling `demo-cast`): one allied faction, the protagonist's ostensible
  employer under Aetheria's usual polite coercion; one opposing faction, which
  decides the boss; the rest neutral. Allied: the Pirates. Antagonist and boss:
  Zhestokost. Featured neutrals: Lucent Media for spectacle, Aeronautics Unlimited
  as the vanilla that balances the other flavours.
- 2026-10-06 (ruling `miss-terris-belongs-to-emily-r3`): Miss Terri's is Emily's
  faction. Its role in the prelude stands. Agents write no lore, explanations,
  faction play, doctrine, barks or ship concepts for it, and build on none of the
  AI-written material already made for it.
- 2026-10-06 (ruling `ship-authors-need-no-unity`): a person adding a ship works in
  Blender and the CultLib tools and never installs Unity or syncs the repo.
- 2026-10-02 and 2026-10-03 (ruling `operator-release-direction`): the release needs
  audio, ships, stations and gear to covet and collect, and mechanics that are
  visible and visceral. The announcement bar names a wider selection of ships and
  gear, a UX polish pass, and audio feedback throughout.

## What "the demo" means

The demo is a public build, announced to players and Patreon, with a scenario named
"Terminus (Demo)". It is done when every line below holds on one Windows build made
from `master`, and the operator has played that build and called it. A ticked list
without the operator's call is not a release.

1. **One run, start to finish.** New Game starts the demo in a hull made by the
   Pirates, at a station near the entrance of one region with a fixed cast: the
   Pirates allied, Zhestokost hostile, Lucent and AU neutral. The Zhestokost home
   zone holds the region's one gate, guarded by its boss. The gate stays sealed
   while the boss lives. Passing it after the boss dies ends the run as won. Death
   ends the run as lost.
2. **The run pays.** At a docked station the player can buy, sell what they looted
   and repair what they lost. Credits persist across save and Continue.
3. **The four factions fight differently.** Each cast faction has a doctrine a
   playtester can describe without being told, from the Faction Play design in
   AetheriaLore.
4. **Every player action that changes the world makes a sound.** Weapon fire and
   charging, impacts on shield and hull, ship destruction, docking and undocking,
   the wormhole and the gate, thrusters and the drive, inventory and trade actions,
   menu clicks, the overheat alarm, and music in the menu, overworld, combat and the
   boss fight. Master, music, effects and interface volume are player settings that
   persist.
5. **Every hit is seen where it lands and felt by the one who takes it.** An impact
   on a shield shows on the shield at the side it came from; an impact on a hull
   shows at the struck point; a hit on the player moves the camera in proportion to
   the damage. Shake can go to zero and the hit is still shown.
6. **Damage, heat and exposure are visible before they are fatal.** Lost armour, a
   destroyed item and a critical hull look different on the ship and in the HUD.
   Overheat and hull-critical warnings use sound plus a visual, and the more urgent
   one wins. The player can see how visible they are and the largest source of it.
7. **There is enough to covet in the demo.** Each cast faction flies a hull of its
   own and Zhestokost's boss flies its boss hull, each entering through the
   first-party package path. The exact count is question `demo-content-bar`. Rarity
   tiers show their colours, and every weapon type sounds and looks distinct.
8. **A new player can play the demo through on the Windows build without hitting a
   blocking defect**, published through the build-delivery path.

## Ends

- **The simulation states what happened, and presentation shows it.** Fire control,
  shields, damage, heat, detection, the boss and the gate publish typed facts.
  Visual and audio presenters derive a cue from each and render it. They decide no
  rule and recompute no geometry.
- **One cue, two channels.** Each fact becomes a cue in one engine-free function;
  the visual and the audio presenter render that same cue.
- **The run is a scenario.** The demo is a compiled scenario script beside the
  tutorial and main galaxies. Its cast, starting standing and gate are written in
  that script; placement stays with the galaxy generator.
- **New content is data, authored without Unity.** A new hull, station or piece of
  gear arrives as a catalog record or a first-party package that carries its own
  product. Nobody hand-wires a prefab array, and no author needs Unity or the repo.
- **The player controls intensity without losing the signal.** Turning shake,
  flashes, particles or any audio channel down never removes the tactical
  information it carried.

## Invariants that must survive

The typed target carries these as labelled statements. The reasons:

- **Ship the game** (operator, 2026-09-11; reframed 2026-10-06). The goal is a shipped
  game, not new foundations for game development. AetheriaThing was stopped because
  reinventing the foundations alongside the game grew into an unmaintainable pile of
  abstraction. So work lands in the existing Unity tree, which is the vehicle and not a
  value in itself: no daemon, CultMesh or Eve re-architecture here, and shared
  infrastructure is adopted, never invented.
- **The simulation owns the facts.** Fire control was rebuilt so that no Unity
  collider or effect decides a hit. Feedback reads `FireControl`'s facts and never
  re-derives them. The same holds for the run: whether the boss lives and whether
  the gate is open are decided in `ServerShared`.
- **The simulation names no sound.** Audio is a presenter of the same semantic
  events visual effects use.
- **State is CultCache.** Player preferences go in the player store, authored data in
  the catalog, run state (credits included) in the run store. Presentation tuning
  stays on its Unity component (fork M (a) of `docs/settings-globals-cut.md`).
- **Instruments bound what is shown.** A cue never shows the player more than their
  sensors know.
- **The demo is named a demo.** Release copy, store text, the scenario name and
  these docs call it a demo.
- **Miss Terri's is not AI-written.** No agent writes material for it.
- **Ship authors need no Unity.**
- **The lanes are respected.** This campaign never commits to a `codex/*` branch or
  the operator's checkout.
- **Rules are tested headless.** Every rule decided in `ServerShared` has a
  behavioural test in `tests/Aetheria.Shared.Tests`.
- **The operator judges whether the bar is met.**

## Not in scope for the demo

- Full Terminus: three regions, procedural regional politics and economy, faction
  selection per run, and the full content bar. These come after the demo.
- Factions outside the cast as flags. They stay brands on shelves and generated
  traffic where the generator already puts them.
- Mining yield and content (mining Cuts 4, 5 and 7), the sublinear targeting index
  (ruling `targeting-sublinear-wanted-not-critical`), and belt rendering through the
  index.
- The faction-play tender loop (`faction-play-4`), drones and munitions, the
  Colosseum, articulated mounts.
- Narrative: storylets, Ghostlight, Ink beats per boss.
- Volumetrics and the nebula backport.
- Asura and procedural planet content.
- New mechanics beyond what the demo's rulings name.
- Build publishing machinery (`docs/build-delivery-target.md` owns it).
- Platforms other than Windows 64-bit, and multiplayer.

Open questions decide where four more items fall: the material pipeline
(`demo-material-scope`), the all-in-`.cc` ship move (`demo-ship-cc-timing`), the
control rebuild (`demo-controls-rebuild`) and which modes a release build lists
(`demo-build-scenario-list`).

## Strands

- **The run.** Three cuts: `demo-galaxy` (the scenario, its cast, the gate's
  placement), `boss-gate` (the boss and the sealed gate, the win), and
  `station-services` (sell, repair, saved credits). The on-ramp is follow-up
  `demo-difficulty-onramp`, after a played build.
- **Feedback.** `feedback-1`: impacts with a world point and a layer, shown on the
  shield or hull and in the camera.
- **Audio.** `audio-1`, native Unity audio (ruling `audio-native-unity-plus-faust`).
  Its sequel covers interface, trade, docking, the wormhole and gate, music
  (including the boss), loops and the overheat alarm.
- **Faction play.** `faction-play-1` to `-3`, re-anchored on post-merge master and
  revised for the demo cast with the Pirates' doctrine (follow-up
  `faction-play-reanchor-demo`). The Pirates need a catalog record first (follow-up
  `pirates-catalog-record`).
- **Loot.** `loot-1` to `-3`: floating items are simulation state and pickup is a
  capability. With `station-services`, loot becomes money.
- **Content.** The package path that landed with moddable-ships, then the add-on's
  frame and mounts cuts, then hulls for the cast and the boss (follow-ups
  `demo-cast-hulls`, `zhestokost-boss-hull`), authored at the operator's pace.
- **UX polish.** A ledger of small fixes verified through scenarios (follow-up
  `demo-ux-ledger`).

## Sequencing

The run goes first, because it is the hole every other strand fills: without it the
demo has no thirty minutes. `demo-galaxy` and `station-services` are independent
and can run in parallel; `boss-gate` follows `demo-galaxy`. Feedback and audio follow
the order already mapped (`feedback-1`, then `audio-1`). Faction play waits for its
re-anchor. Content runs in parallel at the operator's pace, because the hull clock
starts only when the first demo hull flies.

## Lanes and collisions

The hot files are `Entity.cs`, `FireControl.cs`, `ActionGameManager.cs`,
`ZoneGenerator.cs`, `ZoneRenderer.cs` and `TradeMenu.cs`. Each cut branches from
`origin/master` in its own worktree and keeps its hunks out of the regions other open
cuts change, which are listed per spec. New behaviour goes in new files wherever an
existing owner allows it.
