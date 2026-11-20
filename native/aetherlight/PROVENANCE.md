# Aetherlight provenance

Aetherlight is a fork of the synchronous world kernel of Ghostlight.

- Upstream: `GameCult/Ghostlight`, `crates/ghostlight/`
- Commit: `c43eb54962ed4a09cc737ecac73a16b0d7ce62b4` (origin/main)
- Taken: 2026-11-19
- Authority: cut `aetherlight-fork-import` r1 of campaign `aetheria-release`;
  rulings `aetherlight-narrative-state-owner` and `dialogue-is-world-action`.

Every file below was copied as a raw git blob (`git show`), never `git archive`.
The blobs are LF, as upstream. A Windows checkout with `core.autocrlf` turns the
`.rs` files and the two text fixtures into CRLF; rustc, the fixture readers
(`lines()`, `trim()`) and the golden test absorb that, so no `.gitattributes` is
kept (a first commit had one; its stated reason did not hold). The recorded-world
fixture (removed in AL2a) was binary and no newline rule touched it. Line ranges are in the c43eb54 file.

## Taken

| c43eb54 path (under `crates/ghostlight/`) | Here | Range | Edits |
| --- | --- | --- | --- |
| `src/lib.rs` | `src/lib.rs` | all | E1-E5, E10-E12 |
| `src/action.rs` | `src/action.rs` | all | E11, E12 |
| `src/clock.rs` | `src/clock.rs` | all | none |
| `src/journal.rs` | `src/journal.rs` | all | E12 |
| `src/grammar.rs` | `src/grammar.rs` | all | none |
| `src/grammar/tests.rs` | `src/grammar/tests.rs` | all | E12 |
| `src/import_key_tests.rs` | `src/import_key_tests.rs` | all | E12 |
| `src/patch.rs` | `src/patch.rs` | 1-6059 and 7906-9606 (dropped 6060-7905: the patch tool catalog, `PATCH_TOOLS`, `patch_tools()`, the `field_example` helpers, `mod catalog_tests`) | E6, E7, E12 |
| `Cargo.lock` | `Cargo.lock` | all | pruned by cargo to the dependencies below |

`Cargo.toml` is new: package `aetherlight`, its own `[workspace]`, no features,
dependencies rmp-serde, serde, serde_json, sha2, thiserror, unicode-normalization,
unicode-properties, uuid (serde only); dev uuid with v4 (AL3a).

## Edits

- E1. `lib.rs`: dropped the `mod` lines and every `pub`/`pub(crate) use` of
  aetheria, consumer, controllers, elaboration, local_inference (with its
  `cfg(feature = "test-support")` re-export), mailbox, sdk_inference, table,
  tool_schema, vault, verse.
- E2. `lib.rs` crate docs: retitled to Aetherlight with a pointer to this file;
  the `compile_fail` doc-tests name `aetherlight::` instead of `ghostlight::`.
  The sealing prose is kept.
- E3. `lib.rs`: `#![allow(dead_code)]`. The kernel has no entry point until AL4
  adds the ABI; AL4 removes the attribute.
- E4. `lib.rs`: imports that only dropped code used are removed
  (`cover::{CellId, Constituent, Resolution}`), or moved under `#[cfg(test)]`
  because only tests use them (`patch::{Bounds, Declaration, EntityDeclaration,
  SubjectDeclaration}`).
- E5. `lib.rs`: `let mut` removed from the closures `confine_subject` and
  `confine_place` (unused-mut in the non-test build).
- E6. `patch.rs`: deleted `use super::tool_schema;`,
  `use codex_connector::CodexToolDefinition;` and `use serde_json::{Value, json};`
  (only the dropped catalog used the last one); `let mut` removed from the
  `controller_id` closure.
- E7. `patch.rs`: lines 6060-7905 dropped as above.
- E8. `lens.rs`: deleted test `every_lead_names_a_patch_tool` and its
  `use crate::patch::PATCH_TOOLS;`, which read the dropped catalog.
- E9. `lens.rs`: the doc comment on `leads_with` says "patch tool names" instead
  of naming `PATCH_TOOLS`.
- E10. `lib.rs` crate docs describe the fork as it is (no runner, no ports, no
  controllers); the compile_fail doc-tests keep no error code (stable rustdoc does
  not check one) and the two that named items absent from the fork
  (`fixture_controller_opportunities`, `fixture_inference_output`) are deleted.
  The `VerifiedPrincipalEvidence` doc no longer links `WorldMailbox`/`SeedPort`
  and says nothing here enforces `valid_until`; the ABI must (AL4).
- E11. New tests, not upstream, each killing a mutant that survived upstream's 810
  tests: `action::tests::holding_exactly_the_threshold_satisfies_holds`,
  `clock_tests::committed_requires_the_named_counterparty`,
  `clock_tests::committed_requires_the_named_kind`,
  `soul_knowledge_tests::a_telling_by_a_speaker_outside_its_audience_does_not_apply`.

- E12. Cut AL2a deletes the model-guidance lanes; see the section below. It
  supersedes E4's `cover` import, E8 and E9 (`lens.rs` is gone), and `lib.rs`,
  `patch.rs`, `journal.rs`, `action.rs`, `grammar/tests.rs` and
  `import_key_tests.rs` lose the symbols and tests listed there.

The `ghostlight.*` schema ids were renamed in AL3a (see the last section). The
golden replay AL2a removed is back as `fixtures/golden.digests` (AL3a).

## Left behind

| Upstream file | Why it is not here |
| --- | --- |
| `mailbox.rs` | the async port/mailbox lane around the kernel; AL4's ABI replaces it |
| `controllers.rs` | model decider lane; composes an invocation's speech text, which Aetheria's player and C# brains do themselves |
| `elaboration.rs` | model elaboration lane |
| `consumer.rs` | consumer patch admission over HTTP-shaped ports; speech only in its fixtures |
| `table.rs` | encodes actor calls as model tools |
| `vault.rs` | evidence source over a vault |
| `local_inference.rs`, `sdk_inference.rs` | model backends (tokio, reqwest, codex-connector) |
| `tool_schema.rs` | tool schemas for model calls |
| `aetheria.rs`, `aetheria/` | Ghostlight's Aetheria catalog reader; Aetheria content is read by Aetheria |
| `verse/` | one example world; its speech affordances are a pattern to copy into Aetheria content, not a kernel dependency |
| `tests/external_admission.rs` | the cross-crate proof that forged, serde-minted opportunities and ids are refused at the public surface; dropped with the consumer lane it drove. AL4 must carry an equivalent proof against the ABI's own surface |
| crates `ghostlight-persona-projection`, `ghostlight-verse`; `sidecar/` | outside the synchronous kernel |

Speech as world action stays whole: affordance declarations with `carries_speech`,
preconditions, audience, effect slots and outcome bands, `Communicate` and
`Display`, `Knowledge` and `Told`, commitments (`patch.rs`); exercise, role
binding, preconditions, audience, band selection and lowering (`action.rs`);
`DecisionInvocation`, `DecisionEvent` and `ResolvedOp::Communicate` apply
(`lib.rs`); the `SpeakRenderPath` check (`grammar.rs`).

## Dependency pin

The crate no longer pins CultLib: `cultcache-rs` (Ghostlight's rev
`85f7024a35ea42cf9f0a0199a064526427150191`) left the manifest in AL3a with the
redb store it served.

## Pulling a later Ghostlight fix

By hand, against this file: read the upstream diff for the taken files, apply it
here, and record it in this file with its upstream commit and the files it
touched. A fix that lands in a left-behind file is not pulled.

## Cut AL2a: the model-guidance lanes

Authority: cut `aetherlight-cut-agency-a` r1 of campaign `aetheria-release`;
rulings `aetherlight-narrative-state-owner` and `dialogue-is-world-action`.
Deleted, with their c43eb54 homes:

- Lens (`src/lens.rs`, 361 lines): `Lens`, `LensWeights`; the genesis input,
  `WorldState`, snapshot and creation field `lens_weights`;
  `CommandBody::SetLensWeights`; `WorldEffect::LensWeightsReplaced`;
  `Mismatch::LensWeightsNeverDraw`; the draw-rule check in `verify_state_shape`.
- Cover and agency graph (`src/cover.rs`, 1105 lines): `Cover`, `Cell`,
  `CoverBudget`, `derive_cover`, `AgencyGraph`; `WorldKernel::agency_graph` and
  `fn agency_graph` (`lib.rs`); `CommandId::for_cell`, `for_cell_constituent`.
- Persona material (`lib.rs`, `patch.rs`): `PersonaMaterial`, `PersonaReadRef`,
  `ComponentOp::SetPersonaMaterial` and `ResolvedOp::SetPersonaMaterial` with
  resolution, apply and confinement arms, `WorldState.persona_material`, the
  subject snapshot's `material`, `WorldPatch::sets_persona_of_existing_subject`.
  The op set goes from 34 to 33.
- Brief: the `brief` string in `CreateWorld`, `CreateWorldIntent`, `WorldState`,
  `WorldEffect::WorldCreated`, the snapshot and the shape checks.
- Elaborator lane: `SystemCapability::Elaborator`, `PatchAnswer`,
  `CausalBoundary`, `BoundaryDigest` and `derive_boundaries`, `exact_boundary`,
  `require_answer` (the `AnswerRequired` gate), `jurisdiction_deficit`,
  `ground_covers`, `place_in`, `PatchGround::Jurisdiction`; `WorldScaleIntent`,
  `WorldScaleIntentRef`, `JurisdictionKey`, `ScaleDeficitRow`, `qualifies`,
  `derive_scale_deficit`, the subject snapshot's `qualified`, the snapshot's
  `boundaries` and `scale_deficit`, `CreateWorld.scale_intent`,
  `CreateWorldIntent.targets` and `jurisdictions`, `CreateJurisdictionIntent`,
  `WorldState.scale_intent`; `CommandBody::AdmitPatch.answers` and
  `WorldEffect::PatchAdmitted.answers`; `KernelError::AnswerRequired`,
  `AnswerNotDerived`, `AnswerNotSatisfied`; `Mismatch::UnknownJurisdictionRoot`
  and `ScaleWeightsExceedWhole`. `resolve_patch` loses its intent parameter.
- The golden replay `grammar::tests::existing_world_reopens_with_unchanged_digests`
  and the recorded-world fixtures under `tests/fixtures`: removing state fields changed the state
  shape, so Ghostlight's recorded world no longer decodes. AL4 adds
  Aetherlight's own golden.

Behaviour with the gate gone: `require_play_active` keeps the one rule
`require_answer` also held (`Play` authors nothing in Draft) and states the other
as a plain rule: in Active, declaring (new structure or evidence) is `Play`'s act
alone, and the owner is refused. A confined author's only remaining check is
`confine_to_ground`, which now serves the consumer ground alone.

AL4's ABI must route the host through typed commands (the sim reports events,
scripts propose), never through owner declarations in Active: under
`aetherlight-narrative-state-owner` an owner declaring in Active would be a second
door beside that path.

Kept untouched for AL2b: controllers and `ControllerMode`, consumers
(`ConsumerId`, `SystemCapability::Consumer`, `PatchGround::Consumer`,
`bound_to_consumer`), the draft phase and approvals, `VerifiedPrincipalEvidence`.
(Opportunities, scope digests, the attention order, `ExerciseDecision` and
`DeclineDecision` went in AL2b-1, below.)

## Leave-set vocabulary still compiled (AL2b's and AL4's triage list)

`#![allow(dead_code)]` still hides the dead code of a crate with no entry point.
What the cut deletes is gone; what remains is part B's:

- Authenticated-caller vocabulary: `AuthenticatedCaller::fixture`,
  `verified_principal` and `verified_play`; id helpers `text` and `nil_for_test`;
  `commits_for_test`; grammar `verbs`. AL4's ABI
  decides which of these it needs.
- Patch ingress: `decode_patch`, `check_patch_caps`, `PatchDecodeError` and
  `MAX_PATCH_BYTES`, `MAX_PATCH_DECLARATIONS`, `MAX_PATCH_OPERATIONS`,
  `MAX_PATCH_EVIDENCE`; the consumer ingress and elaboration lane read them, and
  nothing here does. They stay: AL4's ABI decides whether it decodes patches
  itself.
- Mailbox and vault comments (`WorldMailbox`, `vault:` evidence strings).

Reproduce the list with `RUSTFLAGS=--force-warn dead_code cargo test --lib --no-run`:
under test configuration the items nothing exercises show up.

## Cut AL2b-1: Play's Act replaces opportunity-based exercise

Base `bf92a054f`. An act is one command, `Act { actor, invocation }`, admitted for
`System(Play)` in Active only, and one effect, `Acted { invocation, event }`.
`require_granted(state, actor, affordance)` is the only constructor of
`GrantedAffordance`: the actor's `affordance_grants` entry holds the affordance
and the catalog defines it, otherwise `AffordanceDenied`. `Retire` removes a
subject's grants, so a retired actor is refused by that check alone.
`action::exercise` takes the actor in place of an opportunity; its pipeline is
unchanged and the band draw keeps `command_id` as its only per-command term.
`DecisionEvent` carries `actor`, not `scope` and `controller_id`. `STATE_SCHEMA`
and `COMMIT_SCHEMA` moved to `consumer.v8`.

Deleted, with their `bf92a054f` locations in `lib.rs`:

- `ScopeDigest`, `ScopePreimage`, `DecisionOpportunity` (459-506) and
  `scope_digest`, `exact_opportunity`, `derive_opportunities`,
  `order_opportunities`, `pressure_total`, `attention_debt`, `granted_entries`
  (3841-4034). `WorldSnapshot.opportunities`, `WorldState.last_opportunity_at` and
  the attention-stamp shape check in `journal.rs` (730-738).
- `CommandBody::ExerciseDecision` and `DeclineDecision`, `WorldEffect::
  DecisionExercised` and `DecisionDeclined`, their `reduce` and `apply_effect`
  arms, and `KernelError::OpportunityMismatch`, `ScopeChanged`,
  `ControllerMismatch`.
- The operator log: `operator_log`, `OperatorEvent`, `BoundRole` (3518-3579,
  1109-1134), and the kernel readers `opportunities`, `clock_advances`,
  `controller_receipt`, `controller_decline_receipt`.
- `WorldJournal::commits` and `FictionalMinutes::since`, dead once the readers
  above went.

Kept: the whole dialogue lane (`carries_speech`, `Communicate`, `Display`, the
kernel Speak entry, the grammar, `preconditions_hold`). `DecisionScope` stays the
grant key and `ControllerAssignment` stays; the owner's `AdmitPatch` and
`AdvanceTime` doors and `SystemCapability::Clock` stay.

AL2b-2a (below) took the writers; AL2b-2b still owns the controller half under
`aetherlight-play-only-after-genesis`: `ControllerAssignment` (which until then
holds only the `Retired` flag and unread identity data), `NewController`,
`ControllerId`, `ControllerMode`, `DecisionScope` as the grant key,
`SubjectState.retired`, `Mismatch::UnadmittedController`, the `admits_human`
rename and the snapshot's controller fields.

`last_acted_at` and `minted_at` in `snapshot` still read `state.events` (by
`event.actor`). AL3 needs keyed rows instead of a scan; that is the AL3 cut's.

## Cut AL2b-2a: after genesis only Play writes

Base `f51275c61`. A world is born Active: the genesis commit is the owner's
only act, and `WorldState::empty` has no phase and no approvals. After it
`CallerId::Play` is the only caller any command body admits. `CallerId` is
`{ Principal, Play }`; `Principal` survives as the genesis creator (and as the
refused caller of every post-genesis body). One rule, `require_play`, decides it
at the top of `reduce` and again at the top of `apply_effect`, so it holds on
commit and at replay; the journal gains no caller rule.
`require_ruler` is `forbid_rulings_at_genesis`: genesis may not rule or mint,
Play may. A grant row exists if and only if the subject holds a verb; a subject
may be declared with none, and revoking the last verb removes the row.
`GrantsOutsideControl` is `GrantsToRetired`. `STATE_SCHEMA` and `COMMIT_SCHEMA`
moved to `consumer.v9`.

Deleted, with their `f51275c61` locations in `lib.rs` unless noted:

- The Clock and Consumer capabilities and `CallerId::Controller`:
  `SystemCapability` (388-406), `AuthenticatedCaller::verified_controller` (433),
  `require_system_capability`, `require_clock_caller`, `require_play_active`,
  `require_owner` and `require_patch_author` (1456-1514, 3594-3625, 3989-4006),
  and `apply_effect`'s capability table.
- The consumer lane: `ConsumerId` and its namespace (317-338), `PatchGround`
  (408-419), `NewController::External`, `ControllerAssignment::
  ExternallyControlled`, `expected_caller`, `consumer()`, `bound_to_consumer`,
  `candidate_places`, `confine_to_ground` (3628-3792), `operation_ground`
  (3826-3987), and in `patch.rs` the mirror and human candidate sets,
  `Mismatch::OutsideJurisdiction`, `RetiresAMirror`, `ControllerGrantMismatch`.
- The draft phase: `WorldPhase`, `WorldState.phase` and `draft_approvals`,
  `CommandBody::ApproveDraft` and `ActivateWorld`, `WorldEffect::DraftApproved`
  and `WorldActivated`, `required_approvers`, `KernelError::WrongPhase`,
  `NotDraftApprover`, `DraftAlreadyApproved`, `MissingApprovals`, the snapshot's
  `phase`, `draft_approvals` and `required_approvers`, `Mismatch::RetiresAnApprover`,
  and the Draft-only shape clause in `journal.rs`.
- The never-mute rule and the controller/grant pairing: in `admit_resolved`, the
  resolver, `Mismatch::WouldLeaveSubjectMute`, and the shape clauses in
  `verify_state_shape` that required a grant row per controlled scope.
- `CommandId::derived`, whose only caller was `ConsumerId::of_name`.
- `VerifiedPrincipalEvidence` (106-146), `CreateWorldIntent` (366-376) and
  `PrincipalCommandIntent` (521-529). The `valid_until` enforcement that
  Ghostlight's mailbox did is no longer an AL4 obligation: the type that carried
  it is gone.

Kept: the whole dialogue lane, `decode_patch`, `check_patch_caps`,
`PatchDecodeError` and the `MAX_PATCH_*` constants (AL4's ABI decides whether
it decodes patches itself), the `ghostlight.*` schema namespaces (AL3 renames
them), `Retire`, `GrantAffordance` and `RevokeAffordance` as Play's patch
operations, and `#![allow(dead_code)]`.

Nothing decides with a controller after this cut. `ControllerAssignment`,
`NewController::{Human, NarrativePersona, OperationalAgent}`, `ControllerId`,
`ControllerMode`, `DecisionScope` as the grant key, `validate_assignment`,
`Mismatch::UnadmittedController` and `admits_human` (the genesis predicate three
places read) stay compiled for AL2b-2b, which removes the controllers and
renames the predicate.

`last_acted_at` and `minted_at` in `snapshot` still read `state.events`; the
count of code readers of `state.events` did not rise (idea
`cutspecs-1119.al3-needs-keyed-rows`).

## Obligations this cut hands to AL4

- A cross-crate proof that forged opportunities and ids are refused, equivalent to
  the dropped `tests/external_admission.rs`, against the ABI's surface.
- Removal of `#![allow(dead_code)]`.

## AL2b-2b: controllers go, retirement on the subject

The subject controller is gone: `ControllerAssignment`, `ControllerId`,
`ControllerMode`, `NewController`, `DecisionScope`, `SubjectDeclaration.controller`,
`Mismatch::UnadmittedController`, `validate_assignment`, the snapshot's controller
fields and `ControllerId::issue`. Retirement is `SubjectState.retired`, and
`is_retired` is its only statement. `affordance_grants` is keyed by `SubjectId`.
`admits_human` is `is_genesis`: it says what the from-nothing lane may build (the
kernel Speak entry and the first subjects), not who may be bound. The schema is
`consumer.v10`; v9 rows are refused at the schema gate, with no migration.

Not this cut's, and kept: the channel controller (`ChannelRecord.controller`,
`SetController`, `resolve_controller`), which is dialogue
(`dialogue-is-world-action`). An earlier version of this file listed
`resolve_controller` and `controller_key` as AL2b-2b's; that was wrong.

## Cut AL3a: the journal is in memory; ids are derived

Authority: cut `aetherlight-journal-a` r1 of campaign `aetheria-release`; rulings
`aetherlight-narrative-state-owner`, `cultnet-scale-is-not-negotiable`,
`dialogue-is-world-action`. Base `09a1c3256` (spec base `39c50acfc`; no `native/`
change between them).

Ids. `CommandEnvelope::id()` is the first 16 bytes of sha256 over
`aetherlight.command.v1`, the world id, the big-endian expected revision and the
named messagepack of caller and body. `CreateWorld::id()` is the same over
`aetherlight.create.v1` and the whole creation. `WorldId::of(creation id)` is the
same over `aetherlight.world.v1`; a genesis commit whose world id is not that is
refused at open. The `id` fields on `CreateWorld` and `CommandEnvelope`,
`CommandId::new`/`parse_uuid` and `WorldId::issue` are gone; `CommandId::issue`
is a test fixture. Retry identity went with them: `SubmitReceipt` collapsed into
`CommitReceipt`, and a resubmitted applied command is a `RevisionMismatch`.
`WorldCommit.committed_at` is gone: wall time is the C# store's `stored_at`.

Journal. `WorldJournal { commits }` is the only record of a world. `export()` is
canonical named messagepack of `{ schema: aetherlight.journal.v1, commits }`;
`open(bytes)` decodes canonically and replays through `verify_history`, which
reads the revision-ordered slice and returns the head state it derives. There is
no state row, no expected world id, no health or ownership check and no
`verify_append`. `STATE_SCHEMA` and `COMMIT_SCHEMA` are
`aetherlight.world_state.v1` and `aetherlight.world_commit.v1`; any `ghostlight.*`
schema is refused at the schema gate and at the commit chain.

Dependencies. chrono, cultcache-rs (and with it redb) and tempfile left the
manifest; uuid keeps `serde` in `[dependencies]` and `v4` only in
`[dev-dependencies]`, so no shipped path draws a random id (`cargo build --lib`
compiles without it).

Not taken from the spec's text: the spec's negative pattern `id: CommandId`
matches the `command_id: CommandId` parameters of the band draw and derivation
(the per-command term the spec keeps); the doc comment said `synchronous`, which
`chrono` matches, and now says `single-threaded`.

Host contracts (AL3a, from Soul's pass):

- Two byte-equal creations share a `WorldId` and every id under it. A host that
  wants distinct worlds salts each run or profile into the creation, for example
  a run seed in the patch.
- Truncation and rollback open: a chain verifies as a prefix, so an export with
  its tail dropped, or an older export, opens as a valid shorter world. The
  holder of the bytes keeps the head (revision and state digest from `snapshot()`
  at export time) and compares after `open`. AL5's C# store owns this.
- The band draw is grindable through the command id (it hashes the body, so the
  text, a magnitude or the revision re-roll it). Only Play submits acts after
  genesis and Play is trusted, so it is not an authority hole.

## Cut AL3b: partitions are tracked documents, the state digest is a set digest

Authority: cut `aetherlight-journal-b` r1 of campaign `aetheria-release`; rulings
`aetherlight-narrative-state-owner`, `cultnet-scale-is-not-negotiable`,
`terminus-clockwork-world`. Base `36d1d2507`.

Tracked documents. Each partition of `WorldState` is a `Docs<K, V>` (`docs.rs`):
a map whose every write method records the key's value as it stood when the
transaction first touched it. `finish` reports the keys whose value really
changed with their old value; `rollback` puts the touched keys back. The world's
own fields (id, owner, title, grammar, clock) are one `Tracked<WorldHeader>`.
`revision`, the accumulator, `state_digest` and `last_commit_digest` are the
chain head, outside every document. `submit` checks, reduces, opens a
transaction, applies to the live state, then folds the change set and seals the
commit; any error before the commit is sealed rolls the state back. There is no
candidate clone, and replay uses the same begin, apply, finish path, so opening
a world costs what its commits touched.

Set digest. A document digest is sha256 over `u64be len(schema)`, schema,
`u64be len(key)`, key and the named messagepack of the value. The accumulator is
LtHash16 (Lewi et al. 2019): a document digest is expanded to 2048 bytes by
sha256(digest, block) for blocks 0 to 63, read as 1024 little-endian `u16`
lanes, and added (or subtracted) with wrapping. An updated document subtracts
its old digest and adds its new one. `state_digest` is `sha256:` plus the hex of
sha256 over `aetherlight.world_state.v2`, `u64be revision` and the lanes. It
depends on the set of documents alone, never on write order.

Document names are `aetherlight.<name>.v1` for world, subject, entity, edge,
position, holdings, dependencies, fact, channel, knowledge, authority,
selection, redress, commitments, pressures, affordance and affordance_grants.
Keys are the id's uuid text; redress is keyed by the grievance name and knowledge
by `subject/fact`. These schema ids exist only as digest input.

Schemas. `aetherlight.world_state.v2` and `aetherlight.world_commit.v2`. An
export whose commits carry v1 is refused. Every state digest, every commit
digest and the export hash moved; `fixtures/golden.digests` was regenerated and
reproduces on Linux.

Events and knowledge. `WorldState` no longer holds events: an event lives once,
in the `Acted` effect of the commit that applied it. `snapshot` folds
`minted_at` and `last_acted_at` over the journal (still one O(history) pass; an
index is the follow-up's). `verify_state_shape` no longer judges events; replay
re-derives each through `action::exercise` in `apply_effect`. Knowledge is one
document per (subject, fact), so a subject's knowledge is a range over the key
and forgetting removes one key. `WorldState` has no `Serialize` or `Deserialize`:
only documents are encoded, one at a time.

The change set is transient here. AL3c records it through the CultLib type.

Timing. The ratio of 1600 to 100 persons is not gated by this cut: the world
reads a commit makes (`audience`, `derive_motion`, the snapshot) are still
O(world), and follow-up `aetherlight-read-indexes-timing-gate` owns the gate. What
this cut owns is the work it does for the digest, which
`golden_tests::digest_work_is_equal_at_100_and_1600` pins: document digests per
commit equal at both sizes and equal to Added + Removed + 2 x Updated.

## Cut AL3b-2: partition read indexes, and the read-work gate

Authority: cut `aetherlight-read-indexes` r1 of campaign `aetheria-release`;
rulings `aetherlight-narrative-state-owner`, `cultnet-scale-is-not-negotiable`,
`terminus-clockwork-world`. Base `576605c7b`. No digested byte moves:
`fixtures/golden.digests` is identical at every commit of the cut.

Read indexes are derived state. A `Docs` may carry a `Projection`, a pure function
of one document's value (`DueAt`, `StandsAt`, `ChildOf`, `BySource`, `ByEndpoint`,
`ByController`, `ByIncumbent`, in `lib.rs` beside `world_state!`). The index is the
projection of the map as of the last `finish`, folded by `Docs::finish` from the
same changed-key list the digest folds. It is not a partition: not digested, not
encoded, ignored by equality, never exported. Opening a world builds it by replay,
since genesis and every replayed commit close through `finish`; there is no rebuild
path. `Docs::indexed(range)` returns (point, key) in order and is corrected by the
open log (a logged key's committed entries are dropped and its current points are
read from the map), so it is true mid-transaction; `rollback` never touches the
index because the index never saw the transaction. `Docs` no longer derefs to its
map: every read is an inherent method, so no reader can step around the counter.

Read work is counted where reads are owned. In test builds `docs::rows_read()` is
the number of rows any `Docs` iterator has yielded plus the entries every `indexed`
call returned. `golden_tests::read_work_is_equal_at_100_and_1600` asserts that a
tick (two routines rolled, two obligations pressed) and a colocated speech read
the same number of rows at 100 and 1600 persons; `operation_reads_are_equal_at_100_and_1600`
does the same for the reads no submit shape reaches (a two-level `under_place`, a
`DischargeCommitment` among N pressure rows, one incumbent's `delegated_authority`,
a three-route `reachable`). Wall time is reported by `clockwork_commit_cost` and
asserted nowhere: it is noise at this scale. The gate world holds a route, a
`Reach::Place` channel and an institution with an office per place, because an
empty partition hides its scan.

Eight scans became index reads: `under_place`, `audience` (colocated), the pressure
holders of `DischargeCommitment`, `scope_components`' routes and controls,
`delegated_authority`, `derive_motion`'s commitment walk and `reachable`'s outbound
edges. Each old scan is kept as a `cfg(test)` `*_by_scan` reference, and
`index_reads_equal_reference_scans` compares them over every revision of the golden
session, the populated worlds and forty clockwork commits. `indexes_are_derived`
rebuilds every index from its map after each commit, after reopen and after each
failed apply; `a_corrupted_index_is_detected` damages each index both ways.

Reads that still scan, each with its owner: `snapshot` (the host's whole-world
projection, never on the commit path); the resolver's reads of the whole world in
`AdmitPatch` (`aetherlight-resolver-reads-world`). The
"Timing" paragraph of AL3b above is superseded: the gate it deferred is
`read_work_is_equal_at_100_and_1600`.

## Cut AL3d-2: cross-partition views

Authority: cut `aetherlight-cross-partition-views` r1 of campaign
`aetheria-release`; rulings `cultnet-scale-is-not-negotiable`,
`aetherlight-narrative-state-owner`, `terminus-clockwork-world`,
`dialogue-is-world-action`. Base `609b3ba5e`. No digested byte moves:
`fixtures/golden.digests` is identical at every commit of the cut.

Two questions the clock asks are joins, so no one partition's index answers them
(a `Docs` projection is a pure function of one document). Whether a dependency is
failing joins `dependencies` with `edges`, `holdings` and `positions`, and two of
its arms are anti-joins (a resource not held, a subject not placed); whether a due
commitment is live joins `commitments` with `subjects.retired`. `views.rs` owns
both as `Views { live_due, failing }`: `live_due` is the subjects with a
commitment due at each minute, retired subjects excluded, and `failing` is the
(dependent, target) pairs failing a live dependent.

`Views` is derived state, a sibling of the `Docs` indexes and not a projection: a
field of `WorldState` outside `partitions()`, so not digested, not encoded and
never exported; its `PartialEq` is always true, so equality is the documents'.
Its one writer is `Views::fold`, called by `WorldState::finish` before the logs
close, from the `changes()` of the six partitions the views join. A subject whose
commitments or retirement changed loses its due points and regains them if it is
live; a subject whose dependencies, holdings or retirement changed has its
failing rows re-judged; a changed route or placement re-judges its dependents,
which the new `DependsOn` index on `dependencies` names. Rollback never touches
it, because the view never saw the transaction; genesis and every replayed
commit close through the same `finish`, so open rebuilds it by replay.

`derive_motion` reads the views only with clean logs (`debug_assert` on the six
partitions; both callers satisfy it: `reduce`, and `apply_effect`'s
`TimeAdvanced` arm before its first write). `DueAt` is deleted, so the due
question has one owner, and `motion_of` no longer walks every dependency row or
visits retired subjects' past-due routines. Motion is byte-identical. The old
walks are `cfg(test)`: `derive_motion_by_scan` over every promisor and every
dependency, and `Views::rebuild`. A test that forges partitions and then calls
`derive_motion` must close its writes through `finish`; a forged state that never
finishes has a stale view by design, the same contract as a `Docs` index.

Oracles: `views_are_derived` (the view is the rebuild after every walked commit,
reopen and failed apply, over a scenario world that closes and reopens a route,
empties and refills a holding, removes a depended-on subject's position, releases
a dependency and retires a subject holding past-due routines and a failing
dependency), `view_reads_equal_reference_scans`, `a_corrupted_view_is_detected`
and a debug refusal of a read with an open log. View reads are counted where they
are owned, like `Docs` reads, so the gate sees them.

The gate world grows with them: a dependency per person (to the previous live
person, so each stays satisfied), two failing (persons 0 and 1 on the last
place's grain), a forum per place, every routine checking that its person holds
their place's grain, every tenth person from the 35th retired, and the clock
moved one period so every routine is past due. The fixed-count pins on
`dependencies` and `redress` are gone; `read_work_is_equal_at_100_and_1600` now
asserts the population at scale.

Soul pass 1 (fix batch). The fold's moved-targets arm reads the dependencies
`DependsOn` index through `Docs::indexed_unchanged`, which names the committed
entries of keys the open log did not change and does not walk the log; a changed
dependencies key is already re-judged whole, so a bulk commit (genesis, every
replayed commit at open) folds in rows proportional to what it changed
(`folding_a_bulk_commit_reads_rows_in_proportion_to_the_change`). The clean-log
check on a view read is an `assert!`, not a `debug_assert!`: six flag tests, held
in every build. `Release` now clears the pressure its dependency raised through
`set_pressure`; before, a pressed dependency's pressure row outlived it and the
export failed to reopen (pre-existing at `609b3ba5e`). A seeded differential test
(`views_equal_their_rebuild_and_the_scans_over_random_commands`) holds the views
to their rebuild and the motion to the scans over random bind, release, route,
holding, relocate, retire, tick, speech and refused commands, reopening every 50.

Not defended by the gate, on purpose: a tick's cost is linear in the number of
failing dependencies, because each failing dependency presses a pressure row each
tick. The gate world pins failing at two, so growth in failing is not seen; that
cost is inherent to pressing and is not a read of the world. `open` of a large
world is superlinear in rows from readers outside this cut (the resolver's reads
of the whole world at genesis, owner `aetherlight-resolver-reads-world`);
`open_cost` reports it.

## Cut AL3d-1: the patch lane reads what the patch touches

Authority: cut `aetherlight-resolver-reads-world` r1 of campaign
`aetheria-release`; rulings `cultnet-scale-is-not-negotiable`,
`aetherlight-narrative-state-owner`, `terminus-clockwork-world`,
`dialogue-is-world-action`. Base `609b3ba5e`. No digested byte moves:
`fixtures/golden.digests` is identical at every commit of the cut.

`resolve_patch` used to seed fifteen candidate maps by copying every partition,
collect every catalog kind and every subject's import key into sets, and re-judge
every holder for overlap after each widening operation. Its reads grew with the
world: 936, 3696 and 14736 rows for one `DischargeCommitment` at 100, 400 and 1600
persons, and 14,000 or more for every other shape. It now reads what the patch
touches.

`patch/candidate.rs` owns the candidate graph of one resolution. Each candidate map
is an `Overlay`: the committed partition read through a base function (a point get;
a `Key::Draft` key has none), and the patch's writes in a local map. `Candidate`
holds the fifteen overlays and the named reads, each bounded by what the patch
wrote plus what it returns: `occupants_under` (`under_place` over `ChildOf` and
`StandsAt`, corrected by the written positions), `offices_of` and `offices_held_by`
(the committed row or `ByIncumbent`, corrected by the written offices), `kind_taken`
(`ByKind` on the catalog) and `import_key_held` (`ByImportKey` on the subjects),
`custody` (the sums over the written holders only: every other holder is on both
sides of the equation and cancels). `authority`, `selection` and `retired` are
private to the module: a write goes through `set_grants`, `set_office` or `retire`,
which re-judge the holders whose effective authority it can change (the holder and
an institution's incumbents; an office's old and new incumbent) and keep
`overlapping`, the set of holders with two grants of one kind over overlapping
ground. A widening operation is `OverlappingJurisdiction` exactly when the set is
non-empty. That is what `graph_overlaps` answered, because the committed world is
disjoint (every commit checks it and opening a world verifies it), so only a holder
the patch touched can enter the set. The mismatch list is unchanged, including the
later widening operations after a first overlap and a revoke that clears it
(`overlap_mismatches_are_unchanged`). The candidate lives for one `resolve_patch`
call and is never written back; `admit_resolved` stays the only writer of the
ontology partitions.

`apply_operations` derives its two checks from the transaction's log. Conservation
sums the holders `holdings` logged as changed, each from its value when the
operations began (so a holder already written by the declarations above it is
measured from there). Disjointness judges only the holders whose grants, offices or
institution the transaction touched, through `holder_overlaps`, which
`overlapping_holder` also calls for the open-time whole-world check
(`verify_state_shape`, whole-world on purpose). `resource_total` survives only as
the test reference `resource_total_by_scan`.

Two partitions gained read indexes, `subjects` by `ByImportKey` and
`affordance_catalog` by `ByKind`, so `held_import_keys` and the per-patch kind set
are gone and `import_key_fault` asks a predicate.

The seeding block, `graph_overlaps`, the old effective-authority walk and
`key_covers_place` live on only as `cfg(test)` references (`Materialized`, whose
`seeded` is the old block verbatim). `candidate_reads_equal_materialized_scans`
walks a civic world through the shapes that move each read (a subject leaving and
entering a subtree, a declared place and subject, a retirement, a vacated and
refilled office, an overlapping grant and its revocation, the retirement of the
lending institution, a declared institution, custody written both ways) and holds
every named read and the overlap set to the scan after every step;
`an_untouched_overlay_reads_what_the_seeding_copied` holds every base function to
the copy in each populated world. The gate is
`golden_tests::patch_read_work_is_equal_at_100_and_1600`: ten patch shapes through
`AdmitPatch` read the same rows at 100, 400 and 1600 persons (at most 21 each),
and `apply_checks_read_the_log` pins the commit lane's two checks. The
`DischargeCommitment` case of `operation_reads_are_equal_at_100_and_1600`, which
called `apply_operation` directly, moved into the gate.

Reads that still scan: `snapshot` (follow-up `aetherlight-snapshot-history-fold`)
and `verify_state_shape` at open, by design.
