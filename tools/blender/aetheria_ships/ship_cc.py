"""Aetheria's Blender-side view of one typed ShipAuthoring CultCache record.

The schema and authority live in Aetheria's C# ShipAuthoring type. This module
preserves the C# schema catalog and all slots it does not edit. Brokkr supplies
the Blender host and CultLib Python dependency path.
"""

from __future__ import annotations

import sys
from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCHEMA = "aetheria.ship_authoring"
SCHEMATIC_LINES_SLOT = 4


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
