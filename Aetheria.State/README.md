# Aetheria.State

`Aetheria.State` is the replacement owner for durable Aetheria game state.

It uses modern `GameCult.Caching`, `GameCult.Caching.MessagePack`, and
`GameCult.Mesh`. It does not reference the old `Assets/Scripts/ServerShared`
tree, RethinkDB, JsonKnownTypes, or Newtonsoft.Json.

The first live state file is `GameData/aetheria-world.cc`. Legacy files such as
`GameData/AetherDB.msgpack` are migration inputs only.

The typed save model is split into player settings, saved runs, saved zones,
entity snapshots, item slots, weapon groups, action-bar bindings, and stat
grids. Do not preserve `SavedGame` or `EntityPack` as opaque payloads in the
new store; they are source shapes for migration, not portable state authority.

The Unity client no longer writes `PlayerSettings.msgpack`, `.loadout`, or
`.zone` files. Until `Aetheria.State` is available to Unity as a runtime Verse
package, player settings and loadout edits are session-local and run saving is
disabled. That is intentional: the missing runtime package is the owner gap, and
the old bespoke file formats must not keep acting as durable truth while the
state spine is being rebuilt.

Keyboard layout rendering now reads the checked-in Unity text asset directly.
The old generated `GameData/KeyboardLayouts/*.msgpack` cache was deleted; layout
edits are runtime-only until Verse owns a typed layout/settings document.

The old IMGUI CultCache database editor has been deleted, and `NameTools` no
longer exports `NameFile/*.msgpack`. `AetherDB.msgpack` and existing name files
remain legacy catalog inputs only; typed catalog migration is the next owner
move.

Current rebuild notes:

- `MessagePack` 3.1.4 is pulled transitively through CultLib and is currently
  flagged by NuGet advisory `GHSA-hv8m-jj95-wg3x`; update CultLib's
  `GameCult.Caching.MessagePack` package ownership rather than adding a local
  Aetheria override.
- `CultRecordRef<T>` should become the persisted reference shape once the
  MessagePack source-generator path handles the GameCult formatter cleanly.
  Until then, v1 profile documents store explicit record-key strings at the
  persistence boundary.
