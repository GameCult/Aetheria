//! The action grammar a world is bound to at creation.
//!
//! A grammar is the closed set of affordance kinds a world may declare, each
//! with its role signature and the one render path its acts take. The grammar's
//! home is the Aetheria repo; the kernel holds only this binding: the verbs it
//! enforces, and a content digest that names exactly which grammar that was.
//! The digest is derived here and nowhere else, so a binding cannot claim a
//! digest its verbs do not have.

use crate::SubjectKind;
use crate::patch::{AffordanceKindName, EntityKind, RefKind, Role, RoleSpec, kernel_speak_entry};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;

/// How an act of one verb reaches the player: filmed in the engine as a ship
/// action, or shown as Aetheria's portrait-and-choices conversation.
#[derive(Clone, Copy, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
#[serde(rename_all = "snake_case")]
pub enum RenderPath {
    ShipAction,
    Conversation,
}

/// What a grammar role may be bound to, in the grammar's own vocabulary. Each
/// lowers to exactly one kernel `RefKind`.
#[derive(Clone, Copy, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
#[serde(rename_all = "snake_case")]
pub enum GrammarReferentKind {
    Person,
    Faction,
    Population,
    Place,
    Cargo,
}

impl GrammarReferentKind {
    fn ref_kind(self) -> RefKind {
        match self {
            Self::Person => RefKind::Subject(Some(SubjectKind::Person)),
            Self::Faction => RefKind::Subject(Some(SubjectKind::Institution)),
            Self::Population => RefKind::Subject(Some(SubjectKind::Population)),
            Self::Place => RefKind::Entity(EntityKind::Place),
            Self::Cargo => RefKind::Entity(EntityKind::Resource),
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct GrammarRole {
    pub role: String,
    pub referent: GrammarReferentKind,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct GrammarVerb {
    pub render_path: RenderPath,
    /// In signature order: the order is part of the verb.
    pub roles: Vec<GrammarRole>,
}

impl GrammarVerb {
    /// Whether a declared role list is this verb's signature: same names, same
    /// referent kinds, same order.
    pub(crate) fn admits_roles(&self, roles: &[RoleSpec]) -> bool {
        self.role_specs() == roles
    }

    /// This verb's signature as the kernel declares it: the one lowering of a
    /// grammar role, so declaring a verb and admitting a declaration cannot
    /// disagree.
    pub(crate) fn role_specs(&self) -> Vec<RoleSpec> {
        self.roles
            .iter()
            .map(|role| RoleSpec {
                role: Role(role.role.clone()),
                kind: role.referent.ref_kind(),
            })
            .collect()
    }
}

/// Why a bound grammar refuses an affordance entry.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum GrammarRefusal {
    KindNotInGrammar,
    RolesDisagree,
    SpeakNotConversation,
}

#[derive(Clone, Copy, Debug, thiserror::Error, PartialEq, Eq)]
pub enum GrammarError {
    #[error("grammar schema id is empty")]
    EmptySchemaId,
    #[error("grammar declares one verb kind twice")]
    DuplicateVerb,
    #[error("grammar digest does not match its verbs")]
    DigestMismatch,
}

/// Which grammar a world is bound to: its schema id, revision and content
/// digest. What a consumer reads; the verb table stays in the binding.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GrammarIdentity {
    pub schema_id: String,
    pub revision: u32,
    pub digest: String,
}

/// The grammar a world was created under. Immutable: the only constructor
/// derives the digest, and deserialisation re-derives it and refuses a mismatch.
#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(try_from = "RawBinding")]
pub struct GrammarBinding {
    schema_id: String,
    revision: u32,
    digest: String,
    verbs: BTreeMap<String, GrammarVerb>,
}

#[derive(Deserialize)]
struct RawBinding {
    schema_id: String,
    revision: u32,
    digest: String,
    verbs: BTreeMap<String, GrammarVerb>,
}

impl TryFrom<RawBinding> for GrammarBinding {
    type Error = GrammarError;

    fn try_from(raw: RawBinding) -> Result<Self, GrammarError> {
        let bound = Self::new(raw.schema_id, raw.revision, raw.verbs)?;
        if bound.digest != raw.digest {
            return Err(GrammarError::DigestMismatch);
        }
        Ok(bound)
    }
}

impl GrammarBinding {
    /// The one constructor. Verbs arrive in any order and are keyed by kind;
    /// the digest is the sha256 of the canonical encoding of the kind-sorted
    /// verbs, so the same verbs bind to the same digest however they were listed.
    pub fn new(
        schema_id: impl Into<String>,
        revision: u32,
        verbs: impl IntoIterator<Item = (String, GrammarVerb)>,
    ) -> Result<Self, GrammarError> {
        let schema_id = schema_id.into();
        if schema_id.is_empty() {
            return Err(GrammarError::EmptySchemaId);
        }
        let mut table = BTreeMap::new();
        for (kind, verb) in verbs {
            if table.insert(kind, verb).is_some() {
                return Err(GrammarError::DuplicateVerb);
            }
        }
        let encoded = rmp_serde::to_vec_named(&table).expect("a verb table encodes");
        let digest = format!("sha256:{:x}", Sha256::digest(encoded));
        Ok(Self {
            schema_id,
            revision,
            digest,
            verbs: table,
        })
    }

    pub fn identity(&self) -> GrammarIdentity {
        GrammarIdentity {
            schema_id: self.schema_id.clone(),
            revision: self.revision,
            digest: self.digest.clone(),
        }
    }

    /// Every verb, in kind order.
    pub(crate) fn verbs(&self) -> impl Iterator<Item = (&str, &GrammarVerb)> {
        self.verbs.iter().map(|(kind, verb)| (kind.as_str(), verb))
    }

    pub(crate) fn verb(&self, kind: &str) -> Option<&GrammarVerb> {
        self.verbs.get(kind)
    }

    /// The one grammar check. The resolver turns a refusal into a mismatch and
    /// `admit_resolved`, the only writer of the affordance catalog, into an
    /// invariant error, so no path adds an entry without passing it.
    pub(crate) fn refusal(
        &self,
        kind: &AffordanceKindName,
        roles: &[RoleSpec],
    ) -> Option<GrammarRefusal> {
        let Some(verb) = self.verb(&kind.0) else {
            return Some(GrammarRefusal::KindNotInGrammar);
        };
        if !verb.admits_roles(roles) {
            return Some(GrammarRefusal::RolesDisagree);
        }
        if *kind == kernel_speak_entry().kind && verb.render_path != RenderPath::Conversation {
            return Some(GrammarRefusal::SpeakNotConversation);
        }
        None
    }
}

#[cfg(test)]
mod tests;
