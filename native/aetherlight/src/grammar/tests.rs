use super::*;
use crate::patch::{
    AffordanceDeclaration, AffordanceKindName, AudienceSpec, Declaration, DraftHandle, EntityKind,
    Mismatch, OutcomeBand, Precondition, Role, RoleSpec,
};
use crate::tests::{auth_principal, command, creation, owner};
use crate::{
    CallerId, CommandBody, CommandId, CreateWorld, KernelError, SubjectKind, SubmitReceipt,
    WorldId, WorldKernel, WorldPatch,
};

fn verb(path: RenderPath, roles: &[(&str, GrammarReferentKind)]) -> GrammarVerb {
    GrammarVerb {
        render_path: path,
        roles: roles
            .iter()
            .map(|(role, referent)| GrammarRole {
                role: (*role).into(),
                referent: *referent,
            })
            .collect(),
    }
}

/// What the fixture world's genesis needs: the kernel speak entry and the
/// fixture's own `convene`. Anything else a test wants is added to it.
fn grammar_with(extra: &[(&str, GrammarVerb)]) -> GrammarBinding {
    let mut verbs = vec![
        ("speak".to_owned(), verb(RenderPath::Conversation, &[])),
        ("convene".to_owned(), verb(RenderPath::Conversation, &[])),
    ];
    verbs.extend(
        extra
            .iter()
            .map(|(kind, v)| ((*kind).to_owned(), v.clone())),
    );
    GrammarBinding::new("aetheria.grammar", 3, verbs).unwrap()
}

fn bound(grammar: Option<GrammarBinding>, title: &str) -> CreateWorld {
    let mut world = creation(CommandId::new(), title);
    world.grammar = grammar;
    world
}

fn declare(handle: &str, kind: &str, roles: &[(&str, RefKind)]) -> Declaration {
    Declaration::Affordance(AffordanceDeclaration {
        handle: DraftHandle::new(handle),
        kind: AffordanceKindName(kind.into()),
        roles: roles
            .iter()
            .map(|(role, kind)| RoleSpec {
                role: Role((*role).into()),
                kind: *kind,
            })
            .collect(),
        // Speech with one audience is the smallest entry the resolver does
        // not refuse as inert; the grammar check does not read it.
        preconditions: vec![Precondition::CanBroadcast {
            via: AudienceSpec::Colocated,
        }],
        effect_slots: Vec::new(),
        outcome_bands: vec![OutcomeBand {
            weight: 1,
            effects: Vec::new(),
        }],
        carries_speech: true,
    })
}

fn patch_of(declarations: Vec<Declaration>) -> WorldPatch {
    WorldPatch {
        declarations,
        operations: Vec::new(),
        evidence: Vec::new(),
    }
}

fn rejected(result: Result<impl Sized, KernelError>) -> Vec<Mismatch> {
    match result {
        Err(KernelError::PatchRejected(set)) => set,
        Err(other) => panic!("expected a patch refusal, got {other:?}"),
        Ok(_) => panic!("expected a patch refusal, the command was admitted"),
    }
}

/// Each call gets its own store path: kernels are created, not reopened.
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

fn admit(kernel: &mut WorldKernel, patch: WorldPatch) -> Result<SubmitReceipt, KernelError> {
    let snapshot = kernel.snapshot().unwrap();
    kernel.submit(
        command(
            &snapshot,
            CommandId::new(),
            CallerId::Principal(owner()),
            CommandBody::AdmitPatch {
                answers: None,
                patch,
            },
        ),
        &auth_principal(owner()),
    )
}

fn person() -> RefKind {
    RefKind::Subject(Some(SubjectKind::Person))
}

fn place() -> RefKind {
    RefKind::Entity(EntityKind::Place)
}

fn not_in_grammar(handle: &str) -> Vec<Mismatch> {
    vec![Mismatch::AffordanceKindNotInGrammar {
        handle: DraftHandle::new(handle),
    }]
}

fn disagrees(handle: &str) -> Vec<Mismatch> {
    vec![Mismatch::AffordanceRolesDisagreeWithGrammar {
        handle: DraftHandle::new(handle),
    }]
}

#[test]
fn grammar_world_refuses_an_affordance_kind_outside_the_grammar() {
    // Genesis: a declared kind the binding lacks is refused.
    let mut world = bound(Some(grammar_with(&[])), "Outside");
    world.patch.declarations.push(declare("wave", "wave", &[]));
    assert_eq!(rejected(create(world).1), not_in_grammar("wave"));

    // A later AdmitPatch: the same kind is refused, and a verb the grammar
    // does carry is admitted by the same path.
    let extra = [("hail", verb(RenderPath::Conversation, &[]))];
    let (_dir, kernel) = create(bound(Some(grammar_with(&extra)), "Outside"));
    let mut kernel = kernel.unwrap();
    assert_eq!(
        rejected(admit(
            &mut kernel,
            patch_of(vec![declare("wave", "wave", &[])])
        )),
        not_in_grammar("wave")
    );
    let receipt = admit(&mut kernel, patch_of(vec![declare("hail", "hail", &[])])).unwrap();
    assert!(matches!(receipt, SubmitReceipt::Applied(_)));
}

#[test]
fn grammar_world_refuses_a_role_signature_the_grammar_does_not_carry() {
    let extra = [
        (
            "meet",
            verb(
                RenderPath::ShipAction,
                &[
                    ("who", GrammarReferentKind::Person),
                    ("where_", GrammarReferentKind::Place),
                ],
            ),
        ),
        ("hail", verb(RenderPath::Conversation, &[])),
    ];
    let (_dir, kernel) = create(bound(Some(grammar_with(&extra)), "Signatures"));
    let mut kernel = kernel.unwrap();
    for (label, roles) in [
        ("renamed", vec![("whom", person()), ("where_", place())]),
        ("rekinded", vec![("who", person()), ("where_", person())]),
        ("swapped", vec![("where_", place()), ("who", person())]),
        ("short", vec![("who", person())]),
        (
            "long",
            vec![("who", person()), ("where_", place()), ("why", person())],
        ),
    ] {
        let set = rejected(admit(
            &mut kernel,
            patch_of(vec![declare(label, "meet", &roles)]),
        ));
        assert_eq!(set, disagrees(label), "{label}");
    }
    // A role on a verb the grammar declares with none.
    let set = rejected(admit(
        &mut kernel,
        patch_of(vec![declare("hail", "hail", &[("who", person())])]),
    ));
    assert_eq!(set, disagrees("hail"));
    // The exact signature is admitted.
    let receipt = admit(
        &mut kernel,
        patch_of(vec![declare(
            "meet",
            "meet",
            &[("who", person()), ("where_", place())],
        )]),
    )
    .unwrap();
    assert!(matches!(receipt, SubmitReceipt::Applied(_)));
}

#[test]
fn each_grammar_referent_kind_admits_exactly_its_own_kernel_kind() {
    let kinds = [
        (GrammarReferentKind::Person, person()),
        (
            GrammarReferentKind::Faction,
            RefKind::Subject(Some(SubjectKind::Institution)),
        ),
        (
            GrammarReferentKind::Population,
            RefKind::Subject(Some(SubjectKind::Population)),
        ),
        (GrammarReferentKind::Place, place()),
        (
            GrammarReferentKind::Cargo,
            RefKind::Entity(EntityKind::Resource),
        ),
    ];
    for (referent, own) in &kinds {
        let extra = [("act", verb(RenderPath::ShipAction, &[("with", *referent)]))];
        for (_, offered) in &kinds {
            let (_dir, kernel) = create(bound(Some(grammar_with(&extra)), "Referents"));
            let mut kernel = kernel.unwrap();
            let result = admit(
                &mut kernel,
                patch_of(vec![declare("act", "act", &[("with", *offered)])]),
            );
            if offered == own {
                assert!(
                    matches!(result, Ok(SubmitReceipt::Applied(_))),
                    "{referent:?} refused its own kind"
                );
            } else {
                assert_eq!(
                    rejected(result),
                    disagrees("act"),
                    "{referent:?} admitted {offered:?}"
                );
            }
        }
    }
}

#[test]
fn grammar_world_without_speak_refuses_the_kernel_speak_grant() {
    let no_speak = GrammarBinding::new(
        "aetheria.grammar",
        1,
        [("convene".to_owned(), verb(RenderPath::Conversation, &[]))],
    )
    .unwrap();
    assert_eq!(
        rejected(create(bound(Some(no_speak), "Mute")).1),
        not_in_grammar(crate::patch::KERNEL_SPEAK_HANDLE)
    );

    // The kernel entry is held to the signature as well as the kind.
    let loud = GrammarBinding::new(
        "aetheria.grammar",
        1,
        [
            (
                "speak".to_owned(),
                verb(
                    RenderPath::Conversation,
                    &[("to", GrammarReferentKind::Person)],
                ),
            ),
            ("convene".to_owned(), verb(RenderPath::Conversation, &[])),
        ],
    )
    .unwrap();
    assert_eq!(
        rejected(create(bound(Some(loud), "Loud")).1),
        disagrees(crate::patch::KERNEL_SPEAK_HANDLE)
    );
}

#[test]
fn unbound_world_keeps_the_open_catalog() {
    let mut world = bound(None, "Open");
    world
        .patch
        .declarations
        .push(declare("wave", "wave", &[("at", person())]));
    let (_dir, kernel) = create(world);
    let mut kernel = kernel.unwrap();
    let receipt = admit(
        &mut kernel,
        patch_of(vec![declare("beacon", "beacon", &[])]),
    )
    .unwrap();
    assert!(matches!(receipt, SubmitReceipt::Applied(_)));
    let snapshot = kernel.snapshot().unwrap();
    assert!(snapshot.grammar.is_none());
    assert!(snapshot.affordances.len() >= 4);
    assert!(snapshot.affordances.iter().all(|a| a.render_path.is_none()));

    // The state row of a world with no grammar carries no grammar key, so its
    // bytes and digests are those of a world written before grammars existed.
    let encoded = rmp_serde::to_vec_named(&kernel.state).unwrap();
    let keys: std::collections::BTreeMap<String, serde::de::IgnoredAny> =
        rmp_serde::from_slice(&encoded).unwrap();
    assert!(!keys.contains_key("grammar"));
    assert!(keys.contains_key("brief"));
}

#[test]
fn a_bound_world_reports_its_grammar_and_derives_render_paths_from_it() {
    let extra = [("dock", verb(RenderPath::ShipAction, &[]))];
    let grammar = grammar_with(&extra);
    let identity = grammar.identity();
    let (_dir, kernel) = create(bound(Some(grammar), "Reports"));
    let mut kernel = kernel.unwrap();
    admit(&mut kernel, patch_of(vec![declare("dock", "dock", &[])])).unwrap();
    let snapshot = kernel.snapshot().unwrap();
    assert_eq!(snapshot.grammar, Some(identity));
    let path_of = |kind: &str| {
        snapshot
            .affordances
            .iter()
            .find(|a| a.entry.kind.0 == kind)
            .unwrap_or_else(|| panic!("{kind} is in the catalog"))
            .render_path
    };
    assert_eq!(path_of("dock"), Some(RenderPath::ShipAction));
    assert_eq!(path_of("convene"), Some(RenderPath::Conversation));
    assert_eq!(path_of("speak"), Some(RenderPath::Conversation));
}

#[test]
fn a_reopened_grammar_world_still_holds_its_grammar() {
    let grammar = grammar_with(&[]);
    let identity = grammar.identity();
    let (directory, kernel) = create(bound(Some(grammar), "Reopened"));
    let kernel = kernel.unwrap();
    let world_id = kernel.snapshot().unwrap().world_id;
    drop(kernel);
    let mut reopened = WorldKernel::open(directory.path().join("world.cc"), world_id).unwrap();
    assert_eq!(reopened.snapshot().unwrap().grammar, Some(identity));
    assert_eq!(
        rejected(admit(
            &mut reopened,
            patch_of(vec![declare("wave", "wave", &[])])
        )),
        not_in_grammar("wave")
    );
}

#[test]
fn the_bound_grammar_is_part_of_the_world_state_digest() {
    let (_dir, kernel) = create(bound(Some(grammar_with(&[])), "Digested"));
    let kernel = kernel.unwrap();
    let bound_digest = crate::state_digest(&kernel.state).unwrap();
    let mut state = kernel.state.clone();
    state.grammar = None;
    let open_digest = crate::state_digest(&state).unwrap();
    state.grammar = Some(grammar_with(&[("dock", verb(RenderPath::ShipAction, &[]))]));
    let other_digest = crate::state_digest(&state).unwrap();
    assert_ne!(bound_digest, open_digest);
    assert_ne!(bound_digest, other_digest);
}

#[test]
fn grammar_digest_is_derived_and_order_free() {
    let forward = [
        ("alpha".to_owned(), verb(RenderPath::ShipAction, &[])),
        (
            "beta".to_owned(),
            verb(
                RenderPath::Conversation,
                &[("to", GrammarReferentKind::Person)],
            ),
        ),
        ("gamma".to_owned(), verb(RenderPath::Conversation, &[])),
    ];
    let mut reversed = forward.clone();
    reversed.reverse();
    let a = GrammarBinding::new("aetheria.grammar", 1, forward.clone()).unwrap();
    let b = GrammarBinding::new("aetheria.grammar", 1, reversed).unwrap();
    assert_eq!(a.digest, b.digest);
    assert_eq!(a, b);

    // Content-derived: each part of a verb moves the digest.
    let changed = |edit: &dyn Fn(&mut Vec<(String, GrammarVerb)>)| {
        let mut verbs = forward.to_vec();
        edit(&mut verbs);
        GrammarBinding::new("aetheria.grammar", 1, verbs)
            .unwrap()
            .digest
    };
    assert_ne!(
        a.digest,
        changed(&|v| v[0].1.render_path = RenderPath::Conversation)
    );
    assert_ne!(a.digest, changed(&|v| v[1].1.roles[0].role = "from".into()));
    assert_ne!(
        a.digest,
        changed(&|v| v[1].1.roles[0].referent = GrammarReferentKind::Faction)
    );
    assert_ne!(a.digest, changed(&|v| v[2].0 = "delta".into()));
    assert_ne!(
        a.digest,
        changed(&|v| {
            v.pop();
        })
    );
    // Role order is part of a verb.
    let ordered = |roles: &[(&str, GrammarReferentKind)]| {
        GrammarBinding::new(
            "g",
            1,
            [("meet".to_owned(), verb(RenderPath::ShipAction, roles))],
        )
        .unwrap()
        .digest
    };
    assert_ne!(
        ordered(&[
            ("a", GrammarReferentKind::Person),
            ("b", GrammarReferentKind::Place)
        ]),
        ordered(&[
            ("b", GrammarReferentKind::Place),
            ("a", GrammarReferentKind::Person)
        ])
    );
    // The identity reads the same fields back.
    let identity = a.identity();
    assert_eq!(identity.schema_id, "aetheria.grammar");
    assert_eq!(identity.revision, 1);
    assert_eq!(identity.digest, a.digest);
}

#[test]
fn a_tampered_grammar_digest_fails_to_deserialise() {
    let binding = grammar_with(&[]);
    let value = serde_json::to_value(&binding).unwrap();
    // Untouched: the round trip holds.
    let back: GrammarBinding = serde_json::from_value(value.clone()).unwrap();
    assert_eq!(back, binding);

    // A digest the verbs do not have.
    let mut forged = value.clone();
    forged["digest"] = "sha256:00".into();
    assert!(serde_json::from_value::<GrammarBinding>(forged.clone()).is_err());

    // A verb edited under the digest it already had.
    let mut edited = value;
    edited["verbs"]["convene"]["render_path"] = "ship_action".into();
    assert!(serde_json::from_value::<GrammarBinding>(edited).is_err());

    // The named encoding a state row uses refuses it the same way.
    let bytes = rmp_serde::to_vec_named(&forged).unwrap();
    assert!(rmp_serde::from_slice::<GrammarBinding>(&bytes).is_err());
}

#[test]
fn a_grammar_names_each_verb_once_and_has_a_schema() {
    assert_eq!(
        GrammarBinding::new("", 1, Vec::new()).unwrap_err(),
        GrammarError::EmptySchemaId
    );
    assert_eq!(
        GrammarBinding::new(
            "g",
            1,
            [
                ("hail".to_owned(), verb(RenderPath::Conversation, &[])),
                ("hail".to_owned(), verb(RenderPath::ShipAction, &[])),
            ]
        )
        .unwrap_err(),
        GrammarError::DuplicateVerb
    );
}

/// The fixture world was written by the code at base 367e2eb, which has no
/// grammar field anywhere. Reopening it must reproduce the digests that code
/// recorded.
#[test]
fn existing_world_reopens_with_unchanged_digests() {
    let directory = tempfile::tempdir().unwrap();
    let path = directory.path().join("world.cc");
    std::fs::write(&path, include_bytes!("../../tests/fixtures/base_world.cc")).unwrap();
    let world_id: WorldId =
        serde_json::from_str(include_str!("../../tests/fixtures/base_world.id").trim()).unwrap();
    let kernel = WorldKernel::open(&path, world_id).expect("a base-written world reopens");
    let snapshot = kernel.snapshot().unwrap();
    let recorded = include_str!("../../tests/fixtures/base_world.digests");
    let mut lines = recorded.lines();
    assert_eq!(snapshot.revision.to_string(), lines.next().unwrap());
    assert_eq!(snapshot.state_digest, lines.next().unwrap());
    assert_eq!(
        snapshot.last_commit_digest.as_deref(),
        Some(lines.next().unwrap())
    );
    assert!(snapshot.grammar.is_none());
    // Its state still derives the recorded digest from its bytes.
    assert_eq!(
        crate::state_digest(&kernel.state).unwrap(),
        snapshot.state_digest
    );
}

fn submit_as(
    kernel: &mut WorldKernel,
    caller: CallerId,
    answers: Option<crate::PatchAnswer>,
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

/// A bound Draft world whose revision has moved past genesis: one owner patch
/// that adds a dead-end place the elaborator can answer.
fn advanced_grammar_world() -> (tempfile::TempDir, WorldKernel, crate::EntityId) {
    let extra = [("hail", verb(RenderPath::Conversation, &[]))];
    let (dir, kernel) = create(bound(Some(grammar_with(&extra)), "Advanced"));
    let mut kernel = kernel.unwrap();
    let commons = *kernel.state.entities.keys().next().unwrap();
    admit(
        &mut kernel,
        patch_of(vec![
            Declaration::Entity(crate::patch::EntityDeclaration {
                handle: DraftHandle::new("dead-end"),
                label: "The Unwalked Road".into(),
                kind: EntityKind::Place,
                container: None,
            }),
            Declaration::Route(crate::patch::RouteDeclaration {
                handle: DraftHandle::new("gate"),
                label: "The Field Gate".into(),
                from: crate::patch::Ref::Existing(commons),
                to: crate::patch::Ref::Draft(DraftHandle::new("dead-end")),
                access: crate::patch::AccessKind::Public,
                cost: crate::patch::Cost(1),
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

type Door = (&'static str, CallerId, Option<crate::PatchAnswer>);

/// Each door refuses an outside kind and a wrong role signature, typed, and the
/// refusal leaves the revision where it was. The owner and the consumer declare
/// unanswered only in Draft; Play and the elaborator act in Active.
fn refuse_at_each_door(kernel: &mut WorldKernel, doors: Vec<Door>) {
    for (door, caller, answers) in doors {
        let revision = kernel.state.revision;
        let kind = submit_as(
            kernel,
            caller.clone(),
            answers.clone(),
            patch_of(vec![declare("wave", "wave", &[])]),
        );
        assert_eq!(rejected(kind), not_in_grammar("wave"), "{door}");
        let roles = submit_as(
            kernel,
            caller,
            answers,
            patch_of(vec![declare("hail", "hail", &[("who", person())])]),
        );
        assert_eq!(rejected(roles), disagrees("hail"), "{door}");
        assert_eq!(
            kernel.state.revision, revision,
            "{door} refusal moved the world"
        );
    }
}

#[test]
fn every_admission_door_refuses_outside_the_grammar_after_the_revision_has_moved() {
    let (_dir, mut kernel, dead_end) = advanced_grammar_world();
    assert_eq!(kernel.state.revision, 1);
    let draft_doors = vec![
        ("owner", CallerId::Principal(owner()), None),
        (
            "consumer",
            CallerId::System(crate::SystemCapability::Consumer {
                consumer: crate::ConsumerId::of_name("grammar-door"),
            }),
            None,
        ),
    ];
    refuse_at_each_door(&mut kernel, draft_doors);
    crate::tests::activate(&mut kernel);
    assert!(
        kernel.state.revision > 1,
        "the world has moved past genesis"
    );
    let answer = crate::derive_boundaries(&kernel.state)
        .unwrap()
        .into_iter()
        .find(|boundary| {
            matches!(boundary, crate::CausalBoundary::UnelaboratedDestination { place, .. }
                if *place == dead_end)
        })
        .expect("the dead end is an unelaborated destination");
    let active_doors = vec![
        (
            "play",
            CallerId::System(crate::SystemCapability::Play),
            None,
        ),
        (
            "elaborator",
            CallerId::System(crate::SystemCapability::Elaborator {
                jurisdiction: crate::JurisdictionKey::PlaceSubtree(dead_end),
            }),
            Some(crate::PatchAnswer::Boundary(answer)),
        ),
    ];
    refuse_at_each_door(&mut kernel, active_doors);
    // The same path admits what the grammar carries, so the refusals above are
    // the grammar's and not a closed door.
    let receipt = submit_as(
        &mut kernel,
        CallerId::System(crate::SystemCapability::Play),
        None,
        patch_of(vec![declare("hail", "hail", &[])]),
    )
    .unwrap();
    assert!(matches!(receipt, SubmitReceipt::Applied(_)));
}

#[test]
fn a_grammar_that_binds_speak_as_anything_but_conversation_is_refused_at_bind() {
    let ship = |roles: &[(&str, GrammarReferentKind)]| {
        GrammarBinding::new(
            "aetheria.grammar",
            1,
            [
                ("speak".to_owned(), verb(RenderPath::ShipAction, roles)),
                ("convene".to_owned(), verb(RenderPath::Conversation, &[])),
            ],
        )
        .unwrap()
    };
    assert_eq!(
        rejected(create(bound(Some(ship(&[])), "Filmed speech")).1),
        vec![Mismatch::SpeakRenderPathDisagreesWithGrammar {
            handle: DraftHandle::new(crate::patch::KERNEL_SPEAK_HANDLE),
        }]
    );
    // A ShipAction speak with a role fails on the signature first and is still
    // refused: no variant of speak but zero roles, Conversation, is admitted.
    assert_eq!(
        rejected(
            create(bound(
                Some(ship(&[("to", GrammarReferentKind::Person)])),
                "Loud"
            ))
            .1
        ),
        disagrees(crate::patch::KERNEL_SPEAK_HANDLE)
    );
    // The control: Conversation speak binds.
    assert!(create(bound(Some(grammar_with(&[])), "Spoken")).1.is_ok());
}

/// `apply_effect` re-decides an effect it is handed. An effect resolved against
/// a world with no grammar must not install an entry the bound grammar lacks:
/// the catalog's only writer holds the grammar check itself.
#[test]
fn an_effect_resolved_elsewhere_cannot_install_an_entry_outside_the_grammar() {
    let extra = [
        (
            "meet",
            verb(
                RenderPath::ShipAction,
                &[("who", GrammarReferentKind::Person)],
            ),
        ),
        ("hail", verb(RenderPath::Conversation, &[])),
    ];
    let (_dir, kernel) = create(bound(Some(grammar_with(&extra)), "Forged"));
    let kernel = kernel.unwrap();
    let mut twin = kernel.state.clone();
    twin.grammar = None;
    let resolve = |kind: &str, roles: &[(&str, RefKind)]| {
        crate::patch::resolve_patch(
            &twin,
            CommandId::new(),
            &patch_of(vec![declare("probe", kind, roles)]),
            None,
            None,
        )
        .expect("the unbound twin admits any canonical kind")
    };
    for (label, resolved, admitted) in [
        ("outside kind", resolve("wave", &[]), false),
        ("wrong roles", resolve("meet", &[("whom", person())]), false),
        ("carried verb", resolve("hail", &[]), true),
    ] {
        let mut state = kernel.state.clone();
        let catalog = state.affordance_catalog.len();
        let result = crate::apply_effect(
            &mut state,
            CommandId::new(),
            &CallerId::Principal(owner()),
            &crate::WorldEffect::PatchAdmitted {
                answers: None,
                resolved,
            },
        );
        if admitted {
            result.unwrap_or_else(|error| panic!("{label}: {error:?}"));
            assert_eq!(state.affordance_catalog.len(), catalog + 1, "{label}");
        } else {
            assert!(
                matches!(result, Err(KernelError::Invariant(_))),
                "{label}: {result:?}"
            );
            assert_eq!(state.affordance_catalog.len(), catalog, "{label}");
        }
    }
}
