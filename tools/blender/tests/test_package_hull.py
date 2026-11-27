"""Blender-free tests for package_hull.py: manifest refusals and place(). The Blender half runs on the fixture manifest
(fixtures/hulls/zh-ammo-tender/package.json) in the Starfire job, not here.

    python -m unittest discover -s tools/blender/tests -p "test_package_hull.py"

package_hull is loaded by path; its bpy imports are inside package(), and its driver block runs only as __main__.
"""

import importlib.util
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

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

    def test_a_station_kind_loads_and_must_be_text(self):
        self.assertEqual(self.load(lambda m: m.update(kind="depot"))["kind"], "depot")
        for bad in (7, "", None):
            self.refused(lambda m, bad=bad: m.update(kind=bad), "kind")

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

    def refused(self, rows, width, height, occupied, *words):
        with self.assertRaises(ph.Refused) as caught:
            ph.place(rows, width, height, occupied)
        for word in words:
            self.assertIn(word, str(caught.exception))

    def test_a_row_off_the_raster_is_refused_not_moved(self):
        self.refused([("Gun.D", "Ballistic", (50, 50), "#", 0, 20, 90)], 6, 6, lambda x, y: True, "Gun.D", "off the 6x6 raster")
        self.refused([("Gun.D", "Ballistic", (5, 5), "##/##", 0, 20, 90)], 6, 6, lambda x, y: True, "Gun.D", "off the 6x6 raster")

    def test_a_row_outside_the_shape_is_refused_not_moved(self):
        self.refused([self.ROW], 6, 6, lambda x, y: x < 2, "Gun.D", "outside the hull shape")

    def test_a_row_overlapping_an_earlier_one_is_refused_not_moved(self):
        second = ("Gun.E", "Ballistic", (3, 3), "#", 0, 20, 90)
        self.refused([self.ROW, second], 6, 6, lambda x, y: True, "Gun.E", "overlaps")


class SwapTests(unittest.TestCase):
    """The all-or-nothing run, through main() with the Blender half replaced by one that writes into the package and then fails
    or finishes. The existing package must be exactly as it was after any failure."""

    def setUp(self):
        self.work = tempfile.TemporaryDirectory()
        self.addCleanup(self.work.cleanup)
        root = Path(self.work.name)
        self.game = root / "game"
        self.manifest = root / "package.json"
        data = json.loads(FIXTURE.read_text(encoding="utf-8"))
        self.id = data["id"]
        self.manifest.write_text(json.dumps(data), encoding="utf-8")
        (root / data["hull"]).write_text("// hull" + chr(10), encoding="utf-8")
        self.package_dir = self.game / "GameData" / "Mods" / self.id
        old = mock.patch.dict(os.environ, {"GAME_FOLDER": str(self.game), "CULTLIB_PACKAGES": "x", "OPENSCAD": "x"})
        old.start()
        self.addCleanup(old.stop)
        self.real = ph.package
        self.addCleanup(setattr, ph, "package", self.real)

    def existing(self):
        self.package_dir.mkdir(parents=True)
        (self.package_dir / "ship.cc").write_bytes(b"studio edits")
        (self.package_dir / "ship.glb").write_bytes(b"model")

    def digest(self):
        files = sorted(self.game.rglob("*")) if self.game.exists() else []
        return [(str(p.relative_to(self.game)), p.read_bytes() if p.is_file() else None) for p in files]

    def builder(self, then):
        def package(manifest, manifest_dir, game, packages, openscad):
            self.package_dir.mkdir(parents=True, exist_ok=True)
            (self.package_dir / "ship.cc").write_bytes(b"derived")
            (self.package_dir / "hull.scad").write_bytes(b"half built")
            then()
        ph.package = package

    def run_entry(self, *flags):
        import io
        from contextlib import redirect_stderr, redirect_stdout
        with redirect_stdout(io.StringIO()) as out, redirect_stderr(io.StringIO()):
            code = ph.entry([str(self.manifest), *flags])
        return code, out.getvalue()

    def fail(self):
        raise ph.Refused("No cell fits")

    def test_a_failed_replace_leaves_the_package_exactly_as_it_was(self):
        self.existing()
        before = self.digest()
        self.builder(self.fail)
        code, out = self.run_entry("--replace")
        self.assertEqual(code, 1)
        self.assertIn("PACKAGE_FAILED: No cell fits", out)
        self.assertEqual(self.digest(), before)

    def test_a_crash_in_a_replace_leaves_the_package_exactly_as_it_was(self):
        self.existing()
        before = self.digest()
        self.builder(lambda: 1 / 0)
        code, out = self.run_entry("--replace")
        self.assertEqual(code, 1)
        self.assertTrue(out.rstrip().endswith("PACKAGE_FAILED"))
        self.assertEqual(self.digest(), before)

    def test_a_successful_replace_swaps_and_leaves_no_backup(self):
        self.existing()
        self.builder(lambda: None)
        code, out = self.run_entry("--replace")
        self.assertEqual((code, out.rstrip().endswith("PACKAGE_OK")), (0, True))
        self.assertEqual((self.package_dir / "ship.cc").read_bytes(), b"derived")
        self.assertEqual(sorted(p.name for p in self.game.iterdir()), ["GameData"])

    def test_an_existing_package_without_replace_is_refused_untouched(self):
        self.existing()
        before = self.digest()
        self.builder(lambda: self.fail())
        code, out = self.run_entry()
        self.assertEqual(code, 1)
        self.assertIn("--replace", out)
        self.assertEqual(self.digest(), before)

    def test_a_failed_first_run_leaves_no_package(self):
        self.builder(self.fail)
        code, _ = self.run_entry()
        self.assertEqual(code, 1)
        self.assertFalse(self.package_dir.exists())

    def test_a_manifest_off_the_raster_exits_1(self):
        def rows():
            ph.place([("Gun.D", "Ballistic", (50, 50), "#", 0, 20, 90)], 6, 6, lambda x, y: True)
        self.builder(rows)
        code, out = self.run_entry()
        self.assertEqual(code, 1)
        self.assertIn("PACKAGE_FAILED: row Gun.D", out)
        self.assertFalse(self.package_dir.exists())


if __name__ == "__main__":
    unittest.main()
