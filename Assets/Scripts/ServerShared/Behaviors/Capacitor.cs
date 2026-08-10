/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class CapacitorData : BehaviorData
{
    [Inspectable, JsonProperty("capacity"), Key(1), RuntimeInspectable]  
    public PerformanceStat Capacity = new PerformanceStat();
    
    [Inspectable, JsonProperty("efficiency"), Key(2), RuntimeInspectable]  
    public PerformanceStat Efficiency = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Capacitor(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Capacitor(this, item);
    }
}

public class Capacitor : Behavior
{
    private CapacitorData _data;

    public float Charge { get; private set; }
    public float Capacity { get; private set; }
    public float Efficiency { get; private set; } = 1;

    public void AddCharge(float charge)
    {
        Charge = clamp(Charge + charge, 0, Capacity);
        AddHeat(abs(charge) * (1-Efficiency));
    }

    public Capacitor(CapacitorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public Capacitor(CapacitorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Capacity = Evaluate(_data.Capacity);
        Efficiency = Evaluate(_data.Efficiency);
        return true;
    }
}