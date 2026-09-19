#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 1 ("Mount direction and arc become simulation data").
#
# Five mutations, each against tests/Aetheria.Shared.Tests/FireControlTests.cs, the cut's own declared table:
#
#   1. ArcFollowsMountRotation -- MountDirection reads Entity.Direction directly instead of rotating it by the
#      item's own authored rotation, so a side-mounted weapon aims like the hull instead of like its mount.
#   2. ArcBoundaryIsHalfWidth -- the bearing test compares against cos(radians(arc)) instead of
#      cos(radians(arc/2)), so an authored FiringArc reads as a half-width arc.
#   3. ArcIsPlanar -- InArc dots the raw 3D vector instead of zeroing height first (R7).
#   4. HardpointOverrideBeatsDefault -- two variants, both against ArcFor: ignoring HardpointData.FiringArc
#      entirely (both hardpoints then fall back to the default), and treating an authored 0 as a literal
#      zero-width arc instead of "use the default."
#   5. CombatStateStepsHeadless -- the cut's own regression: Behaviors.cs:30-35's HardpointTransforms readback
#      branch is gone from this tree (confirmed by negative grep, see below), so there is no literal text left
#      to revert it to, and post-Cut-3 the AI's own gate (Combat.cs) reaches aim direction through
#      FireControl.MountDirection, not the Behavior.Direction property Cut 1 itself touched -- a mutation there
#      is provably off this test's actual path (verified: it survived). The equivalent-shape mutation instead
#      throws from MountDirection itself, the same KeyNotFoundException shape a live Unity-transform readback
#      threw headless before this cut, so the test's own Assert.Null(ex) still catches the regression the cut
#      exists to prevent.
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted. Modelled on
# tests/mutation_tests_fire_control_cut5.sh, the good example: byte-exact I/O, sha256-verified restore after
# every single mutation, and an explicit tree-clean verdict line at exit.
#
# Usage:
#   tests/mutation_tests_fire_control_cut1.sh <path-to-CultLib-worktree>
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

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. ---
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$FIRE_CONTROL_CS")
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
# Anchor must occur exactly once (byte-exact match via perl, not sed's line-oriented rewriting, so a
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

ARTIFACTS_PATH="$(mktemp -d)"

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
  restore_and_verify "$file"

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

# --- No-op control ---
check_mutation \
  "control (no-op round trip)" \
  "$FIRE_CONTROL_CS" \
  'public static float3 MountDirection(EquippedItem item)' \
  'public static float3 MountDirection(EquippedItem item)' \
  "FireControlTests.ArcFollowsMountRotation" \
  "green"

# --- ArcFollowsMountRotation: mount direction must come from the item's own authored rotation, not the
# hull's raw facing. ---
check_mutation \
  "ArcFollowsMountRotation must read the item's rotation, not the hull's raw facing" \
  "$FIRE_CONTROL_CS" \
  'var itemDirection = item.Entity.Direction.Rotate(item.EquippableItem.Rotation);' \
  'var itemDirection = item.Entity.Direction;' \
  "FireControlTests.ArcFollowsMountRotation" \
  "red"

# --- ArcBoundaryIsHalfWidth: FiringArc is full width -- the bearing test must halve it before taking cos. ---
check_mutation \
  "ArcBoundaryIsHalfWidth: FiringArc must be full width (halved before cos), not read as a half-width arc" \
  "$FIRE_CONTROL_CS" \
  'return dot(MountDirection(weapon), planarTarget) >= cos(radians(arc / 2f));' \
  'return dot(MountDirection(weapon), planarTarget) >= cos(radians(arc));' \
  "FireControlTests.ArcBoundaryIsHalfWidth" \
  "red"

# --- ArcIsPlanar (R7): target height must never enter the bearing test. ---
check_mutation \
  "ArcIsPlanar (R7): InArc must zero height, not dot the raw 3D vector" \
  "$FIRE_CONTROL_CS" \
  'var planarToTarget = float3(toTarget.x, 0, toTarget.z);' \
  'var planarToTarget = toTarget;' \
  "FireControlTests.ArcIsPlanar" \
  "red"

# --- HardpointOverrideBeatsDefault, variant 1: ignore HardpointData.FiringArc entirely (both hardpoints in
# the test then fall back to the default, so the 360-authored one fails at 170 degrees too). ---
check_mutation \
  "HardpointOverrideBeatsDefault (variant 1): ArcFor must read HardpointData.FiringArc, not ignore it" \
  "$FIRE_CONTROL_CS" \
  $'        var arc = hardpoint?.FiringArc ?? 0f;\r\n        return arc > 0 ? arc : item.ItemManager.GameplaySettings.FiringArc;' \
  '        return item.ItemManager.GameplaySettings.FiringArc;' \
  "FireControlTests.HardpointOverrideBeatsDefault" \
  "red"

# --- HardpointOverrideBeatsDefault, variant 2: an authored 0 must mean "use the default," not a literal
# zero-width arc -- flipping > to >= makes a 0-authored hardpoint fail even 5 degrees off. ---
check_mutation \
  "HardpointOverrideBeatsDefault (variant 2): an authored 0 must fall back to the default, not read as zero width" \
  "$FIRE_CONTROL_CS" \
  'return arc > 0 ? arc : item.ItemManager.GameplaySettings.FiringArc;' \
  'return arc >= 0 ? arc : item.ItemManager.GameplaySettings.FiringArc;' \
  "FireControlTests.HardpointOverrideBeatsDefault" \
  "red"

# --- CombatStateStepsHeadless: the cut's own regression -- aim direction must never depend on a live Unity
# readback. HardpointTransforms and its readback branch are already fully deleted from this tree (see the
# negative grep below), so there is no literal "restore the read" text left to revert to, and post-Cut-3 the
# AI's own gate reaches aim direction through FireControl.MountDirection (Combat.cs calls
# FireControl.HitProbability, which calls InArc, which calls MountDirection), not the Behavior.Direction
# property this cut itself touched. The equivalent-shape mutation throws from MountDirection itself -- the
# exact KeyNotFoundException shape the old HardpointTransforms dictionary lookup threw headless, before this
# cut -- so the test's own Assert.Null(Record.Exception(...)) still catches the regression this cut exists to
# prevent. ---
check_mutation \
  "CombatStateStepsHeadless: aim direction must never throw when Unity never ran (HardpointTransforms shape)" \
  "$FIRE_CONTROL_CS" \
  'return normalize(float3(itemDirection.x, 0, itemDirection.y));' \
  'throw new System.Collections.Generic.KeyNotFoundException("HardpointTransforms readback restored");' \
  "FireControlTests.CombatStateStepsHeadless" \
  "red"

echo ""
verify_tree_clean

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
