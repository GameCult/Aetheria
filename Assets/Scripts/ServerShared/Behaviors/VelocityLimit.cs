/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class VelocityLimitData : BehaviorData
{
    [Inspectable, JsonProperty("topSpeed"), Key(1), RuntimeInspectable]  
    public PerformanceStat TopSpeed = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new VelocityLimit(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new VelocityLimit(this, item);
    }
}

[Order(100)]
public class VelocityLimit : Behavior
{
    public float Limit { get; private set; }

    private VelocityLimitData _data;

    public VelocityLimit(VelocityLimitData data, EquippedItem item) : base(data, item)
    {
        _data = data;
        Limit = Evaluate(_data.TopSpeed);
    }

    public VelocityLimit(VelocityLimitData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        Limit = Evaluate(_data.TopSpeed);
    }

    public override bool Execute(float dt)
    {
        Limit = Evaluate(_data.TopSpeed);
        if (length(Entity.Velocity) > Limit)
            Entity.Velocity = normalize(Entity.Velocity) * Limit;
        return true;
    }
}