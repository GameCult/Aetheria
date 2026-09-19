# Content Batch One: Authoring Spec

Date: 2026-09-19

Status: spec. This document says exactly what a later pass writes into
`GameData/Aetheria.cc` through the catalog's own types. It writes no catalog data itself,
and it decides nothing the operator has not already ruled on — the forks it found are
listed at the end, unanswered.

Source of the batch: `F:\Projects\AetheriaLore\Aetheria\Brainstorming\Corporate Roster and
Item Wishlist.md`. Quoted flavour is verbatim from that roster or from the source
spreadsheet it condenses; anything invented here is marked **(new text)**.

Every number below was read from the live catalog through `Aetheria.Shared`'s own types
(a scratch reflective dump over `AetheriaStores.Open`, catalog read-only, run against
`F:\Projects\Aetheria-stats\GameData\Aetheria.cc` — byte-identical to the main tree's,
SHA1 `9b10521…`). Where a number has no sibling in the catalog to argue from, it says so.

---

## 0. The finding that reorders the batch

**The batch's stated priority order cannot be authored in that order, because nothing in
the game can carry a shield or a thruster.**

`Entity.ItemFits` (`Assets/Scripts/ServerShared/Entity.cs:658`) refuses any non-`Tool` item
whose `HardpointType` does not match a hardpoint on the hull. The catalog holds exactly one
ship hull, LonginusX, and its ten hardpoints are:

    ControlModule, AetherDrive, Energy ×2, Launcher ×2, Radiator ×2, Reactor, Sensors

There is **no `Shield` hardpoint anywhere in the catalog** — not on LonginusX, not on Zenith
(station), not on Turret. There is no `Thruster` hardpoint either; LonginusX moves on its
AetherDrive (Traction), and the two shipped thruster designs (`deep space burnout`,
`Large Drive`) cannot be equipped on any hull in the game. `AetherDb hardpoint-fit` confirms
both, by omission.

So:

- A Megiddo shield authored alone is content that cannot be equipped, tested, or seen.
- Victoire and Prokope authored alone are the same.
- **The hulls are not item 3. They are item 0.** The Zhestokost and AU hulls are the carrier
  that makes items 1 and 2 reachable, and they must author `Shield` and `Thruster`
  hardpoints or the rest of the batch is shelfware.

Three items in the batch are playable on LonginusX today with no hull work, because their
hardpoints already exist or they are `Tool` items (which `ItemFits` places anywhere in
interior space): **ChirOptos** (`Sensors`, 1×1 hardpoint exists), **Cottage-pi**
(`Tool`), **Enigma Device** (`Tool`). Those are the batch's only zero-dependency wins.

**Revised order:** hulls → shields + thrusters → sensor, cloak, capacitor (any time) →
consumables (blocked, see §8).

---

## 1. Conventions every item in this batch follows

Read from the shipped catalog, not invented here.

**Stat shape.** `PerformanceStat` is `Min`, `Max`, and a list of `StatTerm { Source,
Exponent, Role }` (`ItemData.cs:612-684`). `Evaluate` multiplies each term's factor, lerps
`Min`→`Max` by the product, then applies scale and constant modifiers. A stat with no terms
resolves to `Max`.

**Inverted stats are authored `Min > Max`.** Where lower is better the catalog descends:
`ReflectorData.CrossSection` 500→150, `SensorData.PingVisibility` 5000→1000,
`PingCooldown` 20→5, `RadiatorData.EnergyUsage` 3→2. Every stat below follows that.

**Power supply multiplies (operator, 2026-09-19).** `StatSource.PowerSupply` applies to the
resolved value after interpolation, so zero supply is zero output. Two consequences this
spec obeys:

- A `PowerSupply` term belongs **only on an ascending stat where more output is the point**
  (thrust, pumped heat). On a descending stat it inverts: at zero supply the factor is 0, the
  lerp lands on `Min` — the *worse* end — and then the whole thing is multiplied by zero,
  which for a stat like `CrossSection` or `PingVisibility` means "unpowered = perfectly
  stealthed". **No descending stat in this batch carries a `PowerSupply` term.** This is a
  general trap, not a batch detail; §9 F3 raises it.
- Brownout on a buffered consumer needs no term at all. `InputCapacitor.AddCharge` is already
  scaled by `Item.PowerSupply` (`Shield.cs:127`), so a half grant simply takes twice as long
  to fill. The shields below spend their brownout budget there, not in a stat.

**Power-request stats may not read power supply.** `StatValidation.PowerRequestFields`
(`ItemData.cs:796`) names `ShieldData.Capacity`, `ThrusterData.EnergyUsage`,
`SensorData.PingEnergy`, `EnergyDrawData.EnergyDraw` and four more. The validator is being
widened to every stat a `PowerRequest` actually reads, which for a shield means
`RefillDuration` and `RestoreDuration` too (both feed `RefreshReserve` →
`InputCapacitor.RequestedFill`). **None of those five shield stats carries a `PowerSupply`
term below.**

**Heat response** is four floats on `EquippableItemData`: `MinimumTemperature`,
`MaximumTemperature`, `OptimalTemperature`, `PlateauWidth`. `StatValidation.ValidateHeatResponse`
refuses a zero span, an optimum outside the bounds, a negative plateau, or a plateau that pokes
past the bounds. Every set below is checked against those four rules inline. Plateau width is the
operational-lifespan lever: inside it `Performance()` is exactly 1 and `Wear`'s thermal term is
zero, so a wide plateau is a forgiving item and a narrow one burns itself out. That is the
single most expressive number a brand has, and this batch uses it hard.

**Roles.** A design declares `List<ItemRole>`; a `Quality` term names one.
`StatValidation.ValidateRoleUsage` refuses a term naming an undeclared role. The vocabulary is
small and **shared within a kind** (`docs/stats-power-cut7-roles.md`), so this batch reuses
`injector`/`nozzle` for thrusters, `emitter`/`receiver` for sensors, `plating` for hulls,
`converter`/`storage cell` for capacitors, and introduces exactly one new vocabulary —
`emitter`/`reservoir`/`regulator` for shields — shared by both shields in the batch. `Tool`
designs author roles per design (same doc), so the cloak gets its own two.

**Design text is dry; product text carries the voice** (`docs/item-provenance-target.md`).
Design names are functional nouns in the shipped style (`Large Drive`, `Core Power`,
`Surface Ore Extractor`). Product names are the brand.

**Energy units, for scale.** Reactor `Charge` is per-second generation (`Reactor.cs:60`):
MoveOnPro 25–125/s, Vulcan 50–200/s, Manhattan 500–1000/s, Notorious 400–1200/s. Capacitor
`Capacity` (PotaT+-) is 50–150. A `6k Shooter` shot costs `Energy` 5. Radiator `EnergyUsage`
1–3/s. Shield reserve sizes below are argued against those.

**Heat units, for scale.** `6k Shooter` `Heat` 250–500 per shot; `deep space burnout` 4000–12000;
`Cockpit 2x2` 200; radiator `PumpedHeat` 750–5000.

---

## 2. Item 0a — Zhestokost heavy hull

### Design: `Heavy Combat Hull` (`HullData`)

Compared against **LonginusX**, the only ship hull: Mass 2500, Price 7,500,000, Durability 500,
Shape 6×17 (66 cells), Armor 10, Drag 0.1, Conductivity 32, ThermalResilience 1, heat
173.15/573.15/275.15/48, `ReflectorData.CrossSection` 500→150 `Durability^1, Quality^2[plating]`.

| Field | Value | Why |
|---|---|---|
| `Description` | "Heavy combat hull. Thick plating, high mass, a large power and thermal budget, and a radar cross-section it makes no attempt to hide." | Dry. |
| `HullType` | `Ship` | |
| `Mass` | 4200 | 1.7× LonginusX. Zhestokost is the mass end of the axis; the only other ship hull defines the light end. |
| `Price` | 9,500,000 | Above LonginusX (7.5M), below Zenith (10M). |
| `Durability` | 900 | 1.8× LonginusX, matching mass. |
| `Shape` | 7×16, ~84 cells | ~27% more interior than LonginusX's 66, which is what pays for the extra hardpoints below. Exact cell mask is a schematic-authoring job, not a number this spec can fix. **Guess** on the mask; the cell *count* is argued. |
| `Armor` | 28 | LonginusX 10, Zenith 100, Turret 10. 28 is the only interpolation the three shipped values support for "heavy warship, not a station". |
| `Drag` | 0.16 | LonginusX 0.1; Zenith and Turret are 0. **Guess** — one sibling. |
| `Conductivity` | 20 | LonginusX 32, Turret 32, Zenith 1. Lower conductivity means heat pools locally: a brutalist hull that cooks its own gear. Deliberate, mild. |
| `ThermalResilience` | 3 | LonginusX 1, Turret 5, Zenith 15. |
| Heat four | min 173.15, max 623.15, **optimum 293.15, plateau 100** | Valid: 293.15 ± 50 = 243.15…343.15, inside bounds. The widest plateau on any equippable (LonginusX 48, Turret/Zenith 78). This *is* the Zhestokost brand as a number: the hull takes no thermal wear across a 100K band. Overkill, and finesse is for fools. |
| `Roles` | `plating` | Shared hull vocabulary. |
| `Prefab` | **blocked** — see §9 F1 | |

`ReflectorData`:

| Stat | Min | Max | Terms |
|---|---|---|---|
| `CrossSection` | 900 | 600 | `Durability^1`, `Quality^1[plating]` |

Descending (lower is stealthier) like LonginusX, but the whole band sits above LonginusX's
*worst* value of 500, and `Quality^1` (vs LonginusX's `^2`) means even a perfectly built one
barely improves. A Noka MKI is visible from the next system and no amount of workmanship fixes
that. Argued directly off LonginusX's 500→150.

**Hardpoints.** Sizes are chosen so shipped designs actually fit. `ItemFits` lets a smaller item
sit inside a larger hardpoint (it checks that every item cell lands on a hardpoint cell), so a
1×2 item fits a 2×2 hardpoint; the reverse does not.

| Type | Shape | Count | Armor | Fits today |
|---|---|---|---|---|
| `ControlModule` | 2×2 | 1 | 50 | Cockpit 2x2 |
| `Reactor` | 3×3 | 1 | 0 | Manhattan, Notorious (9 cells), and all 2×2 reactors |
| `AetherDrive` | 2×2 | 1 | 10 | Traction |
| **`Thruster`** | **2×2** | **2** | 15 | deep space burnout, Large Drive, and both new thrusters |
| **`Shield`** | **2×2** | **2** | 20 | both new shields |
| `Radiator` | 2×2 | 2 | 10 | every shipped radiator (2-cell and 4-cell) |
| `Ballistic` | 2×2 | 2 | 25 | every shipped ballistic (2- and 4-cell) |
| `Launcher` | 1×3 | 1 | 30 | GT 3K, pswarm, scorched void policy |
| `Sensors` | 1×1 | 1 | 5 | not if i see you first, ChirOptos |

Two `Thruster` and two `Shield` hardpoints are the load-bearing part of this whole document.

### Product: `Noka MKI`, by Zhestokost

`Description` — verbatim from the source spreadsheet, newlines restored:

> You asked, we answered. Presenting the Noka MKI, the biggest, baddest, most unstoppable\*
> hull in the galaxy\*\*. Designed for durability, the Noka MKI sports the thickest hull in the
> galaxy and n mounts compatible with the highest quality weapons Zhestokost has to offer.
>
> \*Regulatory policies require us to state for the benefit of our more pedantic customers that
> the Noka MKI does, in fact, have a functioning brake system
>
> \*\*We are not liable for any injury, bodily or emotional, maiming, unexpected explosive
> decompression, sudden death, or sinus infections caused by attempts to test the statement.

The source says "ColferV" (the sheet's old name for Zhestokost) and leaves the literal `n` in
"n mounts". Substituting the faction's current name is required; the `n` should stay — it reads
as a mail-merge failure, which is funnier and truer to the brand than any number. Operator fork
F6.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `plating` | 0.80 | 0.09 | The one thing Zhestokost is world-class at, on the one role a hull has. Above every shipped mean but Alakrita's branded 0.78 (Arctica fins) and AU's Vulcan core 0.75. The deviation is tight-ish because a slab of steel is hard to get wrong. Note the interaction: excellent workmanship on a design whose `CrossSection` band is 900→600 still yields an enormous signature — which is exactly the brand, a perfectly built bad idea. |

---

## 3. Item 0b — Aeronautics Unlimited all-rounder hull

### Design: `Medium Utility Hull` (`HullData`)

| Field | Value | Why |
|---|---|---|
| `Description` | "General-purpose hull. Balanced mass and armour, a mixed hardpoint layout, and interior space for tools and cargo." | Dry. |
| `HullType` | `Ship` | |
| `Mass` | 1900 | Below LonginusX (2500) — AU is the middle, and LonginusX is an Alakrita racing hull, so "lighter than the racer" is wrong for feel but right for role: this is a smaller ship, not a faster one. |
| `Price` | 4,200,000 | Well under LonginusX; AU's "resource acquisition" identity is a hull you buy early. |
| `Durability` | 400 | Slightly under LonginusX's 500. |
| `Shape` | 5×14, ~56 cells | Smaller than LonginusX's 66 — fewer hardpoints, more of them general-purpose. **Guess** on the mask. |
| `Armor` | 12 | Just above LonginusX's 10. |
| `Drag` | 0.12 | **Guess**, one sibling. |
| `Conductivity` | 30 | Near LonginusX's 32. |
| `ThermalResilience` | 2 | |
| Heat four | min 173.15, max 573.15, **optimum 288.15, plateau 60** | Valid: 288.15 ± 30 inside bounds. Wider than LonginusX's 48, far under the Noka's 100. Competent and forgiving; nothing is exceptional. |
| `Roles` | `plating` | |

`ReflectorData`:

| Stat | Min | Max | Terms |
|---|---|---|---|
| `CrossSection` | 550 | 220 | `Durability^1`, `Quality^2[plating]` |

Deliberately close to LonginusX's 500→150 — an unremarkable middle — with the same exponents, so
the only thing separating an AU hull from an Alakrita one on this axis is the product's
workmanship. That is the point of an all-rounder.

**Hardpoints:**

| Type | Shape | Count | Armor |
|---|---|---|---|
| `ControlModule` | 2×2 | 1 | 40 |
| `Reactor` | 2×2 | 1 | 0 |
| `AetherDrive` | 2×2 | 1 | 10 |
| **`Thruster`** | **2×2** | **2** | 10 |
| **`Shield`** | **2×2** | **1** | 15 |
| `Radiator` | 2×1 | 2 | 10 |
| `Energy` | 1×2 | 2 | 20 |
| `Launcher` | 1×3 | 1 | 25 |
| `Sensors` | 1×1 | 1 | 5 |

One shield to the Noka's two; two thrusters each. AU's hull is where a player first meets a
shield and the Zhestokost hull is where they meet two.

### Product: `Jason`, by Aeronautics Unlimited

The roster gives AU's voice ("Resource acquisition"; Britain; minimalist) and names the hull and
its upgrade the Argo, "because this ship is fire", but writes no line for Jason itself.

`Description` **(new text)**:

> Every expedition needs a ship that comes back. The Jason carries what you find, survives what
> finds you, and asks for nothing you cannot buy at the next station. Upgrade path available.

The last sentence is the Argo hook, left unbuilt.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `plating` | 0.62 | 0.12 | AU's shipped spread sits at 0.58–0.75 mean with 0.10–0.17 deviation across eight products (Earp 0.73/0.68, Vulcan 0.75/0.58, Traction 0.69/0.65, Skiron 0.64, Iapyx 0.61). 0.62 ± 0.12 lands in the middle of their own established band. That consistency *is* the brand: an all-rounder is a company with no peak. |

---

## 4. Item 1 — Shields

New role vocabulary, shared by both designs:

- **`emitter`** — the projector that turns stored energy into absorption. Owns `Efficiency`.
- **`reservoir`** — the reserve pool itself. Owns `Capacity`.
- **`regulator`** — the charge controller. Owns `EnergyUsage`, `RefillDuration`, `RestoreDuration`.

`ShieldData` semantics, from `Shield.cs`: a hit costs `damage × EnergyUsage` from the reserve;
a hit the reserve cannot cover **breaks** the shield, passes through **in full**, and empties
the reserve; a broken shield absorbs nothing until the reserve refills over `RestoreDuration`.
Absorbing adds `damage / Efficiency` heat.

### 4a. Design: `Heavy Shield Projector` (`GearData`, `Hardpoint = Shield`)

No shield exists to compare against. Every number is argued from the energy and heat scales in
§1 and from the gear it sits beside on a hull; all of it is first-in-kind and should be read as
the batch's least anchored content.

| Field | Value | Why |
|---|---|---|
| `Description` | "Projected barrier. Absorbs incoming damage from a stored energy reserve at a heat cost; a hit larger than the reserve breaks the barrier and passes through undiminished." | Dry, and it states the break rule, because a player who does not know that rule cannot read the item. |
| `Mass` | 420 | Between a reactor (Manhattan 3×3) and a thruster (150). A 2×2 defensive plant. |
| `Price` | 3,400,000 | Above every 2×2 gear item shipped (MoveOnPro, ChargeBlast, OK Disperser all ≤ 800k) and well under a hull. Megiddo's boss-faction signature item should be a purchase you plan for. **Weakly argued** — no shield price exists. |
| `Durability` | 250 | High; LonginusX is 500, most gear 50–100. |
| `Shape` | 2×2 | Matches the `Shield` hardpoints authored in §2/§3. |
| `SpecificHeat` | 2 | It absorbs heat by design; giving it thermal mass of its own is the difference between a shield that cooks and one that buffers. |
| `Conductivity` | 4 | Sheds into the hull grid readily so a radiator can reach it. |
| `ThermalResilience` | 8 | Radiators sit at 1–15; a shield lives in the same heat regime. |
| Heat four | min 173.15, max 573.15, **optimum 258.15, plateau 84** | Valid: 258.15 ± 42 = 216.15…300.15, inside bounds. Second-widest plateau in the batch after the Noka hull. Megiddo's brand is *holding*: the barrier does not degrade while the fight lasts. |
| `Roles` | `emitter`, `reservoir`, `regulator` | |

`ShieldData`:

| Stat | Min | Max | Terms | Why |
|---|---|---|---|---|
| `Efficiency` | 0.05 | 0.20 | `Heat^0.25`, `Durability^0.5`, `Quality^2[emitter]` | Heat per hit is `damage / Efficiency`. A 30-damage shot (a `6k Shooter` maximum) costs 600 heat at the bad end and 150 at the good end — the same order as a weapon's own 250–500 self-heat, so shielding is a real thermal decision rather than free. `Quality^2` makes the emitter the role a buyer pays for. |
| `EnergyUsage` | 8 | 3 | `Heat^0.25`, `Durability^0.25`, `Quality^1.5[regulator]` | Descending. Reserve cost per damage point. |
| `Capacity` | 180 | 520 | `Heat^0.125`, `Durability^0.25`, `Quality^2[reservoir]` | **Power request — no `PowerSupply` term.** At the good end, 520 reserve ÷ 3 per damage = 173 damage absorbed before breaking (≈ 6 maximum `6k Shooter` hits). At the bad end, 180 ÷ 8 = 22 damage (one hit, maybe). That spread is the whole reason to care whose shield you bought. Sized against PotaT+-'s 50–150 capacitor: a shield is a bigger buffer than a general capacitor, by roughly 3×. |
| `RefillDuration` | 12 | 5 | `Heat^0.25`, `Durability^0.25`, `Quality^1.5[regulator]` | Descending. **No `PowerSupply` term** (request-read). Best case 520/5 = 104 power-units/s sustained — a fifth of a Manhattan's 500–1000/s, a ship-defining draw on a MoveOnPro (25–125/s). Shields are why you buy the big reactor. |
| `RestoreDuration` | 34 | 16 | `Durability^0.25`, `Quality^2[regulator]` | Descending. **No `PowerSupply` term.** 16 s naked at best, 34 s at worst: the punish window the operator asked for. Roughly 3× the refill duration, deliberately — breaking a shield has to be worth aiming for. No `Heat` term: the punish window should not also be a heat-management puzzle. |

### 4a Product: `Migdal`, by **Megiddo**

Megiddo has **no `Faction` record in the catalog** — see §9 F2. The product cannot be authored
until one exists.

The roster gives Megiddo a concept ("defensive technology", "Space Zionists", Hebrew, choral)
and no written items at all — its sheet has headers and nothing under them. All flavour is
therefore **(new text)**:

> A wall is not a weapon. It does not need to be clever, or fast, or new. It needs to be there
> when the sky opens, and it needs to be there afterwards. Ours have been.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `emitter` | 0.88 | 0.04 | The single highest mean and lowest deviation in the catalog. Today's ceiling is 0.78 mean (Arctica's branded fins) and today's floor for deviation is 0.07. Megiddo is a boss faction whose entire existence is one capability; its signature item should sit one clear step above every merely-good product, on both axes. |
| `reservoir` | 0.82 | 0.05 | Same logic, one notch down: the pool is the second thing they are selling. |
| `regulator` | 0.74 | 0.06 | Still above almost everything shipped. Megiddo has no weak part — the brand is that there is no corner cut — so the spread between its roles is small and the *deviation* is what sets it apart, not a peak. Choral: everything in tune. |

### 4b. Design: `Light Shield Projector` (`GearData`, `Hardpoint = Shield`)

The cheaper alternative. Maker: **Finch Cybernetics** — the roster's second-named shield maker,
a demo faction with influence 5 and zero products, whose written identity is "the highest
quality and lowest cost\*" with the asterisk reading "when compared to units of higher cost".
Finch's brand is *variance*, which is a thing `ProductRole.StandardDeviation` can actually say.

| Field | Value | Why |
|---|---|---|
| `Description` | "Compact projected barrier. Lower reserve and slower recovery than a heavy projector, in a smaller envelope." | Dry. |
| `Mass` | 160 | |
| `Price` | 620,000 | ~18% of the heavy projector. "Lowest cost" has to be visible in the price, not only the flavour. |
| `Durability` | 60 | Ordinary gear durability (most shipped gear is 50–100). |
| `Shape` | 1×2 | Fits inside the 2×2 `Shield` hardpoints authored in §2/§3, and leaves the rest of the hardpoint free — which is a real, legible tradeoff the fit rule already supports. |
| `SpecificHeat` | 1 | |
| `Conductivity` | 2 | |
| `ThermalResilience` | 2 | |
| Heat four | min 200, max 440, **optimum 268, plateau 28** | Valid: 268 ± 14 inside bounds. A third of the Megiddo plateau. It holds, then it does not. |
| `Roles` | `emitter`, `reservoir`, `regulator` | Same vocabulary. |

`ShieldData`:

| Stat | Min | Max | Terms |
|---|---|---|---|
| `Efficiency` | 0.03 | 0.11 | `Heat^0.5`, `Durability^0.5`, `Quality^2[emitter]` |
| `EnergyUsage` | 11 | 5 | `Heat^0.25`, `Durability^0.25`, `Quality^1.5[regulator]` |
| `Capacity` | 70 | 210 | `Heat^0.125`, `Durability^0.25`, `Quality^2[reservoir]` |
| `RefillDuration` | 16 | 8 | `Heat^0.25`, `Durability^0.25`, `Quality^1.5[regulator]` |
| `RestoreDuration` | 45 | 26 | `Durability^0.25`, `Quality^2[regulator]` |

Every band is a worse slice of the heavy projector's, with two deliberate exceptions: the
`Heat^0.5` on `Efficiency` (it degrades under heat twice as fast — the cheap part is the thermal
design) and the `RestoreDuration` floor of 26 s, which is *worse than the heavy projector's
worst case*. A broken Finch shield is a long walk home. Best-case sustained draw is
210/8 = 26/s, which a MoveOnPro can actually feed — that, not the absorption, is what makes it
the early-game shield.

### 4b Product: `Thorax`, by Finch Cybernetics

The roster's Finch voice is the asterisked claim (the Galapagos hull, "of the highest quality
and lowest cost\*"). **(new text)**, in that register:

> The Thorax delivers heavy-projector protection\* at a fraction of the price. Fully compatible
> with the Finch upgrade path.
>
> \*per credit spent

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `emitter` | 0.72 | 0.19 | The clever part is genuinely good — Finch are transhumanists, not hacks — but the deviation is above anything shipped (today's maximum is 0.18). You are buying a lottery ticket with good odds. |
| `reservoir` | 0.44 | 0.21 | The cheap part, and the widest spread in the catalog. The reservoir is where Finch saved the money and where the asterisk lives. |
| `regulator` | 0.61 | 0.20 | |

Reading the two shields side by side: Megiddo's worst role (0.74 ± 0.06) is better and more
certain than Finch's best (0.72 ± 0.19). That is the price difference made mechanical, and it is
also the first pair in the catalog where two products of *different* designs in the same kind
encode opposite manufacturing philosophies rather than an arbitrary seed.

---

## 5. Item 2 — Thrusters

Shared vocabulary from `deep space burnout`: **`nozzle`** (thrust, plume visibility),
**`injector`** (the fuel/power feed, whose byproduct is heat).

Comparables — the only two thruster designs in the catalog:

| | Thrust | Visibility | Heat | EnergyUsage | Mass | Price | Dur | Heat four |
|---|---|---|---|---|---|---|---|---|
| `deep space burnout` (DME) | 250k→1M `H^.0625 D^.5 Q^2[nozzle] P^1` | 100→400 `H^.25 D^.25 Q^1.5[nozzle]` | 4000→12000 `D^.125 Q^1[injector]` | 0→0, no terms | 150 | 200,000 | 100 | 200/400/251/24 |
| `Large Drive` (unsold) | 250k flat | 100 flat | 2500 flat | 0→0 | 100 | 100,000 | 100 | 200/400/251/24 |

Two things inherited, both noted rather than changed:

- **Shipped thrusters request zero power.** `ThrusterData.EnergyUsage` is authored 0→0 with no
  terms on both. Authoring a real value on the new thrusters would make them the only thrusters
  that cost power. Operator fork **F4**; this spec authors 0→0 to match, so the batch changes no
  balance it was not asked to change.
- **Thruster `Visibility` ascends with quality** (100→400, `Quality^1.5`) — a better-built
  nozzle is *brighter*. Read as "plume brightness tracks output", that is coherent; read as
  "quality makes gear worse", it is a bug. One data point cannot settle it. Operator fork **F5**;
  this spec follows the shipped convention and ascends.

### 5a. Design: `High-Output Thruster` (`GearData`, `Hardpoint = Thruster`)

| Field | Value | Why |
|---|---|---|
| `Description` | "High-thrust drive. Narrow thermal band, low durability, high output. Requires close thermal management." | Dry. |
| `Mass` | 90 | Below both siblings (150, 100). Alakrita is light and fragile. |
| `Price` | 900,000 | 4.5× deep space burnout. "Ultra high-end… spare parts are hard to come by." |
| `Durability` | 35 | Against 100 on both siblings. The lowest durability on any equippable in the catalog. This is the single number that says "failure prone". |
| `Shape` | 2×2 | Matches both siblings and the new `Thruster` hardpoints. |
| `SpecificHeat` | 1.5 | Less thermal mass than deep space burnout's 2.5 — it heats fast. |
| `Conductivity` | 6 | Slightly above 5: dumps into the hull aggressively, which is a liability as much as a feature. |
| `ThermalResilience` | 2 | Against deep space burnout's 15. |
| Heat four | min 232, max 362, **optimum 268, plateau 10** | Valid: 268 ± 5 = 263…273, inside bounds. The narrowest plateau and narrowest bounds in the catalog by a wide margin (nearest shipped is `The Bat` at 18, `plight` at 16.2). "The perfect alignment of the whole system makes them failure prone" written as a number: outside a 10K window it is taking wear every second. |
| `Roles` | `injector`, `nozzle` | |

`ThrusterData`:

| Stat | Min | Max | Terms | Why |
|---|---|---|---|---|
| `Thrust` | 400,000 | 1,500,000 | `Heat^0.25`, `Durability^0.75`, `Quality^2[nozzle]`, `PowerSupply^1` | Top end 1.5× deep space burnout's, floor 1.6× its floor. `Heat^0.25` (vs `^0.0625`) and `Durability^0.75` (vs `^0.5`) are the fragility: it is the fastest thruster while perfect and falls off harder than anything else as it heats and wears. `PowerSupply^1` matches both siblings and is ascending, so the multiplier rule is safe. |
| `Visibility` | 300 | 900 | `Heat^0.25`, `Durability^0.25`, `Quality^1[nozzle]` | Loudest plume in the game. `Quality^1` rather than `^1.5`: Alakrita is not spending workmanship on the exhaust signature, because their customers do not care. |
| `Heat` | 6,500 | 19,000 | `Durability^0.125`, `Quality^1[injector]` | Above deep space burnout's 4000→12000 across the band. The injector is the part Alakrita did not fix. |
| `EnergyUsage` | 0 | 0 | none | Matches shipped thrusters. See F4. |

### 5a Product: `Victoire`, by Alakrita

`Description`, verbatim roster line plus the tagline:

> The most responsive thruster on the market, for the most irresponsible of racing pilots.
> Speed. Elegance. *Alakrita*.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `nozzle` | 0.91 | 0.06 | Highest single mean in the batch and in the catalog. Alakrita's established pattern is already visible in the one branded row shipped — Arctica's `fins` 0.78 ± 0.07, high on the thing the brand claims. A thruster is Alakrita's *actual* specialisation where a radiator is adjacent, so the peak goes higher and the deviation stays as tight. |
| `injector` | 0.38 | 0.16 | Below every shipped mean (today's floor is 0.45). The injector decides `Heat`, and heat is what kills the pilot. High on what makes it fast, low on what keeps it alive — stated as the batch's clearest brand contrast with Zhestokost. The wide deviation is the spare-parts problem. |

The two together are the design intent made buyable: 0.91 nozzle against a `Quality^2` thrust
term gives near-maximum thrust, while 0.38 injector against a `Quality^1` heat term leaves the
heat near the middle of a band that already tops out at 19,000. Fast, and cooking.

### 5b. Design: `Light Thruster` (`GearData`, `Hardpoint = Thruster`)

| Field | Value | Why |
|---|---|---|
| `Description` | "Low-cost drive. Modest thrust across a wide thermal band, with low durability." | Dry. |
| `Mass` | 70 | Lightest thruster. |
| `Price` | 140,000 | Between `Large Drive` (100,000, unsold and unbranded) and deep space burnout (200,000). The cheap *branded* option, and a seventh of the Alakrita. |
| `Durability` | 30 | Lower even than the Alakrita. Finch's cost saving is materials. |
| `Shape` | 2×2 | |
| `SpecificHeat` | 1 | |
| `Conductivity` | 4 | |
| `ThermalResilience` | 1 | Catalog floor. |
| Heat four | min 205, max 445, **optimum 262, plateau 46** | Valid: 262 ± 23 inside bounds. Nearly double deep space burnout's plateau of 24 and 4.6× the Alakrita's. Finch's trick is not precision, it is *tolerance*: the cheap thruster is the forgiving one. That inversion is the most interesting thing in this pair. |
| `Roles` | `injector`, `nozzle` | |

`ThrusterData`:

| Stat | Min | Max | Terms |
|---|---|---|---|
| `Thrust` | 180,000 | 650,000 | `Heat^0.125`, `Durability^0.375`, `Quality^1.5[nozzle]`, `PowerSupply^1` |
| `Visibility` | 150 | 380 | `Heat^0.25`, `Durability^0.25`, `Quality^1[nozzle]` |
| `Heat` | 3,200 | 8,500 | `Durability^0.125`, `Quality^1[injector]` |
| `EnergyUsage` | 0 | 0 | none |

Ceiling is 43% of the Alakrita's and 65% of deep space burnout's. Heat is the lowest of any
thruster. The gentle exponents (`Heat^0.125`, `Durability^0.375`) mean it degrades slowly — a
cheap part that keeps working, which is the honest half of Finch's claim.

### 5b Product: `Prokope`, by Finch Cybernetics

`Description`, verbatim:

> 67% fatal and 100% fun.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `nozzle` | 0.66 | 0.22 | Middle mean, catalog-widest deviation. Matches the Thorax's Finch signature: the parts are sometimes excellent. |
| `injector` | 0.35 | 0.24 | The lowest mean and highest deviation in the catalog. The injector decides heat; heat decides whether the 67% figure was marketing. |

---

## 6. Item 4a — Finch passive sensor

`SensorData` carries both the passive half (`Sensitivity`, `SensitivityCurve`) and the active
ping (`PingBoost`, `PingEnergy`, `PingVisibility`, `PingRange`, `PingCooldown`, `PingDuration`,
`PingRadiusExponent`) in one behaviour. A *passive* sensor is therefore a `SensorData` with the
ping stats authored dead — which is a shape the catalog already uses (`Iapyx` and `Skiron`
author four of five radiator stats flat).

### Design: `Passive Sensor Array` (`GearData`, `Hardpoint = Sensors`)

Comparables: `The Bat` (Sensitivity 1→2, 2×2, mass 1000, price 7.5M) and `not if i see you
first` (Sensitivity 0.25→1.5, 1×1, mass 200, price 250k).

| Field | Value | Why |
|---|---|---|
| `Description` | "Passive sensor array. High sensitivity, no active ping; emits no signature of its own." | Dry, and it names the absence, which is the item. |
| `Mass` | 240 | Near `not if i see you first` (200); it is a 1×1 part with more receiver in it. |
| `Price` | 900,000 | 3.6× `not if i see you first`, an eighth of `The Bat`. The best passive sensitivity in the game should not be the cheapest sensor. |
| `Durability` | 50 | Matches both siblings. |
| `Shape` | 1×1 | Fits LonginusX's existing `Sensors` hardpoint — the reason this item is playable today. |
| `SpecificHeat` | 1 | |
| `Conductivity` | 1 | |
| `ThermalResilience` | 1 | |
| Heat four | min 180, max 400, **optimum 236, plateau 44** | Valid: 236 ± 22 inside bounds. Low optimum, wide plateau: a receiver wants to be cold, and Finch built one that tolerates not being. Compare `The Bat`'s 209/18 (cold and fussy) and `not if i see you first`'s 251/24. |
| `Roles` | `receiver` **only** | The active-ping stats are authored flat with no `Quality` term, so by the Cut 7 rule (a role is pointed at a stat only when it carries `Quality` *and* actually varies) there is no `emitter` role to declare. Declaring one would be authoring fiction. |

`SensorData`:

| Stat | Min | Max | Terms | Why |
|---|---|---|---|---|
| `Sensitivity` | 0.6 | 3.0 | `Heat^0.5`, `Durability^0.4`, `Quality^2[receiver]` | Top end 1.5× `The Bat`'s 2.0 and double `not if i see you first`'s 1.5 — the best passive sensitivity in the game, which is Finch's stated specialisation. `Heat^0.5` is steep: a hot receiver is a deaf receiver, so this sensor pushes the player toward a cool ship. `Quality^2` (vs `The Bat`'s `^1.5`) makes the receiver the role you buy. |
| `SensitivityCurve` | copy `not if i see you first`'s | | | A `BezierCurve`; no basis to author a new shape and no reason to. |
| `PingBoost` | 0 | 0 | none | No active ping. |
| `PingEnergy` | 0 | 0 | none | **Power request — must carry no `PowerSupply` term** (it carries no term at all). A passive sensor drawing zero bus power is the correct reading of the item. |
| `PingVisibility` | 0 | 0 | none | |
| `PingRange` | 0 | 0 | none | |
| `PingCooldown` | 0 | 0 | none | |
| `PingDuration` | 0 | | | |
| `PingRadiusExponent` | 0.5 | | | Matches both siblings. |

Hands should confirm that `Sensor.Execute` handles an all-zero ping without dividing by
`PingCooldown` or `PingRange`; if it does not, that is a one-line guard, not a reason to author
a token ping. Flagged as **F7**.

### Product: `ChirOptos`, by Finch Cybernetics

`Description`, verbatim fragment from the roster, completed in voice:

> ChirOptos, with patented EchoLock — so you always fly under the radar.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `receiver` | 0.86 | 0.11 | The highest non-Megiddo mean in the batch. Passive sensors are Finch's *named* specialisation in the roster, and this is the flagship — it should beat DME's `not if i see you first` receiver (0.75), which is a sensor made by an explosives company. The deviation is tighter than Finch's cheap gear (0.19–0.24 above) but nowhere near Megiddo's 0.04: Finch's QC improves when the product is the one they are known for, and never becomes German. |

Finch's three products in this batch are the brand argued three ways: 0.86 ± 0.11 on the thing
they are for, 0.72 ± 0.19 on a clever budget product, 0.35 ± 0.24 on the part they openly did
not fund. That is a manufacturer with a personality, which is what the roles cut said nothing in
the catalog has yet.

---

## 7. Items 4b and 5 — the cloak and the capacitor

### 7a. Adrasteia's Enigma Device — buildable, but not as invisibility

**It can be expressed with landed behaviours, and it cannot be a cloak.** The honest version is
a *signature dampener*, and the spec below is written as one. If the operator wants invisibility,
that needs a mechanism that does not exist; §9 F8 names it.

What exists:

- `Entity.Visibility` is `VisibilitySources.Values.Sum()` (`Entity.cs:131`) — a sum of
  non-negative contributions. Nothing subtracts.
- `VisibilityData` only **adds** a source (`Visibility.cs`). It cannot hide anything.
- `StatModifierData` with `Type = Multiplier` attaches a scale factor to any named
  `(BehaviorData, PerformanceStat)` on any equipped item, through `StatResolver.ScaleModifier`,
  which multiplies (default 1). It is shipped and in use (MoveOnPro carries one). Targeting
  `ReflectorData.CrossSection` reaches the hull, because `EquippedHull` is in `Entity.Equipment`.
- The hull's cross-section **is the dominant source**: `CrossSection` is 150–900 across the
  catalog, while the entity's own thermal radiation source is
  `Σ_border pow(T, 3) × 1e-8` — about 11 total for a 40-cell border at 300 K, and still under
  150 at 600 K (`Entity.cs:1090-1111`, `HeatRadiationExponent 3`, `HeatRadiationMultiplier 1e-8`).

So multiplying cross-section down by 0.2 removes roughly 80% of a ship's detectability, which is
a real and significant item. What it cannot do:

- It cannot suppress thermal radiation, because that source is computed in `Entity` from global
  settings and is not a `PerformanceStat` — nothing can modify it. A dampened ship running hot
  is still lit up, which is thematically excellent and mechanically a ceiling on the item.
- It cannot suppress weapon or thruster plume visibility without a **separate**
  `StatModifierData` per target stat (one `StatReference` each). Firing breaks the effect, which
  is the right behaviour and comes free.
- There is no "cannot be locked" state. Detection is one scalar against
  `TargetDetectionInfoThreshold` (0.1), so this is a lower number, not a different state.

#### Design: `Signature Dampener` (`GearData`, `Hardpoint = Tool`)

`Tool` items place anywhere in interior space, so this is playable on LonginusX today.

| Field | Value | Why |
|---|---|---|
| `Description` | "Actively cancels the hull's reflected radar return. Does not conceal thermal emission, weapon discharge, or thruster plume." | Dry, and the second sentence is load-bearing: it tells the player the item's actual boundary. |
| `Mass` | 180 | |
| `Price` | 4,800,000 | The most expensive gear item in the batch and above every shipped non-hull design except `The Bat` (7.5M). Adrasteia is expensive and secretive; "harder to obtain than a good reputation" is the roster's own phrasing for their gear. |
| `Durability` | 40 | "Light and Fragile, High Tech" — the roster's own construction note for every Adrasteia item. |
| `Shape` | 2×2 | |
| `SpecificHeat` | 1 | |
| `Conductivity` | 2 | |
| `ThermalResilience` | 1 | |
| Heat four | min 150, max 380, **optimum 214, plateau 30** | Valid: 214 ± 15 inside bounds. The coldest optimum in the batch, one step toward the negent register `docs/stats-and-power-target.md` describes for Adrasteia without being negent gear. |
| `Roles` | `projector`, `controller` | `Tool` roles are per-design (Cut 7). *projector* — the cancellation emitter, which decides how much return is suppressed. *controller* — the timing electronics, which decide what it costs to run. |

Behaviours — three, because `StatModifierData` names one target stat each:

1. `StatModifierData` — `Stat = { Target: "ReflectorData", Stat: "CrossSection" }`,
   `Type = Multiplier`, `RequireBehavior` empty,
   `Modifier`: Min **1.0**, Max **0.12**, terms `Heat^0.5`, `Durability^0.5`,
   `Quality^2[projector]`.
   Descending (1.0 = no effect, 0.12 = 88% suppression), so **no `PowerSupply` term** — see §1;
   a power term here would make an unpowered ship perfectly invisible. `Heat^0.5` is steep: run
   hot and the dampener stops working, which couples the two halves of stealth without needing
   a new mechanism.
2. `EnergyDrawData` — `EnergyDraw`: Min 90, Max 40, terms `Heat^0.25`,
   `Quality^1.5[controller]`; `PerSecond = true`.
   Descending. **Power request — no `PowerSupply` term** (validator would refuse it). 40–90/s
   is a third to most of a MoveOnPro's whole output: staying dark costs you your guns.
3. `HeatData` — `Heat`: Min 900, Max 400, terms `Durability^0.25`,
   `Quality^1.5[controller]`; `PerSecond = true`.
   Descending. Cancellation is not free, and the heat it makes is what eventually reveals you
   through the one source the item cannot touch. That loop is the item's design.

**Weakly argued:** no cloak, dampener or signature item exists to compare against. The
`EnergyDraw` figure is anchored to reactor output and the `Heat` figure to `Cockpit 2x2`'s 200/s
and thruster heat; the multiplier band is anchored only to the cross-section range it multiplies.

#### Product: `Enigma Device`, by Adrasteia

`Description`, verbatim from the source spreadsheet:

> A stealth device par excellence. Comes in an unmarked box from an unknown address, with no
> instructions and no receipt. A small uv glow smiley sticker was added after numerous
> complaints of customers being unable to find the device once switched on.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `projector` | 0.84 | 0.07 | Expensive and secretive: the thing works, and it works consistently, because Adrasteia's entire market position is that other corporations cannot reproduce it. Second only to Megiddo's emitter. Consistent with their one shipped product, ColdFire's focusing array at 0.68 ± 0.10 — a deliberate step up, because ColdFire is an admitted underperformer ("does less damage than other corp's counterparts") while the Enigma Device is the flagship. |
| `controller` | 0.69 | 0.16 | No instructions, no receipt: the usability half is where the money did not go. Good, not exceptional, and inconsistent. |

### 7b. NiteLife's Cottage-pi capacitor

#### Design: `Large Capacitor` (`GearData`, `Hardpoint = Tool`)

The only capacitor design is `PotaT+-` (1×1, Tool, mass 75, price 75k, Capacity 50→150,
Efficiency 0.5→0.99, roles `converter`/`storage cell`). Cottage-pi must be a **separate design**,
not a second product on PotaT+-: both would be NiteLife, and `census` refuses duplicate
`(maker, design)` pairs because `Brand()`'s tie-break would then be arbitrary.

| Field | Value | Why |
|---|---|---|
| `Description` | "High-capacity power buffer. Stores bus surplus for peak draw at a conversion loss." | Dry. |
| `Mass` | 210 | ~3× PotaT+- for 4× the cells and ~3.5× the capacity. |
| `Price` | 340,000 | 4.5× PotaT+-. |
| `Durability` | 60 | Near PotaT+-'s 50. |
| `Shape` | 2×2 | `Tool`, so it competes with cargo and gear for interior space — which is the cost of carrying one. |
| `SpecificHeat` | 1.5 | |
| `Conductivity` | 1 | Matches PotaT+-. |
| `ThermalResilience` | 3 | Above PotaT+-'s 1. |
| Heat four | min 200, max 500, **optimum 279, plateau 54** | Valid: 279 ± 27 inside bounds. PotaT+- is 200/500/276.5/36; the same band with a 50% wider plateau. NiteLife's whole pitch is "we keep the lights on" — their flagship buffer should be the one that does not mind the heat. |
| `Roles` | `converter`, `storage cell` | Shared with PotaT+-. |

`CapacitorData`:

| Stat | Min | Max | Terms | Why |
|---|---|---|---|---|
| `Capacity` | 180 | 520 | `Heat^0.125`, `Durability^0.0625`, `Quality^2[storage cell]` | 3.5× PotaT+- across the band. Same exponents as PotaT+-, deliberately: this is the same technology, larger. Note it lands on the same numbers as the heavy shield's reserve, which is intentional — one Cottage-pi is roughly "a second shield's worth of buffer", a legible unit. |
| `Efficiency` | 0.55 | 0.97 | `Heat^0.0625`, `Durability^0.25`, `Quality^2[converter]` | Marginally worse ceiling than PotaT+-'s 0.99 and a better floor than its 0.5. Bigger buffers lose a little more at the top; NiteLife's volume product is more consistent than its cheap one. |

`Capacitor` is **not** an `IPowerConsumer` — the bus reads `ResolveCapacity()` — so neither stat
is a power request and neither needs the `PowerSupply` exclusion. Neither takes one anyway:
`Capacity` ascends but a capacitor that vanishes under brownout would remove the buffer exactly
when it is needed.

#### Product: `Cottage-pi`, by NiteLife Energy

The roster writes no line for Cottage-pi itself but fixes its role precisely: the meson
reservoir that MoveOnPro, "the only muon catalyst fusion reactor on the market", requires, and
which only NiteLife sells. Tagline: "We keep the lights on, so you can keep on". **(new text)**:

> Genuine NiteLife meson reservoir. Accept no substitute — there isn't one. Keeps the lights on,
> so you can keep on.

Role quality:

| Role | Mean | Dev | Why |
|---|---|---|---|
| `storage cell` | 0.79 | 0.08 | Power storage is NiteLife's stated specialisation and this is their flagship, so it should decisively beat their own PotaT+- storage cell (0.53 ± 0.11). 0.79 sits at the top of the non-boss band (Arctica's branded 0.78) with a tighter deviation than anything NiteLife ships. |
| `converter` | 0.55 | 0.14 | *Lower* than PotaT+-'s converter (0.67). Deliberate and worth stating: NiteLife's business model is selling the reservoir refill, not the conversion electronics, so the big unit stores beautifully and converts adequately. A second product raising `converter` is the obvious future market segment, which is exactly what the roles cut wants to demonstrate. |

---

## 8. Item 6 — Miss Terri's consumables: blocked, do not author

**Verified: consumables are unreachable in play, and authoring one would produce a crash rather
than content.**

- `ItemManager.CreateInstance(int lot)` (`ItemManager.cs:173`) branches on
  `EquippableItemData` and falls through to `CompoundCommodity` for everything else. There is
  **no `ConsumableItemData` branch.** A lot whose design is a consumable mints a
  `CompoundCommodity` whose `Data` points at a `ConsumableItemData` — wrong instance type, no
  error, no log.
- `Entity.TryActivateConsumable` (`Entity.cs:437`) then does an **unchecked cast**:
  `var item = (ConsumableItem) bay.ItemsOfType[key].First();`. The instance in cargo is a
  `CompoundCommodity`, so this throws `InvalidCastException` the first time a player uses the
  item. Not a silent no-op: a crash on use.
- `CreateInstance(FactionProductData)` (`ItemManager.cs:191`) delegates to the same path, so a
  product selling a consumable inherits the gap. The catalog currently holds **zero**
  `ConsumableItemData` records, which is why nothing has hit this.
- The gap is already known and worked around in tests:
  `tests/Aetheria.Shared.Tests/LoadoutTests.cs:731-735` hand-constructs a `ConsumableItem`
  with a comment saying exactly this.

So: **no Miss Terri's content in this batch.** The fix is small (one branch in `CreateInstance`
returning `new ConsumableItem { Data = l.Design, Lot = lot }`, plus a decision about what
`Stackable` means for a lot), but it is a code cut and it is not this pass's to make. It should
be its own cut before any consumable is authored; §9 F9.

Worth noting for whoever takes it: `ConsumableItemData` carries **no heat response** (it derives
from `CraftedItemData`, not `EquippableItemData`), so the thermal four do not apply, and its
behaviours run through `ConsumableItemEffect` with `Item == null` — which is why `Shield`,
`Visibility` and others all carry a `ConsumableItemEffect` constructor. A Miss Terri's stat
booster would be a `StatModifierData` on a `ConsumableItemData` with a `Duration` and an
`Effectiveness` curve, and `StatSource.ConsumableProgress` exists precisely for it. The content
is ready to write the moment the mint path is.

---

## 9. Blockers, mechanism gaps, and operator forks

**F1 — Only one spare ship prefab exists, and two hulls are specified.**
`ZoneRenderer.cs:295` instantiates `EngineAssets.Load<GameObject>(hullData.Prefab)` and calls
`GetComponent<ShipInstance>()`. A hull with no prefab GUID renders nothing and null-refs. The
project holds exactly two ship prefabs: `Assets/Content/Prefabs/Ships/Longinus.prefab` (used by
LonginusX, GUID `4a3db6090789ca24cb57c43666754017`) and
`Assets/Content/Prefabs/Ships/Djinni.prefab` (GUID `79024f635e46f5546b670b58b07fa037`,
**unused**, carries a `ShipInstance` component and named hardpoint transforms: eight thruster
mounts, `Fire Point L`, `Barrel R`, `Nostril L`/`R`, a `Hardpoints` group).
**Fork:** which hull gets the Djinni, and does the other ship without art (invisible in the
zone) or wait for a model? The Djinni's eight thruster transforms and two nostril mounts read
closer to the AU utility hull than to a Zhestokost slab, but that is an art judgement.

**F2 — Megiddo has no `Faction` record.** The catalog holds twelve factions and Megiddo is not
one of them (`AetherDb factions`). Authoring the Migdal requires creating it first:
`Name`, `ShortName`, `Description`, `InfluenceDistance` (boss-scale; Lucent and Zhestokost are
6, the median is 4), `PrimaryColor`/`SecondaryColor`, `Personality` weights, and a
**`GeonameFile`** — every shipped faction has one, and `AetherDb factions` reports it as a
generation-critical link. Megiddo's is Hebrew place-names; the `NameFile` record does not exist
either. `Logo` is an asset GUID with no asset. `BossHull` is unset on every faction today, so
leaving it unset is normal. **Fork:** create Megiddo as part of this batch, or give the heavy
shield to a faction that exists (Zhestokost is the roster's plausible fallback: "no such thing
as overkill" reads fine on a shield) and hold Megiddo for a proper faction pass.

**F3 — `PowerSupply` on a descending stat is a footgun the validator does not catch.** Under
the 2026-09-19 multiplier ruling, a `PowerSupply` term on a stat authored `Min > Max` means
zero supply resolves to zero, which for `CrossSection`, `PingVisibility`, `PingCooldown`,
`EnergyUsage` or `WasteHeat` is the *best possible* value. Nothing refuses it. Worth a
validation rule ("a `PowerSupply` term on a stat whose `Min > Max` is an authoring error") in
whichever cut widens the request-independence check. No shipped stat currently trips it —
checked all 51 designs.

**F4 — Shipped thrusters request zero power.** Both `deep space burnout` and `Large Drive`
author `ThrusterData.EnergyUsage` 0→0 with no terms, so thrust is free and the `PowerSupply^1`
term on `Thrust` can only ever read a full grant. This spec matches them rather than making the
two new thrusters uniquely expensive. **Fork:** author real thruster draw across all four in a
separate balance pass, or leave thrust free.

**F5 — Thruster `Visibility` ascends with quality.** `deep space burnout` authors 100→400 with
`Quality^1.5[nozzle]`: a better-built nozzle is brighter. Coherent if plume brightness tracks
output; a bug if quality is meant to improve every stat. One data point. This spec follows the
convention. **Fork:** confirm or invert, catalog-wide.

**F6 — The Noka MKI ad copy says "ColferV" and contains a literal `n`.** Substituting
"Zhestokost" is required; whether to fix "n mounts" is a taste call. Recommendation: keep it.

**F7 — An all-zero ping may not be safe.** `Sensor.Execute` should be read before the passive
sensor is authored, to confirm nothing divides by `PingCooldown` or `PingRange`. A one-line
guard if it does; not a reason to author a token ping.

**F8 — There is no cloaking mechanism, only signature reduction.** Named precisely, what does
not exist: (a) any way to reduce `Entity`'s own thermal-radiation visibility source, which is
computed in `Entity.cs:1090-1111` from global settings and is not a `PerformanceStat`; (b) any
"undetectable" or "cannot be locked" state — detection is one scalar against a threshold; (c)
any single item-level visibility multiplier, so suppressing several sources needs one
`StatModifierData` per target stat. §7a's dampener is what the landed behaviours can honestly
express. **Fork:** ship the dampener as specified, or hold the Enigma Device until a real
stealth mechanic exists.

**F9 — Consumables cannot be minted or used.** §8. Blocks all Miss Terri's content behind a
small code cut.

**F10 — A hull has exactly one role, by construction.** `Armor`, `Drag`, `Mass` and
`Durability` are plain floats on `HullData`, not `PerformanceStat`s, so the only quality-bearing
stat a hull owns is `ReflectorData.CrossSection` → `plating`. Every hull product in the game can
therefore differ in exactly one dimension. If hulls are meant to carry manufacturer identity the
way gear does, `Armor` and `Drag` need to become stats. Not this batch; named because this batch
is the first time two hulls exist to compare.

---

## 10. Products for existing unsold designs

Fourteen designs no product sells (`AetherDb census`). Not all are cheap wins: six have **no
behaviours at all** and two more are authored entirely flat, so a product on them sells an item
that does nothing.

**Author now — four, all with real stats and an already-written voice:**

| Design | Kind, shape | Maker | Why | Flavour |
|---|---|---|---|---|
| `plight` | Energy, 1×1 | **Lucent Media** | The cheapest win in the catalog: its `Description` is *already* the roster's verbatim TaranisLG lightning-gun copy, naming DragOnBreath as "a subsidary of Lucent Media". The design exists, is fully authored with `focusing array`/`power coupling` roles, and **fits LonginusX's 1×2 `Energy` hardpoints today**. Lucent ships one energy weapon; lasers are their stated specialisation. | Move the existing text to the product as `TaranisLG`; give the design a dry one. |
| `pswarm` | Launcher, 1×1 | **Aeronautics Unlimited** | Dumbfire swarms are AU's stated specialisation and the roster has the name and line written: *Leonid*, "A rain of pain that falls mainly on space planes." Fully authored with `guidance system`/`thruster`/`warhead` roles, and **fits LonginusX's 1×3 `Launcher` hardpoints today**. Also fixes a content bug: `pswarm`'s current description is DME's `scorched void policy` lyric, copied onto the wrong design. | Roster line, verbatim. |
| `Core Power` | Reactor, 2×2 | **Rossum & Douglas** | Doubles the reactor choice on the only playable hull: LonginusX's `Reactor` hardpoint currently has two matching designs and one seller. R&D is "Mom's Friendly Robot Company", Germany, muzak — and the design's existing description is "It gets the job done, but that's all." A beige reactor is already written; it just needs the right company's name on it. R&D ships two products. | Product text in the reassuring-corporate register. |
| `Large Drive` | Thruster, 2×2 | **Lightsail Express** | The roster writes Lightsail a thruster (`True North`) and Lightsail's whole identity is "speed and reliability, is in actual fact neither". `Large Drive` is authored entirely flat — every stat `Min == Max` — which is *perfect* for a product whose joke is that it is unremarkable, and it declares no roles, so no role spread is needed. Becomes equippable the moment §2/§3's hulls land. | "We'll lose our lives before we lose your cargo" is the tagline; the thruster is `True North`. |

**Defer — ten.** `Autocannon` and `SRMM72`/`LRMM72` are authored fully flat with no roles, so a
product adds a name and nothing else, and none of them fits a hardpoint that exists (Turret's
8-cell `Ballistic` mounts match no design at all). `Tractor Beam`, `Refinery`, `Shipyard`,
`Assembly Line`, `Surface Ore Extractor`, `Deep Ore Extractor` and `Industrial Thermostatic
Heater` have `Price = 0`, `Durability = 0` and (bar the heater) **no behaviours whatsoever** —
they are placeholders, not designs. The tractor beam in particular is named in the wishlist as
Ewan Hart's "Space Tractor: It's a tractor. In space.", and Ewan Hart has influence and zero
products, so it is the most tempting of these — but the design carries no behaviour, so selling
it would put a dead object in a player's hold. It becomes a real cheap win the moment the pickup
capability (`docs/shield-presentation-contract.md`) gives it something to do.

---

## 11. What a later pass writes

In one commit, through `Aetheria.Shared`'s own types via `CultRecordRefs.Upsert` (so
`ValidateHeatResponse`, `ValidateStatModifiers`, `ValidateNoPowerSupplyOnRequest` and
`ValidateRoleUsage` all run — **not** through `CultCache.Commit` directly, which bypasses them):

1. `Faction` **Megiddo** + its `NameFile` — or the F2 fallback.
2. Designs: `Heavy Combat Hull`, `Medium Utility Hull`, `Heavy Shield Projector`,
   `Light Shield Projector`, `High-Output Thruster`, `Light Thruster`, `Passive Sensor Array`,
   `Signature Dampener`, `Large Capacitor`. **Nine.**
3. Products: `Noka MKI`, `Jason`, `Migdal`, `Thorax`, `Victoire`, `Prokope`, `ChirOptos`,
   `Enigma Device`, `Cottage-pi`, plus `TaranisLG`, `Leonid`, the R&D reactor and `True North`.
   **Thirteen.**
4. Dry `Description` rewrites on `plight` and `Large Drive` (their current text is product voice
   moving to the product), and a correction to `pswarm`'s wrongly-copied description.

Then `AetherDb census` must report: 60 designs, 50 products; **zero** `(product, role)` gaps;
**zero** duplicate `(maker, design)` pairs; the unsold list down from 14 to 10. And
`AetherDb hardpoint-fit` must show `Shield` and `Thruster` hardpoints on both new hulls with
matching sold designs — which is the one check that proves this batch actually reached the game.
