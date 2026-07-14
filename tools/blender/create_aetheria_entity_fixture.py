"""Create the Blender Entity Authoring v1 conformance fixture.

Run with Blender, not system Python:
  blender --background --factory-startup --python tools/blender/create_aetheria_entity_fixture.py
"""

from pathlib import Path
import math

import bpy


SCHEMA = "gamecult.aetheria.blender_entity.v1"
ROOT_NAME = "AE_AuthoringFixture"


def repo_root() -> Path:
    script = Path(__file__).resolve()
    return script.parents[2]


def set_properties(owner, **properties):
    for key, value in properties.items():
        owner[key.replace("__", ".")] = value


def new_collection(name, parent, stable_id, role, **properties):
    collection = bpy.data.collections.new(name)
    parent.children.link(collection)
    set_properties(
        collection,
        aetheria__id=stable_id,
        aetheria__role=role,
        **properties,
    )
    return collection


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def tag(obj, stable_id, role, **properties):
    set_properties(obj, aetheria__id=stable_id, aetheria__role=role, **properties)
    return obj


def material(name, stable_id, base, metallic, roughness, emission):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    set_properties(mat, aetheria__id=stable_id, aetheria__role="material")
    set_properties(
        mat,
        aetheria__material__paint_model="palette-mask-rgb-v1",
        aetheria__material__palette_primary=[0.08, 0.32, 0.55, 1.0],
        aetheria__material__palette_secondary=[0.9, 0.18, 0.04, 1.0],
        aetheria__material__palette_tertiary=[0.95, 0.72, 0.08, 1.0],
    )
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (700, 0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (420, 0)
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    def image_node(label, map_kind, color, x, y, non_color=False):
        image = bpy.data.images.new(f"IMG_{label}", width=8, height=8, alpha=True)
        pixels = list(color) * (8 * 8)
        image.pixels = pixels
        image.pack()
        set_properties(
            image,
            aetheria__id=f"texture.fixture.{map_kind}",
            aetheria__role="texture",
            aetheria__texture__map=map_kind,
        )
        node = nodes.new("ShaderNodeTexImage")
        node.label = label
        node.name = f"TEX_{map_kind.upper()}"
        node.image = image
        node.location = (x, y)
        if non_color:
            image.colorspace_settings.name = "Non-Color"
        return node

    base_node = image_node("Base Color", "base_color", base, -700, 250)
    normal_node = image_node("Normal", "normal", (0.5, 0.5, 1.0, 1.0), -700, 0, True)
    orm_node = image_node("ORM", "orm", (1.0, roughness, metallic, 1.0), -700, -250, True)
    emission_node = image_node("Emission", "emissive", emission, -700, -500)
    paint_node = image_node("Paint Mask", "paint_mask", (1.0, 0.0, 0.0, 1.0), -700, -750, True)
    paint_pixels = []
    for _y in range(8):
        for x in range(8):
            paint_pixels.extend((1.0, 0.0, 0.0, 1.0) if x < 3 else
                                (0.0, 1.0, 0.0, 1.0) if x < 6 else
                                (0.0, 0.0, 1.0, 1.0))
    paint_node.image.pixels = paint_pixels
    paint_node.image.pack()
    set_properties(
        paint_node.image,
        aetheria__texture__channel_r="palette-primary",
        aetheria__texture__channel_g="palette-secondary",
        aetheria__texture__channel_b="palette-tertiary",
        aetheria__texture__channel_a="paint-coverage",
    )

    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (80, 0)
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.location = (-200, -240)

    links.new(base_node.outputs["Color"], principled.inputs["Base Color"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    links.new(orm_node.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    metallic_input = principled.inputs.get("Metallic IOR Level") or principled.inputs.get("Metallic")
    links.new(separate.outputs["Blue"], metallic_input)
    links.new(emission_node.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 2.0
    return mat


def assign_material(obj, mat):
    if obj.data and hasattr(obj.data, "materials"):
        obj.data.materials.append(mat)


def cube(name, collection, stable_id, role, location, dimensions, mat=None, **properties):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    move_to_collection(obj, collection)
    tag(obj, stable_id, role, **properties)
    if mat:
        assign_material(obj, mat)
    return obj


def sphere(name, collection, stable_id, role, location, radius, mat=None, **properties):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    tag(obj, stable_id, role, **properties)
    if mat:
        assign_material(obj, mat)
    return obj


def emitter_quad(name, collection, stable_id, location, rotation=(0.0, 0.0, 0.0), **properties):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(
        [(-0.5, 0.0, -0.35), (0.5, 0.0, -0.35), (0.5, 0.0, 0.35), (-0.5, 0.0, 0.35)],
        [],
        [(0, 1, 2, 3)],
    )
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    tag(
        obj,
        stable_id,
        "port",
        aetheria__port__kind="thruster",
        aetheria__port__flow_axis="local:-Y",
        **properties,
    )
    return obj


def empty(name, collection, stable_id, role, location, display="ARROWS", **properties):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = location
    obj.empty_display_type = display
    obj.empty_display_size = 0.45
    return tag(obj, stable_id, role, **properties)


def build_fixture():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Aetheria Entity Authoring Fixture"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.frame_start = 1
    scene.frame_end = 40

    root = bpy.data.collections.new(ROOT_NAME)
    scene.collection.children.link(root)
    set_properties(
        root,
        aetheria__id="fixture.entity-authoring-v1",
        aetheria__role="entity-root",
        aetheria__schema=SCHEMA,
        aetheria__asset_id="aetheria/conformance/entity-authoring-v1",
        aetheria__asset_kind="fixture",
        aetheria__version="1.0.0",
        aetheria__units="metres",
        aetheria__handedness="right",
        aetheria__up_axis="+Z",
        aetheria__forward_axis="-Y",
    )

    visual = new_collection("VISUAL", root, "collection.visual", "visual")
    lod0 = new_collection("LOD0", visual, "collection.visual.lod0", "visual", aetheria__lod=0)
    lod1 = new_collection("LOD1", visual, "collection.visual.lod1", "visual", aetheria__lod=1)
    collision = new_collection("COLLISION", root, "collection.collision", "collision")
    destruction = new_collection("DESTRUCTION", root, "collection.destruction", "destruction")
    fracture_primary = new_collection(
        "FRACTURE_PRIMARY", destruction, "collection.destruction.primary", "destruction-set",
        aetheria__destruction__set_id="primary",
    )
    fracture_secondary = new_collection(
        "FRACTURE_SECONDARY", destruction, "collection.destruction.secondary", "destruction-set",
        aetheria__destruction__set_id="secondary",
    )
    sockets = new_collection("SOCKETS", root, "collection.sockets", "sockets")
    hardpoints = new_collection("HARDPOINTS", sockets, "collection.sockets.hardpoints", "sockets")
    thrusters = new_collection("THRUSTER_PORTS", sockets, "collection.sockets.thrusters", "sockets")
    missiles = new_collection("MISSILE_PORTS", sockets, "collection.sockets.missiles", "sockets")
    drones = new_collection("DRONE_PORTS", sockets, "collection.sockets.drones", "sockets")
    variants = new_collection("VARIANTS", root, "collection.variants", "variant-library")
    scout_variant = new_collection(
        "VARIANT_SCOUT", variants, "collection.variant.scout", "variant",
        aetheria__variant__variant_id="scout",
        aetheria__variant__hull_id="aetheria/conformance/entity-authoring-v1",
        aetheria__variant__display_name="Authoring Fixture Scout",
    )
    gunship_variant = new_collection(
        "VARIANT_GUNSHIP", variants, "collection.variant.gunship", "variant",
        aetheria__variant__variant_id="gunship",
        aetheria__variant__hull_id="aetheria/conformance/entity-authoring-v1",
        aetheria__variant__display_name="Authoring Fixture Gunship",
    )
    rig = new_collection("RIG", root, "collection.rig", "rig")
    animation = new_collection("ANIMATION", root, "collection.animation", "animation")
    preview = new_collection("PREVIEW", root, "collection.preview", "preview", aetheria__export=False)

    entity_root = empty("Entity Root", root, "node.root", "entity-root", (0.0, 0.0, 0.0), "PLAIN_AXES")

    hull_material = material(
        "MAT_FixtureHull",
        "material.fixture.hull",
        (0.08, 0.32, 0.55, 1.0),
        0.65,
        0.28,
        (0.0, 0.08, 0.24, 1.0),
    )
    fracture_material = bpy.data.materials.new("MAT_FractureDebug")
    fracture_material.diffuse_color = (0.8, 0.16, 0.05, 1.0)
    set_properties(fracture_material, aetheria__id="material.fixture.fracture", aetheria__role="material")

    hull = cube(
        "Hull LOD0", lod0, "visual.hull.lod0", "visual", (0.0, 0.0, 0.0), (4.0, 7.0, 1.4), hull_material,
        aetheria__visual__part="hull",
    )
    hull.parent = entity_root
    nose = cube(
        "Nose LOD0", lod0, "visual.nose.lod0", "visual", (0.0, -4.0, 0.0), (2.4, 1.5, 0.9), hull_material,
        aetheria__visual__part="nose",
    )
    nose.rotation_euler.z = math.radians(45.0)
    nose.parent = entity_root
    hull_lod1 = cube(
        "Hull LOD1", lod1, "visual.hull.lod1", "visual", (0.0, -0.2, 0.0), (3.7, 7.7, 1.2), hull_material,
        aetheria__visual__part="hull",
    )
    hull_lod1.parent = entity_root

    body_collision = cube(
        "COL Hull Box", collision, "collision.hull.box", "collision", (0.0, 0.2, 0.0), (3.8, 6.3, 1.25),
        aetheria__collider__shape="box",
        aetheria__collider__body_id="body.hull",
        aetheria__collider__trigger=False,
        aetheria__collider__material="aetheria.physics.hull",
    )
    body_collision.display_type = "WIRE"
    body_collision.hide_render = True
    body_collision.parent = entity_root
    sphere_collision = sphere(
        "COL Reactor Sphere", collision, "collision.reactor.sphere", "collision", (0.0, 1.2, 0.0), 0.9,
        aetheria__collider__shape="sphere",
        aetheria__collider__body_id="body.hull",
        aetheria__collider__trigger=False,
        aetheria__collider__material="aetheria.physics.reactor",
    )
    sphere_collision.display_type = "WIRE"
    sphere_collision.hide_render = True
    sphere_collision.parent = entity_root
    convex_collision = cube(
        "COL Nose Convex", collision, "collision.nose.convex", "collision", (0.0, -3.6, 0.0), (2.1, 1.8, 0.75),
        aetheria__collider__shape="convex",
        aetheria__collider__body_id="body.hull",
        aetheria__collider__trigger=False,
        aetheria__collider__material="aetheria.physics.hull",
    )
    convex_collision.rotation_euler.z = math.radians(45.0)
    convex_collision.display_type = "WIRE"
    convex_collision.hide_render = True
    convex_collision.parent = entity_root

    for set_collection, set_id, z_offset in (
        (fracture_primary, "primary", 0.0),
        (fracture_secondary, "secondary", -0.25),
    ):
        for index, x in enumerate((-1.45, -0.5, 0.5, 1.45)):
            piece = cube(
                f"Fracture {set_id.title()} {index}", set_collection,
                f"destruction.{set_id}.piece-{index}", "destruction-piece",
                (x, 0.0, z_offset), (0.85, 5.5, 0.55), fracture_material,
                aetheria__destruction__piece_id=f"{set_id}.piece-{index}",
                aetheria__destruction__body_id=f"debris.{set_id}.piece-{index}",
                aetheria__destruction__mass_fraction=0.25,
            )
            piece.hide_render = True
            piece.parent = entity_root

    for stable_id, label, accepts, size, location, variant in (
        ("socket.hardpoint.weapon-dorsal", "HP Weapon Dorsal", "weapon", "medium", (0.0, -0.4, 1.0), gunship_variant),
        ("socket.hardpoint.utility-port", "HP Utility Port", "utility", "small", (-2.1, 0.8, 0.0), scout_variant),
        ("socket.hardpoint.radiator-starboard", "HP Radiator Starboard", "radiator", "medium", (2.1, 0.8, 0.0), None),
    ):
        point = empty(
            label, hardpoints, stable_id, "socket", location,
            aetheria__socket__kind="hardpoint",
            aetheria__socket__accepts=accepts,
            aetheria__socket__size=size,
            aetheria__socket__mount_standard="aetheria.mount.equipment.v1",
            aetheria__socket__presentation_group=f"fixture.{accepts}",
            aetheria__region_id="region.hull",
        )
        point.parent = entity_root
        if variant is not None:
            variant.objects.link(point)

    for index, x in enumerate((-0.85, 0.85)):
        port = emitter_quad(
            f"PORT Thruster Main {index}", thrusters, f"port.thruster.main-{index}",
            (x, 3.55, 0.0),
            aetheria__port__group_id="thruster.main",
            aetheria__port__sequence=index,
        )
        port.parent = entity_root

    for index, x in enumerate((-0.75, 0.75)):
        port = empty(
            f"PORT Missile {index}", missiles, f"port.missile.bank-{index}", "port", (x, -4.8, -0.15),
            aetheria__port__kind="missile",
            aetheria__port__group_id="missile.bank",
            aetheria__port__sequence=index,
        )
        port.parent = entity_root

    drone_aperture = empty(
        "PORT Drone Bay", drones, "port.drone.bay", "port", (0.0, 2.8, -0.8),
        aetheria__port__kind="drone",
        aetheria__port__group_id="drone.bay",
        aetheria__port__sequence=0,
    )
    drone_aperture.parent = entity_root

    armature_data = bpy.data.armatures.new("RIG_FixtureArmature")
    armature = bpy.data.objects.new("RIG Fixture Armature", armature_data)
    rig.objects.link(armature)
    armature.parent = entity_root
    tag(armature, "rig.fixture", "armature")
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    root_bone = armature.data.edit_bones.new("root")
    root_bone.head = (0.0, 0.0, 0.0)
    root_bone.tail = (0.0, -1.0, 0.0)
    panel_bone = armature.data.edit_bones.new("port_panel_hinge")
    panel_bone.head = (-2.0, 0.8, 0.0)
    panel_bone.tail = (-2.0, 0.8, 1.0)
    panel_bone.parent = root_bone
    bpy.ops.object.mode_set(mode="OBJECT")
    set_properties(armature.data.bones["root"], aetheria__id="bone.root", aetheria__role="bone")
    set_properties(
        armature.data.bones["port_panel_hinge"],
        aetheria__id="bone.port-panel-hinge",
        aetheria__role="joint",
        aetheria__joint__kind="hinge",
        aetheria__joint__mode="physics",
        aetheria__joint__parent_body_id="body.hull",
        aetheria__joint__child_body_id="body.port-panel",
        aetheria__joint__axis="local:X",
        aetheria__joint__limit_min=0.0,
        aetheria__joint__limit_max=75.0,
    )

    panel = cube(
        "Articulated Port Panel", lod0, "visual.port-panel", "visual", (-2.0, 0.8, 0.4), (0.18, 2.2, 1.2), hull_material,
        aetheria__visual__part="articulated-panel",
    )
    panel.parent = armature
    modifier = panel.modifiers.new("Fixture Armature", "ARMATURE")
    modifier.object = armature
    group = panel.vertex_groups.new(name="port_panel_hinge")
    group.add(list(range(len(panel.data.vertices))), 1.0, "REPLACE")

    panel_collision = cube(
        "COL Port Panel", collision, "collision.port-panel.box", "collision", (-2.0, 0.8, 0.4), (0.22, 2.1, 1.1),
        aetheria__collider__shape="box",
        aetheria__collider__body_id="body.port-panel",
        aetheria__collider__trigger=False,
        aetheria__collider__material="aetheria.physics.hull",
    )
    panel_collision.display_type = "WIRE"
    panel_collision.hide_render = True
    panel_collision.parent = armature
    panel_collision.parent_type = "BONE"
    panel_collision.parent_bone = "port_panel_hinge"

    action = bpy.data.actions.new("ACT Port Panel Deploy")
    set_properties(
        action,
        aetheria__id="animation.port-panel-deploy",
        aetheria__role="animation-clip",
        aetheria__animation__loop=False,
    )
    armature.animation_data_create()
    armature.animation_data.action = action
    pose_bone = armature.pose.bones["port_panel_hinge"]
    pose_bone.rotation_mode = "XYZ"
    pose_bone.rotation_euler.x = 0.0
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=1)
    pose_bone.rotation_euler.x = math.radians(75.0)
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=24)
    animation["aetheria.action_id"] = "animation.port-panel-deploy"

    camera_data = bpy.data.cameras.new("Preview Camera")
    camera = bpy.data.objects.new("Preview Camera", camera_data)
    preview.objects.link(camera)
    camera.location = (11.0, 12.0, 8.0)
    camera.rotation_euler = (math.radians(63.0), 0.0, math.radians(138.0))
    tag(camera, "preview.camera", "preview")
    scene.camera = camera
    light_data = bpy.data.lights.new("Preview Key", "AREA")
    light_data.energy = 1200.0
    light_data.shape = "DISK"
    light_data.size = 6.0
    light = bpy.data.objects.new("Preview Key", light_data)
    preview.objects.link(light)
    light.location = (-4.0, -6.0, 9.0)
    tag(light, "preview.light", "preview")
    scale_ref = cube(
        "Preview Metre Cube", preview, "preview.metre-cube", "preview", (6.0, 2.0, 0.5), (1.0, 1.0, 1.0)
    )
    scale_ref.display_type = "WIRE"
    scale_ref.hide_render = True

    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    entity_root.select_set(True)
    bpy.context.view_layer.objects.active = entity_root
    return root


def main():
    build_fixture()
    output = repo_root() / "Asset Sources" / "Conformance" / "AetheriaEntityAuthoringV1.blend"
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False)
    print(f"AETHERIA_ENTITY_FIXTURE_WRITTEN {output}")


if __name__ == "__main__":
    main()
