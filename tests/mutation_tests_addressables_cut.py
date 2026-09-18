#!/usr/bin/env python3
"""Mutation tests for docs/addressables-cut.md Cut 2.

EngineAssetCheck (Assets/Scripts/Editor/EngineAssetCheck.cs) is the rule that keeps every stored
[CultInspectorAssetGuid] value honest. A check nobody can break is a check nobody can trust: each
case here mutates the *real* catalog (GameData/Aetheria.cc) or the source in one specific way the
cut promises to catch, runs EngineAssetCheck.Run in Unity batchmode, asserts it fails naming the
record, then restores the catalog/source exactly. A "control" case first proves the unmutated
catalog passes, so a case that "fails" for the wrong reason (a bad harness, not a caught mutation)
cannot hide.

Run from the repo root: python tests/mutation_tests_addressables_cut.py
Exit code 0 only if every case behaved as the cut's own verification section (Cut 2) demands.

This is a separate script rather than an addition to tests/mutation_tests.py: that file is the
item-provenance cut's own harness (docs/item-provenance-cut.md), a source-anchor-mutation-plus-
`dotnet test` runner with no path to driving a Unity batchmode check, which is what
EngineAssetCheck requires. A concurrent session sharing this working tree landed asset moves
under an unrelated commit message (see docs/addressables-cut.md history around 2026-09-18); this
file's name avoids repeating that collision on the one shared harness filename.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
CATALOG = REPO_ROOT / "GameData" / "Aetheria.cc"
UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
ITEM_DATA_CS = REPO_ROOT / "Assets" / "Scripts" / "ServerShared" / "ItemData.cs"
DIRECTORY_BUILD_PROPS = REPO_ROOT / "Directory.Build.props"

# A known-good record to mutate: HullData "LonginusX", whose Prefab already resolves (see the Cut 2
# migration report). Texture2D guid is its own Schematic (schema_Longinus.png), still a real,
# addressable asset, just the wrong type for Prefab (which wants a GameObject+EntityInstance).
TEXTURE_GUID = "51702555e534a7a4fb39eb105b80bbaf"
# A real, existing, non-addressable asset (a .cs script under Assets/Scripts, outside Assets/Content).
NON_ADDRESSABLE_GUID = "3f3cca818d26268459d28af1cdb469a5"
RANDOM_GUID = "deadbeefdeadbeefdeadbeefdeadbeef"
OLD_PATH = "Assets/Resources/Prefabs/Ships/Longinus.prefab"


def run(cmd, **kwargs):
    print(f"$ {' '.join(str(c) for c in cmd)}")
    return subprocess.run(cmd, capture_output=True, text=True, **kwargs)


def pinned_cultlib_revision() -> str:
    text = DIRECTORY_BUILD_PROPS.read_text(encoding="utf-8")
    match = re.search(r"<CultLibRevision>([0-9a-f]{40})</CultLibRevision>", text)
    if not match:
        raise SystemExit("could not find CultLibRevision in Directory.Build.props")
    return match.group(1)


def resolve_cultlib_root(scratch: Path) -> str:
    """The mutator (like the rest of the .NET build) must build against the exact CultLib
    revision Directory.Build.props pins, not whatever CultLib main happens to be on disk right
    now. Reuse CULTLIB_ROOT / the sibling checkout when it already matches; otherwise check out a
    detached scratch worktree at the pinned revision, same as the cut's own verification pass."""
    revision = pinned_cultlib_revision()

    candidates = []
    if os.environ.get("CULTLIB_ROOT"):
        candidates.append(Path(os.environ["CULTLIB_ROOT"]))
    candidates.append(REPO_ROOT.parent / "CultLib")

    for candidate in candidates:
        if not candidate.exists():
            continue
        head = run(["git", "-C", str(candidate), "rev-parse", "HEAD"]).stdout.strip()
        dirty = run(["git", "-C", str(candidate), "status", "--porcelain"]).stdout.strip()
        if head == revision and not dirty:
            return str(candidate)

    worktree = scratch / f"cultlib-{revision[:12]}"
    if not worktree.exists():
        result = run(["git", "-C", str(REPO_ROOT.parent / "CultLib"), "worktree", "add", "--detach", str(worktree), revision])
        if result.returncode != 0:
            print(result.stdout, result.stderr)
            raise SystemExit("failed to create pinned CultLib worktree for the mutator build")
    return str(worktree)


def build_mutator(build_dir: Path, cultlib_root: str) -> Path:
    build_dir.mkdir(parents=True, exist_ok=True)
    (build_dir / "Mutate.csproj").write_text(
        """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="%s" />
  </ItemGroup>
</Project>
""" % (REPO_ROOT / "Aetheria.Shared" / "Aetheria.Shared.csproj"),
        encoding="utf-8",
    )
    (build_dir / "Program.cs").write_text(
        r"""using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;

// Scratch-only (not landed): sets HullData "LonginusX".Prefab to an arbitrary string in a copy of
// the catalog, for tests/mutation_tests.py to feed to EngineAssetCheck and prove it fails.
internal static class Program
{
    private static int Main(string[] args)
    {
        var catalogPath = args[0];
        var newValue = args[1];
        var cache = AetheriaStores.Open(catalogPath, catalogWritable: true);
        try
        {
            var stored = cache.AllStoredDocuments.First(s =>
                s.Descriptor.DocumentType.Name == "HullData" &&
                (string)s.Descriptor.DocumentType.GetField("Name").GetValue(s.Document) == "LonginusX");
            var field = stored.Descriptor.DocumentType.GetField("Prefab", BindingFlags.Public | BindingFlags.Instance);
            field.SetValue(stored.Document, newValue);
            cache.UpsertAsync(stored.Descriptor.DocumentType, stored.Document, stored.Key).Wait();
            cache.FlushAllBackingStores();
            Console.WriteLine($"Set HullData \"LonginusX\".Prefab = \"{newValue}\"");
            return 0;
        }
        finally
        {
            cache.Dispose();
        }
    }
}
""",
        encoding="utf-8",
    )
    build = run([
        "dotnet", "build", str(build_dir / "Mutate.csproj"),
        "-c", "Release", "-o", str(build_dir / "out"),
        f"-p:CultLibRoot={cultlib_root}",
    ])
    if build.returncode != 0:
        print(build.stdout, build.stderr)
        raise SystemExit("mutator build failed")
    return build_dir / "out" / "Mutate.dll"


def mutate_catalog(mutator_dll: Path, scratch_catalog: Path, value: str):
    shutil.copyfile(CATALOG, scratch_catalog)
    result = run(["dotnet", str(mutator_dll), str(scratch_catalog), value])
    if result.returncode != 0:
        print(result.stdout, result.stderr)
        raise SystemExit("mutation failed to apply")


def run_engine_asset_check(log_path: Path) -> tuple[int, str]:
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-quit", "-projectPath", str(REPO_ROOT),
         "-executeMethod", "EngineAssetCheck.Run", "-logFile", str(log_path)],
        timeout=600,
    )
    text = log_path.read_text(encoding="utf-8", errors="replace")
    return proc.returncode, text


def assert_contains(log_text: str, needle: str, case_name: str):
    if needle not in log_text:
        raise SystemExit(f"[{case_name}] FAIL: expected log to contain {needle!r}; it did not")


def main():
    if not CATALOG.exists():
        raise SystemExit(f"catalog not found: {CATALOG}")

    original_catalog = CATALOG.read_bytes()
    original_item_data = ITEM_DATA_CS.read_text(encoding="utf-8")
    scratch = Path(tempfile.mkdtemp(prefix="aetheria-mutation-"))
    failures = []

    try:
        cultlib_root = resolve_cultlib_root(scratch)
        print(f"CultLibRoot for the mutator build: {cultlib_root}")
        mutator_dll = build_mutator(scratch / "mutator", cultlib_root)

        # Control: the real, unmutated catalog must pass with 0 failures. If this fails, every
        # other case below is meaningless (the harness itself, not the mutation, would be at fault).
        print("\n=== control: unmutated catalog ===")
        log = scratch / "control.log"
        code, text = run_engine_asset_check(log)
        if code != 0 or "0 failure" not in text and "stored reference(s) OK" not in text:
            failures.append("control: expected exit 0 and a clean report on the real catalog")
        else:
            print("control OK: exit 0, no failures")

        cases = [
            ("wrong-type (Texture2D guid for a Prefab field)", TEXTURE_GUID, "not a GameObject"),
            ("non-addressable guid (real asset outside Assets/Content)", NON_ADDRESSABLE_GUID, "is not addressable"),
            ("random 32-hex guid (no such asset)", RANDOM_GUID, "names no existing file"),
            ("old Resources path (pre-migration form)", OLD_PATH, "not 32 lowercase hex characters"),
        ]

        for name, value, expect in cases:
            print(f"\n=== mutation: {name} ===")
            try:
                mutated = scratch / "mutated.cc"
                mutate_catalog(mutator_dll, mutated, value)
                shutil.copyfile(mutated, CATALOG)

                log = scratch / (name.split()[0] + ".log")
                code, text = run_engine_asset_check(log)

                if code == 0:
                    failures.append(f"{name}: EngineAssetCheck exited 0; the mutation was not caught")
                    continue
                if "LonginusX" not in text:
                    failures.append(f"{name}: failure did not name the record (LonginusX)")
                    continue
                assert_contains(text, expect, name)
                print(f"OK: caught, exit {code}, named LonginusX, mentioned {expect!r}")
            except SystemExit as exc:
                failures.append(f"{name}: {exc}")
            finally:
                CATALOG.write_bytes(original_catalog)

        # Missing consumer contract: a new [CultInspectorAssetGuid] member EngineAssetCheck's table
        # doesn't know about must fail loudly, not be silently skipped.
        print("\n=== mutation: attributed member missing from the consumer contract table ===")
        try:
            marker = "    [Inspectable, JsonProperty(\"hardpoints\"), Key(23)]  \n    public List<HardpointData> Hardpoints = new List<HardpointData>();"
            replacement = marker + (
                "\n\n    // Scratch mutation (tests/mutation_tests.py): an attributed member with no "
                "row in EngineAssetCheck.ConsumerContracts. Must fail the check, not be skipped.\n"
                "    [Inspectable, CultInspectorAssetGuid, JsonProperty(\"scratchMutation\"), Key(30)]\n"
                f"    public string ScratchMutationField = \"{TEXTURE_GUID}\";"
            )
            if marker not in original_item_data:
                raise SystemExit("anchor text for the scratch field insertion was not found in ItemData.cs")
            mutated_source = original_item_data.replace(marker, replacement, 1)
            ITEM_DATA_CS.write_text(mutated_source, encoding="utf-8")

            log = scratch / "missing-contract.log"
            code, text = run_engine_asset_check(log)
            if code == 0:
                failures.append("missing-contract: EngineAssetCheck exited 0; an untabled member was not caught")
            elif "ScratchMutationField" not in text or "no consumer contract registered" not in text:
                failures.append("missing-contract: failure did not name the untabled member and reason")
            else:
                print("OK: caught, exit", code, "named ScratchMutationField with no registered contract")
        finally:
            ITEM_DATA_CS.write_text(original_item_data, encoding="utf-8")

        # Restoration proof: after reverting the source, a fresh batchmode compile + check must be
        # clean again — the mutation must leave no residue once undone.
        print("\n=== restoration check: reverted source recompiles clean ===")
        log = scratch / "restored.log"
        code, text = run_engine_asset_check(log)
        if code != 0:
            failures.append("restoration: EngineAssetCheck did not exit 0 after reverting the scratch field")
        else:
            print("OK: restored source compiles and the check is clean again")

    finally:
        CATALOG.write_bytes(original_catalog)
        ITEM_DATA_CS.write_text(original_item_data, encoding="utf-8")
        shutil.rmtree(scratch, ignore_errors=True)
        print(f"\nrestored {CATALOG} and {ITEM_DATA_CS} to their original content.")

    if failures:
        print("\n=== MUTATION TESTS FAILED ===")
        for f in failures:
            print(" -", f)
        return 1

    print("\n=== all mutation tests passed ===")
    return 0


if __name__ == "__main__":
    sys.exit(main())
