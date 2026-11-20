//! The world clock and the motion one tick produces.
//!
//! One entry point into the reducer, [`derive_motion`], reached from `reduce`'s
//! `AdvanceTime` arm and from `apply_effect`'s `TimeAdvanced` arm, so a forged
//! tick is re-derived by the same function that produced the honest one. It is
//! pure over `(state, to)`: no wall clock, no entropy, no caller value beyond
//! the span.

use super::patch::{Commitment, CommitmentKey, CommitmentKind, DependencyTarget, MAX_ROUTE_COST};
use super::{PressureMagnitude, PressureSource, SubjectId, WorldState, action, views};
#[cfg(test)]
use super::is_retired;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

/// Minutes since genesis. Monotonic, kernel-owned, world-level. `u64` because a
/// world clock has no bound; `Cost` is `1..=MAX_ROUTE_COST` because it weighs
/// one edge, and reusing it here would put an edge's bound on the world's age.
#[derive(
    Clone, Copy, Debug, Default, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord, Hash,
)]
#[serde(transparent)]
pub struct FictionalMinutes(pub u64);

impl FictionalMinutes {
    pub(crate) fn checked_add(self, span: TickMinutes) -> Option<Self> {
        self.0.checked_add(u64::from(span.0)).map(Self)
    }
}

/// The span one `AdvanceTime` names, `1..=MAX_ROUTE_COST` (one year of
/// minutes), reusing pass 2's constant as a span bound rather than an edge
/// bound: an unbounded span lets one command jump the clock past every due date
/// in the world and makes the size of a commit a caller-chosen number.
///
/// The type owns its bound at every door: `new` and decoding both go through
/// it, so a command, a routine's period or a stored commit that names a span
/// outside the bound cannot be read at all, and no reducer or replay clause
/// re-checks it.
#[derive(Clone, Copy, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord, Hash)]
#[serde(try_from = "u32", into = "u32")]
pub struct TickMinutes(u32);

impl TryFrom<u32> for TickMinutes {
    type Error = &'static str;

    fn try_from(minutes: u32) -> Result<Self, Self::Error> {
        Self::new(minutes).ok_or("a tick span is 1..=525600 minutes")
    }
}

impl From<TickMinutes> for u32 {
    fn from(span: TickMinutes) -> u32 {
        span.0
    }
}

impl TickMinutes {
    pub fn new(minutes: u32) -> Option<Self> {
        (1..=MAX_ROUTE_COST)
            .contains(&minutes)
            .then_some(Self(minutes))
    }

    pub fn minutes(self) -> u32 {
        self.0
    }
}

/// Everything one tick moves, stated as outcomes rather than deltas so the
/// effect says what the world became.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub(crate) struct Motion {
    /// Routines that fulfilled, in `(subject, key)` order.
    pub(crate) fulfilled: Vec<RoutineFulfilled>,
    /// Pressure rows written, in `(target, source)` order, with the resulting
    /// magnitude — not the delta.
    pub(crate) pressed: Vec<PressureWritten>,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub(crate) struct RoutineFulfilled {
    pub(crate) subject: SubjectId,
    pub(crate) key: CommitmentKey,
    pub(crate) next_due: FictionalMinutes,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub(crate) struct PressureWritten {
    pub(crate) target: SubjectId,
    pub(crate) source: PressureSource,
    pub(crate) magnitude: PressureMagnitude,
}

/// The whole magnitude policy, in one place, with no per-world tuning. A
/// blocked routine raises none at all: it retries on the next tick, which is
/// what keeps an unattended subject free of spurious pressure.
mod step {
    pub(super) const OBLIGATION_PAST_DUE: u32 = 1;
    pub(super) const GOAL_PAST_DUE: u32 = 1;
    pub(super) const DEPENDENCY_UNAVAILABLE: u32 = 1;
}

/// Three stages, each reading only committed `state` and `to`. No stage sees
/// another stage's writes, which is what makes this a one-pass total function
/// with no ordering hazard and no partial state to reason about.
///
/// Pressure accrues per command, not per minute: one thousand one-minute ticks
/// and one thousand-minute tick over the same span produce different
/// magnitudes. The kernel stays an exact function of the command sequence, and
/// the tick cadence is one operational constant in `runtime.rs`.
///
/// What is due, and which dependencies fail, is read from `Views` (the one owner
/// of both questions: a live subject's due commitments and a live subject's
/// unavailable dependencies), so a tick reads the work it does and not the
/// world. A view is true only with clean logs, so it is read behind a check
/// that holds in every build (six flag tests), and both callers (`reduce`, and
/// `apply_effect`'s `TimeAdvanced` arm before its first write) call it clean.
pub(super) fn derive_motion(state: &WorldState, to: FictionalMinutes) -> Motion {
    assert!(views::readable(state), "the views are read with an open log");
    motion_of(state, to, &state.views.due_by(to), state.views.failing().copied())
}

/// What `derive_motion` was before the views: every subject holding a promise
/// and every dependency row, with retirement and availability judged on the
/// spot. The view reads must derive the same motion.
#[cfg(test)]
pub(super) fn derive_motion_by_scan(state: &WorldState, to: FictionalMinutes) -> Motion {
    let live = |subject: &SubjectId| !is_retired(state, *subject);
    let promisors = state.commitments.keys().copied().filter(live).collect();
    let failing: Vec<_> = state
        .dependencies
        .iter()
        .filter(|(subject, _)| live(subject))
        .flat_map(|(subject, targets)| targets.iter().map(move |target| (*subject, *target)))
        .filter(|(subject, target)| dependency_unavailable(state, *subject, *target))
        .collect();
    motion_of(state, to, &promisors, failing.into_iter())
}

/// The motion over `promisors`, which must hold every live subject with a
/// commitment due by `to`, and the `failing` dependencies of live subjects.
fn motion_of(
    state: &WorldState,
    to: FictionalMinutes,
    promisors: &std::collections::BTreeSet<SubjectId>,
    failing: impl Iterator<Item = (SubjectId, DependencyTarget)>,
) -> Motion {
    let mut fulfilled = Vec::new();
    let mut pressed: BTreeMap<(SubjectId, PressureSource), u32> = BTreeMap::new();

    for subject in promisors {
        let held = &state.commitments[subject];
        for (key, commitment) in held {
            if commitment.due > to {
                continue;
            }
            match commitment.kind {
                CommitmentKind::Routine => {
                    if let Some(next_due) = routine_rolls(state, *subject, commitment) {
                        fulfilled.push(RoutineFulfilled {
                            subject: *subject,
                            key: *key,
                            next_due,
                        });
                    }
                }
                CommitmentKind::Obligation | CommitmentKind::Goal => {
                    let step = if commitment.kind == CommitmentKind::Obligation {
                        step::OBLIGATION_PAST_DUE
                    } else {
                        step::GOAL_PAST_DUE
                    };
                    let source = PressureSource::Commitment {
                        subject: *subject,
                        key: *key,
                    };
                    *pressed.entry((*subject, source)).or_default() += step;
                }
            }
        }
    }

    for (subject, target) in failing {
        let source = PressureSource::Dependency(target);
        *pressed.entry((subject, source)).or_default() += step::DEPENDENCY_UNAVAILABLE;
    }

    Motion {
        fulfilled,
        pressed: pressed
            .into_iter()
            .map(|((target, source), step)| PressureWritten {
                target,
                source,
                magnitude: PressureMagnitude(
                    state
                        .pressures
                        .get(&target)
                        .and_then(|held| held.get(&source))
                        .map_or(0, |magnitude| magnitude.0)
                        .saturating_add(step),
                ),
            })
            .collect(),
    }
}

/// A routine fulfils when every one of its checks holds, and its `due` then
/// rolls forward by exactly one period per command. Catch-up arithmetic would
/// make the number of fulfilments a function of the operator's tick cadence in
/// a second place; one roll converges over subsequent ticks.
fn routine_rolls(
    state: &WorldState,
    subject: SubjectId,
    commitment: &Commitment,
) -> Option<FictionalMinutes> {
    if !action::preconditions_hold(state, subject, &commitment.checks).is_empty() {
        return None;
    }
    commitment.due.checked_add(commitment.period?)
}

/// Whether a subject's dependency is currently failing it. A closed route is a
/// supply failure; an unusable one is a permission question, and reading
/// authority here would drag `effective_authority` into every tick for every
/// dependency in the world.
pub(super) fn dependency_unavailable(
    state: &WorldState,
    subject: SubjectId,
    target: DependencyTarget,
) -> bool {
    match target {
        DependencyTarget::Route(edge_id) => !state
            .edges
            .get(&edge_id)
            .is_some_and(super::EdgeRecord::is_open),
        // Absence is zero, per pass 3: the depender holds none of what it
        // depends on.
        DependencyTarget::Resource(entity_id) => !state
            .holdings
            .get(&subject)
            .is_some_and(|held| held.contains_key(&entity_id)),
        // The subject it depends on is nowhere in the world's topology.
        DependencyTarget::Subject(other) => !state.positions.contains_key(&other),
    }
}
