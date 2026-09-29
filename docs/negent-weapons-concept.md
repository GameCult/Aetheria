# Adrasteian Negent Weapons: Item Concept

Date: 2026-09-18

Status: concept, for the content pass to author against. No cut, no schedule.
The mechanism it rests on is ruled in `docs/stats-and-power-target.md`
("Negentropy items"); the fiction is `AetheriaLore` →
`Worldbuilding/Post-Elysium/Technology/Running Cold` and
`Concepts/Negative Entropy`.

## The inversion

Negent converts heat into high-energy photons. So an Adrasteian weapon is not a
gun that tolerates cold: **its ammunition is the ship's thermal load**. A crew
works up a fever and spends it as light.

Every other weapon in the game is limited by how hot firing makes you. This one
is limited by how cold firing makes you. Operator, 2026-09-18: "You're
desperately pumping heat into it just to get it warm enough to fire again. Fun
inversion."

## What falls out of it

- **Firing makes a ship quieter.** The universal tell — the heat a weapon dumps,
  which is what a sensor operator is watching for — reverses. An Adrasteian
  gunner becomes harder to see by shooting.
- **Reloading is deliberate overheating**, and that is the loud, fragile,
  visible part of the cycle. The ship is most detectable when the gun is empty
  and safest while it is firing. No other weapon has that rhythm.
- **The crew is the leak.** Heat leaves the ship somewhere, and sustained fire
  walks a ship toward the cold end of the band, where hypothermia is waiting.
- **A long fight starves it.** Everything already spent downrange, no star
  nearby, and the best weapon aboard is inert until the crew makes heat again —
  which means running drives, which means being seen.
- **Wear punishes the swing**, and this weapon is nothing but swings:
  `Entity.cs:1386` derives wear from distance off optimum *and* from `deltaTemp`.
  A negent gun eats itself faster than a conventional one, which is Adrasteia's
  commercial relationship with its customers in miniature.

## What it needs from the Body

Almost nothing new, which is the point.

- `ItemData.MinimumTemperature` / `MaximumTemperature` and `HeatPerformanceCurve`
  (`ItemData.cs:371-435`) already express a weapon whose optimum sits well above
  ambient and whose performance collapses when it cools.
- The firing behaviour must **remove** heat rather than add it. That is the one
  behaviour-level piece, and it is a sign, not a mechanism.
- The Adrasteian charge (the consumable that gates negent generally, per the
  ruling) is what stops this being free cooling. **Without the charge cost this
  item solves heat management**, which would flatten the system the whole design
  rests on. It is the balance, not a flavour item.
- Roles for the content pass (`docs/item-provenance-target.md`): the charge
  assembly, the conversion array, the emitter. Quality per role is what makes
  two Adrasteian guns different guns rather than two numbers.

## Why it is worth authoring early

The heat system reads as three systems until a player meets something that
inverts it. A weapon that wants you hot, goes quiet when it fires, and freezes
its own crew teaches the whole system in one encounter, without a tutorial
saying any of it.
