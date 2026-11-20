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
/// institution per place with a warden office held by that person, and two
/// Obligations past due from the first minute, so a tick presses two sources
/// at every size.
fn clockwork_world(persons: usize) -> (WorldKernel, Vec<crate::SubjectId>, crate::AffordanceId) {
    use crate::patch::kernel_speak_grant;
    use crate::patch::{
        AccessKind, AuthoredSource, AuthorityGrantRef, AuthorityTargetRef, ChannelDeclaration,
        DependencyRef, EntityDeclaration, EvidenceRef, FactDeclaration, FactStandingRef, Quantity,
        ReachRef, RouteDeclaration, SubjectDeclaration,
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
            institution: Ref::Draft(institution),
            office: office(WARDEN_OFFICE),
            incumbent: draft_person(place * 10),
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
    // Two dependencies, a fixed count: `derive_motion` still walks every
    // dependency row (follow-up aetherlight-cross-partition-views), so a
    // dependency per person would grow a tick's reads with the world.
    for person in 0..2 {
        offices.push(ComponentOp::Bind {
            subject: draft_person(person),
            target: DependencyRef::Subject(draft_person(2)),
        });
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
    let mut routines: Vec<ComponentOp> = people
        .iter()
        .enumerate()
        .map(|(index, subject)| ComponentOp::CreateCommitment {
            subject: Ref::Existing(*subject),
            counterparty: None,
            kind: CommitmentKind::Routine,
            due: FictionalMinutes(1 + index as u64 % period),
            period: Some(TickMinutes::new(period as u32).unwrap()),
            checks: Vec::new(),
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

            let envelope = clockwork_envelope(&kernel, body);
            kernel
                .submit(envelope, &AuthenticatedCaller::verified_play())
                .unwrap_or_else(|error| panic!("{label}: the next honest command: {error:?}"));
            assert_eq!(kernel.state.full_state_digest().unwrap(), kernel.state.state_digest);
            assert_indexes_derived(&kernel.state, &format!("{label}, after the honest command"));
        }
    }
}

/// Worlds that between them hold a document in every partition: the golden
/// session, the civic, custody and speech benches and the ticked clock bench.
pub(crate) fn populated_worlds() -> Vec<crate::WorldState> {
    use crate::tests::{auth_principal, civic_world, creation, custody_world, owner, speech_world};
    let created = |title: &str| {
        WorldKernel::create(creation(title), &auth_principal(owner())).unwrap().0
    };
    let mut worlds = vec![golden_session().0.state];
    // Two subjects holding import keys, so the subjects' `ByImportKey` index has
    // points to read and to damage.
    let mut keyed = crate::tests::creation("Keyed");
    for declaration in &mut keyed.patch.declarations {
        if let Declaration::Subject(subject) = declaration {
            let record = subject.label.clone();
            subject.import_key = Some(crate::ImportKey::new("aetherlight.fixture", record));
        }
    }
    worlds.push(WorldKernel::create(keyed, &auth_principal(owner())).unwrap().0.state);
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
        assert!(state.positions.len() >= persons && state.commitments.len() >= persons);
        assert!(state.edges.len() + 1 == persons / 10 && state.selection.len() == persons / 10);
        // Not at scale, on purpose: `derive_motion` still walks every dependency
        // row, so a dependency per person would grow a tick's reads. The owner
        // is follow-up aetherlight-cross-partition-views; when it lands this
        // count flips to world scale (`>= persons`) so a scan of the partition
        // is seen.
        assert_eq!(state.dependencies.len(), 2);
        // The gate world opens no forum, so `redress` is empty here; the same
        // follow-up owns populating it at scale beside dependencies.
        assert_eq!(state.redress.len(), 0);
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

/// What a patch reads is what it touches, not what the world holds: ten patch
/// shapes, each submitted through `AdmitPatch` (resolution against the
/// candidate graph, the reducer, and the commit's own checks), read the same
/// number of rows at 100, 400 and 1600 persons (some read none: point reads are
/// not rows). The count is taken where reads
/// are owned (`Docs`), so a resolver arm that copies a partition, collects keys
/// from every subject, sums a resource over every holder, or judges every holder
/// for overlap cannot hide from it. The shapes run in order on one world, so
/// each leaves the world the next one reads.
#[test]
fn patch_read_work_is_equal_at_100_and_1600() {
    use crate::patch::{
        AuthorityGrantRef, AuthorityTargetRef, AuthoredSource, DependencyRef, FactDeclaration,
        FactStandingRef, PressureMagnitude, PressureSourceRef, Quantity, SubjectDeclaration,
    };
    use crate::tests::{LEVY_KIND, WARDEN_OFFICE, authority_kind, office};
    use crate::{Confidence, ImportKey, SubjectKind};
    use std::collections::BTreeSet;

    let mut shapes: Vec<&'static str> = Vec::new();
    let mut worked: Vec<Vec<u64>> = Vec::new();
    for persons in [100usize, 400, 1600] {
        let (mut kernel, people, _) = clockwork_world(persons);
        let label = |kernel: &WorldKernel, label: &str| {
            *kernel
                .state
                .entities
                .iter()
                .find(|(_, record)| record.label == label)
                .unwrap_or_else(|| panic!("{label} is declared"))
                .0
        };
        let (place_zero, grain, fact_one) = (
            label(&kernel, "Place 0"),
            label(&kernel, "Grain 0"),
            label(&kernel, "Fact 1"),
        );
        let route = *kernel
            .state
            .edges
            .iter()
            .find(|(_, record)| record.label() == "Route 0")
            .expect("the first route is declared")
            .0;
        let institution = subject_by_label(&kernel.state, "Institution 0");

        // The tick presses the two obligations; then every other person is
        // pressed by one source, so the pressure partition holds a row for each.
        kernel
            .submit(clockwork_envelope(&kernel, tick_body(1)), &AuthenticatedCaller::verified_play())
            .expect("the tick presses the obligations");
        let pressing: Vec<ComponentOp> = people[1..]
            .iter()
            .map(|person| ComponentOp::AdvancePressure {
                source: PressureSourceRef::Subject(Ref::Existing(people[0])),
                target: Ref::Existing(*person),
                by: PressureMagnitude(1),
            })
            .collect();
        kernel
            .submit(
                clockwork_envelope(&kernel, operations(pressing)),
                &AuthenticatedCaller::verified_play(),
            )
            .expect("the pressure rows land");
        assert!(kernel.state.pressures.len() >= persons, "the pressure partition is at scale");
        let obligation = kernel.state.commitments[&people[0]]
            .iter()
            .find(|(_, commitment)| commitment.kind == CommitmentKind::Obligation)
            .map(|(key, _)| *key)
            .expect("the first person owes an obligation");

        let patch = |declarations: Vec<Declaration>, operations: Vec<ComponentOp>| {
            CommandBody::AdmitPatch {
                patch: WorldPatch { declarations, operations, evidence: Vec::new() },
            }
        };
        let bodies: Vec<(&'static str, CommandBody)> = vec![
            (
                "discharge a past-due obligation",
                patch(
                    Vec::new(),
                    vec![ComponentOp::DischargeCommitment {
                        subject: Ref::Existing(people[0]),
                        key: obligation,
                    }],
                ),
            ),
            (
                "relocate along a route",
                patch(
                    Vec::new(),
                    vec![ComponentOp::Relocate { subject: Ref::Existing(people[5]), via: Ref::Existing(route) }],
                ),
            ),
            (
                "declare a subject with an import key",
                patch(
                    vec![Declaration::Subject(SubjectDeclaration {
                        handle: DraftHandle::new("newcomer"),
                        label: "The Newcomer".into(),
                        kind: SubjectKind::Person,
                        affordances: BTreeSet::new(),
                        position: Some(Ref::Existing(place_zero)),
                        import_key: Some(ImportKey::new("aetherlight.gate", "newcomer")),
                    })],
                    Vec::new(),
                ),
            ),
            (
                "transfer grain",
                patch(
                    Vec::new(),
                    vec![ComponentOp::Transfer {
                        from: Ref::Existing(people[0]),
                        to: Ref::Existing(people[1]),
                        resource: Ref::Existing(grain),
                        qty: Quantity(1),
                    }],
                ),
            ),
            (
                "witness a fact declared in the same patch",
                patch(
                    vec![Declaration::Fact(FactDeclaration {
                        handle: DraftHandle::new("omen"),
                        label: "The Omen".into(),
                        statement: Statement::new("The lamp gutters.").unwrap(),
                        standing: FactStandingRef::Ruled,
                    })],
                    vec![ComponentOp::Witness {
                        fact: Ref::Draft(DraftHandle::new("omen")),
                        place: Ref::Existing(place_zero),
                        confidence: Confidence::Believed,
                    }],
                ),
            ),
            (
                "grant authority",
                patch(
                    Vec::new(),
                    vec![ComponentOp::GrantAuthority {
                        holder: Ref::Existing(people[7]),
                        grant: AuthorityGrantRef {
                            kind: authority_kind(LEVY_KIND),
                            over: AuthorityTargetRef::Subject(Ref::Existing(people[8])),
                        },
                    }],
                ),
            ),
            (
                "install an incumbent",
                patch(
                    Vec::new(),
                    vec![ComponentOp::InstallIncumbent {
                        institution: Ref::Existing(institution),
                        office: office(WARDEN_OFFICE),
                        incumbent: Ref::Existing(people[3]),
                    }],
                ),
            ),
            (
                "acquire knowledge",
                patch(
                    Vec::new(),
                    vec![ComponentOp::AcquireKnowledge {
                        subject: Ref::Existing(people[2]),
                        fact: Ref::Existing(fact_one),
                        source: AuthoredSource::Witnessed,
                        confidence: Confidence::Believed,
                    }],
                ),
            ),
            (
                "bind a dependency",
                patch(
                    Vec::new(),
                    vec![ComponentOp::Bind {
                        subject: Ref::Existing(people[4]),
                        target: DependencyRef::Subject(Ref::Existing(people[2])),
                    }],
                ),
            ),
            (
                "retire a subject",
                patch(Vec::new(), vec![ComponentOp::Retire { subject: Ref::Existing(people[9]) }]),
            ),
        ];
        let mut counts = Vec::new();
        for (shape, body) in bodies {
            let rows = submit_counting_rows(&mut kernel, body);
            if persons == 100 {
                shapes.push(shape);
            }
            counts.push(rows);
        }
        eprintln!("{persons:>5} persons, rows per shape: {counts:?}");
        worked.push(counts);
    }
    for (index, shape) in shapes.iter().enumerate() {
        let rows: Vec<u64> = worked.iter().map(|counts| counts[index]).collect();
        assert!(
            rows.windows(2).all(|pair| pair[0] == pair[1]),
            "{shape}: rows read grew with the world (100, 400, 1600): {rows:?}"
        );
    }
    // A shape that is point reads all the way down reads no rows at all; the
    // counter must still be live, or equality proves nothing.
    assert!(worked[0].iter().sum::<u64>() > 0, "no shape read a row: the counter is dead");
}

/// The pure reads no submit shape isolates, inside one open transaction each:
/// who stands under a two-level subtree, one incumbent's delegated authority, and
/// a path of three routes. (What a patch reads is gated through `AdmitPatch` by
/// `patch_read_work_is_equal_at_100_and_1600`.)
#[test]
fn operation_reads_are_equal_at_100_and_1600() {
    use crate::Cost;
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

        let state = &mut kernel.state;
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
        state.rollback();
        worked.push(counts);
    }
    assert_eq!(worked[0], worked[1], "an operation's read work grew with the world");
}

/// The commit lane's checks read what the transaction wrote, not the world:
/// conservation sums the holders that were written, disjointness judges the
/// holders whose authority was written, and both cost the same rows at 100 and
/// 1600 persons. Each shape also has to be answered right: a holder already
/// written when the operations begin is measured from where it stood then, and
/// an overlap made by installing an incumbent, or by a grant, is refused with
/// every other holder untouched.
#[test]
fn apply_checks_read_the_log() {
    use crate::patch::{AuthorityGrant, AuthorityTarget, ResolvedOp};
    use crate::tests::{LEVY_KIND, WARDEN_OFFICE, authority_kind, office};
    use crate::{Quantity, apply_operations};

    let mut worked = Vec::new();
    for persons in [100usize, 1600] {
        let (mut kernel, people, _) = clockwork_world(persons);
        let place = |label: &str| {
            *kernel.state.entities.iter().find(|(_, record)| record.label == label).expect("declared").0
        };
        let (zero, one, grain) = (place("Place 0"), place("Place 1"), place("Grain 0"));
        let institution = subject_by_label(&kernel.state, "Institution 0");
        let level = AuthorityGrant {
            kind: authority_kind(LEVY_KIND),
            over: AuthorityTarget::PlaceSubtree(one),
        };
        let revision = kernel.state.revision + 1;
        let transfer = ResolvedOp::Transfer { from: people[0], to: people[1], resource: grain, qty: Quantity(1) };
        let mut counts = Vec::new();
        let state = &mut kernel.state;

        // A transfer between two of ten holders: nothing else is read.
        state.begin();
        let before = crate::docs::rows_read();
        apply_operations(state, std::slice::from_ref(&transfer), &[], revision)
            .expect("a conserving transfer applies");
        counts.push(crate::docs::rows_read() - before);
        state.rollback();

        // A holder written before the operations begin is measured from there.
        state.begin();
        crate::set_held(state, people[0], grain, 5);
        let before = crate::docs::rows_read();
        apply_operations(state, std::slice::from_ref(&transfer), &[], revision)
            .expect("the entry value is the before, not the value the transaction began with");
        counts.push(crate::docs::rows_read() - before);
        state.rollback();

        // Place 1 inside place 0, committed, with person 5's own grant over place 1.
        state.begin();
        state.entities.get_mut(&one).unwrap().container = Some(zero);
        state.authority.insert(people[5], std::collections::BTreeSet::from([level.clone()]));
        let folded = state.folded().unwrap();
        state.finish(folded);
        crate::journal::verify_state_shape(state).expect("the world is still disjoint");

        // A grant that overlaps the warden's lent ground is refused.
        state.begin();
        let overlap = ResolvedOp::GrantAuthority { holder: people[0], grant: level.clone() };
        let before = crate::docs::rows_read();
        let refused = apply_operations(state, &[overlap], &[], revision).unwrap_err();
        counts.push(crate::docs::rows_read() - before);
        assert!(
            matches!(&refused, crate::KernelError::Invariant(m) if m.contains("overlapping")),
            "{persons}: {refused:?}"
        );
        state.rollback();

        // Seating a holder of overlapping ground in the office is refused too.
        state.begin();
        let install = ResolvedOp::InstallIncumbent {
            institution,
            office: office(WARDEN_OFFICE),
            incumbent: people[5],
        };
        let before = crate::docs::rows_read();
        let refused = apply_operations(state, &[install], &[], revision).unwrap_err();
        counts.push(crate::docs::rows_read() - before);
        assert!(
            matches!(&refused, crate::KernelError::Invariant(m) if m.contains("overlapping")),
            "{persons}: {refused:?}"
        );
        state.rollback();

        // Custody is point reads, which are free; the disjointness judgments read
        // the office index. A refusal stops at the first overlapping holder, and
        // which holder that is depends on id order, so its reads are bounded
        // rather than equal: a few rows, where a scan of the world reads thousands.
        assert!(counts[2..].iter().all(|rows| (1..=8).contains(rows)), "{persons}: {counts:?}");
        worked.push(counts[..2].to_vec());
    }
    assert_eq!(worked[0], worked[1], "a commit check's read work grew with the world");
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
                actor: people[step],
                invocation: speak(speak_entry, &format!("Speech {step}.")),
            }
        };
        let envelope = clockwork_envelope(&kernel, body);
        kernel.submit(envelope, &AuthenticatedCaller::verified_play()).unwrap();
        states.push((format!("clockwork commit {step}"), kernel.state.clone()));
    }
    states.push(("multivalue world".into(), multivalue_world()));
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
    let now = state.header.now;
    for minutes in [1, 30] {
        let to = FictionalMinutes(now.0 + minutes);
        if crate::clock::derive_motion(state, to) != crate::clock::derive_motion_by_scan(state, to) {
            return Some(format!("derive_motion over {minutes} minutes"));
        }
    }
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
    let strange = crate::ImportKey::new("aetherlight.fixture", "held by nobody");
    for key in state
        .subjects
        .values()
        .filter_map(|subject| subject.import_key.as_ref())
        .chain(std::iter::once(&strange))
    {
        if state.import_key_held(key) != crate::import_key_held_by_scan(state, key) {
            return Some(format!("import key {key:?} held"));
        }
    }
    let unnamed = crate::patch::AffordanceKindName("no such kind".into());
    for kind in state
        .affordance_catalog
        .values()
        .map(|entry| &entry.kind)
        .chain(std::iter::once(&unnamed))
    {
        if state.kind_taken(kind) != crate::kind_taken_by_scan(state, kind) {
            return Some(format!("affordance kind {kind:?} taken"));
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
        "aetherlight.subject.v1",
        "aetherlight.affordance.v1",
        "aetherlight.entity.v1",
        "aetherlight.edge.v1",
        "aetherlight.position.v1",
        "aetherlight.channel.v1",
        "aetherlight.selection.v1",
        "aetherlight.commitments.v1",
        "aetherlight.pressures.v1",
    ]
    .into();
    assert_eq!(reported, indexed, "exactly the indexed partitions can be damaged");
    assert_eq!(misread, indexed, "a damaged index must change what a reader answers");
}

/// A holder written before the operations begin and returned by them to the
/// value it had when the transaction opened is no longer in the log, yet it
/// moved: conservation reads it from the entry record, so the transfer that
/// restores it is judged as conserving.
#[test]
fn apply_conserves_a_holder_restored_to_its_start_value() {
    use crate::patch::ResolvedOp;
    use crate::{Quantity, apply_operations};

    let (mut kernel, people, _) = clockwork_world(20);
    let grain = *kernel
        .state
        .entities
        .iter()
        .find(|(_, record)| record.label == "Grain 1")
        .expect("declared")
        .0;
    let revision = kernel.state.revision + 1;
    let state = &mut kernel.state;
    state.begin();
    crate::set_held(state, people[0], grain, 5);
    let transfer = ResolvedOp::Transfer { from: people[0], to: people[1], resource: grain, qty: Quantity(5) };
    apply_operations(state, std::slice::from_ref(&transfer), &[], revision)
        .expect("moving the pre-written five on conserves it");
    state.rollback();
}

/// Patches whose later operations read what earlier ones wrote, judged by the
/// resolver and committed: each is a legal sequence and must be accepted, or,
/// where the sequence changes nothing, refused cleanly by the resolver with the
/// commit never reached.
mod within_one_patch {
    use super::{clockwork_envelope, clockwork_world};
    use crate::patch::{
        AuthorityGrantRef, AuthorityTargetRef, Declaration, DraftHandle, Mismatch,
        PressureMagnitude, PressureSourceRef, SubjectDeclaration,
    };
    use crate::tests::{LEVY_KIND, authority_kind};
    use crate::{
        AuthenticatedCaller, CommandBody, ComponentOp, KernelError, Ref, SubjectKind, SubjectId,
        WorldKernel, WorldPatch,
    };
    use std::collections::BTreeSet;

    fn submit(kernel: &mut WorldKernel, patch: WorldPatch) -> Result<(), KernelError> {
        let envelope = clockwork_envelope(kernel, CommandBody::AdmitPatch { patch });
        kernel.submit(envelope, &AuthenticatedCaller::verified_play()).map(|_| ())
    }

    fn ops(operations: Vec<ComponentOp>) -> WorldPatch {
        WorldPatch { declarations: vec![], operations, evidence: vec![] }
    }

    fn levy_over(holder: SubjectId, over: SubjectId) -> ComponentOp {
        ComponentOp::GrantAuthority {
            holder: Ref::Existing(holder),
            grant: AuthorityGrantRef {
                kind: authority_kind(LEVY_KIND),
                over: AuthorityTargetRef::Subject(Ref::Existing(over)),
            },
        }
    }

    /// Two jurisdictions of one kind over different subjects are disjoint, so
    /// one holder may hold both.
    #[test]
    fn one_holder_may_hold_one_kind_over_two_subjects() {
        let (mut kernel, p, _) = clockwork_world(30);
        submit(&mut kernel, ops(vec![levy_over(p[0], p[5]), levy_over(p[0], p[6])]))
            .expect("levy over two different subjects does not overlap");
    }

    fn source(subject: SubjectId) -> PressureSourceRef {
        PressureSourceRef::Subject(Ref::Existing(subject))
    }

    /// A pressure advanced by one operation is there for the next to resolve,
    /// and one reduced to nothing is gone for the next to advance afresh.
    #[test]
    fn a_pressure_advanced_in_a_patch_is_read_by_the_next_operation() {
        let (mut kernel, p, _) = clockwork_world(30);
        submit(&mut kernel, ops(vec![
            ComponentOp::AdvancePressure { source: source(p[0]), target: Ref::Existing(p[12]), by: PressureMagnitude(2) },
            ComponentOp::ResolvePressure { source: source(p[0]), target: Ref::Existing(p[12]) },
        ]))
        .expect("advance then resolve the same slot");

        submit(&mut kernel, ops(vec![
            ComponentOp::AdvancePressure { source: source(p[0]), target: Ref::Existing(p[13]), by: PressureMagnitude(2) },
            ComponentOp::ReducePressure { source: source(p[0]), target: Ref::Existing(p[13]), by: PressureMagnitude(2) },
            ComponentOp::AdvancePressure { source: source(p[0]), target: Ref::Existing(p[13]), by: PressureMagnitude(1) },
        ]))
        .expect("advance, reduce to nothing, advance again");
    }

    fn declared(name: &str, affordance: crate::AffordanceId) -> Declaration {
        Declaration::Subject(SubjectDeclaration {
            handle: DraftHandle::new(name),
            label: name.into(),
            kind: SubjectKind::Person,
            affordances: BTreeSet::from([Ref::Existing(affordance)]),
            position: None,
            import_key: None,
        })
    }

    /// A subject declared with an affordance already has it: granting it is no
    /// change, and the resolver says so itself, rather than admitting what the
    /// commit then refuses. Revoking it first makes the grant a change.
    #[test]
    fn a_declared_affordance_is_known_to_the_operations_that_follow() {
        let (mut kernel, _, speak) = clockwork_world(30);
        let draft = |name: &str| Ref::Draft(DraftHandle::new(name));

        let refused = submit(&mut kernel, WorldPatch {
            declarations: vec![declared("scribe", speak)],
            operations: vec![ComponentOp::GrantAffordance { subject: draft("scribe"), affordance: Ref::Existing(speak) }],
            evidence: vec![],
        })
        .unwrap_err();
        assert!(
            matches!(&refused, KernelError::PatchRejected(m)
                if m == &[Mismatch::NoOperationEffect { operation: 0 }]),
            "{refused:?}"
        );

        submit(&mut kernel, WorldPatch {
            declarations: vec![declared("scribe2", speak)],
            operations: vec![
                ComponentOp::RevokeAffordance { subject: draft("scribe2"), affordance: Ref::Existing(speak) },
                ComponentOp::GrantAffordance { subject: draft("scribe2"), affordance: Ref::Existing(speak) },
            ],
            evidence: vec![],
        })
        .expect("revoke then grant is a change");
    }
}

/// Seeded random patch sequences through the real kernel. The resolver judges
/// each patch against its overlay, `apply_operations` re-derives conservation
/// and disjointness from the transaction log, and the full-scan references
/// judge the committed world: the three must agree after every step.
///
/// Per step, in a world of grain holders, institutions with a warden office,
/// pressures and a speak affordance:
/// - the outcome is an accepted commit or a `PatchRejected` mismatch set, never
///   a kernel `Invariant` error (the resolver admitted what apply refuses);
/// - a refused patch changes no document, and the same patch refused again
///   gives the same mismatches;
/// - after an accepted one every index equals its scan, no subject holds two
///   overlapping jurisdictions of one kind, and the grain total is unchanged
///   (the generator transfers and never mints).
#[test]
fn random_patch_sequences_agree_with_the_scan_references() {
    use crate::patch::{
        AuthorityGrantRef, AuthorityTargetRef, PressureMagnitude, PressureSourceRef, Quantity,
    };
    use crate::tests::{LEVY_KIND, WARDEN_OFFICE, authority_kind, office};
    use crate::{EntityKind, SubjectKind};

    struct Rng(u64);
    impl Rng {
        fn below(&mut self, n: usize) -> usize {
            self.0 ^= self.0 << 13;
            self.0 ^= self.0 >> 7;
            self.0 ^= self.0 << 17;
            (self.0 % n as u64) as usize
        }
    }

    let (mut accepted, mut refused, mut multi) = (0usize, 0usize, 0usize);
    for seed in 1..=10u64 {
        let (mut kernel, people, speak) = clockwork_world(30);
        let labelled = |kernel: &WorldKernel, kind: EntityKind| -> Vec<crate::EntityId> {
            let mut found: Vec<_> = kernel
                .state
                .entities
                .iter()
                .filter(|(_, record)| record.kind == kind)
                .map(|(id, record)| (record.label.clone(), *id))
                .collect();
            found.sort();
            found.into_iter().map(|(_, id)| id).collect()
        };
        let places = labelled(&kernel, EntityKind::Place);
        let grains = labelled(&kernel, EntityKind::Resource);
        let institutions: Vec<_> = (0..places.len())
            .map(|place| subject_by_label(&kernel.state, &format!("Institution {place}")))
            .collect();
        let grain_total = |kernel: &WorldKernel| -> u128 {
            grains.iter().map(|grain| crate::resource_total_by_scan(&kernel.state, *grain)).sum()
        };
        let total = grain_total(&kernel);
        let mut rng = Rng(seed.wrapping_mul(0x9E37_79B9_7F4A_7C15) | 1);

        for step in 0..30 {
            // A small pool of subjects, so patches collide often.
            let person = |rng: &mut Rng| Ref::Existing(people[rng.below(6)]);
            let levy_target = |rng: &mut Rng| {
                if rng.below(2) == 0 {
                    AuthorityTargetRef::Subject(Ref::Existing(people[rng.below(6)]))
                } else {
                    AuthorityTargetRef::PlaceSubtree(Ref::Existing(places[rng.below(3)]))
                }
            };
            let mut declarations = Vec::new();
            let mut operations = Vec::new();
            for index in 0..1 + rng.below(4) {
                let op = match rng.below(14) {
                    0..=2 => {
                        let holder = if rng.below(4) == 0 {
                            Ref::Existing(institutions[rng.below(3)])
                        } else {
                            person(&mut rng)
                        };
                        ComponentOp::GrantAuthority {
                            holder,
                            grant: AuthorityGrantRef { kind: authority_kind(LEVY_KIND), over: levy_target(&mut rng) },
                        }
                    }
                    3 => ComponentOp::RevokeAuthority {
                        holder: person(&mut rng),
                        grant: AuthorityGrantRef { kind: authority_kind(LEVY_KIND), over: levy_target(&mut rng) },
                    },
                    4 => ComponentOp::InstallIncumbent {
                        institution: Ref::Existing(institutions[rng.below(3)]),
                        office: office(WARDEN_OFFICE),
                        incumbent: person(&mut rng),
                    },
                    5 => ComponentOp::VacateOffice {
                        institution: Ref::Existing(institutions[rng.below(3)]),
                        office: office(WARDEN_OFFICE),
                    },
                    6 => ComponentOp::AdvancePressure {
                        source: PressureSourceRef::Subject(person(&mut rng)),
                        target: person(&mut rng),
                        by: PressureMagnitude(1 + rng.below(3) as u32),
                    },
                    7 => ComponentOp::ReducePressure {
                        source: PressureSourceRef::Subject(person(&mut rng)),
                        target: person(&mut rng),
                        by: PressureMagnitude(1 + rng.below(3) as u32),
                    },
                    8 => ComponentOp::ResolvePressure {
                        source: PressureSourceRef::Subject(person(&mut rng)),
                        target: person(&mut rng),
                    },
                    9 => ComponentOp::GrantAffordance { subject: person(&mut rng), affordance: Ref::Existing(speak) },
                    10 => ComponentOp::RevokeAffordance { subject: person(&mut rng), affordance: Ref::Existing(speak) },
                    11 => ComponentOp::Transfer {
                        from: person(&mut rng),
                        to: person(&mut rng),
                        resource: Ref::Existing(grains[rng.below(3)]),
                        qty: Quantity(1),
                    },
                    12 => {
                        let name = format!("s{step}-{index}");
                        declarations.push(Declaration::Subject(crate::patch::SubjectDeclaration {
                            handle: DraftHandle::new(&name),
                            label: format!("Drafted {seed}-{name}"),
                            kind: SubjectKind::Person,
                            affordances: std::collections::BTreeSet::from([Ref::Existing(speak)]),
                            position: None,
                            import_key: None,
                        }));
                        ComponentOp::GrantAffordance {
                            subject: Ref::Draft(DraftHandle::new(&name)),
                            affordance: Ref::Existing(speak),
                        }
                    }
                    _ if rng.below(8) == 0 => ComponentOp::Retire { subject: person(&mut rng) },
                    _ => ComponentOp::ResolvePressure {
                        source: PressureSourceRef::Subject(person(&mut rng)),
                        target: person(&mut rng),
                    },
                };
                operations.push(op);
            }
            let patch = WorldPatch { declarations, operations, evidence: vec![] };
            let documents = kernel.state.documents().unwrap();
            let again = clockwork_envelope(&kernel, CommandBody::AdmitPatch { patch: patch.clone() });
            let outcome = kernel.submit(
                clockwork_envelope(&kernel, CommandBody::AdmitPatch { patch }),
                &AuthenticatedCaller::verified_play(),
            );
            match outcome {
                Ok(_) => {
                    accepted += 1;
                    assert_eq!(reference_scan_disagreement(&kernel.state), None, "seed {seed} step {step}");
                    assert_indexes_derived(&kernel.state, &format!("seed {seed} step {step}"));
                    for subject in kernel.state.subjects.keys() {
                        assert!(
                            !crate::holder_overlaps(&kernel.state, *subject),
                            "seed {seed} step {step}: {subject:?} holds overlapping jurisdictions"
                        );
                    }
                    assert_eq!(grain_total(&kernel), total, "seed {seed} step {step}: custody");
                }
                Err(KernelError::PatchRejected(mismatches)) => {
                    refused += 1;
                    multi += usize::from(mismatches.len() > 1);
                    assert_eq!(kernel.state.documents().unwrap(), documents, "seed {seed} step {step}: a refusal wrote");
                    // The same patch against the same world: the same refusal.
                    let repeat = kernel.submit(again, &AuthenticatedCaller::verified_play());
                    assert!(
                        matches!(&repeat, Err(KernelError::PatchRejected(m)) if *m == mismatches),
                        "seed {seed} step {step}: {repeat:?}"
                    );
                }
                Err(other) => panic!("seed {seed} step {step}: resolver and apply disagree: {other:?}"),
            }
        }
    }
    // The generator must reach both sides, or the loop proves nothing.
    assert!(accepted >= 40 && refused >= 40 && multi >= 1, "{accepted} accepted, {refused} refused, {multi} multi");
}
