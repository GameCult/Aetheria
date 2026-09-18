/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Cut 2 (docs/stats-and-power-cut.md): one resolver per Entity, owning every resolved (item, stat) value for
// that entity. PerformanceStat -- the catalog object -- used to hold two Dictionary<Entity, ...> fields and grew
// one entry per entity that ever evaluated it, for the life of the process (§0.3). A stat is shared; a resolved
// value is not (§0b) -- that sentence is this type's whole reason to exist. It is reachable only from the Entity
// that owns it, so it dies when the entity does, not when the catalog does.
//
// "Owner" below is whatever called Resolve/AttachModifier for a stat -- an EquippedItem or a ConsumableItemEffect,
// compared by reference, never by value. Two ships equipping the same design share the PerformanceStat instance
// but never share an owner, so they never share a cached value or a modifier.
public class StatResolver
{
    private readonly struct Entry : IEquatable<Entry>
    {
        public readonly object Owner;
        public readonly PerformanceStat Stat;

        public Entry(object owner, PerformanceStat stat)
        {
            Owner = owner;
            Stat = stat;
        }

        public bool Equals(Entry other) => ReferenceEquals(Owner, other.Owner) && ReferenceEquals(Stat, other.Stat);
        public override bool Equals(object obj) => obj is Entry other && Equals(other);

        public override int GetHashCode() => unchecked(
            RuntimeHelpers.GetHashCode(Owner) * 397 ^ RuntimeHelpers.GetHashCode(Stat));
    }

    // A resolved value is valid only as long as every source it read is still at the generation it read.
    // Generations are per (owner, source): a term's *source kind*, not the specific stat, so invalidating "this
    // owner's heat changed" invalidates every stat of that owner's that declared a Heat term, and no others.
    private class CachedValue
    {
        public float Value;
        public Dictionary<StatSource, int> Generations;
    }

    private class ModifierSet
    {
        public readonly Dictionary<object, float> Scale = new Dictionary<object, float>();
        public readonly Dictionary<object, float> Constant = new Dictionary<object, float>();
    }

    private readonly Dictionary<Entry, CachedValue> _cache = new Dictionary<Entry, CachedValue>();
    private readonly Dictionary<Entry, ModifierSet> _modifiers = new Dictionary<Entry, ModifierSet>();
    private readonly Dictionary<object, Dictionary<StatSource, int>> _generations =
        new Dictionary<object, Dictionary<StatSource, int>>();

    private int GenerationOf(object owner, StatSource source)
    {
        if (_generations.TryGetValue(owner, out var perSource) && perSource.TryGetValue(source, out var generation))
            return generation;
        return 0;
    }

    // Called by an owner when one of its own sources moves: EquippedItem.UpdatePerformance every tick for Heat
    // and Durability, ConsumableItemEffect.Update every tick for ConsumableProgress. Invalidates every cached
    // value of this owner's that declared a term reading this source -- and only those.
    public void InvalidateSource(object owner, StatSource source)
    {
        if (!_generations.TryGetValue(owner, out var perSource))
            _generations[owner] = perSource = new Dictionary<StatSource, int>();
        perSource[source] = GenerationOf(owner, source) + 1;
    }

    // The only place a resolved value is computed. Returns the cached value when every source the stat's Terms
    // declared is still at the generation it was computed against; otherwise evaluates once, against the live
    // context, and caches the new value against the current generations.
    public float Resolve(object owner, PerformanceStat stat, IStatContext context)
    {
        var entry = new Entry(owner, stat);
        if (_cache.TryGetValue(entry, out var cached) && IsCurrent(owner, cached))
            return cached.Value;

        var value = stat.Evaluate(context);
        if (float.IsNaN(value)) value = stat.Min;

        var generations = new Dictionary<StatSource, int>();
        foreach (var term in stat.Terms)
            if (!generations.ContainsKey(term.Source))
                generations[term.Source] = GenerationOf(owner, term.Source);
        _cache[entry] = new CachedValue { Value = value, Generations = generations };
        return value;
    }

    private bool IsCurrent(object owner, CachedValue cached)
    {
        foreach (var pair in cached.Generations)
            if (GenerationOf(owner, pair.Key) != pair.Value)
                return false;
        return true;
    }

    public float ScaleModifier(object owner, PerformanceStat stat)
    {
        if (!_modifiers.TryGetValue(new Entry(owner, stat), out var set)) return 1f;
        var result = 1f;
        foreach (var value in set.Scale.Values) result *= value;
        return result;
    }

    public float ConstantModifier(object owner, PerformanceStat stat)
    {
        if (!_modifiers.TryGetValue(new Entry(owner, stat), out var set)) return 0f;
        var result = 0f;
        foreach (var value in set.Constant.Values) result += value;
        return result;
    }

    // Attached by StatModifier when its behaviour executes and detached when it stops (StatModifier.cs); the
    // modifier's own instance is the key both ways, exactly as it was on the catalog object before this cut --
    // only the dictionary this now lives in has changed owner. Invalidates the entry directly: a modifier is not
    // one of the stat's declared Terms, so the source-generation scheme above does not see it move.
    public void AttachModifier(object owner, PerformanceStat stat, object modifierKey, StatModifierType type, float value)
    {
        var entry = new Entry(owner, stat);
        if (!_modifiers.TryGetValue(entry, out var set))
            _modifiers[entry] = set = new ModifierSet();
        (type == StatModifierType.Constant ? set.Constant : set.Scale)[modifierKey] = value;
        _cache.Remove(entry);
    }

    public void DetachModifier(object owner, PerformanceStat stat, object modifierKey)
    {
        var entry = new Entry(owner, stat);
        if (_modifiers.TryGetValue(entry, out var set))
        {
            set.Scale.Remove(modifierKey);
            set.Constant.Remove(modifierKey);
        }
        _cache.Remove(entry);
    }
}
