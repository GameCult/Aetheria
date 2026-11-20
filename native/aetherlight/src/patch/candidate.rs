//! The candidate graph of one resolution: what the world holds plus what the
//! patch writes, without copying the world.
//!
//! Every candidate map is an [`Overlay`]: the committed partition read through a
//! base function, and the patch's own writes in a local map. Nothing here
//! iterates a committed partition; each scale-sensitive read is a named method
//! of [`Candidate`] whose cost is bounded by what the patch wrote plus what the
//! read returns, through the partitions' point reads and `indexed`. A
//! `Candidate` lives for one `resolve_patch` call and is never written back:
//! `admit_resolved` stays the only writer of the ontology partitions.

use super::{
    AffordanceKindName, AudienceKey, AuthorityTargetKey, ChannelCandidate, CommitmentKey,
    DependencyTarget, FactCandidate, GrantKey, GrievanceKindName, ImportKey, Key,
    KnowledgeCandidate, KnowledgeSource, KnowledgeSourceKey, OfficeCandidate, OfficeName,
    PressureSource, PressureSourceKey, Quantity, Reach, ReachKey, RouteCandidate, TargetKey,
    grant_key_of, target_key_of,
};
use crate::{AffordanceId, EdgeId, EntityId, SubjectId, WorldState};
use std::collections::{BTreeMap, BTreeSet};

/// One committed partition seen through a patch: reads fall through to the
/// partition by point get, writes land in `local`, and a removal is a `None`
/// there. A `Key::Draft` key has no committed value, so its base is `None`.
pub(super) struct Overlay<'s, K: Ord + Clone, V: Clone> {
    state: &'s WorldState,
    base: fn(&WorldState, &K) -> Option<V>,
    local: BTreeMap<K, Option<V>>,
}

impl<'s, K: Ord + Clone, V: Clone> Overlay<'s, K, V> {
    fn new(state: &'s WorldState, base: fn(&WorldState, &K) -> Option<V>) -> Self {
        Self { state, base, local: BTreeMap::new() }
    }

    pub(super) fn get(&self, key: &K) -> Option<V> {
        match self.local.get(key) {
            Some(written) => written.clone(),
            None => (self.base)(self.state, key),
        }
    }

    pub(super) fn contains_key(&self, key: &K) -> bool {
        match self.local.get(key) {
            Some(written) => written.is_some(),
            None => (self.base)(self.state, key).is_some(),
        }
    }

    pub(super) fn insert(&mut self, key: K, value: V) {
        self.local.insert(key, Some(value));
    }

    /// The value the key held, if any.
    pub(super) fn remove(&mut self, key: &K) -> Option<V> {
        let held = self.get(key);
        if held.is_some() {
            self.local.insert(key.clone(), None);
        }
        held
    }

    /// Copies the committed value into `local` on first mutable touch; a miss
    /// writes nothing.
    pub(super) fn get_mut(&mut self, key: &K) -> Option<&mut V> {
        if !self.local.contains_key(key) {
            let committed = (self.base)(self.state, key)?;
            self.local.insert(key.clone(), Some(committed));
        }
        self.local.get_mut(key)?.as_mut()
    }

    /// The entries the patch has touched, removals as `None`.
    pub(super) fn local(&self) -> &BTreeMap<K, Option<V>> {
        &self.local
    }
}

fn existing<Id: Copy>(key: &Key<Id>) -> Option<Id> {
    match key {
        Key::Existing(id) => Some(*id),
        Key::Draft(_) => None,
    }
}

fn target_of(key: &TargetKey) -> Option<DependencyTarget> {
    Some(match key {
        TargetKey::Resource(id) => DependencyTarget::Resource(existing(id)?),
        TargetKey::Route(id) => DependencyTarget::Route(existing(id)?),
        TargetKey::Subject(id) => DependencyTarget::Subject(existing(id)?),
    })
}

fn source_of(key: &PressureSourceKey) -> Option<PressureSource> {
    Some(match key {
        PressureSourceKey::Commitment { subject, key } => {
            PressureSource::Commitment { subject: existing(subject)?, key: *key }
        }
        PressureSourceKey::Dependency(target) => PressureSource::Dependency(target_of(target)?),
        PressureSourceKey::Subject(subject) => PressureSource::Subject(existing(subject)?),
    })
}

fn office_candidate(office: &crate::Office) -> OfficeCandidate {
    OfficeCandidate {
        incumbent: office.incumbent.map(Key::Existing),
        delegated: office.delegated.clone(),
    }
}

fn base_container(state: &WorldState, key: &Key<EntityId>) -> Option<Key<EntityId>> {
    state.entities.get(&existing(key)?)?.container.map(Key::Existing)
}

fn base_route(state: &WorldState, key: &Key<EdgeId>) -> Option<RouteCandidate> {
    let record = state.edges.get(&existing(key)?)?;
    let (from, to) = record.endpoints();
    Some(RouteCandidate {
        from: Key::Existing(from),
        to: Key::Existing(to),
        access: record.access().clone(),
        cost: record.cost(),
        open: record.is_open(),
    })
}

fn base_position(state: &WorldState, key: &Key<SubjectId>) -> Option<Key<EntityId>> {
    state.positions.get(&existing(key)?).map(|position| Key::Existing(position.place))
}

fn base_holding(state: &WorldState, key: &(Key<SubjectId>, Key<EntityId>)) -> Option<Quantity> {
    let (holder, resource) = (existing(&key.0)?, existing(&key.1)?);
    state.holdings.get(&holder)?.get(&resource).copied()
}

fn base_dependency(state: &WorldState, key: &(Key<SubjectId>, TargetKey)) -> Option<()> {
    let (subject, target) = (existing(&key.0)?, target_of(&key.1)?);
    state.dependencies.get(&subject)?.contains(&target).then_some(())
}

fn base_grants(state: &WorldState, key: &Key<SubjectId>) -> Option<BTreeSet<GrantKey>> {
    state.authority.get(&existing(key)?).map(|grants| grants.iter().map(grant_key_of).collect())
}

fn base_office(state: &WorldState, key: &(Key<SubjectId>, OfficeName)) -> Option<OfficeCandidate> {
    state.selection.get(&existing(&key.0)?)?.get(&key.1).map(office_candidate)
}

fn base_forum(
    state: &WorldState,
    key: &GrievanceKindName,
) -> Option<(Key<SubjectId>, AuthorityTargetKey)> {
    state
        .redress
        .get(key)
        .map(|forum| (Key::Existing(forum.forum), target_key_of(forum.standing)))
}

fn base_fact(state: &WorldState, key: &Key<EntityId>) -> Option<FactCandidate> {
    state.facts.get(&existing(key)?).map(|record| match &record.standing {
        crate::FactStanding::Canonical { .. } => FactCandidate::Canonical,
        crate::FactStanding::Claimed { by } => FactCandidate::Claimed(Key::Existing(*by)),
        crate::FactStanding::Ruled => FactCandidate::Ruled,
    })
}

fn base_retired(state: &WorldState, key: &Key<SubjectId>) -> Option<()> {
    crate::is_retired(state, existing(key)?).then_some(())
}

fn base_affordances(
    state: &WorldState,
    key: &Key<SubjectId>,
) -> Option<BTreeSet<Key<AffordanceId>>> {
    state
        .affordance_grants
        .get(&existing(key)?)
        .map(|granted| granted.iter().map(|id| Key::Existing(*id)).collect())
}

fn base_channel(state: &WorldState, key: &Key<EntityId>) -> Option<ChannelCandidate> {
    state.channels.get(&existing(key)?).map(|record| ChannelCandidate {
        reach: match &record.reach {
            Reach::Subjects(members) => {
                ReachKey::Subjects(members.iter().copied().map(Key::Existing).collect())
            }
            Reach::Place(place) => ReachKey::Place(Key::Existing(*place)),
        },
        controller: record.controller.map(Key::Existing),
    })
}

fn base_knowledge(
    state: &WorldState,
    key: &(Key<SubjectId>, Key<EntityId>),
) -> Option<KnowledgeCandidate> {
    let (subject, fact) = (existing(&key.0)?, existing(&key.1)?);
    state.knowledge.get(&(subject, fact)).map(|entry| KnowledgeCandidate {
        confidence: entry.confidence,
        source: match entry.source {
            KnowledgeSource::Witnessed => KnowledgeSourceKey::Witnessed,
            KnowledgeSource::Evidenced => KnowledgeSourceKey::Evidenced,
            KnowledgeSource::Told { by, via } => KnowledgeSourceKey::Told {
                by: Key::Existing(by),
                via: via.map(Key::Existing),
            },
            KnowledgeSource::Seen { by } => KnowledgeSourceKey::Seen { by: Key::Existing(by) },
        },
    })
}

fn base_commitment(state: &WorldState, key: &(Key<SubjectId>, CommitmentKey)) -> Option<()> {
    state.commitments.get(&existing(&key.0)?)?.contains_key(&key.1).then_some(())
}

fn base_pressure(state: &WorldState, key: &(Key<SubjectId>, PressureSourceKey)) -> Option<u32> {
    let (target, source) = (existing(&key.0)?, source_of(&key.1)?);
    state.pressures.get(&target)?.get(&source).map(|magnitude| magnitude.0)
}

/// The candidate graph: canonical topology plus everything this patch declares
/// and operates. Containment, endpoints, and preconditions all read it.
///
/// `authority`, `selection` and `retired` are private: a write to any of them
/// goes through a method that re-judges the holders whose candidate effective
/// authority it can change, so `overlapping` is always the set of holders that
/// hold two grants of one kind over overlapping ground, exactly what a scan of
/// every holder would answer. That equality holds because the committed world is
/// disjoint (every commit checks it, and opening a world verifies it), so only
/// a holder the patch touched can enter the set.
pub(super) struct Candidate<'s> {
    state: &'s WorldState,
    pub(super) containers: Overlay<'s, Key<EntityId>, Key<EntityId>>,
    pub(super) routes: Overlay<'s, Key<EdgeId>, RouteCandidate>,
    pub(super) positions: Overlay<'s, Key<SubjectId>, Key<EntityId>>,
    pub(super) holdings: Overlay<'s, (Key<SubjectId>, Key<EntityId>), Quantity>,
    pub(super) dependencies: Overlay<'s, (Key<SubjectId>, TargetKey), ()>,
    authority: Overlay<'s, Key<SubjectId>, BTreeSet<GrantKey>>,
    selection: Overlay<'s, (Key<SubjectId>, OfficeName), OfficeCandidate>,
    pub(super) redress: Overlay<'s, GrievanceKindName, (Key<SubjectId>, AuthorityTargetKey)>,
    pub(super) facts: Overlay<'s, Key<EntityId>, FactCandidate>,
    retired: Overlay<'s, Key<SubjectId>, ()>,
    pub(super) affordance_holdings: Overlay<'s, Key<SubjectId>, BTreeSet<Key<AffordanceId>>>,
    pub(super) channels: Overlay<'s, Key<EntityId>, ChannelCandidate>,
    pub(super) knowledge: Overlay<'s, (Key<SubjectId>, Key<EntityId>), KnowledgeCandidate>,
    pub(super) commitments: Overlay<'s, (Key<SubjectId>, CommitmentKey), ()>,
    pub(super) pressures: Overlay<'s, (Key<SubjectId>, PressureSourceKey), u32>,
    /// The affordance kinds this patch declares.
    kinds: BTreeSet<AffordanceKindName>,
    overlapping: BTreeSet<Key<SubjectId>>,
}

impl<'s> Candidate<'s> {
    pub(super) fn new(state: &'s WorldState) -> Self {
        Self {
            state,
            containers: Overlay::new(state, base_container),
            routes: Overlay::new(state, base_route),
            positions: Overlay::new(state, base_position),
            holdings: Overlay::new(state, base_holding),
            dependencies: Overlay::new(state, base_dependency),
            authority: Overlay::new(state, base_grants),
            selection: Overlay::new(state, base_office),
            redress: Overlay::new(state, base_forum),
            facts: Overlay::new(state, base_fact),
            retired: Overlay::new(state, base_retired),
            affordance_holdings: Overlay::new(state, base_affordances),
            channels: Overlay::new(state, base_channel),
            knowledge: Overlay::new(state, base_knowledge),
            commitments: Overlay::new(state, base_commitment),
            pressures: Overlay::new(state, base_pressure),
            kinds: BTreeSet::new(),
            overlapping: BTreeSet::new(),
        }
    }

    // ---- Named reads -------------------------------------------------------

    /// Whether the affordance kind is already named: by the catalog (its
    /// `ByKind` index) or by this patch.
    pub(super) fn kind_taken(&self, kind: &AffordanceKindName) -> bool {
        self.kinds.contains(kind) || self.state.kind_taken(kind)
    }

    /// Names a kind for this patch, reporting whether it was already taken.
    pub(super) fn declare_kind(&mut self, kind: AffordanceKindName) -> bool {
        let taken = self.kind_taken(&kind);
        self.kinds.insert(kind);
        taken
    }

    /// Whether a subject in the world, retired or not, holds this import key
    /// (the subjects' `ByImportKey` index).
    pub(super) fn import_key_held(&self, key: &ImportKey) -> bool {
        self.state.import_key_held(key)
    }

    /// Every subject standing at or under a place: the committed subtree's
    /// occupants (`under_place`) less every subject this patch gave a position
    /// or took one from, plus the subjects whose written position the candidate
    /// containment places under it. A written position can only be a draft
    /// place's or a relocation's, so the committed subtree cannot hold it.
    pub(super) fn occupants_under(&self, place: &Key<EntityId>) -> BTreeSet<Key<SubjectId>> {
        let mut occupants: BTreeSet<Key<SubjectId>> = match place {
            Key::Existing(root) => {
                crate::under_place(self.state, *root).into_iter().map(Key::Existing).collect()
            }
            Key::Draft(_) => BTreeSet::new(),
        };
        let written = self.positions.local();
        occupants.retain(|subject| !written.contains_key(subject));
        for (subject, standing) in written {
            if let Some(standing) = standing
                && self.covers(place, standing)
            {
                occupants.insert(subject.clone());
            }
        }
        occupants
    }

    /// The offices of one institution: the committed row merged with the
    /// patch's writes to its offices.
    pub(super) fn offices_of(
        &self,
        institution: &Key<SubjectId>,
    ) -> BTreeMap<OfficeName, OfficeCandidate> {
        let mut offices = BTreeMap::new();
        if let Some(id) = existing(institution)
            && let Some(committed) = self.state.selection.get(&id)
        {
            offices.extend(
                committed.iter().map(|(name, office)| (name.clone(), office_candidate(office))),
            );
        }
        let from = (institution.clone(), OfficeName(String::new()));
        for ((held_by, name), written) in self.selection.local().range(from..) {
            if held_by != institution {
                break;
            }
            match written {
                Some(office) => {
                    offices.insert(name.clone(), office.clone());
                }
                None => {
                    offices.remove(name);
                }
            }
        }
        offices
    }

    /// Every office a subject holds, by (institution, name): the committed
    /// offices it holds (`ByIncumbent`) unless the patch rewrote them, plus the
    /// written offices that name it.
    pub(super) fn offices_held_by(
        &self,
        holder: &Key<SubjectId>,
    ) -> BTreeMap<(Key<SubjectId>, OfficeName), OfficeCandidate> {
        let mut held = BTreeMap::new();
        let written = self.selection.local();
        if let Some(id) = existing(holder) {
            for (_, institution) in self.state.selection.indexed(id..=id) {
                for (name, office) in &self.state.selection[&institution] {
                    let slot = (Key::Existing(institution), name.clone());
                    if office.incumbent == Some(id) && !written.contains_key(&slot) {
                        held.insert(slot, office_candidate(office));
                    }
                }
            }
        }
        for (slot, office) in written {
            if let Some(office) = office
                && office.incumbent.as_ref() == Some(holder)
            {
                held.insert(slot.clone(), office.clone());
            }
        }
        held
    }

    /// What a resource's total was and is: the sum over the holders the patch
    /// wrote, before and after. Every other holder is on both sides of the
    /// equation and cancels.
    pub(super) fn custody(&self, resource: &Key<EntityId>) -> (u128, u128) {
        let (mut before, mut after) = (0u128, 0u128);
        for ((holder, held), written) in self.holdings.local() {
            if held != resource {
                continue;
            }
            let committed = base_holding(self.state, &(holder.clone(), held.clone()));
            before += committed.map_or(0, |quantity| u128::from(quantity.0));
            after += written.map_or(0, |quantity| u128::from(quantity.0));
        }
        (before, after)
    }

    /// Whether `place` is `root`, or reaches it through the candidate container
    /// chain. The walk is bounded by the graph size, so a cycle that does not
    /// include either end still terminates.
    pub(super) fn covers(&self, root: &Key<EntityId>, place: &Key<EntityId>) -> bool {
        let bound = self.state.entities.len() + self.containers.local().len();
        let mut current = Some(place.clone());
        for _ in 0..=bound {
            match current {
                None => return false,
                Some(node) if node == *root => return true,
                Some(node) => current = self.containers.get(&node),
            }
        }
        false
    }

    /// Whether a place reaches itself through its container chain: the same
    /// walk, started one link up so a place is not its own ancestor by
    /// definition.
    pub(super) fn contains_itself(&self, start: &Key<EntityId>) -> bool {
        self.containers.get(start).is_some_and(|parent| self.covers(start, &parent))
    }

    /// Whether two jurisdictions of one kind cover common ground. Structural on
    /// purpose: identity for named subordinates, containment for territory, and
    /// never across the two, so the answer cannot decay when a subject walks
    /// somewhere. Containment is fixed at declaration, so every way of creating
    /// an overlap is a checked operation.
    fn targets_overlap(&self, left: &AuthorityTargetKey, right: &AuthorityTargetKey) -> bool {
        match (left, right) {
            (AuthorityTargetKey::Subject(one), AuthorityTargetKey::Subject(other)) => one == other,
            (AuthorityTargetKey::PlaceSubtree(one), AuthorityTargetKey::PlaceSubtree(other)) => {
                self.covers(one, other) || self.covers(other, one)
            }
            _ => false,
        }
    }

    // ---- Authority, offices and retirement ----------------------------------

    pub(super) fn grants_of(&self, holder: &Key<SubjectId>) -> BTreeSet<GrantKey> {
        self.authority.get(holder).unwrap_or_default()
    }

    pub(super) fn office(&self, slot: &(Key<SubjectId>, OfficeName)) -> Option<OfficeCandidate> {
        self.selection.get(slot)
    }

    pub(super) fn is_retired(&self, subject: &Key<SubjectId>) -> bool {
        self.retired.contains_key(subject)
    }

    /// Own grants plus what every held office lends, over the candidate graph.
    /// A retired institution lends nothing, the same predicate
    /// `delegated_authority` reads canonically: `Retire` does not vacate
    /// offices, so the resolver and the kernel must not disagree about whether
    /// a dead institution's grants still reach its office-holders.
    pub(super) fn effective_authority(&self, holder: &Key<SubjectId>) -> BTreeSet<GrantKey> {
        let mut effective = self.grants_of(holder);
        for ((institution, _), office) in self.offices_held_by(holder) {
            if self.is_retired(&institution) {
                continue;
            }
            for grant in self.grants_of(&institution) {
                if office.delegated.contains(&grant.kind) {
                    effective.insert(grant);
                }
            }
        }
        effective
    }

    /// Whether any holder in the candidate graph holds two grants of one kind
    /// over overlapping ground, from any combination of direct grant and office
    /// delegation.
    pub(super) fn overlapping(&self) -> bool {
        !self.overlapping.is_empty()
    }

    /// Replaces a subject's own grants; an empty set removes the row.
    pub(super) fn set_grants(&mut self, holder: Key<SubjectId>, grants: BTreeSet<GrantKey>) {
        if grants.is_empty() {
            self.authority.remove(&holder);
        } else {
            self.authority.insert(holder.clone(), grants);
        }
        // An institution's grants reach its office-holders.
        let mut affected = self.incumbents_of(&holder);
        affected.insert(holder);
        self.rejudge(affected);
    }

    /// Writes one office, or closes it (`None`).
    pub(super) fn set_office(
        &mut self,
        slot: (Key<SubjectId>, OfficeName),
        office: Option<OfficeCandidate>,
    ) {
        let mut affected = BTreeSet::new();
        affected.extend(self.selection.get(&slot).and_then(|old| old.incumbent));
        affected.extend(office.iter().filter_map(|new| new.incumbent.clone()));
        match office {
            Some(office) => self.selection.insert(slot, office),
            None => {
                self.selection.remove(&slot);
            }
        }
        self.rejudge(affected);
    }

    /// Marks a subject retired. A retired institution lends nothing, so its
    /// office-holders are judged again.
    pub(super) fn retire(&mut self, subject: Key<SubjectId>) {
        self.retired.insert(subject.clone(), ());
        let affected = self.incumbents_of(&subject);
        self.rejudge(affected);
    }

    fn incumbents_of(&self, institution: &Key<SubjectId>) -> BTreeSet<Key<SubjectId>> {
        self.offices_of(institution).into_values().filter_map(|office| office.incumbent).collect()
    }

    fn rejudge(&mut self, holders: BTreeSet<Key<SubjectId>>) {
        for holder in holders {
            let effective: Vec<GrantKey> = self.effective_authority(&holder).into_iter().collect();
            let overlaps = effective.iter().enumerate().any(|(index, one)| {
                effective[index + 1..].iter().any(|other| {
                    one.kind == other.kind && self.targets_overlap(&one.over, &other.over)
                })
            });
            if overlaps {
                self.overlapping.insert(holder);
            } else {
                self.overlapping.remove(&holder);
            }
        }
    }

    /// Whether `subject` stands in the audience, over the candidate graph: the
    /// same question `audience()` answers over committed state, asked before
    /// any ID is minted. A controller is inside its own channel's audience for
    /// the purpose of speaking on it: the horn belongs to the temple.
    pub(super) fn in_audience(
        &self,
        subject: &Key<SubjectId>,
        audience: &AudienceKey,
        speaker: &Key<SubjectId>,
    ) -> bool {
        match audience {
            AudienceKey::Colocated => {
                match (self.positions.get(speaker), self.positions.get(subject)) {
                    (Some(here), Some(there)) => here == there,
                    _ => false,
                }
            }
            AudienceKey::Channel(channel) => {
                let Some(record) = self.channels.get(channel) else {
                    return false;
                };
                if record.controller.as_ref() == Some(subject) {
                    return true;
                }
                match &record.reach {
                    ReachKey::Subjects(members) => members.contains(subject),
                    ReachKey::Place(root) => self
                        .positions
                        .get(subject)
                        .is_some_and(|place| self.covers(root, &place)),
                }
            }
        }
    }
}

/// The reference the overlays are measured against, in test builds only: the
/// candidate graph as it was before overlays, every partition copied into a map
/// and every read a scan of those maps.
#[cfg(test)]
pub(super) struct Materialized {
    pub(super) containers: BTreeMap<Key<EntityId>, Key<EntityId>>,
    pub(super) routes: BTreeMap<Key<EdgeId>, RouteCandidate>,
    pub(super) positions: BTreeMap<Key<SubjectId>, Key<EntityId>>,
    pub(super) holdings: BTreeMap<(Key<SubjectId>, Key<EntityId>), Quantity>,
    pub(super) dependencies: BTreeSet<(Key<SubjectId>, TargetKey)>,
    pub(super) authority: BTreeMap<Key<SubjectId>, BTreeSet<GrantKey>>,
    pub(super) selection: BTreeMap<(Key<SubjectId>, OfficeName), OfficeCandidate>,
    pub(super) redress: BTreeMap<GrievanceKindName, (Key<SubjectId>, AuthorityTargetKey)>,
    pub(super) facts: BTreeMap<Key<EntityId>, FactCandidate>,
    pub(super) retired: BTreeSet<Key<SubjectId>>,
    pub(super) affordance_holdings: BTreeMap<Key<SubjectId>, BTreeSet<Key<AffordanceId>>>,
    pub(super) channels: BTreeMap<Key<EntityId>, ChannelCandidate>,
    pub(super) knowledge: BTreeMap<(Key<SubjectId>, Key<EntityId>), KnowledgeCandidate>,
    pub(super) commitments: BTreeSet<(Key<SubjectId>, CommitmentKey)>,
    pub(super) pressures: BTreeMap<(Key<SubjectId>, PressureSourceKey), u32>,
}

#[cfg(test)]
fn apply_written<K: Ord + Clone, V: Clone>(map: &mut BTreeMap<K, V>, local: &BTreeMap<K, Option<V>>) {
    for (key, written) in local {
        match written {
            Some(value) => {
                map.insert(key.clone(), value.clone());
            }
            None => {
                map.remove(key);
            }
        }
    }
}

#[cfg(test)]
fn apply_written_set<K: Ord + Clone>(set: &mut BTreeSet<K>, local: &BTreeMap<K, Option<()>>) {
    for (key, written) in local {
        if written.is_some() {
            set.insert(key.clone());
        } else {
            set.remove(key);
        }
    }
}

#[cfg(test)]
impl Materialized {
    /// The committed world copied whole: what the resolver seeded its candidate
    /// maps with, moved here as the reference.
    pub(super) fn seeded(state: &WorldState) -> Self {
        let containers: BTreeMap<Key<EntityId>, Key<EntityId>> = state
            .entities
            .iter()
            .filter_map(|(entity_id, record)| {
                record
                    .container
                    .map(|container| (Key::Existing(*entity_id), Key::Existing(container)))
            })
            .collect();
        let routes: BTreeMap<Key<EdgeId>, RouteCandidate> = state
            .edges
            .iter()
            .map(|(edge_id, record)| {
                let (from, to) = record.endpoints();
                (
                    Key::Existing(*edge_id),
                    RouteCandidate {
                        from: Key::Existing(from),
                        to: Key::Existing(to),
                        access: record.access().clone(),
                        cost: record.cost(),
                        open: record.is_open(),
                    },
                )
            })
            .collect();
        let positions: BTreeMap<Key<SubjectId>, Key<EntityId>> = state
            .positions
            .iter()
            .map(|(subject_id, position)| (Key::Existing(*subject_id), Key::Existing(position.place)))
            .collect();
        let holdings: BTreeMap<(Key<SubjectId>, Key<EntityId>), Quantity> = state
            .holdings
            .iter()
            .flat_map(|(subject_id, held)| {
                held.iter().map(move |(entity_id, quantity)| {
                    (
                        (Key::Existing(*subject_id), Key::Existing(*entity_id)),
                        *quantity,
                    )
                })
            })
            .collect();
        let dependencies: BTreeSet<(Key<SubjectId>, TargetKey)> = state
            .dependencies
            .iter()
            .flat_map(|(subject_id, bound)| {
                bound.iter().map(move |target| {
                    (
                        Key::Existing(*subject_id),
                        match target {
                            DependencyTarget::Resource(id) => TargetKey::Resource(Key::Existing(*id)),
                            DependencyTarget::Route(id) => TargetKey::Route(Key::Existing(*id)),
                            DependencyTarget::Subject(id) => TargetKey::Subject(Key::Existing(*id)),
                        },
                    )
                })
            })
            .collect();
        let authority: BTreeMap<Key<SubjectId>, BTreeSet<GrantKey>> = state
            .authority
            .iter()
            .map(|(subject_id, grants)| {
                (
                    Key::Existing(*subject_id),
                    grants.iter().map(grant_key_of).collect(),
                )
            })
            .collect();
        let selection: BTreeMap<(Key<SubjectId>, OfficeName), OfficeCandidate> = state
            .selection
            .iter()
            .flat_map(|(institution, offices)| {
                offices.iter().map(move |(name, office)| {
                    (
                        (Key::Existing(*institution), name.clone()),
                        OfficeCandidate {
                            incumbent: office.incumbent.map(Key::Existing),
                            delegated: office.delegated.clone(),
                        },
                    )
                })
            })
            .collect();
        let redress: BTreeMap<GrievanceKindName, (Key<SubjectId>, AuthorityTargetKey)> = state
            .redress
            .iter()
            .map(|(grievance, forum)| {
                (
                    grievance.clone(),
                    (Key::Existing(forum.forum), target_key_of(forum.standing)),
                )
            })
            .collect();
        let facts: BTreeMap<Key<EntityId>, FactCandidate> = state
            .facts
            .iter()
            .map(|(entity_id, record)| {
                (
                    Key::Existing(*entity_id),
                    match &record.standing {
                        crate::FactStanding::Canonical { .. } => FactCandidate::Canonical,
                        crate::FactStanding::Claimed { by } => FactCandidate::Claimed(Key::Existing(*by)),
                        crate::FactStanding::Ruled => FactCandidate::Ruled,
                    },
                )
            })
            .collect();
        // `Retire` and the affordance grant operations read and write these
        // candidates. `retired` seeds from the committed subject rows: a declared subject is always
        // fresh and unretired, so drafts start out of the set. `affordance_holdings`
        // seeds from committed `affordance_grants` and is extended below for a
        // subject this same patch declares.
        let retired: BTreeSet<Key<SubjectId>> = state
            .subjects
            .keys()
            .filter(|subject_id| crate::is_retired(state, **subject_id))
            .map(|subject_id| Key::Existing(*subject_id))
            .collect();
        let affordance_holdings: BTreeMap<Key<SubjectId>, BTreeSet<Key<AffordanceId>>> = state
            .affordance_grants
            .iter()
            .map(|(subject_id, granted)| {
                (
                    Key::Existing(*subject_id),
                    granted.iter().map(|id| Key::Existing(*id)).collect(),
                )
            })
            .collect();
        let channels: BTreeMap<Key<EntityId>, ChannelCandidate> = state
            .channels
            .iter()
            .map(|(entity_id, record)| {
                (
                    Key::Existing(*entity_id),
                    ChannelCandidate {
                        reach: match &record.reach {
                            Reach::Subjects(members) => {
                                ReachKey::Subjects(members.iter().copied().map(Key::Existing).collect())
                            }
                            Reach::Place(place) => ReachKey::Place(Key::Existing(*place)),
                        },
                        controller: record.controller.map(Key::Existing),
                    },
                )
            })
            .collect();
        // Keys and payload, so an `AcquireKnowledge` that changes nothing is
        // `NoOperationEffect` before any ID is minted. `Witness` is modelled here
        // too: its landing is a subtree of `positions`, both maps are candidate
        // maps, and so the fan-out it will perform at apply is derivable now — a
        // subject this patch relocated into the subtree counts, and a witness that
        // reaches nobody is refused at resolve exactly as `apply_operation`
        // refuses it.
        //
        // `Communicate` never enters, and the difference is not scale. A telling is
        // an act whose occurrence does not depend on its reception: its empty
        // fan-out is legal, so there is nothing for this map to refuse and no
        // refusal for it to predict. A witnessed event is only its reception, so
        // its landing is its legality.
        //
        // Neither operation can write `Told`. This map can represent a `Told`
        // entry, but no `ComponentOp` can construct one: `AcquireKnowledge` takes
        // an `AuthoredSource`, `Witness` takes no source at all, and neither type
        // has a `Told` variant —
        // `action::no_component_op_kind_lowers_to_a_told_knowledge_write` pins both
        // lowering sites that could otherwise drift. If a future operation is ever
        // given the power to write `Told` directly — bypassing `Communicate`'s own
        // apply-time fan-out — it must model that fan-out into this candidate graph
        // first, or `resolve_patch` will silently reason about a knowledge state
        // the apply pass will not produce.
        let knowledge: BTreeMap<(Key<SubjectId>, Key<EntityId>), KnowledgeCandidate> = state
            .knowledge
            .iter()
            .map(|((subject_id, fact), entry)| {
                (
                        (Key::Existing(*subject_id), Key::Existing(*fact)),
                        KnowledgeCandidate {
                            confidence: entry.confidence,
                            source: match entry.source {
                                KnowledgeSource::Witnessed => KnowledgeSourceKey::Witnessed,
                                KnowledgeSource::Evidenced => KnowledgeSourceKey::Evidenced,
                                KnowledgeSource::Told { by, via } => KnowledgeSourceKey::Told {
                                    by: Key::Existing(by),
                                    via: via.map(Key::Existing),
                                },
                                KnowledgeSource::Seen { by } => KnowledgeSourceKey::Seen {
                                    by: Key::Existing(by),
                                },
                            },
                        },
                )
            })
            .collect();
        // Presence is all a discharge asks, and a create cannot collide: the key is
        // command-derived and the index is unique within one patch.
        let commitments: BTreeSet<(Key<SubjectId>, CommitmentKey)> = state
            .commitments
            .iter()
            .flat_map(|(subject_id, held)| {
                held.keys()
                    .map(move |key| (Key::Existing(*subject_id), *key))
            })
            .collect();
        let pressures: BTreeMap<(Key<SubjectId>, PressureSourceKey), u32> = state
            .pressures
            .iter()
            .flat_map(|(target, held)| {
                held.iter().map(move |(source, magnitude)| {
                    (
                        (
                            Key::Existing(*target),
                            match source {
                                PressureSource::Commitment { subject, key } => {
                                    PressureSourceKey::Commitment {
                                        subject: Key::Existing(*subject),
                                        key: *key,
                                    }
                                }
                                PressureSource::Dependency(DependencyTarget::Resource(entity_id)) => {
                                    PressureSourceKey::Dependency(TargetKey::Resource(Key::Existing(
                                        *entity_id,
                                    )))
                                }
                                PressureSource::Dependency(DependencyTarget::Route(edge_id)) => {
                                    PressureSourceKey::Dependency(TargetKey::Route(Key::Existing(
                                        *edge_id,
                                    )))
                                }
                                PressureSource::Dependency(DependencyTarget::Subject(subject_id)) => {
                                    PressureSourceKey::Dependency(TargetKey::Subject(Key::Existing(
                                        *subject_id,
                                    )))
                                }
                                PressureSource::Subject(subject_id) => {
                                    PressureSourceKey::Subject(Key::Existing(*subject_id))
                                }
                            },
                        ),
                        magnitude.0,
                    )
                })
            })
            .collect();
        Self {
            containers,
            routes,
            positions,
            holdings,
            dependencies,
            authority,
            selection,
            redress,
            facts,
            retired,
            affordance_holdings,
            channels,
            knowledge,
            commitments,
            pressures,
        }
    }

    /// Whether `place` is `root`, or reaches it through the container chain.
    pub(super) fn covers(&self, root: &Key<EntityId>, place: &Key<EntityId>) -> bool {
        let mut current = Some(place);
        for _ in 0..=self.containers.len() {
            match current {
                None => return false,
                Some(node) if node == root => return true,
                Some(node) => current = self.containers.get(node),
            }
        }
        false
    }

    fn targets_overlap(&self, left: &AuthorityTargetKey, right: &AuthorityTargetKey) -> bool {
        match (left, right) {
            (AuthorityTargetKey::Subject(one), AuthorityTargetKey::Subject(other)) => one == other,
            (AuthorityTargetKey::PlaceSubtree(one), AuthorityTargetKey::PlaceSubtree(other)) => {
                self.covers(one, other) || self.covers(other, one)
            }
            _ => false,
        }
    }

    /// Every subject whose position the container chain puts under `place`.
    pub(super) fn occupants_under(&self, place: &Key<EntityId>) -> BTreeSet<Key<SubjectId>> {
        self.positions
            .iter()
            .filter(|(_, standing)| self.covers(place, standing))
            .map(|(subject, _)| subject.clone())
            .collect()
    }

    pub(super) fn offices_of(
        &self,
        institution: &Key<SubjectId>,
    ) -> BTreeMap<OfficeName, OfficeCandidate> {
        self.selection
            .iter()
            .filter(|((held_by, _), _)| held_by == institution)
            .map(|((_, name), office)| (name.clone(), office.clone()))
            .collect()
    }

    pub(super) fn offices_held_by(
        &self,
        holder: &Key<SubjectId>,
    ) -> BTreeMap<(Key<SubjectId>, OfficeName), OfficeCandidate> {
        self.selection
            .iter()
            .filter(|(_, office)| office.incumbent.as_ref() == Some(holder))
            .map(|(slot, office)| (slot.clone(), office.clone()))
            .collect()
    }

    /// Own grants plus what every held office lends, walking every office.
    pub(super) fn effective_authority(&self, holder: &Key<SubjectId>) -> BTreeSet<GrantKey> {
        let mut effective = self.authority.get(holder).cloned().unwrap_or_default();
        for ((institution, _), office) in &self.selection {
            if office.incumbent.as_ref() != Some(holder) || self.retired.contains(institution) {
                continue;
            }
            for grant in self.authority.get(institution).into_iter().flatten() {
                if office.delegated.contains(&grant.kind) {
                    effective.insert(grant.clone());
                }
            }
        }
        effective
    }

    /// Every holder in the graph with two grants of one kind over overlapping
    /// ground, from any combination of direct grant and office delegation.
    pub(super) fn overlapping_holders(&self) -> BTreeSet<Key<SubjectId>> {
        let holders: BTreeSet<&Key<SubjectId>> = self
            .authority
            .keys()
            .chain(self.selection.values().filter_map(|office| office.incumbent.as_ref()))
            .collect();
        holders
            .into_iter()
            .filter(|holder| {
                let effective: Vec<GrantKey> =
                    self.effective_authority(holder).into_iter().collect();
                effective.iter().enumerate().any(|(index, one)| {
                    effective[index + 1..].iter().any(|other| {
                        one.kind == other.kind && self.targets_overlap(&one.over, &other.over)
                    })
                })
            })
            .cloned()
            .collect()
    }

    /// What a resource's holders hold in all.
    pub(super) fn total(&self, resource: &Key<EntityId>) -> u128 {
        self.holdings
            .iter()
            .filter(|((_, held), _)| held == resource)
            .map(|(_, quantity)| u128::from(quantity.0))
            .sum()
    }
}

#[cfg(test)]
impl Candidate<'_> {
    /// Every candidate map, in full: the committed world with the patch's writes
    /// applied. The named reads must equal scans of it.
    pub(super) fn materialized(&self) -> Materialized {
        let mut all = Materialized::seeded(self.state);
        apply_written(&mut all.containers, self.containers.local());
        apply_written(&mut all.routes, self.routes.local());
        apply_written(&mut all.positions, self.positions.local());
        apply_written(&mut all.holdings, self.holdings.local());
        apply_written_set(&mut all.dependencies, self.dependencies.local());
        apply_written(&mut all.authority, self.authority.local());
        apply_written(&mut all.selection, self.selection.local());
        apply_written(&mut all.redress, self.redress.local());
        apply_written(&mut all.facts, self.facts.local());
        apply_written_set(&mut all.retired, self.retired.local());
        apply_written(&mut all.affordance_holdings, self.affordance_holdings.local());
        apply_written(&mut all.channels, self.channels.local());
        apply_written(&mut all.knowledge, self.knowledge.local());
        apply_written_set(&mut all.commitments, self.commitments.local());
        apply_written(&mut all.pressures, self.pressures.local());
        all
    }

    /// The holders the running judgement holds as overlapping.
    pub(super) fn overlapping_holders(&self) -> &BTreeSet<Key<SubjectId>> {
        &self.overlapping
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::patch::DraftHandle;
    use crate::tests::{
        BAILIFF_OFFICE, LEVY_KIND, WARDEN_OFFICE, auth_principal, authority_kind, civic_world,
        creation, office, operations, owner, reject_play,
    };
    use crate::{Mismatch, WorldKernel};

    fn civic_kernel(title: &str) -> (WorldKernel, crate::tests::Topology, crate::tests::Civic) {
        let (mut kernel, _) = WorldKernel::create(creation(title), &auth_principal(owner())).unwrap();
        let (topology, civic, _) = civic_world(&mut kernel);
        (kernel, topology, civic)
    }

    fn levy_over(place: Key<EntityId>) -> GrantKey {
        GrantKey { kind: authority_kind(LEVY_KIND), over: AuthorityTargetKey::PlaceSubtree(place) }
    }

    /// Every named read, asked of the overlay and of a scan of the materialized
    /// maps, must agree.
    fn agrees(
        graph: &Candidate<'_>,
        label: &str,
        places: &[Key<EntityId>],
        holders: &[Key<SubjectId>],
        resource: &Key<EntityId>,
    ) {
        let all = graph.materialized();
        for place in places {
            assert_eq!(graph.occupants_under(place), all.occupants_under(place), "{label}: under {place:?}");
        }
        for holder in holders {
            assert_eq!(graph.offices_of(holder), all.offices_of(holder), "{label}: offices of {holder:?}");
            assert_eq!(
                graph.offices_held_by(holder),
                all.offices_held_by(holder),
                "{label}: offices held by {holder:?}"
            );
            assert_eq!(
                graph.effective_authority(holder),
                all.effective_authority(holder),
                "{label}: authority of {holder:?}"
            );
        }
        let overlapping = all.overlapping_holders();
        assert_eq!(graph.overlapping_holders(), &overlapping, "{label}: who overlaps");
        assert_eq!(graph.overlapping(), !overlapping.is_empty(), "{label}: whether anyone overlaps");
        // The written holders' sums stand in for the totals: every other holder
        // is on both sides, so the two differ by the same amount.
        let (before, after) = graph.custody(resource);
        let seeded = Materialized::seeded(graph.state).total(resource);
        assert_eq!(
            after as i128 - before as i128,
            all.total(resource) as i128 - seeded as i128,
            "{label}: custody"
        );
        for kind in ["speak", "levy", "novel"] {
            let kind = AffordanceKindName(kind.into());
            assert_eq!(
                graph.kind_taken(&kind),
                crate::kind_taken_by_scan(graph.state, &kind) || graph.kinds.contains(&kind),
                "{label}: kind {kind:?}"
            );
        }
    }

    /// The patch walks the places the clockwork rules care about: a subject
    /// leaving and entering a subtree, a declared place and a declared subject
    /// in it, a retirement, a vacated and refilled office, a grant that
    /// overlaps what an office lends and its revocation, the retirement of the
    /// lending institution, a declared institution, and custody written both
    /// ways. After every step each named read equals a scan of the maps the
    /// patch would have copied.
    #[test]
    fn candidate_reads_equal_materialized_scans() {
        let (kernel, topology, civic) = civic_kernel("Candidate");
        let state = &kernel.state;
        let (hall, chamber, road) =
            (Key::Existing(civic.hall), Key::Existing(civic.chamber), Key::Existing(topology.road));
        let (treasury, reeve, farmer, outsider) = (
            Key::Existing(civic.treasury),
            Key::Existing(civic.reeve),
            Key::Existing(civic.farmer),
            Key::Existing(civic.outsider),
        );
        let grain = Key::Existing(civic.grain);
        let annex: Key<EntityId> = Key::Draft(DraftHandle::new("annex"));
        let scribe: Key<SubjectId> = Key::Draft(DraftHandle::new("scribe"));
        let ministry: Key<SubjectId> = Key::Draft(DraftHandle::new("ministry"));
        let places = [hall.clone(), chamber.clone(), road.clone(), annex.clone()];
        let holders =
            [treasury.clone(), reeve.clone(), farmer.clone(), outsider.clone(), scribe.clone(), ministry.clone()];
        let check = |graph: &Candidate<'_>, label: &str| agrees(graph, label, &places, &holders, &grain);

        let mut graph = Candidate::new(state);
        check(&graph, "untouched");
        assert!(!graph.overlapping() && graph.offices_held_by(&reeve).len() == 1);
        assert_eq!(
            graph.occupants_under(&hall).len(),
            3,
            "the treasury, the reeve and the farmer stand under the hall"
        );

        graph.positions.insert(farmer.clone(), road.clone());
        check(&graph, "a subject leaves the subtree");
        assert!(!graph.occupants_under(&hall).contains(&farmer));

        graph.positions.insert(outsider.clone(), chamber.clone());
        check(&graph, "a subject enters the subtree");
        assert!(graph.occupants_under(&hall).contains(&outsider));

        graph.containers.insert(annex.clone(), chamber.clone());
        graph.positions.insert(scribe.clone(), annex.clone());
        check(&graph, "a declared place holds a declared subject");
        assert!(graph.occupants_under(&hall).contains(&scribe));
        assert_eq!(graph.occupants_under(&annex), BTreeSet::from([scribe.clone()]));

        graph.retire(farmer.clone());
        graph.positions.remove(&farmer);
        check(&graph, "a subject is retired");
        assert!(!graph.occupants_under(&road).contains(&farmer));

        let warden = (treasury.clone(), office(WARDEN_OFFICE));
        let seat = graph.office(&warden).expect("the treasury opens a warden office");
        graph.set_office(warden.clone(), Some(OfficeCandidate { incumbent: None, ..seat.clone() }));
        check(&graph, "the warden office is vacated");
        assert!(graph.offices_held_by(&reeve).is_empty());
        graph.set_office(warden.clone(), Some(OfficeCandidate { incumbent: Some(outsider.clone()), ..seat }));
        check(&graph, "the warden office is refilled");
        assert_eq!(graph.offices_held_by(&outsider).len(), 1);
        assert_eq!(graph.offices_of(&treasury).len(), 2);

        // The outsider now holds levy over the hall through the office; a levy
        // over the chamber of their own overlaps it.
        graph.set_grants(outsider.clone(), BTreeSet::from([levy_over(chamber.clone())]));
        check(&graph, "an overlapping grant");
        assert_eq!(graph.overlapping_holders(), &BTreeSet::from([outsider.clone()]));
        graph.set_grants(outsider.clone(), BTreeSet::new());
        check(&graph, "the grant is revoked");
        assert!(!graph.overlapping());
        graph.set_grants(outsider.clone(), BTreeSet::from([levy_over(chamber.clone())]));
        assert!(graph.overlapping());
        graph.retire(treasury.clone());
        check(&graph, "the lending institution is retired");
        assert!(!graph.overlapping(), "a retired institution lends nothing");

        // A declared institution lends a declared place to a declared subject.
        graph.set_grants(ministry.clone(), BTreeSet::from([levy_over(annex.clone())]));
        let seat = (ministry.clone(), office("reeve"));
        graph.set_office(
            seat.clone(),
            Some(OfficeCandidate {
                incumbent: Some(scribe.clone()),
                delegated: BTreeSet::from([authority_kind(LEVY_KIND)]),
            }),
        );
        check(&graph, "a declared institution seats a declared subject");
        assert_eq!(graph.effective_authority(&scribe), BTreeSet::from([levy_over(annex.clone())]));
        graph.set_grants(scribe.clone(), BTreeSet::from([levy_over(chamber.clone())]));
        check(&graph, "the declared subject's own grant covers the lent place");
        assert_eq!(graph.overlapping_holders(), &BTreeSet::from([scribe.clone()]));
        graph.set_office(seat, None);
        check(&graph, "the office is closed");
        assert!(!graph.overlapping());

        // Custody, written both ways: the farmer's grain goes, the scribe's comes.
        graph.holdings.insert((farmer.clone(), grain.clone()), Quantity(2));
        graph.holdings.insert((scribe.clone(), grain.clone()), Quantity(5));
        graph.holdings.remove(&(outsider.clone(), grain.clone()));
        check(&graph, "custody is rewritten");
        assert_eq!(graph.custody(&grain), (14, 7), "two of seven, five of none, and none of seven");

        assert!(!graph.declare_kind(AffordanceKindName("novel".into())));
        assert!(graph.declare_kind(AffordanceKindName("novel".into())), "a kind is named once");
        assert!(graph.declare_kind(AffordanceKindName("speak".into())), "the catalog names speak");
        check(&graph, "kinds");
    }

    /// An overlay nobody has written reads exactly the partition the old seeding
    /// copied, key for key, in every world that holds a document in some
    /// partition; and a draft key, or an id nothing holds, reads as absent.
    #[test]
    fn an_untouched_overlay_reads_what_the_seeding_copied() {
        let worlds = crate::golden_tests::populated_worlds();
        for state in &worlds {
            let graph = Candidate::new(state);
            let all = Materialized::seeded(state);
            macro_rules! same {
                ($($map:ident),*) => {$(
                    for (key, value) in &all.$map {
                        assert_eq!(
                            format!("{:?}", graph.$map.get(key)),
                            format!("{:?}", Some(value)),
                            stringify!($map)
                        );
                    }
                )*};
            }
            macro_rules! same_set {
                ($($map:ident),*) => {$(
                    for key in &all.$map {
                        assert!(graph.$map.contains_key(key), stringify!($map));
                    }
                )*};
            }
            same!(containers, routes, positions, holdings, redress, facts, affordance_holdings, channels, knowledge, pressures);
            same_set!(dependencies, commitments);
            for (key, value) in &all.authority {
                assert_eq!(&graph.grants_of(key), value);
            }
            for (slot, value) in &all.selection {
                assert_eq!(graph.office(slot).as_ref(), Some(value));
            }
            for key in &all.retired {
                assert!(graph.is_retired(key));
            }
            let nobody: Key<SubjectId> = Key::Existing(SubjectId::issue());
            let nowhere: Key<EntityId> = Key::Existing(EntityId::issue());
            let drafted: Key<SubjectId> = Key::Draft(DraftHandle::new("drafted"));
            assert!(graph.positions.get(&nobody).is_none() && graph.positions.get(&drafted).is_none());
            assert!(graph.containers.get(&nowhere).is_none());
            assert!(graph.grants_of(&nobody).is_empty() && !graph.is_retired(&drafted));
            assert!(graph.office(&(nobody, office(WARDEN_OFFICE))).is_none());
            assert!(graph.occupants_under(&nowhere).is_empty());
            assert_eq!(graph.effective_authority(&drafted), BTreeSet::new());
        }
    }

    /// The first overlap of a patch is named at the operation that made it, and
    /// every later widening operation is named too while it stands; a revoke
    /// that clears it makes the later widening operation clean.
    #[test]
    fn overlap_mismatches_are_unchanged() {
        let (mut kernel, _, civic) = civic_kernel("Overlaps");
        let snapshot = kernel.snapshot().unwrap();
        let grant = |holder: SubjectId| crate::ComponentOp::GrantAuthority {
            holder: crate::Ref::Existing(holder),
            grant: crate::AuthorityGrantRef {
                kind: authority_kind(LEVY_KIND),
                over: crate::AuthorityTargetRef::PlaceSubtree(crate::Ref::Existing(civic.chamber)),
            },
        };
        let revoke = |holder: SubjectId| match grant(holder) {
            crate::ComponentOp::GrantAuthority { holder, grant } => {
                crate::ComponentOp::RevokeAuthority { holder, grant }
            }
            other => other,
        };
        let seat = crate::ComponentOp::InstallIncumbent {
            institution: crate::Ref::Existing(civic.treasury),
            office: office(BAILIFF_OFFICE),
            incumbent: crate::Ref::Existing(civic.farmer),
        };
        let at = |operation: usize| Mismatch::OverlappingJurisdiction { operation };

        // The reeve holds the warden's levy over the hall, so a levy over the
        // chamber of the reeve's own overlaps it, and the seating that follows
        // still sees an overlap standing.
        let both = reject_play(
            &mut kernel,
            &snapshot,
            operations(vec![grant(civic.reeve), seat.clone()]),
        );
        assert_eq!(both, vec![at(0), at(1)]);

        let cleared = reject_play(
            &mut kernel,
            &snapshot,
            operations(vec![grant(civic.reeve), revoke(civic.reeve), seat]),
        );
        assert_eq!(cleared, vec![at(0)]);
    }
}
