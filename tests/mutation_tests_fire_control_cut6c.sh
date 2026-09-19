#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 6c ("the formula gets fixed, and the single point of
# failure").
#
# Two mutations, each against tests/Aetheria.Shared.Tests/FireControlCut6cTests.cs:
#
#   1. 6c.1 HigherResolutionYieldsHigherOrEqualHitProbability -- FireControl.cs's HitProbability reads
#      Resolution as the sensor ceiling directly again (the shipped Cut 6a behaviour), instead of taking its
#      reciprocal. That inverts the direction the operator ruling fixed: a higher-Resolution design would
#      score lower than a cheaper one at low info, the exact defect 6c.1 exists to kill.
#   2. 6c.2 NoSellerDegradesToUnaidedInsteadOfThrowing -- LoadoutGenerator.FillInterior's targeting-system
#      RandomProduct call goes back to `required: true` and the null case throws InvalidLoadoutException
#      again, instead of degrading to an unaided entity and logging the gap.
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted.
#
# Usage:
#   tests/mutation_tests_fire_control_cut6c.sh <path-to-CultLib-45c2f40-worktree>
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
LOADOUT_GENERATOR_CS="$REPO_ROOT/Assets/Scripts/ServerShared/LoadoutGenerator.cs"

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. Same convention
# Cut 5's harness established (5b.3): a restore that is not verified is a restore that can lie. ---
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$FIRE_CONTROL_CS" "$LOADOUT_GENERATOR_CS")
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
# whole script (not just this mutation) if the restore did not reproduce the pre-mutation hash exactly.
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
# PASS/FAIL verdict. Called once explicitly at the bottom of a normal run, and again from the EXIT/INT/TERM
# trap as the safety net for every abnormal exit. (A SIGKILL of the whole process group is the one
# termination this cannot see at all -- that gap is closed by re-checking independently after the fact.)
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

# A dedicated, isolated artifacts path -- see Cut 5's harness for why (stale prebuilt CultLib assemblies from
# an unrelated earlier session's default-path build produced one observed spurious control failure).
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
  'var pSensor = saturate(unlerp(settings.TargetDetectionInfoThreshold, demandCeiling, info));' \
  'var pSensor = saturate(unlerp(settings.TargetDetectionInfoThreshold, demandCeiling, info));' \
  "FireControlCut6cTests.HigherResolutionYieldsHigherOrEqualHitProbability" \
  "green"

# --- 6c.1: Resolution is a benefit, so HitProbability must take its reciprocal to derive the sensor ceiling,
# not read Resolution as that ceiling directly. Mutation: restore the shipped Cut 6a reading. ---
check_mutation \
  "6c.1 HitProbability must take Resolution's reciprocal, not read it as the ceiling directly" \
  "$FIRE_CONTROL_CS" \
  $'        var demandCeiling = settings.TargetDetectionInfoThreshold +\r\n            (1f - settings.TargetDetectionInfoThreshold) / max(Resolution(source), 1e-3f);\r\n        var pSensor = saturate(unlerp(settings.TargetDetectionInfoThreshold, demandCeiling, info));' \
  '        var pSensor = saturate(unlerp(settings.TargetDetectionInfoThreshold, Resolution(source), info));' \
  "FireControlCut6cTests.HigherResolutionYieldsHigherOrEqualHitProbability" \
  "red"

# --- 6c.2: a galaxy with no available targeting-system seller must degrade the loadout, not throw. Mutation:
# restore `required: true` on the RandomProduct call and the InvalidLoadoutException throw. ---
check_mutation \
  "6c.2 FillInterior must degrade to unaided fire on no targeting seller, not throw" \
  "$LOADOUT_GENERATOR_CS" \
  $'            var (targetingProduct, targetingData) = RandomProduct<GearData>(2, item =>\r\n                item.Behaviors.Any(b => b is TargetingSystemData) &&\r\n                item.Shape.FitsWithin(emptyShape, out _, out _));\r\n            if (targetingData == null)\r\n            {\r\n                ItemManager.Log("No targeting system available for armed entity; it will fire unaided.");\r\n            }\r\n            else\r\n            {' \
  $'            var (targetingProduct, targetingData) = RandomProduct<GearData>(2, item =>\r\n                item.Behaviors.Any(b => b is TargetingSystemData) &&\r\n                item.Shape.FitsWithin(emptyShape, out _, out _), required: true);\r\n            if (targetingData == null)\r\n            {\r\n                throw new InvalidLoadoutException("No compatible targeting system found for entity!");\r\n            }\r\n            else\r\n            {' \
  "FireControlCut6cTests.NoSellerDegradesToUnaidedInsteadOfThrowing" \
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
