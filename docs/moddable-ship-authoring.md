# Moddable ship authoring: parallel proof

Status: design and proof lane. The existing `HullData` catalog records, Longinus and
Djinni prefabs, and their spawn path remain live until a complete mod ship passes
authoring, validation, loading, and play checks.

## LineArt proof from Quiet.blend

`C:\Users\Meta\Desktop\Quiet.blend` is a read-only source for this probe. At frame
1, `Quiet / Dexter Quiet Lineart` evaluates to 1,040 polylines with 3,125
points. Blender 5.2.2 wrote those coordinates, point radii and opacities,
material/layer names, cyclic flags, and RGBA into `Build/quiet.cc` without
changing the `.blend`. C# reopened the same typed record and counted the same
lines and points. A second line replacement preserved its record key, schema
ID, hull, model asset, and anchors byte for byte. `Build/quiet.cc` is an
ignored draft probe, not an installed ship.

The line positions remain 3D Blender world coordinates. The runtime can
lower them to a hologram or schematic view without regenerating LineArt.
LineArt evaluation depends on the source scene and camera, so a captured set
may be incomplete from another view. Gameplay cell occupancy remains the
authored `HullData.Shape` in the `.cc`; strokes do not decide armor or mount
footprints.

`tools/blender/aetheria_ships` is a separate Blender add-on in Brokkr's
sidebar. It uses Brokkr's configured CultLib Python source and edits only the
line slot of an existing `aetheria.ship_authoring` record. To try it, make
`F:\Projects\Brokkr\surfaces\blender` and this repo's `tools\blender` visible
to Blender's add-on search path, enable `brokkr_bridge` then
`aetheria_ships`, and install `msgpack` into Blender's Python environment.
Create a draft with `dotnet run --project tools/AetherDb -- ship-authoring
create <ship.cc> <stable-id> <name>`, choose that `.cc` in the **Aetheria
Ship** panel, select a Grease Pencil object, and press **Capture Ship Lines**.
`ship-authoring inspect <ship.cc>` reads drafts; `validate` requires complete
hull, model, and anchor authoring.

## Objective

A mod author edits one typed ship record in a `.cc` file, including the hull
schematic and every hardpoint. Blender, through Aetheria tooling built on
Brokkr, edits that same record while displaying its visual source. The game
assembles an instance from the record and its referenced asset package at
runtime. A mod package can be installed without opening the Unity Editor.

## Current mechanism

`GameData/Aetheria.cc` holds `HullData`: the cell shape, hardpoint footprints,
stats, and a Unity Addressables prefab GUID. `ZoneRenderer.LoadEntity` loads
that prefab and expects `ShipInstance`; `EntityInstance` and `ShipInstance`
match catalog hardpoint transform strings against manually wired prefab
arrays. The new FBX naming builder automates prefab wiring but still emits a
Unity prefab and is part of this existing path. Brokkr can capture Blender
collections and custom properties; the AetheriaEve Blender authoring v1
contract describes a future collection-to-CultMesh deploy lane, but that lane
is not yet an end-to-end ship loader in the current game.

## Target authority map for the parallel lane

- **Owner:** a versioned `ShipAuthoring` document in a mod-owned `.cc` file
  owns the ship's authored hull, schematic, hardpoints, structural and
  presentation associations, and asset references. A ship ID is stable across
  display-name changes.
- **Inputs:** the authoring record; a Blender visual source identified by the
  record; compiled mesh, texture, and material artifacts; references to gear
  designs where the game requires them.
- **Outputs:** a validated runtime hull design and an assembled visual/physics
  `ShipInstance`. The assembly and any catalog row are derived products.
- **Derived state:** Unity objects, component arrays, collision components,
  particle emitters, and schematic UI. Installed equipment, damage, heat,
  cargo, ownership, and position are live game state.
- **Forbidden writers:** Blender object names, Unity prefabs, generated catalog
  rows, and runtime components may not independently decide the schematic,
  hardpoint type or footprint, armor, drag, or mount identity. Geometry nodes
  carry stable IDs; the `.cc` document gives them meaning.
- **Shared path:** Blender validation, command-line validation, editor preview,
  and runtime loading read the same typed record and invoke the same semantic
  validator. A bad mod is rejected before partial scene construction.
- **Cut line:** no existing ship path is removed during proof. Migration later
  deletes or demotes the old ship-specific catalog/prefab authority for each
  migrated hull; it does not keep two writable definitions of that hull.

At this proof stage, `ship-authoring validate` is the sole caller of the
semantic validator. The Blender capture command writes only line data and the
`inspect` command reads incomplete drafts. Editor preview and runtime loading
still need to be built. The current gameplay catalog and prefab loader do not
read these mod records.

## Runtime catalog seam

`CultCache` routes `ItemData` to one home backing store, and
`ActionGameManager.CultCache` holds that store for the entire play session.
Attaching each mod's `.cc` as another item store would violate the cache's
one-home invariant. `ship-authoring compose` instead derives one runtime catalog from
the shipped catalog and validated mod packages, preserving all shipped record
keys. Each mod's stable `ShipAuthoring.Id` yields separate deterministic keys
for its copied `HullData` and its runtime authoring record. The generated
catalog is disposable; the shipped catalog and each mod's source `.cc` remain
the owners. A player session opens one catalog snapshot and cannot hot-swap its
designs beneath existing entities.

Each immediate child of the mods directory has an ID matching its directory
name and contains `ship.cc` plus the GLB at the record's relative `ModelAsset`
path. Every referenced anchor uses an `aetheria.id` custom property exported
into a GLB node's `extras`. Compose checks the asset, unique IDs, and every
reference before atomically replacing the derived catalog. For example:

```text
dotnet run --project tools/AetherDb -- ship-authoring compose GameData/Aetheria.cc GameData/Aetheria.modded.cc GameData/Mods
```

This command does not yet make the game open `Aetheria.modded.cc`; the runtime
loader and a complete playable ship are still outstanding. In the Quiet probe,
206 shipped records became 208 derived records: one `mod-hull:quiet` and one
`mod-ship:quiet`. A missing GLB node ID was rejected without changing the
previous derived file. The probe's four anchors and zero hardpoints are only
a data-path smoke, not a playable Quiet design.

The construction boundary will resolve a mod hull's authoring record from its
derived key, load the package's compiled GLB at runtime, and build the
`ShipInstance` references from validated stable node IDs. It must fail before
scene construction when an asset or anchor is absent. [Unity glTFast's runtime
import](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportRuntime.md)
is a candidate for the GLB step; it has not been added to the project.

## Proof gates

1. A typed `.cc` record round-trips between C# and Blender's Python runtime,
   preserving unknown future fields and record identity.
2. Blender edits schematic cells and hardpoints in that record, and validates
   their association with stable visual node IDs. The model supplies geometry,
   not a second copy of hull semantics.
3. A command-line validator rejects malformed cells, overlapping or dangling
   hardpoints, missing model nodes, and unresolved asset references with
   precise errors. It changes no live catalog or scene on failure. The
   composer now performs this check for a package before replacing its
   derived catalog.
4. A packaged mod ship loads in a built Unity player without Unity Editor or
   Blender installed. Runtime assembly creates the same components the
   current `ShipInstance` expects and uses the existing simulation rules.
5. Spawn, equip, thrust, fire, shield, damage, save/reload, and schematic UI
   work for the mod ship. Only then is a shipped hull eligible for migration.

## Structural and build budget

The proof reuses `HullData` simulation semantics, CultCache `.cc` persistence,
Brokkr's Blender host, and the current `ShipInstance` behavior. This pass adds
one authoring document type, one validator, and one Blender-facing editor
surface. A later pass needs one runtime construction boundary. It does not add
a daemon, a second gameplay
simulation, or another Unity prefab generator. The existing name-based FBX
builder remains available to existing content but is not an input to this
lane. Its retirement is a later cut, after migration.

Focused checks use `Aetheria.Shared` and `tests/Aetheria.Shared.Tests` under
`netstandard2.1` / `net10.0`, Python CultCache interoperability checks, and
the Unity 6000.3.24f1 Editor compile and play probe. No workspace-wide build
or new executable target is justified for schema work. The current catalog is
12.7 MB; it is not rewritten during the proof. The worktree starts clean from
`4b594e11`; unrelated Unity scene and material edits remain in the original
checkout.
