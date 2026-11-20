//! The pinned replay of one scripted session, and the commit-cost harness.
//!
//! The golden file is the determinism contract in its smallest form: every
//! committed revision's state digest and commit digest, and the digest of the
//! export, for a session that touches speech, display, a routine and a refusal.
//! A change to any id derivation, schema, encoding or reducer arm moves a line.

use crate::tests::{
    affordance_named, auth_principal, command, creation, first_grant, operations, owner,
    play_caller, play_command, speak, subject_by_label, submit_play, tick_body,
};
use crate::patch::{
    AffordanceDeclaration, AffordanceKindName, AudienceSpec, Declaration, DraftHandle,
    OutcomeBand, Precondition,
};
use crate::{
    AuthenticatedCaller, CommandBody, CommitmentKind, ComponentOp, DecisionInvocation,
    FictionalMinutes, KernelError, KnowledgeSource, Ref, Statement, TickMinutes, WorldEffect,
    WorldKernel, WorldPatch,
};
use sha2::{Digest, Sha256};
use std::time::Instant;

/// The script: The Human speaks to The Witness, The Human shows a gesture, a
/// two-band `toss` is declared, granted to The Human and used three times (so
/// the band draw is recorded), a Routine is placed on The Witness, thirty minutes roll it, and The Human
/// invokes `convene`, which it does not hold.
pub(super) fn golden_session() -> (WorldKernel, String) {
    let (mut kernel, _) = WorldKernel::create(creation("Golden"), &auth_principal(owner()))
        .expect("the fixture world is created");
    let human = subject_by_label(&kernel.state, "The Human");
    let witness = subject_by_label(&kernel.state, "The Witness");

    let snapshot = kernel.snapshot().unwrap();
    let speak_entry = first_grant(&snapshot, human);
    submit_play(
        &mut kernel,
        &snapshot,
        CommandBody::Act {
            actor: human,
            invocation: speak(speak_entry, "The vote carries."),
        },
    );
    let heard = kernel.snapshot().unwrap();
    let witness_knowledge = &heard
        .subjects
        .iter()
        .find(|subject| subject.id == witness)
        .expect("the witness is a subject")
        .knowledge;
    assert!(
        witness_knowledge.iter().any(|held| matches!(
            held.source,
            KnowledgeSource::Told { by, .. } if by == human
        )),
        "the witness holds what the human said as Told"
    );

    submit_play(
        &mut kernel,
        &heard,
        CommandBody::Act {
            actor: human,
            invocation: DecisionInvocation {
                affordance: speak_entry,
                bindings: Vec::new(),
                proposed: Vec::new(),
                speech: None,
                display: Some(Statement::new("The Human raises one finger.").unwrap()),
            },
        },
    );

    // A two-band entry: the one place the golden records a band draw.
    let snapshot = kernel.snapshot().unwrap();
    submit_play(
        &mut kernel,
        &snapshot,
        CommandBody::AdmitPatch {
            patch: WorldPatch {
                declarations: vec![Declaration::Affordance(AffordanceDeclaration {
                    handle: DraftHandle::new("toss"),
                    kind: AffordanceKindName("toss".into()),
                    roles: Vec::new(),
                    preconditions: vec![Precondition::CanBroadcast {
                        via: AudienceSpec::Colocated,
                    }],
                    effect_slots: Vec::new(),
                    outcome_bands: vec![
                        OutcomeBand { weight: 1, effects: Vec::new() },
                        OutcomeBand { weight: 1, effects: Vec::new() },
                    ],
                    carries_speech: true,
                })],
                operations: vec![ComponentOp::GrantAffordance {
                    subject: Ref::Existing(human),
                    affordance: Ref::Draft(DraftHandle::new("toss")),
                }],
                evidence: Vec::new(),
            },
        },
    );
    for _ in 0..3 {
        let snapshot = kernel.snapshot().unwrap();
        let toss = affordance_named(&snapshot, "toss");
        submit_play(
            &mut kernel,
            &snapshot,
            CommandBody::Act {
                actor: human,
                invocation: DecisionInvocation {
                    affordance: toss,
                    bindings: Vec::new(),
                    proposed: Vec::new(),
                    speech: Some(Statement::new("Heads or tails.").unwrap()),
                    display: None,
                },
            },
        );
    }

    let snapshot = kernel.snapshot().unwrap();
    submit_play(
        &mut kernel,
        &snapshot,
        operations(vec![ComponentOp::CreateCommitment {
            subject: Ref::Existing(witness),
            counterparty: None,
            kind: CommitmentKind::Routine,
            due: FictionalMinutes(10),
            period: Some(TickMinutes::new(10).unwrap()),
            checks: Vec::new(),
            statement: Statement::new("The Witness keeps the lamp.").unwrap(),
        }]),
    );

    let snapshot = kernel.snapshot().unwrap();
    submit_play(&mut kernel, &snapshot, tick_body(30));
    let ticked = kernel.journal.commits_for_test();
    let WorldEffect::TimeAdvanced { motion, .. } =
        &ticked.last().expect("the tick committed").effect
    else {
        panic!("the last commit is the tick");
    };
    assert!(!motion.fulfilled.is_empty(), "the routine rolled");

    // The refusal changes nothing: not the revision, not either digest.
    let before = kernel.snapshot().unwrap();
    let convene = affordance_named(&before, "convene");
    let refused = kernel.submit(
        command(
            &before,
            play_caller(),
            CommandBody::Act {
                actor: human,
                invocation: speak(convene, "A meeting is called."),
            },
        ),
        &AuthenticatedCaller::verified_play(),
    );
    assert!(matches!(refused, Err(KernelError::AffordanceDenied)), "{refused:?}");
    assert_eq!(kernel.snapshot().unwrap(), before);
    assert_eq!(kernel.journal.commit_count(), 9);

    let mut file = String::new();
    for commit in kernel.journal.commits_for_test() {
        file.push_str(&format!(
            "{} {} {}\n",
            commit.resulting_revision, commit.resulting_state_digest, commit.digest
        ));
    }
    let export = kernel.export().unwrap();
    file.push_str(&format!("export sha256:{:x}\n", Sha256::digest(&export)));
    (kernel, file)
}

#[test]
fn golden_session_digests_are_fixed() {
    let (_, computed) = golden_session();
    let (_, again) = golden_session();
    assert_eq!(computed, again, "one session, two runs, one file");
    let fixture: Vec<&str> = include_str!("../fixtures/golden.digests").lines().collect();
    let computed_lines: Vec<&str> = computed.lines().collect();
    assert!(
        fixture == computed_lines,
        "the golden digests moved; the computed file is:\n{computed}"
    );
}

#[test]
fn export_open_round_trip_is_exact() {
    let (mut original, _) = golden_session();
    let exported = original.export().unwrap();
    let mut reopened = WorldKernel::open(&exported).expect("the golden export opens");
    assert_eq!(reopened.state, original.state);
    assert_eq!(reopened.snapshot().unwrap(), original.snapshot().unwrap());
    assert_eq!(reopened.export().unwrap(), exported);

    let human = subject_by_label(&original.state, "The Human");
    let snapshot = original.snapshot().unwrap();
    let next = play_command(
        &snapshot,
        CommandBody::Act {
            actor: human,
            invocation: speak(first_grant(&snapshot, human), "Order, please."),
        },
    );
    let play = AuthenticatedCaller::verified_play();
    let original_receipt = original.submit(next.clone(), &play).unwrap();
    let reopened_receipt = reopened.submit(next, &play).unwrap();
    assert_eq!(original_receipt, reopened_receipt);
    assert_eq!(original.export().unwrap(), reopened.export().unwrap());
}

/// The digest is what a replay or a fresh recompute would derive, at every
/// revision of the golden session and after reopening it, and also across a
/// removal, which the session itself never makes.
#[test]
fn incremental_digest_equals_full_recompute() {
    let (mut kernel, _) = golden_session();
    let commits = kernel.journal.commits_for_test();
    for (index, commit) in commits.iter().enumerate() {
        let replayed = crate::journal::verify_history(&commits[..=index]).unwrap();
        assert_eq!(
            replayed.full_state_digest().unwrap(),
            commit.resulting_state_digest,
            "revision {index}: the incremental digest left the full recompute"
        );
    }
    assert_eq!(kernel.state.full_state_digest().unwrap(), kernel.state.state_digest);

    // A removal: the witness forgets what it was told.
    let witness = subject_by_label(&kernel.state, "The Witness");
    let (fact, _) = kernel
        .state
        .knowledge_of(witness)
        .next()
        .expect("the witness knows something");
    let fact = *fact;
    let rows = kernel.state.knowledge.len();
    let snapshot = kernel.snapshot().unwrap();
    submit_play(
        &mut kernel,
        &snapshot,
        operations(vec![ComponentOp::Forget {
            subject: Ref::Existing(witness),
            fact: Ref::Existing(fact),
        }]),
    );
    assert_eq!(kernel.state.knowledge.len(), rows - 1, "a row was removed");
    assert_eq!(kernel.state.full_state_digest().unwrap(), kernel.state.state_digest);

    let reopened = WorldKernel::open(&kernel.export().unwrap()).unwrap();
    assert_eq!(reopened.state.full_state_digest().unwrap(), reopened.state.state_digest);
    assert_eq!(reopened.state.accumulator, kernel.state.accumulator);
}

/// The clockwork world: `persons` persons, ten to a place, one Routine each
/// with period `persons / 2`, due `1 + i mod (persons / 2)`, so a one-minute
/// tick rolls two routines at every size and a speech reaches the same nine
/// listeners. Every partition a read index serves is populated, because an
/// empty one hides its scan: one route between consecutive places, one
/// `Reach::Place` channel per place controlled by its first person, one
/// institution per place with a warden office held by that person and a forum
/// for the place's grievances, and two Obligations past due from the first
/// minute, so a tick presses two sources at every size.
///
/// Dependencies, retirement and checks grow with the world too, because
/// a partition that stays small hides the walk of it. Every person binds to the
/// previous live person (a satisfied dependency), and persons 0 and 1 also to
/// the last place's grain, which they do not hold: two failing dependencies at
/// every size. Every tenth person from the 35th retires (never a controller or
/// incumbent, who are place * 10), and every routine checks that its person
/// holds their place's grain. The clock then moves one period, so every
/// routine is past due: the live ones roll once, a retired person's stays due
/// for good, and a one-minute tick from there rolls two routines at every size.
fn clockwork_world(persons: usize) -> (WorldKernel, Vec<crate::SubjectId>, crate::AffordanceId) {
    use crate::patch::kernel_speak_grant;
    use crate::patch::{
        AccessKind, AuthoredSource, AuthorityGrantRef, AuthorityTargetRef, ChannelDeclaration,
        DependencyRef, EntityDeclaration, EvidenceRef, FactDeclaration, FactStandingRef,
        GrievanceKindName, PreconditionRef, Quantity, ReachRef, RouteDeclaration,
        SubjectDeclaration,
    };
    use crate::tests::{LEVY_KIND, TITHE_RECEIPT, WARDEN_OFFICE, authority_kind, office};
    use crate::Confidence;
    use crate::{CallerId, CommandEnvelope, CreateWorld, Cost, EntityKind, SubjectKind};
    use std::collections::BTreeSet;

    let places = persons / 10;
    let draft_place = |place: usize| Ref::Draft(DraftHandle::new(format!("place-{place}")));
    let draft_person = |person: usize| Ref::Draft(DraftHandle::new(format!("person-{person}")));
    let mut declarations = Vec::new();
    let mut offices = Vec::new();
    for place in 0..places {
        declarations.push(Declaration::Entity(EntityDeclaration {
            handle: DraftHandle::new(format!("place-{place}")),
            label: format!("Place {place}"),
            kind: EntityKind::Place,
            container: None,
        }));
        if place + 1 < places {
            declarations.push(Declaration::Route(RouteDeclaration {
                handle: DraftHandle::new(format!("route-{place}")),
                label: format!("Route {place}"),
                from: draft_place(place),
                to: draft_place(place + 1),
                access: AccessKind::Public,
                cost: Cost(1),
            }));
        }
        declarations.push(Declaration::Channel(ChannelDeclaration {
            handle: DraftHandle::new(format!("channel-{place}")),
            label: format!("Channel {place}"),
            reach: ReachRef::Place(draft_place(place)),
            controller: Some(draft_person(place * 10)),
        }));
        declarations.push(Declaration::Entity(EntityDeclaration {
            handle: DraftHandle::new(format!("grain-{place}")),
            label: format!("Grain {place}"),
            kind: EntityKind::Resource,
            container: None,
        }));
        declarations.push(Declaration::Fact(FactDeclaration {
            handle: DraftHandle::new(format!("fact-{place}")),
            label: format!("Fact {place}"),
            statement: Statement::new("The place keeps its hour.").unwrap(),
            standing: FactStandingRef::Canonical { evidence: EvidenceRef::new(TITHE_RECEIPT) },
        }));
        let institution = DraftHandle::new(format!("institution-{place}"));
        declarations.push(Declaration::Subject(SubjectDeclaration {
            handle: institution.clone(),
            label: format!("Institution {place}"),
            kind: SubjectKind::Institution,
            affordances: BTreeSet::new(),
            position: None,
            import_key: None,
        }));
        offices.push(ComponentOp::GrantAuthority {
            holder: Ref::Draft(institution.clone()),
            grant: AuthorityGrantRef {
                kind: authority_kind(LEVY_KIND),
                over: AuthorityTargetRef::PlaceSubtree(draft_place(place)),
            },
        });
        offices.push(ComponentOp::OpenOffice {
            institution: Ref::Draft(institution.clone()),
            office: office(WARDEN_OFFICE),
            delegated: BTreeSet::from([authority_kind(LEVY_KIND)]),
        });
        offices.push(ComponentOp::InstallIncumbent {
            institution: Ref::Draft(institution.clone()),
            office: office(WARDEN_OFFICE),
            incumbent: draft_person(place * 10),
        });
        offices.push(ComponentOp::OpenForum {
            grievance: GrievanceKindName(format!("grievance_{place}")),
            forum: Ref::Draft(institution),
            standing: AuthorityTargetRef::PlaceSubtree(draft_place(place)),
        });
    }
    for person in 0..persons {
        let place = person / 10;
        // One holding and one known fact per person, so `holdings`, `facts` and
        // `knowledge` grow with the world and a scan of them breaks the gate.
        offices.push(ComponentOp::Admit {
            holder: draft_person(person),
            resource: Ref::Draft(DraftHandle::new(format!("grain-{place}"))),
            qty: Quantity(1),
            evidence: EvidenceRef::new(TITHE_RECEIPT),
        });
        offices.push(ComponentOp::AcquireKnowledge {
            subject: draft_person(person),
            fact: Ref::Draft(DraftHandle::new(format!("fact-{place}"))),
            source: AuthoredSource::Witnessed,
            confidence: Confidence::Believed,
        });
        declarations.push(Declaration::Subject(SubjectDeclaration {
            handle: DraftHandle::new(format!("person-{person}")),
            label: format!("Person {person}"),
            kind: SubjectKind::Person,
            affordances: kernel_speak_grant(),
            position: Some(Ref::Draft(DraftHandle::new(format!("place-{}", person / 10)))),
            import_key: None,
        }));
    }
    let retires = |person: usize| person >= 35 && (person - 35) % 10 == 0;
    for person in 0..persons {
        if person > 0 {
            // The previous person, or the one before when that one retires and
            // so leaves the topology: the dependency stays satisfied.
            let previous = if retires(person - 1) { person - 2 } else { person - 1 };
            offices.push(ComponentOp::Bind {
                subject: draft_person(person),
                target: DependencyRef::Subject(draft_person(previous)),
            });
        }
        if person < 2 {
            offices.push(ComponentOp::Bind {
                subject: draft_person(person),
                target: DependencyRef::Resource(Ref::Draft(DraftHandle::new(format!(
                    "grain-{}",
                    places - 1
                )))),
            });
        }
    }
    let (mut kernel, _) = WorldKernel::create(
        CreateWorld {
            owner: owner(),
            title: "Clockwork".into(),
            patch: WorldPatch {
                declarations,
                operations: offices,
                evidence: vec![EvidenceRef::new(TITHE_RECEIPT)],
            },
            grammar: None,
        },
        &auth_principal(owner()),
    )
    .expect("the clockwork world is created");
    let people: Vec<_> = (0..persons)
        .map(|person| subject_by_label(&kernel.state, &format!("Person {person}")))
        .collect();
    let period = (persons / 2) as u64;
    let grain_of_place: std::collections::BTreeMap<String, crate::EntityId> = kernel
        .state
        .entities
        .iter()
        .filter(|(_, record)| record.kind == EntityKind::Resource)
        .map(|(id, record)| (record.label.clone(), *id))
        .collect();
    let mut routines: Vec<ComponentOp> = people
        .iter()
        .enumerate()
        .map(|(index, subject)| ComponentOp::CreateCommitment {
            subject: Ref::Existing(*subject),
            counterparty: None,
            kind: CommitmentKind::Routine,
            due: FictionalMinutes(1 + index as u64 % period),
            period: Some(TickMinutes::new(period as u32).unwrap()),
            checks: vec![PreconditionRef::Holds {
                resource: Ref::Existing(grain_of_place[&format!("Grain {}", index / 10)]),
                at_least: Quantity(1),
            }],
            statement: Statement::new("Keeps the hour.").unwrap(),
        })
        .collect();
    routines.extend(people[..2].iter().map(|subject| ComponentOp::CreateCommitment {
        subject: Ref::Existing(*subject),
        counterparty: Some(Ref::Existing(people[2])),
        kind: CommitmentKind::Obligation,
        due: FictionalMinutes(1),
        period: None,
        checks: Vec::new(),
        statement: Statement::new("Owes the tithe.").unwrap(),
    }));
    let placing = CommandEnvelope {
        world_id: kernel.state.header.world_id,
        expected_revision: kernel.state.revision,
        caller: CallerId::Play,
        body: operations(routines),
    };
    kernel
        .submit(placing, &AuthenticatedCaller::verified_play())
        .expect("the routines are placed");
    let retired: Vec<ComponentOp> = (0..persons)
        .filter(|person| retires(*person))
        .map(|person| ComponentOp::Retire { subject: Ref::Existing(people[person]) })
        .collect();
    if !retired.is_empty() {
        let envelope = clockwork_envelope(&kernel, operations(retired));
        kernel
            .submit(envelope, &AuthenticatedCaller::verified_play())
            .expect("the retirements land");
    }
    // One period on: every routine is past due, the live ones roll once.
    let envelope = clockwork_envelope(&kernel, tick_body(period as u32));
    kernel
        .submit(envelope, &AuthenticatedCaller::verified_play())
        .expect("the clock moves one period");
    let speak_entry = *kernel
        .state
        .affordance_catalog
        .iter()
        .find(|(_, entry)| entry.kind.0 == "speak")
        .map(|(id, _)| id)
        .expect("genesis admits Speak");
    (kernel, people, speak_entry)
}

fn clockwork_envelope(kernel: &WorldKernel, body: CommandBody) -> crate::CommandEnvelope {
    crate::CommandEnvelope {
        world_id: kernel.state.header.world_id,
        expected_revision: kernel.state.revision,
        caller: crate::CallerId::Play,
        body,
    }
}

/// Submits and reports the document digests the commit computed beside the
/// ones its change set implies: added + removed + 2 x updated, counted by
/// enumerating every document before and after.
fn submit_counting_digests(kernel: &mut WorldKernel, body: CommandBody) -> (usize, usize) {
    let before = kernel.state.documents().unwrap();
    let envelope = clockwork_envelope(kernel, body);
    crate::digest::DOC_DIGESTS.with(|count| count.set(0));
    kernel
        .submit(envelope, &AuthenticatedCaller::verified_play())
        .expect("the commit lands");
    let computed = crate::digest::DOC_DIGESTS.with(|count| count.get());
    let after = kernel.state.documents().unwrap();
    let added = after.keys().filter(|key| !before.contains_key(*key)).count();
    let removed = before.keys().filter(|key| !after.contains_key(*key)).count();
    let updated = after
        .iter()
        .filter(|(key, digest)| before.get(*key).is_some_and(|old| old != *digest))
        .count();
    (computed, added + removed + 2 * updated)
}

/// A commit's digest work is what it changed, not what the world holds: a tick
/// and a speech cost the same number of document digests at 100 persons and at
/// 1600, and exactly Added + Removed + 2 x Updated of that commit.
#[test]
fn digest_work_is_equal_at_100_and_1600() {
    let mut worked = Vec::new();
    for persons in [100usize, 1600] {
        let (mut kernel, people, speak_entry) = clockwork_world(persons);
        let mut counts = Vec::new();
        for _ in 0..3 {
            counts.push(submit_counting_digests(&mut kernel, tick_body(1)));
        }
        for index in 0..3 {
            let speech = CommandBody::Act {
                actor: people[index],
                invocation: speak(speak_entry, &format!("Speech {index}.")),
            };
            counts.push(submit_counting_digests(&mut kernel, speech));
        }
        for (computed, implied) in &counts {
            assert!(*computed > 0, "a commit changed documents and digested none");
            assert_eq!(
                computed, implied,
                "{persons} persons: digests != Added + Removed + 2 x Updated"
            );
        }
        worked.push(counts);
    }
    assert_eq!(worked[0], worked[1], "digest work grew with the world");
}

/// The ways a failed apply is injected: the revision overflow a tick meets after
/// it has written its documents, and the failure after the digest is folded and
/// the commit sealed, the last point before the world is kept.
#[derive(Clone, Copy, Debug)]
enum Failure {
    RevisionOverflow,
    AfterDigest,
}

/// One command to fail, and the world to fail it in.
struct FailureCase {
    name: &'static str,
    kernel: WorldKernel,
    body: CommandBody,
}

/// Each case writes what its name says: the two ticks are checked against the
/// motion the clock derives for them, so a case cannot quietly stop writing.
fn failure_cases() -> Vec<FailureCase> {
    let mut cases = Vec::new();

    let (kernel, _, _) = clockwork_world(20);
    let to = kernel.state.header.now.checked_add(TickMinutes::new(1).unwrap()).unwrap();
    assert!(!crate::clock::derive_motion(&kernel.state, to).fulfilled.is_empty());
    cases.push(FailureCase { name: "a tick that rolls routines", kernel, body: tick_body(1) });

    let (kernel, _, _) = crate::clock_tests::clock_kernel("Pressure");
    let to = kernel.state.header.now.checked_add(TickMinutes::new(200).unwrap()).unwrap();
    assert!(!crate::clock::derive_motion(&kernel.state, to).pressed.is_empty());
    cases.push(FailureCase { name: "a tick that writes pressure rows", kernel, body: tick_body(200) });

    let (kernel, _) = golden_session();
    let human = subject_by_label(&kernel.state, "The Human");
    let snapshot = kernel.snapshot().unwrap();
    let body = CommandBody::Act {
        actor: human,
        invocation: speak(first_grant(&snapshot, human), "Order, please."),
    };
    cases.push(FailureCase { name: "a speech", kernel, body });

    let (kernel, _) = golden_session();
    let witness = subject_by_label(&kernel.state, "The Witness");
    let (fact, _) = kernel.state.knowledge_of(witness).next().expect("the witness knows");
    let body = operations(vec![ComponentOp::Forget {
        subject: Ref::Existing(witness),
        fact: Ref::Existing(*fact),
    }]);
    cases.push(FailureCase { name: "a forget", kernel, body });
    cases
}

/// A failed apply leaves nothing behind: state, accumulator, digest, every
/// partition's bytes, every log and the journal as before, and the next honest
/// command commits. Each case is failed twice: by the revision overflow a tick
/// meets after it has written its documents, and by a failure after the digest
/// is folded and the commit sealed, the last point before the world is kept.
#[test]
fn a_failed_apply_rolls_back() {
    for failure in [Failure::RevisionOverflow, Failure::AfterDigest] {
        for FailureCase { name, mut kernel, body } in failure_cases() {
            let label = format!("{name}, {failure:?}");
            let honest_revision = kernel.state.revision;
            let before = kernel.state.clone();
            let bytes = before.encoded_documents();
            let commits = kernel.journal.commit_count();
            match failure {
                Failure::RevisionOverflow => kernel.state.revision = u64::MAX,
                Failure::AfterDigest => crate::INJECT_FAILURE_AFTER_DIGEST.set(true),
            }
            let envelope = clockwork_envelope(&kernel, body.clone());
            let error = kernel
                .submit(envelope, &AuthenticatedCaller::verified_play())
                .unwrap_err();
            crate::INJECT_FAILURE_AFTER_DIGEST.set(false);
            match failure {
                Failure::RevisionOverflow => {
                    assert!(matches!(error, KernelError::Serialization(_)), "{label}: {error:?}");
                }
                Failure::AfterDigest => {
                    assert!(matches!(error, KernelError::Invariant(_)), "{label}: {error:?}");
                }
            }
            kernel.state.revision = honest_revision;
            assert_eq!(kernel.state, before, "{label}: the failure left writes behind");
            assert_eq!(kernel.state.encoded_documents(), bytes, "{label}");
            assert_eq!(kernel.journal.commit_count(), commits, "{label}");
            assert!(kernel.state.header.is_clean(), "{label}: header log open");
            for (schema, docs) in kernel.state.partitions() {
                assert!(docs.is_clean(), "{label}: {schema} log open");
            }
            assert_eq!(
                kernel.state.full_state_digest().unwrap(),
                kernel.state.state_digest,
                "{label}: the accumulator no longer matches the documents"
            );
            assert_indexes_derived(&kernel.state, &format!("{label}, after the failure"));
            assert!(
                kernel.state.views.same_as(&crate::views::Views::rebuild(&kernel.state)),
                "{label}: the failure left the views behind"
            );

            let envelope = clockwork_envelope(&kernel, body);
            kernel
                .submit(envelope, &AuthenticatedCaller::verified_play())
                .unwrap_or_else(|error| panic!("{label}: the next honest command: {error:?}"));
            assert_eq!(kernel.state.full_state_digest().unwrap(), kernel.state.state_digest);
            assert_indexes_derived(&kernel.state, &format!("{label}, after the honest command"));
            assert!(
                kernel.state.views.same_as(&crate::views::Views::rebuild(&kernel.state)),
                "{label}: the views left their documents"
            );
        }
    }
}

/// Worlds that between them hold a document in every partition: the golden
/// session, the civic, custody and speech benches and the ticked clock bench.
fn populated_worlds() -> Vec<crate::WorldState> {
    use crate::tests::{auth_principal, civic_world, creation, custody_world, owner, speech_world};
    let created = |title: &str| {
        WorldKernel::create(creation(title), &auth_principal(owner())).unwrap().0
    };
    let mut worlds = vec![golden_session().0.state];
    let mut kernel = created("Civic");
    civic_world(&mut kernel);
    worlds.push(kernel.state);
    let mut kernel = created("Custody");
    custody_world(&mut kernel);
    worlds.push(kernel.state);
    let mut kernel = created("Speech");
    speech_world(&mut kernel);
    worlds.push(kernel.state);
    let (mut kernel, _, _) = crate::clock_tests::clock_kernel("Pressure");
    let tick = clockwork_envelope(&kernel, tick_body(200));
    kernel.submit(tick, &AuthenticatedCaller::verified_play()).unwrap();
    worlds.push(kernel.state);
    worlds
}

/// Membership of the partition table is behaviour: for every partition the
/// declaration names, one written document is in the folded digest and agrees
/// with the full recompute, is put back by a rollback, and leaves the log empty
/// and the accumulator at the full recompute when the transaction finishes. The
/// loop runs over the declaration, so a partition added to it is covered, and a
/// partition no fixture populates fails here rather than going untested.
#[test]
fn every_partition_is_digested_undone_and_finished() {
    let worlds = populated_worlds();
    for index in 0..crate::WorldState::PARTITIONS {
        let schema = worlds[0].partitions()[index].0;
        let base = worlds
            .iter()
            .find(|world| world.partitions()[index].1.rows() > 0)
            .unwrap_or_else(|| panic!("no fixture world holds a document in {schema}"));
        let written = |state: &mut crate::WorldState| {
            state.begin();
            assert!(state.partitions_mut()[index].1.remove_first(), "{schema}: nothing to write");
            let folded = state.folded().unwrap();
            assert_ne!(folded, state.accumulator, "{schema}: the write is not in the digest");
            assert_eq!(
                folded.state_digest(state.revision),
                state.full_state_digest().unwrap(),
                "{schema}: the folded digest left the full recompute"
            );
            folded
        };
        let clean = |state: &crate::WorldState| {
            state.header.is_clean() && state.partitions().iter().all(|(_, docs)| docs.is_clean())
        };

        let mut state = base.clone();
        written(&mut state);
        assert!(!state.partitions()[index].1.is_clean(), "{schema}: the write is not logged");
        state.rollback();
        assert_eq!(&state, base, "{schema}: rollback left the write behind");
        assert_eq!(state.encoded_documents(), base.encoded_documents(), "{schema}");
        assert!(clean(&state), "{schema}: rollback left a log open");

        let mut state = base.clone();
        let folded = written(&mut state);
        state.finish(folded);
        assert!(clean(&state), "{schema}: finish left a log open");
        assert_ne!(&state, base, "{schema}: finish dropped the write");
        assert_eq!(
            state.accumulator.state_digest(state.revision),
            state.full_state_digest().unwrap(),
            "{schema}: the finished accumulator left the full recompute"
        );
    }
}

/// What one tick and one speech cost to commit as the world grows, by the
/// clockwork world above: time, document digests and rows read (Docs rows plus
/// index entries) per commit, and the 1600/100 ratios. Run on its own, in
/// release, to read the numbers; nothing is asserted here. Wall time is noise
/// at this scale (the same code ran a tick ratio of 2.8 to 4.9 over five
/// runs), so the gate is `read_work_is_equal_at_100_and_1600`, which counts
/// the rows each commit reads and fails when one grows with the world, and
/// `digest_work_is_equal_at_100_and_1600` for what a commit digests.
///
/// `cargo test --release --lib clockwork_commit_cost -- --ignored --nocapture`
#[test]
#[ignore]
fn clockwork_commit_cost() {
    println!(
        "persons  create_ms  tick_ms  tick_digests  tick_rows  speech_ms  speech_digests  speech_rows"
    );
    let mut ticks_ms = Vec::new();
    let mut speeches_ms = Vec::new();
    for persons in [100usize, 400, 1600] {
        let started = Instant::now();
        let (mut kernel, people, speak_entry) = clockwork_world(persons);
        let create_ms = started.elapsed().as_secs_f64() * 1e3;
        let play = AuthenticatedCaller::verified_play();

        let (mut ticks, mut tick_digests, mut tick_rows) = (0.0, 0, 0);
        for _ in 0..30 {
            let tick = clockwork_envelope(&kernel, tick_body(1));
            crate::digest::DOC_DIGESTS.with(|count| count.set(0));
            let rows = crate::docs::rows_read();
            let started = Instant::now();
            kernel.submit(tick, &play).expect("the tick commits");
            ticks += started.elapsed().as_secs_f64() * 1e3;
            tick_digests = crate::digest::DOC_DIGESTS.with(|count| count.get());
            tick_rows = crate::docs::rows_read() - rows;
        }
        let (mut speeches, mut speech_digests, mut speech_rows) = (0.0, 0, 0);
        for index in 0..30 {
            let act = clockwork_envelope(
                &kernel,
                CommandBody::Act {
                    actor: people[index],
                    invocation: speak(speak_entry, &format!("Speech {index}.")),
                },
            );
            crate::digest::DOC_DIGESTS.with(|count| count.set(0));
            let rows = crate::docs::rows_read();
            let started = Instant::now();
            kernel.submit(act, &play).expect("the speech commits");
            speeches += started.elapsed().as_secs_f64() * 1e3;
            speech_digests = crate::digest::DOC_DIGESTS.with(|count| count.get());
            speech_rows = crate::docs::rows_read() - rows;
        }
        println!(
            "{persons:>7}  {create_ms:>9.1}  {:>7.3}  {tick_digests:>12}  {tick_rows:>9}  {:>9.3}  {speech_digests:>14}  {speech_rows:>11}",
            ticks / 30.0,
            speeches / 30.0,
        );
        ticks_ms.push(ticks / 30.0);
        speeches_ms.push(speeches / 30.0);
    }
    println!(
        "ratio 1600/100: tick {:.2}  speech {:.2}",
        ticks_ms[2] / ticks_ms[0],
        speeches_ms[2] / speeches_ms[0]
    );
}

/// A document written back to what it was costs no digest: the transaction
/// logged it, and folding finds nothing changed. A real change costs two (the
/// old, subtracted, and the new, added) and moves the accumulator.
#[test]
fn a_touched_document_written_back_costs_no_digest() {
    let (mut kernel, _) = golden_session();
    let witness = subject_by_label(&kernel.state, "The Witness");
    let before = kernel.state.accumulator.clone();
    let digests = || crate::digest::DOC_DIGESTS.with(|count| count.get());

    kernel.state.begin();
    let now = kernel.state.header.now;
    kernel.state.header.get_mut().now = now;
    kernel.state.commitments.get_mut(&witness).expect("the witness holds a routine");
    crate::digest::DOC_DIGESTS.with(|count| count.set(0));
    assert_eq!(kernel.state.folded().unwrap(), before);
    assert_eq!(digests(), 0, "an unchanged document was digested");

    kernel.state.header.get_mut().now = FictionalMinutes(now.0 + 1);
    crate::digest::DOC_DIGESTS.with(|count| count.set(0));
    assert_ne!(kernel.state.folded().unwrap(), before);
    assert_eq!(digests(), 2, "an updated document is its old digest and its new");
    kernel.state.rollback();
    assert_eq!(kernel.state.header.now, now);
}

/// A transaction is never opened over an open one, nor over a stray write.
#[cfg(debug_assertions)]
#[test]
#[should_panic(expected = "a transaction is open")]
fn begin_refuses_an_open_header_log() {
    let (mut kernel, _) = golden_session();
    kernel.state.header.get_mut();
    kernel.state.begin();
}

#[cfg(debug_assertions)]
#[test]
#[should_panic]
fn begin_refuses_an_open_partition_log() {
    let (mut kernel, _) = golden_session();
    let witness = subject_by_label(&kernel.state, "The Witness");
    kernel.state.commitments.get_mut(&witness);
    kernel.state.begin();
}

/// The snapshot's `known_by` is the knowledge partition read the other way:
/// exactly the subjects holding a row for the fact.
#[test]
fn a_fact_is_known_by_exactly_the_subjects_holding_a_row() {
    let (kernel, _) = golden_session();
    let snapshot = kernel.snapshot().unwrap();
    assert!(snapshot.facts.iter().any(|fact| !fact.known_by.is_empty()));
    for fact in &snapshot.facts {
        let holders: Vec<_> = kernel
            .state
            .subjects
            .keys()
            .filter(|subject| kernel.state.knowledge.contains_key(&(**subject, fact.id)))
            .copied()
            .collect();
        assert_eq!(fact.known_by, holders);
    }
}

/// A knowledge row names a live subject and a canonical fact, each judged on
/// its own.
#[test]
fn a_knowledge_row_without_a_subject_or_a_fact_is_corrupt() {
    use crate::tests::subject_by_label;
    let (kernel, _) = golden_session();
    let witness = subject_by_label(&kernel.state, "The Witness");
    let (fact, held) = kernel
        .state
        .knowledge_of(witness)
        .next()
        .map(|(fact, held)| (*fact, *held))
        .expect("the witness knows something");
    crate::journal::verify_state_shape(&kernel.state).expect("the honest state passes");

    let mut no_subject = kernel.state.clone();
    no_subject.knowledge.insert((crate::SubjectId::issue(), fact), held);
    assert!(crate::journal::verify_state_shape(&no_subject).is_err());

    let mut no_fact = kernel.state.clone();
    no_fact.knowledge.insert((witness, crate::EntityId::issue()), held);
    assert!(crate::journal::verify_state_shape(&no_fact).is_err());
}

/// The text a document is keyed by is the contract the digest hashes: an id's
/// uuid, a pair's two joined by a slash, a grievance kind's name.
#[test]
fn document_keys_are_canonical_text() {
    use crate::docs::DocKey;
    let subject = crate::SubjectId::issue();
    let fact = crate::EntityId::issue();
    assert_eq!(subject.key_text(), subject.0.to_string());
    assert_eq!((subject, fact).key_text(), format!("{}/{}", subject.0, fact.0));
    assert_eq!(crate::patch::GrievanceKindName("theft".into()).key_text(), "theft");
}

/// The snapshot says a commitment is past due exactly when its due minute has
/// been reached: the golden's routine rolled once by a thirty-minute tick and
/// is due again at or before the clock.
#[test]
fn a_commitment_due_at_or_before_the_clock_reads_past_due() {
    let (kernel, _) = golden_session();
    let witness = subject_by_label(&kernel.state, "The Witness");
    let snapshot = kernel.snapshot().unwrap();
    let subject = snapshot
        .subjects
        .iter()
        .find(|subject| subject.id == witness)
        .expect("the witness is a subject");
    assert!(!subject.commitments.is_empty());
    for commitment in &subject.commitments {
        assert_eq!(commitment.past_due, commitment.due <= kernel.state.header.now);
    }
    assert!(
        subject.commitments.iter().any(|commitment| commitment.past_due),
        "the routine fell due behind the clock"
    );
}

/// Rows (Docs rows and index entries) one submit read.
fn submit_counting_rows(kernel: &mut WorldKernel, body: CommandBody) -> u64 {
    let envelope = clockwork_envelope(kernel, body);
    let before = crate::docs::rows_read();
    kernel
        .submit(envelope, &AuthenticatedCaller::verified_play())
        .expect("the commit lands");
    crate::docs::rows_read() - before
}

/// A commit's read work is what it touches, not what the world holds: a tick
/// that rolls two routines and presses two obligations, and a colocated speech,
/// read the same number of rows at 100 persons and at 1600. The count is taken
/// where reads are owned (`Docs`), so a reader that scans a partition cannot
/// hide from it. Wall time is not asserted anywhere: it is noise at this scale.
#[test]
fn read_work_is_equal_at_100_and_1600() {
    let mut worked = Vec::new();
    for persons in [100usize, 1600] {
        let (mut kernel, people, speak_entry) = clockwork_world(persons);
        // The gate holds only if the partitions are populated at scale: an empty
        // one hides a scan of it.
        let state = &kernel.state;
        assert!(state.holdings.len() >= persons && state.knowledge.len() >= persons);
        assert!(state.facts.len() >= persons / 10 && state.channels.len() == persons / 10);
        let retired: Vec<_> =
            people.iter().copied().filter(|person| crate::is_retired(state, *person)).collect();
        assert!(retired.len() >= persons / 20, "{persons} persons: too few retired subjects");
        assert_eq!(state.positions.len() + retired.len(), persons, "a retired subject has no position");
        assert!(state.commitments.len() >= persons);
        for person in &retired {
            assert!(
                state.commitments[person].values().any(|commitment| {
                    commitment.kind == CommitmentKind::Routine && commitment.due <= state.header.now
                }),
                "{persons} persons: a retired subject holds no past-due routine"
            );
        }
        for held in state.commitments.values() {
            for commitment in held.values().filter(|c| c.kind == CommitmentKind::Routine) {
                assert!(
                    matches!(commitment.checks.as_slice(), [crate::patch::BoundPrecondition::Holds { .. }]),
                    "{persons} persons: a routine without its Holds check"
                );
            }
        }
        assert!(state.edges.len() + 1 == persons / 10 && state.selection.len() == persons / 10);
        // A dependency per person and a forum per place, so a walk of either
        // is seen; the clock reads only the two that fail.
        assert!(state.dependencies.len() >= persons);
        assert_eq!(state.redress.len(), persons / 10);
        let mut counts = Vec::new();
        for _ in 0..3 {
            counts.push(submit_counting_rows(&mut kernel, tick_body(1)));
        }
        for index in 0..3 {
            let speech = CommandBody::Act {
                actor: people[index],
                invocation: speak(speak_entry, &format!("Speech {index}.")),
            };
            counts.push(submit_counting_rows(&mut kernel, speech));
        }
        assert!(counts.iter().all(|rows| *rows > 0), "{persons} persons: a commit read nothing");
        worked.push(counts);
    }
    assert_eq!(worked[0], worked[1], "read work grew with the world");
}

/// The reads a clockwork submit does not reach, inside one open transaction
/// each: who stands under a two-level subtree, discharging a commitment while
/// the world holds N pressure rows from other sources, one incumbent's
/// delegated authority, and a path of three routes. (An `AdmitPatch` resolves
/// against the whole world until follow-up `aetherlight-resolver-reads-world`,
/// so no submit shape reaches these.)
#[test]
fn operation_reads_are_equal_at_100_and_1600() {
    use crate::patch::ResolvedOp;
    use crate::{Cost, PressureSource};
    use std::collections::BTreeSet;

    let mut worked = Vec::new();
    for persons in [100usize, 1600] {
        let (mut kernel, people, _) = clockwork_world(persons);
        kernel
            .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
            .expect("the tick presses the obligations");
        let place = |label: &str| {
            *kernel
                .state
                .entities
                .iter()
                .find(|(_, record)| record.label == label)
                .expect("a declared place")
                .0
        };
        let (zero, one, two, three) =
            (place("Place 0"), place("Place 1"), place("Place 2"), place("Place 3"));

        // N pressure rows from other sources, committed before the measured transaction.
        kernel.state.begin();
        for person in &people {
            kernel.state.pressures.entry(*person).or_default().insert(
                PressureSource::Subject(people[0]),
                crate::patch::PressureMagnitude(1),
            );
        }
        let folded = kernel.state.folded().unwrap();
        kernel.state.finish(folded);

        let state = &mut kernel.state;
        let obligation = state.commitments[&people[0]]
            .iter()
            .find(|(_, commitment)| commitment.kind == CommitmentKind::Obligation)
            .map(|(key, _)| *key)
            .expect("the first person owes an obligation");

        state.begin();
        // Place 1 inside place 0, place 2 inside place 1.
        state.entities.get_mut(&one).unwrap().container = Some(zero);
        state.entities.get_mut(&two).unwrap().container = Some(one);

        let mut counts = Vec::new();
        let mut measure = |read: &mut dyn FnMut()| {
            let before = crate::docs::rows_read();
            read();
            let rows = crate::docs::rows_read() - before;
            assert!(rows > 0, "a read counted nothing");
            counts.push(rows);
        };
        let mut under = BTreeSet::new();
        measure(&mut || under = crate::under_place(state, zero));
        assert_eq!(under.len(), 30, "places 0, 1 and 2 hold ten persons each");
        let mut delegated = Default::default();
        measure(&mut || delegated = crate::delegated_authority(state, people[0]));
        assert_eq!(delegated.len(), 1, "the first person holds one office");
        let authority = BTreeSet::new();
        let mut reached = false;
        measure(&mut || reached = crate::action::reachable(state, people[0], &authority, three, Cost(3)));
        assert!(reached, "three routes of cost one reach place 3");
        let mut applied = Ok(());
        let revision = state.revision + 1;
        measure(&mut || {
            applied = crate::apply_operation(
                state,
                &ResolvedOp::DischargeCommitment { subject: people[0], key: obligation },
                &[],
                revision,
            );
        });
        applied.expect("the obligation is discharged");
        assert!(
            state.pressures[&people[0]]
                .keys()
                .all(|source| !matches!(source, PressureSource::Commitment { key, .. } if *key == obligation)),
            "the pressure the obligation sourced is gone"
        );
        state.rollback();
        worked.push(counts);
    }
    assert_eq!(worked[0], worked[1], "an operation's read work grew with the world");
}

fn assert_indexes_derived(state: &crate::WorldState, label: &str) {
    for (schema, docs) in state.partitions() {
        assert!(docs.index_is_derived(), "{label}: the read index of {schema} left its documents");
    }
}

/// The states a test walks: every revision of the golden session as replay
/// derives it, the live and reopened golden world, each populated world, and
/// forty commits of the clockwork world.
fn walked_states() -> Vec<(String, crate::WorldState)> {
    let mut states = Vec::new();
    let (kernel, _) = golden_session();
    let commits = kernel.journal.commits_for_test();
    for index in 0..commits.len() {
        let replayed = crate::journal::verify_history(&commits[..=index]).unwrap();
        states.push((format!("golden revision {index}"), replayed));
    }
    let reopened = WorldKernel::open(&kernel.export().unwrap()).unwrap();
    states.push(("golden reopened".into(), reopened.state));
    states.push(("golden live".into(), kernel.state));
    for (index, world) in populated_worlds().into_iter().enumerate() {
        states.push((format!("populated world {index}"), world));
    }
    let (mut kernel, people, speak_entry) = clockwork_world(100);
    states.push(("clockwork created".into(), kernel.state.clone()));
    for step in 0..40 {
        let body = if step % 2 == 0 {
            tick_body(1)
        } else {
            CommandBody::Act {
                actor: people[step % 30],
                invocation: speak(speak_entry, &format!("Speech {step}.")),
            }
        };
        let envelope = clockwork_envelope(&kernel, body);
        kernel.submit(envelope, &AuthenticatedCaller::verified_play()).unwrap();
        states.push((format!("clockwork commit {step}"), kernel.state.clone()));
    }
    let reopened = WorldKernel::open(&kernel.export().unwrap()).unwrap();
    states.push(("clockwork reopened".into(), reopened.state));
    states.push(("multivalue world".into(), multivalue_world()));
    states.extend(view_scenario());
    states
}

/// The commands that move what the views join, one at a time, each state kept:
/// dependencies bound on a route, a resource and a subject; the route closed
/// and reopened; the resource's holding emptied and refilled; a depended-on
/// subject loses its position; a dependent released; and a subject that holds
/// past-due routines and a failing dependency retires.
fn view_scenario() -> Vec<(String, crate::WorldState)> {
    use crate::patch::{DependencyRef, EvidenceRef, Quantity};
    use crate::tests::TITHE_RECEIPT;
    let (mut kernel, people, _) = clockwork_world(20);
    let route = *kernel.state.edges.keys().next().expect("a route");
    let grain = |label: &str| {
        *kernel
            .state
            .entities
            .iter()
            .find(|(_, record)| record.label == label)
            .expect("a declared resource")
            .0
    };
    let (grain_0, grain_1) = (grain("Grain 0"), grain("Grain 1"));
    let bind = |subject: usize, target: DependencyRef| ComponentOp::Bind {
        subject: Ref::Existing(people[subject]),
        target,
    };
    let steps: Vec<(&str, CommandBody)> = vec![
        (
            "binds",
            operations(vec![
                bind(5, DependencyRef::Route(Ref::Existing(route))),
                bind(6, DependencyRef::Resource(Ref::Existing(grain_1))),
                bind(7, DependencyRef::Subject(Ref::Existing(people[8]))),
                bind(7, DependencyRef::Resource(Ref::Existing(grain_0))),
                bind(3, DependencyRef::Subject(Ref::Existing(people[4]))),
            ]),
        ),
        ("the route closes", operations(vec![ComponentOp::CloseRoute { route: Ref::Existing(route) }])),
        ("the route reopens", operations(vec![ComponentOp::OpenRoute { route: Ref::Existing(route) }])),
        (
            "a holding empties",
            operations(vec![ComponentOp::Consume {
                holder: Ref::Existing(people[7]),
                resource: Ref::Existing(grain_0),
                qty: Quantity(1),
            }]),
        ),
        (
            "the holding refills",
            CommandBody::AdmitPatch {
                patch: WorldPatch {
                    declarations: Vec::new(),
                    operations: vec![ComponentOp::Admit {
                        holder: Ref::Existing(people[7]),
                        resource: Ref::Existing(grain_0),
                        qty: Quantity(1),
                        evidence: EvidenceRef::new(TITHE_RECEIPT),
                    }],
                    evidence: vec![EvidenceRef::new(TITHE_RECEIPT)],
                },
            },
        ),
        ("the route closes again", operations(vec![ComponentOp::CloseRoute { route: Ref::Existing(route) }])),
        ("a long tick", tick_body(30)),
        ("a depended-on subject retires", operations(vec![ComponentOp::Retire { subject: Ref::Existing(people[8]) }])),
        (
            "a dependent is released",
            operations(vec![ComponentOp::Release {
                subject: Ref::Existing(people[3]),
                target: DependencyRef::Subject(Ref::Existing(people[4])),
            }]),
        ),
        ("a failing, past-due subject retires", operations(vec![ComponentOp::Retire { subject: Ref::Existing(people[6]) }])),
        ("the route reopens after the retirement", operations(vec![ComponentOp::OpenRoute { route: Ref::Existing(route) }])),
        ("a last tick", tick_body(30)),
    ];
    let mut states = vec![("scenario created".to_owned(), kernel.state.clone())];
    for (name, body) in steps {
        let envelope = clockwork_envelope(&kernel, body);
        kernel
            .submit(envelope, &AuthenticatedCaller::verified_play())
            .unwrap_or_else(|error| panic!("{name}: {error:?}"));
        states.push((format!("scenario: {name}"), kernel.state.clone()));
    }
    states
}

/// A world where one document sits at several points: a pressure target fed by
/// three sources, and an institution whose offices have three distinct
/// incumbents, one of them in two offices. A projection that keeps only the
/// first or last of a document's points loses a row here and nowhere else.
fn multivalue_world() -> crate::WorldState {
    use crate::patch::{Office, PressureMagnitude};
    use crate::tests::{LEVY_KIND, authority_kind, office};
    use crate::PressureSource;
    let (mut kernel, people, _) = clockwork_world(20);
    kernel
        .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
        .expect("the tick commits");
    let mut state = kernel.state;
    let institution = *state.selection.keys().next().expect("an institution holds offices");
    state.begin();
    for source in [people[1], people[2], people[3]] {
        state
            .pressures
            .entry(people[5])
            .or_default()
            .insert(PressureSource::Subject(source), PressureMagnitude(1));
    }
    let delegated = std::collections::BTreeSet::from([authority_kind(LEVY_KIND)]);
    let offices = state.selection.get_mut(&institution).unwrap();
    for (name, incumbent) in [("bailiff", people[3]), ("steward", people[3]), ("reeve", people[4])] {
        offices.insert(office(name), Office { incumbent: Some(incumbent), delegated: delegated.clone() });
    }
    let folded = state.folded().unwrap();
    state.finish(folded);
    state
}

/// Every read index is the projection of its partition and nothing else, after
/// every commit the walked worlds make, however the world was reached.
#[test]
fn indexes_are_derived() {
    for (label, state) in walked_states() {
        assert_indexes_derived(&state, &label);
        // The rebuild above uses the projection itself, so a wrong projection
        // agrees with its own index; the scans it replaced do not.
        assert_eq!(reference_scan_disagreement(&state), None, "{label}");
    }
}

/// The first place an index read and the scan it replaced disagree, if any.
fn reference_scan_disagreement(state: &crate::WorldState) -> Option<String> {
    use crate::{Audience, Cost, PressureSource};
    let subjects: Vec<_> = state.subjects.keys().copied().collect();
    let places: Vec<_> = state
        .entities
        .iter()
        .filter(|(_, record)| record.kind == crate::EntityKind::Place)
        .map(|(id, _)| *id)
        .collect();
    for subject in &subjects {
        if crate::audience(state, *subject, &Audience::Colocated)
            != crate::audience_by_scan(state, *subject, &Audience::Colocated)
        {
            return Some(format!("colocated audience of {subject:?}"));
        }
        if crate::delegated_authority(state, *subject)
            != crate::delegated_authority_by_scan(state, *subject)
        {
            return Some(format!("delegated authority of {subject:?}"));
        }
        if crate::channels_controlled_by(state, *subject)
            != crate::channels_controlled_by_by_scan(state, *subject)
        {
            return Some(format!("channels controlled by {subject:?}"));
        }
        let authority = crate::subject_authority(state, *subject);
        for place in &places {
            if crate::action::reachable(state, *subject, &authority, *place, Cost(3))
                != crate::action::reachable_by_scan(state, *subject, &authority, *place, Cost(3))
            {
                return Some(format!("reachable {place:?} from {subject:?}"));
            }
        }
    }
    let targets: std::collections::BTreeSet<_> =
        state.dependencies.values().flat_map(|held| held.iter().copied()).collect();
    for target in targets {
        let mut by_index = crate::views::Views::dependents_of(state, target);
        by_index.sort();
        if by_index != crate::views::Views::dependents_by_scan(state, target) {
            return Some(format!("dependents of {target:?}"));
        }
    }
    for place in &places {
        if crate::under_place(state, *place) != crate::under_place_by_scan(state, *place) {
            return Some(format!("under_place {place:?}"));
        }
        if crate::routes_at(state, *place) != crate::routes_at_by_scan(state, *place) {
            return Some(format!("routes at {place:?}"));
        }
    }
    for (channel, _) in state.channels.iter() {
        let actor = subjects.first().copied()?;
        if crate::audience(state, actor, &Audience::Channel(*channel))
            != crate::audience_by_scan(state, actor, &Audience::Channel(*channel))
        {
            return Some(format!("audience of channel {channel:?}"));
        }
    }
    let mut sources: Vec<PressureSource> = state
        .pressures
        .iter()
        .flat_map(|(_, held)| held.keys().copied())
        .collect();
    sources.extend(subjects.first().map(|first| PressureSource::Subject(*first)));
    for source in sources {
        if crate::pressure_holders(state, source) != crate::pressure_holders_by_scan(state, source) {
            return Some(format!("holders of {source:?}"));
        }
    }
    None
}

/// Each read an index answers equals the scan it replaced, at every walked state.
#[test]
fn index_reads_equal_reference_scans() {
    for (label, state) in walked_states() {
        assert_eq!(reference_scan_disagreement(&state), None, "{label}");
    }
}

/// A read inside an open transaction sees what the transaction wrote, though
/// the index has not: a subject moved through `positions` leaves its old room
/// and joins the new one, a removed pressure row no longer lists its source, and
/// after the rollback the reads are what they were.
#[test]
fn an_index_read_mid_transaction_sees_the_log() {
    use crate::patch::Position;
    use crate::{Audience, PressureSource};
    let (mut kernel, people, _) = clockwork_world(20);
    kernel
        .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
        .expect("the tick presses the obligations");
    let state = &mut kernel.state;
    let (mover, in_old, in_new) = (people[1], people[2], people[11]);
    let new_place = state.positions[&in_new].place;
    assert_ne!(state.positions[&mover].place, new_place);
    let obligation = state.commitments[&people[0]]
        .iter()
        .find(|(_, commitment)| commitment.kind == CommitmentKind::Obligation)
        .map(|(key, _)| *key)
        .expect("the first person owes an obligation");
    let source = PressureSource::Commitment { subject: people[0], key: obligation };
    let reads = |state: &crate::WorldState| {
        (
            crate::audience(state, in_old, &Audience::Colocated),
            crate::audience(state, in_new, &Audience::Colocated),
            crate::pressure_holders(state, source),
        )
    };
    let before = reads(state);
    assert!(before.0.contains(&mover) && !before.1.contains(&mover));
    assert!(before.2.contains(&people[0]));

    state.begin();
    state.positions.insert(mover, Position { place: new_place });
    state.pressures.remove(&people[0]);
    let during = reads(state);
    assert!(!during.0.contains(&mover), "the old room still lists the mover");
    assert!(during.1.contains(&mover), "the new room does not list the mover");
    assert!(!during.2.contains(&people[0]), "a removed pressure row still lists its source");
    assert_eq!(reference_scan_disagreement(state), None, "mid-transaction reads equal the scans");
    state.rollback();

    assert_indexes_derived(state, "after the rollback");
    assert_eq!(reads(state), before);
}

/// The oracle can see an index that disagrees with its documents: damaged one
/// way (a document listed where it is not) or the other (a document left out),
/// every indexed partition reports it, and the reads the index serves then
/// differ from the scans they replaced.
#[test]
fn a_corrupted_index_is_detected() {
    use std::collections::BTreeSet;
    let mut worlds = populated_worlds();
    let (mut kernel, _, _) = clockwork_world(20);
    kernel
        .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
        .unwrap();
    worlds.push(kernel.state);
    let mut reported = BTreeSet::new();
    let mut misread = BTreeSet::new();
    for world in &worlds {
        for index in 0..crate::WorldState::PARTITIONS {
            for stray in [true, false] {
                let mut damaged = world.clone();
                if !damaged.partitions_mut()[index].1.corrupt_index_first(stray) {
                    continue;
                }
                let schema = damaged.partitions()[index].0;
                assert!(
                    !damaged.partitions()[index].1.index_is_derived(),
                    "{schema}: damage (stray {stray}) went unseen"
                );
                reported.insert(schema);
                if reference_scan_disagreement(&damaged).is_some() {
                    misread.insert(schema);
                }
            }
        }
    }
    let indexed: BTreeSet<&str> = [
        "aetherlight.entity.v1",
        "aetherlight.edge.v1",
        "aetherlight.position.v1",
        "aetherlight.channel.v1",
        "aetherlight.selection.v1",
        "aetherlight.dependencies.v1",
        "aetherlight.pressures.v1",
    ]
    .into();
    assert_eq!(reported, indexed, "exactly the indexed partitions can be damaged");
    assert_eq!(misread, indexed, "a damaged index must change what a reader answers");
}

/// A view is derived from the documents and nothing else: after every commit of
/// the walked worlds, however each was reached, it is what a scan of the
/// committed documents builds.
#[test]
fn views_are_derived() {
    for (label, state) in walked_states() {
        assert!(
            state.views.same_as(&crate::views::Views::rebuild(&state)),
            "{label}: the views left their documents"
        );
    }
}

/// The motion the views derive is the motion the scans derive: every live
/// subject's due commitments and failing dependencies, over short and long
/// spans, in every walked state (which holds routes that close and reopen,
/// holdings that empty, subjects that lose a position, and subjects that retire
/// holding past-due routines and failing dependencies).
#[test]
fn view_reads_equal_reference_scans() {
    let mut pressed_dependency = false;
    for (label, state) in walked_states() {
        for minutes in [1, 30, 5_000] {
            let to = FictionalMinutes(state.header.now.0 + minutes);
            let motion = crate::clock::derive_motion(&state, to);
            assert_eq!(motion, crate::clock::derive_motion_by_scan(&state, to), "{label}, {minutes} minutes");
            pressed_dependency |= motion
                .pressed
                .iter()
                .any(|written| matches!(written.source, crate::PressureSource::Dependency(_)));
        }
    }
    assert!(pressed_dependency, "no walked state had a failing dependency to compare");
}

/// A damaged view is seen by the oracle and changes what the clock derives: a
/// stray failing row presses a dependency nobody holds, and a missing due
/// point leaves its subjects unmoved.
#[test]
fn a_corrupted_view_is_detected() {
    let (mut kernel, _, _) = clockwork_world(20);
    kernel
        .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
        .unwrap();
    let state = kernel.state;
    let to = FictionalMinutes(state.header.now.0 + 30);
    assert!(state.views.same_as(&crate::views::Views::rebuild(&state)));
    for stray in [true, false] {
        let mut damaged = state.clone();
        assert!(damaged.views.corrupt_for_test(stray), "stray {stray}");
        assert!(
            !damaged.views.same_as(&crate::views::Views::rebuild(&damaged)),
            "stray {stray}: the oracle did not see the damage"
        );
        assert_ne!(
            crate::clock::derive_motion(&damaged, to),
            crate::clock::derive_motion_by_scan(&damaged, to),
            "stray {stray}: the damage changed no read"
        );
    }
}

/// A view is read only with clean logs: reading one while a partition it joins
/// has an open write is refused, not answered from a stale fold.
#[cfg(debug_assertions)]
#[test]
#[should_panic(expected = "open log")]
fn a_view_read_with_an_open_log_is_refused() {
    let (mut kernel, people, _) = clockwork_world(20);
    kernel.state.begin();
    kernel.state.positions.remove(&people[3]);
    crate::clock::derive_motion(&kernel.state, FictionalMinutes(1));
}

/// A dependency's pressure ends with the dependency: bind to a resource the
/// subject does not hold, let a tick press it, release it, and the row is gone
/// and the world still reopens from its export.
#[test]
fn releasing_a_dependency_removes_the_pressure_it_raised() {
    use crate::patch::DependencyRef;
    use crate::{DependencyTarget, PressureSource};
    let (mut kernel, people, _) = clockwork_world(20);
    let grain_1 = *kernel
        .state
        .entities
        .iter()
        .find(|(_, record)| record.label == "Grain 1")
        .expect("a declared resource")
        .0;
    let target = DependencyTarget::Resource(grain_1);
    let pressed = PressureSource::Dependency(target);
    let submit = |kernel: &mut WorldKernel, body| {
        let envelope = clockwork_envelope(kernel, body);
        kernel.submit(envelope, &AuthenticatedCaller::verified_play()).expect("the command lands");
    };
    submit(
        &mut kernel,
        operations(vec![ComponentOp::Bind {
            subject: Ref::Existing(people[6]),
            target: DependencyRef::Resource(Ref::Existing(grain_1)),
        }]),
    );
    submit(&mut kernel, tick_body(1));
    assert!(
        kernel.state.pressures.get(&people[6]).is_some_and(|held| held.contains_key(&pressed)),
        "the tick pressed the failing dependency"
    );
    submit(
        &mut kernel,
        operations(vec![ComponentOp::Release {
            subject: Ref::Existing(people[6]),
            target: DependencyRef::Resource(Ref::Existing(grain_1)),
        }]),
    );
    assert!(
        kernel.state.pressures.get(&people[6]).is_none_or(|held| !held.contains_key(&pressed)),
        "the released dependency's pressure row stayed"
    );
    let reopened = WorldKernel::open(&kernel.export().unwrap()).expect("the world reopens");
    assert_eq!(reopened.state, kernel.state);
}
