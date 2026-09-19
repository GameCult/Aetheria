#!/usr/bin/env python3
"""Mutation tests for Cut 8, the operator's condition-ratio ask (2026-09-19): "you can't currently tell when
[a thruster is] busted -- the particle system it spawns responds only to thrust intent. Should multiply that by
actual performance." This follows Cut 7's brownout work (mutation_tests_stats_power_cut7.py) in this repo's own
numbering.

EquippedItem.ConditionRatio (Entity.cs) is the one number that answers "how healthy does this item actually
look": the item's resolved value for a stat against what the same item, same lot, same modifier stack would
produce with Heat/Durability/PowerSupply pinned to their identity (NominalContext, the private struct right
below it). Thruster.Condition and AetherDrive.Condition (Thruster.cs/AetherDrive.cs) are that ratio read against
each behaviour's own governing stat. This script mutates each rule's own changed line and asserts the matching
ConditionRatioTests.cs Fact goes red for exactly that reason. A no-op control mutation proves the
read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_condition_ratio_cut8.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

Only touches files under this repository; every mutation is reversed before the script exits, including on
failure.
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
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float ConditionRatio(PerformanceStat stat) => saturate(Evaluate(stat) / stat.Evaluate(new NominalContext(this)));",
        mutated="    public float ConditionRatio(PerformanceStat stat) => saturate(Evaluate(stat) / stat.Evaluate(new NominalContext(this)));",
        test="ConditionRatioTests.APerfectItemsConditionIsOne",
        expect="green",
    ),

    # --- The ratio must divide actual by nominal, not the reverse. A stat pinned to the identity on the nominal
    # --- side but degraded (half durability) on the actual side proves direction: dividend and divisor swapped
    # --- turns "half sits strictly below full" into "both saturate to 1" (nominal/actual > 1 either way, and
    # --- saturate clamps it there), which is exactly as wrong as it sounds and just as easy to write by accident.
    Mutation(
        rule="ConditionRatio must divide actual by nominal, not the reverse",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float ConditionRatio(PerformanceStat stat) => saturate(Evaluate(stat) / stat.Evaluate(new NominalContext(this)));",
        mutated="    public float ConditionRatio(PerformanceStat stat) => saturate(stat.Evaluate(new NominalContext(this)) / Evaluate(stat));",
        test="ConditionRatioTests.ConditionFallsWithDurabilityAlone",
        expect="red",
    ),

    # --- Durability must actually be one of the terms NominalContext pins to identity -- if it is left to read
    # --- the item's own live DurabilityFactor instead, a half-durability item's nominal denominator shrinks right
    # --- along with its numerator and the ratio never leaves 1. ---
    Mutation(
        rule="NominalContext must pin DurabilityFactor to 1, not forward the item's live durability",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        public float DurabilityFactor(float exponent) => 1f;",
        mutated="        public float DurabilityFactor(float exponent) => _item.DurabilityFactor(exponent);",
        test="ConditionRatioTests.ConditionFallsWithDurabilityAlone",
        expect="red",
    ),

    # --- Same shape of bug, on Heat: if NominalContext forwards the item's live thermal performance instead of
    # --- pinning it to 1, an off-plateau item's own nominal denominator degrades with it and the ratio never
    # --- moves off 1. ---
    Mutation(
        rule="NominalContext must pin HeatFactor to 1, not forward the item's live thermal performance",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        public float HeatFactor(float exponent) => 1f;",
        mutated="        public float HeatFactor(float exponent) => _item.HeatFactor(exponent);",
        test="ConditionRatioTests.ConditionFallsWithTemperatureAwayFromThePlateauAlone",
        expect="red",
    ),

    # --- Same shape again, on PowerSupply -- the exact term Cut 7's brownout curve depends on. ---
    Mutation(
        rule="NominalContext must pin PowerSupplyFactor to 1, not forward the item's live grant",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        public float PowerSupplyFactor(float exponent) => 1f;",
        mutated="        public float PowerSupplyFactor(float exponent) => _item.PowerSupplyFactor(exponent);",
        test="ConditionRatioTests.ConditionFallsWithAPartialPowerGrantAlone",
        expect="red",
    ),

    # --- Zero must land exactly on zero, not divide-by-zero into NaN, when the nominal denominator itself
    # --- degenerates (a Max=0 stat: both actual and nominal evaluate to exactly 0). ConditionRatio relies on
    # --- CultMath's own NaN-favoring math.min/max (0/0 saturates to a real 0f, not NaN) rather than a manual
    # --- epsilon guard -- dropping the saturate() call removes that protection and 0/0 surfaces as raw NaN, which
    # --- is not equal to 0f. ConditionIsZeroWhenTheItemProducesNothing cannot catch this by itself, since its
    # --- nominal stays 100 and only the numerator goes to 0 there. ---
    Mutation(
        rule="ConditionRatio must saturate the division, not return the raw quotient",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float ConditionRatio(PerformanceStat stat) => saturate(Evaluate(stat) / stat.Evaluate(new NominalContext(this)));",
        mutated="    public float ConditionRatio(PerformanceStat stat) => Evaluate(stat) / stat.Evaluate(new NominalContext(this));",
        test="ConditionRatioTests.ConditionIsZeroNotNaNWhenTheNominalValueItselfDegeneratesToZero",
        expect="red",
    ),

    # --- Thruster.Condition must read Thrust, the stat that actually governs this behaviour's force -- reading
    # --- some other stat (here, the item's Visibility) would silently condition against the wrong quantity. ---
    Mutation(
        rule="Thruster.Condition must condition against the Thrust stat, not an unrelated one",
        file="Assets/Scripts/ServerShared/Behaviors/Thruster.cs",
        anchor="    public float Condition => Item?.ConditionRatio(_data.Thrust) ?? 1f;",
        mutated="    public float Condition => Item?.ConditionRatio(_data.Visibility) ?? 1f;",
        test="ConditionRatioTests.ConditionFallsWithDurabilityAlone",
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
        if m.file not in originals:
            originals[m.file] = (REPO_ROOT / m.file).read_text(encoding="utf-8")

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
            print(f"- {f}")
        return 1

    print("\nAll mutations behaved as declared.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
