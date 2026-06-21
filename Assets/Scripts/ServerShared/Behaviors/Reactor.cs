/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using static CultMath.math;

public class Reactor : Behavior, IOrderedBehavior, IDisposable
{
    private readonly PerformanceStat _charge;
    private readonly PerformanceStat _efficiency;
    private readonly PerformanceStat _overloadEfficiency;
    private readonly PerformanceStat _throttlingFactor;

    public float Draw { get; private set; }

    public float CurrentLoadRatio { get; private set; }

    public int Order => 100;

    private List<Capacitor> _capacitors;

    private List<IDisposable> _subscriptions = new List<IDisposable>();

    public Reactor(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _charge = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        _overloadEfficiency = definition.PerformanceStat(3, new PerformanceStat());
        _throttlingFactor = definition.PerformanceStat(4, new PerformanceStat());
        RegisterPerformanceStat("Charge", _charge);
        RegisterPerformanceStat("Efficiency", _efficiency);
        RegisterPerformanceStat("OverloadEfficiency", _overloadEfficiency);
        RegisterPerformanceStat("ThrottlingFactor", _throttlingFactor);
        FindCapacitors();
    }

    public Reactor(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _charge = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        _overloadEfficiency = definition.PerformanceStat(3, new PerformanceStat());
        _throttlingFactor = definition.PerformanceStat(4, new PerformanceStat());
        RegisterPerformanceStat("Charge", _charge);
        RegisterPerformanceStat("Efficiency", _efficiency);
        RegisterPerformanceStat("OverloadEfficiency", _overloadEfficiency);
        RegisterPerformanceStat("ThrottlingFactor", _throttlingFactor);
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
        var charge = Evaluate(_charge) * dt;
        var efficiency = Evaluate(_efficiency);

        // This behavior executes last, so any components drawing power have already done so

        // Subtract the baseline charge from draw
        Draw -= charge;

        // Generate heat using baseline efficiency
        var heat = charge / efficiency;

        // We have an energy deficit, have to overload the reactor
        if (Draw > .01f)
        {
            CurrentLoadRatio = (Draw + charge) / max(charge, .01f);
            var overloadEfficiency = Evaluate(_overloadEfficiency);

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
            heat -= Draw / efficiency * (1 - 1 / Evaluate(_throttlingFactor));
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

    public void RestoreRuntimeState(float draw, float currentLoadRatio)
    {
        Draw = draw;
        CurrentLoadRatio = currentLoadRatio;
    }
}
