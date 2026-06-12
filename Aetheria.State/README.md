# Aetheria.State

`Aetheria.State` is the replacement owner for durable Aetheria game state.

It uses modern `GameCult.Caching`, `GameCult.Caching.MessagePack`, and
`GameCult.Mesh`. It does not reference the old `Assets/Scripts/ServerShared`
tree, RethinkDB, JsonKnownTypes, or Newtonsoft.Json.

The first live state file is `GameData/aetheria-world.cc`. Legacy files such as
`GameData/AetherDB.msgpack` are migration inputs only.

Current rebuild notes:

- `MessagePack` 3.1.4 is pulled transitively through CultLib and is currently
  flagged by NuGet advisory `GHSA-hv8m-jj95-wg3x`; update CultLib's
  `GameCult.Caching.MessagePack` package ownership rather than adding a local
  Aetheria override.
- `CultRecordRef<T>` should become the persisted reference shape once the
  MessagePack source-generator path handles the GameCult formatter cleanly.
  Until then, v1 profile documents store explicit record-key strings at the
  persistence boundary.
