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
| H7 | Next free `BehaviorData` union key? | 48. Behaviors.cs:143-191 holds keys 0-11, 15, 16, 18, 20-24, 28, 31-47; 12 is retired, 13 is absent and 14, 17, 19, 25, 27, 29, 30 are commented out, 26 retired. Never reuse a retired key. `BehaviorData.Group` is `Key(0)`; a new data type's own fields start at `Key(1)`. |
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

## Cut sequence (not yet mapped; cut 1 is mapped as `cut-ew-hide-launch.r1`)

Cut 1 is split in two by size: `ew-hide-launch` is the behaviour, the one hook in `SetSignature` and their tests;
the catalog authoring follows it as 1b.

1b. `ew-hide-launch-catalog`: an `AetherDb` command authoring the Launch Suppressor design and its products by the
    early-tier makers (question `ew-suppressor-makers`). Waits on: cut 1, that question.

The remaining cuts are the target doc's cut sequence (2, 3, 4, 4a `ew-jammer`, 5-10), unchanged.
