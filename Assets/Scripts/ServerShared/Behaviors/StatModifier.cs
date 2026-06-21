/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;

public class StatModifier : Behavior, IInitializableBehavior, IDisposable, IAlwaysUpdatedBehavior
{
    private readonly string _targetBehaviorKind;
    private readonly string _targetStatName;
    private readonly PerformanceStat _modifier;
    private readonly StatModifierType _type;
    private readonly string _requiredBehaviorKind;

    private StatModifierTarget[] _targets;

    private bool _applied;
    private bool _executed;

    public bool Applied => _applied;
    public bool Executed => _executed;
    public int TargetStatCount => _targets?.Length ?? 0;

    public StatModifier(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        var stat = definition.StatReference(1, new StatReference());
        _targetBehaviorKind = stat.Target;
        _targetStatName = stat.Stat;
        _modifier = definition.PerformanceStat(2, new PerformanceStat());
        _type = definition.Enum(3, default(StatModifierType));
        _requiredBehaviorKind = definition.String(4);
        RegisterPerformanceStat("Modifier", _modifier);
    }

    public StatModifier(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        var stat = definition.StatReference(1, new StatReference());
        _targetBehaviorKind = stat.Target;
        _targetStatName = stat.Stat;
        _modifier = definition.PerformanceStat(2, new PerformanceStat());
        _type = definition.Enum(3, default(StatModifierType));
        _requiredBehaviorKind = definition.String(4);
        RegisterPerformanceStat("Modifier", _modifier);
    }

    public void Initialize()
    {
        var targets = new List<StatModifierTarget>();
        foreach (var gear in Entity.Equipment.Where(HasRequiredBehavior))
        foreach (var behavior in gear.Behaviors ?? Array.Empty<Behavior>())
        {
            if (!BehaviorKindMatches(behavior.Kind, _targetBehaviorKind))
                continue;

            var stat = FindTargetStat(behavior);
            if (stat != null)
                targets.Add(new StatModifierTarget(gear, stat));
        }

        _targets = targets.ToArray();
    }

    private PerformanceStat FindTargetStat(Behavior behavior)
    {
        return behavior.TryGetPerformanceStat(_targetStatName, out var stat) ? stat : null;
    }

    private static bool BehaviorKindMatches(string runtimeKind, string expectedKind)
    {
        return AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(
            runtimeKind,
            NormalizeExpectedBehaviorKind(expectedKind));
    }

    private static string NormalizeExpectedBehaviorKind(string expectedKind)
    {
        if (string.IsNullOrWhiteSpace(expectedKind))
        {
            return "";
        }

        const string legacyDataSuffix = "Data";
        return expectedKind.EndsWith(legacyDataSuffix, StringComparison.Ordinal)
            ? expectedKind.Substring(0, expectedKind.Length - legacyDataSuffix.Length)
            : expectedKind;
    }

    private bool HasRequiredBehavior(EquippedItem gear)
    {
        return string.IsNullOrWhiteSpace(_requiredBehaviorKind) ||
               (gear?.Behaviors ?? Array.Empty<Behavior>())
                   .Any(behavior => BehaviorKindMatches(behavior.Kind, _requiredBehaviorKind));
    }

    private void ApplyModifier()
    {
        _applied = true;
        RefreshModifier();
    }

    private void RemoveModifier()
    {
        _applied = false;
        foreach (var target in _targets ?? Array.Empty<StatModifierTarget>())
            target.Item.RemoveStatModifier(target.Stat, this, _type);
    }

    private void RefreshModifier()
    {
        var value = Evaluate(_modifier);
        foreach (var target in _targets ?? Array.Empty<StatModifierTarget>())
            target.Item.SetStatModifier(target.Stat, this, _type, value);
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
        else if(_executed && _applied)
            RefreshModifier();
        if(!_executed && _applied)
            RemoveModifier();
        _executed = false;
    }

    public void RestoreRuntimeState(bool applied, bool executed)
    {
        if (_targets == null)
            Initialize();
        if (applied && !_applied)
            ApplyModifier();
        if (applied && _applied)
            RefreshModifier();
        if (!applied && _applied)
            RemoveModifier();
        _executed = executed;
    }

    private readonly struct StatModifierTarget
    {
        public StatModifierTarget(EquippedItem item, PerformanceStat stat)
        {
            Item = item;
            Stat = stat;
        }

        public EquippedItem Item { get; }
        public PerformanceStat Stat { get; }
    }
}

public enum StatModifierType
{
    Constant,
    Multiplier
}

public class StatReference
{
    public string Target;

    public string Stat;
}
