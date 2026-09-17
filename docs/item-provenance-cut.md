# Item Provenance: Schema Cut Map

Date: 2026-09-17

Status: Imagination pass, cut map. The ends are in `docs/item-provenance-target.md`; the
Body and the fork history are in `docs/item-provenance-substrate.md`. This map owns the
means of the **schema cut only**. Generation, station roster, recipes, routes, piracy and
factory tooling are the next scope and get their own map.

Progress (Self updates this in each cut's landing commit):
- Work lands on branch `codex/item-provenance`, cut from `7425ff1b`.
- **Cut A landed** at `69801bbb` and passed Soul with no findings. Evidence:
  - `Aetheria.Shared` and `AetherDb` build; 29 tests pass; the four negative greps
    are empty.
  - The open editor's recompile after the edit logged 0 `error CS`, which stands in
    for the batchmode compile.
  - Temperature and Armor now restore unconditionally. That is safe, because
    `Entity.MapEntity` initializes both before `Pack`.
- **Cut B landed** at `415f8e11`, `76c331e9` and `1e647953`. Soul's findings, awaiting a fix batch:
  - **F1, critical and pre-existing (also on `codex/cultcache-cutover`):** no run store
    holding an entity can be reopened.
    - Cause: `EntityPack.PersistedBehaviors` (`EntitySerializer.cs:200`) is keyed by
      `int2`, and `CultMessagePackSecurity` has no hash-resistant comparer for it.
    - Effect: after the first save, `AetheriaStores.Open` throws before the main menu.
    - It is the only failing key type among the persisted types.
    - The fix shape is an operator fork.
  - **F2:** the reachability test reads through `EntitySerializer.Items`, so dropping the
    Equipment, CargoBays, DockingBays or CargoContents roots survives.
  - **F3:** the single-tier fixture cannot fail `GetTier` or a re-roll on mint.
  - **F4:** `GetPrice`, the durability and thermal exponents, role fills, a `Produced`
    brand, and first-zone-only roots have no tests.
  - **F5:** `tests/mutation_tests.py` anchors assume LF, and it counts compile errors as
    kills.
  - **F6:** a stale `SavedGame.cs:124` comment.
  - With F1 bypassed in scratch, all 34 tests pass.
  - **F1 ruling (operator, 2026-09-17: "ruling A is fine"):** `PersistedBehaviors`
    becomes an array of `(int2 position, PersistentBehaviorData[] data)`, matching
    `Equipment` and `CargoBays`. It lands on this branch; `codex/cultcache-cutover`
    keeps the defect until this branch merges.
    - Recorded CultLib follow-up: CultCache should refuse, at type registration, a
      persisted dictionary key with no hash-resistant comparer, so a type that can be
      written but never read is loud on day one.
    - `docs/headless-playground-cut.md` Cut 0 is this same fix.
  - **Fix batch landed** at `e4df73e4`, `4fc9070b`, `cb2febec`, `561c5b85`, `080d3b49`
    and `c2ff5431`. Evidence:
    - 42 of 42 tests pass.
    - The mutation script ran on CRLF: the control stayed green and 16 of 16 mutants were
      killed.
    - **Soul passed the batch.**
      - A real-catalog probe reopened a committed run holding 46 generated instances.
      - Every persisted dictionary key is now `int` or `CultRecordRef`.
      - All seven GC-root mutants are killed, and 42 of 42 tests pass in a clean worktree.
    - **Follow-up (low):** one run of `mutation_tests.py` reported a transient `ERROR` on
      its last mutant, and a rerun was clean. Treat an `ERROR` as inconclusive and rerun.
      Record it again if it recurs.
  - **Persisted behaviors deleted** at `8fe09a23`, a pure deletion: −101/+4 lines, with
    key 5 retired and commented. Evidence:
    - 41 of 41 tests pass; `ReopenSurvivesAGeneratedShipWithEntities` was kept.
    - The negative grep is empty, and the Unity code has no references.
    - The mutation run: control green, 17 of 17 killed.
    - Self read the diff; because it is pure deletion, no separate Soul pass.
- **Cut C is next.** It needs Unity closed for the catalog rewrite and the batchmode
  compile.
  - **Open, found while fixing F1:** nothing implements `IPersistentBehavior`, so
    `PersistedBehaviors` has no production writer or reader. To exercise the wire
    shape, Hands added a `MarkerPersistentBehaviorData` union case. That is a production
    type that exists only for tests.
    - **Ruled: delete the feature (operator, 2026-09-17).** "eh, fine, get rid of it, I'm
      sure there's a better way to store it than the setup I had, anyway".
    - Deleted: `PersistedBehaviors` (key 5 retired), `IPersistentBehavior`,
      `PersistentBehaviorData`, `MarkerPersistentBehaviorData`, and the pack/restore
      code with its tests and mutants.
    - Behavior state that must persist gets designed later, with a real behavior as its
      first consumer.
    - The pairs-array ruling still stands as the shape for position-keyed persisted
      maps.

- Repo: `F:\Projects\Aetheria`, branch `codex/cultcache-cutover`, anchors against HEAD
  `b0df689d`. Commits since the substrate map (`dbe1dd83`) touch only the three docs, and
  every anchor used below was re-read at HEAD.
- CultLib pin: `a0813c6eed24d30bf88073ef615b633c77ebfcd6` (`Directory.Build.props:5`).
  Every headless command below takes `-p:CultLibRoot=<clean detached worktree at a0813c6>`.
- Baseline at HEAD, measured: `dotnet test tests/Aetheria.Shared.Tests` 29 passed;
  `AetherDb` `census`, `factions`, `station-fit`, `loadout 1` exit 0; `hardpoint-fit`
  exits 4 (already failing at HEAD, not this cut's to fix).

## 0. Rulings applied, and how they were read

Settled (operator, 2026-09-17), applied as written: F0 (a) placeholder origin; F1 (c)
one run-store ledger document with GC in `RunSave.Commit`; F4 (a) quality per lot per
role, stored; `ItemData.Manufacturer` leaves the design; branding derived, never stored;
factories are built in place (next scope; the `Produced` shape must be able to say it).

Readings this map makes, each checkable against the target's quotes:

- **`FactionProductData.Roles` and `ProductRole` stay.** The substrate's §11 cut them.
  The F0 ruling reverses that: generation "starts from branded items with specific role
  spreads" and synthesizes provenance "to hit those properties". A product's per-role
  mean/SD is that spread. It stays as the brand's authored generation input, read by the
  one lot-minting function. Not a fork.
- **The derived brand is shown** where the design's manufacturer was
  (`PropertiesPanel.cs:371-372`). It is the brand projection's only game reader; without
  it the projection has no reader.
- **Existing saves are discarded** still holds for the run store. `GameData/run.cc` does
  not exist in the working tree today. An old run store would fail loudly on Continue
  (`ProvenanceLedger` lookup of lot 0 throws); no gate or converter is added. The
  catalog is rewritten in Cut C and checked by `AetherDb` captures.
- Deleted per the substrate's defaults: the `instantiate` re-mint path and `TradeMenuDebug`.

## 1. Body re-verified; where it disagrees with the substrate map

All substrate anchors used here hold at `b0df689d` (`ItemInstance.cs:33-53`,
`ItemManager.cs:70-90,108-197`, `LoadoutGenerator.cs:35-110,189-269`,
`Loadout.cs:95-158`, `EntitySerializer.cs:59-137,179-196`, `Entity.cs:1085,1212-1225,1261,1290`,
`SavedGame.cs:96-124`, `AetheriaStores.cs:10`, `ActionGameManager.cs:264,499,540,735-739`,
`PropertiesPanel.cs:371-372`, `ItemData.cs:272,280-281`). `MainMenu.cs` clears the run at
`:112`, not `:111`.

Contradictions and corrections found in the Body:

1. **`TradeMenu.cs` and `ZoneRenderer.cs` need no edit.** The substrate's file list names
   them. Their readers are `GetPrice` and `GetTier`, whose signatures stay
   (`CraftedItemInstance` in); only the bodies move to the lot.
2. **`SimpleCommodity` gets no lot, and stacks need no lot rule.** The substrate put
   `Lot` on the `ItemInstance` base with a merge-within-lot rule at `Entity.cs:1498,1576`.
   Nothing mints a `SimpleCommodity` once `Instantiate` goes: the only caller of
   `CreateInstance(SimpleCommodityData, int)` is `ItemManager.cs:119`, and mining's mint is
   commented out (`Zone.cs:298-304`). A base field would have no writer. `Lot` goes on
   `CraftedItemInstance` at key 11, which `SimpleCommodity` does not use (it uses 2). Moving
   it to the base later with the same key is byte-identical (probe Q4), so mining yield
   adds without reshaping.
3. **`TradeMenuDebug` is dead:** its script GUID `6eed32eb39c04a31b81e8a55685b222c` appears
   in no `.unity`, `.prefab` or `.asset`, and no `.cs` names the type.
4. **Every one of 115 `ItemData` records carries slot 3 in its persisted schema**, not only
   the 37 with a manufacturer set. After removal, all 115 load with `ignored_extra_slot`
   until rewritten (probe Q1).
5. **The catalog has exactly one product per design** (37 products, 37 distinct designs, 0
   designs with more than one maker, 0 `(maker, design)` duplicates, 0 products with an
   unset maker, 0 authored role spreads, 0 designs with roles; probe Q1). The brand
   projection keyed by `(maker, design)` is unambiguous on today's data. The
   `LoadoutTests` fixture is not: `FirstAvailableProductInKeyOrderBuildsTheSlot`
   (`LoadoutTests.cs:289-312`) adds two same-maker Lamp products and reads `.Product`.
6. **`ItemManager` lives one scene load, and every run starts with a scene load.** It is
   constructed in `ActionGameManager.Start` (`:264`). There is one scene, `ARPG`. New Game
   and Continue both end in `SceneManager.LoadScene("ARPG")` (`MainMenu.cs:106,139,167`), and
   no `DontDestroyOnLoad` exists. So a ledger held by `ItemManager` spans exactly one run.
7. **Aside, not this cut:** `CreateInstance(CraftedItemData, float)` mints a
   `CompoundCommodity` for a `ConsumableItemData` design (`ItemManager.cs:136-141`), while
   `TryActivateConsumable` casts to `ConsumableItem` (`Entity.cs:336`). This map keeps
   that behaviour byte for byte in the new mint primitive and does not fix it silently.

## 2. Probes

Source: `C:\Users\Meta\AppData\Local\Temp\claude\F--Projects-Aetheria\2305971e-ad74-41e1-a7cb-2181a612c355\scratchpad\provenance-probe\`.
It was run 2026-09-17 against a detached scratch worktree of Aetheria at `b0df689d` with
`ItemData.cs:280-281` deleted, and CultLib `a0813c6`. The catalog was a copy of
`GameData/Aetheria.cc` (sha1 `4d08859552dbd864ad13363179fd4bd97b567e4a`, 12,686,267 bytes).
Both worktrees were removed afterwards; re-running needs that worktree recreated with the
two-line deletion.

| Id | Question | Result |
|---|---|---|
| Q1 | Does the real catalog open read-only with `ItemData` key 3 removed? | Opens. `LastSchemaMigrationReports`: 180 reports, 115 with `IgnoredExtraSlots` = {3}. 115 items, 102 crafted, 37 products; duplicate/maker/role counts as in §1.5. |
| Q2 | Does a `[CultGlobal]` ledger holding `Dictionary<int, Lot>` with a nested `[Union]` origin and `CultRecordRef` fields round-trip in the run store, committed in one batch with a `SavedZone`? | `GetGlobal` before any write: null. After commit and reopen: `next 4; lots 3; key global:sha256:8660…; zones 1`, all three cases intact (`Attributed` faction, `Produced` station 12 facility 1 inputs 3, `Extracted` zone 4). `batch.Remove(key)` then reopen: null. |
| Q2' | Can `Produced.Station` be a `CultRecordKey`? | No: `FormatterNotRegisteredException: GameCult.Caching.CultRecordKey is not registered in resolver` at commit. `int` works (Q2). See fork N1. |
| Q3 | Does one writable commit re-upserting every `ItemData` clear the drift? | `rewrote 115`; bytes 12,686,267 -> 12,683,061; reopen: 0 reports with ignored slots, 115 items. |
| Q4 | Does a key-11 field on a subclass serialize identically to the same key on the base? | `equal True len 14`. |

Not probed: CultCache Studio's rendering of the ledger (operator check); Unity compile
(batchmode in each cut).

## 3. Authority map

**Lot properties and provenance.**
- Owner: `ProvenanceLedger`, one run-scoped `[CultGlobal]` document. In play, the live
  instance is `ItemManager.Lots`.
- Inputs: the lot-minting functions `ItemManager.CreateLot(...)`, which read the product
  (maker, design, `Roles` spreads) or the console's design, plus `ItemManager.Random`
  and `GameplaySettings.Tiers`.
- Outputs: `Lot { Design, Origin, Quality, Roles }` by `LotId` (an `int`, first id 1, never
  reused within a run).
- Derived state:
  - `CraftedItemInstance.Data` is no longer an independent value. It is copied from
    `Lot.Design` by the one instance mint, `ItemManager.CreateInstance(int lot)`, and
    stays on the instance because `ItemsOfType`, stacking and every `GetData` key off it.
  - Stats, wear, price and tier are derived from the lot through `ItemManager.GetLot`.
  - `EquippedItem` and `ConsumableItemEffect` cache the `Lot` reference at construction.
    That is safe because lots are immutable and the live ledger is never pruned.
  - Brand is derived by `ItemManager.Brand` and never stored.
- Forbidden writers:
  - `CraftedItemInstance.Quality`, `.Ingredients` and `.Product`: deleted.
  - `LoadoutGenerator.CopyBuild`: deleted; a matching hardpoint mints from the same lot.
  - `ItemManager.Instantiate` and `Unpack(instantiate)`: deleted, because they re-rolled
    quality and dropped provenance.
  - `ItemData.Manufacturer`: deleted.
  - Nothing outside `ItemManager.CreateLot` constructs a `Lot` or assigns its fields.
    Tests may construct lots directly.
- Shared paths: NPC and station generation (`LoadoutGenerator`), preset materialization
  (`Loadouts.Materialize`), starting ship and `spawnturret` (the generator, no re-mint)
  and console `give` all mint through `CreateLot` -> `CreateInstance(lot)`. Loot, buying
  and pickup move instance values; their lot rides along.
- Deletion line: in Cut B, the three instance fields and `CopyBuild` go before the ledger
  gains a reader.

**Ledger persistence and GC.**
- Owner: `RunSave`.
- Inputs: the live ledger and the `SavedZone`s being committed.
- Outputs:
  - `RunSave.Lots(cache)` is the only reader of the stored ledger. It returns the stored
    global, or a new empty ledger when there is none.
  - `RunSave.Commit(cache, game, zones, lots)` writes `lots.Reachable(roots)` in the same
    single-store batch as the zones and the game.
  - `RunSave.Clear` removes the ledger through `RunTypes`.
- GC roots: every `CraftedItemInstance` in every committed `SavedZone.Contents.Entities`.
  An entity yields its hull, equipment, cargo bays, docking bays, cargo and docking-bay
  contents, and its `Children`, recursively (`EntitySerializer.Items(pack)`). Closure
  follows `Produced.Facility` and `Produced.Inputs`. Zones never generated have no
  contents and contribute nothing.
- Deliberately not roots: floor `ItemPickup`s. `PackZone` never captures them
  (`Zone.cs:108-119`), so they vanish on reload anyway, and their lots go with them.
- Invariants:
  - GC prunes **only the written copy**. The live ledger keeps unreachable lots, because a
    floor pickup can still be collected after a save.
  - The written copy keeps `NextLot`, so ids are never reused within a run.
  - A root naming an absent lot throws. That is corruption, and it must be loud.
- Forbidden writers: nothing else calls `GetGlobal<ProvenanceLedger>` or upserts one.
  `ZoneGenerator`'s staged-upsert pattern (`ZoneGenerator.cs:99-108`) is not used for
  lots: the ledger is live simulation state, captured at commit like entities.
- Lifecycle: the run begins with a scene load (§1.6), whose `Start` reads
  `RunSave.Lots(CultCache)`. New Game has already cleared the run store (`MainMenu.cs:112`),
  so it gets an empty ledger. Continue gets the stored one. Death clears the run store
  (`ActionGameManager.cs:1090`); the next run reloads the scene.

**Branding.**
- Owner: `ItemManager.Brand(CraftedItemInstance)` -> `(Faction Maker, FactionProductData Product)`.
- Inputs:
  - The lot origin's faction: `Attributed.Faction` or `Produced.Faction`; `Extracted` has none.
  - `instance.Data`.
  - The catalog's `FactionProductData`.
- Rule: `Product` is the first product in record-key order (`StringComparer.Ordinal`, the
  order `Loadout.cs:99` already uses) whose `Manufacturer` is the maker and whose `Design`
  key is the instance's design. It is null when the maker is unset or makes no such product.
- Stays authored in the catalog: `FactionProductData` (`Name`, `Description`, `Design`,
  `Manufacturer`, `Roles`). It is the brand catalog.
- Rule until segment bands exist (next scope): at most one product per `(maker, design)`.
  `census` lists violations (§1.5 shows none today).

## 4. Adds, by rule

New file `Assets/Scripts/ServerShared/Provenance.cs`. It needs no csproj edit: it falls
under `Aetheria.Shared.csproj`'s `ServerShared\**` glob and the Unity asmdef folder. Unity
generates its `.meta`, which is committed.

- `ProvenanceLedger`: `[CultDocument("aetheria.provenanceledger", "1"), CultGlobal, MessagePackObject]`.
  - Key 0 `int NextLot = 1`; key 1 `Dictionary<int, Lot> Lots`.
  - `int Add(Lot)` assigns and returns `NextLot++`.
  - The indexer `Lot this[int]` throws `InvalidOperationException` naming the id when it
    is absent. It has no try-variant.
  - `ProvenanceLedger Reachable(IEnumerable<int> roots)` returns a new ledger: same
    `NextLot`, the closure over `Produced.Facility`/`Inputs`, and the same `Lot` objects.
    It never mutates `this`.
- `Lot`: `[MessagePackObject]`.
  - Key 0 `CultRecordRef<ItemData> Design`. `ItemData`, not `CraftedItemData`, so an
    `Extracted` commodity lot needs no reshape.
  - Key 1 `Provenance Origin`; key 2 `float Quality`; key 3 `List<RoleFill> Roles`.
  - `QualityForRole(string)` moves here from `ItemInstance.cs:47-53`, with the same
    fallback to `Quality`.
- `RoleFill` moves from `ItemInstance.cs:56-62` unchanged (keys 0, 1).
- `Provenance`: abstract, with `[Union]` only and no `[MessagePackObject]` (MsgPack005;
  precedent `ItemInstance.cs:18-23`).
  - `Union(0) Attributed { Key 0 CultRecordRef<Faction> Faction }`: the F0 placeholder.
    Faction only; the generator deletes it.
  - `Union(1) Produced { Key 0 CultRecordRef<Faction> Faction; Key 1 int Station; Key 2 int Facility; Key 3 int[] Inputs }`.
  - `Union(2) Extracted { Key 0 int Zone; Key 1 CultRecordRef<SimpleCommodityData> Commodity }`.
    `Zone` is the run's zone index, the identity `savedzone-{i}` already uses.
- `CraftedItemInstance.Lot`: `[JsonProperty("lot"), Key(11)] int`. Keys 2, 9 and 10 are
  retired, not reused; the comment follows the `ItemData.cs:303` precedent.
- `ItemManager`:
  - Constructor `(CultCache, ProvenanceLedger lots, GameplaySettings, Action<string>)`
    with a required ledger, and `ProvenanceLedger Lots { get; }`.
  - `Lot GetLot(CraftedItemInstance)`.
  - `float RollQuality()`: the tier roll body of `:151-159`, unchanged.
  - `int CreateLot(FactionProductData)`: the maker as `Attributed`, `RollQuality()`, and a
    role spread per design role exactly as `:175-185`.
  - `int CreateLot(CraftedItemData, CultRecordRef<Faction> maker, float quality)`: no roles.
  - `CraftedItemInstance CreateInstance(int lot)`: the type branch of `:128-141`, with
    `Data = lot.Design`, `Lot = lot`, and durability from the design.
  - `CraftedItemInstance CreateInstance(FactionProductData)` stays as the convenience:
    null plus a log when the design is missing (as `:166-171`), otherwise
    `CreateInstance(CreateLot(product))`.
  - `Brand(...)` as in §3.
- `RunSave.Lots(CultCache)`; `RunSave.Commit` gains `ProvenanceLedger lots`.
- `EntitySerializer.Items(EntityPack)`: the GC root walk (§3), a static enumerator next to
  `Pack`/`Unpack`.

### Fields with no reader or writer in this cut

Named by rulings, so kept, and flagged as instructed:

- `Produced` in full (`Faction`, `Station`, `Facility`, `Inputs`) has no writer.
  - Its readers in this cut: `Brand` reads `Faction`, and GC closure reads
    `Facility`/`Inputs`. Both are exercised by tests that build `Produced` lots directly.
  - `Station` has no reader at all.
  - It can say "built in place at its station, from inputs": a facility lot is a
    `Produced` lot whose `Station` is its own station and whose `Inputs` are `Extracted`
    lots there. Retooling cost is a factory-tooling concern that adds a document or
    union case and changes no field here.
- `Extracted` in full has no writer. Its only reader is GC closure, which treats it as a
  leaf.
- `Lot.Design` on lots with no instance: none exist until generation, so no reader.

Not in the cut, because no ruling names them and nothing reads them: segment quality
bands, `Founded`, `Seized`, station roster, source, route and shipment records, recipes
or role input specs, a station id field on `OrbitalEntityPack`, and tooling state.

## 5. Cuts

Three cuts, in order. A and C are subtractions Soul can falsify alone; B is the
behaviour change.

### Cut A: delete the re-mint path and the dead debug menu

Subtraction. The one behaviour change is the scar fix: the starting ship and `spawnturret`
keep what the generator built.

- Deletes:
  - `Assets/Scripts/UI/Menu/TradeMenuDebug.cs` (472 lines) and its `.meta`.
  - `ItemManager.cs:92-106` `CreateInstance(SimpleCommodityData, int)` (15; its only caller
    is `:119`).
  - `ItemManager.cs:108-124` `Instantiate` (17).
- `EntitySerializer.cs`:
  - Remove the `instantiate` parameter from `:59,65-66,72,76,83,86,93,99`.
  - `:75,85,103-105,116,121` pass the pack's own items.
  - Delete the guards at `:133` and `:136`, so `Temperature` and `Armor` are restored
    unconditionally. That is what every non-instantiate caller already gets.
- `ActionGameManager.cs:540` and `:735-739`: drop the `true` argument. The comment at
  `:740` names a `Loadouts.First(... StarterShipTemplate), true` call that no longer
  exists; delete it.
- Verification:
  - `dotnet build Aetheria.Shared/Aetheria.Shared.csproj -p:CultLibRoot=<wt>` and
    `dotnet build tools/AetherDb -p:CultLibRoot=<wt>` are green.
  - `dotnet test tests/Aetheria.Shared.Tests -p:CultLibRoot=<wt>`: 29 passed.
  - Unity batchmode compile (§6) has no `error CS`.
  - Negative:
    - `rg -n "instantiate|\.Instantiate\(" Assets/Scripts/ServerShared/EntitySerializer.cs Assets/Scripts/ServerShared/ItemManager.cs` is empty.
    - `rg -n "EntitySerializer\.Unpack\([^)]*, true\)" Assets` is empty.
    - `rg -n "TradeMenuDebug|6eed32eb39c04a31b81e8a55685b222c" Assets` is empty.
    - `rg -n "SimpleCommodityData item, int count" Assets` is empty.
  - Soul reads, because no test covers it: `GenerateShipLoadout` and
    `GenerateTurretLoadout` pack immediately after construction and outfitting
    (`LoadoutGenerator.cs:46-49,61-64`). Restoring `Temperature` and `Armor` from those packs
    reinstates the generator's own arrays. If any `TryEquip` in `Restore` recomputes armor
    differently, the starting ship differs; the play smoke covers it.
- Ledger: about -516 +0 (plus `.meta`).

### Cut B: lots, ledger, GC, stats through the lot, derived brand

- Deletes:
  - `ItemInstance.cs:35-53`: `Quality`, `Ingredients`, `Product`, `QualityForRole` (19).
  - `ItemInstance.cs:56-62`: `RoleFill`, moved.
  - `ItemManager.cs:126-187`: the two design overloads and the product overload body,
    replaced by the §4 set.
  - `LoadoutGenerator.cs:262-269`: `CopyBuild` (8).
- Per-file changes:
  - `ItemInstance.cs:33-54`: `CraftedItemInstance` holds only `Lot` (key 11) and the
    retired-keys comment.
  - `ItemManager.cs`:
    - `:27-32`: the constructor takes the ledger.
    - `:70-84` `Evaluate`: `var lot = GetLot(item)`; `lot.QualityForRole(stat.FromRole)`
      at `:73`, `lot.Quality` at `:77`.
    - `:86-90` `GetPrice`: `GetLot(item).Quality`.
    - `:189-197` `GetTier`: the same.
    - Add `GetLot`, `RollQuality`, both `CreateLot`s, `CreateInstance(int)` and `Brand`.
  - `Entity.cs`:
    - `ConsumableItemEffect` constructor `:1056-1066` caches `Lot`; `:1085` reads
      `Lot.QualityForRole`.
    - `EquippedItem` constructor `:1210-1225` caches `Lot = ItemManager.GetLot(item)` and
      reads `Lot.Quality` at `:1221,1225`.
    - `:1261` reads `Lot.QualityForRole`; `:1290` reads `Lot.Quality`.
    - `EquippableItem` is assigned only at `:1215`, so the cache cannot go stale.
  - `LoadoutGenerator.cs:210-226`: when `previousItem != null`, mint
    `ItemManager.CreateInstance(previousItem.EquippableItem.Lot)`; otherwise
    `CreateInstance(entry.product)`. Rewrite the comment at `:219` as "same lot". The
    `Random` draw sequence of the lot rolls shifts, because the second unit no longer
    rolls; generator selection uses `LoadoutGenerator.Random` and is unaffected.
  - `Loadout.cs:89-94` comment: a failed materialization leaves minted lots in the live
    ledger, and the next commit drops them. `:129,135` are unchanged.
  - `SavedGame.cs`:
    - Add `RunSave.Lots`.
    - `:96-111` `Commit(cache, game, zones, lots)`: compute roots from
      `zones[i].Contents?.Entities` through `EntitySerializer.Items`, then
      `batch.Upsert(lots.Reachable(roots))` in the existing batch.
    - Update the comments at `:52,93-95,113` (the ledger is the fifth run record).
  - `AetheriaStores.cs:10`: `RunTypes` adds `typeof(ProvenanceLedger)`.
  - `EntitySerializer.cs`: add `Items(EntityPack)`.
  - `ActionGameManager.cs`:
    - `:264`: `new ItemManager(CultCache, RunSave.Lots(CultCache), Settings.GameplaySettings, Debug.Log)`.
    - `:247`: `RunSave.Commit(CultCache, game, zones, ItemManager.Lots)`.
    - `:499` `give`: `ItemManager.CreateInstance(ItemManager.CreateLot(item, default, .95f))`.
      An unset maker means `Brand` returns no maker and no product.
  - `PropertiesPanel.cs:371-372`: for a `CraftedItemInstance`, a "Manufacturer" stat reads
    `Brand(...).Maker.Name`, and is omitted when there is no maker; the "GameCult" fallback
    goes. When `Product` is non-null, add the product `Name` and `Description` as
    properties. This is the first flavour text the player sees. A `SimpleCommodity` shows
    no brand. `ItemData.Manufacturer` is still read here until this edit; Cut C deletes
    the field.
  - `tools/AetherDb/Program.cs`:
    - `:395` passes `new ProvenanceLedger()`.
    - `census` adds a "`(maker, design)` with more than one product" section, the Brand
      rule's check.
    - `save` (`:353-378`) reads `RunSave.Lots(db.Cache)` and prints, per run, the lot
      count, crafted instances, instances whose lot is absent, and instances whose `Data`
      differs from `Lot.Design`. This is the only headless check of a real played save.
- Tests. Each is paired with the mutation that must kill it; Soul applies each mutation
  and sees red.
  - Every `new ItemManager(` in tests passes `new ProvenanceLedger()` (10 in
    `LoadoutTests`, 1 in `RunSaveTests`). `RunSaveTests.cs:43,60,61,76,86,94,112` pass a
    ledger to `Commit`. The record counts in `RepeatedSavesKeepRecordCountConstant`
    (`:48-49`) and `ClearRemovesEveryRunRecord` rise by one for the ledger global.

| Test | File | Asserts | Mutations that must kill it |
|---|---|---|---|
| `MaterializedLotsSurviveSaveAndReload` | `LoadoutTests` | `Materialize` a `HandBuilt` ship; pack it into a zone; `Commit` via `Open()`; reopen. Every crafted instance resolves in `RunSave.Lots`, `Data == Lot.Design`, the origin is `Attributed` with the product's maker, and `Quality` equals the pre-save lot's. | `Commit` omits the ledger upsert; `CreateInstance(int)` leaves `Lot` 0; `CreateLot(product)` writes `default` faction; `ProvenanceLedger` absent from `RunTypes` (the commit throws unrouted) |
| `CommitKeepsOnlyReachableLots` | `RunSaveTests` | Hand-built ledger. Lot 1 is on a hull. Lot 3 is `Produced { Facility 4, Inputs [5] }` in a docked child ship's docking-bay contents. Lots 2 and 6 are unreferenced. `NextLot` is 7. After commit and reopen, the stored lots are {1, 3, 4, 5} with `NextLot` 7, and the live ledger still holds 2 and 6. | `Reachable` returns `this`; closure skips `Inputs` or `Facility`; `Reachable` prunes in place; the written copy resets `NextLot`; `Items` skips `Children` or `DockingBayContents` |
| `MissingLotIsLoud` | `RunSaveTests` | `new ProvenanceLedger()[0]` throws; `Reachable(new[] { 9 })` throws | the indexer returns null or default; `Reachable` skips missing roots |
| `MatchingHardpointsShareALot` | `LoadoutTests` | A fixture hull, 4x4 with two identical Sensors hardpoints, plus a capacitor gear and product (`FillInterior` requires one, `LoadoutGenerator.cs:250-253`). `GenerateShipLoadout` with galaxy null: both hardpoint units carry the same `Lot`. | the matching-hardpoint branch calls `CreateInstance(entry.product)` |
| `FirstAvailableProductInKeyOrderBuildsTheSlot` (rewritten, `:289-312`) | `LoadoutTests` | `lamp-z` and `lamp-a` get two new makers; each case asserts `items.Brand(unit).Product.Name` | `Brand` ignores the maker; `Materialize` drops key order |
| `StatsReadTheLot` | `LoadoutTests` | A Lamp lot with `Quality .2` and `Roles [lens .9]`: `Evaluate` with `FromRole "lens"`, `Min 0`, `Max 1`, `QualityExponent 1` gives .9; with no role it gives .2; `GetTier` reads .2 | `Evaluate` reads `Quality` for a role stat; `QualityForRole` falls back wrongly |

- Verification:
  - The three headless commands of Cut A are green, with 34 tests (29 + 5 new, 1 rewritten).
  - Unity batchmode has no `error CS`.
  - `AetherDb loadout 1` output equals the HEAD capture (0 hulls failed); only the
    quality roll sequence changed.
  - `AetherDb census` equals HEAD, plus the empty duplicate section.
  - Negative greps:
    - `rg -n "\bIngredients\b|\.Product\b|CopyBuild|item\.Quality\b|EquippableItem\.Quality\b|Item\.QualityForRole|EquippableItem\.QualityForRole" Assets/Scripts tools tests` is empty.
    - `rg -n "new Attributed" Assets/Scripts tools` matches only `ItemManager.cs`, the
      site the generator cut replaces.
    - `rg -n "GetGlobal<ProvenanceLedger>" Assets tools tests` matches only
      `SavedGame.cs`.
    - `rg -n "new Lot\b|\.Origin =|\.Roles\.Add" Assets/Scripts` matches only
      `ItemManager.cs`.
    - `rg -n "new (EquippableItem|CompoundCommodity|ConsumableItem)\b" Assets/Scripts tools`
      matches only `ItemManager.CreateInstance(int)`, the one writer of a crafted
      instance's `Data`. Tests may construct instances by hand.

### Cut C: remove the design's manufacturer and rewrite the catalog

Subtraction, plus a data rewrite.

- Deletes: `ItemData.cs:280-281` (`Manufacturer`, key 3), with a one-line retired-key
  comment.
- Readers switch:
  - `tools/AetherDb/Program.cs:54`: census makers come from the product selling each
    design, and "(none)" for unsold designs.
  - `AetheriaStoresTests.cs:32` writes a `FactionProductData { Design = Lance, Manufacturer = faction }`.
  - `:109` asserts `Assert.Same(faction, cache.Get(cache.GetAll<FactionProductData>().Single().Manufacturer))`.
- Catalog rewrite:
  - Unity and CultCache Studio must be closed; the operator's editor holds no catalog
    handle.
  - Hands runs a scratch console, not committed. It is the body of probe Q3: open
    `GameData/Aetheria.cc` writable through `AetheriaStores.Open(..., catalogWritable: true)`
    and re-upsert every stored `ItemData` with its own key in one `Commit`.
  - Expected: 115 rewritten, 12,686,267 -> 12,683,061 bytes, and 0 schema reports with
    ignored slots on reopen.
  - Commit the binary. The CultLib worktree is clean before and after.
- Verification:
  - The headless commands are green (34 tests); Unity batchmode has no `error CS`.
  - Negative: `rg -n "Manufacturer" Assets/Scripts/ServerShared/ItemData.cs` is empty.
  - Negative: `rg -n "(data|item|design)\.Manufacturer" Assets/Scripts tools tests` is
    empty; product `.Manufacturer` readers remain by design.
  - AetherDb captures before and after the rewrite: `census`, `factions`, `dangling`,
    `station-fit`, `loadout 1` and `hardpoint-fit`.
    - Equal: `factions`, `dangling` (0 dangling refs), `station-fit`, `loadout 1`, and
      `hardpoint-fit` (still exit 4, identical text).
    - Differs by design: `census` per-kind maker lists, now from products, so the 14
      unsold designs show "(none)".
    - Any other diff is a defect.
- Ledger: -3 +2 C#; catalog -3,206 bytes.

## 6. Build budget and Unity

- Packages touched: `Aetheria.Shared` (netstandard2.1), `tools/AetherDb` (net10.0), and
  `tests/Aetheria.Shared.Tests` (net10.0), Debug only.
- CultLib `src/GameCult.Caching`, `GameCult.Caching.MessagePack` and `cultmath` are
  referenced at `a0813c6`; none is modified.
- No new project, package or target; one new source file.
- Build host: this Windows workstation. Target: the same .NET SDK for the headless check,
  and Unity 6000.3.24f1 Windows editor for `Assembly-CSharp`.
- Unity batchmode, per cut, detached:
  - First check `Get-Process Unity -ErrorAction SilentlyContinue` and
    `Test-Path F:\Projects\Aetheria\Temp\UnityLockfile`. If the editor is open, stop and
    ask the operator to close it; do not kill it.
  - Then `Start-Process "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe" -ArgumentList '-batchmode -nographics -quit -projectPath F:\Projects\Aetheria -logFile <scratch>\unity-cut-X.log' -PassThru`.
  - Record the PID, and poll `Get-Process -Id <pid>` and
    `Select-String -Path <log> -Pattern "error CS|Exiting batchmode"`.
  - Pass: exit, no `error CS`. Commit the generated `Provenance.cs.meta` in Cut B.
- Temporary CultLib worktree: `git -C F:\Projects\CultLib worktree add --detach <scratch>\cultlib-a0813c6 a0813c6eed24d30bf88073ef615b633c77ebfcd6`.
  Remove it with `git worktree remove` after the pass. Do not touch the other five CultLib
  worktrees.

Operator-only checks after Cut C:
- New Game and fly through one zone. The starting ship's items show tier colour, and the
  properties panel shows Manufacturer, product name and flavour text for branded gear.
- Dock and buy an item: its brand still shows in your cargo.
- Kill an NPC and pick up loot: brand shown.
- Warp through a wormhole (a save), quit, Continue: the same items with the same tier and
  brand.
- Then the agent runs `AetherDb save`: 0 absent lots, 0 design mismatches.
- `give Lamp` or any design name: the item shows no Manufacturer row.
- Open `GameData/run.cc` in CultCache Studio. The `aetheria.provenanceledger` global
  renders its `Lots` dictionary with `Attributed` origins and per-role lists. If the Studio
  cannot render an `int`-keyed dictionary of unions, that is a Studio defect to raise, not
  a schema change.
- Die: Continue is disabled and `run.cc` holds no ledger (`AetherDb save`: "run store
  holds no SavedGame", and no ledger).

## 7. Forks

**N1. `Produced.Station` identity type.** Low-blocking. The field has no writer until the
generator, so any answer can be retyped then with nothing on disk to migrate.
- (a) `int`, a run-scoped station id. This mirrors the ruled ledger shape (F1 (c):
  run-scoped integer ids in one global document) and F3's recommended skeleton document,
  and it round-trips today (Q2).
- (b) The station roster's record key as a string. It is wire-identical to a future
  `CultRecordRef<StationRecord>` (`CultRecordRefFormatter` writes the key string), but it
  pre-commits F3 to one run record per station. `CultRecordKey` itself has no formatter
  (Q2'), so it would be an untyped `string` now.
- (c) Leave `Produced` out until the generator. A union case is additive and reshapes
  nothing, but the schema then no longer shows "who, where, from what", which the
  operator asked the cut to show.
- **Recommend (a).** The cut is written under (a); (b) changes one field type and (c)
  deletes one class.

Recorded defaults, not asked:

- **The live ledger's owner is `ItemManager`, not `Galaxy`.** `ItemManager` spans exactly
  one run (§1.6). The ruling routes stat readers through it. Fixtures and `AetherDb` build
  an `ItemManager` with no galaxy.
- **`Lot` goes on `CraftedItemInstance` at key 11**, not on the base (§1.2, Q4).
- **`LotId` is a bare `int`,** not a typed struct. A struct dictionary key needs a
  hash-resistant comparer under `UntrustedData`: `CultMessagePackSecurity` handles only
  `CultRecordRef<T>` (`CultMessagePackSecurity.cs:14-21`), so a struct key would need a
  CultLib change.
- **The catalog is rewritten** by a throwaway console rather than left drifting, or saved
  through Studio, which is last-writer-wins over the whole file.
- **Console `give` writes `Attributed` with an unset faction.**
- **Duplicate `(maker, design)` products resolve by record-key order,** and `census` flags
  them.

## 8. Subtraction ledger

Measured where a range is named; bounded estimates otherwise.

| Cut | Removed | Added | Targets, schemas, stores |
|---|---|---|---|
| A | `TradeMenuDebug.cs` 472 + `.meta`; `ItemManager` 32; `EntitySerializer` ~12 (param threading, two guards); `ActionGameManager` 2 args + 1 comment | 0 | -1 MonoBehaviour; 0 targets |
| B | `ItemInstance` 26 (19 fields and method, 7 moved `RoleFill`); `ItemManager` ~62 (`:126-187`); `CopyBuild` 8; `PropertiesPanel` 2 | `Provenance.cs` ~90; `ItemManager` ~45; `RunSave` ~12; `Items` ~15; `Entity` ~6; `LoadoutGenerator` ~3; `PropertiesPanel` ~8; `AetherDb` ~20; tests ~170 | +1 run schema `aetheria.provenanceledger` (global); `CraftedItemInstance` keys 2, 9, 10 retired, key 11 added; the run store gains 1 record |
| C | `ItemData` 2; census 1 | census ~4; stores test fixture ±2; 1 retire comment | `ItemData` key 3 retired in the 7 concrete schemas that hold records; the catalog loses slot 3 on 115 records (-3,206 bytes) |

Net source excluding tests: about -620 / +200. Tests: about +170. `TradeMenuDebug` is
most of the deletion; without it the provenance change is close to net +100. That buys a
typed provenance graph, derived brand display and per-lot stats, and it removes three
per-unit fields and two re-mint paths.

## 9. Risks and rejected paths

Risks:
- **Continue with an old run store throws** on the first lot lookup. That is accepted
  under "saves are discarded". No `run.cc` exists in the tree today; the operator
  deletes any stray one before the smoke.
- **The live ledger grows** by unreachable lots within a session: failed
  materializations, consumed consumables, destroyed ships' unlooted gear. That is bounded
  by what generation mints: the substrate probe counts about 16k instances for a whole
  galaxy, and `CopyBuild` sharing lowers the lot count. It is pruned on every write.
- **The ledger is one document.** The single-file store already rewrites its whole view
  per commit (substrate §4), so the ledger adds bytes, not a new rewrite pattern.
- **Studio rendering of `Dictionary<int, Lot>` with union values** is unprobed; it is an
  operator check.
- **Cut A's unconditional `Temperature`/`Armor` restore** is argued from source, not
  tested.

Rejected paths:
- Staging lots into the cache as zone generation stages orbits. That would make the cache
  the live owner and let `Clear` race a live `ItemManager`; lots are simulation state,
  captured at commit.
- Pruning the live ledger at commit, which would break pickups collected after a save.
- A nullable lot or a "no provenance" branch in readers (F0 (b)).
- Keeping `Product` on the lot "for display": that is branding stored.
- A save-format version gate for old run stores.
- A typed `LotId` struct.
- A committed `AetherDb` catalog-rewrite command for a one-time rewrite.
- Moving `Data` off the instance so the lot is its only design authority. Every
  `ItemsOfType`, stacking and `GetData` path keys off `ItemInstance.Data`, and
  `SimpleCommodity` has no lot. Instead, `Data` is written only from `Lot.Design` at mint,
  and `AetherDb save` checks the equality.
