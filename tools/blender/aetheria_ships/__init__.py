"""Aetheria ship authoring controls in Brokkr's Blender sidebar."""

bl_info = {
    "name": "Aetheria Ships for Brokkr",
    "author": "GameCult",
    "version": (0, 1, 0),
    "blender": (4, 3, 0),
    "location": "View3D > Sidebar > Brokkr > Aetheria Ship",
    "description": "Write Grease Pencil ship lines into a typed Aetheria .cc ship record",
    "category": "Object",
}

import bpy

from .ship_cc import capture_grease_pencil, read, read_layout, replace_layout, replace_lines


HARDPOINT_TYPES = tuple((str(i), name, name) for i, name in enumerate((
    "Hull", "Tool", "Thermal", "Thruster", "WarpDrive", "Reactor", "Radiator",
    "Shield", "Sensors", "Energy", "Ballistic", "Launcher", "ControlModule", "AetherDrive",
)))
ROTATIONS = (("0", "None", "None"), ("1", "Counterclockwise", "Counterclockwise"),
             ("2", "Reversed", "Reversed"), ("3", "Clockwise", "Clockwise"))


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
                hp = state.hardpoints.add()
                hp.kind = str(raw[0])
                hp.x, hp.y = raw[1]
                hp.footprint = _footprint(raw[2])
                hp.mount_id = raw[3] or ""
                hp.rotation = str(raw[4])
                hp.armor = raw[5]
                hp.firing_arc = raw[6]
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
            hardpoints = [[int(hp.kind), [hp.x, hp.y], _parse_footprint(hp.footprint),
                           hp.mount_id, int(hp.rotation), hp.armor, hp.firing_arc]
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
            _, _, body, _ = read(path, _brokkr_cultlib(context))
            if collection.get("aetheria.asset_kind") not in (None, "ship"):
                raise ValueError(f"{collection.name} has another aetheria.asset_kind")
            if collection.get("aetheria.id") not in (None, body[0]):
                raise ValueError(f"{collection.name} is bound to another ship ID")
            collection["aetheria.asset_kind"] = "ship"
            collection["aetheria.id"] = body[0]
            collection["aetheria.ship_cc"] = bpy.path.relpath(path)
            self.report({"INFO"}, f"Bound {collection.name} to {body[0]}")
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
            _, _, body, _ = read(path, cultlib)
            if body[0] != collection["aetheria.id"]:
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


classes = (AETHERIA_PG_cell, AETHERIA_PG_hardpoint, AETHERIA_PG_layout,
           AETHERIA_OT_load_layout, AETHERIA_OT_resize_layout,
           AETHERIA_OT_add_hardpoint, AETHERIA_OT_remove_hardpoint,
           AETHERIA_OT_save_layout, AETHERIA_OT_bind_ship_collection,
           AETHERIA_OT_capture_ship_lines, AETHERIA_PT_ship)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.aetheria_layout = bpy.props.PointerProperty(type=AETHERIA_PG_layout)
    bpy.types.Scene.aetheria_ship_cc_path = bpy.props.StringProperty(
        name="Ship .cc", subtype="FILE_PATH", description="Existing typed ship authoring record"
    )
    bpy.types.Scene.aetheria_capture_evaluated_lines = bpy.props.BoolProperty(
        name="Evaluated LineArt", default=True,
        description="Capture the visible modifier result at the current frame",
    )


def unregister():
    del bpy.types.Scene.aetheria_layout
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.aetheria_capture_evaluated_lines
    del bpy.types.Scene.aetheria_ship_cc_path
