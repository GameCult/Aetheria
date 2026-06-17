# Aetheria Eve Runtime

This package mounts provider-owned Eve surfaces from Aetheria's typed
CultCache state into Unity UI Toolkit.

`AetheriaEveSurfacePresenter` is a projection bridge:

- it resolves the active client target from `GameData/aetheria-client.cc`;
- it reads `gamecult.eve.surface.v1` from the selected typed state source, which
  is currently still a local `.cc` file transport;
- it lowers the retained tree through `org.gamecult.eve.unity-uitoolkit`;
- it queues renderer-emitted commands as typed `.cc.eve.pending` command
  envelopes for Aetheria's provider-owned command bridge.

`AetheriaEveRuntimeBootstrap` mounts the first runtime surface after scene load.
By default it creates a `UIDocument` host for `aetheria.operations`, so the
provider-owned operations surface is present at runtime without a hand-wired
scene object. Set `AETHERIA_EVE_SURFACE_ID` to mount a different surface, set
`AETHERIA_STATE_PATH` to override the selected local `.cc` state file (`AETHERIA_EVE_STATE_PATH`
still works as a legacy fallback), or disable the automatic mount with
`AETHERIA_DISABLE_EVE_RUNTIME_BOOTSTRAP=true` or
`--aetheria-disable-eve-runtime-bootstrap`. Batchmode disables the bootstrap so
compile and smoke gates do not accidentally create renderer state.

Commands are surfaced as `gamecult.eve.command.v1` requests. The presenter does
not accept or apply them locally; provider acceptance still belongs to the
CultMesh command bridge. Renderer-local command effects would be the wrong
organ wearing a clean shirt.
