/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public float Evaluate(PerformanceStat stat) => Item?.Evaluate(stat) ?? Consumable.Evaluate(stat);

    public bool TryGetPerformanceStat(string statName, out PerformanceStat stat)
    {
        return !string.IsNullOrWhiteSpace(statName) &&
               _performanceStats.TryGetValue(statName, out stat);
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
