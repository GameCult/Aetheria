# Item Provenance: Substrate Map

Date: 2026-09-17

Status: Imagination pass, substrate map and forks. Not a cut map. The ends are in
`docs/item-provenance-target.md`. This document maps the Body those rulings land on
and names the forks they leave open. Branch `codex/cultcache-cutover` at `dbe1dd83`.

Rulings folded in after the target was written (operator, 2026-09-17, relayed):

- **Terminus generates backwards.** Pick the output product first (loot, NPC gear,
  station stock, starting ship, presets), then synthesize a plausible faction,
  station, factory instance and input lots that would have produced it. The
  forward supply chain is the future Njordr direction, not Terminus scope. "Start
  with the output products, and generate a fake economy to materialize those goods."
- **The recursion goes all the way down to mining.** Full chains to extraction, not
  bounded depth. Consumer goods are included, not just gear.
- **Routes move consumer goods between places**, meant to create piracy gameplay.
  Njordr's interspersed profile (shipment materialization, interception) is the
  shape precedent, adopted as local typed state with no daemon.
- **Scope split.** Backward generation, routes and piracy are the next scope,
  after Terminus ships. The **schema cut lands now**: "I want the schema cut now so
  that the shape of the data reflects the shape of the game we want to build." §11
  separates the two. Forks F2, F3, F5 and F7-F10 below belong to the future
  generation scope and are kept here only as needs the schema must carry.

Evidence method: source reads (file:line below) and one headless probe, a scratch
console project over `Aetheria.Shared` at CultLib `a0813c6` that opened the real
catalog, built a 196-zone sector galaxy from the authored `Settings.asset`, and ran
`ZoneGenerator.GenerateZone` on every zone. Also `AetherDb census` and `factions`.

## 1. Items today

- `ItemData` (`ItemData.cs:272`): Name, Description, `Manufacturer`
  (`CultRecordRef<Faction>`, key 3, `ItemData.cs:280-281`), Mass, Shape, SpecificHeat,
  Conductivity, `Price` (`:297`). The probe counts 37 of 115 designs with
  `Manufacturer` set.
- `CraftedItemData.Roles` (`ItemData.cs:313-318`): a list of `ItemRole` holding only a
  name (`:324-328`). **No design authors a role**: the probe found 0, and `census`
  prints "0 products carry role quality".
- `SimpleCommodityData` (`:300-311`): MaxStack and a `SimpleCommodityCategory`
  (Minerals, Metals, Alloys, Compounds, Organics, Ammo, Consumer; `Enums.cs:147-156`).
  Keys 6, 7, 8 and 11 belonged to the removed resource-distribution fields (`:303`).
  13 records: New Metals, Rock, Uranium, Methane, Algae, Ammo, Gold, Water, Bacteria,
  Hydrogen, Fungi, Animal Products, Potassium.
- `CompoundCommodityData` (`:330-337`): `DemandProfile`
  (`Dictionary<PersonalityAttribute, float>`) and a `CompoundCommodityCategory`
  (Wearables, Consumables, Luxuries, Tools, Manufacturing, Assemblies;
  `Enums.cs:158-166`). 51 records, a real consumer-goods list (Hat, Trousers, Super
  Tough Toffee, SpaceCast, Neural Lace) plus intermediates (Sheet Metal, Circuit
  Board, Support Strut). **Nothing reads `DemandProfile` or `Faction.Personality`.**
  No product sells a compound commodity, and nothing mints one.
- Instances (`ItemInstance.cs`): `ItemInstance` holds `Data` and `Rotation` (`:24-28`).
  `CraftedItemInstance` holds `Quality` (key 2, `:37`), `Ingredients`
  (`List<RoleFill>`, key 9, `:40`), `Product` (`CultRecordRef<FactionProductData>`,
  key 10, `:43`) and `QualityForRole` (`:47-53`). `SimpleCommodity` holds `Quantity`
  (`:67-71`) and has no quality or provenance. `EquippableItem` holds `Durability` and
  `OverrideShutdown` (`:73-78`).
- `FactionProductData` (`FactionProduct.cs:14-31`) is a catalog document: Name,
  Description, `Design`, `Manufacturer`, and `Roles` (`ProductRole` mean/SD, `:37-48`).
  `census`: 37 products over 51 designs. 14 designs have no product and cannot
  spawn, including **Refinery, Assembly Line, Shipyard, Surface Ore Extractor, Deep
  Ore Extractor**.
- Those factory and extractor designs are `GearData` with `HardpointType.Tool` whose
  `Behaviors` lists deserialize to `null` (the probe prints `behaviors=[null]`). They
  are legacy husks: the behavior types they named are gone (`git log -S FactoryData`
  finds "Factory Changes" `8986575c` and "Equipment Data Simplification"
  `47189c8a`). The commented `HaulingControllerData` union (`Behaviors.cs:179`) is
  the same kind of residue.
- **No recipe, blueprint or ingredient model exists.** "Factory", "fabricat",
  "assembler", "blueprint", "recipe" and "refiner" appear in no live C# outside
  `NIH/MIConvexHull`.

`ItemData.Manufacturer` readers: `PropertiesPanel.cs:371-372` (shows "Manufacturer",
falling back to "GameCult"), `tools/AetherDb/Program.cs:54` (census), and the
`AetheriaStoresTests.cs:109` fixture. Generation ignores it; generation reads
`FactionProductData.Manufacturer` (`LoadoutGenerator.cs:131,144,156`).

## 2. Mint sites

All crafted instances come from three `ItemManager` overloads:

- `CreateInstance(CraftedItemData, float quality)` (`ItemManager.cs:126-142`) sets
  Quality and Durability, with no product and no ingredients.
- `CreateInstance(CraftedItemData)` (`:144-160`) rolls a rarity tier with the
  **time-seeded** `ItemManager.Random` (`:20`) and stores the tier's *discrete*
  Quality.
- `CreateInstance(FactionProductData)` (`:164-187`) calls the previous overload, sets
  `Product`, and draws a gaussian `RoleFill` per design role. Since no role is
  authored, that loop never runs.

| Site | Overload | What is knowable at the site |
|---|---|---|
| `LoadoutGenerator.GenerateShipLoadout/Turret/Station` (`LoadoutGenerator.cs:35-110`), with hardpoints `:189-230` and interior `:233-260` | product | galaxy, zone, zone faction, the chosen product and its maker; **not** the owning station |
| Station stock: 16 random products into the station's cargo (`:97-105`) | product | the station being built (the entity in hand), its faction and zone |
| `CopyBuild` on matching hardpoints (`:263-269`) | copies Quality/Ingredients | "a second unit off the same line", already lot semantics |
| `ZoneGenerator.GenerateZone`: stations `:275,296`, turrets `:254`, ships `:309` | via generator | the zone seed (`:47`), `nearestFaction` (`:204`), `factionPresence` (`:206`), story station faction |
| Starting ship (`ActionGameManager.cs:734-739`) and console `spawnturret` (`:526-540`) | generator, then `Unpack(..., instantiate: true)` | see the scar below |
| `Loadouts.Materialize` (`Loadout.cs:95-150`) | product (first available by key) | zone, availability predicate; no game caller |
| Console `give` (`ActionGameManager.cs:492-500`) | design, quality .95 | player ship only |
| `TradeMenuDebug.Buy` (`TradeMenuDebug.cs:354-420`) | design | referenced by no scene or prefab: dead |
| Loot (`EntityInstance.cs:413-430`) | none; drops existing instances by value | the killed entity, its faction |
| Mining (`Zone.cs:285-305`) | none; the `SimpleCommodity` mint is commented out | belt, asteroid, miner |

**Scar: `instantiate: true` strips provenance today.** `EntitySerializer.Unpack` with
`instantiate` calls `ItemManager.Instantiate` on the hull, every equipped item and all
cargo (`EntitySerializer.cs:75,85,103-121`). `Instantiate` (`ItemManager.cs:108-124`)
re-mints through `CreateInstance(CraftedItemData)`. That drops `Product` and
`Ingredients`, re-rolls a tier quality, and resets durability. So **the player's
starting ship and console turrets carry no brand**, and their generated products are
thrown away. Only zone-generated NPCs and stations keep products.

Ship spawning happens only at zone generation: `enemyCount = rand*factionPresence*2 +
stationCount` (`ZoneGenerator.cs:306-311`). On load, every non-player ship gets a
`Minion` running `PatrolOrbitsTask` over four random orbits (`Zone.cs:83-106`). No
agent crosses wormholes. `HaulingTask` (`Agents/Tasks/HaulingTask.cs:12-27`, live
`Entity` fields, no MessagePack attributes) and `Mining` (`Mining.cs:14-20`) are
unexecuted stubs.

## 3. Stations and factories

- A station is an `OrbitalEntityPack` value inside `ZonePack.Entities`
  (`ZoneData.cs:27`), built by `GenerateStationLoadout`. Its fields: faction
  (`EntityPack.Faction`, key 15, `EntitySerializer.cs:195`), orbit ref, security, and
  `Story` index (`:171`). Its faction is `story.Faction` or the zone's
  `nearestFaction` (`ZoneGenerator.cs:275,296`).
- **Stations have no identity.** A station is addressed by
  (`savedzone-{i}`, index in `Entities`) and nothing else. `SavedGame.CurrentZoneEntity`
  is such an index (`SavedGame.cs:34,73`). Docking sets
  `TradeMenu.Inventory = entity.CargoBays.First()` (`ActionGameManager.cs:827-829`).
- **Zones generate lazily on first entry** (`ActionGameManager.cs:643-652`,
  `PackedContents ??=`) from a deterministic zone seed
  (`ZoneGenerator.cs:47`: name hash xor position hash). Item *selection* follows that
  seed, but item *quality* comes from the time-seeded `ItemManager.Random`. A zone is
  never regenerated within a run. New Game clears the run store (`MainMenu.cs:111`).
- Each station carries hull, hardpoint gear, one docking bay, cargo bay, capacitor and
  stock, all embedded by value. No station equips a factory; none can, because factory
  designs have no product (§1).
- A faction (`Corporations.cs:13-60`) has no production stats. Its fields are
  Personality, Allegiance, InfluenceDistance, BossHull, colors and music. Home zones
  live on `Galaxy.HomeZones` and `SavedGame.HomeZones`.
- The zone graph is `GalaxyZone.AdjacentZones`, a per-zone `Distance` map
  (`Galaxy.cs:554-567`), and `Galaxy.FindPath(source, target)` (`Galaxy.cs:524`).
  That is all route pathing needs. **No routes, convoys, freighters, traders,
  shipments or piracy exist.**

## 4. Persistence

- Catalog (`AetheriaStores.cs:9`, read-only at runtime): `ItemData`, `Faction`,
  `FactionProductData`, `PersonalityAttribute`, `NameFile`, `InputLayout`, `Loadout`.
- Run (`:10`): `OrbitData`, `BodyData`, `SavedZone`, `SavedGame`. Written only by
  `RunSave.Commit`, which does one commit, keys zones `savedzone-{i}` and removes stale
  zones (`SavedGame.cs:96-111`). `RunSave.Clear` removes every run-typed record
  (`:114-124`). The single-file store writes its whole view on commit.
- Player (`:11`): `PlayerSettings`.
- **Every item instance is a value** embedded in `EntityPack` (hull, equipment, bays,
  cargo contents; `EntitySerializer.cs:179-196`), inside `SavedZone.Contents`. No
  instance has a key. Buying moves the value between cargo bays
  (`TradeMenu.cs:383`). Floor loot is a Unity `ItemPickup` (`ZoneRenderer.cs:546-561`)
  that `PackZone` never captures (`Zone.cs:108-119`), so dropped loot vanishes on save
  or zone exit. Pickup stores the value (`ShieldManager.cs:29`).
- **Probe sizing** (one galaxy, 196 zones, all generated): 1158 entities (196 stations,
  570 ships), **15,708 crafted instances, 80 per zone on average, 433 at most**, and
  3.46 MB of packed zone contents (about 17.6 KB per zone). A second run gave 16,445
  instances. A player sees only the zones they enter, but a finished run touches many.
- Implication: one lot record per instance, each with a full chain to extraction,
  multiplies that count by the chain's fan-out unless chains **share nodes**. Sharing
  is Njordr's own shape (a run consumes input lots and produces a lot of quantity N),
  and it is the only way this size stays bounded. Stations need a few production
  batches per product, not a chain per unit.

## 5. Quality consumers

Every stat and wear reader goes through the instance:

- `ItemManager.Evaluate` (`ItemManager.cs:70-84`): `QualityForRole(stat.FromRole)`,
  plus `item.Quality` for the durability exponent.
- `EquippedItem` constructor (`Entity.cs:1212-1225`): `item.Quality` for the thermal
  and durability exponents. `EquippedItem.Evaluate` (`:1257-1274`):
  `QualityForRole`. Wear (`:1288-1292`): `item.Quality`.
- `ConsumableItemEffect.Evaluate` (`Entity.cs:1081-1090`): `QualityForRole`.
- `GetPrice` (`ItemManager.cs:86-90`) reads Quality. Its readers are
  `TradeMenu.cs:218,370` and `EntityPack.Price`. Hull purchase charges `data.Price`,
  not `GetPrice` (`TradeMenu.cs:380`).
- `GetTier` (`ItemManager.cs:189-197`) sets the display color and "+" count in
  `PropertiesPanel.cs:424`, `TradeMenu.cs:193` and `ZoneRenderer.cs:567`.
- **No UI shows the product name or flavour text.** `Product` is read only by tests
  (`LoadoutTests.cs:303`). The only brand-like display is the design's `Manufacturer`
  at `PropertiesPanel.cs:371`. The target says flavour text is "most of the lore this
  release delivers" (`three-gates-scope.md`), yet the player never sees it.

## 6. Raw materials and extraction

- `BodyData.Resources` (`ZoneData.cs:53-54`) has **no writer**: `ZoneGenerator` never
  fills it. Mining reads it (`Zone.cs:293-295`), and its yield mint is commented out
  (`:298-304`). So mining yields nothing, and no belt has resources.
- `MiningToolData` (`Behaviors/MiningTool.cs:14-35`) is a live behavior. The extractor
  designs are behaviorless husks (§1). Njordr's `Extracted { source, at }`
  (`njordr-engine/src/lib.rs:98`) maps to an extraction at a (zone, belt or body,
  commodity) source. Nothing names one today. Belts are `BodyData` run records with
  keys minted at zone generation, so a source is as lazy as its zone.

## 7. Conflicts

1. **Scalar quality versus Njordr.** Njordr: "There is no scalar quality on the
   product"; the deletion line names `CraftedItemInstance.Quality`. The rulings keep
   mocked quality and derive it from provenance. `three-gates-scope.md` already
   accepted a one-dimension quality as interim. The conflict is live but ruled.
   Recommend naming it the lot's one-dimension property vector, so the future
   migration widens a field instead of moving one.
2. **Infinite regress under "all the way down to mining".** A factory is itself a
   produced lot (Njordr: "A facility is a lot like any other"). Its process role needs
   a factory. An extractor is a product whose own chain needs an extractor. Backward
   synthesis cannot bottom out at extraction alone. It needs a terminal origin for
   facilities. Njordr's engine has none (`Extracted | Produced | Seized`,
   `lib.rs:97-101`); its lots start from seeded catalogs. See F2.
3. **Conservation and holders.** A Njordr lot has quantity, place and `Holder`
   (`lib.rs:104-120`), and moves only by command. Aetheria's holder of record is
   whichever cargo bay embeds the instance value. Synthesized input lots never exist
   as instances. Terminus cannot conserve quantity without simulating, and the ruling
   says not to simulate. Recommend the lot record carry provenance and properties
   only, with holder and quantity left out, stated as a deliberate divergence.
4. **Seizure.** Njordr re-mints seized lots with `Seized { shipment, by }`. Under
   "branding is derived from provenance", a seized lot that loses its producer loses
   its brand. Recommend custody change only (F8).
5. **Scope.** `three-gates-scope.md` lists "Economy simulation, hauling, mining
   yield, crafting/blueprints" as out of scope, and Terminus's core loop is
   fight -> loot -> sell/refit -> gate. Routes, freighters and piracy are hauling and
   an economy facade. Chains to mining need mining sources and authored recipes. This
   is a **scope change the operator should make knowingly**, not a detail of the item
   model. Sell and Repair (gate item 3) still do not exist. Freighter agents crossing
   zones depend on the navigation planner (gate item 5), which does not exist either.
6. **Existing scar.** `instantiate: true` already discards products (§2), so the
   starting ship contradicts today's product model before provenance lands.

## 8. Identity, lifecycle, authority

"F" marks a fork (§9). "D" marks a recorded default.

| Kind | Named by | Mint | Transfer / loot / sell | Consume | Zone unload / save | New Game / death | Decides |
|---|---|---|---|---|---|---|---|
| **Lot** (provenance node) | run-scoped `LotId` (F1) | synthesis, at the mint site's call (F3) | never moves; instances point at it (D) | synthesized inputs are consumed at synthesis and have no instance (D) | kept while reachable from any instance, route or station; GC on `RunSave.Commit` (D, from the orphan-SavedZone scar) | cleared with the run store (D) | the synthesizer, one function in `ServerShared` |
| **Lot properties** (mocked quality per role) | on the lot (F4) | rolled once at synthesis from factory quality and input qualities, seeded by lot id (F4) | immutable | n/a | with the lot | with the lot | synthesizer; stat readers only read |
| **Item instance** | none; a value in its container (D) | mint sites in §2 now call synthesis and store `LotId` plus per-unit Durability (D) | moves by value: buy, pickup, loot (D) | destroyed with its container | inside `SavedZone` as today | run store cleared | container; `Entity` owns live state |
| **Station identity** | `StationId` in a run-scoped economic skeleton (F3) | galaxy generation (F3) or zone generation | n/a | destroyed? F9 | skeleton row persists; the entity materializes in its zone on entry | cleared | galaxy generator owns the roster; `ZoneGenerator` materializes it |
| **Factory instance** | a facility lot, one per station production line, stored as an equipped or stored instance in the station's pack (ruling) | with its station (F3); origin `Founded` (F2) | stays in the station; if looted, the lot keeps its provenance (D) | n/a | with the station | cleared | skeleton |
| **Station production quality** | derived from the station's facility lots | n/a | n/a | n/a | never stored (D) | n/a | projection |
| **Input references** | `LotId`s on `Produced { run inputs }` | at synthesis, reusing an existing batch for (station, design) before minting a new one (D, sizing §4) | n/a | n/a | ledger | cleared | synthesizer |
| **Raw origin / source** | `SourceId` = skeleton (zone, commodity) row; binding to a belt body is F10 | skeleton | n/a | quantity not tracked (conflict 3) | skeleton | cleared | skeleton |
| **Branding / segment** | derived: (provenance faction, design, station production quality) -> `FactionProductData` entry (F6) | never stored (ruling) | n/a | n/a | n/a | n/a | projection in `ItemManager` |
| **Recipe / role input spec** | catalog (F5) | authored in Studio | n/a | n/a | n/a | persists | catalog author |
| **Route** | skeleton row: origin station, destination station, goods (F7) | galaxy generation from `DemandProfile` x faction `Personality` (F7) | n/a | n/a | skeleton | cleared | skeleton |
| **Shipment** | `ShipmentId` (F7) | materialized on zone entry for routes crossing the zone (F7) | seized by kill and loot (F8) | delivered? F7 | a seized shipment is marked lost so it never re-materializes (D) | cleared | route materializer; combat outcome decides seizure |
| **Designer manufacturer** (`ItemData.Manufacturer`) | removed, key 3 retired (ruling) | n/a | n/a | n/a | n/a | n/a | none |
| **`CraftedItemInstance.Product`, `Ingredients`, `Quality`** | removed from the instance, moved to the lot (F4) | n/a | n/a | n/a | n/a | n/a | none |

## 9. Forks, ranked by what they block

**Ruled (operator, 2026-09-17):** F0 is (a); see the target. F2 is none of the
options below: facilities are built in place from raw materials, with a
retooling cost per design switch; see the target.

**F1. Lot storage: records versus embedded by value.** Blocks everything.
- (a) Provenance embedded by value on each instance: a full DAG per unit. With
  full depth, shared facilities and 16k instances per galaxy, every shared node is
  duplicated per unit, and the factory chain rides in every item.
- (b) One run-store record per lot: shared nodes, and each lot gets its own key.
- (c) One run-store ledger document (`[CultGlobal]`) holding `LotId -> Lot`, with
  instances carrying a `LotId`.
- **Recommend (c).** Sharing is mandatory at this depth. The single-file store
  rewrites its whole view either way. One document commits atomically with
  `SavedZone`s in `RunSave.Commit` and gives GC one place to sweep. Per-lot records
  (b) earn their keep only when a store pages records, which this one does not.
- Depends on it: instance shape, save commit, GC, every mint site, the Studio view.

**F2. Terminal origin for facilities.** Blocks synthesis termination.
- (a) A `Founded { station }` origin: facility lots are seeded with no inputs, and
  mining recursion applies to material inputs only.
- (b) Facilities recurse one generation (built by a founded facility elsewhere).
- (c) Recurse facilities to extraction with a depth cap.
- **Recommend (a).** "Down to mining" then holds for every material an item is made
  of. The factory is the station's equipment, and its stats are known because the
  lot's quality is known. Ask the operator: the ruling's words cover this case only
  by reading.

**F3. Economic skeleton: do chains name real stations and sources, and when are they
synthesized?** Blocks station identity, routes, stock and loot provenance.
- (a) Synthesize at item mint and allow references to unmaterialized, invented
  stations.
- (b) Build a run-scoped skeleton at galaxy generation: per zone, stations (id,
  faction, facility lots), sources (commodity), routes. Derive it deterministically
  from the zone seed and persist it in the run store. `ZoneGenerator` materializes
  skeleton stations on entry instead of choosing its own count and faction
  (`ZoneGenerator.cs:204-209,272-304`). Product chains are synthesized at mint
  against the skeleton.
- (c) Skeleton per zone, built on zone entry: cheap, but a route's origin zone may not
  be generated yet.
- **Recommend (b).** Routes and piracy make "real station" nearly mandatory: a
  pirated freighter's cargo should come from a station the player can fly to. The
  skeleton is about 200 stations, small next to 16k instances. It demotes
  `ZoneGenerator`'s station count and faction choice to reading the roster. It also
  moves item quality from the time-seeded `ItemManager.Random` to seeded synthesis,
  which makes the chain regenerable.
- Sub-default: stations keep no identity outside a run. Zones never regenerate within
  one.

**F4. Quality: per lot or per unit, stored or derived.** Blocks stat readers and the
instance shape.
- (a) Per lot, per role, rolled once at synthesis and stored on the lot.
- (b) Per lot, recomputed from provenance on each read, seeded by lot id.
- (c) Per unit on the instance, as today.
- **Recommend (a)**, with `Quality`, `Ingredients` and `Product` leaving the instance.
  Stat readers resolve lot -> properties through `ItemManager`, which caches them in
  `EquippedItem`. Storing the roll is not a second truth: it is the lot's property
  vector, which Njordr derives once and never mutates. `CopyBuild`
  (`LoadoutGenerator.cs:263`) becomes "same lot". Workmanship per unit disappears.
  Durability stays per unit.
- Depends on it: F6 bands and `GetTier`/`GetPrice`.

**F5. Recipes: what inputs does a design need?** Blocks chain depth past one level.
Today nothing exists: 0 roles authored, and designs reference no inputs.
- (a) Author on `ItemRole` an input spec: a commodity category or a design list.
- (b) One default recipe table by category, for example gear <- Assemblies and
  Manufacturing compounds <- Metals, Minerals and Compounds simples <- extraction,
  with role authoring optional.
- (c) `RecipeData` catalog records per design (the Njordr shape).
- **Recommend (b), overridable by (a).** It gets 51 gear designs and 51 compound
  commodities chained without authoring 100 recipes first, and it stays role-shaped,
  so no ingredient is named. (c) is the Njordr migration's job.

**F6. Branding and segment; the fate of `FactionProductData`.** Blocks UI and
generation selection.
- (a) `FactionProductData` becomes the faction's segment catalog: (faction, design,
  name, flavour, minimum production quality). `Roles` mean/SD is cut; `census` shows
  no product authors it. Brand = the maker's entry for the design with the highest
  band at or below the station's production quality.
- (b) Keep mean/SD as the station's quality control applied at synthesis.
- **Recommend (a).** The station's facility lots now carry what mean and SD modelled.
  Generation selection changes from "pick a product" (`LoadoutGenerator.cs:124-148`,
  `Loadout.cs:99-114`) to "pick (design, maker)". `IsAvailable` is re-keyed to
  (faction, design) (default). A design with no entry for the maker shows the bare
  design name (default).
- Consequence (default): Studio groups `FactionProductData` by `Manufacturer`, not
  `GearData` (target "Consequences").

**F7. Routes and shipments without simulation.** Blocks piracy only; separable.
- (a) Skeleton routes (origin station, destination station, goods class) chosen by
  destination faction `Personality` against compound `DemandProfile`, the one unread
  hook that already exists. On zone entry, spawn freighters for routes whose
  `FindPath` crosses the zone, with cargo synthesized from the origin station's
  batches. There is no run clock and no abstract progression. A destroyed freighter
  marks its shipment lost.
- (b) Njordr-style derived positions over a run clock. There is no run clock today;
  `ZonePack.Time` is per zone.
- (c) Defer routes. Ship provenance first.
- **Recommend (a) as a later slice, after (c) for the first cut.** It needs a freighter
  agent (`HaulingTask` is a stub), and it conflicts with scope (conflict 5).

**F8. What piracy seizes.** Recommend custody change only: looted instances keep
their lot. Provenance is permanent, and the brand still derives. The shipment is marked
lost. A Njordr `Seized` re-mint waits for Njordr.

**F9. Can a station be destroyed?** If so, what becomes of its facility lots and
routes? Today stations are ordinary entities. Recommend: its facility lots persist
while any instance references them, and routes from it stop materializing. Real, but
low-blocking.

**F10. Sources bound to belts.** Default: a skeleton source is (zone, commodity) and
does not bind to a `BodyData` belt. Filling `BodyData.Resources` from the skeleton so
mining works is a separate slice (mining yield is out of scope).

Defaults recorded, not asked:

- `ItemData.Manufacturer` is deleted, with key 3 retired. Its readers switch:
  `PropertiesPanel.cs:371` to the derived brand, census to products, the stores test
  to `FactionProductData.Manufacturer`.
- The `instantiate: true` re-mint path is deleted. Generated packs are already fresh.
  The starting ship and `spawnturret` keep their lots.
- `TradeMenuDebug.cs` is deleted as dead.
- Console `give` mints through synthesis at the docked station, or at a `Founded`
  debug origin when undocked.
- Simple commodity stacks merge only within one lot. This touches `Entity.cs:1560-1580`
  and `ItemsOfType`.
- Floor loot persistence stays out of scope and is noted.
- Player crafting does not exist. Inputs never become instances.
- `PropertiesPanel` shows brand, flavour, maker and station. This is the first time
  flavour text reaches the player.

## 10. Sizing, if everything were built at once

This was sized before the scope split. §11 gives the size of the schema cut alone.

Provenance core (F1-F6; no routes, no mining yield):

- Stores: catalog (`FactionProductData` reshaped, `ItemData` key 3 retired, optional
  role input spec), run (ledger document and skeleton, both `RunTypes`), player
  (untouched).
- Files: `ItemInstance.cs`, `ItemData.cs`, `FactionProduct.cs`, `ItemManager.cs`,
  `LoadoutGenerator.cs`, `Loadout.cs`, `ZoneGenerator.cs`, `Galaxy.cs`, `SavedGame.cs`,
  `AetheriaStores.cs`, `EntitySerializer.cs`, `Entity.cs`, `PropertiesPanel.cs`,
  `TradeMenu.cs`, `ActionGameManager.cs`, one new `Provenance.cs` (lot, ledger,
  skeleton, synthesis), `TradeMenuDebug.cs` (deleted), three test files, and
  `AetherDb/Program.cs`. About 20 files.
- Lines: roughly +700 / -300, net about +400. The deletions cover `ProductRole`,
  `RoleFill`, `Instantiate`, `TradeMenuDebug` (about 150 alone) and the product
  selection code.
- Catalog authoring: segment bands on 37 products, default recipe table, facility
  products for Refinery, Assembly Line, Shipyard and extractors (or skeleton-only
  facilities with no product). Studio work, not code.

Routes and piracy (F7, F8), if the scope changes: skeleton routes, a freighter spawner,
a hauling agent across wormholes, and a shipment-lost ledger. Roughly +400-700 lines,
a real agent behavior, and a dependency on gate item 5 for anything better than
patrol-shaped movement.

Mining yield (F10): filling `BodyData.Resources`, restoring the mint at `Zone.cs:298`,
and extractor behaviors. It is separate from provenance and currently out of scope.

## 11. Schema now versus generation later

### What the schema cut lands now

Each item below names its writer and reader today.

- **`LotId` and a run-store `ProvenanceLedger` document** (F1 (c)), with GC by
  reachability in `RunSave.Commit` and removal in `RunSave.Clear` through `RunTypes`.
  Writers: today's mint sites. Readers: stats, price, tier and brand.
- **`ItemInstance.Lot`** on the base class, so a `SimpleCommodity` can carry an
  extraction origin too. The stack split at `Entity.cs:1576` copies it, and stacks
  merge only within one lot. `ItemInstance.Data` stays on the instance. Invariant: one
  design per lot, checked by the ledger.
- **`Lot { Provenance, Properties }`.** Properties are mocked quality: one lot value
  plus per-role values (F4 (a)). `CraftedItemInstance.Quality` (key 2), `Ingredients`
  (key 9), `Product` (key 10) and `RoleFill` leave the instance, with keys retired.
  Durability stays per unit. Readers: `ItemManager.Evaluate`, `EquippedItem`,
  `ConsumableItemEffect`, `GetPrice`, `GetTier` (§5).
- **A `Provenance` union.** Every case is named by a ruling:
  - `Produced { Faction, Station, Facility: LotId, Inputs: LotId[] }` for "who,
    where, from what".
  - `Extracted { Zone, Commodity }` for the mining terminal.
  - The placeholder case from F0.
- **`ItemData.Manufacturer` removed** (key 3 retired). **`FactionProductData.Roles`
  and `ProductRole` removed**; no product authors them. `FactionProductData` keeps
  name, flavour, design and maker as the brand catalog.
- **Branding derived:** `ItemManager.Brand(instance)` resolves the lot's maker and the
  design to a `FactionProductData`. Readers: `PropertiesPanel` (brand, flavour, maker)
  and the Studio grouping default.
- **Scar fixes the schema forces:** delete `ItemManager.Instantiate` and
  `Unpack(instantiate)`, which would otherwise mint lot-less copies. Delete
  `TradeMenuDebug.cs`.

### What the schema must carry later without reshaping

- **Routes and shipments** are new run documents that hold instances or `LotId`s. The
  lot has no holder field (conflict 3), so a shipment adds custody without touching
  `Lot`.
- **Seizure** as custody change needs no schema. A Njordr-style `Seized` origin would
  be one more union case, which adds rather than reshapes.
- **Facility terminal (F2):** `Founded { Station }` is one more union case, added with
  the generator.
- **Station roster, sources, recipes and segment bands (F3, F5, F6 band)** are new
  catalog or run documents, and none changes `Lot` or `ItemInstance`.

### Fields with no reader or writer until generation exists

These are named by the rulings, so they are in the cut, flagged here as instructed.

- `Produced` as a whole: `Station`, `Facility` and `Inputs` have no writer until the
  generator exists, and no reader except the brand projection's faction lookup.
- `Extracted` has no writer, because mining yield is commented out (`Zone.cs:298`).
- A station identity for `Produced.Station`. Either option adds a writer-only field
  now: (a) a `StationId` minted on `OrbitalEntityPack` by `ZoneGenerator`, whose
  writer exists but whose reader waits for the generator; or (b) the reference type
  alone, with no field on the pack. **Recommend (b):** add the reference type now and
  add the pack field with the station roster (F3). The roster is what decides
  identity, so a pack-index id minted now would be replaced.

Deliberately **not** in the cut, because no ruling names them and they have no reader:
the segment quality band on `FactionProductData`, `Founded`, `Seized`, route,
shipment and source records, and a recipe or role input spec.

### F0. What today's mint sites put in provenance before a generator exists

This fork blocks the schema cut.

- (a) **Required provenance with an honest placeholder case:**
  `Attributed { Faction }`, meaning "made by this faction; where and from what not
  generated yet". Every crafted instance gets a lot.
  - `LoadoutGenerator` (NPC gear, station gear and stock) and `Loadouts.Materialize`
    write the chosen product's maker. One lot per (entity, product) replaces
    `CopyBuild`.
  - The starting ship uses the generator path, now without the re-mint.
  - Console `give` writes `Attributed` with no faction, and its brand is the bare
    design name.
  - Loot mints nothing and carries the lot.
  - The brand projection reads faction and design. Quality is rolled onto the lot as
    today.
- (b) **Optional provenance:** a nullable lot or empty origin. Every reader then has a
  "no provenance" branch that survives the generator, and two shapes decide stats.
  That is split authority by construction.
- (c) **A tiny synthesizer now:** mint a facility lot per generated station and write
  `Produced { faction, station, facility, inputs: [] }` for station stock.
  - NPC ships and loot in unentered zones still have no real station, because stations
    are lazy (§3).
  - Empty `inputs` lies by omission.
  - It is generation work, pulled forward into a scope the operator deferred.
- **Recommend (a).** Every instance has one shape. Every field written now has a
  reader now. The placeholder states what is unknown rather than inventing a station.
  When the generator lands, mint sites switch from `Attributed` to `Produced` and the
  case is deleted, except possibly for the debug console. That deletion is the
  generator cut's negative check: after it, nothing writes `Attributed`.

### Fork status under the split

| Fork | Scope | Status |
|---|---|---|
| F0 placeholder origin | schema now | ask; recommend (a) |
| F1 ledger document | schema now | ask; recommend (c) |
| F4 quality on the lot | schema now | ask; recommend (a) |
| F6 `FactionProductData` becomes the brand catalog, `Roles` cut | schema now | default (band deferred) |
| Station reference type, no pack field | schema now | default (b) |
| F2, F3, F5, F7, F8, F9, F10 | generation, next scope | recorded; not asked now |

### Schema-cut sizing

- Stores: catalog (`ItemData` key 3 retired, `ProductRole` cut) and run (one ledger
  document, one commit, GC). Player untouched.
- Files: `ItemInstance.cs`, `ItemData.cs`, `FactionProduct.cs`, `ItemManager.cs`,
  `LoadoutGenerator.cs`, `Loadout.cs`, `EntitySerializer.cs`, `Entity.cs` (quality
  readers, stack split), `SavedGame.cs`, `AetheriaStores.cs`, new `Provenance.cs`,
  `PropertiesPanel.cs`, `TradeMenu.cs`, `ZoneRenderer.cs` (tier),
  `ActionGameManager.cs` (`give`, start ship), `TradeMenuDebug.cs` (deleted),
  `LoadoutTests.cs`, `RunSaveTests.cs`, `AetheriaStoresTests.cs`,
  `tools/AetherDb/Program.cs`. About 20 files.
- Lines: roughly +300 / -300, net close to zero.
- Catalog data: 37 designs lose a manufacturer that products already carry. No
  product authored `Roles`, so nothing is lost.
- Existing saves are discarded, per the migration target.
