#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md's Cut 7 ("Roles authored") -- this repository's own Cut 8,
following Cut 7's brownout work (mutation_tests_stats_power_cut7.py). This is the content cut: designs declare
roles, stats name the role they read, and products author per-role quality so two products of one design differ
by being good at different parts. Three rules are pinned here:

  1. ValidateRoleUsage (ItemData.cs) must refuse a StatSource.Quality term naming a role its design does not
     declare -- and must not refuse one naming a role the design DOES declare (two mutations, opposite
     directions, so neither "always refuse" nor "never refuse" can pass both).
  2. Lot.QualityForRole (Provenance.cs) must resolve the matching RoleFill's own quality, not fall back to the
     lot's workmanship quality regardless of role.
  3. ItemManager.CreateLot (ItemManager.cs) must roll each role's quality from that product's own ProductRole
     mean/deviation, not a fixed default -- so two products of one design with different means mint lots that
     resolve a role-termed stat to different values.

A no-op control mutation proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut8.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        anchor="                    if (!declared.Contains(term.Role))",
        mutated="                    if (!declared.Contains(term.Role))",
        test="RoleAuthoringTests.UpsertRefusesAStatNamingARoleItsDesignLacks",
        expect="green",
    ),

    # --- ValidateRoleUsage must actually refuse an undeclared role, not merely appear to. Weakening the check to
    # --- "never refuse" is the failure the ruling names directly: "validation already refuses a stat naming a
    # --- role its design lacks." ---
    Mutation(
        rule="ValidateRoleUsage must refuse a StatSource.Quality term naming a role its design does not declare",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                    if (!declared.Contains(term.Role))",
        mutated="                    if (false)",
        test="RoleAuthoringTests.UpsertRefusesAStatNamingARoleItsDesignLacks",
        expect="red",
    ),

    # --- The opposite direction: a check that refuses every role-bearing stat, declared or not, would also pass
    # --- the mutation above. This mutant refuses everything, and must kill the positive control instead. ---
    Mutation(
        rule="ValidateRoleUsage must not refuse a StatSource.Quality term naming a role the design DOES declare",
        file="Assets/Scripts/ServerShared/ItemData.cs",
        anchor="                    if (!declared.Contains(term.Role))",
        mutated="                    if (true)",
        test="RoleAuthoringTests.UpsertAcceptsAStatNamingADeclaredRole",
        expect="red",
    ),

    # --- Lot.QualityForRole must resolve the named role's own RoleFill, not fall back to the lot's workmanship
    # --- regardless of which role (or none) a term names. Commenting out the match makes every role read the
    # --- same number StatsReadTheLot's "noRole" case reads, collapsing the very distinction Cut 7 exists to buy. ---
    Mutation(
        rule="QualityForRole must resolve the matching RoleFill, not fall back to workmanship for every role",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor="        foreach (var fill in Roles)\n            if (fill.Role == role) return fill.Quality;",
        mutated="        foreach (var fill in Roles)\n            if (false) return fill.Quality;",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),

    # --- ItemManager.CreateLot must roll each role from that product's own declared mean, not a constant. This
    # --- is the mechanism "products author quality, designs author roles" depends on: without it, authoring a
    # --- ProductRole spread on a design's role would do nothing at mint time. ---
    Mutation(
        rule="CreateLot must roll a role's quality from the product's own ProductRole.Mean, not a fixed default",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="                    Quality = clamp(Random.NextGaussian(build.Mean, build.StandardDeviation), .01f, 1)",
        mutated="                    Quality = clamp(Random.NextGaussian(.5f, build.StandardDeviation), .01f, 1)",
        test="LoadoutTests.CreateLotFillsRolesFromProductSpread",
        expect="red",
    ),

    # --- The same rule, proved the way the operator's market-segmentation ruling states it: two products of one
    # --- design with different role means must mint lots that resolve a role-termed stat to different values. ---
    Mutation(
        rule="Two products of one design with different role means must resolve a role-termed stat differently",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="                    Quality = clamp(Random.NextGaussian(build.Mean, build.StandardDeviation), .01f, 1)",
        mutated="                    Quality = clamp(Random.NextGaussian(.5f, build.StandardDeviation), .01f, 1)",
        test="RoleAuthoringTests.TwoProductsOfOneDesignWithDifferentRoleMeansResolveDifferentValues",
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
