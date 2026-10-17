# How the part/assembly combining rule shapes the meta

Raised by question aetheria-release:question:quality-combining-rule-meta (cut assembly-from-maker, cut_spec r1), under rulings quality-is-assembly-over-supply-chain, quality-lean-assembly-hard-ceiling, price-follows-effective-quality, effective-quality-mean-of-roles and maker-design-tiers-allowed. Imagination (imagination-ag4-quality-meta), 2026-10-17. The operator's doubt ("I suspect it might be a bit too harsh") is a musing; this note is evidence and a recommendation, not a ruling.

## Method

Catalog: GameData/Aetheria.cc at origin/master 30347219, read by a temporary AetherDb dump (deleted). 121 branded products with roles across 12 makers; 371 stats with StatSource.Quality terms and their exponents (mostly 1.5-2). Each product: 400 lots. Assembly drawn per the cut's derivation: the maker's AssemblyMean = mean of its products' role means, AssemblyDeviation = mean of their deviations (Finch .66±.19, Adrasteia .63±.12, Zhestokost .52±.10, Pirates .45±.22). Each role's part drawn from the product's ProductRole distribution, clamped to [.01, 1]. Effective item quality Q = mean of effective role qualities (the ruling); price multiplier = .25 + 7.75 Q^3 (GameplaySettings.QualityPriceModifier). A unit's performance is the mean over its quality-term stats of q_role^exponent; its stat-implied quality inverts that with the design's mean exponent.

Measures:
- capped: share of roles whose effective value is below the part.
- maker spread: highest minus lowest maker mean Q; Finch-Adr: Finch's mean Q minus Adrasteia's (high tech/loose QC versus solid QC); Finch tail: difference in P(Q > .7).
- signature retained: for products with a role pumped at least .1 above another, effective gap between the two roles divided by the authored gap.
- stat sleeper: unit whose stat-implied quality beats its price-band Q by more than .05 (shop arbitrage).
- part sleeper: unit whose mean part quality exceeds Q by more than .1 (worth taking apart).
- R2 asm / R2 part: share of a unit's stat variance explained by its assembly, and by its mean part.
- part gain: a counterfactual two-role design (barrel, feed) bought from a premium supplier (.8±.1) versus a mediocre one (.5±.12), in a weak shop (.45±.15) and a good shop (.65±.10).
- binning: a Lucent Light Laser budget line (parts .55±.15) against a binned line (parts .75±.08) and a pumped line (focusing array .85±.08), under Lucent's assembly.

The sim is a probe, not the game: heat and durability terms are held at 1, and the role classification used by the critical-roles rule is a sketch.

## Rule families

Each rule is one function f(part, assembly, role) that Lot.QualityForRole could call.

| Family | Representative | f |
|---|---|---|
| A. Ceiling | A1 hard min (the cut) | min(p, a) |
| | A2 soft ceiling | p below a; a + .3(p - a) above |
| | A3 tolerance band | min(p, a + .1) |
| B. Multiplicative (O-ring) | B1 geometric | sqrt(p a) |
| | B2 gain (amplify good, punish bad) | .5 + (p - .5) 2a |
| C. Assembly as floor or spread | C1 floor | max(p, a) |
| | C2 tolerance loss | p - abs(N(0, .25(1 - a))) |
| D. Per-role sensitivity | D1 critical roles only | min(p, a) on precision roles, p on bulk roles |
| E. Gap as trait | E1 temperamental | p below a; above, each read lands uniformly between a and p |
| Baseline | Z part only (master before the cut) | p |

## Results: the catalog as derived

| Rule | mean Q | capped | maker spread | Finch-Adr | Finch tail | signature kept | stat sleeper | part sleeper | price p90/p10 | R2 asm | R2 part | part gain, weak shop | part gain, good shop |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| A1 hard min | .48 | .48 | .24 | -.02 | +.07 | .44 | .07 | .34 | 3.5 | .51 | .26 | .05 | .16 |
| A2 soft ceiling | .50 | .47 | .22 | .00 | +.13 | .61 | .08 | .24 | 3.1 | .34 | .50 | .12 | .19 |
| A3 tolerance +.1 | .52 | .30 | .23 | -.01 | +.10 | .63 | .09 | .17 | 3.2 | .27 | .50 | .09 | .22 |
| B1 geometric | .55 | .46 | .22 | +.01 | +.15 | .52 | .04 | .17 | 3.0 | .64 | .32 | .12 | .16 |
| B2 gain | .57 | .27 | .23 | +.02 | +.10 | 1.07 | .14 | .01 | 4.0 | .06 | .80 | .26 | .37 |
| C1 floor | .65 | .00 | .19 | +.06 | +.17 | .53 | .04 | .00 | 3.0 | .48 | .25 | .24 | .14 |
| C2 tolerance loss | .48 | .90 | .24 | +.03 | +.17 | .96 | .17 | .33 | 4.1 | .05 | .69 | .29 | .30 |
| D1 critical only | .52 | .22 | .22 | +.01 | +.16 | .48 | .11 | .16 | 3.3 | .19 | .62 | .18 | .23 |
| E1 temperamental | .52 | .42 | .21 | +.01 | +.14 | .71 | .10 | .15 | 3.2 | .16 | .62 | .17 | .22 |
| Z part only | .56 | .00 | .21 | +.03 | +.18 | 1.00 | .12 | .00 | 3.4 | .00 | .90 | .30 | .29 |

Per-maker mean Q under A1: Adrasteia .57, Finch .55, R&D .49, DME .49, Lucent .47, AU .47, Zhestokost .46, Pirates .33. Under Z: .63, .66, .57, .58, .57, .56, .52, .45.

Binning (Lucent Light Laser):

| Rule | binned line Q gain | binned price x | pumped role gain | binned gain in a weak (.40) shop |
|---|---|---|---|---|
| A1 hard min | .09 | 1.53 | .09 | .03 |
| A3 tolerance +.1 | .12 | 1.73 | .13 | .05 |
| D1 critical only | .14 | 1.78 | .08 | .10 |
| E1 temperamental | .14 | 1.81 | .20 | .11 |
| B2 gain | .21 | 2.20 | .32 | .15 |
| Z part only | .20 | 2.14 | .30 | .19 |

With the binned product carrying its own assembly draw (.70±.08, a final-test bin) and the pumped one .62±.10, A1 shows a .21 Q gain at 2.34x price and a .15 pumped-role gain; A3 .20 and .19.

## What each family feels like

- A. Ceiling. Shopping is for the shop: a player learns which makers assemble well and buys the brand. Parts matter only up to the shop's tolerance, so a lucky find is a unit whose shop had a good day, and a sleeper is a cheap unit hiding a part too good for its shell, recovered by taking it apart. Assembler and parts supplier are different trades in the fiction: the assembler sets the limit, the supplier fills it. With assembly drawn at the part mean (A1 as derived) the ceiling binds on half of all roles, so overspecced parts are the norm rather than a find: one unit in three is a sleeper.
- B. Multiplicative. Both always matter and neither wins outright. Nothing is wasted, so there is no "worthless perfect part" moment, which is the moment the operator asked for. B2 (gain) makes a good shop a magnifier and a poor shop a leveller; at a weak shop premium parts still pay off (+.26), so it fails "the best parts cannot rescue poor final assembly".
- C. Floor and spread. C1 (assembly as floor) inverts the ruling: a good shop rescues bad parts. C2 (tolerance loss) makes assembly a lottery on top of the part: every unit loses something, stats look noisy against price (17% stat sleepers), and the player chases luck rather than brands.
- D. Per-role sensitivity. Shops matter for lenses, barrels and guidance; bulk parts are commodities. It reads well in the fiction and keeps part suppliers relevant (R2 part .62), but every design needs a sensitivity per role, and a pumped precision role is still capped (.08).
- E. Gap as trait. An overspecced part in a sloppy shop gives flashes of brilliance; the gap is something a player feels in a fight, not only reads in a tooltip. It costs runtime variance on every stat read, which heat, power and AI decisions then see as noise, and it is the most machinery of any option.

## The harshness is in the data, not the function

min(p, a + t) equals min(p, a') with a' drawn t higher: A3 at t = .1 and A1 with AssemblyMean raised by .1 gave the same table row. So "too harsh" is mostly the cut's derivation, AssemblyMean = the maker's mean part mean. Drawing assembly and parts from the same distribution makes the ceiling bind half the time and costs every maker about 0.56 sigma in mean (E[min] of two equal draws), so variance is taxed twice: Finch's higher technology is erased to Adrasteia's level in mean and survives only in its tail.

The second harshness is structural: one assembly per maker. A binned or premium line that selects better parts cannot show above its own maker's shop (+.09 under A1). Real binning is mostly a final test of the finished unit, not only part selection. Letting a product carry its own assembly distribution, defaulting to its maker's, restores the line gap (+.21) under the hard min unchanged.

Does the hard min make part makers irrelevant? Not in the shipped catalog, where every product's parts and assembly come from one maker. In a supply chain, inside a weak shop: yes, premium parts are worth +.05, which is the operator's stated intent ("a perfectly tuned part worthless"). Inside a good shop they are worth about half what they would be uncapped (+.16 against +.29). Under A1 as derived, assembly explains twice the stat variance parts do, so the loadout chase is mostly a brand chase.

## Prior art (from the faculty's knowledge, not re-verified against current game builds)

- Factorio 2.0 (Space Age) quality: a recipe takes ingredients of one quality level and its output starts at that level, with quality modules giving a chance to roll higher. The weakest link is enforced at the recipe door rather than by a min, and the upgrade chance is the luck layer.
- Dwarf Fortress: material and craftsmanship are separate visible layers, and value multiplies them (material value times a quality multiplier), with decorations carrying their own quality. Multiplicative, and both layers stay readable on the item.
- Star Citizen: ship components carry a grade (A-D) and a class (military, civilian, industrial, stealth, competition). The class is a personality axis, not a quality; nothing combines component grade with an assembly quality.
- Elite Dangerous engineering: grades 1-5 of a modification with secondary penalties and experimental effects. Tradeoffs and quirks are first-class, the nearest to family E.
- EVE Online: units of one type are identical; quality lives in the design tier (meta level, Tech II via invention) and blueprint research (material and time efficiency), not in per-unit rolls. The opposite pole: no sleepers.
- Mount & Blade item modifiers (Masterwork, Balanced, Rusty, and the rest): a visible prefix moves both stats and price, so there are no hidden sleepers.
- Kenshi: crafted grade follows the blueprint level and the crafter's skill; the grade is the brand.
- Manufacturing: Leontief (weakest-link) production is the hard min; Kremer's O-ring model (QJE 1993) is multiplicative. Tolerance stack-up is analysed worst case or statistically (root sum of squares), and Taguchi's quadratic loss prices deviation from target, both nearer to family C2. Semiconductor binning tests the finished die and sells it under a SKU by measured performance, which is a final-test selection of the assembled unit: the evidence for a product-level assembly distribution.

## Recommendation

Keep the hard min. It is the only family that delivers the operator's stated moment (a perfect part in a sloppy shop is worthless above the shop's tolerance), it keeps QualityForRole one line, and the multiplicative and floor families each break a ruling. Change the data, in two places:

1. Derive AssemblyMean above the part mean: the maker's mean part mean plus a tolerance margin, about .1, as one authored constant in the derivation and the Studio knob after it. Capped falls from 48% to about 30%, part sleepers from 1 in 3 to about 1 in 6, signature roles keep about 60% of their edge instead of 44%.
2. Let a FactionProductData carry an optional assembly distribution, defaulting to its maker's, so binned and premium lines are a better final test as well as better parts (maker-design-tiers-allowed). A binned line then shows +.20 over its budget sibling under the hard min.

Second choice, if the operator wants part suppliers to matter more than brands: D1, the ceiling on precision roles only, at the cost of a sensitivity per role on every design. E1 is the most flavourful and the most machinery; worth revisiting with real disassembly (named-components-after-demo), not before.

Neither change touches QualityForRole, price, or effective-quality-mean-of-roles. Change 1 is a line in AssemblyFromMakerCommand; change 2 is two fields and CreateLot reading the product before the maker.
