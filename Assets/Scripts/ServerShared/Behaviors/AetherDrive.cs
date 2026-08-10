/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), EntityTypeRestriction(HullType.Ship), RuntimeInspectable]
public class AetherDriveData : BehaviorData
{
    [Inspectable, JsonProperty("diameters"), Key(1)]
    public float3 RotorDiameter;
    
    [Inspectable, JsonProperty("masses"), Key(2)]
    public float3 RotorMass;
    
    [Inspectable, JsonProperty("rpm"), Key(3), RuntimeInspectable]
    public PerformanceStat MaximumRpm;
    
    [Inspectable, JsonProperty("couplingLambdas"), Key(4)]
    public float3 CouplingLambda;
    
    [Inspectable, JsonProperty("lambdaMultiplier"), Key(5)]
    public PerformanceStat LambdaMultiplier;
    
    [Inspectable, JsonProperty("couplingEfficiency"), Key(6), RuntimeInspectable]
    public PerformanceStat CouplingEfficiency;
    
    [Inspectable, JsonProperty("torque"), Key(7), RuntimeInspectable]
    public PerformanceStat Torque;
    
    [Inspectable, JsonProperty("torqueProfile"), Key(8), RuntimeInspectable]
    public BezierCurve TorqueProfile;
    
    [Inspectable, JsonProperty("draw"), Key(9), RuntimeInspectable]
    public PerformanceStat EnergyDraw;
    
    [Inspectable, JsonProperty("passiveCoupling"), Key(10), RuntimeInspectable]
    public PerformanceStat PassiveCoupling;

    [InspectableAudioParameter, JsonProperty("rpmAudio"), Key(11), RuntimeInspectable]
    public uint RpmAudioParameter;

    [InspectableAudioParameter, JsonProperty("torqueAudio"), Key(12), RuntimeInspectable]
    public uint TorqueRatioAudioParameter;

    [InspectablePrefab, JsonProperty("particles"), Key(13)]
    public string Particles;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new AetherDrive(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new AetherDrive(this, item);
    }
}

public class AetherDrive : Behavior
{
    private AetherDriveData _data;
    private float3 _axis;

    public float3 Thrust { get; private set; }
    public float3 Rpm { get; private set; }
    public float MaximumRpm { get; private set; }
    public float2 ThrustDirection { get; private set; }

    public AetherDriveData DriveData => _data;

    public float3 Axis
    {
        get => _axis;
        set => _axis = clamp(value, -1, 1);
    }

    public AetherDrive(AetherDriveData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public AetherDrive(AetherDriveData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        var rotorSpeed = Rpm * _data.RotorDiameter / 100;
        
        var forward = normalize(Entity.Direction);
        var right = forward.Rotate(ItemRotation.Clockwise);
            
        var speed = float2(dot(Entity.Velocity, forward), dot(Entity.Velocity, right));
        var couplingEfficiency = Evaluate(_data.CouplingEfficiency);
        var efficiency = float3(saturate(1 - speed / max(rotorSpeed.xy, 1) * sign(_axis.xy)) * couplingEfficiency, 1);

        Thrust = (Rpm - AetheriaMath.Decay(Rpm, _data.CouplingLambda, dt)) * _data.RotorMass * efficiency;

        var couplingLambda = _data.CouplingLambda * Item.Evaluate(_data.LambdaMultiplier) * max(abs(_axis), Evaluate(_data.PassiveCoupling));
        var previousRpm = Rpm;
        Rpm = AetheriaMath.Decay(Rpm, couplingLambda, dt);
        var rpmLoss = previousRpm - Rpm;
        var force = rpmLoss * _data.RotorMass * efficiency;

        var heat = rpmLoss * _data.RotorMass * (1 - couplingEfficiency);
        AddHeat((heat.x + heat.y + heat.z)*ItemManager.GameplaySettings.AetherHeatMultiplier);

        ThrustDirection = forward * (_axis.x * force.x / Entity.Mass) + right * (_axis.y * force.y / Entity.Mass);
        Entity.Velocity += ThrustDirection;
        
        Entity.Direction = mul(Entity.Direction,
            Unity.Mathematics.float2x2.Rotate(force.z * _axis.z * ItemManager.GameplaySettings.AetherTorqueMultiplier / Entity.Mass));

        if(float.IsNaN(Entity.Velocity.x))
            ItemManager.Log("FUCK FUCK FUCK FUCK");
        
        MaximumRpm = Evaluate(_data.MaximumRpm);
        var torqueProfile = float3(
            _data.TorqueProfile.Evaluate(Rpm.x / MaximumRpm),
            _data.TorqueProfile.Evaluate(Rpm.y / MaximumRpm),
            _data.TorqueProfile.Evaluate(Rpm.z / MaximumRpm));
        var potentialTorque = Evaluate(_data.Torque) * torqueProfile;
        var potentialRpmDelta = potentialTorque / length(_data.RotorMass) * dt;
        var actualRpmDelta = min(MaximumRpm - Rpm, potentialRpmDelta);
        var torqueRatio = actualRpmDelta / potentialRpmDelta;
        var draw = torqueRatio * Evaluate(_data.EnergyDraw) / 3;
        
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, max(max(abs(_axis.x), abs(_axis.y)), abs(_axis.z)));
        Item.SetAudioParameter(_data.RpmAudioParameter, (Rpm.x + Rpm.y + Rpm.z) / 3 / MaximumRpm);
        Item.SetAudioParameter(_data.TorqueRatioAudioParameter, max(max(torqueRatio.x, torqueRatio.y), torqueRatio.z));
        
        if (Entity.TryConsumeEnergy((draw.x + draw.y + draw.z)*dt))
        {
            Rpm += actualRpmDelta;
            return true;
        }
        
        
        return false;
    }
}