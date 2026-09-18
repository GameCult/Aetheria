#!/usr/bin/env python3
"""Mutation tests for docs/shield-panel-cut.md Cut 1 (the shield-panel source landing).

Assets/Scripts/Editor/ShieldPanelCut1Verify.cs is the batchmode probe Cut 1 added because no
NUnit coverage can reach these files: Tests.asmdef cannot see Assembly-CSharp (the map's Q7), and
the rules it defends are Unity-native (shader/compute compile errors, cell counts a compute
kernel would read back). A probe nobody can break is a probe nobody can trust, so each case here
mutates the *real* source in one specific way the cut's own text says it fixed, runs
ShieldPanelCut1Verify.Run in Unity batchmode, asserts it goes red for the reason the cut names,
then restores the source exactly. A control case first proves the unmutated tree passes, so a
case that "fails" for the wrong reason (a broken harness, not a caught mutation) cannot hide.

This is a separate script rather than an addition to tests/mutation_tests.py (the item-provenance
cut's source-anchor-mutation-plus-`dotnet test` runner, with no path to driving a Unity batchmode
check) or tests/mutation_tests_addressables_cut.py (EngineAssetCheck's own harness, same reason
this one exists, different rule). It touches only files this cut owns.

Run from the repo root: python tests/mutation_tests_shield_panel_cut1.py
Exit code 0 only if every case behaved as Cut 1's verification section demands.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"

SHIELD_FIELD = REPO_ROOT / "Assets" / "Shaders" / "Compute" / "ShieldField"
TILINGS_CS = SHIELD_FIELD / "Tilings.cs"
SHIELD_COMMON_HLSL = SHIELD_FIELD / "ShieldCommon.hlsl"
SHIELD_PANEL_CS = SHIELD_FIELD / "ShieldPanel.cs"


def run_probe(log_path: Path) -> tuple[int, str]:
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-nographics", "-quit", "-projectPath", str(REPO_ROOT),
         "-executeMethod", "Aetheria.EditorTools.ShieldPanelCut1Verify.Run",
         "-logFile", str(log_path)],
        timeout=600,
    )
    text = log_path.read_text(encoding="utf-8", errors="replace")
    return proc.returncode, text


def replace_unique(path: Path, anchor: str, mutated: str) -> str:
    original = path.read_text(encoding="utf-8")
    count = original.count(anchor)
    if count != 1:
        raise SystemExit(f"{path}: anchor must occur exactly once, found {count}\n---\n{anchor}")
    path.write_text(original.replace(anchor, mutated), encoding="utf-8")
    return original


def main() -> int:
    scratch = REPO_ROOT / "tests" / "_scratch_shield_panel_cut1"
    scratch.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []

    original_tilings = TILINGS_CS.read_text(encoding="utf-8")
    original_common = SHIELD_COMMON_HLSL.read_text(encoding="utf-8")
    original_panel = SHIELD_PANEL_CS.read_text(encoding="utf-8")

    try:
        # --- control: the real, unmutated tree must pass ---
        print("\n=== control: unmutated tree ===")
        code, text = run_probe(scratch / "control.log")
        if code != 0 or "[ShieldPanelCut1Verify] PASS" not in text:
            failures.append("control: expected exit 0 and PASS on the unmutated tree")
        else:
            print("control OK: exit 0, PASS")

        # --- D18: Penrose must stop expressing panelRadius if the derivation is reverted ---
        print("\n=== mutation: D18 regression (Penrose iterations decoupled from radius again) ===")
        anchor = (
            "            int iterations = Mathf.Clamp(\n"
            "                Mathf.CeilToInt(Mathf.Log(Mathf.Max(radius / Mathf.Max(cellSize, 1e-4f), 1f), Phi)) + 1,\n"
            "                1, 12);\n"
            "            return Penrose(radius, iterations);"
        )
        mutated = (
            "            // mutation_tests_shield_panel_cut1.py: reintroduce the D18 bug — a fixed\n"
            "            // iteration count, independent of radius or cellSize.\n"
            "            return Penrose(radius, 5);"
        )
        try:
            replace_unique(TILINGS_CS, anchor, mutated)
            code, text = run_probe(scratch / "d18.log")
            if code == 0 or "D18 not fixed" not in text:
                failures.append("D18 regression: probe did not go red naming the D18 regression")
            else:
                print("OK: caught — probe exited non-zero and named the D18 regression")
        finally:
            TILINGS_CS.write_text(original_tilings, encoding="utf-8")

        # --- D4: `centroid` is a reserved word in the shader/compute compiler; reintroducing it as
        # a CellStatic field name must produce a compile error somewhere it is used (ShieldCommon.hlsl
        # is shared by both ShieldSim.compute and ShieldPanel.shader) ---
        print("\n=== mutation: D4 regression (centroid field name reintroduced) ===")
        anchor = "struct CellStatic\n{\n    float2 center;"
        mutated = "struct CellStatic\n{\n    float2 centroid;"
        try:
            replace_unique(SHIELD_COMMON_HLSL, anchor, mutated)
            # Every use of `.center` on a CellStatic instance across the two GPU-side consumers
            # must track the field rename for this mutation to compile-check the reserved name
            # (not just silently fail on a dangling reference to a field that no longer exists
            # under that name — the point is the *reserved word*, not a typo).
            sim_cs_path = SHIELD_FIELD / "ShieldSim.compute"
            shader_path = SHIELD_FIELD / "ShieldPanel.shader"
            orig_sim = sim_cs_path.read_text(encoding="utf-8")
            orig_shader = shader_path.read_text(encoding="utf-8")
            sim_cs_path.write_text(orig_sim.replace("c.center", "c.centroid"), encoding="utf-8")
            shader_path.write_text(
                orig_shader.replace("x.c.center", "x.c.centroid").replace("nc.center", "nc.centroid"),
                encoding="utf-8",
            )
            try:
                code, text = run_probe(scratch / "d4.log")
                caught = ("Compute shader error" in text or "Shader error" in text) and "centroid" in text
                if code == 0 or not caught:
                    failures.append("D4 regression: probe did not go red naming the reserved 'centroid' token")
                else:
                    print("OK: caught — probe exited non-zero and named the reserved 'centroid' token")
            finally:
                sim_cs_path.write_text(orig_sim, encoding="utf-8")
                shader_path.write_text(orig_shader, encoding="utf-8")
        finally:
            SHIELD_COMMON_HLSL.write_text(original_common, encoding="utf-8")

        # --- D2/D5: RenderParams.shaderPass does not exist in 6000.3.24f1; reintroducing it must
        # fail the C# compile, not silently resurrect the deleted per-pass draw path ---
        print("\n=== mutation: D2 regression (RenderParams.shaderPass reintroduced) ===")
        anchor = "            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, argsFill, 1, 0);\n        }"
        mutated = "            rp.shaderPass = 0;\n            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, argsFill, 1, 0);\n        }"
        try:
            replace_unique(SHIELD_PANEL_CS, anchor, mutated)
            code, text = run_probe(scratch / "d2.log")
            if code == 0:
                failures.append("D2 regression: batchmode exited 0 despite a nonexistent RenderParams.shaderPass member")
            else:
                print("OK: caught — batchmode failed to compile with shaderPass reintroduced")
        finally:
            SHIELD_PANEL_CS.write_text(original_panel, encoding="utf-8")

    finally:
        # belt-and-suspenders: never leave a mutated file behind even if something above threw
        TILINGS_CS.write_text(original_tilings, encoding="utf-8")
        SHIELD_COMMON_HLSL.write_text(original_common, encoding="utf-8")
        SHIELD_PANEL_CS.write_text(original_panel, encoding="utf-8")

    print("\n=== summary ===")
    if failures:
        for f in failures:
            print(f"FAIL: {f}")
        return 1
    print(f"All cases behaved as Cut 1's verification section demands. Logs: {scratch}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
