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

**Known ownership smell in CultCache: loading writes.** `AddBackingStore`
subscribes `Add` to a store's `EntryAdded`; `MultiFileBackingStore.PullAll`
re-emits every entry it loads; `Add` pushes to the store registered for that
type, and that `Push` writes a file. So a cache with a second store rewrites
that store's files on open, and the game does this to `GameData/NameFile` at
every startup. Nothing distinguishes "loaded this" from "changed this".
Whoever next owns persistence should separate the two; until then, readers
must not register a writable store they only mean to read.

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

## Items: roles now, Njordr later

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

Migration to Njörðr widens quality into dimensions and turns designs into
classes plus recipes, parts into lots, and faction competence into producer
policy. Role names and authored data carry over; scalar quality does not.

Generic classes also mean no faction's absence from a galaxy can remove a
required item class. Until that lands, database variety has to guarantee it;
`LoadoutGenerator` logs whenever a required item falls back to an unavailable
manufacturer.
