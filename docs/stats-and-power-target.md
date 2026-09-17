# Stats and Power Target

Date: 2026-09-17

Status: target, design rulings only. No substrate map or cut map yet. This
document owns the ends; a cut map will own the means. Where it lands in the
order relative to `docs/headless-playground-cut.md` (lifecycle, fire control)
is not decided.

## Why

Two current mechanisms need a better shape:

- **Power.** Every draw succeeds and overdraw becomes reactor heat, so players
  cannot prioritize energy use. There is already a hidden priority: behavior
  execution order decides who drains capacitors first.
- **Stats.** `PerformanceStat` is authored catalog data that also stores runtime
  modifier state per entity. Every read recomputes every factor. `StatModifier`
  snapshots its value once and matches targets by exact type name.

## Power (operator direction, 2026-09-17)

- **Priority tiers.** Critical is fed first; each following tier divides what is
  left, so low-priority subsystems starve first. Reactor throttling is a player
  choice.
- **All draw is continuous.** Instant-activation items get an input-only
  capacitor that the bus charges and an activation spends. Operator's words:
  "convert instant activation items into having a sort of input-only capacitor
  that needs to be charged, that way all power draw is continuous". A partial
  grant fills the buffer slower; no item receives a fraction of a shot.
- **Continuous consumers brown out** through a power supply curve on their
  performance stats. An exponent is enough. Operator's words: "I really want
  performance stats on those continuous consumers to have a power supply curve.
  Just an exponent is plenty, but we don't wanna pay that cost for every stat
  evaluation."

## Stats: one stat authority (operator direction, 2026-09-17)

- **A stat declares only the terms it uses.** Operator: "PerformanceStat would
  hold only the modifiers it uses." It also unifies with `StatModifier` "for a
  single stat authority".
- **A term is a source plus an application.**
  - Item-local sources: heat, durability, quality of a named design role, power
    supply, consumable progress.
  - External sources: another item's resolved stat, attached by a modifier.
- **Applications (ruled: "Scale and constant only" for modifiers).** Declared
  terms apply inside the stat's min/max interpolation as exponents. Modifier
  terms apply only as scale or constant after it. An effect on a condition,
  such as cooling, is expressed by changing that condition.
- **One resolver per entity owns every stat value.** It recomputes a stat only
  when one of its terms' sources changes, and behaviors read resolved values.
- **Validation on assignment is the guard.** A stat that decides a power
  request may not depend on power supply, directly or through a modifier chain,
  and no stat may depend on itself. The check runs at catalog load and Studio
  save against what is possible, and when a term attaches at equip time against
  what is on the ship. It fails loudly and names the items and the stat.

## Quality can change: item upgrades (operator direction, 2026-09-17)

- **Quality is not immutable.** Operator: "don't say never on quality, that'd be
  a footgun when we inevitably add the ability to upgrade items at special
  stations".
- **Upgrades target a design role.** Example: improving a laser's range by
  upgrading its focusing array. The affordance exists today: `Lot.Roles` holds
  per-role quality, `Lot.QualityForRole` reads it, and a stat names its role.
  The catalog authors no roles yet.
- **Upgrade stations are exploration content.** Finding the station, and perhaps
  a small quest to unlock it, is something to discover in each boss-gated region
  of Terminus.
- **Consequence for provenance.** Lots are immutable, and units of one lot are
  identical (`docs/item-provenance-target.md`, F4). An upgrade therefore produces a
  new lot whose provenance names the original lot, the station and the inputs,
  and the upgraded item points at it. The resolver treats a change of lot as a
  change to that item's quality terms.
