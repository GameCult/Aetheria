# Aetheria living world: target, revision 2 (the economy)

Date: 2026-10-22. Status: target, drafted by Imagination (session self-2026-10-19-ag7) for Self to admit. Revision 2
takes in the day's later rulings: consumer goods built from gear's components (`consumer-goods-share-gear-components`),
DemandProfile deleted in cut 1 (`demandprofile-goes-in-cut-1`), the gate as an exchange (`region-gate-is-an-exchange`),
zone-local workforce (`workforce-is-zone-local`), the station-kind stand-in (`station-kind-family-stand-in`) and
give-only transfer (`transfer-is-give-only`).
Body pinned at `GameCult/Aetheria` origin/master `5c38029e`. This revision covers the economy. The rest of the
strategic layer arrives in a later revision of this document: holdings changing hands, expansion, the lore institutions
as damping rules, and the strategic arena's scoring.

## 1. Why

The operator: "We cannot simulate an economy without "the crafting work". Turning basic stuff into more complex stuff
while trying to get an edge on your competitors is kinda the whole game." (`crafting-is-the-game`, 2026-10-22).

The demo carries the living world (`demo-live-expansion`, `demo-one-region-living-presence`). Factions act on live needs,
and the job board posts work from them. Whether the demo ships a Sol-seeded history or a seeded region depends on how
well this simulation performs (`demo-start-gated-on-sim-quality`). The arenas train on the galaxy generator, extended
with plausible starting infrastructure (`strategic-start-r2-generator-trains`). So the economy is not decoration on a
fixed map. It is the engine the strategic layer, the arenas and the demo all run on.

The rulings fix its shape:

- **Chains are deep.** High-end goods sit five or more steps from extraction, with many intermediates, so the
  station-kind set grows past the 17 kinds the station round authored (`chains-are-deep`).
- **Recipes.** Every commodity has an authored recipe naming its inputs, the station kind that makes it and a lore hook.
  Gear and hulls take crafting components by role, permanently. That is what lets a higher-tech component replace an
  old one without a new gear recipe (`named-recipes-gear-takes-components-by-role`).
- **Quality.** Stock carries grade from the first chain cut. Each facility's owner sets its good/fast/cheap setting and
  routes better inputs to premium facilities (`crafting-is-the-game`, `quality-triangle-is-per-facility`). The final
  assembly bounds what the parts give (`quality-is-assembly-over-supply-chain`, `quality-min-tolerance-binned-assembly`).
  A maker may bin one design into several products (`maker-design-tiers-allowed`).
- **Demand.** Demand that is not gear comes from authored needs tables for population and institution kinds. The owning
  faction issues unmet needs as obligations: quota, purchase order, contract or bounty (`demand-is-obligations`).
  DemandProfile is obsolete and goes in the first cut. The operator: "DemandProfile was a crude field that this work
  clearly replaces with much more nuanced push and pull" (`demandprofile-goes-in-cut-1`).
- **Consumer goods.** The old joke compounds become consumer products. Like gear, they are built by role from the same
  component commodities gear uses. The operator: "consumers provide predictable demand for compound commodities in the
  tree. The same cognition substrate that goes in your gear's automation components can also make someone's toaster
  smart" (`consumer-goods-share-gear-components`). Her examples illustrate; the set is not closed.
- **The region's edge.** The gate is an exchange with an authored outside market at band prices and a capped rate. A
  good the region cannot make costs the outside price plus the haul, and a blockade of the gate is felt as shortages
  (`region-gate-is-an-exchange`).
- **Allocators.** Faction economies are compiled allocators, one per economy kind (plan, market, project contract,
  contraband). They all run over one needs-and-stockpile layer (`allocator-per-economy-kind`, `faction-economic-models`).
- **Labour.** Labour is a typed input, and every workforce kind has its own needs (`typed-workforce`). Zhestokost
  permits no biodrones and no AGI automation (`faction-economic-models`). Human crews live in residential stations in
  their facility's own zone; between zones people move only by slow off-screen migration toward work and goods.
  Biodrones remain cargo (`workforce-is-zone-local`).
- **Sinks.** Goods leave by consumption, destruction and upkeep. There is no general decay
  (`sinks-consume-destroy-maintain`). Fights off screen destroy goods by the composition model's expected losses
  (`matchup-composition-model`).
- **What the player reads.** The player reads shortages through a floating price band and through records. A record has
  an issuer, a date and a confidence, and it can be stale, wrong or a lie (`shortages-are-records-and-price-band`).
- **Cargo is lore.** An intercepted shipment labelled biodrones is a way into the world (`catalog-crafting-chains`).
- **Stations.** Each faction gets stations for each part of its economy. The hull kinds and the simulation's station
  kinds are one list (`stations-represent-faction-economy`, `faction-station-models`). A new kind whose faction has no
  model yet stands in with that faction's model of the nearest existing kind, keeping its own name, facilities and
  stock (`station-kind-family-stand-in`).
- **Goods move** on dock, into enclosed bays where the docked entity leaves space (`docking-is-enclosed-bays`), or by
  the ship-to-ship transfer verb (`tender-transfer-verb`). A transfer is always a give by the holder's own pilot
  (`transfer-is-give-only`).

Prior art (`docs/research/2026-10-22-crafting-chains-prior-art.md`) names the failure modes this target is built
against:

- X4's seed-dependent shortages, and consumers stopping once storage is full;
- by-product backups in Satisfactory;
- the sink debate over EVE's destruction economy;
- the Anno deficit spiral;
- UO's dynamic ecology, which had to be constrained heavily to stay predictable.

The Leontief and Hawkins-Simon conditions give the catalog a productivity check that the note found no game applying.

## 2. Body facts (read at 5c38029e)

- **Commodity records.** `SimpleCommodityData` (`ItemData.cs:323`) has a `SimpleCommodityCategory` (`Enums.cs:155`):
  Minerals, Metals, Alloys, Compounds, Organics, Ammo and Consumer. The enum mixes raw goods with processed ones, and its
  stage is not derivable from anything. `CompoundCommodityData` (`ItemData.cs:353`) carries `DemandProfile`, a Big Five
  weight map over `PersonalityAttribute`, and `CompoundCommodityCategory`. No code reads `DemandProfile`.
  `PersonalityAttribute` stays because `Corporations.cs:28` also uses it. The only reader of either category is
  `TradeMenu` (`UI/Menu/TradeMenu.cs:31-32,65-151,200-216,240-244`): two filter dropdowns and the Type column.
- **What the catalog holds** (read through `AetheriaStores.Open` on the LFS file at 5c38029e). 13 simple commodities:
  Minerals (Rock, Hydrogen, Potassium), Metals (New Metals, Uranium, Gold), Compounds (Methane, Water), Organics (Algae,
  Bacteria, Fungi, Animal Products) and Ammo (one commodity, `Ammo`); no record is Alloys or Consumer. 51 compound
  commodities, none with roles and none sold by a product. Four carry a DemandProfile (Neural Lace, Sure Bet Sherbet
  Fountain, Super Tough Toffee, Holo-desk: seven keys in all). By the old category they split into consumer-shaped goods
  (Wearables 9, Consumables 3, Luxuries 8, Tools 7) and component-shaped ones (Manufacturing 18, such as Circuit Board,
  Heat Sink Tile, Sheet Metal, Support Strut, Fuel; Assemblies 6, such as Reaction Chamber, Cockpit, Cargo Pod). The
  content pass in cut 3 reads that split from this list and from git history; the field itself goes in cut 1.
- **The Ammo commodity is live.** `Weapon.AmmoType` (`Behaviors/Weapon.cs:64`) references it: the Autocannon and the
  point-defence and flak designs draw it (seven references in the catalog), and `EvasionTrackingTests.cs:362-396` pins
  that. Its replacement by branded rounds of a calibre belongs to follow-ups `branded-ammo-calibre` and `matrix-rounds`,
  not to this campaign. Only the `Ammo` value of `SimpleCommodityCategory` goes, with the enum.
- **No recipes.** No recipe or facility record exists. The five factory designs (Refinery, Assembly Line, Shipyard,
  Surface and Deep Ore Extractor) were deleted in `653ba490`. The station round's `INDEX.md` still lists them as a
  source, so it is stale on that point. That file is not in the repo: it is
  `F:\Projects\eureka-handoff-aux-2026-10-19\hull-bench\round2\briefs\stations\INDEX.md`, line 23.
- **Provenance.** It has the forward shape and no writer. `Produced {Faction, Station, Facility, Inputs}` and
  `Extracted {Zone, Commodity}` exist (`Provenance.cs:122-137`), and only tests construct them. Every mint writes an
  `Attributed` placeholder. `ProvenanceLedger.Reachable` already closes over `Produced.Facility` and `Inputs`.
- **Quality.** `Lot.Quality` is the assembly. `RoleFill` holds the parts. `QualityForRole` is the minimum of the part
  and the assembly, and `EffectiveQuality` is the mean of the capped roles (`Provenance.cs:64-98`). `GetTier` bins
  effective quality by `GameplaySettings.Tiers` (`ItemManager.cs:242`). Lots are minted from a product's distributions
  on the item stream (`ItemManager.cs:126-170`).
- **Station stock.** `GenerateStationLoadout` puts `RandomProducts<EquippableItemData>(16, 1, ...)` into a station's
  cargo once, at zone generation (`LoadoutGenerator.cs:77,126`). The station round's CC6 records that nothing ever
  restocks a station. Follow-up `station-restock` asks for a restock, and stockpiles supersede it. The trade menu sells
  from station cargo through `StationServices`, which prices goods from catalog price and lot quality alone
  (`StationServices.cs:13-40`).
- **No station kinds.** There is no station-kind datum. `ZoneGenerator` sets a station count from faction presence and
  draws any `HullType.Station` hull (`ZoneGenerator.cs:290-340,422,443`; follow-up `station-hull-source-fit` item 3).
- **Only the player's zone ticks.** No job generator exists (`docs/aetheria-arena-target.md` section 2). `HaulingTask`,
  `Mining`, `StationTowing` and `Survey` are defined, but nothing constructs them.
- **The lore seed.** It is one file: `AetheriaLore/Aetheria/Economy/Item Hooks/Pallas Strike Maintenance Assemblies.md`.
  It proposes the VitaForge BioDrone Standard Limiter Interface, the Cephalopod Dry-Operation Harness, the Bloom Manifold
  Seal Assembly and others, each with candidate parts and performance aspects. It reads as recipes waiting for a schema.

## 3. Invariants

These are the ends. Each label is what Soul measures against.

- `L-source-sink`: Every commodity has at least one source and at least one sink. A source is an extraction recipe, a
  recipe output, or the region gate's outside market selling it. A sink is a recipe input, a role fill, a needs-table
  entry, an upkeep entry, destruction for gear and hulls, or the outside market buying it. The catalog check refuses a
  commodity that has either one missing.
- `L-consumer-demand`: Every component commodity also has a consumer sink. A component commodity is one tagged for a
  role that some gear or hull recipe names. Its consumer sink is a consumer-good recipe that takes it by role, where a
  consumer good is a commodity that some population or institution needs table names. The gate does not count here: the
  demand must be the region's own. The catalog check refuses a component with no consumer sink. The role-fill half is
  checked from cut 3 and the needs-table half from cut 7.
  - Why it is enforced rather than reported: the ruling makes consumer demand part of how the economy stays stable, and
    a catalog-wide property that is only reported rots as content grows. A component that seems military only is a
    content prompt (a fuze also fires a mining charge), not an exemption. No exemption list exists.
- `L-productive`: The recipe graph is productive. Its input matrix A has spectral radius below 1 (Hawkins-Simon).
  - A is augmented with each workforce kind's needs per unit of labour, so the labour, food and labour loop is in the
    matrix. It also holds every facility's upkeep per unit of output.
  - The check is measured at the most input-hungry good/fast/cheap setting any facility may choose.
  - Catalog validation computes it on every catalog build, and a failure names the cycle that carries the eigenvalue.
- `L-deep`: Every high-end good has a longest path of five or more recipe steps from extraction. The catalog check
  reports depth per good, and Self names which goods count as high-end in the recipe cut.
- `L-recipe`: Every commodity has one authored recipe. The recipe names its inputs, its cycle, its workforce need, the
  station kind that makes it and its lore hook. Every recipe input is a commodity, except in gear, hull and consumer-good
  recipes, whose inputs are roles. No recipe is generated at runtime.
- `L-roles`: A gear, hull or consumer-good recipe names roles, never specific component commodities. Any commodity
  tagged for a role can fill it. A higher-tech component therefore enters a design, or a toaster, without a new recipe.
- `L-one-stock`: One owner, the stock ledger, holds every count of every good outside an entity's cargo. Station trade,
  hauling, production, consumption, upkeep and destruction all change counts through its commits and nothing else.
  Station cargo generated at zone creation is not a second stock.
- `L-graded`: Stock is held by grade. A production step's output grade is its facility's assembly, as the facility's
  good/fast/cheap setting gives it, capped by the grades of the inputs it consumed. This is the
  min-with-tolerance rule, so the best parts cannot rescue a poor assembly. No step other than production writes a
  grade.
- `L-lot-at-boundary`: Stock becomes a `Lot` only when it crosses into an entity: on dock, by the transfer verb, by
  seizure or by loot. The new lot carries a `Produced` or `Extracted` origin that names the station and facility it came
  from. Its input lots are synthesised to agree with its grade. The ledger never mints a lot for stock that no entity
  holds.
- `L-no-omniscience`: The player reads the economy only through the price band at stations they dock at, and through
  records: obligations, notices, and scanned or seized manifests. Every record has an issuer, a date and a confidence.
  No player-facing surface reads the stock ledger, needs tables or allocator state directly.
- `L-obligations`: All demand that is not gear reaches the world as obligations, which the owning faction's allocator
  issues from unmet needs. Haulers, NPC or player, serve obligations, and the job board lists them. No other writer posts
  work.
- `L-allocator`: Each faction names one compiled allocator per economy kind. An allocator reads needs, stock and prices.
  It issues obligations, sets prices inside the band, sets rations, and chooses recipes and settings for its own
  faction's facilities. It never writes a stock count.
- `L-workforce`: A facility's throughput is gated by a workforce of a kind its owning faction permits. Each kind's needs
  are a needs table like any other. Zhestokost facilities never hold biodrones or AGI automation.
- `L-zone-crew`: A facility's human crew comes only from residential populations in its own zone. People cross zones
  only through the population step's migration, which is slow and moves toward work and goods. Biodrones are cargo and
  move as stock.
- `L-sinks`: Goods leave only by consumption (needs and recipe inputs), destruction (on screen, or the matchup model's
  expected losses off screen), upkeep, and export through the region gate. No stock decays with time.
- `L-gate-cap`: The region gate trades each commodity at no more than its capped rate per tick, at band prices around
  the outside market's price. Imports cost the outside price plus the haul. Its flows are stock-ledger commits like any
  other. The cap is below the region's demand for every good its kind set can make, so the gate supplements an
  in-region chain and cannot stand in for a broken one.
- `L-no-stall`: A region left alone does not stall. Over the arena's generated seed set, a region simulated for the
  check's horizon with no player keeps every recipe in its station-kind set producing. No commodity's regional output
  sits at zero for longer than the check's window while its inputs exist in reach. No population falls to its floor and
  stays there. The gate is open at its cap (`L-gate-cap`) while the check runs, so it counts imports as inputs in reach
  but cannot hide a chain that only the gate keeps alive. The strategic arena runs this check, and its horizon and window
  are set in the stall-check cut.
- `L-one-sim`: The economy on screen and off screen is one set of rules. The off-screen tick is the same tick at reduced
  detail: flows instead of ships, and the matchup model instead of combat. It is never a second model. This shares the
  arena's `A-one-sim`, `A-pure` and `A-no-clock`. Every economic draw is a `SimulationDice` draw keyed by a structured
  seed.
- `L-lore`: Every commodity carries a lore hook: what a player learns by inspecting or intercepting it.
- `L-one-kind-list`: The station kinds the simulation runs and the station kinds the hulls are authored for are one
  catalog list. A kind whose faction has no model yet names its stand-in kind on its own record. It is placed with that
  faction's model of the stand-in kind, keeping its own name, facilities and stock, and the stand-in reference is
  deleted when the model lands.

## 4. What is not a consumer, and not in scope

Not consumers (nothing in this campaign keeps them working or compatible):

- `DemandProfile`, deleted in cut 1 (`demandprofile-goes-in-cut-1`).
- `CompoundCommodityCategory` and `SimpleCommodityCategory` as stored fields, deleted in cut 1, with the trade menu's
  two filters that read them. A commodity's stage is derived from its recipe: raw if extraction makes it, processed
  otherwise. Whether it is a component is said by its role tags, and whether it is a consumer good by the needs tables
  that name it. A stored category would be a third answer that can disagree with both.
- `RandomProducts` used as station stock.
- Follow-up `station-restock`, which stockpiles supersede.
- The five deleted factory designs.
- The `INDEX.md` table's claim that factory designs exist.
- AetheriaEve, which is archived.

Kept, and not this campaign's to delete: the 51 compound commodity records, which become consumer goods and components
in cut 3 (`consumer-goods-share-gear-components`), and the `Ammo` commodity, which weapons draw today (section 2).

Not in scope for this revision:

- Ownership changing hands, expansion, the lore institutions as damping rules
  (`snowball-damping-lore-institutions`) and strategic scoring. These come in the next revision of this target, and
  they share these owners.
- Fitting the matchup model (campaign `aetheria-matchups`). This campaign consumes it through one typed predictor and
  uses a fixed stub until it lands.
- Named, recoverable components and disassembly (`named-components-after-demo`). Role fills stay statistical on
  lots. Stock is graded counts, not named parts.
- Alternative recipes per commodity, which would be production methods in the Victoria sense. Facilities trade off
  good, fast and cheap. They do not switch recipes.
- An omniscient economy panel, now or as a debug view the player can reach. A developer view through Eve/CultMesh is
  allowed and reads the same ledger.
- Runtime model calls, and agent-written content at runtime. Allocators ship compiled, like brains.
- Station hull art. The hull bench owns it. This campaign owns only the station-kind list the hulls are authored
  against.

## 5. Identity, lifecycle, authority (Eureka step 0b)

| Kind | What names it | What happens to it over time | Who decides (owner; forbidden writers) |
|---|---|---|---|
| Commodity | A catalog record with a stable id and name. One commodity kind; its stage (raw or processed) is derived from its recipe, not stored. It may carry role tags naming the gear, hull and consumer-good roles it can fill. A consumer good is a commodity a needs table names; its recipe takes roles, as gear's does | Authored as content from the lore, agent-written and reviewed by the operator, and revised by commit. It never changes during a run | Owner: the catalog, with the economy content pass as author. Forbidden: runtime creation, and a category field that disagrees with the recipe |
| Recipe | One per output commodity, keyed by that commodity's id. It names its inputs (commodity and count, or role for gear and hulls), its cycle, its workforce need, its station kind and its lore hook | Authored with its commodity. Catalog validation (`L-source-sink`, `L-productive`, `L-deep`, `L-lore`) checks it on every catalog build. It never changes during a run | Owner: the catalog. Checks: AetherDb validation. Forbidden: runtime-generated or allocator-edited recipes |
| Station kind | A catalog record with a stable id, such as `processing.smelter`. It names its facility slots (the recipes it may run), its resident population kind and its institution kind, and it maps each faction that builds it to a hull. Until that faction's model exists, the mapping names a stand-in kind instead, whose hull for that faction is used (`station-kind-family-stand-in`) | Authored content that grows with the chains past 17. Each new kind is authored with the recipes that need it and its stand-in. The hull bench station round adds a model per faction that builds it, and that commit replaces the stand-in reference with the hull | Owner: the catalog, as the one list (`L-one-kind-list`). Kind data is authored by the economy content pass, and models by the hull bench (`faction-station-models`). Forbidden: `ZoneGenerator` drawing any station hull without a kind |
| Facility | (station id, slot ordinal). It holds its recipe, its owner (faction or corporation), its good/fast/cheap setting and its workforce slot | Built in place by its owner from raw materials, and retooled at a cost when its recipe or a sub-assembly changes (item-provenance F2). It runs, slows when short of workforce, idles when short of input, and is destroyed with its station | Owner: the facility's owner, through that faction's allocator, which sets recipe and setting. The production step runs it. Forbidden: any writer of its setting other than the owner |
| Stockpile | (holder station id, commodity, grade). The holder is a station; goods in a ship are cargo and become lots | Created on first deposit. Its count changes only through stock-ledger commits: produce, consume, upkeep, transfer (dock, transfer verb, off-screen haul flow), destroy. It is never decayed or regenerated | Owner: the stock ledger, one ServerShared owner (`L-one-stock`). Forbidden: trade menu, allocators, `GenerateStationLoadout` and any Unity type |
| Grade | A bin index on the existing quality scale. The bins are `GameplaySettings.Tiers`, the ones `GetTier` already reads, so a stock grade and an item's tier mean one thing | Fixed per catalog revision. Stock's grade is set when it is produced and never changes. A lot minted from stock draws its assembly and fills inside the grade's bin | Owner: the production step, by `L-graded`. Bins: `GameplaySettings`. Forbidden: price, allocators or records writing a grade |
| Workforce | Its kind is a catalog record (human, a supplier's biodrone line, automation) with its own needs table. A facility's staffing is (facility, kind, count) | Humans live in residential stations and staff facilities in the same zone only; between zones they move by the population step's slow migration toward work and goods (`workforce-is-zone-local`, `L-zone-crew`). Passenger transport may come later as content. Biodrones are bought as cargo, which is stock until installed in a facility, and then draw their feed and limiter upkeep as needs. Automation is built where its faction allows it | Owner: kinds in the catalog, and which kinds a faction permits on the faction record. Staffing: the strategic tick's population step. Forbidden: a facility staffed by a kind its faction does not permit |
| Population or institution | (station, population or institution kind), with a size | Grows while its basic needs are met. Under a sustained shortfall it shrinks slowly, down to a floor, so a region can recover. Size scales its needs table | Owner: the population step of the strategic tick. Forbidden: needs read as fixed numbers that ignore size |
| Needs table | One catalog record per population, institution or workforce kind: commodity (or role) to a per-capita rate, split into basic and lifestyle | Authored, and revised by commit. Scaled by size every tick and never stored per instance. Biodrone tables carry no entertainment or lifestyle rows (`typed-workforce`) | Owner: the catalog. Forbidden: `DemandProfile`, and any demand formula outside the tables |
| Obligation | (issuer faction, issuing station, ordinal). It holds a kind (quota, purchase order, contract, bounty), a commodity, a grade floor, a quantity, a deadline, a reward and a consequence | The allocator issues it from an unmet need. A hauler, NPC or player, claims it. A delivery commit through the stock ledger fulfils it, or its deadline passes and the issuer applies the consequence. It is never edited: a revised demand is a new obligation that supersedes the old | Owner: the issuing faction's allocator. The job board lists obligations and decides nothing. Forbidden: a job generator of its own (`Galaxy.cs:578` placeholder), and work with no issuer |
| Allocator | A compiled C# class with a stable id per economy kind: `plan`, `market`, `project-contract`, `contraband`. The faction record names its allocator | Code, revised by commits and versioned like arena brains. Each tick it reads needs, stock and prices. It writes obligations, prices within the band, rations, and recipes and settings for its own facilities | Owner: the allocator registry, compiled (`A-compiled`). Forbidden: runtime-loaded or model-written allocators, and an allocator writing a stock count |
| Price band | (station, commodity, grade). The catalog price is the base; the band width is a parameter of the station's allocator | Derived each tick from stock against the allocator's target, inside the band. A plan allocator may hold its band narrow and ration instead | Owner: the station's allocator, within the band. `StationServices` reads the price and decides none of it. Forbidden: a price from catalog price and quality alone once stock exists |
| Notice or record | (issuer, subject, issued date). It holds a confidence and its source: dock, broadcast, scan or seizure. A manifest is a record of one cargo | Created when the player docks, hears a broadcast, scans or seizes. It is never updated, so it grows stale with age. It may be false, if its issuer lies | Owner: the record maker, from obligations and stock at the moment of issue, through the issuer's honesty policy. The player's surfaces read only records and the price band (`L-no-omniscience`). Forbidden: any UI reading the stock ledger |
| Population or institution migration | (from zone, to zone, population kind), a flow per tick | The population step moves people slowly toward zones with unfilled work and met needs. It is a flow, never a ship | Owner: the population step. Forbidden: any other writer of a population's size or zone |
| Region boundary (outside market) | One catalog record per region gate: per commodity, an outside price, whether it buys, sells or both, and a capped rate per tick | Authored, and revised by commit. Each tick the gate step trades at band prices around the outside price, up to the cap, through stock-ledger commits. A blockade of the gate stops its flows (`region-gate-is-an-exchange`) | Owner: the catalog for the record; the gate step of the strategic tick for the flows. Checks: `L-source-sink` counts it, `L-consumer-demand` does not. Forbidden: flows above the cap, and a gate writing stock other than by ledger commits |

Every cell is filled from rulings or Body facts.

## 6. Cut sequence

Each line names its cut and what it waits on.

1. `catalog-subtraction`: delete `DemandProfile` and both stored categories with their enums, and the trade menu's two
   category filters; withdraw `station-restock`; mark the `INDEX.md` factory-design line stale. The compound records
   and the `Ammo` commodity stay. Waits on nothing.
2. `station-kind-datum`: add the station-kind record, the hull per (faction, kind) and the stand-in kind
   (`station-kind-family-stand-in`); `ZoneGenerator` places stations by kind (closing `station-hull-source-fit` item 3).
   Waits on nothing; it touches no file cut 1 touches.
3. `commodities-and-recipes`: the lore-built commodity set, seeded from `Economy/Item Hooks/` and from the 51 kept
   compounds, which become consumer goods and components; recipes with role inputs for gear, hulls and consumer goods;
   the region gate's outside-market record. It also adds the new station kinds and the catalog checks (`L-source-sink`
   counting the gate, `L-consumer-demand`'s role-fill half, `L-productive`, `L-deep`, `L-recipe`, `L-lore`). It is an
   economy content pass (agent-authored, operator reviews) plus one Hands cut for the schema and the checks. Waits on 1
   and 2.
4. `stock-ledger`: stockpiles by (station, commodity, grade); station trade materialises lots on dock
   (`L-lot-at-boundary`); delete `RandomProducts` as station stock. Waits on 3.
5. `facilities-and-setting`: facility records, the production step, the good/fast/cheap setting and grade output by
   `L-graded`. Waits on 4.
6. `workforce`: workforce kinds, faction permits, residential staffing from the facility's own zone (`L-zone-crew`),
   and biodrone cargo and upkeep. Waits on 5.
7. `needs-and-obligations`: needs tables, population size, obligations, and the job board reading them; the
   needs-table half of `L-consumer-demand`. Waits on 4 and 6.
8. `allocators`: the four compiled allocators, the faction record naming one, the price band and rations. Waits on 7.
9. `off-screen-tick`: every zone in the region ticks the economy at reduced detail under `SimClock`; hauling as flows;
   the matchup predictor stub as the destruction sink; it runs in the arena runner; the gate step trades with the
   outside market at its cap (`L-gate-cap`); the population step migrates people between zones. Waits on 8 and on
   aetheria-arena cuts 1 and 5.
10. `legibility`: notices, records, manifests on scan or seizure, and the price band shown at dock. Waits on 7 and 9.
11. `stall-checks`: the strategic arena runs `L-no-stall`, with the gate open at its cap, and runtime `L-source-sink`
    over generated seeds and reports the horizon and window. Waits on 9 and on aetheria-arena cut 6.

**Who authors the new station kinds.** Deep chains grow the kind list past 17.

- **Kind data.** The economy content pass authors each new kind in cut 3, together with the recipes that need it. That
  pass is Imagination with lore authoring, agent-written, and the operator reviews it as she reviews recipes.
- **Models.** The hull bench station round authors a model for each (faction, kind) pair under `faction-station-models`.
  Its next round takes its brief list from cut 3's new kinds. Until each model lands, the kind stands in with its
  faction's model of the stand-in kind its record names (`station-kind-family-stand-in`).
