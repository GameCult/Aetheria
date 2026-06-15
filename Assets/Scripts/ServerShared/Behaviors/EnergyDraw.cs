/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
[Inspectable, Order(-20)]
public class EnergyDrawConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat EnergyDraw = new PerformanceStat();

    [Inspectable]
    public bool PerSecond;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new EnergyDraw(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new EnergyDraw(this, item);
    }
}

public class EnergyDraw : Behavior
{
    private readonly PerformanceStat _energyDraw;
    private readonly bool _perSecond;

    public EnergyDraw(EnergyDrawConfig data, EquippedItem item) : base(data, item)
    {
        _energyDraw = data.EnergyDraw;
        _perSecond = data.PerSecond;
    }

    public EnergyDraw(EnergyDrawConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _energyDraw = data.EnergyDraw;
        _perSecond = data.PerSecond;
    }

    public override bool Execute(float dt)
    {
        return Entity.TryConsumeEnergy(Evaluate(_energyDraw) * (_perSecond ? dt : 1));
    }
}
