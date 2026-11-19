//! The world clock and the motion one tick produces.
//!
//! One entry point into the reducer, [`derive_motion`], reached from `reduce`'s
//! `AdvanceTime` arm and from `apply_effect`'s `TimeAdvanced` arm, so a forged
//! tick is re-derived by the same function that produced the honest one. It is
//! pure over `(state, to)`: no wall clock, no entropy, no caller value beyond
//! the span.

use super::patch::{Commitment, CommitmentKey, CommitmentKind, DependencyTarget, MAX_ROUTE_COST};
use super::{PressureMagnitude, PressureSource, SubjectId, WorldState, action, is_retired};
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

    /// Minutes elapsed since an earlier reading. Saturating because the caller
    /// is the attention order, which reads a stamp `verify_state_shape` already
    /// holds to `<= now`.
    pub(crate) fn since(self, earlier: Self) -> u64 {
        self.0.saturating_sub(earlier.0)
    }
}

/// The span one `AdvanceTime` names. Constructor-checked `1..=MAX_ROUTE_COST`
/// (one year of minutes), reusing pass 2's constant as a span bound rather than
/// an edge bound: an unbounded span lets one command jump the clock past every
/// due date in the world and makes the size of a commit a caller-chosen number.
#[derive(Clone, Copy, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord, Hash)]
#[serde(transparent)]
pub struct TickMinutes(u32);

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
pub(super) fn derive_motion(state: &WorldState, to: FictionalMinutes) -> Motion {
    let mut fulfilled = Vec::new();
    let mut pressed: BTreeMap<(SubjectId, PressureSource), u32> = BTreeMap::new();

    for (subject, held) in &state.commitments {
        // A retired subject cannot act, and neither fulfilling a routine nor
        // pressing an obligation or goal is anything but that subject acting:
        // its commitments stay in history, un-discharged and un-deleted, but
        // the clock stops moving them the moment they are its.
        if is_retired(state, *subject) {
            continue;
        }
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

    for (subject, targets) in &state.dependencies {
        if is_retired(state, *subject) {
            continue;
        }
        for target in targets {
            if dependency_unavailable(state, *subject, *target) {
                let source = PressureSource::Dependency(*target);
                *pressed.entry((*subject, source)).or_default() += step::DEPENDENCY_UNAVAILABLE;
            }
        }
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
