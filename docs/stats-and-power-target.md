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
- **Authoring roles is required, not optional (operator, 2026-09-17).** Until
  designs carry roles, every item is one-dimensional. Operator's words:
  "authoring roles for the catalog is what creates generative depth in the item
  selection for the game, until then every item is literally one-dimensional,
  it's not optional."
  - Agents generate the role data, and the operator reviews it.
  - The data is written through the catalog's own types (AetherDb or a scratch
    console over `Aetheria.Shared`), never through hand-copied schemas.
  - Validation refuses a stat term that names a role its design lacks.
- **Upgrade stations are exploration content.** Finding the station, and perhaps
  a small quest to unlock it, is something to discover in each boss-gated region
  of Terminus.
- **Consequence for provenance.** Lots are immutable, and units of one lot are
  identical (`docs/item-provenance-target.md`, F4). An upgrade therefore produces a
  new lot whose provenance names the original lot, the station and the inputs,
  and the upgraded item points at it. The resolver treats a change of lot as a
  change to that item's quality terms.

## Negentropy items (operator direction, 2026-09-18)

Adrasteian **negent** gear cools below ambient. Untended it is an infinite-DPS exploit:
heat is what limits sustained fire, so a free heat sink removes the limit.

- **It runs on a consumable, and that is the balance.** Operator: "I like the consumable
  option... That would make it expensive to run". The charge is a lot like any other, with a
  maker, a process and a price (`docs/item-provenance-target.md`), so a damage ceiling
  becomes a supply line, priced by the economy rather than by a cap.
- **Embrittlement needs no new mechanic.** Operator: "embrittlement isn't a joke, either,
  running gear that cold already causes rapid wear". `Entity.cs:1386` already derives `Wear`
  from distance off `OptimalTemperature` (`ItemData.cs:409`) and from `deltaTemp`, so negent
  wears gear twice: off-optimum, and by the speed of the swing. Weapons already spend that
  wear as durability damage (`Behaviors.cs:77`, `InstantWeapon.cs:192`,
  `ConstantWeapon.cs:135`). Do not author a negent-specific wear rule.
- **Cooling is a change to a condition, not an effect on a stat.** It lowers temperature and
  every stat that names heat responds on its own, the pilot included. That is the rule this
  document already states for conditions; negent is its sharpest case, not an exception.
- **The pilot stays exposed.** Dying hot is the default failure and the cultural nightmare;
  negent moves a ship toward the other end, where hypothermia is waiting. The safe band
  narrows from both sides and the thing saving the ship is what kills the crew.
