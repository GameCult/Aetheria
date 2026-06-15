/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class CapacitorConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Capacity = new PerformanceStat();

    [Inspectable]
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

    public Capacitor(CapacitorConfig data, EquippedItem item) : base(data, item)
    {
        _capacity = data.Capacity;
        _efficiency = data.Efficiency;
    }

    public Capacitor(CapacitorConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _capacity = data.Capacity;
        _efficiency = data.Efficiency;
    }

    public override bool Execute(float dt)
    {
        Capacity = Evaluate(_capacity);
        Efficiency = Evaluate(_efficiency);
        return true;
    }
}
