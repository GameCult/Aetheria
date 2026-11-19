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
kept (a first commit had one; its stated reason did not hold). `base_world.cc` is
binary and no newline rule touches it. Line ranges are in the c43eb54 file.

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
dependencies chrono, cultcache-rs, rmp-serde, serde, serde_json, sha2, thiserror,
unicode-normalization, unicode-properties, uuid; dev tempfile.

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

Not renamed: the `ghostlight.*` schema ids and namespaces in the code
(`STATE_SCHEMA`, `COMMIT_SCHEMA`, journal schema rows, `ghostlight.speech`). They enter derived ids and digests. No test pins them
since AL2a removed the golden replay (see below); AL4 adds Aetherlight's own
golden. AL3 owns the format rename.

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

`cultcache-rs` is pinned at Ghostlight's rev
`85f7024a35ea42cf9f0a0199a064526427150191`, the rev that wrote
`tests/fixtures/base_world.cc`, not Aetheria's C# CultLib pin. The fork
reproduces Ghostlight first; AL3 removes cultcache-rs.

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
  and `tests/fixtures/base_world.*`: removing state fields changed the state
  shape, so Ghostlight's recorded world no longer decodes. AL4 adds
  Aetherlight's own golden.

Behaviour that changed with the gate: with `require_answer` gone, the owner and
`Play` declare in Active with no answer, and a confined author's only remaining
check is `confine_to_ground`, which now serves the consumer ground alone.
`require_play_active` keeps the one rule `require_answer` also held: `Play`
authors nothing in Draft.

Kept untouched for AL2b: controllers and `ControllerMode`, consumers
(`ConsumerId`, `SystemCapability::Consumer`, `PatchGround::Consumer`,
`bound_to_consumer`), opportunities, scope digests, attention order, the draft
phase and approvals, `ExerciseDecision`, `DeclineDecision`,
`VerifiedPrincipalEvidence`.

## Leave-set vocabulary still compiled (AL2b's and AL4's triage list)

`#![allow(dead_code)]` still hides the dead code of a crate with no entry point.
What the cut deletes is gone; what remains is part B's:

- Consumer lane: `ConsumerId`, `CONSUMER_ID_NAMESPACE`, `bound_to_consumer`,
  `PatchGround::Consumer` and the consumer arm of `confine_to_ground`,
  `VerifiedPrincipalEvidence`.
- Controller lane: `verified_controller`, `resolve_controller`, `controller_mode`,
  `controller_receipt`, `controller_decline_receipt`, `controller_key`.
- Tick-driver readers `WorldKernel::opportunities` and `clock_advances`, and
  `CreateWorldIntent` (no consumer after the mailbox left).
- Mailbox and vault comments (`WorldMailbox`, `vault:` evidence strings).

Reproduce the list with `RUSTFLAGS=--force-warn dead_code cargo test --no-run`:
under test configuration the items nothing exercises show up.

## Obligations this cut hands to AL4

- A cross-crate proof that forged opportunities and ids are refused, equivalent to
  the dropped `tests/external_admission.rs`, against the ABI's surface.
- Enforcement of `VerifiedPrincipalEvidence::valid_until`, which Ghostlight's
  mailbox did and the fork does not.
- Removal of `#![allow(dead_code)]`.
