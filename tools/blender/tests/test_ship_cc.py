"""Blender-free tests for aetheria_ships/ship_cc.py, the Python view of a typed ShipAuthoring record.

Run with CultLib's cultcache-py and msgpack importable:

    CULTLIB_PACKAGES=<CultLib>/packages python -m unittest discover -s tools/blender/tests

ship_cc is loaded by path because the package __init__ imports bpy. The record layout the tests build comes from
ship_cc's own slot constants; ShipSchemaPinTests (C#) pins those constants to the C# [Key] attributes.
"""

import importlib.util
import os
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

PACKAGES = os.environ.get("CULTLIB_PACKAGES")
if not PACKAGES:
    raise RuntimeError("Set CULTLIB_PACKAGES to CultLib's packages directory")

_spec = importlib.util.spec_from_file_location(
    "ship_cc", Path(__file__).resolve().parents[1] / "aetheria_ships" / "ship_cc.py")
ship_cc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ship_cc)

sys.path.insert(0, str(Path(PACKAGES) / "cultcache-py" / "src"))
import cultcache_py  # noqa: E402
import msgpack  # noqa: E402


def shape(width, height, cells):
    return [width, height, cells]


def hardpoint_row(mount="thruster.port", tail=("future-hardpoint-slot",)):
    row = ship_cc.encode_hardpoint(
        Type=3, Position=[1, 0], Shape=[shape(1, 1, [True])], Transform=mount, Rotation=0, Armor=0.0, FiringArc=0.0)
    return row + list(tail)


def hull_slots():
    hull = [None] * (ship_cc.HULL_HARDPOINTS_SLOT + 1)
    hull[1] = "Skiff"
    hull[ship_cc.HULL_SHAPE_SLOT] = [shape(2, 2, [True, False, True, False])]
    hull[ship_cc.HULL_HARDPOINTS_SLOT] = [hardpoint_row()]
    hull.append("future-hull-slot")
    return hull


LINES = [["Hull", "White", [0.0, 0.0, 0.0, 1.0, 0.0, 0.0], [0.01, 0.01], [1.0, 1.0], False, [1.0, 1.0, 1.0, 1.0]]]


def ship_body():
    body = [None] * (ship_cc.SCHEMATIC_LINES_SLOT + 1)
    body[0] = "mod.skiff"
    body[ship_cc.HULL_SLOT] = hull_slots()
    body[2] = "skiff.glb"
    body[3] = [["map", "map-icon", "map", None, 0]]
    body[ship_cc.SCHEMATIC_LINES_SLOT] = LINES
    return body


CATALOG = cultcache_py.CultCacheSchemaCatalogEntry(
    schema_id="ship-authoring-schema-1", schema_name=ship_cc.SCHEMA, schema_version="1",
    content_hash="hash", canonical_schema_json='{"members":[{"slot":9,"name":"FutureMember"}]}',
    compatible_schema_ids=("ship-authoring-schema-1",),
    members=(cultcache_py.CultCacheSchemaCatalogMember(slot=9, member_name="FutureMember", type_name="string"),))


class ShipFileCase(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.path = str(Path(directory.name) / "ship.cc")
        self.write(ship_body())

    def write(self, body, path=None):
        cultcache_py.SingleFileMessagePackBackingStore(path or self.path).push(cultcache_py.CultCacheEnvelope.create(
            key="mod.skiff", type=ship_cc.SCHEMA, payload=msgpack.packb(body, use_bin_type=True),
            schema_id=CATALOG.schema_id, catalog_entry=CATALOG))

    def body(self):
        return ship_cc.read(self.path, PACKAGES)[2]

    def bytes(self):
        return Path(self.path).read_bytes()


class ReadTests(ShipFileCase):
    def test_reads_the_one_ship_record(self):
        store, envelope, body, _ = ship_cc.read(self.path, PACKAGES)
        self.assertEqual("mod.skiff", body[0])
        self.assertEqual(ship_cc.SCHEMA, envelope.type)

    def test_refuses_a_file_with_another_record_beside_the_ship(self):
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push(cultcache_py.CultCacheEnvelope.create(
            key="other", type="aetheria.something", payload=msgpack.packb([1], use_bin_type=True)))
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_file_whose_only_record_is_not_a_ship(self):
        other = str(Path(self.path).with_name("other.cc"))
        cultcache_py.SingleFileMessagePackBackingStore(other).push(cultcache_py.CultCacheEnvelope.create(
            key="other", type="aetheria.something", payload=msgpack.packb([1], use_bin_type=True)))
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(other, PACKAGES)

    def test_refuses_a_file_with_no_record(self):
        empty = str(Path(self.path).with_name("empty.cc"))
        Path(empty).write_bytes(b"")
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(empty, PACKAGES)

    def test_refuses_a_payload_too_short_for_the_line_slot(self):
        self.write(["mod.skiff", None])
        with self.assertRaisesRegex(ValueError, "incompatible"):
            ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_payload_that_is_not_an_array(self):
        for payload in ({"Id": "mod.skiff"}, {str(index): index for index in range(9)}, "a string long enough to fill every slot"):
            with self.subTest(payload=payload):
                self.write(payload)
                with self.assertRaisesRegex(ValueError, "incompatible"):
                    ship_cc.read(self.path, PACKAGES)


class ReplaceLinesTests(ShipFileCase):
    def test_replaces_only_the_line_slot(self):
        new_lines = [["Detail", "Red", [0.0] * 6, [0.1, 0.1], [1.0, 1.0], True, [1.0, 0.0, 0.0, 1.0]]]
        before = self.body()
        self.assertEqual(1, ship_cc.replace_lines(self.path, PACKAGES, new_lines))
        after = self.body()
        self.assertEqual(new_lines, after[ship_cc.SCHEMATIC_LINES_SLOT])
        for slot in range(len(before)):
            if slot != ship_cc.SCHEMATIC_LINES_SLOT:
                self.assertEqual(before[slot], after[slot], f"slot {slot} changed")

    def test_keeps_the_schema_catalog(self):
        ship_cc.replace_lines(self.path, PACKAGES, LINES)
        envelope = cultcache_py.SingleFileMessagePackBackingStore(self.path).pull_all()[0]
        self.assertEqual(CATALOG.members, envelope.catalog_entry.members)
        self.assertEqual(CATALOG.canonical_schema_json, envelope.catalog_entry.canonical_schema_json)

    def test_refuses_an_empty_capture_and_changes_nothing(self):
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "No Grease Pencil strokes"):
            ship_cc.replace_lines(self.path, PACKAGES, [])
        self.assertEqual(before, self.bytes())


class LayoutTests(ShipFileCase):
    def layout(self):
        return ship_cc.read_layout(self.path, PACKAGES)

    def edit(self, revision, width=2, height=2, cells=None, hardpoints=None, ship_id="mod.skiff"):
        return ship_cc.replace_layout(
            self.path, PACKAGES, ship_id, revision,
            [width, height, cells if cells is not None else [True, True, True, False]],
            hardpoints if hardpoints is not None else [hardpoint_row(tail=())])

    def test_reads_the_shape_hardpoints_and_a_revision(self):
        shape_triple, hardpoints, revision = self.layout()
        self.assertEqual([2, 2, [True, False, True, False]], shape_triple)
        self.assertEqual("thruster.port", ship_cc.decode_hardpoint(hardpoints[0])["Transform"])
        self.assertEqual(64, len(revision))

    def test_the_revision_follows_the_layout_and_only_the_layout(self):
        _, _, revision = self.layout()
        body = self.body()
        body[ship_cc.SCHEMATIC_LINES_SLOT] = []
        body[ship_cc.HULL_SLOT][1] = "Renamed"
        self.write(body)
        self.assertEqual(revision, self.layout()[2])
        body[ship_cc.HULL_SLOT][ship_cc.HULL_SHAPE_SLOT] = [shape(2, 2, [True, True, True, True])]
        self.write(body)
        self.assertNotEqual(revision, self.layout()[2])

    def test_the_revision_follows_the_hardpoints_too(self):
        _, _, revision = self.layout()
        moved = hardpoint_row(tail=())
        moved[1] = [0, 0]
        same_cells = [True, False, True, False]
        self.assertEqual(revision, self.edit(revision, cells=same_cells, hardpoints=[hardpoint_row(tail=())]))
        self.assertNotEqual(revision, self.edit(revision, cells=same_cells, hardpoints=[moved]))

    def test_saves_the_layout_and_returns_the_new_revision(self):
        _, _, revision = self.layout()
        moved = hardpoint_row(tail=())
        moved[1] = [0, 0]
        returned = self.edit(revision, cells=[True, True, True, False], hardpoints=[moved])
        shape_triple, hardpoints, saved = self.layout()
        self.assertEqual([2, 2, [True, True, True, False]], shape_triple)
        self.assertEqual([0, 0], ship_cc.decode_hardpoint(hardpoints[0])["Position"])
        self.assertEqual(saved, returned)
        self.assertNotEqual(revision, returned)

    def test_preserves_every_slot_it_does_not_edit(self):
        _, _, revision = self.layout()
        before = self.body()
        self.edit(revision)
        after = self.body()
        for slot in range(len(before)):
            if slot != ship_cc.HULL_SLOT:
                self.assertEqual(before[slot], after[slot], f"ship slot {slot} changed")
        for slot in range(len(before[ship_cc.HULL_SLOT])):
            if slot not in (ship_cc.HULL_SHAPE_SLOT, ship_cc.HULL_HARDPOINTS_SLOT):
                self.assertEqual(before[ship_cc.HULL_SLOT][slot], after[ship_cc.HULL_SLOT][slot], f"hull slot {slot} changed")

    def test_carries_a_later_schemas_hardpoint_slots_by_mount_id(self):
        _, _, revision = self.layout()
        starboard = hardpoint_row("thruster.starboard", tail=())
        self.edit(revision, hardpoints=[starboard, hardpoint_row(tail=())])
        rows = self.layout()[1]
        self.assertEqual(["thruster.starboard", "thruster.port"], [ship_cc.decode_hardpoint(row)["Transform"] for row in rows])
        self.assertEqual(len(ship_cc.HARDPOINT_MEMBERS), len(rows[0]))
        self.assertEqual(["future-hardpoint-slot"], rows[1][len(ship_cc.HARDPOINT_MEMBERS):])

    def test_drops_the_row_of_a_removed_mount(self):
        _, _, revision = self.layout()
        self.edit(revision, hardpoints=[])
        self.assertEqual([], self.layout()[1])

    def test_keeps_the_schema_catalog(self):
        _, _, revision = self.layout()
        self.edit(revision)
        envelope = cultcache_py.SingleFileMessagePackBackingStore(self.path).pull_all()[0]
        self.assertEqual(CATALOG.members, envelope.catalog_entry.members)

    def test_refuses_a_ship_id_that_changed_and_writes_nothing(self):
        _, _, revision = self.layout()
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "bound ship ID changed"):
            self.edit(revision, ship_id="mod.other")
        self.assertEqual(before, self.bytes())

    def test_refuses_a_stale_revision_and_writes_nothing(self):
        _, _, revision = self.layout()
        self.edit(revision)
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "layout changed since Load"):
            self.edit(revision)
        self.assertEqual(before, self.bytes())

    def test_refuses_grid_sizes_outside_one_to_thirty_two_and_writes_nothing(self):
        _, _, revision = self.layout()
        before = self.bytes()
        for width, height in ((0, 2), (2, 0), (33, 1), (1, 33)):
            with self.subTest(width=width, height=height):
                with self.assertRaisesRegex(ValueError, "1..32 cells"):
                    self.edit(revision, width=width, height=height, cells=[True] * (width * height))
        for width, height in ((1, 1), (32, 32)):
            with self.subTest(width=width, height=height):
                self.edit(revision, width=width, height=height, cells=[True] * (width * height), hardpoints=[])
                revision = self.layout()[2]
        self.assertNotEqual(before, self.bytes())

    def test_refuses_a_cell_count_that_does_not_fill_the_grid(self):
        _, _, revision = self.layout()
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "1..32 cells"):
            self.edit(revision, cells=[True, True, True])
        self.assertEqual(before, self.bytes())

    def test_refuses_a_grid_with_no_occupied_cell(self):
        _, _, revision = self.layout()
        with self.assertRaisesRegex(ValueError, "needs an occupied cell"):
            self.edit(revision, cells=[False] * 4)

    def test_refuses_a_hardpoint_row_of_the_wrong_arity_or_without_a_mount_id(self):
        _, _, revision = self.layout()
        before = self.bytes()
        row = hardpoint_row(tail=())
        for name, bad in (("short", row[:-1]), ("long", row + [0]),
                          ("blank", ship_cc.encode_hardpoint(**{**ship_cc.decode_hardpoint(row), "Transform": ""})),
                          ("none", ship_cc.encode_hardpoint(**{**ship_cc.decode_hardpoint(row), "Transform": None})),
                          ("number", ship_cc.encode_hardpoint(**{**ship_cc.decode_hardpoint(row), "Transform": 4}))):
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "stable mount ID"):
                    self.edit(revision, hardpoints=[bad])
        self.assertEqual(before, self.bytes())

    def test_refuses_a_hardpoint_footprint_that_is_not_a_filled_grid_of_one_to_thirty_two(self):
        _, _, revision = self.layout()
        before = self.bytes()
        for name, footprint in (("no width", shape(0, 1, [])), ("no height", shape(1, 0, [])),
                                ("too wide", shape(33, 1, [True] * 33)), ("too tall", shape(1, 33, [True] * 33)),
                                ("short", shape(2, 1, [True])), ("empty", shape(1, 1, [False]))):
            row = ship_cc.encode_hardpoint(**{**ship_cc.decode_hardpoint(hardpoint_row(tail=())), "Shape": [footprint]})
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "thruster.port has an invalid footprint"):
                    self.edit(revision, hardpoints=[row])
        self.assertEqual(before, self.bytes())

    def test_refuses_a_payload_without_the_hull_hardpoint_slot(self):
        body = self.body()
        body[ship_cc.HULL_SLOT] = body[ship_cc.HULL_SLOT][:ship_cc.HULL_HARDPOINTS_SLOT]
        self.write(body)
        with self.assertRaisesRegex(ValueError, "incompatible typed payload"):
            self.layout()


class HardpointRowTests(unittest.TestCase):
    def test_a_row_is_one_value_per_member_in_slot_order(self):
        fields = dict(Type=1, Position=[2, 3], Shape="shape", Transform="mount", Rotation=2, Armor=4.0, FiringArc=90.0)
        row = ship_cc.encode_hardpoint(**fields)
        self.assertEqual([fields[name] for name in ship_cc.HARDPOINT_MEMBERS], row)
        self.assertEqual(fields, ship_cc.decode_hardpoint(row))

    def test_encoding_needs_every_member_and_no_other(self):
        good = dict.fromkeys(ship_cc.HARDPOINT_MEMBERS, 0)
        with self.assertRaises(ValueError):
            ship_cc.encode_hardpoint(**{name: 0 for name in ship_cc.HARDPOINT_MEMBERS[1:]})
        with self.assertRaises(ValueError):
            ship_cc.encode_hardpoint(**good, Extra=1)


class Matrix:
    """Stands in for mathutils.Matrix: a translation by (10, 20, 30)."""

    def __matmul__(self, position):
        return tuple(value + offset for value, offset in zip(position, (10.0, 20.0, 30.0)))


def point(x, radius=0.5, opacity=1.0):
    return SimpleNamespace(position=(x, 0.0, 0.0), radius=radius, opacity=opacity)


def stroke(points, material_index=0, cyclic=False):
    return SimpleNamespace(points=points, material_index=material_index, cyclic=cyclic)


def frame(number, strokes):
    return SimpleNamespace(frame_number=number, drawing=SimpleNamespace(strokes=strokes))


def layer(name, frames):
    return SimpleNamespace(name=name, frames=frames)


def grease_pencil(layers, materials):
    return SimpleNamespace(type="GREASEPENCIL", matrix_world=Matrix(), data=SimpleNamespace(layers=layers, materials=materials))


class CaptureTests(unittest.TestCase):
    def test_refuses_an_object_that_is_not_grease_pencil(self):
        with self.assertRaisesRegex(ValueError, "is not a Grease Pencil object"):
            ship_cc.capture_grease_pencil(SimpleNamespace(type="MESH", name="Cube"), None, 1)

    def test_captures_the_latest_frame_at_or_before_the_current_one(self):
        materials = [SimpleNamespace(name="Ink", grease_pencil=SimpleNamespace(color=(0.1, 0.2, 0.3, 1.0)))]
        obj = grease_pencil([layer("Hull", [frame(1, [stroke([point(1.0), point(2.0)])]),
                                            frame(5, [stroke([point(3.0), point(4.0)])]),
                                            frame(9, [stroke([point(5.0), point(6.0)])])])], materials)
        lines = ship_cc.capture_grease_pencil(obj, None, 5, evaluated=False)
        self.assertEqual([["Hull", "Ink", [13.0, 20.0, 30.0, 14.0, 20.0, 30.0], [0.5, 0.5], [1.0, 1.0], False,
                           [0.1, 0.2, 0.3, 1.0]]], lines)
        self.assertEqual(13.0, ship_cc.capture_grease_pencil(obj, None, 6, evaluated=False)[0][2][0])
        self.assertEqual(11.0, ship_cc.capture_grease_pencil(obj, None, 1, evaluated=False)[0][2][0])

    def test_skips_layers_without_a_frame_yet_and_strokes_of_one_point(self):
        obj = grease_pencil([layer("Later", [frame(7, [stroke([point(1.0), point(2.0)])])]),
                             layer("Now", [frame(1, [stroke([point(1.0)]), stroke([point(1.0), point(2.0)], cyclic=True)])])],
                            [SimpleNamespace(name="Plain", grease_pencil=None, diffuse_color=(0.4, 0.5, 0.6, 1.0))])
        lines = ship_cc.capture_grease_pencil(obj, None, 3, evaluated=False)
        self.assertEqual(1, len(lines))
        self.assertEqual(["Now", "Plain"], lines[0][:2])
        self.assertTrue(lines[0][5])
        self.assertEqual([0.4, 0.5, 0.6, 1.0], lines[0][6])

    def test_a_stroke_without_a_material_is_white_and_unnamed(self):
        obj = grease_pencil([layer("Hull", [frame(1, [stroke([point(1.0), point(2.0)])])])], [None])
        line = ship_cc.capture_grease_pencil(obj, None, 1, evaluated=False)[0]
        self.assertEqual(["Hull", ""], line[:2])
        self.assertEqual([1.0, 1.0, 1.0, 1.0], line[6])

    def test_reads_strokes_from_the_evaluated_copy_but_transforms_by_the_source_object(self):
        evaluated = grease_pencil([layer("Baked", [frame(1, [stroke([point(1.0), point(2.0)])])])], [None])
        source = grease_pencil([layer("Raw", [frame(1, [stroke([point(1.0), point(2.0)])])])], [None])
        source.evaluated_get = lambda depsgraph: evaluated
        self.assertEqual("Baked", ship_cc.capture_grease_pencil(source, "graph", 1)[0][0])
        self.assertEqual("Raw", ship_cc.capture_grease_pencil(source, "graph", 1, evaluated=False)[0][0])


if __name__ == "__main__":
    unittest.main()
