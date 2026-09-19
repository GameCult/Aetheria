#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 5 ("the rules that landed in one path and not its twin").
#
# Eleven mutations, each against tests/Aetheria.Shared.Tests/FireControlCut5Tests.cs or
# tests/Aetheria.Shared.Tests/FireAuthorityTests.cs:
#
#   1-7.  5.1 through 5.7, each killing the test this cut wrote or extended to pin it:
#           5.1 UnaidedTrackingFallsBackToSettingNotZero / TrackingIsAFloorNotACliff
#           5.2 UnabsorbableHitBreaksShield
#           5.3 ConstantWeaponObeysArc
#           5.4 PointBlankBearingAlwaysBears
#           5.5 DestroyedItemStopsBeingAimPoint
#           5.6 DeathRemovesShipFromSimulation
#           5.7 PendingShotCarriesNoDeadFields
#   8-11. Four of the five mutations that survived 197 green tests in Soul's own pass (the fifth,
#         freezing source.TargetItem.Value instead of ResolvedTargetItem, is Cut 7's):
#           FireControl.cs        -- delete shield.Break() from Splash
#           Weapon.cs              -- ArcAllowsFire hardcoded true
#           InstantWeapon.cs       -- delete the player's own arc gate
#           FireControl.cs (Commit) -- elapsed pinned to the shot's own duration instead of tracking `now`
#             (the map's own named mutation for EvasionCountsUntilCommitAndNotAfter; post-5.7 the spelling
#             is `shot.ArrivalTime - shot.FireTime` rather than the deleted FlightTime field, same rule)
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted.
#
# Usage:
#   tests/mutation_tests_fire_control_cut5.sh <path-to-CultLib-45c2f40-worktree>
#
# Every mutation is reversed (byte-exact, from an on-disk copy) before the script exits, including on failure
# or interrupt, and every restore is verified against a sha256 recorded before any mutation ran: the tree-
# clean verdict at the end states PASS/FAIL explicitly rather than leaving a reader to infer it. A SIGKILL of
# the whole process group is the one termination bash cannot trap at all -- verify the tree independently
# after a run that was killed that hard.

set -u

if [ $# -lt 1 ]; then
  echo "Usage: $0 <cultlib-root>" >&2
  exit 2
fi
CULTLIB_ROOT="$1"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_PROJECT="$REPO_ROOT/tests/Aetheria.Shared.Tests"

FIRE_CONTROL_CS="$REPO_ROOT/Assets/Scripts/ServerShared/FireControl.cs"
WEAPON_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Behaviors/Weapon.cs"
INSTANT_WEAPON_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Behaviors/InstantWeapon.cs"
CONSTANT_WEAPON_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Behaviors/ConstantWeapon.cs"
ZONE_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Zone.cs"

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. ---
#
# Cut 5b (5b.3): a restore that is not verified is a restore that can lie. Three runs of this script have
# left mutated files behind (a trap cannot survive a hard kill of the process group, and nothing before this
# cut ever checked that a `cp -p` restore actually reproduced the original bytes). Every target file's
# pre-mutation sha256 is recorded once here, before any mutation touches it; every restore below --
# per-mutation and at exit -- re-hashes and refuses to let a mismatch pass silently.
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$FIRE_CONTROL_CS" "$WEAPON_CS" "$INSTANT_WEAPON_CS" "$CONSTANT_WEAPON_CS" "$ZONE_CS")
BACKUP_DIR="$(mktemp -d)"

hash_file() {
  sha256sum "$1" 2>/dev/null | awk '{print $1}'
}

for f in "${FILES[@]}"; do
  base="$(basename "$f")"
  cp -p "$f" "$BACKUP_DIR/$base.orig"
  ORIGINALS["$f"]="$BACKUP_DIR/$base.orig"
  ORIGINAL_HASH["$f"]="$(hash_file "$f")"
done

# restore_and_verify FILE -- restores one file from its recorded backup, then re-hashes it and aborts the
# whole script (not just this mutation) if the restore did not reproduce the pre-mutation hash exactly. A
# mismatch here means continuing would judge every later mutation against an already-contaminated baseline,
# which is strictly worse than stopping.
restore_and_verify() {
  local file="$1"
  cp -p "${ORIGINALS[$file]}" "$file"
  local actual
  actual="$(hash_file "$file")"
  if [ "$actual" != "${ORIGINAL_HASH[$file]}" ]; then
    echo "" >&2
    echo "FATAL: restoring $file did not reproduce its pre-mutation hash -- aborting." >&2
    echo "  expected sha256: ${ORIGINAL_HASH[$file]}" >&2
    echo "  actual   sha256: $actual" >&2
    exit 1
  fi
}

TREE_CLEAN_OK=1
TREE_VERDICT_DONE=0

# verify_tree_clean -- re-hashes every target file against its pre-mutation baseline and prints an explicit
# PASS/FAIL verdict. Called once explicitly at the bottom of a normal run (so the verdict is part of that
# run's own summary, not something a reader has to infer from silence) and again from the EXIT/INT/TERM trap
# as the safety net for every abnormal exit -- interrupted, failed, or killed in a way bash can still trap.
# (A SIGKILL of the whole process group is the one termination this cannot see at all; nothing running inside
# the killed process can run afterward, trap or not -- that gap is closed by re-checking independently after
# the fact, not by this function.)
verify_tree_clean() {
  if [ "$TREE_VERDICT_DONE" = "1" ]; then return; fi
  local f actual
  for f in "${FILES[@]}"; do
    cp -p "${ORIGINALS[$f]}" "$f" 2>/dev/null
    actual="$(hash_file "$f")"
    if [ "$actual" != "${ORIGINAL_HASH[$f]}" ]; then
      echo "FATAL: $f is NOT byte-identical to how this script found it." >&2
      echo "  expected sha256: ${ORIGINAL_HASH[$f]}" >&2
      echo "  actual   sha256: $actual" >&2
      TREE_CLEAN_OK=0
    fi
  done
  echo ""
  if [ "$TREE_CLEAN_OK" = "1" ]; then
    echo "=== tree-clean verdict: PASS -- every target file is byte-identical to how this script found it ==="
  else
    echo "=== tree-clean verdict: FAIL -- see FATAL lines above; the working tree is contaminated ==="
  fi
  TREE_VERDICT_DONE=1
}
trap verify_tree_clean EXIT INT TERM

FAILURES=()

# replace_unique FILE ANCHOR REPLACEMENT
# Anchor must occur exactly once (byte-exact match via awk/perl, not sed's line-oriented rewriting, so a
# multi-line anchor is matched as literal text). Writes the mutated content back byte-exact.
replace_unique() {
  local file="$1" anchor="$2" replacement="$3"
  local count
  count=$(perl -0777 -e '
    my ($anchor, $file) = @ARGV;
    local $/; open(my $fh, "<:raw", $file) or die $!;
    my $text = <$fh>; close $fh;
    my $n = () = $text =~ /\Q$anchor\E/g;
    print $n;
  ' "$anchor" "$file")
  if [ "$count" != "1" ]; then
    echo "ERROR: $file: anchor must occur exactly once, found $count" >&2
    echo "--- anchor ---" >&2
    echo "$anchor" >&2
    return 1
  fi
  # No -i: on this filesystem perl's in-place-edit extension parsing has mangled a literal ":raw" suffix
  # into stray sibling files before. Read, substitute once, write back byte-exact instead.
  perl -0777 -e '
    my ($file, $anchor, $replacement) = @ARGV;
    local $/; open(my $fh, "<:raw", $file) or die $!;
    my $text = <$fh>; close $fh;
    my $qa = quotemeta($anchor);
    $text =~ s/$qa/$replacement/;
    open(my $out, ">:raw", $file) or die $!;
    print $out $text; close $out;
  ' "$file" "$anchor" "$replacement"
}

# A dedicated, isolated artifacts path -- the default tests/Aetheria.Shared.Tests/bin,obj can carry stale
# prebuilt CultLib assemblies from an unrelated earlier session's default-path build, and mixing those with a
# freshly compiled Aetheria.Shared.Tests.dll produced one observed spurious failure of a control (no-op)
# mutation. A private --artifacts-path sidesteps that shared, unowned build state entirely.
ARTIFACTS_PATH="$(mktemp -d)"

# run_test TEST_FQN -> 0 (passed) or 1 (failed/errored)
run_test() {
  local test="$1"
  local out
  out=$(dotnet test "$TEST_PROJECT" -p:CultLibRoot="$CULTLIB_ROOT" --artifacts-path "$ARTIFACTS_PATH" --filter "FullyQualifiedName~$test" 2>&1)
  if echo "$out" | grep -q "Passed!" && ! echo "$out" | grep -q "Failed:     [1-9]"; then
    return 0
  fi
  LAST_OUTPUT="$out"
  return 1
}

# check_mutation NAME FILE ANCHOR MUTATED TEST EXPECT("red"|"green")
check_mutation() {
  local name="$1" file="$2" anchor="$3" mutated="$4" test="$5" expect="$6"
  echo ""
  echo "=== $name (expect $expect) ==="
  if ! replace_unique "$file" "$anchor" "$mutated"; then
    FAILURES+=("$name: anchor replacement failed")
    restore_and_verify "$file"
    return
  fi
  if run_test "$test"; then
    passed=1
  else
    passed=0
  fi
  restore_and_verify "$file" # restore this file immediately, before judging, so a crash still leaves a clean tree

  if [ "$expect" = "green" ]; then
    if [ "$passed" = "1" ]; then
      echo "OK: $test passed under a no-op mutation, as expected"
    else
      FAILURES+=("$name: expected green (test passing), got failure")
      echo "$LAST_OUTPUT" | tail -40
    fi
  else
    if [ "$passed" = "0" ]; then
      echo "OK: $test went red under the mutation, as expected"
    else
      FAILURES+=("$name: expected red (mutant killed), but $test still passed")
    fi
  fi
}

# --- No-op control: a byte-identical round trip through this script's own replace_unique path, proving the
# anchor/replace/restore mechanism is transparent before any real mutation is trusted. ---
check_mutation \
  "control (no-op round trip)" \
  "$FIRE_CONTROL_CS" \
  'var pDeviation = saturate(1f - deviation / shot.Tracking);' \
  'var pDeviation = saturate(1f - deviation / shot.Tracking);' \
  "FireControlCut5Tests.TrackingIsAFloorNotACliff" \
  "green"

# --- 5.1: Tracking is a floor, not a cliff. Mutation: the unaided fallback reverts to a literal 0f (division
# by zero saturates the deviation term to 0 regardless of how forgivable the deviation actually is). ---
check_mutation \
  "5.1 Tracking must fall back to UnaidedTracking, not 0" \
  "$FIRE_CONTROL_CS" \
  'return system != null && system.Item.Active.Value ? system.Tracking : entity.ItemManager.GameplaySettings.UnaidedTracking;' \
  'return system != null && system.Item.Active.Value ? system.Tracking : 0f;' \
  "FireControlCut5Tests.UnaidedTrackingFallsBackToSettingNotZero" \
  "red"

# --- 5.2: a shot the shield cannot absorb breaks it. Mutation: delete Apply's Break() call -- the hull still
# takes the remainder, but the shield itself never registers the overwhelming hit. ---
check_mutation \
  "5.2 Apply must Break() a shield the roll decided ShieldBroken" \
  "$FIRE_CONTROL_CS" \
  'if (shot.Outcome.ShieldBroken) shot.Target.Shield.Break();' \
  '// deleted' \
  "FireControlCut5Tests.UnabsorbableHitBreaksShield" \
  "red"

# --- 5.3: continuous weapons obey their arc. Mutation: ConstantWeapon's safety check drops ArcAllowsFire,
# restoring StanceAllowsFire-only gating (a side-mounted beam fires forward). ---
check_mutation \
  "5.3 ConstantWeapon.Execute must safe off on ArcAllowsFire too, not StanceAllowsFire alone" \
  "$CONSTANT_WEAPON_CS" \
  'if (_firing && !(StanceAllowsFire && ArcAllowsFire))' \
  'if (_firing && !StanceAllowsFire)' \
  "FireControlCut5Tests.ConstantWeaponObeysArc" \
  "red"

# --- 5.4: one bearing test, including at zero range. Mutation: drop InArc's point-blank guard, restoring the
# NaN comparison a zero-length bearing used to fall through to. ---
check_mutation \
  "5.4 InArc must treat a zero-length bearing as bearing true, not fall through to NaN" \
  "$FIRE_CONTROL_CS" \
  $'        if (lengthsq(planarToTarget) < 1e-6f) return true;\r\n        var planarTarget = normalize(planarToTarget);' \
  '        var planarTarget = normalize(planarToTarget);' \
  "FireControlCut5Tests.PointBlankBearingAlwaysBears" \
  "red"

# --- 5.5: a destroyed subsystem stops being the aim point. Mutation: drop the Durability filter from
# IsRevealed's ranking -- a destroyed item keeps its old tier and TargetItem keeps aiming at a corpse. ---
check_mutation \
  "5.5 IsRevealed's ranking must exclude a destroyed item, not just fail its own tier" \
  "$FIRE_CONTROL_CS" \
  '.Where(e => e.Data.HardpointType != HardpointType.Hull && e.EquippableItem.Durability >= .01f)' \
  '.Where(e => e.Data.HardpointType != HardpointType.Hull)' \
  "FireControlCut5Tests.DestroyedItemStopsBeingAimPoint" \
  "red"

# --- 5.6: death removes the ship, in the simulation. Mutation: remove Zone's own Death subscription --
# nothing outside Unity's own (untested here) loot-drop path would ever remove the corpse. ---
check_mutation \
  "5.6 Zone must remove a dead entity itself, not rely on a presentation subscription" \
  "$ZONE_CS" \
  'Entities.ObserveAdd().Subscribe(add => add.Value.Death.Subscribe(_ => Entities.Remove(add.Value)));' \
  '// deleted -- nothing removes a dead entity' \
  "FireControlCut5Tests.DeathRemovesShipFromSimulation" \
  "red"

# --- 5.7: delete the two carried-and-unread fields. Mutation: re-add PredictedIntercept to the struct. ---
check_mutation \
  "5.7 PendingShot must not carry PredictedIntercept back" \
  "$FIRE_CONTROL_CS" \
  $'    public float3 FireTargetVelocity;\r\n    public float FireTime;' \
  $'    public float3 FireTargetVelocity;\r\n    public float3 PredictedIntercept;\r\n    public float FireTime;' \
  "FireControlCut5Tests.PendingShotCarriesNoDeadFields" \
  "red"

# --- Named survivor 1: FireControl.cs -- delete shield.Break() from Splash. ---
check_mutation \
  "survivor: Splash must Break() a shield it does not absorb" \
  "$FIRE_CONTROL_CS" \
  'if (shieldActive && !shieldAbsorbs) shield.Break();' \
  '// deleted' \
  "FireControlCut5Tests.SplashBreaksUnabsorbedShield" \
  "red"

# --- Named survivor 2: Weapon.cs -- ArcAllowsFire hardcoded true. ---
check_mutation \
  "survivor: Weapon.ArcAllowsFire must defer to InArc's real answer" \
  "$WEAPON_CS" \
  'return FireControl.InArc(Item, toTarget);' \
  'return true;' \
  "FireControlCut5Tests.WeaponArcAllowsFireReflectsRealBearing" \
  "red"

# --- Named survivor 3: InstantWeapon.cs -- delete the player's own arc gate (Q2, Cut 3's own deferred rule). ---
check_mutation \
  "survivor: InstantWeapon.Trigger must refuse to queue a burst out of arc" \
  "$INSTANT_WEAPON_CS" \
  'if (!ArcAllowsFire) return;' \
  '' \
  "FireControlCut5Tests.PlayerTriggerObeysArcGate" \
  "red"

# --- Named survivor 4: FireControl.cs (Commit) -- elapsed pinned to the shot's own fixed duration instead of
# tracking `now`, so deviation never grows past its value at the instant the shot was queued. This is the
# map's own declared mutation for EvasionCountsUntilCommitAndNotAfter (FireAuthorityTests.cs); post-5.7 the
# spelling is `shot.ArrivalTime - shot.FireTime` (constant) rather than the deleted FlightTime field. ---
check_mutation \
  "survivor: Commit must measure elapsed against now, not a fixed duration" \
  "$FIRE_CONTROL_CS" \
  'var elapsed = now - shot.FireTime;' \
  'var elapsed = shot.ArrivalTime - shot.FireTime;' \
  "FireAuthorityTests.EvasionCountsUntilCommitAndNotAfter" \
  "red"

echo ""
verify_tree_clean # explicit call: the tree-clean verdict is part of THIS run's own summary, not only
                   # something printed later at exit (the trap's own call is a no-op after this one runs --
                   # see TREE_VERDICT_DONE above -- and remains the safety net for an interrupted run).

if [ "${#FAILURES[@]}" -gt 0 ]; then
  echo ""
  echo "=== FAILURES ==="
  for f in "${FAILURES[@]}"; do
    echo "- $f"
  done
fi

if [ "${#FAILURES[@]}" -gt 0 ] || [ "$TREE_CLEAN_OK" != "1" ]; then
  echo ""
  echo "RESULT: FAILED -- mutations: $([ "${#FAILURES[@]}" -gt 0 ] && echo "${#FAILURES[@]} failure(s)" || echo "all behaved as declared"), tree-clean: $([ "$TREE_CLEAN_OK" = "1" ] && echo PASS || echo FAIL)"
  exit 1
fi

echo "RESULT: All mutations behaved as declared. tree-clean: PASS"
exit 0
