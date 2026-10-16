#!/usr/bin/env python3
"""Mutation tests for the flow-shader-parity rule (campaign aetheria-fauna, cut fauna-flow-field).

Assets/Scripts/Editor/FlowFieldSceneVerify.cs opens ARPG.unity and drives VolumeSampling and Stardust as the game
does (the keywords, the stardust buffer, CloudShader's keywords); each mutant below names the check that must catch it.
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

PARITY = "Aetheria.EditorTools.FlowFieldParityVerify.Run"
SCENE = "Aetheria.EditorTools.FlowFieldSceneVerify.Run"
STARDUST_COMPUTE = "Assets/Shaders/Compute/Stardust/Stardust.compute"
PARITY_COMPUTE = "Assets/Shaders/Compute/FlowParity.compute"
CLOUD_SHADER = "Assets/Shaders/Raymarching/CloudShader.shader"
CGINC = "Assets/Shaders/Volumetric.cginc"
VOLUME_SAMPLING = "Assets/Scripts/Zone Display/VolumeSampling.cs"
FLOW_FIELD = "Assets/Scripts/ServerShared/FlowField.cs"
SETTINGS_ASSET = "Assets/Resources/Settings.asset"

# (label, file, before, after, the failing check the probe must name)
MUTANTS = [
    ("hlsl-y-halving", CGINC, "flowSample.y *= .5;", "flowSample.y *= .6;", "global"),
    ("hlsl-y-factor-501", CGINC, "flowSample.y *= .5;", "flowSample.y *= .501;", "global"),
    ("hlsl-amplitude-1.0005", CGINC, "- float3(0,_FlowScroll.y,0) ) * _FlowAmplitude;", "- float3(0,_FlowScroll.y,0) ) * _FlowAmplitude * 1.0005;", "global"),
    ("hlsl-scroll-sign", CGINC, "field - float3(0,_FlowScroll.x,0)", "field + float3(0,_FlowScroll.x,0)", "global"),
    ("hlsl-scroll2-dropped", CGINC, "field * 1.61803398875 - float3(0,_FlowScroll.y,0)", "field * 1.61803398875", "global"),
    ("hlsl-swirl-flipped", CGINC, "float3(ngrad.y, 0, -ngrad.x)", "float3(-ngrad.y, 0, ngrad.x)", "slope"),
    ("hlsl-swirl-1.0005", CGINC, "float3(ngrad.y, 0, -ngrad.x) * _FlowSwirlAmplitude", "float3(ngrad.y, 0, -ngrad.x) * _FlowSwirlAmplitude * 1.0005", "slope"),
    ("hlsl-zero-gradient-guard", CGINC, "    if (dot(grad, grad) == 0) return 0;\n", "", "slope"),
    ("lower-scroll-doubled", VOLUME_SAMPLING, "new Vector4(scroll.x, scroll.y)", "new Vector4(scroll.x * 2f, scroll.y)", "lowered-scroll"),
    ("lower-scale-halved", VOLUME_SAMPLING, 'Shader.SetGlobalFloat("_FlowScale", flow.GlobalScale);', 'Shader.SetGlobalFloat("_FlowScale", flow.GlobalScale * .5f);', "lowered-settings"),
    ("lower-frame-clock", VOLUME_SAMPLING, "FlowField.Scroll(flow, flowClock)", "FlowField.Scroll(flow, flowClock + Time.realtimeSinceStartup)", "lowered-scroll"),
    ("lower-cycle-frame-clock", VOLUME_SAMPLING, "FlowField.Cycle(flowClock, flow.Period)", "FlowField.Cycle(Time.realtimeSinceStartup, flow.Period)", "lowered-cycle"),
    ("lower-keywords-lookup-only", VOLUME_SAMPLING, 'GlobalKeyword.Create("FLOW_GLOBAL")', 'new GlobalKeyword("FLOW_GLOBAL")', "flow-keywords"),
    ("csharp-scale-doubled", FLOW_FIELD, "var field = position / settings.GlobalScale;", "var field = position / (settings.GlobalScale * 2f);", "global"),
    ("asset-amplitude", SETTINGS_ASSET, "      GlobalAmplitude: 15\n", "      GlobalAmplitude: 14\n", "authored-flow"),
    ("hlsl-noise-frame-clock", CGINC, "_NebulaNoiseTime * _NebulaNoiseSpeed", "_Time.y * _NebulaNoiseSpeed", "nebula-noise-clock"),
    ("parity-slope-pragma-removed", PARITY_COMPUTE, "#pragma multi_compile __ FLOW_SLOPE", "", "flow-keywords"),
    ("scene-stardust-pragma-removed", STARDUST_COMPUTE, "#pragma multi_compile __ FLOW_GLOBAL", "", "scene-stardust", SCENE),
    ("scene-cloud-include-without-pragmas", CLOUD_SHADER, '#include_with_pragmas "Assets/Shaders/Volumetric.cginc"', '#include "Assets/Shaders/Volumetric.cginc"', "scene-cloud-keywords", SCENE),
    ("scene-keyword-in-field-initializer", VOLUME_SAMPLING, "static GlobalKeyword? _keywordFlowGlobal;", 'static GlobalKeyword? _keywordFlowGlobal = GlobalKeyword.Create("FLOW_GLOBAL");', "scene-presenter-exception", SCENE),
    ("scene-noise-time-frame-clock", VOLUME_SAMPLING, '"_NebulaNoiseTime", (float) clock', '"_NebulaNoiseTime", Time.realtimeSinceStartup', "scene-noise-clock", SCENE),
]


def run_probe(log: Path, entry: str = PARITY) -> tuple[int, str]:
    log.unlink(missing_ok=True)
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-projectPath", str(REPO_ROOT),
         "-executeMethod", entry, "-logFile", str(log)],
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

    for entry, name in ((PARITY, "FlowFieldParityVerify"), (SCENE, "FlowFieldSceneVerify")):
        print(f"\n=== control: unmutated tree, {name} ===")
        code, text = run_probe(scratch / f"control-{name}.log", entry)
        if code != 0 or f"[{name}] PASS" not in text:
            print("control FAILED: expected exit 0 and PASS on the unmutated tree")
            return 1
        print("control OK: exit 0, PASS")

    for label, rel, before, after, check, *rest in MUTANTS:
        entry = rest[0] if rest else PARITY
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
            code, text = run_probe(scratch / f"{label}.log", entry)
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
