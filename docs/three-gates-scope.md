# Three Gates: Shipping Scope

Date: 2026-09-11

Aetheria ships as a short rogue-lite run on the existing Unity game: start at
the Entrance, fight through three boss chokepoints, reach the Exit. Everything
the player earns comes from what they destroy. More systems come after this
ships, one at a time.

## Core loop

Fight -> loot -> sell and refit at stations -> push to the next gate.

## Work, in order

1. **Run structure.** Death deletes the saved run (`ActionGameManager.Die`
   currently leaves `PlayerSettings.SavedRun` intact). Reaching the Exit shows a
   win screen. Credits persist in the save instead of the hardcoded 15M on
   `ActionGameManager`.
2. **Bosses as gates.** `Galaxy.PlaceFactionsMain` already puts boss zones on
   Entrance-to-Exit chokepoints and `SavedGame.BossZones` stores them.
   `ZoneGenerator` spawns the faction `BossHull` with an authored loadout in its
   boss zone; wormholes out of that zone stay locked until the boss dies.
   Difficulty scales per section.
3. **Reason to fight.** Kills already drop gear and cargo
   (`EntityInstance.cs`). Add Sell and Repair to station services
   (`TradeMenu` is buy-only; no repair exists).
4. **Fire control owns hits.** Manual aiming goes away. The player (and AI)
   targets an item on the enemy schematic; a fire control system in
   `ServerShared` resolves where shots land, making the existing damage
   localization matter. See below.
5. **Navigation planner.** Replaces steering for agent movement. See below.
6. **Minimal narrative.** One Ink beat per boss (pre-fight hail, aftermath),
   played through the existing `LocalMenu` Ink player. Story placement
   (`StoryProcessor`, commented out at `Galaxy.cs:239`) stays off.

Items 3 and 6 can land wherever there is room; 1 and 2 come first.

## Fire control

Aiming in space is not fun, and no human aims well enough for the schematic
damage model (armor -> item -> hull per cell) to matter. Fire control moves
skill from aim to decisions: which subsystem to hit, when to commit fire
against heat and visibility budgets, what each weapon group is for. Player and
AI share it, so AI accuracy is no longer superhuman by construction.

**Ruling (operator, 2026-09-17):** hit detection goes away entirely. A hit is a dice
roll over weapon stats, the stats of a new targeting-system subsystem, and sensor
state. Fire control is being mapped as Cut 2 of `docs/headless-playground-cut.md`.

**Ruling (operator, 2026-09-17):** the player and the AI can aim for a specific
subsystem once they have gathered enough target data to reveal it. Operator's words: "We
want the player as well as the AI to be able to aim for specific subsystems once it has
gathered enough target data to reveal those subsystems".
- Per-target info gathering already exists: `Entity.EntityInfoGathered`, accumulated
  with decay by `Sensor`. One detection threshold makes a target visible.
- Revealing a target's subsystems as that info accumulates does not exist yet.
- Selecting a subsystem to aim for does not exist yet either.

**Stance (IFF), rulings (operator, 2026-09-17):**
- Each entity holds a stance toward another, and both directions are visible: you see a
  target's stance toward you, and it sees yours.
- One button toggles your stance on the current target between hostile and neutral.
  Going neutral during a fight safes your weapons. Firing is gated by the shooter's own
  stance.
- No mirroring. Operator: "Mirroring isn't ideal because it would make them too easy to
  exploit. They should respond to a hostile stance by immediately going after you and never
  forgiving, not until we have a utility evaluation function that tells it to cut its
  losses".
- Future AI may read a neutral stance as a signal and change modes.
- Stance signals are events, not polling (operator: "signals like this should be events,
  not polling").
- Detection gates stance. You can only see a stance if you can see its holder: "I can only
  see someone's stance if I can see them". An NPC reacts to a hostile stance when it detects
  the entity holding it, either at the moment of the change or later, when detection
  happens.
- Stance overrides and grudges clear when an entity leaves the zone (operator, 2026-09-17:
  keep clearing). Making grudges persist belongs with the future utility-AI work.

- **Owner:** fire control in `ServerShared`. Decides hit, cell, and damage.
- **Inputs:** target and selected item, sensor/detection quality, range,
  target deviation from the predicted intercept, lock time.
- **Outputs:** schematic hits applied by `DamageSchematic`, which moves from
  `Gameplay/EntityInstance.cs` into `Entity`.
- **Demoted:** the seven Unity weapon effects (`Projectile`,
  `GuidedProjectile`, `Laser`, `ConstantLaser`, `HitscanEffect`, `Lightning`,
  `ConstantLightning`) and `HullCollider` become presentation only. Their
  `Physics.*` queries no longer decide damage.
- **Projectiles still fly** toward the predicted intercept; the hit is resolved
  on arrival against how far the target deviated, so maneuvering is evasion.
- **Deferred Unity-physics surfaces:** ship collision (`HullCollider`), loot
  pickup (`ShieldManager`), `TractorBeam`, `Mine`. Not blockers.

**Invariant: game simulation runs independently of Unity.** `ServerShared`
cannot reference UnityEngine (`Aetheria.Shared.Unity.asmdef` sets
`noEngineReferences`), and `Aetheria.Shared/Aetheria.Shared.csproj` builds it
from source with the plain .NET SDK, so a clean headless build is the check.
Tools (`tools/AetherDb`) and future test harnesses reference that project.
Unity-owned hit resolution still breaks the invariant behaviorally: the
simulation builds without Unity yet cannot resolve combat without it.
That was a velocity compromise. Moving hit authority into fire control
restores the invariant, and lets the planner and a balance harness simulate
combat.

## Navigation planner

Everything is a gravity well, and steering cannot navigate one. Escaping a
well means pumping energy: rocking (the classic mountain-car problem) or
thrusting tangentially to spiral out. Stealthy movement means riding terrain
and thrusting rarely; thrust adds visibility (`Behaviors/Thruster.cs:83`).

Approach: sampling-based model-predictive control (MPPI-style). Each agent
rolls out candidate thrust sequences over a cheap model of the real dynamics
(thrust, `Zone.GetHeight` slope, drag from `Ship.cs`) and follows the best.
Cost = goal progress + thrust x visibility + threat exposure + heat. Rocking,
tangential escape, and surfing emerge from one cost; personality is cost
weights.

The player's predicted coast path is an itemized capability: a navigation
computer behavior that runs the same rollout model and draws the result.
Without the item, surfing is learned by feel.

Open risks: drag may bleed too much energy for surfing to work; the height
field is time-varying (gas giant ripples, moving bodies), so rollouts must
sample it at future times. Loadout quality scales by section rather than
biasing toward the largest items.

## Out of scope for now

Economy simulation, hauling, mining yield, crafting/blueprints, reputation
changes, story placement, multiplayer and `Economy.Server`, a headless
server deployment, and the CultMesh/daemon rebuild (parked in
`F:\Projects\AetheriaEve`).

**Next scope after this ships (operator, 2026-09-17):** backward economy
generation (synthesizing provenance for existing goods down to mining), routes
moving consumer goods, and piracy. The item provenance *schema* is cut now, so
the data already has that shape; see `docs/item-provenance-target.md`.

**Parked for an electronic warfare expansion (operator, 2026-09-17):**
- Partial or misleading sensor traces that do not resolve a ship's identity.
- IFF that resolves at its own target-data threshold, independent of the identity threshold.
- Stance visibility is one detection-gated query today, so a separate IFF threshold can
  replace its gate later.
- Drones are the reason this matters (operator, 2026-09-17). Engaging an enemy with a drone
  swarm is canonically the best way to smoke them out and kill them without them ever getting
  close to spotting you. Drones can spoof their signatures too.

## Items: roles now, Njordr later

**Partly superseded (2026-09-17).** `docs/item-provenance-target.md` owns the
item model where they disagree: the manufacturer leaves the design, a crafted
item carries lot provenance (faction, station and factory, input lots), and the
brand and segment an item shows are derived from that provenance, not stored on
the instance.

Item property derivation belongs to Njörðr (`F:\Projects\Njordr`), the
GameCult economy daemon: properties are a vector over authored dimensions,
recipes name roles never materials, provenance lives on the lot, and rarity
and price are projections. Aetheria must not grow a second owner of that
rule. Njörðr is a specification with a typed state document and no step
function yet, and its Aetheria embedding (Rust engine over a C ABI) comes
after six engine cuts, so Aetheria ships before it exists.

What Aetheria authors now is the same shape, one dimension wide:

- An item design is generic and carries **roles**: named slots, one level
  deep, no requirements beyond the name. Every laser has a focusing array.
- A `PerformanceStat` may name the role whose quality it reads; unset reads
  the instance's own quality, which is today's behavior. This is Njörðr's
  `Derivation { output, from_role, from_dimension }` with the dimension
  pinned to quality, and it fixes the legacy model's mistake of naming an
  ingredient item rather than a role.
- A **faction product** is a manufacturer's branded build of a design: its
  name, its flavor text, and a pseudo-gaussian quality distribution per role,
  whose mean is the technology they put in and whose deviation is their
  quality control. Which products exist is what a faction makes, so
  availability, market segmentation, and regional progression are authored
  there rather than as duplicate item entries.
- A market segment is a second product with a role's mean raised; what makes
  that part better is the flavor text's to tell, because a part here carries
  quality and nothing else. Nuanced trade-offs wait for Njordr's dimensions.
- A crafted instance records the rolled quality per role, continuous. No part
  entry exists yet: the role's quality is authored by the product directly,
  and a part earns a record when crafting needs one.
- Flavor text on products is most of the lore this release delivers.
- Tier, color, and price stay projections of quality.
- **Roles are required content for Terminus (operator, 2026-09-17).** Designs must
  author roles, because without them every item is one-dimensional. Agents
  generate the role data. See `docs/stats-and-power-target.md`.

Migration to Njörðr widens quality into dimensions and turns designs into
classes plus recipes, parts into lots, and faction competence into producer
policy. Role names and authored data carry over; scalar quality does not.

Generic classes also mean no faction's absence from a galaxy can remove a
required item class. Until that lands, database variety has to guarantee it;
`LoadoutGenerator` logs whenever a required item falls back to an unavailable
manufacturer.
