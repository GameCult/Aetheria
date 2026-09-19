#!/usr/bin/env bash
# Mutation tests for docs/fire-control-cut.md's Cut 3 ("Hit authority moves").
#
# Fourteen check_mutation calls covering the cut's own thirteen declared tests (one, ProbabilityFollowsInputs,
# carries three sub-mutations from its own declared list, so fifteen mutations run in total) against
# tests/Aetheria.Shared.Tests/FireAuthorityTests.cs, plus one more against the test Cut 7 rewrote in its place:
#
#   OutOfArcConsumesNoDraw no longer exists. It asserted against ItemManager.Random (`e.Items.Random`), but
#   Cut 6b (6.1) moved every shot's roll onto its own freshly-seeded, immediately-discarded
#   `new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u)` local to Commit -- a deliberate,
#   good change (reproducibility independent of unrelated draws elsewhere) that also meant Commit stopped
#   reading or writing ItemManager.Random on any path, arc or no arc, which made the old test's own declared
#   mutation ("roll first and multiply by zero") pass for a reason that had nothing to do with arcs. Cut 7
#   (fire-control-cut.md, 7.2's own header comment, and FireAuthorityTests.cs's comment on the replacement)
#   rewrote it as CombatNeverDrawsFromTheSharedStream: no combat path draws from the shared stream, in arc or
#   out. That rule IS externally observable (Commit has no reference to ItemManager to draw from at all under
#   the current design, so the only way to make it "touch the shared stream" is to add a read that was not
#   there before) -- this harness's own mutation below does exactly that, through the one handle Commit does
#   hold (`shot.Source.ItemManager`, already read elsewhere in this same function for hull data).
#
# A no-op control (a byte-identical rewrite of one target file through this script's own read/write path)
# proves the mechanism itself is transparent before any real mutation is trusted. Modelled on
# tests/mutation_tests_fire_control_cut5.sh, the good example: byte-exact I/O, sha256-verified restore after
# every single mutation, and an explicit tree-clean verdict line at exit.
#
# Usage:
#   tests/mutation_tests_fire_control_cut3.sh <path-to-CultLib-worktree>
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

# --- byte-exact backup/restore: preserves line endings exactly, no text-mode translation. ---
declare -A ORIGINALS
declare -A ORIGINAL_HASH
FILES=("$FIRE_CONTROL_CS" "$ENTITY_CS")
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
  'var elapsed = now - shot.FireTime;' \
  'var elapsed = now - shot.FireTime;' \
  "FireAuthorityTests.EvasionCountsUntilCommitAndNotAfter" \
  "green"

# --- RollsAreSeeded (Q5): a fight must be reproducible from its seed. Mutation: draw from an unseeded,
# non-reproducible generator instead of the pure function of (zone identity, shot id). A first attempt seeded
# from System.DateTime.UtcNow.Ticks survived: this whole 50-shot fixture runs faster than the clock's own tick
# resolution, so two runs a few milliseconds apart drew the identical "wall clock" value every time and the
# mutation was accidentally still deterministic. Guid.NewGuid() carries its own entropy source independent of
# clock resolution and reliably breaks reproducibility. ---
check_mutation \
  "RollsAreSeeded: Commit's draw must be a pure function of (zone, shot id), not fresh entropy per call" \
  "$FIRE_CONTROL_CS" \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  'var random = new Random((uint) System.Guid.NewGuid().GetHashCode() | 1u);' \
  "FireAuthorityTests.RollsAreSeeded" \
  "red"

# --- ProbabilityFollowsInputs, variant 1/3: drop pSensor -- probability stops depending on detection info. ---
# Cut 6d (docs/fire-control-cut.md) added a fourth factor, pOnHull, to this same return line -- the anchor
# below tracks the current line so this still targets the pSensor drop specifically, not a byte-exact copy of
# the pre-Cut-6d line, which no longer exists.
check_mutation \
  "ProbabilityFollowsInputs (1/3): HitProbability must scale with pSensor, not drop it" \
  "$FIRE_CONTROL_CS" \
  'return Accuracy(source) * pSensor * pSpread * pOnHull;' \
  'return Accuracy(source) * pSpread * pOnHull;' \
  "FireAuthorityTests.ProbabilityFollowsInputs" \
  "red"

# --- ProbabilityFollowsInputs, variant 2/3: drop the range gate -- nonzero probability outside [MinRange,Range]. ---
check_mutation \
  "ProbabilityFollowsInputs (2/3): HitProbability must gate on [MinRange, Range], not skip the check" \
  "$FIRE_CONTROL_CS" \
  'if (range < weapon.MinRange || range > weapon.Range) return 0f;' \
  '// dropped -- no range gate' \
  "FireAuthorityTests.ProbabilityFollowsInputs" \
  "red"

# --- ProbabilityFollowsInputs, variant 3/3: drop the UnaidedAccuracy cap -- an unaided shooter rolls at full
# strength (1f) instead of the authored handicap. ---
check_mutation \
  "ProbabilityFollowsInputs (3/3): an unaided shooter must cap at UnaidedAccuracy, not roll at full strength" \
  "$FIRE_CONTROL_CS" \
  ': entity.ItemManager.GameplaySettings.UnaidedAccuracy;' \
  ': 1f;' \
  "FireAuthorityTests.ProbabilityFollowsInputs" \
  "red"

# --- ShotResolvesOnArrival (R3): no durability change before arrival. Mutation: apply as soon as committed,
# not only once the shot has also arrived. ---
check_mutation \
  "ShotResolvesOnArrival: Step must apply only at ArrivalTime, not as soon as a shot commits" \
  "$FIRE_CONTROL_CS" \
  'if (shot.Committed && now >= shot.ArrivalTime)' \
  'if (shot.Committed)' \
  "FireAuthorityTests.ShotResolvesOnArrival" \
  "red"

# --- DeadEntityStopsTakingShots (0b table): a shot whose target left the zone must resolve as a miss and be
# removed outright, not go on resolving against a target no longer in play. ---
check_mutation \
  "DeadEntityStopsTakingShots: Step must detect a target that left the zone, not resolve against it anyway" \
  "$FIRE_CONTROL_CS" \
  'var targetGone = shot.Target != null && !zone.Entities.Contains(shot.Target);' \
  'var targetGone = false;' \
  "FireAuthorityTests.DeadEntityStopsTakingShots" \
  "red"

# --- OutcomeCommitsBeforeImpact (R4): ShotCommitted must fire exactly once per shot. Mutation: publish it
# twice from the same commit branch. ---
check_mutation \
  "OutcomeCommitsBeforeImpact: ShotCommitted must publish exactly once per shot, not twice" \
  "$FIRE_CONTROL_CS" \
  $'                zone.ShotCommitted.OnNext(shot.Outcome);\r\n                shots[i] = shot;\r\n            }\r\n\r\n            if (shot.Committed && now >= shot.ArrivalTime)' \
  $'                zone.ShotCommitted.OnNext(shot.Outcome);\r\n                zone.ShotCommitted.OnNext(shot.Outcome);\r\n                shots[i] = shot;\r\n            }\r\n\r\n            if (shot.Committed && now >= shot.ArrivalTime)' \
  "FireAuthorityTests.OutcomeCommitsBeforeImpact" \
  "red"

# --- EvasionCountsUntilCommitAndNotAfter (R4's second half): deviation must be measured against the elapsed
# time at commit, not at fire (elapsed pinned to 0, i.e. no drift accounted for at all). Cut 5's own harness
# already pins this test's OTHER declared mutation (elapsed pinned to the shot's fixed total duration, i.e.
# measured as if always at arrival) as a named survivor; this is the complementary variant from this cut's own
# table. ---
check_mutation \
  "EvasionCountsUntilCommitAndNotAfter: deviation must track elapsed time, not measure as if fired an instant ago" \
  "$FIRE_CONTROL_CS" \
  'var elapsed = now - shot.FireTime;' \
  'var elapsed = 0f;' \
  "FireAuthorityTests.EvasionCountsUntilCommitAndNotAfter" \
  "red"

# --- ShortFlightCommitsAtFire (R4's graceful degradation): a sub-horizon flight must commit at fire time, in
# the same tick. Mutation: always require the full CommitHorizon regardless of how short the flight is, so a
# zero-velocity shot's CommitTime lands after its own ArrivalTime and the commit/resolve order breaks. ---
check_mutation \
  "ShortFlightCommitsAtFire: CommitTime must degrade to fire time for a sub-horizon flight, not always wait a full horizon" \
  "$FIRE_CONTROL_CS" \
  'CommitTime = now + max(0f, flightTime - commitHorizon),' \
  'CommitTime = now + commitHorizon,' \
  "FireAuthorityTests.ShortFlightCommitsAtFire" \
  "red"

# --- OutcomeIsSnapshotNotReread (R10/Q6): Apply must use the frozen shot.Damage, not re-read the weapon's
# live (possibly since-changed) Damage at arrival. ---
check_mutation \
  "OutcomeIsSnapshotNotReread: Apply must use the frozen shot.Damage, not re-read the live weapon stat" \
  "$FIRE_CONTROL_CS" \
  'shot.Target.ApplyHit(shot.Source, shot.Outcome.Cell, shot.DamageSpread, shot.Penetration, shot.Damage, hitDirection);' \
  'shot.Target.ApplyHit(shot.Source, shot.Outcome.Cell, shot.DamageSpread, shot.Penetration, ((Weapon) shot.Weapon.Behaviors.First(b => b is Weapon)).Damage, hitDirection);' \
  "FireAuthorityTests.OutcomeIsSnapshotNotReread" \
  "red"

# --- AimedHitLandsOnSelectedItem (R5's payoff): with Precision authored extremely tight (Cut 6d: the test now
# passes precision: 1000, a sigma a tiny fraction of one cell), a hit must land on the aimed item's own cell,
# not the hull's centre of mass. Cut 6d deleted the coin-flip branch this mutation used to sabotage
# (`random.NextFloat() < shot.Precision` picking between the aimed cell and a uniform-random one); the
# equivalent sabotage under the dart-throw kernel is feeding Commit's aim-point resolver a null Aimed, so it
# falls back to the centre-of-mass path unconditionally -- the same "aim is ignored" defect, on the new code. ---
check_mutation \
  "AimedHitLandsOnSelectedItem: the kernel's aim point must respect Aimed, not fall back to centre of mass" \
  "$FIRE_CONTROL_CS" \
  'var (aimPoint, aimedCells) = ResolveAimPoint(shot.Target, hullData, shot.Aimed);' \
  'var (aimPoint, aimedCells) = ResolveAimPoint(shot.Target, hullData, null);' \
  "FireAuthorityTests.AimedHitLandsOnSelectedItem" \
  "red"

# --- HardpointHitDamagesItemThenHull: damage above armor plus item durability must pass its remainder on to
# the hull. Mutation: drop the remainder (it never reaches the hull). ---
check_mutation \
  "HardpointHitDamagesItemThenHull: the remainder after armor and item must reach the hull, not be dropped" \
  "$ENTITY_CS" \
  'hullDamage += d;' \
  '// dropped -- remainder never reaches the hull' \
  "FireAuthorityTests.HardpointHitDamagesItemThenHull" \
  "red"

# --- PenetrationMarchIsPlanar (R7): the march must rotate the hit direction into the target's own facing, not
# march along the raw world-space direction. ---
check_mutation \
  "PenetrationMarchIsPlanar: the penetration march must rotate into the target's own frame, not use raw world direction" \
  "$ENTITY_CS" \
  $'            var forward = normalize(Direction);\r\n            var right = float2(forward.y, -forward.x);\r\n            var penetrationVector = normalize(float2(dot(hitDirection, right), dot(hitDirection, forward)));' \
  '            var penetrationVector = normalize(hitDirection);' \
  "FireAuthorityTests.PenetrationMarchIsPlanar" \
  "red"

# --- ShieldTakesHit: the absorb rule must have one owner (FireControl.Commit). Mutation: remove the shield
# branch entirely -- shielded/shieldBroken never get set, so Apply always lets the hit reach the hull. ---
check_mutation \
  "ShieldTakesHit: Commit must decide the shield branch, not skip it (hit always reaches the hull)" \
  "$FIRE_CONTROL_CS" \
  $'            var shield = shot.Target.Shield;\r\n            var shieldActive = shield != null && shield.Item.Active.Value;\r\n            if (shieldActive && shield.CanTakeHit(shot.DamageType, shot.Damage)) shielded = true;\r\n            else if (shieldActive) shieldBroken = true;' \
  $'            var shield = shot.Target.Shield;' \
  "FireAuthorityTests.ShieldTakesHit" \
  "red"

# --- Cut 7's replacement for OutOfArcConsumesNoDraw: CombatNeverDrawsFromTheSharedStream. Mutation: Commit
# reads shot.Source.ItemManager.Random (the shared stream FireAuthorityTests.cs's own Build exposes as
# e.Items.Random) once per call, restoring exactly the kind of shared-stream touch Cut 6b (6.1) removed --
# unconditionally, so both the theory's in-arc and out-of-arc cases go red. ---
check_mutation \
  "CombatNeverDrawsFromTheSharedStream: Commit must never read ItemManager's shared stream, on any path" \
  "$FIRE_CONTROL_CS" \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);' \
  'var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u); shot.Source.ItemManager.Random.NextFloat();' \
  "FireAuthorityTests.CombatNeverDrawsFromTheSharedStream" \
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
