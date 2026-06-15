/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;

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

    public float3 Direction
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
                    var itemDirection = Entity.Direction.Rotate(Item.EquippableItem.Rotation);
                    return float3(itemDirection.x, 0, itemDirection.y);
                }
            }

            return float3(Entity.Direction.x, 0, Entity.Direction.y);
        }
    }

    protected Behavior(RuntimeBehaviorConfig config, EquippedItem item)
    {
        _performanceStats = CapturePerformanceStats(config);
        Kind = config?.Kind ?? "";
        Group = config?.Group ?? 0;
        Item = item;
        ItemManager = Item.ItemManager;
    }

    protected Behavior(RuntimeBehaviorConfig config, ConsumableItemEffect consumable)
    {
        _performanceStats = CapturePerformanceStats(config);
        Kind = config?.Kind ?? "";
        Group = config?.Group ?? 0;
        Consumable = consumable;
        ItemManager = consumable.Entity.ItemManager;
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
        }
    }

    private static Dictionary<string, PerformanceStat> CapturePerformanceStats(RuntimeBehaviorConfig config)
    {
        return config == null
            ? new Dictionary<string, PerformanceStat>(StringComparer.Ordinal)
            : config
                .GetType()
                .GetFields()
                .Where(field => field.FieldType == typeof(PerformanceStat))
                .Select(field => (field.Name, Stat: field.GetValue(config) as PerformanceStat))
                .Where(entry => entry.Stat != null)
                .ToDictionary(entry => entry.Name, entry => entry.Stat, StringComparer.Ordinal);
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
        return _fields.TryGetValue(key, out var value) ? ToPerformanceStat(value) : fallback;
    }

    public BezierCurve BezierCurve(int key, BezierCurve fallback)
    {
        if (!_fields.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return new BezierCurve
        {
            Keys = value.Children.Count > 0
                ? value.Children[0].Children.Select(ToFloat4).ToArray()
                : Array.Empty<float4>()
        };
    }

    public StatReference StatReference(int key, StatReference fallback)
    {
        if (!_fields.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return new StatReference
        {
            Target = ChildString(value, 1),
            Stat = ChildString(value, 2)
        };
    }

    public T Enum<T>(int key, T fallback) where T : struct
    {
        if (!_fields.TryGetValue(key, out var value))
        {
            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(value.StringValue) &&
            System.Enum.TryParse(value.StringValue, true, out T parsed))
        {
            return parsed;
        }

        return (T)System.Enum.ToObject(typeof(T), checked((int)value.NumberValue));
    }

    private static PerformanceStat ToPerformanceStat(AetheriaRuntimeBehaviorValue value)
    {
        return new PerformanceStat
        {
            Min = ChildFloat(value, 0),
            Max = ChildFloat(value, 1),
            HeatExponentMultiplier = ChildFloat(value, 2),
            DurabilityExponentMultiplier = ChildFloat(value, 3),
            QualityExponent = ChildFloat(value, 4)
        };
    }

    private static float4 ToFloat4(AetheriaRuntimeBehaviorValue value)
    {
        return new float4(ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2), ChildFloat(value, 3));
    }

    private static float ChildFloat(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value.Children.Count > index ? (float)value.Children[index].NumberValue : 0;
    }

    private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
    }
}

[Inspectable]
public abstract class RuntimeBehaviorConfig
{
    public string Kind { get; set; } = "";

    [Inspectable]
    public int Group;

    public abstract Behavior CreateInstance(EquippedItem item);
    public abstract Behavior CreateInstance(ConsumableItemEffect consumable);

    public override string ToString()
    {
        return base.ToString().FormatTypeName();
    }
}
