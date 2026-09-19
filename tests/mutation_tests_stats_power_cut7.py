#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-target.md's brownout ruling ("Continuous consumers brown out through
a power supply curve on their performance stats. An exponent is enough.") -- Cut 7 in this repo's own numbering,
following Cut 6 (mutation_tests_stats_power_cut6.py).

Cut 6 wired a real curve into EquippedItem.PowerSupplyFactor and the bus already wrote a real fractional grant,
but every continuous consumer still gated its own effect on Item.PowerSupply >= 1f (Thruster.Execute,
AetherDrive.Execute, Radiator.Execute, ConstantWeapon.Execute, EnergyDraw.Execute) -- so a partial grant read
exactly like none, the flicking-off bug the ruling names. This cut removes each of those five gates (Radiator's
outright; the other four narrowed to a true-zero floor) and lets the already-curved performance stat (or, for
EnergyDraw, the plain pass/fail gate itself) carry the degradation instead. This script mutates each behaviour's
own changed line back toward the old all-or-nothing shape (or removes the newly-added epsilon floor) and asserts
the matching BrownoutTests.cs Fact goes red for exactly that reason. A no-op control mutation proves the
read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut7.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        file="Assets/Scripts/ServerShared/Behaviors/Thruster.cs",
        anchor="        if(_input > .01f && Item.PowerSupply > 1e-4f)",
        mutated="        if(_input > .01f && Item.PowerSupply > 1e-4f)",
        test="BrownoutTests.ThrusterAtHalfGrantProducesReducedNotZeroNotFullThrust",
        expect="green",
    ),

    # --- Thruster: the gate must not demand a full grant any more. Reverting to the pre-cut ">= 1f" makes a
    # --- half grant produce exactly nothing (the flicking-off bug), instead of a reduced, nonzero thrust. ---
    Mutation(
        rule="Thruster.Execute must not gate its effect on a full grant",
        file="Assets/Scripts/ServerShared/Behaviors/Thruster.cs",
        anchor="        if(_input > .01f && Item.PowerSupply > 1e-4f)",
        mutated="        if(_input > .01f && Item.PowerSupply >= 1f)",
        test="BrownoutTests.ThrusterAtHalfGrantProducesReducedNotZeroNotFullThrust",
        expect="red",
    ),

    # --- AetherDrive: the same rule, the same shape of mutation, on the rotor's own spin-up gate. ---
    Mutation(
        rule="AetherDrive.Execute must not gate its effect on a full grant",
        file="Assets/Scripts/ServerShared/Behaviors/AetherDrive.cs",
        anchor="        if (Item.PowerSupply > 1e-4f)",
        mutated="        if (Item.PowerSupply >= 1f)",
        test="BrownoutTests.AetherDriveAtHalfGrantProducesReducedNotZeroNotFullSpinUp",
        expect="red",
    ),

    # --- Radiator: Cut 7 deletes the gate outright rather than narrowing it (PumpedHeat's own curve already
    # --- reduces the pump, and already zeroes it at true zero supply). Re-inserting the old gate line makes a
    # --- half grant refuse to pump at all, same as a full shortfall. ---
    Mutation(
        rule="Radiator.Execute must not gate its pump on a full grant",
        file="Assets/Scripts/ServerShared/Behaviors/Radiator.cs",
        anchor="        var pumpedHeat = PumpedHeat * max(itemTemperature - _data.TemperatureFloor, 0);",
        mutated="        if (Item != null && Item.PowerSupply < 1f) return false;\n\n        var pumpedHeat = PumpedHeat * max(itemTemperature - _data.TemperatureFloor, 0);",
        test="BrownoutTests.RadiatorAtHalfGrantPumpsReducedNotZeroNotFullHeat",
        expect="red",
    ),

    # --- ConstantWeapon: the exact bug the ruling names -- a partial grant used to safe the weapon off entirely
    # --- (_firing = false), not merely fire it for less. Reverting to "< 1f" makes a half grant stop the weapon
    # --- outright instead of merely firing it for reduced damage. ---
    Mutation(
        rule="ConstantWeapon.Execute must not safe the weapon off on anything less than a full grant",
        file="Assets/Scripts/ServerShared/Behaviors/ConstantWeapon.cs",
        anchor="            if (Item != null && Item.PowerSupply <= 1e-4f)",
        mutated="            if (Item != null && Item.PowerSupply < 1f)",
        test="BrownoutTests.ConstantWeaponAtHalfGrantKeepsFiringWithReducedDamage",
        expect="red",
    ),

    # --- EnergyDraw: the one continuous consumer with no performance stat of its own to curve -- its Execute
    # --- return value IS its whole effect (Entity.cs's per-BehaviorGroup Execute chain). Reverting to ">= 1f"
    # --- makes a half grant close the gate exactly like a full shortfall would. ---
    Mutation(
        rule="EnergyDraw.Execute must not demand a full grant to pass its gate",
        file="Assets/Scripts/ServerShared/Behaviors/EnergyDraw.cs",
        anchor="        return Item == null || Item.PowerSupply > 1e-4f;",
        mutated="        return Item == null || Item.PowerSupply >= 1f;",
        test="BrownoutTests.EnergyDrawAtHalfGrantStillPasses",
        expect="red",
    ),

    # --- The degradation must follow the authored exponent end-to-end through a newly-ungated consumer, not
    # --- just in the abstract (mutation_tests_stats_power_cut6.py already pins this against a bare resolver
    # --- read) -- dropping the exponent from PowerSupplyFactor collapses ConstantWeapon's own Damage curve to
    # --- the identity-linear response regardless of what was authored. ---
    Mutation(
        rule="PowerSupplyFactor must raise PowerSupply to the term's own exponent for a real continuous consumer, not just a bare stat",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);",
        mutated="    public float PowerSupplyFactor(float exponent) => PowerSupply;",
        test="BrownoutTests.ConstantWeaponDamageDegradesByItsAuthoredExponentNotLinearly",
        expect="red",
    ),

    # --- Regression: Cut 4's InputCapacitor all-or-nothing rule for instant items must survive this cut
    # --- completely untouched. Neutering the refusal check proves the regression test actually catches a
    # --- violation, not merely that it happens to pass today. ---
    Mutation(
        rule="InputCapacitor.TrySpend must still refuse a cost it cannot cover",
        file="Assets/Scripts/ServerShared/Behaviors/InputCapacitor.cs",
        anchor="        if (!CanSpend(cost)) return false;",
        mutated="        if (false) return false;",
        test="BrownoutTests.AnInputCapacitorStillRefusesToSpendOnAPartialCharge",
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
