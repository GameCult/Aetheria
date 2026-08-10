/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class VisibilityData : BehaviorData
{
    [Inspectable, JsonProperty("visibility"), Key(1), RuntimeInspectable]  
    public PerformanceStat Visibility = new PerformanceStat();

    [Inspectable, JsonProperty("visibilityDecay"), Key(2)]  
    public PerformanceStat VisibilityDecay = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Visibility(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Visibility(this, item);
    }
}

public class Visibility : Behavior
{
    private VisibilityData _data;

    public Visibility(VisibilityData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Visibility(VisibilityData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_data.Visibility);
        return true;
    }
}