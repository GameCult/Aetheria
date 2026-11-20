//! The state digest: a homomorphic set hash over per-document digests.
//!
//! Each document (one row of a partition, or the world header) has a digest
//! `sha256(len schema, schema, len key, key, rmp named value)`. The world's
//! accumulator is LtHash16 (Lewi et al. 2019): a document digest is expanded to
//! 2048 bytes and added lane-wise as 1024 wrapping `u16`; removing a document
//! subtracts it, and updating one subtracts the old and adds the new. So the
//! digest of a commit costs what the commit changed, not the size of the world,
//! and it depends on the set of documents alone, never on the order they were
//! written. `state_digest` seals the accumulator with the revision.

use crate::docs::{DocKey, Docs, Tracked};
use crate::{KernelError, STATE_SCHEMA};
use serde::Serialize;
use sha2::{Digest, Sha256};

const LANES: usize = 1024;
/// 64 blocks of 32 bytes: 2048 bytes, 1024 lanes.
const BLOCKS: u8 = 64;

pub(crate) type DocDigest = [u8; 32];

#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) struct LtHash16(Box<[u16; LANES]>);

impl Default for LtHash16 {
    fn default() -> Self {
        Self(Box::new([0; LANES]))
    }
}

impl LtHash16 {
    fn lanes(doc: &DocDigest) -> impl Iterator<Item = u16> + '_ {
        (0..BLOCKS).flat_map(move |block| {
            let mut hasher = Sha256::new();
            hasher.update(doc);
            hasher.update([block]);
            let expanded: [u8; 32] = hasher.finalize().into();
            (0..16).map(move |lane| u16::from_le_bytes([expanded[2 * lane], expanded[2 * lane + 1]]))
        })
    }

    pub(crate) fn add(&mut self, doc: &DocDigest) {
        for (slot, lane) in self.0.iter_mut().zip(Self::lanes(doc)) {
            *slot = slot.wrapping_add(lane);
        }
    }

    pub(crate) fn subtract(&mut self, doc: &DocDigest) {
        for (slot, lane) in self.0.iter_mut().zip(Self::lanes(doc)) {
            *slot = slot.wrapping_sub(lane);
        }
    }

    /// The commit-facing digest: the accumulator sealed with the revision.
    pub(crate) fn state_digest(&self, revision: u64) -> String {
        let mut hasher = Sha256::new();
        hasher.update(STATE_SCHEMA.as_bytes());
        hasher.update(revision.to_be_bytes());
        for lane in self.0.iter() {
            hasher.update(lane.to_le_bytes());
        }
        format!("sha256:{:x}", hasher.finalize())
    }
}

#[cfg(test)]
thread_local! {
    /// Document digests computed on this thread, for the work-bound test.
    pub(crate) static DOC_DIGESTS: std::cell::Cell<usize> = const { std::cell::Cell::new(0) };
}

pub(crate) fn doc_digest<V: Serialize>(
    schema: &str,
    key: &str,
    value: &V,
) -> Result<DocDigest, KernelError> {
    #[cfg(test)]
    DOC_DIGESTS.with(|count| count.set(count.get() + 1));
    let bytes = rmp_serde::to_vec_named(value)
        .map_err(|error| KernelError::Serialization(error.to_string()))?;
    let mut hasher = Sha256::new();
    hasher.update((schema.len() as u64).to_be_bytes());
    hasher.update(schema.as_bytes());
    hasher.update((key.len() as u64).to_be_bytes());
    hasher.update(key.as_bytes());
    hasher.update(bytes);
    Ok(hasher.finalize().into())
}

/// One change of one document: subtract the old, add the new.
pub(crate) fn fold_change<V: Serialize>(
    acc: &mut LtHash16,
    schema: &str,
    key: &str,
    old: Option<&V>,
    new: Option<&V>,
) -> Result<(), KernelError> {
    if let Some(old) = old {
        acc.subtract(&doc_digest(schema, key, old)?);
    }
    if let Some(new) = new {
        acc.add(&doc_digest(schema, key, new)?);
    }
    Ok(())
}

/// What `WorldState` needs of a partition, whatever its key and value types.
pub(crate) trait Partition {
    fn is_clean(&self) -> bool;
    fn rollback(&mut self);
    fn finish_log(&mut self);
    /// Folds the open transaction's changes into `acc`.
    fn fold_changes(&self, schema: &str, acc: &mut LtHash16) -> Result<(), KernelError>;
    /// Every document's digest, by (schema, key): the full enumeration the
    /// incremental digest is checked against.
    #[cfg(test)]
    fn documents(
        &self,
        schema: &str,
        out: &mut std::collections::BTreeMap<(String, String), DocDigest>,
    ) -> Result<(), KernelError>;
    /// The partition as encoded: the bytes a test scans for what must never appear.
    #[cfg(test)]
    fn encoded(&self) -> Vec<u8>;
    /// How many documents the partition holds.
    #[cfg(test)]
    fn rows(&self) -> usize;
    /// Removes the first document, logged like any write; false when empty.
    #[cfg(test)]
    fn remove_first(&mut self) -> bool;
}

impl<K, V> Partition for Docs<K, V>
where
    K: Ord + Clone + DocKey + Serialize,
    V: Clone + PartialEq + Serialize,
{
    fn is_clean(&self) -> bool {
        Docs::is_clean(self)
    }

    fn rollback(&mut self) {
        Docs::rollback(self);
    }

    fn finish_log(&mut self) {
        self.finish();
    }

    fn fold_changes(&self, schema: &str, acc: &mut LtHash16) -> Result<(), KernelError> {
        for (key, old, new) in self.changes() {
            fold_change(acc, schema, &key.key_text(), old, new)?;
        }
        Ok(())
    }

    #[cfg(test)]
    fn documents(
        &self,
        schema: &str,
        out: &mut std::collections::BTreeMap<(String, String), DocDigest>,
    ) -> Result<(), KernelError> {
        for (key, value) in self.iter() {
            let key = key.key_text();
            let digest = doc_digest(schema, &key, value)?;
            out.insert((schema.to_owned(), key), digest);
        }
        Ok(())
    }

    #[cfg(test)]
    fn encoded(&self) -> Vec<u8> {
        rmp_serde::to_vec_named(self).expect("a partition encodes")
    }

    #[cfg(test)]
    fn rows(&self) -> usize {
        self.len()
    }

    #[cfg(test)]
    fn remove_first(&mut self) -> bool {
        let Some(key) = self.keys().next().cloned() else {
            return false;
        };
        self.remove(&key).is_some()
    }
}

/// The header's one change, keyed by its own world id.
pub(crate) fn fold_header<T: Clone + PartialEq + Serialize>(
    acc: &mut LtHash16,
    schema: &str,
    key: &str,
    header: &Tracked<T>,
) -> Result<(), KernelError> {
    if let Some((old, new)) = header.change() {
        fold_change(acc, schema, key, old, Some(new))?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn documents(count: u32) -> Vec<DocDigest> {
        (0..count)
            .map(|index| doc_digest("aetherlight.test.v1", &index.to_string(), &index).unwrap())
            .collect()
    }

    /// Order never matters and a subtraction undoes an addition, so the digest
    /// is a function of the set of documents alone.
    #[test]
    fn lthash_add_then_subtract_is_zero() {
        let docs = documents(300);
        let mut acc = LtHash16::default();
        for doc in &docs {
            acc.add(doc);
        }
        assert_ne!(acc, LtHash16::default());

        let mut backwards = LtHash16::default();
        for doc in docs.iter().rev() {
            backwards.add(doc);
        }
        assert_eq!(acc, backwards, "equal sets in two orders differ");

        let mut strided = LtHash16::default();
        for index in (0..docs.len()).map(|step| (step * 7) % docs.len()) {
            strided.add(&docs[index]);
        }
        assert_eq!(acc, strided, "equal sets in a third order differ");

        for index in (0..docs.len()).map(|step| (step * 11) % docs.len()) {
            acc.subtract(&docs[index]);
        }
        assert_eq!(acc, LtHash16::default(), "add then subtract left a residue");
    }

    /// A lane that carries must wrap: 40 additions of one document overflow
    /// nearly every lane of a `u16`, and the subtractions still return to zero.
    #[test]
    fn lanes_wrap_instead_of_saturating() {
        let doc = documents(1)[0];
        let mut acc = LtHash16::default();
        for _ in 0..40 {
            acc.add(&doc);
        }
        for _ in 0..40 {
            acc.subtract(&doc);
        }
        assert_eq!(acc, LtHash16::default());
    }

    #[test]
    fn the_state_digest_seals_the_revision_and_every_lane() {
        let docs = documents(3);
        let mut acc = LtHash16::default();
        docs.iter().for_each(|doc| acc.add(doc));
        assert_ne!(acc.state_digest(1), acc.state_digest(2), "the revision is not sealed");
        let mut other = acc.clone();
        other.0[1023] = other.0[1023].wrapping_add(1);
        assert_ne!(acc.state_digest(1), other.state_digest(1), "the last lane is not hashed");
    }

    #[test]
    fn a_document_digest_separates_schema_key_and_value() {
        let one = doc_digest("ab", "c", &1u8).unwrap();
        assert_ne!(one, doc_digest("a", "bc", &1u8).unwrap(), "schema and key run together");
        assert_ne!(one, doc_digest("ab", "c", &2u8).unwrap());
        assert_ne!(one, doc_digest("ab", "d", &1u8).unwrap());
        assert_eq!(one, doc_digest("ab", "c", &1u8).unwrap());
    }
}
