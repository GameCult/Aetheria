# Ship collection authoring

Status: authoring contract for new ships. Longinus and Djinni predate this contract and keep their current prefabs.

## Objective and ownership

One Blender collection exports one FBX model. Its hierarchy names the ship's presentation anchors. Unity builds
`Assets/Content/Prefabs/Ships/<Name>.prefab` from that model and assigns the components and references that the
runtime currently requires on a hand-built ship prefab. `GameData/Aetheria.cc` remains the owner of `HullData`,
hardpoint cells and types, gameplay stats, products, and the prefab's Addressables key. The model never decides a
hit, thruster force, weapon arc, or shield capacity.

Today `ShipInstance` reads manually filled arrays on each ship prefab. It matches their transform names against
`HullData.Hardpoints[].Transform`. Its shared ping, invisible material, explosion, shield, and tractor references
also live on each prefab. The builder derives the arrays from the FBX hierarchy and supplies the shared Unity
assets once, from their existing project paths. The old prefabs are left alone.

## Blender collection and export

- Name the collection `Ship.<Name>` and configure its collection exporter as FBX. Export to
  `Assets/Models/Ships/<Name>.fbx`. The filename is the generated prefab name. Blender's collection exporter can
  export a collection directly and repeat that export after edits.
- Export meshes and empties, with animation disabled. Use Blender's `-Z Forward`, `Y Up` axis conversion for Unity.
  Keep the collection origin at the ship's simulation centre, with the nose pointing toward the exported +Z axis.
  Apply object scales on visual and collision meshes; the `Shield` empty's scale is the deliberate exception.
- The importer owns every prefab under `Assets/Content/Prefabs/Ships/` whose matching model is in
  `Assets/Models/Ships/`. Editing generated components by hand is temporary; the next export rebuilds them.
  Put appearance in the FBX and gameplay values in the catalog.

The exported hierarchy uses **exact, case-sensitive names**. Names beginning `HP.`, `Pivot.`, or `SHIP.` are
reserved. IDs after a role prefix must be unique across the model. Other visual objects may have any names.

```text
<Name>                         FBX root
  Body                         visible meshes, any nesting
  SHIP.MapIcon                 one mesh; assigned the shared map material and Minimap layer
  SHIP.HullCollider            one low-poly closed convex mesh; rendered invisible, given a MeshCollider
  SHIP.Shield                  one empty; its position and scale set the shield envelope
  SHIP.TractorBeam             one empty; its pose sets the tractor effect origin
  HP.Thruster.PortAft          empty; exact name copied to HullData.Hardpoints[].Transform
    Emitter                    one mesh; thruster particle emission shape
  HP.Weapon.PortGun            empty; exact name copied to the catalog
    Muzzle.0                   empty; +Z is firing direction
    Muzzle.1                   optional additional barrel
  HP.Radiator.Port             empty; exact name copied to the catalog
    RadiatorMesh               one mesh; temperature emission surface
  HP.Equipment.Reactor         empty; exact name copied to the catalog
  Pivot.0.-90.90.-80.80.60     optional empty parent of articulated mounts
```

`HP.Equipment` covers sensors, reactor, shield, cargo, and other mounts without a special visual component.
`HP.Weapon` covers ballistic, energy, and launcher mounts; the catalog chooses which. Every `HP.*` becomes an
`EquipmentHardpoints` entry. Thruster, weapon, and radiator roles also get their matching components and arrays.
`Muzzle.<nonnegative integer>` children are sorted numerically; their transforms become `FiringPoint`. A weapon
mount requires at least one. A `Pivot` name carries `Group.YawMin.YawMax.PitchMin.PitchMax.Speed` as signed
integers in degrees and degrees per second. It gets `ArticulationPoint`; child mounts follow its transform. Pivots are
presentation only: the simulation's catalog firing arc remains authoritative.

`SHIP.Shield` receives the existing Shield prefab as a child. That prefab's effective unscaled radius is one
Unity unit; the marker's three scale components are the intended envelope radii in world units. The builder adds
`ShieldEnvelope` to the Shield prefab root if absent. `SHIP.TractorBeam` receives the existing Tractor Beam
prefab. The four `SHIP.*` markers are required exactly once. Ship-specific mesh materials remain authored in
Blender/Unity's material import mapping; the builder only assigns the shared map-icon material.

## Build, validation, and failure

Re-exporting an FBX in `Assets/Models/Ships/` schedules a prefab rebuild after Unity finishes importing it.
`Aetheria/Build Ship Prefab From Selected Model` performs the same build on demand. The builder validates all
reserved names, required anchors, component meshes, muzzle numbering, and shared asset references **before**
replacing a prefab. A bad export reports an error and leaves the last valid prefab in place. The output is a
Prefab Variant of the FBX model, so mesh and transform identities remain tied to the source export.

After adding a ship, author its `HullData` and product in the catalog, set `HullData.Prefab` to the generated
prefab's GUID, and set every `HardpointData.Transform` to an exact `HP.*` name. Run the catalog asset check and
the Unity compile. A visual play check still owns size, materials, shield fit, barrel orientation, and thrust
appearance. The builder cannot infer the 2D schematic from a 3D mesh.

## Cut and build budget

This adds an Editor-only builder, its focused smoke, and this contract. It adds no runtime component, persistent field, package,
service, or build target. It replaces per-ship array and shared-reference authoring for newly exported ships.
The focused verification target is Unity Editor script compilation plus a synthetic named-hierarchy check; a
real Blender collection export is required before claiming end-to-end import fidelity.
