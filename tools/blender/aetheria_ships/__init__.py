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

import subprocess
from pathlib import Path

import bpy

from .ship_cc import (HARDPOINT_TYPE_NAMES, ROTATION_NAMES, capture_grease_pencil, decode_hardpoint,
                      encode_hardpoint, read, read_layout, replace_layout, replace_lines, replace_visual)

MODEL_ASSET = "ship.glb"
AETHERDB_TIMEOUT = 600  # seconds; the first run builds AetherDb


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


class AETHERIA_OT_load_layout(bpy.types.Operator):
    bl_idname = "aetheria.load_ship_layout"
    bl_label = "Load Ship Layout"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            collection, path = _layout_path(context)
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
            self.report({"INFO"}, f"Loaded {width}x{height} layout and {len(hardpoints)} hardpoints")
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


class AETHERIA_OT_bind_ship_collection(bpy.types.Operator):
    bl_idname = "aetheria.bind_ship_collection"
    bl_label = "Bind Ship Collection"
    bl_description = "Bind the active collection to one typed ship .cc record"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            if not context.scene.aetheria_ship_cc_path:
                raise ValueError("Choose an existing ship authoring .cc file")
            obj = context.active_object
            if obj is None:
                raise ValueError("Select an object in the ship collection")
            direct = list(obj.users_collection)
            marked = [collection for collection in bpy.data.collections
                      if collection.get("aetheria.asset_kind") == "ship" and obj.name in collection.all_objects]
            if len(marked) == 1:
                collection = marked[0]
            elif len(marked) == 0 and len(direct) == 1:
                collection = direct[0]
            else:
                raise ValueError("Select an object in exactly one ship collection")
            path = bpy.path.abspath(context.scene.aetheria_ship_cc_path)
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
            self.report({"INFO"}, f"Bound {collection.name} to {ship_id}")
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

    aetheria_repo: bpy.props.StringProperty(
        name="Aetheria repo", subtype="DIR_PATH",
        description="Aetheria checkout whose tools/AetherDb validates packages. "
                    "Empty: the nearest folder above the bound .cc that holds tools/AetherDb")

    def draw(self, context):
        self.layout.prop(self, "aetheria_repo")


def _aetheria_repo(context, ship_cc):
    addon = context.preferences.addons.get(__package__)
    configured = addon.preferences.aetheria_repo if addon and addon.preferences else ""
    if configured:
        repo = Path(bpy.path.abspath(configured))
        if not (repo / "tools" / "AetherDb").is_dir():
            raise ValueError(f"The Aetheria repo preference {repo} has no tools/AetherDb")
        return repo
    for folder in Path(ship_cc).resolve().parents:
        if (folder / "tools" / "AetherDb").is_dir():
            return folder
    raise ValueError("No Aetheria repo above the ship .cc; set it in the add-on's preferences")


def _aetherdb(context, ship_cc, *args):
    """Runs one AetherDb ship-authoring command; returns its exit code and its output, stdout then stderr. The compiler
    warnings 'dotnet run' prints when it rebuilds AetherDb are left out; errors are kept."""
    repo = _aetheria_repo(context, ship_cc)
    result = subprocess.run(
        ["dotnet", "run", "--project", str(repo / "tools" / "AetherDb"), "--", "ship-authoring", *args],
        cwd=repo, capture_output=True, text=True, timeout=AETHERDB_TIMEOUT)
    lines = (result.stdout + "\n" + result.stderr).splitlines()
    return result.returncode, "\n".join(line for line in lines if line.strip() and ": warning " not in line)


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
    """Exports the collection's objects, Grease Pencil excluded, with custom properties as node extras."""
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
        for obj in collection.all_objects:
            if obj.type != "GREASEPENCIL" and obj.name in view_layer.objects:
                obj.select_set(True)
        bpy.ops.export_scene.gltf(
            filepath=filepath, export_format="GLB", use_active_collection=True, use_selection=True,
            export_extras=True, export_yup=True, export_apply=True)
    finally:
        for obj in view_layer.objects:
            obj.select_set(obj in previous_selection)
        view_layer.objects.active = previous_active
        view_layer.active_layer_collection = previous_layer


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
            code, message = _aetherdb(context, path, "validate", path)
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
        layout.operator("aetheria.load_ship_layout", icon="IMPORT")
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
           AETHERIA_OT_capture_ship_lines, AETHERIA_OT_package_ship, AETHERIA_AP_preferences, AETHERIA_PT_ship)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.aetheria_layout = bpy.props.PointerProperty(type=AETHERIA_PG_layout)
    bpy.types.Scene.aetheria_ship_cc_path = bpy.props.StringProperty(
        name="Ship .cc", subtype="FILE_PATH", description="Existing typed ship authoring record"
    )
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
    del bpy.types.Scene.aetheria_package_report
    del bpy.types.Scene.aetheria_ship_cc_path
