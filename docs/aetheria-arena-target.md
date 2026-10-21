# Aetheria arena: deterministic headless matches between swappable brains

Date: 2026-10-21. Status: target, drafted by Imagination for Self to commit and admit.
Body pinned at `GameCult/Aetheria` origin/master `d52b50f9`. Probes ran on that tree.

## 1. Why

The operator ruled that arenas are a design instrument (`aetheria-release:ruling:arenas-explore-the-metagame`). Agents
explore the metagame through virtual play, and the design iterates on what they find. Her words: "We use agents to explore the
metagame space so that we can iterate on the game design through virtual play." The combat Colosseum develops NPC
behaviour by implementing many strategies over shared primitives, and the arena selects among them
(`colosseum-is-the-npc-behaviour-lab`). Colosseum statistics fit the composition model that settles fights off screen
(`colosseum-feeds-matchup-model`, `matchup-composition-model`). The strategic arena trains on generated galaxies varied
across seeds (`strategic-start-r2-generator-trains`). The demo waits on how well that sim performs
(`demo-start-gated-on-sim-quality`).

All of that rests on one foundation. That foundation is the campaign:

- A match is a pure function of its inputs.
- A brain reaches the world only through a typed seam.
- A match can be recorded, replayed and checked.
- Matches run headless on Yggdrasil fast enough to beat rating noise.

The strategic state, its rules, the matchup model and the strategic arena are later campaigns (section 6). They consume this
foundation and do not reshape it.

## 2. Body facts (probed at d52b50f9)

Randomness:
- **Combat dice.** Combat dice are per shot: `FireControl.Commit` seeds a local generator from `Zone.CombatSeed` and the
  shot id (`FireControl.cs:931`). AI dice are per pilot: `SimulationDice.For(CombatSeed, CombatSampleStream|PatrolStream,
  Agent.Id)` (`SimulationDice.cs:9-38`, `Combat.cs:178`, `Zone.cs:320-323`). Drop dice use `Zone.DropSeed` and the
  death ordinal (`Zone.cs:131,159,460`).
- **CombatSeed ignores the run seed.** `Zone.CombatSeed` is `GalaxyZone.Name.StableHash()` (`Zone.cs:158`), with no run
  seed in it. This is ruled: `aetheria-release:ruling:run-seed-drops-only` (2026-10-18, operator). It means one scenario
  replayed at one galaxy seed rolls identical dice every round.
- **The item stream at run start.** `ItemManager.Random` is still clock-seeded at construction (`ItemManager.cs:20`).
  `RunStart.SeedItems` reseeds it from the galaxy seed before arena generation (step 1) and before staging (step 2)
  (`RunStart.cs:62-65`). After staging, the stream runs on shared. Its draws are lot quality on every mint
  (`ItemManager.cs:147,165`), the scatter of an unpacked ship packed at the origin (`Zone.cs:276`), the wormhole exit and
  console spawns (`ActionGameManager.cs:589-593,710`), and staged and generated fits (`ScenarioStage.cs:31,92`).
- **Zone generation.** It seeds a local generator from the zone's name hash and its position (`ZoneGenerator.cs:52-53`). A
  zone reached by warp is generated in `ActionGameManager.PopulateLevel` (`:716-731`), a Unity owner, and not in
  ServerShared.
- **MIConvexHull is gone.** Commit `e56af288` replaced it with a double-precision Delaunay (`Delaunay.cs`). The
  `colosseum-npc-lab` follow-up's "unseeded MIConvexHull randoms" item is stale.
- **Trig is the host's libm.** CultMath trig and pow forward to `MathF` (`CultLib packages/cultmath/src/CultMath/math.cs:62-70`).
  Cross-host float identity is therefore unproven: Windows UCRT and glibc may differ in the last ulp.

Determinism probe:
- **What ran.** A scratch partial of `RunStartTests` on Yggdrasil, Linux, .NET 10.0.12. It ran `LaunchDuel`: the Duel
  scenario, seed 104, the item stream reseeded. The player is scripted to fire every 90 steps and the rival is an AI. It
  stepped 36,000 steps at 1/60 s and hashed `Snapshot` every step.
- **Result.** Three separate processes gave the same hash, `082F7400...`. The first death came at step 1445. Generation
  took about 325 ms per launch, and stepping ran about 9,700 steps/s. That is about 160x real time, but mostly with one
  ship left after the first death, so it is not yet a two-ship throughput figure.
- **Existing pin.** `SimClockTests.OneHistoryTwice` already pins in-process repeatability of the Duel.

How brains reach the world:
- **The pilot writes ship fields directly.** `Agent.Update` writes `Ship.Turn`, `Ship.MovementDirection` and
  `TryActivateConsumable` (`Agent.cs:53-110`). `Combat` writes `Turn` and calls fire control (`Combat.cs:149` and
  others). `MoveTo` writes too.
- **The flight decides targets.** `Flight` picks `Ship.Target` (`Flight.cs:178`) and `Agent.Task` from the union of its
  members' `VisibleEnemies`, so target choice is sensor-gated. Flights are keyed by faction, one per zone
  (`Zone.cs:75,312`), and tick in `Zone.Update` (`:363`).
- **Aiming reads ground truth.** `Combat` reads the target's true `Position` (`Combat.cs:52`). Its picture of the
  target's weapons comes from the EW book (`Combat.cs:55-60`).
- **The player has its own writes.** The player path is `ActionGameManager`, with 12 direct writes of the same fields.
- **Agents hold live references.** An agent holds live `Ship`, `Zone` (through `Ship.Zone`) and `ItemManager`
  references (`Agent.cs:27-31`). Nothing narrows what a brain can read or write.
- **Defined but unused tasks.** `HaulingTask`, `Mining`, `StationTowing` and `Survey` are defined, but nothing
  constructs them.

Harness:
- **The run owner.** `RunStart` owns new runs and every run is a `Scenario`, compiled C#
  (`Scenario.cs`, `Scenarios/*.cs`: 17 scenarios, Duel and Pursuit among them).
- **Headless today.** `Zone.Update` steps headless. Fights resolve headless, with commit and resolution in `FireControl`.
  `SimClock` is the fixed-step owner.
- **No runner.** There is no headless runner command: `tools/AetherDb/Program.cs:25-56` has catalog commands only. The
  scenario tests are the only headless driver.
- **Off screen.** Only the player's zone ticks (`ActionGameManager.cs:1357-1361`). Zone ownership is written only at
  generation (`Galaxy.cs:346`) and on load (`:77`). There is no job or mission generator (`Galaxy.cs:578`).

## 3. Invariants

- `A-pure`: A match is a pure function of (code revision, catalog revision, scenario id, match seed, brain ids per
  side, host determinism class). The same inputs give the same per-tick state digest, in any process, run alone or
  in a batch.
- `A-no-clock`: No input to a match comes from the wall clock, process identity, hash-code ordering of reference
  types, or a stream another subsystem advances. Every draw a match reaches comes from `SimulationDice` (or a successor
  owner) keyed by a structured seed. A missing seed is refused, never defaulted to the clock.
- `A-seam`: A brain reads only its side's `PilotView` and writes only `PilotCommand`s. One applier in the zone step
  writes ship control fields from commands. Brains, the player's input and the playground use that applier and nothing
  else. A brain holds no live `Ship`, `Zone` or `ItemManager` reference.
- `A-truthful-view`: A view holds what that side's sensors and book give, the player's information. It never holds
  ground truth about another side. (Depends on question `observation-fidelity`.)
- `A-replay`: A replay holds the match inputs, the command stream and periodic digests. Replaying it reproduces every
  digest, or it names the first tick that diverges. A replay records its host determinism class. Under
  `cross-host-determinism`, it is trusted only on that class.
- `A-compiled`: Brains and arena scenarios ship as compiled C# content in the Aetheria repo, under stable ids.
  No model call, interpreter or DSL runs inside a match (`arenas-explore-the-metagame`, Aetheria's compiled-mods rule).
- `A-one-sim`: The arena runs the game's own simulation. There is no arena-only physics, combat or rules fork. An exploit
  the arena finds is a game bug.
- `A-headless`: A match runs with no Unity type loaded, from a command that runs in Yggdrasil's dotnet container.
- `A-records`: Match results, replays and tournaments are CultCache documents in a directory the runner is given. They
  never land in `GameData/run.cc`, `player.cc` or the catalog.

## 4. Not in scope

- Strategic state: holdings, stockpiles, faction relations, strength or fronts. Also any off-screen zone tick, ownership
  change, job board or generator starting-infrastructure rule. These belong to campaign 2.
- The matchup composition model and its fitting or residual retraining (campaign 2 or 3).
- Character or fun scoring, the model judge and the operator's pairwise calibration
  (`strategic-scoring-success-and-character`; strategic arena).
- A large strategy library. This campaign lands the seam and two brains, enough to prove a tournament. The vocabulary
  grows as content afterwards.
- An Eve/CultMesh operator surface for tournaments. Results are CultCache documents, and a surface is a follow-up once
  there is a consumer.
- Moving warp-time zone generation out of `ActionGameManager`. It becomes in scope only if a match crosses a wormhole,
  and arena matches are one zone.
- Cross-host deterministic trig in CultMath. It is a CultLib follow-up if `cross-host-determinism` rules that way.
- The Unity presentation of arena matches, and replay viewing in the client.

## 5. Identity, lifecycle, authority

| Kind | Identity | Lifecycle | Authority (owner; forbidden writers) |
|---|---|---|---|
| Brain (compiled strategy) | A stable string id declared by the class, such as `doctrine.v1`. Ids are unique across the brain registry, which refuses a duplicate. Behaviour is versioned by code revision, not by the id | Added as code. Revised by commits: a behaviour change that should not merge ratings bumps the id's version. Retired brains stay compiled, become products or faction behaviour (`colosseum-is-the-npc-behaviour-lab`), or are deleted | Owner: the brain registry, compiled. Forbidden: any runtime-loaded or model-written brain, and a brain writing ship fields directly |
| Arena scenario | A stable id string on `Scenario`. Today the identity is `Name`, a display string, so a stable `Id` is a finding to add | Same as brains: code, revised by commits. A match records its id and the code revision | Owner: the `Scenarios` registry. Forbidden: a match staging entities outside `RunStart.Stage` |
| Match seed | A uint chosen by the tournament, nonzero. It feeds the galaxy seed, the item-stream steps and the start variance (per question `match-variance`) | Fixed per match, never rerolled. Replay reuses it | Owner: the runner, from the tournament spec. Forbidden: the clock, and `Scenario.Seed`'s clock fallback inside a match |
| Combat, drop and pilot dice | `SimulationDice.For(zoneSeed, stream, ordinal)`. `CombatSeed` is the zone name hash (ruled) and `DropSeed` mixes in the run seed | Derived each draw, never stored | Owner: `SimulationDice`. Forbidden: any draw on `ItemManager.Random` from behaviour or combat (`ai-sample-offset-off-item-stream`) |
| Item stream (`ItemManager.Random`) | One stream per `ItemManager` | Reseeded by `RunStart` steps 1 and 2. After that it advances on every mint | Owner: `RunStart` seeds it and `ItemManager` draws. Forbidden: its clock-seeded default reaching a match (`A-no-clock`) |
| PilotCommand | (match, tick, side, ship ordinal `Agent.Id`) | Produced by a brain each tick and consumed by the applier the same tick. Recorded in the replay, never re-derived | Owner: the side's brain, or player input. Forbidden: any writer of ship control fields other than the applier |
| PilotView | (match, tick, side) | Derived each tick from the side's sensors and book. Never stored, and never in the replay except as a digest if wanted | Owner: the view builder, one function. Forbidden: a brain reading `Zone`, `Ship` or `ItemManager` |
| Replay | A CultCache document keyed by match id = (tournament id, ordinal) | Written once when the match ends and never edited. Superseded only by a new match. A divergence on replay is a finding, not an edit | Owner: the runner's recorder. Forbidden: a replay written by the game client, until a cut says so |
| Match result | Keyed by match id, like the replay | Written with the replay. Read by the tournament and, later, by the matchup model | Owner: the runner. Forbidden: hand edits, and results with no replay |
| Tournament | A CultCache document keyed by a tournament id (date plus label). It names its brains, scenarios, seed range, code and catalog revisions | Created by the runner. Ratings are derived from results and recomputable. A rerun is a new tournament | Owner: the runner. Forbidden: ratings stored as truth without the results they derive from |
| Host determinism class | (OS, architecture, runtime) | Recorded on every replay and result | Owner: the runner. Forbidden: comparing digests across classes unless `cross-host-determinism` rules them one class |

No cell is empty. The Scenario row exposes a gap: `Name` is today's only identity, and it is a display string. Cut 1 adds `Id`.

## 6. Campaign split

1. `aetheria-arena` (this one): determinism, the seam, replay, the headless runner and the first tournament.
2. `aetheria-living-world` (later):
   - strategic state owned in ServerShared;
   - the galaxy generator plus faction starting-infrastructure rules;
   - the off-screen strategic tick at reduced detail;
   - ownership changing hands;
   - the lore institutions as damping rules (`snowball-damping-lore-institutions`);
   - the job board from needs;
   - the strategic arena on generated seeds, scored on success and character.

   It owns the demo gate (`demo-start-gated-on-sim-quality`) and absorbs follow-up `release-target-living-demo`'s map.
3. `aetheria-matchups` (later, after 1 and enough brains): Colosseum statistics fitting the composition model and the
   on-screen residual loop. It is split from 2 because its foundation, a fitted statistical model, is a different
   pipeline. Campaign 2 consumes its output through one typed predictor.

## 7. Cut sequence (campaign 1)

1. **Determinism ledger.**
   - Add `Scenario.Id`.
   - Refuse an unseeded `ItemManager` in a match (delete the clock default and pass the seed in).
   - Move the `Zone.cs:276` scatter onto `SimulationDice`.
   - Pin a two-ship AI-versus-AI digest test.
   - Run the same digest on Starfire and on Yggdrasil, and record the cross-host result.
2. **Command seam.** Add `PilotCommand` and one applier in `Zone.Update`. Route Agent, Combat, MoveTo and
   ActionGameManager's direct writes through it, and delete their direct writes.
3. **View seam.** Add a `PilotView` built from sensors and book. Brains lose their live `Ship`, `Zone` and `ItemManager`
   references. `Flight` and `Minion` become `DoctrineBrain` behind an `IBrain` registry. Aiming moves to the view if
   `observation-fidelity` rules for it.
4. **Replay.** The recorder writes inputs, commands and digests every N ticks as CultCache. The replayer reproduces
   them and names the first divergent tick. Replay tests read an existing match.
5. **Runner.** `AetherDb arena`: a scenario, two brain ids and a seed range produce results and replays in a given
   directory. It runs in Yggdrasil's dotnet container, with two-ship throughput measured and recorded.
6. **First tournament.** Round-robin over two or more brains (`DoctrineBrain` plus one variant) and the combat
   scenarios, with enough seeds per pairing. Ratings are derived with confidence intervals, and the results document
   is ready to feed `aetheria-matchups`.
