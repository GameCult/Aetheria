#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 1 (terms replace the four fixed exponents; R-heat).

Cut 1 replaces PerformanceStat's four fixed exponent fields with a declared list of StatTerm (source +
exponent + role), and replaces EquippableItemData.HeatPerformanceCurve with an authored minimum, maximum,
optimum and plateau width. This script mutates each rule's own line of code and asserts the named test goes
red for exactly that reason. A no-op control mutation proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut1.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), term.Exponent),",
        mutated="                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), term.Exponent),",
        test="LoadoutTests.StatsReadTheLot",
        expect="green",
    ),
    # --- A term's Role must actually route the quality read; Quality is not one universal reader ---
    Mutation(
        rule="a Quality term must read its own Role, not always the item's workmanship (unrolled)",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), term.Exponent),",
        mutated="                StatSource.Quality => pow(context.Lot.QualityForRole(null), term.Exponent),",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    # --- A Durability term must resolve through DurabilityFactor, not some other source's factor ---
    Mutation(
        rule="a Durability term must call DurabilityFactor, not HeatFactor",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                StatSource.Durability => context.DurabilityFactor(term.Exponent),",
        mutated="                StatSource.Durability => context.HeatFactor(term.Exponent),",
        test="LoadoutTests.EvaluateDurabilityExponentReadsLotQuality",
        expect="red",
    ),
    # --- A stat with no declared terms is the identity (factor 1, i.e. Max) -- the zero-multiplier equivalence
    # --- the migration depends on (docs/stats-and-power-cut.md Cut 1's "a zero multiplier becomes no term") ---
    Mutation(
        rule="an undeclared term set must resolve to the identity factor (1), not 0",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        var factor = 1f;",
        mutated="        var factor = 0f;",
        test="LoadoutTests.ConsumableProgressAppliesOnlyWhenDeclared",
        expect="red",
    ),
    # --- ConsumableProgress must apply the term's own exponent, not a fixed pass-through ---
    Mutation(
        rule="a ConsumableProgress term must apply its own exponent",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor=(
            "    public float ConsumableProgressFactor(float exponent) =>\n"
            "        pow(Data.Effectiveness.Evaluate((Data.Duration - RemainingDuration) / Data.Duration), exponent);"
        ),
        mutated=(
            "    public float ConsumableProgressFactor(float exponent) =>\n"
            "        Data.Effectiveness.Evaluate((Data.Duration - RemainingDuration) / Data.Duration);"
        ),
        test="LoadoutTests.ConsumableProgressAppliesOnlyWhenDeclared",
        expect="red",
    ),
    # --- R-heat validation: an optimum outside its own bounds must be refused at load, naming the item ---
    Mutation(
        rule="StatValidation must refuse an OptimalTemperature outside its bounds",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor=(
            "        if (data.OptimalTemperature < data.MinimumTemperature || data.OptimalTemperature > data.MaximumTemperature)"
        ),
        mutated="        if (false)",
        test="HeatResponseTests.LoadRefusesAnOptimumOutsideItsBounds",
        expect="red",
    ),
    # --- R-heat validation: a negative plateau width must be refused at load, naming the item ---
    Mutation(
        rule="StatValidation must refuse a negative PlateauWidth",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        if (data.PlateauWidth < 0)",
        mutated="        if (false)",
        test="HeatResponseTests.LoadRefusesANegativePlateauWidth",
        expect="red",
    ),
    # --- R-heat shape: performance must be full across the whole authored plateau, not only at the optimum ---
    Mutation(
        rule="Performance() must be 1 across the whole plateau, not only exactly at the optimum",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        var halfPlateau = PlateauWidth * 0.5f;",
        mutated="        var halfPlateau = 0f;",
        test="HeatResponseTests.PerformanceIsFullAcrossThePlateau",
        expect="red",
    ),
    # --- The wear lever: inside the plateau, Wear's thermal term must be zero (Performance == 1 there) ---
    Mutation(
        rule="held inside the plateau must take no thermal wear (the operational-lifespan lever)",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor=(
            "        Wear = (1 - pow(ThermalPerformance,\n"
            "                (1 - pow(Lot.Quality, ItemManager.GameplaySettings.QualityWearExponent)) *\n"
            "                ItemManager.GameplaySettings.ThermalWearExponent) +\n"
            "                deltaTemp * ItemManager.GameplaySettings.DeltaTempWearExponent            \n"
            "            ) * Data.Durability / Data.ThermalResilience;"
        ),
        mutated=(
            "        Wear = (0.05f - pow(ThermalPerformance,\n"
            "                (1 - pow(Lot.Quality, ItemManager.GameplaySettings.QualityWearExponent)) *\n"
            "                ItemManager.GameplaySettings.ThermalWearExponent) +\n"
            "                deltaTemp * ItemManager.GameplaySettings.DeltaTempWearExponent            \n"
            "            ) * Data.Durability / Data.ThermalResilience;"
        ),
        test="HeatResponseTests.HeldInsidePlateauTakesNoThermalWear",
        expect="red",
    ),
    # --- The wear lever's other half: a fast swing across the plateau must still wear, from deltaTemp alone ---
    Mutation(
        rule="deltaTemp must still cost wear even when the swing lands back inside the plateau",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        var deltaTemp = math.abs(temp - oldTemperature);",
        mutated="        var deltaTemp = 0f;",
        test="HeatResponseTests.AFastSwingAcrossThePlateauStillWears",
        expect="red",
    ),

    # --- Soul's Cut 1 findings (docs/stats-and-power-cut.md), S7: four mutations that survived the original
    # --- suite. Each gets a dedicated test here rather than reusing a coincidentally-sensitive one. ---

    # S7: out-of-bounds semantics. The old curve returned 1 at any temperature it had no data for; R-heat's whole
    # point is that a design is dead at and beyond its bounds. Restoring the old "1" must fail loudly, at the
    # boundary itself and strictly beyond it.
    Mutation(
        rule="a design must be dead (0), not immune (1), at and beyond its bounds",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        if (temperature <= MinimumTemperature || temperature >= MaximumTemperature) return 0f;",
        mutated="        if (temperature <= MinimumTemperature || temperature >= MaximumTemperature) return 1f;",
        test="HeatResponseTests.PerformanceIsZeroAtAndBeyondEachBound",
        expect="red",
    ),
    # S7: the low-side plateau clamp -- StatValidation must refuse a plateau whose low edge pokes past
    # MinimumTemperature, not just an optimum outside the bounds outright.
    Mutation(
        rule="StatValidation must refuse a plateau whose low edge pokes past MinimumTemperature",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        if (plateauLow < data.MinimumTemperature || plateauHigh > data.MaximumTemperature)",
        mutated="        if (false || plateauHigh > data.MaximumTemperature)",
        test="HeatResponseTests.UpsertRefusesAPlateauThatPokesPastItsBounds",
        expect="red",
    ),
    # S7: the high-side plateau clamp, the other half of the same rule.
    Mutation(
        rule="StatValidation must refuse a plateau whose high edge pokes past MaximumTemperature",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        if (plateauLow < data.MinimumTemperature || plateauHigh > data.MaximumTemperature)",
        mutated="        if (plateauLow < data.MinimumTemperature || false)",
        test="HeatResponseTests.UpsertRefusesAPlateauThatPokesPastItsBounds",
        expect="red",
    ),
    # S7: a Quality term must raise the lot's quality to its own declared Exponent. StatsReadTheLot uses
    # Exponent = 1 throughout (pow(x, 1) == x), which cannot distinguish "read the term's exponent" from "always
    # use 1" -- that is exactly how this mutation survived the original suite.
    Mutation(
        rule="a Quality term must apply its own exponent, not always 1",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), term.Exponent),",
        mutated="                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), 1f),",
        test="LoadoutTests.QualityTermAppliesItsOwnExponent",
        expect="red",
    ),

    # --- S5/S6: the validation gaps Soul found closed. Zero-span and NaN were silently accepted before. ---
    Mutation(
        rule="StatValidation must refuse a zero-span range (Tractor Beam's exact bug: Min == Max)",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="        if (data.MinimumTemperature == data.MaximumTemperature)",
        mutated="        if (false)",
        test="HeatResponseTests.UpsertRefusesAZeroSpanRange",
        expect="red",
    ),
    Mutation(
        rule="StatValidation must refuse NaN in any of the four heat-response fields",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor=(
            "        if (float.IsNaN(data.MinimumTemperature) || float.IsNaN(data.MaximumTemperature) ||\n"
            "            float.IsNaN(data.OptimalTemperature) || float.IsNaN(data.PlateauWidth))"
        ),
        mutated="        if (false)",
        test="HeatResponseTests.UpsertRefusesNaNInAnyHeatResponseField",
        expect="red",
    ),
    # S6: the writable path used to trust Open()'s one-time check; a session that wrote an invalid record and
    # committed it produced a catalog that only failed the *next* time somebody reopened it. Mutating away the
    # Upsert-time validation call must fail a test that checks the record never reaches disk in the first place.
    Mutation(
        rule="CultRecordRefs.Upsert must validate an EquippableItemData's heat response before it reaches disk",
        file="Assets/Scripts/ServerShared/AetheriaStores.cs",
        anchor="        if (document is EquippableItemData data) StatValidation.ValidateHeatResponse(data);",
        mutated="        if (false) { }",
        test="HeatResponseTests.UpsertRefusesAnOptimumOutsideItsBounds",
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

    print("\nAll mutation cases behaved as Cut 1's verification section demands.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
