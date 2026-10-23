# Aetheria arena, cut sim-rate: one game tick, lowered for game and arena alike

Date: 2026-10-22. Status: map, drafted by Imagination for Self to commit beside `docs/aetheria-arena-target.md`.
Body pinned at `GameCult/Aetheria` origin/master `0267a191` (view-seam merged). Every claim below was probed on that tree.
Spec: `aetheria-arena:cut_spec:cut-sim-rate.r1`. Question: `aetheria-arena:question:sim-tick-rate`.

## 1. What the operator said

On 2026-10-21, while we were talking about Colosseum throughput (session transcript `53e9c70e`, lines 4811-4879):

- "So we can expect 160x realtime for the Colosseum? That's not bad, we can gather a few thousand matches per hour per
  thread on Yggdrasil, then?"
- "I think we can fudge the time step just a little, as long as there's enough resolution for all the relevant decisions"
- "Projectiles are roll based, their flight is pure presentation, I made that decision partly with this in mind"
- "Physics definitely doesn't need 60, not at the pace we're setting. I don't think it even needs 30."
- Answering Self's proposal (lower the whole game's step, chosen by a timer check, an equivalence test and her playtest,
  landing before the runner): "Go for it."

That is ruling `aetheria-arena:ruling:lower-game-sim-rate`. Her words decide two things: physics needs less than 60 Hz,
and probably less than 30. The proposal she approved decides the rest: the game's own rate is lowered (`A-one-sim` holds
unchanged), and the rate is chosen by the timer check, the equivalence test and her playtest.

Rulings already in force on the same clock (2026-10-07, campaign `aetheria-release`):
- `sim-speed-knob`: the sim's own clock owns pace; Unity's time scale is not touched.
- `sim-speed-surface`: a design speed in game data times the player's accessibility slow-down.
- `sim-speed-tenth`: the design speed will be about a tenth of today's; the tick rate stays hidden; the player's ship may be
  extrapolated.
- `tick-rate-follows-design-speed`: the step follows the design speed, so the real-time tick rate is a chosen constant.
  "6hz is the kind of rate players notice. But we definitely don't *need* 60."
- `tick-30hz-floor-half`: "30 Hz with 0.5 floor". TickRate is 30 Hz at design speed. The player's factor ranges from 0.5 to 1,
  so the slowest tick is 15 Hz.

The design-speed and accessibility cut (`cut-sim-speed-scale.r2`) was withdrawn under `map-only-what-runs`. So on master the
game still runs at design speed 1 and 60 ticks per real second. The 30 Hz in `tick-30hz-floor-half` never landed. Her
2026-10-21 words ("I don't think it even needs 30") go below it, which is the fork in section 6.

## 2. Body facts

### Where the rate lives

- **Two hidden 60s.** `ActionGameManager.Clock = new SimClock(1f / 60f)` (`ActionGameManager.cs:251`) is the only clock
  that steps the game. `SimClock`'s constructor defaults `stepsPerRealSecond = 60` (`SimClock.cs:21`). The two are
  independent: changing the step to 1/20 while leaving the default would run 60 steps of 1/20 s per real second, which
  makes the game three times faster. The 60 is decided twice, by two writers that do not know about each other.
- **ServerShared has no game rate.** `Zone.Update(float deltaTime)` (`Zone.cs:356`) steps by whatever it is given. Every
  sim rule integrates the `dt` it is handed. Nothing in ServerShared knows the game's step.
- **The headless drivers choose their own step.** There is no runner yet. The tests that pin game histories step with
  literals: the two-AI digest (`DeterminismLedgerTests.cs:217`, `arena.Update(1f / 60f)`, 3600 steps) and the Duel history
  (`SimClockTests.OneHistoryTwice`, `ClockFrame = 1f / 60f`, `:134`). The tests contain 64 `1f / 60` literals, 31 of them
  `Update(1f / 60f)` calls. Most are physics fixtures that need some small step, not the game's step.
- **Unity's fixed timestep is not the sim.** No `FixedUpdate` or `Rigidbody` exists in `Assets/Scripts`. The sim steps
  only through `Clock.Advance(Time.deltaTime, ...)` (`ActionGameManager.cs:1333`). `ProjectSettings/TimeManager.asset`
  decides nothing about the sim and is left alone.

### What assumes 60 Hz

The question is not whether a rule mentions 60. It is whether the rule's per-second meaning depends on the step.

- **Timers that drop their overshoot.** These rules restart a normalized countdown at 1 and discard what the last step
  overshot:
  - `InstantWeapon.Execute` (`InstantWeapon.cs:227-265`): the cooldown ends when `_cooldown < 0`. The next burst starts
    only when a pilot triggers it again on a later step.
  - `Cooldown.cs:51,60`.
  - `Sensor` ping cooldown (`Sensor.cs:144-148,183`).
  - `ConstantWeapon` ammo interval (`ConstantWeapon.cs:148-151`).

  For the cooldown this works out to `floor(C/dt) + 1` steps per cycle, not `C/dt`. A 0.25 s cooldown fires every 16
  steps at 60 Hz (0.267 s, 6.7% slow) and every 6 steps at 20 Hz (0.30 s, 20% slow). Damage per second is therefore a
  function of the tick rate today, at 60 Hz as well. This is the rule that would make an equivalence test fail for the
  wrong reason.
- **Timers that already carry.** Two rules keep their overshoot, so their per-second rates survive any step:
  - the beam's roll cadence (`ConstantWeapon.cs:185-196`, `while (_beamTimer >= interval) _beamTimer -= interval`);
  - the burst's round cadence (`InstantWeapon.cs:243-270`, several rounds in one step if the interval is shorter).

  Transfer-verb's give budget (`CargoTransfer.cs:80-94` on its branch) carries too.
- **Passive sensing is a forward-Euler decay.** `Sensor.Execute` applies `next = saturate(previous + G dt)` and then
  `next *= 1 - TargetInfoDecay * dt` (`Sensor.cs:225-231`). Its fixed point is `G (1 - k dt) / k`, not the `G / k` that
  `Entity.PassiveInfo` documents and returns (`Entity.cs:518-532`). The bias is `k dt`: detection ranges shrink as the
  step grows. The exact form, `x e^{-k dt} + (G/k)(1 - e^{-k dt})`, settles at `G / k` at every step. A ping is an impulse
  with no `dt` (`Sensor.cs:251-254`) and is step-free.
- **Rules that are step-free already.**
  - Drag is `decay(v, drag, delta)` (`Ship.cs:132`).
  - Shots are rolls: commit and arrival are sim times checked each step (`FireControl.Step`, `CommitHorizon = .5 s`), so a
    resolution lands up to one step late, never differently.
  - Tunnelling does not exist for rounds (ruling `projectile-flight-is-presentation`).
  - One-tick notifications (paints, ping crossings, `IncomingPaints`) are defined per tick and read per tick
    (`ActionGameManager.cs:1336-1340`).
- **Integration error that remains.** Ship motion is stepped explicitly. At a larger step, a hard-turning ship's arc and the
  in-arc and in-range test at commit move by O(dt). This is the residual her "fudge" admits. The equivalence test measures
  it.
- **Hull contact.** `Contacts.Step` tests overlap after every move. A closing speed times the step that exceeds the
  smallest hull's radius can pass through. The cut checks the catalog's fastest ship against its smallest hull at the
  chosen step.

### How the render covers the step

`DrawAhead` (`DrawAhead.cs:13-19,114-139`) draws every moving thing at `T = Zone.ExactTime + Clock.Lead`:
- closed-form poses (orbits, belts, the field) at T;
- integrated poses (ships, bodies) as the last step plus step velocity times `Lead`;
- the ship's heading turned by `TurnRate * Lead`.

This is extrapolation (dead reckoning), not interpolation. It adds no latency, and presenters store no previous state, by
rule. At 20 Hz, `Lead` reaches 50 ms. The visible cost is a snap whenever velocity or turn rate changes between steps:
about `Δω * Lead` in heading (a full turn-rate reversal of 3 rad/s gives 0.15 rad at 20 Hz, against 0.05 at 60). Positions
move by `½ a Lead²`, which is negligible.

Input reaches the sim through `PilotCommand`. Held fields persist, and one-shots queue in order until the next step applies
them (`Pilot.cs:43-102`). A tap shorter than a step still fires, because a pull and then a release in one step both apply,
in order. The step costs the player up to one step of input lag: 33 ms at 30 Hz, 50 ms at 20 Hz.

### What the digest pins

`TwoAiDigestIsPinned` pins one SHA-256 over 3600 per-step `Snapshot`s of the two-AI fight at `1/60`, for host class
`linux-x64` (`DeterminismLedgerTests.cs:31-34,186-249`). It has been repinned twice, each for a stated cause. This cut
repins it once, for two causes landing together: the game step (the history has a different number of steps of a
different length), and the carried timers and exact sensor decay (cadence and detection change at any step). It still
pins the same simulated length: 60 s, which is `60 * TickRate` steps. `OneHistoryTwice` and `TwoAiFightIsOneHistory`
compare two runs and need no pin.

### Measured on Yggdrasil

**Not measured yet.**
- **The probe is ready.** `SimRateProbe.cs` sits beside this map. It runs a catalog timer survey and the two-AI fight over
  120 simulated seconds at 60, 30, 20, 15 and 10 Hz, three reps each after a warm-up, and reports the best wall time,
  shots, hits, first hull loss, first death and survivors.
- **Why it has not run.** It waited about an hour for a Yggdrasil slot. Then three runs failed in the fixture: the test
  catalog now loads the first-party mod `aetheria.nakovalnya`, whose `ship.cc` and `ship.glb` are LFS objects the old
  probe recipe did not resolve. With every pointer under `GameData` resolved, one is still missing.
- **The recipe to use.** transfer-verb's job script resolves LFS for current master
  (`F:\Projects\eureka-handoff-aux-2026-10-19\imag-nak-r2\job.sh`).
- **What the earlier probe showed.** At d52b50f9 the Duel ran about 9,700 steps/s at 60 Hz, mostly with one ship alive.
- **Expectation, not a measurement.** The cost per step does not depend on dt, so cost per simulated second scales with
  the tick rate: 2x at 30, 3x at 20, 4x at 15.

## 3. Prior art

- **Glenn Fiedler, "Fix Your Timestep!" (2004).** Use a fixed `dt`, an accumulator fed by real time, a clamp against the
  spiral of death, and then draw either by interpolating the previous and current states (smooth, one step of added
  latency) or by extrapolating. `SimClock` already is the accumulator and the clamp (`MaxFrameSeconds = .1`), and
  `DrawAhead` is the extrapolating branch.
- **Supreme Commander (2007)** runs its sim at 10 ticks per second and draws interpolated, for fleets of hundreds. It is
  the closest precedent for a large-unit, non-twitch game at a low tick.
- **StarCraft II** runs 16 game loops per sim second (22.4 per real second at Faster), with deterministic lockstep and
  interpolated rendering.
- **Factorio** is fixed at 60 UPS, with every timer counted in ticks and lockstep multiplayer, and no decoupling of sim and
  render rate. It is the counter-example: it keeps 60 because its belts and inserters are tick-quantised content. Ours are
  per-second content.
- **EVE Online** runs its Destiny ball physics on a 1 Hz server tick, and clients interpolate and predict. It shows that
  tactical space combat at a slow pace tolerates very coarse ticks when the player commands intent rather than twitch.
- **X4** steps the sectors the player is in at frame rate and the others at a much coarser, abstracted rate. That is the
  living-world campaign's concern, not this cut's.

The lesson that carries over: content authored in seconds survives a step change only if every timer and integrator is
step-exact. Fiedler's accumulator gives that for the clock, and section 2's two families (timers that drop overshoot,
Euler decay) are the places where Aetheria does not have it yet.

## 4. Authority map

- **Owner.** `SimClock` owns the game's tick: `SimClock.TickRate` (steps per real second at design speed 1) and
  `SimClock.GameStep = 1f / TickRate` (sim seconds per step). `SimClock.ForGame()` builds the game's clock from both.
- **Inputs.** None at run time. It is a code constant, so it is part of the code revision in `A-pure`'s tuple.
- **Outputs.** The `dt` every game-rate driver hands to `Zone.Update`, and `Lead`.
- **Derived.**
  - `ActionGameManager.Clock` is `SimClock.ForGame()`.
  - The digest, the Duel history and later the runner step by `SimClock.GameStep`.
  - A timer's cadence and a sensor's settling level are functions of seconds, not of steps.
- **Demoted.**
  - `SimClock`'s `stepsPerRealSecond` default is no longer an owner. The parameter becomes required, and the game's value is
    derived from `TickRate`.
  - `1f / 60f` in `ActionGameManager` is no longer an owner; it is derived from `SimClock.GameStep`.
  - Physics fixtures may keep a literal step of their own, because they test physics, not the game's history.
- **Forbidden writers.**
  - Any `new SimClock(` in `Assets/Scripts` other than `ForGame`.
  - Any literal step in a driver of a pinned game history.
  - A timer that resets to 1 and discards its overshoot.
  - A per-step decay written as `1 - k * dt`.
- **Shared paths.** The player's game, the digest and Duel histories, and the future runner all step `Zone.Update` by
  `SimClock.GameStep`. The design-speed cut, when re-mapped, sets `Step = DesignSpeed / TickRate` inside `ForGame` and
  touches nothing else.
- **Deletion line.** The default `= 60` and the `1f / 60f` in `ActionGameManager` go first. The overshoot-dropping resets go
  before the rate changes, so the rate change is measured on step-exact rules.

## 5. Not this cut

- **Brains deciding below the physics step.** The ruling allows it, and `PilotCommand`'s held fields already make it
  possible. No brain-rate machinery lands here.
- **Design speed and the player's slow-down** (`sim-speed-scale`, withdrawn). `ForGame` is the one place where it will
  land.
- **Render interpolation.** Extrapolation is ruled (`sim-speed-tenth`, DrawAhead's no-stored-state rule). Her playtest is
  where a heading snap would show. If it does, interpolating the player's ship only is the follow-up, at one step of
  latency.
- **Unity's `TimeManager` fixed timestep**, which the sim does not read.

## 6. The fork

`aetheria-arena:question:sim-tick-rate`: which tick rate Hands lands for her playtest, given `tick-30hz-floor-half`.
Options and recommendation are in the question. In short:
- 30 Hz keeps the in-force ruling and halves the cost.
- 20 Hz follows her 2026-10-21 words and costs a third, but at the 0.5 accessibility floor the real tick is 10 Hz.
- 15 Hz is a quarter of the cost and 7.5 Hz at the floor, near the 6 Hz she called noticeable.
