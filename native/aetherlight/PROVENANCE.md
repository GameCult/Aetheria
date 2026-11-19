# Aetherlight provenance

Aetherlight is a fork of the synchronous world kernel of Ghostlight.

- Upstream: `GameCult/Ghostlight`, `crates/ghostlight/`
- Commit: `c43eb54962ed4a09cc737ecac73a16b0d7ce62b4` (origin/main)
- Taken: 2026-11-19
- Authority: cut `aetherlight-fork-import` r1 of campaign `aetheria-release`;
  rulings `aetherlight-narrative-state-owner` and `dialogue-is-world-action`.

Every file below was copied as a raw git blob (`git show`), never `git archive`,
and committed with `-text` (`.gitattributes`) so no checkout converts newlines.
Line ranges are in the c43eb54 file.

## Taken

| c43eb54 path (under `crates/ghostlight/`) | Here | Range | Edits |
| --- | --- | --- | --- |
| `src/lib.rs` | `src/lib.rs` | all | E1-E5 |
| `src/action.rs` | `src/action.rs` | all | none |
| `src/clock.rs` | `src/clock.rs` | all | none |
| `src/journal.rs` | `src/journal.rs` | all | none |
| `src/grammar.rs` | `src/grammar.rs` | all | none |
| `src/grammar/tests.rs` | `src/grammar/tests.rs` | all | none |
| `src/lens.rs` | `src/lens.rs` | all | E8, E9 |
| `src/cover.rs` | `src/cover.rs` | all | none |
| `src/import_key_tests.rs` | `src/import_key_tests.rs` | all | none |
| `src/patch.rs` | `src/patch.rs` | 1-6059 and 7906-9606 (dropped 6060-7905: the patch tool catalog, `PATCH_TOOLS`, `patch_tools()`, the `field_example` helpers, `mod catalog_tests`) | E6, E7 |
| `tests/fixtures/base_world.cc` | same | all | none |
| `tests/fixtures/base_world.digests` | same | all | none |
| `tests/fixtures/base_world.id` | same | all | none |
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

Not renamed: the `ghostlight.*` schema ids and namespaces in the code
(`STATE_SCHEMA`, `COMMIT_SCHEMA`, journal schema rows, `ghostlight.speech`,
`ghostlight.cover.v1`). They enter derived ids and digests; the golden replay
`grammar::tests::existing_world_reopens_with_unchanged_digests` proves them
unchanged. AL3 owns the format rename.

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
| `tests/external_admission.rs` | exercises the dropped consumer lane |
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
