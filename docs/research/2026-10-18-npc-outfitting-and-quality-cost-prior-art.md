# NPC outfitting and quality-vs-cost: prior art

Serves forks (campaign aetheria-release, session self-2026-10-18-ag6, agent eyes-ag6-economy-prior-art):

- `aetheria-release:question:chooseproduct-affordability` (should NPC factions' product choice weigh expected cost, and against what budget)
- `aetheria-release:follow_up:quality-paid-in-cost-or-throughput` (operator: "you can get quality but only by paying for it in production cost or throughput")

Context read, not admitted: ruling `effective-quality-mean-of-roles`. The chooseproduct question records today's ChooseProduct as weighing by ManufacturerPreference alone, with no outfitter budget.

All sources accessed 2026-10-18. Facts and evidence only; no recommendation. Most search-engine summaries were thin; where a claim rests on forum or fan-wiki text it is marked (player report) or (fan wiki). Nothing here is from decompiled game files.

---

## A. How games pick NPC loadouts and equipment

### A1. Starsector (best-documented budget-like mechanism)
- A market's "ship quality" stat sets the number of random d-mods (damage hullmods) on ships it generates (patrol fleets, buyable submarket ships). "A market with a ship quality of 120% or higher" generates only pristine ships with 0 random d-mods. Page marked current for 0.98a. Exact d-mod counts below 120% are not given. https://starsector.wiki.gg/wiki/D-mod
- Changelog 0.9a: faction-specific ships available on the open market have reduced quality, 2 to 3 more d-mods than average for that colony. https://starsector.wiki.gg/wiki/Starsector_0.9a
- Each variant file carries a quality value; richer markets "probably mostly stability-based" get higher-rarity variants on average (wiki says the mechanism is "rather obfuscated"). Some factions have fixed quality and adding a variant with a different quality "messes up fleet generation". Page verified for 0.8.1a only, flagged possibly outdated. https://starsector.wiki.gg/wiki/File_overview:_variant
- Faction pages list doctrine numbers (ship quality, officer quality, aggression, warships/carriers/phase mix). Pirates: ship quality 1; Independent: ship quality 2. Pirate ship quality is tied to production infrastructure: their only Orbital Works is Kapteyn Starworks, and disrupting it "significantly reduces" their ship quality for a period. Pirates use their own variants of standard ships (some cosmetic, some with changed mounts). https://starsector.wiki.gg/wiki/Pirate ; https://starsector.wiki.gg/wiki/Pirates ; https://starsector.wiki.gg/wiki/Independent
- Developer post (2014): a highly stable market produces higher-quality, better-equipped fleets (reported via a secondary search summary; the primary https://fractalsoftworks.com/2014/07/24/fleet-creation returned HTTP 402 and was not read).
- Simulator: fleet groups are sized by deployment points, adjustable 20-240; quality options range from maximum d-mods to many built-in s-mods. https://starsector.wiki.gg/wiki/Simulator
- Budget: deployment points (player-facing) and, per the sources above, a quality stat driven by stability/infrastructure. Whether variant choice is priced was not found. The fleet-points formula was not found.

### A2. X4: Foundations
- Developer post via forum: once the needed blueprints exist, the AI orders ships from a player's shipyard/wharf on its own; factions bring their own blueprints, so ships are equipped even if the player lacks fitting blueprints; players say races prefer certain loadouts that need specific blueprints (player report). https://steamcommunity.com/app/392160/discussions/0/4522261213603206187 ; https://steamcommunity.com/app/392160/discussions/0/4407416886088926175
- Limit on AI shipbuilding reported as material supply, not money: AI shipyards run short of wares, store enough for about 20 or fewer orders then take hours to restock; "AI has a target number of ships it wants and is eternally short of that" (player report). https://steamcommunity.com/app/392160/discussions/0/3203652426713040812 ; https://steamcommunity.com/app/392160/discussions/0/3133918821806548979
- Patch notes: consumables (drones/ammo/deployables) are equipped in a second step after build; rebuild requests were sometimes sent to NPC yards that cannot build that variant. https://steamcommunity.com/sharedfiles/filedetails/changelog/2106438676
- A faction credit budget gating NPC shipbuilding was searched for and not found.

### A3. EVE Online NPC ("rats")
- Fetched evidence is only about loot, not fits: Diamond Rat fleets drop empty wrecks and scrap, not their fitted modules (player request to change); faction-to-meta-loot associations are player claims; a 2012 patch made NPCs stop dropping manufacturable T1 loot. https://forums.eveonline.com/t/diamond-rats-modify-loot-tables/194004 ; https://forums-archive.eveonline.com/topic/99849/5/
- Whether rat fits are fixed per NPC type, and any budget, was not verified.

### A4. Elite Dangerous
- Developer statement 2016 (quoted on a forum): almost all Engineer upgrades removed from NPCs; a follow-up said only Deadly and Elite rank ships are guaranteed to have mods and any below Master are guaranteed none (player-relayed; unclear if still current). Players report NPC ships carry fewer shield boosters/utility than player builds. https://forums.frontier.co.uk/threads/better-ai-ship-design-the-spoils-of-combat-and-using-the-community.341091 ; https://forums.frontier.co.uk/goto/post?id=6501456
- Selection key reported: pilot rank, not price or budget. Current rules not confirmed.

### A5. Mount & Blade / Bannerlord
- Warband: no primary documentation of module_troops equipment selection retrieved. Only a stub noting troop stats are randomly adjusted per new game. https://mountandblade.fandom.com/wiki/Caravan_Guard
- Bannerlord troop-tree modder: each slot has variations acting as a random pool; an empty variation gives 25% chance of no item in that slot; a weapon-distribution pass favors higher-tier soldiers first. Third-party mod note, not base-game docs. https://steamcommunity.com/workshop/filedetails/discussion/2887005424/3551679689867156862 ; https://steamcommunity.com/workshop/filedetails/discussion/3119116807/4035854109137794432
- Selection key reported: tier/culture lists and random per-slot pools; no price.

### A6. Endless Sky
- Fleets are defined as name, government, and weighted variants (ship models and counts); one variant is picked at random by weight (default weight 1). Cargo per commodity is random 0 to capacity. Ships may carry outfits from listed outfitters or from outfitters of the spawn system and directly linked systems. No budget described. https://github.com/endless-sky/endless-sky/wiki/CreatingFleets
- Selection key: weighted fixed variants; availability (outfitters in the spawn/linked systems) mentioned for outfits.

### A7. Space Rangers 2
- Nothing retrieved on NPC equipment generation. Start gear varies by race and class only (original game source). https://rpgcodex.net/article.php?id=126

### A8. Cross-game summary of what was found
- Weighted/tiered fixed lists with no price: Endless Sky variants, Bannerlord slot pools, Elite rank gating.
- Quality stat from wealth/infrastructure rather than per-purchase price: Starsector.
- Money budget for NPC equipment choice: not found in any checked game. Supply of wares is the reported limiter in X4.
- Failure modes reported: X4 chronic AI ship-order backlog from ware starvation and early-game part shortages (player reports above). Starsector variant-quality mismatch breaking fleet generation (variant wiki). "NPCs all buying cheapest" was not found reported for any game checked.

---

## B. Quality-vs-cost/throughput in production

### B1. Factorio 2.0 / Space Age quality
- Quality module 1: +1% quality chance, -5% speed; all quality module levels carry -5% speed; Quality module 3 gives +2.5% at normal quality up to +6.2% at legendary. Speed modules carry a -1% quality penalty that cancels quality modules, floored at 0, intentional per staff. https://wiki.factorio.com/Quality_module ; https://wiki.factorio.com/Quality_module_3 ; https://wiki.factorio.com/Speed_module ; https://forums.factorio.com/116381
- Each craft rolls the machine's quality chance; success raises tier by one, each further tier rolled at a flat 10%. Examples: Q=10% gives 90 / 9 / 0.9 / 0.09 / 0.01% for normal to legendary. Crafting returns the base quality of ingredients; recipes need exact ingredient quality. https://wiki.factorio.com/Quality
- Recycler returns 25% of inputs (modelled as -75% productivity). Wiki table: about 25 normal crafts per legendary output in one 4-slot setup with 50% base productivity, 3 productivity module 3s and 1 quality module 3; "material inefficiency nearly keeps pace with the legendary output rate". Above about 400% total productivity the loop becomes a positive material cycle. Each quality level adds about 30% of base stats (legendary about 2.5x). https://wiki.factorio.com/Quality
- Reported as degenerate or contested (player reports): recycler quality loops ("upcycling") called an exploit by a streamer, defended as a single-building loop whose real fault is infinite productivity research; question of productivity-fed recyclers creating unlimited raw materials, answered by the 25% return and 300% default productivity cap. https://forums.factorio.com/viewtopic.php?p=648582 ; https://forums.factorio.com/viewtopic.php?p=667675
- Down-binner: a player proposal to relabel items above a chosen quality as that lower tier (frames quality as a supply-chain QA project rather than gacha); also objection that random quality is opaque and tedious. https://forums.factorio.com/viewtopic.php?p=629913
- Cost shape: quality is paid in throughput (speed penalty), in input volume (crafts per output), and in lost productivity if module slots are shared.

### B2. EVE Online manufacturing
- Material Efficiency: 1% material reduction per level, max 10 levels (10%); Time Efficiency: 2% time reduction per level, max 10 (20%). Materials multiplied by runs then rounded up, minimum one per run. https://wiki.eveuniversity.org/Research
- Research time is base duration (105 s at level 1 up to 256,000 s at level 10 for rank 1) times blueprint rank; cost is process time value x system cost index x structure bonus, plus facility tax. Copies inherit ME and TE of the original; copies cannot be researched. https://wiki.eveuniversity.org/Research ; https://wiki.eveuniversity.org/Blueprint_Original
- Here the tradeoff is efficiency bought with research time and ISK; the output item is not higher quality. Degenerate strategies were not found in sources read.

### B3. Star Wars Galaxies crafting (Restoration Edition wiki, fan source)
- Resource quality sets the ceiling for experimentation; weighted average of 960 or higher (with a 4% buff) makes a 100% experimentation result possible. Each 10 Experimentation skill gives one point, usually capped at 12. Assembly skill drives the success roll and socket chance. Archived manual: experimenting raises complexity and lowers success chance. https://swgr.org/wiki/crafting ; https://www.swgemu.com/archive/scrapbookv51/data/20070204174832
- Schematics weight different resource stats, so a resource can be excellent for one item and useless for another (secondary source). https://www.giantbomb.com/games/3030-9437/
- Reported degenerate: the page names only XP-grinding macros ("Practice No Item"), not quality exploits.

### B4. Dwarf Fortress
- v50: value multipliers well-crafted 1.1x, finely 1.2x, superior 1.333x, exceptional 1.5x, masterful flat +30, artifact 20x. Older versions (40d, 23a, DF2014): x2, x3, x4, x5, x12 masterful, x120 artifact (the 40d page flags masterful and artifact values as uncertain). Quality depends on creator skill and materials. https://dwarffortresswiki.org/index.php/Quality ; https://dwarffortresswiki.org/index.php/40d:Quality ; https://dwarffortresswiki.org/index.php/DF2014:Value
- Value = base value x material multiplier x quality modifier, plus decorations. Maximizing-value page: the quality flat bonus is added per item in a stack (masterwork minecart 130 each vs a stack of masterwork bolts 800); prepared meals valued by summed ingredients times ingredient count, which "some players consider trading... an exploit". Page itself shows a 4x vs 5x bar-multiplier inconsistency. https://www.dwarffortresswiki.org/index.php/Maximizing_value
- Quality here is paid in dwarf skill (time to train), not input cost.

### B5. Operations and project framing
- Project management triangle: origin unclear, used since at least the 1950s per the cited article; Martin Barnes drew cost-time-quality in 1969; quality treated as the result of budget, deadline and scope; extensions (Project Diamond, PMBOK star, PCTS) separate quality or scope. Criticisms: trade-offs are not always possible (adding staff can slow a project). Article carries original-research tags. https://en.wikipedia.org/wiki/Project_management_triangle
- Cost of quality: four categories prevention, appraisal, internal failure, external failure. Attribution varies by source (Juran vs Feigenbaum; Crosby popularised, "Quality Is Free", 1979). Whether the optimum is at 100% conformance is contested. Secondary sources only. https://www.isixsigma.com/dictionary/cost-of-quality-coq/ ; https://www.isixsigma.com/tools/cost-of-quality

### B6. Not checked
- Satisfactory, Anno, X4 production quality/throughput: no source read (see gaps).

---

## C. Segmentation by binning

- Real manufacturing: product binning sorts finished products into quality tiers because a process yields a range; each bin can be priced for a market. Units sold under a designation must meet it at minimum and may exceed it. Semiconductor yields cited "as low as 30 percent". Flawed chips can be sold cheaper by lowering clock or disabling components (A12X/A12Z shared die; PS3 Cell with seven working SPEs; produce grading also listed). https://en.wikipedia.org/wiki/Product_binning
- Secondary: newer nodes have a larger gap between good and mediocre parts; per-chip silicon lottery causes overclock variation. https://www.cgdirector.com/what-is-the-silicon-lottery ; https://www.howtogeek.com/705101/what-is-binning-for-computer-components/
- As a game mechanic: no write-up using "binning" as a design term was found. Nearest items are the Factorio down-binner proposal (B1) and games that roll quality at craft time weighted by skill or materials (Myth of Empires, EmakiForge, a Hytale mod). https://guildorder.com/games/myth_of_empires/guides/weapon-quality-and-roll-targeting ; https://speedgot.ajg0702.us/resources/133816 ; https://www.curseforge.com/hytale/mods/romnas-quality-crafting/install
- Factorio's mechanic is itself a stochastic output tier (per-craft roll, 10% per further tier) rather than a sort of a continuous distribution.

---

## Known gaps

- Starsector: fleet-point calculation, doctrine-number meaning (the Faction_doctrine and Doctrine wiki pages returned 404), and whether variant choice is priced; Fractal Softworks "Fleet Creation" post (HTTP 402). Game data/API docs not read.
- X4: how NPC shipyards choose equipment, and any faction credit budget; Egosoft wiki and game scripts not read. Steam threads are player reports.
- EVE NPC fits: fixed per type or generated, unverified.
- Elite Dangerous: current NPC loadout rules unverified; only a 2016 developer statement relayed on forums.
- Mount & Blade: no primary Warband module_troops documentation retrieved.
- Space Rangers 2 and Endless Sky outfit logic (beyond fleet variant weights): not found; Endless Sky source code not read.
- Satisfactory, Anno, X4 production quality/throughput mechanics: not searched.
- Any report of "NPCs all buying cheapest" or economy starvation caused by price-weighted NPC purchases: not found in any checked game.
- Binning as an explicit game-design term: not found; Wikipedia and hardware-review sources are non-game.
- Cost-of-quality and PM-triangle attributions rest on secondary and encyclopedic sources; Juran, Feigenbaum, Crosby primary texts not read.
- Factorio loop/exploit discussion is forum sentiment, not developer statement beyond the speed-module cancellation post.

## Addendum (operator, 2026-10-18): budgets in a value currency

The finding "no checked game prices NPC outfitting against a money budget" is narrower than it reads. Where wargames do budget a force, the budget is usually a combat-value currency kept apart from price: BattleTech's Battle Value (BV) sits beside a unit's C-bill cost, and force building is balanced on BV, not C-bills. Starsector's fleet and deployment points (listed under gaps above) are the same family, as are tabletop points costs. So when outfitters get a budget (ruling aetheria-release:ruling:outfitting-affordability-waits-for-budget), the first question is which currency: money (price follows effective quality) or a combat value derived from stats. Not yet checked against a source: how BV is computed from a unit's stats and gear, and how it diverges from C-bill cost.

Why BV exists, operator's examples (2026-10-18, illustrative): C-bill price tracks what a unit costs to build, not what it does on the field. A swarm of Urbanmechs costs one Atlas; a Hunchback beats a Jagermech that costs about twice as much; a Cicada costs about what a Vindicator does with half the punch. In BattleTech's construction rules engine cost dominates price and scales with rating, so speed is expensive and firepower is cheap. Price and combat value are two functions of the same stats, and their divergence is the bargain a buyer can find. BV itself is context-blind (map, range, pilot) and was revised (BV2), so it is a balance currency, not a truth.
