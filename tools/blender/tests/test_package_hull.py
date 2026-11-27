"""Blender-free tests for package_hull.py: manifest refusals and place(). The Blender half runs on the fixture manifest
(fixtures/hulls/zh-ammo-tender/package.json) in the Starfire job, not here.

    python -m unittest discover -s tools/blender/tests -p "test_package_hull.py"

package_hull is loaded by path; its bpy imports are inside package(), and its driver block runs only as __main__.
"""

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location(
    "package_hull", Path(__file__).resolve().parents[1] / "package_hull.py")
ph = importlib.util.module_from_spec(_spec)
sys.modules["package_hull"] = ph
_spec.loader.exec_module(ph)

FIXTURE = Path(__file__).resolve().parent / "fixtures" / "hulls" / "zh-ammo-tender" / "package.json"


class ManifestTests(unittest.TestCase):
    def load(self, edit=None):
        manifest = json.loads(FIXTURE.read_text(encoding="utf-8"))
        if edit:
            edit(manifest)
        with tempfile.TemporaryDirectory() as folder:
            (Path(folder) / "hull.scad").write_text("// hull\n", encoding="utf-8")
            path = Path(folder) / "package.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            return ph.load_manifest(path)

    def refused(self, edit, *words):
        with self.assertRaises(ph.Refused) as caught:
            self.load(edit)
        for word in words:
            self.assertIn(word, str(caught.exception))

    def test_the_fixture_manifest_loads(self):
        manifest = self.load()
        self.assertEqual(len(manifest["rows"]), 6)
        self.assertEqual(ph.rows_of(manifest)[0], ("Gun.D", "Ballistic", (2, 3), "#", 0, 20, 90))

    def test_a_missing_key_is_named(self):
        self.refused(lambda m: m.pop("price"), "missing", "price")
        self.refused(lambda m: m["stats"].pop("drag"), "stats", "drag")
        self.refused(lambda m: m["rows"][2].pop("arc"), "row 2", "arc")

    def test_an_unknown_key_is_named(self):
        self.refused(lambda m: m.update(nakovalnya=1), "unknown", "nakovalnya")
        self.refused(lambda m: m["stats"].update(speed=1), "stats", "speed")
        self.refused(lambda m: m["rows"][0].update(colour="red"), "row 0", "colour")

    def test_an_unknown_kind_is_named(self):
        self.refused(lambda m: m["rows"][1].update(kind="THRUSTER"), "Th.L", "THRUSTER")

    def test_a_malformed_footprint_is_named(self):
        for bad in ("", "#.#/#", "x", "..", "##/#X"):
            self.refused(lambda m, bad=bad: m["rows"][0].update(footprint=bad), "Gun.D", "footprint")

    def test_a_duplicate_row_id_is_named(self):
        self.refused(lambda m: m["rows"][4].update(id="Ra.L"), "Ra.L", "duplicate")

    def test_a_missing_hull_is_named(self):
        self.refused(lambda m: m.update(hull="absent.scad"), "absent.scad")


class PlaceTests(unittest.TestCase):
    ROW = ("Gun.D", "Ballistic", (2, 2), "##/##", 0, 20, 90)

    def test_a_fitting_row_keeps_its_cell(self):
        placed = ph.place([self.ROW], 6, 6, lambda x, y: True)
        self.assertEqual(placed[0][2], (2, 2))

    def test_a_row_outside_the_shape_moves_with_a_deviation(self):
        import io
        from contextlib import redirect_stdout
        with redirect_stdout(io.StringIO()) as captured:
            placed = ph.place([self.ROW], 6, 6, lambda x, y: x < 2)
        self.assertEqual(placed[0][2], (0, 2))
        self.assertIn("DEVIATION row Gun.D moved from (2, 2) to (0, 2)", captured.getvalue())

    def test_a_row_overlapping_an_earlier_one_moves(self):
        import io
        from contextlib import redirect_stdout
        second = ("Gun.E", "Ballistic", (2, 2), "#", 0, 20, 90)
        with redirect_stdout(io.StringIO()) as captured:
            placed = ph.place([self.ROW, second], 6, 6, lambda x, y: True)
        self.assertNotIn(placed[1][2], ph.cells_of(2, 2, "##/##"))
        self.assertIn("DEVIATION row Gun.E", captured.getvalue())

    def test_a_row_no_cell_fits_is_refused(self):
        with self.assertRaises(ph.Refused):
            ph.place([self.ROW], 6, 6, lambda x, y: False)


if __name__ == "__main__":
    unittest.main()
