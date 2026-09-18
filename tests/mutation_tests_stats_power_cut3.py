#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 3 (the power bus replaces TryConsumeEnergy).

Cut 3 deletes Entity.CanConsumeEnergy/TryConsumeEnergy and Reactor's own capacitor handling, replacing them with
PowerBus: a single per-Entity step that turns every IPowerConsumer's request plus the tick's reactor generation
and stored capacitor charge into one shared grant ratio, refusing demand that supply cannot cover instead of
taxing the shortfall onto the reactor as heat. This script mutates each rule's own line of code and asserts the
named test in PowerBusTests.cs goes red for exactly that reason. A no-op control mutation proves the
read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut3.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="        TotalGeneration = reactors.Sum(r => r.Generation(dt));",
        mutated="        TotalGeneration = reactors.Sum(r => r.Generation(dt));",
        test="PowerBusTests.GrantIsCappedAtGenerationPlusStoredCapacitorCharge",
        expect="green",
    ),

    # --- Cut 3 verification bullet 1 (§0.5): "a ship whose requests exceed generation gets a total grant equal
    # --- to generation plus available capacitor charge, and not more." Dropping the capacitor draw on a deficit
    # --- caps the grant at generation alone -- still capped, but at the wrong ceiling, which is exactly the kind
    # --- of silent under-delivery a ceiling test has to catch as sharply as the old unlimited-reactor bug. ---
    Mutation(
        rule="a deficit must draw stored capacitor charge, not just generation",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            var chargeDrawn = min(preCapacitorNet, availableCharge);",
        mutated="            var chargeDrawn = 0f;",
        test="PowerBusTests.GrantIsCappedAtGenerationPlusStoredCapacitorCharge",
        expect="red",
    ),

    # --- Cut 3 verification bullet 2 (§0.5): "a refused request produces no effect at all -- not a partial one,
    # --- and not an overload-heat receipt." This is the map's own named mutation: "make the bus grant the full
    # --- request and heat the reactor; it goes red" -- forcing overload to 0 finances the whole deficit for
    # --- free, the exact HEAD bug ("every draw succeeds while one reactor is online") Cut 3 exists to remove. ---
    Mutation(
        rule="unmet demand after generation and stored charge must be refused (overload), never financed for free",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            overload = preCapacitorNet - chargeDrawn;",
        mutated="            overload = 0f;",
        test="PowerBusTests.ZeroSupplyRefusesTheWholeRequest",
        expect="red",
    ),

    # --- Cut 3 verification bullet 3 (§0.5): "two ships with identical loadouts fitted in opposite equip order
    # --- get identical grants. That is the death of the hidden priority." The invariant that makes equip order
    # --- unable to matter is that every consuming item reads back the SAME GrantRatio; restricting the write to
    # --- only the first equipped consumer reintroduces exactly the "whoever equipped first" priority the cut
    # --- kills, just relocated from SortPosition into this loop. ---
    Mutation(
        rule="every consuming item must be written the same GrantRatio, not just the first one found",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor=(
            "        foreach (var item in _entity.Equipment)\n"
            "            if (item.Behaviors.Any(b => b is IPowerConsumer))\n"
            "                item.PowerSupply = GrantRatio;"
        ),
        mutated=(
            "        var firstConsumer = _entity.Equipment.FirstOrDefault(item => item.Behaviors.Any(b => b is IPowerConsumer));\n"
            "        if (firstConsumer != null) firstConsumer.PowerSupply = GrantRatio;"
        ),
        test="PowerBusTests.EveryConsumingItemReadsTheSameGrantRatio",
        expect="red",
    ),

    # Entity.TrySpendCapacitorCharge/CanSpendCapacitorCharge -- the four instant draws' named, temporary
    # exception this file used to pin -- died with their last caller in Cut 4
    # (docs/stats-and-power-cut.md, Cut 4). See mutation_tests_stats_power_cut4.py's InputCapacitor.TrySpend
    # case for the same atomicity rule, now scoped to each behaviour's own buffer.
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
