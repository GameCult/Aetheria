# Cockpit And Doctrine Combat

Status: target gameplay direction

This document defines the intended Aetheria pilot experience beyond original-game parity. The source-derived behavior remains documented in
`original-game-specification.md`; this document changes who owns combat precision and how the player understands the fight.

## Objective

Aetheria should make piloting a ship compelling at the distances, speeds, and information limits implied by its setting.

Manual aiming is the wrong primary skill test. A bot can place a firing solution more precisely than a human, many lethal attacks arrive from outside direct visual recognition, and the important decisions happen before a target becomes a clean object in the window. Combat should therefore focus on instruments, tactics, doctrine, and command while cognition performs precision sensing, maneuver calculation, fire control, guidance, and terminal execution.

The player still pilots a physical ship. They decide what result to pursue, what risks and expenditures are acceptable, which systems or capabilities to target, when to commit, and when to withdraw. Cognition under their control tries to realize that intent using the ship, crew, equipment, information, and authority actually available.

The desired loop is:

`fly -> interpret contacts -> establish doctrine -> designate intent -> supervise commitments -> disable, destroy, capture, or disengage -> recover -> refit`

## Ownership

### Player

The pilot owns:

- flight intent and the degree to which helm is delegated;
- contact selection and attention priorities;
- engagement objective;
- target-system and protected-system priorities;
- acceptable lethality and collateral risk;
- ammunition, heat, signature, drone, and maneuver expenditure policy;
- withdrawal and surrender conditions;
- cognition autonomy and exception policy;
- intervention at consequential commitment points.

### Cognition Planning

The daemon-hosted cognition planner derives proposed semantic actions within
granted doctrine:

- sensor allocation and contact classification;
- tactical positioning when helm authority is delegated;
- weapon-group selection;
- firing-solution construction and maintenance;
- aim-point selection consistent with subsystem intent;
- launch, guidance, loiter, interception, and recovery scheduling;
- reassessment after damage, new evidence, surrender, or doctrine completion;
- concise explanation of action, delay, refusal, and uncertainty.

Cognition does not inspect hidden truth. It plans from faction-local observations, retained information, doctrine, processing quality, cognitive load, communications, and available authority. The daemon command gate alone validates, accepts, and commits semantic actions. Cognition owns planning; it does not become a second execution authority.

### Daemon And Ymir

The player authors doctrine intent. The Aetheria daemon validates and owns its accepted canonical state along with cognition allocation, observations, accepted semantic actions, equipment state, deterministic probabilistic shot resolution, damage, and combat outcomes. Ymir owns authoritative world geometry and kinematics used by firing solutions, plus motion and collision for persistent physical payloads. Ordinary weapon damage does not wait for a presentation projectile to collide. Clients submit typed intent and lower daemon-authored state through Eve; they do not aim locally, infer hits from effects, or maintain a private combat result. The normative resolver and cut line are defined in `deterministic-combat-resolution.md`.

## Cockpit View

The pilot view becomes first-person from an embodied cockpit rather than a third-person follow camera. The cockpit is a spatial presentation surface with three layers:

1. **Windows and world:** native geometry, nearby vessels, stations, projectiles, docking approaches, fields, and effects.
2. **Fixed instruments:** persistent ship condition, propulsion, thermal, power, damage, cognition, deployment, and medical state.
3. **Tactical projection:** observer-relative contacts, classifications, maneuver guidance, doctrine, commitments, and command exceptions.

The cockpit must remain useful when nothing lethal is directly visible. Windows provide embodiment and local orientation; instruments provide combat reality.

Instrument layout is authored presentation, but every value and command derives from typed daemon-owned state. Unity, Godot, and later pilot clients lower the same semantic cockpit surface without becoming gameplay authorities.

## Direct And Delegated Flight

Direct flight remains part of Terminus and the Starbridge pilot role. Steering, thrust, velocity control, docking, tractor work, and embodied navigation preserve the original game's strongest physical interaction.

Helm supports three authority levels:

- **Manual:** the player supplies continuous semantic movement intent.
- **Assisted:** the player supplies heading, range, intercept, or maneuver intent and cognition resolves actuator use.
- **Delegated:** cognition executes positioning doctrine such as hold range, screen an ally, break solution, withdraw, or match velocity.

Installed thrusters, drives, mass, heat, energy, damage, and control limits constrain all three modes through the same simulation path. Delegation does not grant hidden maneuver capability.

## Doctrine

Doctrine is an ordered policy over combat objectives and constraints, not a stance bonus and not a second AI personality.

A doctrine may define:

- engagement objective: observe, deter, capture, disable, destroy, protect, escape;
- target priority: propulsion, weapons, sensors, cognition, cooling, power, communications, structure, crew spaces, cargo;
- protected regions or capabilities;
- acceptable lethality and collateral probability;
- preferred weapons, payloads, and engagement ranges;
- heat, power, ammunition, propellant, and signature reserves;
- drone launch, loss, loiter, and recovery policy;
- fire conditions under uncertain classification;
- surrender and cease-fire response;
- withdrawal thresholds;
- decisions cognition may make without consultation.

The first implementation should provide authored doctrines such as `Destroy`, `Disable`, and `Disengage`, then allow the player to edit a compact set of priorities and constraints. Doctrine composition must remain readable during combat. It should not begin as a general-purpose visual programming language.

## Subsystem Intent And Fire Control

The player does not aim a reticle at a distant component. They designate a desired effect or subsystem class. Fire control chooses weapon groups, aspects, firing times, and aim points based on gathered target information and the ship's current solution.

Subsystem intent consumes existing and future mechanics:

- observer-relative target information;
- inferred or known schematic topology;
- weapon range, timing, spread, penetration, and damage type;
- armor, fitted-item, and hull-cell damage paths;
- target motion and countermaneuver;
- cognition allocated to fire control;
- fire-control throughput and solution quality;
- protected regions and collateral constraints.

Better fire control both cycles more attacks and keeps more of them on the intended solution. Better cognition can maintain alternatives, recognize changed target state, allocate weapons, and choose when to spend terminal maneuver. Neither creates information or physical capability the ship does not possess.

The original semantic weapon actions remain the execution boundary. Cognition invokes those actions under doctrine instead of the player repeatedly pressing a fire binding.

## Commitment And Intervention

Routine execution proceeds without confirmation. The cockpit interrupts the player only when doctrine cannot resolve a consequential fork, including:

- materially ambiguous target identity;
- conflict between protected and targeted systems;
- transition from disabling to likely lethal effect;
- expenditure below a declared heat, ammunition, propellant, or recovery reserve;
- an action that substantially reveals ship geometry or capability;
- loss of the information or authority required by standing orders;
- surrender, disengagement opportunity, or withdrawal threshold;
- a crew, cognition, or command-authority challenge.

An intervention presents the decision's causal shape: available evidence, expected effect, major uncertainty, resource and signature cost, recommendation, and time remaining. It must not pretend that a forecast is guaranteed truth.

## Required Instruments

The first cockpit surface requires six coherent instrument groups.

### Flight

Velocity, orientation, commanded and available acceleration, turn authority, maneuver reserve, destination or intercept, docking and tractor cues, and helm authority mode.

### Ship Schematic

Hull cells, armor, fitted equipment, local temperature, durability, online state, isolation, damage, and repair priority.

### Thermal And Power

Reactor output, capacitor state, demand by system, heat production and movement, radiator state, cooling limits, and forecast consequences of current doctrine.

### Contact Plot

Observer-relative contacts, information level, classification, hostility, selection, lock state, evidence age, active-ping consequences, and known uncertainty. Early versions may expose the original visibility model before richer hypothesis state exists.

### Doctrine And Command

Objective, target and protection priorities, expenditure limits, withdrawal conditions, autonomy, command owner, pending challenge, and unresolved exception.

### Weapons And Deployments

Weapon readiness, fire-control assignment, solution state, ammunition, launched platforms, drone tasks, loiter and recovery state, and the reason any expected action is being delayed or refused.

Cross-highlighting must connect these surfaces. Selecting a proposed attack should reveal its target region, weapons, power and heat cost, fire-control demand, expected signature, and doctrine rule. Selecting damage should reveal affected capability and current tactical consequence.

## Explainable Cognition

Delegated combat is only playable when the player can understand why the machine acts.

The cockpit must expose compact reason facts such as:

- holding fire because the propulsion solution crosses protected habitation;
- repositioning for an aspect that exposes the selected subsystem;
- reserving a weapon for terminal defense;
- declining recovery because the rack cannot clear before threat arrival;
- withdrawing because declared maneuver reserve has been crossed;
- requesting command because classification confidence does not satisfy doctrine.

These are structured decision facts with references to observations, doctrine clauses, constraints, and semantic actions. They are not generated flavor text and not a client reconstruction of hidden AI state.

A bad outcome must remain diagnosable as some combination of incomplete information, cognition quality, cognitive load, damaged communications, weak fire control, unsuitable doctrine, conflicting authority, or physical impossibility. The interface may summarize; the underlying causal state must remain inspectable.

## Compatibility With The Original Loop

The original combat transition remains valid below the new command layer:

1. The player establishes doctrine and designates contact-level intent.
2. Cognition allocates sensing, maneuver, fire control, and deployed platforms.
3. Cognition chooses an available semantic equipment action.
4. The daemon validates lock, range, cooldown, energy, heat, ammunition, authority, and behavior constraints.
5. Ymir resolves spatial travel and collision where required.
6. Damage applies through shields, armor, equipment, hull cells, and thermal state.
7. Cognition reassesses objective completion, doctrine, and exceptions.
8. The daemon commits and publishes typed facts and receipts through CultMesh; Eve composes their presentation and transient feedback.

Loot, cargo, docking, trade, refit, travel, and progression remain part of the pilot loop. Doctrine combat changes how an engagement is commanded, not why the player cares about surviving it.

## Canonical State Direction

Extend existing canonical documents before introducing another state shape. A new named projection is justified only by hidden-information filtering, viewport selection, expensive aggregation, lossy cockpit summary, or another deliberately different contract.

| Concept | Canonical document owner | Allowed projection | Forbidden duplicate |
| --- | --- | --- | --- |
| Ship systems, thermal, power, damage, movement, medical state | Existing `player_cockpit_state`, authored by daemon | Lossy Eve instrument composition | Renderer-owned cockpit model |
| Contact and target knowledge | Existing `zone_contacts`, authored and observer-filtered by daemon | Viewport contact plot | Client classification cache treated as truth |
| Equipment actions and readiness | Existing `action_slots` plus equipment behavior state, authored by daemon | Cockpit weapon/deployment summary | Separate doctrine weapon inventory |
| Active doctrine and editable policy | One canonical doctrine document, authored through typed player operations and validated by daemon | Compact doctrine controls | Client-local policy used by combat execution |
| Cognition allocation and load | Canonical cognition state, authored by daemon | Cockpit allocation summary | UI-owned cognition budget |
| Combat assignment and subsystem intent | Canonical assignment state, authored by daemon-hosted planner through the command gate | Observer-safe target overlays | Client-generated aim points or assignments |
| Fire-control solution | Daemon-private or faction-authorized state at the resolution gameplay requires | Observer-filtered confidence and reason summary | Publishing exact hidden solutions to every client |
| Deployed-platform orders and recovery | Existing entity, behavior, docking, and action state extended with canonical orders | Deployment plot | Separate drone minigame truth |
| Exceptions, challenges, and receipts | Canonical command and receipt documents, authored by daemon command gate | Cockpit intervention queue | Modal UI state that decides acceptance |
| Decision reasons | Durable accepted decisions, refusals, material constraints, and receipts | Lossy explanation surface | Persisting every transient planner candidate as canonical history |

Do not build a private daemon combat model followed by a copied cockpit DTO graph. Transient planner deliberation may remain local to the daemon; only gameplay-relevant assignments, decisions, refusals, constraints, and receipts require durable shared state.

## First Playable Slice

The smallest test isolates indirect combat before rebuilding the complete pilot presentation:

- one authored encounter;
- one player ship and one known hostile;
- existing pilot presentation and direct manual helm;
- one `Disable` doctrine;
- one propulsion subsystem intent;
- one existing weapon group;
- daemon-hosted cognition choosing whether to activate that group;
- two visible reason outcomes: `firing` or `holding`;
- existing cell-based damage and equipment consequences;
- fire ceases when propulsion is disabled.

The first-person cockpit, doctrine editing, protected regions, intervention prompts, additional doctrines, and assisted or delegated helm follow only after this proof establishes that commanding fire is more engaging than manually aiming it.

This slice answers one question before the design expands:

> Is it fun to pilot the ship while commanding the fight instead of performing precision gunnery better suited to the ship's cognition?

## Experience Proofs

1. A pilot can win an engagement without manually aiming or repeatedly firing weapon groups.
2. Before committing, the pilot can explain the objective, selected subsystem intent, important cost, and major uncertainty.
3. During delegated execution, the pilot can explain why weapons are firing, waiting, changing targets, or refusing an action.
4. Direct, assisted, and delegated helm obey the same propulsion, heat, energy, mass, and damage constraints.
5. A disabling doctrine ceases or changes fire when its objective is met.
6. Damage to sensors, fire control, cognition, power, cooling, weapons, or communications visibly changes available doctrine execution.
7. The cockpit remains useful when the lethal threat is outside direct visual recognition.
8. Reconnect or view transfer restores doctrine, assignments, commitments, and command state without rerolling cognition decisions.
