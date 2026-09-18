# Stats and Power: Cut Map

Date: 2026-09-18

Status: Imagination pass, cut map. Nothing here has landed. Ends are owned by
`docs/stats-and-power-target.md`; this document owns the means.

Anchors are against `codex/item-provenance` HEAD `acb2fef9`. Claims marked
**(probe)** were measured headless against the real catalog `GameData/Aetheria.cc`
(read-only) with a scratch console referencing `Aetheria.Shared`, CultLib a temporary
detached worktree at `45c2f40`, built with `--artifacts-path` in scratch so this tree
got no `bin`/`obj` writes. Both are removed. The probe source is not kept; §1.6 records
what it did.

**Scope: stats and power only.** Fire control is the next campaign and gets its own map.
§8 names the seam and what fire control will need from this authority.

---

## Rulings (operator, 2026-09-18)

- **Q1: stats and power land before fire control.** "Stats before". Fire control is the next
  campaign and maps against the authority this builds (§8).
- **New, R-heat: the heat performance curve is replaced by a range with falloffs.** Operator:
  "Can we reuse most of the calculation and simultaneously simplify heat to remove the curve
  calculation? I never ended up authoring those curves much even before the breach, we'd be
  fine with a range and falloffs, which reduces to range checks and a lerp."
  - `ItemData.HeatPerformanceCurve` (`ItemData.cs:384`, a `BezierCurve`) goes. **The authored
    shape is minimum, maximum, optimum and plateau width** (operator, 2026-09-18: "Same shape,
    more intuitive controls"): where the part likes to be, how forgiving it is, and where it
    dies. Performance is 1 across the plateau and falls linearly to 0 at each bound, so the
    two sides are asymmetric whenever the optimum is off-centre — which is how most gear
    behaves, and how negent gear inverts.
  - **`OptimalTemperature` becomes authored, not derived.** `:409-430` scans 100 samples of a
    bezier to find it and caches the result; wear reads that inferred number
    (`Entity.cs:1386`). The scan, the cache and `_optimum` all die, and the value wear reads
    is one somebody typed.
  - Validation (load and Studio save, with the campaign's other checks): the optimum lies
    between the bounds, the width is not negative, and the plateau clamps to the bounds
    rather than poking past them. Fails loudly naming the item.
  - `Performance` becomes comparisons and a lerp.
  - `BezierCurve` itself stays: `Sensor.SensitivityCurve` still uses it.
  - **Migration is a fit, not a guess.** Each authored curve is sampled: the optimum is where
    it peaks, the width is how far it holds near that peak, and the bounds are already
    authored. The rewrite
    reports a per-item table (old optimum and width against new plateau and bounds) for the
    operator to review, and it rides with Cut 1's catalog rewrite rather than being a second
    migration. Pre-breach history is not consulted for curves: the operator has ruled the
    shape is going regardless.
  - **The balance change is intentional** (operator, 2026-09-18): "currently it's basically
    impossible to run many items without damaging them due to low thermal performance.
    Controlled wear is good, but we need a better lever for operational lifespan when an item
    is managed well." Under R-heat the plateau means full performance, so the thermal term of
    `Wear` (`Entity.cs:1386`) goes to zero inside it and only `deltaTemp` still costs
    anything. **Plateau width is therefore the operational-lifespan lever**: it is the band
    where careful operation is free, authored per item instead of emerging from a curve
    nobody drew. A negent weapon still pays, because it is nothing but swings.
  - Verification must show it: an item held inside its plateau takes no thermal wear, an item
    outside it does, and a fast swing across the plateau still wears. Each under its own
    mutation.
  - Wear reads `OptimalTemperature` (`Entity.cs:1386`), so the fit changes wear too. The
    verification must show wear unchanged for an item at its optimum and a sane curve
    elsewhere.
- **Q4: the input capacitor is the charging one**, and it is derived from `Energy` and
  `Cooldown` with an authored override. Operator: "Do you mean the charging capacitor that
  we're putting in to convert discrete power draw to continuous? Derived from energy and
  cooldown sounds convenient." Burst capacity and sustained rate stay separate levers.
- **Q5: four or five tiers, defaulted per behaviour kind.**
- **Q2, Q6, Q7: Self's defaults**, stated and not objected to — modifiers keep the name pair
  resolved at load, the trade menu shows the evaluated value and sorts by the ceiling, and
  negent needs nothing beyond `ConsumableProgress`.
- **Q3 stands as recommended:** accept per-tick recomputation; do not quantise heat. It is
  the recommendation most likely to be wrong (§9), on a three-hull sample.
- **Products author quality, designs author roles.** `ProductRole` already carries a mean and
  a standard deviation per role (`FactionProduct.cs:37-58`) — the manufacturer's technology
  and their quality control — so a market segment is a second product with one role raised.
  Zero designs declare roles today, so Cut 7 is what makes any of that reachable.

> **Cut 0 landed 2026-09-18** (`e9b688de`, tests `67217ed9`). One evaluation path
> (`PerformanceStat.Evaluate(IStatContext)`) with three contexts; the three arithmetic bodies
> are gone. Equivalence over the real catalog: 3123/3123 unequipped values identical, max
> delta 0. 64 tests, batchmode clean, the three shield probes still pass.
>
> Corrections to this map, from the Body:
> - The Cut 0 negative grep contradicted the map's own keeps. Four `Evaluate(PerformanceStat`
>   sites are legitimate wrappers, and `AetherDrive.cs:105` calls `Item.Evaluate` directly,
>   which the map did not mention.
> - **12 of 30 heat-bearing catalog stats disagree between the unequipped and at-optimum
>   paths, by up to ~100 units**, because a real `HeatPerformanceCurve` rarely reaches 1.0 at
>   its own reported optimum. That is R-heat's problem: the fit must decide whether the
>   plateau means full performance (it does) and therefore that these items get slightly
>   stronger at their optimum. Report that per-item in the migration table.
> - `ItemManager.CreateInstance` has no `ConsumableItemData` branch, so it falls through to
>   `CompoundCommodity` and a `ConsumableItem` is apparently never minted by the normal path.
>   Look before Cut 1 touches consumables.
> - The cut is net +55, not the ledger's ~+1: the interface and three contexts cost more than
>   the 44 deleted lines of arithmetic. Accepted, recorded rather than argued away.

> **Cut 1 landed 2026-09-18** (`05b5a209` schema+code, `38aec5dd` catalog+migration table).
> Soul verified the rewrite lost nothing: 1,181,195 common leaf paths, 0 changed values; term
> migration exact against the census; tombstoned keys absent from the bytes and the old reader
> fails loudly; keys 30/31 collide with no subclass. 71 tests.
>
> Soul's findings, and what they cost:
> - **S1 `Tractor Beam` is permanently dead** (`ItemData.cs:422`): min == max == optimum, width
>   0, so performance is 0 at every temperature and every heat-dependent stat collapses to
>   `Min`. Validation accepted it.
> - **S2, 14 designs went from immune to fatal.** With no authored curve the old code returned
>   1 at any temperature, below the minimum included; the new code returns 0 outside the
>   bounds, so a cold `Autocannon` is offline and wearing. Nobody decided this and the
>   migration doc claims the opposite.
> - **S3 the migration table's old-value columns are wrong for 15 of 51 rows** (the old getter
>   returned `MinimumTemperature` for curve-less designs; one design's old code threw), and
>   **S4 the at-optimum slice understates the change ~80x** — at-optimum is where a trapezoid
>   and its curve agree. Real band: 36 of 37 fitted designs up to +0.19, `Earp` down. No
>   qualitative inversion.
> - **S5/S6 validation is weaker than the ruling and than its own comment**: no plateau-clamp
>   check, zero-span and NaN accepted, and `AetheriaStores.Open` validates on read, so the
>   tools' writable path writes invalid catalogs (the migration relied on that).
> - **S7 four of Soul's five mutations survived**, including a `Quality` term ignoring its own
>   exponent — the migration's central equivalence claim is unpinned.
> - **S8 the shield Cut 3 probe passes at HEAD**, twice. Hands' `KInit` failure did not
>   reproduce; Cut 1 touched no shader.
>
> **Ruling (operator, 2026-09-18): re-author those items.** "reauthor those items". Bounds
> mean the part is dead outside them, which is the point of the change; the 14 are unauthored
> placeholders, not deliberate immunity. Zero-span ranges become an authoring error validation
> refuses. Correction to R-heat: **wear does not read `OptimalTemperature`** — `UpdatePerformance`
> reads `Data.Performance(temp)`, and the property had no reader at all before Cut 1. S3 is a
> reporting defect, not a gameplay one.
>
> **Findings closed 2026-09-18 (Hands, this pass):**
> - **S1/S2 re-authored.** `Tractor Beam` (newly authored bounds 200-400, matching the small
>   Sensors/gear family already using that exact range) and the 13 other curve-less designs got
>   real optimum/plateau values, drawn from the catalog's own house style: every already-fitted
>   design shares one Bezier template (peak at 25.5% of the range, plateau 12% wide), so each
>   unauthored item gets that fraction over its own bounds, or mirrors the exact-bounds sibling
>   that already carries it. Table, full reasoning and per-item "why":
>   `docs/stats-power-cut1-migration.md`. **Beyond the named 15**: enforcing S5/S6's plateau-clamp
>   rule broke the catalog for two more already-*fitted* designs whose plateau poked past a bound
>   — `Earp` (a real curve peaking exactly at its own Minimum, the same "dead at every
>   temperature" bug as S1) got the same re-authoring, and `The Bat` (a genuinely distinct fitted
>   optimum, kept) had only its `PlateauWidth` shrunk to fit. Real fork, not silently patched
>   around: the catalog cannot open at all without a decision here. Recorded, not asked, because
>   the fix was mechanical once the rule was enforced — flag if that call should have gone to
>   the operator instead.
> - **S5/S6 closed.** `StatValidation.ValidateHeatResponse` now refuses NaN in any of the four
>   heat-response fields, a zero-span range (`MinimumTemperature == MaximumTemperature`), and a
>   plateau whose low or high edge pokes past its bounds — the exact rule the ruling names and the
>   Performance() comment already claimed. The writable-path hole is closed for every path this
>   repository owns: `CultRecordRefs.Upsert` (`AetheriaStores.cs`), the one function every tool,
>   test and migration script writes catalog documents through, now validates before the write
>   reaches disk. The one hole left, named in the code: CultCache Studio's generic document editor
>   bypasses `Upsert` and exposes no per-document validation hook to attach to yet — that gap is
>   CultLib's, not this repo's, and stays named rather than silently assumed closed. Giving
>   `EquippableItemData`'s four heat fields non-degenerate default values (0/100/50/20, the same
>   shape `HeatResponseTests` already hand-authored) was necessary alongside this: dozens of
>   existing test fixtures across the suite construct designs that never cared about heat and
>   defaulted to the same zero-span trap Tractor Beam shipped with; without a sane default, closing
>   S5/S6 correctly would have required touching every one of those fixtures instead of the root
>   cause.
> - **S7 pinned.** Four new mutations, each with its own dedicated test (not a coincidentally-
>   sensitive existing one): out-of-bounds semantics (`return 1f` instead of `0f` outside bounds,
>   `HeatResponseTests.PerformanceIsZeroAtAndBeyondEachBound`); the plateau-clamp validation's two
>   sides, low and high (`HeatResponseTests.UpsertRefusesAPlateauThatPokesPastItsBounds`); and a
>   `Quality` term ignoring its own exponent (`LoadoutTests.QualityTermAppliesItsOwnExponent`,
>   `Exponent = 2` where the prior suite's `Exponent = 1` everywhere could not distinguish "reads
>   the term's exponent" from "always uses 1"). **A fifth, separate finding**: `Performance()`'s
>   own runtime plateau clamp (`max`/`min` around `OptimalTemperature ± halfPlateau`) is provably
>   dead code, not merely uncovered — exhaustively verified (a dense numeric sweep well outside
>   both bounds, 0 mismatches) that the outer `<= Minimum || >= Maximum` guard makes the inner
>   clamp unobservable at every input, validated or not. Recorded as unreachable, not worked
>   around with a forced test; the comment at `ItemData.cs` already says so ("redundant, not
>   load-bearing").
> - **S8 corrected.** No comment or note in this repository claims the shield Cut 3 probe is a
>   pre-existing environmental regression; the doc entry above already states, correctly, that it
>   did not reproduce. Re-verified clean at HEAD (batchmode, this pass).
> - **Zero `ConsumableItemData` records** in the catalog (Cut 0's question): nothing to touch.
>
> 9 new tests (80 total), 17 mutation cases in `tests/mutation_tests_stats_power_cut1.py` (all
> behave as declared), catalog re-authored in place (`GameData/Aetheria.cc`, 16 records changed,
> byte length unchanged), `AetherDb` census/factions/dangling/station-fit/hardpoint-fit/loadout-1
> byte-identical before/after re-authoring.

## 0. What is there now

### 0.1 The stat, as it exists

`PerformanceStat` (`ItemData.cs:575-622`) is a `[MessagePackObject]` **value**, not a
document. It has no `[CultDocument]`; every instance on disk lives nested two levels
inside a catalog record (`ItemData` → `BehaviorData` union member → the stat field).
Keys: `0 Min`, `1 Max`, `2 HeatExponentMultiplier`, `3 DurabilityExponentMultiplier`,
`4 QualityExponent`, `5 FromRole`.

**(probe)** The catalog holds **348 reachable `PerformanceStat` instances** across 180
documents:

| | count |
|---|---|
| `HeatExponentMultiplier != 0` | 275 |
| `DurabilityExponentMultiplier != 0` | 145 |
| `QualityExponent != 0` | 193 |
| `FromRole` set | **0** |
| `Min != Max` (actually authored, not a stub) | 188 |

By owner: `AutoWeaponData` 91, `ChargedWeaponData` 64, `GuidedWeaponData` 45,
`LauncherData` 40, `InstantWeaponData` 26, `RadiatorData` 25, `ReactorData` 20,
`SensorData` 12, `ThrusterData` 8, `AetherDriveData` 6, `EnergyDrawData` 3,
`HeatData`/`CapacitorData`/`ReflectorData` 2 each, `StatModifierData` 1, `GearData` 1
(an `AudioStat`).

**(probe)** Roles: 102 `CraftedItemData` designs, 51 equippable, 37 products. **0
designs author roles; 0 products author role spreads.** `FromRole` therefore does
nothing today in the shipped catalog — `Lot.QualityForRole` (`Provenance.cs:73-79`)
falls back to `Lot.Quality` for every read.

### 0.2 Three evaluation bodies that disagree

| Site | Terms applied | Modifiers |
|---|---|---|
| `ItemManager.cs:75-90` `Evaluate(stat, item)` — **unequipped** | quality × durability | no |
| `Entity.cs:1177-1186` `ConsumableItemEffect.Evaluate` | quality × **consumable effectiveness** | no |
| `Entity.cs:1354-1371` `EquippedItem.Evaluate` | quality × durability × **heat** | yes |

Three authorities for one number. The unequipped path in the trade menu and the
equipped path in the properties panel show different values for the same item on
purpose in no design document; it falls out of which function the caller reached for.

### 0.3 The catalog object holds per-entity runtime state, and it leaks

`PerformanceStat` carries two `[IgnoreMember]` dictionaries (`ItemData.cs:596-622`):

```
Dictionary<Entity, Dictionary<Behavior, float>> _scaleModifiers;
Dictionary<Entity, Dictionary<Behavior, float>> _constantModifiers;
```

`EquippedItem.Evaluate` calls `stat.GetScaleModifiers(Entity)` and
`GetConstantModifiers(Entity)` on **every evaluation** (`Entity.cs:1361,1365`), and both
accessors insert an entry for the entity when missing (`ItemData.cs:607-621`).
`StatModifier.RemoveModifier` (`StatModifier.cs:100-103`) removes only the inner
`[this]` key. **Nothing ever removes the outer `Entity` key.**

So a shared catalog document holds a strong reference to every `Entity` that has ever
evaluated any stat, for the life of the process. Every destroyed ship, every zone left
behind, stays reachable from `Aetheria.cc`'s in-memory records. This is the target
doc's "authored catalog data that also stores runtime modifier state per entity" stated
as a defect rather than a smell. It is the strongest reason for the resolver, stronger
than the performance argument.

Not measured: whether the leak is visible in a real session's memory profile. That is
an operator check (§7, O1), not a claim this map makes.

### 0.4 Evaluation cost, measured, and why it is not the motive

`Weapon.Execute` calls `UpdateStats()` unconditionally every tick (`Weapon.cs:127-131`),
so an **idle** weapon still evaluates 13 stats per tick (10 in `Weapon.UpdateStats`
`:113-125`, 3 in `InstantWeapon.UpdateStats` `:107-117`); a `ChargedWeapon` 16
(`ChargedWeapon.cs:111-123`).

**(probe)** Real generated loadouts (`LoadoutGenerator`, seed 1, the three hulls that
generate at HEAD):

| hull | items | behaviors | evaluations/tick |
|---|---|---|---|
| `LonginusX` (Ship) | 14 | 24 | **78** |
| `Turret` | 9 | 15 | 20 |
| `Zenith` (Station) | 8 | 10 | 17 |

78/tick is 4,680/s at 60 fps for one ship; a zone of 7 entities at the median is
~8,400/s. Each evaluation is three `pow` calls plus two nested dictionary lookups and
two dictionary iterations. That is around a millisecond per second per ship. **Caching
buys almost nothing here.** Say so plainly rather than selling the resolver on speed:
the resolver is bought by ownership, by the leak, and by making a power-supply term
cheap enough that adding it is not a regression. The operator's "we don't wanna pay
that cost for every stat evaluation" is satisfied because the power-supply source
changes at most once per tick, not because 78 became 12.

### 0.5 The power path, end to end

**Draw.** Nine call sites, all inside `ServerShared`, none outside it:

| site | shape |
|---|---|
| `EnergyDraw.cs:46` | continuous or per-activation, `PerSecond` flag |
| `Thruster.cs:77` | continuous, scaled by input axis |
| `AetherDrive.cs:138` | continuous, three axes summed |
| `Radiator.cs:74` | continuous, scaled by temperature ratio |
| `ConstantWeapon.cs:87` | continuous while firing |
| `InstantWeapon.cs:98` | **instant**, per burst |
| `InstantWeapon.cs:178` | **instant**, per shot |
| `Sensor.cs:85` | **instant**, per ping |
| `Shield.cs:59` (+ `:54` `CanConsumeEnergy`) | **instant**, per hit taken |

**Supply.** `Entity.TryConsumeEnergy` (`Entity.cs:858-888`): drain every charged
capacitor evenly in a `do/while`, then hand any remainder to every online reactor as
`Reactor.ConsumeEnergy` → `Draw +=`. It returns `onlineReactors > 0`. **Every draw
succeeds while one reactor is online**, which is the target doc's first complaint.

`Reactor.Execute` (`Reactor.cs:85-147`), running last because `Reactor` is the only
`IOrderedBehavior` (`Order => 100`, `Reactor.cs:48`), subtracts its own generation from
the accumulated `Draw`, and:
- deficit → overload heat at `OverloadEfficiency`, then `Draw = 0`. Unmet demand is
  never refused, only taxed as heat.
- surplus → fill capacitors evenly, then throttle.

**The hidden priority the target names.** `EquippedItem.SortPosition` is set from the
**last** `IOrderedBehavior` found on the item (`Entity.cs:1347-1348`) and
`_orderedEquipment = Equipment.OrderBy(SortPosition)` (`Entity.cs:586,810`). Only
`Reactor` sets it, so reactor items sort last and **every other item sorts equal, in
equip order**. Whoever was equipped first drains the capacitors first. That is the
whole existing priority system: the order a player happened to fit their ship.

Within an item, `BehaviorGroups` run in `Group` key order and the chain breaks on the
first `Execute` returning false (`Entity.cs:1404-1414`), so a failed draw silently
stops everything authored after it in the group.

**Capacitors.** `Capacitor` (`Capacitor.cs:31-61`) is a bus capacitor: one shared pool,
charge added by the reactor's surplus and drained by anyone. There is no input-only
capacitor today.

### 0.6 What persists, and where

- **Catalog `Aetheria.cc`, read-only at runtime:** every `PerformanceStat`, every
  `BehaviorData`, `CraftedItemData.Roles`, `FactionProductData.Roles`. A schema change
  here is a migration with a census, like provenance Cut C.
- **Run store `run.cc`, deleted on death:** `EquippableItem.Durability`
  (`ItemInstance.cs:53`), `OverrideShutdown` (`:54`), `Lot` and its `RoleFill`s
  (`Provenance.cs:63-88`), `EntityPack.Temperature`/`Armor`/`Settings`/`WeaponGroups`
  (`EntitySerializer.cs:179-188`).
- **Runtime-only, never saved:** `Capacitor.Charge`, `Reactor.Draw`/`CurrentLoadRatio`,
  every `EquippedItem` derived performance value, the modifier dictionaries.
- **No migration mechanism exists.** `[CultDocument(name, "1")]` is identity only;
  CultCache resolves by name and refuses ambiguity (`CultCache.cs:346-356`). The only
  compatibility is slot-driven: an extra persisted `[Key(n)]` is ignored (reported in
  `LastSchemaMigrationReports.IgnoredExtraSlots`), a missing one is defaulted, and any
  type change is a hard refusal. So the only safe moves are **append a new key** or
  **tombstone a key by comment and rewrite the catalog in one writable `Commit`**.
- `EquippableItem`'s next free MessagePack key is **12** (0,1 base; 2,9,10 tombstoned;
  7,8,11 live).

### 0.7 Consumers outside `ServerShared`

- `UI/Properties Panel/PropertiesPanel.cs:407-419` (equipped, evaluated),
  `:553-569` (design, shows `Min`–`Max`), and `UI/Menu/TradeMenu.cs:273-284` (shows and
  **sorts on `.Max` only**, no evaluation). Three near-duplicate
  `RuntimeInspectable` → `float`/`int`/`PerformanceStat` switches that disagree.
- `PropertiesPanel.cs:400-402` and `:545-550` are the only places
  `StatReference.Target`/`.Stat` are shown, as `SplitCamelCase()` strings.
- `UI/HUD/SchematicDisplay.cs:91-92,176-184` is the power HUD: `_reactor.Draw`,
  `_capacitors.Sum(Charge)/Sum(Capacity)`.
- `Editor/CultCacheDrawers.cs:17-29` is the `InspectableType` dropdown that writes a
  **bare type-name string** and silently resolves an unknown one to "None". It drives
  `StatModifier.cs:23` `RequireBehavior` and `StatModifier.cs:136` `StatReference.Target`.
- `Gameplay/ActionGameManager.cs:899` is the only gameplay gate on power:
  `GetBehavior<Reactor>() == null` refuses undock.
- Tests: `LoadoutTests.cs:491-533` `StatsReadTheLot`, `:554-571`
  `EvaluateDurabilityExponentReadsLotQuality`, `:575-598`
  `EquippedItemThermalExponentReadsLotQuality`; `IffAndCombatTests.cs:286-321` builds
  `LockWeaponData` stats. `tests/mutation_tests.py:134-196` mutates the stat path.
- `tools/AetherDb` touches **no** stat or power type. Its `census`
  (`Program.cs:37-108`) is the before/after invariant a catalog rewrite is verified
  against, and `:85-90` is the role-authoring audit.
- There is **no** `CustomPropertyDrawer` for `PerformanceStat`; in CultCache Studio it
  falls through to `inspector.DrawDefault`.
- There is **no** headless stat or power probe harness in the tree.

---

## 0b. Identity, lifecycle, authority

One row per persistent or runtime kind this campaign touches. No cut below is mapped
while a cell here is empty.

| Kind | What names it | What happens to it over time | Who decides it |
|---|---|---|---|
| **A stat** (`PerformanceStat`) | Its field on a `BehaviorData`/`EquippableItemData`, reached by reflection; on disk, the `[Key(n)]` slot inside its owning catalog record | Authored in the catalog; **immutable at runtime**; one instance shared by every entity equipping that design | The catalog author, through CultCache Studio or a console over `Aetheria.Shared` |
| **A declared term** (`StatTerm`) | Its index in its stat's `Terms` list, plus its `Source` kind (and `Role` name for a quality term) | Authored with the stat; migrated once from the four fixed exponent fields; immutable at runtime | The catalog author. Validation refuses a role the design lacks, and refuses a power-supply term on a stat that decides a power request |
| **A modifier term** | The pair (modifier `Behavior` instance, target stat entry) in the owning entity's resolver | Attaches when the modifier's item is equipped **and** its behaviour executes; detaches on `Dispose` or a tick without execution (`StatModifier.cs:117-124`); never outlives the entity | The `StatModifier` behaviour, writing into the entity's resolver. **Not** the catalog object |
| **A resolver entry** | `(EquippedItem or ConsumableItemEffect, PerformanceStat reference)` | Created at equip / consumable start; invalidated when one of its term sources changes; destroyed at unequip / entity teardown | `StatResolver`, one per `Entity`, the sole owner of every resolved stat value |
| **A bus capacitor** (`Capacitor`) | Its `Behavior` instance on an equipped item | Runtime-only; charge starts at 0 each session and is never persisted; charged only by the bus, drained only by the bus | The `PowerBus`. No behaviour drains it directly |
| **An input capacitor** | Its `Behavior` instance on the instant-activation item that owns it | Runtime-only; filled continuously by the bus up to its rate; spent whole by one activation; never persisted | The bus fills; the owning behaviour spends. Two writers, disjoint signs, named |
| **A power tier** | `EquippableItem.PowerTier` (new run-store key 12) on the *unit*, not the design | A player choice; persists with the run; survives save/Continue; resets only with the run | The player, through the schematic/inventory UI. Nothing else writes it |
| **A power grant** | `(Entity, EquippedItem)` for one tick | Computed once per tick before equipment updates; overwritten next tick; never persisted | The `PowerBus`. Read-only everywhere else, including the HUD |
| **A lot's per-role quality** (`RoleFill`) | `(LotId, role name)` | Minted with the lot and **immutable**; an upgrade mints a *new* lot and repoints the item, which the resolver sees as a quality-source change | `ItemManager.CreateLot` (`ItemManager.cs:110-133`), rolling the product's `ProductRole` spread |
| **A design role** (`ItemRole`) | Its `Name` string on `CraftedItemData.Roles` | Catalog-authored; adding one to a shipped design changes what existing lots fall back on (`QualityForRole` returns `Lot.Quality` for an unfilled role) | The catalog author. Agents generate, the operator reviews |

Two rows deserve their consequence spelled out.

- **A stat is shared; a resolved value is not.** That single sentence is the whole
  ownership fix. Everything per-entity moves off the catalog object into the resolver.
- **A lot is immutable, so quality is a rare invalidation.** An upgrade is a lot swap,
  not a mutation, so the resolver never needs to watch a quality number — only the item's
  `Lot` field.

---

## 1. Authority maps

### 1.1 Stat values

- **Owner:** `StatResolver`, one per `Entity`, constructed with it.
- **Inputs:** the stat's `Min`/`Max` and declared `Terms`; the current value of each
  term's source (heat, durability, quality-for-role, power supply, consumable
  progress); the modifier terms attached to that entry.
- **Outputs:** one `float` per `(item, stat)`, read by every behaviour and by the UI.
- **Derived state:** every `Weapon.Damage`-style cached field on a behaviour becomes a
  read of the resolver, not an independent computation.
- **Forbidden writers:** `PerformanceStat` may hold no per-entity state at all — the
  two dictionaries and their four accessors are deleted, not hidden. `ItemManager` and
  `ConsumableItemEffect` stop computing stat values. No behaviour writes into another
  behaviour's stat.
- **Shared paths:** equipped items, consumable effects, the properties panel, the trade
  menu and any future headless balance harness resolve through the same entry point. The
  unequipped case (trade menu, an item on the floor) is the one path with no entity;
  it resolves against a null-entity context that has no heat, no power and no modifiers,
  and it says so in its own name rather than being a third quiet dialect.
- **Deletion line:** `ItemData.cs:580-594` (the four fixed exponent fields),
  `ItemData.cs:596-622` (the dictionaries), `ItemManager.cs:75-90`, `Entity.cs:1177-1186`,
  `Entity.cs:1354-1371`, `StatModifier.cs:61-103` (the reflection target search and the
  writes into the catalog object) all go before the resolver's first read lands.

### 1.2 Power

- **Owner:** `PowerBus`, one per `Entity`, stepped once per tick from `Entity.Update`
  **before** `_orderedEquipment` updates.
- **Inputs:** each reactor's resolved generation for the tick; each bus capacitor's
  stored charge; each consumer's request, which is a resolved stat multiplied by a
  behaviour-supplied scalar in [0,1]; each item's `PowerTier`.
- **Outputs:** a grant ratio per equipped item in [0,1]; the reactor load ratio; the
  overload and throttling heat the reactor adds.
- **Derived state:** `EquippedItem.PowerSupply` (the grant ratio) is display-only and
  stat-source-only. `Capacitor.Charge` is written only by the bus. `Reactor.Draw`
  becomes a **reported** total, not a sink for unmet demand.
- **Forbidden writers:** `Entity.TryConsumeEnergy` and `Entity.CanConsumeEnergy` are
  deleted, so no behaviour can spend energy on its own. `Reactor.ConsumeEnergy` is
  deleted. `Capacitor.AddCharge` becomes internal to the bus and the owning behaviour.
  Behaviour execution order is no longer allowed to decide who gets power — it decides
  nothing about power at all.
- **Shared paths:** player firing, AI firing, thrust, radiators, shields and sensor
  pings all request through the same path. There is no "the player's weapon is special"
  branch; fire control will inherit this (§8).
- **Deletion line:** `Entity.cs:851-888` and `Reactor.cs:80-83,90-141` are cut before
  the bus's first grant is computed.

### 1.3 Priority tiers

- **Owner:** `PowerBus`'s allocation pass.
- **Inputs:** the per-item tier and the per-item request.
- **Outputs:** the grants.
- **Derived state:** nothing. A tier is a stored player choice, not a derivation.
- **Forbidden writers:** `EquippedItem.SortPosition` must no longer influence who is fed
  — and once the bus exists, `IOrderedBehavior` has exactly one remaining job (running
  the reactor's own heat accounting last) which the bus subsumes. `IOrderedBehavior`,
  `Reactor.Order` and the `OrderBy(SortPosition)` sorts are candidates for deletion in
  the same cut; the map assumes they go, and §7 O3 is where the operator confirms
  nothing else wanted that ordering.
- **Shared paths:** the tier is set from the schematic UI and read by the bus. The AI
  sets tiers through the same field or not at all.
- **Deletion line:** `Entity.cs:1347-1348`, `Entity.cs:586`, `Entity.cs:810`,
  `Reactor.cs:48`, `Behaviors.cs:127-130`.

---

## 2. The target shape, named

Types this campaign creates. Bodies are not transcribed; what matters is what each owns
and what must die under its own mutation.

**`StatSource`** — an enum: `Heat`, `Durability`, `Quality`, `PowerSupply`,
`ConsumableProgress`. Closed. Adding a member is a schema change with a census.

**`StatTerm`** — `[MessagePackObject]`: `Key(0) StatSource Source`, `Key(1) float
Exponent`, `Key(2) string Role`. `Role` is meaningful only for `Quality`; validation
refuses it elsewhere rather than ignoring it.

**`PerformanceStat`** — keeps `Key(0) Min`, `Key(1) Max`; gains `Key(6) List<StatTerm>
Terms`; **tombstones keys 2, 3, 4, 5**. The name stays: renaming it would churn 98 call
sites and buy no invariant.

**`StatResolver`** — per entity. Owns `(item, stat) → value`. Exposes resolve,
invalidate-by-source, attach-modifier, detach-modifier, and dispose. The only type that
may hold a resolved value.

**`StatContext`** — what a resolver entry reads: the item's heat performance, durability
ratio, lot, power supply and consumable progress. Implemented by `EquippedItem`, by
`ConsumableItemEffect`, and by a bare unequipped context whose heat, power and modifier
sets are empty. This is the seam that makes the three dialects one function.

**`PowerBus`** — per entity. Owns supply, demand, tiers, grants, and every capacitor
write.

**`IPowerConsumer`** — implemented by a behaviour that draws: it names *which*
`PerformanceStat` is its request and supplies a scalar in [0,1] for this tick. It does
**not** hand the bus a number it computed itself. That is deliberate: it gives the
validator an exact, static set of request stats, so "a stat deciding a power request may
not depend on power supply" is checkable at catalog load rather than by discipline. A
consumer whose draw is genuinely multi-term (`AetherDrive`, three axes) expresses it as
one stat times a computed scalar.

**`InputCapacitor`** — a behaviour on an instant-activation item. The bus fills it at a
rate; one activation spends a whole shot's worth or does not fire. Its capacity and rate
default from the item's existing `Energy` and `Cooldown` stats so that no catalog
authoring is required for the first cut (§7 Q4).

**`StatValidation`** — the loud refusal. Runs at catalog load, at Studio save, and at
equip. Names the items and the stat.

---

## 3. Cuts

Deletes come first inside each cut, and subtraction cuts are kept separate from
behaviour cuts.

### Cut 0. One evaluation path

- **Repo/branch:** `Aetheria` `codex/item-provenance` from `acb2fef9`. Depends on
  nothing.
- **First:** capture `tools/AetherDb` `census`, `factions`, `dangling`, `station-fit`,
  `loadout 1`, `hardpoint-fit` into scratch, byte-for-byte, same method both sides.
  Record the `dotnet test` baseline at HEAD (the headless map recorded 32 passed / 2
  failed at `1e647953`; re-measure, do not assume).
- **Deletes first:**
  - `ItemManager.cs:75-90` (16 lines) — the unequipped body.
  - `Entity.cs:1177-1186` (10) — the consumable body.
  - `Entity.cs:1354-1371` (18) — the equipped body.
- **Adds:** one evaluation function taking a `StatContext`, plus the three context
  implementations. `EquippedItem`, `ConsumableItemEffect` and the unequipped case
  implement it; each supplies only the sources it has.
- **Per-file changes:**
  - `Behaviors.cs:60` `Evaluate(stat) => Item?.Evaluate(stat) ?? Consumable.Evaluate(stat)`
    becomes a resolve against the owning context. **The method name `Evaluate` and its
    signature stay**, so all 81 behaviour call sites are untouched. This is the one place
    in the campaign where preserving a name is the right call: it is a genuine facade,
    not a compatibility shim, and it delegates rather than deciding.
  - `PropertiesPanel.cs:529` and `:490` both route through the new contexts.
  - `TradeMenu.cs:273-284` keeps showing `.Max` **in this cut** — changing what the trade
    menu shows is a design change, and it belongs in Cut 8 with the operator's ruling
    (§7 Q6).
- **Authority map:** §1.1, minus the resolver and minus the modifier move. This cut
  makes one function own the arithmetic; it does not yet move where modifier state
  lives.
- **Verification:**
  - builds: `dotnet build Aetheria.Shared/Aetheria.Shared.csproj`,
    `dotnet build tools/AetherDb`, each with `-p:CultLibRoot=<worktree>`.
  - tests: `LoadoutTests.StatsReadTheLot` pins that a role-named stat reads the role and
    an unnamed one reads workmanship.
    `EvaluateDurabilityExponentReadsLotQuality` pins the durability path.
    **New:** a test pinning that the *unequipped* and the *equipped-at-full-health-and-
    optimal-temperature* paths now agree, which they do not at HEAD. That test is the
    cut.
  - negative: `rg "public float Evaluate\(PerformanceStat" Assets/` returns exactly one
    hit. Verified against legitimate names: `BezierCurve.Evaluate(float)`,
    `ExponentialCurve.Evaluate(float)`, `Environment.Evaluate(float2,…)` and
    `ZoneData.Evaluate(float)` all differ in parameter type, so the pattern does not
    collide.
  - mutation: restore heat to the unequipped path; the new agreement test must go red
    naming it.
  - operator: none. No visible change is intended, and if one appears it is a defect.

### Cut 1. Terms replace the four fixed exponents

- **Depends on:** Cut 0.
- **First:** a read-only probe against a **copy** of `Aetheria.cc` answering: does the
  catalog still open read-only with keys 2-5 gone, and how many records report
  `IgnoredExtraSlots`? Record the file's sha1 and byte length. This is provenance Cut C's
  method (`docs/item-provenance-cut.md:179-196`) and it is not optional.
- **Deletes first:** `ItemData.cs:580-594` — `HeatExponentMultiplier`,
  `DurabilityExponentMultiplier`, `QualityExponent`, `FromRole` (15 lines), replaced by a
  tombstone comment in the style of `ItemData.cs:281-282`: *keys 2, 3, 4 and 5 belonged
  to fixed exponent fields now expressed as terms; do not reuse them.*
- **Adds:** `StatSource`, `StatTerm`, `PerformanceStat.Terms` at `Key(6)`.
- **Per-file changes:** the single evaluation function from Cut 0 walks `Terms` instead
  of the four fields. `ConsumableItemEffect`'s `effectiveness` becomes a
  `ConsumableProgress` term rather than a hard-coded factor, which removes the last
  reason for that path to be different.
- **The migration.** A scratch console, **deliberately not committed**, opening
  `AetheriaStores.Open(..., catalogWritable: true)` and re-upserting every stored
  `ItemData` under its own key in **one `Commit`**. Precondition: Unity and CultCache
  Studio closed. The committed binary is the artifact; the code is not kept. Per stat,
  the rewrite is mechanical:
  - `HeatExponentMultiplier != 0` → a `Heat` term with that exponent (275 stats).
  - `DurabilityExponentMultiplier != 0` → a `Durability` term (145).
  - `QualityExponent != 0` → a `Quality` term with `Role = FromRole` (193; `FromRole` is
    unset on all 348, so every migrated quality term is roleless).
  - A zero multiplier becomes **no term**, not a term with exponent 0. The difference is
    the point: a stat declares only the terms it uses.
- **Authority map:** unchanged owner (the catalog author); what changes is that the
  stat's dependency set becomes data the validator can read instead of four fields that
  are always present and usually zero.
- **Verification:**
  - Reopen shows **zero** reports with `IgnoredExtraSlots`, and the expected byte delta.
  - `AetherDb` `census`, `factions`, `dangling`, `station-fit`, `loadout 1`,
    `hardpoint-fit` captured before and after are **byte-identical**. None of them reads a
    stat value, so any difference is a defect.
  - A reflection dump of all 180 records before and after, comparing every *evaluated*
    stat at quality 0.5, durability 1.0 and optimal temperature: identical to float
    tolerance. This is the real proof; `census` only proves nothing unrelated moved.
  - tests: `StatsReadTheLot` and `EvaluateDurabilityExponentReadsLotQuality` rewritten to
    build `Terms`, still pinning the same rules.
  - negative: `rg "HeatExponentMultiplier|DurabilityExponentMultiplier|QualityExponent|FromRole" Assets/ tools/ tests/`
    returns only the tombstone comment. `FromRole` is checked separately because
    `ProductRole.Role` and `RoleFill.Role` are legitimate and must not be swept.
  - Unity batchmode, editor closed, no `error CS` (§6).
  - Two commits, as Cut C landed: code, then the catalog rewrite.

### Cut 2. The resolver owns every value

- **Depends on:** Cut 1.
- **Deletes first:**
  - `ItemData.cs:596-622` (27 lines) — both dictionaries and all four accessors. **The
    leak in §0.3 dies here, structurally, not by cleanup.**
  - `StatModifier.cs:61-86` (26) — the reflection target search that matches by type
    name and grabs `PerformanceStat` references out of other designs.
  - `StatModifier.cs:88-103` — the writes into the catalog object.
- **Adds:** `StatResolver`; `StatValidation` for self-dependency and modifier-chain
  cycles; `StatModifier` rewritten to attach and detach a term in the entity's resolver.
- **Per-file changes:**
  - `Entity.cs` constructs a resolver with the entity and disposes it with it.
  - `EquippedItem` signals `Durability` and `Heat` invalidation from `UpdatePerformance`
    (`Entity.cs:1377-1393`), and `Quality` invalidation when its `Lot` changes.
  - `StatReference` (`StatModifier.cs:132-140`) keeps its `(type name, field name)` shape
    but is **resolved once at catalog load into a concrete field, and fails loudly when
    it does not resolve**. Today an unresolvable reference is a silent no-op
    (`StatModifier.cs:63-65` leaves `_stats` null and `ApplyModifier` would throw on
    first use). §7 Q2 is whether the operator wants a stronger identity than a name pair.
- **Authority map:** §1.1 in full.
- **Verification:**
  - tests: a test equipping the same design on **two** entities, attaching a modifier on
    one, and asserting the other's resolved value is unchanged. At HEAD this passes by
    accident because the dictionary is keyed by entity; after the cut it passes because
    there is nothing shared to get wrong. Mutation: make the resolver static; it goes red.
  - tests: a test that unequipping and destroying an entity leaves no reference to it
    reachable from the catalog record — a `WeakReference` to the entity, a forced
    collection, and an assertion that it is dead. **This test fails at HEAD.** It is the
    cut's headline and the only test here that proves the leak is gone.
  - tests: a stat whose term chain reaches itself through a modifier is refused at equip,
    naming both items and the stat.
  - negative: `rg "GetScaleModifiers|GetConstantModifiers"` returns nothing.
  - operator: O1 — play a session, open a few zones, kill some ships, and confirm memory
    does not climb the way it did.

### Cut 3. The power bus replaces `TryConsumeEnergy`

- **Depends on:** Cut 2 (the bus's grant is a resolver source).
- **Deletes first:**
  - `Entity.cs:851-856` `CanConsumeEnergy` (6 lines, one caller: `Shield.cs:54`).
  - `Entity.cs:858-888` `TryConsumeEnergy` (31).
  - `Reactor.cs:80-83` `ConsumeEnergy` (4).
  - `Reactor.cs:90-141` — the deficit and capacitor-fill branches as a *demand*
    mechanism (~52 lines; the overload-heat and throttling arithmetic survives, moved
    under the bus's numbers).
- **Adds:** `PowerBus`; `IPowerConsumer` on the nine draw sites; `EquippedItem.PowerSupply`.
- **Per-file changes:** each of the nine sites in §0.5 stops calling a spend and starts
  declaring a request. The five continuous ones (`EnergyDraw`, `Thruster`, `AetherDrive`,
  `Radiator`, `ConstantWeapon`) convert directly. The four instant ones
  (`InstantWeapon.cs:98,178`, `Sensor.cs:85`, `Shield.cs:59`) are **out of scope for this
  cut** — they keep a direct capacitor spend, marked with a comment naming Cut 4 as their
  owner, and they are the one temporary split this campaign accepts. That split is named,
  bounded to one cut, and Cut 4's deletion line closes it.
- **Authority map:** §1.2, with the instant sites listed under forbidden writers as
  *not yet*, which is the honest word.
- **Verification:**
  - tests: a ship whose requests exceed generation gets a total grant equal to
    generation plus available capacitor charge, and **not** more. At HEAD the equivalent
    assertion is unwritable, because every draw succeeds.
  - tests: a refused request produces no effect at all — not a partial one, and not an
    overload-heat receipt. Mutation: make the bus grant the full request and heat the
    reactor; it goes red.
  - tests: two ships with identical loadouts fitted in **opposite equip order** get
    identical grants. That is the death of the hidden priority, and it fails at HEAD.
  - negative: `rg "TryConsumeEnergy|CanConsumeEnergy|ConsumeEnergy"` returns nothing.
  - operator: O2 — fly with a weak reactor and confirm that things starve visibly and
    legibly rather than cooking the reactor.

### Cut 4. All draw is continuous

- **Depends on:** Cut 3.
- **Deletes first:** the four direct capacitor spends left standing by Cut 3
  (`InstantWeapon.cs:98,178`, `Sensor.cs:85`, `Shield.cs:59`), and with them the last
  path by which anything but the bus touches stored charge.
- **Adds:** `InputCapacitor`, with capacity and rate defaulting from the owning item's
  `Energy` and `Cooldown` stats (§7 Q4).
- **Per-file changes:** firing becomes a check against the input capacitor's charge.
  `ChargedWeapon` (`ChargedWeapon.cs:125-152`) is the interesting case: its charge cycle
  already is a buffer, and `ChargeEnergy` should become the fill rate rather than a
  separate concept. `Shield.cs:57-61` is the odd one out: a shield hit is not an
  activation the player chose, so its "instant" draw is really a continuous reserve. Name
  it as such rather than giving it an activation buffer.
- **Verification:**
  - tests: a weapon whose input capacitor is partly filled does **not** fire a partial
    shot. The operator's ruling — "no item receives a fraction of a shot" — is the rule
    this test pins.
  - tests: halving the grant halves the refill rate and therefore the sustained rate of
    fire, without changing damage per shot.
  - negative: `rg "AddCharge" Assets/` hits only the bus and `InputCapacitor`.
  - operator: O4 — the feel of a weapon that stutters under brownout instead of firing
    at full rate and cooking the ship.

### Cut 5. Priority tiers

- **Depends on:** Cut 3.
- **Deletes first:** `Entity.cs:1347-1348` (the `SortPosition` assignment),
  `Entity.cs:586` and `:810` (the `OrderBy(SortPosition)` sorts), `Reactor.cs:48`
  (`Order => 100`), `Behaviors.cs:127-130` (`IOrderedBehavior`) — **subject to O3**.
- **Adds:** `EquippableItem.PowerTier` at `Key(12)`; the tiered allocation pass;
  a tier control in the schematic UI.
- **Per-file changes:** `EntitySerializer` needs nothing — the field rides inside
  `EquippableItem`, which is already packed. Old run stores default the field, and death
  clears the run anyway.
- **Authority map:** §1.3.
- **Verification:**
  - tests: with generation short of total demand, tier 0 is satisfied in full before tier
    1 receives anything, and tier 1 divides what is left.
  - tests: within a tier, two consumers with equal requests get equal grants; with
    unequal requests, proportional ones.
  - tests: a tier change on an equipped item takes effect on the next tick with no
    re-equip.
  - negative: `rg "SortPosition|IOrderedBehavior"` returns nothing.
  - operator: O3 — confirm nothing depended on reactors executing last for a reason this
    map did not find. O5 — the tier UI is playable.

### Cut 6. The power supply term, brownout, and the request-independence rule

- **Depends on:** Cuts 2, 3, 5.
- **Deletes first:** nothing. This is the one purely additive cut, and it buys the
  operator's named capability: a continuous consumer that degrades instead of stopping.
- **Adds:** `StatSource.PowerSupply` handling in the resolver; the validation rule that a
  request stat may not depend on power supply, directly or through a modifier chain.
- **Per-file changes:** the resolver invalidates `PowerSupply` entries once per tick from
  the bus. The validator runs at catalog load, at Studio save, and at equip; because
  `IPowerConsumer` names its request stat rather than computing a number, the set of
  request stats is exactly enumerable from the catalog, and the check is static.
- **Verification:**
  - tests: a thruster at half grant produces less thrust, by the authored exponent.
  - tests: a catalog holding a thruster whose `EnergyUsage` carries a `PowerSupply` term
    is **refused at load**, naming the item and the stat. Mutation: drop the rule; the
    test goes red.
  - tests: the same refusal through a modifier chain, at equip time, naming both items.
  - tests: the resolver recomputes a power-termed stat at most once per tick. This is the
    operator's "don't pay that cost for every stat evaluation" stated as an assertion.
  - operator: O6 — brownout reads as degradation, not as breakage.

### Cut 7. Roles authored

- **Depends on:** Cut 1 (terms carry the role).
- **Deletes first:** nothing. This is a content cut, and it is where the campaign stops
  being code.
- **Adds:** roles on designs, per-role means and deviations on products, and
  `Role`-carrying quality terms on the stats that should read a part rather than
  workmanship. Agents generate; the operator reviews. Written through the catalog's own
  types — AetherDb or a scratch console over `Aetheria.Shared` — never a hand-copied
  schema.
- **Note the sharp edge:** adding a role to a shipped design changes nothing for existing
  lots, because `QualityForRole` falls back to `Lot.Quality` for an unfilled role. But a
  *newly minted* lot from a product with no spread for that role rolls
  `new ProductRole()` — mean .5, and `ProductRole`'s default deviation
  (`FactionProduct.cs:33-48`). Authoring a role on a design without authoring the
  matching product spreads silently pulls every maker toward average. The census
  (`AetherDb Program.cs:85-90`) is where that is caught, and the two must land together.
- **Verification:**
  - `AetherDb census` reports every design's roles filled by every product that sells it;
    zero designs with roles and no matching product spread.
  - `AetherDb loadout 1` before and after: the failure count must not rise.
  - operator: O7 — the review pass on generated role content. Only the operator can do
    this.

### Cut 8. UI reconciliation

- **Depends on:** Cuts 2, 3, 5.
- **Deletes first:** `PropertiesPanel.cs:553-569` and `TradeMenu.cs:273-284`, the two
  divergent copies of the `RuntimeInspectable` switch (~35 lines together), collapsed
  into the survivor at `PropertiesPanel.cs:407-419`.
- **Per-file changes:**
  - `PropertiesPanel.cs:400-402` and `:545-550` stop building a label from
    `SplitCamelCase()`d type and field names and use the identity the resolver already
    has.
  - `SchematicDisplay.cs:176-184` reads the bus's supply and grants instead of
    `_reactor.Draw` and a hand-summed capacitor pool, and gains the per-item tier and
    grant readout — which is the whole point of tiers being a player choice.
  - `Editor/CultCacheDrawers.cs:17-29`: the type-name dropdown keeps writing a name, but
    Cut 2's load-time resolution means an unresolvable one now fails loudly instead of
    reading as "None".
- **Verification:**
  - operator only, and it is the largest operator surface in the campaign: the properties
    panel, the trade menu, the schematic HUD, the tier control, and a CultCache Studio
    click-through over a stat with terms. §7 O8.

---

## 4. Subtraction ledger, estimated

Line counts for deletes are exact against `acb2fef9`; adds are estimates and should be
compared with what lands, in the postmortem.

| Cut | Removed | Added (est.) | Net | Structure |
|---|---|---|---|---|
| 0 | 44 | ~45 | ~+1 | 3 evaluation authorities → 1 |
| 1 | 15 | ~40 | ~+25 | 4 fixed fields → a declared term list; 4 catalog keys retired; **catalog rewritten** |
| 2 | 53 | ~180 | ~+127 | per-entity state leaves the catalog object; a leak dies; validation appears |
| 3 | 93 | ~200 | ~+107 | 9 ad-hoc spends → 1 bus; 2 public entity methods deleted |
| 4 | ~30 | ~90 | ~+60 | the last direct capacitor writers die |
| 5 | ~12 | ~110 | ~+98 | `IOrderedBehavior` deleted; 1 new run-store key |
| 6 | 0 | ~120 | ~+120 | the only purely additive cut; buys brownout and the request rule |
| 7 | 0 | ~0 code | 0 | content; catalog data only |
| 8 | ~35 | ~20 | ~-15 | 3 stat renderers → 1 |
| **Total** | **~282** | **~805** | **~+523** | |

This campaign is net additive by roughly 500 lines and it should be. What it buys, named
against the "every new abstraction must name its owner" rule:

- `StatResolver` — owner of every resolved value. Replaces per-entity state living on a
  shared catalog document, and the entity leak that shape produced.
- `PowerBus` — owner of allocation. Replaces nine independent spend paths and an implicit
  priority nobody authored.
- `StatTerm` — replaces four fixed fields with a declared set, which is what makes
  validation possible at all.
- `InputCapacitor` — buys the operator's "all power draw is continuous" ruling.

Targets, packages, dependencies, formats: **none added, none removed**. One MessagePack
key added to `EquippableItem` (run store), one added and four retired on
`PerformanceStat` (catalog).

## 5. Build budget

- Packages: `Aetheria.Shared` (netstandard2.1), `tools/AetherDb` and
  `tests/Aetheria.Shared.Tests` (net10.0), Debug only. No new project, target, package or
  store.
- CultLib at `45c2f40`, unmodified, as a detached worktree passed with
  `-p:CultLibRoot=`, removed after.
- Headless commands use `--artifacts-path <scratch>` while another agent reads the tree.
- **Build host is this Windows workstation, and it is also the target** for every
  headless check. `Assembly-CSharp` is proven only by Unity batchmode, editor closed
  (§6), which is also this host.
- The catalog rewrite in Cut 1 is the one irreversible artifact. It runs once, with Unity
  and CultCache Studio closed, against a backed-up `Aetheria.cc`.

## 6. Unity batchmode recipe

Per cut that touches `Assembly-CSharp` (Cut 8, and any cut whose signature change reaches
`Assets/Scripts/UI` or `Assets/Scripts/Gameplay` — Cuts 0, 2, 3, 5 all do):

1. Confirm no editor: `Get-Process Unity`, and `Test-Path F:\Projects\Aetheria\Temp\UnityLockfile`.
   If either says yes, stop and ask. Never two batchmode runs at once.
2. `Start-Process "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe" -ArgumentList '-batchmode -nographics -quit -projectPath F:\Projects\Aetheria -logFile <scratch>\unity-cut-N.log' -PassThru`
3. `Select-String -Path <log> -Pattern "error CS|Exiting batchmode"`.

There is no stat or power probe harness in the tree and this map does not add one. The
NUnit suite reaches everything in `ServerShared`, which is where all of this lives;
batchmode is only the compile check for the UI edits.

## 7. Operator forks

Most blocking first.

**Q1 — Does the campaign land before or after fire control?**
`docs/three-gates-scope.md` puts fire control at item 4 of the shipping list and
`docs/headless-playground-cut.md` maps it as Cut 2. Fire control will roll hits over
weapon and targeting stats, and those rolls want a resolver that can hand out a value
without re-deriving it, and a power model where a weapon can be starved.
**A:** stats and power first, fire control leans on the finished authority.
**B:** fire control first, and it builds its rolls on `PerformanceStat` as it is today,
then migrates.
**Recommended: A**, because B means fire control is written twice — once against three
disagreeing evaluation bodies and a stat that leaks entities, then again. But A delays
the shipping list's item 4, and that is the operator's call, not this map's.

**Q2 — How does a modifier name the stat it targets?**
Today it is `(type name string, field name string)`, matched by reflection, and an
unresolvable pair is a silent no-op that would throw on first use. Exactly **one**
modifier is authored in the whole catalog (**probe**: `CapacitorData.Capacity`,
Multiplier, and it does resolve).
**A:** keep the name pair, resolve it once at catalog load, fail loudly. Studio's
existing `InspectableType` dropdown (`CultCacheDrawers.cs:17-29`) gains a second popup
for the field so a name is never typed.
**B:** give every stat a stable authored id and reference that.
**Recommended: A.** One authored instance does not justify a new identity scheme, and B
is a registry by another name — the thing doctrine says not to reach for. A converts the
silent failure into a loud one, which is what the target doc actually asked for. If role
authoring (Cut 7) multiplies modifiers into the dozens, revisit.

**Q3 — Heat changes every tick. What does "recompute only when a source changes" mean
for it?**
275 of 348 stats carry a heat term, and `ThermalPerformance` is recomputed every tick in
`UpdatePerformance` (`Entity.cs:1377`). A literal dirty-flag on heat means recomputing
almost everything every tick anyway, which is exactly what happens now.
**A:** accept it. Heat-termed stats recompute per tick; the resolver's win is ownership
and the fact that power supply, durability and quality — the sources that would otherwise
multiply the cost — are cheap. Measured cost at HEAD is ~4,680 evaluations/s for the
heaviest generated ship (**probe**), around a millisecond per second.
**B:** quantize `ThermalPerformance` to a step (1/256, say) and invalidate only when it
crosses one. Turns a per-tick recompute into a rare one at the price of a visible
quantization in stat values and HUD bars.
**Recommended: A.** The measurement says the cost is not real, and B pays gameplay
visibility for a performance win nobody needs. B stays available if a future zone is
much denser than 7 entities. This is the fork most likely to be wrong, because the probe
measured three hulls and a real fight may be heavier.

**Q4 — Where does an input capacitor's size come from?**
Every instant-activation item needs one, and **0 are authored**.
**A:** derive it — capacity from the item's `Energy` stat, rate from `Energy / Cooldown` —
so Cut 4 needs no catalog authoring and a ship at rest behaves as it does today.
**B:** author `InputCapacitorData` on every instant item, which is a catalog content pass
across 15 weapon-bearing designs before Cut 4 can land.
**C:** A now, with an authored override that B can fill in per design later.
**Recommended: C.** A alone makes burst capacity and sustained rate the same authored
number, which flattens a real design lever — a weapon that holds three shots and refills
slowly is a different weapon from one that holds one and refills fast. C keeps the cut
landable and keeps the lever.

**Q5 — How many priority tiers, and who authors the default?**
The target says critical first, each lower tier dividing what is left. It does not say
how many, or what an item defaults to.
**A:** three (critical / normal / auxiliary), defaulted per hardpoint type.
**B:** four or five, defaulted per behaviour kind (life support and reactor critical,
thrust normal, weapons normal, sensors auxiliary).
**C:** a free integer, everything defaulting to the same tier, and the player sorts it.
**Recommended: B.** A default that is already sensible is what makes the tier UI an
option rather than a chore, and per-behaviour is where the knowledge is. C makes every
new ship a configuration task before it flies.

**Q6 — What should the trade menu show for a stat?**
It shows and sorts on `.Max` alone (`TradeMenu.cs:273-284`) — the value at perfect
quality, perfect durability and optimal temperature, which no item ever has. The
properties panel shows the evaluated value for an equipped item and `Min`–`Max` for a
design.
**A:** show the evaluated value for the unit on offer, which is what the player is buying.
**B:** keep `.Max` as a comparable ceiling and add the evaluated value beside it.
**Recommended: A** for what the player reads, **B** for what the column sorts on — a
ceiling is the honest sort key when comparing designs, and the evaluated number is the
honest display when comparing units. This is a feel question and the operator should see
it before it lands.

**Q7 — Does the heat/negent work want anything from this campaign?**
`docs/stats-and-power-target.md`'s negentropy section and
`docs/negent-weapons-concept.md` both rest on "cooling is a change to a condition, not an
effect on a stat", which this campaign's `StatSource.Heat` term delivers unchanged. The
one thing negent needs that is not here is a **consumable charge gating the cooling**, and
`StatSource.ConsumableProgress` (Cut 1) is the term for it.
**A:** nothing more. Negent is content against this authority.
**B:** add a negent-shaped source now.
**Recommended: A.** The concept doc's own claim is "almost nothing new, which is the
point"; adding a source ahead of a single authored item would be inventing a mechanism to
match a document.

### Operator-only checks

- **O1 (Cut 2)** — play a session across several zones with kills, and confirm memory does
  not climb the way the leak in §0.3 predicts. The `WeakReference` test proves the
  reference is gone; only a played session proves it mattered.
- **O2 (Cut 3)** — fly with a reactor too small for the loadout. Starvation should be
  legible.
- **O3 (Cut 5)** — confirm nothing wanted reactors to execute last for a reason this map
  did not find, before `IOrderedBehavior` is deleted.
- **O4 (Cut 4)** — the feel of a weapon refilling under brownout.
- **O5 (Cut 5)** — the tier control is playable from the schematic screen.
- **O6 (Cut 6)** — brownout reads as degradation.
- **O7 (Cut 7)** — review the agent-generated role content. Nobody else can.
- **O8 (Cut 8)** — properties panel, trade menu, schematic HUD, tier control, and a
  CultCache Studio click-through over a stat with terms.

---

## 8. The seam with fire control

Fire control is the next campaign and owns its own map. Where the two touch:

**What fire control will find already built.**
- One resolved value per `(item, stat)`, from one owner, with no per-entity state on the
  catalog object. A roll over weapon and targeting stats reads the resolver; it does not
  evaluate.
- Weapon and targeting stats that can carry a `PowerSupply` term, so a starved targeting
  system rolls worse rather than switching off. That is the brownout ruling applied to
  accuracy, and it is available the moment Cut 6 lands.
- An input capacitor per instant weapon, which means "can this weapon fire right now" is
  a question with a cheap, local, authoritative answer — no bus round trip inside the
  roll.
- A power request path shared by player and AI, so an AI ship cannot draw in a way a
  player ship cannot.

**What fire control must own, and this map does not touch.**
- The hit roll itself, the target-data thresholds, subsystem selection, and
  `DamageSchematic` moving out of `Gameplay/EntityInstance.cs`
  (`docs/three-gates-scope.md:80-99`).
- **The pre-impact commit window.** `docs/three-gates-scope.md:92-97` rules that the
  outcome commits a short fixed time before impact and that fire control owns the horizon
  as one authored setting; `docs/shield-presentation-contract.md` binds presentations to
  it. Nothing in this campaign reads or writes that horizon. The one thing to be careful
  of: a weapon's resolved stats at the moment of the roll and at the moment of impact may
  differ, because heat and power move in between. **Fire control must snapshot what it
  rolled over at commit time.** The resolver hands out a value; it does not promise that
  value is still current a second later. That is the seam, stated as the rule fire
  control has to keep.
- Whether a starved weapon that fires anyway should roll worse, or simply not fire. This
  campaign makes it not fire (Cut 4: no fraction of a shot). If fire control wants a
  degraded shot instead, that is a ruling against this map's Q4/Cut 4, and it should be
  raised there rather than worked around downstream.

## 9. Known gaps in this map

Stated so Soul does not have to find them.

- Only **three** hulls generate a loadout at HEAD, so §0.4's per-ship measurement rests
  on a small sample. A real fight may be much heavier than 7 entities at 20
  evaluations/tick.
- The entity leak in §0.3 is proven from the code, not from a memory profile. O1 is where
  it becomes a measured fact.
- The per-tick evaluation table in §0.4 was counted from behaviour bodies by hand and
  applied to real loadouts by the probe. A behaviour whose `Execute` returns early most
  ticks is overcounted; `Thruster` and `Radiator` are the likely ones.
- Cut 3 leaves four instant draw sites spending directly for exactly one cut. That is a
  named, bounded split of authority, and if Cut 4 does not follow closely it becomes the
  thing this doctrine forbids.
- The add estimates in §4 are guesses for types that do not exist yet. They are there to
  be compared against reality in the postmortem, not to be trusted now.
