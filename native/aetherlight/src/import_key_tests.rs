//! One record, one subject: admission keeps the import-key mapping injective at
//! every door and at every revision, and a world nobody imported into is
//! byte-for-byte what it was before keys existed.

use crate::patch::{
    AccessKind, ComponentOp, Cost, Declaration, DraftHandle, EntityDeclaration, EntityKind,
    ImportKey, Mismatch, Ref, RouteDeclaration,
};
use crate::tests::{activate, auth_principal, command, creation, owner};
use crate::{
    CallerId, CommandBody, CommandId, ConsumerId, CreateWorld, EntityId, JurisdictionKey,
    KernelError, NewController, PatchAnswer, SubjectDeclaration, SubjectKind, SubmitReceipt,
    SystemCapability, WorldKernel, WorldPatch,
};
use std::collections::BTreeSet;

fn key(schema: &str, record: &str) -> ImportKey {
    ImportKey::new(schema, record)
}

fn alpha() -> ImportKey {
    key("aetheria.ship", "Alpha")
}

/// The fixture genesis with its witness imported from `alpha()`.
fn keyed_creation(title: &str) -> CreateWorld {
    let mut world = creation(CommandId::new(), title);
    for declaration in &mut world.patch.declarations {
        if let Declaration::Subject(subject) = declaration
            && subject.handle == DraftHandle::new("persona")
        {
            subject.import_key = Some(alpha());
        }
    }
    world
}

fn create(world: CreateWorld) -> (tempfile::TempDir, Result<WorldKernel, KernelError>) {
    let directory = tempfile::tempdir().unwrap();
    let result = WorldKernel::create(
        directory.path().join("world.cc"),
        world,
        &auth_principal(owner()),
    )
    .map(|(kernel, _)| kernel);
    (directory, result)
}

fn keyed_world() -> (tempfile::TempDir, WorldKernel) {
    let (dir, kernel) = create(keyed_creation("Keyed"));
    (dir, kernel.unwrap())
}

fn rejected(result: Result<impl Sized, KernelError>) -> Vec<Mismatch> {
    match result {
        Err(KernelError::PatchRejected(set)) => set,
        Err(other) => panic!("expected a patch refusal, got {other:?}"),
        Ok(_) => panic!("expected a patch refusal, the command was admitted"),
    }
}

fn patch_of(declarations: Vec<Declaration>) -> WorldPatch {
    WorldPatch {
        declarations,
        operations: Vec::new(),
        evidence: Vec::new(),
    }
}

fn submit_as(
    kernel: &mut WorldKernel,
    caller: CallerId,
    answers: Option<PatchAnswer>,
    patch: WorldPatch,
) -> Result<SubmitReceipt, KernelError> {
    let snapshot = kernel.snapshot().unwrap();
    kernel.submit(
        command(
            &snapshot,
            CommandId::new(),
            caller.clone(),
            CommandBody::AdmitPatch { answers, patch },
        ),
        &crate::AuthenticatedCaller::fixture(caller),
    )
}

fn commons(kernel: &WorldKernel) -> EntityId {
    *kernel.state.entities.keys().next().unwrap()
}

/// A mirror subject: the one shape every door may declare, since it needs no
/// catalog grant.
fn mirror(handle: &str, import_key: Option<ImportKey>, place: EntityId) -> Declaration {
    Declaration::Subject(SubjectDeclaration {
        handle: DraftHandle::new(handle),
        label: format!("The {handle}"),
        kind: SubjectKind::Person,
        controller: NewController::External {
            consumer: ConsumerId::of_name("import-key"),
        },
        affordances: BTreeSet::new(),
        position: Some(Ref::Existing(place)),
        import_key,
    })
}

fn held(handle: &str) -> Mismatch {
    Mismatch::ImportKeyHeld {
        handle: DraftHandle::new(handle),
    }
}

fn duplicate(handle: &str) -> Mismatch {
    Mismatch::DuplicateImportKey {
        handle: DraftHandle::new(handle),
    }
}

fn malformed(handle: &str) -> Mismatch {
    Mismatch::MalformedImportKey {
        handle: DraftHandle::new(handle),
    }
}

fn keys_in(kernel: &WorldKernel) -> Vec<ImportKey> {
    kernel
        .snapshot()
        .unwrap()
        .subjects
        .into_iter()
        .filter_map(|subject| subject.import_key)
        .collect()
}

#[test]
fn one_patch_cannot_declare_two_subjects_with_one_import_key() {
    // At genesis.
    let mut world = creation(CommandId::new(), "Twins");
    for declaration in &mut world.patch.declarations {
        if let Declaration::Subject(subject) = declaration
            && (subject.handle == DraftHandle::new("persona")
                || subject.handle == DraftHandle::new("operator"))
        {
            subject.import_key = Some(alpha());
        }
    }
    let (_dir, result) = create(world);
    assert_eq!(rejected(result), vec![duplicate("operator")]);

    // In a later patch, with the key unheld.
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let revision = kernel.state.revision;
    let beta = Some(key("aetheria.ship", "Beta"));
    let error = submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(vec![
            mirror("first", beta.clone(), place),
            mirror("second", beta, place),
        ]),
    );
    assert_eq!(rejected(error), vec![duplicate("second")]);
    assert_eq!(kernel.state.revision, revision, "a refusal moved the world");
}

/// The fixture world past genesis but still in Draft: the owner has added an
/// unwalked place the elaborator can later answer.
fn advanced_keyed_world() -> (tempfile::TempDir, WorldKernel, EntityId) {
    let (dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(vec![
            Declaration::Entity(EntityDeclaration {
                handle: DraftHandle::new("dead-end"),
                label: "The Unwalked Road".into(),
                kind: EntityKind::Place,
                container: None,
            }),
            Declaration::Route(RouteDeclaration {
                handle: DraftHandle::new("gate"),
                label: "The Field Gate".into(),
                from: Ref::Existing(place),
                to: Ref::Draft(DraftHandle::new("dead-end")),
                access: AccessKind::Public,
                cost: Cost(1),
            }),
        ]),
    )
    .unwrap();
    let dead_end = *kernel
        .state
        .entities
        .iter()
        .find(|(_, record)| record.label == "The Unwalked Road")
        .map(|(id, _)| id)
        .unwrap();
    (dir, kernel, dead_end)
}

/// Each door names the held key, typed, and the refusal leaves the revision
/// where it was. The owner and the consumer declare in Draft only; Play and the
/// elaborator act in Active.
fn refuse_held_key_at_each_door(
    kernel: &mut WorldKernel,
    place: EntityId,
    doors: Vec<(&str, CallerId, Option<PatchAnswer>)>,
) {
    for (door, caller, answers) in doors {
        let revision = kernel.state.revision;
        let result = submit_as(
            kernel,
            caller,
            answers,
            patch_of(vec![mirror("copy", Some(alpha()), place)]),
        );
        assert_eq!(rejected(result), vec![held("copy")], "{door}");
        assert_eq!(
            kernel.state.revision, revision,
            "{door} refusal moved the world"
        );
    }
}

fn consumer() -> CallerId {
    CallerId::System(SystemCapability::Consumer {
        consumer: ConsumerId::of_name("import-key"),
    })
}

#[test]
fn a_held_import_key_is_refused_at_every_door_after_the_revision_has_moved() {
    let (_dir, mut kernel, dead_end) = advanced_keyed_world();
    assert!(kernel.state.revision > 0, "the world has moved past genesis");
    refuse_held_key_at_each_door(
        &mut kernel,
        dead_end,
        vec![
            ("owner", CallerId::Principal(owner()), None),
            ("consumer", consumer(), None),
        ],
    );
    activate(&mut kernel);
    let answer = crate::derive_boundaries(&kernel.state)
        .unwrap()
        .into_iter()
        .find(|boundary| {
            matches!(boundary, crate::CausalBoundary::UnelaboratedDestination { place, .. }
                if *place == dead_end)
        })
        .expect("the dead end is an unelaborated destination");
    refuse_held_key_at_each_door(
        &mut kernel,
        dead_end,
        vec![
            ("play", CallerId::System(SystemCapability::Play), None),
            (
                "elaborator",
                CallerId::System(SystemCapability::Elaborator {
                    jurisdiction: JurisdictionKey::PlaceSubtree(dead_end),
                }),
                Some(PatchAnswer::Boundary(answer.clone())),
            ),
        ],
    );
    // The same doors admit an unheld key, so the refusals are the key's; and
    // what a door admitted is held against every later door.
    let fresh = key("aetheria.ship", "Gamma");
    let receipt = submit_as(
        &mut kernel,
        CallerId::System(SystemCapability::Elaborator {
            jurisdiction: JurisdictionKey::PlaceSubtree(dead_end),
        }),
        Some(PatchAnswer::Boundary(answer)),
        patch_of(vec![mirror("fresh", Some(fresh.clone()), dead_end)]),
    )
    .unwrap();
    assert!(matches!(receipt, SubmitReceipt::Applied(_)));
    let again = submit_as(
        &mut kernel,
        CallerId::System(SystemCapability::Play),
        None,
        patch_of(vec![mirror("fresh-again", Some(fresh), dead_end)]),
    );
    assert_eq!(rejected(again), vec![held("fresh-again")]);
}

#[test]
fn a_held_import_key_is_refused_in_a_later_patch() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let revision = kernel.state.revision;
    let error = submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(vec![mirror("again", Some(alpha()), place)]),
    );
    assert_eq!(rejected(error), vec![held("again")]);
    assert_eq!(kernel.state.revision, revision);
    assert_eq!(keys_in(&kernel), vec![alpha()]);
}

#[test]
fn a_retired_subjects_import_key_is_never_reissued() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let witness = kernel
        .snapshot()
        .unwrap()
        .subjects
        .into_iter()
        .find(|subject| subject.import_key.is_some())
        .unwrap()
        .id;
    submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        WorldPatch {
            declarations: Vec::new(),
            operations: vec![ComponentOp::Retire {
                subject: Ref::Existing(witness),
            }],
            evidence: Vec::new(),
        },
    )
    .unwrap();
    let retired = kernel
        .snapshot()
        .unwrap()
        .subjects
        .into_iter()
        .find(|subject| subject.id == witness)
        .unwrap();
    assert!(retired.retired, "the witness is retired");
    assert_eq!(
        retired.import_key,
        Some(alpha()),
        "a retired subject keeps its key"
    );
    let error = submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(vec![mirror("reborn", Some(alpha()), place)]),
    );
    assert_eq!(rejected(error), vec![held("reborn")]);
}

#[test]
fn keys_that_differ_in_any_way_are_distinct_records() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    // Same length, case, prefix, the same concatenation split elsewhere, and
    // the parts swapped: none is the held key.
    let forgeries = [
        key("aetheria.ship", "Alphb"),
        key("aetheria.ship", "alpha"),
        key("aetheria.ship", "Alph"),
        key("aetheria.ship", "Alpha2"),
        key("aetheria.shipA", "lpha"),
        key("aetheria.sh", "ipAlpha"),
        key("Aetheria.ship", "Alpha"),
        key("Alpha", "aetheria.ship"),
        key("aetheria.ship2", "Alpha"),
    ];
    let declarations = forgeries
        .iter()
        .enumerate()
        .map(|(index, forged)| mirror(&format!("forged-{index}"), Some(forged.clone()), place))
        .collect();
    submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(declarations),
    )
    .expect("nine distinct keys beside the held one are admitted together");
    let held_now: BTreeSet<ImportKey> = keys_in(&kernel).into_iter().collect();
    assert_eq!(held_now.len(), forgeries.len() + 1);
    // And each is then held exactly: repeating any one is refused.
    for (index, forged) in forgeries.iter().enumerate() {
        let again = submit_as(
            &mut kernel,
            CallerId::Principal(owner()),
            None,
            patch_of(vec![mirror("repeat", Some(forged.clone()), place)]),
        );
        assert_eq!(rejected(again), vec![held("repeat")], "forgery {index}");
    }
}

#[test]
fn a_malformed_import_key_is_refused_without_echoing_it() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let long = "x".repeat(201);
    let wide = "é".repeat(101);
    let cases = [
        key("", "Delta"),
        key("aetheria.ship", ""),
        key("aetheria ship", "Delta"),
        key("aetheria.ship", "Del\tta"),
        key("aetheria.ship", "Del\u{a0}ta"),
        key("aetheria.ship", " Delta"),
        key("aetheria.ship", "Delta\n"),
        key(&long, "Delta"),
        key("aetheria.ship", &long),
        key("aetheria.ship", &wide),
        key(&wide, "Delta"),
    ];
    for (index, bad) in cases.iter().enumerate() {
        let set = rejected(submit_as(
            &mut kernel,
            CallerId::Principal(owner()),
            None,
            patch_of(vec![mirror("bad", Some(bad.clone()), place)]),
        ));
        assert_eq!(set, vec![malformed("bad")], "case {index}");
        let spelled = format!("{set:?}");
        assert!(
            !spelled.contains("Delta") && !spelled.contains("aetheria"),
            "case {index} echoed the key"
        );
    }
    // The limit is 200 bytes per part, counted in bytes.
    let edge = "é".repeat(100);
    let at_limit = [
        key(&"x".repeat(200), &edge),
        key("aetheria.ship", &"y".repeat(200)),
    ];
    let declarations = at_limit
        .iter()
        .enumerate()
        .map(|(index, limit)| mirror(&format!("edge-{index}"), Some(limit.clone()), place))
        .collect();
    submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(declarations),
    )
    .expect("keys of exactly 200 bytes per part are admitted");
}

#[test]
fn the_subjects_map_refuses_a_second_subject_for_one_key_even_when_resolved_elsewhere() {
    let (_dir, kernel) = keyed_world();
    let place = commons(&kernel);
    let resolve = |handle: &str| {
        crate::patch::resolve_patch(
            &kernel.state,
            CommandId::new(),
            &patch_of(vec![mirror(
                handle,
                Some(key("aetheria.ship", "Omega")),
                place,
            )]),
            None,
            None,
        )
        .expect("the key is unheld at the revision both were resolved against")
    };
    let (first, second) = (resolve("first"), resolve("second"));
    let mut state = kernel.state.clone();
    let before = state.subjects.len();
    let effect = |resolved| crate::WorldEffect::PatchAdmitted {
        answers: None,
        resolved,
    };
    crate::apply_effect(
        &mut state,
        CommandId::new(),
        &CallerId::Principal(owner()),
        &effect(first),
    )
    .unwrap();
    assert_eq!(state.subjects.len(), before + 1);
    let result = crate::apply_effect(
        &mut state,
        CommandId::new(),
        &CallerId::Principal(owner()),
        &effect(second),
    );
    assert!(
        matches!(result, Err(KernelError::Invariant(_))),
        "{result:?}"
    );
    assert_eq!(
        state.subjects.len(),
        before + 1,
        "the second subject was installed"
    );
}

fn contains(haystack: &[u8], needle: &[u8]) -> bool {
    haystack.windows(needle.len()).any(|window| window == needle)
}

#[test]
fn keyless_world_digests_unchanged() {
    // A subject nobody imported serialises without the field at all, so every
    // digest written before keys existed is the digest of the same bytes.
    let (_dir, kernel) = create(creation(CommandId::new(), "Keyless"));
    let kernel = kernel.unwrap();
    let state = rmp_serde::to_vec_named(&kernel.state).unwrap();
    assert!(
        !contains(&state, b"import_key"),
        "a keyless world grew a field"
    );
    // A keyed world carries it, round-trips, and digests differently.
    let (_dir, keyed) = keyed_world();
    let keyed_state = rmp_serde::to_vec_named(&keyed.state).unwrap();
    assert!(contains(&keyed_state, b"import_key"));
    let back: crate::WorldState = rmp_serde::from_slice(&keyed_state).unwrap();
    assert_eq!(
        crate::state_digest(&back).unwrap(),
        crate::state_digest(&keyed.state).unwrap()
    );
    assert_ne!(
        crate::state_digest(&keyed.state).unwrap(),
        crate::state_digest(&kernel.state).unwrap()
    );
    // And a pre-key state deserialises with every key absent.
    let stripped: crate::WorldState = rmp_serde::from_slice(&state).unwrap();
    assert!(stripped.subjects.values().all(|s| s.import_key.is_none()));
}

#[test]
fn a_keyed_world_reopens_with_its_keys_and_digests() {
    let (dir, kernel) = keyed_world();
    let snapshot = kernel.snapshot().unwrap();
    let world_id = snapshot.world_id;
    drop(kernel);
    let reopened = WorldKernel::open(dir.path().join("world.cc"), world_id).unwrap();
    let again = reopened.snapshot().unwrap();
    assert_eq!(again.state_digest, snapshot.state_digest);
    let keys = keys_in(&reopened);
    assert_eq!(keys, vec![alpha()]);
    // A reader of the snapshot gets both parts back, separately.
    assert_eq!(keys[0].schema(), "aetheria.ship");
    assert_eq!(keys[0].record(), "Alpha");
}


#[test]
fn a_record_key_is_one_canonical_spelling() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let nfc = "Caf\u{e9}";
    let nfd = "Cafe\u{301}";
    let submit = |kernel: &mut WorldKernel, handle: &str, record: &str| {
        submit_as(
            kernel,
            CallerId::Principal(owner()),
            None,
            patch_of(vec![mirror(
                handle,
                Some(key("aetheria.ship", record)),
                place,
            )]),
        )
    };
    submit(&mut kernel, "nfc", nfc).expect("the NFC spelling is admitted");
    let refused = [
        nfd,
        "Alpha\u{200b}",
        "Alpha\u{0}",
        "\u{feff}Alpha",
        "Alpha\u{202e}",
        "Alpha\u{7}",
        "Del  ta",
        "Del\tta",
        "Del\u{a0}ta",
        "Del\u{2003}ta",
        "Del\nta",
        // The violation sits deep in a long, real-shaped path, past any prefix.
        "Worldbuilding/Pre-Elysium/Factions/Powers/Minor/Cafe\u{301}",
        "Worldbuilding/Pre-Elysium/Factions/Powers/Minor/Ewan\u{200b}Hart",
    ];
    for (index, record) in refused.iter().enumerate() {
        let set = rejected(submit(&mut kernel, "bad", record));
        assert_eq!(set, vec![malformed("bad")], "spelling {index}");
        assert!(
            !format!("{set:?}").contains("Alpha"),
            "spelling {index} echoed the key"
        );
    }
    assert_eq!(
        keys_in(&kernel).len(),
        2,
        "only the genesis key and the NFC key are held"
    );
}

#[test]
fn a_lore_page_path_with_spaces_is_a_record_key() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let record = "Worldbuilding/Pre-Elysium/Factions/Powers/Minor/Ewan Hart";
    let declare = |kernel: &mut WorldKernel, handle: &str| {
        submit_as(
            kernel,
            CallerId::Principal(owner()),
            None,
            patch_of(vec![mirror(
                handle,
                Some(key("aetheria.lore", record)),
                place,
            )]),
        )
    };
    declare(&mut kernel, "ewan").expect("a path with interior spaces is admitted");
    assert_eq!(rejected(declare(&mut kernel, "again")), vec![held("again")]);
}

#[test]
fn a_loaded_state_with_a_bad_import_key_is_refused() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    submit_as(
        &mut kernel,
        CallerId::Principal(owner()),
        None,
        patch_of(vec![mirror(
            "beta",
            Some(key("aetheria.ship", "Beta")),
            place,
        )]),
    )
    .unwrap();
    assert!(crate::journal::verify_state_shape(&kernel.state).is_ok());
    let corrupt = |edit: &dyn Fn(&mut ImportKey)| {
        let mut state = kernel.state.clone();
        for subject in state.subjects.values_mut() {
            if let Some(held) = subject.import_key.as_mut() {
                edit(held);
            }
        }
        crate::journal::verify_state_shape(&state)
    };
    let shared = corrupt(&|held| *held = alpha());
    assert!(
        matches!(&shared, Err(crate::journal::JournalError::Corrupt(why)) if why.contains("import key")),
        "two subjects sharing one key: {shared:?}"
    );
    assert!(
        corrupt(&|held| *held = key("aetheria.ship", " Beta ")).is_err(),
        "a key with edge spaces must not load"
    );
    assert!(
        corrupt(&|held| *held = key("", "")).is_err(),
        "an empty key must not load"
    );
    assert!(
        corrupt(&|held| *held = key("aetheria.ship", "Be\u{200b}ta")).is_err(),
        "a key with a format character must not load"
    );
}

/// The admission backstop, reached with an effect resolution never produced: a
/// resolved patch whose two subjects carry one key. The control is the same
/// effect with distinct keys, admitted by the same function.
#[test]
fn admitting_a_resolved_effect_refuses_two_subjects_with_one_key() {
    let (_dir, kernel) = keyed_world();
    let place = commons(&kernel);
    let resolve = || {
        crate::patch::resolve_patch(
            &kernel.state,
            CommandId::new(),
            &patch_of(vec![
                mirror("first", Some(key("aetheria.ship", "Beta")), place),
                mirror("second", Some(key("aetheria.ship", "Gamma")), place),
            ]),
            None,
            None,
        )
        .expect("two distinct keys resolve")
    };
    let resolves_to = crate::resolution_revision(&kernel.state).unwrap();
    let mut control = kernel.state.clone();
    crate::admit_resolved(&mut control, &resolve(), resolves_to)
        .expect("distinct keys are admitted");

    let mut twinned = resolve();
    assert_eq!(twinned.subjects.len(), 2);
    twinned.subjects[1].subject.import_key = twinned.subjects[0].subject.import_key.clone();
    let mut state = kernel.state.clone();
    let error = crate::admit_resolved(&mut state, &twinned, resolves_to)
        .expect_err("one key on two resolved subjects is refused");
    assert!(
        matches!(&error, KernelError::Invariant(why) if why.contains("import key")),
        "{error:?}"
    );
    assert!(!format!("{error:?}").contains("Beta"), "the refusal echoed the key");
}

/// A decomposable character at every position of a real-shaped key: the NFD
/// spelling is refused wherever it sits, the NFC spelling is admitted, so the
/// rule cannot be a prefix, suffix or first-character check.
#[test]
fn a_decomposable_character_is_refused_in_nfd_at_every_position() {
    let (_dir, mut kernel) = keyed_world();
    let place = commons(&kernel);
    let template: Vec<char> = "Worldbuilding/Pre-Elysium/Factions/Powers/Minor/Ewan Hart"
        .chars()
        .collect();
    let submit = |kernel: &mut WorldKernel, handle: &str, record: String| {
        submit_as(
            kernel,
            CallerId::Principal(owner()),
            None,
            patch_of(vec![mirror(
                handle,
                Some(key("aetheria.lore", &record)),
                place,
            )]),
        )
    };
    let spelled = |position: usize, inserted: &str| {
        let mut record: String = template[..position].iter().collect();
        record.push_str(inserted);
        record.extend(&template[position..]);
        record
    };
    for position in 0..=template.len() {
        let nfd = spelled(position, "e\u{301}");
        assert!(
            !unicode_normalization::is_nfc(&nfd),
            "position {position} is not NFD"
        );
        let set = rejected(submit(&mut kernel, "bad", nfd));
        assert_eq!(set, vec![malformed("bad")], "NFD at position {position}");
    }
    for position in 0..=template.len() {
        let nfc = spelled(position, "\u{e9}");
        submit(&mut kernel, &format!("good{position}"), nfc)
            .unwrap_or_else(|_| panic!("NFC at position {position} was refused"));
    }
    assert_eq!(
        keys_in(&kernel).len(),
        1 + template.len() + 1,
        "the genesis key and one NFC key per position are held, no NFD key"
    );
}
