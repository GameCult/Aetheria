#!/usr/bin/env python3
"""Mutation tests for the shield reserve ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19).

Supersedes Cut 4's derived reserve sizing (mutation_tests_stats_power_cut4.py's own shield rule, still intact
and still pinned there against InputCapacitorTests.cs -- a hit within the reserve may draw a partial amount).
This script covers what changed on top of that: ShieldData now authors Capacity/RefillDuration/RestoreDuration
directly, a hit the reserve cannot fully cover breaks the shield instead of a silent, charge-preserving refusal,
the breaking hit itself passes through with no partial absorption, the reserve empties to zero on break (not
left at its pre-hit charge), and a broken shield restores on RestoreDuration, never RefillDuration. Each rule
mutates one exact, unique anchor of source text and asserts the named test in ShieldReserveTests.cs goes red for
exactly that reason. A no-op control mutation proves the read/replace/restore path itself is transparent.

Usage:
    python tests/mutation_tests_shield_reserve.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
SHIELD_CS = "Assets/Scripts/ServerShared/Behaviors/Shield.cs"


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
        file=SHIELD_CS,
        anchor="    public void TakeHit(DamageType type, float damage)",
        mutated="    public void TakeHit(DamageType type, float damage)",
        test="ShieldReserveTests.HitWithinReserveIsAbsorbedAndSpendsCharge",
        expect="green",
    ),

    # --- A hit within the reserve is absorbed and spends charge: TakeHit must actually draw the reserve down,
    # --- not just gate on CanTakeHit and leave Charge untouched. ---
    Mutation(
        rule="a hit within the reserve must spend charge, not just be permitted",
        file=SHIELD_CS,
        anchor="        if (Item != null) _reserve.TrySpend(damage * EnergyUsage);",
        mutated="        if (Item != null) { }",
        test="ShieldReserveTests.HitWithinReserveIsAbsorbedAndSpendsCharge",
        expect="red",
    ),

    # --- A hit beyond the reserve breaks the shield: refusing the hit is not enough on its own -- Broken must
    # --- actually flip, or the shield is just Cut 4's silent refusal again with new authored numbers. ---
    Mutation(
        rule="a hit beyond the reserve must set Broken, not just refuse",
        file=SHIELD_CS,
        anchor=(
            "        if (_reserve.CanSpend(damage * EnergyUsage)) return true;\n"
            "        Broken = true;"
        ),
        mutated="        if (_reserve.CanSpend(damage * EnergyUsage)) return true;",
        test="ShieldReserveTests.HitBeyondReserveBreaksTheShield",
        expect="red",
    ),

    # --- Operator's second ruling: the breaking hit passes through in full, no partial absorption -- TakeHit
    # --- (and its TrySpend/AddHeat) must never run for the hit that broke the shield. Routing the breaking hit
    # --- through TakeHit instead of a direct break-and-empty absorbs/processes it, which the ruling forbids.
    # --- Caught by the restore-timing assertion: TrySpend's own atomicity refuses (cost > charge) and leaves
    # --- Charge at its pre-hit value instead of the emptied reserve.
    Mutation(
        rule="the breaking hit must not be absorbed via TakeHit -- it passes through untouched",
        file=SHIELD_CS,
        anchor=(
            "        Broken = true;\n"
            "        _reserve.AddCharge(-_reserve.Charge); // emptied by the break, not left at its pre-hit charge\n"
            "        return false;"
        ),
        mutated=(
            "        Broken = true;\n"
            "        TakeHit(type, damage);\n"
            "        return false;"
        ),
        test="ShieldReserveTests.BreakingHitIsNotPartiallyAbsorbed",
        expect="red",
    ),

    # --- A broken shield absorbs nothing -- gated on Broken itself, not on the reserve happening to read empty.
    # --- Deleting the Broken short-circuit lets a partially-restored (but still Broken) reserve answer purely
    # --- on its own charge arithmetic, absorbing a hit it should still be refusing outright.
    Mutation(
        rule="a broken shield must refuse every hit regardless of reserve arithmetic",
        file=SHIELD_CS,
        anchor=(
            "        if (Item == null) return true;\n"
            "        if (Broken) return false;"
        ),
        mutated="        if (Item == null) return true;",
        test="ShieldReserveTests.BrokenShieldAbsorbsNothing",
        expect="red",
    ),

    # --- A break empties the reserve to zero, not left at its pre-hit charge -- the restore duration must mean
    # --- the same thing every time. Removing the drain leaves whatever charge the reserve held at the moment
    # --- it broke, letting a shield that broke nearly full restore far faster than one that broke empty.
    Mutation(
        rule="a break must empty the reserve, not leave its pre-hit charge intact",
        file=SHIELD_CS,
        anchor="        Broken = true;\n        _reserve.AddCharge(-_reserve.Charge); // emptied by the break, not left at its pre-hit charge",
        mutated="        Broken = true;",
        test="ShieldReserveTests.BreakEmptiesTheReserveRegardlessOfChargeHeldAtTheMoment",
        expect="red",
    ),

    # --- A broken shield restores on RestoreDuration, not RefillDuration -- the two must stay genuinely
    # --- separate speeds through the one InputCapacitor mechanism. Feeding RefillDuration in both branches
    # --- collapses the punish window: a shield would come back exactly as fast broken as it refills while up.
    Mutation(
        rule="a broken shield must derive its rate from RestoreDuration, not RefillDuration",
        file=SHIELD_CS,
        anchor="        var duration = Broken ? Evaluate(_data.RestoreDuration) : Evaluate(_data.RefillDuration);",
        mutated="        var duration = Evaluate(_data.RefillDuration);",
        test="ShieldReserveTests.BrokenShieldRestoresOnRestoreDurationNotRefillDuration",
        expect="red",
    ),

    # --- A partial grant from the bus stretches both durations proportionally -- Shield must scale its banked
    # --- charge by Item.PowerSupply like every other IPowerConsumer, not assume it always receives the whole
    # --- request regardless of what the bus actually granted.
    Mutation(
        rule="the reserve must only bank the fraction PowerBus actually granted, not the whole request",
        file=SHIELD_CS,
        anchor="            _reserve.AddCharge(_reserve.RequestedFill(dt) * Item.PowerSupply);",
        mutated="            _reserve.AddCharge(_reserve.RequestedFill(dt));",
        test="ShieldReserveTests.PartialGrantStretchesTheRefillDurationProportionally",
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
