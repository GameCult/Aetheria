/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, Order(1000)]
public class WearConfig : RuntimeBehaviorConfig
{
    [InspectableTemperature]
    public bool PerSecond = true;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Wear(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Wear(this, item);
    }
}

public class Wear : Behavior
{
    private readonly bool _perSecond;

    public Wear(WearConfig data, EquippedItem item) : base(data, item)
    {
        _perSecond = data.PerSecond;
    }
    public Wear(WearConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _perSecond = data.PerSecond;
    }

    public override bool Execute(float dt)
    {
        CauseWearDamage(_perSecond ? dt : 1);
        return true;
    }
}
