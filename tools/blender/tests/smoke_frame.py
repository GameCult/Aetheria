"""Headless Blender smoke for the add-on frame: New Ship, Rasterise, Flip Nose, Save, the Grid object, undo and Package.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_frame.py

Environment as smoke_package.py's header says: CULTLIB_PACKAGES and SMOKE_GAME_FOLDER (the published AetherDb under its
ModTools, no dotnet), and msgpack importable. The ship is created under <game folder>/GameData/Mods/smoke.frame, which
this smoke removes when it ends.

Pass 1 sends a UV sphere (1 x 0.6 x 0.4, long axis X, off the origin) through New Ship, length 10, like Djinni: nothing
is written, ed.undo takes the ship back, and the first Save creates the file. Pass 2 makes a Save fail after create and
requires the folder gone and the retry to succeed. Pass 3 rasterises after a scale change: the .cc is unchanged until Save
and ed.undo returns the cells and the grid placement together. Pass 4 adds hardpoints and saves. Pass 5 puts meshes
outside Ship Root: Flip Nose and Rasterise refuse and change nothing; parented, Flip Nose moves the nub's cells from the
aft rows to the forward rows. Pass 6 packages: an unsaved buffer is refused, the saved one validates, and the GLB holds
neither the Source nor the Grid. Every refusal report names neither the ship ID, the collection, an object nor a path.
"""

import hashlib
import json
import os
import re
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
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
MODS = GAME / "GameData" / "Mods"
COLLECTION = "Smoke Frame"
NAMES_NOT_ECHOED = (SHIP_ID, COLLECTION, "Second Frame", "Loose Nub", "Hull", "NoSuchHullXYZ", str(GAME), GAME.name)


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
    """The operator's result and, for an operator that reported an error, the report (bpy.ops raises it)."""
    try:
        return operator(), ""
    except RuntimeError as exc:
        print(f"OPERATOR error: {exc}")
        return {"CANCELLED"}, str(exc)


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def refused(operator, expected, what):
    """Requires a CANCELLED operator whose report holds expected and echoes none of NAMES_NOT_ECHOED."""
    result, message = run(operator)
    require(result == {"CANCELLED"}, f"{what} was not refused")
    require(expected in message, f"{what}: the report is not the fixed text ({message!r})")
    for name in NAMES_NOT_ECHOED:
        require(name not in message, f"{what}: the report echoes an input ({name})")
    return message


def saved_layout(path, aetheria_ships):
    shape, hardpoints, revision = aetheria_ships.ship_cc.read_layout(path, PACKAGES)
    width, height, cells = shape
    return width, height, {(i // height, i % height) for i, cell in enumerate(cells) if cell}, hardpoints, revision


def buffer(scene):
    """The layout buffer as (width, height, occupied cells, hardpoint count)."""
    state = scene.aetheria_layout
    return (state.width, state.height,
            {(i // state.height, i % state.height) for i, cell in enumerate(state.cells) if cell.occupied},
            len(state.hardpoints))


def row_count(cells, y):
    return sum(1 for _, cy in cells if cy == y)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def glb_node_names(path):
    data = Path(path).read_bytes()
    length, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A, "The GLB's first chunk is not JSON")
    return {node.get("name") for node in json.loads(data[20:20 + length])["nodes"]}


def child(collection, kind):
    return next((c for c in collection.children if c.get("aetheria.frame") == kind), None)


def grid_faces():
    grid = bpy.data.objects.get("Grid")
    return len(grid.data.polygons) if grid else None


def select(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def undo_to_before(step):
    """Undoes the last operator and returns the fresh scene and ship collection: memfile undo replaces the data-blocks,
    so every Python reference from before is stale."""
    result = bpy.ops.ed.undo()
    require(result == {"FINISHED"}, f"ed.undo after {step} did not run")
    return bpy.context.scene, bpy.data.collections[COLLECTION]


def new_ship(scene, ship_id=SHIP_ID, name="Smoke Frame", reference="Djinni", length=10):
    scene.aetheria_new_ship_id = ship_id
    scene.aetheria_new_ship_name = name
    scene.aetheria_new_ship_like = reference
    scene.aetheria_new_ship_length = length
    return run(bpy.ops.aetheria.new_ship)


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(GAME)
    require(not (MODS / SHIP_ID).exists(), f"{MODS / SHIP_ID} exists; remove it first")
    path = str(MODS / SHIP_ID / "ship.cc")

    scene = bpy.context.scene
    collection = bpy.data.collections.new(COLLECTION)
    scene.collection.children.link(collection)
    sphere_mesh = bpy.data.meshes.new("Sphere")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.5)
    bm.to_mesh(sphere_mesh)
    bm.free()
    sphere = new_object(collection, "Source Sphere", sphere_mesh, location=(3, 0, 0))
    sphere.scale = (1, 0.6, 0.4)
    select(sphere)

    # Refusals come first and change nothing: the report is fixed text.
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.game_folder = ""
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name, scene.aetheria_new_ship_like = SHIP_ID, "Smoke Frame", "Djinni"
    refused(bpy.ops.aetheria.new_ship, "Game folder preference", "New Ship with no game folder and an unsaved .blend")
    preferences.game_folder = str(GAME)
    scene.aetheria_new_ship_id = "con"
    scene.aetheria_new_ship_name = "Smoke Frame"
    scene.aetheria_new_ship_like = "Djinni"
    message = refused(bpy.ops.aetheria.new_ship, "Windows device name", "New Ship with the ID con")
    require(not re.search(r"con", message), "The bad-ID report echoes the ID")
    scene.aetheria_new_ship_id = SHIP_ID
    scene.aetheria_new_ship_like = ""
    refused(bpy.ops.aetheria.new_ship, "reference hull", "New Ship without a reference")
    require("aetheria.id" not in collection and _root_count() == 0 and sphere.parent is None,
            "A refused New Ship changed the collection")

    # Pass 1: New Ship writes nothing; undo takes it back; the first Save creates the file.
    bpy.ops.ed.undo_push(message="before New Ship")
    result, message = new_ship(scene)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    bpy.ops.ed.undo_push(message="New Ship")  # operators called from a script push no undo step, as the UI would
    require(not (MODS / SHIP_ID).exists(), "New Ship wrote to the game folder")
    require(list(collection.get("aetheria.pending")) == ["Smoke Frame", "Djinni"], "The collection is not pending")
    root = next((obj for obj in collection.objects if obj.get("aetheria.ship_root")), None)
    require(root is not None and root.name == "Ship Root", "New Ship made no Ship Root")
    require(tuple(root.scale) == (1, 1, 1), "Ship Root's scale is not 1")
    source = child(collection, "Source")
    require(source is not None and sphere.users_collection == (source,), "The sphere is not in Source alone")
    require(sphere.parent == root, "The sphere is not parented to Ship Root")
    require(tuple(sphere.scale) == (1, 1, 1) and all(abs(a) < 1e-9 for a in sphere.rotation_euler),
            "Rotation and scale were not applied to the sphere")
    dx, dy, _ = sphere.dimensions
    print(f"DIMENSIONS {dx:.3f} x {dy:.3f}")
    require(abs(dy - 20.0) < 0.4 and dx < dy, f"The sphere's long extent is not 20 m on Y: {dx} x {dy}")
    width, height, cells, hardpoints = buffer(scene)
    require((width, height) == (6, 10) and hardpoints == 0, f"The buffer is {width}x{height} with {hardpoints} hardpoints")
    require((0, 0) not in cells and (width // 2, height // 2) in cells, "The ellipse's cells are wrong")
    require(not scene.aetheria_layout.revision, "A pending ship's buffer has a revision")
    origin = collection.get("aetheria.grid_origin")
    require(origin is not None and len(origin) == 4 and (origin[2], origin[3]) == (width, height),
            "aetheria.grid_origin does not carry the grid's size")
    centre = sphere.matrix_world.translation
    grid_centre = (origin[0] - (width - 1), origin[1] - (height - 1))
    require(abs(grid_centre[0] - centre.x) < 1e-3 and abs(grid_centre[1] - centre.y) < 1e-3,
            f"The grid is not centred on the hull: {grid_centre} against {tuple(centre)[:2]}")
    generated = child(collection, "Generated")
    grid = bpy.data.objects.get("Grid")
    require(generated is not None and grid is not None and grid.users_collection == (generated,),
            "The Grid object is not in Generated")
    require(grid.parent == root and grid.display_type == "WIRE", "The Grid is not a wire child of Ship Root")
    require(len(grid.data.polygons) == len(cells) and len(grid.data.edges) == 4 * width * height,
            "The Grid does not draw occupied cells as faces and every cell's edges")
    refused(bpy.ops.aetheria.load_ship_layout, "Not saved yet", "Load on a pending ship")

    scene, collection = undo_to_before("New Ship")
    require("aetheria.id" not in collection and "aetheria.pending" not in collection, "Undo left the collection bound")
    require(bpy.data.objects.get("Ship Root") is None and bpy.data.objects.get("Grid") is None, "Undo left Ship Root or the Grid")
    sphere = bpy.data.objects["Source Sphere"]
    require(sphere.users_collection == (collection,) and sphere.parent is None, "Undo did not return the sphere")
    select(sphere)
    result, message = new_ship(scene)
    require(result == {"FINISHED"}, f"New Ship after undo was refused: {message}")
    root = bpy.data.objects["Ship Root"]
    print("PASS 1 new ship ok")

    # Pass 2: a Save that fails in create or after it leaves nothing behind and tells the author nothing of the input;
    # the retry succeeds.
    collection["aetheria.pending"] = ["Smoke Frame", "NoSuchHullXYZ"]
    refused(bpy.ops.aetheria.save_ship_layout, "AetherDb refused to create the ship", "Save with an unknown reference hull")
    require(not (MODS / SHIP_ID).exists(), "A refused create left the ship folder behind")
    collection["aetheria.pending"] = ["Smoke Frame", "Djinni"]
    real = aetheria_ships.replace_layout

    def failing(*args, **kwargs):
        raise ValueError("probe: replace_layout failed")

    aetheria_ships.replace_layout = failing
    try:
        message = refused(bpy.ops.aetheria.save_ship_layout, "probe: replace_layout failed", "Save with a failing write")
    finally:
        aetheria_ships.replace_layout = real
    require(not (MODS / SHIP_ID).exists(), "A failed first Save left the ship folder behind")
    require(collection.get("aetheria.pending") is not None, "A failed first Save cleared aetheria.pending")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"The retried Save was refused: {message}")
    require(collection.get("aetheria.pending") is None, "Save left aetheria.pending set")
    width, height, cells, hardpoints, revision = saved_layout(path, aetheria_ships)
    require((width, height, cells) == buffer(scene)[:3], "The saved .cc is not the buffer")
    require(scene.aetheria_layout.revision == revision, "The buffer did not record the saved revision")
    require(aetheria_ships.ship_cc.read(path, PACKAGES).ship.body[0] == SHIP_ID, "The saved file is not the ship")
    require(grid_faces() == len(cells), "Save did not redraw the Grid")
    refused_existing = _second_collection_refused()
    print("PASS 2 save creates and cleans up ok", refused_existing)

    # Pass 3: Rasterise edits the buffer and the placement, never the file; undo returns both.
    saved_digest = digest(path)
    before = buffer(scene)
    before_origin = list(collection["aetheria.grid_origin"])
    select(bpy.data.objects["Source Sphere"])
    bpy.ops.ed.undo_push(message="before scale")
    bpy.data.objects["Source Sphere"].scale = (0.5, 1, 1)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise was refused: {message}")
    after = buffer(scene)
    after_origin = list(collection["aetheria.grid_origin"])
    bpy.ops.ed.undo_push(message="Rasterise")
    require(after[:2] != before[:2] and after_origin[2:] == list(after[:2]), f"Rasterise did not resize: {after[:2]}")
    require(digest(path) == saved_digest, "Rasterise wrote the .cc")
    scene, collection = undo_to_before("Rasterise")
    require(buffer(scene) == before and list(collection["aetheria.grid_origin"]) == before_origin,
            "Undo did not return the cells and the grid placement together")
    require(grid_faces() == len(before[2]), "Undo left a Grid that does not draw the restored cells")
    print("PASS 3 rasterise and undo ok")

    # Pass 4: hardpoints in the buffer survive Rasterise and a second Save; a toggled cell changes the Grid.
    select(bpy.data.objects["Source Sphere"])
    for index, (mount, x, y) in enumerate((("mount.a", 1, 2), ("mount.b", 3, 4))):
        bpy.ops.aetheria.add_ship_hardpoint()
        hp = bpy.context.scene.aetheria_layout.hardpoints[index]
        hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint = mount, "5", x, y, "#"  # Reactor: internal, no anchor
        hp.armor, hp.firing_arc = 5.0, 90.0
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save with hardpoints was refused: {message}")
    rows = saved_layout(path, aetheria_ships)[3]
    require(len(rows) == 2, "The saved .cc does not hold two hardpoints")
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"} and buffer(bpy.context.scene)[3] == 2, "Rasterise dropped the buffer's hardpoints")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"The second Save was refused: {message}")
    require(saved_layout(path, aetheria_ships)[3] == rows, "The second Save changed the hardpoint rows")
    scene = bpy.context.scene
    faces = grid_faces()
    state = scene.aetheria_layout
    cell = next(c for c in state.cells if c.occupied)
    cell.occupied = False
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"} and grid_faces() == faces - 1, "Toggling a cell and Save did not redraw the Grid")
    state.new_width = state.width + 1  # a layout resized after the grid was placed: the placement no longer fits it
    bpy.ops.aetheria.resize_ship_layout()
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"} and bpy.data.objects.get("Grid") is None,
            "A Grid placed for another size is still drawn after the layout was resized")
    print("PASS 4 hardpoints and save ok")

    # Pass 5: meshes outside Ship Root stop Flip Nose and Rasterise; parented, the flip moves the nub's cells.
    collection = bpy.data.collections[COLLECTION]
    root = bpy.data.objects["Ship Root"]
    sphere = bpy.data.objects["Source Sphere"]
    sphere.scale = (1, 1, 1)
    centre = sphere.matrix_world.translation
    render = new_object(collection, "Hull", bpy.data.meshes["Sphere"].copy(), location=tuple(sphere.location))
    nub = new_object(collection, "Loose Nub", box("Nub", 4.0, 3.0, 0.5), location=(centre.x, centre.y + 12.0, 0))
    select(render)
    snapshot = (buffer(bpy.context.scene), list(collection["aetheria.grid_origin"]),
                [tuple(map(tuple, child_obj.matrix_world)) for child_obj in root.children])
    for operator, what in ((bpy.ops.aetheria.flip_nose, "Flip Nose"), (bpy.ops.aetheria.rasterise_hull, "Rasterise")):
        refused(operator, "2 meshes in the ship collection are not under Ship Root", f"{what} with loose meshes")
        require(nub.select_get() and render.select_get(), f"{what} did not select the loose meshes")
        require(snapshot == (buffer(bpy.context.scene), list(collection["aetheria.grid_origin"]),
                             [tuple(map(tuple, child_obj.matrix_world)) for child_obj in root.children]),
                f"{what} changed the scene while refusing")
    render.parent = root  # Ship Root's descendants count, however deep: the nub hangs from the hull
    render.matrix_parent_inverse = root.matrix_world.inverted()
    nub.parent = render
    nub.matrix_parent_inverse = render.matrix_world.inverted()
    select(render)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise Hull was refused: {message}")
    width, height, before, _ = buffer(bpy.context.scene)
    aft, forward = row_count(before, 0), row_count(before, height - 1)
    print(f"RASTERISE {width}x{height} aft={aft} forward={forward}")
    require(aft > forward, "The nub's cells are not on the aft rows before the flip")
    result, message = run(bpy.ops.aetheria.flip_nose)
    require(result == {"FINISHED"}, f"Flip Nose was refused: {message}")
    _, _, after, _ = buffer(bpy.context.scene)
    require(row_count(after, height - 1) == aft and row_count(after, 0) == forward,
            "Flip Nose did not move the nub's cells from the aft rows to the forward rows")
    require({(width - 1 - x, height - 1 - y) for x, y in before} == after, "The flipped grid is not the turned grid")
    print("PASS 5 loose meshes, rasterise and flip nose ok")

    # Pass 6: Package refuses an unsaved buffer, then the saved one validates; the GLB leaves Source and Grid out.
    scene = bpy.context.scene
    collection = bpy.data.collections[COLLECTION]
    root = bpy.data.objects["Ship Root"]
    tag(new_object(collection, "Map Icon", box("Map Icon", 0.5, 0.5, 0.01), (0, 0, -1), root), "map-icon", "map")
    tag(new_object(collection, "Hull Collider", tetrahedron("Hull Collider"), parent=root), "hull-collider", "collider")
    tag(new_object(collection, "Shield", None, parent=root), "shield", "shield")
    tag(new_object(collection, "Tractor", None, (0, 2, 0), root), "tractor", "tractor")
    select(bpy.data.objects["Hull"])
    saved_digest = digest(path)
    result, message = run(bpy.ops.aetheria.package_ship)
    require(result == {"CANCELLED"} and "Save Layout first" in scene.aetheria_package_report,
            f"Package took an unsaved buffer: {scene.aetheria_package_report}")
    require(digest(path) == saved_digest and not Path(path).with_name("ship.glb").exists(),
            "A refused Package wrote the .cc or the GLB")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save before Package was refused: {message}")
    preferences.game_folder = ""  # the game folder is then found from the bound .cc
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    preferences.game_folder = str(GAME)
    print(f"PACKAGE {sorted(result)}: {report}")
    require(result == {"FINISHED"}, f"Package with an empty Game folder preference was refused: {report}")
    names = glb_node_names(Path(path).with_name("ship.glb"))
    print(f"GLB nodes: {sorted(n for n in names if n)}")
    require("Hull" in names and "Loose Nub" in names, "The GLB lacks the render meshes")
    require("Source Sphere" not in names and "Grid" not in names, "The GLB holds the Source or the Grid")
    print("PASS 6 package ok")
    print("SMOKE_FRAME_OK")


def _root_count():
    return sum(1 for obj in bpy.data.objects if obj.get("aetheria.ship_root"))


def _second_collection_refused():
    """New Ship on a second collection with the saved ship's ID is refused with fixed text."""
    second = bpy.data.collections.new("Second Frame")
    bpy.context.scene.collection.children.link(second)
    mesh = new_object(second, "Other", box("Other", 1.0, 2.0, 0.5))
    select(mesh)
    bpy.context.scene.aetheria_new_ship_id = SHIP_ID
    refused(bpy.ops.aetheria.new_ship, "already exists", "New Ship over an existing ship")
    require("aetheria.id" not in second, "A refused New Ship bound the second collection")
    bpy.data.objects.remove(mesh)
    bpy.data.collections.remove(second)
    return "existing target refused"


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
