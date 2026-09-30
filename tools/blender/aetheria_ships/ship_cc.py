"""Aetheria's Blender-side view of one ship file: a typed HullData record and the ShipAuthoring visual it names.

The schemas and authority live in Aetheria's C# HullData and ShipAuthoring types. This module edits the hull's grid
and hardpoints and the visual's lines, and preserves the C# schema catalog and every slot it does not edit. Brokkr
supplies the Blender host and CultLib Python dependency path.
"""

from __future__ import annotations

import sys
import hashlib
from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, NamedTuple

SCHEMA = "aetheria.ship_authoring"
HULL_SCHEMA = "aetheria.hulldata"

# MessagePack slots of the C# types, which own them (ShipAuthoring, ItemData.Shape, HullData.Hardpoints,
# HardpointData). RETIRED_HULL_SLOT is the ShipAuthoring key that held the embedded hull before S1; no member owns it now. ShipSchemaPinTests in tests/Aetheria.Shared.Tests fails when any number or name below
# disagrees with those types' [Key] attributes, so a renumbered or added member cannot drift silently.
RETIRED_HULL_SLOT = 1
SCHEMATIC_LINES_SLOT = 4  # ShipAuthoring.SchematicLines
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


class Record(NamedTuple):
    envelope: Any
    body: list


class ShipFile(NamedTuple):
    store: Any
    ship: Record  # the ShipAuthoring visual
    hull: Record  # the HullData it belongs to
    msgpack: Any


def read(path: str, cultlib_packages: str) -> ShipFile:
    cultcache, msgpack = _libraries(cultlib_packages)
    store = cultcache.SingleFileMessagePackBackingStore(path)
    envelopes = store.pull_all()
    ships = [envelope for envelope in envelopes if envelope.type == SCHEMA]
    hulls = [envelope for envelope in envelopes if envelope.type == HULL_SCHEMA]
    # Before S1 the hull lived inside the ship record. Saving such a file would preserve the stale hull under a ship that
    # no longer reads it, so it is refused before anything else is said about its shape.
    for envelope in ships:
        body = msgpack.unpackb(envelope.payload, raw=False)
        if isinstance(body, list) and len(body) > RETIRED_HULL_SLOT and body[RETIRED_HULL_SLOT] is not None:
            raise ValueError(
                f"{path}: {envelope.key}: legacy embedded hull at retired key {RETIRED_HULL_SLOT} of the {SCHEMA} record; "
                f"migrate it into its own {HULL_SCHEMA} record that names the visual, and drop key {RETIRED_HULL_SLOT}")
    if len(envelopes) != 2 or len(ships) != 1 or len(hulls) != 1:
        raise ValueError(f"{path} must hold exactly one {SCHEMA} record and one {HULL_SCHEMA} record")
    bodies = []
    for envelope in (ships[0], hulls[0]):
        body = msgpack.unpackb(envelope.payload, raw=False)
        if not isinstance(body, list):
            raise ValueError(f"{path} has an incompatible {envelope.type} payload")
        bodies.append(Record(envelope, body))
    ship, hull = bodies
    if len(ship.body) < SCHEMATIC_LINES_SLOT + 1:
        raise ValueError(f"{path} has an incompatible {SCHEMA} payload")
    return ShipFile(store, ship, hull, msgpack)


def _stamped(record: Record, msgpack: Any):
    return replace(
        record.envelope,
        payload=msgpack.packb(record.body, use_bin_type=True),
        stored_at=datetime.now(timezone.utc).isoformat(),
    )


def replace_lines(path: str, cultlib_packages: str, lines: list[list[Any]]) -> int:
    file = read(path, cultlib_packages)
    if not lines:
        raise ValueError("No Grease Pencil strokes were captured; the ship record was not changed")
    file.ship.body[SCHEMATIC_LINES_SLOT] = lines
    file.store.push(_stamped(file.ship, file.msgpack))
    return len(lines)


def _hull_body(file: ShipFile) -> list:
    if len(file.hull.body) <= HULL_HARDPOINTS_SLOT:
        raise ValueError("Ship hull has an incompatible typed payload")
    return file.hull.body


def read_layout(path: str, cultlib_packages: str):
    file = read(path, cultlib_packages)
    hull = _hull_body(file)
    return hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT], _layout_revision(hull, file.msgpack)


def _layout_revision(hull, msgpack):
    return hashlib.sha256(msgpack.packb(
        [hull[HULL_SHAPE_SLOT], hull[HULL_HARDPOINTS_SLOT]], use_bin_type=True
    )).hexdigest()


def replace_layout(path: str, cultlib_packages: str, expected_id: str, expected_revision: str,
                   shape: list[Any], hardpoints: list[list[Any]]) -> str:
    file = read(path, cultlib_packages)
    if file.ship.body[0] != expected_id:
        raise ValueError("The bound ship ID changed; reload its layout")
    hull = _hull_body(file)
    if _layout_revision(hull, file.msgpack) != expected_revision:
        raise ValueError("The .cc layout changed since Load; reload before saving")
    width, height, cells = shape
    if not (1 <= width <= 32 and 1 <= height <= 32 and len(cells) == width * height):
        raise ValueError("Hull grid must be 1..32 cells wide and high with one value per cell")
    if not all(type(cell) is bool for cell in cells):
        raise ValueError("Hull grid cells must be booleans")
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
        if not all(type(cell) is bool for cell in hp_cells):
            raise ValueError(f"Hardpoint {mount} footprint cells must be booleans")
        if not (1 <= hp_width <= 32 and 1 <= hp_height <= 32 and
                len(hp_cells) == hp_width * hp_height and any(hp_cells)):
            raise ValueError(f"Hardpoint {mount} has an invalid footprint")
        updated_hardpoints.append(hardpoint + previous.get(mount, []))
    hull[HULL_SHAPE_SLOT] = [shape]
    hull[HULL_HARDPOINTS_SLOT] = updated_hardpoints
    file.store.push(_stamped(file.hull, file.msgpack))
    return _layout_revision(hull, file.msgpack)


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
