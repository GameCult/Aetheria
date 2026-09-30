# Aetheria agent doctrine

## Data tooling: AetherDb

`tools/AetherDb` is the agent's tool for reading and writing Aetheria's CultCache stores (`GameData/Aetheria.cc`,
`run.cc`, `player.cc`). The simulation lives in `Aetheria.Shared` (the headless build of
`Assets/Scripts/ServerShared`) and needs no Unity; Unity is only the shell for graphics and input.

- When a task needs a catalog or save capability AetherDb lacks (authoring or cloning designs, setting fields,
  fixtures for a play smoke, a query), add it to AetherDb as a command. Do not write a scratch console project,
  a loose `.cs` script, a Python or PowerShell reader of the `.cc` bytes, or a Unity editor script instead.
  Loose scripts were retired in `9f9149bd` because they ran against stale schemas.
- A command dry-runs by default and writes only with `apply`, validates with `CultRecordRefs.Validate` and
  commits in one `CultCache.Commit` batch, like the existing migrations in `tools/AetherDb/Program.cs`.
- Deep copies of records use CultCache's own options,
  `CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly)`; MessagePack's standard options
  cannot serialise `CultMath` types or `CultRecordRef`.
- The headless build refuses to compile unless CultLib is a clean checkout at the revisions pinned in
  `Directory.Build.props` (`Directory.Build.targets` enforces it). When `F:\Projects\CultLib` is elsewhere, point
  `CULTLIB_ROOT` and `CULTMATH_ROOT` at detached worktrees of those revisions; never move the shared checkout.
- CultCache Studio is the operator's GUI over the same files. It is not an agent path.
