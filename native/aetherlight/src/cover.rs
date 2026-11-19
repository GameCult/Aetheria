//! The budgeted connected cover: one tick's partition of the active
//! opportunities into cells.
//!
//! This organ owns the partition and nothing else. [`derive_cover`] is pure
//! over its arguments — no clock, no entropy, no mailbox, no filesystem — and
//! the [`Cover`] it returns is derived and disposable. A cell never enters
//! `WorldState`, a `CommandEnvelope`, a `ScopePreimage`, a `DecisionOpportunity`,
//! or any `Declaration`; the kernel never learns a tick happened except through
//! `AdvanceTime` and ordinary one-opportunity submissions.
//!
//! The cover is a partition, never a filter: every active subject is in exactly
//! one cell every tick. The budget decides at what resolution, not whether.

use super::{ControllerMode, DecisionOpportunity, SubjectId, WorldId};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::{BTreeMap, BTreeSet};
use uuid::Uuid;

/// A place, resource, or channel with more associated subjects than this
/// contributes a star centred on its lowest member instead of a clique.
/// Connectivity is identical for component purposes and the edge count stops
/// being quadratic in a crowded room.
pub(crate) const AGENCY_STAR_THRESHOLD: usize = 16;

/// The namespace every derived cover id is salted with. A fixed constant, never
/// regenerated: two worlds in one process are separated by the world id inside
/// the name, not by a second namespace.
const COVER_NAMESPACE: &str = "ghostlight.cover.v1";

/// A tick's ordinal: the number of clock advances the world had committed when
/// the tick opened. The driver reads it from the world and freezes it in the
/// tick document; it is never derived from `now`, so it does not depend on the
/// span. It is threaded into every derived command id, so a resumed tick
/// re-submits the same ids and the kernel's idempotency ledger answers with the
/// original receipts instead of double-committing.
#[derive(
    Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize, Default,
)]
#[serde(transparent)]
pub struct TickIndex(pub u64);

/// How a subject was represented this tick. Derived from the cell, carried
/// nowhere in world state: representation is a compute budget, not world truth.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "resolution", rename_all = "snake_case")]
pub enum Resolution {
    Detail,
    Coarse { constituents: usize },
}

/// Derived from the partition, so a replayed tick over the same inputs produces
/// byte-identical ids. sha256 over `(namespace, world, tick, size, lowest
/// member)`; no `uuid/v5`.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct CellId(Uuid);

impl std::fmt::Display for CellId {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(formatter, "{}", self.0)
    }
}

fn derive_uuid(name: &str) -> Uuid {
    let mut hasher = Sha256::new();
    hasher.update(COVER_NAMESPACE.as_bytes());
    hasher.update([0u8]);
    hasher.update(name.as_bytes());
    let bytes: [u8; 16] = hasher.finalize()[..16]
        .try_into()
        .expect("sha256 yields at least sixteen bytes");
    Uuid::from_bytes(bytes)
}

/// `members.len()` is in the name so a component that splits differently under
/// a different cap yields a different cell, and a resumed tick over the same
/// inputs yields the same one.
fn cell_id(world: WorldId, tick: TickIndex, members: &[SubjectId]) -> CellId {
    let lowest = members
        .first()
        .copied()
        .expect("a cell always has at least one member");
    CellId(derive_uuid(&format!(
        "cell|{world:?}|{}|{}|{lowest:?}",
        tick.0,
        members.len()
    )))
}

/// The stable, derived identity a cell's controller-work row is keyed by.
pub(crate) fn cell_work_uuid(world: WorldId, cell: CellId, tick: TickIndex) -> Uuid {
    derive_uuid(&format!("cell-work|{world:?}|{cell}|{}", tick.0))
}

/// One constituent's submission id. Deterministic, so a resumed cell re-submits
/// the same id and the kernel answers from its idempotency ledger.
pub(crate) fn cell_constituent_uuid(
    world: WorldId,
    cell: CellId,
    subject: SubjectId,
    tick: TickIndex,
) -> Uuid {
    derive_uuid(&format!("cell-act|{world:?}|{cell}|{subject:?}|{}", tick.0))
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Constituent {
    pub subject: SubjectId,
    pub(crate) opportunity: DecisionOpportunity,
}

/// One inference's worth of the world. A singleton keeps the detail path and its
/// membrane; a group is one inference over N labeled, never-unioned views.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Cell {
    Singleton {
        id: CellId,
        tick: TickIndex,
        member: Constituent,
    },
    /// `members` is ascending by `SubjectId`; the index into it is the
    /// constituent handle the model-facing tool names carry.
    Group {
        id: CellId,
        tick: TickIndex,
        members: Vec<Constituent>,
    },
}

impl Cell {
    pub(crate) fn members(&self) -> &[Constituent] {
        match self {
            Self::Singleton { member, .. } => std::slice::from_ref(member),
            Self::Group { members, .. } => members,
        }
    }
}

/// Derived, disposable, dropped at the end of a tick.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Cover {
    pub tick: TickIndex,
    pub cells: Vec<Cell>,
    /// True when the constituent cap could not hold every active subject inside
    /// the cell budget, so the cap yielded and every cell grew. "Every active
    /// subject is in the cover every tick" is an ontology invariant; the cap is
    /// a prompt-size preference. Rendered read-only: it is the honest signal
    /// that the world outgrew its prompt budget rather than a truncation nobody
    /// sees.
    pub oversubscribed: bool,
}

impl Cover {
    pub fn singletons(&self) -> usize {
        self.cells
            .iter()
            .filter(|cell| matches!(cell, Cell::Singleton { .. }))
            .count()
    }

    pub fn groups(&self) -> usize {
        self.cells.len() - self.singletons()
    }
}

/// How many inferences one tick may spend, and how large one prompt may get.
/// Two budgets, deliberately not one: only the second bounds tokens.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct CoverBudget {
    /// B: cells per tick.
    pub cells: u16,
    /// C: the per-cell constituent cap. At least two, or a group is a singleton
    /// with extra steps.
    pub constituent_cap: u16,
    /// U: singleton slots reserved for the head of the attention order. The
    /// rest of the singleton slots carry the rotation window.
    pub urgency_slots: u16,
}

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum CoverBudgetError {
    #[error("cover cell budget must be at least one")]
    NoCells,
    #[error("cover constituent cap must be at least two")]
    CapTooSmall,
}

impl CoverBudget {
    pub fn validated(self) -> Result<Self, CoverBudgetError> {
        if self.cells < 1 {
            return Err(CoverBudgetError::NoCells);
        }
        if self.constituent_cap < 2 {
            return Err(CoverBudgetError::CapTooSmall);
        }
        Ok(self)
    }
}

/// The scheduler's own projection, wider than any subject view and reachable
/// from no prompt builder: it is constructed by the tick driver, handed to
/// [`derive_cover`], and dropped. `run_cell` takes a `&Cell`, which contains no
/// graph, so a controller organ that could read adjacency fails to compile
/// rather than failing a test.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct AgencyGraph {
    /// Ascending. Exactly the subjects with a non-Human controller assignment.
    pub(crate) subjects: Vec<SubjectId>,
    /// Undirected, canonicalised `(lo, hi)` with `lo < hi`.
    pub(crate) edges: BTreeSet<(SubjectId, SubjectId)>,
}

impl AgencyGraph {
    /// A clique below the star threshold, a star above it. Connectivity for
    /// component purposes is identical either way.
    pub(crate) fn relate(&mut self, group: &BTreeSet<SubjectId>) {
        let members: Vec<SubjectId> = group.iter().copied().collect();
        if members.len() < 2 {
            return;
        }
        if members.len() > AGENCY_STAR_THRESHOLD {
            let centre = members[0];
            for member in &members[1..] {
                self.link(centre, *member);
            }
            return;
        }
        for (index, one) in members.iter().enumerate() {
            for other in &members[index + 1..] {
                self.link(*one, *other);
            }
        }
    }

    pub(crate) fn link(&mut self, one: SubjectId, other: SubjectId) {
        if one == other {
            return;
        }
        let pair = if one < other {
            (one, other)
        } else {
            (other, one)
        };
        self.edges.insert(pair);
    }
}

/// The partition. Pure over `(world, tick, opportunities, graph, budget)`.
///
/// `opportunities` arrives in the kernel's one attention order (pressure, then
/// attention debt, then id) and is never re-sorted and never filtered here: a
/// Human opportunity is skipped because a human turn is not an inference, which
/// is a fact about cognition rather than a scheduling preference.
pub fn derive_cover(
    world: WorldId,
    tick: TickIndex,
    opportunities: &[DecisionOpportunity],
    graph: &AgencyGraph,
    budget: CoverBudget,
) -> Cover {
    let active: Vec<&DecisionOpportunity> = opportunities
        .iter()
        .filter(|opportunity| opportunity.controller_mode != ControllerMode::Human)
        .collect();
    let count = active.len();
    if count == 0 {
        return Cover {
            tick,
            cells: Vec::new(),
            oversubscribed: false,
        };
    }
    let constituent = |opportunity: &DecisionOpportunity| Constituent {
        subject: opportunity.scope.subject_id,
        opportunity: opportunity.clone(),
    };
    let cell_budget = usize::from(budget.cells).max(1);
    let cap = usize::from(budget.constituent_cap).max(2);

    if count <= cell_budget {
        let cells = active
            .iter()
            .map(|opportunity| {
                let member = constituent(opportunity);
                Cell::Singleton {
                    id: cell_id(world, tick, &[member.subject]),
                    tick,
                    member,
                }
            })
            .collect();
        return Cover {
            tick,
            cells,
            oversubscribed: false,
        };
    }

    // Step 2. Group cells must hold the non-singletons under the cap:
    // `K + ceil((N - K) / C) <= B`. The cap yields when no such partition
    // exists, because coverage is the invariant and the cap is a preference.
    let capacity = cap.saturating_mul(cell_budget);
    let mut oversubscribed = capacity < count;
    let (singleton_slots, effective_cap) = if oversubscribed {
        (0usize, count.div_ceil(cell_budget))
    } else {
        ((capacity - count) / (cap - 1), cap)
    };
    let singleton_slots = singleton_slots.min(count);
    let group_budget = cell_budget - singleton_slots;

    // Step 3. The rotation window. A stable order that does not move with
    // pressure is what lets it carry a guarantee: a subject at index `i` is in
    // the window exactly when `phase == i / R`, and `phase` cycles once every
    // `ceil(N / R)` consecutive ticks because `now` only advances.
    let urgency = usize::from(budget.urgency_slots).min(singleton_slots);
    let reserve = singleton_slots - urgency;
    let ascending: Vec<SubjectId> = {
        let mut ids: Vec<SubjectId> = active
            .iter()
            .map(|opportunity| opportunity.scope.subject_id)
            .collect();
        ids.sort_unstable();
        ids
    };
    let mut chosen: BTreeSet<SubjectId> = BTreeSet::new();
    if reserve > 0 {
        let cycle = count.div_ceil(reserve);
        let phase = usize::try_from(tick.0 % cycle as u64).unwrap_or(0);
        let lo = phase * reserve;
        let hi = (lo + reserve).min(count);
        for subject in &ascending[lo.min(count)..hi] {
            chosen.insert(*subject);
        }
    }
    // Step 4. Urgency, then backfill in the attention order. A subject in both
    // reserves consumes one slot, not two, and every slot is spent.
    for opportunity in active.iter().take(urgency) {
        chosen.insert(opportunity.scope.subject_id);
    }
    for opportunity in &active {
        if chosen.len() >= singleton_slots {
            break;
        }
        chosen.insert(opportunity.scope.subject_id);
    }

    let mut cells: Vec<Cell> = Vec::new();
    for opportunity in &active {
        let subject = opportunity.scope.subject_id;
        if !chosen.contains(&subject) {
            continue;
        }
        let member = constituent(opportunity);
        cells.push(Cell::Singleton {
            id: cell_id(world, tick, &[subject]),
            tick,
            member,
        });
    }

    // Step 5. Connected components over the remainder.
    let rest: Vec<&DecisionOpportunity> = active
        .iter()
        .copied()
        .filter(|opportunity| !chosen.contains(&opportunity.scope.subject_id))
        .collect();
    let rank: BTreeMap<SubjectId, usize> = active
        .iter()
        .enumerate()
        .map(|(index, opportunity)| (opportunity.scope.subject_id, index))
        .collect();
    let mut groups = components(&rest, graph);

    // Step 6. A component larger than the cap is cut in id order. A chunk of a
    // connected component is still a legitimate coarse group, and a graph-aware
    // cut would be a second partitioning heuristic whose only payoff is
    // adjacency inside a prompt that never mentions adjacency.
    let mut split: Vec<Vec<SubjectId>> = Vec::new();
    for component in groups.drain(..) {
        for chunk in component.chunks(effective_cap) {
            split.push(chunk.to_vec());
        }
    }

    // Step 7. Merge the lowest-ranked cells until the group budget is met. The
    // choke falls on the quietest cells; the highest-pressure groups keep the
    // smallest, sharpest prompts. Priority is read from the attention order,
    // which is its one owner — the scheduler computes no second ranking.
    let priority = |members: &Vec<SubjectId>| {
        members
            .iter()
            .filter_map(|subject| rank.get(subject).copied())
            .min()
            .unwrap_or(usize::MAX)
    };
    split.sort_by_key(|members| (priority(members), members[0]));
    let mut head = split.len();
    let mut packed: Vec<Vec<SubjectId>> = Vec::new();
    let mut bucket: Vec<SubjectId> = Vec::new();
    while head > 0 {
        let open = usize::from(!bucket.is_empty());
        if head + packed.len() + open <= group_budget {
            break;
        }
        head -= 1;
        let component = split[head].clone();
        if !bucket.is_empty() && bucket.len() + component.len() > effective_cap {
            packed.push(std::mem::take(&mut bucket));
        }
        bucket.extend(component);
    }
    if !bucket.is_empty() {
        packed.push(bucket);
    }
    split.truncate(head);
    split.append(&mut packed);
    // Component sizes need not pack perfectly into the cap, so the packing pass
    // can end above budget. The cap yields here for the same reason it yields in
    // step 2: coverage is the invariant, prompt size is the preference, and the
    // operator is told rather than shown a silent truncation.
    while split.len() > group_budget && split.len() >= 2 {
        split.sort_by_key(|members| (priority(members), members[0]));
        let last = split.pop().expect("two or more cells were established");
        let previous = split.pop().expect("two or more cells were established");
        let mut merged = previous;
        merged.extend(last);
        split.push(merged);
        oversubscribed = true;
    }

    for members in &mut split {
        members.sort_unstable();
    }
    split.sort_by_key(|members| (priority(members), members[0]));
    let by_subject: BTreeMap<SubjectId, &DecisionOpportunity> = rest
        .iter()
        .map(|opportunity| (opportunity.scope.subject_id, *opportunity))
        .collect();
    for members in split {
        let constituents: Vec<Constituent> = members
            .iter()
            .filter_map(|subject| by_subject.get(subject).map(|entry| constituent(entry)))
            .collect();
        if constituents.is_empty() {
            continue;
        }
        cells.push(Cell::Group {
            id: cell_id(world, tick, &members),
            tick,
            members: constituents,
        });
    }

    debug_assert_eq!(
        cells
            .iter()
            .flat_map(Cell::members)
            .map(|member| member.subject)
            .collect::<BTreeSet<_>>()
            .len(),
        count,
        "the cover is a partition: every active subject appears exactly once"
    );
    debug_assert!(cells.len() <= cell_budget, "the cover respects its budget");

    Cover {
        tick,
        cells,
        oversubscribed,
    }
}

/// Union-find over the remainder's induced subgraph. Deterministic by
/// construction: a `BTreeSet` of edges in `SubjectId` order, components keyed by
/// their lowest member, members ascending.
fn components(rest: &[&DecisionOpportunity], graph: &AgencyGraph) -> Vec<Vec<SubjectId>> {
    let present: BTreeSet<SubjectId> = rest
        .iter()
        .map(|opportunity| opportunity.scope.subject_id)
        .collect();
    let mut parent: BTreeMap<SubjectId, SubjectId> =
        present.iter().map(|subject| (*subject, *subject)).collect();

    fn find(parent: &mut BTreeMap<SubjectId, SubjectId>, node: SubjectId) -> SubjectId {
        let mut root = node;
        while let Some(next) = parent.get(&root).copied() {
            if next == root {
                break;
            }
            root = next;
        }
        let mut cursor = node;
        while let Some(next) = parent.get(&cursor).copied() {
            if next == cursor {
                break;
            }
            parent.insert(cursor, root);
            cursor = next;
        }
        root
    }

    for (one, other) in &graph.edges {
        if !present.contains(one) || !present.contains(other) {
            continue;
        }
        let left = find(&mut parent, *one);
        let right = find(&mut parent, *other);
        if left != right {
            let (low, high) = if left < right {
                (left, right)
            } else {
                (right, left)
            };
            parent.insert(high, low);
        }
    }

    let mut buckets: BTreeMap<SubjectId, Vec<SubjectId>> = BTreeMap::new();
    for subject in &present {
        let root = find(&mut parent, *subject);
        buckets.entry(root).or_default().push(*subject);
    }
    buckets
        .into_values()
        .map(|mut members| {
            members.sort_unstable();
            members
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{DecisionScope, ScopeDigest};

    fn subject(index: u128) -> SubjectId {
        serde_json::from_value(serde_json::json!(Uuid::from_u128(index).to_string()))
            .expect("a subject id parses from its canonical uuid")
    }

    fn opportunity(index: u128, mode: ControllerMode) -> DecisionOpportunity {
        let subject_id = subject(index);
        DecisionOpportunity {
            world_id: WorldId::nil_for_test(),
            revision: 1,
            scope_digest: ScopeDigest::fixture(&format!("scope-{index}")),
            scope: DecisionScope { subject_id },
            controller_id: serde_json::from_value(serde_json::json!(
                Uuid::from_u128(index + 1_000_000).to_string()
            ))
            .expect("a controller id parses from its canonical uuid"),
            controller_mode: mode,
            affordance_ids: Vec::new(),
        }
    }

    fn active(count: u128) -> Vec<DecisionOpportunity> {
        (1..=count)
            .map(|index| opportunity(index, ControllerMode::OperationalAgent))
            .collect()
    }

    fn budget(cells: u16, cap: u16, urgency: u16) -> CoverBudget {
        CoverBudget {
            cells,
            constituent_cap: cap,
            urgency_slots: urgency,
        }
    }

    fn covered(cover: &Cover) -> Vec<SubjectId> {
        let mut subjects: Vec<SubjectId> = cover
            .cells
            .iter()
            .flat_map(Cell::members)
            .map(|member| member.subject)
            .collect();
        subjects.sort_unstable();
        subjects
    }

    fn derive(
        opportunities: &[DecisionOpportunity],
        graph: &AgencyGraph,
        budget: CoverBudget,
        tick: u64,
    ) -> Cover {
        derive_cover(
            WorldId::nil_for_test(),
            TickIndex(tick),
            opportunities,
            graph,
            budget,
        )
    }

    /// Verification 21, at the design profile: 2,400 subjects, 240 cells, a cap
    /// of 24, 36 urgency slots. Every subject exactly once, the budget held, and
    /// the head of the attention order attended in detail.
    #[test]
    fn soul_the_design_profile_covers_every_subject_exactly_once_inside_its_budget() {
        let opportunities = active(2_400);
        let cover = derive(
            &opportunities,
            &AgencyGraph::default(),
            budget(240, 24, 36),
            0,
        );

        assert_eq!(covered(&cover).len(), 2_400);
        assert_eq!(
            covered(&cover).iter().collect::<BTreeSet<_>>().len(),
            2_400,
            "no subject is covered twice"
        );
        assert!(cover.cells.len() <= 240);
        assert!(!cover.oversubscribed);
        // K = floor((24*240 - 2400) / 23) = 146 singleton slots.
        assert_eq!(cover.singletons(), 146);
        assert_eq!(cover.groups(), 94);
        assert!(
            cover.cells.iter().all(|cell| cell.members().len() <= 24),
            "no cell exceeds the constituent cap"
        );

        let head = opportunities[0].scope.subject_id;
        let urgent: BTreeSet<SubjectId> = opportunities[..36]
            .iter()
            .map(|entry| entry.scope.subject_id)
            .collect();
        let singletons: BTreeSet<SubjectId> = cover
            .cells
            .iter()
            .filter_map(|cell| match cell {
                Cell::Singleton { member, .. } => Some(member.subject),
                Cell::Group { .. } => None,
            })
            .collect();
        assert!(
            singletons.contains(&head),
            "the head of the order is a singleton"
        );
        assert!(
            urgent.is_subset(&singletons),
            "every urgency slot is spent on the head of the order"
        );
    }

    /// A human opportunity is not an inference. It is skipped, and skipping it
    /// is not a filter on the cover: it never was a candidate.
    #[test]
    fn a_human_opportunity_is_never_in_a_cell() {
        let opportunities = vec![
            opportunity(1, ControllerMode::Human),
            opportunity(2, ControllerMode::OperationalAgent),
            opportunity(3, ControllerMode::NarrativePersona),
        ];
        let cover = derive(&opportunities, &AgencyGraph::default(), budget(8, 4, 1), 0);
        assert_eq!(covered(&cover), vec![subject(2), subject(3)]);
    }

    /// Below the budget every opportunity is its own cell and the detail path is
    /// untouched.
    #[test]
    fn a_small_world_is_all_singletons() {
        let opportunities = active(5);
        let cover = derive(
            &opportunities,
            &AgencyGraph::default(),
            budget(240, 24, 36),
            0,
        );
        assert_eq!(cover.singletons(), 5);
        assert_eq!(cover.groups(), 0);
        assert!(!cover.oversubscribed);
    }

    /// The cap yields, the budget does not. Every subject is still covered
    /// exactly once and the operator learns the world outgrew the prompt bound.
    #[test]
    fn soul_an_oversubscribed_world_relaxes_the_cap_rather_than_dropping_a_subject() {
        let opportunities = active(100);
        let cover = derive(&opportunities, &AgencyGraph::default(), budget(10, 4, 2), 0);
        assert!(cover.oversubscribed);
        assert_eq!(covered(&cover).len(), 100);
        assert_eq!(covered(&cover).iter().collect::<BTreeSet<_>>().len(), 100);
        assert!(cover.cells.len() <= 10);
        assert_eq!(cover.singletons(), 0, "no slot is spent on detail here");
    }

    /// The fairness arithmetic, with no persisted field behind it: a subject
    /// continuously active for `ceil(N / R)` ticks is a singleton at least once.
    #[test]
    fn soul_every_subject_reaches_a_singleton_within_the_rotation_window() {
        // N = 25, B = 12, C = 8, U = 1 → K = floor((96 - 25) / 7) = 10, R = 9.
        let opportunities = active(25);
        let budget = budget(12, 8, 1);
        let cycle = 25usize.div_ceil(9);
        let mut seen: BTreeSet<SubjectId> = BTreeSet::new();
        for tick in 0..cycle as u64 {
            let cover = derive(&opportunities, &AgencyGraph::default(), budget, tick);
            for cell in &cover.cells {
                if let Cell::Singleton { member, .. } = cell {
                    seen.insert(member.subject);
                }
            }
        }
        let all: BTreeSet<SubjectId> = opportunities
            .iter()
            .map(|entry| entry.scope.subject_id)
            .collect();
        assert_eq!(seen, all, "the window closes over every subject");
    }

    /// A narrow reserve still closes, it just takes longer. The guarantee is
    /// `ceil(N / R)`, never `ceil(N / B)`: giving every cell to a singleton
    /// would leave subjects out of the cover, which the ontology forbids.
    #[test]
    fn the_rotation_guarantee_is_over_the_reserve_not_the_cell_budget() {
        let opportunities = active(25);
        // K = floor((8*10 - 25) / 7) = 7, U = 6 → R = 1, cycle = 25.
        let budget = budget(10, 8, 6);
        let mut seen: BTreeSet<SubjectId> = BTreeSet::new();
        for tick in 0..25u64 {
            let cover = derive(&opportunities, &AgencyGraph::default(), budget, tick);
            for cell in &cover.cells {
                if let Cell::Singleton { member, .. } = cell {
                    seen.insert(member.subject);
                }
            }
        }
        assert_eq!(seen.len(), 25);
    }

    /// The cover is derived, not remembered: the same inputs yield the same
    /// partition, the same ids, and the same order.
    #[test]
    fn soul_the_same_inputs_derive_an_identical_partition() {
        let opportunities = active(60);
        let mut graph = AgencyGraph::default();
        graph.link(subject(3), subject(4));
        graph.link(subject(4), subject(5));
        let one = derive(&opportunities, &graph, budget(10, 8, 2), 7);
        let other = derive(&opportunities, &graph, budget(10, 8, 2), 7);
        assert_eq!(one, other);
    }

    /// Connected subjects land in one cell when the budget allows it.
    #[test]
    fn a_connected_component_becomes_one_coarse_cell() {
        let opportunities = active(20);
        let mut graph = AgencyGraph::default();
        for index in 10..20 {
            graph.link(subject(10), subject(index));
        }
        let cover = derive(&opportunities, &graph, budget(4, 12, 0), 0);
        let component: BTreeSet<SubjectId> = (10..20).map(subject).collect();
        let together = cover.cells.iter().any(|cell| {
            let members: BTreeSet<SubjectId> =
                cell.members().iter().map(|entry| entry.subject).collect();
            component.is_subset(&members)
        });
        assert!(together, "one component was not split across cells");
    }

    /// Property sweep: the budget and the cap hold, and coverage is exact.
    #[test]
    fn soul_cell_count_and_cap_hold_across_profiles() {
        for count in [1u128, 3, 7, 25, 100, 401] {
            for cells in [1u16, 2, 5, 12, 240] {
                for cap in [2u16, 3, 8, 24] {
                    let opportunities = active(count);
                    let cover = derive(
                        &opportunities,
                        &AgencyGraph::default(),
                        budget(cells, cap, 3),
                        11,
                    );
                    assert!(
                        cover.cells.len() <= usize::from(cells),
                        "count {count} cells {cells} cap {cap} exceeded its budget"
                    );
                    assert_eq!(
                        covered(&cover).len(),
                        usize::try_from(count).expect("a test fixture fits usize"),
                        "count {count} cells {cells} cap {cap} lost a subject"
                    );
                    if usize::from(cap) * usize::from(cells)
                        >= usize::try_from(count).expect("a test fixture fits usize")
                    {
                        assert!(!cover.oversubscribed);
                        assert!(
                            cover
                                .cells
                                .iter()
                                .all(|cell| cell.members().len() <= usize::from(cap)),
                            "count {count} cells {cells} cap {cap} exceeded the cap"
                        );
                    } else {
                        assert!(cover.oversubscribed);
                    }
                }
            }
        }
    }

    /// A crowded referent contributes a star, not a clique: connectivity is the
    /// same and the edge count stops being quadratic.
    #[test]
    fn a_crowded_referent_relates_as_a_star() {
        let crowd: BTreeSet<SubjectId> = (1..=40).map(subject).collect();
        let mut graph = AgencyGraph::default();
        graph.relate(&crowd);
        assert_eq!(graph.edges.len(), 39);

        let small: BTreeSet<SubjectId> = (1..=4).map(subject).collect();
        let mut clique = AgencyGraph::default();
        clique.relate(&small);
        assert_eq!(clique.edges.len(), 6);
    }

    /// Derived ids are a pure function of their tuple, and distinct tuples do
    /// not collide.
    #[test]
    fn derived_ids_are_pure_and_distinct() {
        let world = WorldId::nil_for_test();
        let cell = cell_id(world, TickIndex(4), &[subject(1), subject(2)]);
        assert_eq!(
            cell,
            cell_id(world, TickIndex(4), &[subject(1), subject(2)])
        );
        assert_ne!(
            cell,
            cell_id(world, TickIndex(5), &[subject(1), subject(2)])
        );
        assert_ne!(cell, cell_id(world, TickIndex(4), &[subject(1)]));

        assert_eq!(
            cell_constituent_uuid(world, cell, subject(1), TickIndex(4)),
            cell_constituent_uuid(world, cell, subject(1), TickIndex(4))
        );
        assert_ne!(
            cell_constituent_uuid(world, cell, subject(1), TickIndex(4)),
            cell_constituent_uuid(world, cell, subject(2), TickIndex(4))
        );
        assert_ne!(
            cell_constituent_uuid(world, cell, subject(1), TickIndex(4)),
            cell_work_uuid(world, cell, TickIndex(4))
        );
    }

    #[test]
    fn a_budget_without_cells_or_with_a_singleton_cap_is_refused() {
        assert_eq!(budget(0, 4, 0).validated(), Err(CoverBudgetError::NoCells));
        assert_eq!(
            budget(4, 1, 0).validated(),
            Err(CoverBudgetError::CapTooSmall)
        );
        assert!(budget(4, 2, 0).validated().is_ok());
    }

    /// The rotation guarantee at the design profile rather than at a toy size.
    /// `K = 146`, `U = 36`, so the reserve is `R = 110` and the window closes in
    /// `ceil(2400 / 110) = 22` ticks. Membership is stable across the sweep,
    /// which is the precondition the tight bound is stated under.
    #[test]
    fn soul_b_the_profile_rotation_closes_over_every_subject_in_ceil_n_over_reserve_ticks() {
        let opportunities = active(2_400);
        let budget = budget(240, 24, 36);
        let reserve = 146 - 36;
        let cycle = 2_400usize.div_ceil(reserve);
        assert_eq!(cycle, 22, "the profile's window is not 22 ticks wide");

        let mut seen: BTreeSet<SubjectId> = BTreeSet::new();
        for tick in 0..cycle as u64 {
            let cover = derive(&opportunities, &AgencyGraph::default(), budget, tick);
            assert_eq!(cover.singletons(), 146);
            for cell in &cover.cells {
                if let Cell::Singleton { member, .. } = cell {
                    seen.insert(member.subject);
                }
            }
        }
        assert_eq!(
            seen.len(),
            2_400,
            "{} subjects never reached detail inside the window",
            2_400 - seen.len()
        );

        // One tick short of the cycle must not already close it, or the bound
        // is being read off a coincidence rather than the arithmetic.
        let mut short: BTreeSet<SubjectId> = BTreeSet::new();
        for tick in 0..(cycle - 1) as u64 {
            for cell in &derive(&opportunities, &AgencyGraph::default(), budget, tick).cells {
                if let Cell::Singleton { member, .. } = cell {
                    short.insert(member.subject);
                }
            }
        }
        assert!(short.len() < 2_400, "the window closed a tick early");
    }

    /// The urgency slots go to the head of the attention order the kernel
    /// already owns, and the rotation window goes to ascending id order. The
    /// fixture arrives in descending id order so the two rankings cannot be
    /// confused with each other, and no third ranking exists to explain the
    /// result.
    #[test]
    fn soul_b_urgency_slots_follow_the_attention_order_not_the_id_order() {
        let mut opportunities = active(40);
        opportunities.reverse();
        // N = 40, B = 10, C = 8 → K = floor((80 - 40) / 7) = 5, U = 3, R = 2.
        let cover = derive(&opportunities, &AgencyGraph::default(), budget(10, 8, 3), 0);
        let singletons: BTreeSet<SubjectId> = cover
            .cells
            .iter()
            .filter_map(|cell| match cell {
                Cell::Singleton { member, .. } => Some(member.subject),
                Cell::Group { .. } => None,
            })
            .collect();
        assert_eq!(singletons.len(), 5);

        let head: BTreeSet<SubjectId> = opportunities[..3]
            .iter()
            .map(|entry| entry.scope.subject_id)
            .collect();
        assert!(
            head.is_subset(&singletons),
            "the urgency slots did not follow the attention order"
        );
        // The window is the ascending-id reserve, which here is the tail of the
        // attention order: two rankings, both honoured, neither invented.
        assert!(
            singletons.contains(&subject(1)) && singletons.contains(&subject(2)),
            "the rotation window did not follow ascending id order"
        );
    }

    /// The partition is a pure function of its arguments, and the attention
    /// order is one of those arguments rather than an incidental input. Equal
    /// inputs derive an equal cover; a permuted order derives a different one,
    /// because urgency and backfill read that order and `order_opportunities`
    /// is its one owner.
    #[test]
    fn soul_b_the_cover_is_pure_and_the_attention_order_is_a_real_input() {
        let opportunities = active(40);
        let mut graph = AgencyGraph::default();
        graph.link(subject(11), subject(12));
        let budget = budget(10, 8, 3);
        let ids = |cover: &Cover| -> Vec<CellId> {
            cover
                .cells
                .iter()
                .map(|cell| match cell {
                    Cell::Singleton { id, .. } | Cell::Group { id, .. } => *id,
                })
                .collect()
        };

        let one = derive(&opportunities, &graph, budget, 5);
        let other = derive(&opportunities, &graph, budget, 5);
        assert_eq!(one, other, "identical inputs derived different covers");
        assert_eq!(
            serde_json::to_string(&ids(&one)).unwrap(),
            serde_json::to_string(&ids(&other)).unwrap(),
            "identical inputs derived different cell ids"
        );

        let mut reversed = opportunities.clone();
        reversed.reverse();
        let permuted = derive(&reversed, &graph, budget, 5);
        assert_eq!(
            covered(&one),
            covered(&permuted),
            "a permutation lost or duplicated a subject"
        );
        let singletons = |cover: &Cover| -> BTreeSet<SubjectId> {
            cover
                .cells
                .iter()
                .filter_map(|cell| match cell {
                    Cell::Singleton { member, .. } => Some(member.subject),
                    Cell::Group { .. } => None,
                })
                .collect()
        };
        assert_ne!(
            singletons(&one),
            singletons(&permuted),
            "the attention order is not an input to the partition"
        );
        // Coverage survives a permutation, but so does the sizing decision
        // that partition made: shuffling the same subjects must not change
        // how many cells they filled, only which subjects the singleton slots
        // picked out.
        assert_eq!(
            one.cells.len(),
            permuted.cells.len(),
            "a permutation changed how many cells the same subjects filled"
        );
    }

    /// Every cell in one cover carries that cover's tick index. This is what the
    /// driver's "advance the clock after the cells" ordering buys: one `now`,
    /// one phase, one set of derived command ids per tick.
    #[test]
    fn soul_b_every_cell_shares_the_covers_tick_index() {
        let opportunities = active(60);
        for tick in [0u64, 1, 7, 4_000] {
            let cover = derive(
                &opportunities,
                &AgencyGraph::default(),
                budget(10, 8, 2),
                tick,
            );
            assert_eq!(cover.tick, TickIndex(tick));
            for cell in &cover.cells {
                let carried = match cell {
                    Cell::Singleton { tick, .. } | Cell::Group { tick, .. } => *tick,
                };
                assert_eq!(carried, cover.tick, "a cell carried another tick's index");
            }
        }
    }

    /// Derived ids are collision-free across every cell of every tick in a full
    /// sweep, and a cell key never collides with a constituent key.
    #[test]
    fn soul_b_derived_ids_do_not_collide_across_a_sweep_of_ticks() {
        let world = WorldId::nil_for_test();
        let opportunities = active(600);
        let budget = budget(60, 24, 6);
        let mut cells: BTreeSet<CellId> = BTreeSet::new();
        let mut keys: BTreeSet<Uuid> = BTreeSet::new();
        let mut cell_count = 0usize;
        let mut key_count = 0usize;
        for tick in 0..12u64 {
            let cover = derive(&opportunities, &AgencyGraph::default(), budget, tick);
            for cell in &cover.cells {
                let id = match cell {
                    Cell::Singleton { id, .. } | Cell::Group { id, .. } => *id,
                };
                cells.insert(id);
                cell_count += 1;
                keys.insert(cell_work_uuid(world, id, cover.tick));
                key_count += 1;
                for member in cell.members() {
                    keys.insert(cell_constituent_uuid(world, id, member.subject, cover.tick));
                    key_count += 1;
                }
            }
        }
        assert!(
            cell_count >= 700,
            "the sweep was too small to mean anything"
        );
        assert_eq!(cells.len(), cell_count, "two cells share one id");
        assert_eq!(keys.len(), key_count, "two derived command keys collide");
    }

    /// The cap yields for exactly one reason: no partition inside the budget
    /// respects it. Token capacity alone does not decide that — component shapes
    /// do. Here `C * B = 12 >= N = 9`, and the cap still yields, because two
    /// three-member components and two isolates cannot pack into two cells of
    /// four. Coverage holds and the operator is told.
    #[test]
    fn soul_b_the_cap_yields_when_component_shapes_cannot_pack_inside_the_budget() {
        let opportunities = active(9);
        let mut graph = AgencyGraph::default();
        graph.link(subject(2), subject(3));
        graph.link(subject(3), subject(4));
        graph.link(subject(5), subject(6));
        graph.link(subject(6), subject(7));
        let cover = derive(&opportunities, &graph, budget(3, 4, 0), 0);

        assert_eq!(covered(&cover).len(), 9);
        assert_eq!(covered(&cover).iter().collect::<BTreeSet<_>>().len(), 9);
        assert!(cover.cells.len() <= 3);
        assert!(
            cover.cells.iter().any(|cell| cell.members().len() > 4),
            "the cap did not yield: {:?}",
            cover
                .cells
                .iter()
                .map(|cell| cell.members().len())
                .collect::<Vec<_>>()
        );
        assert!(
            cover.oversubscribed,
            "the cap yielded without telling the operator"
        );

        // The same subjects with no edges pack cleanly and the flag stays down,
        // so the flag tracks the partition rather than the subject count.
        let loose = derive(&opportunities, &AgencyGraph::default(), budget(3, 4, 0), 0);
        assert!(!loose.oversubscribed);
        assert!(loose.cells.iter().all(|cell| cell.members().len() <= 4));
    }
}
