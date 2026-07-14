"""Validate the open Blender file against the Aetheria fixture contract."""

import re
import sys

import bpy


SCHEMA = "gamecult.aetheria.blender_entity.v1"
ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._/-]*$")


def descendants(collection):
    yield collection
    for child in collection.children:
        yield from descendants(child)


def objects_recursive(collection):
    seen = set()
    for current in descendants(collection):
        for obj in current.objects:
            if obj.name not in seen:
                seen.add(obj.name)
                yield obj


def find_root(errors):
    roots = [c for c in bpy.data.collections if c.get("aetheria.schema") == SCHEMA]
    if len(roots) != 1:
        errors.append(f"expected exactly one {SCHEMA} root collection; found {len(roots)}")
        return None
    return roots[0]


def validate_ids(root, errors):
    owners = list(descendants(root)) + list(objects_recursive(root))
    owners += [m for m in bpy.data.materials if m.get("aetheria.role") == "material"]
    owners += [i for i in bpy.data.images if i.get("aetheria.role") == "texture"]
    owners += [a for a in bpy.data.actions if a.get("aetheria.role") == "animation-clip"]
    for obj in objects_recursive(root):
        if obj.type == "ARMATURE":
            owners.extend(obj.data.bones)
    seen = {}
    for owner in owners:
        stable_id = owner.get("aetheria.id")
        label = getattr(owner, "name", repr(owner))
        if not stable_id:
            errors.append(f"{label}: missing aetheria.id")
            continue
        if not ID_PATTERN.fullmatch(str(stable_id)):
            errors.append(f"{label}: invalid aetheria.id {stable_id!r}")
        if stable_id in seen:
            errors.append(f"duplicate aetheria.id {stable_id!r}: {seen[stable_id]} and {label}")
        seen[stable_id] = label


def validate_fixture(root, errors):
    expected_root = {
        "aetheria.role": "entity-root",
        "aetheria.asset_id": "aetheria/conformance/entity-authoring-v1",
        "aetheria.asset_kind": "fixture",
        "aetheria.units": "metres",
        "aetheria.handedness": "right",
        "aetheria.up_axis": "+Z",
        "aetheria.forward_axis": "-Y",
    }
    for key, expected in expected_root.items():
        if root.get(key) != expected:
            errors.append(f"root {key}: expected {expected!r}, got {root.get(key)!r}")

    collections = list(descendants(root))
    roles = {c.get("aetheria.role") for c in collections}
    for role in ("visual", "collision", "destruction", "destruction-set", "sockets", "variant-library", "variant", "rig", "animation", "preview"):
        if role not in roles:
            errors.append(f"missing collection role {role!r}")
    lods = {c.get("aetheria.lod") for c in collections if c.get("aetheria.role") == "visual"}
    if not {0, 1}.issubset(lods):
        errors.append(f"fixture requires visual LOD 0 and 1; found {sorted(x for x in lods if x is not None)}")
    variants = [c for c in collections if c.get("aetheria.role") == "variant"]
    if {c.get("aetheria.variant.variant_id") for c in variants} != {"scout", "gunship"}:
        errors.append("fixture requires scout and gunship variant collections")

    objects = list(objects_recursive(root))
    object_roles = {o.get("aetheria.role") for o in objects}
    for role in ("visual", "collision", "destruction-piece", "socket", "port", "armature", "preview"):
        if role not in object_roles:
            errors.append(f"missing object role {role!r}")

    collision_shapes = {o.get("aetheria.collider.shape") for o in objects if o.get("aetheria.role") == "collision"}
    for shape in ("box", "sphere", "convex"):
        if shape not in collision_shapes:
            errors.append(f"fixture missing {shape!r} collision proxy")
    body_ids = {o.get("aetheria.collider.body_id") for o in objects if o.get("aetheria.role") == "collision"}
    if not {"body.hull", "body.port-panel"}.issubset(body_ids):
        errors.append(f"fixture collision bodies incomplete: {sorted(str(x) for x in body_ids)}")

    sets = [c for c in collections if c.get("aetheria.role") == "destruction-set"]
    if len(sets) != 2:
        errors.append(f"fixture requires two destruction sets; found {len(sets)}")
    for collection in sets:
        pieces = [o for o in collection.objects if o.get("aetheria.role") == "destruction-piece"]
        total = sum(float(o.get("aetheria.destruction.mass_fraction", 0.0)) for o in pieces)
        if not pieces or abs(total - 1.0) > 1e-6:
            errors.append(f"destruction set {collection.name!r} mass fractions sum to {total}")

    hardpoints = [o for o in objects if o.get("aetheria.socket.kind") == "hardpoint"]
    accepts = {o.get("aetheria.socket.accepts") for o in hardpoints}
    if not {"weapon", "utility", "radiator"}.issubset(accepts):
        errors.append(f"fixture hardpoint categories incomplete: {sorted(str(x) for x in accepts)}")
    if any(not o.get("aetheria.socket.mount_standard") for o in hardpoints):
        errors.append("every hardpoint must declare a mount standard")
    variant_memberships = {
        c.get("aetheria.variant.variant_id")
        for o in hardpoints for c in o.users_collection
        if c.get("aetheria.role") == "variant"
    }
    if variant_memberships != {"scout", "gunship"}:
        errors.append(f"fixture variant hardpoint membership incomplete: {sorted(str(x) for x in variant_memberships)}")

    ports = [o for o in objects if o.get("aetheria.role") == "port"]
    kinds = {o.get("aetheria.port.kind") for o in ports}
    if not {"thruster", "missile", "drone"}.issubset(kinds):
        errors.append(f"fixture port kinds incomplete: {sorted(str(x) for x in kinds)}")
    thrusters = [o for o in ports if o.get("aetheria.port.kind") == "thruster"]
    if not thrusters or any(o.type != "MESH" or len(o.data.polygons) == 0 for o in thrusters):
        errors.append("thruster ports must be non-empty emission meshes")
    if any(o.get("aetheria.port.flow_axis") != "local:-Y" for o in thrusters):
        errors.append("thruster flow axis must be local:-Y")

    armatures = [o for o in objects if o.type == "ARMATURE" and o.get("aetheria.role") == "armature"]
    if len(armatures) != 1:
        errors.append(f"fixture requires one armature; found {len(armatures)}")
    else:
        joints = [b for b in armatures[0].data.bones if b.get("aetheria.role") == "joint"]
        if len(joints) != 1:
            errors.append(f"fixture requires one authored joint; found {len(joints)}")
        elif joints[0].get("aetheria.joint.mode") != "physics":
            errors.append("fixture joint must exercise physics mode")
        skinned = [
            o for o in objects
            if any(m.type == "ARMATURE" and m.object == armatures[0] for m in o.modifiers)
        ]
        if not skinned:
            errors.append("fixture requires a skinned visual mesh")

    actions = [a for a in bpy.data.actions if a.get("aetheria.role") == "animation-clip"]
    if not actions:
        errors.append("fixture requires an animation clip")

    maps = {i.get("aetheria.texture.map") for i in bpy.data.images if i.get("aetheria.role") == "texture"}
    if not {"base_color", "normal", "orm", "emissive", "paint_mask"}.issubset(maps):
        errors.append(f"fixture texture maps incomplete: {sorted(str(x) for x in maps)}")
    paint_masks = [i for i in bpy.data.images if i.get("aetheria.texture.map") == "paint_mask"]
    if len(paint_masks) != 1 or any(
        paint_masks[0].get(f"aetheria.texture.channel_{channel}") != meaning
        for channel, meaning in (
            ("r", "palette-primary"),
            ("g", "palette-secondary"),
            ("b", "palette-tertiary"),
            ("a", "paint-coverage"),
        )
    ):
        errors.append("paint mask channel contract is incomplete")
    elif not {
        (1.0, 0.0, 0.0, 1.0),
        (0.0, 1.0, 0.0, 1.0),
        (0.0, 0.0, 1.0, 1.0),
    }.issubset({
        tuple(round(value, 3) for value in paint_masks[0].pixels[index:index + 4])
        for index in range(0, len(paint_masks[0].pixels), 4)
    }):
        errors.append("paint mask does not exercise primary, secondary, and tertiary channel regions")
    paint_materials = [
        material for material in bpy.data.materials
        if material.get("aetheria.material.paint_model") == "palette-mask-rgb-v1"
    ]
    if len(paint_materials) != 1:
        errors.append("fixture requires exactly one palette-mask-rgb-v1 material")
    for image in bpy.data.images:
        if image.get("aetheria.role") == "texture" and not image.packed_file:
            errors.append(f"fixture image {image.name!r} is not packed")

    preview_collections = [c for c in collections if c.get("aetheria.role") == "preview"]
    if not preview_collections or any(c.get("aetheria.export", True) for c in preview_collections):
        errors.append("preview collection must explicitly set aetheria.export=false")


def main():
    errors = []
    root = find_root(errors)
    if root is not None:
        validate_ids(root, errors)
        validate_fixture(root, errors)
    if errors:
        print("AETHERIA_ENTITY_FIXTURE_INVALID")
        for error in errors:
            print(f"  - {error}")
        raise SystemExit(1)
    print("AETHERIA_ENTITY_FIXTURE_OK")


if __name__ == "__main__":
    main()
