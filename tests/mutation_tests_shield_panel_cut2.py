#!/usr/bin/env python3
"""Mutation tests for docs/shield-panel-cut.md Cut 2 (the ShieldEnvelope landing).

Assets/Scripts/Editor/ShieldPanelCut2Verify.cs is the batchmode probe Cut 2 added, for the same
reason Cut 1 has one (the map's Q7): Tests.asmdef cannot see Assembly-CSharp, so ShieldEnvelope
(pure, testable math though it is) has no NUnit path. A probe nobody can break is a probe nobody
can trust, so this mutates the *real* source in specific ways the cut's own text calls out as the
plausible wrong answers, runs ShieldPanelCut2Verify.Run in Unity batchmode, asserts it goes red
for the reason the cut names, then restores the source exactly. A control case first proves the
unmutated tree passes.

The mutation this file exists for is the one the map calls out by name: "the ellipsoid normal is
exactly the kind of arithmetic that has a plausible-looking wrong answer" — SurfaceNormal
collapsing to normalize(p), which is only correct for a sphere and silently wrong (but
plausible-looking) for Longinus's 4:1 ellipsoid. Two more mutations cover the other two
correctness-load-bearing lines this cut introduced: the per-axis mesh-extents factor collapsing
to a flat guess, and the covector (inverse-scale) transform collapsing to the more "obvious" but
wrong TransformDirection.

This is a separate script rather than an addition to tests/mutation_tests.py (the item-provenance
cut's headless dotnet-test runner, with no path to driving a Unity batchmode check) or
tests/mutation_tests_shield_panel_cut1.py (that one owns Cut 1's rules; this one touches only
Cut 2's file, ShieldEnvelope.cs).

Run from the repo root: python tests/mutation_tests_shield_panel_cut2.py
Exit code 0 only if every case behaved as Cut 2's verification section demands.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"

SHIELD_ENVELOPE_CS = REPO_ROOT / "Assets" / "Scripts" / "Gameplay" / "ShieldEnvelope.cs"


def run_probe(log_path: Path) -> tuple[int, str]:
    proc = subprocess.run(
        [UNITY_EXE, "-batchmode", "-nographics", "-quit", "-projectPath", str(REPO_ROOT),
         "-executeMethod", "Aetheria.EditorTools.ShieldPanelCut2Verify.Run", "-logFile", str(log_path)],
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
    scratch = REPO_ROOT / "tests" / "_scratch_shield_panel_cut2"
    scratch.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []

    original_envelope = SHIELD_ENVELOPE_CS.read_text(encoding="utf-8")

    try:
        # --- control: the real, unmutated tree must pass ---
        print("\n=== control: unmutated tree ===")
        code, text = run_probe(scratch / "control.log")
        if code != 0 or "[ShieldPanelCut2Verify] PASS" not in text:
            failures.append("control: expected exit 0 and PASS on the unmutated tree")
        else:
            print("control OK: exit 0, PASS")

        # --- the mutation the map calls out by name: SurfaceNormal collapsing to normalize(p).
        # This is the "plausible-looking wrong answer" — correct for a sphere, silently wrong for
        # a non-uniform ellipsoid — that the map's Cut 2 verification section says must have a
        # test that fails under its own mutation. ---
        print("\n=== mutation: SurfaceNormal degenerates to normalize(p) ===")
        anchor = (
            "        var s = transform.lossyScale;\n"
            "        var gradientOverScale = new Vector3(\n"
            "            s.x != 0 ? gradient.x / s.x : gradient.x,\n"
            "            s.y != 0 ? gradient.y / s.y : gradient.y,\n"
            "            s.z != 0 ? gradient.z / s.z : gradient.z);\n"
            "        var worldGradient = transform.rotation * gradientOverScale;"
        )
        mutated = (
            "        // mutation_tests_shield_panel_cut2.py: reintroduce normalize(p) as the normal —\n"
            "        // correct for a sphere, plausible-looking, wrong for a non-uniform ellipsoid.\n"
            "        var worldGradient = transform.TransformDirection(local);"
        )
        try:
            replace_unique(SHIELD_ENVELOPE_CS, anchor, mutated)
            code, text = run_probe(scratch / "normalize_p.log")
            caught = "SurfaceNormal equals normalize(p)" in text or "disagrees with the finite-difference gradient" in text
            if code == 0 or not caught:
                failures.append("normalize(p) regression: probe did not go red naming the sphere-vs-ellipsoid distinction")
            else:
                print("OK: caught — probe exited non-zero, sphere-vs-ellipsoid distinction check fired")
        finally:
            SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

        # --- the covector-transform bug this cut's own Hands run hit and fixed: dividing by scale
        # is required (a normal is a covector), multiplying (TransformDirection) is the "obvious"
        # but wrong choice for a non-uniform scale. ---
        print("\n=== mutation: SurfaceNormal multiplies by scale instead of dividing (TransformDirection) ===")
        anchor = (
            "        var s = transform.lossyScale;\n"
            "        var gradientOverScale = new Vector3(\n"
            "            s.x != 0 ? gradient.x / s.x : gradient.x,\n"
            "            s.y != 0 ? gradient.y / s.y : gradient.y,\n"
            "            s.z != 0 ? gradient.z / s.z : gradient.z);\n"
            "        var worldGradient = transform.rotation * gradientOverScale;"
        )
        mutated = (
            "        // mutation_tests_shield_panel_cut2.py: the \"obvious\" wrong transform — a normal\n"
            "        // is a covector and must be scaled by the INVERSE of lossyScale, not lossyScale.\n"
            "        var worldGradient = transform.TransformDirection(gradient);"
        )
        try:
            replace_unique(SHIELD_ENVELOPE_CS, anchor, mutated)
            code, text = run_probe(scratch / "transformdirection.log")
            caught = "disagrees with the finite-difference gradient" in text
            if code == 0 or not caught:
                failures.append("TransformDirection regression: probe did not go red naming the finite-difference disagreement")
            else:
                print("OK: caught — probe exited non-zero, finite-difference gradient check fired")
        finally:
            SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

        # --- the mesh-extents factor collapsing to a flat, wrong guess (the map's own draft
        # guessed 0.5; this checks the fix stays measured-per-axis, not re-flattened). ---
        print("\n=== mutation: Radii factor flattened to a guessed 0.5 instead of the measured mesh extents ===")
        anchor = "_localExtents = mesh != null ? mesh.bounds.extents : Vector3.one;"
        mutated = "_localExtents = mesh != null ? new Vector3(0.5f, 0.5f, 0.5f) : Vector3.one;"
        try:
            replace_unique(SHIELD_ENVELOPE_CS, anchor, mutated)
            code, text = run_probe(scratch / "flat_half.log")
            caught = "does not match lossyScale*measuredExtents" in text
            if code == 0 or not caught:
                failures.append("flat-0.5 regression: probe did not go red naming the Radii mismatch")
            else:
                print("OK: caught — probe exited non-zero, Radii mismatch check fired")
        finally:
            SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

        # --- the regression the coordinator flagged directly: ShieldManager's (and FieldDriver's)
        # missing-envelope fallback must reproduce the deleted pre-Cut-2 formula, not silently swap
        # in a fixed direction. This mutates the shared static SurfaceDirection entry point back to
        # the fixed Vector3.forward every hit direction degraded to before the fix. ---
        print("\n=== mutation: SurfaceDirection fallback collapses to a fixed Vector3.forward ===")
        anchor = (
            "    public static Vector3 SurfaceDirection(Transform transform, Vector3 worldPoint)\n"
            "        => LocalUnitDirection(transform, worldPoint);"
        )
        mutated = (
            "    // mutation_tests_shield_panel_cut2.py: reintroduce the exact regression the\n"
            "    // coordinator flagged -- a fallback that silently changes behaviour instead of\n"
            "    // reproducing the deleted formula.\n"
            "    public static Vector3 SurfaceDirection(Transform transform, Vector3 worldPoint)\n"
            "        => Vector3.forward;"
        )
        try:
            replace_unique(SHIELD_ENVELOPE_CS, anchor, mutated)
            code, text = run_probe(scratch / "fixed_forward.log")
            caught = "SurfaceDirection(transform, p) fallback diverged" in text
            if code == 0 or not caught:
                failures.append("fixed-Vector3.forward regression: probe did not go red naming the SurfaceDirection divergence")
            else:
                print("OK: caught — probe exited non-zero, legacy fallback pin fired")
        finally:
            SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

        # --- the other fallback static, mutated to the "wrong transform" shape the coordinator
        # named: dropping the local-frame projection and returning the raw world point instead. ---
        print("\n=== mutation: LegacyLocalSurfacePoint fallback returns the raw world point ===")
        anchor = (
            "    public static Vector3 LegacyLocalSurfacePoint(Transform transform, Vector3 worldPoint)\n"
            "        => Vector3.Scale(LocalUnitDirection(transform, worldPoint), transform.localScale);"
        )
        mutated = (
            "    // mutation_tests_shield_panel_cut2.py: the \"wrong transform\" mutation -- skip the\n"
            "    // local-frame projection entirely and hand back the untransformed world point.\n"
            "    public static Vector3 LegacyLocalSurfacePoint(Transform transform, Vector3 worldPoint)\n"
            "        => worldPoint;"
        )
        try:
            replace_unique(SHIELD_ENVELOPE_CS, anchor, mutated)
            code, text = run_probe(scratch / "wrong_transform.log")
            caught = "LegacyLocalSurfacePoint(transform, p) fallback diverged" in text
            if code == 0 or not caught:
                failures.append("raw-world-point regression: probe did not go red naming the LegacyLocalSurfacePoint divergence")
            else:
                print("OK: caught — probe exited non-zero, legacy fallback pin fired")
        finally:
            SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

    finally:
        # belt-and-suspenders: never leave a mutated file behind even if something above threw
        SHIELD_ENVELOPE_CS.write_text(original_envelope, encoding="utf-8")

    print("\n=== summary ===")
    if failures:
        for f in failures:
            print(f"FAIL: {f}")
        return 1
    print(f"All cases behaved as Cut 2's verification section demands. Logs: {scratch}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
