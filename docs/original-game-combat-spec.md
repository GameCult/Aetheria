# Original Game Combat Specification

Baselines: RTS `d12d7c5c^`; ARPG `origin/master` at `ab2c2944`.

## Target And Lock

Each entity owns one target and clears it when the target leaves the zone or is
no longer a visible enemy. Sensors accumulate observer-relative information;
crossing detection thresholds updates visible friendly/enemy collections.

`LockWeapon` resets lock when target changes. Lock accumulates only against a
hostile target and depends on target angle, lock speed, direction-impact
exponent, and gathered sensor information raised to `SensorImpact`. Outside the
lock angle it decays by `Decay * delta`. Firing requires lock above `0.99`, an
available cooldown, and strict `MinRange < TargetRange < Range`.

Targeting also produces reciprocal `TargetedBy` state and begin, complete,
lost, visibility, and indicator feedback.

Evidence: `ServerShared/Entity.cs`, `Behaviors/LockWeapon.cs`.

## Weapon Families

Base weapon performance includes damage, penetration, spread, minimum/maximum
range, energy, heat, visibility, and velocity from item performance stats.

- **Instant:** trigger starts burst and cooldown. Damage, heat, and energy split
  across rounds. Each round consumes resources, emits fire, wear, heat,
  visibility, and presentation events. Magazines reload from matching cargo.
- **Automatic:** retriggers instant bursts while held after cooldown.
- **Constant:** applies continuous energy, heat, wear, and optional ammunition;
  emits start, stop, and reload transitions.
- **Charged:** accumulates charge and charge-dependent damage, heat, spread,
  burst, visibility, and velocity multipliers. Early release and overcharge
  failure are authored behavior.
- **Lock:** instant weapon gated by progressive angular/sensor lock.
- **Launcher:** lock weapon whose projectile receives guidance, thrust, lift,
  dodge frequency, and missile velocity parameters.
- **Guided:** guided projectile weapon without the lock gate.

Evidence: `ServerShared/Behaviors/Weapon.cs`, `InstantWeapon.cs`,
`AutoWeapon.cs`, `ConstantWeapon.cs`, `ChargedWeapon.cs`, `LockWeapon.cs`, and
`Launcher.cs`.

## Spatial Resolution

Weapon behavior emits firing facts. Presentation binds effects to source,
barrels, and selected target, but does not own accepted damage.

- Ballistic projectiles apply gravity and drag, sweep/raycast their segment,
  ignore source, and terminate on collision, range, or optional airburst.
- Guided projectiles predict intercept, apply guidance/lift/thrust curves,
  dodge procedurally, terminate after range or overshoot, and may split into
  equal-damage children.
- Hitscan resolves range immediately.
- Pulse lasers resolve repeated hits over visual duration and prorate damage.
- Constant laser, lightning, and particle weapons continuously resolve hits.
- Mines arm and resolve proximity/splash behavior.

Target owner: Ymir owns trajectories, sweeps, contacts, ray/shape queries, and
spatial hit candidates. The daemon interprets those candidates through weapon,
shield, armor, equipment, faction, and lifecycle rules.

Evidence: `Gameplay/Weapons/Projectile.cs`, `GuidedProjectile.cs`,
`HitscanEffect.cs`, `Laser.cs`.

## Shields And Damage Topology

Legacy shields are not shield-HP pools. An active shield intercepts only when
the entity can pay `damage * EnergyUsage`; the hit consumes energy and deposits
`damage / Efficiency` as heat.

Hull collision maps a hit into schematic cells from texture coordinates,
hardpoint geometry, spread, and penetration. Per affected cell damage order is:

1. armor;
2. installed item durability;
3. hull durability.

Splash selects exposed cells by impact direction. Damage produces distinct
incoming-hit, armor, item, hull, depleted-armor, destroyed-item,
destroyed-weapon, cockpit-destruction, and death facts.

Evidence: `Behaviors/Shield.cs`, `Gameplay/EntityInstance.cs`,
`ServerShared/Entity.cs`.

## Heat And Death

Weapon and shield heat is deposited at physical item/hull locations. The cell
thermal network and radiators move or reject it. Cockpit temperature drives
heatstroke/hypothermia independently of hull durability.

Death causes are hull destruction, cockpit destruction, heatstroke, and
hypothermia. Cause is authoritative and drives presentation.

## Destruction And Drops

On hull depletion the entity is marked destroyed. Every non-hull equipped item
drops independently with default probability `0.25`; all cargo drops. Pickups
receive random velocity with authored magnitude. Destruction VFX is requested
and the entity leaves the zone.

## Required Feedback Events

The daemon must publish deduplicated, timestamped semantic events for:

- weapon trigger/start/round/stop/reload/cooldown;
- charge start/update/release/failure;
- lock begin/progress/complete/lost;
- projectile launch/split/impact/expire;
- shield interception and energy/heat consequence;
- armor, item, weapon, cockpit, and hull damage;
- incoming hit and controlled-entity hit confirmation;
- death cause, destruction, and each created drop.

Durable current state and transient chronology are separate contracts. A client
joining late needs current lock/cooldown/health; it must not replay old impacts.

## Current Migration Defects

1. Normal daemon simulation and optional abstract combat kernel are concurrent
   writers when the flag is enabled. One combat owner must replace both paths.
2. Current lock is immediate or confidence-based, not original angular lock.
3. Catalog equipment is persisted but combat executes one synthetic weapon.
4. Ammunition, reload, bursts, energy, wear, charge, and failure are absent.
5. Projectile taxonomy is collapsed to one guided circle.
6. Damage is scalar shield HP then scalar hull; original shield and cell damage
   semantics are absent.
7. Thermal state is one scalar with linear decay.
8. Destruction does not transactionally generate original drops/death facts.
9. Ymir hit facts are consumed without a published feedback chronology.
10. Health normalization has competing projections and no single contract.

## Combat Parity Proof

For every weapon family, capture intent, daemon weapon state, Ymir query/body
timeline, accepted hit, damage topology, resource costs, event sequence, Eve
projection, and rendered feedback. Compare thresholds and timing to the fossil.
