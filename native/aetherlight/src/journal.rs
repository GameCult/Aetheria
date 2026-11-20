use super::{
    COMMIT_SCHEMA, CommittedCommand, KernelError, WorldCommit, WorldEffect,
    WorldId, WorldState, apply_effect, commit_digest, reduce,
};
use crate::docs::Changed;
use serde::{Deserialize, Serialize, de::DeserializeOwned};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

pub(super) const JOURNAL_SCHEMA: &str = "aetherlight.journal.v1";

#[derive(Debug, Error)]
pub(super) enum JournalError {
    #[error("world journal is corrupt: {0}")]
    Corrupt(String),
    /// A well-formed commit of another schema generation: not damage, a world
    /// this build cannot read. Named apart so a store can tell an old save from
    /// a corrupt one.
    #[error("commit schema {found} is not the supported {supported}")]
    UnsupportedSchema { found: String, supported: &'static str },
}

/// The one judge of the schema generation, asked of every commit before any of
/// its other fields is believed.
fn require_commit_schema(commit: &WorldCommit) -> Result<(), JournalError> {
    if commit.schema == COMMIT_SCHEMA {
        return Ok(());
    }
    Err(JournalError::UnsupportedSchema {
        found: commit.schema.clone(),
        supported: COMMIT_SCHEMA,
    })
}

/// What an export carries: the schema that names the format, and the commits in
/// revision order. Nothing else is stored, because nothing else is truth: the
/// head state is the replay of these commits.
#[derive(Serialize)]
struct ExportRef<'a> {
    schema: &'a str,
    commits: &'a [WorldCommit],
}

#[derive(Serialize, Deserialize)]
struct Export {
    schema: String,
    commits: Vec<WorldCommit>,
}

/// The only record of a world: its commits in revision order, genesis first.
pub(super) struct WorldJournal {
    commits: Vec<WorldCommit>,
}

impl WorldJournal {
    pub(super) fn genesis(genesis: WorldCommit) -> Self {
        Self {
            commits: vec![genesis],
        }
    }

    pub(super) fn append(&mut self, commit: WorldCommit) {
        self.commits.push(commit);
    }

    /// Every decision the world has acted on, oldest first: the one place an
    /// event lives, in the commit that applied it.
    pub(super) fn acted_events(&self) -> impl Iterator<Item = &super::DecisionEvent> {
        self.commits.iter().filter_map(|commit| match &commit.effect {
            WorldEffect::Acted { event, .. } => Some(event),
            _ => None,
        })
    }

    pub(super) fn export(&self) -> Result<Vec<u8>, KernelError> {
        rmp_serde::to_vec_named(&ExportRef {
            schema: JOURNAL_SCHEMA,
            commits: &self.commits,
        })
        .map_err(|error| KernelError::Serialization(error.to_string()))
    }

    /// Decode canonically, then replay: the head state is whatever the chain
    /// derives, and a chain that does not verify is not a world.
    ///
    /// A chain verifies as a prefix, so an export with its tail dropped, or an
    /// older export, opens as a valid shorter world. The holder of the bytes
    /// owns the head: it keeps the revision and state digest from `snapshot()`
    /// at export time and compares them after `open` (AL5's C# store).
    pub(super) fn open(bytes: &[u8]) -> Result<(Self, WorldState), JournalError> {
        let export: Export = decode(bytes)?;
        if export.schema != JOURNAL_SCHEMA {
            return Err(JournalError::Corrupt(format!(
                "export schema {} is not {JOURNAL_SCHEMA}",
                export.schema
            )));
        }
        let state = verify_history(&export.commits)?;
        Ok((
            Self {
                commits: export.commits,
            },
            state,
        ))
    }

    #[cfg(test)]
    pub(super) fn commit_count(&self) -> usize {
        self.commits.len()
    }

    #[cfg(test)]
    pub(super) fn commits_for_test(&self) -> Vec<WorldCommit> {
        self.commits.clone()
    }
}

/// The one judge of a stored chain, `pub(super)` so a forgery test outside this
/// module routes through the same function that `open` does. It replays the
/// commits in revision order from genesis and returns the head state they
/// derive; position `i` must hold resulting revision `i`.
pub(super) fn verify_history(commits: &[WorldCommit]) -> Result<WorldState, JournalError> {
    let genesis = commits
        .first()
        .ok_or_else(|| JournalError::Corrupt("world has no genesis commit".into()))?;
    require_commit_schema(genesis)?;
    let CommittedCommand::CreateWorld(genesis_command) = &genesis.command else {
        return Err(JournalError::Corrupt(
            "first commit does not contain the immutable creation command".into(),
        ));
    };
    let WorldEffect::WorldCreated { .. } = &genesis.effect else {
        return Err(JournalError::Corrupt(
            "first commit is not immutable world genesis".into(),
        ));
    };
    if genesis.previous_revision.is_some()
        || genesis.resulting_revision != 0
        || genesis.world_id != WorldId::of(genesis_command.id())
        || genesis.previous_state_digest.is_some()
        || genesis.previous_commit_digest.is_some()
        || commit_digest(genesis).map_err(kernel_error)? != genesis.digest
    {
        return Err(JournalError::Corrupt(
            "genesis commit is not canonical or verifiable".into(),
        ));
    }
    let mut replay = WorldState::genesis(genesis.world_id, genesis_command, &genesis.effect)
        .map_err(kernel_error)?;
    verify_state_shape(&replay)?;
    let born: Vec<_> = replay.commitments.keys().map(|subject| (*subject, None)).collect();
    verify_commitments_are_future(&replay, &born)?;
    if replay.state_digest != genesis.resulting_state_digest {
        return Err(JournalError::Corrupt(
            "genesis resulting state digest is invalid".into(),
        ));
    }
    replay.last_commit_digest = Some(genesis.digest.clone());

    for commit in &commits[1..] {
        require_commit_schema(commit)?;
        if commit.world_id != replay.header.world_id
            || commit.previous_revision != Some(replay.revision)
            || commit.resulting_revision != replay.revision.saturating_add(1)
            || commit.previous_state_digest.as_deref() != Some(replay.state_digest.as_str())
            || commit.previous_commit_digest != replay.last_commit_digest
            || commit_digest(commit).map_err(kernel_error)? != commit.digest
        {
            return Err(JournalError::Corrupt(
                "commit chain is not contiguous or verifiable".into(),
            ));
        }
        // Authority is re-decided here for free: `apply_committed_command`
        // re-runs `reduce` against the pre-commit state and requires effect
        // equality, then `apply_effect`, and both of those call
        // `require_play`. A forged commit recording the owner as the caller of
        // a post-genesis command fails replay rather than being admitted by a
        // reducer that trusts the recorded caller. A third statement of the same rule here would be a
        // third owner of one decision.
        replay.begin();
        apply_committed_command(&mut replay, commit)?;
        // The clock needs no chain check of its own: only `TimeAdvanced` writes
        // it, `apply_effect` re-derives `to` from a `TickMinutes` that cannot
        // be read outside `1..=MAX_ROUTE_COST`, so every tick moves it forward
        // and nothing else moves it.
        replay.revision = commit.resulting_revision;
        let accumulator = replay.folded().map_err(kernel_error)?;
        replay.state_digest = accumulator.state_digest(replay.revision);
        if replay.state_digest != commit.resulting_state_digest {
            return Err(JournalError::Corrupt(
                "commit resulting state digest is invalid".into(),
            ));
        }
        let touched = replay.finish(accumulator);
        verify_commitments_are_future(&replay, &touched)?;
        replay.last_commit_digest = Some(commit.digest.clone());
    }
    verify_state_shape(&replay)?;
    Ok(replay)
}

/// The replay-side twin of `Mismatch::CommitmentDueInThePast`: every commitment
/// key this commit introduced is due after the post-commit clock. Replay already
/// reconstructs both sides, and the transaction's change set names the only subjects
/// that can hold a new key, with what each held before.
fn verify_commitments_are_future(
    state: &WorldState,
    touched: &[Changed<super::SubjectId, BTreeMap<super::CommitmentKey, super::Commitment>>],
) -> Result<(), JournalError> {
    for (subject_id, before) in touched {
        let Some(held) = state.commitments.get(subject_id) else {
            continue;
        };
        for (key, commitment) in held {
            let existed = before
                .as_ref()
                .is_some_and(|previous| previous.contains_key(key));
            if !existed && commitment.due <= state.header.now {
                return Err(JournalError::Corrupt(
                    "a commitment was born at or after its own due date".into(),
                ));
            }
        }
    }
    Ok(())
}

pub(super) fn verify_state_shape(state: &WorldState) -> Result<(), JournalError> {
    if state.header.title.trim().is_empty() || state.header.title.trim() != state.header.title {
        return Err(JournalError::Corrupt(
            "world title is empty or noncanonical".into(),
        ));
    }
    super::validate_principal(&state.header.owner).map_err(kernel_error)?;
    if state.subjects.is_empty() {
        return Err(JournalError::Corrupt("world ontology is empty".into()));
    }
    let is_place = |entity_id: &super::EntityId| {
        state
            .entities
            .get(entity_id)
            .is_some_and(|record| record.kind == super::EntityKind::Place)
    };
    for (entity_id, entity) in &state.entities {
        if !super::patch::is_canonical_text(&entity.label) {
            return Err(JournalError::Corrupt(
                "entity label is empty or noncanonical".into(),
            ));
        }
        if let Some(container) = entity.container
            && (entity.kind != super::EntityKind::Place || !is_place(&container))
        {
            return Err(JournalError::Corrupt(
                "entity container does not name a canonical place".into(),
            ));
        }
        if !super::patch::containment_terminates(*entity_id, &state.entities) {
            return Err(JournalError::Corrupt("place contains itself".into()));
        }
    }
    for edge in state.edges.values() {
        let (from, to) = edge.endpoints();
        if !is_place(&from)
            || !is_place(&to)
            || from == to
            || !super::patch::is_valid_cost(edge.cost())
        {
            return Err(JournalError::Corrupt(
                "route does not join two distinct places at a valid cost".into(),
            ));
        }
    }
    for (subject_id, position) in &state.positions {
        if !state.subjects.contains_key(subject_id) || !is_place(&position.place) {
            return Err(JournalError::Corrupt(
                "position does not name a canonical subject and place".into(),
            ));
        }
    }
    let is_resource = |entity_id: &super::EntityId| {
        state
            .entities
            .get(entity_id)
            .is_some_and(|record| record.kind == super::EntityKind::Resource)
    };
    // Absence is zero and one slot holds one pair, so a store satisfying these
    // clauses cannot carry a zero, an empty holder, a duplicate, or a dangling
    // holding — with no scan for any of them.
    for (subject_id, held) in &state.holdings {
        if !state.subjects.contains_key(subject_id)
            || held.is_empty()
            || held
                .iter()
                .any(|(resource, quantity)| !is_resource(resource) || quantity.0 == 0)
        {
            return Err(JournalError::Corrupt(
                "holding does not name a canonical subject and resource at a nonzero quantity"
                    .into(),
            ));
        }
    }
    for (subject_id, targets) in &state.dependencies {
        let target_is_canonical = |target: &super::DependencyTarget| match target {
            super::DependencyTarget::Resource(entity_id) => is_resource(entity_id),
            super::DependencyTarget::Route(edge_id) => state.edges.contains_key(edge_id),
            super::DependencyTarget::Subject(other) => {
                other != subject_id && state.subjects.contains_key(other)
            }
        };
        if !state.subjects.contains_key(subject_id)
            || targets.is_empty()
            || !targets.iter().all(target_is_canonical)
        {
            return Err(JournalError::Corrupt(
                "dependency does not name a canonical subject and a distinct canonical target"
                    .into(),
            ));
        }
    }
    // Facts, channels, and knowledge. The `facts`/`channels` bijections run both
    // ways: a payload row without its entity, or an entity of that kind without
    // its payload, is a referenceable half-thing.
    for (entity_id, record) in &state.facts {
        let standing_is_live = match &record.standing {
            super::FactStanding::Canonical { evidence } => {
                super::patch::is_canonical_text(evidence.text())
            }
            super::FactStanding::Claimed { by } => state.subjects.contains_key(by),
            super::FactStanding::Ruled => true,
        };
        if !state
            .entities
            .get(entity_id)
            .is_some_and(|entity| entity.kind == super::EntityKind::Fact)
            || !super::patch::is_canonical_text(record.statement.as_str())
            || !standing_is_live
        {
            return Err(JournalError::Corrupt(
                "a fact names no canonical entity, statement, or standing".into(),
            ));
        }
    }
    for (entity_id, record) in &state.channels {
        let reach_is_live = match &record.reach {
            super::Reach::Subjects(members) => members
                .iter()
                .all(|subject_id| state.subjects.contains_key(subject_id)),
            super::Reach::Place(place) => is_place(place),
        };
        if !state
            .entities
            .get(entity_id)
            .is_some_and(|entity| entity.kind == super::EntityKind::Channel)
            || !reach_is_live
            || !record
                .controller
                .is_none_or(|subject_id| state.subjects.contains_key(&subject_id))
        {
            return Err(JournalError::Corrupt(
                "a channel names no canonical entity, reach, or controller".into(),
            ));
        }
    }
    for (entity_id, entity) in &state.entities {
        let paired = match entity.kind {
            super::EntityKind::Fact => state.facts.contains_key(entity_id),
            super::EntityKind::Channel => state.channels.contains_key(entity_id),
            super::EntityKind::Place | super::EntityKind::Resource => true,
        };
        if !paired {
            return Err(JournalError::Corrupt(
                "a fact or channel entity carries no payload row".into(),
            ));
        }
    }
    for ((subject_id, fact), entry) in &state.knowledge {
        let entry_is_live = |entry: &super::Knowledge| {
            entry.acquired_at <= state.revision
                && match entry.source {
                    super::KnowledgeSource::Told { by, via } => {
                        state.subjects.contains_key(&by)
                            && via.is_none_or(|channel| state.channels.contains_key(&channel))
                    }
                    super::KnowledgeSource::Seen { by } => state.subjects.contains_key(&by),
                    super::KnowledgeSource::Witnessed | super::KnowledgeSource::Evidenced => true,
                }
        };
        if !state.subjects.contains_key(subject_id)
            || !state.facts.contains_key(fact)
            || !entry_is_live(entry)
        {
            return Err(JournalError::Corrupt(
                "knowledge does not name a canonical subject, fact, and source, or was acquired ahead of the world's revision"
                    .into(),
            ));
        }
    }
    // Commitments, pressures, and the authored scale target.
    // Absence is the only spelling of nothing at every level, so a store
    // satisfying these clauses carries no empty inner map, no zero magnitude,
    // and no dangling row — with no scan for any of them.
    for (subject_id, held) in &state.commitments {
        let commitment_is_canonical = |commitment: &super::Commitment| {
            (commitment.kind == super::CommitmentKind::Routine) == commitment.period.is_some()
                && (commitment.kind == super::CommitmentKind::Routine
                    || commitment.checks.is_empty())
                && match commitment.counterparty {
                    Some(counterparty) => {
                        counterparty != *subject_id
                            && state.subjects.contains_key(&counterparty)
                            && commitment.kind != super::CommitmentKind::Goal
                    }
                    None => true,
                }
                && commitment
                    .checks
                    .iter()
                    .all(|check| super::bound_precondition_is_live(state, check))
        };
        if !state.subjects.contains_key(subject_id)
            || held.is_empty()
            || !held.values().all(commitment_is_canonical)
        {
            return Err(JournalError::Corrupt(
                "a commitment does not name a canonical promisor, counterparty, shape, or check"
                    .into(),
            ));
        }
    }
    for (target, held) in &state.pressures {
        let source_is_live = |source: &super::PressureSource| match source {
            super::PressureSource::Commitment { subject, key } => state
                .commitments
                .get(subject)
                .is_some_and(|commitments| commitments.contains_key(key)),
            super::PressureSource::Dependency(dependency) => state
                .dependencies
                .get(target)
                .is_some_and(|targets| targets.contains(dependency)),
            super::PressureSource::Subject(subject_id) => state.subjects.contains_key(subject_id),
        };
        if !state.subjects.contains_key(target)
            || held.is_empty()
            || held
                .iter()
                .any(|(source, magnitude)| magnitude.0 == 0 || !source_is_live(source))
        {
            return Err(JournalError::Corrupt(
                "a pressure row does not name a canonical target, live source, and nonzero \
                 magnitude"
                    .into(),
            ));
        }
    }
    // The civic subgraph, in the slot the deleted `authority_scope` check
    // occupied: a jurisdiction names live ground under a canonical kind, an
    // office sits on an institution and lends something to a person who holds
    // no other office there, and a forum names a live subject.
    let target_is_canonical = |target: &super::AuthorityTarget| match target {
        super::AuthorityTarget::Subject(subject_id) => state.subjects.contains_key(subject_id),
        super::AuthorityTarget::PlaceSubtree(entity_id) => is_place(entity_id),
    };
    for (subject_id, grants) in &state.authority {
        if !state.subjects.contains_key(subject_id)
            || grants.is_empty()
            || !grants.iter().all(|grant| {
                super::patch::is_civic_name(&grant.kind.0) && target_is_canonical(&grant.over)
            })
        {
            return Err(JournalError::Corrupt(
                "authority does not name a canonical subject and live ground under a canonical kind"
                    .into(),
            ));
        }
    }
    for (institution, offices) in &state.selection {
        let mut incumbents = BTreeSet::new();
        if state.subjects.get(institution).map(|subject| subject.kind)
            != Some(super::SubjectKind::Institution)
            || offices.is_empty()
        {
            return Err(JournalError::Corrupt(
                "an office register does not name a canonical institution".into(),
            ));
        }
        for (name, office) in offices {
            let incumbent_is_person = office.incumbent.is_none_or(|incumbent| {
                state.subjects.get(&incumbent).map(|subject| subject.kind)
                    == Some(super::SubjectKind::Person)
                    && incumbents.insert(incumbent)
            });
            if !super::patch::is_civic_name(&name.0)
                || office.delegated.is_empty()
                || !office
                    .delegated
                    .iter()
                    .all(|kind| super::patch::is_civic_name(&kind.0))
                || !incumbent_is_person
            {
                return Err(JournalError::Corrupt(
                    "an office lends nothing, is misnamed, or is held twice or by a non-person"
                        .into(),
                ));
            }
        }
    }
    for (grievance, forum) in &state.redress {
        if !super::patch::is_civic_name(&grievance.0)
            || !state.subjects.contains_key(&forum.forum)
            || !target_is_canonical(&forum.standing)
        {
            return Err(JournalError::Corrupt(
                "a forum does not name a canonical grievance, subject, and standing".into(),
            ));
        }
    }
    if super::overlapping_holder(state).is_some() {
        return Err(JournalError::Corrupt(
            "one subject holds two overlapping jurisdictions of one kind".into(),
        ));
    }
    let mut declared_keys = BTreeSet::new();
    let no_held = BTreeSet::new();
    for subject in state.subjects.values() {
        if let Some(key) = &subject.import_key
            && super::patch::import_key_fault(key, &no_held, &mut declared_keys).is_some()
        {
            return Err(JournalError::Corrupt(
                "subject import key is malformed or held by two subjects".into(),
            ));
        }
    }
    for subject in state.subjects.values() {
        if !super::patch::is_canonical_text(&subject.label) {
            return Err(JournalError::Corrupt(
                "subject label is empty or noncanonical".into(),
            ));
        }
    }
    // A grant row is a verb list: non-empty, for a live subject, catalog
    // entries only. A subject holding no verb holds no row.
    for (subject_id, granted) in &state.affordance_grants {
        if !state.subjects.contains_key(subject_id)
            || super::is_retired(state, *subject_id)
            || granted.is_empty()
            || granted
                .iter()
                .any(|affordance_id| !state.affordance_catalog.contains_key(affordance_id))
        {
            return Err(JournalError::Corrupt(
                "affordance grant is empty, unscoped, retired, or names no catalog entry".into(),
            ));
        }
    }
    // Every stored entry still passes the declaration validator, so a forged
    // catalog row cannot install an entry the resolver would have refused.
    for entry in state.affordance_catalog.values() {
        if !super::patch::entry_is_admissible(entry) {
            return Err(JournalError::Corrupt(
                "stored affordance entry is not admissible".into(),
            ));
        }
    }
    Ok(())
}

fn apply_committed_command(
    state: &mut WorldState,
    commit: &WorldCommit,
) -> Result<(), JournalError> {
    let CommittedCommand::WorldCommand(command) = &commit.command else {
        return Err(JournalError::Corrupt(
            "creation command appears after world genesis".into(),
        ));
    };
    if command.world_id != state.header.world_id || command.expected_revision != state.revision {
        return Err(JournalError::Corrupt(
            "committed command does not target the exact replay state".into(),
        ));
    }
    let expected_effect = reduce(state, command).map_err(kernel_error)?;
    if expected_effect != commit.effect {
        return Err(JournalError::Corrupt(
            "committed effect is not the deterministic reduction of its command".into(),
        ));
    }
    apply_effect(
        state,
        command.id(),
        &commit.command.caller(),
        &expected_effect,
    )
    .map_err(kernel_error)
}

fn decode<T: DeserializeOwned + Serialize>(bytes: &[u8]) -> Result<T, JournalError> {
    let value: T = rmp_serde::from_slice(bytes)
        .map_err(|error| JournalError::Corrupt(format!("could not decode the export: {error}")))?;
    let canonical = rmp_serde::to_vec_named(&value)
        .map_err(|error| JournalError::Corrupt(error.to_string()))?;
    if canonical != bytes {
        return Err(JournalError::Corrupt("the export is not canonical".into()));
    }
    Ok(value)
}

fn kernel_error(error: KernelError) -> JournalError {
    JournalError::Corrupt(error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::patch::kernel_speak_grant;
    use crate::tests::speak_entry;
    use crate::{
        AuthenticatedCaller, CallerId, CommandBody, CommandEnvelope, CreateWorld, Declaration,
        DraftHandle, EntityDeclaration, EntityId, EntityKind, Position, PrincipalId,
        Ref, SubjectDeclaration, SubjectKind, WorldKernel, WorldPatch,
    };

    #[derive(serde::Serialize, serde::Deserialize)]
    struct CanonicalFixture {
        value: u8,
    }

    /// The bytes an honest exporter writes for `commits`, whatever they claim.
    pub(super) fn export_of(commits: &[WorldCommit]) -> Vec<u8> {
        WorldJournal {
            commits: commits.to_vec(),
        }
        .export()
        .unwrap()
    }

    /// Open a world from forged commits through the door a real export takes:
    /// canonical decode, then the one replay judge.
    pub(super) fn reopen(commits: &[WorldCommit]) -> Result<WorldState, JournalError> {
        WorldJournal::open(&export_of(commits)).map(|(_, state)| state)
    }

    #[test]
    fn decode_rejects_an_alternate_messagepack_shape() {
        let bytes = rmp_serde::to_vec(&CanonicalFixture { value: 7 }).unwrap();
        assert!(decode::<CanonicalFixture>(&bytes).is_err());
        let canonical = rmp_serde::to_vec_named(&CanonicalFixture { value: 7 }).unwrap();
        assert!(decode::<CanonicalFixture>(&canonical).is_ok());
    }

    /// The commit a forgery test rewrites: the one the test just committed.
    pub(super) fn last_commit(commits: &mut [WorldCommit]) -> &mut WorldCommit {
        commits.iter_mut().max_by_key(|commit| commit.resulting_revision).unwrap()
    }

    pub(super) fn genesis_commit(commits: &mut [WorldCommit]) -> &mut WorldCommit {
        commits.iter_mut().min_by_key(|commit| commit.resulting_revision).unwrap()
    }

    fn owner_patch(subject: SubjectDeclaration) -> WorldPatch {
        WorldPatch {
            declarations: vec![Declaration::Subject(subject)],
            operations: Vec::new(),
            evidence: Vec::new(),
        }
    }

    #[test]
    fn replay_rejects_a_forged_command_or_effect_even_with_valid_hashes() {
        let owner = PrincipalId::new("owner@example.test");
        let authenticated = AuthenticatedCaller::fixture(CallerId::Principal(owner.clone()));
        let creation = CreateWorld {
            owner: owner.clone(),
            title: "Before".into(),
            patch: owner_patch(SubjectDeclaration {
                handle: DraftHandle::new("owner"),
                label: "Owner".into(),
                kind: SubjectKind::Person,
                affordances: kernel_speak_grant(),
                position: None,
                import_key: None,
            }),
            grammar: None,
        };
        let (mut kernel, _) = WorldKernel::create(creation, &authenticated).unwrap();
        let snapshot = kernel.snapshot().unwrap();
        kernel
            .submit(
                CommandEnvelope {
                    world_id: snapshot.world_id,
                    expected_revision: snapshot.revision,
                    caller: CallerId::Play,
                    body: CommandBody::AdvanceTime {
                        minutes: crate::TickMinutes::new(1).unwrap(),
                    },
                },
                &AuthenticatedCaller::verified_play(),
            )
            .unwrap();

        let mut command_forged_commits = kernel.journal.commits.clone();
        let command_forged = last_commit(&mut command_forged_commits);
        let CommittedCommand::WorldCommand(command) = &mut command_forged.command else {
            panic!("expected a world command");
        };
        command.body = CommandBody::AdvanceTime {
            minutes: crate::TickMinutes::new(2).unwrap(),
        };
        command_forged.digest = commit_digest(command_forged).unwrap();

        assert!(matches!(
            reopen(&command_forged_commits),
            Err(JournalError::Corrupt(_))
        ));

        let mut caller_forged_commits = kernel.journal.commits.clone();
        let caller_forged = last_commit(&mut caller_forged_commits);
        let CommittedCommand::WorldCommand(command) = &mut caller_forged.command else {
            panic!("expected a world command");
        };
        command.caller = CallerId::Principal(PrincipalId::new("attacker@example.test"));
        caller_forged.digest = commit_digest(caller_forged).unwrap();

        assert!(matches!(
            reopen(&caller_forged_commits),
            Err(JournalError::Corrupt(_))
        ));

        let mut effect_forged_commits = kernel.journal.commits.clone();
        let effect_forged = last_commit(&mut effect_forged_commits);
        let WorldEffect::TimeAdvanced { to, .. } = &mut effect_forged.effect else {
            panic!("expected a time effect");
        };
        *to = crate::FictionalMinutes(99);
        effect_forged.digest = commit_digest(effect_forged).unwrap();

        assert!(matches!(
            reopen(&effect_forged_commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    #[test]
    fn a_forged_patch_effect_does_not_apply() {
        let owner = PrincipalId::new("owner@example.test");
        let authenticated = AuthenticatedCaller::fixture(CallerId::Principal(owner.clone()));
        let creation = CreateWorld {
            owner: owner.clone(),
            title: "Kharad".into(),
            patch: owner_patch(SubjectDeclaration {
                handle: DraftHandle::new("owner"),
                label: "Owner".into(),
                kind: SubjectKind::Person,
                affordances: kernel_speak_grant(),
                position: None,
                import_key: None,
            }),
            grammar: None,
        };
        let (mut kernel, _) = WorldKernel::create(creation, &authenticated).unwrap();
        let snapshot = kernel.snapshot().unwrap();
        kernel
            .submit(
                CommandEnvelope {
                    world_id: snapshot.world_id,
                    expected_revision: snapshot.revision,
                    caller: CallerId::Play,
                    body: CommandBody::AdmitPatch {
                        patch: WorldPatch {
                            declarations: vec![
                                Declaration::Entity(EntityDeclaration {
                                    handle: DraftHandle::new("rhythm-road"),
                                    label: "The Rhythm Road".into(),
                                    kind: EntityKind::Place,
                                    container: None,
                                }),
                                Declaration::Subject(SubjectDeclaration {
                                    handle: DraftHandle::new("rhythm-authority"),
                                    label: "The Rhythm Authority".into(),
                                    kind: SubjectKind::Institution,
                                    affordances: BTreeSet::from([speak_entry(&kernel)]),
                                    position: Some(Ref::Draft(DraftHandle::new("rhythm-road"))),
                                    import_key: None,
                                }),
                            ],
                            operations: Vec::new(),
                            evidence: Vec::new(),
                        },
                    },
                },
                &AuthenticatedCaller::verified_play(),
            )
            .unwrap();

        let mut forged_commits = kernel.journal.commits.clone();
        let forged = last_commit(&mut forged_commits);
        let WorldEffect::PatchAdmitted { resolved, .. } = &mut forged.effect else {
            panic!("expected an admitted patch effect");
        };
        resolved.subjects[0].position = Some(Position {
            place: EntityId::issue(),
        });
        forged.digest = commit_digest(forged).unwrap();

        assert!(matches!(
            reopen(&forged_commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// A world whose history includes a patch and a clock advance, both Play's,
    /// reopened from its own export.
    fn clock_history() -> WorldKernel {
        use crate::tests::{auth_principal, creation, owner, submit_play};
        use crate::TickMinutes;

        let (mut kernel, _) = WorldKernel::create(creation("Clock History"),
            &auth_principal(owner()),
        )
        .unwrap();
        let snapshot = kernel.snapshot().unwrap();
        submit_play(
            &mut kernel,
            &snapshot,
            CommandBody::AdmitPatch {
                patch: crate::WorldPatch {
                    declarations: vec![crate::patch::Declaration::Entity(
                        crate::patch::EntityDeclaration {
                            handle: crate::patch::DraftHandle::new("annex"),
                            label: "The Annex".into(),
                            kind: crate::patch::EntityKind::Place,
                            container: None,
                        },
                    )],
                    operations: Vec::new(),
                    evidence: Vec::new(),
                },
            },
        );
        let snapshot = kernel.snapshot().unwrap();
        submit_play(
            &mut kernel,
            &snapshot,
            CommandBody::AdvanceTime {
                minutes: TickMinutes::new(30).unwrap(),
            },
        );
        kernel
    }

    /// Replay re-derives Play's patch and clock advance: the honest history
    /// reopens to the same state digest and reading, and the exported bytes do too.
    #[test]
    fn a_history_with_a_patch_and_a_clock_advance_replays_exactly() {
        let kernel = clock_history();
        let replayed = reopen(&kernel.journal.commits).expect("the honest history replays");
        assert_eq!(replayed.state_digest, kernel.state.state_digest);
        assert_eq!(replayed.header.now, kernel.state.header.now);

        let (digest, now) = (kernel.state.state_digest.clone(), kernel.state.header.now);
        let exported = kernel.export().unwrap();
        drop(kernel);
        let reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.state.state_digest, digest);
        assert_eq!(reopened.state.header.now, now);
    }

    /// An export is a claim about a chain, and `open` believes none of it that
    /// replay does not derive: a chain with a commit missing, repeated or out of
    /// order, a Ghostlight commit or export schema, and a non-canonical
    /// encoding of an honest chain are each refused.
    #[test]
    fn forged_exports_are_refused() {
        let kernel = clock_history();
        let honest = kernel.journal.commits.clone();
        assert_eq!(honest.len(), 3, "genesis, a patch and a tick");
        reopen(&honest).expect("the honest chain opens");
        let refused = |what: &str, commits: &[WorldCommit]| {
            assert!(
                reopen(commits).is_err(),
                "{what} was admitted"
            );
        };

        refused("a missing middle commit", &[honest[0].clone(), honest[2].clone()]);
        refused("a world without genesis", &honest[1..]);
        refused("a repeated commit", &[honest[0].clone(), honest[1].clone(), honest[1].clone(), honest[2].clone()]);
        refused("reordered commits", &[honest[0].clone(), honest[2].clone(), honest[1].clone()]);
        refused("no commits", &[]);

        // Re-sealed down the chain, so the schema is the only thing wrong.
        let mut ghostlight = honest.clone();
        ghostlight[1].schema = "ghostlight.world_commit.consumer.v10".into();
        for index in 1..ghostlight.len() {
            if index > 1 {
                ghostlight[index].previous_commit_digest = Some(ghostlight[index - 1].digest.clone());
            }
            ghostlight[index].digest = commit_digest(&ghostlight[index]).unwrap();
        }
        refused("a ghostlight commit schema", &ghostlight);

        let foreign = rmp_serde::to_vec_named(&ExportRef {
            schema: "ghostlight.world_state.consumer.v10",
            commits: &honest,
        })
        .unwrap();
        assert!(matches!(
            WorldJournal::open(&foreign),
            Err(JournalError::Corrupt(_))
        ));

        let positional = rmp_serde::to_vec(&ExportRef {
            schema: JOURNAL_SCHEMA,
            commits: &honest,
        })
        .unwrap();
        assert!(matches!(
            WorldJournal::open(&positional),
            Err(JournalError::Corrupt(_))
        ));
        assert!(matches!(
            WorldJournal::open(&[0xc1]),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// A chain written under the whole-state digest is refused: its commits carry
    /// the v1 schema, re-sealed end to end so the schema is the only thing wrong.
    #[test]
    fn a_v1_commit_is_refused() {
        let kernel = clock_history();
        let mut v1 = kernel.journal.commits.clone();
        reopen(&v1).expect("the honest chain opens");
        for index in 0..v1.len() {
            v1[index].schema = "aetherlight.world_commit.v1".into();
            if index > 0 {
                v1[index].previous_commit_digest = Some(v1[index - 1].digest.clone());
            }
            v1[index].digest = commit_digest(&v1[index]).unwrap();
        }
        match reopen(&v1) {
            Err(JournalError::UnsupportedSchema { found, supported }) => {
                assert_eq!(found, "aetherlight.world_commit.v1");
                assert_eq!(supported, COMMIT_SCHEMA);
            }
            other => panic!("a v1 chain must be refused by name, got {other:?}"),
        }
        // A later commit of another generation is refused the same way.
        let mut mixed = kernel.journal.commits.clone();
        last_commit(&mut mixed).schema = "aetherlight.world_commit.v3".into();
        assert!(matches!(
            reopen(&mixed),
            Err(JournalError::UnsupportedSchema { .. })
        ));
    }

    /// A stored tick whose span is zero or above the bound is not a world: the
    /// export does not decode. This is the door the clock needs no check behind:
    /// a zero-minute tick would be a commit that moves nothing, and an oversize
    /// one a commit no `submit` could have built.
    #[test]
    fn a_stored_tick_with_a_span_outside_the_bound_does_not_open() {
        let kernel = clock_history();
        let honest = export_of(&kernel.journal.commits);
        // MessagePack names the span `minutes` and encodes the honest ones
        // (1, 2, 30) as a single positive fixint byte, so each is rewritten in
        // place, in the command and again in the effect.
        let key = b"\xa7minutes";
        let at: Vec<usize> = (0..honest.len() - key.len())
            .filter(|&at| &honest[at..at + key.len()] == key)
            .collect();
        assert!(at.len() >= 2, "a tick carries its span in the command and its effect");
        for forged_span in [vec![0x00], [&[0xce][..], &(crate::patch::MAX_ROUTE_COST + 1).to_be_bytes()].concat()] {
            let mut bytes = honest.clone();
            for &start in at.iter().rev() {
                let value = start + key.len();
                assert!(honest[value] < 0x80, "an honest span is a fixint");
                bytes.splice(value..=value, forged_span.iter().copied());
            }
            let error = WorldJournal::open(&bytes).err().expect("a forged span opened");
            assert!(
                error.to_string().contains("could not decode the export")
                    && error.to_string().contains("tick span"),
                "{forged_span:?}: {error}"
            );
        }
    }

    /// The replay-side future check reads only the subjects the transaction
    /// touched, and a key a subject already held is not newly born.
    #[test]
    fn a_commitment_born_due_is_corrupt_only_for_a_touched_new_key() {
        use crate::tests::subject_by_label;
        let (kernel, _) = crate::golden_tests::golden_session();
        let witness = subject_by_label(&kernel.state, "The Witness");
        let held = kernel
            .state
            .commitments
            .get(&witness)
            .cloned()
            .expect("the witness holds a routine");
        let mut early = kernel.state.clone();
        early.header.get_mut().now = crate::FictionalMinutes(0);
        let mut late = kernel.state.clone();
        late.header.get_mut().now = crate::FictionalMinutes(u64::MAX);

        assert!(verify_commitments_are_future(&early, &[(witness, None)]).is_ok());
        assert!(matches!(
            verify_commitments_are_future(&late, &[(witness, None)]),
            Err(JournalError::Corrupt(_))
        ));
        assert!(verify_commitments_are_future(&late, &[(witness, Some(held))]).is_ok());
        assert!(verify_commitments_are_future(&late, &[]).is_ok());
    }

    /// A committed command names the exact world and revision it applies to;
    /// each is judged on its own.
    #[test]
    fn a_command_aimed_at_another_world_or_revision_is_corrupt() {
        let kernel = clock_history();
        for aim_at_world in [true, false] {
            let mut forged = kernel.journal.commits.clone();
            let last = forged.last_mut().unwrap();
            let CommittedCommand::WorldCommand(command) = &mut last.command else {
                panic!("the last commit is a world command");
            };
            if aim_at_world {
                command.world_id = WorldId::nil_for_test();
            } else {
                command.expected_revision += 1;
            }
            last.digest = commit_digest(last).unwrap();
            assert!(
                matches!(reopen(&forged), Err(JournalError::Corrupt(_))),
                "a command aimed at another {} was admitted",
                if aim_at_world { "world" } else { "revision" }
            );
        }
    }

    /// A stored digest is checked against the commit it seals, on its own:
    /// flipping one character of any commit's digest, with nothing re-sealed,
    /// leaves only the digest clause to refuse it (the last commit and a lone
    /// genesis have no successor whose previous-digest could).
    #[test]
    fn a_stored_digest_flipped_without_resealing_is_refused() {
        let kernel = clock_history();
        let honest = kernel.journal.commits.clone();
        let flip = |digest: &str| {
            let mut flipped = digest.to_owned();
            let last = flipped.pop().unwrap();
            flipped.push(if last == '0' { '1' } else { '0' });
            flipped
        };
        for upto in 1..=honest.len() {
            let mut chain = honest[..upto].to_vec();
            let tail = chain.last_mut().unwrap();
            tail.digest = flip(&tail.digest);
            assert!(
                matches!(reopen(&chain), Err(JournalError::Corrupt(_))),
                "a flipped digest on commit {} of {upto} was admitted",
                upto - 1
            );
        }
    }

    /// Each clause of the genesis condition refuses alone: a lone genesis,
    /// re-sealed after one field is rewritten, so nothing else can object.
    #[test]
    fn each_genesis_clause_refuses_on_its_own() {
        let kernel = clock_history();
        let genesis = kernel.journal.commits[0].clone();
        let forgeries: [(&str, &dyn Fn(&mut WorldCommit)); 5] = [
            ("schema", &|commit| commit.schema = "ghostlight.world_commit.consumer.v10".into()),
            ("a previous revision", &|commit| commit.previous_revision = Some(0)),
            ("a nonzero revision", &|commit| commit.resulting_revision = 1),
            ("a previous state digest", &|commit| {
                commit.previous_state_digest = Some("sha256:00".into())
            }),
            ("a previous commit digest", &|commit| {
                commit.previous_commit_digest = Some("sha256:00".into())
            }),
        ];
        reopen(std::slice::from_ref(&genesis)).expect("a lone honest genesis opens");
        for (what, forge) in forgeries {
            let mut forged = genesis.clone();
            forge(&mut forged);
            forged.digest = commit_digest(&forged).unwrap();
            assert!(
                reopen(&[forged]).is_err(),
                "a genesis with {what} was admitted"
            );
        }
    }

    /// Position `i` holds resulting revision `i`: a commit that names a later
    /// revision than its place, with a state digest honestly computed for that
    /// revision, is still not the chain's next commit.
    #[test]
    fn a_chain_that_skips_a_revision_is_refused() {
        let kernel = clock_history();
        let honest = kernel.journal.commits.clone();
        let mut state = reopen(&honest[..2]).expect("genesis and the patch open");
        state.revision = 7;
        let mut forged = vec![honest[0].clone(), honest[1].clone()];
        forged[1].resulting_revision = 7;
        forged[1].resulting_state_digest = state.full_state_digest().unwrap();
        forged[1].digest = commit_digest(&forged[1]).unwrap();
        assert!(matches!(reopen(&forged), Err(JournalError::Corrupt(_))));
    }

    /// A world is named by its creation: a genesis whose world id is any other
    /// is refused, however consistently every id under it was derived.
    #[test]
    fn a_world_id_not_derived_from_its_creation_is_refused() {
        use crate::patch::resolve_patch;
        use crate::tests::{auth_principal, creation, owner};
        use crate::{CommandId, PreparedCreation};

        let input = creation("Misnamed");
        let honest = crate::prepare_creation(input.clone(), &auth_principal(owner())).unwrap();
        let other = WorldId::of(CommandId::issue());
        assert_ne!(other, honest.world_id);
        let resolved = resolve_patch(
            &WorldState::empty(other, input.owner.clone(), honest.title.clone(), None),
            input.id(),
            &input.patch,
        )
        .unwrap();
        let (kernel, _) = WorldKernel::initialize(PreparedCreation {
            command: input,
            world_id: other,
            owner: honest.owner,
            title: honest.title,
            resolved,
        })
        .unwrap();
        assert!(matches!(
            reopen(&kernel.journal.commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// A stored command is not believed about who made it. The forgery re-seals
    /// the commit around its rewrite and the head's last commit digest names the
    /// re-sealed commit, so no digest can answer for the check; the control, the
    /// same re-sealing around the honest caller, replays. Both Play's doors are
    /// forged: a patch row and a clock row recorded as the owner's.
    #[test]
    fn a_commit_forged_to_the_owner_fails_replay() {
        use crate::tests::owner;

        let kernel = clock_history();
        let honest: Vec<WorldCommit> = kernel.journal.commits.clone();
        // Re-seals the rewritten commit and every later one around it, so the
        // chain of previous-commit digests agrees and only the caller check is
        // left to refuse the history.
        let forge = |is_row: &dyn Fn(&WorldEffect) -> bool,
                     rewrite: &dyn Fn(&mut WorldCommit)|
         -> Vec<WorldCommit> {
            let mut forged: Vec<WorldCommit> = honest.clone();
            forged.sort_by_key(|commit| commit.resulting_revision);
            let rows: Vec<usize> = (0..forged.len())
                .filter(|index| is_row(&forged[*index].effect))
                .collect();
            let [at] = <[usize; 1]>::try_from(rows)
                .expect("the history holds exactly one such row");
            rewrite(&mut forged[at]);
            for index in at..forged.len() {
                if index > at {
                    forged[index].previous_commit_digest = Some(forged[index - 1].digest.clone());
                }
                forged[index].digest = commit_digest(&forged[index]).unwrap();
            }
            forged
        };
        let patch_row = |effect: &WorldEffect| matches!(effect, WorldEffect::PatchAdmitted { .. });
        let clock_row = |effect: &WorldEffect| matches!(effect, WorldEffect::TimeAdvanced { .. });

        for is_row in [&patch_row as &dyn Fn(&WorldEffect) -> bool, &clock_row] {
            let control = forge(is_row, &|_| {});
            reopen(&control)
                .expect("a consistent re-sealed store replays");

            let forged = forge(is_row, &|commit| {
                if let CommittedCommand::WorldCommand(command) = &mut commit.command {
                    command.caller = CallerId::Principal(owner());
                }
            });
            let error = reopen(&forged)
                .expect_err("a commit forged to the owner replayed");
            assert!(matches!(error, JournalError::Corrupt(_)), "{error:?}");
        }
    }

    /// The tick is re-derived at replay by the same function that produced it,
    /// so a committed motion rewritten after the fact no longer reduces from
    /// its command. The clock chain refuses a rewritten reading the same way.
    #[test]
    fn a_forged_motion_does_not_replay() {
        use crate::tests::{auth_principal, command, owner};
        use crate::{
            AuthenticatedCaller, CallerId, CommandBody, FictionalMinutes, 
            TickMinutes,
        };

        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Chronicle"),
            &authenticated,
        )
        .unwrap();
        let active = kernel.snapshot().unwrap();
        let caller = CallerId::Play;
        kernel
            .submit(
                command(
                    &active,
                    caller.clone(),
                    CommandBody::AdvanceTime {
                        minutes: TickMinutes::new(30).unwrap(),
                    },
                ),
                &AuthenticatedCaller::fixture(caller),
            )
            .unwrap();

        let mut forged_commits = kernel.journal.commits.clone();
        let forged = last_commit(&mut forged_commits);
        let WorldEffect::TimeAdvanced { to, .. } = &mut forged.effect else {
            panic!("expected a time-advanced effect");
        };
        *to = FictionalMinutes(31);
        forged.digest = commit_digest(forged).unwrap();

        assert!(matches!(
            reopen(&forged_commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// Replay re-derives the operation half too: a committed relocation rewritten
    /// to move a different subject no longer reduces from its command.
    #[test]
    fn a_forged_relocate_effect_does_not_apply() {
        use crate::tests::{admit_topology, auth_principal, command, owner};

        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Kharad"),
            &authenticated,
        )
        .unwrap();
        let topology = admit_topology(&mut kernel);
        let active = kernel.snapshot().unwrap();
        kernel
            .submit(
                command(
                    &active,
                    CallerId::Play,
                    CommandBody::AdmitPatch {
                        patch: WorldPatch {
                            declarations: Vec::new(),
                            operations: vec![crate::ComponentOp::Relocate {
                                subject: Ref::Existing(topology.walker),
                                via: Ref::Existing(topology.ramp),
                            }],
                            evidence: Vec::new(),
                        },
                    },
                ),
                &AuthenticatedCaller::verified_play(),
            )
            .unwrap();

        let mut forged_commits = kernel.journal.commits.clone();
        let forged = last_commit(&mut forged_commits);
        let WorldEffect::PatchAdmitted { resolved, .. } = &mut forged.effect else {
            panic!("expected an admitted patch effect");
        };
        let crate::ResolvedOp::Relocate { subject_id, .. } = &mut resolved.operations[0]
        else {
            panic!("expected a lowered relocation");
        };
        *subject_id = crate::SubjectId::issue();
        forged.digest = commit_digest(forged).unwrap();

        assert!(matches!(
            reopen(&forged_commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// Soul: `a_forged_band_or_forged_effect_does_not_apply` proves the live
    /// `apply_effect` arm re-derives. `verify_state_shape` does not re-derive a
    /// band — it only bounds the index and refuses effects under an empty band —
    /// so the layer that kills a persisted forgery is replay effect-equality.
    /// This forges a committed decision inside the journal, re-digests it into a
    /// contiguous chain, and asserts recovery refuses it.
    #[test]
    fn soul_a_forged_band_or_effect_in_the_journal_dies_at_replay() {
        use crate::tests::{
            admit_custody, admit_topology, affordance_named, auth_principal, command,
            owner, play_caller,
        };
        use crate::{
            AuthenticatedCaller, DecisionInvocation, Magnitude, ProposedEffect, Quantity,
            RoleBinding, Target,
        };

        let forge = |kind: &str, mutate: &dyn Fn(&mut crate::DecisionEvent)| {
            let authenticated = auth_principal(owner());
            let (mut kernel, _) = WorldKernel::create(crate::tests::creation("ForgedDecision"),
                &authenticated,
            )
            .unwrap();
            let topology = admit_topology(&mut kernel);
            let custody = admit_custody(&mut kernel, &topology);
            let active = kernel.snapshot().unwrap();
            let entry = affordance_named(&active, kind);
            let caller = play_caller();
            let role = |name: &str, target| RoleBinding {
                role: crate::Role(name.into()),
                target,
            };
            kernel
                .submit(
                    command(
                        &active,
                        caller.clone(),
                        CommandBody::Act {
                            actor: custody.holder,
                            invocation: DecisionInvocation {
                                affordance: entry,
                                bindings: vec![
                                    role("from", Target::Subject(custody.holder)),
                                    role("recipient", Target::Subject(custody.counterparty)),
                                    role("place", Target::Entity(topology.yard)),
                                    role("resource", Target::Entity(custody.tithe)),
                                ],
                                proposed: vec![ProposedEffect {
                                    slot: 0,
                                    magnitude: Magnitude::Quantity(Quantity(2)),
                                }],
                                speech: None,
                                display: None,
                            },
                        },
                    ),
                    &AuthenticatedCaller::fixture(caller),
                )
                .unwrap();

            // The honest journal recovers.
            assert!(reopen(&kernel.journal.commits).is_ok());

            let mut forged_commits = kernel.journal.commits.clone();
            let forged = last_commit(&mut forged_commits);
            let WorldEffect::Acted { event, .. } = &mut forged.effect else {
                panic!("expected an acted effect");
            };
            mutate(event);
            forged.digest = commit_digest(forged).unwrap();
            forged_commits
        };

        // A band the seed did not draw, held inside a chain that verifies.
        let commits = forge("carry_chance", &|event| {
            event.band = (event.band + 1) % 3;
        });
        assert!(matches!(
            reopen(&commits),
            Err(JournalError::Corrupt(_))
        ));

        // A magnitude the ceiling admitted but the lowering never produced.
        let commits = forge("carry", &|event| {
            let Some(crate::ResolvedOp::Transfer { qty, .. }) = event.effects.first_mut()
            else {
                panic!("expected a lowered transfer");
            };
            *qty = Quantity(1);
        });
        assert!(matches!(
            reopen(&commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    #[test]
    fn replay_rejects_genesis_that_does_not_derive_from_its_creation_command() {
        let owner = PrincipalId::new("owner@example.test");
        let authenticated = AuthenticatedCaller::fixture(CallerId::Principal(owner.clone()));
        let creation = CreateWorld {
            owner: owner.clone(),
            title: "Admitted".into(),
            patch: owner_patch(SubjectDeclaration {
                handle: DraftHandle::new("owner"),
                label: "Owner".into(),
                kind: SubjectKind::Person,
                affordances: kernel_speak_grant(),
                position: None,
                import_key: None,
            }),
            grammar: None,
        };
        let (kernel, _) = WorldKernel::create(creation, &authenticated).unwrap();
        let mut forged_commits = kernel.journal.commits.clone();
        let forged = genesis_commit(&mut forged_commits);
        let CommittedCommand::CreateWorld(command) = &mut forged.command else {
            panic!("expected genesis creation command");
        };
        command.title = "Different command".into();
        forged.digest = commit_digest(forged).unwrap();

        assert!(matches!(
            reopen(&forged_commits),
            Err(JournalError::Corrupt(_))
        ));

        let mut binding_forged_commits = kernel.journal.commits.clone();
        let binding_forged = genesis_commit(&mut binding_forged_commits);
        let WorldEffect::WorldCreated { resolved, .. } = &mut binding_forged.effect else {
            panic!("expected genesis effect");
        };
        resolved.subjects[0].subject.label = "Not the creation subject".into();
        binding_forged.digest = commit_digest(binding_forged).unwrap();

        assert!(matches!(
            reopen(&binding_forged_commits),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// Soul falsification: the extended replay shape check is not decoration.
    /// A forged state carrying a self-loop, a cost out of range, a
    /// non-place endpoint, or a position on a non-place is refused by
    /// `verify_state_shape`, which `WorldJournal::open` runs on the replayed head.
    #[test]
    fn soul_forged_topology_states_are_refused_by_the_shape_check() {
        use crate::patch::{AccessKind, Cost, EdgeRecord, MAX_ROUTE_COST};
        use crate::tests::{admit_topology, auth_principal, owner};

        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Forged Rows"),
            &auth_principal(owner()),
        )
        .unwrap();
        let topology = admit_topology(&mut kernel);
        // The honest state recovers.
        assert!(verify_state_shape(&kernel.state).is_ok());
        assert!(reopen(&kernel.journal.commits).is_ok());

        let self_loop = {
            let mut state = kernel.state.clone();
            state.edges.insert(
                topology.ramp,
                EdgeRecord::Route {
                    label: "The Yard Ramp".into(),
                    from: topology.yard,
                    to: topology.yard,
                    access: AccessKind::Public,
                    cost: Cost(12),
                    open: true,
                },
            );
            state
        };
        let bad_cost = {
            let mut state = kernel.state.clone();
            state
                .edges
                .get_mut(&topology.ramp)
                .unwrap()
                .set_cost(Cost(MAX_ROUTE_COST + 1));
            state
        };
        let non_place_endpoint = {
            let mut state = kernel.state.clone();
            state.entities.get_mut(&topology.road).unwrap().kind =
                crate::EntityKind::Resource;
            state
        };
        let stray_position = {
            let mut state = kernel.state.clone();
            state.positions.insert(
                topology.walker,
                Position {
                    place: EntityId::issue(),
                },
            );
            state
        };
        for forged in [self_loop, bad_cost, non_place_endpoint, stray_position] {
            let error = verify_state_shape(&forged).unwrap_err();
            assert!(
                matches!(error, JournalError::Corrupt(_)),
                "a forged topology row recovered: {error:?}"
            );
        }
    }

    /// A committed levy survives a restart exactly: the snapshot, the band, the
    /// lowered effects, and the re-derived authority verdict all come back, and
    /// the same envelope is already applied.
    #[test]
    fn restart_replay_after_a_levy_is_exact() {
        use crate::tests::{affordance_named, civic_world, command, owner, play_caller};
        use crate::{
            DecisionInvocation, Magnitude, ProposedEffect, Quantity, Role, RoleBinding,
            Target,
        };

        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Replayed Levy"),
            &crate::tests::auth_principal(owner()),
        )
        .unwrap();
        let (_, civic, active) = civic_world(&mut kernel);
        let caller = play_caller();
        let envelope = command(
            &active,
            caller.clone(),
            CommandBody::Act {
                actor: civic.treasury,
                invocation: DecisionInvocation {
                    affordance: affordance_named(&active, "levy"),
                    bindings: vec![
                        RoleBinding {
                            role: Role("payer".into()),
                            target: Target::Subject(civic.farmer),
                        },
                        RoleBinding {
                            role: Role("resource".into()),
                            target: Target::Entity(civic.grain),
                        },
                    ],
                    proposed: vec![ProposedEffect {
                        slot: 0,
                        magnitude: Magnitude::Quantity(Quantity(3)),
                    }],
                    speech: None,
                    display: None,
                },
            },
        );
        kernel
            .submit(
                envelope.clone(),
                &AuthenticatedCaller::fixture(caller.clone()),
            )
            .unwrap();
        let committed = kernel.snapshot().unwrap();
        let events = kernel.events();
        let event = events.last().expect("the levy event").clone();
        let exported = kernel.export().unwrap();
        drop(kernel);

        let mut reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.snapshot().unwrap(), committed);
        let events = reopened.events();
        let replayed = events.last().expect("the replayed event");
        assert_eq!(replayed.band, event.band);
        assert_eq!(replayed.effects, event.effects);
        assert!(matches!(
            reopened.submit(envelope, &AuthenticatedCaller::fixture(caller)),
            Err(KernelError::RevisionMismatch { .. })
        ));
    }

    /// Soul falsification: the civic shape checks are not decoration. A forged
    /// state that widens a jurisdiction, doubles an incumbency, seats an
    /// institution, or empties a delegation is refused by the shape check.
    #[test]
    fn a_forged_authority_or_incumbency_is_corrupt() {
        use crate::tests::{
            BAILIFF_OFFICE, LEVY_KIND, WARDEN_OFFICE, auth_principal, authority_kind, civic_world,
            office, owner,
        };
        use crate::{AuthorityGrant, AuthorityTarget, Office};
        use std::collections::BTreeSet;

        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Forged Civics"),
            &auth_principal(owner()),
        )
        .unwrap();
        let (topology, civic, _) = civic_world(&mut kernel);
        assert!(verify_state_shape(&kernel.state).is_ok());
        assert!(reopen(&kernel.journal.commits).is_ok());

        let widened = {
            let mut state = kernel.state.clone();
            state.authority.insert(
                civic.reeve,
                BTreeSet::from([AuthorityGrant {
                    kind: authority_kind(LEVY_KIND),
                    over: AuthorityTarget::PlaceSubtree(civic.chamber),
                }]),
            );
            state
        };
        let dangling = {
            let mut state = kernel.state.clone();
            state.authority.insert(
                civic.outsider,
                BTreeSet::from([AuthorityGrant {
                    kind: authority_kind(LEVY_KIND),
                    over: AuthorityTarget::PlaceSubtree(EntityId::issue()),
                }]),
            );
            state
        };
        let doubled = {
            let mut state = kernel.state.clone();
            state
                .selection
                .get_mut(&civic.treasury)
                .unwrap()
                .get_mut(&office(BAILIFF_OFFICE))
                .unwrap()
                .incumbent = Some(civic.reeve);
            state
        };
        let seated_institution = {
            let mut state = kernel.state.clone();
            state
                .selection
                .get_mut(&civic.treasury)
                .unwrap()
                .get_mut(&office(WARDEN_OFFICE))
                .unwrap()
                .incumbent = Some(civic.treasury);
            state
        };
        let inert_office = {
            let mut state = kernel.state.clone();
            state.selection.get_mut(&civic.treasury).unwrap().insert(
                office(WARDEN_OFFICE),
                Office {
                    incumbent: Some(civic.reeve),
                    delegated: BTreeSet::new(),
                },
            );
            state
        };
        let office_on_person = {
            let mut state = kernel.state.clone();
            state
                .selection
                .insert(topology.walker, state.selection[&civic.treasury].clone());
            state
        };
        for forged in [
            widened,
            dangling,
            doubled,
            seated_institution,
            inert_office,
            office_on_person,
        ] {
            let error = verify_state_shape(&forged).unwrap_err();
            assert!(
                matches!(error, JournalError::Corrupt(_)),
                "a forged civic row recovered: {error:?}"
            );
        }
    }

    /// Soul falsification of the structural-overlap decision, updated for the
    /// admission-time covering check: a `Subject` grant naming ground a
    /// `PlaceSubtree` grant already covers is refused on arrival, but nothing
    /// re-derives that check off of live position. Once the farmer walks out
    /// from under the hall, the identical grant is unremarkable, and no
    /// `Relocate` — in either direction — is ever asked to keep a shape
    /// decision current.
    #[test]
    fn a_relocation_never_corrupts_a_civic_store() {
        use crate::tests::{
            LEVY_KIND, auth_principal, civic_world, operations, over_subject, owner, reject_play,
            submit_play,
        };
        use crate::{ComponentOp, Mismatch};

        let (mut kernel, _) = WorldKernel::create(crate::tests::creation("Moving Civics"),
            &auth_principal(owner()),
        )
        .unwrap();
        let (_, civic, _) = civic_world(&mut kernel);

        let admit = |kernel: &mut WorldKernel, ops: Vec<ComponentOp>| {
            let before = kernel.snapshot().unwrap();
            assert!(submit_play(kernel, &before, operations(ops)).resulting_revision > 0);
            verify_state_shape(&kernel.state).expect("the committed store keeps its shape");
            assert!(
                crate::overlapping_holder(&kernel.state).is_none(),
                "a committed store never holds two overlapping jurisdictions"
            );
        };
        let place_covers = |kernel: &WorldKernel| {
            crate::covers(
                &kernel.state,
                crate::AuthorityTarget::PlaceSubtree(civic.hall),
                crate::Target::Subject(civic.farmer),
            )
        };
        let name_farmer = || {
            vec![crate::tests::grant_to(
                civic.treasury,
                LEVY_KIND,
                over_subject(civic.farmer),
            )]
        };

        // The treasury's hall grant already covers the farmer standing in the
        // chamber underneath it, so naming him directly is a second source
        // for the same kind of act, and admission refuses it while he stands
        // there.
        assert!(place_covers(&kernel));
        let before = kernel.snapshot().unwrap();
        assert_eq!(
            reject_play(&mut kernel, &before, operations(name_farmer())),
            vec![Mismatch::OverlappingJurisdiction { operation: 0 }]
        );

        // Out of the subtree, one route at a time.
        for edge in [civic.passage, civic.causeway] {
            admit(
                &mut kernel,
                vec![ComponentOp::Relocate {
                    subject: Ref::Existing(civic.farmer),
                    via: Ref::Existing(edge),
                }],
            );
        }
        // The move dissolved the cover without any operation naming it, which
        // is why the disjointness rule cannot be occupancy-sensitive: nothing
        // reruns the covering check on a `Relocate`, in or out.
        assert!(!place_covers(&kernel));

        // Now that he stands outside it, the identical grant is unremarkable.
        admit(&mut kernel, name_farmer());
        assert!(kernel.state.authority[&civic.treasury].len() >= 3);

        // The whole history replays, and the reopened world is identical.
        let committed = kernel.snapshot().unwrap();
        let exported = kernel.export().unwrap();
        drop(kernel);
        let reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.snapshot().unwrap(), committed);
    }
}

#[cfg(test)]
mod custody_tests {
    use super::*;
    use super::tests::{last_commit, reopen};
    use crate::{
        CallerId, CommandBody, ComponentOp, DependencyRef, EntityKind, Quantity, Ref,
        WorldKernel, WorldPatch,
        tests::{admit_custody, admit_topology, auth_principal, command, creation, owner},
    };

    /// Replay re-derives the custody partitions from the commit log alone: a
    /// world reopened after a transfer is byte-identical, and the transfer's own
    /// envelope is idempotent.
    #[test]
    fn restart_replay_after_a_transfer_is_exact() {
        let authenticated = auth_principal(owner());
        let (mut kernel, _) =
            WorldKernel::create(creation("Replay"), &authenticated)
                .unwrap();
        let topology = admit_topology(&mut kernel);
        let custody = admit_custody(&mut kernel, &topology);
        let active = kernel.snapshot().unwrap();

        let envelope = command(
            &active,
            CallerId::Play,
            CommandBody::AdmitPatch {
                patch: WorldPatch {
                    declarations: Vec::new(),
                    operations: vec![ComponentOp::Transfer {
                        from: Ref::Existing(custody.holder),
                        to: Ref::Existing(custody.counterparty),
                        resource: Ref::Existing(custody.tithe),
                        qty: Quantity(3),
                    }],
                    evidence: Vec::new(),
                },
            },
        );
        kernel.submit(envelope.clone(), &crate::AuthenticatedCaller::verified_play()).unwrap();
        let accepted = kernel.snapshot().unwrap();
        let exported = kernel.export().unwrap();
        drop(kernel);

        let mut reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.snapshot().unwrap(), accepted);
        assert!(matches!(
            reopened.submit(envelope, &crate::AuthenticatedCaller::verified_play()),
            Err(KernelError::RevisionMismatch { .. })
        ));
    }

    /// Replay recomputes `reduce`, so the band and the lowered operations are
    /// re-derived rather than trusted: a stored event whose band or effects the
    /// seed does not reproduce fails effect equality on the way back in.
    #[test]
    fn restart_replay_after_an_action_is_exact() {
        use crate::tests::{affordance_named, play_caller};
        use crate::{AuthenticatedCaller, Magnitude, ProposedEffect, RoleBinding, Target};

        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(creation("ActionReplay"),
            &authenticated,
        )
        .unwrap();
        let topology = admit_topology(&mut kernel);
        let custody = admit_custody(&mut kernel, &topology);
        let active = kernel.snapshot().unwrap();
        let carry = affordance_named(&active, "carry");
        let caller = play_caller();

        let role = |name: &str, target| RoleBinding {
            role: crate::Role(name.into()),
            target,
        };
        let envelope = command(
            &active,
            caller.clone(),
            CommandBody::Act {
                actor: custody.holder,
                invocation: crate::DecisionInvocation {
                    affordance: carry,
                    bindings: vec![
                        role("from", Target::Subject(custody.holder)),
                        role("recipient", Target::Subject(custody.counterparty)),
                        role("place", Target::Entity(topology.yard)),
                        role("resource", Target::Entity(custody.tithe)),
                    ],
                    proposed: vec![ProposedEffect {
                        slot: 0,
                        magnitude: Magnitude::Quantity(Quantity(2)),
                    }],
                    speech: None,
                    display: None,
                },
            },
        );
        let authenticated_controller = AuthenticatedCaller::fixture(caller);
        kernel
            .submit(envelope.clone(), &authenticated_controller)
            .unwrap();
        let accepted = kernel.snapshot().unwrap();
        let committed = kernel
            .events()
            .last()
            .expect("the committed event")
            .clone();
        assert!(!committed.effects.is_empty());
        let exported = kernel.export().unwrap();
        drop(kernel);

        let mut reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.snapshot().unwrap(), accepted);
        assert_eq!(
            reopened.events().last().expect("the replayed event"),
            &committed
        );
        assert!(matches!(
            reopened.submit(envelope, &authenticated_controller),
            Err(KernelError::RevisionMismatch { .. })
        ));
    }

    /// The title clause of the shape check refuses an empty title and a
    /// noncanonical one, each on its own.
    #[test]
    fn a_forged_empty_or_padded_title_is_corrupt() {
        use crate::tests::{auth_principal, creation, owner};

        let (kernel, _) = WorldKernel::create(creation("Titled"),
            &auth_principal(owner()),
        )
        .unwrap();
        verify_state_shape(&kernel.state).expect("a canonical title passes");
        for title in ["", "  ", " Titled", "Titled "] {
            let mut forged = kernel.state.clone();
            forged.header.get_mut().title = title.into();
            assert!(
                matches!(verify_state_shape(&forged), Err(JournalError::Corrupt(_))),
                "title {title:?} passed the shape check"
            );
        }
    }

    /// A grant row is a verb list for a live subject: each clause of the shape
    /// check is pinned on `verify_state_shape` directly, one forged state per
    /// clause, so deleting any one of them fails a case.
    #[test]
    fn a_malformed_grant_row_is_corrupt() {
        use crate::tests::{auth_principal, creation, owner};
        use crate::{AffordanceId, SubjectId};

        let (kernel, _) = WorldKernel::create(creation("GrantRows"),
            &auth_principal(owner()),
        )
        .unwrap();
        verify_state_shape(&kernel.state).expect("a fresh world has canonical grant rows");
        let held = *kernel.state.affordance_grants.keys().next().unwrap();

        let corrupt = |name: &str, forge: &dyn Fn(&mut WorldState)| {
            let mut forged = kernel.state.clone();
            forge(&mut forged);
            assert!(
                matches!(verify_state_shape(&forged), Err(JournalError::Corrupt(_))),
                "{name} passed the shape check"
            );
        };
        corrupt("a grant row for a retired subject", &|state| {
            state.subjects.get_mut(&held).unwrap().retired = true;
        });
        corrupt("an empty grant row", &|state| {
            state.affordance_grants.get_mut(&held).unwrap().clear();
        });
        corrupt("a grant row for no subject", &|state| {
            let verbs = state.affordance_grants[&held].clone();
            state.affordance_grants.insert(SubjectId::issue(), verbs);
        });
        corrupt("a grant row naming an uncataloged verb", &|state| {
            state
                .affordance_grants
                .get_mut(&held)
                .unwrap()
                .insert(AffordanceId::issue());
        });
    }

    /// A chain holding `Acted` commits recovers and verifies on reopen.
    #[test]
    fn an_acted_chain_reopens() {
        use crate::tests::{
            act, auth_principal, creation, first_grant, owner, speak,
        };

        let (mut kernel, _) = WorldKernel::create(creation("ActedChain"),
            &auth_principal(owner()),
        )
        .unwrap();
        let active = kernel.snapshot().unwrap();
        let actor = *active.subjects.first().map(|subject| &subject.id).unwrap();
        act(
            &mut kernel,
            actor,
            speak(first_grant(&active, actor), "The chain holds."),
        )
        .unwrap();
        act(
            &mut kernel,
            actor,
            speak(first_grant(&active, actor), "And again."),
        )
        .unwrap();
        let accepted = kernel.snapshot().unwrap();
        let exported = kernel.export().unwrap();
        drop(kernel);
        let reopened = WorldKernel::open(&exported).expect("the acted chain recovers");
        assert_eq!(reopened.snapshot().unwrap(), accepted);
        verify_state_shape(&reopened.state).expect("an honest event passes the shape check");
    }

    /// Absence is zero and one slot holds one pair, so a stored zero, an empty
    /// holder, and a holding on something that is not a resource are each
    /// corrupt at replay.
    ///
    /// Deviation from the spec's wording, decided here: the assertion is against
    /// `verify_state_shape`, which owns these clauses. Routing through
    /// `verify_history` would also fail on replay-equality, so it would pass
    /// even if the shape clauses were deleted — which is the opposite of a
    /// falsification.
    #[test]
    fn a_zero_or_dangling_holding_is_corrupt() {
        let authenticated = auth_principal(owner());
        let (mut kernel, _) =
            WorldKernel::create(creation("Shape"), &authenticated)
                .unwrap();
        let topology = admit_topology(&mut kernel);
        let custody = admit_custody(&mut kernel, &topology);
        verify_state_shape(&kernel.state).expect("the admitted world has a canonical shape");

        let mut zero = kernel.state.clone();
        zero.holdings
            .get_mut(&custody.holder)
            .unwrap()
            .insert(custody.tithe, Quantity(0));
        assert!(matches!(
            verify_state_shape(&zero),
            Err(JournalError::Corrupt(_))
        ));

        let mut emptied = kernel.state.clone();
        emptied.holdings.get_mut(&custody.holder).unwrap().clear();
        assert!(matches!(
            verify_state_shape(&emptied),
            Err(JournalError::Corrupt(_))
        ));

        let mut not_a_resource = kernel.state.clone();
        assert_eq!(
            not_a_resource.entities[&topology.yard].kind,
            EntityKind::Place
        );
        not_a_resource
            .holdings
            .get_mut(&custody.holder)
            .unwrap()
            .insert(topology.yard, Quantity(1));
        assert!(matches!(
            verify_state_shape(&not_a_resource),
            Err(JournalError::Corrupt(_))
        ));

        let mut dangling = kernel.state.clone();
        dangling.dependencies.insert(
            custody.holder,
            BTreeSet::from([crate::DependencyTarget::Subject(custody.holder)]),
        );
        assert!(matches!(
            verify_state_shape(&dangling),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// Soul: replay is exact after *every* custody and dependency operation, not
    /// only after a transfer. The reopened world is byte-identical, the last
    /// envelope is idempotent, and the whole chain still verifies.
    #[test]
    fn soul_replay_is_exact_after_every_custody_operation() {
        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(creation("EveryOperation"),
            &authenticated,
        )
        .unwrap();
        let topology = admit_topology(&mut kernel);
        // `Admit` and its evidence are Draft-only, so the opening balance is
        // already on the log before activation.
        let custody = admit_custody(&mut kernel, &topology);
        let active = kernel.snapshot().unwrap();

        let envelope = command(
            &active,
            CallerId::Play,
            CommandBody::AdmitPatch {
                patch: WorldPatch {
                    declarations: Vec::new(),
                    operations: vec![
                        ComponentOp::Transfer {
                            from: Ref::Existing(custody.holder),
                            to: Ref::Existing(custody.counterparty),
                            resource: Ref::Existing(custody.tithe),
                            qty: Quantity(3),
                        },
                        ComponentOp::Transform {
                            holder: Ref::Existing(custody.holder),
                            from_resource: Ref::Existing(custody.tithe),
                            into_resource: Ref::Existing(custody.ingot),
                            qty: Quantity(2),
                        },
                        ComponentOp::Consume {
                            holder: Ref::Existing(custody.counterparty),
                            resource: Ref::Existing(custody.tithe),
                            qty: Quantity(1),
                        },
                        ComponentOp::Bind {
                            subject: Ref::Existing(custody.holder),
                            target: DependencyRef::Route(Ref::Existing(topology.shutter)),
                        },
                        ComponentOp::Bind {
                            subject: Ref::Existing(custody.holder),
                            target: DependencyRef::Subject(Ref::Existing(custody.counterparty)),
                        },
                    ],
                    evidence: Vec::new(),
                },
            },
        );
        kernel.submit(envelope.clone(), &crate::AuthenticatedCaller::verified_play()).unwrap();
        let accepted = kernel.snapshot().unwrap();
        let holdings = kernel.state.holdings.clone();
        let dependencies = kernel.state.dependencies.clone();
        let digest = kernel.state.state_digest.clone();
        let exported = kernel.export().unwrap();
        drop(kernel);

        let mut reopened = WorldKernel::open(&exported).unwrap();
        assert_eq!(reopened.snapshot().unwrap(), accepted);
        assert_eq!(reopened.state.holdings, holdings);
        assert_eq!(reopened.state.dependencies, dependencies);
        assert_eq!(reopened.state.state_digest, digest);
        assert!(matches!(
            reopened.submit(envelope, &crate::AuthenticatedCaller::verified_play()),
            Err(KernelError::RevisionMismatch { .. })
        ));
    }

    /// Soul: a dependency naming a target of the wrong kind, or no canonical
    /// target at all, is refused at replay. The shape clauses carry the
    /// referential integrity the operation arm enforces at admission.
    #[test]
    fn soul_a_dependency_on_an_absent_or_wrong_kind_target_is_corrupt() {
        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(creation("DependencyShape"),
            &authenticated,
        )
        .unwrap();
        let topology = admit_topology(&mut kernel);
        let custody = admit_custody(&mut kernel, &topology);
        verify_state_shape(&kernel.state).expect("the admitted world has a canonical shape");

        // A place is not a resource, so a resource dependency naming one dangles.
        let mut wrong_kind = kernel.state.clone();
        wrong_kind.dependencies.insert(
            custody.holder,
            BTreeSet::from([crate::DependencyTarget::Resource(topology.yard)]),
        );
        assert!(matches!(
            verify_state_shape(&wrong_kind),
            Err(JournalError::Corrupt(_))
        ));

        // An empty target set is the second representation of nothing, and there
        // is only supposed to be one.
        let mut emptied = kernel.state.clone();
        emptied.dependencies.insert(custody.holder, BTreeSet::new());
        assert!(matches!(
            verify_state_shape(&emptied),
            Err(JournalError::Corrupt(_))
        ));

        // A holder key that names no canonical subject.
        let mut orphan = kernel.state.clone();
        let absent = *orphan.subjects.keys().next().unwrap();
        orphan.dependencies.insert(
            absent,
            BTreeSet::from([crate::DependencyTarget::Resource(custody.tithe)]),
        );
        orphan.subjects.remove(&absent);
        assert!(matches!(
            verify_state_shape(&orphan),
            Err(JournalError::Corrupt(_))
        ));
    }

    /// A world with one routine, one past-due goal, and a commit chain that
    /// reaches them: the fixture the two clock-forgery checks below need.
    fn ticking_world() -> (WorldKernel, super::super::SubjectId) {
        use crate::tests::{auth_principal, command, operations, owner};
        use crate::{
            AuthenticatedCaller, CallerId, CommandBody, CommitmentKind, ComponentOp,
            FictionalMinutes, Ref, TickMinutes,
        };

        let authenticated = auth_principal(owner());
        let (mut kernel, _) = WorldKernel::create(
            crate::tests::creation("Ticking"),
            &authenticated,
        )
        .unwrap();
        let active = kernel.snapshot().unwrap();
        let subject = active.subjects.first().expect("a seeded subject").id;
        let snapshot = kernel.snapshot().unwrap();
        kernel
            .submit(
                command(
                    &snapshot,
                    CallerId::Play,
                    operations(vec![
                        ComponentOp::CreateCommitment {
                            subject: Ref::Existing(subject),
                            counterparty: None,
                            kind: CommitmentKind::Routine,
                            due: FictionalMinutes(10),
                            period: Some(TickMinutes::new(10).unwrap()),
                            checks: Vec::new(),
                            statement: crate::Statement::new("What was promised, as the promisor would say it.").unwrap(),
                        },
                        ComponentOp::CreateCommitment {
                            subject: Ref::Existing(subject),
                            counterparty: None,
                            kind: CommitmentKind::Goal,
                            due: FictionalMinutes(20),
                            period: None,
                            checks: Vec::new(),
                            statement: crate::Statement::new("What was promised, as the promisor would say it.").unwrap(),
                        },
                    ]),
                ),
                &AuthenticatedCaller::verified_play(),
            )
            .unwrap();

        let caller = CallerId::Play;
        let snapshot = kernel.snapshot().unwrap();
        kernel
            .submit(
                command(
                    &snapshot,
                    caller.clone(),
                    CommandBody::AdvanceTime {
                        minutes: TickMinutes::new(30).unwrap(),
                    },
                ),
                &AuthenticatedCaller::fixture(caller),
            )
            .unwrap();
        (kernel, subject)
    }

    /// Soul falsification: the replay gate and the `apply_effect` gate are two
    /// independent re-derivations. This one exercises the replay side over a
    /// motion that actually moved commitments and pressure — a rewritten
    /// magnitude, a rewritten roll, and a fabricated row each stop replaying.
    #[test]
    fn soul_a_forged_tick_motion_does_not_replay() {
        use crate::{FictionalMinutes, PressureMagnitude, PressureSource};

        let (kernel, subject) = ticking_world();
        let WorldEffect::TimeAdvanced { motion, .. } = &kernel
            .journal
            .commits
            .iter()
            .max_by_key(|commit| commit.resulting_revision)
            .expect("the tick's commit")
            .effect
        else {
            panic!("expected a time-advanced effect");
        };
        assert!(!motion.fulfilled.is_empty(), "the routine rolled");
        assert!(!motion.pressed.is_empty(), "the past-due goal pressed");

        let forge = |rewrite: fn(&mut crate::Motion)| {
            let mut forged_commits = kernel.journal.commits.clone();
            let forged = last_commit(&mut forged_commits);
            let WorldEffect::TimeAdvanced { motion, .. } = &mut forged.effect else {
                panic!("expected a time-advanced effect");
            };
            rewrite(motion);
            forged.digest = commit_digest(forged).unwrap();
            reopen(&forged_commits)
        };

        let rewrites: [fn(&mut crate::Motion); 4] = [
            // A magnitude the tick never wrote.
            |motion| motion.pressed[0].magnitude = PressureMagnitude(42),
            // A roll to a date the period does not reach.
            |motion| motion.fulfilled[0].next_due = FictionalMinutes(9_999),
            // A row for a source nothing derived.
            |motion| {
                let mut row = motion.pressed[0].clone();
                row.source = PressureSource::Subject(row.target);
                motion.pressed.push(row);
            },
            // A fulfilment dropped on the floor.
            |motion| motion.fulfilled.clear(),
        ];
        for rewrite in rewrites {
            assert!(
                matches!(forge(rewrite), Err(JournalError::Corrupt(_))),
                "a forged motion replayed"
            );
        }
        let _ = subject;
    }

    /// `TickMinutes` owns its bound at decode, so a span outside
    /// `1..=MAX_ROUTE_COST` cannot be read in any position: a routine's period,
    /// a command, or a tick's own effect.
    #[test]
    fn a_span_outside_the_bound_cannot_be_decoded() {
        for minutes in [0u32, crate::patch::MAX_ROUTE_COST + 1, u32::MAX] {
            let raw = rmp_serde::to_vec(&minutes).unwrap();
            assert!(
                rmp_serde::from_slice::<crate::TickMinutes>(&raw).is_err(),
                "{minutes} decoded as a span"
            );
            assert!(serde_json::from_value::<crate::TickMinutes>(minutes.into()).is_err());
        }
        for minutes in [1u32, crate::patch::MAX_ROUTE_COST] {
            let raw = rmp_serde::to_vec(&minutes).unwrap();
            assert_eq!(
                rmp_serde::from_slice::<crate::TickMinutes>(&raw).unwrap().minutes(),
                minutes
            );
        }
    }

}
