"""Blender-free tests for aetheria_ships/hull_source.py, which reads a hull.scad through OpenSCAD and judges it against its
faction's form language. A fake runner answers each part with fixture OFF and ECHO text, so no OpenSCAD runs here; the
real OpenSCAD and the real language files are exercised by smoke_hull_source.py.

    python -m unittest discover -s tools/blender/tests -p "test_hull_source.py"

hull_source is loaded by path because the package __init__ imports bpy.
"""

import importlib.util
import json
import sys
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location(
    "hull_source", Path(__file__).resolve().parents[1] / "aetheria_ships" / "hull_source.py")
hs = importlib.util.module_from_spec(_spec)
sys.modules["hull_source"] = hs
_spec.loader.exec_module(hs)

SECRET = "SECRET-NAME-7"  # a value a hull source supplies and no refusal may echo


def box(x0, x1, y0, y1, z0, z1):
    """An OFF text for an axis-aligned box: 8 vertices, 6 quads (12 triangles once fanned)."""
    v = [(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    rows = ["OFF", "8 6 0"] + [f"{x} {y} {z}" for x, y, z in v] + [f"4 {a} {b} {c} {d}" for a, b, c, d in quads]
    return "\n".join(rows)


def echo(name, value):
    return f"ECHO: {name} = {json.dumps(value)}\n"


def language(faction="zhestokost", zones=("primary", "secondary"), body="primary", proportion=((0.25, 0.6), (0.35, 0.75)),
             symmetry=("bilateral", 0.97)):
    palette = [[z, [0.1 * (i + 1), 0.2, 0.3]] for i, z in enumerate(zones)]
    return echo("lang", [faction, palette, body, [1, 0.6, 0.2], [0.5, 0.5, 0.5], [list(p) for p in proportion] if proportion
                         else [], list(symmetry)])


def brief(faction="zhestokost", donor="", length=(8, 14), triangles=(10, 100)):
    return echo("brief", [faction, donor, "tender", list(length), list(triangles), ["a read"]])


class Fixture:
    """A hull source as the fake runner sees it: part -> (OFF, ECHO)."""

    def __init__(self):
        # Body 6 wide, 10 long, 2 high (h/w 0.33, w/l 0.6); a bilateral plate on top; one gun with a muzzle nose-ward.
        self.parts = {
            "brief": ("", brief()),
            "lang": ("", language()),
            "primary": (box(-3, 3, -5, 5, 0, 2), ""),
            "secondary": (box(-1, 1, -1, 1, 2, 3), ""),
            "anchors": ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                        + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"])
                        + echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""])),
            "emit:Th.A": (box(0.5, 1.5, 5, 5.1, 0.5, 1.5), ""),
        }

    def runner(self):
        return lambda part: self.parts[part]

    def build(self):
        return hs.build(self.runner())

    def failed(self):
        return [c.name for c in self.build().failed()]


class ReadingTests(unittest.TestCase):
    def test_off_fans_polygons_into_triangles(self):
        mesh = hs.parse_off(box(0, 1, 0, 1, 0, 1))
        self.assertEqual((len(mesh.vertices), len(mesh.triangles)), (8, 12))
        self.assertEqual(mesh.bounds(), ((0, 0, 0), (1, 1, 1)))

    def test_off_accepts_a_header_with_counts_on_the_first_line_and_skips_comments(self):
        mesh = hs.parse_off("OFF 3 1 0\n# c\n0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n")
        self.assertEqual(mesh.triangles, ((0, 1, 2),))

    def test_nothing_is_an_empty_mesh_and_a_broken_file_is_refused_without_echoing_it(self):
        self.assertEqual(hs.parse_off("").triangles, ())
        with self.assertRaises(hs.HullSourceError) as caught:
            hs.parse_off(f"OFF\n3 1 0\n0 0 0\n1 0 0\n0 1 0\n3 0 1 9 {SECRET}\n")
        self.assertNotIn(SECRET, str(caught.exception))

    def test_echoes_are_read_by_name_and_only_plain_values_are_accepted(self):
        text = echo("anchor", ["a", 1]) + echo("other", [2]) + echo("anchor", ["b", 2.5e-05])
        self.assertEqual(hs.parse_echoes(text, "anchor"), [["a", 1], ["b", 2.5e-05]])
        with self.assertRaises(hs.HullSourceError) as caught:
            hs.parse_echoes(f"ECHO: anchor = [{SECRET}, undef]\n", "anchor")
        self.assertNotIn(SECRET, str(caught.exception))

    def test_a_brief_is_exactly_one_echo_of_six_fields(self):
        self.assertEqual(hs.read_brief(brief()).faction, "zhestokost")
        for text in ("", brief() + brief(), echo("brief", ["x"])):
            with self.assertRaises(hs.HullSourceError):
                hs.read_brief(text)

    def test_a_pirate_language_reads_with_no_body_and_no_proportion(self):
        pirate = hs.read_languages(language("pirate", ("primer", "weld"), body="", proportion=None,
                                            symmetry=("asymmetric", 0.9)))["pirate"]
        self.assertEqual((pirate.body, pirate.proportion, pirate.symmetry), ("", None, ("asymmetric", 0.9)))

    def test_a_language_naming_an_unknown_rule_or_a_body_outside_its_zones_is_refused(self):
        with self.assertRaises(hs.HullSourceError):
            hs.read_languages(language(symmetry=("mostly", 0.5)))
        with self.assertRaises(hs.HullSourceError):
            hs.read_languages(language(body="keel"))


class MirrorOverlapTests(unittest.TestCase):
    @staticmethod
    def top(text):
        mesh = hs.parse_off(text)
        return hs._triangles_xy(mesh)

    def test_a_centred_box_overlaps_its_reflection_completely(self):
        self.assertEqual(hs.mirror_overlap(self.top(box(-2, 2, 0, 4, 0, 1))), 1.0)

    def test_a_box_on_one_side_does_not_overlap_it_at_all(self):
        self.assertEqual(hs.mirror_overlap(self.top(box(1, 3, 0, 4, 0, 1))), 0.0)

    def test_a_box_straddling_the_axis_unevenly_overlaps_by_the_shared_part(self):
        # x in [-1, 3]: the reflection covers [-3, 1]; shared [-1, 1] = 2 of a union 6 wide
        self.assertAlmostEqual(hs.mirror_overlap(self.top(box(-1, 3, 0, 4, 0, 1))), 2 / 6, places=2)


class JudgementTests(unittest.TestCase):
    def test_a_hull_that_keeps_every_rule_passes_every_check(self):
        self.assertEqual(Fixture().failed(), [])

    def test_the_language_decides_proportions_not_the_hull(self):
        # The same 6 x 10 x 2 body: inside Zhestokost's ranges, outside a language that wants tall narrow bodies.
        fixture = Fixture()
        fixture.parts["lang"] = ("", language(proportion=((0.8, 1.2), (0.1, 0.3))))
        self.assertEqual(fixture.failed(), ["the body's proportions lie inside the language's ranges"])

    def test_a_body_too_tall_for_the_language_fails_proportion_and_nothing_else(self):
        fixture = Fixture()
        fixture.parts["primary"] = (box(-3, 3, -5, 5, 0, 5), "")
        self.assertEqual(fixture.failed(), ["the body's proportions lie inside the language's ranges"])

    def test_a_body_too_narrow_for_the_language_fails_proportion(self):
        fixture = Fixture()
        fixture.parts["primary"] = (box(-1, 1, -5, 5, 0, 1), "")
        self.assertIn("the body's proportions lie inside the language's ranges", fixture.failed())

    def test_a_bilateral_language_refuses_a_lopsided_hull(self):
        fixture = Fixture()
        fixture.parts["secondary"] = (box(2, 6, -1, 1, 2, 3), "")
        self.assertEqual(fixture.failed(), ["the top view obeys the language's bilateral rule"])

    def test_an_asymmetric_language_refuses_a_symmetric_hull(self):
        fixture = Fixture()
        fixture.parts["lang"] = ("", language("zhestokost", symmetry=("asymmetric", 0.9)))
        self.assertEqual(fixture.failed(), ["the top view obeys the language's asymmetric rule"])

    def test_an_asymmetric_language_accepts_a_lopsided_hull(self):
        fixture = Fixture()
        fixture.parts["lang"] = ("", language("zhestokost", symmetry=("asymmetric", 0.9)))
        fixture.parts["secondary"] = (box(2, 8, -4, 4, 0, 3), "")
        self.assertEqual(fixture.failed(), [])

    def test_a_zone_the_language_names_must_have_geometry(self):
        fixture = Fixture()
        fixture.parts["secondary"] = ("", "")
        self.assertEqual(fixture.failed(), ["every zone of the language has geometry"])

    def test_the_triangle_budget_is_a_range_with_two_ends(self):
        for budget in ((10, 20), (1000, 2000)):
            fixture = Fixture()
            fixture.parts["brief"] = ("", brief(triangles=budget))
            self.assertEqual(fixture.failed(), ["the triangle count lies inside the brief's budget"], budget)

    def test_the_length_is_a_range_with_two_ends(self):
        for length in ((1, 5), (30, 40)):
            fixture = Fixture()
            fixture.parts["brief"] = ("", brief(length=length))
            self.assertEqual(fixture.failed(), ["the length lies inside the brief's range"], length)

    MUZZLE = "every weapon-mount has a muzzle nose-ward of it and no muzzle is orphaned"

    def test_a_muzzle_aft_of_its_mount_is_refused(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                                    + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, 1, 3], 0, "Gun.A"])
                                    + echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])

    def test_a_muzzle_level_with_its_mount_is_not_nose_ward(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                                    + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [1, 0, 3], 0, "Gun.A"]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])

    def test_a_mount_without_a_muzzle_and_a_muzzle_without_a_mount_are_refused(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])

    def test_an_emitter_with_no_mesh_is_refused(self):
        fixture = Fixture()
        fixture.parts["emit:Th.A"] = ("", "")
        self.assertEqual(fixture.failed(), ["every emitter and radiator anchor has a mesh"])

    def test_the_collider_zone_is_the_languages_body(self):
        self.assertEqual(Fixture().build().body_zone, "primary")


class DonorTests(unittest.TestCase):
    """A Pirate hull: the Pirate language has no body and no proportions, so the donor's decide."""

    @staticmethod
    def pirate(donor_proportion=((0.12, 0.35), (0.55, 1.1)), donor="rnd"):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("pirate", donor, length=(8, 14)))
        fixture.parts["lang"] = ("", language("pirate", ("primer", "weld"), body="", proportion=None,
                                             symmetry=("asymmetric", 0.9))
                                 + language("rnd", ("primary", "secondary"), proportion=donor_proportion))
        for key in ("primary", "secondary"):
            del fixture.parts[key]
        fixture.parts["rnd:primary"] = (box(-3, 3, -5, 5, 0, 1), "")  # h/w 0.17, w/l 0.6
        fixture.parts["rnd:secondary"] = (box(-1, 1, -1, 1, 1, 1.2), "")
        fixture.parts["primer"] = (box(2, 4, -4, -2, 0, 1), "")
        fixture.parts["weld"] = (box(2, 6, 0, 4, 0, 1), "")
        return fixture

    def test_the_donors_proportions_judge_the_body(self):
        fixture = self.pirate()
        self.assertEqual(fixture.build().body_zone, "rnd:primary")
        self.assertEqual(fixture.failed(), [])
        self.assertEqual(self.pirate(donor_proportion=((0.5, 0.9), (0.55, 1.1))).failed(),
                         ["the body's proportions lie inside the language's ranges"])

    def test_the_donors_zones_are_required_and_coloured_from_the_donor(self):
        fixture = self.pirate()
        fixture.parts["rnd:secondary"] = ("", "")
        self.assertEqual(fixture.failed(), ["every zone of the language has geometry"])
        self.assertEqual(fixture.build().colours["rnd:primary"], fixture.build().languages["rnd"].colour("primary"))

    DONOR = "a donor is named exactly when the language has no proportions of its own"

    def test_a_pirate_hull_that_names_no_donor_is_refused(self):
        fixture = self.pirate(donor="")
        fixture.parts["lang"] = ("", language("pirate", ("primer", "weld"), body="", proportion=None,
                                             symmetry=("asymmetric", 0.9)))
        for key in ("rnd:primary", "rnd:secondary"):
            del fixture.parts[key]
        self.assertIn(self.DONOR, fixture.failed())

    def test_a_hull_whose_language_has_proportions_of_its_own_may_not_name_a_donor(self):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("zhestokost", "rnd"))
        fixture.parts["lang"] = ("", language() + language("rnd", ("primary", "secondary")))
        fixture.parts["rnd:primary"] = (box(-3, 3, -5, 5, 0, 1), "")
        fixture.parts["rnd:secondary"] = (box(-1, 1, -1, 1, 1, 1.2), "")
        self.assertIn(self.DONOR, fixture.failed())


class RefusalTests(unittest.TestCase):
    def test_a_source_that_includes_no_language_for_its_faction_is_refused(self):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("nobody"))
        with self.assertRaises(hs.HullSourceError):
            fixture.build()

    def test_anchors_that_repeat_an_id_or_name_an_unknown_role_are_refused_without_echoing_them(self):
        for anchors in (echo("anchor", [SECRET, "weapon-mount", [0, 0, 0], 0, ""]) * 2,
                        echo("anchor", [SECRET, "map-icon", [0, 0, 0], 0, ""])):
            fixture = Fixture()
            fixture.parts["anchors"] = ("", anchors)
            with self.assertRaises(hs.HullSourceError) as caught:
                fixture.build()
            self.assertNotIn(SECRET, str(caught.exception))

    def test_a_malformed_anchor_is_refused_without_echoing_it(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", [SECRET, "weapon-mount", [0, 0], 0, ""]))
        with self.assertRaises(hs.HullSourceError) as caught:
            fixture.build()
        self.assertNotIn(SECRET, str(caught.exception))

    def test_check_names_and_details_carry_no_hull_supplied_text(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", [SECRET, "weapon-mount", [0, 0, 3], 0, ""]))
        fixture.parts["emit:Th.A"] = ("", "")
        for check in fixture.build().checks:
            self.assertNotIn(SECRET, check.name + check.detail)

    def test_the_module_writes_no_json_and_starts_no_second_process(self):
        source = (Path(hs.__file__)).read_text(encoding="utf-8")
        for forbidden in ("json.dump", "ThreadPool", "concurrent.futures", "multiprocessing"):
            self.assertNotIn(forbidden, source)
        self.assertEqual(source.count("subprocess.Popen"), 1)


if __name__ == "__main__":
    unittest.main()
