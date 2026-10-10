"""Blender-free tests for aetheria_ships/ship_cc.py, the Python view of a ship file's HullData and ShipAuthoring records.

Run with CultLib's cultcache-py and msgpack importable:

    CULTLIB_PACKAGES=<CultLib>/packages python -m unittest discover -s tools/blender/tests

ship_cc is loaded by path because the package __init__ imports bpy. The record layout the tests build comes from
ship_cc's own slot constants; ShipSchemaPinTests (C#) pins those constants to the C# [Key] attributes.
"""

import dataclasses
import importlib.util
import os
import shutil
import sys
import tempfile
import unittest
from datetime import datetime
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


def hull_body():
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
    body[2] = "skiff.glb"
    body[3] = [["map", "map-icon", "map", None, 0]]
    body[ship_cc.SCHEMATIC_LINES_SLOT] = LINES
    return body


def catalog(schema_id, schema_name):
    return cultcache_py.CultCacheSchemaCatalogEntry(
        schema_id=schema_id, schema_name=schema_name, schema_version="1",
        content_hash="hash", canonical_schema_json='{"members":[{"slot":9,"name":"FutureMember"}]}',
        compatible_schema_ids=(schema_id,),
        members=(cultcache_py.CultCacheSchemaCatalogMember(slot=9, member_name="FutureMember", type_name="string"),))


CATALOG = catalog("ship-authoring-schema-1", ship_cc.SCHEMA)
HULL_CATALOG = catalog("hull-schema-1", ship_cc.HULL_SCHEMA)


def envelope(key, schema, entry, body):
    return cultcache_py.CultCacheEnvelope.create(
        key=key, type=schema, payload=msgpack.packb(body, use_bin_type=True),
        schema_id=entry.schema_id, catalog_entry=entry)


class ShipFileCase(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.path = str(Path(directory.name) / "ship.cc")
        self.write(ship_body(), hull_body())

    def write(self, ship, hull, path=None):
        cultcache_py.SingleFileMessagePackBackingStore(path or self.path).push_all([
            envelope("mod-ship:mod.skiff", ship_cc.SCHEMA, CATALOG, ship),
            envelope("mod-hull:mod.skiff", ship_cc.HULL_SCHEMA, HULL_CATALOG, hull)])

    def ship(self):
        return ship_cc.read(self.path, PACKAGES).ship.body

    def hull(self):
        return ship_cc.read(self.path, PACKAGES).hull.body

    def envelopes(self):
        return {(e.type, e.key): e for e in cultcache_py.SingleFileMessagePackBackingStore(self.path).pull_all()}

    def bytes(self):
        return Path(self.path).read_bytes()


class ReadTests(ShipFileCase):
    def test_reads_the_ship_and_its_hull(self):
        file = ship_cc.read(self.path, PACKAGES)
        self.assertEqual("mod.skiff", file.ship.body[0])
        self.assertEqual(ship_cc.SCHEMA, file.ship.envelope.type)
        self.assertEqual("Skiff", file.hull.body[1])
        self.assertEqual(ship_cc.HULL_SCHEMA, file.hull.envelope.type)

    def test_refuses_a_file_with_another_record_beside_the_pair(self):
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push(cultcache_py.CultCacheEnvelope.create(
            key="other", type="aetheria.something", payload=msgpack.packb([1], use_bin_type=True)))
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_file_missing_either_half_of_the_pair(self):
        for name, schema, entry, body in (("hull only", ship_cc.HULL_SCHEMA, HULL_CATALOG, hull_body()),
                                          ("ship only", ship_cc.SCHEMA, CATALOG, ship_body())):
            other = str(Path(self.path).with_name(name.replace(" ", "-") + ".cc"))
            cultcache_py.SingleFileMessagePackBackingStore(other).push(envelope("key", schema, entry, body))
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "must hold exactly one"):
                    ship_cc.read(other, PACKAGES)

    def test_refuses_a_file_with_two_hulls(self):
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push(
            envelope("mod-hull:mod.other", ship_cc.HULL_SCHEMA, HULL_CATALOG, hull_body()))
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_file_with_no_record(self):
        empty = str(Path(self.path).with_name("empty.cc"))
        Path(empty).write_bytes(b"")
        with self.assertRaisesRegex(ValueError, "must hold exactly one"):
            ship_cc.read(empty, PACKAGES)

    def test_refuses_a_ship_record_that_still_carries_the_retired_embedded_hull(self):
        for name, ship in (("nested hull", ["mod.skiff", hull_body()]),
                           ("any value", ["mod.skiff", 0])):
            body = ship_body()
            body[ship_cc.RETIRED_HULL_SLOT] = ship[1]
            with self.subTest(name):
                self.write(body, hull_body())
                with self.assertRaisesRegex(ValueError, "legacy embedded hull at retired key 1"):
                    ship_cc.read(self.path, PACKAGES)
        # Nothing else is said about its shape: an old file has no hull record beside the ship.
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push_all(
            [envelope("mod-ship:mod.skiff", ship_cc.SCHEMA, CATALOG, body)])
        with self.assertRaisesRegex(ValueError, "legacy embedded hull"):
            ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_save_over_a_file_with_the_retired_embedded_hull_and_writes_nothing(self):
        body = ship_body()
        body[ship_cc.RETIRED_HULL_SLOT] = hull_body()
        self.write(body, hull_body())
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "legacy embedded hull"):
            ship_cc.replace_lines(self.path, PACKAGES, LINES)
        with self.assertRaisesRegex(ValueError, "legacy embedded hull"):
            ship_cc.replace_layout(self.path, PACKAGES, "mod.skiff", "any", [2, 2, [True] * 4], [])
        self.assertEqual(before, self.bytes())

    def test_a_nil_in_the_retired_slot_is_the_current_shape(self):
        self.assertIsNone(self.ship()[ship_cc.RETIRED_HULL_SLOT])

    def test_refuses_a_ship_payload_too_short_for_the_line_slot(self):
        for body in (["mod.skiff"], ["mod.skiff", None]):
            with self.subTest(slots=len(body)):
                self.write(body, hull_body())
                with self.assertRaisesRegex(ValueError, "incompatible"):
                    ship_cc.read(self.path, PACKAGES)

    def test_refuses_a_payload_that_is_not_an_array(self):
        for payload in ({"Id": "mod.skiff"}, {str(index): index for index in range(9)}, "a string long enough to fill every slot"):
            with self.subTest(ship=payload):
                self.write(payload, hull_body())
                with self.assertRaisesRegex(ValueError, "incompatible"):
                    ship_cc.read(self.path, PACKAGES)
            with self.subTest(hull=payload):
                self.write(ship_body(), payload)
                with self.assertRaisesRegex(ValueError, "incompatible"):
                    ship_cc.read(self.path, PACKAGES)


class RigCarryThroughTests(ShipFileCase):
    """The rig (ShipAuthoring slot 5 Joints, ShipAnchor slot 5 Joint) lies past every slot this module edits, so a save keeps it."""

    JOINTS = [["turret", None, [0.0, 1.0, 0.0], [-30.0], [30.0]]]

    def setUp(self):
        super().setUp()
        ship = ship_body()
        ship.append(self.JOINTS)
        ship[ship_cc.ANCHORS_SLOT] = [["gun", "weapon-mount", "gun", None, 0, "turret"], ["map", "map-icon", "map", None, 0]]
        self.write(ship, hull_body())

    def test_replace_visual_keeps_the_joints_and_each_mounts_joint_by_anchor_id(self):
        ship_cc.replace_visual(self.path, PACKAGES, "mod.skiff", "ship.glb",
                               [["map", "map-icon", "map", None, 0], ["gun", "weapon-mount", "gun", None, 0]])
        ship = self.ship()
        self.assertEqual(self.JOINTS, ship[5])
        self.assertEqual([["map", "map-icon", "map", None, 0], ["gun", "weapon-mount", "gun", None, 0, "turret"]],
                         ship[ship_cc.ANCHORS_SLOT])

    def test_replace_lines_keeps_the_joints_and_the_anchor_rows(self):
        before = self.ship()
        ship_cc.replace_lines(self.path, PACKAGES, LINES)
        after = self.ship()
        self.assertEqual(self.JOINTS, after[5])
        self.assertEqual(before[ship_cc.ANCHORS_SLOT], after[ship_cc.ANCHORS_SLOT])


class ReplaceLinesTests(ShipFileCase):
    def test_replaces_only_the_line_slot(self):
        new_lines = [["Detail", "Red", [0.0] * 6, [0.1, 0.1], [1.0, 1.0], True, [1.0, 0.0, 0.0, 1.0]]]
        before = self.ship()
        self.assertEqual(1, ship_cc.replace_lines(self.path, PACKAGES, new_lines))
        after = self.ship()
        self.assertEqual(new_lines, after[ship_cc.SCHEMATIC_LINES_SLOT])
        for slot in range(len(before)):
            if slot != ship_cc.SCHEMATIC_LINES_SLOT:
                self.assertEqual(before[slot], after[slot], f"slot {slot} changed")

    def test_a_save_refreshes_the_stored_at_of_the_record_it_writes_and_only_that_one(self):
        stale = "2000-01-01T00:00:00+00:00"
        envelopes = self.envelopes()
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push_all(
            [dataclasses.replace(envelopes[key], stored_at=stale) for key in envelopes])
        ship_cc.replace_lines(self.path, PACKAGES, LINES)
        after = self.envelopes()
        self.assertGreater(datetime.fromisoformat(after[(ship_cc.SCHEMA, "mod-ship:mod.skiff")].stored_at),
                           datetime.fromisoformat(stale))
        self.assertEqual(stale, after[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")].stored_at)

    def test_leaves_the_hull_record_byte_identical(self):
        before = self.envelopes()[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")]
        ship_cc.replace_lines(self.path, PACKAGES, LINES)
        self.assertEqual(before, self.envelopes()[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")])

    def test_keeps_the_schema_catalog(self):
        ship_cc.replace_lines(self.path, PACKAGES, LINES)
        envelope = self.envelopes()[(ship_cc.SCHEMA, "mod-ship:mod.skiff")]
        self.assertEqual(CATALOG.members, envelope.catalog_entry.members)
        self.assertEqual(CATALOG.canonical_schema_json, envelope.catalog_entry.canonical_schema_json)

    def test_refuses_an_empty_capture_and_changes_nothing(self):
        before = self.bytes()
        with self.assertRaisesRegex(ValueError, "No Grease Pencil strokes"):
            ship_cc.replace_lines(self.path, PACKAGES, [])
        self.assertEqual(before, self.bytes())


class ReplaceVisualTests(ShipFileCase):
    def anchor(self, anchor_id, role="map-icon", parent=None, order=0):
        return [anchor_id, role, anchor_id, parent, order]

    def test_writes_the_model_asset_and_anchor_rows_in_member_order(self):
        rows = [self.anchor("map"), self.anchor("gun", "weapon-mount"), self.anchor("gun.muzzle", "weapon-muzzle", "gun", 1)]
        self.assertEqual(3, ship_cc.replace_visual(self.path, PACKAGES, "mod.skiff", "ship.glb", rows))
        ship = self.ship()
        self.assertEqual("ship.glb", ship[ship_cc.MODEL_ASSET_SLOT])
        self.assertEqual(rows, ship[ship_cc.ANCHORS_SLOT])
        self.assertEqual(dict(zip(ship_cc.ANCHOR_MEMBERS, rows[2])),
                         {"Id": "gun.muzzle", "Role": "weapon-muzzle", "ModelNodeId": "gun.muzzle", "ParentId": "gun", "Order": 1})

    def test_replace_visual_carries_unknown_anchor_slots_by_id(self):
        ship = ship_body()
        ship[ship_cc.ANCHORS_SLOT] = [["map", "map-icon", "map", None, 0, "future-anchor-slot"],
                                      ["shield", "shield", "shield", None, 0, "shield-future"]]
        self.write(ship, hull_body())
        # Reordered, with one new anchor: each known Id keeps its own tail, the new one gets none.
        ship_cc.replace_visual(self.path, PACKAGES, "mod.skiff", "ship.glb",
                               [self.anchor("shield", "shield"), self.anchor("tractor", "tractor"), self.anchor("map")])
        self.assertEqual([["shield", "shield", "shield", None, 0, "shield-future"],
                          ["tractor", "tractor", "tractor", None, 0],
                          ["map", "map-icon", "map", None, 0, "future-anchor-slot"]], self.ship()[ship_cc.ANCHORS_SLOT])

    def test_replace_visual_refuses_blank_or_duplicate_ids_and_a_changed_ship(self):
        before = self.bytes()
        cases = (("blank", "mod.skiff", [self.anchor(" ")], "needs an ID"),
                 ("none", "mod.skiff", [[None, "map-icon", "x", None, 0]], "needs an ID"),
                 ("duplicate", "mod.skiff", [self.anchor("map"), self.anchor("map", "shield")], "used twice"),
                 ("arity", "mod.skiff", [["map", "map-icon", "map", None]], "exactly"),
                 ("ship", "mod.other", [self.anchor("map")], "ship ID changed"))
        for name, ship_id, rows, message in cases:
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, message):
                    ship_cc.replace_visual(self.path, PACKAGES, ship_id, "ship.glb", rows)
                self.assertEqual(before, self.bytes())

    def test_replace_visual_touches_only_model_asset_and_anchors(self):
        hull_before = self.envelopes()[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")]
        ship_before = self.ship()
        ship_cc.replace_visual(self.path, PACKAGES, "mod.skiff", "ship.glb", [self.anchor("hull", "hull-collider")])
        after = self.envelopes()
        self.assertEqual(hull_before, after[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")])
        ship_after = self.ship()
        for slot, (old, new) in enumerate(zip(ship_before, ship_after)):
            if slot not in (ship_cc.MODEL_ASSET_SLOT, ship_cc.ANCHORS_SLOT):
                self.assertEqual(old, new, f"slot {slot}")
        self.assertEqual(len(ship_before), len(ship_after))
        self.assertEqual(LINES, ship_after[ship_cc.SCHEMATIC_LINES_SLOT])
        self.assertEqual(CATALOG.schema_id, after[(ship_cc.SCHEMA, "mod-ship:mod.skiff")].schema_id)


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
        ship = self.ship()
        ship[ship_cc.SCHEMATIC_LINES_SLOT] = []
        hull = self.hull()
        hull[1] = "Renamed"
        self.write(ship, hull)
        self.assertEqual(revision, self.layout()[2])
        hull[ship_cc.HULL_SHAPE_SLOT] = [shape(2, 2, [True, True, True, True])]
        self.write(ship, hull)
        self.assertNotEqual(revision, self.layout()[2])

    def test_layout_revision_is_the_revision_replace_layout_returns_for_the_same_shape_and_rows(self):
        _, _, revision = self.layout()
        cells = [True, True, True, False]
        rows = [hardpoint_row("thruster.starboard", tail=())]  # a mount the file carried no later slots for
        saved = self.edit(revision, cells=cells, hardpoints=rows)
        self.assertEqual(saved, ship_cc.layout_revision(shape(2, 2, cells), rows))
        self.assertEqual(saved, self.layout()[2])
        self.assertNotEqual(saved, ship_cc.layout_revision(shape(2, 2, [True, True, False, False]), rows))
        self.assertNotEqual(saved, ship_cc.layout_revision(shape(2, 2, cells), []))

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
        ship_before, hull_before = self.ship(), self.hull()
        ship_envelope = self.envelopes()[(ship_cc.SCHEMA, "mod-ship:mod.skiff")]
        self.edit(revision)
        self.assertEqual(ship_before, self.ship())
        self.assertEqual(ship_envelope, self.envelopes()[(ship_cc.SCHEMA, "mod-ship:mod.skiff")])
        hull_after = self.hull()
        self.assertEqual(len(hull_before), len(hull_after))
        for slot in range(len(hull_before)):
            if slot not in (ship_cc.HULL_SHAPE_SLOT, ship_cc.HULL_HARDPOINTS_SLOT):
                self.assertEqual(hull_before[slot], hull_after[slot], f"hull slot {slot} changed")

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
        envelope = self.envelopes()[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")]
        self.assertEqual(HULL_CATALOG.members, envelope.catalog_entry.members)

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

    def test_a_layout_save_refreshes_the_stored_at_of_the_hull_and_leaves_the_ships(self):
        stale = "2000-01-01T00:00:00+00:00"
        envelopes = self.envelopes()
        cultcache_py.SingleFileMessagePackBackingStore(self.path).push_all(
            [dataclasses.replace(envelopes[key], stored_at=stale) for key in envelopes])
        self.edit(self.layout()[2])
        after = self.envelopes()
        self.assertGreater(datetime.fromisoformat(after[(ship_cc.HULL_SCHEMA, "mod-hull:mod.skiff")].stored_at),
                           datetime.fromisoformat(stale))
        self.assertEqual(stale, after[(ship_cc.SCHEMA, "mod-ship:mod.skiff")].stored_at)

    def test_refuses_cells_that_are_not_booleans_and_writes_nothing(self):
        _, _, revision = self.layout()
        before = self.bytes()
        for name, cells in (("integer", [True, 1, True, False]), ("zero", [True, True, True, 0]),
                            ("string", [True, "x", True, False]), ("none", [True, None, True, False])):
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "Hull grid cells must be booleans"):
                    self.edit(revision, cells=cells)
        self.assertEqual(before, self.bytes())

    def test_refuses_hardpoint_footprint_cells_that_are_not_booleans_and_writes_nothing(self):
        _, _, revision = self.layout()
        before = self.bytes()
        for name, cells in (("integer", [1, True]), ("none", [True, None])):
            row = ship_cc.encode_hardpoint(**{**ship_cc.decode_hardpoint(hardpoint_row(tail=())),
                                              "Shape": [shape(2, 1, cells)]})
            with self.subTest(name):
                with self.assertRaisesRegex(ValueError, "Hardpoint footprint cells must be booleans"):
                    self.edit(revision, hardpoints=[row])
        self.assertEqual(before, self.bytes())

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
                with self.assertRaisesRegex(ValueError, "hardpoint has an invalid footprint"):
                    self.edit(revision, hardpoints=[row])
        self.assertEqual(before, self.bytes())

    def test_refuses_a_hull_payload_without_the_hardpoint_slot(self):
        hull = self.hull()[:ship_cc.HULL_HARDPOINTS_SLOT]
        self.write(self.ship(), hull)
        with self.assertRaisesRegex(ValueError, "incompatible typed payload"):
            self.layout()


@unittest.skipUnless(os.environ.get("SHIP_FIXTURE_DIR"), "SHIP_FIXTURE_DIR names the C#-written fixture (see below)")
class CSharpWrittenFixtureTests(unittest.TestCase):
    """Gate 1's Python half: a package C# wrote survives a Python edit with every untouched slot intact.

    The fixture comes from the C# suite (`AETHERIA_SHIP_FIXTURE_DIR=<dir> dotnet test ... --filter
    TheFixturePackageIsCompleteAndItsGlbCarriesAScene`). Point SHIP_FIXTURE_DIR at that <dir> and, optionally,
    SHIP_EDITED_DIR at an empty directory to keep the edited package; C# then reopens and validates it with
    `dotnet run --project tools/AetherDb -- ship-authoring validate <SHIP_EDITED_DIR>/mod.skiff/ship.cc`.
    """

    def test_a_layout_and_line_edit_preserves_every_slot_it_does_not_own(self):
        with tempfile.TemporaryDirectory() as scratch:
            path = str(Path(scratch) / "ship.cc")
            shutil.copy(Path(os.environ["SHIP_FIXTURE_DIR"]) / "mod.skiff" / "ship.cc", path)
            before = ship_cc.read(path, PACKAGES)
            shape_triple, hardpoints, revision = ship_cc.read_layout(path, PACKAGES)
            width, height, cells = shape_triple
            self.assertEqual(64, len(revision))
            cells = list(cells)
            cells[-1] = True
            ship_cc.replace_layout(path, PACKAGES, before.ship.body[0], revision, [width, height, cells], hardpoints)
            ship_cc.replace_lines(path, PACKAGES, before.ship.body[ship_cc.SCHEMATIC_LINES_SLOT])
            after = ship_cc.read(path, PACKAGES)

            self.assertEqual(before.ship.body, after.ship.body)
            self.assertEqual(before.ship.envelope.key, after.ship.envelope.key)
            self.assertEqual(before.ship.envelope.catalog_entry, after.ship.envelope.catalog_entry)
            self.assertEqual(before.hull.envelope.key, after.hull.envelope.key)
            self.assertEqual(before.hull.envelope.catalog_entry, after.hull.envelope.catalog_entry)
            for slot in range(len(before.hull.body)):
                if slot != ship_cc.HULL_SHAPE_SLOT:
                    self.assertEqual(before.hull.body[slot], after.hull.body[slot], f"hull slot {slot} changed")
            self.assertTrue(after.hull.body[ship_cc.HULL_SHAPE_SLOT][0][2][-1])

            kept = os.environ.get("SHIP_EDITED_DIR")
            if kept:
                target = Path(kept) / "mod.skiff"
                target.mkdir(parents=True, exist_ok=True)
                shutil.copy(path, target / "ship.cc")
                shutil.copy(Path(os.environ["SHIP_FIXTURE_DIR"]) / "mod.skiff" / "skiff.glb", target / "skiff.glb")


class RefusalsNameNoInputTests(ShipFileCase):
    def test_refusals_name_no_path_mount_or_anchor_id(self):
        _, _, revision = ship_cc.read_layout(self.path, PACKAGES)
        mount = "mount.zq-unique"
        duplicate = [[ "anchor.zq-unique", "map-icon", "n1", None, 0], ["anchor.zq-unique", "shield", "n2", None, 0]]
        bad_footprint = ship_cc.encode_hardpoint(
            Type=3, Position=[1, 0], Shape=[[1, 1, [0]]], Transform=mount, Rotation=0, Armor=0.0, FiringArc=0.0)
        empty_footprint = ship_cc.encode_hardpoint(
            Type=3, Position=[1, 0], Shape=[[1, 1, [False]]], Transform=mount, Rotation=0, Armor=0.0, FiringArc=0.0)
        calls = (lambda: ship_cc.replace_visual(self.path, PACKAGES, "mod.skiff", "ship.glb", duplicate),
                 lambda: ship_cc.replace_layout(self.path, PACKAGES, "mod.skiff", revision, [2, 2, [True] * 4], [bad_footprint]),
                 lambda: ship_cc.replace_layout(self.path, PACKAGES, "mod.skiff", revision, [2, 2, [True] * 4], [empty_footprint]),
                 lambda: ship_cc.read(os.path.join(self.path, "missing.cc"), PACKAGES))
        for call in calls:
            with self.subTest(call):
                with self.assertRaises(Exception) as caught:
                    call()
                for forbidden in ("zq-unique", self.path, os.path.dirname(self.path)):
                    self.assertNotIn(forbidden, str(caught.exception))


class ShipIdTests(unittest.TestCase):
    def test_admits_the_ids_the_c_sharp_rule_admits(self):
        for ship_id in ("probe.a", "a", "0x", "a_b-c", "com10", "com0", "lpt0", "console", "aux2", "a.nul", "x.com1"):
            with self.subTest(ship_id):
                self.assertTrue(ship_cc.valid_ship_id(ship_id))

    def test_refuses_device_names_alone_or_before_a_dot_and_every_other_malformed_id(self):
        for ship_id in ("con", "nul.x", "com1", "lpt9", "prn", "aux.txt", "com5.a", "abc.", "a..", "Upper", ".x", "_x",
                        "-x", "", "a b", "a/b", "a:b", "abé", "a\b"):
            with self.subTest(ship_id):
                self.assertFalse(ship_cc.valid_ship_id(ship_id))

    def test_the_constants_name_the_allowed_characters_and_the_reserved_stems(self):
        self.assertEqual(set("abcdefghijklmnopqrstuvwxyz0123456789._-"), set(ship_cc.SHIP_ID_CHARS))
        self.assertEqual(22, len(ship_cc.RESERVED_SHIP_IDS))


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
