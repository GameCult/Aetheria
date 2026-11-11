# Element ids and the CultLib pin (cultlib-unity-v3.0.0)

CultLib 3.0.0 (Unity tag `cultlib-unity-v3.0.0`, commit `7225835d`, which is the headless pin in `Directory.Build.props`; CultMath `cultmath-unity-v0.5.1`, `f1edfa45`) gives every element of an object list an identity. Every
element type reachable through a list of a `[CultDocument]` type carries one string member with `[Key(n)]` and `[CultElementId]`;
the registry refuses to open otherwise, naming the type and list.

- **Where the slot goes.** A union base takes the member above its subclasses' highest key. `BehaviorData.Id` is key 37, so an
  array-format behavior is 38 slots on the wire (`PaintModeTests`, `ShipRigTests` pin it). A new `BehaviorData` subclass must keep
  its keys below 37 or move the slot, which is a wire change.
- **Derived ids.** `ShipJoint` and `ShipAnchor` already had a readable `Id`; CultLib refuses a readable id inside a marked member,
  so each gained a trailing `ElementId` marked `[CultElementId(nameof(Id))]`.
- **Minting.** A writer mints random ids for elements that have none. A store written before ids (every file under `GameData/`)
  loads with ids minted in memory and persists them on its next write; `CultCache.MintElementIds()` does that in one pass.
  `ElementIdUpgradeTests` opens a copy of the shipped catalog, mints, flushes and reopens it.
- **Comparing content.** Random ids differ between two writes of the same content. Tests that compare bytes use
  `ElementIdBlind`.
- **One list element is one object.** The same instance twice in a list is refused (`CultElementIdException`).
- **Retyping.** Writing a different type onto a held key is refused unless the record is removed first
  (`EconomyAuthor.Write` does).

## The bump is one-way

A store holding element ids is format v3 (single file) or v5 (directory). The previous pin (`cultlib-unity-v2.0.1`, `b8fc5eb0`) refuses a v3 store loudly
(`NotSupportedException: Store format cultcache.store.v3 is not readable`; checked by Soul on PR 12). CultLib #98 (`7002c252`) records
that some intermediate pins read a single-file v3 store silently and drop the ids on the next write. So: upgrade readers before
writers, keep a pre-upgrade copy of any real store (`GameData/Aetheria.cc` and the mod packages are tracked in git; player saves and
run stores are not), and do not roll the pin back after a store has been written under the new one.

The shipped mod packages (`GameData/Mods/aetheria.wren/ship.cc`, `aetheria.nakovalnya/ship.cc`) held only default
placeholders (`0` and `[]`) in the retired `HullData` slots 21 and 22, which the new CultLib refuses to write over
(`CultSchemaConflictException`). They were regenerated with `AetherDb ship-authoring regenerate <ship.cc> apply`, which loads the
records, writes them into a fresh file under the current schema, requires them to read back identical and replaces the file. Both
now hold persisted element ids and nil in slots 21 and 22; `EveryShippedModPackageCanBeWrittenOver` fails if a package regresses.
A mod file that fails the same way is regenerated the same way. A rewritten catalog is about 15% larger: every behavior row is 38
slots, mostly nil. A regenerated ship package can still shrink: the old nakovalnya file stored its floats as float64 and the
fresh write stores the typed `float` fields as float32 (40059 to 30457 bytes; each value equals the old one rounded to float32).

The Unity manifest (`Packages/manifest.json`) pins `org.gamecult.cultlib` at `cultlib-unity-v3.0.0` and `org.gamecult.cultmath` at
`cultmath-unity-v0.5.1` (the version v3.0.0 depends on). `org.gamecult.caching.unity` stays at `caching-unity-v2.0.0`: it is editor
tooling, unchanged in v3.0.0. The v3.0.0 plugin `GameCult.Caching.dll` exports `CultElementIdAttribute` (sealed, fields and
properties, one optional `derivedFrom` argument), the shape `ServerShared` uses. The Unity editor compile of this branch has not
been run; the headless build and tests are the evidence so far.
