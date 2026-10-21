"""Aetheria's Blender-side view of one ship file: a typed HullData record and the ShipAuthoring visual it names.

The schemas and authority live in Aetheria's C# HullData and ShipAuthoring types. This module edits the hull's grid
and hardpoints and the visual's lines, and preserves the C# schema catalog and every slot it does not edit. The file
also holds the hull's products (FactionProductData records), which C# owns: every push here leaves them untouched. Brokkr
supplies the Blender host and CultLib Python dependency path.
"""

from __future__ import annotations

import sys
import hashlib
import tomllib
from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, NamedTuple

# The cultcache-py this module's raw envelope writes were tested against. cultcache_py has no __version__ and is loaded
# from source, so the guard reads [project].version of the checkout the imported module itself came from.
TESTED_CULTCACHE_PY = "0.3.0"

SCHEMA = "aetheria.ship_authoring"
HULL_SCHEMA = "aetheria.hulldata"
PRODUCT_SCHEMA = "aetheria.factionproductdata"
PRODUCT_MANUFACTURER_SLOT = 4  # FactionProductData.Manufacturer

# MessagePack slots of the C# types, which own them (ShipAuthoring, ItemData.Shape, HullData.Hardpoints,
# HardpointData). RETIRED_HULL_SLOT is the ShipAuthoring key that held the embedded hull before S1; no member owns it now. ShipSchemaPinTests in tests/Aetheria.Shared.Tests fails when any number or name below
# disagrees with those types' [Key] attributes, so a renumbered or added member cannot drift silently.
RETIRED_HULL_SLOT = 1
MODEL_ASSET_SLOT = 2  # ShipAuthoring.ModelAsset
ANCHORS_SLOT = 3  # ShipAuthoring.Anchors
SCHEMATIC_LINES_SLOT = 4  # ShipAuthoring.SchematicLines
HULL_SHAPE_SLOT = 5  # ItemData.Shape
HULL_HARDPOINTS_SLOT = 23  # HullData.Hardpoints
HULL_TYPE_SLOT = 25  # HullData.HullType
# HardpointData member names in slot order; a hardpoint row is one value per name, and any slot past the last
# name belongs to a later schema and is carried through untouched.
HARDPOINT_MEMBERS = ("Type", "Position", "Shape", "Transform", "Rotation", "Armor", "FiringArc")
# ShipAnchor member names in slot order; as for hardpoints, slots past the last name are carried through by anchor Id.
ANCHOR_MEMBERS = ("Id", "Role", "ModelNodeId", "ParentId", "Order")
# Enum member names in value order (HardpointType, ItemRotation, HullType).
HARDPOINT_TYPE_NAMES = ("Hull", "Tool", "Thermal", "Thruster", "WarpDrive", "Reactor", "Radiator",
                        "Shield", "Sensors", "Energy", "Ballistic", "Launcher", "ControlModule")
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


def decode_hardpoint(row: list[Any]) -> dict[str, Any]:
    return dict(zip(HARDPOINT_MEMBERS, row))


def encode_hardpoint(**fields: Any) -> list[Any]:
    if set(fields) != set(HARDPOINT_MEMBERS):
        raise ValueError(f"A hardpoint row needs exactly {', '.join(HARDPOINT_MEMBERS)}")
    return [fields[name] for name in HARDPOINT_MEMBERS]


def _cultcache_py_version(module: Any) -> str:
    """[project].version of the cultcache-py checkout the imported module lives in (src/cultcache_py/__init__.py sits
    two levels under it). A module with no file or no readable pyproject has no version: ""."""
    try:
        with (Path(module.__file__).parents[2] / "pyproject.toml").open("rb") as handle:
            return str(tomllib.load(handle)["project"]["version"])
    except (AttributeError, TypeError, IndexError, OSError, tomllib.TOMLDecodeError, KeyError):
        return ""


def _libraries(cultlib_packages: str):
    source = Path(cultlib_packages) / "cultcache-py" / "src"
    if source.exists() and str(source) not in sys.path:
        sys.path.insert(0, str(source))
    import cultcache_py  # type: ignore
    import msgpack  # type: ignore
    found = _cultcache_py_version(cultcache_py)
    if found != TESTED_CULTCACHE_PY:
        raise ValueError(f"the imported cultcache-py (version {found or 'unreadable'}; CultLib packages {cultlib_packages}) "
                         f"is not the tested {TESTED_CULTCACHE_PY}; the add-on writes raw envelopes and refuses a "
                         f"CultLib whose write path may have moved")
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
                f"The ship file has a legacy embedded hull at retired key {RETIRED_HULL_SLOT} of the {SCHEMA} record; "
                f"migrate it into its own {HULL_SCHEMA} record that names the visual, and drop key {RETIRED_HULL_SLOT}")
    products = [envelope for envelope in envelopes if envelope.type == PRODUCT_SCHEMA]
    if len(ships) != 1 or len(hulls) != 1 or len(ships) + len(hulls) + len(products) != len(envelopes):
        raise ValueError(f"The ship file must hold exactly one {SCHEMA} record and one {HULL_SCHEMA} record, "
                         f"and may hold {PRODUCT_SCHEMA} records beside them, nothing else")
    bodies = []
    for envelope in (ships[0], hulls[0]):
        body = msgpack.unpackb(envelope.payload, raw=False)
        if not isinstance(body, list):
            raise ValueError(f"The ship file has an incompatible {envelope.type} payload")
        bodies.append(Record(envelope, body))
    ship, hull = bodies
    if len(ship.body) < SCHEMATIC_LINES_SLOT + 1:
        raise ValueError(f"The ship file has an incompatible {SCHEMA} payload")
    return ShipFile(store, ship, hull, msgpack)


def product_makers(path: str, cultlib_packages: str) -> list[str]:
    """The Manufacturer (Faction record key) of every product record in the file, in file order. C# owns products and
    this only reads them: a product whose Manufacturer is missing or not a non-empty string is refused, naming nothing."""
    cultcache, msgpack = _libraries(cultlib_packages)
    makers = []
    for envelope in cultcache.SingleFileMessagePackBackingStore(path).pull_all():
        if envelope.type != PRODUCT_SCHEMA:
            continue
        body = msgpack.unpackb(envelope.payload, raw=False)
        maker = body[PRODUCT_MANUFACTURER_SLOT] if isinstance(body, list) and len(body) > PRODUCT_MANUFACTURER_SLOT else None
        if not isinstance(maker, str) or not maker:
            raise ValueError(f"A {PRODUCT_SCHEMA} record names no manufacturer")
        makers.append(maker)
    return makers


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


def replace_visual(path: str, cultlib_packages: str, expected_id: str, model_asset: str,
                   anchors: list[list[Any]]) -> int:
    """Writes the visual's model asset and anchor list (ShipAuthoring slots 2 and 3) and nothing else. Each anchor row is
    ANCHOR_MEMBERS in order; the slots past Order that the same anchor Id carried before are appended to it unchanged."""
    file = read(path, cultlib_packages)
    if file.ship.body[0] != expected_id:
        raise ValueError("The bound ship ID changed; rebind the collection")
    known = len(ANCHOR_MEMBERS)
    ids = set()
    for anchor in anchors:
        if len(anchor) != known:
            raise ValueError(f"Each anchor row needs exactly {', '.join(ANCHOR_MEMBERS)}")
        anchor_id = anchor[0]
        if not isinstance(anchor_id, str) or not anchor_id.strip():
            raise ValueError("Every anchor needs an ID")
        if anchor_id in ids:
            raise ValueError("An anchor ID is used twice")
        ids.add(anchor_id)
    previous = file.ship.body[ANCHORS_SLOT] if len(file.ship.body) > ANCHORS_SLOT else None
    later = {row[0]: row[known:] for row in previous or []
             if isinstance(row, list) and len(row) > known and isinstance(row[0], str)}
    file.ship.body[MODEL_ASSET_SLOT] = model_asset
    file.ship.body[ANCHORS_SLOT] = [list(anchor) + later.get(anchor[0], []) for anchor in anchors]
    file.store.push(_stamped(file.ship, file.msgpack))
    return len(anchors)


def _hull_body(file: ShipFile) -> list:
    if len(file.hull.body) <= HULL_HARDPOINTS_SLOT:
        raise ValueError("Ship hull has an incompatible typed payload")
    return file.hull.body


def read_layout(path: str, cultlib_packages: str):
    file = read(path, cultlib_packages)
    hull = _hull_body(file)
    return hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT], layout_revision(hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT])


def read_hull_type(path: str, cultlib_packages: str) -> str:
    """The hull's HullData.HullType as its enum member name ("Ship", "Station" or "Turret")."""
    hull = _hull_body(read(path, cultlib_packages))
    value = hull[HULL_TYPE_SLOT] if len(hull) > HULL_TYPE_SLOT else None
    if type(value) is not int or not 0 <= value < len(HULL_TYPE_NAMES):
        raise ValueError("The ship file's hull has no hull type this add-on knows")
    return HULL_TYPE_NAMES[value]


def layout_revision(shape: list[Any], hardpoints: list[list[Any]]) -> str:
    """The revision of a layout: a hash of its shape ([width, height, cells]) and hardpoint rows as the file stores
    them. replace_layout returns it, Load records it, and Package compares the layout buffer's against the file's."""
    import msgpack  # type: ignore
    return hashlib.sha256(msgpack.packb([[shape], hardpoints], use_bin_type=True)).hexdigest()


def replace_layout(path: str, cultlib_packages: str, expected_id: str, expected_revision: str,
                   shape: list[Any], hardpoints: list[list[Any]]) -> str:
    file = read(path, cultlib_packages)
    if file.ship.body[0] != expected_id:
        raise ValueError("The bound ship ID changed; reload its layout")
    hull = _hull_body(file)
    if layout_revision(hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT]) != expected_revision:
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
            raise ValueError("Hardpoint footprint cells must be booleans")
        if not (1 <= hp_width <= 32 and 1 <= hp_height <= 32 and
                len(hp_cells) == hp_width * hp_height and any(hp_cells)):
            raise ValueError("A hardpoint has an invalid footprint")
        updated_hardpoints.append(hardpoint + previous.get(mount, []))
    hull[HULL_SHAPE_SLOT] = [shape]
    hull[HULL_HARDPOINTS_SLOT] = updated_hardpoints
    file.store.push(_stamped(file.hull, file.msgpack))
    return layout_revision(hull[HULL_SHAPE_SLOT][0], hull[HULL_HARDPOINTS_SLOT])


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
