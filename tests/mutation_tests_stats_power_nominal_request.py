#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md's nominal-request ruling (operator ruling 2026-09-19).

Two landed rulings collided: a stat deciding a power request may not depend on power supply (Cut 6, widened by
F6 to name every stat a PowerRequest actually reads), and continuous consumers brown out through a power term on
their performance stats (Cut 7). For Radiator and AetherDrive those are the same stat (RadiatorData.PumpedHeat,
AetherDriveData.Torque), which made six shipped catalog records illegal and left six BrownoutTests.cs cases
skipped.

The resolution: a power request is evaluated NOMINALLY -- what the item wants at full supply, not what it is
currently managing. EquippedItem.EvaluateNominalPower (Entity.cs) is the read every PowerRequest/RefreshReserve/
RefreshInputCapacitor implementation now uses for a StatValidation.PowerRequestFields stat; it pins that stat's
own PowerSupplyFactor to 1 regardless of what Terms it declares, removing the circularity at the root instead of
forbidding the field (StatValidation.ValidateNoPowerSupplyOnRequest, the static half of Cut 6/F6's rule, is
deleted -- see mutation_tests_stats_power_cut6.py's own updated header). This script pins that EvaluateNominalPower
actually forces the pin (rather than silently falling back to the real, curved value) and that Radiator.PowerRequest
actually calls it instead of the ordinary Evaluate. AetherDrive.PowerRequest's own Torque read has no equivalent
black-box mutation here -- its torqueRatio term self-cancels Torque's own magnitude outside the rpm cap, so
reverting that one call site produces byte-identical demand in every fixture tried (see the comment in
BrownoutTests.cs, right after AetherDriveAtZeroGrantProducesNothing, for the empirical finding); its correctness
rests on the identical pattern to Radiator's own call site instead.

Usage:
    python tests/mutation_tests_stats_power_nominal_request.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        anchor="        public PowerRequestContext(EquippedItem item) => _item = item;\n        public Lot Lot => _item.Lot;\n        public float HeatFactor(float exponent) => _item.HeatFactor(exponent);\n        public float DurabilityFactor(float exponent) => _item.DurabilityFactor(exponent);\n        public float ConsumableProgressFactor(float exponent) => _item.ConsumableProgressFactor(exponent);\n        public float PowerSupplyFactor(float exponent) => 1f;",
        mutated="        public PowerRequestContext(EquippedItem item) => _item = item;\n        public Lot Lot => _item.Lot;\n        public float HeatFactor(float exponent) => _item.HeatFactor(exponent);\n        public float DurabilityFactor(float exponent) => _item.DurabilityFactor(exponent);\n        public float ConsumableProgressFactor(float exponent) => _item.ConsumableProgressFactor(exponent);\n        public float PowerSupplyFactor(float exponent) => 1f;",
        test="BrownoutTests.EvaluateNominalPowerIgnoresTheItemsCurrentPowerSupplyGrant",
        expect="green",
    ),

    # --- The pin itself: EvaluateNominalPower's own context must actually return the identity for PowerSupply,
    # --- not forward it to the real item -- otherwise EvaluateNominalPower is just Evaluate under another name,
    # --- and every PowerRequest/RefreshReserve/RefreshInputCapacitor call site built on it stays exactly as
    # --- circular as before this ruling (the oscillation Soul originally measured against the shipped catalog).
    Mutation(
        rule="PowerRequestContext.PowerSupplyFactor must return the identity (1), not forward to the real item",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        public float ConsumableProgressFactor(float exponent) => _item.ConsumableProgressFactor(exponent);\n        public float PowerSupplyFactor(float exponent) => 1f;\n        public float ScaleModifier(PerformanceStat stat) => _item.ScaleModifier(stat);\n        public float ConstantModifier(PerformanceStat stat) => _item.ConstantModifier(stat);\n    }\n\n    public float HeatFactor(float exponent)",
        mutated="        public float ConsumableProgressFactor(float exponent) => _item.ConsumableProgressFactor(exponent);\n        public float PowerSupplyFactor(float exponent) => _item.PowerSupplyFactor(exponent);\n        public float ScaleModifier(PerformanceStat stat) => _item.ScaleModifier(stat);\n        public float ConstantModifier(PerformanceStat stat) => _item.ConstantModifier(stat);\n    }\n\n    public float HeatFactor(float exponent)",
        test="BrownoutTests.EvaluateNominalPowerIgnoresTheItemsCurrentPowerSupplyGrant",
        expect="red",
    ),

    # --- Radiator.PowerRequest must actually call the nominal read for PumpedHeat/WasteHeat/EnergyUsage, not the
    # --- ordinary curved Evaluate. A single-tick fixture cannot distinguish the two (Item.PowerSupply defaults to
    # --- 1 before the bus has ever run, so tick 1 reads the same value either way) -- this fixture runs two
    # --- ticks, so tick 2 sees whatever tick 1 actually granted. Tuned so nominal PumpedHeat/WasteHeat (10/5 = 2)
    # --- stays comfortably above tempRatio (pinned near 1) while the curved value at tick 1's partial grant
    # --- (pow(.4, 1)*10/5 = .8) falls below it -- reverting to Evaluate collapses tick 2's demand through
    # --- PowerRequest's own early-out gate instead of leaving it open. AetherDrive.PowerRequest's own Torque read
    # --- has no equivalent black-box mutation: see the comment in BrownoutTests.cs above
    # --- AetherDriveAtHalfGrantProducesReducedNotZeroNotFullSpinUp for why (torqueRatio self-cancels Torque's own
    # --- magnitude outside the rpm cap, verified empirically while building this script).
    Mutation(
        rule="Radiator.PowerRequest must read PumpedHeat/WasteHeat/EnergyUsage nominally, not via the curved Evaluate",
        file="Assets/Scripts/ServerShared/Behaviors/Radiator.cs",
        anchor="        var pumpedHeat = EvaluateNominalPower(_data.PumpedHeat);\n        var wasteHeat = EvaluateNominalPower(_data.WasteHeat);\n        var tempRatio = max(RadiatorTemperature / Temperature, 1);\n        if (tempRatio > pumpedHeat / wasteHeat) return 0f;\n        return EvaluateNominalPower(_data.EnergyUsage) * tempRatio * dt;",
        mutated="        var pumpedHeat = Evaluate(_data.PumpedHeat);\n        var wasteHeat = Evaluate(_data.WasteHeat);\n        var tempRatio = max(RadiatorTemperature / Temperature, 1);\n        if (tempRatio > pumpedHeat / wasteHeat) return 0f;\n        return Evaluate(_data.EnergyUsage) * tempRatio * dt;",
        test="BrownoutTests.RadiatorSecondTickDemandIsUnaffectedByTheFirstTicksPartialGrant",
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
