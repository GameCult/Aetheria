/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class VelocityConversionData : BehaviorData
{
    [Inspectable, JsonProperty("lambda"), Key(1), RuntimeInspectable]  
    public PerformanceStat Lambda = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new VelocityConversion(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new VelocityConversion(this, item);
    }
}

public class VelocityConversion : Behavior
{
    private VelocityConversionData _data;

    public VelocityConversion(VelocityConversionData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public VelocityConversion(VelocityConversionData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Entity.Velocity = AetheriaMath.Damp(Entity.Velocity, Entity.Direction * length(Entity.Velocity), Evaluate(_data.Lambda), dt);
        return true;
    }
}