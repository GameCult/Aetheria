#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 4 (all draw is continuous -- input capacitors).

Cut 4 closes Cut 3's named, temporary exception for the four instant draws (a burst, a shot, a ping, a hit
taken): each now spends from its own InputCapacitor, fed continuously by PowerBus like every other consumer,
instead of the entity's shared bus capacitors directly through the now-deleted
Entity.TrySpendCapacitorCharge/CanSpendCapacitorCharge. This script mutates each rule's own line of code and
asserts the named test in InputCapacitorTests.cs goes red for exactly that reason. A no-op control mutation
proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut4.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        file="Assets/Scripts/ServerShared/Behaviors/InputCapacitor.cs",
        anchor="    public void AddCharge(float amount) => Charge = clamp(Charge + amount, 0f, Capacity);",
        mutated="    public void AddCharge(float amount) => Charge = clamp(Charge + amount, 0f, Capacity);",
        test="InputCapacitorTests.AddChargeNeverExceedsCapacity",
        expect="green",
    ),

    # --- AddCharge must clamp to Capacity: an input capacitor is sized to exactly one activation, so nothing
    # --- (a rounding surplus, a Capacity that shrank after charge had already banked) may ever report more
    # --- charge than one activation could use. ---
    Mutation(
        rule="AddCharge must clamp to Capacity",
        file="Assets/Scripts/ServerShared/Behaviors/InputCapacitor.cs",
        anchor="    public void AddCharge(float amount) => Charge = clamp(Charge + amount, 0f, Capacity);",
        mutated="    public void AddCharge(float amount) => Charge = Charge + amount;",
        test="InputCapacitorTests.AddChargeNeverExceedsCapacity",
        expect="red",
    ),

    # --- TrySpend must refuse before moving anything it cannot fully cover -- the exact atomicity contract
    # --- Entity.TrySpendCapacitorCharge had, now scoped to one behaviour's own buffer. Deleting the upfront
    # --- check restores the old TryConsumeEnergy-class bug: a cost this buffer cannot fully pay would still
    # --- partially drain it. ---
    Mutation(
        rule="TrySpend must refuse before spending anything it cannot fully cover",
        file="Assets/Scripts/ServerShared/Behaviors/InputCapacitor.cs",
        anchor=(
            "        if (cost < .01f) return true;\n"
            "        if (!CanSpend(cost)) return false;"
        ),
        mutated="        if (cost < .01f) return true;",
        test="InputCapacitorTests.TrySpendMovesNothingWhenTheCostCannotBeFullyCovered",
        expect="red",
    ),

    # --- Default rate derives from Capacity/cooldown (Q4 ruling: "derived from energy and cooldown"). Dropping
    # --- the division collapses the derived rate to Capacity itself (refill in 1 second regardless of the
    # --- weapon's own cooldown), which RateOverrideReplacesTheDerivedDefault pins directly. ---
    Mutation(
        rule="the default rate must derive from Capacity/cooldown, not just Capacity",
        file="Assets/Scripts/ServerShared/Behaviors/InputCapacitor.cs",
        anchor="        Rate = rateOverride > 0f ? rateOverride : (cooldown > 0f ? Capacity / cooldown : Capacity);",
        mutated="        Rate = rateOverride > 0f ? rateOverride : Capacity;",
        test="InputCapacitorTests.RateOverrideReplacesTheDerivedDefault",
        expect="red",
    ),

    # --- Cut 4's own verification bullet: "a weapon whose input capacitor is partly filled does not fire a
    # --- partial shot." A fire attempt must spend exactly Capacity -- never a lesser, possibly-partial amount
    # --- -- so it only ever succeeds at full charge. Spending 0 instead bypasses the whole-shot requirement:
    # --- every attempt would "succeed" regardless of charge. ---
    Mutation(
        rule="a fire attempt must spend exactly Capacity, never a lesser amount",
        file="Assets/Scripts/ServerShared/Behaviors/InstantWeapon.cs",
        anchor="    protected bool TrySpendActivationEnergy() => Item == null || _capacitor.TrySpend(_capacitor.Capacity);",
        mutated="    protected bool TrySpendActivationEnergy() => Item == null || _capacitor.TrySpend(0f);",
        test="InputCapacitorTests.WeaponDoesNotFireOnAPartialCharge",
        expect="red",
    ),

    # --- The other half of the same rule, at the fill site instead of the spend site: the capacitor must only
    # --- ever bank the fraction PowerBus actually granted (Item.PowerSupply), never the whole request
    # --- regardless of supply -- otherwise a weapon on half power would still reach full charge (and fire) on
    # --- the very first tick, exactly the "every draw succeeds" bug Cut 3 exists to remove, just relocated. ---
    Mutation(
        rule="the capacitor must only bank the fraction PowerBus actually granted, not the whole request",
        file="Assets/Scripts/ServerShared/Behaviors/InstantWeapon.cs",
        anchor="            _capacitor.AddCharge(_capacitor.RequestedFill(dt) * Item.PowerSupply);",
        mutated="            _capacitor.AddCharge(_capacitor.RequestedFill(dt));",
        test="InputCapacitorTests.WeaponDoesNotFireOnAPartialCharge",
        expect="red",
    ),

    # --- Shield is the map's named exception: a hit is not a chosen activation, so its reserve is a continuous
    # --- one that may absorb an arbitrary fraction of a hit's cost, unlike a weapon's shot (always spent
    # --- whole). Requiring a hit to cover the WHOLE reserve before absorbing anything collapses that
    # --- distinction back into an activation buffer, which the map explicitly says not to give it.
    # ---
    # --- F8 (docs/stats-and-power-cut.md, Soul pass 2026-09-19) re-anchor: the shield reserve ruling
    # --- (2026-09-19) gave Shield a Broken state, splitting CanTakeHit's old single-expression body
    # --- (`return Item == null || _reserve.CanSpend(...)`) into an Item-null check, a Broken check, and this
    # --- CanSpend call as its own line; F5 (the same Soul pass) then made the whole method a pure query. The
    # --- rule under test -- CanSpend reads the hit's own cost, not a full-reserve requirement -- is unchanged
    # --- and still reachable from this same line. ---
    Mutation(
        rule="the shield reserve must allow a partial draw, not require a full reserve before absorbing a hit",
        file="Assets/Scripts/ServerShared/Behaviors/Shield.cs",
        anchor="        return _reserve.CanSpend(damage * EnergyUsage);",
        mutated="        return _reserve.CanSpend(_reserve.Capacity);",
        test="InputCapacitorTests.ShieldReserveAllowsAPartialDrawButRefusesAHitItCannotFullyCover",
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
