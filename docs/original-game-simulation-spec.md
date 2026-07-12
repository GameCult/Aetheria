# Original Game Simulation Specification

Baselines: RTS `d12d7c5c^`; ARPG `origin/master` at `ab2c2944`.

## Entity Lifecycle

Entity construction starts from a hull item. It maps the hull grid, initializes
armor, thermal mass, conductivity, hardpoints, equipment occupancy, behavior
groups, and weapon groups. Activation initializes behaviors and subscriptions
for zone membership, hostility, targeting, visibility, and destruction.

Deactivation disposes subscriptions and stops active behavior updates. Parent
and child entities share parent pose/velocity and child mass contributes to
parent mass. Docking removes the ship from the zone and parents it to a docking
entity; undocking restores zone membership and activation.

Death may result from hull depletion, cockpit destruction, heatstroke, or
hypothermia. Equipment fitting and cargo placement preserve item identity and
validate geometric occupancy.

Evidence: `ServerShared/Entity.cs`, `Zone.cs`, `Ship.cs`,
`EntitySerializer.cs`.

## Stat Evaluation

Behavior stats combine authored data with item quality, durability performance,
thermal performance, wear, and attached modifiers. Additive and multiplicative
modifiers can be attached dynamically by equipment and timed consumables.

The daemon must execute one catalog-derived behavior graph. It must not retain
one set of broad serialized fields while advancing a separate synthetic stat
model.

## Thermal Grid

Each hull/equipment cell has temperature and thermal mass. Orthogonal cells
exchange heat according to conductivity edges and mass ratios. Exterior cells
radiate according to authored curves and radiation contributes visibility.
Behaviors deposit or move heat at their installed location. Item thermal
performance and online state derive from local temperature.

Cockpit temperature advances heatstroke/hypothermia accumulation and recovery.
Threshold crossings and death are authoritative events.

The daemon derives the grid from the equipped catalog, rather than accepting a
renderer-authored heat scalar. Hull mass and specific heat are distributed over
the hull shape; installed equipment contributes thermal mass and replaces the
conductivity of its occupied cells. Equipment temperature is the mean of those
cells. Authored thermal-performance curves and durability derive each item's
current performance and online state. Generic wear and weapon-triggered wear
consume the same temperature-derived wear potential.

## Energy Network

Capacitors store charge. A demand drains charged capacitors evenly first, then
divides unmet demand among online reactors. Behaviors succeed or fail according
to the actual transaction. Radiators consume energy to pump heat and produce
waste heat. Shields consume energy per intercepted damage.

Per daemon tick, equipment performance is derived before behavior execution and
all energy consumers transact against the same equipped network. Radiators then
pump heat and add their demand, reactors settle baseline output, overload,
capacitor refill, throttling and equipment heat, and finally the thermal grid
conducts and radiates. Thermal and radiator radiation are independently replaced
visibility contributions; they are not cumulative scalar drift. A client may
request semantic controls, but cannot set charge, heat, wear, or online state.

## Behavior Execution

Required behavior families:

- propulsion: thruster and Aether drive;
- energy: capacitor, reactor, conversion;
- thermal: radiator, heatsink, thermotoggle;
- sensors: sensor, ping, reflector, visibility;
- defense: shield and hull/armor consequences;
- weapons: instant, automatic, constant, charged, lock, launcher, guided;
- utility: tractor, mining, scanner, item usage, velocity limit;
- composition: switches, triggers, behavior groups, stat modifiers;
- timed consumables containing ordinary behaviors.

Behavior groups expose semantic switches, triggers, or analog controls. The
daemon advertises those controls as current actions. Clients never invoke the
behavior implementation directly.

## Consumables

Activation requires the actual item in cargo. Non-stackable effects reject a
duplicate active effect. Activation removes the item instance and executes its
behaviors for authored duration. Quality contributes to stat evaluation.
Expiration removes effects and attached modifiers exactly once. `ItemUsage` may
consume another configured cargo item.

## Sensors And Visibility

Visibility sources are independently identified and decayed. Passive sensors
accumulate observer-relative information from sensitivity, signature, and
range. Threshold transitions create/remove classified contacts. Ping consumes
energy, raises source visibility, expands in space, and grants information on
crossing. Hostility uses faction ownership, security, and presence rules.

## AI

Agents own root/current state, task, controls, acceleration, and transitions.
Steering compares desired/current velocity. `MoveTo` is stopping-aware;
`MatchVelocity` converges to target motion. Combat evaluates useful weapon
groups and optimum range. Civilian tasks include hauling, mining, patrol,
towing, survey, and wandering.

AI reads the same contacts, behavior graph, physics, and command primitives as
players. It does not use privileged damage or movement shortcuts.

The original corporation scheduler is preserved in commit `ab8aa5fb`,
`ServerShared/GameContext.cs`. For each corporation it filters unreserved
tasks, groups them by task type, discards groups with no compatible available
controller, orders jobs by descending priority, zips them with compatible
controllers, flattens the assignments, reserves each task, and calls the
controller's ordinary `AssignTask` entry point. The daemon scheduler must retain
that queue/capability/priority/availability contract. Assignment state is
durable; task execution drives the same semantic controls used by players.

The historical hauling controller (`991d411c`) travels to the origin, docks,
loads up to available capacity and outstanding quantity, waits, travels to the
destination, docks, unloads, and repeats until the requested quantity is
delivered. Daemon task phases may advance only from accepted movement,
docking, and cargo-transfer results. A rejected transfer leaves both cargo and
task phase unchanged.

The remaining implemented RTS controllers at `d12d7c5c^` are normative:

- `MiningController` selects the nearest extant asteroid, approaches tool
  range, aims and activates the mining switch, chooses another depleted
  asteroid, and returns home to unload when cargo is full.
- `SurveyController` chooses the nearest unsurveyed planet or belt asteroid,
  approaches scanner range while matching target motion, aims and scans until
  the corporation survey floor satisfies scanner density, then advances.
- `TowingController` travels to the station, parents the station to the tug,
  carries it to the requested orbital radius, detaches it, creates the orbit,
  and completes.
- `PatrolController` continuously selects orbital targets and uses locomotion
  until it reaches the configured target distance.
- `WanderController` chooses random planets or orbitals; orbital wandering can
  dock for a randomized duration before selecting another target.

Evidence: `d12d7c5c^:Assets/Scripts/ServerShared/Behaviors/{Mining,
Survey,Towing,Patrol,Wander,Hauling}Controller.cs` and
`ab8aa5fb:Assets/Scripts/ServerShared/GameContext.cs`.

## Quality And Progression

Crafted items carry continuous quality. Quality changes performance, durability
response, thermal response, price, and wear. Rarity tiers map quality bands and
derive tier/upgrade count. There is no XP/skill-tree system in the baseline.

## Ymir Boundary

Ymir owns deterministic bodies, integration, fields, shape/ray/sweep queries,
contacts, impulses, projectile trajectories, and spatial placement candidates.
The daemon owns equipment interpretation, accepted controls, resource costs,
damage meaning, lifecycle, inventory, and persistence.

Daemon projectile integration and contact detection now call standalone
`Ymir.Core`. Ship flight, gravity, tractor interaction, docking placement, and
the remaining Unity query bridge still need to enter that same world before
the boundary is complete.

## Simulation Pipeline

```text
typed commands + elapsed fixed time + catalog-derived behavior graph
  -> validate semantic controls and resource transactions
  -> derive forces/bodies/queries for Ymir
  -> Ymir deterministic spatial step
  -> interpret contacts and spatial results
  -> advance thermal, energy, sensors, AI, behaviors, lifecycle
  -> one atomic daemon commit
  -> durable state + deduplicated event chronology
  -> Eve projections
```

Order must be made explicit and parity-tested against the baseline. Full daemon
boot is not the unit-test boundary: each organ requires narrow ports, mockable
clock/physics/catalog dependencies, and adjacent pipeline smokes.
