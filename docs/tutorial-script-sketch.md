# Tutorial Script Sketch

Date: 2026-09-17

Status: sketch for the operator to react to. Nothing here is built or canon.

> **Authorship.** Threefra Scalarian, the sponsor's staged assessment, and the joke
> that the assessment form is fake while the attack is the real test all come from
> **Emily Harvey's 2020 tutorial script** (`AetheriaTexts.ctd`, Finch Cybernetics
> track). Her text is not reproduced or edited here.
>
> **Everything below is AI-extended material** written on top of her premise, with her
> permission to extend. It is not her writing and implies no involvement or
> endorsement from her. Her original script stays the reference for the character's
> voice.

## Shape

One contained tutorial run in a small prelude galaxy (`IsTutorial`,
`TutorialGenerationSettings`). The player is a newly incorporated subsidiary of the
protagonist faction. Threefra, a debt-born assessor with opinions, walks them through a
"qualification module" whose checklist is theatre. The real test is how the player
handles an ambush the sponsor stages with unmarked drones. The opener sells the
visceral part first, prop hunt, then teaches the verbs the player needs to have
survived it.

Beats are ordered for teaching, not for the old economy game. Economy verbs (mining,
towing, trade) appear only where Terminus has them.

## Director

A tutorial director drives NPCs only through existing agent primitives and advances beats
on **events, never polling**.

- **Owner:** the director, one object held by the run. It sets agent tasks, targets and
  stances, spawns entities, and starts Ink beats. It never moves a ship or fires a weapon
  directly.
- **Advances on:**
  - zone entity add and remove;
  - `VisibleEntities` add;
  - stance changes;
  - `Target` changes;
  - item destroyed and `Death`;
  - dock and undock;
  - pickup completed;
  - warp arrival;
  - dialog closed, or a dialog choice made.

Primitives, marked by whether they exist on `codex/item-provenance` today:

| Primitive | Status | Where |
|---|---|---|
| Spawn a ship or turret from a loadout preset | exists | `Loadouts.Materialize`, `LoadoutGenerator` |
| Give an NPC a `Minion` agent that patrols orbits | exists | `Zone.CreateAgent`, `PatrolOrbitsTask` |
| Set an NPC's target (drives `CombatState`) | exists | `Entity.Target` |
| Set a stance, with a sticky grudge on hostility | exists | `Entity.SetIff`, grudge subscriptions |
| Move to an orbit | exists | `MoveToOrbitState` (no task wires it to `Minion` yet) |
| Mining, survey, station towing, hauling tasks | data only | `Tasks/*.cs`; `Minion` handles only patrol |
| Follow or escort an entity | missing | a `MoveTo` state targeting an entity |
| Hold still, drifting cold (no thrust) | missing | an idle state with zero `MovementDirection` |
| Flee out of detection range | missing | a state steering away from a target |
| Sweep a set of positions (a search pattern) | missing | a patrol over positions, not orbits |
| Agent warps through a wormhole | missing | needs the `Run` lifecycle cut |
| In-game dialog that pauses the game | assumed (operator, 2026-09-17) | the director opens a dialog, the simulation step pauses, and closing it or making a choice is an event |
| Ink beat hooks from the director | missing | `LocalMenu` plays stories bound to orbitals only; the dialog primitive can host Ink |

The **missing** rows are the whole agent-side cost of the tutorial: four small states, one
director, and an Ink hook into the dialog primitive. Everything else the script asks for already exists.

## Beats

Each beat lists what it teaches, what the director does, what advances it, and a sample
of Threefra's lines. The lines are extension material, drafts only.

### 0. Wake up already hiding

- **Teaches:** that the game is about being seen. Look, and don't move.
- **Setup:**
  - The player starts inside an asteroid belt, reactor throttled, drifting.
  - Two sponsor drone ships spawn outside the belt, stance hostile toward the player,
    with no detection yet.
  - Mechanics that exist today: detection through `EntityInfoGathered`, and
    thrust-driven visibility (`Thruster.cs:83`).
- **Director:** drones run a search sweep across the belt (missing primitive). Their
  `Target` stays null until the player enters their `VisibleEntities`.
- **Advances:** the timer runs out with the player undetected (success), or a drone's
  `VisibleEntities` adds the player (go to beat 2 early).
- **Threefra:** "Don't touch the throttle. Don't touch anything. You're a rock. You've
  always been a rock. Rocks don't have feelings and they definitely don't have engine
  plumes."
- **Depends on:** clutter-aware detection and signature masking (parked electronic
  warfare work). Until then, a fixed detection range tuned for the belt stands in.

### 1. The assessment begins, loudly

- **Teaches:** flight in gravity wells, looking, the HUD.
- **Director:** drones withdraw (flee state). Ink beat: Threefra introduces the
  "qualification checklist" with open disdain.
- **Player:** fly to a marked orbit. The director listens for the player's position
  crossing the orbit radius, an event from the zone step.
- **Threefra:** "Item one: can operate a ship. I'll be generous and count 'hasn't hit the
  planet' as a pass."

### 2. Targeting, and your stance

- **Teaches:** target reticle, next and previous, reading a target's stance toward you,
  toggling your own stance (N), and that neutral safes your weapons.
- **Director:** spawn one neutral wanderer on a patrol. After the player toggles hostile
  on it, its grudge fires and it attacks.
- **Advances:** stance change to hostile, then the wanderer's `Target` becomes the player.
- **Threefra:** "Congratulations, you've declared war on a cargo tug. It will remember
  this. They always remember."

### 3. Weapons and heat

- **Teaches:**
  - weapon groups;
  - heat;
  - thermal shutdown and override;
  - radiators;
  - cockpit heatstroke as the cost of overriding.
- **Director:** the tug fights until destroyed (existing `CombatState`).
- **Advances:** `Death` of the tug.
- **Threefra:** "Watch the heat. The override is there for when dying later beats dying
  now. That's not a philosophy, it's a button."

### 4. Loot

- **Teaches:** selecting loot in range, the timed tractor grab, and cargo.
- **Director:** none. Wreck drops come from the kill.
- **Advances:** pickup completed.
- **Depends on:** the pickup capability cut (simulation loot bodies, the tractor as `Tool`
  gear). Until then, today's collision pickup stands in.

### 5. Dock, sell, repair, refit

- **Teaches:**
  - docking;
  - selling loot;
  - repair;
  - refitting on the inventory schematic;
  - item brand and quality (lot provenance).
- **Director:** the station is the protagonist faction's. Ink beat on dock.
- **Advances:** undock with a changed loadout.
- **Depends on:** Sell and Repair (Three Gates item 3).
- **Threefra:** "That laser says Alakrita on the side. That means someone at Alakrita
  was having a good day. Buy it before they find out."

### 6. Power and priorities

- **Teaches:**
  - reactor throttle;
  - capacitors;
  - priority tiers;
  - starving a subsystem on purpose.
- **Director:** spawn a turret pair on an orbit (existing turret loadout). They are
  hostile and not moving.
- **Depends on:** the stats and power pipeline. Until then, this beat teaches the shield
  toggle and capacitor drain only.

### 7. Fire control: reveal and aim

- **Teaches:** gathering target data, revealing subsystems, aiming for the turret's
  weapon, and rolls that depend on distance and evasion.
- **Director:** turrets stay put. Their target is the player once visible.
- **Advances:** both turrets disarmed by subsystem damage, not by hull destruction.
- **Depends on:** fire control (playground Cut 2).
- **Threefra:** "Shoot the gun, not the turret. The turret is mostly an opinion attached
  to a gun."

### 8. Completely unexpected ambush

- **Teaches:** that stance is a signal, and grudges.
- **Director:**
  - The two drones from beat 0 return with a third, launched from an unseen carrier.
  - Their transponders read as an unregistered outlaw credential.
  - They attack the player's position, and the carrier stays outside detection (hold
    cold, missing primitive).
- **Player choices, all valid:**
  - **Fight:** destroy the drones.
  - **Hide:** break line of sight in the belt, and drift until they sweep past.
  - **Hunt the carrier:** gather target data along the drones' approach vector.
- **Advances:** all drones destroyed, or the player undetected for a full sweep cycle, or
  the carrier located.
- **Depends on:** electronic warfare for the credential and hunt branches. Without it,
  the fight branch alone works.

### 9. The form was fake

- **Teaches:** nothing mechanical. It sells the world.
- **Director:** Ink beat. Threefra admits the checklist was theatre and the ambush was
  the assessment. The player's route through beat 8 decides her read on them.
  - **Fought:** she says good, loud and obedient, which is exactly what the sponsor
    likes.
  - **Hid:** she's quietly delighted, and warns them to never let the sponsor see how
    good they are at that.
  - **Found the carrier:** she tells them, very seriously, to act surprised in their
    report.
- **Threefra (hid):** "You sat in a rock pile while three drones looked straight at you.
  I'm going to write 'adequate' on the form. There is no form. I'm writing it anyway."

### 10. Warp out

- **Teaches:** wormholes, warp, saving, and that death is permanent from here on.
- **Director:** unlock the tutorial zone's wormhole. On warp arrival, end the prelude
  run and hand over to the Three Gates run.
- **Depends on:** the `Run` lifecycle cut for handing the tutorial run over to the real
  run.

## Mechanics coverage

| Mechanic | Beat | Buildable today |
|---|---|---|
| Detection, thrust visibility, hiding | 0, 8 | partly (no clutter or masking) |
| Flight and gravity | 1 | yes |
| Targeting, stance toggle, weapons safe, grudges | 2, 8 | yes |
| Weapons, heat, shutdown and override, heatstroke | 3 | yes |
| Loot pickup | 4 | collision stand-in |
| Dock, trade, refit, brands | 5 | buy and refit yes; sell and repair no |
| Power, capacitors, priorities | 6 | shield and capacitor only |
| Fire control, subsystem reveal, aimed rolls | 7 | no |
| Drones, transponders, carrier hunt | 8 | fight branch only |
| Wormholes, save, permadeath | 10 | yes, with the lifecycle cut for handoff |
| Mining, towing | none | left out; they belong to the future economy scope |

## Open questions for the operator

- Does the tutorial open in hiding (beat 0), or does beat 0 move to the ambush so the
  player learns the verbs first? Opening in hiding sells prop hunt immediately, at the
  cost of a player who doesn't know the controls yet.
- Is Threefra the sponsor's assessor, as in the original, or is the sponsor chosen per
  run? The protagonist faction comes from `TutorialGenerationSettings`.
- Should mining and towing get optional side beats, or stay out until the economy scope?
