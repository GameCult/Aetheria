#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 6 (the power-supply term and the request-independence
rule).

Cut 6 is purely additive: StatSource.PowerSupply already existed as an identity everywhere (Cut 2), and the bus
already wrote EquippedItem.PowerSupply (Cut 3/5). This cut wires the identity into a real curve
(EquippedItem.PowerSupplyFactor), wires the bus's own per-tick write to invalidate the resolver
(PowerBus.AllocateTiers), and adds the validation rule that a power request may not depend on power supply,
directly (StatValidation.ValidateNoPowerSupplyOnRequest) or through a modifier chain
(StatModifier.ValidateNoPowerSupplyChain). This script mutates each rule's own line of code and asserts the named
test in PowerCurveTests.cs goes red for exactly that reason. A no-op control mutation proves the read/replace/
restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut6.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        anchor="    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);",
        mutated="    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);",
        test="PowerCurveTests.APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent",
        expect="green",
    ),

    # --- The curve itself: a PowerSupply term must actually read this tick's grant raised to its own exponent,
    # --- not the identity Cut 3 left in place as a placeholder. Reverting to the pre-Cut-6 stub makes a half
    # --- grant read exactly like a full one. ---
    Mutation(
        rule="PowerSupplyFactor must read PowerSupply raised to the term's exponent, not the identity",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);",
        mutated="    public float PowerSupplyFactor(float exponent) => 1f;",
        test="PowerCurveTests.APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent",
        expect="red",
    ),

    # --- The exponent itself must be honoured, not discarded -- a stat authored with a steep curve (exponent 2)
    # --- must degrade faster than a linear one. Dropping the exponent from the pow() call collapses every
    # --- PowerSupply term to the same linear response regardless of what was authored. ---
    Mutation(
        rule="PowerSupplyFactor must raise PowerSupply to the term's own exponent, not always to the first power",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);",
        mutated="    public float PowerSupplyFactor(float exponent) => PowerSupply;",
        test="PowerCurveTests.APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent",
        expect="red",
    ),

    # --- The missing half of the wiring: the bus must invalidate StatSource.PowerSupply every time it writes a
    # --- grant, or a resolver entry that cached last tick's (or the pre-activation default) value never
    # --- recomputes at all. Deleting the invalidate call leaves PowerSupply written but never re-read. ---
    Mutation(
        rule="the bus must invalidate StatSource.PowerSupply every time it writes a grant",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            draw.Item.PowerSupply = ratios[draw.Tier];\n            // Cut 6 (docs/stats-and-power-cut.md): the missing half of the wiring -- a stat with a PowerSupply\n            // term must recompute when the grant actually moves. Called unconditionally, once per draw per tick,\n            // the same shape as EquippedItem.UpdatePerformance's own Heat/Durability invalidation: \"recomputes at\n            // most once per tick per (item, stat),\" not \"only when the value moved.\" An item with no PowerSupply\n            // term pays nothing extra -- the resolver's per-source generation bookkeeping (StatResolver.Resolve)\n            // only ever looks at sources a stat's own Terms declared.\n            _entity.Resolver.InvalidateSource(draw.Item, StatSource.PowerSupply);",
        mutated="            draw.Item.PowerSupply = ratios[draw.Tier];",
        test="PowerCurveTests.APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent",
        expect="red",
    ),

    # --- The resolver's own generation scheme must still be keyed per-source: invalidating PowerSupply must never
    # --- touch a stat that declared only Heat. Rerouting the bus's invalidate call to a different StatSource
    # --- proves the wrong thing gets bumped -- the power-termed stat then never recomputes even though the grant
    # --- moved, because nothing ever invalidates the source it actually declared. ---
    Mutation(
        rule="the bus must invalidate PowerSupply specifically, not some other source",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            _entity.Resolver.InvalidateSource(draw.Item, StatSource.PowerSupply);",
        mutated="            _entity.Resolver.InvalidateSource(draw.Item, StatSource.Heat);",
        test="PowerCurveTests.APowerSupplyTermedStatAtHalfGrantDegradesByItsAuthoredExponent",
        expect="red",
    ),

    # --- The direct half of the request-independence rule: a request stat's own declared Terms must be checked
    # --- for PowerSupply. Neutering the check into a no-op accepts the exact catalog record the rule exists to
    # --- refuse. ---
    Mutation(
        rule="a request stat's own Terms must be checked for a PowerSupply term",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="            foreach (var term in stat.Terms)\n                if (term.Source == StatSource.PowerSupply)\n                    throw new InvalidOperationException(\n                        $\"{ownerName}: {behavior.GetType().Name}.{field} is a power request -- its own Terms may not \" +\n                        \"declare a PowerSupply term, or the request would depend on how much power it receives to \" +\n                        \"decide how much power it asks for\");",
        mutated="            foreach (var term in stat.Terms)\n                if (false)\n                    throw new InvalidOperationException(\n                        $\"{ownerName}: {behavior.GetType().Name}.{field} is a power request -- its own Terms may not \" +\n                        \"declare a PowerSupply term, or the request would depend on how much power it receives to \" +\n                        \"decide how much power it asks for\");",
        test="PowerCurveTests.UpsertRefusesAThrusterWhoseEnergyUsageCarriesAPowerSupplyTerm",
        expect="red",
    ),

    # --- The registry itself must actually name the field the mutant fixture exercises (ThrusterData.EnergyUsage)
    # --- -- removing that one entry lets a catalog write straight past the check because nothing on the whole
    # --- registry recognises it as a request stat any more. ---
    Mutation(
        rule="the request-field registry must include ThrusterData.EnergyUsage",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        (typeof(ThrusterData), nameof(ThrusterData.EnergyUsage)),\n",
        mutated="",
        test="PowerCurveTests.UpsertRefusesAThrusterWhoseEnergyUsageCarriesAPowerSupplyTerm",
        expect="red",
    ),

    # --- AetheriaStores.Open must run the same check at catalog load, not only at Upsert -- otherwise a record
    # --- written through the raw CultCache API (bypassing Upsert, exactly like AetherDb's own named dangling-ref
    # --- hole) reopens silently invalid. ---
    Mutation(
        rule="AetheriaStores.Open must validate every EquippableItemData against the power-request rule",
        file="Assets/Scripts/ServerShared/AetheriaStores.cs",
        anchor="                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);\n                // Cut 6 (docs/stats-and-power-cut.md): a power request may not depend on power supply.\n                StatValidation.ValidateNoPowerSupplyOnRequest(data.Name, data.Behaviors);",
        mutated="                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);",
        test="PowerCurveTests.OpenRefusesAThrusterWhoseEnergyUsageCarriesAPowerSupplyTerm",
        expect="red",
    ),

    # --- The modifier-chain half: a modifier's own magnitude must be checked for a PowerSupply dependency before
    # --- it is allowed to attach onto a known request stat. Skipping the taint check lets a booster with a
    # --- power-dependent magnitude attach onto Drain's own EnergyDraw without complaint. ---
    Mutation(
        rule="a modifier chain reaching a request stat through a power-tainted magnitude must be refused",
        file="Assets/Scripts/ServerShared/Behaviors/StatModifier.cs",
        anchor="        if (!IsPowerTainted(data.Modifier, edges, new HashSet<PerformanceStat>()))\n            return;",
        mutated="        if (true)\n            return;",
        test="PowerCurveTests.ActivateRefusesAModifierChainThatWouldCorruptAPowerRequest",
        expect="red",
    ),

    # --- IsPowerTainted's own base case: a magnitude stat's Terms must actually be checked for PowerSupply, not
    # --- treated as always clean. Without this, no magnitude is ever tainted and the chain check above never
    # --- fires for any catalog. ---
    Mutation(
        rule="IsPowerTainted must check the stat's own Terms for a direct PowerSupply dependency",
        file="Assets/Scripts/ServerShared/Behaviors/StatModifier.cs",
        anchor="        foreach (var term in stat.Terms)\n            if (term.Source == StatSource.PowerSupply)\n                return true;",
        mutated="        foreach (var term in stat.Terms)\n            if (false)\n                return true;",
        test="PowerCurveTests.ActivateRefusesAModifierChainThatWouldCorruptAPowerRequest",
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
