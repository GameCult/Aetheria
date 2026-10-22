# Prior art for follow-up aetheria-release:follow_up:crafting-chains-and-demand (campaign aetheria-living-world), 2026-10-22, eyes self-2026-10-19-ag7

Serves: `aetheria-release:follow_up:crafting-chains-and-demand`. Context read: rulings `catalog-crafting-chains`, `quality-is-assembly-over-supply-chain`, `quality-min-tolerance-binned-assembly`, `quality-triangle-is-per-facility` (question label quality-cost-scope), `maker-design-tiers-allowed`, `snowball-damping-lore-institutions`, `matchup-composition-model`.
Local ruling constraints the survey is read against (not conclusions): quality is per production step with assembly bounding the result; the good/fast/cheap triangle is set per facility by its owner, and premium assembly needs a matching supply chain; runaway factions are damped by lore institutions modelled as rules; off-screen fights settle by a learned composition model.

Provenance tags: [F] = page opened and read this session (WebFetch extraction by a small model, not verbatim); [S] = search-result summary only, page not opened; [M] = from memory, not re-fetched, verify before citing. Several sources blocked fetching (Fandom 402, EVE University 530, some Egosoft wiki pages 404, Paradox forum, web.archive.org, Tesfatsion site certificate error); gaps are named per section. No recommendations.

## 1. X4: Foundations (Egosoft)

Chain structure
- Wares are three layers: mined raw (Ice, Ore, Silicon, Nividium solids; Helium, Hydrogen, Methane liquids), intermediates (Refined Metals, Silicon Wafers, Graphene, Antimatter Cells, Water, Microchips), finished goods (Advanced Electronics, Claytronics, Drone Components, Hull Parts, Weapon Components). Chains run 2 to 4 steps: Ore > Refined Metals > Hull/Engine Parts; Silicon > Wafers > Microchips > Advanced Electronics/Claytronics; Methane > Graphene > Composites; Ice > Water > food/medical. Storage types Solid/Liquid/Container with per-ware volume; the wares table lists producers, consumers and max profit per m3. https://wiki.egosoft.com/X4%20Foundations%20Wiki/Manual%20and%20Guides/Objects%20in%20the%20Game%20Universe/Wares/ [F; page flags itself out of date as of 4.0]
- Branching is by faction/race: Food Rations (Meat+Wheat) consumed by Argon habitats; Paranid habitats consume Soja Husk; Medical Supplies exist in Argon, Paranid, Split, Teladi variants, consumed by habitats and parlours of all four races. Same source [F].
- Energy Cells need only sunlight; secondary wares boost module output (station wiki pages) [S].

Demand that is not gear
- Habitation modules consume food and medical wares and produce workforce; workforce gives a production bonus to modules. Community accounts (Steam): optimal worker count gives up to about 20% bonus; per-module bonuses vary (about 10% for one module type, 37 to 46% reported for others); Terran food needs methane, others only ice. [S, player posts, version unclear]
- Egosoft 4.00 beta note (forum mirror): available habitation capacity per race ties to workforce growth rate; workforce info added to production modules and to the habitation section of the build menu. https://sg.hu/forum/uzenet/1481892553/5083 [S]
- Ship/station construction and the NPC "war economy": miners feed factories that feed shipyards; ships destroyed restart the cycle (player description). [S: https://steamcommunity.com/app/392160/discussions/0/733658398226259846/ ]

Staying alive without the player
- Egosoft store text: "fully simulated and dynamic economy"; factions build and expand stations dynamically in response to economic and military pressure. https://www.egosoft.com/games/x4/info_en.php [F]
- Prices move with how much a station holds or needs relative to storage capacity (wiki station pages). [S]
- Players report NPC factions eventually build stations to fill gaps, and hire other factions to build even after being wiped out; trigger conditions are not documented in anything fetched. [S, Steam]

Reading the economy
- Transaction Log with income/outgoings breakdown added in 4.00; Trade Rules (3.20) control station trade offers and supply offers. https://wiki.egosoft.com/X4%20Foundations%20Wiki/Game%20Updates%20and%20Patch%20History/X4%20Foundations%20Feature%20Development%20Overview [F]
- Wares table with price range and profit per m3 [F]. Player-written external tool "Empire Logistics Tool" (Excel) for chain planning: https://forum.egosoft.com/viewtopic.php?p=5251793 [S].

Failure modes (player testimony, [S], not Egosoft statements)
- New games start with part shortages that stall shipyards/wharfs; advanced electronics gone within first hours on some maps; the bottleneck varies per seed. Players fix by hand-trading missing parts.
- "Economy collapses without intervention" and "lacks the ware sinks earlier X games had"; if ships are not destroyed, factions hit a unit cap and stop demanding from shipyards; war leaves consumers with full storage, which stops purchasing. Effects of inaction appear around 24 hours in, when initial stock runs out.
- Counter-view: a player built 111 stations by manual trading and argued the AI copes and complaints reflect player interference.
- Mods rework it: DeadAir Economy Overhaul https://www.nexusmods.com/x4foundations/mods/2209 (lists "workforce effect strengthened") [S].
Not found: any Egosoft dev diary or Inside Egosoft text on the 4.0 workforce/habitat model.

## 2. Anno 1800 (Ubisoft Blue Byte)

Primary-source status: Fandom wiki blocked (402); no official devblog on needs found. Everything below is wiki/guide/forum summary.

Chain structure and tiers
- Five Old World tiers (Farmers, Workers, Artisans, Engineers, Investors) and two New World tiers (Jornaleros, Obreros). Houses 3x3. https://segmentnext.com/anno-1800-needs-guide/ [F]; https://en.wikipedia.org/wiki/Anno_1800 [S]
- Needs split Basic and Luxury; each need is met by goods (fish, bread, beer) or a service building (pub, church, school, university). Basic needs drive influx (people per house); luxury needs raise happiness and income/tax. A house upgrades to the next tier when its basic needs are met; Season 4 added "lifestyle needs". segmentnext [F]; Steam guide https://steamcommunity.com/app/916440/discussions/0/4035852798889680783 [F]
- Artisan Residence page lists per-need consumption rate per 30-resident residence, e.g. sausage about 0.000667, bread about 0.000606 (unit not stated). https://anno1800.fandom.com/wiki/Artisan_Residence [S]
- Workforce couples tiers to industry: potato farm needs 20 farmers, steelworks 200 workers (PC Gamer tips). https://www.pcgamer.com/anno-1800-tips-guide/ [S]. Engineers bring electricity doubling nearby factory productivity [F Steam guide].
- Chain ratios: clicking a production building shows its cycle rate, so ratios are computed by the player [S]; community calculators exist (annocalc) [M].

Demand not gear
- All consumer demand is residents' needs, tied to tier and house count; higher tiers are the main income ("Investors pay for increasingly complex consumer goods") [F Steam guide]. Raising output can cost happiness; extreme cases riot and halt production [S].

Stalls and snowballing
- A goods deficit makes residents unhappy and they leave, lowering income, so expansion beyond supply self-corrects downward [F Steam guide]. Upkeep per production building is high, penalising sprawl [S]. Passive trade is limited and expensive; Docklands DLC adds barter scaling with storage [F].

Reading the economy
- Hover on a need shows money earned per house [F]; UI shows connected buildings and supply-chain position [S, Aureview review]. One forum poster calls surplus/deficit "extremely fuzzy" in 1800 compared with Anno 2205 unless running large surpluses [S]. Lifestyle needs made it harder to tell when houses are ready to upgrade [S].

## 3. Victoria 3 (Paradox)

Chain structure
- Buildings form a web producing and/or consuming goods; they do nothing without staff. Dev Diary 57: https://paradoxinteractive.com/games/victoria-3/news/dev-diary-57-the-journey-so-far [F]
- Production methods (PMs): each building has PM groups (Base, Secondary, Automation, Organization, Military), exactly one active PM per group; locked PMs need tech, law, another PM, or goods produced in state/market; if requirements lapse the active PM falls to the first valid one in the group. Inputs/outputs scale with building level x employment x throughput. https://vic3.paradoxwikis.com/Production_method [F]
- Dev Diary 5 (search summary only): PMs are trade-offs rather than straight upgrades; categories Base, Refining (shifts to luxury/specialised output), Automation (adds industrial goods input to reduce unskilled labour). Direct fetch of the diary 404'd; forum thread https://admin-forum.paradoxplaza.com/forum/developer-diary/victoria-3-dev-diary-5-production-methods.1480760/ [S]
- Goods categories: staple, luxury, industrial, military; staple and luxury mostly consumed by pops, industrial and military mostly by buildings. Local goods are state-bound and not traded. https://vic3.paradoxwikis.com/Goods [F]

Demand not gear
- Pops staff buildings, earn wages, spend on goods; owners spend profits on luxuries; peasants subsistence-farm unused land. Dev Diary 57 [F].
- Needs: a buy package per wealth level (base-price spending target per need category for 10,000 working adults); pops buy until units x base price reaches the package value, so pricier goods satisfy a need in fewer units; pops ignore price when choosing among goods in a need (split by market share, weight, min/max share); dependents count half; peasants buy 5% of standard; relative substitution capped at 1% to 10% per week; culture obsessions raise a good's weight, taboos halve demand; need patterns across wealth levels are temporary, constant or exponential. https://vic3.paradoxwikis.com/Needs [F, v1.13]
- Standard of living equals wealth level; higher SoL means larger, costlier needs; birth/mortality driven by SoL, growth levels off near SoL 15. https://vic3.paradoxwikis.com/Pop [F]
- Institutions (healthcare, education, policing) are pop-staffed and consume a Bureaucracy currency produced by buildings, so players need not micromanage facilities. Dev Diary 57 [F].

Stalls and snowballing
- Price per good floats up to +/-75% of base cost with relative supply and demand; base cost = price when buy and sell orders are equal. Goods page [F].
- Trade routes grow until marginal profit ends; the earlier trade system needed micromanagement and was exploitable, including by AI; replaced by profit-responsive routes with tariffs/embargoes. Dev Diary 57 [F].
- Unemployment handled by subsistence farming, which may widen wealth gaps in the industrial transition. Dev Diary 57 [F].

Reading the economy: goods have a base cost and per-market price; pops show SoL and needs (wiki pages above) [F]. No fetched source describes the UI.

Failure modes (player testimony [S]: Steam threads, e.g. https://steamcommunity.com/app/529340/discussions/0/5971271658111594308 )
- Over-profitable buildings drive their goods' prices very low while AI keeps expanding them; after the 1.5 economy change players report nearly all goods in balance or oversupplied, exports hard; construction-sector over-building forces deficit spending; the jobseeker system made new factories hard to staff; construction sector cost rose from 50 to 100 points, slowing growth.

## 4. EVE Online (CCP)

Chain structure
- Mining yields ore; reprocessing yields minerals; minerals are inputs to nearly all manufacturing. Net reprocessing yield depends on equipment, skills, implants and standings (worked example: base 50%, about 69.9% with skills). Compressed, batch-compressed and uncompressed ore yield the same minerals per batch. https://wiki.eveuniversity.org/Minerals , /Refining , /Reprocessing [S; direct fetch blocked, 530]. Wiki flags the yield page as needing update after the October 2020 patch [S].
- Moon materials must go through reactions before use [S]. Blueprint originals have infinite runs, copies limited; T2 comes via Invention producing limited-run T2 blueprint copies (baseline 10 runs, +2% ME, +4% TE, decryptors modify); T2 originals are rare lottery-era relics. https://wiki.eveuniversity.org/Invention , /Blueprint_Original [S]. Industry jobs carry a per-system cost index (page /System_Cost_Index; title only, content not read).
- Chain shape per these pages: raw > refined mineral > component > ship/module, plus a moon > reaction > advanced-material branch and an invention branch gated by datacores [S/M].

Demand not gear / sinks
- CCP economist Dr Eyjolfur Gudmundsson, Fanfest 2011 as reported: warfare and the economy thrive on each other; very little consumption without warfare; wars cannot be won without a strong economy; PvP destruction presented as essential to economic health. https://www.engadget.com/2011-04-04-ccp-releases-eve-quarterly-economic-newsletter.html [F]
- Fanfest 2014 framing: ISK faucets versus sinks; too much entering risks rapid inflation, too little risks collapse (secondhand, Engadget tag page) [S]. Varoufakis essay on war spikes in EVE (2014): https://www.yanisvaroufakis.eu/2014/01/30/war-spikes-in-the-eve-online-universe-a-political-economists-account/ [fetch returned 403; content not read].
- Monthly Economic Report format: destruction, production, mining value, money supply, ISK velocity, price indices (mineral, ship, module), sinks-and-faucets graphic; Sept 2025 methodology change made production and mining use static pricing. https://www.eveonline.com/news/view/monthly-economic-report-february-2026 [F]; https://www.eveonline.com/news/view/monthly-economic-report-september-2025 [S]. Feb 2026: destruction up while mining and production down, velocity falling, mineral index about -52% yoy, ship about -21%, module about -13% [F].

Avoiding stalls / snowballing
- No fetched CCP text describes stall avoidance. Structure is player-driven; destruction is presented as the demand sink (above). Cost index named only [S].

Reading the economy: public MER with downloadable data; in-game market history [F/M].

Failure modes
- Forum critique built on the Feb 2026 MER: destruction rising while mining, production and mineral prices fall contradicts "destruction drives the economy" (player reading). https://forums.eveonline.com/t/the-economy-isnt-broken-and-destruction-is-not-good/286457 [S]. Player claim that CCP interventions from the 2020 Ecosystem Outlook reduced player wealth [S, forum opinion]. A 2021 thread questioned regional destruction totals versus the overall figure; one commenter suggested reimbursed losses distort them [S].
- 2012 CCP Recurve dev blog on inflation (mineral prices +17.4%, CPI +13.5%, as cited by a forum user) [S, unverified].

## 5. Starsector (Fractal Softworks): small-scale commodity demand per colony

Wiki current for 0.98a. All [F] unless noted.
- Eighteen commodities with base price and "economic unit" (Food 20 / 2000, Metals 30 / 2000, Domestic Goods 50 / 1000, Luxury Goods 100 / 500, Heavy Machinery 150 / 300, Heavy Armaments 500 / 200, Recreational Drugs 200 / 400, Harvested Organs 300 / 200, others). https://starsector.wiki.gg/wiki/Commodities
- Each industry lists commodities it produces and demands, amounts as formulas of colony size (Farming produces Food at Size and demands Heavy Machinery at Size-3); industry slots = Size-2; upkeep scales with size. https://starsector.wiki.gg/wiki/Industry
- Population and Infrastructure demand for a colony of size X: Food X, Domestic Goods X-1, Luxury Goods X-3, Recreational Drugs X-2, Harvested Organs X-3, Supplies 3 flat, Organics X-1 on non-habitable planets. Meeting Domestic Goods and Luxury demand each gives +1 stability; failing Food or Organics gives a stability penalty equal to the larger shortage. https://starsector.wiki.gg/wiki/Population_%26_Infrastructure
- Trade fleets move goods between markets (physical convoys near the core, simulated elsewhere); destroyed convoys cause "Shipping Disruption" (temporary shortages, price shift, accessibility penalty); pirate activity creates shortages and excess; player sales fill demand for 120 days, listings refresh every 30; buying raises and selling lowers unit price; a market with no demand for a good pays well below base; 30% tariff on open markets; stability changes prices and black-market share. https://starsector.wiki.gg/wiki/Trade , /Market
- Shortage and surplus figures (Deficit/Excess) appear in the F1 tooltip so players can see how much to trade before prices normalise (needs a comm relay). Trade page.
- The wiki does not say how a deficit affects industry output.

## 6. Elite Dangerous Background Simulation (Frontier): faction economy and expansion limits

- Faction influence in a system is zero-sum across factions; control tracks the controlling station, not highest influence; a faction needs 75% influence to expand, expands to the nearest system within 20 LY with fewer than seven factions, else the expansion becomes an invasion; below 2.5% influence a faction withdraws and loses wealth elsewhere; below 7.5% it cannot enter conflicts. https://www.dpss.space/bgs [F; community guide, not Frontier]
- Economy states Famine/Bust/None/Boom/Investment shift happiness and mission rewards; Boom lasts about three weeks or until accrued value is spent; Famine is ended by delivering food [F same page]. Frontier's own description (2018, as relayed): daily calculations apply player actions to faction attributes [S: https://massivelyop.com/?p=192372 ]; four BGSes in 2024 (Political, Economic, Outfitting, Thargoid), Thargoid states suspend political movement [S, Frontier forum].

## 7. Factorio and Satisfactory (chain depth and legibility only)

Factorio
- Science packs set depth: Automation about 3 raw items per pack; Logistic; Military; Chemical (crude oil); Production; Utility about 190 raw (33.3 iron, 49.8 copper, 3.8 coal, 106.8 crude); Space Age packs up to about 477 raw per pack. Demand for the whole factory comes from lab research cost = pack count x pack types. Assembler ratios per pack published (base game 5:6:5:12:7:7). https://wiki.factorio.com/Science_pack [F]
- Space Age agricultural packs spoil, reducing science value over time [F same page].
- Production statistics and ratio calculators as legibility aids [M]; not fetched.

Satisfactory
- Progression through HUB milestones grouped in tiers; Space Elevator phases unlock higher tiers; delivered parts cannot be retrieved. https://satisfactory.wiki.gg/wiki/Milestone , /Tiers [S]
- By-product stall: Heavy Oil Residue is a by-product of Plastic and Rubber in the Refinery; if it backs up, the primary product stops too; outlets are Petroleum Coke (4 HOR > 12 coke), Fuel/Turbofuel, Smokeless Powder, packaging. https://satisfactory.wiki.gg/wiki/Heavy_Oil_Residue [F]
- AWESOME Sink destroys sinkable items for points (by item value); points buy coupons whose price rises in steps; wiki advises an overflow splitter feeding the Sink so lines never clog; throughput up to 1200 items/min on a Mk.6 belt. https://satisfactory.wiki.gg/wiki/AWESOME_Sink [F]

## 8. RimWorld and Dwarf Fortress (need-driven demand)

- RimWorld: need bars (Food, Rest, Recreation, Beauty, Comfort, Indoors/Outdoors, Chemical, others) fall by themselves and refill by eating, sleeping, recreation, drugs; needs feed mood through threshold thoughts; very low mood triggers mental breaks. https://rimworldwiki.com/wiki/Needs [F]
- Dwarf Fortress: idle dwarves eat, drink or sleep when only slightly in need; trade is by caravan to a depot; earlier versions had a monetary system later dropped. https://dwarffortresswiki.org/index.php/v0.34:Eat , /DF2014:Trade [S]; commentary https://crookedtimber.org/2014/01/07/dwarf-fortress-a-marxist-analysis/ [S]. Noble mandates page fetch failed (429); mandate mechanics not verified.

## 9. Virtual-economy design literature (faucets and sinks)

- Lehdonvirta and Castronova, "Virtual Economies: Design and Analysis" (MIT Press 2014): applies microeconomics to artificially scarce virtual goods. Reading summary describes faucets (create assets), sinks (remove assets: transaction taxes, service fees, repair, limited lifespans), inflation when faucets exceed sinks, deflation causing hoarding. Book text not opened. https://www.antoinebuteau.com/virtual-economies-book-summary/ ; https://blogs.lse.ac.uk/usappblog/2014/09/28/book-review-virtual-economies-design-and-analysis-by-vili-lehdonvirta-and-edward-castronova [S]
- Gold sink (Wikipedia): passive sinks (decay, taxes), active sinks (designer-added), feedback controllers adjusting drop rates and prices; Alter Aeon taxed holdings above 1M at 2% of the excess and kept the currency peak within 10% over two years; Ultima Online NPC vendors sold unmakeable "blue" armor at inflated prices and after Renaissance sold luxury items to absorb money; Kingdom of Loathing and RuneScape (Construction skill) examples. https://en.wikipedia.org/wiki/Gold_sink [F; article flagged single-source]
- Zack Booth Simpson, "The In-game Economics of Ultima Online" (GDC): primary source for UO price/inflation measurement; the fetched page exposed only the introduction (body is images/truncated). https://gwern.net/doc/economics/1999-04-07-simpson-theingameconomicsofultimaonline.html [F, not read through]
- Raph Koster, "UO's resource system": objects carry abstract resource labels with amount, max and regrowth; AI creatures have FOOD, SHELTER, DESIRE, PRODUCE lists attempted in Maslow-like order; the beta added spontaneous generation and a closed economy loop. The post does not state the failure; reader comments recall animals hunted to near-extinction and the system removed, and one commenter advises that a dynamic ecology must be constrained heavily to stay predictable. https://www.raphkoster.com/?p=517 [F]
- Koster on SWG: player shops with NPC bot; crafters formed supply chains and customer bases; half of players ran a shop at peak. https://www.raphkoster.com/2012/03/20/do-auction-houses-suck/ [F, snippet]. Faucet-drain model (faucets mint, sink is the in-game pool, drains exit) from a 2004 analysis: https://boingboing.net/2004/05/03/star-wars-galaxies-e.html [S]. Item damage and decay appear in his economy spectrum where items are player-sourced [S, post not identified].
- WildStar (Carbine) dev blog 2013: item repair as "a very handy and fair money sink" that should not be painful because it offers little choice. https://www.engadget.com/2013-05-31-wildstar-economy-post-talks-item-decay-raph-koster-and-more.html [F]

## 10. Academic: agent-based computational economics

- Tesfatsion, "Agent-Based Computational Economics: A Constructive Approach to Economic Theory", Handbook of Computational Economics vol. 2 (2006), ch. 16: economies as dynamic systems of interacting agents; covers production, pricing and trade processes, strategic rivalry, learning, institutions; illustrates with a two-sector decentralised market model. https://ideas.repec.org/p/isu/genres/12514.html ; https://faculty.sites.iastate.edu/tesfatsi/archive/tesfatsi/hbace.htm [S; site cert error, chapter not opened]
- Dosi, Fagiolo, Roventini, "Schumpeter meeting Keynes" (K+S): capital-good firms do R&D and build machines, consumption-good firms invest and produce a homogeneous consumption good, workers, a bank; finds complementarity of demand generation and innovation shapes cycles and growth; extension with income distribution, credit, fiscal policy (Dosi, Fagiolo, Napoletano, Roventini); LEM WP 2014/21 with Treibich; Dosi and Roventini, Cambridge Elements "Agent-Based Macroeconomics" (2025). https://www.cambridge.org/core/elements/abs/agentbased-macroeconomics/B7016A5CEE59694A420AF51A1347F00E [F, abstract only]; https://www.lem.santannapisa.it/WPLem/2014-21.html [S]
- Epstein and Axtell, "Growing Artificial Societies" (1996), Sugarscape: renewable sugar on a grid, agents with metabolism and vision; skewed wealth emerges; adding spice allows trade and an emergent market. https://jasss.org/12/1/6/appendixB/EpsteinAxtell1996.html [S, not opened]
- Hallegatte ARIO (Adaptive Regional Input-Output): input-output structure plus production capacity limits, input scarcity, inventories, adaptive behaviour; Katrina total cost about $149B against $107B direct; most sensitive parameters were construction-sector overproduction capacity and adaptation time; losses nonlinear above about $50B direct. https://hats.arizona.edu/node/300 [S]. Extensions R-ARIO and a credit-based ARIO [S].

## 11. Leontief input-output dynamics

- Static model x = Ax + y, x = (I-A)^-1 y; a nonnegative output exists for any nonnegative final demand iff the Hawkins-Simon condition holds (leading principal minors of I-A positive; equivalent to some x>=0 with (I-A)x>0); the Neumann series converges when the largest eigenvalue of A is less than 1. https://en.wikipedia.org/wiki/Input%E2%80%93output_model [F]; https://en.wikipedia.org/wiki/Hawkins%E2%80%93Simon_condition [F]. In plain terms: a closed chain must yield more of each good than the chain itself consumes, or positive final demand cannot be satisfied.
- Dynamic Leontief model adds capital coefficients B; B is often singular; forward-looking solutions were "unrealistic and fluctuating" and interest declined after the 1970s; one author group claims a positive capital matrix makes the dynamic model always unstable (single-group claim). Wikipedia [F]; Blanc Diaz and Ramos https://www.iioa.org/conferences/intermediate-2004/pdf/blanc.pdf [S].
- Critiques: fixed prices, constant returns, poor handling of relative price changes, ignores resource constraints. Wikipedia [F].
- Not found: a game-development source applying Hawkins-Simon or spectral-radius checks to recipe graphs. Linear-solver recipe calculators for Factorio/Satisfactory use this form [M].

## 12. Index of observed items by report axis

- Chain depth: X4 2 to 4 steps with race branches; Factorio 3 to 7+ steps (up to about 477 raws per pack); Satisfactory by-product branches; Vic3 a web of buildings with PM-swapped inputs; Anno short chains plus service buildings feeding tiers; Starsector 1 to 2 steps; EVE 3 to 5 steps plus reaction and invention branches.
- Non-gear demand drivers: pop-tier needs (Anno, Vic3, Starsector colony size), workforce food/medical (X4), research consumption (Factorio), destruction (EVE, X4 war economy), points sink (Satisfactory), mood needs (RimWorld, DF), stability from met demand (Starsector), happiness (Anno).
- Stall mechanisms: seed-dependent shortages and full-storage consumers (X4), by-product backup (Satisfactory), +/-75% price band (Vic3), deficit makes residents leave (Anno), shipping disruption (Starsector).
- Anti-snowball mechanisms: zero-sum influence, expansion thresholds and retreat (Elite BGS, community guide), upkeep per building (Anno), profit-bounded trade routes (Vic3), industry cap Size-2 (Starsector), system cost index (EVE, name only).
- Legibility devices: transaction log and wares table (X4), hover income per need (Anno), Deficit/Excess tooltip (Starsector), MER with data download (EVE), production statistics (Factorio [M]).
- Failure modes with player or developer comment: under each section (X4 s1, Anno s2, Vic3 s3, EVE s4, UO s9).

## Gaps this pass did not close
- No first-party Egosoft text on the 4.0+ workforce/habitat model; no Anno devblog; no CCP blog on reprocessing/reaction design or on the destruction-demand claim beyond the 2011 report; Vic3 market dev diaries (prices, buy/sell orders) not located; DF mandates not verified; Simpson UO paper body not read; Tesfatsion chapter and Dosi papers read only to abstract level.
