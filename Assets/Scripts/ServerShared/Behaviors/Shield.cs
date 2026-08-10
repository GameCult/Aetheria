/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ShieldData : BehaviorData
{
    [Inspectable, JsonProperty("efficiency"), Key(1), RuntimeInspectable]  
    public PerformanceStat Efficiency = new PerformanceStat();

    [Inspectable, JsonProperty("energy"), Key(2), RuntimeInspectable]  
    public PerformanceStat EnergyUsage = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Shield(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Shield(this, item);
    }
}

public class Shield : Behavior, IProgressBehavior
{
    public float Efficiency { get; private set; }
    public float EnergyUsage { get; private set; }

    private ShieldData _data;

    public Shield(ShieldData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Shield(ShieldData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Efficiency = Evaluate(_data.Efficiency);
        EnergyUsage = Evaluate(_data.EnergyUsage);
        return true;
    }

    public bool CanTakeHit(DamageType type, float damage)
    {
        return Entity.CanConsumeEnergy(damage * EnergyUsage);
    }

    public void TakeHit(DamageType type, float damage)
    {
        Entity.TryConsumeEnergy(damage * EnergyUsage);
        AddHeat(damage / Efficiency);
    }

    public virtual float Progress => Item.ThermalPerformance;
}