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
fn golden_session() -> (WorldKernel, String) {
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

/// What one tick and one speech cost to commit as the world grows: ten persons
/// to a place, one Routine each with period 60, due staggered over the hour.
/// Run on its own, in release, to read the numbers; no threshold is asserted.
///
/// `cargo test --release --lib clockwork_commit_cost -- --ignored --nocapture`
#[test]
#[ignore]
fn clockwork_commit_cost() {
    use crate::patch::kernel_speak_grant;
    use crate::patch::{Declaration, DraftHandle, EntityDeclaration, SubjectDeclaration};
    use crate::{CallerId, CommandEnvelope, CreateWorld, EntityKind, SubjectKind, WorldPatch};

    println!("persons  places  create_ms  tick_mean_ms  speech_mean_ms  revision_after");
    for persons in [100usize, 400, 1600] {
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
        let started = Instant::now();
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
        let create_ms = started.elapsed().as_secs_f64() * 1e3;

        let people: Vec<_> = (0..persons)
            .map(|person| subject_by_label(&kernel.state, &format!("Person {person}")))
            .collect();
        let routines = people
            .iter()
            .enumerate()
            .map(|(index, subject)| ComponentOp::CreateCommitment {
                subject: Ref::Existing(*subject),
                counterparty: None,
                kind: CommitmentKind::Routine,
                due: FictionalMinutes(1 + (index % 60) as u64),
                period: Some(TickMinutes::new(60).unwrap()),
                checks: Vec::new(),
                statement: Statement::new("Keeps the hour.").unwrap(),
            })
            .collect();
        let play = AuthenticatedCaller::verified_play();
        let envelope = |kernel: &WorldKernel, body: CommandBody| CommandEnvelope {
            world_id: kernel.state.world_id,
            expected_revision: kernel.state.revision,
            caller: CallerId::Play,
            body,
        };
        let placing = envelope(&kernel, operations(routines));
        kernel.submit(placing, &play).expect("the routines are placed");

        let speak_entry = *kernel
            .state
            .affordance_catalog
            .iter()
            .find(|(_, entry)| entry.kind.0 == "speak")
            .map(|(id, _)| id)
            .expect("genesis admits Speak");

        let mut ticks = 0.0;
        for _ in 0..30 {
            let tick = envelope(&kernel, tick_body(1));
            let started = Instant::now();
            kernel.submit(tick, &play).expect("the tick commits");
            ticks += started.elapsed().as_secs_f64() * 1e3;
        }
        let mut speeches = 0.0;
        for index in 0..30 {
            let act = envelope(
                &kernel,
                CommandBody::Act {
                    actor: people[index],
                    invocation: speak(speak_entry, &format!("Speech {index}.")),
                },
            );
            let started = Instant::now();
            kernel.submit(act, &play).expect("the speech commits");
            speeches += started.elapsed().as_secs_f64() * 1e3;
        }
        println!(
            "{persons:>7}  {places:>6}  {create_ms:>9.1}  {:>12.2}  {:>14.2}  {:>14}",
            ticks / 30.0,
            speeches / 30.0,
            kernel.state.revision
        );
    }
}
