/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), EntityTypeRestriction(HullType.Ship), RuntimeInspectable]
public class ThrusterData : BehaviorData
{
    [Inspectable, JsonProperty("thrust"), Key(1), RuntimeInspectable]  
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, JsonProperty("visibility"), Key(2), RuntimeInspectable]  
    public PerformanceStat Visibility = new PerformanceStat();

    [Inspectable, JsonProperty("heat"), Key(3), RuntimeInspectable]  
    public PerformanceStat Heat = new PerformanceStat();

    [Inspectable, JsonProperty("energy"), Key(4), RuntimeInspectable]  
    public PerformanceStat EnergyUsage = new PerformanceStat();

    [InspectablePrefab, JsonProperty("Particles"), Key(5)]
    public string ParticlesPrefab;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Thruster(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Thruster(this, item);
    }
}

public class Thruster : Behavior, IAnalogBehavior
{
    public float Thrust { get; private set; }
    public float Torque { get; }

    public float Axis
    {
        get => _input;
        set => _input = saturate(value);
    }

    private ThrusterData _data;
    
    private float _input;

    public Thruster(ThrusterData data, EquippedItem item) : base(data, item)
    {
        _data = data;
        var hullData = ItemManager.GetData(Entity.Hull) as HullData;
        var hullCenter = hullData.Shape.CenterOfMass;
        var itemData = ItemManager.GetData(item.EquippableItem);
        var itemCenter = hullData.Shape.Inset(itemData.Shape, item.Position, item.EquippableItem.Rotation).CenterOfMass;
        var toCenter = hullCenter - itemCenter;
        Torque = -dot(normalize(toCenter), float2(1, 0).Rotate(item.EquippableItem.Rotation));
        Thrust = Evaluate(_data.Thrust);
    }

    public Thruster(ThrusterData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        Torque = 0;
        Thrust = Evaluate(_data.Thrust);
    }

    public override bool Execute(float dt)
    {
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, _input);
        if(_input > .01f && Entity.TryConsumeEnergy(_input * Evaluate(_data.EnergyUsage)))
        {
            Thrust = Evaluate(_data.Thrust);
            Entity.Velocity -= Direction.xz * _input * Thrust / Entity.Mass * dt;
            Entity.Direction = mul(Entity.Direction,
                Unity.Mathematics.float2x2.Rotate(_input * Torque * Thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass * dt));
            AddHeat(_input * Evaluate(_data.Heat) * dt);
            var vis = _input * Evaluate(_data.Visibility);
            if (!Entity.VisibilitySources.ContainsKey(this) || vis > Entity.VisibilitySources[this])
                Entity.VisibilitySources[this] = vis;
            return true;
        }
        return false;
    }
}