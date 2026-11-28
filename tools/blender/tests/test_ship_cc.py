"""Blender-free tests for aetheria_ships/ship_cc.py: the ship id rule and Grease Pencil capture.

    python -m unittest discover -s tools/blender/tests

ship_cc is loaded by path because the package __init__ imports bpy. ShipSchemaPinTests (C#) pins the ship id rule and
the enum names to the C# types.
"""

import importlib.util
import unittest
from pathlib import Path
from types import SimpleNamespace

_spec = importlib.util.spec_from_file_location(
    "ship_cc", Path(__file__).resolve().parents[1] / "aetheria_ships" / "ship_cc.py")
ship_cc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ship_cc)


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
