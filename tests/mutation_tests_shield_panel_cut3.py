#!/usr/bin/env python3
"""Mutation tests for docs/shield-panel-cut.md Cut 3 (the interceptor presenter and panel pool).

Assets/Scripts/Editor/ShieldPanelCut3Verify.cs is the batchmode probe Cut 3 added, for the same
reason Cut 1 and Cut 2 have one (the map's Q7): Tests.asmdef cannot see Assembly-CSharp. Unlike
Cut 1/2's probes this one drives actual GraphicsBuffer dispatches (it steps ShieldPanel's private
Update() by reflection, since Cut 1 deliberately removed [ExecuteAlways] -- D13), so it must run
WITHOUT -nographics.

Each case here mutates the *real* source in one specific way the cut's own text says it fixed or
established as a rule, runs ShieldPanelCut3Verify.Run in Unity batchmode, asserts it goes red for
the reason the cut names, then restores the source exactly. A control case first proves the
unmutated tree passes, so a case that "fails" for the wrong reason (a broken harness, not a caught
mutation) cannot hide.

This is a separate script rather than an addition to mutation_tests_shield_panel_cut1.py or
_cut2.py (each of those owns its own cut's rules and touches only that cut's files) or
mutation_tests.py (the item-provenance cut's headless dotnet-test runner, with no path to a Unity
batchmode check). It touches only files Cut 3 owns: ShieldPanel.cs (the D17/D20/pooling fixes),
Tilings.cs (D21), and ShieldInterceptor.cs (reuse/exhaustion).

Run from the repo root: python tests/mutation_tests_shield_panel_cut3.py
Exit code 0 only if every case behaved as Cut 3's verification section demands.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"

SHIELD_FIELD = REPO_ROOT / "Assets" / "Shaders" / "Compute" / "ShieldField"
SHIELD_PANEL_CS = SHIELD_FIELD / "ShieldPanel.cs"
TILINGS_CS = SHIELD_FIELD / "Tilings.cs"
SHIELD_INTERCEPTOR_CS = REPO_ROOT / "Assets" / "Scripts" / "Gameplay" / "ShieldInterceptor.cs"


def run_probe(log_path: Path) -> tuple[int, str]:
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-quit", "-projectPath", str(REPO_ROOT),
         "-executeMethod", "Aetheria.EditorTools.ShieldPanelCut3Verify.Run",
         "-logFile", str(log_path)],
        timeout=600,
    )
    text = log_path.read_text(encoding="utf-8", errors="replace")
    return proc.returncode, text


def replace_unique(path: Path, anchor: str, mutated: str) -> None:
    original = path.read_text(encoding="utf-8")
    count = original.count(anchor)
    if count != 1:
        raise SystemExit(f"{path}: anchor must occur exactly once, found {count}\n---\n{anchor}")
    path.write_text(original.replace(anchor, mutated), encoding="utf-8")


def main() -> int:
    scratch = REPO_ROOT / "tests" / "_scratch_shield_panel_cut3"
    scratch.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []

    original_panel = SHIELD_PANEL_CS.read_text(encoding="utf-8")
    original_tilings = TILINGS_CS.read_text(encoding="utf-8")
    original_interceptor = SHIELD_INTERCEPTOR_CS.read_text(encoding="utf-8")

    def restore_all() -> None:
        SHIELD_PANEL_CS.write_text(original_panel, encoding="utf-8")
        TILINGS_CS.write_text(original_tilings, encoding="utf-8")
        SHIELD_INTERCEPTOR_CS.write_text(original_interceptor, encoding="utf-8")

    try:
        # --- control: the real, unmutated tree must pass ---
        print("\n=== control: unmutated tree ===")
        code, text = run_probe(scratch / "control.log")
        if code != 0 or "[ShieldPanelCut3Verify] PASS" not in text:
            failures.append("control: expected exit 0 and PASS on the unmutated tree")
        else:
            print("control OK: exit 0, PASS")

        # --- D20: reverting the backdated arm instant to the pre-Cut-3 hardcoded 0f does NOT
        # swallow the spawn-frame hit itself (that's the seeded bState, D17's own fix, still doing
        # its job for frame 1) -- it reopens a narrower, one-frame-later symptom: KUpdate resets
        # growth back to 0 at the end of frame 1, so frame 2's own injection is what gets gated.
        # This is exactly why the probe checks frame 2 separately from frame 1. ---
        print("\n=== mutation: D20 regression (panelSpawnTime hardcoded back to 0f) ===")
        try:
            replace_unique(
                SHIELD_PANEL_CS,
                'sim.SetFloat("_PanelSpawnTime", panelSpawnTime);',
                'sim.SetFloat("_PanelSpawnTime", 0f);',
            )
            code, text = run_probe(scratch / "d17_spawntime.log")
            caught = code != 0 and "D20 not fixed" in text
            if not caught:
                failures.append("D20 regression (spawn time): probe did not go red naming D20")
            else:
                print("OK: caught — probe named D20 not fixed")
        finally:
            restore_all()

        # --- D17: the other half of the fix -- if growth is never seeded ahead of time (left at
        # KInit's 0), the gate blocks the spawn-frame hit even with panelSpawnTime correct. ---
        print("\n=== mutation: D17 regression (growth seed always 0) ===")
        try:
            replace_unique(
                SHIELD_PANEL_CS,
                "growthSeed[i].growth = Mathf.Clamp01(\n"
                "                    (growDuration - data.cells[i].spawnDelay) / Mathf.Max(growDuration, 1e-3f));",
                "growthSeed[i].growth = 0f; // mutation_tests_shield_panel_cut3.py: reintroduce D17",
            )
            code, text = run_probe(scratch / "d17_seed.log")
            caught = code != 0 and "D17 not fixed" in text
            if not caught:
                failures.append("D17 regression (growth seed): probe did not go red naming D17")
            else:
                print("OK: caught — probe named D17 not fixed")
        finally:
            restore_all()

        # --- Pooling: reverting OnEnable/OnDisable to the pre-Cut-3 unconditional Rebuild/
        # ReleaseAll must rebuild the tiling on every pooled reuse, which the pool exists to avoid
        # (§2.4: 1.3-21ms vs 8-11us) -- CheckPoolReuseDoesNotRebuild is the rule that pins this. ---
        print("\n=== mutation: pooling regression (OnEnable rebuilds every reuse again) ===")
        try:
            replace_unique(
                SHIELD_PANEL_CS,
                "void OnEnable() { if (data == null) Rebuild(); }\n"
                "        void OnDisable() { }\n"
                "        void OnDestroy() { ReleaseAll(); }",
                "void OnEnable() { Rebuild(); } // mutation_tests_shield_panel_cut3.py: reintroduce the pre-Cut-3 rebuild-on-every-reuse bug\n"
                "        void OnDisable() { ReleaseAll(); }",
            )
            code, text = run_probe(scratch / "pooling.log")
            caught = code != 0 and "OnEnable is rebuilding (or OnDisable is releasing)" in text
            if not caught:
                failures.append("pooling regression: probe did not go red naming the rebuild-on-reuse regression")
            else:
                print("OK: caught — probe named the rebuild-on-reuse regression")
        finally:
            restore_all()

        # --- Reuse: a second hit inside a living panel must strike it, not spawn a new one. Force
        # FindReusable to always miss and the rule collapses -- the reuse probe must see live count
        # go to 2 on the second (nearby) strike instead of staying at 1. ---
        print("\n=== mutation: reuse regression (FindReusable always misses) ===")
        try:
            replace_unique(
                SHIELD_INTERCEPTOR_CS,
                "    int FindReusable(Vector3 point)\n    {\n        float threshold = PanelRadius * ReuseFraction;",
                "    int FindReusable(Vector3 point)\n    {\n        return -1; // mutation_tests_shield_panel_cut3.py: never reuse\n"
                "        float threshold = PanelRadius * ReuseFraction;",
            )
            code, text = run_probe(scratch / "reuse.log")
            caught = code != 0 and "spawned a new panel" in text
            if not caught:
                failures.append("reuse regression: probe did not go red naming the spurious new panel")
            else:
                print("OK: caught — probe named the spurious new panel")
        finally:
            restore_all()

        # --- Pool exhaustion: MaxLivePanels must cap the live set. Removing the eviction branch (so
        # exhaustion always spawns a fresh instance instead of recycling the oldest) must blow the cap. ---
        print("\n=== mutation: exhaustion regression (cap never enforced) ===")
        try:
            replace_unique(
                SHIELD_INTERCEPTOR_CS,
                "        ShieldPanel panel;\n"
                "        Prototype panelProto;\n"
                "        if (_live.Count >= MaxLivePanels)\n",
                "        ShieldPanel panel;\n"
                "        Prototype panelProto;\n"
                "        if (false) // mutation_tests_shield_panel_cut3.py: never evict, blow the cap\n",
            )
            code, text = run_probe(scratch / "exhaustion.log")
            caught = code != 0 and "the cap did not hold" in text
            if not caught:
                failures.append("exhaustion regression: probe did not go red naming the blown cap")
            else:
                print("OK: caught — probe named the blown cap")
        finally:
            restore_all()

        # --- D21: reverting the Round-to-nearest fix to the pre-Cut-3 CeilToInt(...) + 1 formula
        # must blow past the small-factor bound again (24x/8x order-of-magnitude, not 2-5x). ---
        print("\n=== mutation: D21 regression (Penrose iteration count reverts to Ceil+1) ===")
        try:
            replace_unique(
                TILINGS_CS,
                "            int iterations = Mathf.Clamp(\n"
                "                Mathf.RoundToInt(Mathf.Log(Mathf.Max(radius / Mathf.Max(cellSize, 1e-4f), 1f), Phi)),\n"
                "                1, 12);",
                "            int iterations = Mathf.Clamp(\n"
                "                Mathf.CeilToInt(Mathf.Log(Mathf.Max(radius / Mathf.Max(cellSize, 1e-4f), 1f), Phi)) + 1,\n"
                "                1, 12); // mutation_tests_shield_panel_cut3.py: reintroduce D21's overshoot",
            )
            code, text = run_probe(scratch / "d21.log")
            caught = code != 0 and "not within a small factor" in text
            if not caught:
                failures.append("D21 regression: probe did not go red naming the small-factor violation")
            else:
                print("OK: caught — probe named the small-factor violation")
        finally:
            restore_all()

    finally:
        # belt-and-suspenders: never leave a mutated file behind even if something above threw
        restore_all()

    print("\n=== summary ===")
    if failures:
        for f in failures:
            print(f"FAIL: {f}")
        return 1
    print(f"All cases behaved as Cut 3's verification section demands. Logs: {scratch}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
