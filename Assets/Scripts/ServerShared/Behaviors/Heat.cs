/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), Order(10)]
public class HeatData : BehaviorData
{
    [Inspectable, JsonProperty("heat"), Key(1), RuntimeInspectable]
    public PerformanceStat Heat = new PerformanceStat();
    
    [Inspectable, JsonProperty("perSecond"), Key(2)]
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
    private HeatData _data;

    public Heat(HeatData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Heat(HeatData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        AddHeat(Evaluate(_data.Heat) * (_data.PerSecond ? dt : 1));

        return true;
    }
}