#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 0 (one evaluation path).

Cut 0 collapses three disagreeing PerformanceStat.Evaluate bodies (ItemManager's unequipped read,
ConsumableItemEffect's, EquippedItem's) into one shared function, PerformanceStat.Evaluate(IStatContext),
fed by three IStatContext implementations. This script mutates each context's *identity* factor -- the
constant it returns instead of computing a term it structurally cannot have -- and asserts the named
test goes red for exactly that reason. A no-op control mutation proves the read/replace/restore path is
transparent.

Usage:
    python tests/mutation_tests_stats_power_cut0.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

Only touches files under this repository; every mutation is reversed before the script exits, including
on failure.
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
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="        public float HeatFactor(PerformanceStat stat) => 1f;",
        mutated="        public float HeatFactor(PerformanceStat stat) => 1f;",
        test="LoadoutTests.UnequippedIgnoresHeatRegardlessOfTemperature",
        expect="green",
    ),
    # --- Unequipped has no heat: UnequippedStatContext.HeatFactor is the fixed identity, not a real read ---
    Mutation(
        rule="unequipped must ignore heat (HeatFactor is the identity 1, not a computed value)",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="        public float HeatFactor(PerformanceStat stat) => 1f;",
        mutated="        public float HeatFactor(PerformanceStat stat) => .5f;",
        test="LoadoutTests.UnequippedIgnoresHeatRegardlessOfTemperature",
        expect="red",
    ),
    # --- Unequipped has no modifiers: ConstantModifier is the fixed identity 0 ---
    Mutation(
        rule="unequipped must ignore modifiers (ConstantModifier is the fixed identity 0)",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="        public float ConstantModifier(PerformanceStat stat) => 0f;",
        mutated="        public float ConstantModifier(PerformanceStat stat) => 0.1f;",
        test="LoadoutTests.UnequippedAgreesWithEquippedAtFullHealthAndOptimalTemperature",
        expect="red",
    ),
    # --- Consumable substitutes progress for heat unconditionally, not exponentiated by the stat's field ---
    Mutation(
        rule="consumable heat must not be exponentiated by HeatExponentMultiplier (that is the equipped shape)",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor=(
            "    public float HeatFactor(PerformanceStat stat) =>\n"
            "        Data.Effectiveness.Evaluate((Data.Duration - RemainingDuration) / Data.Duration);"
        ),
        mutated=(
            "    public float HeatFactor(PerformanceStat stat) =>\n"
            "        pow(Data.Effectiveness.Evaluate((Data.Duration - RemainingDuration) / Data.Duration), stat.HeatExponentMultiplier);"
        ),
        test="LoadoutTests.ConsumableEvaluateSubstitutesProgressForHeatUnconditionally",
        expect="red",
    ),
]


def replace_unique(path: Path, anchor: str, mutated: str) -> None:
    original = path.read_text(encoding="utf-8")
    count = original.count(anchor)
    if count != 1:
        raise SystemExit(f"{path}: anchor must occur exactly once, found {count}\n---\n{anchor}")
    path.write_text(original.replace(anchor, mutated), encoding="utf-8")


def run_test(cultlib_root: str, test: str) -> tuple[int, str]:
    proc = subprocess.run(
        ["dotnet", "test", str(TEST_PROJECT), f"-p:CultLibRoot={cultlib_root}",
         "--filter", f"FullyQualifiedName~{test}"],
        capture_output=True, text=True, timeout=300,
    )
    return proc.returncode, proc.stdout + proc.stderr


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cultlib-root", required=True)
    args = parser.parse_args()

    failures: list[str] = []
    originals: dict[str, str] = {}
    for m in MUTATIONS:
        path = REPO_ROOT / m.file
        if m.file not in originals:
            originals[m.file] = path.read_text(encoding="utf-8")

    def restore_all() -> None:
        for file, text in originals.items():
            (REPO_ROOT / file).write_text(text, encoding="utf-8")

    try:
        for m in MUTATIONS:
            print(f"\n=== {m.rule} (expect {m.expect}) ===")
            path = REPO_ROOT / m.file
            replace_unique(path, m.anchor, m.mutated)
            code, output = run_test(args.cultlib_root, m.test)
            passed = "Passed!" in output and code == 0
            if m.expect == "green":
                if not passed:
                    failures.append(f"{m.rule}: expected green (test passing), got failure\n{output[-2000:]}")
                else:
                    print(f"OK: {m.test} passed under a no-op mutation, as expected")
            else:
                if passed:
                    failures.append(f"{m.rule}: expected red (mutant killed), but {m.test} still passed")
                else:
                    print(f"OK: {m.test} went red under the mutation, as expected")
            # Restore this file immediately so the next mutation starts from a clean tree.
            path.write_text(originals[m.file], encoding="utf-8")
    finally:
        restore_all()

    if failures:
        print("\n=== FAILURES ===")
        for f in failures:
            print(f)
        return 1

    print("\nAll mutation cases behaved as Cut 0's verification section demands.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
