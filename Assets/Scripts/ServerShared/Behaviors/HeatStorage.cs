/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class HeatStorageConfig : RuntimeBehaviorConfig
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new HeatStorage(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new HeatStorage(this, item);
    }
}

public class HeatStorage : Behavior
{
    private HeatStorageConfig _data;

    public HeatStorage(HeatStorageConfig data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public HeatStorage(HeatStorageConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }
}