//! Tracked documents: the one writer of a partition and the one recorder of
//! what a transaction changed.
//!
//! A `Docs` is a map whose every write goes through a method that remembers the
//! key's value as it stood before the transaction (first touch wins). `finish`
//! turns that record into the keys whose value really changed, with their old
//! values; `rollback` puts every touched key back. Reads see the plain map
//! through `Deref`, and there is no `DerefMut`, so a write cannot skip the log.
//! The fields are private to this module for the same reason.

use serde::{Serialize, Serializer};
use std::collections::BTreeMap;
use std::collections::btree_map::Entry;
use std::ops::Deref;

/// A key whose value changed in a transaction, with the value it had before
/// (`None` when the key was absent). The new value is whatever the map holds.
pub(crate) type Changed<K, V> = (K, Option<V>);

#[derive(Clone, Debug)]
pub(crate) struct Docs<K: Ord + Clone, V: Clone + PartialEq> {
    map: BTreeMap<K, V>,
    touched: BTreeMap<K, Option<V>>,
}

impl<K: Ord + Clone, V: Clone + PartialEq> Default for Docs<K, V> {
    fn default() -> Self {
        Self {
            map: BTreeMap::new(),
            touched: BTreeMap::new(),
        }
    }
}

/// Equality is the documents, never the log: two worlds with the same content
/// are equal whatever transaction state they are in.
impl<K: Ord + Clone, V: Clone + PartialEq> PartialEq for Docs<K, V> {
    fn eq(&self, other: &Self) -> bool {
        self.map == other.map
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq + Eq> Eq for Docs<K, V> {}

impl<K: Ord + Clone, V: Clone + PartialEq> Deref for Docs<K, V> {
    type Target = BTreeMap<K, V>;
    fn deref(&self) -> &BTreeMap<K, V> {
        &self.map
    }
}

impl<'a, K: Ord + Clone, V: Clone + PartialEq> IntoIterator for &'a Docs<K, V> {
    type Item = (&'a K, &'a V);
    type IntoIter = std::collections::btree_map::Iter<'a, K, V>;
    fn into_iter(self) -> Self::IntoIter {
        self.map.iter()
    }
}

/// A partition encodes as its map, so the encoded world is what it was before
/// the partitions were tracked.
impl<K: Ord + Clone + Serialize, V: Clone + PartialEq + Serialize> Serialize for Docs<K, V> {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.map.serialize(serializer)
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq> Docs<K, V> {
    fn touch(&mut self, key: &K) {
        if !self.touched.contains_key(key) {
            self.touched.insert(key.clone(), self.map.get(key).cloned());
        }
    }

    pub(crate) fn insert(&mut self, key: K, value: V) -> Option<V> {
        self.touch(&key);
        self.map.insert(key, value)
    }

    pub(crate) fn remove(&mut self, key: &K) -> Option<V> {
        self.touch(key);
        self.map.remove(key)
    }

    /// Touches only a key that is present: a miss writes nothing.
    pub(crate) fn get_mut(&mut self, key: &K) -> Option<&mut V> {
        if self.map.contains_key(key) {
            self.touch(key);
        }
        self.map.get_mut(key)
    }

    pub(crate) fn entry(&mut self, key: K) -> Entry<'_, K, V> {
        self.touch(&key);
        self.map.entry(key)
    }

    /// The open transaction's changed keys as (key, old, new), without closing it.
    pub(crate) fn changes(&self) -> impl Iterator<Item = (&K, Option<&V>, Option<&V>)> + '_ {
        self.touched.iter().filter_map(|(key, old)| {
            let new = self.map.get(key);
            (new != old.as_ref()).then_some((key, old.as_ref(), new))
        })
    }

    /// The keys whose value differs from the start of the transaction, with the
    /// value they had then. A key written back to what it was is not reported.
    /// The log is empty afterwards.
    pub(crate) fn finish(&mut self) -> Vec<Changed<K, V>> {
        std::mem::take(&mut self.touched)
            .into_iter()
            .filter(|(key, old)| self.map.get(key) != old.as_ref())
            .collect()
    }

    /// Puts every touched key back as it was and empties the log.
    pub(crate) fn rollback(&mut self) {
        for (key, old) in std::mem::take(&mut self.touched) {
            match old {
                Some(value) => {
                    self.map.insert(key, value);
                }
                None => {
                    self.map.remove(&key);
                }
            }
        }
    }

    pub(crate) fn is_clean(&self) -> bool {
        self.touched.is_empty()
    }
}

/// One tracked value: the same first-touch rule as a `Docs` key, for the
/// world's own fields.
#[derive(Clone, Debug)]
pub(crate) struct Tracked<T: Clone + PartialEq> {
    value: T,
    /// `None` untouched; `Some(None)` logged as absent (genesis), so `finish`
    /// reports it as added.
    old: Option<Option<T>>,
}

impl<T: Clone + PartialEq> PartialEq for Tracked<T> {
    fn eq(&self, other: &Self) -> bool {
        self.value == other.value
    }
}

impl<T: Clone + PartialEq + Eq> Eq for Tracked<T> {}

impl<T: Clone + PartialEq> Deref for Tracked<T> {
    type Target = T;
    fn deref(&self) -> &T {
        &self.value
    }
}

impl<T: Clone + PartialEq> Tracked<T> {
    /// A value that did not exist before this transaction.
    pub(crate) fn absent(value: T) -> Self {
        Self {
            value,
            old: Some(None),
        }
    }

    pub(crate) fn get_mut(&mut self) -> &mut T {
        if self.old.is_none() {
            self.old = Some(Some(self.value.clone()));
        }
        &mut self.value
    }

    /// The old value when the value changed (`Some(None)`: it was absent).
    pub(crate) fn finish(&mut self) -> Option<Option<T>> {
        match self.old.take() {
            Some(old) if old.as_ref() != Some(&self.value) => Some(old),
            _ => None,
        }
    }

    /// (old, new) when the value changed in the open transaction.
    pub(crate) fn change(&self) -> Option<(Option<&T>, &T)> {
        match &self.old {
            Some(old) if old.as_ref() != Some(&self.value) => Some((old.as_ref(), &self.value)),
            _ => None,
        }
    }

    pub(crate) fn rollback(&mut self) {
        if let Some(Some(old)) = self.old.take() {
            self.value = old;
        }
    }

    pub(crate) fn is_clean(&self) -> bool {
        self.old.is_none()
    }
}

/// The text a document is keyed by in its digest: stable, canonical, one per
/// key. An id is its uuid as `Display` prints it.
pub(crate) trait DocKey {
    fn key_text(&self) -> String;
}

impl<A: DocKey, B: DocKey> DocKey for (A, B) {
    fn key_text(&self) -> String {
        format!("{}/{}", self.0.key_text(), self.1.key_text())
    }
}
