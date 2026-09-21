#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 6d ("where a hit lands is a dart throw, not a coin flip").
#
# Four mutations, each from the spec's own Verification block, against
# tests/Aetheria.Shared.Tests/FireControlCut6dTests.cs:
#
#   1. AimingAtTheSternHitsTheStern -- restore the uniform-random branch (ignore the kernel's weights when
#      drawing a landing cell).
#   2. ThinLimbCostsHitChance       -- drop pOnHull from HitProbability's return.
#   3. PlacementAndProbabilityShareOneKernel -- perturb sigma in one of the kernel's two call sites only
#      (HitProbability's, leaving Commit's placement call untouched).
#   4. EveryHitLandsOnMetal -- draw the kernel's weights over the hull's full bounding box
#      (Shape.AllCoordinates) instead of its occupied cells (Shape.Coordinates). Not named in the spec's own
#      Verification block, but declared in this cut's own test file as the mutation that invariant is pinned
#      against, so it is included here rather than left unrun.
#
# A no-op control (a byte-identical rewrite of the target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted.
#
# Usage:
#   tests/mutation_tests_fire_control_cut6d.sh <path-to-CultLib-45c2f40-worktree>
#
# Every mutation is reversed (byte-exact, from an on-disk copy) before the script exits, including on failure
# or interrupt, and every restore is verified against a sha256 recorded before any mutation ran: the tree-
# clean verdict at the end states PASS/FAIL explicitly rather than leaving a reader to infer it. A SIGKILL of
# the whole process group is the one termination bash cannot trap at all -- verify the tree independently
# after a run that was killed that hard.
#
# Run only one mutation harness at a time against this tree -- concurrent harnesses corrupt each other's
# snapshots (docs/fire-control-cut.md's own campaign has hit this).

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
# mutation elsewhere in this campaign. A private --artifacts-path sidesteps that shared, unowned build state
# entirely.
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
  'var sigma = Sigma(precision);' \
  'var sigma = Sigma(precision);' \
  "FireControlCut6dTests.AimingAtTheSternHitsTheStern" \
  "green"

# --- AimingAtTheSternHitsTheStern: restore the uniform-random branch. Mutation: Commit's weighted draw
# ignores the kernel's own weights and picks uniformly over the hull's occupied cells instead -- the shipped
# coin flip's "miss the aimed item" behaviour, which spreads evenly over the whole hull regardless of aim. ---
check_mutation \
  "AimingAtTheSternHitsTheStern: Commit must draw from the kernel's weights, not uniformly" \
  "$FIRE_CONTROL_CS" \
  'cell = WeightedPick(cells, weights, totalWeight, random);' \
  'cell = cells[random.NextInt(cells.Length)];' \
  "FireControlCut6dTests.AimingAtTheSternHitsTheStern" \
  "red"

# --- ThinLimbCostsHitChance: drop pOnHull from the probability. Mutation: HitProbability's return loses its
# fourth factor -- aiming at a thin limb and aiming at the centre of mass then score identically, since
# nothing left in the product carries a spatial term at all. Cut 8's own re-anchor: Cut 7 (docs/fire-control-
# cut.md, Cut 8 header) moved this calculation into Inspect; only the spelling (diagnostic.PBase's product)
# moved, the rule is unchanged. ---
check_mutation \
  "ThinLimbCostsHitChance: HitProbability must carry pOnHull, not drop it" \
  "$FIRE_CONTROL_CS" \
  'diagnostic.PBase = diagnostic.Accuracy * diagnostic.PSensor * diagnostic.PSpread * diagnostic.POnHull;' \
  'diagnostic.PBase = diagnostic.Accuracy * diagnostic.PSensor * diagnostic.PSpread;' \
  "FireControlCut6dTests.ThinLimbCostsHitChance" \
  "red"

# --- PlacementAndProbabilityShareOneKernel: perturb sigma in one of the two call sites only. Mutation:
# HitProbability's own call into the kernel reads a Precision inflated by 1.5x, while Commit's placement call
# (WeightedPick's own kernel read, a few hundred lines below) is untouched -- the two functions answering
# "where will this shot go" now disagree, which is exactly the named risk this cut's own comments call out --
# "One kernel, computed by one function" no longer holds once a caller perturbs its own input. Cut 8's own
# re-anchor: this call now lives in Inspect and reads diagnostic.Precision (set once at the diagnostic's own
# top from Precision(source)) rather than calling Precision(source) a second time; only the spelling moved. ---
check_mutation \
  "PlacementAndProbabilityShareOneKernel: HitProbability and Commit must read the same Precision into the kernel" \
  "$FIRE_CONTROL_CS" \
  $'        diagnostic.POnHull = HullKernel(targetHull,\r\n            ResolveAimPoint(target, targetHull, source.ResolvedTargetItem).AimPoint, diagnostic.Precision).POnHull;' \
  '        diagnostic.POnHull = HullKernel(targetHull, ResolveAimPoint(target, targetHull, source.ResolvedTargetItem).AimPoint, diagnostic.Precision * 1.5f).POnHull;' \
  "FireControlCut6dTests.PlacementAndProbabilityShareOneKernel" \
  "red"

# --- EveryHitLandsOnMetal: draw the kernel's weights over the hull's full bounding box instead of its
# occupied cells. Mutation: HullKernel reads Shape.AllCoordinates (every grid cell in the bounding box,
# occupied or not) instead of Shape.Coordinates (only the cells actually part of the schematic) -- a hull with
# a hole in it can then have a hit resolve onto the hole. Not one of the spec's own four named mutations, but
# the one this cut's own test file declares against this invariant. ---
check_mutation \
  "EveryHitLandsOnMetal: the kernel must draw from the hull's occupied cells, not its full bounding box" \
  "$FIRE_CONTROL_CS" \
  'var coords = hullData.Shape.Coordinates;' \
  'var coords = hullData.Shape.AllCoordinates;' \
  "FireControlCut6dTests.EveryHitLandsOnMetal" \
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
