"""Blender-free tests for aetheria_ships/hull_source.py, which reads a hull.scad through OpenSCAD and judges it against its
faction's form language. A fake runner answers each part with fixture OFF and ECHO text, so no OpenSCAD runs here; the
real OpenSCAD and the real language files are exercised by smoke_hull_source.py.

    python -m unittest discover -s tools/blender/tests -p "test_hull_source.py"

hull_source is loaded by path because the package __init__ imports bpy.
"""

import dataclasses
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

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


MAKER = "faction-record-key"


def language(faction="zhestokost", zones=("primary", "secondary"), body="primary", proportion=((0.25, 0.6), (0.35, 0.75)),
             symmetry=("bilateral", 0.97), maker=MAKER):
    palette = [[z, [0.1 * (i + 1), 0.2, 0.3]] for i, z in enumerate(zones)]
    return echo("lang", [faction, palette, body, [1, 0.6, 0.2], [0.5, 0.5, 0.5], [list(p) for p in proportion] if proportion
                         else [], list(symmetry), maker])


def brief(faction="zhestokost", donor="", length=(8, 14), triangles=(10, 100)):
    return echo("brief", [faction, donor, "tender", list(length), list(triangles), ["a read"]])


class Fixture:
    """A hull source as the fake runner sees it: part -> (OFF, ECHO). library holds the ECHO text of each faction's own
    language file; the hull's own `lang` echo is the files it includes, unless hull_lang says otherwise."""

    def __init__(self):
        self.library = {"zhestokost": language()}
        self.hull_lang = None
        self.hull_type = "Ship"  # HullData.HullType, which the caller reads from the package
        # Body 6 wide, 10 long, 2 high (h/w 0.33, w/l 0.6); a bilateral plate on top; one gun with a muzzle.
        self.parts = {
            "brief": ("", brief()),
            "primary": (box(-3, 3, -5, 5, 0, 2), ""),
            "secondary": (box(-1, 1, -1, 1, 2, 3), ""),
            "anchors": ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                        + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"])
                        + echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""])),
            "emit:Th.A": (box(0.5, 1.5, 5, 5.1, 0.5, 1.5), ""),
            "lines": ("", ""),
        }

    @staticmethod
    def projector(meshes):
        """A top view that is the bounding rectangle of every mesh it is given."""
        lows = [m.bounds()[0] for m in meshes]
        highs = [m.bounds()[1] for m in meshes]
        x0, y0 = min(v[0] for v in lows), min(v[1] for v in lows)
        x1, y1 = max(v[0] for v in highs), max(v[1] for v in highs)
        return [[(x0, y0), (x1, y0), (x1, y1), (x0, y1)]]

    def runner(self):
        return lambda part: ("", self.hull_lang if self.hull_lang is not None else "".join(self.library.values()))             if part == "lang" else self.parts[part]

    def read_library(self, faction):
        if faction not in self.library:
            raise hs.HullSourceError("no such language file")
        return self.library[faction]

    def build(self):
        return hs.build(self.runner(), self.read_library, self.hull_type, self.projector)

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

    def test_a_face_naming_the_vertex_one_past_the_last_is_refused(self):
        with self.assertRaises(hs.HullSourceError):
            hs.parse_off("\n".join(["OFF", "3 1 0", "0 0 0", "1 0 0", "0 1 0", "3 0 1 3"]))

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
                                            symmetry=("asymmetric", 0.9)))[0]
        self.assertEqual((pirate.body, pirate.proportion, pirate.symmetry), ("", None, ("asymmetric", 0.9)))

    def test_a_language_naming_an_unknown_rule_or_a_body_outside_its_zones_is_refused(self):
        with self.assertRaises(hs.HullSourceError):
            hs.read_languages(language(symmetry=("mostly", 0.5)))
        with self.assertRaises(hs.HullSourceError):
            hs.read_languages(language(body="keel"))


PROPORTION = "the body's proportions lie inside the language's ranges"
GRID = "the top view fits the layout grid"


def names(built):
    return [c.name for c in built.checks]


class HullTypeTests(unittest.TestCase):
    """A language's proportions judge Ship hulls only; every other rule, and the layout grid, judges every hull type."""

    @staticmethod
    def fixture(hull_type, **parts):
        fixture = Fixture()
        fixture.hull_type = hull_type
        fixture.parts.update({k: (v, "") for k, v in parts.items()})
        return fixture

    def test_a_station_or_turret_is_not_held_to_the_languages_proportions_and_lists_no_proportion_check(self):
        wide = box(-12, 12, -5, 5, 0, 2)  # h/w 0.08, w/l 2.4: outside both of Zhestokost's ranges
        for hull_type in ("Station", "Turret"):
            with self.subTest(hull_type):
                built = self.fixture(hull_type, primary=wide).build()
                self.assertEqual([c.name for c in built.failed()], [])
                self.assertNotIn(PROPORTION, names(built))
                self.assertEqual(built.hull_type, hull_type)
        self.assertEqual(self.fixture("Ship", primary=wide).failed(), [PROPORTION])

    def test_a_station_with_no_body_lists_no_proportion_check_but_a_ship_fails_it(self):
        self.assertNotIn(PROPORTION, names(self.fixture("Station", primary="").build()))
        ship = self.fixture("Ship", primary="").build()
        self.assertEqual([c.detail for c in ship.checks if c.name == PROPORTION], ["no body to judge"])

    def test_a_ship_lists_the_proportion_check_when_it_passes(self):
        built = self.fixture("Ship").build()
        self.assertEqual([c.ok for c in built.checks if c.name == PROPORTION], [True])

    def test_a_station_is_still_judged_on_symmetry_length_and_budget(self):
        self.assertEqual(self.fixture("Station", secondary=box(2, 6, -1, 1, 2, 3)).failed(),
                         ["the top view obeys the language's bilateral rule"])
        self.assertEqual(self.fixture("Station", primary=box(-3, 3, -10, 10, 0, 2)).failed(),
                         ["the length lies inside the brief's range"])
        station = self.fixture("Station")
        station.parts["brief"] = ("", brief(triangles=(10, 20)))
        self.assertEqual(station.failed(), ["the triangle count lies inside the brief's budget"])

    def test_every_hull_type_must_fit_the_layout_grid_at_thirty_two_cells_a_side(self):
        for hull_type in ("Ship", "Station", "Turret"):
            with self.subTest(hull_type):
                fits = self.fixture(hull_type, secondary=box(-32, 32, -1, 1, 2, 3)).build()
                self.assertEqual([(c.ok, c.detail) for c in fits.checks if c.name == GRID],
                                 [(True, "32 x 5 cells, at most 32 a side")])
                wide = self.fixture(hull_type, secondary=box(-33, 33, -1, 1, 2, 3))
                self.assertEqual(wide.failed(), [GRID])
                self.assertEqual([c.detail for c in wide.build().checks if c.name == GRID],
                                 ["33 x 5 cells, at most 32 a side"])
        long = self.fixture("Station", secondary=box(-1, 1, -32, 32, 2, 3))
        long.parts["brief"] = ("", brief(length=(8, 70)))
        self.assertEqual(long.failed(), [])
        self.assertEqual([c.detail for c in long.build().checks if c.name == GRID], ["3 x 32 cells, at most 32 a side"])
        long = self.fixture("Station", secondary=box(-1, 1, -33, 33, 2, 3))
        long.parts["brief"] = ("", brief(length=(8, 70)))
        self.assertEqual(long.failed(), [GRID])
        self.assertEqual([c.detail for c in long.build().checks if c.name == GRID], ["3 x 33 cells, at most 32 a side"])

    def test_the_grid_reads_zone_meshes_only_not_anchor_meshes(self):
        built = self.fixture("Station", **{"emit:Th.A": box(-35, 35, 5, 5.1, 0.5, 1.5)}).build()
        self.assertEqual([c.name for c in built.failed()], [])
        self.assertEqual([c.detail for c in built.checks if c.name == GRID], ["3 x 5 cells, at most 32 a side"])

    def test_a_hull_with_no_zone_geometry_fails_the_grid_check(self):
        built = self.fixture("Station", primary="", secondary="").build()
        self.assertEqual([(c.ok, c.detail) for c in built.checks if c.name == GRID], [(False, "no geometry")])

    def test_build_has_no_default_hull_type(self):
        fixture = Fixture()
        with self.assertRaises(TypeError):
            hs.build(fixture.runner(), fixture.read_library, projector=fixture.projector)

    def test_build_refuses_a_hull_type_that_is_not_a_hulltype_name_before_reading_the_source(self):
        for hull_type in ("ship", "SHIP", "Ship ", "", None, "Stations", 0, SECRET):
            with self.subTest(hull_type=hull_type):
                fixture, read = Fixture(), []
                runner = fixture.runner()
                with self.assertRaises(hs.HullSourceError) as caught:
                    hs.build(lambda part: read.append(part) or runner(part), fixture.read_library, hull_type, fixture.projector)
                self.assertEqual(read, [])
                self.assertNotIn(SECRET, str(caught.exception))

    def test_build_accepts_each_hulltype_name_and_only_a_ship_lists_the_proportion_check(self):
        for hull_type in ("Ship", "Station", "Turret"):
            with self.subTest(hull_type):
                built = self.fixture(hull_type).build()
                self.assertEqual([c.name for c in built.failed()], [])
                self.assertEqual(PROPORTION in names(built), hull_type == "Ship")


class MirrorOverlapTests(unittest.TestCase):
    @staticmethod
    def top(text):
        mesh = hs.parse_off(text)
        return hs._triangles_xy(mesh)

    def test_a_centred_box_overlaps_its_reflection_completely(self):
        self.assertEqual(hs.mirror_overlap(self.top(box(-2, 2, 0, 4, 0, 1))), 1.0)

    def test_nothing_overlaps_itself_completely(self):
        self.assertEqual(hs.mirror_overlap([]), 1.0)

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
        fixture.library["zhestokost"] = language(proportion=((0.8, 1.2), (0.1, 0.3)))
        self.assertEqual(fixture.failed(), ["the body's proportions lie inside the language's ranges"])

    def test_a_body_too_tall_for_the_language_fails_proportion_and_nothing_else(self):
        fixture = Fixture()
        fixture.parts["primary"] = (box(-3, 3, -5, 5, 0, 5), "")
        self.assertEqual(fixture.failed(), ["the body's proportions lie inside the language's ranges"])

    def test_a_body_too_wide_for_its_length_fails_proportion_and_nothing_else(self):
        fixture = Fixture()
        fixture.parts["primary"] = (box(-6, 6, -5, 5, 0, 4), "")  # h/w 0.33 but w/l 1.2
        self.assertEqual(fixture.failed(), ["the body's proportions lie inside the language's ranges"])

    def test_every_zone_of_the_language_is_coloured_with_its_own_palette_entry(self):
        built = Fixture().build()
        self.assertEqual(built.colours, {"primary": (0.1, 0.2, 0.3), "secondary": (0.2, 0.2, 0.3)})

    def test_a_body_too_narrow_for_the_language_fails_proportion(self):
        fixture = Fixture()
        fixture.parts["primary"] = (box(-1, 1, -5, 5, 0, 1), "")
        self.assertIn("the body's proportions lie inside the language's ranges", fixture.failed())

    def test_a_bilateral_language_refuses_a_lopsided_hull(self):
        fixture = Fixture()
        fixture.parts["secondary"] = (box(2, 6, -1, 1, 2, 3), "")
        self.assertEqual(fixture.failed(), ["the top view obeys the language's bilateral rule"])

    def test_a_bilateral_bound_is_met_by_exactly_that_overlap(self):
        fixture = Fixture()
        fixture.library["zhestokost"] = language(symmetry=("bilateral", 1.0))
        self.assertEqual(fixture.failed(), [])

    def test_an_asymmetric_language_refuses_a_symmetric_hull(self):
        fixture = Fixture()
        fixture.library["zhestokost"] = language("zhestokost", symmetry=("asymmetric", 0.9))
        self.assertEqual(fixture.failed(), ["the top view obeys the language's asymmetric rule"])

    def test_an_asymmetric_language_accepts_a_lopsided_hull(self):
        fixture = Fixture()
        fixture.library["zhestokost"] = language("zhestokost", symmetry=("asymmetric", 0.9))
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

    def test_the_budget_counts_the_triangles_of_the_emitter_meshes_too(self):
        # 12 + 12 triangles of zones, 12 of the emitter: a budget of 30 holds without the emitter and not with it
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief(triangles=(10, 30)))
        self.assertEqual(fixture.failed(), ["the triangle count lies inside the brief's budget"])

    def test_the_length_is_a_range_with_two_ends(self):
        for length in ((1, 5), (30, 40)):
            fixture = Fixture()
            fixture.parts["brief"] = ("", brief(length=length))
            self.assertEqual(fixture.failed(), ["the length lies inside the brief's range"], length)

    MUZZLE = "every weapon-mount has a muzzle and no muzzle is orphaned"

    def test_a_muzzle_facing_any_way_is_accepted(self):
        # Which way a gun points is HullData's hardpoint rotation; the hull source never judges it.
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                                    + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, 1, 3], 0, "Gun.A"])
                                    + echo("anchor", ["Gun.B", "weapon-mount", [0, 0, 3], 0, ""])
                                    + echo("anchor", ["Gun.B.muzzle", "weapon-muzzle", [1, 0, 3], 0, "Gun.B"])
                                    + echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""]))
        self.assertEqual(fixture.failed(), [])

    def test_a_mount_without_a_muzzle_and_a_muzzle_without_a_mount_are_refused(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])
        # Another gun's muzzle does not pair this one.
        fixture.parts["anchors"] = ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                                    + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"])
                                    + echo("anchor", ["Gun.B", "weapon-mount", [2, 0, 3], 0, ""]))
        self.assertEqual(fixture.failed(), [self.MUZZLE])

    def test_a_muzzle_whose_parent_is_not_a_weapon_mount_is_refused(self):
        fixture = Fixture()
        fixture.parts["anchors"] = ("", echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""])
                                    + echo("anchor", ["Th.A.muzzle", "weapon-muzzle", [1, 2, 1], 0, "Th.A"]))
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
        fixture.library = {"pirate": language("pirate", ("primer", "weld"), body="", proportion=None,
                                              symmetry=("asymmetric", 0.9)),
                           "rnd": language("rnd", ("primary", "secondary"), proportion=donor_proportion)}
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
        built = fixture.build()
        for zone in ("primary", "secondary"):
            self.assertEqual(built.colours[f"rnd:{zone}"], built.languages["rnd"].colour(zone))
        self.assertNotEqual(built.colours["rnd:primary"], built.colours["rnd:secondary"])

    DONOR = "a donor is named exactly when the language has no proportions of its own"

    def test_a_pirate_hull_that_names_no_donor_is_refused(self):
        fixture = self.pirate(donor="")
        del fixture.library["rnd"]
        for key in ("rnd:primary", "rnd:secondary"):
            del fixture.parts[key]
        self.assertIn(self.DONOR, fixture.failed())

    def test_a_hull_whose_language_has_proportions_of_its_own_may_not_name_a_donor(self):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("zhestokost", "rnd"))
        fixture.library["rnd"] = language("rnd", ("primary", "secondary"))
        fixture.parts["rnd:primary"] = (box(-3, 3, -5, 5, 0, 1), "")
        fixture.parts["rnd:secondary"] = (box(-1, 1, -1, 1, 1, 1.2), "")
        self.assertIn(self.DONOR, fixture.failed())


class LanguageOwnershipTests(unittest.TestCase):
    """The faction's language file owns its palette, proportions and symmetry rule: the hull's own `lang` echo is compared
    with it and never judged against."""

    OWN = "the hull includes its faction's language files and changes none of them"
    PROPORTIONS = "the body's proportions lie inside the language's ranges"

    def redefined(self, **language_arguments):
        fixture = Fixture()
        fixture.hull_lang = language(**language_arguments)
        return fixture

    def test_a_hull_that_includes_the_languages_unchanged_passes(self):
        self.assertEqual(Fixture().failed(), [])

    def test_a_hull_that_redefines_its_palette_fails_and_is_still_coloured_from_the_file(self):
        fixture = Fixture()
        fixture.hull_lang = echo("lang", ["zhestokost", [["primary", [1, 0, 1]], ["secondary", [1, 0, 1]]], "primary",
                                          [1, 0.6, 0.2], [0.5, 0.5, 0.5], [[0.25, 0.6], [0.35, 0.75]], ["bilateral", 0.97], MAKER])
        self.assertEqual(fixture.failed(), [self.OWN])
        built = fixture.build()
        self.assertEqual(built.colours["primary"], built.languages["zhestokost"].colour("primary"))
        self.assertNotEqual(built.colours["primary"], (1, 0, 1))

    def test_a_hull_that_adds_a_zone_to_its_palette_fails(self):
        self.assertEqual(self.redefined(zones=("primary", "secondary", "extra")).failed(), [self.OWN])

    def test_a_hull_that_redefines_the_proportions_fails_and_is_judged_by_the_file(self):
        fixture = self.redefined(proportion=((0.0, 9.0), (0.0, 9.0)))
        fixture.parts["primary"] = (box(-3, 3, -5, 5, 0, 5), "")  # too tall for the file's ranges
        self.assertEqual(sorted(fixture.failed()), sorted([self.OWN, self.PROPORTIONS]))

    def test_a_hull_that_redefines_the_symmetry_rule_fails_and_is_judged_by_the_file(self):
        fixture = self.redefined(symmetry=("asymmetric", 0.99))
        self.assertEqual(fixture.failed(), [self.OWN])
        fixture.parts["secondary"] = (box(2, 6, -1, 1, 2, 3), "")  # lopsided: the file's bilateral rule refuses it
        self.assertIn("the top view obeys the language's bilateral rule", fixture.failed())

    def test_a_hull_cannot_restate_its_languages_maker(self):
        """The language file owns its maker (the Faction record key Package compares with the product's): an included
        language echoed with another maker fails the includes-unchanged check, and the hull is read as the file's."""
        fixture = self.redefined(maker="another-faction-record-key")
        self.assertEqual(fixture.failed(), [self.OWN])
        self.assertEqual(fixture.build().languages["zhestokost"].maker, MAKER)

    def test_a_second_echo_of_the_language_after_the_first_fails(self):
        fixture = Fixture()
        fixture.hull_lang = language() + language(symmetry=("asymmetric", 0.99))
        self.assertEqual(fixture.failed(), [self.OWN])

    def test_the_genuine_language_echoed_after_an_altered_one_does_not_save_the_hull(self):
        fixture = Fixture()
        fixture.hull_lang = language(symmetry=("asymmetric", 0.99)) + language()
        self.assertEqual(fixture.failed(), [self.OWN])

    def test_a_hull_that_echoes_no_language_or_another_factions_fails(self):
        for text in ("", language("rnd")):
            fixture = Fixture()
            fixture.hull_lang = text
            self.assertEqual(fixture.failed(), [self.OWN])

    def test_a_pirate_hull_must_include_its_donors_language_too(self):
        fixture = DonorTests.pirate()
        fixture.hull_lang = language("pirate", ("primer", "weld"), body="", proportion=None, symmetry=("asymmetric", 0.9))
        self.assertEqual(fixture.failed(), [self.OWN])

    def test_the_library_is_asked_for_the_faction_and_the_donor_the_brief_names(self):
        asked = []
        fixture = DonorTests.pirate()
        hs.build(fixture.runner(), lambda faction: asked.append(faction) or fixture.read_library(faction), "Ship", fixture.projector)
        self.assertEqual(sorted(asked), ["pirate", "rnd"])


class FakeOpenScad:
    """Stands in for subprocess.Popen: records each command and environment, writes `off` where -o points, and answers
    with `output` and `returncode`."""

    def __init__(self, returncode=0, off="", output=""):
        self.returncode, self.off, self.output, self.calls = returncode, off, output, []
        self.pid = 4242

    def __call__(self, command, **kwargs):
        self.calls.append((command, kwargs.get("env")))
        if self.off:
            Path(command[command.index("-o") + 1]).write_text(self.off, encoding="utf-8")
        return self

    def communicate(self, timeout=None):
        return self.output, None

    def kill(self):
        pass


class LanguageLibraryTests(unittest.TestCase):
    def test_a_faction_name_that_is_not_a_plain_word_or_has_no_file_is_refused_without_a_process(self):
        with tempfile.TemporaryDirectory() as folder, mock.patch.object(hs.subprocess, "Popen") as popen:
            library = Path(folder) / "languages"
            library.mkdir()
            (library / "zhestokost.scad").write_text("", encoding="utf-8")
            (Path(folder) / "escape.scad").write_text("", encoding="utf-8")  # a file outside the library
            read = hs.language_library("openscad", library)
            for faction in ("../escape", "Zhestokost", "", "nobody", "zhestokost.scad"):
                with self.assertRaises(hs.HullSourceError) as caught:
                    read(faction)
                self.assertNotIn("nobody", str(caught.exception))
            popen.assert_not_called()

    def test_the_language_is_read_from_the_factions_own_file_run_alone(self):
        with tempfile.TemporaryDirectory() as folder:
            library = Path(folder)
            (library / "zhestokost.scad").write_text("", encoding="utf-8")
            openscad = FakeOpenScad(output=language())
            with mock.patch.object(hs.subprocess, "Popen", openscad):
                text = hs.language_library("openscad", library)("zhestokost")
            self.assertEqual(hs.read_languages(text)[0].faction, "zhestokost")
            command = openscad.calls[0][0]
            self.assertEqual(command[-1], str(library / "zhestokost.scad"))
            self.assertEqual(command[command.index("-D") + 1], 'part="lang"')


class RunnerTests(unittest.TestCase):
    def run_part(self, part, openscad, **arguments):
        with mock.patch.object(hs.subprocess, "Popen", openscad):
            return hs.openscad_runner("scad", "hull.scad", **arguments)(part)

    def test_openscad_is_asked_for_a_full_render_on_the_manifold_backend_of_one_part(self):
        openscad = FakeOpenScad()
        self.run_part("primary", openscad)
        command = openscad.calls[0][0]
        self.assertEqual(command[0], "scad")
        self.assertEqual(command[command.index("--backend") + 1], "Manifold")
        self.assertEqual(command[command.index("--render") + 1], "all")
        self.assertEqual(command[command.index("-D") + 1], 'part="primary"')
        self.assertEqual(command[-1], "hull.scad")

    def test_the_language_folder_is_what_openscad_searches_for_includes(self):
        openscad = FakeOpenScad()
        self.run_part("primary", openscad, library=Path("some") / "languages")
        self.assertEqual(openscad.calls[0][1]["OPENSCADPATH"], str(Path("some") / "languages"))

    def test_a_part_openscad_fails_to_make_is_refused_without_echoing_its_output(self):
        with self.assertRaises(hs.HullSourceError) as caught:
            self.run_part("primary", FakeOpenScad(returncode=1, output=SECRET))
        self.assertNotIn(SECRET, str(caught.exception))

    def test_echo_parts_answer_the_echo_and_geometry_parts_the_off(self):
        self.assertEqual(self.run_part("brief", FakeOpenScad(output=brief())), ("", brief()))
        self.assertEqual(self.run_part("primary", FakeOpenScad(off=box(0, 1, 0, 1, 0, 1), output="log")),
                         (box(0, 1, 0, 1, 0, 1), "log"))

    def test_each_process_is_reported_to_on_start(self):
        started = []
        self.run_part("primary", FakeOpenScad(), on_start=started.append)
        self.assertEqual(started, [4242])


class FingerprintTests(unittest.TestCase):
    def test_equal_values_give_equal_digests_and_a_change_gives_another(self):
        base = hs.fingerprint("a", (1.0, 2.5), [[0.0, 1.0, 2.0]], 3)
        self.assertEqual(base, hs.fingerprint("a", (1.0, 2.5), [[0.0, 1.0, 2.0]], 3))
        for other in (("b", (1.0, 2.5), [[0.0, 1.0, 2.0]], 3), ("a", (1.0, 2.501), [[0.0, 1.0, 2.0]], 3),
                      ("a", (1.0, 2.5), [[0.0, 1.0, 2.0]], 4), ("a", (1.0, 2.5), [[0.0, 1.0], [2.0]], 3)):
            self.assertNotEqual(base, hs.fingerprint(*other))

    def test_a_list_of_two_empty_lists_is_not_one_list_holding_one(self):
        self.assertNotEqual(hs.fingerprint([], []), hs.fingerprint([[]]))

    def test_float_noise_below_five_decimals_and_a_negative_zero_do_not_change_it(self):
        self.assertEqual(hs.fingerprint((1.0, 0.0)), hs.fingerprint((1.0 + 1e-8, -0.0)))
        self.assertEqual(hs.fingerprint((-1e-9,)), hs.fingerprint((0.0,)))


class RefusalTests(unittest.TestCase):
    def test_a_source_that_includes_no_language_for_its_faction_is_refused(self):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("nobody"))
        with self.assertRaises(hs.HullSourceError):
            fixture.build()

    def test_a_named_donor_that_has_no_language_file_is_refused(self):
        fixture = Fixture()
        fixture.parts["brief"] = ("", brief("zhestokost", "rnd"))
        with self.assertRaises(hs.HullSourceError):
            fixture.build()

    def test_a_language_file_that_echoes_another_faction_or_not_exactly_one_language_is_refused(self):
        for text in (language("rnd"), language() + language(), ""):
            fixture = Fixture()
            fixture.library["zhestokost"] = text
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

    def test_a_name_that_is_not_a_string_is_refused_wherever_the_source_gives_one(self):
        def refused(fixture):
            with self.assertRaises(hs.HullSourceError):
                fixture.build()

        for index in range(3):  # faction, donor, role
            fixture = Fixture()
            raw = ["zhestokost", "", "tender", [8, 14], [10, 100], ["a read"]]
            raw[index] = 7
            fixture.parts["brief"] = ("", echo("brief", raw))
            refused(fixture)
        fixture = Fixture()
        fixture.parts["brief"] = ("", echo("brief", ["zhestokost", "", "tender", [8, 14], [10, 100], [7]]))
        refused(fixture)
        for anchor in ([7, "weapon-mount", [0, 0, 3], 0, ""], ["Gun.A", 7, [0, 0, 3], 0, ""],
                       ["Gun.A", "weapon-mount", [0, 0, 3], 0, 7]):
            fixture = Fixture()
            fixture.parts["anchors"] = ("", echo("anchor", anchor))
            refused(fixture)
        for raw in ([7, [["primary", [1, 1, 1]]], "primary", [1, 1, 1], [1, 1, 1], [], ["bilateral", 1], MAKER],
                    ["zhestokost", [[7, [1, 1, 1]]], "primary", [1, 1, 1], [1, 1, 1], [], ["bilateral", 1], MAKER],
                    ["zhestokost", [["primary", [1, 1, 1]]], 7, [1, 1, 1], [1, 1, 1], [], ["bilateral", 1], MAKER]):
            with self.assertRaises(hs.HullSourceError):
                hs.read_languages(echo("lang", raw))

    def test_the_maker_is_the_eighth_element_of_a_language_echo(self):
        self.assertEqual(hs.read_languages(language(maker="K1"))[0].maker, "K1")
        raw = ["zhestokost", [["primary", [1, 1, 1]]], "primary", [1, 1, 1], [1, 1, 1], [], ["bilateral", 1]]
        for bad in (raw, raw + [""], raw + [7], raw + ["K1", "extra"]):  # seven elements, empty maker, not text, nine
            with self.assertRaises(hs.HullSourceError) as caught:
                hs.read_languages(echo("lang", bad))
            self.assertNotIn("K1", str(caught.exception))
        self.assertEqual(hs.read_languages(echo("lang", raw + ["K1"]))[0].maker, "K1")

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


LINES_OVER = "every kit line lies over the hull's top view"
LINES_DISTINCT = "no two kit lines are the same"
SILHOUETTE = "the top view yields a silhouette"


def line(layer="seam", points=((-1, -1, 2), (1, 1, 2)), cyclic=False):
    return echo("line", [layer, cyclic, [list(p) for p in points]])


class LineReadingTests(unittest.TestCase):
    def test_lines_are_read_in_echo_order_with_their_layer_flag_and_points(self):
        text = line("step", ((0, 0, 1), (1, 0, 1), (1, 1, 1)), True) + line("fitting")
        read = hs.read_lines(text)
        self.assertEqual([(l.layer, l.cyclic, len(l.points)) for l in read], [("step", True, 3), ("fitting", False, 2)])
        self.assertEqual(read[0].points[1], (1, 0, 1))

    def test_a_layer_outside_the_kit_layers_is_refused_without_echoing_it(self):
        for layer in ("silhouette", "outlines", "", SECRET):
            with self.subTest(layer=layer), self.assertRaises(hs.HullSourceError) as caught:
                hs.read_lines(line(layer))
            self.assertNotIn(SECRET, str(caught.exception))

    def test_a_line_of_one_point_or_a_point_that_is_not_finite_is_refused(self):
        for text in (line(points=((0, 0, 0),)), line(points=()), 'ECHO: line = ["seam", false, [[0, 0, NaN], [1, 1, 1]]]\n',
                     'ECHO: line = ["seam", false, [[0, 0, Infinity], [1, 1, 1]]]\n', line(points=((0, 0), (1, 1)))):
            with self.subTest(text=text), self.assertRaises(hs.HullSourceError):
                hs.read_lines(text)

    def test_a_cyclic_flag_that_is_not_a_boolean_is_refused(self):
        with self.assertRaises(hs.HullSourceError):
            hs.read_lines(echo("line", ["seam", 1, [[0, 0, 0], [1, 1, 1]]]))


SVG = """<?xml version="1.0" standalone="no"?>
<svg width="9mm" height="9mm" viewBox="0 -4 9 9" xmlns="http://www.w3.org/2000/svg" version="1.1">
<path d="
M 0,-4 L 4,-4 L 4,-1 L 0,-1 z
M 5,1 L 5.001,1 L 5.001,1.001 z
M -1e-05,2 L 2,2 L 2,3 L 0,3 z
" fill="lightgray" stroke="black" stroke-width="0.5"/>
</svg>
"""


class SvgContourTests(unittest.TestCase):
    def test_contours_come_back_in_model_coordinates_with_y_negated(self):
        contours = hs.parse_svg_contours(SVG)
        self.assertEqual(contours[0], [(0.0, 4.0), (4.0, 4.0), (4.0, 1.0), (0.0, 1.0)])
        self.assertEqual(contours[1][0], (-1e-05, -2.0))

    def test_a_contour_under_a_quarter_square_metre_is_a_sliver_and_dropped(self):
        self.assertEqual(len(hs.parse_svg_contours(SVG)), 2)

    def test_a_drawing_with_no_path_has_no_contours(self):
        self.assertEqual(hs.parse_svg_contours("<svg></svg>"), [])


class LineBuildTests(unittest.TestCase):
    def built(self, kit="", **parts):
        fixture = Fixture()
        fixture.parts["lines"] = ("", kit)
        fixture.parts.update(parts)
        return fixture, fixture.build()

    def test_a_source_with_no_lines_has_the_silhouette_alone_and_passes_every_check(self):
        _, built = self.built()
        self.assertEqual([(l.layer, l.cyclic) for l in built.lines], [("silhouette", True)])
        self.assertEqual(built.failed(), [])

    def test_the_silhouette_is_first_cyclic_and_at_the_top_of_the_geometry(self):
        _, built = self.built(line("outline", ((-1, 0, 1), (1, 0, 1))))
        self.assertEqual([l.layer for l in built.lines], ["silhouette", "outline"])
        self.assertEqual({p[2] for p in built.lines[0].points}, {3.0})
        self.assertEqual({p[:2] for p in built.lines[0].points}, {(-3.0, -5.0), (3.0, -5.0), (3.0, 5.0), (-3.0, 5.0)})

    def test_each_layer_carries_its_fixed_style(self):
        kit = "".join(line(layer) for layer in hs.KIT_LAYERS)
        _, built = self.built(kit)
        self.assertEqual([l.rgba() for l in built.lines], [hs.LINE_STYLE[l.layer] for l in built.lines])
        self.assertEqual(len({l.rgba() for l in built.lines}), 5)
        self.assertEqual(hs.LINE_STYLE["silhouette"], (1.0, 1.0, 1.0, 1.0))

    def test_the_projector_is_given_every_zone_mesh_that_has_geometry_and_no_anchor_mesh(self):
        fixture, given = Fixture(), []
        hs.build(fixture.runner(), fixture.read_library, "Ship",
                 lambda meshes: given.append(len(meshes)) or fixture.projector(meshes))
        self.assertEqual(given, [2])  # primary and secondary; the emitter mesh is not part of the layout's top view

    def test_a_kit_line_off_the_hull_fails_the_over_the_top_view_check_and_nothing_else(self):
        _, built = self.built(line(points=((0, 0, 2), (20, 0, 2))))
        self.assertEqual([c.name for c in built.failed()], [LINES_OVER])

    def test_a_kit_line_a_little_off_the_edge_still_lies_over_the_hull(self):
        _, built = self.built(line(points=((3.2, 0, 2), (3.2, 1, 2))))
        self.assertEqual(built.failed(), [])
        _, built = self.built(line(points=((3.4, 0, 2), (3.4, 1, 2))))
        self.assertEqual([c.name for c in built.failed()], [LINES_OVER])

    def test_the_inside_of_a_hole_in_the_silhouette_is_not_over_the_hull(self):
        hole = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
        outer = [(-3, -5), (3, -5), (3, 5), (-3, 5)]
        self.assertTrue(hs.over_silhouette((2, 0, 0), [outer, hole]))
        self.assertFalse(hs.over_silhouette((0, 0, 0), [outer, hole]))

    def test_two_equal_kit_lines_fail_the_distinct_check_and_nothing_else(self):
        _, built = self.built(line() + line("fitting"))
        self.assertEqual([c.name for c in built.failed()], [LINES_DISTINCT])

    def test_lines_that_differ_by_a_millimetre_or_more_are_distinct(self):
        _, built = self.built(line(points=((0, 0, 2), (1, 0, 2))) + line(points=((0, 0.002, 2), (1, 0, 2))))
        self.assertEqual(built.failed(), [])

    def test_no_contour_fails_the_silhouette_check(self):
        fixture = Fixture()
        built = hs.build(fixture.runner(), fixture.read_library, "Ship", lambda meshes: [])
        self.assertEqual([c.name for c in built.failed()], [SILHOUETTE])
        self.assertEqual(built.lines, [])

    def test_a_kit_line_with_no_silhouette_to_lie_over_fails_both_checks(self):
        fixture = Fixture()
        fixture.parts["lines"] = ("", line())
        built = hs.build(fixture.runner(), fixture.read_library, "Ship", lambda meshes: [])
        self.assertEqual(sorted(c.name for c in built.failed()), sorted([SILHOUETTE, LINES_OVER]))

    def test_a_source_with_no_geometry_asks_for_no_silhouette(self):
        fixture = Fixture()
        fixture.parts.update({"primary": ("", ""), "secondary": ("", ""), "emit:Th.A": ("", "")})
        built = hs.build(fixture.runner(), fixture.read_library, "Ship", lambda meshes: self.fail("projected nothing"))
        self.assertEqual(built.lines, [])

    def test_a_refused_line_is_refused_without_echoing_it(self):
        fixture = Fixture()
        fixture.parts["lines"] = ("", line(SECRET))
        with self.assertRaises(hs.HullSourceError) as caught:
            fixture.build()
        self.assertNotIn(SECRET, str(caught.exception))


class WrapperOpenScad(FakeOpenScad):
    """Keeps the wrapper scad and the OFF files OpenSCAD is given, which vanish with the temp folder."""

    def __call__(self, command, **kwargs):
        wrapper = Path(command[-1])
        self.wrapper = wrapper.read_text(encoding="utf-8")
        self.imports = {f.name: f.read_text(encoding="utf-8") for f in wrapper.parent.glob("*.off")}
        return super().__call__(command, **kwargs)


class ProjectorTests(unittest.TestCase):
    def project(self, openscad, meshes):
        with mock.patch.object(hs.subprocess, "Popen", openscad):
            return hs.openscad_projector("scad")(meshes)

    def test_the_meshes_are_unioned_projected_and_the_svg_read_back(self):
        a, b = hs.parse_off(box(0, 1, 0, 1, 0, 1)), hs.parse_off(box(2, 3, 0, 1, 0, 1))
        openscad = WrapperOpenScad(off=SVG)
        contours = self.project(openscad, [a, b])
        self.assertEqual(openscad.wrapper.split(), ["projection()", "union()", "{", 'import("m0.off");', 'import("m1.off");', "}"])
        self.assertEqual(sorted(openscad.imports), ["m0.off", "m1.off"])
        self.assertEqual(hs.parse_off(openscad.imports["m1.off"]).bounds(), b.bounds())
        command = openscad.calls[0][0]
        self.assertEqual(command[command.index("--backend") + 1], "Manifold")
        self.assertTrue(command[command.index("-o") + 1].endswith(".svg"))
        self.assertEqual(len(contours), 2)

    def test_a_failed_or_silent_openscad_is_refused_without_echoing_its_output(self):
        for openscad in (FakeOpenScad(returncode=1, output=SECRET), FakeOpenScad(output=SECRET)):
            with self.assertRaises(hs.HullSourceError) as caught:
                self.project(openscad, [hs.parse_off(box(0, 1, 0, 1, 0, 1))])
            self.assertNotIn(SECRET, str(caught.exception))

    def test_the_process_is_reported_to_on_start(self):
        started = []
        with mock.patch.object(hs.subprocess, "Popen", WrapperOpenScad(off=SVG)):
            hs.openscad_projector("scad", on_start=started.append)([hs.parse_off(box(0, 1, 0, 1, 0, 1))])
        self.assertEqual(started, [4242])

    def test_the_lines_part_answers_the_echo(self):
        with mock.patch.object(hs.subprocess, "Popen", FakeOpenScad(output=line())):
            self.assertEqual(hs.openscad_runner("scad", "hull.scad")("lines"), ("", line()))


class LineStyleTests(unittest.TestCase):
    def test_the_style_table_is_the_one_the_underlay_reads(self):
        self.assertEqual(hs.LINE_STYLE, {
            "silhouette": (1.0, 1.0, 1.0, 1.0), "outline": (1.0, 1.0, 1.0, 0.75), "fitting": (1.0, 1.0, 1.0, 0.6),
            "step": (1.0, 1.0, 1.0, 0.5), "seam": (1.0, 1.0, 1.0, 0.35)})
        self.assertEqual(hs.KIT_LAYERS, ("outline", "step", "seam", "fitting"))

    def test_a_schematic_line_cannot_be_changed_after_it_is_read(self):
        read = hs.read_lines(line())[0]
        with self.assertRaises(dataclasses.FrozenInstanceError):
            read.layer = "step"

    def test_the_four_echo_parts_are_written_as_echo_files_and_geometry_parts_as_off(self):
        for part, suffix in (("brief", ".echo"), ("lang", ".echo"), ("anchors", ".echo"), ("lines", ".echo"), ("primary", ".off")):
            with self.subTest(part=part):
                openscad = FakeOpenScad()
                with mock.patch.object(hs.subprocess, "Popen", openscad):
                    hs.openscad_runner("scad", "hull.scad")(part)
                command = openscad.calls[0][0]
                self.assertTrue(command[command.index("-o") + 1].endswith(suffix))


class LineRefusalTextTests(unittest.TestCase):
    def refusal(self, text):
        with self.assertRaises(hs.HullSourceError) as caught:
            hs.read_lines(text)
        return str(caught.exception)

    def test_each_refusal_says_one_fixed_thing(self):
        self.assertEqual(self.refusal(echo("line", ["seam", False])), "A schematic line has the wrong shape")
        self.assertEqual(self.refusal(echo("line", ["seam", 1, []])), "A schematic line has the wrong shape")
        self.assertEqual(self.refusal(line("nope")), "A schematic line names a layer a kit does not draw")
        self.assertEqual(self.refusal(line(points=((0, 0, 0),))), "A schematic line has fewer than two points")
        self.assertEqual(self.refusal('ECHO: line = ["seam", false, [[0, 0, NaN], [1, 1, 1]]]\n'),
                         "A schematic line has a point that is not a finite number")


class ContourAreaTests(unittest.TestCase):
    @staticmethod
    def svg(*rings):
        paths = "\n".join("M " + " L ".join(f"{x},{-y}" for x, y in ring) + " z" for ring in rings)
        return f'<svg><path d="\n{paths}\n" fill="lightgray"/></svg>'

    def test_a_contour_is_kept_from_a_quarter_square_metre_whichever_way_it_winds_or_where_it_lies(self):
        square = [(3, 7), (3.5, 7), (3.5, 7.5), (3, 7.5)]
        triangle = [(10, 10), (11, 10), (10, 11)]
        for ring in (square, square[::-1], triangle, triangle[::-1]):
            with self.subTest(ring=ring):
                self.assertEqual(len(hs.parse_svg_contours(self.svg(ring))), 1)

    def test_a_contour_just_under_a_quarter_square_metre_is_dropped(self):
        for ring in ([(3, 7), (3.6, 7), (3.6, 7.4), (3, 7.4)], [(0, 0), (1, 0), (0, 0.49)]):
            with self.subTest(ring=ring):
                self.assertEqual(hs.parse_svg_contours(self.svg(ring)), [])

    def test_a_contour_of_fewer_than_three_points_is_dropped(self):
        self.assertEqual(hs.parse_svg_contours(self.svg([(0, 0), (9, 9)])), [])


class OverSilhouetteTests(unittest.TestCase):
    TRIANGLE = [(0.0, 0.0), (10.0, 0.0), (0.0, 10.0)]

    def over(self, x, y, contours=None, z=99.0):
        return hs.over_silhouette((x, y, z), [self.TRIANGLE] if contours is None else contours)

    def test_inside_and_far_outside(self):
        self.assertTrue(self.over(2, 2))
        self.assertFalse(self.over(8, 8))
        self.assertFalse(self.over(-5, 3))

    def test_the_height_of_a_point_does_not_matter(self):
        self.assertTrue(self.over(2, 2, z=-50.0))
        self.assertFalse(self.over(8, 8, z=2.0))

    def test_a_point_outside_but_within_the_tolerance_of_a_slanted_edge_is_over_the_hull(self):
        # the hypotenuse x + y = 10; (5.1, 5.1) is 0.14 m outside it, (5.3, 5.3) is 0.42 m outside
        self.assertTrue(self.over(5.1, 5.1))
        self.assertFalse(self.over(5.3, 5.3))

    def test_a_point_beyond_the_end_of_an_edge_is_measured_to_its_end(self):
        self.assertTrue(self.over(10.25, 0))
        self.assertFalse(self.over(10.35, 0))
        self.assertTrue(self.over(-0.25, -0.0))
        self.assertFalse(self.over(0, -0.35))

    def test_a_ray_through_a_vertex_counts_the_edge_once(self):
        diamond = [(0.0, -2.0), (2.0, 0.0), (0.0, 2.0), (-2.0, 0.0)]
        self.assertTrue(self.over(0.5, 0, [diamond]))
        self.assertTrue(self.over(-0.5, 0, [diamond]))
        self.assertFalse(self.over(3.0, 0, [diamond]))
        self.assertFalse(self.over(0.0, 3.0, [diamond]))

    def test_the_even_odd_rule_alternates_through_nested_contours(self):
        rings = [[(-s, -s), (s, -s), (s, s), (-s, s)] for s in (10.0, 7.0, 4.0, 1.0)]
        self.assertTrue(self.over(8.5, 0, rings))     # in the outer ring only
        self.assertFalse(self.over(5.5, 0, rings))    # a hole
        self.assertTrue(self.over(2.5, 0, rings))     # island in the hole

    def test_three_nested_contours_put_the_centre_back_on_the_hull(self):
        rings = [[(-s, -s), (s, -s), (s, s), (-s, s)] for s in (10.0, 6.0, 2.0)]
        self.assertTrue(self.over(0.0, 0.0, rings))


class ProcessRefusalTests(unittest.TestCase):
    def test_openscad_that_cannot_start_is_refused_in_fixed_words(self):
        with mock.patch.object(hs.subprocess, "Popen", side_effect=OSError(SECRET)):
            for make in (lambda: hs.openscad_runner("scad", "hull.scad")("primary"),
                         lambda: hs.openscad_projector("scad")([hs.parse_off(box(0, 1, 0, 1, 0, 1))])):
                with self.assertRaises(hs.HullSourceError) as caught:
                    make()
                self.assertEqual(str(caught.exception), "OpenSCAD could not be started")

    def test_openscad_that_does_not_finish_is_killed_and_refused_in_fixed_words(self):
        class Slow(FakeOpenScad):
            killed = False

            def communicate(self, timeout=None):
                if not self.killed:
                    raise hs.subprocess.TimeoutExpired("scad", timeout)
                return "", None

            def kill(self):
                self.killed = True

        slow = Slow()
        with mock.patch.object(hs.subprocess, "Popen", slow):
            with self.assertRaises(hs.HullSourceError) as caught:
                hs.openscad_runner("scad", "hull.scad")("primary")
        self.assertEqual((str(caught.exception), slow.killed), ("OpenSCAD did not finish a part in time", True))

    def test_a_projection_openscad_never_wrote_is_refused(self):
        with mock.patch.object(hs.subprocess, "Popen", FakeOpenScad()):
            with self.assertRaises(hs.HullSourceError) as caught:
                hs.openscad_projector("scad")([hs.parse_off(box(0, 1, 0, 1, 0, 1))])
        self.assertEqual(str(caught.exception), "OpenSCAD refused a part of the hull source")

    def test_a_refusing_openscad_says_so_in_fixed_words(self):
        with mock.patch.object(hs.subprocess, "Popen", FakeOpenScad(returncode=2)):
            with self.assertRaises(hs.HullSourceError) as caught:
                hs.openscad_runner("scad", "hull.scad")("primary")
        self.assertEqual(str(caught.exception), "OpenSCAD refused a part of the hull source")


class LineCheckDetailTests(unittest.TestCase):
    def details(self, kit, projector=Fixture.projector):
        fixture = Fixture()
        fixture.parts["lines"] = ("", kit)
        built = hs.build(fixture.runner(), fixture.read_library, "Ship", projector)
        return {c.name: c.detail for c in built.checks}

    def test_the_details_count_what_the_checks_found(self):
        got = self.details(line(points=((0, 0, 2), (20, 0, 2))) + line("step") + line("step"))
        self.assertEqual((got[SILHOUETTE], got[LINES_OVER], got[LINES_DISTINCT]), ("1 contours", "1 outside", "1 repeated"))

    def test_a_silhouette_is_not_a_kit_line_so_two_equal_contours_are_not_a_repeat(self):
        twice = lambda meshes: Fixture.projector(meshes) * 2
        got = self.details("", twice)
        self.assertEqual((got[SILHOUETTE], got[LINES_DISTINCT]), ("2 contours", "0 repeated"))
        fixture = Fixture()
        built = hs.build(fixture.runner(), fixture.read_library, "Ship", twice)
        self.assertEqual(built.failed(), [])


class BytesOpenScad(FakeOpenScad):
    """Writes raw bytes where -o points: an SVG with a byte that is not UTF-8 in its title."""

    def __call__(self, command, **kwargs):
        self.calls.append((command, kwargs.get("env")))
        Path(command[command.index("-o") + 1]).write_bytes(bytes([255]) + SVG.encode("utf-8"))
        return self


class SilhouetteReadTests(unittest.TestCase):
    def test_a_drawing_with_a_byte_that_is_not_utf8_still_reads(self):
        with mock.patch.object(hs.subprocess, "Popen", BytesOpenScad()):
            self.assertEqual(len(hs.openscad_projector("scad")([hs.parse_off(box(0, 1, 0, 1, 0, 1))])), 2)

    def test_a_number_that_is_not_a_number_is_refused_in_fixed_words(self):
        with self.assertRaises(hs.HullSourceError) as caught:
            hs.parse_svg_contours('<svg><path d="M .,. L 1,1 L 2,2 z"/></svg>')
        self.assertEqual(str(caught.exception), "OpenSCAD wrote a silhouette that is not readable")


if __name__ == "__main__":
    unittest.main()
