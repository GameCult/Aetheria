<!-- Prose map for campaign aetheria-living-world. Economy section by Modeling modeling-ag9-economy, session self-2026-10-27-ag9, read at master 30030af6e (after merges 1ed357902 cut-economy-checks and 30030af6e cut-recipe-role-inputs). Target in force: aetheria-living-world:target:r2. -->
# Aetheria living world: map

Target: `aetheria-living-world:target:r2`. Its prose (`docs/aetheria-living-world-target.md`, cited at d61948f6) is on branch `eureka/aetheria-living-world-target` only, not on master (see Stale and missing). Prior art: `docs/research/2026-10-22-crafting-chains-prior-art.md`, `docs/research/2026-10-23-recipe-graph-structure-prior-art.md`. Rulings the economy code answers to: `salvage-by-input-role`, `ores-fill-metal-roles`, `ores-are-regional`, `draw-is-one-use`, `consumer-demand-shared-only`, `upkeep-leads-to-output`, `consumer-goods-share-gear-components` (all `aetheria-living-world:ruling:`).

This page maps what is live. Of the target's invariants, the catalog checks (L-recipe, L-one-kind-list, L-lore, L-source-sink, L-productive, L-deep report, L-consumer-demand role half) are live. Nothing in the running simulation is: no stock ledger, grades, lots, allocators, obligations, workforce or gate flow exists in code yet.

## The economy: catalog records and checks

### Records (catalog, `GameData/Aetheria.cc`, registered in `AetheriaStores.CatalogTypes`)

All in `Assets/Scripts/ServerShared/Recipes.cs` unless noted.

- **`RecipeData`** (`aetheria.recipedata` v1, key `recipe:<output key>`). The one record of what a recipe makes and takes. Keys: 0 Output, 1 OutputCount, 2 Inputs (`CommodityCount`: named commodities), 3 Cycle, 4 Workforce, 5 Upkeep (`CommodityCount`), 6 StationKind, 7 LoreHook, 8 Fills (role names this commodity fills), 9 RoleInputs (`ItemRole` {Name, Count}, the type `CraftedItemData.Roles` uses; additive, an older record reads an empty list).
- **`CommodityCount`** {Commodity, Count}. Names one commodity, never "commodity or role" (forbidden: a role field here).
- **`OutsideMarketData`** / **`OutsideMarketRow`** (`outside-market:<id>`): the region gate's rows {Commodity, Price, Buys, Sells, RatePerHour}.
- Read beside them, owned elsewhere: `CraftedItemData.Roles` (`ItemData.cs`; designs and `CompoundCommodityData`), `FactionProductData` with `LoadoutGenerator.IsOffered`, `WeaponData.AmmoType`, `StationKindData`.

### Where a recipe's role slots live (two stores, one reader)

A recipe takes roles from exactly one of two places:

1. **The output's own roles** when the output is a `CraftedItemData` with roles (`Recipes.IsRoleRecipe`): every gear and hull design, and every consumer good (a `CompoundCommodityData` with roles; `Recipes.IsConsumerGood` = commodity output and role recipe).
2. **`RecipeData.RoleInputs`** for every other recipe: an intermediate commodity that takes a role, for example iron taking 2 x iron-ore, which hematite or ilmenite fills.

`Recipes.RoleInputsOf(catalog, recipe)` is the only reader of a recipe's role slots and picks between them. L-recipe refuses a role recipe that also carries RoleInputs, so the two stores never both speak for one recipe. Consequence to keep in mind: a commodity recipe whose slots come from RoleInputs is not a consumer good; only a commodity whose item carries Roles is.

### Owners

| what | owner | notes |
|---|---|---|
| what a recipe makes and takes | `RecipeData` | role slots never copied into Inputs |
| a recipe's role slots | `Recipes.RoleInputsOf` | sole reader; `Recipes.RolesOf(output)` is read directly only by L-recipe's design-shape checks and by `DesignRoles` (roles a design demands, not slots) |
| who may fill a role | `Recipes.Fillers(catalog, role)` | commodity recipes whose Fills name the role, ordinal by key; derived per call |
| which designs need a recipe and demand roles | `EconomyChecks.OfferedDesigns` | offered by some faction (`LoadoutGenerator.IsOffered`), Station and Turret hulls excluded |
| which roles some design demands | `EconomyChecks.DesignRoles` | offered designs plus every design a recipe makes, recipe or no |
| which commodities a weapon draws | `EconomyChecks.Drawn` | sole reader of offered designs' `WeaponData.AmmoType`; shared by SourceSink and ConsumerDemand |
| what the gate buys and sells | `EconomyChecks.Gate` | from `OutsideMarketData` rows |
| every economy rule | `EconomyChecks` (`Assets/Scripts/ServerShared/EconomyChecks.cs`) | `Check` runs Recipe, Lore, SourceSink, Productive, ConsumerDemand and reports Deep |
| catalog report and authoring | `tools/AetherDb/EconomyCommand.cs` (`economy report`, `economy author <batch>` dry run and apply) | calls `EconomyChecks.Check`; prints recipe lines through `RoleInputsOf`; authoring batches in `tools/AetherDb/Economy/` (Munitions) |
| which filler a running facility consumes | **unowned** | decided at production time by the production organ of cut facilities-and-setting; nothing stores it today, and no record may (forbidden writer below) |

### Rules and what each reads

All rules but Recipe read recipes through `AllRecipes`/`Sound`, which drops null lists and entries without changing the stored record; Recipe alone names malformations.

- **L-recipe (`Recipe`)**: every commodity and every offered design has a recipe (pending faults); each recipe is well formed; Fills only on commodities; no RoleInputs on a role recipe; every role in `RoleInputsOf` has at least one filler. **L-one-kind-list** is raised here too (no faction places the recipe's station kind).
- **L-lore (`Lore`)**: a commodity's recipe has a lore hook.
- **L-source-sink (`SourceSink`)**: source is a grounded chain (`Depths` seeded with what the gate sells) or a gate sale. Sink is any of: a recipe's Inputs or Upkeep take it; it fills a role in `DesignRoles` or in any recipe's `RoleInputsOf`; an offered weapon draws it; the gate buys it. Consumer goods skip the sink half until needs tables exist.
- **L-deep (`Deep`)**: steps from extraction, a role as deep as its shallowest filler (`Candidate`). Reported, refuses nothing.
- **L-productive (`Productive`)**: spectral radius of the input matrix (Inputs, upkeep, workforce load, role inputs at their worst filler) below 1, naming the cycle; members ordinal-sorted, message in cycle order.
- **L-consumer-demand (`ConsumerDemand`, role half)**: for each non-raw, non-consumer commodity, walk forward over `takenBy` edges (Inputs, Upkeep, and an edge from each filler of every role in `RoleInputsOf`, commodity recipes only). If the reach contains a consumer good, it passes. Otherwise its uses are the distinct `DesignRoles` its reach fills, plus each drawn commodity in reach (a draw is one use), plus "upkeep" if anything in reach is upkeep. Fault when there are two or more uses or any upkeep. "Shared" is derived on every check; there is no stored flag or exemption list. The gate counts as no use. The needs-table half waits on cut 7.

### Derived state (never stored)

Role slots per recipe (`RoleInputsOf`), fillers per role, `OfferedDesigns`, `DesignRoles`, `Drawn`, gate sets, depths, the consumer-demand reach and uses, and the productivity matrix are all recomputed on every `Check`. No cache.

### Forbidden writers

- No rule reads `RolesOf(output)` for role slots; only `RoleInputsOf` does.
- `CommodityCount` gains no role field.
- No stored filler choice on `RecipeData` or any catalog record.
- No second "designs that demand roles" set: Recipe, SourceSink and ConsumerDemand read `OfferedDesigns`/`DesignRoles`.
- No stored "shared" flag or consumer-demand exemption list.
- `EconomyChecks` never writes the catalog. Only `economy author` apply (through `EconomyCommand`, after its held-fault checks) and the AetherDb catalog tools write `RecipeData`.

### Shared paths

`economy report`, `economy author` dry run and apply, and the planned Open/Compose wiring all call `EconomyChecks.Check`. Depth, source, sink, productivity, consumer demand and L-recipe all read role slots through `RoleInputsOf`. `EconomyCommand` refuses an authoring batch that introduces a fault or raises a held cycle's radius beyond relative noise (`RiseNoise` = 1e-12); an unknown batch name is refused without echoing it.

### Verification layer

- Unit: `tests/Aetheria.Shared.Tests/` `EconomyChecksTests`, `EconomyChecksFurtherTests`, `EconomyNullGuardTests`, `RecipeSchemaTests`, `RecipeRoleInputTests`, on `EconomyFixture` (Gear/Hull builders, EditRecipe, Reopen, Healthy).
- Command against the shipped catalog: `EconomyCommandTests` runs `Run`'s report and author dry-run branches against `GameData/Aetheria.cc`, asserts the output equals `Report`/`Author`, Console stays silent, and the catalog stays byte-identical.
- Suite: `Aetheria.Shared.Tests` on Yggdrasil (2848 green at dc4838bd9). The whole-suite Stryker run is infeasible (times out at 14400 s); mutation runs use a throwaway slim project over the six economy test files, never committed.
- The shipped catalog report is the content-level check: at 30030af6e it reports 146 faults (cut report total; 124 L-recipe + 18 L-source-sink + 4 L-consumer-demand by arithmetic from the h1 split, not separately reported. The consumer-demand four: Ballistic Powder, Nitric Acid, Nitrocellulose, Potassium Nitrate; Rocket Motor's no-sink line is gone).
- Untriaged: mutation survivors off the diffs' lines in `EconomyChecks.cs` (Productive numerics, the message join) and `EconomyCommand.cs` (apply and atomicity strings).

### Cut line for the next economy cuts

- facilities-and-setting: owns filler choice at production time and must read slots through `RoleInputsOf` and fillers through `Fillers`; it must not store the choice on the recipe.
- needs tables (cut 7): the needs half of L-consumer-demand and the consumer-good sink in SourceSink.
- Open follow-up: `recipe-less-design-roles` (pre-existing, left alone by cut-economy-checks h2).

## Stale and missing

- `docs/aetheria-living-world-target.md` (the target r2 DocRef, commit d61948f6) lives only on `origin/eureka/aetheria-living-world-target`; master has no copy. The target r2 statement of L-recipe ("inputs are commodities except in gear, hull and consumer-good recipes, which take roles") and L-roles are already contradicted by live code: intermediate commodities take roles through `RecipeData.RoleInputs` (ruling `ores-fill-metal-roles`). The next target revision should restate them.
- `docs/item-provenance-substrate.md` line 70 says no recipe model exists in live C#. True when written; `RecipeData`, `Recipes` and `EconomyChecks` now exist. It is a dated substrate snapshot, so the line stands as history, but a reader should not take it as current.
