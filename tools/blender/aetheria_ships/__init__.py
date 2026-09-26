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

from .ship_cc import capture_grease_pencil, replace_lines


def _brokkr_cultlib(context):
    brokkr = context.preferences.addons.get("brokkr_bridge")
    if brokkr is None:
        raise RuntimeError("Enable Brokkr before using Aetheria Ships")
    return brokkr.preferences.cultlib_py_src


class AETHERIA_OT_capture_ship_lines(bpy.types.Operator):
    bl_idname = "aetheria.capture_ship_lines"
    bl_label = "Capture Ship Lines"
    bl_description = "Replace only the line slot in the selected typed ship .cc record"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            obj = context.active_object
            if obj is None:
                raise ValueError("Select the ship's Grease Pencil LineArt object")
            if not context.scene.aetheria_ship_cc_path:
                raise ValueError("Choose an existing ship authoring .cc file")
            path = bpy.path.abspath(context.scene.aetheria_ship_cc_path)
            cultlib = _brokkr_cultlib(context)
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
        layout.prop(context.scene, "aetheria_capture_evaluated_lines")
        obj = context.active_object
        layout.label(text=f"Source: {obj.name if obj else '(none)'}")
        layout.operator("aetheria.capture_ship_lines", icon="GREASEPENCIL")


classes = (AETHERIA_OT_capture_ship_lines, AETHERIA_PT_ship)


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
