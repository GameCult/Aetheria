"""Headless check of the built Twemoji.ttf's GSUB: every ligature record that has FE0F right after its first glyph
has a twin without FE0F pointing at the same ligature glyph, and the original records are all still there.
Run: py -3 -m pip install fonttools==4.66.1, then py -3 tools/emoji/test_fe0f_twins.py (reads the committed font)."""
import sys, unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fontTools.ttLib import TTFont
import fetch_emoji_font as build

FONT = build.FONT_DIR / "Twemoji.ttf"
FE0F_RECORDS = 15  # Twemoji v17.0.3's records with FE0F as the second glyph
UPSTREAM_RECORDS = 2582  # the font's ligature records before any twin


def records(font):
    found = []
    for lookup in font["GSUB"].table.LookupList.Lookup:
        for sub in lookup.SubTable:
            sub = getattr(sub, "ExtSubTable", sub)
            for first, ligatures in getattr(sub, "ligatures", {}).items():
                found += [(first, tuple(l.Component), l.LigGlyph) for l in ligatures]
    return found


class Fe0fTwins(unittest.TestCase):
    def setUp(self):
        self.font = TTFont(FONT)
        self.fe0f = self.font.getBestCmap()[0xFE0F]
        self.records = records(self.font)

    def test_every_fe0f_record_has_a_twin_and_keeps_its_original(self):
        originals = [r for r in self.records if r[1][:1] == (self.fe0f,)]
        self.assertEqual(len(originals), FE0F_RECORDS)
        have = set(self.records)
        for first, comps, glyph in originals:
            twin = tuple(c for c in comps if c != self.fe0f)
            self.assertIn((first, twin, glyph), have, f"no twin for {first} {comps}")
        self.assertEqual(len(self.records), UPSTREAM_RECORDS + FE0F_RECORDS)

    def test_adding_twins_adds_exactly_the_twins_and_is_idempotent(self):
        def strip(comps):
            return tuple(c for c in comps if c != self.fe0f)

        originals = {r for r in self.records if r[1][:1] == (self.fe0f,)}
        twins = {(f, strip(c), g) for f, c, g in originals}
        upstream = set(self.records) - twins  # the font as nanoemoji wrote it, with or without twins present
        for lookup in self.font["GSUB"].table.LookupList.Lookup:
            for sub in lookup.SubTable:
                sub = getattr(sub, "ExtSubTable", sub)
                for first, ligatures in getattr(sub, "ligatures", {}).items():
                    ligatures[:] = [l for l in ligatures if (first, tuple(l.Component), l.LigGlyph) not in twins]
        self.assertEqual(build.add_fe0f_less_twins(self.font), FE0F_RECORDS)
        self.assertEqual(set(records(self.font)), upstream | twins)
        self.assertEqual(build.add_fe0f_less_twins(self.font), 0)


if __name__ == "__main__":
    unittest.main()
