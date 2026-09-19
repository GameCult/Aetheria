#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 6b ("the dice stop being shared, and the flak cannon works
# again").
#
# Four mutations, each against tests/Aetheria.Shared.Tests/FireControlCut6Tests.cs, plus a no-op control and a
# negative-grep step:
#
#   1. 6.1 SameFightRollsSameThroughUnrelatedDraws -- restore Commit's generator to the shared
#      ItemManager.Random stream (read AND write-back; CultMath.Random is a struct, so the old shared-stream
#      behaviour needs both halves, not just the read).
#   2. 6.1 ShotIdDecidesTheDie -- drop ShotId from the local generator's seed.
#   3. 6.2 AirburstAndDiscreteNeverDoubleUp -- call Apply as well as Splash at arrival.
#   4. 6.2 NonAirburstNeverSplashes -- splash unconditionally instead of gating on BurstRadius.
#
# A no-op control (a byte-identical rewrite of the target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted. Modelled on
# tests/mutation_tests_fire_control_cut5.sh, the good example: byte-exact I/O, sha256-verified restore after
# every single mutation, and an explicit tree-clean verdict line at exit.
#
# Usage:
#   tests/mutation_tests_fire_control_cut6.sh <path-to-CultLib-worktree>
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

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. Every target file's
# pre-mutation sha256 is recorded once here, before any mutation touches it; every restore below -- per-
# mutation and at exit -- re-hashes and refuses to let a mismatch pass silently (docs/fire-control-cut.md Cut
# 5b.3's own lesson: a harness that can leak an un-restored mutation into the tree produces fictional
# verdicts). ---
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

# check_mutation NAME FILE TEST EXPECT("red"|"green") -- applies whatever mutation the caller already staged
# via replace_unique calls made just before invoking this, runs TEST, restores FILE, and judges. Used by the
# single-anchor mutations below directly; the one multi-edit mutation (restoring the shared-stream generator)
# calls replace_unique twice itself before delegating here.
judge_mutation() {
  local name="$1" file="$2" test="$3" expect="$4"
  if run_test "$test"; then
    passed=1
  else
    passed=0
  fi
  restore_and_verify "$file" # restore immediately, before judging, so a crash still leaves a clean tree

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

# check_mutation NAME FILE ANCHOR MUTATED TEST EXPECT("red"|"green") -- single-anchor convenience wrapper.
check_mutation() {
  local name="$1" file="$2" anchor="$3" mutated="$4" test="$5" expect="$6"
  echo ""
  echo "=== $name (expect $expect) ==="
  if ! replace_unique "$file" "$anchor" "$mutated"; then
    FAILURES+=("$name: anchor replacement failed")
    restore_and_verify "$file"
    return
  fi
  judge_mutation "$name" "$file" "$test" "$expect"
}

# --- No-op control: a byte-identical round trip through this script's own replace_unique path, proving the
# anchor/replace/restore mechanism is transparent before any real mutation is trusted. ---
check_mutation \
  "control (no-op round trip)" \
  "$FIRE_CONTROL_CS" \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  "FireControlCut6Tests.SameFightRollsSameThroughUnrelatedDraws" \
  "green"

# --- 6.1: a shot's dice belong to the shot. Mutation: restore Commit's generator to the shared
# ItemManager.Random stream -- both the read and the write-back, since CultMath.Random is a struct and the old
# shared-stream behaviour depended on writing the advanced copy back for the next Commit to see. Unrelated
# draws the test makes between shots then perturb the very stream Commit reads from. ---
echo ""
echo "=== 6.1 Commit must not read or write the shared ItemManager.Random stream (expect red) ==="
if replace_unique "$FIRE_CONTROL_CS" \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  'var random = shot.Source.ItemManager.Random;' \
&& replace_unique "$FIRE_CONTROL_CS" \
  'return MakeOutcome(shot, hit, shielded, shieldBroken, cell, aimed, now);' \
  'shot.Source.ItemManager.Random = random;
        return MakeOutcome(shot, hit, shielded, shieldBroken, cell, aimed, now);'; then
  judge_mutation "6.1 shared-stream generator" "$FIRE_CONTROL_CS" \
    "FireControlCut6Tests.SameFightRollsSameThroughUnrelatedDraws" "red"
else
  FAILURES+=("6.1 shared-stream generator: anchor replacement failed")
  restore_and_verify "$FIRE_CONTROL_CS"
fi

# --- 6.1: the roll is a function of the shot's own id. Mutation: drop ShotId from the seed -- every shot in a
# zone then builds the exact same local generator every time. ---
check_mutation \
  "6.1 ShotId must reach the seed" \
  "$FIRE_CONTROL_CS" \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  'var random = new Random((zone.CombatSeed * 2654435761u) | 1u);' \
  "FireControlCut6Tests.ShotIdDecidesTheDie" \
  "red"

# --- 6.2: airburst resolves via Splash instead of Apply, never both. Mutation: call Apply as well as Splash at
# arrival -- the double-application Soul was told to hunt for. ---
check_mutation \
  "6.2 an airburst shot must not also Apply a discrete hit" \
  "$FIRE_CONTROL_CS" \
  $'                if (shot.BurstRadius > 0f) Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType);\r\n                else Apply(shot);' \
  $'                if (shot.BurstRadius > 0f) Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType);\r\n                Apply(shot);' \
  "FireControlCut6Tests.AirburstAndDiscreteNeverDoubleUp" \
  "red"

# --- 6.2: the other side. Mutation: splash unconditionally instead of gating on BurstRadius -- Splash does not
# check Hit, so a shot that never carried the Airburst flag would still deal area damage. ---
check_mutation \
  "6.2 a non-airburst shot must never splash" \
  "$FIRE_CONTROL_CS" \
  $'                if (shot.BurstRadius > 0f) Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType);\r\n                else Apply(shot);' \
  '                Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType);' \
  "FireControlCut6Tests.NonAirburstNeverSplashes" \
  "red"

echo ""
echo "=== negative greps ==="
if grep -q "ItemManager.Random" "$FIRE_CONTROL_CS"; then
  FAILURES+=("negative grep: ItemManager.Random still appears in FireControl.cs")
  echo "FAIL: ItemManager.Random appears in FireControl.cs"
else
  echo "OK: ItemManager.Random does not appear in FireControl.cs"
fi

if grep -rq "Airburst" "$REPO_ROOT/Assets/Scripts/Gameplay"; then
  FAILURES+=("negative grep: Airburst still appears under Assets/Scripts/Gameplay")
  echo "FAIL: Airburst appears under Assets/Scripts/Gameplay"
  grep -rn "Airburst" "$REPO_ROOT/Assets/Scripts/Gameplay"
else
  echo "OK: Airburst does not appear anywhere under Assets/Scripts/Gameplay"
fi

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
