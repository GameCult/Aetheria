/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ReflectorData : BehaviorData
{
    [Inspectable, JsonProperty("crossSection"), Key(1), RuntimeInspectable]  
    public PerformanceStat CrossSection = new PerformanceStat();

    // [InspectableAnimationCurve, JsonProperty("visibility"), Key(1)]  
    // public float4[] VisibilityCurve;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Reflector(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Reflector(this, item);
    }
}

public class Reflector : Behavior
{
    private ReflectorData _data;

    public Reflector(ReflectorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public Reflector(ReflectorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_data.CrossSection) * Entity.Zone.GetLight(Entity.Position.xz);
        
        return true;
    }
}