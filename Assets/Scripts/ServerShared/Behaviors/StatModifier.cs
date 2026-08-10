/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
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

    [InspectableType(typeof(BehaviorData)), JsonProperty("requireBehavior"), Key(4)]
    public Type RequireBehavior;
    
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
    private StatModifierData _data;

    private PerformanceStat[] _stats;
    
    private static Type[] _statObjects;

    private bool _applied;
    private bool _executed;

    private static Type[] StatObjects => _statObjects = _statObjects ?? typeof(BehaviorData).GetAllChildClasses()
        .Concat(typeof(EquippableItemData).GetAllChildClasses()).ToArray();

    public StatModifier(StatModifierData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public StatModifier(StatModifierData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public void Initialize()
    {
        var targetType = StatObjects.FirstOrDefault(so => so.Name == _data.Stat.Target);
        if (targetType != null)
        {
            var statField = targetType.GetFields()
                .Where(f => f.FieldType == typeof(PerformanceStat)).FirstOrDefault(f => f.Name == _data.Stat.Stat);
            if (statField != null)
            {
                if (typeof(EquippableItemData).IsAssignableFrom(targetType))
                    _stats = Entity.Equipment
                        .Select(hp => hp.EquippableItem)
                        .Where(gear => _data.RequireBehavior == null || ItemManager.GetData(gear).Behaviors.Any(behavior => behavior.GetType() == _data.RequireBehavior))
                        .Where(gear => ItemManager.GetData(gear).GetType() == targetType)
                        .Select(gear => statField.GetValue(ItemManager.GetData(gear)) as PerformanceStat)
                        .ToArray();
                else
                    _stats = Entity.Equipment
                        .Select(hp => hp.EquippableItem)
                        .Where(gear => _data.RequireBehavior == null || ItemManager.GetData(gear).Behaviors.Any(behavior => behavior.GetType() == _data.RequireBehavior))
                        .SelectMany(gear => ItemManager.GetData(gear).Behaviors)
                        .Where(behaviorData => behaviorData.GetType() == targetType)
                        .Select(behaviorData => statField.GetValue(behaviorData) as PerformanceStat)
                        .ToArray();
            }
        }
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

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StatReference
{
    [InspectableType(typeof(BehaviorData)), JsonProperty("behavior"), Key(1)]
    public string Target;
    
    [Inspectable, JsonProperty("stat"), Key(2)]
    public string Stat;
}