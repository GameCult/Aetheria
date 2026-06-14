/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using GameCult.Aetheria.State.Unity;

[Inspectable]
public class StatModifierConfig : RuntimeBehaviorConfig
{
    [Inspectable, LegacyPayloadKey(1)]
    public StatReference Stat = new StatReference();

    [Inspectable, LegacyPayloadKey(2)]
    public PerformanceStat Modifier = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(3)]
    public StatModifierType Type;

    [InspectableType(typeof(RuntimeBehaviorConfig)), LegacyPayloadKey(4)]
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

[Order(-4)]
public class StatModifier : Behavior, IInitializableBehavior, IDisposable, IAlwaysUpdatedBehavior
{
    private StatModifierConfig _data;

    private PerformanceStat[] _stats;

    private bool _applied;
    private bool _executed;

    public bool Applied => _applied;
    public bool Executed => _executed;
    public int TargetStatCount => _stats?.Length ?? 0;

    public StatModifier(StatModifierConfig data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public StatModifier(StatModifierConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public void Initialize()
    {
        _stats = Entity.Equipment
            .Where(HasRequiredBehavior)
            .SelectMany(gear => gear.Behaviors ?? Array.Empty<Behavior>())
            .Where(behavior => BehaviorKindMatches(behavior.Kind, _data.Stat.Target))
            .Select(FindTargetStat)
            .Where(stat => stat != null)
            .ToArray();
    }

    private PerformanceStat FindTargetStat(Behavior behavior)
    {
        var data = behavior.Config;
        var statField = data
            .GetType()
            .GetFields()
            .Where(f => f.FieldType == typeof(PerformanceStat))
            .FirstOrDefault(f => f.Name == _data.Stat.Stat);

        return statField?.GetValue(data) as PerformanceStat;
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
        return string.IsNullOrWhiteSpace(_data.RequireBehavior) ||
               (gear?.Behaviors ?? Array.Empty<Behavior>())
                   .Any(behavior => BehaviorKindMatches(behavior.Kind, _data.RequireBehavior));
    }

    private void ApplyModifier()
    {
        _applied = true;
        foreach (var stat in _stats)
            (_data.Type == StatModifierType.Constant
                ? stat.GetConstantModifiers(Entity)
                : stat.GetScaleModifiers(Entity))[this] = Evaluate(_data.Modifier);
    }

    private void RemoveModifier()
    {
        _applied = false;
        foreach (var stat in _stats)
            (_data.Type == StatModifierType.Constant ? stat.GetConstantModifiers(Entity) : stat.GetScaleModifiers(Entity)).Remove(this);
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

[Inspectable]
public class StatReference
{
    [InspectableType(typeof(RuntimeBehaviorConfig)), LegacyPayloadKey(1)]
    public string Target;

    [Inspectable, LegacyPayloadKey(2)]
    public string Stat;
}
