# Locomotion: Control Allocation and Capability-Aware AI

Date: 2026-09-22

Status: target and cut map, from an Imagination pass. None of it has landed. Every anchor is against
`origin/codex/item-provenance` at `cd846916`; the working tree on `codex/fire-control-10` (`e8fbb6a6`) has an
identical tree for every file named below. The first half of this document is the **target** (the ends). The second
half is the **cut map** (the means). When the campaign closes, split the target out as
`docs/locomotion-target.md` and keep this file as history.

Claims marked **(probe)** come from running code: scratch builds of `Aetheria.Shared` from `cd846916` against
CultLib `45c2f40`, run in the session scratchpad. That covers catalog dumps, a legacy-msgpack decode, base-commit
flight measurements and a solver comparison. None of the probe sources are kept. Each result below says what was
run. The base suite passes 244/244 in that scratch copy.

## Rulings

Operator, 2026-09-22:

- **Problem statement.** "We're gonna need much smarter locomotion AI if they're to survive getting a thruster
  shot out. Longinus (non X) had one big thruster in the back and two attitude thrusters in the front. Losing an
  attitude thruster means you can't rotate that way anymore. Losing a thruster on the Djinn usually means some form
  of crab walking just became optimal. Naively trying to use a thruster that's been shot out makes them real easy
  to cheese."
- **R1, two layers.** (1) **Control allocation owns which thrusters fire.** Each live actuator contributes a
  body-frame wrench (Fx, Fy, τ) per unit throttle, with throttle in [0,1]. A bounded least-squares solve finds the
  throttles for the desired wrench (Johansen & Fossen 2013 control-allocation survey). This replaces the rotation
  buckets and the strafe torque compensation. Thrusters that are destroyed, unpowered, disabled or degraded drop
  out of the matrix or scale down in it. (2) **The AI plans with the allocator's achievable set.** For a desired
  Δv it scores candidate headings by achievable acceleration along Δv plus turn time in the directions the ship can
  still rotate. Crab walking and the long way round emerge from that scoring; neither is special-cased.
- **R2, the same-inputs invariant.** "Player and AI are controlling the same ship with the same inputs, this is an
  Aetheria invariant." The player's controls go through the same allocator, through the same input surface the AI
  writes. Any second path that bypasses it is a forbidden writer.
- **R3.** A capability-aware **combat facing/tactics** layer is wanted as its own later cut. Example: a one-way
  turner keeps its target drifting the way it can turn and leans on its turret arcs. This document maps it at
  target-shape level only.
- **R4.** If CultMath lacks a bounded least-squares solver, CultMath gets one, as a CultLib cut. Aetheria does not
  grow a local helper. CultMath has none (probe: `packages/cultmath/src/CultMath` at `45c2f40` holds vector types,
  `math`, `BatchMath`, `Voronoi` and `Random`, with no linear solver of any kind).

Self, relaying an operator answer, 2026-09-22. This is the layer split, stated precisely:

- **Below the inputs**, shared by player and AI, is the ship's own controller plus the allocator. It turns toward
  `LookDirection` and realises `MovementDirection` with whatever actuators are live. **Choosing a feasible turn
  direction belongs here**, including the long way round when one rotation direction is dead. If it lived in the
  AI planner, a player ship with a dead attitude thruster would still stick when looking toward its dead side.
  Holding heading against torque coupling (strafing with an off-centre thruster) also belongs here.
- **Above the inputs**, AI only, is the heading planner. It decides what to write into
  `LookDirection`/`MovementDirection`, for example to turn sideways and crab-walk. The player makes that choice by
  hand.
- The long-way test therefore belongs to the shared-controller cut and drives `LookDirection` directly, as the
  player would. The crab-walk test belongs to the planner cut.

---

# Part I: Target

## Objective

A ship that loses actuators keeps flying as well as its remaining actuators allow, whether a player or an AI is
flying it. The AI picks headings and thrust directions from what the ship can still do, so shooting out a thruster
creates a real handicap and not an exploit.

## Invariants

1. **Same inputs.** A ship is commanded only through `Ship.MovementDirection` (body frame, each component a
   fraction of the ship's capacity along that half-axis) and `Entity.LookDirection` (world-frame heading command;
   the controller reads only its `xz`). The player writes both (`Gameplay/ActionGameManager.cs:1254`, `:1263`). The
   AI writes the same two fields. Nothing else commands a ship's actuators. Identical inputs to identical ships give
   identical actuator throttles, whoever wrote them.
2. **One owner for actuator commands.** The ship controller (new, `ShipControl`, below) is the only writer of
   `Thruster` and `AetherDrive` throttles. `Ship.cs`'s mixer, its rotation buckets and its strafe compensation stop
   existing, and do not survive as a mode or a fallback.
3. **The allocator reads live actuator state every tick.** An actuator's column is derived each tick from its live
   stats and liveness: `EquippedItem.Active` (enabled, thermally online, durability online), a power grant above the
   same epsilon `Thruster.Execute` uses, and the current resolved `Thrust` (or the drive's own stats) with its
   durability, heat and power curves applied. No bucket, cache or event-pruned set decides whether an actuator
   counts. A repaired or re-powered actuator rejoins by the same read that dropped it.
4. **Manual and programmatic inputs share one commit path.** A player keypress, an AI state write, and any future
   scripted or network input all land in the same two fields, and they reach the actuators by the same
   `ShipControl` step inside `Ship.Update`. No input source writes `Thruster.Axis`, `AetherDrive.Axis` or
   `Entity.Direction` directly.
5. **Heading changes only through actuators.** `Entity.Direction` is written by actuator execution
   (`Thruster.Execute`, `AetherDrive.Execute`), by wormhole scripting (`Ship.ExitWormhole`), by spawn placement
   (`ActionGameManager.cs:848`) and by save unpack (`EntitySerializer.cs:66`). The controller does not write it.
   Today's snap-to-look lerp (`Ship.cs:280-284`) is a free, thrusterless rotation, and it goes.
6. **Kinematic rotation is retained.** Rotation stays kinematic: yaw rate is proportional to live throttle
   (`Thruster.cs:106-107`, `AetherDrive.cs:154-155`), with no angular momentum. Nothing found here argues for
   raising it. It keeps turn time exact (angle divided by rate), which the planner relies on. It also matches the
   operator's model ("losing an attitude thruster means you can't rotate that way anymore"): with no momentum,
   there is no coast.
7. **A dead actuator never receives throttle.** A destroyed, disabled or unpowered actuator gets zero throttle,
   not a saturated request it cannot honour.
8. **Capability is one model.** Turn time, turn direction, rotation capacity and translation capacity are computed
   in one place (`ShipControl`), from the same effectiveness matrix the allocator solves with. The AI queries them
   and never recomputes a private estimate.

## Canonical implementations

- **Solver:** CultMath (`F:\Projects\CultLib\packages\cultmath`), a box-constrained linear least-squares solver,
  added by Cut 1. It is the only numerical authority. Aetheria composes the problem and calls it.
- **Allocation reference:** T. A. Johansen and T. I. Fossen, "Control allocation: A survey," *Automatica* 49(5),
  2013. Its weighted-least-squares form with priorities is what `ShipControl` states. The active-set method follows
  O. Härkegård, "Efficient active set algorithms for solving constrained least squares problems in aircraft control
  allocation," CDC 2002, which is bounded-variable least squares in Stark and Parker's sense.

## Out of scope

- **Fire-control Cut 12** (edge-on hit placement in `FireControl.cs`, `Entity.ApplyHit`, `DamageSchematic`) is
  being mapped concurrently by another Imagination pass. Cuts 1 to 3 here touch none of those files. The allocator
  is a read-only downstream consumer of item destruction (`ItemDamage`, `Entity.cs:488`). **Overlap:** Cut 4 (combat
  facing) will rewrite `Agents/States/Combat.cs:88-127`, and presenting armour edge-on is exactly what Cut 12's hit
  placement rewards. Cut 4 must be mapped after Cut 12 lands.
- **The old AetheriaEve/CultMesh/daemon rebuild** is taxidermy. It is not precedent and not a consumer.
- Thruster geometry (the normalized moment-arm `Thruster.Torque`, `Thruster.cs:64-69`, which ignores lever length
  and uses unweighted shape cells for centre of mass) is the game's model and stays as authored. Arrival braking in
  `MoveToState`, docking approach and formation flight are also out.

## Not consumers

- `Assets/Scripts/Gameplay/*` presentation (`ShipInstance` particles, audio parameters) reads throttles and never
  writes them.
- `TurretController` writes `Entity.LookDirection` for turret entities. That is the AI above the inputs for a
  non-ship, and it does not drive ship actuators. See the audit.

## 0b: identity, lifecycle, authority

Nothing persistent changes. Actuator wrench columns, capacities, the chosen turn direction and planner state are
runtime-derived per tick, or per planner cadence, and are never saved.

| Kind | Named by | Lifecycle | Decides |
|---|---|---|---|
| Actuator column (runtime) | the `Thruster`/`AetherDrive` behaviour instance on an `EquippedItem` | rebuilt every tick from live stats; appears and disappears with liveness | `ShipControl` derives; item state (durability, heat, power, `Enabled`) is owned by `EquippedItem`/`PowerBus` as today |
| `MovementDirection`, `LookDirection` (runtime) | fields on `Ship`/`Entity` | overwritten each frame by whoever flies the ship | player (`ActionGameManager`) or AI (`Agent` layer), never both on one ship (`Zone.cs:111` gives agents only to `!IsPlayerShip`) |
| Committed turn direction, planner plan (runtime) | fields on `ShipControl` / `Agent` | reset on wormhole, dock, spawn | `ShipControl` / `Agent` |
| `ThrusterData`, `AetherDriveData`, `HullData.Hardpoints/Shape` (persisted, catalog) | catalog record key | authored | unchanged; read only |
| `GameplaySettings.TorqueFloor`, `TorqueMultiplier`, `AetherTorqueMultiplier` (authored, `Settings.cs:205-207`; values in `Assets/Resources/Settings.asset`: 0.5, 0.1, 0.1) | field name | authored | unchanged values. `TorqueFloor` changes role; see Cut 2 |

This campaign adds no authored field. If Hands finds it needs a tunable, that is an operator question, and any new
field on a persisted CultCache document must be nullable (MessagePack nil rule).

---

# Part II: Body map (what the probes established)

## The mixer today

`Ship.Update` (`Ship.cs:251-292`) runs before `base.Update` executes behaviours. The agents' writes come earlier
still, because `Zone.cs:170-173` updates agents before entities. The steps:

1. `RecalculateThrust` (`Ship.cs:147-247`) sums per-bucket thrust over thrusters whose item is `Active`.
2. It zeroes every thruster's `Axis` (`:256`).
3. Strafe: the right and left buckets get `±MovementDirection.x`, minus a torque compensation for the thrusters
   whose torque sign matches the bucket total (`:257-274`).
4. Forward and reverse buckets add `±MovementDirection.y` (`:275-276`).
5. Rotation: `deltaRot = sqrt(|dot(look, right)|)·sign` (`:278-285`) is added to the clockwise bucket and subtracted
   from the counter-clockwise bucket (`:287-288`).
6. `AetherDrive.Axis = (MovementDirection.y, MovementDirection.x, deltaRot)` (`:291`). There is no allocation for
   drives at all.

Buckets are built once in `Activate` from mount rotation and from `Thruster.Torque` against
`TorqueFloor` (`:102-118`). `RemoveThruster` prunes them permanently on `ItemDestroyed` (`:123`, `:133-143`).

`Thruster.Axis` saturates to [0,1] (`Thruster.cs:45-49`). `Execute` (`:94-115`) applies `Velocity -= facing·input·
Thrust/Mass·dt`, then rotates `Direction` kinematically by `input·Torque·Thrust·TorqueMultiplier/Mass·dt`. Its gate
is `input > .01 && PowerSupply > 1e-4`.

## Findings (probe)

Fixtures here are built from the legacy catalog (next section): hull shape, hardpoints, thruster items with
constant `Thrust` (Large Drive 250000, Medium Drive 200000; mass 100 each), `TorqueFloor` 0.5, `TorqueMultiplier`
0.1. Each ship sits at (37, −12) facing `normalize(0.8, 0.6)`, runs 3 warm-up ticks, is reset, and is measured over
one tick at dt 0.02. Body-frame acceleration is (r, f) in m/s². Heading rate is compass degrees per second,
clockwise positive.

**Torque factors** (`Thruster.Torque`, the sine of the moment arm): Longinus mains ±0.159 (below the floor, so
never used for rotation), attitude thrusters ±0.998. Djinni stern 0 / ±0.161, laterals ±0.727 (aft) and ±0.867
(fore), bow 0.

**Base flight table** (`cd846916`, after warm-up). These are the Cut 2 pins:

| Input | Longinus (r, f, °/s) | Djinni (r, f, °/s) |
|---|---|---|
| forward `(0,1)` | 2.37, 172.38, 0 | 0.11, 55.56, 0 |
| reverse `(0,−1)` | 0, 0, 0 | 0, −18.52, 0 |
| strafe right `(1,0)` | 0, 0, 0 (compensation zeroes the lone attitude thruster) | 34.03, 0.42, 0 (axes 1.00 / 0.84) |
| strafe left `(−1,0)` | 0, 0, 0 | −34.03, 0.42, 0 |
| look 90° right | 68.97, 0, +394.33 | 0.01, 0.59, +169.13 |
| look 90° left | −68.97, 0, −394.33 | 0.01, −0.50, −169.13 |
| look 30° right | 48.77, 0, +278.84 | 0, 0.30, +119.59 |
| look directly behind | 0, 0, 0, **and `|Direction|` falls to 0.96** | same |
| forward+strafe `(.707,.707)` | 1.18, 121.89, 0 | 24.12, 39.49, 0 |

The small cross terms (2.37, 0.42) are real. `Execute` rotates `Direction` thruster by thruster within one tick, so
later thrusters push along an already-rotated nose. The allocator cannot see this, so pins need a tolerance for it.

**Defects the table exposes:**

- **Dead-side stick.** On a Longinus with `Th.CCW` destroyed, looking 90° left gives a heading error of 90.0°
  after 30 s. It never turns. Looking right, it reaches within 2° in 0.32 s, the same as intact.
- **Main-thruster cheese.** On a Djinni with all three stern thrusters destroyed, `Agent` plus `MoveToState` toward
  a point 300 m to port leaves the ship at speed 0 after 120 s. `Agent.Accelerate` points at Δv and burns forward
  thrusters only (`Agent.cs:64-68`), and it never reaches the strafe branch. Intact, the same run arrives in
  4.34 s. With only Stern 2 lost it arrives in 4.98 s; with only Port Aft lost, 4.40 s.
- **Snap-to-look.** Inside `|sin error| < .01`, `Ship.cs:283` lerps `Direction` toward the look vector, which is a
  rotation no thruster pays for. When the look is directly behind, the lerp goes through the origin and shrinks
  `|Direction|`, and thruster force scales with that length (`Thruster.cs:105` uses the unnormalized item
  direction). Base also turns *slower* as error passes 90°: `deltaRot` follows `sin`, so it is zero at 180°.
- **First tick.** Items are not yet `Active` on the first `Update` after `Activate`, so the first-tick strafe
  compensation divides by a zero count (NaN, unused) and the strafe fires uncompensated.
- **`TurnTime` is wrong and unused.** `Ship.cs:61-66` divides angle by the sum of torque *factors* over mass, which
  is unitless nonsense: 261535 s for a Longinus 90° turn. It has no consumer.
- **Permanent pruning.** `RemoveThruster` never restores a thruster whose durability comes back, and `Recalculate*`
  reads `Active` while the mixer still commands inactive thrusters.
- **The live catalog's only ship flies on a drive.** `GameData/Aetheria.cc` holds one ship hull, **LonginusX**
  (mass 2500, drag 0.1, 6×17), with an `AetherDrive` hardpoint and **no `Thruster` hardpoint**. It moves on
  "Traction" (rotor mass (30,20,5), λ (0.5,1,1), MaxRpm 40000..80000). Two thruster designs exist ("deep space
  burnout", "Large Drive"), but no live ship can mount them (`docs/content-batch-one.md` §1 says the same).
  **AetherDrive is therefore the load-bearing actuator for the player today**, and the allocator must reproduce its
  base behaviour exactly.
- **Existing tests sit on an off-centre thruster.** `BrownoutTests`/`ConditionRatioTests` equip a one-cell thruster
  by `TryFindSpace` onto a 5×5 hull. It lands at (1,1) with `Torque` −0.707, and `MovementDirection (0,−1)` fires it
  while it spins the ship. Under an allocator that holds heading, a lone off-centre thruster cannot fire without
  rotating, so those fixtures must be re-seated (Cut 2).

## Legacy hull layouts (probe)

The live catalog is the post-breach minimal rebuild, so absence there proves nothing. The layouts come from the
git-LFS objects of `GameData/Legacy/AetherDB.2021-03-05.msgpack` (`142998fd…`) and `AetherDB.2021-04-14.msgpack`
(`21ce7bd2…`), both present in `.git/lfs/objects`, decoded with MessagePack `ConvertToJson`. Shape cells are
`bool[w,h]`, flattened as `x*h + y`, with y = 0 at the stern.

**Longinus** (2021-04-14: mass 2500, drag 0.1, 6×17; the 2021-03-05 copy has mass 5000). The shape matches the
live LonginusX's. Rows from y = 0: `.####.` ×4, `######` ×5, `.####.` ×2, `..##..` ×6.

| Hardpoint | Pos | Shape | Rotation |
|---|---|---|---|
| Th.L | (1,0) | 2×2 | Reversed (pushes forward) |
| Th.R | (3,0) | 2×2 | Reversed |
| Th.CW | (2,14) | 1×2 | CounterClockwise (pushes starboard, nose right) |
| Th.CCW | (3,14) | 1×2 | Clockwise (pushes port, nose left) |

**The data has two aft 2×2 thrusters, not one.** The operator remembers "one big thruster". Fixtures follow the
data (see Q3).

**Djinni** (mass 10000, drag 0.2, 14×17). Rows from y = 0: `....######....`, `...########...`,
`..##########..`, `.############.` ×2, `##############` ×5, `.############.`, `...########...` ×3,
`....######....`, `.....####.....`, `......##......`.

| Hardpoint | Pos | Shape | Rotation |
|---|---|---|---|
| Stern 1 | (6,0) | 2×1 | Reversed |
| Stern 2 | (5,1) | 2×1 | Reversed |
| Stern 3 | (7,1) | 2×1 | Reversed |
| Port Aft | (4,4) | 1×2 | CounterClockwise |
| Port Fore | (4,11) | 1×2 | CounterClockwise |
| Starboard Aft | (9,4) | 1×2 | Clockwise |
| Starboard Fore | (9,11) | 1×2 | Clockwise |
| Bow | (6,14) | 2×1 | None (pushes aft) |

Legacy thruster items used for the fixtures: Large Drive 2×2, 250000 thrust, mass 100. Medium Drive 2×1, thrust
100000..300000 (fixtures use a constant 200000), mass 100. Small Drive 1×1, 75000.

## Solver choice (probe)

The comparison ran on the fixtures' own normalized columns, plus a 16-column doubled Djinni: 2000 random wrench
demands each, cost measured against a 20000-iteration projected-gradient reference, double precision,
allocation-heavy scratch C#, Release build.

| Method | Worst excess cost | Suboptimal cases | Mean time |
|---|---|---|---|
| Primal active set (bounded-variable LS, Härkegård WLS form) | ≤ 7e-15 | 0 in every case | 1.8–9.5 µs (n 3–8), 32–38 µs (n 15–16); ≤ 30 iterations cold |
| Redistributed pseudo-inverse | up to 4e3 | 270–1564 of 2000 (n ≤ 8) | about half the active set's time |

**Pick the active set.** It is exact, its cold-start cost is bounded, and it warm-starts from last tick's solution.
The redistributed pseudo-inverse saves about half the time and is wrong in up to 78% of cases on these very
matrices. A production float implementation without allocations will be faster than the scratch numbers.

## Consumer audit

`cd846916`, every `*.cs` outside `Assets/Plugins`. "Dies" means deleted, not deprecated.

| Surface | Readers / writers | Fate |
|---|---|---|
| `Ship.MovementDirection` (`Ship.cs:27`) | W: `ActionGameManager.cs:1263` (player), `Agent.cs:67,73,77` (AI); tests `BrownoutTests.cs:143,402`, `ConditionRatioTests.cs:184,193,243,258`. R: `Ship.cs:261-291` | **Survives** as the translation input. Its meaning is defined by `ShipControl` (fraction of capacity per half-axis) |
| `Entity.LookDirection` (`Entity.cs:55`) | W: `ActionGameManager.cs:1254` (player), `Agent.cs:66`, `MoveTo.cs:26`, `Combat.cs:126`, `TurretController.cs:72,75` (turret entities; no `EntityTypeRestriction`, `TurretController.cs:14-15`). R (as heading): `Ship.cs:278`. R (as aim): `LockWeapon.cs:94`, `GuidedProjectileManager.cs:56`, `EntityInstance.cs:404`, `ShipInstance.cs:117` (tractor), `ZoneRenderer.cs:432`, `ActionGameManager.cs:376` (reticle target pick) | **Survives** as heading command *and* aim. `MoveTo.cs:26` dies in Cut 3 (a double writer: `Accelerate` overwrites it when Δv > 20). The heading/aim conflation is Cut 4's question (Q4) |
| `Thruster.Axis` (`Thruster.cs:45-49`) | W: `Ship.cs:256-288` only. R: `ShipInstance.cs:139` (particles), `Thruster.cs:88` (power request), `:96` (audio), `:102-111` | Getter **survives**. The public setter **dies**; only `ShipControl` commands throttle |
| `AetherDrive.Axis` (`AetherDrive.cs:81-85`) | W: `Ship.cs:291` only. R: drive internals, audio `:170` | Same as `Thruster.Axis` |
| `IAnalogBehavior` (`Behaviors.cs:96-99`) | implemented by `Thruster.cs:40`; consumer only a comment, `Entity.cs:2007` | **Dies** (Cut 2 deletes first) |
| Bucket sets `_forward/_reverse/_right/_left/_clockwise/_counterClockwiseThrusters`, `_thrusterItems`, `_aetherDrives`, `_aetherDriveItems` (`Ship.cs:30-39`) | `Ship.cs` only | **Die** |
| `ForwardThrust`, `ReverseThrust`, `LeftStrafeThrust`, `RightStrafeThrust`, `ClockwiseTorque`, `CounterClockwiseTorque`, `Left/RightStrafeTotalTorque`, `Left/RightStrafeTorqueThrusters` (`Ship.cs:50-59`) | written by `RecalculateThrust`, read by the mixer; **no reader outside `Ship.cs`** (grep, including Unity side and tests) | **Die** |
| `Ship.TurnTime` (`Ship.cs:61-66`) | no consumer | **Dies**; replaced by `ShipControl.TurnTime` (Cut 2), consumed in Cut 3 |
| `RemoveThruster`, `RemoveAetherDrive`, their `ItemDestroyed` subscriptions (`Ship.cs:123-143`) | internal | **Die**; liveness is read live (invariant 3) |
| `Agent.Accelerate` (`Agent.cs:58-79`), `FORWARD_DELTA_THRESHOLD`/`THRUST_DELTA_THRESHOLD` (`:16-17`) | called by `MoveTo.cs:27`, `Combat.cs:127` | **Rewritten** in Cut 3 (planner). Signature kept |
| `Agent.TopSpeed` (`Agent.cs:26`) | `MoveTo.cs:25`, `Combat.cs:127`. No live catalog item carries `VelocityLimit` or `VelocityConversion` (probe), so it is 100 everywhere | Survives |
| `GameplaySettings.TorqueFloor` (`Settings.cs:205`) | `Ship.cs:115,118` | **Survives with a new role**: it sizes rotation authority (Cut 2) and no longer classifies which thrusters fire |
| `TorqueMultiplier`, `AetherTorqueMultiplier` | `Thruster.cs:107`, `AetherDrive.cs:155` | Survive; also enter the yaw-rate row of each column |
| `Entity.Direction` writers | `Thruster.cs:106`, `AetherDrive.cs:154`, `Ship.cs:79` (wormhole), `Ship.cs:283` (snap), `ActionGameManager.cs:848` (spawn), `EntitySerializer.cs:66` | `Ship.cs:283` **dies** (invariant 5); the others survive |
| `Entity.Velocity` external writers | `HullCollider.cs:15` (collision impulse), `ShieldManager.cs:79` (bounce), `VelocityConversion.cs:41`, `VelocityLimit.cs:48` | Not control; these are external forces and constraints. Untouched |
| Undock capability check (`ActionGameManager.cs:913`) | `GetBehavior<Thruster/AetherDrive>() == null` | Survives. It could later read the capability model; not this campaign |
| Mining, Survey, HaulingTask, StationTowing (`Agents/Tasks/*.cs`) | data-only task classes; no movement code (grep) | Not consumers. `PatrolOrbitsState` drives `MoveToOrbitState`, which Cut 3 covers |
| Player input path | `Input.Player.Look` → `_entityYawPitch` → `LookDirection` (`:1250-1254`); `Input.Player.Move` → `MovementDirection` (`:1261-1264`). No other action writes motion (grep of `Input.Player.*`: targeting, heat, shield, stance, tractor, ping, UI) | **Already on the shared surface**. The only bypasses are the ones `Ship.cs` itself holds (the snap, direct `Axis` writes), and both die in Cut 2 |

Out-of-scope note: the working tree has an uncommitted change to `Assets/Scripts/AetheriaInput.cs` (a "Cycle Target
Item" action). That is fire-control work, and it does not touch motion.

---

# Part III: Cut map

## Status header

Status: cut map. Ends are owned by Part I above; this section owns the means.

Nothing has landed.

Open: Q1 and Q2 block Cut 2's pins. Q3 blocks only fixture naming. Q4 blocks only Cut 4.

Follow-ups outside this campaign:
- `TurretController` has no `EntityTypeRestriction`, so equipping it on a ship would create a third
  `LookDirection` writer racing the player or agent. Restrict it to non-ship hulls when turret work next opens
  (`TurretController.cs:14`).
- `Thruster.Execute` applies rotation per thruster inside the tick, which couples later thrusters' force to earlier
  thrusters' rotation. It is a small artefact, and it is `Thruster.cs`'s to fix, not the allocator's.
- `AetherDrive.Execute` logs "FUCK FUCK FUCK FUCK" on NaN velocity (`AetherDrive.cs:157-158`), a leftover probe.

## Cut 1. CultMath: bounded least squares (CultLib)

- **Repo/branch:** `F:\Projects\CultLib`, a branch from `45c2f40` (Aetheria's pinned `CultLibRevision`,
  `Directory.Build.props`). Depends on nothing.
- **First:** run `dotnet test packages/cultmath/tests/CultMath.Tests` green at base and record the count.
- **Deletes first:** none. This is a pure addition, and it is the ruled owner fill (R4).
- **Adds:** one C# file in `packages/cultmath/src/CultMath/` with a static solver for
  `min ||A x − b||²` subject to `lo ≤ x ≤ hi`. It serves a dense, small problem (m ≤ ~24 rows, n ≤ ~24 columns).
  Rules:
  - The method is a primal active set (bounded-variable LS). Each iteration solves the free set by Cholesky on the
    normal equations, or by QR if Hands prefers conditioning. It terminates on KKT: at each bound the gradient sign
    must be consistent with that bound.
  - `float` in, `Span`/`ReadOnlySpan` arguments, caller-supplied workspace. No allocation per call.
  - `x` is both warm start and result. The solver clamps it to the box first.
  - It returns an iteration count or status, has a bounded iteration cap, is deterministic, and keeps its
    last feasible iterate on a cap hit. A rank-deficient free set is handled (the caller adds a small ridge row,
    and the solver must not throw on a singular pivot).
  - Naming stays boring and HLSL-adjacent per `docs/design.md` Rules, for example a
    `CultMath.BoundedLeastSquares` static class. It is C#-only like the intercept helpers (`math.cs`). No Rust or
    HLSL mirror, because it is not a batch kernel and no shader needs it.
- **Tests** in `packages/cultmath/tests/CultMath.Tests`:
  - `InteriorOptimumEqualsNormalEquations`: a problem whose unconstrained optimum lies inside the box returns it.
    Pins the free-set solve.
  - `KktHoldsOnRandomBoxedProblems`: randomized problems up to 24×16 are checked against a projected-gradient
    reference to 1e-4 relative cost, and each active bound's gradient sign is checked. Pins optimality and the
    multiplier test.
  - `AllBoundsActive`, `ZeroColumn`, `DuplicateColumns`: degenerate columns return without throwing and are
    optimal. Pins rank handling.
  - `WarmStartMatchesColdStart`: a solution from a perturbed warm start equals the cold solution. Pins
    that the start does not bias the result.
  - `AsymmetricBounds`: `lo = −1, hi = 1` on some columns and `[0,1]` on others. Pins bidirectional actuators.
  - Fixtures must not be degenerate: non-square, non-identity A, and b not in A's range.
- **Release:** build the Unity package (`packages/cultmath/scripts/build-unity-package.ps1`), bump
  `unity/org.gamecult.cultmath/package.json` to 0.2.4, and tag `cultmath-unity-v0.2.4` by CultLib's own release
  path. In Aetheria, bump `Packages/manifest.json`'s `org.gamecult.cultmath` tag and `Directory.Build.props`
  `CultLibRevision` in one commit.
- **Authority map:** Owner: CultMath. Inputs: A, b, bounds, warm start. Outputs: x and status. Forbidden writers:
  no least-squares or linear-solve code in `Aetheria/Assets/Scripts/**` (negative grep in Cut 2).
- **Verification:** `dotnet build packages/cultmath/src/CultMath/CultMath.csproj`; `dotnet test` of CultMath.Tests.
  Aetheria's `dotnet test tests/Aetheria.Shared.Tests` still passes 244 after the bump. Unity compiles the new
  package, and only the operator can check that.
- **Ledger estimate:** +150..220 lines of source and +150 lines of test in CultLib; Aetheria gets a 2-line bump.

## Cut 2. `ShipControl`: shared controller and allocator replace the mixer

- **Repo/branch:** Aetheria, a branch from the Cut 1 bump commit. Depends on Cut 1.
- **First:** before editing, commit the base flight numbers above as test constants, reproduced by the fixture spec
  in "Findings". They are computed at `cd846916`, never captured from new code. Also run a LonginusX-with-Traction
  pin at base: `AetherDrive.Axis` for each probe input is `(my, mx, sqrt(|sin e|)·sign)`, including e = 30°, which
  gives `Axis.z = 0.7071`.
- **Deletes first** (separate commit, build and tests green, no behaviour change):
  - `Ship.cs:61-66` `TurnTime` (6 lines).
  - `Behaviors.cs:96-99` `IAnalogBehavior` (4 lines) and its mention in the `Thruster` declaration (`Thruster.cs:40`).
  - `Entity.cs:2007`, the commented `IAnalogBehavior` line.
- **Deletes (behaviour commit):**
  - `Ship.cs:30-39` (bucket and item sets, 10 lines).
  - `:50-59` (capacity properties and strafe lists, 10 lines).
  - `:96-118` (bucket construction; `Activate` keeps `base.Activate()` and gathers actuators, about 20 lines).
  - `:123-124` (subscriptions) and `:127-143` (`RemoveAetherDrive`, `RemoveThruster`, 17 lines).
  - `:145-249` (the `ThrustCalculation` region, 105 lines).
  - `:255-291` (mixer and snap, 37 lines).
  - The public `Axis` setters: `Thruster.cs:45-49` and `AetherDrive.cs:81-85` become getters plus a command method
    only `ShipControl` calls.
  - Total is about 200 lines out of `Ship.cs`.
- **Keeps:** `Ship.cs:253` gate (active, not in a wormhole); `:294-310` drag, position, gravity and rotation
  quaternion; wormhole code; `Thruster.Execute`/`AetherDrive.Execute` physics unchanged. The throttle is still
  `_input`/`_axis`, so power requests, audio and particles read what they read today.
- **Adds:** `ShipControl` in `Assets/Scripts/ServerShared/ShipControl.cs`, owned by `Ship` and stepped from
  `Ship.Update` where the mixer was. It is one file and one owner. Types and rules:
  - **Actuator columns.** A narrow interface implemented by `Thruster` and `AetherDrive`. There are two
    implementers and one consumer; this replaces `IAnalogBehavior`.
    - Each actuator reports its columns every tick: body-frame (Fx, Fy) in m/s² and yaw rate in rad/s
      (clockwise positive) per unit throttle, plus bounds. It then accepts its throttles back.
    - **Thruster:** one column, [0,1]. F is the negative item facing (`Rotate` of (0,1) by mount rotation) times
      live `Evaluate(Thrust)/Mass`. Yaw is `Torque·Thrust·TorqueMultiplier/Mass`.
    - **Zeroing:** the column is zero when `!Item.Active.Value` or `Item.PowerSupply ≤ 1e-4`, which is the same
      predicate `Execute` gates on. `Thruster.Thrust` (stale, updated only in `Execute`) is not the source.
    - **AetherDrive:** three axes. Translation is split into ± columns, [0,1] each, because the drive's efficiency
      depends on sign and speed (`AetherDrive.cs:138`). Torque is one column, [−1,1].
    - **Drive authority is nominal.** Effectiveness is the drive's force at `MaximumRpm` from its own stats (rotor
      mass, λ, λ multiplier, coupling efficiency, `AetherTorqueMultiplier`), scaled by the same liveness and by its
      condition. It is not the instantaneous `Rpm`. Rotor spin-up is the drive's internal dynamics, not an
      allocation decision. This choice is what keeps LonginusX exact: for a lone drive, self-consistent capacity
      cancels the scale, so throttle equals input.
  - **Heading controller** (below the inputs, shared):
    - `look = LookDirection.xz`. A zero look means hold heading: no NaN, no command.
    - Per rotation direction, rotation authority `ω_cap(dir)` is the sum of that direction's yaw-rate entries over
      columns whose `|Thruster.Torque| > TorqueFloor`. Drive torque columns always count. Under Q2 (A) that is the
      whole of rotation authority. **`TorqueFloor` no longer decides which thrusters fire.** It only sizes how
      much rotation may be demanded.
    - **Direction choice:** pick the direction that minimizes `angle_in_that_direction / ω_cap(dir)`, with
      infinity where `ω_cap = 0`. This yields the long way round. Keep the committed direction unless the other
      direction is faster by a margin, so symmetric ships do not dither near 180°.
    - **Rate demand:** `ω_d = min(profile(e)·ω_cap, e/dt)`, where `profile(e) = sqrt(sin e)` for e ≤ 90° (base's
      curve, which preserves the pins) and 1 beyond. The `e/dt` cap lands the heading without overshoot and
      replaces the snap.
    - It exposes `TurnTime(heading)` and `RotationCapacity(dir)` for Cut 3.
  - **Translation demand:**
    - `F_d = (mx·cap(±x), my·cap(±y))`, where `cap(d)` is the along-d component of the allocator's own solution to
      "a huge demand along d, rotation held at zero". Capacity is self-consistent: one objective, and no second
      rule for what full stick means.
    - It exposes `TranslationCapacity(bodyDir)` along any body direction, same definition, for Cut 3.
    - Probe values: Longinus cap(+y) 172.41, cap(+x) 13.74 (with side effects, so the demanded strafe comes out
      about 0.09 m/s²). Djinni cap(+x) 37.04, cap(+y) 55.56, cap(−y) 18.52.
  - **Allocation objective:** one bounded-LS call, priorities by weight separation (Johansen & Fossen §
    prioritization):
    - P1, yaw rate equals `ω_d`. Separation about 1e3.
    - P2, translation error `F − F_d` in plain L2 (isotropic), each row normalized by the ship's largest column
      magnitude.
    - P3, a small ridge on throttle (about 1e-3), for tie-breaking and conditioning.
    - Warm start from last tick's throttles.
    - Probe: a demand-frame weighting with perpendicular error weighted k = 2 gave no benefit over isotropic
      (k = 1). Keep it isotropic.
  - **Budget:** 1 allocation solve plus 4 capacity solves per ship per tick. At the scratch timings that is well
    under 100 µs per ship. Hands measures with a production build and reports. The fix for cost is solver
    efficiency, not a cache, because a cache would be a second truth for liveness (invariant 3).
- **Per-file changes** (against `cd846916`):
  - `Ship.cs:251-292`: `Update` calls `ShipControl.Step(dt)` inside the existing `:253` gate. The drag and position
    code below stays.
  - `Ship.cs:92-119`: `Activate` collects actuators into `ShipControl` (all `Thruster` and `AetherDrive`
    behaviours). No classification.
  - `Ship.cs:121-125`: the constructor drops both `ItemDestroyed` subscriptions.
  - `Thruster.cs:40`: implement the actuator interface, drop `IAnalogBehavior`. `:45-49`: public getter, internal
    command. `:61-71`: `Torque` stays as the geometry factor.
  - `AetherDrive.cs:81-85`: same pattern. Add the nominal-authority read next to its stats.
  - `tests/Aetheria.Shared.Tests/BrownoutTests.cs:111-131` and `ConditionRatioTests.cs:94-95`: equip the consumer
    thruster on the hull's centre column, using `TryEquip(item, int2(2, y))` with a free y, so `Torque == 0`. These
    tests pin brownout and condition, not flight. Their asserted values (for example `.1f` at
    `BrownoutTests.cs:162`) must not change. Fix the fixture, never the assertion. `PowerCurveTests.cs:52-62`'s
    comment about the `Axis` being clobbered becomes true for a different reason; update the wording only.
- **Authority map:**
  - Owner: `ShipControl` decides every actuator throttle and the feasible turn direction.
  - Inputs: `MovementDirection`, `LookDirection.xz`, current `Direction`, `Mass`, `dt`, each actuator's live
    column, `TorqueFloor`/`TorqueMultiplier`/`AetherTorqueMultiplier`.
  - Outputs: per-actuator throttle (`Thruster._input`, `AetherDrive._axis`); capability queries
    (`TurnTime`, `RotationCapacity`, `TranslationCapacity`).
  - Derived state: columns, capacities and `ω_d` are per-tick derived, never stored across ticks except the warm
    start and the committed turn direction, which are command state and not truth. `Thruster.Axis` and
    `AetherDrive.Axis` are display and command readbacks.
  - Forbidden writers: `Ship.Update`'s mixer (gone); any assignment to `Thruster.Axis`/`AetherDrive.Axis` outside
    `ShipControl`; `Ship.cs:283`'s direct `Direction` write; bucket pruning on `ItemDestroyed`; any Unity-side
    write of throttles.
  - Shared paths: player writes (`ActionGameManager.cs:1254,1263`) and AI writes (`Agent`, `MoveTo`, `Combat`) both
    reach actuators only through `ShipControl.Step`. Wormhole exit leaves the gate closed until the animation
    ends, as today.
  - Deletion line: the delete commits above land before `ShipControl` gains behaviour.
- **Verification:**
  - Builds: `dotnet build Aetheria.Shared`; `dotnet test tests/Aetheria.Shared.Tests -m:1 -p:CultLibRoot=…`.
    Unity batchmode compile (operator, or Self when the editor is closed).
  - Tests (new file `LocomotionControlTests.cs`, fixtures exactly as in "Findings": off-origin, off-axis nose with
    `|fx| > |fy|`, nonzero dt, asymmetric damage):
    - `IntactLonginusMatchesBase` and `IntactDjinniMatchesBase`: every row of the base table within tolerance
      (2% of that ship's full-scale acceleration or turn rate), **except the divergences Q1 names**, which are
      pinned at their probed new values once ruled. Pins the allocator against base.
    - `LonginusXDriveAxisEqualsBaseFormula`: a drive-only hull; for every probe input, `AetherDrive.Axis` equals
      `(my, mx, sqrt(|sin e|)·sign)` to 1e-3. Pins the live player ship's feel exactly.
    - `LonginusMissingCcwReachesHeadingOnDeadSideTheLongWay`: `Th.CCW` destroyed through the real `ItemDamage`
      path. Write `LookDirection` directly, as the player would, to 90° left. Heading changes are clockwise every
      tick (never CCW), and the ship reaches within 2° in under 1.0 s; the base-rate bound is 270°/394.33°/s =
      0.685 s. Base comparison: error still 90.0° after 30 s. Pins the shared feasible-direction choice.
    - `LonginusMissingCcwTurnsRightAsBefore`: looking 90° right reaches within 2° in 0.32 s ± one tick (base
      value). Pins that damage on one side leaves the other side alone.
    - `LookDirectlyBehindTurns`: intact, look exactly aft. The ship turns, `|Direction|` stays 1 ± 1e-4, and there
      is no NaN. Pins the snap's deletion and the >90° profile.
    - `ZeroLookHoldsHeading`: default `LookDirection` gives no NaN in `Direction` or any throttle.
    - `DestroyedThrusterNeverReceivesThrottle`: for each Djinni thruster in turn, destroy it, then drive 50
      ticks of mixed inputs (forward, strafe, turn both ways, diagonal). Its `Axis` is 0 on every tick. Repeat for
      `Enabled = false` and for a zero power grant. Pins invariant 7.
    - `RepairedThrusterRejoins`: destroy, restore durability, `UpdatePerformance`. The next tick's strafe uses it.
      Pins the live read (invariant 3) against the old permanent pruning.
    - `PlayerAndAiInputsGiveIdenticalThrottles`: two identical Djinni hulls with the same damage. One gets inputs
      the way `ActionGameManager` writes them (`LookDirection` with nonzero pitch from yaw/pitch Euler,
      `MovementDirection` from a stick value). The other gets them through an `Agent` state writing the same yaw,
      flat. After `Update`, every actuator throttle is identical. Pins R2.
    - `LonginusMissingMainHoldsHeadingUnderThrust`: `Th.L` destroyed, forward input. Heading drift stays under 1°
      over 2 s, and forward acceleration is at least 70 m/s² (probe: 78 with 12 lateral slip). Pins torque-coupling
      compensation in the shared layer.
  - Negative greps, verified against `cd846916` to hit only the lines this cut deletes:
    - `\.Axis\s*[-+]?=` in `Assets/Scripts` should match nothing outside `ShipControl.cs`. At base it matches
      exactly `Ship.cs:256,264,273,275,276,287,288,291`. `FieldDriver.cs`'s `RectTransform.Axis` does not match;
      checked.
    - `_forwardThrusters|_clockwiseThrusters|StrafeTorqueThrusters|RecalculateThrust|TurnTime\(` should match only
      `ShipControl.cs`'s new `TurnTime`.
    - `IAnalogBehavior` should match nothing.
    - `lerp\(Direction` in `Ship.cs` should match nothing.
    - No least-squares code outside CultMath: `Cholesky|pseudo.?inverse|ActiveSet` in `Assets/Scripts` should
      match nothing.
  - Stryker: `dotnet stryker --since:<Cut 2 base>` from `tests/Aetheria.Shared.Tests`. Every non-equivalent
    survivor in `ShipControl.cs`, `Thruster.cs` and `AetherDrive.cs` is killed or triaged by name. Float boundary
    flips are equivalent, per the fire-control Cut 11 ruling.
  - Operator: fly LonginusX and confirm it feels unchanged (strafe, turn, look behind now turns). Then fly a
    thruster ship, which needs a content hull with `Thruster` hardpoints; until one exists this check is
    fixture-only, so say so.
- **Operator questions:** Q1 and Q2, below.
- **Ledger estimate:** −200 lines in `Ship.cs`, −10 elsewhere. `ShipControl.cs` adds about 250–350 lines, tests
  about 400. Net source roughly +100. That buys actuator-loss capability, which is the explicit ask, and removes
  seven parallel role sets, the snap, and permanent pruning.

## Cut 3. Heading planner in the AI

- **Repo/branch:** Aetheria, after Cut 2.
- **First:** pin base AI results as constants. On base, a Djinni with all stern thrusters destroyed never reaches
  the target (speed 0 at 120 s). Intact, it reaches it in 4.34 s. Both come from the probe (Agent plus MoveTo, dt
  0.02, target 300 m to port from (37,−12), nose `normalize(0.8,0.6)`).
- **Deletes first:** `Agent.cs:16-17` (the two thresholds) and `Agent.cs:60-78` (the body of `Accelerate`, 19
  lines). `MoveTo.cs:26` (the duplicate `LookDirection` write). The signature `Accelerate(float2, bool noTurn)`
  stays, since `Combat.cs:127` and `MoveTo.cs:27` call it.
- **Adds:** in `Agent` (above the inputs, AI only):
  - Candidates: 16 evenly spaced headings, plus the Δv direction, plus the current heading.
  - Score for each: `TurnTime(θ) + |Δv| / TranslationCapacity(Δv expressed in θ's body frame)`, infinite where
    capacity is 0. Both terms come from `ShipControl`'s queries (invariant 8).
  - Pick the argmin with hysteresis: replan when a candidate beats the current plan by a margin, and on a cadence
    (for example every 0.25 s) or when the actuator set changes. The cadence exists for cost: 18 capacity solves
    per replan.
  - Write `LookDirection = θ*` unless `noTurn`. Write `MovementDirection` as the per-half-axis fractions that point
    the translation demand along Δv, inverting `ShipControl`'s own mapping (a `ShipControl` helper, so the AI never
    re-derives it), with a deadband near zero Δv.
  - `noTurn` (combat) skips heading choice but still gets capability-correct thrust direction. A Djinni with no
    stern facing its target strafes or backs off on its own.
- **Authority map:**
  - Owner: `Agent` decides what the AI writes to the two inputs.
  - Inputs: target velocity, `ShipControl` capability queries, current velocity and heading.
  - Outputs: `LookDirection` (unless `noTurn`) and `MovementDirection`.
  - Derived: the plan (heading, time of last replan) is command state only.
  - Forbidden writers: `MoveToState` writing `LookDirection`; any AI code writing throttles or `Direction`; any AI
    capacity estimate not read from `ShipControl`.
  - Shared paths: `MoveToOrbitState` (patrol) and `CombatState` both go through `Accelerate`.
- **Verification:**
  - `DjinniWithoutSternReachesTargetByCrabbing`: stern cluster destroyed, target 300 m at a bearing 30° right of
    the nose. Driven through `Agent` plus `MoveToState` subclass with a fixed target. The ship arrives within 10 m
    in under 8 s, and the planner's chosen heading puts a lateral pair along Δv. Base never arrives. Pins R1(2) and
    the stuck-forever cheese.
  - `CrabBeatsPointAndBurnWhenMainIsWeak`: Djinni with Stern 1 and Stern 2 destroyed (asymmetric; Stern 3's torque
    is balanced by the laterals), target 300 m at 60° left. The planner arrives strictly sooner than a naive
    point-and-burn agent run in the same harness through the same Cut 2 allocator. Pins that crab walking wins when
    it should, as a relation and not a captured number.
  - `IntactShipsStillPointAndBurn`: an intact Djinni picks a heading within 10° of Δv and arrives no later than
    base's 4.34 s + 10%. Pins no regression.
  - `LonginusWithoutReverseFlipsToBrake`: target velocity opposite the current velocity; the planner turns toward
    it (a Longinus has no reverse thrust).
  - `PlannerUsesShipControlCapacities`: after destroying a thruster, the planner's turn time for a dead-side
    heading equals `ShipControl.TurnTime` for that heading. Pins invariant 8 and the long-way-aware estimate.
  - Negative grep: `LookDirection` in `Agents/States/MoveTo.cs` should match nothing.
  - Stryker `--since:<Cut 3 base>` over `Agent.cs`, `MoveTo.cs` and `ShipControl.cs` queries.
  - Operator: watch a patrol Djinni with a stern thruster shot out crab to its next orbit.
- **Ledger estimate:** −22 lines, +120 lines of source, +250 lines of test.

## Cut 4. Combat facing and tactics (target shape only)

This cut gets mapped after fire-control Cut 12 lands, because that cut decides what presenting a side is worth.

- **Owner:** `CombatState` (`Combat.cs:88-127`), above the inputs.
- **Shape:** choose the orbit side and facing from `ShipControl` capabilities (per-direction rotation authority,
  translation capacity along candidate directions) and from weapon arcs. A one-way turner keeps its target drifting
  toward its live turn direction. A ship with good turret arcs and poor rotation holds a heading and lets the
  turrets work. Weapons with fixed arcs pull the heading onto the target. It uses the Cut 3 planner for thrust.
- **Fork:** Q4 (heading versus aim), which must be ruled before mapping.

## Operator questions

Each question gives options and a recommendation. Self paces these one at a time.

- **Q1: intact-ship divergence from base (blocks Cut 2 pins).** The probe of the stated objective found these
  differences on intact ships (new versus base):
  - Longinus turning in place also fires a main at 12%, which adds 10.7 m/s² of forward creep (about 6% of full
    forward) and cuts lateral slip from 69.0 to 67.3. The L2 objective trades a little slip for a little creep.
  - Longinus forward+turn at 30° keeps 164.8 of 172.4 forward (−4%).
  - Djinni strafe reaches 37.0 m/s² instead of 34.0 (+9%), by balancing torque with Stern 3 plus Bow.
  - Djinni turning bleeds Stern 2 plus Bow at 10% each (zero net force, visible exhaust).
  - Turns past 90° now run at full rate instead of slowing to zero at 180°.

  Everything else in the base table matches within 2%, including LonginusX's drive, which matches exactly.
  - **A.** Accept these as the allocator's honest optimum and pin base exactly where they coincide.
  - **B.** Require exact base behaviour. That means per-thruster role rules in the objective, which is the bucket
    machine this campaign deletes.

  **Recommended: A.** The only live ship (LonginusX) is exact under A. The divergences are small, physical, and
  arise only on thruster hulls, which the live catalog does not yet carry.
- **Q2: rotation from translation thrusters (blocks Cut 2).** When no thruster above `TorqueFloor` can turn a ship
  one way, may the controller turn it that way by differential translation thrust? For a Longinus that is its two
  mains: about 78°/s with a forward lurch.
  - **A.** No. Rotation authority comes only from thrusters above `TorqueFloor` and drive torque, so a lost attitude
    thruster means that direction is gone.
  - **B.** Yes, as a slower fallback.

  **Recommended: A**, because it matches the operator's words ("losing an attitude thruster means you can't rotate
  that way anymore") and keeps turning from making the ship lurch. Consequence of A: a Longinus with both attitude
  thrusters gone cannot turn at all.
- **Q3: Longinus fixture layout (fixture naming only).** Both legacy catalogs author two aft 2×2 thrusters (`Th.L`,
  `Th.R`), not the one big thruster the operator remembers.
  - **A.** Fixtures follow the data: two mains.
  - **B.** Model one centred main.

  **Recommended: A**, with a note that a lost main is itself a test case (`LonginusMissingMainHoldsHeadingUnderThrust`).
- **Q4: heading versus aim (blocks Cut 4 only).** `LookDirection` is both the heading command and the aim, read by
  `LockWeapon.cs:94`'s lock cone, guided projectiles, reticle targeting and the tractor beam. Capability-aware
  facing wants the AI to hold a heading that is not its aim, and under R2 any split applies to the player too.
  - **A.** Add a heading input beside the aim on the shared surface. The player's heading follows the view unless a
    player control is added later.
  - **B.** Keep one input, and the AI accepts degraded lock cones and missile aim while it manoeuvres.

  **Recommended: A**, decided when Cut 4 is mapped.

## Subtraction ledger (estimates)

| Cut | Removed | Added | Dependencies / targets |
|---|---|---|---|
| 1 | 0 | ~200 src + ~150 test (CultLib) | CultMath gains one file; cultmath-unity 0.2.4; Aetheria pin bump |
| 2 | ~210 (`Ship.cs` ~200, `Behaviors.cs` 4, `Entity.cs` 1, setters) | ~300 src + ~400 test | `IAnalogBehavior` removed; no new package, target or authored field |
| 3 | ~22 | ~120 src + ~250 test | none |
| 4 | not mapped | not mapped | not mapped |
