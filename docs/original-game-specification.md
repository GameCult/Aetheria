# Aetheria Original Game Specification

Status: source-derived living specification

Authority: the executable fossil has two normative strata. The original RTS
lineage is pinned immediately before the ARPG port at `d12d7c5c^`; the current
ARPG lineage is pinned at `origin/master`. Removed RTS mechanics remain part of
Starbridge even when the ARPG rebuild no longer contains their bodies.
This document describes that behavior without granting the fossil Unity project
authority in the target architecture.

Pinned ARPG baseline: `ab2c294464bb30b9b5c8987cdb9108204821a92d`.
Pinned RTS baseline: `d12d7c5c^`.

Subsystem specifications:

- [Simulation](original-game-simulation-spec.md)
- [Combat](original-game-combat-spec.md)
- [World, economy, and progression](original-game-world-economy-spec.md)
- [Presentation](original-game-presentation-spec.md)
- [Rendering and views](original-game-rendering-spec.md)
- [Migration matrix](original-game-migration-matrix.md)

Target gameplay evolution beyond fossil parity:

- [Cockpit and doctrine combat](cockpit-doctrine-combat.md) defines the
  post-parity target experience: an embodied cockpit, player-authored combat
  intent, and cognition-mediated precision execution. Original manual controls
  remain the parity baseline and an optional fallback/debug surface until that
  direction passes its experience proofs. This target does not rewrite the
  archaeology or parity scenarios; original semantic actions, simulation
  constraints, and damage remain its execution substrate.

## Purpose

The original Unity game contains the accumulated mechanics, timing, controls,
rendering decisions, and player feedback that define Aetheria as a game. The
target is behavioral parity through a different machine:

- the Aetheria daemon owns game state and simulation decisions;
- the Aetheria simulation owns committed physical state and computes spatial
  integration, queries, and collision through Ymir;
- Eve surfaces expose everything a client needs to understand and present;
- EveUnity lowers those semantics into Unity rendering and interaction;
- provider assets retain authored visual, audio, and material content;
- no client reconstructs authority from presentation state.

Parity means the same player intent produces materially the same mechanical
outcome and legible feedback. It does not require preserving Unity component
boundaries or implementation accidents.

## Evidence Rules

Each normative behavior must cite an executable source path in the applicable
pinned lineage. Git history is authoritative evidence when the ARPG port
deleted an implemented RTS mechanic.
Timing, thresholds, formulas, and ordering must cite the defining source rather
than a caller that merely observes the result. Scene and prefab evidence is
valid for composition, layer, camera, and feedback behavior. Current-branch
code is evidence of migration status, not evidence of original behavior.

The principal source bodies are:

- `Assets/Scripts/ServerShared`: Unity-free mechanics and state transitions;
- `Assets/Scripts/Gameplay`: player orchestration and presentation feedback;
- `Assets/Scripts/Zone Display`: world, map, fog, and field presentation;
- `Assets/Scripts/UI`: HUD, inventory, trade, menus, and input display;
- `Assets/Scenes/ARPG.unity`: runtime composition and camera/layer policy;
- `Assets/Resources`: provider-authored prefabs, materials, icons, and effects;
- catalog and settings assets: authored numeric balance and content.

## Modes And Views

### Terminus

Terminus is a single-player game mode using the pilot surface. The minimum
complete loop is: fly, acquire or select a target, fire, survive return fire,
destroy or disable something, scoop useful cargo, dock, and trade. It shares
pilot mechanics and presentation with the Starbridge pilot role. The target
product is a repeatable roguelike run: deploy from the persistent Hangar,
cross escalating encounters, salvage and refit, then extract, win, abandon, or
die. Only an accepted terminal settlement may move rewards or losses between
the run and the Hangar.

### Starbridge

Starbridge is one multiplayer mode with two simultaneous projections of one
authoritative state:

- one commander uses the 2D RTS view;
- up to four pilots use the 3D ARPG view;
- the Unity map panel and Electron commander backdrop are projections of the
  same strategic world facts;
- view choice never creates a second simulation.

Starbridge uses Commander-daemon default simulation with jurisdictional Pilot
correction. The Commander daemon simulates every fact and owns canonical
finality and persistence. Pilot daemons independently predict facts for their
own ship, daemon-assigned nearest environment entities, and assigned combat
engagements. A validated mismatch resolves toward the pilot result; the
Commander daemon corrects and replays its provisional state. Outside pilot
jurisdiction, the Commander player owns Verse authorship and the Commander
daemon result stands. No Pilot daemon owns a private persistent world.

Terminus, Starbridge, and Arena share the canonical Hangar, deployment,
catalog keys, fitting rules, and saved loadout templates described in
[Game Modes And Progression](game-modes-and-progression.md). They do not share
live session entity graphs, encounter state, or mode-local stock.

### Arena

Arena is the server-authoritative PvP and competitive game-simulation mode. Human
players and AI controllers use identical typed observations and operations;
the server alone admits deployments, advances simulation, resolves combat, and
scores outcomes. Deterministic and accelerated Arena runs are the primary NPC
policy training/evaluation and build-versus-build balancing harness, not a
second rules implementation. Terminus and Starbridge must also run headlessly;
headless execution belongs to the shared simulation body, not to one mode.

### Pilot View

The pilot view follows the controlled ship in 3D. It presents world geometry,
combat, fields, effects, target feedback, cockpit status, and overlays. It must
not render tactical-map glyphs embedded in provider prefabs.

Evidence:

- `Assets/Scenes/ARPG.unity` composes follow and dock cameras;
- `ProjectSettings/TagManager.asset` assigns `Minimap` to Unity layer 14;
- `Assets/Scripts/Gameplay/EntityInstance.cs` owns a separate `MapIcon` visual;
- `Assets/Scripts/Zone Display/ZoneRenderer.cs` sizes map icons from minimap
  distance independently of world geometry.

### Tactical And Strategic Views

Map views render semantic contacts, bodies, fields, influence, and navigation
at map scale. Their glyph scale and visibility rules are independent from the
native geometry used by the pilot camera. A native asset may contain visuals
for multiple view channels, but each camera renders only its selected channel.

## Core Entity Model

An entity is a simulated body with identity, faction, transform, velocity,
visibility, target state, equipment, cargo, docking relationships, behaviors,
and derived performance. The controlled entity is selected by authoritative
run state, not by whichever client object currently has focus.

Entity lifecycle includes creation from authored templates, activation in a
zone, observation, targeting, damage, destruction, cargo/equipment drops,
docking, undocking, zone transfer, and persistence in the run checkpoint.

Primary evidence:

- `Assets/Scripts/ServerShared/Entity.cs`;
- `Assets/Scripts/ServerShared/EntityData.cs` and related loadout types;
- `Assets/Scripts/Gameplay/EntityInstance.cs`;
- `Assets/Scripts/Gameplay/ActionGameManager.cs`.

## Flight And Spatial Interaction

Player movement is intent applied to a simulated ship, not direct client
transform mutation. Camera-relative input is converted to semantic movement;
the simulation resolves thrust, velocity, drag, control limits, heat, and
collisions. `origin/master` has no privileged boost input or boost state.
Acceleration comes from installed and active thrusters or Aether drives,
available energy, heat, mass, and movement axes. Future drive-boost or evasive
maneuver actions therefore enter as equipment-provided semantic actions rather
than being invented as hidden base-flight rules.

Spatial mechanics include:

- acceleration, steering, velocity, drag, and stopping behavior;
- collision bodies and collision response;
- range checks for weapons, sensors, tractor, docking, and interactions;
- tractor force and towing/scooping behavior;
- docking and undocking placement;
- projectiles and collision hits;
- zone and navigation boundaries.

Target owner: Ymir computes authoritative spatial results from daemon-owned
intent and body state. The daemon commits resulting game state. Clients may
interpolate presentation but may not decide collisions or accepted movement.

### Original Flight Update

`Ship.Update` recalculates thrust and torque, clears actuator axes, allocates
lateral and longitudinal thrust banks, compensates asymmetric lateral torque,
steers toward the XZ look direction, drives Aether-drive axes, then applies
hull drag and gravity. Thrusters consume energy, apply `thrust / mass * delta`,
contribute torque, add heat, and add visibility. Aether drives separately model
rotor RPM, coupling, passive coupling, torque, energy draw, and thrust.

Original tractor input is analog and approaches the requested value at two
units per second. Interact prioritizes a nearby wormhole and otherwise attempts
the first eligible nearby dock. Undocking requires a cockpit, propulsion, a
reactor, and a bay able to release the ship.

Evidence: `Ship.cs`, `Thruster.cs`, `AetherDrive.cs`, and
`ActionGameManager.cs`.

## Sensors, Visibility, And Targeting

Visibility is simulated and observer-relative. Sensors accumulate information,
contacts distinguish visible/known/hostile state, pings alter visibility, and
target selection is constrained by available contacts and game rules.

Passive sensor capability belongs to installed equipment, not to the `station`
or `ship` entity kind. A station generally sees farther because its size and
mass budget supports more and larger arrays together with the required power,
cooling, and computation. An equivalently equipped ship and station have the
same capability. Docked craft may consume the station's contact picture, but
do not inherit a magical range value.

The pilot can select explicitly, cycle or choose nearest targets, and obtain a
lock where required by equipment. The UI distinguishes target existence,
visibility to target, target visibility, hostility, lock progress, shields, and
hull condition.

Primary evidence:

- sensor and visibility behaviors under `Assets/Scripts/ServerShared`;
- targeting in `Assets/Scripts/Gameplay/ActionGameManager.cs`;
- target feedback fields in the ARPG scene and HUD prefabs.

Visibility is the sum of independently decaying sources produced by thermal
radiation, weapons, propulsion, explicit visibility behavior, reflector cross
section, and active pings. Each observer accumulates information per target.
Crossing the configured threshold creates a classified contact; falling below
it removes the contact and clears invalid targets. Active ping consumes energy,
raises the observer's signature, expands over time, and grants information as
its radius crosses entities. Targeting creates reciprocal `TargetedBy` state.

## Equipment And Derived Performance

Ships are assembled from a hull and slotted items. Items contribute behaviors
and stat recipes. Equipment placement, condition, temperature, energy use,
cooldowns, toggles, and active consumables affect derived ship performance.

Required behavior families include:

- hull, cockpit, reactor, battery, radiator, heatsink, thermotoggle;
- thruster and maneuvering modifiers;
- sensor, visibility, ping, and lock support;
- shield generation and shield state;
- instant, constant, projectile, and guided weapon behavior;
- tractor and cargo interaction;
- consumables and timed effects.

Behavior groups expose switches, triggers, or analog controls while behaviors
own execution. The original set includes instant, constant, charged, lock,
automatic, launcher, and guided weapons; capacitors, reactors, radiators,
sensors, reflectors, visibility, mining and resource scanning; stat modifiers;
conversion, velocity-limit, trigger, switch, and thermotoggle behaviors.

Stats derive from quality, durability performance, thermal performance, wear,
stat recipes, and attached modifiers. Persisting behavior fields without
executing this graph is not parity.

The action bar is a binding surface over currently available semantic actions.
Its contents change with equipment and cargo. Any exposed input, including
Shift or Space, may bind to an action slot. Controller directional strings are
first-class input gestures.

## Thermal And Energy Simulation

Items and ship compartments exchange or generate heat according to their
behaviors. Cockpit temperature contributes to hypothermia and heatstroke.
Threshold crossings produce warnings; sustained extreme temperature can kill
the pilot. Heatstroke and hypothermia recover according to gameplay settings.

Original formula and threshold authority:

- `Assets/Scripts/ServerShared/Entity.cs` heatstroke/hypothermia update paths;
- `Assets/Scripts/ServerShared/Settings.cs` temperature thresholds,
  multipliers, exponents, recovery speeds, and control limits.

Presentation includes schematic temperatures, limit markers, warnings,
post-processing intensity, severe heatstroke phasing, shutdown state, and cause
of death. Presentation never computes medical state from a color or effect.

The original thermal model is cell-based. Hull cells exchange heat with
orthogonal neighbors according to occupants, conductivity edges, and thermal
mass ratios. Exterior cells radiate through an authored power curve and this
radiation contributes visibility. Radiators consume energy to pump and reject
heat. Item online state and thermal performance derive from local temperature.

Energy demand drains capacitors evenly first; remaining demand is distributed
across online reactors. Propulsion, weapons, shields, sensors, charging, and
cooling therefore share one energy network. A scalar heat value with linear
decay is only a placeholder.

## Combat

Combat order is:

1. The actor selects or retains a valid target.
2. An available semantic weapon-group action is activated.
3. Weapon state validates lock, range, cooldown, energy, heat, ammunition, and
   behavior-specific constraints.
4. The simulation emits the appropriate instant, constant, projectile, or
   guided attack.
5. Ymir resolves spatial travel and collision for physical attacks.
6. Damage is applied through shields, armor/hull rules, equipment effects, and
   destruction state.
7. Destruction produces authoritative drops and removes or transforms the
   entity.
8. Eve publishes both durable state and transient feedback events.

The original game exposes multiple weapon behavior classes. A single generic
bolt approximation is not parity. Each behavior's timing, targeting, damage,
visibility, energy, heat, and effect contract must be specified from
`Assets/Scripts/ServerShared` and weapon presentation code.

Player feedback includes muzzle/beam/projectile effects, shield impact,
damage/destruction effects, audio, target meter changes, lock state, and a
time-bounded hit marker when the controlled entity damages its target.

Primary feedback evidence:

- `Assets/Scripts/Gameplay/ActionGameManager.cs` target and hit-marker logic;
- weapon implementations under `Assets/Scripts/Gameplay/Weapons`;
- behavior implementations under `Assets/Scripts/ServerShared`;
- `Assets/Scripts/Gameplay/EntityInstance.cs` effect materialization.

Damage is typed and spatial. Shields are active energy-to-heat interceptors,
not regenerating shield hit points: interception requires an active shield and
energy for `damage * EnergyUsage`, then deposits `damage / Efficiency` as heat.
Remaining damage maps to schematic cells and flows through armor, fitted item
durability, then hull durability according to spread and penetration. Armor,
item, weapon, cockpit, hull, and thermal deaths are distinct facts.

AI is a state/task system. Combat agents evaluate weapon-group damage across
range, select useful groups and optimum range, approach, retreat or match
velocity, target, and fire. Other tasks include hauling, mining, patrol,
towing, survey, and wandering. Nearest-player pursuit with one synthetic
weapon is not parity.

## Loot, Cargo, Inventory, And Equipment

Destroyed entities can create world pickups from authoritative cargo and
equipment decisions. A player can acquire loot through collision/scoop or
tractor interaction when rules permit. Items move between world, cargo bays,
equipment slots, docking storage, and trade stock through typed operations.

The UI supports inspection, comparison, drag/drop transfer, valid-destination
feedback, equipment changes, cargo capacity, and action availability. Client
drag state is presentation-only; accepted transfer and resulting loadout are
daemon-owned.

Original destroyed entities probabilistically drop non-hull equipment and all
cargo as physical pickups with inherited/random velocity and a thirty-second
lifetime. Pickup collision attempts storage in eligible cargo bays; success
destroys the pickup and failure leaves it in space. Equipment fitting validates
hull shape, rotation, occupancy, hardpoints, and placement constraints.

## Docking, Stations, And Trade

Docking requires a valid nearby facility and results in authoritative docking
relationships and placement. Docked presentation switches camera/context and
enables station services. Undocking restores the pilot to world simulation at
an authoritative spawn transform.

Trade uses station stock, player funds/resources, item quality/condition,
prices, and capacity rules. Purchase and sale mutate daemon state atomically
and return receipts. The trade UI derives available commands and disabled
reasons from the published state.

The original market is station inventory. Purchase requires credits and
destination capacity; commodities split into stack-sized lots; hull purchase
creates a docked player ship. No original sell path was found. Stations also
provide refit/loadout restoration, ship selection, local narrative, and towing.

## World, Zones, And Progression

The world contains zones, stations, orbitals, bodies, asteroids, factions,
connections, fields, and encounter state. Zone rendering includes world
objects plus gravity/fog/influence and map projections. Travel and progression
must preserve the original generation and transition rules before new content
extends them.

Terminus content may be sparse, but the machinery must support a repeatable
single-player roguelike loop. Starbridge reuses the same Hangar and pilot
machinery while adding its own shared session, commander, and multi-pilot
coordination under mixed authority. Arena reuses the same deployment vocabulary
under server-authoritative admission/normalization.

Travel is embodied through nearby wormholes. Transition transfers the current
entity, reveals destination adjacency, performs exit motion, and saves.
Progression is exploration, discovery, combat, loot, refit, trade, ship
acquisition, faction territory, boss-path traversal, and narrative locations.
No XP/level system was found; item quality, rarity, and acquisition are the
implemented progression axis.

## HUD And Player Feedback

The pilot HUD must expose, at minimum:

- controlled ship schematic and equipment state;
- target identity, relation, visibility, lock, hull, and shield state;
- crosshair/view direction and selected-target indicators;
- action bar with live availability, cooldown, binding, and activation state;
- cargo/inventory affordances and interaction prompts;
- docking and station context;
- thermal, shutdown, damage, death, hit, and warning feedback;
- minimap/map projection through its own view channel;
- command rejection or disabled reasons where an action cannot execute.

The UI's visual composition may be modernized, but information timing and
salience are part of behavior. A metric card containing the same number is not
automatically equivalent to the original feedback.

## Rendering And Audio

Provider assets may contain multiple semantic subviews: pilot geometry,
tactical glyphs, shields, combat effects, influence, and other overlays. Eve
advertises the active view. Runtime variants map semantic render channels to
native camera/layer mechanisms. The pilot camera must not render map glyphs;
map cameras must not depend on pilot geometry being visible.

Authored prefab transforms are authoritative presentation data. Semantic world
radius is collision/selection metadata and must not rescale native art. Camera
framing derives from authored visual bounds and advertised camera semantics.

Audio and VFX are triggered from semantic state or event facts with stable
identities and timing. They may not infer authoritative hits, deaths, or weapon
activation from local particle collisions.

## Save, Resume, And Multiplayer Consistency

Authoritative run state and durable Hangar progression are separate typed
CultCache state published through CultMesh. Reconnect, reload, and view changes
must reproduce the same current entity, zone, session loadout, docking, target,
combat, and run-local cargo. The Hangar separately preserves owned ships,
stored equipment, currencies/materials, unlocks, and saved loadout templates.
Only deployment and exactly-once settlement transactions cross that boundary.
Presentation-only animation may restart; game decisions may not.

Starbridge clients observe one shared session. A pilot and commander can issue
different semantic commands, but neither owns a private copy of the world.

## Required Scenario Proofs

1. **Flight:** launch Terminus, fly, turn, stop, exercise installed propulsion
   actions, and collide; daemon-committed state and visible Ymir-computed motion
   agree throughout.
2. **Target and combat:** acquire a hostile, lock where required, fire each
   weapon family, observe effects and hit feedback, take return fire, and
   destroy the target.
3. **Loot:** observe authoritative drops, tractor or scoop one, and see the item
   appear in cargo exactly once.
4. **Dock and trade:** dock, inspect stock, buy and sell, change equipment,
   undock, and observe the changed ship behavior.
5. **Thermal:** generate heat, cross warning/control thresholds, recover, and
   verify HUD/post-processing follows daemon facts.
6. **Views:** show the same zone in pilot, minimap, Unity tactical map, and
   Electron commander view without leaking view-specific glyphs.
7. **Persistence:** restart daemon/client mid-loop and recover authoritative
   state without client repair logic.
8. **Starbridge:** one commander and multiple pilots act in the same session;
   all projections converge on one command/receipt history.
9. **Cross-mode progression:** deploy one saved Hangar loadout into Terminus and
   Starbridge through the same typed path; admit it into Arena through that path;
   settle a Terminus outcome exactly
   once; prove reconnect, failure, and proof scenarios cannot duplicate or
   counterfeit durable progression.
10. **Arena simulation:** run a deterministic server-authoritative match with
    human or AI controllers using the same typed operation boundary; reproduce
    the scored outcome headlessly from the same seed and ordered inputs; compare
    controller-policy and ship-build variants with versioned reproducible facts.

## Open Archaeology Ledger

The following require exact source-derived sub-specs before parity can be
claimed:

- complete weapon-family formulas and timing;
- full thermal/energy/stat-recipe update order;
- AI state machines and encounter spawning;
- docking eligibility and placement rules;
- trade price and stock mutation formulas;
- zone generation, travel, and progression rules;
- every HUD indicator's source fact, update trigger, and animation timing;
- audio event matrix;
- render-channel membership for all provider prefab subviews;
- multiplayer ownership and conflict rules from the original network model.
