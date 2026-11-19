//! The stock lenses. A lens is the flavor an elaborator session reaches for:
//! prompt emphasis over the one fixed tool catalog, never admission, authority,
//! or quota. This module owns the stock table, each lens's instruction text,
//! and the draw recipe; it reads no world state and writes nothing.

use super::{CommandId, KernelError, WorldId, digest};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

/// The sentence every lens's instructions begin with.
const ELABORATION_INSTRUCTIONS: &str = "Use only the supplied tools to author structure inside your jurisdiction. Answer the boundary or deficit you were given, then submit. Recording a gap changes nothing.";

/// The library's stock lens set. Serialized by snake_case name, so a name the
/// library does not know cannot be represented at any deserialization boundary.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Lens {
    Patina,
    Charter,
    Ledger,
    Hearth,
    Tangle,
    Veil,
    Ember,
    Numen,
}

impl Lens {
    /// Every lens, in declaration order. The draw walks this order.
    pub const ALL: [Lens; 8] = [
        Lens::Patina,
        Lens::Charter,
        Lens::Ledger,
        Lens::Hearth,
        Lens::Tangle,
        Lens::Veil,
        Lens::Ember,
        Lens::Numen,
    ];

    pub fn name(self) -> &'static str {
        match self {
            Lens::Patina => "Patina",
            Lens::Charter => "Charter",
            Lens::Ledger => "Ledger",
            Lens::Hearth => "Hearth",
            Lens::Tangle => "Tangle",
            Lens::Veil => "Veil",
            Lens::Ember => "Ember",
            Lens::Numen => "Numen",
        }
    }

    /// What kind of structure the lens reaches for.
    pub(crate) fn reaches_for(self) -> &'static str {
        match self {
            Lens::Patina => "ordinary objects, customs, nicknames, local jokes",
            Lens::Charter => "government, law, offices, selection, succession, redress",
            Lens::Ledger => "resources, labor, trade, infrastructure, scarcity, class pressure",
            Lens::Hearth => "kinship, daily life, care, obligation, belonging",
            Lens::Tangle => "factions, alliances, rivalries, leverage",
            Lens::Veil => "secrets, rumors, misinformation, taboos, unevenly held knowledge",
            Lens::Ember => "disputes, hazards, instability, escalation, urgent pressure",
            Lens::Numen => "religion, magic, ritual, awe, cosmology, the genuinely strange",
        }
    }

    /// Existing `PATCH_TOOLS` names in emphasis order. Emphasis is prompt text
    /// only: the catalog, its order, and its schemas are the same for every lens.
    pub(crate) fn leads_with(self) -> &'static [&'static str] {
        match self {
            Lens::Patina => &[
                "set_persona_material",
                "declare_affordance",
                "declare_resource",
                "declare_place",
            ],
            Lens::Charter => &[
                "grant_authority",
                "open_office",
                "install_incumbent",
                "open_forum",
                "declare_subject",
            ],
            Lens::Ledger => &[
                "transfer",
                "bind",
                "declare_resource",
                "alter_cost",
                "declare_route",
            ],
            Lens::Hearth => &[
                "create_commitment",
                "set_persona_material",
                "declare_subject",
            ],
            Lens::Tangle => &[
                "advance_pressure",
                "create_commitment",
                "grant_authority",
                "declare_subject",
                "bind",
            ],
            Lens::Veil => &[
                "acquire_knowledge",
                "communicate",
                "declare_channel",
                "set_reach",
                "declare_fact",
                "forget",
            ],
            Lens::Ember => &[
                "advance_pressure",
                "create_commitment",
                "close_route",
                "consume",
            ],
            Lens::Numen => &[
                "declare_fact",
                "declare_affordance",
                "set_persona_material",
                "witness",
            ],
        }
    }

    /// The elaborator's instructions under this lens: the shared sentence, then
    /// the lens clause. Read once, when a session is first built; the session
    /// then carries the text, and nothing rebuilds it from here.
    pub(crate) fn instructions(self) -> String {
        format!(
            "{ELABORATION_INSTRUCTIONS} Lens: {}. Reach for {}. Lead with {}; every supplied tool remains available.",
            self.name(),
            self.reaches_for(),
            self.leads_with().join(", "),
        )
    }
}

/// A world's lens weights. Validity is decided where weights are admitted;
/// `Default` is the empty map a world holds before genesis writes its weights,
/// and it never draws.
#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(transparent)]
pub struct LensWeights(BTreeMap<Lens, u32>);

impl LensWeights {
    pub fn new(weights: BTreeMap<Lens, u32>) -> Self {
        Self(weights)
    }

    /// A lens the map does not name weighs zero.
    pub fn get(&self, lens: Lens) -> u32 {
        self.0.get(&lens).copied().unwrap_or(0)
    }

    pub fn iter(&self) -> impl Iterator<Item = (Lens, u32)> + '_ {
        self.0.iter().map(|(lens, weight)| (*lens, *weight))
    }

    /// Some weight is nonzero, so a draw has somewhere to land.
    pub(crate) fn draws(&self) -> bool {
        self.0.values().any(|weight| *weight > 0)
    }

    /// Every stock lens weighs the same in both sets. A lens a map does not
    /// name weighs zero, so `{charter: 3}` and `{patina: 0, charter: 3}` are
    /// one set spelled two ways: they draw identically and neither replaces
    /// the other.
    pub(crate) fn weighs_the_same_as(&self, other: &LensWeights) -> bool {
        Lens::ALL
            .into_iter()
            .all(|lens| self.get(lens) == other.get(lens))
    }
}

/// The draw's whole input: the world and the session's fixed identity. The
/// weights select over it; they are not part of it.
#[derive(Serialize)]
struct LensPreimage {
    world_id: WorldId,
    command_id: CommandId,
}

/// One digest, one modulo, one cumulative walk over `Lens::ALL` — the recipe
/// `select_band` uses, not a second entropy shape. The same world, session
/// identity, and weights draw the same lens. Modulo rather than rejection
/// sampling: the bias is bounded by `total_weight / 2^64`.
pub(crate) fn draw(
    weights: &LensWeights,
    world_id: WorldId,
    command_id: CommandId,
) -> Result<Lens, KernelError> {
    // The guard is on the divisor itself, as `select_band`'s is: no other
    // statement of "these weights draw" can let the modulo below divide by zero.
    let total: u128 = Lens::ALL
        .iter()
        .map(|lens| u128::from(weights.get(*lens)))
        .sum();
    if total == 0 {
        return Err(KernelError::Invariant("lens weights never draw".into()));
    }
    let preimage = digest(&LensPreimage {
        world_id,
        command_id,
    })?;
    let head = preimage
        .strip_prefix("sha256:")
        .and_then(|hex| hex.get(..16))
        .and_then(|head| u64::from_str_radix(head, 16).ok())
        .ok_or_else(|| KernelError::Invariant("lens draw digest is not hex".into()))?;
    let mut position = u128::from(head) % total;
    for lens in Lens::ALL {
        let weight = u128::from(weights.get(lens));
        if position < weight {
            return Ok(lens);
        }
        position -= weight;
    }
    Err(KernelError::Invariant(
        "lens weights did not cover the draw".into(),
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::patch::PATCH_TOOLS;
    use crate::tests::stock_weights;
    use std::collections::BTreeSet;

    fn ids() -> Vec<CommandId> {
        (0..200)
            .map(|i| CommandId::derived("l1-probe", &[&i.to_string()]))
            .collect()
    }

    fn tally(weights: &LensWeights) -> BTreeMap<Lens, usize> {
        let mut counts = BTreeMap::new();
        for id in ids() {
            let lens = draw(weights, WorldId::nil_for_test(), id).unwrap();
            *counts.entry(lens).or_insert(0) += 1;
        }
        counts
    }

    #[test]
    fn every_lead_names_a_patch_tool() {
        let catalog: BTreeSet<&str> = PATCH_TOOLS.iter().map(|entry| entry.name).collect();
        for lens in Lens::ALL {
            let leads = lens.leads_with();
            assert!(!leads.is_empty(), "{lens:?} leads with nothing");
            let distinct: BTreeSet<&str> = leads.iter().copied().collect();
            assert_eq!(distinct.len(), leads.len(), "{lens:?} repeats a lead");
            for name in leads {
                assert!(
                    catalog.contains(name),
                    "{lens:?} leads with unknown tool {name}"
                );
            }
        }
    }

    #[test]
    fn the_draw_is_a_function_of_world_and_session() {
        let weights = stock_weights();
        let world = WorldId::nil_for_test();
        for id in ids() {
            assert_eq!(
                draw(&weights, world, id).unwrap(),
                draw(&weights, world, id).unwrap()
            );
        }
        let expected: BTreeMap<Lens, usize> = BTREE_STOCK_TALLY.into_iter().collect();
        assert_eq!(tally(&weights), expected);
    }

    /// Computed at Cut 1 and pinned: the replay proof for 200 derived ids under
    /// stock weights.
    const BTREE_STOCK_TALLY: [(Lens, usize); 8] = [
        (Lens::Patina, 22),
        (Lens::Charter, 37),
        (Lens::Ledger, 26),
        (Lens::Hearth, 19),
        (Lens::Tangle, 23),
        (Lens::Veil, 29),
        (Lens::Ember, 18),
        (Lens::Numen, 26),
    ];

    #[test]
    fn a_zero_weight_never_draws() {
        let weights = LensWeights::new(BTreeMap::from([
            (Lens::Patina, 0),
            (Lens::Charter, 3),
            (Lens::Ledger, 1),
        ]));
        let counts = tally(&weights);
        assert_eq!(counts.get(&Lens::Patina), None, "a zero weight drew");
        let expected: BTreeMap<Lens, usize> = ZERO_WEIGHT_TALLY.into_iter().collect();
        assert_eq!(counts, expected);
    }

    /// Computed at Cut 1 and pinned; near 3:1 over the same 200 ids.
    const ZERO_WEIGHT_TALLY: [(Lens, usize); 2] = [(Lens::Charter, 155), (Lens::Ledger, 45)];

    #[test]
    fn all_zero_weights_are_an_error() {
        let world = WorldId::nil_for_test();
        let id = CommandId::derived("l1-probe", &["0"]);
        let all_zero = LensWeights::new(Lens::ALL.into_iter().map(|lens| (lens, 0)).collect());
        assert_eq!(all_zero.iter().count(), 8);
        for weights in [LensWeights::default(), all_zero] {
            assert!(matches!(
                draw(&weights, world, id),
                Err(KernelError::Invariant(_))
            ));
        }
    }

    #[test]
    fn an_unknown_lens_name_does_not_deserialize() {
        for refused in [
            r#"{"Patina":1}"#,
            r#"{"patina ":1}"#,
            r#"{"patinaa":1}"#,
            r#"{"patina":1,"tribunal":1}"#,
        ] {
            assert!(
                serde_json::from_str::<LensWeights>(refused).is_err(),
                "{refused} deserialized"
            );
        }
        let accepted = serde_json::from_str::<LensWeights>(r#"{"patina":1}"#).unwrap();
        assert_eq!(accepted.get(Lens::Patina), 1);
        assert_eq!(accepted.iter().count(), 1);

        let stock = stock_weights();
        let bytes = rmp_serde::to_vec_named(&stock).unwrap();
        let back: LensWeights = rmp_serde::from_slice(&bytes).unwrap();
        assert_eq!(back, stock);
        assert_eq!(rmp_serde::to_vec_named(&back).unwrap(), bytes);
    }

    #[test]
    fn each_lens_text_names_its_own_leads() {
        let mut texts = BTreeSet::new();
        for lens in Lens::ALL {
            let text = lens.instructions();
            assert!(text.contains(lens.name()), "{lens:?} text omits its name");
            for name in lens.leads_with() {
                assert!(text.contains(name), "{lens:?} text omits {name}");
            }
            texts.insert(text);
        }
        assert_eq!(texts.len(), 8, "two lenses share a text");
    }

    #[test]
    fn lenses_serialize_as_snake_case_names() {
        let names: Vec<String> = Lens::ALL
            .iter()
            .map(|lens| serde_json::to_string(lens).unwrap())
            .collect();
        assert_eq!(
            names,
            [
                r#""patina""#,
                r#""charter""#,
                r#""ledger""#,
                r#""hearth""#,
                r#""tangle""#,
                r#""veil""#,
                r#""ember""#,
                r#""numen""#,
            ]
        );
    }
}
