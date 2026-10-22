"""The Zhestokost kit through the real OpenSCAD: where its schematic lines are drawn relative to its geometry, and which
frames draw none. Skipped unless SMOKE_OPENSCAD names the OpenSCAD executable (nightly with the Manifold backend).

    SMOKE_OPENSCAD=<path>\\openscad.com python -m unittest discover -s tools/blender/tests -p "test_kit_openscad.py"

hull_source is loaded by path because the package __init__ imports bpy.
"""

import importlib.util
import os
import sys
import tempfile
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location(
    "hull_source", Path(__file__).resolve().parents[1] / "aetheria_ships" / "hull_source.py")
hs = importlib.util.module_from_spec(_spec)
sys.modules["hull_source"] = hs
_spec.loader.exec_module(hs)

OPENSCAD = os.environ.get("SMOKE_OPENSCAD")
REPO = Path(__file__).resolve().parents[3]
NAKOVALNYA = REPO / "GameData" / "Mods" / "aetheria.nakovalnya" / "hull.scad"
FRAME = "every kit line is drawn in the frame of its geometry"

# Placement drifts (finding line-drift-inside-footprint): a raw translate or rotate around a part that draws lines, not an
# lp_at. Each moves the geometry and leaves the line where lp_at's frame says.
DRIFTS = {
    "spine mass 3 m aft": ("zh_mass(T2);", "translate([0, 3, 0]) zh_mass(T2);"),
    "radiator troughs 4 m forward": ("for (s = [-1, 1]) zh_on_top(M, RAD_Y, s*RAD_U) zh_rad_trough(RAD_W, RAD_L, 0.45);",
                                     "translate([0, -4, 0]) for (s = [-1, 1]) zh_on_top(M, RAD_Y, s*RAD_U) zh_rad_trough(RAD_W, RAD_L, 0.45);"),
    "apron reveals 3 m aft": ("for (u = [-0.33, 0.33]) zh_on_top(APRON, -19.5, u) zh_reveal(2.3, 7.0);",
                              "translate([0, 3, 0]) for (u = [-0.33, 0.33]) zh_on_top(APRON, -19.5, u) zh_reveal(2.3, 7.0);"),
    "flak ring rotated 18 degrees": ("for (f = FLAK) lp_at(t=[f[0], f[1], DECK - ZH_SINK]) flak(f[2]);",
                                     "rotate([0, 0, 18]) for (f = FLAK) lp_at(t=[f[0], f[1], DECK - ZH_SINK]) flak(f[2]);"),
}

# A fitting on every frame the kit mates to. Only the dorsal one faces up, so only it is seen from above.
HEAD = """include <zhestokost.scad>
part = "primary";
M = [zh_s(-16, 32, 5.0, -2.25), zh_s(-15, 34, 5.5, -2.5), zh_s(5, 33, 5.5, -2.5), zh_s(14, 27, 5.5, -2.5)];
"""
SIDEWAYS = {
    "wall": "zh_on_wall(M, 0, 1, 0.45) zh_hatch(2.6, 3.0);",
    "belt": "zh_on_belt(M, 0, 1, 0.5) zh_hatch(2.6, 3.0);",
    "belly": "zh_on_belly(M, 0, 0) zh_hatch(2.6, 3.0);",
    "stern": "zh_on_stern(M, 0, 0.5) zh_plate(4.0, 1.4, 0.3);",
    "nose": "zh_on_nose(M, 0, 0.5) zh_plate(4.0, 1.4, 0.3);",
}
DORSAL = "zh_on_top(M, 0, 0) zh_hatch(2.6, 3.0);"


@unittest.skipUnless(OPENSCAD, "SMOKE_OPENSCAD names no OpenSCAD")
class KitThroughOpenScad(unittest.TestCase):
    def build(self, source_text):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / "hull.scad"
            source.write_text(source_text, encoding="utf-8")
            return hs.build(hs.openscad_runner(OPENSCAD, source), hs.language_library(OPENSCAD), "Ship",
                            hs.openscad_projector(OPENSCAD))

    def lines(self, body):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / "hull.scad"
            source.write_text(HEAD + body, encoding="utf-8")
            return hs.read_lines(hs.openscad_runner(OPENSCAD, source)("lines")[1])

    def test_the_nakovalnya_draws_every_line_in_the_frame_of_its_geometry(self):
        built = self.build(NAKOVALNYA.read_text(encoding="utf-8"))
        self.assertEqual([c.name for c in built.failed()], [])
        self.assertIn(FRAME, [c.name for c in built.checks])

    def test_a_placement_that_drifts_from_its_lines_fails_the_frame_check(self):
        text = NAKOVALNYA.read_text(encoding="utf-8")
        for label, (old, new) in DRIFTS.items():
            with self.subTest(label):
                self.assertEqual(text.count(old), 1)
                self.assertIn(FRAME, [c.name for c in self.build(text.replace(old, new)).failed()])

    def test_a_fitting_on_a_frame_a_top_view_cannot_see_emits_no_line(self):
        for label, body in SIDEWAYS.items():
            with self.subTest(label):
                self.assertEqual(self.lines(body), [])

    def test_the_gate_is_the_frame_and_not_the_fitting(self):
        self.assertEqual(len(self.lines(DORSAL)), 1)
        self.assertEqual(len(self.lines(DORSAL + " ".join(SIDEWAYS.values()))), 1)


if __name__ == "__main__":
    unittest.main()
