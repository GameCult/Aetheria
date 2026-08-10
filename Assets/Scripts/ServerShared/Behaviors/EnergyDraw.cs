/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), Order(-20), RuntimeInspectable]
public class EnergyDrawData : BehaviorData
{
    [Inspectable, JsonProperty("draw"), Key(1), RuntimeInspectable]
    public PerformanceStat EnergyDraw = new PerformanceStat();
    
    [Inspectable, JsonProperty("perSecond"), Key(2)]
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
    private EnergyDrawData _data;

    public EnergyDraw(EnergyDrawData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public EnergyDraw(EnergyDrawData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        return Entity.TryConsumeEnergy(Evaluate(_data.EnergyDraw) * (_data.PerSecond ? dt : 1));
    }
}