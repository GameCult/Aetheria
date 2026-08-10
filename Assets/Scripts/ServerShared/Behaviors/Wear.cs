/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), Order(1000), RuntimeInspectable]
public class WearData : BehaviorData
{
    [InspectableTemperature, JsonProperty("perSecond"), Key(1)]
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
    private WearData _data;

    public Wear(WearData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Wear(WearData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        CauseWearDamage(_data.PerSecond ? dt : 1);
        return true;
    }
}