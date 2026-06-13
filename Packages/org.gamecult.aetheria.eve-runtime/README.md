# Aetheria Eve Runtime

This package mounts provider-owned Eve surfaces from Aetheria's typed
CultCache state into Unity UI Toolkit.

`AetheriaEveSurfacePresenter` is a projection bridge:

- it reads `gamecult.eve.surface.v1` from `GameData/aetheria-world.cc`;
- it lowers the retained tree through `org.gamecult.eve.unity-uitoolkit`;
- it does not accept, mutate, or persist Aetheria state.

Commands are surfaced as `gamecult.eve.command.v1` requests and currently log a
capability gap until the CultMesh command bridge is wired. That gap is visible
on purpose; renderer-local command effects would be the wrong organ wearing a
clean shirt.
