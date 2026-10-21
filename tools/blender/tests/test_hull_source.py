"""Blender-free tests for aetheria_ships/hull_source.py, which reads a hull.scad through OpenSCAD and judges it against its
faction's form language. A fake runner answers each part with fixture OFF and ECHO text, so no OpenSCAD runs here; the
real OpenSCAD and the real language files are exercised by smoke_hull_source.py.

    python -m unittest discover -s tools/blender/tests -p "test_hull_source.py"

hull_source is loaded by path because the package __init__ imports bpy.
"""

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
        # Body 6 wide, 10 long, 2 high (h/w 0.33, w/l 0.6); a bilateral plate on top; one gun with a muzzle.
        self.parts = {
            "brief": ("", brief()),
            "primary": (box(-3, 3, -5, 5, 0, 2), ""),
            "secondary": (box(-1, 1, -1, 1, 2, 3), ""),
            "anchors": ("", echo("anchor", ["Gun.A", "weapon-mount", [0, 0, 3], 0, ""])
                        + echo("anchor", ["Gun.A.muzzle", "weapon-muzzle", [0, -3, 3], 0, "Gun.A"])
                        + echo("anchor", ["Th.A", "thruster-emitter", [1, 5, 1], 180, ""])),
            "emit:Th.A": (box(0.5, 1.5, 5, 5.1, 0.5, 1.5), ""),
        }

    def runner(self):
        return lambda part: ("", self.hull_lang if self.hull_lang is not None else "".join(self.library.values()))             if part == "lang" else self.parts[part]

    def read_library(self, faction):
        if faction not in self.library:
            raise hs.HullSourceError("no such language file")
        return self.library[faction]

    def build(self):
        return hs.build(self.runner(), self.read_library)

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
        hs.build(fixture.runner(), lambda faction: asked.append(faction) or fixture.read_library(faction))
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


if __name__ == "__main__":
    unittest.main()
