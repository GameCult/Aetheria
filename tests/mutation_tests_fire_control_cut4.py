#!/usr/bin/env python3
"""Mutation tests for docs/fire-control-cut.md's Cut 4 ("The unused kinds join the same path") -- the last five
weapon kinds (ConstantLaser, ConstantLightning, ConstantParticleWeapon, HitscanEffect, Mine) stop deciding
their own hits through live Physics queries and join FireControl, the same owner Cut 3 gave the other four.

Four rules are pinned here, each against tests/Aetheria.Shared.Tests/FireControlCut4Tests.cs:

  1. BeamRollsPerInterval: a continuous weapon (ConstantWeapon) is a sequence of discrete rolls, one per
     GameplaySettings.BeamResolveInterval, not a roll per tick and not one roll for the whole burst.
  2. InstantWeaponFiresWithoutAnyOnFireSubscriber / (implicitly) BeamRollsPerInterval again: FireControl.Fire
     must run whether or not a presentation is listening -- a real Cut 3/4 defect this campaign found and
     fixed. Inlining the call back into `OnFire?.Invoke(FireControl.Fire(...))` silently stops every shot from
     firing in a headless run (no Unity EntityInstance ever subscribes OnFire/OnBeamShot), because C#'s
     null-conditional short-circuits the whole expression, argument included, when the event has no
     subscriber. Pinned on both InstantWeapon.cs and ConstantWeapon.cs since Cut 3 introduced the pattern once
     and Cut 4 copied it a second time.
  3. SplashHitsEveryEntityInRadius: FireControl.Splash (mine/airburst blast) is one rule applied to every
     entity within radius, not just the nearest.
  4. SplashIsDirectional: splash damage lands on the half of each target's own hull that faces the blast, not
     the whole hull.

A no-op control mutation proves the read/replace/restore path is transparent.

Usage:
    python tests/mutation_tests_fire_control_cut4.py --cultlib-root <path-to-CultLib-45c2f40-worktree>

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
CONSTANT_WEAPON_CS = "Assets/Scripts/ServerShared/Behaviors/ConstantWeapon.cs"
INSTANT_WEAPON_CS = "Assets/Scripts/ServerShared/Behaviors/InstantWeapon.cs"
FIRE_CONTROL_CS = "Assets/Scripts/ServerShared/FireControl.cs"


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
        file=CONSTANT_WEAPON_CS,
        anchor="            while (_beamTimer >= interval)",
        mutated="            while (_beamTimer >= interval)",
        test="FireControlCut4Tests.BeamRollsPerInterval",
        expect="green",
    ),

    # --- A beam is a sequence of rolls, not a continuous truth: roll per tick (many more, much smaller
    # --- outcomes over the same second) must be killed. ---
    Mutation(
        rule="ConstantWeapon must roll once per BeamResolveInterval, not once per tick",
        file=CONSTANT_WEAPON_CS,
        anchor="            while (_beamTimer >= interval)",
        mutated="            while (true)",
        test="FireControlCut4Tests.BeamRollsPerInterval",
        expect="red",
    ),

    # --- The opposite direction: rolling once for the whole burst (never draining the timer back below
    # --- interval) must also be killed -- a single outcome instead of a sequence. ---
    Mutation(
        rule="ConstantWeapon must not roll once for the whole burst",
        file=CONSTANT_WEAPON_CS,
        anchor="                _beamTimer -= interval;",
        mutated="                _beamTimer = float.NegativeInfinity;",
        test="FireControlCut4Tests.BeamRollsPerInterval",
        expect="red",
    ),

    # --- The real defect this campaign found: FireControl.Fire must run whether or not OnBeamShot has a
    # --- subscriber. Inlining it back into the null-conditional silently stops every beam from firing in a
    # --- headless run (nothing ever subscribes OnBeamShot). ---
    Mutation(
        rule="ConstantWeapon.Execute must evaluate FireControl.Fire even with no OnBeamShot subscriber",
        file=CONSTANT_WEAPON_CS,
        anchor="                var shotId = FireControl.Fire(this, Item, Entity, Damage * interval);\n                OnBeamShot?.Invoke(shotId);",
        mutated="                OnBeamShot?.Invoke(FireControl.Fire(this, Item, Entity, Damage * interval));",
        test="FireControlCut4Tests.BeamRollsPerInterval",
        expect="red",
    ),

    # --- The same defect, in InstantWeapon.cs (Cut 3's original copy of the pattern) -- caught only by this
    # --- cut's own regression test, since FireAuthorityTests.cs always calls FireControl.Fire directly and
    # --- never exercises Trigger()/Execute() with zero OnFire subscribers. ---
    Mutation(
        rule="InstantWeapon.Execute must evaluate FireControl.Fire even with no OnFire subscriber",
        file=INSTANT_WEAPON_CS,
        anchor="            var shotId = FireControl.Fire(this, Item, Entity);\n            OnFire?.Invoke(shotId);",
        mutated="            OnFire?.Invoke(FireControl.Fire(this, Item, Entity));",
        test="FireControlCut4Tests.InstantWeaponFiresWithoutAnyOnFireSubscriber",
        expect="red",
    ),

    # --- Splash is one rule, not per-effect: damaging only the nearest entity in range instead of every one
    # --- must be killed. ---
    Mutation(
        rule="FireControl.Splash must damage every entity in radius, not only the nearest",
        file=FIRE_CONTROL_CS,
        anchor="            var toTarget = (target.Position - position).xz;\n            if (length(toTarget) > radius) continue;",
        mutated="            var toTarget = (target.Position - position).xz;\n            if (length(toTarget) > radius) continue;\n            if (zone.Entities.Where(e => length((e.Position - position).xz) <= radius).OrderBy(e => length((e.Position - position).xz)).First() != target) continue;",
        test="FireControlCut4Tests.SplashHitsEveryEntityInRadius",
        expect="red",
    ),

    # --- Splash is directional over each target's own hull: damaging the whole shape regardless of facing
    # --- must be killed. ---
    Mutation(
        rule="FireControl.Splash must damage only the half of the hull facing the blast, not the whole shape",
        file=FIRE_CONTROL_CS,
        anchor="                if (dot(normalize((float2) v - hullData.Shape.CenterOfMass), localDirection) < 0)\n                    hitShape[v] = true;",
        mutated="                hitShape[v] = true;",
        test="FireControlCut4Tests.SplashIsDirectional",
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
