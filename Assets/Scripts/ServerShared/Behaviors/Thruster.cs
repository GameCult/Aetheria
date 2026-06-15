/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, EntityTypeRestriction(HullType.Ship)]
public class ThrusterConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable]
    public PerformanceStat Visibility = new PerformanceStat();

    [Inspectable]
    public PerformanceStat Heat = new PerformanceStat();

    [Inspectable]
    public PerformanceStat EnergyUsage = new PerformanceStat();

    [InspectablePrefab]
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
    public string ParticlesPrefab { get; }

    public float Axis
    {
        get => _input;
        set => _input = saturate(value);
    }

    private readonly PerformanceStat _thrust;
    private readonly PerformanceStat _visibility;
    private readonly PerformanceStat _heat;
    private readonly PerformanceStat _energyUsage;

    private float _input;

    public Thruster(ThrusterConfig data, EquippedItem item) : base(data, item)
    {
        _thrust = data.Thrust;
        _visibility = data.Visibility;
        _heat = data.Heat;
        _energyUsage = data.EnergyUsage;
        ParticlesPrefab = data.ParticlesPrefab;
        var hullShape = ItemManager.GetRuntimeShape(Entity.Hull);
        var itemShape = ItemManager.GetRuntimeShape(item.EquippableItem);
        var hullCenter = hullShape.CenterOfMass;
        var itemCenter = hullShape.Inset(itemShape, item.Position, item.EquippableItem.Rotation).CenterOfMass;
        var toCenter = hullCenter - itemCenter;
        Torque = -dot(normalize(toCenter), float2(1, 0).Rotate(item.EquippableItem.Rotation));
        Thrust = Evaluate(_thrust);
    }

    public Thruster(ThrusterConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _thrust = data.Thrust;
        _visibility = data.Visibility;
        _heat = data.Heat;
        _energyUsage = data.EnergyUsage;
        ParticlesPrefab = data.ParticlesPrefab;
        Torque = 0;
        Thrust = Evaluate(_thrust);
    }

    public override bool Execute(float dt)
    {
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, _input);
        if(_input > .01f && Entity.TryConsumeEnergy(_input * Evaluate(_energyUsage)))
        {
            Thrust = Evaluate(_thrust);
            Entity.Velocity -= Direction.xz * _input * Thrust / Entity.Mass * dt;
            Entity.Direction = mul(Entity.Direction,
                Unity.Mathematics.float2x2.Rotate(_input * Torque * Thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass * dt));
            AddHeat(_input * Evaluate(_heat) * dt);
            var vis = _input * Evaluate(_visibility);
            if (!Entity.VisibilitySources.TryGetValue(this, out var existingVisibility) || vis > existingVisibility)
                Entity.VisibilitySources[this] = vis;
            return true;
        }
        return false;
    }
}
