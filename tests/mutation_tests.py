#!/usr/bin/env python3
"""Mutation tests for the Cut B and Cut C provenance rules (docs/item-provenance-cut.md).

For each rule this applies its named mutation to an exact, unique anchor of
source text, runs the one test that should catch it, restores the file
byte-for-byte, and reports whether the mutant was killed. A no-op control
mutation (anchor == replacement) proves the read/replace/write/restore path
itself is transparent: every test it touches must stay green.

Usage:
    python tests/mutation_tests.py --cultlib-root <path-to-CultLib-a0813c6-worktree>

Requires the CultLib worktree described in docs/item-provenance-cut.md section 6.
Only touches files under this repository; every mutation is reversed before
the script exits, including on failure.
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
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor='    [Key(0)] public int NextLot = 1;',
        mutated='    [Key(0)] public int NextLot = 1;',
        test="RunSaveTests.MissingLotIsLoud",
        expect="green",
    ),
    # --- MissingLotIsLoud: the indexer and Reachable must throw on an absent lot ---
    Mutation(
        rule="ProvenanceLedger indexer throws on an absent id",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor=(
            "    public Lot this[int id]\n"
            "    {\n"
            "        get\n"
            "        {\n"
            "            if (!Lots.TryGetValue(id, out var lot))\n"
            "                throw new InvalidOperationException($\"Lot {id} is not in the provenance ledger.\");\n"
            "            return lot;\n"
            "        }\n"
            "    }"
        ),
        mutated=(
            "    public Lot this[int id]\n"
            "    {\n"
            "        get\n"
            "        {\n"
            "            Lots.TryGetValue(id, out var lot);\n"
            "            return lot;\n"
            "        }\n"
            "    }"
        ),
        test="RunSaveTests.MissingLotIsLoud",
        expect="red",
    ),
    Mutation(
        rule="Reachable throws on a missing root instead of skipping it",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor="            var lot = this[id];\n            reached[id] = lot;",
        mutated="            if (!Lots.TryGetValue(id, out var lot)) continue;\n            reached[id] = lot;",
        test="RunSaveTests.MissingLotIsLoud",
        expect="red",
    ),
    # --- MatchingHardpointsShareALot: the second unit must mint from the first unit's lot ---
    Mutation(
        rule="matching hardpoints share a lot, not a fresh mint",
        file="Assets/Scripts/ServerShared/LoadoutGenerator.cs",
        anchor=(
            "                    // Matching hardpoints carry matching units: same lot\n"
            "                    var item = (previousItem != null\n"
            "                        ? ItemManager.CreateInstance(previousItem.EquippableItem.Lot)\n"
            "                        : ItemManager.CreateInstance(entry.product)) as EquippableItem;"
        ),
        mutated=(
            "                    // Matching hardpoints carry matching units: same lot\n"
            "                    var item = ItemManager.CreateInstance(entry.product) as EquippableItem;"
        ),
        test="LoadoutTests.MatchingHardpointsShareALot",
        expect="red",
    ),
    # --- FirstAvailableProductInKeyOrderBuildsTheSlot: Brand and Materialize must both respect key order ---
    Mutation(
        rule="Brand matches the lot's maker, not just the design",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor=(
            "        var product = ItemData.GetAll<FactionProductData>()\n"
            "            .Where(p => p.Manufacturer.Key.Equals(maker.Key) && p.Design.Key.Equals(item.Data.Key))"
        ),
        mutated=(
            "        var product = ItemData.GetAll<FactionProductData>()\n"
            "            .Where(p => p.Design.Key.Equals(item.Data.Key))"
        ),
        test="LoadoutTests.FirstAvailableProductInKeyOrderBuildsTheSlot",
        expect="red",
    ),
    Mutation(
        rule="Materialize resolves a design's product in record-key order",
        file="Assets/Scripts/ServerShared/Loadout.cs",
        anchor="var products = cache.GetAll<FactionProductData>().OrderBy(p => cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();",
        mutated="var products = cache.GetAll<FactionProductData>().ToArray();",
        test="LoadoutTests.FirstAvailableProductInKeyOrderBuildsTheSlot",
        expect="red",
    ),
    # --- BrandPicksFirstProductInKeyOrderForSameMakerAndDesign: Brand's own tie-break must be ascending key order ---
    Mutation(
        rule="Brand's tie-break among a maker's products for one design is ascending record-key order",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="            .OrderBy(p => ItemData.RefOf(p).Key.Value, StringComparer.Ordinal)",
        mutated="            .OrderByDescending(p => ItemData.RefOf(p).Key.Value, StringComparer.Ordinal)",
        test="LoadoutTests.BrandPicksFirstProductInKeyOrderForSameMakerAndDesign",
        expect="red",
    ),
    # --- StatsReadTheLot: stats must read the lot, including its per-role fills ---
    Mutation(
        rule="Evaluate reads the lot's per-role fill for a role stat",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="var quality = pow(lot.QualityForRole(stat.FromRole), stat.QualityExponent);",
        mutated="var quality = pow(lot.Quality, stat.QualityExponent);",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    Mutation(
        rule="QualityForRole falls back to the lot's own workmanship, not a fixed value",
        file="Assets/Scripts/ServerShared/Provenance.cs",
        anchor="        if (string.IsNullOrEmpty(role) || Roles == null) return Quality;",
        mutated="        if (string.IsNullOrEmpty(role) || Roles == null) return 1f;",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    # --- F2 (item-provenance-cut.md): the GC root walk must cover every EntityPack root ---
    Mutation(
        rule="Items yields the Equipment root, not just the hull",
        file="Assets/Scripts/ServerShared/EntitySerializer.cs",
        anchor=(
            "        yield return pack.Hull;\n"
            "        foreach (var (_, item) in pack.Equipment) yield return item;"
        ),
        mutated="        yield return pack.Hull;",
        test="LoadoutTests.MaterializedLotsSurviveSaveAndReload",
        expect="red",
    ),
    # --- F3: GetTier and CreateInstance(int) must read/preserve the lot's real quality ---
    Mutation(
        rule="GetTier reads the lot's quality, not a fixed value",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="        var quality = GetLot(item).Quality;",
        mutated="        var quality = .999f;",
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    Mutation(
        rule="CreateInstance(int) does not re-roll the lot's quality",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor=(
            "    public CraftedItemInstance CreateInstance(int lot)\n"
            "    {\n"
            "        var l = Lots[lot];"
        ),
        mutated=(
            "    public CraftedItemInstance CreateInstance(int lot)\n"
            "    {\n"
            "        var l = Lots[lot];\n"
            "        l.Quality = RollQuality();"
        ),
        test="LoadoutTests.StatsReadTheLot",
        expect="red",
    ),
    # --- F4: GetPrice, the durability/thermal exponent paths, role fills, Produced brand, multi-zone roots ---
    Mutation(
        rule="GetPrice reads the lot's quality, not a fixed value",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="        return (int) (GameplaySettings.QualityPriceModifier.Evaluate(GetLot(item).Quality) * data.Price);",
        mutated="        return (int) (GameplaySettings.QualityPriceModifier.Evaluate(.5f) * data.Price);",
        test="LoadoutTests.GetPriceReadsLotQuality",
        expect="red",
    ),
    Mutation(
        rule="Evaluate's durability exponent reads the lot's quality",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor=(
            "        var durabilityExponent = lerp(\n"
            "            GameplaySettings.DurabilityQualityMin,\n"
            "            GameplaySettings.DurabilityQualityMax,\n"
            "            pow(lot.Quality, GameplaySettings.DurabilityQualityExponent));"
        ),
        mutated=(
            "        var durabilityExponent = lerp(\n"
            "            GameplaySettings.DurabilityQualityMin,\n"
            "            GameplaySettings.DurabilityQualityMax,\n"
            "            .5f);"
        ),
        test="LoadoutTests.EvaluateDurabilityExponentReadsLotQuality",
        expect="red",
    ),
    Mutation(
        rule="EquippedItem's thermal exponent reads the lot's quality",
        file="Assets/Scripts/ServerShared/Entity.cs",
        anchor=(
            "        ThermalExponent = lerp(\n"
            "            ItemManager.GameplaySettings.ThermalQualityMin,\n"
            "            ItemManager.GameplaySettings.ThermalQualityMax,\n"
            "            pow(Lot.Quality, ItemManager.GameplaySettings.ThermalQualityExponent));"
        ),
        mutated=(
            "        ThermalExponent = lerp(\n"
            "            ItemManager.GameplaySettings.ThermalQualityMin,\n"
            "            ItemManager.GameplaySettings.ThermalQualityMax,\n"
            "            .5f);"
        ),
        test="LoadoutTests.EquippedItemThermalExponentReadsLotQuality",
        expect="red",
    ),
    Mutation(
        rule="CreateLot(product) fills roles from the product's own spread, not the design default",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor="                var build = product.Roles?.FirstOrDefault(b => b.Role == role.Name) ?? new ProductRole();",
        mutated="                var build = new ProductRole();",
        test="LoadoutTests.CreateLotFillsRolesFromProductSpread",
        expect="red",
    ),
    Mutation(
        rule="Brand reads the maker off a Produced origin too, not only Attributed",
        file="Assets/Scripts/ServerShared/ItemManager.cs",
        anchor=(
            "        var maker = lot.Origin switch\n"
            "        {\n"
            "            Attributed attributed => attributed.Faction,\n"
            "            Produced produced => produced.Faction,\n"
            "            _ => default\n"
            "        };"
        ),
        mutated=(
            "        var maker = lot.Origin switch\n"
            "        {\n"
            "            Attributed attributed => attributed.Faction,\n"
            "            _ => default\n"
            "        };"
        ),
        test="LoadoutTests.BrandReadsProducedFaction",
        expect="red",
    ),
    Mutation(
        rule="RunSave.Commit takes GC roots from every zone, not just the first",
        file="Assets/Scripts/ServerShared/SavedGame.cs",
        anchor="        var roots = zones\n            .SelectMany(zone => zone.Contents?.Entities ?? new List<EntityPack>())",
        mutated="        var roots = zones.Take(1)\n            .SelectMany(zone => zone.Contents?.Entities ?? new List<EntityPack>())",
        test="RunSaveTests.CommitTakesRootsFromEveryZone",
        expect="red",
    ),
    # --- Cut C: the manufacturer moves from the design to the product; the catalog fixture and its
    # round-trip assertion (FactionIsASingletonInstance) must actually depend on that wiring. ---
    Mutation(
        rule="the catalog fixture's product carries the maker, not an unset reference",
        file="tests/Aetheria.Shared.Tests/AetheriaStoresTests.cs",
        anchor=(
            'cache.Upsert(new FactionProductData { Name = "Lance", '
            "Design = new CultRecordRef<CraftedItemData>(lance.Key), Manufacturer = cache.RefOf(faction) });"
        ),
        mutated=(
            'cache.Upsert(new FactionProductData { Name = "Lance", '
            "Design = new CultRecordRef<CraftedItemData>(lance.Key), Manufacturer = default });"
        ),
        test="AetheriaStoresTests.FactionIsASingletonInstance",
        expect="red",
    ),
]


def run_test(cultlib_root: str, filter_expr: str) -> tuple[str, str]:
    """Runs the one filtered test. Returns ("ERROR", ...) when the build itself failed (a
    compile error is never a legitimate kill or survival — it means the mutation broke
    something the test never got a chance to exercise), otherwise ("PASS"|"FAIL", output)."""
    proc = subprocess.run(
        [
            "dotnet", "test", str(TEST_PROJECT),
            f"-p:CultLibRoot={cultlib_root}",
            "--filter", f"FullyQualifiedName~{filter_expr}",
            "--nologo",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
    )
    output = proc.stdout + proc.stderr
    # A build failure never reaches "Test run for ..."/"Passed!"/"Failed!"; it reports
    # "Build FAILED." and one or more "error CS..." lines instead.
    if "Build FAILED" in output or "error CS" in output:
        return "ERROR", output
    passed = proc.returncode == 0 and "Failed!" not in output
    return ("PASS" if passed else "FAIL"), output


def detect_newline(original: bytes) -> bytes:
    return b"\r\n" if b"\r\n" in original else b"\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cultlib-root", required=True)
    args = parser.parse_args()

    results = []
    failures = []

    for m in MUTATIONS:
        path = REPO_ROOT / m.file
        original = path.read_bytes()
        newline = detect_newline(original).decode("ascii")
        # Anchors are authored with plain "\n"; translate to the file's own line ending
        # before matching, so CRLF-checked-out files (this repo's default) still match.
        anchor = m.anchor.replace("\n", newline)
        mutated = m.mutated.replace("\n", newline)
        text = original.decode("utf-8")
        count = text.count(anchor)
        if count != 1:
            print(f"[ABORT] anchor for '{m.rule}' matches {count} times in {m.file}, expected exactly 1")
            failures.append(m.rule)
            continue

        mutated_text = text.replace(anchor, mutated, 1)
        path.write_bytes(mutated_text.encode("utf-8"))
        try:
            status_code, output = run_test(args.cultlib_root, m.test)
        finally:
            # Reverse write: restore the exact original bytes regardless of outcome.
            path.write_bytes(original)
            restored = path.read_bytes()
            if restored != original:
                print(f"[FATAL] {m.file} did not restore byte-exact after '{m.rule}'!")
                return 2

        if status_code == "ERROR":
            status = "ERROR (build failure)"
            failures.append(m.rule)
        elif m.expect == "red":
            killed = status_code == "FAIL"
            status = "KILLED" if killed else "SURVIVED (bad)"
            if not killed:
                failures.append(m.rule)
        else:  # control: must stay green
            passed = status_code == "PASS"
            status = "GREEN (ok)" if passed else "RED (bad: round trip corrupted something)"
            if not passed:
                failures.append(m.rule)

        results.append((m.rule, m.test, status))
        print(f"{status:22} | {m.test:55} | {m.rule}")

    print()
    print("Summary:")
    for rule, test, status in results:
        print(f"  {status:22} {test:55} {rule}")

    if failures:
        print(f"\n{len(failures)} mutation(s) did not behave as expected:")
        for f in failures:
            print(f"  - {f}")
        return 1

    print(f"\nAll {len(results)} mutations behaved as expected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
