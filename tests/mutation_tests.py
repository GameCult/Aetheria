#!/usr/bin/env python3
"""Mutation tests for the Cut B provenance rules (docs/item-provenance-cut.md).

For each rule this applies its named mutation to an exact, unique anchor of
source text, runs the one test that should catch it, restores the file
byte-for-byte, and reports whether the mutant was killed. A no-op control
mutation (anchor == replacement) proves the read/replace/write/restore path
itself is transparent: every test it touches must stay green.

Usage:
    python tests/mutation_tests.py --cultlib-root <path-to-CultLib-a0813c6-worktree>

Requires the CultLib worktree described in docs/item-provenance-cut.md section 6.
Only touches files under this repository; every mutation is reversed before
the script exits, including on failure.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TEST_PROJECT = REPO_ROOT / "tests" / "Aetheria.Shared.Tests"


@dataclass
class Mutation:
    rule: str
    file: str
    anchor: str
    mutated: str
    test: str
    expect: str  # "red" (mutant must be killed) or "green" (the no-op control)


MUTATIONS: list[Mutation] = [
    # --- No-op control: proves the anchor/replace/restore mechanism is transparent ---
    Mutation(
        rule="control (no-op round trip)",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor='    [Key(0)] public int NextLot = 1;',
        mutated='    [Key(0)] public int NextLot = 1;',
        test="RunSaveTests.MissingLotIsLoud",
        expect="green",
    ),
    # --- MissingLotIsLoud: the indexer and Reachable must throw on an absent lot ---
    Mutation(
        rule="ProvenanceLedger indexer throws on an absent id",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor=(
            "    public Lot this[int id]\n"
            "    {\n"
            "        get\n"
            "        {\n"
            "            if (!Lots.TryGetValue(id, out var lot))\n"
            "                throw new InvalidOperationException($\"Lot {id} is not in the provenance ledger.\");\n"
            "            return lot;\n"
            "        }\n"
            "    }"
        ),
        mutated=(
            "    public Lot this[int id]\n"
            "    {\n"
            "        get\n"
            "        {\n"
            "            Lots.TryGetValue(id, out var lot);\n"
            "            return lot;\n"
            "        }\n"
            "    }"
        ),
        test="RunSaveTests.MissingLotIsLoud",
        expect="red",
    ),
    Mutation(
        rule="Reachable throws on a missing root instead of skipping it",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor="            var lot = this[id];\n            reached[id] = lot;",
        mutated="            if (!Lots.TryGetValue(id, out var lot)) continue;\n            reached[id] = lot;",
        test="RunSaveTests.MissingLotIsLoud",
        expect="red",
    ),
    # --- MatchingHardpointsShareALot: the second unit must mint from the first unit's lot ---
    Mutation(
        rule="matching hardpoints share a lot, not a fresh mint",
        file="Assets/Scripts/ServerShared/LoadoutGenerator.cs",
        anchor=(
            "                    // Matching hardpoints carry matching units: same lot\n"
            "                    var item = (previousItem != null\n"
            "                        ? ItemManager.CreateInstance(previousItem.EquippableItem.Lot)\n"
            "                        : ItemManager.CreateInstance(entry.product)) as EquippableItem;"
        ),
        mutated=(
            "                    // Matching hardpoints carry matching units: same lot\n"
            "                    var item = ItemManager.CreateInstance(entry.product) as EquippableItem;"
        ),
        test="LoadoutTests.MatchingHardpointsShareALot",
        expect="red",
    ),
    # --- FirstAvailableProductInKeyOrderBuildsTheSlot: Brand and Materialize must both respect key order ---
    Mutation(
        rule="Brand matches the lot's maker, not just the design",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor=(
            "        var product = ItemData.GetAll<FactionProductData>()\n"
            "            .Where(p => p.Manufacturer.Key.Equals(maker.Key) && p.Design.Key.Equals(item.Data.Key))"
        ),
        mutated=(
            "        var product = ItemData.GetAll<FactionProductData>()\n"
            "            .Where(p => p.Design.Key.Equals(item.Data.Key))"
        ),
        test="LoadoutTests.FirstAvailableProductInKeyOrderBuildsTheSlot",
        expect="red",
    ),
    Mutation(
        rule="Materialize resolves a design's product in record-key order",
        file="Assets/Scripts/ServerShared/Loadout.cs",
        anchor="var products = cache.GetAll<FactionProductData>().OrderBy(p => cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();",
        mutated="var products = cache.GetAll<FactionProductData>().ToArray();",
        test="LoadoutTests.FirstAvailableProductInKeyOrderBuildsTheSlot",
        expect="red",
    ),
    # --- StatsReadTheLot: stats must read the lot, including its per-role fills ---
    Mutation(
        rule="Evaluate reads the lot's per-role fill for a role stat",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="var quality = pow(lot.QualityForRole(stat.FromRole), stat.QualityExponent);",
        mutated="var quality = pow(lot.Quality, stat.QualityExponent);",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    Mutation(
        rule="QualityForRole falls back to the lot's own workmanship, not a fixed value",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor="        if (string.IsNullOrEmpty(role) || Roles == null) return Quality;",
        mutated="        if (string.IsNullOrEmpty(role) || Roles == null) return 1f;",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
]


def run_test(cultlib_root: str, filter_expr: str) -> tuple[bool, str]:
    proc = subprocess.run(
        [
            "dotnet", "test", str(TEST_PROJECT),
            f"-p:CultLibRoot={cultlib_root}",
            "--filter", f"FullyQualifiedName~{filter_expr}",
            "--nologo",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
    )
    output = proc.stdout + proc.stderr
    passed = proc.returncode == 0 and "Failed!" not in output
    return passed, output


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cultlib-root", required=True)
    args = parser.parse_args()

    results = []
    failures = []

    for m in MUTATIONS:
        path = REPO_ROOT / m.file
        original = path.read_bytes()
        text = original.decode("utf-8")
        count = text.count(m.anchor)
        if count != 1:
            print(f"[ABORT] anchor for '{m.rule}' matches {count} times in {m.file}, expected exactly 1")
            failures.append(m.rule)
            continue

        mutated_text = text.replace(m.anchor, m.mutated, 1)
        path.write_bytes(mutated_text.encode("utf-8"))
        try:
            passed, output = run_test(args.cultlib_root, m.test)
        finally:
            # Reverse write: restore the exact original bytes regardless of outcome.
            path.write_bytes(original)
            restored = path.read_bytes()
            if restored != original:
                print(f"[FATAL] {m.file} did not restore byte-exact after '{m.rule}'!")
                return 2

        if m.expect == "red":
            killed = not passed
            status = "KILLED" if killed else "SURVIVED (bad)"
            if not killed:
                failures.append(m.rule)
        else:  # control: must stay green
            killed = passed
            status = "GREEN (ok)" if passed else "RED (bad: round trip corrupted something)"
            if not passed:
                failures.append(m.rule)

        results.append((m.rule, m.test, status))
        print(f"{status:22} | {m.test:55} | {m.rule}")

    print()
    print("Summary:")
    for rule, test, status in results:
        print(f"  {status:22} {test:55} {rule}")

    if failures:
        print(f"\n{len(failures)} mutation(s) did not behave as expected:")
        for f in failures:
            print(f"  - {f}")
        return 1

    print(f"\nAll {len(results)} mutations behaved as expected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
