#!/usr/bin/env python3
"""Mutation tests for the flow-shader-parity rule (campaign aetheria-fauna, cut fauna-flow-field).

Assets/Scripts/Editor/FlowFieldParityVerify.cs evaluates the HLSL globalFlow and slopeFlow
(Assets/Shaders/Volumetric.cginc, through FlowParity.compute) and FlowField (ServerShared) for the
same positions, scrolls, settings and gradients, after VolumeSampling.LowerFlow has lowered a stepped
Zone's flow into the shader globals. Tests.asmdef cannot see Assembly-CSharp and the check needs a GPU,
so it runs here, in Unity batchmode WITHOUT -nographics (see ShieldPanelCut3Verify.cs).

A control run proves the unmutated tree passes. Then each mutant edits the real source in one way a
rule of the cut forbids, runs the check, asserts it goes red naming the check that owns the rule, and
restores the file byte for byte. A mutant that fails for the wrong reason (a broken harness) cannot
hide behind the control.

Run from the repo root: py -3 tests/mutation_tests_flow_parity.py [--only <label>[,<label>]]
Exit code 0 only if the control passed and every mutant was caught for its reason. Run one Unity at a
time: never start this while another Unity process is running.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"

CGINC = "Assets/Shaders/Volumetric.cginc"
VOLUME_SAMPLING = "Assets/Scripts/Zone Display/VolumeSampling.cs"
FLOW_FIELD = "Assets/Scripts/ServerShared/FlowField.cs"
ZONE = "Assets/Scripts/ServerShared/Zone.cs"
SETTINGS_ASSET = "Assets/Resources/Settings.asset"

# (label, file, before, after, the failing check the probe must name)
MUTANTS = [
    ("hlsl-y-halving", CGINC, "flowSample.y *= .5;", "flowSample.y *= .6;", "global"),
    ("hlsl-scroll-sign", CGINC, "pos / _FlowScale - float3(0,_FlowScroll,0)", "pos / _FlowScale + float3(0,_FlowScroll,0)", "global"),
    ("hlsl-swirl-flipped", CGINC, "float3(ngrad.y, 0, -ngrad.x)", "float3(-ngrad.y, 0, ngrad.x)", "slope"),
    ("hlsl-zero-gradient-guard", CGINC, "    if (dot(grad, grad) == 0) return 0;\n", "", "slope"),
    ("lower-scroll-doubled", VOLUME_SAMPLING, 'Shader.SetGlobalFloat("_FlowScroll", scroll);', 'Shader.SetGlobalFloat("_FlowScroll", scroll * 2f);', "lowered-scroll"),
    ("lower-scale-halved", VOLUME_SAMPLING, 'Shader.SetGlobalFloat("_FlowScale", flow.GlobalScale);', 'Shader.SetGlobalFloat("_FlowScale", flow.GlobalScale * .5f);', "lowered-settings"),
    ("lower-frame-clock", VOLUME_SAMPLING, 'Shader.SetGlobalFloat("_FlowScroll", scroll);', 'Shader.SetGlobalFloat("_FlowScroll", scroll + Time.realtimeSinceStartup);', "lowered-scroll"),
    ("csharp-scale-doubled", FLOW_FIELD, "position / settings.GlobalScale", "position / (settings.GlobalScale * 2f)", "global"),
    ("csharp-scroll-offset", FLOW_FIELD, "(float) (settings.GlobalScrollSpeed * zoneTime)", "(float) (settings.GlobalScrollSpeed * (zoneTime + 1.0))", "zone-scroll"),
    ("zone-scroll-scaled", ZONE, "public float FlowScroll => FlowField.Scroll(Settings.Flow, _time);", "public float FlowScroll => FlowField.Scroll(Settings.Flow, _time * 2);", "zone-scroll"),
    ("asset-amplitude", SETTINGS_ASSET, "      GlobalAmplitude: 15\n", "      GlobalAmplitude: 14\n", "authored-flow"),
]


def run_probe(log: Path) -> tuple[int, str]:
    log.unlink(missing_ok=True)
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-projectPath", str(REPO_ROOT),
         "-executeMethod", "Aetheria.EditorTools.FlowFieldParityVerify.Run", "-logFile", str(log)],
        timeout=1800,
    )
    return proc.returncode, log.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    only = set()
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
    scratch = REPO_ROOT / "tests" / "_scratch_flow_parity"
    scratch.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []

    print("\n=== control: unmutated tree ===")
    code, text = run_probe(scratch / "control.log")
    if code != 0 or "[FlowFieldParityVerify] PASS" not in text:
        print("control FAILED: expected exit 0 and PASS on the unmutated tree")
        return 1
    print("control OK: exit 0, PASS")

    for label, rel, before, after, check in MUTANTS:
        if only and label not in only:
            continue
        path = REPO_ROOT / rel
        original = path.read_bytes()
        text_in = original.decode("utf-8")
        newline = "\r\n" if "\r\n" in text_in else "\n"
        b, a = before.replace("\n", newline), after.replace("\n", newline)
        if text_in.count(b) != 1:
            failures.append(f"{label}: anchor occurs {text_in.count(b)} times in {rel}, expected exactly once")
            continue
        print(f"\n=== mutant {label} ({rel}) ===")
        try:
            path.write_bytes(text_in.replace(b, a).encode("utf-8"))
            code, text = run_probe(scratch / f"{label}.log")
        finally:
            path.write_bytes(original)
        named = f"FAIL {check}" in text or f"-{check}:" in text
        if code != 0 and named:
            print(f"OK: caught, the probe named {check}")
        elif code != 0:
            failures.append(f"{label}: red, but not for {check} (compile error or another check)")
        else:
            failures.append(f"{label}: SURVIVED")

    for f in failures:
        print("FAIL:", f)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
