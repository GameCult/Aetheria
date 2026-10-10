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

Pass 7 pins the guards that protect an author's files: a Save over an existing ship.cc is refused and leaves it, a failed
Save leaves the files that were in the folder before, New Ship takes none of the previous ship's hardpoints, and a buffer
that belongs to another ship is neither saved nor rasterised. Pass 8 calls the operators with undo=True, which pushes the
native undo step in background Blender only for an operator that carries UNDO: Rasterise, Flip Nose and New Ship are
undoable, Save is not.

Pass 7b pins what Package does to a ship with a Ship Root: it puts the saved Shape's centre of mass at the world origin
(through AetherDb 'centre'), makes the map icon, shield and tractor nobody owns from the render meshes, and never makes a
hull collider. A stale grid is refused; a kept anchor survives a second Package and an unkept one is rebuilt; an anchor
the author made takes the place of its generated twin.
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
GUARD_A, GUARD_B, UNDO_SHIP = "smoke.guard.a", "smoke.guard.b", "smoke.undo"
PACKAGE_FRAME_SHIP = "smoke.pframe"
PACKAGE_L_SHIP = "smoke.pellhull"
PACKAGE_SOURCE_SHIP = "smoke.psource"
EXTRA_IDS = (GUARD_A, GUARD_B, UNDO_SHIP, PACKAGE_FRAME_SHIP, PACKAGE_L_SHIP, PACKAGE_SOURCE_SHIP)
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


def rounded(matrix):
    return tuple(round(value, 4) for row in matrix for value in row)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def glb_nodes(path):
    data = Path(path).read_bytes()
    length, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A, "The GLB's first chunk is not JSON")
    return json.loads(data[20:20 + length])["nodes"]


def glb_node_names(path):
    return {node.get("name") for node in glb_nodes(path)}


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
    require(max(vertex.co.length for vertex in sphere.data.vertices) < 0.5 + 1e-6 and sphere.scale.x > 1,
            "New Ship baked the placement into the mesh instead of the object's own matrix")
    world = [sphere.matrix_world @ vertex.co for vertex in sphere.data.vertices]
    dx, dy = (max(v[i] for v in world) - min(v[i] for v in world) for i in (0, 1))
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
    placed_scale, placed_rotation = tuple(sphere.scale), tuple(sphere.rotation_euler)
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
    bpy.data.objects["Source Sphere"].scale = (placed_scale[0] / 2, placed_scale[1], placed_scale[2])
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
    sphere.scale = placed_scale
    centre = sphere.matrix_world.translation
    render = new_object(collection, "Hull", bpy.data.meshes["Sphere"].copy(), location=tuple(sphere.location))
    render.scale, render.rotation_euler = placed_scale, placed_rotation
    nub = new_object(collection, "Loose Nub", box("Nub", 4.0, 3.0, 0.5), location=(centre.x, centre.y + 12.0, 0))
    select(render)
    snapshot = (buffer(bpy.context.scene), list(collection["aetheria.grid_origin"]),
                [tuple(map(tuple, child_obj.matrix_world)) for child_obj in root.children])
    for operator, what in ((bpy.ops.aetheria.flip_nose, "Flip Nose"), (bpy.ops.aetheria.rasterise_hull, "Rasterise"),
                           (bpy.ops.aetheria.package_ship, "Package")):
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
    real_rasterise = aetheria_ships._rasterise
    turned = [rounded(child_obj.matrix_world) for child_obj in root.children]

    def failing_rasterise(*args, **kwargs):
        raise ValueError("probe: rasterise failed")

    aetheria_ships._rasterise = failing_rasterise
    try:
        refused(bpy.ops.aetheria.flip_nose, "probe: rasterise failed", "Flip Nose whose Rasterise fails")
    finally:
        aetheria_ships._rasterise = real_rasterise
    require(turned == [rounded(child_obj.matrix_world) for child_obj in root.children],
            "A Flip Nose whose Rasterise failed left the hull turned")
    result, message = run(bpy.ops.aetheria.flip_nose)
    require(result == {"FINISHED"}, f"Flip Nose was refused: {message}")
    _, _, after, _ = buffer(bpy.context.scene)
    require(row_count(after, height - 1) == aft and row_count(after, 0) == forward,
            "Flip Nose did not move the nub's cells from the aft rows to the forward rows")
    require({(width - 1 - x, height - 1 - y) for x, y in before} == after, "The flipped grid is not the turned grid")
    # A Source mesh that is not parented to Ship Root is loose too: Rasterise would read it and Flip Nose would not turn it.
    sphere = bpy.data.objects["Source Sphere"]
    sphere.parent = None
    for operator, what in ((bpy.ops.aetheria.flip_nose, "Flip Nose"), (bpy.ops.aetheria.rasterise_hull, "Rasterise")):
        select(render)
        refused(operator, "1 meshes in the ship collection are not under Ship Root", f"{what} with an unparented Source mesh")
        require(sphere.select_get(), f"{what} did not select the unparented Source mesh")
    sphere.parent = root
    select(render)
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
    require("the validator accepted the ship" in report and not any(name in report for name in NAMES_NOT_ECHOED),
            f"Package's report is not the fixed text: {report}")
    # Members a later schema adds to a hardpoint row are not unsaved edits.
    real_read_layout = aetheria_ships.read_layout

    def later_schema(*args, **kwargs):
        shape, rows, revision = real_read_layout(*args, **kwargs)
        return shape, [list(row) + [0] for row in rows], revision

    aetheria_ships.read_layout = later_schema
    try:
        result, message = run(bpy.ops.aetheria.package_ship)
    finally:
        aetheria_ships.read_layout = real_read_layout
    require(result == {"FINISHED"}, f"A .cc whose rows carry a later schema's members read as unsaved edits: {message}")
    # A library error is the console's, not the panel's: a directory where ship.glb goes fails the exporter.
    glb = Path(path).with_name("ship.glb")
    glb.unlink()
    glb.mkdir()
    try:
        result, message = run(bpy.ops.aetheria.package_ship)
        report = scene.aetheria_package_report
    finally:
        glb.rmdir()
    require(result == {"CANCELLED"} and "The GLB export failed" in report, f"A failed GLB export was not refused: {report}")
    require("Traceback" not in report and not any(name in report for name in NAMES_NOT_ECHOED),
            f"A library error reached the report: {report}")
    real_visual = aetheria_ships.replace_visual

    def unwritable(*args, **kwargs):
        raise OSError(f"probe: cannot write {path}")

    aetheria_ships.replace_visual = unwritable
    try:
        result, message = run(bpy.ops.aetheria.package_ship)
        report = scene.aetheria_package_report
    finally:
        aetheria_ships.replace_visual = real_visual
    require(result == {"CANCELLED"} and report == "Package failed: Stopped by OSError; see the system console",
            f"A library error of another type is not reported by its type alone: {report}")
    result, message = run(bpy.ops.aetheria.package_ship)
    require(result == {"FINISHED"}, f"Package after the failed export was refused: {scene.aetheria_package_report}")
    print("PASS 6 package ok")
    mirror_pass()
    print("PASS 6b mirror ok")
    shear_pass()
    print("PASS 6c shear ok")
    guards_pass(aetheria_ships)
    print("PASS 7 guards ok")
    package_frame_pass(aetheria_ships)
    package_asymmetric_pass(aetheria_ships)
    print("PASS 7b package frame ok")
    undo_pass()
    print("PASS 8 native undo ok")
    print("SMOKE_FRAME_OK")


def package_frame_pass(aetheria_ships):
    """Package on a ship with a Ship Root: recentre on the saved Shape's centre of mass, the three generated anchors,
    Keep, the author's own anchor, and no collider ever."""
    from mathutils import Vector
    scene = bpy.context.scene
    path = str(MODS / PACKAGE_FRAME_SHIP / "ship.cc")
    glb = Path(path).with_name("ship.glb")
    require(not (MODS / PACKAGE_FRAME_SHIP).exists(), f"{MODS / PACKAGE_FRAME_SHIP} exists; remove it first")
    collection, _ = ship_from_box(scene, "Frame Pack", PACKAGE_FRAME_SHIP, (0, 0, 0))
    root = next(obj for obj in collection.objects if obj.get("aetheria.ship_root"))
    # The Source box, after New Ship sized it to 10 cells, is 20 m long; the render box is 1/1.2 of it on each axis, so
    # anything derived from Source instead of the render mesh lands elsewhere.
    half = tuple(h / 1.2 for h in (10 / 3, 10.0, 5 / 3))
    centre = (0.5, 1.0, 0.3)
    render = new_object(collection, "Frame Render", box("Frame Render", *half), centre, root)
    select(render)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise of the render box was refused: {message}")
    state = scene.aetheria_layout

    def save_l():
        for index, cell in enumerate(state.cells):  # column 0 and row 0 of the grid: an L, not its bounds' middle
            cell.occupied = index // state.height == 0 or index % state.height == 0
        result, message = run(bpy.ops.aetheria.save_ship_layout)
        require(result == {"FINISHED"}, f"Save of the L was refused: {message}")

    def world_state():
        return (tuple(map(tuple, root.matrix_world)), sorted(obj.name for obj in bpy.data.objects))

    def centre_of_mass_world():
        occupied = saved_layout(path, aetheria_ships)[2]
        mean = (sum(x for x, _ in occupied) / len(occupied), sum(y for _, y in occupied) / len(occupied))
        origin = collection["aetheria.grid_origin"]
        local = aetheria_ships.hull_grid.cell_centre((origin[0], origin[1]), *mean)
        return root.matrix_world @ Vector((local[0], local[1], 0.0))

    # A layout resized after Rasterise and saved no longer matches the grid: refused, and nothing moved or made.
    state.new_width = state.width + 1
    bpy.ops.aetheria.resize_ship_layout()
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save of the resized layout was refused: {message}")
    unchanged = world_state()
    select(render)
    refused(bpy.ops.aetheria.package_ship, "Rasterise again: the grid no longer matches the saved layout",
            "Package with a stale grid")
    require(world_state() == unchanged, "A refused Package moved Ship Root or made objects")
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise after the resize was refused: {message}")
    save_l()

    # No collider in the collection: Package is refused by the validator, it has still recentred, and it made no collider.
    root.rotation_euler.z = 0.3  # the point is taken through the root's matrix, not its location
    bpy.context.view_layer.update()
    select(render)
    before = centre_of_mass_world()
    require(abs(before.x) > 0.5 or abs(before.y) > 0.5, "The fixture's centre of mass starts at the origin")
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(not any(obj.get("aetheria.role") == "hull-collider" for obj in collection.all_objects),
            "Package never generates a collider: the collection holds one")
    require(result == {"CANCELLED"} and "the validator refused the ship" in report, f"Package without a collider: {report}")
    after = centre_of_mass_world()
    require(abs(after.x) < 1e-3 and abs(after.y) < 1e-3, f"The centre of mass is not at the origin: {tuple(after)}")
    code, output = aetheria_ships._aetherdb(bpy.context, "validate", path, ship_cc=path)
    require(code != 0 and "hull-collider" in output, "The validator did not refuse a package without a hull-collider")
    nodes = glb_nodes(glb)
    require(not any((node.get("extras") or {}).get("aetheria.role") == "hull-collider" or "collider" in (node.get("name") or "").lower()
                    for node in nodes), "Package never generates a collider: the GLB holds one")
    require("Grid" not in glb_node_names(glb), "The GLB holds the Grid")

    generated = child(collection, "Generated")
    made = {obj["aetheria.role"]: obj for obj in generated.objects if "aetheria.role" in obj}
    require(set(made) == {"map-icon", "shield", "tractor"}, f"Generated holds {sorted(made)} instead of the three anchors")
    require(all(obj.parent == root and obj["aetheria.keep"] is False for obj in made.values()),
            "A generated anchor is not a child of Ship Root with aetheria.keep False (as a Python bool)")
    require({"map", "shield", "tractor"} <= {node.get("extras", {}).get("aetheria.id") for node in nodes},
            "The GLB lacks a generated anchor")
    shield, tractor, icon = made["shield"], made["tractor"], made["map-icon"]
    expected_scale = [max(1.15 * h, 0.1) for h in half]
    require(all(abs(a - b) < 1e-4 for a, b in zip(shield.scale, expected_scale)) and
            all(abs(a - b) < 1e-4 for a, b in zip(shield.location, centre)),
            f"The shield is not 1.15 x the render half extents at its centre: {tuple(shield.scale)} {tuple(shield.location)}")
    require(abs(tractor.location.x - centre[0]) < 0.01 and abs(tractor.location.y - (centre[1] - half[1])) < 1e-3 and
            abs(tractor.location.z - centre[2]) < 1e-3, f"The tractor is not at the render bounds' stern: {tuple(tractor.location)}")
    corners = [vertex.co for vertex in icon.data.vertices]
    require(len(icon.data.polygons) == 1 and all(
            abs(co.x - centre[0]) <= half[0] + 1e-4 and abs(co.y - centre[1]) <= half[1] + 1e-4 and abs(co.z - centre[2]) < 1e-4
            for co in corners), "The map icon is not one face within the render bounds")

    # A second Package changes nothing: it moves Ship Root by nothing and rebuilds the same anchors.
    shape = lambda: sorted((obj["aetheria.role"], tuple(round(v, 6) for v in obj.location), tuple(round(v, 6) for v in obj.scale))
                           for obj in child(collection, "Generated").objects if "aetheria.role" in obj)
    anchors_before, translation = shape(), root.matrix_world.translation.copy()
    select(render)
    run(bpy.ops.aetheria.package_ship)
    require((root.matrix_world.translation - translation).length < 1e-4 and shape() == anchors_before,
            "A second Package moved Ship Root or changed the anchors")

    # Keep: a kept anchor is left where the author put it; an unkept one is rebuilt at its generated place.
    generated = child(collection, "Generated")
    made = {obj["aetheria.role"]: obj for obj in generated.objects if "aetheria.role" in obj}
    made["tractor"]["aetheria.keep"] = True
    made["tractor"].location.x += 5.0
    made["shield"].location.x += 5.0
    kept_at = tuple(made["tractor"].location)
    select(render)
    run(bpy.ops.aetheria.package_ship)
    made = {obj["aetheria.role"]: obj for obj in child(collection, "Generated").objects if "aetheria.role" in obj}
    require(tuple(made["tractor"].location) == kept_at, "Package rebuilt a kept anchor")
    require(all(abs(a - b) < 1e-4 for a, b in zip(made["shield"].location, centre)), "Package did not rebuild an unkept anchor")

    # A flat hull still gets a shield: its scale is floored at 0.1 on the thin axis.
    render.scale.z = 0.01
    select(render)
    run(bpy.ops.aetheria.package_ship)
    shield = next(obj for obj in child(collection, "Generated").objects if obj.get("aetheria.role") == "shield")
    require(all(abs(a - b) < 1e-4 for a, b in zip(shield.scale, (1.15 * half[0], 1.15 * half[1], 0.1))),
            f"A flat hull's shield is not floored at 0.1: {tuple(shield.scale)}")
    render.scale.z = 1.0

    # A render mesh with no outline from above refuses Package before it changes anything: Ship Root, displaced here so
    # that a recentre would move it, stays where it is and the generated anchors stay.
    root.location.x += 3.0
    bpy.context.view_layer.update()
    render.scale.x = 0.0
    select(render)
    unchanged = world_state()
    refused(bpy.ops.aetheria.package_ship, "no outline", "Package of a hull with no outline from above")
    require(world_state() == unchanged, "A refused Package moved Ship Root or changed the objects")
    render.scale.x = 1.0

    # An anchor the author made owns its role: its generated twin goes and is not made again.
    own = new_object(collection, "My Map Icon", box("My Map Icon", 0.5, 0.5, 0.01), (0, 0, 0), root)
    tag(own, "map-icon", "hand.map")
    select(render)
    run(bpy.ops.aetheria.package_ship)
    require(not any(obj.get("aetheria.role") == "map-icon" for obj in child(collection, "Generated").objects),
            "A generated map icon stands beside the author's own")

    # With the collider the author made, the package passes; Generated still holds none.
    tag(new_object(collection, "My Collider", tetrahedron("My Collider"), parent=root), "hull-collider", "collider")
    select(render)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "the validator accepted the ship" in report, f"Package with a collider: {report}")
    require(not any(obj.get("aetheria.role") == "hull-collider" for obj in child(collection, "Generated").objects),
            "Package never generates a collider: Generated holds one")


def package_asymmetric_pass(aetheria_ships):
    """Where generation takes its centre and its meshes from, and what the stale sweep may delete."""
    scene = bpy.context.scene
    for ship_id in (PACKAGE_L_SHIP, PACKAGE_SOURCE_SHIP):
        require(not (MODS / ship_id).exists(), f"{MODS / ship_id} exists; remove it first")

    # An L-shaped render hull: its bounds centre (0, 4, 0.15) differs from the mean of its vertices (1, 2.5, 0.4) and
    # from Ship Root's origin, and Ship Root sits away from the world origin, so a centre taken from any of those lands
    # elsewhere. Generated map-icon, shield and tractor are in Ship Root's frame.
    collection, _ = ship_from_box(scene, "Frame L", PACKAGE_L_SHIP, (0, 0, 0))
    root = next(obj for obj in collection.objects if obj.get("aetheria.ship_root"))
    root.location = (7.0, -3.0, 0.0)
    bpy.context.view_layer.update()
    new_object(collection, "L Long", box("L Long", 3.0, 1.0, 1.0), (0, 0, 0), root)
    arm = new_object(collection, "L Arm", box("L Arm", 0.5, 4.0, 0.5), (2.0, 5.0, 0.8), root)
    select(arm)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise of the L hull was refused: {message}")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save of the L hull was refused: {message}")
    select(arm)
    run(bpy.ops.aetheria.package_ship)
    generated = child(collection, "Generated")
    made = {obj["aetheria.role"]: obj for obj in generated.objects if "aetheria.role" in obj}
    require(set(made) == {"map-icon", "shield", "tractor"}, f"Generated holds {sorted(made)} for the L hull")
    tractor, shield = made["tractor"].location, made["shield"]
    require(all(abs(a - b) < 1e-3 for a, b in zip(tractor, (0.0, -1.0, 0.15))),
            f"The tractor is not at the L hull's bounds centre X and minimum Y: {tuple(tractor)}")
    require(all(abs(a - b) < 1e-3 for a, b in zip(shield.location, (0.0, 4.0, 0.15))) and
            all(abs(a - b) < 1e-3 for a, b in zip(shield.scale, (1.15 * 3.0, 1.15 * 5.0, 1.15 * 1.15))),
            f"The shield is not at the L hull's bounds centre: {tuple(shield.location)} {tuple(shield.scale)}")

    # The author's hull collider is hers wherever she keeps it: Package's stale sweep removes only what Package made.
    own = new_object(collection, "L Collider", tetrahedron("L Collider"), parent=root)
    tag(own, "hull-collider", "collider")
    for holder in own.users_collection:
        holder.objects.unlink(own)
    generated.objects.link(own)
    select(arm)
    run(bpy.ops.aetheria.package_ship)
    require(bpy.data.objects.get("L Collider") is not None and own.name in generated.objects,
            "Package deleted the author's hull collider from Generated")
    require(not any(obj.get("aetheria.generated") for obj in generated.objects if obj.get("aetheria.role") == "hull-collider"),
            "Package marked the author's collider as its own")

    # A ship with Source meshes and no render mesh: Package generates nothing; the Source fallback is Rasterise's.
    collection, source_hull = ship_from_box(scene, "Frame Source", PACKAGE_SOURCE_SHIP, (0, 0, 0))
    select(source_hull)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise of the Source-only ship was refused: {message}")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save of the Source-only ship was refused: {message}")
    select(source_hull)
    run(bpy.ops.aetheria.package_ship)
    require(not any("aetheria.role" in obj for obj in child(collection, "Generated").objects),
            "Package generated anchors from Source meshes")


def ship_from_box(scene, name, ship_id, location):
    """A fresh collection holding one box, made a pending ship by New Ship; returns the collection and the box."""
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    hull = new_object(collection, name + " Hull", box(name, 1.0, 3.0, 0.5), location)
    select(hull)
    result, message = new_ship(scene, ship_id, name, "Djinni", 10)
    require(result == {"FINISHED"}, f"New Ship for {ship_id} was refused: {message}")
    return collection, hull


def mirror_pass():
    """A half hull with a Mirror modifier, its long axis on X, gets the cells of the hull the author sees: the same
    cells as the hand-mirrored solid, 10 long."""
    scene = bpy.context.scene

    def ship_cells(name, corners_y, modifier):
        collection = bpy.data.collections.new(name)
        scene.collection.children.link(collection)
        mesh = bpy.data.meshes.new(name)
        corners = [(x, y, z) for x in (0.0, 6.0) for y in corners_y for z in (-0.5, 0.5)]
        mesh.from_pydata(corners, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
        hull = new_object(collection, name + " Hull", mesh)
        if modifier:
            hull.modifiers.new("Mirror", "MIRROR").use_axis = (False, True, False)
        select(hull)
        result, message = new_ship(scene, "smoke.mirror", name, "Djinni", 10)
        require(result == {"FINISHED"}, f"New Ship for {name} was refused: {message}")
        made = buffer(bpy.context.scene)[:3]
        result, message = run(bpy.ops.aetheria.rasterise_hull)  # reads the objects as New Ship left them
        require(result == {"FINISHED"} and buffer(bpy.context.scene)[:3] == made,
                f"{name}: the cells New Ship drafted are not the cells of the hull it left: {message}")
        return made

    half = ship_cells("Mirror Half", (0.0, 1.0), True)
    solid = ship_cells("Mirror Solid", (-1.0, 1.0), False)
    require(half[1] == 10, f"The mirrored half hull is {half[1]} cells long, not 10")
    require(half == solid, "A Mirror-modified half hull does not get the cells of the hand-mirrored solid")


def shear_pass():
    """A hull parented to a rotated, non-uniformly scaled empty has a sheared world matrix, which an object cannot hold:
    New Ship drafts the cells of the hull it leaves, so Rasterise on that hull proposes the same cells."""
    scene = bpy.context.scene
    collection = bpy.data.collections.new("Shear")
    scene.collection.children.link(collection)
    holder = bpy.data.objects.new("Shear Holder", None)
    holder.rotation_euler = (0, 0, 0.7)
    holder.scale = (3, 1, 1)
    collection.objects.link(holder)
    hull = new_object(collection, "Shear Hull", box("Shear", 1.0, 0.5, 0.1), parent=holder)
    hull.rotation_euler = (0, 0, 0.5)
    select(hull)
    result, message = new_ship(scene, "smoke.shear", "Shear", "Djinni", 10)
    require(result == {"FINISHED"}, f"New Ship under a sheared parent was refused: {message}")
    made = buffer(bpy.context.scene)[:3]
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"} and buffer(bpy.context.scene)[:3] == made,
            f"The cells New Ship drafted under a sheared parent are not the cells of the hull it left: {message}")


def guards_pass(aetheria_ships):
    scene = bpy.context.scene
    path_a, path_b = str(MODS / GUARD_A / "ship.cc"), str(MODS / GUARD_B / "ship.cc")
    for ship_id in EXTRA_IDS:
        require(not (MODS / ship_id).exists(), f"{MODS / ship_id} exists; remove it first")

    # Ship A with two hardpoints in its buffer, saved.
    collection_a, hull_a = ship_from_box(scene, "Guard A", GUARD_A, (2, 1, 0))
    require(buffer(bpy.context.scene)[3] == 0, "New Ship kept the hardpoints of the ship before it in the buffer")
    for index, mount in enumerate(("mount.a", "mount.b")):
        select(hull_a)
        bpy.ops.aetheria.add_ship_hardpoint()
        hp = bpy.context.scene.aetheria_layout.hardpoints[index]
        hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint = mount, "5", index + 1, 2, "#"
    select(hull_a)
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"} and len(saved_layout(path_a, aetheria_ships)[3]) == 2, f"Ship A was not saved: {message}")

    # A pending ship over an existing ship.cc (what Ctrl+Z past a first Save restores) is refused, and the file stays.
    saved = digest(path_a)
    collection_a["aetheria.pending"] = ["Guard A", "Djinni"]
    refused(bpy.ops.aetheria.save_ship_layout, "use Bind Ship Collection with that ship.cc, then Load Layout", "Save of a pending ship over its file")
    require(Path(path_a).is_file() and digest(path_a) == saved, "A refused Save deleted or changed the author's ship.cc")
    del collection_a["aetheria.pending"]

    # A failed Save of a saved ship (a write that raises, then a .cc that changed since Load) leaves its real ship.cc.
    real = aetheria_ships.replace_layout

    def failing(*args, **kwargs):
        raise ValueError("probe: replace_layout failed")

    aetheria_ships.replace_layout = failing
    try:
        refused(bpy.ops.aetheria.save_ship_layout, "probe: replace_layout failed", "Save of a saved ship with a failing write")
    finally:
        aetheria_ships.replace_layout = real
    require(Path(path_a).is_file() and digest(path_a) == saved, "A failed Save deleted or changed a saved ship's ship.cc")
    state = bpy.context.scene.aetheria_layout
    revision, state.revision = state.revision, "changed outside Blender"
    refused(bpy.ops.aetheria.save_ship_layout, "changed since Load", "Save with a stale revision")
    state.revision = revision
    require(Path(path_a).is_file() and digest(path_a) == saved, "A stale-revision Save deleted or changed the ship.cc")

    # New Ship checks everything, the rasterising included, before it changes the collection: a vertical wall has
    # horizontal extent and no top-down area.
    wall_collection = bpy.data.collections.new("Guard Wall")
    scene.collection.children.link(wall_collection)
    wall_mesh = bpy.data.meshes.new("Guard Wall")
    wall_mesh.from_pydata([(-2, 0, -1), (2, 0, -1), (2, 0, 1), (-2, 0, 1)], [], [(0, 1, 2, 3)])
    wall = new_object(wall_collection, "Guard Wall Hull", wall_mesh)
    select(wall)
    scene.aetheria_new_ship_id = "smoke.guard.w"
    scene.aetheria_new_ship_name, scene.aetheria_new_ship_like = "Guard Wall", "Djinni"
    for attempt in ("first", "retry"):
        refused(bpy.ops.aetheria.new_ship, "no triangle with area", f"New Ship on a wall ({attempt})")
        require(not any(key.startswith("aetheria.") for key in wall_collection.keys()), "A refused New Ship bound the collection")
        require(wall.parent is None and wall.users_collection == (wall_collection,) and not wall_collection.children
                and not any(obj.get("aetheria.ship_root") for obj in wall_collection.objects),
                "A refused New Ship moved the meshes or made a Ship Root")
    select(hull_a)

    # A collection with a mesh in a child collection is not made a ship.
    child_host = bpy.data.collections.new("Guard Child")
    scene.collection.children.link(child_host)
    nested = bpy.data.collections.new("Guard Nested")
    child_host.children.link(nested)
    new_object(nested, "Guard Nested Mesh", box("Nested", 1.0, 1.0, 1.0))
    select(new_object(child_host, "Guard Child Hull", box("Child", 1.0, 3.0, 0.5)))
    scene.aetheria_new_ship_id = "smoke.guard.c"
    scene.aetheria_new_ship_name, scene.aetheria_new_ship_like = "Guard Child", "Djinni"
    refused(bpy.ops.aetheria.new_ship, "must sit directly in it", "New Ship with a mesh in a child collection")
    require("aetheria.id" not in child_host, "A refused New Ship bound the collection")

    # Ship B takes none of ship A's hardpoints.
    collection_b, hull_b = ship_from_box(scene, "Guard B", GUARD_B, (0, 0, 0))
    require(buffer(bpy.context.scene)[3] == 0, "New Ship kept the previous ship's hardpoints in the buffer")

    # A pending ship is not packaged, and nothing is written for it.
    select(hull_b)
    refused(bpy.ops.aetheria.package_ship, "Save Layout first", "Package of a pending ship")
    require(not (MODS / GUARD_B).exists(), "Package wrote for a pending ship")

    # A buffer that belongs to ship B is neither saved into ship A's file nor rasterised onto ship A.
    select(hull_a)
    refused(bpy.ops.aetheria.save_ship_layout, "Load this collection's layout before saving", "Save with another ship's buffer")
    require(digest(path_a) == saved, "Save wrote another ship's buffer into ship A's file")
    before = buffer(bpy.context.scene)
    refused(bpy.ops.aetheria.rasterise_hull, "Load this collection's layout first", "Rasterise with another ship's buffer")
    require(buffer(bpy.context.scene) == before, "Rasterise replaced another ship's buffer")

    # A failed Save leaves what the author had in a Mods/<id> folder that existed before it; the retry succeeds.
    (MODS / GUARD_B).mkdir(parents=True)
    keep = MODS / GUARD_B / "keep.txt"
    keep.write_text("an author file")
    select(hull_b)
    real = aetheria_ships.replace_layout

    def failing(*args, **kwargs):
        raise ValueError("probe: replace_layout failed")

    aetheria_ships.replace_layout = failing
    try:
        refused(bpy.ops.aetheria.save_ship_layout, "probe: replace_layout failed", "Save with a failing write into an existing folder")
    finally:
        aetheria_ships.replace_layout = real
    require(keep.is_file(), "A failed Save deleted a file the author had in the ship folder")
    require(not Path(path_b).exists() and collection_b.get("aetheria.pending") is not None, "A failed Save left a half-made ship")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"} and keep.is_file(), f"The retried Save was refused or took the author's file: {message}")
    require(saved_layout(path_b, aetheria_ships)[3] == [], "Ship B's file holds hardpoints from ship A")

    # Another ship's unsaved buffer is no reason to refuse Package: ship A's own file is what it checks.
    select(hull_a)
    result, message = run(bpy.ops.aetheria.package_ship)
    require("Save Layout first" not in message and "Save Layout first" not in scene.aetheria_package_report,
            "Package read another ship's buffer as ship A's unsaved edits")

    # A buffer whose cells do not fill its size draws no Grid.
    generated_b = child(collection_b, "Generated")
    state = bpy.context.scene.aetheria_layout
    require(any(obj.get("aetheria.grid") for obj in generated_b.objects), "Ship B has no Grid")
    state.cells.remove(len(state.cells) - 1)
    aetheria_ships._redraw_grid(collection_b, state)
    require(not any(obj.get("aetheria.grid") for obj in generated_b.objects), "A Grid is drawn from a buffer that lacks cells")

    # Rasterise needs the Ship Root New Ship made.
    select(hull_b)
    bpy.data.objects.remove(next(obj for obj in collection_b.objects if obj.get("aetheria.ship_root")))
    refused(bpy.ops.aetheria.rasterise_hull, "has no Ship Root", "Rasterise without a Ship Root")


def undo_pass():
    scene = bpy.context.scene
    collection = bpy.data.collections.new("Undo Ship")
    scene.collection.children.link(collection)
    hull = new_object(collection, "Undo Hull", box("Undo Hull", 1.0, 3.0, 0.5), (2, 1, 0))
    select(hull)
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = UNDO_SHIP, "Undo Ship"
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", 10

    def frame():
        """The ship collection, its buffer, its grid placement and the hull's placement, read fresh: undo replaces the
        data-blocks."""
        col, obj = bpy.data.collections["Undo Ship"], bpy.data.objects["Undo Hull"]
        origin = col.get("aetheria.grid_origin")
        return (col.get("aetheria.id") is not None, col.get("aetheria.pending") is not None, buffer(bpy.context.scene),
                tuple(round(v, 4) for v in origin) if origin else None,
                tuple(round(v, 4) for row in obj.matrix_world for v in row), tuple(round(v, 4) for v in obj.scale))

    def native(operator):
        """The operator as the UI calls it: its undo step is pushed when, and only when, it carries UNDO."""
        return run(lambda: operator("EXEC_DEFAULT", True))

    def undoable(operator, what):
        before = frame()
        result, message = native(operator)
        require(result == {"FINISHED"}, f"{what} was refused: {message}")
        after = frame()
        require(after != before, f"{what} changed nothing")
        result, _ = run(bpy.ops.ed.undo)
        require(result == {"FINISHED"} and frame() == before, f"{what} is not one undo step of its own (no UNDO)")
        select(bpy.data.objects["Undo Hull"])

    bpy.ops.ed.undo_push(message="start")
    undoable(bpy.ops.aetheria.new_ship, "New Ship")
    result, message = native(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    select(bpy.data.objects["Undo Hull"])
    bpy.data.objects["Undo Hull"].scale = (0.5, 1, 1)
    bpy.ops.ed.undo_push(message="Scale")  # the step the UI pushes for the author's own edit
    undoable(bpy.ops.aetheria.rasterise_hull, "Rasterise")
    result, message = native(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise was refused: {message}")
    select(bpy.data.objects["Undo Hull"])
    undoable(bpy.ops.aetheria.flip_nose, "Flip Nose")

    # Save pushes no step: one undo after it takes back the step before it, and the file stays.
    result, message = native(bpy.ops.aetheria.flip_nose)
    require(result == {"FINISHED"}, f"Flip Nose was refused: {message}")
    select(bpy.data.objects["Undo Hull"])
    flipped = frame()
    result, message = native(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save was refused: {message}")
    path = MODS / UNDO_SHIP / "ship.cc"
    require(path.is_file(), "Save did not create the file")
    result, _ = run(bpy.ops.ed.undo)
    require(result == {"FINISHED"} and frame()[2:] != flipped[2:], "Save is undoable: one undo took back Save and not the step before it")
    require(path.is_file(), "Undo changed the .cc")

    # Ctrl+Z past the first Save leaves the .cc behind an unbound collection: New Ship with the ID is refused with the
    # way forward, and following it (Bind, Load, Save) works.
    for _ in range(12):
        if not frame()[0]:
            break
        require(run(bpy.ops.ed.undo)[0] == {"FINISHED"}, "ed.undo past the first Save did not run")
    require(not frame()[0] and path.is_file(), "Undo past the first Save did not unbind the collection or changed the .cc")
    scene = bpy.context.scene
    select(bpy.data.objects["Undo Hull"])
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = UNDO_SHIP, "Undo Ship"
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", 10
    refused(bpy.ops.aetheria.new_ship, "use Bind Ship Collection with its ship.cc, then Load Layout", "New Ship over the ship the first Save made")
    scene.aetheria_ship_cc_path = str(path)
    for operator, what in ((bpy.ops.aetheria.bind_ship_collection, "Bind"), (bpy.ops.aetheria.load_ship_layout, "Load"),
                           (bpy.ops.aetheria.save_ship_layout, "Save")):
        result, message = run(operator)
        require(result == {"FINISHED"}, f"{what} on the way forward after undoing past the first Save was refused: {message}")


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
        for leftover in (SHIP_ID, *EXTRA_IDS):
            shutil.rmtree(MODS / leftover, ignore_errors=True)
    if created_mods and not existed:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
