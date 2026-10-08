"""Headless Blender smoke for New Ship, Rasterise, Flip Nose, the Grid object and the export exclusions.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_frame.py

Environment as smoke_package.py's header says: CULTLIB_PACKAGES, plus CULTLIB_ROOT and CULTMATH_ROOT at the pinned
revisions for the 'dotnet run --project tools/AetherDb' calls, and msgpack importable. New Ship writes the ship under
<repo>/GameData/Mods/smoke.frame, which this smoke removes when it ends.

Pass 1 sends a UV sphere (1 x 0.6 x 0.4, long axis X, off the origin) through New Ship, length 10, like Djinni.
Pass 2 adds a render mesh (a copy of the sphere) and a nub at the +Y end, rasterises, flips the nose, and requires the
nub's cells to move from the aft rows to the forward rows. Pass 3 tags anchors, packages, and requires the GLB to hold
neither the Source nor the Grid and the validator to accept it.
"""

import json
import os
import shutil
import struct
import sys
import traceback
from pathlib import Path

import addon_utils
import bmesh
import bpy

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
SHIP_ID = "smoke.frame"
MODS = REPO / "GameData" / "Mods"


def new_object(collection, name, mesh, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.parent = parent
    collection.objects.link(obj)
    return obj


def box(name, half_x, half_y, half_z):
    mesh = bpy.data.meshes.new(name)
    corners = [(sx * half_x, sy * half_y, sz * half_z) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    mesh.from_pydata(corners, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
    return mesh


def tetrahedron(name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)], [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
    return mesh


def tag(obj, role, anchor_id):
    obj["aetheria.role"] = role
    obj["aetheria.id"] = anchor_id
    return obj


def run(operator):
    try:
        result = operator()
    except RuntimeError as exc:
        result = {"CANCELLED"}
        print(f"OPERATOR error: {exc}")
    return result


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def layout(path, aetheria_ships):
    shape, hardpoints, _ = aetheria_ships.ship_cc.read_layout(path, PACKAGES)
    width, height, cells = shape
    return width, height, {(i // height, i % height) for i, cell in enumerate(cells) if cell}


def row_count(cells, y):
    return sum(1 for _, cy in cells if cy == y)


def glb_node_names(path):
    data = Path(path).read_bytes()
    length, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A, "The GLB's first chunk is not JSON")
    return {node.get("name") for node in json.loads(data[20:20 + length])["nodes"]}


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    bpy.context.preferences.addons["aetheria_ships"].preferences.aetheria_repo = str(REPO)
    require(not (MODS / SHIP_ID).exists(), f"{MODS / SHIP_ID} exists; remove it first")

    scene = bpy.context.scene
    collection = bpy.data.collections.new("Smoke Frame")
    scene.collection.children.link(collection)
    sphere_mesh = bpy.data.meshes.new("Sphere")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.5)
    bm.to_mesh(sphere_mesh)
    bm.free()
    sphere = new_object(collection, "Source Sphere", sphere_mesh, location=(3, 0, 0))
    sphere.scale = (1, 0.6, 0.4)
    bpy.context.view_layer.objects.active = sphere

    # Pass 1: New Ship.
    scene.aetheria_new_ship_id = SHIP_ID
    scene.aetheria_new_ship_name = "Smoke Frame"
    scene.aetheria_new_ship_like = "Djinni"
    scene.aetheria_new_ship_length = 10
    result = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, "New Ship was refused")
    path = str(MODS / SHIP_ID / "ship.cc")
    root = next((obj for obj in collection.objects if obj.get("aetheria.ship_root")), None)
    require(root is not None and root.name == "Ship Root", "New Ship made no Ship Root")
    require(tuple(root.scale) == (1, 1, 1), "Ship Root's scale is not 1")
    source = next((child for child in collection.children if child.get("aetheria.frame") == "Source"), None)
    require(source is not None and sphere.users_collection == (source,), "The sphere is not in Source alone")
    require(sphere.parent == root, "The sphere is not parented to Ship Root")
    require(tuple(sphere.scale) == (1, 1, 1) and all(abs(a) < 1e-9 for a in sphere.rotation_euler),
            "Rotation and scale were not applied to the sphere")
    dx, dy, _ = sphere.dimensions
    print(f"DIMENSIONS {dx:.3f} x {dy:.3f}")
    require(abs(dy - 20.0) < 0.4 and dx < dy, f"The sphere's long extent is not 20 m on Y: {dx} x {dy}")
    width, height, cells = layout(path, aetheria_ships)
    require((width, height) == (6, 10), f"The layout is {width}x{height}, not 6x10")
    require((0, 0) not in cells and (width // 2, height // 2) in cells, "The ellipse's cells are wrong")
    origin = collection.get("aetheria.grid_origin")
    require(origin is not None, "aetheria.grid_origin is not set")
    centre = sphere.matrix_world.translation
    grid_centre = (origin[0] - (width - 1), origin[1] - (height - 1))
    require(abs(grid_centre[0] - centre.x) < 1e-3 and abs(grid_centre[1] - centre.y) < 1e-3,
            f"The grid is not centred on the hull: {grid_centre} against {tuple(centre)[:2]}")
    generated = next((child for child in collection.children if child.get("aetheria.frame") == "Generated"), None)
    grid = bpy.data.objects.get("Grid")
    require(generated is not None and grid is not None and grid.users_collection == (generated,),
            "The Grid object is not in Generated")
    require(grid.parent == root and grid.display_type == "WIRE", "The Grid is not a wire child of Ship Root")
    require(len(grid.data.polygons) == len(cells) and len(grid.data.edges) == 4 * width * height,
            "The Grid does not draw occupied cells as faces and every cell's edges")
    print("PASS 1 new ship ok")

    # Pass 2: Rasterise and Flip Nose with a render mesh and a nub at the +Y end.
    render = new_object(collection, "Hull", sphere_mesh.copy(), location=tuple(sphere.location), parent=root)
    new_object(collection, "Nub", box("Nub", 4.0, 3.0, 0.5), location=(centre.x, centre.y + 12.0, 0), parent=root)
    bpy.context.view_layer.objects.active = render
    require(run(bpy.ops.aetheria.rasterise_hull) == {"FINISHED"}, "Rasterise Hull was refused")
    width, height, before = layout(path, aetheria_ships)
    aft, forward = row_count(before, 0), row_count(before, height - 1)
    print(f"RASTERISE {width}x{height} aft={aft} forward={forward}")
    require(aft > forward, "The nub's cells are not on the aft rows before the flip")
    require(run(bpy.ops.aetheria.flip_nose) == {"FINISHED"}, "Flip Nose was refused")
    _, _, after = layout(path, aetheria_ships)
    require(row_count(after, height - 1) == aft and row_count(after, 0) == forward,
            "Flip Nose did not move the nub's cells from the aft rows to the forward rows")
    require({(width - 1 - x, height - 1 - y) for x, y in before} == after, "The flipped grid is not the turned grid")
    print("PASS 2 rasterise and flip nose ok")

    # Pass 3: Package leaves Source and Grid out of the GLB.
    tag(new_object(collection, "Map Icon", box("Map Icon", 0.5, 0.5, 0.01), (0, 0, -1), root), "map-icon", "map")
    tag(new_object(collection, "Hull Collider", tetrahedron("Hull Collider"), parent=root), "hull-collider", "collider")
    tag(new_object(collection, "Shield", None, parent=root), "shield", "shield")
    tag(new_object(collection, "Tractor", None, (0, 2, 0), root), "tractor", "tractor")
    bpy.context.view_layer.objects.active = render
    result = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    print(f"PACKAGE {sorted(result)}: {report}")
    require(result == {"FINISHED"}, "Package was refused")
    names = glb_node_names(Path(path).with_name("ship.glb"))
    print(f"GLB nodes: {sorted(n for n in names if n)}")
    require("Hull" in names and "Nub" in names, "The GLB lacks the render meshes")
    require("Source Sphere" not in names and "Grid" not in names, "The GLB holds the Source or the Grid")
    print("PASS 3 export ok")
    print("SMOKE_FRAME_OK")


created_mods = not MODS.exists()
existed = (MODS / SHIP_ID).exists()
try:
    main()
except SystemExit as stop:
    print(f"SMOKE_FRAME_FAILED: {stop}", flush=True)
    code = 1
except Exception:
    traceback.print_exc()
    print("SMOKE_FRAME_FAILED", flush=True)
    code = 1
else:
    code = 0
finally:
    if not existed:
        shutil.rmtree(MODS / SHIP_ID, ignore_errors=True)
    if created_mods and not existed:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
