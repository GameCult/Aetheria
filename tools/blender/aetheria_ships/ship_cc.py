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
SCHEMATIC_LINES_SLOT = 4
HULL_SLOT = 1
HULL_SHAPE_SLOT = 5
HULL_HARDPOINTS_SLOT = 23


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
    previous = {hp[3]: hp[7:] for hp in body[HULL_SLOT][HULL_HARDPOINTS_SLOT]
                if isinstance(hp, list) and len(hp) > 7 and isinstance(hp[3], str)}
    updated_hardpoints = []
    for hardpoint in hardpoints:
        if len(hardpoint) != 7 or not isinstance(hardpoint[3], str) or not hardpoint[3]:
            raise ValueError("Each hardpoint needs seven typed fields and a stable mount ID")
        hp_width, hp_height, hp_cells = hardpoint[2][0]
        if not (1 <= hp_width <= 32 and 1 <= hp_height <= 32 and
                len(hp_cells) == hp_width * hp_height and any(hp_cells)):
            raise ValueError(f"Hardpoint {hardpoint[3]} has an invalid footprint")
        updated_hardpoints.append(hardpoint + previous.get(hardpoint[3], []))
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
