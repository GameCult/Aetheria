//! The invocation pipeline: catalog lookup, role binding, precondition
//! evaluation, effect-ceiling checking, band selection, and lowering.
//!
//! One entry point, [`exercise`], reached from `reduce`'s `ExerciseDecision` arm
//! and from `apply_effect`'s `DecisionExercised` arm, so a forged effect is
//! re-derived by the same function that produced the honest one. Nothing else
//! draws a band, and the draw goes through the one `digest()` owner.

use super::patch::{
    self, Affordance, Audience, AudienceSpec, BoundPrecondition, Bounds, ComponentOpKind, Cost,
    Precondition, Quantity, RefKind, Role, WorldPatch,
};
use super::{
    AffordanceId, AuthorityGrant, AuthorityTarget, CommandId, DecisionEvent, DecisionInvocation,
    DecisionOpportunity, EdgeId, EntityId, EventId, FictionalMinutes, GrantedAffordance,
    KernelError, Magnitude, SubjectId, Target, WorldId, WorldState, digest,
};
use serde::Serialize;
use std::collections::{BTreeMap, BTreeSet};

/// The complete failure set of one affordance invocation. Sorted and complete
/// like `Mismatch`, and like it never serialized: it states the affordance
/// contract, which no commit records.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum ActionMismatch {
    UnboundRole {
        role: Role,
    },
    UnknownRole {
        role: Role,
    },
    DuplicateRoleBinding {
        role: Role,
    },
    UnknownTarget {
        role: Role,
    },
    TargetKindMismatch {
        role: Role,
        expected: RefKind,
        actual: RefKind,
    },
    ActorNotPresent {
        precondition: usize,
    },
    TargetUnreachable {
        precondition: usize,
    },
    InsufficientHolding {
        precondition: usize,
    },
    SlotNotProposed {
        slot: usize,
    },
    UnknownSlot {
        slot: usize,
    },
    DuplicateSlotProposal {
        slot: usize,
    },
    MagnitudeShapeMismatch {
        slot: usize,
    },
    MagnitudeOverCeiling {
        slot: usize,
    },
    ZeroMagnitude {
        slot: usize,
    },
    SpeechRequired,
    SpeechNotCarried,
    EmptySpeech,
    EmptyDisplay,
    /// The acting subject's effective authority covers no such target under
    /// this kind. A target that walked out of the jurisdiction while the
    /// proposal was in flight arrives here, not as a rebind: what the actor is
    /// authorized over is in the digest, the world it reaches is not.
    NotAuthorized {
        precondition: usize,
    },
    /// No forum takes this grievance, or its standing does not reach the actor.
    NoStanding {
        precondition: usize,
    },
    /// An invocation bound the reserved `actor` role.
    ActorRoleBound,
    /// A slot would grant authority the acting subject does not itself hold
    /// over that ground. Without this, any holder mints unlimited authority in
    /// one step and the whole component is decorative.
    DelegationNotMonotone {
        slot: usize,
    },
    /// The actor does not hold the bound fact at the required confidence. A
    /// re-appraisal that lowered its own confidence while the proposal was in
    /// flight arrives here rather than as a rebind: confidence is admission-only.
    FactUnknown {
        precondition: usize,
    },
    /// The actor has no audience at all: unplaced for co-location, or outside a
    /// channel it does not control.
    NoAudience {
        precondition: usize,
    },
    /// The addressed subject is not inside that audience. A target that walked
    /// out of the room while the proposal was in flight arrives here: the
    /// preimage holds what the actor *is*, not the world it reaches.
    CannotReach {
        precondition: usize,
    },
    /// The actor holds no commitment of that kind to the bound referent.
    NotCommitted {
        precondition: usize,
    },
}

/// The band draw's whole preimage. Five fields, none of them from the
/// invocation's payload: the proposer varies the attempt and cannot vary the
/// draw. `affordance` is the id carried by the kernel-verified grant, so the
/// draw cannot be steered by an affordance the scope does not hold.
#[derive(Serialize)]
struct BandPreimage {
    world_id: WorldId,
    revision: u64,
    command_id: CommandId,
    affordance: AffordanceId,
    band_count: usize,
}

/// Catalog lookup, binding, admission, draw, lowering, event. Stages 1 through 4
/// accumulate one complete rejection set and return it sorted before anything is
/// drawn or lowered, so nothing past the gate can fail on caller input.
///
/// The entry arrives as a [`GrantedAffordance`], which only `require_granted`
/// can build: the grant check is the type, not a call a third caller might
/// forget. Nothing here reads `invocation.affordance`.
pub(super) fn exercise(
    state: &WorldState,
    command_id: CommandId,
    current: &DecisionOpportunity,
    granted: &GrantedAffordance<'_>,
    invocation: &DecisionInvocation,
) -> Result<DecisionEvent, KernelError> {
    let entry = granted.entry;
    let actor = current.scope.subject_id;
    let mut rejections = Vec::new();

    let components = super::scope_components(state, actor);
    let authority = super::effective_authority(&components.authority, &components.delegated);
    let bindings = bind_roles(state, actor, entry, invocation, &mut rejections);
    check_preconditions(state, actor, &authority, entry, &bindings, &mut rejections);
    check_proposals(entry, invocation, &mut rejections);
    check_delegation(state, &authority, entry, &bindings, &mut rejections);

    if !rejections.is_empty() {
        rejections.sort();
        return Err(KernelError::ActionRejected(rejections));
    }

    let band = select_band(state, command_id, granted.id, entry)?;
    let operations = lower(entry, band, actor, state.now, invocation, &bindings)?;
    let mut effects = if operations.is_empty() {
        Vec::new()
    } else {
        // The same resolver, the same per-operation preconditions, the same
        // conservation equation the declaration lane passes.
        patch::resolve_patch(
            state,
            command_id,
            &WorldPatch {
                declarations: Vec::new(),
                operations,
                evidence: Vec::new(),
            },
            None,
            None,
        )
        .map_err(KernelError::PatchRejected)?
        .operations
    };

    // The speech lowering: kernel-owned, not a slot, and unreachable by any
    // proposer, band, or effect ceiling. The audience comes from the entry's one
    // speech precondition, so a world's `whisper` and `proclaim` lower through
    // the same two operations with a different audience and the kernel learns no
    // genre. Addressing does not narrow the fan-out: a voice fills its audience.
    let speech = match (&invocation.speech, entry.carries_speech) {
        (Some(statement), true) => {
            let fact = EntityId(patch::derive_id(
                patch::ENTITY_NAMESPACE,
                state.world_id,
                command_id,
                &patch::DraftHandle::new(SPEECH_HANDLE),
                Some(SPEECH_INDEX),
            ));
            let to = speech_audience(entry, &bindings)?;
            effects.splice(
                0..0,
                [
                    patch::ResolvedOp::AssertClaim {
                        fact,
                        statement: statement.clone(),
                        by: actor,
                    },
                    patch::ResolvedOp::Communicate {
                        speaker: actor,
                        fact,
                        to,
                    },
                ],
            );
            Some(fact)
        }
        _ => None,
    };
    // The display lowering: the same standing as speech, from the same
    // allocation site under the second discriminator. It needs no entry
    // field and reads no audience precondition: a body is visible to the room
    // whatever the entry addresses, so `proclaim` down a horn still shrugs at
    // whoever stands beside the speaker.
    let display = invocation.display.as_ref().map(|statement| {
        let fact = EntityId(patch::derive_id(
            patch::ENTITY_NAMESPACE,
            state.world_id,
            command_id,
            &patch::DraftHandle::new(SPEECH_HANDLE),
            Some(DISPLAY_INDEX),
        ));
        let at = usize::from(speech.is_some()) * 2;
        effects.splice(
            at..at,
            [
                patch::ResolvedOp::AssertClaim {
                    fact,
                    statement: statement.clone(),
                    by: actor,
                },
                patch::ResolvedOp::Display { actor, fact },
            ],
        );
        fact
    });

    let revision = super::resolution_revision(state)?;
    Ok(DecisionEvent {
        id: EventId::for_command(command_id),
        revision,
        scope: current.scope,
        controller_id: current.controller_id,
        affordance: granted.id,
        speech,
        display,
        band,
        effects,
    })
}

/// The synthetic handle every minted claim allocates under. It is synthesized
/// only in Active and declarations are Draft-only, so no world-declared handle
/// can collide with it.
const SPEECH_HANDLE: &str = "ghostlight.speech";

/// The speech index, passed as `derive_id`'s discriminator. It is `0` for every
/// invocation because one invocation carries one utterance; it exists so a
/// second minted claim needs no second allocation idiom.
const SPEECH_INDEX: &str = "0";

/// The display index: the second claim one invocation may mint, under the
/// same handle and allocation site as speech.
const DISPLAY_INDEX: &str = "1";

/// The audience a speech-carrying entry names. The declaration validator already
/// refused an entry with none (`SpeechWithoutAudience`) or two
/// (`AmbiguousSpeechAudience`), so anything else here is a corrupt catalog.
fn speech_audience(
    entry: &Affordance,
    bindings: &BTreeMap<Role, Target>,
) -> Result<Audience, KernelError> {
    entry
        .preconditions
        .iter()
        .find_map(|precondition| match precondition {
            Precondition::CanBroadcast { via } | Precondition::CanReach { via, .. } => {
                bound_audience(via, bindings)
            }
            _ => None,
        })
        .ok_or_else(|| KernelError::Invariant("a speech-carrying entry names no audience".into()))
}

/// Stage 2. The acting subject is never a declared role: the kernel binds the
/// reserved name `actor` to it and refuses an invocation that tries to. A
/// binding that happens to name the actor through a declared role is legal and
/// unremarkable.
fn bind_roles(
    state: &WorldState,
    actor: SubjectId,
    entry: &Affordance,
    invocation: &DecisionInvocation,
    rejections: &mut Vec<ActionMismatch>,
) -> BTreeMap<Role, Target> {
    let declared: BTreeMap<&Role, RefKind> = entry
        .roles
        .iter()
        .map(|spec| (&spec.role, spec.kind))
        .collect();
    let mut bound: BTreeMap<Role, Target> = BTreeMap::new();
    let mut seen: BTreeSet<&Role> = BTreeSet::new();
    for binding in &invocation.bindings {
        if binding.role.0 == patch::ACTOR_ROLE {
            rejections.push(ActionMismatch::ActorRoleBound);
            continue;
        }
        if !seen.insert(&binding.role) {
            rejections.push(ActionMismatch::DuplicateRoleBinding {
                role: binding.role.clone(),
            });
            continue;
        }
        let Some(expected) = declared.get(&binding.role).copied() else {
            rejections.push(ActionMismatch::UnknownRole {
                role: binding.role.clone(),
            });
            continue;
        };
        let Some(actual) = live_kind(state, binding.target) else {
            rejections.push(ActionMismatch::UnknownTarget {
                role: binding.role.clone(),
            });
            continue;
        };
        let fits = match (expected, actual) {
            (RefKind::Subject(None), RefKind::Subject(_)) => true,
            (expected, actual) => expected == actual,
        };
        if fits {
            bound.insert(binding.role.clone(), binding.target);
        } else {
            rejections.push(ActionMismatch::TargetKindMismatch {
                role: binding.role.clone(),
                expected,
                actual,
            });
        }
    }
    for spec in &entry.roles {
        if !seen.contains(&spec.role) {
            rejections.push(ActionMismatch::UnboundRole {
                role: spec.role.clone(),
            });
        }
    }
    // The kernel's own binding, added after the caller's so nothing can shadow
    // it: a slot that must land on the actor says `actor` and the proposer has
    // no say. Without it, a levy's `Transfer` would need the proposer to name
    // the payee, and an authorized collector could lawfully take a tax and send
    // it to a friend.
    bound.insert(Role(patch::ACTOR_ROLE.into()), Target::Subject(actor));
    bound
}

/// What a target actually is in committed state. `None` means the target keys no
/// live partition in its namespace.
fn live_kind(state: &WorldState, target: Target) -> Option<RefKind> {
    match target {
        Target::Subject(subject_id) => state
            .subjects
            .get(&subject_id)
            .map(|subject| RefKind::Subject(Some(subject.kind))),
        Target::Entity(entity_id) => state
            .entities
            .get(&entity_id)
            .map(|record| RefKind::Entity(record.kind)),
        Target::Edge(edge_id) => state
            .edges
            .get(&edge_id)
            .map(|_| RefKind::Edge(patch::EdgeKind::Route)),
    }
}

/// Stage 3, in declaration order, each failure carrying its index so a test can
/// name the exact precondition that refused. Role binding is this caller's job:
/// each declared `Precondition` is lowered through stage 2's bindings into a
/// `BoundPrecondition` and handed to the one evaluator. A precondition whose
/// role stage 2 left unbound is skipped here, because stage 2 already named it.
fn check_preconditions(
    state: &WorldState,
    actor: SubjectId,
    authority: &BTreeSet<AuthorityGrant>,
    entry: &Affordance,
    bindings: &BTreeMap<Role, Target>,
    rejections: &mut Vec<ActionMismatch>,
) {
    for (index, precondition) in entry.preconditions.iter().enumerate() {
        let Some(bound) = bind_precondition(precondition, bindings) else {
            continue;
        };
        if let Some(mismatch) = evaluate(state, actor, authority, index, &bound) {
            rejections.push(mismatch);
        }
    }
}

/// The catalog's role indirection lowered to canonical referents. `None` means
/// a role is unbound or bound to the wrong namespace, which stage 2 named.
fn bind_precondition(
    precondition: &Precondition,
    bindings: &BTreeMap<Role, Target>,
) -> Option<BoundPrecondition> {
    let place = |role: &Role| match bindings.get(role)? {
        Target::Entity(entity_id) => Some(*entity_id),
        _ => None,
    };
    Some(match precondition {
        Precondition::Present { at } => BoundPrecondition::Present { at: place(at)? },
        Precondition::Reachable { to, within } => BoundPrecondition::Reachable {
            to: place(to)?,
            within: *within,
        },
        Precondition::Holds { resource, at_least } => BoundPrecondition::Holds {
            resource: place(resource)?,
            at_least: *at_least,
        },
        Precondition::Authorized { over, kind } => BoundPrecondition::Authorized {
            over: bindings.get(over).copied()?,
            kind: kind.clone(),
        },
        Precondition::HasStanding { grievance } => BoundPrecondition::HasStanding {
            grievance: grievance.clone(),
        },
        Precondition::Knows { fact, at_least } => BoundPrecondition::Knows {
            fact: place(fact)?,
            at_least: *at_least,
        },
        Precondition::CanBroadcast { via } => BoundPrecondition::CanBroadcast {
            via: bound_audience(via, bindings)?,
        },
        Precondition::CanReach { subject, via } => BoundPrecondition::CanReach {
            subject: match bindings.get(subject)? {
                Target::Subject(subject_id) => *subject_id,
                _ => return None,
            },
            via: bound_audience(via, bindings)?,
        },
        Precondition::Committed { to, kind } => BoundPrecondition::Committed {
            to: match bindings.get(to)? {
                Target::Subject(subject_id) => *subject_id,
                _ => return None,
            },
            kind: *kind,
        },
    })
}

/// The one precondition reader, over referents that are already canonical.
/// Returns the complete set in declaration order; an empty set is "all hold".
/// The tick reads only its emptiness — an `ActionMismatch` never surfaces to
/// anyone from there.
pub(super) fn preconditions_hold(
    state: &WorldState,
    actor: SubjectId,
    checks: &[BoundPrecondition],
) -> Vec<ActionMismatch> {
    let components = super::scope_components(state, actor);
    let authority = super::effective_authority(&components.authority, &components.delegated);
    checks
        .iter()
        .enumerate()
        .filter_map(|(index, check)| evaluate(state, actor, &authority, index, check))
        .collect()
}

/// One check against committed state. `index` is the position within the slice
/// the caller passed in, which is what every `precondition: usize` means.
fn evaluate(
    state: &WorldState,
    actor: SubjectId,
    authority: &BTreeSet<AuthorityGrant>,
    index: usize,
    check: &BoundPrecondition,
) -> Option<ActionMismatch> {
    let holds = match check {
        BoundPrecondition::Present { at } => {
            return (state.positions.get(&actor).map(|position| position.place) != Some(*at))
                .then_some(ActionMismatch::ActorNotPresent {
                    precondition: index,
                });
        }
        BoundPrecondition::Reachable { to, within } => {
            return (!reachable(state, actor, authority, *to, *within)).then_some(
                ActionMismatch::TargetUnreachable {
                    precondition: index,
                },
            );
        }
        BoundPrecondition::Holds { resource, at_least } => {
            state
                .holdings
                .get(&actor)
                .and_then(|held| held.get(resource))
                .map_or(0, |quantity| quantity.0)
                >= at_least.0
        }
        BoundPrecondition::Authorized { over, kind } => {
            return (!authority
                .iter()
                .any(|grant| &grant.kind == kind && super::covers(state, grant.over, *over)))
            .then_some(ActionMismatch::NotAuthorized {
                precondition: index,
            });
        }
        BoundPrecondition::HasStanding { grievance } => {
            return (!state.redress.get(grievance).is_some_and(|forum| {
                super::covers(state, forum.standing, Target::Subject(actor))
            }))
            .then_some(ActionMismatch::NoStanding {
                precondition: index,
            });
        }
        BoundPrecondition::Knows { fact, at_least } => {
            return (!state
                .knowledge
                .get(&actor)
                .and_then(|held| held.get(fact))
                .is_some_and(|held| held.confidence >= *at_least))
            .then_some(ActionMismatch::FactUnknown {
                precondition: index,
            });
        }
        BoundPrecondition::CanBroadcast { via } => {
            return (!super::can_broadcast(state, actor, via)).then_some(
                ActionMismatch::NoAudience {
                    precondition: index,
                },
            );
        }
        BoundPrecondition::CanReach { subject, via } => {
            return (!super::audience(state, actor, via).contains(subject)).then_some(
                ActionMismatch::CannotReach {
                    precondition: index,
                },
            );
        }
        // A linear scan of one subject's own small map. No key lookup is needed
        // and none is offered, which is why a `CommitmentKey` never has to be
        // guessable.
        BoundPrecondition::Committed { to, kind } => {
            return (!state.commitments.get(&actor).is_some_and(|held| {
                held.values().any(|commitment| {
                    commitment.counterparty == Some(*to) && commitment.kind == *kind
                })
            }))
            .then_some(ActionMismatch::NotCommitted {
                precondition: index,
            });
        }
    };
    (!holds).then_some(ActionMismatch::InsufficientHolding {
        precondition: index,
    })
}

/// The catalog's role indirection lowered to the canonical audience: the entry
/// names a role, the invoker binds a channel to it, and every reader of reach
/// sees one shape. `None` means the role is unbound, which stage 2 already named.
fn bound_audience(via: &AudienceSpec, bindings: &BTreeMap<Role, Target>) -> Option<Audience> {
    match via {
        AudienceSpec::Colocated => Some(Audience::Colocated),
        AudienceSpec::Channel(role) => match bindings.get(role).copied() {
            Some(Target::Entity(entity_id)) => Some(Audience::Channel(entity_id)),
            _ => None,
        },
    }
}

/// Stage 4, beside the ceilings: an authority a slot would grant or revoke
/// must not exceed the actor's own. For a grant this is the monotonicity
/// envelope — no holder mints authority it does not itself hold. For a
/// revocation it is the same envelope read the other way: an actor may not
/// strip ground it holds no jurisdiction over, so revocation is not a lever
/// for reaching past one's own authority. Every slot is checked, not just the
/// drawn band's, for the same reason every ceiling is — the proposer commits
/// to a complete attempt and cannot see the band.
///
/// The rule is action-lane only, and that is not two truths. The patch lane's
/// `GrantAuthority`/`RevokeAuthority` is admitted by the world owner;
/// monotonicity *is* the action lane's authority envelope, and both lanes
/// reach one `apply_operation` with the identical mutation.
fn check_delegation(
    state: &WorldState,
    authority: &BTreeSet<AuthorityGrant>,
    entry: &Affordance,
    bindings: &BTreeMap<Role, Target>,
    rejections: &mut Vec<ActionMismatch>,
) {
    for (index, slot) in entry.effect_slots.iter().enumerate() {
        let kind = match &slot.op_kind {
            ComponentOpKind::GrantAuthority { kind }
            | ComponentOpKind::RevokeAuthority { kind } => kind,
            _ => continue,
        };
        let Some(over) = slot
            .roles
            .get(1)
            .and_then(|role| bindings.get(role))
            .copied()
            .and_then(authority_target_of)
        else {
            continue;
        };
        if !authority.iter().any(|grant| {
            &grant.kind == kind && super::covers(state, grant.over, over.as_referent())
        }) {
            rejections.push(ActionMismatch::DelegationNotMonotone { slot: index });
        }
    }
}

/// The authority target a bound referent names. A route is jurisdictional
/// ground but never a grant's target, so a slot bound to one lowers to nothing.
fn authority_target_of(target: Target) -> Option<AuthorityTarget> {
    match target {
        Target::Subject(subject_id) => Some(AuthorityTarget::Subject(subject_id)),
        Target::Entity(entity_id) => Some(AuthorityTarget::PlaceSubtree(entity_id)),
        Target::Edge(_) => None,
    }
}

/// Dijkstra over the live route graph, admitting the open routes this subject
/// may traverse — the same `route_admits` rule the resolver applies to a
/// `Relocate`, so a subject with the right authority can plan a path through
/// its own restricted ground — and
/// relaxing in `EdgeId` order, pruning any path whose accumulated cost exceeds
/// `within`. Cost 0 — already standing there — succeeds; `Present` is the strict
/// form. An unplaced actor reaches nothing.
///
/// This reads routes the scope digest does not cover, which is deliberate: the
/// alternative binds a proposal to a transitively large slice of world topology
/// and rejects it whenever anything near the actor commits. Admission runs at
/// commit against live state instead, so a route that closed while the proposal
/// was in flight makes this fail rather than letting a stale path commit.
fn reachable(
    state: &WorldState,
    actor: SubjectId,
    authority: &BTreeSet<AuthorityGrant>,
    destination: EntityId,
    within: Cost,
) -> bool {
    let Some(origin) = state.positions.get(&actor).map(|position| position.place) else {
        return false;
    };
    let mut best: BTreeMap<EntityId, u64> = BTreeMap::from([(origin, 0)]);
    let mut settled: BTreeSet<EntityId> = BTreeSet::new();
    let ceiling = u64::from(within.0);
    loop {
        let Some((place, cost)) = best
            .iter()
            .filter(|(place, _)| !settled.contains(place))
            .min_by_key(|(place, cost)| (**cost, **place))
            .map(|(place, cost)| (*place, *cost))
        else {
            return false;
        };
        if place == destination {
            return true;
        }
        settled.insert(place);
        let outbound: Vec<(EdgeId, EntityId, u64)> = state
            .edges
            .iter()
            .filter(|(_, record)| record.is_open())
            .filter_map(|(edge_id, record)| {
                let (from, to) = record.endpoints();
                let next = if from == place {
                    to
                } else if to == place {
                    from
                } else {
                    return None;
                };
                patch::route_admits(state, authority, record.access(), next)
                    .then(|| (*edge_id, next, u64::from(record.cost().0)))
            })
            .collect();
        for (_, next, step) in outbound {
            let total = cost + step;
            if total <= ceiling && best.get(&next).is_none_or(|known| total < *known) {
                best.insert(next, total);
            }
        }
    }
}

/// Stage 4. Every slot is checked, not just the ones the selected band names:
/// the proposer cannot see the band, so it commits to a complete attempt, and
/// checking every slot makes the ceiling total.
fn check_proposals(
    entry: &Affordance,
    invocation: &DecisionInvocation,
    rejections: &mut Vec<ActionMismatch>,
) {
    let mut seen: BTreeSet<usize> = BTreeSet::new();
    for proposed in &invocation.proposed {
        if !seen.insert(proposed.slot) {
            rejections.push(ActionMismatch::DuplicateSlotProposal {
                slot: proposed.slot,
            });
            continue;
        }
        let Some(slot) = entry.effect_slots.get(proposed.slot) else {
            rejections.push(ActionMismatch::UnknownSlot {
                slot: proposed.slot,
            });
            continue;
        };
        match (slot.bounds, proposed.magnitude) {
            (Bounds::None, Magnitude::None) => {}
            (Bounds::Quantity(ceiling), Magnitude::Quantity(value)) => {
                if value.0 == 0 {
                    rejections.push(ActionMismatch::ZeroMagnitude {
                        slot: proposed.slot,
                    });
                } else if value.0 > ceiling.0 {
                    rejections.push(ActionMismatch::MagnitudeOverCeiling {
                        slot: proposed.slot,
                    });
                }
            }
            (Bounds::Cost(ceiling), Magnitude::Cost(value)) => {
                if value.0 == 0 {
                    rejections.push(ActionMismatch::ZeroMagnitude {
                        slot: proposed.slot,
                    });
                } else if value.0 > ceiling.0 {
                    rejections.push(ActionMismatch::MagnitudeOverCeiling {
                        slot: proposed.slot,
                    });
                }
            }
            _ => rejections.push(ActionMismatch::MagnitudeShapeMismatch {
                slot: proposed.slot,
            }),
        }
    }
    for slot in 0..entry.effect_slots.len() {
        if !seen.contains(&slot) {
            rejections.push(ActionMismatch::SlotNotProposed { slot });
        }
    }
    // `Statement` is serde-transparent, so a value that arrived through
    // deserialization rather than through the constructor is re-checked here
    // rather than trusted.
    match (&invocation.speech, entry.carries_speech) {
        // A speech-carrying entry addresses someone; a silent turn that only
        // displays still addresses the room, so it is not `SpeechRequired`.
        (None, true) if invocation.display.is_none() => {
            rejections.push(ActionMismatch::SpeechRequired);
        }
        (Some(_), false) => rejections.push(ActionMismatch::SpeechNotCarried),
        (Some(speech), true) if !patch::is_canonical_text(speech.as_str()) => {
            rejections.push(ActionMismatch::EmptySpeech);
        }
        _ => {}
    }
    if invocation
        .display
        .as_ref()
        .is_some_and(|display| !patch::is_canonical_text(display.as_str()))
    {
        rejections.push(ActionMismatch::EmptyDisplay);
    }
}

/// The only entropy in the kernel: one digest, one modulo, one cumulative walk.
/// Modulo rather than rejection sampling — the bias is bounded by
/// `total_weight / 2^64`, and a rejection loop would need its counter in the
/// preimage, which is a second entropy shape for a rounding error nobody can
/// measure.
fn select_band(
    state: &WorldState,
    command_id: CommandId,
    affordance: AffordanceId,
    entry: &Affordance,
) -> Result<usize, KernelError> {
    let draw = digest(&BandPreimage {
        world_id: state.world_id,
        revision: state.revision,
        command_id,
        affordance,
        band_count: entry.outcome_bands.len(),
    })?;
    let head = draw
        .strip_prefix("sha256:")
        .and_then(|hex| hex.get(..16))
        .and_then(|head| u64::from_str_radix(head, 16).ok())
        .ok_or_else(|| KernelError::Invariant("band draw digest is not hex".into()))?;
    let total: u128 = entry
        .outcome_bands
        .iter()
        .map(|band| u128::from(band.weight))
        .sum();
    if total == 0 {
        return Err(KernelError::Invariant(
            "affordance has no outcome band".into(),
        ));
    }
    let mut position = u128::from(head) % total;
    for (index, band) in entry.outcome_bands.iter().enumerate() {
        let weight = u128::from(band.weight);
        if position < weight {
            return Ok(index);
        }
        position -= weight;
    }
    Err(KernelError::Invariant(
        "band weights did not cover the draw".into(),
    ))
}

/// Stage 7. One arm per `ComponentOpKind`, every referent read from the slot's
/// roles resolved through the bindings, every magnitude from the proposal for
/// that slot. An empty band lowers to nothing.
fn lower(
    entry: &Affordance,
    band: usize,
    actor: SubjectId,
    now: FictionalMinutes,
    invocation: &DecisionInvocation,
    bindings: &BTreeMap<Role, Target>,
) -> Result<Vec<patch::ComponentOp>, KernelError> {
    let malformed = || KernelError::Invariant("admitted affordance slot is malformed".into());
    let band = entry
        .outcome_bands
        .get(band)
        .ok_or_else(|| KernelError::Invariant("selected band does not exist".into()))?;
    let mut operations = Vec::new();
    for slot_index in &band.effects {
        let slot = entry.effect_slots.get(*slot_index).ok_or_else(malformed)?;
        let magnitude = invocation
            .proposed
            .iter()
            .find(|proposed| proposed.slot == *slot_index)
            .map(|proposed| proposed.magnitude)
            .ok_or_else(malformed)?;
        let target = |position: usize| -> Result<Target, KernelError> {
            slot.roles
                .get(position)
                .and_then(|role| bindings.get(role))
                .copied()
                .ok_or_else(malformed)
        };
        let subject = |position: usize| -> Result<patch::Ref<SubjectId>, KernelError> {
            match target(position)? {
                Target::Subject(subject_id) => Ok(patch::Ref::Existing(subject_id)),
                _ => Err(malformed()),
            }
        };
        let entity = |position: usize| -> Result<patch::Ref<EntityId>, KernelError> {
            match target(position)? {
                Target::Entity(entity_id) => Ok(patch::Ref::Existing(entity_id)),
                _ => Err(malformed()),
            }
        };
        let edge = |position: usize| -> Result<patch::Ref<EdgeId>, KernelError> {
            match target(position)? {
                Target::Edge(edge_id) => Ok(patch::Ref::Existing(edge_id)),
                _ => Err(malformed()),
            }
        };
        let quantity = || -> Result<Quantity, KernelError> {
            match magnitude {
                Magnitude::Quantity(value) => Ok(value),
                _ => Err(malformed()),
            }
        };
        let dependency = |position: usize| -> Result<patch::DependencyRef, KernelError> {
            Ok(match target(position)? {
                Target::Subject(subject_id) => {
                    patch::DependencyRef::Subject(patch::Ref::Existing(subject_id))
                }
                Target::Entity(entity_id) => {
                    patch::DependencyRef::Resource(patch::Ref::Existing(entity_id))
                }
                Target::Edge(edge_id) => patch::DependencyRef::Route(patch::Ref::Existing(edge_id)),
            })
        };
        let grant_over = |position: usize| -> Result<patch::AuthorityTargetRef, KernelError> {
            Ok(match target(position)? {
                Target::Subject(subject_id) => {
                    patch::AuthorityTargetRef::Subject(patch::Ref::Existing(subject_id))
                }
                Target::Entity(entity_id) => {
                    patch::AuthorityTargetRef::PlaceSubtree(patch::Ref::Existing(entity_id))
                }
                Target::Edge(_) => return Err(malformed()),
            })
        };
        operations.push(match &slot.op_kind {
            // `due` is computed here, so a proposer cannot set one, and the
            // pressure source is the acting subject, so it cannot be forged.
            ComponentOpKind::CreateCommitment {
                kind,
                horizon,
                period,
            } => patch::ComponentOp::CreateCommitment {
                subject: subject(0)?,
                counterparty: Some(subject(1)?),
                kind: *kind,
                due: now.checked_add(*horizon).ok_or_else(malformed)?,
                period: *period,
                checks: Vec::new(),
                // The world named the action; that name is what was promised.
                // A proposer cannot supply the text, so it cannot promise more
                // than the affordance carries.
                statement: patch::Statement::new(format!("a promise made through {}", entry.kind.0))
                    .ok_or_else(malformed)?,
            },
            ComponentOpKind::AdvancePressure { by } => patch::ComponentOp::AdvancePressure {
                source: patch::PressureSourceRef::Subject(patch::Ref::Existing(actor)),
                target: subject(0)?,
                by: *by,
            },
            ComponentOpKind::ReducePressure { by } => patch::ComponentOp::ReducePressure {
                source: patch::PressureSourceRef::Subject(patch::Ref::Existing(actor)),
                target: subject(0)?,
                by: *by,
            },
            ComponentOpKind::ResolvePressure => patch::ComponentOp::ResolvePressure {
                source: patch::PressureSourceRef::Subject(patch::Ref::Existing(actor)),
                target: subject(0)?,
            },
            ComponentOpKind::Relocate => patch::ComponentOp::Relocate {
                subject: subject(0)?,
                via: edge(1)?,
            },
            ComponentOpKind::OpenRoute => patch::ComponentOp::OpenRoute { route: edge(0)? },
            ComponentOpKind::CloseRoute => patch::ComponentOp::CloseRoute { route: edge(0)? },
            ComponentOpKind::AlterCost => patch::ComponentOp::AlterCost {
                route: edge(0)?,
                cost: match magnitude {
                    Magnitude::Cost(value) => value,
                    _ => return Err(malformed()),
                },
            },
            ComponentOpKind::Transfer => patch::ComponentOp::Transfer {
                from: subject(0)?,
                to: subject(1)?,
                resource: entity(2)?,
                qty: quantity()?,
            },
            ComponentOpKind::Transform => patch::ComponentOp::Transform {
                holder: subject(0)?,
                from_resource: entity(1)?,
                into_resource: entity(2)?,
                qty: quantity()?,
            },
            ComponentOpKind::Consume => patch::ComponentOp::Consume {
                holder: subject(0)?,
                resource: entity(1)?,
                qty: quantity()?,
            },
            ComponentOpKind::Bind => patch::ComponentOp::Bind {
                subject: subject(0)?,
                target: dependency(1)?,
            },
            ComponentOpKind::Release => patch::ComponentOp::Release {
                subject: subject(0)?,
                target: dependency(1)?,
            },
            ComponentOpKind::GrantAuthority { kind } => patch::ComponentOp::GrantAuthority {
                holder: subject(0)?,
                grant: patch::AuthorityGrantRef {
                    kind: kind.clone(),
                    over: grant_over(1)?,
                },
            },
            ComponentOpKind::RevokeAuthority { kind } => patch::ComponentOp::RevokeAuthority {
                holder: subject(0)?,
                grant: patch::AuthorityGrantRef {
                    kind: kind.clone(),
                    over: grant_over(1)?,
                },
            },
            ComponentOpKind::InstallIncumbent { office } => patch::ComponentOp::InstallIncumbent {
                institution: subject(0)?,
                office: office.clone(),
                incumbent: subject(1)?,
            },
            ComponentOpKind::VacateOffice { office } => patch::ComponentOp::VacateOffice {
                institution: subject(0)?,
                office: office.clone(),
            },
            ComponentOpKind::AcquireKnowledge { confidence } => {
                patch::ComponentOp::AcquireKnowledge {
                    subject: subject(0)?,
                    fact: entity(1)?,
                    source: patch::AuthoredSource::Witnessed,
                    confidence: *confidence,
                }
            }
            ComponentOpKind::Witness { confidence } => patch::ComponentOp::Witness {
                fact: entity(0)?,
                place: entity(1)?,
                confidence: *confidence,
            },
            ComponentOpKind::Forget => patch::ComponentOp::Forget {
                subject: subject(0)?,
                fact: entity(1)?,
            },
        });
    }
    Ok(operations)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::custody_tests::custody_kernel;
    use crate::patch::{
        AffordanceDeclaration, ComponentOp, DraftHandle, OutcomeBand, Ref as PatchRef, ResolvedOp,
        RoleSpec,
    };
    use crate::tests::{
        ADMIT_KIND, COMMAND_KIND, Civic, LEVY_KIND, OPENING_BALANCE, SEIZURE_GRIEVANCE, Topology,
        WARDEN_OFFICE, activate, affordance_named, auth_principal, authority_kind, civic_world,
        command, custody_world, grant_to, grievance, office, operations, opportunity_for,
        over_place, over_subject, human_principal, reject_owner, submit_owner,
    };
    use crate::{
        AffordanceKindName, AuthenticatedCaller, AuthoredSource, CallerId, CommandBody, Confidence,
        Declaration, EffectSlot, EntityKind, ProposedEffect, RoleBinding, Statement, SubmitReceipt,
        WorldEffect, WorldKernel, WorldPatch, WorldSnapshot, apply_effect,
    };
    use std::collections::BTreeSet;

    struct Bench {
        kernel: WorldKernel,
        active: WorldSnapshot,
        carry: AffordanceId,
        clerk: SubjectId,
        keeper: SubjectId,
        tithe: EntityId,
        ingot: EntityId,
        yard: EntityId,
        gate: EntityId,
        shutter: EdgeId,
    }

    fn bench(directory: &std::path::Path, title: &str) -> Bench {
        let mut kernel = custody_kernel(directory, title);
        let (topology, custody, active) = custody_world(&mut kernel);
        let carry = affordance_named(&active, "carry");
        Bench {
            kernel,
            active,
            carry,
            clerk: custody.holder,
            keeper: custody.counterparty,
            tithe: custody.tithe,
            ingot: custody.ingot,
            yard: topology.yard,
            gate: topology.gate,
            shutter: topology.shutter,
        }
    }

    /// `exercise` only accepts a grant the kernel resolved, so a test walks the
    /// same gate `reduce` does rather than reaching into the catalog.
    fn draw_once(
        state: &WorldState,
        command_id: CommandId,
        opportunity: &DecisionOpportunity,
        invocation: &DecisionInvocation,
    ) -> Result<DecisionEvent, KernelError> {
        let granted = crate::require_granted(state, opportunity, invocation.affordance)?;
        exercise(state, command_id, opportunity, &granted, invocation)
    }

    fn binding(role: &str, target: Target) -> RoleBinding {
        RoleBinding {
            role: Role(role.into()),
            target,
        }
    }

    impl Bench {
        /// A Carry the clerk proposes. The actor is never a role, so a Carry
        /// that moves the actor's own goods binds `from` to its own id.
        fn carry_of(&self, qty: u64, place: EntityId, resource: EntityId) -> DecisionInvocation {
            DecisionInvocation {
                affordance: self.carry,
                bindings: vec![
                    binding("from", Target::Subject(self.clerk)),
                    binding("recipient", Target::Subject(self.keeper)),
                    binding("place", Target::Entity(place)),
                    binding("resource", Target::Entity(resource)),
                ],
                proposed: vec![ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::Quantity(Quantity(qty)),
                }],
                speech: None,
                display: None,
            }
        }

        fn carry(&self, qty: u64, place: EntityId) -> DecisionInvocation {
            self.carry_of(qty, place, self.tithe)
        }

        fn clerk_opportunity(&self) -> DecisionOpportunity {
            opportunity_for(&self.active, self.clerk)
        }

        fn try_exercise(
            &self,
            invocation: &DecisionInvocation,
        ) -> Result<DecisionEvent, KernelError> {
            let opportunity = self.clerk_opportunity();
            draw_once(
                &self.kernel.state,
                CommandId::issue(),
                &opportunity,
                invocation,
            )
        }

        fn rejections(&self, invocation: &DecisionInvocation) -> Vec<ActionMismatch> {
            match self.try_exercise(invocation) {
                Err(KernelError::ActionRejected(rejected)) => rejected,
                other => panic!("expected an action rejection, got {other:?}"),
            }
        }

        fn commit(&mut self, invocation: DecisionInvocation) -> SubmitReceipt {
            let opportunity = self.clerk_opportunity();
            let caller = CallerId::Controller(opportunity.controller_id);
            self.kernel
                .submit(
                    command(
                        &self.active,
                        CommandId::new(),
                        caller.clone(),
                        CommandBody::ExerciseDecision {
                            opportunity,
                            invocation,
                        },
                    ),
                    &AuthenticatedCaller::fixture(caller),
                )
                .expect("the invocation commits")
        }

        fn held(&self, holder: SubjectId) -> u64 {
            self.kernel
                .state
                .holdings
                .get(&holder)
                .and_then(|held| held.get(&self.tithe))
                .map_or(0, |quantity| quantity.0)
        }

        /// The same Carry attempt against a sibling entry: one dial moved, one
        /// name. The catalog is Draft-only, so a variant is declared rather
        /// than patched into committed state.
        fn variant(&self, invocation: &DecisionInvocation, kind: &str) -> DecisionInvocation {
            DecisionInvocation {
                affordance: affordance_named(&self.active, kind),
                ..invocation.clone()
            }
        }

        fn refresh(&mut self) {
            self.active = self.kernel.snapshot().unwrap();
        }
    }

    #[test]
    fn a_missing_present_precondition_names_itself() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Present");
        let before = bench.kernel.state.clone();

        // The clerk stands in the yard, so a Carry bound to the gate fails the
        // first precondition and nothing else.
        assert_eq!(
            bench.rejections(&bench.carry(1, bench.gate)),
            vec![ActionMismatch::ActorNotPresent { precondition: 0 }]
        );
        assert_eq!(bench.kernel.state.revision, before.revision);
        assert_eq!(bench.kernel.state.holdings, before.holdings);
        assert_eq!(bench.kernel.state.events, before.events);
    }

    #[test]
    fn spending_more_than_held_fails_the_holds_precondition() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Holds");
        let greedy = bench.variant(&bench.carry(1, bench.yard), "carry_greedy");
        assert_eq!(
            bench.rejections(&greedy),
            vec![ActionMismatch::InsufficientHolding { precondition: 1 }]
        );

        // Absence is zero, so a resource the clerk holds none of gives the same
        // name at the boundary rather than a second one.
        let ingot = bench.variant(&bench.carry_of(1, bench.yard, bench.ingot), "carry_holds");
        assert_eq!(
            bench.rejections(&ingot),
            vec![ActionMismatch::InsufficientHolding { precondition: 0 }]
        );
    }

    #[test]
    fn a_reachable_precondition_reads_only_open_public_routes() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = bench(directory.path(), "Reachable");
        // Yard to gate over open public routes is the ramp (12) plus the span
        // (7): the shutter is closed and the toll stair is restricted. A budget
        // of NEAR_REACH covers only the shutter, FAR_REACH covers the two-hop
        // path.
        let attempt = bench.carry(1, bench.gate);
        let near = bench.variant(&attempt, "carry_near");
        let far = bench.variant(&attempt, "carry_far");
        assert_eq!(
            bench.rejections(&near),
            vec![ActionMismatch::TargetUnreachable { precondition: 0 }]
        );
        assert!(bench.try_exercise(&far).is_ok());

        // Opening the one-hop shutter shortens the path, and closing it again
        // restores the two-hop cost: the check reads the live graph rather than
        // anything the proposal was bound against.
        let before = bench.kernel.snapshot().unwrap();
        submit_owner(
            &mut bench.kernel,
            &before,
            operations(vec![ComponentOp::OpenRoute {
                route: PatchRef::Existing(bench.shutter),
            }]),
        );
        bench.refresh();
        assert!(bench.try_exercise(&near).is_ok());

        let before = bench.kernel.snapshot().unwrap();
        submit_owner(
            &mut bench.kernel,
            &before,
            operations(vec![ComponentOp::CloseRoute {
                route: PatchRef::Existing(bench.shutter),
            }]),
        );
        bench.refresh();
        assert_eq!(
            bench.rejections(&near),
            vec![ActionMismatch::TargetUnreachable { precondition: 0 }]
        );
    }

    #[test]
    fn an_unbound_or_miskinded_role_is_rejected_with_the_complete_set() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Roles");
        let mut invocation = bench.carry(1, bench.yard);
        // Drop `recipient`, add an undeclared role, and bind `resource` to a
        // place: one rejection carrying all three, sorted.
        invocation.bindings = vec![
            binding("from", Target::Subject(bench.clerk)),
            binding("place", Target::Entity(bench.yard)),
            binding("resource", Target::Entity(bench.yard)),
            binding("porter", Target::Subject(bench.keeper)),
        ];
        let mut expected = vec![
            ActionMismatch::UnboundRole {
                role: Role("recipient".into()),
            },
            ActionMismatch::UnknownRole {
                role: Role("porter".into()),
            },
            ActionMismatch::TargetKindMismatch {
                role: Role("resource".into()),
                expected: RefKind::Entity(EntityKind::Resource),
                actual: RefKind::Entity(EntityKind::Place),
            },
        ];
        expected.sort();
        assert_eq!(bench.rejections(&invocation), expected);
    }

    #[test]
    fn a_duplicated_or_unknown_binding_target_is_rejected() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Bindings");
        let mut twice = bench.carry(1, bench.yard);
        twice
            .bindings
            .push(binding("resource", Target::Entity(bench.tithe)));
        assert!(
            bench
                .rejections(&twice)
                .contains(&ActionMismatch::DuplicateRoleBinding {
                    role: Role("resource".into())
                })
        );

        let mut unknown = bench.carry(1, bench.yard);
        unknown.bindings[3] = binding("resource", Target::Entity(EntityId::issue()));
        assert!(
            bench
                .rejections(&unknown)
                .contains(&ActionMismatch::UnknownTarget {
                    role: Role("resource".into())
                })
        );
    }

    #[test]
    fn a_magnitude_over_its_slot_ceiling_is_rejected_and_within_it_commits() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = bench(directory.path(), "Ceiling");
        assert_eq!(
            bench.rejections(&bench.carry(4, bench.yard)),
            vec![ActionMismatch::MagnitudeOverCeiling { slot: 0 }]
        );

        let clerk_before = bench.held(bench.clerk);
        let keeper_before = bench.held(bench.keeper);
        let receipt = bench.commit(bench.carry(3, bench.yard));
        assert!(matches!(receipt, SubmitReceipt::Applied(_)));
        assert_eq!(bench.held(bench.clerk), clerk_before - 3);
        assert_eq!(bench.held(bench.keeper), keeper_before + 3);
        assert_eq!(
            bench.held(bench.clerk) + bench.held(bench.keeper),
            clerk_before + keeper_before,
            "the conserved total is unchanged"
        );
    }

    #[test]
    fn a_magnitude_of_the_wrong_shape_or_zero_is_rejected() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Shape");
        for magnitude in [Magnitude::Cost(Cost(2)), Magnitude::None] {
            let mut invocation = bench.carry(1, bench.yard);
            invocation.proposed[0].magnitude = magnitude;
            assert_eq!(
                bench.rejections(&invocation),
                vec![ActionMismatch::MagnitudeShapeMismatch { slot: 0 }]
            );
        }
        assert_eq!(
            bench.rejections(&bench.carry(0, bench.yard)),
            vec![ActionMismatch::ZeroMagnitude { slot: 0 }]
        );
    }

    #[test]
    fn every_slot_must_be_proposed_exactly_once() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Slots");
        let mut missing = bench.carry(1, bench.yard);
        missing.proposed.clear();
        assert_eq!(
            bench.rejections(&missing),
            vec![ActionMismatch::SlotNotProposed { slot: 0 }]
        );

        let mut twice = bench.carry(1, bench.yard);
        twice.proposed.push(twice.proposed[0]);
        assert_eq!(
            bench.rejections(&twice),
            vec![ActionMismatch::DuplicateSlotProposal { slot: 0 }]
        );

        let mut unknown = bench.carry(1, bench.yard);
        unknown.proposed.push(ProposedEffect {
            slot: 7,
            magnitude: Magnitude::None,
        });
        assert_eq!(
            bench.rejections(&unknown),
            vec![ActionMismatch::UnknownSlot { slot: 7 }]
        );
    }

    #[test]
    fn speech_is_required_exactly_when_the_entry_carries_it() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Speech");
        let mut spoken_carry = bench.carry(1, bench.yard);
        spoken_carry.speech = Some(Statement::new("Take it.").unwrap());
        assert_eq!(
            bench.rejections(&spoken_carry),
            vec![ActionMismatch::SpeechNotCarried]
        );

        let speak = affordance_named(&bench.active, "speak");
        let mut silent_speak = DecisionInvocation {
            affordance: speak,
            bindings: Vec::new(),
            proposed: Vec::new(),
            speech: None,
            display: None,
        };
        assert_eq!(
            bench.rejections(&silent_speak),
            vec![ActionMismatch::SpeechRequired]
        );

        // `Statement` is serde-transparent, so a value that arrived without the
        // constructor is re-checked here rather than trusted.
        silent_speak.speech = Some(serde_json::from_str::<Statement>("\"   \"").unwrap());
        assert_eq!(
            bench.rejections(&silent_speak),
            vec![ActionMismatch::EmptySpeech]
        );
    }

    #[test]
    fn the_same_command_id_always_selects_the_same_band() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "Bands");
        // Three equally weighted bands, so the draw alone decides.
        let opportunity = bench.clerk_opportunity();
        let invocation = bench.variant(&bench.carry(1, bench.yard), "carry_chance");
        let draw = |command_id: CommandId, invocation: &DecisionInvocation| {
            draw_once(&bench.kernel.state, command_id, &opportunity, invocation)
                .expect("the invocation is admissible")
                .band
        };

        let command_id = CommandId::issue();
        let fixed = draw(command_id, &invocation);
        for _ in 0..100 {
            assert_eq!(draw(command_id, &invocation), fixed);
        }

        // No caller-supplied value influences the draw: not a binding order,
        // not a magnitude.
        let mut reordered = invocation.clone();
        reordered.bindings.reverse();
        assert_eq!(draw(command_id, &reordered), fixed);
        let mut heavier = invocation.clone();
        heavier.proposed[0].magnitude = Magnitude::Quantity(Quantity(3));
        assert_eq!(draw(command_id, &heavier), fixed);

        // The command id is the only varying term at one revision.
        let mut seen = BTreeSet::new();
        for _ in 0..64 {
            seen.insert(draw(CommandId::issue(), &invocation));
        }
        assert!(
            seen.len() > 1,
            "varying only the command id must reach more than one band"
        );
    }

    #[test]
    fn a_zero_effect_band_commits_nothing_but_the_event() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = bench(directory.path(), "EmptyBand");
        let idle = bench.variant(&bench.carry(3, bench.yard), "carry_idle");

        let holdings = bench.kernel.state.holdings.clone();
        let positions = bench.kernel.state.positions.clone();
        let edges = bench.kernel.state.edges.clone();
        let revision = bench.kernel.state.revision;

        let event = bench
            .try_exercise(&idle)
            .expect("an empty band is admissible");
        assert_eq!(event.band, 0);
        assert!(event.effects.is_empty());

        let receipt = bench.commit(idle);
        assert!(matches!(receipt, SubmitReceipt::Applied(_)));
        assert_eq!(bench.kernel.state.holdings, holdings);
        assert_eq!(bench.kernel.state.positions, positions);
        assert_eq!(bench.kernel.state.edges, edges);
        assert_eq!(bench.kernel.state.events.len(), 1);
        assert_eq!(bench.kernel.state.revision, revision + 1);
    }

    #[test]
    fn a_committed_event_carries_the_exact_lowered_operations() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = bench(directory.path(), "Lowering");
        let receipt = bench.commit(bench.carry(2, bench.yard));
        assert!(matches!(receipt, SubmitReceipt::Applied(_)));
        let event = bench
            .kernel
            .state
            .events
            .last()
            .expect("the committed event");
        assert_eq!(
            event.effects,
            vec![ResolvedOp::Transfer {
                from: bench.clerk,
                to: bench.keeper,
                resource: bench.tithe,
                qty: Quantity(2),
            }]
        );
    }

    /// Soul: contract 10's discriminating case. `a_zero_effect_band_commits_
    /// nothing_but_the_event` proves an empty band commits nothing, and
    /// `a_committed_event_carries_the_exact_lowered_operations` proves a
    /// one-slot entry lowers what it declares — neither separates a slot the
    /// proposer offered from a slot the draw admitted. This entry declares two
    /// slots on disjoint bands and proposes both within their ceilings: only
    /// the drawn band's slot may appear in `event.effects`, and the other slot
    /// must move no partition. Both bands are exercised, so neither branch
    /// passes by never being taken.
    #[test]
    fn soul_only_the_selected_bands_effects_are_committed() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = bench(directory.path(), "SelectedBand");
        let split_id = affordance_named(&bench.active, "carry_split");
        let split = DecisionInvocation {
            affordance: split_id,
            bindings: vec![
                binding("from", Target::Subject(bench.clerk)),
                binding("recipient", Target::Subject(bench.keeper)),
                binding("place", Target::Entity(bench.yard)),
                binding("resource", Target::Entity(bench.tithe)),
            ],
            proposed: vec![
                ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::Quantity(Quantity(2)),
                },
                ProposedEffect {
                    slot: 1,
                    magnitude: Magnitude::Quantity(Quantity(1)),
                },
            ],
            speech: None,
            display: None,
        };
        let mut seen: BTreeSet<usize> = BTreeSet::new();

        for wanted in [0usize, 1] {
            // The draw is a pure function of the pre-commit revision and the
            // command id, so the command id that reaches a band is findable
            // before the envelope is built rather than hunted by retrying.
            let opportunity = bench.clerk_opportunity();
            let command_id = (0..256)
                .map(|_| CommandId::issue())
                .find(|candidate| {
                    draw_once(&bench.kernel.state, *candidate, &opportunity, &split)
                        .expect("the invocation is admissible")
                        .band
                        == wanted
                })
                .expect("a two-band entry reaches both bands over 256 command ids");

            let before_clerk = bench.held(bench.clerk);
            let before_keeper = bench.held(bench.keeper);
            let caller = CallerId::Controller(opportunity.controller_id);
            let receipt = bench
                .kernel
                .submit(
                    command(
                        &bench.active,
                        command_id,
                        caller.clone(),
                        CommandBody::ExerciseDecision {
                            opportunity,
                            invocation: split.clone(),
                        },
                    ),
                    &AuthenticatedCaller::fixture(caller),
                )
                .expect("the invocation commits");
            assert!(matches!(receipt, SubmitReceipt::Applied(_)));
            bench.refresh();
            let event = bench
                .kernel
                .state
                .events
                .last()
                .expect("the committed event")
                .clone();
            assert_eq!(event.band, wanted);
            seen.insert(event.band);
            match event.band {
                0 => {
                    assert_eq!(
                        event.effects,
                        vec![ResolvedOp::Transfer {
                            from: bench.clerk,
                            to: bench.keeper,
                            resource: bench.tithe,
                            qty: Quantity(2),
                        }]
                    );
                    assert_eq!(bench.held(bench.clerk), before_clerk - 2);
                    assert_eq!(bench.held(bench.keeper), before_keeper + 2);
                }
                1 => {
                    assert_eq!(
                        event.effects,
                        vec![ResolvedOp::Consume {
                            holder: bench.clerk,
                            resource: bench.tithe,
                            qty: Quantity(1),
                        }]
                    );
                    assert_eq!(bench.held(bench.clerk), before_clerk - 1);
                    assert_eq!(bench.held(bench.keeper), before_keeper);
                }
                other => panic!("the entry declares two bands, drew {other}"),
            }
        }

        assert_eq!(
            seen.len(),
            2,
            "both declared bands must be reachable across command ids"
        );
    }

    /// Soul: `RoleSpec` persists inside the catalog, so `RefKind` had to become
    /// serializable this pass. An internally tagged newtype over an `Option`
    /// does not serialize; this one is adjacently tagged, and a stored
    /// `Subject(None)` role is what would fault on first write if that were
    /// wrong. The fixture catalog carries four such roles.
    #[test]
    fn soul_a_stored_role_spec_over_an_optional_subject_kind_round_trips() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "OptionalSubjectKind");
        let entry = bench
            .kernel
            .state
            .affordance_catalog
            .get(&bench.carry)
            .expect("the fixture entry");
        assert!(
            entry
                .roles
                .iter()
                .any(|spec| spec.kind == RefKind::Subject(None))
        );

        // The state row encoder and the digest owner are the same `to_vec_named`
        // path the store writes through.
        let encoded = rmp_serde::to_vec_named(&bench.kernel.state).expect("the state encodes");
        let decoded: WorldState = rmp_serde::from_slice(&encoded).expect("the state decodes");
        assert_eq!(
            decoded.affordance_catalog,
            bench.kernel.state.affordance_catalog
        );
        assert!(digest(&bench.kernel.state).is_ok());
    }

    #[test]
    fn a_forged_band_or_forged_effect_does_not_apply() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "ForgedAction");
        let opportunity = bench.clerk_opportunity();
        let command_id = CommandId::issue();
        let invocation = bench.carry(2, bench.yard);
        let honest = draw_once(&bench.kernel.state, command_id, &opportunity, &invocation).unwrap();
        let caller = CallerId::Controller(opportunity.controller_id);

        for forged in [
            DecisionEvent {
                band: 3,
                ..honest.clone()
            },
            DecisionEvent {
                effects: vec![ResolvedOp::Transfer {
                    from: bench.clerk,
                    to: bench.keeper,
                    resource: bench.tithe,
                    qty: Quantity(OPENING_BALANCE),
                }],
                ..honest.clone()
            },
        ] {
            let mut candidate = bench.kernel.state.clone();
            let error = apply_effect(
                &mut candidate,
                command_id,
                &caller,
                &WorldEffect::DecisionExercised {
                    opportunity: opportunity.clone(),
                    invocation: invocation.clone(),
                    event: forged,
                },
            )
            .unwrap_err();
            assert!(matches!(error, KernelError::Invariant(_)));
            assert_eq!(candidate, bench.kernel.state);
        }

        // The honest event still applies through the same arm.
        let mut candidate = bench.kernel.state.clone();
        apply_effect(
            &mut candidate,
            command_id,
            &caller,
            &WorldEffect::DecisionExercised {
                opportunity,
                invocation: invocation.clone(),
                event: honest,
            },
        )
        .unwrap();
        assert_ne!(candidate.holdings, bench.kernel.state.holdings);
    }

    #[test]
    fn the_kernel_speak_entry_commits_its_utterance_and_no_operation() {
        let directory = tempfile::tempdir().unwrap();
        let mut kernel = custody_kernel(directory.path(), "SpeakEntry");
        let _topology = activate(&mut kernel);
        let active = kernel.snapshot().unwrap();
        let speak = affordance_named(&active, "speak");
        let human = active
            .subjects
            .iter()
            .find(|subject| subject.human_controller.is_some())
            .expect("the human subject")
            .id;
        let holdings = kernel.state.holdings.clone();
        let receipt = kernel
            .submit(
                command(
                    &active,
                    CommandId::new(),
                    CallerId::Principal(human_principal()),
                    CommandBody::ExerciseDecision {
                        opportunity: opportunity_for(&active, human),
                        invocation: DecisionInvocation {
                            affordance: speak,
                            bindings: Vec::new(),
                            proposed: Vec::new(),
                            speech: Some(Statement::new("I open the door.").unwrap()),
                            display: None,
                        },
                    },
                ),
                &auth_principal(human_principal()),
            )
            .expect("the Speak entry commits");
        assert!(matches!(receipt, SubmitReceipt::Applied(_)));
        let event = kernel.state.events.last().unwrap();
        assert_eq!(event.band, 0);
        // The two the speech lowering prepends, and nothing from a slot.
        assert_eq!(event.effects.len(), 2);
        assert_eq!(
            crate::tests::spoken(&kernel.state, event),
            Some("I open the door.")
        );
        assert_eq!(kernel.state.holdings, holdings);
    }

    /// The civic bench: one world where an institution holds jurisdiction over
    /// a hall, a person holds the same jurisdiction only through an office, and
    /// two more people stand inside and outside that ground.
    struct Civics {
        kernel: WorldKernel,
        active: WorldSnapshot,
        topology: Topology,
        civic: Civic,
    }

    fn civics(directory: &std::path::Path, title: &str) -> Civics {
        let mut kernel = custody_kernel(directory, title);
        let (topology, civic, active) = civic_world(&mut kernel);
        Civics {
            kernel,
            active,
            topology,
            civic,
        }
    }

    impl Civics {
        fn refresh(&mut self) {
            self.active = self.kernel.snapshot().unwrap();
        }

        fn call(
            &self,
            kind: &str,
            bindings: Vec<RoleBinding>,
            proposed: Vec<ProposedEffect>,
            speech: Option<Statement>,
        ) -> DecisionInvocation {
            DecisionInvocation {
                affordance: affordance_named(&self.active, kind),
                bindings,
                proposed,
                speech,
                display: None,
            }
        }

        /// A levy of `qty` from `payer`, always paid to the actor: the payee is
        /// the reserved role, so it is not a binding the caller supplies.
        fn levy(&self, payer: SubjectId, qty: u64) -> DecisionInvocation {
            self.call(
                "levy",
                vec![
                    binding("payer", Target::Subject(payer)),
                    binding("resource", Target::Entity(self.civic.grain)),
                ],
                vec![ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::Quantity(Quantity(qty)),
                }],
                None,
            )
        }

        fn delegate(&self, deputy: SubjectId, ground: EntityId) -> DecisionInvocation {
            self.call(
                "delegate",
                vec![
                    binding("deputy", Target::Subject(deputy)),
                    binding("ground", Target::Entity(ground)),
                ],
                vec![ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::None,
                }],
                None,
            )
        }

        /// Commands a subordinate to traverse a route: the fixture's one
        /// `Relocate`-lowering affordance, gated by `command` authority over
        /// the subordinate rather than by the subordinate's own consent.
        fn deploy(&self, subordinate: SubjectId, via: EdgeId) -> DecisionInvocation {
            self.call(
                "deploy",
                vec![
                    binding("subordinate", Target::Subject(subordinate)),
                    binding("via", Target::Edge(via)),
                ],
                vec![ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::None,
                }],
                None,
            )
        }

        /// Strips `holder`'s `levy` grant over `ground`: the fixture's one
        /// `RevokeAuthority`-lowering affordance, carrying no precondition of
        /// its own so a rejection can only be the action lane's revocation
        /// envelope.
        fn revoke(&self, holder: SubjectId, ground: EntityId) -> DecisionInvocation {
            self.call(
                "revoke",
                vec![
                    binding("holder", Target::Subject(holder)),
                    binding("ground", Target::Entity(ground)),
                ],
                vec![ProposedEffect {
                    slot: 0,
                    magnitude: Magnitude::None,
                }],
                None,
            )
        }

        fn try_as(
            &self,
            actor: SubjectId,
            invocation: &DecisionInvocation,
        ) -> Result<DecisionEvent, KernelError> {
            draw_once(
                &self.kernel.state,
                CommandId::issue(),
                &opportunity_for(&self.active, actor),
                invocation,
            )
        }

        fn rejected_as(
            &self,
            actor: SubjectId,
            invocation: &DecisionInvocation,
        ) -> Vec<ActionMismatch> {
            match self.try_as(actor, invocation) {
                Err(KernelError::ActionRejected(rejected)) => rejected,
                other => panic!("expected an action rejection, got {other:?}"),
            }
        }

        fn commit_as(&mut self, actor: SubjectId, invocation: DecisionInvocation) -> SubmitReceipt {
            let opportunity = opportunity_for(&self.active, actor);
            let caller = CallerId::Controller(opportunity.controller_id);
            let receipt = self
                .kernel
                .submit(
                    command(
                        &self.active,
                        CommandId::new(),
                        caller.clone(),
                        CommandBody::ExerciseDecision {
                            opportunity,
                            invocation,
                        },
                    ),
                    &AuthenticatedCaller::fixture(caller),
                )
                .expect("the invocation commits");
            self.refresh();
            receipt
        }

        fn admit(&mut self, ops: Vec<ComponentOp>) {
            let before = self.kernel.snapshot().unwrap();
            let receipt = submit_owner(&mut self.kernel, &before, operations(ops));
            assert!(matches!(receipt, SubmitReceipt::Applied(_)));
            self.refresh();
        }

        fn refuse(&mut self, ops: Vec<ComponentOp>) -> Vec<crate::Mismatch> {
            let before = self.kernel.snapshot().unwrap();
            reject_owner(&mut self.kernel, &before, operations(ops))
        }
    }

    /// Contract 7's third rejection: commanding outside authority, with the
    /// exact failed precondition and no allocation.
    #[test]
    fn an_unauthorized_actor_names_the_failed_precondition() {
        let directory = tempfile::tempdir().unwrap();
        let bench = civics(directory.path(), "Unauthorized");
        let before = bench.kernel.state.clone();
        assert_eq!(
            bench.rejected_as(bench.civic.farmer, &bench.levy(bench.civic.reeve, 1)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );
        assert_eq!(bench.kernel.state.revision, before.revision);
        assert_eq!(bench.kernel.state.holdings, before.holdings);
        assert_eq!(bench.kernel.state.events, before.events);
    }

    /// A place jurisdiction reaches everything under it and nothing else, and
    /// the answer is read live: a target that walks out of the subtree stops
    /// being authorized without any digest moving.
    #[test]
    fn place_jurisdiction_reaches_a_containment_subtree_and_stops_at_its_edge() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Subtree");
        let treasury = bench.civic.treasury;
        let farmer = bench.civic.farmer;

        // The farmer stands in the chamber, whose container is the hall.
        assert!(bench.try_as(treasury, &bench.levy(farmer, 1)).is_ok());

        // The pedlar stands on the road, outside the hall entirely, and an
        // unplaced subject is covered by no place target.
        assert_eq!(
            bench.rejected_as(treasury, &bench.levy(bench.civic.outsider, 1)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );
        let unplaced = *bench
            .kernel
            .state
            .subjects
            .keys()
            .find(|subject_id| !bench.kernel.state.positions.contains_key(subject_id))
            .expect("the genesis world declares an unplaced subject");
        assert_eq!(
            bench.rejected_as(treasury, &bench.levy(unplaced, 1)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );

        // Walking the farmer out of the hall flips the same invocation, and a
        // grant naming the subject directly authorizes it again.
        bench.admit(vec![
            ComponentOp::Relocate {
                subject: PatchRef::Existing(farmer),
                via: PatchRef::Existing(bench.civic.passage),
            },
            ComponentOp::Relocate {
                subject: PatchRef::Existing(farmer),
                via: PatchRef::Existing(bench.civic.causeway),
            },
        ]);
        assert_eq!(
            bench.rejected_as(treasury, &bench.levy(farmer, 1)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );
        bench.admit(vec![grant_to(treasury, LEVY_KIND, over_subject(farmer))]);
        assert!(bench.try_as(treasury, &bench.levy(farmer, 1)).is_ok());
    }

    /// An office lends jurisdiction its holder does not own, vacating takes it
    /// back, and a proposal bound before the vacate rebinds rather than
    /// rejecting: revocation mid-flight is a `ScopeChanged`.
    #[test]
    fn office_delegation_grants_and_vacating_revokes() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Office");
        let reeve = bench.civic.reeve;
        let farmer = bench.civic.farmer;
        assert!(bench.kernel.state.authority.get(&reeve).is_none());
        assert!(bench.try_as(reeve, &bench.levy(farmer, 1)).is_ok());

        // A proposal bound before the vacate rebinds rather than rejecting.
        let bound = opportunity_for(&bench.active, reeve);
        let caller = CallerId::Controller(bound.controller_id);
        let invocation = bench.levy(farmer, 1);
        bench.admit(vec![ComponentOp::VacateOffice {
            institution: PatchRef::Existing(bench.civic.treasury),
            office: office(WARDEN_OFFICE),
        }]);

        let holdings = bench.kernel.state.holdings.clone();
        let error = bench
            .kernel
            .submit(
                command(
                    &bench.active,
                    CommandId::new(),
                    caller.clone(),
                    CommandBody::ExerciseDecision {
                        opportunity: bound.clone(),
                        invocation,
                    },
                ),
                &AuthenticatedCaller::fixture(caller),
            )
            .unwrap_err();
        let KernelError::ScopeChanged {
            scope, expected, ..
        } = error
        else {
            panic!("a vacated office is a rebind, not a rejection");
        };
        assert_eq!(scope, bound.scope);
        assert_eq!(expected, bound.scope_digest);
        assert_eq!(bench.kernel.state.holdings, holdings);
        assert_eq!(
            bench.rejected_as(reeve, &bench.levy(farmer, 1)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );
    }

    /// The reserved role in both directions: an invocation may not bind it, and
    /// a catalog entry may not declare it.
    #[test]
    fn a_levy_cannot_be_directed_away_from_the_actor() {
        let directory = tempfile::tempdir().unwrap();
        let bench = civics(directory.path(), "ActorRole");
        let mut redirected = bench.levy(bench.civic.farmer, 1);
        redirected
            .bindings
            .push(binding("actor", Target::Subject(bench.civic.outsider)));
        assert_eq!(
            bench.rejected_as(bench.civic.treasury, &redirected),
            vec![ActionMismatch::ActorRoleBound]
        );

        // The declaration half is Draft-only, so it runs against a fresh world.
        let draft_directory = tempfile::tempdir().unwrap();
        let mut draft = custody_kernel(draft_directory.path(), "ReservedRole");
        let before = draft.snapshot().unwrap();
        let handle = DraftHandle::new("thief");
        let rejected = reject_owner(
            &mut draft,
            &before,
            CommandBody::AdmitPatch {
                answers: None,
                patch: WorldPatch {
                    declarations: vec![Declaration::Affordance(AffordanceDeclaration {
                        handle: handle.clone(),
                        kind: AffordanceKindName("thief".into()),
                        roles: vec![RoleSpec {
                            role: Role("actor".into()),
                            kind: RefKind::Subject(None),
                        }],
                        preconditions: vec![Precondition::CanBroadcast {
                            via: AudienceSpec::Colocated,
                        }],
                        effect_slots: Vec::new(),
                        outcome_bands: vec![OutcomeBand {
                            weight: 1,
                            effects: Vec::new(),
                        }],
                        carries_speech: true,
                    })],
                    operations: Vec::new(),
                    evidence: Vec::new(),
                },
            },
        );
        assert!(rejected.contains(&crate::Mismatch::ReservedRole { handle }));
    }

    /// The two rejection lanes stay distinguishable: an over-levy inside its
    /// declared ceiling passes every affordance check and dies in the ledger.
    #[test]
    fn an_over_levy_fails_in_the_ledger_not_in_the_contract() {
        let directory = tempfile::tempdir().unwrap();
        let bench = civics(directory.path(), "OverLevy");
        let error = bench
            .try_as(bench.civic.treasury, &bench.levy(bench.civic.farmer, 9))
            .unwrap_err();
        let KernelError::PatchRejected(rejected) = error else {
            panic!("an over-levy is a ledger rejection");
        };
        assert_eq!(
            rejected,
            vec![crate::Mismatch::InsufficientCustody { operation: 0 }]
        );
    }

    /// A delegated grant may not exceed the granter's own, and the grant it
    /// does mint is still subject to disjointness.
    #[test]
    fn a_delegated_grant_may_not_exceed_the_granter() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Monotone");
        let treasury = bench.civic.treasury;
        let outsider = bench.civic.outsider;

        // The treasury commands the road but does not levy it, so the
        // precondition passes and the delegation alone is refused.
        let road = bench.topology.road;
        bench.admit(vec![grant_to(treasury, COMMAND_KIND, over_place(road))]);
        let outside = bench.delegate(outsider, road);
        assert_eq!(
            bench.rejected_as(treasury, &outside),
            vec![ActionMismatch::DelegationNotMonotone { slot: 0 }]
        );

        // The chamber is inside it, so the same act commits and the deputy can
        // then levy there.
        let inside = bench.delegate(outsider, bench.civic.chamber);
        assert!(matches!(
            bench.commit_as(treasury, inside),
            SubmitReceipt::Applied(_)
        ));
        assert!(
            bench
                .try_as(outsider, &bench.levy(bench.civic.farmer, 1))
                .is_ok()
        );

        // An office lends exactly the kinds it delegates: the warden's `levy`
        // does not make its holder a commander.
        let again = bench.delegate(outsider, bench.civic.chamber);
        assert_eq!(
            bench.rejected_as(bench.civic.reeve, &again),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );

        // The minted grant is ordinary state: repeating it changes nothing, and
        // widening it to the hall would overlap what the deputy already holds.
        let error = bench.try_as(treasury, &again).unwrap_err();
        let KernelError::PatchRejected(rejected) = error else {
            panic!("a duplicate delegation is a patch rejection");
        };
        assert_eq!(
            rejected,
            vec![crate::Mismatch::NoOperationEffect { operation: 0 }]
        );
        let hall = bench.civic.hall;
        assert_eq!(
            bench.refuse(vec![grant_to(outsider, LEVY_KIND, over_place(hall))]),
            vec![crate::Mismatch::OverlappingJurisdiction { operation: 0 }]
        );
    }

    /// The revocation envelope is the same predicate as the grant one, read
    /// the other way: an actor may strip a `levy` grant only over ground its
    /// own authority covers, so a bystander cannot use `revoke` to reach past
    /// what it may otherwise touch.
    #[test]
    fn revoking_a_grant_requires_covering_authority_over_its_ground() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "RevokeEnvelope");
        let treasury = bench.civic.treasury;
        let farmer = bench.civic.farmer;
        let hall = bench.civic.hall;

        // The farmer holds no authority at all, so stripping the treasury's
        // own `levy` grant over the hall fails the envelope before the patch
        // lane ever sees the operation.
        assert_eq!(
            bench.rejected_as(farmer, &bench.revoke(treasury, hall)),
            vec![ActionMismatch::DelegationNotMonotone { slot: 0 }]
        );

        // The treasury's own `levy` grant over the hall covers the hall, so
        // the treasury may strip its own grant, and the ledger loses it.
        assert!(matches!(
            bench.commit_as(treasury, bench.revoke(treasury, hall)),
            SubmitReceipt::Applied(_)
        ));
        assert!(
            !bench
                .kernel
                .state
                .authority
                .get(&treasury)
                .is_some_and(|grants| grants.contains(&crate::AuthorityGrant {
                    kind: authority_kind(LEVY_KIND),
                    over: crate::AuthorityTarget::PlaceSubtree(hall),
                })),
            "the revoked grant is gone from the ledger"
        );
    }

    /// The fixture's one `Relocate`-lowering affordance: a commander may move
    /// a subordinate standing under its ground, and one standing outside it
    /// fails the same `Authorized` precondition every other civic act reads.
    #[test]
    fn deploy_relocates_a_subordinate_under_command_and_refuses_one_outside_it() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Deploy");
        let treasury = bench.civic.treasury;
        let farmer = bench.civic.farmer;
        let outsider = bench.civic.outsider;
        let chamber = bench.civic.chamber;
        let hall = bench.civic.hall;
        let passage = bench.civic.passage;

        // The pedlar stands on the road, outside the hall the treasury
        // commands, so the precondition alone refuses the attempt.
        assert_eq!(
            bench.rejected_as(treasury, &bench.deploy(outsider, passage)),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );

        // The farmer stands in the chamber, under the treasury's command, and
        // the passage carries it into the hall the same way a direct
        // `Relocate` would.
        assert_eq!(
            bench
                .kernel
                .state
                .positions
                .get(&farmer)
                .map(|position| position.place),
            Some(chamber)
        );
        assert!(matches!(
            bench.commit_as(treasury, bench.deploy(farmer, passage)),
            SubmitReceipt::Applied(_)
        ));
        assert_eq!(
            bench
                .kernel
                .state
                .positions
                .get(&farmer)
                .map(|position| position.place),
            Some(hall)
        );
    }

    /// `Redress` with a reducer reader: standing is the covering predicate over
    /// the forum's target, and closing the forum removes the verb's ground.
    #[test]
    fn a_petition_requires_standing() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Petition");
        let petition = bench.call(
            "petition",
            Vec::new(),
            Vec::new(),
            Some(Statement::new("The tithe was taken twice.").unwrap()),
        );
        assert_eq!(
            bench.rejected_as(bench.civic.outsider, &petition),
            vec![ActionMismatch::NoStanding { precondition: 0 }]
        );

        let farmer = bench.civic.farmer;
        assert!(matches!(
            bench.commit_as(farmer, petition.clone()),
            SubmitReceipt::Applied(_)
        ));
        let event = bench
            .kernel
            .state
            .events
            .last()
            .expect("the committed event");
        assert_eq!(event.effects.len(), 2);
        assert_eq!(
            crate::tests::spoken(&bench.kernel.state, event),
            Some("The tithe was taken twice.")
        );

        bench.admit(vec![ComponentOp::CloseForum {
            grievance: grievance(SEIZURE_GRIEVANCE),
        }]);
        assert_eq!(
            bench.rejected_as(farmer, &petition),
            vec![ActionMismatch::NoStanding { precondition: 0 }]
        );
    }

    /// One predicate, two callers: the resolver's `Relocate` arm and
    /// `Reachable`'s edge admission read the same `Restricted` rule, and a
    /// same-patch grant then move resolves through the candidate shadow.
    #[test]
    fn a_restricted_route_admits_exactly_its_named_authority() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Restricted");
        let walker = bench.topology.walker;
        let step = ComponentOp::Relocate {
            subject: PatchRef::Existing(walker),
            via: PatchRef::Existing(bench.civic.postern),
        };

        // Holding nothing, and holding `admit` over the origin only, are both
        // refused: the rule asks about the destination.
        assert!(
            bench
                .refuse(vec![step.clone()])
                .contains(&crate::Mismatch::RouteAccessRestricted { operation: 0 })
        );
        let yard = bench.topology.yard;
        bench.admit(vec![grant_to(walker, ADMIT_KIND, over_place(yard))]);
        assert!(
            bench
                .refuse(vec![step.clone()])
                .contains(&crate::Mismatch::RouteAccessRestricted { operation: 0 })
        );

        // Reachable reads the same rule from the other side.
        let authority = crate::subject_authority(&bench.kernel.state, walker);
        assert!(!reachable(
            &bench.kernel.state,
            walker,
            &authority,
            bench.civic.chamber,
            Cost(4)
        ));

        // A grant covering the destination, minted in the same patch as the
        // move, resolves through the candidate authority shadow.
        let hall = bench.civic.hall;
        let authority = crate::subject_authority(&bench.kernel.state, walker);
        assert!(!patch::route_admits(
            &bench.kernel.state,
            &authority,
            bench.kernel.state.edges[&bench.civic.postern].access(),
            bench.civic.chamber
        ));
        bench.admit(vec![grant_to(walker, ADMIT_KIND, over_place(hall)), step]);
        assert_eq!(
            bench.kernel.state.positions[&walker].place,
            bench.civic.chamber
        );
        let authority = crate::subject_authority(&bench.kernel.state, walker);
        assert!(patch::route_admits(
            &bench.kernel.state,
            &authority,
            bench.kernel.state.edges[&bench.civic.postern].access(),
            bench.civic.chamber
        ));
    }

    /// The remaining institutional affordances, each proving one lowering arm:
    /// `sanction` closes shared topology under a legitimacy check with an
    /// interdiction band that fails, and `appoint` makes succession an act
    /// someone performs under authority rather than a declared method string.
    #[test]
    fn sanction_and_appoint_reach_the_reducer_under_authority() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "Institutional");
        let treasury = bench.civic.treasury;
        let passage = bench.civic.passage;
        let sanction = bench.call(
            "sanction",
            vec![binding("road", Target::Edge(passage))],
            vec![ProposedEffect {
                slot: 0,
                magnitude: Magnitude::None,
            }],
            None,
        );

        // The reeve holds the verb but its office lends only `levy`, so the same
        // act names the precondition it failed.
        assert_eq!(
            bench.rejected_as(bench.civic.reeve, &sanction),
            vec![ActionMismatch::NotAuthorized { precondition: 0 }]
        );

        // The treasury commands the hall, and the passage has both endpoints
        // under it. The draw decides whether the interdiction lands.
        let opportunity = opportunity_for(&bench.active, treasury);
        let closing = (0..256)
            .map(|_| CommandId::issue())
            .find(|candidate| {
                draw_once(&bench.kernel.state, *candidate, &opportunity, &sanction)
                    .expect("the invocation is admissible")
                    .band
                    == 0
            })
            .expect("a two-band entry reaches its first band over 256 command ids");
        let caller = CallerId::Controller(opportunity.controller_id);
        let receipt = bench
            .kernel
            .submit(
                command(
                    &bench.active,
                    closing,
                    caller.clone(),
                    CommandBody::ExerciseDecision {
                        opportunity,
                        invocation: sanction,
                    },
                ),
                &AuthenticatedCaller::fixture(caller),
            )
            .expect("the sanction commits");
        assert!(matches!(receipt, SubmitReceipt::Applied(_)));
        bench.refresh();
        assert!(!bench.kernel.state.edges[&passage].is_open());

        // Appointment installs an incumbent through the action lane.
        let farmer = bench.civic.farmer;
        let appoint = bench.call(
            "appoint",
            vec![
                binding("institution", Target::Subject(treasury)),
                binding("candidate", Target::Subject(farmer)),
            ],
            vec![ProposedEffect {
                slot: 0,
                magnitude: Magnitude::None,
            }],
            None,
        );
        assert!(matches!(
            bench.commit_as(treasury, appoint),
            SubmitReceipt::Applied(_)
        ));
        assert_eq!(
            bench.kernel.state.selection[&treasury][&office("bailiff")].incumbent,
            Some(farmer)
        );
    }

    // --- Soul: pass-5 falsification -------------------------------------

    /// Submits an invocation through the real command path and hands back
    /// whatever the kernel decided, refusal included.
    fn soul_submit(
        bench: &mut Civics,
        actor: SubjectId,
        invocation: DecisionInvocation,
    ) -> Result<SubmitReceipt, KernelError> {
        let opportunity = opportunity_for(&bench.active, actor);
        let caller = CallerId::Controller(opportunity.controller_id);
        bench.kernel.submit(
            command(
                &bench.active,
                CommandId::new(),
                caller.clone(),
                CommandBody::ExerciseDecision {
                    opportunity,
                    invocation,
                },
            ),
            &AuthenticatedCaller::fixture(caller),
        )
    }

    /// `DelegationNotMonotone` is not a fixture-only verdict: the real `submit`
    /// path reaches it, and the refused act commits nothing.
    #[test]
    fn soul_delegation_not_monotone_reaches_the_real_command_path() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "SoulMonotone");
        let treasury = bench.civic.treasury;
        let road = bench.topology.road;
        let outsider = bench.civic.outsider;
        bench.admit(vec![grant_to(treasury, COMMAND_KIND, over_place(road))]);
        let invocation = bench.delegate(outsider, road);
        let before = bench.kernel.state.clone();
        let error = soul_submit(&mut bench, treasury, invocation).unwrap_err();
        let KernelError::ActionRejected(rejected) = error else {
            panic!("an over-wide delegation is an action rejection");
        };
        assert_eq!(
            rejected,
            vec![ActionMismatch::DelegationNotMonotone { slot: 0 }]
        );
        assert_eq!(bench.kernel.state.revision, before.revision);
        assert_eq!(bench.kernel.state.authority, before.authority);
        assert_eq!(bench.kernel.state.events.len(), before.events.len());
    }

    /// The `Restricted` rule is enforced by the component writer, not only by
    /// the resolver: a forged `ResolvedOp` that skipped `resolve_patch` still
    /// cannot walk a subject through a door it holds no key to, and the same
    /// operation succeeds the moment the key exists.
    #[test]
    fn soul_a_restricted_relocate_is_refused_by_the_component_writer() {
        let directory = tempfile::tempdir().unwrap();
        let bench = civics(directory.path(), "SoulReducerGate");
        let walker = bench.topology.walker;
        let step = ResolvedOp::Relocate {
            subject_id: walker,
            edge_id: bench.civic.postern,
        };

        let mut forged = bench.kernel.state.clone();
        forged.positions.insert(
            walker,
            crate::patch::Position {
                place: bench.topology.yard,
            },
        );
        let mut refused = forged.clone();
        let resolves_to = refused.revision + 1;
        let error =
            crate::apply_operations(&mut refused, std::slice::from_ref(&step), &[], resolves_to)
                .unwrap_err();
        assert!(matches!(error, KernelError::Invariant(_)));

        let mut admitted = forged;
        admitted.authority.insert(
            walker,
            BTreeSet::from([crate::AuthorityGrant {
                kind: crate::tests::authority_kind(ADMIT_KIND),
                over: crate::AuthorityTarget::PlaceSubtree(bench.civic.hall),
            }]),
        );
        let resolves_to = admitted.revision + 1;
        crate::apply_operations(&mut admitted, std::slice::from_ref(&step), &[], resolves_to)
            .expect("the named key opens the door at the component writer too");
        assert_eq!(admitted.positions[&walker].place, bench.civic.chamber);
    }

    /// The rebind requirement at the layer the operator sees it: a grant change
    /// on the *granter* invalidates the delegate's bound opportunity, and the
    /// refusal is `ScopeChanged`, not `ActionRejected`.
    #[test]
    fn soul_a_granter_revocation_rebinds_the_delegate() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "SoulRebind");
        let reeve = bench.civic.reeve;
        let farmer = bench.civic.farmer;
        let treasury = bench.civic.treasury;
        let hall = bench.civic.hall;

        // Bound before the revocation, submitted after it.
        let bound = bench.active.clone();
        let opportunity = opportunity_for(&bound, reeve);
        let caller = CallerId::Controller(opportunity.controller_id);
        let invocation = bench.levy(farmer, 1);
        bench.admit(vec![ComponentOp::RevokeAuthority {
            holder: PatchRef::Existing(treasury),
            grant: crate::tests::grant_of(LEVY_KIND, over_place(hall)),
        }]);
        // The envelope carries the current revision, so only the stale scope
        // digest can refuse it.
        assert_ne!(bound.revision, bench.active.revision);
        let error = bench
            .kernel
            .submit(
                command(
                    &bench.active,
                    CommandId::new(),
                    caller.clone(),
                    CommandBody::ExerciseDecision {
                        opportunity,
                        invocation,
                    },
                ),
                &AuthenticatedCaller::fixture(caller),
            )
            .unwrap_err();
        assert!(
            matches!(error, KernelError::ScopeChanged { .. }),
            "a revoked lend is a rebind, not a rejection: {error:?}"
        );
    }

    /// Structural overlap alone does not see a `Subject` grant sitting inside
    /// a `PlaceSubtree` grant of the same kind: `targets_overlap` compares
    /// shapes, not position, and stays that way so the answer cannot decay
    /// when a subject walks somewhere. Admission catches the cross-shape case
    /// live instead, in the resolver where the second grant lands, using the
    /// same covering predicate `Authorized` reads. Both directions fire the
    /// identical check, so whichever grant is minted second is the one that
    /// is refused.
    #[test]
    fn soul_a_subject_grant_inside_a_place_grant_is_a_second_source() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "SoulOverlap");
        let treasury = bench.civic.treasury;
        let farmer = bench.civic.farmer;
        let hall = bench.civic.hall;

        // The farmer stands under the hall the treasury already levies, so the
        // covering predicate says both grants would answer for the same
        // target, and admission refuses the narrower grant landing second.
        assert!(crate::covers(
            &bench.kernel.state,
            crate::AuthorityTarget::PlaceSubtree(hall),
            Target::Subject(farmer)
        ));
        assert_eq!(
            bench.refuse(vec![grant_to(treasury, LEVY_KIND, over_subject(farmer))]),
            vec![crate::Mismatch::OverlappingJurisdiction { operation: 0 }]
        );

        // The symmetric order: a direct grant over the farmer, minted first
        // under a fresh kind, blocks a later place grant that would cover
        // him too.
        const AUDIT_KIND: &str = "audit";
        bench.admit(vec![grant_to(treasury, AUDIT_KIND, over_subject(farmer))]);
        assert_eq!(
            bench.refuse(vec![grant_to(treasury, AUDIT_KIND, over_place(hall))]),
            vec![crate::Mismatch::OverlappingJurisdiction { operation: 0 }]
        );
    }

    /// Replay for the new arms is not a separate machine: `reduce` is a pure
    /// function of committed state and the envelope, so running it twice on the
    /// same pre-state yields the same effect, and that effect is exactly what
    /// the commit stored. No clock and no draw entropy enter.
    #[test]
    fn soul_reduce_is_pure_for_every_new_civic_arm() {
        let directory = tempfile::tempdir().unwrap();
        let mut bench = civics(directory.path(), "SoulReplay");
        let treasury = bench.civic.treasury;
        let farmer = bench.civic.farmer;
        let outsider = bench.civic.outsider;
        let chamber = bench.civic.chamber;
        let passage = bench.civic.passage;

        let sanction = bench.call(
            "sanction",
            vec![binding("road", Target::Edge(passage))],
            vec![ProposedEffect {
                slot: 0,
                magnitude: Magnitude::None,
            }],
            None,
        );
        let appoint = bench.call(
            "appoint",
            vec![
                binding("institution", Target::Subject(treasury)),
                binding("candidate", Target::Subject(farmer)),
            ],
            vec![ProposedEffect {
                slot: 0,
                magnitude: Magnitude::None,
            }],
            None,
        );
        for invocation in [
            bench.levy(farmer, 2),
            bench.delegate(outsider, chamber),
            sanction,
            appoint,
        ] {
            let before = bench.kernel.state.clone();
            let opportunity = opportunity_for(&bench.active, treasury);
            let caller = CallerId::Controller(opportunity.controller_id);
            let envelope = command(
                &bench.active,
                CommandId::new(),
                caller.clone(),
                CommandBody::ExerciseDecision {
                    opportunity,
                    invocation,
                },
            );
            let first = crate::reduce(&before, &envelope).expect("the act reduces");
            let second = crate::reduce(&before, &envelope).expect("the act reduces again");
            assert_eq!(first, second, "reduce is not a pure function of the state");
            let receipt = bench
                .kernel
                .submit(envelope, &AuthenticatedCaller::fixture(caller))
                .expect("the act commits");
            assert!(matches!(receipt, SubmitReceipt::Applied(_)));
            bench.refresh();
            let WorldEffect::DecisionExercised { event, .. } = first else {
                panic!("an exercised decision");
            };
            assert_eq!(
                bench.kernel.state.events.last().expect("a committed event"),
                &event,
                "the committed event is not what reduce produced"
            );
        }
    }

    /// `resolve_patch`'s candidate knowledge map never models `Communicate`'s
    /// fan-out (see the comment beside its construction in `patch.rs`), and
    /// that omission is safe only because no `ComponentOpKind` an affordance
    /// may declare can lower to a knowledge write carrying
    /// `KnowledgeSource::Told`. Two kinds touch knowledge: `AcquireKnowledge`,
    /// whose source `lower` fixes to `AuthoredSource::Witnessed` — a type with
    /// no `Told` variant to begin with — and `Witness`, which carries no source
    /// field and no speaker field at all. This test pins both lowerings, at the
    /// call sites that turn a declared slot into a `ComponentOp`, so a future
    /// author cannot widen either without this assertion naming the change.
    #[test]
    fn no_component_op_kind_lowers_to_a_told_knowledge_write() {
        let directory = tempfile::tempdir().unwrap();
        let bench = bench(directory.path(), "AcquireKnowledgeLowering");
        let entry = Affordance {
            kind: AffordanceKindName("witness".into()),
            roles: Vec::new(),
            preconditions: Vec::new(),
            effect_slots: vec![EffectSlot {
                op_kind: ComponentOpKind::AcquireKnowledge {
                    confidence: Confidence::Believed,
                },
                roles: vec![Role("subject".into()), Role("fact".into())],
                bounds: Bounds::None,
            }],
            outcome_bands: vec![OutcomeBand {
                weight: 1,
                effects: vec![0],
            }],
            carries_speech: false,
        };
        let bindings = BTreeMap::from([
            (Role("subject".into()), Target::Subject(bench.clerk)),
            (Role("fact".into()), Target::Entity(bench.tithe)),
        ]);
        let invocation = DecisionInvocation {
            affordance: bench.carry,
            bindings: Vec::new(),
            proposed: vec![ProposedEffect {
                slot: 0,
                magnitude: Magnitude::None,
            }],
            speech: None,
            display: None,
        };
        let lowered = lower(
            &entry,
            0,
            bench.clerk,
            FictionalMinutes::default(),
            &invocation,
            &bindings,
        )
        .expect("the acquire slot lowers");
        assert_eq!(
            lowered,
            vec![ComponentOp::AcquireKnowledge {
                subject: PatchRef::Existing(bench.clerk),
                fact: PatchRef::Existing(bench.tithe),
                source: AuthoredSource::Witnessed,
                confidence: Confidence::Believed,
            }],
            "AcquireKnowledge must lower with a Witnessed source, never a forged teller"
        );

        let witnessing = Affordance {
            kind: AffordanceKindName("beacon".into()),
            roles: Vec::new(),
            preconditions: Vec::new(),
            effect_slots: vec![EffectSlot {
                op_kind: ComponentOpKind::Witness {
                    confidence: Confidence::Believed,
                },
                roles: vec![Role("fact".into()), Role("place".into())],
                bounds: Bounds::None,
            }],
            outcome_bands: vec![OutcomeBand {
                weight: 1,
                effects: vec![0],
            }],
            carries_speech: false,
        };
        let places = BTreeMap::from([
            (Role("fact".into()), Target::Entity(bench.tithe)),
            (Role("place".into()), Target::Entity(bench.yard)),
        ]);
        let lowered = lower(
            &witnessing,
            0,
            bench.clerk,
            FictionalMinutes::default(),
            &invocation,
            &places,
        )
        .expect("the witness slot lowers");
        assert_eq!(
            lowered,
            vec![ComponentOp::Witness {
                fact: PatchRef::Existing(bench.tithe),
                place: PatchRef::Existing(bench.yard),
                confidence: Confidence::Believed,
            }],
            "Witness must lower with no speaker field to forge"
        );
    }

    /// The pin above names two knowledge-touching kinds by example, and a list
    /// by example goes stale in silence. This match is exhaustive, so a new
    /// `ComponentOpKind` breaks the build here and its author must say whether
    /// it touches knowledge. The set that does is `AcquireKnowledge`, `Witness`,
    /// and `Forget`; the first two are pinned above as unable to carry a teller,
    /// and `Forget` writes no row at all — it only removes one, so it has no
    /// source to forge.
    #[test]
    fn soul_every_knowledge_touching_op_kind_is_named_by_the_told_pin() {
        fn touches_knowledge(kind: &ComponentOpKind) -> bool {
            match kind {
                ComponentOpKind::AcquireKnowledge { .. }
                | ComponentOpKind::Witness { .. }
                | ComponentOpKind::Forget => true,
                ComponentOpKind::Relocate
                | ComponentOpKind::OpenRoute
                | ComponentOpKind::CloseRoute
                | ComponentOpKind::AlterCost
                | ComponentOpKind::Transfer
                | ComponentOpKind::Transform
                | ComponentOpKind::Consume
                | ComponentOpKind::Bind
                | ComponentOpKind::Release
                | ComponentOpKind::GrantAuthority { .. }
                | ComponentOpKind::RevokeAuthority { .. }
                | ComponentOpKind::InstallIncumbent { .. }
                | ComponentOpKind::VacateOffice { .. }
                | ComponentOpKind::CreateCommitment { .. }
                | ComponentOpKind::AdvancePressure { .. }
                | ComponentOpKind::ReducePressure { .. }
                | ComponentOpKind::ResolvePressure => false,
            }
        }

        let confidence = Confidence::Believed;
        let touching = [
            ComponentOpKind::AcquireKnowledge { confidence },
            ComponentOpKind::Witness { confidence },
            ComponentOpKind::Forget,
            ComponentOpKind::Relocate,
            ComponentOpKind::Transfer,
            ComponentOpKind::ResolvePressure,
        ]
        .iter()
        .filter(|kind| touches_knowledge(kind))
        .count();
        assert_eq!(
            touching, 3,
            "the knowledge-touching set is not the one this pin names"
        );
    }
}
