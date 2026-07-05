# Aetheria Eve Runtime

This package mounts provider-owned Eve surfaces from Aetheria's typed
CultCache state into Unity UI Toolkit.

`AetheriaEveSurfacePresenter` is a typed surface document lowerer:

- it resolves the active client target from `GameData/aetheria-client.cc`;
- it reads `gamecult.eve.surface.v1` from the selected typed state source, which
  is currently still a local `.cc` file transport;
- it lowers the retained tree through `org.gamecult.eve.unity-uitoolkit`;
- it submits renderer-emitted commands as typed `gamecult.eve.command.v1` state
  records for Aetheria's provider-owned command bridge.

`AetheriaEveRuntimeBootstrap` mounts the first runtime surface after scene load.
By default it creates a `UIDocument` host for `aetheria.game`, the daemon-owned
game surface, so Unity starts as a lowering surface for the daemon Verse member
without a hand-wired scene object. Set `AETHERIA_EVE_SURFACE_ID` to mount a
different daemon or provider surface such as `aetheria.game.tui`,
`aetheria.daemon.editor`, or `aetheria.daemon.editor.tui`; set
`AETHERIA_STATE_PATH` to override the selected local `.cc` state file, or disable the automatic mount with
`AETHERIA_DISABLE_EVE_RUNTIME_BOOTSTRAP=true` or
`--aetheria-disable-eve-runtime-bootstrap`. Batchmode disables the bootstrap so
compile and smoke gates do not accidentally create renderer state.

Nested CultUI regions are expressed as `embeddedDocuments` / `surface.slot`
entries on the retained surface component. Aetheria mirrors Eve's
`EveEmbeddedDocumentSlot` as `AetheriaRuntimeEmbeddedDocumentSlot`, and the
Unity host resolves those child documents through the same managed CultMesh
state handles as the parent surface. Use this for daemon-owned composite UI
such as inventory panels with dropdown child surfaces; do not rebuild the child
state as a Unity-only model, projector, or drag/drop workaround.

Discovery starts from Eve's `web/fixtures/cultui-embedded-surface.json` fixture
and `docs/parity-testing-harness.md` runtime matrix. Keep Unity covered by the
Aetheria state verifier and `verify-stage7d-unity-parity.ps1`; keep the shared
CultUI contract covered by Eve's browser DSL test, parity harness, and Flutter
parity smoke.

Commands are surfaced as `gamecult.eve.command.v1` requests. The presenter does
not accept or apply them locally; provider acceptance still belongs to the
CultMesh command bridge. Renderer-local command effects would be the wrong
organ wearing a clean shirt.
