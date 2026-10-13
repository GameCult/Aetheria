# Aetheria electronic warfare: map

Campaign `aetheria-ew`. Target: `docs/aetheria-ew-target.md` (typed `aetheria-ew:target:r2`). Written by Imagination
(agent imagination-aeth-ew-r2, session self-2026-10-13-ag). Body facts, model-page corrections and the cut
sequence not yet mapped live here; cut specs, rulings and questions are documents in the mind.

## Body facts

Probed at `origin/master` `1779ca97` (code identical to `0894aab1`, which carries emissions-heard and
reveals-replace-tiers).

| # | Question | Result |
|---|---|---|
| H1 | Who writes `LaunchFlash`? | Only `InstantWeapon` (InstantWeapon.cs:280, once per shot) and `ConstantWeapon` (ConstantWeapon.cs:173, every tick while a beam fires), both through `Entity.SetSignature` (Entity.cs:98-103). Launcher.cs and Weapon.cs write none. Grep `SignatureChannel.LaunchFlash` over `Assets/Scripts` outside tests names exactly those two writers, the enum (Sensing.cs:14) and nothing else. |
| H2 | Does order of execution between a weapon and a suppressor matter? | Yes if the suppressor pushes state: items execute in `_orderedEquipment` order (Entity.cs:1746), a weapon may fire before the suppressor's `Execute`. So the suppressor is pulled, not pushed: `SetSignature` asks each equipped `LaunchSuppressor` at write time (below). No per-tick suppression state exists to order or to go stale. |
| H3 | How does gear find sibling behaviours on its ship? | `Entity.GetBehaviors<T>()` (Entity.cs:1618) iterates `Equipment` and each item's `Behaviors`. Consumable-hosted behaviours are not in it, so a suppressor is equipped gear only. |
| H4 | How does a behaviour pay heat into its own cell? | `Behavior.AddHeat` (Behaviors.cs:52) is `Item?.AddHeat`, which splits the heat over the item's `InsetShape` cells through `Entity.AddHeat(cell, heat)` (Entity.cs:2221-2226, :1212). A gear with the fixture's `Shape = new Shape()` has no cells; a test that asserts heat in the item's cell needs a gear with a solid 1x1 shape (probe before use). |
| H5 | What stops an item running when too hot? | `EquippedItem.Active` = `Enabled` and `Online`; `Online` = `ThermalOnline` and `DurabilityOnline` (Entity.cs:2071-2074, :2241-2242). `Radiator.Execute` returns early below its floor (Radiator.cs:103-117). A rated temperature is therefore a behaviour stat compared with `Behavior.Temperature` (the mean of the item's cells, Entity.cs:1968-1974), as the radiator compares its own. |
| H6 | What makes a flash a reveal? | `Entity.Sense` (Entity.cs:996-1000) records a reveal when a signature's `Strength` times the observer's `PassiveInfo` at range exceeds `TargetDetectionInfoThreshold`. Scaling the strength at write therefore decides the reveal with no change to `Sense`. The flash also feeds `Visibility` (the sum of sources), so a suppressed launch thins the ship's track too. |
| H7 | Next free `BehaviorData` union key? | Corrected at 0898725f: `LaunchSuppressorData` landed as 49 (Behaviors.cs:193), because `PickupData` took 48 (:192) first; 50 is the next free key. Never reuse a retired key (12, 26). `BehaviorData.Group` is `Key(0)`; a new data type's own fields start at `Key(1)`. |
| H8 | Where does a new gear design reach the catalog? | `tools/AetherDb/Program.cs`: one-shot authoring commands (`pd-gear`, `consumables-first-set`) write a design plus `FactionProductData` rows per maker with `MakerProfiles` (Program.cs:244-249) giving the quality mean and deviation. Makers are the factions `Zhestokost`, `AU`, `Lightsail`, `R&D`, `Lucent`, `NiteLife`, `Finch`, `Alakrita`, `DME`, `Adrasteia`. |
| H9 | Test fixture that already builds ships, fires guns and observes reveals? | `ContactRevealTests` (ContactRevealTests.cs:27-232, a `partial` class also extended by EmissionTests.cs): its constructor authors catalog gear, `Add` builds a ship, `Fire` fires a gun and ticks until its light shows, `Tick`, `RevealsOf`. Gun visibility 5, `TargetDetectionInfoThreshold` .1, `VisibilityDecay` 100. |

## Model page: corrections to the target's table

- **Channel suppression** (target row): not stored. It is derived on every `SetSignature` call by asking the
  ship's equipped `LaunchSuppressor` behaviours, in equipment order, for the factor of the channel being written
  (H2). There is no `(ship, channel)` factor field to write, order or let go stale. The suppressor's authority
  is unchanged: only the suppressor's behaviour decides the factor and pays the heat; weapons and observers do not.
- **Suppressor EW gear stats** (inside the target's "EW gear stats" row): `LaunchSuppressorData` carries the factor,
  the heat paid per unit of strength removed, and the rated temperature, all `PerformanceStat`s so quality moves
  them like other gear.
- **Contact** and **Belief** (target rows): in cut 2, `Contact.Profile` is derived on read from the subject's
  `ClaimedHull` once `Identified` and is null below it; no belief is stored. The Belief row (stored, with
  `SeenThrough`) begins in cut 4, when the judge gives it a writer (C5).
- **Ship claim** and **Item claim** (target rows): held on the subject `Entity` as `_hullClaim` and `_itemClaims`,
  read through `ClaimedHull` and `ClaimOf(item)` (absent means the truth), written only through `Entity.Claim`.

## Cut 2 probes at 0681d6f2

`ew-claims-in-the-book` is mapped as `cut-ew-claims-in-the-book.r1` (agent imagination-aeth-ew-claims). Probed at
`origin/master` `0681d6f2` (contact-hud and ew-hide-launch merged).

| # | Question | Result |
|---|---|---|
| C1 | Which consumers read another ship's identity to label, rank or choose? | Two. The enemy schematic: rows are `RevealsOf(...).Select(r => r.Item).Where(IsWeapon)` with `IsWeapon` over the true item's live `Behaviors` (SchematicDisplay.cs:96, 99, 104), the label is `ShowWeapon(x.Item.Data)` (:128), the range label is the true behaviour's `Weapon.Range` (:240-245). `CombatState`'s target-item choice walks `target.Equipment`, keeps items whose live behaviour is a `Weapon` and that `IsRevealed`, and ranks by that behaviour's `RangeDamagePerSecond` (Combat.cs:40-51). |
| C2 | Who chooses by allegiance? | `IsHostileTo`, `VisibleEnemies`/`VisibleFriendlies`, `Faction`: ActionGameManager.cs:419, 629, 1273, 1469; Minion.cs:14 (first enemy added); TurretController.cs:98 (first enemy ship); LockWeapon.cs:98; Weapon.cs:113; Sensor.cs:211 (ally datalink, truth by ruling). None reads a class. All belong to cut 3 (`ew-hostility-from-belief-ruled`). |
| C3 | What else shows another ship's identity? | The target `InventoryPanel` (InventoryPanel.cs:241-330 with `hud`): title `entity.Name`, which is `HullData.Name` (Entity.cs:1057), the true hull shape, true `GearOccupancy` and `Armor`; enemy hull fills (ActionGameManager.cs:1351, SchematicDisplay.cs:216-222). Owned by the aetheria-release follow-up `target-panel-reads-enemy-hull` (an operator design call). Under a hull lie these show the true hull, so cut 4 needs that follow-up settled or folded. |
| C4 | Which reads of truth stay? | Aim and damage: FireControl's `target.Hull` reads (FireControl.cs:359, 392, 882, 931, 1022, 1159, 1231, 1567, 1638, 1954), `IsRevealed` (:160), the player's `CycleTargetItem` (ActionGameManager.cs:438) and Combat's aim height `target.Hull` `GridOffset` (Combat.cs:110-112). |
| C5 | Does cut 2 need a stored belief? | No. Nothing can set `SeenThrough` before the judge (cut 4), so a stored belief would be state with no writer. `Contact.Profile` is derived in `ContactOf` from the subject's `ClaimedHull` at `Identified`. The belief store and `SeenThrough` arrive with the judge in cut 4, which then owns `seen-through-holds-while-tracked`. |
| C6 | Can a claim ride the act? | Yes, one writer each: `Signature` (SetSignature, Entity.cs:106), `ActiveEmission` (Emit, :903), `PaintWarning` (ReceivePaint, :912). `PaintWarning.EmitterClass` is already a class slot; outside tests nothing reads it (LockWarningTests.cs:144, PaintModeTests.cs:195, 269 assert it equals the item's Data, which stays true by default). |
| C7 | Does the 3D view classify? | `ZoneRenderer` instantiates the true hull's prefab once per entity (ZoneRenderer.cs:380-383). Under a hull claim the drawn model is a label; re-instancing on a belief change is cut 4's, since a profile cannot differ from the truth before then. |
| C8 | Point defence and seekers? | No point defence in the Body (grep `pointdefen`: zero). Seekers are data (`MunitionSeekerData`, MunitionParts.cs:76) with no in-flight target choice. Nothing to move. |
| C9 | How do tests set a claim with no EW gear? | Through `Entity.Claim`, internal (Aetheria.Shared.csproj:28 has `InternalsVisibleTo Aetheria.Shared.Tests`). It is the model page's ship-claim slot. Its production writer is cut 4's masquerade behaviour. Because SetSignature stamps `ClaimOf(item)` at write time, cut 4 must make the item claim known before a weapon fires (H2): it can pull from the EW behaviour as the suppressor does, or write before execution order matters. |
| C10 | What can an observer measure of a claimed weapon? | A claim is a design (`EquippableItemData`) with no live stats; today's rank reads the true item's quality, heat, wear and power through its behaviour. Question `ew-claim-weapon-measure`. |

## Cut 1b probes at 0898725f

`ew-hide-launch-catalog` is mapped as `cut-ew-hide-launch-catalog.r1` (agent imagination-aeth-ew-catalog), blocked on
question `ew-suppressor-strength`. Probed by a scratch AetherDb command and a scratch test run on Yggdrasil at
`0898725f` (never pushed to origin; job log kept in the session scratchpad only).

| # | Question | Result |
|---|---|---|
| K1 | Which Tool gear exists to template from? | Five non-weapon Tool designs. `Targeting Computer`: 1x1, mass 60, price 60000, durability 50, SpecificHeat 1, Conductivity 1, temperatures min 200 / max 500 / optimum 276.5 / plateau 36, resilience 1, roles [processor, array]; consumables-first-set already reads it by name as the 1x1 tool template (Program.cs:2134). Tool prices run 25000-150000. |
| K2 | How did a prior catalog cut land? | Command first (`815526ee`), then `GameData/Aetheria.cc` alone with the dry-run text in the message (`7a882b62`), then the command made `public` with a `root` argument so tests drive it on a scratch catalog (`d26b465e`). The .cc is an LFS binary: at merge the command is replayed on master's catalog, never the blob merged (release map SC13). Tests: `ConsumablesFirstSetCommandTests` (dry run, apply, second apply on a scratch copy), `SmokeRoundsCommandTests` (apply writes the shipped design's exact bytes), content tests on the shipped catalog. |
| K3 | What reveals a flash, numerically? | `TargetDetectionInfoThreshold` .1, `TargetInfoDecay` .5. One sensor: revealed while S x g / (.5 x d) > .1, so reach is 20 x S x g, linear in S (Sensor.Gain, Entity.PassiveInfo:440-450). Shipped sensors: `The Bat` sensitivity 1-2, `not if i see you first` .25-1.5, curve hull 0..1, so g is about .7-1.35 at mid quality. |
| K4 | How bright are shipped flashes? | Weapon `Visibility` (Min at quality 0, Max at 1): Spectra 550-150, PD Laser 300-100, PD Gun 400-150, Autocannon 1200-800, LRMM72 1200-800, SRMM72 1500-1000, ChargeBlast 5000-1000, CShot 2000-3000. So an unsuppressed 1000 flash is seen about 20,000 units out against weapon ranges of 300-2,500: hiding a shot from its own target needs Factor about .05. |
| K5 | How fast does a suppressor heat? | Cell thermal mass = the hull's share (mass x SpecificHeat / cells: Longinus 37.88, Djinni 45.18) plus the item's mass x SpecificHeat (Entity.cs:1198, :1533). A 60-mass 1x1 suppressor's cell is 97.9 (Longinus) or 105.2 (Djinni): 224 heat raised it 2.29 K and 2.13 K exactly. Ten seconds of zone updates with no radiator cooled it by under .5 K. Ships start at 280 K. Thermal shutdown (`ShutdownPerformance` .1 on the template's 200/500/276.5/36 shape) is at about 479 K, so a rated temperature of 310-330 K gives out long before the item fails. |
| K6 | Does a 1x1 Tool suppressor fit and work on shipped hulls? | Yes. Built like `BuildThrustedShip` with only the reactor, then the suppressor at `UnoccupiedSpace` via `Shape.FitsWithin` and `TryEquip` before `Admit`: Djinni 104 free cells, Longinus 20. Active at once; at lot quality .5 a draft with Factor .4-.2 evaluated .3 and each `SetSignature(LaunchFlash, 400)` stored 120. |
| K7 | Do new Tool products reach generation? | `HasHome` is true for any Tool design (LoadoutGenerator.cs:240-242), so stations stock it and `FillInterior` may fit it on generated ships by the existing availability rule. Nothing else to wire. |
| K8 | Does adding a Tool design and four products disturb existing tests? | No. The full `Aetheria.Shared.Tests` suite (1665 tests, about 16 minutes) ran green before and after a draft design and four products (NiteLife, Finch, DME, R&D) were applied to the catalog in the same job. CB-R1 (`ShippedCatalogRolesAreLive`) held with two roles, each read by a Quality term of exponent 1. |

## Cut sequence (not yet mapped; cuts 1, 1b and 2 are mapped as specs)

Cut 1 is split in two by size: `ew-hide-launch` is the behaviour, the one hook in `SetSignature` and their tests;
the catalog authoring follows it as 1b, mapped above.

Cut 2 `ew-claims-in-the-book` is mapped (above). It differs from the target line in one place: `SeenThrough` and the stored belief move to cut 4 with the judge (C5). Cut 4 also takes the drawn hull model (C7) and needs the target panel settled (C3). The remaining cuts are the target doc's cut sequence (3, 4, 4a `ew-jammer`, 5-10).
