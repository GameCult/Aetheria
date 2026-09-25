# Fire Control: Cut Map

Date: 2026-09-19

Status: Imagination pass, cut map. Nothing here has landed. Ends are owned by
`docs/three-gates-scope.md` ("Fire control") and
`docs/shield-presentation-contract.md` ("The commit window"); this document owns
the means.

Anchors are against `codex/item-provenance` HEAD `8a00fc92`, read-only. Nothing was
built in the main tree and Unity was not run. Claims marked **(probe)** were measured
by running `tools/AetherDb` (`census`, `hardpoint-fit`) against the live catalog
`GameData/Aetheria.cc` from the free worktree `F:\Projects\Aetheria-stats`, CultLib
pinned at the shared `45c2f40` worktree. Claims marked **(read)** come from reading
source at the cited anchor.

**This map supersedes Cut 2 of `docs/headless-playground-cut.md`**
(`:490-810`, anchored to `562cdcf2`). That draft is now history for three reasons:
its anchors predate the stats-and-power campaign, which moved most of the code it
cites; it predates the commit-window ruling of 2026-09-18, which changes the shape of
a shot from an instant to a lifecycle; and it predates the per-hardpoint arc override
and 360-degree turret rulings. Its substrate probes are still good and are carried
forward below with citations. Where the two disagree, this document wins; nobody
should be reading two live designs.

**Scope: fire control only.** What a presentation does with a committed outcome is the
capability/presentation seam's business (`docs/shield-presentation-contract.md`).
Faction territory is the campaign after this. §6 names every seam and stops there.

---

## Rulings this map is built on (do not re-litigate)

From `docs/three-gates-scope.md:47-99` and `docs/shield-presentation-contract.md:34-57`:

- **R1. Hit detection goes.** Operator, 2026-09-17: "We wanted to get rid of hit
  detection anyway... Just roll the dice based on weapon stats, targeting system stats
  (new subsystem) and sensor state."
- **R2. Targeting systems are a new subsystem**, interior `Tool` gear.
- **R3. A shot resolves on arrival**, hit chance reduced by how far the target deviated
  from the predicted intercept. Manoeuvring is evasion.
- **R4. The outcome commits a short fixed time before impact.** Operator, 2026-09-18:
  "That hybrid is the way." Deviation feeds the roll up to that horizon; after it the
  result stands and the rest of the flight is choreography. The horizon is one authored
  fire-control setting. A commit is authoritative; presentations perform it and never
  predict.
- **R5. Subsystems reveal between `TargetArmorInfoThreshold` and
  `TargetGearInfoThreshold`**, and both the player and the AI may aim at a revealed
  subsystem.
- **R6. Per-hardpoint firing arcs.** Mount direction from the item's `ItemRotation`, as
  reaction thrusters do; default 120 degrees from `GameplaySettings`; an optional
  per-hardpoint override; turrets 360.
- **R7. The simulation is 2D.** The 3D is set dressing.
- **R8. Unity weapon effects and `HullCollider` become presentation only.** Their
  `Physics.*` queries stop deciding damage.
- **R9. Every weapon type in the catalog's history stays**
  (`docs/headless-playground-cut.md` fork E). Nothing is deleted for being unused.

From `docs/stats-and-power-cut.md:996-1030`, the seam the stats campaign left:

- **R10. Fire control must snapshot what it rolled over.** The resolver hands out a
  current value; it does not promise that value is still current a second later.
- **R11. A starved weapon does not fire** (Cut 4 of that campaign: no fraction of a
  shot). A degraded shot instead is a ruling against that map, not a thing to work
  around here. See Q6.

---

## 0. Substrate

### 0.1 What decides a hit today, end to end

Nothing in `ServerShared` does. The whole chain is Unity **(read)**:

1. `InstantWeapon.Execute` (`Assets/Scripts/ServerShared/Behaviors/InstantWeapon.cs:216-237`)
   walks the burst and raises `OnFire` (`:228`), a parameterless `event Action` declared
   at `:73`. It carries no outcome and nothing in `ServerShared` subscribes.
2. `Assets/Scripts/Gameplay/EntityInstance.cs:203-204` subscribes and calls
   `InstantWeaponEffectManager.Fire(weapon, item, source, target)`
   (`Assets/Scripts/Gameplay/Weapons/InstantWeaponEffectManager.cs:7`). The constant
   family goes the same way at `:232-235`.
3. The manager spawns an effect and copies the weapon's resolved numbers onto it.
   `ProjectileManager.cs:18-25` jitters the barrel forward vector by
   `Random.Range(±Spread/2)` on all three Euler axes — that is the entire aiming model.
4. The effect raycasts. `Projectile.cs:58`, `GuidedProjectile.cs:153`, `Laser.cs:40`,
   `Lightning.cs:34`, `ConstantLaser.cs:74`, `ConstantLightning.cs:54`,
   `HitscanEffect.cs:31`, `Mine.cs:68,92`, plus `Projectile.cs:114` for airburst.
5. On a hull collider it calls `HullCollider.SendHit`
   (`Assets/Scripts/Gameplay/HullCollider.cs:18-30`), or `SendSplash` (`:32-41`).
6. `EntityInstance.cs:316-396` subscribes to those subjects, converts a UV texture
   coordinate into schematic cells, expands by `DamageSpread`, marches a penetration ray,
   and calls the local function `DamageSchematic` (`:277-314`), which is the only writer
   of `Armor`, item `Durability` and `Hull.Durability` from combat.
7. Death: `EntityInstance.cs:405-439` subscribes to `HullDamage`, rolls loot with
   `UnityEngine.Random`, and calls `entity.Zone.Entities.Remove(entity)` (`:437`).

**Authority versus presentation in that chain.** Everything from step 3 down is
authority wearing a presentation costume. The shield branch repeated in all seven
effects (`Projectile.cs:60-80` and its copies) is the absorb rule, duplicated seven
times. `DamageSchematic` is the damage rule. `EntityInstance.cs:437` is the death rule.
Genuine presentation is narrow: trail rendering, line renderers, the lightning compute
animation, hit effect prefabs, `ShieldManager.ShowHit`, and the destroy effect.

**The 2D/3D violation.** `EntityInstance.cs:323` and `:380` call
`transform.InverseTransformDirection` to get a hit direction into the ship's frame. That
is a 3D rotation standing in for the planar rotation R7 says the simulation is made of.

### 0.2 What already exists of targeting

- `Entity.Target` — `Assets/Scripts/ServerShared/Entity.cs:46`, a
  `ReactiveProperty<Entity>`. Cleared when the target leaves `VisibleEnemies` (`:196`,
  `:222`). `TargetRange` is recomputed each tick at `:920`.
- `Entity.EntityInfoGathered` — `Entity.cs:57`, a `ReactiveDictionary<Entity, float>` in
  [0,1], accumulated with decay by `Sensor.Execute`
  (`Assets/Scripts/ServerShared/Behaviors/Sensor.cs:152-184`).
- `TargetDetectionInfoThreshold` gates `VisibleEnemies` at `Entity.cs:232-246`.
- **`TargetArmorInfoThreshold` and `TargetGearInfoThreshold` are authored and read by no
  code.** They are declared at `Assets/Scripts/ServerShared/Settings.cs:212-213` and
  appear nowhere else in `Assets/Scripts` **(read)**. R5's reveal tiers are unwired
  settings.
- `LockWeapon` — `Assets/Scripts/ServerShared/Behaviors/LockWeapon.cs`. Integrates a
  0..1 `_lock` scaled by `pow(EntityInfoGathered[target], SensorImpact)` (`:92`), gated by
  a `LockAngle` cone off `Entity.LookDirection` (`:88-89`). `CanFire` (`:56`) requires
  `_lock > .99f` and range within `[MinRange, Range]`. This is the closest thing in the
  codebase to a to-hit probability, and it is per-weapon rather than per-ship.
- `TurretController` — `Assets/Scripts/ServerShared/Behaviors/TurretController.cs`.
  `TurretControllerData` has no fields (`:15-25`). It writes `Entity.LookDirection` to a
  first-order intercept (`:67-72`) and fires each weapon when
  `dot(x.Direction, Entity.LookDirection) > .99f` (`:81-83`).
- `Sensor` — sensitivity, ping boost/energy/visibility/range/cooldown, all
  `PerformanceStat` (`Sensor.cs:16-34`).
- Player target selection: `Assets/Scripts/Gameplay/ActionGameManager.cs:368-403`
  (reticle, nearest, next, previous). Player fire is not arc-gated at all.
- **No targeting-system item, behavior or stat exists anywhere in code or catalog.** A
  repo-wide grep for `Targeting|FireControl|Accuracy|Aim` finds one `#region Targeting`
  label at `ActionGameManager.cs:368` and nothing else; the same grep over the catalog
  returns zero **(probe)**.

### 0.3 Where hardpoint rotation and arcs live

- `HardpointData` — `Assets/Scripts/ServerShared/ItemData.cs:538-545`: `Type`,
  `Position`, `Shape`, `Transform` (a prefab node name), `Rotation` (`ItemRotation`),
  `Armor`. **There is no arc field on `HardpointData`, `WeaponData`, or any effect.**
  A per-hardpoint override attaches here, as catalog data already authored per hull.
- `ItemRotation` is four-way (`Assets/Scripts/ServerShared/Enums.cs:7`);
  `Extensions.cs:135-166` gives `None` = forward, `Reversed` = aft, `Clockwise` = right,
  `CounterClockwise` = left. `Agent.cs:72` reads `Clockwise` as right and `Thruster` uses
  the same convention for mount direction, which is the precedent R6 names.
- `Behavior.Direction` (`Assets/Scripts/ServerShared/Behaviors/Behaviors.cs:24-44`) has
  two paths. The first (`:30-34`) reads `Entity.HardpointTransforms`; the second
  (`:37-38`) is the item-rotation fallback R6 wants.
- **`Entity.HardpointTransforms` (`Entity.cs:58-59`) is written only by Unity**, at
  `EntityInstance.cs:514-517`, from the first `WeaponHardpoint.FiringPoint` barrel's
  world forward. Headless it is empty. That single dictionary is why
  `Combat.cs:100-102` throws `KeyNotFoundException` on the first combat tick outside
  Unity, and why the simulation's own notion of where a gun points is a readback from a
  renderer.
- Barrel swing is `ArticulationPoint`, yawing and pitching toward `LookAtPoint`
  (`EntityInstance.cs:400-403`, `:519-520`). Authored limits on the Longinus energy
  barrels are yaw [-1, 1] — effectively fixed forward — while the Turret prefab's yaw
  pivot is [-720, 720] (`docs/headless-playground-cut.md:533-545`, prefab probe, not
  re-measured here). That is presentation and stays.
- `AgentFiringMinDot` (`Settings.cs:218`) is **declared and read by nothing** **(read)**.

### 0.4 What the catalog ships in weapons

**(probe)**, `tools/AetherDb census` and `hardpoint-fit` over `GameData/Aetheria.cc`:
51 designs, 37 products, of which 18 are weapons.

| Hardpoint kind | Designs | Sellers |
|---|---|---|
| Ballistic | 6 | Zhestokost 2, Lightsail 1, AU 1, DME 1, unsold 1 |
| Energy | 7 | NiteLife 3, Lucent 1, Adrasteia 1, Alakrita 1, unsold 1 |
| Launcher | 5 | DME 1, R&D 1, unsold 3 |

Unsold, therefore unspawnable: `Autocannon`, `SRMM72`, `LRMM72`, `plight`, `pswarm`.
The earlier behaviour-class breakdown (8 `ProjectileManager`, 3 `LaserManager` at
velocity 0, 5 `GuidedProjectileManager`, 1 `LightningGunManager`, and **no design using
`ConstantWeaponData`, `HitscanEffect`, `Mine`, airburst or MIRV**) is carried from
`docs/headless-playground-cut.md:500-506` and was not re-measured; the census agent
could not reproduce it because `census` buckets by `HardpointType` and the one command
that prints behaviour fields was blocked. **Cut 4's safety argument depends on that
claim, so Hands re-measures it before Cut 4** (§4, First).

**Stats a roll can read today** (`Weapon.cs:22-68`, all `PerformanceStat` unless noted):
`Damage`, `Penetration`, `DamageSpread`, `MinRange`, `Range`, `Energy`, `Heat`,
`Visibility`, `Spread` (an angular cone in degrees), `Velocity`; plus `DamageCurve`
(a `BezierCurve`, damage against normalized range), `MagazineSize`, `ReloadTime`,
`AmmoType`. `InstantWeaponData` adds `Count`, `BurstTime`, `Cooldown`
(`InstantWeapon.cs:13-22`). `LockWeaponData` adds `LockSpeed`, `SensorImpact`,
`LockAngle`, `DirectionImpact`, `Decay` (`LockWeapon.cs:13-25`). The item carries
`WeaponRange`, `WeaponCaliber`, `WeaponType`, `WeaponFireTypes`, `WeaponModifiers`
(`ItemData.cs:476-492`).

Note against a misreading: **`DamageSpread` is not damage variance.** It is the
footprint, in schematic cells: `EntityInstance.cs:372-375` expands the hit shape
`round(hit.Spread)` times, where `hit.Spread` is the weapon's `DamageSpread`.

**What is missing from the authored data for a roll to be meaningful:**

- **No accuracy stat on anything.** No weapon, hull, cockpit or sensor contributes a
  to-hit number. This is what R2's targeting system exists to supply, and it is the
  whole reason the subsystem is required content rather than a nice-to-have.
- **No target-size term.** A roll that ignores how big the target is will feel wrong at
  every range. The hull `Shape` and a cell size give an angular radius; `SchematicCellSize`
  does not exist and must be authored (Q4).
- **No tracking or angular-velocity stat**, so nothing yet reads R3's deviation. The
  targeting system supplies it.
- **Beams have nothing beam-specific.** `ConstantWeaponData` adds only `AmmoInterval`
  (`ConstantWeapon.cs:15`); `Spread` is inherited and unread by any beam code, and
  `Velocity` is meaningless for one. There is no dwell or wander stat. Unused today, so
  Cut 4 gives beams the plainest possible rule and authors nothing new.
- **`ChargedWeaponData.ChargeFiringSpreadMultiplier` is a plain `float`**
  (`ChargedWeapon.cs:34`), not a `PerformanceStat`, so unlike every other combat number
  it cannot vary with quality or wear. Noted, not fixed here.
- **Lock is binary at 0.99** (`LockWeapon.cs:56`). There is no stat turning partial lock
  into hit chance, and `GuidedWeaponData` (`Launcher.cs:37-55`) has no lock stats at all.

### 0.5 Can the headless sim step a full engagement today

No, and two specific things stop it **(read)**:

1. `Combat.cs:100-102` indexes `HardpointTransforms`, which only Unity writes (§0.3).
   `Minion` enters `CombatState` as soon as `VisibleEnemies` is non-empty, so any NPC
   that sees an enemy throws on the tick it sees them.
2. There is no owner between `InstantWeapon.OnFire` and `Entity.HullDamage`. The only
   `ServerShared` writers of durability are the repair consumable
   (`Behaviors.cs:67,72-73`).

Everything else is already there and already exercised.
`tests/Aetheria.Shared.Tests/IffAndCombatTests.cs:52` (`BuildWorld`) opens the real
catalog, builds an `ItemManager` and a `Zone`, and `:73` (`NewShip`) equips a gun on a
hardpoint and activates the ship — two ships, positions, look directions, sensor info,
targets and IFF, headless. `:241` and `:255` assert on `weapon.Progress` after
`Activate()`, because a cooldown starting is the only observable consequence of firing
that exists. That fixture is this campaign's harness; it does not need to be built.

`Zone.Update` (`Zone.cs:126-151`) steps orbits, belts, agents, then entities.
`ItemManager.Random` is still clock-seeded (`ItemManager.cs:19`), so a seeded-roll test
needs a seed argument — see Q5.

The xunit project is `tests/Aetheria.Shared.Tests/Aetheria.Shared.Tests.csproj`
(net10.0, xunit), referencing `Aetheria.Shared/Aetheria.Shared.csproj`
(netstandard2.1, `noEngineReferences` mirrored by
`Assets/Scripts/ServerShared/Aetheria.Shared.Unity.asmdef`). The mutation harnesses are
`tests/mutation_tests_*.py`, 17 of them, each patching a named source anchor and
asserting a named test goes red with a no-op control.

---

## Rulings (operator and Self, 2026-09-19)

- **Q4, unaided accuracy: really, really bad.** Operator: "Targeting with the mark one eyeball
  should be really, really bad." A ship with no targeting system can hit something close,
  slow and unaware, and little else. That makes the targeting subsystem **required equipment**
  rather than an upgrade: every hull needs somewhere to put one, the catalog needs them at
  several price points, and a stripped hull is a genuine handicap. Hands picks the other two
  balance numbers; this one is authored to make unaided fire a last resort, and the operator
  tunes it in play.
- **Q1, turrets: author `FiringArc: 360`.** No new type for what a number already says.
- **Q2, player fire is arc-gated** like everyone else's. Manual and programmatic must not be
  two truths.
- **Q3, death removes the ship**, and loot stays as it is until the pickup cut.
- **Q5, seed the rolls in this campaign.** Two lines here beats waiting on an unstarted cut.
- **Q6, snapshot at fire, not at commit** (Self, accepting Imagination's counter-proposal to
  the stats map's §8 seam). A shot carries the gun that fired it: its parameters freeze when
  the trigger is pulled, and nothing about the shooter's heat, power or wear changes them
  mid-flight. Evasion still decides the outcome, because deviation is measured at arrival
  against the predicted intercept. `docs/stats-and-power-cut.md` §8 said "snapshot at commit";
  that is superseded, and the resolver's promise (a current value, not a stable one) is
  honoured either way.
- **Q7, mine proximity physics: leave it.**

## 0b. Identity, lifecycle, authority

One row per kind this campaign introduces. No cut below is mapped with a cell empty.

| Kind | What names it | What happens to it over time | Who decides |
|---|---|---|---|
| **A shot in flight** | `PendingShot`, a struct in `Zone.PendingShots` (a `List<PendingShot>`), identified by a monotonic `int ShotId` from `Zone`. Nothing outside `Zone` holds a reference. | Born in `FireControl.Fire` when a weapon's burst step fires, carrying a payload snapshot frozen at that instant. Ages by `dt` in `FireControl.Step`, called from `Zone.Update` after entities. Reaches its commit horizon, then its arrival time, then is removed the same tick it resolves. A shot whose source or target entity leaves the zone resolves as a miss and is removed. Never serialised: a zone saved mid-flight loses its shots, which is correct — a save is a scene boundary. | `Zone` owns the collection and the identity. `FireControl` owns the transitions. Nothing else may add, remove or mutate one. |
| **A roll** | Nothing. It is not a thing; it is one draw from `ItemManager.Random` inside `FireControl.Commit`, and only its result is named. | Exists for the duration of one call. Its inputs are the payload snapshot plus deviation measured at that instant; its output is written into the shot's `Outcome` and never recomputed. | `FireControl` alone. No behaviour, agent, presentation or UI may draw for a hit. |
| **A commit** | `ShotOutcome { int ShotId; Entity Source; Entity Target; EquippedItem Weapon; bool Hit; bool Shielded; EquippedItem Aimed; int2 Cell; float ArrivalIn; DamageType DamageType; }`, published on `Zone.ShotCommitted` (a `Subject<ShotOutcome>`). | Created once, at `ArrivalTime - CommitHorizon`, or at fire time when the flight is shorter than the horizon. Immutable from then on. Published once. Read by presentations and by nothing else. Dies with the shot. | `FireControl` creates it and is the only writer. Presentations are the only readers. **Nothing outside presentation may read a commit to change state** (`docs/shield-presentation-contract.md:57`). |
| **A targeting solution** | Not a stored object. `FireControl.HitProbability(weapon, source, target)` is a pure function, recomputed on demand. | Has no lifetime. AI reads it to decide whether to fire; the HUD reads it to draw a number; `FireControl.Fire` reads it once per shot and freezes the result into the payload. Two reads a tick apart may differ; nothing caches one. | `FireControl`. The targeting-system behaviour owns only its own resolved stats; it does not compute probability. |
| **Target data / knowledge of a subsystem** | `Entity.EntityInfoGathered[other]` (existing, `Entity.cs:57`) is the knowledge. `FireControl.IsRevealed(observer, item)` is the derived predicate over it. The current aim point is `Entity.TargetItem`, a `ReactiveProperty<EquippedItem>`. | Info accumulates and decays continuously under `Sensor` (`Sensor.cs:152-184`); ownership of that does not change. Reveal is **derived on every read**, never stored and never cached, so it falls back the moment info decays. `TargetItem` is written only by `Entity.TrySelectTargetItem`, nulled when `Target` changes, and **read through a derived accessor that returns null if the item is no longer revealed or no longer belongs to the target** — no loop clears it. | `Sensor` owns info. `FireControl` owns the reveal rule and the thresholds it reads. `Entity` owns the selection slot; `TrySelectTargetItem` is its only writer. Player input, `CombatState` and the test harness are its three callers. |
| **A hardpoint arc** | `HardpointData.FiringArc`, a `float` on catalog data (`ItemData.cs:538-545`), zero meaning "use the default". Resolved through `FireControl.ArcFor(item)`. | Authored once, in the catalog. Immutable at runtime. Read per shot and per AI fire decision; never stored on the entity. Mount direction is likewise derived per read from `Behavior.Direction`'s item-rotation path. | The catalog authors it; `FireControl` resolves and applies it; `GameplaySettings.FiringArc` supplies the default. **No prefab, transform, articulation limit or renderer may influence it.** |

---

## Cut order

1. **Mount direction and arc become simulation data.** Deletes `HardpointTransforms`
   and the Unity readback; adds the arc gate; unbreaks headless combat.
2. **Targeting system and reveal.** The new subsystem, the reveal rule, the selection
   slot, and the two dead settings wired up.
3. **Hit authority moves.** The Physics path in the four effects the catalog actually
   uses is deleted and `FireControl` plus the shot lifecycle replaces it, in one cut, so
   damage is never applied twice.
4. **The unused kinds join the same path.** Beams, hitscan, mines, splash and airburst
   stop querying Physics. They keep existing (R9).

Cuts 3 and 4 cannot be swapped, and 3 cannot be split into "roll" and "delete the old
path": a cut boundary between them is a cut boundary with two damage owners.

---

## Cut 1. Mount direction and arc become simulation data

- **Repo/branch:** `Aetheria`, branch `codex/fire-control` from `codex/item-provenance`
  `8a00fc92`. Depends on nothing.
- **First:** update `F:\Projects\Aetheria-stats` to HEAD (it sits 11 commits behind at
  `c4f6029e`); capture `AetherDb census`, `loadout 1`, `dangling` as the before side.

**Deletes first:**

- `Assets/Scripts/ServerShared/Entity.cs:58-59` — `HardpointTransforms` (2).
- `Assets/Scripts/Gameplay/EntityInstance.cs:514-517` — its only writer (4).
- `Assets/Scripts/ServerShared/Behaviors/Behaviors.cs:30-35` — the readback branch in
  `Behavior.Direction` (6). The item-rotation path at `:37-38` becomes the whole body.
- `Assets/Scripts/ServerShared/Agents/States/Combat.cs:100-102` — the
  `HardpointTransforms` dot test (3).
- `Assets/Scripts/ServerShared/Behaviors/TurretController.cs:81-83` — the `.99f` dot
  test (3).
- `Assets/Scripts/ServerShared/Settings.cs:218` — `AgentFiringMinDot`, read by nothing
  (1).

**Adds:**

- `Assets/Scripts/ServerShared/FireControl.cs` — a new static class, this cut holding
  only geometry: `MountDirection(EquippedItem)` (the planar unit vector from
  `Behavior.Direction`), `ArcFor(EquippedItem)` and
  `bool InArc(EquippedItem weapon, float3 toTarget)`, which tests
  `dot(normalize(weapon.Direction.xz), normalize(toTarget.xz)) >= cos(radians(arc/2))`.
  The test is planar (R7); target height never enters it. An arc of 360 or more passes
  unconditionally.
- `HardpointData.FiringArc` (`ItemData.cs:545`, next `Key`) — full width in degrees,
  `0` meaning "use `GameplaySettings.FiringArc`".
- `GameplaySettings.FiringArc = 120` (`Settings.cs`, after `:214`).
- `GameplaySettings.AgentMinHitProbability` is **not** added here; Cut 1's AI gate is
  `InArc` alone, which is strictly closer to today's behaviour than a probability the
  roll does not yet produce.

**Per-file changes (against `8a00fc92`):**

- `Combat.cs:100-102`: `shouldFire = FireControl.InArc(testWeapon.Item, toTarget)`. The
  intercept prediction at `:84-98` stays — it still aims `LookDirection`, and under R3 it
  becomes the predicted intercept a deviation is measured against.
- `TurretController.cs:81-83`: the same call. Under the default arc a turret hull, whose
  `Direction` never rotates (`TurretController` writes only `LookDirection`), would
  engage only within 60 degrees of its spawn facing. R6 says turrets are 360; the turret
  hull's hardpoints author `FiringArc: 360`. See Q1.
- Catalog: author `FiringArc` on the `Turret` hull's two Ballistic hardpoints. Follows
  the Cut C precedent — a Hands scratch console upserts, Unity and Studio closed.

**Authority map:**

- **Owner:** `FireControl` owns whether a weapon bears on a point. `HardpointData`
  authors the arc; `ItemRotation` authors the mount direction.
- **Inputs:** the equipped item's hardpoint rotation, `HardpointData.FiringArc`,
  `GameplaySettings.FiringArc`, the planar vector to the target.
- **Outputs:** one bool, consumed by `CombatState`, `TurretController`, and from Cut 3
  by `FireControl.Fire`.
- **Derived state:** `Behavior.Direction` becomes purely derived from item rotation.
- **Forbidden writers:** no Unity transform, `ArticulationPoint`, `WeaponHardpoint` or
  prefab node may influence aim. `Barrels`, `GetBarrel` and `ArticulationPoint` survive
  as presentation and must never be read by `ServerShared`.
- **Shared paths:** player fire, AI fire and turret fire all go through `InArc`. Player
  fire is arc-gated for the first time; see Q2.
- **Deletion line:** `HardpointTransforms` and its writer and readers are gone before
  `FireControl.cs` is written.

**Verification:**

- builds: `Aetheria.Shared/Aetheria.Shared.csproj`, `tests/Aetheria.Shared.Tests`,
  `tools/AetherDb`.
- tests, each named with the rule it pins:
  - `ArcFollowsMountRotation` pins *mount direction is the item's rotation, not the
    hull's facing*: a fixture hull with one weapon on a `None` hardpoint and one on a
    `Clockwise` hardpoint; a target 90 degrees to starboard passes only for the
    `Clockwise` weapon, a target dead ahead only for the `None` weapon. Mutation: make
    `InArc` use `Entity.Direction`; swap `Rotate`'s `Clockwise`/`CounterClockwise` cases.
  - `ArcBoundaryIsHalfWidth` pins *`FiringArc` is full width*: at 120, a target 59
    degrees off passes and one at 61 does not. Mutation: compare against
    `cos(radians(arc))`.
  - `ArcIsPlanar` pins R7: a target inside the arc horizontally but far above the firer
    still passes. Mutation: use the 3D vector.
  - `HardpointOverrideBeatsDefault` pins *the per-hardpoint override wins*: the same
    weapon on a hardpoint with `FiringArc: 360` passes at 170 degrees, and fails there on
    a hardpoint with `0`. Mutation: ignore the field; treat `0` as an arc of zero.
  - `CombatStateStepsHeadless` pins *the simulation decides aim without Unity*: a
    `Minion` with a target in range steps through `Zone.Update` without throwing.
    Mutation: restore the `HardpointTransforms` read.
- negative: `rg -n "HardpointTransforms" Assets/Scripts` empty;
  `rg -n "ArticulationPoint|FiringPoint|WeaponHardpoint" Assets/Scripts/ServerShared`
  empty; `rg -n "AgentFiringMinDot" Assets/Scripts` empty. (`\.99f` is **not** a safe
  negative grep — it occurs legitimately in `LockWeapon.cs:56`. Grep the two files by
  name instead.)
- operator: in Unity, side-mounted weapons still fire at targets abeam; a turret still
  tracks all the way around; nothing fires through the hull.

**Risk:** this cut changes what the AI fires at before the roll exists, so combat feel
shifts once and shifts again in Cut 3. That is the price of deleting the obsolete
authority first, and it is the right price.

---

## Cut 2. Targeting system and reveal

- **Repo/branch:** continues Cut 1. Depends on Cut 1 (uses `FireControl.cs`), and on
  provenance Cut C if that has not landed, because Cut C re-upserts every `ItemData`.
- **First:** `AetherDb census` and `loadout 1` over several seeds, as the before side.

**Deletes first:** nothing. This cut is entirely additive, which needs its account
(CODE IS A LIABILITY): the invariant it buys is R5, which cannot be purchased by moving
authority, because no authority for it exists — two authored settings are read by
nothing and there is no subsystem to carry accuracy. Cut 3 is where the subtraction is.

**Adds:**

- `Assets/Scripts/ServerShared/Behaviors/TargetingSystem.cs` —
  `TargetingSystemData : BehaviorData` (next free `Union` index on `BehaviorData`; Hands
  reads the current list in `Behaviors.cs` rather than trusting the number 39 quoted in
  the superseded draft) plus a `TargetingSystem : Behavior` that resolves and exposes:
  - `Accuracy` (0..1) — the ceiling on hit probability.
  - `Resolution` (detection threshold..1) — the info level at which sensor state stops
    limiting hits.
  - `Precision` (0..1) — the chance a hit lands on the aimed item rather than a random
    cell.
  - `Tracking` — how much deviation it forgives (consumed in Cut 3).

  All four are `PerformanceStat`, so lot quality, wear, heat and `PowerSupply` reach them
  through `EquippedItem.Evaluate` — that is the brownout ruling applied to accuracy, and
  it is why a starved targeting system rolls worse instead of switching off. Energy and
  heat come from the existing `EnergyDraw` and `Heat` behaviours on the item; the
  subsystem adds no power machinery of its own.
- `GameplaySettings.UnaidedAccuracy` — what an entity with no working targeting system
  fires with. Such an entity uses `Resolution = 1` and `Precision = 0`: it can still
  select a revealed item, and its hits scatter.
- `FireControl.IsRevealed(Entity observer, EquippedItem item)` — derived, never cached:
  1. Rank the target's non-hull equipment: hardpoint-mounted first, then size
     descending, then equipment index.
  2. Item `i` of `N` is revealed when
     `info >= lerp(TargetArmorInfoThreshold, TargetGearInfoThreshold, i / max(1, N - 1))`.

  This is the first and only reader of `Settings.cs:212-213`.
- `Entity.TargetItem` (`ReactiveProperty<EquippedItem>`, beside `Target` at
  `Entity.cs:46`) and `bool Entity.TrySelectTargetItem(EquippedItem)`, the only writer.
  It accepts null, or an item of `Target.Value` that `IsRevealed`. A `Target` change
  nulls it. The read accessor returns null when the item is no longer revealed, so decay
  drops the aim point without a loop.
- Player input: a select/cycle command beside `ActionGameManager.cs:368-403`, calling
  `TrySelectTargetItem`. The target schematic
  (`Assets/Scripts/UI/HUD/SchematicDisplay.cs:109-119`, which today lists only weapon
  items) shows revealed items and marks the selection. That is presentation; it decides
  nothing.
- AI: `CombatState` selects the revealed target `Weapon` item with the highest
  `RangeDamagePerSecond(range)`, or null.
- Catalog: two targeting-system designs, 1-cell and 2-cell, each with products from at
  least the manufacturers that sell the capacitor, so `IsAvailable` finds one in every
  galaxy. `LoadoutGenerator.FillInterior` picks one with `required: true` for every
  entity with `Weapons` — the starting LonginusX, NPC ships and turrets. Zenith, with no
  weapon hardpoints, gets none.

**Authority map:**

- **Owner:** `FireControl` owns the reveal rule. `Entity` owns the selection slot.
  `TargetingSystem` owns nothing but its own resolved stats.
- **Inputs:** `EntityInfoGathered`, the two threshold settings, the target's equipment
  list and item sizes.
- **Outputs:** a bool per (observer, item); a nullable `EquippedItem` per entity.
- **Derived state:** reveal is derived on read. `TargetItem`'s *validity* is derived;
  only the slot is stored.
- **Forbidden writers:** the HUD may not write `TargetItem` except through
  `TrySelectTargetItem`; no code may store a reveal result; no code may read the two
  thresholds except `IsRevealed`.
- **Shared paths:** player selection, AI selection and the test harness use one writer.
- **Deletion line:** none, and that is stated rather than hidden.

**Verification:**

- builds: as Cut 1.
- tests:
  - `SelectionNeedsReveal` pins *you cannot aim at what you have not resolved*: below
    the item's tier `TrySelectTargetItem` returns false; above it, true; a target change
    nulls it; info decaying below the tier makes the read null. Mutation: `IsRevealed`
    returns true; the `Target` change skips the null; the accessor returns the stored
    value without re-checking.
  - `RevealOrder` pins *the big obvious things resolve first*: hardpoint items reveal
    before interior items, larger before smaller. Mutation: drop the ordering.
  - `RevealSpansBothThresholds` pins *the tiers are the authored ones*: with N items,
    the first reveals at `TargetArmorInfoThreshold` and the last at
    `TargetGearInfoThreshold`. Mutation: swap them; use the detection threshold.
  - `UnaidedFiresWorse` pins *the subsystem is what supplies accuracy*: an entity with
    no targeting system resolves `UnaidedAccuracy` and `Precision` 0; one with a
    destroyed or offline system resolves the same. Mutation: fall back to the system's
    stats when offline.
  - `StarvedTargetingRollsWorse` pins the brownout ruling on accuracy: with an
    `Accuracy` stat carrying a `PowerSupply` term, a half-grant halves it rather than
    switching the item off. Mutation: gate on `PowerSupply <= 1e-4`.
  - `EveryArmedLoadoutGetsATargetingSystem` pins the required-item rule across seeds.
    Mutation: drop `required: true`.
- negative: `rg -n "TargetArmorInfoThreshold|TargetGearInfoThreshold" Assets/Scripts`
  returns `Settings.cs` and `FireControl.cs` **only**.
- operator: `AetherDb census` shows 2 more designs; `loadout 1` shows one more interior
  item per armed entity; `dangling` is 0; reopen reports 0 schema issues; CultCache
  Studio renders the new behaviour union (an operator check, because Studio's handling of
  a new union index is not something this map can promise).

**Risk:** `LoadoutGenerator.EquipHardpoints` can already fit the Tractor Beam into
LonginusX's only Sensors slot, leaving an NPC that gathers no info and therefore reveals
nothing and hits nothing (`docs/headless-playground-cut.md:520-523`). Check `loadout`
over several seeds. The fix is data or `EquipHardpoints`, not fire control.

---

## Cut 3. Hit authority moves

The rebuild. The old path is deleted and the new owner added in the same cut, because
any boundary between them is a boundary with two damage owners.

- **Repo/branch:** continues Cut 2. Depends on Cuts 1 and 2.
- **First:** capture a Unity play smoke of current combat feel (kill time against one
  NPC, roughly) as the before side, since this cut changes feel more than anything in
  the campaign.

**Deletes first** (paths and line counts against `8a00fc92`):

- `Assets/Scripts/Gameplay/EntityInstance.cs:277-396` — `DamageSchematic` and both
  `HullCollider` subscriptions (120).
- `Assets/Scripts/Gameplay/HullCollider.cs:13-14,18-41,48-65` — `Hit`, `Splash`,
  `SendHit`, `SendSplash` and both argument classes (~45 of 65). `OnCollisionEnter`
  (`:43-46`) stays; ship collision is a deferred Unity-physics surface.
- `Assets/Scripts/Gameplay/Weapons/Projectile.cs:57-103` — the raycast, shield branch and
  `SendHit` (47). `:109-122` (airburst) is Cut 4's.
- `Assets/Scripts/Gameplay/Weapons/GuidedProjectile.cs:152-189` — the same (38).
- `Assets/Scripts/Gameplay/Weapons/Laser.cs:40-75` — the same; the `LineRenderer`
  endpoint writes at `:59,70,77` are presentation and are rebuilt from the outcome (36).
- `Assets/Scripts/Gameplay/Weapons/Lightning.cs:34-72` — the same; the endpoint capture
  at `:67-71` stays as a visual (39).
- `Assets/Scripts/Gameplay/EntityInstance.cs:411-431` — the `UnityEngine.Random` loot
  roll, **only if Q3 is answered (a)**; otherwise this cut leaves it and the seam is
  named in §6.

**Adds:**

- `Entity.DamageSchematic(float damage, Shape hitShape)` — the rule moved verbatim from
  `EntityInstance.cs:277-314`, now a method on `Entity` in `ServerShared`, writing
  `Armor`, item `Durability`, `Hull.Durability` and raising the existing `ArmorDamage`,
  `ItemDamage` and `HullDamage` subjects. Not a redesign: the same arithmetic, the same
  order, the same thresholds.
- `Entity.ApplyHit(...)` — the shape construction moved from
  `EntityInstance.cs:334-394`, with two changes forced by R7: the hit cell comes from the
  rolled `Cell` rather than a UV texture coordinate, and the penetration march rotates
  the firer-to-target vector into the target's frame with a planar rotation by
  `-target.Direction` instead of `transform.InverseTransformDirection`.
- `FireControl.HitProbability(weapon, source, target)` — pure, no draw:
  - Zero, with no draw consumed, when there is no target; the target is not in
    `VisibleEntities`; range is outside `[MinRange, Range]`; a `LockWeapon` is not
    locked; or `InArc` fails.
  - Otherwise `p = Accuracy * pSensor * pSpread`, where
    `pSensor = saturate(unlerp(TargetDetectionInfoThreshold, Resolution, info))` and
    `pSpread = Spread > 0 ? saturate(angularRadius / (Spread / 2)) : 1`, with
    `angularRadius = degrees(atan(0.5 * max(Shape.Width, Shape.Height) * SchematicCellSize / range))`.
  - `SchematicCellSize` is the one new tuning constant (Q4).
- `FireControl.Fire(weapon, item, source)` — called once per burst step from
  `InstantWeapon.Execute` (replacing the bare `OnFire?.Invoke()` at
  `InstantWeapon.cs:228`). It computes the predicted intercept, the flight time
  (`range / Velocity`, or zero when `Velocity` is at or near zero), **freezes the payload
  snapshot** (R10) and appends a `PendingShot` to `Zone.PendingShots`. The snapshot holds
  `Damage`, `Penetration`, `DamageSpread`, `DamageType`, `Aimed`, the resolved targeting
  stats, and the intercept point. Nothing downstream re-evaluates a stat.
- `FireControl.Step(Zone, dt)` — called from `Zone.Update` after the entity loop
  (`Zone.cs:150`). For each pending shot: age it; at `ArrivalTime - CommitHorizon`
  compute deviation, roll, write `Outcome`, publish `Zone.ShotCommitted`; at
  `ArrivalTime` apply the outcome and remove the shot. A shot whose flight is shorter
  than the horizon commits at fire time, in the same call as `Fire` — that is every
  velocity-0 weapon, which is three of the catalog's eighteen.
- `GameplaySettings.CommitHorizon` — R4's authored horizon, one number, in seconds.
- The deviation term: `pDeviation` folds in how far the target is from the predicted
  intercept at commit time, forgiven by the targeting system's `Tracking`. This is R3's
  evasion, and the commit horizon is what bounds how late it counts.
- The roll: one draw against `p`. On a hit, a second draw against `Precision` decides
  where — on `Aimed`'s footprint cells on success, otherwise on one cell drawn uniformly
  over the hull `Shape`. An active shield that `CanTakeHit` takes the hit instead, and
  the outcome is marked `Shielded`; that rule now exists once, in `FireControl`, rather
  than seven times in the effects.
- `Zone.ShotCommitted` and `Zone.ShotResolved` (`Subject<ShotOutcome>`), the presentation
  feed.
- `InstantWeapon.OnFire` becomes `event Action<int>` carrying the `ShotId`, so the
  Unity manager can bind its effect to the shot it belongs to.

**Per-file changes:**

- `InstantWeapon.cs:228`: `OnFire?.Invoke(FireControl.Fire(this, Item, Entity))`.
  Charge multipliers already fold into `Damage` and `Spread` before this point.
- `Combat.cs:100-102`:
  `shouldFire = FireControl.HitProbability(...) >= Settings.AgentMinHitProbability`,
  replacing Cut 1's bare `InArc` (which the probability already subsumes, returning zero
  out of arc). `AgentMinHitProbability` is added here, not in Cut 1.
- `TurretController.cs:81-83`: the same gate and threshold.
- `EntityInstance.cs:203-204,232-235`: the manager call takes the `ShotId` and looks the
  outcome up, or subscribes to `ShotCommitted`. Effects fly to the rolled cell's world
  point or to a miss offset and play the impact on arrival. **They apply nothing.**
- `Zone.cs:150`: `FireControl.Step(this, deltaTime)` after the entity loop.

**Authority map:**

- **Owner:** `FireControl` owns the engage gate, hit probability, the roll, the cell, the
  shield branch, and when each of those happens. `Entity` owns the application of damage
  to its own schematic. `Zone` owns the pending-shot collection.
- **Inputs:** weapon stats through `EquippedItem.Evaluate`; targeting-system stats;
  `EntityInfoGathered`; `Target` and `TargetItem`; both entities' positions, velocities
  and planar directions; mount direction and arc; `ItemManager.Random`.
- **Outputs:** `ShotOutcome` on two subjects; `Entity.DamageSchematic`; shield
  `TakeHit`; `Entity.IncomingHit`.
- **Derived state:** `Behavior.Direction`, reveal, hit probability. Everything a
  presentation shows about a shot is derived from the outcome.
- **Forbidden writers:** after this cut no `Physics.*` query in
  `Assets/Scripts/Gameplay/Weapons` (except the unused four Cut 4 handles) may reach
  durability; `HullCollider` may not raise a hit; `EntityInstance` may not damage a
  schematic; no effect may call `TakeHit`; nothing outside `FireControl` may draw for a
  hit; nothing outside presentation may read a commit to change state.
- **Shared paths:** player fire, AI fire, turret fire, instant, charged, guided and
  locked weapons all reach damage through `FireControl.Fire` and `FireControl.Step`.
  There is exactly one commit primitive.
- **Deletion line:** the six deletions above land before `FireControl.Fire` exists.

**Verification:**

- builds: `Aetheria.Shared`, `tests/Aetheria.Shared.Tests`, `tools/AetherDb`; and Unity
  batchmode with the editor closed (`docs/item-provenance-cut.md` §6) reporting no
  `error CS`.
- tests, each with the mutation that must kill it:

  | Test | Pins | Mutation |
  |---|---|---|
  | `RollsAreSeeded` | *a fight is reproducible* — two zones, same seed, 50 shots, identical outcome sequences | `FireControl` draws from `new Random()` |
  | `ProbabilityFollowsInputs` | *the roll reads what the ruling says it reads* — zero below detection and outside range; rising with info to `Resolution`; falling with `Spread`; capped at `UnaidedAccuracy` with no system | drop `pSensor`; drop the range gate; drop the cap |
  | `OutOfArcConsumesNoDraw` | *the gate is a gate, not a penalty* — the RNG sequence is unchanged by an out-of-arc trigger | roll first and multiply by zero |
  | `ShotResolvesOnArrival` | R3 — no durability change before `range / Velocity`; a target removed mid-flight takes none | apply at fire time |
  | `OutcomeCommitsBeforeImpact` | R4 — `ShotCommitted` fires exactly `CommitHorizon` before `ShotResolved`, once per shot | publish at arrival; publish twice |
  | `EvasionCountsUntilCommitAndNotAfter` | R4's second half — a target that jinks before the horizon changes the outcome distribution; the identical jink after the horizon does not | measure deviation at arrival; measure it at fire |
  | `ShortFlightCommitsAtFire` | *the horizon degrades gracefully* — a velocity-0 weapon commits and resolves in one tick, still publishing both events in order | skip the commit for short flights |
  | `OutcomeIsSnapshotNotReread` | R10, the seam the stats campaign named — a weapon whose `Damage` collapses (power cut, heat spike) between fire and arrival still lands the damage it rolled | re-evaluate `Damage` at arrival |
  | `AimedHitLandsOnSelectedItem` | R5's payoff — `Precision` 1 and `p` 1: only the aimed item's durability falls | the roll ignores `Aimed` |
  | `HardpointHitDamagesItemThenHull` | the damage rule survived the move verbatim — damage above armor plus item durability zeroes the item and the remainder hits the hull | armor not subtracted; hull skipped |
  | `PenetrationMarchIsPlanar` | R7 — the march rotates by the target's planar direction and gives the same cells whatever the height difference | use a 3D rotation |
  | `ShieldTakesHit` | *the absorb rule has one owner* — an active shield absorbs and the schematic is untouched | remove the shield branch |
  | `DeadEntityStopsTakingShots` | *a shot cannot damage a corpse* — a target destroyed before arrival takes nothing and the shot is removed | resolve against a removed entity |

- negative greps, each checked against legitimate names before publishing:
  - `rg -n "Physics\." Assets/Scripts/Gameplay/Weapons/Projectile.cs Assets/Scripts/Gameplay/Weapons/GuidedProjectile.cs Assets/Scripts/Gameplay/Weapons/Laser.cs Assets/Scripts/Gameplay/Weapons/Lightning.cs Assets/Scripts/Gameplay/HullCollider.cs`
    — empty. (A repo-wide `Physics\.` grep is **not** valid until Cut 4, and never for
    `TractorBeam.cs`, which stays.)
  - `rg -n "SendHit|SendSplash|DamageSchematic" Assets/Scripts --glob '!**/ServerShared/**'`
    — empty.
  - `rg -n "TakeHit\(" Assets/Scripts --glob '!**/ServerShared/**'` — empty. Note
    `CanTakeHit` shares the substring; the trailing `(` on `TakeHit` distinguishes them,
    and the `Can` prefix must be checked by eye in the first run.
- operator play smoke: shots show rolled impacts and misses; a miss reads as a
  deliberate near-miss rather than a bug; damage matches the HUD; aiming at a revealed
  subsystem visibly concentrates damage there; kill time is in the same order as before.

**Risks.** `UnaidedAccuracy`, `SchematicCellSize`, `CommitHorizon` and the targeting stat
ranges are first guesses. The headless fixture is the tuning harness, and the operator
smoke is the arbiter. Second: `Combat.cs:84-98` aims `LookDirection` at a first-order
intercept, and R3's deviation is measured against `FireControl`'s own predicted
intercept; if the two predictions differ, an AI will systematically miss. They must be
one function. Hands makes `FireControl` own the prediction and `CombatState` call it.

---

## Cut 4. The unused kinds join the same path

R9 keeps every weapon type. This cut makes the ones no design currently ships obey the
same owner, so that authoring one later does not resurrect Unity hit detection.

- **Repo/branch:** continues Cut 3. Depends on Cut 3.
- **First:** **re-measure the claim this cut's safety rests on** — that no live design
  uses `ConstantWeaponData`, `HitscanEffect`, `Mine`, airburst or MIRV (§0.4, carried
  from a superseded map and not reproduced by this campaign's probe). A behaviour-class
  census needs `AetherDb roles-migrate` as a dry run, which the permission classifier
  blocked; the operator can unblock it or Hands can add a read-only `behaviors` command.
  **If any of these is live, it moves into Cut 3 instead.**

**Deletes first:**

- `Assets/Scripts/Gameplay/Weapons/Projectile.cs:109-122` — airburst `OverlapSphere` and
  `SendSplash` (14).
- `Assets/Scripts/Gameplay/Weapons/ConstantLaser.cs:74-110` (~37),
  `ConstantLightning.cs:54-70` (~17), `HitscanEffect.cs:31-55` (~25),
  `Mine.cs:68-75,92-117` (~34), `ConstantParticleWeapon.cs:72` and its `_hull` plumbing
  (~8) — every remaining `Physics.*` hit query and `SendHit`/`SendSplash` call in
  `Assets/Scripts/Gameplay/Weapons`.

**Adds:**

- `ConstantWeapon` rolls once per `GameplaySettings.BeamResolveInterval` for
  `Damage * interval`, through the same `FireControl.Fire`/`Step` pair with a flight time
  of zero, raising `OnBeamShot(int shotId)`. About ten lines; no design uses it yet, and
  it authors no new stat.
- Splash — airburst and mine blast — as `FireControl.Splash`, applying the existing
  `DamageSchematic` over the directional half-shape the deleted
  `EntityInstance.cs:318-329` computed, against every entity in radius. This is the one
  genuinely new rule in the cut, and it exists because R9 keeps the flak cannon and the
  mine.
- Nothing else. `Mine`, `TractorBeam` and ship collision stay Unity-physics surfaces for
  *placement and contact*, which is not hit detection; only their damage application
  moves.

**Authority map:** unchanged from Cut 3 except that the forbidden-writer list now covers
every file in `Assets/Scripts/Gameplay/Weapons`, and `FireControl` gains splash.

**Verification:**

- builds: as Cut 3.
- tests:
  - `BeamRollsPerInterval` pins *a beam is a sequence of rolls, not a continuous truth*:
    over one second a beam produces `1 / BeamResolveInterval` outcomes totalling
    `Damage`. Mutation: roll per tick; roll once.
  - `SplashHitsEveryEntityInRadius` pins *splash is one rule, not per-effect*: two
    targets in radius both take damage, one outside takes none. Mutation: damage only
    the nearest.
  - `SplashIsDirectional` pins that the moved half-shape rule survived: cells facing the
    blast take it, cells facing away do not. Mutation: damage the whole shape.
- negative: `rg -n "Physics\." Assets/Scripts/Gameplay/Weapons` returns **only**
  `Mine.cs`'s placement query, if Q7 keeps it — otherwise empty. `TractorBeam.cs:21` is
  outside this directory and stays.
  `rg -n "SendHit|SendSplash" Assets/Scripts` empty.
  `rg -n "class HullHitEventArgs|class HullSplashEventArgs" Assets/Scripts` empty.
- operator: nothing to check in play, because nothing in the catalog spawns these. The
  check is a Studio authoring pass: author one beam product and one flak product onto a
  test hull and confirm they damage.

---

## 5. Subtraction ledger

Estimates, to be compared with reality in the postmortem.

| Cut | Removed | Added | Deps / targets / formats |
|---|---|---|---|
| 1 | 19 production (Entity 2, EntityInstance 4, Behaviors 6, Combat 3, TurretController 3, Settings 1) | ~60 production (`FireControl.cs` geometry ~45, `HardpointData.FiringArc` 1, `GameplaySettings.FiringArc` 1, call sites ~13), ~90 test | +1 catalog field, authored on one hull. No new project, package or dependency. |
| 2 | 0 | ~140 production (`TargetingSystem.cs` ~55, reveal ~25, `Entity.TargetItem` ~25, AI selection ~10, player input ~15, `LoadoutGenerator` ~12), ~160 test | +1 behaviour union index, +2 designs and their products, +1 setting. |
| 3 | ~285 production (EntityInstance 120, HullCollider 45, Projectile 47, GuidedProjectile 38, Laser 36, Lightning 39) | ~330 production (`Entity.DamageSchematic`/`ApplyHit` ~130 moved, `FireControl` roll and lifecycle ~140, `Zone` pending shots ~35, call sites ~25), ~280 test | +2 settings. **Seven duplicated copies of the shield-absorb rule collapse to one.** |
| 4 | ~135 production | ~70 production, ~70 test | +1 setting. |
| **Total** | **~439** | **~600 production, ~600 test** | |

Net production about +160. The account: it buys the invariant that the simulation
resolves combat without Unity — `docs/three-gates-scope.md:101-110` calls today's state
a velocity compromise — and it collapses seven copies of the absorb rule and one copy of
the damage rule into single owners. It does **not** buy that with a registry, an adapter,
a mode flag or a compatibility layer: the old path is deleted in the cut that replaces
it, and no cut leaves two writers of damage.

The line count is the wrong thing to stare at here. The structural delta is: one new
owner (`FireControl`), one new subsystem (`TargetingSystem`), one field on catalog data,
four settings, and **minus one whole category of authority** — Unity physics deciding
gameplay.

---

## 6. Build budget

- **Packages and targets.** `Aetheria.Shared/Aetheria.Shared.csproj` (netstandard2.1),
  `tests/Aetheria.Shared.Tests/Aetheria.Shared.Tests.csproj` (net10.0, xunit),
  `tools/AetherDb/AetherDb.csproj`. Three projects, no new ones. Debug only; no release
  profile, no feature flags, no code generation, no new target platform.
- **Build host and target are the same machine**: a Windows workstation producing
  managed assemblies for the same runtime. Nothing here is cross-platform, so no claim
  about a Linux artifact is made or needed.
- **Where.** `F:\Projects\Aetheria-stats` (the free worktree; it must first be advanced
  from `c4f6029e` to HEAD). Never the main tree — the operator plays there. Never
  `F:\Projects\Aetheria-tiers`. CultLib via
  `-p:CultLibRoot=<scratchpad>\cultlib-45c2f40`, the shared pinned worktree, which is
  never removed. `--artifacts-path` into scratch so no `bin`/`obj` lands in a working
  tree.
- **Footprint.** Three managed projects plus their test host; on the order of a few
  hundred MB of artifacts, comparable to the stats campaign. No expected growth: this
  campaign adds no project, package reference or native component.
- **Unity.** Batchmode compile check with the editor closed, per
  `docs/item-provenance-cut.md` §6, once per cut that touches `Assets/Scripts/Gameplay`
  (Cuts 1, 3, 4). The operator's play smoke is the only Unity *run*. Imagination did not
  run Unity and did not build.
- **Mutation harnesses.** One per cut, in the established shape
  (`tests/mutation_tests_fire_control_cut1..4.py`): patch a named anchor, run
  `dotnet test tests/Aetheria.Shared.Tests --cultlib-root <pin>`, assert the named test
  goes red, with a no-op control. Note the scar from `8dddda4d`: later cuts move earlier
  anchors, so Cut 4 re-anchors Cuts 1-3's harnesses before it lands.

---

## 7. Seams this campaign names and stops at

- **Presentation of shots.** `Zone.ShotCommitted` and `Zone.ShotResolved` are the whole
  contract. What a projectile, panel, whip or near-miss *does* with a commit belongs to
  `docs/shield-presentation-contract.md` and the panel and Lariat maps. This campaign
  guarantees only that the commit exists, is authoritative, arrives `CommitHorizon`
  before impact, and carries what the contract says it carries: what happens, where,
  when it arrives, and which capability answers it — not who fired, not damage numbers.
  The contract's open question "where the commit window lives" is answered here:
  `FireControl`, with the horizon in `GameplaySettings`.
- **Death, removal and loot.** `Entity.Death` is already composed in `ServerShared`
  (`Entity.cs:363-366`), but `Zone.Entities.Remove` and the loot roll live in Unity
  (`EntityInstance.cs:411-437`) and use `UnityEngine.Random`. Fire control makes a
  headless kill *happen*; it does not make the corpse go away. Q3.
- **Pickup.** `ShieldManager.cs:40-51` stores loot on trigger contact. Untouched.
- **Ship collision, `TractorBeam`, mine placement.** Deferred Unity-physics surfaces
  (`docs/three-gates-scope.md:98-99`). Untouched.
- **The navigation planner.** R3 makes manoeuvring evasion, which changes what a good
  flight path is. The planner is a separate item in the shipping scope and this map does
  not anticipate it.
- **Stance and IFF.** Ruled and partly built (`Weapon.StanceAllowsFire`,
  `Weapon.cs:101`). Fire control reads the gate and does not redesign it.
- **Faction territory.** The campaign after this.

---

## 8. Operator questions

Most blocking first.

**Q1. What makes a turret 360?** R6 says turrets are 360 degrees, but a turret hull's
`Direction` never rotates — `TurretController` writes only `LookDirection` — so under a
120-degree default a turret would only engage within 60 degrees of its spawn facing.
**A:** author `FiringArc: 360` on the `Turret` hull's hardpoints; the override R6 already
grants does the whole job and no code knows what a turret is. **B:** add
`HardpointType.Turret` and give it an implicit 360. **C:** give the arc to the hull
rather than the hardpoint. **Recommended: A**, because it buys the invariant with
authored data instead of a new type, and because a future partially-traversing turret
(a 270-degree sponson) is then already expressible. B makes the arc a property of a
category and loses that.

**Q2. Is player fire arc-gated?** Today it is not gated at all; the player fires whatever
is equipped, wherever they point. Cut 1 makes the arc a shared rule. **A:** yes, gate the
player exactly as the AI — a side-mounted gun simply does not fire forward, and the HUD
shows which groups bear. **B:** gate the AI only, leaving the player's rear gun able to
shoot forward. **Recommended: A.** Under R1 aiming is gone, so what the player does with
a weapon group *is* the decision, and arcs are what make that decision interesting. B
also splits the shared path the doctrine cares about: manual and programmatic fire would
be two truths. It is a real difficulty change and should be felt in the smoke before the
campaign closes.

**Q3. Does this campaign move death removal and loot, or leave them in Unity?** A
headless kill currently leaves the corpse in `Zone.Entities`, because `Entities.Remove`
is at `EntityInstance.cs:437`. **A:** move removal and the loot roll into `Zone` in
Cut 3, using `ItemManager.Random` — about 50 lines, and it makes a headless kill a
complete event. **B:** move only the removal; leave loot to the run-structure work.
**C:** leave both; fire control's tests assert on durability, not on removal.
**Recommended: B.** Removal is the half without which the simulation is lying about its
own contents, and a seeded loot roll pulls in drop velocity, bay capacity and pickup —
the run-structure campaign's material. A is more complete but widens this campaign; C
leaves `ServerShared` unable to say who is alive.

**Q4. Three balance numbers need first values:** `CommitHorizon` (R4's "short fixed
time"), `SchematicCellSize` (metres per schematic cell, which sets how much target size
matters), and `UnaidedAccuracy`. **A:** Hands picks defaults (0.3s, measured off the
LonginusX prefab, 0.35) and the operator tunes them in the smoke. **B:** the operator
states them now. **Recommended: A** for the first two and **B for `UnaidedAccuracy`**,
because it is not a tuning number — it decides whether a targeting system is a
requirement or an upgrade, which is a design ruling. If it is low, Cut 2's required-item
rule is load-bearing; if it is high, the subsystem is a nice-to-have and Cut 2 shrinks.

**Q5. What seeds the rolls?** `ItemManager.Random` is clock-seeded
(`ItemManager.cs:19`), so `RollsAreSeeded` cannot pass without a seed argument.
**A:** add the seed parameter here, as a two-line change. **B:** wait for the `Run`
lifecycle work that owns it (`docs/headless-playground-cut.md` Cut 1, unlanded).
**Recommended: A.** A reproducible fight is how this campaign is tuned and how a
regression is ever found again; waiting on an unstarted cut for two lines is the
compensator pattern in miniature.

**Q6. When is the snapshot taken — fire, or commit?** `docs/stats-and-power-cut.md:1020`
says "at commit time". **A:** one snapshot, at fire: the shot carries what left the
barrel, and only deviation is measured up to the commit. **B:** two snapshots — payload
at fire, targeting stats at commit — so a targeting system knocked out mid-flight
degrades the shot already in the air. **Recommended: A.** One rule, one freeze point, and
over a horizon measured in tenths of a second B's extra fidelity is invisible while its
extra state is permanent. B is the more simulationist answer and is worth having if the
operator wants killing the gunnery computer to spoil shots already fired.

**Q7. Mines.** Cut 4 moves mine *damage* to `FireControl.Splash` but leaves
`Mine.cs:68`'s `OverlapSphere` for proximity triggering, which is contact detection
rather than hit detection. **A:** leave it, alongside `TractorBeam` and ship collision,
as a named deferred physics surface. **B:** move proximity into `Zone` too, so the last
`Physics.*` in the weapons directory goes. **Recommended: A.** No design ships a mine,
and B buys a clean grep rather than an invariant. Revisit when a mine is authored.

---

## Cut 5: the rules that landed in one path and not its twin

Date: 2026-09-19. Branch `codex/fire-control-5` off `ca57819d`.

Soul's pass over the whole campaign (2026-09-19) confirmed eleven findings. Most
of them are the same shape: a rule this map declared, implemented on one path,
and left off the path beside it. Splash breaks a shield the discrete roll does
not. `InstantWeapon` gates on arc and `ConstantWeapon` does not. Unity removes a
corpse from the zone and the simulation does not. This cut closes the twins.
Catalog authoring (Soul's findings 1 and 2) is Cut 6; the shared-RNG stream
(finding 6) and airburst (finding 5) are Cut 6 as well; the missing mutation
harnesses (finding 8) are Cut 7.

### 5.1 Tracking is a floor, not a cliff (Soul finding 3)

`FireControl.cs:102-106` falls back to `Tracking = 0` with no targeting system,
and `:273-275` turns that zero into `deviation < .01f ? 1f : 0f`. A probe fired
334 shots at a target drifting at 5 u/s with `Accuracy` forced to 1 and landed
none. Q4 asked for unaided fire to be really, really bad; this is a wall.

- `Settings.cs`, beside `UnaidedAccuracy`: add `public float UnaidedTracking = 10f;`
  with the same authoring note. Ten world units of deviation forgiveness is the
  first guess and the operator's knob: a target that has strayed five units from
  its fire-time projection halves an already-5% chance.
- `FireControl.Tracking` falls back to `settings.UnaidedTracking`, exactly as
  `Accuracy` falls back to `UnaidedAccuracy`. One shape for both.
- `Commit` then deletes the zero branch outright: `p *= saturate(1f - deviation / shot.Tracking)`.
  Tracking is now always positive, so the branch protects nothing.

**Authority:** `Settings` owns the unaided figures; `FireControl.Tracking` is the
only reader; `Commit` no longer carries a special case for a value that cannot occur.

### 5.2 A shot the shield cannot absorb breaks it (Soul finding 4)

`Commit:299-302` sets `Shielded` only when `CanTakeHit` passes, and `Apply:311-326`
routes the rest to the hull -- neither calls `Break()`. `Splash:365` does. The
deleted Unity path did (`git show b7743789^:Assets/Scripts/Gameplay/Weapons/Projectile.cs`).
So the F4/F5 shield-break mechanic is unreachable for every weapon in the catalog:
an unabsorbable 5000-damage hit leaves `Broken == false` and the shield absorbs the
very next shot, refilling at `RefillDuration` instead of the punitive `RestoreDuration`.

- `ShotOutcome` gains `public bool ShieldBroken;`.
- `Commit` decides it where it already asks `CanTakeHit`: shield present, active,
  and `CanTakeHit` false means `ShieldBroken = true`. The decision is frozen with
  the rest of the outcome (R4) -- a shield that recharges during the flight does
  not retroactively survive.
- `Apply` performs it: `if (shot.Outcome.ShieldBroken) shot.Target.Shield.Break();`
  before the hull damage. `CanTakeHit` is still called exactly once per shot.
- Presentation gains the signal for free; nothing is required to read it yet.

### 5.3 Continuous weapons obey their arc (Soul finding 9)

`ConstantWeapon.cs:91,99` gate on `StanceAllowsFire` alone. A side-mounted beam
fires forward. Q2 says manual and programmatic must not be two truths; instant and
continuous currently are.

- Both gates become `StanceAllowsFire && ArcAllowsFire`, matching `InstantWeapon.cs:105`.

### 5.4 One bearing test, including at zero range (Soul finding 11)

`Weapon.cs:117` allows a zero-length bearing; `FireControl.InArc:47` normalizes the
zero vector into NaN and `HitProbability` returns 0. The player's trigger says fire
and the probability says impossible, so AI and turrets refuse to fire at a
co-located target.

- `InArc` takes the rule: a planar bearing shorter than `1e-6` returns true --
  point-blank cannot fail a bearing test.
- `Weapon.ArcAllowsFire` drops its own `lengthsq` special case and simply calls
  `InArc`. One owner, one answer.

### 5.5 A destroyed subsystem stops being the aim point (Soul finding 10)

A destroyed item is never removed from `Equipment` (`Entity.cs:475` only observes
it), so `IsRevealed` still ranks it and `ResolvedTargetItem` still returns it --
`Precision` keeps concentrating hits onto a dead item's footprint.

- `FireControl.IsRevealed` returns false for an item whose
  `EquippableItem.Durability < .01f`, and the ranking excludes destroyed items so
  the reveal tiers of the survivors close up rather than leaving a gap.
- `Entity.ResolvedTargetItem` therefore drops a destroyed aim point on its next
  read. Nothing caches it, so no clearing pass is needed.

### 5.6 Death removes the ship, in the simulation (Soul finding 7)

Operator ruling Q3 says death removes the ship. `Zone.Entities.Remove` for death
exists only at `EntityInstance.cs:324`, inside Unity's loot-drop subscription. So
headless, `Step`'s `targetGone` guard never fires, shots land on corpses, `Splash`
keeps hitting them, the AI keeps engaging them, and the tuning harness this map
calls its arbiter cannot end a fight. `DeadEntityStopsTakingShots` passes only
because it calls `Entities.Remove` by hand.

- `Zone` subscribes each entity's `Death` observable when the entity joins, and
  removes it from `Entities` on death. The simulation owns the corpse.
- `EntityInstance.cs:324` deletes its `Entities.Remove` call and keeps the loot
  drop and destroy effect, which are its own business. Forbidden writer: no
  presentation removes an entity from the zone.

### 5.7 Delete the two carried-and-unread fields (Soul finding 12)

`PendingShot.PredictedIntercept` and `PendingShot.FlightTime` are written at
`FireControl.cs:204-205` and read nowhere. The intercept in particular is
carry-weight from Cut 3's named risk: `Combat.cs:109` and `Fire` do share
`FireControl.PredictedIntercept`, but the roll judges deviation against a
straight-line projection from fire position, so the stored copy decides nothing.

- Both fields are deleted from the struct. The local `flightTime` stays; it still
  computes `ArrivalTime` and `CommitTime`.

### Verification

Each of 5.1-5.7 gets a test that fails under its own mutation, and the mutation
harness is committed as `tests/mutation_tests_fire_control_cut5.sh` with a no-op
control. In addition this cut pins four of the five mutations that survived 197
green tests in Soul's pass, because they are rules 5.1-5.6 touch:

- `FireControl.cs:365` -- delete `shield.Break()` from `Splash`. Must die.
- `Weapon.cs:117` -- `ArcAllowsFire => true`. Must die.
- `InstantWeapon.cs:105` -- delete the `if (!ArcAllowsFire) return;` player arc gate
  (Q2, the ruling Cut 1 deferred to Cut 3 and nothing verifies). Must die.
- `FireControl.cs:270` -- `elapsed = shot.FlightTime` in place of `now - shot.FireTime`.
  This is the map's own declared mutation for `EvasionCountsUntilCommitAndNotAfter`,
  and it survives because that test's fixture gives the target zero velocity, so
  `FireTargetVelocity * elapsed` is identically zero and the mutation is a no-op
  inside it. Give the fixture a moving target. (After 5.7 deletes `FlightTime` the
  mutation's spelling becomes `elapsed = shot.ArrivalTime - shot.FireTime`; the rule
  it attacks is the same.)

The fifth survivor -- freezing `source.TargetItem.Value` instead of
`ResolvedTargetItem` at `FireControl.cs:193`, bypassing the reveal re-check -- is
Cut 7's, alongside the missing Cut 1-3 harnesses.

Negative checks: no `Entities.Remove` for death outside `Zone`; no `Tracking` read
that can return zero; no `PredictedIntercept` or `FlightTime` field on `PendingShot`.

---

## Cut 6: the dice stop being shared, and the flak cannon works again

Date: 2026-09-19. Branch `codex/fire-control-6` off Cut 5.

Cut 6a (catalog authoring, worktree `Aetheria-catalog`, branch
`codex/fire-control-6-catalog`) runs in parallel and is specified by Soul's
findings 1 and 2 plus the Cut 2 authoring spec above. This section is Cut 6b, the
two remaining code findings.

### 6.1 A shot's dice belong to the shot (Soul finding 6)

`Commit:266,304` draws from `ItemManager.Random`, a single stream shared with
`ActionGameManager.cs:552,558,567,686,787` (wormhole exit velocity, loadout
generation, travel) and `Combat.cs:144` (`SampleDps`, drawn per AI per
evaluation). Q5's reproducible fight therefore holds only inside a hermetic
fixture: in play, adding an NPC or opening a shop changes every subsequent die.
Soul's determinism probes passed for exactly that reason, which makes them a
demonstration of the fixture rather than of the rule.

The fix is a deletion, not a second stream. A stream makes the roll depend on
*how many* draws came before it; what this campaign actually promised is that a
shot's outcome is a function of the shot. So make it one.

- `Zone` exposes `public uint CombatSeed { get; }`, set in the constructor from
  the same expression that already seeds `_random`:
  `galaxyZone?.Name.StableHash() ?? 1337u`. One zone, one stable identity — a
  galaxy seed reproduces it, and nothing else in the zone can perturb it.
- `Commit` stops touching `ItemManager.Random` entirely. It builds its own local
  `CultMath.Random` from the zone seed and the shot's own id:
  `var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);`
  The `| 1u` guards the degenerate zero seed. Every draw the commit makes — the
  hit roll, the precision roll, the cell pick — comes from that one local
  generator, and it dies with the call.
- The write-back at `:304` (`shot.Source.ItemManager.Random = random;`) is
  deleted with it.

**Authority:** the roll's randomness is a pure function of `(zone identity, shot
id)`. **Forbidden writers:** nothing outside `Commit` may seed or advance a
combat draw, and `Commit` may not read a shared stream. **Consequence worth
stating:** two shots that happen to share a `ShotId` across two zones roll
differently, because the zone seed differs; two runs of the same fight from the
same galaxy seed roll identically no matter what the UI did in between, which is
the promise Q5 actually made.

### 6.2 Airburst resolves in the simulation (Soul finding 5)

Cut 4 deleted `Projectile.cs`'s `OverlapSphere`/`SendSplash` and added
`FireControl.Splash` "because R9 keeps the flak cannon and the mine". Only the
mine was wired (`Mine.cs:99`). `Projectile.cs:16-17` still carries
`AirburstDistance`/`AirburstRange`, still written by `ProjectileManager.cs:32`,
read by nothing — so a flak round flies and does nothing. The stale comment at
`Projectile.cs:7-8` calls airburst "Cut 4's — untouched here".

The wiring does **not** go back into `Projectile`. R8 says presentation does not
decide damage, and a projectile MonoBehaviour calling `Splash` would be exactly
the authority Cut 3 spent itself deleting. Airburst is a property of the weapon,
so the simulation resolves it:

- `PendingShot` regains a burst point — `public float3 BurstPosition;` — frozen
  at fire like every other payload field, from `PredictedIntercept` (which Cut 5
  deletes as unread; this is its live consumer, and it comes back named for what
  it is rather than for what computed it).
- `PendingShot` gains `public float BurstRadius;`, frozen from the weapon's
  authored airburst range, and zero for a weapon without the `Airburst` flag
  (`Enums.cs:101`).
- `FireControl.Fire` sets both when the weapon's `WeaponData` carries `Airburst`.
- `FireControl.Step`, at arrival, resolves an airburst shot by calling
  `Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType)`
  **instead of** `Apply`. An airburst round does not also roll a discrete hit:
  it is an area effect, which is the whole point of the flag, and applying both
  would be the double-application Soul was told to hunt for.
- `Projectile.cs` deletes `AirburstDistance` and `AirburstRange` and the stale
  comment; `ProjectileManager.cs:32` deletes the line that wrote one. The
  projectile's remaining job is to fly and disappear, which is all presentation
  should have been doing.

**Open tuning question, recorded not asked:** the authored airburst radius has no
sibling in the catalog to argue from, because no product has ever used it. Cut 6a
or a later authoring pass sets it; until then the flag is wired and unexercised,
and the report must say so rather than implying a working flak cannon.

### Verification

Committed harness `tests/mutation_tests_fire_control_cut6.sh`, no-op control
required, same byte-exact I/O rules as Cut 5.

- `SameFightRollsSameThroughUnrelatedDraws` pins 6.1: run a fixed fight twice from
  one zone seed, drawing an arbitrary number of values from `ItemManager.Random`
  between the two runs, and the outcomes must be identical. Mutation: restore the
  `ItemManager.Random` draw. Must die — and note that this is the test Soul's own
  probe could not be, because a hermetic fixture cannot see the defect.
- `ShotIdDecidesTheDie` pins that the roll is a function of the shot: two shots
  with the same frozen payload and different ids may differ; the same id in the
  same zone always gives the same result. Mutation: drop `ShotId` from the seed.
- `AirburstSplashesAndDoesNotAlsoRoll` pins 6.2: an airburst shot damages
  everything in its radius, and its target takes area damage rather than a
  discrete cell hit. Mutation: call `Apply` as well as `Splash`. Must die.
- `NonAirburstNeverSplashes` pins the other side. Mutation: splash unconditionally.
- negative: `ItemManager.Random` appears nowhere in `FireControl.cs`;
  `Airburst` appears nowhere in `Assets/Scripts/Gameplay`.

---

## Cut 6c: the formula gets fixed, and the single point of failure

Date: 2026-09-19. Branch off Cut 6a (`e5f1318a`) once Cut 5 has merged — this
touches `FireControl.cs`, which Cut 5 owns until then.

Cut 6a landed the catalog data that Cut 2 never shipped: 53 designs, 43 products,
`loadout` green on ten seeds, turret arcs migrated. Reviewing it turned up two
defects it introduced and one it inherited.

### 6c.1 Resolution becomes reciprocal, so the name stops lying

`HitProbability` computes `pSensor = saturate(unlerp(TargetDetectionInfoThreshold, Resolution(source), info))`.
With the live `TargetDetectionInfoThreshold` of `.1`, a **higher** Resolution
means **more** gathered info is needed before sensor state stops limiting hits.
Resolution is a cost. The field's own comment says so accurately -- "the info
level at which sensor state stops limiting hits" -- and the word "Resolution"
says the opposite, because in every other context a higher resolution is a
better instrument.

Cut 6a authored the 1-cell `Targeting Computer` at `.3` and the 2-cell
`Fire Control Array` at `.75`. Against a target scanned to info `.3`, that gives
the cheap design `pSensor = 1.0` and the 150000c design `pSensor = 0.31`. The
premium item is strictly worse until the player has scanned hard, which is not a
tradeoff anyone authored -- it is the formula misleading an author who had read
it and said so in the same paragraph.

Operator ruling, 2026-09-19: "When the formula is unintuitive, we don't make the
handle more opaque for correctness, we fix the formula. Plenty of core gameplay
formulas are reciprocal just for this reason." An earlier draft of this cut
proposed renaming the field to `SensorDemand`; that is superseded. The name
stays `Resolution`, higher stays better, and the formula takes the reciprocal:

- `HitProbability` derives the ceiling rather than reading it directly:
  `var demandCeiling = detection + (1f - detection) / max(Resolution(source), 1e-3f);`
  then `pSensor = saturate(unlerp(detection, demandCeiling, info))`.
- Resolution `1` therefore means "needs complete information before sensor data
  stops penalising this system" -- which is precisely what the unaided fallback
  of `1f` should mean. That fallback stays as it is and is now the floor by
  construction instead of by coincidence. The guard against a zero or negative
  authored Resolution is the `max`, and no authored value should ever reach it.
- Re-author both designs on the new scale: 1-cell `2` (sensor-limited until info
  reaches `.55`), 2-cell `4` (until `.325`). Both comfortably better than unaided,
  the premium one better than the cheap one, and the numbers now read the way a
  designer reaching for the knob expects.
- `TargetingSystemData.Resolution`'s comment gains one sentence naming the
  direction and the reciprocal, so the next author does not have to derive it.

### 6c.2 One seller is a die roll against ship generation

Cut 6a sold both targeting designs from NiteLife Energy alone, satisfying the
Cut 2 spec's letter ("at least the manufacturers that sell the capacitor") while
missing what that clause was for. `Galaxy.cs:109` selects
`factions.OrderBy(random).Take(settings.MegaCount)` — a **random subset** of the
twelve factions — plus the quest faction and the authored neutrals. A galaxy that
does not draw NiteLife has no targeting product, `IsAvailable`
(`LoadoutGenerator.cs:154`) filters every candidate out, and
`FillInterior`'s `required: true` throws `InvalidLoadoutException` again. Cut 6a's
ten green seeds do not disprove this; they sample the draw.

Two changes, because content coverage and structural fragility are different
problems and only one of them is fixed by authoring:

- **The generator degrades instead of throwing.** A `required: true` interior
  item with no available product leaves the entity without one rather than
  failing generation outright. This is only safe *because* Cut 5.1 gave unaided
  fire a floor: such a ship now fires badly instead of not at all. The generator
  logs the gap so it surfaces as content debt rather than silence, and `AetherDb
  loadout` reports it per seed.
- **Thematic sellers, which also buy coverage.** NiteLife Energy is the roster's
  power-generation faction; targeting belongs to Finch Cybernetics (passive
  sensors) and Lucent Media (active sensors), both of which the roster names for
  exactly this. Each design gains products from both. That is three sellers per
  design, and it puts the item in the hands of the factions whose brand explains
  why they make it.

### 6c.3 Recorded, not fixed

`Large Drive`'s new product declares no roles because the design declares none.
That is honest, and it is also a design with no role axes in a game where roles
are how products differentiate. It belongs to the content pass, not here.

### Verification

- A test pins 6c.1's direction: two entities, identical but for `Resolution`, at
  the same info level -- the **higher** Resolution has the higher
  `HitProbability`, at every info level between detection and full. Mutation:
  drop the reciprocal and read Resolution as the ceiling directly (the shipped
  behaviour). Must die. This is the assertion whose absence let a premium item
  ship strictly worse than a cheap one.
- A test pins 6c.2: a galaxy containing no seller of any targeting design still
  generates every hull, and those entities resolve `UnaidedAccuracy`. Mutation:
  restore the throw. Must die.
- `AetherDb loadout` across at least 50 seeds, plus a run with the faction set
  forced to exclude all three sellers. Report the gap count, not just "no throw".
- Census unchanged at 53 designs; products rise by 4 (two more sellers per design).

---

## Cut 7: the harnesses that were prose

Date: 2026-09-19. Branch `codex/fire-control-7` off Cut 6.

`git log --all -- tests/mutation_tests_fire_control*` returns exactly one commit,
`034e8d3e`, adding `cut4` alone. §6 of this map declares `cut1..4`. So the Cut 3
mutation table above (seeding, the commit horizon, the frozen snapshot, the
shield branch, the penetration march, the roll itself) is prose: it names the
mutations that should die and nothing has ever run them.

That is not a paperwork gap. Soul wrote five mutations against rules this map
claims to pin and **all five survived 197 green tests**, including
`InstantWeapon.cs:105`, which is operator ruling Q2 — the one thing Cut 1
explicitly deferred to Cut 3. Cut 5 pins four of them because it touches their
rules. This cut pins the fifth and builds the harnesses that should have existed
since Cut 1, so that every verdict this campaign has recorded becomes reproducible
rather than remembered.

### 7.1 The fifth survivor: the reveal re-check

`FireControl.cs:193` freezes `Aimed = source.ResolvedTargetItem`. Mutating it to
`source.TargetItem.Value` bypasses the reveal re-check — you could aim at a
subsystem you have lost resolution on — and 197 tests stayed green.

- A test pins that an aim point whose reveal has decayed below its tier is **not**
  frozen into the shot: the shot still fires, at a random cell, with no `Aimed`.
  Mutation: read the raw `TargetItem.Value`. Must die.
- Note the interaction with Cut 5.5: a *destroyed* aimed item must fall out by the
  same path, through `IsRevealed`, not through a second check. If pinning this
  needs two tests, the rule has two owners and that is itself the finding.

### 7.2 Harnesses for Cuts 1, 2 and 3

Three committed scripts — `tests/mutation_tests_fire_control_cut1.sh`, `cut2.sh`,
`cut3.sh` — each with a no-op control, byte-exact I/O, anchors matching exactly
once, and a reverse-write restore. Each runs the mutation table its cut already
declares in this document; do not invent a new table, and do not quietly
substitute an easier mutation for one that will not die.

**Every survivor is a finding to report, never a test to weaken.** The expected
cause, seen twice in this campaign already, is a fixture too weak to distinguish
the mutant: `EvasionCountsUntilCommitAndNotAfter` survived its own declared
mutation because its target had zero velocity, so the sabotage was arithmetically
a no-op. When a mutation survives, first ask whether the fixture differs from the
rule's actual precondition, fix the fixture, and say so in the report.

Where a declared mutation genuinely cannot be reached by the current suite, write
it up as **not yet reached**, never as unreachable, and name what reaching it
would take. Both times that call was made elsewhere in this project it was wrong,
and a fifteen-line probe killed the mutant.

### 7.3 What the harnesses are allowed to assume

Cut 6a authors the first targeting-system designs into the catalog. A harness that
depends on catalog content is a harness that breaks when content changes, so
these suites build their own fixtures through the same synthetic-upsert path
`TargetingSystemTests.cs:368-403` already uses. That file is also a cautionary
example: it passes by upserting a synthetic design into a throwaway cache, which
pins the generator and says nothing about whether the shipped catalog can
generate a ship. Both are worth having; only one of them was, and the map claimed
the other.

So: **one test in this cut asserts against the live catalog** — that
`AetheriaStores.Open` over `GameData/Aetheria.cc` yields a targeting system the
player's own hull can be generated with. That is the assertion whose absence let
Cut 2 ship code without data.

### Verification

- All four harnesses (cut1-3 plus the existing cut4) run clean: every declared
  mutation dies, every no-op control leaves the suite green.
- The report carries the full table — cut, mutation, died/survived, and for any
  survivor the fixture defect behind it.
- `dotnet test` green, with the count.

---

## Cut 6d: where a hit lands is a dart throw, not a coin flip

Date: 2026-09-19. Operator direction, 2026-09-19.

### The defect

`FireControl.Commit` places a hit like this:

```csharp
if (aimedCells != null && aimedCells.Length > 0 && random.NextFloat() < shot.Precision)
    cell = aimedCells[random.NextInt(aimedCells.Length)];
else
    cell = coords[random.NextInt(coords.Length)];
```

`Precision` is a coin flip between *exactly on the aimed item* and *uniformly
anywhere on the hull*. Aim at a stern thruster with Precision `.35` and the 65%
of hits that miss it are spread evenly over the bow, the cockpit and everything
else — no nearer the thruster than if the player had never aimed at all. There is
no spatial term anywhere in it.

This is the same shape as 5.1's Tracking cliff: a quantity that should be a
gradient implemented as a binary, so the failure case is maximally wrong rather
than slightly wrong.

Operator, 2026-09-19: "I would presume that aiming for an item in the stern would
still tend hit in that general area even if it misses the item. Like throwing a
dart at the grid."

### The rule

One Gaussian kernel over the target's hull schematic, in cell units:

- **Aim point.** The centroid of the aimed item's cells, or the hull's
  `CenterOfMass` when nothing is aimed at. One path — the uniform-random branch
  is deleted, not demoted.
- **Sigma.** Derived from the frozen `shot.Precision`, higher Precision giving a
  tighter group. Authored so that the unaided floor (`UnaidedPrecision`, joining
  `UnaidedAccuracy` and `UnaidedTracking` in `GameplaySettings`) is a sigma wide
  enough to read as spraying at the silhouette.
- **Weights.** `w(cell) = exp(-d² / 2σ²)` for each occupied hull cell, `d` the
  distance from the aim point.

### The tradeoff, and why it cannot be a second miss stage

Operator, 2026-09-19: "It would also mean that aiming can be a tradeoff; if
you're not precise enough, you're better off aiming center mass than risking
misses on a thin limb."

That tradeoff only exists if a stray dart can miss the ship. An earlier draft of
this section clamped a stray to the nearest occupied cell, which destroys it:
aiming at a wingtip would cost nothing and merely redistribute damage inward.

But resolving the stray as a *second* miss stage would break R3 — one roll
decides damage — and make `HitProbability` a lie, since a shot could pass the
roll and then fizzle on placement. So the off-hull mass is priced into the single
roll instead:

```
pOnHull = Σ w(cell) / (2πσ²)
```

the share of the kernel's total mass that lands on metal.

- `HitProbability` gains `pOnHull` as a factor. Aiming at a thin limb with a
  loose group drops the number **on the HUD, before the trigger** — the tradeoff
  is legible at decision time rather than discovered afterwards.
- `Commit` draws the cell as a weighted pick over the same `w(cell)`. A hit
  always lands on metal, because the chance of not landing on metal was already
  charged at the roll.

One kernel, computed by one function, used for both the probability and the
placement. Cut 3's named risk was two functions answering "where will this shot
go" and eventually disagreeing; this does not reintroduce it.

`pOnHull` depends only on the aim point, the hull shape and the frozen
`Precision`, so it folds into `PBase` at fire time and the freeze discipline
(R10, Q6) is unchanged.

### Consequences, named rather than discovered

- **Every hit in the game moves**, not only aimed ones: unaimed fire becomes
  "aim at the centre of mass with the worst sigma", so big central sections soak
  more fire than extremities. That is the intended change, and it is a balance
  change.
- **A fat hull is inherently easier to hit than a needle**, for free.
- **The two targeting products' authored `Precision` values (`.05-.2` and
  `.35-.55`) become meaningless as written**, because they are probabilities and
  this makes Precision a grouping tightness. They are re-authored in the same
  pass as Cut 6c's reciprocal `Resolution`.
- **Double-charging risk.** `HitProbability` already carries `pSpread`, which
  penalises a small target against a wide weapon spread. `pOnHull` also
  penalises small targets. Check whether the two charge the same thing twice
  before authoring numbers; if they do, one of them owns it and the other goes.
- **AI willingness shifts.** `Combat.cs` and `TurretController.cs` gate on
  `AgentMinHitProbability` against this number, so NPCs will hold fire more than
  they used to the moment `pOnHull` lands. That is a tuning pass, not a bug, but
  it will show up in the first smoke and should not be mistaken for broken AI.

### Verification

- `AimingAtTheSternHitsTheStern`: with a mid-range Precision, hits aimed at a
  stern item land in the stern half far more often than the bow half. Mutation:
  restore the uniform-random branch. Must die. This is the assertion the shipped
  coin flip would fail.
- `ThinLimbCostsHitChance`: `HitProbability` against an aim point on a
  single-cell extremity is strictly lower than against the centre of mass, at the
  same Precision — and the gap widens as Precision falls. Mutation: drop
  `pOnHull` from the probability. Must die.
- `PlacementAndProbabilityShareOneKernel`: the empirical on-hull rate over many
  seeded draws matches the `pOnHull` the probability reported, within tolerance.
  Mutation: perturb sigma in one of the two call sites only. Must die — this is
  the test that would catch the two functions drifting apart.
- `EveryHitLandsOnMetal`: no committed hit ever resolves to an unoccupied cell,
  across seeds and aim points including extremities.

### 7.4 Amendment (2026-09-19): the live-catalog test has a second job

7.3 above asks for one test that opens the real `GameData/Aetheria.cc` through
`AetheriaStores.Open`, because Cut 2 shipped a `required: true` rule with no data
to satisfy it and every green suite in the campaign missed it.

Merging Cuts 6b and 6c gave that test a second and larger job. 6b added
`WeaponItemData.AirburstRange` as a non-nullable `float` at a new MessagePack
key. Every weapon record in the shipped catalog was serialized before that key
existed, so the reader was handed `nil` for the absent slot, a `float` cannot
take nil, and `AetheriaStores.Open` threw outright — the game could not read its
own catalog. Fixed at `c896489b` by making the field `float?`.

Neither cut could have caught it. 6b verified with unit tests over synthetic
fixtures and never opened the shipped catalog; 6c opened the catalog constantly
but on a branch without the field. The defect existed only in the merge, and
only when something read real data.

So the live-catalog test is not a content check that happens to touch a file. It
is **the campaign's only assertion that the shipped data is readable by the
shipped code at all**, and it is the one test whose absence let a branch that
bricks the catalog pass every gate. Write it so it fails loudly on a
deserialization throw, not only on a missing design:

- `ShippedCatalogOpensAndGeneratesAnArmedHull`: open the real catalog read-only
  through `AetheriaStores.Open`, enumerate every `EquippableItemData` so every
  document type is actually deserialized (a lazy read that never touches
  `WeaponItemData` would have passed on the broken merge), and generate a
  loadout for `LonginusX`. Any exception is a failure.
- Mutation: none needed for the throw path — the shipped catalog either
  deserializes or it does not. For the generation half, the Cut 2 mutation
  applies: drop the targeting designs' products and the test must go red.

**The standing rule this cut records**, because it will recur every time a field
is added: a new non-nullable value-type field on a persisted CultCache document
cannot read records written before it existed. Nullable, or a migration that
rewrites every affected record — and the migration cannot run while the read
still throws, so tolerant deserialization comes first either way.

---

## Cut 8: a removed entity is a dead entity, and the anchors the diagnostics moved

Date: 2026-09-22. Branch `codex/fire-control-8` off `72c0109c`.

Operator play smoke, 2026-09-20: heat spike on entry with the ship unable to
move, a nearby ship likewise glowing hot and motionless, shots that registered no
hits, and finally a NullReferenceException out of `LockWeapon` reaching
`Entity.EntityInfoGathered` as the other ship vanished. Diagnosis (Codex, relayed
by the operator): the other ship's behaviors updated after the entity had been
cleaned out. Not reproducible afterwards, which makes it a hidden bug rather than
a fixed one -- the diagnostics commit `72c0109c` changed timing and allocation,
not the cause.

### 8.1 Removal and deactivation are one transition

`Entity.Deactivate` (`Entity.cs:445`) disposes every subscription, clears
`EntityInfoGathered`, `VisibleEntities`, `VisibleEnemies` and `VisibleFriendlies`,
and sets `_active = false`. `Zone.TryDock` (`Entity.cs:943`) has always paired the
two: `Zone.Entities.Remove(ship); ship.Deactivate();`. Removal from the zone means
the entity stops living there.

Cut 5.6 added the death path and did only half of it. `Zone.cs:78`:

    Entities.ObserveAdd().Subscribe(add => add.Value.Death.Subscribe(_ => Entities.Remove(add.Value)));

The entity leaves the collection and stays **active**, holding live subscriptions
and stale references, while every surviving entity's `ObserveRemove` handler
(`Entity.cs:200-213`) nulls its `Target` and drops it from `EntityInfoGathered`,
`EntityHostility` and the visibility sets. Half the world has forgotten it and it
does not know it is dead.

- The death subscription becomes `Entities.Remove(e); e.Deactivate();`, matching
  `TryDock`'s existing pairing exactly. One transition, one owner.
- **Ordering risk to verify, not assume:** `EntityInstance` also subscribes to
  `Entity.Death` (its loot drop and destroy effect, `EntityInstance.cs:300-325`).
  Subscription order decides whether loot drops before or after `Deactivate`.
  `Deactivate` does not touch `Equipment` or `CargoBays`, so loot should survive,
  but this must be confirmed in play rather than reasoned about -- if loot stops
  dropping, that is this cut's fault and the deactivate moves after it.

### 8.2 A deactivated entity does not update

`Zone.Update` (`Zone.cs:172`) iterates a snapshot:

    foreach (var entity in Entities.ToArray()) entity.Update(deltaTime);

so an entity removed part-way through a frame still receives its `Update` in that
same frame, after the rest of the world has dropped its references. `Entity.Update`
(`Entity.cs:1031`) never consults `_active`. Every behaviour then runs against
torn-down state, and `LockWeapon.cs:98` reaches `Entity.EntityInfoGathered[Entity.Target.Value]`
through a raw indexer.

- `Entity.Update` returns immediately when `!_active`.

This is the fix, and the raw indexer is deliberately **not** the fix. Guarding
`LockWeapon` with `TryGetValue` would silence this one call site and leave every
other behaviour running on a corpse -- the compensator the doctrine warns about.
Liveness is the entity's own property and the entity is the one place to enforce
it. `IsActive` already exists at `Entity.cs:116`; nothing was asking it.

### 8.3 The other symptoms are not explained by this, and are not closed

The heat spike, the motionless ships and the shots that registered no hits are
**unexplained**. One coherent story fits all three -- an item's heat driving the
targeting system offline makes `FireControl.Accuracy` fall back to
`UnaidedAccuracy` (`.05`), which reads exactly like "I can fire but nothing
lands," and an overheated thruster reads as "I cannot move" -- but that is a
hypothesis, not a finding, and nothing here has reproduced it.

What can be said from the source: `ActionGameManager.cs:1281` passes
`Time.deltaTime` into `Zone.Update` unclamped, and `Zone.cs:160` divides by it
(`orbit.Value.Velocity = (Position - PreviousPosition) / deltaTime`). Unity caps
`Time.deltaTime` at `Time.maximumDeltaTime`, so a large spike is bounded, but a
**zero or near-zero** first frame is not bounded from below and that division is
unguarded. Worth a probe before it is worth a fix.

This cut does not chase it. It records the symptom, fixes the defect that is
proven, and leaves the rest open with the evidence that exists -- an unreproducible
heat spike is not something to patch speculatively.

### 8.4 Re-anchor the four harnesses the diagnostics refactor stranded

`72c0109c` routed `HitProbability` through a new `FireControl.Inspect`, replacing
five early returns with flags and one gate. That is the campaign's own doctrine
applied correctly -- the operator's comment says it outright, that keeping the
calculation here stops the debug HUD growing a second, subtly different fire
control model -- and it rewrote the exact lines four harnesses anchor into.

Seven anchors now fail: `cut3` (2), `cut5` (1, its **no-op control**), `cut6c`
(2, including its control), `cut6d` (2). Every one reports `tree-clean: PASS`, so
nothing leaked, but a harness whose control cannot run is defending nothing.

- Re-anchor all seven onto the current spelling in `Inspect`. The rules they
  attack are unchanged; only the text moved.
- A control that cannot find its anchor must stay a hard failure. Do not make
  anchors fuzzy to survive refactors -- loudly stale is the property that makes
  these harnesses trustworthy, and it is what surfaced this within one run.

### Verification

- `DeadEntityDoesNotUpdate`: an entity killed mid-frame receives no further
  `Update`, and a `LockWeapon` on a ship whose target dies the same frame does not
  throw. Mutation: drop the `_active` guard. Must die.
- `DeathDeactivates`: a dead entity reports `IsActive == false` and holds no live
  subscriptions. Mutation: remove the `Deactivate()` call. Must die.
- `LootStillDropsOnDeath` if it can be reached headlessly; otherwise an explicit
  operator check, named as such rather than assumed.
- All seven harnesses clean with tree-clean PASS, controls included.
- 222 tests plus the new ones; catalog reads; Unity batchmode exit 0.

---

## Cut 9: the die is not a die

Date: 2026-09-22. Branch `codex/fire-control-9` off `dcb8c448`.

Soul pass over `ca57819d..dcb8c448` (2026-09-22). Ten findings; this cut takes the
three that block play. The rest are Cut 10.

### 9.1 The hit roll has a range of 0.125 (Soul C1). **Blocking.**

`FireControl.cs:356`:

    var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);
    ...
    var hit = p > 0f && random.NextFloat() < p;

`CultMath.Random` is xorshift32 and `NextFloat()` returns the top 24 bits of the
**first** round (`Random.cs:22-42`). One round does not carry a low-bit seed
difference into the high bits. `ShotId` varies only in the low bits, so the top
three bits of the first draw are fixed by the zone seed alone.

Measured, 2000 shot ids per zone: **every zone's roll spans 0.1249**. Reproduced
independently with a different hash function — the span is structural, not a
property of `StableHash`. The consequence is that a shot is a step function of
`p` whose threshold is fixed when the zone is created: below the band it never
hits, above it always does. An unaided shooter hit 0 of 18,926 shots against the
shipped catalog. Roughly one zone in eight cannot land a fully aided shot at
p = .74.

This is the operator's "I can target and fire with no hits registering at all,"
and it is stable per zone, which is why it came and went.

It is also this map's own prescription (Cut 6b, §6.1), written by Self, shipped
through three cuts, and never once measured.

**The rule:** the roll must be uniform on [0,1). Not merely deterministic —
determinism is what a constant already gives, and is exactly why three
determinism tests passed over it.

- Mix the seed before the first draw, with a standard 32-bit finalizer
  (`fmix32`/`splitmix32`: xor-shift, multiply, xor-shift, multiply, xor-shift).
  The per-shot seed derivation is unchanged; only its diffusion into the first
  output changes. Determinism, the `(zone, shot id)` purity and the freeze
  discipline all survive untouched.
- **This is a caller's obligation, not a CultMath defect.** Seeding xorshift with
  a structured value and drawing once is the documented way to get a correlated
  first output. Record a follow-up to give `CultMath.Random` a mixed-seed entry
  point so no other GameCult consumer re-derives this the hard way — that is a
  CultLib cut with its own release, not this one.

**Verification, and the test whose absence caused this:**

- `TheDieIsUniform`: fire many shots across several zone seeds at an
  intermediate `p` (say .3 and .6) and assert the observed hit fraction is near
  `p`, and that the observed rolls span substantially more than 0.2. Mutation:
  remove the finalizer. Must die.
- No test anywhere in this campaign measures a distribution. Every behavioural
  fixture uses `accuracy: 1` or `accuracy: 0`, which is why a die stuck on one
  face satisfied all of them. That gap is the finding, not the line of code.

### 9.2 `pOnHull` collapses to zero above Precision ~4 (Soul C2). **Blocking.**

`FireControl.cs:497-516`. `Sigma = 1/Precision`, and once sigma falls below about
half a cell every `exp(-d²/2σ²)` underflows against an aim point that is not
exactly on a cell centre — which is **every shipped hull**, since no centre of
mass is integral (LonginusX 2.5/6.697, Zenith 5.5/5.5, Turret 3.5/3.5). Measured
`pOnHull` on LonginusX: `.516` at Precision .3, `1.000` at 1.19, `.112` at 5,
`.000` at 10 and beyond.

So a better targeting system makes you miss, and past Precision ≈ 5 the weapon
cannot hit anything. Authored content sits at .5-1.3, just under the cliff, with
a margin of about 4x — and the operator is about to tune this number.

- Clamp sigma at a floor of about half a cell, or normalize by the discrete sum
  rather than the continuous 2πσ². Either makes `pOnHull` monotonic in Precision
  over the whole domain instead of only below the cliff.
- `WeightedPick` (`:535-545`) carries a comment claiming it "can never fall
  through." It can, whenever `totalWeight == 0`, returning a fixed corner cell.
  It is unreachable today only because `pOnHull == 0` forces `PBase == 0` first.
  Fix the cause and delete the false comment.
- Cut 6d's map section claims to name its consequences; it does not name this.
  Add it.

### 9.3 The LockWeapon crash is still reachable (Soul C3). **High.**

Cut 8 fixed a real liveness defect and did **not** fix the operator's crash.
`LockWeapon.cs:98` only runs on an *active* entity, so 8.2's `!_active` guard
cannot be what prevents it — `tests/mutation_tests_fire_control_cut8.sh:17-19`
says as much outright, and I merged it anyway.

The live precondition is `Target.Value` set but absent from `EntityInfoGathered`
on an active entity, and docking still reaches it: `Deactivate` (`Entity.cs:445`)
disposes the `Entities.ObserveRemove` subscription that is the only thing nulling
a stale `Target`, and nulls nothing itself; `Activate` (`Entity.cs:167`) reseeds
`EntityInfoGathered` and likewise leaves `Target` alone. Dock, let the target die
or leave, undock, and the first frame throws.

- `Deactivate` nulls `Target` (and `TargetItem`), or `Activate` reconciles it
  against the reseeded `EntityInfoGathered`. One of the two owns it; say which.
- Still not a `TryGetValue` in `LockWeapon`. The invariant is that an active
  entity's `Target` is always a live entity it has info on.
- Pin it with the dock/die/undock sequence, not with a unit test on the guard.

### Verification

- The three tests above, each dying under its own mutation, in a committed
  `tests/mutation_tests_fire_control_cut9.sh` with a no-op control, byte-exact
  I/O, sha256-verified restore and an explicit tree-clean verdict.
- All nine harnesses sequentially, one at a time, all clean.
- 226 tests plus the new ones; catalog reads; `dangling` 0.
- After 9.1 lands, re-measure the fight: Soul's probe, run with a corrected die,
  gave 60.8% hit rate and a 4.3 s median time to kill for an aided shooter, 2.6%
  unaided. Confirm the shipped build now reproduces that, because those are the
  numbers the operator will tune against.

### Recorded for Cut 10, not fixed here

Soul C4 (a committed hit on a target that dies mid-flight publishes as a hit),
C5 (`Inspect` is numerically exact but costs 9.87 µs and 448 B per call on paths
that used to bail — the operator's own comment on the hot path looking like a
diagnostic one has an empirical answer now), C6 (`RollsAreSeeded` is obsolete —
Cut 7 retired one such test and missed this one), C7 (`FireControlCut6cTests`
authors `Precision = 0`, making six of seven checks vacuous), C8 (`Precision =
1000` fixtures make several kernel-shape mutants unkillable, and `pOnHull`'s
deletion is caught by exactly one test), C9 (`EveryHitLandsOnMetal` runs one
configuration, not the range it claims), plus the harness-audit items: the
tree-clean verdict restores before it measures, a red result cannot distinguish a
killed mutant from a compile failure, and the no-op control covers only one file
per harness.

---

## Cut 11: mutation testing moves to Stryker, and the survivors it found

Date: 2026-09-22. Operator, on the hand-written anchor harnesses: "What is the
point of those mutation anchors? Tests should cover how the code behaves, not how
it is shaped." And on Stryker: "sounds good; we don't need to run with every
mutation active, right? > to >= on float thresholds is indeed petty."

### Why

The Eureka principle (2026-09-15) is sound and stays: every rule needs a test that
fails when the rule breaks. The mechanism that accreted on it over the following
week -- literal-text anchors, perl substitution, per-cut bash and Python harnesses
-- coupled mutation testing to code shape. It stranded anchors on every refactor
(`72c0109c`, Cut 9, Cut 10), and at Cut 10 an anchor kept matching exactly once
while silently re-targeting onto `Inspect`, mutating code the tests no longer ran.
Soul's harness audit also found the tree-clean verdict restoring before it
measured, and a compile failure counting as a kill.

A Stryker spike against `FireControl.cs` alone: 276 mutants, 7m18s, 208 killed,
67 survived. It killed every mutation the hand-written harnesses defended, never
edits a source file, and has nothing to re-anchor.

### 11.1 Retire the ServerShared harnesses

Delete the 23 hand-rolled harnesses (5,816 lines) whose targets are all under
`Assets/Scripts/ServerShared` -- every `mutation_tests_fire_control_*`, every
`mutation_tests_stats_power_*`, `mutation_tests_shield_reserve.py`,
`mutation_tests_condition_ratio_cut8.py` and `mutation_tests.py`. Stryker's scope
covers all of their targets; the behavioural tests they measured stay.

**Kept, recorded, not yet answered:** `mutation_tests_addressables_cut.py` and the
three `mutation_tests_shield_panel_cut*.py`. They target Unity Editor probes
(`EngineAssetCheck.cs`, `ShieldPanelCut*Verify.cs`) that Unity compiles outside
`Aetheria.Shared`, so Stryker cannot reach them. They carry the same anchor
fragility; the right answer for GPU and editor code is probably that the batchmode
probes are themselves the behavioural tests, but that is a separate decision.

### 11.2 Scope, pinned

`tests/Aetheria.Shared.Tests/stryker-config.json` scopes mutation to
`Assets/Scripts/ServerShared/**` and excludes plugins. Per cut, run against the
cut's own diff:

    dotnet tool restore
    cd tests/Aetheria.Shared.Tests
    CULTLIB_ROOT=<CultCache worktree> CULTMATH_ROOT=<CultMath worktree> dotnet stryker --since:<base commit>

Each root must sit at its pinned revision in `Directory.Build.props` (see the Cut 12.0
status note).

**Float-threshold boundary flips are known-equivalent.** Stryker files `>`→`>=`
under the same Equality mutator as `==`→`!=`, and the latter catches real bugs, so
the mutator stays on. A survivor that only moves a float comparison across its
boundary is triaged as equivalent and never chased. For the same reason the raw
mutation score is not a gate; the survivor list is the signal.

### 11.3 Degenerate fixtures

Most of the 67 survivors trace to three constants shared across the fire-control
fixtures, each of which makes a family of mutants arithmetically identical to the
original -- the same defect class as the zero-velocity target and the durability
coincidence, found here as a pattern instead of one at a time:

- **Shooter at `float3.zero`.** `target - source` equals `target + source`, so four
  separate position mutations survive (`FireControl.cs` HitProbability, Inspect,
  Fire, Apply).
- **Fire time zero.** `now - FireTime` equals `now + FireTime`, so the elapsed-time
  mutation survives -- which means `EvasionCountsUntilCommitAndNotAfter` still does
  not defend the rule Cut 5 gave it a moving target to defend.
- **Spread zero everywhere.** `PSpread` returns 1 on every fixture, so it is
  effectively untested, and `*` versus `/` against it is indistinguishable.

Move shooters off the origin, fire at nonzero times, and give some fixtures real
spread. Fix the fixture, never weaken the assertion.

### 11.4 Behavioural tests the survivors point at

- **Splash handedness.** Flipping the sign in `var right = float2(forward.y,
  -forward.x)` passes every test. The operator's "damage on the wrong side of the
  ship" was a legacy Unity bug; this line has read the same since `482c47bf`, so
  the rewrite never shipped it, but nothing defended against it either. Self said on 2026-09-19 that Cut 6b would add a test pinning
  that a port blast damages port cells; it never landed and nothing checked. Pin it.
- **The shield loses energy when it absorbs.** Deleting `Shield.TakeHit` from the
  discrete path survives, because tests assert the hull was untouched and never
  that the shield paid for it.
- **Splash's shield-absorb branch** has no coverage at all.
- **Zones roll differently.** `CombatSeed *` → `/` makes the seed zone-independent
  and survives; `TheDieIsUniform` checks uniformity within a zone only. Tighten it
  enough that the `MixSeed` weakenings that survive it (`>>`→`<<`, `^=`→`|=`) die.
- **A shot whose target left the zone resolves as a miss** (Soul C4). Today only
  the uncommitted branch rewrites the outcome; a committed hit on a target that
  died mid-flight is published as a hit. Fix the code and pin it.

### Verification

Stryker over `FireControl.cs` before and after: every non-equivalent survivor
above killed, the remainder triaged by name. 231 tests plus the new ones; Unity
batchmode clean.

### Status (2026-09-22)

Landed on `codex/fire-control-10`: `8ef3b34c` (Stryker tool), `b305debd` (map,
config), `724ec5e7` (11.1), `a6543af7` (11.4 and the C4 fix), `1b187db4` (11.3),
`b0fd3cd0` (second round, below). 244 tests.

Stryker over `FireControl.cs`, 278 mutants per run:

| Run | Killed | Survived | Score |
|---|---|---|---|
| Spike, before Cut 11 | 208 | 67 | 74.6% |
| After 11.4 | 219 | 58 | 78.6% |
| After 11.3 fixtures | 225 | 51 | 81.1% |
| After the second round | 225 | 43 | 83.3% |

**The second round** came from reading the 51. Four were real undefended
behaviour and one was a duplicated formula:

- `Inspect` recomputed `PBase` as its own product of the factors, so the gates and
  the product had two owners, and a `*`→`/` mutant of the HUD's copy survived the
  agreement test (every fixture had spread 0, so `PSpread` was 1). `PBase` is now
  `HitProbability`'s own answer; the test became
  `TheHudShowsTheFactorsTheSimulationMultiplies`, over a spread cone that does not
  fill the silhouette. `Fire`'s redundant `target != null ?` guard around
  `HitProbability` went with it.
- A beam shot's `damageOverride` could be dropped: `BeamRollsPerInterval` computed
  the expected damage from its own roll count, a tautology, now deleted.
  `ADamageOverrideIsWhatTheShotCarries`.
- `Tracking` could ignore the fitted targeting system: no fixture's target ever
  moved off its predicted line. `AFittedTargetingSystemForgivesAJink`.
- A shot orphaned before commit could skip `ShotCommitted`, or publish its miss as
  shielded. `ShotAtTargetThatLeavesBeforeCommitCommitsAsAMiss`.
- `ArrivalIn` (the HUD countdown) was unchecked; asserted in the C4 test.

**The 43 that remain, triaged:**

- *Boundary flips, equivalent (23):* L52, 54, 56, 75, 135 (two), 157 (two), 193
  (two), 223, 273, 410, 418 (two), 465 (two), 503, 518, 524, 586, 605, 608.
- *`MixSeed` shift and xor weakenings, measured equivalent (4):* L382, 384, 386
  (two). 11.4 asked for them to die; measured, the die stays uniform and
  zone-distinct under each, so there is no behaviour to pin. They change how the
  mixer is built, not whether the die is fair.
- *Unreachable through the live callers (8):* L240 and L241
  (`DeviationProbability`'s null-target branch; the engine guards it, only the HUD
  can pass null), L283 (three; airburst has no authored radius anywhere, recorded
  since Cut 6b), L320 (empty-list early return, an optimisation), L410 `||` (a
  nonzero `PBase` implies a target), L158 and L610 (no coverage: an unlocked lock
  weapon reaching this line, and the last-cell fallback of the weighted pick).
- *Recorded gaps, not yet reached (8):* L135 ternary (two; no test that the AI in
  `Combat.cs` leads a moving target), L435 (three; the aimed flag on an outcome),
  L464 and L465 negate (the `Apply` hit direction through the `FireControl`
  path), L585 and L587 (the aimed item's centroid as aim point). These are "not
  yet reached," not "unreachable."

## Cut 12: shots arrive edge-on

Date: 2026-09-22. Imagination pass, revised the same day after the second round of rulings
below. Anchors are against `origin/codex/item-provenance` `cd846916` (Cuts 10 and 11 merged).
Claims marked **(probe)** were measured in a scratch copy of the test project
(`scratchpad/cut12probe`, the Cut 11 fixture plus probe methods) or by a lane-model script
(`scratchpad/exposure.py`), in both cases against a copy of `GameData/Aetheria.cc`. Claims
marked **(read)** come from source at the cited anchor.

### The defect

The impact cell is chosen as if every shot drops in from overhead.
`FireControl.HullKernel` (`FireControl.cs:558-578`) throws a 2D Gaussian at the top-down
schematic, centred on the aim point. `POnHull` (`:233-234`), `WeightedPick` (`:601-611`) and
`ResolveAimPoint` (`:583-589`) all read that kernel, and none of them takes the shooter's
bearing. **(probe)** `Probe3_BearingBlind`: pOnHull is `0.8748` at target facings (0,1),
(1,0) and (.6,.8) alike.

Bearing is only used after the cell has been chosen: `Entity.ApplyHit` (`Entity.cs:381-409`)
walks penetration from that cell, and `Splash` (`FireControl.cs:515-527`) picks the half of
the hull that faces the blast. So armour facing does nothing. A wedge whose bow carries all
the plate gets hit in its soft stern from dead ahead. The game is planar (R7), so a shot
travels in the plane and should arrive edge-on.

The damage rule makes it worse. `Entity.DamageSchematic` (`Entity.cs:336-374`) splits the
damage evenly over every cell of the hit shape. **(probe)** `Probe1`: 30 damage with
penetration 3 into column 1 of the 5x3 fixture, with 10 armour on the front cell only, puts
10 on each of the three path cells. The plate absorbs its 10, the item behind it still takes
10, and the stern cell passes 10 to the hull. The item receives exactly what it would have
with no plate in front of it.

### Rulings (operator, 2026-09-22)

First round:

- **Model adopted: throw the dart at the silhouette the shooter sees.** Scatter is 1D and
  perpendicular to the bearing. Project the hull's occupied cells onto the lateral axis and
  throw a 1D Gaussian at that projection, with the same sigma rule, centred on the aim
  point's projection. pOnHull is the Gaussian's mass on that shadow. Depth comes from
  geometry: walk the ray in from outside along the bearing, and the first occupied cell is
  the impact cell. Penetration continues along the same ray.
- Operator: "No, you're right, this is fine. Armor absorbs first. Shields remain
  omnidirectional. Map it."
  - **Armor absorbs first.** Damage runs down the ray in order. At each cell the armour
    absorbs first, then the item, and only the remainder travels on. The even split over
    the hit shape goes, for the penetration path and the DamageSpread expansion alike.
  - **Shields remain omnidirectional.** The shield stays one whole-ship reserve with no
    facings.
- **Rejected:** any fraction of shots arriving top-down to preserve cockpit sniping. The
  operator accepted that cockpit shots become positional: the cockpit must be exposed from
  your bearing, or you must flank, or penetration must reach it with what the armour did not
  absorb. Cockpit destruction is still instant death (`Entity.cs:494`).

Second round:

- **Q12-1 = B: the bearing that places a hit is taken at Commit.** Operator: "Of course we
  must use the target's facing at arrival, tanking a hit where you have armor is basic
  maneuvering." After Self explained the commit horizon: "at commit." The target's facing
  therefore stays live until Commit, as deviation already does. See **Bearing timing**.
- **Q12-2 = A: spread is width.** It becomes 2n+1 parallel lanes, and each lane is absorbed
  from its own facing cell. Operator: "Spread is width makes sense for shotguns, but what
  about explosive penetrators? Burrowing in before splashing radially would be a satisfying
  way to damage vulnerable internals." That remark became sub-cut 12.4.
  - **Amended 2026-09-25, lane spacing.** Soul found that lanes one cell apart share cells at
    angled bearings (a cell's shadow is up to √2 wide), so whichever lane went first ate the
    shared armour and mirror-image shots did different damage in ~30% of angled spread hits.
    Operator: "I would prefer if the overlapping damage cells were diffused sideways to
    thicken the line rather than doubling up." Self's reading, confirmed ("nope, you got it"):
    **lanes are spaced by one cell's shadow width at the committed bearing** (the `Extent`
    width `2h`: 1 when axis-aligned, up to √2 at 45°). Each cell's admitted interval is exactly
    that wide and half-open, so no two lanes can strike the same cell; the footprint widens at
    angles instead of doubling up, and axis-aligned shots are unchanged.
- **Q12-3 = A (direct hits):** a lane's remainder goes into the hull wherever the lane
  ends.
- **Q12-4 = A:** delete `ShotOutcome.Aimed`.
- **Penetration retune is a follow-up.** Operator: "Retuning penetration is a follow up."
  See F12-2. Until it lands, interior cells are reached only by blasts.
- **The detonation primitive (12.4): a blast is an area, not a bundle of rays.** The first
  draft of this section cast rays; the operator cut that. Operator, 2026-09-22: "sorry, why
  are we doing blast rays instead of areas? I'd say distribute the damage over the affected
  area, but that only works for areas". So a blast at a schematic point P with radius r
  covers the cells within r of P, and **damage is distributed over the disc's area**: a cell
  takes the share of the disc that lies inside it. The share of the disc that falls outside
  the hull's occupied cells is lost. Each covered cell absorbs independently, in the
  existing order (armour, then item, then hull). Nothing travels, nothing is occluded, and
  no remainder is routed anywhere.

  **A shot has a direction; a blast does not.** That sentence is the rule behind the split:
  direct hits keep the lane model of 12.2 and 12.3, and blasts are areas. Where P sits:
  - proximity fuse: the intercept point outside the hull (today's airburst);
  - contact fuse: the impact cell;
  - delayed fuse or penetrator: the end of the penetration walk.

  All three are weapons that carry a blast radius. A weapon without one is not a blast at
  any penetration: it keeps 12.3's lane (see **Where 12.3 and 12.4 meet**).

  12.4 replaces `FireControl.Splash` and its half-hull footprint, which absorbs F12-1.
- **WeaponModifiers are labels, not behaviour.** Operator: "The AP modifier was merely
  there as a label. Like the Incendiary modifier which doesn't actually determine that the
  weapon does Thermal damage, but exists to mark weapons that do. Modifiers show up in
  schematics as a shorthand for the weapon type." Detonation behaviour comes from weapon
  data fields only.
- **Not ruled:** a HUD showing which target items are exposed from the current bearing
  (Q12-5).

### What the probes found

- **Penetration does nothing in the shipped catalog.** **(probe)** The largest authored
  Penetration is `0.25` (Autocannon, SRMM72, LRMM72). `ApplyHit` only walks when
  `penetration > .5f` (`Entity.cs:392`), and `WeaponData.Penetration` is inspector-ranged
  `0..1` (`Behaviors/Weapon.cs:28`).
- **LonginusX's cockpit cannot be reached edge-on until penetration reaches about two
  cells.** **(probe)** `exposure.py` walked lanes 0.01 cells apart at every whole degree of
  bearing over the real 6x17 schematic. The ControlModule (cockpit) and the AetherDrive are
  never the first cell in any lane. They are still never reached at penetration 0.25 or
  1.0, and are first reached at 2.0. The Turret hull's ControlModule is enclosed too.
  Until F12-2 lands, the only way to kill a LonginusX through its cockpit is a blast
  delivered inside the hull (12.4).
- **DamageSpread is live content.** **(probe)** GT 3K authors 1-3.25, plight 1-3.3, and
  pswarm and scorched void policy 0.9-1.5. `Shape.Expand` is an 8-neighbour ring
  (`ItemData.cs:164-174`), so spread 3 produces a 7x7 footprint today.
- **No shipped weapon carries a blast, or the Airburst label.** **(probe)** `Probe4`:
  every `WeaponItemData.AirburstRange` is null, and no `WeaponModifiers` includes Airburst.
  The labels in use are RapidFire, Cluster and Incendiary. All three Thermal weapons carry
  Incendiary, and no non-Thermal weapon does. No weapon carries ArmorPenetrating,
  including the three with penetration 0.25. So the only live consequence of demoting the
  Airburst label is the code path at `FireControl.cs:282`.
- **The handedness history.** `right = float2(forward.y, -forward.x)` has not changed since
  `b7743789`/`482c47bf` (`git log -S`). No sign error ever shipped. What survived was the
  *mutant* `-forward.x` → `forward.x`, which is invisible when |fy| > |fx|, and Cut 11 pinned
  it. The formula is duplicated at `Entity.cs:394-396` and `FireControl.cs:516-520`, and 12.1
  collapses the two copies.
- **The code has two cell conventions.** The kernel, `Shape.CenterOfMass`
  (`ItemData.cs:121-123`) and `ResolveAimPoint` put cell *i* at the integer point *i*.
  `ApplyHit` instead starts from `cell + .5` and truncates (`Entity.cs:398-404`), which
  treats cell *i* as the square [i, i+1). **(probe)** On bearing (.8,-.6) from (1,2), the
  0.5-step march went (1,2)→(2,1) and skipped (2,2), a cell the ray actually crosses.
- **Nothing reads `ShotOutcome.Aimed`.** `rg "\.Aimed\b"` finds only `FireControl.cs:292`
  (PendingShot, which stays), `:432`, `:435` and `:481`, plus `FireControlCut7Tests.cs:190`
  (which also reads PendingShot).
- **`Apply` reads live positions at arrival** (`FireControl.cs:463-465`), which is an R4
  leak and Stryker's L464/L465. 12.2 removes it.
- **No simulation code places the schematic in the world.** **(read)** `SchematicCellSize`
  (`Settings.cs:255`, default 2) is read only by `PSpread`, and ApplyHit and Splash convert
  directions only. A blast needs world↔schematic *points*, so 12.4 names that rule.
  `HullCollider` is a physics surface only (`Gameplay/HullCollider.cs:4-8`).
- **Renaming slot 29 is compatible drift, not a break.** **(probe + read)** The catalog
  embeds each type's schema descriptor, including `{"slot":29,"name":"AirburstRange",
  "type":"System.Nullable<System.Single>"}`, and the wire format is positional
  (`Probe5_Wire`: `[…,12.5,50,20]`, with no field names on the wire). On a content-hash
  mismatch, `CultCache.ResolveSchema` (`CultCache.cs:590-670`) runs `CompareSchemaShapes`
  (`:672-744`), which compares slot type, reference, cardinality, target schema, name
  semantics and index alias, **not the member name**. So renaming slot 29 opens as
  `CompatibleDrift` with warnings. A new nullable slot opens with a `defaulted_missing_slot`
  warning. The warnings persist until the catalog is next written with the new descriptor.
  Readers of the field: `FireControl.cs:284` and `FireControlCut6Tests.cs:81-101, 249, 268,
  279`. `tools/AetherDb` has none. The `AirburstRange:` lines in nine weapon prefabs
  (for example `Flak Cannon.prefab:65`) are orphaned YAML from a deleted MonoBehaviour field:
  no C# field of that name exists outside `ItemData.cs:498`.

### Frame, geometry and the bearing (the rules every sub-cut reads)

- **Schematic frame, one owner.** `Entity.ToSchematic(float2 worldPlanar) → float2` maps
  world to schematic with x = starboard and y = bow: `forward = normalize(Direction)`,
  `right = float2(forward.y, -forward.x)`, returning `float2(dot(v, right), dot(v, forward))`.
  This is the transform ApplyHit and Splash already use, with the same sign. The Cut 11
  fixture comment and the LonginusX schematic (nose at y 11-16, reactor and radiators at
  y 0-3) agree with it. 12.4 adds the point forms `ToSchematicPoint(float2 worldPlanar) =
  ToSchematic(worldPlanar - Position.xz) / SchematicCellSize + Shape.CenterOfMass` and its
  inverse `ToWorldPoint`, so the entity's position is the schematic's centre of mass.
  Operator confirmed 2026-09-22, from the schematic images: "bow is indeed +y on the
  schematic images." Still on the operator's check list after 12.4 lands, because it is the
  half that only a running blast can show: that blasts land where the model shows them,
  which is what confirms the centre-of-mass anchor.
- **A cell is the unit square centred on its integer coordinate.** This matches
  `CenterOfMass`, the aim point and the old kernel. The `+ .5` convention dies with ApplyHit.
- **Travel direction.** `FireControl.TravelDirection(Weapon, Entity source, Entity target) →
  float2` is the planar world direction of `PredictedIntercept(weapon, source, target) -
  source.Position`. That intercept is Cut 3's one function, and for a beam it reduces to the
  target position. Below a length of 1e-6 it falls back to world +z, which is today's
  `Apply` fallback (`:463`). It belongs to the shot: Fire freezes it into `PendingShot`.
- **Bearing.** `bearing = normalize(target.ToSchematic(travelDirection))`. This is the only
  place a world direction meets a target's facing.
- **Lateral axis** `ℓ = float2(-b.y, b.x)`. The sign is fixed once, here.
- **Shadow.** Each occupied cell *c* projects to `[dot(c,ℓ) - h, dot(c,ℓ) + h)`, with
  `h = (|ℓ.x| + |ℓ.y|) / 2`. The shadow is the sorted and merged union of those intervals;
  merging matters because off-axis intervals overlap. Squares are used rather than points
  because a line at lateral offset *s* crosses square *c* exactly when *s* falls in *c*'s
  interval. "s lies in the shadow" is therefore the same event as "this lane has metal", so
  pOnHull is exactly that event's probability, and placement drawn from the shadow always
  lands on metal.
- **`Silhouette(Entity target, HullData hull, EquippedItem aimed, float2 bearing, float
  precision)`** replaces `HullKernel` and is the one function. It returns the merged
  intervals, `a = dot(aimPoint, ℓ)`, sigma, `Span = hi_max - lo_min`, and
  `POnHull = Σ_k Φ((hi_k - a)/σ) - Φ((lo_k - a)/σ)`, where `Φ(z) = ½(1 + erf(z/√2))`.
  `aimPoint` comes from `ResolveAimPoint` unchanged; only its lateral projection matters.
  `Sigma` keeps its rule (`FireControl.cs:549-550`). The 9.2 rationale in its comment is
  gone, because an exact 1D integral does not undershoot, so rewrite it: the floor is now a
  design minimum on group tightness (F12-3). The function allocates only on the ungated
  path: one interval array of length N (≤128 on shipped hulls) plus a sort.
- **PSpread** keeps its formula, but takes values instead of a `Weapon`:
  `PSpread(float spread, float span, float range, float cellSize)`. The half-extent becomes
  `.5f * span * cellSize` instead of `max(Width, Height)` (`:224`). PSpread and pOnHull read
  one silhouette, so they cannot disagree about the target's size.
- **Lateral draw.** One `NextFloat` u, taken after the hit roll (the draw `WeightedPick` used
  to take). Walk the cumulative interval mass to `u · POnHull` to select interval k, then
  invert inside it with `s = a + σ√2 · erfinv(2p - 1)`. Clamp into [lo_k, hi_k) to absorb
  rounding. The clamp is a guard, not an invariant.
- **Lane walk** `Lane(HullData hull, float2 b, float s, buffer)` collects the occupied cells
  whose interval contains s, each with slab entry and exit parameters `t` along b, where
  `t = dot(point, b)`. Cells are ordered by entry, with ties broken by `dot(c, b)` and then
  cell index. It is exact slab traversal and replaces the 0.5-step sampling. The result goes
  into a caller-supplied pooled buffer (`ArrayPool<LaneCell>.Shared`; netstandard2.1 has
  `System.Buffers`). A direct hit starts from t → -∞ (outside), so its first element is the
  impact cell. It is the walker's only caller: blasts are areas and do not traverse.

  Metal is continuous while `next.entry ≤ current.exit + 1e-4`, and the first gap ends the
  walk. That keeps today's rule at `Entity.cs:401`.
- **Penetration along a direct-hit lane.** A cell is reached when
  `entry - impact.entry < penetration`, in cells. The impact cell is always reached. The
  `> .5f` threshold (`Entity.cs:392`) is deleted.

**Bearing timing (Q12-1 = B).** A shot is split into what the shooter decided and what the
target is doing:

- **Frozen at Fire:** everything the shooter decided (R10). `PendingShot.PBase` is renamed
  `PFire` because its meaning changes: it is `Accuracy × PSensor` behind the same gates, and
  it is 0 when any gate fails. Also frozen: `TravelDirection`, `Precision`, `Aimed`, the
  weapon's `Spread`, and `FireRange` (the range at Fire, which PSpread reads).
- **Live until Commit:** everything the target is doing. That was already its position
  (deviation); now it is also its facing. `Commit` computes, in this order:
  1. `pDeviation`, as today.
  2. `bearing = normalize(shot.Target.ToSchematic(shot.TravelDirection))`, from the
     target's facing at the commit tick.
  3. `sil = Silhouette(target, hull, shot.Aimed, bearing, shot.Precision)`.
  4. `p = PFire × pDeviation × PSpread(shot.Spread, sil.Span, shot.FireRange, cellSize) ×
     sil.POnHull`.
  5. The one roll against p. On a hit, the lateral draw from `sil`, and the impact cell
     from `Lane`.

  Steps 2-5 are one function, `FireControl.CommitProbability(PendingShot, float now,
  out Silhouette)`. The debug HUD calls it, which retires the inline estimate at
  `ActionGameManager.cs:1317-1318` (`shot.PBase * pDeviation`, which would otherwise be a
  second, wrong copy of the model).
- **The outcome freezes the committed geometry** (R4). `ShotOutcome` gains `float2 Bearing`
  (schematic frame, as committed) and `float Lateral`, and keeps `Cell`. `Apply` walks
  `Lane(hull, outcome.Bearing, outcome.Lateral)`. After Commit, turning the target changes
  nothing, and Apply reads no position or facing.
- **HitProbability and Inspect** report `PFire × PSpread × pOnHull` at the *current*
  travel direction and facing. That is a forecast, in the same way the HUD already treats
  deviation. `Inspect.PBase == HitProbability` still holds, because both are the forecast.
  The hot path still gates first (`:150-159`) and builds the silhouette only after the gates.

What this does to the earlier rules:

- **R3 (one roll, and a hit always lands on metal) still holds.** The factor the roll is
  charged with, pOnHull at the commit bearing, comes from the same `sil` that placement
  draws from, in the same call. Placement is exactly consistent with the pOnHull applied,
  and it always lands on metal. What changes is that HitProbability's number is no longer
  the exact roll price when the target turns during flight. It is the price only if the
  target holds its facing, just as it already is only if the target holds its course.
- **Cut 5's deviation factor keeps its ordering and meaning.** It is still measured at the
  commit tick against the fire-time projection, and it is still multiplied into p before
  the single roll. The two silhouette factors join it in the same product. The Cut 7 guard
  (`p > 0f && shot.Target != null`, `:410`) extends to cover them: when `PFire × pDeviation`
  is 0, Commit builds no silhouette. Multiplication order has no numerical effect. The guard
  exists only to skip work.
- **6d's premise ("pOnHull depends only on the aim point, the hull shape and the frozen
  Precision, so it folds into PBase at fire time") is retired.** pOnHull now depends on the
  target's facing, which is live target state like position, so it is priced at Commit.
  R10 is untouched: every *shooter* stat is still frozen at Fire, and the target's facing
  was never a shooter stat.
- **Cut 10 still holds:** gated HitProbability allocates nothing. Commit is not on the
  hot path.

### Sub-cut order

| Sub-cut | Repo | Nature | Depends on |
|---|---|---|---|
| 12.0 | CultLib | CultMath gains `erf` and `erfinv` (a gap filled in its owner) | none |
| 12.1 | Aetheria | subtraction and frame owner; no behaviour change | none |
| 12.2 | Aetheria | where a hit lands: silhouette at Commit, Apply stops reading positions | 12.0, 12.1 |
| 12.3 | Aetheria | how a direct hit travels: exact lane walk, sequential absorption, spread lanes | 12.2 |
| 12.4 | Aetheria | the detonation primitive (areas), fuse data, the Airburst label demoted; Splash deleted | 12.3 |

The five are kept apart so that Soul falsifies each question on its own: where a shot
lands, what a direct hit does, and what a blast does. In 12.2, `ApplyHit` survives for one
more cut with a changed signature: it takes the schematic bearing and loses its own
rotation, while its march and its even split are left untouched until 12.3.

---

### Cut 12.0. CultMath gains `erf` and `erfinv`

- **Repo/branch:** `F:\Projects\CultLib`, off current `main`.
- **Adds:** `math.erf(float)` and `math.erfinv(float)` in
  `packages/cultmath/src/CultMath/math.cs`, as standard single-precision approximations (for
  example Abramowitz-Stegun 7.1.26 or a minimax fit; Giles 2010 for erfinv), with tests in
  `packages/cultmath/tests/CultMath.Tests`. Neither is an HLSL intrinsic, so there is no
  shader-parity obligation.
- **Release:** run `packages/cultmath/scripts/build-unity-package.ps1`, bump
  `unity/org.gamecult.cultmath/package.json` from 0.2.3 to 0.2.4, and tag
  `cultmath-unity-v0.2.4`. Aetheria then bumps `Packages/manifest.json:56` and the headless
  `CultLibRoot` pin.
- **Verification:**
  - `ErfMatchesReferenceValues`: erf at 0, ±0.5, ±1, ±2 and ±3 matches tabulated values
    within 2e-6; erf is odd; the limits are ±1.
  - `ErfinvInvertsErf`: over y ∈ [-0.999, 0.999].
  - The CultLib mutation run, with every survivor triaged by name.

**Status: the code closes, the release does not.** Landed on CultLib
`hands/cultmath-erf`: `87b95ae`, `b688cb2` (erf, erfinv, tests), then the fix batch
`fc9ce97`, `4cdc02b`, `6e8362b`, `cc71f17`. 102 tests.

Two verification lines above were stale when written and are corrected here. CultLib had
no mutation tooling at all, so there was no "CultLib mutation run"; Stryker.NET was adopted
in its own cut (`hands/adopt-stryker`) and its CultMath baseline is 2243 mutants, 350
survived, triaged in that cut's report. The headless pin needed more than a bump. `Directory.Build.props:5` pins one CultLib revision
for every package, and that revision (`45c2f40`) is exactly Unity's `caching-unity-v1.4.0` /
`cultlib-unity-v1.0.60`; moving it to the release would have put six CultNet commits of newer
CultCache, including a breaking registration change, under the headless tests while Unity
stayed on 1.4.0. So the headless build now pins per package, mirroring the manifest:
`CultMathRoot`/`CultMathRevision` at the 0.2.4 tag's merge (`6d5e2096`), `CultLibRoot`/
`CultLibRevision` still at `45c2f40` for CultCache. A package's pin moves only when its
manifest tag moves. (Self first repeated Soul's claim that there was no pin, from a read of
lines 3-4; line 5 is the pin, and the build refused the first attempt to use the release.) The
Unity manifest lives at `packages/cultmath/unity/org.gamecult.cultmath/package.json`, not
at the repo root.

Soul's first pass found the defect this cut would otherwise have shipped: `erfinv(±1)`
returned the **wrong-signed** infinity, so a sample drawn for the far edge of an interval
was clamped onto the near edge. Reachable in ordinary play — `erf` saturates to exactly 1
at 5.543σ, so any interval edge past that yields `p = 1`. Fixed with two guards; outside
the domain is NaN, now documented and pinned. Soul also measured the reported accuracy
figures 1.5× optimistic (true worst error: erf 6.621e-7, erfinv 5.066e-7, round trip
6.109e-7, all inside the 2e-6 bar), found `erfinv` had no accuracy test at all (the round
trip was blind by 300-400×, proven with a coefficient mutant), and found
`ErfinvIsMonotonicallyIncreasing` asserting a property neither function has (9662 backward
steps in `erfinv` at float granularity; the fixture's 0.001 step hid every one). The second
pass re-derived all 19 new reference values by a different method and closed the cut.

**Cost, for 12.2 to answer rather than assume:** `erf` is 16.3 ns per call and
allocation-free, but a silhouette costs one `erf` per interval edge — ~4.2 µs per
evaluation on a 128-interval hull, 1.06 µs on 64, and worse under IL2CPP. The AI evaluates
`HitProbability` constantly. 12.2 must measure the new placement against the old kernel per
gated-in evaluation and report both, not assume 1D beats 2D.

**Released 2026-09-24:** CultLib `20e6da5` (release) and `8c86bc9` (the changelog meta), merged to
main at `6d5e2096`, tagged `cultmath-unity-v0.2.4`. Release Soul pass: the DLL rebuilt byte-
identical from source (sha256 `2470cd48…`), public surface +2 (`erf`, `erfinv`), shipped
`erfinv(±1)` correctly signed; its one finding, `CHANGELOG.md` without a Unity `.meta`, was fixed
before tagging. Aetheria repinned in `2a68b4c0` (manifest and Unity-written lock, hash
`6d5e2096`); batchmode resolve and compile clean, no missing-meta warning. Follow-up for CultLib:
`build-unity-package.ps1:89-90` checks metas only for code and binary files, so it could not see
the missing `.md` meta.

**Was owed (kept for the record):** Unity
consumes a *precompiled* `CultMath.dll` pinned at `cultmath-unity-v0.2.3`
(`Packages/manifest.json:56`), so no Unity-side fire-control code can call `erf` until the
package is rebuilt, bumped to 0.2.4, tagged and repinned. The headless tests see the
sources directly, which is why nothing failed. 12.2 must not land in Unity before that.

**Recorded, not done:** `erf`/`erfinv` are scalar-only while every other scalar in
`math.cs` ships float2/3/4 overloads. The consumer is scalar; vector overloads are a
separate decision.

### Cut 12.1. One frame owner, and the dead outcome flag

- **Repo/branch:** Aetheria, off the Cut 11 merge. No behaviour change.
- **Deletes first:**
  - The two inline frame copies:
    - `FireControl.cs:516-520` becomes `target.ToSchematic(toTarget)`, normalized, keeping
      the zero-length fallback. The whole of Splash dies in 12.4, but the frame owner lands
      first.
    - `Entity.cs:394-396` becomes `ToSchematic(hitDirection)`.
  - `ShotOutcome.Aimed` and its plumbing (Q12-4 = A):
    - the field, `FireControl.cs:702`;
    - the `aimed` parameter of `MakeOutcome` (`:470`, `:481`);
    - `Commit`'s `aimed` local and flag (`:420`, `:435`);
    - the `AimedCells` half of `ResolveAimPoint`'s tuple (`:583-589`), which now returns
      `float2`.
- **Adds:** `Entity.ToSchematic(float2)`.
- **Verification:**
  - Builds: headless `Aetheria.Shared`, plus a Unity batchmode compile by Self.
  - All 244 tests pass unchanged. `SplashDamagesTheSideTheBlastCameFrom` and
    `PenetrationMarchIsPlanar` pin the frame through its new owner.
  - Negative greps:
    - `rg -n "forward\.y, -forward\.x" Assets/Scripts` returns exactly one hit, in
      `Entity.cs`.
    - `rg -n "Outcome\.Aimed|outcome\.Aimed|o\.Aimed" Assets/Scripts tests` returns
      nothing.

    Both were checked against the current tree: the first finds today's two copies, and the
    second finds nothing.

**Status (2026-09-24): landed** at `b851b0d1` (+21/-18 across `Entity.cs` and
`FireControl.cs`). 244 tests pass, both greps as specified (the one frame copy is
`Entity.cs:383`, inside `ToSchematic`), Unity batchmode compile clean, and nothing outside
`FireControl.cs` read `ShotOutcome.Aimed` (the HUD holds outcomes but never touched it).
Stryker on the changed lines: the frame's sign mutant dies; the only survivors near the diff
are `ResolveAimPoint`'s pre-existing centroid arithmetic (`aimedCells.Length > 0` → `>= 0`,
`/` → `*`), which 12.2's `AimingAtAnItemCentresTheScatterOnItsLane` is specified to close.
`--since` does not work in this repo (Stryker's own diff comes back empty), so runs use
`--mutate` on the touched files and filter the report to changed lines. No separate Soul pass:
a no-behaviour cut this size folds into 12.2's.

### Cut 12.2. Where a hit lands

- **Repo/branch:** Aetheria, on top of 12.1, with the 12.0 pin in place.
- **Deletes first:**
  - `HullKernel` and its comment block (`FireControl.cs:531-578`).
  - `WeightedPick` (`:591-611`).
  - `POnHull` (`:229-234`).
  - Apply's live direction (`:463-465`).
  - The bounding half-extent (`:224`).
  - The 9.2 SigmaFloor rationale (`:542-548`). Rewrite it as described above.
  - The ActionGameManager inline estimate (`:1317-1318`).
- **Keeps:** `Sigma`, `SigmaFloor`, `ResolveAimPoint`, `CellsOf`, the gate order
  (`:150-159`) and `Inspect.PBase = HitProbability(...)` (`:204`).
- **Adds:** `TravelDirection`, `Silhouette`, `CommitProbability`, the lateral draw, `Lane`
  (only its first element is used in 12.2), and these fields:
  - `PendingShot`: `TravelDirection`, `Spread` and `FireRange`; `PBase` is renamed to
    `PFire`.
  - `ShotOutcome`: `Bearing` and `Lateral`.
- **Per-file changes:**
  - `FireControl.cs:161-167`: after the gates, `PFire(...) × PSpread(...) ×
    Silhouette(...).POnHull`. The bearing comes from `TravelDirection` and the target's
    current facing. Nothing new runs before `:159`.
  - `:197-202`: Inspect uses the same functions. It gains nothing new beyond
    `diagnostic.PFire` if the HUD wants it (optional; presentation).
  - `:263`, `:286-308`: Fire freezes `PFire`, `TravelDirection`, `Spread = weapon.Spread`
    and `FireRange`.
  - `:409-435`: Commit takes steps 1-5 of **Bearing timing** through `CommitProbability`.
  - `:463-467`: Apply passes `shot.Outcome.Bearing` (schematic frame) to `ApplyHit`. For
    this one cut, ApplyHit's parameter becomes a schematic bearing and the `ToSchematic`
    call 12.1 put inside it is removed.
  - `ActionGameManager.cs:1317-1318`: the pending line prints
    `FireControl.CommitProbability(shot, now, out _)` instead of `shot.PBase * pDeviation`.
  - `Combat.cs:117` and `TurretController.cs:84` need no change. They read HitProbability,
    which is now a facing-aware forecast. Expect the AI to prefer broadside shots on long
    hulls, and targets that turn to lose fewer hits. That is tuning, not a defect.
- **Authority map:**
  - Owner:
    - `FireControl.Silhouette` owns scatter.
    - `FireControl.CommitProbability` owns the commit-time roll price and placement
      source.
    - `FireControl.TravelDirection` owns the shot's world direction.
    - `Entity.ToSchematic` owns the frame.
  - Inputs:
    - Frozen: `PFire`, `TravelDirection`, `Precision`, `Aimed`, `Spread`, `FireRange`.
    - Live at Commit: target position (deviation) and target facing (bearing).
  - Outputs: p for the one roll, then `Bearing`, `Lateral` and `Cell` in the immutable
    outcome.
  - Derived state:
    - HitProbability and Inspect are a forecast of the commit price and decide nothing.
    - `ShotOutcome.Cell` is display-only, for the HUD (`ActionGameManager.cs:1323`) and
      the tests.
  - Forbidden writers:
    - Nothing else computes a sigma, a projection or a bearing.
    - Nothing after Commit reads a target's facing or anyone's position for placement or
      damage direction.
    - The HUD computes no probability of its own.
  - Shared paths: InstantWeapon (`InstantWeapon.cs:248`), beam rolls
    (`ConstantWeapon.cs:181`, where zero flight time makes the fire tick the commit tick),
    the AI, turrets and the HUD.
  - Deletion line: `HullKernel`, `WeightedPick`, `POnHull` and the HUD estimate are gone
    before `Silhouette` has a caller.
- **Verification.** Fixtures follow 11.3: the shooter is off the origin, fire times are
  nonzero, facings include ones where |fx| > |fy|, and weapon spread is nonzero wherever
  PSpread is asserted.
  - `BroadsideIsEasierThanHeadOn`: on a 2x12 hull, pOnHull and PSpread head-on are strictly
    below broadside, at facings (0,1) and (1,0). It kills the bearing being ignored and the
    old `max(W,H)`.
  - `TurningArmourIntoTheShotTakesItOnTheArmour` (the ruling's own case):
    - Setup: a slow shot (flight time well above `CommitHorizon`) at the soft stern of a
      wedge with bow-only plate. The target turns its bow to face the shot's
      `TravelDirection` before commit.
    - Pass: every hit's `Cell` is on the bow row.
    - Kills: freezing the bearing at Fire; reading the facing at Fire.
  - `TurningAfterCommitChangesNothing` (R4, and closes L464/L465):
    - Setup: through `Fire`. After commit and before arrival, the target turns 90° and the
      shooter moves.
    - Pass: the damaged cells equal those implied by the committed `Bearing`/`Lateral`.
    - Kills: Apply reading live positions or facing.
  - `PlacementAndProbabilityShareOneSilhouette` replaces 6d's
    `PlacementAndProbabilityShareOneKernel` (`FireControlCut6dTests.cs:318`).
    - Setup: a static target at a diagonal bearing, so the intervals overlap and the
      forecast equals the commit price.
    - Pass: `CommitProbability` equals a pOnHull that the test computes independently, by
      numerically integrating the Gaussian over a union of intervals the test builds itself.
      The empirical `Lateral` distribution matches the per-interval shares, and the
      empirical hit rate matches p.
    - Kills: sigma or bearing perturbed at one site; the union merge skipped; ℓ swapped
      with b.
  - `TheHudEstimateIsTheCommitPrice`: for an in-flight shot, the value the HUD prints and
    the p Commit rolls against are the same number at the commit tick. It kills a second
    formula.
  - `HitsLandOnTheFacingEdge` supersedes `EveryHitLandsOnMetal` (`:357`): on the concave
    fixture, each hit's `Cell` is occupied, and nothing occupied precedes it in its own lane.
    The test recomputes the lane from `Bearing` and `Lateral`.
  - `AimingAtAnItemCentresTheScatterOnItsLane` closes L585/L587 and replaces
    `AimingAtTheSternHitsTheStern` (`:213`), whose fixture fires from the target's stern
    (`FireControlCut6dTests.cs:163-164`) and would pass for the wrong reason.
    - From broadside, hits aimed at the stern land mostly in the stern half, and hits aimed
      at the bow land mostly in the bow half.
    - From dead astern, hits aimed at the bow land on the stern edge.
  - `TheSigmaFloorHoldsAtHalfACell`: pOnHull at Precision 1000 equals pOnHull at Precision 2
    on a one-cell lane. The exact integral no longer collapses, so this test, not the cliff
    test, is now what kills "delete the floor". Keep the cliff test and rewrite its
    mutation comment.
  - `ShieldIsOmnidirectional`: an active shield absorbs the same shot identically from bow,
    stern and beam.
  - Kept green:
    - `GatedOutHitProbabilityAllocatesNothing`.
    - `TheHudShowsTheFactorsTheSimulationMultiplies`, re-read as forecast factors.
    - `PSpreadIsTheSilhouettesShareOfTheSpreadCone`, which fires from astern at a 5-wide
      hull. There the span is 5, equal to the old `max(5,3)`, so its numbers stand.
    - `ThinLimbCostsHitChance`, with its comment rewritten in 1D terms.
    - Every test that reads `shot.PBase`, updated to `PFire` with its expectation
      unchanged.
  - Stryker: `--mutate "**/FireControl.cs" --since:<12.1 head>`. Survivors are triaged by
    name, and float boundary flips count as equivalent (11.2).
- **Operator:**
  - A smoke against a LonginusX-class AI from the bow and then the beam. The HUD `hull`
    factor should read higher from the beam.
  - Turn your armoured face into a slow missile and watch where it lands.

**Status (2026-09-25): closed after three Soul passes.** Landed on `codex/fire-control-12`:
`7dbb3bcf`, `efe4f7c9` (cut and first Stryker batch), `8bd25f6f` (Soul pass 1 fixes), `d38efba3`..`ca1670a2`
(Soul pass 2 fixes). 263 tests, identical across runs; Unity batchmode compile clean at each stage.

What the Soul passes found, in order, because two of them are lessons for 12.3:
- **Pass 1.** A lateral draw clamped to closed `[Lo, Hi]` could land exactly on `Hi`, return an empty lane,
  and fall back silently to cell (0,0), a hit on nothing. The statistical tests used `Guid` zone names, so
  every run rolled different dice (the source of a "flake" that got its threshold tuned instead of its cause
  found). Where a hit lands was untested, only its price; the HUD test compared the commit price to itself;
  `HitProbability` and `Inspect` each assembled the forecast, and only Inspect's copy was guarded; penetration
  could follow a turn made after commit; multi-cell aimed items were untested.
- **Pass 2.** The pass-1 fix clamped the top edge with an epsilon and turned the silent (0,0) into a throw.
  The **bottom** edge still produced empty lanes at angled bearings, so the fix converted a rare wrong result
  into a rare crash (~1 per 2^24 hits, every shipped hull), reproduced through `Fire`→`Zone.Update`. Root
  cause: **two geometric models answering one question** — `Silhouette` priced each cell's projected interval,
  `Lane` re-derived admission with an independent slab test, and floats disagreed at corners. Also: the
  two-prong placement test passed five wrong-sigma draws; "never allocates" was still false (CoreCLR wraps an
  `IComparer` in a delegate per sort).
- **Pass 3 closed it.** `Lane` now admits with the same `Extent` support function `Silhouette` uses, and the
  slab test is gone (`AlongBearing` survives as a non-rejecting entry/exit computer). Proof and check: all
  cells' intervals have equal width, so the merge cannot open a gap; exhaustive check at every breakpoint and
  float neighbour (8.9M lateral values, 13 hulls, 1,540 bearings down to 1e-9 rad) and a 402M-draw enumeration
  found 0 empty lanes and 0 non-finite values; a split-arithmetic mutant of `Lane` (equal in exact
  arithmetic, different in float) is killed by two tests, so the one-owner rule is guarded, not incidental.
  The `Commit` invariant throw is unreachable.

Cost, measured in Release: pooling the forecast's interval buffer took gated-in `HitProbability` on LonginusX
from 672 to 184 B/call (time unchanged; the ~1.9× over the old kernel is the sort). What remains is outside
fire control's arithmetic: 40 B per `GetBehavior<T>` (the `Equipment` enumerator), three per call; and with an
item aimed at, `ResolveAimPoint`→`CellsOf` builds a list and `ToArray`s it every call — 944 B per
`HitProbability`, 1928 B per `Inspect`. Recorded as follow-ups.

**Carried into 12.3's brief** (low, all on code 12.3 builds on): pin `Lane`'s seam contract (a 2x1 hull at
b=(0,1): `Lane(s=-0.5)` returns only (0,0), `Lane(s=0.5)` returns nothing — the `s >= Hi` → `s > Hi` mutant
survives today and would interleave two columns in 12.3's walk); `AlongBearing`/`SlabAxis` have no test of
their values (every mutant there survives, equivalent for 12.2 only); correct the allocation comments in
`Forecast` and `LaneAndSilhouetteAllocateNothingGivenAPooledBuffer`; make
`ZeroProbabilityShotCommitsAMissWithoutBuildingASilhouette` behavioural (produce `PFire = 0` through
visibility, assert zero allocation rather than `sil.Intervals == null`); drop the unused `System.Reflection`
import.

### Cut 12.3. How a direct hit travels

- **Repo/branch:** Aetheria, on top of 12.2.
- **Deletes first:** `Entity.DamageSchematic` (`Entity.cs:333-374`) and `Entity.ApplyHit`
  (`Entity.cs:376-409`). Going with them:
  - the 0.5-step march;
  - the `+ .5` cell convention;
  - the `> .5f` threshold;
  - the Expand spread footprint.
- **Adds:**
  - `Entity.Absorb(int2 cell, float damage) → float`, moved verbatim from
    `Entity.cs:348-366`. Armour absorbs up to its value, then the item when `d > .1f`, and
    the remainder is returned. It emits `ArmorDamage((cell, incoming))` and
    `ItemDamage((item, incoming))` with today's meaning, and only for incoming > 0.
  - `Entity.DamageHull(float)`, moved from `Entity.cs:369-373` (`> .1f`, one `HullDamage`
    event).
- **Per-file changes:** `FireControl.Apply` (`:449-468`), with the shield branches
  unchanged:
  1. `IncomingHit.OnNext(source)` moves here from `Entity.cs:383`.
  2. There are `2n+1` lanes, `k ∈ [-n, n]`, where `n = (int) floor(DamageSpread + .5f)` (today's
     rounding, `Entity.cs:389`). Lane `k` sits `k` cell *shadows* (`2h`) from `Lateral` along ℓ, and
     **no cell belongs to two lanes** (Q12-2 = A, amended 2026-09-25). Membership is decided once per
     cell, not by testing float lane positions against float intervals (Soul, 12.3 pass 2: those two
     derivations disagreed at cell edges and struck a cell twice).
  3. Damage splits evenly over the lanes that meet metal. The centre lane always does.
  4. Each lane walks from its own facing cell, clipped to the penetration depth, with
     `rem = target.Absorb(cell, rem)` at each cell.
  5. Each lane's final remainder goes to the hull, where the lane ends: penetration
     exhausted, a gap, or the far side (Q12-3 = A). The remainders are summed and passed to
     `DamageHull` once.
  6. A weapon with a contact or delayed fuse does not take this path. It detonates instead
     (12.4), never both.

  **Where 12.3 and 12.4 meet.** Which model a shot uses is decided by the blast radius, not
  by its penetration:
  - **No blast radius — a dumb AP round.** Steps 1-5 are the whole model, whatever the
    penetration. The shot travels its lane and is absorbed cell by cell in order, with the
    remainder carrying on until it is spent or the lane ends (Q12-3). Damage in a line. More
    penetration spreads the same damage over more cells; it never concentrates it at depth.
    Q12-8 says nothing about this case.
  - **With a blast radius — an explosive penetrator.** The shot reaches its fuse depth
    without spending damage on the way, and then detonates as an area (12.4). Q12-8 governs
    only this case, because the payload is the explosion, not the dart.

  The two roles that follow are the design intent: the AP round is the pinpoint component
  sniper, a thin line of damage through whatever it crosses, and the explosive penetrator
  is the interior wrecker, a disc across a whole section.

  Splash is untouched in 12.3 and deleted in 12.4. Until then it calls `Absorb` per
  footprint cell and passes the summed remainder to `DamageHull`, which is arithmetically
  identical to today.
- **Authority map:**
  - Owner: `Entity.Absorb` owns what one cell does with incoming damage.
    `FireControl.Apply` owns the order and the share per lane.
  - Inputs: the committed `Bearing`, `Lateral` and `Cell`, plus the frozen `Penetration`,
    `DamageSpread` and `Damage`.
  - Outputs: `Armor[]`, item `Durability` and `Hull.Durability`, plus the events
    `IncomingHit`, `ArmorDamage`, `ItemDamage` and `HullDamage`. Their consumers:
    `ActionGameManager.cs:1070`, `EntityInstance.cs:292`, `InventoryPanel.cs:470-482`,
    and `Entity.cs:488-494` (ItemDestroyed, HullArmorDepleted, Death).
  - Forbidden writers: no weapon hit writes armour or item durability except through
    `Absorb`. `Behaviors.CauseDamage` (`Behaviors/Behaviors.cs:62-73`) is self-damage and
    out of scope.
  - Deletion line: both Entity methods are deleted before `Absorb` gets a second caller.
- **Verification:**
  - `DamageIsAbsorbedInOrderAlongTheRay` replaces `HardpointHitDamagesItemThenHull` and
    `PenetrationMarchIsPlanar` (`FireAuthorityTests.cs:481-526`, which call the deleted
    `ApplyHit`).
    - Setup: through `Fire` at facing (1,0). The lane runs through a 10-armour front cell,
      then a marker, then a soft cell, with damage 30 and penetration 3.
    - Pass: armour −10, marker −20, soft cell untouched, hull −0. Today it would be
      10/10/10 **(probe)**.
    - Kills: an even split; items before armour; no carry-forward.
  - `ArmourFacesTheShot`:
    - Setup: a wedge with bow-only plate, penetration 0, many shots. Facings (0,1), (.8,.6)
      and one where |fx| > |fy|.
    - Pass: from ahead only bow cells lose armour, and from astern the reverse.
  - `BuriedCockpitNeedsPenetration`: a cockpit enclosed by one ring of cells.
    - At penetration 0 it is never damaged from four bearings.
    - At a penetration that clears the ring, it takes exactly `damage/lanes` minus what the
      ring absorbed.
    - Enough damage fires `Death` with `CockpitDestroyed`.
  - `SpreadWidensTheImpactAcrossLanes`: spread 1 at penetration 0 puts damage/3 on each of
    three facing-edge cells and nothing behind them.
  - `ARayThroughACornerCrossesTheCornerCell`: the diagonal case from `Probe1` reaches (2,2).
  - `LaneRemainderReachesTheHull`: a ray that exits a thin hull delivers its remainder to
    `Hull.Durability` (Q12-3 = A).
  - Kept green: every Splash test, and `AirburstSplashes…` (Cut 6).
  - Negative: `rg -n "DamageSchematic|ApplyHit\(" Assets/Scripts tests` returns nothing.
    Today it matches `Entity.cs`, `FireControl.cs`, `FireAuthorityTests.cs`, and two comment
    lines at `Gameplay/EntityInstance.cs:280-281`. Rewrite those comments and
    `FireControl.cs:492`.
  - Stryker: `--since:<12.2 head>` over `FireControl.cs` and `Entity.cs`.
- **Operator:** a smoke with a GT 3K or pswarm launcher into a LonginusX bow, then from a
  flank. The schematic display should pulse the facing edge only.

**Status (2026-09-25): closed apart from one pending operator ruling** (multi-cell items shared
between lanes, below). Landed on `codex/fire-control-12`: `70c3e0cc`..`73b15d9f` (cut and first
Stryker batch), `b82b7eab`..`be538217` (Soul pass 1 fixes and the lane-spacing ruling),
`4e6495dd`, `742354c6`, `cb71a3cf` (Soul pass 2 fixes). 286 tests; Unity batchmode clean at each
stage.

What the three Soul passes found:
- **Pass 1.** Production correct (an independent double-precision ray model agreed on 14,984 of
  15,000 shots; the rest sit on the √2 float boundary), tests blind: no test drove `Apply` at an
  angled bearing, the gap rule, penetration depth or side-lane remainders; one test could not fail.
  It also found lanes one cell apart **share cells** at angled bearings, which the operator ruled on
  (Q12-2 amendment: no doubling up, the line thickens).
- **Pass 2.** The spacing fix was disjoint only by float luck (78 of 26,377 boundary-lateral hits
  struck a cell twice), and the disjointness test missed four spacing breakages. Hands' first
  structural fix (a `ceil` lane index) introduced a **third** membership computation, which left the
  centre lane empty on `solid5, b=(0,1), s=0.49999997` while `Lane(s)` found the row — a committed hit
  with no damage. Final shape: `Lanes` is built from `Lane`, `Lane(s)` is literally lane 0, and float
  drift is resolved by ownership (nearest the centre wins). **The third time this campaign met two
  derivations of one geometric answer disagreeing in float** (12.2's crash, 12.3's double strike,
  the `ceil` empty centre); the cure each time was one owner.
- **Pass 3 closed it.** 2.09M boundary cases over 196 bearings and 9 hull shapes: no cell in two
  lanes, lane 0 always equals `Lane(s)`, no empty centre lane. The tie-break is mirror-symmetric by
  construction. Residual float effects (a side lane starting one cell deeper, a column falling
  between lanes, rarely a footprint narrowed by one column) occur at about 2×10⁻⁶ of draws; damage
  is never lost.

**Owed with the pending ruling's batch:** a test that many axis-aligned spread-n shots each strike
exactly 2n+1 facing cells (a 1% spacing error, R1b, survives today and would drop a column from 1-2%
of spread shots). **Recorded, not reachable through the public pipeline at a usable rate:** nothing
ties `Apply` to `Lanes` behaviourally (reverting `Apply` to a per-lane `Lane()` loop survives, since
it only reintroduces boundary double strikes); the behaviour after a cession is unpinned.

**Pending operator ruling (blocks closing 12.3):** when two lanes cross one multi-cell item, the
first-processed lane drains its durability and the second carries the leftover into its own deeper
cells, so the left-of-travel lane always gets the damage that passes wide components (mirror shots on
a symmetric hull with 2x1 items differ in ~30% of axis-aligned trials). Recommended: the item absorbs
from all lanes that strike it at once, and each lane's leftover is in proportion to what it brought.
Alternative: keep the processing order and pin it.

**Ruled 2026-09-25: proportional absorption.** Operator: "proportional absorption; go". A multi-cell
item struck by several lanes of one shot absorbs from their combined incoming damage, and each lane
carries on with a leftover in proportion to what it brought. The result must not depend on the order
lanes are processed; with no shared item it equals sequential absorption exactly.

### Cut 12.4. The detonation primitive

- **Repo/branch:** Aetheria, on top of 12.3. It lands as two commits:
  - (a) the data fields and the label demotion. This is behaviour-neutral on shipped
    content, because no weapon carries the label or a radius **(probe)**.
  - (b) `Detonate`, with Splash deleted.
- **Deletes first:**
  - The label read `isAirburst = … WeaponModifiers.HasFlag(WeaponModifiers.Airburst)`
    (`FireControl.cs:281-284`), in (a). Afterwards `WeaponModifiers` has no simulation
    reader. `rg -n "WeaponModifiers\." Assets/Scripts/ServerShared` returns nothing (today
    it returns exactly `FireControl.cs:282`).
  - `FireControl.Splash` (`:488-529`) and its half-hull footprint, in (b). This absorbs
    F12-1.
  - `PendingShot.BurstRadius` (`:648-652`) becomes `BlastRadius`, and `BurstPosition` stays.
- **Data (smallest field set, both on `WeaponItemData`, `ItemData.cs:476-499`):**
  - Slot 29, `float? AirburstRange`, is renamed `float? BlastRadius` (JSON `"blastRadius"`),
    in world units, meaning the same thing it meant for airburst. Same slot, same type:
    CultCache resolves it as compatible drift (see the probes).
  - New slot 30: `WeaponFuse? Fuse` (JSON `"fuse"`), with `enum WeaponFuse { Contact,
    Proximity, Delayed }` in `Enums.cs`. It is nullable per the 7.4 rule. Null means no
    blast, which is what every shipped record reads as.
  - A shot detonates iff `Fuse != null && BlastRadius > 0`. Fire freezes both
    (`PendingShot.Fuse`, `BlastRadius`). A fuse without a radius (or a radius without a
    fuse) is inert, and nothing reports it: Q12-6 ruled that the catalog is not policed.
  - The catalog needs one write through `tools/AetherDb` or Studio to clear the drift
    warnings. That write is content, with no data change, and is recorded as part of the
    retune follow-up.
  - Readers to update: `FireControl.cs:284` and `FireControlCut6Tests.cs:81-101, 249, 268,
    279`, which author `WeaponModifiers.Airburst`. Those fixtures move to
    `Fuse = Proximity`. Whether those fixtures keep the label is free: Q12-6 ruled that
    nothing checks labels against behaviour.
- **Adds:** `FireControl.Detonate(Zone zone, float2 worldPoint, float radius, float damage,
  DamageType type)`, the one owner of blast damage, plus `Entity.ToSchematicPoint` and
  `ToWorldPoint` (see the frame rules).
  - **Where P is:**
    - Proximity: Step calls it at arrival with the frozen `BurstPosition`
      (`PredictedIntercept`, `:283`). There is no roll, as today (`AirburstSplashesEvenOnA
      GuaranteedMiss` stands).
    - Contact: on a committed, unshielded hit, P is the entry point of the impact cell on
      the committed lane.
    - Delayed: on a committed, unshielded hit, P is the end of the penetration walk on that
      lane (penetration exhausted or first gap). The walk only locates P; nothing is
      absorbed along it (Q12-8), and that is true only because this weapon carries a blast.
      A weapon with no blast radius never reaches this path and spends its damage down the
      lane instead (12.3).

    Both contact and delayed are converted with `target.ToWorldPoint`, so every entity in
    the radius (the host included) is treated alike. A shielded contact or delayed hit is
    absorbed whole, as a direct hit is, and does not detonate. `Mine.cs:99` calls
    `Detonate`.
  - **Area, per entity:**
    1. Candidates are the entities with `|Position.xz - P| ≤ radius + boundingRadius`,
       where `boundingRadius = ½·√(W²+H²)·cellSize`.
    2. For each candidate, `Ps = ToSchematicPoint(P)` and `rCells = radius /
       SchematicCellSize`.
    3. For every cell of the candidate's schematic inside the bounding box
       `[Ps ± (rCells + .5)]`, compute `overlap(c)`, the exact area of the disc of radius
       `rCells` centred on `Ps` intersected with that cell's unit square.
    4. An occupied cell takes `damage × overlap(c) / (π · rCells²)` through
       `Absorb(c, …)`, and the remainder of that share goes to `DamageHull`. An unoccupied
       cell's share is lost, and so is every part of the disc outside the schematic.
    5. Entities are independent. Two ships caught by one blast each cover their own cells,
       and neither shadows the other.
  - **Why exact overlap and not sampling.** The overlap is the standard circle-rectangle
    intersection, evaluated per cell from the antiderivative `½(x√(r²-x²) + r²·asin(x/r))`
    over the cell's clipped x-span, combined across the rectangle's edges. It is closed
    form, allocation-free, and exactly conservative: the overlaps over the bounding box sum
    to `π·rCells²`, so shares sum to 1 and the only loss is geometric. Supersampling would
    be cheaper to write and would break that: a blast smaller than one cell, or one
    straddling a cell boundary, would deliver the wrong total, and the conservation test
    below could only be written with a tolerance wide enough to hide real errors.
  - **Shields:** an entity with an active shield is charged the summed share of the disc
    that its occupied cells cover, under the same `CanTakeHit`/`Break`/`TakeHit` rules as
    today.
    - If the shield absorbs, no cell of that entity takes anything.
    - If not, it breaks and the cells take their shares.

    Today every shield in radius pays the full damage (`:505-513`). Charging the covered
    share makes the shield pay only for what reaches the ship, which is consistent with
    "omnidirectional": the reserve has no facings, but it is not charged for energy that
    went past the hull.
  - **Cost:** `candidates × (2·rCells + 2)²` overlap evaluations, about 3 × 27² ≈ 2k for a
    25-unit blast at the default cell size, once per detonation and never per tick. No
    allocation: the loop writes nothing but the per-cell absorption.
  - **Accepted loss.** Interior armour no longer shadows the cells behind it, because
    nothing is occluded within an area. A bulkhead protects its own cell and no other. The
    ray model bought that shadowing, and the operator cut the ray model. Falloff toward the
    rim is F12-7, not this cut.
  - **HitProbability and Inspect** are unchanged:
    - A contact or delayed blast still needs the shot to hit, so the same forecast
      applies.
    - A proximity blast has no roll and delivers whatever geometry gives. The forecast
      still reports the direct-hit number for it. That is pre-existing, and AI gating on it
      is recorded as F12-5.
- **Authority map:**
  - Owner: `FireControl.Detonate` owns blast damage and the disc's coverage.
    `Entity.Absorb` owns per-cell absorption, shared with 12.3. `Lane` owns traversal and
    is a direct-hit function only; a blast calls it just once, for the delayed fuse's
    point, on the walk 12.3 already performs.
  - Inputs:
    - world P, radius, damage and type;
    - each entity's position, facing, hull, armour and occupancy at the detonation tick;
    - the fuse and radius, frozen from weapon data at Fire.
  - Outputs: the same mutations and events as 12.3.
  - Derived state: `WeaponModifiers` is display-only: `SchematicListElement.cs:54` icons,
    `GameSettings.cs:67`, and `GameplaySettingsEditor.cs:44`.
  - Forbidden writers:
    - No label decides behaviour.
    - Nothing but `Detonate` applies area damage.
    - Mine's Unity `BlastRange` and arming query remain presentation-side inputs (R8
      residue, Cut 4 Q7) and decide no damage themselves.
  - Shared paths: the airburst resolution in `Step` (`:356`), Mine (`Mine.cs:99`), and
    contact and delayed hits from `Apply`.
  - Deletion line: `Splash` and the label read are deleted before `Detonate` is called.
- **Verification:**
  - `TheDiscIsConservedOverTheGrid`: over a solid hull large enough to contain the disc,
    the total damage delivered equals `damage` within float tolerance, at several radii
    including one smaller than a cell and one centred on a cell corner. It kills a
    sampling approximation, a wrong normalizer, and a bounding box that clips the disc.
  - `EachCellTakesItsShareOfTheDisc`: on a solid hull, the damage on a named cell equals
    `damage × overlap / (π r²)`, with the overlap computed independently by the test
    (fine numeric integration). It kills an even split over the covered cells, which is
    what today's Splash does.
  - `ExternalBlastWastesMostOfItsEnergy`: a proximity blast beside a hull delivers total
    armour + item + hull damage well below `damage`. The expected share is the disc's
    overlap with the schematic, computed by the test.
  - `InternalBlastDeliversIt`: the same blast deep inside the hull delivers nearly all of
    `damage`, and damages interior items an external blast of the same radius cannot reach.
  - `PenetratorBurrowsBeforeItBursts`: a delayed-fuse hit with penetration 2 into the
    enclosed-cockpit fixture damages the cockpit, while a contact-fuse hit with the same
    stats does not. It also pins Q12-8's scope, in two halves:
    - With a blast radius, the armour the burrow passes through absorbs nothing, so the
      cockpit's share is the full area share of its own cell.
    - With the radius removed and everything else held, the same shot is a dumb AP round:
      it damages the cells along its lane in order, and the cockpit gets only what the
      armour and items in front of it left (12.3). It kills a build that routes every
      penetrating shot through the fuse path.
  - `ABlastDamagesTheCellsNearestIt`: a blast to port damages port cells and leaves
    starboard cells untouched, at facings including |fx| > |fy|. It is the successor of
    `SplashDamagesTheSideTheBlastCameFrom` and `SplashIsDirectional`, and it is what pins
    `ToSchematicPoint`'s handedness.
  - `ShieldPaysForWhatReachesIt`: a shield in radius is charged the covered share, not
    the whole blast. `SplashShieldAbsorptionDrainsTheReserve` and
    `SplashBreaksUnabsorbedShield` are rewritten to the covered share, with the expected
    amounts recomputed rather than loosened.
  - `LabelsDoNotDecideBehaviour`: a weapon labelled `Airburst` with no fuse resolves as a
    direct hit, and a weapon with `Fuse = Proximity` and no label detonates.
  - Kept green: `AirburstSplashesEvenOnAGuaranteedMiss` and its no-double-application twin,
    re-fixtured to `Fuse`.
  - Negative: `rg -n "Splash\(|HasFlag\(WeaponModifiers" Assets/Scripts tests` returns
    nothing. Today it matches `FireControl.cs`, `Mine.cs` and `FireControlCut4/5/6/11Tests.cs`.
  - Stryker: `--since:<12.3 head>`.
- **Operator:** author one proximity weapon and one delayed-fuse weapon in a scratch
  catalog. Watch an airburst beside a LonginusX and a penetrator into its nose, and check
  that the damage lands where the model shows the blast. That also verifies
  `ToSchematicPoint`'s centre-of-mass anchor.

### 0b. Identity, lifecycle, authority

Two persisted changes, both on `aetheria.weaponitemdata` v1 and both in 12.4:

| Field | Slot | Change | Lifecycle | Reader risk |
|---|---|---|---|---|
| `BlastRadius` (was `AirburstRange`) | 29 | rename, same `float?` type | authored catalog | compatible drift: `CompareSchemaShapes` ignores member names (`CultCache.cs:672-744`); drift warnings until the next catalog write |
| `Fuse` | 30 | new, `WeaponFuse?` | authored catalog | nullable, so older records read as null ("no blast"), per the 7.4 rule |

Runtime-only and never serialised (0b table, `FireControl.cs:626-629`, `:686-689`):

- `PendingShot`: `PFire`, `TravelDirection`, `Spread`, `FireRange`, `Fuse`, `BlastRadius`.
- `ShotOutcome`: `Bearing`, `Lateral`.

Nothing in saves changes shape.

### Operator questions

Ruled 2026-09-22 (the words are recorded under **Rulings**):

- **Q12-1: B**, the bearing is taken at Commit.
- **Q12-2: A**, spread is width.
- **Q12-3: A**, the lane remainder goes into the hull where the lane ends (direct hits).
- **Q12-4: A**, `ShotOutcome.Aimed` is deleted.
- **Q12-6: no.** Operator: "12-6, nope". No catalog test ties a label to behaviour.
  `WeaponModifiers` stays authored metadata that nothing checks and nothing reads in the
  simulation, and a weapon whose label disagrees with its data is authoring's business.
- **Q12-7: dissolved, not answered.** It asked where a blast ray's remainder goes. With
  blasts as areas there are no rays and no remainder to route, so the question has no
  subject. The ray model's shadowing goes with it (see **Accepted loss** in 12.4).
- **Q12-8: A, and it is scoped to weapons that carry a blast.** Nothing is absorbed along
  an *explosive penetrator's* burrow: it reaches its fuse depth without spending damage,
  and the explosion is the payload. Penetration is a reach decision there — how deep the
  fuse point can sit — not a damage budget.

  The first wording ("the burrow is travel, not damage") read as if it covered every
  penetrating shot. It does not. Operator, 2026-09-22: "Q12-8 is odd. Like, penetration is
  still a thing even if it's not a warhead. For a dumb AP round, you'd expect it to do
  damage in a line, not dump all its damage at max depth, that would make AP rounds even
  better at component sniping than AP explosives". Agreed, and the map never meant
  otherwise: a weapon with no blast radius takes 12.3's lane, absorbed cell by cell in order
  with the remainder carrying on, and Q12-8 does not reach it. See **Where 12.3 and 12.4
  meet**.

  So "armour absorbs first" holds everywhere it was ruled: down the lane for a direct hit,
  and per covered cell for a blast. Against an explosive penetrator, armour protects by the
  reach gate and by absorbing its own cell's share of the disc. The consequence to watch
  when F12-2 tunes penetration is that an explosive penetrator with reach pays nothing for
  the plate it crosses, while an AP round pays for every plate it passes.

Open:

- **Q12-5 (not ruled). Should the HUD show which target items are exposed from the current
  bearing?**
  - **Recommended:** a follow-up after 12.3. It would be a presentation read of `Silhouette`
    and `Lane`, the way Inspect is: for each revealed item, is it first in some lane of the
    current shadow. It belongs on the target-item cycling UI.

### Follow-ups recorded, not in this cut

- **F12-2. Penetration retune** (operator: "Retuning penetration is a follow up.").
  - Shipped values are ≤ 0.25 against an inspector range of 0..1, and enclosed cockpits
    need about 2 cells.
  - The same pass decides which weapons get fuses and blast radii (none have one today),
    and writes the catalog once to clear the slot-29/30 drift warnings.
  - Until it lands, interior cells are reached only by blasts.
- **F12-3. SigmaFloor's purpose changed.** It was a numerical guard and is now a design
  minimum on group tightness. Revisit it when Precision is next tuned.
- **F12-4. Double charging.** 6d's note that pSpread and pOnHull both price target size
  still stands. Both now read one Span and shadow, so a later merge is mechanical.
- **F12-5. AI and proximity blasts.** `Combat.cs:117` gates a proximity weapon on a
  direct-hit forecast that does not decide its damage. This is pre-existing.
- **F12-6. Mine's blast inputs.** The prefab `BlastRange` and the arming OverlapSphere
  (`Mine.cs:69-78`, `:99`) are presentation-side (Cut 4 Q7). Mine has no catalog weapon
  record carrying `Fuse`/`BlastRadius`. It can move when it ships (R9).
- **F12-7. Blast falloff toward the rim.** Every cell takes its area share flat, so a cell
  at the rim is hurt as hard per unit of area as the one under the fuse. A radial falloff is
  a tuning decision for the same pass that authors radii, and it changes only the weight
  inside the integral, not the owner.
- **F12-8. Blasts do not shadow.** Nothing inside an area is occluded, so a bulkhead
  protects only its own cell, and two ships caught by one blast do not shade each other.
  This is the accepted cost of areas over rays. Reopen it only if play shows interiors are
  too soft.
- **Behaviors.CauseDamage** (`Behaviors/Behaviors.cs:62-73`) writes durability and
  `HullDamage` without `Absorb`. It is self-damage, not a hit, and is left alone.
- **Orphaned `Airburst*` YAML** in nine weapon prefabs. It is harmless, and Unity drops it
  on the next save of each prefab.

### Subtraction ledger (estimate)

| Sub-cut | Removed | Added | Notes |
|---|---|---|---|
| 12.0 | 0 | ~40 src + ~30 test (CultLib) | release `cultmath-unity-v0.2.4`, manifest bump |
| 12.1 | ~15 (two frame copies, Aimed plumbing) | ~6 (`ToSchematic`) | no behaviour change |
| 12.2 | ~65 (HullKernel, WeightedPick, POnHull, live direction, bounding extent, HUD estimate, stale comments) | ~95 (TravelDirection, Silhouette, CommitProbability, lateral draw, Lane, 5 PendingShot/Outcome fields) | 6d kernel tests rewritten (~−150/+220) |
| 12.3 | ~80 (DamageSchematic, ApplyHit) | ~45 (Absorb, DamageHull, lane orchestration) | 2 FireAuthority tests replaced; ~6 new |
| 12.4 | ~45 (Splash, its half-hull footprint, the label read, BurstRadius plumbing) | ~60 (Detonate, circle-square overlap, point transforms, `WeaponFuse`, slot 30) | Splash tests rewritten; ~7 new. Areas need no ray loop, so this is smaller than the ray draft |
| **Net Aetheria src** | **~205** | **~205** | one persisted slot added (nullable), one renamed; no targets, dependencies or formats; CultMath gains 2 functions |

The net is roughly flat: the positive part buys the bearing-aware scatter, sequential
absorption, and a blast model that replaces a half-hull approximation. The rest is
subtraction.

**Section history:** Cut 6d's "The rule" (the 2D kernel) and its premise that pOnHull folds
into PBase at Fire, 9.2's SigmaFloor rationale, and Cut 4's Splash rule (`FireControl.cs:488-497`)
describe models this cut replaces. Self should mark them as history in the status header
as 12.2 and 12.4 land.
