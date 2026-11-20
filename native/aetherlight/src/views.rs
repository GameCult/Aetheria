//! Cross-partition views: what a read needs that no one partition's index can
//! answer.
//!
//! A `Docs` projection is a pure function of one document. Two questions the
//! clock asks are joins: "is this dependency failing" reads dependencies with
//! edges, holdings and positions (two of its arms are anti-joins: a resource
//! not held, a subject not placed), and "is this due commitment live" reads
//! commitments with `subjects.retired`. [`Views`] answers them as state derived
//! from those partitions' change lists, so a tick reads the work due and the
//! rows failing instead of every dependency or every dead pledge in the world.
//!
//! `Views` is derived, never state: not digested, not encoded, never exported,
//! and ignored by `WorldState`'s equality (its `PartialEq` is always true, as a
//! `Docs` index is ignored). The one writer is [`Views::fold`], called by
//! `WorldState::finish` from the open logs before they close, so replay (genesis
//! and every replayed commit close through `finish`) rebuilds it on open and a
//! rollback has nothing to undo: the view never saw the transaction. It is read
//! only with clean logs, which `derive_motion` asserts.

use super::clock::dependency_unavailable;
#[cfg(test)]
use super::docs;
use super::patch::DependencyTarget;
use super::{EntityId, FictionalMinutes, SubjectId, WorldState, is_retired};
use std::collections::{BTreeMap, BTreeSet};
use uuid::Uuid;

#[derive(Clone, Debug, Default)]
pub(crate) struct Views {
    /// When live subjects' commitments fall due: a point for each distinct
    /// due, holding the subjects that have a commitment due then. A retired
    /// subject has no point.
    live_due: BTreeMap<FictionalMinutes, BTreeSet<SubjectId>>,
    /// The (dependent, target) pairs failing a live dependent: a closed route,
    /// a resource the dependent does not hold, a subject nobody places.
    failing: BTreeSet<(SubjectId, DependencyTarget)>,
}

/// A view is derived and carries no content of its own, so two worlds with the
/// same documents are equal whatever their views hold.
impl PartialEq for Views {
    fn eq(&self, _: &Self) -> bool {
        true
    }
}

impl Eq for Views {}

/// Whether the six partitions the views read have no open log: the only time a
/// view is true.
pub(crate) fn readable(state: &WorldState) -> bool {
    state.commitments.is_clean()
        && state.subjects.is_clean()
        && state.dependencies.is_clean()
        && state.edges.is_clean()
        && state.holdings.is_clean()
        && state.positions.is_clean()
}

impl Views {
    /// Subjects with a live commitment due at or before `to`.
    pub(crate) fn due_by(&self, to: FictionalMinutes) -> BTreeSet<SubjectId> {
        #[cfg(test)]
        docs::count_rows(self.live_due.range(..=to).map(|(_, held)| held.len() as u64).sum());
        self.live_due
            .range(..=to)
            .flat_map(|(_, held)| held.iter().copied())
            .collect()
    }

    /// Every failing (dependent, target), in order.
    pub(crate) fn failing(&self) -> impl Iterator<Item = &(SubjectId, DependencyTarget)> {
        #[cfg(test)]
        docs::count_rows(self.failing.len() as u64);
        self.failing.iter()
    }

    /// Brings the views up to the open logs of the six partitions. Called by
    /// `WorldState::finish` before the logs close; nothing else writes a view.
    pub(crate) fn fold(&mut self, state: &WorldState) {
        // Dues. A subject whose commitments or retirement changed loses the
        // points its commitments held before and regains them if it is live.
        let mut before: BTreeMap<SubjectId, Option<_>> = BTreeMap::new();
        for (subject, old, _) in state.commitments.changes() {
            before.insert(*subject, old);
        }
        for (subject, _, _) in state.subjects.changes() {
            before.entry(*subject).or_insert_with(|| state.commitments.get(subject));
        }
        for (subject, old) in &before {
            for commitment in old.into_iter().flat_map(|held| held.values()) {
                if let Some(due) = self.live_due.get_mut(&commitment.due) {
                    due.remove(subject);
                    if due.is_empty() {
                        self.live_due.remove(&commitment.due);
                    }
                }
            }
            if is_retired(state, *subject) {
                continue;
            }
            for commitment in state.commitments.get(subject).into_iter().flat_map(|held| held.values()) {
                self.live_due.entry(commitment.due).or_default().insert(*subject);
            }
        }

        // Failing dependencies. A subject whose dependencies, holdings or
        // retirement changed has every row re-judged; a route or a placement
        // that changed re-judges its dependents.
        let rejudged: BTreeSet<SubjectId> = state
            .dependencies
            .changes()
            .map(|(subject, _, _)| *subject)
            .chain(state.holdings.changes().map(|(subject, _, _)| *subject))
            .chain(state.subjects.changes().map(|(subject, _, _)| *subject))
            .collect();
        for subject in &rejudged {
            let rows: Vec<_> = self
                .failing
                .range(
                    (*subject, DependencyTarget::Resource(EntityId(Uuid::nil())))
                        ..=(*subject, DependencyTarget::Subject(SubjectId(Uuid::max()))),
                )
                .copied()
                .collect();
            for row in rows {
                self.failing.remove(&row);
            }
            if is_retired(state, *subject) {
                continue;
            }
            for target in state.dependencies.get(subject).into_iter().flatten() {
                if dependency_unavailable(state, *subject, *target) {
                    self.failing.insert((*subject, *target));
                }
            }
        }
        let moved: BTreeSet<DependencyTarget> = state
            .edges
            .changes()
            .map(|(edge, _, _)| DependencyTarget::Route(*edge))
            .chain(state.positions.changes().map(|(subject, _, _)| DependencyTarget::Subject(*subject)))
            .collect();
        // A changed dependencies key is already in `rejudged`, so the dependents
        // left to judge are the unchanged ones, which the committed index names
        // without walking the log once per target.
        for target in moved {
            for (_, dependent) in state.dependencies.indexed_unchanged(target..=target) {
                if rejudged.contains(&dependent) {
                    continue;
                }
                let row = (dependent, target);
                if !is_retired(state, dependent) && dependency_unavailable(state, dependent, target) {
                    self.failing.insert(row);
                } else {
                    self.failing.remove(&row);
                }
            }
        }
    }
}

/// The views as a scan of the committed documents derives them, and the
/// oracle's other tools (test builds).
#[cfg(test)]
impl Views {
    pub(crate) fn rebuild(state: &WorldState) -> Self {
        let mut views = Self::default();
        for (subject, held) in state.commitments.iter() {
            if is_retired(state, *subject) {
                continue;
            }
            for commitment in held.values() {
                views.live_due.entry(commitment.due).or_default().insert(*subject);
            }
        }
        for (subject, targets) in state.dependencies.iter() {
            if is_retired(state, *subject) {
                continue;
            }
            for target in targets {
                if dependency_unavailable(state, *subject, *target) {
                    views.failing.insert((*subject, *target));
                }
            }
        }
        views
    }

    /// Content equality, which `PartialEq` deliberately is not.
    pub(crate) fn same_as(&self, other: &Self) -> bool {
        self.live_due == other.live_due && self.failing == other.failing
    }

    /// Damages the view in one place: a stray failing row, else a missing
    /// due point (the latest, which only its own subjects sit at). False when there
    /// is nothing to damage that way.
    pub(crate) fn corrupt_for_test(&mut self, stray: bool) -> bool {
        if stray {
            let row = (SubjectId(Uuid::from_u128(1)), DependencyTarget::Subject(SubjectId(Uuid::from_u128(2))));
            return self.failing.insert(row);
        }
        let Some(due) = self.live_due.keys().next_back().copied() else {
            return false;
        };
        self.live_due.remove(&due);
        true
    }

    /// The subjects with a dependency on `target`, by a scan of every row.
    pub(crate) fn dependents_by_scan(state: &WorldState, target: DependencyTarget) -> Vec<SubjectId> {
        state
            .dependencies
            .iter()
            .filter(|(_, held)| held.contains(&target))
            .map(|(subject, _)| *subject)
            .collect()
    }

    /// The subjects with a dependency on `target`, from the dependencies
    /// partition's `DependsOn` index (true mid-transaction).
    pub(crate) fn dependents_of(state: &WorldState, target: DependencyTarget) -> Vec<SubjectId> {
        state
            .dependencies
            .indexed(target..=target)
            .into_iter()
            .map(|(_, subject)| subject)
            .collect()
    }
}
