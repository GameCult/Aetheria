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
use std::collections::{BTreeMap, BTreeSet};
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

/// Where a document sits in a partition's read index: a pure function of one
/// document's value, so the index can always be rebuilt from the map and never
/// disagrees with it.
pub(crate) trait Projection<V> {
    type Point: Ord + Clone + std::fmt::Debug;
    /// Appends every point `value` sits at (duplicates allowed).
    fn points(value: &V, out: &mut Vec<Self::Point>);
}

/// The projection of a partition nothing reads by value.
#[derive(Clone, Debug, Default)]
pub(crate) struct NoIndex;

impl<V> Projection<V> for NoIndex {
    type Point = ();
    fn points(_: &V, _: &mut Vec<()>) {}
}

/// A partition: its documents, the log of the open transaction, and the read
/// index (the projection of the documents as of the last `finish`). The index
/// is derived state: it is not digested, not encoded, and ignored by equality.
#[derive(Clone, Debug)]
pub(crate) struct Docs<K: Ord + Clone, V: Clone + PartialEq, P: Projection<V> = NoIndex> {
    map: BTreeMap<K, V>,
    touched: BTreeMap<K, Option<V>>,
    index: BTreeMap<P::Point, BTreeSet<K>>,
}

impl<K: Ord + Clone, V: Clone + PartialEq, P: Projection<V>> Default for Docs<K, V, P> {
    fn default() -> Self {
        Self {
            map: BTreeMap::new(),
            touched: BTreeMap::new(),
            index: BTreeMap::new(),
        }
    }
}

/// Equality is the documents, never the log: two worlds with the same content
/// are equal whatever transaction state they are in.
impl<K: Ord + Clone, V: Clone + PartialEq, P: Projection<V>> PartialEq for Docs<K, V, P> {
    fn eq(&self, other: &Self) -> bool {
        self.map == other.map
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq + Eq, P: Projection<V>> Eq for Docs<K, V, P> {}

impl<'a, K: Ord + Clone, V: Clone + PartialEq, P: Projection<V>> IntoIterator for &'a Docs<K, V, P> {
    type Item = (&'a K, &'a V);
    type IntoIter = Counted<std::collections::btree_map::Iter<'a, K, V>>;
    fn into_iter(self) -> Self::IntoIter {
        self.iter()
    }
}

/// A partition encodes as its map, so the encoded world is what it was before
/// the partitions were tracked.
impl<K: Ord + Clone + Serialize, V: Clone + PartialEq + Serialize, P: Projection<V>> Serialize for Docs<K, V, P> {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.map.serialize(serializer)
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq, P: Projection<V>> std::ops::Index<&K> for Docs<K, V, P> {
    type Output = V;
    fn index(&self, key: &K) -> &V {
        &self.map[key]
    }
}

impl<K: Ord + Clone, V: Clone + PartialEq, P: Projection<V>> Docs<K, V, P> {
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
        let changed: Vec<Changed<K, V>> = std::mem::take(&mut self.touched)
            .into_iter()
            .filter(|(key, old)| self.map.get(key) != old.as_ref())
            .collect();
        for (key, old) in &changed {
            self.fold_index(key, old.as_ref());
        }
        changed
    }

    /// Moves one changed key's entries: its old points out, its current points in.
    fn fold_index(&mut self, key: &K, old: Option<&V>) {
        let mut points = Vec::new();
        if let Some(old) = old {
            P::points(old, &mut points);
            for point in points.drain(..) {
                if let Some(keys) = self.index.get_mut(&point) {
                    keys.remove(key);
                    if keys.is_empty() {
                        self.index.remove(&point);
                    }
                }
            }
        }
        if let Some(new) = self.map.get(key) {
            P::points(new, &mut points);
            for point in points {
                self.index.entry(point).or_default().insert(key.clone());
            }
        }
    }

    /// Every (point, key) inside `range`, in order, true at this moment: the
    /// committed index, corrected by the open log. A logged key's committed
    /// entries are dropped and its current value's points are read from the
    /// map, so a read inside a transaction sees what the transaction wrote and
    /// the index itself never saw it (which is why `rollback` leaves it alone).
    pub(crate) fn indexed<R: std::ops::RangeBounds<P::Point>>(
        &self,
        range: R,
    ) -> Vec<(P::Point, K)> {
        let mut found: Vec<(P::Point, K)> = self
            .index
            .range((range.start_bound(), range.end_bound()))
            .flat_map(|(point, keys)| keys.iter().map(move |key| (point, key)))
            .filter(|(_, key)| !self.touched.contains_key(*key))
            .map(|(point, key)| (point.clone(), key.clone()))
            .collect();
        if !self.touched.is_empty() {
            let mut points = Vec::new();
            for key in self.touched.keys() {
                if let Some(value) = self.map.get(key) {
                    P::points(value, &mut points);
                    for point in points.drain(..) {
                        if range.contains(&point) {
                            found.push((point, key.clone()));
                        }
                    }
                }
            }
            found.sort();
            found.dedup();
        }
        #[cfg(test)]
        count_rows(found.len() as u64);
        found
    }

    /// Whether the index is exactly the projection of the map (test builds).
    #[cfg(test)]
    pub(crate) fn index_is_derived(&self) -> bool {
        let mut rebuilt: BTreeMap<P::Point, BTreeSet<K>> = BTreeMap::new();
        let mut points = Vec::new();
        for (key, value) in &self.map {
            P::points(value, &mut points);
            for point in points.drain(..) {
                rebuilt.entry(point).or_default().insert(key.clone());
            }
        }
        rebuilt == self.index
    }

    /// Makes the index disagree with the map in one place: a stray entry (a
    /// document listed at a point it does not sit at) or a missing one. False
    /// when this partition has no such place to damage (test builds).
    #[cfg(test)]
    pub(crate) fn corrupt_index_for_test(&mut self, stray: bool) -> bool {
        if !stray {
            let Some(point) = self.index.keys().next().cloned() else {
                return false;
            };
            let keys = self.index.get_mut(&point).expect("the point is present");
            let key = keys.iter().next().cloned().expect("a point holds a key");
            keys.remove(&key);
            if keys.is_empty() {
                self.index.remove(&point);
            }
            return true;
        }
        for keys in self.index.values_mut() {
            if let Some(key) = self.map.keys().find(|key| !keys.contains(*key)) {
                keys.insert(key.clone());
                return true;
            }
        }
        false
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

    /// A value sits at its tens digit and its units digit (one point when they
    /// agree), so a document has several points and two documents can share one.
    #[derive(Clone, Debug)]
    struct Digits;

    impl Projection<u8> for Digits {
        type Point = u8;
        fn points(value: &u8, out: &mut Vec<u8>) {
            out.push(value / 10);
            out.push(value % 10);
        }
    }

    type Spread = Docs<u8, u8, Digits>;

    fn spread(held: &[(u8, u8)]) -> Spread {
        let mut docs = Spread::default();
        for (key, value) in held {
            docs.insert(*key, *value);
        }
        docs.finish();
        docs
    }

    /// The projection of the map, computed from scratch: what `indexed(..)` must
    /// return at every moment.
    fn truth(docs: &Spread) -> Vec<(u8, u8)> {
        let mut out = Vec::new();
        for (key, value) in docs.iter() {
            out.push((value / 10, *key));
            out.push((value % 10, *key));
        }
        out.sort();
        out.dedup();
        out
    }

    #[test]
    fn a_committed_index_is_the_projection_and_follows_every_finish() {
        let mut docs = spread(&[(1, 10), (2, 21), (3, 33)]);
        assert!(docs.index_is_derived());
        assert_eq!(docs.indexed(..), truth(&docs));
        assert_eq!(docs.indexed(1..=1), vec![(1, 1), (1, 2)]);

        docs.insert(1, 32);
        docs.remove(&2);
        docs.insert(4, 44);
        docs.finish();
        assert!(docs.index_is_derived(), "old points leave, new points arrive");
        assert_eq!(docs.indexed(..), truth(&docs));
        assert!(docs.indexed(0..=0).is_empty() && docs.indexed(1..=1).is_empty());
    }

    #[test]
    fn a_read_inside_a_transaction_sees_the_log_and_the_index_does_not() {
        let mut docs = spread(&[(1, 10), (2, 21), (3, 33)]);
        docs.insert(1, 55);
        docs.remove(&2);
        docs.insert(7, 12);
        assert_eq!(docs.indexed(..), truth(&docs), "the read is true mid-transaction");
        assert!(!docs.indexed(0..=0).iter().any(|(_, key)| *key == 1), "an overwritten value leaves its old points");
        assert!(docs.indexed(5..=5).contains(&(5, 1)), "a new value is seen at once");
        assert!(docs.indexed(..).iter().all(|(_, key)| *key != 2), "a removed document is gone");
        for point in 0..=9 {
            assert_eq!(
                docs.indexed(point..=point),
                truth(&docs).into_iter().filter(|(p, _)| *p == point).collect::<Vec<_>>()
            );
        }
        assert!(!docs.index_is_derived(), "the open log has not reached the index");

        docs.insert(1, 10);
        docs.insert(7, 12);
        docs.remove(&7);
        assert_eq!(docs.indexed(..), truth(&docs), "a key written back or added and removed adds nothing");
    }

    #[test]
    fn rollback_leaves_the_index_as_it_was_because_it_never_saw_the_transaction() {
        let mut docs = spread(&[(1, 10), (2, 21)]);
        let before = docs.indexed(..);
        docs.insert(1, 99);
        docs.insert(5, 35);
        docs.remove(&2);
        docs.rollback();
        assert!(docs.index_is_derived());
        assert_eq!(docs.indexed(..), before);
        assert_eq!(docs.indexed(..), truth(&docs));
    }

    #[test]
    fn an_index_read_counts_the_entries_it_returns() {
        let docs = spread(&[(1, 10), (2, 21), (3, 33)]);
        let before = rows_read();
        let found = docs.indexed(..);
        assert_eq!(rows_read() - before, found.len() as u64);
        let before = rows_read();
        assert_eq!(docs.indexed(1..=1).len(), 2);
        assert_eq!(rows_read() - before, 2);
    }

    #[test]
    fn a_damaged_index_is_seen_as_not_derived() {
        for stray in [true, false] {
            let mut docs = spread(&[(1, 10), (2, 21), (3, 33)]);
            assert!(docs.corrupt_index_for_test(stray));
            assert!(!docs.index_is_derived(), "stray={stray}");
        }
        let mut empty = Spread::default();
        assert!(!empty.corrupt_index_for_test(true) && !empty.corrupt_index_for_test(false));
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
