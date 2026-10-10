# Moddable ship authoring: parallel proof

Status: design and proof lane. The existing `HullData` catalog records, Longinus and
Djinni prefabs, and their spawn path remain live until a complete mod ship passes
authoring, validation, loading, and play checks.

## LineArt proof from Quiet.blend

The numbers in this section and in the compose example below come from an
unrecorded probe. `Build/quiet.cc` is git-ignored and no committed fixture or
test reproduces them.

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
sidebar. It uses Brokkr's configured CultLib Python source and edits the
line slot of a ship file's `aetheria.ship_authoring` visual record and the
hull shape and hardpoint slots of the `aetheria.hulldata` record beside it. To try it, make
`F:\Projects\Brokkr\surfaces\blender` and this repo's `tools\blender` visible
to Blender's add-on search path, enable `brokkr_bridge` then
`aetheria_ships`, and install `msgpack` into Blender's Python environment.
Create a draft with `dotnet run --project tools/AetherDb -- ship-authoring
create <ship.cc> <stable-id> <name> [--like <shipped hull name>]`; `--like Djinni`
copies that shipped ship's stats and leaves the body and layout empty. Choose that `.cc` in the **Aetheria
Ship** panel, select an object in its ship collection, and press **Bind Ship
Collection**. The collection stores `aetheria.asset_kind=ship`, the stable
`aetheria.id`, and a relative `.cc` path. Select a Grease Pencil object in
that bound collection and press **Capture Ship Lines**. Capture checks the
collection ID against the record before writing; object and collection
display names are not IDs.
**Load Ship Layout** opens the same `.cc` file's hull grid and hardpoints in
the panel. Toggle occupied grid cells, resize up to 32×32, and edit each
hardpoint's type, mount ID, position, footprint, rotation, armor, and firing
arc. A footprint uses top-to-bottom rows of `#` (occupied) and `.` (empty),
separated by `/`; `##/##` is a 2×2 mount. **Save Layout to .cc** replaces only
the hull shape and hardpoint slots. It preserves hull stats, anchors, visual
assets, lines, the record key, and schema. Save rejects a changed ship ID or
layout revision; reload first if another editor changed those fields. A line
capture leaves the loaded layout revision intact. The saved layout is a draft until
its mount IDs resolve to visual anchors and C# validation passes.
**Package Ship** turns the bound collection into the package. Tag each anchor
object with the custom properties `aetheria.role` (`map-icon`, `hull-collider`,
`shield`, `tractor`, `thruster-emitter`, `radiator-mesh`, `weapon-mount`,
`weapon-muzzle`) and `aetheria.id` (the hardpoint's mount ID for a mount),
plus `aetheria.order` if a weapon has several muzzles. Parent a muzzle to its
weapon mount object. Thrusters and radiators are mesh objects; the others may
be empties. Package exports the collection, Grease Pencil excluded, to
`ship.glb` beside the `.cc`, writes the anchors, captures the lines when the
collection holds exactly one Grease Pencil object, and shows AetherDb's
verdict in the panel. It runs the AetherDb in the game folder's `ModTools`
(see Tools below).
`ship-authoring inspect <ship.cc>` reads drafts; `validate` requires complete
hull, model, and anchor authoring, and judges `ship.glb` once it exists.

## Tools

Install Blender and the `aetheria_ships` add-on. In the add-on's preferences set
**Game folder** to your Aetheria install, the folder holding `GameData` (its
`GameData/Aetheria.cc` and `GameData/Mods`). Left empty, the add-on uses the
nearest folder above the bound `.cc` whose `GameData` holds `Aetheria.cc`.
AetherDb ships with the game in `ModTools`; the add-on runs it with the game
folder as its working directory, and needs neither Unity, the Aetheria repository
nor dotnet. New Ship creates the package under `<game folder>/GameData/Mods/<id>`.

From a repository checkout, publish AetherDb into the ignored `<repo>/ModTools` and
author from the repo as the game folder:

    dotnet publish tools/AetherDb -c Release -r <rid> --self-contained -p:PublishSingleFile=true -o ModTools

with `<rid>` one of `win-x64`, `linux-x64`, `osx-arm64`. Linux needs `libssl3`
and `libicu`; on macOS clear the quarantine flag (`xattr -d com.apple.quarantine
ModTools/AetherDb`).

## Objective

A mod author edits one ship `.cc` file, including the hull schematic and every
hardpoint. The file holds two typed records: the ship's `HullData` and the
`ShipAuthoring` visual package that hull names through `HullData.Visual`. Blender, through Aetheria tooling built on
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

- **Owner:** a mod-owned `.cc` file holds two records. Its `HullData` owns the
  ship's authored hull semantics: schematic, hardpoints, and stats. Its
  `ShipAuthoring` document owns the visual package: model path, anchor-to-node
  associations, and schematic lines. `HullData.Visual` is the only binding
  between them; a hull names exactly one of `Prefab` and `Visual`. A ship ID is stable across
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

`ship-authoring validate`, the catalog composer, and the visual importer call
the same `ShipAuthoringStore.Validate(hull, visual)`; that shared path is true of the C# callers only.
The Blender side checks grid bounds and footprint shape (`ship_cc.py`) and
nothing semantic. Blender edits the hull grid, hardpoints, and
captured lines in the typed source records; `inspect` reads incomplete drafts.
Unity's **Aetheria / Preview Mod Ship
Package** imports a validated package from disk, resolves stable GLB node IDs,
and draws the captured polylines directly as 3D line segments. Point opacity
and color are retained; radius and material-specific stroke styling are not
rendered yet. The game reads mod records through the boot path described
under Runtime catalog seam.

## Runtime catalog seam

`AetheriaStores.CatalogTypes` routes `ShipAuthoring` to the shipped catalog,
but the shipped catalog contains no `ShipAuthoring` record. This stays correct
only while nothing writes a `ShipAuthoring` through a catalog-writable cache on
`Aetheria.cc`; a mod ship's authoring record lives in its own `ship.cc` and in
the derived catalog.

`CultCache` routes `ItemData` to one home backing store, and
`ActionGameManager.CultCache` holds that store for the entire play session.
Attaching each mod's `.cc` as another item store would violate the cache's
one-home invariant. `ship-authoring compose` instead derives one runtime catalog from
the shipped catalog and validated mod packages, preserving all shipped record
keys. Each mod's stable `ShipAuthoring.Id` yields separate deterministic keys
for its copied `HullData` (`mod-hull:<id>`) and its runtime authoring record
(`mod-ship:<id>`). A mod's `ship.cc` stores its two records under those same
keys, and compose copies them verbatim, so the derived catalog holds one hull
per mod ship and `HullData.Visual` names the `mod-ship` record. The generated
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

The game does not need this command: at boot, when `GameData/Mods` holds a
package, it composes the derived catalog into `Application.persistentDataPath`
(`ShipModCatalog.ResolveCatalog`) and opens that file instead of
`Aetheria.cc`; with no package it opens the shipped catalog. The derived file is
recomposed on every launch. A playable ship is still outstanding. In the Quiet probe,
206 shipped records became 208 derived records: one `mod-hull:quiet` and one
`mod-ship:quiet`. A missing GLB node ID was rejected without changing the
previous derived file. The probe's four anchors and zero hardpoints are only
a data-path smoke, not a playable Quiet design.

The visual importer uses [Unity glTFast's runtime
import](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportRuntime.md)
to read the package GLB asynchronously and map its stable node IDs to Unity
transforms. It keeps a partially loaded object inactive and destroys it on
failure. `ShipModShips` preloads one assembled, inactive prototype per hull
that names a `Visual`, at boot and before the game scene loads.
`ShipAuthoringStore.Validate` owns which hardpoints carry anchors and which role
each anchor plays. Mount anchors exist only for hardpoints that show on the
model: weapons, radiators and thrusters. Each such hardpoint needs an anchor
whose id is its mount id: `thruster-emitter` for a thruster, `radiator-mesh` for
a radiator, `weapon-mount` for an energy, ballistic or launcher hardpoint. An
internal hardpoint (reactor, sensors, shield and the rest) has no anchor, and
an anchor taking its id is refused. A thruster's or radiator's anchor node is a
mesh: the thruster's is not rendered and is the surface its exhaust emits from,
the radiator's is rendered. A weapon mount is a point, and each one needs at
least one `weapon-muzzle` anchor whose `ParentId` is the weapon's mount id.
`articulation` is reserved for pivots and is never a mount; no pivot is
planned until pivots move onto `HullData`. `ShipModPlan` then maps the
validated pair to `ShipInstance` slots and refuses nothing itself. The shared effects (shield, tractor beam, ping, explosion, map
and invisible materials) come from the Addressable `ShipModTemplate` prefab.
`ZoneRenderer.LoadEntity` clones the prototype when the hull names a `Visual`.
Gameplay reads a mod ship's records from the derived catalog; the package
directory supplies only the GLB. `capturepreset` still writes the shipped
catalog. Continue refuses a run naming a design the catalog no longer holds
(`RunSave.RequireDesigns`), naming the missing mod ships.

## Proof gates

1. A typed `.cc` record round-trips between C# and Blender's Python runtime,
   preserving unknown future fields and record identity.
2. Blender edits schematic cells and hardpoints in that record. **Package
   Ship** writes the anchors and `ship.glb` from the bound collection: each
   object tagged `aetheria.role` becomes an anchor whose id, and GLB node
   `aetheria.id`, is the object's `aetheria.id`. It then runs
   `AetherDb ship-authoring validate`, which judges the GLB too: node ids,
   and a mesh under every thruster and radiator anchor. The model supplies
   geometry, not a second copy of hull semantics.
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

### Unity batch smokes

Both run in batchmode against a package the tests generate: set `AETHERIA_SHIP_FIXTURE_DIR` while running
`ShipModCatalogTests.TheFixturePackageIsCompleteAndItsGlbCarriesAScene` and it leaves `mod.skiff/ship.cc` and
`skiff.glb` there (the GLB carries a tetrahedron mesh on the map, collider and thruster anchors, plus a weapon mount
and muzzle). Neither takes `-quit`: both are `async void` and end the Editor themselves.

- `-executeMethod ShipModPreview.Smoke -shipModPath <dir>/mod.skiff/ship.cc [-assemble]` imports the GLB and checks
  the anchors; `-assemble` also builds the `ShipInstance` from the template.
- `-executeMethod ShipModPlaySmoke.Run -shipModPath <dir>/mod.skiff/ship.cc [-shippedCatalog GameData/Aetheria.cc]`
  composes a derived catalog with the package, runs `ShipModShips.Preload`, instantiates the ship through
  `ShipModShips.Instantiate` (the call `ZoneRenderer.LoadEntity` makes) under a bare parent transform, and requires
  thruster and weapon hardpoints (each weapon with a muzzle), one `MeshCollider` over a readable mesh, and a map icon.
  It prints `SHIP_MOD_PLAY_SMOKE thrusters=N weapons=N collider=ok mapicon=ok` and exits 0, or logs the exception and
  exits 1. It uses no scene and no zone.

## Structural and build budget

The proof reuses `HullData` simulation semantics, CultCache `.cc` persistence,
Brokkr's Blender host, and the current `ShipInstance` behavior. It adds one
authoring document type and validator, Blender-facing layout and line editing,
a disposable catalog composer, and one runtime GLB/line visual importer with
an Editor preview, `ShipInstance` assembly and boot-time preloading. No daemon, second gameplay simulation, or Unity
prefab generator is added. The existing FBX builder remains available to
existing content but is not an input to this lane. Its retirement follows
migration.

Focused checks use `Aetheria.Shared` and `tests/Aetheria.Shared.Tests` under
`netstandard2.1` / `net10.0`, Python CultCache interoperability checks, and
the Unity 6000.3.24f1 Editor compile and play probe. No workspace-wide build
or new executable target is justified for schema work. The current catalog is
12.7 MB; it is not rewritten during the proof. The worktree starts clean from
`4b594e11`; unrelated Unity scene and material edits remain in the original
checkout.
