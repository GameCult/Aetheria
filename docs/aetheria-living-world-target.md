# Aetheria living world: target, revision 1 (the economy)

Date: 2026-10-22. Status: target, drafted by Imagination (session self-2026-10-19-ag7) for Self to admit.
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
- **Allocators.** Faction economies are compiled allocators, one per economy kind (plan, market, project contract,
  contraband). They all run over one needs-and-stockpile layer (`allocator-per-economy-kind`, `faction-economic-models`).
- **Labour.** Labour is a typed input, and every workforce kind has its own needs (`typed-workforce`). Zhestokost
  permits no biodrones and no AGI automation (`faction-economic-models`).
- **Sinks.** Goods leave by consumption, destruction and upkeep. There is no general decay
  (`sinks-consume-destroy-maintain`). Fights off screen destroy goods by the composition model's expected losses
  (`matchup-composition-model`).
- **What the player reads.** The player reads shortages through a floating price band and through records. A record has
  an issuer, a date and a confidence, and it can be stale, wrong or a lie (`shortages-are-records-and-price-band`).
- **Cargo is lore.** An intercepted shipment labelled biodrones is a way into the world (`catalog-crafting-chains`).
- **Stations.** Each faction gets stations for each part of its economy. The hull kinds and the simulation's station
  kinds are one list (`stations-represent-faction-economy`, `faction-station-models`).
- **Goods move** on dock, into enclosed bays (`docking-is-enclosed-bays`), or by the ship-to-ship transfer verb
  (`tender-transfer-verb`).

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
  `PersonalityAttribute` stays because `Corporations.cs:28` also uses it. The compound set is a set of gag placeholders,
  not lore (predecessor's pass).
- **No recipes.** No recipe or facility record exists. The five factory designs (Refinery, Assembly Line, Shipyard,
  Surface and Deep Ore Extractor) were deleted in `653ba490`. The station round's `INDEX.md` still lists them as a
  source, so it is stale on that point.
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

- `L-source-sink`: Every commodity has at least one source and at least one sink. A source is an extraction recipe or a
  recipe output. A sink is a recipe input, a role fill, a needs-table entry, an upkeep entry, or destruction for gear and
  hulls. The catalog check refuses a commodity that has either one missing.
- `L-productive`: The recipe graph is productive. Its input matrix A has spectral radius below 1 (Hawkins-Simon).
  - A is augmented with each workforce kind's needs per unit of labour, so the labour, food and labour loop is in the
    matrix. It also holds every facility's upkeep per unit of output.
  - The check is measured at the most input-hungry good/fast/cheap setting any facility may choose.
  - Catalog validation computes it on every catalog build, and a failure names the cycle that carries the eigenvalue.
- `L-deep`: Every high-end good has a longest path of five or more recipe steps from extraction. The catalog check
  reports depth per good, and Self names which goods count as high-end in the recipe cut.
- `L-recipe`: Every commodity has one authored recipe. The recipe names its inputs, its cycle, its workforce need, the
  station kind that makes it and its lore hook. Every recipe input is a commodity, except in gear and hull recipes,
  whose inputs are roles. No recipe is generated at runtime.
- `L-roles`: A gear or hull recipe names roles, never specific component commodities. Any commodity tagged for a role
  can fill it. A higher-tech component therefore enters a design without a new gear recipe.
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
- `L-sinks`: Goods leave only by consumption (needs and recipe inputs), destruction (on screen, or the matchup model's
  expected losses off screen) and upkeep. No stock decays with time.
- `L-no-stall`: A region left alone does not stall. Over the arena's generated seed set, a region simulated for the
  check's horizon with no player keeps every recipe in its station-kind set producing. No commodity's regional output
  sits at zero for longer than the check's window while its inputs exist in reach. No population falls to its floor and
  stays there. The strategic arena runs this check, and its horizon and window are set in the stall-check cut.
- `L-one-sim`: The economy on screen and off screen is one set of rules. The off-screen tick is the same tick at reduced
  detail: flows instead of ships, and the matchup model instead of combat. It is never a second model. This shares the
  arena's `A-one-sim`, `A-pure` and `A-no-clock`. Every economic draw is a `SimulationDice` draw keyed by a structured
  seed.
- `L-lore`: Every commodity carries a lore hook: what a player learns by inspecting or intercepting it.
- `L-one-kind-list`: The station kinds the simulation runs and the station kinds the hulls are authored for are one
  catalog list.

## 4. What is not a consumer, and not in scope

Not consumers (nothing in this campaign keeps them working or compatible):

- `DemandProfile` and `CompoundCommodityCategory`. Their deletion is ruled.
- `SimpleCommodityCategory` as a stored field. A commodity's stage is derived from its recipe: raw if extraction makes
  it, processed otherwise.
- The gag compound commodities.
- `RandomProducts` used as station stock.
- Follow-up `station-restock`, which stockpiles supersede.
- The five deleted factory designs.
- The `INDEX.md` table's claim that factory designs exist.
- AetheriaEve, which is archived.

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
| Commodity | A catalog record with a stable id and name. One commodity kind; its stage (raw or processed) is derived from its recipe, not stored. It may carry role tags naming the gear and hull roles it can fill | Authored as content from the lore, agent-written and reviewed by the operator, and revised by commit. It never changes during a run | Owner: the catalog, with the economy content pass as author. Forbidden: runtime creation, and a category field that disagrees with the recipe |
| Recipe | One per output commodity, keyed by that commodity's id. It names its inputs (commodity and count, or role for gear and hulls), its cycle, its workforce need, its station kind and its lore hook | Authored with its commodity. Catalog validation (`L-source-sink`, `L-productive`, `L-deep`, `L-lore`) checks it on every catalog build. It never changes during a run | Owner: the catalog. Checks: AetherDb validation. Forbidden: runtime-generated or allocator-edited recipes |
| Station kind | A catalog record with a stable id, such as `processing.smelter`. It names its facility slots (the recipes it may run), its resident population kind and its institution kind, and it maps each faction that builds it to a hull | Authored content that grows with the chains past 17. Each new kind is authored with the recipes that need it. The hull bench station round adds a model per faction that builds it (question `station-kind-before-model`) | Owner: the catalog, as the one list (`L-one-kind-list`). Kind data is authored by the economy content pass, and models by the hull bench (`faction-station-models`). Forbidden: `ZoneGenerator` drawing any station hull without a kind |
| Facility | (station id, slot ordinal). It holds its recipe, its owner (faction or corporation), its good/fast/cheap setting and its workforce slot | Built in place by its owner from raw materials, and retooled at a cost when its recipe or a sub-assembly changes (item-provenance F2). It runs, slows when short of workforce, idles when short of input, and is destroyed with its station | Owner: the facility's owner, through that faction's allocator, which sets recipe and setting. The production step runs it. Forbidden: any writer of its setting other than the owner |
| Stockpile | (holder station id, commodity, grade). The holder is a station; goods in a ship are cargo and become lots | Created on first deposit. Its count changes only through stock-ledger commits: produce, consume, upkeep, transfer (dock, transfer verb, off-screen haul flow), destroy. It is never decayed or regenerated | Owner: the stock ledger, one ServerShared owner (`L-one-stock`). Forbidden: trade menu, allocators, `GenerateStationLoadout` and any Unity type |
| Grade | A bin index on the existing quality scale. The bins are `GameplaySettings.Tiers`, the ones `GetTier` already reads, so a stock grade and an item's tier mean one thing | Fixed per catalog revision. Stock's grade is set when it is produced and never changes. A lot minted from stock draws its assembly and fills inside the grade's bin | Owner: the production step, by `L-graded`. Bins: `GameplaySettings`. Forbidden: price, allocators or records writing a grade |
| Workforce | Its kind is a catalog record (human, a supplier's biodrone line, automation) with its own needs table. A facility's staffing is (facility, kind, count) | Humans live in residential stations and staff facilities (question `human-workforce-mobility`). Biodrones are bought as cargo, which is stock until installed in a facility, and then draw their feed and limiter upkeep as needs. Automation is built where its faction allows it | Owner: kinds in the catalog, and which kinds a faction permits on the faction record. Staffing: the strategic tick's population step. Forbidden: a facility staffed by a kind its faction does not permit |
| Population or institution | (station, population or institution kind), with a size | Grows while its basic needs are met. Under a sustained shortfall it shrinks slowly, down to a floor, so a region can recover. Size scales its needs table | Owner: the population step of the strategic tick. Forbidden: needs read as fixed numbers that ignore size |
| Needs table | One catalog record per population, institution or workforce kind: commodity (or role) to a per-capita rate, split into basic and lifestyle | Authored, and revised by commit. Scaled by size every tick and never stored per instance. Biodrone tables carry no entertainment or lifestyle rows (`typed-workforce`) | Owner: the catalog. Forbidden: `DemandProfile`, and any demand formula outside the tables |
| Obligation | (issuer faction, issuing station, ordinal). It holds a kind (quota, purchase order, contract, bounty), a commodity, a grade floor, a quantity, a deadline, a reward and a consequence | The allocator issues it from an unmet need. A hauler, NPC or player, claims it. A delivery commit through the stock ledger fulfils it, or its deadline passes and the issuer applies the consequence. It is never edited: a revised demand is a new obligation that supersedes the old | Owner: the issuing faction's allocator. The job board lists obligations and decides nothing. Forbidden: a job generator of its own (`Galaxy.cs:578` placeholder), and work with no issuer |
| Allocator | A compiled C# class with a stable id per economy kind: `plan`, `market`, `project-contract`, `contraband`. The faction record names its allocator | Code, revised by commits and versioned like arena brains. Each tick it reads needs, stock and prices. It writes obligations, prices within the band, rations, and recipes and settings for its own facilities | Owner: the allocator registry, compiled (`A-compiled`). Forbidden: runtime-loaded or model-written allocators, and an allocator writing a stock count |
| Price band | (station, commodity, grade). The catalog price is the base; the band width is a parameter of the station's allocator | Derived each tick from stock against the allocator's target, inside the band. A plan allocator may hold its band narrow and ration instead | Owner: the station's allocator, within the band. `StationServices` reads the price and decides none of it. Forbidden: a price from catalog price and quality alone once stock exists |
| Notice or record | (issuer, subject, issued date). It holds a confidence and its source: dock, broadcast, scan or seizure. A manifest is a record of one cargo | Created when the player docks, hears a broadcast, scans or seizes. It is never updated, so it grows stale with age. It may be false, if its issuer lies | Owner: the record maker, from obligations and stock at the moment of issue, through the issuer's honesty policy. The player's surfaces read only records and the price band (`L-no-omniscience`). Forbidden: any UI reading the stock ledger |
| Region boundary | The region's wormhole gate | Depends on question `region-boundary`: either nothing crosses it, or an authored outside market trades through it at band prices and a capped rate | Owner: the outside-market record, if the ruling makes one. Until then this row is the question |

The table leaves three cells to questions: human workforce movement, the station-kind stand-in, and the region
boundary. Every other cell is filled from rulings or Body facts.

## 6. Cut sequence

Each line names its cut and what it waits on.

1. `catalog-subtraction`: delete `DemandProfile`, `CompoundCommodityCategory`, the stored `SimpleCommodityCategory` and
   the gag compounds; withdraw `station-restock`; mark the `INDEX.md` factory-design line stale. Waits on nothing.
2. `station-kind-datum`: add the station-kind record and the hull per (faction, kind); `ZoneGenerator` places stations
   by kind (closing `station-hull-source-fit` item 3). Waits on 1 and on question `station-kind-before-model`.
3. `commodities-and-recipes`: the lore-built commodity set, seeded from `Economy/Item Hooks/`, and recipes with role
   inputs for gear. It also adds the new station kinds and the catalog checks (`L-source-sink`, `L-productive`, `L-deep`,
   `L-recipe`, `L-lore`). It is an economy content pass (agent-authored, operator reviews) plus one Hands cut for the
   schema and the checks. Waits on 2.
4. `stock-ledger`: stockpiles by (station, commodity, grade); station trade materialises lots on dock
   (`L-lot-at-boundary`); delete `RandomProducts` as station stock. Waits on 3.
5. `facilities-and-setting`: facility records, the production step, the good/fast/cheap setting and grade output by
   `L-graded`. Waits on 4.
6. `workforce`: workforce kinds, faction permits, residential staffing, and biodrone cargo and upkeep. Waits on 5 and on
   question `human-workforce-mobility`.
7. `needs-and-obligations`: needs tables, population size, obligations, and the job board reading them. Waits on 4 and 6.
8. `allocators`: the four compiled allocators, the faction record naming one, the price band and rations. Waits on 7.
9. `off-screen-tick`: every zone in the region ticks the economy at reduced detail under `SimClock`; hauling as flows;
   the matchup predictor stub as the destruction sink; it runs in the arena runner; the region boundary goes in here.
   Waits on 8, on aetheria-arena cuts 1 and 5, and on question `region-boundary`.
10. `legibility`: notices, records, manifests on scan or seizure, and the price band shown at dock. Waits on 7 and 9.
11. `stall-checks`: the strategic arena runs `L-no-stall` and runtime `L-source-sink` over generated seeds and reports
    the horizon and window. Waits on 9 and on aetheria-arena cut 6.

**Who authors the new station kinds.** Deep chains grow the kind list past 17.

- **Kind data.** The economy content pass authors each new kind in cut 3, together with the recipes that need it. That
  pass is Imagination with lore authoring, agent-written, and the operator reviews it as she reviews recipes.
- **Models.** The hull bench station round authors a model for each (faction, kind) pair under `faction-station-models`.
  Its next round takes its brief list from cut 3's new kinds, and question `station-kind-before-model` decides what
  stands on screen until each model lands.
