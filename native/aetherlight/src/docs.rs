//! Tracked documents: the one writer of a partition and the one recorder of
//! what a transaction changed.
//!
//! A `Docs` is a map whose every write goes through a method that remembers the
//! key's value as it stood before the transaction (first touch wins). `finish`
//! turns that record into the keys whose value really changed, with their old
//! values; `rollback` puts every touched key back. Reads see the plain map
//! through inherent methods, and no method hands out the map, so a write cannot skip the log.
//! The fields are private to this module for the same reason.

use serde::{Serialize, Serializer};
use std::collections::BTreeMap;
use std::collections::btree_map::Entry;
use std::ops::Deref;

#[cfg(test)]
thread_local! {
    /// Rows any `Docs` iterator has yielded on this thread, and entries any
    /// `indexed` read has returned: the work a read did, counted where the
    /// reads are owned so no reader can skip it.
    static ROWS_READ: std::cell::Cell<u64> = const { std::cell::Cell::new(0) };
}

/// The rows read on this thread so far (test builds only).
#[cfg(test)]
pub(crate) fn rows_read() -> u64 {
    ROWS_READ.with(|rows| rows.get())
}

#[cfg(test)]
fn count_rows(rows: u64) {
    ROWS_READ.with(|count| count.set(count.get() + rows));
}

/// A partition iterator that counts every row it yields (test builds); in a
/// shipped build it is the map's own iterator.
pub(crate) struct Counted<I>(I);

impl<I: Iterator> Iterator for Counted<I> {
    type Item = I::Item;
    fn next(&mut self) -> Option<I::Item> {
        let item = self.0.next();
        #[cfg(test)]
        if item.is_some() {
            count_rows(1);
        }
        item
    }
    fn size_hint(&self) -> (usize, Option<usize>) {
        self.0.size_hint()
    }
}

impl<I: DoubleEndedIterator> DoubleEndedIterator for Counted<I> {
    fn next_back(&mut self) -> Option<I::Item> {
        let item = self.0.next_back();
        #[cfg(test)]
        if item.is_some() {
            count_rows(1);
        }
        item
    }
}

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

impl<'a, K: Ord + Clone, V: Clone + PartialEq> IntoIterator for &'a Docs<K, V> {
    type Item = (&'a K, &'a V);
    type IntoIter = Counted<std::collections::btree_map::Iter<'a, K, V>>;
    fn into_iter(self) -> Self::IntoIter {
        self.iter()
    }
}

/// A partition encodes as its map, so the encoded world is what it was before
/// the partitions were tracked.
impl<K: Ord + Clone + Serialize, V: Clone + PartialEq + Serialize> Serialize for Docs<K, V> {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.map.serialize(serializer)
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq> std::ops::Index<&K> for Docs<K, V> {
    type Output = V;
    fn index(&self, key: &K) -> &V {
        &self.map[key]
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq> Docs<K, V> {
    pub(crate) fn get(&self, key: &K) -> Option<&V> {
        self.map.get(key)
    }

    pub(crate) fn contains_key(&self, key: &K) -> bool {
        self.map.contains_key(key)
    }

    pub(crate) fn len(&self) -> usize {
        self.map.len()
    }

    pub(crate) fn is_empty(&self) -> bool {
        self.map.is_empty()
    }

    pub(crate) fn iter(&self) -> Counted<std::collections::btree_map::Iter<'_, K, V>> {
        Counted(self.map.iter())
    }

    pub(crate) fn keys(&self) -> Counted<std::collections::btree_map::Keys<'_, K, V>> {
        Counted(self.map.keys())
    }

    pub(crate) fn values(&self) -> Counted<std::collections::btree_map::Values<'_, K, V>> {
        Counted(self.map.values())
    }

    pub(crate) fn range<R: std::ops::RangeBounds<K>>(
        &self,
        range: R,
    ) -> Counted<std::collections::btree_map::Range<'_, K, V>> {
        Counted(self.map.range(range))
    }

    /// The plain map, for a test that compares a decoded partition to it.
    #[cfg(test)]
    pub(crate) fn as_map(&self) -> &BTreeMap<K, V> {
        &self.map
    }

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

#[cfg(test)]
mod tests {
    use super::*;

    type Rows = Docs<u8, u8>;

    fn rows(held: &[(u8, u8)]) -> Rows {
        let mut docs = Rows::default();
        for (key, value) in held {
            docs.insert(*key, *value);
        }
        docs.finish();
        docs
    }

    fn contents(docs: &Rows) -> Vec<(u8, u8)> {
        docs.iter().map(|(key, value)| (*key, *value)).collect()
    }

    #[test]
    fn a_write_is_reported_with_the_value_the_transaction_began_with() {
        let mut docs = rows(&[(1, 10), (2, 20)]);
        docs.insert(1, 11);
        docs.insert(1, 12);
        docs.insert(3, 30);
        docs.remove(&2);
        let mut changed = docs.finish();
        changed.sort();
        assert_eq!(changed, vec![(1, Some(10)), (2, Some(20)), (3, None)]);
        assert!(docs.finish().is_empty(), "finish empties the log");
        assert!(docs.is_clean());
    }

    #[test]
    fn a_key_written_back_to_what_it_was_is_not_a_change() {
        let mut docs = rows(&[(1, 10), (2, 20)]);
        docs.insert(1, 11);
        docs.insert(1, 10);
        docs.insert(3, 30);
        docs.remove(&3);
        *docs.get_mut(&2).unwrap() = 20;
        *docs.entry(2).or_insert(0) = 20;
        assert!(docs.changes().next().is_none());
        assert!(docs.finish().is_empty());
    }

    #[test]
    fn a_get_mut_that_misses_touches_nothing() {
        let mut docs = rows(&[(1, 10)]);
        assert!(docs.get_mut(&9).is_none());
        assert!(docs.is_clean());
        *docs.get_mut(&1).unwrap() += 1;
        assert!(!docs.is_clean());
        assert_eq!(docs.finish(), vec![(1, Some(10))]);
    }

    #[test]
    fn an_entry_that_adds_a_row_is_a_change_and_one_that_finds_it_is_not() {
        let mut docs = rows(&[(1, 10)]);
        docs.entry(1).or_insert(99);
        docs.entry(2).or_insert(20);
        assert_eq!(docs.finish(), vec![(2, None)]);
        assert_eq!(contents(&docs), vec![(1, 10), (2, 20)]);
    }

    #[test]
    fn rollback_restores_every_touched_key_and_empties_the_log() {
        let mut docs = rows(&[(1, 10), (2, 20), (3, 30)]);
        docs.insert(1, 11);
        docs.insert(4, 40);
        docs.remove(&2);
        *docs.get_mut(&3).unwrap() = 31;
        *docs.entry(5).or_insert(0) = 50;
        docs.rollback();
        assert_eq!(contents(&docs), vec![(1, 10), (2, 20), (3, 30)]);
        assert!(docs.is_clean());
    }

    #[test]
    fn every_row_an_iterator_yields_is_counted_and_point_reads_are_not() {
        let docs = rows(&[(1, 10), (2, 20), (3, 30), (4, 40)]);
        let before = rows_read();
        assert_eq!(docs.iter().count(), 4);
        assert_eq!(rows_read() - before, 4);
        assert_eq!(docs.keys().count() + docs.values().count(), 8);
        assert_eq!(docs.range(2..4).count(), 2);
        assert_eq!((&docs).into_iter().rev().take(1).count(), 1);
        assert_eq!(rows_read() - before, 4 + 8 + 2 + 1);
        let before = rows_read();
        assert_eq!(docs.get(&2), Some(&20));
        assert!(docs.contains_key(&3) && docs[&4] == 40);
        assert_eq!(docs.len(), 4);
        assert_eq!(rows_read(), before);
    }

    #[test]
    fn equality_is_the_documents_and_not_the_log() {
        let mut open = rows(&[(1, 10)]);
        open.insert(1, 11);
        open.insert(1, 10);
        assert_eq!(open, rows(&[(1, 10)]));
    }

    #[test]
    fn the_header_is_logged_once_and_a_genesis_value_is_an_addition() {
        let mut header = Tracked::absent(5u8);
        assert_eq!(header.finish(), Some(None), "genesis adds the header");

        *header.get_mut() = 6;
        *header.get_mut() = 7;
        assert_eq!(header.finish(), Some(Some(5)));
        assert!(header.is_clean());

        *header.get_mut() = 8;
        *header.get_mut() = 7;
        assert_eq!(header.finish(), None, "written back is not a change");

        *header.get_mut() = 9;
        header.rollback();
        assert_eq!(*header, 7);
        assert!(header.is_clean());
    }
}
