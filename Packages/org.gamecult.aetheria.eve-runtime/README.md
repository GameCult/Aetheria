# Aetheria Eve Runtime

This package mounts provider-owned Eve surfaces from Aetheria's typed
CultCache state into Unity UI Toolkit.

`AetheriaEveSurfacePresenter` is a projection bridge:

- it reads `gamecult.eve.surface.v1` from `GameData/aetheria-world.cc`;
- it lowers the retained tree through `org.gamecult.eve.unity-uitoolkit`;
- it queues renderer-emitted commands as typed `.cc.eve.pending` command
  envelopes for Aetheria's provider-owned command bridge.

Commands are surfaced as `gamecult.eve.command.v1` requests. The presenter does
not accept or apply them locally; provider acceptance still belongs to the
CultMesh command bridge. Renderer-local command effects would be the wrong
organ wearing a clean shirt.
