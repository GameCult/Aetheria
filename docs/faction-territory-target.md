# Faction Territory Target

Date: 2026-09-19

Status: target, operator direction only. No map, no schedule. This document owns
the ends; a cut map will own the means.

## Why

A faction's territory today is a set of zones carrying a Markov-generated name.
`Galaxy.cs:340` feeds one chain per faction from its `GeonameFile` (a massaged
GeoNames extract), `:348` stamps the result on each owned zone, and `:352` gives
everything unowned an `EAC-####` serial. Nothing else distinguishes one faction's
space from another's. A Zhestokost sector and an Alakrita sector differ only in
the phonotactics of a string.

Operator, 2026-09-19: "Namefiles were how we fed Markov chains to generate
faction-flavored names, the limitations of the geonames database I massaged to
create them were reflected in the naming choices. We can do much better with
generative models, even giving sectors and their stations roles in a faction's
colonization timeline and supply chain, with names and backstories that match.
Giving each faction such a mapping will also gives each faction a recognizable
shape to their territory."

## Where it sits

**After fire control** (operator, 2026-09-19: "slot it in right after fire control, first step
is to get the new shape of the combat working"). Combat's new shape — rolls from weapon,
targeting and sensor stats, resolution on arrival, the pre-impact commit window — comes first;
territory is the campaign after it. Territory is what finally makes the economy and provenance
work visible in play, so it does not drift further than that.

## Ends

- **A place has a job.** Every owned sector, and every station in it, carries a
  role in its owner's supply chain — extraction, refining, fabrication,
  logistics, administration, defence, research, disposal — and a position in that
  faction's colonization timeline: founding wave, expansion, boom, retrenchment,
  whatever that faction's history was.
- **Territory has a shape you can read.** The role map is spatial, so a faction's
  space looks like what it does. Lightsail is corridors between other people's
  markets; Zhestokost is a fortified frontier with depth behind it; Adrasteia is
  scattered deniable nodes with no obvious centre; Aeronautics Unlimited is an
  extraction belt feeding a few refineries. A player who has flown two sectors of
  a faction's space should be able to guess the third.
- **Names and backstories match the job and the era.** A founding-wave refinery
  and a late boom-town dormitory do not sound alike, and neither sounds like the
  other faction's equivalent. The name says which faction, which era, which role.
- **The supply chain is real, not decorative.** Roles connect: an extraction
  sector feeds a refinery, which feeds fabrication, which feeds a market. Those
  links are data, so the economy's backward generation
  (`docs/item-provenance-target.md`) can walk them, a lot's `Produced` origin can
  name the station that made it, and piracy has routes worth interdicting rather
  than an abstract risk number.
- **Generation stays deterministic.** A galaxy seed produces the same territory
  every time. Nothing calls a model at runtime.

## Constraints

- **Authoring happens ahead of play, into the catalog.** Generative models write
  typed catalog data that the operator reviews, exactly like roles and product
  text; the game then reads authored data. No runtime inference, no network
  dependency, no per-run variation that the seed does not explain.
- **Volume has to be honest.** A galaxy has many sectors. Whatever is authored
  must either cover the count or compose deterministically from authored parts;
  a bank of a hundred hand-written names behind a thousand sectors will repeat
  visibly, and that has to be a decision rather than an accident.
- **The faction record owns its territory rules.** Today `Faction.GeonameFile`
  points at a `NameFile`; whatever replaces it belongs on the faction, and
  `NameFile` and `MarkovNameGenerator` go when the last consumer does.
- **Unowned space needs an answer too.** `EAC-####` is a placeholder, and
  unclaimed sectors are most of the map in some galaxies.
- **Zone generation already has settings and a seed** (`ZoneGenerationSettings`,
  `StableHash`); territory roles hang off the same generation, not a second pass.

## Open questions for the map

- Authored bank versus composable grammar versus a hybrid, and what repetition
  rate is acceptable at real galaxy sizes.
- Whether a station's backstory is authored text, a few typed facts the UI
  renders, or both — and where it is shown, since a name nobody reads is
  cheaper than prose nobody reads.
- How a faction's colonization timeline interacts with galaxy generation's
  existing spatial logic (megaplex, boss zones, entrances).
- Whether roles are assigned to sectors, to stations within them, or both.
- What happens to a sector when its owner changes hands, and whether the
  history it carries survives the change.

## Not in scope

- Runtime model calls, for anything.
- Rewriting galaxy topology. This assigns meaning to what generation already
  produces; it does not redesign the map.
