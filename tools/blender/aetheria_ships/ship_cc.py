"""Aetheria ship vocabulary the Blender add-on shares with its tools: the ship id rule, the enum member names, and Grease
Pencil capture. The ship.cc record and its schemas belong to AetherDb ('ship-authoring open' and 'push'); nothing here
reads or writes the file. ShipSchemaPinTests in tests/Aetheria.Shared.Tests pins the ship id rule and the enum names to
the C# types.
"""

from __future__ import annotations

from typing import Any

# Enum member names in value order (HardpointType, ItemRotation, HullType).
HARDPOINT_TYPE_NAMES = ("Hull", "Tool", "Thermal", "Thruster", "WarpDrive", "Reactor", "Radiator",
                        "Shield", "Sensors", "Energy", "Ballistic", "Launcher", "ControlModule", "Facility")
ROTATION_NAMES = ("None", "CounterClockwise", "Reversed", "Clockwise")
HULL_TYPE_NAMES = ("Ship", "Station", "Turret")


# ShipAuthoringStore.RequireShipId's rule, mirrored so New Ship refuses before it touches anything; ShipSchemaPinTests
# pins both constants and valid_ship_id to the C# rule.
SHIP_ID_CHARS = "abcdefghijklmnopqrstuvwxyz0123456789._-"
RESERVED_SHIP_IDS = ("con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
                     "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9")


def valid_ship_id(ship_id: str) -> bool:
    """True when ShipAuthoringStore.RequireShipId would accept the ID: lower-case ASCII letters, digits, dots,
    underscores and hyphens, starting with a letter or digit, not ending in a dot, and whose stem before the first dot
    is not a Windows device name."""
    return (bool(ship_id) and ship_id[0] not in "._-" and all(c in SHIP_ID_CHARS for c in ship_id)
            and not ship_id.endswith(".") and ship_id.split(".")[0] not in RESERVED_SHIP_IDS)


def capture_grease_pencil(obj: Any, depsgraph: Any, frame_number: int, *, evaluated: bool = True) -> list[list[Any]]:
    if obj.type != "GREASEPENCIL":
        raise ValueError("The selected object is not a Grease Pencil object")
    source = obj.evaluated_get(depsgraph) if evaluated else obj
    lines: list[list[Any]] = []
    for layer in source.data.layers:
        frames = [frame for frame in layer.frames if frame.frame_number <= frame_number]
        if not frames:
            continue
        frame = max(frames, key=lambda candidate: candidate.frame_number)
        for stroke in frame.drawing.strokes:
            if len(stroke.points) < 2:
                continue
            material = source.data.materials[stroke.material_index]
            rgba = (
                tuple(material.grease_pencil.color)
                if material and material.grease_pencil
                else tuple(material.diffuse_color) if material else (1.0, 1.0, 1.0, 1.0)
            )
            points: list[float] = []
            radii: list[float] = []
            opacities: list[float] = []
            for point in stroke.points:
                position = obj.matrix_world @ point.position
                points.extend(float(value) for value in position)
                radii.append(float(point.radius))
                opacities.append(float(point.opacity))
            lines.append([
                layer.name,
                material.name if material else "",
                points,
                radii,
                opacities,
                bool(stroke.cyclic),
                [float(value) for value in rgba],
            ])
    return lines
