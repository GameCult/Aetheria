# Blender Entity Authoring v1

This contract makes one Blender collection the authoring authority for an
Aetheria ship, station, turret, drone, or other hull-shaped entity. Unity
prefabs are import products. They do not own hull semantics.

The source `.blend` is consumed by the Brokkr asset pipeline. Brokkr captures
the collection, validates it, packages geometry, textures, materials, skins,
and animation through the CultMesh CDN, then publishes a
`gamecult.mesh.entity_prefab_package.v1` document. Generic clients lower that
portable package. Clients do not need the `.blend` file or Aetheria provider
code.

## Authority map

- **Owner:** one root Blender collection owns the authored entity.
- **Inputs:** Blender collections, objects, meshes, armatures, actions,
  materials, images, transforms, and namespaced custom properties.
- **Outputs:** a Brokkr prefab snapshot, CDN artifact manifests and bodies, and
  one `CultMeshEntityPrefabPackage` graph.
- **Derived state:** Unity prefabs, runtime scene objects, Ymir body fixtures,
  particle systems, fracture instances, and equipped prefab instances.
- **Forbidden writers:** Unity prefabs, Eve lowerers, and client-side scripts
  must not redefine collision, sockets, ports, fracture membership, or joints.
- **Shared path:** editor preview, daemon import, CDN packaging, and every
  runtime lowerer consume the same package graph.
- **Deletion line:** semantics that exist only in Djinni or Longinus Unity
  prefabs must be migrated into Blender before those prefabs can be retired.

## Coordinates and identity

The root collection MUST carry:

| Property | Value |
| --- | --- |
| `aetheria.schema` | `gamecult.aetheria.blender_entity.v1` |
| `aetheria.asset_id` | Stable provider-owned asset ID |
| `aetheria.asset_kind` | `ship`, `station`, `turret`, `drone`, or `fixture` |
| `aetheria.version` | Author-selected immutable version |
| `aetheria.units` | `metres` |
| `aetheria.handedness` | `right` |
| `aetheria.up_axis` | `+Z` |
| `aetheria.forward_axis` | `-Y` |

CultMesh stores source transforms in Blender coordinates. Each runtime lowerer
owns one tested coordinate conversion. Authors must not pre-rotate objects for
Unity.

Every exported collection, object, bone, material, image, and action MUST have
a stable `aetheria.id`. Names are labels and may change. IDs use lower-case
ASCII letters, digits, `.`, `_`, `/`, and `-`; they are unique within the root
entity collection.

Custom properties are typed scalars or fixed numeric vectors. Do not hide the
contract in a JSON string. New semantics require a versioned property or
component contract.

## Collection roles

Names below are the recommended readable layout. The authoritative selector is
the collection's `aetheria.role` property.

| Role | Purpose |
| --- | --- |
| `entity-root` | Exactly one root authoring collection. |
| `visual` | Render geometry. Child collections may declare `aetheria.lod`. |
| `collision` | Non-rendering collision proxy meshes. |
| `destruction` | Parent for one or more destruction-set collections. |
| `destruction-set` | A complete named set of explosion pieces. |
| `sockets` | Attachment points and launch/emission ports. |
| `variant-library` | Parent for hull-family variant declarations. |
| `variant` | One compile-time selection of mount candidates and geometry modules. |
| `rig` | Armatures and rig helpers. |
| `animation` | Optional organizational collection for animation helpers. |
| `preview` | Cameras, lights, labels, and scale references; never exported. |

Objects may belong to only one primary semantic role collection. They may also
be linked into one or more `variant` collections to declare variant inclusion.
Blender parenting is the runtime transform hierarchy; collection nesting is
classification.

## Hull families, variants, and loadouts

The mature MechWarrior/BattleTech split is worth preserving: a reusable chassis
owns the complete modeled hardpoint vocabulary; a variant selects which of
those hardpoints exist; a loadout selects installed equipment. Those are three
different authorities.

An Aetheria `.blend` therefore authors one **hull family** and may contain a
`variant-library`. Each child `variant` collection carries:

- `aetheria.variant.variant_id`
- `aetheria.variant.hull_id`, matching the root `aetheria.asset_id`
- `aetheria.variant.display_name`

Objects linked only to their primary role collection are common to every
variant. An object additionally linked to one or more `variant` collections is
included only in those variants. This supports variant-specific hardpoint
placement, cowlings, apertures, collision proxies, and destruction pieces
without copying the base mesh.

The deployer emits one portable prefab package per variant while reusing the
same content-addressed mesh and texture artifacts. Aetheria's typed hull or
item definition references the variant package. Installed equipment remains
daemon-owned entity state and is never saved into the `.blend`.

The distinction is structural:

- The hull family says, "these mounts and geometry modules can physically
  exist."
- The variant says, "this subset exists on this manufactured hull."
- The loadout says, "these item instances are installed now."

No layer may repair an impossible request from the layer above. If a variant
selects a hardpoint with no presentable geometry or envelope, the import fails.

## Object roles

All exported objects carry `aetheria.id` and `aetheria.role`.

### Visual meshes

`aetheria.role = visual` identifies render geometry. The containing visual
collection supplies `aetheria.lod`; `0` is mandatory. Materials and all image
nodes reachable from them are CDN dependencies. Packed images are legal source
assets but are unpacked into independent CDN artifacts by the deployer.

Skinned visual meshes use an ordinary Blender Armature modifier. Shape keys,
vertex groups, armature bindings, and actions are exported through the skin and
animation payload. Generic clients receive a standard render/skin graph, not a
Unity Animator controller.

### Collision proxies

Collision meshes use `aetheria.role = collision` and:

| Property | Meaning |
| --- | --- |
| `aetheria.collider.shape` | `box`, `sphere`, `capsule`, `convex`, or `mesh` |
| `aetheria.collider.body_id` | Stable body or articulated-body ID |
| `aetheria.collider.trigger` | Boolean trigger intent |
| `aetheria.collider.material` | Provider physics-material ID |

Proxy geometry is authoritative. `box`, `sphere`, and `capsule` meshes define
their fitted dimensions; `convex` defines a convex hull; `mesh` is reserved for
static bodies such as stations. The Aetheria importer lowers these facts into
Ymir/Box3D fixtures appropriate to the simulation plane. Render meshes are
never silently substituted as collision meshes.

### Destruction sets

Each `destruction-set` collection carries a stable `aetheria.destruction.set_id`.
Every piece inside it is a visual mesh with:

- `aetheria.role = destruction-piece`
- `aetheria.destruction.piece_id`
- `aetheria.destruction.body_id`
- `aetheria.destruction.mass_fraction`

Mass fractions in a set sum to one within importer tolerance. A set is selected
as a whole; collection membership is not inferred from names or proximity.
Explosion impulse, lifetime, and loot are simulation state owned by the daemon,
not mesh metadata.

### Hardpoints

A hardpoint is an Empty with `aetheria.role = socket` and:

- `aetheria.socket.kind = hardpoint`
- `aetheria.socket.accepts = weapon`, `utility`, `radiator`, or another typed
  equipment category
- `aetheria.socket.size` as the provider's slot-size token
- `aetheria.socket.mount_standard` as the transform/envelope contract implemented
  by compatible equipment prefabs
- `aetheria.socket.presentation_group` for selecting the hull's modeled mount
  family when a cowl or aperture differs by weapon category
- `aetheria.region_id` for the structural region containing the mount

The equipped prefab root is instantiated at the Empty's local transform. Its
local `-Y` axis is forward and local `+Z` is up. Weapon muzzle points belong to
the equipped weapon prefab, not the hull hardpoint.

### Thruster, missile, and drone ports

Ports use `aetheria.role = port`, a stable `aetheria.port.group_id`, and an
`aetheria.port.kind` of `thruster`, `missile`, or `drone`.

Thruster ports MUST be meshes. Their triangles are the particle emission
surface and their local `-Y` axis is exhaust flow. They also carry
`aetheria.port.flow_axis = local:-Y`. They are semantic meshes, not visible hull
geometry.

Missile ports are ordered Empty transforms or aperture meshes. Drone ports are
Empty spawn transforms or aperture meshes. Ordered banks add
`aetheria.port.sequence`, starting at zero. The daemon decides whether and when
something launches; the port only describes where and how it is presented.

### Armatures and joints

Armatures use normal Blender bones, weights, constraints, and actions. Every
exported bone has `aetheria.id`. A bone that declares an articulated joint also
has:

| Property | Meaning |
| --- | --- |
| `aetheria.joint.kind` | `hinge`, `slider`, `fixed`, or `aim` |
| `aetheria.joint.mode` | `animation` or `physics` |
| `aetheria.joint.parent_body_id` | Parent collision body |
| `aetheria.joint.child_body_id` | Child collision body |
| `aetheria.joint.axis` | Local axis token such as `local:X` |
| `aetheria.joint.limit_min` | Minimum degrees or metres |
| `aetheria.joint.limit_max` | Maximum degrees or metres |

`animation` joints are presentation-only. `physics` joints are imported into
daemon/Ymir state and require matching collision body IDs. An armature does not
silently grant physics authority to a client animation.

Actions have `aetheria.id`, `aetheria.role = animation-clip`, and
`aetheria.animation.loop`. Animation state is selected by typed entity state;
clients merely lower the clip.

## Paint, patterns, and decals

Do not duplicate hull materials for every skin. A paintable material owns:

- ordinary PBR maps: base color, normal, ORM, and emissive;
- a non-color `paint_mask` image;
- `aetheria.material.paint_model = palette-mask-rgb-v1`;
- three default palette colors.

The paint mask uses channel weights:

| Channel | Meaning |
| --- | --- |
| R | primary palette weight |
| G | secondary palette weight |
| B | tertiary palette weight |
| A | total paint coverage; zero preserves authored base color |

RGB should normally be mutually exclusive but may blend at boundaries. The
shader normalizes non-zero RGB before applying the palette, multiplies the
result by alpha coverage, then composites over authored base color. Pattern
textures can remap the same three weights without replacing PBR detail.
Palette, pattern, weathering amount, and decal selection are instance or
loadout presentation state; UVs and masks are hull-authoring state.

Mask channel meaning is declared on the image custom properties and copied into
the CDN material definition. Generic lowerers consume the declared paint model;
they do not recognize an Aetheria shader name.

## CultMesh lowering

The deployer maps authored facts into the existing portable prefab graph:

| Blender fact | Portable node/component intent |
| --- | --- |
| Visual mesh | `mesh` node plus mesh/material asset references |
| Collision proxy | `collider` node plus `gamecult.prefab.collider.v1` |
| Hardpoint/port | `socket` node plus `gamecult.prefab.socket.v1` or `gamecult.prefab.port.v1` |
| Thruster surface | mesh asset plus `gamecult.prefab.emission_surface.v1` |
| Destruction piece | mesh node plus `gamecult.prefab.destruction_piece.v1` |
| Armature/joint | animation asset plus `gamecult.prefab.joint.v1` |

Those component IDs are portable intents. Aetheria owns their gameplay use;
generic lowerers own only rendering, attachment, particles, animation, and
collision-debug presentation. If CultMesh cannot carry one of these facts
without opaque metadata, extend the typed prefab contract before shipping the
asset. Do not add an Aetheria branch to EveUnity.

Recommended runtime payloads are GLB (`model/gltf-binary`) for geometry, skins,
and animation; PNG or KTX2 for textures; and typed CultCache documents for the
prefab graph and component intents. CDN manifests are content-addressed and
provider-owned.

## Conformance fixture

`Asset Sources/Conformance/AetheriaEntityAuthoringV1.blend` exercises:

- LOD0 and LOD1 render meshes;
- packed base-color, normal, ORM, and emissive maps;
- box, sphere, convex, and articulated collision bodies;
- two complete destruction sets;
- weapon, utility, and radiator hardpoints;
- mesh thruster emission ports;
- ordered missile and drone ports;
- a two-variant hull family with different hardpoint selections;
- a channel-packed three-color paint mask;
- a skinned articulated panel, armature, physics hinge metadata, and animation;
- preview-only camera, light, and scale reference.

Regenerate and validate it headlessly:

```powershell
blender --background --factory-startup --python tools/blender/create_aetheria_entity_fixture.py
blender --background "Asset Sources/Conformance/AetheriaEntityAuthoringV1.blend" --python tools/blender/validate_aetheria_entity.py
```

The validator prints `AETHERIA_ENTITY_FIXTURE_OK` and exits zero only when the
contract is present. This validates authoring structure. A later pipeline smoke
must also prove snapshot capture, CDN dependency packaging, portable-package
publication, and lowering in a generic client.

## Prior art

- HBS's published [How to Create a BattleMech](https://ryanburrell.com/wp-content/uploads/2023/03/HBS_How-to-Create-a-BattleMech_X.pdf)
  records that most visual sources came from Piranha/MWO and treats
  `HardpointDataDef`, `ChassisDef`, `MechDef`, colliders, weapon prefabs,
  heraldry, and asset-bundle association as separate production steps.
- Piranha's [MW5 Mod Editor Guide](https://static.mw5mercs.com/docs/MW5Mercs_Mod_Editor_Guide_%28v2.3%29.pdf)
  treats a loadout/variant as the spawnable selection and recommends a real
  test map for gameplay assets.
- The community-documented MW5 asset split describes MDL/HPS/MDA/Loadout as
  chassis, modeled hardpoint vocabulary, variant, and installed configuration.
  See [Hardpoints, Mech Data, and Loadouts](https://mechwarrior5modding.fandom.com/wiki/Hardpoints%2C_Mech_Data%2C_and_Loadouts).
  We copy the ownership split, not Unreal's file formats.
- Epic's [texture-mask guidance](https://dev.epicgames.com/documentation/unreal-engine/using-texture-masks-in-unreal-engine)
  documents channel packing as the normal way to carry several independent
  material masks efficiently.

## Pipeline gates exposed by the fixture

The current Brokkr Blender snapshot is not yet sufficient to deploy this
contract. Before the importer can claim conformance it must capture:

- collection custom properties, including roles and variant declarations;
- armature bones, bone custom properties, parentage, and bind poses;
- Armature modifier targets, vertex groups, and skin weights;
- actions, clip metadata, and animation curves;
- mesh UV sets and material-to-image dependency edges;
- packed image bytes and declared channel semantics;
- stable mesh payload identity rather than mesh counts alone.

The deployer must then compile each variant into a fully resolved
`CultMeshEntityPrefabPackage`, publish all referenced CDN bodies, and reject
dangling IDs, duplicate IDs, unsupported component intents, missing payloads,
invalid destruction mass closure, or physics joints whose body IDs do not
exist.

The existing portable package also needs typed, advertised definitions for the
socket, port, emission-surface, destruction-piece, and joint component intents.
String metadata may carry a development probe, but it is not the released
contract. This fixture is the acceptance input for that work; it is not evidence
that the deploy lane already exists.
