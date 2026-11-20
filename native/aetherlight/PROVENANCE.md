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
