"""Headless Blender smoke for Bake From Source, end to end on a real Tripo GLB.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_bake.py

Blender 5.2 is required: the tolerances below were measured on 5.2.2 (Cycles on the CPU, 256 x 256, one sample).
Environment as smoke_package.py's header says: CULTLIB_PACKAGES and SMOKE_GAME_FOLDER (a folder holding
GameData/Aetheria.cc and the published AetherDb under ModTools, no dotnet). The ship is created under
<game folder>/GameData/Mods/smoke.bake, which this smoke removes when it ends.

The Source is tests/fixtures/tripo-hull.glb imported unchanged: the glTF importer gives its material Metallic 1.0, which
bakes black unless the add-on neutralises it for the bake. The render mesh is a 10% Decimate copy with Smart UV Project.

Pass 1 bakes the render mesh and requires the albedo to match the texture's mean (it would be black from Metallic 1.0) and
the normal to be a tangent-space map, both packed in their colour spaces and wired, with the Source material, scene
settings and selection as they were; a bake that fails on its second call leaves everything as it was. Pass 2 bakes the
render mesh under a Bevel and Weighted Normal stack, then three render meshes that share one atlas, two without a material.
Pass 3 requires every refusal to name no input, change nothing and select the offenders, and a bake over wired images to
need replace. Pass 4 requires the images to survive Save As and reopen and ride Package into ship.glb at the Max texture
cap.
"""

import hashlib
import json
import os
import shutil
import struct
import sys
import tempfile
import traceback
import types
from pathlib import Path

import addon_utils
import bpy
import numpy

REPO = Path(__file__).resolve().parents[3]
FIXTURE = REPO / "tools" / "blender" / "tests" / "fixtures" / "tripo-hull.glb"
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
SHIP_ID = "smoke.bake"
COLLECTION = "Smoke Bake"
SIZE = [256]  # the bake size the images are expected at
TEXTURE_MEAN = (0.0754, 0.0692, 0.0746)  # the fixture's base colour texture, linear, measured in Blender 5.2.2
NAMES_NOT_ECHOED = [SHIP_ID, COLLECTION, "Hull", "Wing", "Fin", "Tripo Source", str(GAME), GAME.name]


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def run(operator, **arguments):
    """The operator's result and, for one that reported an error, the report (bpy.ops raises it)."""
    try:
        return operator(**arguments), ""
    except RuntimeError as exc:
        print(f"OPERATOR error: {exc}")
        return {"CANCELLED"}, str(exc)


def pixels(image):
    flat = numpy.empty(len(image.pixels), dtype=numpy.float32)
    image.pixels.foreach_get(flat)
    return flat.reshape(-1, 4)


def written(image):
    """Texels with any colour: the bake wrote them (an unwritten texel is black)."""
    return pixels(image)[:, :3].max(axis=1) > 0


def digest(image):
    return hashlib.sha256(pixels(image).tobytes()).hexdigest()


def bake_images():
    return bpy.data.images.get(f"{SHIP_ID}.albedo"), bpy.data.images.get(f"{SHIP_ID}.normal")


def check_maps(label, albedo, normal):
    """The tolerances measured on Blender 5.2.2: the albedo is the texture's linear mean (Image.pixels is linear) to 0.015 on at least 80% of the
    texels, the tangent normal has x and y at 0.5 to 0.02 and z in [0.75, 0.95]."""
    mean = pixels(albedo)[:, :3].mean(axis=0)
    covered = written(albedo).mean()
    print(f"{label} albedo mean {tuple(round(float(v), 4) for v in mean)} written {covered:.3f}")
    require(all(abs(float(m) - e) < 0.015 for m, e in zip(mean, TEXTURE_MEAN)), f"{label}: the albedo mean is {mean}, not {TEXTURE_MEAN}")
    require(covered >= 0.8, f"{label}: the albedo wrote only {covered:.2f} of the texels")
    x, y, z = pixels(normal)[:, :3].mean(axis=0)
    print(f"{label} normal mean {float(x):.4f} {float(y):.4f} {float(z):.4f}")
    require(abs(x - 0.5) < 0.02 and abs(y - 0.5) < 0.02 and 0.75 <= z <= 0.95, f"{label}: the normal mean is {(x, y, z)}")


def check_images(label, material_names):
    """Both images packed at the bake size in their colour spaces, wired as specified in every material."""
    albedo, normal = bake_images()
    require(albedo is not None and normal is not None, f"{label}: the baked images are missing")
    for image, space in ((albedo, "sRGB"), (normal, "Non-Color")):
        require(image.packed_file is not None, f"{label}: {image.name} is not packed")
        require(tuple(image.size) == (SIZE[0], SIZE[0]), f"{label}: {image.name} is {tuple(image.size)}")
        require(image.colorspace_settings.name == space, f"{label}: {image.name} is {image.colorspace_settings.name}")
    for material in (bpy.data.materials[name] for name in material_names):
        bsdf = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        base = bsdf.inputs["Base Color"].links[0].from_node
        require(base.type == "TEX_IMAGE" and base.image == albedo, f"{label}: Base Color is not the albedo")
        mapper = bsdf.inputs["Normal"].links[0].from_node
        require(mapper.type == "NORMAL_MAP", f"{label}: Normal does not come from a Normal Map node")
        texture = mapper.inputs["Color"].links[0].from_node
        require(texture.type == "TEX_IMAGE" and texture.image == normal, f"{label}: the Normal Map is not fed the normal")
    return albedo, normal


def wiring(materials):
    """Every node and link of the materials, with the image each Image Texture holds."""
    rows = []
    for material in materials:
        tree = material.node_tree
        rows.append((material.name,
                     sorted((node.name, node.image.session_uid if node.type == "TEX_IMAGE" and node.image else 0) for node in tree.nodes),
                     sorted((link.from_node.name, link.from_socket.identifier, link.to_node.name, link.to_socket.identifier,
                             link.is_muted) for link in tree.links)))
    return rows


def source_state(sources):
    """Each Source material's Principled Metallic and Transmission Weight and every link into them."""
    rows = []
    for obj in sources:
        for slot in obj.material_slots:
            for node in slot.material.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    for name in ("Metallic", "Transmission Weight"):
                        socket = node.inputs[name]
                        rows.append((slot.material.name, name, socket.default_value,
                                     [(link.from_node.name, link.is_muted) for link in socket.links]))
    return rows


def scene_state(scene):
    layer = bpy.context.view_layer
    return (scene.render.engine, scene.cycles.samples, scene.cycles.device,
            sorted(obj.name for obj in layer.objects if obj.select_get()), layer.objects.active.name if layer.objects.active else None)


def select(objects, active=None):
    layer = bpy.context.view_layer
    for obj in layer.objects:
        obj.select_set(obj in objects)
    layer.objects.active = active or objects[0]


def import_fixture():
    """The fixture's one mesh, alone in a new collection and parentless, at the transform the importer gave it."""
    scene = bpy.context.scene
    collection = bpy.data.collections.new(COLLECTION)
    scene.collection.children.link(collection)
    layer = bpy.context.view_layer
    layer.active_layer_collection = layer.layer_collection.children[COLLECTION]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(FIXTURE))
    new = [obj for obj in bpy.data.objects if obj not in before]
    meshes = [obj for obj in new if obj.type == "MESH"]
    require(len(meshes) == 1, f"The fixture imported {len(meshes)} meshes")
    mesh = meshes[0]
    world = mesh.matrix_world.copy()
    mesh.parent = None
    mesh.matrix_world = world
    for obj in new:
        if obj is not mesh:
            bpy.data.objects.remove(obj, do_unlink=True)
    for other in list(mesh.users_collection):
        if other != collection:
            other.objects.unlink(mesh)
    if mesh.name not in collection.objects:
        collection.objects.link(mesh)
    mesh.name = "Tripo Source"
    return collection, mesh


def make_render(collection, root, source, name):
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name, obj.data.name = name, name
    obj.data.materials.clear()
    collection.objects.link(obj)
    obj.parent = root
    obj.matrix_world = source.matrix_world.copy()
    return obj


def place_uv(obj, du, dv, su, sv):
    layer = obj.data.uv_layers[0]
    uv = numpy.empty(len(layer.data) * 2, dtype=numpy.float32)
    layer.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    uv[:, 0] = du + uv[:, 0] * su
    uv[:, 1] = dv + uv[:, 1] * sv
    layer.data.foreach_set("uv", uv.ravel())


def with_material(obj, name):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    obj.data.materials.append(material)
    return material


def glb_document(path):
    data = Path(path).read_bytes()
    json_length, _ = struct.unpack_from("<II", data, 12)
    return data, json.loads(data[20:20 + json_length]), 20 + json_length + 8


def glb_image_sizes(path):
    data, document, bin_start = glb_document(path)
    sizes = []
    for image in document.get("images", []):
        view = document["bufferViews"][image["bufferView"]]
        start = bin_start + view.get("byteOffset", 0)
        png = data[start:start + view["byteLength"]]
        require(png[:8] == b"\x89PNG\r\n\x1a\n", "A GLB image is not a PNG")
        sizes.append(struct.unpack_from(">II", png, 16))
    return sorted(sizes)


class FailingCall:
    """Stands in for a function: the n-th call raises, the others run the real one."""

    def __init__(self, real, fail_on):
        self.real, self.fail_on, self.calls = real, fail_on, 0

    def __call__(self, *args, **arguments):
        self.calls += 1
        if self.calls == self.fail_on:
            raise RuntimeError("probe: forced failure")
        return self.real(*args, **arguments)


def failing_pass(aetheria_ships, label, targets, materials, sources, replace, site, fail_on):
    """A run whose fail_on-th call of site raises (the Cycles bake, or one of the add-on's own commit steps) leaves the
    Source, the materials, the images, the scene settings and the selection as they were."""
    real_bpy, real_site = aetheria_ships.bpy, getattr(aetheria_ships, site, None)
    if site == "bake":
        patched = FailingCall(bpy.ops.object.bake, fail_on)
        proxy = {name: getattr(bpy, name) for name in dir(bpy) if not name.startswith("__")}
        proxy["ops"] = types.SimpleNamespace(object=types.SimpleNamespace(bake=patched))
        replacement = {"bpy": types.SimpleNamespace(**proxy)}
    else:
        patched = FailingCall(real_site, fail_on)
        replacement = {site: patched}

    def state():
        return (wiring(materials), source_state(sources), scene_state(bpy.context.scene), sorted(i.session_uid for i in bpy.data.images),
                [len(t.data.materials) for t in targets], sorted(m.name for m in bpy.data.materials))

    before = state()
    for name, value in replacement.items():
        setattr(aetheria_ships, name, value)
    try:
        result, message = run(bpy.ops.aetheria.bake_from_source, replace=replace)
    finally:
        aetheria_ships.bpy = real_bpy
        if site != "bake":
            setattr(aetheria_ships, site, real_site)
    require(patched.calls >= fail_on, f"{label}: the forced failure never landed ({patched.calls} calls)")
    require(result == {"CANCELLED"}, f"{label}: a failing run was not refused")
    if site == "bake":
        require("The bake failed" in message and "probe" not in message, f"{label}: Blender's own text reached the report ({message!r})")
    after = state()
    for part, name in enumerate(("wiring", "Source materials", "scene settings and selection", "images", "material slots", "materials")):
        require(before[part] == after[part], f"{label}: a failed run left the {name} changed")


def refusal(label, expected, snapshot, offenders=None):
    before = snapshot()
    result, message = run(bpy.ops.aetheria.bake_from_source)
    require(result == {"CANCELLED"}, f"{label}: was not refused")
    require(expected in message, f"{label}: the report is not the fixed text ({message!r})")
    for name in NAMES_NOT_ECHOED:
        require(name not in message, f"{label}: the report echoes an input ({name})")
    require(snapshot() == before, f"{label}: a refusal changed the scene")
    if offenders is not None:
        chosen = sorted(obj.name for obj in bpy.context.view_layer.objects if obj.select_get())
        require(chosen == sorted(obj.name for obj in offenders), f"{label}: the offenders are not the selection ({chosen})")


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    require(FIXTURE.is_file() and FIXTURE.stat().st_size > 1_000_000, "tripo-hull.glb is missing or an LFS pointer")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.game_folder = str(GAME)
    require(preferences.bake_size == 2048 and abs(preferences.bake_cage_extrusion - 0.05) < 1e-6
            and preferences.bake_max_ray_distance == 0.0, "The bake preference defaults moved")
    preferences.bake_size = SIZE[0]
    ship_folder = GAME / "GameData" / "Mods" / SHIP_ID
    require(not ship_folder.exists(), f"{ship_folder} exists; remove it first")
    try:
        passes_one_to_three(aetheria_ships, bpy.context.scene)
        pass_four(aetheria_ships)
    finally:
        shutil.rmtree(ship_folder, ignore_errors=True)


def passes_one_to_three(aetheria_ships, scene):
    collection, source = import_fixture()
    bsdf = next(node for node in source.material_slots[0].material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    require(bsdf.inputs["Metallic"].default_value == 1.0, "The importer no longer gives the fixture Metallic 1.0")
    # A linked Metallic and a Transmission Weight darken the bake just as the value does; both are neutralised and restored.
    source_tree = source.material_slots[0].material.node_tree
    one = source_tree.nodes.new("ShaderNodeValue")
    one.outputs[0].default_value = 1.0
    source_tree.links.new(one.outputs[0], bsdf.inputs["Metallic"])
    bsdf.inputs["Transmission Weight"].default_value = 1.0
    select([source])
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = SHIP_ID, "Smoke Bake"
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", 10
    result, message = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    root = bpy.data.objects["Ship Root"]
    require(source.name in aetheria_ships._child(collection, "Source").all_objects, "The fixture did not land in Source")

    hull = make_render(collection, root, source, "Hull")
    hull.modifiers.new("Decimate", "DECIMATE").ratio = 0.1
    select([hull])
    bpy.ops.object.modifier_apply(modifier="Decimate")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project()
    bpy.ops.object.mode_set(mode="OBJECT")
    hull_material = with_material(hull, "Hull Paint")
    paint = hull_material.node_tree.nodes.new("ShaderNodeRGB")  # her own Base Color link, which a failed bake puts back
    hull_material.node_tree.links.new(paint.outputs[0], hull_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    scene.render.engine, scene.cycles.samples, scene.cycles.device = "BLENDER_EEVEE", 77, "GPU"
    select([hull])
    sources = [source]

    # Pass 1: the albedo is not black, the normal is tangent space, and a failed bake changes nothing.
    failing_pass(aetheria_ships, "Pass 1 failing bake", [hull], [hull_material], sources, False, "bake", 2)
    failing_pass(aetheria_ships, "Pass 1 failing wiring", [hull], [hull_material], sources, False, "_link_into", 2)
    require(bake_images() == (None, None), "Pass 1: a failed bake left images behind")
    state_source, state_scene = source_state(sources), scene_state(scene)
    result, message = run(bpy.ops.aetheria.bake_from_source)
    require(result == {"FINISHED"}, f"Pass 1: Bake From Source was refused: {message}")
    albedo, normal = check_images("Pass 1", [hull_material.name])
    check_maps("Pass 1", albedo, normal)
    require(source_state(sources) == state_source and bsdf.inputs["Metallic"].default_value == 1.0,
            "Pass 1: the Source material was left changed")
    require(scene_state(scene) == state_scene, "Pass 1: the scene settings or selection were left changed")
    print("PASS 1 tripo bake ok")

    # Pass 2: the evaluated render mesh bakes; three meshes of one atlas bake into one pair.
    hull.modifiers.new("Bevel", "BEVEL").width = 0.05
    hull.modifiers.new("Weighted Normal", "WEIGHTED_NORMAL")
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the modifier stack bake was refused: {message}")
    albedo, normal = check_images("Pass 2 evaluated", [hull_material.name])
    check_maps("Pass 2 evaluated", albedo, normal)
    hull.modifiers.clear()
    z_default = float(pixels(normal)[:, 2].mean())
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.bake_cage_extrusion = 0.02
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the 0.02 cage bake was refused: {message}")
    z_tight = float(pixels(bake_images()[1])[:, 2].mean())
    preferences.bake_cage_extrusion = 0.1
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the 0.1 cage bake was refused: {message}")
    z_open = float(pixels(bake_images()[1])[:, 2].mean())
    preferences.bake_max_ray_distance = 1.0
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the 1 m ray bake was refused: {message}")
    z_short = float(pixels(bake_images()[1])[:, 2].mean())
    preferences.bake_cage_extrusion, preferences.bake_max_ray_distance = 0.05, 0.0
    print(f"PASS 2 preferences: normal z {z_tight:.4f} at cage 0.02, {z_default:.4f} at 0.05 (evaluated), {z_open:.4f} at 0.1, "
          f"{z_short:.4f} at 0.1 with a 1 m ray")
    # Measured on Blender 5.2.2 at 256: 0.908 at 0.02 m, 0.833 at 0.1 m, 0.547 with the 1 m ray; windows of 0.02 either side.
    require(abs(z_tight - 0.908) < 0.02 and abs(z_open - 0.833) < 0.02 and z_short < z_open - 0.1,
            "Pass 2: the cage extrusion and max ray distance do not decide the bake")

    wing, fin = make_render(collection, root, source, "Wing"), make_render(collection, root, source, "Fin")
    for obj in (wing, fin):
        obj.data = hull.data.copy()
        obj.data.name = obj.name
        obj.data.materials.clear()
    place_uv(hull, 0.0, 0.0, 0.5, 1.0)
    place_uv(wing, 0.5, 0.0, 0.5, 0.5)
    place_uv(fin, 0.5, 0.5, 0.5, 0.5)
    select([hull])
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the left-half bake was refused: {message}")
    select([hull, wing, fin], active=hull)
    failing_pass(aetheria_ships, "Pass 2 failing with bare meshes", [hull, wing, fin], [hull_material], sources, True, "bake", 2)
    select([hull])
    alone = written(bake_images()[0]).reshape(SIZE[0], SIZE[0])  # rows are v, columns are u
    select([hull, wing, fin], active=hull)
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 2: the shared bake was refused: {message}")
    shared = wing.data.materials[0]
    require(shared.name == f"{SHIP_ID}.hull" and fin.data.materials[0] == shared
            and len([m for m in bpy.data.materials if m.name.startswith(f"{SHIP_ID}.hull")]) == 1,
            "Pass 2: the meshes without a material did not share one hull material")
    albedo, normal = check_images("Pass 2 shared", [hull_material.name, shared.name])
    both = written(albedo).reshape(SIZE[0], SIZE[0])
    left_alone, left_both = int(alone[:, :SIZE[0] // 2].sum()), int(both[:, :SIZE[0] // 2].sum())
    print(f"PASS 2 shared: left half {left_alone} alone, {left_both} with the others; total {int(alone.sum())} to {int(both.sum())}")
    require(both.sum() >= 1.3 * alone.sum(), "Pass 2: the second and third meshes did not add texels")
    require(left_both >= 0.98 * left_alone, "Pass 2: a later mesh cleared the first mesh's texels")
    print("PASS 2 evaluated and shared ok")

    # Pass 3: refusals name no input, change nothing and select the offenders; wired images need replace.
    renders, materials = [hull, wing, fin], [hull_material, shared]

    def snapshot():
        return (len(bpy.data.images), wiring(materials), scene.render.engine, scene.cycles.samples, source_state(sources))

    select([hull, wing], active=hull)
    layer = hull.data.uv_layers[0]
    uv_name = layer.name
    hull.data.uv_layers.remove(layer)
    refusal("No UV layer", "have no UV layer", snapshot, offenders=[hull])
    hull.data.uv_layers.new(name=uv_name)
    select([hull])
    bpy.ops.object.mode_set(mode="EDIT")
    refusal("Edit mode", "Object mode", snapshot)
    bpy.ops.object.mode_set(mode="OBJECT")
    source.hide_set(True)
    refusal("Hidden Source", "Source meshes are hidden", snapshot)
    source.hide_set(False)
    select([hull])
    for obj in renders:
        obj.select_set(False)
    refusal("Nothing selected", "Select the render meshes", snapshot)
    select([hull, wing], active=hull)
    tree = hull_material.node_tree
    principled = next(node for node in tree.nodes if node.type == "BSDF_PRINCIPLED")
    position = principled.location.copy()
    tree.nodes.remove(principled)  # a material without a Principled BSDF
    refusal("No Principled BSDF", "without a Principled BSDF", lambda: (len(bpy.data.images), len(tree.nodes), scene.render.engine),
            offenders=[hull])
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = position
    tree.links.new(next(n for n in tree.nodes if n.type == "TEX_IMAGE" and n.image == bake_images()[0]).outputs["Color"],
                   principled.inputs["Base Color"])
    tree.links.new(next(n for n in tree.nodes if n.type == "NORMAL_MAP").outputs["Normal"], principled.inputs["Normal"])
    tree.links.new(principled.outputs["BSDF"], next(n for n in tree.nodes if n.type == "OUTPUT_MATERIAL").inputs["Surface"])
    select(renders, active=hull)

    old_albedo, old_normal = bake_images()
    old_digest = (digest(old_albedo), digest(old_normal))
    old_uids = (old_albedo.session_uid, old_normal.session_uid)
    before = wiring(materials)
    result, message = run(bpy.ops.aetheria.bake_from_source)
    require(result == {"CANCELLED"} and "confirm the replace" in message, f"Pass 3: a bake over wired images was not refused: {message}")
    require(wiring(materials) == before and (digest(old_albedo), digest(old_normal)) == old_digest and bake_images() == (old_albedo, old_normal),
            "Pass 3: a refused replace changed the images or the wiring")
    failing_pass(aetheria_ships, "Pass 3 failing replace bake", renders, materials, sources, True, "bake", 2)
    failing_pass(aetheria_ships, "Pass 3 failing replace wiring", renders, materials, sources, True, "_remove_node", 1)
    require(bake_images() == (old_albedo, old_normal) and (digest(old_albedo), digest(old_normal)) == old_digest,
            "Pass 3: a failed replace changed the old images")
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 3: replace was refused: {message}")
    new_albedo, new_normal = check_images("Pass 3 replaced", [m.name for m in materials])
    require((new_albedo.session_uid, new_normal.session_uid) != old_uids, "Pass 3: replace kept the old datablocks")
    require(not any(image.session_uid in old_uids for image in bpy.data.images), "Pass 3: the replaced images are still in bpy.data")
    require(len([i for i in bpy.data.images if i.name.startswith(SHIP_ID)]) == 2, "Pass 3: the ship has other than two baked images")
    print("PASS 3 refusals, replace and rollback ok")


def pass_four(aetheria_ships):
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    SIZE[0] = preferences.bake_size = 512
    preferences.max_texture = 256
    result, message = run(bpy.ops.aetheria.bake_from_source, replace=True)
    require(result == {"FINISHED"}, f"Pass 4: the 512 bake was refused: {message}")
    materials = ["Hull Paint", f"{SHIP_ID}.hull"]
    check_images("Pass 4", materials)
    mesh = bpy.data.meshes.new("Hull Collider")  # the validator wants one hull-collider anchor
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)], [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
    collider = bpy.data.objects.new("Hull Collider", mesh)
    collider["aetheria.role"], collider["aetheria.id"] = "hull-collider", "collider"
    bpy.data.collections[COLLECTION].objects.link(collider)
    collider.parent = bpy.data.objects["Ship Root"]
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Pass 4: Save Layout was refused: {message}")
    blend = Path(tempfile.mkdtemp(prefix="aetheria-smoke-bake-")) / "ship.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    check_images("Pass 4 reopened", materials)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"}, f"Pass 4: Package was refused: {report}")
    glb = GAME / "GameData" / "Mods" / SHIP_ID / "ship.glb"
    require(glb_image_sizes(glb) == [(256, 256), (256, 256)], f"Pass 4: the GLB images are {glb_image_sizes(glb)}")
    _, document, _ = glb_document(glb)
    textured = [m for m in document["materials"] if "baseColorTexture" in m.get("pbrMetallicRoughness", {}) and "normalTexture" in m]
    require(len(textured) == 2, f"Pass 4: {len(textured)} GLB materials hold both textures")
    node_names = {node.get("name") for node in document["nodes"]}
    require("Tripo Source" not in node_names and "Grid" not in node_names, "Pass 4: the GLB holds Source or the Grid")
    for name in (f"{SHIP_ID}.albedo", f"{SHIP_ID}.normal"):
        require(tuple(bpy.data.images[name].size) == (512, 512), "Pass 4: Package changed the .blend's own image")
    print("PASS 4 save, reopen and package ok")


try:
    main()
except SystemExit as stop:
    print(f"SMOKE_BAKE_FAILED: {stop}", flush=True)
    os._exit(1)
except Exception:
    traceback.print_exc()
    print("SMOKE_BAKE_FAILED", flush=True)
    os._exit(1)
print("SMOKE_BAKE_OK")
sys.stdout.flush()
os._exit(0)
