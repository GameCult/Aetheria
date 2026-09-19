#!/usr/bin/env python3
"""Mutation tests for docs/stats-and-power-cut.md Cut 5 (priority tiers).

Cut 5 replaces PowerBus's single shared GrantRatio with priority tiers (PowerTiers.cs): critical is fed in full
before the next tier sees anything, each following tier divides what remains, and a tier is a stored player
choice (EquippableItem.PowerTier) rather than something the bus derives every tick. This script mutates each
rule's own line of code and asserts the named test in PowerTierTests.cs goes red for exactly that reason. A
no-op control mutation proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_stats_power_cut5.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
        anchor="            if (demand <= 1e-4f) { ratios[tier] = 1f; continue; }",
        mutated="            if (demand <= 1e-4f) { ratios[tier] = 1f; continue; }",
        test="PowerTierTests.CriticalTierIsFedInFullWhileLowerTierStarves",
        expect="green",
    ),

    # --- Cut 5's headline rule: critical must be satisfied in full before the next tier receives anything.
    # --- Walking tiers in descending order instead of ascending feeds Utility first, so the fixed generation
    # --- that exactly covers Critical's own demand gets spent on Utility instead, starving Critical -- the
    # --- opposite of the ruling ("critical is fed first"). ---
    Mutation(
        rule="tiers must be walked lowest-number (highest priority) first",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="        for (var tier = 0; tier < PowerTiers.Count; tier++)",
        mutated="        for (var tier = PowerTiers.Count - 1; tier >= 0; tier--)",
        test="PowerTierTests.CriticalTierIsFedInFullWhileLowerTierStarves",
        expect="red",
    ),

    # --- A satisfied tier must commit what it took, so a lower tier can never see it again. Skipping the
    # --- subtraction lets every tier see the SAME remaining pool -- Utility would compute against the full 30,
    # --- not the 0 left after Critical, so it would stop reading as fully starved. ---
    Mutation(
        rule="a fed tier must commit its grant against remaining, or a lower tier could see it twice",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            remaining -= grant;",
        mutated="            // remaining -= grant;",
        test="PowerTierTests.CriticalTierIsFedInFullWhileLowerTierStarves",
        expect="red",
    ),

    # --- Within a tier, every consumer must ration by the SAME fraction of its OWN request -- that is what
    # --- makes two unequal requests divide proportionally instead of splitting the tier's grant evenly per
    # --- consumer regardless of how much each asked for. ---
    Mutation(
        rule="within a tier, the ratio must be grant/demand (proportional), not an even per-consumer split",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            ratios[tier] = saturate(grant / demand);",
        mutated="            ratios[tier] = saturate(grant / tierDemand.Length);",
        test="PowerTierTests.TwoConsumersInOneTierAreRationedProportionally",
        expect="red",
    ),

    # --- An empty tier must read as fully met (ratio 1, remaining untouched) so its share passes straight to
    # --- the next tier down. Forcing it to 0 instead makes an unused tier look starved and -- because nothing
    # --- reduces remaining for a 0-demand tier either way -- has no effect on the real consumer, but the
    # --- assertion on the empty tiers' own ratio catches the wrong value directly. ---
    Mutation(
        rule="a tier with no demand must read as fully met (1f), not starved (0f)",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            if (demand <= 1e-4f) { ratios[tier] = 1f; continue; }",
        mutated="            if (demand <= 1e-4f) { ratios[tier] = 0f; continue; }",
        test="PowerTierTests.ATierWithNoDemandPassesItsShareDown",
        expect="red",
    ),

    # --- TotalGrant is the ceiling tiering allocates -- it must still reflect what generation and stored charge
    # --- can actually cover, not the raw total request. Feeding the allocator TotalDemand instead (pretending
    # --- supply always covers everything) hands Utility the untouched 80 instead of the 0 left after Critical's
    # --- own 30 is committed, so it reads as fully fed instead of starved. ---
    Mutation(
        rule="tiering must allocate against TotalGrant (what supply covers), not TotalDemand (what was asked)",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="        var remaining = TotalGrant;",
        mutated="        var remaining = TotalDemand;",
        test="PowerTierTests.CriticalTierIsFedInFullWhileLowerTierStarves",
        expect="red",
    ),

    # --- The boundary rule itself: once a higher tier's own shortfall has consumed all of remaining, a lower
    # --- tier must see 0, not some leftover computed from the higher tier's unmet demand. Handing every tier
    # --- the ORIGINAL TotalGrant instead of the shrinking remaining pool would let Utility compute its own
    # --- ratio against the full grant as if Critical had never been served first -- exactly the leak §1.3
    # --- forbids. ---
    Mutation(
        rule="each tier must divide what is actually left (remaining), not the original total grant again",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="            var grant = min(demand, remaining);",
        mutated="            var grant = min(demand, TotalGrant);",
        test="PowerTierTests.LowerTierGetsExactlyZeroWhenAHigherTierAloneStarves",
        expect="red",
    ),

    # --- The tier must be read fresh from the stored field every Step, not cached at equip time -- otherwise a
    # --- player's tier change on the schematic UI would need an unequip/re-equip round trip to take effect,
    # --- which the map's own verification bullet rules out. ---
    Mutation(
        rule="the bus must read the item's CURRENT stored PowerTier every Step, not one captured once at equip",
        file="Assets/Scripts/ServerShared/PowerBus.cs",
        anchor="                var tier = item.EquippableItem.PowerTier;",
        mutated="                var tier = consumer.DefaultPowerTier;",
        test="PowerTierTests.ATierChangeOnAnEquippedItemTakesEffectNextTickWithNoReEquip",
        expect="red",
    ),

    # --- EquippedItem's constructor must seed a kind default only once, the first time a unit is equipped
    # --- (PowerTiers.Unassigned) -- never re-seeding it on every equip is what makes the tier a genuinely stored
    # --- choice instead of one silently reset back to the kind default whenever a unit changes hands. Removing
    # --- the guard reassigns Utility's own default even after a mutating test has moved it to Critical.
    Mutation(
        rule="the kind default must only be seeded when PowerTier is still Unassigned, never re-seeded",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor="        if (EquippableItem.PowerTier == PowerTiers.Unassigned)\n        {\n            var consumerTiers = Behaviors.OfType<IPowerConsumer>().Select(c => c.DefaultPowerTier).ToArray();\n            if (consumerTiers.Length > 0)\n                EquippableItem.PowerTier = consumerTiers.Min();\n        }",
        mutated="        {\n            var consumerTiers = Behaviors.OfType<IPowerConsumer>().Select(c => c.DefaultPowerTier).ToArray();\n            if (consumerTiers.Length > 0)\n                EquippableItem.PowerTier = consumerTiers.Min();\n        }",
        test="PowerTierTests.ATierChangeOnAnEquippedItemTakesEffectNextTickWithNoReEquip",
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
