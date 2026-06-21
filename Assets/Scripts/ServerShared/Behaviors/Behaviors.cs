/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using static CultMath.math;
using cfloat3 = CultMath.float3;
using cfloat4 = CultMath.float4;

public abstract class Behavior
{
    private readonly Dictionary<string, PerformanceStat> _performanceStats;
    public string Kind { get; }
    public int Group { get; }
    public EquippedItem Item { get; }
    private ConsumableItemEffect Consumable { get; }
    protected ItemManager ItemManager { get; }

    protected Entity Entity => Item?.Entity ?? Consumable.Entity;
    public float Temperature => Item?.Temperature ?? Consumable.Entity.MaxTemp;

    public cfloat3 Direction
    {
        get
        {
            if(Item != null)
            {
                var hardpoint = Entity.Hardpoints[Item.Position.x, Item.Position.y];
                if (hardpoint != null && Entity.HardpointTransforms.TryGetValue(hardpoint, out var hardpointTransform))
                {
                    return normalize(hardpointTransform.direction);
                }
                else
                {
                    var itemDirection = AetheriaMath.Rotate(Entity.CultDirection, Item.EquippableItem.Rotation);
                    return new cfloat3(itemDirection.x, 0, itemDirection.y);
                }
            }

            var entityDirection = Entity.CultDirection;
            return new cfloat3(entityDirection.x, 0, entityDirection.y);
        }
    }

    protected Behavior(RuntimeBehaviorDefinition definition, EquippedItem item)
    {
        _performanceStats = new Dictionary<string, PerformanceStat>(StringComparer.Ordinal);
        Kind = definition?.Kind ?? "";
        Group = definition?.Group ?? 0;
        Item = item;
        ItemManager = Item.ItemManager;
    }

    protected Behavior(RuntimeBehaviorDefinition definition, ConsumableItemEffect consumable)
    {
        _performanceStats = new Dictionary<string, PerformanceStat>(StringComparer.Ordinal);
        Kind = definition?.Kind ?? "";
        Group = definition?.Group ?? 0;
        Consumable = consumable;
        ItemManager = consumable.Entity.ItemManager;
    }

    public float Evaluate(PerformanceStat stat) => Item?.Evaluate(stat) ?? Consumable.Evaluate(stat);

    public float Evaluate(PerformanceStat stat, StatConditionMask condition, float value) =>
        Item?.Evaluate(stat, condition, value) ?? Consumable.Evaluate(stat, condition, value);

    public float Evaluate(
        PerformanceStat stat,
        StatConditionMask firstCondition,
        float firstValue,
        StatConditionMask secondCondition,
        float secondValue) =>
        Item?.Evaluate(stat, firstCondition, firstValue, secondCondition, secondValue) ??
        Consumable.Evaluate(stat, firstCondition, firstValue, secondCondition, secondValue);

    public bool TryGetPerformanceStat(string statName, out PerformanceStat stat)
    {
        if (!string.IsNullOrWhiteSpace(statName) &&
            _performanceStats.TryGetValue(statName, out stat))
        {
            return true;
        }

        stat = null;
        return false;
    }

    protected void RegisterPerformanceStat(string statName, PerformanceStat stat)
    {
        if (!string.IsNullOrWhiteSpace(statName) && stat != null)
        {
            _performanceStats[statName] = stat;
            Item?.RegisterPerformanceStat(Kind, statName, stat);
        }
    }

    protected void AddHeat(float heat) => Item?.AddHeat(heat); // TODO: Heat for Consumables

    protected void CauseDamage(float damage)
    {
        if (Item != null)
        {
            Item.EquippableItem.Durability -= damage;
            Entity.ItemDamage.OnNext((Item, damage));
        }
        else
        {
            Consumable.Entity.Hull.Durability -= damage;
            Consumable.Entity.HullDamage.OnNext(damage);
        }
    }

    protected void CauseWearDamage(float multiplier)
    {
        if (Item != null)
        {
            CauseDamage(Item.Wear * multiplier);
        }
    }

    public virtual bool Execute(float dt)
    {
        return true;
    }
}

public interface IActivatedBehavior
{
    void Activate();
    void Deactivate();
}

public interface IAnalogBehavior
{
    float Axis { get; set; }
}

public interface IEventBehavior
{
    void ResetEvents();
}

public interface IInitializableBehavior
{
    void Initialize();
}

public interface IInteractiveBehavior
{
    bool Exposed { get; }
}

public interface IAlwaysUpdatedBehavior
{
    void Update(float delta);
}

public interface IProgressBehavior
{
    float Progress { get; }
}

public interface IOrderedBehavior
{
    int Order { get; }
}

public interface IPopulationAssignment
{
    int AssignedPopulation { get; set; }
}

public sealed class RuntimeBehaviorDefinition
{
    private readonly IReadOnlyDictionary<int, AetheriaRuntimeBehaviorValue> _fields;

    public string Kind { get; }
    public int Group { get; }

    public RuntimeBehaviorDefinition(AetheriaRuntimeBehaviorPayload payload)
    {
        Kind = payload?.Kind ?? "";
        Group = payload?.Group ?? 0;

        var fields = new Dictionary<int, AetheriaRuntimeBehaviorValue>();
        if (payload != null)
        {
            foreach (var field in payload.Fields)
            {
                if (!fields.ContainsKey(field.Key))
                {
                    fields.Add(field.Key, field.Value);
                }
            }
        }

        _fields = fields;
    }

    public bool Bool(int key, bool fallback = false)
    {
        return _fields.TryGetValue(key, out var value) ? value.BoolValue : fallback;
    }

    public float Float(int key, float fallback = 0)
    {
        return _fields.TryGetValue(key, out var value) ? (float)value.NumberValue : fallback;
    }

    public int Int(int key, int fallback = 0)
    {
        return _fields.TryGetValue(key, out var value) ? checked((int)value.NumberValue) : fallback;
    }

    public uint UInt(int key, uint fallback = 0)
    {
        return _fields.TryGetValue(key, out var value) ? checked((uint)value.NumberValue) : fallback;
    }

    public cfloat3 Float3(int key, cfloat3 fallback = default)
    {
        return _fields.TryGetValue(key, out var value) ? ToFloat3(value) : fallback;
    }

    public string String(int key, string fallback = "")
    {
        return _fields.TryGetValue(key, out var value) ? value.StringValue ?? "" : fallback;
    }

    public string ItemKey(int key, string fallback = "")
    {
        return _fields.TryGetValue(key, out var value) &&
               !string.IsNullOrWhiteSpace(value.ItemKeyValue)
            ? value.ItemKeyValue
            : fallback;
    }

    public PerformanceStat PerformanceStat(int key, PerformanceStat fallback)
    {
        return _fields.TryGetValue(key, out var value)
            ? AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(value)
            : fallback;
    }

    public BezierCurve BezierCurve(int key, BezierCurve fallback)
    {
        if (!_fields.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return global::BezierCurve.FromKeys(value.Children.Count > 0
            ? value.Children[0].Children.Select(ToBezierKey)
            : Enumerable.Empty<(float, float, float, float)>());
    }

    public cfloat4[] Float4Array(int key, cfloat4[] fallback)
    {
        return _fields.TryGetValue(key, out var value)
            ? value.Children.Select(ToFloat4).ToArray()
            : fallback;
    }

    public StatReference StatReference(int key, StatReference fallback)
    {
        if (!_fields.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return AetheriaRuntimeBehaviorValueReader.ReadStatReference(value);
    }

    public T Enum<T>(int key, T fallback) where T : struct
    {
        return _fields.TryGetValue(key, out var value)
            ? AetheriaRuntimeBehaviorValueReader.ReadEnum(value, fallback)
            : fallback;
    }

    private static cfloat4 ToFloat4(AetheriaRuntimeBehaviorValue value)
    {
        return new cfloat4(ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2), ChildFloat(value, 3));
    }

    private static (float, float, float, float) ToBezierKey(AetheriaRuntimeBehaviorValue value)
    {
        return (ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2), ChildFloat(value, 3));
    }

    private static cfloat3 ToFloat3(AetheriaRuntimeBehaviorValue value)
    {
        return new cfloat3(ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2));
    }

    private static float ChildFloat(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value.Children.Count > index ? (float)value.Children[index].NumberValue : 0;
    }

    private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
    }

    private static AetheriaRuntimeBehaviorValue ChildValue(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index ? value.Children[index] : null;
    }
}
