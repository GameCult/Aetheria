# Eve UI Toolkit Lowering

This package lowers `gamecult.eve.surface.v1` retained surface documents into
Unity UI Toolkit `VisualElement` trees.

It owns native projection only. Providers still own truth, accepted state,
style token values, and command effects through CultMesh/CultNet. Unknown
component kinds degrade to inert containers instead of gaining local semantics.

This copy is staged in Aetheria because the neighboring Eve repository is dirty
on unrelated work. The intended upstream home is the Eve repository as an
importable Unity package.
