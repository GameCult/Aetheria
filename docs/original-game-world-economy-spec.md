# Original Game World, Economy, And Progression Specification

Baselines: RTS `d12d7c5c^`; ARPG `origin/master` at `ab2c2944`.

## Docking

Contextual interact first considers a nearby wormhole, then docking. Docking is
proximity-based, requires the ship to be unparented, iterates nearby entities,
and uses the first bay accepting `TryDock`. Success changes zone/parent
relationships, control binding, perspective, camera, inventory context, and
station services.

Undocking requires cockpit, propulsion, reactor, and a docking bay able to
release the ship under its occupancy/cargo constraints. The daemon owns all
eligibility and placement. Eve publishes current context, available command,
disabled reason, and resulting receipt. Camera/menu transition is derived.

## Inventory And Equipment

Items retain stable identity, quality, durability, temperature, behavior state,
and quantity. Equipment placement validates hull shape, rotation, occupancy,
hardpoints, and equipment constraints. Cargo bays are shaped grids supporting
capacity and stacks.

Every transfer is an atomic daemon operation with source, destination, item,
placement, resulting state, and typed rejection reason. Client drag/drop is a
proposal and preview only. Required paths include cargo-to-cargo,
cargo-to-equipment, equipment-to-cargo, equipment-to-equipment, station/player
transfers, loadout restore, naming, and current-ship selection.

## Destruction, Loot, And Scooping

Destroyed entities roll each non-hull fitted item independently for drop and
drop all cargo. Pickups are physical bodies with identity, item contents,
velocity, and thirty-second lifetime. Tractor affects spatial motion. Pickup
collision attempts storage in any eligible cargo bay. Success commits item
transfer and despawn exactly once; insufficient capacity leaves the pickup.

## Stations

Stations provide docking bays, stock cargo, trade, refit/loadout restoration,
owned/player ship selection, local narrative, and optional towing. Target
architecture publishes one coherent station workflow surface rather than a
catalog of unrelated contextual dialogs.

## Trade

Station inventory is the baseline market. Price comes from item data or crafted
valuation. Purchase requires credits and destination capacity. Commodities
split into stack-sized lots. Hull purchase creates a docked player ship. Credits,
stock, destination inventory, and ship ownership commit atomically.

No baseline sell path was found. Selling, stock replenishment, scarcity,
production, and dynamic faction economy are extensions; they must not be cited
as missing baseline parity.

## Galaxy And Zones

Generation creates zones, links, factions, entrance/exit, critical/boss path,
initial discovery, and contents. Zone state includes entities, bodies,
asteroids/resources, orbitals, stations, fields, wormholes, encounters, and
faction/security context.

Sector map is a discovered-topology inspection projection. Full zone map and
minimap are separate spatial projections. Shared identities link all views.

## Travel

Travel is embodied by entering a nearby wormhole. One daemon transaction:

1. validates proximity and destination;
2. removes/transfers the current entity;
3. changes current zone;
4. reveals destination adjacency according to baseline rules;
5. chooses authoritative exit pose/motion through Ymir;
6. checkpoints the run;
7. publishes transition/result events and new projections.

No Unity callback may complete discovery, transfer, or save after command
acceptance. Towing is a separate station-mediated relocation mechanic.

## Progression Spine

New game begins at the entrance with a generated player ship. Baseline
progression consists of:

- exploration and discovery;
- combat and survival;
- physical loot acquisition;
- equipment fitting and refit;
- trade and ship acquisition;
- faction territory and critical/boss-path traversal;
- narrative locations;
- persistence of zone, entity, inventory, loadout, and action bindings.

The target daemon needs an explicit run-state machine linking these pieces,
including new/continue, active encounter, docked service context, travel,
failure/death, completion, and Starbridge session roles. A collection of
documents is not itself a game loop.

## Receipts And Player Recovery

All rejected operations expose stable reason codes and recovery facts. Required
examples include cargo full, invalid placement, missing cockpit/propulsion/
reactor, no eligible bay, insufficient credits, invalid target, unavailable
route, item unavailable, stale quote, and duplicate command.

## Scenario Proof

Run from new game through flight, wormhole travel, hostile encounter,
destruction/drop, failed and successful scoop, docking, purchase, equipment
change, undock, behavior change, save, restart, and continue. Capture daemon
commits, Ymir timeline, Eve surfaces/events, receipts, and visible client state.
