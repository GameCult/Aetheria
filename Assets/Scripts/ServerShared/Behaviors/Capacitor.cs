/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class Capacitor : Behavior
{
    private readonly PerformanceStat _capacity;
    private readonly PerformanceStat _efficiency;

    public float Charge { get; private set; }
    public float Capacity { get; private set; }
    public float Efficiency { get; private set; } = 1;

    public void AddCharge(float charge)
    {
        Charge = clamp(Charge + charge, 0, Capacity);
        AddHeat(abs(charge) * (1-Efficiency));
    }

    public Capacitor(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _capacity = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        RegisterPerformanceStat(nameof(Capacity), _capacity);
        RegisterPerformanceStat(nameof(Efficiency), _efficiency);
    }

    public Capacitor(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _capacity = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        RegisterPerformanceStat(nameof(Capacity), _capacity);
        RegisterPerformanceStat(nameof(Efficiency), _efficiency);
    }

    public override bool Execute(float dt)
    {
        Capacity = Evaluate(_capacity);
        Efficiency = Evaluate(_efficiency);
        return true;
    }

    public void RestoreRuntimeState(float charge, float capacity, float efficiency)
    {
        Charge = clamp(charge, 0, capacity);
        Capacity = capacity;
        Efficiency = efficiency;
    }
}
