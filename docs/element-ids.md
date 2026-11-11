# Element ids and the CultLib pin (abd1a92e)

CultLib 3.0 (unreleased; headless pin `abd1a92e`, `Directory.Build.props`) gives every element of an object list an identity. Every
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

A store holding element ids is format v3 (single file) or v5 (directory). A pin older than `abd1a92e` can open a single-file v3
store without error and drops the ids on its next write (CultLib #98). So: upgrade readers before writers, keep a pre-upgrade
copy of any real store (`GameData/Aetheria.cc` and the mod packages are tracked in git; player saves and run stores are not),
and do not roll the pin back after a store has been written under the new one.

The Unity manifest (`Packages/manifest.json`) still pins `caching-unity-v2.0.0` / `cultlib-unity-v2.0.1`; those move when CultLib
tags a Unity release of 3.0. Until then the editor and the headless build consume different CultLib revisions.
