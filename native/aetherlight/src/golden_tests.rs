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
/// listeners.
fn clockwork_world(persons: usize) -> (WorldKernel, Vec<crate::SubjectId>, crate::AffordanceId) {
    use crate::patch::kernel_speak_grant;
    use crate::patch::{EntityDeclaration, SubjectDeclaration};
    use crate::{CallerId, CommandEnvelope, CreateWorld, EntityKind, SubjectKind};

    let places = persons / 10;
    let mut declarations = Vec::new();
    for place in 0..places {
        declarations.push(Declaration::Entity(EntityDeclaration {
            handle: DraftHandle::new(format!("place-{place}")),
            label: format!("Place {place}"),
            kind: EntityKind::Place,
            container: None,
        }));
    }
    for person in 0..persons {
        declarations.push(Declaration::Subject(SubjectDeclaration {
            handle: DraftHandle::new(format!("person-{person}")),
            label: format!("Person {person}"),
            kind: SubjectKind::Person,
            affordances: kernel_speak_grant(),
            position: Some(Ref::Draft(DraftHandle::new(format!("place-{}", person / 10)))),
            import_key: None,
        }));
    }
    let (mut kernel, _) = WorldKernel::create(
        CreateWorld {
            owner: owner(),
            title: "Clockwork".into(),
            patch: WorldPatch {
                declarations,
                operations: Vec::new(),
                evidence: Vec::new(),
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
    let routines = people
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

/// A failed apply leaves nothing behind: state, accumulator and digest equal
/// to before, no transaction open, and the next honest command commits. The
/// failure is the revision overflow a tick meets after it has written the
/// clock, a routine and its pressure.
#[test]
fn a_failed_apply_rolls_back() {
    let (mut kernel, _, _) = clockwork_world(20);
    let honest_revision = kernel.state.revision;
    kernel.state.revision = u64::MAX;
    let before = kernel.state.clone();
    let commits = kernel.journal.commit_count();

    let tick = clockwork_envelope(&kernel, tick_body(1));
    let error = kernel
        .submit(tick, &AuthenticatedCaller::verified_play())
        .unwrap_err();
    assert!(matches!(error, KernelError::Serialization(_)), "{error:?}");
    assert_eq!(kernel.state, before, "the failed tick left writes behind");
    assert_eq!(kernel.journal.commit_count(), commits);
    kernel.state.begin();
    assert_eq!(kernel.state.folded().unwrap(), kernel.state.accumulator);
    assert_eq!(
        kernel.state.accumulator.state_digest(u64::MAX),
        kernel.state.full_state_digest().unwrap(),
        "the accumulator no longer matches the documents"
    );

    kernel.state.revision = honest_revision;
    let tick = clockwork_envelope(&kernel, tick_body(1));
    kernel
        .submit(tick, &AuthenticatedCaller::verified_play())
        .expect("the next honest command commits");
    assert_eq!(kernel.state.full_state_digest().unwrap(), kernel.state.state_digest);
}

/// What one tick and one speech cost to commit as the world grows, by the
/// clockwork world above. Run on its own, in release, to read the numbers; no
/// time is asserted here. The timing gate belongs to the follow-up
/// `aetherlight-read-indexes-timing-gate`, because the world reads a commit
/// makes (`audience`, `derive_motion`) are still O(world) and bound the ratio.
/// `digest_work_is_equal_at_100_and_1600` is the gate on what this commit owns.
///
/// `cargo test --release --lib clockwork_commit_cost -- --ignored --nocapture`
#[test]
#[ignore]
fn clockwork_commit_cost() {
    println!("persons  create_ms  tick_ms  tick_digests  speech_ms  speech_digests");
    let mut ticks_ms = Vec::new();
    let mut speeches_ms = Vec::new();
    for persons in [100usize, 400, 1600] {
        let started = Instant::now();
        let (mut kernel, people, speak_entry) = clockwork_world(persons);
        let create_ms = started.elapsed().as_secs_f64() * 1e3;
        let play = AuthenticatedCaller::verified_play();

        let (mut ticks, mut tick_digests) = (0.0, 0);
        for _ in 0..30 {
            let tick = clockwork_envelope(&kernel, tick_body(1));
            crate::digest::DOC_DIGESTS.with(|count| count.set(0));
            let started = Instant::now();
            kernel.submit(tick, &play).expect("the tick commits");
            ticks += started.elapsed().as_secs_f64() * 1e3;
            tick_digests = crate::digest::DOC_DIGESTS.with(|count| count.get());
        }
        let (mut speeches, mut speech_digests) = (0.0, 0);
        for index in 0..30 {
            let act = clockwork_envelope(
                &kernel,
                CommandBody::Act {
                    actor: people[index],
                    invocation: speak(speak_entry, &format!("Speech {index}.")),
                },
            );
            crate::digest::DOC_DIGESTS.with(|count| count.set(0));
            let started = Instant::now();
            kernel.submit(act, &play).expect("the speech commits");
            speeches += started.elapsed().as_secs_f64() * 1e3;
            speech_digests = crate::digest::DOC_DIGESTS.with(|count| count.get());
        }
        println!(
            "{persons:>7}  {create_ms:>9.1}  {:>7.3}  {tick_digests:>12}  {:>9.3}  {speech_digests:>14}",
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
