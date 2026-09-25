# Locomotion: Control Allocation and Capability-Aware AI

Date: 2026-09-22

Status: target and cut map, from an Imagination pass, revised twice on 2026-09-22: first after the operator
ruled Q1, Q2 and Q3 and asked for the legacy thruster hulls back, then after the operator approved the content
inventory, corrected the hull-behaviour guess and dissolved Q5 by replacing the controller's rotation demand with
a single full-wrench solve. None of it has landed. **The cuts are renumbered by that
revision**: the content restore is the new Cut 1, and what the first committed version called Cuts 1 to 4 are now
Cuts 2 to 5. Every anchor is against
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

Operator rulings on this map, 2026-09-22, relayed by Self:

- **Q1 = A, the divergence is accepted.** "The divergence is fine." A small divergence from base behaviour on
  intact thruster ships is accepted: the Longinus's forward creep of about 10.7 m/s² while turning in place, and
  the Djinn's roughly 9% faster strafe. Base is pinned exactly only where the old mixer and the allocator agree,
  and the accepted divergences are named with their measured magnitudes in Cut 3's verification, so Soul can tell
  an accepted divergence from a regression. **No per-thruster role rules are reintroduced to match base.**
- **Q2 = B, differential main thrust is allowed.** "That's absolutely viable and can be a better decision than
  turning around the long way, I wouldn't introduce a thruster exclusion for this." Any live actuator's torque may
  be spent on rotation, mains included. A damaged ship turning toward its dead side lurches forward, and the
  planner may prefer that to the long way round when it is faster.
- **Q3, fixtures follow the data.** "Good catch on the Longinus main thrusters, I was misremembering." Two aft 2×2
  mains, as both legacy catalogs author them.
- **Restore the legacy thruster hulls.** Asked whether the lost layouts could come back: "can we restore these
  layouts?" They become Cut 1, the campaign's first cut, ahead of the solver.

Second round, same day:

- **Cut 1 is approved as inventoried, optional gear included.** "Yes and yes": the Longinus and Djinni hulls,
  Medium Drive, and Small Drive, Victoire, Talaria and RevvITup 2.0 as well.
- **The second hull behaviour is not `VelocityConversion`.** "I don't think VelocityConversion was on either
  hull." The decode agrees: it is `VelocityLimitData`. See Cut 1.
- **Q5 is dissolved, not answered: the rotation demand is not sized at all.** "It should only do that if the
  ship's controller also *wants* to accelerate forwards that much, but I'm not sure how to encode that while
  allowing the lurch. Like, lurch burn to compensate for a missing attitude thruster is also only a good move when
  you both need to turn a bit and don't mind being all the way over there after. It doesn't help when, say,
  there's a guy to port and your aft thruster is out, because you'd just end up turning in a great big circle
  without changing your bearing at all." On the shape that replaced it: "Yeah, that's better."
  - **Cut 3** now runs one weighted least-squares solve over the whole desired wrench (Fx, Fy, yaw) at once,
    under throttle bounds. No separate rotation demand, no `TorqueFloor`-sized demand, no piecewise fall-back, and
    no branch anywhere on whether a ship can turn a given way. The weights are the design knob, and both error
    terms share a currency because each axis is normalized by what that ship can actually achieve on it.
  - **Cut 4** chooses manoeuvres by predicted outcome over a short horizon, simulating each candidate, never by a
    rule. The operator's case above is its test.

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
   raising it. It keeps turn time exact (angle divided by rate), which is what lets the planner compare a long
   way round against a slow lurching turn without simulating either.
7. **A dead actuator never receives throttle.** A destroyed, disabled or unpowered actuator gets zero throttle,
   not a saturated request it cannot honour.
8. **Capability is one model.** What a ship can do is computed in one place (`ShipControl`), from the same live
   column matrix the allocator solves with: the achievable extremes, the turn-direction comparison, and the solve
   itself. The AI reads those and predicts with the controller's own step; it never keeps a private estimate of
   what the ship can do, and never branches on which actuators are missing.

## Canonical implementations

- **Solver:** CultMath (`F:\Projects\CultLib\packages\cultmath`), a box-constrained linear least-squares solver,
  added by Cut 2. It is the only numerical authority. Aetheria composes the problem and calls it.
- **Allocation reference:** T. A. Johansen and T. I. Fossen, "Control allocation: A survey," *Automatica* 49(5),
  2013. Its weighted-least-squares form with priorities is what `ShipControl` states. The active-set method follows
  O. Härkegård, "Efficient active set algorithms for solving constrained least squares problems in aircraft control
  allocation," CDC 2002, which is bounded-variable least squares in Stark and Parker's sense.

## Out of scope

- **Fire-control Cut 12** (edge-on hit placement in `FireControl.cs`, `Entity.ApplyHit`, `DamageSchematic`) is
  being mapped concurrently by another Imagination pass. Cuts 2 to 4 here touch none of those files. The allocator
  is a read-only downstream consumer of item destruction (`ItemDamage`, `Entity.cs:488`). **Overlap:** Cut 5 (combat
  facing) will rewrite `Agents/States/Combat.cs:88-127`, and presenting armour edge-on is exactly what Cut 12's hit
  placement rewards. Cut 5 must be mapped after Cut 12 lands. Cut 1 restores catalog content, and the only file it
  and Cut 12 could both touch is `GameData/Aetheria.cc`; Cut 1 must not be in flight while another cut writes the
  catalog.
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
| `GameplaySettings.TorqueMultiplier`, `AetherTorqueMultiplier` (authored, `Settings.cs:206-207`; values in `Assets/Resources/Settings.asset`: 0.1, 0.1) | field name | authored | unchanged. `TorqueFloor` (`:205`, authored 0.5) loses its last consumer in Cut 3 and is deleted with its authored value |

This campaign adds no authored **field**. Cut 1 adds authored **records** (hulls, thruster designs and their
products) to `GameData/Aetheria.cc` using the schema as it stands. If Hands finds it needs a tunable, that is an
operator question, and any new field on a persisted CultCache document must be nullable (MessagePack nil rule).

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

**Base flight table** (`cd846916`, after warm-up). These are the Cut 3 pins:

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
  base behaviour exactly. Cut 1 restores the thruster hulls, which is what makes any play check on a thruster ship
  possible at all.
- **Existing tests sit on an off-centre thruster.** `BrownoutTests`/`ConditionRatioTests` equip a one-cell thruster
  by `TryFindSpace` onto a 5×5 hull. It lands at (1,1) with `Torque` −0.707, and `MovementDirection (0,−1)` fires it
  while it spins the ship. Under an allocator that holds heading, a lone off-centre thruster cannot fire without
  rotating, so those fixtures must be re-seated (Cut 3).

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

**The data has two aft 2×2 thrusters, not one.** The operator remembered "one big thruster" and ruled on it,
2026-09-22: "Good catch on the Longinus main thrusters, I was misremembering." Fixtures follow the data.

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
| `Entity.LookDirection` (`Entity.cs:55`) | W: `ActionGameManager.cs:1254` (player), `Agent.cs:66`, `MoveTo.cs:26`, `Combat.cs:126`, `TurretController.cs:72,75` (turret entities; no `EntityTypeRestriction`, `TurretController.cs:14-15`). R (as heading): `Ship.cs:278`. R (as aim): `LockWeapon.cs:94`, `GuidedProjectileManager.cs:56`, `EntityInstance.cs:404`, `ShipInstance.cs:117` (tractor), `ZoneRenderer.cs:432`, `ActionGameManager.cs:376` (reticle target pick) | **Survives** as heading command *and* aim. `MoveTo.cs:26` dies in Cut 4 (a double writer: `Accelerate` overwrites it when Δv > 20). The heading/aim conflation is Cut 5's question (Q4) |
| `Thruster.Axis` (`Thruster.cs:45-49`) | W: `Ship.cs:256-288` only. R: `ShipInstance.cs:139` (particles), `Thruster.cs:88` (power request), `:96` (audio), `:102-111` | Getter **survives**. The public setter **dies**; only `ShipControl` commands throttle |
| `AetherDrive.Axis` (`AetherDrive.cs:81-85`) | W: `Ship.cs:291` only. R: drive internals, audio `:170` | Same as `Thruster.Axis` |
| `IAnalogBehavior` (`Behaviors.cs:96-99`) | implemented by `Thruster.cs:40`; consumer only a comment, `Entity.cs:2007` | **Dies** (Cut 3 deletes first) |
| Bucket sets `_forward/_reverse/_right/_left/_clockwise/_counterClockwiseThrusters`, `_thrusterItems`, `_aetherDrives`, `_aetherDriveItems` (`Ship.cs:30-39`) | `Ship.cs` only | **Die** |
| `ForwardThrust`, `ReverseThrust`, `LeftStrafeThrust`, `RightStrafeThrust`, `ClockwiseTorque`, `CounterClockwiseTorque`, `Left/RightStrafeTotalTorque`, `Left/RightStrafeTorqueThrusters` (`Ship.cs:50-59`) | written by `RecalculateThrust`, read by the mixer; **no reader outside `Ship.cs`** (grep, including Unity side and tests) | **Die** |
| `Ship.TurnTime` (`Ship.cs:61-66`) | no consumer | **Dies**; replaced by `ShipControl.TurnTime` (Cut 3), consumed in Cut 4 |
| `RemoveThruster`, `RemoveAetherDrive`, their `ItemDestroyed` subscriptions (`Ship.cs:123-143`) | internal | **Die**; liveness is read live (invariant 3) |
| `Agent.Accelerate` (`Agent.cs:58-79`), `FORWARD_DELTA_THRESHOLD`/`THRUST_DELTA_THRESHOLD` (`:16-17`) | called by `MoveTo.cs:27`, `Combat.cs:127` | **Rewritten** in Cut 4 (planner). Signature kept |
| `Agent.TopSpeed` (`Agent.cs:26`) | `MoveTo.cs:25`, `Combat.cs:127`. No live catalog item carries `VelocityLimit` or `VelocityConversion` (probe), so it is 100 everywhere | Survives |
| `GameplaySettings.TorqueFloor` (`Settings.cs:205`) | `Ship.cs:115,118` only | **Dies** in Cut 3. The full-wrench solve has no threshold to apply it to, and nothing else reads it |
| `TorqueMultiplier`, `AetherTorqueMultiplier` | `Thruster.cs:107`, `AetherDrive.cs:155` | Survive; also enter the yaw-rate row of each column |
| `Entity.Direction` writers | `Thruster.cs:106`, `AetherDrive.cs:154`, `Ship.cs:79` (wormhole), `Ship.cs:283` (snap), `ActionGameManager.cs:848` (spawn), `EntitySerializer.cs:66` | `Ship.cs:283` **dies** (invariant 5); the others survive |
| `Entity.Velocity` external writers | `HullCollider.cs:15` (collision impulse), `ShieldManager.cs:79` (bounce), `VelocityConversion.cs:41`, `VelocityLimit.cs:48` | Not control; these are external forces and constraints. Untouched |
| Undock capability check (`ActionGameManager.cs:913`) | `GetBehavior<Thruster/AetherDrive>() == null` | Survives. It could later read the capability model; not this campaign |
| Mining, Survey, HaulingTask, StationTowing (`Agents/Tasks/*.cs`) | data-only task classes; no movement code (grep) | Not consumers. `PatrolOrbitsState` drives `MoveToOrbitState`, which Cut 4 covers |
| Player input path | `Input.Player.Look` → `_entityYawPitch` → `LookDirection` (`:1250-1254`); `Input.Player.Move` → `MovementDirection` (`:1261-1264`). No other action writes motion (grep of `Input.Player.*`: targeting, heat, shield, stance, tractor, ping, UI) | **Already on the shared surface**. The only bypasses are the ones `Ship.cs` itself holds (the snap, direct `Axis` writes), and both die in Cut 3 |

Out-of-scope note: the working tree has an uncommitted change to `Assets/Scripts/AetheriaInput.cs` (a "Cycle Target
Item" action). That is fire-control work, and it does not touch motion.

---

# Part III: Cut map

## Status header

Status: cut map. Ends are owned by Part I above; this section owns the means.

Nothing has landed.

Rulings (operator, 2026-09-22): **Q1 A** ("The divergence is fine"), **Q2 B** ("that's absolutely viable and can
be a better decision than turning around the long way, I wouldn't introduce a thruster exclusion for this"), **Q3**
fixtures follow the data ("Good catch on the Longinus main thrusters, I was misremembering"), and Cut 1 exists
because of "can we restore these layouts?". The Q2 text below is marked as history; the live design is in Cut 3.

Second-round rulings (operator, 2026-09-22): Cut 1 approved as inventoried, optional gear included ("Yes and
yes"); the second hull behaviour is not `VelocityConversion` ("I don't think VelocityConversion was on either
hull") and the decode names it `VelocityLimitData`; **Q5 is dissolved** by the full-wrench solve ("Yeah, that's
better"), so the Q5 text below and every "sized demand" design it belonged to are history.

Open: **Q4** (heading versus aim) blocks only Cut 5. **Q6** (the one weight in Cut 3's solve) does not block
Hands, who starts at the recommended default; the operator settles it by feel in the Cut 1 play check.

**Tutorial station (operator, 2026-09-25):** "What we're testing is going to be the tutorial level, right? Let's drop a station in there." The live catalog already has a station hull: Zenith, sold by Aeronautics Unlimited. The story-station path is dead because `StoryProcessor` has been commented out at `Galaxy.cs:242` since `ce1a0a46` (2021). Ruled: **faction station now**. The tutorial's entrance zone (`Galaxy.Entrance`) always gets a generated faction station with a docking bay. That is deterministic, not merely likely, and the test pins it on the real tutorial construction path. Reviving the Ink story stations belongs to a later narrative campaign, which must first find out why they were switched off.

Follow-ups outside this campaign:
- `TurretController` has no `EntityTypeRestriction`, so equipping it on a ship would create a third
  `LookDirection` writer racing the player or agent. Restrict it to non-ship hulls when turret work next opens
  (`TurretController.cs:14`).
- `Thruster.Execute` applies rotation per thruster inside the tick, which couples later thrusters' force to earlier
  thrusters' rotation. It is a small artefact, and it is `Thruster.cs`'s to fix, not the allocator's.
- `AetherDrive.Execute` logs "FUCK FUCK FUCK FUCK" on NaN velocity (`AetherDrive.cs:157-158`), a leftover probe.

## Cut 1. Restore the legacy thruster ship hulls to the live catalog

A pure content cut, separable from every code cut, so Soul can falsify it on its own. Operator, 2026-09-22: "can
we restore these layouts?", and on the inventory below, optional gear included: "Yes and yes". **The list is
approved**; what is left for review is anything Hands finds that this map did not name.

- **Repo/branch:** Aetheria, a branch from `codex/locomotion`. Depends on nothing. It blocks no code, but nothing
  in this campaign can be flown on a thruster ship until it lands, and Cuts 3 and 4 get their real fixtures from
  it. It writes `GameData/Aetheria.cc`, so it must not be in flight while another cut writes the catalog.
- **Standing rule** (memory: `aetheria-content-audits-pre-breach`): `GameData/Aetheria.cc` is the post-breach
  bare-minimum rebuild, so absence there is not evidence of absence, and **the operator's word is the authority**
  on what the pre-breach content was. The list below is therefore an **operator review item before Hands writes
  anything into the catalog**.

### What the legacy record actually holds (probe)

Sources, all decoded in the scratchpad: the git-LFS objects behind `GameData/Legacy/AetherDB.2021-04-14.msgpack`
(`21ce7bd2…`, 76 KB) and `AetherDB.2021-03-05.msgpack` (`142998fd…`, 59 KB), and the last
`GameData/AetherDB.msgpack` before the Cut 9 import (`8963a686…`, 46 KB, the commit's own parent at `70fbaca1^`).
All three are present in `.git/lfs/objects`; nothing had to be fetched.

The comparison explains the loss. The 2021-04-14 record carries 5 ship-class hulls and 45 gear designs. The import
source carries 3 hulls (LonginusX, Zenith, Turret) and 25 gear. **Longinus, Djinni and every thruster below the
2×2 class were already gone from the file Cut 9 imported**, which is why the live catalog has no thruster ship.

**Recoverable in full from 2021-04-14** (name, shape, mass, price, hardpoint list with position, shape, rotation
and armour, behaviours, temperature band, manufacturer, prefab and schematic paths):

| Record | Kind | Legacy detail | Notes |
|---|---|---|---|
| **Longinus** | ship hull | 6×17, mass 2500, drag 0.1, price 7,500,000, maker Alakrita | The shape is byte-identical to the live LonginusX's. 4 thruster hardpoints (`Th.L`/`Th.R` 2×2 aft, `Th.CW`/`Th.CCW` 1×2 in the nose) plus control module, 2 energy, 2 launcher, 2 radiator, reactor, sensors |
| **Djinni** | ship hull | 14×17, mass 10000, drag 0.2, price 10,000,000, maker Rossum & Douglas | 8 thruster hardpoints (3 stern, 4 lateral, 1 bow) plus 4 launcher, 2 ballistic, reactor, control module, 4 radiator, sensors, and a shield hardpoint |
| **Medium Drive** | thruster gear | 2×1, mass 100, thrust 100000..300000, price 50,000, no maker | **The one item both restored hulls need.** Fits the 2×1 stern/bow hardpoints and, rotated, the 1×2 lateral and nose ones |
| **Small Drive** | thruster gear | 1×1, mass 50, thrust 75000, price 25,000, no maker | Optional; the cheap end of the line |
| **Victoire** | thruster gear | 2×1, mass 25, thrust 200000..1500000, maker Alakrita | Optional; the racing thruster, and a natural Longinus fit |
| **Talaria** | thruster gear | 2×1, mass 150, thrust 75000..250000, maker Aeronautics Unlimited | Optional |
| **RevvITup 2.0** | thruster gear | 2×2, mass 150, thrust 250000..1000000, maker NiteLife Energy | Optional; a second 2×2 beside the two the catalog already ships |

Everything those records depend on is **already in the live catalog**: all 12 factions (Alakrita and Rossum &
Douglas included), both existing 2×2 thrusters, and the Unity presentation. The prefabs still carry the hardpoint
transforms by their legacy names — `Th.L`, `Th.R`, `Th.CW`, `Th.CCW` in `Assets/Content/Prefabs/Ships/
Longinus.prefab` (GUID `4a3db609…`, already the hull the live LonginusX points at) and `Thruster Stern 1..3`,
`Thruster Port/Starboard Fore/Aft` and `Thruster Bow` in `Djinni.prefab` (GUID `79024f63…`). The schematics exist
too (`schema_Longinus.png` `51702555…`, already referenced; `djinni_schematic_transparent.png` `66f3acdd…`, unused
today). So no art, no prefab work and no faction work is needed.

**Not recoverable, because it post-dates the breach and has to be authored:**

- `HardpointData.FiringArc` (Key 6) for the Djinni's weapon hardpoints. The legacy hardpoint record stops at
  armour. 0 means "use `GameplaySettings.FiringArc`"; the `firing-arc-migrate` command in `tools/AetherDb` is the
  precedent for deriving one from mount rotation.
- `ItemRole` lists on the restored designs, and `ProductRole` qualities on their products.
  `RoleAuthoringTests` refuses a stat naming a role its design lacks, and `docs/content-batch-one.md` fixes the
  nomenclature (`injector`/`nozzle` for thrusters, `plating` for hulls). The live "deep space burnout" carries
  `injector, nozzle`; "Large Drive" carries none.
- A `FactionProductData` per restored design. `LoadoutGenerator` picks products, not designs, so a design without
  one exists but is never generated or sold. The catalog holds 47 products today.
- Any `PowerSupply` term on the restored thrusters' stats (the stats-and-power cut's brownout curve). The shipped
  thrusters request zero energy, so matching them is the safe default and is a content decision.
- Provenance is runtime, not authored: `ItemManager` mints the `Lot` (`docs/item-provenance-target.md`).

**The second hull behaviour, identified** (probe; the operator was right that it is not `VelocityConversion`).
Both legacy hulls carry a behaviour LonginusX does not, union tag 11 in the legacy record. The union table is in
`Assets/Scripts/ServerShared/Behaviors/Behaviors.cs` at the legacy-project commit `7006a6b0` and unchanged at
`b4c06b50`: **tag 11 is `VelocityLimitData`**; `VelocityConversionData` is tag 10, and it is on neither hull. The
table cross-checks against the live catalog on every record that survived: tag 6 `ReflectorData` (live LonginusX
has exactly that and nothing else), 8 `ThrusterData` and 9 `WearData` ("deep space burnout" carries both), 3
`RadiatorData` (Arctica), 7 `ShieldData` (legacy "Shield"), 31 `CapacitorData` (legacy "Capacitor").

The payload is a single `PerformanceStat` in `Key(1)`, `VelocityLimitData.TopSpeed`, authored `50..50` with the
legacy exponents `1, 0, 1.5` on both hulls. So **both restored hulls cap at 50 units of speed**, where a hull
without the behaviour leaves `Agent.TopSpeed` at its 100 default (`Agent.cs:26`) and leaves the player uncapped.
`VelocityLimit.Execute` clamps `Entity.Velocity` (`VelocityLimit.cs:48`), so this is a real handling difference on
the restored hulls and Cut 4's arrival times are measured with it.

**The legacy stat shape differs from today's.** A legacy `PerformanceStat` is
`[Min, Max, HeatExponentMultiplier, DurabilityExponentMultiplier, QualityExponent]`; today it is `Min`, `Max` and
a `Terms` list of `StatTerm` (`ItemData.cs:632-643`, keys 2 to 4 retired). Every restored stat has to be
translated, not copied. `docs/stats-power-cut1-migration.md` is the precedent for that mapping and the authority
on what each legacy exponent becomes.

### The cut

- **Deletes first:** nothing. This is content restoration.
- **Adds:** the two hull records and all five thruster designs (`Medium Drive`, `Small Drive`, `Victoire`,
  `Talaria`, `RevvITup 2.0`), approved 2026-09-22; a `FactionProductData` for each; roles for each; firing arcs on
  the Djinni's weapon hardpoints; each hull's `VelocityLimit` behaviour with its translated stat.
- **Mechanism:** a one-shot command in `tools/AetherDb` (`restore-hulls [apply]`), in the shape of the existing
  `*-migrate [apply]` commands: dry run by default, opens the catalog through `AetheriaStores.Open(catalogWritable:
  true)` only when applying, writes every record in one `Commit`, and refuses to run twice. The Cut 9 importer
  (`tools/AetherDb/Import.cs`, added in `70fbaca1` and deleted by Cut 10) is the precedent for reading the legacy
  `[tag, payload]` records and rewriting them by the target type's `[Key]` shape; read it out of git history rather
  than re-deriving the mapping. **Delete the command in the same campaign, as Cut 10 deleted the importer**, so the
  carrying cost of the restore is zero once it has run.
- **Authority map:**
  - Owner: the catalog (`GameData/Aetheria.cc`) owns authored content; the operator owns what the content *is*.
  - Inputs: the legacy LFS records, the existing factions and assets, the operator's review of the list above.
  - Outputs: new `HullData`, `GearData` and `FactionProductData` records.
  - Derived state: none. Runtime `Lot`s and provenance are minted as for any other design.
  - Forbidden writers: nothing else may write the catalog while this cut is in flight; the one-shot command must
    refuse a second apply rather than upsert twice.
  - Deletion line: the command goes once the records are in.
- **Verification:**
  - `FireControlCut7Tests.ShippedCatalogOpensAndGeneratesAnArmedHull` still passes unchanged: the real catalog
    opens, every `EquippableItemData` deserializes, and LonginusX still generates an armed loadout.
  - New `RestoredHullsTests` against the real catalog, read-only: each restored hull exists, has the expected
    thruster hardpoint count (Longinus 4, Djinni 8), every thruster hardpoint accepts a catalog thruster design
    through `TryEquip` at that hardpoint's own position, and the ship then moves and turns under
    `MovementDirection`/`LookDirection` for a few ticks. Before Cut 3 this runs on the old mixer, which is the
    point: it proves the content, not the controller.
  - `LoadoutGenerator` generates each restored hull when filtered to it, which is the check that the products and
    roles are authored correctly.
  - `dotnet run --project tools/AetherDb -- census` names the two hulls with their makers, and `dangling` reports
    no new dangling reference.
  - Operator: the Studio click-through, then a play check flying a restored hull. That play check is what
    unblocks the rest of this campaign, and it is where Q6's weight gets settled by feel.
- **Operator questions:** none open. The list is approved and tag 11 is identified.
- **Ledger estimate:** no source removed. About 3 to 7 catalog records added, plus their products, plus a one-shot
  command of roughly 150 lines that is deleted again in this campaign. Net carrying cost: the content, and nothing
  else.

## Cut 2. CultMath: bounded least squares (CultLib)

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
  no least-squares or linear-solve code in `Aetheria/Assets/Scripts/**` (negative grep in Cut 3).
- **Verification:** `dotnet build packages/cultmath/src/CultMath/CultMath.csproj`; `dotnet test` of CultMath.Tests.
  Aetheria's `dotnet test tests/Aetheria.Shared.Tests` still passes 244 after the bump. Unity compiles the new
  package, and only the operator can check that.
- **Ledger estimate:** +150..220 lines of source and +150 lines of test in CultLib; Aetheria gets a 2-line bump.

## Cut 3. `ShipControl`: shared controller and allocator replace the mixer

- **Repo/branch:** Aetheria, a branch from the Cut 2 bump commit. Depends on Cut 2. Cut 1 is not a
  prerequisite for the code, but its restored hulls are what the operator's play check needs.
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
  - **One solve over the whole wrench.** There is no rotation stage and no translation stage. Each tick
    `ShipControl` builds a desired wrench `v = (Fx_d, Fy_d, ω_d)` and solves, once,

        min over u in [lo, hi] of  Σ_axis ( w_axis · (B_axis · u − v_axis) / cap_axis )² + ridge · |u|²

    where `B` is the live column matrix, `cap_axis` is that axis's achievable extreme, and `ridge` is about 1e-3.
    The normalization is what lets a rotation error and a translation error be compared: each residual reads as a
    fraction of what this ship can do on that axis.
  - **Achievable extremes.** `cap` for an axis is the largest value that axis can take inside the throttle box,
    which for a box constraint is just the sum of the positive entries of that row (the negative ones for the
    other half-axis). Six numbers, no solve: `+x`, `−x`, `+y`, `−y`, clockwise, counter-clockwise. They are also
    what `MovementDirection` is a fraction of. Probe values: Longinus `+y` 172.41, `±x` 68.97, yaw 8.25 rad/s
    (472.9°/s); Djinni `+y` 55.56, `−y` 18.52, `±x` 37.04, yaw 3.25 rad/s (186.2°/s).
  - **Demand.** `F_d = (mx · cap(±x), my · cap(±y))` from `MovementDirection`. `ω_d = min(profile(e) · cap(dir),
    e / dt)` toward the `LookDirection` heading, `profile(e) = sqrt(sin e)` for e ≤ 90° (base's own curve) and 1
    beyond, with the `e/dt` term landing the heading without overshoot in place of the deleted snap. A zero
    `LookDirection` means hold heading: no demand, no NaN.
  - **Direction choice** stays a time comparison, `angle_in_that_direction / cap(dir)`, infinite where `cap` is
    zero, with the committed direction held unless the other is faster by a margin. It is not a branch on whether
    the ship "can" turn: under Q2 = B `cap(dir)` is almost always non-zero, and the comparison is what makes the
    long way round appear.
  - **Weights.** Translation rows weigh 1. The yaw row weighs `w`, the single knob (Q6), default **3**. There is
    no other tuning constant in the objective.
  - **The three regimes fall out of that solve; none of them is a rule** (closed-loop probe, below):
    - (a) *Turning while forward thrust is also wanted.* The mains are already firing for `F_d`, so their torque
      costs nothing extra, and the ship turns with them.
    - (b) *Turning in place.* `F_d` is zero, so every newton of main thrust is pure error, and the solver spends it
      only to the extent the remaining yaw residual is worth more. The intact Longinus is no longer forced into
      the 84 m/s² burn the earlier sized-demand design produced.
    - (c) *No attitude authority in that direction.* The only way to cut the yaw residual at all is the mains, so
      the solver takes the lurch, and the direction comparison decides whether that beats the long way round.
  - **`TorqueFloor` has no consumer left.** It was read only at `Ship.cs:115,118`. The field goes from
    `GameplaySettings` (`Settings.cs:205`) along with its authored value in `Assets/Resources/Settings.asset`;
    `TorqueMultiplier` and `AetherTorqueMultiplier` stay, because they are physics.
  - **Budget.** One solve per ship per tick, plus six sums. The earlier design's four extra capacity solves are
    gone. The fix for cost is solver efficiency, not a cache, because a cache would be a second truth for liveness
    (invariant 3).

### Closed-loop probe behind Cut 3

The single-tick table in Part II is measured against the real engine at `cd846916`. The numbers in this section
come from a **separate scratch model**: the fixture columns above, kinematic rotation, `dt` 0.02, the objective as
stated, and the base mixer re-implemented against the same integrator so the two are compared like for like. They
are design evidence and test targets, not engine pins; Hands reproduces them against the real engine and reports
the differences.

| Manoeuvre (Longinus fixture) | base mixer | solve at w = 3 |
|---|---|---|
| Turn 90° in place | 0.32 s, 13.9 m/s of drift | 0.30 s, 16.9 m/s of drift, main peaks at 0.73 throttle |
| Turn 90° at full forward | 0.32 s | 0.30 s |
| Strafe right, 1 s, look held | no strafe at all | 10.7 m/s gained, heading holds within 1.1° |
| Djinni turn 90° in place | 0.68 s, no drift | 0.68 s, no drift, no stern throttle |

The drift in a turn is dominated by the nose thruster, which pushes sideways whatever the controller does: base
pays 13.9 m/s for a 90° turn and the solve pays 16.9 m/s, in about the same time. That is the honest shape of
regime (b): the main does contribute torque, but the turn does not become a forward burn.

The yaw weight trades heading-holding against how hard the mains are spent, and on this hull both come from the
same pair of actuators:

| w | strafe gained in 1 s | heading deviation while strafing | 90° turn in place | main peak |
|---|---|---|---|---|
| 0.5 | 0.1 m/s | 179° (heading lost) | 1.62 s | 0.14 |
| 1 | 46.4 m/s | 59° | 0.60 s | 0.37 |
| 2 | 13.7 m/s | 3.8° | 0.36 s | 0.63 |
| **3** | **10.7 m/s** | **1.1°** | **0.30 s** | **0.73** |
| 5 | 9.9 m/s | 0.4° | 0.28 s | 0.79 |

Damaged cases at w = 3: a Longinus with `Th.CCW` destroyed reaches a heading 90° to port **the long way round**
(0.74 s, sweeping +269°) and a heading 20° to port **the short way, on differential main thrust** (1.18 s, sweeping
−18°, gaining 19.7 m/s). A hull whose single main sits on the centre line has no counter-clockwise authority at
all and always takes the long way (0.86 s). A Longinus with both nose thrusters gone still turns 20° in 0.60 s on
its mains alone, and does it while gaining 83 m/s if forward thrust was also asked for against 20 m/s if it was
not — regime (a) against regime (b) on one fixture.

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
    column, `TorqueMultiplier`/`AetherTorqueMultiplier`.
  - Outputs: per-actuator throttle (`Thruster._input`, `AetherDrive._axis`); the achievable extremes, the
    movement-command mapping, `TurnTime(heading)`, and the step itself, which Cut 4 uses as its prediction model.
  - Derived state: columns, extremes and the demand are per-tick derived, never stored across ticks except the
    warm start and the committed turn direction, which are command state and not truth. `Thruster.Axis` and
    `AetherDrive.Axis` are display and command readbacks.
  - Forbidden writers: `Ship.Update`'s mixer (gone); any assignment to `Thruster.Axis`/`AetherDrive.Axis` outside
    `ShipControl`; `Ship.cs:283`'s direct `Direction` write; bucket pruning on `ItemDestroyed`; any Unity-side
    write of throttles. Also forbidden inside `ShipControl`: any test of whether an actuator is "for" rotation or
    translation, any per-direction special case, and any second solve that re-decides part of the wrench.
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
      (2% of that ship's full-scale acceleration or turn rate), except the four accepted divergences below, which
      are pinned at their own probed values. Pins the allocator against base.
    - **Accepted divergences** (operator, 2026-09-22, "The divergence is fine"). Soul reads these as pinned
      behaviour, not as regressions, and anything outside them is a regression:
      1. Longinus turning in place spends its mains for torque, peaking near 0.73 throttle at w = 3. The
         manoeuvre costs 16.9 m/s of drift against base's 13.9 and finishes in 0.30 s against base's 0.32.
      2. Longinus strafing gains a weak strafe it never had: about 10.7 m/s in a held second, heading holding
         within about 1°, where base produced exactly nothing.
      3. Djinni strafe reaches 37.0 m/s² against base's 34.0 (+9%), by balancing torque with Stern 3 and Bow.
      4. Djinni turning in place may run a stern thruster and the bow thruster against each other at low throttle;
         net force stays zero, but their exhaust is visible.
      Plus two intended fixes, which are not divergences to tolerate but behaviour this cut is buying: turns past
      90° run at full rate instead of decaying to zero at 180°, and `|Direction|` no longer shrinks when the look
      is directly aft.
    - `LonginusXDriveAxisEqualsBaseFormula`: a drive-only hull; for every probe input, `AetherDrive.Axis` equals
      `(my, mx, sqrt(|sin e|)·sign)` to 1e-3. Pins the live player ship's feel exactly.
    - `CentreMainHullTakesTheLongWayRound`: the long-way fixture, built so the long way is the *only* way. One
      centred main (torque 0, so no differential authority) and one clockwise attitude thruster, the
      counter-clockwise one destroyed through the real `ItemDamage` path. Write `LookDirection` directly, as the
      player would, to 90° left. Heading changes clockwise every tick (never counter-clockwise), and the ship
      arrives within 2° in under 1.2 s (model: 0.86 s). Base comparison: a ship of that shape never turns at all.
      Pins the shared feasible-direction choice.
    - `LonginusMissingCcwPrefersTheLongWayAtNinetyDegrees`: the two-main Longinus with `Th.CCW` destroyed, looking
      90° left. It sweeps clockwise through about +270° and arrives (model: 0.74 s). Pins that the comparison is by
      time, not by a rule about which actuators are "for" turning.
    - `LonginusMissingCcwLurchesRoundTheShortWayAtTwentyDegrees`: the same ship looking 20° left. It sweeps
      counter-clockwise through about −20° on differential main thrust, arrives within 2°, and its speed rises
      while it does (model: 1.18 s, 19.7 m/s). Pins that the lurching turn is available and is chosen when it is
      faster.
    - `IntactShipTurningInPlaceDoesNotBurnForward`: the intact Djinni, looking 90° right with
      `MovementDirection` zero. Its speed at the end of the turn is under 1 m/s and no stern or bow thruster
      exceeds a low throttle. Pins regime (b) on a hull that has the actuators to keep translation clean. The
      intact Longinus, which does not, is covered by accepted divergence 1 instead, with its own bound: drift no
      worse than 1.3× base's 13.9 m/s and turn time no worse than base's 0.32 s.
    - `NoAttitudeAuthorityStillTurns`: a Longinus with both nose thrusters destroyed, looking 20° right. It
      arrives, on differential main thrust, and it gains speed doing so (model: 0.60 s). Pins regime (c).
    - `TurningIsCheaperWhenForwardThrustIsAlsoWanted`: the same mains-only ship, 20° right, run twice with
      `MovementDirection` zero and at full forward. Both arrive in the same time; the second gains much more
      speed because the burn was wanted anyway (model: 20 m/s against 83 m/s). Pins regime (a) as a consequence of
      one solve rather than a special case.
    - `StrafeHoldsHeading`: the intact Longinus, full strafe held for 1 s with `LookDirection` fixed. Heading
      deviation stays under 2° (model at w = 3: 1.1°). Pins the weight's other half, which is what stops the nose
      swinging when a ship strafes with a thruster that also yaws.
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
    - `TorqueFloor` should match nothing in `Assets/Scripts`, and nothing in `Assets/Resources/Settings.asset`
      once Unity rewrites it.
    - `IAnalogBehavior` should match nothing.
    - `lerp\(Direction` in `Ship.cs` should match nothing.
    - No least-squares code outside CultMath: `Cholesky|pseudo.?inverse|ActiveSet` in `Assets/Scripts` should
      match nothing.
  - Stryker: `dotnet stryker --since:<Cut 3 base>` from `tests/Aetheria.Shared.Tests`. Every non-equivalent
    survivor in `ShipControl.cs`, `Thruster.cs` and `AetherDrive.cs` is killed or triaged by name. Float boundary
    flips are equivalent, per the fire-control Cut 11 ruling.
  - Operator: fly LonginusX and confirm it feels unchanged (strafe, turn, look behind now turns). Then fly a
    restored Longinus or Djinni from Cut 1, shoot a thruster off it, and confirm the damaged handling and the
    lurching turn. Without Cut 1 this half of the check cannot be done at all.
- **Operator questions:** Q6, below, which does not block the cut. Q1, Q2, Q3 are ruled and Q5 is dissolved.
- **Ledger estimate:** −200 lines in `Ship.cs`, −12 elsewhere (`IAnalogBehavior`, `TorqueFloor` and its authored
  value). `ShipControl.cs` adds about 250–350 lines, tests about 500. Net source roughly +100. That buys
  actuator-loss capability, which is the explicit ask, and removes seven parallel role sets, the snap, permanent
  pruning, one authored setting and four capacity solves a tick.

## Cut 4. Heading planner in the AI

- **Repo/branch:** Aetheria, after Cut 3.
- **First:** pin base AI results as constants. On base, a Djinni with all stern thrusters destroyed never reaches
  the target (speed 0 at 120 s). Intact, it reaches it in 4.34 s. Both come from the probe (Agent plus MoveTo, dt
  0.02, target 300 m to port from (37,−12), nose `normalize(0.8,0.6)`).
- **Deletes first:** `Agent.cs:16-17` (the two thresholds) and `Agent.cs:60-78` (the body of `Accelerate`, 19
  lines). `MoveTo.cs:26` (the duplicate `LookDirection` write). The signature `Accelerate(float2, bool noTurn)`
  stays, since `Combat.cs:127` and `MoveTo.cs:27` call it.
- **Adds:** in `Agent` (above the inputs, AI only). **The planner chooses by predicted outcome, never by a
  rule.** It simulates each candidate and compares where the ship actually ends up.
  - **Candidate manoeuvre:** a pair (heading command, movement command) held for the horizon. The set is the
    headings the ship might fly with the translation that points the demand along Δv at each: 16 evenly spaced
    headings (Hands default; 8 to 32 is the sensible range), plus the Δv direction and the current heading.
  - **Prediction:** roll each candidate forward over a horizon of 1.0 s (Hands default; 0.5 to 2.0 s is the
    sensible range) in 5 substeps (3 to 10), stepping the *same* `ShipControl` solve and the same kinematics the
    ship will actually fly. There is no second physics model and no closed-form estimate: one owner for what the
    ship does, which is invariant 8 applied to prediction.
  - **Score:** the predicted outcome at the horizon — remaining distance to the objective, and the closing speed
    that ends the horizon (so a manoeuvre that arrives pointing the wrong way with no closing speed loses to one
    that is simply nearer and still closing). Infinite where the candidate ends further away than it started.
  - **This is what answers the operator's case.** A ship whose rotation has to be bought with main thrust turns in
    a great circle: the prediction shows the heading improving while the range opens, so a lateral burn that keeps
    the bearing and closes wins on its own numbers. Nothing in the planner tests whether the ship is a one-way
    turner, whether a thruster is missing, or which actuator would be used.
  - Pick the argmin with hysteresis: replan when a candidate beats the current plan by a margin, on a cadence
    (0.25 s default) or when the actuator set changes. The cadence exists for cost: 18 candidates × 5 substeps is
    about 90 solves per replan, roughly 1 ms per replan per ship at the scratch timings, so a 0.25 s cadence over
    20 AI ships is a few percent of a core. Hands measures and reports; if it is too slow the answer is fewer
    candidates or a shorter horizon, both of which are stated ranges, not a cache.
  - Write `LookDirection = θ*` unless `noTurn`. Write `MovementDirection` as the per-half-axis fractions that point
    the translation demand along Δv, inverting `ShipControl`'s own mapping (a `ShipControl` helper, so the AI never
    re-derives it), with a deadband near zero Δv.
  - `noTurn` (combat) skips the heading half of the candidate but still predicts the translation. A Djinni with no
    stern facing its target strafes or backs off on its own.
- **Authority map:**
  - Owner: `Agent` decides what the AI writes to the two inputs.
  - Inputs: target velocity and objective position, the `ShipControl` step used as the prediction model, current
    velocity and heading.
  - Outputs: `LookDirection` (unless `noTurn`) and `MovementDirection`.
  - Derived: the plan (heading, time of last replan) is command state only.
  - Forbidden writers: `MoveToState` writing `LookDirection`; any AI code writing throttles or `Direction`; any
    second model of what the ship will do, whether a closed-form estimate, a capability table of its own, or a
    branch on which actuators are missing.
  - Shared paths: `MoveToOrbitState` (patrol) and `CombatState` both go through `Accelerate`.
- **Verification:**
  - `DjinniWithoutSternReachesTargetByCrabbing`: stern cluster destroyed, target 300 m at a bearing 30° right of
    the nose, driven through `Agent` plus a fixed-target `MoveToState`. The ship arrives within 10 m in under 8 s.
    Base never arrives (speed 0 at 120 s). Pins R1(2) and the stuck-forever cheese.
  - `CrabBeatsTheBigCircle`: **the operator's case, and the reason Cut 4 predicts instead of deciding.** The
    fixture is a hull that can strafe cleanly but can only rotate by spending a main: two off-centre mains aft and
    one amidships lateral pair whose torque cancels, no attitude thrusters, with an objective abeam to port at
    300 m. A turn-to-face policy run in the same harness spends the manoeuvre sweeping a wide arc that barely
    improves the bearing and opens the range; the planner's choice closes it. Pinned as time to reach the
    objective, and as range at the horizon, never as "the planner chose to strafe".
  - `CrabBeatsPointAndBurnWhenMainIsWeak`: Djinni with Stern 1 and Stern 2 destroyed (asymmetric; Stern 3's torque
    is balanced by the laterals), target 300 m at 60° left. The planner arrives strictly sooner than a naive
    point-and-burn agent run in the same harness through the same Cut 3 controller. A relation, not a captured
    number.
  - `IntactShipsStillPointAndBurn`: an intact Djinni arrives no later than base's 4.34 s + 10%, with the restored
    hull's own `VelocityLimit` in force. Pins no regression.
  - `LonginusWithoutReverseFlipsToBrake`: target velocity opposite the current velocity; the predicted-outcome
    score puts the ship round, because a Longinus has no reverse thrust.
  - `PlannerTakesTheLurchWhenItPays`: the Longinus with `Th.CCW` destroyed and a Δv 20° to port, where the
    lurching turn is genuinely faster. The ship gets there sooner than a fixed-heading strafe would in the same
    harness. Pins that the planner inherits the controller's comparison rather than avoiding the lurch on
    principle.
  - `PlannerPredictionMatchesFlight`: run a candidate's prediction, then fly the same commands for the same
    horizon, and compare the end states. They agree within a small tolerance. Pins that the prediction uses the
    real controller and that nobody has grown a second model.
  - Negative grep: `LookDirection` in `Agents/States/MoveTo.cs` should match nothing.
  - Stryker `--since:<Cut 4 base>` over `Agent.cs`, `MoveTo.cs` and `ShipControl.cs` queries.
  - Operator: watch a patrol Djinni with a stern thruster shot out crab to its next orbit.
- **Ledger estimate:** −22 lines, +140 lines of source (the prediction loop replaces the closed-form score),
  +300 lines of test. No new type beyond the plan and the candidate.

## Cut 5. Combat facing and tactics (target shape only)

This cut gets mapped after fire-control Cut 12 lands, because that cut decides what presenting a side is worth.

- **Owner:** `CombatState` (`Combat.cs:88-127`), above the inputs.
- **Shape:** extend Cut 4's predicted-outcome scoring with what the weapons want — arcs, ranges and the aim the
  fire-control path will take — so the facing a ship holds is the one its predicted damage favours, not a rule
  about its turn asymmetry. A ship that turns one way fast and the other way slowly will, under that scoring, tend
  to keep its target drifting toward its fast side, because that is what predicts better. A ship with good turret arcs and poor rotation holds a heading and lets the
  turrets work. Weapons with fixed arcs pull the heading onto the target. It uses the Cut 4 planner for thrust.
- **Fork:** Q4 (heading versus aim), which must be ruled before mapping.

## Operator questions

Each question gives options and a recommendation. Self paces these one at a time. Q1, Q2 and Q3 were ruled on
2026-09-22; their text is kept as history, and the live design is in the cuts above.

- **Q6: the yaw weight in Cut 3's solve (does not block Hands).** The solve has exactly one knob, the weight on
  the yaw residual against the translation residuals, after each axis is normalized by that ship's achievable
  extreme. On a Longinus both the strafe and the turn come from the same nose thrusters, so the knob trades
  heading-holding against how hard the mains are spent for torque (closed-loop model, table in Cut 3):

  | w | strafe gained in a held second | heading deviation while strafing | 90° turn in place | main peak |
  |---|---|---|---|---|
  | 1 | 46.4 m/s | 59° | 0.60 s | 0.37 |
  | 2 | 13.7 m/s | 3.8° | 0.36 s | 0.63 |
  | 3 | 10.7 m/s | 1.1° | 0.30 s | 0.73 |
  | 5 | 9.9 m/s | 0.4° | 0.28 s | 0.79 |

  The Djinni is unaffected at every value: it has the actuators to do both cleanly. Base, for reference, does not
  strafe at all and turns 90° in 0.32 s.
  - **A: w = 3.** Heading holds while strafing, turns match base's speed, and the mains carry a real share of the
    torque in a turn.
  - **B: w ≈ 1.** The mains stay nearly out of turns, but a Longinus asked to strafe swings its nose most of the
    way round, because strafing and turning are the same thrusters.
  - **C: w = 5.** Heading holding is near-exact and the mains are spent hardest.

  **Recommended: A.** Hands starts there and the Cut 3 pins are written against it. This is a feel question, so
  the honest place to settle it is the Cut 1 play check; changing it afterwards moves numbers in pins, not
  structure.
- **Q4: heading versus aim (blocks Cut 5 only).** `LookDirection` is both the heading command and the aim, read by
  `LockWeapon.cs:94`'s lock cone, guided projectiles, reticle targeting and the tractor beam. Capability-aware
  facing wants the AI to hold a heading that is not its aim, and under R2 any split applies to the player too.
  - **A.** Add a heading input beside the aim on the shared surface. The player's heading follows the view unless a
    player control is added later.
  - **B.** Keep one input, and the AI accepts degraded lock cones and missile aim while it manoeuvres.

  **Recommended: A**, decided when Cut 5 is mapped.

### Ruled or dissolved (history)

- **Q1: intact-ship divergence from base.** **Ruled A, 2026-09-22: "The divergence is fine."** The four accepted
  divergences and their magnitudes are listed in Cut 3's verification. The rejected option was to require exact
  base behaviour, which would have meant reintroducing per-thruster role rules into the objective.
- **Q2: rotation from translation thrusters.** **Ruled B, 2026-09-22: "that's absolutely viable and can be a
  better decision than turning around the long way, I wouldn't introduce a thruster exclusion for this."** The
  option this map recommended (A: rotation authority only from thrusters above `TorqueFloor`, so a lost attitude
  thruster means that direction is gone) was rejected. Cut 3 and Cut 4 are written to B.
- **Q3: Longinus fixture layout.** **Ruled, 2026-09-22: "Good catch on the Longinus main thrusters, I was
  misremembering."** Fixtures follow the data: two aft 2×2 mains.
- **Q5: how the rotation demand is sized.** **Dissolved, 2026-09-22.** The question assumed a separate rotation
  demand that something had to size. The operator refused the premise — "it should only do that if the ship's
  controller also *wants* to accelerate forwards that much" — and the answer is one solve over the whole wrench
  with both residuals in the same currency, which makes the three regimes consequences rather than rules. Neither
  of the options this map offered (size from attitude authority with a fall-back; size from everything) survives,
  and `TorqueFloor` loses its last consumer. What is left of the question is Q6, the single weight.

## Subtraction ledger (estimates)

| Cut | Removed | Added | Dependencies / targets |
|---|---|---|---|
| 1 | 0 source; the one-shot command is deleted again in this campaign | 3–7 catalog records + products; ~150 lines of throwaway tool + ~120 test | No new package, target, asset or authored field. Existing prefabs, schematics and factions are reused |
| 2 | 0 | ~200 src + ~150 test (CultLib) | CultMath gains one file; cultmath-unity 0.2.4; Aetheria pin bump |
| 3 | ~212 (`Ship.cs` ~200, `Behaviors.cs` 4, `Entity.cs` 1, setters, `TorqueFloor` and its authored value) | ~300 src + ~500 test | `IAnalogBehavior` and one authored setting removed; four capacity solves a tick removed; no new package, target or authored field |
| 4 | ~22 | ~140 src + ~300 test | none |
| 5 | not mapped | not mapped | not mapped |
