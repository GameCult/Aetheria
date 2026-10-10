"""Aetheria ship authoring controls in Brokkr's Blender sidebar."""

bl_info = {
    "name": "Aetheria Ships for Brokkr",
    "author": "GameCult",
    "version": (0, 1, 0),
    "blender": (4, 3, 0),
    "location": "View3D > Sidebar > Brokkr > Aetheria Ship",
    "description": "Author a typed Aetheria ship package: layout, lines, anchors and its GLB",
    "category": "Object",
}

import math
import shutil
import subprocess
import sys
import traceback
from pathlib import Path

import bpy
import numpy
from mathutils import Matrix

from . import hull_grid
from .ship_cc import (HARDPOINT_MEMBERS, HARDPOINT_TYPE_NAMES, ROTATION_NAMES, capture_grease_pencil, decode_hardpoint,
                      encode_hardpoint, layout_revision, read, read_layout, replace_layout, replace_lines,
                      replace_visual, valid_ship_id)

MODEL_ASSET = "ship.glb"
SOURCE = "Source"  # child collection: the Tripo mesh, input to Rasterise, never exported
GENERATED = "Generated"  # child collection: derived objects (the Grid), never exported
SHIP_ROOT = "Ship Root"
AETHERDB_TIMEOUT = 120  # seconds


HARDPOINT_TYPES = tuple((str(i), name, name) for i, name in enumerate(HARDPOINT_TYPE_NAMES))
ROTATIONS = tuple((str(i), name, name) for i, name in enumerate(ROTATION_NAMES))


class AETHERIA_PG_cell(bpy.types.PropertyGroup):
    occupied: bpy.props.BoolProperty(name="Occupied")


class AETHERIA_PG_hardpoint(bpy.types.PropertyGroup):
    mount_id: bpy.props.StringProperty(name="Mount ID")
    kind: bpy.props.EnumProperty(name="Type", items=HARDPOINT_TYPES)
    x: bpy.props.IntProperty(name="X", min=0, max=31)
    y: bpy.props.IntProperty(name="Y", min=0, max=31)
    footprint: bpy.props.StringProperty(
        name="Footprint", default="#", description="Top-to-bottom rows of # and ., separated by /"
    )
    rotation: bpy.props.EnumProperty(name="Rotation", items=ROTATIONS)
    armor: bpy.props.FloatProperty(name="Armor", min=0)
    firing_arc: bpy.props.FloatProperty(name="Firing Arc", min=0, max=360)


class AETHERIA_PG_layout(bpy.types.PropertyGroup):
    ship_id: bpy.props.StringProperty()
    ship_cc: bpy.props.StringProperty()
    revision: bpy.props.StringProperty()
    width: bpy.props.IntProperty(default=1)
    height: bpy.props.IntProperty(default=1)
    new_width: bpy.props.IntProperty(name="Width", default=1, min=1, max=32)
    new_height: bpy.props.IntProperty(name="Height", default=1, min=1, max=32)
    cells: bpy.props.CollectionProperty(type=AETHERIA_PG_cell)
    hardpoints: bpy.props.CollectionProperty(type=AETHERIA_PG_hardpoint)


def _footprint(raw):
    width, height, cells = raw[0]
    return "/".join("".join("#" if cells[x * height + y] else "." for x in range(width))
                    for y in range(height - 1, -1, -1))


def _parse_footprint(text):
    rows = text.split("/")
    width = len(rows[0])
    height = len(rows)
    if not (1 <= width <= 32 and 1 <= height <= 32 and
            all(len(row) == width and set(row) <= {"#", "."} for row in rows)):
        raise ValueError("Footprint must use equal-width #/. rows separated by /")
    return [[width, height, [rows[height - 1 - y][x] == "#"
                             for x in range(width) for y in range(height)]]]


def _layout_path(context):
    collection = _ship_collection(context)
    if not collection.get("aetheria.ship_cc"):
        raise ValueError("The ship collection has no bound .cc path")
    return collection, bpy.path.abspath(collection["aetheria.ship_cc"])


def _set_cells(state, width, height, cells):
    state.width = state.new_width = width
    state.height = state.new_height = height
    state.cells.clear()
    for cell in cells:
        state.cells.add().occupied = bool(cell)


def _load_layout(context, collection, path):
    """Fills the scene's layout buffer from the bound .cc and redraws the Grid; returns (width, height, hardpoints)."""
    shape, hardpoints, revision = read_layout(path, _brokkr_cultlib(context))
    width, height, cells = shape
    state = context.scene.aetheria_layout
    state.ship_id = collection["aetheria.id"]
    state.ship_cc = path
    state.revision = revision
    _set_cells(state, width, height, cells)
    state.hardpoints.clear()
    for raw in hardpoints:
        fields = decode_hardpoint(raw)
        hp = state.hardpoints.add()
        hp.kind = str(fields["Type"])
        hp.x, hp.y = fields["Position"]
        hp.footprint = _footprint(fields["Shape"])
        hp.mount_id = fields["Transform"] or ""
        hp.rotation = str(fields["Rotation"])
        hp.armor = fields["Armor"]
        hp.firing_arc = fields["FiringArc"]
    _redraw_grid(collection, state)
    return width, height, len(hardpoints)


_EXPECTED = (OSError, ValueError, RuntimeError, ImportError, KeyError)


def _failure_text(exc):
    """What an operator reports for exc. This module's own ValueError and RuntimeError messages are fixed text that
    names no input; any other error is reported by its type, and its text goes to the system console."""
    if type(exc) in (ValueError, RuntimeError):
        return str(exc)
    traceback.print_exception(exc)
    return f"Stopped by {type(exc).__name__}; see the system console"


def _refuse(operator, exc):
    operator.report({"ERROR"}, _failure_text(exc))
    return {"CANCELLED"}


class AETHERIA_OT_load_layout(bpy.types.Operator):
    bl_idname = "aetheria.load_ship_layout"
    bl_label = "Load Ship Layout"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
            if collection.get("aetheria.pending"):
                raise ValueError("Not saved yet; Save Layout creates it")
            width, height, count = _load_layout(context, collection, path)
            self.report({"INFO"}, f"Loaded {width}x{height} layout and {count} hardpoints")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


class AETHERIA_OT_resize_layout(bpy.types.Operator):
    bl_idname = "aetheria.resize_ship_layout"
    bl_label = "Resize Grid"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        state = context.scene.aetheria_layout
        old = [cell.occupied for cell in state.cells]
        width, height = state.width, state.height
        expanded = [old[x * height + y] if x < width and y < height else False
                    for x in range(state.new_width) for y in range(state.new_height)]
        state.cells.clear()
        for cell in expanded:
            state.cells.add().occupied = cell
        state.width, state.height = state.new_width, state.new_height
        return {"FINISHED"}


class AETHERIA_OT_add_hardpoint(bpy.types.Operator):
    bl_idname = "aetheria.add_ship_hardpoint"
    bl_label = "Add Hardpoint"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        context.scene.aetheria_layout.hardpoints.add()
        return {"FINISHED"}


class AETHERIA_OT_remove_hardpoint(bpy.types.Operator):
    bl_idname = "aetheria.remove_ship_hardpoint"
    bl_label = "Remove Hardpoint"
    bl_options = {"REGISTER", "UNDO"}
    index: bpy.props.IntProperty()

    def execute(self, context):
        state = context.scene.aetheria_layout
        if 0 <= self.index < len(state.hardpoints):
            state.hardpoints.remove(self.index)
        return {"FINISHED"}


def _encode_buffer(state):
    """The layout buffer as ship_cc stores it: (shape, hardpoint rows)."""
    shape = [state.width, state.height, [cell.occupied for cell in state.cells]]
    rows = [encode_hardpoint(Type=int(hp.kind), Position=[hp.x, hp.y], Shape=_parse_footprint(hp.footprint),
                             Transform=hp.mount_id, Rotation=int(hp.rotation), Armor=hp.armor, FiringArc=hp.firing_arc)
            for hp in state.hardpoints]
    return shape, rows


def _save_layout(context, collection, state):
    """The one writer of the .cc layout: writes the layout buffer, creating the file first when the ship is pending. A
    failure after the file was created removes what this save made. Returns the hardpoint count."""
    if not collection.get("aetheria.ship_cc"):
        raise ValueError("The ship collection has no bound .cc path")
    path = bpy.path.abspath(collection["aetheria.ship_cc"])
    ship_id = collection["aetheria.id"]
    if state.ship_id != ship_id or state.ship_cc != path:
        raise ValueError("Load this collection's layout before saving")
    cultlib = _brokkr_cultlib(context)
    shape, rows = _encode_buffer(state)
    pending = collection.get("aetheria.pending")
    folder = Path(path).parent
    if pending and Path(path).exists():
        raise ValueError("A ship file already exists at the bound path (Ctrl+Z past a first Save leaves it there); "
                         "use Bind Ship Collection with that ship.cc, then Load Layout")
    made_folder = bool(pending) and not folder.exists()
    revision = state.revision
    try:
        if pending:
            code, output = _aetherdb(context, "create", path, ship_id, pending[0], "--like", pending[1], ship_cc=path)
            if output:
                print(output)
            if code != 0:
                raise RuntimeError("AetherDb refused to create the ship; its message is in the system console")
            revision = read_layout(path, cultlib)[2]
        revision = replace_layout(path, cultlib, ship_id, revision, shape, rows)
    except Exception:
        if pending:
            Path(path).unlink(missing_ok=True)
            if made_folder:
                shutil.rmtree(folder, ignore_errors=True)
        raise
    state.revision = revision
    if pending:
        del collection["aetheria.pending"]
    _redraw_grid(collection, state)
    return len(rows)


class AETHERIA_OT_save_layout(bpy.types.Operator):
    bl_idname = "aetheria.save_ship_layout"
    bl_label = "Save Layout to .cc"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            collection, _ = _layout_path(context)
            count = _save_layout(context, collection, context.scene.aetheria_layout)
            self.report({"INFO"}, f"Saved the layout and {count} hardpoints")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


def _brokkr_cultlib(context):
    brokkr = context.preferences.addons.get("brokkr_bridge")
    if brokkr is None:
        raise RuntimeError("Enable Brokkr before using Aetheria Ships")
    return brokkr.preferences.cultlib_py_src


def _is_ship(collection):
    return collection.get("aetheria.asset_kind") == "ship"


def _ship_collection(context, unbound_ok=False):
    """The one chooser: the ship collection holding the active object. unbound_ok (Bind, New Ship) also accepts the
    active object's only collection when no ship collection holds it."""
    obj = context.active_object
    if obj is None:
        raise ValueError("Select an object in the ship collection")
    ships = [collection for collection in bpy.data.collections if _is_ship(collection) and obj.name in collection.all_objects]
    if len(ships) == 1:
        return ships[0]
    if unbound_ok and not ships and len(obj.users_collection) == 1:
        return obj.users_collection[0]
    raise ValueError("Select an object in exactly one ship collection" if unbound_ok
                     else "Selected object must belong to exactly one bound ship collection")


def _bind_collection(collection, path, ship_id, pending=None):
    """Binds the collection to the ship .cc at path (aetheria.asset_kind, .id and .ship_cc, and .pending = [name,
    reference] for a ship whose file the first Save creates), or changes nothing."""
    if collection.get("aetheria.asset_kind") not in (None, "ship"):
        raise ValueError("The collection has another aetheria.asset_kind")
    if collection.get("aetheria.id") not in (None, ship_id):
        raise ValueError("The collection is bound to another ship ID")
    stored = path
    if bpy.data.filepath:
        try:
            stored = bpy.path.relpath(path)
        except ValueError:
            pass
    keys = ("aetheria.asset_kind", "aetheria.id", "aetheria.ship_cc", "aetheria.pending")
    before = {key: collection[key] for key in keys if key in collection}
    values = {"aetheria.asset_kind": "ship", "aetheria.id": ship_id, "aetheria.ship_cc": stored}
    if pending:
        values["aetheria.pending"] = pending
    try:
        for key, value in values.items():
            collection[key] = value
        if not pending and "aetheria.pending" in collection:
            del collection["aetheria.pending"]
    except Exception:
        for key in keys:  # the ID-property store refused a value: put every key back as it was
            if key in before:
                collection[key] = before[key]
            elif key in collection:
                del collection[key]
        raise


class AETHERIA_OT_bind_ship_collection(bpy.types.Operator):
    bl_idname = "aetheria.bind_ship_collection"
    bl_label = "Bind Ship Collection"
    bl_description = "Bind the active collection to one typed ship .cc record"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            if not context.scene.aetheria_ship_cc_path:
                raise ValueError("Choose an existing ship authoring .cc file")
            path = bpy.path.abspath(context.scene.aetheria_ship_cc_path)
            collection = _ship_collection(context, unbound_ok=True)
            _bind_collection(collection, path, read(path, _brokkr_cultlib(context)).ship.body[0])
            self.report({"INFO"}, "Bound the ship collection")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


class AETHERIA_OT_capture_ship_lines(bpy.types.Operator):
    bl_idname = "aetheria.capture_ship_lines"
    bl_label = "Capture Ship Lines"
    bl_description = "Replace only the line slot in the selected typed ship .cc record"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            obj = context.active_object
            collection = _ship_collection(context)
            if not collection.get("aetheria.ship_cc"):
                raise ValueError("The ship collection has no bound .cc path")
            path = bpy.path.abspath(collection["aetheria.ship_cc"])
            cultlib = _brokkr_cultlib(context)
            if read(path, cultlib).ship.body[0] != collection["aetheria.id"]:
                raise ValueError("The bound collection ID does not match its .cc ship ID")
            lines = capture_grease_pencil(
                obj, context.evaluated_depsgraph_get(), context.scene.frame_current,
                evaluated=context.scene.aetheria_capture_evaluated_lines,
            )
            count = replace_lines(path, cultlib, lines)
            self.report({"INFO"}, f"Captured {count} lines")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


class AETHERIA_AP_preferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    game_folder: bpy.props.StringProperty(
        name="Game folder", subtype="DIR_PATH",
        description="The folder whose GameData holds Aetheria.cc and Mods (the install folder); its ModTools holds "
                    "AetherDb. Empty: the nearest folder above the bound .cc whose GameData holds Aetheria.cc")

    def draw(self, context):
        self.layout.prop(self, "game_folder")


GAME_FOLDER_REFUSAL = ("Set the add-on's Game folder preference to the folder whose GameData holds Aetheria.cc "
                       "and whose ModTools holds AetherDb")


def _game_folder(context, ship_cc=None):
    """The folder AetherDb runs against: the Game folder preference, else the nearest folder above the bound .cc whose
    GameData holds Aetheria.cc. Refusals name the preference and never a path."""
    addon = context.preferences.addons.get(__package__)
    configured = addon.preferences.game_folder if addon and addon.preferences else ""
    if configured:
        folder = Path(bpy.path.abspath(configured))
        if not (folder / "GameData" / "Aetheria.cc").is_file():
            raise ValueError(GAME_FOLDER_REFUSAL)
        return folder
    if ship_cc:
        for folder in Path(ship_cc).resolve().parents:
            if (folder / "GameData" / "Aetheria.cc").is_file():
                return folder
    raise ValueError(GAME_FOLDER_REFUSAL)


def _aetherdb(context, *args, ship_cc=None):
    """Runs one AetherDb ship-authoring command from the game folder's ModTools; returns its exit code and its output,
    stdout then stderr. ship_cc is the bound .cc the game folder is found from when the preference is empty."""
    folder = _game_folder(context, ship_cc)
    tool = folder / "ModTools" / ("AetherDb.exe" if sys.platform == "win32" else "AetherDb")
    if not tool.is_file():
        raise ValueError(GAME_FOLDER_REFUSAL)
    try:
        result = subprocess.run([str(tool), "ship-authoring", *args], cwd=folder, capture_output=True, text=True,
                                timeout=AETHERDB_TIMEOUT)
    except subprocess.TimeoutExpired:
        raise RuntimeError("AetherDb did not finish in time") from None
    except OSError:
        raise RuntimeError("AetherDb could not be started") from None
    lines = (result.stdout + "\n" + result.stderr).splitlines()
    return result.returncode, "\n".join(line for line in lines if line.strip())


def _layer_collection(layer, collection):
    if layer.collection == collection:
        return layer
    for child in layer.children:
        found = _layer_collection(child, collection)
        if found:
            return found
    return None


def _anchors(collection):
    """One ShipAnchor row per object tagged aetheria.role. Its aetheria.id is both the anchor id and the GLB node id."""
    rows = []
    for obj in sorted(collection.all_objects, key=lambda candidate: candidate.name):
        role = obj.get("aetheria.role")
        if role is None:
            continue
        anchor_id = obj.get("aetheria.id")
        if not isinstance(anchor_id, str) or not anchor_id.strip():
            raise ValueError("An object with aetheria.role has no aetheria.id")
        parent = None
        if role == "weapon-muzzle":
            parent = obj.parent.get("aetheria.id") if obj.parent else None
            if not parent:
                raise ValueError("A weapon-muzzle must be parented to its weapon mount object")
        rows.append([anchor_id, str(role), anchor_id, parent, int(obj.get("aetheria.order", 0))])
    return rows


def _export_glb(context, collection, filepath):
    """Exports the collection's objects, Grease Pencil and the Source and Generated children excluded, with custom
    properties as node extras."""
    view_layer = context.view_layer
    layer = _layer_collection(view_layer.layer_collection, collection)
    if layer is None:
        raise ValueError("The ship collection is not in the current view layer")
    previous_layer = view_layer.active_layer_collection
    previous_selection = [obj for obj in view_layer.objects if obj.select_get()]
    previous_active = view_layer.objects.active
    try:
        view_layer.active_layer_collection = layer
        for obj in previous_selection:
            obj.select_set(False)
        derived = _derived_names(collection)
        for obj in collection.all_objects:
            if obj.type != "GREASEPENCIL" and obj.name not in derived and obj.name in view_layer.objects:
                obj.select_set(True)
        bpy.ops.export_scene.gltf(
            filepath=filepath, export_format="GLB", use_active_collection=True, use_selection=True,
            export_extras=True, export_yup=True, export_apply=True)
    except RuntimeError:  # the exporter's text holds paths and a traceback
        traceback.print_exc()
        raise RuntimeError("The GLB export failed; its message is in the system console") from None
    finally:
        for obj in view_layer.objects:
            obj.select_set(obj in previous_selection)
        view_layer.objects.active = previous_active
        view_layer.active_layer_collection = previous_layer


def _child(collection, kind):
    """The child collection tagged aetheria.frame == kind. Blender keeps collection names unique per file, so the
    name 'Source' or 'Generated' cannot identify it."""
    return next((child for child in collection.children if child.get("aetheria.frame") == kind), None)


def _child_or_new(collection, kind, name):
    child = _child(collection, kind)
    if child is None:
        child = bpy.data.collections.new(name)
        child["aetheria.frame"] = kind
        collection.children.link(child)
    return child


def _derived_names(collection):
    """Names of the objects in the Source and Generated children, which are never exported."""
    return {obj.name for kind in (SOURCE, GENERATED) for child in [_child(collection, kind)] if child
            for obj in child.all_objects}


def _ship_root(collection):
    return next((obj for obj in collection.objects if obj.get("aetheria.ship_root")), None)


def _meshes(objects):
    return [obj for obj in objects if obj.type == "MESH"]


def _frame_meshes(collection):
    """The ship's own meshes: Ship Root's descendants outside Source and Generated that carry no aetheria.role; the
    Source meshes while there are none."""
    derived = _derived_names(collection)
    own = [obj for obj in _meshes(_ship_root(collection).children_recursive)
           if obj.name not in derived and "aetheria.role" not in obj]
    source = _child(collection, SOURCE)
    return own or (_meshes(source.all_objects) if source else [])


def _loose_meshes(collection):
    """Meshes of a collection that has a Ship Root which are not under the root, Generated's apart. Source meshes count:
    Rasterise reads them and Flip Nose turns only what hangs from Ship Root."""
    root = _ship_root(collection)
    if root is None:
        return []
    generated = _child(collection, GENERATED)
    derived = {obj.name for obj in generated.all_objects} if generated else set()
    framed = {obj.name for obj in root.children_recursive}
    return [obj for obj in _meshes(collection.all_objects) if obj.name not in derived and obj.name not in framed]


def _refuse_loose_meshes(context, collection):
    """Refuses while meshes sit outside Ship Root, selecting them so the author can parent them."""
    loose = _loose_meshes(collection)
    if not loose:
        return
    for obj in context.view_layer.objects:
        obj.select_set(False)
    for obj in loose:
        if obj.name in context.view_layer.objects:
            obj.select_set(True)
    raise ValueError(f"{len(loose)} meshes in the ship collection are not under Ship Root; parent them to it")


def _frame_inputs(context, collection, state):
    """Ship Root and the meshes Rasterise reads, or the refusal that stops Rasterise and Flip Nose before they change
    anything."""
    root = _ship_root(collection)
    if root is None:
        raise ValueError("The ship collection has no Ship Root; run New Ship first")
    if state.ship_id != collection["aetheria.id"]:
        raise ValueError("Load this collection's layout first")
    _refuse_loose_meshes(context, collection)
    meshes = _frame_meshes(collection)
    if not meshes:
        raise ValueError("The ship collection has no render or Source mesh to rasterise")
    return root, meshes


def _world_mesh(obj, depsgraph, matrix):
    """The evaluated mesh's points through matrix, as (points, triangles) numpy arrays."""
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        points = numpy.empty(len(mesh.vertices) * 3, dtype=numpy.float32)
        mesh.vertices.foreach_get("co", points)
        mesh.calc_loop_triangles()
        triangles = numpy.empty(len(mesh.loop_triangles) * 3, dtype=numpy.int32)
        mesh.loop_triangles.foreach_get("vertices", triangles)
    finally:
        evaluated.to_mesh_clear()
    linear = numpy.array(matrix, dtype=numpy.float64)
    return points.reshape(-1, 3) @ linear[:3, :3].T + linear[:3, 3], triangles.reshape(-1, 3)


def _held(matrix):
    """The world matrix an object holds after matrix_world = matrix: Blender keeps location, rotation and scale, so a
    shear (a non-uniform scaled parent under rotation) is dropped. The hull the author sees afterwards is this one."""
    return Matrix.LocRotScale(*matrix.decompose())


def _triangles_xy(depsgraph, placements):
    """The top-down triangles of the evaluated meshes, each through its matrix: what the author sees from above.
    placements is [(object, matrix)]."""
    triangles = []
    for obj, matrix in placements:
        points, indices = _world_mesh(obj, depsgraph, matrix)
        triangles += points[indices][:, :, :2].tolist()
    return triangles


def _grid_current(collection, state):
    """Whether the Grid can be drawn: grid_origin was computed for the buffer's own size."""
    origin = collection.get("aetheria.grid_origin")
    return (origin is not None and len(origin) == 4 and (int(origin[2]), int(origin[3])) == (state.width, state.height)
            and len(state.cells) == state.width * state.height)


def _redraw_grid(collection, state):
    """Redraws the Grid object from the layout buffer and the collection's aetheria.grid_origin while the origin was
    computed for the buffer's size; otherwise removes the Grid. A collection without a Ship Root or a grid_origin has no
    frame yet and gets no Grid."""
    root = _ship_root(collection)
    origin = collection.get("aetheria.grid_origin")
    if root is None or origin is None:
        return
    generated = _child_or_new(collection, GENERATED, GENERATED)
    grid = next((obj for obj in generated.objects if obj.get("aetheria.grid")), None)
    if not _grid_current(collection, state):
        if grid is not None:
            stale = grid.data
            bpy.data.objects.remove(grid)
            if stale.users == 0:
                bpy.data.meshes.remove(stale)
        return
    origin = (float(origin[0]), float(origin[1]))
    half = hull_grid.CELL_SIZE / 2
    verts, edges, faces = [], [], []
    for x in range(state.width):
        for y in range(state.height):
            cx, cy = hull_grid.cell_centre(origin, x, y)
            i = len(verts)
            verts += [(cx - half, cy - half, 0.0), (cx + half, cy - half, 0.0), (cx + half, cy + half, 0.0),
                      (cx - half, cy + half, 0.0)]
            if state.cells[x * state.height + y].occupied:
                faces.append((i, i + 1, i + 2, i + 3))
            else:
                edges += [(i, i + 1), (i + 1, i + 2), (i + 2, i + 3), (i + 3, i)]
    mesh = bpy.data.meshes.new("Grid")
    mesh.from_pydata(verts, edges, faces)
    mesh.update(calc_edges=True)
    if grid is None:
        grid = bpy.data.objects.new("Grid", mesh)
        grid["aetheria.grid"] = True
        generated.objects.link(grid)
    else:
        previous, grid.data = grid.data, mesh
        if previous.users == 0:
            bpy.data.meshes.remove(previous)
    grid.parent = root
    grid.matrix_parent_inverse = Matrix.Identity(4)
    grid.location = (0.0, 0.0, 0.0)
    grid.rotation_mode = "XYZ"
    grid.rotation_euler = (0.0, 0.0, 0.0)
    grid.scale = (1.0, 1.0, 1.0)
    grid.display_type = "WIRE"


def _rasterise(context, collection, state):
    """Proposes the hull's cells from the frame meshes, top-down in Ship Root's frame, into the layout buffer (its
    hardpoints stay) and stores the grid's placement with the size it was computed for. Writes no file."""
    root, meshes = _frame_inputs(context, collection, state)
    to_root = root.matrix_world.inverted()
    width, height, cells, origin = hull_grid.rasterise(_triangles_xy(
        context.evaluated_depsgraph_get(), [(obj, to_root @ obj.matrix_world) for obj in meshes]))
    _place_grid(collection, state, width, height, cells, origin)
    return width, height


def _place_grid(collection, state, width, height, cells, origin):
    """Puts a rasterised hull into the layout buffer (its hardpoints stay), stores the grid's placement with the size it
    was computed for and redraws the Grid. The only writer of aetheria.grid_origin."""
    _set_cells(state, width, height, cells)
    collection["aetheria.grid_origin"] = [float(origin[0]), float(origin[1]), width, height]
    _redraw_grid(collection, state)


class AETHERIA_OT_rasterise_hull(bpy.types.Operator):
    bl_idname = "aetheria.rasterise_hull"
    bl_label = "Rasterise Hull"
    bl_description = "Propose the hull's cells from its top-down silhouette; you edit them afterwards, then Save Layout"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            width, height = _rasterise(context, _ship_collection(context), context.scene.aetheria_layout)
            self.report({"INFO"}, f"Rasterised a {width}x{height} hull")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


class AETHERIA_OT_flip_nose(bpy.types.Operator):
    bl_idname = "aetheria.flip_nose"
    bl_label = "Flip Nose"
    bl_description = "Turn the hull 180 degrees about Ship Root's Z axis, then rasterise it again"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            collection = _ship_collection(context)
            state = context.scene.aetheria_layout
            root, _ = _frame_inputs(context, collection, state)
            turn = root.matrix_world @ Matrix.Rotation(math.pi, 4, "Z") @ root.matrix_world.inverted()
            for child in root.children:
                child.matrix_world = turn @ child.matrix_world
            try:
                _rasterise(context, collection, state)
            except Exception:
                for child in root.children:  # a half turn is its own inverse
                    child.matrix_world = turn @ child.matrix_world
                raise
            self.report({"INFO"}, "Turned the nose about Ship Root")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


def _new_ship(context, ship_id, name, reference, length):
    """Sets the active collection up as a pending ship: binds it, moves its meshes into Source under a Ship Root, turns
    them nose to -Y, sizes them to length cells, fills the layout buffer and draws the Grid. The meshes keep their data:
    the placement is the objects' own matrix, so modifiers act as the author saw them and the cells are the ones checked
    here. Every check and the rasterising come first; after the bind nothing can refuse. Nothing is written to disk: the
    first Save creates the file."""
    if not valid_ship_id(ship_id):
        raise ValueError("The ship ID must use lower-case letters, digits, dots, underscores or hyphens, must not end "
                         "in a dot, and must not be a Windows device name")
    if not name.strip() or not reference.strip():
        raise ValueError("Give the ship a display name and a reference hull")
    path = _game_folder(context, bpy.data.filepath or None) / "GameData" / "Mods" / ship_id / "ship.cc"
    if path.exists():
        raise ValueError("A ship with this ID already exists under GameData/Mods (Ctrl+Z past a first Save leaves it "
                         "there); use Bind Ship Collection with its ship.cc, then Load Layout, or give the new ship "
                         "another ID")
    collection = _ship_collection(context, unbound_ok=True)
    if collection.get("aetheria.id") is not None or _ship_root(collection) is not None:
        raise ValueError("The collection is already a ship")
    meshes = _meshes(collection.objects)
    if not meshes:
        raise ValueError("The ship collection holds no mesh objects to make a ship from")
    if len(_meshes(collection.all_objects)) != len(meshes):
        raise ValueError("The ship collection's meshes must sit directly in it, not in child collections")
    worlds = {obj: obj.matrix_world.copy() for obj in meshes}
    depsgraph = context.evaluated_depsgraph_get()
    points = numpy.vstack([_world_mesh(obj, depsgraph, worlds[obj])[0] for obj in meshes])
    extent_x, extent_y = (float(v) for v in numpy.ptp(points, axis=0)[:2])
    longest = max(extent_x, extent_y)
    if longest <= 0:
        raise ValueError("The hull meshes have no horizontal extent")
    scale = length * hull_grid.CELL_SIZE / longest
    transform = Matrix.Diagonal((scale, scale, scale, 1.0))
    if extent_x > extent_y:
        transform = transform @ Matrix.Rotation(math.pi / 2, 4, "Z")
    placed = {obj: _held(transform @ worlds[obj]) for obj in meshes}
    width, height, cells, origin = hull_grid.rasterise(_triangles_xy(depsgraph, [(obj, placed[obj]) for obj in meshes]))
    _bind_collection(collection, str(path), ship_id, [name, reference])
    source = _child_or_new(collection, SOURCE, SOURCE)
    root = bpy.data.objects.new(SHIP_ROOT, None)
    root["aetheria.ship_root"] = True
    collection.objects.link(root)
    for obj in meshes:
        collection.objects.unlink(obj)
        source.objects.link(obj)
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_world = placed[obj]
    state = context.scene.aetheria_layout
    state.ship_id = ship_id
    state.ship_cc = bpy.path.abspath(collection["aetheria.ship_cc"])
    state.revision = ""
    state.hardpoints.clear()
    _place_grid(collection, state, width, height, cells, origin)


class AETHERIA_OT_new_ship(bpy.types.Operator):
    bl_idname = "aetheria.new_ship"
    bl_label = "New Ship"
    bl_description = ("Set the active collection up as a new ship: nose toward -Y, sized to the length, with a drafted "
                      "grid. Nothing is written until Save Layout")
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        try:
            _new_ship(context, scene.aetheria_new_ship_id, scene.aetheria_new_ship_name,
                      scene.aetheria_new_ship_like, scene.aetheria_new_ship_length)
            self.report({"INFO"}, "New ship set up; Save Layout creates its file")
            return {"FINISHED"}
        except _EXPECTED as exc:
            return _refuse(self, exc)


class AETHERIA_OT_package_ship(bpy.types.Operator):
    bl_idname = "aetheria.package_ship"
    bl_label = "Package Ship"
    bl_description = ("Write the anchors and ship.glb from the bound collection, capture its LineArt, "
                      "and run AetherDb's validator")
    bl_options = {"REGISTER"}

    def execute(self, context):
        scene = context.scene
        try:
            collection, path = _layout_path(context)
            if collection.get("aetheria.pending"):
                raise ValueError("Save Layout first")
            _refuse_loose_meshes(context, collection)
            cultlib = _brokkr_cultlib(context)
            ship_id = collection["aetheria.id"]
            state = scene.aetheria_layout
            if state.ship_id == ship_id and state.ship_cc == path:
                shape, rows = _encode_buffer(state)
                saved_shape, saved_rows, _ = read_layout(path, cultlib)
                if layout_revision(shape, rows) != layout_revision(
                        saved_shape, [row[:len(HARDPOINT_MEMBERS)] for row in saved_rows]):
                    raise ValueError("Save Layout first")
            if read(path, cultlib).ship.body[0] != ship_id:
                raise ValueError("The bound collection ID does not match its .cc ship ID")
            anchors = _anchors(collection)
            _export_glb(context, collection, str(Path(path).with_name(MODEL_ASSET)))
            replace_visual(path, cultlib, ship_id, MODEL_ASSET, anchors)
            pencils = [obj for obj in collection.all_objects if obj.type == "GREASEPENCIL"]
            if len(pencils) == 1:
                replace_lines(path, cultlib, capture_grease_pencil(
                    pencils[0], context.evaluated_depsgraph_get(), scene.frame_current,
                    evaluated=scene.aetheria_capture_evaluated_lines))
            code, message = _aetherdb(context, "validate", path, ship_cc=path)
        except _EXPECTED as exc:
            scene.aetheria_package_report = f"Package failed: {_failure_text(exc)}"
            self.report({"ERROR"}, scene.aetheria_package_report)
            return {"CANCELLED"}
        if message:
            print(message)
        if code != 0:
            scene.aetheria_package_report = (f"Packaged {len(anchors)} anchors; the validator refused the ship; "
                                             "its message is in the system console")
            self.report({"ERROR"}, scene.aetheria_package_report)
            return {"CANCELLED"}
        scene.aetheria_package_report = f"Packaged {len(anchors)} anchors; the validator accepted the ship"
        self.report({"INFO"}, scene.aetheria_package_report)
        return {"FINISHED"}


class AETHERIA_PT_ship(bpy.types.Panel):
    bl_label = "Aetheria Ship"
    bl_idname = "AETHERIA_PT_ship"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Brokkr"

    def draw(self, context):
        layout = self.layout
        new = layout.box()
        new.label(text="New Ship")
        for prop in ("aetheria_new_ship_id", "aetheria_new_ship_name", "aetheria_new_ship_like",
                     "aetheria_new_ship_length"):
            new.prop(context.scene, prop)
        new.operator("aetheria.new_ship", icon="ADD")
        layout.prop(context.scene, "aetheria_ship_cc_path")
        layout.operator("aetheria.bind_ship_collection", icon="LINKED")
        layout.prop(context.scene, "aetheria_capture_evaluated_lines")
        obj = context.active_object
        layout.label(text=f"Source: {obj.name if obj else '(none)'}")
        state = context.scene.aetheria_layout
        try:
            collection = _ship_collection(context)
            layout.label(text=f"Ship: {collection['aetheria.id']}")
            if collection.get("aetheria.pending"):
                layout.label(text="Not saved yet: Save Layout creates the file", icon="INFO")
            elif (collection.get("aetheria.grid_origin") is not None and state.ship_id == collection["aetheria.id"]
                  and not _grid_current(collection, state)):
                layout.label(text="Rasterise again: the grid no longer matches the layout", icon="INFO")
        except (ValueError, KeyError):
            layout.label(text="No bound ship collection", icon="ERROR")
        layout.operator("aetheria.capture_ship_lines", icon="GREASEPENCIL")

        layout.separator()
        row = layout.row(align=True)
        row.operator("aetheria.load_ship_layout", icon="IMPORT")
        row.operator("aetheria.flip_nose", icon="LOOP_BACK")
        row.operator("aetheria.rasterise_hull", icon="MESH_GRID")
        if not state.ship_id:
            self._draw_package(context, layout)
            return
        layout.label(text=f"Layout: {state.ship_id}")
        dims = layout.row(align=True)
        dims.prop(state, "new_width")
        dims.prop(state, "new_height")
        dims.operator("aetheria.resize_ship_layout", text="Resize")
        grid = layout.box()
        grid.label(text="Hull cells (top row first)")
        if len(state.cells) == state.width * state.height:
            for y in range(state.height - 1, -1, -1):
                row = grid.row(align=True)
                row.label(text=str(y))
                for x in range(state.width):
                    row.prop(state.cells[x * state.height + y], "occupied", text="")
        layout.label(text="Hardpoints")
        for index, hp in enumerate(state.hardpoints):
            box = layout.box()
            header = box.row(align=True)
            header.prop(hp, "mount_id")
            header.operator("aetheria.remove_ship_hardpoint", text="", icon="X").index = index
            box.prop(hp, "kind")
            position = box.row(align=True)
            position.prop(hp, "x")
            position.prop(hp, "y")
            box.prop(hp, "footprint")
            box.prop(hp, "rotation")
            box.prop(hp, "armor")
            box.prop(hp, "firing_arc")
        layout.operator("aetheria.add_ship_hardpoint", icon="ADD")
        layout.operator("aetheria.save_ship_layout", icon="FILE_TICK")
        self._draw_package(context, layout)

    @staticmethod
    def _draw_package(context, layout):
        layout.separator()
        layout.operator("aetheria.package_ship", icon="PACKAGE")
        report = context.scene.aetheria_package_report
        if report:
            box = layout.box()
            for line in report.splitlines():
                box.label(text=line)


classes = (AETHERIA_PG_cell, AETHERIA_PG_hardpoint, AETHERIA_PG_layout,
           AETHERIA_OT_load_layout, AETHERIA_OT_resize_layout,
           AETHERIA_OT_add_hardpoint, AETHERIA_OT_remove_hardpoint,
           AETHERIA_OT_save_layout, AETHERIA_OT_bind_ship_collection,
           AETHERIA_OT_capture_ship_lines, AETHERIA_OT_new_ship, AETHERIA_OT_flip_nose,
           AETHERIA_OT_rasterise_hull, AETHERIA_OT_package_ship, AETHERIA_AP_preferences, AETHERIA_PT_ship)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.aetheria_layout = bpy.props.PointerProperty(type=AETHERIA_PG_layout)
    bpy.types.Scene.aetheria_ship_cc_path = bpy.props.StringProperty(
        name="Ship .cc", subtype="FILE_PATH", description="Existing typed ship authoring record"
    )
    bpy.types.Scene.aetheria_new_ship_id = bpy.props.StringProperty(
        name="ID", description="The new ship's ID; also the folder under GameData/Mods")
    bpy.types.Scene.aetheria_new_ship_name = bpy.props.StringProperty(name="Name", description="Display name")
    bpy.types.Scene.aetheria_new_ship_like = bpy.props.StringProperty(
        name="Like", default="Djinni", description="Shipped hull whose stats the new ship starts from")
    bpy.types.Scene.aetheria_new_ship_length = bpy.props.IntProperty(
        name="Length (cells)", default=10, min=1, max=hull_grid.MAX_CELLS,
        description="Cells from stern to nose; the hull's long axis becomes this many 2 m cells")
    bpy.types.Scene.aetheria_package_report = bpy.props.StringProperty(
        name="Package report", description="AetherDb's verdict on the last Package Ship")
    bpy.types.Scene.aetheria_capture_evaluated_lines = bpy.props.BoolProperty(
        name="Evaluated LineArt", default=True,
        description="Capture the visible modifier result at the current frame",
    )


def unregister():
    del bpy.types.Scene.aetheria_layout
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.aetheria_capture_evaluated_lines
    del bpy.types.Scene.aetheria_new_ship_length
    del bpy.types.Scene.aetheria_new_ship_like
    del bpy.types.Scene.aetheria_new_ship_name
    del bpy.types.Scene.aetheria_new_ship_id
    del bpy.types.Scene.aetheria_package_report
    del bpy.types.Scene.aetheria_ship_cc_path

