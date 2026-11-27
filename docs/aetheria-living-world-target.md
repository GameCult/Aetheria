# Aetheria living world: target, revision 3 (the economy and its actors)

Date: 2026-11-27. Status: target r3, written by Imagination (nibu-imagination, session nibu-self-2026-11-17).
It supersedes r2 (this file at `d61948f6`). Body read at `GameCult/Aetheria` origin/master `80c3555c7`.

Revision 3 maps the seam between the economy and Aetherlight. It also folds in the 2026-11-27 rulings
(all `aetheria-living-world:ruling:` unless named):

- `living-world-supersedes-njordr`
- `installed-gear-decides-what-a-station-buys`
- `demand-consumed-inputs-processed-by-facilities`
- `mass-populations-live-in-blooms`
- `planetary-habitats-are-populations-too`
- `no-market-absorption`
- `cluster-syndicates-are-aetherlight-actors`
- `ethical-sourcing-is-audited-certification`
- `simple-commodities-fungible`
- `compound-commodities-stack-by-lot`
- `no-grade-on-simple-goods`
- `aetheria-release:ruling:no-ftl-comms`
- `aetheria-release:ruling:aetherlight-narrative-state-owner`

It closes follow-ups `target-r3-roles-and-home` and `target-r3-commodity-lots`. Ownership change, expansion, the lore
institutions as damping rules and strategic scoring still wait for a later revision.

## 1. Why

The economy is the engine the strategic layer, the arenas and the demo run on (r2, section 1: `crafting-is-the-game`,
`demo-live-expansion`). Two operator rulings of 2026-11-27 change its shape. First, the actors are not the economy's.
Aetherlight, already Aetheria's in-process owner of who is alive, who holds whom and who knows what, also owns the
factions, the sub-factions, the unions and syndicates, their offices, their commitments and their knowledge. The
economy owns only physical facts: stock, recipes, installed gear, aggregate populations, gates and prices
(`living-world-supersedes-njordr`). Second, no information crosses a gate except in something that makes the trip
(`no-ftl-comms`). So an actor knows only what records carried to it say, exactly as the player does.

The physical side changes too:

- A station buys only what its installed gear uses. Facilities process industrial inputs. Crew and habitation modules
  house the people who consume (`installed-gear-decides-what-a-station-buys`,
  `demand-consumed-inputs-processed-by-facilities`).
- Most of those people live in Blooms, and many in planetary habitats (`mass-populations-live-in-blooms`,
  `planetary-habitats-are-populations-too`).
- A station never absorbs goods it cannot hold (`no-market-absorption`).
- Simple goods are fungible counts with no grade. Compound goods carry their lot (`simple-commodities-fungible`,
  `no-grade-on-simple-goods`, `compound-commodities-stack-by-lot`).
- Origin trust is a certification that audit can expose (`ethical-sourcing-is-audited-certification`).

## 2. Body facts (read at origin/master 80c3555c7)

- **Live economy code is catalog-only.** `RecipeData` (`aetheria.recipedata`, keys 0-9, 9 = `RoleInputs`),
  `OutsideMarketData` and `EconomyChecks` exist (`Assets/Scripts/ServerShared/Recipes.cs`, `EconomyChecks.cs`).
  No stock ledger, grade, allocator, obligation, workforce, population or gate flow exists in the running simulation
  (`docs/aetheria-living-world-map.md`, opening paragraph).
- **Intermediate commodities already take roles.** `RecipeData.RoleInputs` lets an intermediate take a role (iron takes
  `iron-ore`, which hematite or ilmenite fills; ruling `ores-fill-metal-roles`, since `30030af6e`).
  `Recipes.RoleInputsOf` is the one reader of a recipe's role slots. r2's L-recipe and L-roles ("inputs are commodities
  except in gear, hull and consumer-good recipes") are false of live code.
- **Facilities are being specified.** Cut spec `cut-recipe-facilities.r1` (in force, unreported) adds `FacilityData` (an
  inert behaviour on gear), `RecipeData.Facility` (key 10) and `StationKindData.Facilities` (key 3), with check
  `L-facility`. It demotes `RecipeData.StationKind` to a checked mirror, which follow-up `recipe-station-kind-derived`
  deletes. `StationKindData` today has keys 0-2: Name, Description and StandIns (`StationKinds.cs:13`).
- **Factions are catalog records.** `Faction` (`Corporations.cs:14`) carries Personality, Allegiance (key 12), Relations
  (key 16), Doctrine (key 17) and Places (key 21). No runtime faction state exists.
- **Aetherlight is sealed.** `native/aetherlight` (Rust, a fork of Ghostlight `c43eb549`; `PROVENANCE.md`) has no entry
  point yet: "the ABI that creates, opens and drives a world arrives in AL4" (`src/lib.rs` crate docs). Its state
  partitions (`lib.rs`, `world_state!`) are the following:
  - subjects, of kind Person, Institution or Population, each with an optional `ImportKey` (a CultCache schema and
    record key);
  - entities: places, resources, facts and channels;
  - routes, positions and holdings (conserved `Quantity` per subject and resource, through Transfer, Transform, Consume
    and Admit);
  - dependencies, facts, channels, knowledge (subject and fact; confidence; source Witnessed, Told, Seen or Evidenced);
  - authority grants (kind over a subject or a place subtree), offices (`selection`: institution, office and incumbent,
    with delegated authority kinds), redress forums;
  - commitments (Routine, Obligation or Goal; counterparty, due date, period, checks and statement; key = command id
    and index; `DischargeCommitment` removes one);
  - pressures, and the affordance catalog and grants.

  After genesis only the play table writes: an Act, an AdmitPatch or an AdvanceTime. Ids are derived and never drawn.
  The clock counts whole fictional minutes; one AdvanceTime spans 1 to 525,600 of them (`clock.rs`).
- **Only subjects carry import keys.** `EntityDeclaration` (places, resources) and `FactDeclaration` have none
  (`patch.rs:1007,1034`).
- **The game clock.** `SimClock` is the one owner of simulation time: 30 steps a second, `GameStep` = 1/30 s
  (`SimClock.cs:11-24`). `SimulationDice` is the one draw (`SimulationDice.cs:9`).

## 3. The seam (authority map)

**Owner split.** Each decision and fact has one owner.

| Decision or fact | Owner | Notes |
|---|---|---|
| Who the actors are: factions, sub-factions, unions, syndicates, councils and the persons in their offices | Aetherlight (subjects) | Seeded at genesis from the catalog (`Faction` records) and from world generation (cluster unions and syndicates, `cluster-syndicates-are-aetherlight-actors`) |
| Who has jurisdiction over an installation, a facility or a cluster | Aetherlight (authority grants over the installation's place subtree) | The controlling faction of a cluster is authored (`cluster-syndicates-are-aetherlight-actors`) |
| Which office decides what, and who sits in it | Aetherlight (`selection`) | |
| What an actor has promised, to whom, and by when | Aetherlight (commitments) | Including obligations posted to haulers and quotas set on facilities |
| What an actor knows | Aetherlight (knowledge) | Rows arrive only from records that reached the actor's cell (section 3, Knowledge) |
| Whether a certification claim is honest | Nobody decides it; an audit exposes it | The audit is an actor's act that reads records (section 3, Certification) |
| Stock: every count of every good outside an entity's hold, and every money balance not carried by an entity | The economy's stock ledger | One writer, the ledger's commit primitive |
| Recipes, commodities, station kinds, facility and habitation designs | The catalog | Authored; never changed during a run |
| What is installed where, and what each facility is set to run | The economy (installation state) | It changes only by executing an order |
| Aggregate populations: size, composition, needs met, migration | The economy's population step | |
| Prices | The economy, derived each tick from committed stock inside the band the operating actor set | An actor sets a band and a target; it never writes a price |
| Gate flows | The economy's gate step | |
| Time | `SimClock`, through the strategic tick | Aetherlight's clock is advanced by exactly each tick's span and decides nothing |

**What crosses.** The two runtimes share no mutable state. They exchange two documents each strategic tick, and
nothing else crosses:

- **Facts out** (economy to Aetherlight), `F_k`: the records the economy issued at tick `k`, each with its issuer,
  date, subject and confidence, and the cell it landed in. They include the following:
  - market reports: settled prices and trades, and stock levels as reported by the installation;
  - shortage and shortfall reports: a facility idle for want of input, or a population below a need;
  - delivery receipts, and receipts for settled orders;
  - population reports: size, needs met, migration;
  - extraction and certification tallies by operator, which audits read;
  - and every record a hauler carried into the cell by docking.
- **Orders in** (Aetherlight to economy), `O_k`: the typed payloads of the acts Aetherlight admitted at tick `k`. They
  include the following:
  - a facility setting (which recipe, good/fast/cheap, certify output or not);
  - a ration on a population's consumption;
  - an install or removal of gear, which is a build order paid from stock;
  - a price band and stock target;
  - a posted obligation, whose reward is escrowed in the ledger;
  - a haul order, which moves stock between installations;
  - a strike or withdrawal of labour, which the economy executes as workforce unavailability.

  Each order names the commitment it executes.

**The allocator becomes an office.** r2's `L-allocator` put a compiled allocator on the faction record, and that
allocator read needs, stock and prices. In r3 the allocator is the compiled brain of an Aetherlight office: a
faction's quartermaster, a syndicate's foreman, a Bloom council's supply office.

- **What it reads.** Each tick it reads only its incumbent's own Aetherlight snapshot: the knowledge rows that records
  brought, its commitments, its pressures and its authority.
- **What it does.** It proposes acts. Aetherlight admits an act only if the office's delegated authority covers the
  installation or facility the act names.
- **What an admitted act does.** It records the commitment, and its payload becomes an order in `O_k`.
- **What executes the order.** The economy executes it through the ledger's one writer at tick `k+1`. The economy never
  judges authority, and Aetherlight never touches stock.

The four economy kinds (plan, market, project-contract and contraband) remain four compiled brain families
(`allocator-per-economy-kind`, `A-compiled`). The catalog names which family each office runs.

Demotions:

- **The faction record is no longer an economic owner.** It is the authored seed Aetherlight imports at genesis.
- **The allocator is no longer an owner.** It is a brain, and its output is derived into orders only through admitted
  acts.
- **`RecipeData.StationKind` is no longer an owner.** The facility decides (cut `cut-recipe-facilities`).
- **Needs tables are no longer a kind.** A crew or habitation module design carries its consumption table.

**Knowledge arrives by record.** The economy issues every record at an installation. A record lands in that
installation's comms cell, and Aetherlight adds a knowledge row for each subject whose position or channel reaches that
cell. The size of a cell is question `comms-cell`.

Between cells a record travels only as cargo. It is a one-cell chip, by `intangibles-are-cargo`, in a hold that makes
the trip. It lands when that hold docks in another cell. A seized courier's records belong to whoever seized them.
The region gate's outside market is itself a record: the outside prices as the last courier brought them.

No actor, brain or player surface reads the ledger, the population step or another subject's state. This is r2's
`L-no-omniscience`, extended to actors.

**Determinism across two runtimes.** One strategic tick `k`, with a span of whole fictional minutes derived from
`SimClock` steps, runs in this order and no other:

1. The economy step reads its state `S_{k-1}`, the orders `O_{k-1}` and the tick's structured seed. It writes `S_k`
   and `F_k`.
2. Aetherlight admits `F_k` as one patch, advances by the tick's span, then runs each office's brain in subject-id
   order. It writes its journal head `J_k` and the orders `O_k`.
3. The composite digest is `H(digest(S_k), state_digest(J_k))`.

The strategic-tick driver in ServerShared owns this sequence, and neither runtime calls the other. Feedback always
takes one tick, so there is no same-tick fixed point and no ordering race. Both steps are pure (`A-pure`), and both
documents are sorted by id before they cross. The same seed and the same orders give the same `D_k` on Windows and in
the Linux headless arena. On screen and off screen run the same sequence (`L-one-sim`).

**Cross-runtime names.** The economy names an Aetherlight referent only by `ImportKey`, never by a derived Aetherlight
id. Those referents are actors, installations (as places) and records (as facts). Aetherlight names an economy fact
only by its catalog or ledger key, carried in an order payload.

Places and facts need import keys, and acts need a typed payload. Both are Aetherlight changes, in follow-up
`aetherlight-economy-referents`.

**Certification.** The economy carries the "certified" stamp as part of a compound batch's key. It writes the stamp
when the producing facility's setting says to certify, and it never checks whether the claim is true.

The audit is an actor's act. The auditor reads the producer's extraction tallies and its certified output from
records, and finds a mismatch or none; the finding is a fact Aetherlight holds. A producer that buys open-market raw
goods and certifies anyway is exposed by audit, not refused by the ledger (`ethical-sourcing-is-audited-certification`).
Follow-up `sourcing-certification` reads "minted only when the producer's faction oversaw extraction", which is a
gate the ruling does not order. Its next revision should drop the gate.

**Forbidden writers.**

- Aetherlight `holdings` never hold a commodity or a money balance. Its resources are narrative tokens only.
- No brain or Aetherlight path writes a stock count, a price, a grade or an installation's state, except through an
  order the economy executes.
- No economy step creates, retires, positions or informs an actor, or writes a commitment, except by issuing a record
  in `F_k`.
- No station-level demand table. No absorption.
- No Aetherlight clock advance other than the tick's span.

**Shared paths.** Player trade at a dock, an NPC hauler's delivery, an off-screen haul flow, production, consumption and
the gate all change counts through the one ledger commit primitive. The player's dock purchase is not an order: it is a
trade the economy settles directly at the posted price, and its record goes out in `F_k` like any other.

## 4. Invariants

These are the ends. Each label is what Soul measures against. Labels kept from r2 keep their meaning unless restated.

- `L-actors`: Aetherlight owns every actor and its offices, jurisdiction, commitments and knowledge. The economy owns
  stock, recipes, installed gear, aggregate populations, gates and prices. No actor state is stored in the economy and
  no physical count is stored in Aetherlight.
- `L-orders`: An actor's decision reaches the economy only as the payload of an act Aetherlight admitted, and it is
  executed by the stock ledger's one writer at the next tick. The economy never judges authority, and a brain never
  writes economy state.
- `L-tick-order`: Each strategic tick runs the economy step, then Aetherlight, which ingests the facts out, advances
  time and runs the brains. Only the facts-out and orders-in documents cross, sorted by id. The composite digest of
  both states is a pure function of the seed and the orders. Feedback takes one tick.
- `L-no-omniscience`: No player surface and no actor or brain reads the stock ledger, the population step, installation
  state or another subject's state. Each reads only the price band where docked (the player) and records, each with an
  issuer, a date and a confidence.
- `L-records-carried`: A record reaches an actor only by landing in the actor's comms cell. Between cells a record
  travels only as cargo in a hold that makes the trip, and it can be delayed, lost or seized.
- `L-installed`: Every good an installation buys is used by gear installed on it. Facilities process industrial inputs
  by recipe. Crew and habitation modules house populations that consume by the module's table. No installation-level
  demand table exists.
- `L-no-absorb`: A sale moves only what the buyer's bays can hold, and only goods its installed gear uses. The rest stays
  in the seller's hold. Nothing a station pays for vanishes.
- `L-populations`: Consumer demand comes from mass populations living in installed crew and habitation modules. Blooms,
  stations and planetary habitats are one installation kind with different gear, and most people live in Blooms. A
  population's size scales its module tables.
- `L-prices`: Every price is derived by the economy from committed stock and the operating actor's band and target. No
  actor, record or UI writes a price.
- `L-recipe`: Every commodity has exactly one authored recipe. It names its inputs (commodity counts), its role inputs
  (through `RoleInputs`, or the output's own roles for a role recipe), its cycle, workforce, upkeep, the facility design
  that runs it, and its lore hook. No recipe is generated at runtime.
- `L-roles`: A role is filled by any commodity tagged for it. Gear, hull and consumer-good recipes take only roles, and
  any recipe may take a role through `RoleInputs`. `Recipes.RoleInputsOf` is the one reader of a recipe's role slots.
- `L-one-stock`: The stock ledger is the one owner of every count of every good outside an entity's hold, and of every
  money balance not carried by an entity. Trade, hauling, production, consumption, upkeep, destruction and gate flows
  all go through its commits.
- `L-fungible`: Simple stock is a count per (holder, commodity) with no grade, lot or origin. Units of one simple design
  always merge.
- `L-graded`: Grade first appears on compound stock. A production step's output grade is its facility's assembly at
  its setting, capped by the grades of compound inputs (min with tolerance). Simple inputs cap nothing, and only
  production writes a grade.
- `L-lot-at-boundary`: Compound stock is held in batches keyed by holder, design, producing installation and facility,
  grade and certification. A batch becomes a `Lot` with a Produced origin when it crosses into an entity. Simple stock
  crosses as a count with no lot.
- `L-certified`: A certified stamp is written only by a producing facility whose operator's setting certifies output.
  The economy never checks a claim. An audit is an actor's act over records, and a dishonest claim is exposed, not
  refused.
- `L-obligations`: All work posted to the world is a commitment of the actor that posted it. Its reward is escrowed in
  the ledger by its order, and the job board lists only posted obligations, as records.
- `L-allocator`: Each office that decides economy matters runs one compiled brain of one economy kind, named by the
  catalog. A brain reads only its incumbent's snapshot and emits only proposed acts.
- `L-workforce`: A facility's throughput is gated by a workforce of a kind its operator permits. Each workforce kind
  lives in its own module design with its own table. Zhestokost facilities never hold biodrones or AGI.
- `L-zone-crew`: A facility's human crew comes only from populations in its own zone. People cross zones only by the
  population step's slow migration toward work and goods. Biodrones move as cargo.
- `L-sinks`: Goods leave only by consumption (module tables and recipe inputs), destruction (on screen, or the matchup
  model's expected losses), upkeep, and export through the region gate. No stock decays.
- `L-gate-cap`: The region gate trades each commodity at no more than its capped rate per tick, at band prices around
  the outside price, through ledger commits. The cap is below regional demand for every good the region can make.
- `L-source-sink`: Every commodity has a source (an extraction recipe, a recipe output, or the gate selling it) and a
  sink (a recipe input or upkeep, a role fill, a module table, destruction, or the gate buying it). The catalog check
  refuses either one missing.
- `L-consumer-demand`: Every shared component reaches some consumer good, a commodity a crew or habitation module's
  table names. The gate does not count. The window is guidance (`consumer-demand-shared-only`); the role half is live
  and the module half arrives with module tables.
- `L-productive`: The recipe graph, augmented with each module table per worker and with upkeep, has spectral radius
  below 1 at the most input-hungry setting. It is checked on every catalog build, and a failure names the cycle.
- `L-deep`: Depth from extraction is reported per good. Chains are deep where each step is real production
  (`chain-depth-is-justified-not-counted`). It is reported and refuses nothing.
- `L-no-stall`: Over the arena's generated seeds, a region left alone with its gate open at the cap and its offices'
  brains running keeps every recipe in its kind set producing, and no population pinned at its floor. The strategic
  arena runs the check.
- `L-one-sim`: On screen and off screen are one rule set at two levels of detail, sharing the arena's `A-one-sim`,
  `A-pure` and `A-no-clock`. Both runtimes run inside it, and every draw is `SimulationDice` keyed by a structured seed.
- `L-lore`: Every commodity carries a lore hook: what a player learns by inspecting or intercepting it.
- `L-one-kind-list`: The station kinds the simulation runs and the kinds hulls are authored for are one catalog list. A
  kind lists its facilities, and a kind without its faction's model names a stand-in kind, which is deleted when the
  model lands.

## 5. What is not a consumer, and not in scope

Not consumers (nothing keeps them working):

- r2's list: `DemandProfile`, stored commodity categories, `RandomProducts` as station stock, `station-restock`, the
  deleted factory designs, the `INDEX.md` factory-design claim, AetheriaEve.
- Market absorption (`no-market-absorption`).
- Njordr, which is parked (`living-world-supersedes-njordr`).
- r2's needs-table kind, which module designs replace.
- The faction-record allocator field r2 planned, which offices replace.
- Aetherlight `holdings` as a store of commodities or money.

Not in scope:

- Ownership changing hands, expansion, snowball-damping institutions and strategic scoring. They come in the next
  revision, under these owners.
- Aetherlight's ABI (AL4) and its narrative scope, which belong to campaign `aetheria-release`. This campaign consumes
  them and owns only follow-up `aetherlight-economy-referents`.
- Lore text for Aethernet and couriers, which belongs to `aetheria-release` cut `cut-lore-no-ftl-comms` (spec `cut-cut-lore-no-ftl-comms.r1`).
- Planetary-habitat placement, which waits on Asura bodies. The installation kind is shaped for it now.
- Population subjects as Aetherlight actors (idea `seam-map.population-subjects`).
- Matchup-model fitting (`aetheria-matchups`), named components and disassembly, alternative recipes, an omniscient
  economy panel, runtime model calls, station hull art, and passenger transport. All are as in r2.

## 6. Identity, lifecycle, authority (Eureka step 0b)

| Kind | What names it | What happens to it over time | Who decides (owner; forbidden writers) |
|---|---|---|---|
| Actor | An Aetherlight subject (Institution or Person). It carries an `ImportKey`: `aetheria.faction` and the record key for catalog factions; `aetheria.actor` and a seed-derived site label (`{seed}:{cluster}:union:{n}`) for generated unions and syndicates | Created at genesis or by a later patch. It acts, holds offices, and is retired, never deleted | Owner: Aetherlight. Forbidden: Faction or other economy records holding runtime actor state |
| Office | (institution, office name) in Aetherlight `selection`. Its brain family is named by the catalog per office kind | Seeded at genesis. Its incumbent changes by Aetherlight acts | Owner: Aetherlight. The brain is compiled code (`A-compiled`). Forbidden: a brain chosen at runtime |
| Commitment | Aetherlight `CommitmentKey` (command id, index) under its promisor | Admitted by an act, then discharged, or it falls past due and raises pressure. Never edited | Owner: Aetherlight. Forbidden: the economy writing or discharging one |
| Order | The admitted act's event id, plus the payload's index. It is typed: setting, ration, install, band, obligation, haul, labour | Born in `O_k`. Executed once at `k+1`: it commits, or it fails with a receipt record. Never re-executed | Owner: the economy executes it; Aetherlight authorized it. Forbidden: an order with no admitted act |
| Installation | A catalog-independent runtime record keyed by its zone body site (`zone-body-site-identity`) or its station key. In Aetherlight it is a place with `ImportKey` `aetheria.installation` and that key | Generated with its zone, and gains or loses gear by install orders. Destroyed with its body or hull | Owner: the economy. Its jurisdiction is Aetherlight's. Forbidden: two kinds for station, Bloom and habitat |
| Installed gear | (installation, slot ordinal). It holds a gear design: a facility, a crew or habitation module, or other gear | Installed or removed only by an order. A facility runs its setting; a module houses a population | Owner: the economy, through orders. Forbidden: a generator or UI installing gear outside the build order path (world generation seeds it once) |
| Facility setting | (installation, slot) | Changed only by a setting order. Read by the production step | Owner: the operator actor, through orders. Forbidden: any other writer |
| Population | (installation, module slot, workforce or population kind). It holds a size and its needs met | Grows while basic needs are met. It shrinks slowly to a floor under shortfall, and migrates slowly between zones | Owner: the population step. Forbidden: any other writer of size or zone |
| Simple stockpile | (holder, commodity) and a count | Changed only by ledger commits | Owner: the stock ledger. Forbidden: grade, lot or origin on it |
| Compound batch | (holder, design, producing installation, facility, grade, certified) and a count | Created by production. It merges with an equal key, and splits by moves | Owner: the stock ledger, and production for grade and stamp |
| Lot | Minted at the boundary from (batch key, crossing event) | It stacks in a hold by (design, lot), and carries a Produced origin | Owner: `ItemManager` at the boundary. Forbidden: minting for stock no entity holds |
| Account | (holder actor `ImportKey`, cell); see question `money-across-cells` | Changed only by ledger commits: settlements, escrow, wages | Owner: the stock ledger. Forbidden: Aetherlight holdings, and any UI balance edit |
| Record | (issuer installation, tick, ordinal). It holds a subject, a payload, a confidence and the cell it landed in. In Aetherlight it is a fact with `ImportKey` `aetheria.record` and the record id | Issued once and never edited. It travels as cargo between cells | Owner: the economy issues it; Aetherlight owns who knows it. Forbidden: a record with no issuer, and any reader of the payload without a knowledge row |
| Knowledge row | Aetherlight (subject, fact) | Added when a record lands in the subject's cell. Forgetting removes it | Owner: Aetherlight. Forbidden: any economy-side "who knows" store |
| Comms cell | A zone or a region; question `comms-cell` | Fixed by the galaxy | Owner: galaxy generation. Forbidden: a record crossing a cell boundary other than as cargo |
| Certification | A stamp in a compound batch key and on its lots. An audit finding is an Aetherlight fact | The stamp is written by production under a certify setting. A finding is written by an auditor's act | Owner: the operator decides the claim; the auditor decides the finding. Forbidden: the ledger refusing a claim |
| Tick exchange | (`F_k`, `O_k`) for tick `k` | Built once per tick, sorted by id. Consumed once | Owner: the strategic-tick driver. Forbidden: any other channel between the runtimes |
| Commodity, Recipe, Station kind, Facility design, Module design, Workforce kind, Outside market | Catalog keys, as in r2. A module design carries its consumption table per occupant | Authored, and revised by commit | Owner: the catalog, written by economy batches. Forbidden: runtime edits |
| Price band | (installation, commodity) | The band and target come from a band order; the price is derived each tick | Owner: the economy for the price, the operator actor for the band. Forbidden: a price from catalog price alone once stock exists |

## 7. Cut sequence

Landed: catalog subtraction, the station-kind datum, recipe schema and checks, recipe role inputs, the ore batches,
derived prices, commodity shapes, and simple fungibility. In force and unreported: `cut-compound-commodity-stacks`,
`cut-design-bills-of-materials` and `cut-recipe-facilities`. The rest are follow-ups in the mind, and each names what
it waits on.

1. Facility production (follow-up `facility-production`, named by `cut-recipe-facilities`). It waits on that cut and
   on the stock ledger.
2. The stock ledger: simple stockpiles, compound batches and lots at the boundary. It is r2's cut 4, re-scoped by
   `L-fungible`, `L-lot-at-boundary` and `L-no-absorb`.
3. Installations and habitation (follow-up `installations-and-habitation`): one installation kind; crew and habitation
   module designs with tables; populations in modules; the module half of `L-consumer-demand`. It replaces r2's cuts 6
   and 7 as kinds. It waits on 2.
4. Station outflow, mapped now by another Imagination against r2. It re-anchors on r3 (section 8).
5. Aetherlight economy referents (follow-up `aetherlight-economy-referents`): import keys on places and facts, and a
   typed order payload on acts. It waits on AL4 (`aetheria-release`).
6. The tick exchange (follow-up `seam-tick-exchange`): the strategic-tick driver, `F_k` and `O_k`, records landing in
   cells, the composite digest, and the office brains replacing r2's allocators. It waits on 2, 3 and 5, and on
   question `comms-cell`.
7. Accounts: money in the ledger, escrow and wages. It waits on question `money-across-cells` and on 2.
8. Off-screen tick, legibility and stall checks: r2's cuts 9 to 11, unchanged in purpose, run inside 6's sequence.

## 8. What changes for the station-outflow mapping

The station-outflow pass was mapped against r2 and `no-market-absorption`. Under r3:

- **Consumption at a station is not a station rate.** It is the consumption of the populations housed in its installed
  crew modules, by each module's table (`L-installed`, `L-populations`). A "local consumption at its demand rate" with
  no module behind it is forbidden.
- **Industrial drain is facility production.** Ore drains only through an installed facility running a recipe. A
  station with no such facility does not buy ore (`L-no-absorb`). Where the outflow work needs this, it waits on
  facility production, or names that follow-up as its first step.
- **Onward trade is an actor's haul order, not an economy flow.** Until offices run (follow-up `seam-tick-exchange`),
  an outflow cut may feed the ledger orders from a fixed order document (a test fixture or a generator-seeded standing
  order). That document has the r3 order shape, and it is deleted when offices issue orders. It must never be a brain
  or a second issuer that lives on.
- **Starting stock that leaves room** is generation, and it is still the economy's. It is written through ledger
  commits.
- **Prices respond through `L-prices`.** A price is derived from stock inside a band. Until band orders exist, the
  band is the catalog default.

## 9. Prior art

These notes come from the designers' published systems, recalled for this pass rather than fetched in it (the mind's
`known` index did not answer during this pass). An Eyes pass should pin sources before the questions are ruled.

- **Victoria 3** separates pops from interest groups. Pops are economic aggregates in buildings: employment, wages,
  standard of living and needs met. Interest groups are the political actors, and their clout is derived from the pops
  that back them. Buildings run production methods that their owners choose. The lesson is the seam this revision
  takes: aggregates on the physical side, actors whose pressure reads them. The known failure is opaque pop-to-IG
  attribution, which players could not read; records with issuers answer it here.
- **X4: Foundations** builds stations from modules. Production modules consume wares. Habitat modules house a workforce
  that consumes food and medical supplies and speeds production. A station manager (an actor) sets buy and sell offers
  and budgets inside the station's physical limits. Its known failures are seed-dependent shortages and stations that
  stop buying when storage fills. `L-no-absorb` and `L-no-stall` answer them.
- **Dwarf Fortress** keeps civilisations and their sub-entities (guilds, religions, governments) as historical entities
  that hold positions with holders over sites. World generation seeds them, and their knowledge spreads with travellers.
  That is the shape of offices over installations and of records carried by traffic.
- **Bills of exchange**: medieval merchant banking moved value between cities by carried instruments drawn on
  correspondents, because no message travelled faster than a courier. It is prior art for `money-across-cells`.
