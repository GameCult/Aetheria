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

`ew-hide-launch-catalog` is mapped as `cut-ew-hide-launch-catalog.r2` (agent imagination-aeth-ew-catalog-r2; r1 is
superseded). Question `ew-suppressor-strength` is ruled both-as-two-designs (kitchen-sink direction): a strong
Launch Suppressor (NiteLife, Finch, DME, R&D) and a mild sustained one (Lightsail, Alakrita, defaulted). K1-K8 were probed by
a scratch AetherDb command and a scratch test run on Yggdrasil at `0898725f`; K9-K11 by a scratch test on Yggdrasil at
`1746e8bd` (code identical at `1e219e96`). Nothing was pushed to origin; job logs stay in session scratchpads.

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
| K9 | How far does a flash reach on a shipped observer? (corrects K3's reach) | A Longinus with its first shipped Sensors-hardpoint sensor at lot quality .5 has `PassiveInfo(position, 1)` of 1.89e-3 at 500, 6.31e-4 at 1500, 3.16e-4 at 3000, 1.58e-4 at 6000, 9.5e-5 at 10000: about 0.95 / distance. A flash is revealed while strength x that exceeds .1, so reach is about 9.5 x strength, not K3's 20 x (K3's gain range of .7-1.35 is high for this sensor). Unsuppressed 1000: seen to 9,500, hidden at 10,000. Mild mid (300): seen at 1500 (.19), hidden at 3000 (.095) and 6000. Strong mid (55): hidden at 1500 (.035). |
| K10 | How long does a suppressor last under fire, with the hull ticking? | Probed as a draft design on a shipped Longinus and Djinni with reactor and radiator, one launch per second for 120 s, four .25 s zone updates between launches, from rest at 280 K. Cell thermal mass 97.9 / 105.2. Strong (Factor .08-.03, HeatPerUnit 1.4-.8, rated 310-330) at mid quality, 1000 flash: Longinus 4 suppressed launches (stored 55) then bright, Djinni 5; 300 flash: 14 / 14. After that the cell cools through the rated line and flickers: 10 of 120 launches suppressed (Longinus), 6 (Djinni) at 1000. Mild (Factor .4-.2, HeatPerUnit .8-.5, rated 310-330) at mid, 1000 flash (stored 300): first 10 suppressed on both hulls, 22 / 13 of 120 in all; 300 flash (stored 90): first 36 (Longinus) and 33 (Djinni), 68 / 41 of 120. Quality moves it: mild at quality 1 and 300 flash lasts 60 / 48 launches, strong at quality 0 and 1000 flash only 3. So the mild design is sustained for light weapons (about half a minute at one launch a second) and a dozen autocannon-class launches, not for ever: a hot cell holds the rated line and lets a flash through about every fifth to tenth launch. |
| K11 | Does an opening volley stop for good without cooling? | Direct `SetSignature` calls with no zone update between them (r1's test shape): the cell never cools, so once past the rated temperature every later call is unsuppressed. Strong mid, 1000 flash: removed 945 x 1.1 = 1039 heat per call = 10.6 K (Longinus) / 9.9 K (Djinni); the fifth call on a Longinus (321.7 K > 320) and the sixth on a Djinni are the first bright ones. Margin on the Djinni is under 1 K (319.4 after four calls against a rated 320), so a test must derive its expected count from `ThermalMass` and the authored figures rather than assert a literal. |

## Cut 3 probes at ae300c6a

`ew-belief-allegiance` is mapped as `cut-ew-belief-allegiance.r2` (agent imagination-ag4-ew-belief; r1 superseded after B2 was
corrected); telemetry is received with the track (ruling `ew-telemetry-with-the-track`). B1-B3 ran as a scratch test class over the `IffAndCombatTests` world on
Yggdrasil at `ae300c6a` (2026-10-17 04:19 UTC, 17 tests green, the 16 existing IffAndCombat tests included; B2b at 04:31 UTC); S1-S6 are
source reads at the same commit. Nothing was pushed to origin.

| # | Question | Result |
|---|---|---|
| B1 | Does the derived branch know allegiance with nothing sensed? | Yes. Zone owned by O, a patrol of O and a stranger of X. At Open: hostile false. Stranger set to Critical: `patrol.IsHostileTo(stranger)` true and `stranger.IsHostileTo(patrol)` true with `EntityInfoGathered` 0, no reveal, untracked. |
| B2 | Does `EntityHostility` follow `IsHostileTo`? | Yes, through the tick. `Entity.Update` re-derives it for every zone entity while active, writing only on change (Entity.cs:1890-1897). B2b (04:31 UTC): an owner station at Critical, radius 1000; a tracked stranger at x=5000 is Open and a friendly; moved inside, two `zone.Update(.25)` later it is Critical, `EntityHostility` true, listed once in `VisibleEnemies`, gone from `VisibleFriendlies`. The cache lags one tick when the observer updates before the subject. (The first B2 run set `CurrentSecurityLevel` by hand; `Update` re-derives it from position each tick, so the ticks put it back to Open: a probe artifact, not staleness. The comment at :62 is right.) |
| B3 | Does a grudge need an act? | No. `a.SetIff(npc, true)` with npc tracking a: npc holds a hostile override on a with zero paints and zero reveals of a (`WatchForGrudge` reads `other.EntityHostility`, Entity.cs:915-944). `PerceivedStanceOf` (:903) reads the same; it has no product reader, only IffAndCombatTests:233-237. |
| S1 | Who reads allegiance? | Through `IsHostileTo`: LockWeapon.cs:98, Weapon.cs:142 (fire gate), ActionGameManager.cs:427, 624, 1484, RememberedHostiles (Entity.cs:1097; ActionGameManager 1280, 1517). Through `VisibleEnemies`: Flight.cs:63 (picture), :139 (seen), TurretController.cs:97, TargetNearestEnemy (Entity.cs:579), ActionGameManager 1106-1136, 1279. Through overrides: Flight.cs:76 (Grudged), :157 (held tracks), RunStart.cs:54-55. Truth by datalink: Sensor.cs:211. Subject truth outside IsHostileTo: Flight.cs:88 (`entity.PresencePermitted`, Trespass). Minion and Combat no longer read allegiance (faction-play-1 moved targeting to Flight). |
| S2 | Is a belief branch equal to today's rule for honest tracked pairs? | Yes, by case: today the recursive call `other.IsHostileTo(this, true)` is true only when `other.Faction` is the zone owner and `this` is not permitted. So owner to subject is `!subject.PresencePermitted`, non-owner or no faction to an owner ship is `!own PresencePermitted`, everything else false, and two owner ships are never hostile (both Beloved). The spec's branch (ally false; unknown false; telemetry faction is owner and own presence forbidden; or own faction is owner and the subject believed trespassing) gives the same four answers once telemetry is received. |
| S3 | What act can provoke? | A lock: `LockWeapon.Execute` calls `target.ReceivePaint(Entity, Item, Track, Lock)` (LockWeapon.cs:111), published with its emitter in `PublishInbox` (Entity.cs:973-986). A shot: `PendingShot` carries `Source` and `Target` (FireControl.cs:1995-1999) to `Apply` (:1124). A blast (`Detonate`, :1577) carries no source. LockWeapon locks only what its ship already holds hostile (:98), so a lock is always a hostile act. |
| S4 | Does Sense allocate? | It must not: ContactRevealTests pins zero bytes over 600 Sense calls (~:1720). `RevealsOf` and `ContactOf` enumerate through `IEnumerable`; the telemetry prune walks the book's `Dictionary` values directly. |
| S5 | Does the filing subscription duplicate? | Every `EntityHostility` Replace adds the subject to `VisibleEnemies` or `VisibleFriendlies` again (Entity.cs:374-381; FlightTests:732 notes it). The Update loop writes only on change, so the per-tick path lists once (B2b); `RefreshStance` keeps that rule for every caller. |
| S6 | Which tests lean on the deleted machinery? | Grudge by stance: IffAndCombatTests:160-222 (four tests), PerceivedStance :226-239. Derived hostility with nothing sensed: ContactRevealTests:1744-1746 (docked-stance fixture). Everything else sets overrides directly (FlightTests, ScenarioTests, ConsumableAiTests, the FireControl suites). |

Model page corrections for cut 3 (target rows Belief and Contact): the Belief row's allegiance part is stored from this cut,
as `_telemetry` on the observer, because it now has a writer (the observer's own track entry) and must outlive the track for
remembered contacts; its profile and `SeenThrough` parts still arrive with the judge in cut 4. It is created at track entry
(ruling `ew-telemetry-with-the-track`), not at identification, ends with the contact, and the subject's claim it reads is the truth until
cut 4 adds `ClaimedFaction`. Contact gains no allegiance field: no reader needs one. Grudges stay the observer's IFF
override, now written only from acts on its side (`Provoke`).

## Cut sequence (not yet mapped; cuts 1, 1b, 2 and 3 are mapped as specs)

Cut 1 is split in two by size: `ew-hide-launch` is the behaviour, the one hook in `SetSignature` and their tests;
the catalog authoring follows it as 1b (two designs, six products), mapped above.

Cut 2 `ew-claims-in-the-book` is mapped (above). It differs from the target line in one place: `SeenThrough` and the stored belief move to cut 4 with the judge (C5). Cut 4 also takes the drawn hull model (C7) and needs the target panel settled (C3). Cut 3 `ew-belief-allegiance` is mapped (Cut 3 probes, above). The remaining cuts are the target doc's cut sequence (4, 4a `ew-jammer`, 5-10). Cut 4 also adds `ClaimedFaction` (read by `ReceiveTelemetry`), the silent-transponder case and the policy for an unknown contact in a secure area.
