# CultCache Migration Postmortem

Written 2026-09-15. It covers Aetheria and CultLib from the decision to migrate
(2026-09-12 18:03Z) to Aetheria `58b27690` and CultLib `a0813c6` (2026-09-14
23:33 local).

Related documents:
- Means: [`cultcache-migration-cut.md`](cultcache-migration-cut.md)
- Ends: [`cultcache-migration-target.md`](cultcache-migration-target.md)
- Evidence behind this document: the main session transcript
  `2a6aec4d-7cba-481c-8586-be54662389a5.jsonl` and 114 subagent records.
- The process this migration produced is formalized as the Claude Code skill
  `~/.claude/skills/eureka` (Eureka, the Claude Code counterpart of Epiphany).

## Summary

Aetheria's in-tree CultCache, the operator's original 437-line cache, has been
replaced by CultLib's modern CultCache. Along the way it gained CultMath in
place of Unity.Mathematics. Every piece of game state now lives in typed
`.cc` stores with one home store per type:
- **Catalog** (`GameData/Aetheria.cc`, read-only at runtime): authored data.
- **Run:** the current playthrough.
- **Player:** settings and bindings.

Four private file formats are gone, along with the vendored MessagePack, the
vendored JsonKnownTypes, and the IMGUI Database Tools. CultCache Studio edits
the catalog.

**Scale:**
- Aetheria: +5,652 / −36,920 lines, net −31,268.
- CultLib: about +15.7k / −13.35k attributable to the migration, most of the
  growth being CultMath's generated HLSL surface and the new tests.
- 114 subagents ran: 50 Hands, 32 Soul, 3 Imagination, 23 Eyes/Explore,
  6 Mind Steward.

**Still outstanding with the operator:**
- the play smoke;
- the Studio click-through, which also fixes three dangling `AmmoType` refs.

**CultLib missed its own size target.** The Caching core ended at 4,368 lines
against a target of about 3,150, and `CultCache.cs` at 2,265 against about
1,000. The ledger was never reconciled.

## Scope and invariants

The target was a **cache-only** migration.
- An earlier attempt, rolled back by `7006a6b0`, had failed because it bundled
  CultCache, CultMesh, Eve and daemons into one change.
- AetheriaEve was ruled out as a consumer early ("preserved for taxidermy").
- CultMath was the only scope added mid-migration, by operator ruling
  ("it's the best workout we can give it").

Invariants carried to the end, each pinned by tests (listed in the Eyes fact
sheet):
- Wire parity across runtimes, with C# as the reference.
- One home store per type, in C#, TS, Rust and Python.
- Loading never writes.
- The cache never invents globals; lifecycle owns them.
- Runtime type decides schema.
- Atomic single-store commit plus compare-exchange (proven in-process only).
- No observer runs under the cache gate; order is carried by a per-cache
  Sequence.
- Catalog read-only at runtime.
- ServerShared builds headless with no UnityEngine.

## Timeline

| Cut | Where | When (local) | Soul passes | Notable |
|---|---|---|---|---|
| 0 target + map | Aetheria `68fbce4a`..`82c1e72e`, second pass `c3377f11`..`040e50b2` | 09-12 23:12 to 09-13 15:52 | none (Imagination) | Fable ran out mid second pass |
| 1 contract + red tests | CultLib `37f73c4`..`0db1fe5` | 09-13 16:01 | 1 | SoA parked at `parked/cultcache-soa` |
| 2 subtraction | `d904960`..`14d7c64` | 09-13 16:21 to 17:17 | 1 | −2,533 lines; 5 kept durability invariants had lost their tests |
| 3 routing, atomic commit, Sequence | `a3cc394`..`825b4f7` | 09-13 17:57 to 21:02 | **6** | deadlocks, a lost write, a ticket system built and then deleted |
| 4 serialization, generator deleted | `6fcc2a7`..`5bd4e4e` | 09-13 21:09 to 09-14 00:21 | 3 | keyed get-only data loss |
| 5 TS/Rust/Python | `8dd5a45`..`47c7111` | 09-14 00:37 to 12:43 | 4 | "BROKEN against its own contract" on pass 1 |
| 6 Studio on an engine-free model | `8be174b`..`741d212` | 09-14 13:02 to 14:55 | **4** | unloadable-store key, IMGUI split authority |
| 6b CultMath HLSL parity | `d514efe`..`d4c43ed`, merged `1b95dd6` | 09-14 13:29 to 14:57 | **4** | `step` NaN taken from docs rather than dxc |
| 7 release | `546c919`, `ae194d5` | 09-14 15:16 | **0** | the tag push silently skipped publish |
| CultMath 0.2.1 to 0.2.3 | `f060536`, `111b918`, `0f2c1f0` | 09-14 16:16 to 18:19 | via 8a | gaps filled in the owner, not the consumer |
| 8a CultMath swap | Aetheria `4f4beb0f`..`2b38d2dc` | 09-14 17:38 to 18:29 | 2 | SectorMap pivot, degenerate zone seeds |
| 8b data model | `b4c06b50`..`9bdf6ef2` | 09-14 18:42 to 19:17 | 1 | Unity attached no run store |
| CultLib 1.0.58 / 1.0.59 | `b85a828`, `a0813c6` | 09-14 21:01 to 21:35 | 1 + 1 | nil map key broke Python |
| 9 importer | `70fbaca1` | 09-14 21:11 to 21:23 | 1 | dangling refs traced to 2021 deletions |
| 10 runtime cutover + presets | `4a545469`..`58b27690` | 09-14 21:43 to 23:33 | 3 | untested operator rulings; catalog writable in editor |

The whole migration took about 48 hours of wall clock, including one night.

## Structural delta

**Aetheria.**
- The ledger estimated −33,000; actual is −36,920 / +5,652 (net −34,388
  excluding docs).
- Cut 8b alone removed 35,989 lines, of which 31,293 were vendored MessagePack
  and `Unsafe.dll`.
- Added:
  - three UPM git pins;
  - a CultLib pin guard;
  - `Aetheria.Shared.Tests` (29 tests);
  - `AetheriaStores`, `RunSave`, `Loadout`, `StableHash`;
  - `GameData/Aetheria.cc` under LFS.
- 20 document schemas replace one root union.

**CultLib.**
- Removed:
  - the MessagePack generator and the empty Analyzers project;
  - SoA (parked, not deleted);
  - the managed document;
  - ambient transactions;
  - `initializeGlobals`;
  - the TS/Rust/Python mirrors;
  - the v2/v3 directory formats;
  - typed serialize helpers;
  - the Studio's by-name reflection bridge;
  - the composite constructor heuristic;
  - the `soft` flush flag in core.
- Added:
  - `Commit(Action<CultCacheBatch>)` with `Expect`/`TryCommit`;
  - a per-cache Sequence;
  - `CultInspectorModel`;
  - CultMath's HLSL surface, Unity bridge, pinned dxc, and PCG hashes;
  - `Contracts/cultcache-store-composition.md`.

**The core size miss has visible causes.** Soul-driven gate and publication
rules, compare-exchange, and the registry absorbing the generator's rules all
added lines, and the inspection model moved into core. None of these was
weighed against the target. The ledger stopped being updated after Cut 4.

## What Soul caught

32 Soul passes ran; 29 found at least one real defect that Hands' tests and
report had passed. The tally is roughly 75 defects. They fall into a few
families.

**Concurrency.** Five deadlocks, stalls or hangs:
- Cut 3's pull/upsert deadlock on directory stores, which hung 3 of 3 runs.
- A ticket-ordered delivery that deadlocked through
  `CultNetDatabaseSubscriptionServer._lifecycleGate` and could stall a cache
  forever.
- TS's per-cache queue deadlocking when a store awaited its own cache.
- TS's AsyncLocalStorage re-entry guard, which both over-refused and missed
  escaping waits.

Each of these had passing tests. Cut 3's race tests were red only on Windows,
by accident.

**Lost writes and data loss.**
- A re-pull erased a staged write the contract said was safe.
- A shared unpublished-changes list let one throwing observer discard other
  threads' committed changes.
- Keyed get-only members were silently skipped.
- Studio Save deleted presets captured during play.

**Wire parity.**
- 1.0.58 wrote an unset `CultRecordRef` dictionary key as nil, which Python's
  msgpack rejects under `strict_map_key`. It was released, then corrected
  74 minutes later in 1.0.59.
- A Python global stored under a legacy key made the cache unloadable.

**Semantics taken from the wrong authority.**
- CultMath's `step` NaN rule came from documentation. dxc compiles it the
  other way.
- The matrix row indexer silently wrote to defensive copies.
- The HLSL mirror "compiled as C#" claim was false at first.
- The mirror test later proved only compilation, not values.

**Lifecycle and ownership.**
- **8b.** Unity attached no run store, so every run write threw. Hands had
  described those paths as "upserting orphans". Separately,
  `PlayerSettings.SavedRun` was a second owner of the run root.
- **10.** The catalog was read-only in the editor only by convention. It had
  been opened writable for preset capture.
- **Split authority recurred at small scale.** Affordability stayed in the menu
  after the charge moved into `Materialize`. The IMGUI Studio kept key, element
  and narrowing decisions after the model was supposed to own them.

**Untested operator rulings.**
- **Cut 10.** Removing the availability rule, the price rule, or all-or-nothing
  on a failed fit left all 15 tests green. Hands' "each new test fails under its
  mutation" covered only the tests Hands chose to write.
- **Cut 6.** 3 of 7 inspection-model tests survived removal of their own rules.
- **Cut 2.** Five durability invariants that were kept had lost their tests
  along with the stage probes.

**Behaviour the captures could not see (8a).**
- `sign` returns `int`, so SectorMap's `sign(x)/2+.5f` became integer division.
- CultMath's shader-style `hash` gave 1,625 distinct zone seeds in 10,000
  positions.

**What these share.** The defect was invisible at the layer where the author
checked: unit tests in one runtime, captures that did not exercise the code
path, or a rule read from a document instead of the compiler. Soul found them by
checking at the layer where each would actually fail: another runtime's decoder,
dxc's lowered IR, Unity's decompiled `EditorGUI`, an independent decode of the
legacy file, or a scratch probe over every document type.

**The pass that did not run.** Cut 7, the release, got no Soul pass. Its
problems surfaced late:
- stale committed DLLs;
- a commit SHA baked into release byte checks;
- a native DLL rebuilt nondeterministically;
- a tag push that silently skipped publishing.

Hands or later Soul passes found each of these, but after tags were already
pushed.

## Operator corrections

35 rulings or corrections are recorded, with quotes, in the Eyes fact sheet. The
ones that matter here are those where the agents' model was wrong. For each, the
question is what context was missing.

- **Defunct neighbour repos treated as parity runtimes.** No doctrine said where
  canonical runtimes live. Fixed at the source: `F:\Projects\CLAUDE.md` now
  names `CultLib\packages\` as the only home.
- **Imagination stated cause and effect it never traced** ("the effects you
  listed don't follow from the causes", 09-12). The map made mechanism claims
  from names. From then on, the brief standard required probes for every
  mechanism claim.
- **Mirrors and non-decisions.** Replication had no consumers, and "3-5 are not
  decisions". The consumer audit came after the questions instead of before
  them.
- **"Soul pass on the cut map"** was a faculty confusion. It is now in doctrine:
  a pass over a plan is Imagination.
- **`initializeGlobals`.** The cache invented globals because Aetheria had a
  lifecycle problem. The operator named the real owner: lifecycle.
- **Style bar.** "Does the current code look *anything* like that". Four months
  of drift had nobody holding the original design up for comparison. The
  original 437-line cache became the explicit bar.
- **Atomic commit and compare-exchange** were slated for deletion as unused in
  C#. The operator knew they had been needed across Ghostlight and Epiphany. A
  consumer count limited to C# undercounted a capability the operator uses
  across projects.
- **Generator** ("do we even need a generator anymore?"). Once MessagePack
  became canonical, the generator only duplicated the registry's rules. The map
  had kept it because it existed.
- **Tickets, then Sequence** ("an itch regarding authority here"). Order was
  first carried by scheduling, as tickets that serialized delivery. The operator
  preferred order as data. A whole ticket subsystem was built, found deadlocking
  by Soul, and deleted.
- **CultMath "drop-in" and the Unity license.** Both claims were wrong until
  checked, by audit and by the operator. HLSL, not Unity.Mathematics, was the
  real parity target, and only the operator knew that.
- **Composite heuristic** ("what a silly and fragile idea"). Hands invented a
  constructor-matching guess to fill a drawer gap. It had no owner and no
  consumer.
- **Loadouts, three definitions in one evening.**
  1. The spec made loadouts player-store builds with a product per slot.
  2. "Blueprint means design": no manufacturer.
  3. "Think variants in Mechwarrior": authored catalog presets, not
     player-facing.

  Each redefinition reworked shipped code. The missing context was **product
  meaning**. Nobody asked what a loadout is *for* before specifying its store,
  its UI and its price. Imagination inferred the purpose from a broken legacy
  menu.

## Incidents

- **Probe fork bomb, 2026-09-13.** An Imagination probe (`cc-cas-probe`)
  relaunched `Environment.ProcessPath` with the dll as an argument, so every
  child re-ran the parent. Starfire needed two hard reboots and survived a
  third process storm only through a cancelled shutdown. It cost about two
  hours and woke the operator's household. PCIe ASPM was blamed first. The probe
  (P7, the two-process race) never produced a result, so cross-runtime lock
  exclusion is still unproven. The operator ruled out process-spawn policing.
  The kept lesson is narrow: read how a child chooses its role before running
  launch code.
- **Fable exhausted before Cut 1** (HTTP 429 at 13:51Z on 09-13). The plan had
  Fable on Imagination and Soul. Only 1 of 114 agents ran on Fable. Opus carried
  every other pass, including all 32 Souls, and the Soul hit rate held.
- **Permission classifier.** It blocked a Hands edit, then a Hands
  `git add -A && git commit -F - <<EOF` during Cut 2. The coordinator made the
  commit (`88e1c8e`). Briefs changed to message files, with "if blocked, stop and
  report".
- **BOM commit subjects.** PowerShell 5.1 `Set-Content -Encoding utf8` wrote
  BOMs into message files. Two pushed CultLib commits (`5aad137`, `e0a054a`)
  start with one; they were left, since fixing them needs a force-push. The rule
  is to write message files with the Write tool.
- **Parallel Hands shared commit-message filenames.** This was a near miss; no
  commit was actually swapped.
- **`git add -A` swept `PlayerSettings.msgpack`** into `5c5c3d5e`. It was
  untracked a commit later, but it stays in history. About 65 earlier LFS
  versions already existed, so history was left alone.
- **More than three tags in one push.** GitHub skipped the tag workflows, and no
  publish ran. `cultcache-py` 0.3.0 was re-pushed alone and reached PyPI. npm
  publication is parked by choice (CultLib QUIC map, Q5 C, 2026-09-16); no
  `NPM_TOKEN` exists and none is planned for now.
- **Stale incremental DLL.** `a9a2ba6` shipped a CultMath DLL from an incremental
  build that did not recompile. The release script now builds non-incrementally
  without Source Link. The native QUIC DLL got `/Brepro`.
- **An interrupted operator question.** A tool-based question was rejected after
  about an hour while the operator moved devices, and was re-presented as text.

## What worked

- **A separate falsifier.** A pass that finds real defects 29 times in 32 is not
  ceremony. It held on Opus.
- **Rulings with options and a recommendation.** The operator could answer
  "AAA" from a phone, and every answer landed in the map with its wording.
- **Gaps filled in the owner.** CultMath gained conversions, integer `clamp`,
  `frac(double)`, a dxc-matching `normalize` and PCG hashes. Aetheria grew no
  helpers, and `AetheriaMath.cs` was deleted.
- **Deleting old authorities before adding new ones.** Aetheria's four private
  formats, the vendored libraries, the importer and `SavedRun` are gone, not
  demoted.
- **Captures across cuts.** `census`, `factions` and `hardpoint-fit` stayed
  byte-identical from 8a through 10. That made the importer checkable, and made
  behaviour changes elsewhere visible by their absence.
- **Mind Steward at boundaries.** It caught stale claims, including the
  unmerged PackageCache coupling, player-store loadouts and "spawners
  materialize". It also moved process lessons into the skill.

## What to change in the pipeline

These are applied to the Eureka skill and recorded in its changelog.

1. **Soul before any release or tag.** The release cut got no Soul pass, and
   1.0.58 shipped a wire-parity defect found an hour later.
2. **Ask what a domain concept is *for* before specifying it.** Loadouts were
   rebuilt twice because product intent was inferred from a broken menu.
3. **Reconcile the subtraction ledger at every landing, and at close.** The
   CultLib size miss went unexamined.
4. **Every operator ruling gets its own failing mutation, listed in the Hands
   brief and checked by Soul.** Cuts 2, 6 and 10 all shipped rulings or
   invariants with no pinning test.
5. **Count a capability's consumers across every project the operator works
   in, not only one runtime's repos.** Atomic commit and compare-exchange were
   nearly cut.
6. **Prefer order carried as data over order carried by scheduling** when
   designing concurrency in a map. The ticket system was the costliest dead end.
7. **Safety check on process-launching probes:** read how a child chooses its
   role before launching it.

## Open follow-ups

- **Operator:** play smoke; Studio click-through, including the three
  `AmmoType` refs. Check `dangling` lists zero afterwards.
- **CultLib:**
  - Studio detects on-disk changes before Save.
  - The release byte check is enforced by script.
  - Collapse the dead empty-key check in `CultRecordRefFormatter`.
  - Unset-ref contract wording.
  - Remove `soft` from CultMesh/CultNetLocal and their callers.
  - CultNet/CultMesh sequence gaps.
  - By-name reflection in Networking and Mesh.
  - Prove cross-runtime lock exclusion (Rust `fs2` against C#
    `FileShare.None`) and the P7 two-process race.
  - Reconcile the core size ledger.
- **Consumers to re-pin:** Delvehold (generator ProjectReferences), EveUnity
  (`Deserialize<T>`), Epiphany and CodexConnector (Rust `Result`, `push_all`),
  VoidBot (TS promises), Heimdall.
- **Aetheria:**
  - No spawner uses presets yet (candidates: `ZoneGenerator.cs:309`,
    `ActionGameManager.StartGame`).
  - Hardpoint-configuration variants.
  - New Game clears the old run before generation succeeds.
  - `LookRotation` NaN is unpinned.
- **Release:** npm `cultcache-ts` 0.14.0 is tagged and unpublished by choice;
  registry publication is a later pass. Unity 1.0.47 to 1.0.56 were never
  tagged.
