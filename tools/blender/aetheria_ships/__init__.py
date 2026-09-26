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

from .ship_cc import capture_grease_pencil, read, replace_lines


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


classes = (AETHERIA_OT_bind_ship_collection, AETHERIA_OT_capture_ship_lines, AETHERIA_PT_ship)


def register():
    bpy.types.Scene.aetheria_ship_cc_path = bpy.props.StringProperty(
        name="Ship .cc", subtype="FILE_PATH", description="Existing typed ship authoring record"
    )
    bpy.types.Scene.aetheria_capture_evaluated_lines = bpy.props.BoolProperty(
        name="Evaluated LineArt", default=True,
        description="Capture the visible modifier result at the current frame",
    )
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.aetheria_capture_evaluated_lines
    del bpy.types.Scene.aetheria_ship_cc_path
