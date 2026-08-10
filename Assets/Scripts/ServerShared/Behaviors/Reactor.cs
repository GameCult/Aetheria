/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ReactorData : BehaviorData
{
    [Inspectable, JsonProperty("charge"), Key(1), RuntimeInspectable]  
    public PerformanceStat Charge = new PerformanceStat();

    [Inspectable, JsonProperty("efficiency"), Key(2), RuntimeInspectable]  
    public PerformanceStat Efficiency = new PerformanceStat();

    [Inspectable, JsonProperty("overload"), Key(3), RuntimeInspectable]  
    public PerformanceStat OverloadEfficiency = new PerformanceStat();

    [Inspectable, JsonProperty("underload"), Key(4), RuntimeInspectable]  
    public PerformanceStat ThrottlingFactor = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Reactor(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Reactor(this, item);
    }
}

public class Reactor : Behavior, IOrderedBehavior, IDisposable
{
    private ReactorData _data;

    public float Draw { get; private set; }
    
    public float CurrentLoadRatio { get; private set; }

    public int Order => 100;

    private List<Capacitor> _capacitors;

    private List<IDisposable> _subscriptions = new List<IDisposable>();

    public Reactor(ReactorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
        FindCapacitors();
    }
    public Reactor(ReactorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        FindCapacitors();
    }

    private void FindCapacitors()
    {
        _capacitors = Entity.GetBehaviors<Capacitor>().ToList();
        _subscriptions.Add(Entity.Equipment.ObserveAdd().Subscribe(onAdd =>
        {
            var capacitor = onAdd.Value.GetBehavior<Capacitor>();
            if (capacitor != null) _capacitors.Add(capacitor);
        }));
        _subscriptions.Add(Entity.Equipment.ObserveRemove().Subscribe(onRemove =>
        {
            var capacitor = onRemove.Value.GetBehavior<Capacitor>();
            if (capacitor != null) _capacitors.Remove(capacitor);
        }));
    }

    public void ConsumeEnergy(float energy)
    {
        Draw += energy;
    }

    public override bool Execute(float dt)
    {
        var charge = Evaluate(_data.Charge) * dt;
        var efficiency = Evaluate(_data.Efficiency);

        // This behavior executes last, so any components drawing power have already done so

        // Subtract the baseline charge from draw
        Draw -= charge;
        
        // Generate heat using baseline efficiency
        var heat = charge / efficiency;

        // We have an energy deficit, have to overload the reactor
        if (Draw > .01f)
        {
            CurrentLoadRatio = (Draw + charge) / max(charge, .01f);
            var overloadEfficiency = Evaluate(_data.OverloadEfficiency);
            
            // Generate heat using overload efficiency, usually much less efficient!
            heat += Draw / overloadEfficiency;
            
            // Overload power will always neutralize the energy deficit
            Draw = 0;
        }

        // We have an energy surplus, try to store energy in our capacitors
        if (Draw < -.01f)
        {
            int nonFullCapacitorCount;
            do
            {
                var chargeToAdd = -Draw;
                nonFullCapacitorCount = _capacitors.Count(c => c.Charge < c.Capacity - .01f);
                foreach (var capacitor in _capacitors)
                {
                    if (capacitor.Charge < capacitor.Capacity - .01f)
                    {
                        var chargeAdded = min(chargeToAdd / nonFullCapacitorCount, capacitor.Capacity - capacitor.Charge);
                        capacitor.AddCharge(chargeAdded);
                        Draw += chargeAdded;
                    }
                }
            } while (nonFullCapacitorCount > 0 && Draw < -.01f);
        }

        // We still have an energy surplus, try to throttle the reactor to reduce heat generation
        if (Draw < -.01f)
        {
            CurrentLoadRatio = (Draw + charge) / max(charge, .01f);
            heat -= Draw / efficiency * (1 - 1 / Evaluate(_data.ThrottlingFactor));
            Draw = 0;
        }
        else
        {
            CurrentLoadRatio = 1;
        }
        
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, max(.25f, 1 - 1 / CurrentLoadRatio));
        
        AddHeat(heat);
        return true;
    }

    public void Dispose()
    {
        foreach(var sub in _subscriptions)
            sub.Dispose();
    }
}