"""Aetheria's Blender-side view of one typed ShipAuthoring CultCache record.

The schema and authority live in Aetheria's C# ShipAuthoring type. This module
preserves the C# schema catalog and all slots it does not edit. Brokkr supplies
the Blender host and CultLib Python dependency path.
"""

from __future__ import annotations

import sys
import hashlib
from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCHEMA = "aetheria.ship_authoring"

# MessagePack slots of the C# types, which own them (ShipAuthoring, ItemData.Shape, HullData.Hardpoints,
# HardpointData). ShipSchemaPinTests in tests/Aetheria.Shared.Tests fails when any number or name below
# disagrees with those types' [Key] attributes, so a renumbered or added member cannot drift silently.
SCHEMATIC_LINES_SLOT = 4  # ShipAuthoring.SchematicLines
HULL_SLOT = 1  # ShipAuthoring.Hull
HULL_SHAPE_SLOT = 5  # ItemData.Shape
HULL_HARDPOINTS_SLOT = 23  # HullData.Hardpoints
# HardpointData member names in slot order; a hardpoint row is one value per name, and any slot past the last
# name belongs to a later schema and is carried through untouched.
HARDPOINT_MEMBERS = ("Type", "Position", "Shape", "Transform", "Rotation", "Armor", "FiringArc")
# Enum member names in value order (HardpointType, ItemRotation).
HARDPOINT_TYPE_NAMES = ("Hull", "Tool", "Thermal", "Thruster", "WarpDrive", "Reactor", "Radiator",
                        "Shield", "Sensors", "Energy", "Ballistic", "Launcher", "ControlModule", "AetherDrive")
ROTATION_NAMES = ("None", "CounterClockwise", "Reversed", "Clockwise")


def decode_hardpoint(row: list[Any]) -> dict[str, Any]:
    return dict(zip(HARDPOINT_MEMBERS, row))


def encode_hardpoint(**fields: Any) -> list[Any]:
    if set(fields) != set(HARDPOINT_MEMBERS):
        raise ValueError(f"A hardpoint row needs exactly {', '.join(HARDPOINT_MEMBERS)}")
    return [fields[name] for name in HARDPOINT_MEMBERS]


def _libraries(cultlib_packages: str):
    source = Path(cultlib_packages) / "cultcache-py" / "src"
    if source.exists() and str(source) not in sys.path:
        sys.path.insert(0, str(source))
    import cultcache_py  # type: ignore
    import msgpack  # type: ignore
    return cultcache_py, msgpack


def read(path: str, cultlib_packages: str):
    cultcache, msgpack = _libraries(cultlib_packages)
    store = cultcache.SingleFileMessagePackBackingStore(path)
    envelopes = store.pull_all()
    ships = [envelope for envelope in envelopes if envelope.type == SCHEMA]
    if len(envelopes) != 1 or len(ships) != 1:
        raise ValueError(f"{path} must hold exactly one {SCHEMA} record")
    envelope = ships[0]
    body = msgpack.unpackb(envelope.payload, raw=False)
    if not isinstance(body, list) or len(body) < SCHEMATIC_LINES_SLOT + 1:
        raise ValueError(f"{path} has an incompatible {SCHEMA} payload")
    return store, envelope, body, msgpack


def replace_lines(path: str, cultlib_packages: str, lines: list[list[Any]]) -> int:
    store, envelope, body, msgpack = read(path, cultlib_packages)
    if not lines:
        raise ValueError("No Grease Pencil strokes were captured; the ship record was not changed")
    body[SCHEMATIC_LINES_SLOT] = lines
    updated = replace(
        envelope,
        payload=msgpack.packb(body, use_bin_type=True),
        stored_at=datetime.now(timezone.utc).isoformat(),
    )
    store.push(updated)
    return len(lines)


def read_layout(path: str, cultlib_packages: str):
    _, _, body, msgpack = read(path, cultlib_packages)
    hull = body[HULL_SLOT]
    if not isinstance(hull, list) or len(hull) <= HULL_HARDPOINTS_SLOT:
        raise ValueError("Ship hull has an incompatible typed payload")
    return hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT], _layout_revision(hull, msgpack)


def _layout_revision(hull, msgpack):
    return hashlib.sha256(msgpack.packb(
        [hull[HULL_SHAPE_SLOT], hull[HULL_HARDPOINTS_SLOT]], use_bin_type=True
    )).hexdigest()


def replace_layout(path: str, cultlib_packages: str, expected_id: str, expected_revision: str,
                   shape: list[Any], hardpoints: list[list[Any]]) -> str:
    store, envelope, body, msgpack = read(path, cultlib_packages)
    if body[0] != expected_id:
        raise ValueError("The bound ship ID changed; reload its layout")
    hull = body[HULL_SLOT]
    if _layout_revision(hull, msgpack) != expected_revision:
        raise ValueError("The .cc layout changed since Load; reload before saving")
    width, height, cells = shape
    if not (1 <= width <= 32 and 1 <= height <= 32 and len(cells) == width * height):
        raise ValueError("Hull grid must be 1..32 cells wide and high with one value per cell")
    if not any(cells):
        raise ValueError("Hull grid needs an occupied cell")
    known = len(HARDPOINT_MEMBERS)
    previous = {decode_hardpoint(hp)["Transform"]: hp[known:] for hp in hull[HULL_HARDPOINTS_SLOT]
                if isinstance(hp, list) and len(hp) > known and isinstance(decode_hardpoint(hp)["Transform"], str)}
    updated_hardpoints = []
    for hardpoint in hardpoints:
        fields = decode_hardpoint(hardpoint)
        mount = fields.get("Transform")
        if len(hardpoint) != known or not isinstance(mount, str) or not mount:
            raise ValueError(f"Each hardpoint needs {known} typed fields and a stable mount ID")
        hp_width, hp_height, hp_cells = fields["Shape"][0]
        if not (1 <= hp_width <= 32 and 1 <= hp_height <= 32 and
                len(hp_cells) == hp_width * hp_height and any(hp_cells)):
            raise ValueError(f"Hardpoint {mount} has an invalid footprint")
        updated_hardpoints.append(hardpoint + previous.get(mount, []))
    hull[HULL_SHAPE_SLOT] = [shape]
    hull[HULL_HARDPOINTS_SLOT] = updated_hardpoints
    store.push(replace(
        envelope,
        payload=msgpack.packb(body, use_bin_type=True),
        stored_at=datetime.now(timezone.utc).isoformat(),
    ))
    return _layout_revision(hull, msgpack)


def capture_grease_pencil(obj: Any, depsgraph: Any, frame_number: int, *, evaluated: bool = True) -> list[list[Any]]:
    if obj.type != "GREASEPENCIL":
        raise ValueError(f"{obj.name} is not a Grease Pencil object")
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
