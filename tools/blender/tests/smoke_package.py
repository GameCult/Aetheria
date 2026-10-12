"""Headless Blender smoke for the Package Ship action, end to end.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_package.py

Environment: CULTLIB_PACKAGES (CultLib's packages directory, as test_ship_cc.py) and SMOKE_GAME_FOLDER, a folder holding
GameData/Aetheria.cc and the published AetherDb under ModTools (AetherDb.exe on Windows), which the smoke sets as the
add-on's Game folder. No dotnet is needed: the add-on runs the published tool and nothing else.
SMOKE_MODS names the mods directory to write into (default: a new temp directory); the package is left there so the
Unity play smoke can load it, and its path is printed as SMOKE_PACKAGE=<ship.cc>.

Pass 1 packages a primitive ship built from role-tagged objects and requires the C# validator to accept it. Pass 2 swaps
the thruster disc for an empty of the same id and requires the validator to refuse it, naming the anchor. Pass 3 puts
the disc back and packages again, so the package left behind is the good one. Pass 4 points Game folder at a folder
that holds Aetheria.cc but no ModTools/AetherDb, and at one that holds a ModTools tool but no Aetheria.cc, and
requires Package to report the fixed preference text and no path.

Before packaging, the bind cases: a bind that fails while computing the stored path leaves no aetheria.* property; an
unsaved .blend stores the absolute path, which still resolves after Save As to two other folders; a saved .blend on the
.cc's drive stores a '//'-relative path. SMOKE_OTHER_DRIVE names a directory on another drive than SMOKE_MODS; when set,
the .blend is saved there before the final bind, so the bind crosses drives and the package passes prove it resolves.
"""

import array
import contextlib
import io
import json
import os
import struct
import sys
import tempfile
import traceback
from pathlib import Path

import addon_utils
import bpy

REPO = Path(__file__).resolve().parents[3]
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
SHIP_ID = "smoke.gale"


def tetrahedron(name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)], [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
    return mesh


def quad(name, size=1.0):
    mesh = bpy.data.meshes.new(name)
    half = size / 2
    mesh.from_pydata([(-half, -half, 0), (half, -half, 0), (half, half, 0), (-half, half, 0)], [], [(0, 1, 2, 3)])
    return mesh


def tagged(collection, name, data, role=None, anchor_id=None, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    if role:
        obj["aetheria.role"] = role
        obj["aetheria.id"] = anchor_id
    obj.parent = parent
    collection.objects.link(obj)
    return obj


def textured(obj, name, width, height):
    """Gives the object a material whose base colour is a generated width x height image; returns the image."""
    image = bpy.data.images.new(name, width, height)
    image.pixels[0] = 0.5  # a dirty generated image is what the exporter encodes
    obj.data.uv_layers.new(name="UVMap")
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    texture = tree.nodes.new("ShaderNodeTexImage")
    texture.image = image
    tree.links.new(texture.outputs["Color"], tree.nodes["Principled BSDF"].inputs["Base Color"])
    obj.data.materials.append(material)
    return image


def glb_image_sizes(glb):
    """The (width, height) of each PNG the GLB embeds, read from the PNG headers in its binary chunk."""
    data = Path(glb).read_bytes()
    json_length, _ = struct.unpack_from("<II", data, 12)
    document = json.loads(data[20:20 + json_length])
    bin_start = 20 + json_length + 8
    sizes = []
    for image in document.get("images", []):
        view = document["bufferViews"][image["bufferView"]]
        start = bin_start + view.get("byteOffset", 0)
        png = data[start:start + view["byteLength"]]
        if image.get("mimeType") != "image/png" or png[:8] != b"\x89PNG\r\n\x1a\n":
            raise SystemExit("A GLB image is not a PNG")
        sizes.append(struct.unpack_from(">II", png, 16))
    return sorted(sizes)


def texture_cap_pass(addon, hull_object, albedo, detail, limit, expected, glb):
    """Package at the Max texture preference limit; the GLB holds exactly the expected image sizes, and the .blend's own
    images are untouched and no scaled copy is left in bpy.data.images."""
    bpy.context.preferences.addons["aetheria_ships"].preferences.max_texture = limit
    names = sorted(image.name for image in bpy.data.images)
    result, report = package(addon)
    if result != {"FINISHED"}:
        raise SystemExit(f"Texture cap {limit}: Package was refused")
    if glb_image_sizes(glb) != sorted(expected):
        raise SystemExit(f"Texture cap {limit}: the GLB images are {glb_image_sizes(glb)}, not {sorted(expected)}")
    if tuple(albedo.size) != (4096, 1000) or tuple(detail.size) != (256, 256):
        raise SystemExit(f"Texture cap {limit}: Package changed a .blend image to {tuple(albedo.size)}, {tuple(detail.size)}")
    nodes = [node.image for slot in hull_object.material_slots for node in slot.material.node_tree.nodes
             if node.type == "TEX_IMAGE"]
    if nodes != [albedo, detail]:
        raise SystemExit(f"Texture cap {limit}: a material node does not hold its original image")
    if sorted(image.name for image in bpy.data.images) != names:
        raise SystemExit(f"Texture cap {limit}: a scaled copy is left in bpy.data.images")


def expected_cap_size(width, height, limit):
    """The longest side at the cap, the other side scaled by the same ratio and rounded half up; untouched at or under the
    cap. Integer arithmetic, so it shares nothing with the add-on's float rounding."""
    longest = max(width, height)
    if longest <= limit:
        return (width, height)
    return (max(1, (2 * width * limit + longest) // (2 * longest)), max(1, (2 * height * limit + longest) // (2 * longest)))


def texture_boundary_cases(addon, hull_object, glb):
    """Each case gives the hull one material whose image is width x height (generated, file-backed, packed, or inside a
    node group) and packages at cap 256: the GLB holds exactly the expected size, the image is as it was (size, pixels,
    node identity), and no copy is left. Cases sit exactly at the cap, one over, on the long side being the height, on
    sizes where rounding and flooring differ, and on a tall image whose width rounds up."""
    bpy.context.preferences.addons["aetheria_ships"].preferences.max_texture = limit = 256
    original_materials = list(hull_object.data.materials)
    folder = Path(tempfile.mkdtemp(prefix="aetheria-smoke-cap-"))
    cases = [("generated", 256, 128), ("generated", 257, 125), ("generated", 257, 128), ("generated", 256, 256),
             ("generated", 255, 255), ("generated", 375, 376), ("generated", 125, 513), ("generated", 200, 513), ("generated", 1000, 4097),
             ("file", 513, 400), ("file", 256, 257), ("packed", 513, 400), ("packed", 258, 70), ("group", 513, 400),
             ("group", 125, 513)]
    try:
        for kind, width, height in cases:
            label = f"{kind} {width}x{height}"
            while hull_object.data.materials:
                hull_object.data.materials.pop(index=0)
            image = bpy.data.images.new("CapCase", width, height)
            image.pixels.foreach_set([((k * 7) % 251) / 251.0 for k in range(width * height * 4)])
            if kind in ("file", "packed"):
                image.filepath_raw = str(folder / f"{kind}_{width}_{height}.png")
                image.file_format = "PNG"
                image.save()
                bpy.data.images.remove(image)
                image = bpy.data.images.load(str(folder / f"{kind}_{width}_{height}.png"))
                image.name = "CapCase"
                if kind == "packed":
                    image.pack()
            material = bpy.data.materials.new("CapCase")
            material.use_nodes = True
            tree = material.node_tree
            if kind == "group":
                group = bpy.data.node_groups.new("CapGroup", "ShaderNodeTree")
                group.interface.new_socket("Color", in_out="OUTPUT", socket_type="NodeSocketColor")
                texture = group.nodes.new("ShaderNodeTexImage")
                texture.image = image
                group.links.new(texture.outputs["Color"], group.nodes.new("NodeGroupOutput").inputs[0])
                holder = tree.nodes.new("ShaderNodeGroup")
                holder.node_tree = group
                tree.links.new(holder.outputs[0], tree.nodes["Principled BSDF"].inputs["Base Color"])
            else:
                texture = tree.nodes.new("ShaderNodeTexImage")
                texture.image = image
                tree.links.new(texture.outputs["Color"], tree.nodes["Principled BSDF"].inputs["Base Color"])
            hull_object.data.materials.append(material)
            pixels = bytes(array.array("f", image.pixels[:]).tobytes())
            names = sorted(i.name for i in bpy.data.images)
            result, report = package(addon)
            if result != {"FINISHED"}:
                raise SystemExit(f"Texture case {label}: Package was refused")
            if glb_image_sizes(glb) != [expected_cap_size(width, height, limit)]:
                raise SystemExit(f"Texture case {label}: the GLB image is {glb_image_sizes(glb)}, "
                                 f"not {expected_cap_size(width, height, limit)}")
            if (tuple(image.size) != (width, height) or bytes(array.array("f", image.pixels[:]).tobytes()) != pixels
                    or texture.image != image or sorted(i.name for i in bpy.data.images) != names):
                raise SystemExit(f"Texture case {label}: the .blend's image changed or a copy is left behind")
            if kind == "file" and Path(image.filepath_raw).exists() is False:
                raise SystemExit(f"Texture case {label}: the source file is gone")
        # An exporter that fails (the GLB's path is a directory) still restores the originals and deletes the copies.
        names = sorted(i.name for i in bpy.data.images)
        glb.unlink()
        glb.mkdir()
        try:
            result, report = package(addon)
        finally:
            glb.rmdir()
        if result != {"CANCELLED"}:
            raise SystemExit("Texture failure case: the export into a directory did not fail")
        if tuple(image.size) != (width, height) or texture.image != image or sorted(i.name for i in bpy.data.images) != names:
            raise SystemExit("Texture failure case: a failed export left a scaled copy or a swapped node")
    finally:
        while hull_object.data.materials:
            hull_object.data.materials.pop(index=0)
        for material in original_materials:
            hull_object.data.materials.append(material)
        bpy.context.preferences.addons["aetheria_ships"].preferences.max_texture = 2048


BIND_KEYS = ("aetheria.asset_kind", "aetheria.id", "aetheria.ship_cc")


def same_file(a, b):
    return os.path.normcase(os.path.normpath(a)) == os.path.normcase(os.path.normpath(b))


def bind(ship, label):
    for key in BIND_KEYS:
        if key in ship:
            del ship[key]
    try:
        result = bpy.ops.aetheria.bind_ship_collection()
    except RuntimeError:
        result = {"CANCELLED"}
    stored = ship.get("aetheria.ship_cc")
    print(f"BIND {label} {sorted(result)}: blend={bpy.data.filepath!r} stored={stored!r}")
    return result, stored


def bind_cases(ship, path, mods):
    if bpy.data.filepath:
        raise SystemExit("Bind cases need an unsaved .blend to start from")
    result, stored = bind(ship, "unsaved")
    if result != {"FINISHED"} or not os.path.isabs(stored) or not same_file(stored, path):
        raise SystemExit("Bind case unsaved: the stored path is not the absolute .cc path")
    for folder in ("blend-a", "blend-b"):
        (mods / folder).mkdir()
        bpy.ops.wm.save_as_mainfile(filepath=str(mods / folder / "ship.blend"))
        if not same_file(bpy.path.abspath(ship["aetheria.ship_cc"]), path):
            raise SystemExit(f"Bind case unsaved: the stored path stopped resolving after Save As to {folder}")

    relpath = bpy.path.relpath
    bpy.path.relpath = lambda *args, **kwargs: (_ for _ in ()).throw(OSError("probe: relpath failed"))
    try:
        result, _ = bind(ship, "failing")
    finally:
        bpy.path.relpath = relpath
    if result != {"CANCELLED"} or any(key in ship for key in BIND_KEYS):
        raise SystemExit("Bind case failing: a failed bind left aetheria.* properties behind")

    result, stored = bind(ship, "same-drive")
    if result != {"FINISHED"} or not stored.startswith("//") or not same_file(bpy.path.abspath(stored), path):
        raise SystemExit("Bind case same-drive: the stored path is not a resolving '//'-relative path")

    # relpath raises ValueError across drives: the bind completes with the absolute path, without a second drive.
    bpy.path.relpath = lambda *args, **kwargs: (_ for _ in ()).throw(ValueError("probe: path is on mount 'D:'"))
    try:
        result, stored = bind(ship, "cross-drive-forced")
    finally:
        bpy.path.relpath = relpath
    if result != {"FINISHED"} or not os.path.isabs(stored) or not same_file(stored, path):
        raise SystemExit("Bind case cross-drive-forced: the stored path is not the absolute .cc path")

    # A value the ID-property store refuses leaves the collection exactly as it was, bound or not. A bound collection is
    # rebound to a different .cc, so a rollback that puts the keys back but not their values leaves the new path behind.
    import aetheria_ships
    rebound = str(Path(path).with_name("rebound.cc"))
    for label, ship_id, pending in (("bad-id", object(), None), ("bad-pending", SHIP_ID, [object()])):
        for key in list(ship.keys()):
            if key.startswith("aetheria."):
                del ship[key]
        for bound in (False, True):
            if bound:
                aetheria_ships._bind_collection(ship, path, SHIP_ID, ["Old", "Ref"])
            before = {key: ship[key] for key in ship.keys() if key.startswith("aetheria.")}
            before = {key: (value.to_list() if hasattr(value, "to_list") else value) for key, value in before.items()}
            try:
                aetheria_ships._bind_collection(ship, rebound if bound else path, ship_id, pending)
            except Exception:
                pass
            else:
                raise SystemExit(f"Bind case {label}: the store did not refuse the value")
            after = {key: ship[key] for key in ship.keys() if key.startswith("aetheria.")}
            after = {key: (value.to_list() if hasattr(value, "to_list") else value) for key, value in after.items()}
            if after != before:
                raise SystemExit(f"Bind case {label} bound={bound}: a refused bind left the collection half-bound")
    aetheria_ships._bind_collection(ship, path, SHIP_ID)
    for key in BIND_KEYS:
        if key not in ship:
            raise SystemExit("Bind case rebind: the collection lost its binding")
    if "aetheria.pending" in ship:
        raise SystemExit("Bind case rebind: a bind without pending kept the old pending")

    other = os.environ.get("SMOKE_OTHER_DRIVE")
    if not other:
        print("BIND cross-drive skipped: SMOKE_OTHER_DRIVE is not set")
        return
    if os.path.splitdrive(other)[0].lower() == os.path.splitdrive(path)[0].lower():
        raise SystemExit("SMOKE_OTHER_DRIVE is on the same drive as SMOKE_MODS")
    Path(other).mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(Path(other) / "ship.blend"))
    result, stored = bind(ship, "cross-drive")
    if result != {"FINISHED"} or not os.path.isabs(stored) or not same_file(stored, path):
        raise SystemExit("Bind case cross-drive: the stored path is not the absolute .cc path")


CONSOLE = []  # what the last package() sent to the system console


def package(addon):
    # An operator that reports an error raises it out of bpy.ops; the panel's report is the verdict either way.
    console = io.StringIO()
    try:
        with contextlib.redirect_stdout(console):
            result = bpy.ops.aetheria.package_ship()
    except RuntimeError:
        result = {"CANCELLED"}
    CONSOLE[:] = [console.getvalue()]
    report = bpy.context.scene.aetheria_package_report
    print(f"PACKAGE {sorted(result)}: {report}{CONSOLE[0]}")
    return result, report


def main():
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    # Brokkr is the Blender host's CultLib path provider and is not installed in a factory-startup Blender.
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(GAME)

    mods = Path(os.environ.get("SMOKE_MODS") or tempfile.mkdtemp(prefix="aetheria-smoke-mods-"))
    path = str(mods / SHIP_ID / "ship.cc")
    if Path(path).exists():
        raise SystemExit(f"{path} already exists; point SMOKE_MODS at an empty directory")
    code, message = aetheria_ships._aetherdb(bpy.context, "create", path, SHIP_ID, "Smoke Gale", "--like", "Djinni")
    print(f"CREATE {code}: {message}")
    if code != 0:
        raise SystemExit("create --like Djinni failed")

    scene = bpy.context.scene
    ship = bpy.data.collections.new("Smoke Gale")
    scene.collection.children.link(ship)
    hull = tagged(ship, "Hull", tetrahedron("Hull"))
    albedo = textured(hull, "Hull Albedo", 4096, 1000)
    detail = textured(hull, "Hull Detail", 256, 256)
    hull.data.polygons[0].material_index = 1  # the exporter writes only materials some face uses
    tagged(ship, "Map Icon", quad("Map Icon"), "map-icon", "map", (0, 0, -1))
    tagged(ship, "Hull Collider", tetrahedron("Hull Collider"), "hull-collider", "collider")
    tagged(ship, "Shield", None, "shield", "shield")
    tagged(ship, "Tractor", None, "tractor", "tractor", (0, 2, 0))
    disc = tagged(ship, "Thruster", quad("Thruster", 0.4), "thruster-emitter", "thruster", (0, -1, 0))
    gun = tagged(ship, "Gun", None, "weapon-mount", "gun", (1, 0, 0))
    tagged(ship, "Gun Muzzle", None, "weapon-muzzle", "gun.muzzle", (0, 0.5, 0), parent=gun)
    tagged(ship, "Radiator", quad("Radiator", 0.5), "radiator-mesh", "radiator", (-1, 0, 0))

    scene.aetheria_ship_cc_path = path
    bpy.context.view_layer.objects.active = hull
    bind_cases(ship, path, mods)

    ship_cc = aetheria_ships.ship_cc
    _, _, revision = ship_cc.read_layout(path, PACKAGES)

    def hardpoint(kind, x, mount):
        return ship_cc.encode_hardpoint(Type=ship_cc.HARDPOINT_TYPE_NAMES.index(kind), Position=[x, 0],
                                        Shape=[[1, 1, [True]]], Transform=mount, Rotation=0, Armor=0.0, FiringArc=0.0)

    ship_cc.replace_layout(path, PACKAGES, SHIP_ID, revision, [4, 1, [True] * 4], [
        hardpoint("Thruster", 0, "thruster"), hardpoint("Radiator", 1, "radiator"),
        hardpoint("Energy", 2, "gun"), hardpoint("Reactor", 3, "reactor")])

    objects_before = sorted(obj.name for obj in bpy.data.objects)
    result, report = package(aetheria_ships)
    if result != {"FINISHED"}:
        raise SystemExit("Pass 1: the validator refused the primitive ship")
    # The texture cap: the 4096 x 1000 map exports at 2048 x 500 (the longest side at the cap, the aspect kept), the
    # 256 x 256 one is under the cap and exports as it is, and the preference decides the cap.
    glb = Path(path).with_name("ship.glb")
    texture_cap_pass(aetheria_ships, hull, albedo, detail, 2048, [(2048, 500), (256, 256)], glb)
    texture_cap_pass(aetheria_ships, hull, albedo, detail, 512, [(512, 125), (256, 256)], glb)
    bpy.context.preferences.addons["aetheria_ships"].preferences.max_texture = 2048
    texture_boundary_cases(aetheria_ships, hull, glb)
    print("TEXTURE_CAP_OK")
    # A collection with no Ship Root is packaged exactly as before: the frame step makes and moves nothing.
    if sorted(obj.name for obj in bpy.data.objects) != objects_before or any(
            child.get("aetheria.frame") for child in ship.children):
        raise SystemExit("Pass 1: Package of a collection with no Ship Root created objects or a Generated collection")

    ship.objects.unlink(disc)
    hollow = tagged(ship, "Thruster Empty", None, "thruster-emitter", "thruster", (0, -1, 0))
    result, report = package(aetheria_ships)
    if result != {"CANCELLED"} or "the validator refused the ship" not in report or "thruster" in report or SHIP_ID in report:
        raise SystemExit("Pass 2: an empty thruster node was not refused with the fixed text")
    if "thruster-emitter anchor thruster needs a mesh" not in CONSOLE[0]:
        raise SystemExit("Pass 2: the validator's message naming the anchor did not reach the system console")

    ship.objects.unlink(hollow)
    ship.objects.link(disc)
    result, report = package(aetheria_ships)
    if result != {"FINISHED"}:
        raise SystemExit("Pass 3: the restored ship was refused")

    bare = Path(tempfile.mkdtemp(prefix="aetheria-smoke-bare-"))
    (bare / "GameData").mkdir()
    (bare / "GameData" / "Aetheria.cc").write_bytes(b"")
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(bare)
    result, report = package(aetheria_ships)
    if result != {"CANCELLED"} or report != "Package failed: " + aetheria_ships.GAME_FOLDER_REFUSAL \
            or "Game folder" not in report or str(bare) in report or str(GAME) in report:
        raise SystemExit("Pass 4: a game folder without ModTools/AetherDb was not refused with the fixed text")
    # A tool under ModTools does not make a folder a game folder: GameData/Aetheria.cc is the marker.
    unmarked = Path(tempfile.mkdtemp(prefix="aetheria-smoke-unmarked-"))
    (unmarked / "ModTools").mkdir()
    (unmarked / "ModTools" / ("AetherDb.exe" if sys.platform == "win32" else "AetherDb")).write_bytes(b"")
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(unmarked)
    result, report = package(aetheria_ships)
    if result != {"CANCELLED"} or report != "Package failed: " + aetheria_ships.GAME_FOLDER_REFUSAL:
        raise SystemExit("Pass 4: a folder without GameData/Aetheria.cc was not refused with the fixed text")
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(GAME)
    print(f"SMOKE_PACKAGE={path}")
    print("SMOKE_PACKAGE_OK")


try:
    main()
except SystemExit as stop:
    print(f"SMOKE_PACKAGE_FAILED: {stop}", flush=True)
    os._exit(1)
except Exception:
    traceback.print_exc()
    print("SMOKE_PACKAGE_FAILED", flush=True)
    os._exit(1)
sys.stdout.flush()
os._exit(0)
