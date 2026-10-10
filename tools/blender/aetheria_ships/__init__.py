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
import subprocess
import sys
from pathlib import Path

import bpy
import numpy
from mathutils import Matrix

from . import hull_grid
from .ship_cc import (HARDPOINT_TYPE_NAMES, ROTATION_NAMES, capture_grease_pencil, decode_hardpoint,
                      encode_hardpoint, read, read_layout, replace_layout, replace_lines, replace_visual)

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
        raise ValueError("Ship collection has no bound .cc path")
    return collection, bpy.path.abspath(collection["aetheria.ship_cc"])


def _load_layout(context, collection, path):
    """Fills the scene's layout state from the bound .cc and redraws the Grid; returns (width, height, hardpoints)."""
    shape, hardpoints, revision = read_layout(path, _brokkr_cultlib(context))
    width, height, cells = shape
    state = context.scene.aetheria_layout
    state.ship_id = collection["aetheria.id"]
    state.ship_cc = path
    state.revision = revision
    state.width = state.new_width = width
    state.height = state.new_height = height
    state.cells.clear()
    for cell in cells:
        state.cells.add().occupied = bool(cell)
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


class AETHERIA_OT_load_layout(bpy.types.Operator):
    bl_idname = "aetheria.load_ship_layout"
    bl_label = "Load Ship Layout"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
            width, height, count = _load_layout(context, collection, path)
            self.report({"INFO"}, f"Loaded {width}x{height} layout and {count} hardpoints")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError, KeyError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


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


class AETHERIA_OT_save_layout(bpy.types.Operator):
    bl_idname = "aetheria.save_ship_layout"
    bl_label = "Save Layout to .cc"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
            state = context.scene.aetheria_layout
            if state.ship_id != collection["aetheria.id"] or state.ship_cc != path:
                raise ValueError("Load this collection's layout before saving")
            cells = [cell.occupied for cell in state.cells]
            hardpoints = [encode_hardpoint(
                              Type=int(hp.kind), Position=[hp.x, hp.y], Shape=_parse_footprint(hp.footprint),
                              Transform=hp.mount_id, Rotation=int(hp.rotation), Armor=hp.armor, FiringArc=hp.firing_arc)
                          for hp in state.hardpoints]
            state.revision = replace_layout(
                path, _brokkr_cultlib(context), state.ship_id, state.revision,
                [state.width, state.height, cells], hardpoints
            )
            _redraw_grid(collection, state)
            self.report({"INFO"}, f"Saved {len(hardpoints)} hardpoints to {path}")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError, KeyError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


def _brokkr_cultlib(context):
    brokkr = context.preferences.addons.get("brokkr_bridge")
    if brokkr is None:
        raise RuntimeError("Enable Brokkr before using Aetheria Ships")
    return brokkr.preferences.cultlib_py_src


def _ship_collection(context):
    obj = context.active_object
    if obj is None:
        raise ValueError("Select an object in the ship collection")
    matches = [collection for collection in bpy.data.collections
               if collection.get("aetheria.asset_kind") == "ship" and obj.name in collection.all_objects]
    if len(matches) != 1:
        raise ValueError("Selected object must belong to exactly one bound ship collection")
    return matches[0]


def _choose_collection(context):
    """The collection a bind acts on: the one marked ship that holds the active object, else the active object's only
    collection."""
    obj = context.active_object
    if obj is None:
        raise ValueError("Select an object in the ship collection")
    direct = list(obj.users_collection)
    marked = [collection for collection in bpy.data.collections
              if collection.get("aetheria.asset_kind") == "ship" and obj.name in collection.all_objects]
    if len(marked) == 1:
        return marked[0]
    if len(marked) == 0 and len(direct) == 1:
        return direct[0]
    raise ValueError("Select an object in exactly one ship collection")


def _bind_collection(context, path):
    """Binds the chosen collection to the ship .cc at path (aetheria.asset_kind, .id and .ship_cc), or changes nothing."""
    collection = _choose_collection(context)
    ship_id = read(path, _brokkr_cultlib(context)).ship.body[0]
    if collection.get("aetheria.asset_kind") not in (None, "ship"):
        raise ValueError(f"{collection.name} has another aetheria.asset_kind")
    if collection.get("aetheria.id") not in (None, ship_id):
        raise ValueError(f"{collection.name} is bound to another ship ID")
    stored = path
    if bpy.data.filepath:
        try:
            stored = bpy.path.relpath(path)
        except ValueError:
            pass
    collection["aetheria.asset_kind"] = "ship"
    collection["aetheria.id"] = ship_id
    collection["aetheria.ship_cc"] = stored
    return collection


class AETHERIA_OT_bind_ship_collection(bpy.types.Operator):
    bl_idname = "aetheria.bind_ship_collection"
    bl_label = "Bind Ship Collection"
    bl_description = "Bind the active collection to one typed ship .cc record"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            if not context.scene.aetheria_ship_cc_path:
                raise ValueError("Choose an existing ship authoring .cc file")
            collection = _bind_collection(context, bpy.path.abspath(context.scene.aetheria_ship_cc_path))
            self.report({"INFO"}, f"Bound {collection.name} to {collection['aetheria.id']}")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


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
                raise ValueError(f"{collection.name} has no bound ship .cc path")
            path = bpy.path.abspath(collection["aetheria.ship_cc"])
            cultlib = _brokkr_cultlib(context)
            if read(path, cultlib).ship.body[0] != collection["aetheria.id"]:
                raise ValueError("The bound collection ID does not match its .cc ship ID")
            lines = capture_grease_pencil(
                obj, context.evaluated_depsgraph_get(), context.scene.frame_current,
                evaluated=context.scene.aetheria_capture_evaluated_lines,
            )
            count = replace_lines(path, cultlib, lines)
            self.report({"INFO"}, f"Captured {count} lines into {path}")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


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
    result = subprocess.run([str(tool), "ship-authoring", *args], cwd=folder, capture_output=True, text=True,
                            timeout=AETHERDB_TIMEOUT)
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
            raise ValueError(f"{obj.name} has aetheria.role {role} but no aetheria.id")
        parent = None
        if role == "weapon-muzzle":
            parent = obj.parent.get("aetheria.id") if obj.parent else None
            if not parent:
                raise ValueError(f"Muzzle {obj.name} must be parented to its weapon mount object")
        rows.append([anchor_id, str(role), anchor_id, parent, int(obj.get("aetheria.order", 0))])
    return rows


def _export_glb(context, collection, filepath):
    """Exports the collection's objects, Grease Pencil and the Source and Generated children excluded, with custom
    properties as node extras."""
    view_layer = context.view_layer
    layer = _layer_collection(view_layer.layer_collection, collection)
    if layer is None:
        raise ValueError(f"{collection.name} is not in the current view layer")
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


def _render_meshes(collection):
    """The ship's own meshes: bound-collection meshes outside Source and Generated that carry no aetheria.role."""
    derived = _derived_names(collection)
    return [obj for obj in _meshes(collection.all_objects) if obj.name not in derived and "aetheria.role" not in obj]


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


def _redraw_grid(collection, state):
    """Redraws the Grid object from the layout state and the collection's aetheria.grid_origin. A collection without a
    Ship Root or a grid_origin has no frame yet and gets no Grid."""
    root = _ship_root(collection)
    origin = collection.get("aetheria.grid_origin")
    if root is None or origin is None or len(state.cells) != state.width * state.height:
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
    generated = _child_or_new(collection, GENERATED, GENERATED)
    grid = next((obj for obj in generated.objects if obj.get("aetheria.grid")), None)
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


def _rasterise(context, collection, path):
    """Proposes the hull's cells from the render meshes (Source while there are none), top-down in Ship Root's frame,
    writes them with replace_layout keeping the hardpoint rows, and stores the grid's placement. The only writer of
    aetheria.grid_origin."""
    root = _ship_root(collection)
    if root is None:
        raise ValueError(f"{collection.name} has no Ship Root; run New Ship first")
    source = _child(collection, SOURCE)
    meshes = _render_meshes(collection) or (_meshes(source.all_objects) if source else [])
    if not meshes:
        raise ValueError(f"{collection.name} has no render or Source mesh to rasterise")
    depsgraph = context.evaluated_depsgraph_get()
    to_root = root.matrix_world.inverted()
    triangles = []
    for obj in meshes:
        points, indices = _world_mesh(obj, depsgraph, to_root @ obj.matrix_world)
        triangles += points[indices][:, :, :2].tolist()
    width, height, cells, origin = hull_grid.rasterise(triangles)
    cultlib = _brokkr_cultlib(context)
    _, hardpoints, revision = read_layout(path, cultlib)
    replace_layout(path, cultlib, collection["aetheria.id"], revision, [width, height, cells],
                   [encode_hardpoint(**decode_hardpoint(row)) for row in hardpoints])
    collection["aetheria.grid_origin"] = [origin[0], origin[1]]
    _load_layout(context, collection, path)
    return width, height


class AETHERIA_OT_rasterise_hull(bpy.types.Operator):
    bl_idname = "aetheria.rasterise_hull"
    bl_label = "Rasterise Hull"
    bl_description = "Propose the hull's cells from its top-down silhouette; you edit them afterwards"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
            width, height = _rasterise(context, collection, path)
            self.report({"INFO"}, f"Rasterised a {width}x{height} hull")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError, KeyError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


class AETHERIA_OT_flip_nose(bpy.types.Operator):
    bl_idname = "aetheria.flip_nose"
    bl_label = "Flip Nose"
    bl_description = "Turn the hull 180 degrees about Ship Root's Z axis, then rasterise it again"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
            root = _ship_root(collection)
            if root is None:
                raise ValueError(f"{collection.name} has no Ship Root; run New Ship first")
            turn = root.matrix_world @ Matrix.Rotation(math.pi, 4, "Z") @ root.matrix_world.inverted()
            for child in root.children:
                child.matrix_world = turn @ child.matrix_world
            _rasterise(context, collection, path)
            self.report({"INFO"}, "Turned the nose about Ship Root")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError, KeyError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


def _new_ship(context, ship_id, name, reference, length):
    """Creates the ship .cc from a shipped hull, binds the active collection, moves its meshes into Source under a Ship
    Root, turns them nose to -Y, sizes them to length cells and rasterises."""
    if not ship_id or ship_id != ship_id.strip() or any(c in ship_id for c in "/\\:") or not ship_id.strip("."):
        raise ValueError("The ship ID must be a single name without slashes, colons or surrounding spaces")
    if not name.strip() or not reference.strip():
        raise ValueError("Give the ship a display name and a reference hull")
    collection = _choose_collection(context)
    meshes = _meshes(collection.objects)
    if not meshes:
        raise ValueError(f"{collection.name} holds no mesh objects to make a ship from")
    if any(obj.data.users > 1 for obj in meshes):
        raise ValueError("Make the hull meshes single-user before New Ship (Object > Relations > Make Single User)")
    worlds = {obj: obj.matrix_world.copy() for obj in meshes}
    depsgraph = context.evaluated_depsgraph_get()
    points = numpy.vstack([_world_mesh(obj, depsgraph, worlds[obj])[0] for obj in meshes])
    extent_x, extent_y = (float(v) for v in numpy.ptp(points, axis=0)[:2])
    longest = max(extent_x, extent_y)
    if longest <= 0:
        raise ValueError("The hull meshes have no horizontal extent")
    path = str(_game_folder(context, bpy.data.filepath or None) / "GameData" / "Mods" / ship_id / "ship.cc")
    code, message = _aetherdb(context, "create", path, ship_id, name, "--like", reference, ship_cc=path)
    if code != 0:
        raise RuntimeError(f"create failed: {message}")
    collection = _bind_collection(context, path)
    scale = length * hull_grid.CELL_SIZE / longest
    transform = Matrix.Diagonal((scale, scale, scale, 1.0))
    if extent_x > extent_y:
        transform = transform @ Matrix.Rotation(math.pi / 2, 4, "Z")
    source = _child_or_new(collection, SOURCE, SOURCE)
    root = bpy.data.objects.new(SHIP_ROOT, None)
    root["aetheria.ship_root"] = True
    collection.objects.link(root)
    for obj in meshes:
        placed = transform @ worlds[obj]
        collection.objects.unlink(obj)
        source.objects.link(obj)
        obj.data.transform(placed.to_3x3().to_4x4())
        if placed.determinant() < 0:
            obj.data.flip_normals()
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.rotation_mode = "XYZ"
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)
        obj.location = placed.to_translation()
    context.scene.aetheria_ship_cc_path = path
    _rasterise(context, collection, path)
    return collection


class AETHERIA_OT_new_ship(bpy.types.Operator):
    bl_idname = "aetheria.new_ship"
    bl_label = "New Ship"
    bl_description = ("Create the ship .cc beside the Aetheria repo's mods, bind the active collection, and set its "
                      "meshes up as Source: nose toward -Y, sized to the length, with a drafted grid")
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        try:
            collection = _new_ship(context, scene.aetheria_new_ship_id, scene.aetheria_new_ship_name,
                                   scene.aetheria_new_ship_like, scene.aetheria_new_ship_length)
            self.report({"INFO"}, f"New ship {collection['aetheria.id']} bound to {collection.name}")
            return {"FINISHED"}
        except (OSError, ValueError, RuntimeError, ImportError, KeyError, subprocess.SubprocessError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}


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
            cultlib = _brokkr_cultlib(context)
            ship_id = collection["aetheria.id"]
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
        except (OSError, ValueError, RuntimeError, ImportError, KeyError, subprocess.SubprocessError) as exc:
            scene.aetheria_package_report = f"Package failed: {exc}"
            self.report({"ERROR"}, scene.aetheria_package_report)
            return {"CANCELLED"}
        scene.aetheria_package_report = message or f"validate exited {code}"
        if code != 0:
            self.report({"ERROR"}, f"Packaged {len(anchors)} anchors; the validator refused it: {scene.aetheria_package_report}")
            return {"CANCELLED"}
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
        try:
            collection = _ship_collection(context)
            layout.label(text=f"Ship: {collection['aetheria.id']}")
        except (ValueError, KeyError):
            layout.label(text="No bound ship collection", icon="ERROR")
        layout.operator("aetheria.capture_ship_lines", icon="GREASEPENCIL")

        layout.separator()
        row = layout.row(align=True)
        row.operator("aetheria.load_ship_layout", icon="IMPORT")
        row.operator("aetheria.flip_nose", icon="LOOP_BACK")
        row.operator("aetheria.rasterise_hull", icon="MESH_GRID")
        state = context.scene.aetheria_layout
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
