#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 2 (the resolver owns every value).

Cut 2 deletes PerformanceStat's two per-entity modifier dictionaries (the leak, §0.3) and moves every resolved
(item, stat) value and every attached modifier into a new StatResolver owned by the Entity, one per entity. This
script mutates each rule's own line of code and asserts the named test goes red for exactly that reason. A no-op
control mutation proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut2.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        file="Assets/Scripts/ServerShared/StatResolver.cs",
        anchor="        var value = stat.Evaluate(context);",
        mutated="        var value = stat.Evaluate(context);",
        test="StatResolverTests.ResolveRecomputesOnlyAfterItsDeclaredSourceIsInvalidated",
        expect="green",
    ),

    # --- The leak (§0.3): a resolver reachable from a static field is reachable from the catalog's assembly for
    # --- the life of the process, exactly like the two dictionaries it replaced. Both the leak test and the
    # --- one-resolver-per-entity structural test die under this same mutation. ---
    Mutation(
        rule="Entity.Resolver must be an instance field: one resolver per entity, not a shared/static one",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public readonly StatResolver Resolver = new StatResolver();",
        mutated="    public static readonly StatResolver Resolver = new StatResolver();",
        test="StatResolverTests.EachEntityOwnsItsOwnResolver",
        expect="red",
    ),
    Mutation(
        rule="a static resolver keeps every entity that ever evaluated a stat reachable for the life of the process",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="    public readonly StatResolver Resolver = new StatResolver();",
        mutated="    public static readonly StatResolver Resolver = new StatResolver();",
        test="StatResolverTests.UnequippingAndDroppingAnEntityLeavesItCollectible",
        expect="red",
    ),

    # --- A resolved value is per (item, stat): a modifier must attach to the resolver entry it actually targets
    # --- (the target item's own owner key), not to the modifying behaviour's own item. This is the real defect
    # --- Hands found and fixed while wiring StatModifier to the resolver: attaching under the wrong owner leaves
    # --- the target item's resolved value untouched. ---
    Mutation(
        rule="a modifier must attach under the target item's own owner, not the modifying behaviour's owner",
        file="Assets/Scripts/ServerShared/Behaviors/StatModifier.cs",
        anchor="        foreach (var target in _targets)\n            Entity.Resolver.AttachModifier(target.Owner, target.Stat, this, _data.Type, value);",
        mutated="        foreach (var target in _targets)\n            Entity.Resolver.AttachModifier(StatOwner, target.Stat, this, _data.Type, value);",
        test="StatResolverTests.AModifierOnOneEntityDoesNotReachAnotherEntitysResolvedValue",
        expect="red",
    ),

    # --- Self-dependency / modifier-chain cycles (Cut 2's Adds): a modifier whose magnitude stat is one of its
    # --- own targets must be refused at equip, not silently accepted to resolve one tick behind itself. ---
    Mutation(
        rule="a modifier chain that reaches its own magnitude stat must be refused at equip",
        file="Assets/Scripts/ServerShared/Behaviors/StatModifier.cs",
        anchor="                if (ReferenceEquals(target, data.Modifier)) return true;",
        mutated="                if (false) return true;",
        test="StatResolverTests.AModifierThatTargetsItsOwnMagnitudeStatIsRefusedAtEquip",
        expect="red",
    ),

    # --- Recompute-on-change: a cached value must be reused while every source it declared is unchanged, and
    # --- must be recomputed once any of them is invalidated. Each half gets its own mutation. ---
    Mutation(
        rule="a resolved value must stay cached until its own declared source is invalidated (never on every read)",
        file="Assets/Scripts/ServerShared/StatResolver.cs",
        anchor="        if (_cache.TryGetValue(entry, out var cached) && IsCurrent(owner, cached))",
        mutated="        if (false)",
        test="StatResolverTests.ResolveDoesNotRecomputeWhenNoDeclaredSourceMoved",
        expect="red",
    ),
    Mutation(
        rule="a resolved value must actually recompute once its declared source is invalidated (never stay cached forever)",
        file="Assets/Scripts/ServerShared/StatResolver.cs",
        anchor="        if (_cache.TryGetValue(entry, out var cached) && IsCurrent(owner, cached))",
        mutated="        if (_cache.TryGetValue(entry, out var cached))",
        test="StatResolverTests.ResolveRecomputesOnlyAfterItsDeclaredSourceIsInvalidated",
        expect="red",
    ),
    # --- Invalidation must be scoped to the source that actually moved -- otherwise every stat recomputes on
    # --- every tick regardless of the Terms it declared, which defeats the whole point of caching by source. ---
    Mutation(
        rule="invalidating one source must not bump every source's generation",
        file="Assets/Scripts/ServerShared/StatResolver.cs",
        anchor="        perSource[source] = GenerationOf(owner, source) + 1;",
        mutated="        foreach (StatSource s in System.Enum.GetValues(typeof(StatSource))) perSource[s] = GenerationOf(owner, s) + 1;",
        test="StatResolverTests.InvalidatingAnUnrelatedSourceDoesNotRecomputeAStatThatDidNotDeclareIt",
        expect="red",
    ),
    # --- Attaching/detaching a modifier must invalidate immediately: a modifier is not one of the stat's
    # --- declared Terms, so the source-generation scheme never sees it move on its own. ---
    Mutation(
        rule="attaching a modifier must invalidate its (owner, stat) entry immediately",
        file="Assets/Scripts/ServerShared/StatResolver.cs",
        anchor=(
            "        (type == StatModifierType.Constant ? set.Constant : set.Scale)[modifierKey] = value;\n"
            "        _cache.Remove(entry);"
        ),
        mutated="        (type == StatModifierType.Constant ? set.Constant : set.Scale)[modifierKey] = value;",
        test="StatResolverTests.AttachingAModifierInvalidatesImmediately",
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
            print(f)
        return 1

    print("\nAll mutation cases behaved as Cut 2's verification section demands.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
