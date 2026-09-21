#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 8 ("a removed entity is a dead entity, and the anchors the
# diagnostics moved").
#
# Three mutations against tests/Aetheria.Shared.Tests/FireControlCut8Tests.cs, each from the spec's own
# Verification block:
#
#   1. DeadEntityDoesNotUpdate -- drop the `_active` guard from Entity.Update (8.2). The always-runs prefix of
#      Update (TargetRange, temperature, visibility decay, equipment performance) then keeps executing on a
#      torn-down entity instead of returning immediately.
#   2. DeathDeactivates -- remove the Deactivate() call from Zone's own Death subscription (8.1). The dead
#      entity then leaves Entities but stays active, holding live subscriptions and an uncleared
#      EntityInfoGathered/VisibleEntities.
#
# LootStillDropsOnDeath_EquipmentSurvivesDeactivate and LockWeaponOnDeactivatedEntityDoesNotThrow are not
# mutation-targeted here: the first pins a fact about Deactivate's own body (it never touches Equipment) that
# no single-line mutation isolates without duplicating the DeathDeactivates mutation above, and the second is
# a regression pin already satisfied by Entity.Update's pre-existing inner `if (_active)` gate regardless of
# this cut's own guard -- confirmed by hand: dropping the Cut 8.2 guard does not turn it red. Both still run as
# part of the full test file on every invocation of run_test above and are reported in the final tally.
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted.
#
# Usage:
#   tests/mutation_tests_fire_control_cut8.sh <path-to-CultLib-45c2f40-worktree>
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

ENTITY_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Entity.cs"
ZONE_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Zone.cs"

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. ---
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$ENTITY_CS" "$ZONE_CS")
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

# A dedicated, isolated artifacts path -- see cut5.sh's own comment on this: mixing this run's build with a
# stale default tests/Aetheria.Shared.Tests/bin,obj has produced spurious control failures before.
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
  "$ENTITY_CS" \
  $'    public virtual void Update(float delta)\r\n    {\r\n        if (!_active) return;' \
  $'    public virtual void Update(float delta)\r\n    {\r\n        if (!_active) return;' \
  "FireControlCut8Tests.DeadEntityDoesNotUpdate" \
  "green"

# --- 8.2: a deactivated entity does not update. Mutation: drop the `_active` guard -- the always-runs prefix
# of Update (TargetRange included) keeps executing on a torn-down entity instead of returning immediately. ---
check_mutation \
  "8.2 Entity.Update must return immediately when !_active, not run its prefix regardless" \
  "$ENTITY_CS" \
  $'    public virtual void Update(float delta)\r\n    {\r\n        if (!_active) return;\r\n\r\n        var hullData = ItemManager.GetData(Hull) as HullData;' \
  $'    public virtual void Update(float delta)\r\n    {\r\n        var hullData = ItemManager.GetData(Hull) as HullData;' \
  "FireControlCut8Tests.DeadEntityDoesNotUpdate" \
  "red"

# --- 8.1: removal and deactivation are one transition. Mutation: drop the Deactivate() call from Zone's own
# Death subscription, restoring Cut 5.6's half-fix -- the dead entity leaves Entities but stays active. ---
check_mutation \
  "8.1 Zone's Death subscription must pair Entities.Remove with Deactivate, exactly like TryDock" \
  "$ZONE_CS" \
  'Entities.ObserveAdd().Subscribe(add => add.Value.Death.Subscribe(_ => { Entities.Remove(add.Value); add.Value.Deactivate(); }));' \
  'Entities.ObserveAdd().Subscribe(add => add.Value.Death.Subscribe(_ => { Entities.Remove(add.Value); }));' \
  "FireControlCut8Tests.DeathDeactivates" \
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
