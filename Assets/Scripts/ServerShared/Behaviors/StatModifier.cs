/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StatModifierData : BehaviorData
{
    [Inspectable, JsonProperty("stat"), Key(1)]
    public StatReference Stat = new StatReference();

    [Inspectable, JsonProperty("modifier"), Key(2)]
    public PerformanceStat Modifier = new PerformanceStat();

    [Inspectable, JsonProperty("type"), Key(3)]
    public StatModifierType Type;

    // The simple name of a BehaviorData type, as InspectableType writes it
    [InspectableType(typeof(BehaviorData)), JsonProperty("requireBehavior"), Key(4)]
    public string RequireBehavior;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new StatModifier(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect consumable)
    {
        return new StatModifier(this, consumable);
    }
}

public class StatModifier : Behavior, IInitializableBehavior, IDisposable, IAlwaysUpdatedBehavior
{
    // A resolved value is per (item, stat) (docs/stats-and-power-cut.md §1.1), so a modifier that reaches a stat
    // must attach under the owner that specific stat resolves through -- the EquippedItem the field came from --
    // not under the modifying behaviour's own owner. Two items of the same design targeted by one modifier get
    // two separate resolver entries, exactly as two separate Evaluate calls on them already would.
    private readonly struct Target
    {
        public readonly object Owner;
        public readonly PerformanceStat Stat;
        public Target(object owner, PerformanceStat stat) { Owner = owner; Stat = stat; }
    }

    private StatModifierData _data;

    private Target[] _targets;

    private bool _applied;
    private bool _executed;

    public StatModifier(StatModifierData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public StatModifier(StatModifierData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 2 (docs/stats-and-power-cut.md): the (type name, field name) resolution that used to live here, done
    // fresh and silently at every equip, now goes through StatValidation.ResolveStatField -- resolved once,
    // memoized, and refused loudly (at catalog load, or here if a caller built one without going through the
    // catalog) instead of leaving `_stats` null for ApplyModifier to throw an opaque NullReferenceException on.
    //
    // Gate 2 fix (Soul pass over Cut 2): Initialize is the entity's own re-activation hook, called exactly once
    // per (re)activation for every StatModifier on it -- it is the one place that already knows "this behaviour's
    // targets are about to change." Detaching an old attachment, if any, belongs here, not in Update's _executed/
    // _applied latch: that latch only tracks per-tick execution and was never told a re-Initialize had happened,
    // so after deactivate -> unequip the target -> equip a replacement -> activate, _applied stayed true from the
    // stale attachment and Update's `_executed && !_applied` guard never fired again -- the modifier silently
    // stopped reaching anything. Making Initialize own the detach/reattach transition removes the split
    // authority instead of just resetting the flag: whatever this modifier was attached to before is explicitly
    // let go before new targets are computed, so the next Execute+Update reliably reapplies against them.
    public void Initialize()
    {
        if (_applied)
            RemoveModifier();
        _targets = TargetsOf(Entity, _data);
        ValidateNoCycle(Entity, _data, _targets);
        ValidateNoPowerSupplyChain(Entity, _data, _targets, Item);
    }

    private static Target[] TargetsOf(Entity entity, StatModifierData data)
    {
        var (targetType, statField) = StatValidation.ResolveStatField(data.Stat);
        var gear = entity.Equipment
            .Where(g => string.IsNullOrEmpty(data.RequireBehavior) ||
                        entity.ItemManager.GetData(g.EquippableItem).Behaviors.Any(b => b.GetType().Name == data.RequireBehavior));
        return typeof(EquippableItemData).IsAssignableFrom(targetType)
            ? gear.Where(g => entity.ItemManager.GetData(g.EquippableItem).GetType() == targetType)
                .Select(g => new Target(g, statField.GetValue(entity.ItemManager.GetData(g.EquippableItem)) as PerformanceStat))
                .Where(t => t.Stat != null)
                .ToArray()
            : gear.Where(g => entity.ItemManager.GetData(g.EquippableItem).Behaviors.Any(bd => bd.GetType() == targetType))
                .SelectMany(g => entity.ItemManager.GetData(g.EquippableItem).Behaviors
                    .Where(bd => bd.GetType() == targetType)
                    .Select(bd => new Target(g, statField.GetValue(bd) as PerformanceStat)))
                .Where(t => t.Stat != null)
                .ToArray();
    }

    // Shared by ValidateNoCycle and ValidateNoPowerSupplyChain below: every StatModifier currently on this
    // entity, as an edge from the magnitude stat it reads to the stat(s) it writes. Built fresh from
    // Entity.Equipment every time (never a static or catalog-side map), so it costs nothing to discard and cannot
    // leak: by the time Activate() calls any StatModifier's Initialize, every EquippedItem (and so every
    // StatModifier) on the entity already exists (Entity.cs Activate). Both checks see exactly what is actually
    // equipped, not what the catalog merely allows.
    private static Dictionary<PerformanceStat, List<PerformanceStat>> BuildModifierEdges(Entity entity, StatModifierData data, Target[] targets)
    {
        var edges = new Dictionary<PerformanceStat, List<PerformanceStat>>();
        foreach (var behavior in entity.Equipment.SelectMany(e => e.Behaviors).OfType<StatModifier>())
        {
            if (behavior._data.Modifier == null) continue;
            Target[] behaviorTargets;
            try
            {
                behaviorTargets = ReferenceEquals(behavior._data, data) ? targets : TargetsOf(entity, behavior._data);
            }
            catch (InvalidOperationException)
            {
                continue; // another modifier's own unresolved reference is that modifier's Initialize to refuse
            }
            if (!edges.TryGetValue(behavior._data.Modifier, out var list))
                edges[behavior._data.Modifier] = list = new List<PerformanceStat>();
            foreach (var target in behaviorTargets)
                if (target.Stat != null) list.Add(target.Stat);
        }
        return edges;
    }

    // A modifier's magnitude (_data.Modifier) is itself a PerformanceStat, evaluated through the same resolver as
    // everything else it modifies. If that magnitude stat is, directly or through another modifier on this
    // entity, one of the stats this modifier writes, resolving it would depend on a value computed from itself
    // one tick late -- an authoring error, not a feature. Refused at equip, naming the entity and the reference.
    private static void ValidateNoCycle(Entity entity, StatModifierData data, Target[] targets)
    {
        if (data.Modifier == null) return;

        var edges = BuildModifierEdges(entity, data, targets);

        var visited = new HashSet<PerformanceStat>();
        bool ReachesSelf(PerformanceStat node)
        {
            if (!edges.TryGetValue(node, out var next)) return false;
            foreach (var target in next)
            {
                if (ReferenceEquals(target, data.Modifier)) return true;
                if (visited.Add(target) && ReachesSelf(target)) return true;
            }
            return false;
        }
        if (ReachesSelf(data.Modifier))
            throw new InvalidOperationException(
                $"{entity.Name}: stat modifier \"{data.Stat.Target}.{data.Stat.Stat}\" reads a magnitude stat whose " +
                "modifier chain reaches back to itself -- a stat cannot (even transitively) modify its own magnitude");
    }

    // Cut 6 (docs/stats-and-power-cut.md), narrowed by the nominal-request ruling (operator ruling 2026-09-19,
    // ItemData.cs's own PowerRequestFields comment): this is now the ONLY surviving half of "a stat that decides
    // a power request may not depend on power supply, directly or through a modifier chain." The direct half --
    // a request stat's own declared Terms naming PowerSupply -- used to be refused statically
    // (StatValidation.ValidateNoPowerSupplyOnRequest, at catalog load, Upsert and equip), but every
    // PowerRequest/RefreshReserve/RefreshInputCapacitor implementation now reads a PowerRequestFields stat
    // through EquippedItem.EvaluateNominalPower (Entity.cs), which pins that stat's own PowerSupplyFactor to 1
    // regardless of its Terms -- so a direct term can no longer make the request depend on its own answer, and
    // that static check was deleted. What EvaluateNominalPower does NOT pin is ScaleModifier/ConstantModifier --
    // it forwards those to the item's real, non-nominal resolver entries (same as ConditionRatio's own
    // NominalContext), so a modifier chain that reaches a power-tainted magnitude stat still corrupts a nominal
    // read too. That is exactly what this check still catches, and it needs an entity to do it: whether a given
    // modifier's target is "a power request stat" is static (the (BehaviorData type, field) registry in
    // StatValidation), but which items are actually feeding a modifier chain into it is a fact about this
    // concrete ship's loadout. Walks the same magnitude->target edges ValidateNoCycle builds, in the other
    // direction: is this modifier's own magnitude stat "power-tainted" -- does it, or something feeding it
    // through another modifier on this entity, carry a PowerSupply term -- and does it write onto a known
    // request stat. Refused at equip, naming both the modifying item and the item whose request it would
    // corrupt.
    private static void ValidateNoPowerSupplyChain(Entity entity, StatModifierData data, Target[] targets, EquippedItem modifyingItem)
    {
        if (data.Modifier == null) return;
        var edges = BuildModifierEdges(entity, data, targets);
        if (!IsPowerTainted(data.Modifier, edges, new HashSet<PerformanceStat>()))
            return;

        foreach (var target in targets)
        {
            if (!(target.Owner is EquippedItem targetItem)) continue;
            if (!StatValidation.TryGetPowerRequestBehaviorName(targetItem.Data, target.Stat, out var behaviorName))
                continue;
            throw new InvalidOperationException(
                $"{entity.Name}: stat modifier on \"{modifyingItem?.Data.Name ?? "?"}\" would let " +
                $"\"{targetItem.Data.Name}\".{behaviorName}'s power request depend on power supply through a " +
                "modifier chain -- a stat that decides how much power a behaviour asks for may not depend, even " +
                "transitively, on how much it receives");
        }
    }

    // A magnitude stat is power-tainted if its own Terms name PowerSupply directly, or if it is itself the
    // target of some other modifier on this entity whose magnitude is (transitively) power-tainted. `visited`
    // guards the search against a real cycle -- ValidateNoCycle already refuses those outright, but this walk
    // must not stack-overflow while that refusal is still in flight for a different modifier on the same entity.
    private static bool IsPowerTainted(PerformanceStat stat, Dictionary<PerformanceStat, List<PerformanceStat>> edges, HashSet<PerformanceStat> visited)
    {
        if (stat == null || !visited.Add(stat)) return false;
        foreach (var term in stat.Terms)
            if (term.Source == StatSource.PowerSupply)
                return true;
        foreach (var pair in edges)
            if (pair.Value.Contains(stat) && IsPowerTainted(pair.Key, edges, visited))
                return true;
        return false;
    }

    // Cut 2: the modifier value used to be written straight into the catalog stat's own per-entity dictionary
    // (the leak, §0.3). It now attaches to the resolver entry it targets, keyed by (that item, that stat); it is
    // discarded with the entity, never with the catalog.
    private void ApplyModifier()
    {
        _applied = true;
        var value = Evaluate(_data.Modifier);
        foreach (var target in _targets)
            Entity.Resolver.AttachModifier(target.Owner, target.Stat, this, _data.Type, value);
    }

    private void RemoveModifier()
    {
        _applied = false;
        foreach (var target in _targets)
            Entity.Resolver.DetachModifier(target.Owner, target.Stat, this);
    }

    public override bool Execute(float dt)
    {
        _executed = true;
        return true;
    }

    public void Dispose()
    {
        if(_applied)
            RemoveModifier();
    }

    public void Update(float delta)
    {
        if(_executed && !_applied)
            ApplyModifier();
        if(!_executed && _applied)
            RemoveModifier();
        _executed = false;
    }
}

public enum StatModifierType
{
    Constant,
    Multiplier
}

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StatReference
{
    [InspectableType(typeof(BehaviorData)), JsonProperty("behavior"), Key(1)]
    public string Target;

    [Inspectable, JsonProperty("stat"), Key(2)]
    public string Stat;
}
