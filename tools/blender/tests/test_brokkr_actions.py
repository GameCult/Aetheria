"""Blender-free test of aetheria_ships/brokkr_actions.json, the host file Brokkr reads: agents may open and dry-run a
ship; every operator that writes game data or applies lines and visuals is absent (human-only is absence).

    python -m unittest discover -s tools/blender/tests
"""

import json
import re
import unittest
from pathlib import Path

FILE = Path(__file__).resolve().parents[1] / "aetheria_ships" / "brokkr_actions.json"


class HostActionFileTests(unittest.TestCase):
    def setUp(self):
        self.document = json.loads(FILE.read_text(encoding="utf-8"))
        self.rows = {row["name"]: row for row in self.document["actions"]}

    def test_lists_exactly_open_and_push(self):
        self.assertEqual("brokkr.blender.host_actions.v0", self.document["schema"])
        self.assertEqual({"aetheria.open_ship", "aetheria.push_ship"}, set(self.rows))
        self.assertEqual(2, len(self.document["actions"]))

    def test_push_changes_nothing_and_returns_its_result_property(self):
        push = self.rows["aetheria.push_ship"]
        self.assertIs(False, push["changesScene"])
        self.assertEqual("aetheria_push_result", push["result"])

    def test_open_is_one_undo_step_refused_in_edit_mode(self):
        opened = self.rows["aetheria.open_ship"]
        self.assertIs(True, opened["changesScene"])
        self.assertIs(False, opened["editModeSafe"])

    def test_no_row_names_a_human_only_operator(self):
        self.assertIsNone(re.search(r"apply|package|capture|new_ship|bind", FILE.read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
