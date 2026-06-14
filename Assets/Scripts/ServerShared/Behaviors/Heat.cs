/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
[Inspectable, Order(10)]
public class HeatConfig : RuntimeBehaviorConfig
{
    [Inspectable, LegacyPayloadKey(1)]
    public PerformanceStat Heat = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(2)]
    public bool PerSecond;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Heat(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Heat(this, item);
    }
}

public class Heat : Behavior
{
    private HeatConfig _data;

    public Heat(HeatConfig data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Heat(HeatConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        AddHeat(Evaluate(_data.Heat) * (_data.PerSecond ? dt : 1));

        return true;
    }
}