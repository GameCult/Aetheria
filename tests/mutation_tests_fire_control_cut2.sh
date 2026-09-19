#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 2 ("Targeting system and reveal").
#
# Six mutations, each against tests/Aetheria.Shared.Tests/TargetingSystemTests.cs, the cut's own declared
# table -- with two adaptations forced by later cuts changing the Body underneath this one (see the notes on
# each, and the report):
#
#   1. SelectionNeedsReveal -- IsRevealed returns true unconditionally instead of checking the tier.
#   2. RevealOrder -- drop the ranking (hardpoint-first, size-descending) entirely.
#   3. RevealSpansBothThresholds -- swap the two authored thresholds in the lerp.
#   4. UnaidedFiresWorse -- Accuracy/Precision/Resolution fall back to the (destroyed/offline) system's own
#      stats instead of the unaided settings, i.e. drop the Active.Value gate.
#   5. StarvedTargetingRollsWorse -- the brownout ruling becomes a switch (PowerSupply <= 1e-4 ? 0 : 1)
#      instead of the authored pow(PowerSupply, exponent) gradient. This is Entity.cs's PowerSupplyFactor,
#      shared production code the brownout ruling (docs/stats-and-power-cut.md) applies well beyond Accuracy
#      -- restored immediately after the single filtered test run, same as every other mutation here.
#   6. EveryArmedLoadoutGetsATargetingSystem -- the doc's own declared mutation ("drop required: true") no
#      longer has a `required: true` to drop: Cut 6c.2 (LoadoutGenerator.cs, 2026-09-19) deliberately changed
#      this call from required:true/throw to required:false/degrade-and-log, an operator ruling this harness
#      must not fight. The adapted mutation targets the surviving acquisition attempt instead -- the predicate
#      that finds a targeting-system seller never matches -- which is what "the required-equipment rule
#      regresses to nothing" now looks like on this Body. See the report for the full account.
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted. Modelled on
# tests/mutation_tests_fire_control_cut5.sh, the good example: byte-exact I/O, sha256-verified restore after
# every single mutation, and an explicit tree-clean verdict line at exit.
#
# Usage:
#   tests/mutation_tests_fire_control_cut2.sh <path-to-CultLib-worktree>
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
ENTITY_CS="$REPO_ROOT/Assets/Scripts/ServerShared/Entity.cs"
LOADOUT_GENERATOR_CS="$REPO_ROOT/Assets/Scripts/ServerShared/LoadoutGenerator.cs"

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. ---
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$FIRE_CONTROL_CS" "$ENTITY_CS" "$LOADOUT_GENERATOR_CS")
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
  'return info >= tier;' \
  'return info >= tier;' \
  "TargetingSystemTests.SelectionNeedsReveal" \
  "green"

# --- SelectionNeedsReveal: IsRevealed must check the tier, not return true unconditionally. ---
check_mutation \
  "SelectionNeedsReveal: IsRevealed must check the tier, not return true unconditionally" \
  "$FIRE_CONTROL_CS" \
  'return info >= tier;' \
  'return true;' \
  "TargetingSystemTests.SelectionNeedsReveal" \
  "red"

# --- RevealOrder: hardpoint-mounted items must rank before interior ones, larger before smaller. ---
check_mutation \
  "RevealOrder: IsRevealed's ranking must be hardpoint-first, size-descending, not equipment order alone" \
  "$FIRE_CONTROL_CS" \
  $'        var ranked = target.Equipment\r\n            .Where(e => e.Data.HardpointType != HardpointType.Hull && e.EquippableItem.Durability >= .01f)\r\n            .OrderByDescending(e => e.Data.HardpointType != HardpointType.Tool)\r\n            .ThenByDescending(e => e.Data.Shape.Coordinates.Length)\r\n            .ToList();' \
  $'        var ranked = target.Equipment\r\n            .Where(e => e.Data.HardpointType != HardpointType.Hull && e.EquippableItem.Durability >= .01f)\r\n            .ToList();' \
  "TargetingSystemTests.RevealOrder" \
  "red"

# --- RevealSpansBothThresholds: the first item must reveal at TargetArmorInfoThreshold and the last at
# TargetGearInfoThreshold -- swap them and the ends flip. ---
check_mutation \
  "RevealSpansBothThresholds: the ranking's tiers must not be swapped" \
  "$FIRE_CONTROL_CS" \
  'var tier = lerp(settings.TargetArmorInfoThreshold, settings.TargetGearInfoThreshold,' \
  'var tier = lerp(settings.TargetGearInfoThreshold, settings.TargetArmorInfoThreshold,' \
  "TargetingSystemTests.RevealSpansBothThresholds" \
  "red"

# --- UnaidedFiresWorse (Q4): a destroyed or disabled targeting system must fall back to the unaided figures,
# not keep supplying its own (now-meaningless) stats. Mutation: drop the Active.Value gate from Accuracy. ---
check_mutation \
  "UnaidedFiresWorse: Accuracy must gate on Active.Value, not just existence, before trusting the system" \
  "$FIRE_CONTROL_CS" \
  $'        var system = entity.GetBehavior<TargetingSystem>();\r\n        return system != null && system.Item.Active.Value\r\n            ? system.Accuracy\r\n            : entity.ItemManager.GameplaySettings.UnaidedAccuracy;' \
  $'        var system = entity.GetBehavior<TargetingSystem>();\r\n        return system != null\r\n            ? system.Accuracy\r\n            : entity.ItemManager.GameplaySettings.UnaidedAccuracy;' \
  "TargetingSystemTests.UnaidedFiresWorse" \
  "red"

# --- StarvedTargetingRollsWorse: the brownout ruling on Accuracy (a shared PowerSupply term, F1 of
# docs/stats-and-power-cut.md) must be a gradient (pow(PowerSupply, exponent)), not a switch. This is Entity's
# own PowerSupplyFactor, read by every stat carrying a PowerSupply term, not just targeting -- restored
# immediately after this one filtered test run, same discipline as every mutation in this harness. ---
check_mutation \
  "StarvedTargetingRollsWorse: PowerSupplyFactor must be a gradient, not a >1e-4 on/off switch" \
  "$ENTITY_CS" \
  'public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);' \
  'public float PowerSupplyFactor(float exponent) => PowerSupply <= 1e-4f ? 0f : 1f;' \
  "TargetingSystemTests.StarvedTargetingRollsWorse" \
  "red"

# --- EveryArmedLoadoutGetsATargetingSystem (Q4): every armed hull's loadout must still try to acquire a
# targeting system. The doc's own mutation ("drop required: true") no longer applies -- Cut 6c.2 already
# changed this call to required:false/degrade-and-log, an operator ruling (see the header comment and the
# report). The adapted mutation makes the acquisition predicate never match, which is what "the rule regresses
# to nothing" now looks like: FillInterior degrades exactly as it does for a galaxy with no seller, silently. ---
check_mutation \
  "EveryArmedLoadoutGetsATargetingSystem: FillInterior must still attempt to acquire a targeting system for an armed hull" \
  "$LOADOUT_GENERATOR_CS" \
  $'            var (targetingProduct, targetingData) = RandomProduct<GearData>(2, item =>\r\n                item.Behaviors.Any(b => b is TargetingSystemData) &&\r\n                item.Shape.FitsWithin(emptyShape, out _, out _));' \
  $'            var (targetingProduct, targetingData) = RandomProduct<GearData>(2, item => false);' \
  "TargetingSystemTests.EveryArmedLoadoutGetsATargetingSystem" \
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
